"""Trace a speedway's centreline from its OUTER WALL, for venues whose raceway lines OSM never finished.

Daytona's `highway=raceway` mapping covers 3,368 m of a 4,023 m lap, so fetch_traces.py cannot find a ring
there. Its outer SAFER wall, though, is mapped as one closed `barrier=wall` way, and on an oval the racing
surface is a fixed width inside that wall. So: take the wall, resample it, offset it inward by half the
racing surface, and that is the centreline. Checked against USGS NAIP imagery (public domain): the 11 m
offset lands on the painted yellow line on the straights and in the banked turns.

    python -I Tools/trace_from_wall.py Daytona 407709771 11.0 2.5 --start=apex --pit=352067003 --preview [--osm=answer.json]

<wayId> may be a comma list of open ways, joined end to end into one ring. With --centre the ways are taken
as the CENTRELINE (a complete highway=raceway ring); with --inner as the INSIDE edge (pit wall / apron line),
offset outward. Martinsville's raceway line balloons 10-18 m wide of the real turns, but its inside walls are
mapped; the one gap (turn 3 exit) is filled by way -1, read off NAIP imagery, in Tools/osm/Martinsville.json:

    python -I Tools/trace_from_wall.py Martinsville 448514914,-1,448514913 15.0 0.526 --inner --osm=Tools/osm/Martinsville.json "--pit=402168723[17:20]" --start=36.634234,-79.852234 --preview

--osm may be given more than once. A --pit id written <id>[a:b] uses nodes a..b-1 of that way only (the game's
pit road is one straight; Martinsville's wraps the whole infield, so only its front-stretch run is used).

--start=apex puts the start/finish line at the tri-oval apex; --start=lat,lon puts it nearest that point.
--pit=<wayId>[,<wayId>...] stores the mapped pit lane (joined end to end, turned to run the way the cars do);
the importer lays pit road along it instead of guessing.
Without it the trace starts wherever the mapper began and the importer starts the lap at the longest straight.

writes Assets/TrackTraces/<id>.json in the same format as fetch_traces.py, and with --preview an overview
PNG (wall red, centre green, inside edge yellow, apron edge cyan) to Temp/<id>_wall_trace.png.

Offsets are horizontal map distances, which is what a top-down game wants; a banked surface is wider than
it looks from above, but the yellow line and the wall are where they are.

Data (c) OpenStreetMap contributors, ODbL. Imagery: USGS NAIP, public domain.
"""
import json, math, os, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MIRRORS = ['https://overpass-api.de/api/interpreter', 'https://overpass.kumi.systems/api/interpreter']
ATTRIBUTION = 'Data (c) OpenStreetMap contributors, ODbL (opendatacommons.org/licenses/odbl)'
APRON_M = 3.5


def fetch_way(way_id):
    # --osm=<file>: read the way from an Overpass answer already on disk (Overpass rate-limits repeat asks).
    els = cached_elements()
    if els is not None:
        if way_id in els:
            return els[way_id]
        sys.exit('way %d is not in the --osm file(s)' % way_id)
    query = '[out:json][timeout:60];way(%d);out tags geom;' % way_id
    for url in MIRRORS:
        p = subprocess.run(['curl', '-s', '--max-time', '90', '-G', url, '--data-urlencode', 'data=' + query],
                           capture_output=True)
        if p.stdout[:1] == b'{':
            els = json.loads(p.stdout)['elements']
            if els:
                return els[0]
    sys.exit('Overpass did not return way %d' % way_id)


def resample_closed(pts, step):
    n = len(pts)
    seg = [(pts[(i + 1) % n][0] - pts[i][0], pts[(i + 1) % n][1] - pts[i][1]) for i in range(n)]
    lens = [math.hypot(dx, dy) for dx, dy in seg]
    out, i, at, total = [], 0, 0.0, sum(lens)
    walked = 0.0
    s = 0.0
    while s < total:
        while walked + lens[i] < s:
            walked += lens[i]
            i += 1
        t = (s - walked) / lens[i] if lens[i] else 0.0
        out.append((pts[i][0] + t * seg[i][0], pts[i][1] + t * seg[i][1]))
        s += step
    return out


