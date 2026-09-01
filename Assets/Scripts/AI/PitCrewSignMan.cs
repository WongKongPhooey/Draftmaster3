using UnityEngine;

// The sign man — the "lollipop" — is the sixth person over the wall: the one holding the stop/go board on a
// pole. He waits on the wall with the rest of the crew, walks out to the front of the box while his car is
// still coming down pit road, and drops the board over the nose. That board IS the stop line, which is why he
// goes out on the APPROACH rather than when the car arrives: by the time it has stopped, the sign has already
// done its job. He lifts it back to GO the moment the crew are off the car, and only then walks back.
//
// Seen from directly above, a raised sign is the board foreshortened onto his hands and a lowered one is the
// pole swung out flat across the car — so the swing is animated as the sign's REACH: scaled down against him
// when up, full length over the nose when down. The board itself swaps sprite the instant he commits to a
// direction, so it reads STOP all the way down and GO from the first frame of the lift.
//
// The board renderer is the member's held-gear child, so it is the crew walk code that keeps it visible and
// this component only ever animates its length. PitCrewBox drives it: SignalApproach -> Lower, EndService ->
// Raise.
[RequireComponent(typeof(PitCrewMember))]
public class PitCrewSignMan : MonoBehaviour
{
    [Tooltip("Seconds for the board to swing between up and down.")]
    public float swingSeconds = 0.35f;
    [Tooltip("Fraction of the sign's full reach still drawn when it is raised — from above, a vertical pole is a stub, not nothing.")]
    [Range(0.05f, 0.6f)] public float raisedReach = 0.22f;
    [Tooltip("Seconds to hold the board up at the car before walking back to the wall. The raise is the driver's cue to go, so it wants a beat of its own before the man moves.")]
    public float holdAfterRaiseSeconds = 0.6f;

    PitCrewMember _member;
    SpriteRenderer _sign;
    Sprite _stopSprite, _goSprite;
    Vector3 _signBaseScale = Vector3.one;
    bool _down;              // which way the board is being held (the signal itself)
    float _down01;           // 0 = fully raised, 1 = fully lowered (the swing)
    bool _returnPending;
    float _returnTimer;

    public bool IsDown => _down;
    public float Down01 => _down01;
    public PitCrewMember Member => _member != null ? _member : _member = GetComponent<PitCrewMember>();

    // `sign` is the member's held-gear renderer: the board and pole, pivoted at the hand end so scaling its
    // length swings the board out from him rather than stretching it in place.
    public void Init(SpriteRenderer sign, Sprite stopSprite, Sprite goSprite)
    {
        _member = GetComponent<PitCrewMember>();
        _sign = sign;
        _stopSprite = stopSprite;
        _goSprite = goSprite;
        if (_sign != null) _signBaseScale = _sign.transform.localScale;
        _down = false;
        _down01 = 0f;
        _returnPending = false;
        Apply();
    }

    // Car on its way in: out to the front of the box, board down over where the nose will stop.
    public void Lower()
    {
        _down = true;
        _returnPending = false;
        Member?.SetWorking(true);
        Apply();
    }

    // Crew off the car: board up (GO) at once, then back to the wall after a beat.
    public void Raise()
    {
        if (!_down && !_returnPending && Member != null && !Member.IsWorking) return;
        _down = false;
        _returnPending = true;
        _returnTimer = Mathf.Max(0f, holdAfterRaiseSeconds);
        Apply();
    }

    // Where (box-local) he stands to hold the sign, and the footprint to route around getting there.
    public void SetWorkTarget(Vector3 workLocal) => Member?.SetWorkTarget(workLocal);
    public void SetCarRect(Vector3 centerLocal, Vector2 half) => Member?.SetCarRect(centerLocal, half);
    public void WearTeamColours(Color primary, Color secondary) => Member?.WearTeamColours(primary, secondary);

    void Update() => Step(Time.deltaTime);

    // The whole tick, taking its own delta so it can be driven a frame at a time from a test.
    public void Step(float dt)
    {
        float target = _down ? 1f : 0f;
        if (!Mathf.Approximately(_down01, target))
        {
            _down01 = Mathf.MoveTowards(_down01, target, dt / Mathf.Max(0.01f, swingSeconds));
            Apply();
        }

        if (!_returnPending) return;
        _returnTimer -= dt;
        if (_returnTimer > 0f) return;
        _returnPending = false;
        Member?.SetWorking(false);   // board is up and seen: back to the wall
    }

    void Apply()
    {
        if (_sign == null) return;
        var wanted = _down ? _stopSprite : _goSprite;
        if (wanted != null) _sign.sprite = wanted;
        float reach = Mathf.Lerp(Mathf.Clamp01(raisedReach), 1f, _down01);
        _sign.transform.localScale = new Vector3(_signBaseScale.x, _signBaseScale.y * reach, _signBaseScale.z);
    }
}
