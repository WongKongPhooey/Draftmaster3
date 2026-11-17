"""Track reference pack: everything needed to build a venue accurately, gathered in one command.

    python -I Tools/track_reference.py Daytona               # one venue (cached after the first run)
    python -I Tools/track_reference.py --all                 # every venue in Tools/TrackExports, + STATUS.md
    python -I Tools/track_reference.py Phoenix --refresh     # ignore the cache, fetch everything again
    python -I Tools/track_reference.py Phoenix --offline     # re-render from the cache, no network
    python -I Tools/track_reference.py Phoenix --write-trace=2 [--start=apex|lat,lon] [--pit=<wayId>] [--force]
    python -I Tools/track_reference.py --selftest            # the fitting maths on a synthetic lap

For one venue it collects, into Tools/TrackReference/<id>/:

  facts       Wikipedia's infobox (every layout: length, turns, banking, surface, coordinates) and the
              layout that matches the game's published lap, set against what the game uses.
  osm         every mapped raceway, wall, barrier, grandstand, building, car park, road and pond round the
              venue (one Overpass query, cached).
  aerial      USGS NAIP orthoimagery (public domain, US only) in Web Mercator, so pixels are square in
              ground metres; the exact ground resolution is written next to it.
  track map   the Wikipedia infobox's layout diagram, from Wikimedia Commons, with its licence.
  candidates  every closed ring OSM can make near the published lap length - raceway lines taken as the
              centre, walls taken as the outer edge or the inside edge and offset by half the game's
              racing surface - each scored by how far its lap is from the published one, and by whether it
              crosses itself. The committed trace (Assets/TrackTraces/<id>.json) is scored the same way.
  built fit   the game's own centreline (Tools/TrackExports/<id>.json) fitted onto the best candidate by
              rotation, translation and a uniform scale, then measured point by point: RMS / 95th
              percentile / worst deviation in metres, and the three stretches of the lap (in metres from
              its seam) that are furthest out. This is the number that says whether a track is right.
  overview    the aerial with all of the above drawn over it, for a person (or Claude) to look at.
  alignment   where the aerial sits in the package's own metres, for Unity's
              `Draftmaster > Tracks > Reference > Open Aerial Overlay For Selected Track`.

Committed: facts.json, candidates.json (no geometry), report.md, and STATUS.md for --all. Everything big
or not ours to redistribute (OSM answer, imagery, Commons map, overview renders, candidate geometry) goes in
Tools/TrackReference/<id>/cache/, which git ignores.

--write-trace=<n> writes candidate n as Assets/TrackTraces/<id>.json in the same format as
trace_from_wall.py (curvature segmentation, optional start/finish and mapped pit lane), ready for
`Draftmaster > Tracks > Import Traced Geometry`. Hand-built venues (WatkinsGlen, Daytona, FortWorth,
Martinsville) are refused without --force.

The game's centreline comes from the committed export; after changing a track in Unity, re-run
`Draftmaster > Tracks > Export Geometry For Paddock Layout (Selected Track)` before measuring it again.

Data (c) OpenStreetMap contributors, ODbL. Imagery: USGS NAIP, public domain. Facts: Wikipedia, CC BY-SA.
"""
import json, math, os, re, subprocess, sys, time, urllib.parse

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fetch_traces as ft        # noqa: E402  ring walking (rings), venue coordinates (VENUES)
import trace_from_wall as tw     # noqa: E402  resample / smooth / offset / tri-oval apex

ROOT = os.path.dirname(HERE)
OUT = os.path.join(HERE, 'TrackReference')
EXPORTS = os.path.join(HERE, 'TrackExports')
TRACES = os.path.join(ROOT, 'Assets', 'TrackTraces')
UA = 'Draftmaster3-track-reference/1.0 (offline game-asset reference tool)'
MILE = 1609.344
EARTH = 6378137.0
OVERPASS = ['https://overpass-api.de/api/interpreter', 'https://overpass.kumi.systems/api/interpreter',
            'https://overpass.private.coffee/api/interpreter']
NAIP = 'https://imagery.nationalmap.gov/arcgis/rest/services/USGSNAIPImagery/ImageServer/exportImage'
WIKI_API = 'https://en.wikipedia.org/w/api.php'
COMMONS_API = 'https://commons.wikimedia.org/w/api.php'
NL = chr(10)
HAND_BUILT = {'WatkinsGlen', 'Daytona', 'FortWorth', 'Martinsville'}

# Wikipedia article per track id where the game's display name is not the article title. The layout inside
# the article is chosen by lap length, so a road course sharing an oval's article finds its own row.
WIKI_TITLES = {
    'CharlotteRoval': 'Charlotte Motor Speedway',
    'IndyRoad': 'Indianapolis Motor Speedway',
    'MexicoCity': 'Autódromo Hermanos Rodríguez',
    'Miami': 'Homestead–Miami Speedway',
    'SanDiego': 'Naval Base Coronado',
    'LongPond': 'Pocono Raceway',
    'IRP': 'Lucas Oil Indianapolis Raceway Park',
}


# ----------------------------------------------------------------------------------------------- network

def http_get(url, binary=False, timeout=120):
    p = subprocess.run(['curl', '-s', '-L', '--max-time', str(timeout), '-A', UA, url], capture_output=True)
    if p.returncode != 0 or not p.stdout:
        return None
    return p.stdout if binary else p.stdout.decode('utf-8', 'replace')


def get_json(url, timeout=120):
    t = http_get(url, timeout=timeout)
    try:
        return json.loads(t) if t else None
    except ValueError:
        return None


def overpass(query, waits=(0, 20, 40, 80, 160, 300)):
    """The public Overpass servers turn work away when they are full ("at capacity", 502/504), sometimes for
    many minutes, so back off exponentially across the mirrors - about ten minutes in all - before giving up."""
    for wait in waits:
        time.sleep(wait)
        for url in OVERPASS:
            p = subprocess.run(['curl', '-s', '--max-time', '150', '-A', UA, '-G', url,
                                '--data-urlencode', 'data=' + query], capture_output=True)
            try:
                data = json.loads(p.stdout)
                if 'elements' in data and not data.get('remark', '').startswith('runtime error'):
                    return data
            except ValueError:
                pass
        print('  (Overpass busy, retrying in %ds)' % (wait * 2 or 20), flush=True)
    return None


# -------------------------------------------------------------------------------------------- geometry

class Frame:
    """Local east/north metres about a venue centre. Metres per degree from the WGS84 series, not a
    sphere, so a 2 km venue measures within a few centimetres of a proper projection."""

    def __init__(self, lat0, lon0):
        self.lat0, self.lon0 = lat0, lon0
        p = math.radians(lat0)
        self.ky = 111132.954 - 559.822 * math.cos(2 * p) + 1.175 * math.cos(4 * p)
        self.kx = 111412.84 * math.cos(p) - 93.5 * math.cos(3 * p) + 0.118 * math.cos(5 * p)

    def xy(self, lat, lon):
        return ((lon - self.lon0) * self.kx, (lat - self.lat0) * self.ky)

    def latlon(self, x, y):
        return (self.lat0 + y / self.ky, self.lon0 + x / self.kx)


def merc(lat, lon):
    return (EARTH * math.radians(lon), EARTH * math.log(math.tan(math.pi / 4 + math.radians(lat) / 2)))


def unmerc(mx, my):
    return (math.degrees(2 * math.atan(math.exp(my / EARTH)) - math.pi / 2), math.degrees(mx / EARTH))


