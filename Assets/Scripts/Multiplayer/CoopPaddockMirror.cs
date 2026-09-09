using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// Gives the co-op guest the host's paddock, rather than letting it lay out one of its own.
//
// The paddock is not scenery that happens to look the same on both machines — it is the frame every other
// place in the weekend is measured from. DriverMotorhomeLot parks a row of motorhomes; PopupGarageLot
// continues that row with the team rigs; the walkable pocket is cut round both; the player's own RV (and
// the masked room inside it) is DRIVEN INTO its place in the row rather than left where it was authored;
// and WeekendVenueSites lays the drivers' room, the hospitality awning, the fan fence and the intro stage
// out in the first clear ground past the far end of all of it.
//
// So a guest that solved the row for itself would not merely park different names on the boards. Its row
// is built from ITS OWN player car number and ITS OWN driver database, and one rig more or fewer moves the
// end of the block, which moves the venue cluster, which moves four rooms' worth of walls. That is exactly
// what it did: one player stood inside the drivers' meeting while the other watched them stand in open
// ground sixty metres away, with each of them walking into walls the other could not see.
//
// The fix is the rule the rest of co-op already follows — the host decides, the guest receives — applied to
// the layout rather than to the objects. What travels is small: the line the host solved (origin, axes,
// spacing, how many places per row), which place the player's rig holds, and who is parked in the rest.
// The guest then runs the SAME placement code over it, so both machines build the identical block out of
// their own local prefabs. Nothing is spawned across the wire and nothing has to survive a scene load.
//
// Re-sent on a slow loop rather than once, for the same reason CoopBodies re-sends the player's name: the
// guest's row is rebuilt from nothing on every scene load, and the career reloads the scene constantly.
// A layout that arrives before the guest's own RV has spawned is refused and simply comes round again.
public class CoopPaddockMirror : MonoBehaviour
{
    const string LayoutMessage = "coop.paddock.layout";

    // Bumped whenever the fields below change, so a peer on an older build is refused rather than reading
    // one field's bytes as another's and parking the whole paddock somewhere plausible but wrong.
    const int LayoutVersion = 1;

    // Written last and checked last. A silently mis-ordered read is the one failure this message can have
    // that looks like success — every field is a float or an int, so the numbers come out believable and
    // the block simply lands in the wrong place, which is the exact bug this whole component exists to fix.
    const int EndMarker = unchecked((int)0xDEADBE71);

    // Ceiling on how many rigs a layout may claim, so a misread length cannot ask for an allocation big
    // enough to end the process. Three series' entry lists over would still fit.
    const int MaxSlots = 256;

    [Tooltip("Seconds between layout broadcasts while a guest is connected. The layout only changes on a scene load, so this is a heal loop rather than a feed — it exists so a guest that reloaded, or joined late, does not have to be noticed by anything.")]
    public float sendInterval = 2f;

    float _nextSend;
    bool _registered;

    // What this layer last did, for the co-op debug panel.
    public static int LastSentSlots { get; private set; }
    public static float LastLayoutAt { get; private set; }   // unscaled time a layout last arrived (guest)
    public static bool GuestApplied { get; private set; }

    void OnDestroy() => Unregister();

