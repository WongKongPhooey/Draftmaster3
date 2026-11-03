using UnityEngine;

namespace Draftmaster.Sim
{
    // Following the car ahead in a single-file oval train (Docs/Oval-Lanes.md, stage 2).
    //
    // A constant-time-headway follower: the gap wanted to the car ahead is a standstill gap plus a time gap at the
    // car's own speed, and the speed asked for is the car ahead's speed plus a share of the gap error:
    //
    //     wanted = standstillGap + headwaySeconds x v
    //     speed  = vAhead + gapGain x (gap - wanted)
    //
    // Two properties make it a train rather than an accordion:
    //  - The headway term is what damps the line. With the car's speed loop lagging by tau seconds, a gap error
    //    passed down the line shrinks at every frequency as long as headwaySeconds x (2 + gapGain x headwaySeconds)
    //    >= 2 tau - here 0.3 s x 2.15 against a speed loop of ~0.2 s, so a lift by the car at the front fades out
    //    down the train instead of growing. (With no lag the transfer from one car's speed to the next is
    //    (s + k) / ((1 + k h) s + k), never above 1.)
    //  - The approach is planned on the throttle alone. Closing from further back, the speed over the car ahead is
    //    held to what lifting can take off by the time the gap is right: sqrt(2 x b x (gap - wanted)), where b is
    //    the deceleration a lift gives right now. In a tow a lift sheds less (the car ahead is pushing the air
    //    aside), so b is smaller there and the car backs off earlier - the draft pulling it closer is planned for,
    //    not discovered at the bumper.
    // Braking only comes in when the car is genuinely too quick for the gap (SplineInputDriver brakes gently past a
    // margin), and hard only in an emergency: still closing with less room than a firm stop needs.
    public static class TrainFollow
    {
        [System.Serializable]
        public struct Spec
        {
            public float standstillGap;   // m, centre to centre, at a standstill
            public float headwaySeconds;  // s of time gap at the car's own speed, on top
            public float gapGain;         // 1/s: m/s asked for over the car ahead per metre of gap to spare
            public float liftShare;       // share of a lift's deceleration the approach plans on
            public float hardGap;         // m, centre to centre: closing inside this is an emergency
            public float emergencyDecel;  // m/s²: needing more than this to stop closing is an emergency

            public static Spec Default => new Spec
            {
                standstillGap = 7f,       // a 4.8 m car and 2.2 m of air
                headwaySeconds = 0.3f,    // ~29.5 m centre to centre at Texas speeds: inside the 35 m tow
                gapGain = 0.5f,           // overdamped with the speed loop: settles in ~2 s, no overshoot
                liftShare = 0.8f,
                hardGap = 6f,             // 1.2 m bumper to bumper
                emergencyDecel = 4f,
            };
        }

        public struct Command
        {
            public float speedMps;      // the most this car should be doing for the gap
            public float wantedGap;     // m, centre to centre
            public bool emergency;      // closing too fast for a gentle response: brake as hard as it takes
        }

        public static float WantedGap(in Spec s, float speedMps) =>
            s.standstillGap + s.headwaySeconds * Mathf.Max(0f, speedMps);

        // gap: m centre to centre to the car ahead in the lane (along the track). liftDecelMps2: what lifting off
        // the throttle sheds right now (less in a tow).
        public static Command Follow(in Spec s, float gap, float speedMps, float aheadSpeedMps, float liftDecelMps2)
        {
            var c = new Command { wantedGap = WantedGap(s, speedMps) };
            float spare = gap - c.wantedGap;
            float linear = aheadSpeedMps + s.gapGain * spare;
            float b = Mathf.Max(0.2f, liftDecelMps2 * s.liftShare);
            float approach = aheadSpeedMps + Mathf.Sqrt(2f * b * Mathf.Max(0f, spare));
            c.speedMps = Mathf.Max(0f, Mathf.Min(linear, approach));

            float closing = speedMps - aheadSpeedMps;
            float room = gap - s.hardGap;
            if (closing > 0f)
                c.emergency = room <= 0f || closing * closing / (2f * room) > s.emergencyDecel;
            return c;
        }
    }
}
