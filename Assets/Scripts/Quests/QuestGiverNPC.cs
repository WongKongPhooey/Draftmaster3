using UnityEngine;

// An NPCInteractable that fronts a quest: offers it, comments while it's underway, accepts the turn-in
// (or the item delivery), and hands out items. The right line set is chosen when the conversation opens,
// and the state change lands when it ends — so backing out mid-conversation still commits (matching how
// the base class cycles lines), but the player has heard the pitch first.
public class QuestGiverNPC : NPCInteractable
{
    [Header("Quest")]
    public QuestInfo quest;
    [Tooltip("This NPC offers the quest. Off for an NPC that's only the delivery target of someone else's quest.")]
    public bool offersQuest = true;
    [Tooltip("This NPC is where a DeliverItem quest's item gets handed over.")]
    public bool isDeliveryTarget = false;
    [Tooltip("Item id granted when the player accepts the quest (e.g. a package to carry). Empty = none.")]
    public string grantItemOnAccept = "";

    [Header("Lines by quest state (#player suffix works as usual)")]
    [TextArea] public string[] offerLines = { "Got a job for you." };
    [TextArea] public string[] activeLines = { "How's it coming along?" };
    [TextArea] public string[] turnInLines = { "You did it! Thanks." };
    [TextArea] public string[] completedLines = { "Thanks again for the help." };
    [TextArea] public string[] lockedLines = { "Come back once you've made a name for yourself." };

    enum Outcome { None, Accept, TurnIn, Deliver }
    Outcome _pending;

    public override bool Interact()
    {
        if (quest == null) return base.Interact();
        if (!IsTalking) PrepareConversation();
        bool ongoing = base.Interact();
        if (!ongoing) ResolveOutcome();
        return ongoing;
    }

    void PrepareConversation()
    {
        _pending = Outcome.None;
        switch (QuestManager.GetState(quest))
        {
            case QuestManager.State.NotStarted:
                if (!offersQuest) { lines = activeLines; }
                else if (!QuestManager.PrerequisiteMet(quest)) { lines = lockedLines; }
                else { lines = offerLines; _pending = Outcome.Accept; }
                break;

            case QuestManager.State.Active:
                if (isDeliveryTarget && quest.objective == QuestInfo.ObjectiveType.DeliverItem
                    && PlayerInventory.Has(quest.itemId))
                { lines = turnInLines; _pending = Outcome.Deliver; }
                else lines = activeLines;
                break;

            case QuestManager.State.ReadyToTurnIn:
                // Delivery quests turn in at the target NPC; everything else at the giver.
                if (!isDeliveryTarget && quest.objective == QuestInfo.ObjectiveType.DeliverItem) lines = activeLines;
                else { lines = turnInLines; _pending = Outcome.TurnIn; }
                break;

            case QuestManager.State.Completed:
                lines = completedLines;
                break;
        }
    }

    void ResolveOutcome()
    {
        switch (_pending)
        {
            case Outcome.Accept:
                QuestManager.Accept(quest);
                // Log it on the player's phone: a quest here is a conversation, and the Notes app is
                // the only record of who asked for what.
                PhoneNotes.RecordQuest(quest, speakerName);
                if (!string.IsNullOrEmpty(grantItemOnAccept)) PlayerInventory.Add(grantItemOnAccept);
                break;
            case Outcome.TurnIn:
                QuestManager.Complete(quest);
                break;
            case Outcome.Deliver:
                QuestManager.TryDeliver(quest);
                break;
        }
        _pending = Outcome.None;
    }
}
