using System.Collections.Generic;
using Draftmaster.Controls;
using Draftmaster.MasterDraft;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// MASTER DRAFT's pack opening (the Reveal overlay), to the Draftmaster3_UI_Unity design handoff
// (Designs/Draftmaster3_UI_Unity, Layout/PackSealed_PC.json and Reference/PackSealed_PC@3x.png).
//
// A full-screen layer over the menu, in four stages:
//  * Sealed — the design's screen: "Open Packs" header bar, the dark stage with its diagonal stripes, the pack
//    (gold frame, "Draftmaster 3", the pack's name over the cream/red stripe strip, what's inside) floating over
//    its shadow — stepped 4 px bob — and the blinking "Click or press Enter" prompt under it.
//  * Opening — confirm or a tap: the pack shakes, flashes and is gone.
//  * Dealing — the cards rise into rows face down, showing a playing-card back (procedural: cream border, red
//    lattice, the 3 medallion).
//  * Flipping — the cards turn over one at a time, left to right, each a stepped flip on its vertical axis; an
//    Epic or Legendary pull holds a beat longer and gets a call-out.
//  * Revealed — every card face up and pickable (opens the card's detail), Done, and Open Another for a bought pack.
// Confirm, a tap, R / pad North or Esc during the animation skips straight to Revealed (the cards are already in the
// collection: the pack was bought and saved before the screen opened).
//
// Only the sealed screen follows the design closely; the opened layout is a placeholder until the design settles
// (Reference/PackOpened_PC is a first pass at it). Animations are stepped, whole-pixel, like the design's
// IronOvalUIAnim (blink 1 s, bob 1.2 s, rise in 5 steps), and run on unscaled time from Update (PackUpdate).
public partial class MasterDraftUI
{
    enum PackStage { Sealed, Opening, Dealing, Flipping, Revealed }

    // Design colours (Layout/PackSealed_PC.json).
    static class PackStyle
    {
        static Color Hex(uint rgba) => new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);

