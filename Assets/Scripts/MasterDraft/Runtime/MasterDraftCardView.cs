using Draftmaster.MasterDraft;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Draws a card procedurally on the 640x360 kit canvas: a frame in the rarity's colour, the OVR, the type, the
// name and the stats that matter for that type. No art is needed.
//
// ART HOOK: if a sprite exists at Resources/MasterDraft/Cards/<artKey> (CardDefinition.artKey, e.g.
// "driver_12", "car_FRD_2", "action_PushToPass"), it is drawn in the portrait box behind the name. Drop art in
// and it appears; nothing else changes.
public static class MasterDraftCardView
{
    public const string ArtFolder = "MasterDraft/Cards/";

    public static readonly Color EpicPurple = new Color32(0xa0, 0x6c, 0xe8, 0xff);

    public static Color RarityColour(Rarity r)
    {
        var t = PixelUITheme.Instance;
        switch (r)
        {
            case Rarity.Legendary: return t != null ? t.gold : new Color(0.95f, 0.76f, 0.3f);
            case Rarity.Epic: return EpicPurple;
            case Rarity.Rare: return t != null ? t.info : new Color(0.29f, 0.56f, 0.88f);
            default: return t != null ? t.textDim : Color.grey;
        }
    }

    public static string TypeShort(CardType t) => t switch
    {
        CardType.Driver => "DRV",
        CardType.Car => "CAR",
        CardType.CrewChief => "CC",
        _ => "ACT",
    };

    // The two stat lines a card face has room for.
    public static string[] StatLines(CardDefinition d)
    {
        switch (d.type)
        {
            case CardType.Driver:
            {
                // The driver's two best track types, then qualifying pace.
                TrackKind best = TrackKind.ShortTrack, second = TrackKind.Speedway;
                int b = -1, s = -1;
                foreach (var k in CardText.AllTrackKinds)
                {
                    int a = d.Aptitude(k);
                    if (a > b) { second = best; s = b; best = k; b = a; }
                    else if (a > s) { second = k; s = a; }
                }
                return new[]
                {
                    $"{CardText.TrackKindShort(best)} {b}  {CardText.TrackKindShort(second)} {s}",
                    $"QUA {d.qualifying}  CON {d.consistency}",
                };
            }
            case CardType.Car:
                return new[] { $"PWR {d.power}  HND {d.handling}", $"DRF {d.draft}  REL {d.reliability}" };
            case CardType.CrewChief:
                return new[] { $"PIT {d.pitSpeed}  STR {d.strategy}", $"SET {d.setup}" };
            default:
                return new[] { d.rules ?? "" };
        }
    }

    // Every stat, for the detail view.
    public static string FullStats(CardDefinition d)
    {
        switch (d.type)
        {
            case CardType.Driver:
                return $"#{d.carNumber}  {d.team}\n" +
                       $"Short Tracks {d.shortTracks}   Speedways {d.speedways}\n" +
                       $"Superspeedways {d.superspeedways}   Road Courses {d.roadCourses}\n" +
                       $"Dirt {d.dirtCourses}\n" +
                       $"Qualifying {d.qualifying}   Consistency {d.consistency}\n" +
                       $"Aggression {d.aggression}   Awareness {d.awareness}\n" +
                       $"Tyre Management {d.tyreManagement}";
            case CardType.Car:
                return $"Power {d.power}   Handling {d.handling}\nDraft {d.draft}   Reliability {d.reliability}";
            case CardType.CrewChief:
                return $"Pit Speed {d.pitSpeed}   Strategy {d.strategy}\nSetup {d.setup}";
            default:
                return d.rules ?? "";
        }
    }

