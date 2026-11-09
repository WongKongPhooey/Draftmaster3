# Overnight lanes — what can run in parallel, and how to write rows for it

Tokenmaxxer (its own repo, `Documents/Tokenmaxxer`) now runs several tasks at once. Each row on the
Draftmaster3 sheet has a **Lane** (column C) and an optional **After** (column D):

| Lane | Runs in | One at a time? | Verified by |
|---|---|---|---|
| blank / `unity` | this checkout, with the Unity Editor over MCP | yes — the Editor is shared | the agent: EditMode tests over MCP |
| `code` | a git worktree (`Draftmaster3-wt/w1`, `w2`) | no — 2 slots | gates: headless compile, headless tests, data validators |
| `research` | a worktree, with web search | no | the same gates |
| `plan` | a worktree | no | writes new rows onto the sheet |

**After** holds row numbers (`12` or `12, 14`) that must reach *Awaiting Review* / *Done* before the row
starts. A worktree row that passes its gates goes *Merge queued*, then lands on `develop` the next time
nothing is using this checkout — never over your uncommitted edits (git refuses; the row goes *Unmerged*
and the work stays on `tokenmaxxer/wt/<id>`). Details: the Tokenmaxxer README, "Parallel slots".

## What a worktree can and can't do

A worktree has no Unity Editor, so:

- **Can**: any C#, JSON, Markdown, Python; new scripts (the gate writes their `.meta` files); tests that
  only touch engine-free code (`Tools/HeadlessTests`).
- **Can't**: scenes, prefabs, ScriptableObjects, materials — anything binary-serialised. New `.asmdef`
  files (no csproj until Unity generates one). Play Mode. Anything that has to *see* the game.

So the trick for parallel work is to move what used to live in a prefab or a code table into **text** the
game reads, and leave one `unity` row at the end to wire or check it. Three areas are set up that way.

## The gates

Every worktree branch must pass all of these before it merges (`worktree_checks` in Tokenmaxxer's
config, plus the compile):

1. `python <Tokenmaxxer>/unity_compile.py .` — every Unity assembly, compiled with plain `dotnet build`
   against the real Unity and package DLLs, in ~20 s. It also adds new `.cs` files to the (gitignored)
   csprojs, putting each in the assembly Unity would (nearest `.asmdef`, else `Assembly-CSharp(-Editor)`).
   Run it yourself any time: `python ../Tokenmaxxer/unity_compile.py .` from the repo root.
2. `dotnet test Tools/HeadlessTests -nologo` — the engine-free rules tests (Master Draft, the travel route
   planner, track geometry: 113 tests, ~35 s). See below.
3. `python Tools/validate_travel_stops.py` — the travel map's shops and junkyards.
4. `python Tools/paddock_sidecar.py validate` — paddock sidecars.

## Headless tests (`Tools/HeadlessTests`)

`HeadlessTests.csproj` compiles `Assets/Scripts/MasterDraft/Core` and `Assets/Scripts/Tracks/Core` straight
from source, plus the EditMode test files that only touch them, and runs them under NUnit with no Unity.
`UnityEngine.CoreModule` is referenced for the managed value types (Vector2, Mathf). Anything that calls
the engine's native side (JsonUtility, Debug.Log, Application.dataPath, Mathf.PerlinNoise) can't run here;
the few tests that do are excluded in `headless.runsettings` and still run in Unity's Test Runner.

**Master Draft Core is fully engine-free now** (`noEngineReferences: true`). Its one engine call, the
profile's JSON, goes through `ProfileJson.Write/Read`: the game installs JsonUtility
(`MasterDraftJsonInstaller`, before the first scene and on every editor reload), the headless tests install
System.Text.Json set to JsonUtility's rules. So Master Draft rules work is ideal `code`-lane work: write the
rule, add the test to `MasterDraftRulesTests` (or a new file listed in the csproj), and the gate runs it.

## Satnav stops: `Assets/Resources/Travel/Stops/<Region>.json`

The shops and junkyards used to be `ShopOn`/`YardOn` calls in `TravelGraph.cs`; they are now five region
files — `West`, `Central`, `Midwest`, `Southeast`, `Northeast` — so five rows can each own one file.

```json
{ "region": "Northeast", "stops": [
  { "id": "pitt_bros", "type": "EngineShop", "name": "Pitt Brothers Engine Builders",
    "road": ["LimeRock", "NewHampshire"],
    "flavor": "Two brothers, three dynos, zero patience. ...",
    "stock": ["engine_r7", "engine_hemi", "gearbox_close"] },
  { "id": "some_yard", "type": "Junkyard", "name": "...", "lat": 41.49, "lon": -74.45,
    "flavor": "...", "source": "research notes are fine, the game ignores them" } ] }
```

- `type`: `EngineShop` (fixed `stock`, 1–6 PartCatalog ids, not the `_team` ones) or `Junkyard` (stock
  rerolls weekly; no `stock` field).
- Where: `road` = the two circuit ids of the highway it splits, or `lat`/`lon` and the game mounts it on the
  nearest road nothing else sits on. One stop per road.
- `python Tools/validate_travel_stops.py --free-roads --region Northeast` lists the roads with nothing on
  them, nearest that region first, with each road's midpoint coordinates.
