using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// Play-mode readout for walkers that animate but go nowhere. Take a snapshot, let the game run a second or
// two, then report: every walker that thinks it is walking but has not moved since the snapshot, with what
// its step is running into. Written to Logs/stuck-walkers.txt as well, since the console is unreliable
// over MCP in this project.
public static class PaddockWalkerDebug
{
    static readonly Dictionary<int, Vector2> _snapshot = new();
    static float _snapshotTime;

    static readonly BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Draftmaster/Debug/Walkers/1. Snapshot Positions")]
    public static void Snapshot()
    {
        if (!Application.isPlaying) { Debug.LogWarning("PaddockWalkerDebug: enter play mode first."); return; }
        _snapshot.Clear();
        foreach (var w in Object.FindObjectsByType<PaddockWalker>(FindObjectsSortMode.None))
            _snapshot[w.GetInstanceID()] = w.transform.position;
        _snapshotTime = Time.time;
        Debug.Log($"PaddockWalkerDebug: snapshot of {_snapshot.Count} walkers at t={_snapshotTime:F1}.");
    }

    [MenuItem("Draftmaster/Debug/Walkers/2. Report Stuck Since Snapshot")]
    public static void Report()
    {
        if (!Application.isPlaying) { Debug.LogWarning("PaddockWalkerDebug: enter play mode first."); return; }
        var sb = new StringBuilder();
        var walkers = Object.FindObjectsByType<PaddockWalker>(FindObjectsSortMode.None);
        int stuck = 0, moving = 0, total = 0;

        foreach (var w in walkers)
        {
            if (!_snapshot.TryGetValue(w.GetInstanceID(), out Vector2 was)) continue;
            total++;
            Vector2 now = w.transform.position;
            bool isMoving = (bool)(typeof(PaddockWalker).GetField("_moving", Priv)?.GetValue(w) ?? false);
            var diving = w.GetComponent<CartDodge>();
            if (diving != null && diving.Dodging) isMoving = true;   // the dodge animates on its own
            if (!isMoving) continue;
            moving++;
            if ((now - was).sqrMagnitude > 0.04f) continue;
            stuck++;

            var path = typeof(PaddockWalker).GetField("_path", Priv)?.GetValue(w) as List<Vector3>;
            int idx = (int)(typeof(PaddockWalker).GetField("_idx", Priv)?.GetValue(w) ?? -1);
            Vector2 target = path != null && idx >= 0 && idx < path.Count ? (Vector2)path[idx] : now;
            var rb = w.GetComponent<Rigidbody2D>();
            var blockHere = PaddockObstacles.Blocker(now, w.obstacleRadius);
            Vector2 dir = (target - now).normalized;
            var blockAhead = PaddockObstacles.Blocker(now + dir * 0.1f, w.obstacleRadius);

            var dodge = w.GetComponent<CartDodge>();
            var crowd = w.GetComponent<CrowdActor>();
            if (stuck <= 40)
                sb.AppendLine($"  {Path(w.transform)} dodge={(dodge ? (dodge.Dodging ? "DODGING" : "idle") : "-")} " +
                              $"lod={(crowd ? crowd.Lod.ToString() : "-")} pos={now.ToString("F2")} target={target.ToString("F2")} " +
                              $"enabled={w.enabled} rb={(rb ? $"{rb.bodyType} sim={rb.simulated} interp={rb.interpolation} rbpos={rb.position.ToString("F2")}" : "none")} " +
                              $"inBoundary={PaddockBoundary.IsInside(now)} targetInBoundary={PaddockBoundary.IsInside(target)} " +
                              $"blockHere={(blockHere ? Path(blockHere.transform) : "-")} blockAhead={(blockAhead ? Path(blockAhead.transform) : "-")}");
        }

        string head = $"PaddockWalkerDebug: {Time.time - _snapshotTime:F1}s since snapshot — {total} walkers, " +
                      $"{moving} walking, {stuck} walking but not moving (<0.2m). Time.timeScale={Time.timeScale} " +
                      $"Physics2D.simulationMode={Physics2D.simulationMode}";
        string text = head + "\n" + sb;
        File.WriteAllText("Logs/stuck-walkers.txt", text);
        Debug.Log(text);
    }

    static string Path(Transform t)
    {
        string s = t.name;
        for (var up = t.parent; up != null; up = up.parent) s = up.name + "/" + s;
        return s;
    }
}
