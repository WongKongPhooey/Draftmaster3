using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The title screen, as drawn in the Iron Oval design file: wordmark and menu on a dark scrim over the
// title art, a blinking gold cursor on the selected row, scanlines over the lot.
//
// This is the binder half of the authored-canvas pattern — the layout is built once by
// Draftmaster > Art > Build Title Screen Scene and lives in the scene; this drives selection, the cursor
// blink and where each row goes. Nothing here creates UI.
//
// Rows whose destination isn't in the build settings draw disabled rather than silently doing nothing,
// so a half-wired menu reads as half-wired instead of broken.
public class TitleScreenUI : MonoBehaviour
{
    public enum Command
    {
        NewSeason,    // fresh weekend at newSeasonTrackId (or the first calendar track with a layout)
        Continue,     // straight back into the selected track, session as it stands
        Exhibition,   // one race, no practice or qualifying
        LoadScene,    // whatever is in sceneName
        NotWired,     // drawn disabled: a row the design has and the game hasn't yet
    }

    [Serializable]
    public class Row
    {
        public string label = "NEW SEASON";
        public Command command = Command.NotWired;
        [Tooltip("Scene loaded by the LoadScene command. Must be in the build settings.")]
        public string sceneName = "";

        [Header("Wired by the builder")]
        public TextMeshProUGUI labelText;
        [Tooltip("The kit's gold arrow. Shown on the selected row only; it blinks itself (IronOvalBlink).")]
        public GameObject cursor;
        public RectTransform rect;

        [NonSerialized] public bool available;
    }

    [Header("Menu")]
    public List<Row> rows = new();
    [Tooltip("Row selected when the screen opens.")]
    public int startIndex = 0;

    [Header("Wired by the builder")]
    public TextMeshProUGUI statusLabel;

    [Header("Feel")]
    [Tooltip("Race scene loaded by the season / continue / exhibition rows.")]
    public string raceSceneName = "RaceScene";
    [Tooltip("Track the NEW SEASON row opens the calendar at. Empty = the first calendar track that has " +
             "geometry, which is wherever the season's schedule happens to start.")]
    public string newSeasonTrackId = TrackCatalog.DefaultTrackId;

    int _index;
    float _statusUntil;
    bool _loading;

    void Start()
    {
        // Nothing else in a menu scene installs these, and the design puts the TV line over everything.
        IronOvalScanlines.Ensure();

        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            row.available = IsAvailable(row);
            HookPointer(row, i);
        }

