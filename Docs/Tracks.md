# Tracks

How the game holds every track on the Cup, National and Truck calendars without one copy of the
race scene per track.

## The shape of it

A track is one string id — `Daytona`, `Martinsville` — and that id was already the shared key across the
whole game: the `Tracks` table (`Track.Name`), the calendar (`Race.TrackName`), the travel map's circuit
nodes, the geometry asset name, and the legacy per-track scenes. Nothing new was invented; it was just made
resolvable in one call.

Each track is three things, and any of them may be missing while it's being built:

| Piece | Lives at | Made by |
| --- | --- | --- |
| **Catalogue row** — type, length, banking, default laps | `Tracks` table, seeded from `DummyTracks.cs` | Hand-written; 30 rounds already there |
| **Geometry** — the spline `TrackBuilder` builds | `Resources/Tracks/<id>.asset` (`TrackInfoV2`) | `OvalTrackFactory` for ovals, `RoadCourseFactory` for authored layouts |
| **Content package** — everything else specific to this track | `Resources/TrackPackages/<id>.prefab` | `Draftmaster > Tracks` window, then dressed in Prefab Mode |

`TrackCatalog` resolves all three. Ask `HasGeometry` / `HasPackage` before offering a track anywhere —
most of the calendar will be catalogue-only for a long time, and `TrackCatalog.Playable()` is what a
track-select screen should list.

## Why packages instead of 35 scenes

The reference scene this all came out of (`Assets/Scenes/WatkinsGlen.unity`, since deleted) had ~33 root
objects. Roughly two thirds of them are the same in every race: the player car, `GridSpawner`,
`PitLaneStart`, the directors, the HUDs, the camera, the database. The rest — the road, its environment,
the ground, grandstands, the paddock boundary, spawn markers, the RV — belonged to Watkins Glen alone.

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
- A scene that already contains a track is **left exactly as it is**: the loader adopts its `TrackBuilder`
  so the rest of the game can ask `TrackPackage.ActiveTrack` either way. That is why a package preview left
  saved in `RaceScene` silently pins every race to that one track — clear previews before saving.

`TrackSelection` decides which track loads. It's PlayerPrefs-backed because the weekend deliberately
reloads the scene (practice → qualifying → race, then NEXT WEEKEND), and it falls back to the travel map's
current location, so "drive to Martinsville, then race" works without the menu setting anything.

## Adding a track

1. **Catalogue it.** Add a row to `TrackDimensions` with the venue's published length, width and
   banking; `DummyTracks` derives its calendar rows from that table, so nothing needs typing twice.
   Bump `DatabaseManager.SchemaVersion` if you want the `Tracks` table rebuilt from the new seed.
2. **Generate the layout.** `Draftmaster > Tracks > Track Builder Window`, find the row, press
   **Generate Layout**. That writes `Resources/Tracks/<id>.asset` — a closed lap with a racing line, pit
   road and corner-speed hints. Ovals are solved from the length; a road course needs its corner
   sequence in `RoadCourseLayouts` first. Regenerating an existing asset refills it in place, so its
   GUID (and every reference to it) survives.
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

## Where the numbers come from

`Draftmaster.Tracks.TrackDimensions` is the one table of real-world measurements: every venue on the
Cup / National / Truck calendars with its published lap length, racing-surface width, turn banking and
straight banking, plus which of the three championships visit it. Widths are authored in **feet**, because
that is how American ovals publish them, and converted once.

It exists because width used to come from the track TYPE - every superspeedway 18 m, every short track
13 m. That is wrong in a way you feel from the driver's seat: **Michigan is 73 feet wide and Dover is 40,
and both are "speedways"**. Bristol and Martinsville are both half-mile bullrings and are not the same
width either (they are, as it happens, both 40 ft - which is also exactly Daytona's width, so the
intuition that a bullring is the narrower road turns out to be false).

Three things derive from that one table, so they cannot drift apart:

- the **catalogue seed** (`DummyTracks` builds its calendar rows from it),
- the **layout generator** (`OvalGeometry.ApplyTrackShape` layers it over the type defaults),
- and the **road-course specs** (`RoadCourseLayouts.Spec`).

