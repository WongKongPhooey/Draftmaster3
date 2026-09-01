using System.Collections;
using System.Collections.Generic;
using Draftmaster.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

// The drivers' motorhome lot: one RV per driver in the field, parked side by side in a small number of
// long lines (rowCount, default 2) stacked one in front of the other in the pit area, each with a name
// board over the cab. The lot is anchored on the player's own RV — the scene-placed RVExterior prefab
// instance that PitLaneStart spawns them inside — so it grows out of a motorhome the player already
// knows and inherits its rotation and door convention for free. The player's rig never moves:
// playerLineIndex decides which place it occupies, and the lot's start slides the other way to suit.
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
    [Tooltip("World direction the lines run, seen from the player's motorhome. (1,0) = to the right of screen.")]
    public Vector2 lineDirection = Vector2.right;
    [Tooltip("Gap (m) of open ground between neighbouring motorhomes in a line.")]
    public float lineGap = 2f;
    [Tooltip("How many lines the field is split across, stacked one in front of the other. 2 halves the width of the lot for twice the depth.")]
    public int rowCount = 2;
    [Tooltip("Open ground (m) between one line's bodies and the next line's — the walkway drivers mill about in. Keep it wide enough for the on-foot player to walk down.")]
    public float rowGap = 4f;
    [Tooltip("Which side of the first line the extra lines stack on. On = the way the cabs point (up-screen for an unrotated RV); off = the other way.")]
    public bool stackRowsForward = true;
    [Tooltip("Which place in the lot the player's own motorhome takes, counting along the first line and then on to the next. 0 = the head of the front line. The player's authored RV never moves — the lot's start slides to suit, so the field parks around them.")]
    public int playerLineIndex = 0;

    [Header("Sorting")]
    [Tooltip("Sorting layer for the motorhome bodies.")]
    public string sortingLayerName = "Default";
    [Tooltip("Order for the bodies: above the paddock tarmac (1), below the cars (5) and the on-foot crowd. Raise the walkers' order instead if anyone disappears behind a motorhome.")]
    public int sortingOrder = 2;

    [Header("Car numbers")]
    [Tooltip("Paint each driver's car number on the roof of their motorhome, using the same number art the cars carry.")]
    public bool showCarNumbers = true;
    [Tooltip("Resources name prefix for the number art: this + the car number, e.g. 'cup20num' + 8 loads Resources/cup20num8.")]
    public string numberSpritePrefix = "cup20num";
    [Tooltip("Height (m) of the painted number. The art is 16x16 px and the world runs at 12.8 px/m, so whole multiples of 1.25m (1.25 / 2.5 / 3.75) keep its pixels square against everything else.")]
    public float numberSize = 2.5f;
    [Tooltip("Offset (m) from the middle of the roof toward the cab. 0 = dead centre.")]
    public float numberOffset = 0f;

    [Header("Field")]
    [Tooltip("Seconds to wait for GridSpawner to finish spawning the AI field before building the lot with whoever has turned up.")]
    public float fieldTimeout = 12f;
    [Tooltip("Park a motorhome for every driver on the series entry list, not only for the cars that happen to be on track. The paddock is the same place all weekend; the field only exists during a session.")]
    public bool parkWholeRoster = true;
    [Tooltip("Most motorhomes in one line before another line is stacked behind it. Stops a full entry list parking in one 250m row.")]
    public int maxPerRow = 10;
    [Tooltip("Put each driver somewhere once the row is built (DriverPresenceDirector): in their car, at their motorhome, or walking the lot. Off = an empty lot of parked rigs.")]
    public bool populateDrivers = true;

    [Header("Garages")]
    [Tooltip("Park a team garage for every entry behind the motorhomes (PopupGarageLot): a rig with a canopy off its side, the car sat under it whenever it isn't out on track, and a masked meeting room behind its door. Off = motorhomes only.")]
    public bool buildPopupGarages = true;

    public static DriverMotorhomeLot Instance { get; private set; }

    readonly List<Slot> _slots = new();
    public IReadOnlyList<Slot> Slots => _slots;

    // The line this lot actually laid out, and how many lines it ended up needing. PopupGarageLot parks
    // its own block of rigs behind this one, and needs the same anchor, rotation and stacking direction
    // to continue the paddock rather than start a second, unrelated grid somewhere else.
    public LineLayout Line { get; private set; }
    public int LineRows { get; private set; }
    public bool HasLine { get; private set; }

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

        // Outside a session there is no field to wait for — GridSpawner leaves the track empty — so the lot
        // is built from the entry list straight away instead of standing in an empty paddock for the whole
        // timeout first.
        int seen = -1, stable = 0;
        float timeout = RaceWeekend.SessionLive ? fieldTimeout : 0f;
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
        Debug.Log($"DriverMotorhomeLot: {_slots.Count} motorhomes in {Mathf.Max(1, rowCount)} line(s) " +
                  $"({(_slots.Count > 0 && _slots[0].isPlayer ? $"player at place {Mathf.Clamp(playerLineIndex, 0, _slots.Count - 1)}" : "no player RV found")}).", this);

        // Now that every driver has an address, put each of them somewhere: in their car, at their
        // motorhome, or walking the lot.
        if (populateDrivers && FindObjectOfType<DriverPresenceDirector>() == null)
            DriverPresenceDirector.Create(this);

        // And the other half of the paddock: the team rigs, with each car parked under its canopy for as
        // long as it isn't out on track or sat in its pit box.
        if (buildPopupGarages && FindObjectOfType<PopupGarageLot>() == null)
            PopupGarageLot.Create(this);
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

        // And everyone else on the entry list. Cars are only out during a session, so between sessions the
        // labels above find nothing — but the paddock a driver walks around on a Friday morning is the same
        // paddock whether or not anybody is on track, and every driver entered has an address in it.
        if (!parkWholeRoster) return;

        foreach (var e in Draftmaster.Data.CupRoster2026.Entries)
        {
            if (e == null || e.Number == playerNumber) continue;
            if (TryGetSlot(e.Number, out _)) continue;

            var entrySlot = new Slot { carNumber = e.Number, teamName = e.Team };
            FillNames(entrySlot, string.IsNullOrEmpty(e.Short) ? e.Last : e.Short);
            _slots.Add(entrySlot);
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

    // The parked lot: one or more lines of motorhomes stacked one in front of the other. Shared by the
    // runtime lot and the editor preview so what a track author sees while placing the player's RV is
    // exactly what gets built at play time.
    public struct LineLayout
    {
        public Vector3 origin;      // body centre of place 0 — the head of the front line
        public Vector3 axis;        // unit vector each line runs along
        public Vector3 front;       // unit vector out of a line's face; later lines stack this way
        public Quaternion rotation; // body frame every rig shares (from the player's RV)
        public float pitch;         // centre-to-centre spacing along a line
        public float rowPitch;      // centre-to-centre spacing between lines
        public float depth;         // body extent along `front`
        public int perRow;          // places in a line before the next one starts

        // Places run along the front line, then wrap to the head of the next line back.
        public Vector3 PlaceAt(int index)
        {
            int n = Mathf.Max(1, perRow);
            Vector3 p = origin + axis * (pitch * (index % n)) + front * (rowPitch * (index / n));
            p.z = origin.z;
            return p;
        }

        public int RowOf(int index) => index / Mathf.Max(1, perRow);
    }

    // anchor/rot come from the player's RV. playerIndex is the place that rig occupies: the lot's start
    // is pushed back so the anchor keeps its authored position whatever place it is given. Pitch
    // measures the body ACROSS the line direction and rowPitch measures it along the stack direction,
    // so a rotated RV still parks with the right spacing either way.
    public static LineLayout ComputeLine(Vector3 anchor, Quaternion rot, Vector2 direction,
                                         float rvWidth, float rvLength, float gap, float rowGap,
                                         int rowCount, int total, int playerIndex, float z,
                                         bool stackForward = true)
    {
        Vector3 axis = new Vector3(direction.x, direction.y, 0f);
        if (axis.sqrMagnitude < 1e-6f) axis = rot * Vector3.right;
        axis.Normalize();

        // Lines stack the way the cabs point, resolved onto the axis' perpendicular so the walkway
        // never ends up skewed when the lines run at an angle to the bodies.
        Vector3 front = new Vector3(-axis.y, axis.x, 0f);
        if (Vector3.Dot(front, rot * Vector3.up) < 0f) front = -front;
        if (!stackForward) front = -front;   // stack back past the cabs instead, walkways with them

        Vector3 bodyLen = rot * Vector3.up;    // local +Y = length (cab)
        Vector3 bodyWide = rot * Vector3.right; // local +X = width (door side)
        float across = Mathf.Abs(Vector3.Dot(bodyLen, axis)) * rvLength + Mathf.Abs(Vector3.Dot(bodyWide, axis)) * rvWidth;
        float depth = Mathf.Abs(Vector3.Dot(bodyLen, front)) * rvLength + Mathf.Abs(Vector3.Dot(bodyWide, front)) * rvWidth;

        int rows = Mathf.Max(1, rowCount);
        var layout = new LineLayout
        {
            axis = axis,
            front = front,
            rotation = rot,
            pitch = across + Mathf.Max(0f, gap),
            rowPitch = depth + Mathf.Max(0f, rowGap),
            depth = depth,
            perRow = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, total) / (float)rows)),
        };

        playerIndex = Mathf.Max(0, playerIndex);
        layout.origin = anchor
                      - axis * (layout.pitch * (playerIndex % layout.perRow))
                      - front * (layout.rowPitch * (playerIndex / layout.perRow));
        layout.origin.z = z;
        return layout;
    }

    void BuildRow()
    {
        if (_slots.Count == 0) return;

        if (!ResolveAnchor(out Vector3 origin, out Quaternion rot, out RVExterior playerRv))
        {
            Debug.LogWarning("DriverMotorhomeLot: no player RV and no usable pit lane — lot not built.", this);
            return;
        }

        int playerPlace = Mathf.Clamp(playerLineIndex, 0, _slots.Count - 1);

        // A whole entry list in two lines is a quarter-mile row. Add lines instead, so the lot stays a lot
        // rather than a street.
        int rows = maxPerRow > 0
            ? Mathf.Max(rowCount, Mathf.CeilToInt(_slots.Count / (float)maxPerRow))
            : rowCount;

        var line = ComputeLine(origin, rot, lineDirection, rvWidth, rvLength, lineGap, rowGap,
                               rows, _slots.Count, playerPlace, rvZ, stackRowsForward);

        Line = line;
        LineRows = rows;
        HasLine = true;

        var root = new GameObject("Motorhomes").transform;
        root.SetParent(transform, false);

        // The player's rig holds its place; everyone else fills the remaining places in roster order,
        // so the lot closes up around whichever spot the player was given.
        int nextPlace = 0;
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
                if (nextPlace == playerPlace && playerRv != null) nextPlace++;   // leave the player's gap open
                slot.position = line.PlaceAt(nextPlace);
                nextPlace++;

                // Drivers stand in the walkway ahead of their own line, not in the gap between two rigs.
                slot.doorDirection = line.front;
                slot.doorPosition = slot.position + line.front * (line.depth * 0.5f + 0.8f);
                slot.rv = BuildMotorhome(root, slot);
            }

            AssignAisle(slot, line);
            if (showCarNumbers) BuildNumberDecal(slot);
        }

        ExtendWalkableArea(line.axis, line.front);
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

        // Pad past the outermost bodies: along the lines by half a rig, and across them by half a rig
        // plus a walkway, so the band takes in the aisle ahead of the last line and, behind the first,
        // enough ground to overlap the authored paddock.
        float sPad = rvWidth * 0.5f + rvLength * 0.5f + lineGap;
        // Floored at a rig's length so tightening rowGap can't shrink the band back off the authored
        // paddock polygon — the overlap is what lets the player walk in from the tarmac.
        float rPad = rvLength * 0.5f + Mathf.Max(rowGap, rvLength);
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

    // Where the line is anchored and which way the bodies face. The player's placed RV wins — the lot
    // then reads as "the paddock the player woke up in". Without one, fall back to the middle of the pit
    // lane, set back behind the paddock, so the feature still works on an unauthored track.
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
        // Cabs (local +Y) point away from the lane, so the line faces out into open ground and the
        // walkway in front of it is clear of the pit wall.
        rot = Quaternion.LookRotation(Vector3.forward, normal);
        return true;
    }

    // The open band in front of this slot's stretch of its line — a few motorhomes wide, so a driver
    // wanders near their own rig instead of the whole lot, and never through parked bodywork. With
    // several lines that band is the walkway between this line and the one stacked ahead of it.
    void AssignAisle(Slot slot, LineLayout line)
    {
        slot.aisleAlong = line.axis;
        slot.aisleOut = line.front;
        slot.aisleHalfLen = line.pitch * 1.5f;
        slot.aisleHalfDepth = Mathf.Max(1f, rowGap * 0.35f);
        slot.aisleCenter = slot.position + line.front * (line.depth * 0.5f + rowGap * 0.5f);
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

    // The driver's number painted on the roof, from the same Resources art the cars wear. Parented to
    // the motorhome itself (including the player's scene-placed one) so it travels with the body, and
    // centred on the body's own sprite rather than on the transform origin — the player's RV pivots at
    // its doorstep, not at the middle of the rig.
    void BuildNumberDecal(Slot slot)
    {
        if (slot.rv == null || slot.carNumber <= 0) return;
        Sprite sprite = NumberSprite(slot.carNumber);
        if (sprite == null) return;

        BodySprite(slot, out Vector3 center, out int order);
        center += (slot.rotation * Vector3.up) * numberOffset;

        var go = new GameObject($"Number_{slot.carNumber}");
        go.transform.SetParent(slot.rv, false);
        go.transform.position = new Vector3(center.x, center.y, rvZ - 0.1f);
        go.transform.localRotation = Quaternion.identity;   // reads along the body, whatever way it parks

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = UnlitSprite();
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = order;

        float h = sprite.bounds.size.y;
        if (h > 0.0001f)
        {
            float s = numberSize / h;
            go.transform.localScale = new Vector3(s, s, 1f);
        }
    }

    // Where the rig's paintwork actually sits, and what to sort above. Uses the biggest sprite on the
    // motorhome as "the body", so extra bits (door, steps, awning) can't drag the number off centre.
    void BodySprite(Slot slot, out Vector3 center, out int order)
    {
        center = slot.position;
        order = sortingOrder + 1;

        var rends = slot.rv.GetComponentsInChildren<SpriteRenderer>();
        SpriteRenderer biggest = null;
        float bestArea = 0f;
        int top = int.MinValue;
        foreach (var r in rends)
        {
            if (r == null || r.sprite == null) continue;
            Vector3 size = r.bounds.size;
            float area = size.x * size.y;
            if (area > bestArea) { bestArea = area; biggest = r; }
            top = Mathf.Max(top, r.sortingOrder);
        }
        if (biggest != null) center = biggest.bounds.center;
        if (top != int.MinValue) order = top + 1;
    }

    static readonly Dictionary<string, Sprite> _numberSprites = new();
    Sprite NumberSprite(int carNumber)
    {
        string path = $"{numberSpritePrefix}{carNumber}";
        if (_numberSprites.TryGetValue(path, out var cached)) return cached;
        var sprite = Resources.Load<Sprite>(path);
        _numberSprites[path] = sprite;   // cache misses too: a missing number shouldn't re-hit disk
        return sprite;
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
