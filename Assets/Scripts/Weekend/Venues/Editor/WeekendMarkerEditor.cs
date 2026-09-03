using UnityEditor;
using UnityEngine;

// Authoring surface for a WeekendMarker: place the marker where the player can walk, then drag the place
// they actually end up.
//
// A marker with a teleport is two positions that mean different things and one of them is nowhere near the
// object you have selected — the gate is at the paddock exit and the seat is across the track. Dragging the
// seat meant hunting for a child object in the hierarchy, clicking it, losing sight of the gate, and having
// nothing on screen to say which marker it belonged to. So the seat gets a handle of its own while the
// MARKER is selected: both ends visible at once, a line between them, and a label on each.
//
// Everything here is editor-only convenience. The runtime contract is unchanged — WeekendVenueSites reads
// `teleportTo` and hangs a WeekendMarkerGate off it exactly as before.
[CustomEditor(typeof(WeekendMarker))]
[CanEditMultipleObjects]
public class WeekendMarkerEditor : Editor
{
    // The child name the naming convention picks up. WeekendMarker.AdoptNamedObjects accepts several; this
    // is the one we create, so an authored marker and an adopted one look the same in the hierarchy.
    const string SeatChildName = "Seat";

    static readonly Color GateColor = new Color(1f, 0.78f, 0.25f, 1f);
    static readonly Color SeatColor = new Color(0.35f, 0.85f, 1f, 1f);

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var marker = (WeekendMarker)target;

        EditorGUILayout.Space();

        // Where the player ends up ------------------------------------------------------------------
        EditorGUILayout.LabelField("Teleport", EditorStyles.boldLabel);

        if (marker.teleportTo == null)
        {
            EditorGUILayout.HelpBox(
                "No teleport target. The player walks to this marker and the booking starts here.\n\n" +
                "Add one for a place they cannot walk to — a grandstand across the track, a room upstairs. " +
                "The marker stays at the gate they CAN reach; the target is where they come out.",
                MessageType.None);

            if (GUILayout.Button("Add teleport target"))
                foreach (var t in targets) CreateSeat((WeekendMarker)t);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"Walking in here and pressing the action button puts the player at '{marker.teleportTo.name}', " +
                $"{Vector2.Distance(marker.MarkerPosition, marker.teleportTo.position):0} m away.\n\n" +
                "Drag the blue handle in the scene view to place it.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select target")) Selection.activeGameObject = marker.teleportTo.gameObject;
                if (GUILayout.Button("Frame both")) FrameBoth(marker);
            }
        }

        // Can the player actually get to the gate? ------------------------------------------------------
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Reachability", EditorStyles.boldLabel);

        if (!PaddockBoundary.AnyActive)
        {
            EditorGUILayout.HelpBox("No PaddockBoundary in the scene, so nothing constrains the player and " +
                                    "any position counts as reachable.", MessageType.None);
        }
        else if (marker.IsReachable(out float outsideBy))
        {
            EditorGUILayout.HelpBox("Inside the walkable paddock.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"{outsideBy:0.0} m outside the walkable paddock. The player is clamped away from this and " +
                "can never satisfy the objective. Move it inside the boundary — and if the venue itself is " +
                "out there, that is what the teleport target is for.",
                MessageType.Error);

            if (GUILayout.Button("Move marker inside the boundary"))
                foreach (var t in targets) SnapInside((WeekendMarker)t);
        }
    }

    void OnSceneGUI()
    {
        var marker = (WeekendMarker)target;

        // The gate itself, and what it is called on the objective HUD.
        Handles.color = GateColor;
        Vector3 gate = marker.MarkerPosition;
        Handles.Label(gate + new Vector3(0.8f, 0.8f, 0f), marker.Label, Caption(GateColor));

        if (marker.teleportTo == null) return;

        Vector3 seat = marker.teleportTo.position;

        // The pair, and the trip between them. Dotted because it is not a path anybody walks — it is a cut.
        Handles.color = SeatColor;
        Handles.DrawDottedLine(gate, seat, 4f);
        Handles.DrawWireDisc(seat, Vector3.forward, 1.5f);
        Handles.Label(seat + new Vector3(0.8f, 0.8f, 0f),
                      $"{marker.Label} — where you come out", Caption(SeatColor));

        // The handle that is the point of all this: drag the seat without leaving the marker.
        EditorGUI.BeginChangeCheck();
        Vector3 moved = Handles.PositionHandle(seat, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(marker.teleportTo, "Move Teleport Target");
            // Flat: the on-foot layer is a plane, and a target nudged off it puts the player at the wrong
            // depth when the gate drops them there.
            marker.teleportTo.position = new Vector3(moved.x, moved.y, marker.teleportTo.position.z);
            EditorUtility.SetDirty(marker.teleportTo);
        }
    }

    // ------------------------------------------------------------------ actions

    static void CreateSeat(WeekendMarker marker)
    {
        if (marker == null || marker.teleportTo != null) return;

        var go = new GameObject(SeatChildName);
        Undo.RegisterCreatedObjectUndo(go, "Add Teleport Target");
        Undo.SetTransformParent(go.transform, marker.transform, "Add Teleport Target");

        // Offset rather than on top of the marker, so the handle is grabbable the moment it appears
        // instead of fighting the marker's own gizmo for the same pixel.
        go.transform.position = marker.MarkerPosition + new Vector3(0f, 12f, 0f);

        Undo.RecordObject(marker, "Add Teleport Target");
        marker.teleportTo = go.transform;
        EditorUtility.SetDirty(marker);
    }

    static void SnapInside(WeekendMarker marker)
    {
        if (marker == null || !PaddockBoundary.AnyActive) return;

        Vector2 inside = PaddockBoundary.Constrain(marker.MarkerPosition);

        // Constrain lands exactly ON the edge, and the player is clamped to that same line — so a marker
        // left there is a coin toss every frame. Step it in along the direction it came from.
        Vector2 back = ((Vector2)marker.MarkerPosition - inside);
        Vector2 nudge = back.sqrMagnitude > 1e-4f ? -back.normalized * 1.5f : Vector2.zero;

        Undo.RecordObject(marker.transform, "Move Marker Inside Paddock");
        Vector3 was = marker.transform.position;
        Vector3 offset = marker.transform.position - marker.MarkerPosition;  // collider may not be centred
        marker.transform.position = new Vector3(inside.x + nudge.x, inside.y + nudge.y, was.z) + offset;
        EditorUtility.SetDirty(marker.transform);
    }

    static void FrameBoth(WeekendMarker marker)
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null || marker.teleportTo == null) return;

        Vector3 gate = marker.MarkerPosition;
        Vector3 seat = marker.teleportTo.position;
        var bounds = new Bounds(gate, Vector3.zero);
        bounds.Encapsulate(seat);
        bounds.Expand(20f);
        view.Frame(bounds, false);
    }

    static GUIStyle Caption(Color color)
    {
        var style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = color;
        return style;
    }
}
