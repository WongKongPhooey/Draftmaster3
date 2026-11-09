using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

// Keeps the MCP for Unity session alive (Draftmaster > Tools > MCP Session Watchdog, on by default).
//
// WHY. The session kept dying after domain reloads with the server perfectly healthy (/health 200,
// /api/instances empty) — it cost an overnight run 7 hours once. The cause is in the package (9.6.8): while the
// MCP For Unity window is open it polls the local server every 0.75 s with a TCP connect that has a 50 ms
// timeout (ServerManagementService.IsLocalHttpServerReachable), and the first poll that misses — easy right
// after a reload, when the editor is busy — is taken as "the server died": McpConnectionSection ends the
// session ("Server no longer running; ending orphaned session.") and nothing ever starts it again.
//
// WHAT. Every few seconds, when the editor is idle: if there is no session but the local server answers a
// connect with a sane timeout, start the session again. If the server itself has gone (two checks running —
// it was killed, crashed, or replaced by a package update), start it the way the window's Start Server button
// does, wait for it, then start the session. Backs off after a failed start. Logs each restart.
// Closing the MCP For Unity window also stops the false alarm at its source — the check only runs from it.
[InitializeOnLoad]
public static class McpSessionWatchdog
{
    const string PrefKey = "Draftmaster.McpSessionWatchdog";
    const string MenuPath = "Draftmaster/Tools/MCP Session Watchdog";
    const double CheckEverySeconds = 4.0;
    const double BackOffSeconds = 30.0;
    const int ConnectTimeoutMs = 1500;
    const string ServerPrefKey = "Draftmaster.McpSessionWatchdog.StartServer";
    const string ServerMenuPath = "Draftmaster/Tools/MCP Session Watchdog Starts The Server";

    static double _nextCheck;
    static bool _busy;
    static int _serverDownChecks;

    public static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    static McpSessionWatchdog()
    {
        // A reload is exactly when the session tends to drop: look again shortly after one.
        _nextCheck = EditorApplication.timeSinceStartup + 6.0;
        EditorApplication.update += Tick;
    }

    [MenuItem(MenuPath)]
    static void Toggle()
    {
        Enabled = !Enabled;
        Debug.Log($"[MCP watchdog] {(Enabled ? "on — dropped sessions are restarted" : "off")}.");
    }

    [MenuItem(MenuPath, true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    // Also bring the local server back when it is gone, not just the session. On by default; turn it off if
    // you ever stop the server on purpose and want it to stay stopped.
    public static bool StartsServer
    {
        get => EditorPrefs.GetBool(ServerPrefKey, true);
        set => EditorPrefs.SetBool(ServerPrefKey, value);
    }

    [MenuItem(ServerMenuPath)]
    static void ToggleServer()
    {
        StartsServer = !StartsServer;
        Debug.Log($"[MCP watchdog] {(StartsServer ? "will" : "won't")} start the local MCP server when it is down.");
    }

    [MenuItem(ServerMenuPath, true)]
    static bool ToggleServerValidate()
    {
        Menu.SetChecked(ServerMenuPath, StartsServer);
        return true;
    }

    static void Tick()
    {
        if (_busy || EditorApplication.timeSinceStartup < _nextCheck) return;
        _nextCheck = EditorApplication.timeSinceStartup + CheckEverySeconds;
        if (!Enabled) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying) return;

        bool running;
        string url;
        try
        {
            if (HttpEndpointUtility.IsRemoteScope()) return;   // a remote server is somebody else's business
            running = MCPServiceLocator.Bridge.IsRunning;
            url = HttpEndpointUtility.GetLocalBaseUrl();
        }
        catch (Exception) { return; }
        if (running) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Port <= 0) return;

        _busy = true;
        _ = Restart(uri.Host, uri.Port);
    }

    static async Task Restart(string host, int port)
    {
        try
        {
            // Off the main thread: a slow connect must not hitch the editor.
            bool reachable = await Task.Run(() => CanConnect(host, port));
            if (!reachable)
            {
                // Down twice running (not one slow answer): start it, as the window's Start Server button does.
                if (!StartsServer || ++_serverDownChecks < 2) return;
                _serverDownChecks = 0;
                Debug.Log($"[MCP watchdog] No MCP server at {host}:{port} — starting one.");
                if (!MCPServiceLocator.Server.StartLocalHttpServer(quiet: true))
                {
                    Debug.LogWarning("[MCP watchdog] Couldn't start the local MCP server; trying again in 30 s.");
                    _nextCheck = EditorApplication.timeSinceStartup + BackOffSeconds;
                    return;
                }
                // uvx may have to fetch a new server version first: give it up to two minutes.
                for (int i = 0; i < 60 && !reachable; i++)
                {
                    await Task.Delay(2000);
                    reachable = await Task.Run(() => CanConnect(host, port));
                }
                if (!reachable)
                {
                    Debug.LogWarning("[MCP watchdog] Started the MCP server but it never answered; trying again in 30 s.");
                    _nextCheck = EditorApplication.timeSinceStartup + BackOffSeconds;
                    return;
                }
            }
            _serverDownChecks = 0;
            bool started = await MCPServiceLocator.Bridge.StartAsync();
            if (started) Debug.Log($"[MCP watchdog] Session had dropped with the server still up at {host}:{port} — restarted it.");
            else
            {
                Debug.LogWarning("[MCP watchdog] Session is down and restarting it failed; trying again in 30 s.");
                _nextCheck = EditorApplication.timeSinceStartup + BackOffSeconds;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MCP watchdog] Restart failed: {e.Message}; trying again in 30 s.");
            _nextCheck = EditorApplication.timeSinceStartup + BackOffSeconds;
        }
        finally { _busy = false; }
    }

    static bool CanConnect(string host, int port)
    {
        foreach (var h in host == "localhost" ? new[] { "127.0.0.1", "::1" } : new[] { host })
        {
            try
            {
                using var client = new TcpClient(h.Contains(":") ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork);
                var connect = client.ConnectAsync(h, port);
                if (connect.Wait(ConnectTimeoutMs) && client.Connected) return true;
            }
            catch (Exception) { }
        }
        return false;
    }
}
