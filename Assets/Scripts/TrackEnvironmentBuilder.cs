using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class TrackEnvironmentBuilder : MonoBehaviour
{
    public TrackBuilder track;
    public TrackEnvironment environment;
    public bool rebuildOnValidate = true;

    void OnEnable() { Build(); }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!rebuildOnValidate) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null && isActiveAndEnabled) Build();
        };
    }
#endif

    public void Build()
    {
        if (track == null || environment == null) return;
        ClearChildren();

        var samples = track.SampleCenterline();
        var pitSamples = track.SamplePitCenterline();
        if (samples.Count < 2) return;

        BuildStrips(samples, pitSamples);
        BuildDecorations(samples, pitSamples);
    }

    void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i);
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
    }

    void BuildStrips(List<TrackBuilder.Sample> mainSamples, List<TrackBuilder.Sample> pitSamples)
    {
        if (environment.strips == null || environment.strips.Length == 0) return;
        var stripsRoot = new GameObject("Strips");
        stripsRoot.transform.SetParent(transform, false);

        float spacing = Mathf.Max(0.25f, environment.stripSampleSpacing);
        for (int s = 0; s < environment.strips.Length; s++)
        {
            var strip = environment.strips[s];
            if (strip.endDistance <= strip.startDistance) continue;
            var lookup = strip.useSpline == TrackEnvironment.SplineRef.Pit ? pitSamples : mainSamples;
            if (lookup == null || lookup.Count < 2) continue;

            var go = new GameObject(string.IsNullOrEmpty(strip.label) ? $"Strip_{s}" : strip.label);
            go.transform.SetParent(stripsRoot.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sortingOrder = strip.sortingOrder;
            if (strip.material != null) mr.sharedMaterial = strip.material;
            mf.sharedMesh = BuildStripMesh(lookup, strip, spacing);
        }
    }

    Mesh BuildStripMesh(List<TrackBuilder.Sample> samples, TrackEnvironment.Strip strip, float spacing)
    {
        var mesh = new Mesh { name = $"Strip_{strip.startDistance}_{strip.endDistance}" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        float length = strip.endDistance - strip.startDistance;
        int steps = Mathf.Max(2, Mathf.CeilToInt(length / spacing) + 1);
        float uvScale = strip.uvLengthScale > 0f ? strip.uvLengthScale : 1f;

        bool usePit = strip.useSpline == TrackEnvironment.SplineRef.Pit;
        for (int i = 0; i < steps; i++)
        {
            float t = i / (float)(steps - 1);
            float d = strip.startDistance + length * t;
            var sample = usePit ? track.SamplePitAt(d, samples) : track.SampleAt(d, samples);
            Vector2 center = sample.position + sample.normal * strip.lateralOffset;
            Vector2 left = center - sample.normal * (strip.width * 0.5f);
            Vector2 right = center + sample.normal * (strip.width * 0.5f);
            verts.Add(new Vector3(left.x, left.y, 0));
            verts.Add(new Vector3(right.x, right.y, 0));
            uvs.Add(new Vector2(0f, length * t * uvScale));
            uvs.Add(new Vector2(1f, length * t * uvScale));

            if (i > 0)
            {
                int a = (i - 1) * 2;
                int b = i * 2;
                tris.Add(a + 0); tris.Add(b + 0); tris.Add(b + 1);
                tris.Add(a + 0); tris.Add(b + 1); tris.Add(a + 1);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void BuildDecorations(List<TrackBuilder.Sample> mainSamples, List<TrackBuilder.Sample> pitSamples)
    {
        if (environment.decorations == null || environment.decorations.Length == 0) return;
        var decorationsRoot = new GameObject("Decorations");
        decorationsRoot.transform.SetParent(transform, false);

        for (int i = 0; i < environment.decorations.Length; i++)
        {
            var deco = environment.decorations[i];
            if (deco.prefab == null) continue;

            bool usePit = deco.useSpline == TrackEnvironment.SplineRef.Pit;
            var lookup = usePit ? pitSamples : mainSamples;
            if (lookup == null || lookup.Count < 2) continue;
            var sample = usePit ? track.SamplePitAt(deco.distance, lookup) : track.SampleAt(deco.distance, lookup);
            Vector2 pos = sample.position + sample.normal * deco.lateralOffset;
            float angleDeg = Mathf.Atan2(sample.tangent.y, sample.tangent.x) * Mathf.Rad2Deg + deco.rotationOffset;

            GameObject instance;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(deco.prefab);
            else
                instance = Instantiate(deco.prefab);
#else
            instance = Instantiate(deco.prefab);
#endif
            instance.name = string.IsNullOrEmpty(deco.label) ? deco.prefab.name : deco.label;
            instance.transform.SetParent(decorationsRoot.transform, false);
            instance.transform.position = new Vector3(pos.x, pos.y, 0);
            instance.transform.rotation = Quaternion.Euler(0, 0, angleDeg);
            if (deco.scale.sqrMagnitude > 0f)
                instance.transform.localScale = new Vector3(deco.scale.x, deco.scale.y, 1f);
        }
    }
}
