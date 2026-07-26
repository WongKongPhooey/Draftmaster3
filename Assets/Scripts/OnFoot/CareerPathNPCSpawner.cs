using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Draftmaster.Progression;

// Stands the career-opening old-hand (CareerPathNPC) somewhere the on-foot player will walk past.
//
// In a finished career mode this beat belongs to the very first scene of a new save. Until that exists he is
// simply present in the demo paddock, a short walk from the player's motorhome, so the choice is playable and
// testable. Placement: out from the RV door and off to the far side of the engineer's walk-up beat, so the two
// don't overlap; if the scene has no RV he goes near the on-foot spawn instead. Clamped into the walkable
// PaddockBoundary when one is authored, or he'd be visible and unreachable.
//
// Self-installing: no scene wiring needed. Same gate as DriverMotorhomeLot / AutographFanSpawner — single
// player, a spline TrackBuilder, and a PitLaneStart (i.e. the on-foot paddock flow), so he lights up in
// WatkinsGlen and stays out of menu and legacy scenes.
public class CareerPathNPCSpawner : MonoBehaviour
{
    [Header("When he's there")]
    [Tooltip("Conditions for the beat: session, scene, series, career progress, repeat policy. Default is every time — he's a fixture until career mode owns this moment. Career-path clause left empty on purpose: he's the one who asks.")]
    public AppearanceConditions appearance = new AppearanceConditions();
    [Tooltip("Keep him in the scene after the choice is made (he switches to short small talk). Off = he only appears while the question is still unanswered.")]
    public bool stayAfterChoosing = true;

    [Header("Who he is")]
    [Tooltip("Name shown over his speech bubble.")]
    public string speakerName = "Paddock Veteran";
    [Tooltip("Talk range (m). Matches the other on-foot NPCs.")]
    public float interactRange = 2.4f;

    [Header("Placement")]
    [Tooltip("Metres straight out from the player's RV door.")]
    public float doorDistance = 9f;
    [Tooltip("Metres along the RV from the door line. Negative puts him the opposite side to the race engineer's walk-up beat (which sits at +2.5).")]
    public float doorLateral = -7f;
    [Tooltip("Fallback offset (m) from the on-foot spawn point when the scene has no RV to stand outside.")]
    public Vector2 spawnFallbackOffset = new Vector2(6f, -4f);
    [Tooltip("Z he stands at. Negative (toward the camera) so the sprite draws in front of the ground plane and the motorhome bodies — the same band the paddock drivers walk in.")]
    public float npcZ = -0.6f;

    [Header("Look")]
    [Tooltip("Paper-doll outfit library. Falls back to Resources/NPC/NPCPartLibrary, then a coloured placeholder.")]
    public NPCPartLibrary partLibrary;
    [Tooltip("World height he renders at. Matches the on-foot player and the pit crew.")]
    public float heightM = PitCrewSpawner.OnFootPersonHeight;
    public string sortingLayerName = "Vehicles";
    [Tooltip("Draws above the cars (car sorting order is ~5) and the motorhomes.")]
    public int baseSortingOrder = 10;
    [Tooltip("Outfit seed, so he looks the same every session.")]
    public int appearanceSeed = 771;

    [Header("Timing")]
    [Tooltip("Seconds to wait for the on-foot player to exist before giving up (PitLaneStart spawns them on its own Start).")]
    public float playerTimeout = 10f;

    public static CareerPathNPC Instance { get; private set; }

