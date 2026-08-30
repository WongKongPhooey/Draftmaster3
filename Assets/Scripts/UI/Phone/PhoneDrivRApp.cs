using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Draftmaster.Data;

// DrivR — the form guide. Every driver in the database, ranked by ability, with their season results and
// the stats the AI actually drives on, so the player can look up who they're about to race.
//
// The stat names here are the same ones AIDriverBinding reads (Qualifying and Consistency set pace,
// Aggression skews the line), so a driver who reads aggressive on this screen races that way.
//
// The database is read once per open, and again when the player picks somebody — never per frame.
public class PhoneDrivRApp : PhoneApp
{
    public override string Id => "drivr";
    public override string TileName => "DrivR";
    public override string TileSubtitle => "The form guide";
    public override Color Accent => PixelGUI.Danger;

    class Row
    {
        public Driver driver;
        public int starts, wins, top5s, bestFinish;
        public float avgFinish;
        public bool resultsLoaded;
    }

    readonly List<Row> _rows = new();
    Row _selected;
    bool _loaded;
    string _status = "";

    public override void OnOpen()
    {
        _selected = null;
        _loaded = false;
        Load();
    }

    void Load()
    {
        if (_loaded) return;
        _rows.Clear();

        var dbm = DatabaseManager.Instance;
        if (dbm == null || !dbm.IsReady)
        {
            _status = "Database still loading.";
            return;
        }

        try
        {
            foreach (var d in dbm.Connection.Table<Driver>())
                if (d != null && !d.Retired) _rows.Add(new Row { driver = d });
        }
        catch (System.Exception e)
        {
            _status = "Couldn't read the driver table.";
            Debug.LogWarning($"DrivR: driver query failed — {e.Message}");
            return;
        }

        _rows.Sort((a, b) => b.driver.CurrentAbility.CompareTo(a.driver.CurrentAbility));
        _status = _rows.Count == 0 ? "No drivers in the database." : "";
        _loaded = true;
    }

