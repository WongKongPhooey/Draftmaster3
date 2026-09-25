using UnityEngine;
using UnityEngine.InputSystem;

// STATS — the numbers, on one tile: the three championships (POINTS) and the form guide (DRIVERS).
//
// These were two tiles until MESSAGES needed a slot. They are both "how is everybody doing", so they share
// one now, as two tabs along the top. Left/right (or A/D) swaps tab; so does clicking one. Each page is the
// app it used to be — PhoneChampionshipApp and PhoneDrivRApp still draw themselves and are simply no longer
// registered as tiles — so neither lost anything in the move.
//
// The tile's badge is the championships' own: results in from races the player did not drive.
public class PhoneStatsApp : PhoneApp
{
    public override string Id => "stats";
    public override string TileName => "STATS";
    public override string TileSubtitle => "Points & drivers";
    public override Color Accent => PixelGUI.Info;
    public override int Badge => _pages[0].Badge;

    readonly PhoneApp[] _pages = { new PhoneChampionshipApp(), new PhoneDrivRApp() };
    static readonly string[] TabNames = { "POINTS", "DRIVERS" };
    int _tab;

    // Which page is up. Public so a caller can open the phone straight onto the form guide.
    public int Tab
    {
        get => _tab;
        set
        {
            int next = Mathf.Clamp(value, 0, _pages.Length - 1);
            if (next == _tab) return;
            _tab = next;
            _pages[_tab].OnOpen();
            ScrollToTop();
        }
    }

    // Always lands on POINTS: the badge on the tile is about the championships, so that is what the player
    // came in to read.
    public override void OnOpen()
    {
        _tab = 0;
        _pages[0].OnOpen();
    }

    public override void HandleKeys(Keyboard kb)
    {
        if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Tab = _tab - 1;
        if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Tab = _tab + 1;
    }

    // D-pad / stick left and right, or the shoulder buttons.
    public override void HandlePad(Gamepad pad)
    {
        int step = PadInput.HorizontalStep();
        if (pad.leftShoulder.wasPressedThisFrame) step = -1;
        if (pad.rightShoulder.wasPressedThisFrame) step = 1;
        if (step != 0) Tab = _tab + step;
    }

    public override float Draw(float x, float y, float w)
    {
        float y0 = y;
        y += Tabs(x, y, w);
        y += _pages[_tab].Draw(x, y, w);
        return y - y0;
    }

    float Tabs(float x, float y, float w)
    {
        float h = RowH + PixelGUI.Px(2f);
        float gap = PixelGUI.Px(2f);
        float tw = (w - gap * (TabNames.Length - 1)) / TabNames.Length;

        for (int i = 0; i < TabNames.Length; i++)
        {
            var r = new Rect(x + i * (tw + gap), y, tw, h);
            bool on = i == _tab;
            if (on) PixelGUI.Fill(r, Accent);
            else Plate(r);
            PhoneStyles.Label(r, TabNames[i], PhoneStyles.Heading, on ? PixelGUI.Ink : PixelGUI.TextDim,
                              TextAnchor.MiddleCenter);
            if (Pressed(r)) Tab = i;
        }

        return h + PixelGUI.Px(4f);
    }
}
