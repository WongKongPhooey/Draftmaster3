using System.Collections.Generic;
using UnityEngine;

// "Scene within a scene" for the player's RV/motorhome. Standing outside, you see the scene (and the
// RV exterior sprite) normally. Walk through the doorway and the rest of the world is masked black
// while a small interior room is revealed — like stepping into a separate scene, but with no load.
//
// How the mask works (no camera culling, no touching other objects):
//   The scene renders under the 3D URP renderer, where opaque geometry writes depth and closer-to-camera
//   fragments occlude farther ones (the same z-layering PaddockSpawner documents: a negative-z opaque quad
//   sits IN FRONT of the z=0 ground plane and hides it). So while inside we switch on one giant opaque
//   black quad at a small negative z — it occludes the entire world behind it — then draw the interior
//   room's opaque quads slightly in front of the mask, and pull the walking player's transform in front
//   of the interior so the sprite (transparent, always drawn over opaque) stays visible. Stepping back out
//   through the door switches the mask + interior off and restores the player's z, revealing the world again.
//
// Self-configuring: PitLaneStart builds one and calls Initialize() with the RV spawn marker, the walking
// player, and the parked car (used only to orient the doorway toward where the player must walk). No scene
// wiring or art is required.
//
// Editing the interior: the room is hand-authorable as a prefab. Run Draftmaster > RV Interior > Build
// Prefab to generate Assets/Resources/OnFoot/RVInterior.prefab (this component at the root, the room as
// sprite children under an "Interior" child), then open it in Prefab Mode and rearrange/restyle/add art
// freely. PitLaneStart instantiates that prefab when it exists; Initialize() detects the authored
// "Interior" child and uses it instead of building the procedural placeholder room. Author in the
// Interior child's local frame: (0,0) is where the player spawns, +Y points at the doorway (Initialize
// rotates the whole room so +Y faces the parked car). Keep art z between the -2.0 mask and the -2.5
// player (the builder's defaults follow the k*Z constants below). If no prefab exists, or its Interior
// child is missing, the procedural room below is used; assigning interiorSprite swaps the procedural
// room for a single hand-drawn floor-plan sprite.
public class RVInterior : MonoBehaviour
{
    [Header("Room size (metres, local to the doorway)")]
    [Tooltip("Interior width across the doorway. With the RV's side door this is the RV's LENGTH (the long axis runs across the door).")]
    public float roomWidth = 9.6f;
    [Tooltip("Depth of the room behind the spawn point (away from the door).")]
    public float roomBack = 1.85f;
    [Tooltip("Distance from the spawn point forward to the doorway. Crossing this line (out the side door) steps outside. Slightly past the body edge so the shallow collider notch still lets the player's collider cross the enter threshold (roomFront - hysteresis).")]
    public float roomFront = 2.3f;
    [Tooltip("Width of the door opening in the front wall.")]
    public float doorWidth = 1.4f;
    [Tooltip("Dead-band around the doorway so the view doesn't flicker while standing in the threshold.")]
    public float hysteresis = 0.4f;

    [Header("Art (optional)")]
    [Tooltip("If set, the interior floor is this sprite instead of the procedural placeholder room. Sized to roomWidth x (roomBack+roomFront).")]
    public Sprite interiorSprite;

    [Header("Satnav")]
    [Tooltip("Interaction range (m) for the driver-seat satnav that opens the travel map. Walk within this to see the prompt.")]
    public float satnavRange = 2f;

    [Header("Laptop")]
    [Tooltip("Interaction range (m) for the laptop on the table that opens the garage screen. Walk within this to see the prompt.")]
    public float laptopRange = 1.6f;

    // World z-planes. More negative = closer to the camera (which sits at player.z - 100 looking +z), so
    // each layer draws in front of the one below it. The player is pulled to insidePlayerZ while inside.
    const float kMaskZ = -2.0f;
    const float kFloorZ = -2.2f;
    const float kWallZ = -2.25f;
    const float kPropZ = -2.3f;
    const float kInsidePlayerZ = -2.5f;

