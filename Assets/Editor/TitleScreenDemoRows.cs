using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Draftmaster > UI > Set Up Demo Rows On Title Screen
//
// Splits the title menu into its two builds, IN PLACE. Same reasoning as TitleScreenRowEditor next door:
// IronOvalTitleBuilder regenerates TitleScreen.unity from code and would destroy the hand-authored
// wordmark, layout and labels, so a menu change is a surgical edit to the scene, never a rebuild.
//
// What it does:
//   * marks the rows the demo does not get (the exhibition row, the factory row) as FullOnly,
//   * adds a RESTART DEMO row, marked DemoOnly, cloned from the bottom row so it inherits the kit's font,
//     cursor and styling exactly.
//
// Nothing is deleted and nothing else moves: TitleScreenUI hides the rows this build doesn't draw at
// runtime and closes the column up over the gaps, so in the editor the menu still shows every row.
//
// Idempotent — running it twice does not add two rows.
public static class TitleScreenDemoRows
{
    const string ScenePath = "Assets/Scenes/TitleScreen.unity";
    const string RestartLabel = "RESTART DEMO";

    [MenuItem("Draftmaster/UI/Set Up Demo Rows On Title Screen", priority = 402)]
    public static void SetUp() => Debug.Log(Apply());

    // Returns what happened rather than announcing it: a DisplayDialog blocks the editor until clicked,
    // which wedges automation and MCP.
    public static string Apply()
    {
        // Mid-compile, a component whose script has not been reloaded yet reads as a MISSING script — and
        // this method deletes those. Running it during a domain reload once ate the blinking cursor off
        // the row it had just added, so it refuses rather than repeating that.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return "Unity is still compiling — run this again once it has settled.";

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var ui = Object.FindObjectsByType<TitleScreenUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                       .FirstOrDefault();
        if (ui == null) return $"No TitleScreenUI in {ScenePath}.";

        var changes = new System.Collections.Generic.List<string>();

        // The full game keeps these; the demo menu is Continue / Multiplayer / Single Race / Options /
        // Restart Demo. Matched on COMMAND, not on the label — the labels in the scene have been rewritten
        // by hand and no longer match the ones the builder wrote.
        foreach (var row in ui.rows)
        {
            if (row == null) continue;
            bool full = row.command == TitleScreenUI.Command.Exhibition
                        || (row.command == TitleScreenUI.Command.LoadScene && row.sceneName == "TeamGarage");
            if (!full || row.appearsIn == TitleScreenUI.Build.FullOnly) continue;

            row.appearsIn = TitleScreenUI.Build.FullOnly;
            changes.Add($"{Shown(row)} -> full release only");
        }

        var existing = ui.rows.FirstOrDefault(r => r != null && r.command == TitleScreenUI.Command.RestartDemo);
        if (existing != null)
        {
            if (existing.appearsIn != TitleScreenUI.Build.DemoOnly)
            {
                existing.appearsIn = TitleScreenUI.Build.DemoOnly;
                changes.Add($"{Shown(existing)} -> demo only");
            }

            int stripped = StripMissingScripts(existing.rect);
            if (stripped > 0) changes.Add($"cleared {stripped} missing script(s) off {Shown(existing)}");
            if (RepairCursor(ui, existing)) changes.Add($"rebuilt the blinking cursor on {Shown(existing)}");
        }
        else
        {
            string added = AddRestartRow(ui);
            if (added == null) return "The menu has no row to copy.";
            changes.Add(added);
        }

        if (changes.Count == 0) return "Title screen: demo rows already set up — nothing changed.";

        EditorUtility.SetDirty(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        return "Title screen: " + string.Join("; ", changes)
               + ". Edited in place, so nothing hand-authored was touched.";
    }

    // Clone the bottom row and hang the new one under it, at the spacing the menu already uses.
    static string AddRestartRow(TitleScreenUI ui)
    {
        var placed = ui.rows.Where(r => r != null && r.rect != null).ToList();
        if (placed.Count == 0) return null;

        var bottom = placed.OrderBy(r => r.rect.anchoredPosition.y).First();

        float spacing = 26f;    // the builder's row pitch, used when there is nothing to measure
        if (placed.Count >= 2)
        {
            var ys = placed.Select(r => r.rect.anchoredPosition.y).OrderByDescending(y => y).ToList();
            float smallest = 0f;
            for (int i = 1; i < ys.Count; i++)
            {
                float gap = ys[i - 1] - ys[i];
                if (gap > 0.01f && (smallest <= 0f || gap < smallest)) smallest = gap;
            }
            if (smallest > 0f) spacing = smallest;
        }

        var clone = Object.Instantiate(bottom.rect.gameObject, bottom.rect.parent);
        clone.name = "Row_" + RestartLabel.Replace(' ', '_');
        Undo.RegisterCreatedObjectUndo(clone, "Add RESTART DEMO row");

        var cloneRect = (RectTransform)clone.transform;
        cloneRect.SetSiblingIndex(bottom.rect.GetSiblingIndex() + 1);
        cloneRect.anchoredPosition = bottom.rect.anchoredPosition + Vector2.down * spacing;

        var text = clone.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();
        if (text == null) return null;
        text.text = RestartLabel;

        var cursor = clone.GetComponentsInChildren<UnityEngine.UI.Image>(true)
                          .FirstOrDefault(i => i.gameObject != clone);

        var theme = PixelUITheme.Instance;
        if (theme != null) text.color = theme.textDisabled;      // only the opening row reads as selected
        if (cursor != null) cursor.gameObject.SetActive(false);

        // Instantiating a row leaves an empty script slot on the cursor — the blinking arrow's component
        // is a second class inside IronOvalUI.cs, and the copy comes out with the component AND a broken
        // stand-in beside it. Harmless to the game, but SceneNavigationTests counts missing scripts as a
        // scene defect (rightly: it is normally a deleted component), so clear it while we are here.
        StripMissingScripts(cloneRect);

        var added = new TitleScreenUI.Row
        {
            label = RestartLabel,
            command = TitleScreenUI.Command.RestartDemo,
            sceneName = "",
            appearsIn = TitleScreenUI.Build.DemoOnly,
            labelText = text,
            cursor = cursor != null ? cursor.gameObject : null,
            rect = cloneRect,
        };
        ui.rows.Add(added);
        RepairCursor(ui, added);

        return $"added {RestartLabel} (demo only) under {Shown(bottom)}, {spacing:0} px below it";
    }

    // The gold arrow blinks itself: IronOvalBlink, a second class inside IronOvalUI.cs. A COPY of that
    // component — from Instantiate or from paste — writes a script reference that does not resolve when
    // the scene is next opened, and the row comes back with a missing script where its blink was (which
    // SceneNavigationTests fails the scene for, rightly). Copying a component is the problem, so don't:
    // throw the copied cursor away and build a fresh one through IronOvalUI.Cursor, the same call that
    // made every other cursor in this menu, keeping the geometry of the one it replaces.
    // True = the cursor was rebuilt.
    static bool RepairCursor(TitleScreenUI ui, TitleScreenUI.Row row)
    {
        if (row == null || row.rect == null) return false;

        var cursor = row.cursor;
        bool sound = cursor != null
                     && cursor.GetComponent<IronOvalBlink>() != null
                     && GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(cursor) == 0;
        if (sound) return false;

        // Geometry comes off whichever cursor is healthy, or off the broken one before it goes.
        RectTransform model = cursor != null ? (RectTransform)cursor.transform : null;
        foreach (var other in ui.rows)
        {
            if (other == null || other == row || other.cursor == null) continue;
            if (other.cursor.GetComponent<IronOvalBlink>() == null) continue;
            model = (RectTransform)other.cursor.transform;
            break;
        }

        Vector2 anchorMin = Vector2.zero, anchorMax = Vector2.zero, pivot = new Vector2(0f, 1f);
        Vector2 size = new Vector2(6f, 8f), at = new Vector2(0f, -7f);
        if (model != null)
        {
            anchorMin = model.anchorMin;
            anchorMax = model.anchorMax;
            pivot = model.pivot;
            size = model.sizeDelta;
            at = model.anchoredPosition;
        }

        if (cursor != null) Undo.DestroyObjectImmediate(cursor);

        var built = IronOvalUI.Cursor(row.rect);
        var rt = built.rectTransform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = at;
        built.gameObject.SetActive(false);      // only the opening row rests with a cursor showing

        row.cursor = built.gameObject;
        return true;
    }

    // Empty script slots on a row and everything under it. Returns how many were cleared.
    static int StripMissingScripts(RectTransform root)
    {
        if (root == null) return 0;

        int cleared = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            cleared += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        return cleared;
    }

    // What the row actually reads on screen, which is not always what its label field says.
    static string Shown(TitleScreenUI.Row row)
        => row.labelText != null && !string.IsNullOrEmpty(row.labelText.text) ? row.labelText.text : row.label;
}
