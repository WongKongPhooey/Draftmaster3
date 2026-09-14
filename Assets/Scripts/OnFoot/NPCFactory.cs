using UnityEngine;
using UnityEngine.InputSystem;

// Turns the on-foot player prefab into a standing, talkable NPC: strip anything that would drive it,
// freeze the walk cycle at an idle pose, swap to the unlit sprite shader this scene's renderer needs,
// then attach whatever kind of speaker the caller wants.
//
// Both spawn paths use this — PlacedNPC (editor-placed) and the procedural spawners — so a body built by
// hand and a body built from geometry are the same body.
public static class NPCFactory
{
    // One unlit sprite material for every NPC in the scene. It used to be a fresh Material per NPC,
    // which is both a leak and a batch-breaker: two sprites can only be drawn together if they share a
    // material, so a crowd of a hundred was a hundred materials and a hundred draw calls that had no
    // reason to be separate. Nothing per-NPC is stored on it — the outfit colours live on the
    // SpriteRenderers — so one instance serves the lot.
    static Material _unlitSprite;
    public static Material UnlitSpriteMaterial
    {
        get
        {
            if (_unlitSprite != null) return _unlitSprite;
            Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) return null;
            _unlitSprite = new Material(sh) { name = "NPC Unlit Sprite (shared)", hideFlags = HideFlags.DontSave };
            return _unlitSprite;
        }
    }

    // Clone the prefab and make it inert. No dialogue attached yet.
    public static GameObject SpawnBody(GameObject prefab, Vector3 pos, string goName)
    {
        if (prefab == null) return null;

        var npc = Object.Instantiate(prefab, pos, Quaternion.identity);
        npc.name = goName;

        // Strip anything that would drive/control it — it just stands and talks.
        var mv = npc.GetComponent<MovementOnFoot>(); if (mv != null) mv.enabled = false;
        var pi = npc.GetComponent<PlayerInput>(); if (pi != null) pi.enabled = false;
        var ofc = npc.GetComponent<OnFootController>(); if (ofc != null) Object.Destroy(ofc);
        var rb = npc.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            // Interpolation drives the Transform from the body's own pose every frame, which silently
            // undoes any code that repositions this NPC by its transform (PlacedNPC.followAnchor did
            // exactly that, and the NPC ended up at the world origin). Nothing here is moving fast enough
            // to need smoothing anyway — it stands still, or walks at 2 m/s in a cutscene.
            rb.interpolation = RigidbodyInterpolation2D.None;
        }

        // Nothing drives this Animator, so freeze it — otherwise the walk cycle plays in place forever.
        var anim = npc.GetComponent<Animator>();
        if (anim != null)
        {
            foreach (var p in anim.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Float &&
                    (p.name == "Horizontal" || p.name == "Vertical" || p.name == "Speed"))
                    anim.SetFloat(p.name, 0f);
            }
            anim.Update(0f);   // sample the idle pose at zeroed params...
            anim.speed = 0f;   // ...then stop so it can't treadmill
        }

        // Same unlit-shader swap the player gets, so it renders under the 3D URP renderer.
        var sr = npc.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            var mat = UnlitSpriteMaterial;
            if (mat != null) sr.sharedMaterial = mat;
        }

        return npc;
    }

    // Dress a cloned body in an authored paper-doll outfit.
    //
    // A body clone comes out of the on-foot prefab looking exactly like the player, which is fine for a
    // face in the crowd and wrong for anyone the player is meant to recognise — the liaison, the crew
    // chief, a promoter. `wardrobe` is an NPCLayeredAppearance authored somewhere else (on the PlacedNPC
    // marker, where the designer can see it in the scene view) and used here as a template: its library,
    // sorting and outfit choices are copied onto the body, which then builds its own layers.
    //
    // The clone's own art goes in the process — its SpriteRenderer and its Animator — because a paper doll
    // draws itself out of child renderers and the prefab's walk sprite would otherwise stand inside it.
    //
    // heightM 0 = the standard on-foot person height, the same figure the crowd, the pit crew and the
    // paddock drivers are all built to.
    public const string LookChild = "Look";   // where a dressed body's paper-doll layers live

    public static bool Dress(GameObject body, NPCLayeredAppearance wardrobe, float heightM = 0f)
    {
        if (body == null || wardrobe == null || wardrobe.library == null) return false;

        // The doll is built on a CHILD, not on the body itself. A paper doll is drawn from an 8px frame and
        // has to be scaled up by about 8x to stand person-high; doing that to the body would take its
        // collider and its speech-bubble canvas up with it — a liaison with a ten-metre collider blocking
        // the paddock and a billboard over her head.
        var look = body.transform.Find(LookChild);
        if (look == null)
        {
            var go = new GameObject(LookChild);
            look = go.transform;
            look.SetParent(body.transform, false);
        }

        var doll = look.GetComponent<NPCLayeredAppearance>();
        if (doll == null) doll = look.gameObject.AddComponent<NPCLayeredAppearance>();

        doll.library = wardrobe.library;
        doll.sortingLayerName = wardrobe.sortingLayerName;
        doll.baseSortingOrder = wardrobe.baseSortingOrder;
        doll.layerMaterial = wardrobe.layerMaterial != null ? wardrobe.layerMaterial : UnlitSpriteMaterial;
        doll.useAuthoredOutfit = wardrobe.useAuthoredOutfit;
        doll.authoredOutfit = CopyOutfit(wardrobe.authoredOutfit);

        if (!doll.Build())
        {
            // No options in the library yet: leave the prefab's own art alone rather than deleting it and
            // standing an invisible person in the pit lane.
            Destroy(doll);
            return false;
        }

        // The prefab's own body, now that the doll is drawing this character.
        var sr = body.GetComponent<SpriteRenderer>();
        if (sr != null) Destroy(sr);
        var anim = body.GetComponent<Animator>();
        if (anim != null) Destroy(anim);

        var lib = doll.library;
        float frameWorldH = Mathf.Max(0.01f, lib.frameHeight / Mathf.Max(1f, lib.pixelsPerUnit));
        float target = heightM > 0f ? heightM : PitCrewSpawner.OnFootPersonHeight;
        look.localScale = Vector3.one * (target / frameWorldH);
        look.localRotation = Quaternion.identity;
        look.localPosition = Vector3.zero;
        return true;
    }

    static NPCLayeredAppearance.LayerChoice[] CopyOutfit(NPCLayeredAppearance.LayerChoice[] src)
    {
        if (src == null) return null;
        var copy = new NPCLayeredAppearance.LayerChoice[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            var c = src[i];
            copy[i] = c == null ? null : new NPCLayeredAppearance.LayerChoice
            {
                category = c.category,
                include = c.include,
                styleIndex = c.styleIndex,
                tint = c.tint,
            };
        }
        return copy;
    }

    // Editor tools build bodies outside play mode, where Destroy() is a no-op that logs.
    static void Destroy(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Object.Destroy(o);
        else Object.DestroyImmediate(o);
    }

    // Body + plain conversation. The common case.
    public static NPCInteractable SpawnTalkable(GameObject prefab, Vector3 pos, string goName,
                                                string speaker, string[] lines)
    {
        var body = SpawnBody(prefab, pos, goName);
        if (body == null) return null;
        return AddTalker<NPCInteractable>(body, speaker, lines);
    }

    // Attach a speaker of the given kind (NPCInteractable, QuestGiverNPC, …) with these lines.
    public static T AddTalker<T>(GameObject body, string speaker, string[] lines) where T : NPCInteractable
    {
        var inter = body.AddComponent<T>();
        inter.speakerName = speaker;
        if (lines != null && lines.Length > 0) inter.lines = lines;
        return inter;
    }
}
