"""Generate randomised paddock layouts (sidecars) from the exported track geometry.

    python Tools/paddock_generate.py Dover                 # top up to 8 layouts -> Assets/Resources/Paddocks/Dover.json
    python Tools/paddock_generate.py --all                 # every exported track the package doesn't lay out itself
    python Tools/paddock_generate.py Dover --regenerate --seed 7 --preview   # new generated ones, authored kept

How many layouts a track gets, and whether any are generated at all (Indianapolis: hand-authored only), is in
Tools/paddock_variants.json. Layouts saved from Draftmaster > Tracks > Paddock Variants are marked `authored`
and are never replaced or reordered — they come first, generated ones fill the rest.

Every variant has the full set of pieces — the team garages and the drivers' motorhome lot, the sponsor
area (SponsorSuite), the fan zone (SigningFence), the drivers' room (MeetingRoom), the intro stage and a
grandstand gate with its seat across the track — and none of them overlap each other, the road, the pit
lane or anything already in the package. What varies is WHERE: which stretch of the pit lane each piece
sits behind, how far back, in what order, how the lots are proportioned and turned. The game picks one
variant per visit (PaddockSidecar: track + travel week), so the paddock is laid out differently each time
you come back but never changes mid-weekend.

The paddock goes behind the pit lane, on the side away from the racing surface — where it is at every real
venue, and where the authored Watkins Glen / Daytona / Texas / Martinsville packages put it. Tracks whose
package already places lots and markers are skipped (the package wins anyway). Deterministic per seed.
Stdlib only; uses Tools/paddock_sidecar.py for the geometry checks.
"""
import argparse
import hashlib
import json
import math
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import paddock_sidecar as ps  # noqa: E402

CLEAR = 4.0          # metres of open ground kept round every piece
ROAD_CLEAR = 6.0     # past the road edge
PIT_CLEAR = 3.0      # past the pit lane edge

# (name, width, height, preferred stretch of the pit lane as (lo, hi) fractions, distance band behind it)
MARKERS = [
    ("SponsorSuite_Marker", (12, 18), (10, 14), (0.15, 0.85), (10, 110)),
    ("SigningFence_Marker", (16, 26), (3, 5), (0.1, 0.9), (8, 130)),
    ("MeetingRoom_Marker", (8, 12), (8, 12), (0.15, 0.85), (10, 120)),
    ("IntroStage_Marker", (10, 14), (6, 9), (0.72, 0.97), (6, 50)),   # the end of pit road
]


def stable_seed(track_id, seed):
    return int(hashlib.sha1(f"{track_id}:{seed}".encode()).hexdigest()[:8], 16)


def inside(poly, x, y):
    """Point in polygon (ray cast)."""
    hit = False
    n = len(poly)
    for i in range(n):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % n]
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / ((y2 - y1) or 1e-9) + x1:
            hit = not hit
    return hit


class Site:
    """One track's geometry and everything placed so far."""

    def __init__(self, export):
        self.road = [tuple(p) for p in export["centerline"]]
        self.pit = [tuple(p) for p in export["pitLane"]]
        self.poly = [(p[0], p[1]) for p in self.road]
        self.objects = [ps.rect(o["x"], o["y"], o["width"], o["height"], o.get("rotation", 0))
                        for o in export["objects"]
                        if o["width"] * o["height"] < ps.BIG_OBJECT_M2]
        self.start = tuple(export["startLine"])
        # The package's own stands: where a grandstand seat should be.
        self.stands = [(o["x"], o["y"]) for o in export["objects"]
                       if o["name"].lower().startswith("grandstand") and o.get("kind") == "object"]
        self.placed = []
        self.spine = self._spine()

    def _spine(self):
        """The pit lane (or, with none, the 400 m of road after the start line) as
        (x, y, tangent angle, away-from-track normal, half width) samples."""
        line = self.pit if len(self.pit) >= 4 else self._road_from_start(400)
        out = []
        for i, (x, y, w) in enumerate(line):
            a = line[max(i - 1, 0)]
            b = line[min(i + 1, len(line) - 1)]
            ang = math.atan2(b[1] - a[1], b[0] - a[0])
            nx, ny = -math.sin(ang), math.cos(ang)
            # Away from the racing surface: the side whose probe point is further from the road.
            if self._road_gap(x + nx * 20, y + ny * 20) < self._road_gap(x - nx * 20, y - ny * 20):
                nx, ny = -nx, -ny
            out.append((x, y, math.degrees(ang), (nx, ny), w / 2))
        return out

    def _road_from_start(self, metres):
        best = min(range(len(self.road)), key=lambda i: (self.road[i][0] - self.start[0]) ** 2
                   + (self.road[i][1] - self.start[1]) ** 2)
        n = max(4, int(metres / 5))
        return [self.road[(best + k) % len(self.road)] for k in range(n)]

    def _road_gap(self, x, y):
        return min(math.hypot(x - p[0], y - p[1]) - p[2] / 2 for p in self.road)

    def fits(self, box, x, y, w, h, rot):
        for px, py, pw in self.road:
            if ps.point_rect_distance(px, py, x, y, w, h, rot) < pw / 2 + ROAD_CLEAR:
                return False
        for px, py, pw in self.pit:
            if ps.point_rect_distance(px, py, x, y, w, h, rot) < pw / 2 + PIT_CLEAR:
                return False
        grown = ps.rect(x, y, w + 2 * CLEAR, h + 2 * CLEAR, rot)
        if any(ps.rects_overlap(grown, o) for o in self.objects):
            return False
        if any(ps.rects_overlap(grown, p) for p in self.placed):
            return False
        return True

    def place(self, rng, w, h, stretch, band, rot_jitter=6.0, tries=600):
        """A spot behind the spine for a w x h box, or None."""
        n = len(self.spine)
        for attempt in range(tries):
            # Widen the search the longer it fails, so a cramped track still gets everything.
            grow = attempt / tries
            lo = max(0.0, stretch[0] - grow * 0.3)
            hi = min(1.0, stretch[1] + grow * 0.3)
            x0, y0, ang, (nx, ny), half = self.spine[min(n - 1, int(rng.uniform(lo, hi) * (n - 1)))]
            d = half + rng.uniform(band[0], band[1] * (1 + 1.5 * grow)) + h / 2
            x, y = x0 + nx * d, y0 + ny * d
            rot = ang + rng.uniform(-rot_jitter, rot_jitter)
            if self.fits(None, x, y, w, h, rot):
                self.placed.append(ps.rect(x, y, w, h, rot))
                return round(x, 1), round(y, 1), round(rot % 360, 1)
        return None


