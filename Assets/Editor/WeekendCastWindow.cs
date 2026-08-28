using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The race weekend, half-day by half-day: who is in the paddock, what is booked, and what any of them
// actually say.
//
// The NPC Director answers "who turns up in practice / qualifying / the race". This answers the question
// that comes up while writing a weekend: pick Friday afternoon, see everything that happens in it — the
// bookings on the sheet, the person waiting at each venue, and every placed NPC who would be stood out
// there — then click one and edit its dialogue, its quest and its objective marker without going hunting
// through the hierarchy.
//
// Nothing here is a second source of truth: the bookings come from WeekendTimetable, the venues and their
// hosts from WeekendVenues / WeekendVenueCast, the obligation dialogue from the same content the game
// plays, and the placed NPCs are the scene's own markers, edited through their real serialized fields.
public class WeekendCastWindow : EditorWindow
{
    [MenuItem("Draftmaster/NPCs/Weekend Cast %#w")]
    public static void Open()
    {
        var w = GetWindow<WeekendCastWindow>("Weekend Cast");
        w.minSize = new Vector2(620f, 420f);
        w.Show();
    }

    WeekendSlot _slot = WeekendSlot.FridayAM;
    RacingSeries _series = RacingSeries.Cup;
    int _weekendId;

    Vector2 _scroll;
    string _selectedActivityId = "";
    PlacedNPC _selected;
    Editor _selectedInspector;
    bool _showFullInspector;

    WeekendTimetable _timetable;

    void OnEnable()
    {
        EditorApplication.hierarchyChanged += Repaint;
        Selection.selectionChanged += OnSelectionChanged;
        Rebuild();
    }

    void OnDisable()
    {
        EditorApplication.hierarchyChanged -= Repaint;
        Selection.selectionChanged -= OnSelectionChanged;
        DestroyInspector();
    }

    void OnSelectionChanged()
    {
        var picked = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<PlacedNPC>() : null;
        if (picked != null) Select(picked);
        Repaint();
    }

    void Rebuild()
    {
        _timetable = WeekendTimetable.Build(_series, _weekendId, PlacedNPCSceneContext.PreviewTrack);
        // The scene-view gizmos and the Director read this, so picking a half-day here greys out anybody
        // who would not be in the paddock during it.
        PlacedNPCSceneContext.PreviewSession = SessionOf(_slot);
    }

    void OnGUI()
    {
        DrawToolbar();
        DrawSlotTabs();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawWhatsOn();
        EditorGUILayout.Space(6);
        DrawPaddockCast();
        EditorGUILayout.Space(6);
        DrawDetail();
        EditorGUILayout.EndScrollView();
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            EditorGUI.BeginChangeCheck();
            _series = (RacingSeries)EditorGUILayout.EnumPopup(_series, EditorStyles.toolbarPopup, GUILayout.Width(90f));
            GUILayout.Label("weekend", EditorStyles.miniLabel, GUILayout.Width(52f));
            _weekendId = EditorGUILayout.IntField(_weekendId, EditorStyles.toolbarTextField, GUILayout.Width(34f));

            GUILayout.Label("track", EditorStyles.miniLabel, GUILayout.Width(32f));
            string track = EditorGUILayout.TextField(PlacedNPCSceneContext.PreviewTrack,
                                                    EditorStyles.toolbarTextField, GUILayout.Width(100f));
            if (track != PlacedNPCSceneContext.PreviewTrack) PlacedNPCSceneContext.PreviewTrack = track;
            if (EditorGUI.EndChangeCheck()) Rebuild();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Install Core Cast", EditorStyles.toolbarButton, GUILayout.Width(110f)))
                InstallCoreCast();
            if (GUILayout.Button("Add NPC", EditorStyles.toolbarButton, GUILayout.Width(65f)))
                EditorApplication.ExecuteMenuItem("Draftmaster/NPCs/Add Placed NPC");
            if (GUILayout.Button("NPC Director", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                NPCDirectorWindow.Open();
        }
    }

    // The people every track has, whatever series is running: greeter, crew chief, engineer, strategist,
    // PR and the team liaison. Everything else in the paddock is crowd scattered around them.
    void InstallCoreCast()
    {
        int added = PlacedNPCDefaults.EnsureCoreCast();
        if (added > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"Weekend Cast: installed {added} core cast marker(s). They are editable objects under " +
                      $"'{PlacedNPCDefaults.RootName}' — move them, redress them, rewrite them.");
        }
        else Debug.Log("Weekend Cast: the core cast is already in this scene.");
        Repaint();
    }