    const float kMaskHalfSize = 200f; // giant, so it covers the frame at any on-foot camera position/zoom
    const float kWallThickness = 0.25f;

    // The dinette table, in the interior's local frame. Shared: the procedural room draws the table here
    // and the laptop is placed on top of it, so the two can't drift apart.
    static readonly Vector2 kTablePos = new(1.8f, 0.6f);

    Transform _player;
    Vector2 _anchorXY;
    Vector2 _doorDir = Vector2.down;   // world direction from the spawn toward the doorway (RV facing, or toward the car)
    RVExterior _exterior;              // placed RV shell; its solid colliders are switched off while inside
    GameObject _insideView;            // mask + interior root; toggled as one
    bool _inside;
    float _outsidePlayerZ;             // the player's z before we ever pulled them into the interior
    bool _initialised;

    // The interior's own frame: +Y points at the doorway, (0,0) is where the player stands on entry.
    // Public so things that belong INSIDE the RV can be parented into it — the weekend's debrief happens
    // at this dinette, with the engineer sat across the table, and it has to be inside the mask or it draws
    // over the blackout with the rest of the world.
    public Transform InteriorRoot { get; private set; }

    // Where the dinette table is in that frame, so anything put "at the table" agrees with the table.
    public static Vector2 TableLocal => kTablePos;

    // Local z the interior's props sit at.
    public static float InteriorPropZ => kPropZ;

    // Whether the player is currently in the masked interior. Cutscene/trigger logic outside the
    // RV gates on this so nothing fires while the world is masked.
    public bool IsInside => _inside;

    readonly List<Material> _mats = new(); // owned runtime materials, released on destroy

    // Pure room-bounds test, split out so the state logic can be reasoned about on its own.
    // localForward = how far the player is past the spawn toward the door; localRight = lateral offset.
    // Inside = within the room's box: short of the door line ahead, within halfWidth to the sides, and
    // not past backDistance behind — so approaching the RV from a side or the back never re-masks; only
    // the doorway does. Hysteresis grows the box while inside and shrinks it while outside, keeping each
    // pair of switch points apart so hovering on an edge can't rapidly toggle the view.
    public static bool EvaluateInside(bool wasInside, float localForward, float localRight, float doorDistance, float backDistance, float halfWidth, float hysteresis)
    {
        float h = wasInside ? hysteresis : -hysteresis;
        return localForward < doorDistance + h
            && localForward > -backDistance - h
            && Mathf.Abs(localRight) < halfWidth + h;
    }

    // anchorWorld: the RV spawn point (player starts here, inside). player: the walking player transform.
    // exterior: the placed RV shell — its up orients the doorway, and its solid colliders are switched
    // off while inside (the interior's own wall colliders take over) and back on when stepping out.
    // car: fallback doorway orientation when there is no exterior — only its position is used, to point
    // the door the way the player must walk.
    public void Initialize(Vector3 anchorWorld, Transform player, Transform car, RVExterior exterior = null)
    {
        _player = player;
        _exterior = exterior;
        _anchorXY = new Vector2(anchorWorld.x, anchorWorld.y);

        if (exterior != null)
        {
            _doorDir = exterior.DoorWorldDirection;
        }
        else if (car != null)
        {
            Vector2 toCar = (Vector2)car.position - _anchorXY;
            if (toCar.sqrMagnitude > 0.0001f) _doorDir = toCar.normalized;
        }

        // Anchor this object at the spawn, in the ground plane. A Z-rotation (below, on the interior root)
        // preserves world z, so each quad's local z equals its world z.
        transform.position = new Vector3(_anchorXY.x, _anchorXY.y, 0f);

        _outsidePlayerZ = _player != null ? _player.position.z : 0f;

        BuildView();
        _initialised = true;

        // The player spawns at the anchor, i.e. inside — open the scene already in the interior.
        SetInside(true, force: true);
    }

