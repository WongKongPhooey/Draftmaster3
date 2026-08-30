using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

// One-shot editor generator for the TeamGarage hub. Bakes the whole scene as REAL, persistent GameObjects
// (with saved sprite + material assets) so everything is visible and hand-movable in the editor — nothing is
// generated at play time. Re-running clears the previous "GarageContent" root and rebuilds, so run it before
// you start hand-placing things (a re-run wipes manual tweaks under that root).
//
// Menu: Tools/Draftmaster/Build Team Garage Scene. Operates on the currently open scene.
public static class TeamGarageBuilder
{
    const string ArtDir = "Assets/Art/Garage";
    const string SquarePath = ArtDir + "/GarageSquare.png";
    const string DiscPath = ArtDir + "/GarageDisc.png";
    const string MatPath = ArtDir + "/GarageUnlit.mat";
    const string ControlsPath = "Assets/Input/PlayerControl.inputactions";
    const string RootName = "GarageContent";

    [MenuItem("Tools/Draftmaster/Build Team Garage Scene")]
    static void Build()
    {
        EnsureFolders();
        var square = LoadOrCreateSprite(SquarePath, MakeSquareTex());
        var disc = LoadOrCreateSprite(DiscPath, MakeDiscTex(64));
        var mat = LoadOrCreateUnlitMaterial();

        var scene = EditorSceneManager.GetActiveScene();

        // Clear a previous build so this is repeatable.
        var existing = GameObject.Find(RootName);
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Team Garage");

        // --- floor + car pad ---
        MakeQuad(root.transform, "Floor", new Vector3(0f, 0f, 0.2f), new Vector3(26f, 18f, 1f),
                 square, mat, new Color(0.16f, 0.16f, 0.18f), 0);
        MakeQuad(root.transform, "CarPad", new Vector3(0f, 0f, 0.15f), new Vector3(9f, 5f, 1f),
                 square, mat, new Color(0.22f, 0.22f, 0.25f), 1);

        // --- team car (solid so the player walks around it) ---
        var car = MakeQuad(root.transform, "TeamCar", Vector3.zero, new Vector3(6f, 2.6f, 1f),
                           square, mat, new Color(0.85f, 0.20f, 0.22f), 5);
        var carCol = car.AddComponent<BoxCollider2D>();
        carCol.size = Vector2.one; // scaled by the transform to the sprite size
        LocationTitle.Attach(car, "TEAM CAR", 5f, "This year's chassis");

        // --- player ---
        var player = MakeDiscObject(root.transform, "OnFootPlayer", new Vector3(0f, -6f, 0f), disc, mat,
                                    new Color(0.95f, 0.90f, 0.40f), 25);
        var rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        var pcol = player.AddComponent<CircleCollider2D>();
        pcol.radius = 0.4f;
        var ofc = player.AddComponent<OnFootController>();
        ofc.faceMoveDirection = false; // placeholder disc has no facing to preserve
        var controls = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath);
        if (controls == null) Debug.LogWarning($"Build Team Garage: PlayerControl not found at {ControlsPath} — assign OnFootController.controlsAsset by hand.");
        ofc.controlsAsset = controls;

        // --- three crew stations ---
        MakeStation(root.transform, RoleStation.Role.Fabricator,         new Vector3(-8f, 2.5f, 0f), new Color(0.95f, 0.55f, 0.30f), disc, mat);
        MakeStation(root.transform, RoleStation.Role.EngineMechanic,     new Vector3( 8f, 2.5f, 0f), new Color(0.45f, 0.75f, 0.95f), disc, mat);
        MakeStation(root.transform, RoleStation.Role.SponsorshipManager, new Vector3( 0f, 6.5f, 0f), new Color(0.55f, 0.85f, 0.50f), disc, mat);

        // --- the desk laptop: the factory's way into the car sheet ---
        MakeLaptop(root.transform, new Vector3(-8f, -5f, 0f), square, mat);

        // --- the way out: the factory is otherwise a room with no exit ---
        MakeExitDoor(root.transform, new Vector3(8f, -8f, 0f), square, mat);

        // --- camera ---
        var cam = Camera.main;
        if (cam != null)
        {
            Undo.RecordObject(cam, "Build Team Garage");
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.09f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            var follow = cam.GetComponent<OnFootCameraFollow>();
            if (follow == null) follow = Undo.AddComponent<OnFootCameraFollow>(cam.gameObject);
            follow.target = player.transform;
        }
        else Debug.LogWarning("Build Team Garage: no Main Camera in the scene.");

