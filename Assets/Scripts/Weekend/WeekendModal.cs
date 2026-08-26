using UnityEngine;

// One place that owns "the world is frozen because a weekend panel is up".
//
// The weekend stacks panels - an activity finishes, its result card opens, the schedule opens behind that -
// and each of those would otherwise save and restore Time.timeScale for itself. That breaks, because
// Destroy() is deferred to the end of the frame: the next panel's Awake runs BEFORE the previous panel's
// OnDestroy, so the outgoing panel restores a timescale the incoming panel had already zeroed and the game
// starts running underneath an open menu.
//
// A depth counter fixes it. The world is saved once on the way in and restored once on the way out, however
// the panels overlap in between.
public static class WeekendModal
{
    static int _depth;
    static float _saved = 1f;

    public static bool AnyOpen => _depth > 0;

    public static void Push()
    {
        if (_depth++ == 0)
        {
            _saved = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
        }
    }

    public static void Pop()
    {
        if (--_depth <= 0)
        {
            _depth = 0;
            Time.timeScale = _saved;
        }
    }

    // Hard reset for a scene change: nothing that was open is open any more, and the new scene must not
    // inherit a frozen clock.
    public static void Reset()
    {
        _depth = 0;
        Time.timeScale = 1f;
    }
}