    void Update()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || !Coop.Active) { Unregister(); return; }

        Register(nm);

        if (!nm.IsServer) return;
        if (!Coop.GuestPresent) return;
        if (Time.unscaledTime < _nextSend) return;

        _nextSend = Time.unscaledTime + Mathf.Max(0.5f, sendInterval);
        SendLayout(nm);
    }

    // ------------------------------------------------------------------ wiring

    void Register(NetworkManager nm)
    {
        if (_registered) return;
        var msg = nm.CustomMessagingManager;
        if (msg == null) return;

        // Only the guest listens: the host is the one answer, and a relay would be a peer telling the
        // owner of the career what its own paddock looks like.
        if (!nm.IsServer) msg.RegisterNamedMessageHandler(LayoutMessage, OnLayout);

        _registered = true;
        _nextSend = 0f;   // a guest already connected gets the layout on the very next frame
    }

    void Unregister()
    {
        if (!_registered) return;
        _registered = false;
        GuestApplied = false;

        var nm = NetworkManager.Singleton;
        var msg = nm != null ? nm.CustomMessagingManager : null;
        if (msg != null) msg.UnregisterNamedMessageHandler(LayoutMessage);
    }

    // ------------------------------------------------------------------ host side

    void SendLayout(NetworkManager nm)
    {
        var msg = nm.CustomMessagingManager;
        if (msg == null) return;

        var lot = DriverMotorhomeLot.Instance;
        if (lot == null || !lot.Built || !lot.HasLine || lot.Slots.Count == 0) return;

        var line = lot.Line;
        var slots = lot.Slots;

        // Fixed header, then four strings a slot. Sized generously and allowed to grow: a full entry list
        // is forty-odd rigs, which is past one MTU and has to fragment.
        int size = 128 + slots.Count * 192;
        using var writer = new FastBufferWriter(size, Allocator.Temp, 1024 * 1024);

        writer.WriteValueSafe(LayoutVersion);
        writer.WriteValueSafe(line.origin);
        writer.WriteValueSafe(line.axis);
        writer.WriteValueSafe(line.front);
        writer.WriteValueSafe(line.rotation);
        writer.WriteValueSafe(line.pitch);
        writer.WriteValueSafe(line.rowPitch);
        writer.WriteValueSafe(line.depth);
        writer.WriteValueSafe(line.perRow);

        writer.WriteValueSafe(lot.LineRows);
        writer.WriteValueSafe(lot.PlayerPlace);
        writer.WriteValueSafe(lot.AisleRowGap);

        writer.WriteValueSafe(slots.Count);
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            writer.WriteValueSafe(s.carNumber);
            writer.WriteValueSafe(s.fullName ?? "");
            writer.WriteValueSafe(s.shortName ?? "");
            writer.WriteValueSafe(s.teamName ?? "");
        }

        writer.WriteValueSafe(EndMarker);

        msg.SendNamedMessageToAll(LayoutMessage, writer, NetworkDelivery.ReliableFragmentedSequenced);
        LastSentSlots = slots.Count;
    }

    // ------------------------------------------------------------------ guest side

    void OnLayout(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int version);
        if (version != LayoutVersion)
        {
            Debug.LogError($"CoopPaddockMirror: the host sent a paddock layout in format v{version}, this " +
                           $"build reads v{LayoutVersion}. The two of you are on different builds — the " +
                           "paddock has not been taken, so the venues on this machine will not line up " +
                           "with the host's.");
            return;
        }

        var line = new DriverMotorhomeLot.LineLayout();
        reader.ReadValueSafe(out Vector3 origin);
        reader.ReadValueSafe(out Vector3 axis);
        reader.ReadValueSafe(out Vector3 front);
        reader.ReadValueSafe(out Quaternion rotation);
        reader.ReadValueSafe(out float pitch);
        reader.ReadValueSafe(out float rowPitch);
        reader.ReadValueSafe(out float depth);
        reader.ReadValueSafe(out int perRow);

        line.origin = origin;
        line.axis = axis;
        line.front = front;
        line.rotation = rotation;
        line.pitch = pitch;
        line.rowPitch = rowPitch;
        line.depth = depth;
        line.perRow = Mathf.Max(1, perRow);

        reader.ReadValueSafe(out int rows);
        reader.ReadValueSafe(out int playerPlace);
        reader.ReadValueSafe(out float aisleRowGap);
        reader.ReadValueSafe(out int count);

        // A field is forty-odd cars and a full entry list is not much more. Anything past this is a stream
        // read out of step rather than a very large race, and reserving it would be the last thing this
        // process did — so refuse it before the allocation rather than after.
        if (count < 0 || count > MaxSlots)
        {
            Debug.LogError($"CoopPaddockMirror: a paddock layout claiming {count} motorhomes is not a " +
                           "paddock, it is a misread stream. Nothing has been placed.");
            return;
        }

        var slots = new List<DriverMotorhomeLot.SlotInfo>(count);
        for (int i = 0; i < count; i++)
        {
            reader.ReadValueSafe(out int carNumber);
            reader.ReadValueSafe(out string fullName);
            reader.ReadValueSafe(out string shortName);
            reader.ReadValueSafe(out string teamName);
            slots.Add(new DriverMotorhomeLot.SlotInfo
            {
                carNumber = carNumber,
                fullName = fullName,
                shortName = shortName,
                teamName = teamName,
            });
        }

        reader.ReadValueSafe(out int end);
        if (end != EndMarker)
        {
            Debug.LogError("CoopPaddockMirror: the paddock layout did not end where it should have, so the " +
                           "fields were read out of step. Nothing has been placed — a half-read layout " +
                           "parks the whole block somewhere believable and wrong, which is worse than none.");
            return;
        }

        LastLayoutAt = Time.unscaledTime;

        // No lot in this scene is not a fault: menus, the garage sheet and the team factory have no
        // paddock, and the layout is simply dropped there.
        var lot = DriverMotorhomeLot.Instance;
        if (lot == null || lot.Built) return;

        lot.ApplyRemoteLayout(line, rows, playerPlace, aisleRowGap, slots);
        GuestApplied = lot.Built;
    }
}
