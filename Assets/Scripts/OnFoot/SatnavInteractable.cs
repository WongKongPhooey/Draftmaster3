using UnityEngine;

// The RV's dashboard satnav. Reuses the on-foot NPC prompt + interaction machinery: OnFootController
// shows a floating "E" prompt while the player is within interactRange and calls Interact() on the
// action button. Instead of talking, interacting opens the travel map — the same map otherwise only
// reachable via the F9 dev hotkey. Built by RVInterior inside the masked interior, so it registers in
// NPCInteractable.All (and thus prompts) only while the player is actually inside the RV; walking out
// deactivates the interior root, which unregisters it.
public class SatnavInteractable : NPCInteractable
{
    // Never carries a conversation, so it never locks player movement.
    public override bool IsTalking => false;

    public override bool Interact()
    {
        if (!TravelMapScreen.IsOpen) TravelMapScreen.Open();
        return false; // the map takes over — no ongoing conversation for OnFootController to track
    }
}
