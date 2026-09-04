"""Pull traced raceway centrelines into the project.

Three things the first pass got wrong, all fixed here:

1. Rate limiting. Overpass hands out "no response" freely; retry across mirrors with a backoff instead of
   giving up on the first miss.
2. Circuits traced as SEVERAL ways. Daytona, Charlotte, Indianapolis and the road courses are drawn as a
   handful of open ways sharing end nodes rather than one closed ring, so the ring has to be assembled by
   walking the shared endpoints.
3. Bad coordinates. A guessed lat/lon that lands in the wrong town finds nothing at all, so the venue's
   NAME is the fallback: raceways are a small enough set that Overpass will regex the lot inside a bbox.

What identifies the circuit is still the CHECK: of every ring found, take one whose traced length is within
12% of the published lap, preferring rings whose ways a mapper actually named. A kart track or a drag strip
in the same infield cannot pass that.

Data (c) OpenStreetMap contributors, ODbL.
"""
import json, math, os, subprocess, sys, time
from collections import defaultdict

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   'Assets', 'TrackTraces')
MIRRORS = ['https://overpass-api.de/api/interpreter',
           'https://overpass.kumi.systems/api/interpreter',
           'https://overpass.private.coffee/api/interpreter']
MILE = 1609.344
USA = '(24,-126,50,-66)'
MEX = '(14,-118,33,-86)'

# id, lat, lon, published miles, name regex for the fallback, bbox for that fallback
VENUES = [
    ('Phoenix',        33.3747, -112.3111, 1.022, 'Phoenix Raceway', USA),
    ('Martinsville',   36.6353,  -79.8517, 0.526, 'Martinsville Speedway', USA),
    ('Richmond',       37.5925,  -77.4197, 0.750, 'Richmond Raceway', USA),
    ('Iowa',           41.6820,  -93.0140, 0.875, 'Iowa Speedway', USA),
    ('NewHampshire',   43.3625,  -71.4611, 1.058, 'New Hampshire Motor Speedway', USA),
    ('NorthWilkesboro',36.1330,  -81.1000, 0.625, 'North Wilkesboro Speedway', USA),
    ('Rockingham',     34.9700,  -79.6300, 0.940, 'Rockingham Speedway', USA),
    ('BowmanGray',     36.0900,  -80.2400, 0.250, 'Bowman Gray', USA),
    ('Nashville',      36.0161,  -86.4064, 1.330, 'Nashville Superspeedway', USA),
    ('Gateway',        38.6503,  -90.1350, 1.250, 'World Wide Technology Raceway|Gateway', USA),
    ('Kansas',         39.1157,  -94.8319, 1.500, 'Kansas Speedway', USA),
    ('LasVegas',       36.2717, -115.0100, 1.500, 'Las Vegas Motor Speedway', USA),
    ('Miami',          25.4517,  -80.4090, 1.500, 'Homestead', USA),
    ('FortWorth',      33.0372,  -97.2820, 1.500, 'Texas Motor Speedway', USA),
    ('Charlotte',      35.3520,  -80.6829, 1.500, 'Charlotte Motor Speedway', USA),
    ('Atlanta',        33.3875,  -84.3167, 1.540, 'Atlanta Motor Speedway', USA),
    ('Daytona',        29.1852,  -81.0708, 2.500, 'Daytona International Speedway', USA),
    ('Talladega',      33.5686,  -86.0661, 2.660, 'Talladega Superspeedway', USA),
    ('Indianapolis',   39.7950,  -86.2347, 2.500, 'Indianapolis Motor Speedway', USA),
    ('WatkinsGlen',    42.3369,  -76.9272, 2.450, 'Watkins Glen', USA),
    ('Sonoma',         38.1610, -122.4550, 2.520, 'Sonoma Raceway', USA),
    ('COTA',           30.1328,  -97.6411, 3.410, 'Circuit of the Americas', USA),
    ('MidOhio',        40.6900,  -82.6400, 2.258, 'Mid-Ohio', USA),
    ('Portland',       45.5950, -122.6900, 1.967, 'Portland International Raceway', USA),
    ('RoadAmerica',    43.7980,  -87.9950, 4.048, 'Road America', USA),
    ('LimeRock',       41.9280,  -73.3850, 1.500, 'Lime Rock', USA),
    ('MexicoCity',     19.4042,  -99.0907, 2.674, 'Hermanos Rodr', MEX),
]

SELECT = ('way{0}["highway"="raceway"];'
          'way{0}["leisure"="track"]["sport"~"motor|racing|stock_car",i];')


