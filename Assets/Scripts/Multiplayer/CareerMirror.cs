using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using WeekendLedger = Draftmaster.Weekend.WeekendLedger;

// Keeps the co-op guest's career state in step with the host's.
//
// The host owns the career outright, so this replicates in one direction only: host reads its own statics,
// guest applies them and never writes back. What travels is the whole weekend rather than a field at a time
// — the ledger is one JSON book (slot, clock, done/missed, meters, earnings, headlines) and shipping it
// whole is both simpler and immune to a partial update landing between two related fields.
//
// Built on NGO named messages rather than a NetworkBehaviour, because a NetworkObject would buy nothing
// here: there is no transform to replicate, no ownership to hand around and no scene presence. Named
// messages need no prefab, no registration on either peer and no spawn, so the mirror is just a component
// the launcher adds to the NetworkManager and it works the moment the transport is up.
//
// Why polling rather than events: every one of these is a PlayerPrefs-backed static with no change
// notification of its own (WeekendLedger.Changed exists, the rest do not), and the career writes them from
// a dozen places. A cheap poll at 4Hz on the host catches all of it without threading a callback through
// every writer.
//
// A guest that joins mid-weekend gets the full state pushed to it alone on connect, which is what makes
// drop-in at any moment work: there is no lobby and no starting state to agree on, just "here is where the
// weekend currently is".
public class CareerMirror : MonoBehaviour
{
    public static CareerMirror Instance { get; private set; }

    const string StateMessage = "coop.career.state";
    const string RecallMessage = "coop.career.recall";

    [Tooltip("Seconds between host-side change checks. Career state moves at human pace; this does not need to be fast.")]
    public float pushInterval = 0.25f;

    [Tooltip("Metres. A recall leaves the guest alone if they are already this close to the host — being dragged across a paddock you were already stood in is worse than not being moved.")]
    public float defaultRecallRadius = 25f;

    // Last values pushed, for change detection on the host.
    string _lastLedger;
    int _lastWeekendId = int.MinValue;
    int _lastSession = -1;
    bool _lastLive;
    string _lastTrack;
    int _lastPhase = -1;
    bool _pushedOnce;

    [Tooltip("Metres moved by the host in a SINGLE frame that count as a jump rather than a walk, and pull the guest along. Walking flat out covers a fraction of a metre per frame, so anything near this is a teleport.")]
    public float jumpThreshold = 15f;

    float _nextPush;
    bool _registered;
    bool _capturedGuestPrefs;

