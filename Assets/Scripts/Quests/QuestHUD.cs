using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Minimal tracked-quest readout: top-right list of Active/ReadyToTurnIn quests, title + progress line.
// Drawn with the Iron Oval kit (PixelGUI), same as the rest of the in-race furniture. Lives across scene
// loads; draws only in gameplay scenes (an on-foot player or a race in progress), never in menus.
public class QuestHUD : MonoBehaviour
{
    public static QuestHUD Instance { get; private set; }

    bool _gameplayScene;
    float _nextSceneCheck;

    // One line of the readout, worked out on a timer rather than inside OnGUI.
    //
    // OnGUI runs once per IMGUI event: a Layout and a Repaint every frame at rest, and one more for
    // every key and mouse event on top, so it fires several times a frame while the player is walking.
    // Rebuilding the tracked list, re-reading each quest's state out of PlayerPrefs and re-formatting
    // its progress line on every one of those was pure garbage. A quest readout does not need to be
    // frame-fresh, so it is refreshed four times a second and OnGUI only draws what is here.
    struct Row
    {
        public string title;
        public string progress;
        public bool ready;
    }

    const float RowRefreshSeconds = 0.25f;
    readonly List<Row> _rows = new();
    float _nextRowRefresh;

    public static QuestHUD Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("QuestHUD");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<QuestHUD>();
        }
        Instance._nextRowRefresh = 0f;   // a quest just changed hands — redraw on the next tick
        return Instance;
    }

    // Revive the HUD on load when a save already has tracked quests.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (QuestManager.Tracked().Count > 0) Ensure();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m) => _nextSceneCheck = 0f;

    void Update()
    {
        // Cheap periodic probe instead of per-frame FindAnyObjectByType.
        if (Time.unscaledTime >= _nextSceneCheck)
        {
            _nextSceneCheck = Time.unscaledTime + 2f;
            _gameplayScene = RacePositionTracker.Instance != null
                             || OnFootController.Current != null;
        }

        if (!_gameplayScene) return;
        if (Time.unscaledTime < _nextRowRefresh) return;
        _nextRowRefresh = Time.unscaledTime + RowRefreshSeconds;
        RebuildRows();
    }

    // Walks the quest definitions directly rather than through QuestManager.Tracked(), which builds a
    // fresh list every call — there is nothing to hand out here, only rows to fill.
    void RebuildRows()
    {
        _rows.Clear();
        foreach (var q in QuestManager.All)
        {
            if (q == null) continue;
            var state = QuestManager.GetState(q);
            if (state != QuestManager.State.Active && state != QuestManager.State.ReadyToTurnIn) continue;
            _rows.Add(new Row
            {
                title = string.IsNullOrEmpty(q.title) ? "" : q.title.ToUpperInvariant(),
                progress = QuestManager.DescribeProgress(q),
                ready = state == QuestManager.State.ReadyToTurnIn,
            });
        }
    }

    void OnGUI()
    {
        if (!_gameplayScene || _rows.Count == 0) return;

        // Iron Oval card per tracked quest: gold Silkscreen title over the VT323 progress line, and a
        // gain-green line once the quest is ready to hand in — the only state the player has to act on.
        float w = PixelGUI.Px(150f);
        float x = Screen.width - w - PixelGUI.Px(8f);
        float y = PixelGUI.Px(60f);   // below the RESULTS/position widgets
        float h = PixelGUI.Px(34f);

        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];

            PixelGUI.Panel(new Rect(x, y, w, h), focused: row.ready);
            var c = PixelGUI.PanelContent(new Rect(x, y, w, h), 4f);

            GUI.Label(new Rect(c.x, c.y, c.width, PixelGUI.Px(9f)), row.title, PixelGUI.HeadingSmall);

            var style = PixelGUI.Row;
            var prev = style.normal.textColor;
            style.normal.textColor = row.ready ? PixelGUI.Confirm : PixelGUI.Text;
            GUI.Label(new Rect(c.x, c.y + PixelGUI.Px(10f), c.width, PixelGUI.Px(12f)), row.progress, style);
            style.normal.textColor = prev;

            y += h + PixelGUI.Px(4f);
        }
    }

}
