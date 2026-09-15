# CLAUDE.md

Draftmaster3 is a 2D NASCAR-style racing game in Unity 6 (6000.4.3f1), URP. Open it in the
Editor — it is not built from the CLI. There is no CI; testing is Play Mode plus EditMode tests.

**`Docs/Editor-Handbook.md` is the index** — task-by-task click order for building content, the
full Draftmaster menu reference, the play-mode key map, and the standing gotchas. Start there.
It links the rest: `Docs/Tracks.md` (track pipeline), `Docs/Race-Weekend.md`,
`Docs/NPCs-and-Quests.md` (paper-doll characters, dialogue, quests),
`Docs/Rivalry-and-TeamSwitch.md`, `Docs/Sponsorship.md`.

## Hazards

- **`using UnityEditor` in runtime scripts** (RaceManager, VehicleLogic, EnvironmentObjectV2)
  breaks standalone builds. Wrap editor-only code in `#if UNITY_EDITOR`.
- **Scenes, prefabs and ScriptableObjects are binary-serialised.** grep cannot find GUIDs and a
  text edit corrupts them. Go through the Unity MCP tools.
- **A track package saved into `RaceScene` silently overrides every track selection.**
  `RaceSceneSplitter` now prevents this automatically: editing in context removes its own instance
  when the prefab stage closes, and saving the race scene strips any package still in it.
- **Never re-run a full scene builder for a small change** — builders overwrite hand edits.
- **`WatkinsGlen` is hand-measured off satellite imagery and is never regenerated.**

## Active system: spline-based tracks

- **TrackInfoV2** (`Assets/ScriptableObjects/`, instances in `Assets/Resources/Tracks/`) — a track
  as an ordered list of `TrackSegment`s (Straight or Turn) with length, angle, banking, width, plus
  an embedded `SegmentRacingLine` (ideal / leftmost / rightmost lateral offsets at entry/apex/exit).
- **TrackBuilder** — generates road mesh, edge lines and pit lane from a TrackInfoV2 at edit time.
  Centerline sampling via `SampleCenterline()` / `SampleAt(distance)`. Racing-line gizmo: yellow
  centerline, green ideal, blue leftmost, red rightmost.
- **SplineDriver** — AI/test driver walking the spline on a VehicleInfo's accel/decel/cornering
  curves. Looks ahead `brakingLookahead` metres to brake for slower segments, applies racing-line
  offset via `lineFactor` (-1 leftmost, 0 ideal, +1 rightmost), leans by heading-change rate.
- **AIDriverBinding** — binds a `Draftmaster.Data.Driver` row to a SplineDriver: Aggression skews
  `lineFactor`; Qualifying/Consistency set `paceMultiplier`.
- **GridSpawner** — instantiates N AI cars, waits on `DatabaseManager.IsReady`, pulls a shuffled
  driver pool, applies bindings.
- **DatabaseManager** (`Assets/Scripts/Database/`) — singleton over sqlite-net-pcl at
  `Application.persistentDataPath/draftmaster.db`. Seeds dummy drivers on first launch via
  `DummyDrivers.Build()`. Models in `Assets/Scripts/Database/Models/`, namespace `Draftmaster.Data`.

### Multi-track structure

A track is one string id (`Daytona`) shared by the `Tracks` table, the calendar, the travel map and
the assets. `TrackCatalog` resolves it to three things: a catalogue row, geometry at
`Resources/Tracks/<id>.asset`, and a content package at `Resources/TrackPackages/<id>.prefab`.
`TrackSceneLoader` drops the selected package (`TrackSelection`) into the shared race scene
(`Assets/Scenes/RaceScene.unity` — managers, no road, all `TrackBuilder` fields null) and binds it
to everything holding a `TrackBuilder` — so the race scene is authored once and the track is
content, rather than one scene per round. `TrackDressingFactory` generates each package's ground,
walls, grandstands and paddock from the spline, and never overwrites hand-authored pieces.

**All 38 venues on the Cup / National / Truck calendars are built.** Real published length, width
and banking live in `Draftmaster.Tracks.TrackDimensions`, which the catalogue seed and both
generators derive from. Ovals are solved from lap length by `OvalGeometry`; the ten road/street
circuits — plus Pocono, a triangle no oval formula closes — are authored corner by corner in
`RoadCourseLayouts` and solved by `RoadCourseGeometry`.
`Draftmaster > Tracks > Build All Calendar Tracks` rebuilds the lot. Full pipeline in `Docs/Tracks.md`.

## The race weekend

Six half-days (Fri/Sat/Sun, AM and PM) at one track shared by three championships — Trucks,
National and Cup. The player is entered in one: their practice/qualifying/race are drivable and hand
off to the race scene, the other two championships' sessions are watched from a grandstand, and the
rest is media, signing sessions, sponsor duties and the race-day ceremony, booked against a clock
where things clash.

