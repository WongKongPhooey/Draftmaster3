# Track reference status

Every venue measured against the real one by `python -I Tools/track_reference.py --all`, or rebuilt from each venue's last report by `--status` (2026-11-18). "Built RMS" is how far the game's centreline is from the best real-world centreline candidate after fitting position, rotation and scale; matches <= 4 m, close <= 10 m, off <= 25 m, else wrong shape. Per-venue detail: `Tools/TrackReference/<id>/report.md`. See `Docs/Track-Reference.md`.

| Track | Verdict | Built RMS / p95 / max (m) | Best candidate | Its lap vs published | Wikipedia lap vs game | Aerial |
|---|---|---|---|---|---|---|
| [COTA](COTA/report.md) | wrong shape | 67.8 / 137.7 / 159.2 | raceway ring taken as the centre | +0.28% | +0.47% | NAIP |
| [Charlotte](Charlotte/report.md) | wrong shape | 42.8 / 67.6 / 71.0 | committed trace (Assets/TrackTraces) | -2.13% | +0.00% | NAIP |
| [CharlotteRoval](CharlotteRoval/report.md) | wrong shape | 107.6 / 220.8 / 232.5 | raceway ring taken as the centre | -1.07% | +0.00% | NAIP |
| [Chicago](Chicago/report.md) | wrong shape | 159.2 / 255.1 / 291.5 | raceway ring taken as the centre | -3.55% | -2.73% | NAIP |
| [Darlington](Darlington/report.md) | wrong shape | 38.2 / 52.3 / 52.4 | committed trace (Assets/TrackTraces) | -4.56% | -0.29% | NAIP |
| [Indianapolis](Indianapolis/report.md) | wrong shape | 150.3 / 177.1 / 177.6 | raceway ring taken as the centre | +1.16% | +0.00% | NAIP |
| [IndyRoad](IndyRoad/report.md) | wrong shape | 163.3 / 287.7 / 320.0 | raceway ring taken as the centre | +0.00% | -0.04% | NAIP |
| [LimeRock](LimeRock/report.md) | wrong shape | 46.7 / 85.5 / 99.0 | raceway ring taken as the centre | +0.02% | +2.00% | NAIP |
| [MexicoCity](MexicoCity/report.md) | wrong shape | 121.3 / 194.7 / 247.5 | committed trace (Assets/TrackTraces) | +1.02% | +0.15% | none |
| [Portland](Portland/report.md) | wrong shape | 47.6 / 91.5 / 107.3 | committed trace (Assets/TrackTraces) | -0.18% | -0.15% | NAIP |
| [RoadAmerica](RoadAmerica/report.md) | wrong shape | 198.5 / 300.8 / 331.7 | raceway ring taken as the centre | -0.29% | -0.05% | NAIP |
| [Sonoma](Sonoma/report.md) | wrong shape | 147.5 / 281.4 / 296.4 | committed trace (Assets/TrackTraces) | -6.72% | +0.00% | NAIP |
| [Talladega](Talladega/report.md) | wrong shape | 29.4 / 56.6 / 61.2 | raceway ring taken as the centre | -0.22% | +0.00% | NAIP |
| [IRP](IRP/report.md) | off | 24.5 / 30.9 / 31.2 | raceway ring taken as the centre | -4.52% | -0.58% | NAIP |
| [Rockingham](Rockingham/report.md) | off | 14.3 / 22.5 / 23.6 | committed trace (Assets/TrackTraces) | -1.15% | +0.00% | NAIP |
| [Dover](Dover/report.md) | close | 4.2 / 8.0 / 8.8 | committed trace (Assets/TrackTraces) | +3.22% | +0.00% | NAIP |
| [Iowa](Iowa/report.md) | close | 4.0 / 6.4 / 8.9 | committed trace (Assets/TrackTraces) | +1.08% | -0.57% | NAIP |
| [LasVegas](LasVegas/report.md) | close | 7.2 / 12.1 / 13.2 | committed trace (Assets/TrackTraces) | +2.63% | +0.00% | NAIP |
| [LongPond](LongPond/report.md) | close | 7.4 / 14.3 / 18.3 | committed trace (Assets/TrackTraces) | +0.97% | +0.00% | NAIP |
| [Atlanta](Atlanta/report.md) | matches | 1.4 / 2.3 / 2.5 | committed trace (Assets/TrackTraces) | -2.64% | +0.00% | NAIP |
| [BowmanGray](BowmanGray/report.md) | matches | 3.8 / 5.5 / 5.7 | committed trace (Assets/TrackTraces) | +0.14% | +0.00% | NAIP |
| [Bristol](Bristol/report.md) | matches | 3.3 / 4.7 / 4.9 | committed trace (Assets/TrackTraces) | -0.79% | +0.57% | NAIP |
| [Daytona](Daytona/report.md) | matches | 1.5 / 2.9 / 3.2 | committed trace (Assets/TrackTraces) | -0.32% | +0.00% | NAIP |
| [FortWorth](FortWorth/report.md) | matches | 1.6 / 2.6 / 3.5 | committed trace (Assets/TrackTraces) | -1.40% | -3.00% | NAIP |
| [Gateway](Gateway/report.md) | matches | 3.4 / 5.8 / 6.0 | committed trace (Assets/TrackTraces) | +1.48% | +0.00% | NAIP |
| [Kansas](Kansas/report.md) | matches | 0.5 / 0.8 / 1.1 | committed trace (Assets/TrackTraces) | +2.58% | +0.00% | NAIP |
| [Martinsville](Martinsville/report.md) | matches | 1.1 / 2.1 / 2.5 | raceway ring taken as the centre | -3.24% | -0.75% | NAIP |
| [Richmond](Richmond/report.md) | matches | 1.2 / 2.4 / 3.0 | committed trace (Assets/TrackTraces) | -2.27% | +0.00% | NAIP |
| [WatkinsGlen](WatkinsGlen/report.md) | matches | 1.9 / 4.4 / 7.0 | raceway ring taken as the centre | -0.20% | +0.00% | NAIP |
| [SanDiego](SanDiego/report.md) | unmeasured | - | none in OSM | - | +13.33% | NAIP |
| Miami | error | not measured yet; run python -I Tools/track_reference.py Miami | | | | |
| Michigan | error | not measured yet; run python -I Tools/track_reference.py Michigan | | | | |
| MidOhio | error | not measured yet; run python -I Tools/track_reference.py MidOhio | | | | |
| Milwaukee | error | not measured yet; run python -I Tools/track_reference.py Milwaukee | | | | |
| Nashville | error | not measured yet; run python -I Tools/track_reference.py Nashville | | | | |
| NewHampshire | error | not measured yet; run python -I Tools/track_reference.py NewHampshire | | | | |
| NorthWilkesboro | error | not measured yet; run python -I Tools/track_reference.py NorthWilkesboro | | | | |
| Phoenix | error | not measured yet; run python -I Tools/track_reference.py Phoenix | | | | |

