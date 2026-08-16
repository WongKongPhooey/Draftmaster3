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
            Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh != null) sr.sharedMaterial = new Material(sh);
        }

        return npc;
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
