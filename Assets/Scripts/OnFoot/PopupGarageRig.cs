using UnityEngine;

// A team's popup garage in the paddock: the same body as a driver's motorhome, with an open canopy
// pitched off one side and the car parked underneath it. Drivers get an RV each (DriverMotorhomeLot);
// teams get one of these each, which is where the crew live between sessions and where the car sits
// whenever it is not out on track or sat in its pit box.
//
// Local frame, shared with the motorhome lot so both blocks park the same way round:
//   +Y = the body's LENGTH, toward the cab.
//   +X = the body's WIDTH — the door side, and the side the canopy is pitched on (flip with canopySide).
// The transform's origin is the middle of the BODY, not of the body-plus-canopy, so a rig can be dropped
// at a line place without the canopy dragging it off centre.
//
// The doorway sits on the canopy side, `doorAlong` metres toward the cab — i.e. past the nose of the
// parked car, so walking in never means walking through the bodywork. PopupGarageInterior reads the
// doorway back off this component to line its masked room up with the shell, exactly as RVInterior does
// with RVExterior. Solid colliders outline the body with a notch at the door; the interior switches them
// off while the player is inside (they overlap its floor) and back on when they step out.
//
// Everything here is a SpriteRenderer, never a mesh quad: the on-foot player is a sprite too, and an
// opaque quad drawn at the rig's negative z would depth-occlude them the moment they walked onto the
// canopy. Sprites don't write depth, so ordering stays under our control via sortingOrder. (The masked
// INTERIOR is the opposite case and does use opaque meshes — occluding the world is the whole point.)
public class PopupGarageRig : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Car number this garage belongs to. Drives the number decal and the colours.")]
    public int carNumber;
    [Tooltip("Team name lettered on the canopy edge.")]
    public string teamName = "";
    [Tooltip("Driver in this garage — used for the object name and by anything looking the rig up.")]
    public string driverName = "";
    [Tooltip("Carset prefix the parked car's livery is loaded from, e.g. 'cup26' gives Resources/cup26livery20.")]
    public string carset = "cup26";

    [Header("Body (metres)")]
    [Tooltip("Width across the door side (local X). Matches the motorhome lot's rigs.")]
    public float bodyWidth = 3.95f;
    [Tooltip("Length toward the cab (local +Y). Matches the motorhome lot's rigs.")]
    public float bodyLength = 9.93f;

    [Header("Canopy")]
    [Tooltip("Which side of the body the canopy is pitched on: +1 = local +X, -1 = local -X. The door follows it.")]
    public int canopySide = 1;
    [Tooltip("How far the canopy reaches out from the body's side (m).")]
    public float canopyWidth = 6.5f;
    [Tooltip("How much of the body's length the canopy runs along (m).")]
    public float canopyLength = 7.2f;

    [Header("Doorway")]
    [Tooltip("Width of the door opening in the body's side (m). Must clear the walking player.")]
    public float doorWidth = 1.6f;
    [Tooltip("Where along the body the doorway sits, from the body centre toward the cab (m). Kept past the parked car's nose so the way in is never through the car.")]
    public float doorAlong = 3.1f;
    [Tooltip("Thickness of the shell colliders (m). Thick enough that a running player can't tunnel it.")]
    public float wallThickness = 0.5f;

    [Header("Parked car")]
    [Tooltip("Park the team's car under the canopy. The lot turns this off for a driver whose real car is already in the world — out on track or sat in its pit box.")]
    public bool carAtHome = true;
    [Tooltip("Length of the parked car (m). Matches GridSpawner's collision extents.")]
    public float carLength = 4.8f;
    [Tooltip("Width of the parked car (m).")]
    public float carWidth = 2f;
    [Tooltip("Where the car sits along the canopy, from its centre toward the cab (m). Negative parks it back from the doorway.")]
    public float carAlong = -0.7f;

    [Header("Look")]
    public string sortingLayerName = "Default";
    [Tooltip("Order the canopy floor draws at. Everything else on the rig stacks just above it, and the whole thing stays under the walking crowd — the same band the motorhome lot uses.")]
    public int sortingOrder = 2;
    [Tooltip("Team's primary colour — the canopy and the body. Set from CarColours by the lot.")]
    public Color primary = Color.white;
    [Tooltip("Team's secondary colour — the canopy trim and the body's stripe.")]
    public Color secondary = new Color(0.6f, 0.6f, 0.6f);
    [Tooltip("Resources name prefix for the number art painted on the roof, e.g. 'cup20num' plus 8.")]
    public string numberSpritePrefix = "cup20num";
    [Tooltip("Height (m) of the painted number. 16x16 art at 12.8 px/m, so multiples of 1.25m keep its pixels square.")]
    public float numberSize = 2.5f;
    [Tooltip("Letter the team's name across the canopy edge. Off = an unmarked awning.")]
    public bool showTeamName = true;

    // Which way the canopy (and so the door) faces, as a clean +1 / -1.
    public int Side => canopySide < 0 ? -1 : 1;

    // The doorway, in the body's local frame and in the world — the pair PopupGarageInterior needs to
    // stand its room up in the right place whatever way the rig is parked.
    public Vector2 DoorLocalDirection => new Vector2(Side, 0f);
    public Vector2 DoorLocalPosition => new Vector2(Side * bodyWidth * 0.5f, doorAlong);
    public Vector2 DoorWorldDirection => ((Vector2)transform.TransformDirection(DoorLocalDirection)).normalized;
    public Vector3 DoorWorldPosition => transform.TransformPoint(new Vector3(DoorLocalPosition.x, DoorLocalPosition.y, 0f));

    // Middle of the canopy, and the spot under it the car is parked on.
    public Vector2 CanopyLocalCentre => new Vector2(Side * (bodyWidth + canopyWidth) * 0.5f, 0f);
    public Vector3 CanopyWorldCentre => transform.TransformPoint(new Vector3(CanopyLocalCentre.x, CanopyLocalCentre.y, 0f));
    public Vector3 ParkedCarWorldPosition => transform.TransformPoint(new Vector3(CanopyLocalCentre.x, CanopyLocalCentre.y + carAlong, 0f));

    // The car standing under the canopy, or null when the real one is out on track / in its pit box.
    public Transform ParkedCar { get; private set; }

    bool _assembled;
    Collider2D[] _colliders;

    // Shell colliders only block from OUTSIDE: they overlap the interior's floor, so the masked room
    // switches them off while the player is in it and its own walls take over. Same contract as RVExterior.
    public void SetCollidersEnabled(bool value)
    {
        if (_colliders == null) _colliders = GetComponentsInChildren<Collider2D>(true);
        foreach (var c in _colliders)
            if (c != null) c.enabled = value;
    }

    // Stand an empty rig up at a place in a line. Set the fields that differ (identity, colours, whether
    // the car is home) and then call Assemble() — nothing is built until then, so a lot can configure a
    // rig in one pass rather than tearing art down and rebuilding it.
    public static PopupGarageRig Create(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        return go.AddComponent<PopupGarageRig>();
    }

    // Build the art and the shell. Safe to call once; later calls are ignored so a rig can't grow two
    // bodies if something re-runs the lot.
    public void Assemble()
    {
        if (_assembled) return;
        _assembled = true;

        BuildCanopy();
        if (carAtHome) BuildParkedCar();
        BuildBody();
        BuildShellColliders();
    }

    // The awning itself: a shaded pad in the team's primary with a trim strip along its outer edge and a
    // post at each corner. Drawn first (lowest order) so the car parks ON it rather than behind it.
    void BuildCanopy()
    {
        var canopy = new GameObject("Canopy");
        canopy.transform.SetParent(transform, false);
        canopy.transform.localPosition = new Vector3(CanopyLocalCentre.x, CanopyLocalCentre.y, 0f);

        // Darkened primary: under a canopy you are looking at shade, not at fresh paint.
        var shade = new Color(primary.r * 0.55f, primary.g * 0.55f, primary.b * 0.55f, 1f);
        Block(canopy.transform, "Shade", Vector2.zero, new Vector2(canopyWidth, canopyLength), 0f, shade, sortingOrder);

        // Trim along the open edge, in the secondary — the valance a real awning hangs.
        float outerX = Side * (canopyWidth * 0.5f - 0.25f);
        Block(canopy.transform, "Trim", new Vector2(outerX, 0f), new Vector2(0.5f, canopyLength), -0.05f, secondary, sortingOrder + 1);

        // A post at each corner, so the shade reads as something pitched rather than as paint on the ground.
        float outPost = Side * (canopyWidth * 0.5f - 0.3f);
        float inPost = Side * (-canopyWidth * 0.5f + 0.3f);
        float py = canopyLength * 0.5f - 0.3f;
        var postColour = new Color(0.16f, 0.17f, 0.19f);
        var postSize = new Vector2(0.45f, 0.45f);
        Block(canopy.transform, "PostA", new Vector2(outPost, py), postSize, -0.1f, postColour, sortingOrder + 5);
        Block(canopy.transform, "PostB", new Vector2(outPost, -py), postSize, -0.1f, postColour, sortingOrder + 5);
        Block(canopy.transform, "PostC", new Vector2(inPost, py), postSize, -0.1f, postColour, sortingOrder + 5);
        Block(canopy.transform, "PostD", new Vector2(inPost, -py), postSize, -0.1f, postColour, sortingOrder + 5);

        if (!showTeamName || string.IsNullOrEmpty(teamName)) return;

        // Lettered along the outer edge and — like every other sign in the paddock — kept the right way
        // up in the world rather than inheriting whatever rotation the rig parked at.
        string label = PlayerDriver.ShortTeamName(teamName);
        if (string.IsNullOrEmpty(label)) return;

        var sign = PaddockProps.Sign(canopy.transform, label, new Vector2(outerX, 0f),
                                     Mathf.Min(canopyLength * 0.8f, 5f), Color.white, -0.15f);
        var signRenderer = sign.GetComponent<MeshRenderer>();
        if (signRenderer != null)
        {
            signRenderer.sortingLayerName = sortingLayerName;
            signRenderer.sortingOrder = sortingOrder + 6;
        }
    }

    // The car itself, in its own paint, and solid: seen from above, walking over the roof of a race car is
    // the one prop nobody reads as flat, and the REAL cars are already immovable on foot (VehicleCollision
    // hands a person straight to Unity's solver, which pushes the walker out and never the car). A garage
    // whose car happens to be at home behaved the opposite way to the one next door whose driver is out on
    // track, which is the giveaway. The small props — the pit box stand, the tyre stacks — stay
    // walk-through; a 4.8m car is not one of those.
    //
    // Nothing is pinched by it: the car is parked in the middle of the canopy, which leaves 2.25m of open
    // ground between its side and the body's door wall at the stock sizes, and the doorway sits a further
    // 1.4m past the car's nose (doorAlong is kept clear of carAlong for exactly this reason). The footprint
    // is a plain child of the rig rather than a collider on the art, because the art is turned a quarter
    // turn and scaled by whatever the livery's pixels-per-unit happens to be.
    void BuildParkedCar()
    {
        var go = new GameObject("ParkedCar");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(CanopyLocalCentre.x, CanopyLocalCentre.y + carAlong, -0.08f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = SpriteMaterial();
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder + 3;

        // Livery art runs its LENGTH along the sprite's +X (64x32) while the body frame runs length along
        // +Y — so the art is turned a quarter turn and scaled to the real car, whatever its
        // pixels-per-unit happens to be. The same recipe the motorhome lot uses for its bodies.
        Sprite livery = string.IsNullOrEmpty(carset) || carNumber <= 0
            ? null
            : Resources.Load<Sprite>($"{carset}livery{carNumber}");

        if (livery != null)
        {
            sr.sprite = livery;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            Vector2 s = livery.bounds.size;
            if (s.x > 0.0001f && s.y > 0.0001f)
                go.transform.localScale = new Vector3(carLength / s.x, carWidth / s.y, 1f);
        }
        else
        {
            // No paint for this number: a block in the team's colours still reads as "their car is in".
            sr.sprite = WhiteBlock();
            sr.color = primary;
            go.transform.localScale = new Vector3(carWidth, carLength, 1f);
        }

        ParkedCar = go.transform;

        // The footprint, in the body's own frame: carWidth across the canopy, carLength along it, on the
        // spot the art is drawn. Built with the same helper as the shell walls so it is picked up by the
        // collider sweep at the end of Assemble() — and so the interior switches it off with the rest of
        // them while the player is stood in the masked room.
        Wall("CarBody", new Vector2(CanopyLocalCentre.x, CanopyLocalCentre.y + carAlong),
             new Vector2(Mathf.Max(0.1f, carWidth), Mathf.Max(0.1f, carLength)));
    }

    // The rig body, plus the car number painted on its roof so a walk down the row tells you whose garage
    // each one is without stopping to read the signs.
    void BuildBody()
    {
        var body = new GameObject("Body");
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0f, 0f, -0.12f);
        body.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);   // sprite length (+X) onto the body's length

        var sr = body.AddComponent<SpriteRenderer>();
        sr.sprite = HaulerSprite(carNumber);
        sr.sharedMaterial = SpriteMaterial();
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder + 2;
        // Team primary, knocked back so the number and the trim still read against it.
        sr.color = Color.Lerp(primary, Color.white, 0.15f);

        if (sr.sprite != null)
        {
            Vector2 s = sr.sprite.bounds.size;
            if (s.x > 0.0001f && s.y > 0.0001f)
                body.transform.localScale = new Vector3(bodyLength / s.x, bodyWidth / s.y, 1f);
        }
        else
        {
            sr.sprite = WhiteBlock();
            body.transform.localRotation = Quaternion.identity;
            body.transform.localScale = new Vector3(bodyWidth, bodyLength, 1f);
        }

        // A step in the secondary colour on the ground outside the doorway: under a canopy the same
        // length as the rig, this is what says which bit of the side you walk through.
        Block(transform, "Doorstep", new Vector2(Side * (bodyWidth * 0.5f + 0.5f), doorAlong),
              new Vector2(1f, doorWidth), -0.06f, secondary, sortingOrder + 1);

        if (carNumber <= 0 || string.IsNullOrEmpty(numberSpritePrefix)) return;
        var number = Resources.Load<Sprite>($"{numberSpritePrefix}{carNumber}");
        if (number == null) return;

        var num = new GameObject($"Number_{carNumber}");
        num.transform.SetParent(transform, false);
        num.transform.localPosition = new Vector3(-Side * 0.6f, 0f, -0.2f);
        num.transform.localRotation = Quaternion.identity;   // reads along the body, whatever way it parks

        var nsr = num.AddComponent<SpriteRenderer>();
        nsr.sprite = number;
        nsr.sharedMaterial = SpriteMaterial();
        nsr.sortingLayerName = sortingLayerName;
        nsr.sortingOrder = sortingOrder + 4;
        float h = number.bounds.size.y;
        if (h > 0.0001f)
        {
            float scale = numberSize / h;
            num.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    // Solid walls around the body with a notch at the door — the same shell the RV prefab carries, built
    // in code because these rigs are generated. The notch IS the doorway, so the only way in is through
    // it and the interior's own door gap lines up with it.
    void BuildShellColliders()
    {
        float t = Mathf.Max(0.1f, wallThickness);
        float halfW = bodyWidth * 0.5f;
        float halfL = bodyLength * 0.5f;

        // The side away from the canopy, and the two ends.
        Wall("WallBack", new Vector2(-Side * (halfW - t * 0.5f), 0f), new Vector2(t, bodyLength));
        Wall("WallCab", new Vector2(0f, halfL - t * 0.5f), new Vector2(bodyWidth, t));
        Wall("WallTail", new Vector2(0f, -halfL + t * 0.5f), new Vector2(bodyWidth, t));

        // Door side, in two pieces either side of the opening.
        float doorX = Side * (halfW - t * 0.5f);
        float doorTop = doorAlong + doorWidth * 0.5f;
        float doorBottom = doorAlong - doorWidth * 0.5f;

        float cabSide = halfL - doorTop;
        if (cabSide > 0.05f) Wall("WallDoorCab", new Vector2(doorX, (halfL + doorTop) * 0.5f), new Vector2(t, cabSide));
        float tailSide = doorBottom + halfL;
        if (tailSide > 0.05f) Wall("WallDoorTail", new Vector2(doorX, (doorBottom - halfL) * 0.5f), new Vector2(t, tailSide));

        _colliders = GetComponentsInChildren<Collider2D>(true);
    }

    void Wall(string name, Vector2 centre, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(centre.x, centre.y, 0f);
        go.AddComponent<BoxCollider2D>().size = size;
    }

    // --- art helpers ------------------------------------------------------------------------------

    // A flat tinted rectangle, as a sprite (see the class comment for why nothing out here is a mesh).
    GameObject Block(Transform parent, string name, Vector2 centre, Vector2 size, float localZ, Color colour, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(centre.x, centre.y, localZ);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = WhiteBlock();
        sr.sharedMaterial = SpriteMaterial();
        sr.color = colour;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = order;
        return go;
    }

    // The same three motorhome sprites the drivers' lot parks, picked per car number so a row of garages
    // isn't visibly three objects repeated.
    static Sprite[] _haulers;
    static Sprite HaulerSprite(int carNumber)
    {
        if (_haulers == null)
        {
            var found = new System.Collections.Generic.List<Sprite>();
            foreach (string n in new[] { "Environment/motorhome", "Environment/motorhome2", "Environment/motorhome3" })
            {
                var s = Resources.Load<Sprite>(n);
                if (s != null) found.Add(s);
            }
            _haulers = found.ToArray();
        }
        return _haulers.Length == 0 ? null : _haulers[Mathf.Abs(carNumber) % _haulers.Length];
    }

    // One world unit per side, so a block's scale IS its size in metres.
    static Sprite _white;
    static Sprite WhiteBlock()
    {
        if (_white != null) return _white;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px); tex.Apply();
        _white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return _white;
    }

    // The scene renders through the 3D URP renderer, where Sprite-Lit-Default gets no Light2D and comes
    // out black. Everything on foot swaps to unlit for the same reason.
    static Material _spriteMat;
    static Material SpriteMaterial()
    {
        if (_spriteMat != null) return _spriteMat;
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        _spriteMat = new Material(sh) { name = "PopupGarageUnlit" };
        return _spriteMat;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Footprint, canopy and door arrow, so a rig dropped by hand can be lined up without play mode.
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(bodyWidth, bodyLength, 0.01f));
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.7f);
        Gizmos.DrawWireCube(new Vector3(CanopyLocalCentre.x, CanopyLocalCentre.y, 0f),
                            new Vector3(canopyWidth, canopyLength, 0.01f));
        Gizmos.matrix = Matrix4x4.identity;

        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
        Vector3 dir = DoorWorldDirection;
        Vector3 side = new Vector3(-dir.y, dir.x, 0f);
        Vector3 p = DoorWorldPosition;
        Vector3 tip = p + dir * 2.5f;
        Gizmos.DrawLine(p, tip);
        Gizmos.DrawLine(tip, tip - dir * 0.7f + side * 0.45f);
        Gizmos.DrawLine(tip, tip - dir * 0.7f - side * 0.45f);
        UnityEditor.Handles.color = Gizmos.color;
        UnityEditor.Handles.Label(tip + side * 0.3f, string.IsNullOrEmpty(teamName) ? "garage door" : teamName + " garage");
    }
#endif
}
