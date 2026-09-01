using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A pit crew servicing one pit box. Five members wait on the wall side of the box; when the assigned car stops
// in the box the crew run out to their work stations, hold their gear for the duration of the stop, then walk
// back to standby. The four wheel men work ONE SIDE AT A TIME, the way a NASCAR stop runs: two to a corner on
// the car's RIGHT-hand side (a changer on the wheel, a carrier a step further out), and once those wheels are
// on they run round the car and do the left-hand pair. The fueller stays at the left rear — that is where the
// filler is — and never changes sides. A sixth man — the sign man (PitCrewSignMan) — goes out earlier, on the
// car's APPROACH, and holds the stop/go board over the nose until the crew are done. The car's own pit logic (PitStopController for the AI, PlayerPitService for the human)
// calls SignalApproach/BeginService/EndService — the crew are purely cosmetic and never gate the stop.
//
// Boxes register themselves by box index (= a car's grid / qualifying position) so a pitting car can find its
// crew with no scene wiring. PitCrewSpawner builds the boxes from the shared PitLane geometry.

// Static lookup from box index -> crew box. Also supports a spatial nearest lookup for the human player, whose
// box index isn't always known to the service code.
public static class PitCrewRegistry
{
    static readonly Dictionary<int, PitCrewBox> _boxes = new();

    public static void Register(int boxIndex, PitCrewBox box) => _boxes[boxIndex] = box;
    public static void Unregister(int boxIndex) { if (_boxes.ContainsKey(boxIndex)) _boxes.Remove(boxIndex); }

    public static PitCrewBox ForBox(int boxIndex) => _boxes.TryGetValue(boxIndex, out var b) && b != null ? b : null;

    public static PitCrewBox Nearest(Vector3 worldPos)
    {
        PitCrewBox best = null;
        float bestSq = float.MaxValue;
        foreach (var b in _boxes.Values)
        {
            if (b == null) continue;
            float sq = (b.transform.position - worldPos).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = b; }
        }
        return best;
    }
}

// One crew member. Walks (in its box's local space) between a standby spot on the pit wall and a work station at
// the car, animating a paper-doll walk cycle and showing its held gear (wheel / fuel can) while on the job.
public class PitCrewMember : MonoBehaviour
{
    [Tooltip("They run rather than stroll — a wheel man has both corners of one side to reach inside a single stop.")]
    public float moveSpeed = 4.2f;
    public float arriveRadius = 0.1f;
    public float frameRate = 10f;
    [Tooltip("Walk art faces -Y, so +90 lines the drawn facing up with the movement angle (same as PaddockWalker).")]
    public float spriteFacingOffsetDeg = 90f;
    public float turnRate = 720f;
    [Tooltip("Seconds at the car's corner before the held wheel disappears — the moment it 'goes on' the car. Half the beat it used to be, because a wheel man now fits two of them in a stop, one per side. A fueller (and the sign man) keep what they are holding for the whole stop.")]
    public float wheelFitSeconds = 0.6f;
    [Tooltip("Turn to face the car on arrival instead of keeping the walk-in heading. The sign man does — he works facing the nose he is holding the board over.")]
    public bool faceCarWhenWorking;

    NPCLayeredAppearance _appearance;
    SpriteRenderer _itemRenderer;
    Vector3 _standbyLocal, _workLocal, _targetLocal;
    readonly List<Vector3> _path = new();   // waypoints to the current target (walks around the car)
    Vector3 _carCenterLocal;                // serviced car's footprint, box-local (axis-aligned approx)
    Vector2 _carHalf;
    bool _hasCar;
    bool _working;
    bool _keepsGear;    // holds the same thing all stop (fuel can, sign) rather than fitting it and letting go
    bool _gearSpent;    // the held wheel is on the car; empty-handed until restocked at standby
    float _workTimer;
    float _frameTimer;
    int _frame;

    public bool IsWorking => _working;
    // The wheel he carried out is on the car. His box waits on all four of these before sending the crew
    // round for the other side.
    public bool WheelFitted => _gearSpent;
    // Where (box-local) he is currently working — which side of the car that is, is the whole point here.
    public Vector3 WorkStation => _workLocal;

