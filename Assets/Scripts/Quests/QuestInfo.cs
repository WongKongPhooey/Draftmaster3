using UnityEngine;

// A side quest, defined as data. Assets live under Resources/Quests so QuestManager can find them all.
// One objective per quest; chain quests via prerequisiteQuestId (e.g. get the charm, then meet the
// NPC who needs one).
[CreateAssetMenu(fileName = "Quest", menuName = "Quests/Quest")]
public class QuestInfo : ScriptableObject
{
    public enum ObjectiveType
    {
        BeatDriverInRace,    // finish ahead of a named driver
        FinishRacePosition,  // finish at or better than targetPosition
        StatThreshold,       // a PlayerStatsLedger counter reaches statTarget
        DeliverItem,         // hand itemId to the quest's delivery-target NPC
        RelationshipBelow,   // drive the player's relationship with driverName DOWN to relationshipTarget (make an enemy)
        RelationshipAbove,   // bring the player's relationship with driverName UP to relationshipTarget (make peace)
        ContactDriver,       // cause a contact of at least minContactSeverity with driverName (a hit job)
    }

    [Header("Identity")]
    [Tooltip("Stable save key — never change once players have progress against it.")]
    public string id = "";
    public string title = "";
    [TextArea] public string description = "";
    [Tooltip("Optional: quest id that must be Completed before this one can be offered.")]
    public string prerequisiteQuestId = "";

    [Header("Objective")]
    public ObjectiveType objective = ObjectiveType.FinishRacePosition;

    [Tooltip("BeatDriverInRace: driver to finish ahead of. Matched case-insensitively against race-result names.")]
    public string driverName = "";
    [Tooltip("BeatDriverInRace: true = one shot, failing the next race puts the quest back to NotStarted; false = keep trying every race.")]
    public bool singleRaceAttempt = false;

    [Tooltip("FinishRacePosition: finish at or better than this (1 = win).")]
    public int targetPosition = 10;

    [Tooltip("StatThreshold: PlayerStatsLedger key, e.g. 'starts', 'wins', 'starts.chevrolet'.")]
    public string statKey = "starts";
    public int statTarget = 30;
    [Tooltip("StatThreshold: count from the moment the quest is accepted rather than the career total.")]
    public bool countFromAccept = false;

    [Tooltip("DeliverItem: inventory item id the delivery-target NPC needs.")]
    public string itemId = "";
    [Tooltip("Display name for the item in HUD text, e.g. 'lucky charm'.")]
    public string itemDisplayName = "";

    [Tooltip("RelationshipBelow/Above: the score the player's relationship with driverName must reach (Below: at or under, e.g. -60; Above: at or over, e.g. 40). Range -100..100.")]
    public int relationshipTarget = -60;
    [Tooltip("ContactDriver: minimum contact severity (0..1) that counts. ~0.2 is a bump, ~0.6 a proper slam.")]
    [Range(0f, 1f)] public float minContactSeverity = 0.3f;
    [Tooltip("ContactDriver: when true the PLAYER must be the striker (drove into them); false counts any mutual contact.")]
    public bool playerMustCause = true;

    [Header("Reward")]
    [Tooltip("Optional item granted on completion — e.g. completing one quest hands over the charm a later quest needs.")]
    public string rewardItemId = "";
    [Tooltip("Shown in the HUD toast when the quest completes. Cosmetic for now.")]
    public string rewardText = "";
}
