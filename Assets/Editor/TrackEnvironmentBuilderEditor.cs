using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrackEnvironmentBuilder))]
public class TrackEnvironmentBuilderEditor : Editor
{
    TrackEnvironmentBuilder _builder;

    // Manual-barrier pickers.
    TrackEnvironment.BarrierSide _side = TrackEnvironment.BarrierSide.Outer;
    int _startSeg;
    TrackEnvironment.SegmentEnd _startEnd = TrackEnvironment.SegmentEnd.Start;
    int _endSeg;
    TrackEnvironment.SegmentEnd _endEnd = TrackEnvironment.SegmentEnd.Start;

    // Runoff picker.
    TrackEnvironment.SurfaceType _runoffSurface = TrackEnvironment.SurfaceType.Grass;

    // Kerb pickers.
    TrackEnvironment.BarrierSide _kerbSide = TrackEnvironment.BarrierSide.Outer;
    int _kerbStartSeg;
    float _kerbStartDist;
    float _kerbLength = 20f; // metres along the spline; may run past the start segment's end
    float _kerbWidth = 2f;

    void OnEnable() { _builder = (TrackEnvironmentBuilder)target; }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var env = _builder.environment;
        if (env == null) return;

        int segCount = (_builder.track != null && _builder.track.track != null && _builder.track.track.segments != null)
            ? _builder.track.track.segments.Length : 0;
        int maxSeg = Mathf.Max(0, segCount - 1);

