# Tracks

How the game holds 35 racetracks without 35 copies of the race scene.

## The shape of it

A track is one string id — `Daytona`, `Martinsville` — and that id was already the shared key across the
whole game: the `Tracks` table (`Track.Name`), the calendar (`Race.TrackName`), the travel map's circuit
nodes, the geometry asset name, and the legacy per-track scenes. Nothing new was invented; it was just made
resolvable in one call.

Each track is three things, and any of them may be missing while it's being built:

| Piece | Lives at | Made by |
| --- | --- | --- |
| **Catalogue row** — type, length, banking, default laps | `Tracks` table, seeded from `DummyTracks.cs` | Hand-written; 30 rounds already there |
| **Geometry** — the spline `TrackBuilder` builds | `Resources/Tracks/<id>.asset` (`TrackInfoV2`) | `OvalTrackFactory` for ovals, by hand for road courses |
| **Content package** — everything else specific to this track | `Resources/TrackPackages/<id>.prefab` | `Draftmaster > Tracks` window, then dressed in Prefab Mode |

`TrackCatalog` resolves all three. Ask `HasGeometry` / `HasPackage` before offering a track anywhere —
most of the calendar will be catalogue-only for a long time, and `TrackCatalog.Playable()` is what a
track-select screen should list.

## Why packages instead of 35 scenes

The reference scene (`Assets/Scenes/WatkinsGlen.unity`) has ~37 root objects. Roughly two thirds of them
are the same in every race: the player car, `GridSpawner`, `PitLaneStart`, the directors, the HUDs, the
camera, the database. The rest — the road, its environment, the ground, grandstands, the paddock boundary,
spawn markers, the RV — belong to Watkins Glen alone.

With one scene per track, 35 rounds means 35 copies of those shared managers, and every change to the race
flow has to be repeated 35 times. `Assets/Levels/Racetracks/` is that problem already frozen in place: a
folder of scenes on the previous system that nothing can cheaply update.

So: **the race scene is authored once and the track is content.**

- `TrackPackage` marks a per-track prefab and holds its `TrackBuilder`.
- `TrackSceneLoader` runs before the scene's own scripts, instantiates the selected track's package, and
  binds it to every component that wants a `TrackBuilder` — there are about fifteen of those
  (`GridSpawner`, `PitLaneStart`, `LapTimingManager`, `RacePositionTracker`, `SplineDriver`, `PitLimiter`,
  the pit and paddock spawners…), and only fields that are still null get filled, so anything deliberately
  wired to another spline is left alone.
- A scene that already contains a track — WatkinsGlen, authored in place — is **left exactly as it is**.
  The loader adopts its `TrackBuilder` so the rest of the game can ask `TrackPackage.ActiveTrack` either
  way. Nothing had to be torn up to add this.

`TrackSelection` decides which track loads. It's PlayerPrefs-backed because the weekend deliberately
reloads the scene (practice → qualifying → race, then NEXT WEEKEND), and it falls back to the travel map's
current location, so "drive to Martinsville, then race" works without the menu setting anything.

## Adding a track

1. **Catalogue it.** Most of the calendar is already in `DummyTracks.cs` with type, length, banking and
   laps. Bump `DatabaseManager.SchemaVersion` if you edit that seed and want the table rebuilt.
2. **Generate the layout.** `Draftmaster > Tracks > Track Builder Window`, find the row, press
   **Generate Layout**. That writes `Resources/Tracks/<id>.asset` — a closed oval with a racing line, pit
   road and corner-speed hints. Regenerating an existing asset refills it in place, so its GUID (and every
   reference to it) survives.
3. **Build the package.** Press **Build Package**, which writes `Resources/TrackPackages/<id>.prefab`
   containing the road, an `Environment` root and a `Paddock` root — then dresses it (below), so what comes
   out is drivable and walkable without opening it.
4. **Dress it further, if you want to.** Open the package prefab and move or replace any of the generated
   scenery. Hand edits are safe: re-dressing never touches a piece it didn't make.
5. **Race it.** Press **Race** in the window (or call `TrackSelection.Select("<id>")`) and load
   `Assets/Scenes/RaceScene.unity`.

