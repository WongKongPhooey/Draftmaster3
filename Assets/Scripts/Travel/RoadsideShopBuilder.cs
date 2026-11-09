using System.Collections.Generic;
using Draftmaster.Tracks;
using UnityEngine;

// Builds a roadside stop's interior from a layout (Draftmaster.Tracks.ShopLayout): floor, walls with a door
// back to the road, the counter with somebody behind it, the bays, the people working at them and the clutter.
//
//   Fabricator     plain body shells in their bays (Resources cup25blank / xfi25blank / cts25blank /
//                  arc25blank, tinted one flat colour each — no numbers, logos or stripes); working bays hold a
//                  bare chassis (cup25chassis) with a half-hung panel and a fabricator at it.
//   Engine builder engines on stands, engineers at the ones being built. Placeholder engine art.
//   Junkyard       this week's wrecks from the other ladders (AltSeriesCatalog.Wrecks), as placeholder
//                  silhouettes in the series' colour, plus picked-over husks; the yard hand at the counter.
//
// Which layout: Resources/Travel/Shops/<locationId>.json if that place has its own, else <Kind>.json; one
// variant per visit (ShopLayouts.Pick: location + travel week). Every piece carries a ShopPiece, which is how
// the Shop Layouts editor window reads a tweaked room back into a layout.
public static class RoadsideShopBuilder
{
    public const string RootName = "RoadsideShop";
    public const string ForcedVariantKey = "RoadsideShop.ForcedVariant";

    public static ShopKind KindOf(TravelNode node)
    {
        if (node == null) return ShopKind.EngineBuilder;
        return node.locationType switch
        {
            TravelLocationType.Fabricator => ShopKind.Fabricator,
            TravelLocationType.Junkyard => ShopKind.Junkyard,
            _ => ShopKind.EngineBuilder,
        };
    }

    // ------------------------------------------------------------------ data

    public static ShopLayoutFile LoadFile(string name)
    {
        var asset = Resources.Load<TextAsset>($"{ShopLayouts.Folder}/{name}");
        if (asset == null) return null;
        try { return JsonUtility.FromJson<ShopLayoutFile>(asset.text); }
        catch (System.Exception e)
        {
            Debug.LogError($"RoadsideShopBuilder: Resources/{ShopLayouts.Folder}/{name}.json is not valid JSON: {e.Message}");
            return null;
        }
    }

    // The layout a visit uses, and which file/variant it came from (for logs and the editor).
    public static ShopLayout LayoutFor(string locationId, ShopKind kind, int week, out string source)
    {
        var file = (string.IsNullOrEmpty(locationId) ? null : LoadFile(locationId)) ?? LoadFile(kind.ToString());
        int count = file?.variants?.Length ?? 0;
        if (count == 0) { source = "built-in fallback"; return Fallback(kind); }
        int k = ShopLayouts.Pick(locationId, week, count);
#if UNITY_EDITOR
        int forced = UnityEditor.SessionState.GetInt(ForcedVariantKey, -1);
        if (forced >= 0 && forced < count) k = forced;
#endif
        source = $"{(file.kind == kind.ToString() ? kind.ToString() : locationId)}.json variant {k}";
        return file.variants[k];
    }

    // A room that always works, for when there is no data at all.
    public static ShopLayout Fallback(ShopKind kind)
    {
        bool engines = kind == ShopKind.EngineBuilder;
        return new ShopLayout
        {
            name = "fallback", width = 22f, height = 16f, doorX = -7f,
            counter = new ShopSpot { x = 6f, y = -4.5f }, clerk = new ShopSpot { x = 6f, y = -3.4f },
            bays = engines
                ? new[] { new ShopBay { x = -6f, y = 4f }, new ShopBay { x = -2f, y = 4f, state = "working" }, new ShopBay { x = 2f, y = 4f } }
                : new[] { new ShopBay { x = -5f, y = 4f }, new ShopBay { x = 3f, y = 4f, state = "working" } },
            workers = new[] { new ShopSpot { x = engines ? -2f : 3f, y = engines ? 2.2f : 1.7f } },
        };
    }

