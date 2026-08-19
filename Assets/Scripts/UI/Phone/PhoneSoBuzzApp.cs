using System.Collections.Generic;
using UnityEngine;
using Draftmaster.Data;
using Draftmaster.Fans;
using Draftmaster.Sponsors;

// SoBuzz — the social feed. Two jobs: show the player where their fan appeal actually sits, and let them
// see which sponsors are circling before a rep ever walks up to them in the pit lane.
//
// A sponsor's interest is not flavour text — it's SponsorTerms' own rule (standing vs the brand's
// MinPrestige), so a post that says a brand is watching means their rep will deal, and a locked one tells
// the player exactly how much more appeal they need. Posts are deterministic per race weekend: the feed
// doesn't reshuffle every time the phone opens, but it's different next weekend.
public class PhoneSoBuzzApp : PhoneApp
{
    public override string Id => "sobuzz";
    public override string TileName => "SoBUZZ";
    public override string TileSubtitle => "Who's talking";
    public override Color Accent => PixelGUI.Info;

    struct Post
    {
        public string handle;
        public string text;
        public string tag;        // "INTERESTED" / "SIGNED" / "OUT OF REACH"
        public Color tagColour;
        public int likes;
        public bool muted;        // drawn dim: a brand out of reach, or a hostile fan
    }

    readonly List<Post> _feed = new();
    int _builtForWeekend = -1;
    int _builtForSponsorCount = -1;

    public override void OnOpen() => _builtForWeekend = -1;   // rebuild on open so new deals show up

    public override float Draw(float x, float y, float w)
    {
        float y0 = y;
        float appeal = FanAppeal.Value;

        // The gauge. Followers are a readable stand-in for appeal — the number the player watches.
        y += Section(x, y, w, "YOUR REACH");
        y += Meter(x, y, w, "Fan appeal", appeal / 100f, $"{Mathf.RoundToInt(appeal)}/100", AppealColour(appeal));
        y += Row(x, y, w, "Followers", Followers(appeal).ToString("N0"), PixelGUI.TextDim);
        y += Row(x, y, w, "Sponsor standing", Standing(appeal), PixelGUI.TextDim);
        y += PixelGUI.Px(4f);
        y += Body(x, y, w, AppealAdvice(appeal), PixelGUI.TextDisabled);

        Build(appeal);

        y += PixelGUI.Px(6f);
        y += Section(x, y, w, "MENTIONS");
        if (_feed.Count == 0)
            y += Empty(x, y, w, "Quiet out there. Run some races and sign for the fans.");
        for (int i = 0; i < _feed.Count; i++) y += DrawPost(x, y, w, _feed[i]);

        return y - y0 + PixelGUI.Px(6f);
    }

    // ------------------------------------------------------------------ the feed

    void Build(float appeal)
    {
        int weekend = RaceWeekend.WeekendId;
        if (_builtForWeekend == weekend && _builtForSponsorCount == SponsorBook.Count) return;
        _builtForWeekend = weekend;
        _builtForSponsorCount = SponsorBook.Count;
        _feed.Clear();

        // Signed first — the player's own board, confirmed in public.
        foreach (var deal in SponsorBook.Deals)
        {
            if (!deal.IsActive) continue;
            string brand = string.IsNullOrEmpty(deal.sponsorName) ? "Sponsor" : deal.sponsorName;
            var rng = Seeded(weekend, brand.GetHashCode());
            _feed.Add(new Post
            {
                handle = Handle(brand),
                text = deal.IsPlaced ? Pick(rng, SignedPlaced, brand) : Pick(rng, SignedUnplaced, brand),
                tag = deal.IsPlaced ? "SIGNED" : "WAITING ON PAINT",
                tagColour = deal.IsPlaced ? PixelGUI.Confirm : PixelGUI.Gold,
                likes = Likes(rng, appeal, 1.4f),
            });
        }

        // Then the brands who haven't signed: interested ones, then the ones still out of reach.
        var interested = new List<Sponsor>();
        var outOfReach = new List<Sponsor>();
        foreach (var s in SponsorCatalog.All())
        {
            if (s == null || SponsorBook.HasSponsor(s.Id)) continue;
            if (SponsorTerms.CanApproach(appeal, s.MinPrestige)) interested.Add(s);
            else if (s.MinPrestige - appeal <= 25f) outOfReach.Add(s);
        }

        interested.Sort((a, b) => b.MinPrestige.CompareTo(a.MinPrestige));   // best brand first
        outOfReach.Sort((a, b) => a.MinPrestige.CompareTo(b.MinPrestige));  // nearest first

        int shown = 0;
        foreach (var s in interested)
        {
            if (shown++ >= 5) break;
            var rng = Seeded(weekend, s.Id);
            _feed.Add(new Post
            {
                handle = Handle(s.Name),
                text = Pick(rng, Interested, s.Industry ?? "racing"),
                tag = "INTERESTED",
                tagColour = PixelGUI.Info,
                likes = Likes(rng, appeal, 1f),
            });
        }

        shown = 0;
        foreach (var s in outOfReach)
        {
            if (shown++ >= 3) break;
            var rng = Seeded(weekend, s.Id);
            int needed = Mathf.CeilToInt(s.MinPrestige - appeal);
            _feed.Add(new Post
            {
                handle = Handle(s.Name),
                text = Pick(rng, TooSmall, s.Industry ?? "racing"),
                tag = $"+{needed} APPEAL",
                tagColour = PixelGUI.TextDisabled,
                likes = Likes(rng, appeal, 0.4f),
                muted = true,
            });
        }

        // And the crowd, reading off what the player has actually done.
        AddFanPosts(weekend, appeal);
    }

