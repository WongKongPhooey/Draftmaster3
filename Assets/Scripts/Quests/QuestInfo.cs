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

    [Header("Reward")]
    [Tooltip("Optional item granted on completion — e.g. completing one quest hands over the charm a later quest needs.")]
    public string rewardItemId = "";
    [Tooltip("Shown in the HUD toast when the quest completes. Cosmetic for now.")]
    public string rewardText = "";
}
