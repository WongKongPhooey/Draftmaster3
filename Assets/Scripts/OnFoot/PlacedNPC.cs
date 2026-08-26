using System.Collections;
using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEngine;

// An NPC you place in the editor instead of spawning from code.
//
// Drop an empty GameObject wherever the person should stand — in the track package (`Paddock/NPCs`) for
// somebody who belongs to that track, or in the shared race scene for a member of the every-track cast —
// add this, and fill in who they are, what they say, how the player engages them, and the conditions under
// which they turn up at all. Nothing else needs wiring: the body is cloned from the on-foot prefab at
// runtime and the dialogue/cutscene components are attached to it.
//
// Three things are authored here that used to be hard-coded in a spawner:
//
//   WHERE   `anchor` — a fixed spot in the world, or a spot derived from geometry that differs per track
//           (a point along the pit lane, beside the player's parked car, outside the RV door). Geometry
//           anchors are what let one placed NPC work at all thirty-five tracks.
//   HOW     `interaction` — stand and be talked to, walk over and start a cutscene, or wait to be driven
//           by the scene flow (the crew chief's briefing, which fires when the player gets in the car).
//   WHEN    `appear` — the full AppearanceConditions block: session, series, track, career stat range,
//           quest state, inventory, repeat policy, chance.
//
// The NPC Director window (Draftmaster > NPCs > Director) lists every one of these, previews which of them
// show up in practice / qualifying / race, and says why the others don't.
public class PlacedNPC : MonoBehaviour
{
    // Beats the scene flow needs to find by name. Generic covers everyone else — most NPCs are Generic.
    public enum Role
    {
        Generic,
        PitGreeter,     // stands in the pit lane, chats, nothing depends on him
        RaceEngineer,   // opening beat: walks up as the session starts / as the player leaves the RV
        CrewChief,      // briefs the driver when they climb into the car, then opens the setup panel
        TeamLiaison,    // catches the driver on their way out of the motorhome with where they are due next
        ChiefStrategist,// the one with the run plan: fuel windows, tyre calls, what the race is going to be
        PRManager,      // the media and sponsor side of the driver's day
    }

    // Where the body actually ends up. Only `Here` uses this GameObject's own position.
    public enum Anchor
    {
        Here,        // exactly where you put it — for anything placed inside a track package
        PitLane,     // along the pit lane from the player's spawn point (along = metres up the lane)
        ParkedCar,   // relative to the player's car, re-derived as the car is re-parked
        RVDoor,      // out from the RV door; falls back to PlayerSpawn when the track has no RV
        PlayerSpawn, // relative to where the player spawned, on the line toward their car
    }

    public enum Interaction
    {
        TalkOnInteract, // walk up, press E (the ordinary case)
        WalkUpCutscene, // player freezes, bars come in, the NPC walks over and opens the conversation
        OnCarEntry,     // silent until the scene flow runs it (crew chief briefing)
        Silent,         // set dressing: a body, no dialogue
    }

    [Header("Identity")]
    [Tooltip("Stable id, e.g. 'daytona.promoter'. Used for the appearance save key when that's left blank, and as the row label in the NPC Director.")]
    public string npcId = "";
    [Tooltip("Name shown in the speech bubble.")]
    public string speakerName = "Crew Member";
    [Tooltip("Beats the scene flow looks up by name. Leave Generic unless this IS the engineer/chief/greeter.")]
    public Role role = Role.Generic;

    [Header("When they appear")]
    public AppearanceConditions appear = new AppearanceConditions();

    [Header("Where they stand")]
    [Tooltip("Here = this GameObject's position (place it in a track package). The others derive a position from geometry, so one NPC works at every track.")]
    public Anchor anchor = Anchor.Here;
    [Tooltip("Metres along the anchor's forward axis: up the pit lane, out from the RV door, or toward the car. Negative = behind.")]
    public float anchorAlong = 0f;
    [Tooltip("Metres sideways from the anchor. For pit-lane anchors, negative = away from the pit wall.")]
    public float anchorLateral = 0f;
    [Tooltip("Keep re-deriving the position every frame instead of placing once. Needed for anything anchored to the player's car — GridSpawner re-parks it into its fitted pit box several frames after the scene opens.")]
    public bool followAnchor = false;
    [Tooltip("Turn the body to this heading (degrees) when it spawns. Off = keep the prefab's default facing.")]
    public bool applyFacing = false;
    public float facingDeg = 0f;