    void AddFanPosts(int weekend, float appeal)
    {
        int wins = PlayerStatsLedger.Get("wins");
        int top5 = PlayerStatsLedger.Get("top5s");
        int starts = PlayerStatsLedger.Get("starts");
        int caused = PlayerStatsLedger.Get("contacts.caused");
        int autographs = PlayerStatsLedger.Get("autographs");

        var rng = Seeded(weekend, 991);

        if (wins > 0)
            _feed.Add(new Post { handle = "@pitroadpete", likes = Likes(rng, appeal, 2f),
                                 text = wins == 1 ? "First win and they made it look easy. Watch this one."
                                                  : $"{wins} wins now. Nobody's calling it luck any more." });
        else if (top5 > 0)
            _feed.Add(new Post { handle = "@thehighline", likes = Likes(rng, appeal, 1.1f),
                                 text = "Quietly stacking top fives. The win's coming." });
        else if (starts > 0)
            _feed.Add(new Post { handle = "@thehighline", likes = Likes(rng, appeal, 0.7f),
                                 text = "Rookie's still learning the rhythm. Plenty of race left in the season." });

        if (caused >= 3)
            _feed.Add(new Post { handle = "@wreckwatch", likes = Likes(rng, appeal, 1.3f), muted = true,
                                 text = "Another car ends up in the fence. Somebody have a word." });

        if (autographs > 0)
            _feed.Add(new Post { handle = "@garagegal", likes = Likes(rng, appeal, 1f),
                                 text = "Signed for every kid on the fence today. That's how you do it." });
        else if (appeal < 35f)
            _feed.Add(new Post { handle = "@garagegal", likes = Likes(rng, appeal, 0.5f), muted = true,
                                 text = "Walked straight past the autograph line again." });
    }

    float DrawPost(float x, float y, float w, Post post)
    {
        float pad = PixelGUI.Px(4f);
        float inner = w - pad * 2f;
        float bodyH = PixelGUI.Body.CalcHeight(new GUIContent(post.text), inner);
        float h = PixelGUI.Px(11f) + bodyH + PixelGUI.Px(11f) + pad * 2f;

        var plate = new Rect(x, y, w, h);
        Plate(plate, post.muted ? PixelGUI.PlateLight : post.tagColour);

        float cx = x + pad, cy = y + pad;
        cy += Row(cx, cy, inner, post.handle, post.tag, post.muted ? PixelGUI.TextDisabled : post.tagColour);
        cy += Body(cx, cy, inner, post.text, post.muted ? PixelGUI.TextDisabled : PixelGUI.Text);
        Row(cx, cy, inner, $"♥ {post.likes:N0}", "", PixelGUI.TextDisabled, dim: true);

        return h + PixelGUI.Px(4f);
    }

    // ------------------------------------------------------------------ copy + numbers

    static readonly string[] Interested =
    {
        "Watching that car closely this season. {0} money likes a story.",
        "Our marketing lot won't stop talking about this driver. Might make a call.",
        "Room on that hood for the right brand. Just saying.",
        "Good hands, good crowd, right numbers. We're paying attention.",
    };

    static readonly string[] TooSmall =
    {
        "Plenty of promise. Come back to us when the grandstand knows the name.",
        "Not the profile we're after yet — but we're not saying never.",
        "We back winners. Go be one.",
    };

    static readonly string[] SignedPlaced =
    {
        "Proud to be on this car. {0} is going racing.",
        "Our colours, their right foot. Let's go.",
    };

    static readonly string[] SignedUnplaced =
    {
        "Signed the deal — still waiting to see {0} on the bodywork.",
        "Deal's done. Now paint it on, would you?",
    };

    static string Pick(System.Random rng, string[] pool, string arg) =>
        string.Format(pool[rng.Next(pool.Length)], arg);

    static System.Random Seeded(int weekend, int salt) => new System.Random(weekend * 397 ^ salt);

    static int Likes(System.Random rng, float appeal, float weight) =>
        Mathf.Max(3, Mathf.RoundToInt((20f + appeal * appeal * 0.6f) * weight * (0.6f + (float)rng.NextDouble())));

    // Followers scale hard with appeal — the difference between a club racer and a name.
    static int Followers(float appeal) =>
        Mathf.RoundToInt(Mathf.Lerp(400f, 480000f, Mathf.Pow(Mathf.Clamp01(appeal / 100f), 2.2f)));

    static string Standing(float appeal) =>
        appeal >= 80f ? "Household name"
      : appeal >= 60f ? "Known quantity"
      : appeal >= 40f ? "Getting noticed"
      : appeal >= 20f ? "Local interest"
      : "Nobody";

    static Color AppealColour(float appeal) =>
        appeal >= 60f ? PixelGUI.Confirm : appeal >= 30f ? PixelGUI.Gold : PixelGUI.Danger;

    static string AppealAdvice(float appeal) =>
        appeal >= 60f
            ? "Brands come to you at this level. Hold the reps out for more money."
            : "Appeal comes from results and from the fans who come up to you in the pit lane. Signing for them raises it; walking past lowers it.";

    static string Handle(string name)
    {
        if (string.IsNullOrEmpty(name)) return "@brand";
        var sb = new System.Text.StringBuilder("@");
        foreach (char c in name)
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }
}
