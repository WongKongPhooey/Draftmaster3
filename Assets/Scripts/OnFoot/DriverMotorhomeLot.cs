using System.Collections;
using System.Collections.Generic;
using Draftmaster.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

// The drivers' motorhome lot: one RV per driver in the field, lined up next to each other in rows,
// each with a name board over the cab. Slot 0 is the player's own RV — the scene-placed RVExterior
// prefab instance that PitLaneStart spawns them inside — so the row grows out of a motorhome the
// player already knows, and inherits its rotation and door convention for free.
//
// This component also owns the FIELD ROSTER (who is racing, under what number, for which team).
// DriverPresenceDirector reads the slots back to put each of those drivers somewhere in the world.
//
// Self-installing: no scene wiring needed. Same gate as AutographFanSpawner — single player, a
// spline TrackBuilder with a pit lane, and a PitLaneStart (i.e. the on-foot paddock flow). So it
// lights up in WatkinsGlen and stays out of menu/legacy scenes.
public class DriverMotorhomeLot : MonoBehaviour
{
    // One driver's parking spot. Slot 0 wraps the player's scene-placed RV; the rest are built here.
    public class Slot
    {
        public int carNumber;
        public string fullName;      // "Taylor Emerson" — what an NPC is called in dialogue
        public string shortName;     // "Emerson" — what fits on a name board
        public string teamName;
        public GameObject car;       // the driver's car in the scene (null for a driver with no entry)
        public bool isPlayer;
        public Transform rv;         // the motorhome itself

        public Vector3 position;     // body centre, world
        public Quaternion rotation;  // body frame: local +Y = length/cab, local +X = width/door side
        public Vector3 doorPosition; // world doorway
        public Vector2 doorDirection; // world facing out of the door

        // Open band in front of the row this slot sits in — where its driver wanders without
        // walking through parked motorhomes.
        public Vector3 aisleCenter, aisleAlong, aisleOut;
        public float aisleHalfLen, aisleHalfDepth;
    }

    [Header("Refs")]
    [Tooltip("Track whose pit lane anchors the lot when no player RV is placed in the scene. Auto-found if null.")]
    public TrackBuilder track;

    [Header("Body")]
    [Tooltip("Motorhome width (m) across the door side. Matches the RV.prefab body.")]
    public float rvWidth = 3.95f;
    [Tooltip("Motorhome length (m), cab toward local +Y. Matches the RV.prefab body.")]
    public float rvLength = 9.93f;
    [Tooltip("Z the bodies sit at. Negative draws in front of the z=0 ground plane, behind the player.")]
    public float rvZ = -0.5f;

    [Header("Layout")]
    [Tooltip("Motorhomes parked side by side before the row wraps.")]
    public int perRow = 8;
    [Tooltip("Gap (m) between neighbouring motorhomes in a row. Their doors face across it.")]
    public float sideGap = 4f;
    [Tooltip("Open aisle (m) between one row's back edge and the next row's front edge.")]
    public float aisleDepth = 10f;
    [Tooltip("How far past the player's RV (the way its cab points) the drivers' rows start, metres. The player's rig sits at the head of the lot on its own; the rows need to clear the paddock tarmac, the pit garages and the RV's walk-out cutscene, all of which sit behind it.")]
    public float lotSetback = 25f;

    [Header("Sorting")]
    [Tooltip("Sorting layer for the motorhome bodies.")]
    public string sortingLayerName = "Default";
    [Tooltip("Order for the bodies: above the paddock tarmac (1), below the cars (5) and the on-foot crowd. Raise the walkers' order instead if anyone disappears behind a motorhome.")]
    public int sortingOrder = 2;

    [Header("Name boards")]
    [Tooltip("Show '#number / SURNAME' over each motorhome's cab.")]
    public bool showNameBoards = true;
    [Tooltip("Height (m) of the name board above the cab end of the body.")]
    public float nameBoardLift = 1.4f;
    public Color nameBoardColor = new Color(1f, 0.93f, 0.7f, 1f);

