using System.Collections.Generic;
using UnityEngine;

// The between-races road map of the USA: circuit nodes (one per racetrack scene) joined by highway edges,
// with made-up minor locations (engine builders, junkyards) strung along them. Code-defined (DummyDrivers
// pattern). Geography is FIXED — learning what lives where is the game. Junkyard STOCK rerolls weekly.
//
// CIRCUITS: every venue on the 2026 Cup / O'Reilly / Truck / ARCA Menards calendars, ARCA East and West
// included, plus the two legacy scenes (Kentucky, Los Angeles) that are no longer raced.
//
// LAYOUT: circuits sit on an 11x9 lattice (col 0 = west, row 0 = north). The cell comes from real
// longitude/latitude RANK, not raw degrees — columns hold roughly equal numbers of tracks — so relative
// geography holds (Homestead is the far southeast, Evergreen the northwest, Sonoma the west coast) while
// the distances are deliberately not to scale. Each node is then pushed off its cell by a fixed per-id
// jitter, so the map reads as a road network instead of a pegboard. Empty cells are wilderness.
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
public enum TravelLocationType { None, EngineShop, Junkyard }

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
        _nodes = new List<TravelNode>();
        _byId = new Dictionary<string, TravelNode>();
        _adjacency = new Dictionary<string, List<string>>();

        // --- Circuits on the lattice (id = scene name where one exists) ---
        // West coast: ARCA West country, plus the road courses and the new San Diego street race.
        Circuit("Evergreen", "Evergreen", 0, 0);
        Circuit("Portland", "Portland", 0, 1);
        Circuit("AllAmerican", "All American", 0, 3);
        Circuit("Sonoma", "Sonoma", 0, 4);
        Circuit("Madera", "Madera", 0, 5);
        Circuit("LasVegas", "Las Vegas", 1, 4);
        Circuit("KernCounty", "Kern County", 1, 5);
        Circuit("Irwindale", "Irwindale", 1, 6);
        Circuit("LosAngeles", "Los Angeles", 1, 7);
        Circuit("SanDiego", "San Diego", 1, 8);
        // Rockies and the southern plains.
        Circuit("ColoradoNational", "Colorado National", 2, 1);
        Circuit("Kansas", "Kansas", 2, 2);
        Circuit("Phoenix", "Phoenix", 2, 6);
        Circuit("FortWorth", "Texas", 2, 7);
        Circuit("COTA", "Circuit of the Americas", 2, 8);
        // Upper midwest.
        Circuit("Elko", "Elko", 3, 0);
        Circuit("MadisonIntl", "Madison International", 3, 1);
        Circuit("Iowa", "Iowa", 3, 2);
        Circuit("Springfield", "Springfield Mile", 3, 3);
        Circuit("Gateway", "World Wide Technology", 3, 4);
        Circuit("Milwaukee", "Milwaukee Mile", 4, 0);
        Circuit("Joliet", "Chicagoland", 4, 2);
        Circuit("DuQuoin", "DuQuoin", 4, 4);
        Circuit("NashvilleFair", "Nashville Fairgrounds", 4, 6);
        Circuit("FiveFlags", "Five Flags", 4, 8);
        // Indiana / Tennessee / the deep south.
        Circuit("IRP", "Indianapolis Raceway Park", 5, 3);
        Circuit("Indianapolis", "Indianapolis", 5, 4);
        Circuit("Salem", "Salem", 5, 5);
        Circuit("Nashville", "Nashville Superspeedway", 5, 6);
        Circuit("Talladega", "Talladega", 5, 7);
        Circuit("Berlin", "Berlin Raceway", 6, 0);
        Circuit("Michigan", "Michigan", 6, 1);
        Circuit("Winchester", "Winchester", 6, 2);
        Circuit("Kentucky", "Kentucky", 6, 4);
        Circuit("Atlanta", "Atlanta", 6, 7);
        // Great Lakes shore down the Appalachians.
        Circuit("FlatRock", "Flat Rock", 7, 1);
        Circuit("Toledo", "Toledo", 7, 2);
        Circuit("MidOhio", "Mid-Ohio", 7, 3);
        Circuit("Bristol", "Bristol", 7, 5);
        Circuit("NorthWilkesboro", "North Wilkesboro", 7, 6);
        // The Carolinas and Florida.
        Circuit("BowmanGray", "Bowman Gray", 8, 4);
        Circuit("Charlotte", "Charlotte", 8, 5);
        Circuit("Daytona", "Daytona", 8, 6);
        Circuit("NewSmyrna", "New Smyrna", 8, 7);
        Circuit("Miami", "Homestead-Miami", 8, 8);
        // The seaboard and New England.
        Circuit("WatkinsGlen", "Watkins Glen", 9, 1);
        Circuit("Richmond", "Richmond", 9, 4);
        Circuit("Martinsville", "Martinsville", 9, 5);
        Circuit("Rockingham", "Rockingham", 9, 6);
        Circuit("Darlington", "Darlington", 9, 7);
        Circuit("NewHampshire", "New Hampshire", 10, 0);
        Circuit("LimeRock", "Lime Rock Park", 10, 1);
        Circuit("LongPond", "Pocono", 10, 2);
        Circuit("Dover", "Dover", 10, 3);

        // --- Roads: generated from the layout, so moving a circuit re-routes the map around it ---
        BuildRoads();

        // --- Minor locations: each one sits ON a road, splitting it into two hops ---
        YardOn("Portland", "Evergreen", "cascade_wrecking", "Cascade Auto Wrecking",
            "Moss on the roofs, rain in the wiring loom. The engines have all been kept indoors.");
        ShopOn("Sonoma", "AllAmerican", "sierra_speed", "Sierra Speed & Machine",
            "Wine-country money, dirt-track know-how. They'll build whatever you can pay for.",
            "engine_r7", "gearbox_close", "aero_kit");
        YardOn("KernCounty", "LasVegas", "mojave_yard", "Mojave Boneyard",
            "Dry air keeps the sheet metal honest. Best-preserved junk in America.");
        ShopOn("LosAngeles", "Irwindale", "socal_speed", "SoCal Speed Emporium",
            "Land-speed royalty. Half the shop wall is Bonneville timing slips.",
            "engine_hemi", "chassis_light", "aero_kit");
        YardOn("Phoenix", "FortWorth", "route66_trading", "Route 66 Trading Post",
            "Gas, jerky, and a barn of parts pulled off everything that ever broke down on the Mother Road.");
        ShopOn("Gateway", "Phoenix", "panhandle_speed", "Panhandle Speed & Custom",
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
        YardOn("Charlotte", "Martinsville", "carolina_yard", "Tar Heel Salvage",
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

    // --- lattice ---
    public const int Cols = 11, Rows = 9;
    const float MarginX = 0.055f, MarginY = 0.075f;
    const float Jitter = 0.34f;   // cell fractions; keeps the map off a rigid pegboard
    static float StepX => (1f - 2f * MarginX) / (Cols - 1);
    static float StepY => (1f - 2f * MarginY) / (Rows - 1);

    // col 0 = west .. col 10 = east; row 0 = north .. row 8 = south (map space is y-down).
    public static Vector2 Cell(int col, int row) => new Vector2(MarginX + col * StepX, MarginY + row * StepY);

    // Fixed per-id shove off the cell centre. FNV-1a over the id, so it never moves between runs (or
    // between here and the prefab bake) but no two neighbours land on the same angle.
    static Vector2 CellJitter(string id)
    {
        uint h = 2166136261u;
        for (int i = 0; i < id.Length; i++) { h ^= id[i]; h *= 16777619u; }
        float jx = ((h & 0xFFFF) / 65535f * 2f - 1f) * Jitter;
        float jy = (((h >> 16) & 0xFFFF) / 65535f * 2f - 1f) * Jitter;
        return new Vector2(jx * StepX, jy * StepY);
    }

    static void Circuit(string id, string name, int col, int row) =>
        AddNode(new TravelNode { id = id, name = name, pos = Cell(col, row) + CellJitter(id), isCircuit = true });

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
    static void MountOn(string a, string b, string id)
    {
        var na = Get(a); var nb = Get(b);
        if (na == null || nb == null) { Debug.LogError($"TravelGraph: '{id}' mounts on unknown road {a} - {b}"); return; }
        if (!RemoveEdge(a, b))
            Debug.LogWarning($"TravelGraph: '{id}' mounts on {a} - {b}, which BuildRoads didn't draw — laying it anyway.");
        _byId[id].pos = (na.pos + nb.pos) * 0.5f;
        Edge(a, id);
        Edge(id, b);
    }

    // Roads from the layout: a Gabriel graph over the circuits — a road joins two of them when no third
    // sits inside the circle that has them as its diameter. That is exactly "join neighbours, never hop
    // over the track in between", which is what makes it read as a highway map: planar, no crossings.
    // Latitude is squashed by LatSquash first, because the map is wider than it is tall and a raw circle
    // test would otherwise favour north-south roads over east-west ones.
    const float LatSquash = 0.75f;

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
