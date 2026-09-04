# Editor handbook — how to build content

The other docs in `Docs/` explain how each system *works*. This one is the operator's manual: **what to
click, in what order**, for the jobs that come up while making content. Every entry links out to the deep
doc when there is one.

`Docs/BuildBook.html` is the same material as a browsable page (the Build Book), styled with the
project's own Iron Oval kit. Open it in a browser; keep the two in step when either changes.

---

## The demo flow (how the scenes hang together)

Press Play on **`Assets/Scenes/TitleScreen.unity`** — it is first in the build list, so it is also what a
build boots into.

```
TitleScreen  ──NEW SEASON / CONTINUE / EXHIBITION──▶  RaceScene   (builds WatkinsGlen — TrackSelection)
     │                                                    │
     │                                                    ├─ pause (Esc) ▸ QUIT TO TITLE ──▶ TitleScreen
     │                                                    └─ RV interior ▸ laptop ──┐
     │                                                                              ▼
     └──TEAM FACTORY──▶  TeamGarage  ─ laptop ─────────────────────────────▶  GarageScreen
                             │                                                    │
                             └─ EXIT door ──▶ TitleScreen        BACK ◀───────────┘
                                                            (returns to whichever scene opened it)
```

* **NEW SEASON** starts a fresh weekend at `TitleScreenUI.newSeasonTrackId` (WatkinsGlen).
  **CONTINUE** resumes the saved `TrackSelection`; **EXHIBITION** skips practice and qualifying. Both
  fall back to a track that actually has a layout, so neither can load a race scene with no road.
* **The garage sheet is not on the menu.** It is opened from a `LaptopInteractable` — one on the dinette
  table in the RV, one on the desk in the team factory. `GarageScreenLoader` remembers which scene
  opened it so BACK returns there; opened cold it falls back to the title.
* Returning from the garage **reloads** the scene it came from, so a practice session in progress
  restarts. The laptops are for between sessions.
* **The weekend is a schedule, not a straight line.** Arriving at a track puts the three-day timetable up
  (`F10` after that): six half-days, your own practice/qualifying/race, the other two championships'
  sessions to watch, and the media, fan and sponsor obligations booked around them. Your sessions still
  hand off to the race scene exactly as before. `Docs/Race-Weekend.md`.
* **The menu is two menus.** `DemoMode.IsDemo` decides which: each row carries an `appearsIn` flag
  (Both / DemoOnly / FullOnly), and `TitleScreenUI` switches the others off and closes the column up over
  the gaps at runtime — in the editor you always see every row. The demo drops the exhibition and factory
  rows and gains **RESTART DEMO**, which wipes the save (`CareerReset.ClearAll`) and opens a fresh career
  at the season's opening track; it asks twice before it does. The flag is the `DRAFTMASTER_DEMO` compile
  define (`Draftmaster > Demo > Build Is Demo`), with a PlayerPrefs override for the editor and
  development builds (`Draftmaster > Demo > Preview Demo Menu`) so the demo menu can be looked at without
  a rebuild. Rows are added or re-flagged by `Draftmaster > UI > Set Up Demo Rows On Title Screen`, which
  edits the scene in place — never rebuild the title screen for a menu change.
* **`CareerReset` clears by deleting everything and putting the settings back**, rather than by naming the
  progress keys: PlayerPrefs cannot be enumerated and a list of career keys goes stale the moment a
  subsystem adds one. Settings, HUD toggles and the signed-in account survive; a new subsystem's keys are
  cleared without anyone remembering to add them.
* Build list order is load-bearing: `TitleScreen`, `RaceScene`, `GarageScreen`, `TeamGarage`, `DemoMenu`
  (multiplayer lobby). A destination missing from that list makes its title row draw disabled.
  `Assets/Tests/Editor/TitleScreenWiringTests.cs` checks the whole chain.
* **`DemoMenu` is currently unreachable** — nothing routes to the multiplayer lobby since the title screen
  became the boot scene. Either give it a title row or uncheck it in the build settings; until then it is
  listed in `SceneNavigationTests.KnownOrphans`.

### Is it still navigable? (run this after any scene or menu change)

Two suites, both runnable from the Test Runner (`Window > General > Test Runner`) or over MCP:

| Suite | Mode | What it proves |
| --- | --- | --- |
| `Assets/Tests/Editor/SceneNavigationTests.cs` | EditMode, ~2s | Every enabled build scene opens, carries no missing scripts, and names only scenes that are in the build list. The exits form one connected graph: everything is reachable from `TitleScreen`, nothing is a dead end, and the title is reachable again from everywhere. |
| `Assets/Tests/PlayMode/NavigationFlowTests.cs` | PlayMode, ~20s | The same routes actually walked with the game running: every title row pressed, the factory laptop and door used, RACE/BACK on the garage sheet, `QUIT TO TITLE` out of a race. Also: every scene comes up without a `Debug.LogError`, with a camera, with no button whose `onClick` calls nothing, and with the clock running (a weekend panel left open used to freeze everything loaded after it). |

The static suite reads the map; the play suite drives it. A route that only exists in code (the pause
menu's `titleSceneName`, the laptop's `GarageScreenLoader.SceneName`) is read off the real type in
`SceneNavigationTests.CodeExits`, so renaming either fails the test rather than silently dropping an edge.

**Writing more play-mode tests: never call `SceneManager.LoadScene` directly.** The play-mode test runner
is a scene object marked `DontSave`, not `DontDestroyOnLoad`, so a single-mode load deletes the object
running your coroutine — the run does not fail, it hangs in play mode forever. Load through
`NavigationFlowTests.Go(sceneName)`, which moves the runner out of the way first.

## Which scene do I open?