    // ------------------------------------------------------------------ build

    public class Context
    {
        public ShopKind kind;
        public string locationId = "";
        public int week = 1;
        public Material material;      // the unlit sprite material the scene uses; null = default sprite material
        public bool editMode;          // the Shop Layouts window: no interactables, no doors that load scenes
    }

    // Build `layout` under `parent` (the room's centre). Returns the root.
    public static GameObject Build(ShopLayout layout, Transform parent, Context ctx)
    {
        var root = new GameObject(RootName);
        root.transform.SetParent(parent, false);
        var rng = new System.Random(AltSeriesCatalog.StableHash(ctx.locationId) ^ ctx.week * 977);

        BuildRoom(root.transform, layout, ctx);

        // Counter and the person behind it.
        var counter = Quad(root.transform, "Counter", new Vector2(layout.counter.x, layout.counter.y), layout.counter.rotation,
                           new Vector2(ShopLayouts.CounterWidth, ShopLayouts.CounterDepth), Wood, 8, ctx, solid: true);
        Piece(counter, ShopPiece.Role.Counter);
        var clerk = Person(root.transform, "Clerk", new Vector2(layout.clerk.x, layout.clerk.y), ctx, rng.Next(),
                           faceTowards: new Vector2(layout.counter.x, layout.counter.y - 3f));
        Piece(clerk, ShopPiece.Role.Clerk);
        if (!ctx.editMode)
        {
            var npc = clerk.AddComponent<ShopCounterNPC>();
            npc.speakerName = ClerkName(ctx.kind);
            npc.lines = Greetings(ctx.kind);
            npc.interactRange = 2.6f;
            LocationTitle.Attach(clerk, "COUNTER", 4.5f, ctx.kind == ShopKind.Junkyard ? "Talk to buy salvage and wrecks" : "Talk to buy");
        }

        // Bays.
        var wrecks = ctx.kind == ShopKind.Junkyard ? AltSeriesCatalog.Wrecks(ctx.locationId, ctx.week) : new List<Wreck>();
        int wreckIndex = 0;
        for (int i = 0; i < layout.bays.Length; i++)
        {
            var bay = layout.bays[i];
            var go = new GameObject($"Bay_{i}");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(bay.x, bay.y, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, bay.rotation);
            var piece = Piece(go, ShopPiece.Role.Bay);
            piece.state = bay.state;
            var (bw, bh) = ShopLayouts.BayFootprint(ctx.kind);
            Quad(go.transform, "Bay", Vector2.zero, 0f, new Vector2(bw, bh), BayMarking(ctx.kind), 1, ctx, local: true);
            bool working = bay.state == "working";
            switch (ctx.kind)
            {
                case ShopKind.Fabricator: Shell(go, working, rng, ctx); break;
                case ShopKind.EngineBuilder: Engine(go, working, rng, ctx); break;
                case ShopKind.Junkyard:
                    Wreck(go, wreckIndex < wrecks.Count ? wrecks[wreckIndex] : null, rng, ctx);
                    wreckIndex++;
                    break;
            }
        }

        // People at work.
        for (int i = 0; i < layout.workers.Length; i++)
        {
            var w = layout.workers[i];
            var at = new Vector2(w.x, w.y);
            var worker = Person(root.transform, $"Worker_{i}", at, ctx, rng.Next(), NearestBay(layout, at));
            Piece(worker, ShopPiece.Role.Worker);
            if (!ctx.editMode)
            {
                var npc = worker.AddComponent<NPCInteractable>();
                npc.speakerName = WorkerName(ctx.kind);
                npc.lines = WorkerLines(ctx.kind);
                npc.interactRange = 2f;
            }
        }

        // Clutter.
        for (int i = 0; i < layout.props.Length; i++)
        {
            var p = layout.props[i];
            var go = Quad(root.transform, $"Prop_{p.kind}_{i}", new Vector2(p.x, p.y), p.rotation,
                          new Vector2(p.width, p.height), PropColour(p.kind), 6, ctx, solid: true);
            var piece = Piece(go, ShopPiece.Role.Prop);
            piece.propKind = p.kind;
        }
        return root;
    }

