using UnityEngine;

// A place that introduces itself when you walk up to it.
//
// The paddock used to letter its venues in world space — "FAN ZONE" floating over the fence, "HOSPITALITY"
// over the awning — which is a label stuck to the scene rather than something the game says. A place now
// announces itself the way the track does when a scene opens and the way an objective does when one is
// set: the title card in the upper middle of the screen, held for a few seconds and faded out
// (`SpawnIntroUI`).
//
// Self-contained — drop one on whatever the place is built under, give it a name and a radius. It waits
// for whatever is already on the card (the arrival card, an objective banner) to finish before it speaks,
// because where you are standing is the least urgent thing that card carries.
public class LocationTitle : MonoBehaviour
{
    [Tooltip("The name of the place, as the card will print it.")]
    public string title = "";
    [Tooltip("The line under the name — usually what the place is for. Optional.")]
    public string subtitle = "";
    [Tooltip("How close the player has to get before the place names itself, in metres.")]
    public float radius = 12f;
    [Tooltip("How far back out they must walk before it will name itself again. 0 = radius x 1.5.")]
    public float forgetRadius = 0f;
    [Tooltip("Never name the same place twice inside this many seconds.")]
    public float minRepeatSeconds = 25f;
    [Tooltip("Name it the first time and never again this scene.")]
    public bool onceOnly = false;

    const float PollSeconds = 0.2f;

    float _nextPoll;
    bool _armed = true;
    bool _pending;
    float _lastShown = -999f;

    // One line at a build site: LocationTitle.Attach(fence, "FAN ZONE", 14f, "Where signing is done").
    public static LocationTitle Attach(GameObject place, string title, float radius, string subtitle = "")
    {
        if (place == null || string.IsNullOrEmpty(title)) return null;
        var t = place.AddComponent<LocationTitle>();
        t.title = title;
        t.radius = radius;
        t.subtitle = subtitle;
        return t;
    }

    void Update()
    {
        if (string.IsNullOrEmpty(title)) return;
        if (Time.unscaledTime < _nextPoll) return;
        _nextPoll = Time.unscaledTime + PollSeconds;

        var player = OnFootController.Current;
        if (player == null) return;                        // in the car, or a scene with no on-foot body

        float d = Vector2.Distance(player.transform.position, transform.position);
        float forget = forgetRadius > 0f ? forgetRadius : radius * 1.5f;

        if (_armed && d <= radius)
        {
            _pending = true;
            _armed = false;
        }
        else if (!_armed && !onceOnly && d > forget && Time.unscaledTime - _lastShown >= minRepeatSeconds)
        {
            _armed = true;
        }

        if (!_pending) return;

        var intro = SpawnIntroUI.Instance;
        if (intro != null && intro.TitleBusy) return;      // let the card in front of it finish
        if (d > forget) { _pending = false; return; }      // walked off while it was waiting its turn

        SpawnIntroUI.Banner(title, subtitle);
        _pending = false;
        _lastShown = Time.unscaledTime;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.95f, 0.86f, 0.55f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.color = new Color(0.95f, 0.86f, 0.55f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, forgetRadius > 0f ? forgetRadius : radius * 1.5f);
    }
}
