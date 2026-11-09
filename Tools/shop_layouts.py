"""Roadside shop interiors (satnav stops): generate, validate and preview their layout variants.

    python Tools/shop_layouts.py generate                  # top every kind up to 8 variants
    python Tools/shop_layouts.py generate Fabricator --regenerate --seed 3
    python Tools/shop_layouts.py validate                  # exit 1 on any error
    python Tools/shop_layouts.py preview Junkyard 2        # Temp/ShopPreview/Junkyard.v2.png

Files: Assets/Resources/Travel/Shops/<Kind>.json (Fabricator, EngineBuilder, Junkyard), or <locationId>.json
for one place with its own floor plans (validate them with --kind). The game picks one variant per visit
(Draftmaster.Tracks.ShopLayouts.Pick: location + travel week) and RoadsideShopBuilder builds it.

Variants saved from Draftmaster > Travel Map > Shop Layouts are marked `authored`; generate keeps them first
and untouched and only fills the rest. The rules here mirror ShopLayouts.Problems (C#), which the headless
tests run over the same files: pieces don't overlap (with a walkway between), stay on the floor, keep the door
clear, the clerk stands at the counter and every working bay has somebody at it.
"""
import argparse
import hashlib
import json
import math
import random
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SHOPS = ROOT / "Assets" / "Resources" / "Travel" / "Shops"
PREVIEWS = ROOT / "Temp" / "ShopPreview"
KINDS = ("Fabricator", "EngineBuilder", "Junkyard")
DEFAULT_COUNT = 8

COUNTER_W, COUNTER_D = 3.6, 1.2
DOOR_W, DOOR_CLEAR, WALL_MARGIN, CLEAR = 2.4, 3.5, 0.6, 0.8


def bay_size(kind):
    return (2.6, 2.6) if kind == "EngineBuilder" else (6.2, 3.4)


# Room size, bay count, how many bays are mid-build, props — per kind.
RECIPES = {
    "Fabricator": dict(w=(20, 26), h=(14, 18), bays=(4, 6), working=(1, 2), props=(3, 6),
                       prop_kinds=["bench", "rack", "welder", "toolbox", "bench"]),
    "EngineBuilder": dict(w=(16, 22), h=(12, 16), bays=(5, 9), working=(2, 4), props=(3, 6),
                          prop_kinds=["bench", "rack", "toolbox", "crate"]),
    "Junkyard": dict(w=(26, 34), h=(18, 24), bays=(5, 8), working=(0, 0), props=(5, 9),
                     prop_kinds=["tyres", "crate", "tyres", "rack"]),
}
PROP_SIZES = {"bench": (2.4, 0.9), "rack": (3.0, 0.8), "welder": (0.9, 0.9), "toolbox": (1.2, 0.6),
              "crate": (1.4, 1.4), "tyres": (1.6, 1.6)}


# ------------------------------------------------------------------ geometry

def corners(x, y, w, h, rot):
    c, s = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    return [(x + ox * c - oy * s, y + ox * s + oy * c)
            for ox, oy in ((-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2))]


def overlap(a, b):
    for poly in (a, b):
        for i in range(4):
            (x1, y1), (x2, y2) = poly[i], poly[(i + 1) % 4]
            nx, ny = y1 - y2, x2 - x1
            pa = [nx * px + ny * py for px, py in a]
            pb = [nx * px + ny * py for px, py in b]
            if max(pa) < min(pb) or max(pb) < min(pa):
                return False
    return True


def footprints(layout, kind):
    bw, bh = bay_size(kind)
    out = [("counter", layout["counter"]["x"], layout["counter"]["y"], COUNTER_W, COUNTER_D,
            layout["counter"].get("rotation", 0))]
    out += [(f"bay {i}", b["x"], b["y"], bw, bh, b.get("rotation", 0)) for i, b in enumerate(layout.get("bays", []))]
    out += [(f"{p['kind']} {i}", p["x"], p["y"], p["width"], p["height"], p.get("rotation", 0))
            for i, p in enumerate(layout.get("props", []))]
    out += [(f"worker {i}", w["x"], w["y"], 0.8, 0.8, 0) for i, w in enumerate(layout.get("workers", []))]
    return out