        DrawManualBarrierSection(env, maxSeg);
        EditorGUILayout.Space();
        DrawRunoffSection(env);
        EditorGUILayout.Space();
        DrawKerbSection(env, maxSeg);
    }

    // ---------------------------------------------------------------- Manual barriers

    void DrawManualBarrierSection(TrackEnvironment env, int maxSeg)
    {
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
            SetBarrierEdit(list.Count - 1);
            EditorUtility.SetDirty(env);
            _builder.Build();
            SceneView.RepaintAll();
        }

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
                    SetBarrierEdit(active ? -1 : i);
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
            "Editing the active barrier (▶) in the Scene view:\n" +
            "• Click empty space = add a point (between the anchors)\n" +
            "• Drag a point = move • Shift+Click a point = delete\n" +
            "Green spheres are the fixed start/end anchors.",
            MessageType.None);
    }

    // ---------------------------------------------------------------- Runoff areas

    void DrawRunoffSection(TrackEnvironment env)
    {
        EditorGUILayout.LabelField("New Runoff Area", EditorStyles.boldLabel);
        _runoffSurface = (TrackEnvironment.SurfaceType)EditorGUILayout.EnumPopup("Surface", _runoffSurface);

        if (GUILayout.Button("Create Runoff Area"))
        {
            var list = new List<TrackEnvironment.RunoffArea>(env.runoffAreas ?? new TrackEnvironment.RunoffArea[0]);
            list.Add(new TrackEnvironment.RunoffArea
            {
                label = $"{_runoffSurface} {list.Count}",
                surface = _runoffSurface,
                points = new Vector2[0],
            });
            Undo.RecordObject(env, "Create Runoff Area");
            env.runoffAreas = list.ToArray();
            SetRunoffEdit(list.Count - 1);
            EditorUtility.SetDirty(env);
            _builder.Build();
            SceneView.RepaintAll();
        }

        EditorGUILayout.LabelField("Runoff Areas", EditorStyles.boldLabel);
        var areas = env.runoffAreas;
        if (areas == null || areas.Length == 0)
        {
            EditorGUILayout.HelpBox("None yet. Pick a surface and Create Runoff Area, then click in the Scene to draw the polygon.", MessageType.None);
        }
        else
        {
            for (int i = 0; i < areas.Length; i++)
            {
                var a = areas[i];
                EditorGUILayout.BeginHorizontal();
                bool active = _builder.editRunoffIndex == i;
                string title = string.IsNullOrEmpty(a.label) ? a.surface.ToString() : a.label;
                EditorGUILayout.LabelField($"{(active ? "▶ " : "")}{i}: {title} ({(a.points != null ? a.points.Length : 0)} pts)");
                if (GUILayout.Button(active ? "Editing" : "Edit", GUILayout.Width(64)))
                {
                    SetRunoffEdit(active ? -1 : i);
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    var list = new List<TrackEnvironment.RunoffArea>(env.runoffAreas);
                    Undo.RecordObject(env, "Delete Runoff Area");
                    list.RemoveAt(i);
                    env.runoffAreas = list.ToArray();
                    if (_builder.editRunoffIndex >= env.runoffAreas.Length) _builder.editRunoffIndex = -1;
                    EditorUtility.SetDirty(env);
                    _builder.Build();
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.HelpBox(
            "Editing the active runoff (▶) in the Scene view:\n" +
            "• Click empty space = add a polygon point (in order)\n" +
            "• Drag a point = move • Shift+Click a point = delete\n" +
            "The polygon auto-closes from the last point back to the first.",
            MessageType.None);
    }

    // ---------------------------------------------------------------- Kerbs (strips preset)

    void DrawKerbSection(TrackEnvironment env, int maxSeg)
    {
        EditorGUILayout.LabelField("New Kerb", EditorStyles.boldLabel);
        _kerbSide = (TrackEnvironment.BarrierSide)EditorGUILayout.EnumPopup("Side (of travel)", _kerbSide);

        EditorGUILayout.LabelField("Span");
        EditorGUI.indentLevel++;
        _kerbStartSeg = EditorGUILayout.IntSlider("Start segment", _kerbStartSeg, 0, maxSeg);
        _kerbStartDist = EditorGUILayout.FloatField("Start dist (m into segment)", _kerbStartDist);
        _kerbLength = EditorGUILayout.FloatField("Length (m)", _kerbLength);
        EditorGUI.indentLevel--;
        _kerbWidth = EditorGUILayout.FloatField("Width (m)", _kerbWidth);

        if (GUILayout.Button("Create Kerb"))
        {
            // Kerbs are just Strips with a preset: edge-anchored, shifted fully OUTBOARD of the track
            // surface (center = edge ± width/2), kerb material, drawn above the track and edge lines.
            // Inner = right of travel (+lateral), Outer = left of travel (−lateral) — matches barriers.
            bool inner = _kerbSide == TrackEnvironment.BarrierSide.Inner;
            var segs = _builder.track.track.segments;

            // The Strip struct stores a segment-anchored END; the UI takes a LENGTH. Convert: walk forward
            // from (startSeg, startDist) by _kerbLength metres, spilling into following segments as needed.
            // Clamps at the end of the last segment (no wrap past the lap seam).
            int endSeg = Mathf.Clamp(_kerbStartSeg, 0, segs.Length - 1);
            float endDist = Mathf.Clamp(_kerbStartDist, 0f, segs[endSeg].length) + Mathf.Max(0.1f, _kerbLength);
            while (endDist > segs[endSeg].length && endSeg < segs.Length - 1)
            {
                endDist -= segs[endSeg].length;
                endSeg++;
            }
            endDist = Mathf.Min(endDist, segs[endSeg].length);

            var list = new List<TrackEnvironment.Strip>(env.strips ?? new TrackEnvironment.Strip[0]);
            list.Add(new TrackEnvironment.Strip
            {
                label = $"Kerb {_kerbSide} {_kerbStartSeg}+{_kerbLength:0}m",
                useSpline = TrackEnvironment.SplineRef.Main,
                anchor = inner ? TrackEnvironment.LateralAnchor.RightEdge : TrackEnvironment.LateralAnchor.LeftEdge,
                startSegmentIndex = _kerbStartSeg,
                startDistance = _kerbStartDist,
                endSegmentIndex = endSeg,
                endDistance = endDist,
                lateralOffset = (inner ? 1f : -1f) * _kerbWidth * 0.5f,
                width = _kerbWidth,
                sortingOrder = 2,
                material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Kerb.mat"),
                uvLengthScale = 1f,
            });
            Undo.RecordObject(env, "Create Kerb");
            env.strips = list.ToArray();
            EditorUtility.SetDirty(env);
            _builder.Build();
            SceneView.RepaintAll();
        }

        EditorGUILayout.LabelField("Strips (kerbs, lines, ribbons)", EditorStyles.boldLabel);
        var strips = env.strips;
        if (strips == null || strips.Length == 0)
        {
            EditorGUILayout.HelpBox("None yet. Pick a side + span above and Create Kerb. Fine-tune in the Strips array.", MessageType.None);
        }
        else
        {
            for (int i = 0; i < strips.Length; i++)
            {
                var s = strips[i];
                EditorGUILayout.BeginHorizontal();
                string title = string.IsNullOrEmpty(s.label) ? $"Strip {i}" : s.label;
                EditorGUILayout.LabelField($"{i}: {title} (seg {s.startSegmentIndex}→{s.endSegmentIndex}, w {s.width:0.##}m)");
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    var list = new List<TrackEnvironment.Strip>(env.strips);
                    Undo.RecordObject(env, "Delete Strip");
                    list.RemoveAt(i);
                    env.strips = list.ToArray();
                    EditorUtility.SetDirty(env);
                    _builder.Build();
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    // Only one edit mode active at a time.
    void SetBarrierEdit(int i) { _builder.editManualSectionIndex = i; if (i >= 0) _builder.editRunoffIndex = -1; }
    void SetRunoffEdit(int i) { _builder.editRunoffIndex = i; if (i >= 0) _builder.editManualSectionIndex = -1; }

    static string Short(TrackEnvironment.SegmentEnd e) => e == TrackEnvironment.SegmentEnd.Start ? "s" : "e";

    // ---------------------------------------------------------------- Scene editing

    void OnSceneGUI()
    {
        var env = _builder.environment;
        if (env == null || _builder.track == null) return;

        if (_builder.editManualSectionIndex >= 0) EditBarrier(env);
        else if (_builder.editRunoffIndex >= 0) EditRunoff(env);
        else return;

        // Take control so plain clicks don't deselect the builder while editing.
        if (Event.current.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
    }

    void EditBarrier(TrackEnvironment env)
    {
        int idx = _builder.editManualSectionIndex;
        if (env.manualSections == null || idx >= env.manualSections.Length) return;

        var section = env.manualSections[idx];
        var pts = section.manualPoints != null ? new List<Vector2>(section.manualPoints) : new List<Vector2>();
        Transform tf = _builder.track.transform;

        bool haveAnchors = _builder.TryGetManualAnchors(idx, out Vector2 startLocal, out Vector2 endLocal);
        Vector3 startWorld = tf.TransformPoint(new Vector3(startLocal.x, startLocal.y, 0f));
        Vector3 endWorld = tf.TransformPoint(new Vector3(endLocal.x, endLocal.y, 0f));

        if (haveAnchors)
        {
            Handles.color = Color.green;
            Handles.SphereHandleCap(0, startWorld, Quaternion.identity, HandleUtility.GetHandleSize(startWorld) * 0.16f, EventType.Repaint);
            Handles.SphereHandleCap(0, endWorld, Quaternion.identity, HandleUtility.GetHandleSize(endWorld) * 0.16f, EventType.Repaint);
            Handles.Label(startWorld, " start");
            Handles.Label(endWorld, " end");
        }

        bool changed = EditPoints(pts, tf, env);

        // Open polyline: startAnchor → points → endAnchor.
        Handles.color = Color.yellow;
        Vector3 prev = startWorld;
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 w = tf.TransformPoint(new Vector3(pts[i].x, pts[i].y, 0f));
            if (haveAnchors || i > 0) Handles.DrawLine(prev, w);
            prev = w;
        }
        if (haveAnchors) Handles.DrawLine(prev, endWorld);

        if (changed)
        {
            section.manualPoints = pts.ToArray();
            env.manualSections[idx] = section;
            EditorUtility.SetDirty(env);
            _builder.Build();
        }
    }

    void EditRunoff(TrackEnvironment env)
    {
        int idx = _builder.editRunoffIndex;
        if (env.runoffAreas == null || idx >= env.runoffAreas.Length) return;

        var area = env.runoffAreas[idx];
        var pts = area.points != null ? new List<Vector2>(area.points) : new List<Vector2>();
        Transform tf = _builder.track.transform;

        bool changed = EditPoints(pts, tf, env);

        // Closed polygon outline.
        Handles.color = new Color(1f, 0.6f, 0.1f, 1f);
        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 a = tf.TransformPoint(new Vector3(pts[i].x, pts[i].y, 0f));
            Vector3 b = tf.TransformPoint(new Vector3(pts[(i + 1) % pts.Count].x, pts[(i + 1) % pts.Count].y, 0f));
            if (pts.Count >= 2) Handles.DrawLine(a, b);
        }

        if (changed)
        {
            area.points = pts.ToArray();
            env.runoffAreas[idx] = area;
            EditorUtility.SetDirty(env);
            _builder.Build();
        }
    }

    // Move (drag) / delete (shift-click) existing points and add a new one on a plain click in empty space.
    static bool EditPoints(List<Vector2> pts, Transform tf, Object undoTarget)
    {
        Event e = Event.current;
        bool changed = false;
        bool onExisting = false;

        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 world = tf.TransformPoint(new Vector3(pts[i].x, pts[i].y, 0f));
            float size = HandleUtility.GetHandleSize(world) * 0.12f;
            if (HandleUtility.DistanceToCircle(world, size) < 10f) onExisting = true;

            if (e.shift && e.type == EventType.MouseDown && e.button == 0 &&
                HandleUtility.DistanceToCircle(world, size) < 10f)
            {
                Undo.RecordObject(undoTarget, "Delete Point");
                pts.RemoveAt(i);
                e.Use();
                return true;
            }

            EditorGUI.BeginChangeCheck();
            Handles.color = Color.cyan;
            Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(undoTarget, "Move Point");
                Vector3 local = tf.InverseTransformPoint(moved);
                pts[i] = new Vector2(local.x, local.y);
                changed = true;
            }
            Handles.Label(world, $" {i}");
        }

        if (!e.shift && !e.alt && e.type == EventType.MouseDown && e.button == 0 && !onExisting)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane plane = new Plane(Vector3.forward, Vector3.zero);
            if (plane.Raycast(ray, out float dist))
            {
                Vector3 hit = ray.GetPoint(dist);
                Vector3 local = tf.InverseTransformPoint(hit);
                Undo.RecordObject(undoTarget, "Add Point");
                pts.Add(new Vector2(local.x, local.y));
                changed = true;
                e.Use();
            }
        }

        return changed;
    }
}
