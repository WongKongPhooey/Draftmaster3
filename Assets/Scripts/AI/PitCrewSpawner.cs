using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Builds one PitCrewBox (5 members) at every pit box, fitting them to the shared PitLane geometry that
// GridSpawner publishes. Drop this on an empty GameObject in the race scene and point it at the TrackBuilder.
//
// Members are paper-doll NPCs (NPCLayeredAppearance, same look as the paddock crowd). Four wheel changers stand
// at the car's corners and one fueller at the rear; supply the wheel / fuel-can sprites they hold. Anything left
// unassigned falls back to a coloured placeholder so the scene is visible before the art exists.
public class PitCrewSpawner : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Track whose pit lane the boxes sit on. Auto-found if null.")]
    public TrackBuilder track;
    [Tooltip("Paper-doll outfit library for the crew. If null, members are coloured placeholders.")]
    public NPCPartLibrary crewLibrary;

    [Header("Held gear (you model these)")]
    [Tooltip("Sprite the four wheel changers hold. Placeholder built if null.")]
    public Sprite wheelSprite;
    [Tooltip("Sprite the fueller holds. Placeholder built if null.")]
    public Sprite fuelCanSprite;
    public float gearScale = 1f;

    [Header("Layout")]
    [Tooltip("0 = one crew per race car (PitLane.BoxCount). Otherwise spawn this many boxes.")]
    public int boxCount = 0;
    [Tooltip("Sign of the pit-wall side the crew stand on. Flip to -1 if they end up on the racing side.")]
    public float wallSide = 1f;
    [Tooltip("Uniform scale per crew member. Placeholders are already built ~1.4m tall, so leave at 1. If you assign a tiny paper-doll library (e.g. 8px art), bump this up to reach roughly person height.")]
    public float memberScale = 1f;
    [Tooltip("Lateral distance (m) from the box centre to the standby line on the wall. Must clear the parked box lane (PitLane.ParkLateral + car half-width) or the crew stand inside the parked cars.")]
    public float standbyLateral = 4.6f;
    [Tooltip("Half the car length the wheel stations straddle (m).")]
    public float wheelLongitudinal = 1.8f;
    [Tooltip("Lateral offset (m) of a wheel station from the box centre.")]
    public float wheelLateral = 1.2f;

    [Header("Box props")]
    [Tooltip("Dress every box with a static set of 4 tyres and a fuel can. Cars park directly in front of them.")]
    public bool buildBoxProps = true;
    [Tooltip("How far behind the box centre (m, along the lane) the prop cluster sits. Car half-length is ~2.4, so the default puts the props just behind the parked car's rear bumper.")]
    public float propsBehind = 3.4f;
    [Tooltip("Fallback lateral offset (m) of the props toward the pit wall. When GridSpawner has published PitLane.ParkLateral (the wall-side lane cars park on), that is used instead so the props sit directly behind each parked car.")]
    public float propsLateral = 2.6f;
    [Tooltip("Uniform scale applied to each prop sprite.")]
    public float propScale = 1f;

    [Header("Sorting")]
    public string sortingLayerName = "Vehicles";
    [Tooltip("Crew draw above the cars (car sorting order is ~5).")]
    public int baseSortingOrder = 8;
    [Tooltip("Box props draw below the cars so a car sliding past reads as driving over them.")]
    public int propSortingOrder = 4;

    Material _unlit;

    void Start() => StartCoroutine(SpawnWhenReady());

    IEnumerator SpawnWhenReady()
    {
        if (track == null) track = FindFirstObjectByType<TrackBuilder>();
        // Wait for GridSpawner to fit the boxes to the lane and for the pit mesh to exist.
        float timeout = 10f;
        while (timeout > 0f && (track == null || !PitLane.Configured))
        {
            if (track == null) track = FindFirstObjectByType<TrackBuilder>();
            timeout -= Time.deltaTime;
            yield return null;
        }
        if (track == null || track.track == null || !track.track.hasPitLane) yield break;

        var pit = track.SamplePitCenterline();
        if (pit.Count < 2) yield break;
        float pitLength = pit[pit.Count - 1].distance;
        if (pitLength <= 0f) yield break;

        int count = boxCount > 0 ? boxCount : PitLane.BoxCount;
        var root = new GameObject("PitCrews").transform;
        root.SetParent(transform, false);

        for (int idx = 0; idx < count; idx++)
        {
            float boxDist = PitLane.Configured ? PitLane.BoxDistance(idx, pitLength)
                                               : Mathf.Lerp(pitLength, 0f, (idx + 0.5f) / count);
            BuildBox(root, idx, boxDist, pit);
        }
    }

    void BuildBox(Transform root, int idx, float boxDist, List<TrackBuilder.Sample> pit)
    {
        var s = track.SamplePitAt(boxDist, pit);
        Vector3 worldPos = track.transform.TransformPoint(new Vector3(s.position.x, s.position.y, 0f));
        worldPos.z = -0.1f; // toward the camera so crew draw in front of the pit tarmac
        Vector3 tangent = track.transform.TransformDirection(new Vector3(s.tangent.x, s.tangent.y, 0f)).normalized;

        // Box frame: local +Y runs along the lane (= car forward), local +X points to the wall side.
        float tangAng = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
        var boxGo = new GameObject($"PitCrewBox_{idx}");
        boxGo.transform.SetParent(root, false);
        boxGo.transform.position = worldPos;
        boxGo.transform.rotation = Quaternion.Euler(0f, 0f, tangAng - 90f); // up = tangent

        var box = boxGo.AddComponent<PitCrewBox>();
        box.wheelLongitudinal = wheelLongitudinal;
        box.wheelLateral = wheelLateral;
        box.Configure(idx);

        // Five stations: 4 wheels (corners) + 1 fueller (rear). x is lateral (wall = +wallSide), y is along lane.
        float wl = wallSide;
        var work = new[]
        {
            new Vector3( wheelLateral * wl,  wheelLongitudinal, 0f), // front wheel, wall side
            new Vector3( wheelLateral * wl, -wheelLongitudinal, 0f), // rear wheel, wall side
            new Vector3(-wheelLateral * wl,  wheelLongitudinal, 0f), // front wheel, far side
            new Vector3(-wheelLateral * wl, -wheelLongitudinal, 0f), // rear wheel, far side
            new Vector3( wheelLateral * wl, -wheelLongitudinal - 1.0f, 0f), // fueller, rear wall side
        };
        // Standby: lined up along the wall, always beyond the parked box lane so the crew never stand
        // inside a parked car (ParkLateral is the parked file's centre; +2.2 clears a half-width + margin).
        float standbyLat = standbyLateral;
        if (PitLane.Configured) standbyLat = Mathf.Max(standbyLat, Mathf.Abs(PitLane.ParkLateral) + 2.2f);
        float sx = standbyLat * wl;
        var standby = new[]
        {
            new Vector3(sx,  2.4f, 0f),
            new Vector3(sx,  1.2f, 0f),
            new Vector3(sx,  0.0f, 0f),
            new Vector3(sx, -1.2f, 0f),
            new Vector3(sx, -2.6f, 0f),
        };

        for (int m = 0; m < 5; m++)
        {
            bool fueller = m == 4;
            BuildMember(box, boxGo.transform, standby[m], work[m], fueller, idx * 5 + m);
        }

        if (buildBoxProps) BuildProps(boxGo.transform);
    }

    // Static box dressing: a 2×2 set of tyres with the fuel can behind them, sitting at the back of the
    // box on the wall side. The car's park point is the box centre, so it stops directly in front.
    void BuildProps(Transform boxParent)
    {
        var props = new GameObject("BoxProps");
        props.transform.SetParent(boxParent, false);
        // Box-frame +X matches the spline lateral sign, so PitLane.ParkLateral (signed) drops in directly.
        float lat = (PitLane.Configured && Mathf.Abs(PitLane.ParkLateral) > 0.01f) ? PitLane.ParkLateral : propsLateral * wallSide;
        props.transform.localPosition = new Vector3(lat, -propsBehind, 0f);

        var tirePositions = new[]
        {
            new Vector3(-0.35f,  0.35f, 0f),
            new Vector3( 0.35f,  0.35f, 0f),
            new Vector3(-0.35f, -0.35f, 0f),
            new Vector3( 0.35f, -0.35f, 0f),
        };
        for (int t = 0; t < tirePositions.Length; t++)
            BuildProp(props.transform, "Tyre", tirePositions[t],
                wheelSprite != null ? wheelSprite : Placeholder(new Color(0.08f, 0.08f, 0.08f), 0.6f));

        BuildProp(props.transform, "FuelCan", new Vector3(0f, -1.1f, 0f),
            fuelCanSprite != null ? fuelCanSprite : Placeholder(new Color(0.85f, 0.2f, 0.15f), 0.7f));
    }

    void BuildProp(Transform parent, string name, Vector3 localPos, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one * propScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = UnlitSprite();
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = propSortingOrder;
    }

    void BuildMember(PitCrewBox box, Transform parent, Vector3 standby, Vector3 work, bool fueller, int seed)
    {
        var go = new GameObject(fueller ? "Fueller" : "WheelChanger");
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * memberScale;

        var member = go.AddComponent<PitCrewMember>();

        // Appearance: paper-doll if a library exists, else a coloured blob.
        NPCLayeredAppearance appearance = null;
        var layered = go.AddComponent<NPCLayeredAppearance>();
        layered.library = crewLibrary;
        layered.layerMaterial = UnlitSprite();
        layered.sortingLayerName = sortingLayerName;
        layered.baseSortingOrder = baseSortingOrder;
        if (crewLibrary != null && layered.Build(seed))
        {
            appearance = layered;
        }
        else
        {
            Destroy(layered);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Placeholder(fueller ? new Color(0.95f, 0.55f, 0.15f) : new Color(0.2f, 0.5f, 0.95f), 1.4f);
            sr.sharedMaterial = UnlitSprite();
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = baseSortingOrder;
        }

        // Held gear, shown only while servicing.
        var itemGo = new GameObject("Gear");
        itemGo.transform.SetParent(go.transform, false);
        itemGo.transform.localPosition = new Vector3(0f, 0.18f, 0f);
        itemGo.transform.localScale = Vector3.one * gearScale;
        var item = itemGo.AddComponent<SpriteRenderer>();
        item.sprite = fueller
            ? (fuelCanSprite != null ? fuelCanSprite : Placeholder(new Color(0.85f, 0.2f, 0.15f), 0.7f))
            : (wheelSprite != null ? wheelSprite : Placeholder(new Color(0.1f, 0.1f, 0.1f), 0.6f));
        item.sharedMaterial = UnlitSprite();
        item.sortingLayerName = sortingLayerName;
        item.sortingOrder = baseSortingOrder + 6; // gear in front of the member
        item.enabled = false;                     // Init turns it on — crew always hold their gear

        member.Init(standby, work, appearance, item, fueller);
        box.AddMember(member);
    }

    Material UnlitSprite()
    {
        if (_unlit != null) return _unlit;
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        _unlit = new Material(sh);
        return _unlit;
    }

    // Builds a round placeholder sized to `metres` across in world space (pixelsPerUnit set so the sprite is that
    // big at scale 1 — independent of memberScale), so unsupplied art never balloons over the cars.
    static Sprite Placeholder(Color color, float metres)
    {
        int s = 32;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        Vector2 c = new(s * 0.5f, s * 0.5f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                px[y * s + x] = Vector2.Distance(new Vector2(x, y), c) < s * 0.45f ? (Color32)color : new Color32(0, 0, 0, 0);
        tex.SetPixels32(px); tex.Apply();
        float ppu = s / Mathf.Max(0.05f, metres);   // s pixels span `metres` world units
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), ppu);
    }
}