def smooth_closed(pts, passes):
    n = len(pts)
    for _ in range(passes):
        pts = [(0.25 * pts[i - 1][0] + 0.5 * pts[i][0] + 0.25 * pts[(i + 1) % n][0],
                0.25 * pts[i - 1][1] + 0.5 * pts[i][1] + 0.25 * pts[(i + 1) % n][1]) for i in range(n)]
    return pts


def offset_closed(pts, metres):
    """Positive = toward the inside of the loop, whichever way the wall was drawn."""
    n = len(pts)
    area = sum(pts[i][0] * pts[(i + 1) % n][1] - pts[(i + 1) % n][0] * pts[i][1] for i in range(n)) / 2
    side = 1.0 if area > 0 else -1.0
    out = []
    for i in range(n):
        tx = pts[(i + 1) % n][0] - pts[i - 1][0]
        ty = pts[(i + 1) % n][1] - pts[i - 1][1]
        l = math.hypot(tx, ty) or 1.0
        out.append((pts[i][0] - ty / l * side * metres, pts[i][1] + tx / l * side * metres))
    return out


def signed_area(pts):
    n = len(pts)
    return sum(pts[i][0] * pts[(i + 1) % n][1] - pts[(i + 1) % n][0] * pts[i][1] for i in range(n)) / 2


def tri_oval_apex(pts):
    """Index of the start/finish line on a tri-oval: the point of the front stretch farthest from the back
    stretch. The back stretch is the longest run with no real curvature (radius over 2 km)."""
    n = len(pts)
    straight = []
    for i in range(n):
        a, b, c = pts[i - 5], pts[i], pts[(i + 5) % n]
        cross = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
        ab = math.hypot(b[0] - a[0], b[1] - a[1])
        bc = math.hypot(c[0] - b[0], c[1] - b[1])
        ca = math.hypot(a[0] - c[0], a[1] - c[1])
        straight.append(abs(cross) < 1e-9 or ab * bc * ca / (2 * abs(cross)) > 2000)
    best, run, start, best_start = 0, 0, 0, 0
    for i in range(2 * n):                     # twice round, so a run across the seam counts whole
        if straight[i % n]:
            if run == 0: start = i
            run += 1
            if run > best and run < n: best, best_start = run, start
        else:
            run = 0
    p, q = pts[best_start % n], pts[(best_start + best - 1) % n]
    dx, dy = q[0] - p[0], q[1] - p[1]
    l = math.hypot(dx, dy) or 1.0
    return max(range(n), key=lambda i: abs((pts[i][0] - p[0]) * dy - (pts[i][1] - p[1]) * dx) / l)


def cached_elements():
    """Elements from every --osm=<file> given (merged; a later file wins), or None for none."""
    els = None
    for a in sys.argv:
        if a.startswith('--osm='):
            els = els or {}
            els.update({e['id']: e for e in json.load(open(a[6:]))['elements']})
    return els


def fetch_ways(ids):
    """Ways from --osm=<file> if given, else from Overpass. An id written <id>[a:b] keeps nodes a..b-1."""
    out = []
    for i in ids:
        sl = None
        if isinstance(i, str) and '[' in i:
            i, sl = i.split('[')
            lo, hi = sl.rstrip(']').split(':')
            sl = slice(int(lo) if lo else None, int(hi) if hi else None)
        w = dict(fetch_way(int(i)))
        if sl:
            w['geometry'] = w['geometry'][sl]
        out.append(w)
    return out


def join_ways(ways):
    """Chain ways end to end into one polyline, flipping any that were drawn the other way."""
    line = list(ways[0]['geometry'])
    for w in ways[1:]:
        g = list(w['geometry'])
        ends = [(line[-1], g[0], False, False), (line[-1], g[-1], False, True),
                (line[0], g[-1], True, False), (line[0], g[0], True, True)]
        d = lambda a, b: (a['lat'] - b['lat']) ** 2 + (a['lon'] - b['lon']) ** 2
        _, _, prepend, flip = min(ends, key=lambda e: d(e[0], e[1]))
        if flip: g.reverse()
        line = g + line if prepend else line + g
    return line


