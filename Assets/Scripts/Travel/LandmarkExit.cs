using UnityEngine;

// The player's parked car in the Landmark scene. Interact to get back on the road — returns to the
// scene the travel map was floating over and reopens the map (LandmarkLoader remembers the way).
public class LandmarkExit : NPCInteractable
{
    public override bool Interact()
    {
        LandmarkLoader.ExitToRoad();
        return false;
    }
}