    public void Init(Vector3 standbyLocal, Vector3 workLocal, NPCLayeredAppearance appearance, SpriteRenderer itemRenderer, bool keepsGear)
    {
        _appearance = appearance;
        _itemRenderer = itemRenderer;
        _keepsGear = keepsGear;
        _standbyLocal = standbyLocal;
        _workLocal = workLocal;
        _targetLocal = standbyLocal;
        transform.localPosition = standbyLocal;
        ShowItem(true);   // crew always stand ready with their wheel / fuel can in hand
    }

    // Dress this member in the car's colours (CarColours' primary and secondary), as everyone over the wall
    // is on a real pit road. Paper-doll members take the uniform on the layers TeamUniform names; a member
    // still standing in for missing art is a plain blob, so the blob itself takes the primary.
    public void WearTeamColours(Color primary, Color secondary)
    {
        if (_appearance != null && _appearance.WearTeamColours(primary, secondary) > 0) return;

        // Not the gear — that is a wheel or a fuel can, and it lives on a child renderer.
        var body = GetComponent<SpriteRenderer>();
        if (body != null) body.color = primary;
    }

    // Where (box-local) this member works the current stop. The box calls this on BeginService with the
    // serviced car's ACTUAL corner positions, so the crew run to the car wherever it stopped in the box.
    public void SetWorkTarget(Vector3 workLocal) => _workLocal = workLocal;

    // The serviced car's footprint (box-local, axis-aligned — parked cars sit along the lane). Routes to
    // the far side detour around the nearer bumper instead of walking across the top of the car.
    public void SetCarRect(Vector3 centerLocal, Vector2 half)
    {
        _carCenterLocal = centerLocal;
        _carHalf = half;
        _hasCar = true;
    }

    public void SetWorking(bool working)
    {
        _working = working;
        _targetLocal = working ? _workLocal : _standbyLocal;
        BuildPath(transform.localPosition, _targetLocal);
        if (working) _workTimer = 0f;
    }

    // Move a man who is already on the job to a NEW station without sending him back to the wall first: the
    // run round the car to the other side. `withFreshWheel` puts a wheel back in his hands on the way, or he
    // would arrive at the second corner empty-handed, having left the first one on the car.
    public void SendToStation(Vector3 workLocal, bool withFreshWheel)
    {
        _workLocal = workLocal;
        _targetLocal = workLocal;
        _working = true;
        _workTimer = 0f;
        if (withFreshWheel && !_keepsGear) _gearSpent = false;
        BuildPath(transform.localPosition, _targetLocal);
        ShowItem(!_gearSpent);
    }

    // Straight line unless it would cross the car: then walk to the nearer bumper line on THIS side,
    // across past the bumper, and down the far side — an L/U route around the footprint's edge.
    void BuildPath(Vector3 from, Vector3 to)
    {
        _path.Clear();
        if (_hasCar && SegmentCrossesCar(from, to))
        {
            const float endMargin = 0.7f;
            float top = _carCenterLocal.y + _carHalf.y + endMargin;
            float bot = _carCenterLocal.y - _carHalf.y - endMargin;
            // Round whichever bumper makes the shorter total detour.
            float costTop = Mathf.Abs(from.y - top) + Mathf.Abs(to.y - top);
            float costBot = Mathf.Abs(from.y - bot) + Mathf.Abs(to.y - bot);
            float endY = costTop <= costBot ? top : bot;
            _path.Add(new Vector3(from.x, endY, 0f));
            _path.Add(new Vector3(to.x, endY, 0f));
        }
        _path.Add(to);
    }

