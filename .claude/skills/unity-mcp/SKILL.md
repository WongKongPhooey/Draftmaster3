---
name: unity-mcp
description: Check whether the Unity MCP bridge is connected to the Draftmaster3 editor before doing any Unity work. Use when the user asks "is UnityMCP connected?", "is Unity up?", "can you see the editor?", or types /unity-mcp — and as the first step of any task that will drive the Unity Editor over MCP.
---

# Unity MCP connection check

Two calls, no exploration. Do not list resources, do not read the project, do not open files.

1. `ToolSearch` with query `select:ReadMcpResourceTool`
2. `ReadMcpResourceTool` — server `unity`, uri `mcpforunity://editor/state`

Report only, in at most 5 lines:

- connected? (a successful read = yes; also give `unity.instance_id` and `unity_version`)
- `editor.active_scene.name`
- `editor.play_mode.is_playing` / `is_paused`
- `compilation.is_compiling` or `is_domain_reload_pending` if either is true
- `advice.ready_for_tools`, plus `advice.blocking_reasons` if not ready

## Failures

- Read errors / "Resource not found" / no instance → **not connected**. Say so and stop. Fix is on the user's side: Unity Editor must be open with the MCP for Unity bridge running (Window > MCP For Unity).
- Multiple instances listed → read `mcpforunity://instances` and ask which, or `set_active_instance` on `Draftmaster3`.

## Standing caveats worth repeating when true

- `is_playing: true` + editor unfocused → game time is frozen; runtime behaviour cannot be ticked over MCP.
- `is_compiling: true` → wait for it before creating or using new types.
