using System;
using Unity.Netcode;
using UnityEngine;

// Who is who in a co-op career session.
//
// The host is player one and owns the career outright: the save, the ledger, the weekend clock, the track
// selection, the AI field, every booking. The guest persists nothing and decides nothing — state flows one
// way, host to guest, which is what removes the whole class of merge/conflict problems a shared save would
// have.
//
// The practical rule this exists to answer: "should I spawn this?" The guest spawns NOTHING career-side —
// no paddock cast, no RV lot, no sponsor reps, no AI field, no bookings. It receives all of it. Content
// spawners gate on GameSession.CareerActive && !Coop.IsGuest, so the two processes never each decide
// independently what Friday morning looks like.
public static class Coop
{
    // A co-op session is up. Note this is the MODE, not the transport: it flips the instant the launcher
    // commits to co-op, before NetworkManager is listening, so scene code loading in that window branches
    // correctly.
    public static bool Active => GameSession.IsCoop;

    // Player one. True on the host process, and also true when co-op has been chosen but no guest has
    // arrived yet — the host is playing its own career either way.
    public static bool IsHost => Active && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer);

    // Player two: a network session we did not start.
    public static bool IsGuest => Active && NetworkManager.Singleton != null
                                         && NetworkManager.Singleton.IsListening
                                         && !NetworkManager.Singleton.IsServer;

    // True once a second player is actually connected (host side). Content that only matters with someone
    // else in the world — a networked field, a guest avatar — waits on this rather than on Active.
    public static bool GuestPresent => IsHost && GuestClientId != NoGuest;

    public const ulong NoGuest = ulong.MaxValue;

    // The connected guest's client id, or NoGuest. One guest only: this is two-player co-op, and letting a
    // third in would mean deciding which of them possesses which car on a grid the host already filled.
    public static ulong GuestClientId { get; private set; } = NoGuest;

    // Raised on the host when the guest arrives / leaves, and on the guest when it is connected / dropped.
    public static event Action GuestJoined;
    public static event Action GuestLeft;

    static bool _hooked;

    // Called by NetworkLauncher once it has committed to co-op and NetworkManager exists. Idempotent.
    public static void Hook()
    {
        var nm = NetworkManager.Singleton;
        if (_hooked || nm == null) return;
        nm.OnClientConnectedCallback += OnClientConnected;
        nm.OnClientDisconnectCallback += OnClientDisconnected;
        _hooked = true;
    }

    static void OnClientConnected(ulong clientId)
    {
        if (!Active) return;

        var nm = NetworkManager.Singleton;
        // Host side: the first non-host client to arrive is the guest. A second one is ignored rather than
        // refused outright — the session cap does the refusing; this just never re-points GuestClientId.
        if (nm != null && nm.IsServer)
        {
            if (clientId == NetworkManager.ServerClientId) return;
            if (GuestClientId != NoGuest) return;
            GuestClientId = clientId;
        }
        else
        {
            // Guest side: our own connection landing.
            if (clientId != nm.LocalClientId) return;
            GuestClientId = clientId;
        }

        GuestJoined?.Invoke();
    }

    static void OnClientDisconnected(ulong clientId)
    {
        if (!Active) return;
        var nm = NetworkManager.Singleton;

        if (nm != null && nm.IsServer)
        {
            if (clientId != GuestClientId) return;
            GuestClientId = NoGuest;
            GuestLeft?.Invoke();
            return;
        }

        // Guest side: losing the host ends the session. There is no host migration — the career being
        // played belongs to the host's save, so there is nothing for the guest to migrate INTO.
        GuestClientId = NoGuest;
        GuestLeft?.Invoke();
    }

    // Full teardown back to single player. Called by the launcher on Leave and on an unrecoverable
    // transport failure.
    public static void Reset()
    {
        var nm = NetworkManager.Singleton;
        if (_hooked && nm != null)
        {
            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        _hooked = false;
        GuestClientId = NoGuest;
    }

    // ------------------------------------------------------------------ recall

    // Same-scene sync: pull the guest to the host. Scene changes carry the guest automatically (NGO scene
    // management, via CoopScene.Load), but an in-place time skip or the host simply walking off does not —
    // so this exists for the cases no scene load covers.
    //
    // A no-op when the guest is already close by: being dragged across the paddock you were already stood
    // in is worse than not being moved at all.
    public static void RecallGuest(float radius = 25f)
    {
        if (!GuestPresent) return;
        if (CareerMirror.Instance == null)
        {
            Debug.LogWarning("Coop.RecallGuest: no CareerMirror in the session — nothing to send the recall on.");
            return;
        }
        CareerMirror.Instance.RecallGuest(radius);
    }
}
