using Unity.Netcode.Components;

// Owner-authoritative NetworkTransform for cars in a co-op career.
//
// Possession is the whole point of co-op: the guest takes over a car that is already out on track. That car
// starts life spawned by the host and driven by the host's AI, and later has to be driven by the guest —
// so the machine that owns the car has to be the machine whose pose wins.
//
// NetworkTransform decides that at the component level, not per instance: OnIsServerAuthoritative is
// virtual and fixed for the type, so authority cannot be flipped on one car at runtime. Owner authority
// covers both halves anyway, with nothing to switch: a server-spawned NetworkObject is owned by the server,
// so while the host's AI is driving, owner-authoritative and server-authoritative are the same thing. The
// moment ChangeOwnership hands the car to the guest, the guest's pose becomes the authoritative one and the
// host's copy turns into a puppet — which is exactly the handover possession needs.
public class CoopNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}
