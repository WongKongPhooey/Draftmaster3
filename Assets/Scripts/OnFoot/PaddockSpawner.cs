using System.Collections.Generic;
using UnityEngine;

// Builds a paddock alongside the longest straight of the pit lane: a tarmac rectangle populated with NPCs.
// A handful are conversational (NPCInteractable, walk up and press E); the rest wander a generated path
// (PaddockWalker) playing the walk cycle. Runs at scene start.
//
// The paddock area is auto-derived from the pit lane for now (a plain rectangle). Later it can be authored
// by hand — set defineAreaManually and drag a BoxCollider2D / corner transforms in, then read those instead.
public class PaddockSpawner : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("TrackBuilder that owns the pit lane spline. Auto-found if null.")]
    public TrackBuilder track;
    [Tooltip("On-foot NPC prefab (TaylorEmerson). A placeholder capsule is built if null.")]
    public GameObject npcPrefab;
    [Tooltip("Paddock surface material (e.g. Assets/Materials/TarmacMid). A grey unlit material is built if null.")]
    public Material tarmacMaterial;
    [Tooltip("Layered-NPC part library. If null, tries Resources/NPC/NPCPartLibrary. When absent or empty, NPCs fall back to the prefab's own sprite.")]
    public NPCPartLibrary partLibrary;

    [Header("Paddock Area")]
    [Tooltip("Depth of the paddock measured away from the pit lane, metres.")]
    public float paddockDepth = 30f;
    [Tooltip("Gap between the pit centerline and the near edge of the paddock, metres.")]
    public float pitGap = 8f;
    [Tooltip("Which side of the pit lane the paddock sits on. 0 = auto (side facing away from the main track), +1 = pit normal side, -1 = other side.")]
    public float side = 0f;
    [Tooltip("Fraction of the straight's length the paddock spans. 1 = full straight.")]
    [Range(0.1f, 1f)] public float lengthFraction = 1f;
    [Tooltip("UV tile size (m) for the tarmac texture across the paddock surface.")]
    public float surfaceTileMetres = 8f;
    [Tooltip("Sorting order for the paddock surface mesh. Keep below NPC sorting (20) so NPCs draw on top.")]
    public int surfaceSortingOrder = 1;

    [Header("NPCs")]
    [Tooltip("Total NPCs spawned in the paddock.")]
    public int totalNpcs = 34;
    [Tooltip("How many of them are conversational. The rest wander. Keep at or below the dialogue table size (10) so no two talkers repeat the same script.")]
    public int talkingNpcs = 9;
    [Tooltip("Give the wandering NPCs ambient one-liners they mutter as the player walks past (NPCAmbientChatter).")]
    public bool ambientChatter = true;
    [Tooltip("Extra multiplier on top of the height normalisation. 0 or 1 = leave at the standard figure.")]
    public float npcScale = 0f;
    [Tooltip("World height (m) a paddock walker is normalised to, matching the player and every other NPC. " +
             "0 = PitCrewSpawner.OnFootPersonHeight.")]
    public float npcHeightM = 0f;
    [Tooltip("Footprint (m, world space) the NPC blocks the player with: x = width across, y = depth. " +
             "Sized to the drawn body, not the sprite frame. Set either axis to 0 to leave the prefab's collider alone.")]
    public Vector2 npcColliderSize = new(0.30f, 0.17f);
    [Tooltip("Walk speed for wandering NPCs, units/sec.")]
    public float walkSpeed = 1.2f;

    // The compiled-in house style. Exposed so DialogueLibrary can layer authored pools on top of it, and so
    // the pool seeder can copy it into an asset for editing.
    public static string[][] BuiltInConversations => kDialogue;
    public static string[] BuiltInNames => kNames;

    // Sample dialogue for the talking NPCs. A line ending with "#player" is spoken by the player.
    static readonly string[][] kDialogue =
    {
        new[] { "Big crowd in today, eh? Should be a good one.", "First time at this track for me. #player", "You'll love it — fast and flowing. Watch turn one." },
        new[] { "Spare tyres are stacked behind the hauler if your crew needs 'em.", "Cheers, I'll let them know. #player", "No worries. Go get 'em out there." },
        new[] { "Heard the weather might turn this afternoon.", "Think it'll rain before the flag? #player", "Maybe. I'd keep the wets close, just in case." },
        new[] { "You're driving the 22 today, right?", "That's me. #player", "Bold livery! Hard to miss you out there. Good luck." },
        new[] { "Press wants a word after the session — don't disappear on me.", "I'll find you in the paddock. #player", "Appreciate it. Now go warm those tyres up." },
        new[] { "Scales say we're eight kilos heavy on the left rear.", "Can we get it out before the session? #player", "Working on it. Don't lean on that corner early, yeah?" },
        new[] { "Timing screens have you fourth on the long runs.", "Only fourth? #player", "On old tyres, mate. That's the good news." },
        new[] { "Kid over by the fence has had your poster since Friday.", "I'll go and sign it. #player", "Good lad. That's how you get a fanbase." },
        new[] { "Fuel's on the limit if we run the whole stint flat.", "So I lift and coast down the back? #player", "Two corners' worth. I'll call it on the radio." },
        new[] { "Our neighbours in the next bay are protesting something.", "Us? #player", "Everyone. They protest the weather, that lot." },
    };

    static readonly string[] kNames =
    {
        "Crew Chief", "Tyre Tech", "Spotter", "Fan", "Press Officer",
        "Engineer", "Data Analyst", "Autograph Hunter", "Fuel Man", "Team Manager",
    };

    void Start()
    {
        if (track == null) track = FindObjectOfType<TrackBuilder>();
        if (track == null) { Debug.LogError("PaddockSpawner: no TrackBuilder found."); enabled = false; return; }

        if (DialogueUI.Instance == null)
            new GameObject("DialogueUI").AddComponent<DialogueUI>();

        if (partLibrary == null) partLibrary = Resources.Load<NPCPartLibrary>("NPC/NPCPartLibrary");

        if (!ComputePaddockRect(out Vector3 center, out Vector3 along, out Vector3 outward, out float halfLen, out float halfDepth))
        {
            Debug.LogError("PaddockSpawner: could not derive a pit-lane straight to place the paddock.");
            enabled = false;
            return;
        }

        var root = new GameObject("Paddock").transform;
        root.SetParent(transform, false);

        // The tarmac surface is baked into the scene in the editor (Tools/Draftmaster/Bake Paddock
        // Surface) — nothing is generated at play time. Only the NPCs are runtime-spawned.
        if (transform.Find("PaddockSurface") == null)
            Debug.LogWarning("PaddockSpawner: no baked PaddockSurface in the scene — run Tools/Draftmaster/Bake Paddock Surface.", this);

        SpawnNpcs(root, center, along, outward, halfLen, halfDepth);
    }

    // Walks the pit segments to find the longest Straight, then frames a rectangle alongside it on `side`.
    bool ComputePaddockRect(out Vector3 center, out Vector3 along, out Vector3 outward, out float halfLen, out float halfDepth)
    {
        center = along = outward = Vector3.zero; halfLen = halfDepth = 0f;

        var info = track.track;
        if (info == null || !info.hasPitLane || info.pitSegments == null || info.pitSegments.Length == 0) return false;

        var pitSamples = track.SamplePitCenterline();
        if (pitSamples.Count < 2) return false;

        float cum = 0f, bestStart = 0f, bestLen = 0f;
        foreach (var seg in info.pitSegments)
        {
            if (seg.type == TrackInfoV2.SegmentType.Straight && seg.length > bestLen)
            {
                bestLen = seg.length;
                bestStart = cum;
            }
            cum += seg.length;
        }
        // No explicit straight? Fall back to the whole pit lane.
        if (bestLen <= 0f) { bestStart = 0f; bestLen = pitSamples[pitSamples.Count - 1].distance; }
        if (bestLen <= 0f) return false;

        float midD = bestStart + bestLen * 0.5f;
        var mid = track.SamplePitAt(midD, pitSamples);

        // Pit-local axes → world.
        along = track.transform.TransformDirection(new Vector3(mid.tangent.x, mid.tangent.y, 0f)).normalized;
        Vector3 normalW = track.transform.TransformDirection(new Vector3(mid.normal.x, mid.normal.y, 0f)).normalized;
        Vector3 midWorld = track.transform.TransformPoint(new Vector3(mid.position.x, mid.position.y, 0f));

        // Pick the side. Auto (side==0): face away from the main racetrack so the paddock sits behind the pits.
        float sign;
        if (Mathf.Approximately(side, 0f))
        {
            var mainSamples = track.SampleCenterline();
            float dPlus = NearestMainDist(midWorld + normalW * paddockDepth, mainSamples);
            float dMinus = NearestMainDist(midWorld - normalW * paddockDepth, mainSamples);
            sign = dPlus >= dMinus ? 1f : -1f; // whichever probe lands farther from the track
        }
        else sign = Mathf.Sign(side);
        outward = normalW * sign;

        halfLen = bestLen * lengthFraction * 0.5f;
        halfDepth = paddockDepth * 0.5f;
        center = midWorld + outward * (pitGap + halfDepth);
        return true;
    }

    // Min distance (world) from a probe point to the main centerline sample set.
    float NearestMainDist(Vector3 worldProbe, List<TrackBuilder.Sample> mainSamples)
    {
        if (mainSamples == null || mainSamples.Count == 0) return 0f;
        float best = float.MaxValue;
        foreach (var s in mainSamples)
        {
            Vector3 w = track.transform.TransformPoint(new Vector3(s.position.x, s.position.y, 0f));
            float d = (w - worldProbe).sqrMagnitude;
            if (d < best) best = d;
        }
        return Mathf.Sqrt(best);
    }

    GameObject BuildSurface(Transform root, Vector3 center, Vector3 along, Vector3 outward, float halfLen, float halfDepth)
    {
        var go = new GameObject("PaddockSurface");
        go.transform.SetParent(root, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = ResolveSurfaceMaterial();
        mr.sortingOrder = surfaceSortingOrder;

        // Sit coplanar with the ground (z=0) and rely on sorting order to layer it: ground(0) < paddock(1) < cars(5).
        // A negative z would pull the opaque quad in FRONT of the player/car (same z=0 plane) and hide them under it.
        float z = 0f;
        Vector3 a = along * halfLen, d = outward * halfDepth;
        Vector3 v0 = center - a - d + Vector3.forward * z;
        Vector3 v1 = center + a - d + Vector3.forward * z;
        Vector3 v2 = center + a + d + Vector3.forward * z;
        Vector3 v3 = center - a + d + Vector3.forward * z;

        float uS = (halfLen * 2f) / Mathf.Max(0.01f, surfaceTileMetres);
        float vS = (halfDepth * 2f) / Mathf.Max(0.01f, surfaceTileMetres);

        var mesh = new Mesh { name = "PaddockSurface" };
        mesh.SetVertices(new List<Vector3> { v0, v1, v2, v3 });
        mesh.SetUVs(0, new List<Vector2> { new(0, 0), new(uS, 0), new(uS, vS), new(0, vS) });
        // Double-sided: emit both windings so the quad is never backface-culled regardless of camera side.
        mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 }, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
        return go;
    }