| Job | Open |
| --- | --- |
| Race managers, HUDs, the every-track NPC cast, the player car | `Assets/Scenes/RaceScene.unity` — the shared race scene. **No road in it**; the track loads as a package at play time. |
| One track's road, scenery, paddock, its own NPCs | that track's package prefab, `Resources/TrackPackages/<id>.prefab` — open `RaceScene`, then `Draftmaster > Tracks > Edit Selected Package In Context (Race Scene)` |
| Team garage on-foot hub ("Team Factory") | `Assets/Menus/TeamGarage.unity` (built by `Tools > Draftmaster > Build Team Garage Scene`) |
| The title menu | `Assets/Scenes/TitleScreen.unity` (built by `Draftmaster > Art > Build Title Screen Scene`) |
| The car / driver sheet | `Assets/Scenes/GarageScreen.unity` (built by `Draftmaster > Art > Build Garage Screen Scene`) |
| Judging UI widgets | `Assets/Scenes/IronOvalShowcase.unity` (`Draftmaster > Art > Build Iron Oval Showcase Scene`) |

**The rule:** a thing that exists at every track belongs in the race scene; a thing that is *that track*
belongs in the package prefab. See `Docs/Tracks.md`.

**Placing NPCs, specifically.** Open `RaceScene`, pick the track with
`Draftmaster > Tracks > Select Track For Next Race...`, then
`Draftmaster > Tracks > Edit Selected Package In Context (Race Scene)`. That opens the track package on a
Prefab Mode stage *through* an instance in the race scene, so you place people against the real road with
the managers and HUDs around them, and every edit is saved into the package rather than into the scene.
Track-specific people go under `Paddock/NPCs` in the package. The every-track cast — pit greeter, crew
chief, team liaison, chief strategist, PR manager — lives in `RaceScene`'s own `NPCs` root instead; edit
those with `RaceScene` open and no prefab stage. `Draftmaster > NPCs > Move Selected NPC Into Track
Package` moves one from the scene to the package when you get it the wrong way round.

## Where things live

| What | Path |
| --- | --- |
| Track geometry (`TrackInfoV2`) | `Assets/Resources/Tracks/<id>.asset` |
| Track walls/strips (`TrackEnvironment`) | `Assets/Resources/Tracks/<id>Environment.asset` |
| Track content package | `Assets/Resources/TrackPackages/<id>.prefab` |
| Track catalogue rows (the calendar) | `Assets/Scripts/Database/DummyTracks.cs` |
| Quests | `Assets/Resources/Quests/*.asset` |
| Dialogue pools (random crowd) | `Assets/Resources/Dialogue/*.asset` (claimed by their `trackId`) |
| UI prefabs + theme | `Assets/Resources/UI/` — `PixelUITheme.asset` drives the whole look |
| RV interior / speech box | `Assets/Resources/OnFoot/` |
| Vehicles | `Assets/Resources/Vehicles/` |
| Driver database | `draftmaster.db` in `Application.persistentDataPath` |

---

# Recipes

## 1. Add a new oval

1. Check it is in the calendar: `Assets/Scripts/Database/DummyTracks.cs`. Most of the 35 are already there
   with type, length, banking and laps. If you edit that seed, bump `DatabaseManager.SchemaVersion` so the
   table rebuilds.
2. `Draftmaster > Tracks > Track Builder Window`. Find the row, press **Generate Layout** — writes
   `Resources/Tracks/<id>.asset`. Regenerating refills the asset in place, so its GUID (and every
   reference to it) survives.
3. **Build Package** — writes `Resources/TrackPackages/<id>.prefab` and dresses it. What comes out is
   drivable and walkable without opening it.
4. **Race** — sets `TrackSelection`; open `RaceScene` and press Play.

`Draftmaster > Tracks > Report Calendar Coverage` prints what's built vs catalogue-only.
`Dress All Undressed Packages` fills in every package missing scenery. Detail: `Docs/Tracks.md`.

## 2. Add a road course

The generator refuses road courses — there is no formula for one. Duplicate
`Assets/Resources/Tracks/WatkinsGlen.asset`, rename it `<id>.asset`, edit its segment list (Straight/Turn
with length, angle, banking, width, plus the racing line offsets), then **Build Package** from the Track
Builder Window as above.

## 3. Change a track's scenery

Open the package: `Draftmaster > Tracks > Edit Selected Package (Prefab Mode)`, or **In Context (Race
Scene)** to see it against the managers. Move, delete or replace anything.

Re-dressing (`Dress Selected Package`) only ever replaces pieces **it** generated — those inside the
`Environment` / `Paddock` roots, or carrying its names. Hand-made pieces are kept and reported. Kerbs,
garages, signage and camera towers are hand work by design.

## 4. Paint the start/finish line or pit exit line

Strip arrays can't be grown through the MCP property API, so these are menu items:

- `Tools > Track > Add Missing Finish Line Strips` — anchors a strip to `TrackInfoV2.startFinishDistance`.
- `Tools > Track > Add Missing Pit Exit Lines` — anchors to `PitExitLineDistance`, where the pit limiter
  releases.
- `Tools > Track > Rebuild Track Environment` after either.

## 4b. Car colours and the pit box stands

Every pit box gets the team's **pit box** — the cart on the wall the crew chief sits on — painted in that
car's colours: canopy in the primary, stripe in the secondary, car number on the roof. `PitBoxStand`, built
one per box by `PitCrewSpawner` (`spawnStands`, `standLateral`). They appear when the field does, which is
when a session is live; `Draftmaster > Debug > Session Live (spawn the field)` puts cars, boxes, crews and
stands in the scene without walking the weekend to a session.

The colours come from **`Resources/Cars/CarColours`**, matched most specific first: carset + car number →
team name → carset default → the asset's fallback. You do not type it in:
**`Draftmaster > Cars > Build Car Colours From Liveries`** reads every `<carset>livery<n>` texture and picks
its two colours off the paint (`LiveryPalette` — ignores the outline, the tyres and the glass, and refuses a
"second colour" that is really a shade of the first or a ten-pixel sponsor patch). Correct any it gets
wrong in the asset and tick **Hand Authored** on that row; the seeder never overwrites those, so re-running
it after a repaint is safe.

## 5. See the pit boxes while authoring

`Draftmaster > Debug > Show Pit Boxes` (a checked toggle) draws the box ladder at edit time from the same
`PitLane.FitBoxes` call the spawner makes — so it is where cars and crews actually end up.
`Log Pit Box Lines` dumps distances and local positions for placing the painted dividers;
`Log Pit Fit` explains a "field doesn't fit" layout.

