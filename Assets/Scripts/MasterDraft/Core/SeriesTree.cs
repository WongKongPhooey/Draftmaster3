using System;
using System.Collections.Generic;

namespace Draftmaster.MasterDraft
{
    // ---- The series tree -----------------------------------------------------------------------------------
    // The Series screen is an overworld: a branching tree of series, read left to right. The first series is open
    // from the start; finishing a series with enough championship points opens the nodes after it; branches split
    // toward the different kinds of racing, and challenges sit on the branches as nodes of their own, paying the
    // drivers, cars and paint schemes.
    //
    // The tree lives in the content itself: a series (Series.json), a challenge event or a single chapter
    // (Challenges.json) is on the tree when it carries a "tree" placement. Those two JSON files are the master copy,
    // edited by hand. SeriesTree.Build reads the placements out of the loaded catalogues.

    // Where a series, event or chapter sits on the tree, and what opens it. Public fields for JsonUtility.
    [Serializable]
    public class TreePlacement
    {
        public int col = -1;       // map column, left to right; 0 = the start. Below 0 (or no "tree" at all) = off the tree
        public int row;            // map row; 0 = the middle, negative = up
        public string branch = ""; // the branch it belongs to, shown in the panel ("Late Models")
        public List<string> after = new List<string>();   // series / event / chapter ids: open once ANY is cleared. Empty = open
        public int unlockPoints;   // a series: the championship points that clear it; 0 = SeriesTree.DefaultUnlockPoints
        public string starterCar = "";  // a car handed over free when it's open and nothing in the garage can enter
        public string icon = "";   // the tile's icon, Resources/MasterDraft/TreeIcons/<icon>; "" = SeriesTree.IconFor's default

        public static bool IsPlaced(TreePlacement t) => t != null && t.col >= 0;
    }

    public static class TreeNodeKind
    {
        public const string Series = "Series";        // target = SeriesDef.id
        public const string Challenge = "Challenge";  // target = a ChallengeEvent.id (every chapter) or a ChallengeChapter.id
    }

    [Serializable]
    public class TreeNode
    {
        public string id;          // unique on the tree
        public string kind;        // TreeNodeKind
        public string target;      // what it races (see TreeNodeKind)
        public string branch;      // the branch it belongs to, shown on the screen ("Open Wheel")
        public int col;            // map column, left to right; 0 = the start
        public int row;            // map row; 0 = the middle, negative = up
        public List<string> after = new List<string>();     // node ids: open once ANY of them is cleared. Empty = open
        public List<string> carsets = new List<string>();   // a series' entry carsets ("irl23"); empty = any car
        public int unlockPoints;   // a series: the championship points that clear it; 0 = SeriesTree.DefaultUnlockPoints
        public string starterCar;  // a car handed over free when the node is open and nothing in the garage can enter
        public string icon;        // the tile's icon, Resources/MasterDraft/TreeIcons/<icon>; "" = SeriesTree.IconFor's default

        public bool IsSeries => kind == TreeNodeKind.Series;
        public bool IsChallenge => kind == TreeNodeKind.Challenge;

        public override string ToString() => $"{id} ({kind} {target} @{col},{row})";
    }

    // A tree spelled out node by node (tests; SeriesTree.Build makes the game's from the catalogues).
    [Serializable]
    public class SeriesTreeFile
    {
        public int version;
        public List<TreeNode> nodes = new List<TreeNode>();
    }

    public enum TreeNodeState
    {
        Locked = 0,    // no node before it is cleared yet
        Open = 1,      // can be raced, never cleared
        Running = 2,   // a championship in progress, or an event part way through
        Cleared = 3,   // scored enough points (or won the title) / won every chapter
    }

    public class SeriesTree
    {
        // Points a round asks for by default: 30 is 7th place on the Cup table (SeriesRules.PointsFor) — run the
        // championship around the top ten and the next series open.
        public const int PointsPerRound = 30;

        readonly List<TreeNode> nodes = new List<TreeNode>();
        readonly Dictionary<string, TreeNode> byId = new Dictionary<string, TreeNode>(StringComparer.Ordinal);
        readonly Dictionary<string, TreeNode> byTarget = new Dictionary<string, TreeNode>(StringComparer.Ordinal);

        public int Version { get; }
        public IReadOnlyList<TreeNode> Nodes => nodes;

