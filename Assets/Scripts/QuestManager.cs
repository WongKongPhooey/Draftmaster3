using System.Collections.Generic;
using UnityEngine;

// QuestManager owns the lifecycle of every side-quest: it holds the in-code
// catalogue of quests, mirrors them into the database, and drives them through
// available -> active -> completed. NPC dialogue starts quests through here, and
// gameplay systems (driving) push progress in through ReportEvent. When a quest
// completes its reward is paid out via StatsManager and the new status is
// persisted by DatabaseManager, so nothing is lost between sessions.
public static class QuestManager
{
    // The catalogue of side-quests available in the game. Keyed by quest id.
    // Built lazily on first access so it survives scene loads / domain reloads.
    private static Dictionary<string, SideQuest> catalogue;

    // Fired when a quest is started and when it is completed so UI can react.
    public static event System.Action<SideQuest> OnQuestStarted;
    public static event System.Action<SideQuest> OnQuestCompleted;

    private static Dictionary<string, SideQuest> Catalogue {
        get {
            if(catalogue == null){
                BuildCatalogue();
            }
            return catalogue;
        }
    }

    // ---------------------------------------------------------------------
    // Catalogue
    // ---------------------------------------------------------------------

    private static void BuildCatalogue(){
        catalogue = new Dictionary<string, SideQuest>();

        Register(new SideQuest(
            "RickLemondeLogoLap",
            "Lemonde's Lap",
            "Rick wants you to run a full race showing off his sponsor livery.",
            "Rick Lemonde",
            "race_finish", 1,
            StatsManager.Reputation, 5));

        Register(new SideQuest(
            "KyleCaution",
            "Cause a Caution",
            "Kyle reckons the race needs a late caution. Cover 8km of racing first.",
            "Kyle Miller",
            "drive_distance", 8000,
            StatsManager.Charisma, 3));

        Register(new SideQuest(
            "JoshPatience",
            "The Waiting Game",
            "Josh just wants you to enjoy a race while the game is finished.",
            "Josh Wong-Grant",
            "race_finish", 1,
            StatsManager.Charisma, 2));
    }

    // Adds a quest to the in-memory catalogue and ensures a matching row exists
    // in the database (without clobbering any existing progress).
    private static void Register(SideQuest quest){
        catalogue[quest.id] = quest;
        DatabaseManager.RegisterQuest(quest.id, quest.target, quest.rewardStat, quest.rewardAmount);
    }

    public static SideQuest GetQuest(string questId){
        SideQuest quest;
        return Catalogue.TryGetValue(questId, out quest) ? quest : null;
    }

    // ---------------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------------

    public static bool IsActive(string questId){
        return DatabaseManager.GetQuestStatus(questId) == "active";
    }

    public static bool IsCompleted(string questId){
        return DatabaseManager.GetQuestStatus(questId) == "completed";
    }

    // Called from NPC dialogue to hand a quest to the player. Quests that are
    // already active or completed are ignored so a chatty NPC can't reset them.
    // Objectives that complete on the spot (objectiveEvent == "talk") are
    // finished immediately.
    public static bool StartQuest(string questId){
        SideQuest quest = GetQuest(questId);
        if(quest == null){
            Debug.LogWarning("[Quest] Unknown quest id: " + questId);
            return false;
        }

        string status = DatabaseManager.GetQuestStatus(questId);
        if(status == "active" || status == "completed"){
            return false;
        }

        DatabaseManager.SetQuestStatus(questId, "active");
        Debug.Log("[Quest] Started '" + quest.title + "' (" + quest.description + ")");
        OnQuestStarted?.Invoke(quest);

        if(quest.objectiveEvent == "talk"){
            CompleteQuest(questId);
        }
        return true;
    }

    // Pushes progress into every active quest whose objective matches the given
    // event key, completing any that reach their target. Driving systems call
    // this (e.g. ReportEvent("drive_distance", metresThisRace)).
    public static void ReportEvent(string objectiveEvent, int amount){
        if(string.IsNullOrEmpty(objectiveEvent) || amount <= 0){
            return;
        }

        foreach(string questId in DatabaseManager.GetActiveQuestIds()){
            SideQuest quest = GetQuest(questId);
            if(quest == null || quest.objectiveEvent != objectiveEvent){
                continue;
            }
            int progress = DatabaseManager.AddQuestProgress(questId, amount);
            Debug.Log("[Quest] '" + quest.title + "' progress " + progress + "/" + quest.target);
            if(progress >= quest.target){
                CompleteQuest(questId);
            }
        }
    }

    // Directly advances a single named quest (used by dialogue tags).
    public static void AdvanceQuest(string questId, int amount){
        if(!IsActive(questId)){
            return;
        }
        SideQuest quest = GetQuest(questId);
        int progress = DatabaseManager.AddQuestProgress(questId, amount);
        if(quest != null && progress >= quest.target){
            CompleteQuest(questId);
        }
    }

    // Marks a quest completed and pays out its stat reward. Idempotent - calling
    // it twice won't award the reward twice.
    public static bool CompleteQuest(string questId){
        if(DatabaseManager.GetQuestStatus(questId) == "completed"){
            return false;
        }

        SideQuest quest = GetQuest(questId);
        DatabaseManager.SetQuestStatus(questId, "completed");

        if(quest != null && !string.IsNullOrEmpty(quest.rewardStat) && quest.rewardAmount != 0){
            StatsManager.GainStat(quest.rewardStat, quest.rewardAmount);
        }

        Debug.Log("[Quest] Completed '" + (quest != null ? quest.title : questId) + "'");
        if(quest != null){
            OnQuestCompleted?.Invoke(quest);
        }
        return true;
    }
}