## 6a. Lay out a track's cast for the weekend

`Draftmaster > NPCs > Weekend Cast` (`Ctrl+Shift+W`) is the window for building a race weekend's people.

1. Pick the **half-day** across the top (FRI AM … SUN PM) and the series. That one control is the whole
   preview: the window rebuilds the timetable for that weekend, and the scene view, the NPC Director's
   table and the inspector card all switch to that half-day with it. Everybody who would not be there
   greys out where they stand.

   Who is there on a given half-day is authorable, not guessed: `AppearanceConditions` has a **Which
   half-day** block (six toggles, all on = any time this weekend) next to the session toggles, so "the fan
   fence crowd is Saturday morning" is a rule rather than a comment.
2. **Booked this half-day** lists what is on the sheet: time, title, venue and who is waiting there. Click a
   booking to read the whole conversation the player will have — every beat, every answer, and what each
   answer is worth — then **Open the words** to jump to the content file.
3. **In the paddock this half-day** lists every placed NPC, greyed if they would not appear, with the line
   set that is live for that half-day. Click one to select and frame it in the scene view.
4. The panel underneath edits the selected NPC: **what they say in that half-day**, their quest and its four
   line sets, and the marker/beat fields (interaction, objective banner, trigger). Everything else is behind
   the fold.

**Anchored NPCs are not moved by their transform.** Everyone in the core cast stands off a piece of
geometry — the pit lane, the parked car, the motorhome door — so the marker object itself sits at the
origin and a dotted line runs from it to where they actually stand. Drag the **handle at the stand point**
(not the object): it writes back to `anchorAlong` / `anchorLateral`, the two offsets in the inspector. Set
`anchor = Here` if you want a marker you can move by hand instead. When an anchor cannot resolve — the
track is not in the scene, or `along` has run off the end of the pit lane and is being clamped — the
inspector says so in orange rather than leaving you dragging a dead handle.

Clicking any NPC — in the window, the hierarchy or the scene view — puts the same read-out at the top of the
Inspector: which half-days they are here for (the previewed one in bold) and the clause that stops them when
they are not, where they stand, how you meet them (walk up, or they walk over and from how far), the quest
they hand out and what finishing it takes, whether they are the one who hands the player their day, and the
lines they will actually speak in that half-day. The raw fields are underneath, unchanged.

**Install Core Cast** (also `Draftmaster > NPCs > Install Core Cast`, and the same button in the Director)
stamps the people every track has, whatever series is running — pit greeter, crew
chief, race engineer, chief strategist, PR manager, team liaison — as ordinary editable markers under
`NPCs`. Run it in any track scene or package; it never duplicates what is already there. Everybody else in
the paddock is crowd, scattered around them by `PaddockSpawner`.

**How busy the paddock is:** `PaddockSpawner.totalNpcs` on the `PaddockSpawner` object in `RaceScene` is
the *full-house* headcount, and it is set to 400 — `CrowdPolicy.ComfortableMaxPopulation`, the ceiling
`CrowdBenchmarkTests` measured (report in `Library/CrowdBenchmark.txt`; re-run that suite before raising
it). Ten of them are talkable, the rest walk. Almost all of them are frozen at any moment — `CrowdActor` /
`CrowdDirector` only run the handful within 25 m of an on-foot player, and freeze the lot the instant the
player gets in the car — so what the number actually costs is under 1.2 ms/frame at the tightest paddock on
the calendar (Bowman Gray, 75 x 30 m) and nothing at all while driving.

`scaleWithWeekend` thins that down on the quieter half-days and fills it back up for the busy ones — Friday
morning setup runs 55% of a full house, Sunday afternoon the lot — and any race session (a booked race, a
single race, multiplayer) is always a full house whatever the sheet says. Untick it to spawn the same
headcount in every session.

**Where the crowd stands:** the walkers are a pool, not a cast. A paddock is a few hundred metres long and
thirty deep, so an evenly spread crowd is mostly behind you — walk to one end and three quarters of it is
doing nothing for the scene. Any walker that drifts more than 100 m from an on-foot player is taken out of
the paddock and put back down between 14 m and 45 m away with a freshly rolled outfit, so it reads as
somebody new arriving rather than the same person teleported. The player never sees it: the inner radius is
raised at runtime to the on-foot camera's corner distance, so a respawn is off screen by construction, and
nothing recycles at all while the player is in a car. Talkers, quest givers, drivers, reps and anything
placed by hand are never touched — only the anonymous wanderers.

`CrowdDirector.recycling` holds the knobs (`CrowdRecycleTuning`). `targetNearPlayer` is the one that
matters: it caps how many walkers may be inside the 100 m radius at once (280 of 400), and anyone who
wanders off while the cluster is full is simply left where they are, frozen, until there is room again.
That is what stops the whole paddock piling onto the player. Untick `PaddockSpawner.recycleWalkers` (or
`CrowdRecycleTuning.enabled`) to go back to a crowd that stays where it was spawned. Rules and defaults are
covered by `CrowdRecyclePolicyTests`.

**Dialogue that changes across the weekend:** a marker carries a *set of lines per half-day*
(`PlacedNPC.schedule`). The first set covering the half-day being played wins; anything uncovered falls back
to the marker's default lines. The crew chief ships with three — Friday's practice brief, Saturday's
qualifying trim, Sunday's race brief — and `ScheduledDialogue` swaps them as the weekend's clock advances,
without a scene reload. Write a new one with **Write a FRI AM set** in the window; it starts as a copy of
what they say now.

## 6. Place an NPC

1. Decide where the marker lives: **track-specific** → the package, under `Paddock/NPCs`; **every track** →
   `RaceScene.unity`.
2. Empty GameObject + `PlacedNPC`, or `Draftmaster > NPCs > Add Placed NPC` (drops one at the scene-view
   pivot, parented correctly when a package is open in Prefab Mode).
3. Set the **anchor**: `Here` inside a package, otherwise a geometry anchor (`PitLane`, `ParkedCar`,
   `RVDoor`, `PlayerSpawn`) so one marker works at all 35 tracks. `ParkedCar` needs `followAnchor` on —
   the car is re-parked into its fitted box a few frames after the scene opens.
