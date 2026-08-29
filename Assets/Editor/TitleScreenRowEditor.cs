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

    // Insert a row directly after the first row carrying `after`. Returns what happened.
    public static string AddRow(string label, TitleScreenUI.Command command, string sceneName,
                                TitleScreenUI.Command after)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var ui = Object.FindObjectsByType<TitleScreenUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                       .FirstOrDefault();
        if (ui == null) return $"No TitleScreenUI in {ScenePath}.";

        // The row may already be on the menu as a placeholder. The design had SINGLE RACE drawn disabled
        // (Command.NotWired) long before there was a scene behind it, which is the house style for "the
        // design has this and the game doesn't yet" — so the job is to WIRE it, not to add a second one.
        var placeholder = ui.rows.FirstOrDefault(r => r != null && r.label == label);
        if (placeholder != null)
        {
            if (placeholder.command == command && placeholder.sceneName == sceneName)
                return $"Title screen already has {label} wired to {sceneName} — left alone.";

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