    [Header("Dialogue")]
    [TextArea]
    [Tooltip("One line per interact. A line ending with \"#player\" is spoken by the driver in their own bubble.")]
    public string[] lines = { "Hey, good to see you in the pits." };

    // What they say when, across the three days.
    //
    // The core cast is stood in the same places all weekend, but a crew chief on Friday morning is talking
    // about a practice session that has not happened, and the same man on Sunday lunchtime is talking about
    // the race you are about to start. One flat list of lines cannot say both, so a marker can carry a set
    // per half-day; the first entry whose half-days include the one being played wins, and anything with no
    // matching entry falls back to `lines` above.
    [System.Serializable]
    public class ScheduledLines
    {
        [Tooltip("What this set is for, in the editor's list. Not shown to the player.")]
        public string label = "Friday";

        [Tooltip("Half-days this set is used in. None ticked = never used.")]
        public bool fridayAM, fridayPM, saturdayAM, saturdayPM, sundayAM, sundayPM;

        [TextArea]
        public string[] lines = { "" };

        [Tooltip("Objective banner shown when this set's walk-up beat ends. Empty = use the marker's own.")]
        public string objectiveOnFinish = "";

        public bool Covers(WeekendSlot slot) => slot switch
        {
            WeekendSlot.FridayAM => fridayAM,
            WeekendSlot.FridayPM => fridayPM,
            WeekendSlot.SaturdayAM => saturdayAM,
            WeekendSlot.SaturdayPM => saturdayPM,
            WeekendSlot.SundayAM => sundayAM,
            _ => sundayPM,
        };

        public void Set(WeekendSlot slot, bool on)
        {
            switch (slot)
            {
                case WeekendSlot.FridayAM: fridayAM = on; break;
                case WeekendSlot.FridayPM: fridayPM = on; break;
                case WeekendSlot.SaturdayAM: saturdayAM = on; break;
                case WeekendSlot.SaturdayPM: saturdayPM = on; break;
                case WeekendSlot.SundayAM: sundayAM = on; break;
                default: sundayPM = on; break;
            }
        }
    }

    [Tooltip("Per-half-day dialogue. The first set covering the half-day being played is used; with none, " +
             "the lines above are.")]
    public List<ScheduledLines> schedule = new();

    // The set for a given half-day, or null when nothing covers it.
    public ScheduledLines ScheduledFor(WeekendSlot slot)
    {
        for (int i = 0; i < schedule.Count; i++)
        {
            var set = schedule[i];
            if (set == null || set.lines == null || set.lines.Length == 0) continue;
            if (set.Covers(slot)) return set;
        }
        return null;
    }

    // What this NPC says in a given half-day, falling back to the flat list.
    public string[] LinesFor(WeekendSlot slot)
    {
        var set = ScheduledFor(slot);
        return set != null ? set.lines : lines;
    }
    [Tooltip("Loop back to the first line after the conversation ends.")]
    public bool repeatable = true;
    [Tooltip("Player must be this close (m) to talk.")]
    public float interactRange = 2.2f;
    [Header("Quest (optional)")]
    [Tooltip("Set a quest and this becomes a QuestGiverNPC: the dialogue above is their idle chat, and the four line sets below carry the quest conversation.")]
    public QuestInfo quest;
    [Tooltip("This NPC is the quest's delivery target rather than its giver.")]
    public bool isDeliveryTarget = false;
    [Tooltip("Item handed over when the player accepts. Empty = none.")]
    public string grantItemOnAccept = "";
    [TextArea] public string[] questOfferLines = { "Got a job for you." };
    [TextArea] public string[] questActiveLines = { "How's it coming along?" };
    [TextArea] public string[] questTurnInLines = { "You did it! Thanks." };
    [TextArea] public string[] questCompletedLines = { "Thanks again for the help." };
    [TextArea] public string[] questLockedLines = { "Come back once you've made a name for yourself." };

