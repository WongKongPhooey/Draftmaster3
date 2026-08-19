using System.Collections.Generic;
using UnityEngine;

// Full-field timing screen, opened from the crew-chief "Timing" button. Rows come from
// LapTimingManager, ranked by best lap (cars with no time yet sort to the bottom, by laps run).
public class TimingScreenUI : MonoBehaviour
{
    public static TimingScreenUI Instance { get; private set; }

    public bool visible;

    readonly List<LapTimingManager.CarTimes> _sorted = new();

    public static TimingScreenUI Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("TimingScreenUI");
            Instance = go.AddComponent<TimingScreenUI>();
        }
        return Instance;
    }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public void Toggle() { visible = !visible; }
    public void Hide() { visible = false; }

    void OnGUI()
    {
        if (!visible) return;
        var lt = LapTimingManager.Instance;
        if (lt == null) return;

        lt.RankByBest(_sorted);

        float bestOverall = _sorted.Count > 0 ? _sorted[0].bestLap : -1f;

        float row = PixelGUI.Px(11f);
        float w = PixelGUI.Px(292f);
        float h = PixelGUI.Px(42f) + _sorted.Count * row;
        float x = PixelGUI.Px(12f);
        float y = Mathf.Max(PixelGUI.Px(30f), Mathf.Round((Screen.height - h) * 0.35f));

        PixelGUI.Panel(new Rect(x, y, w, h), focused: true);
        var c0 = PixelGUI.PanelContent(new Rect(x, y, w, h), 8f);
        float cx = c0.x, cy = c0.y;

        string session = RaceWeekend.IsQualifying ? "QUALIFYING" : (RaceWeekend.IsPractice ? "PRACTICE" : "RACE");
        GUI.Label(new Rect(cx, cy, c0.width, PixelGUI.Px(18f)), $"TIMING · {session}", PixelGUI.Heading);
        cy += PixelGUI.Px(20f);
        GUI.Label(new Rect(cx, cy, c0.width, PixelGUI.Px(10f)),
                  $"{"POS",-5}{"#",-5}{"DRIVER",-17}{"LAPS",-6}{"LAST",-11}{"BEST",-11}{"GAP",-8}",
                  PixelGUI.LabelDim);
        cy += PixelGUI.Px(11f);
        PixelGUI.Rule(cx, cy, c0.width);
        cy += PixelGUI.Px(3f);

        var style = PixelGUI.Data;
        var prev = style.normal.textColor;
        for (int i = 0; i < _sorted.Count; i++)
        {
            var c = _sorted[i];
            // Purple-for-fastest is a broadcast idiom this palette has no room for, so the session's best
            // lap takes the one accent and the player's own row is the only other marked line.
            style.normal.textColor = i == 0 && c.bestLap > 0f ? PixelGUI.Gold
                                   : c.isPlayer ? PixelGUI.Confirm
                                   : PixelGUI.Text;

            string name = string.IsNullOrEmpty(c.name) ? "?" : (c.name.Length > 15 ? c.name.Substring(0, 15) : c.name);
            string last = LapTimingManager.Format(c.lastLap) + (c.lastLap > 0f && !c.lastValid ? "✕" : "");
            string best = LapTimingManager.Format(c.bestLap);
            string gap = (c.bestLap > 0f && bestOverall > 0f)
                ? (i == 0 ? "—" : $"+{c.bestLap - bestOverall:0.000}")
                : "—";

            GUI.Label(new Rect(cx, cy, c0.width, row),
                $"{("P" + (i + 1)),-5}{("#" + c.carNumber),-5}{name,-17}{c.lapsCompleted,-6}{last,-11}{best,-11}{gap,-8}",
                style);
            cy += row;
        }
        style.normal.textColor = prev;
    }
}
