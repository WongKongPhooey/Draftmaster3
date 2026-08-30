using UnityEngine;

// Notes — the log of what people in the paddock have asked of the player. Open notes first, with what
// they want and how far along it is; finished ones fall to the bottom, struck through rather than gone,
// because "who did I do that favour for" is worth being able to look up.
public class PhoneNotesApp : PhoneApp
{
    public override string Id => "notes";
    public override string TileName => "NOTES";
    public override string TileSubtitle => "Who asked what";
    public override Color Accent => PixelGUI.Confirm;
    public override int Badge => PhoneNotes.Unread;

    public override void OnOpen() => PhoneNotes.MarkAllRead();

    public override float Draw(float x, float y, float w)
    {
        float y0 = y;
        var notes = PhoneNotes.All;

        int open = 0, done = 0;
        for (int i = 0; i < notes.Count; i++) { if (notes[i].resolved) done++; else open++; }

        if (notes.Count == 0)
            return Empty(x, y, w, "Nothing written down yet. Talk to people in the paddock — anything you " +
                                  "agree to ends up here.");

        y += Section(x, y, w, $"OPEN · {open}");
        if (open == 0) y += Empty(x, y, w, "All caught up.");
        for (int i = 0; i < notes.Count; i++)
            if (!notes[i].resolved) y += DrawNote(x, y, w, notes[i]);

        if (done > 0)
        {
            y += PixelGUI.Px(6f);
            y += Section(x, y, w, $"DONE · {done}");
            for (int i = 0; i < notes.Count; i++)
                if (notes[i].resolved) y += DrawNote(x, y, w, notes[i]);
        }

        return y - y0 + PixelGUI.Px(6f);
    }

    float DrawNote(float x, float y, float w, PhoneNotes.Note note)
    {
        // Measure first so the plate can be drawn behind the text in one pass.
        float pad = PixelGUI.Px(4f);
        float inner = w - pad * 2f;
        float h = RowH * 2f;                       // title + from/stamp line
        float bodyH = PhoneStyles.Body.CalcHeight(new GUIContent(note.body ?? ""), inner);
        if (!string.IsNullOrEmpty(note.body)) h += bodyH + PixelGUI.Px(2f);

        string progress = ProgressLine(note);
        if (!string.IsNullOrEmpty(progress)) h += RowH;
        h += pad * 2f;

        var plate = new Rect(x, y, w, h);
        Plate(plate, note.resolved ? PixelGUI.PlateLight : PixelGUI.Confirm);

        float cx = x + pad, cy = y + pad;
        cy += Row(cx, cy, inner, note.title, note.resolved ? "DONE" : "",
                  note.resolved ? PixelGUI.TextDim : PixelGUI.Text);
        cy += Row(cx, cy, inner, string.IsNullOrEmpty(note.from) ? note.stamp : note.from, note.stamp,
                  PixelGUI.TextDisabled, dim: true);
        if (!string.IsNullOrEmpty(note.body))
            cy += Body(cx, cy, inner, note.body, note.resolved ? PixelGUI.TextDisabled : PixelGUI.TextDim);
        if (!string.IsNullOrEmpty(progress))
            Row(cx, cy, inner, progress, "", note.resolved ? PixelGUI.TextDisabled : PixelGUI.Gold);

        return h + PixelGUI.Px(4f);
    }

    // Quest notes can say how far along they are; plain notes can't.
    static string ProgressLine(PhoneNotes.Note note)
    {
        if (note.resolved || string.IsNullOrEmpty(note.questId)) return "";
        var quest = FindQuest(note.questId);
        if (quest == null) return "";
        var state = QuestManager.GetState(quest);
        if (state == QuestManager.State.NotStarted) return "";
        return QuestManager.DescribeProgress(quest);
    }

    static QuestInfo FindQuest(string id)
    {
        var all = QuestManager.All;
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].id == id) return all[i];
        return null;
    }
}
