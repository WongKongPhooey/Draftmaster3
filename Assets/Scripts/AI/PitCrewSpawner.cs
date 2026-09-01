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

    [Header("Pit box stands")]
    [Tooltip("Build the team's pit box — the cart on the wall the crew chief sits on — over every box, " +
             "painted in that car's colours (CarColours). See PitBoxStand.")]
    public bool spawnStands = true;
    [Tooltip("How far past the PARKED CAR (m) the stand sits. Measured from the car rather than from the " +
             "lane centre, because the car is what it has to be behind: a stand a fixed distance off the " +
             "centreline lands between the car and the racing surface whenever the box lane is wide.")]
    public float standBeyondCar = 3.4f;
    [Tooltip("Fallback distance (m) from the box centre, used only when the box lane has not been " +
             "configured and there is no parked-car lateral to measure from.")]
    public float standLateral = 6.2f;
    [Tooltip("Put the stands on the PADDOCK side of the boxes — behind the wall, away from the racing " +
             "surface, which is where a real one sits. The side is taken from the paddock itself " +
             "(PaddockSpawner.TryGetArea), so it is right at every track whichever way its pit lane runs. " +
             "Off = follow Wall Side instead, like the crew.")]
    public bool standsOnPaddockSide = true;
    [Tooltip("Half the car length the wheel stations straddle (m).")]
    public float wheelLongitudinal = 1.8f;
    [Tooltip("Lateral offset (m) of a wheel station from the box centre.")]
    public float wheelLateral = 1.2f;

    [Tooltip("World height a paper-doll member is normalised to, whatever the library's pixel size. memberScale multiplies on top. Default matches the on-foot player (an 8px frame at 100 PPU drawn at scale 8), so crew read as the same size as everyone else on foot — the world is metric for cars but the people are drawn smaller than 1:1.")]
    public float memberHeightM = OnFootPersonHeight;

    // On-foot characters (player, paddock NPCs, autograph fans) all render to one figure, so generated
    // crew match the player instead of towering over them. The 8px-tall walk frame drawn at the project
    // standard is 8 / 12.8 = 0.625m -- which is right for a person seen from above beside a 5m car, and
    // replaces the old hand-set 0.64 that was 2.3% off the grid the rest of the art sits on.
    public const float OnFootPersonHeight = 8f / PixelArt.PixelsPerMetre;

    [Header("Sorting")]
    public string sortingLayerName = "Vehicles";
    [Tooltip("Crew draw above the cars (car sorting order is ~5).")]
    public int baseSortingOrder = 8;

    Material _unlit;

    void Start() => StartCoroutine(SpawnWhenReady());

    IEnumerator SpawnWhenReady()
    {
        if (track == null) track = FindFirstObjectByType<TrackBuilder>();
        // Same paper-doll library as the paddock crowd unless one is wired in the scene. Keeps the crew
        // as real walking NPCs without a serialized scene reference.
        if (crewLibrary == null) crewLibrary = Resources.Load<NPCPartLibrary>("NPC/NPCPartLibrary");
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

        // Unconfigured fallback spreads the boxes inside the grey box-lane strip (not the full lane —
        // that would drop boxes on the entry/exit ramps outside the grey surface).
        float fallbackTo = track.HasPitBoxLane ? track.PitBoxLaneTo(pitLength) : pitLength;
        float fallbackFrom = track.HasPitBoxLane ? track.PitBoxLaneFrom(pitLength) : 0f;
        for (int idx = 0; idx < count; idx++)
        {
            float boxDist = PitLane.Configured ? PitLane.BoxDistance(idx, pitLength)
                                               : Mathf.Lerp(fallbackTo, fallbackFrom, (idx + 0.5f) / count);
            BuildBox(root, idx, boxDist, pit);
        }
    }

    // Where the stand goes across the box, in the box's own local X.
    //
    // Behind the CAR, not a fixed distance off the lane centre. Cars park out on the box lane
    // (PitLane.ParkLateral, ~8 m at Watkins Glen), so a stand 6.2 m off the centreline sat between the car
    // and the racing surface — the wrong side of its own car, and in the way of the stop. Measuring from
    // the car and stepping further out puts it where a real one is: past the car, against the wall, with
    // the paddock behind it.
    float StandLateral(Transform box, float fallbackSign)
    {
        float carLateral = PitLane.Configured ? PitLane.ParkLateral : 0f;

        // No box lane configured: nothing to measure from, so fall back to the old fixed offset on
        // whichever side the paddock is.
        if (Mathf.Approximately(carLateral, 0f))
            return standLateral * StandSideSign(box, fallbackSign);

        return carLateral + Mathf.Sign(carLateral) * standBeyondCar;
    }

    // Which way is the paddock, in a pit box's own local X? +1 = the box's wall side, -1 = the other one.
    // Falls back to the crew's side when a track has no paddock to read (nothing else to go on, and being
    // beside the crew is a better wrong answer than being out on the racing line).
    float StandSideSign(Transform box, float fallbackSign)
    {
        if (!standsOnPaddockSide) return fallbackSign;
        if (!PaddockSpawner.TryGetArea(out _, out _, out Vector3 outward, out _, out _)) return fallbackSign;
        if (outward.sqrMagnitude < 1e-4f) return fallbackSign;

        float dot = Vector3.Dot(outward.normalized, box.right);
        if (Mathf.Abs(dot) < 1e-3f) return fallbackSign;   // paddock is straight up the lane: no side to pick
        return Mathf.Sign(dot);
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

        float wallSideSign = Mathf.Sign(wallSide == 0f ? 1f : wallSide);

        var box = boxGo.AddComponent<PitCrewBox>();
        box.wheelLongitudinal = wheelLongitudinal;
        box.wheelLateral = wheelLateral;
        box.Configure(idx);

        // The team's own pit box, behind the boxes on the paddock side: the one thing on pit road painted
        // in the car's colours, so a glance down the lane says whose box is whose.
        //
        // Which side that is cannot be assumed from `wallSide` — that is where the CREW stand, over the
        // wall next to the car. The stand belongs on the far side of them, away from the racing surface,
        // and which way that points depends on which hand the pit lane runs. The paddock already knows:
        // PaddockSpawner picks its own side by probing which way leads away from the track, and everything
        // else in the paddock is placed in that frame.
        if (spawnStands) PitBoxStand.Build(boxGo.transform, idx, StandLateral(boxGo.transform, wallSideSign), -0.02f);

        // Five stations: 4 wheels (corners) + 1 fueller (rear). x is lateral (wall = +wallSide), y is along lane.
        float wl = wallSideSign;
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
        // A paper-doll frame is frameHeight/pixelsPerUnit metres tall at scale 1 — normalise the member to
        // memberHeightM whatever the library's pixel size, so tiny sheets don't spawn ant-sized crew.
        float bodyScale = 1f;
        if (crewLibrary != null && layered.Build(seed))
        {
            appearance = layered;
            float frameWorldH = Mathf.Max(0.01f, crewLibrary.frameHeight / Mathf.Max(1f, crewLibrary.pixelsPerUnit));
            bodyScale = memberHeightM / frameWorldH;
            go.transform.localScale = Vector3.one * memberScale * bodyScale;
        }
        else
        {
            Destroy(layered);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Placeholder(fueller ? new Color(0.95f, 0.55f, 0.15f) : new Color(0.2f, 0.5f, 0.95f), memberHeightM);
            sr.sharedMaterial = UnlitSprite();
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = baseSortingOrder;
        }

        // Held gear, shown only while servicing. The gear sprites are metric (sized in world units at
        // scale 1), so divide the body normalisation back out of this child's scale and offset.
        // Placeholder gear is sized off the member so it stays in proportion if memberHeightM is retuned.
        var itemGo = new GameObject("Gear");
        itemGo.transform.SetParent(go.transform, false);
        itemGo.transform.localPosition = new Vector3(0f, memberHeightM * 0.15f, 0f) / bodyScale;
        itemGo.transform.localScale = Vector3.one * gearScale / bodyScale;
        var item = itemGo.AddComponent<SpriteRenderer>();
        item.sprite = fueller
            ? (fuelCanSprite != null ? fuelCanSprite : Placeholder(new Color(0.85f, 0.2f, 0.15f), memberHeightM * 0.45f))
            : (wheelSprite != null ? wheelSprite : Placeholder(new Color(0.1f, 0.1f, 0.1f), memberHeightM * 0.38f));
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