        _index = Mathf.Clamp(startIndex, 0, Mathf.Max(0, rows.Count - 1));
        SetStatus("");
        Redraw();
    }

    void Update()
    {
        if (_loading) return;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Step(1);
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Step(-1);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) Confirm();
        }

        var pad = Gamepad.current;
        if (pad != null)
        {
            if (pad.dpad.down.wasPressedThisFrame) Step(1);
            if (pad.dpad.up.wasPressedThisFrame) Step(-1);
            if (pad.buttonSouth.wasPressedThisFrame) Confirm();
        }

        if (_statusUntil > 0f && Time.unscaledTime >= _statusUntil) SetStatus("");
    }

    // ------------------------------------------------------------------ selection

    void Step(int by)
    {
        if (rows.Count == 0) return;
        _index = (_index + by + rows.Count) % rows.Count;
        Redraw();
    }

    void Select(int index)
    {
        if (index == _index || index < 0 || index >= rows.Count) return;
        _index = index;
        Redraw();
    }

    void Confirm()
    {
        if (_index < 0 || _index >= rows.Count) return;
        var row = rows[_index];

        if (!row.available)
        {
            SetStatus(row.command == Command.LoadScene && !string.IsNullOrEmpty(row.sceneName)
                ? $"{row.sceneName} isn't in the build settings yet."
                : "Not wired up yet.");
            return;
        }

        switch (row.command)
        {
            case Command.NewSeason:
                string opener = OpeningTrack();
                if (string.IsNullOrEmpty(opener)) { SetStatus("No track has a layout yet."); return; }
                TrackSelection.StartWeekendAt(opener);
                Load(raceSceneName);
                break;

            case Command.Continue:
                if (!EnsureRaceableTrack()) return;
                Load(raceSceneName);
                break;

            case Command.Exhibition:
                // One race: skip the practice/qualifying half of the weekend.
                if (!EnsureRaceableTrack()) return;
                RaceWeekend.Current = RaceWeekend.Session.Race;
                Load(raceSceneName);
                break;

            case Command.LoadScene:
                Load(row.sceneName);
                break;

            default:
                SetStatus("Not wired up yet.");
                break;
        }
    }

    void Load(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SetStatus($"{sceneName} isn't in the build settings yet.");
            return;
        }
        _loading = true;
        SceneManager.LoadScene(sceneName);
    }

    bool IsAvailable(Row row)
    {
        switch (row.command)
        {
            case Command.NotWired: return false;
            case Command.LoadScene: return !string.IsNullOrEmpty(row.sceneName)
                                        && Application.CanStreamedLevelBeLoaded(row.sceneName);
            default: return Application.CanStreamedLevelBeLoaded(raceSceneName);
        }
    }

    // The saved selection is only as good as whoever set it last — the travel map moves it to any circuit
    // on the map, and most of the calendar is still catalogue-only. The race scene builds its road from
    // that id, so both rows that resume an existing selection come through here rather than load a scene
    // with no track in it. False = nothing anywhere has a layout, and the status line says so.
    bool EnsureRaceableTrack()
    {
        if (TrackCatalog.HasGeometry(TrackSelection.CurrentId)) return true;

        string resume = OpeningTrack();
        if (string.IsNullOrEmpty(resume)) { SetStatus("No track has a layout yet."); return false; }
        TrackSelection.Select(resume);
        return true;
    }

    // Where a season starts: the configured track when it has a layout to race on, otherwise the first
    // one on the calendar that does. Both rows fall back through here, so neither can load the race scene
    // pointed at a track that is still just a row in the catalogue.
    string OpeningTrack()
    {
        if (!string.IsNullOrEmpty(newSeasonTrackId) && TrackCatalog.HasGeometry(newSeasonTrackId))
            return newSeasonTrackId;
        return FirstRaceableTrack();
    }

    // First calendar track with geometry — the fallback when the configured one has no layout yet.
    static string FirstRaceableTrack()
    {
        foreach (var row in TrackCatalog.All)
            if (row != null && TrackCatalog.HasGeometry(row.Name)) return row.Name;
        return null;
    }

    // ------------------------------------------------------------------ drawing

    void Redraw()
    {
        var theme = PixelUITheme.Instance;
        Color on = theme != null ? theme.text : Color.white;
        Color off = theme != null ? theme.textDisabled : new Color(0.49f, 0.53f, 0.64f);
        Color dead = theme != null ? theme.plateLight : new Color(0.17f, 0.19f, 0.27f);

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.labelText != null)
                row.labelText.color = !row.available ? dead : (i == _index ? on : off);
            if (row.cursor != null) row.cursor.SetActive(i == _index);
        }
    }

    void SetStatus(string text)
    {
        if (statusLabel == null) return;
        statusLabel.text = text ?? "";
        _statusUntil = string.IsNullOrEmpty(text) ? 0f : Time.unscaledTime + 2.5f;
    }

    // Mouse: hovering a row selects it, clicking confirms — the same two states the keyboard drives,
    // so there is no separate pointer look to keep in sync.
    void HookPointer(Row row, int index)
    {
        if (row.rect == null) return;

        var trigger = row.rect.GetComponent<EventTrigger>();
        if (trigger == null) trigger = row.rect.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => Select(index));
        trigger.triggers.Add(enter);

        var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        click.callback.AddListener(_ => { Select(index); Confirm(); });
        trigger.triggers.Add(click);

        // The row needs something raycastable under it or the pointer never lands on it.
        var hit = row.rect.GetComponent<Image>();
        if (hit == null) hit = row.rect.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
    }
}
