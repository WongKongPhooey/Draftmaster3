using System.Collections.Generic;

namespace Draftmaster.Weekend
{
    // One championship's race weekend at one round, resolved into the only thing a season cares about: a
    // classification worth points.
    //
    // The two championships the player is not entered in run their weekend whether anybody watches or not,
    // so their result is simulated (SeriesSimulator). The championship the player IS in runs the same
    // simulated field, and then the player is cut into it at the position they actually finished when they
    // drove the race - which is why this type carries a player slot rather than there being two kinds of
    // round.
    //
    // Why the player's real race is not used wholesale: the AI cars in the race scene are drawn from a
    // shuffled pool of database drivers, so the names change from round to round. A championship table
    // needs the same thirty-odd drivers every week to mean anything, so the field is the simulator's
    // roster and the player is the one real entry in it.
    //
    // Deterministic from (series, round): the same round always classifies the same way, so the standings
    // can be rebuilt from a list of round numbers and always agree with the race the player sat and watched.
    public class SeriesWeekendResult
    {
        public RacingSeries series;
        public int round;
        public string trackName = "";

        // The simulated running of the race. Carries the grid too - every entry keeps the qualifying
        // position it started from.
        public SeriesSimulator.Session race;

        // The player's own drive, when this is their championship and they took the start.
        public string playerName = "";
        public int playerFinish;      // 1-based; 0 = did not drive this one
        public int playerGrid;        // 1-based qualifying position; 0 = unknown / no qualifying run
        // The player's number on the timing tower. Not partitioned like the simulated fields (those are
        // spaced mod 3 so no number is ever entered twice at the venue), so it is set from the one number
        // the rest of the game already uses for the player's car.
        public int playerCarNumber;

        public bool PlayerRan => playerFinish > 0 && !string.IsNullOrEmpty(playerName);

        // One classified car: what it is worth to the championship and what the results feed reads off.
        public class Classified
        {
            public string driverName = "";
            public int carNumber;
            public int gridPosition;
            public int finishPosition;
            public bool retired;
            public bool isPlayer;
            public bool pole;
            public int points;
        }

        List<Classified> _classification;

        // ------------------------------------------------------------------ building

        public static SeriesWeekendResult Simulate(RacingSeries series, int round, string trackName,
                                                   IReadOnlyList<string> roster = null)
        {
            return new SeriesWeekendResult
            {
                series = series,
                round = round,
                trackName = trackName ?? "",
                // Base lap time is a display detail - it shifts every lap time by the same amount and so
                // never changes the order. The standings are keyed off order alone, which is what lets the
                // season agree with the grandstand screen, where the real track's lap length is known.
                race = SeriesSimulator.Race(series, round, trackName, 32f, roster),
            };
        }

        // The player drove this round of their own championship. Cutting them in at their finishing position
        // pushes everybody from there down one place, which is exactly what beating them does.
        public SeriesWeekendResult WithPlayer(string name, int finishPosition, int gridPosition = 0)
        {
            playerName = name ?? "";
            playerFinish = finishPosition > 0 ? finishPosition : 0;
            playerGrid = gridPosition > 0 ? gridPosition : 0;
            _classification = null;
            return this;
        }

        // ------------------------------------------------------------------ the result

        public IReadOnlyList<Classified> Classification => _classification ??= BuildClassification();

        List<Classified> BuildClassification()
        {
            var rows = new List<Classified>();
            if (race == null) return rows;

            // The simulated field, in finishing order.
            var field = new List<SeriesSimulator.Entry>(race.entries);
            field.Sort((a, b) => a.finishPosition.CompareTo(b.finishPosition));
            foreach (var e in field)
                rows.Add(new Classified
                {
                    driverName = e.driverName,
                    carNumber = e.carNumber,
                    gridPosition = e.gridPosition,
                    retired = e.retired,
                });

            if (PlayerRan)
            {
                // The player qualified too, so the simulated grid has to make room: anybody who was slower
                // than them in qualifying started a row further back. Without this, a player who took pole
                // and the simulated pole sitter would both be scored the pole point.
                if (playerGrid > 0)
                    foreach (var r in rows)
                        if (r.gridPosition >= playerGrid) r.gridPosition++;

                int at = playerFinish - 1;
                if (at > rows.Count) at = rows.Count;
                rows.Insert(at, new Classified
                {
                    driverName = playerName,
                    carNumber = playerCarNumber,
                    gridPosition = playerGrid,
                    isPlayer = true,
                });
            }

            // Renumber after the insert, then price it.
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                r.finishPosition = i + 1;
                r.pole = r.gridPosition == 1;
                r.points = ChampionshipPoints.ForRound(r.finishPosition, r.pole);
            }
            return rows;
        }

        public Classified Winner
        {
            get
            {
                var rows = Classification;
                return rows.Count > 0 ? rows[0] : null;
            }
        }

        public Classified PoleSitter()
        {
            foreach (var r in Classification) if (r.pole) return r;
            return null;
        }

        public Classified Find(string driverName)
        {
            if (string.IsNullOrEmpty(driverName)) return null;
            foreach (var r in Classification) if (r.driverName == driverName) return r;
            return null;
        }

        // ------------------------------------------------------------------ what it reads like

        // The line the results feed carries: who won, where they started, and where it was.
        public string Headline
        {
            get
            {
                var w = Winner;
                if (w == null) return SeriesCatalog.ShortCode(series) + ": no result.";

                string where = string.IsNullOrEmpty(trackName) ? "" : " at " + trackName;
                string from = w.gridPosition > 0 ? " from P" + w.gridPosition : "";
                string who = w.isPlayer ? "You win" : $"#{w.carNumber} {w.driverName} wins";
                return $"{SeriesCatalog.ShortCode(series)}: {who} the {SeriesCatalog.Nickname(series)} race{where}{from}.";
            }
        }

        // How the player's own round read, when they were in it.
        public string PlayerLine
        {
            get
            {
                if (!PlayerRan) return "";
                var me = Find(playerName);
                int pos = me?.finishPosition ?? playerFinish;
                return pos == 1 ? "You won." : "You finished P" + pos + ".";
            }
        }
    }
}