    void BuildView()
    {
        // A pre-existing "Interior" child means we were instantiated from the hand-authored prefab
        // (see the class comment) — adopt it instead of generating the placeholder room.
        Transform authored = transform.Find("Interior");

        _insideView = new GameObject("InsideView");
        _insideView.transform.SetParent(transform, false);

        BuildMask(_insideView.transform);

        // Interior root is rotated so its local +Y points at the doorway (the direction the player walks
        // out). Everything below is authored in that local frame: +Y = toward the door, +X = to the right.
        Transform interior;
        if (authored != null)
        {
            authored.SetParent(_insideView.transform, false);
            interior = authored;
        }
        else
        {
            interior = new GameObject("Interior").transform;
            interior.SetParent(_insideView.transform, false);
        }
        InteriorRoot = interior;
        float doorAngle = Mathf.Atan2(_doorDir.y, _doorDir.x) * Mathf.Rad2Deg - 90f; // map local +Y onto doorDir
        interior.localRotation = Quaternion.Euler(0f, 0f, doorAngle);

        if (authored == null)
        {
            if (interiorSprite != null) BuildSpriteFloor(interior);
            else BuildProceduralRoom(interior);
        }

        // The authored prefab carries its own satnav; only generate one when it doesn't.
        if (interior.GetComponentInChildren<SatnavInteractable>(true) == null)
            BuildSatnav(interior);
        if (interior.GetComponentInChildren<LaptopInteractable>(true) == null)
            BuildLaptop(interior);
    }

    // The laptop on the dinette table: the only way into the garage screen from a race weekend. Same
    // arrangement as the satnav — visuals plus a co-located empty carrying the interactable — so the
    // walk-up prompt and action button come free from OnFootController.
    //
    // The hand-authored prefab puts its table somewhere else than the procedural room does, so the spot
    // is taken from whatever "Table" the room actually has and only falls back to the procedural one.
    void BuildLaptop(Transform interior)
    {
        Transform table = interior.Find("Table");
        Vector2 at = table != null ? (Vector2)table.localPosition : kTablePos;

        // Lid (with a lit screen on it) behind the keyboard slab, so the thing reads as open from above.
        BuildQuad(interior, "LaptopLid", at + new Vector2(0f, 0.16f), new Vector2(0.52f, 0.30f), kPropZ - 0.02f, MakeUnlit(new Color(0.13f, 0.14f, 0.16f)));
        BuildQuad(interior, "LaptopScreen", at + new Vector2(0f, 0.16f), new Vector2(0.44f, 0.22f), kPropZ - 0.04f, MakeUnlit(new Color(0.36f, 0.66f, 0.85f)));
        BuildQuad(interior, "LaptopBase", at + new Vector2(0f, -0.06f), new Vector2(0.52f, 0.26f), kPropZ - 0.02f, MakeUnlit(new Color(0.22f, 0.23f, 0.26f)));

        var go = new GameObject("Laptop");
        go.transform.SetParent(interior, false);
        go.transform.localPosition = new Vector3(at.x, at.y, kPropZ);
        var laptop = go.AddComponent<LaptopInteractable>();
        laptop.interactRange = laptopRange;
        laptop.speakerName = "Laptop";  // the base default ("Crew Member") is nobody here
        laptop.turnsToFace = false;     // a laptop on a table doesn't swivel to look at you
    }

    // A satnav at the RV's driver seat (front-left of the cab, beside the doorway). Its floating prompt +
    // action-button handling are inherited from NPCInteractable via OnFootController; interacting opens the
    // travel map. Built under the interior root so it only exists — and only prompts — while inside the RV.
    void BuildSatnav(Transform interior)
    {
        float frontY = roomFront - 0.5f;                        // near the front wall, level with the door
        float x = -Mathf.Min(1.9f, roomWidth * 0.5f - 0.6f);   // driver side (left), kept clear of the wall
        Vector2 unit = new(x, frontY);

        // Driver's seat block just behind the unit, so the spot reads as the front cab.
        BuildQuad(interior, "DriverSeat", new Vector2(x, frontY - 0.85f), new Vector2(1.0f, 1.1f), kPropZ, MakeUnlit(new Color(0.16f, 0.16f, 0.18f)));
        // Satnav housing + lit screen.
        BuildQuad(interior, "SatnavBody", unit, new Vector2(0.62f, 0.44f), kPropZ - 0.02f, MakeUnlit(new Color(0.08f, 0.08f, 0.09f)));
        BuildQuad(interior, "SatnavScreen", unit, new Vector2(0.44f, 0.30f), kPropZ - 0.04f, MakeUnlit(new Color(0.20f, 0.72f, 0.55f)));

        // Co-located empty child carries the interactable. Kept separate from the visuals so the
        // face-each-other rotation OnFootController applies on interact never spins the device.
        var go = new GameObject("Satnav");
        go.transform.SetParent(interior, false);
        go.transform.localPosition = new Vector3(unit.x, unit.y, kPropZ);
        var sat = go.AddComponent<SatnavInteractable>();
        sat.interactRange = satnavRange;
        sat.speakerName = "Satnav";   // shown above its dialogue; the base default ("Crew Member") is nobody here
        sat.turnsToFace = false;      // a fitted device doesn't turn to look at you
    }

