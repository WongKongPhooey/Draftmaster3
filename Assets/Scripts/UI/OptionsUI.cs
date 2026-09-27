using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Draftmaster.Controls;

// OPTIONS: the screen behind the title menu's last row.
//
// WHY THIS EXISTS. OPTIONS had been a NotWired placeholder since the title screen was drawn — the house
// style for "the design has this and the game hasn't yet" — so pressing it said "Not wired up yet." The
// only settings screen in the project is Assets/Menus/Settings.unity, which belongs to the previous
// iteration: legacy sliders bound to a legacy scene, not in the build, and nothing like the Iron Oval kit
// the current menus are drawn in. So this is a new screen rather than a wire to that one.
//
// WHAT IS ON IT. The player's name, in two boxes. Everywhere in the game that says who the player is —
// the garage plate, the timing tower, every {playerfirst} an NPC speaks — reads one saved career name, and
// until now the only things that ever wrote it were SINGLE RACE (which copies whichever roster driver you
// picked) and the demo's opening. There was no way to simply be called your own name.
//
// WHY THE UI IS BUILT IN CODE. Same reason as SingleRaceUI: a generated scene's serialised listeners do
// not survive a save in this project, so a menu that wires itself in Start is the durable half of the
// authored-canvas pattern. It uses the same kit helpers the authored screens do, so it matches them.
//
// The row list below is where a future setting goes — add a MenuRow and it gets navigation, drawing and
// the cursor for nothing.
public class OptionsUI : MonoBehaviour
{
    [Tooltip("Scene the BACK row and ESC return to. Must be in the build settings.")]
    public string titleSceneName = "TitleScreen";

    // What a row does when it is confirmed.
    enum RowKind
    {
        Text,   // opens for typing: ENTER edits, ENTER again saves, ESC puts it back
        Back,   // leaves the screen
    }

    class MenuRow
    {
        public string label;
        public RowKind kind;
        public int maxLength = PlayerDriverNameLimit;
        public Func<string> read;           // what is saved now, "" when nothing is
        public Action<string> write;        // called once, on save
        public string emptyHint = "";       // drawn instead of a blank value

        public TextMeshProUGUI labelText;
        public TextMeshProUGUI valueText;
        public Image cursor;
    }

    // PlayerDriver lives in the same assembly, but naming its constant in a field initialiser above reads
    // worse than one alias here.
    const int PlayerDriverNameLimit = PlayerDriver.MaxNameHalfLength;

    readonly List<MenuRow> _rows = new();
    int _index;
    int _editing = -1;      // row being typed into, -1 when just walking the menu
    string _buffer = "";
    bool _loading;

    TextMeshProUGUI _preview;
    TextMeshProUGUI _help;
    int _helpVersion = -1;
    TextMeshProUGUI _status;
    float _statusUntil;

    // The caret's flash, matched to the kit's cursor blink (0.45s on, 0.45s off).
    const float BlinkPeriod = 0.9f;

    void Start()
    {
        IronOvalScanlines.Ensure();
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        BuildRows();
        BuildChrome();
        Redraw();
    }