def overpass(query, attempts=3):
    for attempt in range(attempts):
        for url in MIRRORS:
            p = subprocess.run(['curl', '-s', '--max-time', '120', '-G', url,
                                '--data-urlencode', 'data=' + query], capture_output=True)
            try:
                data = json.loads(p.stdout)
                if 'elements' in data:
                    return data
            except Exception:
                pass
            time.sleep(4)
        time.sleep(10 * (attempt + 1))
    return None


def around(lat, lon, radius):
    return ('[out:json][timeout:90];(' + SELECT.format('(around:%d,%.5f,%.5f)' % (radius, lat, lon))
            + ');out tags geom;')


def by_name(regex, bbox):
    return ('[out:json][timeout:120];(way["highway"="raceway"]["name"~"%s",i]%s;);out tags geom;'
            % (regex, bbox))


def length_of(geom):
    if len(geom) < 2: return 0.0
    lat0 = sum(p['lat'] for p in geom) / len(geom)
    k = math.cos(math.radians(lat0)) * 111320.0
    return sum(math.hypot((b['lon'] - a['lon']) * k, (b['lat'] - a['lat']) * 110540.0)
               for a, b in zip(geom, geom[1:]))


def key(p):
    return (round(p['lat'], 7), round(p['lon'], 7))


def rings(ways, want, tol=0.12):
    """Every loop these ways can make whose length is near the published lap.

    A venue mapped as one closed way falls out immediately. The big ones are not: Daytona's tri-oval is four
    ways, and they are cut wherever the road course, the pit road and the apron join it, so chaining by
    end nodes alone misses it — a way can end halfway ALONG its neighbour rather than at its tip.

    So walk the node graph instead and, at every junction, carry straight on. That is what a lap does: the
    pit entry and the infield roads leave at an angle and the circuit goes on ahead. Keep walking until it
    comes back to where it started."""
    found = []

    for i, w in enumerate(ways):
        g = w['geometry']
        if len(g) > 8 and key(g[0]) == key(g[-1]):
            found.append([p for p in g])

    adj = defaultdict(set)
    for w in ways:
        g = w['geometry']
        for a, b in zip(g, g[1:]):
            ka, kb = key(a), key(b)
            if ka == kb: continue
            adj[ka].add(kb)
            adj[kb].add(ka)

    def metres(a, b):
        k = math.cos(math.radians(a[0])) * 111320.0
        return math.hypot((b[1] - a[1]) * k, (b[0] - a[0]) * 110540.0)

    def bearing(a, b):
        k = math.cos(math.radians(a[0]))
        return math.atan2(b[0] - a[0], (b[1] - a[1]) * k)

    # Where the mapped road stops but the circuit does not. Daytona's tri-oval walks 3,367m of its 4,023m
    # and halts: the next piece was drawn as a separate way whose end node is a couple of metres away
    # rather than the same node. Rather than lose the venue to that, allow a hop to a nearby node that
    # carries straight on.
    cells = defaultdict(list)
    for k in adj:
        cells[(round(k[0], 3), round(k[1], 3))].append(k)

    def near(k, radius=30.0):
        out = []
        for dlat in (-0.001, 0, 0.001):
            for dlon in (-0.001, 0, 0.001):
                for other in cells.get((round(k[0] + dlat, 3), round(k[1] + dlon, 3)), ()):
                    if other != k and metres(k, other) <= radius: out.append(other)
        return out

    def follow(first, second):
        path = [first, second]
        total = metres(first, second)
        seen = {second}
        while total < want * (1 + tol):
            prev, cur = path[-2], path[-1]
            incoming = bearing(prev, cur)
            best, best_turn = None, 1e9
            for n in adj[cur]:
                if n == prev: continue
                turn = abs((bearing(cur, n) - incoming + math.pi) % (2 * math.pi) - math.pi)
                if turn < best_turn: best, best_turn = n, turn
            if best is None or best_turn > math.radians(75):
                best, best_turn = None, 1e9
                for n in near(cur):
                    if n in seen or n == prev: continue
                    turn = abs((bearing(cur, n) - incoming + math.pi) % (2 * math.pi) - math.pi)
                    if turn < best_turn and turn < math.radians(40): best, best_turn = n, turn
                if best is None: return None
            total += metres(cur, best)
            if best == first:
                path.append(best)
                return path if abs(total - want) / want <= tol else None
            if best in seen: return None          # doubled back on itself
            seen.add(best)
            path.append(best)
        return None

    # Seed from the long ways: a circuit is made of them, and a walk that starts on one and keeps going
    # straight stays on it. Both directions, because a trace runs whichever way the mapper drew it.
    seeds = sorted(range(len(ways)), key=lambda i: -length_of(ways[i]['geometry']))[:12]
    for i in seeds:
        g = ways[i]['geometry']
        if key(g[0]) == key(g[-1]): continue
        for a, b in ((g[0], g[1]), (g[-1], g[-2])):
            ring = follow(key(a), key(b))
            if ring: found.append([{'lat': p[0], 'lon': p[1]} for p in ring])

    return found