#if UNITY_EDITOR
    // Bakes the paddock tarmac into the scene as a persistent object (child "PaddockSurface" of the
    // spawner), replacing the old play-time generation. Re-run after changing the paddock layout fields.
    [UnityEditor.MenuItem("Tools/Draftmaster/Bake Paddock Surface")]
    static void BakePaddockSurface()
    {
        var sp = FindObjectOfType<PaddockSpawner>();
        if (sp == null) { Debug.LogError("Bake Paddock Surface: no PaddockSpawner in the open scene."); return; }
        if (sp.track == null) sp.track = FindObjectOfType<TrackBuilder>();
        if (sp.track == null) { Debug.LogError("Bake Paddock Surface: no TrackBuilder in the open scene."); return; }

        if (!sp.ComputePaddockRect(out Vector3 center, out Vector3 along, out Vector3 outward, out float halfLen, out float halfDepth))
        {
            Debug.LogError("Bake Paddock Surface: could not derive a pit-lane straight to place the paddock.");
            return;
        }

        var old = sp.transform.Find("PaddockSurface");
        if (old != null) DestroyImmediate(old.gameObject);

        var go = sp.BuildSurface(sp.transform, center, along, outward, halfLen, halfDepth);

        // A baked object must reference an ASSET material — a runtime-built fallback is not saved with
        // the scene and would come back missing (magenta) after an editor restart.
        var mat = go.GetComponent<MeshRenderer>().sharedMaterial;
        if (mat == null || !UnityEditor.AssetDatabase.Contains(mat))
            Debug.LogWarning("Bake Paddock Surface: surface material is not an asset — assign 'tarmacMaterial' (or the track's pit/surface material) and re-bake, or it will go missing on reload.", go);

        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Bake Paddock Surface");
        UnityEditor.EditorUtility.SetDirty(sp);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log($"Bake Paddock Surface: baked '{go.name}' under '{sp.name}'.", go);
    }