**Obligations are places, not panels**: committing on the `F10` sheet books an appointment and points
an objective marker at it, and the thing itself happens in the paddock as a conversation with whoever
is waiting — crew chief at the pit box, engineer in your motorhome, official in the drivers' room,
fans through the fence, rep under the hospitality awning.

**The circuit belongs to whoever is running**: `WeekendTrackSessions` / `WeekendTrackState` read the
sheet's clock, so `GridSpawner` puts a field out only for a designated practice, qualifying or race —
the player's own (the full race scene) or another championship's (a cheap kinematic field they cannot
join) — and nothing at all during meetings, media, signings and sponsor duties. The two championships
the player is not in run every round whether anybody watches; `SeasonChampionships` folds those
results into three points tables, gated so Sunday's result is not readable on Friday.

Code: `Assets/Scripts/Weekend/Core/` (asmdef `Draftmaster.Weekend`) holds the pure rules — timetable,
ledger, support-race sim, season championships, press bank, venue map, conversation content.
`Assets/Scripts/Weekend/` holds the runtime (director, schedule screen, `Venues/` builder + hosts).
Full guide: `Docs/Race-Weekend.md`.

## Scenes

- **`Assets/Scenes/`** — the live scenes, and what is in the build: `TitleScreen` (boot),
  `RaceScene` (the shared race scene), `GarageScreen`, `SingleRace`, `DemoMenu`, `IronOvalShowcase`.
- **`Assets/Menus/`** — UI scenes (MainMenu, Garage, TrackSelect, SeriesSelect, Store, Settings).
- **`Assets/Levels/Racetracks/`** — legacy racetrack scenes (Phoenix, Daytona, Atlanta…), `Custom/`
  holds template tracks. Not part of the spline system.
- **`Assets/Levels/Legacy/`** — older tracks, still in the build, may use legacy scripts.
- **`Assets/Levels/Scenarios/`** — special events (DaytonaDay, Halloween, Throwback, Pitlane).
- **`Assets/Levels/`** — utility scenes (LoginRegister, MyAccount, DeleteAccount).

**`Assets/Scenes/RaceScene.unity` is the current development/test scene.** It holds no road — the
track arrives as a package. To edit track content (scenery, paddock, per-track NPCs), open
`RaceScene` and use `Draftmaster > Tracks > Edit Selected Package In Context (Race Scene)`; edits
land in the package, not the scene. The every-track NPC cast lives in `RaceScene`'s own `NPCs` root.
Phoenix is wired to the legacy scrolling system.

There is no `WatkinsGlen.unity` — it was deleted 2026-09-01 because a second scene with a road in it
both confused "which scene do I open" and could never load a package. Watkins Glen lives on as
`Resources/TrackPackages/WatkinsGlen.prefab` like every other venue.

### Demo flow

`TitleScreen` (build index 0) → `RaceScene` (builds whatever `TrackSelection` names), or
TitleScreen → **SingleRace** → RaceScene (pick track, series and driver — `SingleRaceUI`, the only
in-game way to reach the other 37 venues), or TitleScreen → TeamGarage ("Team Factory"). The garage
sheet (`Assets/Scenes/GarageScreen.unity`) is **not** on the title menu — a `LaptopInteractable` in
the RV interior or the factory opens it, and `GarageScreenLoader` remembers which scene to return to.
`Esc > QUIT TO TITLE` in a race scene closes the loop. Asserted by
`Assets/Tests/Editor/TitleScreenWiringTests.cs`; diagram in `Docs/Editor-Handbook.md`.

## Legacy scrolling system (Phoenix scene)

The previous iteration: no 3D track geometry, the player car sits near screen centre and the
environment scrolls past via shader texture offsets (`_MotionOffset`) and transform positioning.
VehicleLogic + EnvironmentObjectV2 + RaceManager + EnvironmentManager + CameraManager, plus
InputManager and MovementOnFoot. Still live in `Assets/Levels/Racetracks/` and
`Assets/Levels/Legacy/`, and many scripts in `Assets/Scripts/` belong to it — check references
before removing anything. Full detail, including the scroll-divisor formula, the object
lifecycle and player switching: **`Docs/Legacy-Scrolling.md`**.

## Conventions

- All game physics runs in **FixedUpdate**, not Update.
- `playerLocation` is metres along the track, reset to 0 each lap via `updateTurnCount`.
- `playerXShift` is set to `-player.transform.position.x` on switch, then zeroed in LateUpdate once
  the player reaches x=0.
- Speed is stored in mph (`speed`); `speedMetres = speed / 2.237f`.
- Materials encode pixel width in their name (parsed by `GetNumbersFromString`), default 128.
- PlayerPrefs is used extensively (track records, settings, fuel, progression).

## External services

**PlayFab** (leaderboards, player data, via PlayFabManager) — **Vivox** (voice, integration status
unclear) — **Netcode for GameObjects** (installed, not active in current scenes).