    void BuildMask(Transform parent)
    {
        // One giant opaque black quad, centred on the anchor at a small negative z: it occludes the whole
        // world behind it (everything at/around the ground plane) while the interior draws in front of it.
        BuildQuad(parent, "BlackMask", Vector2.zero, new Vector2(kMaskHalfSize * 2f, kMaskHalfSize * 2f), kMaskZ, MakeUnlit(Color.black));
    }

    void BuildSpriteFloor(Transform interior)
    {
        float w = roomWidth;
        float h = roomBack + roomFront;
        var mat = MakeSpriteUnlit(interiorSprite.texture);
        // Centre the floor between the back wall and the door.
        BuildQuad(interior, "FloorSprite", new Vector2(0f, (roomFront - roomBack) * 0.5f), new Vector2(w, h), kFloorZ, mat);
    }

    void BuildProceduralRoom(Transform interior)
    {
        float halfW = roomWidth * 0.5f;
        float halfDoor = doorWidth * 0.5f;
        float centreY = (roomFront - roomBack) * 0.5f;
        float depth = roomBack + roomFront;

        // Floor.
        BuildQuad(interior, "Floor", new Vector2(0f, centreY), new Vector2(roomWidth, depth), kFloorZ, MakeUnlit(new Color(0.60f, 0.48f, 0.33f)));

        // Walls: a dark frame around the floor with a gap at the door (front, +Y). Solid, so the player
        // can only leave through the doorway (colliders toggle with InsideView, so they never block
        // anything while the interior is hidden).
        var wallMat = MakeUnlit(new Color(0.28f, 0.22f, 0.17f));
        float wallSpan = depth + kWallThickness;
        BuildQuad(interior, "WallBack", new Vector2(0f, -roomBack), new Vector2(roomWidth + kWallThickness, kWallThickness), kWallZ, wallMat, withCollider: true);
        BuildQuad(interior, "WallLeft", new Vector2(-halfW, centreY), new Vector2(kWallThickness, wallSpan), kWallZ, wallMat, withCollider: true);
        BuildQuad(interior, "WallRight", new Vector2(halfW, centreY), new Vector2(kWallThickness, wallSpan), kWallZ, wallMat, withCollider: true);
        // Front wall in two segments, leaving the doorway open in the middle.
        float segW = halfW - halfDoor;
        if (segW > 0.01f)
        {
            float segCentre = (halfW + halfDoor) * 0.5f;
            BuildQuad(interior, "WallFrontL", new Vector2(-segCentre, roomFront), new Vector2(segW, kWallThickness), kWallZ, wallMat, withCollider: true);
            BuildQuad(interior, "WallFrontR", new Vector2(segCentre, roomFront), new Vector2(segW, kWallThickness), kWallZ, wallMat, withCollider: true);
        }

        // A few furnishings so the room reads as a motorhome interior. All placeholder blocks — replace by
        // assigning interiorSprite. Kept clear of the origin (0,0), where the player spawns.
        Vector2 tablePos = kTablePos;
        BuildQuad(interior, "Rug", tablePos, new Vector2(2.4f, 1.7f), kWallZ, MakeUnlit(new Color(0.55f, 0.20f, 0.18f)));
        BuildQuad(interior, "Bed", new Vector2(-halfW + 1.6f, -roomBack + 1.3f), new Vector2(2.7f, 1.9f), kPropZ, MakeUnlit(new Color(0.78f, 0.80f, 0.86f)));
        BuildQuad(interior, "BedPillow", new Vector2(-halfW + 1.6f, -roomBack + 2.0f), new Vector2(2.4f, 0.6f), kPropZ - 0.02f, MakeUnlit(new Color(0.92f, 0.93f, 0.96f)));
        BuildQuad(interior, "Counter", new Vector2(halfW - 1.3f, -roomBack + 0.55f), new Vector2(2.4f, 0.9f), kPropZ, MakeUnlit(new Color(0.70f, 0.71f, 0.74f)));
        BuildQuad(interior, "Table", tablePos, new Vector2(1.4f, 1.0f), kPropZ, MakeUnlit(new Color(0.48f, 0.34f, 0.22f)));
        BuildQuad(interior, "Doormat", new Vector2(0f, roomFront - 0.4f), new Vector2(doorWidth, 0.5f), kPropZ, MakeUnlit(new Color(0.35f, 0.30f, 0.24f)));
    }

