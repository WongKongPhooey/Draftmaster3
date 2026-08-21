# Editor handbook — how to build content

The other docs in `Docs/` explain how each system *works*. This one is the operator's manual: **what to
click, in what order**, for the jobs that come up while making content. Every entry links out to the deep
doc when there is one.

`Docs/BuildBook.html` is the same material as a browsable page (the Build Book), styled with the
project's own Iron Oval kit. Open it in a browser; keep the two in step when either changes.

---

## Which scene do I open?

| Job | Open |
| --- | --- |
| Race managers, HUDs, the every-track NPC cast, the player car | `Assets/Scenes/RaceScene.unity` — the shared race scene. **No road in it**; the track loads as a package at play time. |
| One track's road, scenery, paddock, its own NPCs | that track's package prefab, `Resources/TrackPackages/<id>.prefab` — `Draftmaster > Tracks > Edit Selected Package (Prefab Mode)` |
| The hand-authored reference road course | `Assets/Scenes/WatkinsGlen.unity` |
| Team garage on-foot hub | `Assets/Scenes/TeamGarage.unity` (built by `Tools > Draftmaster > Build Team Garage Scene`) |
| Judging UI widgets | `Assets/Scenes/IronOvalShowcase.unity` (`Draftmaster > Art > Build Iron Oval Showcase Scene`) |

**The rule:** a thing that exists at every track belongs in the race scene; a thing that is *that track*
belongs in the package prefab. See `Docs/Tracks.md`.

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

## 5. See the pit boxes while authoring

`Draftmaster > Debug > Show Pit Boxes` (a checked toggle) draws the box ladder at edit time from the same
`PitLane.FitBoxes` call the spawner makes — so it is where cars and crews actually end up.
`Log Pit Box Lines` dumps distances and local positions for placing the painted dividers;
`Log Pit Fit` explains a "field doesn't fit" layout.

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
The RV race engineer is package content — `Draftmaster > NPCs > Add RV Engineer To Open Package`.

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
- `Tools > Draftmaster > Bake Paddock Surface` — bakes the paddock tarmac into the scene as a real object;
  re-run after changing the layout fields.
- `Draftmaster > Paddock > Preview Motorhome Lot` / `Clear Motorhome Lot Preview` — see where the drivers'
  RV row lands while authoring (preview objects are `DontSave`, so they never hit the scene file).
- `Tools > Draftmaster > Build Team Garage Scene`.

---

# Play-mode keys

| Key | Panel |
| --- | --- |
| `Esc` | Pause menu (closes the phone first if it's up) |
| `P` | Phone — on foot only: Tasks, Notes, SoBuzz, DrivR |
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
| `C` | Crew chief mode |
| `V` | Drive / Broadcast toggle |
| `L` | Pit limiter |

Free: `F10`–`F12`.

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
| Select Track For Next Race > … | Sets `TrackSelection` |
| Report Calendar Coverage | What's built vs catalogue-only |
| Split Shared Race Scene (WatkinsGlen → package) | One-shot: turn the hand-authored scene into shared scene + package |

### `Draftmaster > NPCs`
Director (**Ctrl+Shift+N**) · Add Placed NPC · Install Default Pit Cast (greeter + chief) ·
Move Selected NPC Into Track Package · Add RV Engineer To Open Package · Dialogue Pool (Global) ·
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
- **Geometry-anchored NPCs need a track in the scene** — preview a package, or author inside the package's
  own Prefab Mode stage.
- **The race scene has no road.** Manager fields pointing at a `TrackBuilder` are filled at load
  (`BindSceneReferences` only fills nulls); serialise a road into the scene and no package can ever load.
- **Road courses are hand-authored.** The oval generator refuses them.
- **Scenes and prefabs are binary-serialised.** Text search won't find a GUID inside one — use the Unity
  MCP tools or the editor.
- **Editing the seeded roster or calendar needs `DatabaseManager.SchemaVersion` bumped**, or the old table
  survives.
- **`F6` drives three panels and `F9` two** (see the key table). Only `F10`–`F12` are still free — a new
  panel should take one of those.
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
