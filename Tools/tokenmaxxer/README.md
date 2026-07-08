# Tokenmaxxer

Overnight autonomous Claude task runner. Windows Task Scheduler fires `controller.py` every 30 minutes; each tick runs at most one task from the Google Doc, gated so the weekly subscription limit lands near `weekly_ceiling_pct` (default 97%) at reset without capping out early.

## Setup (once)

1. Create a Google Doc for tasks. Share it as **"Anyone with the link" (Viewer)**.
2. Copy the doc ID from its URL (`docs.google.com/document/d/<THIS PART>/edit`) into `doc_id` in `config.json`.
3. Run `install.ps1` in PowerShell to register the scheduled task.

## Task doc conventions

- Lines starting with `#` are headings/comments — ignored.
- Every other non-empty line is one task (bullets and numbering are fine — they're stripped).
- The tool never edits the doc. Done tasks are remembered locally by a hash of the line text, so finished lines are skipped even if left in the doc. **Editing a line's wording makes it a new task** and it will run again — delete lines you don't want re-run before rewording.

## How it decides to work (per 30-min tick)

1. **Gates**: no run already in progress; you've been idle ≥ `idle_minutes` (keyboard/mouse); 5-hour window ≤ `five_hour_max_pct`; usage endpoint reachable (fails safe otherwise).
2. **Pacing**: weekly utilization must be below a target curve (`elapsed week fraction × ceiling`), minus a reserve for your own daytime usage — learned each week from how much usage happened outside tool runs.
3. If all pass: run the next pending task headless (`claude -p --model opus --dangerously-skip-permissions`), measure the weekly-% delta it cost, write the morning report, commit.

## Where things live

- Morning reports: `reports/YYYY-MM-DD.md` (committed to develop).
- State + logs: `%LOCALAPPDATA%\Tokenmaxxer\` (`state.json`, `log.txt`).

## Manual controls

```
python controller.py --dry-run   # show gates/pacing decision + pending tasks, run nothing
python controller.py --force     # run one task now, ignoring idle + pacing gates
```

To pause the whole system: disable the "Tokenmaxxer" task in Task Scheduler.

## Config knobs (`config.json`)

| Key | Meaning |
|---|---|
| `doc_id` | Google Doc ID for the task list |
| `tasks_file` | Local txt file overriding the doc (testing) |
| `model` | Model for overnight runs (`opus`) |
| `weekly_ceiling_pct` | Where the week should land at reset (97) |
| `five_hour_max_pct` | Max 5-hour utilization before skipping a tick (70) |
| `idle_minutes` | Minimum keyboard/mouse idle before working (20) |
| `default_user_daily_burn_pct` | Assumed daytime burn/day until a half-day of history exists (8) |
| `task_timeout_minutes` | Hard kill for a single task run (90) |
