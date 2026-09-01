using System.Collections.Generic;
using UnityEngine;

// The inside of a team's popup garage — the same "scene within a scene" trick RVInterior plays for the
// player's motorhome, standing behind every rig the PopupGarageLot parks. Walk through the door in a
// PopupGarageRig's side and the rest of the world is masked black while a meeting room is revealed;
// walk back out and the paddock comes back. No load, no camera culling, nothing else touched.
//
// How the mask works is documented at length on RVInterior and is identical here: the scene renders under
// the 3D URP renderer, so ONE giant opaque black quad at a small negative z occludes everything behind it
// (sprites are transparent — they don't write depth, but they are still depth-tested against it), the room
// draws just in front of the mask, and the walking player is pulled in front of the room. The z-planes
// below are deliberately the same numbers RVInterior uses: only one interior can hold the player at a
// time, and keeping one z scheme across both means anything authored for the RV reads the same in here.
//
// Differences from the RV, all of them because there are dozens of these rather than one:
//   * The room is BUILT LAZILY, the first time the player comes within buildRange. A full entry list is
//     40-odd garages; generating 40 rooms nobody walks into would cost a scene-load's worth of objects
//     for nothing.
//   * The player is re-resolved from OnFootController.Current rather than handed in once, because the
//     walking body is destroyed and respawned across a session while these rigs stay parked.
//   * The doorway is off-centre in the front wall (it sits toward the cab, past the parked car), so the
//     wall gap is placed from the rig's real door rather than assumed to be in the middle.
public class PopupGarageInterior : MonoBehaviour
{
    // Every garage interior in the scene, and the one the player is currently stood in (null nearly
    // always). Registered the same way OnFootController, RVInterior and NPCInteractable keep theirs, so
    // asking "is the player indoors" is a list read rather than a scene walk.
    public static readonly List<PopupGarageInterior> All = new();