- Names are **made up** (TravelGraph's rule: geography is real, businesses are not). A real town to hang
  one on is fine.
- New stops need their marker on the map prefab: a `unity` row running
  `Draftmaster > Travel Map > Sync Node Markers` then `Save And Rebuild Markers And Routes`, after the
  research rows (put their rows in its After).
- Only `EngineShop` and `Junkyard` exist. A new kind (a parts centre, say) is a `code` row first: the
  enum value, what it sells, its shop panel — then research rows can use it.

## Paddock placements: `Assets/Resources/Paddocks/<TrackId>.json`

Track packages are binary prefabs, so venue markers and lot areas could only be placed in the Editor, one
track at a time. A **sidecar** holds the same things as text and `PaddockSidecar` builds them into the
package when the race scene loads it (`TrackSceneLoader`), before venues and lots look for them.
**Anything the package itself authors wins** — a marker name it already has, or a lot kind it already has,
is skipped — so a sidecar only fills gaps and never undoes Editor work.

```json
{ "trackId": "Dover",
  "markers": [
    { "name": "Grandstand_Marker", "x": 120, "y": -40, "rotation": 0, "width": 8, "height": 6,
      "seat": { "x": 150, "y": -95 }, "view": { "x": 150, "y": -60 } },
    { "name": "PitBox_Marker", "x": 30, "y": 12, "width": 6, "height": 4 } ],
  "lots": [
    { "kind": "Motorhomes", "x": 0, "y": 60, "rotation": 0, "width": 180, "height": 55, "gap": 2, "rowGap": 6 },
    { "kind": "Garages",    "x": 0, "y": 130, "rotation": 0, "width": 220, "height": 40 } ] }
```

- Coordinates are **package-local metres**, the frame of `Tools/TrackExports/<TrackId>.json`.
- Marker names are `<venue word>_Marker`: PitBox, Motorhome, MeetingRoom, SigningFence, SponsorSuite,
  IntroStage, Grandstand (and the aliases in `WeekendMarkerNames`). `seat` = where the player is teleported
  (a Grandstand seat across the track), `view` = where the camera settles. The box is the arrival perimeter.
- Lots: one `Motorhomes` and one `Garages` per track; local +X is the way a line of rigs runs. A full field
  is 43 rigs — the validator warns when the box can't hold them at its spacing.
- **Geometry to work from**: `Tools/TrackExports/<TrackId>.json` — centreline and pit lane (with widths, every
  5 m), the start line, and the box of every object already in the package. Written by
  `Draftmaster > Tracks > Export Geometry For Paddock Layout (All Packages)`; re-run it (a `unity` row)
  after a track's layout or scenery changes. The four authored tracks — WatkinsGlen, Daytona, FortWorth,
  Martinsville — have their markers and lots in the export as worked examples.
- **Check**: `python Tools/paddock_sidecar.py validate Dover` (errors: a lot on the road or pit lane, a
  marker centred on the road, unknown venue words) and `python Tools/paddock_sidecar.py preview Dover`,
  which draws road, pit lane, existing objects and the sidecar to `Temp/PaddockPreview/Dover.png` — look at
  it. Real-world layouts (OSM: parking, campgrounds, infield buildings) are worth researching; see
  `Tools/osm/` and Docs/Tracks.md "Traced geometry" for the trace tooling.
- **Randomised layouts**: `python Tools/paddock_generate.py --all` writes four `variants` per track (34
  tracks; the four authored packages are skipped). Every variant has both lots and every venue —
  SponsorSuite (sponsor area), SigningFence (fan zone), MeetingRoom, IntroStage (near the end of pit road),
  Grandstand (seat in one of the package's own stands) — none overlapping each other, the road, the pit lane
  or package objects, placed behind the pit lane on the side away from the track. What varies is which
  stretch each piece sits behind, how far back, the order, the lot proportions and angle. The game picks a
  variant per visit (`PaddockSidecar.PickVariant`: track id + `TravelState.Week`), so the paddock moves
  between visits but never mid-weekend. `--seed N` rerolls; `preview <id> --variant k` draws one.
  PitBox and the player's Motorhome markers are left to the game (pit box from the pit lane, RV from the lot).
- Nobody has played a sidecar track yet: the first one deserves a `unity` row that loads it, walks the
  paddock and checks the RVs land where the box says.

## Writing rows that run well unattended

- One job per row, 30–90 minutes. Name the files, the acceptance check and how to verify.
- Two rows with no After between them may run **at the same time**, so they must not edit the same file.
  That's why the stops are split by region and paddocks are one file per track.
- End a chain of `code`/`research` rows with a `unity` row that does the Editor half (markers, menus,
  a play-check), with the chain in its After.
- For something big (the Master Draft rework), write the brief in `Docs/` and add one `plan` row:
  "Read Docs/MasterDraft-Rework.md and break it into rows". The planner appends the rows with lanes and
  After links; review them before arming the night.

Example rows:

| Task | Lane | After |
|---|---|---|
| Research 6 made-up junkyards and engine shops for the Southeast along real towns on free roads; add them to `Assets/Resources/Travel/Stops/Southeast.json`; validator clean. | research | |
| Same for Northeast. | research | |
| Lay out the Dover paddock sidecar from `Tools/TrackExports/Dover.json` and the real track's layout: Motorhomes + Garages lots, PitBox, Motorhome, MeetingRoom, SigningFence, Grandstand (with seat) markers; validator clean, preview checked. | research | |
| Sync travel map markers for the new stops and rebuild routes; check the map in the prefab. | unity | 2, 3 |
