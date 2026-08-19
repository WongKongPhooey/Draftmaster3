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
        ReevaluateStatObjectives();          // an already-met threshold turns in immediately
        ReevaluateRelationshipObjective(q);  // ditto for an already-poisoned (or -friendly) relationship
        QuestHUD.Ensure();
    }

    public static void Complete(QuestInfo q)
    {
        SetState(q, State.Completed);
        if (!string.IsNullOrEmpty(q.rewardItemId)) PlayerInventory.Add(q.rewardItemId);
        PhoneNotes.ResolveQuest(q);   // the phone's Notes app keeps it, struck through, rather than dropping it
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

    // Relationship score changed (either party may be the player). Called by DriverRelationships.Modify.
    public static void OnRelationshipChanged(string a, string b, float value)
    {
        bool aIsPlayer = DriverRelationships.IsPlayerName(a);
        bool bIsPlayer = DriverRelationships.IsPlayerName(b);
        if (!aIsPlayer && !bIsPlayer) return;
        string other = aIsPlayer ? b : a;

        foreach (var q in All)
        {
            if (q == null || GetState(q) != State.Active) continue;
            // Empty driverName = any driver (field names reshuffle each race, so wildcards are the
            // reliable authoring choice for relationship quests).
            if (!string.IsNullOrEmpty(q.driverName) && !NameMatches(other, q.driverName)) continue;
            if (q.objective == QuestInfo.ObjectiveType.RelationshipBelow && value <= q.relationshipTarget)
                SetState(q, State.ReadyToTurnIn);
            else if (q.objective == QuestInfo.ObjectiveType.RelationshipAbove && value >= q.relationshipTarget)
                SetState(q, State.ReadyToTurnIn);
        }
    }

    // On accept, a relationship quest whose condition the player already satisfies turns in immediately
    // (mirrors how stat thresholds behave) — otherwise it would only complete on the NEXT score change.
    static void ReevaluateRelationshipObjective(QuestInfo q)
    {
        if (GetState(q) != State.Active) return;
        if (q.objective != QuestInfo.ObjectiveType.RelationshipBelow
            && q.objective != QuestInfo.ObjectiveType.RelationshipAbove) return;

        var rt = RacePositionTracker.Instance;
        string player = (rt != null && !string.IsNullOrEmpty(rt.playerName) ? rt.playerName : "You").Trim().ToLowerInvariant();
        foreach (var (a, b, value) in DriverRelationships.AllPairs())
        {
            string other = a == player ? b : (b == player ? a : null);
            if (other == null) continue;
            if (!string.IsNullOrEmpty(q.driverName) && !NameMatches(other, q.driverName)) continue;
            if ((q.objective == QuestInfo.ObjectiveType.RelationshipBelow && value <= q.relationshipTarget)
                || (q.objective == QuestInfo.ObjectiveType.RelationshipAbove && value >= q.relationshipTarget))
            {
                SetState(q, State.ReadyToTurnIn);
                return;
            }
        }
    }

    // A contact involving the player was logged. otherName = the non-player party; playerCaused = the
    // player was the striker. Called by DriverRelationships.ReportContact.
    public static void OnPlayerContact(string otherName, float severity, bool playerCaused)
    {
        foreach (var q in All)
        {
            if (q == null || q.objective != QuestInfo.ObjectiveType.ContactDriver) continue;
            if (GetState(q) != State.Active) continue;
            if (!string.IsNullOrEmpty(q.driverName) && !NameMatches(otherName, q.driverName)) continue;
            if (severity < q.minContactSeverity) continue;
            if (q.playerMustCause && !playerCaused) continue;
            SetState(q, State.ReadyToTurnIn);
        }
    }

    static bool NameMatches(string resultName, string questName)
    {
        if (string.IsNullOrEmpty(resultName) || string.IsNullOrEmpty(questName)) return false;
        return resultName.ToLowerInvariant().Contains(questName.ToLowerInvariant());
    }

    // Player's current relationship score with the quest's named driver, for HUD progress text.
    static int RelProgress(QuestInfo q)
    {
        var rt = RacePositionTracker.Instance;
        string player = rt != null && !string.IsNullOrEmpty(rt.playerName) ? rt.playerName : "You";
        return Mathf.RoundToInt(DriverRelationships.Get(player, q.driverName));
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
            case QuestInfo.ObjectiveType.RelationshipBelow:
                return string.IsNullOrEmpty(q.driverName)
                    ? $"Make an enemy (reach {q.relationshipTarget} with anyone)"
                    : $"Feud with {q.driverName} ({RelProgress(q)}/{q.relationshipTarget})";
            case QuestInfo.ObjectiveType.RelationshipAbove:
                return string.IsNullOrEmpty(q.driverName)
                    ? $"Win a friend (reach +{q.relationshipTarget} with anyone)"
                    : $"Make peace with {q.driverName} ({RelProgress(q)}/{q.relationshipTarget})";
            case QuestInfo.ObjectiveType.ContactDriver:
                string who = string.IsNullOrEmpty(q.driverName) ? "someone" : q.driverName;
                return q.minContactSeverity > 0.5f ? $"Wreck {who}" : $"Rattle {who}'s cage";
        }
        return "";
    }
}