def length_closed(pts):
    n = len(pts)
    return sum(math.hypot(pts[(i + 1) % n][0] - pts[i][0], pts[(i + 1) % n][1] - pts[i][1]) for i in range(n))


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    if len(args) < 4:
        sys.exit(__doc__)
    track_id, width, miles = args[0], float(args[2]), float(args[3])
    way_ids = [int(v) for v in args[1].split(',')]
    way_id = way_ids[0]
    centre_mode = '--centre' in sys.argv
    inner_mode = '--inner' in sys.argv

    if len(way_ids) == 1:
        way = fetch_way(way_id)
        geom = way['geometry']
    else:
        ways = fetch_ways(way_ids)
        way = ways[0]
        geom = join_ways(ways)
    lat0 = sum(p['lat'] for p in geom) / len(geom)
    lon0 = sum(p['lon'] for p in geom) / len(geom)
    kx, ky = 111320 * math.cos(math.radians(lat0)), 110950
    wall = [((p['lon'] - lon0) * kx, (p['lat'] - lat0) * ky) for p in geom]
    if wall[0] == wall[-1]:
        wall = wall[:-1]

    wall = smooth_closed(resample_closed(wall, 2.0), 20)   # keeps straights straight, rounds node corners
    if signed_area(wall) < 0:
        wall.reverse()                                     # counter-clockwise: the way the cars run
    if centre_mode:
        # The ways ARE the centreline (a complete highway=raceway ring): the wall is half a surface outside.
        centre = wall
        wall = offset_closed(centre, -width / 2)
    elif inner_mode:
        # The ways are the INSIDE edge (pit wall / apron line): the centre is half a surface outside it, the
        # outer wall a whole surface.
        inner = wall
        centre = offset_closed(inner, -width / 2)
        wall = offset_closed(inner, -width)
    else:
        centre = offset_closed(wall, width / 2)

    # Start the trace on the start/finish line, so the importer can keep the lap starting there.
    start = None
    for a in sys.argv:
        if a.startswith('--start='):
            start = a[8:]
    if start == 'apex':
        k = tri_oval_apex(centre)
    elif start:
        slat, slon = (float(v) for v in start.split(','))
        sx, sy = (slon - lon0) * kx, (slat - lat0) * ky
        k = min(range(len(centre)), key=lambda i: (centre[i][0] - sx) ** 2 + (centre[i][1] - sy) ** 2)
    else:
        k = 0
    centre = centre[k:] + centre[:k]
    wall = wall[k:] + wall[:k]
    published = miles * 1609.344
    traced = length_closed(centre)
    print('%s: wall %.1f m, centre %.1f m (%.2f m in), published %.1f m, %+.2f%%'
          % (track_id, length_closed(wall), traced, width / 2, published, (traced / published - 1) * 100))

    geo = [{'lat': round(lat0 + y / ky, 7), 'lon': round(lon0 + x / kx, 7)} for x, y in centre[::2]]
    geo.append(geo[0])
    out = {
        'trackId': track_id,
        'osmWayId': way_id,
        'osmName': way.get('tags', {}).get('name', '') or '%s outer wall' % track_id,
        'foundBy': ('inside edge (ways %s, joined) offset %.2f m outward to the centre of a %.1f m racing '
                    'surface; checked against USGS NAIP imagery' % (','.join(str(i) for i in way_ids), width / 2, width))
                   if inner_mode else
                   ('highway=raceway ring (ways %s) taken as the centreline of a %.1f m racing surface; '
                    'checked against USGS NAIP imagery' % (','.join(str(i) for i in way_ids), width))
                   if centre_mode else
                   'outer wall (closed barrier=wall way %d) offset %.2f m inward to the centre of a %.1f m '
                   'racing surface; checked against USGS NAIP imagery' % (way_id, width / 2, width),
        'publishedMiles': miles,
        'startFinish': 'firstNode' if start else '',
        'segmentation': 'curvature',   # a precise trace: the importer follows it closely, not one arc per corner
        'tracedMetres': round(traced, 1),
        'surfaceWidthMetres': width,
        'apronWidthMetres': APRON_M,
        'attribution': ATTRIBUTION,
        'geometry': geo,
    }
    pit_ids = None
    for a in sys.argv:
        if a.startswith('--pit='):
            pit_ids = [v if '[' in v else int(v) for v in a[6:].split(',')]
    if pit_ids:
        lane = join_ways(fetch_ways(pit_ids))
        pts = [((p['lon'] - lon0) * kx, (p['lat'] - lat0) * ky) for p in lane]
        # Run it the way the cars do: along the racing line's direction where it is nearest.
        mid = pts[len(pts) // 2]
        i = min(range(len(centre)), key=lambda k: (centre[k][0] - mid[0]) ** 2 + (centre[k][1] - mid[1]) ** 2)
        tx, ty = centre[(i + 1) % len(centre)][0] - centre[i][0], centre[(i + 1) % len(centre)][1] - centre[i][1]
        if (pts[-1][0] - pts[0][0]) * tx + (pts[-1][1] - pts[0][1]) * ty < 0:
            lane.reverse()
        out['pitLane'] = [{'lat': p['lat'], 'lon': p['lon']} for p in lane]
        out['pitLaneWays'] = pit_ids
        print('pit lane: %d nodes from way(s) %s' % (len(lane), pit_ids))

    path = os.path.join(ROOT, 'Assets', 'TrackTraces', track_id + '.json')
    with open(path, 'w') as f:
        json.dump(out, f, indent=1)
    print('wrote', path)

    if '--preview' in sys.argv:
        preview(track_id, lat0, lon0, kx, ky, wall, width)


def preview(track_id, lat0, lon0, kx, ky, wall, width):
    from PIL import Image, ImageDraw
    import urllib.request
    xs, ys = [p[0] for p in wall], [p[1] for p in wall]
    pad = 60
    b = (lon0 + (min(xs) - pad) / kx, lat0 + (min(ys) - pad) / ky,
         lon0 + (max(xs) + pad) / kx, lat0 + (max(ys) + pad) / ky)
    url = ('https://imagery.nationalmap.gov/arcgis/rest/services/USGSNAIPImagery/ImageServer/exportImage?'
           'bbox=%.6f,%.6f,%.6f,%.6f&bboxSR=4326&imageSR=4326&size=2000,2000&format=png&f=image' % b)
    os.makedirs(os.path.join(ROOT, 'Temp'), exist_ok=True)
    path = os.path.join(ROOT, 'Temp', '%s_wall_trace.png' % track_id)
    urllib.request.urlretrieve(url, path)
    im = Image.open(path).convert('RGB')
    dr = ImageDraw.Draw(im)
    w, h = im.size
    for line, col in [(wall, (255, 0, 0)), (offset_closed(wall, width / 2), (0, 255, 0)),
                      (offset_closed(wall, width), (255, 255, 0)), (offset_closed(wall, width + APRON_M), (0, 200, 255))]:
        pts = [(((lon0 + x / kx) - b[0]) / (b[2] - b[0]) * w, (b[3] - (lat0 + y / ky)) / (b[3] - b[1]) * h)
               for x, y in line]
        dr.line(pts + [pts[0]], fill=col, width=2)
    sx, sy = offset_closed(wall, width / 2)[0]
    px_, py_ = (((lon0 + sx / kx) - b[0]) / (b[2] - b[0]) * w, (b[3] - (lat0 + sy / ky)) / (b[3] - b[1]) * h)
    dr.ellipse([px_ - 12, py_ - 12, px_ + 12, py_ + 12], outline=(255, 0, 255), width=4)   # start/finish
    im.save(path)
    print('preview', path)


if __name__ == '__main__':
    main()
