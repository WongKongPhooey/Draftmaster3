"""Tokenmaxxer controller — one tick of the overnight task runner.

Fired by Windows Task Scheduler every 30 minutes. Each tick runs AT MOST one
task. The gates and pacing math decide whether this tick works or exits:

  gates:  no overlapping run, user idle, 5-hour headroom, usage data present
  pacing: keep weekly utilization under a target curve that lands the week
          near weekly_ceiling_pct at reset, reserving headroom for the
          user's own daytime usage (learned from history)

Flags: --dry-run (print decisions, run nothing), --force (skip idle+pacing).
"""
import argparse
import ctypes
import json
import os
import shutil
import subprocess
import sys
import time
from datetime import datetime, timedelta, timezone
from pathlib import Path

import tasks as tasks_mod
import usage as usage_mod

# Windows consoles default to cp1252; keep prints from crashing on non-ASCII.
if sys.stdout is not None:
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except AttributeError:
        pass

SCRIPT_DIR = Path(__file__).resolve().parent
STATE_DIR = Path(os.environ.get("LOCALAPPDATA", str(Path.home()))) / "Tokenmaxxer"
STATE_FILE = STATE_DIR / "state.json"
LOCK_FILE = STATE_DIR / "run.lock"
LOG_FILE = STATE_DIR / "log.txt"
REPORTS_DIR = SCRIPT_DIR / "reports"


def log(msg):
    line = f"{datetime.now().strftime('%Y-%m-%d %H:%M:%S')} {msg}"
    print(line)
    try:
        STATE_DIR.mkdir(parents=True, exist_ok=True)
        with open(LOG_FILE, "a", encoding="utf-8") as f:
            f.write(line + "\n")
    except OSError:
        pass


def load_config():
    return json.loads((SCRIPT_DIR / "config.json").read_text(encoding="utf-8"))


def load_state():
    if STATE_FILE.exists():
        try:
            return json.loads(STATE_FILE.read_text(encoding="utf-8"))
        except Exception:
            log("state file corrupt; starting fresh")
    return {"done": {}, "failed": {}, "runs": [], "snapshots": []}


def save_state(state):
    STATE_DIR.mkdir(parents=True, exist_ok=True)
    state["snapshots"] = state["snapshots"][-2000:]
    state["runs"] = state["runs"][-500:]
    tmp = STATE_FILE.with_suffix(".tmp")
    tmp.write_text(json.dumps(state, indent=1), encoding="utf-8")
    tmp.replace(STATE_FILE)


def user_idle_minutes():
    class LASTINPUTINFO(ctypes.Structure):
        _fields_ = [("cbSize", ctypes.c_uint), ("dwTime", ctypes.c_uint)]

    lii = LASTINPUTINFO()
    lii.cbSize = ctypes.sizeof(LASTINPUTINFO)
    if not ctypes.windll.user32.GetLastInputInfo(ctypes.byref(lii)):
        return 0.0
    millis = ctypes.windll.kernel32.GetTickCount() - lii.dwTime
    return millis / 60000.0


def parse_ts(iso):
    return datetime.fromisoformat(iso)


def now_utc():
    return datetime.now(timezone.utc)


# ---------------------------------------------------------------- pacing ----

def pacing_decision(limits, state, cfg):
    """Return (allowed: bool, info: dict) for whether a run fits the pace."""
    ceiling = cfg["weekly_ceiling_pct"]
    weekly = usage_mod.find_limit(limits, "weekly_all")
    if weekly is None:
        return False, {"reason": "no weekly_all limit in usage data"}

    resets_at = parse_ts(weekly["resets_at"])
    now = now_utc()
    week_start = resets_at - timedelta(days=7)
    days_elapsed = max((now - week_start).total_seconds() / 86400.0, 0.01)
    days_remaining = max((resets_at - now).total_seconds() / 86400.0, 0.0)
    elapsed_frac = min(days_elapsed / 7.0, 1.0)
    target = elapsed_frac * ceiling

    # How much of this week's usage was ours vs the user's own?
    week_start_ts = week_start.timestamp()
    our_delta = sum(
        r.get("weekly_delta", 0.0) for r in state["runs"] if r["ts"] >= week_start_ts
    )
    user_burn_total = max(0.0, weekly["percent"] - our_delta)
    if days_elapsed >= 0.5:
        user_daily = user_burn_total / days_elapsed
    else:
        user_daily = cfg["default_user_daily_burn_pct"]
    reserve = user_daily * days_remaining
    allowed_now = min(target, ceiling - reserve)

    info = {
        "weekly_pct": weekly["percent"],
        "target_pct": round(target, 1),
        "allowed_now_pct": round(allowed_now, 1),
        "user_daily_burn_pct": round(user_daily, 2),
        "reserve_pct": round(reserve, 1),
        "days_remaining": round(days_remaining, 2),
        "resets_at": weekly["resets_at"],
    }

    if weekly["percent"] >= allowed_now - 1:  # 1% hysteresis
        info["reason"] = "weekly at/above pace"
        return False, info

    # Don't run ahead of the curve on the run model's scoped weekly limit either.
    scoped = usage_mod.find_limit(limits, "weekly_scoped", model=cfg["model"])
    if scoped is not None:
        info["scoped_pct"] = scoped["percent"]
        if scoped["percent"] >= target - 1:
            info["reason"] = f"{cfg['model']} scoped weekly at/above pace"
            return False, info

    return True, info


