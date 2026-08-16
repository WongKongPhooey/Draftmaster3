using UnityEngine;

// Shared movement/animation plumbing for the paddock fight system. The characters involved come from two
// different rigs — the player and PitLaneStart's NPCs are TaylorEmerson clones driven by an Animator, while
// paddock drivers, fans and crowd walkers are paper-doll NPCLayeredAppearance rigs whose frames are stepped
// by script — so every fight script needs the same "move this body, turn it, step its walk cycle" helpers
// regardless of which rig it got handed.
public static class FightMotion
{
    // The on-foot art is drawn facing -Y, so a world direction becomes a z rotation of atan2 + 90.
    public const float FacingOffsetDeg = 90f;

    // Which way a body is facing, in world space, under that convention (the drawn front is -transform.up).
    public static Vector2 Forward(Transform t) => t == null ? Vector2.down : -(Vector2)t.up;

    // Move a body by a world delta, respecting whatever physics it has. Rigidbody2D positions are written
    // directly rather than through MovePosition: fights nudge bodies in small steps from Update, and
    // MovePosition would queue an interpolated move that the on-foot controller's velocity write overwrites.
    public static void Move(Transform t, Rigidbody2D rb, Vector3 delta)
    {
        if (t == null) return;
        if (rb != null) rb.position += (Vector2)delta;
        else t.position += delta;
    }

    // Snap a body to a world position, keeping its own z (sorting plane).
    public static void PlaceAt(Transform t, Rigidbody2D rb, Vector3 worldPos)
    {
        if (t == null) return;
        worldPos.z = t.position.z;
        if (rb != null) rb.position = worldPos;
        else t.position = worldPos;
    }

    // Turn to look along dir. Reuses the on-foot controller's facing snap so fighters, peacemakers and
    // conversation partners all orient the same way (including directional Animator rigs).
    public static void Face(Transform t, Rigidbody2D rb, Vector2 dir)
    {
        if (t == null || dir.sqrMagnitude < 0.0001f) return;
        OnFootController.ApplyFacing(t, rb, dir, FacingOffsetDeg);
    }

    // Walk toward a world point at speed, turning as it goes. Returns true once within arriveRadius.
    // Paper-doll rigs get their walk cycle stepped; Animator rigs are left alone (their own params drive them).
    public static bool WalkToward(Transform t, Rigidbody2D rb, Vector3 target, float speed, float arriveRadius,
                                  NPCLayeredAppearance appearance, ref float frameTimer, ref int frame, float fps = 8f)
    {
        if (t == null) return true;
        Vector3 to = target - t.position;
        to.z = 0f;
        float dist = to.magnitude;
        if (dist <= arriveRadius)
        {
            IdleFrame(appearance, ref frameTimer, ref frame);
            return true;
        }

        Vector3 dir = to / dist;
        float step = Mathf.Min(speed * Time.deltaTime, dist);
        Move(t, rb, dir * step);
        Face(t, rb, new Vector2(dir.x, dir.y));
        StepFrames(appearance, ref frameTimer, ref frame, fps);
        return false;
    }

    // Advance a paper-doll walk cycle. No-op for Animator rigs (appearance == null).
    public static void StepFrames(NPCLayeredAppearance appearance, ref float frameTimer, ref int frame, float fps)
    {
        if (appearance == null || appearance.FrameCount == 0) return;
        frameTimer += Time.deltaTime;
        float step = 1f / Mathf.Max(0.01f, fps);
        while (frameTimer >= step)
        {
            frameTimer -= step;
            frame++;
            appearance.SetFrame(frame);
        }
    }

    // Park a paper-doll rig on its standing frame.
    public static void IdleFrame(NPCLayeredAppearance appearance, ref float frameTimer, ref int frame)
    {
        frameTimer = 0f;
        if (frame == 0) return;
        frame = 0;
        appearance?.SetFrame(0);
    }

    // Every SpriteRenderer that draws this character (one for an Animator rig, one per paper-doll layer).
    public static SpriteRenderer[] Renderers(GameObject go)
        => go == null ? new SpriteRenderer[0] : go.GetComponentsInChildren<SpriteRenderer>(true);
}