        public static readonly Color Screen = Hex(0x12141cff);
        public static readonly Color Bar = Hex(0x1b2030ff);
        public static readonly Color Stage = Hex(0x0d0f16ff);
        public static readonly Color StripeA = Hex(0x141826ff);
        public static readonly Color StripeB = Hex(0x171c2cff);
        public static readonly Color Shadow = Hex(0x05060aff);
        public static readonly Color Gold = Hex(0xe8b13cff);
        public static readonly Color Cream = Hex(0xf4ead7ff);
        public static readonly Color Red = Hex(0xc4452fff);
        public static readonly Color Small = Hex(0xb9ae9aff);
        public static readonly Color Purple = Hex(0xa06fd0ff);
        public static readonly Color Meta = Hex(0x8b93adff);
        public static readonly Color StarterBody = Hex(0x14213aff);
        public static readonly Color StandardBody = Hex(0x221838ff);
    }

    // Timing, seconds.
    const float PackBobPeriod = 1.2f, PromptBlinkPeriod = 1f;
    const float PackShakeTime = 0.3f, PackFlashTime = 0.2f;
    const float DealStagger = 0.07f, DealRiseTime = 0.25f;
    const float FlipLead = 0.35f, FlipInterval = 0.45f, FlipHalf = 0.12f, BigPullHold = 0.5f, RevealHold = 0.3f;
    const int FlipSteps = 3;
    const float PackW = 140f, PackH = 186f, HeaderH = 30f, FooterH = 38f;

    PackStage _packStage;
    PackKind _packKind;
    float _packTime;
    string _packNote = "";
    float[] _flipStarts = new float[0];
    float _flipEnd;

    // The layer and what the animation moves; rebuilt with the rest of the screen.
    RectTransform _packLayer, _packBody, _packFlash;
    CanvasGroup _packPrompt;
    TextMeshProUGUI _packCaption;
    readonly List<PackCardView> _packCards = new();

    class PackCardView
    {
        public RectTransform slot;
        public Vector2 home;
        public CanvasGroup group;
        public GameObject back, face;
        public Rarity rarity;
    }

    // ------------------------------------------------------------------ what the screen shows (EditMode tests)

    public string PackStageName => _overlay == Overlay.Reveal ? _packStage.ToString() : "";
    public int PackCardCount => _reveal.Count;
    // Cards showing their face right now (a card mid-flip counts once its face is up).
    public int PackCardsFaceUp { get; private set; }

    // Show a pack's opening against a profile with no session (EditMode tests): the cards are already "bought".
    public void BuildOfflinePack(MasterDraftProfile profile, List<CardInstance> cards, PackKind kind)
    {
        _profileOverride = profile;
        _busy = false;
        if (_root == null) BuildChrome();
        _tab = Tab.Packs;
        StartPackOpening(kind, cards, "");
    }

    // Move the animation on by `seconds` (EditMode tests; Update does it in play).
    public void AdvancePack(float seconds) => PackUpdate(seconds);

    // Confirm on the pack screen: open the sealed pack, or skip the animation.
    public void ConfirmPack() => PackConfirm();

    // ------------------------------------------------------------------ flow

    void StartPackOpening(PackKind kind, List<CardInstance> cards, string note)
    {
        _reveal = cards ?? new List<CardInstance>();
        _revealed = 0;
        _revealTitle = PackGenerator.NameOf(kind);
        _packKind = kind;
        _packNote = note ?? "";
        _packStage = PackStage.Sealed;
        _packTime = 0f;
        _overlay = Overlay.Reveal;
        _lastFocusKey = "pack.done";
        Rebuild();
    }

    // Enter / Space / pad South / a tap while the pack is animating. False on the Revealed stage: there the
    // focused item takes the confirm.
    bool PackConfirm()
    {
        if (_overlay != Overlay.Reveal) return false;
        switch (_packStage)
        {
            case PackStage.Sealed:
                _packStage = PackStage.Opening;
                _packTime = 0f;
                if (_packPrompt != null) _packPrompt.alpha = 0f;
                return true;
            case PackStage.Revealed:
                return false;
            default:
                SkipPack();
                return true;
        }
    }

    void SkipPack()
    {
        if (_overlay != Overlay.Reveal || _packStage == PackStage.Revealed) return;
        _packStage = PackStage.Revealed;
        _revealed = _reveal.Count;
        _lastFocusKey = "pack.done";
        Rebuild();
    }

    // Esc / Backspace / pad East: skip the animation, then close.
    void PackBack()
    {
        if (_packStage != PackStage.Revealed) { SkipPack(); return; }
        CloseReveal();
    }

    void RevealAll() => SkipPack();

    void CloseReveal()
    {
        _overlay = Overlay.None;
        _packStage = PackStage.Revealed;
        Rebuild();
    }

    void PackUpdate(float dt)
    {
        if (_overlay != Overlay.Reveal || _packLayer == null) return;
        _packTime += dt;
        switch (_packStage)
        {
            case PackStage.Sealed:
                AnimateSealed();
                break;
            case PackStage.Opening:
                if (_packTime >= PackShakeTime + PackFlashTime)
                {
                    _packStage = PackStage.Dealing;
                    _packTime = 0f;
                    Rebuild();
                    AnimateCards();
                }
                else AnimateOpening();
                break;
            case PackStage.Dealing:
                if (_packTime >= DealEnd)
                {
                    _packStage = PackStage.Flipping;
                    _packTime = 0f;
                    PlanFlips();
                }
                AnimateCards();
                break;
            case PackStage.Flipping:
                AnimateCards();
                if (_packTime >= _flipEnd + RevealHold)
                {
                    _packStage = PackStage.Revealed;
                    _revealed = _reveal.Count;
                    Rebuild();
                }
                break;
        }
    }

    float DealEnd => DealRiseTime + DealStagger * Mathf.Max(0, _reveal.Count - 1);

    // When each card starts to turn: left to right, a big pull holding the next one back a beat.
    void PlanFlips()
    {
        int n = _reveal.Count;
        _flipStarts = new float[n];
        float t = FlipLead;
        for (int i = 0; i < n; i++)
        {
            _flipStarts[i] = t;
            t += FlipInterval + (_reveal[i].def.rarity >= Rarity.Epic ? BigPullHold : 0f);
        }
        _flipEnd = n == 0 ? 0f : _flipStarts[n - 1] + 2f * FlipHalf;
    }

    // ------------------------------------------------------------------ animation

    void AnimateSealed()
    {
        // Stepped bob (design: steps(2), up 4 px for half the cycle) — the pack lifts off its shadow.
        if (_packBody != null)
            _packBody.anchoredPosition = PackHome + ((_packTime % PackBobPeriod) < PackBobPeriod * 0.5f ? Vector2.zero : new Vector2(0f, 4f));
        if (_packPrompt != null)
            _packPrompt.alpha = (_packTime % PromptBlinkPeriod) < PromptBlinkPeriod * 0.5f ? 1f : 0f;
    }

    void AnimateOpening()
    {
        if (_packBody != null)
        {
            // A 2 px shake on 50 ms steps, then the flash covers it and the pack is gone.
            int step = Mathf.FloorToInt(_packTime / 0.05f);
            float dx = _packTime < PackShakeTime ? (step % 2 == 0 ? -2f : 2f) : 0f;
            _packBody.anchoredPosition = PackHome + new Vector2(dx, 0f);
            _packBody.gameObject.SetActive(_packTime < PackShakeTime);
        }
        if (_packFlash != null)
        {
            float k = Mathf.Clamp01((_packTime - PackShakeTime) / PackFlashTime);
            var img = _packFlash.GetComponent<Image>();
            img.enabled = _packTime >= PackShakeTime;
            img.color = new Color(1f, 1f, 1f, 1f - Mathf.Floor(k * 4f) / 4f);
        }
    }

    // Every card from the stage and the clock: rising into place while dealing, then each flip in turn.
    void AnimateCards()
    {
        int faceUp = 0;
        Rarity latest = Rarity.Common, best = Rarity.Common;
        for (int i = 0; i < _packCards.Count; i++)
        {
            var card = _packCards[i];
            float rise = 1f, scaleX = 1f, lift = 0f;
            bool face = false;

            if (_packStage == PackStage.Dealing)
            {
                float k = Mathf.Clamp01((_packTime - i * DealStagger) / DealRiseTime);
                rise = Mathf.Floor(k * 5f) / 5f;
            }
            else if (_packStage == PackStage.Flipping && i < _flipStarts.Length)
            {
                float t = _packTime - _flipStarts[i];
                if (t >= 2f * FlipHalf) face = true;
                else if (t >= 0f)
                {
                    lift = 4f;
                    int s = Mathf.Min(FlipSteps - 1, Mathf.FloorToInt((t % FlipHalf) / FlipHalf * FlipSteps));
                    if (t < FlipHalf) scaleX = 1f - (s + 1f) / FlipSteps;     // back narrows to an edge
                    else { face = true; scaleX = (s + 1f) / FlipSteps; }       // face opens out
                }
            }
            else if (_packStage == PackStage.Revealed) face = true;

            card.group.alpha = rise;
            card.slot.anchoredPosition = card.home + new Vector2(0f, -18f * (1f - rise) + lift);
            card.slot.localScale = new Vector3(scaleX, 1f, 1f);
            card.back.SetActive(!face);
            card.face.SetActive(face);
            if (face)
            {
                faceUp++;
                latest = card.rarity;
                if (card.rarity > best) best = card.rarity;
            }
        }
        PackCardsFaceUp = faceUp;
        _revealed = faceUp;

        if (_packCaption != null)
        {
            // The call-out while a big pull is the newest card face up, and over the finished pack.
            Rarity? pulled = _packStage == PackStage.Revealed ? best : faceUp > 0 ? latest : (Rarity?)null;
            if (pulled.HasValue && pulled.Value >= Rarity.Epic)
            {
                _packCaption.text = $"{CardText.RarityName(pulled.Value)} Pull!";
                _packCaption.color = MasterDraftCardView.RarityColour(pulled.Value);
            }
            else
            {
                _packCaption.text = $"{_revealTitle}   {faceUp} of {_reveal.Count}";
                _packCaption.color = PackStyle.Cream;
            }
        }
    }

    // ------------------------------------------------------------------ the screen

    // Before every Rebuild: the last layer goes (the Detail overlay over a revealed card has no layer).
    void PreparePackLayer()
    {
        if (_packLayer != null) Discard(_packLayer.gameObject);
        _packLayer = _packBody = _packFlash = null;
        _packPrompt = null;
        _packCaption = null;
        _packCards.Clear();
        PackCardsFaceUp = 0;
    }

    void BuildReveal()
    {
        var layer = new GameObject("PackOpening", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        layer.SetParent(_root, false);
        layer.SetAsLastSibling();
        MasterDraftCardView.Fill(layer, 0f);
        var screen = layer.GetComponent<Image>();
        screen.color = PackStyle.Screen;
        screen.raycastTarget = true;   // the menu underneath takes no clicks
        _packLayer = layer;

        // Header bar: "Open Packs", the pack's name, coins.
        var header = Strip(layer, "Header", true, HeaderH);
        var title = Text(header, "Open Packs", IronOvalUI.Role.Header, PackStyle.Cream, new Vector2(14f, -6f), new Vector2(200f, 18f));
        title.name = "Title";
        string packName = $"<color=#{ColorUtility.ToHtmlStringRGB(PackStyle.Purple)}>{_revealTitle}</color>";
        var right = Text(header, $"{packName}     Coins  {P.coins:N0}", IronOvalUI.Role.Data, PackStyle.Gold, Vector2.zero, new Vector2(300f, 18f));
        right.rectTransform.anchorMin = right.rectTransform.anchorMax = right.rectTransform.pivot = new Vector2(1f, 1f);
        right.rectTransform.anchoredPosition = new Vector2(-14f, -7f);
        right.alignment = TextAlignmentOptions.TopRight;
        right.name = "Coins";

        // The stage: dark, diagonal stripes, everything centred on it.
        var stage = new GameObject("Stage", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        stage.SetParent(layer, false);
        stage.anchorMin = Vector2.zero;
        stage.anchorMax = Vector2.one;
        stage.offsetMin = new Vector2(0f, FooterH);
        stage.offsetMax = new Vector2(0f, -HeaderH);
        stage.GetComponent<Image>().color = PackStyle.Stage;
        var stripes = new GameObject("Stripes", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        stripes.transform.SetParent(stage, false);
        MasterDraftCardView.Fill(stripes.rectTransform, 0f);
        stripes.sprite = StripeSprite;
        stripes.type = Image.Type.Tiled;
        stripes.pixelsPerUnitMultiplier = 1f;
        stripes.raycastTarget = false;

        var centre = new GameObject("Centre", typeof(RectTransform)).GetComponent<RectTransform>();
        centre.SetParent(stage, false);
        centre.anchorMin = centre.anchorMax = centre.pivot = new Vector2(0.5f, 0.5f);
        centre.sizeDelta = Vector2.zero;

        var footer = Strip(layer, "Footer", false, FooterH);

        if (_packStage != PackStage.Revealed)
        {
            // Anywhere on the stage opens the pack / skips the animation.
            var tap = stage.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.navigation = new Navigation { mode = Navigation.Mode.None };
            tap.onClick.AddListener(() =>
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                if (!_busy && !_leaving) PackConfirm();
            });
            string skip = InputGlyphs.UsingTouch ? "" : InputGlyphs.UsingGamepad
                ? $"{InputGlyphs.PadName(PadBindings.Back)}  Skip" : "Esc  Skip";
            if (_packStage != PackStage.Sealed && skip.Length > 0)
                Small(footer, skip, PackStyle.Meta, new Vector2(14f, -12f), new Vector2(200f, 10f), tracked: true);
        }

        if (_packStage == PackStage.Sealed || _packStage == PackStage.Opening) BuildSealedPack(centre, stage);
        else BuildPackCards(centre, footer);
    }

    // A full-width bar along the top or bottom of the layer with the design's 3 px dark edge on its inside.
    static RectTransform Strip(RectTransform layer, string name, bool top, float height)
    {
        var rt = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rt.SetParent(layer, false);
        rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
        rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
        rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = Vector2.zero;
        var img = rt.GetComponent<Image>();
        img.color = PackStyle.Bar;
        img.raycastTarget = false;

        var edge = new GameObject("Edge", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        edge.SetParent(rt, false);
        edge.anchorMin = new Vector2(0f, top ? 0f : 1f);
        edge.anchorMax = new Vector2(1f, top ? 0f : 1f);
        edge.pivot = new Vector2(0.5f, top ? 0f : 1f);
        edge.sizeDelta = new Vector2(0f, 3f);
        edge.anchoredPosition = Vector2.zero;
        var e = edge.GetComponent<Image>();
        e.color = PackStyle.Shadow;
        e.raycastTarget = false;
        return rt;
    }

    // The pack's top-left on the stage centre: the design puts it 14 px above centre, 140 x 186.
    static readonly Vector2 PackHome = new Vector2(-PackW / 2f, 14f + PackH / 2f);

    void BuildSealedPack(RectTransform centre, RectTransform stage)
    {
        bool starter = _packKind == PackKind.Starter;

        // Shadow stays put; the pack bobs over it.
        Plate(centre, "PackShadow", PackStyle.Shadow, PackHome + new Vector2(6f, -6f), new Vector2(PackW, PackH));
        var pack = Plate(centre, "Pack", starter ? PackStyle.StarterBody : PackStyle.StandardBody, PackHome, new Vector2(PackW, PackH));
        _packBody = pack;
        Frame(pack, PackStyle.Gold, 3f, 3f);

        var brand = Small(pack, "Draftmaster 3", PackStyle.Gold, new Vector2(0f, -10f), new Vector2(PackW, 10f), tracked: true);
        brand.characterSpacing = 25f;   // the design's 2 px tracking
        brand.alignment = TextAlignmentOptions.Top;

        // The pack's name in two lines, display face with a hard 2 px shadow, over the cream/red strip.
        string name = _revealTitle.EndsWith(" Pack") ? _revealTitle.Substring(0, _revealTitle.Length - 5) : _revealTitle;
        PackWord(pack, name, -56f);
        PackWord(pack, "Pack", -79f);
        var strip = Plate(pack, "Strip", Color.white, new Vector2((PackW - 60f) / 2f, -107f), new Vector2(60f, 6f)).GetComponent<Image>();
        strip.sprite = StripSprite;
        strip.type = Image.Type.Tiled;
        strip.pixelsPerUnitMultiplier = 1f;

        int size = PackGenerator.SizeOf(_packKind);
        var count = Small(pack, $"{size} Cards", PackStyle.Small, new Vector2(0f, -152f), new Vector2(PackW, 10f), tracked: true);
        count.alignment = TextAlignmentOptions.Top;
        var promise = Small(pack, starter ? "A team that can race" : "1 Rare+ guaranteed", PackStyle.Small,
                            new Vector2(0f, -165f), new Vector2(PackW, 10f), tracked: true);
        promise.alignment = TextAlignmentOptions.Top;

        // "▶ Click or press Enter", blinking, under the pack. The arrow is drawn in pixels.
        string prompt = InputGlyphs.UsingTouch ? "Tap to open"
            : InputGlyphs.UsingGamepad ? $"Press {InputGlyphs.PadName(PadBindings.Confirm)} to open"
            : "Click or press Enter";
        var promptRt = new GameObject("Prompt", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        promptRt.SetParent(centre, false);
        var label = Text(promptRt, prompt, IronOvalUI.Role.Header, PackStyle.Gold, Vector2.zero, new Vector2(300f, 16f));
        float w = Mathf.Ceil(label.GetPreferredValues(prompt).x);
        float total = w + 10f;
        TopLeft(promptRt, new Vector2(-total / 2f, -91f), new Vector2(total, 16f));
        TopLeft(label.rectTransform, new Vector2(10f, -1f), new Vector2(w + 2f, 16f));
        for (int i = 0; i < 4; i++)
            Plate(promptRt, "Arrow", PackStyle.Gold, new Vector2(i, -4f - i), new Vector2(1f, 7f - 2f * i));
        _packPrompt = promptRt.GetComponent<CanvasGroup>();

        // The flash that swallows the pack as it opens.
        var flash = new GameObject("Flash", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        flash.SetParent(stage, false);
        MasterDraftCardView.Fill(flash, 0f);
        var img = flash.GetComponent<Image>();
        img.color = Color.white;
        img.raycastTarget = false;
        img.enabled = false;
        _packFlash = flash;

        if (_packStage == PackStage.Opening) AnimateOpening();
        else AnimateSealed();
    }

    void PackWord(RectTransform pack, string word, float y)
    {
        var shadow = Text(pack, word, IronOvalUI.Role.Body, PackStyle.Shadow, new Vector2(2f, y - 2f), new Vector2(PackW, 24f));
        shadow.alignment = TextAlignmentOptions.Top;
        var text = Text(pack, word, IronOvalUI.Role.Body, PackStyle.Cream, new Vector2(0f, y), new Vector2(PackW, 24f));
        text.alignment = TextAlignmentOptions.Top;
        text.name = "PackName";
    }

    // The cards: up to six to a row, two rows for the Starter Pack's twelve. Face down while dealing; flipped by
    // AnimateCards; pickable once revealed.
    void BuildPackCards(RectTransform centre, RectTransform footer)
    {
        int n = _reveal.Count;
        const int perRow = 6;
        int rows = Mathf.Max(1, (n + perRow - 1) / perRow);
        var size = rows == 1 ? new Vector2(88f, 116f) : new Vector2(72f, 96f);
        float gapX = rows == 1 ? 10f : 8f, gapY = 10f;
        float blockH = rows * size.y + (rows - 1) * gapY;
        float top = blockH / 2f - 6f;   // a little low of centre: the caption sits above

        _packCaption = Text(centre, "", IronOvalUI.Role.Header, PackStyle.Cream, new Vector2(-300f, top + 26f), new Vector2(600f, 18f));
        _packCaption.alignment = TextAlignmentOptions.Top;
        _packCaption.name = "Caption";

        for (int i = 0; i < n; i++)
        {
            int row = i / perRow, col = i % perRow;
            int inRow = row == rows - 1 ? n - row * perRow : perRow;
            float rowW = inRow * size.x + (inRow - 1) * gapX;
            // The slot pivots on its centre so the flip turns about the card's middle.
            var home = new Vector2(-rowW / 2f + col * (size.x + gapX) + size.x / 2f, top - row * (size.y + gapY) - size.y / 2f);

            var slot = new GameObject("PackCard" + i, typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
            slot.SetParent(centre, false);
            slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0.5f, 0.5f);
            slot.sizeDelta = size;
            slot.anchoredPosition = home;

            Plate(slot, "Shadow", PackStyle.Shadow, new Vector2(3f, -3f), size);
            var back = Plate(slot, "Back", Color.white, Vector2.zero, size);
            var backImg = back.GetComponent<Image>();
            backImg.sprite = CardBackSprite(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y));
            var mark = Text(back, "3", IronOvalUI.Role.Body, PackStyle.Gold, new Vector2(0f, -size.y / 2f + 9f), new Vector2(size.x, 24f));
            mark.alignment = TextAlignmentOptions.Top;

            var def = _reveal[i].def;
            var face = MasterDraftCardView.Build(slot, def, size);
            TopLeft(face, Vector2.zero, size);

            var view = new PackCardView
            {
                slot = slot, home = home, group = slot.GetComponent<CanvasGroup>(),
                back = back.gameObject, face = face.gameObject, rarity = def.rarity,
            };
            _packCards.Add(view);

            if (_packStage == PackStage.Revealed)
            {
                int index = i;
                AddItem(face, "rev" + i, () => { _detail = _reveal[index]; ShowDetail(); });
            }
        }

        if (_packStage == PackStage.Revealed)
        {
            var buttons = new GameObject("Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
            buttons.SetParent(footer, false);
            buttons.anchorMin = buttons.anchorMax = buttons.pivot = new Vector2(0.5f, 1f);
            buttons.sizeDelta = new Vector2(600f, FooterH);
            buttons.anchoredPosition = Vector2.zero;
            float y = -Mathf.Round(3f + (FooterH - 3f - UIButton.KitPx) / 2f);   // centred under the 3 px edge
            ButtonItem(buttons, "pack.done", "Done", new Vector2(490f, y), 110f, CloseReveal);
            if (_packKind == PackKind.Standard)
            {
                int price = Economy.PriceOf(PackKind.Standard);
                ButtonItem(buttons, "pack.again", $"Open Another  {price}", new Vector2(300f, y), 180f,
                           () => OpenPack(PackKind.Standard), P.coins >= price);
            }
            if (_packNote.Length > 0)
            {
                var note = Text(buttons, _packNote, IronOvalUI.Role.Data, PackStyle.Meta, new Vector2(0f, -8f), new Vector2(290f, 16f));
                note.overflowMode = TextOverflowModes.Ellipsis;
            }
        }
        AnimateCards();
    }

    // ------------------------------------------------------------------ procedural art

    static Sprite _stripeSprite, _stripSprite;
    static readonly Dictionary<Vector2Int, Sprite> _cardBacks = new();

    // The design's stage stripes (stripes_f3f816d6): 28 px tile, 14 px diagonal bands in two navies.
    static Sprite StripeSprite
    {
        get
        {
            if (_stripeSprite != null) return _stripeSprite;
            var tex = PixelTexture(28, 28);
            var px = new Color32[28 * 28];
            for (int y = 0; y < 28; y++)
                for (int x = 0; x < 28; x++)
                    px[(27 - y) * 28 + x] = (x + y) % 28 < 14 ? PackStyle.StripeA : PackStyle.StripeB;
            tex.SetPixels32(px);
            tex.Apply();
            return _stripeSprite = MakeSprite(tex);
        }
    }

    // The pack's cream/red strip (stripes_6bd82a34): 6 cream, 6 red.
    static Sprite StripSprite
    {
        get
        {
            if (_stripSprite != null) return _stripSprite;
            var tex = PixelTexture(12, 1);
            var px = new Color32[12];
            for (int x = 0; x < 12; x++) px[x] = x < 6 ? PackStyle.Cream : PackStyle.Red;
            tex.SetPixels32(px);
            tex.Apply();
            return _stripSprite = MakeSprite(tex);
        }
    }

    // A playing-card back at the card's pixel size: dark edge, cream border, a red rule, then a red field with a
    // cream diamond lattice and a navy medallion ringed in cream and gold (the "3" sits on it as text).
    static Sprite CardBackSprite(int w, int h)
    {
        var key = new Vector2Int(w, h);
        if (_cardBacks.TryGetValue(key, out var cached) && cached != null) return cached;

        Color32 ink = PackStyle.Shadow, cream = PackStyle.Cream, red = PackStyle.Red, navy = PackStyle.Bar, gold = PackStyle.Gold;
        var px = new Color32[w * h];
        float cx = (w - 1) / 2f, cy = (h - 1) / 2f, rx = w * 0.24f, ry = h * 0.17f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                Color32 c;
                if (edge == 0) c = ink;
                else if (edge <= 4) c = cream;
                else if (edge == 5) c = red;
                else if (edge == 6) c = cream;
                else
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r <= 1f) c = navy;
                    else if (r <= 1f + 1.5f / rx) c = gold;
                    else if (r <= 1f + 3.5f / rx) c = cream;
                    else c = ((x + y) % 6 == 0 || (x - y + 6000) % 6 == 0) ? cream : red;
                }
                px[y * w + x] = c;
            }
        }
        // Rounded corners: the corner pixel and its neighbours drop to transparent, the ink follows the curve.
        Color32 clear = new Color32(0, 0, 0, 0);
        foreach (var (x, y) in new[] { (0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1) })
        {
            int sx = x == 0 ? 1 : -1, sy = y == 0 ? 1 : -1;
            px[y * w + x] = clear;
            px[y * w + x + sx] = clear;
            px[(y + sy) * w + x] = clear;
            px[(y + sy) * w + x + sx] = ink;
        }
        var tex = PixelTexture(w, h);
        tex.SetPixels32(px);
        tex.Apply();
        var sprite = MakeSprite(tex);
        _cardBacks[key] = sprite;
        return sprite;
    }

    static Texture2D PixelTexture(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false)
    {
        filterMode = FilterMode.Point,
        wrapMode = TextureWrapMode.Repeat,
        hideFlags = HideFlags.HideAndDontSave,
    };

    // 100 pixels per unit against the canvas's reference 100: one texture pixel to one kit pixel.
    static Sprite MakeSprite(Texture2D tex)
    {
        var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                                   SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
