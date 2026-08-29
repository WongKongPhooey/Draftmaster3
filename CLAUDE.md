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

### Key Scripts (legacy scrolling system, attached to Phoenix scene)

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
- **`Assets/Levels/Racetracks/`** — Active racetrack scenes (WatkinsGlen, Phoenix, Daytona, Atlanta, etc.). `Custom/` subdirectory has template tracks for custom track creation. **WatkinsGlen** is the current test scene for the spline-based system.
- **`Assets/Levels/Legacy/`** — Older tracks from a previous iteration, still in the build but may use legacy scripts
- **`Assets/Levels/Scenarios/`** — Special event scenes (DaytonaDay, Halloween, Throwback, Pitlane)
- **`Assets/Levels/`** — Utility scenes (LoginRegister, MyAccount, DeleteAccount)

**WatkinsGlen is the current development/test scene.** It uses the new spline-based system (see below). Phoenix is wired up to the legacy scrolling system.

### The race weekend

A weekend is six half-days (Fri/Sat/Sun, AM and PM) at one track shared by three championships — Trucks,
National and Cup. The player is entered in one: their practice/qualifying/race are drivable and hand off to
the race scene, the other two championships' sessions are watched from a grandstand, and the rest of the
three days is media, signing sessions, sponsor duties and the race-day ceremony, all booked against a clock
where things clash. **Obligations are places, not panels**: committing on the `F10` sheet books an
appointment and points an objective marker at it, and the thing itself happens in the paddock as a
conversation with whoever is waiting — the crew chief at the pit box, the engineer in your motorhome, the
official in the drivers' room, the fans through the fence, the rep under the hospitality awning.
`Assets/Scripts/Weekend/Core/` (asmdef `Draftmaster.Weekend`) holds the pure rules — timetable, ledger,
support-race sim, season championships, press bank, venue map, conversation content;
`Assets/Scripts/Weekend/` holds the runtime (director, schedule screen, `Venues/` builder + hosts). The two
championships the player is not in run every round whether anybody watches or not: `SeasonChampionships`
folds those results into three points tables, gated so Sunday's result is not readable on Friday. Full
guide: `Docs/Race-Weekend.md`.

**The demo flow** starts at `Assets/Scenes/TitleScreen.unity` (build index 0) and runs
TitleScreen → RaceScene (which builds whatever `TrackSelection` names), TitleScreen → **SingleRace**
→ RaceScene (pick a track, a series and a driver — `SingleRaceUI`, the only in-game way to reach the
other 37 venues), or TitleScreen → TeamGarage ("Team Factory"). The garage sheet (`Assets/Scenes/GarageScreen.unity`) is **not** on the title menu — it
is opened by a `LaptopInteractable` in the RV interior or in the factory, and `GarageScreenLoader`
remembers which scene to send BACK to. `Esc > QUIT TO TITLE` in a race scene closes the loop. The whole
chain is asserted by `Assets/Tests/Editor/TitleScreenWiringTests.cs`; the diagram is in `Docs/Editor-Handbook.md`.

### Active System: Spline-Based Tracks

A second, newer system runs alongside the legacy scrolling code:

- **TrackInfoV2** (`Assets/ScriptableObjects/`) — ScriptableObject defining a track as an ordered list of `TrackSegment`s (Straight or Turn) with length, angle, banking, width, plus an embedded `SegmentRacingLine` (ideal / leftmost / rightmost lateral offsets at entry/apex/exit). Stored under `Assets/Resources/Tracks/`.
- **TrackBuilder** — generates the road mesh, edge lines, and pit lane from a TrackInfoV2 at edit time. Provides centerline sampling (`SampleCenterline()`, `SampleAt(distance)`) and a gizmo for the racing line (yellow centerline, green ideal, blue leftmost, red rightmost).
- **SplineDriver** — AI/test driver that walks the spline using a VehicleInfo's accel/decel/cornering curves. Looks ahead `brakingLookahead` metres to brake for upcoming slower segments, applies racing-line lateral offset via `lineFactor` (-1=leftmost, 0=ideal, +1=rightmost), and leans into turns by heading-change rate.
- **AIDriverBinding** — ties a `Draftmaster.Data.Driver` (SQLite row) to a SplineDriver: Aggression skews `lineFactor`, Qualifying/Consistency set `paceMultiplier`.
- **GridSpawner** — instantiates N AI cars, waits on `DatabaseManager.IsReady`, pulls a shuffled driver pool, and applies bindings.
- **DatabaseManager** (`Assets/Scripts/Database/`) — singleton wrapping a sqlite-net-pcl connection at `Application.persistentDataPath/draftmaster.db`. Seeds dummy drivers on first launch via `DummyDrivers.Build()`. Tables live in `Assets/Scripts/Database/Models/` under namespace `Draftmaster.Data`.

