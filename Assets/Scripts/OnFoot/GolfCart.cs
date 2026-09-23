using System.Collections.Generic;
using UnityEngine;

// The team's golf cart: the paddock runabout parked at the mouth of the player's own garage. Walk up,
// press the action button, and the player rides it — same controls, several times walking pace — until
// they press it again and step off.
//
// Why it is an NPCInteractable and not a vehicle: the paddock already has one way of saying "you can use
// this" (the floating keycap over anything in NPCInteractable.All) and one thing that reads the action
// button (OnFootController). A cart with its own prompt and its own key would be a second, quieter set of
// rules for the player to learn. So this subclasses the talker and overrides Interact to mount instead of
// speak — the same trick RoleStation and VendingMachine use to open a panel instead of a conversation.
//
// The one thing it must NOT do is report IsTalking while it is being ridden: OnFootController zeroes
// movement for as long as the thing it is engaged with says it is talking, so a cart that claimed to be
// mid-conversation would be a cart nailed to the tarmac.
//
// Riding is the walker's body carried along by the cart's own driving model, not a walk with a cart drawn
// under it. The body stays OnFootController's: a second set of movement rules would need its own boundary
// clamp, its own bump handling and its own co-op puppet, and all three come for free by handing the walker
// a velocity each step (IRiddenVehicle) instead of moving the transform ourselves.
//
// What is driven is a cart, not a person. The stick is the race car's controls — forward accelerates, back
// brakes and then reverses, left and right steer — so it points where it is going, it cannot pivot on the
// spot, and it carries its speed through a corner. See CartDrive for the arithmetic and CartDodge for what
// the people in front of it do about it.
//
// Everything it is made of is a SpriteRenderer, never a mesh quad — same rule as PopupGarageRig. The
// on-foot player is a transparent sprite, and an opaque mesh drawn nearer the camera hides them.
public class GolfCart : NPCInteractable, IRiddenVehicle
{
    // The cart the player is currently sat in, or null. One at a time: mounting is only reachable through
    // the action button, and the button goes to the nearest interactable, which while riding is always
    // the cart sat at the player's own feet.
    public static GolfCart Ridden { get; private set; }

    [Header("Riding")]
    [Tooltip("Top speed (units/sec) on full throttle. OnFootController.moveSpeed is 3.5 on foot.")]
    public float rideSpeed = 8f;
    [Tooltip("Metres to the player's left the cart is left standing when they step off. Far enough that " +
             "they are not stood in the middle of it, near enough that it is obviously the thing they " +
             "just got out of.")]
    public float stepOffGap = 1.1f;
    [Tooltip("How fast the cart swings round to the way it is being driven, deg/sec. Only used to settle " +
             "the art onto the heading; the heading itself turns at CartDrive.steerRate.")]
    public float turnRate = 540f;

    [Header("Driving")]
    [Tooltip("Metres/sec² on the throttle.")]
    public float accelRate = 5f;
    [Tooltip("Metres/sec² on the brake — firmer than the throttle, so a stab of back-stick stops it short.")]
    public float brakeRate = 14f;
    [Tooltip("Metres/sec² with neither pedal: the cart rolls to a stop rather than dropping dead.")]
    public float coastRate = 3.5f;
    [Tooltip("Top speed backwards (units/sec), holding the brake once already stopped.")]
    public float reverseSpeed = 2.5f;
    [Tooltip("Seconds the brake must be held at a standstill before reverse engages, so that braking to " +
             "a halt is never an accidental lurch backwards.")]
    public float reverseDelay = 0.4f;
    [Tooltip("Degrees/sec the nose swings at full lock, once there is enough speed for the steering to bite.")]
    public float steerRate = 200f;
    [Tooltip("Speed (units/sec) at which the steering has full authority. Below it the lock scales down, " +
             "so a stopped cart cannot pivot on the spot.")]
    public float steerBiteSpeed = 2f;

    [Header("Getting people out of the way")]
    [Tooltip("How close the cart has to come to somebody before they scramble clear, metres. Measured " +
             "against the stretch of ground the cart is about to cover, not just where it is now.")]
    public float dodgeClearance = 1f;
    [Tooltip("Seconds of the cart's travel looked ahead when deciding who is in the way. At cart pace a " +
             "person needs telling before the cart is on top of them.")]
    public float dodgeLookahead = 0.45f;

