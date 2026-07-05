using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Minimal tracked-quest readout: top-right list of Active/ReadyToTurnIn quests, title + progress line.
// OnGUI, same style family as RaceDirector's HUD. Lives across scene loads; draws only in gameplay
// scenes (an on-foot player or a race in progress), never in menus.
public class QuestHUD : MonoBehaviour
{
    public static QuestHUD Instance { get; private set; }

    GUIStyle _title, _line;
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

        EnsureStyles();
        float w = 250f;
        float x = Screen.width - w - 12f;
        float y = 120f; // below the RESULTS/position widgets

        foreach (var q in tracked)
        {
            bool ready = QuestManager.GetState(q) == QuestManager.State.ReadyToTurnIn;
            string progress = QuestManager.DescribeProgress(q);
            float h = 42f;

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            _title.normal.textColor = new Color(1f, 0.85f, 0.3f);
            GUI.Label(new Rect(x + 8f, y + 3f, w - 16f, 18f), q.title, _title);
            _line.normal.textColor = ready ? new Color(0.4f, 1f, 0.5f) : Color.white;
            GUI.Label(new Rect(x + 8f, y + 21f, w - 16f, 18f), progress, _line);

            y += h + 6f;
        }
    }

    void EnsureStyles()
    {
        if (_title != null) return;
        _title = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        _line = new GUIStyle(GUI.skin.label) { fontSize = 12 };
    }
}
