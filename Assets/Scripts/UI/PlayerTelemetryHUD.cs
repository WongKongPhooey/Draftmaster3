using UnityEngine;

// Lightweight IMGUI overlay for the free-driven player car. Shows live handling telemetry from
// PlayerVehicleController: speed, body slip, yaw rate, front/rear slip angles, handling balance
// (understeer vs oversteer) and per-axle tyre wear. Drop on any GameObject in the race scene.
// Toggle with the bound key (default F3). Editor/dev aid — not part of the production race HUD.
public class PlayerTelemetryHUD : MonoBehaviour
{
    [Tooltip("Player car to read. Auto-found by type if left empty.")]
    public PlayerVehicleController target;
    [Tooltip("Start visible.")]
    public bool visible = true;
    [Tooltip("Key to toggle the overlay on/off.")]
    public KeyCode toggleKey = KeyCode.F3;
    [Tooltip("Slip angle (deg) treated as the limit for the colour ramp.")]
    public float slipLimitDeg = 8f;

    Texture2D _px;
    GUIStyle _label, _head;

    void Awake()
    {
        _px = new Texture2D(1, 1);
        _px.SetPixel(0, 0, Color.white);
        _px.Apply();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) visible = !visible;
        if (target == null) target = FindObjectOfType<PlayerVehicleController>();
    }

    void OnGUI()
    {
        if (!visible || target == null) return;

        if (_label == null)
        {
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _head = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        }

        float w = 240f, x = 12f, y = Screen.height - 232f, pad = 10f;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(x, y, w, 220f), _px);
        GUI.color = Color.white;

        float cx = x + pad, cy = y + pad, lw = w - pad * 2f;
        GUI.Label(new Rect(cx, cy, lw, 20f), "TELEMETRY", _head); cy += 24f;

        Line(cx, ref cy, lw, $"Speed   {target.SpeedMph,6:F0} mph", Color.white);
        Line(cx, ref cy, lw, $"Yaw     {target.YawRateDeg,6:F0} °/s", Color.white);
        Line(cx, ref cy, lw, $"Slip    {target.SlipAngleDeg,6:F1} °", SlipColor(target.SlipAngleDeg));
        cy += 4f;

        // Per-axle slip.
        Line(cx, ref cy, lw, $"Front α {target.SlipFrontDeg,6:F1} °", SlipColor(target.SlipFrontDeg));
        Line(cx, ref cy, lw, $"Rear  α {target.SlipRearDeg,6:F1} °", SlipColor(target.SlipRearDeg));

        // Balance verdict.
        float bal = target.HandlingBalanceDeg;
        string verdict = Mathf.Abs(bal) < 0.8f ? "NEUTRAL" : (bal > 0f ? "UNDERSTEER" : "OVERSTEER");
        Color bc = Mathf.Abs(bal) < 0.8f ? Color.green : (bal > 0f ? new Color(1f, 0.7f, 0.1f) : new Color(1f, 0.3f, 0.3f));
        Line(cx, ref cy, lw, $"Balance  {verdict}", bc);
        cy += 6f;

        // Wear bars.
        Bar(cx, ref cy, lw, "Front tyre", target.wearFront);
        Bar(cx, ref cy, lw, "Rear tyre", target.wearRear);
    }

    void Line(float x, ref float y, float w, string text, Color c)
    {
        var prev = _label.normal.textColor;
        _label.normal.textColor = c;
        GUI.Label(new Rect(x, y, w, 18f), text, _label);
        _label.normal.textColor = prev;
        y += 18f;
    }

    void Bar(float x, ref float y, float w, string label, float frac01)
    {
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, w, 16f), label, _label); y += 16f;
        float h = 10f;
        GUI.color = new Color(1f, 1f, 1f, 0.25f);
        GUI.DrawTexture(new Rect(x, y, w, h), _px);
        // green → red as wear climbs.
        GUI.color = Color.Lerp(new Color(0.2f, 1f, 0.2f), new Color(1f, 0.2f, 0.2f), Mathf.Clamp01(frac01));
        GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(frac01), h), _px);
        GUI.color = Color.white;
        y += h + 6f;
    }

    Color SlipColor(float deg)
    {
        float t = Mathf.Clamp01(Mathf.Abs(deg) / Mathf.Max(slipLimitDeg, 0.1f));
        return Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), t);
    }
}
