# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Draftmaster3 is a 2D NASCAR-style racing game built in Unity 6 (6000.4.3f1) using URP. The game simulates racing through a **visual illusion of movement** — the player car stays near screen center while environment objects scroll past using shader-based texture offsets and transform positioning. There is no 3D track geometry.

## Architecture

### Core Loop

1. **VehicleLogic** updates each car's speed, `locationOnTrack`, and turn logic every FixedUpdate
2. The player vehicle publishes its speed to **RaceManager** (`playerSpeedMetres`, `motionOffset`, `playerLocation`)
3. **EnvironmentObjectV2** instances read `RaceManager.playerLocation` to determine visibility, positioning, and shader scroll offset (`_MotionOffset`)
4. **EnvironmentManager** shifts the environment root on the X axis via `RaceManager.playerXShift` to keep the player centered
5. **CameraManager** follows the player via Cinemachine and applies Dutch rotation for banking through turns

### The Scrolling System

Environment objects have a lifecycle: **invisible → slide in (100m before start) → shader scrolling → slide out (100m after end) → invisible**. Each object has `specificStartLocation` and `specificEndLocation` defining where on the track it appears.

- **Scrollable objects**: Texture scrolls via `_MotionOffset` shader property, derived deterministically from `RaceManager.playerLocation / scrollDivisor`. The divisor is computed from the material name (e.g., "ScrollingMaterial512" = 512px) using the formula `(pixelSize / 512f) * 40f`, or from a per-object `scrollSpeedOverride`.
- **Non-scrollable objects**: Positioned purely by transform offset from `playerLocation`. They have no visibility lifecycle — they're always visible.

### Player Switching

The game supports switching between vehicles and an on-foot character mid-race via `setAsPlayer()` methods on VehicleLogic or MovementOnFoot. `RaceManager.setPlayer()` is the central switch point — it updates `playerLocation` immediately, resets all EnvironmentObjectV2 states, updates the camera, and sets the Cinemachine follow target. Input maps must be switched separately ("InCar" vs "OnFoot" via `InputManager.ChangeInputMap`).

### Vehicle & Track Data

Track and vehicle properties are defined as **ScriptableObjects** in `Assets/ScriptableObjects/` (TrackInfo, VehicleInfo) with instances loaded from `Resources/Tracks/` and `Resources/Vehicles/`. TrackInfo defines turn positions, lengths, angles, steering angles, racing lines (lowest/ideal/highest), pit lane curves, and speed profiles. VehicleInfo defines acceleration/deceleration AnimationCurves and top speed.

VehicleLogic calculates dynamic racing lines per turn based on random high/mid/low selection, with speed curves derived from the chosen line and track banking data.

### Key Scripts (attached to Phoenix scene)

- **VehicleLogic.cs** — Car physics, speed, turns, drafting, wrecking, motion offset publishing
- **EnvironmentObjectV2.cs** — Scrollable/static environment element lifecycle and shader control
- **RaceManager.cs** — Central race state, player management, track initialization
- **CameraManager.cs** — Cinemachine camera, zoom, rotation, FPS cap
- **InputManager.cs** — Input System action maps (InCar/OnFoot), direction vector
- **MovementOnFoot.cs** — On-foot player movement with Rigidbody2D
- **EnvironmentManager.cs** — Environment root X-shift, global materials

### Legacy Scripts

Many scripts in `Assets/Scripts/` are from a previous iteration (e.g., `Movement.cs`, `CameraRotate.cs`, `EnviroMovement.cs`, `EnvironmentObject.cs`). The active system uses VehicleLogic + EnvironmentObjectV2 + RaceManager. Legacy scripts may still be referenced by old scene objects or contain static fields read elsewhere — check references before removing.

## Key Conventions

- All game physics runs in **FixedUpdate**, not Update
- `playerLocation` is metres along the track (resets to 0 each lap via `updateTurnCount`)
- `playerXShift` is set to `-player.transform.position.x` on switch, then zeroed in LateUpdate once the player reaches x=0
- Speed is stored in mph (`speed`) and converted to m/s (`speedMetres = speed / 2.237f`)
- Materials encode pixel width in their name (parsed via `GetNumbersFromString`); defaults to 128 if no digits found
- PlayerPrefs is used extensively for persistence (track records, settings, fuel, progression)

### Scene Organization

- **`Assets/Menus/`** — UI scenes (MainMenu, Garage, TrackSelect, SeriesSelect, Store, Settings, etc.)
- **`Assets/Levels/Racetracks/`** — Active racetrack scenes (Phoenix, Daytona, Atlanta, etc.). `Custom/` subdirectory has template tracks for custom track creation.
- **`Assets/Levels/Legacy/`** — Older tracks from a previous iteration, still in the build but may use legacy scripts
- **`Assets/Levels/Scenarios/`** — Special event scenes (DaytonaDay, Halloween, Throwback, Pitlane)
- **`Assets/Levels/`** — Utility scenes (LoginRegister, MyAccount, DeleteAccount)

Phoenix is the primary development/test racetrack scene where the core scripts are wired up.

## Development

This is a Unity 6 (6000.4.3f1) project — open in the Unity Editor, not built from CLI. There is no automated test suite or CI pipeline. Testing is done by entering Play Mode in the editor.

**Build hazard**: Several runtime scripts have `using UnityEditor` imports (RaceManager, VehicleLogic, EnvironmentObjectV2). These will cause build failures for standalone builds. Wrap any editor-only code in `#if UNITY_EDITOR` directives.

## External Services

- **PlayFab** — Leaderboards, player data (via PlayFabManager, PlayFab SDK)
- **Vivox** — Voice chat (package installed, integration status unclear)
- **Netcode for GameObjects** — Multiplayer (package installed, not active in current scenes)
