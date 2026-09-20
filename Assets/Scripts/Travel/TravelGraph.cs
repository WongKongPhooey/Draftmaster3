using System.Collections.Generic;
using UnityEngine;

// The between-races road map of the USA: circuit nodes (one per racetrack scene) joined by highway edges,
// with made-up minor locations (engine builders, junkyards) strung along them. Code-defined (DummyDrivers
// pattern). Geography is FIXED — learning what lives where is the game. Junkyard STOCK rerolls weekly.
//
// CIRCUITS: every venue on the 2026 Cup / O'Reilly / Truck / ARCA Menards calendars, ARCA East and West
// included, plus the two legacy scenes (Kentucky, Los Angeles) that are no longer raced.
//
// LAYOUT: real latitude and longitude, projected onto the board (`ProjectCircuits`). It used to be a
// rank-based 11x9 lattice, which kept the compass directions honest but spread the country evenly — two
// tracks an hour apart sat as far apart as two a day apart, and the outline of the USA was nowhere in it.
// Now the map is the country: equirectangular, longitude narrowed by the cosine of the mean latitude so
// the shape is right, fitted to the plot without stretching either axis. Same-state tracks sit together,
// the Carolinas are a knot, Homestead hangs off the bottom right on its own.
//
// Two venues in the same town would be one unclickable blob at that scale, so `SpreadOut` then pushes any
// pair closer than MinGap apart along the line between them until nothing overlaps — Indianapolis and IRP
// are five miles apart in life and a dot's width apart here. It is a relaxation, not a re-layout: it moves
// what it must and leaves the rest where geography put it.
//
// Hand placement wins over both: Draftmaster > Travel Map > Save Marker Positions To Code writes whatever
// you dragged in Prefab Mode into TravelMapLayout, and anything in there overrides the projection. Drag
// first, save, then Snap Markers To Graph Layout to push the result back and re-bake the highways.
//
// ROADS build themselves from those positions (`BuildRoads`): a Gabriel graph — a road joins two tracks
// when no third track sits inside the circle spanning them — which is what a real highway map looks like:
// planar, neighbours joined, nothing hopping over a town in between. Anything left under three roads gets
// its nearest missing neighbour. Deterministic, so the geography is still fixed and learnable.
//
// Minor locations are mounted ON a road (`ShopOn`/`YardOn`), sitting at its midpoint and splitting it
// into two hops. So a route through a shop costs one stop more than the direct road beside it — that
// trade is the point of the detour allowance.
//
// Coordinates are normalized map space: x 0=west..1=east, y 0=north..1=south (GUI y-down).

// TeamFactory is your own shop rather than somebody else's business — one node, near the middle of the
// map, where the fabricators leave the parts they have built since the last time you called in.
public enum TravelLocationType { None, EngineShop, Junkyard, TeamFactory }

public class TravelNode
{
    public string id;            // circuits: the scene name
    public string name;
    public Vector2 pos;
    public bool isCircuit;
    public TravelLocationType locationType;
    public string flavor;
    public string[] shopStock;   // EngineShop: fixed PartCatalog ids for sale
}

public static class TravelGraph
{
    public const int DetourAllowance = 2; // stops beyond the direct route — "enough for a small detour"

    static List<TravelNode> _nodes;
    static Dictionary<string, TravelNode> _byId;
    static Dictionary<string, List<string>> _adjacency;
    static readonly List<(string a, string b)> _edges = new();

    public static IReadOnlyList<TravelNode> Nodes { get { EnsureBuilt(); return _nodes; } }
    public static IReadOnlyList<(string a, string b)> Edges { get { EnsureBuilt(); return _edges; } }

