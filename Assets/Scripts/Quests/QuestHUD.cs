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
        if (!GameSession.CareerActive) return;   // career quests have no business in a single race
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
        if (PixelGUI.Handheld) { DrawHandheld(); return; }

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

    GUIStyle _handheldTitle, _handheldProgress;
    int _handheldStylesAt;

    // The same cards on a phone, set in the objective strip's two sizes (Draftmaster.Controls.HandheldType):
    // the title in the display face at two cells — the strip's title — and the progress in the data face at
    // one, its detail line. The desktop card's title is the kit's smallest label, which on a handset is a
    // millimetre of capitals, so the tracker read as the one tiny thing on the screen.
    //
    // Laid out off the measured text rather than fixed rows, since the type no longer fits the desktop card:
    // a card is as tall as its lines and as wide as its longest, within a share of the screen, and a long
    // title or progress line wraps inside that rather than running out of the plate.
    void DrawHandheld()
    {
        if (_handheldTitle == null || _handheldStylesAt != PixelGUI.Scale)
        {
            _handheldStylesAt = PixelGUI.Scale;
            _handheldTitle = new GUIStyle(PixelGUI.Heading)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                clipping = TextClipping.Overflow,
            };
            _handheldProgress = new GUIStyle(PixelGUI.Data)
            {
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                clipping = TextClipping.Overflow,
            };
        }

        float inset = PixelGUI.Px(8f);   // what PanelContent(box, 4f) takes off each side
        float gap = PixelGUI.Px(2f);
        float minW = PixelGUI.Px(150f);
        float maxW = Mathf.Max(minW, Mathf.Floor(Screen.width * 0.4f));
        float x0 = Screen.width - PixelGUI.Px(8f);
        float y = PixelGUI.Px(60f);   // below the RESULTS/position widgets

        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var title = new GUIContent(row.title);
            var progress = new GUIContent(row.progress);

            float textW = Mathf.Max(_handheldTitle.CalcSize(title).x, _handheldProgress.CalcSize(progress).x);
            float w = PixelGUI.SnapUp(Mathf.Clamp(textW + inset * 2f, minW, maxW));
            float colW = w - inset * 2f;
            float titleH = Mathf.Ceil(_handheldTitle.CalcHeight(title, colW));
            float progressH = Mathf.Ceil(_handheldProgress.CalcHeight(progress, colW));
            float h = PixelGUI.SnapUp(titleH + gap + progressH + inset * 2f);

            var box = new Rect(x0 - w, y, w, h);
            PixelGUI.Panel(box, focused: row.ready);
            var c = PixelGUI.PanelContent(box, 4f);

            GUI.Label(new Rect(c.x, c.y, c.width, titleH), title, _handheldTitle);
            _handheldProgress.normal.textColor = row.ready ? PixelGUI.Confirm : PixelGUI.Text;
            GUI.Label(new Rect(c.x, c.y + titleH + gap, c.width, progressH), progress, _handheldProgress);

            y += h + PixelGUI.Px(4f);
        }
    }

}
