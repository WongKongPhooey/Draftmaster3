using System.Collections.Generic;
using UnityEngine;

// The garage lot: one popup garage per car, parked in lines behind the drivers' motorhomes.
//
// DriverMotorhomeLot parks a motorhome for every DRIVER and owns the field roster; this parks a rig for
// every TEAM ENTRY off the same roster, so the two blocks always agree on who is at this race. Each rig
// (PopupGarageRig) is a body with a canopy pitched off its side, the car sat under the canopy, and a
// masked meeting room behind its door (PopupGarageInterior) — walk in and the paddock blacks out exactly
// as it does in the player's own motorhome.
//
// Where it parks: the motorhome lot hands over the line it laid out (anchor, rotation, which way the
// lines run and how deep they got), and the garages continue in the same direction past the last row of
// motorhomes. So the paddock reads as one place walked through in order — tarmac, motorhomes, garages —
// rather than as two unrelated grids, and it inherits the player's RV rotation for free.
//
// Built by DriverMotorhomeLot once its own row exists (same pattern as DriverPresenceDirector): there is
// no scene wiring and no ordering problem.
public class PopupGarageLot : MonoBehaviour
{
    public static PopupGarageLot Instance { get; private set; }

    [Header("Layout")]
    // Everything in this block is IGNORED when the track package holds a Garages PaddockLotArea:
    // the drawn rectangle decides where the block goes, its spacing and how many lines there are.
    [Tooltip("Open ground (m) between the last line of motorhomes and the first line of garages.")]
    public float gapFromMotorhomes = 10f;
    [Tooltip("Gap (m) of open ground between one rig's canopy and the next rig's body, along a line.")]
    public float lineGap = 2.5f;
    [Tooltip("Open ground (m) between one line of garages and the next — the walkway the player comes down.")]
    public float rowGap = 7f;
    [Tooltip("Most garages in one line before another line is stacked behind it. A full entry list in one row would be a quarter-mile street.")]
    public int maxPerRow = 10;
    [Tooltip("Z the rigs sit at. Negative draws in front of the z=0 ground plane, matching the motorhome lot.")]
    public float garageZ = -0.5f;

    [Header("Rig")]
    [Tooltip("Body width (m) across the door/canopy side.")]
    public float bodyWidth = 3.95f;
    [Tooltip("Body length (m), cab toward the walkway.")]
    public float bodyLength = 9.93f;
    [Tooltip("How far the canopy reaches out from the body (m). The line's spacing makes room for it.")]
    public float canopyWidth = 6.5f;
    [Tooltip("How far the canopy runs along the body (m).")]
    public float canopyLength = 7.2f;
    [Tooltip("Width of the doorway in the body's side (m).")]
    public float doorWidth = 1.6f;
    [Tooltip("Where the doorway sits along the body (m from its centre, toward the cab). Kept past the parked car's nose.")]
    public float doorAlong = 3.1f;

    [Header("Cars")]
    [Tooltip("Park each team's car under its canopy. A driver whose real car is already in the world — out on track or sat in its pit box — gets an empty canopy instead.")]
    public bool parkCarsUnderCanopy = true;
    [Tooltip("Carset the parked liveries come from (Resources/<prefix>liveryN). Taken from the scene's GridSpawner when there is one.")]
    public string carsetPrefix = "cup26";

    [Header("Interiors")]
    [Tooltip("Give every garage a masked meeting room behind its door. Off = solid rigs you can only walk around.")]
    public bool buildInteriors = true;
    [Tooltip("How close (m) the player has to get before a garage's room is generated. Rooms nobody walks near are never built.")]
    public float interiorBuildRange = 25f;

    [Header("Look")]
    public string sortingLayerName = "Default";
    [Tooltip("Sorting order the canopies draw at; the rest of each rig stacks just above it. Same band as the motorhomes, so the walking crowd still draws over the lot.")]
    public int sortingOrder = 2;
    [Tooltip("Resources name prefix for the number art painted on each roof.")]
    public string numberSpritePrefix = "cup20num";
    [Tooltip("Height (m) of the painted number.")]
    public float numberSize = 2.5f;
    [Tooltip("Letter each team's name along its canopy edge.")]
    public bool showTeamNames = true;

    readonly List<PopupGarageRig> _rigs = new();
    public IReadOnlyList<PopupGarageRig> Rigs => _rigs;

