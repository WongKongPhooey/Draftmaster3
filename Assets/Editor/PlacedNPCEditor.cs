using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Scene-view authoring for PlacedNPC: draw every placed NPC where they will actually stand, colour them by
// whether they'd appear in the session being previewed, and give the stand point and the trigger ring drag
// handles instead of typed numbers.
//
// Geometry anchors (pit lane, parked car, RV door) resolve against whatever track is in the open scene, so
// this only shows a true position with the track package loaded — either a package preview in the race scene
// (Draftmaster > Tracks > Preview Selected Package In Scene) or the package's own Prefab Mode stage.
[CustomEditor(typeof(PlacedNPC))]
[CanEditMultipleObjects]
public class PlacedNPCEditor : Editor
{
    static readonly Color kAppears = new Color(0.35f, 0.95f, 0.45f);
    static readonly Color kHidden = new Color(0.95f, 0.35f, 0.35f);
    static readonly Color kTrigger = new Color(0.4f, 0.9f, 1f);

    public override void OnInspectorGUI()
    {
        var npc = (PlacedNPC)target;

        DrawConditionSummary(npc);
        EditorGUILayout.Space(4);
        DrawDefaultInspector();

        if (npc.anchor != PlacedNPC.Anchor.Here && !PlacedNPCSceneContext.HasTrack)
        {
            EditorGUILayout.HelpBox(
                "This NPC is anchored to track geometry, but no track is loaded in the open scene, so the " +
                "scene view can only draw the marker itself.\n\n" +
                "Draftmaster > Tracks > Preview Selected Package In Scene to see where they really stand.",
                MessageType.Info);
        }

        if (GUILayout.Button("Open NPC Director")) NPCDirectorWindow.Open();
    }

    // The three-session verdict, plus the reason when they don't show up. This is the thing you actually
    // want to read off an NPC — "will this person be here?" — and it's spread over a dozen fields otherwise.
    void DrawConditionSummary(PlacedNPC npc)
    {
        var box = new GUIStyle(EditorStyles.helpBox) { richText = true, wordWrap = true };
        var sb = new System.Text.StringBuilder();
        sb.Append("<b>Appears in:</b>  ");

        string blockedReason = null;
        foreach (RaceWeekend.Session s in System.Enum.GetValues(typeof(RaceWeekend.Session)))
        {
            string unmet = PlacedNPCSceneContext.Evaluate(npc, s);
            bool ok = unmet == null;
            sb.Append(ok ? "<color=#5CE07A>" : "<color=#888888>");
            sb.Append(ok ? "✔ " : "✘ ");
            sb.Append(s.ToString());
            sb.Append("</color>   ");
            if (!ok && blockedReason == null) blockedReason = unmet;
        }

        sb.Append("\n<b>Rules:</b>  ").Append(npc.appear.Summarise());
        if (blockedReason != null) sb.Append("\n<b>Blocked by:</b>  ").Append(blockedReason);
        sb.Append($"\n<b>Preview:</b>  {PlacedNPCSceneContext.PreviewTrack} · series \"{PlacedNPCSceneContext.PreviewSeries}\"");

        EditorGUILayout.LabelField(sb.ToString(), box);
    }

    // ---------------------------------------------------------------- scene view

    void OnSceneGUI()
    {
        var npc = (PlacedNPC)target;
        PlacedNPCSceneContext.Apply(npc);

        Vector3 stand = npc.ResolveStandPoint();
        bool appears = PlacedNPCSceneContext.Evaluate(npc, PlacedNPCSceneContext.PreviewSession) == null;
        Handles.color = appears ? kAppears : kHidden;

        // Stand point. A marker placed by hand moves with its own transform; a geometry-anchored one is
        // dragged along the anchor's own axes, which writes back to anchorAlong / anchorLateral.
        if (npc.anchor != PlacedNPC.Anchor.Here)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.PositionHandle(stand, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(npc, "Move Placed NPC");
                ApplyDrag(npc, moved - stand, ref npc.anchorAlong, ref npc.anchorLateral);
                EditorUtility.SetDirty(npc);
            }
            // Tie the body back to the marker so it's obvious which icon in the hierarchy this is.
            Handles.color = new Color(1f, 1f, 1f, 0.25f);
            Handles.DrawDottedLine(npc.transform.position, stand, 3f);
            Handles.color = appears ? kAppears : kHidden;
        }

        Handles.DrawWireDisc(stand, Vector3.forward, npc.interactRange);
        Handles.Label(stand + Vector3.up * 0.9f, $"{npc.Label}\n{npc.appear.Summarise()}", LabelStyle(appears));

