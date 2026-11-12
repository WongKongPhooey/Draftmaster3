using Draftmaster.Controls;
using Draftmaster.MasterDraft;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// MASTER DRAFT's Card Detail screen (the CarDetail overlay): what a card on the Card Collection grid opens, to the
// Draftmaster3_UI_Unity design handoff (Designs/Draftmaster3_UI_Unity, Layout/CardDetail_PC.json and
// Reference/CardDetail_PC@3x.png).
//
// A full-screen layer over the menu, laid out as the design draws it:
//  * Header bar — "◀ Esc" (closes; key g.close), "Card Detail", the coins on the right.
//  * The card, large, on the left: rarity plate and number, the livery on the hatched slot, driver, team / make and
//    carset, and at the foot the class — "Class D" and six pips, one per class climbed — where the design has
//    "LV 2" and its level pips. A car still to collect is the dark "?" card.
//  * Car Stats — four rows of ten pips (CarStats: PLACEHOLDER names and values). Gold pips are the stat now; green
//    pips and the green "+1" are what the next class adds ("Next Class" legend, the design's "Next Level").
//  * Trait — the car's DM2 driver type and a line about it (CarStats.TraitText, placeholder words).
//  * Upgrade — the design's parts panel in Master Draft's terms: duplicate cards become gears, the gears bar to the
//    next class, the coins it costs, and the button: "Need 41 more gears" / "Need 4,000 more coins" (off) or
//    "Upgrade to Class C" (key g.upgrade; R / pad North too).
//  * "Set as race car ▶" (key g.race) — makes it the profile's selected car, the one series and challenges enter
//    by default. Opening a card no longer selects it; this button does.
//
// Not taken from the design: the bottom tab bar (the mode keeps its own tabs) — its strip carries the status and
// help lines instead — and the packs count in the header.
public partial class MasterDraftUI
{
    static class DetailStyle
    {
        static Color Hex(uint rgba) => new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);

