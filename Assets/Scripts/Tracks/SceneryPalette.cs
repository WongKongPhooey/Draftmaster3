using Draftmaster.Tracks;
using UnityEngine;

// What the trackside scenery scatter is allowed to put on the grass, and how much of it.
//
// One shared asset (Resources/Tracks/SceneryPalette) serves every track — the scatter is the same idea at
// Daytona as at Sonoma, only the ground differs. Add a kind by adding an entry: a set of sprites (greenery,
// tents, flags…), a prefab, or the built-in paper-doll spectator. An entry with nothing to draw is skipped,
// so a placeholder "Greenery" row can sit here until the art exists.
//
// Footprint frame: local +X is the piece's LENGTH, local -Y is its FRONT (the way the NPC art faces). A
// sprite drawn with its long side along +X — the motorhome art is — needs no turning.
[CreateAssetMenu(menuName = "Draftmaster/Tracks/Scenery Palette", fileName = "SceneryPalette")]
public class SceneryPalette : ScriptableObject
{
    public const string ResourcePath = "Tracks/SceneryPalette";

    public enum Look
    {
        Sprites,     // one of `sprites`, drawn at `size`
        Spectator,   // a paper-doll person from the NPC part library, stood still
        Prefab,      // an instance of `prefab`
    }

    [System.Serializable]
    public class Entry
    {
        public string name = "Scenery";
        [Tooltip("Untick to leave this kind out of the next scatter without losing its settings.")]
        public bool enabled = true;
        public Look look = Look.Sprites;

        [Tooltip("Sprites: one is picked per piece. Long side along +X.")]
        public Sprite[] sprites;
        [Tooltip("Prefab: instantiated per piece. Its pivot is the footprint centre.")]
        public GameObject prefab;

        [Tooltip("Footprint in metres: x = length (local +X), y = width. Sprites are stretched to it. " +
                 "0 = the sprite's own size at its import pixels-per-unit (a prefab with 0 uses 2 x 2).")]
        public Vector2 size;
        [Tooltip("Paint each piece a random pastel, so a field of the same three sprites doesn't read as clones.")]
        public bool pastelTint;

        [Header("How many, and where")]
        [Tooltip("Pieces per kilometre of lap. Groups standing round them (below) come on top of this.")]
        [Min(0f)] public float perKm = 10f;
        [Tooltip("Plus this many per hectare of the ground the entry is allowed on (its setback band, in camps if " +
                 "Camps Only) — what fills a big infield or a short track's acres of grass.")]
        [Min(0f)] public float perHectare;
        [Tooltip("Nearest the piece's centre may be to the road edge, metres.")]
        [Min(0f)] public float minSetback = 25f;
        [Tooltip("Furthest the piece's centre may be from the road edge, metres.")]
        [Min(0f)] public float maxSetback = 120f;
        [Tooltip("Open ground kept clear round the footprint, metres.")]
        [Min(0f)] public float spacing = 2f;
        [Tooltip("Only inside the camping patches (a noise field over the ground), so pieces bunch into camps " +
                 "with open grass between rather than an even sprinkle.")]
        public bool campsOnly;

        [Header("Turning")]
        public SceneryFacing facing = SceneryFacing.Random;
        [Tooltip("Random +/- degrees on top of the facing rule.")]
        [Min(0f)] public float angleJitter;
        [Tooltip("Along Track only: chance a whole camp parks nose-in to the track instead of alongside it.")]
        [Range(0f, 1f)] public float turnChance;

        [Header("Group round each one")]
        [Tooltip("Name of another entry to stand a few of round each of these (fans outside a motorhome). Empty = none.")]
        public string followers;
        [Min(0)] public int followersMin;
        [Min(0)] public int followersMax;
        [Tooltip("How far past this piece's footprint the group may stand, metres.")]
        [Min(0f)] public float followerReach = 4f;

        [Header("Drawing")]
        [Tooltip("Sorting order on the Default layer. Grass is -30, the road 0, grandstands 2.")]
        public int sortingOrder = 2;

        // Is there anything to draw? An empty greenery row is a placeholder, not an error.
        public bool HasArt
        {
            get
            {
                switch (look)
                {
                    case Look.Spectator: return true;
                    case Look.Prefab: return prefab != null;
                    default:
                        if (sprites == null) return false;
                        foreach (var s in sprites) if (s != null) return true;
                        return false;
                }
            }
        }

        // The footprint the layout reserves, in metres.
        public Vector2 Footprint
        {
            get
            {
                if (size.x > 0f && size.y > 0f) return size;
                switch (look)
                {
                    case Look.Spectator: return new Vector2(0.6f, 0.6f);
                    case Look.Prefab: return new Vector2(2f, 2f);
                    default:
                        Vector2 biggest = Vector2.zero;
                        if (sprites != null)
                            foreach (var s in sprites)
                                if (s != null) biggest = Vector2.Max(biggest, (Vector2)s.bounds.size);
                        return biggest.x > 0f && biggest.y > 0f ? biggest : new Vector2(1f, 1f);
                }
            }
        }
    }

    public Entry[] entries = new Entry[0];

    [Header("Camping patches")]
    [Tooltip("Metres across a typical camp.")]
    [Min(1f)] public float campScale = 90f;
    [Tooltip("Roughly how much of the open grass is camp, 0..1.")]
    [Range(0f, 1f)] public float campCoverage = 0.45f;

    public int IndexOf(string entryName)
    {
        if (string.IsNullOrEmpty(entryName) || entries == null) return -1;
        for (int i = 0; i < entries.Length; i++)
            if (entries[i] != null && entries[i].name == entryName) return i;
        return -1;
    }

    public static SceneryPalette LoadDefault() => Resources.Load<SceneryPalette>(ResourcePath);
}
