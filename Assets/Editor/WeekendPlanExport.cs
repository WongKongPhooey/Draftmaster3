using System.IO;
using Draftmaster.Weekend;
using UnityEditor;
using UnityEngine;

// Turning the generated schedule into a plan file, and the menu items around authoring one.
//
// Starting a track from a blank sheet is right when you know what you want the weekend to be; starting it
// from what the round already plays as is right the other 90% of the time. `FromTimetable` writes the
// procedural weekend out in the authored format, so the first edit is a change rather than a rewrite.
public static class WeekendPlanExport
{
    // The current generated weekend for a track and series, expressed as a plan file.
    public static WeekendPlan FromTimetable(string track, RacingSeries series)
    {
        var plan = WeekendPlan.Empty(track, series);
        plan.notes = "Exported from the generated schedule. Edit freely — this file now IS the weekend.";

        // weekendId 1 rather than 0 deliberately: weekend zero carries the one-off rookie orientation, and
        // exporting that into every track's plan would hand the phone tutorial out at all thirty-five rounds.
        var timetable = WeekendTimetable.Build(series, weekendId: 1, trackName: "");

        foreach (var a in timetable.Activities)
        {
            if (a == null) continue;
            if (!WeekendEventCatalog.TryGetByKind(a.kind, a.series, out var type)) continue;

            var e = new WeekendPlanEvent
            {
                @event = type.id,
                start = WeekendSlots.Clock(a.startMinute),
                minutes = a.minutes == type.minutes ? 0 : a.minutes,
                series = ActivityKinds.IsSpectate(a.kind) ? a.series.ToString() : "",
                // Only carry across what actually differs from the catalogue, so the file reads as a
                // schedule rather than as a dump of every field on every row.
                title = a.title == type.title ? "" : a.title,
                subtitle = a.subtitle == type.subtitle ? "" : a.subtitle,
                fee = a.appearanceFee == type.appearanceFee ? -1 : a.appearanceFee,
                skipMoney = a.skipMoneyPenalty == type.skipMoney ? -1 : a.skipMoneyPenalty,
                skipAppeal = Mathf.Approximately(a.skipAppealPenalty, type.skipAppeal) ? -1f : a.skipAppealPenalty,
                skipReason = a.skipReason == type.skipReason ? "" : a.skipReason,
                mandatory = a.mandatory == type.mandatory ? 0 : (a.mandatory ? 1 : 2),
            };

            plan.EnsureSlot(a.slot).events.Add(e);
        }

        return plan;
    }

    // ------------------------------------------------------------------ menu

    [MenuItem("Draftmaster/Weekend/New Plan From Generated Schedule…", priority = 20)]
    static void ExportSelected()
    {
        string track = TrackSelection.CurrentId;
        if (string.IsNullOrEmpty(track)) track = "WatkinsGlen";

        foreach (var series in SeriesCatalog.All)
        {
            string path = WeekendPlanLibrary.AssetPath(track, series);
            if (File.Exists(path))
            {
                Debug.Log($"Weekend Plan: {path} already exists, left alone.");
                continue;
            }

            var plan = FromTimetable(track, series);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(plan, prettyPrint: true));
            Debug.Log($"Weekend Plan: wrote {path} ({plan.EventCount} bookings).");
        }

        AssetDatabase.Refresh();
        WeekendPlanLibrary.ClearCache();
    }

    [MenuItem("Draftmaster/Weekend/List Event Ids", priority = 21)]
    static void ListIds()
    {
        var text = new System.Text.StringBuilder("Weekend event ids a plan file can use:\n");
        foreach (var e in WeekendEventCatalog.All)
            text.Append("  ").Append(e.id.PadRight(30))
                .Append(e.minutes.ToString().PadLeft(4)).Append("m  ")
                .Append(e.title).Append(e.needsSeries ? "   (needs \"series\")" : "").Append('\n');
        Debug.Log(text.ToString());
    }

    // Every shipped plan checked in one go, so a broken file is found at author time rather than in play.
    [MenuItem("Draftmaster/Weekend/Validate All Plans", priority = 22)]
    static void ValidateAll()
    {
        var plans = Resources.LoadAll<TextAsset>(WeekendPlanLibrary.ResourceFolder);
        if (plans.Length == 0)
        {
            Debug.Log("Weekend Plan: no plan files yet — every round builds from the generated schedule.");
            return;
        }

        int bad = 0;
        foreach (var asset in plans)
        {
            WeekendPlan plan = null;
            try { plan = JsonUtility.FromJson<WeekendPlan>(asset.text); }
            catch (System.Exception e) { Debug.LogError($"{asset.name}.json: unreadable — {e.Message}"); bad++; continue; }

            if (plan == null) { Debug.LogError($"{asset.name}.json: parsed to nothing."); bad++; continue; }

            var problems = plan.Problems();
            if (problems.Count == 0)
            {
                Debug.Log($"{asset.name}.json: OK — {plan.EventCount} booking(s).");
                continue;
            }

            bad++;
            Debug.LogError($"{asset.name}.json has {problems.Count} problem(s):\n  " + string.Join("\n  ", problems));
        }

        Debug.Log($"Weekend Plan: checked {plans.Length} file(s), {bad} with problems.");
    }
}
