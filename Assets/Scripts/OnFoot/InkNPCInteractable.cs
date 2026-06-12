using UnityEngine;

// NPC whose conversation comes from a compiled Ink story (.json), displayed through the
// Phoenix-proven DialogueHandler / PlayerDialogue world-space canvases.
// Interact once to start; each further interact advances. Lines tagged #player route to
// the player's own DialogueCanvas via DialogueManager.
public class InkNPCInteractable : NPCInteractable
{
    [Tooltip("Compiled ink story (.json TextAsset from Assets/Dialogue).")]
    public TextAsset inkJSON;
    [Tooltip("DialogueHandler on the child DialogueCanvas. Auto-found if empty.")]
    public DialogueHandler handler;

    bool _inkTalking;

    void Awake()
    {
        if (handler == null) handler = GetComponentInChildren<DialogueHandler>(true);
        if (handler != null && handler.actor == null) handler.actor = gameObject;
    }

    public override bool IsTalking => _inkTalking;

    public override bool Interact()
    {
        if (handler == null || inkJSON == null) return false;

        if (!_inkTalking)
        {
            // endDialogue() deactivates the canvas GO but leaves the Canvas renderer enabled,
            // which TriggerDialogue treats as "already running" — reset both before restarting.
            handler.gameObject.SetActive(true);
            var cv = handler.GetComponent<Canvas>();
            if (cv != null) cv.enabled = false;
            handler.TriggerDialogue(inkJSON);
            _inkTalking = true;
            return true;
        }

        // A #player line parks the conversation on the player's canvas; advancing goes through it.
        var playerCanvas = DialogueManager.getPlayerDialogueCanvas();
        var playerDialogue = playerCanvas != null ? playerCanvas.GetComponent<PlayerDialogue>() : null;
        var playerRenderer = playerCanvas != null ? playerCanvas.GetComponent<Canvas>() : null;
        if (playerDialogue != null && playerRenderer != null && playerRenderer.enabled)
            playerDialogue.respondToDialogue();
        else
            handler.AdvanceDialogue(inkJSON);

        if (!handler.gameObject.activeSelf)
        {
            _inkTalking = false; // story hit END — handler deactivated its canvas
            return false;
        }
        return true;
    }
}
