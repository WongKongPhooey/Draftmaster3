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
// wiring or art is required. Assign interiorSprite to drop a hand-drawn floor plan in place of the
// procedural placeholder room.
public class RVInterior : MonoBehaviour
{
    [Header("Room size (metres, local to the doorway)")]
    [Tooltip("Interior width across the doorway.")]
    public float roomWidth = 8f;
    [Tooltip("Depth of the room behind the spawn point (away from the door).")]
    public float roomBack = 3f;
    [Tooltip("Distance from the spawn point forward to the doorway. Crossing this line (toward the car) steps outside.")]
    public float roomFront = 2.5f;
    [Tooltip("Width of the door opening in the front wall.")]
    public float doorWidth = 1.8f;
    [Tooltip("Dead-band around the doorway so the view doesn't flicker while standing in the threshold.")]
    public float hysteresis = 0.4f;

    [Header("Art (optional)")]
    [Tooltip("If set, the interior floor is this sprite instead of the procedural placeholder room. Sized to roomWidth x (roomBack+roomFront).")]
    public Sprite interiorSprite;

    // World z-planes. More negative = closer to the camera (which sits at player.z - 100 looking +z), so
    // each layer draws in front of the one below it. The player is pulled to insidePlayerZ while inside.
    const float kMaskZ = -2.0f;
    const float kFloorZ = -2.2f;
    const float kWallZ = -2.25f;
    const float kPropZ = -2.3f;
    const float kInsidePlayerZ = -2.5f;

    const float kMaskHalfSize = 200f; // giant, so it covers the frame at any on-foot camera position/zoom
    const float kWallThickness = 0.25f;

    Transform _player;
    Vector2 _anchorXY;
    Vector2 _doorDir = Vector2.down;   // world direction from the spawn toward the doorway (and the car)
    GameObject _insideView;            // mask + interior root; toggled as one
    bool _inside;
    float _outsidePlayerZ;             // the player's z before we ever pulled them into the interior
    bool _initialised;

    readonly List<Material> _mats = new(); // owned runtime materials, released on destroy

    // Pure doorway test, split out so the state logic can be reasoned about on its own.
    // localForward = how far the player is past the spawn toward the door. Hysteresis keeps the two
    // switch points apart so standing in the doorway can't rapidly toggle the view.
    public static bool EvaluateInside(bool wasInside, float localForward, float doorDistance, float hysteresis)
    {
        return wasInside
            ? localForward < doorDistance + hysteresis   // stay inside until clearly past the door
            : localForward < doorDistance - hysteresis;  // step well back inside before re-entering
    }

    // anchorWorld: the RV spawn point (player starts here, inside). player: the walking player transform.
    // car: the parked car — only its position is used, to point the doorway the way the player must walk.
    public void Initialize(Vector3 anchorWorld, Transform player, Transform car)
    {
        _player = player;
        _anchorXY = new Vector2(anchorWorld.x, anchorWorld.y);

        if (car != null)
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
        _insideView = new GameObject("InsideView");
        _insideView.transform.SetParent(transform, false);

        BuildMask(_insideView.transform);

        // Interior root is rotated so its local +Y points at the doorway (the direction the player walks
        // out). Everything below is authored in that local frame: +Y = toward the door, +X = to the right.
        var interior = new GameObject("Interior").transform;
        interior.SetParent(_insideView.transform, false);
        float doorAngle = Mathf.Atan2(_doorDir.y, _doorDir.x) * Mathf.Rad2Deg - 90f; // map local +Y onto doorDir
        interior.localRotation = Quaternion.Euler(0f, 0f, doorAngle);

        if (interiorSprite != null) BuildSpriteFloor(interior);
        else BuildProceduralRoom(interior);
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

        // Walls: a dark frame around the floor with a gap at the door (front, +Y).
        var wallMat = MakeUnlit(new Color(0.28f, 0.22f, 0.17f));
        float wallSpan = depth + kWallThickness;
        BuildQuad(interior, "WallBack", new Vector2(0f, -roomBack), new Vector2(roomWidth + kWallThickness, kWallThickness), kWallZ, wallMat);
        BuildQuad(interior, "WallLeft", new Vector2(-halfW, centreY), new Vector2(kWallThickness, wallSpan), kWallZ, wallMat);
        BuildQuad(interior, "WallRight", new Vector2(halfW, centreY), new Vector2(kWallThickness, wallSpan), kWallZ, wallMat);
        // Front wall in two segments, leaving the doorway open in the middle.
        float segW = halfW - halfDoor;
        if (segW > 0.01f)
        {
            float segCentre = (halfW + halfDoor) * 0.5f;
            BuildQuad(interior, "WallFrontL", new Vector2(-segCentre, roomFront), new Vector2(segW, kWallThickness), kWallZ, wallMat);
            BuildQuad(interior, "WallFrontR", new Vector2(segCentre, roomFront), new Vector2(segW, kWallThickness), kWallZ, wallMat);
        }

        // A few furnishings so the room reads as a motorhome interior. All placeholder blocks — replace by
        // assigning interiorSprite. Kept clear of the origin (0,0), where the player spawns.
        Vector2 tablePos = new(1.8f, 0.6f);
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
        bool nowInside = EvaluateInside(_inside, localForward, roomFront, hysteresis);
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
    static GameObject BuildQuad(Transform parent, string name, Vector2 centreLocal, Vector2 size, float localZ, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(centreLocal.x, centreLocal.y, localZ);

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
