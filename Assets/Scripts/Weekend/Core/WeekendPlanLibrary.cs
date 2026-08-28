using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Weekend
{
    // Where authored weekends are kept, and how one gets from disk into a WeekendTimetable.
    //
    // Files live at `Assets/Resources/Weekends/<Track>.<Series>.json`, so they load with Resources.Load and
    // ship in the build without an addressables setup. A track+series with a file uses it; one without falls
    // back to the procedural builder, so authoring is per round and never all-or-nothing.
    //
    // A per-track file with no series suffix (`WatkinsGlen.json`) is the shared fallback for all three
    // championships at that circuit — useful because most of a weekend's media/sponsor/fan obligations are
    // the same whoever you are driving for, and only the sessions differ.
    public static class WeekendPlanLibrary
    {
        public const string ResourceFolder = "Weekends";

        // Cached because the weekend rebuilds its timetable on every scene load — three times a round — and
        // re-reading and re-parsing the same JSON each time is pure waste. Cleared by the editor tooling
        // after a save so authoring shows up without a domain reload.
        static readonly Dictionary<string, WeekendPlan> _cache = new();
        static readonly HashSet<string> _missing = new();

        public static string FileName(string track, RacingSeries series) => track + "." + series;
        public static string FileName(string track) => track;

        public static string AssetPath(string track, RacingSeries series) =>
            "Assets/Resources/" + ResourceFolder + "/" + FileName(track, series) + ".json";

        // The plan for this track and series, or null when there isn't one and the weekend should build
        // itself the old way. Tries the series-specific file first, then the track-wide one.
        public static WeekendPlan For(string track, RacingSeries series)
        {
            if (string.IsNullOrWhiteSpace(track)) return null;
            return Load(FileName(track, series)) ?? Load(FileName(track));
        }

        public static bool Exists(string track, RacingSeries series) => For(track, series) != null;

        static WeekendPlan Load(string name)
        {
            if (_cache.TryGetValue(name, out var cached)) return cached;
            if (_missing.Contains(name)) return null;

            var text = Resources.Load<TextAsset>(ResourceFolder + "/" + name);
            if (text == null)
            {
                _missing.Add(name);
                return null;
            }

            WeekendPlan plan = null;
            try
            {
                plan = JsonUtility.FromJson<WeekendPlan>(text.text);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"WeekendPlanLibrary: '{name}.json' is not valid JSON and the weekend will " +
                               $"fall back to the generated schedule. {e.Message}");
            }

            if (plan == null)
            {
                _missing.Add(name);
                return null;
            }

            // A plan that parses but is wrong is worse than no plan, because it plays as a broken weekend
            // rather than an obvious one. Report every problem and use it anyway — the bookings that ARE
            // valid still build, and the console says exactly what to fix.
            var problems = plan.Problems();
            if (problems.Count > 0)
            {
                Debug.LogWarning($"WeekendPlanLibrary: '{name}.json' has {problems.Count} problem(s):\n  " +
                                 string.Join("\n  ", problems));
            }

            _cache[name] = plan;
            return plan;
        }

        // Drop everything read so far. The editor calls this after writing a plan file so the next Play does
        // not run the version that was on disk when the editor started.
        public static void ClearCache()
        {
            _cache.Clear();
            _missing.Clear();
        }

        // ------------------------------------------------------------------ plan -> timetable

        // Turn an authored plan into the bookings the weekend actually runs.
        //
        // Every field falls back to the catalogue, so the plan file only ever states what is unusual about a
        // booking. Anything that does not resolve is skipped with a warning rather than throwing: a single
        // fat-fingered event id should cost that one booking, not the whole weekend.
        public static void Apply(WeekendPlan plan, WeekendTimetable timetable, RacingSeries playerSeries)
        {
            if (plan == null || timetable == null) return;

            foreach (var planSlot in plan.slots)
            {
                if (planSlot?.events == null) continue;
                if (!WeekendPlan.TryParseSlot(planSlot.slot, out var slot)) continue;

                foreach (var e in planSlot.events)
                {
                    if (e == null) continue;
                    if (!WeekendEventCatalog.TryGet(e.@event, out var type)) continue;

                    int start = WeekendPlan.ParseClock(e.start);
                    if (start < 0) continue;

                    int minutes = e.minutes > 0 ? e.minutes : type.minutes;

                    // Whose session: stated for a watch-* booking, the player's for everything else.
                    var series = playerSeries;
                    if (type.needsSeries && WeekendPlan.TryParseSeries(e.series, out var stated)) series = stated;

                    var activity = timetable.AddAuthored(slot, start, minutes, type.kind, series);

                    activity.title = Pick(e.title, TitleFor(type, series, playerSeries));
                    activity.subtitle = Pick(e.subtitle, type.subtitle);
                    activity.markerLocation = e.markerLocation ?? "";

                    activity.appearanceFee = e.fee >= 0 ? e.fee : type.appearanceFee;
                    activity.skipMoneyPenalty = e.skipMoney >= 0 ? e.skipMoney : type.skipMoney;
                    activity.skipAppealPenalty = e.skipAppeal >= 0f ? e.skipAppeal : type.skipAppeal;
                    activity.skipReason = Pick(e.skipReason, type.skipReason);

                    activity.mandatory = e.mandatory == 0 ? type.mandatory : e.mandatory == 1;
                }
            }

            // `requires` is written in the plan as an event id ("team-debrief" needs "session-practice"), but
            // the ledger gates on the activity id of a specific booking. Resolved after everything is placed
            // so a dependency can point either forwards or backwards in the file.
            ResolveRequirements(plan, timetable);
        }

        static void ResolveRequirements(WeekendPlan plan, WeekendTimetable timetable)
        {
            foreach (var planSlot in plan.slots)
            {
                if (planSlot?.events == null) continue;
                if (!WeekendPlan.TryParseSlot(planSlot.slot, out var slot)) continue;

                foreach (var e in planSlot.events)
                {
                    if (e == null || string.IsNullOrWhiteSpace(e.requires)) continue;
                    if (!WeekendEventCatalog.TryGet(e.@event, out var type)) continue;
                    if (!WeekendEventCatalog.TryGet(e.requires, out var needed)) continue;

                    int start = WeekendPlan.ParseClock(e.start);
                    if (start < 0) continue;

                    var activity = timetable.Find(slot, start, type.kind);
                    var prerequisite = timetable.FirstOfKind(needed.kind);
                    if (activity != null && prerequisite != null) activity.requiresId = prerequisite.id;
                }
            }
        }

        // A watch-* booking is titled by whose session it is, so "QUALIFYING" reads as "TRK QUALIFYING" on
        // the sheet and the player can tell three championships apart at a glance. The player's own sessions
        // get their code too, for the same reason.
        static string TitleFor(WeekendEventCatalog.EventType type, RacingSeries series, RacingSeries playerSeries)
        {
            bool isSession = ActivityKinds.IsOnTrack(type.kind) || ActivityKinds.IsSpectate(type.kind);
            if (!isSession) return type.title;
            return SeriesCatalog.ShortCode(series) + " " + type.title;
        }

        static string Pick(string over, string fallback) => string.IsNullOrWhiteSpace(over) ? fallback : over;
    }
}
