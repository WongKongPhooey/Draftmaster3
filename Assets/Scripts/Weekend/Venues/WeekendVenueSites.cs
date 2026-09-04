using System.Collections;
using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEngine;

// Builds the places a race weekend happens in, into whatever track is loaded.
//
// The weekend used to be a stack of panels you could open standing anywhere. It is now a set of places you
// walk to: the plan meeting is at the pit box, the debrief is inside your own motorhome, the drivers
// meeting and the press conference are in the room every circuit has, signing is done through the fence at
// the edge of the paddock, sponsor duty is under the hospitality awning, driver intros are on the stage at
// the end of pit road, and watching somebody else's session means sitting in a grandstand.
//
// Everything is placed off two things the track already knows: the paddock rectangle (PaddockSpawner's own
// frame, so the room and the fence land where the player can actually walk) and the generated grandstands
// (so a spectating seat exists at every track without anyone authoring one). A track can override any of
// them by putting a WeekendVenueAnchor in its package prefab — an anchor that already exists is never
// replaced, which is the same rule the rest of the track dressing follows.
//
// Self-installing: no scene wiring. Race scenes only — a menu has no paddock.
public class WeekendVenueSites : MonoBehaviour
{
    public static WeekendVenueSites Instance { get; private set; }

    // Room sizes, metres. Blocked out at the scale of the RV interior: a person is ~0.5m across at this
    // project's pixel standard, so a 14m room with three rows of seats reads as a room, not a hangar.
    const float RoomWidth = 16f;
    const float RoomDepth = 11f;
    const float DoorWidth = 2.4f;
    const float WallThickness = 0.4f;
    const float SeatSize = 0.55f;
    const float SeatPitch = 1.0f;
    const float RowPitch = 1.3f;

    const float FenceLength = 26f;
    const float FencePostGap = 1.6f;
    // Enough to read as a crowd without competing with the paddock's own 120 walkers for frame time —
    // CrowdBenchmarkTests is the thing to re-run before pushing any of these up.
    const int FanCount = 15;
    // A chair for every driver entered, but a body in only the first few rows: the room reads as full from
    // the door, and a hundred paper dolls in one place is a frame-rate problem, not a fidelity one.
    const int SeatedDrivers = 24;

    const float StageWidth = 12f;
    const float StageDepth = 5f;

    // The winner's circle: a chequered square to be photographed standing in, fenced off with crowd
    // barriers, with the sponsors' boards round the outside of it.
    const float CircleSize = 9f;          // the chequered square itself
    const int CircleSquares = 6;          // squares across it
    const float BarrierRing = 12.5f;      // where the barriers stand, measured across
    const float BarrierLength = 2.4f;     // one barrier section
    const float BarrierThickness = 0.3f;
    const float BoardWidth = 4.5f;
    const float BoardDepth = 0.9f;

    Transform _root;
    readonly List<Material> _materials = new();

    static bool _hooked;