    // 2D segment vs the car's AABB (slab test). Endpoints sit at the stations beside the car — outside
    // the rect because its half-width is kept under the station lateral.
    bool SegmentCrossesCar(Vector3 a, Vector3 b)
    {
        Vector2 min = new(_carCenterLocal.x - _carHalf.x, _carCenterLocal.y - _carHalf.y);
        Vector2 max = new(_carCenterLocal.x + _carHalf.x, _carCenterLocal.y + _carHalf.y);
        Vector2 d = new(b.x - a.x, b.y - a.y);
        Vector2 p = new(a.x, a.y);
        float t0 = 0f, t1 = 1f;
        for (int axis = 0; axis < 2; axis++)
        {
            float da = axis == 0 ? d.x : d.y;
            float pa = axis == 0 ? p.x : p.y;
            float mn = axis == 0 ? min.x : min.y;
            float mx = axis == 0 ? max.x : max.y;
            if (Mathf.Abs(da) < 1e-6f)
            {
                if (pa < mn || pa > mx) return false;
            }
            else
            {
                float ta = (mn - pa) / da, tb = (mx - pa) / da;
                if (ta > tb) (ta, tb) = (tb, ta);
                t0 = Mathf.Max(t0, ta);
                t1 = Mathf.Min(t1, tb);
                if (t0 > t1) return false;
            }
        }
        return true;
    }

    void Update() => Step(Time.deltaTime);

    // The whole tick, taking its own delta so it can be driven a frame at a time from a test.
    public void Step(float dt)
    {
        Vector3 cur = transform.localPosition;
        Vector3 target = _path.Count > 0 ? _path[0] : _targetLocal;
        Vector3 to = target - cur;
        to.z = 0f;
        float dist = to.magnitude;

        if (dist > arriveRadius)
        {
            Vector3 dir = to / dist;
            // Never step past the station: at a long frame (or a coarse test delta) a crew running at
            // moveSpeed can overshoot by more than arriveRadius and hop back and forth over it forever.
            transform.localPosition = cur + dir * Mathf.Min(moveSpeed * dt, dist);
            FaceLocal(dir, dt);
            Animate(dt);
            ShowItem(!_gearSpent);       // carry the wheel to the car; run back empty-handed after fitting it
        }
        else if (_path.Count > 0)
        {
            _path.RemoveAt(0);           // waypoint reached — head for the next leg
        }
        else
        {
            transform.localPosition = new Vector3(_targetLocal.x, _targetLocal.y, cur.z);
            _frame = 0;
            _appearance?.SetFrame(0);

            if (_working)
            {
                // Standing at the station, the walk-in heading is meaningless — the sign man in particular
                // has to be square to the car, because what he is holding points where he is looking.
                if (faceCarWhenWorking && _hasCar) FaceLocal(_carCenterLocal - transform.localPosition, dt);

                // At the corner: the wheel goes on after a beat and the held one disappears.
                _workTimer += dt;
                if (!_keepsGear && !_gearSpent && _workTimer >= wheelFitSeconds) _gearSpent = true;
            }
            else
            {
                _gearSpent = false;      // back at standby: restocked with a fresh wheel
            }
            ShowItem(!_gearSpent);
        }
    }

    void FaceLocal(Vector3 dir, float dt)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        // Members live under a rotated box; rotate them in local space so facing reads in the box frame.
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + spriteFacingOffsetDeg;
        float z = Mathf.MoveTowardsAngle(transform.localEulerAngles.z, ang, turnRate * dt);
        transform.localRotation = Quaternion.Euler(0f, 0f, z);
    }

    void Animate(float dt)
    {
        if (_appearance == null || _appearance.FrameCount == 0) return;
        _frameTimer += dt;
        float step = 1f / Mathf.Max(0.01f, frameRate);
        while (_frameTimer >= step)
        {
            _frameTimer -= step;
            _frame++;
            _appearance.SetFrame(_frame);
        }
    }

    void ShowItem(bool show)
    {
        if (_itemRenderer != null) _itemRenderer.enabled = show;
    }
}

