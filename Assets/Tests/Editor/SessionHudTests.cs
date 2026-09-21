using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode coverage for SessionHud.HeadOut — the reset PitLaneStart runs the first time the player takes
// the car out in a session: the F1 lap timing readout up, every other F-key panel down.
//
// The panels are stood up in the scene with everything switched ON (lap timing OFF), the reset is run,
// and each one is read back. Awake does not run in EditMode, so nothing here depends on a panel's own
// start-up — only on what HeadOut writes.
//
// SessionHud and the panels live in Assembly-CSharp, which an asmdef can't reference, so they're reached
// by reflection — the same way GolfCartTests reaches the cart.
public class SessionHudTests
{
    static System.Type T(string name) => System.Type.GetType(name + ", Assembly-CSharp");

    readonly List<GameObject> _made = new();

    [TearDown]
    public void TearDown()
    {
        foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
        _made.Clear();
        var diag = T("FormationDiagnostics");
        diag?.GetField("Open", BindingFlags.Public | BindingFlags.Static)?.SetValue(null, false);
    }

    Component Make(string type)
    {
        var t = T(type);
        Assert.IsNotNull(t, type + " not found in Assembly-CSharp");
        var go = new GameObject("SessionHudTest_" + type);
        _made.Add(go);
        return go.AddComponent(t);
    }

    static void Set(Component c, string member, object value)
    {
        var t = c.GetType();
        var f = t.GetField(member, BindingFlags.Public | BindingFlags.Instance);
        if (f != null) { f.SetValue(c, value); return; }
        var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(p, $"{t.Name}.{member} not found");
        p.SetValue(c, value);
    }

    static object Get(Component c, string member)
    {
        var t = c.GetType();
        var f = t.GetField(member, BindingFlags.Public | BindingFlags.Instance);
        if (f != null) return f.GetValue(c);
        var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(p, $"{t.Name}.{member} not found");
        return p.GetValue(c);
    }

    static void HeadOut()
    {
        var hud = T("SessionHud");
        Assert.IsNotNull(hud, "SessionHud not found in Assembly-CSharp");
        hud.GetMethod("HeadOut", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
    }

    [Test]
    public void HeadingOut_LeavesOnlyLapTimingUp()
    {
        var lapTiming = Make("LapTimingManager");
        Set(lapTiming, "showPlayerHud", false);

        // Each panel, the member that says whether it is up, and the value that means "up".
        var panels = new (Component c, string member, object shown, object hidden)[]
        {
            (Make("LeaderboardUI"),        "Visible",       true,  false),   // F2
            (Make("TeamSwitchController"), "Hidden",        false, true),    // F3
            (Make("RivalryFeed"),          "StandingsOpen", true,  false),   // F4
            (Make("DriverInfoPanel"),      "Open",          true,  false),   // F5
            (Make("TireTempWearUI"),       "visible",       true,  false),   // F6
            (Make("PixelUIShowcase"),      "open",          true,  false),   // F6
            (Make("PlayerTelemetryHUD"),   "visible",       true,  false),   // F7
            (Make("HandlingTuner"),        "Open",          true,  false),   // F9
        };
        foreach (var p in panels) Set(p.c, p.member, p.shown);
        var diag = T("FormationDiagnostics").GetField("Open", BindingFlags.Public | BindingFlags.Static);
        diag.SetValue(null, true);                                              // F8

        HeadOut();

        Assert.AreEqual(true, Get(lapTiming, "showPlayerHud"), "F1 lap timing should be up");
        foreach (var p in panels)
            Assert.AreEqual(p.hidden, Get(p.c, p.member), $"{p.c.GetType().Name} should be hidden");
        Assert.AreEqual(false, diag.GetValue(null), "F8 formation diagnostics should be closed");
    }

    [Test]
    public void HeadingOut_WithNoPanelsInTheScene_DoesNotThrow()
    {
        Assert.DoesNotThrow(HeadOut);
    }
}
