using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrackEnvironmentBuilder))]
public class TrackEnvironmentBuilderEditor : Editor
{
    TrackEnvironmentBuilder _builder;
    int _pickSegment;
    TrackEnvironment.BarrierSide _pickSide = TrackEnvironment.BarrierSide.Outer;

    void OnEnable() { _builder = (TrackEnvironmentBuilder)target; }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var env = _builder.environment;
        if (env == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Barrier Manual Sections", EditorStyles.boldLabel);

        int segCount = (_builder.track != null && _builder.track.track != null && _builder.track.track.segments != null)
            ? _builder.track.track.segments.Length : 0;

        _pickSegment = EditorGUILayout.IntSlider("Segment", _pickSegment, 0, Mathf.Max(0, segCount - 1));
        _pickSide = (TrackEnvironment.BarrierSide)EditorGUILayout.EnumPopup("Side", _pickSide);

        int existing = FindSection(env, _pickSegment, _pickSide);
        if (existing < 0)
        {
            if (GUILayout.Button("Switch This Section To Manual"))
            {
                var list = new List<TrackEnvironment.ManualBarrierSection>(env.manualSections ?? new TrackEnvironment.ManualBarrierSection[0]);
                list.Add(new TrackEnvironment.ManualBarrierSection { segmentIndex = _pickSegment, side = _pickSide, manualPoints = new Vector2[0] });
                env.manualSections = list.ToArray();
                _builder.editManualSectionIndex = list.Count - 1;
                EditorUtility.SetDirty(env);
                _builder.Build();
            }
        }
        else
        {
            EditorGUILayout.HelpBox($"Segment {_pickSegment} / {_pickSide} is Manual (section {existing}).", MessageType.Info);
            if (GUILayout.Button("Edit This Section In Scene"))
                _builder.editManualSectionIndex = existing;
            if (GUILayout.Button("Revert This Section To Auto"))
            {
                var list = new List<TrackEnvironment.ManualBarrierSection>(env.manualSections);
                list.RemoveAt(existing);
                env.manualSections = list.ToArray();
                _builder.editManualSectionIndex = -1;
                EditorUtility.SetDirty(env);
                _builder.Build();
            }
        }

        EditorGUILayout.HelpBox(
            "Scene editing the active manual section:\n" +
            "• Ctrl+Click = add point\n" +
            "• Drag handles = move\n" +
            "• Shift+Click a point = delete\n" +
            "Endpoints auto-connect to neighbour barriers.",
            MessageType.None);
    }

    static int FindSection(TrackEnvironment env, int seg, TrackEnvironment.BarrierSide side)
    {
        if (env.manualSections == null) return -1;
        for (int i = 0; i < env.manualSections.Length; i++)
            if (env.manualSections[i].segmentIndex == seg && env.manualSections[i].side == side) return i;
        return -1;
    }

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

        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 world = tf.TransformPoint(new Vector3(pts[i].x, pts[i].y, 0f));
            float size = HandleUtility.GetHandleSize(world) * 0.12f;

            if (e.shift && e.type == EventType.MouseDown && e.button == 0 &&
                HandleUtility.DistanceToCircle(world, size) < 8f)
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

        Handles.color = Color.yellow;
        for (int i = 1; i < pts.Count; i++)
            Handles.DrawLine(
                tf.TransformPoint(new Vector3(pts[i - 1].x, pts[i - 1].y, 0f)),
                tf.TransformPoint(new Vector3(pts[i].x, pts[i].y, 0f)));

        if (e.control && e.type == EventType.MouseDown && e.button == 0)
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

        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
    }
}
