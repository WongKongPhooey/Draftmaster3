using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The NPC Director: one place to see and manage every editor-placed NPC in the open scene or track package.
//
// The table answers the question the inspector can't — "who is actually in the paddock during qualifying?"
// Each row shows the three sessions as ticks, so a cast that changes across a race weekend can be read at a
// glance, and the row explains itself when somebody doesn't turn up.
//
// The session/track/series pickers at the top drive AppearanceConditions.Preview, which the scene-view
// gizmos read too: flip to Race and the people who won't be there grey out in the scene.
public class NPCDirectorWindow : EditorWindow
{
    [MenuItem("Draftmaster/NPCs/Director %#n")]
    public static void Open()
    {
        var w = GetWindow<NPCDirectorWindow>("NPC Director");
        w.minSize = new Vector2(560f, 260f);
        w.Show();
    }

    Vector2 _scroll;
    string _filter = "";
    bool _onlyVisible;

    void OnEnable()
    {
        EditorApplication.hierarchyChanged += Repaint;
        Selection.selectionChanged += Repaint;
    }

    void OnDisable()
    {
        EditorApplication.hierarchyChanged -= Repaint;
        Selection.selectionChanged -= Repaint;
    }

    void OnGUI()
    {
        DrawToolbar();
        DrawPreviewRow();
        EditorGUILayout.Space(2);
        DrawTable();
        EditorGUILayout.Space(2);
        DrawFooter();
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Add NPC", EditorStyles.toolbarButton, GUILayout.Width(70f))) AddNPC();
            if (GUILayout.Button("Install Core Cast", EditorStyles.toolbarButton, GUILayout.Width(110f))) InstallCoreCast();
            if (GUILayout.Button("Edit Track Package", EditorStyles.toolbarButton, GUILayout.Width(130f)))
                EditorApplication.ExecuteMenuItem("Draftmaster/Tracks/Edit Selected Package (Prefab Mode)");
            GUILayout.FlexibleSpace();
            _onlyVisible = GUILayout.Toggle(_onlyVisible, "Only who appears", EditorStyles.toolbarButton, GUILayout.Width(115f));
            _filter = GUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.Width(140f));
        }
    }

    // What the whole window is answering "would they be here?" against: a day of the weekend and which half
    // of it. Everything reads this — the table, the scene-view gizmos and the inspector card — so picking
    // SATURDAY / AFTERNOON here shows Saturday afternoon's paddock in the scene view.
    void DrawPreviewRow()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            var slot = PlacedNPCSceneContext.PreviewSlot;
            int day = (int)slot / 2;
            int half = (int)slot % 2;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Day", GUILayout.Width(30f));
                int newDay = GUILayout.Toolbar(day, new[] { "FRIDAY", "SATURDAY", "SUNDAY" }, GUILayout.Width(240f));

                EditorGUILayout.LabelField("Time", GUILayout.Width(36f));
                int newHalf = GUILayout.Toolbar(half, new[] { "MORNING", "AFTERNOON" }, GUILayout.Width(180f));

                if (newDay != day || newHalf != half)
                {
                    var picked = (Draftmaster.Weekend.WeekendSlot)(newDay * 2 + newHalf);
                    PlacedNPCSceneContext.PreviewSlot = picked;
                    // No timetable in this window, so the day picks the session: practice Friday,
                    // qualifying Saturday, the race Sunday. Weekend Cast overrides it from the real sheet.
                    PlacedNPCSceneContext.PreviewSession = PlacedNPCSceneContext.SessionFor(picked);
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    $"session: {PlacedNPCSceneContext.PreviewSession}", EditorStyles.miniLabel, GUILayout.Width(130f));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Track", GUILayout.Width(40f));
                string track = EditorGUILayout.TextField(PlacedNPCSceneContext.PreviewTrack, GUILayout.Width(110f));
                if (track != PlacedNPCSceneContext.PreviewTrack) PlacedNPCSceneContext.PreviewTrack = track;

                EditorGUILayout.LabelField("Series", GUILayout.Width(45f));
                string series = EditorGUILayout.TextField(PlacedNPCSceneContext.PreviewSeries, GUILayout.Width(90f));
                if (series != PlacedNPCSceneContext.PreviewSeries) PlacedNPCSceneContext.PreviewSeries = series;

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    "Practice runs Friday, qualifying Saturday, the race Sunday — so the day picks the session.",
                    EditorStyles.miniLabel);
            }

            if (!PlacedNPCSceneContext.HasTrack)
                EditorGUILayout.LabelField(
                    "No track in the open scene — anchored NPCs draw at their marker. " +
                    "Draftmaster > Tracks > Preview Selected Package In Scene.", EditorStyles.miniLabel);
        }
    }

    void DrawTable()
    {
        var all = PlacedNPCSceneContext.AllInScene();
        all.Sort((a, b) => string.Compare(a.Label, b.Label, System.StringComparison.OrdinalIgnoreCase));

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUILayout.LabelField("NPC", EditorStyles.miniBoldLabel, GUILayout.Width(150f));
            EditorGUILayout.LabelField("Anchor", EditorStyles.miniBoldLabel, GUILayout.Width(85f));

            // The whole weekend across the row: six half-days, the previewed one in bold. Reading down a
            // column is "who is in the paddock on Saturday morning".
            foreach (var s in Draftmaster.Weekend.WeekendSlots.All)
            {
                var head = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
                if (s == PlacedNPCSceneContext.PreviewSlot) head.normal.textColor = new Color(0.95f, 0.85f, 0.4f);
                EditorGUILayout.LabelField(Draftmaster.Weekend.WeekendSlots.ShortLabel(s), head, GUILayout.Width(46f));
            }

            EditorGUILayout.LabelField("Rules / why not", EditorStyles.miniBoldLabel);
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        int shown = 0;
        foreach (var npc in all)
        {
            if (npc == null) continue;
            if (!string.IsNullOrEmpty(_filter) &&
                npc.Label.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                npc.name.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

            string here = PlacedNPCSceneContext.Evaluate(npc, PlacedNPCSceneContext.PreviewSlot);
            if (_onlyVisible && here != null) continue;
            shown++;
            DrawRow(npc, here);
        }

        EditorGUILayout.EndScrollView();

        if (shown == 0)
            EditorGUILayout.HelpBox(all.Count == 0
                ? "No placed NPCs in this scene. 'Add NPC' drops one in front of the scene camera; " +
                  "'Install Cast' puts the people every track has — the pit greeter, the crew chief, the " +
                  "team liaison at the motorhome door, the strategist and the PR manager — into the scene " +
                  "as markers you can move and edit."
                : "Nothing matches the filter.", MessageType.None);
    }

    void DrawRow(PlacedNPC npc, string unmetHere)
    {
        bool selected = Selection.activeGameObject == npc.gameObject;
        var row = new GUIStyle(selected ? "SelectionRect" : GUIStyle.none) { padding = new RectOffset(2, 2, 2, 2) };

        using (new EditorGUILayout.HorizontalScope(row))
        {
            if (GUILayout.Button(npc.Label, EditorStyles.label, GUILayout.Width(150f)))
            {
                Selection.activeGameObject = npc.gameObject;
                EditorGUIUtility.PingObject(npc.gameObject);
                SceneView.lastActiveSceneView?.Frame(new Bounds(npc.ResolveStandPoint(), Vector3.one * 6f), false);
            }

            EditorGUILayout.LabelField(npc.anchor.ToString(), EditorStyles.miniLabel, GUILayout.Width(85f));

            foreach (var s in Draftmaster.Weekend.WeekendSlots.All)
                Tick(PlacedNPCSceneContext.Evaluate(npc, s) == null, s == PlacedNPCSceneContext.PreviewSlot);

            EditorGUILayout.LabelField(unmetHere == null ? npc.appear.Summarise() : "— " + unmetHere,
                                       EditorStyles.miniLabel);

            if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20f)))
            {
                if (EditorUtility.DisplayDialog("Remove NPC", $"Delete '{npc.Label}' from the scene?", "Delete", "Cancel"))
                    Undo.DestroyObjectImmediate(npc.gameObject);
                GUIUtility.ExitGUI();
            }
        }
    }

    static void Tick(bool on, bool previewed = false)
    {
        var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = on
            ? (previewed ? new Color(0.5f, 1f, 0.55f) : new Color(0.35f, 0.9f, 0.45f))
            : new Color(0.45f, 0.45f, 0.45f);
        EditorGUILayout.LabelField(on ? "✔" : "·", style, GUILayout.Width(46f));
    }

    // The crowd this window can't list: NPCs spawned at random by PaddockSpawner / AutographFanSpawner and
    // the ambient barks. They have no marker to select, but what they SAY is authorable per track — that's
    // a DialoguePool asset, and this is where you get at it.
    void DrawChatterPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Random crowd (paddock talkers, fans, ambient barks)", EditorStyles.miniBoldLabel);

            string track = PlacedNPCSceneContext.PreviewTrack;
            var global = DialoguePoolMenu.Find("");
            var forTrack = string.IsNullOrEmpty(track) ? null : DialoguePoolMenu.Find(track);

            PoolRow("Global pool", global, "");
            PoolRow(string.IsNullOrEmpty(track) ? "Track pool" : $"{track} pool", forTrack, track);

            EditorGUILayout.LabelField(
                "Track lines are ADDED to the global pool and the built-in tables, unless the pool's " +
                "Replace Built In is on.", EditorStyles.miniLabel);
        }
    }

    void PoolRow(string label, Draftmaster.Chatter.DialoguePool pool, string trackId)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, GUILayout.Width(140f));

            if (pool == null)
            {
                EditorGUILayout.LabelField("none", EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(label != "Global pool" && string.IsNullOrEmpty(trackId)))
                    if (GUILayout.Button("Create", GUILayout.Width(70f)))
                    {
                        Selection.activeObject = DialoguePoolMenu.EnsurePool(trackId);
                        EditorGUIUtility.PingObject(Selection.activeObject);
                    }
                return;
            }

            int barks = 0, convos = 0;
            if (pool.chatter != null) foreach (var c in pool.chatter) if (c?.lines != null) barks += c.lines.Length;
            if (pool.conversations != null) foreach (var c in pool.conversations) if (c?.lines != null && c.lines.Length > 0) convos++;

            EditorGUILayout.LabelField(
                $"{barks} bark(s), {convos} conversation(s){(pool.replaceBuiltIn ? ", REPLACES built-ins" : "")}",
                EditorStyles.miniLabel);
            if (GUILayout.Button("Edit", GUILayout.Width(70f)))
            {
                Selection.activeObject = pool;
                EditorGUIUtility.PingObject(pool);
            }
        }
    }

    void DrawFooter()
    {
        DrawChatterPanel();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Clear Appearance Flags"))
                EditorApplication.ExecuteMenuItem("Draftmaster/NPCs/Clear Appearance Flags");
            if (GUILayout.Button("Repaint Scene")) SceneView.RepaintAll();
        }
        EditorGUILayout.LabelField(
            "Track-specific NPCs belong in the track package (Paddock/NPCs); the every-track cast belongs in RaceScene.",
            EditorStyles.centeredGreyMiniLabel);
    }

    // ---------------------------------------------------------------- actions

    [MenuItem("Draftmaster/NPCs/Add Placed NPC")]
    public static void AddNPC()
    {
        var go = new GameObject("NPC_New");
        var npc = go.AddComponent<PlacedNPC>();
        npc.npcId = "npc.new";
        npc.speakerName = "Crew Member";

        // Drop them where the author is looking, and under the package's NPC root if one is open — that's
        // the difference between an NPC that travels with the track and one that doesn't.
        var view = SceneView.lastActiveSceneView;
        if (view != null) go.transform.position = view.pivot;

        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            var root = stage.prefabContentsRoot.transform.Find("Paddock/NPCs");
            go.transform.SetParent(root != null ? root : stage.prefabContentsRoot.transform, true);
        }
        else
        {
            go.transform.SetParent(PlacedNPCDefaults.Root(), true);
        }

        Undo.RegisterCreatedObjectUndo(go, "Add Placed NPC");
        Selection.activeGameObject = go;
        Open();
    }

    // Everybody every track has, as real markers you can select, move and edit. Without this they are
    // built from code when the scene runs, which means opening a scene shows an empty paddock and there is
    // nothing to click on — the thing this window exists to fix.
    [MenuItem("Draftmaster/NPCs/Install Core Cast")]
    public static void InstallCoreCast()
    {
        var before = PlacedNPCSceneContext.AllInScene().Count;
        int added = PlacedNPCDefaults.EnsureCoreCast(PlacedNPCDefaults.Root());

        if (added == 0)
        {
            Debug.Log("NPC Director: this scene already has the whole every-track cast.");
            Open();
            return;
        }

        foreach (var npc in PlacedNPCSceneContext.AllInScene())
            if (npc != null) Undo.RegisterCreatedObjectUndo(npc.gameObject, "Install cast");

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log($"NPC Director: added {added} marker(s) — {before + added} in the scene now. " +
                  "They stand where their anchors put them (the pit lane, the parked car, the motorhome " +
                  "door); move them with the scene-view handle or their anchor offsets. Save the scene to keep them.");
        Open();
    }

}