`Draftmaster > Tracks > Report Calendar Coverage` prints what's built and what's still catalogue-only.

## Dressing a track without doing it thirty-five times

None of the trackside furniture is a creative decision — it all follows from the spline — so
`TrackDressingFactory` derives it and writes **real GameObjects into the package prefab**, which you can then
move, delete or replace by hand.

| Piece | Where it comes from |
| --- | --- |
| Ground plane | `TrackGround`, sized from the bounding box of the road + pit lane, tiled at the pixel standard |
| Walls | A generated `TrackEnvironment` asset (`Resources/Tracks/<id>Environment.asset`) driving the existing `TrackEnvironmentBuilder`, with barrier gaps cut at pit entry and exit |
| Grandstands | `Grandstand` quads along every straight ≥ 150 m, on the side facing away from the middle of the circuit |
| Paddock | A `PaddockBoundary` pocket behind the pit lane, with the RV prefab (which carries `SpawnPoint_RV`) and a fallback `PlayerSpawnPoint` in it |
| `Paddock/NPCs` | An empty root — the home for NPCs belonging to this track alone. The paddock regulars are spawned from the pit lane by the shared scene; see `Docs/NPCs-and-Quests.md` §4 |

Two rules make it safe to re-run:

- **Overwrite means "replace what I generated", never "replace what you made."** A piece found outside the
  `Environment`/`Paddock` roots this factory owns is left alone and reported as kept. That's why dressing
  WatkinsGlen — whose package came out of the hand-authored scene, with its ground and stands at the package
  root — adds nothing.
- A hand-made piece doesn't always carry the component that identifies a generated one (Watkins' ground is a
  bare quad, not a `TrackGround`), so the **name** counts as a claim as well as the component.

`Draftmaster > Tracks > Dress All Undressed Packages` fills in every package that's missing pieces; the
window's **Dress** button re-does one (with the Overwrite toggle to replace what was generated before).

What is still hand work, deliberately: kerbs, the pit boxes' furniture, garages, signage, camera towers, and
anything about a track that makes it *that* track.

## The oval generator

`Draftmaster.Tracks.OvalGeometry` (own assembly, unit tested in `OvalGeometryTests`) solves an oval from
its lap length:

- Corner angles are equal arcs summing to exactly 360°, so the heading closes by construction; the back
  stretch is then solved by bisection so the position closes too, and every length is scaled uniformly to
  land the lap on its catalogue distance.
- **Two straights joined by two semicircular ends can only close if they are the same length.** This is
  worth knowing before you try to author a "longer front stretch": a 2.5-mile oval split 56/44 leaves a
  254 m gap, and no corner skew or radius change closes it (measured, not assumed). What makes a real front
  stretch longer is the tri-oval bow, so the **dog-leg is the input and the straight split is the output** —
  give it the kink you want to see and the front stretch comes out longer by however much the bow is worth.
  Martinsville, with no kink, solves to 245.5 m straights; the real ones are 244 m.
- The racing line comes out wide-in / tight-apex / wide-out, with the leftmost and rightmost AI lines
  pinned near the edges so the field can run two and three abreast.
- A **tri-oval** (Daytona, Talladega) is a shallow dog-leg on the front stretch — out, across, back — whose
  angles net to zero, so it bulges toward the grandstand without stealing heading from the corners.
- A **paperclip** (Martinsville, Bristol) is `corners = 2`: one continuous 180 at each end.
- Corner speed hints come from `v = √(g·r·(grip + tan bank))`, so a banked 2.5-miler and a flat bullring
  don't claim the same corner speed.

It is a **starting point, not a finished track.** Real ovals have unequal radii, progressive banking and
asymmetric straights. Generate, then tune the numbers in the inspector with `TrackBuilder`'s racing-line
gizmo on. Road courses aren't generated at all — there's no formula for the Bus Stop; duplicate
`WatkinsGlen.asset` and author by hand.

## Making a superspeedway feel like one