    [Header("Look")]
    [Tooltip("Cart width across the seats, metres. A real one is about 1.2m.")]
    public float cartWidth = 1.25f;
    [Tooltip("Cart length nose to tail, metres.")]
    public float cartLength = 2.4f;
    [Tooltip("Body paint. The spawner fills these in from the team's own colours.")]
    public Color primary = new Color(0.85f, 0.85f, 0.88f);
    public Color secondary = new Color(0.20f, 0.22f, 0.26f);
    [Tooltip("Sorting layer the parked cart draws on. Matches the popup garages it is parked beside.")]
    public string sortingLayerName = "Default";
    [Tooltip("Sorting order of the parked cart's lowest part. Its roof and wheels stack just above it.")]
    public int sortingOrder = 2;
    [Tooltip("Z the parked cart sits at. Negative draws in front of the ground plane, in line with the " +
             "garages (PopupGarageLot.garageZ).")]
    public float parkZ = -0.45f;

    // The walker currently sat in it. Unity-null-safe: the on-foot body is destroyed outright when the
    // player gets into the race car, and this has to notice that rather than follow a corpse.
    OnFootController _rider;
    float _riderWalkSpeed, _riderRunMultiplier;
    bool _assembled;
    Vector2 _heading = Vector2.up;                       // the way the nose points, held while stationary

    // Throttle, brake and lock. Owned by the cart, stepped by the walker's FixedUpdate through
    // IRiddenVehicle, and reset to the parked heading every time somebody gets in.
    readonly CartDrive _drive = new();

    // Every painted part, with the sorting order it was built with, so riding can drop the whole cart
    // behind whatever sorting layer the player's own sprite is on and parking can put it back.
    readonly List<SpriteRenderer> _parts = new();
    readonly List<int> _partOrders = new();

    public bool Riding => _rider != null;

    // Nose direction in world space. The cart is built pointing along its own +Y, matching the rigs.
    public Vector3 NoseDirection => transform.up;

    // ---------------------------------------------------------------- the cart itself

    // Stand an unpainted cart up at a spot. Set the colours and sizes, then call Assemble() — nothing is
    // built until then, so the spawner can configure one in a single pass.
    public static GolfCart Create(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        return go.AddComponent<GolfCart>();
    }

    // Build the art. Safe to call twice; later calls do nothing, so a cart can't grow a second roof.
    public void Assemble()
    {
        if (_assembled) return;
        _assembled = true;

        float halfW = cartWidth * 0.5f;
        float halfL = cartLength * 0.5f;

        // Wheels first and widest, so the body is drawn sat on them.
        var tyre = new Color(0.10f, 0.10f, 0.12f);
        var wheelSize = new Vector2(cartWidth * 0.18f, cartLength * 0.2f);
        float wheelX = halfW + wheelSize.x * 0.25f;
        float wheelY = halfL * 0.62f;
        Block("WheelFrontLeft", new Vector2(-wheelX, wheelY), wheelSize, tyre, 0);
        Block("WheelFrontRight", new Vector2(wheelX, wheelY), wheelSize, tyre, 0);
        Block("WheelRearLeft", new Vector2(-wheelX, -wheelY), wheelSize, tyre, 0);
        Block("WheelRearRight", new Vector2(wheelX, -wheelY), wheelSize, tyre, 0);

        // The floor pan, in the team's paint.
        Block("Body", Vector2.zero, new Vector2(cartWidth, cartLength), primary, 1);

        // Bench seat and backrest, in the trim colour — what says "cart" rather than "crate" from above.
        Block("Seat", new Vector2(0f, -cartLength * 0.12f), new Vector2(cartWidth * 0.86f, cartLength * 0.26f),
              secondary, 2);
        Block("Backrest", new Vector2(0f, -cartLength * 0.32f), new Vector2(cartWidth * 0.86f, cartLength * 0.1f),
              new Color(secondary.r * 0.7f, secondary.g * 0.7f, secondary.b * 0.7f, 1f), 2);

        // Nose panel: the bit in front of the driver's knees, so the cart reads as pointing somewhere.
        Block("Nose", new Vector2(0f, halfL * 0.62f), new Vector2(cartWidth * 0.9f, cartLength * 0.24f),
              secondary, 2);

        // The canopy over the top, held off the paint so the seat still reads under it. Drawn last and
        // highest: from above, a cart is mostly roof.
        var roof = new Color(0.94f, 0.94f, 0.96f);
        Block("Roof", new Vector2(0f, cartLength * 0.02f), new Vector2(cartWidth * 0.94f, cartLength * 0.72f),
              roof, 4);

        // No collider on purpose. The cart is walk-through, like the pit box stands and the tyre stacks —
        // a solid cart parked at the garage mouth is something to get wedged against on the one walkway
        // into the row, and the player steps off INTO its footprint every time they dismount.
    }