    static void BuildRoom(Transform root, ShopLayout l, Context ctx)
    {
        float w = l.width, h = l.height, t = 0.6f;
        var floor = Quad(root, "Floor", Vector2.zero, 0f, new Vector2(w, h), FloorColour(ctx.kind), 0, ctx);
        floor.transform.localPosition = new Vector3(0f, 0f, 0.2f);
        var wall = new Color(0.10f, 0.10f, 0.12f);
        Quad(root, "Wall_Top", new Vector2(0f, h / 2f + t / 2f), 0f, new Vector2(w + 2 * t, t), wall, 5, ctx, solid: true);
        Quad(root, "Wall_Left", new Vector2(-w / 2f - t / 2f, 0f), 0f, new Vector2(t, h), wall, 5, ctx, solid: true);
        Quad(root, "Wall_Right", new Vector2(w / 2f + t / 2f, 0f), 0f, new Vector2(t, h), wall, 5, ctx, solid: true);
        // The bottom wall, either side of the door.
        float doorL = l.doorX - ShopLayouts.DoorWidth / 2f, doorR = l.doorX + ShopLayouts.DoorWidth / 2f;
        float leftW = doorL - (-w / 2f - t), rightW = (w / 2f + t) - doorR;
        if (leftW > 0.05f) Quad(root, "Wall_BottomL", new Vector2(-w / 2f - t + leftW / 2f, -h / 2f - t / 2f), 0f, new Vector2(leftW, t), wall, 5, ctx, solid: true);
        if (rightW > 0.05f) Quad(root, "Wall_BottomR", new Vector2(doorR + rightW / 2f, -h / 2f - t / 2f), 0f, new Vector2(rightW, t), wall, 5, ctx, solid: true);

        var door = Quad(root, "Door", new Vector2(l.doorX, -h / 2f - t / 2f), 0f, new Vector2(ShopLayouts.DoorWidth, t), new Color(0.42f, 0.34f, 0.20f), 6, ctx);
        Piece(door, ShopPiece.Role.Door);
        // Outside the door: a strip of road so the gap doesn't open on to nothing, and a stop so you can't walk off.
        Quad(root, "Forecourt", new Vector2(l.doorX, -h / 2f - 2.2f), 0f, new Vector2(ShopLayouts.DoorWidth + 3f, 3f), new Color(0.2f, 0.2f, 0.22f), 0, ctx);
        Quad(root, "ForecourtStop", new Vector2(l.doorX, -h / 2f - 3.9f), 0f, new Vector2(ShopLayouts.DoorWidth + 3.4f, 0.4f), wall, 5, ctx, solid: true);
        if (!ctx.editMode)
        {
            var exit = new GameObject("ExitDoor");
            exit.transform.SetParent(root, false);
            exit.transform.localPosition = new Vector3(l.doorX, -h / 2f - 0.6f, 0f);
            var d = exit.AddComponent<SceneDoorInteractable>();
            d.interactRange = 2.4f;
            d.speakerName = "Exit";
            d.turnsToFace = false;
            LocationTitle.Attach(exit, "EXIT", 4.5f, "Back to the road");
        }
    }

    // Where the player comes in: just inside the door.
    public static Vector2 Spawn(ShopLayout l) => new Vector2(l.doorX, -l.height / 2f + 1.4f);

    // ------------------------------------------------------------------ pieces

    // Flat paint. No numbers, logos or stripes — a shell is sold plain.
    static readonly Color[] PlainPaint =
    {
        new Color(0.92f, 0.92f, 0.92f), new Color(0.20f, 0.22f, 0.25f), new Color(0.75f, 0.12f, 0.12f),
        new Color(0.12f, 0.30f, 0.68f), new Color(0.95f, 0.78f, 0.15f), new Color(0.12f, 0.50f, 0.25f),
        new Color(0.95f, 0.45f, 0.10f), new Color(0.55f, 0.58f, 0.62f), new Color(0.40f, 0.18f, 0.55f),
    };