    void Update()
    {
        if (!_initialised || _player == null || !_player.gameObject.activeInHierarchy) return;

        Vector2 rel = (Vector2)_player.position - _anchorXY;
        float localForward = Vector2.Dot(rel, _doorDir);
        float localRight = Vector2.Dot(rel, new Vector2(_doorDir.y, -_doorDir.x));
        bool nowInside = EvaluateInside(_inside, localForward, localRight, roomFront, roomBack, roomWidth * 0.5f, hysteresis);
        if (nowInside != _inside) SetInside(nowInside);

        // Keep the player pulled in front of the mask every frame while inside, in case anything else
        // writes their z (2D physics leaves z alone, but this is belt-and-braces).
        if (_inside)
        {
            var p = _player.position;
            if (!Mathf.Approximately(p.z, kInsidePlayerZ))
                _player.position = new Vector3(p.x, p.y, kInsidePlayerZ);
        }
    }

    void SetInside(bool inside, bool force = false)
    {
        if (inside == _inside && !force) return;
        _inside = inside;
        if (_insideView != null) _insideView.SetActive(inside);

        // Swap which shell is solid: the interior's wall colliders toggle with InsideView above, while
        // the exterior's body colliders must only block from outside (they overlap the room's floor).
        if (_exterior != null) _exterior.SetCollidersEnabled(!inside);

        if (_player != null)
        {
            var p = _player.position;
            float z = inside ? kInsidePlayerZ : _outsidePlayerZ;
            _player.position = new Vector3(p.x, p.y, z);
        }
    }

    // --- geometry / material helpers -------------------------------------------------------------

    // Axis-aligned quad in the parent's local space, centred at centreLocal with the given size, at localZ.
    // Double-sided (both windings) so it's never backface-culled regardless of which side the camera is on.
    // withCollider adds a solid BoxCollider2D matching the quad (used for the walls).
    static GameObject BuildQuad(Transform parent, string name, Vector2 centreLocal, Vector2 size, float localZ, Material mat, bool withCollider = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(centreLocal.x, centreLocal.y, localZ);
        if (withCollider) go.AddComponent<BoxCollider2D>().size = size;

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        float hx = size.x * 0.5f, hy = size.y * 0.5f;
        var mesh = new Mesh { name = name };
        mesh.SetVertices(new List<Vector3> { new(-hx, -hy, 0f), new(hx, -hy, 0f), new(hx, hy, 0f), new(-hx, hy, 0f) });
        mesh.SetUVs(0, new List<Vector2> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) });
        mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 }, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
        return go;
    }

    Material MakeUnlit(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        _mats.Add(m);
        return m;
    }

    Material MakeSpriteUnlit(Texture tex)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        if (tex != null) m.mainTexture = tex;
        _mats.Add(m);
        return m;
    }

    void OnDestroy()
    {
        foreach (var m in _mats) if (m != null) Destroy(m);
        _mats.Clear();
    }
}
