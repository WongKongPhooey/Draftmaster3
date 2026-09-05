using System.Collections;
using Draftmaster.Weekend;
using UnityEngine;
using UnityEngine.SceneManagement;

// The blackout the circuit changes hands behind.
//
// The runtime half of WeekendHandover. GridSpawner calls Begin() the moment it has claimed a handover and
// then waits on HoldingOff; this puts the screen down, lets it through when nothing can be seen, and brings
// the screen back once the field and its crews are stood where they belong. Done() is the other end.
//
//   GridSpawner                       WeekendTrackChangeover
//   ------------------------------------------------------------------
//   claims the new session
//   Begin()                     -->   fade down (or wave it straight through)
//   while (HoldingOff) wait     <--   screen black: HoldingOff drops
//   clears the old field
//   spawns the new one
//   lays out the boxes          -->   (PitLane.Changed rebuilds every crew)
//   Done()                      -->   fade back up
//
// Staging stays true across the whole thing, and the objective strip reads it: the marker and the banner
// for the session that just took the circuit go up when the screen does, not behind it.
//
// Nothing here is required for the handover to work — with no player on foot to see it, Begin() declines,
// HoldingOff is never raised and GridSpawner runs exactly as it always did.
public class WeekendTrackChangeover : MonoBehaviour
{
    public static WeekendTrackChangeover Instance { get; private set; }

    // The circuit must not change under the player until this drops.
    public static bool HoldingOff { get; private set; }

    // A handover is in flight, screen down or on its way back up. What holds the objective banner.
    public static bool Staging { get; private set; }

    // Statics outlive a play session; a handover interrupted by leaving play mode would otherwise come
    // back holding the next one off forever.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlaySession()
    {
        HoldingOff = false;
        Staging = false;
        Instance = null;
    }

    static WeekendTrackChangeover Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("WeekendTrackChangeover");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<WeekendTrackChangeover>();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance != this) return;
        Instance = null;
        // Whatever was mid-handover went with the object. Never leave the gate up behind us.
        HoldingOff = false;
        Staging = false;
    }

    // A scene load is its own wipe, and the field in the scene we are going to is spawned from scratch by
    // that scene's own GridSpawner.Start rather than handed over. Anything in flight is abandoned here.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        StopAllCoroutines();
        HoldingOff = false;
        if (!Staging) return;

        // Caught mid-wipe by the load. Hand the screen back rather than leaving the scene we have just
        // arrived in under our black rectangle; one that wants the lights left off says so in its own
        // Start, which runs after this.
        Staging = false;
        ScreenFade.FromBlack(0f, WeekendHandover.FadeInSeconds);
    }

    // ------------------------------------------------------------------ the two ends

    // A handover has been claimed. Put the screen down if there is anybody to see it happen, and hold the
    // caller at the gate until it is black.
    //
    // Safe to call again while one is running: the blackout already up is the blackout this one wants.
    public static void Begin()
    {
        if (Staging) return;

        bool wipe = WeekendHandover.ShouldWipe(
            onFootInThePaddock: WeekendVenueAnchor.OnFootPlayer() != null,
            multiplayer: GameSession.IsMultiplayer,
            watchingFromAStand: GrandstandVisit.Watching || GrandstandSpectate.Watching,
            screenAlreadyWiping: ScreenFade.Busy);

        if (!wipe) return;

        var host = Ensure();
        Staging = true;
        HoldingOff = true;
        host.StartCoroutine(host.GoDown());
    }

    // The field is up, its boxes are fitted and its crews are stood in them. Bring the screen back.
    public static void Done()
    {
        if (!Staging || Instance == null) return;
        Instance.StopAllCoroutines();   // the watchdog below has nothing left to watch
        ComeBackUp();
    }

    static void ComeBackUp()
    {
        HoldingOff = false;
        ScreenFade.FromBlack(WeekendHandover.HoldSeconds, WeekendHandover.FadeInSeconds,
                             () => Staging = false);
    }

    // ------------------------------------------------------------------ the wipe

    IEnumerator GoDown()
    {
        // Wait for the player to be free before taking the lights off them. The obligation that moved the
        // clock puts a card up saying what it earned, and the crew chief who ran it is still stood in front
        // of them; blacking out over either is worse than the pop-in this is here to hide. Nothing moves on
        // the circuit while we wait — HoldingOff is already up.
        float waited = 0f;
        while (!WeekendHandover.ReadyToWipe(WeekendResultCard.IsOpen,
                                            WeekendModal.AnyOpen || WeekendScheduleUI.IsOpen,
                                            NPCInteractable.AnyConversationActive || DialogueChoiceUI.IsOpen))
        {
            if (WeekendHandover.GaveUpWaiting(waited)) { LetItThrough(); yield break; }
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        // Somebody else started a wipe while we were waiting (a grandstand seat, a marker gate). Theirs
        // covers the same frames ours would have; ours would only fight it.
        if (ScreenFade.Busy) { LetItThrough(); yield break; }

        // Down. Bounded, because something else taking the fade over mid-wipe (a scene's own wake-up, a
        // marker gate) stops the coroutine that would have reported back — and the gate below must open
        // either way or the circuit never changes hands at all.
        bool black = false;
        float falling = 0f;
        ScreenFade.ToBlack(WeekendHandover.FadeOutSeconds, () => black = true);
        while (!black && falling < WeekendHandover.FadeOutSeconds * 3f + 0.5f)
        {
            falling += Time.unscaledDeltaTime;
            yield return null;
        }

        // Screen down. The circuit is the spawner's to change now.
        HoldingOff = false;

        // ...and the way back up is its to ask for, through Done(). This is only the promise that the
        // screen comes back even if it never does — a spawner destroyed mid-handover, a track with no
        // geometry in it, a database that never opened.
        float blackFor = 0f;
        while (!WeekendHandover.StagedLongEnough(blackFor))
        {
            blackFor += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning("WeekendTrackChangeover: the handover never reported back — bringing the screen up anyway.");
        ComeBackUp();
    }

    // Give up on the wipe and let the handover happen in the open, exactly as it did before any of this
    // existed. Better a moment of pop-in than a black screen over something the player is reading.
    void LetItThrough()
    {
        HoldingOff = false;
        Staging = false;
    }
}
