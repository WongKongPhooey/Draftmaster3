using UnityEngine;

namespace Draftmaster.Sim
{
    // Passing by lane change on an oval (Docs/Oval-Lanes.md, stage 3). Pure: SplineDriver runs the move, OvalPass
    // feeds it the field, the EditMode tests check the rules without a scene.
    //
    // A car in a lane train (TrainFollow) may move to the other lane to pass the car it is following when:
    //  - it is being held up: close behind the car ahead (gap within engageGap of the gap it wants),
    //  - it has a run on it: its own speed plan where that car is, over that car's, or a closing speed, of at least
    //    minAdvantage (smoothed over advantageSeconds, so a blip does not start a move),
    //  - the other lane is clear alongside and ahead (room to pull alongside and a train gap beyond), and behind
    //    (the car there gets a full train gap, more if it is closing - nobody is cut off),
    //  - the road over the move is not a corner taken at more than maxCornerAccel of lateral load (the move itself
    //    adds lateral acceleration, and the speed plan only has a few per cent of grip in hand at the limit).
    //
    // The move is a lateral blend from one lane's line to the other with the sideways speed capped at
    // maxLateralSpeed and the sideways acceleration at lateralAccel, so it starts, cruises and settles with no
    // swerve and lands exactly on the new line. If the other lane stops being clear (at abortShare of the start
    // thresholds) while the car is still mostly in its own lane, it goes back the same smooth way; past that
    // it completes and the new lane's train law handles whatever is there.
    public static class LaneChange
    {
        [System.Serializable]
        public struct Spec
        {
            public float maxLateralSpeed;    // m/s sideways at most
            public float lateralAccel;       // m/s² sideways at most, starting and stopping the move
            public float minAdvantage;       // m/s of run on the car ahead needed to pull out
            public float advantageSeconds;   // s the run is smoothed over
            public float engageGap;          // m: only while the gap ahead is within this of the gap wanted
            public float aheadMargin;        // m on top of the room needed ahead in the other lane
            public float closingLookSeconds; // s of a closing car's closing speed added to the room it is given
            public float abortShare;         // share of the start room under which a move still mostly undone aborts
            public float cooldownSeconds;    // s after a move before the next may start
            public float maxCornerAccel;     // m/s² of cornering load over the move above which no move starts

            public static Spec Default => new Spec
            {
                maxLateralSpeed = 2f,       // 3.2 m in ~2.9 s; 1.5° of heading at Texas speeds
                lateralAccel = 1.5f,        // well inside the ~2 m/s² the speed plan leaves in hand
                minAdvantage = 0.8f,        // ~1% of pace at Texas
                advantageSeconds = 1f,
                engageGap = 8f,
                aheadMargin = 10f,          // two car lengths: pull fully alongside the car being passed
                closingLookSeconds = 3f,
                abortShare = 0.6f,
                cooldownSeconds = 3f,
                maxCornerAccel = 12f,       // straights, dog-legs and the last of a corner exit; not mid-turn
            };
        }

        // Room (m, centre to centre along the track) a car needs clear ahead of it in the other lane: enough to pull
        // alongside the car it is passing with aheadMargin to spare, and never less than a train gap at its speed.
        public static float RoomAhead(in Spec s, in TrainFollow.Spec train, float mySpeedMps, float gapToCarAhead) =>
            Mathf.Max(TrainFollow.WantedGap(train, mySpeedMps), gapToCarAhead) + s.aheadMargin;

        // Room (m) a car behind in the other lane must have: its own train gap, plus what it would close in
        // closingLookSeconds if it is quicker.
        public static float RoomBehind(in Spec s, in TrainFollow.Spec train, float mySpeedMps, float behindSpeedMps) =>
            TrainFollow.WantedGap(train, behindSpeedMps) + Mathf.Max(0f, behindSpeedMps - mySpeedMps) * s.closingLookSeconds;

        // Whether a car `along` metres from this one (centre to centre, + ahead, - behind) in the other lane leaves
        // room for the move. share = 1 to start one, abortShare while it is under way.
        public static bool Clears(in Spec s, in TrainFollow.Spec train, float along, float otherSpeedMps,
                                  float mySpeedMps, float gapToCarAhead, float share)
        {
            if (along >= 0f) return along >= share * RoomAhead(s, train, mySpeedMps, gapToCarAhead);
            return -along >= share * RoomBehind(s, train, mySpeedMps, otherSpeedMps);
        }

        // Whether a car with this (smoothed) run, this far behind the car ahead, wants out.
        public static bool WantsToPass(in Spec s, float smoothedAdvantageMps, float gap, float wantedGap) =>
            smoothedAdvantageMps >= s.minAdvantage && gap <= wantedGap + s.engageGap;

        // One step of the move. progress 0..1 from the old lane's line to the new one, rate in progress per second.
        // spacing = metres between the two lines here. Returns true when the car has landed on the new line (progress
        // exactly 1, rate 0). Sideways speed <= maxLateralSpeed, sideways acceleration <= lateralAccel throughout.
        public static bool Step(in Spec s, ref float progress, ref float rate, float spacing, float dt)
        {
            spacing = Mathf.Max(0.5f, spacing);
            float vMax = s.maxLateralSpeed / spacing, aMax = s.lateralAccel / spacing;
            float remaining = 1f - progress;
            float want = Mathf.Min(vMax, Mathf.Sqrt(2f * aMax * Mathf.Max(0f, remaining)));
            rate = Mathf.MoveTowards(rate, want, aMax * dt);
            progress += rate * dt;
            if (progress < 0f) { progress = 0f; rate = Mathf.Max(0f, rate); }
            // Landed. The braking ramp (rate = sqrt(2 a remaining)) overshoots in its last step or two, at a
            // sideways speed of a few cm/s, so the snap onto the line is a millimetre or less.
            if (progress >= 1f - 1e-4f)
            {
                progress = 1f;
                rate = 0f;
                return true;
            }
            return false;
        }

        // Whether a move this far through, moving this fast, can still be called off and stop short of the middle of
        // the road (so turning back never takes the car more than halfway across).
        public static bool CanAbort(in Spec s, float progress, float rate, float spacing)
        {
            spacing = Mathf.Max(0.5f, spacing);
            float aMax = s.lateralAccel / spacing;
            float stop = rate > 0f ? rate * rate / (2f * aMax) : 0f;
            return progress + stop < 0.5f;
        }

        // How long (s) a move across `spacing` metres takes from a standstill sideways to landed.
        public static float MoveDuration(in Spec s, float spacing)
        {
            spacing = Mathf.Max(0.5f, spacing);
            // Triangular profile when it never reaches maxLateralSpeed, trapezoid otherwise.
            float tri = 2f * Mathf.Sqrt(spacing / s.lateralAccel);
            float vPeak = Mathf.Sqrt(spacing * s.lateralAccel);
            if (vPeak <= s.maxLateralSpeed) return tri;
            float tA = s.maxLateralSpeed / s.lateralAccel;
            float dA = s.maxLateralSpeed * tA;   // both ramps together
            return 2f * tA + (spacing - dA) / s.maxLateralSpeed;
        }
    }
}
