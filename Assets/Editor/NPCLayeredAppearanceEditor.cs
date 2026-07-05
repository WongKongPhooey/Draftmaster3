using UnityEditor;
using UnityEngine;

// Inspector for designing NPCs in the editor (Stardew-style paper doll): per layer, pick a style
// (which sprite sheet) and a colour, with live preview in the Scene view. The preview layers are
// ordinary child objects, so what you see in the scene is what builds at runtime; Build() sweeps
// and rebuilds them on Play, so they never double up.
[CustomEditor(typeof(NPCLayeredAppearance))]
public class NPCLayeredAppearanceEditor : Editor
{
    int _previewFrame;

    public override void OnInspectorGUI()
    {
        var app = (NPCLayeredAppearance)target;

        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "authoredOutfit");
        serializedObject.ApplyModifiedProperties();

        var lib = app.library;
        bool changed = false;

        if (app.useAuthoredOutfit)
        {
            if (lib == null || lib.categories == null || lib.categories.Length == 0)
            {
                EditorGUILayout.HelpBox("Assign a Part Library to author an outfit.", MessageType.Info);
            }
            else
            {
                SyncChoices(app, lib);
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Outfit", EditorStyles.boldLabel);
                foreach (var cat in lib.categories)
                    changed |= DrawCategoryRow(app, cat);
            }
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview / Rebuild")) { Rebuild(app); _previewFrame = 0; }
            if (GUILayout.Button("Randomize") && lib != null) { Randomize(app, lib); Rebuild(app); }
            if (GUILayout.Button("Clear Preview")) { Undo.RegisterFullObjectHierarchyUndo(app.gameObject, "Clear NPC Preview"); app.Clear(); }
        }

        // Scrub the walk cycle to check that every layer stays aligned frame-for-frame.
        if (app.Built && app.FrameCount > 1)
        {
            int f = EditorGUILayout.IntSlider("Preview Frame", _previewFrame, 0, app.FrameCount - 1);
            if (f != _previewFrame) { _previewFrame = f; app.SetFrame(f); SceneView.RepaintAll(); }
        }

