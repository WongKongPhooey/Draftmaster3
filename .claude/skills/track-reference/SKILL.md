---
name: track-reference
description: Gather real-world reference for a Draftmaster3 track (satellite imagery, OSM walls/raceways/pit lanes, Wikipedia length/turns/banking, layout diagrams) and measure how far the game's track is from the real one. Use when asked to make a track accurate or realistic, build/rebuild/reshape a venue, check a track against reality, find satellite imagery or dimensions for a track, or "which tracks are wrong".
---

# Track reference

Full guide: `Docs/Track-Reference.md`. Short version:

1. `python -I Tools/track_reference.py <TrackId>` (ids as in `Tools/TrackExports/`; Texas = `FortWorth`,
   Pocono = `LongPond`). Overpass may be busy - the script backs off by itself; run it in the background
   for `--all`.
2. Read `Tools/TrackReference/<id>/report.md`, then LOOK at `Tools/TrackReference/<id>/cache/overview_small.jpg`
   with the Read tool before trusting the verdict. Check the best candidate (bright green) sits on the
   real racing surface; outer-vs-inner wall readings can be close on length but a track width apart.
   Also glance at `cache/trackmap.png` (Wikipedia diagram) for corner layout. A candidate's *Against it*
   column (pit lane, another circuit's ways, wrong turning for the type) means it is a chain, not the circuit -
   never write one as a trace. Per-venue rebuild decisions live in `Tools/TrackReference/audit.json` (printed
   under *Audit* in STATUS.md; `--status` regenerates STATUS.md offline).
3. Verdict = RMS of the game's centreline against the best real one after a rotation/translation/scale
   fit: matches <= 4 m (WG, Daytona, Texas are 1.5-2 m), close <= 10, off <= 25, else wrong shape.
4. To fix geometry: `--write-trace=<candidate#> [--start=apex|lat,lon] [--pit=<wayId>]` -> Unity
   `Draftmaster > Tracks > Import Traced Geometry For Every Trace` (via MCP `execute_menu_item`) ->
   `Export Geometry For Paddock Layout (Selected Track)` -> re-run the report -> retrain the racing line.
   Never overwrite WatkinsGlen, Daytona, FortWorth or Martinsville (needs `--force`; Josh hand-built them).
   Scope (Josh, 2026-11-19): shape the track correctly - that is the job. Then a paddock from the standard
   randomised layout (`python Tools/paddock_generate.py <TrackId> --regenerate` off the re-exported geometry), and outside the paddock
   only the scatter (crowds, fan RVs, trees/greenery via `Scenery > Scatter Scenery`). No landmarks, stands,
   buildings or other bespoke environment objects.
5. Width/banking/lap counts live in `Assets/Scripts/Tracks/Core/TrackDimensions.cs`; the report shows
   Wikipedia's figures beside the game's.

Rules: never commit anything from `cache/` (imagery, OSM dumps are git-ignored); no Google/Esri imagery;
the Unity overlay (`Draftmaster > Tracks > Reference > Open Aerial Overlay For Selected Track`) opens a new
scene - never build overlays inside RaceScene.