4. Set the **interaction**: `TalkOnInteract` / `WalkUpCutscene` / `OnCarEntry` / `Silent`, plus lines.
5. Gate it with `AppearanceConditions` — session, series, track, career stats, quest state, inventory,
   career path, repeat scope, chance.

**Which file the marker lives in decides which tracks they appear at.** In the package = that track only;
in `RaceScene` = every track, unless you fill in `AppearanceConditions > Tracks`. Put one in the wrong half
and `Draftmaster > NPCs > Move Selected NPC Into Track Package` moves it, keeping every tuned field.
The motorhome door belongs to the **team liaison**, who is stood up from the weekend's sheet at runtime
(`PlacedNPCDefaults.CreateLiaison`) rather than placed by hand.

**Geometry anchors only resolve with a track loaded.** In the race scene run
`Draftmaster > Tracks > Preview Selected Package In Scene` first, then `Clear Package Previews From Scene`
when done — otherwise the gizmo is lying to you.

Check the cast with `Draftmaster > NPCs > Director` (**Ctrl+Shift+N**): flip between Practice / Qualifying /
Race and everyone greys out or lights up, with a reason per row ("not in Race", "wins is 0, needs 5..∞").
`Install Default Pit Cast` turns the runtime greeter / engineer / crew chief into editable markers.

Detail: `Docs/NPCs-and-Quests.md` §0.

## 7. Design what an NPC looks like

`GameObject > 2D Object > Layered NPC (Paper Doll)`, then work in the inspector: per layer pick a style
sheet and a colour, with live Scene-view preview. The preview layers are real children, so what you see is
what builds at runtime. Sprite spec and the greyscale/tint rule: `Docs/NPCs-and-Quests.md` §1.

## 8. Add crowd chatter

`Draftmaster > NPCs > Dialogue Pool (Selected Track)` creates or opens the per-track pool;
`Dialogue Pool (Global)` the shared one; `Seed Global Dialogue Pool From Built-Ins` fills it from the
hardcoded lines. A pool is claimed by its `trackId` field.

## 9. Add a side quest

1. `Assets > Create > Quests > Quest`, saved into `Assets/Resources/Quests/`.
2. Fill in objective type, target, reward (`rewardItemId` makes it a chain link), prerequisites.
3. Put it on an NPC: `QuestGiverNPC` (setting `quest` on a `PlacedNPC` turns it into one) and fill the five
   line sets — `offerLines`, `activeLines`, `turnInLines`, `completedLines`, `lockedLines`.
4. A delivery target is the same component with `isDeliveryTarget` on and `offersQuest` off.

Objective types, stat keys and the pause-menu mission board: `Docs/NPCs-and-Quests.md` §3.

## 10. Sponsors and car decals

- `Draftmaster > Sponsors > Create Car Sponsor Layout` — the asset saying where the hood / tail / quarter
  panels sit on the livery.
- `Generate Placeholder Decals` — one car-scale logo per brand in the database, so the feature is playable
  before any real art. Overwrite the PNGs later; nothing else changes.
- `Preview Slots On Livery` writes `Temp/SponsorSlotPreview.png`; `Preview Sponsored Car` shows the baked
  result.

`Docs/Sponsorship.md`.

## 11. Edit the RV (interior and exterior)

`Draftmaster > RV Interior > Build Prefab` generates `Resources/OnFoot/RVInterior.prefab` once — after that
it is **yours**: open it in Prefab Mode and lay it out, swap the placeholder squares for art. Build refuses
to overwrite; `Force Rebuild` loses hand edits. Same pair for the exterior (`Build RV Prefab`).
Authoring frame: (0,0) is where the player spawns, +Y points at the doorway.

The room's two devices — the cab **satnav** (opens the travel map) and the table **laptop** (opens the
garage sheet) — are generated by `RVInterior` at play time for any room that doesn't already carry one,
so an older prefab still gets them. Put your own `SatnavInteractable` / `LaptopInteractable` in the
prefab to place them yourself; the generated one then stands down. The laptop sits on whatever child is
named `Table`.

## 12. Edit the travel map

`Draftmaster > Travel Map > Build Prefab` once, then edit `Resources/UI/TravelMap.prefab` by hand.
`Sync Node Markers` adds markers for nodes added to `TravelGraph` later without moving existing ones;
`Snap Markers To Graph Layout` re-snaps everything to the code positions; `Open (Play Mode)` opens the map
without the F9 key. Geography is code-defined in `TravelGraph`. `Docs/Travel-Map.md`.

## 13. Add an app to the phone

One `PhoneApp` subclass plus a line in `PhoneUI.BuildApps()` — the home grid keeps six slots and draws the
spare ones as empty bays, so nothing else moves. Writing to the existing apps from your own system:
`PhoneTasksApp.Push(id, text)` / `Resolve(id)`, `PhoneNotes.Record(...)`. Detail: `Docs/Phone.md`.

## 14. Edit drivers

`Window > Draftmaster > Driver Database` — edits the same SQLite file the game reads, so changes show up in
Play Mode. To change the *schema* or the seeded roster, edit `Assets/Scripts/Database/` and bump
`DatabaseManager.SchemaVersion` to force a rebuild.

## 15. New world art (sprites, props)

The standard is **12.8 px/m**. Import at that PPU — never fix size with transform scale.

- `Draftmaster > Art > Retarget World Sprites to Pixel Standard` — bulk-fix import PPU.
- `Audit Pixel Scale` → writes `Docs/PixelScaleAudit.md`.
- `Report Sprite Scale Compensation` — finds prefabs faking size with scale.
- `Apply Pixel Standard to Open Scene` + `Rebuild Track Generators` after changing materials or tiling.
- `Report Surface Material Usage` — who else uses a material before you retile it.

**The one exception to the standard**: a flat *unit quad* sprite (`Assets/Textures/Environment/WhiteSquare.png`,
4x4 px at **4** px/unit = 1x1 world unit) is stretched to its metres by transform scale on purpose — it is a
building block, not a drawing. Retargeting it to 12.8 px/m does not resize art, it rescales everything built
out of it: it shrank the RV exterior and its interior floor to 4/12.8 of size inside a full-size collider
shell. The retargeter skips it (`PixelSpriteImport.kExcludedFiles`) and the RV builder repairs the import if
it drifts again.

