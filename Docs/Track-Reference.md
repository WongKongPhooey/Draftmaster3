# Track reference: building a track from the real one

Watkins Glen, Daytona and Texas are right because somebody sat with satellite imagery and measured them by
hand. This is the pipeline that does the gathering and the measuring for any venue, so making a track
accurate is a matter of reading a report and choosing, not of hunting for way ids and calibrating
screenshots.

```
python -I Tools/track_reference.py <TrackId>      # one venue, ~1-2 min the first time, seconds after
python -I Tools/track_reference.py --all          # every venue + Tools/TrackReference/STATUS.md
python -I Tools/track_reference.py --status       # STATUS.md again from every venue's committed report, offline
```

Track ids are the ones in `Tools/TrackExports/` (`FortWorth` for Texas, `LongPond` for Pocono, ...).

## What it gathers

Into `Tools/TrackReference/<id>/`:

| | From | What for |
| --- | --- | --- |
| **Facts** | Wikipedia infobox, every layout | Lap length, turns, banking text, surface, coordinates. The layout whose length matches the game's is picked out and set against the game's numbers. |
| **OSM** | Overpass, one cached answer | Raceway lines, pit lanes, walls/barriers/fences, `area:highway` surfaces, grandstands, plus buildings, roads, car parks and water for context. |
| **Aerial** | USGS NAIP (public domain, US only) | Web Mercator, so pixels are square in ground metres; the exact metres per pixel is in `aerial.json`. ~0.6 m/px. |
| **Track maps** | Wikimedia Commons | The infobox diagrams with their captions and licences. Look at these for corner names and layout variants. |
| **Candidates** | OSM, derived | Every closed ring near the published lap: raceway rings taken as the centre, wall/fence rings taken as the **outer edge** or as the **inside edge** and offset by half the game's racing surface, and the committed trace. Ranked as described under *Choosing the candidate*. |
| **Built fit** | `Tools/TrackExports/<id>.json` | The game's centreline fitted onto each candidate (rotation, translation, uniform scale; any start point, either direction), then measured point by point: RMS, 95th percentile, worst point, and the three worst 100 m stretches by spline metre. |
| **Overview** | All of the above | `cache/overview_small.jpg` (1600 px) and full-size `overview.jpg`: the aerial with every layer, the candidates in green (best bright), and the game's line coloured by how far off it is (cyan <= 4 m, amber <= 10 m, red beyond), with spline-metre dots every 500 m. |
| **Alignment** | Built fit | `cache/alignment.json`: the aerial's corners in the package's own metres, for the Unity overlay. |

`facts.json`, `candidates.json` (scores, no geometry) and `report.md` are committed, and so is `STATUS.md`.
Everything in `cache/` is git-ignored: the OSM answer, imagery, Commons diagrams and renders are big or
not ours to redistribute. Re-running re-creates it; `--refresh` ignores the cache, `--offline` re-renders
from it without the network.

## Reading the verdict

Built RMS against the best candidate, after the fit:

| Verdict | RMS | Meaning |
| --- | --- | --- |
| matches | <= 4 m | Within a car width everywhere that matters. Daytona 1.5, Texas 1.6, Watkins Glen 1.9. |
| close | <= 10 m | Right shape, a corner or a straight somewhat out. |
| off | <= 25 m | Recognisable, but corners in the wrong place or the wrong size. |
| wrong shape | > 25 m | A formula's guess, not the track. |
| unmeasured | - | OSM has no closed ring near the published lap (see below). |

The scale in the fit is reported, not judged: every game lap is rescaled to the published length, and a
real centreline is rarely exactly that (it depends where the official measured: 15 ft off the inside
wall at most ovals). A scale of x0.99-x1.02 is normal.

**Always look at `overview_small.jpg` before trusting a number.** The best candidate is chosen by lap
length, and a wall ring offset the wrong way (outer read as inner) can be within 2% on length while sitting
a whole track width off. The image makes that obvious in a second.

## Choosing the candidate