# ------------------------------------------------------------- execution ----

def claude_cmd(cfg):
    # Prompt goes via stdin, NOT argv: multiline argv gets truncated at the
    # first newline when relayed through cmd.exe, silently dropping the task
    # text and any flags that follow it.
    claude = shutil.which("claude") or "claude"
    cmd = [
        claude,
        "--model", cfg["model"],
        "--dangerously-skip-permissions",
        "--output-format", "json",
        "-p",
    ]
    if claude.lower().endswith((".cmd", ".bat")):
        cmd = ["cmd.exe", "/c"] + cmd
    return cmd


def run_task(task, cfg):
    template = (SCRIPT_DIR / "prompt_template.txt").read_text(encoding="utf-8")
    prompt = template.replace("{task}", task["text"])
    timeout_s = cfg["task_timeout_minutes"] * 60
    started = time.time()
    try:
        proc = subprocess.run(
            claude_cmd(cfg),
            input=prompt,
            cwd=cfg["repo_path"],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=timeout_s,
        )
        duration = time.time() - started
        try:
            result = json.loads(proc.stdout.strip())
        except Exception:
            return {
                "ok": False,
                "summary": f"claude exited {proc.returncode}; unparseable output: "
                           f"{(proc.stdout or proc.stderr)[:500]}",
                "duration_s": duration,
            }
        return {
            "ok": not result.get("is_error", False),
            "summary": (result.get("result") or "")[:4000],
            "num_turns": result.get("num_turns"),
            "usage": result.get("usage", {}),
            "duration_s": duration,
        }
    except subprocess.TimeoutExpired:
        return {
            "ok": False,
            "summary": f"timed out after {cfg['task_timeout_minutes']} min (killed)",
            "duration_s": time.time() - started,
        }


def git(repo, *args):
    return subprocess.run(
        ["git", "-C", repo] + list(args),
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )


def dirty_paths(repo):
    """Set of changed/untracked paths (files listed individually)."""
    r = git(repo, "status", "--porcelain", "-uall")
    paths = set()
    for line in r.stdout.splitlines():
        if len(line) > 3:
            p = line[3:].strip().strip('"')
            if " -> " in p:
                p = p.split(" -> ")[-1]
            paths.add(p)
    return paths


def commit_leftovers(task, cfg, baseline):
    """Commit the report + anything that changed DURING the run and wasn't
    committed by Claude itself. Pre-existing dirty files (Josh's WIP) are
    never touched — only paths new relative to the pre-run baseline."""
    repo = cfg["repo_path"]
    new_paths = dirty_paths(repo) - baseline
    if not new_paths:
        return "nothing to commit"
    for p in sorted(new_paths):
        git(repo, "add", "--", p)
    staged = git(repo, "diff", "--cached", "--name-only")
    if not staged.stdout.strip():
        return "nothing to commit"
    msg = (
        f"Overnight: {task['text'][:70]}\n\n"
        "Automated tokenmaxxer run.\n\n"
        "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
    )
    r = git(repo, "commit", "-m", msg)
    return "committed" if r.returncode == 0 else f"commit failed: {r.stderr[:300]}"


def write_report(task, outcome, deltas, cfg):
    REPORTS_DIR.mkdir(parents=True, exist_ok=True)
    path = REPORTS_DIR / f"{datetime.now().strftime('%Y-%m-%d')}.md"
    usage = outcome.get("usage", {})
    lines = [
        f"\n## {datetime.now().strftime('%H:%M')} — {task['text']}",
        f"- outcome: {'ok' if outcome['ok'] else 'ERROR'}"
        f" | {outcome['duration_s'] / 60:.1f} min"
        f" | turns: {outcome.get('num_turns', '?')}"
        f" | out-tokens: {usage.get('output_tokens', '?')}"
        f" | weekly Δ: {deltas.get('weekly', '?')}%",
        "",
        outcome.get("summary", "").strip(),
        "",
    ]
    new_file = not path.exists()
    with open(path, "a", encoding="utf-8") as f:
        if new_file:
            f.write(f"# Overnight report — {datetime.now().strftime('%Y-%m-%d')}\n")
        f.write("\n".join(lines))


