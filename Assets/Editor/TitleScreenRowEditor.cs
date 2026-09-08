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

        int at = ui.rows.FindIndex(r => r != null && r.command == after);
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
        // wherever the designer put it.
        for (int i = insertAt; i < ui.rows.Count; i++)
            if (ui.rows[i]?.rect != null)
                ui.rows[i].rect.anchoredPosition += Vector2.down * spacing;

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