#endif

    void SpawnNpcs(Transform root, Vector3 center, Vector3 along, Vector3 outward, float halfLen, float halfDepth)
    {
        int total = Mathf.Max(0, totalNpcs);
        int talkers = Mathf.Clamp(talkingNpcs, 0, total);

        for (int i = 0; i < total; i++)
        {
            float l = Random.Range(-halfLen * 0.9f, halfLen * 0.9f);
            float dd = Random.Range(-halfDepth * 0.9f, halfDepth * 0.9f);
            Vector3 pos = center + along * l + outward * dd;
            pos.z = -0.1f; // toward the camera so sprites draw in front of the tarmac surface

            bool talking = i < talkers;
            var npc = BuildNpc(root, pos, talking);

            if (talking)
            {
                // Dialogue comes from the library, not straight from kDialogue: a track's own DialoguePool
                // asset adds to (or replaces) the house style, so the paddock can sound like this circuit.
                var conversations = DialogueLibrary.Conversations(Draftmaster.Chatter.ConversationKind.PaddockCrew, kDialogue);
                var lines = conversations[i % conversations.Length];

                var inter = npc.AddComponent<NPCInteractable>();
                inter.lines = lines;
                inter.speakerName = DialogueLibrary.SpeakerNameFor(
                    Draftmaster.Chatter.ConversationKind.PaddockCrew, lines, kNames, i);
            }
            else
            {
                // Walkers are the crowd. They carry no interact prompt, but they do mutter at a passing
                // player — that's what stops a bigger paddock reading as a bigger set of props. Stagger
                // the first bark per NPC so the crowd doesn't all fire the moment the player walks in.
                if (ambientChatter)
                {
                    var chat = npc.AddComponent<NPCAmbientChatter>();
                    chat.area = Draftmaster.Chatter.ChatterArea.Paddock;
                    chat.minRepeatSeconds = Random.Range(18f, 40f);
                }

                var walker = npc.AddComponent<PaddockWalker>();
                walker.speed = walkSpeed * Random.Range(0.8f, 1.25f); // a crowd doesn't march in step
                walker.Configure(center, along, outward, halfLen, halfDepth);
            }
        }
    }

    GameObject BuildNpc(Transform root, Vector3 pos, bool talking)
    {
        GameObject npc;
        if (npcPrefab != null)
        {
            npc = Instantiate(npcPrefab, pos, Quaternion.identity, root);

            // Strip anything that would let it drive itself / take input — spawner owns its behaviour.
            var mv = npc.GetComponent<MovementOnFoot>(); if (mv != null) mv.enabled = false;
            var pi = npc.GetComponent<UnityEngine.InputSystem.PlayerInput>(); if (pi != null) pi.enabled = false;
            var ofc = npc.GetComponent<OnFootController>(); if (ofc != null) Destroy(ofc);

            var rb = npc.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.gravityScale = 0f; rb.bodyType = RigidbodyType2D.Kinematic; }

            // Unlit material so sprites render under the 3D URP renderer (Sprite-Lit-Default goes black without a Light2D).
            var unlit = UnlitSpriteMaterial();
            var baseSr = npc.GetComponent<SpriteRenderer>();
            var anim = npc.GetComponent<Animator>();

            // Try to build a random layered outfit. We drive frames ourselves, so when it succeeds the prefab's
            // own SpriteRenderer + Animator are switched off and the layer renderers take over.
            var layered = npc.AddComponent<NPCLayeredAppearance>();
            layered.library = partLibrary;
            layered.layerMaterial = unlit;
            bool built = layered.Build();

            if (built)
            {
                if (baseSr != null) baseSr.enabled = false;
                if (anim != null) anim.enabled = false;
            }
            else
            {
                // No part art yet: fall back to the prefab's own sprite + Animator so the scene still runs.
                Destroy(layered);
                if (baseSr != null) baseSr.sharedMaterial = unlit;
            }
        }
        else
        {
            npc = BuildPlaceholder(talking ? "Crew" : "Walker", pos, talking ? new Color(1f, 0.6f, 0.4f) : new Color(0.7f, 0.8f, 0.5f));
            npc.transform.SetParent(root, false);
            npc.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        }

        // Size the walker to a real height rather than inheriting whatever scale the source prefab carries.
        //
        // These used to come out right by accident: the prefab was scaled 8 to compensate for an 8px sprite
        // imported at 100 px/unit, and this spawner left that scale alone. Once the prefab was corrected to
        // scale 1 (its sprite now being imported at the project standard), the paddock walkers shrank to an
        // eighth. The paper-doll layers are built through NPCPartLibrary.pixelsPerUnit, not the PNG's import
        // setting, so the only reliable size is the one derived from the library -- which is exactly what
        // every other NPC spawner already does, and why the conversational NPCs were unaffected.
        float target = npcHeightM > 0f ? npcHeightM : PitCrewSpawner.OnFootPersonHeight;
        float frameWorldH = partLibrary != null
            ? partLibrary.frameHeight / Mathf.Max(1f, partLibrary.pixelsPerUnit)
            : 0f;
        if (frameWorldH > 0.0001f)
            npc.transform.localScale = Vector3.one * (target / frameWorldH) * (npcScale > 0f ? npcScale : 1f);
        else if (npcScale > 0f)
            npc.transform.localScale = Vector3.one * npcScale;

        FitCollider(npc);

        npc.name = talking ? "PaddockNPC_Talk" : "PaddockNPC_Walk";
        return npc;
    }

    // The prefab's collider is authored against the prefab's own 1:1 scale (0.64 x 0.32 for a 0.625m
    // figure). The height normalisation above rescales the whole NPC -- with the part library at
    // 100 px/unit and the figure at 12.8 px/m that's ~7.8x -- and a Collider2D scales with its
    // transform, so every paddock NPC was blocking a 5m x 2.5m box floating 0.6m off its own feet.
    // The sprites never showed it because the paper-doll layers are built at the library's PPU, so the
    // scale cancels out for them and only the collider was left oversized.
    //
    // Re-derive the footprint in world metres and divide the transform scale back out, so the box stays
    // the same size on the ground whatever the library's pixel settings are.
    void FitCollider(GameObject npc)
    {
        if (npcColliderSize.x <= 0f || npcColliderSize.y <= 0f) return;

        float s = Mathf.Abs(npc.transform.localScale.x);
        if (s < 0.0001f) return;

        var box = npc.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.size = npcColliderSize / s;
            box.offset = Vector2.zero; // prefab offsets the box up the body; centred is right for a top-down footprint
            return;
        }

        var circle = npc.GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            circle.radius = Mathf.Max(npcColliderSize.x, npcColliderSize.y) * 0.5f / s;
            circle.offset = Vector2.zero;
        }
    }

    GameObject BuildPlaceholder(string name, Vector3 pos, Color color)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        int s = 32;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        Vector2 c = new(s * 0.5f, s * 0.5f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                px[y * s + x] = Vector2.Distance(new Vector2(x, y), c) < s * 0.45f ? (Color32)color : new Color32(0, 0, 0, 0);
        tex.SetPixels32(px); tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
        sr.sortingOrder = 20;
        return go;
    }

    Material _unlitSprite;
    Material UnlitSpriteMaterial()
    {
        if (_unlitSprite != null) return _unlitSprite;
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        _unlitSprite = new Material(sh);
        return _unlitSprite;
    }

    // Pick a URP-compatible tarmac material. The inspector-assigned one may be a legacy built-in (Standard) material
    // (e.g. TarmacMid) that renders wrong/white under URP, so prefer the track's own proven tarmac, then a flat fallback.
    Material ResolveSurfaceMaterial()
    {
        if (IsUrp(tarmacMaterial)) return tarmacMaterial;
        if (track != null)
        {
            if (IsUrp(track.pitSurfaceMaterial)) return track.pitSurfaceMaterial;
            if (IsUrp(track.surfaceMaterial)) return track.surfaceMaterial;
        }
        return BuildFallbackTarmac();
    }

    static bool IsUrp(Material m)
    {
        if (m == null || m.shader == null) return false;
        string n = m.shader.name;
        return n.Contains("Universal") || n.Contains("Unlit") || n.Contains("Sprites");
    }

    Material BuildFallbackTarmac()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.18f, 0.18f, 0.19f));
        else if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(0.18f, 0.18f, 0.19f));
        return m;
    }
}
