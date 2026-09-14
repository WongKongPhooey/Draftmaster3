using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Draftmaster.Weekend;

// Stands the paddock drinks machine (VendingMachine) at the side of the grandstand.
//
// Where exactly: off the END of the stand nearest the player — which is where a real circuit puts one, by
// the steps rather than in front of the seats — and, when that end is the wrong side of the fence, beside
// the grandstand seat WeekendVenueSites anchors instead. Either way the spot is pulled inside the walkable
// paddock (PaddockBoundary), because a machine the player can see and not reach is worse than no machine.
//
// Self-installing: no scene wiring. Same gate as CareerPathNPCSpawner — a career (the drink lasts a race
// weekend, and a single race has no weekend around it), not a co-op guest (content rides the host), and a
// scene with the on-foot paddock flow in it.
public class VendingMachineSpawner : MonoBehaviour
{
    [Header("Where it stands")]
    [Tooltip("Metres clear of the end of the stand. The machine stands beside the steps, not in the seating.")]
    public float endGap = 3f;
    [Tooltip("Metres in front of the stand (toward the track) the machine stands, measured from its front row.")]
    public float frontGap = 2f;
    [Tooltip("Metres to the side of the grandstand seat, for tracks whose stands are outside the paddock and " +
             "the seat is the way out towards them.")]
    public float seatOffset = 4f;
    [Tooltip("How far the walkable-area clamp may drag the chosen spot before it is worth saying so in the " +
             "log — past this the machine is at the paddock edge nearest the stands rather than beside them.")]
    public float strayWarning = 10f;

    [Header("Who it is")]
    [Tooltip("Name over the machine's own speech bubble.")]
    public string speakerName = "DRINKS MACHINE";
    [Tooltip("How close the player has to be to use it, metres.")]
    public float interactRange = 2.2f;

    [Header("Look")]
    [Tooltip("Machine footprint seen from above, metres. A person is about half a metre across at this " +
             "project's scale.")]
    public Vector2 bodySize = new Vector2(1.3f, 0.9f);
    public Color bodyColour = new Color(0.62f, 0.13f, 0.16f);
    public Color glassColour = new Color(0.09f, 0.10f, 0.13f);
    [Tooltip("Cans behind the glass. Colour only — what is actually in the machine changes every weekend.")]
    public Color[] canColours =
    {
        new Color(0.95f, 0.78f, 0.22f),
        new Color(0.30f, 0.75f, 0.95f),
        new Color(0.45f, 0.85f, 0.40f),
    };

    [Header("Timing")]
    [Tooltip("Seconds to wait for the on-foot player to exist (PitLaneStart spawns them in its own Start).")]
    public float playerTimeout = 10f;
    [Tooltip("Seconds to wait for WeekendVenueSites to have anchored a grandstand seat. It waits on the " +
             "motorhome lot first, so this has to outlast that.")]
    public float venueTimeout = 25f;

    public static VendingMachine Instance { get; private set; }

