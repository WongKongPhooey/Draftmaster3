"""Check and preview paddock sidecars (Assets/Resources/Paddocks/<TrackId>.json) without Unity.

    python Tools/paddock_sidecar.py validate             # every sidecar; exit 1 on any error
    python Tools/paddock_sidecar.py validate Dover       # just one
    python Tools/paddock_sidecar.py preview Dover        # PNG -> Temp/PaddockPreview/Dover.png
    python Tools/paddock_sidecar.py preview Dover --variant 2
    python Tools/paddock_generate.py Dover               # randomised layouts (variants) from the geometry
    python Tools/paddock_sidecar.py new Dover            # print an empty sidecar to start from

A sidecar places WeekendMarker venues (`*_Marker`) and PaddockLotArea footprints for one track, in
package-local metres (see Assets/Scripts/Tracks/PaddockSidecar.cs). Geometry to place them against comes
from Tools/TrackExports/<TrackId>.json, written in Unity by Draftmaster > Tracks > Export Geometry For
Paddock Layout: the centreline and pit lane with widths, the start line, and every object already in the
package. Workflow: Docs/Tracks.md, "Paddock sidecars".

Errors: bad shape, unknown venue word, a lot on the racing surface or the pit lane, two lots of one kind,
a marker centred on the racing surface. Warnings: a lot too small for a full field, overlap with an
existing package object, an entry the package already authors (the package wins, so it is ignored).
Needs Pillow for `preview` only.
"""
import argparse
import json
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SIDECARS = ROOT / "Assets" / "Resources" / "Paddocks"
EXPORTS = ROOT / "Tools" / "TrackExports"
PACKAGES = ROOT / "Assets" / "Resources" / "TrackPackages"
PREVIEWS = ROOT / "Temp" / "PaddockPreview"

# WeekendMarkerNames.VenueFromName: the words before "_Marker", compared on letters and digits only.
VENUE_WORDS = {
    "PitBox": ["pitbox", "box"],
    "Motorhome": ["motorhome", "rv"],
    "MeetingRoom": ["driversroom", "meetingroom", "meeting"],
    "SigningFence": ["signingfence", "signing", "fanfence", "fence", "autographs"],
    "SponsorSuite": ["sponsorsuite", "winnerscircle", "winnercircle", "hospitality", "sponsor", "suite",
                     "photoshoot", "photo"],
    "IntroStage": ["introstage", "stage", "intros"],
    "Grandstand": ["grandstand", "stand", "spectate"],
}
LOT_KINDS = ("Motorhomes", "Garages")
FIELD = 43                      # cars in a full field
RV = (3.95, 9.93)               # PaddockLotArea.DefaultRvWidth / DefaultRvLength
GARAGE = (3.95 + 6.5, 9.93)     # body + canopy across, body long
ROAD_MARGIN = 3.0               # clear air between a lot and the road edge
BIG_OBJECT_M2 = 20000.0         # ground planes and the like: not obstacles


def simplify(s):
    return "".join(c for c in s.lower() if c.isalnum())


def venue_of(name):
    if not name or not name.lower().endswith("_marker"):
        return None
    key = simplify(name[: -len("_Marker")])
    for venue, words in VENUE_WORDS.items():
        if key in words:
            return venue
    return None


def rect(x, y, w, h, rot):
    """Corners of a rotated rectangle."""
    c, s = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    out = []
    for dx, dy in ((-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)):
        out.append((x + dx * c - dy * s, y + dx * s + dy * c))
    return out


def point_rect_distance(px, py, x, y, w, h, rot):
    """Distance from a point to a rotated rectangle (0 inside)."""
    c, s = math.cos(math.radians(-rot)), math.sin(math.radians(-rot))
    lx, ly = (px - x) * c - (py - y) * s, (px - x) * s + (py - y) * c
    dx, dy = max(abs(lx) - w / 2, 0.0), max(abs(ly) - h / 2, 0.0)
    return math.hypot(dx, dy)


def rects_overlap(a, b):
    """Separating-axis test for two convex quads."""
    for poly in (a, b):
        for i in range(4):
            x1, y1 = poly[i]
            x2, y2 = poly[(i + 1) % 4]
            nx, ny = y1 - y2, x2 - x1
            pa = [nx * px + ny * py for px, py in a]
            pb = [nx * px + ny * py for px, py in b]
            if max(pa) < min(pb) or max(pb) < min(pa):
                return False
    return True