`Docs/PixelArtStandard.md`.

## 16. UI work

- Kit setup: `Draftmaster > Art > Set Up Iron Oval Kit`, then `Verify Iron Oval Kit` (writes
  `Docs/IronOvalKit.md`). Older kit: `Set Up / Verify Pixel UI Kit`.
- Everything reads one asset: `Assets/Resources/UI/PixelUITheme.asset`.
- Move authored Canvases onto the kit: `Restyle Scene Canvas UI` / `Restyle UI Prefabs`.
- The IMGUI race panels are code (`PixelGUI`), not prefabs — `Docs/IronOvalKit.md`.
- Screens from the design file: `Draftmaster > Art > Build Title Screen Scene` and
  `Build Garage Screen Scene` regenerate `Assets/Scenes/TitleScreen.unity` / `GarageScreen.unity`. The
  race HUD (`IronOvalRaceHUD`) and results screen are code, not scenes. See `Docs/IronOvalKit.md`.
- Demo-flow prefabs: `Draftmaster > UI > Build Demo UI Prefabs` (car setup panel + control hint).
- Font trouble: `Dump Pixel Font Atlas`, `Preview Dialogue Bubble`.

## 17. Paddock, spawns and the garage

- `GameObject > Draftmaster > Player Spawn Point` — where the on-foot player starts (labelled, weighted).
- `GameObject > Draftmaster > Paddock Boundary` — the walkable pocket.
- All four `GameObject > Draftmaster` items go through `PaddockAuthoringStage.Place`, which puts the new
  object in **whatever is open** and parents it under the package's paddock root. Without that, an object
  made while a package is open in context is created in the main scene behind the stage — selected, shown
  in the Inspector, and invisible in the Hierarchy — and a root-level sibling of the prefab root is dropped
  when the stage is saved. If you have one of those strays from before, it is sitting in `RaceScene`:
  delete it there and make a new one inside the package.
- `Tools > Draftmaster > Bake Paddock Surface` — bakes the paddock tarmac into the scene as a real object;
  re-run after changing the layout fields.
- `GameObject > Draftmaster > Paddock Lot Area (Motorhomes)` / `(Garages)` — **the footprint of each
  paddock block, drawn in the editor.** Both lots are built at play time out of the live field, so
  without one you set `lineDirection` / `rowGap` / `gapFromMotorhomes` by number and found out in play
  mode. Drop an area into the track package instead (`Draftmaster > Tracks > Edit Selected Package In
  Context`), size its `BoxCollider2D` with Edit Collider, and that rectangle **is** the lot.
  - The box's local **+X is the direction a line of rigs runs**, local **+Y is the way lines stack** and
    the way the bodies point. Rotate the object and the whole block turns with it.
  - The gizmo draws the rectangle, every place a rig will stand and a caption —
    `Motorhomes lot — 43 rigs, 5 line(s) of 9, 5.9m apart (2 place(s) spare)`. Too small a box turns the
    caption red and says `TIGHT`, and the lot logs the same warning at play time rather than quietly
    growing out the back.
  - With an area present the lot **ignores** its own `lineDirection`, `rowCount`, `maxPerRow`, `lineGap`,
    `rowGap` and (garages) `gapFromMotorhomes`; spacing comes from the area's `gap` / `rowGap`. No area =
    the old behaviour exactly, anchored on the player's RV.
  - The walkable pocket is cut to the rectangle plus `walkPad` (4 m) instead of being grown off the
    rigs — so **overlap the box with the paddock next door** or the player is fenced out of a block they
    can see. Still only ever added when the scene already has a `PaddockBoundary`.
  - **The player's RV never moves.** Stood inside the motorhome box it holds the place nearest it and the
    field parks around it; stood outside, the lot fills every place and their rig is simply elsewhere.
  - A garages area stands on its own: it no longer needs the motorhome lot to have laid out a line first.
  - Covered by `Assets/Tests/Editor/PaddockLotAreaTests.cs` (8 EditMode tests).
- `Draftmaster > Paddock > Preview Motorhome Lot` / `Clear Motorhome Lot Preview` — spawns actual RV
  bodies for the un-authored path, anchored on the player's RV (preview objects are `DontSave`, so they
  never hit the scene file). The preview draws the motorhomes only and does **not** know about
  `PaddockLotArea` — with an area authored, the area's own gizmo is what to trust.
- **Popup garages** (`PopupGarageLot`, `PopupGarageRig`, `PopupGarageInterior`) — one team garage per entry,
  parked in lines *behind* the motorhome lot, each a rig with a canopy pitched off its door side and the
  car sat under it. `DriverMotorhomeLot` builds the block once its own row exists (switch it off with
  `buildPopupGarages`), so it inherits the player's RV rotation and the same line direction — the paddock
  reads as tarmac → motorhomes → garages walked through in order. Knobs live on the `PopupGarageLot`
  object at play time: `gapFromMotorhomes`, `lineGap`, `rowGap`, `maxPerRow`, the canopy size, and
  `parkCarsUnderCanopy` — but a `Garages` **Paddock Lot Area** in the track package overrides the
  placement ones and is the way to author it. It brings its own `PaddockBoundary` pocket, overlapping the
  motorhome lot's, so the player can walk straight in.
  - **The car is at the garage whenever it isn't somewhere else.** A driver with a live car in the scene
    (out on track, sat in its pit box) gets an empty canopy; between sessions nothing is spawned on track,
    so every canopy has its bodywork under it.
  - **Walking in blacks the world out**, exactly like the player's motorhome — one opaque quad in front of
    everything, the room drawn in front of that, the player pulled in front of the room (see `RVInterior`
    for the long version). Inside is a meeting table with seats round it, a setup board in the team's
    colours and a bench. Rooms are generated the first time the player comes within
    `interiorBuildRange` (25 m), so forty of them cost nothing until they're walked up to.