def lot_size(rng, kind):
    across, deep = ps.RV if kind == "Motorhomes" else ps.GARAGE
    gap = round(rng.uniform(1.5, 3.0), 1)
    row_gap = round(rng.uniform(5.0, 8.0), 1)
    per_line = rng.randint(11, 22) if kind == "Motorhomes" else rng.randint(8, 16)
    lines = math.ceil(ps.FIELD / per_line)
    w = per_line * (across + gap) + 2
    h = lines * deep + (lines - 1) * row_gap + 2
    return round(w, 1), round(h, 1), gap, row_gap


def grandstand(site, rng):
    """The gate on the paddock side, near the start line, and the seat across the track."""
    road = site.road
    k = min(range(len(road)), key=lambda i: (road[i][0] - site.start[0]) ** 2 + (road[i][1] - site.start[1]) ** 2)
    k = (k + rng.randint(-25, 25)) % len(road)
    rx, ry, rw = road[k]
    # Seat: across the road from the paddock — the side of this sample further from the spine.
    a, b = road[(k - 1) % len(road)], road[(k + 1) % len(road)]
    ang = math.atan2(b[1] - a[1], b[0] - a[0])
    nx, ny = -math.sin(ang), math.cos(ang)
    sx, sy = site.spine[len(site.spine) // 2][:2]
    if (rx + nx - sx) ** 2 + (ry + ny - sy) ** 2 < (rx - nx - sx) ** 2 + (ry - ny - sy) ** 2:
        nx, ny = -nx, -ny
    off = rw / 2 + rng.uniform(18, 35)
    seat = {"x": round(rx + nx * off, 1), "y": round(ry + ny * off, 1)}
    view = {"x": round(rx, 1), "y": round(ry, 1)}
    if site.stands:
        # A seat in one of the package's own grandstands, and the camera on the road in front of it.
        gx, gy = rng.choice(site.stands)
        seat = {"x": round(gx, 1), "y": round(gy, 1)}
        j = min(range(len(road)), key=lambda i: (road[i][0] - gx) ** 2 + (road[i][1] - gy) ** 2)
        view = {"x": round(road[j][0], 1), "y": round(road[j][1], 1)}
    spot = site.place(rng, 10, 10, (0.0, 1.0), (6, 40))
    if spot is None:
        return None
    x, y, rot = spot
    return {"name": "Grandstand_Marker", "x": x, "y": y, "rotation": rot, "width": 10, "height": 10,
            "seat": seat, "view": view}


def variant(export, rng):
    site = Site(export)
    lots, markers = [], []
    kinds = ["Garages", "Motorhomes"]
    rng.shuffle(kinds)
    for kind in kinds:
        for _ in range(6):   # a smaller lot shape if the first one finds no room
            w, h, gap, row_gap = lot_size(rng, kind)
            spot = site.place(rng, w, h, (0.1, 0.9), (8, 70), rot_jitter=4.0)
            if spot:
                x, y, rot = spot
                lots.append({"kind": kind, "x": x, "y": y, "rotation": rot, "width": w, "height": h,
                             "gap": gap, "rowGap": row_gap})
                break
        else:
            return None, f"no room for the {kind} lot"
    order = MARKERS[:]
    rng.shuffle(order)
    for name, wr, hr, stretch, band in order:
        w, h = round(rng.uniform(*wr), 1), round(rng.uniform(*hr), 1)
        spot = site.place(rng, w, h, stretch, band)
        if spot is None:
            return None, f"no room for {name}"
        x, y, rot = spot
        markers.append({"name": name, "x": x, "y": y, "rotation": rot, "width": w, "height": h})
    g = grandstand(site, rng)
    if g is None:
        return None, "no room for the grandstand gate"
    markers.append(g)
    return {"markers": markers, "lots": lots}, None


def authored(export):
    return any(o.get("kind") in ("lot", "marker") for o in export["objects"])


CONFIG = Path(__file__).resolve().parent / "paddock_variants.json"


def settings(track_id):
    """(variants wanted in total, may generate) from Tools/paddock_variants.json."""
    try:
        cfg = json.loads(CONFIG.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        cfg = {}
    merged = dict({"variants": 8, "generate": True}, **(cfg.get("default") or {}))
    merged.update((cfg.get("tracks") or {}).get(track_id) or {})
    return int(merged["variants"]), bool(merged["generate"])


def generate(track_id, count, seed, log=print, regenerate=False):
    """Bring a track's sidecar up to `count` layouts. Variants marked `authored` (saved from the Paddock
    Variants window) are always kept, first and untouched; generated ones fill the rest. Without
    `regenerate`, generated variants already in the file are kept too, so a re-run only tops up."""
    export, err = ps.load(ps.EXPORTS / f"{track_id}.json")
    if err:
        log(f"{track_id}: no geometry export ({err})")
        return False
    if authored(export):
        log(f"{track_id}: the package lays out its own paddock — skipped")
        return False
    want, may_generate = settings(track_id)
    if count is not None:
        want = count
    existing, _ = ps.load(ps.SIDECARS / f"{track_id}.json")
    old = (existing or {}).get("variants") or []
    keep = [v for v in old if v.get("authored")]
    if not regenerate:
        keep += [v for v in old if not v.get("authored")]
    if not may_generate:
        if not old:
            log(f"{track_id}: set to hand-authored only (Tools/paddock_variants.json) and has no layouts yet — "
                "author one in Draftmaster > Tracks > Paddock Variants")
            return False
        keep = old if not regenerate else [v for v in old if v.get("authored")]
        if not keep:
            log(f"{track_id}: hand-authored only and nothing authored yet — left as it was")
            return False
        variants, notes = keep, []
    else:
        variants, notes = list(keep), []
        attempt = 0
        while len(variants) < want and attempt < want * 6:
            rng = random.Random(stable_seed(track_id, seed * 1000 + attempt))
            attempt += 1
            v, why = variant(export, rng)
            if v:
                variants.append(v)
            else:
                notes.append(why)
        variants = variants[:max(want, sum(1 for v in variants if v.get("authored")))]
    if not variants:
        log(f"{track_id}: FAILED — {', '.join(sorted(set(notes)))}")
        return False
    data = {"trackId": track_id,
            "generatedBy": f"Tools/paddock_generate.py seed {seed}",
            "markers": variants[0]["markers"], "lots": variants[0]["lots"],
            "variants": variants}
    ps.SIDECARS.mkdir(parents=True, exist_ok=True)
    (ps.SIDECARS / f"{track_id}.json").write_text(json.dumps(data, indent=1) + "\n", encoding="utf-8")
    n_auth = sum(1 for v in variants if v.get("authored"))
    log(f"{track_id}: {len(variants)} variant(s), {n_auth} authored"
        + (f" ({len(notes)} rejected: {', '.join(sorted(set(notes)))})" if notes else ""))
    return True


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("tracks", nargs="*")
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--variants", type=int, default=None,
                    help="layouts per track (default: Tools/paddock_variants.json, normally 8)")
    ap.add_argument("--regenerate", action="store_true",
                    help="replace the generated layouts (authored ones are always kept)")
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--preview", action="store_true", help="also render every variant's PNG")
    a = ap.parse_args()
    ids = sorted(p.stem for p in ps.EXPORTS.glob("*.json")) if a.all else a.tracks
    if not ids:
        ap.error("name a track or pass --all")
    ok = 0
    for tid in ids:
        if generate(tid, a.variants, a.seed, regenerate=a.regenerate):
            ok += 1
            if a.preview:
                n = len(json.loads((ps.SIDECARS / f"{tid}.json").read_text(encoding="utf-8"))["variants"])
                for k in range(n):
                    ps.preview(tid, k)
    print(f"{ok}/{len(ids)} track(s) written")


if __name__ == "__main__":
    main()
