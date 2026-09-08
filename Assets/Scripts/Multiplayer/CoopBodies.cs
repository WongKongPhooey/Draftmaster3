using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// The other player, walking around your paddock.
//
// Each peer sends its own on-foot pose a few times a second and holds one puppet body per other peer,
// built from the same prefab the scene already spawns the player from. The puppet is a normal walking body
// as far as everything else in the paddock is concerned — the crowd avoids it, it registers in
// OnFootController.All — but it is flagged RemotePuppet, so it never reads input, never interacts, and is
// never handed back as OnFootController.Current.
//
// Named messages rather than NetworkObject/NetworkTransform, for the same reason CareerMirror is: no prefab
// asset to author, nothing to register on either peer, and — the one that actually matters here — nothing
// that has to survive a scene load. The career reloads the scene constantly (practice → qualifying → race,
// travel, the garage sheet), and a spawned NetworkObject across those loads is far more machinery than a
// walking avatar is worth. A puppet is a plain scene object: it dies with the scene and is rebuilt from the
// next pose that arrives, which makes a scene change self-healing instead of something to coordinate.
//
// Interpolated rather than snapped: at walking pace a 15Hz feed lerped over the gap is indistinguishable
// from local movement, and it costs about forty lines.
//
// Each peer also broadcasts the name it set on the OPTIONS screen, on its own slow reliable message rather
// than riding the pose, and the puppet wears it on a CoopNameTag above its head. A name is a string and a
// pose is dropped freely; losing the one packet that carried the name would leave the other player labelled
// PLAYER 2 for the rest of the session. Re-sent on a loop so a join, a scene load or a rename all fix
// themselves without anything having to notice them.
public class CoopBodies : MonoBehaviour
{
    const string PoseMessage = "coop.body.pose";
    const string NameMessage = "coop.body.name";

    [Tooltip("Pose sends per second. Walking pace does not need more; the receiver interpolates between them.")]
    public float sendRate = 15f;

    [Tooltip("Float the other player's name on a tag above their head — whatever they typed on the OPTIONS screen.")]
    public bool showNameTags = true;

    [Tooltip("Seconds between name broadcasts. The name rides its own reliable message rather than the 15Hz pose, and is re-sent on a slow loop so a join, a scene load or a rename in OPTIONS all fix themselves without anything having to notice them.")]
    public float nameSendInterval = 2f;

    [Tooltip("How quickly a puppet closes on the last pose received. Higher = tighter tracking, more visible stepping if the connection is lumpy.")]
    public float followLerp = 12f;

    [Tooltip("Beyond this far from the last received pose the puppet snaps instead of walking there — a teleport, a recall or a scene change should not be animated as a sprint across the paddock.")]
    public float snapDistance = 8f;

    [Tooltip("Tint applied to the other player's body so the two of you are told apart at a glance.")]
    public Color remoteTint = new Color(0.62f, 0.85f, 1f);

    class Puppet
    {
        public GameObject go;
        public OnFootController ofc;
        public Animator animator;
        public CoopNameTag nameTag;
        public Vector3 targetPos;
        public float targetFacing;
        public Vector2 moveDir;
        public float lastSeen;
    }

    readonly Dictionary<ulong, Puppet> _puppets = new();

    // The name each peer last told us they go by. Kept apart from the puppets because the two arrive
    // independently — a name can land before that peer has a body in this scene, and a puppet is rebuilt
    // from scratch on every scene load while the name is not.
    readonly Dictionary<ulong, string> _names = new();

    float _nextSend;
    float _nextNameSend;
    bool _registered;

    void OnDestroy() => Unregister();