    [Header("Interaction")]
    public Interaction interaction = Interaction.TalkOnInteract;
    [Tooltip("Walk-up cutscene: wait for the player to step into the trigger below. Off = play as soon as the scene opens.")]
    public bool waitForTrigger = true;
    [Tooltip("Trigger offset from the anchor: x = along, y = sideways, same axes as the stand point. Drag the blue ring in the scene view.")]
    public Vector2 triggerOffset = new Vector2(2.6f, 0f);
    [Tooltip("Trigger radius (m).")]
    public float triggerRadius = 1.5f;
    [Tooltip("Walk-up cutscene: how close (m) the NPC gets before stopping and starting to talk.")]
    public float stopDistance = 1.2f;
    [Tooltip("Never fire while the player is still inside the RV interior mask. Keep on for any beat outside an RV door.")]
    public bool requireOutsideRV = true;
    [Tooltip("Objective banner shown when this NPC's cutscene ends. Empty = none.")]
    public string objectiveOnFinish = "";

    [Header("Look")]
    [Tooltip("Body to clone. Empty = the scene's on-foot prefab (PitLaneStart.onFootPrefab).")]
    public GameObject prefabOverride;

    // ---------------------------------------------------------------- runtime

    // Everything the geometry anchors need. PitLaneStart fills one in and hands it to BuildAll().
    public struct BuildContext
    {
        public GameObject prefab;
        public Transform player;
        public Transform car;
        public TrackBuilder track;
        public List<TrackBuilder.Sample> pitSamples;
        public bool usedPit;
        public float playerPitDistance;   // metres along the pit lane the player spawned at
        public Vector3 playerSpawnPos;
        public RVExterior rv;
        public RVInterior rvInterior;
        public float groundZ;             // world z for spawned bodies (NOT the player's — see RVInterior)
    }

    // Every placed NPC currently in a loaded scene, authored or not yet built.
    public static readonly List<PlacedNPC> All = new List<PlacedNPC>();

    // Fired when a placed NPC's walk-up cutscene finishes. PitLaneStart listens so it can put the next
    // objective on screen and let the control hints resume.
    public static event System.Action<PlacedNPC> CutsceneFinished;

    // True while any placed cutscene is armed or mid-play — control hints stay quiet until it clears.
    public static bool AnyCutsceneArmed { get; private set; }

    NPCInteractable _npc;
    Rigidbody2D _npcRb;
    NPCWalkUpCutscene _walkUp;
    CutsceneTrigger _trigger;
    BuildContext _ctx;
    bool _built, _claimed, _skipped;

    public NPCInteractable Interactable => _npc;
    public bool Built => _built;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // Look up one of the named beats. Returns only NPCs that actually got built this session.
    public static PlacedNPC Find(Role role)
    {
        for (int i = 0; i < All.Count; i++)
            if (All[i] != null && All[i].role == role && All[i]._built) return All[i];
        return null;
    }

    // Build every placed NPC that passes its conditions. Called once by the scene flow (PitLaneStart) with
    // a context the geometry anchors can resolve against.
    public static void BuildAll(BuildContext ctx)
    {
        // ToArray: a build can spawn objects, and an NPC's OnEnable would otherwise mutate the list we walk.
        foreach (var npc in All.ToArray())
            if (npc != null) npc.TryBuild(ctx);
    }

    // A scene with no PitLaneStart (the team garage, a menu diorama) still gets its `Here` NPCs: wait a frame
    // for a builder to claim us, then stand up on our own.
    IEnumerator Start()
    {
        yield return null;
        if (_built || _claimed || _skipped) yield break;

        var ctx = new BuildContext { groundZ = transform.position.z };
        var flow = FindFirstObjectByType<PitLaneStart>();
        if (flow != null) ctx.prefab = flow.onFootPrefab;
        var player = FindFirstObjectByType<OnFootController>();
        if (player != null) ctx.player = player.transform;
        TryBuild(ctx);
    }