# ------------------------------------------------------------------ main ----

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--force", action="store_true")
    args = ap.parse_args()

    cfg = load_config()
    state = load_state()
    state.setdefault("failed", {})

    # --- lock ---
    STATE_DIR.mkdir(parents=True, exist_ok=True)
    if LOCK_FILE.exists():
        age_min = (time.time() - LOCK_FILE.stat().st_mtime) / 60
        if age_min < cfg["task_timeout_minutes"] + 10:
            log(f"gate: lock held ({age_min:.0f} min old); exiting")
            return
        log("stale lock removed")
        LOCK_FILE.unlink()

    # --- usage (required even for --force: measures the run's cost) ---
    limits = usage_mod.get_limits(log)
    if limits is None:
        log("gate: no usage data; failing safe (no run)")
        return
    session = usage_mod.find_limit(limits, "session")
    weekly = usage_mod.find_limit(limits, "weekly_all")
    state["snapshots"].append({
        "ts": time.time(),
        "session_pct": session["percent"] if session else None,
        "weekly_pct": weekly["percent"] if weekly else None,
    })

    allowed, info = pacing_decision(limits, state, cfg)
    idle = user_idle_minutes()

    if args.dry_run:
        log(f"dry-run: idle={idle:.1f} min, session={session['percent'] if session else '?'}%")
        log(f"dry-run: pacing → allowed={allowed} {json.dumps(info)}")
        task_list = tasks_mod.fetch_tasks(cfg, log)
        if task_list is not None:
            pending = [t for t in task_list if t["id"] not in state["done"]
                       and state["failed"].get(t["id"], 0) < 2]
            log(f"dry-run: {len(pending)} pending task(s)"
                + (f"; next: {pending[0]['text'][:80]}" if pending else ""))
        save_state(state)
        return

    if not args.force:
        if idle < cfg["idle_minutes"]:
            log(f"gate: user active (idle {idle:.1f} < {cfg['idle_minutes']} min)")
            save_state(state)
            return
        if session and session["percent"] > cfg["five_hour_max_pct"]:
            log(f"gate: 5-hour at {session['percent']}% > {cfg['five_hour_max_pct']}%")
            save_state(state)
            return
        if not allowed:
            log(f"gate: pacing says wait — {json.dumps(info)}")
            save_state(state)
            return

    # --- pick a task ---
    task_list = tasks_mod.fetch_tasks(cfg, log)
    if task_list is None:
        save_state(state)
        return
    pending = [t for t in task_list if t["id"] not in state["done"]
               and state["failed"].get(t["id"], 0) < 2]
    if not pending:
        log("no pending tasks")
        save_state(state)
        return
    task = pending[0]

    # --- run ---
    LOCK_FILE.write_text(str(os.getpid()))
    try:
        log(f"running task {task['id']}: {task['text'][:100]}")
        before = {"weekly": weekly["percent"] if weekly else None}
        baseline = dirty_paths(cfg["repo_path"])
        outcome = run_task(task, cfg)

        after_limits = usage_mod.get_limits(log) or []
        weekly_after = usage_mod.find_limit(after_limits, "weekly_all")
        deltas = {}
        if weekly_after and before["weekly"] is not None:
            deltas["weekly"] = round(weekly_after["percent"] - before["weekly"], 1)

        write_report(task, outcome, deltas, cfg)
        commit_result = commit_leftovers(task, cfg, baseline)
        log(f"task {'ok' if outcome['ok'] else 'ERROR'} in "
            f"{outcome['duration_s'] / 60:.1f} min; git: {commit_result}; "
            f"weekly Δ {deltas.get('weekly', '?')}%")

        if outcome["ok"]:
            state["done"][task["id"]] = {
                "task": task["text"],
                "completed_at": datetime.now().isoformat(timespec="seconds"),
            }
        else:
            # Retry on a later tick; park after 2 failures so a broken task
            # can't burn the budget all night.
            state["failed"][task["id"]] = state["failed"].get(task["id"], 0) + 1
        state["runs"].append({
            "ts": time.time(),
            "task_id": task["id"],
            "ok": outcome["ok"],
            "duration_s": round(outcome["duration_s"]),
            "num_turns": outcome.get("num_turns"),
            "output_tokens": outcome.get("usage", {}).get("output_tokens"),
            "weekly_delta": deltas.get("weekly", 0.0) or 0.0,
        })
        save_state(state)
    finally:
        LOCK_FILE.unlink(missing_ok=True)


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        log(f"FATAL: {e!r}")
        sys.exit(1)
