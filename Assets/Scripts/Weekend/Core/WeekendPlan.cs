using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Weekend
{
    // A race weekend written down instead of generated: one JSON file per track per series, six half-days,
    // and whatever the author put in each of them.
    //
    //     Assets/Resources/Weekends/WatkinsGlen.Cup.json
    //
    // The file starts as six empty slots and bookings get added to them by hand. A track+series with a plan
    // file uses ONLY that file — the procedural builder in WeekendTimetable does not run for it, so what is
    // in the file is what the weekend is. A track without one still builds procedurally, which is what keeps
    // the other thirty-four rounds playable while one is being authored.
    //
    // The shape on disk, in full:
    //
    //     {
    //       "track": "WatkinsGlen",
    //       "series": "Cup",
    //       "notes": "free text for whoever opens this next",
    //       "slots": [
    //         { "slot": "FridayAM", "events": [
    //             { "event": "sponsor_event-photoshoot", "start": "09:45" },
    //             { "event": "watch-qualifying", "start": "10:00", "series": "Trucks" }
    //         ]},
    //         { "slot": "FridayPM", "events": [] },
    //         ...
    //       ]
    //     }
    //
    // `event` and `start` are the only required fields — everything else falls back to WeekendEventCatalog,
    // so a booking is one line unless it is doing something unusual. JsonUtility is the parser (same as the
    // ledger and the sponsor book), which is why every field is a plain public field on a [Serializable]
    // class and why times are strings rather than a struct.
    //
    // Pure: no scene, no Resources, no PlayerPrefs. Loading is WeekendPlanLibrary's job; this is the shape
    // and the rules, so both the game and the editor tooling read the same definition of a valid weekend.

    [System.Serializable]
    public class WeekendPlanEvent
    {
        [Tooltip("Catalogue id, e.g. 'sponsor_event-photoshoot'. See WeekendEventCatalog.Ids().")]
        public string @event = "";

        [Tooltip("Clock time it starts, 24h, 'HH:MM'.")]
        public string start = "";

        [Tooltip("Minutes it blocks the calendar for. 0 = the catalogue's default length.")]
        public int minutes;

        [Tooltip("Whose session, for watch-* events: Trucks / National / Cup. Empty = the player's series.")]
        public string series = "";

        // ---- overrides. Empty/zero means "whatever the catalogue says". ------------------------

        public string title = "";
        public string subtitle = "";

        [Tooltip("Name of the marker GameObject in the track package this booking sends the player to, " +
                 "e.g. \"PitBox_Marker\". Empty = whichever marker the venue for this kind normally uses.")]
        public string markerLocation = "";

        [Tooltip("Catalogue id of a booking that has to have happened first — a debrief needs its practice. " +
                 "Matched against events in this same plan.")]
        public string requires = "";

        // -1 rather than 0 so a plan can deliberately zero out a fee the catalogue charges.
        public int fee = -1;
        public int skipMoney = -1;
        public float skipAppeal = -1f;
        public string skipReason = "";

        [Tooltip("0 = the catalogue's default, 1 = force mandatory, 2 = force optional.")]
        public int mandatory;

        public override string ToString() => start + " " + @event;
    }

    [System.Serializable]
    public class WeekendPlanSlot
    {
        [Tooltip("FridayAM / FridayPM / SaturdayAM / SaturdayPM / SundayAM / SundayPM.")]
        public string slot = "";

        public List<WeekendPlanEvent> events = new();
    }

    [System.Serializable]
    public class WeekendPlan
    {
        public string track = "";
        public string series = "";
        public string notes = "";
        public List<WeekendPlanSlot> slots = new();

        // ------------------------------------------------------------------ authoring

        // The blank sheet: the six half-days in order, all empty, ready to have bookings added to them.
        // This is what "Draftmaster > Weekend > New Plan For Selected Track" writes out.
        public static WeekendPlan Empty(string track, RacingSeries series)
        {
            var plan = new WeekendPlan { track = track ?? "", series = series.ToString() };
            foreach (var slot in WeekendSlots.All)
                plan.slots.Add(new WeekendPlanSlot { slot = slot.ToString(), events = new List<WeekendPlanEvent>() });
            return plan;
        }

        public WeekendPlanSlot Slot(WeekendSlot slot)
        {
            string want = slot.ToString();
            foreach (var s in slots)
                if (s != null && string.Equals(s.slot, want, System.StringComparison.OrdinalIgnoreCase)) return s;
            return null;
        }

        // Adding a booking to a half-day that is not in the file yet creates it, so a hand-trimmed file that
        // only lists the days it uses still works.
        public WeekendPlanSlot EnsureSlot(WeekendSlot slot)
        {
            var found = Slot(slot);
            if (found != null) return found;

            var made = new WeekendPlanSlot { slot = slot.ToString(), events = new List<WeekendPlanEvent>() };
            slots.Add(made);
            return made;
        }

        public int EventCount
        {
            get
            {
                int n = 0;
                foreach (var s in slots) if (s?.events != null) n += s.events.Count;
                return n;
            }
        }

        // ------------------------------------------------------------------ parsing

        // "09:45" / "9:45" / "0945" -> minutes from midnight. -1 when it is not a time at all, which the
        // validator turns into a named problem rather than a booking that silently lands at midnight.
        public static int ParseClock(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return -1;
            text = text.Trim();

            int colon = text.IndexOf(':');
            string hh, mm;
            if (colon >= 0)
            {
                hh = text.Substring(0, colon);
                mm = text.Substring(colon + 1);
            }
            else if (text.Length == 4)
            {
                hh = text.Substring(0, 2);
                mm = text.Substring(2, 2);
            }
            else return -1;

            if (!int.TryParse(hh, out int h) || !int.TryParse(mm, out int m)) return -1;
            if (h < 0 || h > 23 || m < 0 || m > 59) return -1;
            return h * 60 + m;
        }

        public static bool TryParseSlot(string text, out WeekendSlot slot)
        {
            slot = WeekendSlot.FridayAM;
            if (string.IsNullOrWhiteSpace(text)) return false;
            return System.Enum.TryParse(text.Trim(), ignoreCase: true, out slot);
        }

        public static bool TryParseSeries(string text, out RacingSeries series)
        {
            series = RacingSeries.Cup;
            if (string.IsNullOrWhiteSpace(text)) return false;
            return System.Enum.TryParse(text.Trim(), ignoreCase: true, out series);
        }

        // ------------------------------------------------------------------ validation

        // Everything wrong with this plan, in the order a person would want to fix it. Empty list = the
        // weekend will build exactly as written.
        //
        // This is deliberately more than JSON well-formedness: a plan that parses but sends the player to a
        // booking at 03:00, or names an event id that does not exist, is a weekend that is broken in play
        // and fine on disk. The editor runs this on save and the tests run it over every shipped plan.
        public List<string> Problems()
        {
            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(track)) problems.Add("No 'track' — the plan does not say which circuit it is for.");
            if (!TryParseSeries(series, out _))
                problems.Add($"'series' is '{series}' — expected Trucks, National or Cup.");

            var seen = new HashSet<string>();

            foreach (var planSlot in slots)
            {
                if (planSlot == null) continue;
                if (!TryParseSlot(planSlot.slot, out var slot))
                {
                    problems.Add($"'{planSlot.slot}' is not a half-day. Expected one of FridayAM, FridayPM, " +
                                 "SaturdayAM, SaturdayPM, SundayAM, SundayPM.");
                    continue;
                }

                if (!seen.Add(planSlot.slot.ToLowerInvariant()))
                    problems.Add($"{WeekendSlots.ShortLabel(slot)} is listed twice — the second one's bookings would be ignored.");

                if (planSlot.events == null) continue;

                int opens = WeekendSlots.OpensAt(slot);
                int closes = WeekendSlots.ClosesAt(slot);

                foreach (var e in planSlot.events)
                {
                    if (e == null) continue;
                    string where = WeekendSlots.ShortLabel(slot) + " '" + (e.@event ?? "") + "'";

                    if (!WeekendEventCatalog.TryGet(e.@event, out var type))
                    {
                        problems.Add($"{where}: no such event id. Run Draftmaster > Weekend > List Event Ids.");
                        continue;
                    }

                    int start = ParseClock(e.start);
                    if (start < 0)
                    {
                        problems.Add($"{where}: 'start' is '{e.start}' — expected a 24h clock time like \"09:45\".");
                        continue;
                    }

                    int minutes = e.minutes > 0 ? e.minutes : type.minutes;

                    if (start < opens)
                        problems.Add($"{where}: starts at {WeekendSlots.Clock(start)}, before the half-day opens " +
                                     $"at {WeekendSlots.Clock(opens)} — the player can never reach it.");

                    if (start + minutes > closes)
                        problems.Add($"{where}: runs to {WeekendSlots.Clock(start + minutes)}, past the " +
                                     $"{WeekendSlots.Clock(closes)} close of {WeekendSlots.ShortLabel(slot)}.");

                    if (type.needsSeries && !string.IsNullOrWhiteSpace(e.series) && !TryParseSeries(e.series, out _))
                        problems.Add($"{where}: 'series' is '{e.series}' — expected Trucks, National or Cup.");

                    if (!string.IsNullOrWhiteSpace(e.requires) && !WeekendEventCatalog.Exists(e.requires))
                        problems.Add($"{where}: 'requires' names '{e.requires}', which is not an event id.");
                }
            }

            return problems;
        }

        public bool IsValid => Problems().Count == 0;
    }
}