Each row carries a `confidence`: `Published` (the venue's own spec), `Measured` (taken off satellite
imagery for this project - Watkins Glen only) or `Estimated` (no published figure; Bowman Gray's width and
the San Diego street circuit's whole layout). `Draftmaster > Tracks > Report Track Dimensions` prints the
table with that column, so what is a real number and what is a guess is visible without opening the source.

**What is exact**: lap length, width, banking. **What is not**: the corner-by-corner shape. A generated
layout drives the right distance at the right speed in the right width of road; it is not a survey.

## The oval generator

`Draftmaster.Tracks.OvalGeometry` (own assembly, unit tested in `OvalGeometryTests`) solves an oval from
its lap length:

- Corner angles are equal arcs summing to exactly 360 degrees, so the heading closes by construction; the
  back stretch is then solved by bisection so the position closes too, and every length is scaled uniformly
  to land the lap on its catalogue distance.
- **Two straights joined by two semicircular ends can only close if they are the same length.** This is
  worth knowing before you try to author a "longer front stretch": a 2.5-mile oval split 56/44 leaves a
  254 m gap, and no corner skew or radius change closes it (measured, not assumed). What makes a real front
  stretch longer is the tri-oval bow, so the **dog-leg is the input and the straight split is the output** -
  give it the kink you want to see and the front stretch comes out longer by however much the bow is worth.
  Martinsville, with no kink, solves to 245.5 m straights; the real ones are 244 m.
- The racing line comes out wide-in / tight-apex / wide-out, with the leftmost and rightmost AI lines
  pinned near the edges so the field can run two and three abreast. That margin scales with the road now -
  a fixed 1.5 m left almost nothing between the lines on a 40 ft track.
- A **tri-oval** (Daytona, Talladega) is a shallow dog-leg on the front stretch - out, across, back - whose
  angles net to zero, so it bulges toward the grandstand without stealing heading from the corners.
- A **paperclip** (Martinsville, Bristol) is `corners = 2`: one continuous 180 at each end.
- Corner speed hints come from `v = sqrt(g*r*(grip + tan bank))`, so a banked 2.5-miler and a flat bullring
  do not claim the same corner speed.

It is a **starting point, not a finished track.** Real ovals have unequal radii and progressive banking.
Generate, then tune the numbers in the inspector with `TrackBuilder`'s racing-line gizmo on.

## Traced geometry, which beats any formula

A published lap length describes a **distance, not a shape**. "A 1,551 ft back stretch on a 1.022 mile lap"
is equally true of a long thin oval and of a rounded triangle — and Phoenix is the second one, but was
generated as the first: its corners came out 34 m tighter than the real ones and its straights 92 m too
long. No amount of tuning `turnShareOfLap` fixes that, because the input never carried the shape.

A traced centreline does. `Assets/TrackTraces/<id>.json` holds a circuit as OpenStreetMap draws it, and
`Draftmaster > Tracks > Import Traced Geometry For Every Trace` reads the shape back out of it:

1. **Project** lat/lon to metres (longitude scaled by the cosine of the latitude, or every circuit comes
   out stretched along one axis).
2. **Resample** to an even spacing. Mappers click densely round corners and sparsely down straights, so raw
   node spacing measures the tracing rather than the road, and curvature computed off it is nonsense.
3. **Smooth**, then **segment** on degrees-per-metre: a run above the threshold is a corner, below it a
   straight. Slivers are merged, so a wobble in somebody's tracing does not come back as a corner.
4. **Close the lap.** Corner angles fix every heading, so each corner contributes a fixed displacement and
   each straight contributes its length along a fixed direction — closure is linear in the lengths, and
   `LapGeometry` takes the least-norm answer, which disturbs the measured numbers least.
5. **Rescale** to the published lap length. Uniform, so a closed lap stays closed.

**Only the plan view comes from the trace.** Banking is not in OSM at all and width almost never is for
raceways, so those, the pit lane, the speed limits and the lap counts still come from `TrackDimensions`;
the import replaces a track asset's main line and nothing else.

### Closing an oval is not the same problem as closing a road course

Prefer to move **only the straights** — on a hand-measured lap the corners are the part that was actually
surveyed. That works for a rounded triangle or a road course, whose straights point in several directions.

It cannot work for a plain oval. Its two straights are **antiparallel**, so between them they can only move
the far end of the lap along one axis; the perpendicular error has nowhere to go and the solve either fails
or asks for a straight of negative length. On an oval that error *is* the corner radius, so the fallback
lets every segment give a little — for a corner, its arc length and therefore its radius. Both paths are in
`LapGeometry.Close`, and both are tested.

### Getting the traces

`Assets/TrackTraces/README.md` covers provenance and the ODbL, which is worth settling deliberately before
this ships. The fetch is a manual step on purpose: traces are committed so an import is reproducible,
needs no network, and shows a change to a track's shape as a reviewable diff rather than one appearing
because somebody edited a map.

What identifies a circuit is not its coordinates but a **check against the published lap length** — a
venue has several similar rings near each other (the racing surface, edges traced separately, an infield
road course, a kart track) and only the length test tells them apart.

### The import refuses its own bad readings

How much the closure solve had to move is the honest measure of whether a reading describes the circuit.
A good one is a few metres out. A bad one is hundreds, and the solve then buys closure by moving corner
radii and straight lengths that were measured off the real thing — so past **3% of the lap** the import is
refused and the asset left alone. That is what keeps a poor trace of a road course from replacing a layout
someone authored corner by corner.

Twenty-one venues have a trace and thirteen of them import: Bristol, Bowman Gray, Darlington, Dover, Fort
Worth, Gateway, Iowa, Las Vegas, Michigan, New Hampshire, Phoenix, Pocono and Richmond. The rest are
refused for now. Watkins Glen is skipped by name — it is hand-measured off satellite imagery.

The other twelve venues have **no usable trace at all**, and that is a hole in OSM rather than in the
fetch: Talladega has 3,345m of its 4,281m lap drawn as raceway and Daytona 3,368m of 4,023m, the rest
simply absent. Those keep their generated geometry, which is why the generator stays.

## Road courses, and the one oval no formula fits

An oval is a formula. A road course is not - so the ten road and street circuits are **authored corner by
corner** in `RoadCourseLayouts` and solved by `RoadCourseGeometry`. A layout reads in lap order:

```csharp
S("Front Straight", 430),        // a real straight
C("Turn 2 Keyhole", 110, -170),  // 110 m of arc turning 170 degrees RIGHT (negative = right)
L(160),                          // a connector - takes gentle curvature when the lap is solved
```

**Why the connectors matter.** Guess twenty corner angles off a track map and they sum to something like
700 degrees, not the 360 that any simple closed loop must turn through, and the two ends of the circuit
miss each other by hundreds of metres. Both errors have the same honest home: the connecting sections. The
"straights" of a real road course are not straight - Road America's Moraine Sweep and the run down to
Canada Corner both bend, and that gentle curvature is exactly what lets a circuit with eight
ninety-degree right-handers still come back to where it started. So:

1. **Heading.** Named corners keep their authored angles. The leftover is shared across the links by
   length, so a long sweep takes more of the bend than a short link and nothing becomes an accidental
   hairpin.
2. **Position.** The loop is then shut by changing lengths - and this part is *exact, not a search*. A
   piece's displacement is linear in its own length (an arc of fixed angle just changes radius), and a
   length change never moves any heading downstream, so one weighted least-norm solve puts the gap at zero.
   Straights are weighted to give up length four times as readily as curved links.
3. **Length.** Everything is scaled by one factor to hit the published distance. Uniform scaling cannot
   reopen the loop, because a closed shape stays closed when you scale it.

Steps 1 and 2 interact, so they alternate for a handful of passes. Every circuit comes out closing to
under a centimetre, at its exact published lap length, with no self-intersections.

**Pocono is on this path too**, despite being an oval. It is a triangle with three straights of different
lengths and three corners of different radius *and* different banking (14, 8 and 6 degrees) - precisely
what the two-ends oval solver cannot express; it left the lap 1.5 km short of closing. Authored here, each
corner carries its own numbers. The rule is simply: **an authored layout always wins over the oval solver,
whatever the catalogue type says.**

**Watkins Glen is never generated.** It was measured off satellite imagery by hand and is the reference the
others are aiming at; `RoadCourseLayouts.Has` returns false for it and Build All skips it. What the
generated circuits get right is lap distance, width, corner count, and the order and relative severity of
the corners. What they do not get right is the exact position of each apex. To improve one, open the map,
correct the angles and arc lengths in place, and rebuild - the solver re-closes the lap for you, so a
partial correction is always safe to commit.

## Building the whole calendar

`Draftmaster > Tracks > Build All Calendar Tracks` does layout, package and dressing for all 37 generated
venues in one press (Watkins Glen is skipped). Existing packages are left alone - the geometry inside them
is refilled in place, so GUIDs and hand-dressing survive. **Rebuild All Calendar Tracks (replace packages)**
throws the package prefabs away and regenerates them.

> **Do not wrap that loop in `AssetDatabase.StartAssetEditing()`.** It pauses importing, so the `.asset`
> written by `GenerateGeometry` cannot be loaded back by `BuildPackage` on the very next line. The first
> run of this did exactly that: 37 layouts were written, every package silently failed with "no layout,
> generate one first", and the summary still said "built 37".

`BuiltTrackAssetTests` guards that outcome by reading the assets **on disk** - length, width, closure, pit
lane, package wiring - rather than re-running the solver. It reaches them through `SerializedObject`,
because `TrackInfoV2` lives in Assembly-CSharp and an assembly definition cannot reference the predefined
assemblies.

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
| `Assets/Scripts/Tracks/Core/TrackDimensions.cs` | Published length, width and banking for every venue on the three calendars. |
| `Assets/Scripts/Tracks/Core/RoadCourseGeometry.cs` | The road-course solver: residual curvature onto the links, exact closure by least-norm. |
| `Assets/Scripts/Tracks/Core/RoadCourseLayouts.cs` | The authored corner sequences, circuit by circuit (plus Pocono). |
| `Assets/Scripts/Tracks/Core/LapGeometry.cs` | A lap as straights and corners: does it close, and make it. Used by both hand-measured and traced geometry. |
| `Assets/Scripts/Tracks/Core/OsmTrackGeometry.cs` | Reading a shape out of a traced centreline: project, resample, smooth, segment. |
| `Assets/Editor/OsmTrackImporter.cs` | Trace JSON → a track asset's main line. Banking and width still come from `TrackDimensions`. |
| `Assets/Editor/LegacyTrackPort.cs` | The other way in: a legacy `TrackInfo` (Phoenix, hand-measured) ported to the spline system. |
| `Assets/TrackTraces/` | The committed traces, one JSON per venue, plus their provenance and licence. |
| `Assets/Scripts/Tracks/RoadCourseFactory.cs` | Adapter: solved road course -> `TrackInfoV2` asset. |
| `Assets/Scripts/Tracks/TrackCatalog.cs` | id → catalogue row, geometry asset, package prefab. DB first, seed list fallback. |
| `Assets/Scripts/Tracks/TrackSelection.cs` | Which track the next race scene builds. |
| `Assets/Scripts/Tracks/TrackProfile.cs` | Game-side view of `TrackTuning`. |
| `Assets/Scripts/Tracks/TrackPackage.cs` | Marks a per-track content prefab; binds it to the scene. |
| `Assets/Scripts/Tracks/TrackSceneLoader.cs` | Loads the selected package into the shared race scene. |
| `Assets/Scripts/Tracks/OvalTrackFactory.cs` | Adapter: solved geometry → `TrackInfoV2` asset. |
| `Assets/Scripts/Tracks/TrackGround.cs` | The ground plane, sized from the spline's bounding box. |
| `Assets/Editor/TrackAuthoringMenu.cs` | The `Draftmaster > Tracks` window and menu items. |
| `Assets/Editor/TrackDressingFactory.cs` | Ground, walls, grandstands and paddock, derived from the geometry. |
| `Assets/Editor/RaceSceneSplitter.cs` | Select / preview / edit the track a race is run at. Named for the one-shot split it used to perform. |
| `Assets/Tests/Editor/OvalGeometryTests.cs` | Lap length, closure, tri-oval, paperclip, racing line, pit lane, tuning. |
| `Assets/Tests/Editor/TrackDimensionsTests.cs` | Every venue solves: closure, length, width, corner count, no self-intersection. |
| `Assets/Tests/Editor/BuiltTrackAssetTests.cs` | The built assets on disk measure what they claim, and every package is wired. |
| `Assets/Tests/Editor/OsmTrackGeometryTests.cs` | A lap walked into points comes back as the lap that went in, noise and all; closure on both paths. |

## The shared race scene

`Assets/Scenes/RaceScene.unity` is the scene every track loads into: the twenty-odd manager roots (player
car, `GridSpawner`, `PitLaneStart`, the directors, HUDs, camera, database) and **no road**. Every
`TrackBuilder` field in it is deliberately null — `TrackSceneLoader` fills them from whichever package
loads.

Watkins Glen is now just another package (`Resources/TrackPackages/WatkinsGlen.prefab`) and loads into
RaceScene like any other round. `Assets/Scenes/WatkinsGlen.unity` — the scene the split was copied from —
**was deleted**; keeping a second scene with a road in it around only invited editing the wrong copy. The
three every-track NPC markers that had been left behind in it (team liaison, chief strategist, PR manager)
were moved into `RaceScene`'s `NPCs` root first, so nothing was lost. The one-shot editor tools that
existed for the split — `RaceSceneSplitter.Split`, `RaceSceneNameFixup`, `WatkinsGlenCoverageAudit` — went
with it.

**To edit a track now:** open `RaceScene`, pick the track with `Select Track For Next Race...`, then
`Edit Selected Package In Context (Race Scene)`. That is the only place track content should be edited.

## Still to do

- **Wire `TrackProfile` into the sim** (draft, tyres, AI spread, camera), as above.
- **Per-track records and laps**: `TrackInfoV2.trackLaps` and the catalogue's `DefaultLaps` both exist and
  currently disagree in places. Pick the catalogue as the authority when the calendar starts driving races.
