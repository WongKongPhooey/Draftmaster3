using System.Collections;
using Draftmaster.Data;
using Draftmaster.Fans;
using Draftmaster.Sponsors;
using Draftmaster.Weekend;
using UnityEngine;

// Somebody waiting for you on the way out of the winner's circle.
//
// The chequered square in the middle of the paddock is where the driver is put to be photographed and to
// be pleasant to twenty of a sponsor's best customers (WeekendVenueSites.PlaceHospitality). It has one
// way in and out — the gap in the front rail — so it is the one place in the paddock where you know
// exactly which two metres of ground the player will cross on their way back to the rigs. That is where
// a rival brand's rep stands.
//
// The beat plays like the one outside the driver's own motorhome: a walk-over trigger, the player frozen,
// the bars in, the rep walking over and opening the conversation (CutsceneTrigger + NPCWalkUpCutscene).
// The difference is what it ends in — a term sheet rather than a goodbye (SponsorPoacherNPC).
//
// Two things decide when it plays, and both are the pitch: the rep says they watched the driver hold a
// room, so nobody is put there until the sponsor photo shoot on the sheet is actually done, and the
// trigger only fires on the way OUT of the pen. The gap in the front rail is the way in as well as the way
// out, so a bare walk-over trigger on it met the player on their way TO the shoot — before the thing the
// rep is supposedly impressed by had happened. Being inside the square once opens the gate.
//
// Built from code rather than placed, because the winner's circle itself is generated: there is no scene
// object at any of the thirty-eight tracks to hang an editor marker off.
public class SponsorPoachBeat : MonoBehaviour
{
    [Tooltip("Metres beyond the front rail the walk-over trigger sits — the mouth everyone leaves through.")]
    public float triggerBeyondRail = 2.0f;
    [Tooltip("Trigger radius (m). Wide enough that walking briskly out of the gap cannot miss it.")]
    public float triggerRadius = 2.6f;
    [Tooltip("Metres beyond the rail the rep waits, and how far to the side of the gap, so they never stand in the doorway.")]
    public float repBeyondRail = 3.6f;
    public float repBesideGap = 3.0f;

    [Tooltip("Seconds to wait for the driver database and the on-foot player before giving up.")]
    public float readyTimeout = 20f;

    // When they bother. Once per save, and only above a reputation — see SponsorPoach.AppealRequired,
    // which is deliberately low so the demo's first weekend can show it.
    public AppearanceConditions appear = new AppearanceConditions
    {
        repeat = AppearanceConditions.Repeat.OnceEver,
        saveKey = "sponsor.poach.winnerscircle",
    };

    Vector3 _triggerAt, _repAt, _facing;

    // The square itself, and how far its rail stands off the middle: what "inside the pen" means.
    Transform _circle;
    float _insideRadius;
    OnFootController _player;
    bool _beenInside;

    // Stand the beat up beside a winner's circle. `circle` is the square's own transform (local +Y runs
    // away from the racetrack, so the gap in the rail is at local -Y); `barrierRing` is the rail's width.
    public static SponsorPoachBeat Install(Transform circle, float barrierRing)
    {
        if (circle == null) return null;
        // The career's own content. A co-op guest receives the paddock from the host rather than building
        // a second copy of it, and a signed contract belongs to whoever's career it is.
        if (!GameSession.CareerActive || Coop.IsGuest) return null;
        if (FindFirstObjectByType<SponsorPoachBeat>() != null) return null;

        var go = new GameObject("SponsorPoachBeat");
        go.transform.SetParent(circle.parent, false);
        var beat = go.AddComponent<SponsorPoachBeat>();

        float rail = barrierRing * 0.5f;
        beat._triggerAt = Walkable(circle.TransformPoint(new Vector3(0f, -(rail + beat.triggerBeyondRail), 0f)));
        beat._repAt = Walkable(circle.TransformPoint(
            new Vector3(beat.repBesideGap, -(rail + beat.repBeyondRail), 0f)));
        // They are watching the square, so they face back toward it.
        beat._facing = circle.position - beat._repAt;
        beat._circle = circle;
        beat._insideRadius = rail;
        return beat;
    }

