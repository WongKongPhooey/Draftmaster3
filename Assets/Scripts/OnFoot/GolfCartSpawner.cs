using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Draftmaster.Weekend;

// Parks the team's golf cart at the mouth of the player's own garage.
//
// Where exactly: off the walkway end of the rig, straight out in front of the canopy — the spot a real
// team leaves theirs, where it is in nobody's way and is the first thing you see walking up to your own
// garage. That is also the point of it being there at all: the cart is not on any menu and nothing tells
// the player it exists, so it has to be parked somewhere they walk past on their way to the one place the
// weekend keeps sending them.
//
// Falls back to the pit box venue anchor (WeekendVenue.PitBox is the team's garage in the paddock) at a
// track or a session where the garage row was never built, and gives up quietly if there is no paddock at
// all — a cart parked in the middle of nowhere is worse than no cart.
//
// Self-installing, with the same gate as VendingMachineSpawner: a career (a single race has no team
// garage to park outside of), not a co-op guest (career content rides the host), and a scene with the
// on-foot paddock flow in it.
public class GolfCartSpawner : MonoBehaviour
{
    [Header("Where it parks")]
    [Tooltip("Metres past the walkway end of the garage rig. Small: the cart is parked at the mouth of " +
             "the garage, not out in the middle of the road between the rows.")]
    public float noseGap = 1.6f;
    [Tooltip("Metres to the side of the pit box anchor, for a paddock with no garage row in it.")]
    public float anchorOffset = 3f;
    [Tooltip("How far the walkable-area clamp may drag the chosen spot before it is worth saying so in " +
             "the log — past this the cart is at the paddock edge rather than at the garage.")]
    public float strayWarning = 8f;

    [Header("The cart")]
    [Tooltip("Name over the cart's title card.")]
    public string cartName = "TEAM GOLF CART";
    [Tooltip("Second line of the title card — the nudge that says it can be used.")]
    public string cartSubtitle = "Keys are in it";
    [Tooltip("How close the player has to be to climb in, metres.")]
    public float interactRange = 2.4f;
    [Tooltip("Metres from the cart the title card introduces it. Generously wide: this is the thing " +
             "meant to catch the eye on the walk up to the garage.")]
    public float titleRadius = 9f;
    [Tooltip("Riding speed, units/sec. Walking is 3.5 and running 7, so this is a shade past a run — a " +
             "cart that is worth finding without being a car.")]
    public float rideSpeed = 8f;

    [Header("Timing")]
    [Tooltip("Seconds to wait for the on-foot player to exist (PitLaneStart spawns them in its own Start).")]
    public float playerTimeout = 10f;
    [Tooltip("Seconds to wait for the garage row to have parked the player's own rig. The garages are " +
             "built by the motorhome lot, so this has to outlast that.")]
    public float garageTimeout = 25f;

