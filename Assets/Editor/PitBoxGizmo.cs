using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Draws the pit boxes in the scene view at EDIT time, so the box ladder can be seen (and the painted
// dividing lines placed) without entering play mode. The layout comes from PitLane.FitBoxes — the same
// call GridSpawner makes at spawn — so what is drawn here is where the cars and crews actually end up.
//
// Toggle:  Draftmaster/Debug/Show Pit Boxes
// Export:  Draftmaster/Debug/Log Pit Box Lines  (distances + local positions of every dividing line)
//
// Box count comes from TrackBuilder.ResolvePitBoxCount() — the same one the painted box lines are built
// from, so the gizmo and the paint can never disagree.
static class PitBoxGizmo
{
    const string ShowMenu = "Draftmaster/Debug/Show Pit Boxes";
    const string ShowKey = "Draftmaster.ShowPitBoxes";

    static readonly Color RailColor = new Color(1f, 1f, 1f, 0.55f);
    static readonly Color LineColor = new Color(1f, 0.95f, 0.35f, 0.95f);
    static readonly Color ParkColor = new Color(0.2f, 0.9f, 1f, 0.9f);
    static readonly Color OverflowColor = new Color(1f, 0.3f, 0.25f, 0.95f);

    static GUIStyle _label;

    public static bool Show
    {
        get => EditorPrefs.GetBool(ShowKey, true);
        set => EditorPrefs.SetBool(ShowKey, value);
    }

    [MenuItem(ShowMenu)]
    static void ToggleShow()
    {
        Show = !Show;
        SceneView.RepaintAll();
    }

    [MenuItem(ShowMenu, true)]
    static bool ToggleShowValidate()
    {
        Menu.SetChecked(ShowMenu, Show);
        return true;
    }

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
    static void DrawPitBoxes(TrackBuilder track, GizmoType type)
    {
        if (!Show || track == null || !track.drawGizmos) return;
        if (!Resolve(track, out var pit, out float pitLen, out var fit)) return;

        float inner = track.PitBoxLaneInnerLateral;
        float outer = track.PitBoxLaneOuterLateral;
        // No strip on this track: draw a nominal 6m-wide band off the pit centreline so the boxes still read.
        if (!track.HasPitBoxLane) { inner = 0f; outer = 6f; }
        float park = track.HasPitBoxLane ? track.PitBoxLaneCenterLateral : 3f;
        LineSpan(track, pitLen, out float stripFrom, out float stripTo);

        float frontD = PitLane.BoxLineDistance(0, pitLen, fit, stripFrom, stripTo);
        float backD = PitLane.BoxLineDistance(fit.boxes, pitLen, fit, stripFrom, stripTo);

        // Rails: the two long edges of the ladder, sampled so they follow a curved lane.
        Gizmos.color = fit.Overflow > 0.01f ? OverflowColor : RailColor;
        DrawRail(track, pit, backD, frontD, inner);
        DrawRail(track, pit, backD, frontD, outer);

        // One dividing line per box boundary, plus the two end lines — where TrackBuilder paints them.
        Gizmos.color = LineColor;
        for (int i = 0; i <= fit.boxes; i++)
        {
            var s = track.SamplePitAt(PitLane.BoxLineDistance(i, pitLen, fit, stripFrom, stripTo), pit);
            Gizmos.DrawLine(World(track, s, inner), World(track, s, outer));
        }

        // Box centres: where the car parks, plus the index/distance readout.
        int labelEvery = fit.boxes > 20 ? 5 : 1;
        for (int i = 0; i < fit.boxes; i++)
        {
            float d = PitLane.BoxDistance(i, pitLen, fit);
            var s = track.SamplePitAt(d, pit);
            Vector3 p = World(track, s, park);
            Gizmos.color = ParkColor;
            Gizmos.DrawWireSphere(p, 0.5f);
            if (i % labelEvery == 0 || i == fit.boxes - 1)
                Handles.Label(p, $"#{i}  {d:0.0}m", Label());
        }

        if (fit.Overflow > 0.01f)
        {
            var s = track.SamplePitAt(backD, pit);
            Handles.Label(World(track, s, outer),
                $"box lane overflows by {fit.Overflow:0.0}m ({fit.boxes} boxes @ min {PitLane.MinSpacing:0.0}m)",
                Label(OverflowColor));
        }
    }

    [MenuItem("Draftmaster/Debug/Log Pit Box Lines")]
    static void LogPitBoxLines()
    {
        var track = Object.FindFirstObjectByType<TrackBuilder>();
        if (track == null) { Debug.LogWarning("PitBoxGizmo: no TrackBuilder in the open scene."); return; }
        if (!Resolve(track, out var pit, out float pitLen, out var fit))
        {
            Debug.LogWarning("PitBoxGizmo: this track has no pit lane, or its pit centreline is empty.");
            return;
        }

        float inner = track.HasPitBoxLane ? track.PitBoxLaneInnerLateral : 0f;
        float outer = track.HasPitBoxLane ? track.PitBoxLaneOuterLateral : 6f;
        LineSpan(track, pitLen, out float stripFrom, out float stripTo);

        var sb = new StringBuilder();
        sb.AppendLine($"Pit box lines — {track.name}: pitLen={pitLen:0.0}m boxes={fit.boxes} spacing={fit.spacing:0.00}m " +
                      $"exitGap={fit.exitGap:0.0}m strip=[{fit.spanFrom:0.0}..{fit.spanTo:0.0}] overflow={fit.Overflow:0.0}m");
        sb.AppendLine("line, pitDistance, innerLocalX, innerLocalY, outerLocalX, outerLocalY");
        for (int i = 0; i <= fit.boxes; i++)
        {
            float d = PitLane.BoxLineDistance(i, pitLen, fit, stripFrom, stripTo);
            var s = track.SamplePitAt(d, pit);
            Vector2 a = s.position + s.normal * inner;
            Vector2 b = s.position + s.normal * outer;
            sb.AppendLine($"{i}, {d:0.00}, {a.x:0.00}, {a.y:0.00}, {b.x:0.00}, {b.y:0.00}");
        }
        Debug.Log(sb.ToString());
    }

    // Shared setup: pit samples, pit length and the box fit for the count this track should preview.
    static bool Resolve(TrackBuilder track, out List<TrackBuilder.Sample> pit, out float pitLen, out PitLane.BoxFit fit)
    {
        pit = null;
        pitLen = 0f;
        fit = default;
        if (track == null || track.track == null || !track.track.hasPitLane) return false;

        pit = track.SamplePitCenterline();
        if (pit.Count < 2) return false;
        pitLen = pit[pit.Count - 1].distance;
        if (pitLen <= 0f) return false;

        fit = PitLane.FitBoxes(track, pitLen, track.ResolvePitBoxCount());
        return fit.boxes > 0 && fit.spacing > 0f;
    }

    // The distances the end lines are pinned to, matching TrackBuilder.BuildPitBoxLines: the grey strip's
    // own ends, inset by half a line so the paint sits fully on the tarmac.
    static void LineSpan(TrackBuilder track, float pitLen, out float from, out float to)
    {
        float half = track.pitBoxLineWidth * 0.5f;
        from = (track.HasPitBoxLane ? track.PitBoxLaneFrom(pitLen) : 0f) + half;
        to = Mathf.Max(from, (track.HasPitBoxLane ? track.PitBoxLaneTo(pitLen) : pitLen) - half);
    }

    // Walks the pit samples themselves between the two distances, so the rail follows a curved lane exactly
    // (and costs one pass, not one binary search per metre of scene-view redraw).
    static void DrawRail(TrackBuilder track, List<TrackBuilder.Sample> pit, float from, float to, float lateral)
    {
        Vector3 prev = World(track, track.SamplePitAt(from, pit), lateral);
        for (int i = 0; i < pit.Count; i++)
        {
            if (pit[i].distance <= from) continue;
            if (pit[i].distance >= to) break;
            Vector3 next = World(track, pit[i], lateral);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
        Gizmos.DrawLine(prev, World(track, track.SamplePitAt(to, pit), lateral));
    }

    static Vector3 World(TrackBuilder track, TrackBuilder.Sample s, float lateral)
    {
        Vector2 p = s.position + s.normal * lateral;
        return track.transform.TransformPoint(new Vector3(p.x, p.y, 0f));
    }

    static GUIStyle Label(Color? color = null)
    {
        if (_label == null) _label = new GUIStyle(EditorStyles.miniLabel);
        _label.normal.textColor = color ?? Color.white;
        return _label;
    }
}