    static Vector3 Walkable(Vector3 wanted)
    {
        Vector3 flat = new Vector3(wanted.x, wanted.y, 0f);
        if (!PaddockBoundary.AnyActive) return flat;
        Vector2 inside = PaddockBoundary.Constrain(flat);
        return new Vector3(inside.x, inside.y, 0f);
    }

    void Start() => StartCoroutine(ArmWhenReady());

    IEnumerator ArmWhenReady()
    {
        if (!appear.IsMet())
        {
            Debug.Log($"SponsorPoachBeat: not this time — {appear.FirstUnmet()}.", this);
            Destroy(gameObject);
            yield break;
        }

        // The brands live in SQLite, which DatabaseManager opens a few frames into the scene, and the
        // player's body is spawned by PitLaneStart.
        float timeout = readyTimeout;
        while (timeout > 0f && (DatabaseManager.Instance == null || !DatabaseManager.Instance.IsReady))
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        while (timeout > 0f && OnFootController.Current == null)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        var player = OnFootController.Current;
        if (player == null)
        {
            Debug.Log("SponsorPoachBeat: nobody on foot to walk up to — beat skipped.", this);
            Destroy(gameObject);
            yield break;
        }

        _player = player;

        // Nobody is stood there before the shoot. No timeout on this one: the shoot is an hour of Friday
        // morning the player books themselves, and the paddock is walkable for three days around it.
        while (true)
        {
            var state = Shoot();
            if (state == ShootState.Done) break;
            if (state == ShootState.NeverHappening)
            {
                Debug.Log("SponsorPoachBeat: the photo shoot was missed — nobody watched this driver work " +
                          "a room, so nobody came looking. Beat skipped.", this);
                Destroy(gameObject);
                yield break;
            }
            yield return new WaitForSeconds(0.5f);
        }

        // The gate the brief is actually about: nobody comes looking for a driver nobody has heard of.
        // Read here rather than at scene build, because the shoot itself moves it.
        float standing = FanAppeal.Value;
        if (!SponsorPoach.Qualifies(standing))
        {
            Debug.Log($"SponsorPoachBeat: appeal {Mathf.RoundToInt(standing)} is under " +
                      $"{SponsorPoach.AppealRequired} — nobody is poaching this driver yet.", this);
            Destroy(gameObject);
            yield break;
        }

        var sponsor = PickBrand();
        if (sponsor == null)
        {
            Debug.Log("SponsorPoachBeat: no unsigned brand left to send anybody — beat skipped.", this);
            Destroy(gameObject);
            yield break;
        }

        Build(sponsor, player, standing);
    }

    enum ShootState { Waiting, Done, NeverHappening }

    // Where this weekend's sponsor photo shoot has got to. Any shoot on the sheet being done is the cue —
    // Friday's stills or Saturday's dealer photos, whichever the player actually turned up to. A sheet with
    // no shoot booked on it never waits; a sheet whose shoots have all gone by unattended never plays the
    // beat at all, because the whole pitch is about a room the player was not in.
    static ShootState Shoot()
    {
        var timetable = WeekendDirector.Timetable;
        if (timetable == null) return ShootState.Done;

        bool booked = false, pending = false;
        foreach (var a in timetable.Activities)
        {
            if (a == null || a.kind != ActivityKind.PhotoShoot) continue;
            booked = true;
            if (WeekendLedger.IsDone(a.id)) return ShootState.Done;
            if (!WeekendLedger.IsMissed(a.id)) pending = true;
        }
        if (!booked) return ShootState.Done;
        return pending ? ShootState.Waiting : ShootState.NeverHappening;
    }

    // The way out, not the way in — see the header. Being inside the rail once opens the trigger's gate.
    void Update()
    {
        if (_beenInside || _player == null || _circle == null) return;
        if (Vector2.Distance(_player.transform.position, _circle.position) <= _insideRadius) _beenInside = true;
    }

