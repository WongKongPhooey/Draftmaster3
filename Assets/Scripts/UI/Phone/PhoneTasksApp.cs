using System.Collections.Generic;
using UnityEngine;
using Draftmaster.Fans;
using Draftmaster.Sponsors;

// Tasks — "what do I do next?". The checklist of things standing between the player and progress, built
// fresh each frame from the systems that own the answer rather than stored anywhere: the weekend session,
// the sponsor book, fan appeal, and any side quest that's finished and waiting to be reported.
//
// Anything else that wants a line here calls PhoneTasksApp.Push(id, text) and PhoneTasksApp.Resolve(id);
// pushed tasks persist in PlayerPrefs so a scene reload doesn't lose them.
public class PhoneTasksApp : PhoneApp
{
    public override string Id => "tasks";
    public override string TileName => "TASKS";
    public override string TileSubtitle => "What's next";
    public override Color Accent => PixelGUI.Gold;

    struct Task
    {
        public string text;
        public string readout;
        public bool done;
        public string hint;
    }

    const string PushedKey = "phone.tasks.pushed";   // id|text pairs, newline separated

    // ------------------------------------------------------------------ external tasks

    // Add a one-off task from anywhere ("Collect the engine from the truck"). Re-pushing the same id
    // replaces its text rather than doubling it up.
    public static void Push(string id, string text)
    {
        if (string.IsNullOrEmpty(id)) return;
        var lines = PushedLines();
        lines.RemoveAll(l => l.StartsWith(id + "|"));
        lines.Add(id + "|" + text);
        PlayerPrefs.SetString(PushedKey, string.Join("\n", lines));
        PlayerPrefs.Save();
    }

    public static void Resolve(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        var lines = PushedLines();
        if (lines.RemoveAll(l => l.StartsWith(id + "|")) == 0) return;
        PlayerPrefs.SetString(PushedKey, string.Join("\n", lines));
        PlayerPrefs.Save();
    }

    static List<string> PushedLines()
    {
        var raw = PlayerPrefs.GetString(PushedKey, "");
        var list = new List<string>();
        if (string.IsNullOrEmpty(raw)) return list;
        foreach (var line in raw.Split('\n'))
            if (!string.IsNullOrEmpty(line)) list.Add(line);
        return list;
    }

    // ------------------------------------------------------------------ the list

    public override int Badge
    {
        get
        {
            int n = 0;
            foreach (var q in QuestManager.All)
                if (q != null && QuestManager.GetState(q) == QuestManager.State.ReadyToTurnIn) n++;
            return n;
        }
    }

    public override float Draw(float x, float y, float w)
    {
        float y0 = y;

        var weekend = new List<Task>();
        BuildWeekend(weekend);
        y += Section(x, y, w, SessionHeading());
        y += DrawTasks(x, y, w, weekend);

        var career = new List<Task>();
        BuildCareer(career);
        y += PixelGUI.Px(6f);
        y += Section(x, y, w, "CAREER");
        y += DrawTasks(x, y, w, career);

        var pushed = new List<Task>();
        foreach (var line in PushedLines())
        {
            int split = line.IndexOf('|');
            if (split > 0) pushed.Add(new Task { text = line.Substring(split + 1) });
        }
        if (pushed.Count > 0)
        {
            y += PixelGUI.Px(6f);
            y += Section(x, y, w, "REMINDERS");
            y += DrawTasks(x, y, w, pushed);
        }

        return y - y0 + PixelGUI.Px(6f);
    }

    static string SessionHeading() =>
        RaceWeekend.IsQualifying ? "QUALIFYING" : RaceWeekend.IsPractice ? "PRACTICE" : "RACE";