    void Update()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || !Coop.Active) { Unregister(); return; }

        Register(nm);

        if (Time.unscaledTime >= _nextSend)
        {
            _nextSend = Time.unscaledTime + 1f / Mathf.Max(1f, sendRate);
            SendMyPose(nm);
        }

        if (showNameTags && Time.unscaledTime >= _nextNameSend)
        {
            _nextNameSend = Time.unscaledTime + Mathf.Max(0.5f, nameSendInterval);
            SendMyName(nm);
        }

        DrivePuppets();
    }

    // ------------------------------------------------------------------ wiring

    void Register(NetworkManager nm)
    {
        if (_registered) return;
        var msg = nm.CustomMessagingManager;
        if (msg == null) return;
        msg.RegisterNamedMessageHandler(PoseMessage, OnPose);
        msg.RegisterNamedMessageHandler(NameMessage, OnName);
        _registered = true;
        _nextNameSend = 0f;   // say who we are on the very next frame, not two seconds into the session
    }

    void Unregister()
    {
        if (_registered)
        {
            var msg = NetworkManager.Singleton != null ? NetworkManager.Singleton.CustomMessagingManager : null;
            if (msg != null)
            {
                msg.UnregisterNamedMessageHandler(PoseMessage);
                msg.UnregisterNamedMessageHandler(NameMessage);
            }
            _registered = false;
        }
        ClearPuppets();
        _names.Clear();
    }

    void ClearPuppets()
    {
        foreach (var p in _puppets.Values)
        {
            if (p.nameTag != null) p.nameTag.Detach();
            if (p.go != null) Destroy(p.go);
        }
        _puppets.Clear();
    }

    // ------------------------------------------------------------------ sending

    void SendMyPose(NetworkManager nm)
    {
        var msg = nm.CustomMessagingManager;
        if (msg == null) return;

        var me = OnFootController.Current;     // never a puppet — that is what the flag is for
        bool hasBody = me != null;

        Vector3 pos = hasBody ? me.transform.position : Vector3.zero;
        float facing = hasBody ? me.transform.eulerAngles.z : 0f;
        Vector2 move = hasBody ? ReadPlanarVelocity(me) : Vector2.zero;

        using var writer = new FastBufferWriter(48, Allocator.Temp);
        writer.WriteValueSafe(nm.LocalClientId);
        writer.WriteValueSafe(hasBody);
        writer.WriteValueSafe(pos);
        writer.WriteValueSafe(facing);
        writer.WriteValueSafe(move);

        // Named messages are client↔server only, so the server is also the relay: a guest sends up, the
        // server re-broadcasts (below) to everyone but the sender. Unreliable — a dropped pose is replaced
        // by the next one 66ms later, and re-sending a stale position is worse than skipping it.
        if (nm.IsServer) msg.SendNamedMessageToAll(PoseMessage, writer, NetworkDelivery.Unreliable);
        else msg.SendNamedMessage(PoseMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.Unreliable);
    }

    static Vector2 ReadPlanarVelocity(OnFootController ofc)
    {
        var rb = ofc.GetComponent<Rigidbody2D>();
        return rb != null ? rb.linearVelocity : Vector2.zero;
    }

    // Who this machine's player is, for the tag over their head on the other machine.
    //
    // Reliable and on its own slow loop rather than riding the pose: a name is a string, the pose is sent
    // fifteen times a second and dropped freely, and losing the one frame that carried the name would leave
    // the other player labelled PLAYER 2 for the rest of the session.
    void SendMyName(NetworkManager nm)
    {
        var msg = nm.CustomMessagingManager;
        if (msg == null) return;

        string label = LocalDisplayName();

        using var writer = new FastBufferWriter(128, Allocator.Temp, 512);
        writer.WriteValueSafe(nm.LocalClientId);
        writer.WriteValueSafe(label);

        if (nm.IsServer) msg.SendNamedMessageToAll(NameMessage, writer, NetworkDelivery.Reliable);
        else msg.SendNamedMessage(NameMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
    }

    // The name this player set on the OPTIONS screen, in the two halves that screen writes. Empty when they
    // have never named themselves — the receiver turns that into a PLAYER 1 / PLAYER 2 placeholder rather
    // than leaving a blank tag hanging over them.
    public static string LocalDisplayName()
    {
        string first = PlayerDriver.FirstName;
        string last = PlayerDriver.LastName;
        return (first + " " + last).Trim();
    }

    // ------------------------------------------------------------------ receiving

    void OnPose(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out ulong owner);
        reader.ReadValueSafe(out bool hasBody);
        reader.ReadValueSafe(out Vector3 pos);
        reader.ReadValueSafe(out float facing);
        reader.ReadValueSafe(out Vector2 move);

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Server: pass a guest's pose on to every other client before using it ourselves.
        if (nm.IsServer && owner != NetworkManager.ServerClientId)
        {
            var msg = nm.CustomMessagingManager;
            if (msg != null)
            {
                using var relay = new FastBufferWriter(48, Allocator.Temp);
                relay.WriteValueSafe(owner);
                relay.WriteValueSafe(hasBody);
                relay.WriteValueSafe(pos);
                relay.WriteValueSafe(facing);
                relay.WriteValueSafe(move);

                foreach (var id in nm.ConnectedClientsIds)
                    if (id != owner && id != NetworkManager.ServerClientId)
                        msg.SendNamedMessage(PoseMessage, id, relay, NetworkDelivery.Unreliable);
            }
        }

        if (owner == nm.LocalClientId) return;   // our own pose coming back off the relay

        if (!hasBody) { RetirePuppet(owner); return; }   // they are in the car or in a menu

        var puppet = EnsurePuppet(owner, pos, facing);
        if (puppet == null) return;

        puppet.targetPos = pos;
        puppet.targetFacing = facing;
        puppet.moveDir = move;
        puppet.lastSeen = Time.unscaledTime;
    }

    void OnName(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out ulong owner);
        reader.ReadValueSafe(out string label);

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Server relays, exactly as it does for poses — named messages are client↔server only.
        if (nm.IsServer && owner != NetworkManager.ServerClientId)
        {
            var msg = nm.CustomMessagingManager;
            if (msg != null)
            {
                using var relay = new FastBufferWriter(128, Allocator.Temp, 512);
                relay.WriteValueSafe(owner);
                relay.WriteValueSafe(label);

                foreach (var id in nm.ConnectedClientsIds)
                    if (id != owner && id != NetworkManager.ServerClientId)
                        msg.SendNamedMessage(NameMessage, id, relay, NetworkDelivery.Reliable);
            }
        }

        if (owner == nm.LocalClientId) return;   // our own name coming back off the relay

        _names[owner] = label ?? "";
        if (_puppets.TryGetValue(owner, out var p) && p.nameTag != null)
            p.nameTag.SetName(NameFor(owner));
    }

    // What the tag over a given peer reads. Their OPTIONS name when they have sent one, and which player
    // they are when they have not.
    string NameFor(ulong owner)
    {
        _names.TryGetValue(owner, out string sent);
        return CoopNameTag.LabelFor(sent, owner == NetworkManager.ServerClientId);
    }

    // ------------------------------------------------------------------ puppets

    void DrivePuppets()
    {
        float t = 1f - Mathf.Exp(-followLerp * Time.deltaTime);   // framerate-independent ease

        foreach (var kv in _puppets)
        {
            var p = kv.Value;
            if (p.go == null) continue;

            Vector3 here = p.go.transform.position;
            // A recall, a teleport or a scene change moves them further than they could have walked. Snap
            // rather than animating a sprint across the paddock.
            if ((p.targetPos - here).sqrMagnitude > snapDistance * snapDistance)
                p.go.transform.position = p.targetPos;
            else
                p.go.transform.position = Vector3.Lerp(here, p.targetPos, t);

            p.go.transform.rotation = Quaternion.Slerp(
                p.go.transform.rotation, Quaternion.Euler(0f, 0f, p.targetFacing), t);

            // Drive the same animator parameters the local walker sets, so a remote body walks rather than
            // gliding. Whichever of them the rig actually has is set; the rest are ignored.
            if (p.animator != null)
            {
                var d = p.moveDir;
                SetIfPresent(p.animator, "Horizontal", d.x);
                SetIfPresent(p.animator, "Vertical", d.y);
                SetIfPresent(p.animator, "Speed", d.magnitude);
            }
        }
    }

    static void SetIfPresent(Animator a, string param, float value)
    {
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Float && p.name == param) { a.SetFloat(param, value); return; }
    }

    Puppet EnsurePuppet(ulong owner, Vector3 pos, float facing)
    {
        if (_puppets.TryGetValue(owner, out var existing) && existing.go != null) return existing;

        var prefab = FindBodyPrefab();
        if (prefab == null) return null;   // no paddock in this scene (a menu, the garage sheet) — nothing to show

        var go = Instantiate(prefab, pos, Quaternion.Euler(0f, 0f, facing));
        go.name = $"CoopPlayer_{owner}";

        // Same treatment PitLaneStart gives the local body: the legacy scrolling components have no working
        // RaceManager here, and the scene's 3D URP renderer needs an unlit sprite material.
        var legacy = go.GetComponent<MovementOnFoot>();
        if (legacy != null) legacy.enabled = false;
        var pi = go.GetComponent<UnityEngine.InputSystem.PlayerInput>();
        if (pi != null) pi.enabled = false;

        var ofc = go.GetComponent<OnFootController>();
        if (ofc == null) ofc = go.AddComponent<OnFootController>();
        ofc.RemotePuppet = true;   // in All, so the crowd sees them; never Current, never reading our input

        var sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh != null) sr.sharedMaterial = new Material(sh);
            sr.color = remoteTint;   // tell the two of you apart at a glance
        }

        // Kinematic: their pose is authoritative from the network, so local physics must not push them
        // around or fight the interpolation.
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        var puppet = new Puppet
        {
            go = go,
            ofc = ofc,
            animator = go.GetComponent<Animator>(),
            targetPos = pos,
            targetFacing = facing,
            lastSeen = Time.unscaledTime,
        };

        // Who they are, above their head. The tag is an independent object rather than a child: the puppet
        // rotates to face where it is walking, and a parented tag would turn upside down with it.
        if (showNameTags) puppet.nameTag = CoopNameTag.Attach(go.transform, NameFor(owner));

        _puppets[owner] = puppet;
        return puppet;
    }

    void RetirePuppet(ulong owner)
    {
        if (!_puppets.TryGetValue(owner, out var p)) return;
        if (p.nameTag != null) p.nameTag.Detach();
        if (p.go != null) Destroy(p.go);
        _puppets.Remove(owner);
    }

    // The body prefab this scene spawns its player from. PitLaneStart holds it for the paddock; if there is
    // no paddock in this scene there is nothing to build a puppet out of, and none is built.
    GameObject FindBodyPrefab()
    {
        var start = FindFirstObjectByType<PitLaneStart>();
        return start != null ? start.onFootPrefab : null;
    }
}
