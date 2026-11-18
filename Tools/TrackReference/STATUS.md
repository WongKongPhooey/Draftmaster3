# Track reference status

Every venue measured against the real one by `python -I Tools/track_reference.py --all` (2026-11-17; Chicago, Portland, Richmond, RoadAmerica, Rockingham, SanDiego, Sonoma and Talladega run one by one on 2026-11-18). "Built RMS" is how far the game's centreline is from the best real-world centreline candidate after fitting position, rotation and scale; matches <= 4 m, close <= 10 m, off <= 25 m, else wrong shape. Per-venue detail: `Tools/TrackReference/<id>/report.md`. See `Docs/Track-Reference.md`.

| Track | Verdict | Built RMS / p95 / max (m) | Best candidate | Its lap vs published | Wikipedia lap vs game | Aerial |
|---|---|---|---|---|---|---|
| [Atlanta](Atlanta/report.md) | wrong shape | 36.9 / 57.7 / 86.5 | committed trace (Assets/TrackTraces) | +0.06% | +0.00% | NAIP |
| [COTA](COTA/report.md) | wrong shape | 71.2 / 150.0 / 171.5 | raceway ring taken as the centre | -0.01% | +0.47% | NAIP |
| [Charlotte](Charlotte/report.md) | wrong shape | 42.8 / 67.6 / 70.9 | raceway ring taken as the centre | -2.11% | +0.00% | NAIP |
| [CharlotteRoval](CharlotteRoval/report.md) | wrong shape | 107.8 / 221.3 / 232.7 | raceway ring taken as the centre | -3.19% | +0.00% | NAIP |
| [Chicago](Chicago/report.md) | wrong shape | 159.2 / 255.1 / 291.5 | raceway ring taken as the centre | -3.55% | -2.73% | NAIP |
| [Darlington](Darlington/report.md) | wrong shape | 38.2 / 52.4 / 52.5 | committed trace (Assets/TrackTraces) | -4.54% | -0.29% | NAIP |
| [Indianapolis](Indianapolis/report.md) | wrong shape | 150.3 / 177.1 / 177.6 | raceway ring taken as the centre | +1.16% | +0.00% | NAIP |
| [IndyRoad](IndyRoad/report.md) | wrong shape | 135.8 / 235.7 / 259.5 | raceway ring taken as the centre | +0.99% | -0.04% | NAIP |
| [Kansas](Kansas/report.md) | wrong shape | 46.4 / 59.3 / 60.5 | committed trace (Assets/TrackTraces) | +0.24% | +0.00% | NAIP |
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
| [BowmanGray](BowmanGray/report.md) | matches | 3.8 / 5.5 / 5.7 | committed trace (Assets/TrackTraces) | +0.14% | +0.00% | NAIP |
| [Bristol](Bristol/report.md) | matches | 3.3 / 4.7 / 4.9 | committed trace (Assets/TrackTraces) | -0.79% | +0.57% | NAIP |
| [Daytona](Daytona/report.md) | matches | 1.5 / 2.9 / 3.2 | committed trace (Assets/TrackTraces) | -0.32% | +0.00% | NAIP |
| [FortWorth](FortWorth/report.md) | matches | 1.6 / 2.6 / 3.5 | committed trace (Assets/TrackTraces) | -1.40% | -3.00% | NAIP |
| [Gateway](Gateway/report.md) | matches | 3.4 / 5.8 / 6.0 | committed trace (Assets/TrackTraces) | +1.48% | +0.00% | NAIP |
| [Martinsville](Martinsville/report.md) | matches | 1.1 / 2.1 / 2.5 | raceway ring taken as the centre | -3.24% | -0.75% | NAIP |
| [Richmond](Richmond/report.md) | matches | 1.2 / 2.4 / 3.0 | committed trace (Assets/TrackTraces) | -2.27% | +0.00% | NAIP |
| [WatkinsGlen](WatkinsGlen/report.md) | matches | 1.9 / 4.4 / 7.0 | raceway ring taken as the centre | -0.20% | +0.00% | NAIP |
| [SanDiego](SanDiego/report.md) | unmeasured | - | none in OSM | - | +13.33% | NAIP |
| Miami | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Michigan | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| MidOhio | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Milwaukee | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Nashville | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| NewHampshire | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| NorthWilkesboro | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Phoenix | error | not fetched yet (no cache/osm.json); run without --offline | | | | |

## Notes from the 2026-11-18 run (read before acting on a verdict)

- **Chicago**: OSM maps no raceway for the street course, so the reference is a synthetic lap assembled from OSM street centrelines in race order (S Columbus Dr, E Balbo Dr, DuSable Lake Shore Dr, E Roosevelt Rd, Columbus, Balbo, S Michigan Ave, E Congress Plaza Dr, E Jackson Dr; route from NBC Chicago's 2024 turn-by-turn guide and the Wikipedia map), corners smoothed. Unsmoothed street lap 3448.5 m against Wikipedia's 3444 m (2.14 mi). It is a street-centreline estimate, not a survey of the racing line. Rebuild with `Chicago/build_reference.py`; the pipeline merges `Chicago/reference_ways.json` into the OSM layers.
- **SanDiego**: the game's lap is 3.0 mi; Wikipedia (Coronado Street Course, NASCAR circuit 2026) gives 3.4 mi, 19 turns. OSM has only three fragments of the course (Runway Road, the Chicane and one unnamed piece, about 1.7 km together), so there is no ring to measure against.
- **Rockingham** and **Talladega**: the best candidate is a raceway ring that runs down pit road on the front stretch (Rockingham's committed trace is that same ring). The ring on the racing surface is the next candidate (Rockingham #2, +3.2% lap, built RMS 23.2 m; Talladega #1, +0.5% lap, built RMS 44.5 m), so the real gap is at least as bad as the verdict says.
- **Sonoma**: the committed trace is 6.7% shorter than the 2.52 mi layout the game uses. Check which layout it follows before using it.
- **Portland**: the committed trace sits on the real circuit, but the game's line does not follow it (47.6 m RMS). Either the trace was never imported or `Tools/TrackExports/Portland.json` is out of date.
- **Richmond** matches with a fit scale of x0.975, so the game's lap is about 2.5% longer than the traced one.
