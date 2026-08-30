using System.IO;
using UnityEditor;
using UnityEngine;

// One-shot generator for the authored RV interior prefab (Resources/OnFoot/RVInterior.prefab).
// Generates the motorhome room as hand-editable SpriteRenderer children, so the interior can be
// laid out in Prefab Mode instead of in code. After building, the prefab is YOURS: open it, move
// furniture, swap the white-square sprites for real art — Build Prefab refuses to overwrite.
// PitLaneStart instantiates the prefab when it exists (see RVInterior's class comment).
//
// Authoring frame (the "Interior" child): (0,0) is where the player spawns; +Y points at the
// doorway. The RV's door is on its right SIDE (local +X on the exterior prefab, mid-way toward the
// cab), so in this frame the room reads sideways: local -X = toward the RV's cab, local +X = toward
// its rear, and the door gap sits off-centre in the front wall on the cab side. Keep art z between
// the -2.0 black mask and the -2.5 player, matching RVInterior's z constants (floor -2.2,
// walls -2.25, props -2.3).
public static class RVInteriorPrefabBuilder
{
    const string PrefabPath = "Assets/Resources/OnFoot/RVInterior.prefab";
    const string RVPrefabPath = "Assets/Prefabs/Environment/RV.prefab";
    const string MaterialPath = "Assets/Resources/OnFoot/RVInteriorSprite.mat";
    const string SpritePath = "Assets/Textures/Environment/WhiteSquare.png";
    // 4x4 texture at 4 px/unit = one world unit across, which is what Quad()'s metres-into-localScale
    // sizing depends on. See GetOrCreateWhiteSprite.
    const float UnitSpritePPU = 4f;

    // Exterior body footprint (RV-local): 3.95 wide x 9.93 long, centred on (0,-2), cab at +Y.
    // The spawn marker sits at the body centre (0,-2), so in the interior frame the floor is
    // centred on the origin: RoomLength spans the RV's length, RoomWidth its width.
    const float RoomLength = 9.93f;   // interior local X (RV long axis)
    const float RoomWidth = 3.95f;    // interior local Y (across the RV, toward the door)
    // Door gap in the front (+Y) wall, interior local X. Matches the exterior collider notch
    // (RV-local y 0.51..1.90 on the +X edge) under the mapping localX = -(rvY - markerY).
    const float DoorGapMin = -3.9f, DoorGapMax = -2.51f;
    // Mirror RVInterior's serialized defaults (kept in sync with the exterior shape).
    const float RoomFront = 2.3f, RoomBack = 1.85f, DoorWidth = 1.4f, RoomWidthParam = 9.6f;
    const float FloorZ = -2.2f, WallZ = -2.25f, PropZ = -2.3f;
    const float WallThickness = 0.25f;

