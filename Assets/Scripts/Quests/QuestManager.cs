using System.Collections.Generic;
using UnityEngine;

// Central quest state. Definitions are QuestInfo assets under Resources/Quests; per-quest state persists
// in PlayerPrefs ("quest.state.<id>"). Race outcomes arrive from RaceDirector.RecordCareerResult, stat
// changes from PlayerStatsLedger.Increment, item deliveries from QuestGiverNPC.
public static class QuestManager
{
    public enum State { NotStarted = 0, Active = 1, ReadyToTurnIn = 2, Completed = 3 }

    static QuestInfo[] _all;
    public static QuestInfo[] All
    {
        get
        {
            if (_all == null) _all = Resources.LoadAll<QuestInfo>("Quests");
            return _all;
        }
    }

    static string StateKey(QuestInfo q) => "quest.state." + q.id;
    static string BaselineKey(QuestInfo q) => "quest.base." + q.id;

    public static State GetState(QuestInfo q) => (State)PlayerPrefs.GetInt(StateKey(q), 0);

    static void SetState(QuestInfo q, State s)
    {
        PlayerPrefs.SetInt(StateKey(q), (int)s);
        PlayerPrefs.Save();
    }

    public static bool PrerequisiteMet(QuestInfo q)
    {
        if (string.IsNullOrEmpty(q.prerequisiteQuestId)) return true;
        foreach (var other in All)
            if (other != null && other.id == q.prerequisiteQuestId)
                return GetState(other) == State.Completed;
        return false; // prerequisite id names a quest that doesn't exist — treat as locked
    }

    public static void Accept(QuestInfo q)
    {
        if (GetState(q) != State.NotStarted) return;
        if (q.objective == QuestInfo.ObjectiveType.StatThreshold && q.countFromAccept)
            PlayerPrefs.SetInt(BaselineKey(q), PlayerStatsLedger.Get(q.statKey));
        SetState(q, State.Active);
        ReevaluateStatObjectives(); // an already-met threshold turns in immediately
        QuestHUD.Ensure();
    }

    public static void Complete(QuestInfo q)
    {
        SetState(q, State.Completed);
        if (!string.IsNullOrEmpty(q.rewardItemId)) PlayerInventory.Add(q.rewardItemId);
    }

    // DeliverItem turn-in: consumes the item. False if the player doesn't actually have it.
    public static bool TryDeliver(QuestInfo q)
    {
        if (GetState(q) != State.Active || q.objective != QuestInfo.ObjectiveType.DeliverItem) return false;
        if (!PlayerInventory.Remove(q.itemId)) return false;
        Complete(q);
        return true;
    }

    public static void ReevaluateStatObjectives()
    {
        foreach (var q in All)
        {
            if (q == null || q.objective != QuestInfo.ObjectiveType.StatThreshold) continue;
            if (GetState(q) != State.Active) continue;
            if (StatProgress(q) >= q.statTarget) SetState(q, State.ReadyToTurnIn);
        }
    }

    public static int StatProgress(QuestInfo q)
    {
        int v = PlayerStatsLedger.Get(q.statKey);
        if (q.countFromAccept) v -= PlayerPrefs.GetInt(BaselineKey(q), 0);
        return v;
    }

    // Final race classification, 1-based by list order. Called once per race by RaceDirector.
    public static void OnRaceFinished(IReadOnlyList<(string name, bool isPlayer)> classification)
    {
        int playerPos = 0;
        for (int i = 0; i < classification.Count; i++)
            if (classification[i].isPlayer) { playerPos = i + 1; break; }
        if (playerPos == 0) return;

        foreach (var q in All)
        {
            if (q == null || GetState(q) != State.Active) continue;
            switch (q.objective)
            {
                case QuestInfo.ObjectiveType.BeatDriverInRace:
                    int rivalPos = 0;
                    for (int i = 0; i < classification.Count; i++)
                        if (!classification[i].isPlayer && NameMatches(classification[i].name, q.driverName))
                        { rivalPos = i + 1; break; }
                    if (rivalPos == 0) break; // rival wasn't in this race — no attempt consumed
                    if (playerPos < rivalPos) SetState(q, State.ReadyToTurnIn);
                    else if (q.singleRaceAttempt) SetState(q, State.NotStarted);
                    break;

                case QuestInfo.ObjectiveType.FinishRacePosition:
                    if (playerPos <= q.targetPosition) SetState(q, State.ReadyToTurnIn);
                    break;
            }
        }
    }

    static bool NameMatches(string resultName, string questName)
    {
        if (string.IsNullOrEmpty(resultName) || string.IsNullOrEmpty(questName)) return false;
        return resultName.ToLowerInvariant().Contains(questName.ToLowerInvariant());
    }

    // Quests worth showing in the HUD.
    public static List<QuestInfo> Tracked()
    {
        var list = new List<QuestInfo>();
        foreach (var q in All)
        {
            if (q == null) continue;
            var s = GetState(q);
            if (s == State.Active || s == State.ReadyToTurnIn) list.Add(q);
        }
        return list;
    }

    public static string DescribeProgress(QuestInfo q)
    {
        if (GetState(q) == State.ReadyToTurnIn) return "Done — report back";
        switch (q.objective)
        {
            case QuestInfo.ObjectiveType.BeatDriverInRace:
                return $"Beat {q.driverName} in the race";
            case QuestInfo.ObjectiveType.FinishRacePosition:
                return q.targetPosition == 1 ? "Win a race" : $"Finish P{q.targetPosition} or better";
            case QuestInfo.ObjectiveType.StatThreshold:
                return $"{Mathf.Min(StatProgress(q), q.statTarget)}/{q.statTarget} {q.statKey}";
            case QuestInfo.ObjectiveType.DeliverItem:
                string what = string.IsNullOrEmpty(q.itemDisplayName) ? q.itemId : q.itemDisplayName;
                return PlayerInventory.Has(q.itemId) ? $"Deliver the {what}" : $"Find a {what}";
        }
        return "";
    }
}
