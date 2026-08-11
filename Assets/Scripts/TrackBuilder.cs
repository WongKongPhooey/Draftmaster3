using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TrackBuilder : MonoBehaviour
{
    public TrackInfoV2 track;
    public Material surfaceMaterial;
    public Material pitSurfaceMaterial;
    public bool drawGizmos = true;
    public bool rebuildOnValidate = true;

    [Header("Pit Box Lane")]
    [Tooltip("Build a second lane alongside the pit lane (wall side, +normal) where the pit boxes sit. Cars park on it; the pit lane proper stays clear for driving.")]
    public bool buildPitBoxLane = true;
    [Tooltip("Width (m) of the box lane strip.")]
    public float pitBoxLaneWidth = 6f;
    [Tooltip("Where the grey box-lane strip starts, metres from the pit lane's start. 0 = from the very start.")]
    public float pitBoxLaneStartOffset = 0f;
    [Tooltip("Where the grey box-lane strip ends, metres BEFORE the pit lane's end. 0 = all the way to the end.")]
    public float pitBoxLaneEndOffset = 0f;
    [Tooltip("Material for the box lane. Null = a flat grey tarmac is built automatically (pitBoxLaneColor).")]
    public Material pitBoxLaneMaterial;
    public Color pitBoxLaneColor = new Color(0.42f, 0.42f, 0.44f);

    // Box-lane geometry, published so the pit systems all agree with the mesh: GridSpawner parks the
    // field at the lane's centre lateral; the pit service/engage checks extend to its outer edge.
    public bool HasPitBoxLane => buildPitBoxLane && track != null && track.hasPitLane;
    float PitHalfWidth => track == null ? 0f : (track.pitDefaultWidth > 0f ? track.pitDefaultWidth : track.defaultWidth) * 0.5f;
    public float PitBoxLaneCenterLateral => PitHalfWidth + pitBoxLaneWidth * 0.5f;
    public float PitBoxLaneOuterLateral => PitHalfWidth + pitBoxLaneWidth;
    // Span of the grey strip along the pit lane (distances from the pit-lane start), matching the mesh
    // built in BuildPitLane. GridSpawner fits the pit boxes inside this span.
    public float PitBoxLaneFrom(float pitLen) => Mathf.Clamp(pitBoxLaneStartOffset, 0f, pitLen);
    public float PitBoxLaneTo(float pitLen) => Mathf.Clamp(pitLen - Mathf.Max(0f, pitBoxLaneEndOffset), 0f, pitLen);

    [Header("Brake Marker Boards")]
    [Tooltip("Place 150/100/50m marker boards on the barrier of straights that lead into a turn.")]
    public bool drawMarkerBoards = true;
    [Tooltip("Only add boards when the straight immediately before a turn is at least this long (m).")]
    public float minStraightForMarkers = 200f;
    [Tooltip("Distances (m) before the turn entry to place a board. One board per value.")]
    public float[] markerDistances = { 150f, 100f, 50f };
    [Tooltip("Gap (m) from the track edge out to the board, on top of the track half-width.")]
    public float markerEdgeOffset = 1.5f;
    [Tooltip("Board panel size (m): x across, y along the track.")]
    public Vector2 markerBoardSize = new Vector2(2.4f, 1.4f);
    [Tooltip("Sorting order for the boards (above the road surface).")]
    public int markerSortingOrder = 3;
    public Color markerPanelColor = new Color(0.05f, 0.05f, 0.05f, 1f);
    public Color markerTextColor = new Color(1f, 0.95f, 0.2f, 1f);

    [Header("Racing Line Gizmo")]
    public bool drawRacingLineGizmo = true;
    public Color idealLineColor = new Color(0.2f, 1f, 0.3f, 1f);
    public Color leftLineColor = new Color(0.3f, 0.5f, 1f, 0.7f);
    public Color rightLineColor = new Color(1f, 0.3f, 0.3f, 0.7f);
    public bool drawLeftRightBounds = true;
    public float anchorMarkerRadius = 1.2f;

    Mesh _mainMesh;
    Mesh _pitMesh;
    GameObject _pitChild;
    List<Sample> _surfaceCache;
    List<Sample> _pitSurfaceCache;

    public struct Sample
    {
        public Vector2 position;
        public Vector2 tangent;
        public Vector2 normal;
        public float width;
        public float distance;
    }

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
        if (track == null || track.segments == null || track.segments.Length == 0) return;
        _surfaceCache = null; // invalidate on-surface lookup; rebuilt lazily on next query
        _pitSurfaceCache = null;

        var mf = GetComponent<MeshFilter>();
        var mr = GetComponent<MeshRenderer>();
        if (surfaceMaterial != null) mr.sharedMaterial = surfaceMaterial;

        var mainSamples = SampleCenterline();
        _mainMesh = BuildRibbonMesh(mainSamples, track.closedLoop, $"Track_{track.name}",
                                    PixelArt.UvScale(surfaceMaterial));
        mf.sharedMesh = _mainMesh;

        BuildEdgeLines(mainSamples);
        BuildMarkerBoards(mainSamples);
        BuildPitLane();
    }

    // 150/100/50m brake boards on the barrier of any straight (>= minStraightForMarkers) that leads into a turn.
    void BuildMarkerBoards(List<Sample> samples)
    {
        TearDownChild("BrakeMarkers");
        if (!drawMarkerBoards || track.segments == null || samples == null || samples.Count < 2) return;
        if (markerDistances == null || markerDistances.Length == 0) return;

        // Cumulative start distance of each segment along the lap.
        var segs = track.segments;
        var startDist = new float[segs.Length];
        float cum = 0f;
        for (int i = 0; i < segs.Length; i++) { startDist[i] = cum; cum += segs[i].length; }
        float lapLen = samples[samples.Count - 1].distance;

        GameObject root = null;

        for (int i = 0; i < segs.Length; i++)
        {
            if (segs[i].type != TrackInfoV2.SegmentType.Turn) continue;

            // The straight feeding this turn. For the first segment, the preceding one wraps to the last (closed loop).
            int prev = i - 1;
            if (prev < 0) prev = track.closedLoop ? segs.Length - 1 : -1;
            if (prev < 0 || segs[prev].type != TrackInfoV2.SegmentType.Straight) continue;
            if (segs[prev].length < minStraightForMarkers) continue;

            float turnStart = startDist[i];
            float straightStart = startDist[prev];
            // Outside of the turn = where the wall/runoff is. +normal is the right of travel (matches RightEdgeLine).
            float outsideSign = segs[i].angle >= 0f ? 1f : -1f;

            for (int m = 0; m < markerDistances.Length; m++)
            {
                float d = turnStart - markerDistances[m];
                if (d < straightStart) continue;            // board would fall before the straight begins
                if (d < 0f) d += lapLen;                     // wrap (first-segment turn)

                var s = SampleNearestDistance(samples, d);
                if (root == null)
                {
                    root = new GameObject("BrakeMarkers");
                    root.transform.SetParent(transform, false);
                }
                BuildBoard(root.transform, s, outsideSign, Mathf.RoundToInt(markerDistances[m]));
            }
        }
    }

    Sample SampleNearestDistance(List<Sample> samples, float distance)
    {
        Sample best = samples[0];
        float bestDiff = Mathf.Abs(samples[0].distance - distance);
        for (int i = 1; i < samples.Count; i++)
        {
            float diff = Mathf.Abs(samples[i].distance - distance);
            if (diff < bestDiff) { bestDiff = diff; best = samples[i]; }
        }
        return best;
    }

    void BuildBoard(Transform root, Sample s, float outsideSign, int metres)
    {
        Vector3 normal = new Vector3(s.normal.x, s.normal.y, 0f);
        Vector3 basePos = new Vector3(s.position.x, s.position.y, 0f);
        Vector3 pos = basePos + normal * outsideSign * (s.width * 0.5f + markerEdgeOffset);
        pos.z = -0.05f; // sit just above the road surface

        // Orient so the board's local +Y runs along the track (up-track), reading upright to an approaching driver.
        float tangAng = Mathf.Atan2(s.tangent.y, s.tangent.x) * Mathf.Rad2Deg;

        var board = new GameObject($"Marker_{metres}m");
        board.transform.SetParent(root, false);
        board.transform.localPosition = pos;
        board.transform.localRotation = Quaternion.Euler(0f, 0f, tangAng - 90f);

        var panel = board.AddComponent<SpriteRenderer>();
        panel.sprite = UnitSquareSprite();
        panel.color = markerPanelColor;
        panel.sharedMaterial = MarkerMaterial();
        panel.sortingOrder = markerSortingOrder;
        board.transform.localScale = new Vector3(markerBoardSize.x, markerBoardSize.y, 1f);

        // Number, as a TextMesh (renders reliably under the 3D URP renderer, same as the NPC prompt glyph).
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(board.transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        labelGo.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, markerBoardSize.x), 1f / Mathf.Max(0.01f, markerBoardSize.y), 1f);
        var tm = labelGo.AddComponent<TextMesh>();
        tm.text = metres.ToString();
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.characterSize = 0.18f;
        tm.fontSize = 64;
        tm.color = markerTextColor;
        var tmr = labelGo.GetComponent<MeshRenderer>();
        tmr.sortingOrder = markerSortingOrder + 1;
    }

    static Sprite _square;
    static Sprite UnitSquareSprite()
    {
        if (_square != null) return _square;
        var tex = new Texture2D(2, 2);
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        _square = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f); // 2px ÷ 2ppu = 1 unit
        return _square;
    }

    static Material _markerMat;
    static Material MarkerMaterial()
    {
        if (_markerMat != null) return _markerMat;
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        _markerMat = new Material(sh);
        return _markerMat;
    }

    void BuildEdgeLines(List<Sample> samples)
    {
        TearDownChild("LeftEdgeLine");
        TearDownChild("RightEdgeLine");

        if (!track.drawEdgeLines || track.edgeLineMaterial == null) return;
        if (samples == null || samples.Count < 2) return;
        if (track.drawLeftEdgeLine) BuildSingleEdgeLine("LeftEdgeLine", samples, -1f);
        if (track.drawRightEdgeLine) BuildSingleEdgeLine("RightEdgeLine", samples, 1f);
    }

    void BuildSingleEdgeLine(string childName, List<Sample> samples, float edgeSign)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = track.edgeLineMaterial;
        mr.sortingOrder = track.edgeLineSortingOrder;

        var meshSamples = samples;
        if (track.closedLoop)
        {
            meshSamples = new List<Sample>(samples.Count + 1);
            meshSamples.AddRange(samples);
            meshSamples.Add(samples[0]);
        }

        var mesh = new Mesh { name = $"EdgeLine_{childName}" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var verts = new List<Vector3>(meshSamples.Count * 2);
        var uvs = new List<Vector2>(meshSamples.Count * 2);
        var tris = new List<int>(meshSamples.Count * 6);
        float halfLine = track.edgeLineWidth * 0.5f;
        // A painted line is directional, so unlike the asphalt it keeps ribbon UVs: across the line
        // (0..1, the texture spans the paint's width) and along it in metres at the standard density.
        Vector2 uvScale = PixelArt.UvScale(mr.sharedMaterial);
        float distance = 0f;

        for (int i = 0; i < meshSamples.Count; i++)
        {
            var s = meshSamples[i];
            float centerLateral = edgeSign * (s.width * 0.5f - track.edgeLineInset);
            Vector3 right = new Vector3(s.normal.x, s.normal.y, 0);
            Vector3 baseP = new Vector3(s.position.x, s.position.y, 0);
            Vector3 lineCenter = baseP + right * centerLateral;
            verts.Add(lineCenter - right * halfLine);
            verts.Add(lineCenter + right * halfLine);
            uvs.Add(new Vector2(0f, distance * uvScale.y));
            uvs.Add(new Vector2(1f, distance * uvScale.y));
            if (i > 0)
            {
                int a = (i - 1) * 2;
                int b = i * 2;
                tris.Add(a + 0); tris.Add(b + 0); tris.Add(b + 1);
                tris.Add(a + 0); tris.Add(b + 1); tris.Add(a + 1);
                distance += Vector2.Distance(meshSamples[i - 1].position, s.position);
            }
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
    }

    void TearDownChild(string childName)
    {
        var existing = transform.Find(childName);
        if (existing == null) return;
        if (Application.isPlaying) Destroy(existing.gameObject);
        else DestroyImmediate(existing.gameObject);
    }

    void BuildPitLane()
    {
        // Tear down any previous pit child first.
        var existing = transform.Find("PitLane");
        if (existing != null)
        {
            if (Application.isPlaying) Destroy(existing.gameObject);
            else DestroyImmediate(existing.gameObject);
        }
        _pitChild = null;
        _pitMesh = null;

        if (!track.hasPitLane || track.pitSegments == null || track.pitSegments.Length == 0) return;

        var pitSamples = SamplePitCenterline();
        if (pitSamples.Count < 2) return;

        _pitChild = new GameObject("PitLane");
        _pitChild.transform.SetParent(transform, false);
        var mf = _pitChild.AddComponent<MeshFilter>();
        var mr = _pitChild.AddComponent<MeshRenderer>();
        mr.sharedMaterial = pitSurfaceMaterial != null ? pitSurfaceMaterial : surfaceMaterial;
        _pitMesh = BuildRibbonMesh(pitSamples, false, $"Pit_{track.name}",
                                   PixelArt.UvScale(mr.sharedMaterial));
        mf.sharedMesh = _pitMesh;

        // Second lane on the wall side (+normal): the grey strip the pit boxes sit on. Shares its inner
        // edge with the pit ribbon so the two read as one paved surface. Its span is trimmed by the
        // start/end offsets so the strip can begin after the pit entry and stop before the exit.
        if (buildPitBoxLane && pitBoxLaneWidth > 0f)
        {
            float pitLen = pitSamples[pitSamples.Count - 1].distance;
            float from = Mathf.Clamp(pitBoxLaneStartOffset, 0f, pitLen);
            float to = Mathf.Clamp(pitLen - Mathf.Max(0f, pitBoxLaneEndOffset), 0f, pitLen);
            var bandSamples = SampleRange(pitSamples, from, to);
            if (bandSamples.Count >= 2)
            {
                var laneGo = new GameObject("PitBoxLane");
                laneGo.transform.SetParent(_pitChild.transform, false);
                var lmf = laneGo.AddComponent<MeshFilter>();
                var lmr = laneGo.AddComponent<MeshRenderer>();
                lmr.sharedMaterial = pitBoxLaneMaterial != null ? pitBoxLaneMaterial : BuildBoxLaneMaterial();
                lmf.sharedMesh = BuildBandMesh(bandSamples, s => s.width * 0.5f, s => s.width * 0.5f + pitBoxLaneWidth,
                                               $"PitBoxLane_{track.name}", PixelArt.UvScale(lmr.sharedMaterial));
            }
        }
    }

    // Sub-range of a sample list between two distances, with exact interpolated samples at both ends.
    List<Sample> SampleRange(List<Sample> samples, float from, float to)
    {
        var list = new List<Sample>();
        if (to - from < 0.01f) return list;
        list.Add(SamplePitAt(from, samples));
        for (int i = 0; i < samples.Count; i++)
            if (samples[i].distance > from && samples[i].distance < to) list.Add(samples[i]);
        list.Add(SamplePitAt(to, samples));
        return list;
    }

    Material BuildBoxLaneMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        var m = new Material(sh) { name = "PitBoxLaneGrey" };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", pitBoxLaneColor);
        else if (m.HasProperty("_Color")) m.SetColor("_Color", pitBoxLaneColor);
        return m;
    }

    // Ribbon between two lateral offsets from the centerline (both along +normal; pass negatives for the
    // other side). Same vertex layout as BuildRibbonMesh, open-ended.
    Mesh BuildBandMesh(List<Sample> samples, System.Func<Sample, float> latFrom, System.Func<Sample, float> latTo,
                       string name, Vector2 uvScale)
    {
        if (samples == null || samples.Count < 2) return null;

        var mesh = new Mesh { name = name };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        var verts = new List<Vector3>(samples.Count * 2);
        var uvs = new List<Vector2>(samples.Count * 2);
        var tris = new List<int>(samples.Count * 6);

        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            Vector3 right = new Vector3(s.normal.x, s.normal.y, 0);
            Vector3 c = new Vector3(s.position.x, s.position.y, 0);
            Vector3 a0 = c + right * latFrom(s);
            Vector3 b0 = c + right * latTo(s);
            verts.Add(a0);
            verts.Add(b0);
            uvs.Add(new Vector2(a0.x * uvScale.x, a0.y * uvScale.y));
            uvs.Add(new Vector2(b0.x * uvScale.x, b0.y * uvScale.y));

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

    // uvScale bakes the project pixel density into the mesh (see PixelArt.UvScale). UVs are the vertex's
    // WORLD position in metres, scaled -- not 0..1 across the ribbon. Asphalt is isotropic, so anchoring it
    // to the world grid rather than the ribbon means the road, the pit lane, the runoff and the grass all
    // share one continuous texel grid, and a 12m-wide road and a 100m-wide runoff cannot end up at
    // different pixel sizes the way a 0..1 cross-ribbon UV guarantees they will.
    public static Mesh BuildRibbonMesh(List<Sample> samples, bool closedLoop, string name)
        => BuildRibbonMesh(samples, closedLoop, name, Vector2.one);

    public static Mesh BuildRibbonMesh(List<Sample> samples, bool closedLoop, string name, Vector2 uvScale)
    {
        if (samples == null || samples.Count < 2) return null;

        var meshSamples = samples;
        if (closedLoop)
        {
            meshSamples = new List<Sample>(samples.Count + 1);
            meshSamples.AddRange(samples);
            meshSamples.Add(samples[0]);
        }

        var mesh = new Mesh { name = name };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        var verts = new List<Vector3>(meshSamples.Count * 2);
        var uvs = new List<Vector2>(meshSamples.Count * 2);
        var tris = new List<int>(meshSamples.Count * 6);

        for (int i = 0; i < meshSamples.Count; i++)
        {
            var s = meshSamples[i];
            Vector3 right = new Vector3(s.normal.x, s.normal.y, 0);
            Vector3 l = new Vector3(s.position.x, s.position.y, 0) - right * (s.width * 0.5f);
            Vector3 r = new Vector3(s.position.x, s.position.y, 0) + right * (s.width * 0.5f);
            verts.Add(l);
            verts.Add(r);
            uvs.Add(new Vector2(l.x * uvScale.x, l.y * uvScale.y));
            uvs.Add(new Vector2(r.x * uvScale.x, r.y * uvScale.y));

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

    public List<Sample> SampleCenterline()
    {
        return SampleSegments(
            track.startPosition,
            track.startHeading,
            track.segments,
            track.defaultWidth,
            Mathf.Max(1, track.samplesPerSegment),
            Mathf.Max(0.1f, track.maxArcStepMetres),
            track.closedLoop,
            seg => seg.width <= 0f ? track.defaultWidth : seg.width);
    }

    public List<Sample> SamplePitCenterline()
    {
        if (track == null || !track.hasPitLane || track.pitSegments == null) return new List<Sample>();
        float pitWidth = track.pitDefaultWidth > 0f ? track.pitDefaultWidth : track.defaultWidth;

        // Pit start locked to the pit entry node on the main spline. Use authored-segment walker (skips seam closure samples).
        track.SampleAuthoredSpline(track.pitEntryDistance, out Vector2 pitStartPos, out float pitStartHeading);
        pitStartHeading += track.pitStartHeadingOffset;
        track.pitStartPosition = pitStartPos;
        track.pitStartHeading = pitStartHeading;

        return SampleSegments(
            pitStartPos,
            pitStartHeading,
            track.pitSegments,
            pitWidth,
            Mathf.Max(1, track.samplesPerSegment),
            Mathf.Max(0.1f, track.maxArcStepMetres),
            false,
            seg => seg.width <= 0f ? pitWidth : seg.width);
    }

    public Sample SampleAt(float distance, List<Sample> samples = null)
    {
        if (samples == null) samples = SampleCenterline();
        return SampleListAt(samples, distance, track != null && track.closedLoop);
    }

    public Sample SamplePitAt(float distance, List<Sample> samples = null)
    {
        if (samples == null) samples = SamplePitCenterline();
        return SampleListAt(samples, distance, false);
    }

    // True if worldPos sits over a drivable surface (main track OR pit lane). Outputs |lateral| offset from the
    // nearest centreline (m). Uses cached centerlines (invalidated on Build); cheap enough for one car per FixedUpdate.
    public bool IsOnSurface(Vector3 worldPos, out float lateralAbs)
    {
        lateralAbs = 0f;
        if (track == null) return true;
        if (_surfaceCache == null || _surfaceCache.Count < 2) _surfaceCache = SampleCenterline();

        Vector2 local = transform.InverseTransformPoint(worldPos);

        if (OnSampleSurface(_surfaceCache, local, out lateralAbs)) return true;
        if (_surfaceCache.Count < 2) return true; // no usable main centerline — don't penalise

        // The pit lane is drivable too: don't treat it like grass. The band extends past the ribbon on
        // the +normal side to cover the box lane, so parked/box-lane cars keep tarmac physics.
        if (track.hasPitLane)
        {
            if (_pitSurfaceCache == null || _pitSurfaceCache.Count < 2) _pitSurfaceCache = SamplePitCenterline();
            float extraPlus = HasPitBoxLane ? pitBoxLaneWidth : 0f;
            if (OnPitBand(_pitSurfaceCache, local, extraPlus, out float pitLat))
            {
                lateralAbs = pitLat;
                return true;
            }
        }

        // Free-placed extra ribbons (escape roads, alternate-layout splits) are tarmac too, not grass.
        var extras = ExtraTrackSpline.Active;
        for (int i = 0; i < extras.Count; i++)
        {
            if (extras[i] != null && extras[i].IsOnSurface(worldPos, out float exLat))
            {
                lateralAbs = exLat;
                return true;
            }
        }
        return false;
    }

    // Signed band test against the pit centerline: [-width/2, width/2 + extraPlus] along +normal.
    static bool OnPitBand(List<Sample> samples, Vector2 local, float extraPlus, out float lateralAbs)
    {
        lateralAbs = 0f;
        if (samples == null || samples.Count < 2) return false;
        float best = float.MaxValue;
        int bi = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            float d = ((Vector2)samples[i].position - local).sqrMagnitude;
            if (d < best) { best = d; bi = i; }
        }
        var s = samples[bi];
        float lat = Vector2.Dot(local - s.position, s.normal);
        lateralAbs = Mathf.Abs(lat);
        return lat >= -s.width * 0.5f && lat <= s.width * 0.5f + extraPlus;
    }

    // True if worldPos sits over the pit lane (ribbon or box lane) and NOT over the main track surface —
    // the main band wins at the entry/exit blends where the two overlap, so a car passing the pit mouth
    // on track is never located by pit distance.
    public bool IsOnPitSurface(Vector3 worldPos)
    {
        if (track == null || !track.hasPitLane) return false;
        if (_surfaceCache == null || _surfaceCache.Count < 2) _surfaceCache = SampleCenterline();
        if (_pitSurfaceCache == null || _pitSurfaceCache.Count < 2) _pitSurfaceCache = SamplePitCenterline();
        Vector2 local = transform.InverseTransformPoint(worldPos);
        if (OnSampleSurface(_surfaceCache, local, out _)) return false;
        float extraPlus = HasPitBoxLane ? pitBoxLaneWidth : 0f;
        return OnPitBand(_pitSurfaceCache, local, extraPlus, out _);
    }

    // Project a WORLD position onto the main centerline and return its distance (m) along the spline.
    // Lets a free-driven car (the player) be located on the track for gap maths against the AI field.
    // Uses the cached centerline (invalidated on Build); cheap enough for one query per FixedUpdate.
    public float NearestCenterlineDistance(Vector3 worldPos)
    {
        if (_surfaceCache == null || _surfaceCache.Count < 2) _surfaceCache = SampleCenterline();
        return NearestDistanceOnSamples(transform.InverseTransformPoint(worldPos), _surfaceCache);
    }

    // Same projection against the PIT centerline — used to rejoin the pit lane without a positional jump.
    public float NearestPitDistance(Vector3 worldPos)
    {
        if (_pitSurfaceCache == null || _pitSurfaceCache.Count < 2) _pitSurfaceCache = SamplePitCenterline();
        return NearestDistanceOnSamples(transform.InverseTransformPoint(worldPos), _pitSurfaceCache);
    }

    static float NearestDistanceOnSamples(Vector2 local, List<Sample> samples)
    {
        if (samples == null || samples.Count < 2) return 0f;

        float best = float.MaxValue;
        int bi = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            float d = ((Vector2)samples[i].position - local).sqrMagnitude;
            if (d < best) { best = d; bi = i; }
        }

        // Refine to sub-sample accuracy by projecting onto the segment toward the closer neighbour.
        int ni = bi;
        float bestN = float.MaxValue;
        if (bi > 0) { float d = ((Vector2)samples[bi - 1].position - local).sqrMagnitude; if (d < bestN) { bestN = d; ni = bi - 1; } }
        if (bi < samples.Count - 1) { float d = ((Vector2)samples[bi + 1].position - local).sqrMagnitude; if (d < bestN) { bestN = d; ni = bi + 1; } }

        var a = samples[bi];
        var b = samples[ni];
        Vector2 ab = (Vector2)b.position - (Vector2)a.position;
        float denom = ab.sqrMagnitude;
        float t = denom > 1e-6f ? Mathf.Clamp01(Vector2.Dot(local - (Vector2)a.position, ab) / denom) : 0f;
        return Mathf.Lerp(a.distance, b.distance, t);
    }

    // Nearest-sample test: is local within half-width of this centerline? Outputs |lateral| from it.
    // Public+static: ExtraTrackSpline runs the same test against its own centerline.
    public static bool OnSampleSurface(List<Sample> samples, Vector2 local, out float lateralAbs)
    {
        lateralAbs = 0f;
        if (samples == null || samples.Count < 2) return false;
        float best = float.MaxValue;
        int bi = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            float d = ((Vector2)samples[i].position - local).sqrMagnitude;
            if (d < best) { best = d; bi = i; }
        }
        var s = samples[bi];
        lateralAbs = Mathf.Abs(Vector2.Dot(local - s.position, s.normal));
        return lateralAbs <= s.width * 0.5f;
    }

    static Sample SampleListAt(List<Sample> samples, float distance, bool loop)
    {
        if (samples.Count == 0) return default;
        if (samples.Count == 1) return samples[0];

        float total = samples[samples.Count - 1].distance;
        if (loop && total > 0f) distance = ((distance % total) + total) % total;

        int lo = 0, hi = samples.Count - 1;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) >> 1;
            if (samples[mid].distance <= distance) lo = mid;
            else hi = mid;
        }
        var a = samples[lo];
        var b = samples[hi];
        float denom = b.distance - a.distance;
        float t = denom > 0f ? Mathf.Clamp01((distance - a.distance) / denom) : 0f;
        Vector2 tan = Vector2.Lerp(a.tangent, b.tangent, t);
        if (tan.sqrMagnitude > 0) tan.Normalize();
        else tan = a.tangent;
        Vector2 nrm = new Vector2(tan.y, -tan.x);
        return new Sample
        {
            position = Vector2.Lerp(a.position, b.position, t),
            tangent = tan,
            normal = nrm,
            width = Mathf.Lerp(a.width, b.width, t),
            distance = distance
        };
    }

    public static List<Sample> SampleSegments(
        Vector2 startPos,
        float startHeading,
        TrackInfoV2.TrackSegment[] segments,
        float defaultWidth,
        int minSpp,
        float step,
        bool closedLoop,
        System.Func<TrackInfoV2.TrackSegment, float> widthFn)
    {
        var samples = new List<Sample>();
        if (segments == null || segments.Length == 0) return samples;

        Vector2 pos = startPos;
        float headingDeg = startHeading;
        float cumulativeDistance = 0f;

        EmitSample(samples, pos, headingDeg, widthFn(segments[0]), cumulativeDistance);

        for (int segIndex = 0; segIndex < segments.Length; segIndex++)
        {
            var seg = segments[segIndex];
            if (seg.length <= 0f) continue;

            TrackInfoV2.TrackSegment nextSeg;
            if (segIndex < segments.Length - 1) nextSeg = segments[segIndex + 1];
            else if (closedLoop) nextSeg = segments[0];
            else nextSeg = seg;
            float wA = widthFn(seg);
            float wB = widthFn(nextSeg);
            int spp = Mathf.Max(minSpp, Mathf.CeilToInt(seg.length / step));

            if (seg.type == TrackInfoV2.SegmentType.Straight)
            {
                Vector2 dir = HeadingToDir(headingDeg);
                Vector2 sPos = pos;
                for (int s = 1; s <= spp; s++)
                {
                    float t = s / (float)spp;
                    Vector2 p = sPos + dir * seg.length * t;
                    float w = Mathf.Lerp(wA, wB, t);
                    float d = cumulativeDistance + seg.length * t;
                    EmitSample(samples, p, headingDeg, w, d);
                }
                pos = sPos + dir * seg.length;
                cumulativeDistance += seg.length;
            }
            else // Turn
            {
                float angleRad = seg.angle * Mathf.Deg2Rad;
                if (Mathf.Abs(angleRad) < 0.0001f)
                {
                    Vector2 dir = HeadingToDir(headingDeg);
                    pos += dir * seg.length;
                    cumulativeDistance += seg.length;
                    EmitSample(samples, pos, headingDeg, wB, cumulativeDistance);
                    continue;
                }

                float radius = seg.length / Mathf.Abs(angleRad);
                Vector2 forward = HeadingToDir(headingDeg);
                Vector2 toCentre = (seg.angle >= 0f)
                    ? new Vector2(-forward.y, forward.x)
                    : new Vector2(forward.y, -forward.x);
                Vector2 centre = pos + toCentre * radius;

                Vector2 startRadial = pos - centre;
                float startAngle = Mathf.Atan2(startRadial.y, startRadial.x);

                for (int s = 1; s <= spp; s++)
                {
                    float t = s / (float)spp;
                    float a = startAngle + angleRad * t;
                    Vector2 p = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                    float headingHere = headingDeg + seg.angle * t;
                    float w = Mathf.Lerp(wA, wB, t);
                    float d = cumulativeDistance + seg.length * t;
                    EmitSample(samples, p, headingHere, w, d);
                }

                pos = centre + new Vector2(Mathf.Cos(startAngle + angleRad), Mathf.Sin(startAngle + angleRad)) * radius;
                headingDeg += seg.angle;
                cumulativeDistance += seg.length;
            }
        }

        // Stitch the loop seam: if closed, add samples interpolating final pos back to startPos so the car travels
        // continuously across the join instead of teleporting. Authoring imprecision in segment sums lands here.
        if (closedLoop)
        {
            float closeDist = Vector2.Distance(pos, startPos);
            if (closeDist > 0.01f)
            {
                int spp = Mathf.Max(minSpp, Mathf.CeilToInt(closeDist / step));
                Vector2 dir = (startPos - pos) / closeDist;
                float closeHeading = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                float wA = widthFn(segments[segments.Length - 1]);
                float wB = widthFn(segments[0]);
                Vector2 sPos = pos;
                for (int s = 1; s <= spp; s++)
                {
                    float t = s / (float)spp;
                    Vector2 p = sPos + dir * (closeDist * t);
                    float headingHere = Mathf.LerpAngle(headingDeg, closeHeading, t);
                    float w = Mathf.Lerp(wA, wB, t);
                    float d = cumulativeDistance + closeDist * t;
                    EmitSample(samples, p, headingHere, w, d);
                }
                cumulativeDistance += closeDist;
            }
        }

        return samples;
    }

    static Vector2 HeadingToDir(float headingDeg)
    {
        float r = headingDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }

    static void EmitSample(List<Sample> samples, Vector2 pos, float headingDeg, float width, float distance)
    {
        Vector2 t = HeadingToDir(headingDeg);
        Vector2 n = new Vector2(t.y, -t.x);
        samples.Add(new Sample
        {
            position = pos,
            tangent = t,
            normal = n,
            width = width,
            distance = distance
        });
    }

    void DrawRacingLineCurve(List<Sample> samples, List<TrackInfoV2.RacingLineAnchor> anchors, float lineFactor, Color color)
    {
        Gizmos.color = color;
        Vector3 prev = Vector3.zero;
        float loopLen = samples.Count > 0 ? samples[samples.Count - 1].distance : 0f;
        for (int i = 0; i < samples.Count; i++)
        {
            var s = samples[i];
            Vector2 right = new Vector2(s.tangent.y, -s.tangent.x);
            float lateral = track.GetLateralAt(s.distance, lineFactor, anchors, loopLen);
            Vector2 p = s.position + right * lateral;
            Vector3 w = transform.TransformPoint(new Vector3(p.x, p.y, 0));
            if (i > 0) Gizmos.DrawLine(prev, w);
            prev = w;
        }
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || track == null) return;
        var samples = SampleCenterline();
        if (samples.Count < 2) return;

        Gizmos.color = Color.yellow;
        for (int i = 1; i < samples.Count; i++)
        {
            Vector3 a = transform.TransformPoint(new Vector3(samples[i - 1].position.x, samples[i - 1].position.y, 0));
            Vector3 b = transform.TransformPoint(new Vector3(samples[i].position.x, samples[i].position.y, 0));
            Gizmos.DrawLine(a, b);
        }

        Gizmos.color = Color.green;
        Vector3 sp = transform.TransformPoint(new Vector3(track.startPosition.x, track.startPosition.y, 0));
        Gizmos.DrawCube(sp, Vector3.one * 4f);

        if (track.hasPitLane)
        {
            var pitSamples = SamplePitCenterline();
            Gizmos.color = new Color(1f, 0.6f, 0f);
            for (int i = 1; i < pitSamples.Count; i++)
            {
                Vector3 a = transform.TransformPoint(new Vector3(pitSamples[i - 1].position.x, pitSamples[i - 1].position.y, 0));
                Vector3 b = transform.TransformPoint(new Vector3(pitSamples[i].position.x, pitSamples[i].position.y, 0));
                Gizmos.DrawLine(a, b);
            }

            // Entry/exit markers on the main spline + connector lines to pit ends
            var entrySample = SampleAt(track.pitEntryDistance, samples);
            var exitSample = SampleAt(track.pitExitDistance, samples);
            Vector3 entry = transform.TransformPoint(new Vector3(entrySample.position.x, entrySample.position.y, 0));
            Vector3 exit = transform.TransformPoint(new Vector3(exitSample.position.x, exitSample.position.y, 0));
            Gizmos.color = new Color(0.2f, 1f, 0.4f);
            Gizmos.DrawWireSphere(entry, 4f);
            Gizmos.color = new Color(1f, 0.4f, 0.4f);
            Gizmos.DrawWireSphere(exit, 4f);
            if (pitSamples.Count > 0)
            {
                Vector3 pitStart = transform.TransformPoint(new Vector3(pitSamples[0].position.x, pitSamples[0].position.y, 0));
                Vector3 pitEnd = transform.TransformPoint(new Vector3(pitSamples[pitSamples.Count - 1].position.x, pitSamples[pitSamples.Count - 1].position.y, 0));
                Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.5f);
                Gizmos.DrawLine(entry, pitStart);
                Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.5f);
                Gizmos.DrawLine(exit, pitEnd);
            }
        }

        if (drawRacingLineGizmo && samples.Count >= 2)
        {
            var anchors = track.BuildRacingLineAnchors();
            if (anchors.Count > 0)
            {
                if (drawLeftRightBounds)
                {
                    DrawRacingLineCurve(samples, anchors, -1f, leftLineColor);
                    DrawRacingLineCurve(samples, anchors, +1f, rightLineColor);
                }
                DrawRacingLineCurve(samples, anchors, 0f, idealLineColor);

                Gizmos.color = idealLineColor;
                for (int i = 0; i < anchors.Count; i++)
                {
                    var anchor = anchors[i];
                    var s = SampleAt(anchor.distance, samples);
                    Vector2 right = new Vector2(s.tangent.y, -s.tangent.x);
                    Vector2 p = s.position + right * anchor.ideal;
                    Vector3 w = transform.TransformPoint(new Vector3(p.x, p.y, 0));
                    Gizmos.DrawWireSphere(w, anchorMarkerRadius);
                }
            }
        }
    }
}
