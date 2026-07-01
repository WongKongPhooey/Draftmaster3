using System.Collections.Generic;
using UnityEngine;

// Full-field timing screen, opened from the crew-chief "Timing" button. Rows come from
// LapTimingManager, ranked by best lap (cars with no time yet sort to the bottom, by laps run).
public class TimingScreenUI : MonoBehaviour
{
    public static TimingScreenUI Instance { get; private set; }

    public bool visible;

    GUIStyle _headStyle, _rowStyle, _titleStyle;
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

        if (_titleStyle == null)
        {
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
            _headStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _headStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
            _rowStyle = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _rowStyle.normal.textColor = Color.white;
        }

        _sorted.Clear();
        var rows = lt.Rows;
        for (int i = 0; i < rows.Count; i++) if (rows[i] != null && rows[i].tf != null) _sorted.Add(rows[i]);
        _sorted.Sort((a, b) =>
        {
            bool aHas = a.bestLap > 0f, bHas = b.bestLap > 0f;
            if (aHas != bHas) return aHas ? -1 : 1;
            if (aHas) return a.bestLap.CompareTo(b.bestLap);
            return b.lapsCompleted.CompareTo(a.lapsCompleted);
        });

        float bestOverall = _sorted.Count > 0 ? _sorted[0].bestLap : -1f;

        float w = 560f;
        float h = 64f + _sorted.Count * 19f;
        float x = 24f;
        float y = Mathf.Max(60f, (Screen.height - h) * 0.35f);

        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(new Rect(x - 10f, y - 10f, w + 20f, h + 20f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        string session = RaceWeekend.IsQualifying ? "QUALIFYING" : (RaceWeekend.IsPractice ? "PRACTICE" : "RACE");
        GUI.Label(new Rect(x, y, w, 22f), $"TIMING  —  {session}", _titleStyle);
        y += 26f;
        GUI.Label(new Rect(x, y, w, 18f), $"{"Pos",-5}{"#",-5}{"Driver",-17}{"Laps",-6}{"Last",-11}{"Best",-11}{"Gap",-8}", _headStyle);
        y += 20f;

        for (int i = 0; i < _sorted.Count; i++)
        {
            var c = _sorted[i];
            _rowStyle.normal.textColor = c.isPlayer ? new Color(0.4f, 1f, 0.5f) : Color.white;

            string name = string.IsNullOrEmpty(c.name) ? "?" : (c.name.Length > 15 ? c.name.Substring(0, 15) : c.name);
            string last = LapTimingManager.Format(c.lastLap) + (c.lastLap > 0f && !c.lastValid ? "✕" : "");
            string best = LapTimingManager.Format(c.bestLap);
            string gap = (c.bestLap > 0f && bestOverall > 0f)
                ? (i == 0 ? "—" : $"+{c.bestLap - bestOverall:0.000}")
                : "—";

            GUI.Label(new Rect(x, y, w, 18f),
                $"{("P" + (i + 1)),-5}{("#" + c.carNumber),-5}{name,-17}{c.lapsCompleted,-6}{last,-11}{best,-11}{gap,-8}", _rowStyle);
            y += 19f;
        }
    }
}
