using System;
using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Weekend
{
    // One driver's line in a championship table.
    public class ChampionshipRow
    {
        public string driverName = "";
        public int carNumber;
        public bool isPlayer;

        public int position;        // 1 = championship leader
        public int points;
        public int starts;
        public int wins;
        public int poles;
        public int top5s;
        public int top10s;
        public int dnfs;
        public int best = int.MaxValue;   // best finish of the season so far

        public string BestText => best == int.MaxValue ? "-" : "P" + best;
    }

    // The three championships across a season, only one of which the player is driving in.
    //
    // The other two run their full weekend at every venue whether the player watches from a grandstand,
    // spends the hour signing hats, or never leaves the motorhome. Their results are not thrown away when
    // the spectate screen closes any more: every round the player turns up to is entered here, and the
    // table is folded back out of the rounds on file.
    //
    // Almost nothing about a result is stored. It does not need to be — SeriesSimulator is deterministic
    // from (series, round), so a list of round numbers is a complete record of three championships, and the
    // race the player sat and watched in the stand is by construction the one in the standings. What IS
    // written down is the one fact that cannot be recomputed: the player's own finishing position in their
    // own championship, on the rounds they actually drove.
    //
    // PlayerPrefs JSON, the same as WeekendLedger, because the weekend crosses scene loads.
    public static class SeasonChampionships
    {
        const string Key = "season.championships";

        [Serializable]
        class Round
        {
            public int round = -1;            // RaceWeekend.WeekendId — also the simulator's seed
            public string trackId = "";
            public string trackName = "";

            // The player's own race, when they drove it. playerFinish 0 means the round happened without
            // them taking the start — the other two championships raced regardless.
            public int playerSeries = -1;
            public string playerName = "";
            public int playerCarNumber;
            public int playerFinish;
            public int playerGrid;
        }

        [Serializable]
        class Book
        {
            public int season = 1;
            public List<Round> rounds = new();
            public int seenResults;           // how many finished races the player has read on the phone
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
                _cache.rounds ??= new List<Round>();
                return _cache;
            }
        }

        static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
            Bump();
            Changed?.Invoke();
        }

        // Fired whenever a round is entered or a result recorded, so an open standings screen can redraw.
        public static event Action Changed;

        // ------------------------------------------------------------------ the calendar so far

        public static int Season => Data.season;
        public static int RoundCount => Data.rounds.Count;

        // Round numbers in the order they were run. These are weekend ids, so they do not necessarily start
        // at zero — a career picks up wherever the weekend counter already was.
        public static IReadOnlyList<int> Rounds
        {
            get
            {
                var list = new List<int>(Data.rounds.Count);
                foreach (var r in Data.rounds) list.Add(r.round);
                return list;
            }
        }

        // 1-based position of a round within this season, for "ROUND 4" labels. 0 = not on file.
        public static int RoundNumber(int round)
        {
            for (int i = 0; i < Data.rounds.Count; i++) if (Data.rounds[i].round == round) return i + 1;
            return 0;
        }

        public static string TrackNameOf(int round) => Find(round)?.trackName ?? "";
        public static string TrackIdOf(int round) => Find(round)?.trackId ?? "";

        // Did the player drive their own race at this round, and where did they come?
        public static int PlayerFinishAt(int round) => Find(round)?.playerFinish ?? 0;

        static Round Find(int round)
        {
            foreach (var r in Data.rounds) if (r.round == round) return r;
            return null;
        }

        // Put a round on the calendar. Called every time the weekend's timetable is built, so it has to be
        // cheap and idempotent: a round already on file is only touched if the venue has since been
        // resolved (the title screen knows the weekend id before it knows where the weekend is).
        public static void EnterRound(int round, string trackId, string trackName)
        {
            if (round < 0) return;

            var existing = Find(round);
            if (existing != null)
            {
                bool changed = false;
                if (!string.IsNullOrEmpty(trackId) && existing.trackId != trackId) { existing.trackId = trackId; changed = true; }
                if (!string.IsNullOrEmpty(trackName) && existing.trackName != trackName) { existing.trackName = trackName; changed = true; }
                if (changed) Save();
                return;
            }

            Data.rounds.Add(new Round
            {
                round = round,
                trackId = trackId ?? "",
                trackName = trackName ?? "",
            });
            Data.rounds.Sort((a, b) => a.round.CompareTo(b.round));
            Save();
        }

        // The player took the start in their own championship and was classified. This is the only fact in
        // the whole season that cannot be recomputed, so it is the only one written down.
        public static void RecordPlayerRace(int round, RacingSeries series, string driverName, int finishPosition,
                                            int gridPosition = 0, int carNumber = 0)
        {
            if (round < 0 || finishPosition <= 0) return;

            EnterRound(round, "", "");
            var r = Find(round);
            if (r == null) return;

            r.playerSeries = (int)series;
            r.playerName = string.IsNullOrEmpty(driverName) ? "You" : driverName;
            r.playerFinish = finishPosition;
            r.playerGrid = gridPosition;
            r.playerCarNumber = carNumber;
            Save();
        }

        // ------------------------------------------------------------------ results

        static readonly Dictionary<long, SeriesWeekendResult> _results = new();

        // One championship's weekend at one round, simulated on demand and cached. Identical every time it
        // is asked for, so this is safe to call from a redraw loop.
        public static SeriesWeekendResult Result(RacingSeries series, int round)
        {
            long key = (long)round * 4 + (int)series;
            if (_results.TryGetValue(key, out var cached)) return cached;

            var r = Find(round);
            var result = SeriesWeekendResult.Simulate(series, round, r?.trackName ?? "");
            if (r != null && r.playerSeries == (int)series && r.playerFinish > 0)
            {
                result.playerCarNumber = r.playerCarNumber;
                result.WithPlayer(r.playerName, r.playerFinish, r.playerGrid);
            }

            _results[key] = result;
            return result;
        }

        // Has this race been run yet, as far as the player's own weekend clock is concerned?
        //
        // A championship's result is knowable the moment the round is on the calendar — it is a pure
        // function of the round number — but a driver stood in the paddock on Friday morning does not know
        // who wins the Cup race on Sunday. Everything the player can read goes through here, so the
        // standings fill in across the three days in the order the races actually run.
        public static bool HasRun(RacingSeries series, int round)
        {
            int live = WeekendLedger.WeekendId;
            if (round < live) return true;
            if (round > live) return false;
            if (WeekendLedger.WeekendOver) return true;

            var t = WeekendTimetable.RaceTime(series);
            int slot = (int)WeekendLedger.CurrentSlot;
            if (slot != (int)t.slot) return slot > (int)t.slot;
            return WeekendLedger.ClockMinute >= t.startMinute + t.minutes;
        }

        // Rounds of this championship that have been run, oldest first.
        public static List<int> RunRounds(RacingSeries series)
        {
            var list = new List<int>();
            foreach (var r in Data.rounds) if (HasRun(series, r.round)) list.Add(r.round);
            return list;
        }

        // ------------------------------------------------------------------ the table

        static readonly Dictionary<RacingSeries, List<ChampionshipRow>> _tables = new();
        static int _tableVersion = int.MinValue;
        static int _tableGate = int.MinValue;
        static int _version;

        static void Bump()
        {
            _version++;
            _results.Clear();
            _tables.Clear();
            _tableVersion = int.MinValue;
        }

        // How far the live weekend's clock has got through its three races. The other half of the cache
        // key: a table goes stale either because the book changed or because a race just finished.
        static int Gate()
        {
            int bits = 0;
            int live = WeekendLedger.WeekendId;
            foreach (var s in SeriesCatalog.All) if (HasRun(s, live)) bits |= 1 << (int)s;
            return live * 8 + bits;
        }

        public static IReadOnlyList<ChampionshipRow> Standings(RacingSeries series)
        {
            int gate = Gate();
            if (_tableVersion != _version || _tableGate != gate)
            {
                _tables.Clear();
                _tableVersion = _version;
                _tableGate = gate;
            }
            if (_tables.TryGetValue(series, out var cached)) return cached;

            var table = Fold(series);
            _tables[series] = table;
            return table;
        }

        static List<ChampionshipRow> Fold(RacingSeries series)
        {
            var byDriver = new Dictionary<string, ChampionshipRow>();
            var order = new List<ChampionshipRow>();

            foreach (var round in Data.rounds)
            {
                if (!HasRun(series, round.round)) continue;
                var result = Result(series, round.round);

                foreach (var c in result.Classification)
                {
                    if (string.IsNullOrEmpty(c.driverName)) continue;

                    // The player keeps their own line even if they share a name with somebody in the field.
                    string key = c.isPlayer ? "player" : c.driverName;
                    if (!byDriver.TryGetValue(key, out var row))
                    {
                        row = new ChampionshipRow { driverName = c.driverName, carNumber = c.carNumber, isPlayer = c.isPlayer };
                        byDriver[key] = row;
                        order.Add(row);
                    }

                    row.points += c.points;
                    row.starts++;
                    if (c.finishPosition == 1) row.wins++;
                    if (c.pole) row.poles++;
                    if (c.finishPosition <= 5) row.top5s++;
                    if (c.finishPosition <= 10) row.top10s++;
                    if (c.retired) row.dnfs++;
                    if (c.finishPosition < row.best) row.best = c.finishPosition;
                }
            }

            // Points, then wins, then the best day anybody had — the usual tie-breaks, with the name last so
            // the order is stable rather than dependent on dictionary iteration.
            order.Sort((a, b) =>
            {
                int byPoints = b.points.CompareTo(a.points);
                if (byPoints != 0) return byPoints;
                int byWins = b.wins.CompareTo(a.wins);
                if (byWins != 0) return byWins;
                int byBest = a.best.CompareTo(b.best);
                if (byBest != 0) return byBest;
                int byPoles = b.poles.CompareTo(a.poles);
                if (byPoles != 0) return byPoles;
                return string.CompareOrdinal(a.driverName, b.driverName);
            });

            for (int i = 0; i < order.Count; i++) order[i].position = i + 1;
            return order;
        }

        public static ChampionshipRow Leader(RacingSeries series)
        {
            var table = Standings(series);
            return table.Count > 0 ? table[0] : null;
        }

        // The player's line in whichever championship they are entered in. Null until they have been
        // classified in a race in it.
        public static ChampionshipRow PlayerRow(RacingSeries series)
        {
            foreach (var row in Standings(series)) if (row.isPlayer) return row;
            return null;
        }

        // How far off the championship lead the player is. 0 when leading, -1 when not classified at all.
        public static int PlayerDeficit(RacingSeries series)
        {
            var me = PlayerRow(series);
            var top = Leader(series);
            if (me == null || top == null) return -1;
            return top.points - me.points;
        }

        // ------------------------------------------------------------------ the results feed

        public struct FeedLine
        {
            public int round;
            public RacingSeries series;
            public string text;
            public bool playerRaced;
        }

        // What has happened lately across all three championships, newest first.
        public static List<FeedLine> Feed(int max = 12)
        {
            var lines = new List<FeedLine>();
            for (int i = Data.rounds.Count - 1; i >= 0 && lines.Count < max; i--)
            {
                var round = Data.rounds[i];
                // Sunday's race first, then Saturday's, then Friday's — newest at the top of the feed.
                for (int s = SeriesCatalog.All.Length - 1; s >= 0 && lines.Count < max; s--)
                {
                    var series = SeriesCatalog.All[s];
                    if (!HasRun(series, round.round)) continue;
                    lines.Add(new FeedLine
                    {
                        round = round.round,
                        series = series,
                        text = Result(series, round.round).Headline,
                        playerRaced = round.playerSeries == (int)series && round.playerFinish > 0,
                    });
                }
            }
            return lines;
        }

        // ------------------------------------------------------------------ unread results

        // Races that have been run and were not the player's own drive. Somebody else's result is news;
        // your own is not.
        static int NewsCount()
        {
            int n = 0;
            foreach (var round in Data.rounds)
                foreach (var s in SeriesCatalog.All)
                {
                    if (!HasRun(s, round.round)) continue;
                    if (round.playerSeries == (int)s && round.playerFinish > 0) continue;
                    n++;
                }
            return n;
        }

        public static int Unread => Mathf.Max(0, NewsCount() - Data.seenResults);

        public static void MarkRead()
        {
            int n = NewsCount();
            if (Data.seenResults == n) return;
            Data.seenResults = n;
            Save();
        }

        // ------------------------------------------------------------------ maintenance

        // Roll the whole thing over: a new season starts with three empty championships. The weekend
        // counter keeps climbing, so last season's rounds can never be confused with this season's.
        public static void StartNewSeason()
        {
            Data.season++;
            Data.rounds.Clear();
            Data.seenResults = 0;
            Save();
        }

        public static void InvalidateCache() { _cache = null; Bump(); }

        public static void ClearAll()
        {
            _cache = new Book();
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            Bump();
            Changed?.Invoke();
        }
    }
}
