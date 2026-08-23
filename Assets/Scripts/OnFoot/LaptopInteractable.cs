using UnityEngine;

// The laptop the player's car sheet lives on. Sits on the table in the RV and on the desk in the team
// factory; walking up to it and pressing the action button opens the garage screen (GarageScreenLoader),
// and BACK on that screen comes straight back to this scene.
//
// Same trick as SatnavInteractable: subclass NPCInteractable so OnFootController's proximity prompt and
// action-button handling work unchanged, and override what "interact" does. Never carries a conversation,
// so it never locks the player's movement — the scene change is the whole interaction.
public class LaptopInteractable : NPCInteractable
{
    public override bool IsTalking => false;

    public override bool Interact()
    {
        GarageScreenLoader.Open();
        return false; // the garage screen takes over — no ongoing conversation for OnFootController to track
    }
}