    // Typed characters arrive as text events rather than as keys, so that a keyboard layout the game has
    // never heard of still spells names correctly. Subscribed for the life of the screen and ignored while
    // nothing is being edited — the same shape CoopJoinPanel's code field uses.
    void OnEnable()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
    }

    void OnDisable()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
    }

    // ------------------------------------------------------------------ what is on the sheet

    void BuildRows()
    {
        _rows.Clear();

        _rows.Add(new MenuRow
        {
            label = "FIRST NAME",
            kind = RowKind.Text,
            emptyHint = "(NOT SET)",
            read = () => PlayerDriver.FirstName,
            write = value => PlayerDriver.SetCareerName(value, PlayerDriver.LastName),
        });

        _rows.Add(new MenuRow
        {
            label = "LAST NAME",
            kind = RowKind.Text,
            emptyHint = "(NOT SET)",
            read = () => PlayerDriver.LastName,
            write = value => PlayerDriver.SetCareerName(PlayerDriver.FirstName, value),
        });

        _rows.Add(new MenuRow { label = "BACK", kind = RowKind.Back });
    }

    // ------------------------------------------------------------------ input

    void Update()
    {
        if (_loading) return;

        var kb = Keyboard.current;

        // The help line names the keys of whichever device was touched last.
        if (_helpVersion != InputGlyphs.Version) { _helpVersion = InputGlyphs.Version; Redraw(); }

        if (_editing >= 0)
        {
            EditKeys(kb);
            if (_editing >= 0) PollOnScreenKeyboard();
            Redraw();       // the caret flashes, so this row is redrawn every frame while it is open
            return;
        }

        NavKeys(kb, Gamepad.current);
        if (_statusUntil > 0f && Time.unscaledTime >= _statusUntil) SetStatus("");
    }

    void NavKeys(Keyboard kb, Gamepad pad)
    {
        if (kb != null)
        {
            if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) Move(1);
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) Move(-1);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame
                || kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame) { Confirm(); return; }
            if (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) { Back(); return; }
        }

        if (pad != null)
        {
            if (pad.dpad.down.wasPressedThisFrame) Move(1);
            if (pad.dpad.up.wasPressedThisFrame) Move(-1);
            if (PadInput.Control(pad, PadBindings.Confirm).wasPressedThisFrame) { Confirm(); return; }
            if (PadInput.Control(pad, PadBindings.Back).wasPressedThisFrame) { Back(); return; }
        }
    }

    // While a name is open, the keyboard belongs to the name: no row moves, nothing loads a scene, and
    // the only three keys that mean anything are the two that close it and the one that rubs out.
    void EditKeys(Keyboard kb)
    {
        // A pad cannot type, but it must be able to get back out of a name it opened: confirm keeps what is
        // there, back puts it back.
        if (PadInput.WasPressed(PadBindings.Back)) { CancelEdit(); return; }
        if (PadInput.WasPressed(PadBindings.Confirm)) { CommitEdit(); return; }

        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame) { CancelEdit(); return; }
        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { CommitEdit(); return; }
        if (kb.backspaceKey.wasPressedThisFrame && _buffer.Length > 0)
            _buffer = _buffer.Substring(0, _buffer.Length - 1);
    }

    // A phone types through the OS keyboard, opened when the field opens. Whatever it holds is the field,
    // put through the same character rules as typed keys.
    readonly OnScreenKeyboard _osk = new();

    void PollOnScreenKeyboard()
    {
        if (!_osk.Open) return;
        string typed = _osk.Text;
        if (typed != null) _buffer = Filtered(typed, _rows[_editing].maxLength);
        switch (_osk.Poll())
        {
            case OnScreenKeyboard.Result.Done: CommitEdit(); break;
            case OnScreenKeyboard.Result.Cancelled: CancelEdit(); break;
        }
    }

    static string Filtered(string typed, int maxLength)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in typed)
        {
            if (!IsNameChar(c)) continue;
            if (sb.Length == 0 && c == ' ') continue;
            if (sb.Length >= maxLength) break;
            sb.Append(c);
        }
        return sb.ToString();
    }

    // A tap or click on a row: a name field opens for typing (or, already open, is saved); BACK leaves.
    void TapRow(int i)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (_loading || i < 0 || i >= _rows.Count) return;
        if (_editing >= 0)
        {
            bool same = _editing == i;
            if (_rows[i].kind == RowKind.Back) { CancelEdit(); Back(); return; }
            CommitEdit();
            if (same) return;
        }
        _index = i;
        Confirm();
    }

    void OnTextInput(char c)
    {
        if (_editing < 0 || _editing >= _rows.Count) return;
        if (!IsNameChar(c)) return;
        if (_buffer.Length == 0 && c == ' ') return;                 // no leading space
        if (_buffer.Length >= _rows[_editing].maxLength) return;
        _buffer += c;
    }

    // What a name may be made of. Letters of any alphabet, plus the three marks real surnames use — a
    // space (De La Rosa), a hyphen (Hulkenberg-Smith) and an apostrophe (O'Ward). Everything else,
    // including every control character the text stream carries, is dropped.
    static bool IsNameChar(char c) => char.IsLetter(c) || c == ' ' || c == '-' || c == '\'';

    void Move(int by)
    {
        if (_rows.Count == 0) return;
        _index = (_index + by + _rows.Count) % _rows.Count;
        Redraw();
    }

    void Confirm()
    {
        if (_index < 0 || _index >= _rows.Count) return;
        var row = _rows[_index];

        switch (row.kind)
        {
            case RowKind.Text:
                _editing = _index;
                _buffer = row.read != null ? row.read() : "";
                _osk.Show(_buffer, row.maxLength);
                SetStatus("");
                Redraw();
                break;

            case RowKind.Back:
                Back();
                break;
        }
    }

    void CommitEdit()
    {
        var row = _rows[_editing];
        _editing = -1;
        _osk.Hide();

        string cleaned = PlayerDriver.CleanNameHalf(_buffer);
        _buffer = "";
        row.write?.Invoke(cleaned);

        // The paddock resolves the player's name once per scene and keeps it; this one has not reloaded,
        // so tell it the answer changed rather than wait for the next scene to ask again.
        DialogueNames.Refresh();

        string full = PlayerDriver.CareerName;
        SetStatus(full.Length > 0 ? $"SAVED — {full.ToUpperInvariant()}" : "NAME CLEARED.");
        Redraw();
    }

    void CancelEdit()
    {
        _editing = -1;
        _buffer = "";
        _osk.Hide();
        SetStatus("LEFT AS IT WAS.");
        Redraw();
    }

    void Back()
    {
        if (string.IsNullOrEmpty(titleSceneName) || !Application.CanStreamedLevelBeLoaded(titleSceneName))
        {
            SetStatus($"{titleSceneName} isn't in the build settings.");
            return;
        }
        _loading = true;
        SceneManager.LoadScene(titleSceneName);
    }

    // ------------------------------------------------------------------ drawing

    void Redraw()
    {
        var theme = PixelUITheme.Instance;

        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            bool selected = i == _index;
            bool editing = i == _editing;

            if (row.cursor != null) row.cursor.gameObject.SetActive(selected && !editing);
            if (row.labelText != null)
                row.labelText.color = editing ? theme.gold : selected ? theme.text : theme.textDisabled;

            if (row.valueText == null) continue;

            if (editing)
            {
                bool on = Mathf.Repeat(Time.unscaledTime, BlinkPeriod) < BlinkPeriod * 0.5f;
                row.valueText.text = _buffer + (on ? "_" : " ");
                row.valueText.color = theme.gold;
            }
            else
            {
                string value = row.read != null ? row.read() : "";
                bool empty = string.IsNullOrEmpty(value);
                row.valueText.text = empty ? row.emptyHint : value;
                row.valueText.color = empty ? theme.plateLight : selected ? theme.text : theme.textDim;
            }
        }

        if (_preview != null)
        {
            string full = PlayerDriver.CareerName;
            _preview.text = full.Length > 0
                ? "ON THE CAR:  " + full.ToUpperInvariant()
                : "NOBODY HAS NAMED YOU YET.";
            _preview.color = full.Length > 0 ? theme.gold : theme.plateLight;
        }

        if (_help != null)
        {
            string ok = InputGlyphs.Label("ENTER", PadBindings.Confirm);
            string back = InputGlyphs.Label("ESC", PadBindings.Back);
            _help.text = InputGlyphs.UsingTouch
                ? (_editing >= 0 ? "TYPE A NAME, THEN DONE     TAP THE ROW AGAIN  SAVE" : "TAP A ROW TO CHANGE IT")
                : _editing >= 0
                ? $"TYPE A NAME     {ok}  SAVE     {back}  CANCEL"
                : InputGlyphs.UsingGamepad
                    ? $"D-PAD  MOVE     {ok}  CHANGE     {back}  BACK"
                    : "W/S OR ARROWS  MOVE     ENTER  CHANGE     ESC  BACK";
        }
    }

    void SetStatus(string message)
    {
        if (_status == null) return;
        _status.text = message ?? "";
        _statusUntil = string.IsNullOrEmpty(message) ? 0f : Time.unscaledTime + 3f;
    }

    // ------------------------------------------------------------------ chrome

    void BuildChrome()
    {
        var theme = PixelUITheme.Instance;

        var canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);   // the project's pixel canvas grid
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        var root = (RectTransform)canvas.transform;

        var backdrop = new GameObject("Backdrop", typeof(Image)).GetComponent<Image>();
        backdrop.transform.SetParent(root, false);
        backdrop.color = theme.ink;
        Stretch((RectTransform)backdrop.transform);

        var title = IronOvalUI.Label(root, "Title", "OPTIONS", IronOvalUI.Role.Header, theme.text);
        Place((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(24f, -20f),
              new Vector2(400f, 28f), TextAlignmentOptions.TopLeft);

        var section = IronOvalUI.Label(root, "Section", "DRIVER", IronOvalUI.Role.HeaderSmall, theme.gold);
        section.characterSpacing = 4f;
        Place((RectTransform)section.transform, new Vector2(0f, 1f), new Vector2(24f, -56f),
              new Vector2(560f, 12f), TextAlignmentOptions.TopLeft);

        var blurb = IronOvalUI.Label(root, "Blurb",
                                     "WHAT THE SPOTTER, THE CREW AND THE TIMING TOWER CALL YOU.",
                                     IronOvalUI.Role.Body, theme.plateLight);
        Place((RectTransform)blurb.transform, new Vector2(0f, 1f), new Vector2(24f, -74f),
              new Vector2(560f, 18f), TextAlignmentOptions.TopLeft);

        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            // BACK sits a row clear of the fields — it is leaving the screen, not another thing to set.
            float y = -108f - i * 24f - (row.kind == RowKind.Back ? 16f : 0f);

            // The row's hit area, under its text, so a tap or click anywhere along it works (a phone has no
            // Enter). The text on top is told not to catch the ray.
            var hit = new GameObject($"Hit_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
            hit.transform.SetParent(root, false);
            hit.GetComponent<Image>().color = Color.clear;
            Place((RectTransform)hit.transform, new Vector2(0f, 1f), new Vector2(20f, y + 2f),
                  new Vector2(600f, 22f), TextAlignmentOptions.TopLeft);
            var hitButton = hit.GetComponent<Button>();
            hitButton.transition = Selectable.Transition.None;
            hitButton.navigation = new Navigation { mode = Navigation.Mode.None };
            int index = i;
            hitButton.onClick.AddListener(() => TapRow(index));

            row.cursor = IronOvalUI.Cursor(root, $"Cursor_{i}");
            Place((RectTransform)row.cursor.transform, new Vector2(0f, 1f), new Vector2(26f, y - 4f),
                  new Vector2(10f, 12f), TextAlignmentOptions.Left);

            row.labelText = IronOvalUI.Label(root, $"Row_{i}", row.label, IronOvalUI.Role.Body,
                                             theme.textDisabled);
            Place((RectTransform)row.labelText.transform, new Vector2(0f, 1f), new Vector2(44f, y),
                  new Vector2(220f, 20f), TextAlignmentOptions.TopLeft);
            row.cursor.raycastTarget = false;
            row.labelText.raycastTarget = false;

            if (row.kind == RowKind.Back) continue;

            row.valueText = IronOvalUI.Label(root, $"Value_{i}", "", IronOvalUI.Role.Body, theme.textDim);
            Place((RectTransform)row.valueText.transform, new Vector2(0f, 1f), new Vector2(270f, y),
                  new Vector2(346f, 20f), TextAlignmentOptions.TopLeft);
            row.valueText.raycastTarget = false;
        }

        _preview = IronOvalUI.Label(root, "Preview", "", IronOvalUI.Role.Body, theme.gold);
        Place((RectTransform)_preview.transform, new Vector2(0f, 0f), new Vector2(24f, 84f),
              new Vector2(560f, 20f), TextAlignmentOptions.BottomLeft);

        _status = IronOvalUI.Label(root, "Status", "", IronOvalUI.Role.Body, theme.text);
        Place((RectTransform)_status.transform, new Vector2(0f, 0f), new Vector2(24f, 44f),
              new Vector2(560f, 18f), TextAlignmentOptions.BottomLeft);

        _help = IronOvalUI.Label(root, "Help", "", IronOvalUI.Role.Body, theme.plateLight);
        Place((RectTransform)_help.transform, new Vector2(0f, 0f), new Vector2(24f, 22f),
              new Vector2(560f, 18f), TextAlignmentOptions.BottomLeft);
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // Anchor to a corner and offset in design pixels, so the layout holds at any window size.
    static void Place(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size,
                      TextAlignmentOptions alignment)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0f, anchor.y);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;

        var text = rect.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
        }
    }
}