// A pit box's crew. Owns its members and toggles them between standby and work on BeginService/EndService.
// Members are added in station order: 4 wheel men (front changer, rear changer, front carrier, rear carrier),
// then the fueller — BeginService relies on that order to put a pair on each corner of whichever side is being
// worked. A stop runs the NASCAR way rather than four corners at once: the car's right-hand wheels first, and
// the moment those are on, the four wheel men run round the car for the left-hand pair. The sign man is held
// separately because he does not keep their timing: he leaves the wall on SignalApproach, before the car is
// even stopped, and stays out a beat past EndService with the board up.
public class PitCrewBox : MonoBehaviour
{
    [Tooltip("Half the car length the wheel stations straddle (m). Set by PitCrewSpawner.")]
    public float wheelLongitudinal = 1.8f;
    [Tooltip("Lateral offset (m) of a wheel station from the car centre. Set by PitCrewSpawner.")]
    public float wheelLateral = 1.2f;
    [Tooltip("How far behind the rear wheel station the fueller stands (m).")]
    public float fuellerBehind = 1.0f;
    [Tooltip("How far ahead of the front wheel station the sign man stands (m) — clear of the nose, holding the board back over it.")]
    public float signStandoff = 1.9f;
    [Tooltip("How far off the car's centreline the sign man stands, as a signed fraction of the wheel station lateral (the spawner signs it with the crew's wall side). He is in front of the car, not in front of the driver.")]
    public float signLateralFrac = 0.6f;

    [Tooltip("Work the car's RIGHT-hand wheels first and then send the crew round for the left-hand pair, the way a NASCAR stop runs. Off = one man on each of the four corners, all at once.")]
    public bool rightSideFirst = true;
    [Tooltip("Gap (m) along the car between the two men on one corner: the changer on the wheel and the carrier a step further out towards the bumper.")]
    public float carrierOffset = 0.7f;
    [Tooltip("Send the crew round for the left side after this long (s) even if a wheel man never got his wheel on, so one member who cannot reach his corner can't strand the stop on one side.")]
    public float sideChangeTimeout = 4f;

    [Tooltip("How long (s) to keep looking for the car assigned to this box before leaving the crew in their own clothes. The grid spawns over several frames and is re-parked afterwards, so a box is usually built before its car exists.")]
    public float resolveWindow = 8f;
    [Tooltip("Give up on an announced arrival after this long (s) and put the board back up. A car that called the box and then wrecked, pitted through, or ran out of race must not leave a man standing in the lane holding a sign forever.")]
    public float approachTimeout = 30f;

    // Members 0..3 are the wheel men (two to a corner); member 4 is the fueller, who works one place all stop.
    const int WheelMen = 4;

    int _boxIndex = -1;
    readonly List<PitCrewMember> _members = new();
    PitCrewSignMan _signMan;
    Transform _servicingCar;
    bool _approaching;
    float _approachExpires;
    bool _dressed;
    Color _primary = Color.white, _secondary = Color.white;
    bool _servicing;         // a stop is running (true even for a stop with no car handed over)
    bool _onLeftSide;        // the right-hand wheels are on and the crew have gone round
    float _sideTimer;
    readonly Vector3[] _rightStations = new Vector3[WheelMen];
    readonly Vector3[] _leftStations = new Vector3[WheelMen];

    public bool IsServicing => _servicingCar != null;
    public bool IsSignDown => _signMan != null && _signMan.IsDown;
    public int BoxIndex => _boxIndex;
    // The crew have finished the right-hand side and are working the left.
    public bool WorkingLeftSide => _onLeftSide;

    public void Configure(int boxIndex)
    {
        _boxIndex = boxIndex;
        PitCrewRegistry.Register(boxIndex, this);
    }

    public void AddMember(PitCrewMember m)
    {
        if (m == null) return;
        _members.Add(m);
        if (_dressed) m.WearTeamColours(_primary, _secondary);   // a late arrival still wears the kit
    }

    public void SetSignMan(PitCrewSignMan man)
    {
        _signMan = man;
        if (_dressed && _signMan != null) _signMan.WearTeamColours(_primary, _secondary);
    }

    void Start() => StartCoroutine(DressCrew());

