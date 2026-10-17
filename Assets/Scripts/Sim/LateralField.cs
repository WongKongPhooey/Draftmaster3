using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Sim
{
    // Where across the road an AI car should be, NR2003's way (Docs/NR2003-Internals.md, "The lateral
    // acceleration field").
    //
    // The road between the car's lateral bounds is cut into slots. Each slot is scored by how far the car could
    // get in the next few seconds if it drove there:
    //   * on its own, at the speed the air there allows - clean air, the tow of a car ahead, the push of a line
    //     behind (the same shapes DraftAero gives the physics);
    //   * but never further than the car ahead in that slot lets it - its speed over the horizon plus the room to a
    //     bumper's length behind it.
    // The car aims for the best slot it can reach. A car alongside is a wall - the slots past it can't be reached -
    // and a slot in front of a car closing fast from behind is not taken. Moving costs a little per metre, and the
    // slot already aimed for gets a small bonus, so a car doesn't wander for nothing.
    //
    // No rule says "pass" or "follow the line": a car tucks in behind another because the tow there beats clean air,
    // pulls out when the slot beside is going somewhere faster, and a line of cars holds together because each car
    // in it scores the slot behind the one ahead highest. The speed cap is the tightest follow cap on the way to the
    // chosen slot, so a car can't cut across someone's bumper at a closing speed it couldn't shed.
    //
    // Everything is in one car's track frame: metres along the track between car centres (+ = ahead), metres
    // across it (either sign convention, used consistently), speeds in m/s.
    public struct FieldCar
    {
        public float Gap;        // centre to centre along the track (m): + = ahead of me
        public float Lateral;    // across the track now (m)
        public float LateralRate; // m/s across the track
        public float Speed;      // m/s
        public float Heading;    // the lateral it has committed to moving to (NaN = staying where it is)

        // How far this car is from a lateral, counting where it is going as well as where it is: a car committed to
        // moving into a slot is in it from the moment it commits - waiting for it to arrive is too late to react.
        public float Across(float lat)
        {
            float d = System.Math.Abs(Lateral - lat);
            if (!float.IsNaN(Heading))
            {
                float lo = System.Math.Min(Lateral, Heading), hi = System.Math.Max(Lateral, Heading);
                d = lat < lo ? lo - lat : lat > hi ? lat - hi : 0f;
            }
            return d;
        }
    }

    public sealed class LateralField
    {
        public struct Settings
        {
            public float SlotWidth;          // m
            public float Horizon;            // s over which progress is scored
            public float CarLength;          // m
            public float CarWidth;           // m
            public float SideGap;            // m of daylight kept to a car alongside
            public float PredictSeconds;     // a car alongside is walled off where it will be this far ahead
            public float FollowGap;          // centre-to-centre gap (m) held behind a car in a line
            public float CorridorFull;       // lateral separation (m) inside which a car ahead fully caps a slot:
                                             // a car's width - any less and the noses touch
            public float CorridorClear;      // ...and beyond which it doesn't cap it at all
            public float CloseDecel;         // m/s² a car is willing to shed closing speed at behind another
            public float TowMaxGap;          // m: the tow fades to nothing here
            public float TowHalfWidth;       // m: and across
            public float TowGain;            // fraction of top speed a full tow adds
            public float PushLateral;        // m: a car behind within this counts as in our line
            public float PushLinkGap;        // m between cars in a pushing line
            public int PushMaxChain;
            public float PushGain;           // fraction of top speed a full push adds
            public float BehindTimeToContact;// s: a slot in front of a car that would reach us sooner is not taken
            public float BehindClear;        // m of bumper-to-bumper room a slot needs behind us to be cut into
            public float MoveCost;           // m of progress per m moved across
            public float Stickiness;         // m of progress the current target is given over the rest
            public float LinePull;           // m of progress per m away from the preferred line
            public float CornerRadius;       // m: the tightest corner within the horizon (MaxValue = none)
            public float OffLineCost;        // how much of the off-line radius change a corner costs (0 = an oval's lanes)

            public static Settings Default => new Settings
            {
                SlotWidth = 0.25f, Horizon = 3f, CarLength = 4.8f, CarWidth = 2f, SideGap = 0.4f,
                PredictSeconds = 0.5f, FollowGap = 6.2f, CorridorFull = 2f, CorridorClear = 2.4f,
                CloseDecel = 1.2f, TowMaxGap = 35f, TowHalfWidth = 2.5f, TowGain = 0.1f,
                PushLateral = 1.4f, PushLinkGap = 9f, PushMaxChain = 8, PushGain = 0.03f,
                BehindTimeToContact = 2f, BehindClear = 1.5f, MoveCost = 0.1f, Stickiness = 1f, LinePull = 0.15f,
                CornerRadius = float.MaxValue, OffLineCost = 0f,
            };
        }

        public struct Result
        {
            public float TargetLateral;
            public float SpeedCap;     // m/s; float.MaxValue when nothing caps us
            public float WallLow, WallHigh;
            public float Score;        // progress (m) at the target
            public bool Squeezed;      // walls on both sides cross: aimed at the middle of the gap they leave
        }

        public Settings S;
        float[] _progress = new float[0];
        float[] _cap = new float[0];
        bool[] _blocked = new bool[0];

        public LateralField() : this(Settings.Default) { }
        public LateralField(Settings s) { S = s; }

        public int SlotCount { get; private set; }
        public float Low { get; private set; }
        public float SlotLateral(int i) => Low + i * S.SlotWidth;
        public float Progress(int i) => _progress[i];
        public bool Blocked(int i) => _blocked[i];

        int SlotOf(float lateral) => Mathf.Clamp(Mathf.RoundToInt((lateral - Low) / S.SlotWidth), 0, SlotCount - 1);

        // myLat/mySpeed: where we are and how fast. topSpeed: our clean-air top speed here (m/s). lo/hi: the road we
        // may use. preferredLat: our racing line. currentTarget: what we aimed for last step (NaN = none).
        // lanes: when given, the car only ever aims for one of these laterals (a superspeedway's grooves) - the field
        // decides which, the grooves keep the cars a lane apart.
        public Result Solve(float myLat, float mySpeed, float topSpeed, float lo, float hi, float preferredLat,
                            float currentTarget, IReadOnlyList<FieldCar> cars, IReadOnlyList<float> lanes = null)
        {
            if (hi < lo) { float t = lo; lo = hi; hi = t; }
            Low = lo;
            SlotCount = Mathf.Max(1, Mathf.FloorToInt((hi - lo) / S.SlotWidth) + 1);
            if (_progress.Length < SlotCount)
            {
                _progress = new float[SlotCount];
                _cap = new float[SlotCount];
                _blocked = new bool[SlotCount];
            }

            // Walls: every car alongside bounds how far toward it we can go.
            float wallLow = float.MinValue, wallHigh = float.MaxValue;
            float sep = S.CarWidth + S.SideGap;
            float alongside = S.CarLength + 0.5f;
            for (int c = 0; c < cars.Count; c++)
            {
                var car = cars[c];
                if (Mathf.Abs(car.Gap) > alongside) continue;
                // Beside us, not in front or behind: a car more in line than half a width is one we follow or lead.
                if (Mathf.Abs(car.Lateral - myLat) < S.CarWidth * 0.5f) continue;
                float at = car.Lateral + car.LateralRate * S.PredictSeconds;
                if (!float.IsNaN(car.Heading)) at = car.Lateral >= myLat ? Mathf.Min(at, car.Heading) : Mathf.Max(at, car.Heading);
                // Where it is now or where it's heading, whichever is nearer us.
                if (car.Lateral >= myLat) wallHigh = Mathf.Min(wallHigh, Mathf.Min(at, car.Lateral) - sep);
                else wallLow = Mathf.Max(wallLow, Mathf.Max(at, car.Lateral) + sep);
            }

            float h = S.Horizon;
            float freeSpeed = topSpeed * (1f + S.TowGain) * (1f + S.PushGain);
            for (int i = 0; i < SlotCount; i++)
            {
                float lat = SlotLateral(i);
                float tow = 0f, cap = float.MaxValue, capDist = float.MaxValue;
                bool blocked = lat < wallLow || lat > wallHigh;
                // Every slot gets its caps, blocked or not: a blocked slot can't be aimed for, but the one we are in still
                // has to hold us off the car ahead in it.
                for (int c = 0; c < cars.Count; c++)
                {
                    var car = cars[c];
                    float across = car.Across(lat);
                    if (car.Gap > 0f)
                    {
                        float inLine = Mathf.Abs(car.Lateral - lat);
                        if (car.Gap <= S.TowMaxGap && inLine <= S.TowHalfWidth)
                            tow = Mathf.Max(tow, (1f - car.Gap / S.TowMaxGap) * (1f - inLine / S.TowHalfWidth));
                        float overlap = 1f - Mathf.Clamp01((across - S.CorridorFull) / Mathf.Max(S.CorridorClear - S.CorridorFull, 0.01f));
                        if (overlap > 0f)
                        {
                            // How far the car ahead lets us get: its progress plus the room to a bumper behind it,
                            // faded out as it moves out of the slot's corridor.
                            float room = car.Gap - S.FollowGap;
                            float d = car.Speed * h + room;
                            capDist = Mathf.Min(capDist, Mathf.Lerp(freeSpeed * h, d, overlap));
                            cap = Mathf.Min(cap, Mathf.Lerp(freeSpeed, FollowSpeed(car.Speed, room), overlap));
                        }
                    }
                }
                // A car behind in this slot that would reach us before we could get going is not cut in on - nor is one
                // right on our bumper, closing or not. Judged on the speed we'd settle to there, not the speed we have:
                // a car pulling out of a tow slows as it goes, and dropped in front of a quicker line it gets hit.
                float settle = Mathf.Min(mySpeed, topSpeed * (1f + S.TowGain * tow));
                for (int c = 0; c < cars.Count && !blocked; c++)
                {
                    var car = cars[c];
                    if (car.Across(lat) >= S.CarWidth || -car.Gap <= alongside) continue;
                    float closing = car.Speed - settle;
                    float clear = -car.Gap - S.CarLength;
                    if (clear < S.BehindClear || (closing > 0f && clear / closing < S.BehindTimeToContact)) blocked = true;
                }
                float push = blocked ? 0f : Push(lat, cars);
                float eq = topSpeed * (1f + S.PushGain * push) * (1f + S.TowGain * tow);
                // Off the racing line through a corner the car is slower: its radius tightens (or its arc lengthens)
                // by the offset, and corner speed goes with the square root of the radius.
                if (S.OffLineCost > 0f && S.CornerRadius < float.MaxValue)
                    eq *= Mathf.Sqrt(Mathf.Clamp(1f - S.OffLineCost * Mathf.Abs(lat - preferredLat) / S.CornerRadius, 0.25f, 1f));
                float progress = Mathf.Min(0.5f * (mySpeed + eq) * h, capDist);
                progress -= S.LinePull * Mathf.Abs(lat - preferredLat);
                _progress[i] = progress;
                _cap[i] = cap;
                _blocked[i] = blocked;
            }

            var r = new Result { WallLow = wallLow, WallHigh = wallHigh, SpeedCap = float.MaxValue };
            int here = SlotOf(myLat);
            if (wallLow > wallHigh)
            {
                // Three wide in the middle: there is no slot between the walls - aim for the middle of the gap.
                r.Squeezed = true;
                r.TargetLateral = Mathf.Clamp(0.5f * (wallLow + wallHigh), lo, hi);
                r.SpeedCap = _cap[here];
                r.Score = _progress[here];
                return r;
            }

            // Already closer to a car alongside than we should be: start from the nearest slot that isn't.
            int start = here;
            if (SlotLateral(start) > wallHigh) start = SlotOf(wallHigh);
            if (SlotLateral(start) < wallLow) start = SlotOf(wallLow);
            int aim = float.IsNaN(currentTarget) ? -1 : SlotOf(currentTarget);
            int best = start;
            float bestScore = float.NegativeInfinity;
            float bestLat = float.NaN;
            if (lanes != null && lanes.Count > 0)
            {
                // Only the grooves: each one we can reach without driving through a blocked slot.
                best = -1;
                for (int k = 0; k < lanes.Count; k++)
                {
                    int i = SlotOf(lanes[k]);
                    if (_blocked[i] && i != start) continue;
                    bool reachable = true;
                    for (int j = Mathf.Min(start, i); j <= Mathf.Max(start, i) && reachable; j++)
                        if (_blocked[j] && j != start) reachable = false;
                    if (!reachable) continue;
                    float score = _progress[i] - S.MoveCost * Mathf.Abs(lanes[k] - myLat);
                    if (aim >= 0 && Mathf.Abs(i - aim) * S.SlotWidth <= 0.5f) score += S.Stickiness;
                    if (score > bestScore) { bestScore = score; best = i; bestLat = lanes[k]; }
                }
                if (best < 0) best = start;   // boxed in: hold where the walls leave us
            }
            // Any slot: walk out from there each way; a blocked slot ends the walk - we can't drive through a car.
            else
            for (int dir = -1; dir <= 1; dir += 2)
            {
                for (int i = dir < 0 ? start : start + 1; i >= 0 && i < SlotCount; i += dir)
                {
                    if (_blocked[i] && i != start) break;
                    float score = _progress[i] - S.MoveCost * Mathf.Abs(SlotLateral(i) - myLat);
                    // Near what we were already aiming for: a move has to beat that by Stickiness to change it.
                    if (aim >= 0 && Mathf.Abs(i - aim) * S.SlotWidth <= 0.5f) score += S.Stickiness;
                    if (score > bestScore) { bestScore = score; best = i; }
                }
            }
            r.TargetLateral = float.IsNaN(bestLat) ? SlotLateral(best) : bestLat;
            r.Score = _progress[best];
            int a = Mathf.Min(here, best), b = Mathf.Max(here, best);
            if (wallLow > float.MinValue || wallHigh < float.MaxValue) { a = Mathf.Min(a, start); b = Mathf.Max(b, start); }
            for (int i = a; i <= b; i++) r.SpeedCap = Mathf.Min(r.SpeedCap, _cap[i]);
            return r;
        }

        // After Solve: may this lateral still be driven to - not walled off, nothing blocking on the way from here?
        public bool Reachable(float fromLat, float toLat)
        {
            if (SlotCount == 0) return false;
            int a = SlotOf(fromLat), b = SlotOf(toLat);
            for (int i = Mathf.Min(a, b); i <= Mathf.Max(a, b); i++) if (_blocked[i] && i != a) return false;
            return true;
        }

        // After Solve: the tightest follow cap (m/s) between two laterals.
        public float CapAlong(float fromLat, float toLat)
        {
            float cap = float.MaxValue;
            if (SlotCount == 0) return cap;
            int a = SlotOf(fromLat), b = SlotOf(toLat);
            for (int i = Mathf.Min(a, b); i <= Mathf.Max(a, b); i++) cap = Mathf.Min(cap, _cap[i]);
            return cap;
        }

        // The speed to hold behind a car in our slot with this much room (m) left to a bumper behind it: close no
        // faster than we could shed at CloseDecel, then sit there - a touch under its speed if we're inside the gap.
        public float FollowSpeed(float aheadSpeed, float room)
        {
            if (room > 0f) return aheadSpeed + Mathf.Sqrt(2f * S.CloseDecel * room);
            return Mathf.Max(0f, aheadSpeed + Mathf.Max(room * 0.27f, -1.1f));
        }

        // The push (0..1) a line of cars nose to tail behind this lateral gives: DraftAero.Push's shape.
        static readonly List<float> _chain = new List<float>(16);
        float Push(float lat, IReadOnlyList<FieldCar> cars)
        {
            _chain.Clear();
            for (int c = 0; c < cars.Count; c++)
            {
                float g = -cars[c].Gap;
                if (g <= 0.5f || g > S.PushLinkGap * S.PushMaxChain) continue;
                if (Mathf.Abs(cars[c].Lateral - lat) > S.PushLateral) continue;
                _chain.Add(g);
            }
            if (_chain.Count == 0) return 0f;
            _chain.Sort();
            int count = 0;
            float prev = 0f;
            for (int i = 0; i < _chain.Count && count < S.PushMaxChain; i++)
            {
                if (_chain[i] - prev > S.PushLinkGap) break;
                prev = _chain[i];
                count++;
            }
            return 1f - Mathf.Pow(0.55f, count);
        }
    }
}
