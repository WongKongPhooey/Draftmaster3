"""Fetch every venue's traced centreline in ONE Overpass query, then sort the answer out locally.

One request per venue is the wrong shape for this: Overpass rate-limits hard, and thirty-odd venues each
retried across mirrors took seven minutes per FAILURE. Overpass will happily take a union of thirty around
clauses in a single query and answer it once, so ask once and do the identifying here.

Data (c) OpenStreetMap contributors, ODbL.
"""
import json, math, os, subprocess, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fetch_traces as F

SP = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(SP, 'all_raceways.json')


def fetch_everything(venues, chunk=5):
    """Ask in small batches. One clause per venue is fine, twenty-six in one query gets refused."""
    elements = []
    for start in range(0, len(venues), chunk):
        group = venues[start:start + chunk]
        clauses = []
        for tid, lat, lon, miles, name, bbox in group:
            radius = int(max(2500, miles * F.MILE / 2))
            clauses.append('way(around:%d,%.5f,%.5f)["highway"="raceway"];' % (radius, lat, lon))
        query = '[out:json][timeout:180];(' + ''.join(clauses) + ');out tags geom;'

        got = None
        for attempt in range(3):
            for url in F.MIRRORS:
                p = subprocess.run(['curl', '-s', '--max-time', '240', '-G', url,
                                    '--data-urlencode', 'data=' + query], capture_output=True)
                if p.stdout[:1] == b'{':
                    got = json.loads(p.stdout)
                    break
                print('  %s said no (%d bytes)' % (url.split('/')[2], len(p.stdout)), flush=True)
                time.sleep(5)
            if got: break
            time.sleep(15 * (attempt + 1))

        names = ', '.join(v[0] for v in group)
        if not got:
            print('%s: no answer' % names, flush=True)
            continue
        found = got.get('elements', [])
        elements.extend(found)
        print('%s: %d ways' % (names, len(found)), flush=True)
        time.sleep(3)

    data = {'elements': elements}
    json.dump(data, open(RAW, 'w', encoding='utf-8'))
    return data


def near(way, lat, lon, radius):
    for p in way['geometry']:
        k = math.cos(math.radians(lat)) * 111320.0
        if math.hypot((p['lon'] - lon) * k, (p['lat'] - lat) * 110540.0) <= radius:
            return True
    return False


if __name__ == '__main__':
    todo = [v for v in F.VENUES if not os.path.exists(os.path.join(F.OUT, v[0] + '.json'))]
    print('%d venues still without a trace' % len(todo), flush=True)

    data = json.load(open(RAW, encoding='utf-8')) if os.path.exists(RAW) else fetch_everything(todo)
    if not data:
        print('Overpass would not answer'); sys.exit(1)

    everything = [w for w in data.get('elements', []) if len(w.get('geometry') or []) >= 4]
    print('%d raceway ways in the answer\n' % len(everything), flush=True)

    ok = 0
    for tid, lat, lon, miles, name, bbox in todo:
        want = miles * F.MILE
        radius = max(2500, want / 2)
        ways = [w for w in everything if near(w, lat, lon, radius)]
        if not ways:
            print('%-17s nothing mapped there' % tid, flush=True)
            continue

        got = F.pick(ways, want)
        if got is None:
            longest = max(F.length_of(w['geometry']) for w in ways)
            print('%-17s %d ways nearby, no ring near %.0fm (longest way %.0fm)'
                  % (tid, len(ways), want, longest), flush=True)
            continue

        pts, err, share = got
        payload = {
            'trackId': tid,
            'osmName': name,
            'publishedMiles': miles,
            'tracedMetres': round(F.length_of(pts), 1),
            'foundBy': 'batch',
            'namedShare': round(share, 2),
            'attribution': 'Data (c) OpenStreetMap contributors, ODbL (opendatacommons.org/licenses/odbl)',
            'geometry': [{'lat': p['lat'], 'lon': p['lon']} for p in pts],
        }
        with open(os.path.join(F.OUT, tid + '.json'), 'w', encoding='utf-8') as f:
            json.dump(payload, f, indent=1)
        ok += 1
        print('%-17s ok   %.3f mi vs %.3f published (%.1f%% off), %d nodes, %.0f%% named'
              % (tid, payload['tracedMetres'] / F.MILE, miles, 100 * err, len(pts), 100 * share), flush=True)

    print('\n%d of %d' % (ok, len(todo)))