    static readonly string[] ShellSprites = { "cup25blank", "xfi25blank", "cts25blank", "arc25blank" };

    static void Shell(GameObject bay, bool working, System.Random rng, Context ctx)
    {
        if (working)
        {
            // In the jig: a bare frame, one panel hung, the rest stacked beside it.
            var frame = Sprite(bay.transform, "Chassis", LoadSprite("cup25chassis"), Color.white, 5.0f, 10, ctx);
            Solid(frame, new Vector2(5f, 2.4f));
            var panel = Sprite(bay.transform, "PanelHung", LoadSprite(ShellSprites[rng.Next(ShellSprites.Length)]),
                               PlainPaint[rng.Next(PlainPaint.Length)] * new Color(1, 1, 1, 0.45f), 5.0f, 11, ctx);
            panel.transform.localPosition = new Vector3(0.4f, 0f, -0.01f);
            Quad(bay.transform, "PanelStack", new Vector2(0f, -1.45f), 0f, new Vector2(1.6f, 0.35f), new Color(0.7f, 0.7f, 0.72f), 9, ctx, local: true);
            if (!ctx.editMode) LocationTitle.Attach(bay, "IN THE JIG", 3.5f, "A chassis being built up");
            return;
        }
        var shell = Sprite(bay.transform, "Shell", LoadSprite(ShellSprites[rng.Next(ShellSprites.Length)]),
                           PlainPaint[rng.Next(PlainPaint.Length)], 5.0f, 10, ctx);
        Solid(shell, new Vector2(5f, 2.4f));
        if (!ctx.editMode) LocationTitle.Attach(bay, "BODY SHELL", 3.5f, "Hung, finished, unpainted");
    }

    static void Engine(GameObject bay, bool working, System.Random rng, Context ctx)
    {
        var stand = Quad(bay.transform, "Stand", Vector2.zero, 0f, new Vector2(1.6f, 1.2f), new Color(0.25f, 0.26f, 0.3f), 9, ctx, local: true);
        Solid(stand, new Vector2(1f, 1f));
        var engine = Sprite(bay.transform, "Engine", EngineSprite(), Color.white, 1.5f, 10, ctx);
        if (working) engine.GetComponent<SpriteRenderer>().color = new Color(0.8f, 0.8f, 0.8f);
        if (working) Quad(bay.transform, "PartsTray", new Vector2(0f, -1.0f), 0f, new Vector2(1.1f, 0.4f), new Color(0.55f, 0.15f, 0.12f), 9, ctx, local: true);
        if (!ctx.editMode) LocationTitle.Attach(bay, working ? "ON THE BUILD STAND" : "ENGINE", 3f,
                                                working ? "Half a motor and a torque wrench" : "Built, bagged and tagged");
    }