    // Builds a card at `size` under `parent`. faceDown draws the back only (pack opening).
    public static RectTransform Build(Transform parent, CardDefinition d, Vector2 size, bool faceDown = false)
    {
        var t = PixelUITheme.Instance;
        var root = new GameObject("Card", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        var rt = (RectTransform)root.transform;
        rt.sizeDelta = size;
        var frame = root.GetComponent<Image>();
        frame.color = faceDown || d == null ? (t != null ? t.plateLight : Color.grey) : RarityColour(d.rarity);

        var inner = Box(rt, "Inner", t != null ? t.plateDeep : Color.black, 3f);

        if (faceDown || d == null)
        {
            var back = IronOvalUI.Label(inner, "Back", "Master\nDraft", IronOvalUI.Role.Header, t != null ? t.goldShade : Color.yellow);
            Fill(back.rectTransform, 2f);
            back.alignment = TextAlignmentOptions.Center;
            return rt;
        }

        bool small = size.x < 100f;
        float pad = 6f;

        // Art hook — behind the text, top half.
        var art = string.IsNullOrEmpty(d.artKey) ? null : Resources.Load<Sprite>(ArtFolder + d.artKey);
        if (art != null)
        {
            var img = new GameObject("Art", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(inner, false);
            img.sprite = art;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var art_ = img.rectTransform;
            art_.anchorMin = new Vector2(0f, 0.45f);
            art_.anchorMax = new Vector2(1f, 1f);
            art_.offsetMin = new Vector2(pad, 0f);
            art_.offsetMax = new Vector2(-pad, -pad);
        }

        var ovr = IronOvalUI.Label(inner, "OVR", d.overall.ToString(), IronOvalUI.Role.Header, RarityColour(d.rarity));
        TopLeft(ovr.rectTransform, new Vector2(pad, -pad), new Vector2(40f, 18f));

        var type = IronOvalUI.Label(inner, "Type", TypeShort(d.type), IronOvalUI.Role.Data, t != null ? t.textDim : Color.grey);
        TopLeft(type.rectTransform, new Vector2(size.x - 6f - 40f - pad, -pad), new Vector2(40f, 18f));
        type.alignment = TextAlignmentOptions.TopRight;

        var name = IronOvalUI.Label(inner, "Name", d.name, IronOvalUI.Role.Body, t != null ? t.text : Color.white);
        TopLeft(name.rectTransform, new Vector2(pad, -pad - 20f), new Vector2(size.x - 6f - 2 * pad, 36f));
        name.textWrappingMode = TextWrappingModes.Normal;
        name.overflowMode = TextOverflowModes.Ellipsis;

        var lines = StatLines(d);
        if (small)
        {
            string body = d.type == CardType.Action ? CardText.RarityName(d.rarity) : lines[0];
            var stat = IronOvalUI.Label(inner, "Stats", body, IronOvalUI.Role.Data, t != null ? t.textDim : Color.grey);
            BottomLeft(stat.rectTransform, new Vector2(pad, pad), new Vector2(size.x - 6f - 2 * pad, 18f));
            stat.overflowMode = TextOverflowModes.Ellipsis;
            stat.textWrappingMode = TextWrappingModes.NoWrap;
        }
        else
        {
            var stat = IronOvalUI.Label(inner, "Stats", string.Join("\n", lines), IronOvalUI.Role.Data, t != null ? t.textDim : Color.grey);
            BottomLeft(stat.rectTransform, new Vector2(pad, pad + 16f), new Vector2(size.x - 6f - 2 * pad, size.y * 0.45f));
            stat.alignment = TextAlignmentOptions.BottomLeft;
            stat.textWrappingMode = TextWrappingModes.Normal;
            stat.overflowMode = TextOverflowModes.Ellipsis;

            var rarity = IronOvalUI.Label(inner, "Rarity", CardText.RarityName(d.rarity), IronOvalUI.Role.Data, RarityColour(d.rarity));
            BottomLeft(rarity.rectTransform, new Vector2(pad, pad), new Vector2(size.x - 6f - 2 * pad, 16f));
        }
        return rt;
    }

    // A filled child rect inset from its parent.
    public static RectTransform Box(RectTransform parent, string name, Color colour, float inset)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = colour;
        img.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        Fill(rt, inset);
        return rt;
    }

    // A hollow rectangle of four bars — the selection frame. Needs no sprite, so it can't go missing.
    public static RectTransform Outline(RectTransform target, Color colour, float thickness, float outset)
    {
        var go = new GameObject("Focus", typeof(RectTransform));
        go.transform.SetParent(target, false);
        var rt = (RectTransform)go.transform;
        Fill(rt, -outset);
        Bar(rt, colour, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), Vector2.zero);
        Bar(rt, colour, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness));
        Bar(rt, colour, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f));
        Bar(rt, colour, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-thickness, 0f), Vector2.zero);
        return rt;
    }

    static void Bar(RectTransform parent, Color colour, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = colour;
        img.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = oMin;
        rt.offsetMax = oMax;
    }

    public static void Fill(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    static void TopLeft(RectTransform rt, Vector2 offset, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = size;
        if (rt.TryGetComponent<TMP_Text>(out var text)) { text.alignment = TextAlignmentOptions.TopLeft; text.textWrappingMode = TextWrappingModes.NoWrap; }
    }

    static void BottomLeft(RectTransform rt, Vector2 offset, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = size;
        if (rt.TryGetComponent<TMP_Text>(out var text)) text.alignment = TextAlignmentOptions.BottomLeft;
    }
}