    public static TravelNode Get(string id)
    {
        EnsureBuilt();
        return !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out var n) ? n : null;
    }

    public static IReadOnlyList<string> Neighbors(string id)
    {
        EnsureBuilt();
        return _adjacency.TryGetValue(id ?? "", out var list) ? list : (IReadOnlyList<string>)System.Array.Empty<string>();
    }

    public static bool AreAdjacent(string a, string b)
    {
        var n = Neighbors(a);
        for (int i = 0; i < n.Count; i++) if (n[i] == b) return true;
        return false;
    }

    // BFS hop count (every edge costs 1 stop). -1 when unreachable.
    public static int ShortestHops(string from, string to)
    {
        EnsureBuilt();
        if (from == to) return 0;
        if (Get(from) == null || Get(to) == null) return -1;
        var dist = new Dictionary<string, int> { [from] = 0 };
        var q = new Queue<string>();
        q.Enqueue(from);
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            foreach (var nb in Neighbors(cur))
            {
                if (dist.ContainsKey(nb)) continue;
                dist[nb] = dist[cur] + 1;
                if (nb == to) return dist[nb];
                q.Enqueue(nb);
            }
        }
        return -1;
    }

    static void EnsureBuilt()
    {
        if (_nodes != null) return;
        _places.Clear();
        _projected.Clear();
        _authoredMounts.Clear();
        _mounts.Clear();
        _circuitRoads.Clear();
        _nodes = new List<TravelNode>();
        _byId = new Dictionary<string, TravelNode>();
        _adjacency = new Dictionary<string, List<string>>();

        // --- Circuits, by latitude/longitude (id = scene name where one exists) ---
        // Coordinates are the real venues, to three decimals: where a track has an OSM trace in
        // Assets/TrackTraces these are its centroid, the rest are the published site.
        // West coast: ARCA West country, plus the road courses and the new San Diego street race.
        Circuit("Evergreen", "Evergreen", 47.858f, -121.988f);
        Circuit("Portland", "Portland", 45.597f, -122.696f);
        Circuit("AllAmerican", "All American", 38.754f, -121.281f);
        Circuit("Sonoma", "Sonoma", 38.163f, -122.459f);
        Circuit("Madera", "Madera", 36.953f, -120.087f);
        Circuit("LasVegas", "Las Vegas", 36.272f, -115.010f);
        Circuit("KernCounty", "Kern County", 35.313f, -119.157f);
        Circuit("Irwindale", "Irwindale", 34.111f, -117.968f);
        Circuit("LosAngeles", "Los Angeles", 34.014f, -118.288f);
        Circuit("SanDiego", "San Diego", 32.684f, -117.180f);
        // Rockies and the southern plains.
        Circuit("ColoradoNational", "Colorado National", 40.079f, -104.955f);
        Circuit("Kansas", "Kansas", 39.116f, -94.830f);
        Circuit("Phoenix", "Phoenix", 33.375f, -112.311f);
        Circuit("FortWorth", "Texas", 33.037f, -97.282f);
        Circuit("COTA", "Circuit of the Americas", 30.134f, -97.641f);
        // Upper midwest.
        Circuit("Elko", "Elko", 44.575f, -93.323f);
        Circuit("MadisonIntl", "Madison International", 42.924f, -89.320f);
        Circuit("Iowa", "Iowa", 41.675f, -93.013f);
        Circuit("Springfield", "Springfield Mile", 39.826f, -89.648f);
        Circuit("Gateway", "World Wide Technology", 38.651f, -90.135f);
        Circuit("Milwaukee", "Milwaukee Mile", 43.021f, -88.011f);
        Circuit("Joliet", "Chicagoland", 41.475f, -88.057f);
        Circuit("DuQuoin", "DuQuoin", 38.013f, -89.239f);
        Circuit("NashvilleFair", "Nashville Fairgrounds", 36.133f, -86.768f);
        Circuit("FiveFlags", "Five Flags", 30.522f, -87.315f);
        // Indiana / Tennessee / the deep south.
        Circuit("IRP", "Indianapolis Raceway Park", 39.795f, -86.339f);
        Circuit("Indianapolis", "Indianapolis", 39.795f, -86.235f);
        Circuit("Salem", "Salem", 38.630f, -86.088f);
        Circuit("Nashville", "Nashville Superspeedway", 36.145f, -86.412f);
        Circuit("Talladega", "Talladega", 33.567f, -86.066f);
        Circuit("Berlin", "Berlin Raceway", 43.028f, -85.828f);
        Circuit("Michigan", "Michigan", 42.067f, -84.241f);
        Circuit("Winchester", "Winchester", 40.173f, -84.981f);
        Circuit("Kentucky", "Kentucky", 38.712f, -84.920f);
        Circuit("Atlanta", "Atlanta", 33.383f, -84.318f);
        // Great Lakes shore down the Appalachians.
        Circuit("FlatRock", "Flat Rock", 42.090f, -83.281f);
        Circuit("Toledo", "Toledo", 41.705f, -83.606f);
        Circuit("MidOhio", "Mid-Ohio", 40.690f, -82.636f);
        Circuit("Bristol", "Bristol", 36.516f, -82.257f);
        Circuit("NorthWilkesboro", "North Wilkesboro", 36.131f, -81.098f);
        // The Carolinas and Florida.
        Circuit("BowmanGray", "Bowman Gray", 36.083f, -80.222f);
        Circuit("Charlotte", "Charlotte", 35.352f, -80.683f);
        Circuit("Daytona", "Daytona", 29.185f, -81.070f);
        Circuit("NewSmyrna", "New Smyrna", 29.028f, -80.965f);
        Circuit("Miami", "Homestead-Miami", 25.452f, -80.409f);
        // The seaboard and New England.
        Circuit("WatkinsGlen", "Watkins Glen", 42.337f, -76.927f);
        Circuit("Richmond", "Richmond", 37.592f, -77.419f);
        Circuit("Martinsville", "Martinsville", 36.635f, -79.853f);
        Circuit("Rockingham", "Rockingham", 34.974f, -79.610f);
        Circuit("Darlington", "Darlington", 34.295f, -79.906f);
        Circuit("NewHampshire", "New Hampshire", 43.363f, -71.460f);
        Circuit("LimeRock", "Lime Rock Park", 41.928f, -73.384f);
        Circuit("LongPond", "Pocono", 41.055f, -75.509f);
        Circuit("Dover", "Dover", 39.189f, -75.530f);

        // Degrees into board positions, then apart far enough to be clicked, then whatever was placed by
        // hand. Roads are drawn from the result, so all three can move a highway.
        ProjectCircuits();

        // --- Roads: generated from the layout, so moving a circuit re-routes the map around it ---
        BuildRoads();
        foreach (var e in _edges) _circuitRoads.Add(e);   // before any of them are split by a shop

        // --- Your own shop, in the middle of the country ---
        // Not on a lattice cell: the middle cells are all taken (Indianapolis sits on the dead centre of
        // the board), so this is the most central spot on the map that still leaves ~95px of clear air
        // around it at the authored 1470x950 — measured against every circuit, shop and yard, labels
        // included. Moving it re-picks its slip roads below, and Restyle re-seats the marker.
        // Tulsa, Oklahoma: the emptiest patch of the middle of the country on this board — far enough off
        // the Kansas/Springfield/Gateway knot that its name has room — and still central enough that a
        // run across the map goes past the door, which is the point of it.
        FactoryHub("team_factory", "Team Factory", 36.154f, -95.993f,
            "Your own shop. The fabricators keep building while you are away, and nothing gets posted " +
            "out — whatever they have finished is on the rack, waiting for you to come and get it.");

        // --- Minor locations: each one sits ON a road, splitting it into two hops ---
        YardOn("Portland", "Evergreen", "cascade_wrecking", "Cascade Auto Wrecking",
            "Moss on the roofs, rain in the wiring loom. The engines have all been kept indoors.");
        ShopOn("Sonoma", "AllAmerican", "sierra_speed", "Sierra Speed & Machine",
            "Wine-country money, dirt-track know-how. They'll build whatever you can pay for.",
            "engine_r7", "gearbox_close", "aero_kit");
        // Between LA and Vegas, which is where the Mojave actually is — Bakersfield and Vegas are no
        // longer joined now the map is real geography.
        YardOn("Irwindale", "LasVegas", "mojave_yard", "Mojave Boneyard",
            "Dry air keeps the sheet metal honest. Best-preserved junk in America.");
        ShopOn("LosAngeles", "Irwindale", "socal_speed", "SoCal Speed Emporium",
            "Land-speed royalty. Half the shop wall is Bonneville timing slips.",
            "engine_hemi", "chassis_light", "aero_kit");
        YardOn("Phoenix", "FortWorth", "route66_trading", "Route 66 Trading Post",
            "Gas, jerky, and a barn of parts pulled off everything that ever broke down on the Mother Road.");
        // Texas up to Colorado, i.e. through the panhandle it is named after. It used to hang off
        // St Louis - Phoenix, a road the real map has no business drawing.
        ShopOn("FortWorth", "ColoradoNational", "panhandle_speed", "Panhandle Speed & Custom",
            "Big sky, big power. Everything's negotiable except the dyno numbers.",
            "engine_358", "gearbox_tall", "tires_soft", "tires_hard");
        ShopOn("ColoradoNational", "Kansas", "front_range_fab", "Front Range Fabrication",
            "Thin air, thick welds. They chassis-jig by eye and have never been wrong yet.",
            "chassis_light", "gearbox_tall", "engine_358");
        YardOn("Iowa", "Kansas", "cornbelt", "Corn Belt Salvage",
            "A thousand miles of flat road wears cars out. They all end up here.");
        YardOn("Gateway", "Springfield", "ozark_salvage", "Ozark Hollow Salvage",
            "Down a gravel track, past two dogs. Everything is for sale and nothing is priced.");
        YardOn("Milwaukee", "Joliet", "lakeshore_salvage", "Lakeshore Auto Salvage",
            "Lake wind has taken the paint off every shell in the yard. The mechanicals are immaculate.");
        YardOn("Toledo", "FlatRock", "rustbelt", "Rust Belt Auto Graveyard",
            "Rows of Detroit iron going back fifty years. The owner knows every casting number by heart.");
        ShopOn("IRP", "Indianapolis", "brickyard_machine", "Brickyard Machine Werks",
            "Ex-Indy fabricators who got bored of open wheels. Precision costs.",
            "engine_r7", "gearbox_close", "chassis_light");
        ShopOn("Kentucky", "Indianapolis", "bluegrass_machine", "Bluegrass Machine Co.",
            "A horse barn with a dyno cell in it. They only work on motors they think are worth it.",
            "engine_358", "engine_barnfind", "gearbox_close");
        ShopOn("Bristol", "Kentucky", "moonshine_garage", "Copperhead Hollow Garage",
            "They built engines to outrun the law long before anybody paid them to win races.",
            "engine_bootleg", "engine_358", "tires_soft");
        ShopOn("FiveFlags", "Talladega", "gulfcoast_gear", "Gulf Coast Gearworks",
            "Salt air, shrimp boats, and the best gear cutter in three states.",
            "gearbox_close", "gearbox_tall", "tires_hard");
        YardOn("Charlotte", "NorthWilkesboro", "carolina_yard", "Tar Heel Salvage",
            "Half the field's old wrecks end up here. So do their good parts.");
        YardOn("NewSmyrna", "Miami", "gatorbone", "Gator Bone Salvage",
            "Swamp air eats sheet metal, but the drivetrains keep. Watch where you step.");
        ShopOn("Dover", "Richmond", "liberty_speed", "Liberty Speed Shop",
            "Strip-mall storefront, serious back room. Honest work at honest prices.",
            "engine_358", "gearbox_tall", "tires_hard");
        YardOn("WatkinsGlen", "NewHampshire", "adirondack_salvage", "Adirondack Auto Salvage",
            "Acres of rusting stock cars under the pines. Bring a flashlight and cash.");
        ShopOn("LimeRock", "NewHampshire", "pitt_bros", "Pitt Brothers Engine Builders",
            "Two brothers, three dynos, zero patience. The best motors east of Charlotte — if you can find the place.",
            "engine_r7", "engine_hemi", "gearbox_close");
    }

    // --- geography ---

    // The authored plot (TravelMapPrefabBuilder.MapW/MapH). Positions are normalized, so only the RATIO
    // matters here: it is what stops the projection stretching the country to fill a wider rectangle.
    const float BoardW = 1470f, BoardH = 950f;
    // Clear air round the outside, in board fractions, so an edge venue's name is not against the frame.
    const float EdgeMargin = 0.05f;
    // How close two dots may sit, in board pixels. A dot is 16px with a name under it; any closer and two
    // tracks in the same county are one blob nobody can click.
    const float MinGap = 34f;

    // Where each circuit really is, kept alongside the node until the projection runs.
    struct Place { public TravelNode node; public float lat, lon; }
    static readonly List<Place> _places = new();

    // Where pure geography put each node, before TravelMapLayout overrode it. The editor needs this to
    // tell a marker somebody dragged from one still sitting where the coordinates left it — comparing
    // against the final position instead would drop every override the next time it saved.
    static readonly Dictionary<string, Vector2> _projected = new();

    // Which road each minor location splits: what the code asked for, and what it actually got once a
    // hand-mounted override from TravelMapLayout had its say. The editor needs both — it only writes an
    // override when the road a marker was dragged onto is not the authored one.
    static readonly Dictionary<string, (string a, string b)> _authoredMounts = new();
    static readonly Dictionary<string, (string a, string b)> _mounts = new();

    // The circuit-to-circuit roads as BuildRoads drew them, before any of them were split by a shop or a
    // yard. These are what a mount can be moved onto.
    static readonly List<(string a, string b)> _circuitRoads = new();

    public static bool TryMount(string id, out string a, out string b)
    {
        EnsureBuilt();
        if (!string.IsNullOrEmpty(id) && _mounts.TryGetValue(id, out var m)) { a = m.a; b = m.b; return true; }
        a = b = null;
        return false;
    }

    public static bool TryAuthoredMount(string id, out string a, out string b)
    {
        EnsureBuilt();
        if (!string.IsNullOrEmpty(id) && _authoredMounts.TryGetValue(id, out var m)) { a = m.a; b = m.b; return true; }
        a = b = null;
        return false;
    }

    // The road a point is closest to, measured in board pixels: what a shop dragged across the map should
    // snap onto. Roads already carrying another minor location are skipped — two on one road would leave
    // the second laying a duplicate of the road the first has already split.
    public static bool NearestRoad(Vector2 pos, string exceptId, out string a, out string b)
    {
        EnsureBuilt();
        a = b = null;

        var taken = new HashSet<string>();
        foreach (var kv in _mounts)
        {
            if (kv.Key == exceptId) continue;
            taken.Add(PairKey(kv.Value.a, kv.Value.b));
        }

        float best = float.MaxValue;
        foreach (var (ra, rb) in _circuitRoads)
        {
            if (taken.Contains(PairKey(ra, rb))) continue;
            var na = Get(ra); var nb = Get(rb);
            if (na == null || nb == null) continue;

            float d = PointToSegmentPixels(pos, na.pos, nb.pos);
            if (d >= best) continue;
            best = d; a = ra; b = rb;
        }
        return a != null;
    }

    static string PairKey(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;

    // Normalized positions on a board that is wider than it is tall, so both axes go to pixels before
    // anything is compared — otherwise "closest" leans north-south.
    static float PointToSegmentPixels(Vector2 p, Vector2 s0, Vector2 s1)
    {
        Vector2 pp = new Vector2(p.x * BoardW, p.y * BoardH);
        Vector2 a = new Vector2(s0.x * BoardW, s0.y * BoardH);
        Vector2 b = new Vector2(s1.x * BoardW, s1.y * BoardH);

        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.0001f) return Vector2.Distance(pp, a);

        float t = Mathf.Clamp01(Vector2.Dot(pp - a, ab) / len2);
        return Vector2.Distance(pp, a + ab * t);
    }

    public static bool TryProjected(string id, out Vector2 pos)
    {
        EnsureBuilt();
        if (!string.IsNullOrEmpty(id) && _projected.TryGetValue(id, out pos)) return true;
        pos = default;
        return false;
    }

    static void Circuit(string id, string name, float lat, float lon)
    {
        var n = new TravelNode { id = id, name = name, isCircuit = true };
        AddNode(n);
        _places.Add(new Place { node = n, lat = lat, lon = lon });
    }

    // The projection, remembered so the factory (placed by coordinates too) lands in the same country.
    static float _projScale = 1f, _projU0, _projV0, _projK = 1f;

    static void ProjectCircuits()
    {
        if (_places.Count == 0) return;

        // Equirectangular. Longitude degrees are narrowed by the cosine of the mean latitude, or the map
        // comes out stretched east-west the way an unprojected lat/lon plot always does.
        float latSum = 0f;
        foreach (var p in _places) latSum += p.lat;
        _projK = Mathf.Cos(latSum / _places.Count * Mathf.Deg2Rad);

        float uMin = float.MaxValue, uMax = float.MinValue, vMin = float.MaxValue, vMax = float.MinValue;
        foreach (var p in _places)
        {
            float u = p.lon * _projK, v = -p.lat;     // x east, y south (map space is y-down)
            uMin = Mathf.Min(uMin, u); uMax = Mathf.Max(uMax, u);
            vMin = Mathf.Min(vMin, v); vMax = Mathf.Max(vMax, v);
        }

        // One scale for both axes: the country keeps its shape and the spare room goes to the margins.
        float spanU = Mathf.Max(uMax - uMin, 0.0001f), spanV = Mathf.Max(vMax - vMin, 0.0001f);
        _projScale = Mathf.Min(BoardW * (1f - 2f * EdgeMargin) / spanU,
                               BoardH * (1f - 2f * EdgeMargin) / spanV);
        _projU0 = (uMin + uMax) * 0.5f;
        _projV0 = (vMin + vMax) * 0.5f;

        var px = new Vector2[_places.Count];
        for (int i = 0; i < _places.Count; i++) px[i] = Pixels(_places[i].lat, _places[i].lon);

        SpreadOut(px);

        for (int i = 0; i < _places.Count; i++)
        {
            var node = _places[i].node;
            node.pos = new Vector2(px[i].x / BoardW, px[i].y / BoardH);
            _projected[node.id] = node.pos;
            // Anything dragged in Prefab Mode and saved back to code beats the projection outright.
            if (TravelMapLayout.TryGet(node.id, out var placed)) node.pos = placed;
        }
    }

    // One place's coordinates in board pixels. Used for the circuits and, afterwards, for the factory,
    // so everything on the board is projected the same way.
    static Vector2 Pixels(float lat, float lon) => new Vector2(
        (lon * _projK - _projU0) * _projScale + BoardW * 0.5f,
        (-lat - _projV0) * _projScale + BoardH * 0.5f);

    // Push overlapping dots apart without re-laying the map out: any pair closer than MinGap shoves each
    // other half the shortfall along the line between them, repeatedly, until nothing is too close or the
    // passes stop achieving anything. Deterministic — same country, same board, every run.
    static void SpreadOut(Vector2[] px)
    {
        const int MaxPasses = 220;
        float low = BoardW * EdgeMargin * 0.4f;   // never shove a track off the board
        float highX = BoardW - low, highY = BoardH - low;

        for (int pass = 0; pass < MaxPasses; pass++)
        {
            float worst = 0f;
            for (int i = 0; i < px.Length; i++)
                for (int j = i + 1; j < px.Length; j++)
                {
                    Vector2 d = px[j] - px[i];
                    float dist = d.magnitude;
                    // Two venues on the exact same spot have no direction to part in, so give them one.
                    if (dist < 0.0001f) { d = new Vector2(0.6f, 0.8f); dist = 1f; }
                    if (dist >= MinGap) continue;

                    float shove = (MinGap - dist) * 0.5f;
                    Vector2 step = d / dist * shove;
                    px[i] -= step;
                    px[j] += step;
                    worst = Mathf.Max(worst, shove);
                }

            for (int i = 0; i < px.Length; i++)
                px[i] = new Vector2(Mathf.Clamp(px[i].x, low, highX), Mathf.Clamp(px[i].y, low, highY));

            if (worst < 0.05f) break;
        }
    }

    // The team factory is not somebody's roadside business like the shops and yards, so it is not mounted
    // on one highway: it gets its own slip roads onto the nearest few circuits. Central and never far off
    // a route through the middle of the country, which is the point — you are meant to call in.
    const int FactoryRoads = 4;

    static void FactoryHub(string id, string name, float lat, float lon, string flavor)
    {
        Vector2 p = Pixels(lat, lon);
        Vector2 pos = new Vector2(p.x / BoardW, p.y / BoardH);
        _projected[id] = pos;
        if (TravelMapLayout.TryGet(id, out var placed)) pos = placed;

        AddNode(new TravelNode { id = id, name = name, pos = pos, locationType = TravelLocationType.TeamFactory, flavor = flavor });

        var circuits = new List<TravelNode>();
        foreach (var n in _nodes) if (n.isCircuit) circuits.Add(n);
        circuits.Sort((a, b) => Sq(a.pos, pos).CompareTo(Sq(b.pos, pos)));
        for (int i = 0; i < FactoryRoads && i < circuits.Count; i++) Edge(id, circuits[i].id);
    }

    static void Shop(string id, string name, float x, float y, string flavor, params string[] stock) =>
        AddNode(new TravelNode { id = id, name = name, pos = new Vector2(x, y), locationType = TravelLocationType.EngineShop, flavor = flavor, shopStock = stock });

    static void Yard(string id, string name, float x, float y, string flavor) =>
        AddNode(new TravelNode { id = id, name = name, pos = new Vector2(x, y), locationType = TravelLocationType.Junkyard, flavor = flavor });

    // Mount a minor location halfway along the highway a..b: the road now runs a -> here -> b, and there
    // is no direct a..b edge, so stopping by costs the extra hop.
    static void ShopOn(string a, string b, string id, string name, string flavor, params string[] stock)
    {
        Shop(id, name, 0f, 0f, flavor, stock);
        MountOn(a, b, id);
    }

    static void YardOn(string a, string b, string id, string name, string flavor)
    {
        Yard(id, name, 0f, 0f, flavor);
        MountOn(a, b, id);
    }

    // Drop the location onto the middle of the a..b road: the road becomes a -> here -> b, so pulling in
    // costs the extra hop. The direct road is removed; if BuildRoads never drew one, we lay it here.
    static void MountOn(string authoredA, string authoredB, string id)
    {
        _authoredMounts[id] = (authoredA, authoredB);

        // Dragging a shop onto a different road in Prefab Mode saves the ROAD, not a position — the thing
        // a mount actually is. TravelMapLayout holds those, written by the editor's save.
        string a = authoredA, b = authoredB;
        if (TravelMapLayout.TryMount(id, out var oa, out var ob) && Get(oa) != null && Get(ob) != null)
        {
            a = oa; b = ob;
        }
        _mounts[id] = (a, b);

        var na = Get(a); var nb = Get(b);
        if (na == null || nb == null) { Debug.LogError($"TravelGraph: '{id}' mounts on unknown road {a} - {b}"); return; }
        if (!RemoveEdge(a, b))
            Debug.LogWarning($"TravelGraph: '{id}' mounts on {a} - {b}, which BuildRoads didn't draw — laying it anyway.");
        // A shop or yard is not placed, it is MOUNTED: its position is the middle of the road it splits,
        // and it has to follow when either end of that road moves. So it takes no hand placement — the
        // editor's save skips minor locations for the same reason.
        _projected[id] = (na.pos + nb.pos) * 0.5f;
        _byId[id].pos = _projected[id];
        Edge(a, id);
        Edge(id, b);
    }

    // Roads from the layout: a Gabriel graph over the circuits — a road joins two of them when no third
    // sits inside the circle that has them as its diameter. That is exactly "join neighbours, never hop
    // over the track in between", which is what makes it read as a highway map: planar, no crossings.
    // Positions are normalized to a board that is wider than it is tall, so a raw circle test on them
    // would favour north-south roads over east-west ones. LatSquash turns the y axis back into the same
    // units as the x axis — board pixels — which is what "nearest" and "in between" have to mean.
    const float LatSquash = BoardH / BoardW;

    static void BuildRoads()
    {
        var c = new List<TravelNode>(_nodes);   // only circuits exist at this point
        for (int i = 0; i < c.Count; i++)
            for (int j = i + 1; j < c.Count; j++)
            {
                Vector2 mid = (c[i].pos + c[j].pos) * 0.5f;
                float r2 = Sq(c[i].pos, c[j].pos) * 0.25f;
                bool clear = true;
                for (int k = 0; k < c.Count && clear; k++)
                    if (k != i && k != j && Sq(c[k].pos, mid) < r2) clear = false;
                if (clear) Edge(c[i].id, c[j].id);
            }

        // A Gabriel graph can still leave an outlying track with only a road or two. Anything under three
        // gets its nearest track that it isn't already joined to, so every venue offers a real choice.
        for (int i = 0; i < c.Count; i++)
        {
            while (_adjacency[c[i].id].Count < 3)
            {
                string best = null; float bestD = float.MaxValue;
                for (int k = 0; k < c.Count; k++)
                {
                    if (k == i || AreAdjacent(c[i].id, c[k].id)) continue;
                    float d = Sq(c[i].pos, c[k].pos);
                    if (d < bestD) { bestD = d; best = c[k].id; }
                }
                if (best == null) break;
                Edge(c[i].id, best);
            }
        }
    }

    static float Sq(Vector2 a, Vector2 b)
    {
        float dx = a.x - b.x, dy = (a.y - b.y) * LatSquash;
        return dx * dx + dy * dy;
    }

    static bool RemoveEdge(string a, string b)
    {
        int hit = _edges.FindIndex(e => (e.a == a && e.b == b) || (e.a == b && e.b == a));
        if (hit < 0) return false;
        _edges.RemoveAt(hit);
        _adjacency[a].Remove(b);
        _adjacency[b].Remove(a);
        return true;
    }

    static void AddNode(TravelNode n)
    {
        _nodes.Add(n);
        _byId[n.id] = n;
        _adjacency[n.id] = new List<string>();
    }

    static void Edge(string a, string b)
    {
        if (!_byId.ContainsKey(a) || !_byId.ContainsKey(b)) { Debug.LogError($"TravelGraph edge references unknown node: {a} - {b}"); return; }
        _edges.Add((a, b));
        _adjacency[a].Add(b);
        _adjacency[b].Add(a);
    }
}
