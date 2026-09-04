# Tools

Scripts that are run by hand, outside Unity.

| Script | What it does |
| --- | --- |
| `fetch_traces.py` | Pulls one venue's traced centreline out of OpenStreetMap into `Assets/TrackTraces/`. Identifies the circuit by finding a closed ring — walking several ways through their junctions where a venue is mapped that way — and checking it against the published lap length. |
| `fetch_traces_batch.py` | The same thing for every venue still missing a trace, in one Overpass query per five venues. Overpass rate-limits a request per venue into uselessness; asking in batches takes a couple of minutes for the lot. It caches the raw answer as `all_raceways.json` so the ring-finding can be iterated on offline. |

Run either with the repo root as the working directory:

```
python Tools/fetch_traces_batch.py          # everything still missing
python Tools/fetch_traces.py Phoenix        # one venue
```

Then, in Unity, `Draftmaster > Tracks > Import Traced Geometry For Every Trace`. The importer refuses a
trace whose shape does not close to within 3% of its own lap, so a bad fetch cannot quietly replace good
geometry — see `Docs/Tracks.md`.

Data © OpenStreetMap contributors, ODbL. `Assets/TrackTraces/README.md` carries the licence note.
