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

    [Tooltip("Recycle radii and budgets: how far a filler NPC may drift before it is picked up and put " +
             "back just out of shot, and how many are allowed around the player at once.")]
    public CrowdRecycleTuning recycling = CrowdRecycleTuning.Default;

    [Tooltip("Freeze the entire crowd whenever there is no on-foot player in the scene (i.e. while driving).")]
    public bool freezeWhenNotOnFoot = true;

    [Tooltip("Seconds between checks for the on-foot player. Nothing here needs to be frame-accurate — " +
             "the transition it is watching for is getting in or out of a car.")]
    public float playerPollSeconds = 0.35f;

    [Tooltip("Seconds between full recounts of how many recyclable NPCs are inside the recycle radius. " +
             "One pass over the crowd doing a squared-distance compare — cheap enough at 400 that it is " +
             "not worth maintaining the tally incrementally, and it can never drift out of step.")]
    public float clusterRecountSeconds = 0.35f;

    OnFootController _player;
    Camera _camera;
    float _pollTimer;
    float _recountTimer;
    int _frame;
    bool _allFrozen;
    int _nearCount;

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
        // Back on foot, possibly a long way from where the player got in. The cluster tally is about a
        // position that no longer means anything, so force a recount before anything acts on it.
        if (_allFrozen) _recountTimer = 0f;
        _allFrozen = false;

        // On foot: re-evaluate a slice of the crowd per frame. Only `evaluationsPerFrame` NPCs are
        // considered on any one frame, so this loop costs the same with 20 in the paddock as with 500 —
        // and CrowdPolicy is what decides, so the behaviour here is the behaviour the tests cover.
        Vector2 p = _player.transform.position;
        int stride = CrowdPolicy.StrideFor(n, tuning.evaluationsPerFrame);

        // Radii widened so nothing can be put back inside the camera frame, whatever the inspector says.
        var recycle = CameraClampedRecycling();
        int recycleBudget = recycle.enabled ? Mathf.Max(0, recycle.recyclesPerFrame) : 0;

        _recountTimer -= Time.deltaTime;
        if (recycle.enabled && _recountTimer <= 0f)
        {
            _recountTimer = Mathf.Max(0.05f, clusterRecountSeconds);
            RecountCluster(crowd, p, recycle.despawnRadius);
        }

        int start = ((_frame % stride) + stride) % stride;
        for (int i = start; i < n; i += stride)
        {
            var actor = crowd[i];
            if (actor == null) continue;

            float d = actor.DistanceTo(p);

            // Filler that has drifted out of the player's part of the paddock goes back in the pool and
            // comes down again just out of shot, so the crowd stays clustered where the player is. Then
            // fall through and LOD it from where it now stands, not where it was.
            if (recycleBudget > 0 && actor.CanRecycle &&
                CrowdRecyclePolicy.ShouldRecycle(true, d, _nearCount, recycle))
            {
                // The budget counts attempts, not successes. A player stood somewhere with nowhere legal
                // to put anybody — off the end of the paddock, out on the racetrack — fails every sample,
                // and it is that case, not the successful one, that would otherwise run the sampler for
                // every eligible NPC in the slice, every frame.
                recycleBudget--;
                if (TryRecycle(actor, p, recycle))
                {
                    _nearCount++;
                    d = actor.DistanceTo(p);
                }
            }

            actor.Apply(CrowdPolicy.EvaluateWithHysteresis(actor.Lod, true, d, tuning));
        }
        _frame++;
    }

    // Roll respawn points until one lands inside both the NPC's own paddock rectangle and any authored
    // PaddockBoundary. Giving up is the right answer, not a fallback: a player stood out on the racetrack
    // or off the end of the paddock has nowhere legal nearby, and leaving the NPC where it is costs
    // nothing — it is frozen out there anyway.
    bool TryRecycle(CrowdActor actor, Vector2 player, in CrowdRecycleTuning recycle)
    {
        var area = actor.RecycleArea;
        int samples = Mathf.Max(1, recycle.samplesPerRecycle);
        for (int s = 0; s < samples; s++)
        {
            if (!CrowdRecyclePolicy.TryCandidate(player, area, recycle,
                                                 Random.value, Random.value, out Vector2 point)) continue;
            if (!PaddockBoundary.IsInside(point)) continue;
            actor.RecycleTo(point);
            return true;
        }
        return false;
    }

    // How many recyclable NPCs are currently inside the recycle radius — the number the cap is applied
    // to. Squared compare, so it is one multiply-add per crowd member and no square roots.
    void RecountCluster(System.Collections.Generic.List<CrowdActor> crowd, Vector2 player, float radius)
    {
        float r2 = Mathf.Max(0f, radius); r2 *= r2;
        int count = 0;
        for (int i = 0; i < crowd.Count; i++)
        {
            var a = crowd[i];
            if (a == null || !a.recyclable) continue;
            Vector3 t = a.transform.position;
            float dx = t.x - player.x, dy = t.y - player.y;
            if (dx * dx + dy * dy <= r2) count++;
        }
        _nearCount = count;
    }

    // "Just out of shot" is a property of the camera, not a number somebody typed in, so measure it.
    // A perspective or missing camera leaves the authored radii alone — they are already clear of the
    // on-foot frame — and the sanitiser still orders them against the despawn radius.
    CrowdRecycleTuning CameraClampedRecycling()
    {
        if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
        if (_camera == null || !_camera.orthographic)
            return CrowdRecyclePolicy.Sanitised(recycling);
        return CrowdRecyclePolicy.ClampedToCamera(recycling, _camera.orthographicSize, _camera.aspect);
    }

    // AutographFanSpawner already polls for the on-foot player and publishes it, so use that when it is
    // running. It only installs itself on spline tracks with a pit lane, though, and the crowd shouldn't
    // depend on that — fall back to finding the controller directly. Either way this runs three times a
    // second, not every frame.
    void ResolvePlayer()
    {
        _player = AutographFanSpawner.OnFootPlayer;
        if (_player == null) _player = OnFootController.Current;
    }
}
