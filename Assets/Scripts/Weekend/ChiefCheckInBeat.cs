using Draftmaster.Chatter;
using Draftmaster.Weekend;
using UnityEngine;

// The crew chief texting "where are you?" while the driver walks to the Friday strategy briefing — the
// phone's first bleep, and the prompt that teaches its key.
//
// Watches the objective: when the booking is the team briefing and the player comes within
// ChiefCheckIn.TriggerMetres of it on foot, the phone bleeps, the chief's message lands in MESSAGES
// (tile: "1 unread message"), and a control hint says P - Check your phone. The hint stays up until the
// phone is opened or the briefing stops being the booking. Once per save (AppearanceConditions, OnceEver).
//
// Self-installing, like WeekendObjectiveHUD. The rule is ChiefCheckIn (Draftmaster.Weekend, EditMode-tested).
public class ChiefCheckInBeat : MonoBehaviour
{
    public static ChiefCheckInBeat Instance { get; private set; }

    const string HintId = "phone";
    const float PollSeconds = 0.25f;
    [Range(0f, 1f)] public float bleepVolume = 0.6f;

    static readonly AppearanceConditions Memory = new AppearanceConditions
    {
        repeat = AppearanceConditions.Repeat.OnceEver,
        saveKey = ChiefCheckIn.SaveKey,
    };

    public static bool AlreadyFired => Memory.AlreadySeen();

    float _poll;
    bool _hintUp;
    string _hintFor = "";          // the booking the hint was put up for
    AudioSource _audio;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (Instance != null) return;
        var go = new GameObject("ChiefCheckInBeat");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<ChiefCheckInBeat>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        // Opening the phone is the whole point of the prompt; take it down the frame it happens.
        if (_hintUp && PhoneUI.IsOpen) TakeHintDown();

        _poll -= Time.unscaledDeltaTime;
        if (_poll > 0f) return;
        _poll = PollSeconds;

        if (_hintUp && WeekendAppointment.PendingId != _hintFor) TakeHintDown();

        // The phone is the host's career device (PhoneUI stands down for a guest too).
        if (!GameSession.CareerActive || Coop.IsGuest) return;
        if (AlreadyFired) return;

        var booked = WeekendAppointment.Pending;
        if (booked == null || booked.kind != ChiefCheckIn.Booking) return;

        float metres = WeekendAppointment.DistanceRemaining();
        if (metres < 0f || metres > ChiefCheckIn.TriggerMetres) return;   // the cheap early out, before the arrival test

        if (!ChiefCheckIn.ShouldFire(AlreadyFired, booked.kind, metres,
                                     WeekendAppointment.PlayerHasArrived(), PlayerBusy()))
            return;

        Fire(booked);
    }

    // Anything that has the player's attention, or the screen. The text waits for it to clear rather than
    // bleeping under a conversation or a wipe.
    static bool PlayerBusy()
    {
        var player = OnFootController.Current;
        return player == null
            || player.MovementLocked
            || PhoneUI.IsOpen
            || ScreenFade.Busy
            || RacePauseMenu.IsPaused
            || NPCInteractable.AnyConversationActive
            || DialogueChoiceUI.IsOpen
            || WeekendScheduleUI.IsOpen
            || WeekendModal.AnyOpen
            || WeekendTrackChangeover.Staging;
    }

    // Put the beat back: its memory, the prompt's own once-only memory, and the thread it left on the phone.
    // Draftmaster > Demo > Re-arm The Opening calls this.
    public static void Rearm()
    {
        Memory.Forget();
        new AppearanceConditions { repeat = AppearanceConditions.Repeat.OnceEver, saveKey = "hint." + HintId }.Forget();
        PhoneMessages.Clear();
        if (Instance != null && Instance._hintUp) Instance.TakeHintDown();
    }

    // Send it now, wherever the player is — the Demo menu's test button. Still once per save.
    public static bool FireNow()
    {
        if (Instance == null || AlreadyFired) return false;
        var booked = WeekendAppointment.Pending;
        Instance.Fire(booked != null && booked.kind == ChiefCheckIn.Booking ? booked : null);
        return true;
    }

    void Fire(WeekendActivity booking)
    {
        Memory.MarkSeen();

        string chief = DialogueNames.CrewChiefName;
        string startsAt = booking != null ? WeekendSlots.ClockAmPm(booking.startMinute) : "";
        PhoneMessages.Receive(ChiefCheckIn.ThreadId,
                              string.IsNullOrEmpty(chief) ? "Crew Chief" : chief,
                              "Crew chief",
                              ChiefCheckIn.MessageId,
                              SpeakerIdentity.Fill(ChiefCheckIn.Message(startsAt)));

        PhoneUI.SelectOnNextOpen("messages");
        Bleep();

        ControlHints.ShowSticky(HintId, WeekendScripts.PhoneKeyName(), "", "Check your phone", once: true);
        _hintUp = true;
        _hintFor = WeekendAppointment.PendingId;
    }

    void TakeHintDown()
    {
        ControlHints.Hide(HintId);
        _hintUp = false;
    }

    // ------------------------------------------------------------------ the bleep

    void Bleep()
    {
        if (_audio == null)
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;   // in the driver's pocket, not somewhere in the paddock
        }
        _audio.PlayOneShot(TextTone(), bleepVolume);
    }

    static AudioClip _tone;

    // A text alert, generated rather than shipped, like the wake-up alarm: two quick rising pips, twice.
    // Sine rather than the alarm's square — a phone chirps, a bedside clock buzzes.
    internal static AudioClip TextTone()
    {
        if (_tone != null) return _tone;

        const int rate = 44100;
        const float pip = 0.07f;
        const float gap = 0.05f;
        const float pause = 0.22f;          // between the two pairs
        float[] freqs = { 1568f, 2093f };   // G6 then C7

        float pair = 2f * pip + gap;
        float length = 2f * pair + pause;
        int samples = Mathf.CeilToInt(length * rate);
        var data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            float inPair = t;
            if (inPair >= pair + pause) inPair -= pair + pause;
            else if (inPair >= pair) continue;                   // the pause

            int which = inPair < pip ? 0 : (inPair >= pip + gap ? 1 : -1);
            if (which < 0) continue;                             // the gap inside a pair
            float within = which == 0 ? inPair : inPair - pip - gap;
            if (within > pip) continue;

            // Eased in and out over a few milliseconds so the speaker doesn't click.
            float edge = Mathf.Clamp01(Mathf.Min(within, pip - within) / 0.005f);
            data[i] = Mathf.Sin(2f * Mathf.PI * freqs[which] * t) * edge * 0.4f;
        }

        _tone = AudioClip.Create("PhoneTextTone", samples, 1, rate, false);
        _tone.SetData(data, 0);
        return _tone;
    }
}
