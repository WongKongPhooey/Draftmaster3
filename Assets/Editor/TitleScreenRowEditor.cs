using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;

// Adds a row to the title menu WITHOUT rebuilding the scene.
//
// WHY THIS EXISTS. IronOvalTitleBuilder regenerates TitleScreen.unity from scratch, which means every
// hand edit made in the scene since it last ran is destroyed — and that has already happened once: a
// rebuild to add one menu row reverted the wordmark from DRAFTMASTER 3 back to the builder's IRON OVAL
// placeholder, dropped a prefab instance and a material, and moved the layout. The scene is the source of
// truth for anything hand-authored; the builder only knows what its code says.
//
// So adding a row is a surgical edit: clone an existing row so the new one inherits its font, colour,
// cursor and styling exactly, insert it into the menu list, and nudge the rows below it down by the
// spacing already in use. Nothing else in the scene is touched.
//
// Idempotent — running it twice does not add two rows.
public static class TitleScreenRowEditor
{
    const string ScenePath = "Assets/Scenes/TitleScreen.unity";

    [MenuItem("Draftmaster/UI/Add SINGLE RACE Row To Title Screen")]
    public static void AddSingleRaceRow()
    {
        Debug.Log(AddRow("SINGLE RACE", TitleScreenUI.Command.LoadScene, "SingleRace",
                         after: TitleScreenUI.Command.Continue));
    }

    // Wire the MULTIPLAYER row the design already has. It sat as a NotWired placeholder — the house style
    // for "the design has this and the game doesn't yet" — so this WIRES it rather than adding a second
    // row: AddRow matches on the label and rewires in place, moving nothing.
    //
    // Joining loads no scene of its own (the host pulls the guest into whichever one they are stood in),
    // so the row carries no scene name and opens CoopJoinPanel instead. The other half, hosting, is
    // deliberately NOT on this menu: opening your career to a friend only means anything from inside a
    // career, so it sits on the race-scene pause menu.
    [MenuItem("Draftmaster/UI/Wire MULTIPLAYER Row To Co-op Join")]
    public static void WireMultiplayerRow()
    {
        Debug.Log(AddRow("MULTIPLAYER", TitleScreenUI.Command.JoinCoop, "",
                         after: TitleScreenUI.Command.LoadScene));
    }

    // MASTER DRAFT sits between SINGLE RACE and MULTIPLAYER. It went in as a NotWired placeholder before the
    // mode existed; now it loads the mode's menu scene (Assets/Scenes/MasterDraft.unity). AddRow matches the
    // placeholder by label and rewires it in place, moving nothing — running this on a scene that has no
    // MASTER DRAFT row yet adds one after SINGLE RACE.
    //
    // Placed by label rather than by command: SINGLE RACE and OPTIONS are both LoadScene rows, so "after
    // the first LoadScene" depends on list order, which need not match the column.
    [MenuItem("Draftmaster/UI/Add MASTER DRAFT Row To Title Screen")]
    public static void AddMasterDraftRow()
    {
        Debug.Log(AddRow("MASTER DRAFT", TitleScreenUI.Command.LoadScene, "MasterDraft", afterLabel: "SINGLE RACE"));
    }

    // CONTINUE -> CAREER. The row resumes the career you are in the middle of, and "continue" describes the
    // button rather than the thing behind it — every other row on the menu names a mode.
    //
    // Surgical, like AddRow: the row keeps its command, its rect, its cursor and its "Chapter" subtitle
    // child, and nothing else in the scene is touched. Idempotent.
    [MenuItem("Draftmaster/UI/Rename CONTINUE Row To CAREER")]
    public static void RenameContinueRow()
    {
        Debug.Log(RenameRow("CONTINUE", "CAREER"));
    }

    // NEW SEASON is gone: CAREER already opens the career, and two rows for one door is the kind of thing
    // that gets clicked wrong. Removing it also clears a live trap — Row_NEW_SEASON was drawing the word
    // "MULTIPLAYER" while still running NewSeason, so the demo menu showed that word twice and the higher
    // one started a career instead of opening the join box.
    [MenuItem("Draftmaster/UI/Remove NEW SEASON Row From Title Screen")]
    public static void RemoveNewSeasonRow()
    {
        Debug.Log(RemoveRow("NEW SEASON"));
    }