def simple(pts, samples=300):
    """A racing lap does not cross itself.

    This is the check that tells a circuit from a pile of infield service roads chained into a loop of
    roughly the right length: Daytona's tri-oval assembles from ten ways, and so does a figure of eight
    through the infield that is 7% off the published lap and looks nothing like a speedway."""
    step = max(1, len(pts) // samples)
    g = pts[::step]
    if len(g) < 6: return False
    lat0 = sum(p['lat'] for p in g) / len(g)
    k = math.cos(math.radians(lat0)) * 111320.0
    xy = [((p['lon'] - g[0]['lon']) * k, (p['lat'] - g[0]['lat']) * 110540.0) for p in g]
    if xy[0] != xy[-1]: xy.append(xy[0])

    def side(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    n = len(xy) - 1
    for i in range(n):
        a, b = xy[i], xy[i + 1]
        for j in range(i + 2, n):
            if i == 0 and j == n - 1: continue        # they share the closing node
            c, d = xy[j], xy[j + 1]
            if (side(a, b, c) > 0) != (side(a, b, d) > 0) and                (side(c, d, a) > 0) != (side(c, d, b) > 0):
                return False
    return True


def pick(ways, want, tol=0.12):
    """The best ring: closest to the published lap, then the one somebody named, then the simplest."""
    named = {}
    for w in ways:
        if w.get('tags', {}).get('name'):
            for p in w['geometry']: named[key(p)] = True

    best, best_score = None, None
    for pts in rings(ways, want, tol):
        err = abs(length_of(pts) - want) / want
        if err > tol: continue
        if not simple(pts): continue
        # Length is the objective test and comes first, in half-percent buckets so near-ties are settled by
        # the mapper: within a bucket, a ring drawn on named road beats one nobody named. The wrong way
        # round picked ten unnamed infield roads over Daytona's own tri-oval.
        share = sum(1 for p in pts if key(p) in named) / float(len(pts))
        score = (round(err / 0.005), -round(share, 2), len(pts))
        if best_score is None or score < best_score:
            best, best_score = (pts, err, share), score
    return best


def fetch(tid, lat, lon, miles, name, bbox):
    want = miles * MILE
    radius = int(max(2500, want / 2))

    tries = [('around', around(lat, lon, radius)),
             ('name', by_name(name, bbox)),
             ('wide', around(lat, lon, radius * 3))]

    for how, query in tries:
        data = overpass(query)
        if not data: continue
        ways = [w for w in data.get('elements', []) if len(w.get('geometry') or []) >= 4]
        if not ways: continue

        got = pick(ways, want)
        if got is None: continue

        pts, err, share = got
        payload = {
            'trackId': tid,
            'osmName': name,
            'publishedMiles': miles,
            'tracedMetres': round(length_of(pts), 1),
            'foundBy': how,
            'namedShare': round(share, 2),
            'attribution': 'Data (c) OpenStreetMap contributors, ODbL (opendatacommons.org/licenses/odbl)',
            'geometry': [{'lat': p['lat'], 'lon': p['lon']} for p in pts],
        }
        os.makedirs(OUT, exist_ok=True)
        with open(os.path.join(OUT, tid + '.json'), 'w', encoding='utf-8') as f:
            json.dump(payload, f, indent=1)
        return 'ok   %.3f mi traced vs %.3f published (%.1f%% off), %d nodes, %.0f%% named, by %s' % (
            payload['tracedMetres'] / MILE, miles, 100 * err, len(pts), 100 * share, how)

    return 'no ring of about the right length found'


if __name__ == '__main__':
    only = set(sys.argv[1:]) or None
    ok = 0
    todo = [v for v in VENUES if not only or v[0] in only]
    for tid, lat, lon, miles, name, bbox in todo:
        if os.path.exists(os.path.join(OUT, tid + '.json')):
            print('%-17s already have it' % tid, flush=True)
            ok += 1
            continue
        msg = fetch(tid, lat, lon, miles, name, bbox)
        print('%-17s %s' % (tid, msg), flush=True)
        if msg.startswith('ok'): ok += 1
        time.sleep(3)
    print('\n%d of %d' % (ok, len(todo)))