        // Nodes with no id, kind or target are dropped; a repeated id or target keeps its first node.
        public SeriesTree(SeriesTreeFile file)
        {
            if (file?.nodes == null) return;
            Version = file.version;
            foreach (var n in file.nodes)
            {
                if (n == null || string.IsNullOrEmpty(n.id) || string.IsNullOrEmpty(n.kind) || string.IsNullOrEmpty(n.target)) continue;
                if (byId.ContainsKey(n.id) || byTarget.ContainsKey(n.target)) continue;
                if (n.after == null) n.after = new List<string>();
                if (n.carsets == null) n.carsets = new List<string>();
                nodes.Add(n);
                byId[n.id] = n;
                byTarget[n.target] = n;
            }
        }

        // The tree the catalogues place: every series, then every challenge event and chapter, with a "tree"
        // placement. A node's id is the id of what it races, so "after" names series, events and chapters.
        public static SeriesTree Build(DraftSeriesCatalog series, ChallengeCatalog challenges)
        {
            var file = new SeriesTreeFile { version = 1 };
            if (series != null)
                foreach (var s in series.All)
                    if (TreePlacement.IsPlaced(s.tree))
                        file.nodes.Add(NodeFor(s.id, TreeNodeKind.Series, s.tree, s.restriction?.carsets));
            if (challenges != null)
                foreach (var ev in challenges.Events)
                {
                    if (TreePlacement.IsPlaced(ev.tree)) file.nodes.Add(NodeFor(ev.id, TreeNodeKind.Challenge, ev.tree, null));
                    if (ev.chapters == null) continue;
                    foreach (var c in ev.chapters)
                        if (TreePlacement.IsPlaced(c.tree)) file.nodes.Add(NodeFor(c.id, TreeNodeKind.Challenge, c.tree, null));
                }
            return new SeriesTree(file);
        }

        static TreeNode NodeFor(string id, string kind, TreePlacement t, List<string> carsets) => new TreeNode
        {
            id = id, kind = kind, target = id, col = t.col, row = t.row, branch = t.branch ?? "",
            after = t.after == null ? new List<string>() : new List<string>(t.after),
            carsets = carsets == null ? new List<string>() : new List<string>(carsets),
            unlockPoints = t.unlockPoints, starterCar = t.starterCar ?? "", icon = t.icon ?? "",
        };

        public TreeNode Get(string id) => id != null && byId.TryGetValue(id, out var n) ? n : null;

        // The node racing this series.
        public TreeNode ForSeries(string seriesId) =>
            seriesId != null && byTarget.TryGetValue(seriesId, out var n) && n.IsSeries ? n : null;

        // The node a chapter is raced from: the chapter's own node, else its event's.
        public TreeNode ForChapter(string chapterId, string eventId)
        {
            if (chapterId != null && byTarget.TryGetValue(chapterId, out var n) && n.IsChallenge) return n;
            if (eventId != null && byTarget.TryGetValue(eventId, out n) && n.IsChallenge) return n;
            return null;
        }

        // The nodes that open once this one is cleared, in tree order.
        public List<TreeNode> Children(TreeNode node)
        {
            var list = new List<TreeNode>();
            if (node == null) return list;
            foreach (var n in nodes) if (n.after.Contains(node.id)) list.Add(n);
            return list;
        }

        public List<TreeNode> Parents(TreeNode node)
        {
            var list = new List<TreeNode>();
            if (node == null) return list;
            foreach (var id in node.after) { var p = Get(id); if (p != null) list.Add(p); }
            return list;
        }

        // --- Icons ---------------------------------------------------------------------------------------------

        public const string IconFolder = "MasterDraft/TreeIcons";   // Resources.Load<Sprite>(IconFolder + "/" + icon)
        public const string ChallengeIcon = "challenge";

        // The icon a node's tile shows: its own icon if set, else a series' DM2 menu ("superspeedways", "seasons",
        // "team"...), else "challenge". So one icon covers every series of a kind until a node names its own.
        public static string IconFor(TreeNode node, DraftSeriesCatalog series)
        {
            if (node == null) return "";
            if (!string.IsNullOrEmpty(node.icon)) return node.icon;
            if (node.IsChallenge) return ChallengeIcon;
            var def = series?.Get(node.target);
            return string.IsNullOrEmpty(def?.menu) ? "series" : def.menu;
        }

