using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Draftmaster.Weekend;

// Parks the team's golf cart at the mouth of the player's own garage, and a second, unmarked paddock cart
// somewhere random in the walkable paddock.
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
// The paddock cart is the one somebody left lying about: a different spot every time the scene loads, picked
// from inside the walkable paddock (never a grandstand viewing pocket), on clear ground no motorhome, garage
// or keep-out floor covers, and far enough from the team cart that the two read as separate finds.
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

    [Header("The paddock cart")]
    [Tooltip("Also park a second cart at a random spot in the paddock, different every load.")]
    public bool parkPaddockCart = true;
    [Tooltip("Name over the paddock cart's title card.")]
    public string paddockCartName = "PADDOCK GOLF CART";
    [Tooltip("Metres of clear ground the paddock cart needs around its centre — about half a cart length, " +
             "so it is never drawn through the side of a motorhome or a garage.")]
    public float paddockClearance = 1.6f;
    [Tooltip("Least distance, metres, between the paddock cart and the team cart.")]
    public float paddockSeparation = 15f;
    [Tooltip("Random spots tried before giving up on the paddock cart.")]
    public int paddockAttempts = 200;

    [Header("Timing")]
    [Tooltip("Seconds to wait for the on-foot player to exist (PitLaneStart spawns them in its own Start).")]
    public float playerTimeout = 10f;
    [Tooltip("Seconds to wait for the garage row to have parked the player's own rig. The garages are " +
             "built by the motorhome lot, so this has to outlast that.")]
    public float garageTimeout = 25f;

    public static GolfCart Instance { get; private set; }

    // The randomly parked paddock cart, or null when there was nowhere clear to put it.
    public static GolfCart PaddockCart { get; private set; }

    // Name prefix WeekendVenueSites gives the walkable pocket round a grandstand seat. Those are boundaries
    // too, but across the circuit from the paddock — nowhere to leave a cart.
    const string ViewingPocketPrefix = "ViewingPocket_";

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

        if (TryFindSpot(out Vector3 at, out Quaternion facing, out Color primary, out Color secondary))
        {
            Instance = Build(at, facing, primary, secondary, cartName);
        }
        else
        {
            Debug.Log("GolfCartSpawner: no team garage in this paddock — no team cart parked.", this);
        }

        if (parkPaddockCart) ParkPaddockCart();
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

    // ---------------------------------------------------------------- the paddock cart

    // Somewhere random inside the walkable paddock, on clear ground, well away from the team cart.
    void ParkPaddockCart()
    {
        var areas = new List<Rect>();
        var owners = new List<PaddockBoundary>();
        foreach (var b in PaddockBoundary.Active)
        {
            if (b == null || !b.isActiveAndEnabled) continue;
            if (b.name.StartsWith(ViewingPocketPrefix, StringComparison.Ordinal)) continue;
            var poly = b.GetComponent<PolygonCollider2D>();
            if (poly == null) continue;
            Bounds bb = poly.bounds;
            areas.Add(new Rect(bb.min.x, bb.min.y, bb.size.x, bb.size.y));
            owners.Add(b);
        }

        // A paddock with no drawn boundary: scatter it round the team's garage instead, the one place such
        // a paddock is known to have.
        if (areas.Count == 0)
        {
            Vector3 centre;
            if (Instance != null) centre = Instance.transform.position;
            else
            {
                var anchor = WeekendVenueAnchor.Find(WeekendVenue.PitBox);
                if (anchor == null)
                {
                    Debug.Log("GolfCartSpawner: no paddock to leave a cart in — no paddock cart parked.", this);
                    return;
                }
                centre = anchor.StandPosition;
            }
            float r = paddockSeparation * 2f;
            areas.Add(new Rect(centre.x - r, centre.y - r, r * 2f, r * 2f));
        }

        Vector2? teamCart = Instance != null ? (Vector2?)Instance.transform.position : null;
        var rng = new System.Random();

        bool found = PickRandomSpot(rng, areas, p =>
        {
            if (owners.Count > 0)
            {
                bool inside = false;
                for (int i = 0; i < owners.Count && !inside; i++) inside = owners[i].Contains(p);
                if (!inside) return false;
            }
            if (teamCart.HasValue && Vector2.Distance(p, teamCart.Value) < paddockSeparation) return false;
            return !PaddockObstacles.IsBlocked(p, paddockClearance);
        }, paddockAttempts, out Vector2 spot);

        if (!found)
        {
            Debug.Log("GolfCartSpawner: no clear ground found in the paddock — no paddock cart parked.", this);
            return;
        }

        // Stock paint (the cart's own defaults): this one belongs to nobody in particular.
        var facing = Quaternion.Euler(0f, 0f, (float)(rng.NextDouble() * 360.0));
        PaddockCart = Build(new Vector3(spot.x, spot.y, 0f), facing,
                            new Color(0.85f, 0.85f, 0.88f), new Color(0.20f, 0.22f, 0.26f), paddockCartName);
    }

    // A random point, spread over the union of `areas` by area (a big paddock polygon gets proportionally
    // more tries than a sliver of a lot pocket), that `accept` passes. False after `attempts` misses. Pure
    // and seeded, so it can be measured without a paddock round it.
    public static bool PickRandomSpot(System.Random rng, IList<Rect> areas, Func<Vector2, bool> accept,
                                      int attempts, out Vector2 spot)
    {
        spot = Vector2.zero;
        if (rng == null || areas == null || areas.Count == 0) return false;

        double total = 0;
        for (int i = 0; i < areas.Count; i++) total += Mathf.Max(0f, areas[i].width * areas[i].height);
        if (total <= 0) return false;

        for (int n = 0; n < attempts; n++)
        {
            double pick = rng.NextDouble() * total;
            Rect r = areas[areas.Count - 1];
            for (int i = 0; i < areas.Count; i++)
            {
                double a = Mathf.Max(0f, areas[i].width * areas[i].height);
                if (pick < a) { r = areas[i]; break; }
                pick -= a;
            }

            var p = new Vector2(r.xMin + (float)rng.NextDouble() * r.width,
                                r.yMin + (float)rng.NextDouble() * r.height);
            if (accept == null || accept(p)) { spot = p; return true; }
        }
        return false;
    }

    // ---------------------------------------------------------------- the cart itself

    GolfCart Build(Vector3 at, Quaternion facing, Color primary, Color secondary, string title)
    {
        // Left at the root and filed by RuntimeHierarchy rather than parented to the spawner, the same
        // way the drinks machine is: Adopt only moves objects that have no parent.
        var cart = GolfCart.Create(null, "GolfCart", at, facing);
        cart.transform.position = new Vector3(at.x, at.y, cart.parkZ);
        cart.primary = primary;
        cart.secondary = secondary;
        cart.rideSpeed = rideSpeed;
        cart.speakerName = title;
        cart.interactRange = interactRange;
        cart.turnsToFace = false;                 // it is parked; it does not swivel to greet anybody
        cart.Assemble();
        RuntimeHierarchy.Adopt(cart.gameObject, HierarchyGroup.Vehicles);

        // Nothing in the game mentions the cart, so the paddock introduces it the way it introduces any
        // other place worth walking to — a title card the first time the player comes near it.
        LocationTitle.Attach(cart.gameObject, title, titleRadius, cartSubtitle);
        return cart;
    }
}
