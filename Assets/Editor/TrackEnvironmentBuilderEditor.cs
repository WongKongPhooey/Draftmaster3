using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrackEnvironmentBuilder))]
public class TrackEnvironmentBuilderEditor : Editor
{
    TrackEnvironmentBuilder _builder;

    // New-section pickers.
    TrackEnvironment.BarrierSide _side = TrackEnvironment.BarrierSide.Outer;
    int _startSeg;
    TrackEnvironment.SegmentEnd _startEnd = TrackEnvironment.SegmentEnd.Start;
    int _endSeg;
    TrackEnvironment.SegmentEnd _endEnd = TrackEnvironment.SegmentEnd.Start;

    void OnEnable() { _builder = (TrackEnvironmentBuilder)target; }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var env = _builder.environment;
        if (env == null) return;

        int segCount = (_builder.track != null && _builder.track.track != null && _builder.track.track.segments != null)
            ? _builder.track.track.segments.Length : 0;
        int maxSeg = Mathf.Max(0, segCount - 1);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("New Manual Barrier", EditorStyles.boldLabel);
        _side = (TrackEnvironment.BarrierSide)EditorGUILayout.EnumPopup("Side", _side);

        EditorGUILayout.LabelField("Start anchor");
        EditorGUI.indentLevel++;
        _startSeg = EditorGUILayout.IntSlider("Segment", _startSeg, 0, maxSeg);
        _startEnd = (TrackEnvironment.SegmentEnd)EditorGUILayout.EnumPopup("End", _startEnd);
        EditorGUI.indentLevel--;

        EditorGUILayout.LabelField("End anchor");
        EditorGUI.indentLevel++;
        _endSeg = EditorGUILayout.IntSlider("Segment", _endSeg, 0, maxSeg);
        _endEnd = (TrackEnvironment.SegmentEnd)EditorGUILayout.EnumPopup("End", _endEnd);
        EditorGUI.indentLevel--;

        if (GUILayout.Button("Create Manual Barrier"))
        {
            var list = new List<TrackEnvironment.ManualBarrierSection>(env.manualSections ?? new TrackEnvironment.ManualBarrierSection[0]);
            list.Add(new TrackEnvironment.ManualBarrierSection
            {
                label = $"{_side} {_startSeg}{Short(_startEnd)}→{_endSeg}{Short(_endEnd)}",
                side = _side,
                startSegmentIndex = _startSeg,
                startEnd = _startEnd,
                endSegmentIndex = _endSeg,
                endEnd = _endEnd,
                manualPoints = new Vector2[0],
            });
            Undo.RecordObject(env, "Create Manual Barrier");
            env.manualSections = list.ToArray();
            _builder.editManualSectionIndex = list.Count - 1;
            EditorUtility.SetDirty(env);
            _builder.Build();
            SceneView.RepaintAll();
        }

