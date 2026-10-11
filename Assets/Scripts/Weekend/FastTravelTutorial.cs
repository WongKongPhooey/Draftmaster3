using Draftmaster.Controls;
using Draftmaster.Weekend;
using UnityEngine;

// Fast travel stays locked until the career's first team meeting is over, and the prompt that teaches it
// goes up the first time there is somewhere to travel to after that: "T - Fast travel to your objective",
// the pad's button while a pad is in use, and on a phone a line pointing at the FAST TRAVEL button on the
// objective strip (which is slid down with it, so the button is on screen when the line names it).
//
// WeekendObjectiveHUD and the phone's TASKS app ask Locked before offering the shortcut or drawing the
// button. Self-installing, like ChiefCheckInBeat. The rules are FastTravelUnlock (Draftmaster.Weekend,
// EditMode-tested).
public class FastTravelTutorial : MonoBehaviour
{
    public static FastTravelTutorial Instance { get; private set; }

    const float PollSeconds = 0.25f;
    const float PromptSeconds = 10f;

    static readonly AppearanceConditions Memory = new AppearanceConditions
    {
        repeat = AppearanceConditions.Repeat.OnceEver,
        saveKey = FastTravelUnlock.SaveKey,
    };

    float _poll;

    // Worked out once a frame: the objective strip asks in Update and in every OnGUI pass.
    static bool _locked;
    static int _lockedFrame = -1;

    public static bool Locked
    {
        get
        {
            if (_lockedFrame == Time.frameCount) return _locked;
            _lockedFrame = Time.frameCount;
            _locked = ComputeLocked();
            return _locked;
        }
    }

    static bool ComputeLocked()
    {
        if (!GameSession.CareerActive) return false;   // a single race has no team meetings and no career to earn it in
        if (!FastTravelUnlock.Locked(Memory.AlreadySeen(), CareerModes.Current, Coop.IsGuest)) return false;

        // Settled on this weekend's sheet but never recorded — a meeting gone by unattended, or a save from
        // before the lock — counts the same as the director's own Unlock.
        var sheet = WeekendLedger.Timetable;
        if (sheet != null && FastTravelUnlock.MeetingSettled(sheet.Activities, WeekendLedger.IsDone, WeekendLedger.IsMissed))
        {
            Memory.MarkSeen();
            return false;
        }
        return true;
    }

    // A team meeting has just been finished (WeekendDirector.Finish). The prompt follows on the next poll
    // that finds the player free with somewhere to go.
    public static void Unlock()
    {
        if (!Memory.AlreadySeen()) Memory.MarkSeen();
        _lockedFrame = -1;
    }

    // Lock it again and put the prompt back. Draftmaster > Demo > Re-arm The Opening calls this.
    public static void Relock()
    {
        Memory.Forget();
        ControlHints.Forget(FastTravelUnlock.HintId);
        ControlHints.Hide(FastTravelUnlock.HintId);
        _lockedFrame = -1;
    }

    // The player has fast travelled: the prompt has done its job.
    public static void Travelled() => ControlHints.Hide(FastTravelUnlock.HintId);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (Instance != null) return;
        var go = new GameObject("FastTravelTutorial");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<FastTravelTutorial>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        _poll -= Time.unscaledDeltaTime;
        if (_poll > 0f) return;
        _poll = PollSeconds;

        if (!GameSession.CareerActive || ControlHints.Taught(FastTravelUnlock.HintId)) return;
        if (!FastTravelUnlock.ShouldPrompt(Locked, false, WeekendObjectiveHUD.CanTravel, PlayerBusy())) return;

        // Urgent, so a hint already up cannot hold it back past the moment it is about.
        ControlHints.Show(FastTravelUnlock.HintId, WeekendObjectiveHUD.TravelKey.ToString().ToUpperInvariant(),
                          InputGlyphs.Pad(PadBindings.TravelThere), "Fast travel to your objective",
                          PromptSeconds, once: true, urgent: true,
                          onPress: () => WeekendObjectiveHUD.TravelThere(), icon: PixelGUI.ActionIcon.Walk,
                          touchText: "Tap FAST TRAVEL on your objective to go straight there");

        // The strip carries the button the phone's line points at, so bring it down with the prompt.
        WeekendObjectiveHUD.Reveal();
    }

    // Anything that has the player's attention or the screen, the result card the meeting just left
    // included: the prompt waits for it to clear rather than timing out underneath it.
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
            || WeekendResultCard.IsOpen
            || WeekendTrackChangeover.Staging;
    }
}