    // The player's car, when the player is not the one racing.
    //
    // PitLaneStart parks it in its pit box on every scene load, because that is where it has to be for the
    // hour they are in it. The rest of the weekend it should not be there at all: a Cup car sat in a box on
    // pit road through a Truck practice is a car nobody is running, in the way of the people who are, and it
    // is the one thing in the paddock saying the player's session is now when it is not.
    //
    // So it goes home to its own garage — the real car, moved under its own canopy, rather than a second
    // copy parked on top of it. It is already inert (PlayerVehicleController is disabled until somebody
    // climbs in) and the next scene load with a session live puts it back on pit road.
    void PutThePlayersCarAway()
    {
        if (RaceWeekend.SessionLive) return;          // their hour: the car belongs in its box
        if (!TryGetPlayerRig(out var rig) || rig == null) return;

        var car = CarIdentity.FindPlayerCar();
        if (car == null) return;

        Vector3 home = rig.ParkedCarWorldPosition;
        car.transform.SetPositionAndRotation(new Vector3(home.x, home.y, car.transform.position.z),
                                             rig.transform.rotation);

        // Whatever was following the car — the crew chief's anchor, the pit box marker — is reading a
        // transform, so moving it is the whole job. Nothing else in the scene owns its position while it is
        // parked.
        var body = car.GetComponent<Rigidbody2D>();
        if (body != null) body.position = car.transform.position;
    }

    // The player's own garage: the rig carrying the number on the paint they are racing, read the same way
    // the motorhome lot and the timing tower read it. This is where the team's weekend actually happens —
    // the plan meeting is had here, not out on pit road.
    public bool TryGetPlayerRig(out PopupGarageRig rig)
    {
        int number = CarIdentity.NumberOf(CarIdentity.FindPlayerCar());
        if (number > 0 && TryGetRig(number, out rig)) return true;

        rig = null;
        return false;
    }

    public bool TryGetRig(int carNumber, out PopupGarageRig rig)
    {
        for (int i = 0; i < _rigs.Count; i++)
            if (_rigs[i] != null && _rigs[i].carNumber == carNumber) { rig = _rigs[i]; return true; }
        rig = null;
        return false;
    }

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    // Built by DriverMotorhomeLot the moment its own row exists.
    public static PopupGarageLot Create(DriverMotorhomeLot lot)
    {
        if (lot == null) return null;
        var go = new GameObject("PopupGarageLot");
        var garages = go.AddComponent<PopupGarageLot>();
        garages.Build(lot);
        return garages;
    }

    public void Build(DriverMotorhomeLot lot)
    {
        if (lot == null || lot.Slots.Count == 0) return;

        // An authored footprint stands on its own: the block is packed into the rectangle drawn for it in
        // the track package and needs nothing from the motorhomes — not their line, not gapFromMotorhomes.
        var area = PaddockLotArea.Find(PaddockLotKind.Garages);

        if (area == null && !lot.HasLine)
        {
            Debug.LogWarning("PopupGarageLot: the motorhome lot never laid out a line (no player RV and no usable " +
                             "pit lane), so there is nothing to park the garages behind. Draw a Garages " +
                             "PaddockLotArea in the track package to place them outright.", this);
            return;
        }

        // The liveries the field is actually racing, so a parked car matches the one in the pit box.
        var grid = FindObjectOfType<GridSpawner>();
        if (grid != null && !string.IsNullOrEmpty(grid.carsetPrefix)) carsetPrefix = grid.carsetPrefix;

        var slots = lot.Slots;
        int count = slots.Count;

        DriverMotorhomeLot.LineLayout line;
        int rows;

        if (area != null && area.Solve(count, bodyWidth + canopyWidth, bodyLength, garageZ, out line, out rows, out bool tight))
        {
            if (tight)
                Debug.LogWarning($"PopupGarageLot: {count} garages do not fit '{area.name}' at its authored " +
                                 $"spacing — packed to {line.pitch:0.0}m against a {bodyWidth + canopyWidth:0.0}m " +
                                 "rig. Grow the box or cut the field.", area);
        }
        else
        {
            rows = maxPerRow > 0 ? Mathf.Max(1, Mathf.CeilToInt(count / (float)maxPerRow)) : 1;

            var motorhomes = lot.Line;

            // A rig is body-plus-canopy wide, so the line is spaced for both: each canopy fills the gap
            // between its own body and the next one along, and the row's own maths stays in one place.
            line = DriverMotorhomeLot.ComputeLine(motorhomes.origin, motorhomes.rotation, lot.lineDirection,
                                                  bodyWidth + canopyWidth, bodyLength, lineGap, rowGap,
                                                  rows, count, 0, garageZ, lot.stackRowsForward);

            // Slide the whole block past the motorhomes, continuing the way their lines stack.
            float motorhomeReach = motorhomes.rowPitch * Mathf.Max(0, lot.LineRows - 1) + motorhomes.depth * 0.5f;
            line.origin += line.front * (motorhomeReach + Mathf.Max(0f, gapFromMotorhomes) + line.depth * 0.5f);
            line.origin.z = garageZ;
        }

        var root = new GameObject("Garages").transform;
        root.SetParent(transform, false);
        Transform interiors = null;
        if (buildInteriors)
        {
            interiors = new GameObject("GarageInteriors").transform;
            interiors.SetParent(transform, false);
        }

        int parked = 0;
        for (int i = 0; i < count; i++)
        {
            var slot = slots[i];
            var rig = BuildRig(root, line.PlaceAt(i), line.rotation, slot);
            _rigs.Add(rig);
            if (rig.carAtHome) parked++;

            if (interiors != null)
            {
                var room = PopupGarageInterior.Create(interiors, rig);
                if (room != null) room.buildRange = interiorBuildRange;
            }
        }

        PutThePlayersCarAway();

        if (area != null) area.InstallWalkablePocket(transform);
        else ExtendWalkableArea(line.axis, line.front);

        Debug.Log($"PopupGarageLot: {count} team garages in {rows} line(s) of {line.perRow}, {line.pitch:0.0}m apart, " +
                  $"{parked} with the car at home" +
                  (area != null ? $", packed into '{area.name}'" : "") +
                  ". Rooms build as the player walks up to them.", this);
    }