        // --- systems: EventSystem for UI input ---
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Build Team Garage");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Build Team Garage: baked scene content under '" + RootName + "'. Save the scene to keep it.");
    }

    // ---- object builders ----

    static GameObject MakeQuad(Transform parent, string name, Vector3 pos, Vector3 scale,
                               Sprite sprite, Material mat, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = mat;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    static GameObject MakeDiscObject(Transform parent, string name, Vector3 pos,
                                     Sprite sprite, Material mat, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = mat;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    static void MakeStation(Transform parent, RoleStation.Role role, Vector3 pos, Color color,
                            Sprite disc, Material mat)
    {
        var go = MakeDiscObject(parent, "Station_" + role, pos, disc, mat, color, 20);
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.45f;
        var station = go.AddComponent<RoleStation>();
        station.role = role;
        station.speakerName = RoleStation.SpeakerFor(role);
        station.interactRange = 2.4f;
        LocationTitle.Attach(go, RoleStation.SpeakerFor(role).ToUpperInvariant(), 4.5f);
    }

    // The engineers' desk with the team laptop on it. Interacting opens the garage screen; BACK there
    // returns to this scene (GarageScreenLoader remembers). The desk is solid so the player walks round it.
    static void MakeLaptop(Transform parent, Vector3 pos, Sprite square, Material mat)
    {
        var desk = MakeQuad(parent, "Desk", pos, new Vector3(2.6f, 1.2f, 1f),
                            square, mat, new Color(0.30f, 0.26f, 0.22f), 8);
        desk.AddComponent<BoxCollider2D>().size = Vector2.one;

        MakeQuad(parent, "LaptopLid", pos + new Vector3(-0.35f, 0.15f, -0.05f), new Vector3(0.7f, 0.5f, 1f),
                 square, mat, new Color(0.13f, 0.14f, 0.16f), 9);
        MakeQuad(parent, "LaptopScreen", pos + new Vector3(-0.35f, 0.15f, -0.1f), new Vector3(0.55f, 0.36f, 1f),
                 square, mat, new Color(0.36f, 0.66f, 0.85f), 10);
        MakeQuad(parent, "LaptopBase", pos + new Vector3(0.35f, 0.05f, -0.05f), new Vector3(0.7f, 0.44f, 1f),
                 square, mat, new Color(0.22f, 0.23f, 0.26f), 9);

        // Co-located empty carries the interactable, kept off the visuals so nothing rotates them.
        var go = new GameObject("Laptop");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var laptop = go.AddComponent<LaptopInteractable>();
        laptop.interactRange = 2.4f;
        laptop.speakerName = "Laptop";
        laptop.turnsToFace = false;
        LocationTitle.Attach(go, "TEAM LAPTOP", 4f, "Setup, parts and the garage sheet");
    }

    // A door back to the title screen. Nothing else in this scene changes scene, and the pause menu only
    // arms itself where there's a TrackBuilder, so without this the factory is a dead end.
    static void MakeExitDoor(Transform parent, Vector3 pos, Sprite square, Material mat)
    {
        MakeQuad(parent, "ExitFrame", pos, new Vector3(2.2f, 3.0f, 1f),
                 square, mat, new Color(0.10f, 0.10f, 0.12f), 6);
        MakeQuad(parent, "ExitDoor", pos, new Vector3(1.8f, 2.6f, 1f),
                 square, mat, new Color(0.42f, 0.34f, 0.20f), 7);

        var go = new GameObject("ExitDoor");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var door = go.AddComponent<SceneDoorInteractable>();
        door.sceneName = GarageScreenLoader.TitleSceneName;
        door.interactRange = 2.4f;
        door.speakerName = "Exit";
        door.turnsToFace = false;
        LocationTitle.Attach(go, "EXIT", 4.5f, "Back to the title screen");
    }

    // ---- asset creation ----

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder(ArtDir)) AssetDatabase.CreateFolder("Assets/Art", "Garage");
    }

    static Texture2D MakeSquareTex()
    {
        var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        var px = new Color32[64];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px); tex.Apply();
        return tex;
    }

    static Texture2D MakeDiscTex(int s)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        Vector2 c = new(s * 0.5f, s * 0.5f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                px[y * s + x] = Vector2.Distance(new Vector2(x, y), c) < s * 0.46f
                    ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
        tex.SetPixels32(px); tex.Apply();
        return tex;
    }

    // Write a texture as a PNG asset and import it as a 1-world-unit sprite, then return the sprite.
    static Sprite LoadOrCreateSprite(string path, Texture2D tex)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = tex.width;   // sprite spans 1 world unit; transform scale sets real size
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Material LoadOrCreateUnlitMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (existing != null) return existing;

        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var mat = new Material(sh);
        AssetDatabase.CreateAsset(mat, MatPath);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