- `Tools > Draftmaster > Build Team Garage Scene` — rebuilds `GarageContent` in the open TeamGarage scene:
  floor, team car, the three crew stations, the desk **laptop** (opens the garage sheet) and the **EXIT**
  door back to the title. A re-run wipes hand edits under that root, so run it first, then tweak.

### Naming a place

**Places are announced, never lettered.** A location the player walks up to introduces itself on the title
card in the upper middle of the screen — the same card the track name arrives on and the same one an
objective banner uses — and fades after a few seconds. Put a `LocationTitle` on whatever the place is built
under (`title`, optional `subtitle`, `radius`, `forgetRadius`, `minRepeatSeconds`, `onceOnly`), or call
`LocationTitle.Attach(go, "FAN ZONE", 13f, "Signing sessions")` from the builder that makes it. It waits for
whatever is already on the card to finish before it speaks, and re-arms only once the player has walked back
out past `forgetRadius`.

Do **not** float a `TextMesh` with the place's name over it — that reads as a label stuck to the scene.
World text is for signage a real circuit would have (a board, a hoarding, a braking marker):
`PaddockProps.Sign` is for that and says so.

## 17b. The demo's opening: waking up, and being told the day

The demo opens on a **black screen with an alarm clock going off**, then fades in on the driver getting up
in their motorhome. It is `WakeUpSequence`, played by `PitLaneStart` and tuned on its inspector under
**Wake Up**: `wakeUpInRV`, the alarm clip (empty = a synthesised placeholder), how long it rings in the
dark, the fade, the getting-up beat, plus `lyingDownSprite` and `getUpTrigger` for when the real art lands —
with neither, the body is laid on its side and stood back up, which is the placeholder. Any key hits the
clock and brings the picture up early. It plays on the first morning of a weekend only (`weekend.wokeup`),
not on the reloads that practice, qualifying and the race are made of.

**You wake up with nothing booked.** The weekend normally books the next thing on the sheet by itself, but
the first obligation of a weekend is handed over by a person: the team liaison waiting outside the
motorhome door. Until she has said it, `WeekendAppointment` is empty and the objective strip is blank —
`WeekendBriefing` is the rule, `WeekendDirector.BookNextUp` asks it, and the flag that says who does the
handing over is **`givesTheDaysObjective` on a `PlacedNPC`** (on the liaison by default; NPC Director,
Interaction section). Tick it on somebody else and the day is theirs to give; tick it on nobody and the
weekend books for itself as it used to. If nothing in the cast claims it, `PitLaneStart` says so the moment
the cast is up and the booking happens immediately — the empty strip is never a dead end.

**Moving the liaison** (or anyone else in the every-track cast): **Install Core Cast** first — until then
she is built from code at play time and there is nothing in the scene to click (§6a). Once she is a marker,
she stands off the motorhome door rather than at a world position: `anchor = RVDoor`, `anchorAlong` metres
out from it, `anchorLateral` metres to one side, `triggerOffset`/`triggerRadius` where stepping out sets her
off, `stopDistance` how close she walks before she speaks. Her lines still come off the live sheet —
`linesFromTheWeekendSheet` re-reads them when she appears — so a placed marker never goes stale. Keep her
anchored to the door in the shared `RaceScene` and she works at all 38 tracks; to put her on an exact spot
instead, set `anchor = Here` and author the marker **in that track's package**
(`Draftmaster > Tracks > Edit Selected Package`, or `Draftmaster > NPCs > Move Selected NPC Into Track
Package`) — a hand-placed position only means anything at one circuit.

Re-arm both beats for another run with **`Draftmaster > Demo > Re-arm The Opening (alarm + liaison)`**
(it also puts the three days back to Friday morning, since testing the opening walks the clock on). If the
liaison herself does not turn up, her appearance flag has been used: `Draftmaster > NPCs > Clear Appearance
Flags`. `Draftmaster > Debug > RV Cutscene > Report State` prints which of the gates said no.

## 18. Change what a race weekend looks like

`F10` in play mode is the timetable, but the weekend leads itself: the team liaison meets the player
outside the motorhome and hands them the day, the objective marker points at it, and finishing one books
the next. **Committing to something books it rather than running it** — the sheet
closes, a marker and a strip name the place and the distance, and the obligation happens when the player
walks up to whoever is waiting there (`T` travels you if you would rather not walk). To change it:

- **Lay a whole weekend out by hand** — `Draftmaster > Weekend > Plan Editor` (`Ctrl+Shift+E`). Writes
  `Assets/Resources/Weekends/<Track>.<Series>.json`: six half-days, whatever you put in them. A track+series
  with a plan file uses ONLY that file and the generated schedule below does not run for it. This is the
  way to author a round; the C# builders are the fallback for tracks nobody has got to yet.
- **Say where something happens at a track** — make a GameObject in the track package and name it
  `PitBox_Marker` (also `Motorhome_`, `DriversRoom_`, `SigningFence_`, `SponsorSuite_`, `IntroStage_`,
  `Grandstand_`). Give it a collider and **its shape is the perimeter the booking starts inside**. Authored
  markers become venue anchors before anything is generated, so that venue is never worked out from the pit
  lane — which is what used to drop a marker on the fence line.
- **Send one booking somewhere specific** — the `Marker Location` field on a booking names the object, so
  `"markerLocation": "Podium_Marker"` sends that one photo shoot to the podium while the rest go to
  hospitality.
- **A venue the player cannot walk to** — give its marker a child called `Seat` (or set `teleportTo`). The
  marker goes where they CAN reach, the child goes where the thing happens, and the marker becomes a gate
  that wipes them across. This is how the grandstands work at a road course.
  `Draftmaster > Weekend > Check Markers In Open Scene` reports the rest.
- **Move a session** (generated tracks) — `WeekendTimetable.PracticeTime / QualifyingTime / RaceTime`.
  Everything else keys off these: the drivers meeting is two hours before your race and intros thirty
  minutes before it, wherever that lands.
- **Add or move an obligation** (generated tracks) — `WeekendTimetable.BuildObligations`. Deliberate clashes
  are the point.
