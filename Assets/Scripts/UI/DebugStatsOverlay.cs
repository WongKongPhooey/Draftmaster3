using UnityEngine;

// Frame-rate readout for checking performance at a glance, on a phone as much as in the editor: FPS, average
// frame time, the 1% and 10% lows, and the single worst frame, all over the last few seconds.
//
// Switched on from the pause menu ("Debug stats") and remembered across runs. Self-installing and kept across
// scene loads, so once it is on it also reads the paddock, the menus and the title screen.
//
// "Lows" are the usual benchmark sense: the average frame rate of the slowest 1% (or 10%) of frames in the
// window. An average of 59.5 fps with a 1% low of 30 is a game that is fine most of the time and hitches
// regularly — which the average alone hides, and which is exactly what this was asked for to show.
//
// Cheap on purpose, because it is measuring the thing it costs: frame times go into a ring buffer every
// frame (no allocation), and the figures and their strings are rebuilt only twice a second.
public class DebugStatsOverlay : MonoBehaviour
{
    const string PrefKey = "debug.stats";

    public static bool Visible
    {
        get => Draftmaster.Weekend.FramePrefs.GetInt(PrefKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Draftmaster.Weekend.FramePrefs.Invalidate();
        }
    }

    // Seconds of history the figures are taken over. Long enough for a 1% low to mean something at 60 fps
    // (300 frames, so the slowest three), short enough to follow a change in what is on screen.
    const float WindowSeconds = 5f;
    const float RefreshSeconds = 0.5f;
    const int Capacity = 1024;   // ~17 s at 60 fps; the window never needs more

    static DebugStatsOverlay _instance;

    readonly float[] _times = new float[Capacity];   // frame durations, seconds, oldest overwritten
    readonly float[] _sorted = new float[Capacity];
    int _head, _count;
    float _nextRefresh;

    string _fps = "", _avg = "", _low1 = "", _low10 = "", _worst = "", _target = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (_instance != null) return;
        var go = new GameObject("DebugStatsOverlay");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<DebugStatsOverlay>();
    }

    void Update()
    {
        // Recorded whether or not it is shown, so switching it on shows a full window straight away.
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;
        _times[_head] = dt;
        _head = (_head + 1) % Capacity;
        if (_count < Capacity) _count++;

        if (!Visible || Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + RefreshSeconds;
        Recompute();
    }

    void Recompute()
    {
        // Walk back from the newest frame until the window is full.
        int n = 0;
        float span = 0f, total = 0f;
        for (int i = 0; i < _count && span < WindowSeconds; i++)
        {
            float t = _times[(_head - 1 - i + Capacity) % Capacity];
            _sorted[n++] = t;
            span += t;
            total += t;
        }
        if (n == 0) return;

        System.Array.Sort(_sorted, 0, n);   // ascending: the slowest frames are at the end

        float avg = total / n;
        _fps = $"{n / total:0.0}";
        _avg = $"{avg * 1000f:0.0} ms";
        _low1 = $"{1f / SlowestAverage(n, 0.01f):0.0}";
        _low10 = $"{1f / SlowestAverage(n, 0.10f):0.0}";
        _worst = $"{_sorted[n - 1] * 1000f:0.0} ms";

        int cap = Application.targetFrameRate;
        _target = cap > 0 ? $"cap {cap}" : "no cap";
    }

    // Mean duration of the slowest `fraction` of the n frames in _sorted (at least one frame).
    float SlowestAverage(int n, float fraction)
    {
        int k = Mathf.Max(1, Mathf.CeilToInt(n * fraction));
        float sum = 0f;
        for (int i = n - k; i < n; i++) sum += _sorted[i];
        return sum / k;
    }

    void OnGUI()
    {
        if (!Visible || Event.current.type != EventType.Repaint) return;

        float line = PixelGUI.DataLineH;
        float pad = PixelGUI.Px(4f);
        float labelW = PixelGUI.Px(52f);
        float valueW = PixelGUI.Px(64f);
        float w = labelW + valueW + pad * 2f;
        float h = line * 6f + pad * 2f;

        // Left edge, a third of the way down: clear of the position/speed block at the top, the touch pedals
        // at the bottom, and the tyre panel and minimap on the right, in both landscape and portrait.
        var safe = Screen.safeArea;
        float x = Mathf.Round(safe.x + PixelGUI.Px(6f));
        float y = Mathf.Round(Screen.height - safe.yMax + Screen.height * 0.3f);

        var old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = old;

        float ly = y + pad;
        Row(x + pad, ref ly, labelW, valueW, line, "FPS", _fps);
        Row(x + pad, ref ly, labelW, valueW, line, "AVG", _avg);
        Row(x + pad, ref ly, labelW, valueW, line, "1% LOW", _low1);
        Row(x + pad, ref ly, labelW, valueW, line, "10% LOW", _low10);
        Row(x + pad, ref ly, labelW, valueW, line, "WORST", _worst);
        Row(x + pad, ref ly, labelW, valueW, line, "TARGET", _target);
    }

    static void Row(float x, ref float y, float labelW, float valueW, float h, string label, string value)
    {
        GUI.Label(new Rect(x, y, labelW, h), label, PixelGUI.DataDim);
        GUI.Label(new Rect(x + labelW, y, valueW, h), value, PixelGUI.Data);
        y += h;
    }
}
