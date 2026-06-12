using UnityEngine;

// Sets up the on-foot pit scene: spawns the walking player in the pit area, builds the dialogue UI,
// and places conversational NPCs. Drop on an empty GameObject in WatkinsGlen.
public class PitWalkSpawner : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("On-foot player prefab. If null, a placeholder capsule sprite is built.")]
    public GameObject playerPrefab;
    [Tooltip("Where the player starts walking. If null, uses this GameObject's position.")]
    public Transform playerSpawn;
    public float playerScale = 1f;

    [Header("NPCs")]
    public NpcSpec[] npcs;
    [Tooltip("NPC sprite (optional). Placeholder built if null.")]
    public Sprite npcSprite;
    public Sprite playerSprite;
    public float npcScale = 1f;

    [System.Serializable]
    public struct NpcSpec
    {
        public string speakerName;
        public Vector2 position;
        [TextArea] public string[] lines;
    }

    void Start()
    {
        if (DialogueUI.Instance == null)
        {
            var ui = new GameObject("DialogueUI");
            ui.AddComponent<DialogueUI>();
        }

        SpawnPlayer();
        SpawnNpcs();
    }

    void SpawnPlayer()
    {
        Vector3 pos = playerSpawn != null ? playerSpawn.position : transform.position;
        GameObject go;
        if (playerPrefab != null)
        {
            go = Instantiate(playerPrefab, pos, Quaternion.identity);
        }
        else
        {
            go = BuildPlaceholder("OnFootPlayer", pos, playerSprite, new Color(0.3f, 0.7f, 1f));
            go.AddComponent<OnFootController>();
        }
        go.transform.localScale = Vector3.one * playerScale;

        if (go.GetComponent<OnFootController>() == null) go.AddComponent<OnFootController>();
        if (go.GetComponent<Rigidbody2D>() == null) go.AddComponent<Rigidbody2D>();
        if (go.GetComponent<Collider2D>() == null)
        {
            var c = go.AddComponent<CircleCollider2D>();
            c.radius = 0.4f;
        }

        var cam = Camera.main;
        if (cam != null)
        {
            var follow = cam.GetComponent<OnFootCameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<OnFootCameraFollow>();
            follow.target = go.transform;
        }
    }

    void SpawnNpcs()
    {
        if (npcs == null) return;
        var root = new GameObject("NPCs").transform;
        root.SetParent(transform, false);
        for (int i = 0; i < npcs.Length; i++)
        {
            var spec = npcs[i];
            var go = BuildPlaceholder($"NPC_{spec.speakerName}", spec.position, npcSprite, new Color(1f, 0.6f, 0.4f));
            go.transform.SetParent(root, false);
            go.transform.localScale = Vector3.one * npcScale;
            var npc = go.AddComponent<NPCInteractable>();
            npc.speakerName = string.IsNullOrEmpty(spec.speakerName) ? "Crew" : spec.speakerName;
            npc.lines = spec.lines != null && spec.lines.Length > 0 ? spec.lines : new[] { "..." };
        }
    }

    GameObject BuildPlaceholder(string name, Vector3 pos, Sprite sprite, Color fallbackColor)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite != null ? sprite : PlaceholderSprite(fallbackColor);
        sr.sortingOrder = 20;
        return go;
    }

    static Sprite PlaceholderSprite(Color color)
    {
        int s = 32;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                px[y * s + x] = d < s * 0.45f ? (Color32)color : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
    }
}