    // Host-side teleport watch. Rather than maintaining a list of every place the career moves the player —
    // a time skip, a weekend marker, a cutscene, an obligation settled on the spot — the host simply notices
    // its own body jumping and pulls the guest after it. One rule, and it cannot be forgotten at a new call
    // site later.
    Vector3 _lastHostPos;
    bool _haveHostPos;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Unregister();
        if (Instance == this) Instance = null;
    }

    // A scene change carries the guest with it (CoopScene.Load), but it drops them at that scene's own
    // spawn, which can be the far side of the paddock from where the host came up. Close the gap once
    // both bodies actually exist.
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _haveHostPos = false;   // a new scene's coordinates are not a jump within the old one
        if (Coop.GuestPresent) StartCoroutine(RecallOnceSettled());
    }

    IEnumerator RecallOnceSettled()
    {
        // The paddock stands itself up over a few frames — PitLaneStart spawns the body, the cast follows.
        // Wait for a body rather than a fixed delay, then give the guest a moment to arrive in the scene too.
        for (float t = 0f; t < 10f && LocalPlayerTransform() == null; t += Time.unscaledDeltaTime)
            yield return null;
        yield return new WaitForSecondsRealtime(0.5f);
        Coop.RecallGuest();
    }

    void Update()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) { Unregister(); return; }

        Register(nm);

        if (!nm.IsServer) return;

        WatchForHostJump();

        if (Time.unscaledTime < _nextPush) return;
        _nextPush = Time.unscaledTime + pushInterval;
        PushIfChanged(nm);
    }

    // The host went somewhere in one frame that they could not have walked: a time skip, a marker teleport,
    // a cutscene placing them. Bring the guest along — RecallGuest already leaves them alone if they happen
    // to be standing there already.
    void WatchForHostJump()
    {
        var me = LocalPlayerTransform();
        if (me == null) { _haveHostPos = false; return; }

        Vector3 now = me.position;
        if (_haveHostPos && (now - _lastHostPos).sqrMagnitude > jumpThreshold * jumpThreshold)
            Coop.RecallGuest();

        _lastHostPos = now;
        _haveHostPos = true;
    }

    // ------------------------------------------------------------------ wiring

    void Register(NetworkManager nm)
    {
        if (_registered) return;
        var msg = nm.CustomMessagingManager;
        if (msg == null) return;

        if (nm.IsServer)
        {
            // A guest arriving mid-weekend needs the current state immediately, not on the next change.
            nm.OnClientConnectedCallback += OnClientConnected;
        }
        else
        {
            msg.RegisterNamedMessageHandler(StateMessage, OnStateReceived);
            msg.RegisterNamedMessageHandler(RecallMessage, OnRecallReceived);

            // Park this player's own career before we start writing the host's over the top of it, and
            // stop anything local from persisting into the weekend we are about to be handed.
            CoopGuestPrefs.Capture();
            WeekendLedger.ReadOnly = true;
            _capturedGuestPrefs = true;
        }

        _registered = true;
    }

    void Unregister()
    {
        if (!_registered) return;
        _registered = false;

        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientConnectedCallback -= OnClientConnected;
            var msg = nm.CustomMessagingManager;
            if (msg != null)
            {
                msg.UnregisterNamedMessageHandler(StateMessage);
                msg.UnregisterNamedMessageHandler(RecallMessage);
            }
        }

        // Give this player their own career back the moment the session ends, however it ended.
        if (_capturedGuestPrefs)
        {
            WeekendLedger.ReadOnly = false;
            CoopGuestPrefs.Restore();
            _capturedGuestPrefs = false;
        }

        _pushedOnce = false;
    }

    void OnClientConnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer || clientId == NetworkManager.ServerClientId) return;
        SendState(nm, clientId);
    }

    // ------------------------------------------------------------------ host side

    void PushIfChanged(NetworkManager nm)
    {
        string ledger = WeekendLedger.ExportJson();
        int weekendId = RaceWeekend.WeekendId;
        int session = (int)RaceWeekend.Current;
        bool live = RaceWeekend.SessionLive;
        string track = TrackSelection.CurrentId ?? "";
        int phase = (int)RaceStart.Current;

        if (_pushedOnce
            && ledger == _lastLedger
            && weekendId == _lastWeekendId
            && session == _lastSession
            && live == _lastLive
            && track == _lastTrack
            && phase == _lastPhase) return;

        _lastLedger = ledger;
        _lastWeekendId = weekendId;
        _lastSession = session;
        _lastLive = live;
        _lastTrack = track;
        _lastPhase = phase;
        _pushedOnce = true;

        SendState(nm, Coop.NoGuest);   // NoGuest = broadcast to every connected client
    }

    // Reads live rather than replaying the cached push, so a guest arriving between two pushes still lands
    // on the truth.
    void SendState(NetworkManager nm, ulong onlyTo)
    {
        var msg = nm.CustomMessagingManager;
        if (msg == null) return;

        string ledger = WeekendLedger.ExportJson() ?? "";
        string track = TrackSelection.CurrentId ?? "";

        // Sized for the two strings (length prefix + UTF-16 payload) plus the fixed fields, with slack.
        int size = 64 + ledger.Length * 2 + track.Length * 2;
        using var writer = new FastBufferWriter(size, Allocator.Temp, 1024 * 1024);

        writer.WriteValueSafe(ledger);
        writer.WriteValueSafe(RaceWeekend.WeekendId);
        writer.WriteValueSafe((int)RaceWeekend.Current);
        writer.WriteValueSafe(RaceWeekend.SessionLive);
        writer.WriteValueSafe(track);
        writer.WriteValueSafe((int)RaceStart.Current);

        // The ledger can run to a few KB once a weekend has headlines in it, so this has to be able to
        // fragment rather than being capped at one MTU.
        if (onlyTo == Coop.NoGuest)
            msg.SendNamedMessageToAll(StateMessage, writer, NetworkDelivery.ReliableFragmentedSequenced);
        else
            msg.SendNamedMessage(StateMessage, onlyTo, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }

    // ------------------------------------------------------------------ guest side

    void OnStateReceived(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string ledgerJson);
        reader.ReadValueSafe(out int weekendId);
        reader.ReadValueSafe(out int session);
        reader.ReadValueSafe(out bool sessionLive);
        reader.ReadValueSafe(out string trackId);
        reader.ReadValueSafe(out int racePhase);

        // The book, held in memory only — ImportJson never writes the guest's prefs.
        WeekendLedger.ImportJson(ledgerJson);

        // The prefs-backed ones are written directly rather than through their own setters: those setters
        // stamp the save date (CareerSave.Stamp), and the guest's save must not be dated by somebody else's
        // weekend moving. CoopGuestPrefs puts all of these back when the session ends.
        PlayerPrefs.SetInt("raceweekend.id", weekendId);
        PlayerPrefs.SetInt("raceweekend.sessionlive", sessionLive ? 1 : 0);
        if (!string.IsNullOrEmpty(trackId)) PlayerPrefs.SetString("track.current", trackId);

        // Plain statics — no prefs, nothing to protect.
        RaceWeekend.Current = (RaceWeekend.Session)Mathf.Clamp(session, 0, 2);
        RaceStart.Current = (RaceStart.Phase)Mathf.Clamp(racePhase, 0, 2);
    }

    // ------------------------------------------------------------------ recall

    // Host-side entry point (via Coop.RecallGuest). Sends the host's current world position; the guest
    // decides whether it is already close enough to ignore it.
    public void RecallGuest(float radius)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || !nm.IsServer) return;
        var msg = nm.CustomMessagingManager;
        if (msg == null) return;

        var host = LocalPlayerTransform();
        if (host == null)
        {
            Debug.LogWarning("CareerMirror.RecallGuest: no local player to recall the guest to.");
            return;
        }

        using var writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe(host.position);
        writer.WriteValueSafe(radius <= 0f ? defaultRecallRadius : radius);
        msg.SendNamedMessageToAll(RecallMessage, writer, NetworkDelivery.Reliable);
    }

    void OnRecallReceived(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out Vector3 hostPosition);
        reader.ReadValueSafe(out float radius);

        var me = LocalPlayerTransform();
        if (me == null) return;   // no body yet — nothing to move

        if ((me.position - hostPosition).sqrMagnitude <= radius * radius) return;   // already there

        // Offset so the two are not stacked in the same pixel.
        me.position = hostPosition + new Vector3(2.5f, 0f, 0f);
    }

    // The thing THIS player is currently steering: their own walking body if they have one, otherwise their
    // car. OnFootController.Current skips remote puppets, so this is never the other player's avatar.
    static Transform LocalPlayerTransform()
    {
        var onFoot = OnFootController.Current;
        if (onFoot != null) return onFoot.transform;

        var car = CarIdentity.FindPlayerCar();
        return car != null ? car.transform : null;
    }
}