    [Header("Field")]
    [Tooltip("Seconds to wait for GridSpawner to finish spawning the AI field before building the lot with whoever has turned up.")]
    public float fieldTimeout = 12f;
    [Tooltip("Put each driver somewhere once the row is built (DriverPresenceDirector): in their car, at their motorhome, or walking the lot. Off = an empty lot of parked rigs.")]
    public bool populateDrivers = true;

    public static DriverMotorhomeLot Instance { get; private set; }

    readonly List<Slot> _slots = new();
    public IReadOnlyList<Slot> Slots => _slots;

    // Raised once the row exists and every slot is filled in. DriverPresenceDirector waits on this.
    public bool Built { get; private set; }
    public event System.Action<DriverMotorhomeLot> Ready;

    public bool TryGetSlot(int carNumber, out Slot slot)
    {
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i].carNumber == carNumber) { slot = _slots[i]; return true; }
        slot = null;
        return false;
    }

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
        if (FindObjectOfType<DriverMotorhomeLot>() != null) return;      // authored or already installed
        if (!GameSession.IsSinglePlayer) return;                          // MP skips the on-foot paddock entirely
        if (FindObjectOfType<PitLaneStart>() == null) return;             // no on-foot flow, no paddock
        var tb = FindObjectOfType<TrackBuilder>();
        if (tb == null || tb.track == null || !tb.track.hasPitLane) return;
        var go = new GameObject("DriverMotorhomeLot");
        go.AddComponent<DriverMotorhomeLot>().track = tb;
    }

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start() => StartCoroutine(BuildWhenFieldReady());

    IEnumerator BuildWhenFieldReady()
    {
        if (track == null) track = FindObjectOfType<TrackBuilder>();

        // GridSpawner waits on the driver database and then spawns the field several frames into the
        // scene, so the roster does not exist yet at Start. Wait for the database, then for the car
        // count to stop growing — that's the field being complete without needing GridSpawner's count.
        if (DatabaseManager.Instance != null)
        {
            float dbWait = fieldTimeout;
            while (!DatabaseManager.Instance.IsReady && dbWait > 0f) { dbWait -= Time.deltaTime; yield return null; }
        }

        int seen = -1, stable = 0;
        float timeout = fieldTimeout;
        while (timeout > 0f)
        {
            int now = FindObjectsByType<DriverLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            if (now > 0 && now == seen) { if (++stable >= 3) break; }
            else stable = 0;
            seen = now;
            timeout -= Time.deltaTime;
            yield return null;
        }

        CollectField();
        BuildRow();

        Built = true;
        Ready?.Invoke(this);
        Debug.Log($"DriverMotorhomeLot: {_slots.Count} motorhomes parked ({(_slots.Count > 0 && _slots[0].isPlayer ? "player at slot 0" : "no player RV found")}).", this);

        // Now that every driver has an address, put each of them somewhere: in their car, at their
        // motorhome, or walking the lot.
        if (populateDrivers && FindObjectOfType<DriverPresenceDirector>() == null)
            DriverPresenceDirector.Create(this);
    }

    // ---------------------------------------------------------------- roster

    void CollectField()
    {
        _slots.Clear();

        // The player leads the row. Their number comes off the paint they're racing, exactly as
        // GridSpawner reads it, so the lot and the timing tower agree on who they are.
        int playerNumber = CarIdentity.NumberOf(CarIdentity.FindPlayerCar());
        var playerSlot = new Slot
        {
            carNumber = playerNumber,
            isPlayer = true,
            car = CarIdentity.FindPlayerCar(),
        };
        FillNames(playerSlot, NPCInteractable.PlayerSpeakerName);
        _slots.Add(playerSlot);

        // Everyone else: one slot per car in the field, identified by the DriverLabel GridSpawner
        // stamped on it. Sorted by car number so the row is stable between reloads.
        var labels = new List<DriverLabel>(FindObjectsByType<DriverLabel>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        labels.Sort((a, b) => a.carNumber.CompareTo(b.carNumber));

        foreach (var label in labels)
        {
            if (label == null) continue;
            if (label.carNumber == playerNumber) continue;          // the player's own paint, already slotted
            if (TryGetSlot(label.carNumber, out _)) continue;       // never two motorhomes for one number

            var slot = new Slot
            {
                carNumber = label.carNumber,
                teamName = label.teamName,
                car = label.gameObject,
            };
            FillNames(slot, label.driverName);
            _slots.Add(slot);
        }
    }

    // Prefer the roster's real first/last name (a DriverLabel only carries the timing-tower short
    // name), falling back to whatever the label knows and finally to the car number.
    void FillNames(Slot slot, string labelName)
    {
        Driver d = RosterLookup.ByCarNumber(slot.carNumber);
        if (d != null)
        {
            string first = string.IsNullOrEmpty(d.FirstName) ? "" : d.FirstName.Trim();
            string last = string.IsNullOrEmpty(d.LastName) ? "" : d.LastName.Trim();
            slot.fullName = (first + " " + last).Trim();
            slot.shortName = !string.IsNullOrEmpty(last) ? last : slot.fullName;
            if (string.IsNullOrEmpty(slot.teamName)) slot.teamName = d.TeamName;
        }

        if (string.IsNullOrEmpty(slot.fullName)) slot.fullName = labelName;
        if (string.IsNullOrEmpty(slot.shortName)) slot.shortName = labelName;
        if (string.IsNullOrEmpty(slot.fullName))
        {
            slot.fullName = slot.carNumber > 0 ? $"Car #{slot.carNumber}" : "Driver";
            slot.shortName = slot.fullName;
        }
    }

    // ---------------------------------------------------------------- layout

    void BuildRow()
    {
        if (_slots.Count == 0) return;

        if (!ResolveAnchor(out Vector3 origin, out Quaternion rot, out RVExterior playerRv))
        {
            Debug.LogWarning("DriverMotorhomeLot: no player RV and no usable pit lane — lot not built.", this);
            return;
        }

        // Body frame: local +Y is the length (cab forward), local +X is the door side.
        // Rows march the way the cabs point — on a placed RV that's away from the pit lane, into the
        // open grass behind the paddock, which is the only direction with room for a full field.
        Vector3 sideAxis = -(rot * Vector3.right);   // march away from the player's door
        Vector3 rowAxis = rot * Vector3.up;          // extra rows stack ahead, aisle between
        float sidePitch = rvWidth + sideGap;
        float rowPitch = rvLength + aisleDepth;

        var root = new GameObject("Motorhomes").transform;
        root.SetParent(transform, false);

        // The player's rig keeps its authored spot at the head of the lot and is NOT part of the grid:
        // it is parked on the paddock tarmac, and the rows have to start clear of the tarmac, the pit
        // garage props behind it and the RV-door cutscene. Everything else lines up from the setback.
        Vector3 gridOrigin = playerRv != null ? origin + rowAxis * lotSetback : origin;
        int gridIndex = 0;

        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            slot.rotation = rot;

            if (i == 0 && playerRv != null)
            {
                // The player's own RV is already standing there, doors and colliders and all.
                slot.rv = playerRv.transform;
                slot.position = playerRv.transform.position;
                slot.doorPosition = playerRv.DoorWorldPosition;
                slot.doorDirection = playerRv.DoorWorldDirection;
            }
            else
            {
                int col = gridIndex % Mathf.Max(1, perRow);
                int row = gridIndex / Mathf.Max(1, perRow);
                gridIndex++;

                slot.position = gridOrigin + sideAxis * (col * sidePitch) + rowAxis * (row * rowPitch);
                slot.position.z = rvZ;
                slot.doorDirection = rot * Vector3.right;
                slot.doorPosition = slot.position
                                  + (Vector3)(slot.doorDirection * (rvWidth * 0.5f))
                                  + (rot * Vector3.up) * (rvLength * 0.12f);
                slot.rv = BuildMotorhome(root, slot);
            }

            AssignAisle(slot, sideAxis, rowAxis, sidePitch);
            if (showNameBoards) BuildNameBoard(root, slot);
        }

        ExtendWalkableArea(sideAxis, rowAxis);
    }

    // WatkinsGlen clamps the on-foot player to an authored PaddockBoundary polygon that stops at the
    // paddock tarmac — a lot built out in the grass beyond it would be visible and unreachable. So the
    // lot brings its own boundary: PaddockBoundary explicitly supports several disjoint pockets (inside
    // ANY of them counts as inside), and this one is deliberately grown back far enough to overlap the
    // authored polygon, so the player can walk straight from the paddock into the lot.
    //
    // Only when a boundary is already active: on a scene with no boundary at all, adding one here would
    // newly fence the player in.
    void ExtendWalkableArea(Vector3 sideAxis, Vector3 rowAxis)
    {
        if (!PaddockBoundary.AnyActive || _slots.Count == 0) return;

        float sMin = float.MaxValue, sMax = float.MinValue, rMin = float.MaxValue, rMax = float.MinValue;
        foreach (var slot in _slots)
        {
            float s = Vector3.Dot(slot.position, sideAxis);
            float r = Vector3.Dot(slot.position, rowAxis);
            sMin = Mathf.Min(sMin, s); sMax = Mathf.Max(sMax, s);
            rMin = Mathf.Min(rMin, r); rMax = Mathf.Max(rMax, r);
        }

        float sPad = rvWidth * 0.5f + sideGap;
        float rPad = rvLength * 0.5f + aisleDepth;   // back edge reaches into the authored paddock
        sMin -= sPad; sMax += sPad;
        rMin -= rPad; rMax += rPad;

        var go = new GameObject("MotorhomeLotBoundary");
        go.transform.SetParent(transform, false);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); // local space == world space

        var poly = go.AddComponent<PolygonCollider2D>();
        poly.points = new[]
        {
            (Vector2)(sideAxis * sMin + rowAxis * rMin),
            (Vector2)(sideAxis * sMax + rowAxis * rMin),
            (Vector2)(sideAxis * sMax + rowAxis * rMax),
            (Vector2)(sideAxis * sMin + rowAxis * rMax),
        };
        go.AddComponent<PaddockBoundary>();
    }

    // Where the row starts and which way it points. The player's placed RV wins — the lot then reads
    // as "the paddock the player woke up in". Without one, fall back to the pit lane's longest
    // straight, set back behind the paddock, so the feature still works on an unauthored track.
    bool ResolveAnchor(out Vector3 origin, out Quaternion rot, out RVExterior playerRv)
    {
        playerRv = FindObjectOfType<RVExterior>();
        if (playerRv != null)
        {
            origin = playerRv.transform.position;
            origin.z = rvZ;
            rot = playerRv.transform.rotation;
            return true;
        }

        origin = Vector3.zero;
        rot = Quaternion.identity;
        if (track == null || track.track == null || !track.track.hasPitLane) return false;

        var pit = track.SamplePitCenterline();
        if (pit.Count < 2) return false;

        float pitLen = pit[pit.Count - 1].distance;
        var mid = track.SamplePitAt(pitLen * 0.5f, pit);
        Vector3 midWorld = track.transform.TransformPoint(new Vector3(mid.position.x, mid.position.y, 0f));
        Vector3 normal = track.transform.TransformDirection(new Vector3(mid.normal.x, mid.normal.y, 0f)).normalized;

        // Set back well behind the pit wall, clear of PaddockSpawner's tarmac.
        origin = midWorld + normal * 60f;
        origin.z = rvZ;
        // Cabs (local +Y) point away from the lane, so rows stack further out into open ground and the
        // doors (local +X) open along the lane rather than onto a neighbour's back end.
        rot = Quaternion.LookRotation(Vector3.forward, normal);
        return true;
    }

    // The open band in front of this slot's row — a few motorhomes wide, so a driver wanders near
    // their own rig instead of the whole lot, and never through parked bodywork.
    void AssignAisle(Slot slot, Vector3 sideAxis, Vector3 rowAxis, float sidePitch)
    {
        slot.aisleAlong = sideAxis;
        slot.aisleOut = rowAxis;
        slot.aisleHalfLen = sidePitch * 1.5f;
        slot.aisleHalfDepth = Mathf.Max(1f, aisleDepth * 0.35f);
        slot.aisleCenter = slot.position + rowAxis * (rvLength * 0.5f + aisleDepth * 0.5f);
        slot.aisleCenter.z = 0f;
    }

    Transform BuildMotorhome(Transform root, Slot slot)
    {
        var go = new GameObject($"RV_{slot.carNumber}_{slot.shortName}");
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(slot.position, slot.rotation);

        var rng = new System.Random(slot.carNumber * 7919 + 17);

        // Art: the top-down motorhome sprites are 64x32 with their LENGTH along +X, while the body
        // frame runs length along +Y — so the renderer child is turned a quarter turn and scaled to
        // the real body size, whatever the sprite's pixels-per-unit happens to be.
        var art = new GameObject("Body");
        art.transform.SetParent(go.transform, false);
        art.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

        var sr = art.AddComponent<SpriteRenderer>();
        sr.sprite = MotorhomeSprite(rng.Next(3));
        sr.sharedMaterial = UnlitSprite();
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = sortingOrder;
        // A seeded pastel per driver, so a row of the same three sprites doesn't read as clones.
        sr.color = Color.HSVToRGB((float)rng.NextDouble(), 0.22f, 0.95f);

        if (sr.sprite != null)
        {
            Vector2 s = sr.sprite.bounds.size;             // sprite-local: x = length, y = width
            if (s.x > 0.0001f && s.y > 0.0001f)
                art.transform.localScale = new Vector3(rvLength / s.x, rvWidth / s.y, 1f);
        }
        else
        {
            // No art imported: a plain tinted block still parks something walkable in the lot.
            sr.sprite = BlockSprite();
            art.transform.localRotation = Quaternion.identity;
            art.transform.localScale = new Vector3(rvWidth, rvLength, 1f);
        }

        // Solid, so the player walks around the lot rather than through it.
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(rvWidth, rvLength);

        return go.transform;
    }

    // "#8 / EMERSON" over the cab. Parented to the lot root, not the RV, so it stays upright
    // however the row is rotated.
    void BuildNameBoard(Transform root, Slot slot)
    {
        var go = new GameObject($"NameBoard_{slot.carNumber}");
        go.transform.SetParent(root, false);
        Vector3 cab = slot.position + (slot.rotation * Vector3.up) * (rvLength * 0.5f + nameBoardLift);
        go.transform.position = new Vector3(cab.x, cab.y, rvZ - 0.1f);
        go.transform.rotation = Quaternion.identity;

        var tm = go.AddComponent<TextMesh>();
        tm.text = slot.carNumber > 0
            ? $"#{slot.carNumber}\n{slot.shortName.ToUpperInvariant()}"
            : slot.shortName.ToUpperInvariant();
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 72;
        tm.characterSize = 0.016f;
        tm.color = slot.isPlayer ? new Color(0.6f, 1f, 0.7f, 1f) : nameBoardColor;

        var mr = go.GetComponent<MeshRenderer>();
        mr.sortingLayerName = "Vehicles";
        mr.sortingOrder = 40;
    }

    // ---------------------------------------------------------------- assets

    static Sprite[] _motorhomes;
    static Sprite MotorhomeSprite(int index)
    {
        if (_motorhomes == null)
        {
            var found = new List<Sprite>();
            foreach (string n in new[] { "Environment/motorhome", "Environment/motorhome2", "Environment/motorhome3" })
            {
                var s = Resources.Load<Sprite>(n);
                if (s != null) found.Add(s);
            }
            _motorhomes = found.ToArray();
        }
        return _motorhomes.Length == 0 ? null : _motorhomes[Mathf.Abs(index) % _motorhomes.Length];
    }

    static Sprite _block;
    static Sprite BlockSprite()
    {
        if (_block != null) return _block;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px); tex.Apply();
        _block = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f); // 1 world unit per side
        return _block;
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
}