    // Roll the conditions once and, if they pass, put a body in the world. Returns true if it appeared.
    public bool TryBuild(BuildContext ctx)
    {
        if (_built || _claimed) return _built;
        _claimed = true;
        _ctx = ctx;

        if (!appear.IsMet()) { _skipped = true; return false; }

        GameObject prefab = prefabOverride != null ? prefabOverride : ctx.prefab;
        if (prefab == null)
        {
            Debug.LogWarning($"PlacedNPC '{Label}': no body prefab (set prefabOverride, or put a PitLaneStart in the scene). Skipped.");
            _skipped = true;
            return false;
        }

        Vector3 pos = ResolveStandPoint();
        var body = NPCFactory.SpawnBody(prefab, pos, GameObjectName);
        _npc = quest != null ? BuildQuestGiver(body) : NPCFactory.AddTalker<NPCInteractable>(body, speakerName, lines);

        // A marker with a per-half-day script keeps its lines in step with the weekend's clock, which moves
        // while the scene is up — the schedule advances as bookings are completed, so what the crew chief
        // has to say changes without a reload.
        if (schedule != null && schedule.Count > 0)
        {
            var scheduled = body.AddComponent<ScheduledDialogue>();
            scheduled.marker = this;
            scheduled.speaker = _npc;
        }
        _npc.interactRange = interactRange;
        _npc.repeatable = repeatable;
        _npcRb = _npc.GetComponent<Rigidbody2D>();
        if (applyFacing)
            OnFootController.ApplyFacing(_npc.transform, _npc.GetComponent<Rigidbody2D>(),
                new Vector2(Mathf.Cos(facingDeg * Mathf.Deg2Rad), Mathf.Sin(facingDeg * Mathf.Deg2Rad)), 90f);

        _built = true;

        // A stationary NPC has appeared the moment they're stood there. A beat hasn't happened until it
        // plays: a cutscene marks itself from its trigger (walking past and back shouldn't burn it), and a
        // scene-flow beat waits for MarkPlayed() from whoever runs it.
        if (interaction == Interaction.WalkUpCutscene) ArmCutscene();
        else if (interaction != Interaction.OnCarEntry) appear.MarkSeen();

        return true;
    }

    // "This beat has now actually happened" — for interactions the scene flow drives (the crew chief's
    // briefing fires when the player climbs in, which may never happen).
    public void MarkPlayed() => appear.MarkSeen();

    QuestGiverNPC BuildQuestGiver(GameObject body)
    {
        var giver = NPCFactory.AddTalker<QuestGiverNPC>(body, speakerName, lines);
        giver.quest = quest;
        giver.offersQuest = !isDeliveryTarget;
        giver.isDeliveryTarget = isDeliveryTarget;
        giver.grantItemOnAccept = grantItemOnAccept;
        if (questOfferLines != null && questOfferLines.Length > 0) giver.offerLines = questOfferLines;
        if (questActiveLines != null && questActiveLines.Length > 0) giver.activeLines = questActiveLines;
        if (questTurnInLines != null && questTurnInLines.Length > 0) giver.turnInLines = questTurnInLines;
        if (questCompletedLines != null && questCompletedLines.Length > 0) giver.completedLines = questCompletedLines;
        if (questLockedLines != null && questLockedLines.Length > 0) giver.lockedLines = questLockedLines;
        return giver;
    }