## Audit

Every venue the 2026-11-17 run called 'wrong shape' or 'off', checked by eye on `cache/overview_small.jpg` after candidate selection was fixed (see `Docs/Track-Reference.md` - Reading the verdict). 'Wrong' says which side is at fault: the game's track, or the reference candidate the 2026-11-17 run measured it against. Candidate numbers are the ones in each venue's current `report.md`; `--write-trace=<#>` takes them. No track has been changed. In all eleven the game's line is wrong; in five the old reference was wrong too (a chain through a pit lane or another circuit's roads), and two committed traces (Atlanta, Kansas) are such chains and must be replaced, not imported.

| Track | Wrong | Rebuild | From | Why |
|---|---|---|---|---|
| Atlanta | game, and the committed trace | yes | #0 (OSM way 288853230, the oval itself, -2.6%); pit way 288853232 | Game is the generic stadium oval (1035 x 382 m) on a wider quad-oval (about 950 x 510 m); 46.5 m RMS. The committed trace Assets/TrackTraces/Atlanta.json (old best) is not the oval: it runs 15% down Pit Road and through the AMS Small Oval and turns 118 deg against the lap - why the importer refused it. Replace it with #0, do not import the old one. |
| COTA | game (old reference ran down the pit lane) | yes | #0 (Grand Prix circuit, raceway only, +0.28%); pit way 514836373 | Game's hand-authored RoadCourseLayouts line is a loose sketch, 68 m RMS, most corners in the wrong place. The old best (-0.01%) took the 948 m Pit Lane way instead of the front straight; the new #0 sits on the circuit all the way round. NASCAR has raced the 2.4 mi National-style layout since 2025 - the game's 3.41 mi GP lap is a TrackDimensions decision, not a geometry one. |
| Charlotte | game | trace written 2026-11-18; Unity import pending | #0 (Charlotte Motor Speedway oval ways, -2.1%), now Assets/TrackTraces/Charlotte.json (--start=apex); pit ways 116034339,401660631,1052107105,401660628 (668 m, the whole front-stretch pit road) | Game is a narrow generic oval (1021 x 351 m) on a wide 1.5 mi quad-oval; 42.8 m RMS. The reference was right (the same ring as before; OSM draws it a little inside the groove, which the importer's rescale to the published lap absorbs). The Roval-chicane chains now rank below it. Rebuilt 2026-11-18 as far as a lane without the Editor can: the trace imports headlessly (TracedOvalImportTests, the importer's own Core path) closing to 3 m (0.13% of the lap, limit 3%) in 46 constant-curvature pieces, and that lap lies 0.6-1.0 m RMS off the trace - 'matches' once imported; the mapped pit lane fits as an 820 m pit road (r160 arcs, 601 m straight). The same reading predicts Daytona's real import to 0.1 m and its pit road exactly. The stale trained line (fitted to the generic oval, same lap length so it would have been accepted) is deleted. Still to do in Unity: Import Traced Geometry (selected asset) on Charlotte only, Export Geometry For Paddock Layout (Selected Track), re-run this report, redress walls/ground/stands, retrain the line, run the Charlotte track tests and a lap sim. |
| CharlotteRoval | game (old reference was an oval-and-Roval chain) | yes | #0 (oval + Roval + Roval 2024 infield + both chicanes, -1.07%) | Game line leaves the venue entirely - loops through the car parks north and south of the speedway; 107.6 m RMS. The old best (-3.19%) came from the straight-on walker, which cannot turn off the oval where the Roval does; the junction search finds the Roval proper. #0 includes both the frontstretch and backstretch chicanes; check the infield against the 2024 layout on the Unity aerial overlay before writing the trace (no diagram on Commons). |
| Darlington | game | yes (re-import) | #0 committed trace (= OSM way 104277971, Darlington Raceway, -4.5%) | Game is a symmetric oval (817 x 513 m) spilling outside the walls at both ends; the real egg is 843 x 393 m; 38.2 m RMS with the fit's scale pinned at its 0.95 limit. The trace is on the racing surface (drawn toward the inside, hence -4.5%) and README says it imported, yet the export is not that shape: either the package was regenerated afterwards or Tools/TrackExports/Darlington.json is stale. Re-export first; if still off, re-run Import Traced Geometry. |
| IRP | game | yes | #0 (OSM way 123830268, Lucas Oil Raceway, -4.5%) | Game is a long thin oval (464 x 156 m) on a wider, rounder 0.686 mi oval; 24.5 m RMS. The OSM ring sits on the racing surface toward the inside line (hence -4.5%; the importer rescales to the lap). |
| Indianapolis | game | yes | #0 (IMS oval's named ways: main straight, turns 1-4, short chutes, backstretch, +1.2%) | Game is a long thin stadium (1807 x 360 m); the real oval is a rounded rectangle (about 1.4 x 0.8 km) with short chutes; 150 m RMS. Not a tool artefact: #0 is a clean ring of the oval's own ways, and the outer-wall offset #1 gives the same fit (150.6 m). |
| IndyRoad | game (old reference was a backstretch-and-pit-lane chain) | yes | #0 (+0.00%) or #1 (-0.44%) - pick by the turn 1 / turns 12-12A layout | Game line wanders far outside the speedway; 163 m RMS. The old best (+0.99%) was the oval's backstretch chained through the warm-up lane and pit lanes; the new candidates are real road-course laps. #0 and #1 differ only where OSM maps 'Alternate Corner 1' and 'Alternate Corners 12 & 12A': choose the one matching the 2014-present Grand Prix course (14 turns) on the Unity aerial overlay. |
| Kansas | game, and the committed trace | yes | #1 (OSM way 37552701, Kansas Speedway, +2.6%) - or #0, the outer fence offset 8.4 m in, which agrees | Game is a narrow oval (1022 x 345 m) on a 1.5 mi tri-oval about 950 x 460 m; 70 m RMS against the real ring. The committed trace (old best, +0.24%) runs 34% down the pit lane - the importer refused it; replace it with #1. |
| LimeRock | game | yes | #0 (Climbing Turn layout, +0.02%); #1 is the South Chicane variant (-1.9%) | Game line follows the west side then leaves the property north and south; 46.7 m RMS. Reference right: #0 runs on the circuit all round. Wikipedia's full course is 1.53 mi against the game's 1.50: use #1 if the series runs the chicane, and check TrackDimensions' length. |
| MexicoCity | game | yes | #0 committed trace (GP circuit through the Foro Sol, +1.0%), rewritten with --write-trace=0 | Game line is a loose sketch; 121 m RMS. No NAIP imagery (outside the US), but the trace (94% on ways OSM names Autódromo Hermanos Rodríguez) follows the 2015-present layout through the stadium. The importer refused it on its 3% closure check, which is a segmentation problem, not a wrong reference: re-segment with --write-trace=0 (or take #1, +0.07%). |