    PopupGarageRig BuildRig(Transform root, Vector3 position, Quaternion rotation, DriverMotorhomeLot.Slot slot)
    {
        string label = string.IsNullOrEmpty(slot.shortName) ? $"#{slot.carNumber}" : slot.shortName;
        var rig = PopupGarageRig.Create(root, $"Garage_{slot.carNumber}_{label}", position, rotation);

        rig.carNumber = slot.carNumber;
        rig.driverName = slot.fullName;
        // A garage is the team's, but an entry with no team on it still has to say whose it is — so it
        // falls back to the driver in it rather than standing there unmarked.
        rig.teamName = !string.IsNullOrEmpty(slot.teamName) ? slot.teamName : label;
        rig.carset = carsetPrefix;

        rig.bodyWidth = bodyWidth;
        rig.bodyLength = bodyLength;
        rig.canopyWidth = canopyWidth;
        rig.canopyLength = canopyLength;
        rig.doorWidth = doorWidth;
        rig.doorAlong = doorAlong;

        // The car is at its garage whenever it is not somewhere else. A live car in the scene means the
        // driver is out on track or sat in their pit box; between sessions there are no cars at all and
        // the whole lot has its bodywork at home.
        rig.carAtHome = parkCarsUnderCanopy && slot.car == null;

        rig.sortingLayerName = sortingLayerName;
        rig.sortingOrder = sortingOrder;
        rig.numberSpritePrefix = numberSpritePrefix;
        rig.numberSize = numberSize;
        rig.showTeamName = showTeamNames;
        CarColours.For(carsetPrefix, slot.carNumber, slot.teamName, out var primary, out var secondary);
        rig.primary = primary;
        rig.secondary = secondary;

        rig.Assemble();
        return rig;
    }

    // The paddock is fenced by PaddockBoundary polygons, and a lot built outside them would be visible and
    // unreachable. So the garages bring their own pocket (boundaries are disjoint — inside ANY counts),
    // deliberately grown back far enough to overlap the motorhome lot's band so the player can walk
    // straight from one into the other.
    //
    // Only when a boundary is already active: adding one to a scene with no boundary at all would newly
    // fence the player in. Same rule the motorhome lot follows.
    void ExtendWalkableArea(Vector3 sideAxis, Vector3 rowAxis)
    {
        if (!PaddockBoundary.AnyActive || _rigs.Count == 0) return;

        float sMin = float.MaxValue, sMax = float.MinValue, rMin = float.MaxValue, rMax = float.MinValue;
        foreach (var rig in _rigs)
        {
            if (rig == null) continue;
            Vector3 p = rig.transform.position;
            float s = Vector3.Dot(p, sideAxis);
            float r = Vector3.Dot(p, rowAxis);
            sMin = Mathf.Min(sMin, s); sMax = Mathf.Max(sMax, s);
            rMin = Mathf.Min(rMin, r); rMax = Mathf.Max(rMax, r);
        }
        if (sMin > sMax) return;

        // Along the lines: past the last body by a whole rig, so the end of a row can be walked around
        // (the canopy alone reaches most of that). Across them: back over the gap to the motorhomes so the
        // two pockets overlap, and forward past the last line's canopies by a walkway.
        float sPad = bodyWidth + canopyWidth + lineGap;
        float rPadBack = bodyLength * 0.5f + Mathf.Max(0f, gapFromMotorhomes) + bodyLength;
        float rPadFront = bodyLength * 0.5f + rowGap;

        var go = new GameObject("PopupGarageLotBoundary");
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);   // local space == world space

        var poly = go.AddComponent<PolygonCollider2D>();
        poly.points = new[]
        {
            (Vector2)(sideAxis * (sMin - sPad) + rowAxis * (rMin - rPadBack)),
            (Vector2)(sideAxis * (sMax + sPad) + rowAxis * (rMin - rPadBack)),
            (Vector2)(sideAxis * (sMax + sPad) + rowAxis * (rMax + rPadFront)),
            (Vector2)(sideAxis * (sMin - sPad) + rowAxis * (rMax + rPadFront)),
        };
        go.AddComponent<PaddockBoundary>();
    }
}