    // Results are only pulled for the driver being looked at — one query per tap, not per row.
    void LoadResults(Row row)
    {
        if (row == null || row.resultsLoaded) return;
        row.resultsLoaded = true;

        var dbm = DatabaseManager.Instance;
        if (dbm == null || !dbm.IsReady) return;

        try
        {
            var results = dbm.Connection.Table<Result>().Where(r => r.DriverId == row.driver.Id).ToList();
            row.starts = results.Count;
            row.bestFinish = int.MaxValue;
            int total = 0;
            foreach (var r in results)
            {
                if (r.FinishPosition == 1) row.wins++;
                if (r.FinishPosition <= 5) row.top5s++;
                if (r.FinishPosition > 0 && r.FinishPosition < row.bestFinish) row.bestFinish = r.FinishPosition;
                total += r.FinishPosition;
            }
            row.avgFinish = results.Count > 0 ? (float)total / results.Count : 0f;
            if (row.bestFinish == int.MaxValue) row.bestFinish = 0;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"DrivR: results query failed — {e.Message}");
        }
    }

    public override float Draw(float x, float y, float w)
    {
        Load();
        if (!string.IsNullOrEmpty(_status)) return Empty(x, y, w, _status);
        return _selected == null ? DrawList(x, y, w) : DrawProfile(x, y, w, _selected);
    }

    float DrawList(float x, float y, float w)
    {
        float y0 = y;
        y += Section(x, y, w, $"FIELD · {_rows.Count}");

        // The number on the right is the driver's ability rating out of 100 — the same one the profile
        // opens with. Unlabelled it reads like points, which it is not.
        float head = RowH;
        PhoneStyles.Label(new Rect(x + PixelGUI.Px(2f), y, w - PixelGUI.Px(4f), head), "DRIVER",
                          PhoneStyles.Footer);
        PhoneStyles.Label(new Rect(x + PixelGUI.Px(2f), y, w - PixelGUI.Px(4f), head), "ABILITY",
                          PhoneStyles.Footer, null, TextAnchor.MiddleRight);
        y += head;

        float rowH = RowH;
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var d = row.driver;
            var r = new Rect(x, y, w, rowH);

            if (i % 2 == 0) PixelGUI.Fill(r, new Color(PixelGUI.Plate.r, PixelGUI.Plate.g, PixelGUI.Plate.b, 0.5f));
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) { _selected = row; LoadResults(row); }

            // Ability doubles as the rank colour: the quick ones stand out in a list this long.
            Color tint = d.CurrentAbility >= 85 ? PixelGUI.Gold
                       : d.CurrentAbility >= 70 ? PixelGUI.Text
                       : PixelGUI.TextDim;

            Row2(x + PixelGUI.Px(2f), y, w - PixelGUI.Px(4f),
                 $"{("#" + d.CarNumber),-4}{Trim(Name(d), 14)}", d.CurrentAbility.ToString(), tint);
            y += rowH;
        }

        return y - y0 + PixelGUI.Px(6f);
    }

    float DrawProfile(float x, float y, float w, Row row)
    {
        float y0 = y;
        var d = row.driver;

        if (GUI.Button(new Rect(x, y, w, RowH), GUIContent.none, GUIStyle.none)) _selected = null;
        y += Row(x, y, w, "< all drivers", "", PixelGUI.Info);
        y += PixelGUI.Px(3f);

        y += Section(x, y, w, $"#{d.CarNumber} {Name(d).ToUpperInvariant()}");
        y += Row(x, y, w, d.TeamName ?? "Unattached", d.Manufacturer ?? "", PixelGUI.TextDim);
        y += Row(x, y, w, $"Age {d.Age}", $"Debut {d.DebutSeason}", PixelGUI.TextDim);
        if (!string.IsNullOrEmpty(d.Nickname)) y += Body(x, y, w, "\"" + d.Nickname + "\"", PixelGUI.TextDisabled);

        y += PixelGUI.Px(4f);
        y += Section(x, y, w, "ABILITY / 100");
        y += Meter(x, y, w, "Now", d.CurrentAbility / (float)Driver.AbilityMax, d.CurrentAbility.ToString(), PixelGUI.Gold);
        y += Meter(x, y, w, "Ceiling", d.PotentialAbility / (float)Driver.AbilityMax, d.PotentialAbility.ToString(), PixelGUI.Info);

        y += PixelGUI.Px(4f);
        y += Section(x, y, w, "SEASON");
        LoadResults(row);
        if (row.starts == 0)
        {
            y += Empty(x, y, w, "No races logged yet.");
        }
        else
        {
            y += Row(x, y, w, "Starts", row.starts.ToString(), PixelGUI.TextDim);
            y += Row(x, y, w, "Wins", row.wins.ToString(), row.wins > 0 ? PixelGUI.Gold : PixelGUI.TextDim);
            y += Row(x, y, w, "Top 5s", row.top5s.ToString(), PixelGUI.TextDim);
            y += Row(x, y, w, "Best finish", row.bestFinish > 0 ? "P" + row.bestFinish : "—", PixelGUI.TextDim);
            y += Row(x, y, w, "Average finish", row.avgFinish > 0f ? row.avgFinish.ToString("0.0") : "—", PixelGUI.TextDim);
        }

        y += PixelGUI.Px(4f);
        y += Section(x, y, w, "CRAFT");
        y += Stat(x, y, w, "Qualifying", d.Qualifying);
        y += Stat(x, y, w, "Consistency", d.Consistency);
        y += Stat(x, y, w, "Aggression", d.Aggression);
        y += Stat(x, y, w, "Awareness", d.Awareness);
        y += Stat(x, y, w, "Adaptability", d.Adaptability);
        y += Stat(x, y, w, "Fuel saving", d.FuelManagement);
        y += Stat(x, y, w, "Tyre saving", d.TyreManagement);

        y += PixelGUI.Px(4f);
        y += Section(x, y, w, "TRACK TYPES");
        y += Stat(x, y, w, "Short tracks", d.ShortTracks);
        y += Stat(x, y, w, "Speedways", d.Speedways);
        y += Stat(x, y, w, "Superspeedways", d.Superspeedways);
        y += Stat(x, y, w, "Road courses", d.RoadCourses);
        y += Stat(x, y, w, "Dirt", d.DirtCourses);

        y += PixelGUI.Px(4f);
        y += Section(x, y, w, "OFF TRACK");
        y += Meter(x, y, w, "Prestige", d.Prestige / 100f, d.Prestige.ToString(), PixelGUI.Info);
        y += Meter(x, y, w, "Fan support", d.FanSupport / 100f, d.FanSupport.ToString(), PixelGUI.Confirm);
        y += Meter(x, y, w, "Sponsor appeal", d.SponsorAppeal / 100f, d.SponsorAppeal.ToString(), PixelGUI.Gold);

        return y - y0 + PixelGUI.Px(6f);
    }

    // A 0..20 skill as pips — the same read as the driver database window, and quicker than a number.
    float Stat(float x, float y, float w, string label, int value)
    {
        float h = RowH;
        GUI.Label(new Rect(x, y, w * 0.55f, h), label, PhoneStyles.DataDim);
        float cellsW = PixelGUI.CellsWidth(Driver.StatMax / 2);
        var cells = new Rect(x + w - cellsW, y + (h - PixelGUI.CellsHeight) * 0.5f, cellsW, PixelGUI.CellsHeight);
        PixelGUI.Cells(cells, Mathf.RoundToInt(value / 2f), Driver.StatMax / 2,
                       value >= 16 ? PixelGUI.Gold : (Color?)null);
        return h;
    }

    // Left/right pair with the right side coloured too — the list rows want both halves tinted.
    static float Row2(float x, float y, float w, string left, string right, Color colour)
    {
        float h = RowH;
        var style = PhoneStyles.Data;
        var prev = style.normal.textColor;
        style.normal.textColor = colour;
        GUI.Label(new Rect(x, y, w, h), left, style);
        var prevAlign = style.alignment;
        style.alignment = TextAnchor.MiddleRight;
        GUI.Label(new Rect(x, y, w, h), right, style);
        style.alignment = prevAlign;
        style.normal.textColor = prev;
        return h;
    }

    static string Name(Driver d)
    {
        string first = string.IsNullOrEmpty(d.FirstName) ? "" : d.FirstName.Substring(0, 1) + ". ";
        return (first + (d.LastName ?? "")).Trim();
    }
}
