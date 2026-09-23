using Draftmaster.Chatter;
using Draftmaster.Weekend;
using UnityEngine;

// The crew chief texting "where are you?" while the driver walks to the Friday strategy briefing — the
// phone's first bleep, and the lesson that teaches its key.
//
// Watches the objective: when the booking is the team briefing and the player comes within
// ChiefCheckIn.TriggerMetres of it on foot, the phone bleeps, the chief's message lands in MESSAGES
// (tile: "1 unread message"), the player is stopped where they stand (PhoneUI.Summon) and a control hint
// says P - Check your phone. Taking the phone out lifts the prompt; putting it away again is when the run
// hint gets its turn — PitLaneStart holds "hold to run" while HoldsRunHint says so. Once per save
// (AppearanceConditions, OnceEver).
//
// Self-installing, like WeekendObjectiveHUD. The rules are ChiefCheckIn (Draftmaster.Weekend, EditMode-tested).
public class ChiefCheckInBeat : MonoBehaviour
{
    public static ChiefCheckInBeat Instance { get; private set; }

    // Stop the player until they take the phone out, so the one prompt that teaches it cannot be walked past.
    // Every build, not only the demo: the beat is a once-per-save tutorial on a career's first walk anyway.
    // A pad player is not stranded — the phone opens on View / Create as well as P.
    public static bool HoldPlayerForPhone = true;

    const string HintId = "phone";
    const float PollSeconds = 0.25f;
    [Range(0f, 1f)] public float bleepVolume = 0.6f;

    static readonly AppearanceConditions Memory = new AppearanceConditions
    {
        repeat = AppearanceConditions.Repeat.OnceEver,
        saveKey = ChiefCheckIn.SaveKey,
    };

    public static bool AlreadyFired => Memory.AlreadySeen();

    // The lesson: the phone has gone off (Ringing), the player has it out (Reading), and it is over once they
    // put it away again.
    enum Stage { Idle, Ringing, Reading }
    Stage _stage;
    bool _held;                    // PhoneUI took the player for this lesson
    string _ringingFor = "";       // the booking the phone went off for
    bool _fired;                   // AlreadyFired, re-read on the poll
    float _poll;
    AudioSource _audio;

    // PitLaneStart asks this before teaching the run control: running is taught after the phone, not before.
    // Live rather than polled — the liaison books the briefing the same frame she hands movement back, and a
    // quarter-second-old answer would let the run hint in ahead of the phone.
    public static bool HoldsRunHint
    {
        get
        {
            var beat = Instance;
            if (beat == null) return false;
            bool lessonLive = beat._stage != Stage.Idle;

            // Only a lesson still to come needs the booking looked up; this runs every frame of the walk.
            if (lessonLive || beat._fired || !GameSession.CareerActive || Coop.IsGuest)
                return ChiefCheckIn.HoldsRunHint(beat._fired, false, lessonLive);

            var booked = WeekendAppointment.Pending;
            return ChiefCheckIn.HoldsRunHint(beat._fired, booked != null && booked.kind == ChiefCheckIn.Booking,
                                             lessonLive);
        }
    }

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
        _fired = AlreadyFired;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        StepLesson();

        _poll -= Time.unscaledDeltaTime;
        if (_poll > 0f) return;
        _poll = PollSeconds;

        // The briefing stopped being the booking with the phone still in their pocket: let them go.
        if (_stage == Stage.Ringing && WeekendAppointment.PendingId != _ringingFor) EndLesson();

        _fired = AlreadyFired;

        // The phone is the host's career device (PhoneUI stands down for a guest too).
        if (_fired || !GameSession.CareerActive || Coop.IsGuest) return;

        var booked = WeekendAppointment.Pending;
        if (booked == null || booked.kind != ChiefCheckIn.Booking) return;

        float metres = WeekendAppointment.DistanceRemaining();
        if (metres < 0f || metres > ChiefCheckIn.TriggerMetres) return;   // the cheap early out, before the arrival test

        if (!ChiefCheckIn.ShouldFire(_fired, booked.kind, metres,
                                     WeekendAppointment.PlayerHasArrived(), PlayerBusy()))
            return;

        Fire(booked);
    }

    void StepLesson()
    {
        switch (_stage)
        {
            case Stage.Ringing:
                // Taking it out is the whole point of the prompt; it comes down the frame it happens.
                if (PhoneUI.IsOpen) { _stage = Stage.Reading; ControlHints.Hide(HintId); }
                // The hold went with the body (a scene change, a swap into the car): nothing left to wait for.
                else if (_held && !PhoneUI.Summoned) EndLesson();
                break;

            case Stage.Reading:
                // Put away. PitLaneStart sees HoldsRunHint drop and teaches running next.
                if (!PhoneUI.IsOpen) { _stage = Stage.Idle; _held = false; }
                break;
        }
    }

    void EndLesson()
    {
        ControlHints.Hide(HintId);
        if (_held) PhoneUI.CancelSummon();
        _held = false;
        _stage = Stage.Idle;
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

    // Put the beat back: its memory, the two prompts it orders (the phone's and the run hint it holds back),
    // and the thread it left on the phone. Draftmaster > Demo > Re-arm The Opening calls this.
    public static void Rearm()
    {
        Memory.Forget();
        ControlHints.Forget(HintId);
        ControlHints.Forget("run");
        PhoneMessages.Clear();
        if (Instance != null)
        {
            Instance.EndLesson();
            Instance._fired = false;
        }
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
        _fired = true;

        // Putting the phone away is when running is taught, and the lesson promises that order. The run
        // hint is once per save, though, and any earlier walk in the same save spends it — a quick test
        // walk, a teleport out of the RV — so the hand-off pointed at a prompt that would never show. The
        // phone lesson is itself once per save, so re-arming the run hint here teaches it once more at most.
        ControlHints.Forget("run");

        string chief = DialogueNames.CrewChiefName;
        string startsAt = booking != null ? WeekendSlots.ClockAmPm(booking.startMinute) : "";
        PhoneMessages.Receive(ChiefCheckIn.ThreadId,
                              string.IsNullOrEmpty(chief) ? "Crew Chief" : chief,
                              "Crew chief",
                              ChiefCheckIn.MessageId,
                              SpeakerIdentity.Fill(ChiefCheckIn.Message(startsAt)));

        PhoneUI.SelectOnNextOpen("messages");
        Bleep();

        _held = HoldPlayerForPhone && PhoneUI.Summon();

        // Not once-only: the beat itself is, and a player held still must always be told why. Urgent, so a
        // hint already on screen cannot keep it waiting while they stand there.
        ControlHints.ShowSticky(HintId, WeekendScripts.PhoneKeyName(), InputGlyphs.PhonePad, "Check your phone",
                                once: false, urgent: true);
        _stage = Stage.Ringing;
        _ringingFor = WeekendAppointment.PendingId;
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