def problems(layout, kind):
    """Mirror of Draftmaster.Tracks.ShopLayouts.Problems."""
    out = []
    W, H = layout["width"], layout["height"]
    if W < 10 or H < 8:
        out.append(f"room {W}x{H} m is too small")
    if not layout.get("bays"):
        out.append("no bays")
    fp = footprints(layout, kind)
    hw, hh = W / 2 - WALL_MARGIN, H / 2 - WALL_MARGIN
    door = corners(layout["doorX"], -H / 2 + DOOR_CLEAR / 2, DOOR_W + 1, DOOR_CLEAR, 0)
    for what, x, y, w, h, rot in fp:
        if any(abs(cx) > hw + 0.01 or abs(cy) > hh + 0.01 for cx, cy in corners(x, y, w, h, rot)):
            out.append(f"{what} is off the floor")
        if overlap(corners(x, y, w, h, rot), door):
            out.append(f"{what} blocks the door")
    if abs(layout["doorX"]) > W / 2 - DOOR_W:
        out.append("door is off the bottom wall")
    for i in range(len(fp)):
        for j in range(i + 1, len(fp)):
            a, b = fp[i], fp[j]
            worker_at_bay = ({a[0].split()[0], b[0].split()[0]} == {"worker", "bay"})
            pad = -0.3 if worker_at_bay else CLEAR
            if overlap(corners(a[1], a[2], a[3] + pad, a[4] + pad, a[5]),
                       corners(b[1], b[2], b[3] + pad, b[4] + pad, b[5])):
                out.append(f"{a[0]} overlaps {b[0]}")
    c, k = layout["counter"], layout["clerk"]
    if math.hypot(k["x"] - c["x"], k["y"] - c["y"]) > 2.5:
        out.append("clerk is not at the counter")
    bw, bh = bay_size(kind)
    reach = max(bw, bh) / 2 + 1.6
    for b in layout.get("bays", []):
        if b.get("state") == "working" and not any(
                math.hypot(w["x"] - b["x"], w["y"] - b["y"]) <= reach for w in layout.get("workers", [])):
            out.append(f"working bay at ({b['x']}, {b['y']}) has nobody at it")
    return out


# ------------------------------------------------------------------ generation

def r1(v):
    return round(v, 1)