    // The crew wear their car's colours, like the pit box stand behind them: five people in the same kit is
    // what says whose stop this is when the field is all in the lane at once. The car is not there yet when
    // the box is built, so keep asking until it is (PitBoxCars answers everyone from one shared scan).
    //
    // If nothing ever claims the box, the crew keep the outfit they rolled — a paddock face in their own
    // clothes reads better than five people washed the fallback grey.
    IEnumerator DressCrew()
    {
        float giveUpAt = Time.time + Mathf.Max(0f, resolveWindow);
        while (Time.time <= giveUpAt)
        {
            var label = PitBoxCars.Label(_boxIndex);
            if (label != null)
            {
                CarColours.For(label, out _primary, out _secondary);
                _dressed = true;
                for (int i = 0; i < _members.Count; i++)
                    if (_members[i] != null) _members[i].WearTeamColours(_primary, _secondary);
                if (_signMan != null) _signMan.WearTeamColours(_primary, _secondary);
                yield break;
            }
            yield return null;
        }
    }

    // The car has committed to this box and is coming down the lane. Only the sign man moves: he walks out
    // ahead of the box and puts the board down over the spot the nose is going to stop on, which is the whole
    // point of him. The rest of the crew stay on the wall until there is a car to work on.
    //
    // Idempotent — the pit logic shouts this every frame of the run-in, and re-sending the man would restart
    // his walk each time.
    public void SignalApproach(Transform car)
    {
        if (_approaching || _servicingCar != null || _signMan == null) return;
        _approaching = true;
        _approachExpires = Time.time + Mathf.Max(0f, approachTimeout);

        // No car to measure yet, so he sets up on the box's own nominal car: centred on the box, facing back
        // down it. BeginService moves him the last half-metre onto wherever the car actually stopped.
        _signMan.SetCarRect(Vector3.zero, NominalCarHalf);
        _signMan.SetWorkTarget(NominalSignStation);
        _signMan.Lower();
    }

    // Half-extents (box-local) of a car parked square in the box: the footprint the crew route around.
    // Half-width stays just inside the wheel stations so those remain reachable endpoints; half-length runs
    // a touch past them, roughly the bodywork.
    Vector2 NominalCarHalf => new(Mathf.Max(0.4f, wheelLateral - 0.15f), wheelLongitudinal + 0.4f);

    // Where the sign man stands for a car parked square in the box (box-local: +Y is up the lane = car
    // forward, +X the wall side the crew work from).
    Vector3 NominalSignStation => new(wheelLateral * signLateralFrac, wheelLongitudinal + signStandoff, 0f);

    public void BeginService(Transform car)
    {
        _servicingCar = car;
        _approaching = false;
        _servicing = true;
        _onLeftSide = false;
        _sideTimer = 0f;

        // Measure both sides off the serviced car's ACTUAL pose, so the crew run to the car wherever it
        // stopped in the box rather than to a fixed spot. Car forward is its local +X, so transform.right is
        // the long axis and transform.up the side axis — and because up is a quarter turn anticlockwise of
        // forward, +up is the car's LEFT-hand side and -up its RIGHT. With no car handed over, fall back to
        // one parked square in the box: nose up the lane, right-hand side towards the box's own +X.
        bool haveCar = car != null;
        Vector3 origin = haveCar ? car.position : transform.position;
        Vector3 fwd = haveCar ? car.right : transform.up;
        Vector3 side = haveCar ? -car.up : transform.right;   // out to the car's right-hand side

        Vector3 f = fwd * wheelLongitudinal;
        Vector3 r = side * wheelLateral;
        Vector3 step = fwd * carrierOffset;
        FillStations(_rightStations, origin, f, r, step);
        FillStations(_leftStations, origin, f, -r, step);

        // Publish the car's footprint so members route around the bumpers instead of over the roof — which
        // is what makes the change of sides a run around the nose or the tail. Half-width sits just inside
        // the wheel stations (they must stay reachable endpoints); half-length a touch beyond them, roughly
        // the bodywork.
        Vector3 carLocal = ToBoxLocal(origin);
        var carHalf = NominalCarHalf;

        // The filler is on the left rear, so that is where the fueller works for the whole stop.
        Vector3 fuellerStation = ToBoxLocal(origin - f - fwd * fuellerBehind - r);

        for (int i = 0; i < _members.Count; i++)
        {
            if (_members[i] == null) continue;
            _members[i].SetCarRect(carLocal, carHalf);
            if (i < WheelMen)
                // Right side first, two men to a corner. With the sequence turned off, the old layout:
                // one man on each of the four corners, all of them at once.
                _members[i].SetWorkTarget(rightSideFirst ? _rightStations[i]
                                                         : i < 2 ? _rightStations[i] : _leftStations[i - 2]);
            else if (i == WheelMen)
                _members[i].SetWorkTarget(fuellerStation);
        }

        // The sign man was already out on the nominal box; nudge him onto the real nose so the board
        // sits over the car that actually turned up rather than the one the box was drawn for.
        if (haveCar && _signMan != null)
        {
            Vector3 signLocal = ToBoxLocal(
                car.position + car.right * (wheelLongitudinal + signStandoff) + car.up * (wheelLateral * signLateralFrac));
            _signMan.SetCarRect(carLocal, carHalf);
            _signMan.SetWorkTarget(signLocal);
        }

        for (int i = 0; i < _members.Count; i++) _members[i]?.SetWorking(true);
        // A stop nobody announced (a car that crawled in without calling ahead) still gets its board down.
        _signMan?.Lower();
    }

