using System;
using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Weekend
{
    // What the player has actually done with their three days, and what it earned them.
    //
    // The weekend is played across scene reloads - the schedule screen sends the player into the race scene
    // for their own sessions and the race scene reloads again between practice, qualifying and the race - so
    // none of this can live in a static field. It is JSON in PlayerPrefs, the same way SponsorBook and the
    // rest of the live career persist.
    //
    // Time is a cursor, not a queue: doing something at 14:00 that runs 45 minutes moves the clock to 14:45,
    // and anything in the same half-day that started before 14:45 and was never attended is marked MISSED.
    // That is what makes the sponsor suite meet-and-greet and the National race being at the same hour an
    // actual decision instead of a list.
    public static class WeekendLedger
    {
        const string Key = "weekend.ledger";

        [Serializable]
        class Book
        {
            public int weekendId = -1;
            public int series;
            public int slotIndex;              // half-day the player is living in
            public int clockMinute;            // cursor inside that day, minutes from midnight
            public List<string> done = new();
            public List<string> missed = new();

            public int earnings;               // appearance fees and bonuses banked this weekend
            public int fines;                  // penalties taken this weekend (positive number)

            public float sponsorMood;          // -100..100
            public float teamMorale;           // -100..100
            public float mediaStanding;        // -100..100
            public float setupGain;            // 0..1

            public List<string> headlines = new();
        }

        static Book _cache;

        static Book Data
        {
            get
            {
                if (_cache != null) return _cache;
                string json = PlayerPrefs.GetString(Key, "");
                if (!string.IsNullOrEmpty(json))
                {
                    try { _cache = JsonUtility.FromJson<Book>(json); } catch { _cache = null; }
                }
                _cache ??= new Book();
                _cache.done ??= new List<string>();
                _cache.missed ??= new List<string>();
                _cache.headlines ??= new List<string>();
                return _cache;
            }
        }

        static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // Fired whenever anything here moves, so an open schedule screen can redraw.
        public static event Action Changed;

        // ------------------------------------------------------------------ settle hooks
        //
        // Money and career stats live in Assembly-CSharp (PlayerWallet, PlayerStatsLedger), which this
        // assembly cannot reference. The runtime layer installs these once at boot; until it does, the
        // ledger still records everything, it just does not push it anywhere.

        public static Action<int> MoneyHook;                 // +earn / -fine
        public static Action<string, int> StatHook;          // career counter
        public static Action<string, float> RelationshipHook; // driver name, delta

        // ------------------------------------------------------------------ weekend lifecycle

        // Point the ledger at a weekend. A different id than the one on file wipes the sheet: a new weekend
        // starts on Friday morning at 08:00 with nothing done and the meters back at neutral.
        public static void EnsureWeekend(int weekendId, RacingSeries series)
        {
            var d = Data;
            if (d.weekendId == weekendId && d.series == (int)series) return;

            d.weekendId = weekendId;
            d.series = (int)series;
            d.slotIndex = 0;
            d.clockMinute = WeekendSlots.OpensAt(WeekendSlot.FridayAM);
            d.done.Clear();
            d.missed.Clear();
            d.headlines.Clear();
            d.earnings = 0;
            d.fines = 0;
            d.sponsorMood = 0f;
            d.teamMorale = 0f;
            d.mediaStanding = 0f;
            d.setupGain = 0f;
            Save();
        }

        public static int WeekendId => Data.weekendId;
        public static RacingSeries Series => (RacingSeries)Mathf.Clamp(Data.series, 0, 2);

        // ------------------------------------------------------------------ the clock

        public static WeekendSlot CurrentSlot => (WeekendSlot)Mathf.Clamp(Data.slotIndex, 0, WeekendSlots.Count - 1);
        public static int ClockMinute => Data.clockMinute;
        public static bool WeekendOver => Data.slotIndex >= WeekendSlots.Count;

        public static string ClockText => WeekendSlots.Day(CurrentSlot) + " " + WeekendSlots.Clock(ClockMinute);

        // How much of the current half-day is left, in minutes.
        public static int MinutesLeftInSlot =>
            Mathf.Max(0, WeekendSlots.ClosesAt(CurrentSlot) - Mathf.Max(Data.clockMinute, WeekendSlots.OpensAt(CurrentSlot)));

        // ------------------------------------------------------------------ activity state

        public enum State
        {
            Available,   // in the current half-day, still ahead on the clock
            Done,
            Missed,      // the clock went past it unattended
            Later,       // a future half-day
            Past,        // a half-day that has already been left behind
        }

        public static bool IsDone(string id) => id != null && Data.done.Contains(id);
        public static bool IsMissed(string id) => id != null && Data.missed.Contains(id);

        public static State Status(WeekendActivity a)
        {
            if (a == null) return State.Past;
            if (IsDone(a.id)) return State.Done;
            if (IsMissed(a.id)) return State.Missed;
            if ((int)a.slot > Data.slotIndex) return State.Later;
            if ((int)a.slot < Data.slotIndex) return State.Past;
            return a.startMinute >= Data.clockMinute ? State.Available : State.Missed;
        }

        // Can the player still walk into this? Reason is a short line for the schedule screen when not.
        public static bool CanDo(WeekendActivity a, out string reason)
        {
            reason = "";
            if (a == null) { reason = "Nothing booked."; return false; }

            switch (Status(a))
            {
                case State.Done: reason = "Already done."; return false;
                case State.Missed: reason = "You missed it."; return false;
                case State.Later: reason = "Not until " + WeekendSlots.Label(a.slot) + "."; return false;
                case State.Past: reason = "That day is behind you."; return false;
            }

            if (a.EndMinute > WeekendSlots.ClosesAt(a.slot))
            {
                reason = "Runs past the end of the day.";
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ doing things

        // Mark an activity attended, move the clock to its end, sweep up anything the clock just walked past,
        // and settle its outcome. Returns false if it was not doable in the first place.
        public static bool Complete(WeekendActivity a, WeekendOutcome outcome)
        {
            if (!CanDo(a, out _)) return false;

            var d = Data;
            d.done.Add(a.id);
            d.clockMinute = Mathf.Max(d.clockMinute, a.EndMinute);

            Apply(outcome, save: false);
            SweepMissed(save: false);
            Save();
            return true;
        }

        // Give up the rest of the current half-day and move to the next one. Everything left unattended in it
        // is marked missed and its penalties land.
        public static void AdvanceSlot()
        {
            var d = Data;
            d.clockMinute = WeekendSlots.ClosesAt(CurrentSlot);
            SweepMissed(save: false);

            d.slotIndex = Mathf.Min(d.slotIndex + 1, WeekendSlots.Count);
            if (d.slotIndex < WeekendSlots.Count)
                d.clockMinute = WeekendSlots.OpensAt((WeekendSlot)d.slotIndex);
            Save();
        }

        // Skip dead time: shove the clock forward to just before the next thing worth doing, without leaving
        // the half-day. Anything it steps over is missed like any other no-show.
        public static void SkipTo(int minuteOfDay)
        {
            var d = Data;
            d.clockMinute = Mathf.Clamp(Mathf.Max(d.clockMinute, minuteOfDay),
                                        WeekendSlots.OpensAt(CurrentSlot), WeekendSlots.ClosesAt(CurrentSlot));
            SweepMissed(save: false);
            Save();
        }

        // The timetable the sweep works against. The ledger has no opinion about what is on the sheet, so the
        // runtime layer keeps this pointed at the live one.
        public static WeekendTimetable Timetable;

        // Anything in a half-day at or before the cursor that was never attended becomes a no-show, and a
        // no-show on something mandatory costs what the timetable says it costs.
        static void SweepMissed(bool save = true)
        {
            var t = Timetable;
            if (t == null) return;
            var d = Data;

            foreach (var a in t.Activities)
            {
                if (IsDone(a.id) || IsMissed(a.id)) continue;
                bool gone = (int)a.slot < d.slotIndex ||
                            ((int)a.slot == d.slotIndex && a.startMinute < d.clockMinute);
                if (!gone) continue;

                d.missed.Add(a.id);
                if (!a.mandatory) continue;

                if (a.skipMoneyPenalty > 0)
                {
                    d.fines += a.skipMoneyPenalty;
                    MoneyHook?.Invoke(-a.skipMoneyPenalty);
                }
                if (a.skipAppealPenalty > 0f)
                    Draftmaster.Fans.FanAppeal.Add(-a.skipAppealPenalty);

                if (ActivityKinds.IsSponsorDuty(a.kind)) d.sponsorMood = Clamp100(d.sponsorMood - 18f);
                else if (ActivityKinds.IsMedia(a.kind)) d.mediaStanding = Clamp100(d.mediaStanding - 12f);
                else if (ActivityKinds.IsTeam(a.kind)) d.teamMorale = Clamp100(d.teamMorale - 12f);

                if (!string.IsNullOrEmpty(a.skipReason))
                    Note("NO-SHOW: " + a.title + " - " + a.skipReason);
            }

            if (save) Save();
        }

        // Fold an outcome into the persistent meters and push the parts that live elsewhere.
        public static void Apply(WeekendOutcome o, bool save = true)
        {
            var d = Data;

            if (o.money != 0)
            {
                if (o.money > 0) d.earnings += o.money; else d.fines += -o.money;
                MoneyHook?.Invoke(o.money);
            }
            if (Mathf.Abs(o.fanAppeal) > 0.001f) Draftmaster.Fans.FanAppeal.Add(o.fanAppeal);

            d.sponsorMood = Clamp100(d.sponsorMood + o.sponsorMood);
            d.teamMorale = Clamp100(d.teamMorale + o.teamMorale);
            d.mediaStanding = Clamp100(d.mediaStanding + o.mediaStanding);
            d.setupGain = Mathf.Clamp01(d.setupGain + o.setupGain);

            if (!string.IsNullOrEmpty(o.statKey) && o.statCount != 0) StatHook?.Invoke(o.statKey, o.statCount);
            if (!string.IsNullOrEmpty(o.rivalName) && Mathf.Abs(o.rivalDelta) > 0.001f)
                RelationshipHook?.Invoke(o.rivalName, o.rivalDelta);
            if (!string.IsNullOrEmpty(o.headline)) Note(o.headline);

            if (save) Save();
        }

        static float Clamp100(float v) => Mathf.Clamp(v, -100f, 100f);

        // ------------------------------------------------------------------ meters

        public static float SponsorMood => Data.sponsorMood;
        public static float TeamMorale => Data.teamMorale;
        public static float MediaStanding => Data.mediaStanding;
        public static float SetupGain => Data.setupGain;
        public static int Earnings => Data.earnings;
        public static int Fines => Data.fines;
        public static int NetEarnings => Data.earnings - Data.fines;

        // What a meter is worth where it matters.

        // Sponsor payout multiplier at the race: a weekend of kept promises pays up to 30% more, a weekend of
        // no-shows up to 40% less.
        public static float SponsorPayoutMultiplier => 1f + (SponsorMood >= 0f ? SponsorMood * 0.003f : SponsorMood * 0.004f);

        // Pace the crew and the setup work are worth, as a fraction of a second per lap, expressed 0..1 for
        // whoever is applying it. Setup knowledge is the bigger half; morale is the tie-breaker.
        public static float CarPreparation01 => Mathf.Clamp01(SetupGain * 0.75f + Mathf.Max(0f, TeamMorale / 100f) * 0.25f);

        // Pit-stop sharpness, 0..1. A crew that likes their driver is quicker over the wall.
        public static float CrewSharpness01 => Mathf.Clamp01(0.5f + TeamMorale / 200f);

        // ------------------------------------------------------------------ the wrap-up

        public static IReadOnlyList<string> Headlines => Data.headlines;

        public static void Note(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            var d = Data;
            d.headlines.Add(line);
            if (d.headlines.Count > 40) d.headlines.RemoveAt(0);
        }

        public static int DoneCount => Data.done.Count;
        public static int MissedCount => Data.missed.Count;

        // ------------------------------------------------------------------ maintenance

        public static void InvalidateCache() => _cache = null;

        public static void ClearAll()
        {
            _cache = new Book();
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