    void DrawSlotTabs()
    {
        var labels = new string[WeekendSlots.All.Length];
        for (int i = 0; i < labels.Length; i++) labels[i] = WeekendSlots.ShortLabel(WeekendSlots.All[i]);

        EditorGUI.BeginChangeCheck();
        int picked = GUILayout.Toolbar(System.Array.IndexOf(WeekendSlots.All, _slot), labels);
        if (EditorGUI.EndChangeCheck())
        {
            _slot = WeekendSlots.All[Mathf.Clamp(picked, 0, WeekendSlots.All.Length - 1)];
            _selectedActivityId = "";
            Rebuild();
        }

        EditorGUILayout.LabelField(
            $"{WeekendSlots.Label(_slot)}  ·  {WeekendSlots.Clock(WeekendSlots.OpensAt(_slot))}–" +
            $"{WeekendSlots.Clock(WeekendSlots.ClosesAt(_slot))}  ·  previewing as " +
            $"{SessionOf(_slot).ToString().ToUpperInvariant()}", EditorStyles.miniLabel);
    }

    // ------------------------------------------------------------------ what is booked

    void DrawWhatsOn()
    {
        var booked = _timetable != null ? _timetable.InSlot(_slot) : new List<WeekendActivity>();

        EditorGUILayout.LabelField($"Booked this half-day ({booked.Count})", EditorStyles.boldLabel);
        if (booked.Count == 0)
        {
            EditorGUILayout.HelpBox("Nothing on the sheet in this half-day.", MessageType.None);
            return;
        }

        foreach (var a in booked)
        {
            var venue = WeekendVenues.For(a.kind);
            bool selected = a.id == _selectedActivityId;

            using (new EditorGUILayout.HorizontalScope(selected ? "SelectionRect" : GUIStyle.none))
            {
                if (GUILayout.Button($"{a.Clock}   {a.title}", EditorStyles.label, GUILayout.Width(260f)))
                    _selectedActivityId = selected ? "" : a.id;

                EditorGUILayout.LabelField(ActivityKinds.Tag(a.kind), EditorStyles.miniLabel, GUILayout.Width(60f));
                EditorGUILayout.LabelField(venue == WeekendVenue.None ? "on track" : WeekendVenues.ShortLabel(venue),
                                           EditorStyles.miniLabel, GUILayout.Width(95f));
                EditorGUILayout.LabelField(WeekendVenueCast.SpeakerAt(venue), EditorStyles.miniLabel);
            }

            if (selected) DrawActivityDetail(a);
        }
    }

    // What the player will actually be asked, and where the words live.
    void DrawActivityDetail(WeekendActivity a)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(a.subtitle, EditorStyles.wordWrappedMiniLabel);

            if (a.IsOnTrack)
            {
                EditorGUILayout.HelpBox("The player drives this one — it hands off to the race scene.", MessageType.None);
                return;
            }
            if (a.IsSpectate)
            {
                EditorGUILayout.HelpBox("Watched from a grandstand seat. The session itself is simulated " +
                                        "(SeriesSimulator); there is no dialogue to edit.", MessageType.None);
                return;
            }

            var script = ScriptFor(a);
            if (script == null)
            {
                EditorGUILayout.HelpBox("No conversation is wired to this kind yet — add a case to " +
                                        "WeekendScripts.For.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField($"{script.beats.Count} beat(s), scored into " +
                                       $"'{script.statKey}'", EditorStyles.miniBoldLabel);

            foreach (var beat in script.beats)
            {
                EditorGUILayout.LabelField($"{beat.speaker}: \"{beat.line}\"", EditorStyles.wordWrappedMiniLabel);
                foreach (var choice in beat.choices)
                    EditorGUILayout.LabelField($"       → \"{choice.text}\"   {Worth(choice)}",
                                               EditorStyles.wordWrappedMiniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open the words", GUILayout.Width(120f))) OpenContentFile(a);
            }
        }
    }