    void BuildWeekend(List<Task> list)
    {
        float best = PlayerBestLap();
        string lap = best > 0f ? LapTimingManager.Format(best) : "no time";

        if (RaceWeekend.IsPractice)
        {
            list.Add(new Task { text = "Run a clean lap", readout = lap, done = best > 0f,
                                hint = "Off the track surface or into a barrier voids it." });
            list.Add(new Task { text = "Set the car up with the crew chief", done = false,
                                hint = "He's on the pit box." });
            list.Add(new Task { text = "Move on to qualifying", done = false,
                                hint = "The session button is top right." });
        }
        else if (RaceWeekend.IsQualifying)
        {
            list.Add(new Task { text = "Set a qualifying time", readout = lap, done = best > 0f,
                                hint = "Best lap sets the grid." });
            list.Add(new Task { text = "Start the race", done = false });
        }
        else
        {
            float progress = RaceDirector.Progress01;
            list.Add(new Task
            {
                text = "Finish the race",
                readout = progress >= 0f ? $"{Mathf.RoundToInt(progress * 100f)}%" : "not started",
                done = progress >= 1f,
            });
        }
    }

    void BuildCareer(List<Task> list)
    {
        int signed = SponsorBook.Count;
        list.Add(new Task
        {
            text = "Sign a sponsor",
            readout = signed > 0 ? $"{signed} signed" : "none",
            done = signed > 0,
            hint = signed > 0 ? "" : "Their reps work the pit lane on a race weekend.",
        });

        if (signed > 0)
        {
            int placed = 0, active = 0;
            foreach (var deal in SponsorBook.Deals)
            {
                if (!deal.IsActive) continue;
                active++;
                if (deal.IsPlaced) placed++;
            }
            list.Add(new Task
            {
                text = "Get every deal on the car",
                readout = $"{placed}/{active}",
                done = active > 0 && placed >= active,
                hint = placed >= active ? "" : "A deal earns nothing until its decal is painted on.",
            });
        }

        float appeal = FanAppeal.Value;
        if (appeal < 55f)
            list.Add(new Task
            {
                text = "Build your fan appeal",
                readout = $"{Mathf.RoundToInt(appeal)}/100",
                done = false,
                hint = "Sign for the fans who come up to you in the pit lane.",
            });

        foreach (var q in QuestManager.All)
        {
            if (q == null || QuestManager.GetState(q) != QuestManager.State.ReadyToTurnIn) continue;
            list.Add(new Task { text = "Report back: " + q.title, readout = "done", done = false,
                                hint = "Find whoever gave it to you." });
        }
    }

    float PlayerBestLap()
    {
        var lt = LapTimingManager.Instance;
        if (lt == null) return -1f;
        var rows = lt.Rows;
        for (int i = 0; i < rows.Count; i++)
            if (rows[i] != null && rows[i].isPlayer) return rows[i].bestLap;
        return -1f;
    }

    float DrawTasks(float x, float y, float w, List<Task> tasks)
    {
        float y0 = y;
        if (tasks.Count == 0) return Empty(x, y, w, "Nothing outstanding.");

        foreach (var t in tasks)
        {
            float box = PixelGUI.Px(7f);
            var tick = new Rect(x, y + PixelGUI.Px(2f), box, box);
            PixelGUI.Fill(tick, t.done ? PixelGUI.Confirm : PixelGUI.PlateLight);
            if (t.done)
                PixelGUI.Fill(new Rect(tick.x + PixelGUI.Px(2f), tick.y + PixelGUI.Px(2f),
                                       box - PixelGUI.Px(4f), box - PixelGUI.Px(4f)), PixelGUI.Ink);

            float tx = x + box + PixelGUI.Px(4f);
            float tw = w - box - PixelGUI.Px(4f);
            y += Row(tx, y, tw, t.text, t.readout, t.done ? PixelGUI.TextDim : PixelGUI.Text);
            if (!string.IsNullOrEmpty(t.hint)) y += Body(tx, y, tw, t.hint, PixelGUI.TextDisabled);
            y += PixelGUI.Px(3f);
        }
        return y - y0;
    }
}