Raceway rings come from two searches: the straight-on walker (follow the road, carry straight on at every
junction - what an oval lap does) and a junction search that may turn off at any junction within 100 degrees of
straight ahead, which is the only way to find a road course that leaves an oval (the Roval, the Indianapolis
road course: the straight-on walker can only ever find the oval there). Each ring is then checked for what is
against it, shown in the report's *Against it* column:

- **runs down a pit lane** - more than 2% of it on a pit way (COTA's old best took the pit lane for the front straight);
- **on ways named for another circuit** - more than 3% on a kart track, dirt track, quarter-mile, skidpad, club
  circuit or run-off road, and on an oval also on a road course, Roval or chicane (Atlanta's old best and committed
  trace ran through Pit Road and the AMS Small Oval). A name listing two circuits (`Charlotte Motor Speedway;1/4 Mile
  track`) is shared, not foreign;
- **turns the wrong way for the track type** - *Turns against* is how far the lap turns against its own direction:
  0-40 degrees on a real oval, 400-760 on a road course; past 90 degrees is not an oval, under 90 not a road course.

The ranking is: does not cross itself, then fewest things against it, then the committed trace if within 5% of
the lap, then lap length. Length alone used to decide, and a chain through the infield can be closer to the
published lap than the oval itself (Atlanta: 0.06% against 2.6%). Near-duplicates (within 5 m for 90% of the
lap) keep only the best-ranked, so the list shows real alternatives.

`Tools/TrackReference/audit.json` holds the verdict a person reached per venue - which side is wrong, whether to
rebuild and from which candidate - and `STATUS.md` prints it under *Audit*, so it survives regeneration.

## Building or fixing a track from the report

1. **Run the report** and open the overview. Pick the candidate that sits on the real racing surface:
   on an oval usually a raceway ring or the outer wall offset inward; at a venue whose raceway lines
   balloon wide of the apexes (Martinsville) the inside wall offset outward.
2. **Write it as a trace:**
   ```
   python -I Tools/track_reference.py Phoenix --write-trace=1 --start=apex --pit=<wayId>
   ```
   `--start=apex` puts the start/finish line at the tri-oval apex, `--start=lat,lon` nearest a point;
   `--pit` takes a mapped pit lane from the report's "Mapped pit lanes" line. This writes
   `Assets/TrackTraces/<id>.json` in the same format as `trace_from_wall.py` (curvature segmentation).
   WatkinsGlen, Daytona, FortWorth and Martinsville are refused without `--force`.
   For a ring the walker could not assemble (gaps, partial ways), fall back to `trace_from_wall.py`
   with the way ids from the report - see `Docs/Tracks.md` § Tracing from the wall.
3. **Import** in Unity: `Draftmaster > Tracks > Import Traced Geometry For Every Trace`. The importer still
   refuses a shape that does not close within 3% of its own lap.
   Without the Editor (an overnight lane), add a `[TestCase("<id>")]` to `Assets/Tests/Editor/TracedOvalImportTests.cs`
   and run `dotnet test Tools/HeadlessTests`: it reads the trace through the importer's own Core path and checks
   the closure, that the built lap lies within 4 m RMS of the trace, and that a mapped pit lane fits as pit road -
   so the import is known to land before anyone opens Unity. To import one venue without re-importing every
   trace (Daytona, Texas...), select its asset and use `Import Traced Geometry (selected asset)`.
4. **Re-export and re-measure**: `Draftmaster > Tracks > Export Geometry For Paddock Layout (Selected Track)`,
   then run the report again. The verdict should now be `matches`.
5. **Retrain the racing line** (a re-import keeps the old one if the lap length did not change - see
   `Docs/Tracks.md` § Training the racing line) and re-dress what depends on the geometry.

**Scope of a rebuild (Josh, 2026-11-19).** The job is the track's *shape*. After it, give the venue a paddock from
the standard randomised layout the authored tracks use: re-export the geometry (step 4), then
`python Tools/paddock_generate.py <TrackId> --regenerate` (generated variants were laid out on the old shape; authored ones are kept) (see `Docs/Overnight-Lanes.md` § Paddock sidecars). Outside the
paddock, the only dressing is the scatter: crowds, the fan RVs, trees and other greenery
(`Draftmaster > Tracks > Scenery > Scatter Scenery`, `Docs/Tracks.md` § Scenery). Don't author landmarks,
grandstands, buildings or other bespoke environment objects for a rebuilt venue.

Banking, width, pit speed and lap counts still come from `TrackDimensions`. The report shows Wikipedia's
banking text next to the game's numbers, and where an outer and an inner wall ring are both mapped it
measures the gap between them (`wallGapMeasured` - outer wall to pit wall, so an upper bound on width).
Banking cannot be read from a top-down image.

## Checking it in Unity

`Draftmaster > Tracks > Reference > Open Aerial Overlay For Selected Track` opens a **new, unsaved scene**
(it offers to save what is open first) with the selected track's package at the origin and the aerial
already lined up on it, translucent on top. `Toggle Aerial Over Or Under The Track` swaps it to opaque
underneath the road. `Open Report For Selected Track` opens `report.md` and the overview.

The aerial is a `TrackReferenceImage` loaded from the cache at run time, not an asset: the overlay scene
can be saved but the picture will not come back with it - re-run the menu item. Never do this in
`RaceScene` - its save hook folds anything on a track instance into the package.

## Limits

- **NAIP is US only.** Mexico City gets an overview drawn on plain ground. Google/Esri imagery is not used
  (licence); for a one-off check a screenshot in `TrackReferenceImage` still works.
- **OSM coverage is the ceiling.** Where neither the raceway nor any wall closes a ring near the lap, the
  venue is `unmeasured`; the aerial and the OSM layers are still drawn, and the shape has to be traced off
  the aerial by hand (add a synthetic way to an `--osm` file as Martinsville's turn 3 gap was, or reshape
  the layout over the Unity overlay).
- **Overpass turns work away when it is busy** ("at capacity", 502/504), sometimes for hours; a venue can take
  ten minutes. The client backs off for about ten minutes before giving up on a venue; finished venues come
  from the cache, so just run `--all` again (run it in the background). `--all --offline` rebuilds every
  report and `STATUS.md` from whatever is cached, listing venues not fetched yet.
  The track features are fetched first and the context layers (buildings, roads) second, so a busy server
  costs the decoration before it costs the measurement.
- Street circuits rarely have a closed raceway ring in OSM; their streets do, but the walker only follows
  raceways and barriers. For those, a committed `Tools/TrackReference/<id>/reference_ways.json` (synthetic
  raceway ways, Overpass format) is merged into the OSM layers on every run; Chicago's is assembled from OSM
  street centrelines in race order by `Tools/TrackReference/Chicago/build_reference.py`.

## Files

| File | Role |
| --- | --- |
| `Tools/track_reference.py` | The pipeline. `--selftest` checks the fitting maths, the infobox parser and the junction search offline. |
| `Tools/TrackReference/<id>/` | Per venue: `report.md`, `facts.json`, `candidates.json`; `cache/` (ignored). |
| `Tools/TrackReference/STATUS.md` | Every venue's verdict, worst first, then the audit. |
| `Tools/TrackReference/audit.json` | Per venue, by hand: which side is wrong, rebuild or not, from which candidate. |
| `Assets/Tests/Editor/TracedOvalImportTests.cs` | A curvature trace read as the importer reads it, headless: closes, lies on the trace, pit lane fits. |
| `Assets/Scripts/Tracks/Core/TrackReferenceAlignment.cs` | Alignment corners -> sprite placement. Headless-tested (`TrackReferenceAlignmentTests`). |
| `Assets/Editor/TrackReferenceOverlay.cs` | The `Draftmaster > Tracks > Reference` menu. |
| `.claude/skills/track-reference/SKILL.md` | How Claude should use all this when asked to make a track accurate. |

Data (c) OpenStreetMap contributors, ODbL. Imagery USGS NAIP, public domain. Wikipedia text CC BY-SA 4.0;
Commons diagrams under the licence recorded beside each.