    // The four wheel men on ONE side of the car, in member order: the front and rear changers on the wheels
    // themselves, then their two carriers a step further out towards the bumpers, so a pair sharing a corner
    // don't stand in the same place. `side` is the lateral out to the side being worked.
    void FillStations(Vector3[] into, Vector3 origin, Vector3 halfLength, Vector3 side, Vector3 step)
    {
        into[0] = ToBoxLocal(origin + halfLength + side);
        into[1] = ToBoxLocal(origin - halfLength + side);
        into[2] = ToBoxLocal(origin + halfLength + side + step);
        into[3] = ToBoxLocal(origin - halfLength + side - step);
    }

    // Members walk in the box's local frame, and they are flat, so drop the depth on the way in.
    Vector3 ToBoxLocal(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        local.z = 0f;
        return local;
    }

    public void EndService()
    {
        _servicingCar = null;
        _approaching = false;
        _servicing = false;
        _onLeftSide = false;
        _sideTimer = 0f;
        for (int i = 0; i < _members.Count; i++) _members[i]?.SetWorking(false);
        _signMan?.Raise();   // board up = GO; he holds it there a beat before following them back
    }

    void Update()
    {
        if (_servicing) StepService(Time.deltaTime);

        // The car that announced itself never arrived. Put the board up rather than leave a man in the lane.
        if (!_approaching || _servicingCar != null || Time.time < _approachExpires) return;
        _approaching = false;
        _signMan?.Raise();
    }

    // Half way through a stop the right-hand wheels are on, and the four wheel men run round the car for the
    // left-hand pair, each picking a fresh wheel out of the pit box on the way. The hand-off is driven by the
    // men themselves rather than a clock, so it happens when the first side is genuinely finished at whatever
    // pace they are moving; sideChangeTimeout is only the backstop for one who never reaches his corner.
    // Takes its own delta so a test can drive it a frame at a time.
    public void StepService(float dt)
    {
        if (!rightSideFirst || _onLeftSide) return;
        _sideTimer += dt;
        if (!RightSideDone && _sideTimer < sideChangeTimeout) return;

        _onLeftSide = true;
        for (int i = 0; i < _members.Count && i < WheelMen; i++)
            _members[i]?.SendToStation(_leftStations[i], withFreshWheel: true);
    }

    // Every wheel man has left the wheel he carried out on the car.
    bool RightSideDone
    {
        get
        {
            for (int i = 0; i < _members.Count && i < WheelMen; i++)
                if (_members[i] != null && !_members[i].WheelFitted) return false;
            return true;
        }
    }

    void OnDestroy()
    {
        if (_boxIndex >= 0) PitCrewRegistry.Unregister(_boxIndex);
    }
}
