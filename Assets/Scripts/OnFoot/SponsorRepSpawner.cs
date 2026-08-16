using System.Collections;
using System.Collections.Generic;
using Draftmaster.Data;
using Draftmaster.Sponsors;
using UnityEngine;
using UnityEngine.SceneManagement;

// Puts sponsor reps in the pit area on a race weekend, so deals are found by walking into people rather
// than through a menu. Who turns up is deterministic per (track, weekend): the same faces are there all
// weekend, and the next round brings different brands — see SponsorCatalog.RepsForWeekend.
//
// Placement mirrors AutographFanSpawner: spread along the box-lane span, out on the pit-wall side, clear of
// any parked cars. Self-installing on the same terms as the other paddock features — single player, a
// spline track with a pit lane, and a PitLaneStart (i.e. the on-foot flow).
public class SponsorRepSpawner : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Track whose pit lane the reps stand along. Auto-found if null.")]
    public TrackBuilder track;
    [Tooltip("Paper-doll outfit library. Falls back to Resources/NPC/NPCPartLibrary, then a coloured placeholder.")]
    public NPCPartLibrary partLibrary;

    [Header("Who turns up")]
    [Tooltip("How many brands send someone to a given weekend.")]
    [Range(0, 4)] public int repsPerWeekend = 2;
    [Tooltip("Conditions for the beat — session, track, career gates. Reps are a race-weekend fixture by default.")]
    public AppearanceConditions appearance = new AppearanceConditions();

    [Header("Placement")]
    [Tooltip("Sign of the pit-wall side the reps stand on. Flip to -1 if they end up on the racing side.")]
    public float wallSide = 1f;
    [Tooltip("Lateral distance (m) from the pit centerline. Clamped clear of the parked box lane.")]
    public float lateralFromCenter = 5.5f;
    [Tooltip("Z they stand at. Negative draws in front of the pit tarmac.")]
    public float npcZ = -0.6f;

    [Header("Look")]
    [Tooltip("World height a rep renders at. Matches the on-foot player and the pit crew.")]
    public float heightM = PitCrewSpawner.OnFootPersonHeight;
    public string sortingLayerName = "Vehicles";
    [Tooltip("Draws above the cars (car sorting order is ~5).")]
    public int baseSortingOrder = 10;

    [Header("Timing")]
    [Tooltip("Seconds to wait for the driver database and the pit lane before giving up.")]
    public float readyTimeout = 15f;

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
        if (FindObjectOfType<SponsorRepSpawner>() != null) return;
        if (!GameSession.IsSinglePlayer) return;
        if (FindObjectOfType<PitLaneStart>() == null) return;
        var tb = FindObjectOfType<TrackBuilder>();
        if (tb == null || tb.track == null || !tb.track.hasPitLane) return;
        var go = new GameObject("SponsorRepSpawner");
        go.AddComponent<SponsorRepSpawner>().track = tb;
    }

    void Start() => StartCoroutine(SpawnWhenReady());

    IEnumerator SpawnWhenReady()
    {
        if (repsPerWeekend <= 0) yield break;
        if (!appearance.IsMet()) yield break;
        if (track == null) track = FindObjectOfType<TrackBuilder>();
        if (partLibrary == null) partLibrary = Resources.Load<NPCPartLibrary>("NPC/NPCPartLibrary");

        // The brands live in SQLite, which DatabaseManager opens a few frames into the scene.
        float timeout = readyTimeout;
        while (timeout > 0f && (DatabaseManager.Instance == null || !DatabaseManager.Instance.IsReady))
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        // And PitLane needs configuring before we know where the parked cars are.
        timeout = Mathf.Max(timeout, 3f);
        while (timeout > 0f && !PitLane.Configured) { timeout -= Time.deltaTime; yield return null; }

        if (track == null || track.track == null || !track.track.hasPitLane) yield break;
        var pit = track.SamplePitCenterline();
        if (pit.Count < 2) yield break;
        float pitLength = pit[pit.Count - 1].distance;
        if (pitLength <= 0f) yield break;

        var reps = SponsorCatalog.RepsForWeekend(AppearanceConditions.CurrentTrackId, RaceWeekend.WeekendId, repsPerWeekend);
        if (reps.Count == 0)
        {
            Debug.Log("SponsorRepSpawner: no unsigned brands left to send a rep — nobody spawned.", this);
            yield break;
        }

        // Stand them along the boxes, at the far end from the autograph fans so the pit walk has variety.
        float from = track.HasPitBoxLane ? track.PitBoxLaneFrom(pitLength) : pitLength * 0.15f;
        float to = track.HasPitBoxLane ? track.PitBoxLaneTo(pitLength) : pitLength * 0.85f;

        float lateral = lateralFromCenter;
        if (PitLane.Configured) lateral = Mathf.Max(lateral, Mathf.Abs(PitLane.ParkLateral) + 2.5f);

        var root = new GameObject("SponsorReps").transform;
        root.SetParent(transform, false);

        for (int i = 0; i < reps.Count; i++)
        {
            float t = reps.Count == 1 ? 0.35f : Mathf.Lerp(0.2f, 0.75f, i / (float)(reps.Count - 1));
            Build(root, reps[i], Mathf.Lerp(from, to, t), lateral, pit);
        }

        appearance.MarkSeen();
        Debug.Log($"SponsorRepSpawner: {reps.Count} sponsor rep(s) in the pit lane at " +
                  $"{AppearanceConditions.CurrentTrackId} (standing {Mathf.RoundToInt(SponsorCatalog.PlayerStanding)}).", this);
    }

    void Build(Transform root, Sponsor sponsor, float dist, float lateral, List<TrackBuilder.Sample> pit)
    {
        var s = track.SamplePitAt(dist, pit);
        Vector3 basePos = track.transform.TransformPoint(new Vector3(s.position.x, s.position.y, 0f));
        Vector3 normalW = track.transform.TransformDirection(new Vector3(s.normal.x, s.normal.y, 0f)).normalized;

        Vector3 pos = basePos + normalW * (wallSide * lateral);
        pos.z = npcZ;

        var go = new GameObject($"SponsorRep_{sponsor.Name}");
        go.transform.SetParent(root, false);
        go.transform.position = pos;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        // Face across the lane, toward where the player walks.
        Vector3 face = -normalW * wallSide;
        OnFootController.ApplyFacing(go.transform, null, new Vector2(face.x, face.y), 90f);

        BuildAppearance(go, sponsor.Id * 131 + 17);

        var npc = go.AddComponent<SponsorRepNPC>();
        npc.sponsor = sponsor;
        npc.speakerName = $"{sponsor.Name} rep";
        npc.interactRange = 2.4f;
        npc.turnsToFace = true;
    }

    void BuildAppearance(GameObject go, int seed)
    {
        var layered = go.AddComponent<NPCLayeredAppearance>();
        layered.library = partLibrary;
        layered.layerMaterial = UnlitSprite();
        layered.sortingLayerName = sortingLayerName;
        layered.baseSortingOrder = baseSortingOrder;

        if (partLibrary != null && layered.Build(seed))
        {
            float frameWorldH = Mathf.Max(0.01f, partLibrary.frameHeight / Mathf.Max(1f, partLibrary.pixelsPerUnit));
            go.transform.localScale = Vector3.one * (heightM / frameWorldH);
        }
        else
        {
            Destroy(layered);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Placeholder(new Color(0.9f, 0.75f, 0.35f), heightM);   // gold blob = money man
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
