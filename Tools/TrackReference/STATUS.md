# Track reference status

Every venue measured against the real one by `python -I Tools/track_reference.py --all` (2026-11-17). "Built RMS" is how far the game's centreline is from the best real-world centreline candidate after fitting position, rotation and scale; matches <= 4 m, close <= 10 m, off <= 25 m, else wrong shape. Per-venue detail: `Tools/TrackReference/<id>/report.md`. See `Docs/Track-Reference.md`.

| Track | Verdict | Built RMS / p95 / max (m) | Best candidate | Its lap vs published | Wikipedia lap vs game | Aerial |
|---|---|---|---|---|---|---|
| [Atlanta](Atlanta/report.md) | wrong shape | 36.9 / 57.7 / 86.5 | committed trace (Assets/TrackTraces) | +0.06% | +0.00% | NAIP |
| [COTA](COTA/report.md) | wrong shape | 71.2 / 150.0 / 171.5 | raceway ring taken as the centre | -0.01% | +0.47% | NAIP |
| [Charlotte](Charlotte/report.md) | wrong shape | 42.8 / 67.6 / 70.9 | raceway ring taken as the centre | -2.11% | +0.00% | NAIP |
| [CharlotteRoval](CharlotteRoval/report.md) | wrong shape | 107.8 / 221.3 / 232.7 | raceway ring taken as the centre | -3.19% | +0.00% | NAIP |
| [Darlington](Darlington/report.md) | wrong shape | 38.2 / 52.4 / 52.5 | committed trace (Assets/TrackTraces) | -4.54% | -0.29% | NAIP |
| [Dover](Dover/report.md) | close | 4.2 / 8.0 / 8.8 | committed trace (Assets/TrackTraces) | +3.22% | +0.00% | NAIP |
| [BowmanGray](BowmanGray/report.md) | matches | 3.8 / 5.5 / 5.7 | committed trace (Assets/TrackTraces) | +0.14% | +0.00% | NAIP |
| [Bristol](Bristol/report.md) | matches | 3.3 / 4.7 / 4.9 | committed trace (Assets/TrackTraces) | -0.79% | +0.57% | NAIP |
| [Daytona](Daytona/report.md) | matches | 1.5 / 2.9 / 3.2 | committed trace (Assets/TrackTraces) | -0.32% | +0.00% | NAIP |
| [FortWorth](FortWorth/report.md) | matches | 1.6 / 2.6 / 3.5 | committed trace (Assets/TrackTraces) | -1.40% | -3.00% | NAIP |
| [Gateway](Gateway/report.md) | matches | 3.4 / 5.8 / 6.0 | committed trace (Assets/TrackTraces) | +1.48% | +0.00% | NAIP |
| [WatkinsGlen](WatkinsGlen/report.md) | matches | 1.9 / 4.4 / 7.0 | raceway ring taken as the centre | -0.20% | +0.00% | NAIP |
| [Chicago](Chicago/report.md) | unmeasured | - | none in OSM | - | -2.73% | NAIP |
| IRP | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Indianapolis | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| IndyRoad | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Iowa | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Kansas | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| LasVegas | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| LimeRock | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| LongPond | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Martinsville | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| MexicoCity | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Miami | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Michigan | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| MidOhio | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Milwaukee | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Nashville | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| NewHampshire | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| NorthWilkesboro | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Phoenix | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Portland | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Richmond | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| RoadAmerica | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Rockingham | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| SanDiego | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Sonoma | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
| Talladega | error | not fetched yet (no cache/osm.json); run without --offline | | | | |
