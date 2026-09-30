using System.Collections.Generic;
using UnityEngine;

// The front of the queue at the fan fence: one person per place along the rail on the public side, and the
// spot on the paddock side where the driver stands to sign for them.
//
// The signing session is walked, not stood at. The fence host (WeekendVenueHost) takes the player along this
// line one fan at a time — walk to the next place, turn to the rail, and that fan says their piece out of
// their own bubble. The places are laid down once, by the fence builder, in the order they are worked;
// a session shorter than the line leaves the ones at the far end stood there, which is the point of it.
public class SigningQueue : MonoBehaviour
{
    // The fence in the paddock right now. There is one; a second one registering simply takes over.
    public static SigningQueue Current { get; private set; }

    readonly List<Transform> _fans = new();
    readonly List<Vector3> _stands = new();

    public int Count => _fans.Count;

    void OnEnable() { Current = this; }
    void OnDisable() { if (Current == this) Current = null; }

    // Add the next place in the line: who is stood there, and where the driver stands to face them.
    public void Add(Transform fan, Vector3 stand)
    {
        _fans.Add(fan);
        _stands.Add(stand);
    }

    public Transform Fan(int place) => place >= 0 && place < _fans.Count ? _fans[place] : null;

    public Vector3 Stand(int place) =>
        place >= 0 && place < _stands.Count ? _stands[place] : transform.position;
}