- **Move where something happens** — `WeekendVenues.For` maps each `ActivityKind` to a place; the sheet's
  location column is generated from it, so the two can't disagree.
- **Rewrite what somebody says** — `Core/Conversations/` (`TeamMeetingContent`, `CeremonyContent`,
  `SponsorContent`, `SigningContent`). A beat is a speaker, a line and its answers; each answer carries
  what it is worth. `PressConferenceContent.Pool` still owns the press questions.
- **Rotate a weekend feature** — `WeekendTimetable.AddFeature`, one seeded roll per weekend.
- **Retune the meters** — `WeekendLedger.Apply` and the per-answer values in the conversation content.
  Nothing else touches them.
- **Change who meets the player at the RV** — `PlacedNPCDefaults.CreateLiaison` (the team liaison, who
  names the next booking) and `CreateEngineer` (who meets them instead when the next thing is their own
  session). Both are `PlacedNPC` walk-up beats triggered by stepping out of the motorhome.
- **Move a venue in the world** — `WeekendVenueSites` builds them all off the paddock rectangle; a track
  package can override any one by authoring its own `WeekendVenueAnchor`, which the builder leaves alone.

Rules live in the `Draftmaster.Weekend` assembly and are covered by `WeekendTimetableTests`,
`WeekendActivityContentTests`, `WeekendVenueTests` and `WeekendPlanTests` (EditMode); the paddock the weekend is played in is
covered by `WeekendVenuePresenceTests` (PlayMode — venues exist, hosts are stood at them, the room has a
chair per driver, and booking → objective → walk → talk actually connects). Full guide:
`Docs/Race-Weekend.md`.

---

# Play-mode keys

