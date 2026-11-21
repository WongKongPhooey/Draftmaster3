using System;

namespace Draftmaster.MasterDraft
{
    // 3 was Action (the action cards, removed 2026-11-20). Saved cards of type 3 are dropped on load
    // (MasterDraftProfile.RestoreLists); never reuse the number.
    public enum CardType { Driver = 0, Car = 1, CrewChief = 2 }

    // Ordered weakest to strongest: comparisons (rarity >= Rare) rely on it.
    public enum Rarity { Common = 0, Rare = 1, Epic = 2, Legendary = 3 }

    // Mirrors Draftmaster.Data.TrackType number for number (the runtime casts between them). Core can't see
    // the sqlite models, so it keeps its own copy.
    public enum TrackKind { ShortTrack = 0, Speedway = 1, Superspeedway = 2, RoadCourse = 3, DirtCourse = 4 }

    // What a card IS. A copy in somebody's collection (CardInstance) carries its own snapshot of this, the
    // way a printed card keeps the stats it was printed with when the real driver has a bad season — so a
    // collection never changes under the player when the career database ages its drivers.
    //
    // One flat class for all three types (JsonUtility has no polymorphism); fields that don't apply to a type
    // stay zero. Stats are on the card scale: driver aptitudes 0-20 (the Driver table's own scale),
    // everything else 0-100.
    [Serializable]
    public class CardDefinition
    {
        public string id;          // definition id, e.g. "DRV-12", "CAR-FRD-2", "CC-mike-hutchins"
        public CardType type;
        public Rarity rarity;
        public string name;
        public int overall;        // 0-100

        // Hook for card art: when a sprite exists at Resources/MasterDraft/Cards/<artKey>, the card view draws
        // it in the portrait box instead of the procedural type badge. Empty = procedural only.
        public string artKey;

        // --- Driver ---
        public int carNumber;
        public string team;
        public string manufacturer;    // CHV / FRD / TYT (drivers and cars)
        // Track-type aptitudes, 0-20, indexed by TrackKind.
        public int shortTracks, speedways, superspeedways, roadCourses, dirtCourses;
        // Craft, 0-20.
        public int qualifying, consistency, aggression, awareness, tyreManagement;

        // --- Car (0-100) ---
        public int power, handling, draft, reliability;

        // --- Crew chief (0-100) ---
        public int pitSpeed, strategy, setup;

        // --- A car card wearing an alternate paint (PackGenerator.AltPaintOneIn) ---
        // The pack's shiny pull: the PaintDef id ("cup26_21_alt1"), its Resources livery sprite
        // ("cup26livery21blankalt1") and its name as it was pulled ("No. 21 alt paint"). Empty on every other card.
        public string paint;
        public string paintLivery;
        public string paintName;

        // Drawn shiny (holo frame, sheen, glitter) by MasterDraftCardView.
        public bool Shiny => type == CardType.Car && !string.IsNullOrEmpty(paint);

        public int Aptitude(TrackKind kind) => kind switch
        {
            TrackKind.ShortTrack => shortTracks,
            TrackKind.Speedway => speedways,
            TrackKind.Superspeedway => superspeedways,
            TrackKind.RoadCourse => roadCourses,
            _ => dirtCourses,
        };

        public CardDefinition Clone() => (CardDefinition)MemberwiseClone();
    }

    // One copy of a card in a collection. uid is unique within a profile; def.id is shared by every copy.
    [Serializable]
    public class CardInstance
    {
        public string uid;
        public CardDefinition def;

        public CardInstance() { }
        public CardInstance(string uid, CardDefinition def) { this.uid = uid; this.def = def; }
    }

    public static class CardText
    {
        public static string TypeName(CardType t) => t switch
        {
            CardType.Driver => "Driver",
            CardType.Car => "Car",
            _ => "Crew Chief",
        };

        // A type this version knows. Old saves can hold retired types (3 = Action).
        public static bool IsKnown(CardType t) => t == CardType.Driver || t == CardType.Car || t == CardType.CrewChief;