        if (changed)
        {
            EditorUtility.SetDirty(app);
            if (HasPreview(app)) Rebuild(app);
        }
    }

    // One row per library category: include toggle, style dropdown, colour field, palette swatches.
    bool DrawCategoryRow(NPCLayeredAppearance app, NPCPartLibrary.PartCategory cat)
    {
        var choice = FindChoice(app, cat.name);
        if (choice == null) return false;

        bool changed = false;
        using (new EditorGUILayout.HorizontalScope())
        {
            bool include = EditorGUILayout.ToggleLeft(cat.name, choice.include, GUILayout.Width(90));
            if (include != choice.include)
            {
                Undo.RecordObject(app, "Edit NPC Outfit");
                choice.include = include;
                changed = true;
            }

            using (new EditorGUI.DisabledScope(!choice.include))
            {
                // Style dropdown: sheet asset names, plus Random as the last entry (styleIndex -1).
                int optionCount = cat.options != null ? cat.options.Length : 0;
                var names = new string[optionCount + 1];
                for (int i = 0; i < optionCount; i++)
                    names[i] = cat.options[i] != null ? cat.options[i].name : "(missing)";
                names[optionCount] = "Random";

                int shown = (choice.styleIndex >= 0 && choice.styleIndex < optionCount) ? choice.styleIndex : optionCount;
                int picked = EditorGUILayout.Popup(shown, names);
                int newIndex = picked >= optionCount ? -1 : picked;
                if (newIndex != choice.styleIndex)
                {
                    Undo.RecordObject(app, "Edit NPC Outfit");
                    choice.styleIndex = newIndex;
                    changed = true;
                }

                Color tint = EditorGUILayout.ColorField(GUIContent.none, choice.tint, true, false, false, GUILayout.Width(60));
                if (tint != choice.tint)
                {
                    Undo.RecordObject(app, "Edit NPC Outfit");
                    choice.tint = tint;
                    changed = true;
                }
            }
        }

        // Swatch row from the category's palette, for one-click colours.
        if (choice.include && cat.tintOptions != null && cat.tintOptions.Length > 0)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(94);
                for (int i = 0; i < cat.tintOptions.Length; i++)
                {
                    var prev = GUI.backgroundColor;
                    GUI.backgroundColor = cat.tintOptions[i];
                    if (GUILayout.Button(GUIContent.none, GUILayout.Width(18), GUILayout.Height(14)))
                    {
                        Undo.RecordObject(app, "Edit NPC Outfit");
                        choice.tint = cat.tintOptions[i];
                        changed = true;
                    }
                    GUI.backgroundColor = prev;
                }
                GUILayout.FlexibleSpace();
            }
        }
        return changed;
    }

    // Make sure authoredOutfit has exactly one entry per library category, preserving existing picks.
    static void SyncChoices(NPCLayeredAppearance app, NPCPartLibrary lib)
    {
        var fresh = new NPCLayeredAppearance.LayerChoice[lib.categories.Length];
        bool dirty = app.authoredOutfit == null || app.authoredOutfit.Length != lib.categories.Length;
        for (int i = 0; i < lib.categories.Length; i++)
        {
            var existing = FindChoice(app, lib.categories[i].name);
            fresh[i] = existing ?? new NPCLayeredAppearance.LayerChoice { category = lib.categories[i].name };
            if (existing == null) dirty = true;
        }
        if (dirty)
        {
            app.authoredOutfit = fresh;
            EditorUtility.SetDirty(app);
        }
        else
        {
            app.authoredOutfit = fresh; // reorder to library order; contents unchanged
        }
    }

    static NPCLayeredAppearance.LayerChoice FindChoice(NPCLayeredAppearance app, string category)
    {
        if (app.authoredOutfit == null) return null;
        foreach (var c in app.authoredOutfit)
            if (c != null && c.category == category) return c;
        return null;
    }

    static void Randomize(NPCLayeredAppearance app, NPCPartLibrary lib)
    {
        Undo.RecordObject(app, "Randomize NPC Outfit");
        SyncChoices(app, lib);
        foreach (var cat in lib.categories)
        {
            var choice = FindChoice(app, cat.name);
            if (choice == null || cat.options == null || cat.options.Length == 0) continue;
            choice.include = !cat.optional || Random.value <= cat.presentChance;
            choice.styleIndex = Random.Range(0, cat.options.Length);
            if (cat.tintOptions != null && cat.tintOptions.Length > 0)
                choice.tint = cat.tintOptions[Random.Range(0, cat.tintOptions.Length)];
            else if (cat.randomHue)
                choice.tint = Color.HSVToRGB(Random.value, 0.5f + Random.value * 0.4f, 0.6f + Random.value * 0.35f);
            else
                choice.tint = Color.white;
        }
        EditorUtility.SetDirty(app);
    }

    static void Rebuild(NPCLayeredAppearance app)
    {
        Undo.RegisterFullObjectHierarchyUndo(app.gameObject, "Rebuild NPC Preview");
        if (!app.Build())
            Debug.LogWarning("NPCLayeredAppearance: nothing to build — check the library has sheets assigned.", app);
        SceneView.RepaintAll();
    }

    static bool HasPreview(NPCLayeredAppearance app)
        => app.GetComponentInChildren<NPCLayerTag>(true) != null;

    // Scene-menu shortcut: a ready-to-design NPC with the project's part library pre-assigned.
    [MenuItem("GameObject/2D Object/Layered NPC (Paper Doll)", false, 10)]
    static void CreateLayeredNpc(MenuCommand cmd)
    {
        var go = new GameObject("LayeredNPC");
        GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);

        var app = go.AddComponent<NPCLayeredAppearance>();
        app.useAuthoredOutfit = true;

        var guids = AssetDatabase.FindAssets("t:NPCPartLibrary");
        if (guids.Length > 0)
        {
            app.library = AssetDatabase.LoadAssetAtPath<NPCPartLibrary>(AssetDatabase.GUIDToAssetPath(guids[0]));
            SyncChoices(app, app.library);
            app.Build();
        }

        Undo.RegisterCreatedObjectUndo(go, "Create Layered NPC");
        Selection.activeGameObject = go;
    }
}
