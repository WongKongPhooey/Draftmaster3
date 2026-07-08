"""Fetch and parse the task list.

Primary source: a Google Doc shared as "anyone with the link (Viewer)",
fetched via the plain-text export URL (no auth). Optional override for local
testing: a `tasks_file` path in config.

Doc conventions:
  - Lines starting with '#' are headings/comments — ignored.
  - Every other non-empty line is one task. Leading bullets (-, *, numbers)
    and indentation are stripped, so both typed dashes and Google Docs
    bullet lists work.

The doc is read-only to the tool. Completion is tracked locally by task ID
(hash of the normalized text), so finished lines are never re-run even if
they stay in the doc.
"""
import hashlib
import re
import urllib.request
from pathlib import Path

EXPORT_URL = "https://docs.google.com/document/d/{doc_id}/export?format=txt"
_BULLET_RE = re.compile(r"^[\s]*(?:[-*•●○▪]|\d+[.)])\s*")


def task_id(text):
    normalized = " ".join(text.lower().split())
    return hashlib.sha1(normalized.encode("utf-8")).hexdigest()[:12]


def _parse(raw):
    tasks = []
    for line in raw.splitlines():
        line = _BULLET_RE.sub("", line).strip().lstrip("﻿")
        if not line or line.startswith("#"):
            continue
        tasks.append({"id": task_id(line), "text": line})
    return tasks


def fetch_tasks(config, log=print):
    """Return list of {id, text}, or None on failure (fail safe)."""
    tasks_file = config.get("tasks_file")
    if tasks_file:
        try:
            return _parse(Path(tasks_file).read_text(encoding="utf-8"))
        except Exception as e:
            log(f"tasks: cannot read tasks_file {tasks_file}: {e}")
            return None

    doc_id = config.get("doc_id")
    if not doc_id:
        log("tasks: no doc_id configured (and no tasks_file)")
        return None
    try:
        with urllib.request.urlopen(EXPORT_URL.format(doc_id=doc_id), timeout=30) as resp:
            raw = resp.read().decode("utf-8", errors="replace")
    except Exception as e:
        log(f"tasks: doc fetch failed: {e}")
        return None
    if raw.lstrip().lower().startswith(("<!doctype", "<html")):
        log("tasks: doc export returned HTML — is the doc shared 'anyone with the link'?")
        return None
    return _parse(raw)
