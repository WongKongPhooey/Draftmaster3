using System;

// A plain data description of a side-quest an NPC can hand out. These are
// defined in code in QuestManager's catalogue and mirrored into the local
// database (the SideQuests table) so progress and completion survive a restart.
[Serializable]
public class SideQuest
{
    // Stable identifier used as the database primary key and as the argument in
    // Ink dialogue tags (e.g. #quest_start:KyleCaution).
    public string id;

    // Player-facing text for a quest log / prompt.
    public string title;
    public string description;

    // Name of the NPC who gives the quest (purely informational).
    public string giver;

    // What the player has to do. "objectiveEvent" is matched against the event
    // key reported by gameplay systems (see QuestManager.ReportEvent); "target"
    // is how many of that event are needed to complete.
    //   objectiveEvent = "talk"           -> completes the moment it starts
    //   objectiveEvent = "drive_distance" -> target metres driven
    //   objectiveEvent = "race_finish"    -> target races finished
    public string objectiveEvent;
    public int target;

    // Reward paid out by StatsManager when the quest completes.
    public string rewardStat;
    public int rewardAmount;

    public SideQuest(string id, string title, string description, string giver,
                     string objectiveEvent, int target, string rewardStat, int rewardAmount){
        this.id = id;
        this.title = title;
        this.description = description;
        this.giver = giver;
        this.objectiveEvent = objectiveEvent;
        this.target = target < 1 ? 1 : target;
        this.rewardStat = rewardStat;
        this.rewardAmount = rewardAmount;
    }
}