        // Two letters for a tile whose icon isn't drawn yet: "SS" superspeedways, "CC" cookie_cutters.
        public static string IconPlaceholder(string icon)
        {
            switch (icon)
            {
                case "rookies": return "RK";
                case "superspeedways": return "SS";
                case "cookie_cutters": return "CC";
                case "short_tracks": return "ST";
                case "class": return "CL";
                case "team": return "TM";
                case "manufacturer": return "MF";
                case "type": return "TY";
                case "rarity": return "RA";
                case "seasons": return "SN";
                case ChallengeIcon: return "CH";
            }
            if (string.IsNullOrEmpty(icon)) return "?";
            var words = icon.Split('_', ' ', '-');
            if (words.Length >= 2 && words[0].Length > 0 && words[1].Length > 0)
                return (words[0].Substring(0, 1) + words[1].Substring(0, 1)).ToUpperInvariant();
            return icon.Substring(0, Math.Min(2, icon.Length)).ToUpperInvariant();
        }

        // --- Clearing ----------------------------------------------------------------------------------------

        public static int DefaultUnlockPoints(int rounds) => Math.Max(1, rounds) * PointsPerRound;

        // The points a series node asks for (0 for a challenge, or a series the data doesn't have).
        public static int UnlockPoints(TreeNode node, DraftSeriesCatalog series)
        {
            if (node == null || !node.IsSeries) return 0;
            if (node.unlockPoints > 0) return node.unlockPoints;
            var def = series?.Get(node.target);
            return def == null ? 0 : DefaultUnlockPoints(def.RoundCount);
        }

        // The chapters a challenge node races: its one chapter, or every chapter of its event.
        public static List<ChallengeChapter> Chapters(TreeNode node, ChallengeCatalog challenges)
        {
            var list = new List<ChallengeChapter>();
            if (node == null || !node.IsChallenge || challenges == null) return list;
            var chapter = challenges.GetChapter(node.target);
            if (chapter != null) { list.Add(chapter); return list; }
            var ev = challenges.Get(node.target);
            if (ev?.chapters != null) list.AddRange(ev.chapters);
            return list;
        }

        // A series is cleared by finishing a championship with UnlockPoints, or by winning its title; a challenge
        // node by winning every chapter it races.
        public static bool IsCleared(TreeNode node, MasterDraftProfile profile, DraftSeriesCatalog series, ChallengeCatalog challenges)
        {
            if (node == null || profile == null) return false;
            if (node.IsSeries)
            {
                var rec = profile.SeriesRecord(node.target);
                if (rec == null) return false;
                int need = UnlockPoints(node, series);
                return rec.titles > 0 || (need > 0 && rec.bestPoints >= need);
            }
            var chapters = Chapters(node, challenges);
            if (chapters.Count == 0) return false;
            foreach (var c in chapters) if (!Challenges.IsCompleted(profile.challengeRecords, c.id)) return false;
            return true;
        }

        // Open when it has no node before it, or any node before it is cleared.
        public bool IsOpen(TreeNode node, MasterDraftProfile profile, DraftSeriesCatalog series, ChallengeCatalog challenges)
        {
            if (node == null) return false;
            if (node.after.Count == 0) return true;
            foreach (var id in node.after)
                if (IsCleared(Get(id), profile, series, challenges)) return true;
            return false;
        }

        public TreeNodeState State(TreeNode node, MasterDraftProfile profile, DraftSeriesCatalog series, ChallengeCatalog challenges)
        {
            if (node == null || profile == null) return TreeNodeState.Locked;
            if (IsCleared(node, profile, series, challenges)) return TreeNodeState.Cleared;
            if (!IsOpen(node, profile, series, challenges)) return TreeNodeState.Locked;
            if (node.IsSeries) return profile.Championship(node.target) != null ? TreeNodeState.Running : TreeNodeState.Open;
            foreach (var c in Chapters(node, challenges))
                if (Challenges.IsCompleted(profile.challengeRecords, c.id)) return TreeNodeState.Running;
            return TreeNodeState.Open;
        }

        // Chapters of a challenge node won, of how many.
        public static (int won, int total) ChapterProgress(TreeNode node, MasterDraftProfile profile, ChallengeCatalog challenges)
        {
            var chapters = Chapters(node, challenges);
            int won = 0;
            if (profile != null)
                foreach (var c in chapters) if (Challenges.IsCompleted(profile.challengeRecords, c.id)) won++;
            return (won, chapters.Count);
        }

