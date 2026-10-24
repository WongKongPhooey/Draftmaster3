using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEngine;

// Developer shortcuts for the race weekend, hung under the frame-rate readout.
//
// Shown only while the pause menu's "Debug stats" toggle is on (DebugStatsOverlay.Visible) and a career weekend
// is running, so it stays out of a player's way. Collapsed it is one line, WEEKEND; opened it gives:
//
//   SKIP QUEST  — the current objective (the session being driven, or the booking the marker points at) is
//                 credited as attended with a neutral outcome, and the next booking goes up behind it
//                 (WeekendDirector.DevSkipQuest).
//   JUMP TO     — every booking on the sheet, Friday morning to Sunday evening. Picking one moves the weekend
//                 straight there, forwards or back: everything before it counts as attended, everything from
//                 it on is reopened, nothing is charged, and the player lands back at the motorhome with it
//                 booked (WeekendDirector.DevJumpTo). END OF WEEKEND walks off the end of Sunday.
//
// Self-installing and kept across scene loads, like the readout it sits under. Host only in co-op: the guest
// does not own the weekend.
public class WeekendDebugPanel : MonoBehaviour
{
    static WeekendDebugPanel _instance;

    bool _open;
    string _status = "";
    Vector2 _scroll;
    GUIStyle _row;
    readonly List<WeekendActivity> _sorted = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (_instance != null) return;
        var go = new GameObject("WeekendDebugPanel");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<WeekendDebugPanel>();
    }

    void OnGUI()
    {
        if (!DebugStatsOverlay.Visible || !GameSession.CareerActive || Coop.IsGuest) return;
        var t = WeekendDirector.Timetable;
        if (t == null) return;

        GUI.depth = -20;   // over the race-side HUD, so the buttons take the click
        _row ??= new GUIStyle(PixelGUI.Data)
        {
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            clipping = TextClipping.Clip,
        };
        _row.hover.textColor = PixelGUI.Gold;

        float line = PixelGUI.DataLineH + PixelGUI.Px(2f);
        float pad = PixelGUI.Px(4f);
        var box = DebugStatsOverlay.BoxRect;
        float x = box.x;
        float y = box.yMax + PixelGUI.Px(4f);
        float w = Mathf.Max(box.width, PixelGUI.Px(200f));

        // Header: opens and closes the rest.
        var head = new Rect(x, y, w, line + pad * 2f);
        PixelGUI.Fill(head, new Color(0f, 0f, 0f, 0.6f));
        if (TouchTaps.Button(new Rect(x + pad, y + pad, w - pad * 2f, line), (_open ? "[-] " : "[+] ") + "WEEKEND", _row))
            _open = !_open;
        y = head.yMax;
        if (!_open) return;

        // Every booking in clock order, then the way off the end of Sunday.
        _sorted.Clear();
        foreach (var a in t.Activities) if (a != null) _sorted.Add(a);
        _sorted.Sort((a, b) => a.slot != b.slot ? a.slot.CompareTo(b.slot) : a.startMinute.CompareTo(b.startMinute));

        // Sized before anything is drawn, so the backing goes down first and the text lands on top of it.
        float bottom = Screen.height - Screen.safeArea.y - PixelGUI.Px(6f);
        int labelLines = string.IsNullOrEmpty(_status) ? 4 : 5;   // clock, now, SKIP, [status], jump to
        float listTop = y + pad + line * labelLines + PixelGUI.Px(2f);
        float listH = (_sorted.Count + 1) * line;
        float viewH = Mathf.Max(line * 3f, Mathf.Min(listH, bottom - listTop - pad));
        PixelGUI.Fill(new Rect(x, y, w, listTop + viewH + pad - y), new Color(0f, 0f, 0f, 0.6f));
        y += pad;

        // Where the weekend is, and what the marker is on.
        var pending = WeekendAppointment.Pending;
        Label(x + pad, ref y, w - pad * 2f, line,
              WeekendLedger.WeekendOver ? "WEEKEND OVER" : WeekendLedger.ClockText, PixelGUI.TextDim);
        Label(x + pad, ref y, w - pad * 2f, line,
              "NOW: " + (pending != null ? pending.title : "-"), PixelGUI.Text);

        var skip = new Rect(x + pad, y, w - pad * 2f, line);
        PixelGUI.Fill(skip, PixelGUI.PlateLight);
        if (TouchTaps.Button(skip, " > SKIP QUEST", _row))
            _status = WeekendDirector.DevSkipQuest();
        y += line + PixelGUI.Px(2f);

        if (!string.IsNullOrEmpty(_status))
            Label(x + pad, ref y, w - pad * 2f, line, _status, PixelGUI.Gold);

        Label(x + pad, ref y, w - pad * 2f, line, "JUMP TO:", PixelGUI.TextDim);

        var view = new Rect(x + pad, y, w - pad * 2f, viewH);
        float innerW = view.width - (listH > viewH ? PixelGUI.Px(12f) : 0f);
        _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, innerW, listH));
        float ry = 0f;
        WeekendActivity jump = null;
        bool jumpToEnd = false;
        foreach (var a in _sorted)
        {
            var old = GUI.contentColor;
            GUI.contentColor = RowColour(a, pending);
            if (TouchTaps.Button(new Rect(0f, ry, innerW, line), RowText(a, pending), _row)) jump = a;
            GUI.contentColor = old;
            ry += line;
        }
        if (TouchTaps.Button(new Rect(0f, ry, innerW, line), "  END OF WEEKEND", _row)) jumpToEnd = true;
        GUI.EndScrollView();

        // Acted on after the layout is closed: a jump reloads the scene and rebuilds the sheet being drawn.
        if (jump != null)
        {
            _status = "Jumped to " + jump.title + ".";
            WeekendDirector.DevJumpTo(jump);
        }
        else if (jumpToEnd)
        {
            _status = "Jumped to the end of the weekend.";
            WeekendDirector.DevJumpTo(null);
        }
    }

    static string RowText(WeekendActivity a, WeekendActivity pending)
    {
        string mark = pending != null && a.id == pending.id ? ">"
                    : WeekendLedger.IsDone(a.id) ? "x"
                    : WeekendLedger.IsMissed(a.id) ? "-"
                    : " ";
        return $"{mark} {WeekendSlots.ShortLabel(a.slot)} {WeekendSlots.Clock(a.startMinute)} {a.title}";
    }

    static Color RowColour(WeekendActivity a, WeekendActivity pending)
    {
        if (pending != null && a.id == pending.id) return PixelGUI.Gold;
        if (WeekendLedger.IsDone(a.id)) return PixelGUI.TextDisabled;
        if (WeekendLedger.IsMissed(a.id)) return PixelGUI.Danger;
        return PixelGUI.Text;
    }

    void Label(float x, ref float y, float w, float h, string text, Color colour)
    {
        var old = GUI.contentColor;
        GUI.contentColor = colour;
        GUI.Label(new Rect(x, y, w, h), text, _row);
        GUI.contentColor = old;
        y += h;
    }
}