    // The demo's menu is the menu now. It was the shorter of two columns — the same rows with the
    // full-release ones switched off by a per-row flag and RESTART DEMO switched on — which meant the
    // editor drew a menu nobody would ever see: every row at once, including the two the demo hid. The
    // flag is gone from TitleScreenUI, so the two rows have to come out of the scene for the column in
    // the editor to be the column in the game.
    //
    // EXHIBITION goes because SINGLE RACE does the same thing with a track and a driver to pick, and
    // TEAM FACTORY because the factory is reached from the laptop in the RV, not from the title screen.
    //
    // Idempotent: RemoveRow says so and changes nothing when the row has already gone.
    [MenuItem("Draftmaster/UI/Make The Demo Menu The Only Menu")]
    public static void MakeDemoMenuPermanent()
    {
        Debug.Log(RemoveRow("EXHIBITION"));
        Debug.Log(RemoveRow("TEAM FACTORY"));
    }

    // The title menu is the one place the game sets its labels in capitals (Docs/Type-Scale.md, "Letter
    // case"). The capitals are written into the text itself rather than put on by a TMP style, so a row can
    // go back to mixed case by editing its text. Each goes through RenameRow, so label, drawn text and
    // Row_<Label> object name move together, and nothing else in the hand-edited scene is touched.
    // Idempotent.
    [MenuItem("Draftmaster/UI/All Caps The Title Menu Rows")]
    public static void AllCapsRows()
    {
        foreach (var (from, to) in new[]
                 {
                     ("Career", "CAREER"), ("Single Race", "SINGLE RACE"), ("Multiplayer", "MULTIPLAYER"),
                     ("Options", "OPTIONS"), ("Restart Demo", "RESTART DEMO"),
                 })
            Debug.Log(RenameRow(from, to));
    }

    // The menu rows and the copyright footer under them trade faces. The rows are written in capitals now,
    // so they go back to Silkscreen — the face the footer was in, at its size and tracking — and the footer
    // takes the rows' Fixedsys with its text in mixed case. Silkscreen has no lower case: a row that goes back
    // to mixed case has to go back to Fixedsys with it. Only those six texts change. Idempotent.
    [MenuItem("Draftmaster/UI/Title Menu In Silkscreen, Footer In Fixedsys")]
    public static void SwapRowAndFooterFaces()
    {
        var theme = PixelUITheme.Instance;
        var display = theme != null ? theme.display : null;
        var data = theme != null ? theme.data : null;
        if (display == null || data == null) { Debug.Log("Theme is missing a face — title screen left alone."); return; }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var ui = Object.FindObjectsByType<TitleScreenUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                       .FirstOrDefault();
        if (ui == null) { Debug.Log($"No TitleScreenUI in {ScenePath}."); return; }

        var footer = ui.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == "Footer");

        // The footer's own look, read before it is changed, is what the rows take.
        float size = footer != null && footer.font == display ? footer.fontSize : 16f;
        float spacing = footer != null && footer.font == display ? footer.characterSpacing : 2f;

        int rows = 0;
        foreach (var row in ui.rows)
        {
            var text = row != null ? row.labelText : null;
            if (text == null) continue;
            SetFace(text, display, size, spacing);
            rows++;
        }

