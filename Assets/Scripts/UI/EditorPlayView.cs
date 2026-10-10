#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

// In the editor: is the Device Simulator the view the game is being played in, rather than the Game view?
//
// UnityEngine.Device.Application can't answer that on its own. Once a Simulator window exists — even as a tab
// behind the Game view — its simulated platform and touchscreen stay in force, so the Game view reported a
// phone: `?` tap prompts, the touch pause button and the strip's arrow tab, on a PC with a keyboard. What
// decides is which play-mode view Unity is rendering the game into. That is internal (PlayModeView), so it's
// read by reflection; if a Unity upgrade renames it, this falls back to what UnityEngine.Device says and logs
// once.
//
// The Simulator also switches the real mouse OFF for as long as its window exists, and feeds mouse input in
// as touches through its own touchscreen. Left like that, the Game view could not be clicked at all (the
// title menu ignored every click on PC while taps worked in the Simulator). So while the Game view is the
// one being played, the mouse is switched back on; when the Simulator is shown again, it gets the mouse
// back off — only if it was this that turned it on.
[InitializeOnLoad]
public static class EditorPlayView
{
    static bool _mouseRestored;

    static EditorPlayView() => EditorApplication.update += KeepMouseForGameView;

    static void KeepMouseForGameView()
    {
        if (!EditorApplication.isPlaying) { _mouseRestored = false; return; }
        var mouse = Mouse.current ?? InputSystem.GetDevice<Mouse>();
        if (mouse == null) return;

        if (!Read())
        {
            if (mouse.enabled) return;
            InputSystem.EnableDevice(mouse);
            _mouseRestored = true;
        }
        else if (_mouseRestored && mouse.enabled)
        {
            InputSystem.DisableDevice(mouse);
            _mouseRestored = false;
        }
    }

    static Func<EditorWindow> _main;
    static bool _resolved;
    static int _frame = -1;
    static bool _simulator;

    public static bool SimulatorShowing
    {
        get
        {
            if (Time.frameCount == _frame) return _simulator;   // asked many times a frame from OnGUI
            _frame = Time.frameCount;
            _simulator = Read();
            return _simulator;
        }
    }

    static bool Read()
    {
        Resolve();
        if (_main == null) return UnityEngine.Device.Application.isMobilePlatform;
        var view = _main();
        if (view == null) return UnityEngine.Device.Application.isMobilePlatform;
        return view.GetType().Name.IndexOf("Simulator", StringComparison.Ordinal) >= 0;
    }

    static void Resolve()
    {
        if (_resolved) return;
        _resolved = true;
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        foreach (var name in new[] { "GetMainPlayModeView", "GetLastFocusedPlayModeView" })
        {
            var m = type?.GetMethod(name, flags, null, Type.EmptyTypes, null);
            if (m == null) continue;
            _main = () => m.Invoke(null, null) as EditorWindow;
            Debug.Log($"[EditorPlayView] play view from PlayModeView.{name}: {_main()?.GetType().Name ?? "none"}");
            return;
        }
        Debug.LogWarning("[EditorPlayView] PlayModeView has no main-view lookup in this Unity — phone vs PC " +
                         "follows UnityEngine.Device, which a Simulator tab can leave reporting a phone.");
    }
}
#endif