        public static readonly Color Panel = Hex(0x182136ff);
        public static readonly Color TraitPanel = Hex(0x101a30ff);
        public static readonly Color Label = Hex(0xb9ae9aff);   // stat names, panel copy, the off button's caption
        public static readonly Color Gold = Hex(0xe8b13cff);
        public static readonly Color Red = Hex(0xc4452fff);
    }

    const float DetailHeaderH = 30f, DetailFooterH = 32f, DetailBodyW = 640f, DetailBodyH = 298f;
    const float StatPipW = 8f, StatPipH = 11f, StatPipStep = 10f;
    const float ClassPipW = 6f, ClassPipH = 10f, ClassPipStep = 8f;

    RectTransform _detailLayer;
    bool _detailLayout;   // this Rebuild draws the Card Detail layer: the status and help lines ride on its footer

    // What the Card Detail screen shows, for EditMode tests (GarageScreenTests).
    public string GarageDetailClass { get; private set; }        // "Class D"; "" for a car not owned
    public string GarageDetailUpgradeCaption { get; private set; }
    public int[] GarageDetailStats { get; private set; } = new int[0];
    public int[] GarageDetailNextGains { get; private set; } = new int[0];

    // Make the open car the profile's race car (the one series and challenges enter by default). Owned cars only.
    public bool SetGarageRaceCar()
    {
        var p = P;
        var owned = p?.Car(_garageCar);
        if (owned == null) { SetStatus("You don't own this car yet."); return false; }
        if (p.selectedCar == owned.id) return true;
        p.selectedCar = owned.id;
        var def = Cars?.Get(owned.id);
        _lastFocusKey = "g.race";
        SaveTeam(def != null ? $"#{def.number} {def.driver} is your race car." : "Race car set.");
        return true;
    }

    void PrepareCarDetailLayer()
    {
        if (_detailLayer != null) Discard(_detailLayer.gameObject);
        _detailLayer = null;
        _detailLayout = _overlay == Overlay.CarDetail && P != null;
    }

    void BuildCarDetail()
    {
        var cars = Cars;
        var def = cars?.Get(_garageCar);
        if (def == null)
        {
            _overlay = Overlay.None;
            _detailLayout = false;
            PlaceStatusLine();
            BuildGarage();
            return;
        }
        var p = P;
        var owned = p.Car(def.id);

        var layer = new GameObject("CardDetail", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        layer.SetParent(_root, false);
        layer.SetAsLastSibling();
        MasterDraftCardView.Fill(layer, 0f);
        var screen = layer.GetComponent<Image>();
        screen.color = PackStyle.Screen;
        screen.raycastTarget = true;   // the menu underneath takes no clicks
        _detailLayer = layer;

        // The body: the design's 640 x 298 between the header and the footer, centred on a wider screen.
        var body = new GameObject("Body", typeof(RectTransform)).GetComponent<RectTransform>();
        body.SetParent(layer, false);
        body.anchorMin = body.anchorMax = body.pivot = new Vector2(0.5f, 1f);
        body.anchoredPosition = new Vector2(0f, -DetailHeaderH);
        body.sizeDelta = new Vector2(DetailBodyW, DetailBodyH);

        DetailBigCard(body, def, owned);
        DetailStats(body, def, owned);
        DetailTrait(body, def);
        DetailUpgrade(body, def, owned);

        Strip(layer, "Footer", false, DetailFooterH);
        DetailHeader(layer);   // last: its Esc button is the fallback focus, after Upgrade and Set As Race Car

        PlaceStatusLine();   // the status and help lines over the footer
    }

    // ------------------------------------------------------------------ header

    void DetailHeader(RectTransform layer)
    {
        var header = Strip(layer, "Header", true, DetailHeaderH);

        string back = InputGlyphs.UsingGamepad ? InputGlyphs.PadName(PadBindings.Back)
            : InputGlyphs.UsingTouch ? "Back" : "Esc";
        var caption = Small(header, back, PackStyle.Cream, Vector2.zero, new Vector2(80f, 10f), tracked: true);
        float textW = Mathf.Ceil(caption.GetPreferredValues(back).x);
        float w = 16f + textW + 6f;
        var esc = Plate(header, "Button_g.close", CollectionStyle.Edge, new Vector2(14f, -6f), new Vector2(w, 16f));
        Frame(esc, CollectionStyle.Shadow, 2f, 2f);
        for (int i = 0; i < 3; i++)   // ◀, in pixels (Silkscreen has no arrows)
            Plate(esc, "Arrow", PackStyle.Cream, new Vector2(6f + i, -8f + i), new Vector2(1f, 1f + 2f * i));
        caption.transform.SetParent(esc, false);
        TopLeft(caption.rectTransform, new Vector2(14f, -3f), new Vector2(textW + 2f, 10f));
        AddItem(esc, "g.close", CloseCarDetail);

        var title = Text(header, "Card Detail", IronOvalUI.Role.Header, PackStyle.Cream, new Vector2(14f + w + 12f, -6f), new Vector2(200f, 18f));
        title.name = "Title";

        var coins = Text(header, $"Coins  {P.coins:N0}", IronOvalUI.Role.Data, PackStyle.Gold, Vector2.zero, new Vector2(260f, 18f));
        coins.rectTransform.anchorMin = coins.rectTransform.anchorMax = coins.rectTransform.pivot = new Vector2(1f, 1f);
        coins.rectTransform.anchoredPosition = new Vector2(-14f, -7f);
        coins.alignment = TextAlignmentOptions.TopRight;
        coins.name = "Coins";
    }

    // ------------------------------------------------------------------ the card

    // The design's 150 x 278 card: rarity frame and plate, number, livery slot, name lines, class and pips.
    void DetailBigCard(RectTransform body, CarDef def, OwnedCar owned)
    {
        const float W = 150f, H = 278f, Inner = W - 16f;
        Color rarity = CollectionStyle.RarityColour(def.rarity);
        Plate(body, "CardShadow", CollectionStyle.Shadow, new Vector2(16f, -14f), new Vector2(W, H));
        var card = Plate(body, "Card", owned != null ? CollectionStyle.BodyColour(def.rarity) : CollectionStyle.LockedBody,
                         new Vector2(12f, -10f), new Vector2(W, H));
        Frame(card, owned != null ? rarity : CollectionStyle.Edge, 3f, 3f);

        // Rarity plate (ink on the rarity colour) and the number.
        string rarityName = CardCollection.RarityName(def.rarity);
        var plateText = Small(card, rarityName, CollectionStyle.Ink, Vector2.zero, new Vector2(100f, 10f), tracked: true);
        float plateW = Mathf.Ceil(plateText.GetPreferredValues(rarityName).x) + 8f;
        var plate = Plate(card, "Rarity", rarity, new Vector2(8f, -10f), new Vector2(plateW, 12f));
        plateText.transform.SetParent(plate, false);
        TopLeft(plateText.rectTransform, new Vector2(5f, -1f), new Vector2(plateW, 10f));
        var number = Text(card, "#" + def.number, IronOvalUI.Role.Data, CollectionStyle.Number, new Vector2(8f, -9f), new Vector2(Inner, 16f));
        number.alignment = TextAlignmentOptions.TopRight;
        number.name = "Number";

        // The livery on the hatched slot in a 2 px frame; a car still to collect shows the "?".
        var slot = Plate(card, "Slot", owned != null ? Color.white : CollectionStyle.LockedHatch, new Vector2(8f, -30f), new Vector2(Inner, 84f));
        Hatch(slot, owned != null ? Color.white : CollectionStyle.LockedHatch);
        Frame(slot, CollectionStyle.Edge, 2f, 2f);
        var sprite = owned != null ? Livery(def) : null;
        if (sprite != null)
        {
            var livery = Plate(slot, "Livery", Color.white, new Vector2(3f, -10f), new Vector2(128f, 64f)).GetComponent<Image>();
            livery.sprite = sprite;
            livery.preserveAspect = true;
        }
        else if (owned != null)
        {
            var none = Small(slot, "[ Car ]", CollectionStyle.Meta, new Vector2(0f, -37f), new Vector2(Inner, 10f));
            none.alignment = TextAlignmentOptions.Top;
        }
        else
        {
            var mark = Text(slot, "?", IronOvalUI.Role.BodyLarge, CollectionStyle.LockedMark, new Vector2(0f, -18f), new Vector2(Inner, 50f));
            mark.alignment = TextAlignmentOptions.Top;
        }

        var name = Text(card, def.driver ?? def.id, IronOvalUI.Role.Data, CollectionStyle.Cream, new Vector2(8f, -121f), new Vector2(Inner, 16f));
        name.name = "Name";
        name.overflowMode = TextOverflowModes.Ellipsis;
        Small(card, $"{def.team} - {def.manufacturer}", CollectionStyle.Meta, new Vector2(8f, -141f), new Vector2(Inner, 10f), tracked: true)
            .overflowMode = TextOverflowModes.Ellipsis;
        Small(card, SetName(def), CollectionStyle.Meta, new Vector2(8f, -154f), new Vector2(Inner, 10f), tracked: true)
            .overflowMode = TextOverflowModes.Ellipsis;

        if (owned == null)
        {
            GarageDetailClass = "";
            Small(card, "Not collected", CollectionStyle.Meta, new Vector2(8f, -258f), new Vector2(Inner, 10f), tracked: true);
            return;
        }

        // "Class D" where the design has "LV 2", and a pip per class climbed (0-6) where it has the level pips.
        string cls = "Class " + CardCollection.ClassLetter(owned.carClass);
        GarageDetailClass = cls;
        var classText = Text(card, cls, IronOvalUI.Role.Display, CollectionStyle.Cream, new Vector2(8f, -246f), new Vector2(100f, 26f));
        classText.fontSize = IronOvalUI.Snap(classText.font, 22);   // the design's 22 px on Pixelify's 20 grid
        classText.name = "Class";
        float classW = Mathf.Ceil(classText.GetPreferredValues(cls).x);
        var pips = new GameObject("ClassPips", typeof(RectTransform)).GetComponent<RectTransform>();
        pips.SetParent(card, false);
        float pipsX = Mathf.Min(8f + classW + 6f, W - 8f - (CarProgression.MaxClass - 1) * ClassPipStep - ClassPipW);
        TopLeft(pips, new Vector2(pipsX, -254f), new Vector2(CarProgression.MaxClass * ClassPipStep, ClassPipH));
        int climbed = CarProgression.ClampClass(owned.carClass);
        for (int i = 0; i < CarProgression.MaxClass; i++)
            Plate(pips, "Pip", i < climbed ? DetailStyle.Gold : CollectionStyle.Edge, new Vector2(i * ClassPipStep, 0f), new Vector2(ClassPipW, ClassPipH));
    }

    // ------------------------------------------------------------------ car stats

    void DetailStats(RectTransform body, CarDef def, OwnedCar owned)
    {
        const float W = 274f, H = 225f, Row = 246f;
        var panel = Plate(body, "Stats", DetailStyle.Panel, new Vector2(172f, -10f), new Vector2(W, H));
        Frame(panel, CollectionStyle.Cream, 3f, 3f);
        Text(panel, "Car Stats", IronOvalUI.Role.Data, DetailStyle.Gold, new Vector2(14f, -11f), new Vector2(120f, 16f)).name = "Head";

        var lines = CarStats.Lines(def, owned);
        GarageDetailStats = new int[lines.Length];
        GarageDetailNextGains = new int[lines.Length];
        bool anyGain = false;
        for (int s = 0; s < lines.Length; s++)
        {
            var line = lines[s];
            GarageDetailStats[s] = line.value;
            GarageDetailNextGains[s] = line.nextGain;
            anyGain |= line.nextGain > 0;

            var row = new GameObject("Stat_" + line.name, typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(panel, false);
            TopLeft(row, new Vector2(14f, -34f - s * 24f), new Vector2(Row, 16f));
            Text(row, line.name, IronOvalUI.Role.Data, DetailStyle.Label, new Vector2(0f, -1f), new Vector2(88f, 16f)).name = "Name";

            // Ten pips: gold to the value, green for what the next class adds, dark for the rest.
            var bar = new GameObject("Pips", typeof(RectTransform)).GetComponent<RectTransform>();
            bar.SetParent(row, false);
            TopLeft(bar, new Vector2(90f, -2f), new Vector2(CarStats.MaxPips * StatPipStep - 2f, StatPipH));
            for (int i = 0; i < CarStats.MaxPips; i++)
            {
                Color c = i < line.value ? DetailStyle.Gold
                    : i < line.value + line.nextGain ? CollectionStyle.Ready
                    : CollectionStyle.Edge;
                Plate(bar, "Pip", c, new Vector2(i * StatPipStep, 0f), new Vector2(StatPipW, StatPipH));
            }

            Text(row, line.value.ToString(), IronOvalUI.Role.Data, CollectionStyle.Cream, new Vector2(198f, -1f), new Vector2(16f, 16f)).name = "Value";
            if (line.nextGain > 0)
                Text(row, "+" + line.nextGain, IronOvalUI.Role.Data, CollectionStyle.Ready, new Vector2(215f, -1f), new Vector2(30f, 16f)).name = "Gain";
        }

        if (anyGain)
        {
            // "■ Next Class" legend, right-aligned on the heading row (the square drawn in pixels).
            const string legend = "Next Class";
            var label = Small(panel, legend, CollectionStyle.Ready, Vector2.zero, new Vector2(100f, 10f));
            float w = Mathf.Ceil(label.GetPreferredValues(legend).x);
            float x = W - 14f - w;
            TopLeft(label.rectTransform, new Vector2(x, -14f), new Vector2(w + 2f, 10f));
            label.name = "Legend";
            Plate(panel, "LegendPip", CollectionStyle.Ready, new Vector2(x - 9f, -15f), new Vector2(6f, 6f));
        }
    }

    // ------------------------------------------------------------------ trait

    void DetailTrait(RectTransform body, CarDef def)
    {
        const float W = 274f, H = 45f;
        var panel = Plate(body, "Trait", DetailStyle.TraitPanel, new Vector2(172f, -(DetailBodyH - 10f - H)), new Vector2(W, H));
        Frame(panel, DetailStyle.Gold, 3f, 3f);
        var head = Small(panel, "Trait - " + CarStats.TraitName(def), DetailStyle.Gold, new Vector2(12f, -7f), new Vector2(W - 24f, 10f), tracked: true);
        head.characterSpacing = 25f;   // the design's 2 px tracking
        head.name = "Head";
        var text = Text(panel, CarStats.TraitText(def), IronOvalUI.Role.Data, CollectionStyle.Cream, new Vector2(12f, -21f), new Vector2(W - 24f, 16f));
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.name = "Text";
    }

    // ------------------------------------------------------------------ upgrade

    void DetailUpgrade(RectTransform body, CarDef def, OwnedCar owned)
    {
        const float W = 172f, H = 242f, Inner = W - 24f;
        var p = P;
        var panel = Plate(body, "Upgrade", DetailStyle.Panel, new Vector2(456f, -10f), new Vector2(W, H));
        Frame(panel, CollectionStyle.Cream, 3f, 3f);
        Text(panel, "Upgrade", IronOvalUI.Role.Data, DetailStyle.Gold, new Vector2(12f, -11f), new Vector2(Inner, 16f)).name = "Head";

        if (owned == null)
        {
            GarageDetailUpgradeCaption = "";
            Text(panel, "Not collected yet. Win it in a challenge or series to add it to your garage.", IronOvalUI.Role.Data,
                 DetailStyle.Label, new Vector2(12f, -35f), new Vector2(Inner, 120f), wrap: true).name = "Copy";
            return;
        }

        bool max = CarProgression.IsMaxClass(owned);
        var progress = CardCollection.Progress(owned, p.coins);
        Text(panel, max ? "Top class. Gears still bank on this car."
                        : "Duplicate cards become gears. Fill the bar to class up this car.",
             IronOvalUI.Role.Data, DetailStyle.Label, new Vector2(12f, -35f), new Vector2(Inner, 72f), wrap: true).name = "Copy";

        // Gears  9 / 50
        Text(panel, "Gears", IronOvalUI.Role.Data, DetailStyle.Label, new Vector2(12f, -113f), new Vector2(60f, 16f));
        var gears = Text(panel, max ? owned.gears.ToString("N0") : $"{progress.gears} / {progress.target}", IronOvalUI.Role.Data,
                         CollectionStyle.Cream, new Vector2(12f, -113f), new Vector2(Inner, 16f));
        gears.alignment = TextAlignmentOptions.TopRight;
        gears.name = "Gears";

        var bar = Plate(panel, "GearsBar", CollectionStyle.Edge, new Vector2(12f, -135f), new Vector2(Inner, 12f));
        Frame(bar, CollectionStyle.Shadow, 2f, 2f);
        float fill = progress.ready ? 1f : progress.fill;
        if (fill > 0f)
            Plate(bar, "Fill", progress.ready ? CollectionStyle.Ready : DetailStyle.Gold, Vector2.zero, new Vector2(Mathf.Round(Inner * fill), 12f));

        // Cost  10,000 coins
        int cost = CarProgression.UpgradeCost(owned.carClass);
        Text(panel, "Cost", IronOvalUI.Role.Data, DetailStyle.Label, new Vector2(12f, -155f), new Vector2(60f, 16f));
        var costText = Text(panel, max ? "-" : cost == 0 ? "Free" : $"{cost:N0} coins", IronOvalUI.Role.Data, DetailStyle.Gold,
                            new Vector2(12f, -155f), new Vector2(Inner, 16f));
        costText.alignment = TextAlignmentOptions.TopRight;
        costText.name = "Cost";

        // The upgrade button: what's still missing (off), or the class it buys.
        var check = CarProgression.CanUpgrade(owned, p.coins);
        string letter = CardCollection.ClassLetter(owned.carClass);
        string next = max ? letter : CardCollection.ClassLetter(owned.carClass + 1);
        string caption = check switch
        {
            UpgradeCheck.Ok => next == letter ? "Upgrade" : $"Upgrade to Class {next}",
            UpgradeCheck.NeedGears => $"Need {CarProgression.GearsToNextClass(owned)} more gears",
            UpgradeCheck.NeedCoins => $"Need {cost - p.coins:N0} more coins",
            _ => "Top class",
        };
        GarageDetailUpgradeCaption = caption;
        bool ok = check == UpgradeCheck.Ok;
        DetailButton(panel, "g.upgrade", caption, new Vector2(12f, -196f), new Vector2(Inner, 36f),
                     ok ? CollectionStyle.Ready : CollectionStyle.Edge, ok ? CollectionStyle.Ink : DetailStyle.Label,
                     () => UpgradeSelectedCar(), ok, arrow: false);

        // Set As Race Car, under the panel.
        bool isRaceCar = p.selectedCar == owned.id;
        DetailButton(body, "g.race", isRaceCar ? "Your race car" : "Set as race car", new Vector2(456f, -260f), new Vector2(W, 28f),
                     isRaceCar ? CollectionStyle.Edge : DetailStyle.Red, isRaceCar ? DetailStyle.Label : CollectionStyle.Cream,
                     () => SetGarageRaceCar(), !isRaceCar, arrow: !isRaceCar);
    }

    // A design button: drop shadow 4 px down-right, the plate in a 3 px dark frame, a centred tracked Silkscreen
    // caption (two lines if it has to), and an optional ▶ after it drawn in pixels.
    void DetailButton(RectTransform parent, string key, string caption, Vector2 pos, Vector2 size, Color plate, Color text,
                      System.Action confirm, bool enabled, bool arrow)
    {
        Plate(parent, "Shadow_" + key, CollectionStyle.Shadow, pos + new Vector2(4f, -4f), size);
        var rt = Plate(parent, "Button_" + key, plate, pos, size);
        Frame(rt, CollectionStyle.Shadow, 3f, 3f);
        var label = Small(rt, caption, text, Vector2.zero, size, tracked: true);
        label.name = "Caption";
        float arrowRoom = arrow ? 8f : 0f;
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(8f, 0f);
        lrt.offsetMax = new Vector2(-8f - arrowRoom, 0f);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        if (arrow)
        {
            float textW = Mathf.Min(size.x - 16f - arrowRoom, Mathf.Ceil(label.GetPreferredValues(caption).x));
            float x = Mathf.Round(8f + (size.x - 16f - arrowRoom + textW) / 2f + 4f);
            float y = Mathf.Round(-(size.y - 7f) / 2f);
            for (int i = 0; i < 4; i++)   // ▶
                Plate(rt, "Arrow", text, new Vector2(x + i, y - i), new Vector2(1f, 7f - 2f * i));
        }
        AddItem(rt, key, confirm, enabled);
        label.color = text;   // AddItem greys an off button's text; the design keeps its own colour for it
    }
}
