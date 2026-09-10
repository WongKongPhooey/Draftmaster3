using UnityEngine;
using UnityEngine.InputSystem;

// The way out of a car that is no longer going anywhere.
//
// Put it in the wall hard enough and the session used to be over in the only sense that mattered: the car
// sat across the kerbs with a folded nose and nothing to do about it. This watches for that — stopped, on
// the circuit, not in the pit lane — and offers the tow. Press the key and the screen goes dark, the truck
// does the rest, and the driver is stood in their own box with the car in front of them, still bent. The
// crew start on it there (PitCrewRepair), which is the part that makes the crash cost something.
//
// The key is P, which is also the phone's. They never overlap: the phone is an on-foot thing and this is
// only ever offered from the driving seat, so at any moment exactly one of them is listening.
//
// Self-installing like the rest of the in-race furniture: it finds the PitLaneStart that owns the
// on-foot/car handover and needs no wiring.
public class StrandedTow : MonoBehaviour
{
    public static StrandedTow Instance { get; private set; }

    [Header("Key")]
    public Key towKey = Key.P;

    [Header("What counts as stranded")]
    [Tooltip("At or below this speed (mph) the car is not going anywhere under its own power.")]
    public float stoppedBelowMph = 4f;
    [Tooltip("Seconds the car has to have been stopped before the offer appears. Long enough that a spin " +
             "the driver can drive out of does not get one.")]
    public float stoppedForSeconds = 3f;
    [Tooltip("Only offer the tow to a car that is actually damaged. Off = any stopped car can call one.")]
    public bool requireDamage = true;
    [Tooltip("Damage level at or above which a stopped car counts as wrecked rather than parked.")]
    public float damageAtLeast = 0.12f;

    PitLaneStart _pits;
    PlayerVehicleController _car;
    VehicleDamage _bodywork;
    float _stoppedSince = -1f;
    bool _offering;
    bool _towing;
    float _searchTimer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("StrandedTow");
        DontDestroyOnLoad(go);
        go.AddComponent<StrandedTow>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (_towing) return;

        if (!Rebind()) { Offer(false); return; }

        if (!Stranded()) { Offer(false); return; }

        Offer(true);

        var kb = Keyboard.current;
        if (kb != null && towKey != Key.None && kb[towKey].wasPressedThisFrame) Tow();
    }

    // The scene's handover owner and the car it owns. Both die on a scene load, so this re-looks now and
    // then rather than caching once — the same shape TrackMiniMap uses for its TrackBuilder.
    bool Rebind()
    {
        if (_pits == null || _car == null)
        {
            _searchTimer -= Time.unscaledDeltaTime;
            if (_searchTimer > 0f) return false;
            _searchTimer = 1f;

            _pits = FindAnyObjectByType<PitLaneStart>();
            _car = _pits != null ? _pits.car : null;
            _bodywork = _car != null ? _car.GetComponentInChildren<VehicleDamage>() : null;
        }
        return _pits != null && _car != null;
    }

    // Stopped, on the circuit, with the player actually driving it. The pit lane is excluded outright: a
    // car sat in its own box is not stranded, it is parked, and offering a tow there would be nonsense.
    bool Stranded()
    {
        if (!_pits.IsDriving || RacePauseMenu.IsPaused) return false;

        if (_pits.track != null && _pits.track.IsOnPitSurface(_car.transform.position)) { _stoppedSince = -1f; return false; }

        if (requireDamage && (_bodywork == null || _bodywork.DamageLevel < damageAtLeast)) { _stoppedSince = -1f; return false; }

        if (_car.SpeedMph > stoppedBelowMph) { _stoppedSince = -1f; return false; }

        if (_stoppedSince < 0f) _stoppedSince = Time.time;
        return Time.time - _stoppedSince >= stoppedForSeconds;
    }

    void Offer(bool show)
    {
        if (show == _offering) return;
        _offering = show;

        if (show)
            ControlHints.ShowSticky("tow", towKey.ToString().ToUpperInvariant(), "Y",
                                    "Call a tow back to the pits");
        else
            ControlHints.Hide("tow");
    }

    // Dark, move, light. The cut hides the teleport, which is the only reason it is here — a car and a
    // driver both jumping half a lap in one frame reads as a bug however well it is explained.
    void Tow()
    {
        _towing = true;
        Offer(false);

        ScreenFade.Cut(() =>
        {
            if (_pits != null) _pits.TowToPits();
            _stoppedSince = -1f;
            _towing = false;
        }, outSeconds: 0.5f, holdSeconds: 0.7f);
    }
}
