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
            if (GUILayout.Button("Install Default Pit Cast", EditorStyles.toolbarButton, GUILayout.Width(150f))) InstallCast();
            if (GUILayout.Button("Edit Track Package", EditorStyles.toolbarButton, GUILayout.Width(130f)))
                EditorApplication.ExecuteMenuItem("Draftmaster/Tracks/Edit Selected Package (Prefab Mode)");
            GUILayout.FlexibleSpace();
            _onlyVisible = GUILayout.Toggle(_onlyVisible, "Only who appears", EditorStyles.toolbarButton, GUILayout.Width(115f));
            _filter = GUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.Width(140f));
        }
    }

    // What the whole window is answering "would they be here?" against.
    void DrawPreviewRow()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Previewing", GUILayout.Width(70f));
                var session = (RaceWeekend.Session)GUILayout.Toolbar(
                    (int)PlacedNPCSceneContext.PreviewSession,
                    new[] { "Practice", "Qualifying", "Race" }, GUILayout.Width(240f));
                if (session != PlacedNPCSceneContext.PreviewSession) PlacedNPCSceneContext.PreviewSession = session;

                EditorGUILayout.LabelField("Track", GUILayout.Width(40f));
                string track = EditorGUILayout.TextField(PlacedNPCSceneContext.PreviewTrack, GUILayout.Width(110f));
                if (track != PlacedNPCSceneContext.PreviewTrack) PlacedNPCSceneContext.PreviewTrack = track;

                EditorGUILayout.LabelField("Series", GUILayout.Width(45f));
                string series = EditorGUILayout.TextField(PlacedNPCSceneContext.PreviewSeries, GUILayout.Width(90f));
                if (series != PlacedNPCSceneContext.PreviewSeries) PlacedNPCSceneContext.PreviewSeries = series;
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
            EditorGUILayout.LabelField("P", EditorStyles.miniBoldLabel, GUILayout.Width(18f));
            EditorGUILayout.LabelField("Q", EditorStyles.miniBoldLabel, GUILayout.Width(18f));
            EditorGUILayout.LabelField("R", EditorStyles.miniBoldLabel, GUILayout.Width(18f));
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

            string here = PlacedNPCSceneContext.Evaluate(npc, PlacedNPCSceneContext.PreviewSession);
            if (_onlyVisible && here != null) continue;
            shown++;
            DrawRow(npc, here);
        }

        EditorGUILayout.EndScrollView();

        if (shown == 0)
            EditorGUILayout.HelpBox(all.Count == 0
                ? "No placed NPCs in this scene. 'Add NPC' drops one in front of the scene camera, or " +
                  "'Install Default Pit Cast' creates the greeter, engineer and crew chief as editable markers."
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

            foreach (RaceWeekend.Session s in System.Enum.GetValues(typeof(RaceWeekend.Session)))
                Tick(PlacedNPCSceneContext.Evaluate(npc, s) == null);

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

    static void Tick(bool on)
    {
        var style = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = on ? new Color(0.35f, 0.9f, 0.45f) : new Color(0.55f, 0.55f, 0.55f);
        EditorGUILayout.LabelField(on ? "✔" : "·", style, GUILayout.Width(18f));
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

    [MenuItem("Draftmaster/NPCs/Install Default Pit Cast")]
    public static void InstallCast()
    {
        var existing = PlacedNPCSceneContext.AllInScene();
        var made = new List<GameObject>();

        // The every-track cast only. The RV race engineer belongs to the track that owns the motorhome —
        // Draftmaster > NPCs > Add RV Engineer To Open Package.
        if (!HasRole(existing, PlacedNPC.Role.PitGreeter)) made.Add(PlacedNPCDefaults.CreateGreeter().gameObject);
        if (!HasRole(existing, PlacedNPC.Role.CrewChief)) made.Add(PlacedNPCDefaults.CreateChief().gameObject);

        if (made.Count == 0)
        {
            EditorUtility.DisplayDialog("Default Pit Cast",
                "The greeter and crew chief are already placed in this scene.", "OK");
            return;
        }

        // Under the scene's "NPCs" root, matching where the runtime install puts them.
        var parent = PlacedNPCDefaults.Root();
        foreach (var go in made)
        {
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Install Default Pit Cast");
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Selection.objects = made.ToArray();
        Debug.Log($"NPC Director: installed {made.Count} default cast marker(s). Save the scene to keep them.");
        Open();
    }

    static bool HasRole(List<PlacedNPC> list, PlacedNPC.Role role)
    {
        foreach (var npc in list)
            if (npc != null && npc.role == role) return true;
        return false;
    }
}
