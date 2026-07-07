using UnityEngine;

// The shopkeeper at a landmark location. Walk up, interact: opens the parts shop panel for the
// location the player is parked at (TravelState.CurrentNodeId); interact again to close — the
// RoleStation pattern, so OnFootController's prompt, facing and movement lock all work unchanged.
// If an NPCLayeredAppearance is present it's built with a seed derived from the location id, so
// each shop has the same face every visit (geography is fixed; so are the people).
public class ShopCounterNPC : NPCInteractable
{
    bool _open;

    public override bool IsTalking => _open;

    void Start()
    {
        var layered = GetComponent<NPCLayeredAppearance>();
        if (layered == null || layered.Built) return;
        if (layered.library == null) layered.library = Resources.Load<NPCPartLibrary>("NPC/NPCPartLibrary");
        if (layered.library != null && layered.Build(TravelState.CurrentNodeId.GetHashCode()))
            layered.SetFrame(0); // standing still; nobody walks the counter
    }

    public override bool Interact()
    {
        var panel = LandmarkShopPanel.Ensure();
        if (!_open)
        {
            _open = true;
            panel.Show();
            return true;  // keep focus so the player stays put while browsing
        }
        _open = false;
        panel.Hide();
        return false;
    }
}
