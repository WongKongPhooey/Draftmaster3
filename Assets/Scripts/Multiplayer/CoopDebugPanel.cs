#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.IO;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using WeekendLedger = Draftmaster.Weekend.WeekendLedger;

// F12 co-op test panel. Editor and development builds only.
//
// Co-op has no shipping UI yet — opening your career to a friend is an in-career action and belongs on the
// pause menu or the phone, which is a design job rather than a plumbing one. This exists so the whole thing
// can be driven and watched before that lands: host, join, see what each peer thinks is true, and leave.
//
// Installs itself into every scene (RuntimeInitializeOnLoadMethod) so there is nothing to wire up, and
// creates a NetworkLauncher if the scene has none — the race scene does not carry one, and hosting co-op
// only ever happens from inside a career scene.
public class CoopDebugPanel : MonoBehaviour
{
    public static Key ToggleKey = Key.F12;

    // Test convenience: the host drops its join code here so the guest's JOIN button can pre-fill it.
    // Two Multiplayer Play Mode players are on one machine, so this saves retyping a code every iteration.
    // Never used outside the editor.
    static string CodeDropPath => Path.Combine(Path.GetTempPath(), "draftmaster_coop_code.txt");

    bool _open;
    string _code = "";
    string _status = "";
    Rect _rect = new Rect(12f, 12f, 340f, 0f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<CoopDebugPanel>() != null) return;
        var go = new GameObject("CoopDebugPanel");
        go.AddComponent<CoopDebugPanel>();
        DontDestroyOnLoad(go);
    }

    void Start()
    {
        if (NetworkLauncher.Instance != null)
            NetworkLauncher.Instance.StatusChanged += s => _status = s;
    }

    // Smoothed frame time, so "it feels choppy" can be read as a number and told apart from the connection.
    // A round trip of 60ms with the frame rate on the floor is a different fault from a steady 60fps with
    // the round trip at 300ms, and until both are on screen at once the two are guesswork.
    float _smoothedDt;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && ToggleKey != Key.None && kb[ToggleKey].wasPressedThisFrame) _open = !_open;

        float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        _smoothedDt = _smoothedDt <= 0f ? dt : Mathf.Lerp(_smoothedDt, dt, 0.1f);
    }

    // Round trip to the other peer, in milliseconds, as the transport measures it. -1 when there is nobody
    // to measure against.
    static float RoundTripMs(NetworkManager nm)
    {
        if (nm == null || !nm.IsListening) return -1f;
        var transport = nm.NetworkConfig != null ? nm.NetworkConfig.NetworkTransport : null;
        if (transport == null) return -1f;

        ulong peer = nm.IsServer ? Coop.GuestClientId : NetworkManager.ServerClientId;
        if (nm.IsServer && !Coop.GuestPresent) return -1f;
        return transport.GetCurrentRtt(peer);
    }

    void OnGUI()
    {
        if (!_open) return;
        _rect.height = 0f;
        _rect = GUILayout.Window(GetInstanceID(), _rect, Draw, "CO-OP (F12)");
    }

    void Draw(int id)
    {
        var nm = NetworkManager.Singleton;
        bool listening = nm != null && nm.IsListening;

        GUILayout.Label($"mode      {GameSession.CurrentMode}");
        GUILayout.Label($"role      {(Coop.IsGuest ? "GUEST" : Coop.Active ? "HOST" : "-")}   " +
                        $"transport {(listening ? (nm.IsServer ? "server" : "client") : "down")}");
        GUILayout.Label($"guest     {(Coop.GuestPresent ? $"present (id {Coop.GuestClientId})" : "-")}");
        GUILayout.Label($"weekend   {WeekendLedger.ClockText}   session {RaceWeekend.Current}" +
                        $"{(RaceWeekend.SessionLive ? " LIVE" : "")}");
        GUILayout.Label($"track     {TrackSelection.CurrentId}   phase {RaceStart.Current}");
        GUILayout.Label($"field     {NetworkedAICar.All.Count} networked cars");
        GUILayout.Label($"driving   {(CoopPossession.GuestCar != null ? CoopPossession.GuestCar.name : "-")}");
        GUILayout.Label($"guest is  {(CoopPossession.GuestCarNumber > 0 ? $"#{CoopPossession.GuestCarNumber} {CoopPossession.GuestDriverName}" : "nobody yet")}" +
                        $"   field {(GridSpawner.FieldReady ? "ready" : "building")}");

        // The bodies layer, which is what "I can't see the other player" is actually about. myBody says
        // whether the other peer is being told to draw you at all; prefab says whether this scene can build
        // them; pose is how long ago they last said where they were, and where that was.
        float age = CoopBodies.LastPoseAt > 0f ? Time.unscaledTime - CoopBodies.LastPoseAt : -1f;
        GUILayout.Label($"bodies    myBody {(CoopBodies.LocalHasBody ? "yes" : "NO")}   " +
                        $"prefab {(CoopBodies.CanBuildPuppets ? "yes" : "NO")}   " +
                        $"puppets {CoopBodies.PuppetCount}");
        GUILayout.Label($"pose      {(age < 0f ? "never received" : $"{age:0.0}s ago at {CoopBodies.LastPosePos}")}");

        // Choppiness has two quite different causes and this line says which one is happening.
        float rtt = RoundTripMs(nm);
        float fps = _smoothedDt > 0f ? 1f / _smoothedDt : 0f;
        GUILayout.Label($"net       rtt {(rtt < 0f ? "-" : $"{rtt:0}ms")}   " +
                        $"local {fps:0} fps ({_smoothedDt * 1000f:0.0}ms)");

        // The paddock layer: whether both peers are standing in the same paddock. A guest that never took
        // the host's row solves the whole block for itself, and every venue laid out beside it lands
        // somewhere else — which reads in game as the other player standing outside the room you are in.
        var lot = DriverMotorhomeLot.Instance;
        string lotState = lot == null ? "none in scene"
                        : lot.Built ? $"{lot.Slots.Count} rigs, player place {lot.PlayerPlace}"
                        : lot.AwaitingHostLayout ? "waiting for the host's layout"
                        : "building";
        GUILayout.Label($"paddock   {lotState}");
        if (Coop.IsGuest)
        {
            float layoutAge = CoopPaddockMirror.LastLayoutAt > 0f
                ? Time.unscaledTime - CoopPaddockMirror.LastLayoutAt : -1f;
            GUILayout.Label($"layout    {(layoutAge < 0f ? "never received" : $"{layoutAge:0.0}s ago")}" +
                            $"   applied {(CoopPaddockMirror.GuestApplied ? "yes" : "NO")}");
        }
        else if (Coop.Active)
        {
            GUILayout.Label($"layout    sent {CoopPaddockMirror.LastSentSlots} slots");
        }
        if (OnFootController.Current != null)
            GUILayout.Label($"me        {OnFootController.Current.transform.position}");

        string code = NetworkLauncher.Instance != null ? NetworkLauncher.Instance.JoinCode : null;
        if (!string.IsNullOrEmpty(code)) GUILayout.Label($"CODE      {code}");
        if (!string.IsNullOrEmpty(_status)) GUILayout.Label($"status    {_status}");

        GUILayout.Space(6f);

        if (!Coop.Active)
        {
            if (GUILayout.Button("HOST CO-OP (open my career)"))
            {
                Launcher()?.HostCoop();
                // Drop the code for the other Play Mode player once the session actually exists.
                StartCoroutine(DropCodeWhenReady());
            }

            GUILayout.BeginHorizontal();
            _code = GUILayout.TextField(_code ?? "", 12).ToUpperInvariant();
            if (GUILayout.Button("PASTE", GUILayout.Width(60f))) _code = ReadDroppedCode();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("JOIN CO-OP"))
            {
                if (string.IsNullOrWhiteSpace(_code)) _code = ReadDroppedCode();
                Launcher()?.JoinCoop(_code);
            }
        }
        else
        {
            if (GUILayout.Button("LEAVE")) Launcher()?.Leave();
            if (Coop.IsHost && Coop.GuestPresent && GUILayout.Button("RECALL GUEST")) Coop.RecallGuest();
        }

        GUI.DragWindow();
    }

    // The race scene carries no NetworkLauncher — it was only ever needed on the multiplayer menu. Hosting
    // co-op happens from inside a career scene, so make one when there isn't one.
    static NetworkLauncher Launcher()
    {
        if (NetworkLauncher.Instance != null) return NetworkLauncher.Instance;
        var go = new GameObject("NetworkLauncher");
        return go.AddComponent<NetworkLauncher>();
    }

    System.Collections.IEnumerator DropCodeWhenReady()
    {
        for (float t = 0f; t < 30f; t += Time.unscaledDeltaTime)
        {
            string c = NetworkLauncher.Instance != null ? NetworkLauncher.Instance.JoinCode : null;
            if (!string.IsNullOrEmpty(c))
            {
                try { File.WriteAllText(CodeDropPath, c); } catch { }
                yield break;
            }
            yield return null;
        }
    }

    static string ReadDroppedCode()
    {
        try { return File.Exists(CodeDropPath) ? File.ReadAllText(CodeDropPath).Trim() : ""; }
        catch { return ""; }
    }
}
#endif
