using UnityEngine;

// MESSAGES — texts from the people the driver works with. The tile says how many are waiting
// ("1 unread message"), and opening the app goes straight to whoever wrote last if any of it is unread,
// because the arrow keys only move round the home grid: a list the player has to click into would strand
// anyone on a keyboard. Otherwise it is a list of threads, newest first.
//
// Read-only, like every app on the phone. Store: PhoneMessages.
public class PhoneMessagesApp : PhoneApp
{
    public override string Id => "messages";
    public override string TileName => "MESSAGES";
    public override string TileSubtitle => UnreadLabel(PhoneMessages.Unread);
    public override Color Accent => PixelGUI.Danger;
    public override int Badge => PhoneMessages.Unread;

    string _threadId;         // null = the thread list
    int _newFrom = -1;        // first message in the open thread the player had not seen, for the NEW rule

    public static string UnreadLabel(int unread)
    {
        if (unread <= 0) return "No new messages";
        return unread == 1 ? "1 unread message" : unread + " unread messages";
    }

    public override void OnOpen()
    {
        _threadId = null;
        _newFrom = -1;

        var threads = PhoneMessages.Threads;
        for (int i = 0; i < threads.Count; i++)
            if (threads[i].Unread > 0) { OpenThread(threads[i]); return; }
        if (threads.Count == 1) OpenThread(threads[0]);
    }

    void OpenThread(PhoneMessages.Thread thread)
    {
        _threadId = thread.id;
        _newFrom = thread.Unread > 0 ? thread.read : -1;
        PhoneMessages.MarkRead(thread.id);
        ScrollToTop();
    }

    public override float Draw(float x, float y, float w)
    {
        var thread = PhoneMessages.Find(_threadId);
        if (thread != null) return DrawThread(x, y, w, thread);

        _threadId = null;
        return DrawList(x, y, w);
    }

    float DrawList(float x, float y, float w)
    {
        float y0 = y;
        var threads = PhoneMessages.Threads;
        if (threads.Count == 0)
            return Empty(x, y, w, "No messages. People on the team text when they need you somewhere.");

        y += Section(x, y, w, $"THREADS · {threads.Count}");

        float pad = PixelGUI.Px(4f);
        float inner = w - pad * 2f;
        for (int i = 0; i < threads.Count; i++)
        {
            var t = threads[i];
            float h = RowH * 2f + pad * 2f;
            var plate = new Rect(x, y, w, h);
            Plate(plate, t.Unread > 0 ? Accent : PixelGUI.PlateLight);
            if (Pressed(plate)) OpenThread(t);

            float cy = y + pad;
            cy += Row(x + pad, cy, inner, Name(t), t.Unread > 0 ? t.Unread + " NEW" : (t.Last?.stamp ?? ""),
                      t.Unread > 0 ? PixelGUI.Text : PixelGUI.TextDim);
            Row(x + pad, cy, inner, Trim(t.Last?.text, 30), "", PixelGUI.TextDisabled, dim: true);

            y += h + PixelGUI.Px(4f);
        }

        return y - y0 + PixelGUI.Px(6f);
    }

    float DrawThread(float x, float y, float w, PhoneMessages.Thread thread)
    {
        float y0 = y;

        // Back to the list — only worth offering when there is a list to go back to.
        if (PhoneMessages.Threads.Count > 1)
        {
            if (Pressed(new Rect(x, y, w, RowH))) { _threadId = null; ScrollToTop(); }
            y += Row(x, y, w, "< all messages", "", PixelGUI.Info);
            y += PixelGUI.Px(3f);
        }

        y += Section(x, y, w, Name(thread).ToUpperInvariant());
        if (!string.IsNullOrEmpty(thread.role)) y += Row(x, y, w, thread.role, "", PixelGUI.TextDim, dim: true);
        y += PixelGUI.Px(3f);

        for (int i = 0; i < thread.messages.Count; i++)
        {
            if (i == _newFrom) y += NewRule(x, y, w);
            y += Bubble(x, y, w, thread.messages[i]);
        }

        return y - y0 + PixelGUI.Px(6f);
    }

    // One text: theirs on the left, the player's on the right, the time underneath.
    float Bubble(float x, float y, float w, PhoneMessages.Message m)
    {
        float pad = PixelGUI.Px(4f);
        float bw = Mathf.Round(w * 0.86f);
        float inner = bw - pad * 2f;
        float textH = PhoneStyles.Body.CalcHeight(new GUIContent(m.text ?? ""), inner);
        float h = textH + pad * 2f;

        float bx = m.fromPlayer ? x + w - bw : x;
        var plate = new Rect(bx, y, bw, h);
        Plate(plate, m.fromPlayer ? PixelGUI.Info : Accent);
        Body(bx + pad, y + pad, inner, m.text, m.fromPlayer ? PixelGUI.TextDim : PixelGUI.Text);

        float used = h;
        if (!string.IsNullOrEmpty(m.stamp))
        {
            PhoneStyles.Label(new Rect(bx, y + h, bw, RowH), m.stamp, PhoneStyles.Footer, null,
                              m.fromPlayer ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft);
            used += RowH;
        }
        return used + PixelGUI.Px(4f);
    }

    // The line above the first message the player had not seen before they opened the thread.
    float NewRule(float x, float y, float w)
    {
        float h = RowH;
        const string label = "NEW";
        float labelW = Mathf.Ceil(PhoneStyles.Footer.CalcSize(new GUIContent(label)).x);
        PhoneStyles.Label(new Rect(x, y, labelW, h), label, PhoneStyles.Footer, Accent);
        float gap = PixelGUI.Px(3f);
        PixelGUI.Rule(x + labelW + gap, y + h * 0.5f, Mathf.Max(0f, w - labelW - gap),
                      new Color(Accent.r, Accent.g, Accent.b, 0.6f));
        return h;
    }

    static string Name(PhoneMessages.Thread t) =>
        string.IsNullOrEmpty(t.contact) ? (string.IsNullOrEmpty(t.role) ? "Unknown" : t.role) : t.contact;
}