| Key | Panel |
| --- | --- |
| `Esc` | Pause menu (closes the phone first if it's up) |
| `P` | Phone — on foot only: Schedule, Tasks, Notes, SoBuzz, DrivR, Points |
| `T` | Travel to whatever the weekend has you booked in for (only while an appointment is up) |
| `Tab` (hold) | Expand the running-order board to the full field |
| `F1` | Lap timing readout |
| `F2` | Running-order board (in practice/qualifying it ranks on best lap) |
| `F3` | TEAM box — mid-race car switch |
| `F4` | Rivalry standings |
| `F5` | Driver dossier |
| `F6` | Tyre temp/wear **and** sponsor board **and** the UI-kit showcase (clash) |
| `F7` | Player telemetry |
| `F8` | Formation-lap diagnostics — gap / closing speed / state per car |
| `F9` | Handling tuner **and** travel-map dev hotkey (clash) |
| `F10` | Race weekend schedule — the three-day timetable |
| `F11` | Live timing for the session you are watching from a grandstand |
| `C` | Crew chief mode |
| `V` | Drive / Broadcast toggle |
| `L` | Pit limiter |

Free: `F12`.

---

# Menu reference

### `Draftmaster > Tracks`
| Item | Does |
| --- | --- |
| Track Builder Window | The main authoring window: Generate Layout / Build Package / Dress / Race, per calendar row |
| Create Starter Layouts (Daytona + Martinsville) | Seeds the two example tracks |
| Dress Selected Package · Dress All Undressed Packages | Generate ground, walls, grandstands, paddock from the spline |
| Edit Selected Package (Prefab Mode) · In Context (Race Scene) | Open a package for editing |
| Preview Selected Package In Scene · Clear Package Previews From Scene | Drop a track into the race scene so geometry anchors resolve |
| Select Track For Next Race... | Dropdown of every built track, grouped by type, ticked on the current one. Sets `TrackSelection` |
| Build All Calendar Tracks | Layout + package + dressing for all 37 generated venues (Watkins Glen is skipped) |
| Rebuild All Calendar Tracks (replace packages) | As above, but throws the package prefabs away first |
| Report Track Dimensions | Published length, width, banking and confidence for all 38 venues |
| Report Current Selection | Which track the next race builds, and why — prefs, travel-map fallback, resolved id |
| Clear Package Previews From Scene | **Run this before saving any scene you previewed a track in.** A package left in `RaceScene` overrides every selection |
| Report Calendar Coverage | What's built vs catalogue-only |

### `Draftmaster > NPCs`
Director (**Ctrl+Shift+N**) · Add Placed NPC · Install Default Pit Cast (greeter + chief) ·
Move Selected NPC Into Track Package · Dialogue Pool (Global) ·
Dialogue Pool (Selected Track) · Seed Global Dialogue Pool From Built-Ins · Clear Appearance Flags ·
Clear Career Path Choice.

> `Clear Appearance Flags` is the one to remember: a once-per-weekend / once-ever beat writes a PlayerPrefs
> flag when it plays, and you can't see it again until that's wiped.

### `Draftmaster > Art`
Set Up / Verify Iron Oval Kit · Set Up / Verify Pixel UI Kit · Build Iron Oval Showcase Scene ·
Restyle Scene Canvas UI · Restyle UI Prefabs · Apply Pixel Standard to Open Scene ·
Rebuild Track Generators · Retarget World Sprites to Pixel Standard · Audit Pixel Scale ·
Report Sprite Scale Compensation · Report Surface Material Usage · Dump Pixel Font Atlas ·
Preview / Clear Dialogue Bubble.

### `Draftmaster > UI`
Build Demo UI Prefabs · Build Car Setup Panel Prefab · Build Control Hint Prefab ·
Add Pit Limiter Chip To Speedometer · Build Speech Box Texture (+ Force Rebuild).

### `Draftmaster > UI`
Retarget Fonts In Prefabs (points every authored TMP/legacy label at the theme's faces and snaps its size
onto that face's pixel cell) · Retarget Fonts In Open Scenes (same, for whatever scenes are open — leaves
them dirty for you to check and save).

### `Draftmaster > Demo`
Preview Demo Menu (checked toggle — forces the demo title menu in the editor and development builds) ·
Build Is Demo (checked toggle — the `DRAFTMASTER_DEMO` define on the active build target, which is what
actually ships) · Wipe Career Save (what RESTART DEMO does, from the editor).
Add or re-flag the rows themselves with `Draftmaster > UI > Set Up Demo Rows On Title Screen`.

### `Draftmaster > Sponsors`
Generate Placeholder Decals · Create Car Sponsor Layout · Preview Slots On Livery · Preview Sponsored Car.

### `Draftmaster > RV Interior` · `Travel Map` · `Paddock` · `Fights` · `Debug`
Build + Force Rebuild pairs (see recipes) · motorhome lot preview · Clear Seeded Test Rivalries ·
Reset ALL Driver Relationships · Show Pit Boxes · Log Pit Box Lines · Log Pit Fit ·
RV Cutscene > Teleport Player Outside Door / Onto Trigger, Start NPC Conversation, Report State
(play-mode stand-ins for walking the player, since MCP can't move objects during play).

### `Draftmaster > Scene`
Tidy Hierarchy Into Groups (files the open scene's root objects into the same buckets play mode uses —
undoable, save the scene to keep it) · Flatten Hierarchy Groups (undoes it, returning everything to the root).

### `Tools`
`Tools > Track >` Add Missing Finish Line Strips · Add Missing Pit Exit Lines · Rebuild Track Environment ·
Normalise Racing Line Sides.
`Tools > Draftmaster >` Build Team Garage Scene · Bake Paddock Surface · Migrate Strip Distances.

### `GameObject > Draftmaster`
Player Spawn Point · Paddock Boundary. Plus `GameObject > 2D Object > Layered NPC (Paper Doll)`.

### `Window > Draftmaster`
Driver Database.

### `Assets > Create`
| Menu | Asset |
| --- | --- |
| Racetrack > Track Info V2 | Track geometry (spline system) |
| Racetrack > Track Environment | Walls, strips, barrier gaps |
| Racetrack > New Track | Legacy `TrackInfo` (scrolling system) |
| Vehicle > New Vehicle | `VehicleInfo` |
| Quests > Quest | `QuestInfo` |
| Draftmaster > Dialogue Pool | Crowd lines |
| Draftmaster > Car Sponsor Layout | Decal panel rects |
| Draftmaster > Pixel UI Theme | UI theme |
| NPC > Part Library | Paper-doll parts |
| Sounds > Engine Sound Set · Sounds > Clips · Commentary > Lines | Audio |

---

# Gotchas

- **"Force Rebuild" items destroy hand edits.** The plain `Build …` items refuse to overwrite on purpose.
- **A modal that survives a scene load takes the clock with it.** `WeekendModal` zeroes `Time.timeScale`
  while a weekend panel is up, and those panels are `DontDestroyOnLoad` — so anything that leaves the scene
  while one is open hands the next scene a frozen game (`Update` still runs, so menus respond; nothing
  physical moves). `WeekendDirector.OnSceneLoaded` closes the schedule and calls `WeekendModal.Reset()` on
  every non-additive load. Any NEW panel that freezes the world must be closed on scene change the same way.
- **Nobody speaks without asking.** Every `SpeechBubble.Speak` goes through `SpeechDirector`: one bubble is
  on screen at a time, ambient chatter is dropped rather than queued when anything else is talking, a
  cutscene outranks a conversation, and two conversations take turns. Pass the priority
  (`Draftmaster.Sim.SpeechPriority`) when adding a new speaker, and pass `owner` for a two-hander so the
  player's reply is not queued behind the line it answers. Rules: `Assets/Scripts/Sim/SpeechQueue.cs`.
- **Bubbles clamp themselves into the view.** A speaker at the edge of the frame would otherwise put half
  its box off screen. `SpeechBubble.KeepOnScreen` prefers above the head, then below, then slides along the
  edge — so never assume the box is directly over the speaker.
- **A generated screen's buttons are dead unless the binder wires them in `Start`.** `onClick.AddListener`
  at build time is a runtime listener and is not serialised into the saved scene — see
  `GarageScreenUI.WireButtons`. `NavigationFlowTests` fails on any button whose `onClick` calls nothing.
- **Geometry-anchored NPCs need a track in the scene** — preview a package, or author inside the package's
  own Prefab Mode stage.
- **The race scene has no road.** Manager fields pointing at a `TrackBuilder` are filled at load
  (`BindSceneReferences` only fills nulls); serialise a road into the scene and no package can ever load.
- **Road courses are hand-authored.** The oval generator refuses them.
- **Scenes and prefabs are binary-serialised.** Text search won't find a GUID inside one — use the Unity
  MCP tools or the editor.
- **Editing the seeded roster or calendar needs `DatabaseManager.SchemaVersion` bumped**, or the old table
  survives.
- **`F6` drives three panels and `F9` two** (see the key table). Only `F12` is still free — a new panel
  should take it.
- **The UI is one typeface (VT323) and sizes follow the face, not the role.** A bitmap face is drawn for
  one pixel cell — VT323 16, Silkscreen 8, Pixelify Sans 20 — and rendering off whole multiples of it goes
  soft. `IronOvalUI.Snap` and `PixelGUI`'s `LineH` / `DataLineH` exist so layouts re-flow when the theme's
  faces change; lay text rows out with those rather than literals. Changing a theme font slot is therefore
  safe, but a screen authored at the old cell may need its row pitch re-checked. Authored labels hold a
  direct font reference — `Draftmaster > UI > Retarget Fonts …` is what moves them.
- **The runtime hierarchy is grouped, so root paths change in play mode.** `RuntimeHierarchyOrganizer`
  files every root object under `UI`, `Particles`, `Environment`, `Vehicles`, `Characters`, `Directors`,
  `Cameras`, `Lighting`, `Audio`, `Markers` or `Misc` — empty parents at the origin, identity scale, world
  pose preserved. `GameObject.Find("Name")` is unaffected; a hard-coded `"/Name/Child"` path is not.
  Spawning something that should be filed instantly? Call `RuntimeHierarchy.Adopt(go, HierarchyGroup.X)`.
  Something that must stay at the root? Add a `HierarchyIgnore` component. Whole thing off?
  `RuntimeHierarchy.Enabled = false` before the scene loads. DontDestroyOnLoad objects and Netcode
  `NetworkObject`s are left alone by design.
- **Play mode pauses while the editor is unfocused**, so nothing driven by game time ticks while you work
  in another window.
