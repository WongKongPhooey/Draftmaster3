# Track reference status

Every venue measured against the real one by `python -I Tools/track_reference.py --all` (2026-11-17). "Built RMS" is how far the game's centreline is from the best real-world centreline candidate after fitting position, rotation and scale; matches <= 4 m, close <= 10 m, off <= 25 m, else wrong shape. Per-venue detail: `Tools/TrackReference/<id>/report.md`. See `Docs/Track-Reference.md`.

| Track | Verdict | Built RMS / p95 / max (m) | Best candidate | Its lap vs published | Wikipedia lap vs game | Aerial |
|---|---|---|---|---|---|---|
| [Atlanta](Atlanta/report.md) | wrong shape | 36.9 / 57.7 / 86.5 | committed trace (Assets/TrackTraces) | +0.06% | +0.00% | NAIP |
| [COTA](COTA/report.md) | wrong shape | 71.2 / 150.0 / 171.5 | raceway ring taken as the centre | -0.01% | +0.47% | NAIP |
| [Charlotte](Charlotte/report.md) | wrong shape | 42.8 / 67.6 / 70.9 | raceway ring taken as the centre | -2.11% | +0.00% | NAIP |
| [CharlotteRoval](CharlotteRoval/report.md) | wrong shape | 107.8 / 221.3 / 232.7 | raceway ring taken as the centre | -3.19% | +0.00% | NAIP |
| [Darlington](Darlington/report.md) | wrong shape | 38.2 / 52.4 / 52.5 | committed trace (Assets/TrackTraces) | -4.54% | -0.29% | NAIP |
| [Indianapolis](Indianapolis/report.md) | wrong shape | 150.3 / 177.1 / 177.6 | raceway ring taken as the centre | +1.16% | +0.00% | NAIP |
| [IndyRoad](IndyRoad/report.md) | wrong shape | 135.8 / 235.7 / 259.5 | raceway ring taken as the centre | +0.99% | -0.04% | NAIP |
| [Kansas](Kansas/report.md) | wrong shape | 46.4 / 59.3 / 60.5 | committed trace (Assets/TrackTraces) | +0.24% | +0.00% | NAIP |
| [LimeRock](LimeRock/report.md) | wrong shape | 46.7 / 85.5 / 99.0 | raceway ring taken as the centre | +0.02% | +2.00% | NAIP |
| [MexicoCity](MexicoCity/report.md) | wrong shape | 121.3 / 194.7 / 247.5 | committed trace (Assets/TrackTraces) | +1.02% | +0.15% | none |
| [IRP](IRP/report.md) | off | 24.5 / 30.9 / 31.2 | raceway ring taken as the centre | -4.52% | -0.58% | NAIP |
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
| [WatkinsGlen](WatkinsGlen/report.md) | matches | 1.9 / 4.4 / 7.0 | raceway ring taken as the centre | -0.20% | +0.00% | NAIP |
| [Chicago](Chicago/report.md) | unmeasured | - | none in OSM | - | -2.73% | NAIP |
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