def generate_one(kind, rng):
    rec = RECIPES[kind]
    W = r1(rng.uniform(*rec["w"]))
    H = r1(rng.uniform(*rec["h"]))
    door = r1(rng.uniform(-W / 2 + 3.5, W / 2 - 3.5))
    # Counter on the far side of the room from the door along the front, so you walk past the work to it.
    side = -1 if door > 0 else 1
    cx = r1(side * rng.uniform(W / 4, W / 2 - COUNTER_W / 2 - WALL_MARGIN - 0.5))
    cy = r1(-H / 2 + rng.uniform(2.6, 4.2))
    layout = {"name": "", "authored": False, "width": W, "height": H, "doorX": door,
              "counter": {"x": cx, "y": cy, "rotation": 0}, "clerk": {"x": cx, "y": r1(cy + 1.1), "rotation": 0},
              "bays": [], "workers": [], "props": []}

    def fits(extra):
        trial = json.loads(json.dumps(layout))
        for key, item in extra:
            trial[key].append(item)
        return not problems(trial, kind) or all(p.startswith("working bay") for p in problems(trial, kind))

    bw, bh = bay_size(kind)
    want = rng.randint(*rec["bays"])
    # Rows of bays look like a workshop; a junkyard is rows that have gone a bit wonky.
    tilt = 10 if kind == "Junkyard" else 0
    for _ in range(want * 60):
        if len(layout["bays"]) >= want:
            break
        rot = rng.choice((0, 0, 90)) + (rng.uniform(-tilt, tilt) if tilt else 0)
        x = r1(rng.uniform(-W / 2 + 1, W / 2 - 1))
        y = r1(rng.uniform(-H / 2 + DOOR_CLEAR, H / 2 - 1))
        bay = {"x": x, "y": y, "rotation": r1(rot % 360), "state": "finished"}
        if fits([("bays", bay)]):
            layout["bays"].append(bay)
    if len(layout["bays"]) < max(2, rec["bays"][0] - 1):
        return None

    # Mid-build bays, each with somebody at it.
    n_work = rng.randint(*rec["working"])
    order = list(range(len(layout["bays"])))
    rng.shuffle(order)
    for i in order:
        if n_work <= 0:
            break
        b = layout["bays"][i]
        offs = [(0, bh / 2 + 0.6), (0, -bh / 2 - 0.6), (bw / 2 + 0.6, 0), (-bw / 2 - 0.6, 0)]
        rng.shuffle(offs)
        for ox, oy in offs:
            c, s = math.cos(math.radians(b["rotation"])), math.sin(math.radians(b["rotation"]))
            worker = {"x": r1(b["x"] + ox * c - oy * s), "y": r1(b["y"] + ox * s + oy * c), "rotation": 0}
            b["state"] = "working"
            if fits([("workers", worker)]):
                layout["workers"].append(worker)
                n_work -= 1
                break
            b["state"] = "finished"
    # A junkyard has a yard hand wandering the rows.
    if kind == "Junkyard":
        for _ in range(200):
            worker = {"x": r1(rng.uniform(-W / 2 + 1, W / 2 - 1)), "y": r1(rng.uniform(-H / 2 + DOOR_CLEAR, H / 2 - 1)),
                      "rotation": 0}
            if fits([("workers", worker)]):
                layout["workers"].append(worker)
                break

    # Clutter along the walls.
    want_props = rng.randint(*rec["props"])
    for _ in range(want_props * 80):
        if len(layout["props"]) >= want_props:
            break
        kind_p = rng.choice(rec["prop_kinds"])
        pw, ph = PROP_SIZES[kind_p]
        wall = rng.choice(("top", "left", "right", "top"))
        if wall == "top":
            x, y, rot = rng.uniform(-W / 2 + pw, W / 2 - pw), H / 2 - WALL_MARGIN - ph / 2 - 0.15, 0
        else:
            sgn = -1 if wall == "left" else 1
            x, y, rot = sgn * (W / 2 - WALL_MARGIN - ph / 2 - 0.15), rng.uniform(-H / 2 + DOOR_CLEAR, H / 2 - pw), 90
        prop = {"kind": kind_p, "x": r1(x), "y": r1(y), "rotation": rot, "width": pw, "height": ph}
        if fits([("props", prop)]):
            layout["props"].append(prop)
    return layout if not problems(layout, kind) else None


def stable_seed(kind, seed, attempt):
    return int(hashlib.sha1(f"{kind}:{seed}:{attempt}".encode()).hexdigest()[:8], 16)


def load(path):
    try:
        return json.loads(path.read_text(encoding="utf-8")), None
    except FileNotFoundError:
        return None, "missing"
    except ValueError as e:
        return None, f"not valid JSON ({e})"


def generate(kind, count, seed, regenerate):
    path = SHOPS / f"{kind}.json"
    existing, _ = load(path)
    old = (existing or {}).get("variants") or []
    variants = [v for v in old if v.get("authored")] + ([] if regenerate else [v for v in old if not v.get("authored")])
    attempt = 0
    while len(variants) < count and attempt < count * 40:
        v = generate_one(kind, random.Random(stable_seed(kind, seed, attempt)))
        attempt += 1
        if v:
            v["name"] = f"generated {seed}.{attempt}"
            variants.append(v)
    SHOPS.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps({"kind": kind, "variants": variants}, indent=1) + "\n", encoding="utf-8")
    n_auth = sum(1 for v in variants if v.get("authored"))
    print(f"{kind}: {len(variants)} variant(s), {n_auth} authored ({attempt} tries)")