def closed_length(pts):
    n = len(pts)
    return sum(math.hypot(pts[(i + 1) % n][0] - pts[i][0], pts[(i + 1) % n][1] - pts[i][1]) for i in range(n))


def self_intersects(pts, samples=300):
    step = max(1, len(pts) // samples)
    g = pts[::step]
    n = len(g)

    def side(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    for i in range(n):
        a, b = g[i], g[(i + 1) % n]
        for j in range(i + 2, n):
            if i == 0 and j == n - 1:
                continue
            c, d = g[j], g[(j + 1) % n]
            if (side(a, b, c) > 0) != (side(a, b, d) > 0) and (side(c, d, a) > 0) != (side(c, d, b) > 0):
                return True
    return False


def np():
    import numpy
    return numpy


def resample(pts, n):
    """n points evenly spaced by arc length round a closed loop."""
    N = np()
    P = N.asarray(pts, float)
    Q = N.vstack([P, P[:1]])
    seg = N.hypot(*N.diff(Q, axis=0).T)
    cum = N.concatenate([[0], N.cumsum(seg)])
    s = N.linspace(0, cum[-1], n, endpoint=False)
    return N.column_stack([N.interp(s, cum, Q[:, 0]), N.interp(s, cum, Q[:, 1])])


def umeyama(A, B, scale=True):
    """s, R, t minimising |s R A + t - B|^2 over a proper rotation (no mirror image)."""
    N = np()
    ma, mb = A.mean(0), B.mean(0)
    Ac, Bc = A - ma, B - mb
    U, S, Vt = N.linalg.svd(Bc.T @ Ac / len(A))
    D = N.diag([1.0, N.sign(N.linalg.det(U @ Vt)) or 1.0])
    R = U @ D @ Vt
    s = float(N.trace(N.diag(S) @ D) / ((Ac ** 2).sum() / len(A))) if scale else 1.0
    return s, R, mb - s * R @ ma


def nearest(P, Q, chunk=600):
    """Distance from every point of P to the nearest point of Q, and which point that was."""
    N = np()
    d = N.empty(len(P))
    k = N.empty(len(P), int)
    for i in range(0, len(P), chunk):
        D2 = ((P[i:i + chunk, None, :] - Q[None, :, :]) ** 2).sum(-1)
        k[i:i + chunk] = D2.argmin(1)
        d[i:i + chunk] = N.sqrt(D2[N.arange(len(D2)), k[i:i + chunk]])
    return d, k


def fit_shape(built, ref, scale=True):
    """Fit the game's closed centreline onto a reference one. Neither the start point nor the direction of
    the reference is known, so every cyclic shift in both directions is tried on arc-length pairs, and the
    best is refined by iterative closest point (pairs by distance, not by arc length, so a lap whose
    corners are in slightly the wrong place along it is not penalised twice). Returns (s, R, t)."""
    N = np()
    n = 360
    A = resample(built, n)
    best = None
    for B0 in (resample(ref, n), resample(ref[::-1], n)):
        for sh in range(0, n, 2):
            B = N.roll(B0, -sh, axis=0)
            s, R, t = umeyama(A, B, scale)
            err = (((s * A @ R.T + t) - B) ** 2).sum(1).mean()
            if best is None or err < best[0]:
                best = (err, s, R, t)
    _, s, R, t = best
    dense = resample(ref, max(400, int(closed_length(ref))))
    for _ in range(25):
        moved = s * A @ R.T + t
        _, k = nearest(moved, dense)
        s, R, t = umeyama(A, dense[k], scale)
    return s, R, t


def deviation(built, ref, s, R, t, lap_m):
    """Point-by-point distance of the fitted built line from the reference, plus the worst stretches."""
    N = np()
    nb = max(400, int(lap_m / 2))
    A = resample(built, nb)
    moved = s * A @ R.T + t
    dense = resample(ref, max(400, int(closed_length(ref))))
    d, _ = nearest(moved, dense)
    back, _ = nearest(dense[::2], moved)
    step = lap_m / nb
    win = max(3, int(100 / step))
    roll = N.convolve(N.concatenate([d, d[:win]]), N.ones(win) / win, 'valid')[:nb]
    worst, taken = [], N.zeros(nb, bool)
    for i in N.argsort(-roll):
        if len(worst) == 3:
            break
        if taken[i]:
            continue
        worst.append({'fromM': int(round(i * step)), 'toM': int(round((i + win) * step)), 'meanM': round(float(roll[i]), 1)})
        for j in range(i - win, i + win + 1):
            taken[j % nb] = True
    return {
        'rmsM': round(float(N.sqrt((d ** 2).mean())), 2),
        'p95M': round(float(N.percentile(d, 95)), 2),
        'maxM': round(float(d.max()), 2),
        'refToBuiltMaxM': round(float(back.max()), 2),
        'worstStretches': worst,
        'perPointM': d,
        'moved': moved,
    }


def verdict(rms):
    if rms is None:
        return 'unmeasured'
    if rms <= 4:
        return 'matches'
    if rms <= 10:
        return 'close'
    if rms <= 25:
        return 'off'
    return 'wrong shape'


# ------------------------------------------------------------------------------------------- wikipedia

def strip_wiki(v):
    v = re.sub(r'<ref[^>]*/>', '', v)
    v = re.sub(r'<ref[^>]*>.*?</ref>', '', v, flags=re.S)
    v = re.sub(r'<!--.*?-->', '', v, flags=re.S)
    v = re.sub(r'<br\s*/?>', '; ', v)
    v = re.sub(r'\{\{\s*convert\s*\|([^|}]*)\|([^|}]*)[^}]*\}\}', r'\1 \2', v, flags=re.I)
    v = re.sub(r'\{\{\s*(?:start date and age|start date|dts)\s*\|(\d{4})[^}]*\}\}', r'\1', v, flags=re.I)
    v = re.sub(r'\[\[(?:[^|\]]*\|)?([^\]]*)\]\]', r'\1', v)
    v = re.sub(r'\{\{[^{}]*\}\}', '', v)
    v = re.sub(r'<[^>]+>', '', v)
    v = v.replace('&nbsp;', ' ').replace("'''", '').replace("''", '')
    return re.sub(r'\s+', ' ', v).strip(' ;')


def split_top(body):
    """Split an infobox body on the pipes that are not inside a template or link."""
    parts, depth, cur, i = [], 0, [], 0
    while i < len(body):
        two = body[i:i + 2]
        if two in ('{{', '[['):
            depth += 1; cur.append(two); i += 2; continue
        if two in ('}}', ']]'):
            depth -= 1; cur.append(two); i += 2; continue
        if body[i] == '|' and depth == 0:
            parts.append(''.join(cur)); cur = []; i += 1; continue
        cur.append(body[i]); i += 1
    parts.append(''.join(cur))
    return parts


def infobox(text):
    i = text.lower().find('{{infobox')
    if i < 0:
        return {}
    depth, j = 0, i
    while j < len(text):
        if text[j:j + 2] == '{{':
            depth += 1; j += 2; continue
        if text[j:j + 2] == '}}':
            depth -= 1; j += 2
            if depth == 0:
                break
            continue
        j += 1
    out = {}
    for part in split_top(text[i + 2:j - 2])[1:]:
        if '=' in part:
            k, v = part.split('=', 1)
            out[k.strip().lower()] = v.strip()
    return out


def parse_coord(raw):
    m = re.search(r'\{\{\s*coord\s*\|([^}]*)\}\}', raw or '', re.I)
    if not m:
        return None
    args = [a.strip() for a in m.group(1).split('|') if '=' not in a]
    nums, hemi = [], []
    for a in args:
        if a.upper() in ('N', 'S', 'E', 'W'):
            hemi.append(a.upper())
        else:
            try:
                nums.append(float(a))
            except ValueError:
                break
    if not hemi and len(nums) >= 2:
        return nums[0], nums[1]
    if len(hemi) == 2 and nums:
        half = len(nums) // 2
        def dms(v): return sum(x / 60 ** k for k, x in enumerate(v))
        lat, lon = dms(nums[:half]), dms(nums[half:])
        return (-lat if hemi[0] == 'S' else lat), (-lon if hemi[1] == 'W' else lon)
    return None


def first_number(s):
    m = re.search(r'(\d+(?:\.\d+)?)', (s or '').replace(',', ''))
    return float(m.group(1)) if m else None


def wiki_facts(title, published_miles):
    q = urllib.parse.urlencode({'action': 'parse', 'page': title, 'prop': 'wikitext', 'section': 0,
                                'redirects': 1, 'format': 'json'})
    d = get_json(WIKI_API + '?' + q)
    if not d or 'parse' not in d:
        s = get_json(WIKI_API + '?' + urllib.parse.urlencode(
            {'action': 'query', 'list': 'search', 'srsearch': title, 'srlimit': 1, 'format': 'json'}))
        hits = (s or {}).get('query', {}).get('search', [])
        if not hits:
            return {'error': 'no Wikipedia article for %r' % title}
        title = hits[0]['title']
        q = urllib.parse.urlencode({'action': 'parse', 'page': title, 'prop': 'wikitext', 'section': 0,
                                    'redirects': 1, 'format': 'json'})
        d = get_json(WIKI_API + '?' + q)
        if not d or 'parse' not in d:
            return {'error': 'Wikipedia parse failed for %r' % title}
    title = d['parse']['title']
    box = infobox(d['parse']['wikitext']['*'])
    layouts = {}
    for k, v in box.items():
        # Both spellings occur: length_km2 and length2_km.
        m = re.match(r'^(layout|length|turns|banking|surface|record_time|record_driver|record_year|record_class)'
                     r'(\d*)(_km|_mi)?(\d*)$', k)
        if m and (m.group(1) != 'length' or m.group(3)):
            layouts.setdefault(m.group(2) or m.group(4) or '1', {})[m.group(1) + (m.group(3) or '')] = strip_wiki(v)
    rows = []
    for idx in sorted(layouts, key=int):
        L = layouts[idx]
        mi = first_number(L.get('length_mi'))
        km = first_number(L.get('length_km'))
        if mi is None and km is None:
            raw = L.get('length', '')
            v = first_number(raw)
            if v is not None:
                mi, km = (v, None) if re.search(r'[0-9. ]mi', raw) else (None, v)
        if mi is None and km is not None:
            mi = km * 1000 / MILE
        L['miles'] = round(mi, 3) if mi else None
        L['index'] = int(idx)
        rows.append(L)
    match = min((r for r in rows if r.get('miles')), default=None,
                key=lambda r: abs(r['miles'] - published_miles))
    # Diagrams: the infobox's image and track_map, each with its caption, the one whose caption shares most
    # words with the matched layout first (Daytona's image is the tri-oval, its track_map the road course).
    maps = []
    for key in ('image', 'track_map'):
        m = re.search(r'\[\[\s*(?:File|Image):([^|\]]+)', box.get(key, ''), re.I)
        if m:
            maps.append({'file': m.group(1).strip(), 'caption': strip_wiki(box.get(key + '_caption', ''))})
    if match:
        words = set(re.findall(r'[a-z]+', (match.get('layout') or '').lower()))
        maps.sort(key=lambda d: -len(words & set(re.findall(r'[a-z]+', d['caption'].lower()))))
    return {
        'title': title,
        'url': 'https://en.wikipedia.org/wiki/' + urllib.parse.quote(title.replace(' ', '_')),
        'coordinates': parse_coord(box.get('coordinates', '')),
        'location': strip_wiki(box.get('location', '')),
        'opened': strip_wiki(box.get('opened', '')),
        'capacity': strip_wiki(box.get('capacity', '')),
        'layouts': rows,
        'matchedLayout': match['index'] if match else None,
        'trackMaps': maps,
        'licence': 'Text from Wikipedia, CC BY-SA 4.0',
    }


def commons_image(filename, path):
    q = urllib.parse.urlencode({'action': 'query', 'titles': 'File:' + filename, 'prop': 'imageinfo',
                                'iiprop': 'url|extmetadata', 'iiurlwidth': 1400, 'format': 'json'})
    d = get_json(COMMONS_API + '?' + q)
    try:
        info = next(iter(d['query']['pages'].values()))['imageinfo'][0]
    except (TypeError, KeyError, StopIteration):
        return None
    url = info.get('thumburl') or info.get('url')
    data = http_get(url, binary=True) if url else None
    if not data:
        return None
    with open(path, 'wb') as f:
        f.write(data)
    meta = info.get('extmetadata', {})
    return {'file': filename, 'source': info.get('descriptionurl'),
            'licence': strip_wiki(meta.get('LicenseShortName', {}).get('value', '')),
            'artist': strip_wiki(meta.get('Artist', {}).get('value', ''))}


# ------------------------------------------------------------------------------------------------- osm

# What the circuit is made of (small, asked first) and the surroundings drawn for context (big, optional).
OSM_TRACK = ['["highway"="raceway"]', '["barrier"~"^(wall|guard_rail|jersey_barrier|retaining_wall|fence)$"]',
             '["area:highway"]', '["leisure"~"^(track|stadium)$"]', '["building"="grandstand"]']
OSM_CONTEXT = ['["building"]', '["amenity"="parking"]', '["natural"="water"]',
               '["highway"~"^(service|unclassified|tertiary|secondary|primary|residential)$"]']


def osm_query(lat, lon, radius, selectors):
    a = '(around:%d,%.6f,%.6f)' % (radius, lat, lon)
    return '[out:json][timeout:120];(' + ''.join('way%s%s;' % (a, s) for s in selectors) + ');out tags geom;'


def classify(e):
    t = e.get('tags', {})
    if t.get('highway') == 'raceway':
        return 'pit' if re.search(r'pit', t.get('name', '') + t.get('raceway', '') + t.get('service', ''), re.I) \
            else 'raceway'
    b = t.get('barrier')
    if b:
        return 'fence' if b == 'fence' else 'wall'
    if 'area:highway' in t:
        return 'surface'
    if t.get('building') == 'grandstand' or t.get('leisure') == 'stadium':
        return 'grandstand'
    if 'building' in t:
        return 'building'
    if t.get('amenity') == 'parking':
        return 'parking'
    if t.get('natural') == 'water':
        return 'water'
    if t.get('leisure') == 'track':
        return 'raceway'
    return 'road'


def ring_ways(ring, ways):
    """Which ways (in lap order) a ring found by node walking runs along."""
    owner = {}
    for w in ways:
        for p in w['geometry']:
            owner.setdefault(ft.key(p), w['id'])
    out = []
    for p in ring:
        wid = owner.get(ft.key(p))
        if wid is not None and (not out or out[-1] != wid):
            out.append(wid)
    while len(out) > 1 and out[0] == out[-1]:
        out.pop()
    return out


def find_candidates(by_kind, frame, published_m, width_m, trace):
    """Every closed ring near the published lap, as a centreline: (label, kind, ways, centre xy)."""
    cands = []
    if trace:
        xy = [frame.xy(p['lat'], p['lon']) for p in trace['geometry']]
        if xy[0] == xy[-1]:
            xy = xy[:-1]
        cands.append({'label': 'committed trace (Assets/TrackTraces)', 'kind': 'trace', 'ways': [],
                      'centre': xy, 'note': trace.get('foundBy', '')})

    def rings_of(ways, tol):
        found = []
        for r in ft.rings(ways, published_m, tol=tol):
            xy = [frame.xy(p['lat'], p['lon']) for p in r]
            if xy[0] == xy[-1]:
                xy = xy[:-1]
            if len(xy) < 8:
                continue
            L = closed_length(xy)
            if abs(L - published_m) / published_m > tol:
                continue
            cx, cy = sum(p[0] for p in xy) / len(xy), sum(p[1] for p in xy) / len(xy)
            if any(abs(L - f[1]) / L < 0.01 and math.hypot(cx - f[2], cy - f[3]) < 15 for f in found):
                continue
            found.append((r, L, cx, cy, xy))
        return found

    for r, L, _, _, xy in rings_of(by_kind['raceway'] + by_kind['pit'], 0.12):
        cands.append({'label': 'raceway ring taken as the centre', 'kind': 'raceway',
                      'ways': ring_ways(r, by_kind['raceway'] + by_kind['pit']), 'centre': xy})
    for kind in ('wall', 'fence'):
        for r, L, _, _, xy in rings_of(by_kind[kind], 0.15):
            base = tw.smooth_closed(tw.resample_closed(xy, 2.0), 20)
            ids = ring_ways(r, by_kind[kind])
            cands.append({'label': '%s ring as the OUTER edge, %.1f m in' % (kind, width_m / 2), 'kind': kind + '-outer',
                          'ways': ids, 'centre': tw.offset_closed(base, width_m / 2), 'edge': base})
            cands.append({'label': '%s ring as the INSIDE edge, %.1f m out' % (kind, width_m / 2), 'kind': kind + '-inner',
                          'ways': ids, 'centre': tw.offset_closed(base, -width_m / 2), 'edge': base})
    for c in cands:
        c['lengthM'] = round(closed_length(c['centre']), 1)
        c['lengthErrPct'] = round((c['lengthM'] / published_m - 1) * 100, 2)
        c['selfIntersects'] = self_intersects(c['centre'])
    # Committed trace first when it is plausible (somebody already chose it), then by lap length.
    cands.sort(key=lambda c: (c['selfIntersects'], c['kind'] != 'trace' or abs(c['lengthErrPct']) > 5,
                              abs(c['lengthErrPct'])))
    return cands[:8]


# ---------------------------------------------------------------------------------------------- imagery

def in_naip(lat, lon):
    return 24.4 <= lat <= 49.5 and -125 <= lon <= -66.8


def fetch_aerial(bbox_ll, path):
    """NAIP over a lat/lon box, in Web Mercator. The service times out now and then on a big render, so a
    failed ask is retried coarser. Returns the georeference, or None."""
    for mpp in (0.6, 0.6, 1.0, 1.5):
        r = fetch_aerial_once(bbox_ll, path, target_mpp=mpp)
        if r:
            return r
        time.sleep(5)
    return None


def fetch_aerial_once(bbox_ll, path, max_px=4000, target_mpp=0.6):
    (la0, lo0), (la1, lo1) = bbox_ll
    x0, y0 = merc(la0, lo0)
    x1, y1 = merc(la1, lo1)
    cos = math.cos(math.radians((la0 + la1) / 2))
    ground_w, ground_h = (x1 - x0) * cos, (y1 - y0) * cos
    mpp = max(target_mpp, ground_w / max_px, ground_h / max_px)
    W, H = int(round(ground_w / mpp)), int(round(ground_h / mpp))
    q = urllib.parse.urlencode({'bbox': '%.3f,%.3f,%.3f,%.3f' % (x0, y0, x1, y1), 'bboxSR': 3857,
                                'imageSR': 3857, 'size': '%d,%d' % (W, H), 'format': 'jpg',
                                'compressionQuality': 88, 'f': 'json'})
    d = get_json(NAIP + '?' + q, timeout=90)
    if not d or 'href' not in d:
        return None
    data = http_get(d['href'], binary=True, timeout=120)
    if not data or data[:2] != bytes([0xFF, 0xD8]):
        return None
    with open(path, 'wb') as f:
        f.write(data)
    e = d['extent']
    return {'file': os.path.basename(path), 'crs': 'EPSG:3857', 'width': d['width'], 'height': d['height'],
            'mercExtent': [e['xmin'], e['ymin'], e['xmax'], e['ymax']],
            'groundMetresPerPixel': round((e['xmax'] - e['xmin']) * cos / d['width'], 4),
            'source': 'USGS NAIP (USGS, USDA, The National Map: Orthoimagery), public domain',
            'fetched': time.strftime('%Y-%m-%d')}


# ---------------------------------------------------------------------------------------------- render

def render(tid, cache, frame, osm_by_kind, cands, best, fit, aerial, built_moved, lap_m, dev):
    from PIL import Image, ImageDraw, ImageFont
    if aerial:
        im = Image.open(os.path.join(cache, aerial['file'])).convert('RGB')
        W, H = im.size
        mx0, my0, mx1, my1 = aerial['mercExtent']

        def px(x, y):
            lat, lon = frame.latlon(x, y)
            mx, my = merc(lat, lon)
            return ((mx - mx0) / (mx1 - mx0) * W, (my1 - my) / (my1 - my0) * H)
        mpp = aerial['groundMetresPerPixel']
    else:
        pts = [p for c in cands for p in c['centre']] or [(0, 0)]
        if built_moved is not None:
            pts += [tuple(p) for p in built_moved]
        xs, ys = [p[0] for p in pts], [p[1] for p in pts]
        x0, y0, x1, y1 = min(xs) - 250, min(ys) - 250, max(xs) + 250, max(ys) + 250
        mpp = max(1.0, (x1 - x0) / 3000, (y1 - y0) / 3000)
        W, H = int((x1 - x0) / mpp), int((y1 - y0) / mpp)
        im = Image.new('RGB', (W, H), (40, 44, 40))

        def px(x, y):
            return ((x - x0) / mpp, (y1 - y) / mpp)
    lw = max(2, int(round(1.2 / mpp)))
    fs = max(14, int(min(W, H) / 90))
    font = ImageFont.load_default(size=fs)
    ov = Image.new('RGBA', im.size, (0, 0, 0, 0))
    dr = ImageDraw.Draw(ov)

    def poly(geom, col, width=lw, fill=None, closed=False):
        p = [px(*frame.xy(g['lat'], g['lon'])) for g in geom]
        if len(p) < 2:
            return
        if fill and closed and len(p) > 2:
            dr.polygon(p, fill=fill)
        dr.line(p + ([p[0]] if closed else []), fill=col, width=width)

    styles = [('parking', (90, 90, 90, 140), (90, 90, 90, 50)), ('water', (60, 120, 255, 200), (60, 120, 255, 80)),
              ('road', (230, 230, 230, 110), None), ('building', (200, 200, 200, 170), None),
              ('grandstand', (255, 0, 220, 230), (255, 0, 220, 70)), ('surface', (255, 150, 0, 200), None),
              ('fence', (255, 120, 120, 150), None), ('wall', (255, 30, 30, 255), None),
              ('pit', (0, 230, 255, 255), None), ('raceway', (255, 230, 0, 255), None)]
    for kind, col, fill in styles:
        for w in osm_by_kind.get(kind, []):
            g = w['geometry']
            closed = len(g) > 2 and ft.key(g[0]) == ft.key(g[-1])
            poly(g, col, lw if kind in ('wall', 'raceway', 'pit') else max(1, lw - 1), fill, closed)
    for i, c in enumerate(cands):
        col = (0, 255, 0, 255) if c is best else (0, 160, 0, 150)
        p = [px(*xy) for xy in c['centre']]
        dr.line(p + [p[0]], fill=col, width=lw + (1 if c is best else 0))
        lx, ly = px(*c['centre'][len(c['centre']) // (3 + i)])
        dr.text((lx + 6, ly + 6), '#%d' % i, fill=col, font=font)
    if built_moved is not None:
        d = dev['perPointM']
        for i in range(len(built_moved)):
            a, b = built_moved[i], built_moved[(i + 1) % len(built_moved)]
            e = d[i]
            col = (0, 255, 255, 255) if e <= 4 else (255, 200, 0, 255) if e <= 10 else (255, 0, 0, 255)
            dr.line([px(*a), px(*b)], fill=col, width=lw + 1)
        s0 = built_moved[0]
        x, y = px(*s0)
        r = 5 * lw
        dr.ellipse([x - r, y - r, x + r, y + r], outline=(255, 0, 255, 255), width=lw)
        # Lap metres every 500 m, so a stretch the report names can be found on the picture.
        step = lap_m / len(built_moved)
        for k in range(1, int(lap_m // 500) + 1):
            j = int(round(k * 500 / step)) % len(built_moved)
            x, y = px(*built_moved[j])
            dr.ellipse([x - 2 * lw, y - 2 * lw, x + 2 * lw, y + 2 * lw], fill=(255, 0, 255, 255))
            dr.text((x + 3 * lw, y - fs), '%d m' % (k * 500), fill=(255, 255, 255, 255), font=font,
                    stroke_width=2, stroke_fill=(0, 0, 0, 255))
    im = Image.alpha_composite(im.convert('RGBA'), ov).convert('RGB')
    dr = ImageDraw.Draw(im)
    bar = 100 / mpp                                                      # 100 m scale bar, north up
    dr.rectangle([20, H - 40, 20 + bar, H - 30], fill=(255, 255, 255))
    dr.text((20, H - 46 - fs), '100 m   north is up   %.2f m/px' % mpp, fill=(255, 255, 255), font=font,
            stroke_width=2, stroke_fill=(0, 0, 0))
    lines = ['%s reference: yellow raceway, cyan pit, red wall, pink fence, magenta stands, orange surface,' % tid,
             'green = candidate centrelines (bright = best #%d), built line cyan<=4 m / amber<=10 / red>10 m off,'
             % cands.index(best) if best else 'no candidate centreline found',
             'magenta ring = metre 0 of the game spline (its seam), dots = spline metres']
    if dev:
        lines.append('built vs best: RMS %.1f m, p95 %.1f m, max %.1f m, scale x%.4f'
                     % (dev['rmsM'], dev['p95M'], dev['maxM'], fit[0]))
    for i, t in enumerate(lines):
        y = 10 + int(fs * 1.3) * i
        dr.rectangle([10, y, 14 + dr.textlength(t, font=font), y + int(fs * 1.3)], fill=(0, 0, 0))
        dr.text((12, y + 1), t, fill=(255, 255, 255), font=font)
    im.save(os.path.join(cache, 'overview.jpg'), quality=88)
    small = im.copy()
    small.thumbnail((1600, 1600))
    small.save(os.path.join(cache, 'overview_small.jpg'), quality=85)


# ------------------------------------------------------------------------------------------------- pack

def load_json(path):
    try:
        with open(path, encoding='utf-8') as f:
            return json.load(f)
    except (OSError, ValueError):
        return None


def save_json(path, obj):
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(obj, f, indent=1, ensure_ascii=False)


def build(tid, refresh=False, offline=False):
    export = load_json(os.path.join(EXPORTS, tid + '.json'))
    if not export:
        return {'trackId': tid, 'error': 'no Tools/TrackExports/%s.json (Unity: Export Geometry For Paddock Layout)' % tid}
    folder = os.path.join(OUT, tid)
    cache = os.path.join(folder, 'cache')
    os.makedirs(cache, exist_ok=True)
    miles = export['lengthMiles']
    published_m = miles * MILE
    width_m = export['centerline'][0][2]
    built = [(p[0], p[1]) for p in export['centerline']]
    trace = load_json(os.path.join(TRACES, tid + '.json'))
    log = []

    # Facts.
    wpath = os.path.join(cache, 'wiki.json')
    wiki = None if refresh else load_json(wpath)
    if wiki is None and not offline:
        wiki = wiki_facts(WIKI_TITLES.get(tid, export['displayName']), miles)
        save_json(wpath, wiki)
    wiki = wiki or {'error': 'offline, nothing cached'}

    # Venue centre: the committed trace, else Wikipedia, else fetch_traces' table.
    centre = None
    if trace:
        g = trace['geometry']
        centre = (sum(p['lat'] for p in g) / len(g), sum(p['lon'] for p in g) / len(g))
    elif wiki.get('coordinates'):
        centre = tuple(wiki['coordinates'])
    else:
        centre = next(((v[1], v[2]) for v in ft.VENUES if v[0] == tid), None)
    if not centre:
        return {'trackId': tid, 'error': 'no coordinates for the venue'}
    radius = int(min(2200, max(900, published_m * 0.32 + 350)))

    # OSM.
    opath = os.path.join(cache, 'osm.json')
    osm = None if refresh else load_json(opath)
    if osm is None and not offline:
        osm = overpass(osm_query(centre[0], centre[1], radius, OSM_TRACK))
        if osm:
            osm['query'] = {'lat': centre[0], 'lon': centre[1], 'radiusM': radius}
            osm['contextMissing'] = True
            save_json(opath, osm)
        else:
            log.append('Overpass did not answer; no OSM layers (it turns work away when busy - re-run later, '
                       'finished venues come from the cache)')
    if osm and osm.get('contextMissing') and not offline:
        ctx = overpass(osm_query(centre[0], centre[1], radius, OSM_CONTEXT), waits=(0, 30, 90))
        if ctx:
            seen = {e['id'] for e in osm['elements']}
            osm['elements'] += [e for e in ctx['elements'] if e['id'] not in seen]
            osm.pop('contextMissing')
            save_json(opath, osm)
    if osm and osm.get('contextMissing'):
        log.append('buildings, roads, car parks and water not drawn (Overpass busy); a re-run fetches them')
    by_kind = {k: [] for k in ('raceway', 'pit', 'wall', 'fence', 'surface', 'grandstand', 'building',
                               'parking', 'water', 'road')}
    for e in (osm or {}).get('elements', []):
        if e.get('type') == 'way' and e.get('geometry'):
            by_kind[classify(e)].append(e)

    frame = Frame(*centre)
    cands = find_candidates(by_kind, frame, published_m, width_m, trace)
    best = next((c for c in cands if not c['selfIntersects']), None)

    # Fit the game's line onto every candidate; the best candidate's fit is the headline.
    fit = dev = None
    for c in cands:
        if c['selfIntersects']:
            continue
        s, R, t = fit_shape(built, c['centre'])
        d = deviation(built, c['centre'], s, R, t, published_m)
        c['builtFit'] = {k: d[k] for k in ('rmsM', 'p95M', 'maxM')}
        c['builtFit']['scale'] = round(s, 4)
        if c is best:
            fit, dev = (s, R, t), d

    # Pit lanes OSM knows about (not part of any ring), longest first.
    pits = sorted(({'wayId': w['id'], 'name': w.get('tags', {}).get('name', ''),
                    'lengthM': round(ft.length_of(w['geometry']), 1)} for w in by_kind['pit']),
                  key=lambda p: -p['lengthM'])[:6]

    # Wall-to-wall width where an outer and an inner ring are both mapped.
    width_measured = None
    edges = [c for c in cands if c.get('edge') and c['kind'].endswith('-outer')]
    if len(edges) >= 2:
        N = np()
        rings_xy = sorted({id(c['edge']): c['edge'] for c in edges}.values(), key=lambda e: -closed_length(e))
        if len(rings_xy) >= 2:
            d, _ = nearest(resample(rings_xy[0], 600), resample(rings_xy[1], 1200))
            width_measured = {'medianM': round(float(N.median(d)), 1), 'p10M': round(float(N.percentile(d, 10)), 1),
                              'p90M': round(float(N.percentile(d, 90)), 1),
                              'note': 'gap between the two longest wall rings (outer wall to pit wall / inside wall)'}

    # Imagery.
    aerial = None if refresh else load_json(os.path.join(cache, 'aerial.json'))
    if aerial and not os.path.exists(os.path.join(cache, aerial['file'])):
        aerial = None
    if aerial is None and not offline:
        if in_naip(*centre):
            # Frame the circuit (best candidates and the fitted game line); only with neither, every raceway.
            pts = [p for c in cands[:3] for p in c['centre']]
            if not pts:
                pts = [frame.xy(g['lat'], g['lon']) for w in by_kind['raceway'] for g in w['geometry']]
            if dev is not None:
                pts += [tuple(p) for p in dev['moved']]
            if pts:
                xs, ys = [p[0] for p in pts], [p[1] for p in pts]
                x0, y0, x1, y1 = max(min(xs), -radius), max(min(ys), -radius), min(max(xs), radius), min(max(ys), radius)
            else:
                x0 = y0 = -0.7 * radius
                x1 = y1 = 0.7 * radius
            pad = 250
            aerial = fetch_aerial((frame.latlon(x0 - pad, y0 - pad), frame.latlon(x1 + pad, y1 + pad)),
                                  os.path.join(cache, 'aerial.jpg'))
            if aerial:
                save_json(os.path.join(cache, 'aerial.json'), aerial)
            else:
                log.append('NAIP did not return imagery')
        else:
            log.append('outside NAIP coverage (US only): no public-domain imagery; overview drawn on plain ground')

    # Track map from Commons.
    tmap = None if refresh else load_json(os.path.join(cache, 'trackmap.json'))
    if tmap is None and not offline and wiki.get('trackMaps'):
        tmap = []
        for i, m in enumerate(wiki['trackMaps']):
            name = 'trackmap.png' if i == 0 else 'trackmap%d.png' % (i + 1)
            got = commons_image(m['file'], os.path.join(cache, name))
            if got:
                tmap.append(got | {'caption': m['caption'], 'saved': name})
        save_json(os.path.join(cache, 'trackmap.json'), tmap)

    # Alignment for Unity: the aerial's corners in the package's own metres.
    if fit and aerial:
        s, R, t = fit
        N = np()
        mx0, my0, mx1, my1 = aerial['mercExtent']
        corners = {}
        for name, (mx, my) in (('topLeft', (mx0, my1)), ('topRight', (mx1, my1)),
                               ('bottomRight', (mx1, my0)), ('bottomLeft', (mx0, my0))):
            e = N.array(frame.xy(*unmerc(mx, my)))
            local = R.T @ (e - t) / s
            corners[name] = {'x': round(float(local[0]), 3), 'y': round(float(local[1]), 3)}
        save_json(os.path.join(cache, 'alignment.json'), dict(
            trackId=tid, image='aerial.jpg', width=aerial['width'], height=aerial['height'],
            frame='package-local metres (Tools/TrackExports)', fittedTo=best['label'],
            scale=round(fit[0], 5), rmsM=dev['rmsM'], **corners))

    render(tid, cache, frame, by_kind, cands, best, fit, aerial,
           dev['moved'] if dev else None, published_m, dev)

    match = None
    if wiki.get('layouts') and wiki.get('matchedLayout') is not None:
        match = next(r for r in wiki['layouts'] if r['index'] == wiki['matchedLayout'])
    facts = {
        'trackId': tid, 'displayName': export['displayName'], 'type': export['type'],
        'game': {'lengthMiles': miles, 'surfaceWidthM': width_m, 'hasCommittedTrace': bool(trace),
                 'traceFoundBy': (trace or {}).get('foundBy', '')},
        'wikipedia': {k: v for k, v in wiki.items() if k != 'layouts'},
        'wikipediaLayouts': wiki.get('layouts', []),
        'matchedLayout': match,
        'lengthCheckPct': round((match['miles'] / miles - 1) * 100, 2) if match and match.get('miles') else None,
        'venueCentre': [round(centre[0], 6), round(centre[1], 6)],
        'osmRadiusM': radius,
        'osmCounts': {k: len(v) for k, v in by_kind.items()},
        'osmPitLanes': pits,
        'wallGapMeasured': width_measured,
        'aerial': {k: v for k, v in (aerial or {}).items() if k != 'mercExtent'} or None,
        'trackMap': tmap,
        'attribution': 'OSM (c) OpenStreetMap contributors, ODbL; imagery USGS NAIP, public domain; '
                       'facts Wikipedia, CC BY-SA 4.0',
    }
    save_json(os.path.join(folder, 'facts.json'), facts)
    save_json(os.path.join(cache, 'candidates_geometry.json'),
              [{'index': i, 'kind': c['kind'], 'centreLatLon': [frame.latlon(*p) for p in c['centre']]}
               for i, c in enumerate(cands)])
    cand_out = [{k: v for k, v in c.items() if k not in ('centre', 'edge')} | {'index': i}
                for i, c in enumerate(cands)]
    summary = {
        'trackId': tid, 'candidates': cand_out, 'best': cands.index(best) if best else None,
        'builtVsBest': {k: v for k, v in dev.items() if k not in ('perPointM', 'moved')} | {'scale': round(fit[0], 4)}
        if dev else None,
        'verdict': verdict(dev['rmsM'] if dev else None),
        'notes': log,
    }
    save_json(os.path.join(folder, 'candidates.json'), summary)
    write_report(folder, facts, summary)
    return summary | {'facts': facts}


def write_report(folder, facts, summary):
    tid = facts['trackId']
    L = ['# %s - track reference' % facts['displayName'], '',
         'Generated by `python -I Tools/track_reference.py %s` on %s. Images and raw data are in `cache/` '
         '(git-ignored; re-run to recreate).' % (tid, time.strftime('%Y-%m-%d')), '',
         '**Verdict: %s.**' % summary['verdict'], '']
    g = facts['game']
    m = facts['matchedLayout']
    L += ['## Facts', '', '| | Game | Wikipedia |', '|---|---|---|',
          '| Lap | %.3f mi | %s |' % (g['lengthMiles'], ('%s mi (%s)' % (m['miles'], m.get('layout', ''))) if m else '-'),
          '| Racing surface | %.2f m (%.0f ft) | not in the infobox |' % (g['surfaceWidthM'], g['surfaceWidthM'] / 0.3048),
          '| Turns | - | %s |' % ((m or {}).get('turns') or '-'),
          '| Banking | `TrackDimensions` | %s |' % ((m or {}).get('banking') or '-'),
          '| Surface | - | %s |' % ((m or {}).get('surface') or '-'), '']
    w = facts['wikipedia']
    if w.get('url'):
        L.append('Source: [%s](%s). ' % (w['title'], w['url']) + ('Length check: Wikipedia is %+.2f%% from the game.'
                                                                  % facts['lengthCheckPct'] if facts['lengthCheckPct'] is not None else ''))
    elif w.get('error'):
        L.append('Wikipedia: %s.' % w['error'])
    if facts['wikipediaLayouts'] and len(facts['wikipediaLayouts']) > 1:
        L += ['', 'All layouts in the article:', '']
        for r in facts['wikipediaLayouts']:
            L.append('- %s: %s mi, %s turns, banking %s' % (r.get('layout', '?'), r.get('miles'), r.get('turns', '?'),
                                                            r.get('banking', '-')))
    if facts['wallGapMeasured']:
        wg = facts['wallGapMeasured']
        L += ['', 'Measured on OSM: %s - median %.1f m (p10 %.1f, p90 %.1f).' % (wg['note'], wg['medianM'], wg['p10M'], wg['p90M'])]
    L += ['', '## Centreline candidates', '',
          '| # | What | Lap | vs published | Crosses itself | Built line fits within (RMS / p95 / max) | OSM ways |',
          '|---|---|---|---|---|---|---|']
    for c in summary['candidates']:
        bf = c.get('builtFit')
        L.append('| %d%s | %s | %.0f m | %+.2f%% | %s | %s | %s |' % (
            c['index'], ' (best)' if c['index'] == summary['best'] else '', c['label'], c['lengthM'], c['lengthErrPct'],
            'yes' if c['selfIntersects'] else 'no',
            '%.1f / %.1f / %.1f m (scale x%.3f)' % (bf['rmsM'], bf['p95M'], bf['maxM'], bf['scale']) if bf else '-',
            ','.join(str(i) for i in c['ways'][:8]) + ('...' if len(c['ways']) > 8 else '')))
    if not summary['candidates']:
        L.append('| - | OSM has no closed ring near the published lap here | | | | | |')
    if facts['osmPitLanes']:
        L += ['', 'Mapped pit lanes: ' + ', '.join('way %d %s(%.0f m)' % (p['wayId'], (p['name'] + ' ') if p['name'] else '',
                                                                           p['lengthM']) for p in facts['osmPitLanes'])]
    b = summary['builtVsBest']
    if b:
        L += ['', '## How far the game is from the best candidate', '',
              'Fitted by rotation, translation and uniform scale (x%.4f). RMS **%.1f m**, 95%% within %.1f m, '
              'worst point %.1f m; worst gap from the candidate back to the game %.1f m.'
              % (b['scale'], b['rmsM'], b['p95M'], b['maxM'], b['refToBuiltMaxM']), '',
              'Furthest-out stretches (mean deviation over 100 m, in metres along the game spline from its seam - the export\'s first point, which is the start line unless the track sets a startFinishDistance):', '']
        for s in b['worstStretches']:
            L.append('- %d-%d m: %.1f m' % (s['fromM'], s['toM'], s['meanM']))
    L += ['', '## Files', '',
          '- `cache/overview_small.jpg` (and full-size `overview.jpg`): the aerial with every layer and the fitted game line.',
          '- `cache/aerial.jpg` + `aerial.json`: NAIP orthoimagery and its georeference.' if facts['aerial'] else
          '- No aerial (outside NAIP coverage or the service did not answer).',
          NL.join('- `cache/%s`: Wikipedia diagram "%s" (%s, %s).' % (m['saved'], m['caption'], m['licence'], m['source'])
                    for m in facts['trackMap']) if facts['trackMap'] else '- No layout diagram.',
          '- `cache/alignment.json`: the aerial placed in package metres; Unity: `Draftmaster > Tracks > Reference > '
          'Open Aerial Overlay For Selected Track`.',
          '', '## Next steps', '',
          '- Use a candidate: `python -I Tools/track_reference.py %s --write-trace=<#> [--start=apex|lat,lon] '
          '[--pit=<wayId>]`, then Unity `Draftmaster > Tracks > Import Traced Geometry For Every Trace`, '
          're-export the geometry, re-run this report, retrain the racing line.' % tid]
    for n in summary['notes']:
        L.append('- Note: %s.' % n)
    L += ['', facts['attribution'] + '.', '']
    with open(os.path.join(folder, 'report.md'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(L))


def write_trace(tid, index, force):
    if tid in HAND_BUILT and not force:
        sys.exit('%s is hand-built; pass --force to overwrite its trace' % tid)
    folder = os.path.join(OUT, tid)
    geo = load_json(os.path.join(folder, 'cache', 'candidates_geometry.json'))
    summ = load_json(os.path.join(folder, 'candidates.json'))
    facts = load_json(os.path.join(folder, 'facts.json'))
    if not geo or not summ or index >= len(geo):
        sys.exit('run the report first, and pick a candidate index it lists')
    c = summ['candidates'][index]
    if c['selfIntersects']:
        sys.exit('candidate %d crosses itself' % index)
    ll = geo[index]['centreLatLon']
    lat0 = sum(p[0] for p in ll) / len(ll)
    lon0 = sum(p[1] for p in ll) / len(ll)
    frame = Frame(lat0, lon0)
    centre = tw.smooth_closed(tw.resample_closed([frame.xy(*p) for p in ll], 2.0), 4)
    if tw.signed_area(centre) < 0:
        centre.reverse()
    start = next((a[8:] for a in sys.argv if a.startswith('--start=')), None)
    if start == 'apex':
        k = tw.tri_oval_apex(centre)
    elif start:
        slat, slon = (float(v) for v in start.split(','))
        sx, sy = frame.xy(slat, slon)
        k = min(range(len(centre)), key=lambda i: (centre[i][0] - sx) ** 2 + (centre[i][1] - sy) ** 2)
    else:
        k = 0
    centre = centre[k:] + centre[:k]
    g = facts['game']
    out = {
        'trackId': tid,
        'osmWayId': c['ways'][0] if c['ways'] else 0,
        'osmName': '%s %s' % (tid, c['kind']),
        'foundBy': 'track_reference.py candidate %d: %s (ways %s); %.2f%% from the published lap'
                   % (index, c['label'], ','.join(str(i) for i in c['ways']), c['lengthErrPct']),
        'publishedMiles': g['lengthMiles'],
        'startFinish': 'firstNode' if start else '',
        'segmentation': 'curvature',
        'tracedMetres': round(closed_length(centre), 1),
        'surfaceWidthMetres': g['surfaceWidthM'],
        'apronWidthMetres': tw.APRON_M if facts['type'] != 'RoadCourse' else 0.0,
        'attribution': tw.ATTRIBUTION,
        'geometry': [{'lat': round(a, 7), 'lon': round(b, 7)} for a, b in (frame.latlon(*p) for p in centre[::2])],
    }
    out['geometry'].append(out['geometry'][0])
    pit = next((a[6:] for a in sys.argv if a.startswith('--pit=')), None)
    if pit:
        osm = load_json(os.path.join(folder, 'cache', 'osm.json'))
        ways = {e['id']: e for e in osm['elements']}
        ids = [int(v) for v in pit.split(',')]
        missing = [i for i in ids if i not in ways]
        if missing:
            sys.exit('pit way(s) %s not in the cached OSM answer' % missing)
        lane = tw.join_ways([ways[i] for i in ids])
        pts = [frame.xy(p['lat'], p['lon']) for p in lane]
        mid = pts[len(pts) // 2]
        i = min(range(len(centre)), key=lambda k: (centre[k][0] - mid[0]) ** 2 + (centre[k][1] - mid[1]) ** 2)
        tx = centre[(i + 1) % len(centre)][0] - centre[i][0]
        ty = centre[(i + 1) % len(centre)][1] - centre[i][1]
        if (pts[-1][0] - pts[0][0]) * tx + (pts[-1][1] - pts[0][1]) * ty < 0:
            lane.reverse()
        out['pitLane'] = [{'lat': p['lat'], 'lon': p['lon']} for p in lane]
        out['pitLaneWays'] = ids
    path = os.path.join(TRACES, tid + '.json')
    save_json(path, out)
    print('wrote %s (%.0f m traced, %+.2f%%)' % (path, out['tracedMetres'], c['lengthErrPct']))


# ------------------------------------------------------------------------------------------- selftest

def selftest():
    """A stadium lap moved, turned, scaled, reversed, re-started and jittered must fit back to ~0."""
    N = np()
    rng = N.random.default_rng(3)
    pts = []
    for i in range(400):
        a = 2 * math.pi * i / 400
        pts.append((600 * math.cos(a) + 200 * math.cos(3 * a), 300 * math.sin(a)))
    built = pts
    ang = math.radians(37)
    R = N.array([[math.cos(ang), -math.sin(ang)], [math.sin(ang), math.cos(ang)]])
    ref = (1.03 * N.asarray(pts) @ R.T + N.array([1234.0, -567.0]))[::-1]
    ref = N.roll(ref, 123, axis=0) + rng.normal(0, 0.3, ref.shape)
    s, R2, t = fit_shape(built, [tuple(p) for p in ref])
    d = deviation(built, [tuple(p) for p in ref], s, R2, t, closed_length(built))
    rot = math.degrees(math.atan2(R2[1, 0], R2[0, 0]))
    ok = abs(s - 1.03) < 0.002 and abs(rot - 37) < 0.2 and d['rmsM'] < 1.0
    print('selftest: scale %.4f (1.03), rotation %.2f (37), RMS %.2f m -> %s' % (s, rot, d['rmsM'], 'PASS' if ok else 'FAIL'))
    # Infobox parsing on the shapes Wikipedia actually uses.
    box = infobox("{{Infobox motorsport venue\n| coordinates = {{Coord|29|11|8|N|81|4|10|W|display=inline}}\n"
                  "| layout = Tri-Oval\n| length_mi = 2.500\n| turns = 4\n| banking = Turns: 31°<br />Tri: 18°\n"
                  "| layout2 = Road [[Course|course]]\n| length_km2 = {{convert|5.73|km|mi}}\n}}")
    c = parse_coord(box['coordinates'])
    ok2 = abs(c[0] - 29.1856) < 1e-3 and abs(c[1] + 81.0694) < 1e-3 and box['layout2'].endswith('[[Course|course]]')
    print('selftest: infobox/coord parse -> %s' % ('PASS' if ok2 else 'FAIL'))
    return ok and ok2


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    if '--selftest' in sys.argv:
        sys.exit(0 if selftest() else 1)
    wt = next((a for a in sys.argv if a.startswith('--write-trace=')), None)
    if wt:
        if len(args) != 1:
            sys.exit('--write-trace needs exactly one track id')
        write_trace(args[0], int(wt.split('=')[1]), '--force' in sys.argv)
        return
    refresh, offline = '--refresh' in sys.argv, '--offline' in sys.argv
    if '--all' in sys.argv:
        ids = sorted(f[:-5] for f in os.listdir(EXPORTS) if f.endswith('.json'))
    elif args:
        ids = args
    else:
        sys.exit(__doc__)
    results = []
    for i, tid in enumerate(ids):
        had_cache = os.path.exists(os.path.join(OUT, tid, 'cache', 'osm.json'))
        try:
            r = build(tid, refresh, offline)
        except Exception as e:                       # one venue failing must not lose the rest of --all
            r = {'trackId': tid, 'error': '%s: %s' % (type(e).__name__, e)}
        results.append(r)
        if r.get('error'):
            print('%-16s ERROR %s' % (tid, r['error']), flush=True)
        else:
            b = r['builtVsBest']
            print('%-16s %-11s %s candidates, best #%s, built RMS %s m' % (
                tid, r['verdict'], len(r['candidates']), r['best'], b['rmsM'] if b else '-'), flush=True)
        if len(ids) > 1 and not offline and (refresh or not had_cache) and i < len(ids) - 1:
            time.sleep(12)                           # Overpass etiquette between venues
    if '--all' in sys.argv:
        write_status(results)


def write_status(results):
    L = ['# Track reference status', '',
         'Every venue measured against the real one by `python -I Tools/track_reference.py --all` (%s). '
         '"Built RMS" is how far the game\'s centreline is from the best real-world centreline candidate after '
         'fitting position, rotation and scale; matches <= 4 m, close <= 10 m, off <= 25 m, else wrong shape. '
         'Per-venue detail: `Tools/TrackReference/<id>/report.md`. See `Docs/Track-Reference.md`.' % time.strftime('%Y-%m-%d'),
         '', '| Track | Verdict | Built RMS / p95 / max (m) | Best candidate | Its lap vs published | Wikipedia lap vs game | Aerial |',
         '|---|---|---|---|---|---|---|']
    order = {'wrong shape': 0, 'off': 1, 'close': 2, 'matches': 3, 'unmeasured': 4}
    for r in sorted(results, key=lambda r: (order.get(r.get('verdict'), 5), r['trackId'])):
        if r.get('error'):
            L.append('| %s | error | %s | | | | |' % (r['trackId'], r['error']))
            continue
        b, f = r['builtVsBest'], r['facts']
        c = r['candidates'][r['best']] if r['best'] is not None else None
        L.append('| [%s](%s/report.md) | %s | %s | %s | %s | %s | %s |' % (
            r['trackId'], r['trackId'], r['verdict'],
            '%.1f / %.1f / %.1f' % (b['rmsM'], b['p95M'], b['maxM']) if b else '-',
            c['label'] if c else 'none in OSM', '%+.2f%%' % c['lengthErrPct'] if c else '-',
            '%+.2f%%' % f['lengthCheckPct'] if f['lengthCheckPct'] is not None else '-',
            'NAIP' if f['aerial'] else 'none'))
    L.append('')
    with open(os.path.join(OUT, 'STATUS.md'), 'w', encoding='utf-8') as f:
        f.write('\n'.join(L))
    print('wrote', os.path.join(OUT, 'STATUS.md'))


if __name__ == '__main__':
    main()
