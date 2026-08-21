using UnityEngine;

// Lightweight IMGUI overlay for the free-driven player car. Shows live handling telemetry from
// PlayerVehicleController: speed, body slip, yaw rate, front/rear slip angles, handling balance
// (understeer vs oversteer) and per-axle tyre wear. Drop on any GameObject in the race scene.
// Toggle with the bound key. Editor/dev aid — not part of the production race HUD.
//
// Drawn with the Iron Oval kit: framed plate, VT323 columns (its fixed advance is what keeps the numbers
// from dancing as they change), and the palette's alarm red reserved for a reading actually at the limit.
public class PlayerTelemetryHUD : MonoBehaviour
{
    [Tooltip("Player car to read. Auto-found by type if left empty.")]
    public PlayerVehicleController target;
    [Tooltip("Start visible.")]
    public bool visible = true;
    [Tooltip("Key to toggle the overlay on/off.")]
    public KeyCode toggleKey = KeyCode.F7;   // F3 is the TEAM box (iRacing layout)
    [Tooltip("Slip angle (deg) treated as the limit for the colour ramp.")]
    public float slipLimitDeg = 8f;

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) visible = !visible;
        if (target == null) target = FindFirstObjectByType<PlayerVehicleController>();
    }

    void OnGUI()
    {
        if (!visible || target == null) return;

        // Height is the sum of what goes in it — heading, rule, six data lines, two labelled wear
        // rows — so a face on a bigger cell grows the panel instead of spilling out of it.
        float w = PixelGUI.Px(150f);
        float h = PixelGUI.Px(16f) + PixelGUI.LineH + PixelGUI.Px(5f) + 6f * PixelGUI.DataLineH
                  + PixelGUI.Px(2f) + 2f * (PixelGUI.LineH + PixelGUI.CellsHeight + PixelGUI.Px(3f));
        float x = PixelGUI.Px(8f), y = Screen.height - h - PixelGUI.Px(14f);
        PixelGUI.Panel(new Rect(x, y, w, h));

        var c = PixelGUI.PanelContent(new Rect(x, y, w, h), 8f);
        float cy = c.y;
        float line = PixelGUI.DataLineH;

        GUI.Label(new Rect(c.x, cy, c.width, PixelGUI.LineH), "TELEMETRY", PixelGUI.HeadingSmall);
        cy += PixelGUI.LineH;
        PixelGUI.Rule(c.x, cy, c.width);
        cy += PixelGUI.Px(3f);

        Line(c.x, ref cy, c.width, line, $"SPD  {target.SpeedMph,5:F0} MPH", PixelGUI.Text);
        Line(c.x, ref cy, c.width, line, $"YAW  {target.YawRateDeg,5:F0} d/s", PixelGUI.Text);
        Line(c.x, ref cy, c.width, line, $"SLIP {target.SlipAngleDeg,5:F1} d", SlipColour(target.SlipAngleDeg));
        Line(c.x, ref cy, c.width, line, $"FRNT {target.SlipFrontDeg,5:F1} d", SlipColour(target.SlipFrontDeg));
        Line(c.x, ref cy, c.width, line, $"REAR {target.SlipRearDeg,5:F1} d", SlipColour(target.SlipRearDeg));

        // Balance verdict. Understeer is the accent, oversteer the alarm: one is a lap time, the other is
        // about to be a wall.
        float bal = target.HandlingBalanceDeg;
        string verdict = Mathf.Abs(bal) < 0.8f ? "NEUTRAL" : (bal > 0f ? "UNDERSTEER" : "OVERSTEER");
        Color bc = Mathf.Abs(bal) < 0.8f ? PixelGUI.Confirm : (bal > 0f ? PixelGUI.Gold : PixelGUI.Danger);
        Line(c.x, ref cy, c.width, line, $"BAL  {verdict}", bc);
        cy += PixelGUI.Px(2f);

        Wear(c.x, ref cy, "FRONT TYRE", target.wearFront);
        Wear(c.x, ref cy, "REAR TYRE", target.wearRear);
    }

    void Line(float x, ref float y, float w, float h, string text, Color colour)
    {
        var style = PixelGUI.Data;
        var prev = style.normal.textColor;
        style.normal.textColor = colour;
        GUI.Label(new Rect(x, y, w, h), text, style);
        style.normal.textColor = prev;
        y += h;
    }

    // Wear as cells rather than a continuous bar: the driver counts these down, and ten steps is as fine
    // as the reading is actually trusted to be.
    void Wear(float x, ref float y, string label, float wear01)
    {
        GUI.Label(new Rect(x, y, PixelGUI.CellsWidth(10), PixelGUI.LineH), label, PixelGUI.Label);
        y += PixelGUI.LineH;
        float life = 1f - Mathf.Clamp01(wear01);
        var colour = life > 0.66f ? PixelGUI.Confirm : life > 0.33f ? PixelGUI.Gold : PixelGUI.Danger;
        PixelGUI.Cells(new Rect(x, y, PixelGUI.CellsWidth(10), PixelGUI.CellsHeight),
                       Mathf.CeilToInt(life * 10f), 10, colour);
        y += PixelGUI.CellsHeight + PixelGUI.Px(3f);
    }

    // White until the tyre is genuinely working, then alarm red. No ramp through pink in between: the kit
    // treats red as a state, not a gradient.
    Color SlipColour(float deg) =>
        Mathf.Abs(deg) / Mathf.Max(slipLimitDeg, 0.1f) > 0.8f ? PixelGUI.Danger : PixelGUI.Text;
}