    // ----- self-install -----
    static bool _hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        TryInstall();
        if (_hooked) return;
        SceneManager.sceneLoaded += (_, __) => TryInstall();
        _hooked = true;
    }

    static void TryInstall()
    {
        if (FindObjectOfType<CareerPathNPCSpawner>() != null) return;   // authored or already installed
        if (!GameSession.IsSinglePlayer) return;                        // no on-foot paddock in multiplayer
        if (FindObjectOfType<PitLaneStart>() == null) return;           // no on-foot flow, nobody to talk to
        var go = new GameObject("CareerPathNPCSpawner");
        go.AddComponent<CareerPathNPCSpawner>();
    }

    void Start() => StartCoroutine(SpawnWhenReady());

    IEnumerator SpawnWhenReady()
    {
        if (CareerPath.HasChosen && !stayAfterChoosing) yield break;
        if (!appearance.IsMet()) yield break;
        if (partLibrary == null) partLibrary = Resources.Load<NPCPartLibrary>("NPC/NPCPartLibrary");

        // PitLaneStart instantiates the on-foot player in its own Start, and the RV it stands outside is a
        // scene object, so both may not be there for a frame or two.
        float timeout = playerTimeout;
        OnFootController player = null;
        while (timeout > 0f && player == null)
        {
            player = FindObjectOfType<OnFootController>();
            if (player != null) break;
            timeout -= Time.deltaTime;
            yield return null;
        }
        if (player == null)
        {
            Debug.Log("CareerPathNPCSpawner: no on-foot player turned up — skipping the career-path beat.", this);
            yield break;
        }

        Build(Place(player));
        appearance.MarkSeen();   // a stationary NPC has appeared the moment he's stood there
    }

    // Out from the RV door and along the body, away from the engineer's walk-up spot; otherwise a fixed
    // offset from where the player spawned. Either way, inside the walkable boundary.
    Vector3 Place(OnFootController player)
    {
        Vector3 pos;
        var rv = FindObjectOfType<RVExterior>();
        if (rv != null)
        {
            Vector2 doorDir = rv.DoorWorldDirection;
            Vector2 side = new Vector2(-doorDir.y, doorDir.x);
            pos = rv.DoorWorldPosition + (Vector3)(doorDir * doorDistance + side * doorLateral);
        }
        else
        {
            pos = player.transform.position + new Vector3(spawnFallbackOffset.x, spawnFallbackOffset.y, 0f);
        }
        // Never inherit the player's z: while they're stood in the masked RV interior they've been pulled
        // to -2.5, which would put him in front of the blackout.
        pos.z = npcZ;

        if (PaddockBoundary.AnyActive)
        {
            Vector2 clamped = PaddockBoundary.Constrain(pos);
            pos = new Vector3(clamped.x, clamped.y, pos.z);
        }
        return pos;
    }

    void Build(Vector3 pos)
    {
        var go = new GameObject("CareerPathNPC");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        BuildAppearance(go);

        var npc = go.AddComponent<CareerPathNPC>();
        npc.speakerName = speakerName;
        npc.interactRange = interactRange;
        Instance = npc;

        Debug.Log($"CareerPathNPCSpawner: {speakerName} stood at {pos} " +
                  $"(career path so far: {CareerPath.DisplayName(CareerPath.Current)}).", this);
    }

    // Same paper-doll build as the paddock drivers and the autograph fans, with a fixed seed so he's the
    // same person every session.
    void BuildAppearance(GameObject go)
    {
        var layered = go.AddComponent<NPCLayeredAppearance>();
        layered.library = partLibrary;
        layered.layerMaterial = UnlitSprite();
        layered.sortingLayerName = sortingLayerName;
        layered.baseSortingOrder = baseSortingOrder;

        if (partLibrary != null && layered.Build(appearanceSeed))
        {
            float frameWorldH = Mathf.Max(0.01f, partLibrary.frameHeight / Mathf.Max(1f, partLibrary.pixelsPerUnit));
            go.transform.localScale = Vector3.one * (heightM / frameWorldH);
        }
        else
        {
            Destroy(layered);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Placeholder(new Color(0.55f, 0.8f, 0.55f), heightM);   // green blob = the old hand
            sr.sharedMaterial = UnlitSprite();
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = baseSortingOrder;
        }
    }

    Material _unlit;
    Material UnlitSprite()
    {
        if (_unlit != null) return _unlit;
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        _unlit = new Material(sh);
        return _unlit;
    }

    static Sprite Placeholder(Color color, float metres)
    {
        int s = 32;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var px = new Color32[s * s];
        Vector2 c = new(s * 0.5f, s * 0.5f);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                px[y * s + x] = Vector2.Distance(new Vector2(x, y), c) < s * 0.45f ? (Color32)color : new Color32(0, 0, 0, 0);
        tex.SetPixels32(px); tex.Apply();
        float ppu = s / Mathf.Max(0.05f, metres);
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), ppu);
    }
}
