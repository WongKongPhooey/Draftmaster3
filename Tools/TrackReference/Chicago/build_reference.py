"""Chicago street course reference lap, assembled from OpenStreetMap street centrelines.

OSM maps no raceway for the Grant Park course (it is put up on public roads each July and taken down), so
track_reference.py found no candidate. The lap is instead walked along the streets it is known to use, in
race order (2023-2025 layout, NBC Chicago's turn-by-turn guide, 2024):

    start/finish southbound on S Columbus Dr from E Jackson Dr
    T1  left onto E Balbo Dr, east              T2/T3  right onto DuSable Lake Shore Dr, south (SB lanes)
    T4  right onto E Roosevelt Rd, west          T5     right onto S Columbus Dr, north
    T6  left onto E Balbo Dr, west               T7     right onto S Michigan Ave, north (NB lanes)
    T8-T10  chicane: right onto E Congress Plaza Dr, round and back onto Michigan
    T11 right onto E Jackson Dr, east            T12    right onto S Columbus Dr

The way ids below are those streets' OSM ways, in lap order. Their nodes are joined, resampled every 2 m and
smoothed (40 binomial passes, about a 9 m Gaussian), kept every 4 m, so the street junctions become driveable corners - which
also keeps the two visits to the Columbus/Balbo junction (T1 from the north, T6 from the south) apart.
The result is written as one closed, synthetic way (id -1, highway=raceway) that track_reference.py merges
into the venue's OSM layers from reference_ways.json.

It is a street-centreline estimate of the racing line, not a survey: the course used one side of the wider
boulevards and the barriers moved some corners (turns 4, 5, 10 and 11 changed lanes between years).

    python -I Tools/TrackReference/Chicago/build_reference.py <overpass-answer.json>

The Overpass answer must hold the ways below with geometry, e.g.
    [out:json];way(41.864,-87.629,41.880,-87.612)[highway];out tags geom;

Data (c) OpenStreetMap contributors, ODbL.
"""
import json, math, os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(os.path.dirname(HERE)))
import trace_from_wall as tw     # noqa: E402

# (way id, street) in lap order; each way is reversed as needed to join its neighbour.
LAP = [
    (435559504, 'S Columbus Dr'), (435559500, 'S Columbus Dr'), (435559499, 'S Columbus Dr'),
    (231237737, 'S Columbus Dr'), (435559505, 'S Columbus Dr'), (518573327, 'S Columbus Dr'),
    (435559501, 'S Columbus Dr'), (231237526, 'S Columbus Dr'),
    (435558483, 'E Balbo Dr'), (435558481, 'E Balbo Dr'), (25026606, 'E Balbo Dr'),
    (1227037106, 'S DuSable Lake Shore Dr'), (235741029, 'S DuSable Lake Shore Dr'),
    (435561742, 'S DuSable Lake Shore Dr'),
    (90707299, 'E Roosevelt Rd'), (1287253578, 'E Roosevelt Rd'), (435561319, 'E Roosevelt Rd'),
    (235714536, 'S Columbus Dr'), (435561738, 'S Columbus Dr'), (435559503, 'S Columbus Dr'),
    (316907584, 'E Balbo Dr'), (435558482, 'E Balbo Dr'), (435558479, 'E Balbo Dr'),
    (25026604, 'E Balbo Dr (bridge over Metra)'), (314943087, 'E Balbo Dr'),
    (255623675, 'S Michigan Ave'), (435559507, 'S Michigan Ave'),
    (621319020, 'E Congress Plaza Dr'), (1377694368, 'E Congress Plaza Dr'), (313874094, 'E Congress Plaza Dr'),
    (313874095, 'E Congress Plaza Dr'), (1377697560, 'E Congress Plaza Dr'), (5010709, 'E Congress Plaza Dr'),
    (372671686, 'S Michigan Ave'), (772541923, 'S Michigan Ave'),
    (33116710, 'E Jackson Dr'), (23888140, 'E Jackson Dr (bridge over Metra)'), (435657099, 'E Jackson Dr'),
    (25026653, 'E Jackson Dr'),
]
LAT0, LON0 = 41.873, -87.621


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    with open(sys.argv[1], encoding='utf-8') as f:
        ways = {e['id']: e for e in json.load(f)['elements'] if e.get('type') == 'way' and e.get('geometry')}
    missing = [w for w, _ in LAP if w not in ways]
    if missing:
        sys.exit('ways missing from the Overpass answer: %s' % missing)
    kx = math.cos(math.radians(LAT0)) * 111320.0
    ky = 110540.0
    pts = []
    for wid, _ in LAP:
        g = [((p['lon'] - LON0) * kx, (p['lat'] - LAT0) * ky) for p in ways[wid]['geometry']]
        if pts:
            if math.dist(pts[-1], g[-1]) < math.dist(pts[-1], g[0]):
                g.reverse()
            gap = math.dist(pts[-1], g[0])
            if gap > 1.0:
                sys.exit('way %d does not join the lap (%.1f m gap)' % (wid, gap))
            g = g[1:]
        pts += g
    gap = math.dist(pts[0], pts[-1])
    if gap > 1.0:
        sys.exit('lap does not close (%.1f m gap)' % gap)
    pts = pts[:-1]
    raw_len = sum(math.dist(pts[i], pts[(i + 1) % len(pts)]) for i in range(len(pts)))
    ring = tw.smooth_closed(tw.resample_closed(pts, 2.0), 40)
    lap = sum(math.dist(ring[i], ring[(i + 1) % len(ring)]) for i in range(len(ring)))
    geom = [{'lat': round(LAT0 + y / ky, 7), 'lon': round(LON0 + x / kx, 7)} for x, y in ring[::2]]   # 4 m
    geom.append(dict(geom[0]))
    out = {
        'attribution': 'Data (c) OpenStreetMap contributors, ODbL (opendatacommons.org/licenses/odbl)',
        'note': 'Synthetic reference lap for the Chicago street course: OSM street centrelines in race order '
                '(2023-2025 layout, NBC Chicago turn-by-turn 2024), corners smoothed. Built by build_reference.py. '
                'An estimate of the racing line, not a survey.',
        'sourceWays': [{'wayId': w, 'street': s} for w, s in LAP],
        'streetLapM': round(raw_len, 1),
        'smoothedLapM': round(lap, 1),
        'elements': [{'type': 'way', 'id': -1, 'geometry': geom,
                      'tags': {'highway': 'raceway', 'name': 'Chicago Street Course (OSM street centrelines)',
                               'source': 'assembled from OSM ways, see sourceWays'}}],
    }
    path = os.path.join(HERE, 'reference_ways.json')
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(out, f, indent=1)
        f.write('\n')
    print('street lap %.1f m, smoothed %.1f m (%.3f mi), %d points -> %s' % (raw_len, lap, lap / 1609.344, len(geom), path))


if __name__ == '__main__':
    main()