    public static GolfCart Instance { get; private set; }

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
        if (FindFirstObjectByType<GolfCartSpawner>() != null) return;     // authored or already installed
        if (!GameSession.CareerActive || Coop.IsGuest) return;
        if (FindFirstObjectByType<PitLaneStart>() == null) return;        // no on-foot paddock here
        var go = new GameObject("GolfCartSpawner");
        go.AddComponent<GolfCartSpawner>();
    }

    void Start() => StartCoroutine(SpawnWhenReady());

    IEnumerator SpawnWhenReady()
    {
        // Somebody may have parked one by hand in the track package; that one is the cart.
        if (FindFirstObjectByType<GolfCart>() != null) yield break;

        float timeout = playerTimeout;
        while (timeout > 0f && OnFootController.Current == null) { timeout -= Time.deltaTime; yield return null; }

        // The garage row is what this is parked against, and it is built a second or two into the scene
        // (DriverMotorhomeLot stands its own line up first and then hands it over).
        float garageWait = garageTimeout;
        while (garageWait > 0f && !PlayerRig(out _)) { garageWait -= Time.deltaTime; yield return null; }

        if (!TryFindSpot(out Vector3 at, out Quaternion facing, out Color primary, out Color secondary))
        {
            Debug.Log("GolfCartSpawner: no team garage in this paddock — no cart parked.", this);
            yield break;
        }

        Build(at, facing, primary, secondary);
    }

    // ---------------------------------------------------------------- placement

    // The player's own garage, when the row has been built and their number is in it.
    static bool PlayerRig(out PopupGarageRig rig)
    {
        rig = null;
        var lot = PopupGarageLot.Instance;
        return lot != null && lot.TryGetPlayerRig(out rig) && rig != null;
    }

    // Straight out in front of the canopy, past the walkway end of the rig, pointing out of the row. The
    // rig's frame runs its length along +Y with the cab toward the walkway, and the canopy hangs off
    // whichever side `canopySide` says — so this is the open corner of the team's own garage.
    bool TryFindSpot(out Vector3 at, out Quaternion facing, out Color primary, out Color secondary)
    {
        at = Vector3.zero;
        facing = Quaternion.identity;
        primary = new Color(0.85f, 0.85f, 0.88f);
        secondary = new Color(0.20f, 0.22f, 0.26f);

        if (PlayerRig(out var rig))
        {
            Vector3 spot = ParkingSpot(rig, noseGap);

            at = Walkable(spot);
            facing = rig.transform.rotation;               // nose out of the row, the way the rig faces
            primary = rig.primary;
            secondary = rig.secondary;

            float stray = Vector2.Distance(at, spot);
            if (stray > strayWarning)
                Debug.LogWarning($"GolfCartSpawner: the team garage is {stray:0.#}m outside the walkable " +
                                 "paddock, so the cart is parked at the edge nearest it rather than at " +
                                 "the garage mouth.", this);
            return true;
        }

        // No garage row: the pit box anchor is the team's garage as far as the weekend is concerned.
        var anchor = WeekendVenueAnchor.Find(WeekendVenue.PitBox);
        if (anchor == null) return false;

        at = Walkable(anchor.StandPosition + Vector3.right * anchorOffset);
        facing = Quaternion.identity;
        return true;
    }

    // Where a cart parks against a given garage, in world space. Pulled out as a pure function of the rig
    // so it can be measured without a paddock around it: the thing that matters is that the spot is clear
    // of the shed and of the canopy (a cart drawn over either reads as parked inside the garage) and that
    // a full cart length fits between the rig's end and the row behind it.
    public static Vector3 ParkingSpot(PopupGarageRig rig, float noseGap)
    {
        if (rig == null) return Vector3.zero;
        float x = rig.CanopyLocalCentre.x;                 // out in front of the open side
        float y = rig.bodyLength * 0.5f + noseGap;         // past the cab end, which faces the walkway
        return rig.transform.TransformPoint(new Vector3(x, y, 0f));
    }

    // The nearest spot inside the walkable paddock. Same rule every other venue follows.
    static Vector3 Walkable(Vector3 point)
    {
        if (!PaddockBoundary.AnyActive) return new Vector3(point.x, point.y, 0f);
        Vector2 inside = PaddockBoundary.Constrain(point);
        return new Vector3(inside.x, inside.y, 0f);
    }

    // ---------------------------------------------------------------- the cart itself

    void Build(Vector3 at, Quaternion facing, Color primary, Color secondary)
    {
        // Left at the root and filed by RuntimeHierarchy rather than parented to the spawner, the same
        // way the drinks machine is: Adopt only moves objects that have no parent.
        var cart = GolfCart.Create(null, "GolfCart", at, facing);
        cart.transform.position = new Vector3(at.x, at.y, cart.parkZ);
        cart.primary = primary;
        cart.secondary = secondary;
        cart.rideSpeed = rideSpeed;
        cart.speakerName = cartName;
        cart.interactRange = interactRange;
        cart.turnsToFace = false;                 // it is parked; it does not swivel to greet anybody
        cart.Assemble();
        RuntimeHierarchy.Adopt(cart.gameObject, HierarchyGroup.Vehicles);

        // Nothing in the game mentions the cart, so the paddock introduces it the way it introduces any
        // other place worth walking to — a title card the first time the player comes near it.
        LocationTitle.Attach(cart.gameObject, cartName, titleRadius, cartSubtitle);

        Instance = cart;
        Debug.Log($"GolfCartSpawner: golf cart parked at {at} by the team garage.", this);
    }
}
