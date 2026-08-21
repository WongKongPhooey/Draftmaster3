using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Bulk font retarget: points every authored label at the theme's faces.
//
// Runtime-built UI already follows PixelUITheme (PixelGUI, BrandFonts, IronOvalUI), so moving the whole
// game onto one typeface is a theme edit for that half. Authored prefabs and scenes are the other half —
// their TMP_Text and legacy Text components hold a direct font reference, so they need rewriting once.
//
// Sizes are snapped to the target face's pixel cell (VT323 16, Silkscreen 8, Pixelify Sans 20). A bitmap
// face drawn at a size that is not a whole multiple of its cell is resampled and goes soft, which is the
// one thing this UI is built to avoid — so a Silkscreen 8 label becomes VT323 16, not VT323 8.
//
// Both items are undoable per asset (prefabs are saved immediately; scenes are left dirty for you to
// check and save) and neither opens a dialog, so they are safe to drive from MCP.
public static class FontRetargetMenu
{
    [MenuItem("Draftmaster/UI/Retarget Fonts In Prefabs", false, 60)]
    public static void RetargetPrefabs()
    {
        var theme = PixelUITheme.Instance;
        if (theme == null) { Debug.LogError("[FontRetarget] No PixelUITheme at Resources/UI/PixelUITheme."); return; }

        int prefabs = 0, labels = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Resources" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changed = Retarget(root, theme);
                if (changed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabs++;
                    labels += changed;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[FontRetarget] Retargeted {labels} label(s) across {prefabs} prefab(s).");
    }

    [MenuItem("Draftmaster/UI/Retarget Fonts In Open Scenes", false, 61)]
    public static void RetargetOpenScenes()
    {
        var theme = PixelUITheme.Instance;
        if (theme == null) { Debug.LogError("[FontRetarget] No PixelUITheme at Resources/UI/PixelUITheme."); return; }

        int labels = 0, scenes = 0;
        var roots = new List<GameObject>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            roots.Clear();
            scene.GetRootGameObjects(roots);

            int changed = 0;
            foreach (var root in roots) changed += Retarget(root, theme);
            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                scenes++;
                labels += changed;
            }
        }

        Debug.Log($"[FontRetarget] Retargeted {labels} label(s) across {scenes} open scene(s). Save the scene(s) to keep it.");
    }

    // ---- work ------------------------------------------------------------------------------------

    static int Retarget(GameObject root, PixelUITheme theme)
    {
        int changed = 0;

        var tmpFont = theme.data != null ? theme.data : theme.body;
        if (tmpFont != null)
        {
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                int size = IronOvalUI.Snap(tmpFont, Mathf.RoundToInt(label.fontSize));
                if (label.font == tmpFont && Mathf.Approximately(label.fontSize, size)) continue;

                Undo.RecordObject(label, "Retarget Fonts");
                label.font = tmpFont;
                if (tmpFont.material != null) label.fontSharedMaterial = tmpFont.material;
                label.fontSize = size;
                EditorUtility.SetDirty(label);
                changed++;
            }
        }

        var uiFont = theme.imguiFont;
        if (uiFont != null)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                int size = SnapLegacy(text.fontSize);
                if (text.font == uiFont && text.fontSize == size) continue;

                Undo.RecordObject(text, "Retarget Fonts");
                text.font = uiFont;
                text.fontSize = size;
                EditorUtility.SetDirty(text);
                changed++;
            }
        }

        return changed;
    }

    // Legacy Text carries no font asset to read a cell off, and the theme's IMGUI face is the same VT323
    // the rest of the UI uses, so snap to its 16px cell.
    static int SnapLegacy(int size) => Mathf.Max(16, Mathf.RoundToInt(size / 16f) * 16);
}
