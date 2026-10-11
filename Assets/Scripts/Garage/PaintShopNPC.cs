using UnityEngine;

// The Paint Scheme Designer at Team HQ. Talks first — an ordinary NPCInteractable conversation, so the
// lines are authored on the component like any other NPC — then opens the paint screen (PaintShopPanel)
// once the last line has been read. The player stays planted while the screen is up; SAVE, CANCEL or
// Escape closes it and ends the conversation.
public class PaintShopNPC : NPCInteractable
{
    bool _panelOpen;

    void Reset()
    {
        speakerName = "Paint Scheme Designer";
        lines = new[]
        {
            "Back for a new look? The booth's warm.",
            "Pick your base coat and a livery, then tell me whose name goes where.",
        };
        interactRange = 2.4f;
    }

    public override bool IsTalking => base.IsTalking || _panelOpen;

    public override bool Interact()
    {
        if (_panelOpen)
        {
            // The screen owns the buttons while it's up; a stray interact press must not end the visit.
            if (PaintShopPanel.Instance != null && PaintShopPanel.Instance.IsOpen) return true;
            _panelOpen = false;
            return false;
        }

        if (base.Interact()) return true;          // still talking

        // Conversation over: into the booth.
        _panelOpen = true;
        PaintShopPanel.Ensure().Show(() => _panelOpen = false);
        return true;
    }
}
