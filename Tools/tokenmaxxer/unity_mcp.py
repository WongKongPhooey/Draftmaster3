"""Confirm the Unity Editor's MCP bridge is connected before doing Unity work.

Every Tokenmaxxer task runs inside the Draftmaster3 Unity project and verifies
itself through Unity MCP (EditMode tests, compile checks via read_console). If
the MCP server is down or no Unity Editor is registered with it, those tasks
cannot verify anything and would make blind, unchecked edits — so the whole run
is gated on a confirmed connection.

Detection (MCP for Unity v9.6+ PluginHub, HTTP transport):
  GET {base}/health        -> the MCP server process is up and healthy
  GET {base}/api/instances -> the Unity Editor plugins currently registered
We require the server healthy AND at least one connected instance whose
`project` matches this repo (its folder name), so a Unity Editor open on some
*other* project does not count as "connected".

Everything here fails safe: any error, timeout, or unexpected shape returns
not-connected, which makes the controller skip the tick rather than run blind.
"""
import json
import urllib.request
from pathlib import Path

DEFAULT_BASE_URL = "http://127.0.0.1:8080"


def _get_json(url, timeout=5):
    with urllib.request.urlopen(url, timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8"))


def expected_project(cfg):
    """Name the Unity plugin registers itself under (its project folder name)."""
    name = cfg.get("unity_project_name")
    if name:
        return name
    return Path(cfg["repo_path"]).name


def check_connection(cfg, log=print):
    """Return (ok: bool, info: dict).

    ok is True only when the MCP server is healthy AND a Unity Editor for this
    project is registered with it. Fails safe (ok=False) on any error.
    """
    base = cfg.get("unity_mcp_url", DEFAULT_BASE_URL).rstrip("/")
    want = expected_project(cfg)

    # 1) Is the MCP server process up and healthy?
    try:
        health = _get_json(f"{base}/health")
    except Exception as e:
        return False, {"reason": f"MCP server unreachable at {base} ({e})"}
    if str(health.get("status", "")).lower() != "healthy":
        return False, {"reason": f"MCP server not healthy: {health.get('status')!r}"}
    server_version = health.get("version")

    # 2) Is a Unity Editor for *this* project registered with it?
    try:
        data = _get_json(f"{base}/api/instances")
    except Exception as e:
        return False, {"reason": f"cannot list Unity instances ({e})",
                       "server_version": server_version}
    instances = data.get("instances") or []
    if not instances:
        return False, {"reason": "MCP server up but no Unity Editor connected",
                       "server_version": server_version}

    match = next((i for i in instances if (i.get("project") or "") == want), None)
    if match is None:
        connected = [i.get("project") for i in instances]
        return False, {"reason": f"Unity connected but not for '{want}' "
                                 f"(connected: {connected})",
                       "server_version": server_version}

    return True, {
        "project": match.get("project"),
        "hash": match.get("hash"),
        "unity_version": match.get("unity_version"),
        "session_id": match.get("session_id"),
        "server_version": server_version,
    }


if __name__ == "__main__":
    ok, info = check_connection({"repo_path": str(Path.cwd())})
    print("connected" if ok else "NOT connected")
    print(json.dumps(info, indent=2))