### Multi-track structure

A track is one string id (`Daytona`), shared by the `Tracks` table, the calendar, the travel map and the assets. It resolves to three things via `TrackCatalog`: a catalogue row, geometry at `Resources/Tracks/<id>.asset`, and a content package at `Resources/TrackPackages/<id>.prefab`. `TrackSceneLoader` drops the selected package (`TrackSelection`) into the shared race scene (`Assets/Scenes/RaceScene.unity` — managers, no road, all `TrackBuilder` fields null; **a track package left in that scene silently overrides every selection**, so clear previews before saving) and binds it to everything holding a `TrackBuilder` — so the race scene is authored once and the track is content, rather than one scene per round. `TrackDressingFactory` generates each package's ground, walls, grandstands and paddock from the spline, and never overwrites hand-authored pieces. **All 38 venues on the Cup / National / Truck calendars are built.** Real published length, width and banking for each live in `Draftmaster.Tracks.TrackDimensions`, which the catalogue seed and both generators derive from. Ovals are solved from their lap length by `OvalGeometry`; the ten road/street circuits — plus Pocono, a triangle no oval formula closes — are authored corner by corner in `RoadCourseLayouts` and solved by `RoadCourseGeometry`. **WatkinsGlen is hand-measured off satellite imagery and is never regenerated.** `Draftmaster > Tracks > Build All Calendar Tracks` rebuilds the lot. Full pipeline in `Docs/Tracks.md`.

## Development

This is a Unity 6 (6000.4.3f1) project — open in the Unity Editor, not built from CLI. There is no automated test suite or CI pipeline. Testing is done by entering Play Mode in the editor.

Authoring guides live in `Docs/`. **`Docs/Editor-Handbook.md` is the index**: task-by-task click order for building content (add a track, place an NPC, author a quest, sponsors, art/UI passes), plus the full Draftmaster menu reference, the play-mode key map and the standing gotchas — start there and follow its links. `Docs/NPCs-and-Quests.md` covers the paper-doll character system (sprite specs, greyscale/tint rules, editor NPC designer), dialogue (NPCInteractable), and the side-quest system (QuestInfo assets, QuestGiverNPC, stats ledger, inventory, pause-menu mission board). `Docs/Rivalry-and-TeamSwitch.md` covers the driver-relationship/payback system (DriverRelationships, contact blame, AIRacingBehaviour payback, RivalryFeed) and mid-race team car switching (TeamSwitchController, GridSpawner teams). `Docs/Sponsorship.md` covers sponsor contracts (pit-lane reps, haggling, the PlayerPrefs deal book) and the car decal pipeline (panel layout assets, livery baking, placeholder art tools).

**Build hazard**: Several runtime scripts have `using UnityEditor` imports (RaceManager, VehicleLogic, EnvironmentObjectV2). These will cause build failures for standalone builds. Wrap any editor-only code in `#if UNITY_EDITOR` directives.

## External Services

- **PlayFab** — Leaderboards, player data (via PlayFabManager, PlayFab SDK)
- **Vivox** — Voice chat (package installed, integration status unclear)
- **Netcode for GameObjects** — Multiplayer (package installed, not active in current scenes)