`Draftmaster.Tracks.TrackTuning` holds what separates the types, keyed by `TrackKind` (a mirror of
`Draftmaster.Data.TrackType`), with per-track exceptions layered on top (Talladega drafts harder than
Daytona; Bristol eats tyres faster than Martinsville). Read it through `TrackProfile.Current`.

| | Superspeedway | Speedway | Short track | Road course |
| --- | --- | --- | --- | --- |
| Draft | 1.65 | 1.15 | 0.7 | 0.5 |
| Tyre wear | 0.7 | 1.0 | 1.5 | 1.25 |
| AI line spread | 1.0 | 0.75 | 0.5 | 0.55 |
| Caution proneness | 0.8 | 0.45 | 0.9 | 0.35 |
| Road width | 18 m | 16 m | 13 m | 12 m |
| Pit limit | 55 mph | 45 | 35 | 45 |
| Racing zoom | 26 | 22 | 16 | 20 |

**These are not wired into the sim yet** — the table exists and is tested, but `DraftAero`, the tyre model,
`AIRacingBehaviour`, `GridSpawner` and the camera still use their own constants. Connecting them is the
next job, and it's a one-line change at each site (`* TrackProfile.Current.draftScale` and so on). Doing it
that way round means the numbers can be argued about in one file rather than hunted across the codebase.

## Files

| File | Role |
| --- | --- |
| `Assets/Scripts/Tracks/Core/OvalGeometry.cs` | The solver: spec → segments, pit lane, corner speeds, closure check. Own asmdef, unit tested. |
| `Assets/Scripts/Tracks/Core/TrackTuning.cs` | Per-type feel numbers plus per-track exceptions. |
| `Assets/Scripts/Tracks/TrackCatalog.cs` | id → catalogue row, geometry asset, package prefab. DB first, seed list fallback. |
| `Assets/Scripts/Tracks/TrackSelection.cs` | Which track the next race scene builds. |
| `Assets/Scripts/Tracks/TrackProfile.cs` | Game-side view of `TrackTuning`. |
| `Assets/Scripts/Tracks/TrackPackage.cs` | Marks a per-track content prefab; binds it to the scene. |
| `Assets/Scripts/Tracks/TrackSceneLoader.cs` | Loads the selected package into the shared race scene. |
| `Assets/Scripts/Tracks/OvalTrackFactory.cs` | Adapter: solved geometry → `TrackInfoV2` asset. |
| `Assets/Scripts/Tracks/TrackGround.cs` | The ground plane, sized from the spline's bounding box. |
| `Assets/Editor/TrackAuthoringMenu.cs` | The `Draftmaster > Tracks` window and menu items. |
| `Assets/Editor/TrackDressingFactory.cs` | Ground, walls, grandstands and paddock, derived from the geometry. |
| `Assets/Editor/RaceSceneSplitter.cs` | One-shot: WatkinsGlen → `RaceScene.unity` + a WatkinsGlen package. |
| `Assets/Tests/Editor/OvalGeometryTests.cs` | Lap length, closure, tri-oval, paperclip, racing line, pit lane, tuning. |

## The shared race scene

`Assets/Scenes/RaceScene.unity` is the scene every track loads into: the twenty-odd manager roots (player
car, `GridSpawner`, `PitLaneStart`, the directors, HUDs, camera, database) and **no road**. Every
`TrackBuilder` field in it is deliberately null — `TrackSceneLoader` fills them from whichever package
loads.

`Assets/Scenes/WatkinsGlen.unity` is untouched and still works as the authored reference; the split was done
by copying it, so nothing was migrated by hand. Watkins now also exists as a package
(`Resources/TrackPackages/WatkinsGlen.prefab`), so it loads into RaceScene like any other round.

## Still to do

- **Wire `TrackProfile` into the sim** (draft, tyres, AI spread, camera), as above.
- **Point the multiplayer entry points at RaceScene** — `NetworkLauncher.raceSceneName` and
  `MultiplayerMenuUI` still name `WatkinsGlen`, which works but pins multiplayer to one track.
- **Per-track records and laps**: `TrackInfoV2.trackLaps` and the catalogue's `DefaultLaps` both exist and
  currently disagree in places. Pick the catalogue as the authority when the calendar starts driving races.
