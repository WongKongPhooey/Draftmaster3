using System.Collections.Generic;
using UnityEngine;

// A pit crew servicing one pit box. Five members wait on the wall side of the box; when the assigned car stops
// in the box the crew walk out to their work stations (four wheel changers at the corners, one fueller at the
// rear), hold their gear for the duration of the stop, then walk back to standby. The car's own pit logic
// (PitStopController for the AI, PlayerPitService for the human) calls BeginService/EndService — the crew are
// purely cosmetic and never gate the stop.
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
    public float moveSpeed = 3.2f;
    public float arriveRadius = 0.1f;
    public float frameRate = 10f;
    [Tooltip("Walk art faces -Y, so +90 lines the drawn facing up with the movement angle (same as PaddockWalker).")]
    public float spriteFacingOffsetDeg = 90f;
    public float turnRate = 720f;
    [Tooltip("Seconds at the car's corner before the held wheel disappears — the moment it 'goes on' the car. Fuellers keep their can for the whole stop.")]
    public float wheelFitSeconds = 1.2f;

    NPCLayeredAppearance _appearance;
    SpriteRenderer _itemRenderer;
    Vector3 _standbyLocal, _workLocal, _targetLocal;
    bool _working;
    bool _isFueller;
    bool _gearSpent;    // the held wheel is on the car; empty-handed until restocked at standby
    float _workTimer;
    float _frameTimer;
    int _frame;

    public void Init(Vector3 standbyLocal, Vector3 workLocal, NPCLayeredAppearance appearance, SpriteRenderer itemRenderer, bool isFueller)
    {
        _appearance = appearance;
        _itemRenderer = itemRenderer;
        _isFueller = isFueller;
        _standbyLocal = standbyLocal;
        _workLocal = workLocal;
        _targetLocal = standbyLocal;
        transform.localPosition = standbyLocal;
        ShowItem(true);   // crew always stand ready with their wheel / fuel can in hand
    }

    // Where (box-local) this member works the current stop. The box calls this on BeginService with the
    // serviced car's ACTUAL corner positions, so the crew run to the car wherever it stopped in the box.
    public void SetWorkTarget(Vector3 workLocal) => _workLocal = workLocal;

    public void SetWorking(bool working)
    {
        _working = working;
        _targetLocal = working ? _workLocal : _standbyLocal;
        if (working) _workTimer = 0f;
    }

    void Update()
    {
        Vector3 cur = transform.localPosition;
        Vector3 to = _targetLocal - cur;
        to.z = 0f;
        float dist = to.magnitude;

        if (dist > arriveRadius)
        {
            Vector3 dir = to / dist;
            transform.localPosition = cur + dir * moveSpeed * Time.deltaTime;
            FaceLocal(dir);
            Animate();
            ShowItem(!_gearSpent);       // carry the wheel to the car; run back empty-handed after fitting it
        }
        else
        {
            transform.localPosition = new Vector3(_targetLocal.x, _targetLocal.y, cur.z);
            _frame = 0;
            _appearance?.SetFrame(0);

            if (_working)
            {
                // At the corner: the wheel goes on after a beat and the held one disappears.
                _workTimer += Time.deltaTime;
                if (!_isFueller && !_gearSpent && _workTimer >= wheelFitSeconds) _gearSpent = true;
            }
            else
            {
                _gearSpent = false;      // back at standby: restocked with a fresh wheel
            }
            ShowItem(!_gearSpent);
        }
    }

    void FaceLocal(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        // Members live under a rotated box; rotate them in local space so facing reads in the box frame.
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + spriteFacingOffsetDeg;
        float z = Mathf.MoveTowardsAngle(transform.localEulerAngles.z, ang, turnRate * Time.deltaTime);
        transform.localRotation = Quaternion.Euler(0f, 0f, z);
    }

    void Animate()
    {
        if (_appearance == null || _appearance.FrameCount == 0) return;
        _frameTimer += Time.deltaTime;
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
// Members are added in station order: 4 wheel changers (front-near, rear-near, front-far, rear-far), then the
// fueller — BeginService relies on that order to send each one to the right corner of the serviced car.
public class PitCrewBox : MonoBehaviour
{
    [Tooltip("Half the car length the wheel stations straddle (m). Set by PitCrewSpawner.")]
    public float wheelLongitudinal = 1.8f;
    [Tooltip("Lateral offset (m) of a wheel station from the car centre. Set by PitCrewSpawner.")]
    public float wheelLateral = 1.2f;
    [Tooltip("How far behind the rear wheel station the fueller stands (m).")]
    public float fuellerBehind = 1.0f;

    int _boxIndex = -1;
    readonly List<PitCrewMember> _members = new();
    Transform _servicingCar;

    public bool IsServicing => _servicingCar != null;
    public int BoxIndex => _boxIndex;

    public void Configure(int boxIndex)
    {
        _boxIndex = boxIndex;
        PitCrewRegistry.Register(boxIndex, this);
    }

    public void AddMember(PitCrewMember m) { if (m != null) _members.Add(m); }

    public void BeginService(Transform car)
    {
        _servicingCar = car;

        // Aim each member at the serviced car's ACTUAL wheel corners (car forward = local +X, so
        // transform.right is its long axis and transform.up its side axis), converted to box-local
        // space — the crew run to the car wherever it stopped in the box, not to a fixed spot.
        if (car != null)
        {
            Vector3 p = car.position;
            Vector3 f = car.right * wheelLongitudinal;
            Vector3 s = car.up * wheelLateral;
            var stations = new[]
            {
                p + f + s,                                   // front wheel, near side
                p - f + s,                                   // rear wheel, near side
                p + f - s,                                   // front wheel, far side
                p - f - s,                                   // rear wheel, far side
                p - f - car.right * fuellerBehind + s,       // fueller, behind the rear
            };
            for (int i = 0; i < _members.Count && i < stations.Length; i++)
            {
                Vector3 local = transform.InverseTransformPoint(stations[i]);
                local.z = 0f;
                _members[i].SetWorkTarget(local);
            }
        }

        for (int i = 0; i < _members.Count; i++) _members[i].SetWorking(true);
    }

    public void EndService()
    {
        _servicingCar = null;
        for (int i = 0; i < _members.Count; i++) _members[i].SetWorking(false);
    }

    void OnDestroy()
    {
        if (_boxIndex >= 0) PitCrewRegistry.Unregister(_boxIndex);
    }
}