        if (npc.applyFacing)
        {
            Vector3 dir = new Vector3(Mathf.Cos(npc.facingDeg * Mathf.Deg2Rad), Mathf.Sin(npc.facingDeg * Mathf.Deg2Rad), 0f);
            EditorGUI.BeginChangeCheck();
            Vector3 tip = Handles.FreeMoveHandle(stand + dir * 1.5f, 0.12f, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(npc, "Face Placed NPC");
                Vector2 d = tip - stand;
                if (d.sqrMagnitude > 0.001f) npc.facingDeg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                EditorUtility.SetDirty(npc);
            }
            Handles.DrawLine(stand, stand + dir * 1.5f);
        }

        if (npc.interaction != PlacedNPC.Interaction.WalkUpCutscene) return;

        // Walk-up cutscene: the ring the player has to step into, and how close he gets before talking.
        Vector3 trigger = npc.ResolveTriggerPoint();
        Handles.color = kTrigger;
        Handles.DrawWireDisc(trigger, Vector3.forward, npc.triggerRadius);
        Handles.DrawDottedLine(stand, trigger, 2f);
        Handles.Label(trigger, npc.waitForTrigger ? "walk-over trigger" : "trigger (unused: plays on open)");

        if (npc.waitForTrigger)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 movedTrigger = Handles.PositionHandle(trigger, Quaternion.identity);
            float radius = Handles.RadiusHandle(Quaternion.identity, trigger, npc.triggerRadius);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(npc, "Move Cutscene Trigger");
                float along = npc.triggerOffset.x, lateral = npc.triggerOffset.y;
                ApplyDrag(npc, movedTrigger - trigger, ref along, ref lateral);
                npc.triggerOffset = new Vector2(along, lateral);
                npc.triggerRadius = Mathf.Max(0.1f, radius);
                EditorUtility.SetDirty(npc);
            }
        }

        Handles.color = new Color(1f, 0.85f, 0.3f, 0.8f);
        Handles.DrawWireDisc(stand, Vector3.forward, npc.stopDistance);
    }

    // Convert a world drag into the anchor's own (along, lateral) axes. The axes are measured off the anchor
    // itself by nudging one metre each way, so this works the same for a pit-lane curve, the parked car and
    // the RV door without any of them needing to hand out a basis.
    static void ApplyDrag(PlacedNPC npc, Vector3 delta, ref float along, ref float lateral)
    {
        if (npc.anchor == PlacedNPC.Anchor.Here)
        {
            along += delta.x;
            lateral += delta.y;
            return;
        }

        Vector3 origin = npc.ResolveOffset(along, lateral);
        Vector3 fwd = npc.ResolveOffset(along + 1f, lateral) - origin;
        Vector3 side = npc.ResolveOffset(along, lateral + 1f) - origin;
        if (fwd.sqrMagnitude < 1e-5f || side.sqrMagnitude < 1e-5f) return;

        along += Vector3.Dot(delta, fwd.normalized);
        lateral += Vector3.Dot(delta, side.normalized);
    }

    static GUIStyle _label;
    static GUIStyle LabelStyle(bool appears)
    {
        if (_label == null) _label = new GUIStyle(EditorStyles.miniLabel) { richText = false, alignment = TextAnchor.UpperCenter };
        _label.normal.textColor = appears ? kAppears : kHidden;
        return _label;
    }

    // Every placed NPC draws itself all the time, selected or not — that's the point of the system: open the
    // scene and see the cast.
    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
    static void DrawMarker(PlacedNPC npc, GizmoType type)
    {
        PlacedNPCSceneContext.Apply(npc);
        bool appears = PlacedNPCSceneContext.Evaluate(npc, PlacedNPCSceneContext.PreviewSession) == null;

        Vector3 stand = npc.ResolveStandPoint();
        Gizmos.color = appears ? kAppears : new Color(kHidden.r, kHidden.g, kHidden.b, 0.4f);
        // A person-sized capsule, so the scene reads as people rather than as points.
        Gizmos.DrawWireSphere(stand + Vector3.up * 0.55f, 0.18f);
        Gizmos.DrawLine(stand + Vector3.up * 0.37f, stand);
        Gizmos.DrawLine(stand + Vector3.up * 0.3f, stand + new Vector3(-0.22f, 0.12f, 0f));
        Gizmos.DrawLine(stand + Vector3.up * 0.3f, stand + new Vector3(0.22f, 0.12f, 0f));

        if ((type & GizmoType.Selected) == 0)
            Handles.Label(stand + Vector3.up * 0.9f, npc.Label, LabelStyle(appears));
    }
}

// Shared edit-time state for everything that draws or lists placed NPCs: which session/track/series is being
// previewed, and a build context so geometry anchors resolve against the track in the open scene.
public static class PlacedNPCSceneContext
{
    const string SessionKey = "draftmaster.npcpreview.session";
    const string TrackKey = "draftmaster.npcpreview.track";
    const string SeriesKey = "draftmaster.npcpreview.series";