    void ArmCutscene()
    {
        if (_ctx.player == null)
        {
            Debug.LogWarning($"PlacedNPC '{Label}': walk-up cutscene needs the on-foot player. Falling back to talk-on-interact.");
            appear.MarkSeen();
            return;
        }

        // A walk-over trigger only makes sense if there's something to walk out of. An RV-door beat at a
        // track whose package has no RV interior plays as the scene opens instead of waiting for a doorway
        // that isn't there.
        bool useTrigger = waitForTrigger && (anchor != Anchor.RVDoor || _ctx.rvInterior != null);

        var seq = new GameObject(name + "_Cutscene");
        seq.transform.position = useTrigger ? ResolveTriggerPoint() : _npc.transform.position;

        _walkUp = seq.AddComponent<NPCWalkUpCutscene>();
        _walkUp.player = _ctx.player.GetComponent<OnFootController>();
        _walkUp.npc = _npc;
        _walkUp.stopDistance = stopDistance;
        _walkUp.Finished = () =>
        {
            AnyCutsceneArmed = false;
            CutsceneFinished?.Invoke(this);
        };

        AnyCutsceneArmed = true;

        if (useTrigger)
        {
            _trigger = seq.AddComponent<CutsceneTrigger>();
            _trigger.radius = triggerRadius;
            _trigger.target = _ctx.player;
            if (requireOutsideRV && _ctx.rvInterior != null)
                _trigger.Gate = () => !_ctx.rvInterior.IsInside; // never fire behind the interior mask
            _trigger.Triggered = () => { appear.MarkSeen(); _walkUp.Play(); };
            return;
        }

        // Nothing to walk out of: he comes over as the scene opens, one frame later so the player has
        // finished spawning (OnFootController wires its input in its own Start).
        appear.MarkSeen();
        StartCoroutine(PlayNextFrame());
    }

    IEnumerator PlayNextFrame()
    {
        yield return null;
        if (_walkUp != null) _walkUp.Play();
    }

    void LateUpdate()
    {
        // Anything anchored to the car keeps up with it until somebody engages them — the car moves under
        // them for the first few frames of the scene, and again if it's re-parked.
        if (!_built || !followAnchor || _npc == null) return;
        if (_npc.IsTalking) return;

        Vector3 p = ResolveStandPoint();
        // Teleport the physics pose as well as the transform. A kinematic body that only had its transform
        // written keeps its own idea of where it is, and anything that syncs from the body wins.
        if (_npcRb != null && _npcRb.bodyType != RigidbodyType2D.Dynamic) _npcRb.position = p;
        _npc.transform.position = p;
    }

    // ---------------------------------------------------------------- anchors

    // Hand in the geometry the anchors read from without building anything. The editor calls this so the
    // scene view can draw an anchored NPC where they'd really stand, before play mode.
    public void SetContext(BuildContext ctx) { if (!_built) _ctx = ctx; }

    // World position this NPC stands at, for the anchor they're set to. Public so the editor gizmo draws
    // the resolved spot rather than the marker's own position.
    public Vector3 ResolveStandPoint() => ResolveOffset(anchorAlong, anchorLateral);

    public Vector3 ResolveTriggerPoint() => ResolveOffset(triggerOffset.x, triggerOffset.y);

    // Turn an (along, lateral) pair into a world point in the anchor's own frame.
    public Vector3 ResolveOffset(float along, float lateral)
    {
        switch (anchor)
        {
            case Anchor.PitLane:     return FromPitLane(_ctx.playerPitDistance + along, lateral, 0f);
            case Anchor.ParkedCar:   return FromCar(along, lateral);
            case Anchor.RVDoor:      return FromRVDoor(along, lateral);
            case Anchor.PlayerSpawn: return FromPlayerSpawn(along, lateral);
            default:                 return transform.position + new Vector3(along, lateral, 0f);
        }
    }

    // Sample the pit lane (or the main spline if the track has no pit lane) and step sideways off it.
    Vector3 FromPitLane(float distance, float lateral, float extraLateral)
    {
        var track = _ctx.track;
        var samples = _ctx.pitSamples;
        if (track == null || samples == null || samples.Count < 2) return transform.position;

        float end = samples[samples.Count - 1].distance;
        float d = Mathf.Clamp(distance, 0f, end);
        var sample = _ctx.usedPit ? track.SamplePitAt(d, samples) : track.SampleAt(d, samples);
        Vector2 off = sample.position + sample.normal * (lateral + extraLateral);
        Vector3 wp = track.transform.TransformPoint(new Vector3(off.x, off.y, 0f));
        return new Vector3(wp.x, wp.y, _ctx.groundZ);
    }

