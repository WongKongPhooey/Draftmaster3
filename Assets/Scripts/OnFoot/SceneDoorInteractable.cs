using UnityEngine;
using UnityEngine.SceneManagement;

// A door out of an on-foot scene. Walk up, press the action button, load somewhere else.
//
// The team factory is otherwise a room with no exit: nothing in it changes scene, and the pause menu only
// arms itself in scenes that have a TrackBuilder, so there is no Escape to fall back on. Rather than give
// that one scene a bespoke menu, the way out is a thing in the room — the same walk-up interaction as
// every other object the player uses.
//
// Like SatnavInteractable and LaptopInteractable this never talks, so it never locks movement.
public class SceneDoorInteractable : NPCInteractable
{
    [Header("Door")]
    [Tooltip("Scene loaded on interact. Must be in the build settings, or the door does nothing.")]
    public string sceneName = GarageScreenLoader.TitleSceneName;

    public override bool IsTalking => false;

    public override bool Interact()
    {
        if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"SceneDoorInteractable on '{name}': '{sceneName}' is not in the Build Settings — the door leads nowhere.");
            return false;
        }
        SceneManager.LoadScene(sceneName);
        return false;
    }
}
