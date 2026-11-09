"""Check the travel map's minor locations: Assets/Resources/Travel/Stops/<Region>.json.

    python Tools/validate_travel_stops.py                # validate; exit 1 on any error
    python Tools/validate_travel_stops.py --free-roads   # also list roads with no stop on them yet
    python Tools/validate_travel_stops.py --region West --free-roads

Errors (exit 1): bad JSON, missing fields, a duplicate id, an unknown circuit or part id, two stops on
one road, a stop with neither `road` nor `lat`/`lon`, coordinates outside the lower 48.
Warnings: a `road` the game's own road builder probably doesn't draw (the stop then lays that road
itself, which adds a highway to the map), a long flavor line.

The road check rebuilds TravelGraph's roads from the circuits' coordinates in TravelGraph.cs and the
hand placements in TravelMapLayout.cs: same projection, same Gabriel graph, same "at least three roads"
top-up, same closed roads. It skips SpreadOut's few-pixel nudges, so treat it as very nearly, not exactly,
what the game draws. Stdlib only. Used as a Tokenmaxxer worktree gate; see Docs/Travel-Map.md.
"""
import argparse
import json
import math
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
STOPS = ROOT / "Assets" / "Resources" / "Travel" / "Stops"
GRAPH = ROOT / "Assets" / "Scripts" / "Travel" / "TravelGraph.cs"
LAYOUT = ROOT / "Assets" / "Scripts" / "Travel" / "TravelMapLayout.cs"
PARTS = ROOT / "Assets" / "Scripts" / "Travel" / "PartCatalog.cs"

TYPES = ("EngineShop", "Fabricator", "Junkyard")   # EngineShop = engine builder
ID_RE = re.compile(r"^[a-z][a-z0-9_]{2,40}$")
BOARD_W, BOARD_H, EDGE_MARGIN = 1470.0, 950.0, 0.05


def circuits():
    """{id: (name, lat, lon)} from the Circuit(...) calls, in code order."""
    out = {}
    for m in re.finditer(r'Circuit\("(\w+)",\s*"([^"]+)",\s*(-?[\d.]+)f,\s*(-?[\d.]+)f\)',
                         GRAPH.read_text(encoding="utf-8")):
        out[m.group(1)] = (m.group(2), float(m.group(3)), float(m.group(4)))
    return out


def part_ids():
    text = PARTS.read_text(encoding="utf-8")
    return set(re.findall(r'\bid\s*=\s*"(\w+)"', text))


def layout():
    text = LAYOUT.read_text(encoding="utf-8")
    placed = {m.group(1): (float(m.group(2)), float(m.group(3))) for m in re.finditer(
        r'\{\s*"(\w+)",\s*new Vector2\((-?[\d.]+)f,\s*(-?[\d.]+)f\)\s*\}', text)}
    closed_block = text[text.find("Closed ="):text.find("ClosedRoads")]
    closed = {frozenset(p) for p in re.findall(r'\("(\w+)",\s*"(\w+)"\)', closed_block)}
    return placed, closed


def board_positions(circ):
    """Normalised board position per circuit — TravelGraph.ProjectCircuits without SpreadOut."""
    k = math.cos(math.radians(sum(c[1] for c in circ.values()) / len(circ)))
    uv = {cid: (lon * k, -lat) for cid, (_, lat, lon) in circ.items()}
    us = [p[0] for p in uv.values()]
    vs = [p[1] for p in uv.values()]
    scale = min(BOARD_W * (1 - 2 * EDGE_MARGIN) / max(max(us) - min(us), 1e-4),
                BOARD_H * (1 - 2 * EDGE_MARGIN) / max(max(vs) - min(vs), 1e-4))
    u0, v0 = (min(us) + max(us)) / 2, (min(vs) + max(vs)) / 2

    def to_board(lat, lon):
        return (((lon * k - u0) * scale + BOARD_W / 2) / BOARD_W,
                ((-lat - v0) * scale + BOARD_H / 2) / BOARD_H)

    pos = {cid: to_board(lat, lon) for cid, (_, lat, lon) in circ.items()}
    return pos, to_board


