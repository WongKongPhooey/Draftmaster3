"""Regression test: a completed task is never re-run.

Two guarantees are checked against controller.main():

  1. The moment a task run succeeds, its `done` marker is flushed to disk
     BEFORE the report/commit follow-ups. So even if commit_leftovers() (or
     write_report()) blows up, the completion survives and the task is not
     picked up again on the next tick.
  2. On the next tick, fetch_tasks() returning the same doc lines does not
     re-run an already-done task — the next pending task is chosen instead.

Runs fully offline: every network/git/claude call is monkeypatched, and all
state is redirected to a throwaway temp dir. `--force` skips the idle/pacing
gates; the Unity-MCP and usage gates are stubbed as connected/available.
"""
import json
import sys
import tempfile
from datetime import datetime, timedelta, timezone
from pathlib import Path

import controller
import tasks as tasks_mod
import unity_mcp as unity_mcp_mod
import usage as usage_mod

TWO_TASKS = [
    {"id": tasks_mod.task_id("first task"), "text": "first task"},
    {"id": tasks_mod.task_id("second task"), "text": "second task"},
]


def _future_iso(days):
    return (datetime.now(timezone.utc) + timedelta(days=days)).isoformat()


def _fake_limits():
    # Mid-week, well under the ceiling so pacing math never errors.
    return [
        {"kind": "session", "percent": 5, "resets_at": _future_iso(0.1)},
        {"kind": "weekly_all", "percent": 10, "resets_at": _future_iso(4)},
    ]


def run(tmp, ran_ids, *, commit_raises):
    """Invoke one controller tick with everything stubbed. Returns loaded state."""
    state_dir = Path(tmp)
    controller.STATE_DIR = state_dir
    controller.STATE_FILE = state_dir / "state.json"
    controller.LOCK_FILE = state_dir / "run.lock"
    controller.LOG_FILE = state_dir / "log.txt"
    controller.REPORTS_DIR = state_dir / "reports"

    unity_mcp_mod.check_connection = lambda cfg, log=print: (True, {"project": "Draftmaster3"})
    usage_mod.get_limits = lambda log=print: _fake_limits()
    tasks_mod.fetch_tasks = lambda cfg, log=print: list(TWO_TASKS)

    controller.dirty_paths = lambda repo: set()
    controller.write_report = lambda *a, **k: None

    def fake_run_task(task, cfg):
        ran_ids.append(task["id"])
        return {"ok": True, "summary": "done", "num_turns": 1,
                "usage": {"output_tokens": 1}, "duration_s": 1.0}

    controller.run_task = fake_run_task

    def fake_commit(task, cfg, baseline):
        if commit_raises:
            raise RuntimeError("simulated git explosion")
        return "committed"

    controller.commit_leftovers = fake_commit

    sys.argv = ["controller.py", "--force"]
    controller.main()
    return json.loads(controller.STATE_FILE.read_text(encoding="utf-8"))


def main():
    with tempfile.TemporaryDirectory() as tmp:
        ran = []

        # Tick 1: task succeeds but the commit step throws.
        state = run(tmp, ran, commit_raises=True)
        first_id = TWO_TASKS[0]["id"]
        assert ran == [first_id], f"tick 1 should run first task, ran {ran}"
        assert first_id in state["done"], \
            "BUG: completed task not persisted when commit_leftovers() threw"
        print("PASS: completion persisted despite commit failure")

        # Tick 2: same doc lines; the done task must NOT run again.
        state = run(tmp, ran, commit_raises=False)
        second_id = TWO_TASKS[1]["id"]
        assert ran == [first_id, second_id], \
            f"BUG: done task re-run or wrong task picked, ran {ran}"
        assert second_id in state["done"], "second task should now be done"
        print("PASS: already-done task skipped, next task run exactly once")

    print("ALL PASS")


if __name__ == "__main__":
    main()