def validate(files, kind_override=None):
    errors = []
    for path in files:
        data, err = load(path)
        rel = path.relative_to(ROOT).as_posix()
        if err:
            errors.append(f"{rel}: {err}")
            continue
        kind = kind_override or data.get("kind") or path.stem
        if kind not in KINDS:
            errors.append(f"{rel}: kind must be one of {KINDS} (pass --kind for a per-location file)")
            continue
        vs = data.get("variants") or []
        if not vs:
            errors.append(f"{rel}: no variants")
        for k, v in enumerate(vs):
            for p in problems(v, kind):
                errors.append(f"{rel} variant {k}: {p}")
    for e in errors:
        print(f"error: {e}")
    print(f"{len(files)} file(s): {len(errors)} error(s)")
    return not errors


def preview(kind_or_file, k):
    from PIL import Image, ImageDraw
    path = SHOPS / f"{kind_or_file}.json"
    data, err = load(path)
    if err:
        sys.exit(f"{path}: {err}")
    kind = data.get("kind") or kind_or_file
    v = data["variants"][k]
    S = 32
    W, H = v["width"], v["height"]
    img = Image.new("RGB", (int((W + 4) * S), int((H + 6) * S)), (20, 20, 24))
    d = ImageDraw.Draw(img, "RGBA")

    def px(x, y):
        return ((x + W / 2 + 2) * S, (H / 2 + 2 - y) * S)

    def box(x, y, w, h, rot, fill, outline=(0, 0, 0, 255), label=None):
        poly = [px(*c) for c in corners(x, y, w, h, rot)]
        d.polygon(poly, fill=fill, outline=outline)
        if label:
            d.text((poly[3][0] + 2, poly[3][1] + 2), label, fill=(255, 255, 255, 255))

    box(0, 0, W, H, 0, (70, 72, 78, 255))
    box(v["doorX"], -H / 2, DOOR_W, 0.6, 0, (120, 90, 50, 255), label="door")
    bw, bh = bay_size(kind)
    for b in v["bays"]:
        box(b["x"], b["y"], bw, bh, b["rotation"], (90, 160, 90, 200) if b["state"] == "finished" else (200, 140, 60, 200),
            label=b["state"])
    for p in v["props"]:
        box(p["x"], p["y"], p["width"], p["height"], p["rotation"], (120, 120, 160, 220), label=p["kind"])
    c = v["counter"]
    box(c["x"], c["y"], COUNTER_W, COUNTER_D, c.get("rotation", 0), (130, 95, 60, 255), label="counter")
    for w in v["workers"] + [v["clerk"]]:
        q = px(w["x"], w["y"])
        d.ellipse([q[0] - 8, q[1] - 8, q[0] + 8, q[1] + 8], fill=(255, 220, 90, 255))
    d.text((8, 8), f"{kind} variant {k}: {v.get('name', '')} — green finished bay, orange working, dots people",
           fill=(255, 255, 255, 255))
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    out = PREVIEWS / f"{kind_or_file}.v{k}.png"
    img.save(out)
    print(out)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("command", choices=["generate", "validate", "preview"])
    ap.add_argument("what", nargs="?", help="a kind (generate/preview) or file stem (preview/validate)")
    ap.add_argument("variant", nargs="?", type=int, default=0)
    ap.add_argument("--count", type=int, default=DEFAULT_COUNT)
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--regenerate", action="store_true")
    ap.add_argument("--kind", help="validate: the kind of a per-location file")
    a = ap.parse_args()
    if a.command == "generate":
        for kind in ([a.what] if a.what else KINDS):
            generate(kind, a.count, a.seed, a.regenerate)
    elif a.command == "preview":
        preview(a.what or "Fabricator", a.variant)
    else:
        files = [SHOPS / f"{a.what}.json"] if a.what else sorted(SHOPS.glob("*.json"))
        sys.exit(0 if validate(files, a.kind) else 1)


if __name__ == "__main__":
    main()