    public static RaceWeekend.Session PreviewSession
    {
        get => (RaceWeekend.Session)EditorPrefs.GetInt(SessionKey, 0);
        set { EditorPrefs.SetInt(SessionKey, (int)value); SceneView.RepaintAll(); }
    }

    public static string PreviewTrack
    {
        get
        {
            string s = EditorPrefs.GetString(TrackKey, "");
            return string.IsNullOrEmpty(s) ? TrackSelection.CurrentId : s;
        }
        set { EditorPrefs.SetString(TrackKey, value ?? ""); SceneView.RepaintAll(); }
    }

    public static string PreviewSeries
    {
        get => EditorPrefs.GetString(SeriesKey, "");
        set { EditorPrefs.SetString(SeriesKey, value ?? ""); SceneView.RepaintAll(); }
    }

    public static bool HasTrack => Context().track != null;

    // Would this NPC appear in that session, under the current preview track/series? Returns null when they
    // would, otherwise the clause that stops them.
    public static string Evaluate(PlacedNPC npc, RaceWeekend.Session session)
    {
        if (npc == null) return "no npc";
        var previous = AppearanceConditions.Preview;
        AppearanceConditions.Preview = new AppearanceConditions.PreviewContext
        {
            session = session,
            trackId = PreviewTrack,
            series = PreviewSeries,
            ignoreSeen = true,   // a preview is about the authored rule, not about this save's history
            ignoreChance = true,
        };
        try { return npc.appear.FirstUnmet(); }
        finally { AppearanceConditions.Preview = previous; }
    }

    // Give an NPC the edit-time geometry so its anchors resolve for drawing.
    public static void Apply(PlacedNPC npc)
    {
        if (npc != null && !Application.isPlaying) npc.SetContext(Context());
    }

    static PlacedNPC.BuildContext _ctx;
    static double _built = -1;

    // Rebuilt at most a few times a second — sampling the pit spline on every gizmo draw for every NPC would
    // make the scene view crawl.
    static PlacedNPC.BuildContext Context()
    {
        if (EditorApplication.timeSinceStartup - _built < 0.5) return _ctx;
        _built = EditorApplication.timeSinceStartup;
        _ctx = Build();
        return _ctx;
    }

    static PlacedNPC.BuildContext Build()
    {
        var ctx = new PlacedNPC.BuildContext();

        var flow = Find<PitLaneStart>();
        var track = flow != null && flow.track != null ? flow.track : Find<TrackBuilder>();
        if (flow != null) ctx.prefab = flow.onFootPrefab;
        if (flow != null && flow.car != null) ctx.car = flow.car.transform;
        else
        {
            var pvc = Find<PlayerVehicleController>();
            if (pvc != null) ctx.car = pvc.transform;
        }

        ctx.rv = Find<RVExterior>();
        ctx.rvInterior = Find<RVInterior>();
        ctx.groundZ = ctx.rv != null ? ctx.rv.transform.position.z : 0f;

        if (track == null) return ctx;
        ctx.track = track;

        var samples = track.SamplePitCenterline();
        ctx.usedPit = samples != null && samples.Count >= 2;
        if (!ctx.usedPit) samples = track.SampleCenterline();
        if (samples == null || samples.Count < 2) return ctx;
        ctx.pitSamples = samples;

        float total = samples[samples.Count - 1].distance;
        float fraction = flow != null ? flow.pitFraction : 0.5f;
        ctx.playerPitDistance = total * fraction;

        // Where the player spawns, so PlayerSpawn/RVDoor anchors have an origin before play mode.
        var spawn = Find<PlayerSpawnPoint>();
        if (spawn != null) ctx.playerSpawnPos = spawn.transform.position;
        else
        {
            var mid = ctx.usedPit ? track.SamplePitAt(ctx.playerPitDistance, samples) : track.SampleAt(ctx.playerPitDistance, samples);
            float lateral = flow != null ? flow.lateralOffsetMetres : -3f;
            Vector2 off = mid.position + mid.normal * lateral;
            ctx.playerSpawnPos = track.transform.TransformPoint(new Vector3(off.x, off.y, 0f));
        }

        return ctx;
    }

    // Find a component in the open scene OR in the prefab stage, so authoring inside a track package works
    // the same as authoring in the race scene.
    public static T Find<T>() where T : Component
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            var inStage = stage.prefabContentsRoot.GetComponentInChildren<T>(true);
            if (inStage != null) return inStage;
        }
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }

    // Every placed NPC in the open scene or prefab stage.
    public static List<PlacedNPC> AllInScene()
    {
        var list = new List<PlacedNPC>();
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null) list.AddRange(stage.prefabContentsRoot.GetComponentsInChildren<PlacedNPC>(true));
        foreach (var npc in Object.FindObjectsByType<PlacedNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (!list.Contains(npc)) list.Add(npc); // a prefab stage can show up in both sweeps
        return list;
    }
}