def load(path):
    try:
        return json.loads(path.read_text(encoding="utf-8")), None
    except FileNotFoundError:
        return None, "missing"
    except ValueError as e:
        return None, f"not valid JSON ({e})"


def capacity(kind, w, h, gap, row_gap):
    across, deep = RV if kind == "Motorhomes" else GARAGE
    per_line = int((w + gap) // (across + gap)) if w >= across else 0
    lines = int((h + row_gap) // (deep + row_gap)) if h >= deep else 0
    return per_line * lines


def validate(track_id, errors, warnings):
    path = SIDECARS / f"{track_id}.json"
    rel = path.relative_to(ROOT).as_posix()
    data, err = load(path)
    if err:
        errors.append(f"{rel}: {err}")
        return
    if not isinstance(data, dict):
        errors.append(f"{rel}: must be an object")
        return
    if data.get("trackId") != track_id:
        errors.append(f"{rel}: trackId must be '{track_id}' (the file name)")
    if not (PACKAGES / f"{track_id}.prefab").exists():
        errors.append(f"{rel}: no package Resources/TrackPackages/{track_id}.prefab")
    export, xerr = load(EXPORTS / f"{track_id}.json")
    if xerr:
        warnings.append(f"{rel}: no geometry export (Tools/TrackExports/{track_id}.json: {xerr}); "
                        "road and overlap checks skipped")
        export = None
    road = [(p[0], p[1], p[2]) for p in (export or {}).get("centerline", [])]
    pit = [(p[0], p[1], p[2]) for p in (export or {}).get("pitLane", [])]
    objects = [o for o in (export or {}).get("objects", [])
               if o.get("kind") == "object" and o["width"] * o["height"] < BIG_OBJECT_M2]
    authored_names = {simplify(o["name"]) for o in (export or {}).get("objects", []) if o.get("kind") == "marker"}
    authored_lots = {o.get("lotKind") for o in (export or {}).get("objects", []) if o.get("kind") == "lot"}

    def num(v, lo, hi):
        return isinstance(v, (int, float)) and lo <= v <= hi

    def check_layout(layout, rel):
        boxes = []
        for i, m in enumerate(layout.get("markers") or []):
            where = f"{rel} marker {i} ({(m or {}).get('name', '?')})"
            if not isinstance(m, dict):
                errors.append(f"{where}: not an object")
                continue
            if not venue_of(m.get("name", "")):
                errors.append(f"{where}: name must be '<venue word>_Marker'; venue words: "
                              + ", ".join(w for ws in VENUE_WORDS.values() for w in ws[:1]))
                continue
            if not (num(m.get("x"), -1e5, 1e5) and num(m.get("y"), -1e5, 1e5)):
                errors.append(f"{where}: x and y must be numbers")
                continue
            w, h, rot = m.get("width", 6), m.get("height", 6), m.get("rotation", 0)
            if not (num(w, 0.5, 80) and num(h, 0.5, 80) and num(rot, -360, 360)):
                errors.append(f"{where}: width/height 0.5-80 m, rotation -360..360")
                continue
            for sub in ("seat", "view"):
                p = m.get(sub)
                if p is not None and not (isinstance(p, dict) and num(p.get("x"), -1e5, 1e5) and num(p.get("y"), -1e5, 1e5)):
                    errors.append(f"{where}: {sub} must be {{\"x\": .., \"y\": ..}}")
            if simplify(m["name"]) in authored_names:
                warnings.append(f"{where}: the package already has this marker; the package's wins and this is ignored")
            for px, py, rw in road:
                if point_rect_distance(px, py, m["x"], m["y"], 0, 0, 0) < rw / 2:
                    errors.append(f"{where}: centred on the racing surface")
                    break
            boxes.append((where, rect(m["x"], m["y"], w, h, rot)))

        kinds = set()
        for i, lot in enumerate(layout.get("lots") or []):
            where = f"{rel} lot {i} ({(lot or {}).get('kind', '?')})"
            if not isinstance(lot, dict):
                errors.append(f"{where}: not an object")
                continue
            kind = lot.get("kind")
            if kind not in LOT_KINDS:
                errors.append(f"{where}: kind must be one of {LOT_KINDS}")
                continue
            if kind in kinds:
                errors.append(f"{where}: a second {kind} lot (the game uses the first per kind)")
            kinds.add(kind)
            x, y, w, h = lot.get("x"), lot.get("y"), lot.get("width"), lot.get("height")
            rot, gap, row_gap = lot.get("rotation", 0), lot.get("gap", 2), lot.get("rowGap", 6)
            if not (num(x, -1e5, 1e5) and num(y, -1e5, 1e5) and num(w, 5, 600) and num(h, 5, 600)
                    and num(rot, -360, 360) and num(gap, 0.5, 20) and num(row_gap, 2, 40)):
                errors.append(f"{where}: needs x, y, width/height 5-600 m, rotation, gap 0.5-20, rowGap 2-40")
                continue
            if kind in authored_lots:
                warnings.append(f"{where}: the package already has a {kind} lot; the package's wins and this is ignored")
            cap = capacity(kind, w, h, gap, row_gap)
            if cap < FIELD:
                warnings.append(f"{where}: holds ~{cap} rigs at this spacing, a full field is {FIELD} "
                                "(the game squeezes them along the lines)")
            for label, line, margin in (("racing surface", road, ROAD_MARGIN), ("pit lane", pit, 1.0)):
                for px, py, rw in line:
                    if point_rect_distance(px, py, x, y, w, h, rot) < rw / 2 + margin:
                        errors.append(f"{where}: overlaps the {label} near ({px:.0f}, {py:.0f})")
                        break
            boxes.append((where, rect(x, y, w, h, rot)))

        for where, box in boxes:
            for o in objects:
                if rects_overlap(box, rect(o["x"], o["y"], o["width"], o["height"], o.get("rotation", 0))):
                    warnings.append(f"{where}: overlaps package object '{o['path']}'")
                    break
        # Pieces of one layout must not overlap each other either.
        for i in range(len(boxes)):
            for j in range(i + 1, len(boxes)):
                if rects_overlap(boxes[i][1], boxes[j][1]):
                    errors.append(f"{boxes[i][0]}: overlaps {boxes[j][0].split(rel)[-1].strip()}")

    check_layout(data, rel)
    variants = data.get("variants") or []
    if not isinstance(variants, list):
        errors.append(f"{rel}: variants must be a list of {{markers, lots}}")
        variants = []
    for k, v in enumerate(variants):
        if not isinstance(v, dict):
            errors.append(f"{rel} variant {k}: not an object")
            continue
        check_layout(v, f"{rel} variant {k}")
        if not any((m or {}).get("name") for m in v.get("markers") or []) or not v.get("lots"):
            warnings.append(f"{rel} variant {k}: has no markers or no lots")


def preview(track_id, variant=None):
    """Render a sidecar (or one of its variants) over the track. Returns the PNG path."""
    try:
        from PIL import Image, ImageDraw
    except ImportError:
        sys.exit("preview needs Pillow: pip install pillow")
    export, xerr = load(EXPORTS / f"{track_id}.json")
    if xerr:
        sys.exit(f"Tools/TrackExports/{track_id}.json: {xerr} — export it from Unity first")
    side, _ = load(SIDECARS / f"{track_id}.json")
    side = side or {}
    if variant is not None:
        vs = side.get("variants") or []
        if not 0 <= variant < len(vs):
            sys.exit(f"{track_id} has {len(vs)} variant(s); no variant {variant}")
        side = vs[variant]

    pts = [(p[0], p[1]) for p in export["centerline"] + export["pitLane"]]
    for m in (side.get("markers") or []) + (side.get("lots") or []):
        if isinstance(m, dict) and "x" in m:
            pts.append((m["x"], m["y"]))
    objs = [o for o in export["objects"] if o["width"] * o["height"] < BIG_OBJECT_M2]
    for o in objs:
        pts.append((o["x"], o["y"]))
    minx, maxx = min(p[0] for p in pts) - 60, max(p[0] for p in pts) + 60
    miny, maxy = min(p[1] for p in pts) - 60, max(p[1] for p in pts) + 60
    size = 1600
    scale = size / max(maxx - minx, maxy - miny)
    W, H = int((maxx - minx) * scale), int((maxy - miny) * scale)
    img = Image.new("RGB", (W, H), (34, 52, 34))
    d = ImageDraw.Draw(img, "RGBA")

    def px(x, y):
        return ((x - minx) * scale, (maxy - y) * scale)

    def line(samples, colour, closed):
        if len(samples) < 2:
            return
        seq = samples + ([samples[0]] if closed else [])
        width = max(2, int(sum(s[2] for s in samples) / len(samples) * scale))
        d.line([px(s[0], s[1]) for s in seq], fill=colour, width=width, joint="curve")

    line([tuple(p) for p in export["centerline"]], (90, 90, 95, 255), export.get("closedLoop", True))
    line([tuple(p) for p in export["pitLane"]], (120, 120, 125, 255), False)
    sx, sy = export["startLine"]
    d.ellipse([px(sx, sy)[0] - 5, px(sx, sy)[1] - 5, px(sx, sy)[0] + 5, px(sx, sy)[1] + 5], fill=(255, 255, 255))
    for o in export["objects"]:
        if o["width"] * o["height"] >= BIG_OBJECT_M2:
            continue
        poly = [px(*c) for c in rect(o["x"], o["y"], o["width"], o["height"], o.get("rotation", 0))]
        colour = {"marker": (255, 170, 40), "lot": (60, 200, 255)}.get(o["kind"], (170, 170, 170))
        d.polygon(poly, outline=colour + (255,), fill=colour + (40,))
        if o["kind"] != "object":
            d.text(poly[0], o["name"], fill=colour + (255,))
    for m in side.get("markers") or []:
        if not isinstance(m, dict) or "x" not in m:
            continue
        poly = [px(*c) for c in rect(m["x"], m["y"], m.get("width", 6), m.get("height", 6), m.get("rotation", 0))]
        d.polygon(poly, outline=(255, 120, 0, 255), fill=(255, 120, 0, 110))
        d.text((poly[2][0] + 3, poly[2][1]), m.get("name", "?"), fill=(255, 220, 160, 255))
        for sub, col in (("seat", (255, 255, 0)), ("view", (255, 0, 255))):
            p = m.get(sub)
            if isinstance(p, dict) and "x" in p:
                q = px(p["x"], p["y"])
                d.ellipse([q[0] - 4, q[1] - 4, q[0] + 4, q[1] + 4], outline=col + (255,))
    for lot in side.get("lots") or []:
        if not isinstance(lot, dict) or "x" not in lot:
            continue
        poly = [px(*c) for c in rect(lot["x"], lot["y"], lot.get("width", 0), lot.get("height", 0), lot.get("rotation", 0))]
        d.polygon(poly, outline=(40, 140, 255, 255), fill=(40, 140, 255, 90))
        d.text((poly[0][0] + 3, poly[0][1] + 3), f"{lot.get('kind')} lot", fill=(200, 230, 255, 255))
    # 100 m scale bar
    d.line([(20, H - 20), (20 + 100 * scale, H - 20)], fill=(255, 255, 255), width=3)
    d.text((20, H - 38), "100 m", fill=(255, 255, 255))
    d.text((20, 12), f"{export.get('displayName', track_id)} — grey road, lighter pit lane, white dot = start line; "
                     "orange = markers, blue = lots (filled = sidecar, outline = in the package)", fill=(255, 255, 255))
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    out = PREVIEWS / (f"{track_id}.png" if variant is None else f"{track_id}.v{variant}.png")
    img.save(out)
    print(out)
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("command", choices=["validate", "preview", "new"])
    ap.add_argument("track", nargs="?")
    ap.add_argument("--variant", type=int, help="preview: draw this variant instead of the top-level layout")
    a = ap.parse_args()
    if a.command == "preview":
        if not a.track:
            sys.exit("preview needs a track id")
        preview(a.track, a.variant)
        return
    if a.command == "new":
        print(json.dumps({"trackId": a.track or "TrackId", "markers": [
            {"name": "Grandstand_Marker", "x": 0, "y": 0, "rotation": 0, "width": 8, "height": 6,
             "seat": {"x": 0, "y": 0}}], "lots": [
            {"kind": "Motorhomes", "x": 0, "y": 0, "rotation": 0, "width": 160, "height": 60,
             "gap": 2, "rowGap": 6}]}, indent=2))
        return
    ids = [a.track] if a.track else sorted(p.stem for p in SIDECARS.glob("*.json"))
    errors, warnings = [], []
    for tid in ids:
        validate(tid, errors, warnings)
    for w in warnings:
        print(f"warning: {w}")
    for e in errors:
        print(f"error: {e}")
    print(f"{len(ids)} sidecar(s): {len(errors)} error(s), {len(warnings)} warning(s)")
    sys.exit(1 if errors else 0)


if __name__ == "__main__":
    main()
