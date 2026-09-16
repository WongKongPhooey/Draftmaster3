using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Sim
{
    // How an AI car in a pack sees what is in front of it and decides to brake, swerve alongside, or both.
    // Pure maths and a small stateful planner, kept out of the MonoBehaviours so a whole pace-lap field can be
    // driven through it in an EditMode test (play mode does not tick in an unfocused editor).
    //
    // Everything is in one car's track frame: metres along the centreline between car CENTRES, and signed
    // lateral offsets from the centreline (+ = right of travel). Speeds are mph, as SplineDriver stores them.
    //
    // Two layers, deliberately separate:
    //   * a COMFORT law — the linear follower the formation lap was tuned on. It holds station smoothly and its
    //     braking is rate-limited, so a wobble up front dies out down the train instead of amplifying.
    //   * a SAFETY law — the fastest speed from which the car can still stop behind whatever is in its lane,
    //     even if that car brakes as hard as it can (Gipps' safe speed). It is not rate-limited and it grants
    //     real brake authority. The comfort law sits beyond it, so in a healthy train it never binds; when
    //     something goes wrong (a crash, a cut-in, a car merging in fast) it is what stops the contact.
    //
    // The old law measured gaps nose-to-nose against thresholds that ignored car length, so at pit-out and
    // close-up speeds the "panic" trigger sat INSIDE a car length: cars touched before they reacted. Here every
    // decision is taken on bumper-to-bumper clearance.
    public struct PackCar
    {
        public int Id;               // stable identity, so a swerve can stay latched onto one car
        public float Gap;            // centre-to-centre along the track (m): + = ahead of me, - = behind
        public float Lateral;        // where it is across the track now (m)
        public float PlannedLateral; // where it is heading across the track (m); = Lateral when unknown
        public float SpeedMph;
        public float HalfLength;
        public float HalfWidth;
        public float BrakeMphPerSec; // the hardest it is assumed able to brake
        public bool IsPaceCar;

        // The strip of road it occupies or is about to, current and planned together — a car sliding across
        // into my lane is in my way before it gets there.
        public float LaneLo => Mathf.Min(Lateral, PlannedLateral) - HalfWidth;
        public float LaneHi => Mathf.Max(Lateral, PlannedLateral) + HalfWidth;
    }

    public struct PackSelf
    {
        public float SpeedMph;
        public float Lateral;        // where I am across the track now (m)
        public float Tactical;       // the tactical offset currently applied (it is part of Lateral)
        public float HalfLength;
        public float HalfWidth;
        public float BrakeMphPerSec; // braking I can call on when the safety law needs it
        public float TrackLo;        // lateral limits I may drive between (m); use -inf / +inf when unknown
        public float TrackHi;
        public float Drift;          // lateral still easing out of Lateral whatever I do (the pit-exit merge); my
                                     // lane covers where that lands too
    }

    // What the formation wants when nothing is in the way.
    public struct PackIntent
    {
        public float BaseCapMph;     // pace ceiling: cruise, catch-up or pit-out
        public float MaxCapMph;      // absolute ceiling
        public float FloorMph;       // the comfort law never asks for less than this (the safety law may)
        public float WantGap;        // station (centre-to-centre, m) off another car
        public float PaceCarGap;     // station off the pace car
        public float CruiseMph;      // never chase the pace car above this
        public float ColumnTactical; // tactical offset for the car's column / weave
        public float ColumnSlew;     // m/s
        public bool AllowSwerve;     // off for the pit-out settle and the pit lane
    }

    public enum PackMode { Clear, Follow, Brake, Swerve, Alongside, Hold }

    public struct PackCommand
    {
        public float CapMph;
        public float MinDecelMphPerSec; // > 0 = brake at least this hard (SplineDriver.aiMinDecelMphPerSec)
        public float TacticalTarget;
        public float Slew;
        public PackMode Mode;

        // Diagnostics (F8).
        public bool HasLeader;
        public bool LeaderIsPaceCar;
        public float Clearance;       // bumper-to-bumper to the car being followed (m)
        public float SafeClearance;   // below this the safety law is braking
        public float StationGap;      // centre-to-centre gap the comfort law holds (m)
        public float ClosingMph;      // + = catching the car being followed
    }

    [System.Serializable]
    public sealed class PackSettings
    {
        [Header("Comfort (station keeping)")]
        public float GapGainMphPerMetre = 2.5f;
        public float GapDeadbandM = 1.5f;
        public float RelVelDampMph = 1.5f;
        public float MaxBrakeMphPerSec = 20f;
        public float HeadwaySec = 0.55f;
        public float MinGap = 5f;
        [Tooltip("Clearance (m) the station keeps beyond what the safety law needs, so the safety law does not bind in a healthy train.")]
        public float StationBufferM = 2.5f;

        [Header("Safety (never hit what is in the lane)")]
        [Tooltip("Seconds of travel allowed before the brakes bite: covers the frame of latency between the scan and the speed update.")]
        public float ReactionSec = 0.12f;
        [Tooltip("Bumper-to-bumper clearance (m) left after a full stop.")]
        public float StandstillClearM = 1f;

        [Header("Lanes")]
        [Tooltip("Extra lateral clearance (m) on top of the two half-widths before two cars count as sharing a lane.")]
        public float LaneMarginM = 0.35f;

        [Header("Swerve alongside")]
        [Tooltip("Lateral daylight (m) left between the two cars when sat alongside.")]
        public float SwerveClearM = 0.6f;
        public float SwerveSlewPerSec = 4.5f;
        [Tooltip("A car below this speed (mph) is a blockage — it gets driven around, not followed.")]
        public float BlockStoppedMph = 14f;
        [Tooltip("A car this much slower than me (mph) is a blockage.")]
        public float BlockSpeedDeltaMph = 18f;
        [Tooltip("Braking (mph/s) a driver is happy to use for a blockage. If stopping behind it needs more, and a side is open, go round it.")]
        public float ComfortDecelMphPerSec = 15f;
        [Tooltip("Below this (mph) a car is stopped dead — a wreck, not a crawling car working its own way round one.")]
        public float StoppedDeadMph = 3f;
        [Tooltip("A car stopped dead within this clearance (m) is driven round whenever a side opens, even by a car already waiting behind it — unless it is itself only queuing behind another stopped car.")]
        public float BlockageSwerveRangeM = 20f;
        [Tooltip("A stopped car with another slow car this close (m of clearance) in front of it is queuing, not blocking: wait behind it rather than file past the whole queue.")]
        public float QueueGapM = 12f;
        [Tooltip("Seconds stopped behind a queued car before going round anyway — a queue that does not move is a pile-up.")]
        public float QueueWaitSec = 4f;
        [Tooltip("How much faster than a blockage (mph) a car may go while passing alongside it.")]
        public float PassDeltaMph = 25f;
        [Tooltip("A stopped car with less than this much daylight (m) beside my lane is passed at the passing speed.")]
        public float PassClearanceM = 1.5f;
        [Tooltip("Alongside a car that is still moving: sit this far under its pace (mph) and drop back in behind it — no overtaking under the pace car.")]
        public float HoldBehindMph = 5f;
        [Tooltip("Longitudinal window (m, beyond the two half-lengths) ahead of me that must be clear in the lane I move into.")]
        public float SideWindowAheadM = 6f;
        [Tooltip("Longitudinal window (m, beyond the two half-lengths) behind me that must be clear in the lane I move into.")]
        public float SideWindowBehindM = 3f;
        [Tooltip("Seconds a lane change is assumed to take: a car closing from behind (or a slower car ahead) widens the window by its closing speed times this.")]
        public float MergeTimeSec = 1f;
        [Tooltip("A swerve that has not resolved in this long (s) is dropped.")]
        public float SwerveMaxSec = 12f;
        [Tooltip("After a swerve ends, wait this long (s) before committing to another, so a car can't chatter in and out of one.")]
        public float SwerveCooldownSec = 0.75f;
        [Tooltip("A lateral move smaller than this (m) is not checked for traffic (the weave and column trims).")]
        public float MoveCheckM = 0.3f;
    }

    public static class PackAvoidance
    {
        public const float MphToMps = 1f / 2.237f;
        public const float MpsToMph = 2.237f;

        // Gipps' safe speed (m/s): the fastest a car may go and still stop short of the car ahead, allowing
        // reactionSec before it brakes at brake, while the car ahead brakes at leadBrake from leadSpeed.
        // Solves  v*t + v^2/(2b) = clear + vL^2/(2bL)  for v.
        public static float SafeSpeedMps(float clear, float leadSpeedMps, float brakeMps2, float leadBrakeMps2,
                                         float reactionSec)
        {
            float b = Mathf.Max(brakeMps2, 0.1f);
            float bL = Mathf.Max(leadBrakeMps2, 0.1f);
            float vL = Mathf.Max(0f, leadSpeedMps);
            float budget = clear + vL * vL / (2f * bL);
            if (budget <= 0f) return 0f;
            float t = Mathf.Max(0f, reactionSec);
            return b * (-t + Mathf.Sqrt(t * t + 2f * budget / b));
        }

        // The clearance (m) SafeSpeedMps needs at these speeds — the inverse of it.
        public static float SafeClearance(float speedMps, float leadSpeedMps, float brakeMps2, float leadBrakeMps2,
                                          float reactionSec)
        {
            float v = Mathf.Max(0f, speedMps);
            float vL = Mathf.Max(0f, leadSpeedMps);
            float need = v * Mathf.Max(0f, reactionSec) + v * v / (2f * Mathf.Max(brakeMps2, 0.1f))
                         - vL * vL / (2f * Mathf.Max(leadBrakeMps2, 0.1f));
            return Mathf.Max(0f, need);
        }

        // Metres to stop from speedMps at brakeMps2, after reactionSec.
        public static float StoppingDistance(float speedMps, float brakeMps2, float reactionSec)
        {
            float v = Mathf.Max(0f, speedMps);
            return v * Mathf.Max(0f, reactionSec) + v * v / (2f * Mathf.Max(brakeMps2, 0.1f));
        }

        public static bool Overlaps(float aLo, float aHi, float bLo, float bHi) => aLo < bHi && bLo < aHi;

        public static bool IsBlockage(float myMph, float otherMph, float stoppedMph, float deltaMph)
            => otherMph < stoppedMph || myMph - otherMph > deltaMph;

        // Along-track distance from mine to theirs, wrapped into (-lap/2, lap/2]. + = they are ahead.
        public static float SignedGap(float theirDistance, float myDistance, float lapLength)
        {
            float g = theirDistance - myDistance;
            if (lapLength > 0f)
            {
                g %= lapLength;
                if (g > lapLength * 0.5f) g -= lapLength;
                else if (g <= -lapLength * 0.5f) g += lapLength;
            }
            return g;
        }

        // Linear follower: the car ahead's pace, trimmed by gap error (with a deadband) and damped by the closing
        // rate. The closing-rate term is what keeps a train string-stable.
        public static float FollowCapMph(float myMph, float aheadMph, float gap, float wantGap,
                                         float gainMphPerMetre, float deadbandM, float relVelDampMph)
        {
            float gapErr = gap - wantGap;
            if (Mathf.Abs(gapErr) <= deadbandM) gapErr = 0f;
            else gapErr -= Mathf.Sign(gapErr) * deadbandM;
            return aheadMph + gainMphPerMetre * gapErr - relVelDampMph * (myMph - aheadMph);
        }

        // A box of halfLength x halfWidth yawed by yawDeg against the track: the footprint it sweeps along and
        // across the road. A car sat sideways blocks a lot more of the road than one pointing down it.
        public static void Footprint(float halfLength, float halfWidth, float yawDeg,
                                     out float alongHalf, out float acrossHalf)
        {
            float r = yawDeg * Mathf.Deg2Rad;
            float c = Mathf.Abs(Mathf.Cos(r));
            float s = Mathf.Abs(Mathf.Sin(r));
            alongHalf = c * halfLength + s * halfWidth;
            acrossHalf = s * halfLength + c * halfWidth;
        }
    }

    // Where a formation car wants to sit across the road when nothing is in the way: two columns by grid parity,
    // pulled in (never together) through turns, with a slow tyre-warming weave on the straights.
    public static class FormationLanes
    {
        // Even grid slots run the left column, odd slots the right.
        public static float Column(int gridSlot, float halfOffset, bool corner, float cornerScale)
        {
            int parity = ((gridSlot % 2) + 2) % 2;
            float column = parity == 0 ? -halfOffset : halfOffset;
            return corner ? column * cornerScale : column;
        }

        public static float Weave(float time, int gridSlot, float amplitude, float hz, float phasePerSlot, float envelope)
            => Mathf.Sin(time * (2f * Mathf.PI * hz) + gridSlot * phasePerSlot) * amplitude * envelope;

        // The weave fades in slowly, and out fast: the columns pull in for a turn at the same moment the weave is
        // told to stop, and anti-phase weave on top of narrowed columns is what closes a pair to a touch.
        public static float StepEnvelope(float envelope, bool on, float dt, float fadeInSec, float fadeOutSec)
        {
            float rate = on ? dt / Mathf.Max(fadeInSec, 0.01f) : dt / Mathf.Max(fadeOutSec, 0.01f);
            return Mathf.MoveTowards(envelope, on ? 1f : 0f, rate);
        }
    }

    // One car's brain for the pack: owns the latches (the swerve it is committed to, the brake rate-limit
    // history) and turns a snapshot of the cars around it into a speed cap, a brake demand and a lateral target.
    public sealed class PackPlanner
    {
        public const int NoCar = int.MinValue;

        public PackSettings Settings = new PackSettings();

        float _prevCap;
        bool _hasPrevCap;
        float _lastTactical;
        bool _hasLastTactical;
        int _swerveId = NoCar;
        int _swerveSide;
        bool _swerveBlockage;
        float _swerveTimer;
        float _waitTimer;
        float _cooldown;

        public bool Swerving => _swerveId != NoCar;
        public int SwerveSide => _swerveSide;
        public int SwerveTargetId => _swerveId;

        // How far ahead (m, centre to centre) the caller must gather cars: far enough to see a stopped car in
        // time to stop gently, and the queue in front of it.
        public float ScanAheadM(float mySpeedMph, float minRange)
        {
            var s = Settings;
            float stop = PackAvoidance.StoppingDistance(Mathf.Max(0f, mySpeedMph) * PackAvoidance.MphToMps,
                s.ComfortDecelMphPerSec * PackAvoidance.MphToMps, s.ReactionSec);
            return Mathf.Max(minRange, stop + 2f * CarHalfLengthGuess + s.StandstillClearM + s.StationBufferM
                                       + s.QueueGapM + 2f * CarHalfLengthGuess);
        }

        // How far behind (m) matters: a car closing from behind while I change lanes.
        public float ScanBehindM => 2f * CarHalfLengthGuess + Settings.SideWindowBehindM + 30f * Settings.MergeTimeSec;

        const float CarHalfLengthGuess = 2.5f;

        // Where this car is heading across the track, for the cars around it (they treat a car sliding into
        // their lane as already in it). baseLateral = the car's lateral with its tactical offset taken out.
        public float PlannedLateral(float baseLateral, float currentLateral)
            => _hasLastTactical ? baseLateral + _lastTactical : currentLateral;

        public void Reset()
        {
            _hasPrevCap = false;
            _hasLastTactical = false;
            _waitTimer = 0f;
            _cooldown = 0f;
            ClearSwerve();
        }

        void ClearSwerve()
        {
            _swerveId = NoCar;
            _swerveSide = 0;
            _swerveBlockage = false;
            _swerveTimer = 0f;
        }

        public PackCommand Step(in PackSelf me, in PackIntent intent, IReadOnlyList<PackCar> cars, float dt)
        {
            var s = Settings;
            var cmd = new PackCommand { Mode = PackMode.Clear };
            int n = cars != null ? cars.Count : 0;

            float myMps = Mathf.Max(0f, me.SpeedMph) * PackAvoidance.MphToMps;
            float myBrake = Mathf.Max(me.BrakeMphPerSec, 0.1f) * PackAvoidance.MphToMps;
            float baseLat = me.Lateral - me.Tactical;
            float plannedLat = _hasLastTactical ? baseLat + _lastTactical : me.Lateral;

            // My lane: where I am plus where I'm going. Current-only is kept for cars already level with me.
            float curLo = me.Lateral - me.HalfWidth - s.LaneMarginM;
            float curHi = me.Lateral + me.HalfWidth + s.LaneMarginM;
            float settled = plannedLat - me.Drift;
            float laneLo = Mathf.Min(Mathf.Min(me.Lateral, plannedLat), settled) - me.HalfWidth - s.LaneMarginM;
            float laneHi = Mathf.Max(Mathf.Max(me.Lateral, plannedLat), settled) + me.HalfWidth + s.LaneMarginM;

            // --- Keep or drop the swerve I'm committed to.
            int foe = FindCar(cars, _swerveId);
            if (_swerveId != NoCar)
            {
                _swerveTimer += dt;
                bool release = foe < 0 || _swerveTimer > s.SwerveMaxSec || !intent.AllowSwerve;
                if (!release)
                {
                    var f = cars[foe];
                    float lengths = me.HalfLength + f.HalfLength;
                    // A car that stops dead beside me is driven past — sitting parked next to it, half in the next
                    // lane, waits for someone to run into me. A blockage that got going again is just a car in the
                    // train.
                    if (f.SpeedMph < s.BlockStoppedMph) _swerveBlockage = true;
                    else if (_swerveBlockage && !PackAvoidance.IsBlockage(intent.BaseCapMph, f.SpeedMph,
                            s.BlockStoppedMph, s.BlockSpeedDeltaMph))
                        _swerveBlockage = false;

                    // Everything between where I am and my column: if the car isn't anywhere in that, going back
                    // can't hit it, and it has moved out of the way on its own.
                    float colLat = baseLat + intent.ColumnTactical;
                    bool inTheWayBack = PackAvoidance.Overlaps(
                        Mathf.Min(me.Lateral, colLat) - me.HalfWidth - s.LaneMarginM,
                        Mathf.Max(me.Lateral, colLat) + me.HalfWidth + s.LaneMarginM, f.LaneLo, f.LaneHi);

                    if (f.Gap < -lengths) release = true; // passed it: the lane check below decides when to move back
                    else if (!inTheWayBack) release = true;
                    else if (!_swerveBlockage && f.Gap > 0f)
                    {
                        // It pulled away again: fall back in behind it once there's a proper gap.
                        float clear = f.Gap - lengths;
                        float need = s.StandstillClearM + s.StationBufferM + PackAvoidance.SafeClearance(myMps,
                            f.SpeedMph * PackAvoidance.MphToMps, myBrake, LeadBrake(f), s.ReactionSec);
                        if (clear > need && f.SpeedMph >= me.SpeedMph - 1f) release = true;
                    }
                }
                if (release)
                {
                    ClearSwerve();
                    _cooldown = s.SwerveCooldownSec;
                    foe = -1;
                }
            }

            // --- What's in my lane ahead: the nearest one to follow, and the tightest safe speed over all of them.
            int lead = -1;
            float leadGap = float.MaxValue;
            float safeMps = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = cars[i];
                if (c.Gap <= 0f) continue;
                float clear = c.Gap - me.HalfLength - c.HalfLength;
                float v;
                if (clear < 0f)
                {
                    // Already level with it. Only a car overlapping where I am NOW matters (one I'm merely
                    // planning towards is the lateral check's business), and the answer is to drop back behind
                    // it — not to stop dead in the middle of the pack.
                    if (!PackAvoidance.Overlaps(curLo, curHi, c.LaneLo, c.LaneHi)) continue;
                    v = Mathf.Max(0f, c.SpeedMph - s.HoldBehindMph) * PackAvoidance.MphToMps;
                }
                else
                {
                    if (!PackAvoidance.Overlaps(laneLo, laneHi, c.LaneLo, c.LaneHi)) continue;
                    v = PackAvoidance.SafeSpeedMps(clear - s.StandstillClearM, c.SpeedMph * PackAvoidance.MphToMps,
                        myBrake, LeadBrake(c), s.ReactionSec);
                }
                if (v < safeMps) safeMps = v;
                if (c.Gap < leadGap) { leadGap = c.Gap; lead = i; }
            }

            // Nobody passes the pace car: whichever lane it is in, it is the car to keep station off unless
            // something nearer is. (Only station keeping — the safety law still only brakes for what is in my lane.)
            for (int i = 0; i < n; i++)
            {
                var c = cars[i];
                if (!c.IsPaceCar || c.Gap <= 0f) continue;
                if (c.Gap < leadGap) { leadGap = c.Gap; lead = i; }
            }

            // --- Comfort law: hold station off the car in front, far enough back that the safety law stays quiet.
            float cap = intent.BaseCapMph;
            float floor = intent.FloorMph;
            if (lead >= 0 && cars[lead].SpeedMph < s.BlockStoppedMph) floor = 0f; // stopped ahead: roll up and stop, gently
            if (lead >= 0)
            {
                var L = cars[lead];
                float lengths = me.HalfLength + L.HalfLength;
                float leadMps = L.SpeedMph * PackAvoidance.MphToMps;
                float want = L.IsPaceCar ? intent.PaceCarGap : intent.WantGap;
                float safetyGap = lengths + s.StandstillClearM + s.StationBufferM
                                  + PackAvoidance.SafeClearance(myMps, leadMps, myBrake, LeadBrake(L), s.ReactionSec);
                float station = Mathf.Max(want, Mathf.Max(s.MinGap, myMps * s.HeadwaySec));
                station = Mathf.Max(station, safetyGap);
                float follow = PackAvoidance.FollowCapMph(me.SpeedMph, L.SpeedMph, L.Gap, station,
                    s.GapGainMphPerMetre, s.GapDeadbandM, s.RelVelDampMph);
                if (L.IsPaceCar) follow = Mathf.Min(follow, intent.CruiseMph); // pace it, never chase its peel-away
                cap = Mathf.Min(cap, follow);

                cmd.HasLeader = true;
                cmd.LeaderIsPaceCar = L.IsPaceCar;
                cmd.Clearance = L.Gap - lengths;
                cmd.StationGap = station;
                cmd.ClosingMph = me.SpeedMph - L.SpeedMph;
                cmd.SafeClearance = s.StandstillClearM
                    + PackAvoidance.SafeClearance(myMps, leadMps, myBrake, LeadBrake(L), s.ReactionSec);
                cmd.Mode = PackMode.Follow;
            }
            cap = Mathf.Clamp(cap, floor, intent.MaxCapMph);

            // Comfort braking is rate-limited so a touch of the brakes up front stays a touch down the train. The
            // limit runs from the car's actual speed, not a stale cap far above it, so it never delays the brakes.
            if (_hasPrevCap)
            {
                float from = Mathf.Min(_prevCap, me.SpeedMph);
                cap = Mathf.Max(cap, from - s.MaxBrakeMphPerSec * dt);
            }

            // Sat alongside the car I swerved round: pass a blockage at a controlled speed, otherwise hold back
            // and drop in behind it.
            if (foe >= 0)
            {
                var f = cars[foe];
                if (!PackAvoidance.Overlaps(curLo, curHi, f.LaneLo, f.LaneHi))
                {
                    float lim = _swerveBlockage ? f.SpeedMph + s.PassDeltaMph : f.SpeedMph - s.HoldBehindMph;
                    cap = Mathf.Min(cap, Mathf.Max(0f, lim));
                    cmd.Mode = PackMode.Alongside;
                }
            }

            // Driving past any stopped car close alongside (not just the one I swerved for): slow to a passing speed,
            // starting far enough back to get there at a comfortable rate.
            if (intent.AllowSwerve)
            {
                for (int i = 0; i < n; i++)
                {
                    var c = cars[i];
                    if (c.SpeedMph >= s.BlockStoppedMph) continue;
                    float lengths = me.HalfLength + c.HalfLength;
                    float passMph = c.SpeedMph + s.PassDeltaMph;
                    float excessMps = Mathf.Max(0f, me.SpeedMph - passMph) * PackAvoidance.MphToMps;
                    float window = lengths + s.SideWindowAheadM + PackAvoidance.StoppingDistance(excessMps,
                        s.ComfortDecelMphPerSec * PackAvoidance.MphToMps, s.ReactionSec);
                    if (c.Gap > window || c.Gap < -lengths) continue;
                    float latClear = Mathf.Max(c.LaneLo - (me.Lateral + me.HalfWidth),
                                               (me.Lateral - me.HalfWidth) - c.LaneHi);
                    if (latClear < 0f || latClear > s.PassClearanceM) continue; // in my lane (followed) or well clear
                    if (passMph < cap) { cap = passMph; if (cmd.Mode == PackMode.Clear || cmd.Mode == PackMode.Follow) cmd.Mode = PackMode.Alongside; }
                }
            }

            // --- Safety law: never faster than I can stop from. Not rate-limited, and it gets real brakes.
            float minDecel = 0f;
            if (safeMps < float.MaxValue)
            {
                float safeMph = safeMps * PackAvoidance.MpsToMph;
                if (safeMph < cap)
                {
                    cap = safeMph;
                    if (safeMph < me.SpeedMph) { minDecel = me.BrakeMphPerSec; cmd.Mode = PackMode.Brake; }
                }
            }
            cap = Mathf.Max(0f, cap);
            _prevCap = cap;
            _hasPrevCap = true;

            bool waiting = lead >= 0 && me.SpeedMph < s.BlockStoppedMph && cars[lead].SpeedMph < s.BlockStoppedMph;
            _waitTimer = waiting ? _waitTimer + dt : 0f;

            // --- Swerve: a blockage I'd need more than comfortable braking for, or anything the safety law is
            // already braking for, gets driven round if a side is open. Braking carries on until I'm clear of it.
            if (_cooldown > 0f) _cooldown -= dt;
            if (_swerveId == NoCar && _cooldown <= 0f && intent.AllowSwerve && lead >= 0 && !cars[lead].IsPaceCar)
            {
                var L = cars[lead];
                float clear = L.Gap - me.HalfLength - L.HalfLength;
                bool blockage = PackAvoidance.IsBlockage(me.SpeedMph, L.SpeedMph, s.BlockStoppedMph, s.BlockSpeedDeltaMph);
                float closingMps = Mathf.Max(0f, me.SpeedMph - L.SpeedMph) * PackAvoidance.MphToMps;
                float comfortStop = PackAvoidance.StoppingDistance(closingMps,
                    s.ComfortDecelMphPerSec * PackAvoidance.MphToMps, s.ReactionSec);
                bool urgent = safeMps * PackAvoidance.MpsToMph <= me.SpeedMph + 0.5f;
                bool mustGoRound = blockage && clear < comfortStop + s.StandstillClearM + s.StationBufferM;
                // Stopped dead ahead: go round when a side opens, however long I've been waiting behind it —
                // but a car that is only queuing behind another stopped car is waited behind, or a stalled train
                // would empty itself down the other lane.
                bool stoppedAhead = L.SpeedMph < s.StoppedDeadMph;
                bool waitedLongEnough = _waitTimer > s.QueueWaitSec;
                if (stoppedAhead && clear < s.BlockageSwerveRangeM)
                    mustGoRound = waitedLongEnough || !IsQueued(L, cars);
                else if (stoppedAhead && mustGoRound && !waitedLongEnough && IsQueued(L, cars))
                    mustGoRound = false;
                if (urgent || mustGoRound)
                {
                    int side = ChooseSide(me, L, cars);
                    if (side != 0)
                    {
                        _swerveId = L.Id;
                        _swerveSide = side;
                        _swerveBlockage = blockage;
                        _swerveTimer = 0f;
                        foe = lead;
                    }
                }
            }

            // --- Lateral.
            float tactical;
            float slew;
            if (foe >= 0)
            {
                float target = SwerveTarget(me, cars[foe], _swerveSide);
                target = Mathf.Clamp(target, me.TrackLo, me.TrackHi);
                // Something moved into the lane I'm heading for: stop moving across and let the brakes do it.
                if (Mathf.Abs(target - me.Lateral) > s.MoveCheckM && LaneBlocked(me, target, cars, cars[foe].Id))
                {
                    target = me.Lateral;
                    cmd.Mode = PackMode.Hold;
                }
                else if (cmd.Mode != PackMode.Alongside && cmd.Mode != PackMode.Brake)
                    cmd.Mode = PackMode.Swerve;
                tactical = target - baseLat;
                slew = s.SwerveSlewPerSec;
            }
            else
            {
                tactical = intent.ColumnTactical;
                slew = intent.ColumnSlew;
                // Any real move across (back into the column after a swerve, the column narrowing for a turn)
                // waits until the lane it moves into is empty.
                float target = baseLat + tactical;
                if (Mathf.Abs(target - me.Lateral) > s.MoveCheckM && LaneBlocked(me, target, cars, NoCar))
                {
                    tactical = me.Tactical;
                    if (cmd.Mode == PackMode.Clear || cmd.Mode == PackMode.Follow) cmd.Mode = PackMode.Hold;
                }
            }

            _lastTactical = tactical;
            _hasLastTactical = true;

            cmd.CapMph = cap;
            cmd.MinDecelMphPerSec = minDecel;
            cmd.TacticalTarget = tactical;
            cmd.Slew = slew;
            return cmd;
        }

        static float LeadBrake(in PackCar c) => Mathf.Max(c.BrakeMphPerSec, 0.1f) * PackAvoidance.MphToMps;

        // Is this slow car sat behind another slow car (or the pace car) in its own lane?
        bool IsQueued(in PackCar c, IReadOnlyList<PackCar> cars)
        {
            var s = Settings;
            for (int i = 0; i < cars.Count; i++)
            {
                var o = cars[i];
                if (o.Id == c.Id) continue;
                float clear = o.Gap - c.Gap - o.HalfLength - c.HalfLength;
                if (clear < -0.5f || clear > s.QueueGapM) continue;
                if (o.SpeedMph >= s.BlockStoppedMph + 5f) continue;
                if (PackAvoidance.Overlaps(c.LaneLo - s.LaneMarginM, c.LaneHi + s.LaneMarginM, o.LaneLo, o.LaneHi))
                    return true;
            }
            return false;
        }

        static int FindCar(IReadOnlyList<PackCar> cars, int id)
        {
            if (id == NoCar || cars == null) return -1;
            for (int i = 0; i < cars.Count; i++)
                if (cars[i].Id == id) return i;
            return -1;
        }

        float SwerveTarget(in PackSelf me, in PackCar foe, int side)
        {
            float clear = me.HalfWidth + foe.HalfWidth + Settings.SwerveClearM;
            return side > 0
                ? Mathf.Max(foe.Lateral, foe.PlannedLateral) + clear
                : Mathf.Min(foe.Lateral, foe.PlannedLateral) - clear;
        }

        // +1 right, -1 left, 0 = nowhere to go. A side is open if the spot alongside the car is on the road and
        // nothing is in, or about to be in, the lane between here and there. The shorter move wins; a tie goes to
        // the side with more road.
        int ChooseSide(in PackSelf me, in PackCar foe, IReadOnlyList<PackCar> cars)
        {
            int best = 0;
            float bestMove = float.MaxValue;
            float bestRoom = 0f;
            for (int side = -1; side <= 1; side += 2)
            {
                float t = SwerveTarget(me, foe, side);
                if (t < me.TrackLo || t > me.TrackHi) continue;
                if (LaneBlocked(me, t, cars, foe.Id)) continue;
                float move = Mathf.Abs(t - me.Lateral);
                float room = side > 0 ? me.TrackHi - t : t - me.TrackLo;
                bool better = move < bestMove - 0.05f || (Mathf.Abs(move - bestMove) <= 0.05f && room > bestRoom);
                if (!better) continue;
                best = side;
                bestMove = move;
                bestRoom = room;
            }
            return best;
        }

        // Is anything in, or crossing into, the lane at `target` near enough to me to hit while I move there?
        // Also any car sitting in the strip I'd cross on the way. Cars in the lane I'm leaving are not the
        // question here — the speed law already deals with them.
        public bool LaneBlocked(in PackSelf me, float target, IReadOnlyList<PackCar> cars, int ignoreId)
        {
            if (cars == null) return false;
            var s = Settings;
            float myMps = Mathf.Max(0f, me.SpeedMph) * PackAvoidance.MphToMps;
            float tLo = target - me.HalfWidth - s.LaneMarginM;
            float tHi = target + me.HalfWidth + s.LaneMarginM;
            float stripLo = Mathf.Min(me.Lateral, target) + me.HalfWidth;
            float stripHi = Mathf.Max(me.Lateral, target) - me.HalfWidth;

            for (int i = 0; i < cars.Count; i++)
            {
                var c = cars[i];
                if (c.Id == ignoreId) continue;
                float lengths = me.HalfLength + c.HalfLength;
                float cMps = Mathf.Max(0f, c.SpeedMph) * PackAvoidance.MphToMps;
                float behind = lengths + s.SideWindowBehindM + Mathf.Max(0f, cMps - myMps) * s.MergeTimeSec;
                float ahead = lengths + s.SideWindowAheadM + Mathf.Max(0f, myMps - cMps) * s.MergeTimeSec;
                if (c.Gap < -behind || c.Gap > ahead) continue;
                if (PackAvoidance.Overlaps(tLo, tHi, c.LaneLo, c.LaneHi)) return true;
                if (stripHi > stripLo && PackAvoidance.Overlaps(stripLo, stripHi, c.LaneLo, c.LaneHi)) return true;
            }
            return false;
        }
    }
}
