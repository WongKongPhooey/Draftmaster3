using UnityEngine;

// The pit box — the tall cart parked on the wall above each team's box, with the crew chief on top of it,
// the timing screens under the awning and the tyres stacked behind. On a stock-car pit road it is the thing
// that tells you whose box you are looking at, because it is painted in the car's colours.
//
// Drawn from above: a dark cart body, a canopy in the car's PRIMARY colour, a stripe in its SECONDARY, and
// the car number on the roof. Placeholder geometry — quads, like the rest of the paddock props — so it
// reads correctly now and can be swapped for a sprite later without moving anything.
//
// Built one per pit box by PitCrewSpawner, on the wall side of the box. The colours come from CarColours,
// keyed by whichever car is assigned that box; the car may not exist yet when the stand is built (the grid
// spawns over several frames, and the field is re-parked into fitted boxes after that), so the stand keeps
// looking for its car for a few seconds and paints itself the moment it knows.
public class PitBoxStand : MonoBehaviour
{
    [Tooltip("Pit box this stand overlooks. Matches the car's grid slot / qualifying position.")]
    public int boxIndex;

    [Tooltip("How long (s) to keep looking for the car assigned to this box before settling for grey.")]
    public float resolveWindow = 8f;

    // Cart, in metres, seen from above. A real one is about 2.4 m long, 1.5 m wide and taller than a person;
    // from overhead only the footprint and the canopy read, so that is what is drawn.
    public const float LengthM = 2.4f;
    public const float WidthM = 1.5f;

    MeshRenderer _canopy, _stripe;
    Material _canopyMat, _stripeMat;
    TextMesh _number;
    float _giveUpAt;
    bool _painted;

    // Build the whole cart under `parent`, in the pit box's local frame: +Y along the lane, +X toward the
    // wall. `lateral` is how far out onto the wall the cart sits.
    public static PitBoxStand Build(Transform parent, int boxIndex, float lateral, float localZ)
    {
        var go = new GameObject($"PitBoxStand_{boxIndex}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(lateral, 0f, localZ);

        var stand = go.AddComponent<PitBoxStand>();
        stand.boxIndex = boxIndex;
        stand.BuildParts();
        return stand;
    }

    void BuildParts()
    {
        // Cart body: dark, and slightly bigger than the canopy so it reads as a chassis under it.
        var body = PaddockProps.Unlit(new Color(0.13f, 0.14f, 0.17f));
        PaddockProps.Quad(transform, "Cart", Vector2.zero, new Vector2(WidthM, LengthM), 0f, body);

        // Canopy: the car's primary. This is the bit you see from the grandstand.
        _canopyMat = PaddockProps.Unlit(Color.white);
        _canopy = PaddockProps.Quad(transform, "Canopy", Vector2.zero,
                                    new Vector2(WidthM - 0.22f, LengthM - 0.3f), -0.01f, _canopyMat)
                               .GetComponent<MeshRenderer>();

        // Stripe: the secondary, across the front of the canopy where a real one carries the team name.
        _stripeMat = PaddockProps.Unlit(new Color(0.6f, 0.6f, 0.6f));
        _stripe = PaddockProps.Quad(transform, "Stripe", new Vector2(0f, LengthM * 0.5f - 0.42f),
                                    new Vector2(WidthM - 0.22f, 0.34f), -0.02f, _stripeMat)
                              .GetComponent<MeshRenderer>();

        // Tyre stack behind the cart: two dark discs' worth of rectangle, enough to read as rubber.
        var rubber = PaddockProps.Unlit(new Color(0.09f, 0.09f, 0.1f));
        PaddockProps.Quad(transform, "Tyres", new Vector2(0f, -LengthM * 0.5f - 0.45f),
                          new Vector2(WidthM - 0.5f, 0.7f), -0.01f, rubber);

        _giveUpAt = Time.time + resolveWindow;
        Repaint(Color.white, new Color(0.55f, 0.55f, 0.6f), -1);
    }

    void Update()
    {
        if (_painted) { enabled = false; return; }
        if (Time.time > _giveUpAt) { enabled = false; return; }

        var label = FindCarLabel();
        if (label == null) return;

        CarColours.For(label, out Color primary, out Color secondary);
        Repaint(primary, secondary, label.carNumber);
        _painted = true;
        enabled = false;
    }

    // Whoever is racing out of this box. The search is shared with the crew working the same box, who are
    // asking the identical question at the identical moment (PitBoxCars).
    DriverLabel FindCarLabel() => PitBoxCars.Label(boxIndex);

    public void Repaint(Color primary, Color secondary, int carNumber)
    {
        SetColour(_canopyMat, primary);
        SetColour(_stripeMat, secondary);

        if (carNumber >= 0) EnsureNumber(carNumber, primary);
        else if (_number != null) _number.gameObject.SetActive(false);
    }

    static void SetColour(Material m, Color c)
    {
        if (m == null) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    // The number on the roof, in whichever of black or white actually reads against the canopy.
    void EnsureNumber(int carNumber, Color canopy)
    {
        if (_number == null)
        {
            var go = new GameObject("Number");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, -0.28f, -0.03f);
            go.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);   // read along the lane, like the box lines

            _number = go.AddComponent<TextMesh>();
            _number.anchor = TextAnchor.MiddleCenter;
            _number.alignment = TextAlignment.Center;
            _number.fontSize = 64;
            _number.characterSize = 0.02f;
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        _number.gameObject.SetActive(true);
        _number.text = carNumber.ToString();
        float luma = 0.299f * canopy.r + 0.587f * canopy.g + 0.114f * canopy.b;
        _number.color = luma > 0.55f ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
    }
}