        string footerNote = "no Footer text found";
        if (footer != null)
        {
            SetFace(footer, data, 16f, 0f);
            footer.text = "(C) Phooey 2026";
            footerNote = $"footer \"{footer.text}\" in {data.name}";
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"Title screen: {rows} menu row(s) in {display.name} {size:0}/{spacing:0.#}; {footerNote}.");
    }

    // The menu rows go into monogram Bold, the face everything else readable is in (2026-11-11, from the
    // Draftmaster3_UI_Unity design handoff: menu items in monogram Bold, no tracking, capitals, one pixel
    // step above its native 16px). The step is PixelStepText's to work out from the canvas scale — 21.33 at
    // 1080p, 20 at 1440p — because no single point size is crisp at both.
    //
    // At step +1 the caps are 7 font px = 9.33 kit px (at 3x) under a 14.67 kit px ascent, so the label
    // drops 1px inside its row to centre the caps on the row's cursor (8px tall at y -7, centre -11; caps
    // centre lands at -11 at 1080p, -10.4 at 1440p). The copyright footer goes to monogram's native 16:
    // it had been at 11, which is off monogram's grid. Absolute, so idempotent.
    [MenuItem("Draftmaster/UI/Title Menu In monogram")]
    public static void RowsInMonogram()
    {
        var face = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Monogram Pixel.asset");
        if (face == null) { Debug.Log("Monogram Pixel font asset missing — title screen left alone."); return; }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var ui = Object.FindObjectsByType<TitleScreenUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                       .FirstOrDefault();
        if (ui == null) { Debug.Log($"No TitleScreenUI in {ScenePath}."); return; }

        const float LabelDrop = 1f;
        const int MenuStep = 1;
        int rows = 0;
        foreach (var row in ui.rows)
        {
            var text = row != null ? row.labelText : null;
            if (text == null) continue;
            SetFace(text, face, 16f, 0f);
            var rt = text.rectTransform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, -LabelDrop);
            EditorUtility.SetDirty(rt);

            var stepper = text.GetComponent<PixelStepText>();
            if (stepper == null) stepper = text.gameObject.AddComponent<PixelStepText>();
            stepper.nativePx = 16;
            stepper.step = MenuStep;
            stepper.Apply();
            EditorUtility.SetDirty(stepper);
            rows++;
        }

        var footer = ui.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == "Footer");
        if (footer != null) SetFace(footer, face, 16f, 0f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"Title screen: {rows} menu row(s) in {face.name}, step +{MenuStep}; " +
                  (footer != null ? "footer at 16." : "no Footer text found."));
    }

    // The keyboard legend in the bottom-right corner — "[W] [S] MOVE   [ENTER] SELECT" — laid out to the
    // pixel from the Draftmaster3_UI_Unity design (Title_PC.json, nodes 54-68): a 183x20 plate 12px in from
    // the right and 10px up from the bottom of the 640x360 canvas, a 2px outline round it, cream key caps
    // with a 2px drop shadow, Silkscreen 8 throughout. KeyboardLegend hides it on a phone and while a pad is
    // in use. Rebuilt from scratch each run (the old one is removed first), so idempotent.
    [MenuItem("Draftmaster/UI/Title Keyboard Legend")]
    public static void AddKeyboardLegend()
    {
        var label = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/Fonts/Silkscreen Pixel.asset");
        if (label == null) { Debug.Log("Silkscreen Pixel font asset missing — title screen left alone."); return; }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var ui = Object.FindObjectsByType<TitleScreenUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                       .FirstOrDefault();
        var canvas = ui != null ? ui.GetComponentInParent<Canvas>() : null;
        if (canvas == null) { Debug.Log($"No TitleScreenUI canvas in {ScenePath}."); return; }
        var root = canvas.rootCanvas.transform;

        var old = root.Find(LegendName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        var plateCol = Hex("#0d0f16cc");
        var outlineCol = Hex("#2c3145");
        var capCol = Hex("#f4ead7");
        var shadowCol = Hex("#8b93ad");
        var capText = Hex("#12141c");
        var wordCol = Hex("#b9ae9a");

        // Plate: anchored to the canvas's bottom-right corner, everything inside measured from its top-left.
        var plate = new GameObject(LegendName, typeof(RectTransform), typeof(CanvasGroup), typeof(KeyboardLegend));
        var prt = (RectTransform)plate.transform;
        prt.SetParent(root, false);
        prt.SetAsLastSibling();
        prt.anchorMin = prt.anchorMax = new Vector2(1f, 0f);
        prt.pivot = new Vector2(1f, 0f);
        prt.sizeDelta = new Vector2(183f, 20f);
        prt.anchoredPosition = new Vector2(-12f, 10f);
        Box(prt, "Fill", 0, 0, 183, 20, plateCol);

        // 2px outline just outside the plate (the design's frame at -2,-2, 187x24). Four strips rather than a
        // filled box behind, which would show through the translucent plate.
        Box(prt, "OutlineTop", -2, -2, 187, 2, outlineCol);
        Box(prt, "OutlineBottom", -2, 20, 187, 2, outlineCol);
        Box(prt, "OutlineLeft", -2, 0, 2, 20, outlineCol);
        Box(prt, "OutlineRight", 183, 0, 2, 20, outlineCol);

        void Cap(string key, float x, float w)
        {
            Box(prt, $"Key_{key}_Shadow", x, 7, w, 10, shadowCol);
            var cap = Box(prt, $"Key_{key}", x, 5, w, 10, capCol);
            Text(cap, key, 4, 0, w - 4, 10, capText, 0f);
        }
        Cap("W", 7, 16);
        Cap("S", 29, 15);
        Text(prt, "MOVE", 50, 5, 31, 10, wordCol, 1f);
        Cap("ENTER", 91, 41);
        Text(prt, "SELECT", 138, 5, 40, 10, wordCol, 1f);

        RectTransform Box(RectTransform parent, string name, float x, float y, float w, float h, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.color = col;
            img.raycastTarget = false;
            return rt;
        }

        void Text(RectTransform parent, string s, float x, float y, float w, float h, Color col, float trackingPx)
        {
            var go = new GameObject($"Text_{s}", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = label;
            if (label.material != null) t.fontSharedMaterial = label.material;
            t.fontSize = 8f;
            // TMP tracking is in hundredths of an em: 1px at 8px is 12.5.
            t.characterSpacing = trackingPx * 100f / 8f;
            t.color = col;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.text = s;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("Title screen: keyboard legend built (bottom-right, PC only).");
    }

    const string LegendName = "KeyboardLegend";

    static void SetFace(TextMeshProUGUI text, TMP_FontAsset face, float size, float spacing)
    {
        text.font = face;
        if (face.material != null) text.fontSharedMaterial = face.material;
        text.fontSize = size;
        text.characterSpacing = spacing;
        EditorUtility.SetDirty(text);
    }

    // Bring every row's drawn text back to its label. The label is the row's identity — the wiring tests
    // match on it and RenameRow moves label, text and object name together — so when the two disagree the
    // text is the one that has drifted, and a menu that reads one thing and does another is unusable.
    [MenuItem("Draftmaster/UI/Repair Title Row Labels")]
    public static void RepairRowLabels()
    {
        Debug.Log(SyncLabels());
    }

    // Take a row off the menu: the list entry and the GameObject both, with the rows below it moved up so
    // the column stays evenly spaced. Surgical, like AddRow — nothing else in the scene is touched — and
    // idempotent.
    public static string RemoveRow(string label)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var ui = Object.FindObjectsByType<TitleScreenUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                       .FirstOrDefault();
        if (ui == null) return $"No TitleScreenUI in {ScenePath}.";

        int at = ui.rows.FindIndex(r => r != null && r.label == label);
        if (at < 0) return $"Title screen has no {label} row — nothing to remove.";

        var row = ui.rows[at];

        // Spacing the menu actually uses, measured rather than assumed.
        float spacing = 26f;
        if (ui.rows.Count >= 2 && ui.rows[0].rect != null && ui.rows[1].rect != null)
            spacing = Mathf.Abs(ui.rows[0].rect.anchoredPosition.y - ui.rows[1].rect.anchoredPosition.y);

        float removedY = row.rect != null ? row.rect.anchoredPosition.y : float.NegativeInfinity;

        // Everything BELOW the removed row comes up one place. Measured by position rather than by list
        // index: the list is the wiring and the column is the layout, and the two need not agree.
        foreach (var other in ui.rows)
        {
            if (other == null || other == row || other.rect == null) continue;
            if (other.rect.anchoredPosition.y < removedY)
                other.rect.anchoredPosition += Vector2.up * spacing;
        }
        if (ui.statusLabel != null && ui.statusLabel.rectTransform.anchoredPosition.y < removedY)
            ui.statusLabel.rectTransform.anchoredPosition += Vector2.up * spacing;

        if (row.rect != null) Undo.DestroyObjectImmediate(row.rect.gameObject);
        ui.rows.RemoveAt(at);

        // The opening selection is stored as an index into the list, so it has to follow the shuffle or it
        // silently lands on a different row — which is how a menu opens with the cursor somewhere nobody
        // put it.
        // >= rather than >: when the row being removed IS the opening selection, the cursor falls back to
        // the row above it rather than to whatever slid up into the empty slot.
        if (ui.startIndex >= at) ui.startIndex--;
        ui.startIndex = Mathf.Clamp(ui.startIndex, 0, Mathf.Max(0, ui.rows.Count - 1));

        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        return $"Title screen: removed the {label} row and its GameObject, moved the rows below it up " +
               $"{spacing:0} px, and left {ui.rows.Count} rows. Opening selection is row {ui.startIndex} " +
               $"(\"{(ui.rows.Count > 0 ? ui.rows[ui.startIndex].label : "none")}\").";
    }

    // Text follows label, for every row. Returns what moved.
    public static string SyncLabels()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var ui = Object.FindObjectsByType<TitleScreenUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                       .FirstOrDefault();
        if (ui == null) return $"No TitleScreenUI in {ScenePath}.";

        var fixedUp = new System.Collections.Generic.List<string>();
        foreach (var row in ui.rows)
        {
            if (row == null || row.labelText == null || string.IsNullOrEmpty(row.label)) continue;
            if (row.labelText.text == row.label) continue;

            fixedUp.Add($"{row.labelText.text} -> {row.label}");
            row.labelText.text = row.label;
            EditorUtility.SetDirty(row.labelText);
        }

        if (fixedUp.Count == 0) return "Title screen: every row already draws its own label.";

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        return "Title screen: " + string.Join(", ", fixedUp) + ".";
    }

    // Clear every null-script component under the menu column. Returns how many went.
    static int StripMissingScripts(TitleScreenUI ui)
    {
        int removed = 0;
        foreach (var row in ui.rows)
        {
            if (row == null || row.rect == null) continue;
            foreach (var t in row.rect.GetComponentsInChildren<Transform>(true))
                removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        }
        return removed;
    }

    // Relabel a row in place: both the serialized label the menu matches on and the TMP text the player
    // reads. The two must move together — the label is what code and tests look the row up by, and the
    // text is what is actually drawn.
    public static string RenameRow(string from, string to)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var ui = Object.FindObjectsByType<TitleScreenUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                       .FirstOrDefault();
        if (ui == null) return $"No TitleScreenUI in {ScenePath}.";

        var row = ui.rows.FirstOrDefault(r => r != null && (r.label == from || r.label == to));
        if (row == null) return $"No {from} row on the title screen.";

        // THREE names have to move together, not two. The serialized label is what the menu code and the
        // wiring tests match a row by; the TMP text is what the player reads; and the row GameObject is
        // named Row_<LABEL>, which is where TitleScreenWiringTests reads the drawn column order from
        // (RowLabel(rect.name)). Leave the object behind and the walk order and the column disagree, which
        // is precisely the bug those tests exist to catch — so it fails, correctly, on a half-done rename.
        string objectName = "Row_" + to.Replace(' ', '_');
        bool already = row.label == to
                       && (row.labelText == null || row.labelText.text == to)
                       && (row.rect == null || row.rect.name == objectName);
        if (already) return $"Title screen already has a {to} row — left alone.";

        row.label = to;
        if (row.labelText != null) { row.labelText.text = to; EditorUtility.SetDirty(row.labelText); }
        if (row.rect != null) { row.rect.name = objectName; EditorUtility.SetDirty(row.rect.gameObject); }

        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        return $"Title screen: {from} row relabelled to {to} (label, text and object name). " +
               "Command, position and subtitle untouched.";
    }

    // Insert a row directly after the first row carrying `after`. Returns what happened.
    public static string AddRow(string label, TitleScreenUI.Command command, string sceneName,
                                TitleScreenUI.Command after)
        => AddRow(label, command, sceneName, rows => rows.FindIndex(r => r != null && r.command == after));

    // Insert a row directly after the row labelled `afterLabel`. Returns what happened.
    public static string AddRow(string label, TitleScreenUI.Command command, string sceneName, string afterLabel)
        => AddRow(label, command, sceneName, rows => rows.FindIndex(r => r != null && r.label == afterLabel));

    static string AddRow(string label, TitleScreenUI.Command command, string sceneName,
                         System.Func<System.Collections.Generic.List<TitleScreenUI.Row>, int> findAnchor)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var ui = Object.FindObjectsByType<TitleScreenUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                       .FirstOrDefault();
        if (ui == null) return $"No TitleScreenUI in {ScenePath}.";

        // Always, before anything else — including the early return below, so re-running this cleans up
        // after an earlier run.
        //
        // Cloning a row brings its gold cursor with it, and the cursor's blink (IronOvalBlink) does NOT
        // survive the save: it is a second class declared inside IronOvalUI.cs, and Unity cannot resolve a
        // MonoBehaviour whose type is not in a file of its own. What lands in the scene is a component with
        // a null script, which SceneNavigationTests correctly reports as a broken object. TitleScreenUI
        // puts the blink back at runtime (InstallCursorBlinks), so the empty shell is pure debris.
        int stripped = StripMissingScripts(ui);
        if (stripped > 0) Debug.Log($"Title screen: cleared {stripped} missing script(s) left by a cloned row.");

        // The row may already be on the menu as a placeholder. The design had SINGLE RACE drawn disabled
        // (Command.NotWired) long before there was a scene behind it, which is the house style for "the
        // design has this and the game doesn't yet" — so the job is to WIRE it, not to add a second one.
        var placeholder = ui.rows.FirstOrDefault(r => r != null && r.label == label);
        if (placeholder != null)
        {
            if (placeholder.command == command && placeholder.sceneName == sceneName)
            {
                // Nothing to wire, but the strip above may still have cleaned debris out — and this path
                // used to return without saving, which quietly threw that away.
                if (stripped > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene, ScenePath);
                    AssetDatabase.Refresh();
                    return $"Title screen: {label} was already wired; cleared {stripped} missing script(s).";
                }
                return $"Title screen already has {label} wired — left alone.";
            }

            var was = placeholder.command;
            placeholder.command = command;
            placeholder.sceneName = sceneName;

            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            return $"Title screen: wired the existing {label} row ({was} -> {command} \"{sceneName}\"). " +
                   "Nothing was moved — the row was already in the design.";
        }

        int at = findAnchor(ui.rows);
        int insertAt = at >= 0 ? at + 1 : ui.rows.Count;

        // Clone a real row rather than building one: it carries the kit's font, spacing, colour and the
        // gold blinking cursor already, and a hand-tweaked row stays hand-tweaked.
        var template = ui.rows[Mathf.Clamp(insertAt - 1, 0, ui.rows.Count - 1)];
        if (template == null || template.rect == null) return "The menu has no row to copy.";

        var clone = Object.Instantiate(template.rect.gameObject, template.rect.parent);
        clone.name = "Row_" + label.Replace(' ', '_');
        Undo.RegisterCreatedObjectUndo(clone, "Add title row");

        var cloneRect = (RectTransform)clone.transform;
        cloneRect.SetSiblingIndex(template.rect.GetSiblingIndex() + 1);

        var text = clone.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();
        if (text == null) return "The copied row has no label.";
        text.text = label;

        var cursor = clone.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                          .FirstOrDefault(i => i.gameObject != clone);

        // Spacing the menu already uses, measured rather than assumed — the rows may have been moved.
        float spacing = 26f;
        if (ui.rows.Count >= 2 && ui.rows[0].rect != null && ui.rows[1].rect != null)
            spacing = Mathf.Abs(ui.rows[0].rect.anchoredPosition.y - ui.rows[1].rect.anchoredPosition.y);

        cloneRect.anchoredPosition = template.rect.anchoredPosition + Vector2.down * spacing;

        // Everything below the insertion point moves down by one row, so the block stays evenly spaced
        // wherever the designer put it. Measured by position rather than by list index, as RemoveRow does:
        // the list is the wiring and the column is the layout, and the two need not agree.
        float anchorY = template.rect.anchoredPosition.y;
        foreach (var other in ui.rows)
            if (other?.rect != null && other.rect.anchoredPosition.y < anchorY)
                other.rect.anchoredPosition += Vector2.down * spacing;

        if (ui.statusLabel != null &&
            ui.statusLabel.rectTransform.anchoredPosition.y < cloneRect.anchoredPosition.y)
            ui.statusLabel.rectTransform.anchoredPosition += Vector2.down * spacing;

        ui.rows.Insert(insertAt, new TitleScreenUI.Row
        {
            label = label,
            command = command,
            sceneName = sceneName,
            labelText = text,
            cursor = cursor != null ? cursor.gameObject : null,
            rect = cloneRect,
        });

        // Match the resting look the builder bakes in: only the first row reads as selected.
        var theme = PixelUITheme.Instance;
        if (theme != null)
        {
            text.color = theme.textDisabled;
            if (cursor != null) cursor.gameObject.SetActive(false);
        }

        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        return $"Title screen: added {label} -> {(string.IsNullOrEmpty(sceneName) ? command.ToString() : sceneName)} " +
               $"at row {insertAt + 1} of {ui.rows.Count}, and moved the rows below it down {spacing:0} px. " +
               "The scene was edited in place, so nothing hand-authored was touched.";
    }
}