    [MenuItem("Draftmaster/RV Interior/Build Prefab")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            Debug.LogWarning($"RVInterior prefab already exists at {PrefabPath} — it holds your hand-authored room. " +
                             "Edit it in Prefab Mode, or Force Rebuild to start over.");
            return;
        }
        BuildInternal();
    }

    // Note: rebuilds overwrite in place (SaveAsPrefabAsset over the existing path) rather than
    // delete-and-recreate — deleting would change the asset's GUID and sever every placed instance.
    [MenuItem("Draftmaster/RV Interior/Force Rebuild Prefab (loses hand edits)")]
    public static void ForceRebuild()
    {
        BuildInternal();
    }

    [MenuItem("Draftmaster/RV Interior/Build RV Prefab (exterior)")]
    public static void BuildRV()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RVPrefabPath) != null)
        {
            Debug.LogWarning($"RV prefab already exists at {RVPrefabPath} — it holds your hand-authored exterior. " +
                             "Edit it in Prefab Mode, or Force Rebuild RV Prefab to start over.");
            return;
        }
        BuildRVInternal();
    }

    [MenuItem("Draftmaster/RV Interior/Force Rebuild RV Prefab (loses hand edits)")]
    public static void ForceRebuildRV()
    {
        BuildRVInternal();
    }

    // Scene-placeable RV exterior, regenerating the hand-reshaped prefab: a 3.95 x 9.93 body with
    // the cab at +Y and the door on the right side (+X edge, RV-local y 0.51..1.90). RVExterior's
    // serialized door fields carry the doorway's position/facing; the SpawnPoint_RV marker sits at
    // the body centre so the interior's inside-box is symmetric about it.
    static void BuildRVInternal()
    {
        Sprite white = GetOrCreateWhiteSprite();
        Material mat = GetOrCreateSpriteMaterial();
        if (white == null || mat == null)
        {
            Debug.LogError($"RVInteriorPrefabBuilder: missing build inputs (sprite={(white != null)}, material={(mat != null)}) — RV prefab not built.");
            return;
        }

        var root = new GameObject("RV");
        try
        {
            root.AddComponent<RVExterior>(); // door fields default to the side door (+X, centre (1.73, 1.21))

            // Placeholder body: sits in front of the z=0 ground but behind the walking player and the
            // interior's -2.0 mask. The cab stripe marks the front (+Y) so the facing reads at a glance.
            Quad(white, mat, root.transform, "Body", new Vector2(0f, -2f), new Vector2(3.95f, 9.93f), -0.5f, new Color(0.80f, 0.80f, 0.84f));
            Quad(white, mat, root.transform, "CabStripe", new Vector2(0f, 2.35f), new Vector2(3.82f, 0.7f), -0.52f, new Color(0.20f, 0.22f, 0.28f));

            // Solid shell: everything left of the door wall as one box, plus the right edge split
            // above/below the door notch (y 0.51..1.90). The notch reaches to x 1.5 so the player's
            // collider can cross the interior's enter threshold (roomFront - hysteresis past the
            // marker). RVInterior turns these off while the player is inside.
            ColliderBox(root.transform, "ColliderBody", new Vector2(-0.25f, -2f), new Vector2(3.5f, 9.93f));
            ColliderBox(root.transform, "ColliderFrontL", new Vector2(1.76f, 2.43f), new Vector2(0.43f, 1.06f));
            ColliderBox(root.transform, "ColliderFrontR", new Vector2(1.72f, -3.23f), new Vector2(0.47f, 7.48f));

            // Marker at the body centre: the interior room is centred on it, and RVInterior's
            // inside-box (symmetric about the marker) then matches the body footprint.
            var spawn = new GameObject("SpawnPoint_RV");
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.localPosition = new Vector3(0f, -2f, 0f);
            var marker = spawn.AddComponent<PlayerSpawnPoint>();
            marker.label = "RV";

            Directory.CreateDirectory(Path.GetDirectoryName(RVPrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, RVPrefabPath);
            Debug.Log($"RV prefab built at {RVPrefabPath}. Place it in a scene and rotate as needed — the gizmo " +
                      "arrow shows the side door's facing. Drag SpawnPoint_RV to move where the player stands inside.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static void BuildInternal()
    {
        Sprite white = GetOrCreateWhiteSprite();
        Material mat = GetOrCreateSpriteMaterial();
        if (white == null || mat == null)
        {
            Debug.LogError($"RVInteriorPrefabBuilder: missing build inputs (sprite={(white != null)}, material={(mat != null)}) — prefab not built.");
            return;
        }

        var root = new GameObject("RVInterior");
        try
        {
            var rv = root.AddComponent<RVInterior>();
            rv.roomWidth = RoomWidthParam;
            rv.roomBack = RoomBack;
            rv.roomFront = RoomFront;
            rv.doorWidth = DoorWidth;

            var interior = new GameObject("Interior").transform;
            interior.SetParent(root.transform, false);

            float halfL = RoomLength * 0.5f;   // 4.965 — cab end at -X, rear at +X
            float halfW = RoomWidth * 0.5f;    // 1.975 — door wall at +Y
            float doorCentre = (DoorGapMin + DoorGapMax) * 0.5f;
            float doorGapWidth = DoorGapMax - DoorGapMin;

            Quad(white, mat, interior, "Floor", Vector2.zero, new Vector2(RoomLength, RoomWidth), FloorZ, new Color(0.60f, 0.48f, 0.33f));

            // Walls are solid so the player can only leave through the side door; the colliders
            // toggle with InsideView, so they never block anything while the interior is hidden.
            // Front (+Y) wall = the RV's right side, carrying the door gap on the cab side.
            var wallColor = new Color(0.28f, 0.22f, 0.17f);
            float wallSpan = RoomLength + WallThickness;
            float segLW = DoorGapMin - (-halfL - WallThickness * 0.5f);            // cab-side segment
            float segRW = (halfL + WallThickness * 0.5f) - DoorGapMax;             // rear-side segment
            Quad(white, mat, interior, "WallFrontL", new Vector2(DoorGapMin - segLW * 0.5f, halfW), new Vector2(segLW, WallThickness), WallZ, wallColor, withCollider: true);
            Quad(white, mat, interior, "WallFrontR", new Vector2(DoorGapMax + segRW * 0.5f, halfW), new Vector2(segRW, WallThickness), WallZ, wallColor, withCollider: true);
            Quad(white, mat, interior, "WallBack", new Vector2(0f, -halfW), new Vector2(wallSpan, WallThickness), WallZ, wallColor, withCollider: true);
            Quad(white, mat, interior, "WallLeft", new Vector2(-halfL, 0f), new Vector2(WallThickness, RoomWidth), WallZ, wallColor, withCollider: true);   // cab end
            Quad(white, mat, interior, "WallRight", new Vector2(halfL, 0f), new Vector2(WallThickness, RoomWidth), WallZ, wallColor, withCollider: true);   // rear end

            // Furnishings, motorhome layout: cab up front (-X), bed across the tail (+X), galley
            // along the back wall, dinette by the door. All placeholder blocks — restyle in Prefab
            // Mode. Kept clear of the origin (0,0), where the player spawns.
            Quad(white, mat, interior, "Doormat", new Vector2(doorCentre, halfW - 0.45f), new Vector2(doorGapWidth, 0.5f), PropZ, new Color(0.35f, 0.30f, 0.24f));
            Quad(white, mat, interior, "Rug", new Vector2(-1.2f, 0.55f), new Vector2(2.0f, 1.5f), WallZ, new Color(0.55f, 0.20f, 0.18f));
            Quad(white, mat, interior, "Table", new Vector2(-1.2f, 1.15f), new Vector2(1.2f, 1.0f), PropZ, new Color(0.48f, 0.34f, 0.22f));
            Quad(white, mat, interior, "Bed", new Vector2(4.0f, 0f), new Vector2(1.6f, 2.7f), PropZ, new Color(0.78f, 0.80f, 0.86f));
            Quad(white, mat, interior, "BedPillow", new Vector2(4.55f, 0f), new Vector2(0.5f, 2.4f), PropZ - 0.02f, new Color(0.92f, 0.93f, 0.96f));
            Quad(white, mat, interior, "Counter", new Vector2(1.2f, -1.5f), new Vector2(2.4f, 0.7f), PropZ, new Color(0.70f, 0.71f, 0.74f));

            // Front cab: driver's seat (RV's left side = local -Y here) with the satnav ahead of it.
            Vector2 unit = new(-4.45f, -1.15f);
            Quad(white, mat, interior, "DriverSeat", new Vector2(-3.85f, -1.15f), new Vector2(1.0f, 1.1f), PropZ, new Color(0.16f, 0.16f, 0.18f));
            Quad(white, mat, interior, "PassengerSeat", new Vector2(-3.85f, 1.15f), new Vector2(1.0f, 1.1f), PropZ, new Color(0.16f, 0.16f, 0.18f));
            Quad(white, mat, interior, "SatnavBody", unit, new Vector2(0.44f, 0.62f), PropZ - 0.02f, new Color(0.08f, 0.08f, 0.09f));
            Quad(white, mat, interior, "SatnavScreen", unit, new Vector2(0.30f, 0.44f), PropZ - 0.04f, new Color(0.20f, 0.72f, 0.55f));

            // Co-located empty child carries the interactable, separate from the visuals so the
            // face-each-other rotation on interact never spins the device.
            var sat = new GameObject("Satnav");
            sat.transform.SetParent(interior, false);
            sat.transform.localPosition = new Vector3(unit.x, unit.y, PropZ);
            sat.AddComponent<SatnavInteractable>().interactRange = 2f;

            // Laptop on the dinette table: the way into the garage screen from a race weekend. Same
            // visuals-plus-empty split as the satnav. RVInterior generates one at runtime for any room
            // that lacks it, so an older prefab still gets a laptop — this just bakes it into new ones.
            Vector2 desk = new(-1.2f, 1.15f);   // the Table above
            Quad(white, mat, interior, "LaptopLid", desk + new Vector2(-0.16f, 0f), new Vector2(0.30f, 0.52f), PropZ - 0.02f, new Color(0.13f, 0.14f, 0.16f));
            Quad(white, mat, interior, "LaptopScreen", desk + new Vector2(-0.16f, 0f), new Vector2(0.22f, 0.44f), PropZ - 0.04f, new Color(0.36f, 0.66f, 0.85f));
            Quad(white, mat, interior, "LaptopBase", desk + new Vector2(0.06f, 0f), new Vector2(0.26f, 0.52f), PropZ - 0.02f, new Color(0.22f, 0.23f, 0.26f));

            var laptop = new GameObject("Laptop");
            laptop.transform.SetParent(interior, false);
            laptop.transform.localPosition = new Vector3(desk.x, desk.y, PropZ);
            var laptopIx = laptop.AddComponent<LaptopInteractable>();
            laptopIx.interactRange = 1.6f;
            laptopIx.speakerName = "Laptop";
            laptopIx.turnsToFace = false;

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"RVInterior prefab built at {PrefabPath}. Open it in Prefab Mode to edit the room " +
                      "(+Y = the side doorway, -X = the cab; player spawns at the Interior origin).");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // 1x1-unit white sprite tinted per renderer; localScale sets the block's size in metres.
    // withCollider adds a solid BoxCollider2D auto-fit to the sprite (so it scales with the block).
    // `size` is metres. The sprite is a unit quad (UnitSpritePPU), so metres go straight into localScale.
    static SpriteRenderer Quad(Sprite sprite, Material mat, Transform parent, string name, Vector2 centre, Vector2 size, float z, Color tint, bool withCollider = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(centre.x, centre.y, z);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = mat;
        sr.color = tint;
        if (withCollider) go.AddComponent<BoxCollider2D>();
        return sr;
    }

    // Invisible solid box, local units (unlike Quad, no transform scale involved).
    static void ColliderBox(Transform parent, string name, Vector2 centre, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(centre.x, centre.y, 0f);
        go.AddComponent<BoxCollider2D>().size = size;
    }

    // The unit quad every piece of this prefab is drawn with. Quad() sizes a piece by putting metres
    // straight into localScale, which only holds while this sprite covers exactly 1x1 world unit — so a
    // 4x4 texture MUST import at 4 px/unit.
    //
    // It is not world art and must not be retargeted to the project's 12.8 px/m standard: doing that once
    // already shrank the RV's body and its interior floor to 4/12.8 = 0.3125 of their size, with the
    // collider shell (authored in plain metres) left at full size around a motorhome a third the size of
    // its own doorway. So an existing asset is checked and repaired rather than trusted.
    static Sprite GetOrCreateWhiteSprite()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (existing != null)
        {
            var imp = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (imp != null && (imp.spritePixelsPerUnit != UnitSpritePPU
                                || imp.spriteImportMode != SpriteImportMode.Single))
            {
                Debug.LogWarning($"RVInteriorPrefabBuilder: {SpritePath} was imported at " +
                                 $"{imp.spritePixelsPerUnit} px/unit ({imp.spriteImportMode}); it is a unit quad, " +
                                 $"putting it back to {UnitSpritePPU} px/unit (Single).");
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = UnitSpritePPU;
                imp.SaveAndReimport();
                existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            }
            return existing;
        }

        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(SpritePath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single; // project default is Multiple, which yields no Sprite sub-asset
        importer.spritePixelsPerUnit = UnitSpritePPU; // 4x4 texture -> exactly 1x1 world unit
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    static Material GetOrCreateSpriteMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existing != null) return existing;

        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) { Debug.LogError("RVInteriorPrefabBuilder: no sprite shader found."); return null; }

        Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
        AssetDatabase.CreateAsset(new Material(sh), MaterialPath);
        return AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
    }
}
