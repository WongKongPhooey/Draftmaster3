using Draftmaster.Crowd;
using UnityEngine;

// One scene-wide ticker for the whole background crowd. Decides what level every CrowdActor should be
// running at and applies it, so a paddock of two hundred people costs one Update instead of four hundred.
//
// Two things make the crowd cheap:
//
//   * The player driving (or in a menu, or anywhere with no on-foot body in the scene) freezes the lot in
//     a single pass. Nobody can walk up to a paddock NPC from inside a race car, so from the green flag
//     onward the crowd is scenery and costs nothing but its draw.
//   * On foot, only NPCs near enough to be seen or heard run. The on-foot camera is a 3.5 orthographic
//     size — about 12m by 7m of world — so most of a big paddock is off screen at any moment.
//
// Installs itself the moment the first CrowdActor enables, so a spawner never has to know it exists.
[AddComponentMenu("")]
public class CrowdDirector : MonoBehaviour
{
    public static CrowdDirector Instance { get; private set; }

    [Tooltip("LOD radii and the per-frame re-evaluation budget.")]
    public CrowdTuning tuning = CrowdTuning.Default;

    [Tooltip("Freeze the entire crowd whenever there is no on-foot player in the scene (i.e. while driving).")]
    public bool freezeWhenNotOnFoot = true;

    [Tooltip("Seconds between checks for the on-foot player. Nothing here needs to be frame-accurate — " +
             "the transition it is watching for is getting in or out of a car.")]
    public float playerPollSeconds = 0.35f;

    OnFootController _player;
    float _pollTimer;
    int _frame;
    bool _allFrozen;

    // Called by CrowdActor.OnEnable. Cheap to call repeatedly.
    public static void EnsureExists()
    {
        if (Instance != null) return;
        var existing = FindObjectOfType<CrowdDirector>();
        Instance = existing != null ? existing : new GameObject("CrowdDirector").AddComponent<CrowdDirector>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        var crowd = CrowdActor.All;
        int n = crowd.Count;
        if (n == 0) return;

        _pollTimer -= Time.deltaTime;
        if (_pollTimer <= 0f)
        {
            _pollTimer = Mathf.Max(0.05f, playerPollSeconds);
            ResolvePlayer();
        }

        bool onFoot = _player != null;

        // Not on foot: one pass over the crowd, then nothing at all until the player is back out of the
        // car. The rota is deliberately skipped here — a hundred NPCs still walking around behind a race
        // is exactly the cost this is meant to remove, so it goes in one go rather than over 25 frames.
        if (!onFoot)
        {
            if (!freezeWhenNotOnFoot) return;   // opted out: leave everyone at whatever level they're on
            if (_allFrozen) return;
            // Unity null, not C# null: an NPC destroyed this frame is still in the list until its
            // OnDisable runs at the end of it.
            for (int i = 0; i < n; i++) { var a = crowd[i]; if (a != null) a.Apply(CrowdLod.Frozen); }
            _allFrozen = true;
            return;
        }
        _allFrozen = false;

        // On foot: re-evaluate a slice of the crowd per frame. Only `evaluationsPerFrame` NPCs are
        // considered on any one frame, so this loop costs the same with 20 in the paddock as with 500 —
        // and CrowdPolicy is what decides, so the behaviour here is the behaviour the tests cover.
        Vector2 p = _player.transform.position;
        int stride = CrowdPolicy.StrideFor(n, tuning.evaluationsPerFrame);

        int start = ((_frame % stride) + stride) % stride;
        for (int i = start; i < n; i += stride)
        {
            var actor = crowd[i];
            if (actor == null) continue;
            actor.Apply(CrowdPolicy.EvaluateWithHysteresis(actor.Lod, true, actor.DistanceTo(p), tuning));
        }
        _frame++;
    }

    // AutographFanSpawner already polls for the on-foot player and publishes it, so use that when it is
    // running. It only installs itself on spline tracks with a pit lane, though, and the crowd shouldn't
    // depend on that — fall back to finding the controller directly. Either way this runs three times a
    // second, not every frame.
    void ResolvePlayer()
    {
        _player = AutographFanSpawner.OnFootPlayer;
        if (_player == null) _player = FindObjectOfType<OnFootController>();
    }
}