    // Who comes looking. The biggest name the driver is not already carrying and who is not already stood
    // in the pit lane this weekend — a poacher has to feel like a step up, and finding the same rep in two
    // places at one meeting reads as a bug.
    Sponsor PickBrand()
    {
        var reps = SponsorCatalog.RepsForWeekend(AppearanceConditions.CurrentTrackId,
                                                 RaceWeekend.WeekendId, 2);
        Sponsor best = null;
        foreach (var s in SponsorCatalog.All())
        {
            if (s == null || SponsorBook.HasSponsor(s.Id)) continue;

            bool alreadyInTheLane = false;
            foreach (var r in reps) if (r != null && r.Id == s.Id) { alreadyInTheLane = true; break; }
            if (alreadyInTheLane) continue;

            if (best == null || s.Wealth > best.Wealth) best = s;
        }
        return best;
    }

    void Build(Sponsor sponsor, OnFootController player, float standing)
    {
        int beat = SponsorPoach.BestRateOnTheBooks(SponsorBook.Deals);
        var offer = SponsorPoach.Offer(sponsor.Wealth, sponsor.Prestige, standing, sponsor.MinPrestige, beat);

        var rep = PaddockPerson.SpawnTalker<SponsorPoacherNPC>(
            transform, new Vector3(_repAt.x, _repAt.y, PaddockPerson.GroundZ),
            "SponsorPoacher_" + sponsor.Name, sponsor.Id * 977 + 41,
            $"{sponsor.Name} rep", Lines(sponsor, beat), interactRange: 2.4f);
        rep.sponsor = sponsor;
        rep.offer = offer;
        rep.beatRate = beat;
        rep.turnsToFace = true;
        OnFootController.ApplyFacing(rep.transform, rep.GetComponent<Rigidbody2D>(),
                                     new Vector2(_facing.x, _facing.y), 90f);

        // The cutscene and its walk-over trigger share one object, exactly as PlacedNPC arms a placed
        // beat — so the whole thing goes away together when it has played.
        var seq = new GameObject("SponsorPoach_Cutscene");
        seq.transform.SetParent(transform, false);
        seq.transform.position = new Vector3(_triggerAt.x, _triggerAt.y, 0f);

        var walkUp = seq.AddComponent<NPCWalkUpCutscene>();
        walkUp.player = player;
        walkUp.npc = rep;
        walkUp.stopDistance = 1.3f;

        // A player who is already outside the pen when the rep turns up has nothing to walk out of, so the
        // gate opens for them straight away; anyone stood on the chequers has to leave first.
        _beenInside = _circle == null
                   || Vector2.Distance(player.transform.position, _circle.position) > _insideRadius;

        var trigger = seq.AddComponent<CutsceneTrigger>();
        trigger.radius = triggerRadius;
        trigger.target = player.transform;
        trigger.Gate = () => _beenInside;
        trigger.Triggered = () => { appear.MarkSeen(); walkUp.Play(); };

        Debug.Log($"SponsorPoachBeat: {sponsor.Name} waiting at the winner's circle exit " +
                  $"(appeal {Mathf.RoundToInt(standing)}, offering ${offer.perRace:N0}/race for " +
                  $"{offer.races} races against ${beat:N0} on the car).", this);
    }

    // What they say on the way over. The pitch is the press: they are buying somebody who can hold a room,
    // and they say what that is worth against whoever is on the car already.
    static string[] Lines(Sponsor sponsor, int beatRate)
    {
        string money = beatRate > 0
            ? $"Whoever has your hood is paying you ${beatRate:N0} a race for that. They are underpaying you."
            : "And you are carrying nobody's name for it. That is money left on the table.";

        return new[]
        {
            "Don't mind me. I have been stood at the back of this thing all afternoon.",
            "Do I know you? #player",
            $"{sponsor.Name}. Nothing to do with these people — I came to watch you, not them.",
            "You held that room. No flinching, no ums, and the press walked away with exactly what you " +
            "chose to give them. That is rarer than a quick lap.",
            money,
            "So what is this? #player",
            "An offer, {playerfirst}. Already drawn up, already better. Have a look at it.",
        };
    }
}