    // ----- self-install -----
    static bool _hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        TryInstall();
        if (_hooked) return;
        SceneManager.sceneLoaded += (_, __) => TryInstall();
        _hooked = true;
    }

    static void TryInstall()
    {
        if (FindFirstObjectByType<VendingMachineSpawner>() != null) return;   // authored or already installed
        if (!GameSession.CareerActive || Coop.IsGuest) return;
        if (FindFirstObjectByType<PitLaneStart>() == null) return;            // no on-foot paddock here
        var go = new GameObject("VendingMachineSpawner");
        go.AddComponent<VendingMachineSpawner>();
    }

    void Start() => StartCoroutine(SpawnWhenReady());

    IEnumerator SpawnWhenReady()
    {
        // The player is the reference for "the nearest stand", and PitLaneStart instantiates them in its
        // own Start — so wait for them rather than measuring off the origin.
        float timeout = playerTimeout;
        while (timeout > 0f && OnFootController.Current == null) { timeout -= Time.deltaTime; yield return null; }

        // And for the grandstand seat, which is the fallback spot and the thing that knows which stand the
        // player is actually meant to be able to reach. Proceed without it if it never turns up — a track
        // with stands but no seat anchor can still have a machine at the end of the nearest one.
        float venueWait = venueTimeout;
        while (venueWait > 0f && GrandstandAnchor() == null) { venueWait -= Time.deltaTime; yield return null; }

        if (!TryFindSpot(out Vector3 at, out Quaternion facing))
        {
            Debug.Log("VendingMachineSpawner: no grandstand at this track — no drinks machine placed.", this);
            yield break;
        }

        Build(at, facing);
    }

    // ---------------------------------------------------------------- placement

    // Four candidate spots — both ends of the nearest stand, then either side of the grandstand seat — and
    // the one the walkable-area clamp moves LEAST wins, ties going to the earlier (more stand-like) one. At
    // a track whose stands are inside the paddock that is the end of the stand; where they are across the
    // track it is beside the seat, which is the way out towards them.
    bool TryFindSpot(out Vector3 at, out Quaternion facing)
    {
        at = Vector3.zero;
        facing = Quaternion.identity;

        Vector3 reference = ReferencePoint();
        var stand = NearestStand(reference);
        var anchor = GrandstandAnchor();
        if (stand == null && anchor == null) return false;

        var candidates = new System.Collections.Generic.List<(Vector3 point, Quaternion rot)>();

        if (stand != null)
        {
            Quaternion rot = stand.transform.rotation;
            // Local +X runs along the stand, local +Y is its depth, and it is turned to face the road — so
            // the front row is half a depth toward the track and the ends are half a length either way.
            float x = stand.length * 0.5f + endGap + bodySize.x * 0.5f;
            float y = -(stand.depth * 0.5f + frontGap);

            Vector3 endA = stand.transform.TransformPoint(new Vector3(x, y, 0f));
            Vector3 endB = stand.transform.TransformPoint(new Vector3(-x, y, 0f));

            // The end nearest whoever is looking for a drink goes first.
            if (Vector2.Distance(endB, reference) < Vector2.Distance(endA, reference))
                (endA, endB) = (endB, endA);

            candidates.Add((endA, rot));
            candidates.Add((endB, rot));
        }

        if (anchor != null)
        {
            Quaternion rot = stand != null ? stand.transform.rotation : Quaternion.identity;
            Vector3 axis = rot * Vector3.right;
            candidates.Add((anchor.StandPosition + axis * seatOffset, rot));
            candidates.Add((anchor.StandPosition - axis * seatOffset, rot));
        }

        float bestStray = float.MaxValue;
        foreach (var (point, rot) in candidates)
        {
            Vector3 walkable = Walkable(point);
            float stray = Vector2.Distance(walkable, point);
            if (stray >= bestStray) continue;

            bestStray = stray;
            at = walkable;
            facing = rot;
        }

        if (bestStray == float.MaxValue) return false;

        if (bestStray > strayWarning)
            Debug.LogWarning($"VendingMachineSpawner: the nearest grandstand is {bestStray:0.#}m outside the " +
                             "walkable paddock, so the drinks machine stands at the edge nearest it rather " +
                             "than beside it.", this);
        return true;
    }

    static Vector3 ReferencePoint()
    {
        var player = OnFootController.Current;
        if (player != null) return new Vector3(player.transform.position.x, player.transform.position.y, 0f);

        var anchor = GrandstandAnchor();
        if (anchor != null) return anchor.StandPosition;

        if (PaddockSpawner.TryGetArea(out var centre, out _, out _, out _, out _)) return centre;
        return Vector3.zero;
    }

    static WeekendVenueAnchor GrandstandAnchor() =>
        WeekendVenueAnchor.Nearest(WeekendVenue.Grandstand, ReferenceForAnchor());

    // Deliberately not ReferencePoint(): that asks the anchors where they are, and this is what the anchor
    // lookup is measured from.
    static Vector3 ReferenceForAnchor()
    {
        var player = OnFootController.Current;
        if (player != null) return player.transform.position;
        if (PaddockSpawner.TryGetArea(out var centre, out _, out _, out _, out _)) return centre;
        return Vector3.zero;
    }

    static Grandstand NearestStand(Vector3 to)
    {
        var stands = FindObjectsByType<Grandstand>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Grandstand best = null;
        float bestSqr = float.MaxValue;
        foreach (var stand in stands)
        {
            if (stand == null) continue;
            float sqr = ((Vector2)(stand.transform.position - to)).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = stand; }
        }
        return best;
    }

    // The nearest spot inside the walkable paddock. Same rule WeekendVenueAnchor applies to itself.
    static Vector3 Walkable(Vector3 point)
    {
        if (!PaddockBoundary.AnyActive) return new Vector3(point.x, point.y, 0f);
        Vector2 inside = PaddockBoundary.Constrain(point);
        return new Vector3(inside.x, inside.y, 0f);
    }

    // ---------------------------------------------------------------- the machine itself

    void Build(Vector3 at, Quaternion facing)
    {
        // Left at the root of the scene and filed by RuntimeHierarchy rather than parented to the spawner:
        // Adopt only moves objects that have no parent, so hanging it off this object would leave it loose
        // in the hierarchy instead of under Environment with the rest of the paddock furniture.
        var root = new GameObject("VendingMachine");
        root.transform.SetPositionAndRotation(at, facing);
        RuntimeHierarchy.Adopt(root, HierarchyGroup.Environment);

        // Placeholder art, same recipe as the rest of the paddock furniture: flat unlit quads, drawn just
        // in front of the tarmac, with the body solid so the player and the crowd walk round it.
        var body = PaddockProps.Unlit(bodyColour);
        var glass = PaddockProps.Unlit(glassColour);

        PaddockProps.Quad(root.transform, "Body", Vector2.zero, bodySize, PaddockProps.PropZ, body, solid: true);

        var windowSize = new Vector2(bodySize.x * 0.72f, bodySize.y * 0.45f);
        PaddockProps.Quad(root.transform, "Window", new Vector2(0f, bodySize.y * 0.12f), windowSize,
                          PaddockProps.PropZ - 0.02f, glass);

        int cans = canColours != null ? canColours.Length : 0;
        for (int i = 0; i < cans; i++)
        {
            float t = cans == 1 ? 0.5f : i / (float)(cans - 1);
            float x = Mathf.Lerp(-windowSize.x * 0.32f, windowSize.x * 0.32f, t);
            PaddockProps.Quad(root.transform, $"Can_{i}", new Vector2(x, bodySize.y * 0.12f),
                              new Vector2(windowSize.x * 0.16f, windowSize.y * 0.55f),
                              PaddockProps.PropZ - 0.04f, PaddockProps.Unlit(canColours[i]));
        }

        PaddockProps.Sign(root.transform, "DRINKS", new Vector2(0f, -bodySize.y * 0.28f), bodySize.x * 0.8f,
                          new Color(0.95f, 0.95f, 0.9f));

        var machine = root.AddComponent<VendingMachine>();
        machine.speakerName = speakerName;
        machine.interactRange = interactRange;
        machine.turnsToFace = false;      // it is bolted to the ground; it cannot turn to face anybody
        machine.bubbleHeadHeight = 1.2f;
        Instance = machine;

        Debug.Log($"VendingMachineSpawner: drinks machine stood at {at} (weekend {RaceWeekend.WeekendId}).", this);
    }
}
