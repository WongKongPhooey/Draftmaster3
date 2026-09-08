using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Track limits, checked against a real track rather than a mock: Watkins Glen is the only venue with
// painted kerbs authored on it, so it is the one place the rule can be asked "does riding this kerb cost
// the driver the lap?". The package is instantiated for real (its TrackEnvironmentBuilder registers the
// surface polygons on enable), then LapTimingManager's rule is asked about points across every kerb.
//
// Everything goes through reflection because the runtime types live in Assembly-CSharp, which a test
// assembly cannot reference.
public class TrackLimitsTests
{
    const string PackagePath = "Assets/Resources/TrackPackages/WatkinsGlen.prefab";
    const float CarHalfWidth = 1f;

    GameObject _instance;
    MonoBehaviour _trackBuilder;
    System.Reflection.MethodInfo _onLegalSurface;

    static Type Runtime(string typeName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name != "Assembly-CSharp") continue;
            var t = asm.GetType(typeName);
            if (t != null) return t;
        }
        return null;
    }

    [SetUp]
    public void SpawnTrack()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PackagePath);
        Assert.IsNotNull(prefab, $"track package missing at {PackagePath}");
        _instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        var envBuilderType = Runtime("TrackEnvironmentBuilder");
        var trackBuilderType = Runtime("TrackBuilder");
        Assert.IsNotNull(envBuilderType, "TrackEnvironmentBuilder type not found");
        Assert.IsNotNull(trackBuilderType, "TrackBuilder type not found");

        // Register the runoff/kerb polygons with SurfaceField from THIS instance, whatever ran before.
        var envBuilder = _instance.GetComponentInChildren(envBuilderType, true) as MonoBehaviour;
        Assert.IsNotNull(envBuilder, "package has no TrackEnvironmentBuilder");
        envBuilderType.GetMethod("Build").Invoke(envBuilder, null);

        _trackBuilder = _instance.GetComponentInChildren(trackBuilderType, true) as MonoBehaviour;
        Assert.IsNotNull(_trackBuilder, "package has no TrackBuilder");

        _onLegalSurface = Runtime("LapTimingManager").GetMethod("OnLegalSurface");
        Assert.IsNotNull(_onLegalSurface, "LapTimingManager.OnLegalSurface not found");
    }

    [TearDown]
    public void DespawnTrack()
    {
        if (_instance != null) UnityEngine.Object.DestroyImmediate(_instance);
    }

    bool WithinTrackLimits(Vector3 worldPos) =>
        (bool)_onLegalSurface.Invoke(null, new object[] { _trackBuilder, worldPos, CarHalfWidth });

    // Distance from the main centreline, so a kerb's two rails can be told apart (which one is outboard
    // depends on which side of the track the kerb was painted).
    float Lateral(Vector3 worldPos)
    {
        var args = new object[] { worldPos, 0f };
        _trackBuilder.GetType().GetMethod("IsOnSurface").Invoke(_trackBuilder, args);
        return (float)args[1];
    }

    // Strip meshes are built as (left rail, right rail) vertex pairs down the ribbon; the pair halfway
    // along is a cross-section of the painted kerb, in world space.
    static bool KerbCrossSection(MeshFilter mf, out Vector3 a, out Vector3 b)
    {
        a = b = Vector3.zero;
        var mesh = mf.sharedMesh;
        if (mesh == null || mesh.vertexCount < 4) return false;
        var verts = mesh.vertices;
        int mid = (verts.Length / 2 / 2) * 2;
        a = mf.transform.TransformPoint(verts[mid]);
        b = mf.transform.TransformPoint(verts[mid + 1]);
        return true;
    }

    List<MeshFilter> Kerbs()
    {
        var kerbs = new List<MeshFilter>();
        foreach (var mf in _instance.GetComponentsInChildren<MeshFilter>(true))
            if (mf.name.IndexOf("kerb", StringComparison.OrdinalIgnoreCase) >= 0) kerbs.Add(mf);
        return kerbs;
    }

    [Test]
    public void WatkinsGlenHasPaintedKerbs()
    {
        Assert.Greater(Kerbs().Count, 0, "no kerb strips in the Watkins Glen package — the rest of this fixture proves nothing");
    }

    [Test]
    public void RidingAKerbKeepsTheLap()
    {
        var complaints = new List<string>();
        foreach (var kerb in Kerbs())
        {
            if (!KerbCrossSection(kerb, out var a, out var b)) continue;
            bool aIsInner = Lateral(a) <= Lateral(b);
            Vector3 inner = aIsInner ? a : b, outer = aIsInner ? b : a;
            Vector3 outward = (outer - inner).normalized;

            // Across the painted band, and a touch over its outer lip — a car sitting there still has its
            // inside wheels on the kerb or the road.
            foreach (float t in new[] { 0.1f, 0.5f, 0.9f, 1f })
            {
                Vector3 p = Vector3.Lerp(inner, outer, t);
                if (!WithinTrackLimits(p)) complaints.Add($"{kerb.name} at {t:0.0} across the kerb: {p}");
            }
            Vector3 lip = outer + outward * 0.25f;
            if (!WithinTrackLimits(lip)) complaints.Add($"{kerb.name} 0.25m over the outer lip: {lip}");
        }
        Assert.IsEmpty(complaints, "lap invalidated while on a painted kerb:\n" + string.Join("\n", complaints));
    }

    [Test]
    public void RunningWellPastAKerbLosesTheLap()
    {
        // The rule has to still bite: step outboard from each kerb until both the car centre and its
        // inside wheels are over unpaved ground, and check the lap is gone there. Kerbs backed by tarmac
        // runoff never reach that state and are skipped.
        int checkedKerbs = 0;
        var complaints = new List<string>();
        foreach (var kerb in Kerbs())
        {
            if (!KerbCrossSection(kerb, out var a, out var b)) continue;
            bool aIsInner = Lateral(a) <= Lateral(b);
            Vector3 inner = aIsInner ? a : b, outerLip = aIsInner ? b : a;
            Vector3 outward = (outerLip - inner).normalized;
            bool unpavedFound = false;
            for (float d = 0.5f; d <= 12f && !unpavedFound; d += 0.5f)
            {
                Vector3 p = outerLip + outward * d;
                // "Off" means off for the whole car: the inside wheels are a car half-width further in.
                if (WithinTrackLimits(p)) continue;
                unpavedFound = true;
                checkedKerbs++;
                // One more metre out must still be off — the rule must not flip back on.
                if (WithinTrackLimits(p + outward)) complaints.Add($"{kerb.name}: {d + 1f:0.0}m past the kerb reads as on-track");
            }
        }
        Assert.Greater(checkedKerbs, 0, "no kerb had unpaved ground behind it — nothing tested");
        Assert.IsEmpty(complaints, string.Join("\n", complaints));
    }
}