def roads(pos, closed):
    """TravelGraph.BuildRoads: Gabriel graph, then top every circuit up to three roads."""
    squash = BOARD_H / BOARD_W

    def sq(a, b):
        return (a[0] - b[0]) ** 2 + ((a[1] - b[1]) * squash) ** 2

    ids = list(pos)
    edges = set()
    adj = {i: set() for i in ids}
    for i in range(len(ids)):
        for j in range(i + 1, len(ids)):
            a, b = pos[ids[i]], pos[ids[j]]
            mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
            r2 = sq(a, b) / 4
            if all(sq(pos[c], mid) >= r2 for c in ids if c not in (ids[i], ids[j])):
                edges.add(frozenset((ids[i], ids[j])))
                adj[ids[i]].add(ids[j])
                adj[ids[j]].add(ids[i])
    for i in ids:
        while len(adj[i]) < 3:
            best = min((c for c in ids if c != i and c not in adj[i]), key=lambda c: sq(pos[i], pos[c]),
                       default=None)
            if best is None:
                break
            edges.add(frozenset((i, best)))
            adj[i].add(best)
            adj[best].add(i)
    return edges - closed


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--free-roads", action="store_true", help="list circuit roads with no stop on them")
    ap.add_argument("--region", help="with --free-roads: only roads touching this region file's stops' area")
    a = ap.parse_args()

    circ = circuits()
    parts = part_ids()
    placed, closed = layout()
    pos, to_board = board_positions(circ)
    pos.update({k: v for k, v in placed.items() if k in pos})
    drawn = roads(pos, closed)

    errors, warnings = [], []
    seen_ids, used_roads = {}, {}
    files = sorted(STOPS.glob("*.json"))
    if not files:
        errors.append(f"no stop files in {STOPS.relative_to(ROOT)}")
    region_points = {}
    for f in files:
        rel = f.relative_to(ROOT).as_posix()
        try:
            data = json.loads(f.read_text(encoding="utf-8"))
        except ValueError as e:
            errors.append(f"{rel}: not valid JSON ({e})")
            continue
        stops = data.get("stops") if isinstance(data, dict) else None
        if not isinstance(stops, list):
            errors.append(f"{rel}: needs {{\"region\": ..., \"stops\": [...]}}")
            continue
        if data.get("region") and data["region"] != f.stem:
            warnings.append(f"{rel}: region '{data['region']}' doesn't match the file name")
        for i, s in enumerate(stops):
            where = f"{rel} stop {i} ({(s or {}).get('id', '?')})"
            if not isinstance(s, dict):
                errors.append(f"{where}: not an object")
                continue
            sid = s.get("id", "")
            if not ID_RE.match(sid or ""):
                errors.append(f"{where}: id must be snake_case, 3-41 chars")
            elif sid in seen_ids or sid in circ or sid == "team_factory":
                errors.append(f"{where}: id '{sid}' already used ({seen_ids.get(sid, 'a circuit or the factory')})")
            seen_ids.setdefault(sid, rel)
            if s.get("type") not in TYPES:
                errors.append(f"{where}: type must be one of {TYPES}")
            if not str(s.get("name", "")).strip():
                errors.append(f"{where}: no name")
            flavor = str(s.get("flavor", "")).strip()
            if not flavor:
                errors.append(f"{where}: no flavor line")
            elif len(flavor) > 200:
                warnings.append(f"{where}: flavor is {len(flavor)} chars; the shop panel fits ~200")
            stock = s.get("stock") or []
            if s.get("type") == "EngineShop":
                if not 1 <= len(stock) <= 6:
                    errors.append(f"{where}: an EngineShop stocks 1-6 parts")
                for p in stock:
                    if p not in parts:
                        errors.append(f"{where}: unknown part id '{p}'")
                    elif p.endswith("_team"):
                        errors.append(f"{where}: '{p}' is team-factory only")
            elif stock:
                errors.append(f"{where}: only an EngineShop has `stock` (a fabricator sells by `tier`, a junkyard rolls weekly)")
            if s.get("type") == "Fabricator":
                if s.get("tier") not in (1, 2, 3):
                    errors.append(f"{where}: a Fabricator needs `tier` 1 (Standard), 2 (Pro) or 3 (Elite)")
            elif "tier" in s:
                errors.append(f"{where}: only a Fabricator has a `tier`")
            road = s.get("road")
            lat, lon = s.get("lat"), s.get("lon")
            if lat is not None or lon is not None:
                if not (isinstance(lat, (int, float)) and isinstance(lon, (int, float))
                        and 24 <= lat <= 50 and -125 <= lon <= -66):
                    errors.append(f"{where}: lat/lon must both be numbers inside the lower 48")
                else:
                    region_points.setdefault(f.stem, []).append(to_board(lat, lon))
            if road:
                if not (isinstance(road, list) and len(road) == 2 and road[0] != road[1]):
                    errors.append(f"{where}: road must be two different circuit ids")
                    continue
                bad = [r for r in road if r not in circ]
                if bad:
                    errors.append(f"{where}: unknown circuit id(s) {bad}")
                    continue
                key = frozenset(road)
                if key in used_roads:
                    errors.append(f"{where}: road {road[0]} - {road[1]} already carries '{used_roads[key]}'")
                used_roads[key] = sid
                if key not in drawn:
                    warnings.append(f"{where}: {road[0]} - {road[1]} is probably not a road the map draws; "
                                    "the stop would add it (use --free-roads to pick a drawn one)")
                region_points.setdefault(f.stem, []).extend(pos[r] for r in road)
            elif lat is None:
                errors.append(f"{where}: needs `road` (two circuit ids) or `lat` + `lon`")

    for w in warnings:
        print(f"warning: {w}")
    for e in errors:
        print(f"error: {e}")
    print(f"{len(seen_ids)} stops in {len(files)} file(s): "
          f"{len(errors)} error(s), {len(warnings)} warning(s)")

    if a.free_roads:
        free = sorted((tuple(sorted(r)) for r in drawn if r not in used_roads))
        if a.region and region_points.get(a.region):
            pts = region_points[a.region]
            cx = sum(p[0] for p in pts) / len(pts)
            cy = sum(p[1] for p in pts) / len(pts)
            free.sort(key=lambda r: ((pos[r[0]][0] + pos[r[1]][0]) / 2 - cx) ** 2
                      + ((pos[r[0]][1] + pos[r[1]][1]) / 2 - cy) ** 2)
        print(f"\n{len(free)} drawn road(s) with no stop yet"
              + (f", nearest the {a.region} stops first" if a.region else "") + ":")
        for x, y in free:
            (_, la, lo), (_, lb, lob) = circ[x], circ[y]
            print(f"  {x} - {y}   midpoint ~({(la + lb) / 2:.2f}, {(lo + lob) / 2:.2f})")
    sys.exit(1 if errors else 0)


if __name__ == "__main__":
    main()