        // --- A branch's starter car ---------------------------------------------------------------------------

        // The node's starter car can be collected: the node is open, it names a car the catalogue has and the
        // player doesn't own, and nothing in the garage can enter the series (a branch's first series, reached
        // with no car of its kind).
        public bool CanCollectStarter(TreeNode node, MasterDraftProfile profile, DraftSeriesCatalog series,
            ChallengeCatalog challenges, CarCatalog cars)
        {
            if (node == null || !node.IsSeries || profile == null || cars == null || string.IsNullOrEmpty(node.starterCar)) return false;
            if (cars.Get(node.starterCar) == null || profile.Owns(node.starterCar)) return false;
            if (!IsOpen(node, profile, series, challenges)) return false;
            var def = series?.Get(node.target);
            if (def == null) return false;
            return Restrictions.EligibleGarage(def.restriction, cars, profile.garage, profile.level).Count == 0;
        }

        // Hands the starter car over at the class the series asks for, so it can enter straight away. Null if it
        // can't be collected (CanCollectStarter).
        public OwnedCar CollectStarter(TreeNode node, MasterDraftProfile profile, DraftSeriesCatalog series,
            ChallengeCatalog challenges, CarCatalog cars)
        {
            if (!CanCollectStarter(node, profile, series, challenges, cars)) return null;
            CarProgression.AwardCar(profile.garage, node.starterCar, out var car);
            if (car == null) return null;
            int minClass = series.Get(node.target)?.restriction?.minClass ?? 0;
            if (car.carClass < minClass) car.carClass = CarProgression.ClampClass(minClass);
            return car;
        }

        // --- Checking the data ----------------------------------------------------------------------------------

        // Everything wrong with the tree against the catalogues, one line each; empty when it's sound. cars may be
        // null (then starter cars and carsets aren't checked).
        public List<string> Validate(DraftSeriesCatalog series, ChallengeCatalog challenges, CarCatalog cars)
        {
            var problems = new List<string>();
            var cells = new HashSet<(int, int)>();
            int roots = 0;
            foreach (var n in nodes)
            {
                if (n.IsSeries)
                {
                    var def = series?.Get(n.target);
                    if (def == null) problems.Add($"{n.id}: no series {n.target}");
                    else if (def.restriction != null && def.restriction.minLevel > 0)
                        problems.Add($"{n.id}: has a level gate ({def.restriction.minLevel}); the tree is the gate");
                }
                else if (n.IsChallenge)
                {
                    var chapters = Chapters(n, challenges);
                    if (chapters.Count == 0) problems.Add($"{n.id}: no challenge {n.target}");
                    foreach (var c in chapters)
                        if (c.restriction != null && c.restriction.minLevel > 0)
                            problems.Add($"{n.id}: {c.id} has a level gate ({c.restriction.minLevel}); the tree is the gate");
                    var ev = chapters.Count > 0 ? challenges.EventOf(chapters[0]) : null;
                    if (ev != null)
                        for (int w = 1; w <= challenges.CycleWeeks; w++)
                            if (!Challenges.IsLive(ev, w, challenges.CycleWeeks)) { problems.Add($"{n.id}: doesn't run every week (week {w})"); break; }
                }
                else problems.Add($"{n.id}: unknown kind {n.kind}");

                if (!cells.Add((n.col, n.row))) problems.Add($"{n.id}: shares cell {n.col},{n.row}");
                if (n.after.Count == 0) roots++;
                foreach (var id in n.after)
                {
                    var p = Get(id);
                    if (p == null) problems.Add($"{n.id}: after unknown node {id}");
                    else if (p.col >= n.col) problems.Add($"{n.id}: after {id}, which isn't left of it");
                }
                if (cars != null)
                {
                    foreach (var cs in n.carsets) if (cars.GetCarset(cs) == null) problems.Add($"{n.id}: unknown carset {cs}");
                    if (!string.IsNullOrEmpty(n.starterCar))
                    {
                        var car = cars.Get(n.starterCar);
                        if (car == null) problems.Add($"{n.id}: unknown starter car {n.starterCar}");
                        else if (n.carsets.Count > 0 && !n.carsets.Contains(car.carset))
                            problems.Add($"{n.id}: starter car {n.starterCar} can't enter it");
                    }
                }
            }
            if (nodes.Count > 0 && roots == 0) problems.Add("no node is open from the start");
            return problems;
        }
    }
}
