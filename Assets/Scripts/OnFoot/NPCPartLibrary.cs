using UnityEngine;

// Data for the layered (paper-doll) NPC look. Categories are listed in DRAW ORDER: index 0 = back-most
// (e.g. the plain base body), last = front-most (e.g. a hat). Each category offers several sprite-sheet
// options; NPCLayeredAppearance picks one at random per NPC and layers them.
//
// Authoring rules:
// - Every sheet (base + all parts) must share the SAME frame grid (frameWidth x frameHeight) and the same
//   frame count / order, drawn in the same canvas position, so layers line up frame-for-frame.
// - A part that doesn't animate (e.g. a hat) can be a single-frame sheet; it just holds frame 0.
// - Sheets are referenced as Texture2D and sliced at runtime (no Read/Write flag needed).
[CreateAssetMenu(fileName = "NPCPartLibrary", menuName = "NPC/Part Library")]
public class NPCPartLibrary : ScriptableObject
{
    [Header("Frame Grid (shared by every sheet)")]
    [Tooltip("Width of one animation frame, in pixels.")]
    public int frameWidth = 8;
    [Tooltip("Height of one animation frame, in pixels.")]
    public int frameHeight = 8;
    [Tooltip("Pixels per unit for the generated sprites (match the base art).")]
    public float pixelsPerUnit = 100f;
    [Tooltip("Pivot for each frame. 0.5,0.5 (centre) keeps the NPC rotating cleanly about its middle.")]
    public Vector2 pivot = new(0.5f, 0.5f);

    [Header("Layers, back-to-front")]
    [Tooltip("First entry draws behind, last draws in front. Typical: Base, Bottoms, Shoes, Top, Hair, Hat.")]
    public PartCategory[] categories;

    [System.Serializable]
    public class PartCategory
    {
        public string name = "Category";
        [Tooltip("If true this layer may be absent on an NPC (e.g. hats, glasses).")]
        public bool optional = false;
        [Range(0f, 1f)]
        [Tooltip("Chance the layer is present, when optional.")]
        public float presentChance = 0.5f;
        [Tooltip("Sprite-sheet options. One picked at random per NPC. Empty = layer skipped.")]
        public Texture2D[] options;

        [Header("Optional colour tint")]
        [Tooltip("Random colour multiplied onto this layer. One picked at random per NPC. Empty = no tint (art as-is). Best with greyscale/white art so the tint reads cleanly.")]
        public Color[] tintOptions;
        [Tooltip("If tintOptions is empty and this is on, apply a random vivid hue instead.")]
        public bool randomHue = false;
    }
}
