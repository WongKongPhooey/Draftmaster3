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

    // Everything you need to know about this person, before the raw fields underneath: which half-days
    // they are here for, where they stand, how you meet them, what they hand out and what they say.
    //
    // The fields below are the authoring surface; this is the read-out — written for the moment you click
    // somebody in a scene and want to know what they are FOR.
    void DrawConditionSummary(PlacedNPC npc)
    {
        var box = new GUIStyle(EditorStyles.helpBox) { richText = true, wordWrap = true };
        var slot = PlacedNPCSceneContext.PreviewSlot;
        var sb = new System.Text.StringBuilder();

        // --- who -------------------------------------------------------------------------------------
        sb.Append("<b><size=13>").Append(string.IsNullOrEmpty(npc.speakerName) ? npc.name : npc.speakerName)
          .Append("</size></b>   <color=#888888>").Append(npc.role.ToString()).Append("</color>");

        // --- when ------------------------------------------------------------------------------------
        sb.AppendLine().AppendLine().Append("<b>Here on:</b>  ");
        foreach (var s in Draftmaster.Weekend.WeekendSlots.All)
        {
            bool ok = PlacedNPCSceneContext.Evaluate(npc, s) == null;
            bool now = s == slot;
            sb.Append(ok ? "<color=#5CE07A>" : "<color=#777777>");
            if (now) sb.Append("<b>");
            sb.Append(ok ? Tick : Cross).Append(' ').Append(Draftmaster.Weekend.WeekendSlots.ShortLabel(s));
            if (now) sb.Append("</b>");
            sb.Append("</color>   ");
        }

        string unmet = PlacedNPCSceneContext.Evaluate(npc, slot);
        sb.AppendLine().Append("<b>").Append(Draftmaster.Weekend.WeekendSlots.Label(slot)).Append(":</b>  ")
          .Append(unmet == null ? "<color=#5CE07A>here</color>"
                                : "<color=#E08A5C>not here — " + unmet + "</color>");
        sb.AppendLine().Append("<b>Rules:</b>  ").Append(npc.appear.Summarise());

        // --- where -----------------------------------------------------------------------------------
        sb.AppendLine().AppendLine().Append("<b>Stands:</b>  ").Append(
            npc.anchor == PlacedNPC.Anchor.Here
                ? "where this object is"
                : $"{npc.anchorAlong:0.#} m along, {npc.anchorLateral:0.#} m across, from the {Where(npc.anchor)}");

        string trouble = AnchorTrouble(npc);
        if (trouble != null) sb.AppendLine().Append("<color=#E08A5C>").Append(trouble).Append("</color>");

        // --- how you meet them -----------------------------------------------------------------------
        sb.AppendLine().Append("<b>Meeting:</b>  ").Append(npc.interaction switch
        {
            PlacedNPC.Interaction.TalkOnInteract =>
                $"walk up and press the action button (within {npc.interactRange:0.#} m)",
            PlacedNPC.Interaction.WalkUpCutscene => npc.waitForTrigger
                ? $"they walk over when the player crosses their trigger ({npc.triggerRadius:0.#} m ring), " +
                  $"stopping {npc.stopDistance:0.#} m away"
                : "they walk over as the scene opens",
            PlacedNPC.Interaction.OnCarEntry =>
                "the scene flow starts them — the crew chief's briefing, when the player gets in the car",
            _ => "set dressing — no dialogue",
        });

        // --- what they hand out ----------------------------------------------------------------------
        sb.AppendLine().AppendLine().Append("<b>Quest:</b>  ");
        if (npc.quest == null) sb.Append("<color=#888888>none</color>");
        else
        {
            sb.Append("<b>").Append(npc.quest.title).Append("</b> (").Append(npc.quest.id).Append(") — ")
              .Append(npc.isDeliveryTarget ? "they are the DELIVERY TARGET" : "they give it out");
            sb.AppendLine().Append("<b>Objective:</b>  ").Append(DescribeObjective(npc.quest));
            if (!string.IsNullOrEmpty(npc.grantItemOnAccept))
                sb.AppendLine().Append("<b>Hands over on accept:</b>  ").Append(npc.grantItemOnAccept);
            if (!string.IsNullOrEmpty(npc.quest.rewardItemId))
                sb.AppendLine().Append("<b>Reward:</b>  ").Append(npc.quest.rewardItemId);
        }

        if (npc.givesTheDaysObjective)
            sb.AppendLine().Append("<color=#E0C15C><b>Hands the player their day.</b></color>  Until this " +
                                   "conversation happens the weekend books nothing and the objective strip " +
                                   "stays empty.");

        // --- what they say ---------------------------------------------------------------------------
        var set = npc.ScheduledFor(slot);
        sb.AppendLine().AppendLine().Append("<b>Says</b> (")
          .Append(npc.linesFromTheWeekendSheet ? "read off the weekend's sheet"
                : set != null ? "script: " + set.label
                : "default lines").Append("):");

        if (npc.linesFromTheWeekendSheet)
            sb.AppendLine().Append("   <color=#888888><i>written when they appear, so they can name the " +
                                   "booking that is actually next</i></color>");
        else
        {
            var lines = npc.LinesFor(slot);
            if (lines == null || lines.Length == 0) sb.Append("  <color=#888888>nothing</color>");
            else
            {
                // The player's own replies are marked #player in the line, and read as the other half of a
                // conversation rather than as this NPC talking.
                for (int i = 0; i < lines.Length && i < 4; i++)
                {
                    bool player = lines[i].Contains("#player");
                    sb.AppendLine().Append(player ? "   <color=#7FB2E0>you: " : "   <color=#CFCFCF>")
                      .Append(lines[i].Replace("#player", "").Trim()).Append("</color>");
                }
                if (lines.Length > 4)
                    sb.AppendLine().Append("   <color=#888888>… and ").Append(lines.Length - 4)
                      .Append(" more</color>");
            }
        }

        EditorGUILayout.LabelField(sb.ToString(), box);
    }

    const string Tick = "✔";
    const string Cross = "✘";

    // Why an anchored NPC is not where you expect — the two failures that look identical in the scene view
    // and feel like a bug: nothing to anchor to, and an offset that has run off the end of the pit lane.
    static string AnchorTrouble(PlacedNPC npc)
    {
        if (npc.anchor == PlacedNPC.Anchor.Here) return null;

        Vector3 stand = npc.ResolveStandPoint();
        if ((stand - npc.transform.position).sqrMagnitude < 1e-6f)
            return "The geometry this anchor reads from is not in the open scene, so they are drawn at the " +
                   "marker. Open a scene with the track in it (or preview the package) to place them.";

        Vector3 f = npc.ResolveOffset(npc.anchorAlong + 1f, npc.anchorLateral) - stand;
        Vector3 b = npc.ResolveOffset(npc.anchorAlong - 1f, npc.anchorLateral) - stand;
        if (f.sqrMagnitude < 1e-5f && b.sqrMagnitude < 1e-5f)
            return "Pinned: 'along' has run past the end of the pit lane and is being clamped, so moving it " +
                   "further changes nothing. Bring it back toward the middle of the lane.";

        return null;
    }

    static string Where(PlacedNPC.Anchor anchor) => anchor switch
    {
        PlacedNPC.Anchor.PitLane => "player's pit-lane spawn",
        PlacedNPC.Anchor.ParkedCar => "player's parked car",
        PlacedNPC.Anchor.RVDoor => "motorhome door",
        PlacedNPC.Anchor.PlayerSpawn => "player's spawn point",
        _ => "marker",
    };

    // One line saying what finishing this quest actually takes.
    static string DescribeObjective(QuestInfo q)
    {
        if (q == null) return "";
        return q.objective switch
        {
            QuestInfo.ObjectiveType.BeatDriverInRace =>
                $"finish ahead of {q.driverName}" + (q.singleRaceAttempt ? ", in one race" : ""),
            QuestInfo.ObjectiveType.FinishRacePosition => $"finish {q.targetPosition} or better",
            QuestInfo.ObjectiveType.StatThreshold =>
                $"get '{q.statKey}' to {q.statTarget}" + (q.countFromAccept ? ", counted from accepting" : ""),
            QuestInfo.ObjectiveType.DeliverItem => $"deliver '{q.itemId}' to the NPC marked as the target",
            QuestInfo.ObjectiveType.RelationshipBelow =>
                $"drive {q.driverName}'s opinion down to {q.relationshipTarget}",
            QuestInfo.ObjectiveType.RelationshipAbove =>
                $"bring {q.driverName}'s opinion up to {q.relationshipTarget}",
            QuestInfo.ObjectiveType.ContactDriver =>
                $"hit {q.driverName} at severity {q.minContactSeverity:0.##} or more",
            _ => q.objective.ToString(),
        };
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

        // Probe backwards when forwards has nowhere to go. A pit-lane anchor CLAMPS to the ends of the
        // spline, so an NPC whose `along` has run past either end sits in a dead zone where stepping one
        // metre further changes nothing — and the drag used to give up there, which reads in the scene view
        // as an NPC welded to the pit entry that no amount of dragging will move.
        if (fwd.sqrMagnitude < 1e-5f) fwd = origin - npc.ResolveOffset(along - 1f, lateral);
        if (side.sqrMagnitude < 1e-5f) side = origin - npc.ResolveOffset(along, lateral - 1f);

        // Still nothing: the anchor's geometry is missing from the scene entirely. Fall back to world axes
        // so the handle always writes something rather than silently doing nothing.
        if (fwd.sqrMagnitude < 1e-5f) fwd = Vector3.right;
        if (side.sqrMagnitude < 1e-5f) side = Vector3.up;

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
    const string SlotKey = "draftmaster.npcpreview.slot";
    const string SessionKey = "draftmaster.npcpreview.session";
    const string TrackKey = "draftmaster.npcpreview.track";
    const string SeriesKey = "draftmaster.npcpreview.series";

    // The half-day being previewed: the day of the week and which half of it. This is the main axis now —
    // "who is in the paddock on Saturday morning" is the question an author actually has.
    public static Draftmaster.Weekend.WeekendSlot PreviewSlot
    {
        get => (Draftmaster.Weekend.WeekendSlot)EditorPrefs.GetInt(SlotKey, 0);
        set { EditorPrefs.SetInt(SlotKey, (int)value); SceneView.RepaintAll(); }
    }

    // Which session that half-day is: the weekend runs practice on Friday, qualifying on Saturday and the
    // race on Sunday, so the day picks the session and there is no second control to keep in step. An NPC
    // gated to qualifying therefore shows up under Saturday, which is where the player would meet them.
    public static RaceWeekend.Session SessionFor(Draftmaster.Weekend.WeekendSlot slot) => slot switch
    {
        Draftmaster.Weekend.WeekendSlot.FridayAM or Draftmaster.Weekend.WeekendSlot.FridayPM
            => RaceWeekend.Session.Practice,
        Draftmaster.Weekend.WeekendSlot.SaturdayAM or Draftmaster.Weekend.WeekendSlot.SaturdayPM
            => RaceWeekend.Session.Qualifying,
        _ => RaceWeekend.Session.Race,
    };

    // The session that half-day is played in. Stored rather than derived: the Weekend Cast window knows
    // the real answer (it has the timetable in front of it and can see that Saturday afternoon is the
    // National race), and the Director sets the rule-of-thumb above when it has no sheet to read.
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
        => Evaluate(npc, PreviewSlot, session);

    // Would this NPC be there on that half-day? Null when they would, otherwise the clause that stops them.
    public static string Evaluate(PlacedNPC npc, Draftmaster.Weekend.WeekendSlot slot)
        => Evaluate(npc, slot, PreviewSession);

    static string Evaluate(PlacedNPC npc, Draftmaster.Weekend.WeekendSlot slot, RaceWeekend.Session session)
    {
        if (npc == null) return "no npc";
        var previous = AppearanceConditions.Preview;
        AppearanceConditions.Preview = new AppearanceConditions.PreviewContext
        {
            session = session,
            slot = slot,
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

        // Where the car will be PARKED once the scene runs, which is not where it is sitting in the scene
        // file: PitLaneStart puts it carAheadMetres up the pit lane from the player's spawn. The crew chief
        // is anchored to the car, so without this he previews next to a car that is about to drive away —
        // typically pinned against the pit entry, metres from where he actually ends up.
        if (flow != null)
        {
            float carDistance = Mathf.Min(total, total * fraction + flow.carAheadMetres);
            var carSample = ctx.usedPit ? track.SamplePitAt(carDistance, samples) : track.SampleAt(carDistance, samples);
            Vector2 carOff = carSample.position + carSample.normal * flow.lateralOffsetMetres;
            ctx.parkedCarPos = track.transform.TransformPoint(new Vector3(carOff.x, carOff.y, 0f));
            ctx.hasParkedCarPos = true;
        }

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