    // Installed per scene load, not once at boot. The builder's object is a plain scene object — it dies
    // with the scene it built, and the weekend deliberately reloads the race scene between practice,
    // qualifying and the race — so a one-shot RuntimeInitialize would place the venues on the first track
    // of the session and never again.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        TryInstall();
        if (_hooked) return;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, mode) =>
        {
            if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single) TryInstall();
        };
        _hooked = true;
    }

    static void TryInstall()
    {
        if (Instance != null) return;
        var go = new GameObject("WeekendVenueSites");
        Instance = go.AddComponent<WeekendVenueSites>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    IEnumerator Start()
    {
        // The paddock frame comes from the track, and the motorhome lot spawns a frame later than the track
        // does, so give the scene a moment to finish assembling itself before measuring it.
        yield return null;
        yield return null;

        if (!IsPaddockScene())
        {
            Debug.Log("WeekendVenues: not a paddock scene, nothing to place.");
            Destroy(gameObject);
            yield break;
        }
        Debug.Log("WeekendVenues: building venues into " + gameObject.scene.name + ".");

        // Wait for the motorhome lot to have actually parked its RVs, not just for the component to exist:
        // it waits on the database first, and every venue is placed relative to the end of that row. Build
        // before it lands and the drivers' room goes up around the player's own motorhome, which is exactly
        // what it did. Eight seconds, then build anyway off whatever is there.
        float wait = 8f;
        while (GameObject.Find("MotorhomeLotBoundary") == null && wait > 0f) { wait -= Time.deltaTime; yield return null; }

        // And for the garages, which the motorhome lot puts up once its own row exists. The plan meeting is
        // held at the player's own rig, so building before they are parked would fall back to pit road.
        float garageWait = 5f;
        while (garageWait > 0f && (PopupGarageLot.Instance == null || PopupGarageLot.Instance.Rigs.Count == 0))
        {
            garageWait -= Time.deltaTime;
            yield return null;
        }
        if (PlayerMotorhome() == Vector3.zero)
            Debug.LogWarning("WeekendVenueSites: no motorhome parked yet — venues are placed off the paddock " +
                             "alone and the debrief has no venue.");

        Build();
    }

    static bool IsPaddockScene() =>
        FindFirstObjectByType<PaddockSpawner>() != null || FindFirstObjectByType<PitLaneStart>() != null;

    void Build()
    {
        _root = new GameObject("WeekendVenues").transform;
        RuntimeHierarchy.Adopt(_root.gameObject, HierarchyGroup.Environment);

        // Authored places first. Anything the track says for itself becomes a real anchor before a single
        // venue is generated, and every builder below is already guarded by WeekendVenueAnchor.Exists — so
        // an authored marker simply means that venue is never computed from geometry at all.
        AdoptAuthoredMarkers();

        PlaceGrandstandSeats();
        PlacePitBox();
        PlaceMotorhome();

        if (PaddockSpawner.TryGetArea(out var centre, out var along, out var outward, out float halfLen, out float halfDepth))
        {
            // Paddock-local frame: +along runs with the pit lane, +outward runs away from the racetrack, so
            // the far edge (outward) is the public side and the near edge is pit road.
            //
            // Everything is laid out from the END OF THE MOTORHOME ROW rather than from the middle of the
            // paddock. The lot is where the player is stood, and a drivers' room measured from the paddock's
            // own centre landed on top of their RV — four solid walls around the door they were trying to
            // walk out of.
            float basis = FirstFreeSpaceBeside(centre, along, halfLen, out float step);

            PlaceDriversRoom(centre, along, outward, halfLen, halfDepth, basis + step * 0.6f);
            PlaceHospitality(centre, along, outward, halfLen, halfDepth, basis + step * 1.5f);
            PlaceFanFence(centre, along, outward, halfLen, halfDepth, basis + step * 2.3f);
            PlaceIntroStage(centre, along, outward, halfLen, halfDepth, basis + step * 3.1f);
        }
        else
        {
            Debug.LogWarning("WeekendVenueSites: no paddock rectangle at this track — the drivers' room, the " +
                             "fan fence, the winner's circle and the intro stage were not placed. Bake a " +
                             "paddock for it (Draftmaster > Tracks) or author the anchors in the package.");
        }

        StaffTheVenues();

        // The places exist now, so the weekend can point at one. The director books on scene load too, but
        // that happens before any of this is standing — with nowhere to walk to yet, it books nothing, and
        // the player would be left in a paddock with no objective on it.
        WeekendDirector.BookNextUp();
    }

    // ------------------------------------------------------------------ the people who work there

    // A host at every venue that is a conversation: the person you walk up to and press the action button
    // on. They are there whether or not anything is booked — a crew chief is at the box all weekend — and
    // they simply have nothing for you when the timetable does not.
    void StaffTheVenues()
    {
        // Who works where is data (WeekendVenueCast), not a list in here, so the editor's weekend cast
        // window can show the same people without entering play mode.
        foreach (var host in WeekendVenueCast.All)
            Host(host.venue, host.speaker, host.offsetAlong, new[] { host.idleLine });
    }

    // Stand a host beside the venue's anchor, offset along the paddock so the player is not walking into
    // them to reach the mark.
    void Host(WeekendVenue venue, string speaker, float offsetAlong, string[] idle)
    {
        var anchor = WeekendVenueAnchor.Find(venue);
        if (anchor == null) return;

        // Somebody already authored a host here (a track package can), so leave it alone.
        foreach (var existing in FindObjectsByType<WeekendVenueHost>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (existing.venue == venue) return;

        // The debrief is had sitting down in your own motorhome, so the engineer is INSIDE it — parented
        // into the interior's masked frame at the dinette, across the table from the laptop. Anywhere else
        // in that scene-within-a-scene and he would draw over the blackout with the rest of the world.
        if (venue == WeekendVenue.Motorhome)
        {
            var rv = FindFirstObjectByType<RVInterior>();
            if (rv != null && rv.InteriorRoot != null)
            {
                var seated = PaddockPerson.SpawnTalker<WeekendVenueHost>(
                    rv.InteriorRoot, Vector3.zero, "Host_" + venue, speaker.GetHashCode(), speaker, idle,
                    interactRange: 1.8f, heightM: PaddockPerson.SeatedHeightM);
                seated.transform.localPosition = new Vector3(RVInterior.TableLocal.x + 0.75f,
                                                            RVInterior.TableLocal.y,
                                                            RVInterior.InteriorPropZ - 0.05f);
                seated.venue = venue;
                seated.idleLines = idle;
                return;
            }
        }

        Vector3 at = anchor.transform.position + new Vector3(offsetAlong, 1.1f, 0f);
        at.z = PaddockPerson.GroundZ;
        if (PaddockBoundary.AnyActive && venue != WeekendVenue.Grandstand)
        {
            Vector2 clamped = PaddockBoundary.Constrain(at);
            at = new Vector3(clamped.x, clamped.y, at.z);
        }

        var host = PaddockPerson.SpawnTalker<WeekendVenueHost>(
            _root, at, "Host_" + venue, speaker.GetHashCode(), speaker, idle, interactRange: 2.6f);
        host.venue = venue;
        host.idleLines = idle;
    }

    // ------------------------------------------------------------------ venues the track authored

    // Turn every WeekendMarker in the loaded track into a venue anchor.
    //
    // This runs before everything else in Build() and is the reason a hand-placed object beats the runtime's
    // guesswork: the generated venues all bail out early when an anchor for their kind already exists, so
    // adopting the authored ones first is the whole override mechanism. Nothing downstream is a special case
    // — the objective marker, the travel shortcut and the arrival test all read WeekendVenueAnchor and
    // cannot tell an authored venue from a computed one.
    void AdoptAuthoredMarkers()
    {
        // The naming convention, resolved first: an object called PitBox_Marker in the track package gets its
        // component here, so drawing a sprite and naming it is genuinely the whole workflow.
        var package = TrackPackage.Active;
        WeekendMarker.AdoptNamedObjects(package != null ? package.transform : null);

        int adopted = 0;
        foreach (var marker in WeekendMarker.All)
        {
            if (marker == null) continue;

            // The anchor sits ON the marker, so the objective arrow points at the shape somebody drew and the
            // arrival test is that shape. Where the player ENDS UP is the marker's stand position, which is
            // the teleport target when it has one — the seat across the track from a gate you can walk to.
            var anchor = PaddockProps.Anchor(_root, marker.venue, marker.MarkerPosition, marker.StandPosition,
                                             marker.Range, marker.label);
            anchor.markerLocation = marker.name;
            anchor.marker = marker;
            anchor.name = "Venue_" + marker.name;

            // A marker that stands in for somewhere unreachable gets the door that leads there.
            if (marker.HasTeleport)
            {
                MakeSomewhereToStand(marker);

                var gate = anchor.gameObject.AddComponent<WeekendMarkerGate>();
                gate.destination = marker.teleportTo;
                gate.marker = marker;
                gate.venue = marker.venue;
                gate.speakerName = marker.Label.ToUpperInvariant();
                gate.interactRange = Mathf.Max(3f, marker.Range);
            }

            adopted++;
        }

        if (adopted > 0)
            Debug.Log($"WeekendVenues: {adopted} authored marker(s) in this track — those venues are not generated.");
    }

    // Somewhere to stand at the far end of a teleport.
    //
    // The on-foot player is clamped to the walkable area, and a gate's destination is by definition outside
    // it — that is why the gate exists. Arriving there, the clamp read the player as somebody who had been
    // pushed through a fence and hauled them back to the nearest paddock edge: at Watkins Glen the seat is
    // across the circuit, so the walk to the stands ended just past the end of pit road.
    //
    // So the destination gets a walkable pocket of its own. Boundaries are already additive — inside any of
    // them counts — which is exactly the disjoint "paddock plus a viewing area" case they were built for.
    void MakeSomewhereToStand(WeekendMarker marker)
    {
        Vector3 seat = marker.TeleportPosition;
        if (PaddockBoundary.IsInside(seat)) return;

        PaddockBoundary.Pocket(_root, "ViewingPocket_" + marker.name, seat, new Vector2(14f, 10f));
        Debug.Log($"WeekendVenues: '{marker.name}' sends the player outside the paddock, so a walkable " +
                  $"pocket was put around where it lands ({seat.x:0}, {seat.y:0}).");
    }

    // ------------------------------------------------------------------ venues found on what exists

    // The plan meeting happens at the team's garage — the rig in the paddock carrying the number on the
    // player's car, with the car under its canopy and the room behind its door.
    //
    // It used to be held at the pit box marker, which is a stall painted on the racing surface of pit road.
    // Constrained to the walkable area, that put the crew chief on the pit lane at the entrance to it:
    // somewhere no team meets, and somewhere the player is standing in the way of a car coming in. The
    // garage is where the team is, and it is the first place the demo sends anybody.
    //
    // Pit road is still the fallback, for a track with no garage lot in it.
    void PlacePitBox()
    {
        if (WeekendVenueAnchor.Exists(WeekendVenue.PitBox)) return;

        if (TryPlayerGarage(out Vector3 garage))
        {
            PaddockProps.Anchor(_root, WeekendVenue.PitBox, garage, garage, arriveRange: 4f);
            return;
        }

        var box = FindFirstObjectByType<PlayerPitBoxMarker>();
        if (box == null) return;

        Debug.LogWarning("WeekendVenueSites: no garage for the player's car, so the plan meeting is at the " +
                         "pit box on pit road instead.");
        Vector3 at = Walkable(box.transform.position);
        PaddockProps.Anchor(_root, WeekendVenue.PitBox, at, at, arriveRange: 5f);
    }

    // Standing room at the player's garage: out of the door, on the canopy side, which is the side the
    // walkway runs down and the side the car is parked under. Far enough out not to be inside the doorway
    // the player walks through to reach the meeting room behind it.
    static bool TryPlayerGarage(out Vector3 at)
    {
        at = Vector3.zero;

        var lot = PopupGarageLot.Instance;
        if (lot == null || !lot.TryGetPlayerRig(out var rig) || rig == null) return false;

        Vector3 outside = rig.DoorWorldPosition + (Vector3)(rig.DoorWorldDirection * 2.5f);
        at = Walkable(outside);
        return true;
    }

    // The debrief is had sitting down in your own motorhome — the anchor is its door, and the conversation
    // itself happens inside, through the same doorway the player already uses.
    void PlaceMotorhome()
    {
        if (WeekendVenueAnchor.Exists(WeekendVenue.Motorhome)) return;

        Vector3 door = PlayerMotorhome();
        if (door == Vector3.zero)
        {
            Debug.LogWarning("WeekendVenueSites: no player motorhome in this scene, so there is nowhere to " +
                             "debrief. Session debriefs will have no venue at this track.");
            return;
        }

        PaddockProps.Anchor(_root, WeekendVenue.Motorhome, Walkable(door), Walkable(door), arriveRange: 4f);
    }

    // The player's own motorhome door: the lot's player slot when it has built one, otherwise the RV shell
    // that PitLaneStart spawns them outside of. Vector3.zero = neither is up yet.
    static Vector3 PlayerMotorhome()
    {
        var lot = DriverMotorhomeLot.Instance;
        if (lot != null)
            foreach (var slot in lot.Slots)
                if (slot != null && slot.isPlayer) return slot.doorPosition;

        var rv = FindFirstObjectByType<RVExterior>();
        if (rv != null) return rv.DoorWorldPosition;

        return Vector3.zero;
    }

    // A seat in front of every generated grandstand. Spectating is sitting in the crowd watching the cars
    // go past, so the seat is on the stand's front row, facing the road.
    void PlaceGrandstandSeats()
    {
        // The same guard every other builder has. Without it an authored Grandstand_Marker was adopted and
        // then a generated seat was added beside it, so the track's own answer and the runtime's guess both
        // existed and whichever registered first won the lookup — which made authoring a grandstand the one
        // venue a track could not actually override.
        if (WeekendVenueAnchor.Exists(WeekendVenue.Grandstand)) return;

        var stands = FindObjectsByType<Grandstand>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (stands == null || stands.Length == 0) return;

        int placed = 0;
        Vector3 gatePoint = Vector3.zero;
        float gateReach = float.MaxValue;

        foreach (var stand in stands)
        {
            if (stand == null) continue;

            // Local +Y is the stand's depth, and the stand is turned to face the road, so the front row is
            // half a depth toward the track from its centre.
            Vector3 front = stand.transform.TransformPoint(new Vector3(0f, -stand.depth * 0.5f + 1.2f, 0f));
            front.z = 0f;

            // A stand out beyond the paddock fence is a seat the player cannot walk to, and an objective
            // marker on the far side of a wall is worse than no marker at all. Remember the walkable point
            // nearest it — that is the way out towards it — and let the gate below stand in for the lot.
            Vector3 reachable = Walkable(front);
            if (Vector2.Distance(reachable, front) > 1f)
            {
                float reach = Vector2.Distance(reachable, front);
                if (reach < gateReach) { gateReach = reach; gatePoint = reachable; }
                continue;
            }

            // Anchored on the walkable point rather than the front row itself: inside the paddock the two
            // are the same spot, and where the stand is a metre the wrong side of the fence this keeps the
            // marker on the player's side of it. (WeekendVenueAnchor enforces that anyway — this just means
            // the seat is authored where it will end up.)
            var anchor = PaddockProps.Anchor(_root, WeekendVenue.Grandstand, reachable, reachable,
                                             arriveRange: 4f,
                                             label: "the " + stand.name.Replace('_', ' ').ToLowerInvariant());

            // The seat itself is the interaction: walk up, press the action button, sit down and watch.
            var seat = anchor.gameObject.AddComponent<GrandstandSeat>();
            seat.speakerName = "GRANDSTAND";
            seat.interactRange = 3.2f;
            placed++;
        }

        // Every stand at this circuit is outside the paddock. The sheet still says be in one, so the
        // objective becomes the way out towards them: walk to the gate, press the action button, and the
        // walk to the stand happens behind the wipe GrandstandSeat plays.
        if (placed == 0 && gateReach < float.MaxValue)
        {
            var gate = PaddockProps.Anchor(_root, WeekendVenue.Grandstand, gatePoint, gatePoint,
                                           arriveRange: 4f, label: "the gate to the grandstands");
            var gateSeat = gate.gameObject.AddComponent<GrandstandSeat>();
            gateSeat.speakerName = "GRANDSTAND";
            gateSeat.interactRange = 3.2f;
            placed++;
            Debug.Log("WeekendVenues: the stands are outside the paddock — watching a session starts at the gate.");
        }

        if (placed > 0) Debug.Log($"WeekendVenues: {placed} grandstand seat(s) to watch a session from.");
    }

    // ------------------------------------------------------------------ venues built into the paddock

    // The drivers' room: four walls with a door in the pit-side face, a top table across the back, and a
    // seat for every driver entered at this circuit. Parked at the far end of the paddock so it is a walk
    // rather than a step, and inset from the boundary so the player can get round it.
    void PlaceDriversRoom(Vector3 centre, Vector3 along, Vector3 outward, float halfLen, float halfDepth,
                          float alongOffset)
    {
        if (WeekendVenueAnchor.Exists(WeekendVenue.MeetingRoom)) return;

        Vector3 roomCentre = centre + along * AlongOffset(alongOffset, halfLen, RoomWidth * 0.5f)
                                    + outward * Mathf.Max(0f, halfDepth - RoomDepth * 0.75f);

        var room = new GameObject("DriversRoom");
        room.transform.SetParent(_root, false);
        room.transform.position = Walkable(roomCentre);
        room.transform.rotation = FrameRotation(outward);

        var floorMat = Mat(new Color(0.30f, 0.31f, 0.34f));
        var wallMat = Mat(new Color(0.17f, 0.18f, 0.21f));
        var tableMat = Mat(new Color(0.42f, 0.30f, 0.20f));
        var seatMat = Mat(new Color(0.20f, 0.22f, 0.27f));

        PaddockProps.Quad(room.transform, "Floor", Vector2.zero, new Vector2(RoomWidth, RoomDepth),
                          PaddockProps.FloorZ, floorMat);

        float hx = RoomWidth * 0.5f, hy = RoomDepth * 0.5f;
        // Back wall (away from pit road) and the two ends are solid; the pit-side wall has the doorway.
        PaddockProps.Quad(room.transform, "WallBack", new Vector2(0f, hy), new Vector2(RoomWidth + WallThickness, WallThickness),
                          PaddockProps.WallZ, wallMat, solid: true);
        PaddockProps.Quad(room.transform, "WallLeft", new Vector2(-hx, 0f), new Vector2(WallThickness, RoomDepth),
                          PaddockProps.WallZ, wallMat, solid: true);
        PaddockProps.Quad(room.transform, "WallRight", new Vector2(hx, 0f), new Vector2(WallThickness, RoomDepth),
                          PaddockProps.WallZ, wallMat, solid: true);

        float segment = (RoomWidth - DoorWidth) * 0.5f;
        float segmentCentre = (DoorWidth + segment) * 0.5f;
        PaddockProps.Quad(room.transform, "WallFrontL", new Vector2(-segmentCentre, -hy), new Vector2(segment, WallThickness),
                          PaddockProps.WallZ, wallMat, solid: true);
        PaddockProps.Quad(room.transform, "WallFrontR", new Vector2(segmentCentre, -hy), new Vector2(segment, WallThickness),
                          PaddockProps.WallZ, wallMat, solid: true);

        // The top table the officials and the press sit behind, across the back of the room.
        PaddockProps.Quad(room.transform, "TopTable", new Vector2(0f, hy - 1.6f), new Vector2(RoomWidth - 4f, 1.0f),
                          PaddockProps.PropZ, tableMat);
        LocationTitle.Attach(room, "DRIVERS' ROOM", RoomWidth * 0.7f, "Drivers' meeting and the press");

        int seats = BuildSeats(room.transform, seatMat, hy);
        SeatTheDrivers(room.transform, hy, seats);

        // Stand the player just inside the door, facing the table.
        Vector3 door = Walkable(room.transform.TransformPoint(new Vector3(0f, -hy + 0.9f, 0f)));
        PaddockProps.Anchor(_root, WeekendVenue.MeetingRoom, door, door, arriveRange: 4f);

        Debug.Log($"WeekendVenues: drivers' room with {seats} seat(s) — one per driver entered.");
    }

    // A chair per driver at the circuit, laid out in rows facing the top table. Every entry in all three
    // championships is here, because a drivers meeting is every driver in the building.
    int BuildSeats(Transform room, Material seatMat, float halfDepth)
    {
        int drivers = DriversAtTheCircuit();
        if (drivers <= 0) return 0;

        int perRow = Mathf.Max(6, Mathf.FloorToInt((RoomWidth - 3f) / SeatPitch));
        int rows = Mathf.CeilToInt(drivers / (float)perRow);

        var seatsRoot = new GameObject("Seats");
        seatsRoot.transform.SetParent(room, false);

        int placed = 0;
        for (int row = 0; row < rows && placed < drivers; row++)
        {
            int inThisRow = Mathf.Min(perRow, drivers - placed);
            float rowWidth = (inThisRow - 1) * SeatPitch;
            float y = halfDepth - 3.6f - row * RowPitch;

            for (int i = 0; i < inThisRow; i++)
            {
                float x = -rowWidth * 0.5f + i * SeatPitch;
                PaddockProps.Quad(seatsRoot.transform, $"Seat_{placed}", new Vector2(x, y),
                                  new Vector2(SeatSize, SeatSize), PaddockProps.PropZ, seatMat);
                placed++;
            }
        }
        return placed;
    }

    // Bodies in the front rows. The seats are laid out for everybody entered; these are the ones you can
    // see from the doorway, and they are why the room feels like a drivers meeting rather than a store
    // cupboard with chairs in it.
    void SeatTheDrivers(Transform room, float halfDepth, int seats)
    {
        if (seats <= 0) return;

        var seated = new GameObject("SeatedDrivers");
        seated.transform.SetParent(room, false);

        int perRow = Mathf.Max(6, Mathf.FloorToInt((RoomWidth - 3f) / SeatPitch));
        int bodies = Mathf.Min(SeatedDrivers, seats);

        for (int i = 0; i < bodies; i++)
        {
            int row = i / perRow;
            int col = i % perRow;
            int inThisRow = Mathf.Min(perRow, bodies - row * perRow);
            float rowWidth = (inThisRow - 1) * SeatPitch;
            float x = -rowWidth * 0.5f + col * SeatPitch;
            float y = halfDepth - 3.6f - row * RowPitch;

            var body = PaddockPerson.Spawn(seated.transform, Vector3.zero, $"SeatedDriver_{i}", 7700 + i,
                                           heightM: PaddockPerson.SeatedHeightM);
            body.transform.localPosition = new Vector3(x, y + 0.25f, PaddockProps.PropZ - 0.1f);
        }
    }

    // How many drivers are at this circuit this weekend: the three championships' field sizes. The roster
    // is the authority when it is up; the catalogue's entry counts are the fallback so a room still has the
    // right number of chairs before the database is ready.
    static int DriversAtTheCircuit()
    {
        int total = 0;
        foreach (var series in SeriesCatalog.All) total += SeriesCatalog.FieldSize(series);
        return Mathf.Clamp(total, 6, 120);
    }

    // The fan fence: a run of barrier along the public edge of the paddock, with the crowd on the far side
    // of it. You sign from the inside — that is the whole point of the fence being there.
    void PlaceFanFence(Vector3 centre, Vector3 along, Vector3 outward, float halfLen, float halfDepth,
                       float alongOffset)
    {
        bool authored = AuthoredSpot(WeekendVenue.SigningFence, out Vector3 at, out Quaternion facing);
        if (!authored)
        {
            at = Walkable(centre + along * AlongOffset(alongOffset, halfLen, FenceLength * 0.5f)
                                 + outward * (halfDepth - 1.5f));
            facing = FrameRotation(outward);
        }

        var fence = new GameObject("FanFence");
        fence.transform.SetParent(_root, false);
        fence.transform.position = at;
        fence.transform.rotation = facing;

        var railMat = Mat(new Color(0.72f, 0.74f, 0.78f));
        var postMat = Mat(new Color(0.35f, 0.36f, 0.40f));

        // The rail runs along the paddock edge (local X) and is solid, so the player cannot walk through it.
        PaddockProps.Quad(fence.transform, "Rail", Vector2.zero, new Vector2(FenceLength, 0.25f),
                          PaddockProps.PropZ, railMat, solid: true);

        int posts = Mathf.Max(2, Mathf.RoundToInt(FenceLength / FencePostGap));
        for (int i = 0; i <= posts; i++)
        {
            float x = -FenceLength * 0.5f + i * (FenceLength / posts);
            PaddockProps.Quad(fence.transform, $"Post_{i}", new Vector2(x, 0f), new Vector2(0.18f, 0.55f),
                              PaddockProps.PropZ - 0.02f, postMat);
        }

        LocationTitle.Attach(fence, "FAN ZONE", FenceLength * 0.5f, "Signing sessions");

        // The crowd, on the public side of the rail. They are the reason the fence is there and the reason
        // signing is done stood at it — you are on the inside, they are on the outside, and the barrier is
        // between you the whole time.
        var crowd = new GameObject("FanCrowd");
        crowd.transform.SetParent(fence.transform, false);
        for (int i = 0; i < FanCount; i++)
        {
            float t = FanCount == 1 ? 0.5f : i / (float)(FanCount - 1);
            float x = Mathf.Lerp(-FenceLength * 0.45f, FenceLength * 0.45f, t);
            float y = 0.9f + (i % 3) * 0.75f;     // three deep, like a real fence on a Friday
            var body = PaddockPerson.Spawn(crowd.transform, Vector3.zero, $"Fan_{i}", 9100 + i,
                                           heightM: PaddockPerson.HeightM);
            body.transform.localPosition = new Vector3(x, y, PaddockProps.PropZ - 0.1f);
        }

        // Stand the driver on the paddock side of the rail; the fans queue up on the other one. With an
        // authored marker the anchor is already there, and the rail was built around it.
        if (authored) return;
        Vector3 inside = Walkable(fence.transform.TransformPoint(new Vector3(0f, -1.1f, 0f)));
        PaddockProps.Anchor(_root, WeekendVenue.SigningFence, inside, inside, arriveRange: 4.5f);
    }

    // The winner's circle: where a driver is stood to be photographed.
    //
    // This was a hospitality awning, and it did not work for the one reason a place in this game has to:
    // you could not see the player in it. The canopy was a nine-by-eight slab of prop laid over the exact
    // ground the player had to stand on, so walking to the mark meant walking under a lid and disappearing.
    //
    // So it is built the other way round now. The middle is FLOOR — a chequered square, drawn behind
    // everybody — and every solid thing is outside it: crowd barriers ringing the square with a gap to walk
    // in through, the sponsors' boards behind them, a billboard across the back. The player stands on the
    // chequers with nothing over them, which is the whole point of a photograph.
    //
    // Placeholder art, deliberately: flat colour blocked out to be walked around now and repainted later,
    // the same way the drivers' room and the intro stage were.
    void PlaceHospitality(Vector3 centre, Vector3 along, Vector3 outward, float halfLen, float halfDepth,
                          float alongOffset)
    {
        bool authored = AuthoredSpot(WeekendVenue.SponsorSuite, out Vector3 at, out Quaternion facing);
        if (!authored)
        {
            at = Walkable(centre + along * AlongOffset(alongOffset, halfLen, BarrierRing * 0.5f)
                                 - outward * Mathf.Min(halfDepth * 0.25f, halfDepth - BarrierRing * 0.5f));
            facing = FrameRotation(outward);
        }

        var circle = new GameObject("WinnersCircle");
        circle.transform.SetParent(_root, false);
        circle.transform.position = at;
        circle.transform.rotation = facing;

        PaddockProps.Chequers(circle.transform, "Chequers", Vector2.zero, CircleSize, CircleSquares,
                              Mat(new Color(0.90f, 0.90f, 0.88f)), Mat(new Color(0.13f, 0.13f, 0.14f)));

        BarrierRingAround(circle.transform);

        // The sponsors' furniture: two boards at the back corners and a billboard across the back, all
        // outside the barriers so none of them can be stood in front of the player.
        var boardFace = Mat(new Color(0.82f, 0.80f, 0.76f));
        float back = BarrierRing * 0.5f + 1.2f;
        float side = BarrierRing * 0.5f - BoardWidth * 0.35f;

        // Behind everybody, like the chequers: these stand at the BACK of the shot, and a board that draws
        // over the person being photographed is the bug this whole place was rebuilt to fix.
        const int BehindPeople = -10;

        PaddockProps.Quad(circle.transform, "Billboard", new Vector2(0f, back + 1.4f),
                          new Vector2(BoardWidth * 2f, BoardDepth * 1.6f), PaddockProps.WallZ, boardFace,
                          sortingOrder: BehindPeople);
        PaddockProps.Sign(circle.transform, "SPONSOR BOARD", new Vector2(0f, back + 1.4f), BoardWidth * 1.8f,
                          new Color(0.16f, 0.16f, 0.18f));

        PaddockProps.Quad(circle.transform, "SponsorBoardLeft", new Vector2(-side, back),
                          new Vector2(BoardWidth, BoardDepth), PaddockProps.PropZ, boardFace,
                          sortingOrder: BehindPeople);
        PaddockProps.Sign(circle.transform, "SPONSOR", new Vector2(-side, back), BoardWidth * 0.8f,
                          new Color(0.16f, 0.16f, 0.18f));

        PaddockProps.Quad(circle.transform, "SponsorBoardRight", new Vector2(side, back),
                          new Vector2(BoardWidth, BoardDepth), PaddockProps.PropZ, boardFace,
                          sortingOrder: BehindPeople);
        PaddockProps.Sign(circle.transform, "SPONSOR", new Vector2(side, back), BoardWidth * 0.8f,
                          new Color(0.16f, 0.16f, 0.18f));

        LocationTitle.Attach(circle, "WINNER'S CIRCLE", BarrierRing, "Sponsor duty");

        // The mark is the middle of the chequers: stand there and the boards are behind you. An authored
        // marker already put an anchor here — this is the same spot, so there is nothing to add.
        if (authored) return;
        Vector3 mark = Walkable(circle.transform.TransformPoint(Vector3.zero));
        PaddockProps.Anchor(_root, WeekendVenue.SponsorSuite, mark, mark, arriveRange: 4f);
    }

    // Crowd barriers round the square, with the front left open to walk in through.
    //
    // Solid, so they read as barriers rather than as paint — the player has to go round to the gap, which
    // is what makes the middle of it feel like somewhere you were put rather than somewhere you wandered.
    void BarrierRingAround(Transform circle)
    {
        var metal = Mat(new Color(0.62f, 0.64f, 0.67f));
        float half = BarrierRing * 0.5f;
        int perSide = Mathf.Max(2, Mathf.RoundToInt(BarrierRing / BarrierLength));
        float step = BarrierRing / perSide;
        float first = -half + step * 0.5f;

        for (int i = 0; i < perSide; i++)
        {
            float at = first + i * step;
            var run = new Vector2(step - 0.25f, BarrierThickness);
            var stand = new Vector2(BarrierThickness, step - 0.25f);

            PaddockProps.Quad(circle, $"Barrier_Back_{i}", new Vector2(at, half), run,
                              PaddockProps.PropZ, metal, solid: true);
            PaddockProps.Quad(circle, $"Barrier_Left_{i}", new Vector2(-half, at), stand,
                              PaddockProps.PropZ, metal, solid: true);
            PaddockProps.Quad(circle, $"Barrier_Right_{i}", new Vector2(half, at), stand,
                              PaddockProps.PropZ, metal, solid: true);

            // The front rail is the way in: leave the middle two sections out of it.
            bool gap = i == perSide / 2 || i == (perSide - 1) / 2;
            if (!gap)
                PaddockProps.Quad(circle, $"Barrier_Front_{i}", new Vector2(at, -half), run,
                                  PaddockProps.PropZ, metal, solid: true);
        }
    }

    // The intro stage: a platform at the pit-road end of the paddock, where the field is announced.
    void PlaceIntroStage(Vector3 centre, Vector3 along, Vector3 outward, float halfLen, float halfDepth,
                         float alongOffset)
    {
        if (WeekendVenueAnchor.Exists(WeekendVenue.IntroStage)) return;

        Vector3 stageCentre = centre + along * AlongOffset(alongOffset, halfLen, StageWidth * 0.5f)
                                     - outward * Mathf.Max(0f, halfDepth - StageDepth * 0.6f);

        var stage = new GameObject("IntroStage");
        stage.transform.SetParent(_root, false);
        stage.transform.position = Walkable(stageCentre);
        stage.transform.rotation = FrameRotation(outward);

        PaddockProps.Quad(stage.transform, "Deck", Vector2.zero, new Vector2(StageWidth, StageDepth),
                          PaddockProps.FloorZ, Mat(new Color(0.24f, 0.25f, 0.29f)));
        PaddockProps.Quad(stage.transform, "Backdrop", new Vector2(0f, StageDepth * 0.5f - 0.5f),
                          new Vector2(StageWidth, 1.0f), PaddockProps.PropZ, Mat(new Color(0.13f, 0.35f, 0.58f)));
        LocationTitle.Attach(stage, "DRIVER INTRODUCTIONS", StageWidth, "The stage at the end of pit road");

        Vector3 mark = Walkable(stage.transform.TransformPoint(new Vector3(0f, -StageDepth * 0.25f, 0f)));
        PaddockProps.Anchor(_root, WeekendVenue.IntroStage, mark, mark, arriveRange: 4.5f);
    }

    // Where a venue the track authored for itself actually sits.
    //
    // A marker used to mean "this venue is placed, build nothing" — which is right for a place that is a
    // real object in the package (a grandstand gate) and wrong for one whose whole appearance is generated
    // (the winner's circle, the fan fence). Those two are a pile of props with an anchor in the middle, so
    // an authored marker moves the props rather than suppressing them: the track says where and which way
    // round, and the builder puts the same thing there.
    //
    // The position is taken as authored, NOT clamped into the walkable area. Somebody who drags a marker
    // has decided where it goes, and the fan fence in particular belongs ON the paddock edge with the
    // crowd outside it.
    static bool AuthoredSpot(WeekendVenue venue, out Vector3 position, out Quaternion rotation)
    {
        var marker = WeekendMarker.Find(venue);
        if (marker == null)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        Vector3 at = marker.MarkerPosition;
        position = new Vector3(at.x, at.y, 0f);
        rotation = marker.transform.rotation;
        return true;
    }

    // ------------------------------------------------------------------ helpers

    // Where the venue cluster starts: just past the end of the motorhome row, on whichever side of it has
    // more paddock left, walking away from the RVs. `step` is how far apart consecutive venues are, and it
    // carries the direction — negative when the cluster runs the other way.
    //
    // Measured off the lot's own boundary (DriverMotorhomeLot builds a MotorhomeLotBoundary polygon around
    // every RV it parks), because that is the one piece of the paddock that is definitely occupied.
    static float FirstFreeSpaceBeside(Vector3 centre, Vector3 along, float halfLen, out float step)
    {
        const float Gap = 14f;        // clear air between the last motorhome and the first venue
        const float Spacing = 26f;    // between venues: separate places, still a short walk

        float lotMin = 0f, lotMax = 0f;
        var lot = GameObject.Find("MotorhomeLotBoundary");
        var bounds = lot != null ? lot.GetComponent<Collider2D>() : null;
        if (bounds != null)
        {
            var b = bounds.bounds;
            // Project the lot's corners onto the paddock's long axis to find how much of it the RVs eat.
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector3(i < 2 ? b.min.x : b.max.x, (i % 2 == 0) ? b.min.y : b.max.y, 0f);
                float t = Vector3.Dot(corner - centre, along);
                lotMin = i == 0 ? t : Mathf.Min(lotMin, t);
                lotMax = i == 0 ? t : Mathf.Max(lotMax, t);
            }
        }

        // Whichever end of the lot has more paddock behind it is where the cluster goes.
        float roomAhead = halfLen - lotMax;
        float roomBehind = lotMin + halfLen;
        if (roomAhead >= roomBehind) { step = Spacing; return lotMax + Gap; }
        step = -Spacing;
        return lotMin - Gap;
    }

    // Where along the paddock a venue sits: what we asked for, pulled back inside the paddock's own ends so
    // a short pit straight cannot push a building out onto the grass beyond it.
    static float AlongOffset(float wanted, float halfLen, float halfSize)
    {
        float limit = Mathf.Max(0f, halfLen - halfSize);
        return Mathf.Clamp(wanted, -limit, limit);
    }

    // Everything on foot lives on z = 0; the props carry their own depth in local z.
    static Vector3 Flat(Vector3 v) => new(v.x, v.y, 0f);

    // Pull a spot back inside the walkable paddock.
    //
    // The paddock RECTANGLE is derived from the pit lane and is only an approximation of the paddock the
    // player can actually walk in — the boundary polygon is authored, and at Watkins it is a good deal
    // smaller. A venue placed on the rectangle's far edge put its standing mark fifteen metres beyond the
    // boundary, where the player is clamped back the moment they try to reach it: an obligation you can see
    // and can never attend.
    static Vector3 Walkable(Vector3 wanted)
    {
        if (!PaddockBoundary.AnyActive) return Flat(wanted);
        Vector2 inside = PaddockBoundary.Constrain(wanted);
        return new Vector3(inside.x, inside.y, 0f);
    }

    // A rotation whose local +Y runs away from the racetrack and local +X therefore runs along the paddock,
    // so every building can be laid out in plain "width x depth" terms with its front facing pit road.
    //
    // Built from `outward` rather than from `along`: aligning +X with the paddock's direction leaves +Y
    // pointing at either edge depending on which way round the pit lane runs, which put the drivers' room's
    // doorway on the wrong side of the building at half the tracks in the calendar.
    static Quaternion FrameRotation(Vector3 outward) =>
        Quaternion.FromToRotation(Vector3.up, new Vector3(outward.x, outward.y, 0f).normalized);

    Material Mat(Color c)
    {
        var m = PaddockProps.Unlit(c);
        _materials.Add(m);
        return m;
    }
}