        // Existing sections.
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Manual Barriers", EditorStyles.boldLabel);
        var sections = env.manualSections;
        if (sections == null || sections.Length == 0)
        {
            EditorGUILayout.HelpBox("None yet. Pick anchors above and Create Manual Barrier.", MessageType.None);
        }
        else
        {
            for (int i = 0; i < sections.Length; i++)
            {
                var s = sections[i];
                EditorGUILayout.BeginHorizontal();
                bool active = _builder.editManualSectionIndex == i;
                string title = string.IsNullOrEmpty(s.label) ? $"{s.side} {s.startSegmentIndex}→{s.endSegmentIndex}" : s.label;
                EditorGUILayout.LabelField($"{(active ? "▶ " : "")}{i}: {title} ({(s.manualPoints != null ? s.manualPoints.Length : 0)} pts)");
                if (GUILayout.Button(active ? "Editing" : "Edit", GUILayout.Width(64)))
                {
                    _builder.editManualSectionIndex = active ? -1 : i;
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    var list = new List<TrackEnvironment.ManualBarrierSection>(env.manualSections);
                    Undo.RecordObject(env, "Delete Manual Barrier");
                    list.RemoveAt(i);
                    env.manualSections = list.ToArray();
                    if (_builder.editManualSectionIndex >= env.manualSections.Length) _builder.editManualSectionIndex = -1;
                    EditorUtility.SetDirty(env);
                    _builder.Build();
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.HelpBox(
            "Editing the active manual barrier (▶) in the Scene view:\n" +
            "• Click empty space = add a point (appended in order between the anchors)\n" +
            "• Drag a point = move it\n" +
            "• Shift+Click a point = delete it\n" +
            "Green spheres are the fixed start/end anchors.",
            MessageType.None);
    }

    static string Short(TrackEnvironment.SegmentEnd e) => e == TrackEnvironment.SegmentEnd.Start ? "s" : "e";

    void OnSceneGUI()
    {
        var env = _builder.environment;
        if (env == null || env.manualSections == null) return;
        int idx = _builder.editManualSectionIndex;
        if (idx < 0 || idx >= env.manualSections.Length) return;
        if (_builder.track == null) return;

        var section = env.manualSections[idx];
        var pts = section.manualPoints != null ? new List<Vector2>(section.manualPoints) : new List<Vector2>();
        Transform tf = _builder.track.transform;
        Event e = Event.current;
        bool changed = false;

        bool haveAnchors = _builder.TryGetManualAnchors(idx, out Vector2 startLocal, out Vector2 endLocal);
        Vector3 startWorld = tf.TransformPoint(new Vector3(startLocal.x, startLocal.y, 0f));
        Vector3 endWorld = tf.TransformPoint(new Vector3(endLocal.x, endLocal.y, 0f));

        // Fixed anchors.
        if (haveAnchors)
        {
            Handles.color = Color.green;
            Handles.SphereHandleCap(0, startWorld, Quaternion.identity, HandleUtility.GetHandleSize(startWorld) * 0.16f, EventType.Repaint);
            Handles.SphereHandleCap(0, endWorld, Quaternion.identity, HandleUtility.GetHandleSize(endWorld) * 0.16f, EventType.Repaint);
            Handles.Label(startWorld, " start");
            Handles.Label(endWorld, " end");
        }

        // Movable / deletable user points. Track if the mouse-down landed on one so a plain click there
        // doesn't also add a new point.
        bool onExistingPoint = false;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 world = tf.TransformPoint(new Vector3(pts[i].x, pts[i].y, 0f));
            float size = HandleUtility.GetHandleSize(world) * 0.12f;
            if (HandleUtility.DistanceToCircle(world, size) < 10f) onExistingPoint = true;

            if (e.shift && e.type == EventType.MouseDown && e.button == 0 &&
                HandleUtility.DistanceToCircle(world, size) < 10f)
            {
                Undo.RecordObject(env, "Delete Barrier Point");
                pts.RemoveAt(i);
                changed = true;
                e.Use();
                break;
            }

            EditorGUI.BeginChangeCheck();
            Handles.color = Color.cyan;
            Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(env, "Move Barrier Point");
                Vector3 local = tf.InverseTransformPoint(moved);
                pts[i] = new Vector2(local.x, local.y);
                changed = true;
            }
            Handles.Label(world, $" {i}");
        }

        // Polyline preview: startAnchor → points → endAnchor.
        Handles.color = Color.yellow;
        Vector3 prev = startWorld;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 w = tf.TransformPoint(new Vector3(pts[i].x, pts[i].y, 0f));
            if (haveAnchors || i > 0) Handles.DrawLine(prev, w);
            prev = w;
        }
        if (haveAnchors) Handles.DrawLine(prev, endWorld);

        // Plain left-click in empty space adds a point.
        if (!e.shift && !e.alt && e.type == EventType.MouseDown && e.button == 0 && !onExistingPoint)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane plane = new Plane(Vector3.forward, Vector3.zero);
            if (plane.Raycast(ray, out float dist))
            {
                Vector3 hit = ray.GetPoint(dist);
                Vector3 local = tf.InverseTransformPoint(hit);
                Undo.RecordObject(env, "Add Barrier Point");
                pts.Add(new Vector2(local.x, local.y));
                changed = true;
                e.Use();
            }
        }

        if (changed)
        {
            section.manualPoints = pts.ToArray();
            env.manualSections[idx] = section;
            EditorUtility.SetDirty(env);
            _builder.Build();
        }

        // Take control so plain clicks don't deselect the builder while editing.
        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
    }
}
