using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Weekend
{
    // The other two championships at the venue still have to happen whether the player watches or not.
    // Rather than spawn thirty-six more cars on the same track, their sessions are simulated: a seeded
    // qualifying order, a race result, and a timeline of the handful of moments a broadcast would actually
    // cut to. The spectate screen plays that timeline forward on a compressed clock, so watching the
    // National race is watching a real running order change rather than a static table fading in.
    //
    // Deterministic from (weekendId, series, session), so the same race gives the same result no matter how
    // many times the schedule screen is reopened, and the result the player watched is the result that goes
    // in the standings.
    public static class SeriesSimulator
    {
        public class Entry
        {
            public string driverName;
            public int carNumber;
            public int gridPosition;     // 1-based; qualifying order
            public int finishPosition;   // 1-based; race classification
            public float lapTime;        // qualifying lap, seconds
            public float gapToLeader;    // race gap, seconds; -1 when laps down
            public int lapsDown;
            public bool retired;
            public string retirement;    // "Engine", "Accident", "" when running
        }

        public class Moment
        {
            public float at01;           // where in the session it happens, 0..1
            public string text;          // one broadcast line
            public bool caution;
        }

        public class Session
        {
            public RacingSeries series;
            public ActivityKind kind;        // SpectatePractice / SpectateQualifying / SpectateRace
            public string trackName = "";
            public int laps;
            public List<Entry> entries = new();
            public List<Moment> moments = new();
            public int cautions;
            public int leadChanges;

            // The running order this many percent of the way through, written into `into` (1-based order).
            // Grid at 0, classification at 1, with a deterministic wobble in between so positions actually
            // trade places rather than sliding smoothly from one list to the other.
            public void OrderAt(float t01, List<Entry> into)
            {
                into.Clear();
                if (entries.Count == 0) return;
                float t = Mathf.Clamp01(t01);

                // Sort by a blended running "progress" value - lower is further ahead.
                var scored = new List<(float score, Entry e)>(entries.Count);
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    float blended = Mathf.Lerp(e.gridPosition, e.finishPosition, SmoothIn(t));
                    // Mid-race churn: a sine keyed off the car number, biggest in the middle of the race.
                    float churn = Mathf.Sin((t * 9f) + e.carNumber * 1.7f) * 2.4f * Mathf.Sin(t * Mathf.PI);
                    // Somebody who retires drops to the back as their moment arrives.
                    if (e.retired && t > 0.55f) blended = entries.Count + e.finishPosition;
                    scored.Add((blended + churn, e));
                }
                scored.Sort((a, b) => a.score.CompareTo(b.score));
                for (int i = 0; i < scored.Count; i++) into.Add(scored[i].e);
            }

            static float SmoothIn(float t) => t * t * (3f - 2f * t);

            public Entry Winner => entries.Count == 0 ? null : ByFinish(1);

            public Entry ByFinish(int position)
            {
                foreach (var e in entries) if (e.finishPosition == position) return e;
                return null;
            }

            public Entry PoleSitter()
            {
                foreach (var e in entries) if (e.gridPosition == 1) return e;
                return null;
            }
        }

        // ------------------------------------------------------------------ rosters
        //
        // A fictional field per championship so the simulator stands on its own in menu scenes where the
        // driver database has never been opened. The runtime layer can hand real drivers in instead.

        static readonly string[] CupField =
        {
            "Wade Corliss", "Ronnie Tate", "Beau Danville", "Casey Ruhl", "Marcus Pell", "Dale Overstreet",
            "Nate Kirkham", "Sonny Vaughn", "Ty Brannigan", "Jesse Hollis", "Ford McKenna", "Ricky Dunmore",
            "Curtis Yarrow", "Vince Ottoway", "Hank Salter", "Chip Devereaux", "Lonnie Frayne", "Gus Whitlock",
            "Trey Mullins", "Brady Corso", "Shane Peltier", "Eli Rasmussen", "Cal Winterburn", "Rex Amundsen",
        };

        static readonly string[] NationalField =
        {
            "Jimmy Karras", "Owen Balfour", "Dusty Rowe", "Nash Terrell", "Colby Fenn", "Ray Bledsoe",
            "Ike Hargrove", "Sam Odell", "Buddy Crane", "Levi Marchetti", "Tucker Vance", "Otis Lamm",
            "Jared Coffey", "Wes Padgett", "Marty Sable", "Kip Ferraro", "Dane Wexler", "Roscoe Lindt",
            "Cody Trammell", "Emory Fitch", "Bryce Nadeau", "Hal Prentiss",
        };

        static readonly string[] TruckField =
        {
            "Junior Kemp", "Abe Stroud", "Chuck Delaney", "Wyatt Boone", "Milo Prentice", "Grady Vollmer",
            "Tate Ashby", "Rusty Fenwick", "Clay Muncie", "Vern Kowalski", "Deke Sharpton", "Ozzie Brill",
            "Hoyt Cassidy", "Angus Reeve", "Pete Larrabee", "Woody Grimes", "Silas Rooker", "Boyd Tinsley",
            "Rowdy Hearn", "Zane Mattox",
        };

        public static string[] RosterFor(RacingSeries s) => s switch
        {
            RacingSeries.Cup => CupField,
            RacingSeries.National => NationalField,
            _ => TruckField,
        };

        // ------------------------------------------------------------------ simulation

        // A qualifying session: everybody sets one lap, ranked. Grid position IS the result here.
        public static Session Qualifying(RacingSeries series, int weekendId, string trackName, float baseLapSeconds = 32f,
                                         IReadOnlyList<string> roster = null)
        {
            var s = NewSession(series, ActivityKind.SpectateQualifying, weekendId, trackName, roster);
            var rng = WeekendRandom.For(weekendId, (int)series, 1);

            // One lap each, spread over about a second and a half of a field.
            foreach (var e in s.entries)
            {
                float talent = Mathf.InverseLerp(0f, s.entries.Count, e.gridPosition);   // seeded by roster order
                e.lapTime = baseLapSeconds + talent * 1.4f + rng.Range(-0.35f, 0.55f);
            }
            s.entries.Sort((a, b) => a.lapTime.CompareTo(b.lapTime));
            for (int i = 0; i < s.entries.Count; i++)
            {
                s.entries[i].gridPosition = i + 1;
                s.entries[i].finishPosition = i + 1;   // the "result" of qualifying is the grid
            }

            var pole = s.PoleSitter();
            s.moments.Add(new Moment { at01 = 0.15f, text = "Track goes green - the first runs are out." });
            s.moments.Add(new Moment { at01 = 0.55f, text = "Provisional pole changes hands twice inside a minute." });
            if (pole != null)
                s.moments.Add(new Moment { at01 = 0.95f, text = $"POLE: #{pole.carNumber} {pole.driverName}, {Fmt(pole.lapTime)}." });
            return s;
        }

        // A practice session: no result that matters, just a fastest-lap board and a couple of spins.
        public static Session Practice(RacingSeries series, int weekendId, string trackName, float baseLapSeconds = 32f,
                                       IReadOnlyList<string> roster = null)
        {
            var s = Qualifying(series, weekendId + 9001, trackName, baseLapSeconds + 0.6f, roster);
            s.kind = ActivityKind.SpectatePractice;
            s.moments.Clear();
            s.moments.Add(new Moment { at01 = 0.2f, text = "Long runs first - nobody is showing anything yet." });
            s.moments.Add(new Moment { at01 = 0.6f, text = "A car gets loose off turn two and saves it." });
            s.moments.Add(new Moment { at01 = 0.9f, text = "Mock qualifying runs at the end of the session." });
            return s;
        }

        // A race: grid from a seeded qualifying, a classification that moves people around, some cautions and
        // a couple of retirements, plus the broadcast timeline.
        public static Session Race(RacingSeries series, int weekendId, string trackName, float baseLapSeconds = 32f,
                                   IReadOnlyList<string> roster = null)
        {
            var quali = Qualifying(series, weekendId, trackName, baseLapSeconds, roster);
            var s = new Session
            {
                series = series,
                kind = ActivityKind.SpectateRace,
                trackName = trackName ?? "",
                laps = SeriesCatalog.RaceLaps(series),
                entries = quali.entries,
            };

            var rng = WeekendRandom.For(weekendId, (int)series, 2);
            int n = s.entries.Count;

            // Retirements: a couple of cars do not see the end.
            int retirements = rng.Range(1, 4);
            var retiredIdx = new List<int>();
            for (int i = 0; i < retirements; i++)
            {
                int idx = rng.Range(0, n);
                if (retiredIdx.Contains(idx)) continue;
                retiredIdx.Add(idx);
                s.entries[idx].retired = true;
                s.entries[idx].retirement = rng.Pick(new[] { "Engine", "Accident", "Overheating", "Suspension", "Crash damage" });
            }

            // Race outcome: qualifying order with real movement on top, retirements shuffled to the back.
            var scored = new List<(float score, Entry e)>(n);
            foreach (var e in s.entries)
            {
                float move = rng.Range(-6f, 6f) + rng.Range(-3f, 3f);   // two rolls: fat middle, long tails
                float score = e.gridPosition + move;
                if (e.retired) score += 1000f;
                scored.Add((score, e));
            }
            scored.Sort((a, b) => a.score.CompareTo(b.score));

            float gap = 0f;
            for (int i = 0; i < scored.Count; i++)
            {
                var e = scored[i].e;
                e.finishPosition = i + 1;
                if (e.retired)
                {
                    e.lapsDown = rng.Range(3, 40);
                    e.gapToLeader = -1f;
                }
                else if (i == 0)
                {
                    e.gapToLeader = 0f;
                }
                else
                {
                    gap += rng.Range(0.15f, 2.6f);
                    if (rng.Chance(0.10f)) { e.lapsDown = 1; e.gapToLeader = -1f; }
                    else e.gapToLeader = gap;
                }
            }

            s.cautions = rng.Range(2, 9);
            s.leadChanges = rng.Range(3, 26);
            BuildRaceMoments(s, ref rng);
            return s;
        }

        static void BuildRaceMoments(Session s, ref WeekendRandom rng)
        {
            var winner = s.ByFinish(1);
            var pole = s.PoleSitter();
            var second = s.ByFinish(2);

            s.moments.Add(new Moment { at01 = 0.02f, text = pole != null ? $"Green flag. #{pole.carNumber} {pole.driverName} leads them into turn one." : "Green flag." });

            // Cautions spread across the middle of the race, each with a reason.
            for (int i = 0; i < s.cautions; i++)
            {
                float at = Mathf.Lerp(0.12f, 0.92f, (i + 0.5f) / s.cautions) + rng.Range(-0.03f, 0.03f);
                var victim = s.entries[rng.Range(0, s.entries.Count)];
                string reason = rng.Pick(new[]
                {
                    $"#{victim.carNumber} {victim.driverName} into the wall off turn four.",
                    $"Debris on the backstretch - caution number {i + 1}.",
                    $"#{victim.carNumber} spins on his own and collects nobody.",
                    "Multi-car stack-up in turn three brings out the yellow.",
                    $"#{victim.carNumber} slows with a flat right rear.",
                });
                s.moments.Add(new Moment { at01 = Mathf.Clamp01(at), text = reason, caution = true });
            }

            // Retirements get their own line when they happen.
            foreach (var e in s.entries)
            {
                if (!e.retired) continue;
                s.moments.Add(new Moment
                {
                    at01 = rng.Range(0.25f, 0.85f),
                    text = $"#{e.carNumber} {e.driverName} is out - {e.retirement.ToLowerInvariant()}.",
                });
            }

            if (winner != null && second != null)
            {
                float margin = second.gapToLeader;
                s.moments.Add(new Moment
                {
                    at01 = 0.93f,
                    text = margin >= 0f && margin < 0.6f
                        ? $"Side by side to the white flag - #{winner.carNumber} and #{second.carNumber}."
                        : $"#{winner.carNumber} {winner.driverName} is clear with a handful to go.",
                });
                s.moments.Add(new Moment
                {
                    at01 = 1f,
                    text = $"CHECKERED: #{winner.carNumber} {winner.driverName} wins the {SeriesCatalog.Nickname(s.series)} race.",
                });
            }

            s.moments.Sort((a, b) => a.at01.CompareTo(b.at01));
        }

        // ------------------------------------------------------------------ plumbing

        static Session NewSession(RacingSeries series, ActivityKind kind, int weekendId, string trackName,
                                  IReadOnlyList<string> roster)
        {
            var names = roster != null && roster.Count >= 8 ? roster : RosterFor(series);
            int field = Mathf.Min(SeriesCatalog.FieldSize(series), names.Count);

            var s = new Session
            {
                series = series,
                kind = kind,
                trackName = trackName ?? "",
                laps = SeriesCatalog.RaceLaps(series),
            };

            var rng = WeekendRandom.For(weekendId, (int)series, 3);
            var numbers = new List<int>(field);
            for (int i = 0; i < field; i++) numbers.Add(NumberFor(series, i));
            rng.Shuffle(numbers);

            for (int i = 0; i < field; i++)
            {
                s.entries.Add(new Entry
                {
                    driverName = names[i],
                    carNumber = numbers[i],
                    gridPosition = i + 1,
                    finishPosition = i + 1,
                    gapToLeader = 0f,
                    retirement = "",
                });
            }
            return s;
        }

        // Plausible car numbers that cannot collide across the three championships sharing the weekend: each
        // one takes its own residue mod 3, so no number is ever entered in two of them on the same day. That
        // matters because the timing towers sit next to each other in the paddock and the player reads the
        // field by car number, not by name.
        static int NumberFor(RacingSeries series, int index) => series switch
        {
            RacingSeries.Cup => 1 + index * 3,
            RacingSeries.National => 2 + index * 3,
            _ => 3 + index * 3,
        };

        public static string Fmt(float seconds)
        {
            if (seconds < 0f) return "--.---";
            int m = (int)(seconds / 60f);
            float rem = seconds - m * 60f;
            return m > 0 ? $"{m}:{rem:00.000}" : rem.ToString("0.000");
        }

        // "+1.482" / "+2L" / "DNF"
        public static string GapText(Entry e)
        {
            if (e == null) return "";
            if (e.retired) return "DNF";
            if (e.lapsDown > 0) return "+" + e.lapsDown + "L";
            if (e.finishPosition == 1) return "WINNER";
            return "+" + e.gapToLeader.ToString("0.000");
        }
    }
}
