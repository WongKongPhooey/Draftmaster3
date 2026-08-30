using UnityEngine;

// One person stood in the paddock, built the way every other person in this game is built.
//
// The weekend's venues need a lot of bodies — the crew chief at the box, the engineer at the dinette, the
// official at the top table, the sponsor's rep under the awning, the queue at the fence, a driver in every
// chair in the drivers' room — and they are all the same thing: a paper-doll sprite from the part library,
// normalised to a person's height, with an interactable bolted on if they have something to say.
//
// Same recipe as CareerPathNPCSpawner and the autograph fans, pulled out so the venue builders are not
// each carrying their own copy of it.
public static class PaddockPerson
{
    // The one figure everybody on foot is drawn to — the player, the pit crew, the autograph fans, the
    // paddock crowd: an 8px walk frame at the project's 12.8 px/m, so 0.625m. The world is metric for cars
    // and drawn smaller than 1:1 for people; standing a venue's bodies at a real 1.75m made them nearly
    // three times the size of everyone else, which is what the drivers' room and the hospitality awning
    // were showing. Never hand-set a metric height here — take it from the standard.
    public const float HeightM = PitCrewSpawner.OnFootPersonHeight;

    // Somebody sitting down. Same person, folded up — the seated head sits lower than the standing one.
    public const float SeatedHeightM = HeightM * 0.85f;
    public const float GroundZ = -0.4f;      // in front of the tarmac, behind the on-foot player
    public const string SortingLayer = "Default";
    public const int SortingOrder = 20;

    static NPCPartLibrary _library;
    static Material _unlit;

    public static NPCPartLibrary Library =>
        _library != null ? _library : (_library = Resources.Load<NPCPartLibrary>("NPC/NPCPartLibrary"));

    // A body, stood where it is put. `seed` fixes what they look like, so the same person is the same
    // person every time the scene is built.
    public static GameObject Spawn(Transform parent, Vector3 position, string name, int seed,
                                   float heightM = HeightM, Color? fallbackTint = null)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = position;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.None;   // interpolation would fight the placement

        Dress(go, seed, heightM, fallbackTint ?? new Color(0.75f, 0.76f, 0.80f));
        return go;
    }

    // A body with something to say. T is the interactable kind — WeekendVenueHost for a venue's host, or
    // plain NPCInteractable for somebody who is only scenery with a line.
    public static T SpawnTalker<T>(Transform parent, Vector3 position, string name, int seed,
                                   string speaker, string[] lines, float interactRange = 2.4f,
                                   float heightM = HeightM, Color? fallbackTint = null)
        where T : NPCInteractable
    {
        var body = Spawn(parent, position, name, seed, heightM, fallbackTint);
        var talker = body.AddComponent<T>();
        talker.speakerName = speaker;
        if (lines != null && lines.Length > 0) talker.lines = lines;
        talker.interactRange = interactRange;
        return talker;
    }

    static void Dress(GameObject go, int seed, float heightM, Color fallback)
    {
        var layered = go.AddComponent<NPCLayeredAppearance>();
        layered.library = Library;
        layered.layerMaterial = Unlit;
        layered.sortingLayerName = SortingLayer;
        layered.baseSortingOrder = SortingOrder;

        if (Library != null && layered.Build(seed))
        {
            float frameWorldH = Mathf.Max(0.01f, Library.frameHeight / Mathf.Max(1f, Library.pixelsPerUnit));
            go.transform.localScale = Vector3.one * (heightM / frameWorldH);
            return;
        }

        // No part library in this project yet: a coloured blob is still a person you can walk up to.
        // DestroyImmediate outside play mode so an edit-mode test can measure one of these without Unity
        // refusing the deferred Destroy — the size of a person is exactly the thing worth testing.
        if (Application.isPlaying) Object.Destroy(layered);
        else Object.DestroyImmediate(layered);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Blob(fallback, heightM);
        sr.sharedMaterial = Unlit;
        sr.sortingLayerName = SortingLayer;
        sr.sortingOrder = SortingOrder;
    }

    public static Material Unlit
    {
        get
        {
            if (_unlit != null) return _unlit;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            _unlit = new Material(shader) { name = "PaddockPersonUnlit" };
            return _unlit;
        }
    }

    static Sprite Blob(Color color, float metres)
    {
        const int s = 32;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        var centre = new Vector2(s * 0.5f, s * 0.5f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                px[y * s + x] = Vector2.Distance(new Vector2(x, y), centre) < s * 0.45f
                    ? (Color32)color : new Color32(0, 0, 0, 0);
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s / Mathf.Max(0.05f, metres));
    }
}
