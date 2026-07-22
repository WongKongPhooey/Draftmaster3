using UnityEngine;

// On-screen 2×2 tyre readout for the player's car: each corner shows its temperature (background colour:
// blue = cold, green = optimal, red = overheating) and its wear (dark fill rising from the bottom;
// the % is tyre life remaining — 100 fresh, counting down to 0).
public class TireTempWearUI : MonoBehaviour
{
    [Tooltip("Tyre model to display. Auto-found from the player's car if left empty.")]
    public TireModel tires;
    public bool autoFindPlayer = true;
    [Tooltip("Key toggling the tyre readout (iRacing-style F6).")]
    public KeyCode toggleKey = KeyCode.F6;
    public bool visible = true;

    [Header("Layout")]
    public float cellW = 78f;
    public float cellH = 56f;
    public float gap = 6f;
    public Vector2 margin = new Vector2(20f, 30f);

    static Texture2D _tex;
    GUIStyle _style;

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
        if (_tex == null) { _tex = new Texture2D(1, 1); _tex.SetPixel(0, 0, Color.white); _tex.Apply(); }
        if (_style == null)
            _style = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

        float x0 = margin.x;
        float y0 = Screen.height - 2f * cellH - gap - margin.y;

        DrawTyre("FL", TireModel.FL, x0, y0);
        DrawTyre("FR", TireModel.FR, x0 + cellW + gap, y0);
        DrawTyre("RL", TireModel.RL, x0, y0 + cellH + gap);
        DrawTyre("RR", TireModel.RR, x0 + cellW + gap, y0 + cellH + gap);
    }

    void DrawTyre(string label, int i, float x, float y)
    {
        float t = tires.tempC[i];
        float w = Mathf.Clamp01(tires.wear[i]);

        Fill(new Rect(x, y, cellW, cellH), TempColor(t));                          // temperature background
        Fill(new Rect(x, y + cellH * (1f - w), cellW, cellH * w), new Color(0f, 0f, 0f, 0.45f)); // wear fill
        Outline(new Rect(x, y, cellW, cellH), new Color(0f, 0f, 0f, 0.8f));

        GUI.Label(new Rect(x, y, cellW, cellH), $"{label}\n{t:F0}°C\n{(1f - w) * 100f:F0}%", _style);
    }

    Color TempColor(float t)
    {
        if (t < tires.optimalC)
            return Color.Lerp(new Color(0.30f, 0.55f, 1f), new Color(0.25f, 0.9f, 0.35f),
                Mathf.Clamp01(Mathf.InverseLerp(tires.coldC, tires.optimalC, t)));
        return Color.Lerp(new Color(0.25f, 0.9f, 0.35f), new Color(1f, 0.25f, 0.18f),
            Mathf.Clamp01(Mathf.InverseLerp(tires.optimalC, tires.overheatC, t)));
    }

    static void Fill(Rect r, Color c)
    {
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, _tex);
        GUI.color = prev;
    }

    static void Outline(Rect r, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, 1f), c);
        Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
        Fill(new Rect(r.x, r.y, 1f, r.height), c);
        Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
    }
}
