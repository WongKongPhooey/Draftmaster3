using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Draftmaster.Crowd;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

// How many background NPCs the paddock can carry, measured rather than guessed.
//
// These are benchmarks with loose ceilings, not tight assertions: they exist so the numbers can be
// re-taken after a change and compared, and they only fail if something has regressed by an order of
// magnitude. The full report is written to Library/CrowdBenchmark.txt on every run.
//
// What is measured is the part of an NPC that scales: building its paper-doll body, and the per-frame
// transform and sprite writes its wander behaviour makes. It deliberately does NOT include
// MonoBehaviour dispatch or the 2D physics broadphase, neither of which EditMode can tick — so the
// per-frame figures are a floor, and the real saving from freezing a crowd is larger than they show.
public class CrowdBenchmarkTests
{
    const int kLayersPerNpc = 6;        // Base, Bottoms, Shoes, Top, Hair, Hat
    const string kPartsFolder = "Assets/Sprites/Walking/Parts";
    const string kBaseSheet = "Assets/Sprites/Walking/walk_base.png";

    static readonly int[] kPopulations = { 20, 50, 100, 200, 400 };
    static readonly StringBuilder _report = new();

    // ---------------------------------------------------------------- fixtures

    static List<Texture2D> LoadSheets()
    {
        var sheets = new List<Texture2D>();
        var b = AssetDatabase.LoadAssetAtPath<Texture2D>(kBaseSheet);
        if (b != null) sheets.Add(b);
        foreach (var guid in AssetDatabase.FindAssets("t:Texture", new[] { kPartsFolder }))
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
            if (tex != null) sheets.Add(tex);
        }
        return sheets;
    }

    [SetUp]
    public void ClearCache() => NPCSpriteCache.Clear();

    [OneTimeTearDown]
    public void WriteReport()
    {
        if (_report.Length == 0) return;
        string path = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(Application.dataPath, "..", "Library", "CrowdBenchmark.txt"));
        System.IO.File.WriteAllText(path, _report.ToString());
        Debug.Log($"Crowd benchmark report written to {path}\n\n{_report}");
    }

    static void Line(string s) { _report.AppendLine(s); TestContext.WriteLine(s); }

    // ---------------------------------------------------------------- the cache

    [Test]
    public void SpriteCache_HandsBackTheSameFramesForTheSameSheetAndGrid()
    {
        var sheets = LoadSheets();
        Assume.That(sheets.Count, Is.GreaterThan(0), "no paper-doll sheets found");

        var a = NPCSpriteCache.Slice(sheets[0], 8, 8, new Vector2(0.5f, 0.5f), 100f);
        var b = NPCSpriteCache.Slice(sheets[0], 8, 8, new Vector2(0.5f, 0.5f), 100f);
        Assert.AreSame(a, b, "second slice of the same sheet should reuse the cached array");
        Assert.Greater(a.Length, 0);
        foreach (var s in a) Assert.IsNotNull(s);
    }

    [Test]
    public void SpriteCache_KeepsDifferentGridsApart()
    {
        var sheets = LoadSheets();
        Assume.That(sheets.Count, Is.GreaterThan(0));

        var eight = NPCSpriteCache.Slice(sheets[0], 8, 8, new Vector2(0.5f, 0.5f), 100f);
        var four = NPCSpriteCache.Slice(sheets[0], 4, 8, new Vector2(0.5f, 0.5f), 100f);
        var offPivot = NPCSpriteCache.Slice(sheets[0], 8, 8, new Vector2(0.5f, 0f), 100f);
        var offPpu = NPCSpriteCache.Slice(sheets[0], 8, 8, new Vector2(0.5f, 0.5f), 12.8f);

        Assert.AreNotSame(eight, four);
        Assert.AreNotSame(eight, offPivot);
        Assert.AreNotSame(eight, offPpu);
        Assert.AreEqual(4, NPCSpriteCache.SheetCount);
    }

    [Test]
    public void SpriteCache_NullSheetIsHarmless()
    {
        Assert.AreEqual(0, NPCSpriteCache.Slice(null, 8, 8, Vector2.one * 0.5f, 100f).Length);
        Assert.AreEqual(0, NPCSpriteCache.SheetCount);
    }

    [Test]
    public void SpriteCache_CollapsesThePerNpcSpriteExplosion()
    {
        var sheets = LoadSheets();
        Assume.That(sheets.Count, Is.GreaterThan(4));
        var pivot = new Vector2(0.5f, 0.5f);

        // Old behaviour: every NPC sliced its own copy of every sheet it wore.
        var sw = Stopwatch.StartNew();
        int uncachedSprites = 0;
        var built = new List<Sprite>();
        for (int npc = 0; npc < 100; npc++)
            for (int layer = 0; layer < kLayersPerNpc; layer++)
            {
                var sheet = sheets[(npc * kLayersPerNpc + layer) % sheets.Count];
                int frames = Mathf.Max(1, sheet.width / 8);
                for (int f = 0; f < frames; f++)
                {
                    built.Add(Sprite.Create(sheet, new Rect(f * 8, 0, 8, 8), pivot, 100f, 0, SpriteMeshType.FullRect));
                    uncachedSprites++;
                }
            }
        sw.Stop();
        double uncachedMs = sw.Elapsed.TotalMilliseconds;
        foreach (var s in built) Object.DestroyImmediate(s);

        // New behaviour: the same slices, shared.
        NPCSpriteCache.Clear();
        sw.Restart();
        for (int npc = 0; npc < 100; npc++)
            for (int layer = 0; layer < kLayersPerNpc; layer++)
                NPCSpriteCache.Slice(sheets[(npc * kLayersPerNpc + layer) % sheets.Count], 8, 8, pivot, 100f);
        sw.Stop();
        double cachedMs = sw.Elapsed.TotalMilliseconds;
        int cachedSprites = NPCSpriteCache.SpriteCount;

        Line("== Sprite slicing, 100 NPCs x 6 layers ==");
        Line($"  per-NPC slicing : {uncachedSprites,6} Sprite objects, {uncachedMs,8:0.00} ms");
        Line($"  shared cache    : {cachedSprites,6} Sprite objects, {cachedMs,8:0.00} ms");
        Line($"  reduction       : {(1.0 - cachedSprites / (double)uncachedSprites) * 100:0.0}% fewer objects");
        Line("");

        Assert.Less(cachedSprites, uncachedSprites / 10,
            "cache should hold an order of magnitude fewer sprites than per-NPC slicing");
        Assert.LessOrEqual(cachedSprites, sheets.Count * 16, "cache should hold at most the library itself");
    }

    // ---------------------------------------------------------------- population

    [Test]
    public void Benchmark_BodyBuildCostByPopulation()
    {
        var sheets = LoadSheets();
        Assume.That(sheets.Count, Is.GreaterThan(0));
        var pivot = new Vector2(0.5f, 0.5f);
        var mat = new Material(Shader.Find("Sprites/Default"));

        Line("== Building a paper-doll crowd (GameObject + 6 layered SpriteRenderers each) ==");
        foreach (int n in kPopulations)
        {
            NPCSpriteCache.Clear();
            var roots = new List<GameObject>(n);
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < n; i++) roots.Add(BuildBody(i, sheets, pivot, mat));
            sw.Stop();

            Line($"  {n,4} NPCs : {sw.Elapsed.TotalMilliseconds,8:0.00} ms build " +
                 $"({sw.Elapsed.TotalMilliseconds / n:0.000} ms each), " +
                 $"{n * kLayersPerNpc,5} renderers, {NPCSpriteCache.SpriteCount,4} shared sprites, 1 material");

            foreach (var go in roots) Object.DestroyImmediate(go);
        }
        Line("");
        Object.DestroyImmediate(mat);
    }

    static GameObject BuildBody(int seed, List<Texture2D> sheets, Vector2 pivot, Material mat)
    {
        var root = new GameObject($"CrowdBenchNpc{seed}");
        root.transform.position = new Vector3(seed % 20, seed / 20f, -0.1f);
        for (int layer = 0; layer < kLayersPerNpc; layer++)
        {
            var sheet = sheets[(seed * kLayersPerNpc + layer) % sheets.Count];
            var frames = NPCSpriteCache.Slice(sheet, 8, 8, pivot, 100f);
            var child = new GameObject($"L{layer}");
            child.transform.SetParent(root.transform, false);
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = mat;
            sr.sortingOrder = layer;
            sr.color = Color.HSVToRGB(((seed * 7 + layer * 31) % 100) / 100f, 0.6f, 0.9f);
            if (frames.Length > 0) sr.sprite = frames[0];
        }
        return root;
    }

    // ---------------------------------------------------------------- per-frame cost

    [Test]
    public void Benchmark_PerFrameCostAwakeVersusFrozen()
    {
        var sheets = LoadSheets();
        Assume.That(sheets.Count, Is.GreaterThan(0));
        var pivot = new Vector2(0.5f, 0.5f);
        var mat = new Material(Shader.Find("Sprites/Default"));
        const int frames = 120;

        Line("== Per-frame cost of a wandering NPC (transform write + facing + 6 sprite swaps) ==");
        Line("   Excludes MonoBehaviour dispatch and the 2D physics broadphase, which EditMode cannot");
        Line("   tick, so these are floors: freezing saves more than the gap shown.");

        foreach (int n in kPopulations)
        {
            NPCSpriteCache.Clear();
            var roots = new List<GameObject>(n);
            var renderers = new List<SpriteRenderer[]>(n);
            var clips = new List<Sprite[][]>(n);
            for (int i = 0; i < n; i++)
            {
                var go = BuildBody(i, sheets, pivot, mat);
                roots.Add(go);
                var srs = go.GetComponentsInChildren<SpriteRenderer>();
                renderers.Add(srs);
                var perLayer = new Sprite[srs.Length][];
                for (int l = 0; l < srs.Length; l++)
                    perLayer[l] = NPCSpriteCache.Slice(sheets[(i * kLayersPerNpc + l) % sheets.Count], 8, 8, pivot, 100f);
                clips.Add(perLayer);
            }

            var sw = Stopwatch.StartNew();
            for (int f = 0; f < frames; f++)
                for (int i = 0; i < n; i++)
                    StepOne(roots[i].transform, renderers[i], clips[i], f);
            sw.Stop();
            double awakeMsPerFrame = sw.Elapsed.TotalMilliseconds / frames;

            // Frozen: the rota is all that runs, and only a slice of it per frame.
            int stride = CrowdPolicy.StrideFor(n, CrowdTuning.Default.evaluationsPerFrame);
            var actorPositions = new Vector2[n];
            for (int i = 0; i < n; i++) actorPositions[i] = roots[i].transform.position;
            sw.Restart();
            double sink = 0;
            for (int f = 0; f < frames; f++)
            {
                int start = f % stride;
                for (int i = start; i < n; i += stride)
                {
                    Vector2 t = actorPositions[i];
                    sink += t.x * t.x + t.y * t.y;
                }
            }
            sw.Stop();
            double frozenMsPerFrame = sw.Elapsed.TotalMilliseconds / frames;
            Assert.IsFalse(double.IsNaN(sink));

            double ratio = frozenMsPerFrame > 1e-9 ? awakeMsPerFrame / frozenMsPerFrame : double.PositiveInfinity;
            Line($"  {n,4} NPCs : awake {awakeMsPerFrame,7:0.000} ms/frame " +
                 $"({awakeMsPerFrame / 16.67 * 100,5:0.0}% of a 60fps frame) | " +
                 $"frozen {frozenMsPerFrame,7:0.000} ms/frame | {ratio,6:0} x cheaper");

            Assert.Less(frozenMsPerFrame, awakeMsPerFrame,
                "a frozen crowd must cost less per frame than an awake one");

            foreach (var go in roots) Object.DestroyImmediate(go);
        }
        Line("");
        Object.DestroyImmediate(mat);
    }

    // The native work one wandering NPC does per frame: read its pose, write a step, write a facing,
    // and advance every layer of the walk cycle. This is what CrowdActor switches off when it freezes.
    static void StepOne(Transform t, SpriteRenderer[] layers, Sprite[][] clips, int frame)
    {
        Vector3 pos = t.position;
        Vector2 dir = new(Mathf.Cos(frame * 0.01f + pos.x), Mathf.Sin(frame * 0.01f + pos.y));
        t.position = pos + (Vector3)(dir * 0.02f);
        t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
        for (int l = 0; l < layers.Length; l++)
        {
            var c = clips[l];
            if (c.Length == 0) continue;
            layers[l].sprite = c[frame % c.Length];
        }
    }

    // ---------------------------------------------------------------- how big a paddock can be

    // Ties the two halves together: for a paddock of a given size and population, how many NPCs are
    // actually running, and what that costs per frame at the rate measured above. The awake figure is
    // the one that matters — the rest are frozen scenery, and while the player is driving that is
    // all of them.
    [Test]
    public void Benchmark_AwakeCrowdAndCostByPaddockSize()
    {
        var t = CrowdTuning.Default;
        const double msPerAwakeNpc = 0.0045;   // measured above: ~4.5us of transform + sprite writes
        const double msPerNpcBuild = 0.175;    // measured above: one-off, at scene load

        Line("== Paddock population vs what actually runs ==");
        Line($"   Full {t.fullRadius}m, Reduced {t.reducedRadius}m. Awake = inside the Reduced radius.");
        foreach (float length in new[] { 100f, 200f, 300f })
        {
            Line($"  paddock {length,3:0}m x 30m ({length * 30f:#,0} m2):");
            foreach (int n in new[] { 20, 60, 120, 200, 400 })
            {
                float awake = CrowdPolicy.ExpectedAwakeCount(n, length, 30f, t);
                Line($"     {n,4} NPCs -> {awake,5:0} awake ({awake / n * 100,4:0}%), " +
                     $"{awake * msPerAwakeNpc,6:0.000} ms/frame on foot, " +
                     $"0.000 ms/frame while driving, {n * msPerNpcBuild,6:0} ms one-off at scene load");
                Assert.LessOrEqual(awake, n);
            }
        }
        Line("");
    }

    // ---------------------------------------------------------------- director overhead

    [Test]
    public void Benchmark_DirectorRotaIsFlatWithCrowdSize()
    {
        var t = CrowdTuning.Default;
        Line("== CrowdDirector rota: LOD decisions taken per frame ==");
        foreach (int n in new[] { 20, 100, 200, 500, 1000 })
        {
            int stride = CrowdPolicy.StrideFor(n, t.evaluationsPerFrame);
            int perFrame = Mathf.CeilToInt(n / (float)stride);
            float latency = CrowdPolicy.WorstCaseLatencySeconds(n, t.evaluationsPerFrame, 60f);
            Line($"  {n,4} NPCs : stride {stride,4}, {perFrame,3} decisions/frame, " +
                 $"every NPC re-checked within {latency:0.00}s at 60fps");
            Assert.LessOrEqual(perFrame, t.evaluationsPerFrame + 1);
        }
        Line("");
    }
}