    static void Wreck(GameObject bay, Wreck w, System.Random rng, Context ctx)
    {
        Color c = w != null ? new Color(w.series.r, w.series.g, w.series.b) : new Color(0.45f, 0.42f, 0.38f);
        float dmg = w != null ? w.damagePercent / 100f : 0.9f;
        c = Color.Lerp(c, new Color(0.3f, 0.25f, 0.2f), dmg * 0.6f);
        string shape = w == null ? "saloon" : w.series.id == "sprint_cars" ? "sprint" : w.series.id == "indy_lights" ? "openwheel" : "saloon";
        var car = Sprite(bay.transform, "Wreck", WreckSprite(shape), c, shape == "saloon" ? 4.6f : 4.2f, 10, ctx);
        car.transform.localRotation = Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() * 16 - 8));
        Solid(car, new Vector2(4.4f, 2f));
        if (!ctx.editMode)
        {
            if (w != null) LocationTitle.Attach(bay, w.series.name.ToUpperInvariant(), 3.5f, $"{w.damagePercent}% damaged · {PlayerWallet.Format(w.price)} as it sits");
            else LocationTitle.Attach(bay, "PICKED OVER", 3f, "Nothing left on it worth having");
        }
    }

    // A person built from the paddock's paper-doll parts, standing still and facing what they work on.
    static GameObject Person(Transform parent, string name, Vector2 at, Context ctx, int seed, Vector2 faceTowards)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(at.x, at.y, 0f);
        Vector2 d = faceTowards - at;
        if (d.sqrMagnitude > 0.01f) go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.3f;
        var look = new GameObject("Look");
        look.transform.SetParent(go.transform, false);
        var layered = look.AddComponent<NPCLayeredAppearance>();
        layered.library = Resources.Load<NPCPartLibrary>("NPC/NPCPartLibrary");
        layered.sortingLayerName = "Default";
        layered.baseSortingOrder = 20;
        if (ctx.material != null) layered.layerMaterial = ctx.material;
        if (layered.library == null || !layered.Build(seed))
            Sprite(go.transform, "Disc", Square(), new Color(0.95f, 0.85f, 0.4f), 0.6f, 20, ctx);
        else layered.SetFrame(0);
        return go;
    }

    static Vector2 NearestBay(ShopLayout l, Vector2 at)
    {
        Vector2 best = at + Vector2.up;
        float bd = float.MaxValue;
        foreach (var b in l.bays)
        {
            float d = (new Vector2(b.x, b.y) - at).sqrMagnitude;
            if (d < bd) { bd = d; best = new Vector2(b.x, b.y); }
        }
        return best;
    }

    // ------------------------------------------------------------------ reading back (editor)

    // The room under `root` as a layout: positions and rotations of every ShopPiece, relative to the root.
    public static ShopLayout Read(Transform root, ShopLayout template)
    {
        var l = new ShopLayout
        {
            name = template.name, authored = true, width = template.width, height = template.height,
            doorX = template.doorX, counter = template.counter, clerk = template.clerk,
        };
        var bays = new List<ShopBay>();
        var workers = new List<ShopSpot>();
        var props = new List<ShopProp>();
        foreach (var p in root.GetComponentsInChildren<ShopPiece>(true))
        {
            Vector3 pos = root.InverseTransformPoint(p.transform.position);
            float rot = Round((Quaternion.Inverse(root.rotation) * p.transform.rotation).eulerAngles.z);
            float x = Round(pos.x), y = Round(pos.y);
            switch (p.role)
            {
                case ShopPiece.Role.Counter: l.counter = new ShopSpot { x = x, y = y, rotation = rot }; break;
                case ShopPiece.Role.Clerk: l.clerk = new ShopSpot { x = x, y = y }; break;
                case ShopPiece.Role.Door: l.doorX = x; break;
                case ShopPiece.Role.Bay: bays.Add(new ShopBay { x = x, y = y, rotation = rot, state = string.IsNullOrEmpty(p.state) ? "finished" : p.state }); break;
                case ShopPiece.Role.Worker: workers.Add(new ShopSpot { x = x, y = y }); break;
                case ShopPiece.Role.Prop:
                    var s = p.transform.localScale;
                    props.Add(new ShopProp { kind = p.propKind, x = x, y = y, rotation = rot, width = Round(Mathf.Abs(s.x)), height = Round(Mathf.Abs(s.y)) });
                    break;
            }
        }
        l.bays = bays.ToArray();
        l.workers = workers.ToArray();
        l.props = props.ToArray();
        return l;
    }

    static float Round(float v) => Mathf.Round(v * 10f) / 10f;

    // ------------------------------------------------------------------ primitives

    static ShopPiece Piece(GameObject go, ShopPiece.Role role)
    {
        var p = go.AddComponent<ShopPiece>();
        p.role = role;
        return p;
    }

    // A flat coloured box. `local`: position is in the parent's space (bay children); otherwise also the
    // parent's, which for room pieces is the room. Scale carries the size, so a prop's size reads back off it.
    static GameObject Quad(Transform parent, string name, Vector2 at, float rot, Vector2 size, Color c, int order,
                           Context ctx, bool solid = false, bool local = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(at.x, at.y, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Square();
        sr.color = c;
        sr.sortingOrder = order;
        if (ctx.material != null) sr.sharedMaterial = ctx.material;
        if (solid) go.AddComponent<BoxCollider2D>().size = Vector2.one;
        return go;
    }

    // A sprite scaled so its long side is `length` metres.
    static GameObject Sprite(Transform parent, string name, Sprite sprite, Color c, float length, int order, Context ctx)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : Square();
        sr.color = c;
        sr.sortingOrder = order;
        if (ctx.material != null) sr.sharedMaterial = ctx.material;
        var size = sr.sprite.bounds.size;
        float k = length / Mathf.Max(0.001f, Mathf.Max(size.x, size.y));
        go.transform.localScale = new Vector3(k, k, 1f);
        return go;
    }

    static void Solid(GameObject go, Vector2 worldSize)
    {
        var col = go.AddComponent<BoxCollider2D>();
        var s = go.transform.lossyScale;
        col.size = new Vector2(worldSize.x / Mathf.Max(0.001f, Mathf.Abs(s.x)), worldSize.y / Mathf.Max(0.001f, Mathf.Abs(s.y)));
    }

    static readonly Dictionary<string, Sprite> _sprites = new();

    static Sprite LoadSprite(string name)
    {
        if (_sprites.TryGetValue(name, out var s) && s != null) return s;
        s = Resources.Load<Sprite>(name);
        if (s == null)
        {
            var tex = Resources.Load<Texture2D>(name);
            if (tex != null) s = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 12.8f);
        }
        _sprites[name] = s;
        return s;
    }

    static Sprite _square;
    static Sprite Square()
    {
        if (_square != null) return _square;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();
        _square = UnityEngine.Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return _square;
    }

    // Placeholder engine: a block with two valve covers and an intake, seen from above (20 x 14 px).
    static Sprite _engine;
    static Sprite EngineSprite()
    {
        if (_engine != null) return _engine;
        const int W = 20, H = 14;
        var px = new Color32[W * H];
        var block = new Color32(70, 72, 78, 255); var cover = new Color32(180, 40, 35, 255);
        var intake = new Color32(170, 172, 178, 255); var dark = new Color32(30, 30, 34, 255);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                Color32 c = new Color32(0, 0, 0, 0);
                if (x >= 2 && x < 18 && y >= 1 && y < 13) c = block;
                if (x >= 3 && x < 17 && (y == 2 || y == 3 || y == 10 || y == 11)) c = cover;
                if (x >= 7 && x < 13 && y >= 5 && y < 9) c = intake;
                if ((x == 1 || x == 18) && y >= 3 && y < 11 && y % 2 == 1) c = dark;   // headers
                px[y * W + x] = c;
            }
        _engine = MakeSprite(px, W, H);
        return _engine;
    }

    // Placeholder wrecks until the sprites exist: a saloon, a winged sprint car or an open-wheeler, in white so
    // the series colour tints them, with crumple patches knocked out of the body.
    static readonly Dictionary<string, Sprite> _wrecks = new();
    static Sprite WreckSprite(string shape)
    {
        if (_wrecks.TryGetValue(shape, out var s) && s != null) return s;
        const int W = 64, H = 32;
        var px = new Color32[W * H];
        var body = new Color32(235, 235, 235, 255); var tyre = new Color32(25, 25, 28, 255);
        var dent = new Color32(150, 150, 150, 255);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                Color32 c = new Color32(0, 0, 0, 0);
                bool wheel = (x >= 10 && x < 20 || x >= 44 && x < 54) && (y < 5 || y >= 27);
                switch (shape)
                {
                    case "sprint":
                        if (x >= 6 && x < 58 && y >= 11 && y < 21) c = body;
                        if (x >= 34 && x < 54 && y >= 2 && y < 30) c = new Color32(220, 220, 220, 255);   // the wing
                        if (wheel) c = tyre;
                        break;
                    case "openwheel":
                        if (x >= 4 && x < 60 && y >= 12 && y < 20) c = body;
                        if ((x < 6 || x >= 58) && y >= 4 && y < 28) c = body;                             // wings
                        if (wheel) c = tyre;
                        break;
                    default:
                        if (x >= 3 && x < 61 && y >= 4 && y < 28) c = body;
                        if (wheel && (y < 4 || y >= 28)) c = tyre;
                        break;
                }
                if (c.a > 0 && c.r > 100 && ((x * 7 + y * 13) % 23 == 0 || (x > 46 && y < 14 && (x + y) % 3 == 0))) c = dent;
                px[y * W + x] = c;
            }
        s = MakeSprite(px, W, H);
        _wrecks[shape] = s;
        return s;
    }

    static Sprite MakeSprite(Color32[] px, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply();
        return UnityEngine.Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 12.8f);
    }

    // ------------------------------------------------------------------ dressing and words

    static readonly Color Wood = new Color(0.36f, 0.27f, 0.18f);

    static Color FloorColour(ShopKind k) => k switch
    {
        ShopKind.Fabricator => new Color(0.30f, 0.31f, 0.33f),
        ShopKind.EngineBuilder => new Color(0.22f, 0.23f, 0.26f),
        _ => new Color(0.33f, 0.28f, 0.21f),
    };

    static Color BayMarking(ShopKind k) => k == ShopKind.Junkyard ? new Color(0.29f, 0.25f, 0.19f) : new Color(0.38f, 0.38f, 0.30f);

    static Color PropColour(string kind) => kind switch
    {
        "toolbox" => new Color(0.75f, 0.12f, 0.10f),
        "rack" => new Color(0.45f, 0.47f, 0.52f),
        "tyres" => new Color(0.10f, 0.10f, 0.11f),
        "crate" => new Color(0.50f, 0.38f, 0.22f),
        "welder" => new Color(0.20f, 0.35f, 0.60f),
        _ => new Color(0.40f, 0.36f, 0.30f),   // bench
    };

    static string ClerkName(ShopKind k) => k switch
    {
        ShopKind.Fabricator => "Fabricator",
        ShopKind.EngineBuilder => "Engine builder",
        _ => "Yard owner",
    };

    static string[] Greetings(ShopKind k) => k switch
    {
        ShopKind.Fabricator => new[]
        {
            "Shells are hung, frames are jigged. What are we building you?",
            "Your own shop not keeping up? Happens. Have a look round.",
            "Everything leaves here plain. Paint's your problem.",
        },
        ShopKind.EngineBuilder => new[]
        {
            "Dyno's warm. What are you running at the moment?",
            "Don't touch the one on the stand, it isn't finished.",
            "Horsepower's cheap. Horsepower that lasts five hundred miles isn't.",
        },
        _ => new[]
        {
            "Everything's for sale. Nothing's got a price on it yet.",
            "Mind the dog. And the sharp bits. Mostly the sharp bits.",
            "Some of these raced last month. Some of them will again.",
        },
    };

    static string WorkerName(ShopKind k) => k == ShopKind.EngineBuilder ? "Engineer" : k == ShopKind.Fabricator ? "Fabricator" : "Yard hand";

    static string[] WorkerLines(ShopKind k) => k switch
    {
        ShopKind.Fabricator => new[] { "Mind the welder.", "Two more panels and she's hung.", "Measure twice, cut once, swear three times." },
        ShopKind.EngineBuilder => new[] { "Torquing the heads. Don't talk to me for a minute.", "This one's going on the dyno tonight.", "Cam's in. Now the fun part." },
        _ => new[] { "Pulled this one in last week.", "Frame's straighter than it looks.", "If it's got a wheel on it, it's for sale." },
    };
}