    // A one-line read of what an answer is worth, so the balance is visible next to the words.
    static string Worth(WeekendChoice c)
    {
        var parts = new List<string>();
        if (c.setup != 0f) parts.Add($"setup {c.setup:+0.00;-0.00}");
        if (c.morale != 0f) parts.Add($"team {c.morale:+0;-0}");
        if (c.sponsor != 0f) parts.Add($"sponsor {c.sponsor:+0;-0}");
        if (c.media != 0f) parts.Add($"press {c.media:+0;-0}");
        if (c.appeal != 0f) parts.Add($"fans {c.appeal:+0.0;-0.0}");
        if (c.money != 0) parts.Add($"${c.money}");
        return parts.Count == 0 ? "" : "(" + string.Join(", ", parts) + ")";
    }

    // The content for a booking, built the way the game builds it. Press content needs the runtime's own
    // facts, so it is asked for through the same seam the venue host uses.
    static WeekendConversation ScriptFor(WeekendActivity a)
    {
        WeekendLedger.Timetable ??= WeekendDirector.Timetable;
        try { return WeekendScripts.For(a); }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Weekend Cast: could not build the conversation for '{a.title}' — {e.Message}");
            return null;
        }
    }

    static void OpenContentFile(WeekendActivity a)
    {
        string file = a.kind switch
        {
            ActivityKind.TeamBriefing or ActivityKind.Debrief => "TeamMeetingContent",
            ActivityKind.Orientation => "OrientationContent",
            ActivityKind.DriversMeeting or ActivityKind.DriverIntros => "CeremonyContent",
            ActivityKind.SponsorDuty or ActivityKind.PhotoShoot => "SponsorContent",
            ActivityKind.Autographs or ActivityKind.HaulerParade => "SigningContent",
            ActivityKind.PressConference or ActivityKind.MediaHit => "PressConferenceContent",
            _ => null,
        };
        if (file == null) return;

        foreach (string guid in AssetDatabase.FindAssets($"{file} t:MonoScript"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith($"/{file}.cs")) continue;
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(path));
            return;
        }
    }

    // ------------------------------------------------------------------ who is out there

    void DrawPaddockCast()
    {
        var all = PlacedNPCSceneContext.AllInScene();
        var session = SessionOf(_slot);

        EditorGUILayout.LabelField("In the paddock this half-day", EditorStyles.boldLabel);

        int shown = 0;
        foreach (var npc in all)
        {
            if (npc == null) continue;
            string unmet = PlacedNPCSceneContext.Evaluate(npc, session);

            using (new EditorGUILayout.HorizontalScope(npc == _selected ? "SelectionRect" : GUIStyle.none))
            {
                var label = new GUIStyle(EditorStyles.label);
                if (unmet != null) label.normal.textColor = new Color(0.55f, 0.55f, 0.55f);

                if (GUILayout.Button(npc.Label, label, GUILayout.Width(170f))) Frame(npc);

                EditorGUILayout.LabelField(npc.interaction.ToString(), EditorStyles.miniLabel, GUILayout.Width(110f));

                var set = npc.ScheduledFor(_slot);
                var live = new GUIStyle(EditorStyles.miniLabel);
                if (set != null) live.normal.textColor = new Color(0.55f, 0.8f, 1f);
                EditorGUILayout.LabelField(set != null ? "says: " + set.label : "says: default",
                                           live, GUILayout.Width(150f));

                EditorGUILayout.LabelField(Summary(npc), EditorStyles.miniLabel);
            }

            if (unmet != null)
                EditorGUILayout.LabelField("        not here: " + unmet, EditorStyles.miniLabel);
            shown++;
        }

        if (shown == 0)
            EditorGUILayout.HelpBox("No placed NPCs in this scene. Draftmaster > NPCs > Add Placed NPC, or open " +
                                    "the track's package to edit the ones that belong to it.", MessageType.None);

        // The people the weekend stands up itself. They have no marker to select because they are built at
        // play time, at whichever venue the booking is kept — but what they say is worth seeing here.
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Stood up by the weekend (built at play time)", EditorStyles.boldLabel);
        foreach (var host in WeekendVenueCast.All)
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(host.speaker, GUILayout.Width(170f));
                EditorGUILayout.LabelField(WeekendVenues.ShortLabel(host.venue), EditorStyles.miniLabel, GUILayout.Width(110f));
                EditorGUILayout.LabelField($"idle: \"{host.idleLine}\"", EditorStyles.miniLabel);
            }
    }

    static string Summary(PlacedNPC npc)
    {
        var parts = new List<string>();
        int lines = npc.lines != null ? npc.lines.Length : 0;
        parts.Add(lines == 1 ? "1 line" : $"{lines} lines");
        if (npc.schedule != null && npc.schedule.Count > 0) parts.Add($"{npc.schedule.Count} scheduled set(s)");
        if (npc.quest != null) parts.Add("quest: " + npc.quest.name);
        if (!string.IsNullOrEmpty(npc.objectiveOnFinish)) parts.Add("objective: " + npc.objectiveOnFinish);
        if (npc.interaction == PlacedNPC.Interaction.WalkUpCutscene && npc.waitForTrigger)
            parts.Add($"trigger r={npc.triggerRadius:0.0}");
        return string.Join("  ·  ", parts);
    }

    void Frame(PlacedNPC npc)
    {
        Select(npc);
        Selection.activeGameObject = npc.gameObject;
        EditorGUIUtility.PingObject(npc.gameObject);
        SceneView.lastActiveSceneView?.Frame(new Bounds(npc.ResolveStandPoint(), Vector3.one * 6f), false);
    }

    void Select(PlacedNPC npc)
    {
        if (_selected == npc) return;
        _selected = npc;
        DestroyInspector();
    }

    void DestroyInspector()
    {
        if (_selectedInspector != null) DestroyImmediate(_selectedInspector);
        _selectedInspector = null;
    }

    // ------------------------------------------------------------------ the selected NPC

    void DrawDetail()
    {
        if (_selected == null)
        {
            EditorGUILayout.HelpBox("Pick somebody above to see and edit what they say.", MessageType.None);
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(_selected.Label, EditorStyles.boldLabel);

            var so = new SerializedObject(_selected);
            so.Update();

            EditorGUILayout.PropertyField(so.FindProperty("speakerName"));

            DrawSlotDialogue(so);

            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(so.FindProperty("lines"),
                                          new GUIContent("Default lines", "Used in any half-day no set covers."), true);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Quest", EditorStyles.miniBoldLabel);
            var quest = so.FindProperty("quest");
            EditorGUILayout.PropertyField(quest);
            if (quest.objectReferenceValue != null)
            {
                EditorGUILayout.PropertyField(so.FindProperty("isDeliveryTarget"));
                EditorGUILayout.PropertyField(so.FindProperty("questOfferLines"), true);
                EditorGUILayout.PropertyField(so.FindProperty("questActiveLines"), true);
                EditorGUILayout.PropertyField(so.FindProperty("questTurnInLines"), true);
                EditorGUILayout.PropertyField(so.FindProperty("questCompletedLines"), true);
                EditorGUILayout.PropertyField(so.FindProperty("questLockedLines"), true);
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("Marker / beat", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("interaction"));
            EditorGUILayout.PropertyField(so.FindProperty("objectiveOnFinish"),
                                          new GUIContent("Objective banner"));
            if (_selected.interaction == PlacedNPC.Interaction.WalkUpCutscene)
            {
                EditorGUILayout.PropertyField(so.FindProperty("waitForTrigger"));
                EditorGUILayout.PropertyField(so.FindProperty("triggerOffset"));
                EditorGUILayout.PropertyField(so.FindProperty("triggerRadius"));
            }

            so.ApplyModifiedProperties();

            EditorGUILayout.Space(2);
            _showFullInspector = EditorGUILayout.Foldout(_showFullInspector, "Everything else (appearance, anchor, body)");
            if (_showFullInspector)
            {
                if (_selectedInspector == null) _selectedInspector = Editor.CreateEditor(_selected);
                _selectedInspector.OnInspectorGUI();
            }
        }
    }

    // What this NPC says in the half-day currently being looked at — the whole point of picking a slot.
    // Either the set that covers it (edited in place) or an offer to write one.
    void DrawSlotDialogue(SerializedObject so)
    {
        var npc = (PlacedNPC)so.targetObject;
        var schedule = so.FindProperty("schedule");

        int index = IndexOfSetCovering(npc, _slot);
        string when = WeekendSlots.ShortLabel(_slot);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"What they say in {when}", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();

                if (index < 0 && GUILayout.Button($"Write a {when} set", GUILayout.Width(120f)))
                {
                    AddSetFor(npc, _slot);
                    return;
                }
                if (index >= 0 && GUILayout.Button("Remove", GUILayout.Width(70f)))
                {
                    Undo.RecordObject(npc, "Remove scheduled dialogue");
                    npc.schedule.RemoveAt(index);
                    EditorUtility.SetDirty(npc);
                    return;
                }
            }

            if (index < 0)
            {
                EditorGUILayout.LabelField("Nothing scheduled — they use their default lines.",
                                           EditorStyles.miniLabel);
                return;
            }

            var set = schedule.GetArrayElementAtIndex(index);
            EditorGUILayout.PropertyField(set.FindPropertyRelative("label"));
            DrawSlotToggles(set);
            EditorGUILayout.PropertyField(set.FindPropertyRelative("lines"), true);
            EditorGUILayout.PropertyField(set.FindPropertyRelative("objectiveOnFinish"),
                                          new GUIContent("Objective banner"));
        }
    }

    // The six half-days as ticks, so one set can cover "all of Friday" or "race day" in a click.
    static void DrawSlotToggles(SerializedProperty set)
    {
        string[] names = { "fridayAM", "fridayPM", "saturdayAM", "saturdayPM", "sundayAM", "sundayPM" };

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Half-days", GUILayout.Width(70f));
            for (int i = 0; i < names.Length; i++)
            {
                var prop = set.FindPropertyRelative(names[i]);
                prop.boolValue = GUILayout.Toggle(prop.boolValue, WeekendSlots.ShortLabel(WeekendSlots.All[i]),
                                                  EditorStyles.miniButton, GUILayout.Width(48f));
            }
        }
    }

    static int IndexOfSetCovering(PlacedNPC npc, WeekendSlot slot)
    {
        if (npc.schedule == null) return -1;
        for (int i = 0; i < npc.schedule.Count; i++)
        {
            var set = npc.schedule[i];
            if (set != null && set.Covers(slot)) return i;
        }
        return -1;
    }

    static void AddSetFor(PlacedNPC npc, WeekendSlot slot)
    {
        Undo.RecordObject(npc, "Add scheduled dialogue");
        npc.schedule ??= new List<PlacedNPC.ScheduledLines>();

        var set = new PlacedNPC.ScheduledLines
        {
            label = WeekendSlots.Label(slot),
            lines = npc.lines != null && npc.lines.Length > 0
                ? (string[])npc.lines.Clone()      // start from what they say now, then rewrite it
                : new[] { "" },
        };
        set.Set(slot, true);
        npc.schedule.Add(set);
        EditorUtility.SetDirty(npc);
    }

    // ------------------------------------------------------------------ slots

    // Which session a half-day is previewed as: the player's own session when one falls in it, otherwise
    // practice, which is the on-foot context the paddock is dressed for.
    RaceWeekend.Session SessionOf(WeekendSlot slot)
    {
        if (_timetable == null) return RaceWeekend.Session.Practice;

        foreach (var a in _timetable.InSlot(slot))
        {
            if (a.kind == ActivityKind.Race) return RaceWeekend.Session.Race;
            if (a.kind == ActivityKind.Qualifying) return RaceWeekend.Session.Qualifying;
            if (a.kind == ActivityKind.Practice) return RaceWeekend.Session.Practice;
        }
        return RaceWeekend.Session.Practice;
    }
}
