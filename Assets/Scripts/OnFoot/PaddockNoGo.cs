using UnityEngine;

// Ground a walking NPC keeps out of, that physics deliberately leaves open.
//
// Nearly everything in the paddock is no-go because it is solid, and PaddockObstacles can simply ask the
// physics world about it: a parked motorhome is ONE BoxCollider2D over the whole rig, so the player walks
// round it and a paddock walker steers round it.
//
// A popup garage is not built like that. Its shell is a RING of wall colliders with a notch cut out for
// the doorway, because the player has to be able to walk INSIDE one — the meeting room behind the door is
// the point of the thing. The floor it encloses is therefore open ground as far as the physics world is
// concerned, and the crowd treated it as exactly that: a walker recycled on top of a rig was never noticed
// to be standing in one (the escape check asks whether anything solid covers where they stand, and inside
// the ring nothing does), waypoints were generated down the middle of every garage, and a walker that
// found the doorway strolled in. So a walk down the garage row had somebody stood in the middle of half
// the team's garages, on the floor of a room nobody but the player is meant to be in.
//
// Filling the shell in would shut the player out, which is the one thing that must not happen. So the
// keep-out is STATED rather than inferred: a trigger volume marked with this component reads as solid to
// PaddockObstacles and to nothing else in the game. Triggers never stop a dynamic Rigidbody2D, so the
// player walks in through the door exactly as before, and the on-foot collision they do feel is still the
// shell's own walls.
//
// PaddockObstacles is the only thing that reads this. Anything genuinely solid should carry a real
// collider instead — this is for the narrow case of a hole that has to stay open for one body and closed
// for everybody else.
[RequireComponent(typeof(Collider2D))]
public class PaddockNoGo : MonoBehaviour
{
    // Lay a rectangular keep-out in the parent's local frame — the footprint of the thing whose inside
    // must stay empty. Returns the volume so a builder can hold on to it.
    public static PaddockNoGo Box(Transform parent, string name, Vector2 centreLocal, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(centreLocal.x, centreLocal.y, 0f);

        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
        box.isTrigger = true;

        return go.AddComponent<PaddockNoGo>();
    }
}
