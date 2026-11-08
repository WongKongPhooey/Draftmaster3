#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

// Draftmaster > Tools > Profiler Spike Report
//
// Reads whatever frames are sitting in the Profiler window's buffer (they survive leaving Play Mode) and
// writes Temp/ProfilerSpikes.txt:
//
//   * frame-time stats (median / p95 / p99 / max) over the buffer
//   * the spike frames (well above the median) with their times and the gap between them, so a periodic
//     job (every N seconds) shows up as a regular interval
//   * the markers that cost the most EXTRA self time in spike frames versus normal frames — the culprits
//   * a pruned main-thread call tree for the worst spike frames, with GC allocation per node
//
// Main thread only. In-editor profiling includes EditorLoop; a spike that lives under EditorLoop is editor
// overhead (inspector repaint, scene view), not the game.
public static class ProfilerSpikeReport
{
    const string OutPath = "Temp/ProfilerSpikes.txt";
    const int WorstFramesToTree = 8;
    const float TreeMinMs = 0.5f;
    const int TreeMaxDepth = 14;
    const int TreeMaxLines = 90;

    struct MarkerStat { public double selfMs; public double gcBytes; public double calls; }

    [MenuItem("Draftmaster/Tools/Profiler Spike Report")]
    public static void Run()
    {
        int first = ProfilerDriver.firstFrameIndex;
        int last = ProfilerDriver.lastFrameIndex;
        if (first < 0 || last < first)
        {
            Debug.LogWarning("[ProfilerSpikes] No profiler frames in the buffer — record some in the Profiler window first.");
            return;
        }

        // 1. Frame times.
        var frames = new List<(int index, float ms, double startMs)>();
        for (int i = first; i <= last; i++)
        {
            using (var raw = ProfilerDriver.GetRawFrameDataView(i, 0))
            {
                if (raw == null || !raw.valid) continue;
                frames.Add((i, (float)raw.frameTimeMs, (double)raw.frameStartTimeMs));
            }
        }
        if (frames.Count == 0)
        {
            Debug.LogWarning("[ProfilerSpikes] Buffer frames were all invalid.");
            return;
        }

        var sorted = frames.Select(f => f.ms).OrderBy(x => x).ToList();
        float Pct(float p) => sorted[Mathf.Clamp(Mathf.RoundToInt(p * (sorted.Count - 1)), 0, sorted.Count - 1)];
        float median = Pct(0.5f);
        float threshold = Mathf.Max(median * 1.75f, median + 8f);

        var spikes = frames.Where(f => f.ms >= threshold).OrderByDescending(f => f.ms).ToList();
        var spikeSet = new HashSet<int>(spikes.Select(s => s.index));

        var sb = new StringBuilder();
        sb.AppendLine($"Profiler spike report  {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"frames {first}..{last} ({frames.Count} valid)");
        sb.AppendLine($"median {median:F2} ms  p95 {Pct(0.95f):F2}  p99 {Pct(0.99f):F2}  max {sorted[sorted.Count - 1]:F2}");
        sb.AppendLine($"spike threshold {threshold:F2} ms -> {spikes.Count} spike frames");
        sb.AppendLine();

        // 2. Spike timeline (chronological, with gaps) — periodic jobs show a regular interval.
        sb.AppendLine("== Spike timeline (frame, ms, seconds since previous spike) ==");
        double prevStart = -1;
        foreach (var s in spikes.OrderBy(s => s.index))
        {
            string gap = prevStart < 0 ? "" : $"  +{(s.startMs - prevStart) / 1000.0:F2}s";
            sb.AppendLine($"  #{s.index}  {s.ms,7:F2} ms{gap}");
            prevStart = s.startMs;
        }
        sb.AppendLine();

        // 3. Per-marker self time, spikes vs normal.
        var spikeAgg = new Dictionary<string, MarkerStat>();
        var normalAgg = new Dictionary<string, MarkerStat>();
        int normalCount = 0;
        int normalStride = Mathf.Max(1, (frames.Count - spikes.Count) / 300);
        int normalSeen = 0;
        foreach (var f in frames)
        {
            bool isSpike = spikeSet.Contains(f.index);
            if (!isSpike && (normalSeen++ % normalStride) != 0) continue;
            using (var view = GetView(f.index))
            {
                if (view == null || !view.valid) continue;
                Accumulate(view, view.GetRootItemID(), isSpike ? spikeAgg : normalAgg);
                if (!isSpike) normalCount++;
            }
        }

        int spikeCount = Mathf.Max(1, spikes.Count);
        normalCount = Mathf.Max(1, normalCount);
        sb.AppendLine("== Markers ranked by EXTRA self time per spike frame (spike avg - normal avg) ==");
        sb.AppendLine("   extra ms   spike ms  normal ms   spike GC KB  calls/spike  marker");
        foreach (var kv in spikeAgg
                     .Select(kv =>
                     {
                         normalAgg.TryGetValue(kv.Key, out var n);
                         double s = kv.Value.selfMs / spikeCount, nm = n.selfMs / normalCount;
                         return (name: kv.Key, extra: s - nm, s, nm, gc: kv.Value.gcBytes / spikeCount / 1024.0,
                                 calls: kv.Value.calls / spikeCount);
                     })
                     .OrderByDescending(x => x.extra).Take(40))
        {
            sb.AppendLine($"  {kv.extra,9:F2}  {kv.s,9:F2}  {kv.nm,9:F2}  {kv.gc,12:F1}  {kv.calls,11:F1}  {kv.name}");
        }
        sb.AppendLine();

        sb.AppendLine("== Top self time in NORMAL frames (steady-state cost) ==");
        foreach (var kv in normalAgg.OrderByDescending(kv => kv.Value.selfMs).Take(20))
            sb.AppendLine($"  {kv.Value.selfMs / normalCount,8:F2} ms  {kv.Value.gcBytes / normalCount / 1024.0,8:F1} KB GC  {kv.Key}");
        sb.AppendLine();

        // 4. Trees for the worst frames.
        foreach (var s in spikes.Take(WorstFramesToTree))
        {
            using (var view = GetView(s.index))
            {
                if (view == null || !view.valid) continue;
                sb.AppendLine($"== Frame #{s.index}  {s.ms:F2} ms  (main thread, nodes >= {TreeMinMs} ms) ==");
                int lines = 0;
                WriteTree(view, view.GetRootItemID(), 0, sb, ref lines);
                sb.AppendLine();
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
        File.WriteAllText(OutPath, sb.ToString());
        Debug.Log($"[ProfilerSpikes] {spikes.Count} spikes over {frames.Count} frames (median {median:F2} ms). Report: {Path.GetFullPath(OutPath)}");
    }

    static HierarchyFrameDataView GetView(int frame) =>
        ProfilerDriver.GetHierarchyFrameDataView(frame, 0,
            HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
            HierarchyFrameDataView.columnTotalTime, false);

    static void Accumulate(HierarchyFrameDataView view, int id, Dictionary<string, MarkerStat> agg)
    {
        var children = new List<int>();
        view.GetItemChildren(id, children);
        foreach (int c in children)
        {
            string name = view.GetItemName(c);
            agg.TryGetValue(name, out var st);
            st.selfMs += view.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnSelfTime);
            st.gcBytes += view.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnGcMemory);
            st.calls += view.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnCalls);
            agg[name] = st;
            Accumulate(view, c, agg);
        }
    }

    static void WriteTree(HierarchyFrameDataView view, int id, int depth, StringBuilder sb, ref int lines)
    {
        if (depth > TreeMaxDepth || lines >= TreeMaxLines) return;
        var children = new List<int>();
        view.GetItemChildren(id, children);
        foreach (int c in children
                     .OrderByDescending(c => view.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime)))
        {
            float total = view.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime);
            if (total < TreeMinMs) break;
            if (lines++ >= TreeMaxLines) { sb.AppendLine(new string(' ', depth * 2) + "  ..."); return; }
            float self = view.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnSelfTime);
            float gc = view.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnGcMemory);
            float calls = view.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnCalls);
            string gcStr = gc >= 1024 ? $"  GC {gc / 1024f:F1} KB" : "";
            sb.AppendLine($"{new string(' ', depth * 2)}{total,7:F2} ms (self {self:F2}, x{calls:F0}){gcStr}  {view.GetItemName(c)}");
            WriteTree(view, c, depth + 1, sb, ref lines);
        }
    }
}
#endif