    GameObject Block(string name, Vector2 centre, Vector2 size, Color colour, int orderAbove)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(centre.x, centre.y, -0.01f * orderAbove);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = WhiteBlock();
        sr.sharedMaterial = SpriteMaterial();
        sr.color = colour;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder + orderAbove;

        _parts.Add(sr);
        _partOrders.Add(orderAbove);
        return go;
    }

    // ---------------------------------------------------------------- riding

    // The action button. Mount if they are stood next to it, step off if they are sat in it. Returns false
    // either way: nothing here is a conversation, and returning true would leave OnFootController holding
    // the cart as an open piece of dialogue.
    public override bool Interact()
    {
        if (Riding) { Dismount(); return false; }

        // Whoever pressed the button, not "the player": OnFootController hands itself over as the
        // interactor immediately before this, and in a co-op session there are two bodies in the paddock.
        var rider = Interactor != null ? Interactor.GetComponent<OnFootController>() : null;
        Mount(rider != null ? rider : OnFootController.Current);
        return false;
    }

    // Never report a conversation. OnFootController freezes the player while the thing they are engaged
    // with is talking; a cart that said yes here could not be driven anywhere.
    public override bool IsTalking => false;

    public bool Mount(OnFootController rider)
    {
        if (rider == null || _rider != null) return false;
        if (Ridden != null && Ridden != this) return false;      // somebody is already in a cart

        _rider = rider;
        Ridden = this;

        // The walker keeps its own rules — boundary clamp, bumping, co-op — and takes its velocity from
        // the cart instead of from the stick. moveSpeed is still swapped so that anything else asking the
        // walker how fast it is going gets the cart's pace, and gets its legs back on the way out.
        _riderWalkSpeed = rider.moveSpeed;
        _riderRunMultiplier = rider.runMultiplier;
        rider.moveSpeed = rideSpeed;
        rider.runMultiplier = 1f;                                // a cart has one pedal; sprinting in it is not a thing
        rider.Ridden = this;

        // Pull away pointing the way it was parked, from a standstill. Getting in and finding yourself
        // already rolling, or facing the way you happened to walk up, is neither of them a cart.
        _heading = ((Vector2)transform.up).sqrMagnitude > 1e-6f ? ((Vector2)transform.up).normalized : _heading;
        ApplyTuning();
        _drive.Reset(_heading);

        DrawBehindRider(rider);
        FollowRider();
        return true;
    }

    public void Dismount()
    {
        var rider = _rider;
        _rider = null;
        if (Ridden == this) Ridden = null;

        if (rider != null)
        {
            rider.moveSpeed = _riderWalkSpeed;
            rider.runMultiplier = _riderRunMultiplier;
            if (ReferenceEquals(rider.Ridden, this)) rider.Ridden = null;   // back on their own legs
            if (rider.TryGetComponent(out Rigidbody2D body)) body.linearVelocity = Vector2.zero;

            // Left standing at the player's left hand, pointing the way it was last driven, and pulled
            // inside the walkable paddock so it can never be parked through a fence.
            Vector2 left = new Vector2(-_heading.y, _heading.x);
            Vector3 spot = rider.transform.position + (Vector3)(left * stepOffGap);
            if (PaddockBoundary.AnyActive) spot = PaddockBoundary.Constrain(spot);
            transform.position = new Vector3(spot.x, spot.y, parkZ);
        }

        RestoreParkedDraw();
    }

    // ---------------------------------------------------------------- driving (IRiddenVehicle)

    // One step of pedals and lock, handed the raw on-foot stick. The walker moves at what comes back.
    public Vector2 Steer(Vector2 stick, float dt)
    {
        ApplyTuning();
        Vector2 velocity = _drive.Step(stick, dt);
        _heading = _drive.Heading;

        // Anybody about to be run over gets out of the way. Measured from the RIDER, not from this
        // transform: the cart is drawn at the player's feet a frame behind, and the people in front of it
        // need warning off where it is actually going to be.
        if (_rider != null && velocity.sqrMagnitude > 0.25f)
            CartDodge.ScatterFrom(_rider.transform.position, velocity, _rider.transform,
                                  dodgeClearance, dodgeLookahead);

        return velocity;
    }

    // What the walker really managed — the boundary clamp and the shove out of people both have a say
    // after us, and a cart pressed against a fence has to lose its speed against it.
    public void Moved(Vector2 actualVelocity) => _drive.Blocked(actualVelocity);

    public Vector2 Facing => _heading;

    // Speed along the nose, m/s. Negative is reversing. For anything wanting to read the cart's pace.
    public float Speed => _drive.Speed;

    // Inspector values are the cart's; CartDrive is the arithmetic. Copied over each step so a knob
    // dragged in play mode takes effect immediately, which is how the whole thing gets tuned.
    void ApplyTuning()
    {
        _drive.topSpeed = rideSpeed;
        _drive.reverseSpeed = reverseSpeed;
        _drive.reverseDelay = reverseDelay;
        _drive.accelRate = accelRate;
        _drive.brakeRate = brakeRate;
        _drive.coastRate = coastRate;
        _drive.steerRate = steerRate;
        _drive.steerBiteSpeed = steerBiteSpeed;
    }

    // Carried on the walker rather than driven: the cart is put where the player is, after they have
    // moved, and turned to the way they are going.
    void LateUpdate()
    {
        if (_rider == null) return;

        // The on-foot body is destroyed outright when the player gets into the race car or the scene
        // swaps under us. Leave the cart standing where they last were rather than following a dead
        // transform — there is no speed to hand back to a body that no longer exists.
        if (!_rider)
        {
            _rider = null;
            if (Ridden == this) Ridden = null;
            transform.position = new Vector3(transform.position.x, transform.position.y, parkZ);
            RestoreParkedDraw();
            return;
        }

        FollowRider();
    }

    void FollowRider()
    {
        Vector3 p = _rider.transform.position;

        // The nose is the driving model's, not the direction of travel: a cart being reversed still points
        // the way it is aimed, and one stopped at the pit box holds its last heading rather than snapping.
        // The art is built nose-along-+Y, so the heading angle is measured off +Y.
        float want = Mathf.Atan2(_heading.y, _heading.x) * Mathf.Rad2Deg - 90f;
        float z = Mathf.MoveTowardsAngle(transform.eulerAngles.z, want, turnRate * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, z);

        // Just behind the rider's own plane. Draw order is what actually decides who is on top (see
        // DrawBehindRider); the z nudge only keeps the two from fighting over the same depth.
        transform.position = new Vector3(p.x, p.y, p.z + 0.02f);
    }

    // Put every part of the cart on the rider's own sorting layer, below their sprite, so the player is
    // drawn sat IN it. A parked cart lives on the paddock's layer with the garages; a ridden one has to
    // share a layer with the person on top of it or the order means nothing.
    void DrawBehindRider(OnFootController rider)
    {
        var riderSprites = rider.GetComponentsInChildren<SpriteRenderer>(true);
        if (riderSprites == null || riderSprites.Length == 0) return;

        int layer = riderSprites[0].sortingLayerID;
        int lowest = int.MaxValue;
        foreach (var sr in riderSprites)
            if (sr != null && sr.sortingOrder < lowest) lowest = sr.sortingOrder;
        if (lowest == int.MaxValue) lowest = 0;

        for (int i = 0; i < _parts.Count; i++)
        {
            if (_parts[i] == null) continue;
            _parts[i].sortingLayerID = layer;
            // Ten clear of the lowest thing the player is drawn from: a paper-doll rig is a stack of parts
            // (boots, legs, body, head) and the cart has to sit under all of them, roof included.
            _parts[i].sortingOrder = lowest - 10 + _partOrders[i];
        }
    }

    void RestoreParkedDraw()
    {
        for (int i = 0; i < _parts.Count; i++)
        {
            if (_parts[i] == null) continue;
            _parts[i].sortingLayerName = sortingLayerName;
            _parts[i].sortingOrder = sortingOrder + _partOrders[i];
        }
    }

    // Deliberately OnDestroy and not OnDisable: NPCInteractable does its own bookkeeping in a private
    // OnDisable (it un-registers from All and puts its bubbles away), and a message of the same name
    // declared down here would hide it — Unity calls the most-derived one only. Unity runs OnDisable and
    // then OnDestroy on the way out, so the base still gets its turn.
    void OnDestroy()
    {
        // Never leave the player stuck at cart speed because the cart went away underneath them.
        if (_rider != null) Dismount();
        else if (Ridden == this) Ridden = null;
    }

    // --- art helpers ------------------------------------------------------------------------------

    // One world unit per side, so a block's scale IS its size in metres. Same recipe as PopupGarageRig.
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
        _spriteMat = new Material(sh) { name = "GolfCartUnlit" };
        return _spriteMat;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.4f, 0.9f, 0.5f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(cartWidth, cartLength, 0.01f));
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawLine(transform.position, transform.position + NoseDirection * 1.5f);
    }
#endif
}
