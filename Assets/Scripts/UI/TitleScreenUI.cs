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
        // APPEND new commands, never insert: the scene file stores a row's command as the number above it,
        // so inserting one moves every row in TitleScreen.unity onto the wrong command.
        RestartDemo,  // wipe the save, then open a fresh career — the demo build's "start again"
    }

    // Which build a row belongs to. The demo menu is a different menu, not the same one with things
    // greyed out: no fresh-season row, no factory, and a RESTART DEMO row the full game has no use for.
    public enum Build
    {
        Both,       // every build
        DemoOnly,   // only when DemoMode.IsDemo
        FullOnly,   // only in the full release
    }

    [Serializable]
    public class Row
    {
        public string label = "NEW SEASON";
        public Command command = Command.NotWired;
        [Tooltip("Scene loaded by the LoadScene command. Must be in the build settings.")]
        public string sceneName = "";
        [Tooltip("Which build draws this row. A hidden row is switched off and the column closes up over it.")]
        public Build appearsIn = Build.Both;

        [Header("Wired by the builder")]
        public TextMeshProUGUI labelText;
        [Tooltip("The kit's gold arrow. Shown on the selected row only; it blinks itself (IronOvalBlink).")]
        public GameObject cursor;
        public RectTransform rect;

        [NonSerialized] public bool available;
        [NonSerialized] public bool shown;      // in THIS build — see appearsIn
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

    // RESTART DEMO throws the save away, so it asks twice: the first press arms it and says what it does,
    // and the arming expires with the status line that announced it.
    float _restartArmedUntil;
    const float StatusSeconds = 2.5f;

    // Row indices sorted top-to-bottom by where the row actually sits on screen. The list is the wiring
    // and the column is the layout, and the two drift apart the moment a row is dragged up the menu in
    // the scene without the list following it — so DOWN means the next row down the screen, not the next
    // element of the list.
    int[] _order;

    void Start()
    {
        // Nothing else in a menu scene installs these, and the design puts the TV line over everything.
        IronOvalScanlines.Ensure();

        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        RebuildOrder();     // decides which rows this build draws, and in which order

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.rect != null) row.rect.gameObject.SetActive(row.shown);
            row.available = row.shown && IsAvailable(row);
            if (row.shown) HookPointer(row, i);
        }

        CompactRows();
        _index = FirstShownFrom(startIndex);
        SetStatus("");
        Redraw();
    }

    bool ShownInThisBuild(Row row)
    {
        if (row == null) return false;
        switch (row.appearsIn)
        {
            case Build.DemoOnly: return DemoMode.IsDemo;
            case Build.FullOnly: return !DemoMode.IsDemo;
            default: return true;
        }
    }

    // Every row keeps the position it was given in the scene, so switching one off leaves a hole in the
    // column. Close it: re-stack the visible rows from wherever the top of the block sits, at the spacing
    // the menu already uses. Both are measured rather than assumed — the block has been moved by hand.
    void CompactRows()
    {
        var all = new List<RectTransform>();
        foreach (var row in rows)
            if (row != null && row.rect != null) all.Add(row.rect);
        if (all.Count == 0) return;

        all.Sort((a, b) => b.anchoredPosition.y.CompareTo(a.anchoredPosition.y));
        float top = all[0].anchoredPosition.y;
        float wasBottom = all[all.Count - 1].anchoredPosition.y;

        float spacing = 0f;
        for (int i = 1; i < all.Count; i++)
        {
            float gap = all[i - 1].anchoredPosition.y - all[i].anchoredPosition.y;
            if (gap > 0.01f && (spacing <= 0f || gap < spacing)) spacing = gap;
        }
        if (spacing <= 0f) spacing = 26f;      // the builder's row pitch

        var shown = new List<RectTransform>();
        foreach (var row in rows)
            if (row != null && row.shown && row.rect != null) shown.Add(row.rect);
        if (shown.Count == 0) return;

        shown.Sort((a, b) => b.anchoredPosition.y.CompareTo(a.anchoredPosition.y));
        for (int i = 0; i < shown.Count; i++)
        {
            var at = shown[i].anchoredPosition;
            shown[i].anchoredPosition = new Vector2(at.x, top - i * spacing);
        }

        // The status line is authored under the menu block, in a different parent — but the move is a pure
        // vertical shift, so the same delta lands it under the shortened column instead of floating in the
        // gap the hidden rows left behind.
        if (statusLabel != null)
        {
            float nowBottom = top - (shown.Count - 1) * spacing;
            statusLabel.rectTransform.anchoredPosition += Vector2.up * (nowBottom - wasBottom);
        }
    }

    // The opening selection, skipped past any row this build doesn't draw.
    int FirstShownFrom(int index)
    {
        if (rows.Count == 0) return 0;
        index = Mathf.Clamp(index, 0, rows.Count - 1);
        if (rows[index] != null && rows[index].shown) return index;
        return _order != null && _order.Length > 0 ? _order[0] : index;
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
        if (_order == null || _order.Length == 0) RebuildOrder();
        if (_order.Length == 0) return;

        int at = System.Array.IndexOf(_order, _index);
        if (at < 0) at = 0;
        _index = _order[(at + by + _order.Length) % _order.Length];
        _restartArmedUntil = 0f;    // moving off a primed RESTART DEMO disarms it
        Redraw();
    }

    // Sort the rows the way the eye reads them: highest on screen first, and only the ones this build
    // draws — a hidden row is not somewhere the cursor can land. Rows with no rect sort to the bottom,
    // which is the only sensible place for a row that isn't drawn anywhere; ties keep list order.
    //
    // This is also where `shown` is decided, rather than in Start(), so that the walk order is a function
    // of the rows alone — the wiring test calls it on a component that has never run.
    void RebuildOrder()
    {
        var order = new List<int>();
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] == null) continue;
            rows[i].shown = ShownInThisBuild(rows[i]);
            if (rows[i].shown) order.Add(i);
        }

        order.Sort((a, b) =>
        {
            int cmp = ScreenKey(rows[a]).CompareTo(ScreenKey(rows[b]));
            return cmp != 0 ? cmp : a.CompareTo(b);
        });
        _order = order.ToArray();
    }

    static float ScreenKey(Row row)
        => row.rect != null ? -row.rect.position.y : float.PositiveInfinity;

    void Select(int index)
    {
        if (index == _index || index < 0 || index >= rows.Count) return;
        if (!rows[index].shown) return;
        _index = index;
        _restartArmedUntil = 0f;
        Redraw();
    }

    void Confirm()
    {
        if (_index < 0 || _index >= rows.Count) return;
        var row = rows[_index];
        if (!row.shown) return;

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
                // A new weekend opens in the paddock, not in the car — the sheet decides when the track
                // goes live.
                RaceWeekend.SessionLive = false;
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
                // No weekend around an exhibition: the session is live the moment the scene loads.
                RaceWeekend.SessionLive = true;
                Load(raceSceneName);
                break;

            case Command.LoadScene:
                Load(row.sceneName);
                break;

            // The demo's start-again row: the same fresh career NEW SEASON opens, on a save wiped back to
            // the first day — no money, no stats, no championship, no quests, nobody met.
            case Command.RestartDemo:
                if (Time.unscaledTime > _restartArmedUntil)
                {
                    _restartArmedUntil = Time.unscaledTime + StatusSeconds;
                    SetStatus("Erases all progress. Press again to restart.");
                    return;
                }
                _restartArmedUntil = 0f;

                string restartAt = OpeningTrack();
                if (string.IsNullOrEmpty(restartAt)) { SetStatus("No track has a layout yet."); return; }

                CareerReset.ClearAll();
                // After the wipe, not before: StartWeekendAt writes the selection the wipe would have eaten.
                TrackSelection.StartWeekendAt(restartAt);
                RaceWeekend.SessionLive = false;
                Load(raceSceneName);
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
            if (!row.shown) continue;
            if (row.labelText != null)
                row.labelText.color = !row.available ? dead : (i == _index ? on : off);
            if (row.cursor != null) row.cursor.SetActive(i == _index);
        }
    }

    void SetStatus(string text)
    {
        if (statusLabel == null) return;
        statusLabel.text = text ?? "";
        _statusUntil = string.IsNullOrEmpty(text) ? 0f : Time.unscaledTime + StatusSeconds;
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