    public static PopupGarageInterior Occupied
    {
        get
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i] != null && All[i].IsInside) return All[i];
            return null;
        }
    }

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() => All.Remove(this);

    [Header("Doorway")]
    [Tooltip("Dead-band around the doorway (m) so the view can't flicker while stood in the threshold.")]
    public float hysteresis = 0.4f;
    [Tooltip("How far past the body's edge (m) the door line sits. Big enough that the shallow collider notch still lets the player's own collider cross the enter threshold.")]
    public float doorLip = 0.35f;

    [Header("Build")]
    [Tooltip("Distance (m) from the player at which this room is generated. Rooms nobody walks near are never built at all.")]
    public float buildRange = 25f;

    // World z-planes, matching RVInterior. More negative = closer to the camera, which sits at
    // player.z - 100 looking down +z, so each layer draws in front of the one below it.
    const float kMaskZ = -2.0f;
    const float kFloorZ = -2.2f;
    const float kWallZ = -2.25f;
    const float kPropZ = -2.3f;
    const float kInsidePlayerZ = -2.5f;

    const float kMaskHalfSize = 200f;   // giant, so it covers the frame at any on-foot camera position/zoom
    const float kWallThickness = 0.25f;

    PopupGarageRig _rig;
    Transform _player;
    Vector2 _anchorXY;        // body centre, in the ground plane: the room's (0,0)
    Vector2 _doorDir;         // world direction from the anchor out through the doorway
    float _roomWidth;         // across the doorway — the rig's LENGTH (the long axis runs across the door)
    float _roomBack, _roomFront;
    float _doorOffset;        // where the doorway sits along the room's local X (0 = middle of the wall)

    GameObject _insideView;   // mask + room, toggled as one
    bool _built;
    bool _inside;
    float _outsidePlayerZ;    // the player's z before we pulled them in front of the mask

    // Whether the player is in this room right now.
    public bool IsInside => _inside;
    public PopupGarageRig Rig => _rig;
    public bool Built => _built;

    // The room's frame: +Y points out of the doorway, +X is a quarter turn clockwise from it — the same
    // convention RVInterior uses, so its EvaluateInside test can be shared verbatim below.
    public Transform InteriorRoot { get; private set; }

    readonly List<Material> _mats = new();   // team-coloured materials owned by this room

    // Stand a room up behind `rig`. Nothing is generated here: the geometry is measured off the rig and
    // the room itself waits until the player is close enough to be able to walk into it.
    //
    // The component is deliberately NOT parented to the rig: the mask and the room quads are authored in
    // world z, and hanging them under a rig sat at z = -0.5 would shift every plane by half a metre.
    public static PopupGarageInterior Create(Transform parent, PopupGarageRig rig)
    {
        if (rig == null) return null;

        var go = new GameObject($"GarageInterior_{rig.carNumber}");
        go.transform.SetParent(parent, false);

        var interior = go.AddComponent<PopupGarageInterior>();
        interior.Initialize(rig);
        return interior;
    }

    public void Initialize(PopupGarageRig rig)
    {
        _rig = rig;
        if (_rig == null) return;

        Vector3 anchor = _rig.transform.position;
        _anchorXY = new Vector2(anchor.x, anchor.y);
        _doorDir = _rig.DoorWorldDirection;
        if (_doorDir.sqrMagnitude < 0.0001f) _doorDir = Vector2.right;

        // Where the rig's own doorway is, measured in the room's frame: how far ahead of the anchor
        // (the door line) and how far along the wall (the gap's offset). Reading both off the rig rather
        // than assuming a centred door is what lets the way in sit past the parked car's nose.
        Vector2 doorRel = (Vector2)_rig.DoorWorldPosition - _anchorXY;
        Vector2 right = new Vector2(_doorDir.y, -_doorDir.x);
        _roomFront = Vector2.Dot(doorRel, _doorDir) + doorLip;
        _doorOffset = Vector2.Dot(doorRel, right);

        _roomBack = _rig.bodyWidth * 0.5f;
        _roomWidth = _rig.bodyLength;

        // Sat in the ground plane: a Z-rotation on the room root below preserves world z, so each quad's
        // local z is also its world z.
        transform.position = new Vector3(_anchorXY.x, _anchorXY.y, 0f);
        transform.rotation = Quaternion.identity;
    }

    void Update()
    {
        if (_rig == null) return;

        // The walking body comes and goes — the player climbs into the car, the scene respawns them —
        // so it is looked up rather than cached. OnFootController keeps a register; this is a list read.
        var walker = OnFootController.Current;
        _player = walker != null ? walker.transform : null;

        if (_player == null || !_player.gameObject.activeInHierarchy)
        {
            if (_inside) LeaveWithoutPlayer();
            return;
        }

        Vector2 rel = (Vector2)_player.position - _anchorXY;

        // Cheap reject first: everything below only matters within arm's length of this one rig, and
        // there is a paddock full of them.
        if (!_built)
        {
            if (rel.sqrMagnitude > buildRange * buildRange) return;
            BuildNow();
        }

        float localForward = Vector2.Dot(rel, _doorDir);
        float localRight = Vector2.Dot(rel, new Vector2(_doorDir.y, -_doorDir.x));

        // Same threshold rule as the motorhome, hysteresis and all — one implementation, so the two
        // doorways can never drift apart in feel.
        bool nowInside = RVInterior.EvaluateInside(_inside, localForward, localRight,
                                                   _roomFront, _roomBack, _roomWidth * 0.5f, hysteresis);
        if (nowInside != _inside) SetInside(nowInside);

        // Hold the player in front of the mask while inside, in case anything else writes their z.
        if (_inside)
        {
            var p = _player.position;
            if (!Mathf.Approximately(p.z, kInsidePlayerZ))
                _player.position = new Vector3(p.x, p.y, kInsidePlayerZ);
        }
    }

    // The player vanished while the room was up (they got in the car from inside a cutscene, the scene is
    // tearing down). Put the world back without touching a transform that is no longer there.
    void LeaveWithoutPlayer()
    {
        _inside = false;
        if (_insideView != null) _insideView.SetActive(false);
        if (_rig != null) _rig.SetCollidersEnabled(true);
    }

    void SetInside(bool inside)
    {
        if (inside == _inside) return;

        // Capture the outside z on the way IN, not once at build time: the walking body is respawned
        // across a session and may come back on a different plane.
        if (inside && _player != null) _outsidePlayerZ = _player.position.z;

        _inside = inside;
        if (_insideView != null) _insideView.SetActive(inside);

        // Swap which shell is solid: the room's own walls toggle with the view, and the rig's body
        // colliders must only block from outside — they overlap this floor.
        if (_rig != null) _rig.SetCollidersEnabled(!inside);

        if (_player != null)
        {
            var p = _player.position;
            _player.position = new Vector3(p.x, p.y, inside ? kInsidePlayerZ : _outsidePlayerZ);
        }
    }

    // ---------------------------------------------------------------- the room

    // Generate the room. Called for you the first time the player walks within buildRange; public so a
    // tool (or a test) can stand one up without waiting for somebody to walk over. Safe to call twice.
    public void BuildNow()
    {
        if (_built || _rig == null) return;
        _built = true;

        _insideView = new GameObject("InsideView");
        _insideView.transform.SetParent(transform, false);

        // One giant opaque black quad centred on the anchor: it occludes the whole world behind it while
        // the room draws in front of it.
        PaddockProps.Quad(_insideView.transform, "BlackMask", Vector2.zero,
                          new Vector2(kMaskHalfSize * 2f, kMaskHalfSize * 2f), kMaskZ, Black());

        var room = new GameObject("Interior").transform;
        room.SetParent(_insideView.transform, false);
        float doorAngle = Mathf.Atan2(_doorDir.y, _doorDir.x) * Mathf.Rad2Deg - 90f;   // local +Y onto the door
        room.localRotation = Quaternion.Euler(0f, 0f, doorAngle);
        InteriorRoot = room;

        BuildShell(room);
        BuildMeetingArea(room);
        BuildWorkArea(room);

        _insideView.SetActive(false);   // built on approach, shown only once the player is through the door
    }

    // Floor and walls, with the gap in the front wall exactly where the rig's doorway is.
    void BuildShell(Transform room)
    {
        float halfW = _roomWidth * 0.5f;
        float depth = _roomBack + _roomFront;
        float centreY = (_roomFront - _roomBack) * 0.5f;

        PaddockProps.Quad(room, "Floor", new Vector2(0f, centreY), new Vector2(_roomWidth, depth), kFloorZ, FloorMat());

        var wall = WallMat();
        float span = depth + kWallThickness;
        PaddockProps.Quad(room, "WallBack", new Vector2(0f, -_roomBack), new Vector2(_roomWidth + kWallThickness, kWallThickness), kWallZ, wall, solid: true);
        PaddockProps.Quad(room, "WallLeft", new Vector2(-halfW, centreY), new Vector2(kWallThickness, span), kWallZ, wall, solid: true);
        PaddockProps.Quad(room, "WallRight", new Vector2(halfW, centreY), new Vector2(kWallThickness, span), kWallZ, wall, solid: true);

        // Front wall in two pieces, leaving the doorway open where the shell's notch is.
        float doorWidth = _rig != null ? _rig.doorWidth : 1.6f;
        float gapMin = _doorOffset - doorWidth * 0.5f;
        float gapMax = _doorOffset + doorWidth * 0.5f;

        float leftLen = gapMin - (-halfW);
        if (leftLen > 0.05f)
            PaddockProps.Quad(room, "WallFrontL", new Vector2((-halfW + gapMin) * 0.5f, _roomFront), new Vector2(leftLen, kWallThickness), kWallZ, wall, solid: true);
        float rightLen = halfW - gapMax;
        if (rightLen > 0.05f)
            PaddockProps.Quad(room, "WallFrontR", new Vector2((gapMax + halfW) * 0.5f, _roomFront), new Vector2(rightLen, kWallThickness), kWallZ, wall, solid: true);

        PaddockProps.Quad(room, "Doormat", new Vector2(_doorOffset, _roomFront - 0.45f), new Vector2(doorWidth, 0.5f), kPropZ, MatteMat());
    }

    // What the garage is FOR, as far as the player is concerned: somewhere the team sits down together.
    // A long table with chairs round it, a setup board on the wall behind, all at the end of the rig away
    // from the door so walking in doesn't put you in somebody's seat.
    void BuildMeetingArea(Transform room)
    {
        float halfW = _roomWidth * 0.5f;
        // Away from the doorway. With the door toward one end (it sits past the parked car), that puts the
        // table at the other; a centred door falls back to the +X half.
        float side = _doorOffset > 0f ? -1f : 1f;
        float tableX = side * Mathf.Min(halfW - 2.2f, halfW * 0.45f);
        float tableLen = Mathf.Min(3.4f, _roomWidth * 0.38f);

        var furniture = FurnitureMat();
        var seat = AccentMat(_rig != null ? _rig.secondary : new Color(0.6f, 0.6f, 0.6f));

        PaddockProps.Quad(room, "MeetingTable", new Vector2(tableX, 0f), new Vector2(tableLen, 1.15f), kPropZ, furniture);

        // Three seats along each side of the table and one at each end — a crew debrief, not a dinette.
        float step = tableLen / 3f;
        for (int i = -1; i <= 1; i++)
        {
            PaddockProps.Quad(room, $"Chair_N{i + 1}", new Vector2(tableX + i * step, 0.95f), new Vector2(0.55f, 0.55f), kPropZ, seat);
            PaddockProps.Quad(room, $"Chair_S{i + 1}", new Vector2(tableX + i * step, -0.95f), new Vector2(0.55f, 0.55f), kPropZ, seat);
        }
        PaddockProps.Quad(room, "Chair_E", new Vector2(tableX + tableLen * 0.5f + 0.5f, 0f), new Vector2(0.55f, 0.55f), kPropZ, seat);
        PaddockProps.Quad(room, "Chair_W", new Vector2(tableX - tableLen * 0.5f - 0.5f, 0f), new Vector2(0.55f, 0.55f), kPropZ, seat);

        // The setup board on the wall behind the table: white, with a header in the team's primary so
        // whose garage you are stood in is readable from the doorway.
        float boardY = -_roomBack + 0.42f;
        PaddockProps.Quad(room, "SetupBoard", new Vector2(tableX, boardY), new Vector2(tableLen + 0.4f, 0.55f), kPropZ, BoardMat());
        PaddockProps.Quad(room, "BoardHeader", new Vector2(tableX, boardY + 0.2f), new Vector2(tableLen + 0.4f, 0.16f), kPropZ - 0.02f,
                          AccentMat(_rig != null ? _rig.primary : Color.white));
    }

    // The working half, by the door: a bench along the back wall, a toolbox in the team's colours and a
    // stack of tyres. Placeholder blocks, same as every other paddock prop — they are here to make the
    // room read as a garage rather than as an empty box.
    void BuildWorkArea(Transform room)
    {
        float halfW = _roomWidth * 0.5f;
        float side = _doorOffset > 0f ? 1f : -1f;
        float benchX = side * Mathf.Min(halfW - 1.6f, halfW * 0.5f);

        PaddockProps.Quad(room, "Bench", new Vector2(benchX, -_roomBack + 0.55f), new Vector2(2.6f, 0.85f), kPropZ, FurnitureMat());
        PaddockProps.Quad(room, "Toolbox", new Vector2(benchX - side * 1.7f, -_roomBack + 0.6f), new Vector2(0.9f, 0.8f), kPropZ,
                          AccentMat(_rig != null ? _rig.primary : Color.white));
        PaddockProps.Quad(room, "Tyres", new Vector2(benchX + side * 1.6f, -_roomBack + 0.7f), new Vector2(1.1f, 1.1f), kPropZ, RubberMat());
    }

    // --- materials --------------------------------------------------------------------------------
    //
    // The plain surfaces are shared by every garage in the paddock (there are dozens); only the two
    // team colours are per-room, and those are released with the room.

    static Material _black, _floor, _wall, _furniture, _board, _rubber, _matte;

    static Material Black() => _black != null ? _black : (_black = PaddockProps.Unlit(Color.black));
    static Material FloorMat() => _floor != null ? _floor : (_floor = PaddockProps.Unlit(new Color(0.42f, 0.43f, 0.47f)));
    static Material WallMat() => _wall != null ? _wall : (_wall = PaddockProps.Unlit(new Color(0.16f, 0.17f, 0.20f)));
    static Material FurnitureMat() => _furniture != null ? _furniture : (_furniture = PaddockProps.Unlit(new Color(0.27f, 0.29f, 0.33f)));
    static Material BoardMat() => _board != null ? _board : (_board = PaddockProps.Unlit(new Color(0.90f, 0.91f, 0.93f)));
    static Material RubberMat() => _rubber != null ? _rubber : (_rubber = PaddockProps.Unlit(new Color(0.09f, 0.09f, 0.10f)));
    static Material MatteMat() => _matte != null ? _matte : (_matte = PaddockProps.Unlit(new Color(0.22f, 0.23f, 0.25f)));

    Material AccentMat(Color colour)
    {
        var m = PaddockProps.Unlit(colour);
        _mats.Add(m);
        return m;
    }

    void OnDestroy()
    {
        foreach (var m in _mats) if (m != null) Destroy(m);
        _mats.Clear();
    }
}
