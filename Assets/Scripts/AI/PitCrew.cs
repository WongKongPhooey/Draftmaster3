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

    NPCLayeredAppearance _appearance;
    SpriteRenderer _itemRenderer;
    Vector3 _standbyLocal, _workLocal, _targetLocal;
    bool _working;
    float _frameTimer;
    int _frame;

    public void Init(Vector3 standbyLocal, Vector3 workLocal, NPCLayeredAppearance appearance, SpriteRenderer itemRenderer)
    {
        _appearance = appearance;
        _itemRenderer = itemRenderer;
        _standbyLocal = standbyLocal;
        _workLocal = workLocal;
        _targetLocal = standbyLocal;
        transform.localPosition = standbyLocal;
        ShowItem(false);
    }

    public void SetWorking(bool working)
    {
        _working = working;
        _targetLocal = working ? _workLocal : _standbyLocal;
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
            ShowItem(false);            // gear is stowed while walking
        }
        else
        {
            transform.localPosition = new Vector3(_targetLocal.x, _targetLocal.y, cur.z);
            _frame = 0;
            _appearance?.SetFrame(0);
            ShowItem(_working);          // arrived at the car: present the wheel / fuel can
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
public class PitCrewBox : MonoBehaviour
{
    int _boxIndex = -1;
    readonly List<PitCrewMember> _members = new();
    Transform _servicingCar;

    public bool IsServicing => _servicingCar != null;

    public void Configure(int boxIndex)
    {
        _boxIndex = boxIndex;
        PitCrewRegistry.Register(boxIndex, this);
    }

    public void AddMember(PitCrewMember m) { if (m != null) _members.Add(m); }

    public void BeginService(Transform car)
    {
        _servicingCar = car;
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
