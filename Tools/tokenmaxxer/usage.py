"""Read Claude subscription usage limits via the OAuth usage endpoint.

The access token is read from the local Claude Code credentials file and is
never logged or written anywhere else. On a 401 (stale access token) we run a
tiny `claude -p` call, which makes the CLI refresh and rewrite the credentials
file, then retry once.
"""
import json
import shutil
import subprocess
import urllib.error
import urllib.request
from pathlib import Path

USAGE_URL = "https://api.anthropic.com/api/oauth/usage"
CREDENTIALS_PATH = Path.home() / ".claude" / ".credentials.json"


def _read_access_token():
    data = json.loads(CREDENTIALS_PATH.read_text(encoding="utf-8"))
    return data["claudeAiOauth"]["accessToken"]


def _fetch(token):
    req = urllib.request.Request(
        USAGE_URL,
        headers={
            "Authorization": f"Bearer {token}",
            "anthropic-beta": "oauth-2025-04-20",
        },
    )
    with urllib.request.urlopen(req, timeout=30) as resp:
        return json.loads(resp.read().decode("utf-8"))


def _refresh_via_cli(log):
    """Run a minimal claude -p call so the CLI refreshes the stored token."""
    claude = shutil.which("claude") or "claude"
    cmd = [claude, "-p", "ok", "--output-format", "text"]
    if claude.lower().endswith((".cmd", ".bat")):
        cmd = ["cmd.exe", "/c"] + cmd
    log("access token stale; running minimal claude -p to refresh credentials")
    subprocess.run(cmd, capture_output=True, timeout=300)


def get_limits(log=print):
    """Return the parsed limits list, or None on any failure (fail safe).

    Each limit: {kind, group, percent, resets_at, scope, is_active}.
    kind is one of: session (5-hour), weekly_all, weekly_scoped.
    """
    try:
        token = _read_access_token()
    except Exception as e:
        log(f"usage: cannot read credentials: {e}")
        return None

    for attempt in (1, 2):
        try:
            data = _fetch(token)
            return data.get("limits") or None
        except urllib.error.HTTPError as e:
            if e.code == 401 and attempt == 1:
                try:
                    _refresh_via_cli(log)
                    token = _read_access_token()
                    continue
                except Exception as e2:
                    log(f"usage: token refresh failed: {e2}")
                    return None
            log(f"usage: HTTP {e.code} from usage endpoint")
            return None
        except Exception as e:
            log(f"usage: fetch failed: {e}")
            return None
    return None


def find_limit(limits, kind, model=None):
    """Find a limit by kind; for weekly_scoped, match the scope's model name."""
    for lim in limits:
        if lim.get("kind") != kind:
            continue
        if kind == "weekly_scoped":
            scope = lim.get("scope") or {}
            name = ((scope.get("model") or {}).get("display_name") or "").lower()
            if model is None or name != model.lower():
                continue
        return lim
    return None


if __name__ == "__main__":
    limits = get_limits()
    print(json.dumps(limits, indent=2))
