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

    public static QuestHUD Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("QuestHUD");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<QuestHUD>();
        }
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
                             || FindAnyObjectByType<OnFootController>() != null;
        }
    }

    void OnGUI()
    {
        if (!_gameplayScene) return;
        List<QuestInfo> tracked = QuestManager.Tracked();
        if (tracked.Count == 0) return;

        // Iron Oval card per tracked quest: gold Silkscreen title over the VT323 progress line, and a
        // gain-green line once the quest is ready to hand in — the only state the player has to act on.
        float w = PixelGUI.Px(150f);
        float x = Screen.width - w - PixelGUI.Px(8f);
        float y = PixelGUI.Px(60f);   // below the RESULTS/position widgets
        float h = PixelGUI.Px(34f);

        foreach (var q in tracked)
        {
            bool ready = QuestManager.GetState(q) == QuestManager.State.ReadyToTurnIn;
            string progress = QuestManager.DescribeProgress(q);

            PixelGUI.Panel(new Rect(x, y, w, h), focused: ready);
            var c = PixelGUI.PanelContent(new Rect(x, y, w, h), 4f);

            GUI.Label(new Rect(c.x, c.y, c.width, PixelGUI.Px(9f)), q.title.ToUpperInvariant(),
                      PixelGUI.HeadingSmall);

            var style = PixelGUI.Row;
            var prev = style.normal.textColor;
            style.normal.textColor = ready ? PixelGUI.Confirm : PixelGUI.Text;
            GUI.Label(new Rect(c.x, c.y + PixelGUI.Px(10f), c.width, PixelGUI.Px(12f)), progress, style);
            style.normal.textColor = prev;

            y += h + PixelGUI.Px(4f);
        }
    }

}