    // Beside the player's car: project the car onto the lane, then offset from the CAR's own lateral rather
    // than from the lane centre, so a car parked in its pit box doesn't leave the NPC out in the road.
    Vector3 FromCar(float along, float lateral)
    {
        var track = _ctx.track;
        if (_ctx.car == null) return transform.position;
        if (track == null || _ctx.pitSamples == null || _ctx.pitSamples.Count < 2)
            return _ctx.car.position + new Vector3(along, lateral, 0f);

        Vector3 carPos = _ctx.car.position;
        float carDist = _ctx.usedPit ? track.NearestPitDistance(carPos) : track.NearestCenterlineDistance(carPos);
        var carSample = _ctx.usedPit ? track.SamplePitAt(carDist, _ctx.pitSamples) : track.SampleAt(carDist, _ctx.pitSamples);

        Vector2 carLocal = track.transform.InverseTransformPoint(carPos);
        float carLateral = Vector2.Dot(carLocal - carSample.position, carSample.normal);

        Vector3 p = FromPitLane(carDist + along, carLateral + lateral, 0f);
        return new Vector3(p.x, p.y, carPos.z);
    }

    // Out from the RV's door, sideways along its flank. No RV in this track's package? Fall back to the
    // open-air placement so the beat still happens instead of vanishing.
    Vector3 FromRVDoor(float along, float lateral)
    {
        if (_ctx.rv == null) return FromPlayerSpawn(along, lateral);

        Vector2 doorDir = _ctx.rv.DoorWorldDirection;
        Vector2 side = new Vector2(-doorDir.y, doorDir.x);
        Vector3 doorPos = _ctx.rv.DoorWorldPosition;
        Vector3 p = doorPos + (Vector3)(doorDir * along + side * lateral);
        // Ground z, never the player's: inside an RV the player has been pulled to the interior's -2.5,
        // and a body spawned at that z stands in front of the black mask.
        p.z = _ctx.rv.transform.position.z;
        return p;
    }

    // On the line the player is about to walk, facing them: far enough out that a walk-up reads as somebody
    // coming over rather than somebody already standing there.
    Vector3 FromPlayerSpawn(float along, float lateral)
    {
        Vector3 origin = _ctx.playerSpawnPos;
        Vector2 toCar = _ctx.car != null ? (Vector2)(_ctx.car.position - origin) : Vector2.right;
        Vector2 dir = toCar.sqrMagnitude > 0.01f ? toCar.normalized : Vector2.right;
        Vector2 side = new Vector2(-dir.y, dir.x);
        Vector3 p = origin + (Vector3)(dir * along + side * lateral);
        p.z = _ctx.groundZ != 0f ? _ctx.groundZ : origin.z;
        return p;
    }

    // ---------------------------------------------------------------- labels

    public string Label => !string.IsNullOrEmpty(npcId) ? npcId
                         : !string.IsNullOrEmpty(speakerName) ? speakerName
                         : name;

    // The spawned body is named after its marker, so the hierarchy at runtime reads back to the thing you
    // placed ("NPC_RaceEngineer" → "NPC_RaceEngineer_Body" / "…_Cutscene").
    string GameObjectName => name + "_Body";

    // The save key an appearance block falls back to when none was typed, so a repeat policy set in the
    // inspector works without also remembering to invent a key.
    public string EffectiveSaveKey =>
        !string.IsNullOrEmpty(appear.saveKey) ? appear.saveKey : "placed." + Label.ToLowerInvariant().Replace(' ', '.');

    void Awake()
    {
        if (string.IsNullOrEmpty(appear.saveKey) && appear.repeat != AppearanceConditions.Repeat.EveryTime)
            appear.saveKey = EffectiveSaveKey;
    }
}
