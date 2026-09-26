---
name: unity-mcp
description: Check whether the Unity MCP bridge is connected to the Draftmaster3 editor before doing any Unity work. Use when the user asks "is UnityMCP connected?", "is Unity up?", "can you see the editor?", or types /unity-mcp — and as the first step of any task that will drive the Unity Editor over MCP.
---

# Unity MCP connection check

Two calls, no exploration (a third, a curl probe, only if the read fails — see Failures). Do not list resources, do not read the project, do not open files.

1. `ToolSearch` with query `select:ReadMcpResourceTool`
2. `ReadMcpResourceTool` — server `unity`, uri `mcpforunity://editor/state`

Report only, in at most 5 lines:

- connected? (a successful read = yes; also give `unity.instance_id` and `unity_version`)
- `editor.active_scene.name`
- `editor.play_mode.is_playing` / `is_paused`
- `compilation.is_compiling` or `is_domain_reload_pending` if either is true
- `advice.ready_for_tools`, plus `advice.blocking_reasons` if not ready

## Failures — reconnect fallback

If step 2 errors (`Server "unity" is not connected`, "Resource not found", no instance), do not stop
yet. Probe the server directly (Bash, one call):

```bash
curl -s -m 3 -o /dev/null -w "%{http_code}\n" http://127.0.0.1:8080/health; curl -s -m 3 http://127.0.0.1:8080/api/instances | head -c 300
```

The URL comes from the `unity` entry in `~/.claude.json` (`http://127.0.0.1:8080/mcp`). Then:

- **`/health` 200 and `instances` lists `Draftmaster3`** → the bridge is up and only this session's
  MCP link is stale (typical when Unity was not running at session start: the server was refused
  once and Claude Code never retries). **Claude cannot run `/mcp` itself** — it is a built-in CLI
  command, not a skill or tool. Tell the user, in one line: *"Bridge is up, session link is stale —
  type `/mcp`, pick `unity`, Reconnect, then `/unity-mcp` again."* Stop.
- **`/health` 200 but `instances` empty** → server running, Unity editor not attached. User must open
  Window > MCP For Unity and Start/Connect the bridge (a domain reload can drop it). Then `/mcp` reconnect.
- **curl fails / timeout / connection refused** → nothing listening on 8080. Unity is closed or the
  MCP for Unity server was never started. User opens the Draftmaster3 editor, starts the bridge
  (Window > MCP For Unity), then `/mcp` reconnect.

Report the diagnosis in at most 2 lines. Do not retry the read in a loop.

- Multiple instances listed → read `mcpforunity://instances` and ask which, or `set_active_instance` on `Draftmaster3`.

## Standing caveats worth repeating when true

- `is_playing: true` + editor unfocused → game time is frozen; runtime behaviour cannot be ticked over MCP.
- `is_compiling: true` → wait for it before creating or using new types.
- `blocking_reasons: ["stale_status"]` alone → harmless right after a reconnect; the state just
  hasn't ticked. Connected is still yes; re-read once to clear it.
