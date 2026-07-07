using System.Collections.Generic;
using UnityEngine;

// The between-races road map of the USA: circuit nodes (one per racetrack scene) joined by highway edges,
// with made-up minor locations (engine builders, junkyards) strung along them. Code-defined (DummyDrivers
// pattern). Geography is FIXED — learning what lives where is the game. Junkyard STOCK rerolls weekly.
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

        // --- Circuits (id = scene name in Assets/Levels/Racetracks or Assets/Scenes) ---
        Circuit("WatkinsGlen", "Watkins Glen", 0.795f, 0.245f);
        Circuit("NewHampshire", "New Hampshire", 0.875f, 0.185f);
        Circuit("LongPond", "Long Pond", 0.805f, 0.300f);
        Circuit("Dover", "Dover", 0.825f, 0.365f);
        Circuit("Richmond", "Richmond", 0.790f, 0.435f);
        Circuit("Martinsville", "Martinsville", 0.745f, 0.475f);
        Circuit("NorthWilkesboro", "North Wilkesboro", 0.718f, 0.502f);
        Circuit("Charlotte", "Charlotte", 0.745f, 0.530f);
        Circuit("Darlington", "Darlington", 0.775f, 0.575f);
        Circuit("Atlanta", "Atlanta", 0.685f, 0.625f);
        Circuit("Talladega", "Talladega", 0.635f, 0.635f);
        Circuit("Daytona", "Daytona", 0.765f, 0.775f);
        Circuit("Miami", "Miami", 0.800f, 0.905f);
        Circuit("Nashville", "Nashville", 0.615f, 0.545f);
        Circuit("Bristol", "Bristol", 0.700f, 0.475f);
        Circuit("Kentucky", "Kentucky", 0.655f, 0.445f);
        Circuit("Indianapolis", "Indianapolis", 0.615f, 0.395f);
        Circuit("Michigan", "Michigan", 0.645f, 0.280f);
        Circuit("Joliet", "Chicagoland", 0.575f, 0.335f);
        Circuit("Milwaukee", "Milwaukee", 0.565f, 0.265f);
        Circuit("Iowa", "Iowa", 0.490f, 0.330f);
        Circuit("Kansas", "Kansas", 0.445f, 0.440f);
        Circuit("FortWorth", "Fort Worth", 0.430f, 0.680f);
        Circuit("Phoenix", "Phoenix", 0.175f, 0.615f);
        Circuit("LasVegas", "Las Vegas", 0.130f, 0.475f);
        Circuit("LosAngeles", "Los Angeles", 0.075f, 0.560f);

        // --- Minor locations (made up; fixed geography, learnable) ---
        Shop("pitt_bros", "Pitt Brothers Engine Builders", 0.915f, 0.100f,
            "Two brothers, three dynos, zero patience. The best motors east of Charlotte — if you can find the place.",
            "engine_r7", "engine_hemi", "gearbox_close");
        Yard("adirondack_salvage", "Adirondack Auto Salvage", 0.845f, 0.200f,
            "Acres of rusting stock cars under the pines. Bring a flashlight and cash.");
        Shop("liberty_speed", "Liberty Speed Shop", 0.810f, 0.400f,
            "Strip-mall storefront, serious back room. Honest work at honest prices.",
            "engine_358", "gearbox_tall", "tires_hard");
        Yard("carolina_yard", "Tar Heel Salvage", 0.762f, 0.552f,
            "Half the field's old wrecks end up here. So do their good parts.");
        Shop("moonshine_garage", "Copperhead Hollow Garage", 0.662f, 0.508f,
            "They built engines to outrun the law long before anybody paid them to win races.",
            "engine_bootleg", "engine_358", "tires_soft");
        Yard("gatorbone", "Gator Bone Salvage", 0.785f, 0.845f,
            "Swamp air eats sheet metal, but the drivetrains keep. Watch where you step.");
        Yard("rustbelt", "Rust Belt Auto Graveyard", 0.612f, 0.305f,
            "Rows of Detroit iron going back fifty years. The owner knows every casting number by heart.");
        Shop("brickyard_machine", "Brickyard Machine Werks", 0.635f, 0.420f,
            "Ex-Indy fabricators who got bored of open wheels. Precision costs.",
            "engine_r7", "gearbox_close", "chassis_light");
        Yard("cornbelt", "Corn Belt Salvage", 0.465f, 0.385f,
            "A thousand miles of flat road wears cars out. They all end up here.");
        Shop("panhandle_speed", "Panhandle Speed & Custom", 0.437f, 0.560f,
            "Big sky, big power. Everything's negotiable except the dyno numbers.",
            "engine_358", "gearbox_tall", "tires_soft", "tires_hard");
        Yard("route66_trading", "Route 66 Trading Post", 0.300f, 0.655f,
            "Gas, jerky, and a barn of parts pulled off everything that ever broke down on the Mother Road.");
        Yard("mojave_yard", "Mojave Boneyard", 0.150f, 0.545f,
            "Dry air keeps the sheet metal honest. Best-preserved junk in America.");
        Shop("socal_speed", "SoCal Speed Emporium", 0.095f, 0.515f,
            "Land-speed royalty. Half the shop wall is Bonneville timing slips.",
            "engine_hemi", "chassis_light", "aero_kit");

        // --- Highways ---
        Edge("NewHampshire", "pitt_bros");            // dead-end detour into Maine
        Edge("NewHampshire", "adirondack_salvage");
        Edge("adirondack_salvage", "WatkinsGlen");
        Edge("WatkinsGlen", "LongPond");
        Edge("LongPond", "Dover");
        Edge("Dover", "liberty_speed");
        Edge("liberty_speed", "Richmond");
        Edge("Richmond", "Martinsville");
        Edge("Martinsville", "NorthWilkesboro");
        Edge("NorthWilkesboro", "Charlotte");
        Edge("Martinsville", "Bristol");
        Edge("Charlotte", "carolina_yard");
        Edge("carolina_yard", "Darlington");
        Edge("Darlington", "Atlanta");
        Edge("Atlanta", "Talladega");
        Edge("Atlanta", "Daytona");
        Edge("Daytona", "gatorbone");
        Edge("gatorbone", "Miami");
        Edge("Bristol", "moonshine_garage");
        Edge("moonshine_garage", "Nashville");
        Edge("Nashville", "Atlanta");
        Edge("Nashville", "Talladega");
        Edge("Nashville", "Kentucky");
        Edge("Kentucky", "brickyard_machine");
        Edge("brickyard_machine", "Indianapolis");
        Edge("Indianapolis", "Michigan");
        Edge("Indianapolis", "Joliet");
        Edge("Michigan", "rustbelt");
        Edge("rustbelt", "Joliet");
        Edge("Joliet", "Milwaukee");
        Edge("Milwaukee", "Iowa");
        Edge("Iowa", "cornbelt");
        Edge("cornbelt", "Kansas");
        Edge("Kansas", "panhandle_speed");
        Edge("panhandle_speed", "FortWorth");
        Edge("FortWorth", "route66_trading");
        Edge("route66_trading", "Phoenix");
        Edge("Phoenix", "mojave_yard");
        Edge("mojave_yard", "LasVegas");
        Edge("LasVegas", "socal_speed");
        Edge("socal_speed", "LosAngeles");
        Edge("LosAngeles", "Phoenix");
    }

    static void Circuit(string id, string name, float x, float y) =>
        AddNode(new TravelNode { id = id, name = name, pos = new Vector2(x, y), isCircuit = true });

    static void Shop(string id, string name, float x, float y, string flavor, params string[] stock) =>
        AddNode(new TravelNode { id = id, name = name, pos = new Vector2(x, y), locationType = TravelLocationType.EngineShop, flavor = flavor, shopStock = stock });

    static void Yard(string id, string name, float x, float y, string flavor) =>
        AddNode(new TravelNode { id = id, name = name, pos = new Vector2(x, y), locationType = TravelLocationType.Junkyard, flavor = flavor });

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
