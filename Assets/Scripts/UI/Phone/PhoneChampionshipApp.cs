using Draftmaster.Weekend;
using UnityEngine;

// The other two championships, on the player's phone.
//
// Three series share every venue and the player drives in one of them; the other two run their weekend
// whether anybody watches or not (SeasonChampionships). This is where a driver finds out what happened in
// them — the badge counts results that have come in since they last looked, which is how you learn that
// the Cup race finished while you were signing hats at the fence.
//
// Read-only, like every app on the phone. Sunday's results are not visible on Friday: everything here goes
// through SeasonChampionships.HasRun, which is gated on the weekend clock.
public class PhoneChampionshipApp : PhoneApp
{
    public override string Id => "championship";
    public override string TileName => "POINTS";
    public override string TileSubtitle => "Championships";
    public override Color Accent => PixelGUI.Info;

    // Somebody else's race finishing is news; your own result is not.
    public override int Badge => SeasonChampionships.Unread;

    public override void OnOpen() => SeasonChampionships.MarkRead();

    public override float Draw(float x, float y, float w)
    {
        float y0 = y;
        var mine = SeriesCatalog.PlayerSeries;
        int live = WeekendLedger.WeekendId;

        if (SeasonChampionships.RoundCount == 0)
            return Empty(x, y, w, "No rounds run yet. The season starts at the first track you turn up to.");

        // --- the three races of the weekend you are stood in ---
        y += Section(x, y, w, "THIS WEEKEND");
        if (live < 0 || SeasonChampionships.RoundNumber(live) == 0)
        {
            y += Empty(x, y, w, "Between weekends.");
        }
        else
        {
            string track = SeasonChampionships.TrackNameOf(live);
            y += Row(x, y, w, string.IsNullOrEmpty(track) ? "The track" : track,
                     "ROUND " + SeasonChampionships.RoundNumber(live), PixelGUI.Gold);

            foreach (var s in SeriesCatalog.All)
                y += WeekendRaceRow(x, y, w, s, live, s == mine);
        }
        y += PixelGUI.Px(6f);

        // --- what has come in lately ---
        var feed = SeasonChampionships.Feed(5);
        if (feed.Count > 0)
        {
            y += Section(x, y, w, "RESULTS");
            foreach (var line in feed)
                y += Body(x, y, w, "· " + line.text, line.playerRaced ? PixelGUI.Gold : PixelGUI.TextDim);
            y += PixelGUI.Px(6f);
        }

        // --- the three tables, the player's own first ---
        y += Table(x, y, w, mine, true);
        foreach (var s in SeriesCatalog.All)
            if (s != mine) y += Table(x, y, w, s, false);

        y += Body(x, y, w, "Standings are through the last race run.", PixelGUI.TextDisabled);
        return y - y0 + PixelGUI.Px(6f);
    }

    // One of this weekend's three races: the result if it has run, otherwise when it is due.
    static float WeekendRaceRow(float x, float y, float w, RacingSeries s, int round, bool mine)
    {
        string label = SeriesCatalog.ShortCode(s) + " RACE";

        if (!SeasonChampionships.HasRun(s, round))
        {
            var t = WeekendTimetable.RaceTime(s);
            return Row(x, y, w, label, WeekendSlots.ShortLabel(t.slot) + " " + WeekendSlots.Clock(t.startMinute),
                       mine ? PixelGUI.Gold : PixelGUI.TextDim);
        }

        var winner = SeasonChampionships.Result(s, round).Winner;
        string right = winner == null ? "-"
                     : winner.isPlayer ? "YOU WON"
                     : "#" + winner.carNumber + " " + Trim(LastName(winner.driverName), 12);
        return Row(x, y, w, label, right, winner != null && winner.isPlayer ? PixelGUI.Gold : PixelGUI.Confirm);
    }

    // One championship table: the top five, plus the player's own line when they are outside it.
    static float Table(float x, float y, float w, RacingSeries s, bool mine)
    {
        float y0 = y;
        var table = SeasonChampionships.Standings(s);

        y += Section(x, y, w, SeriesCatalog.Name(s).ToUpperInvariant() + (mine ? " - YOURS" : ""));
        if (table.Count == 0)
        {
            y += Empty(x, y, w, "Not run yet.");
            return y - y0 + PixelGUI.Px(6f);
        }

        int leaderPoints = table[0].points;
        int shown = Mathf.Min(5, table.Count);
        bool playerShown = false;

        for (int i = 0; i < shown; i++)
        {
            if (table[i].isPlayer) playerShown = true;
            y += StandingRow(x, y, w, table[i], leaderPoints);
        }

        if (!playerShown)
        {
            var me = SeasonChampionships.PlayerRow(s);
            if (me != null)
            {
                if (me.position > shown + 1) y += Row(x, y, w, "  ...", "", PixelGUI.TextDisabled);
                y += StandingRow(x, y, w, me, leaderPoints);
            }
        }

        y += PixelGUI.Px(6f);
        return y - y0;
    }

    static float StandingRow(float x, float y, float w, ChampionshipRow row, int leaderPoints)
    {
        string left = row.position + ". " + Trim(row.driverName, 18);
        int gap = leaderPoints - row.points;
        string right = row.position == 1 ? row.points.ToString() : row.points + "  -" + gap;
        return Row(x, y, w, left, right, row.isPlayer ? PixelGUI.Gold : PixelGUI.Text);
    }

    // "Wade Corliss" -> "Corliss". The weekend race row has room for a surname, not a name.
    static string LastName(string full)
    {
        if (string.IsNullOrEmpty(full)) return "";
        int space = full.LastIndexOf(' ');
        return space > 0 && space < full.Length - 1 ? full.Substring(space + 1) : full;
    }
}
