using System;

namespace Draftmaster.MasterDraft
{
    public enum CardType { Driver = 0, Car = 1, CrewChief = 2, Action = 3 }

    // Ordered weakest to strongest: comparisons (rarity >= Rare) rely on it.
    public enum Rarity { Common = 0, Rare = 1, Epic = 2, Legendary = 3 }

    // Mirrors Draftmaster.Data.TrackType number for number (the runtime casts between them). Core can't see
    // the sqlite models, so it keeps its own copy.
    public enum TrackKind { ShortTrack = 0, Speedway = 1, Superspeedway = 2, RoadCourse = 3, DirtCourse = 4 }

    // What an action card does when played. Appended only — serialised by number in saved profiles.
    public enum ActionEffect
    {
        None = 0,
        PerfectPitStop = 1,
        PushToPass = 2,
        FreshRubber = 3,
        Slipstream = 4,
        QuickRepair = 5,
        RocketRestart = 6,
        SpottersEye = 7,
        FuelSaver = 8,
    }

    // What a card IS. A copy in somebody's collection (CardInstance) carries its own snapshot of this, the
    // way a printed card keeps the stats it was printed with when the real driver has a bad season — so a
    // collection never changes under the player when the career database ages its drivers.
    //
    // One flat class for all four types (JsonUtility has no polymorphism); fields that don't apply to a type
    // stay zero. Stats are on the card scale: driver aptitudes 0-20 (the Driver table's own scale),
    // everything else 0-100.
    [Serializable]
    public class CardDefinition
    {
        public string id;          // definition id, e.g. "DRV-12", "CAR-FRD-2", "CC-mike-hutchins", "ACT-PushToPass"
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

        // --- Action ---
        public ActionEffect effect;
        public string rules;

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
            CardType.CrewChief => "Crew Chief",
            _ => "Action",
        };

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
    }
}
