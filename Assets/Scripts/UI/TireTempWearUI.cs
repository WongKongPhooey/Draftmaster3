using UnityEngine;

// On-screen 2×2 tyre readout for the player's car: each corner shows its temperature and the life left in
// the tyre (100 fresh, counting down to 0).
//
// Drawn with the Iron Oval kit. Temperature is a continuous bar because it is a continuous reading, and
// life is a ten-cell segmented bar because that is a quantity the driver counts rather than reads — the
// same split the kit's own HUD block makes. Colour carries meaning, not decoration: telemetry blue while
// the tyre is cold, gain green in the window, alarm red once it is over temperature or nearly worn out.
public class TireTempWearUI : MonoBehaviour
{
    [Tooltip("Tyre model to display. Auto-found from the player's car if left empty.")]
    public TireModel tires;
    public bool autoFindPlayer = true;
    [Tooltip("Key toggling the tyre readout (iRacing-style F6).")]
    public KeyCode toggleKey = KeyCode.F6;
    public bool visible = true;

    [Header("Layout (UI pixels, before PixelGUI.Scale)")]
    public float cellW = 62f;
    public float cellH = 34f;
    public float gap = 4f;
    public Vector2 margin = new Vector2(10f, 14f);

    void Update()
    {
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey)) visible = !visible;
        if (tires == null && autoFindPlayer) tires = FindPlayerTires();
    }

    static TireModel FindPlayerTires()
    {
        var all = Object.FindObjectsByType<PlayerVehicleController>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i].GetComponent<SplineInputDriver>() == null) // the human car has no AI input driver
            {
                var tm = all[i].GetComponent<TireModel>();
                if (tm != null) return tm;
            }
        return null;
    }

    void OnGUI()
    {
        if (!visible || tires == null) return;

        float cw = PixelGUI.Px(cellW), ch = PixelGUI.Px(cellH), g = PixelGUI.Px(gap);
        float pad = PixelGUI.Px(6f);
        float boardW = cw * 2f + g, boardH = ch * 2f + g;
        float x0 = PixelGUI.Px(margin.x) + pad;
        float y0 = Screen.height - boardH - PixelGUI.Px(margin.y) - pad;

        PixelGUI.Panel(new Rect(x0 - pad, y0 - pad, boardW + pad * 2f, boardH + pad * 2f));

        DrawTyre("FL", TireModel.FL, x0, y0, cw, ch);
        DrawTyre("FR", TireModel.FR, x0 + cw + g, y0, cw, ch);
        DrawTyre("RL", TireModel.RL, x0, y0 + ch + g, cw, ch);
        DrawTyre("RR", TireModel.RR, x0 + cw + g, y0 + ch + g, cw, ch);
    }

    void DrawTyre(string label, int i, float x, float y, float w, float h)
    {
        float t = tires.tempC[i];
        float life = 1f - Mathf.Clamp01(tires.wear[i]);

        float line = PixelGUI.LineH;
        // Corner and temperature on one line of the label face, then the temperature bar, then the life
        // cells. The line height comes from the face so a bigger cell moves the bar down with it.
        GUI.Label(new Rect(x, y, w, line), label, PixelGUI.Label);
        var tempLabel = PixelGUI.Data;
        var prevAlign = tempLabel.alignment;
        tempLabel.alignment = TextAnchor.UpperRight;
        GUI.Label(new Rect(x, y, w, line), $"{t:F0}°", tempLabel);
        tempLabel.alignment = prevAlign;

        PixelGUI.Bar(new Rect(x, y + line, w, PixelGUI.Px(5f)), TempFill(t), TempColour(t));

        // Ten cells of life, red once a third of the tyre is gone — the point at which the lap time is
        // already going away, rather than the point at which the tyre is finished.
        int cells = Mathf.CeilToInt(life * 10f);
        var wearColour = life > 0.66f ? PixelGUI.Confirm : life > 0.33f ? PixelGUI.Gold : PixelGUI.Danger;
        PixelGUI.Cells(new Rect(x, y + line + PixelGUI.Px(7f), w, PixelGUI.CellsHeight), cells, 10, wearColour);
    }

    // How full the temperature bar reads: empty at cold, full at the overheat threshold.
    float TempFill(float t) => Mathf.Clamp01(Mathf.InverseLerp(tires.coldC, tires.overheatC, t));

    Color TempColour(float t)
    {
        if (t < tires.optimalC)
        {
            // Cold to in-window. Stepped rather than a gradient: the kit's palette has three states here
            // and a continuous blend would land on colours that are in neither.
            return Mathf.InverseLerp(tires.coldC, tires.optimalC, t) > 0.75f ? PixelGUI.Confirm : PixelGUI.Info;
        }
        return Mathf.InverseLerp(tires.optimalC, tires.overheatC, t) > 0.75f ? PixelGUI.Danger : PixelGUI.Confirm;
    }
}