        public static string RarityName(Rarity r) => r.ToString();

        public static string TrackKindName(TrackKind k) => k switch
        {
            TrackKind.ShortTrack => "Short Track",
            TrackKind.Speedway => "Speedway",
            TrackKind.Superspeedway => "Superspeedway",
            TrackKind.RoadCourse => "Road Course",
            _ => "Dirt",
        };

        // Short labels for a card face.
        public static string TrackKindShort(TrackKind k) => k switch
        {
            TrackKind.ShortTrack => "SHT",
            TrackKind.Speedway => "SPW",
            TrackKind.Superspeedway => "SSW",
            TrackKind.RoadCourse => "RD",
            _ => "DRT",
        };

        public static readonly TrackKind[] AllTrackKinds =
        {
            TrackKind.ShortTrack, TrackKind.Speedway, TrackKind.Superspeedway, TrackKind.RoadCourse, TrackKind.DirtCourse,
        };

        // The four track types a driver card face always shows at least one of. Dirt is a stat, but not one
        // of these.
        public static bool IsHeadlineTrackKind(TrackKind k) =>
            k == TrackKind.ShortTrack || k == TrackKind.Speedway || k == TrackKind.Superspeedway || k == TrackKind.RoadCourse;

        // How many stats a driver card face prints.
        public const int DriverFaceStatCount = 3;

        // One stat as a card face prints it ("SSW 18").
        public readonly struct FaceStat
        {
            public readonly string label;
            public readonly int value;
            public readonly bool headlineTrack;   // Short Track, Speedway, Superspeedway or Road Course

            public FaceStat(string label, int value, bool headlineTrack)
            {
                this.label = label;
                this.value = value;
                this.headlineTrack = headlineTrack;
            }

            public override string ToString() => $"{label} {value}";
        }

        // Every driver stat in face order: track aptitudes first, then craft. Ties on the face resolve to
        // whichever comes first here, so an even split favours a track type.
        static FaceStat[] DriverStats(CardDefinition d)
        {
            var list = new FaceStat[AllTrackKinds.Length + 5];
            int n = 0;
            foreach (var k in AllTrackKinds)
                list[n++] = new FaceStat(TrackKindShort(k), d.Aptitude(k), IsHeadlineTrackKind(k));
            list[n++] = new FaceStat("QUA", d.qualifying, false);
            list[n++] = new FaceStat("CON", d.consistency, false);
            list[n++] = new FaceStat("AGG", d.aggression, false);
            list[n++] = new FaceStat("AWR", d.awareness, false);
            list[n++] = new FaceStat("TYR", d.tyreManagement, false);
            return list;
        }

        // The driver's three highest stats (track aptitudes and craft alike), best first. At least one is
        // always Short Track, Speedway, Superspeedway or Road Course: when the top three hold none of those,
        // the third gives way to the best of them.
        public static FaceStat[] DriverFaceStats(CardDefinition d)
        {
            if (d == null) return new FaceStat[0];

            // Stable sort, highest first.
            var all = DriverStats(d);
            for (int i = 1; i < all.Length; i++)
            {
                var x = all[i];
                int j = i - 1;
                while (j >= 0 && all[j].value < x.value) { all[j + 1] = all[j]; j--; }
                all[j + 1] = x;
            }

            var top = new FaceStat[DriverFaceStatCount];
            bool hasHeadline = false;
            for (int i = 0; i < top.Length; i++)
            {
                top[i] = all[i];
                hasHeadline |= all[i].headlineTrack;
            }
            if (!hasHeadline)
            {
                // Sorted, so the first headline stat past the top three is the best of them, and no higher
                // than the slot it takes: the face stays in order.
                for (int i = top.Length; i < all.Length; i++)
                    if (all[i].headlineTrack) { top[top.Length - 1] = all[i]; break; }
            }
            return top;
        }
    }
}
