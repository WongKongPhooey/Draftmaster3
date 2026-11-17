using UnityEngine;

namespace Draftmaster.Sim
{
    // Fuel, scaled to the race. A tank is sized against the race distance rather than in absolute litres per second:
    // run flat out all the way, a full tank lasts one stint of the race plus a couple of laps spare. With no stops
    // required (today's setting) that is the whole race plus spare, so the field can go the distance without a fuel
    // stop but not with much to play with. With requiredStops = 1 a tank covers half the race plus spare, so it takes
    // a stop to make the distance.
    //
    // Burn goes with distance and throttle: flat out burns the planned rate, a lift or a tow burns less (a car never
    // burns less than IdleShare of it - the engine is still turning). That is what makes saving fuel a thing a driver
    // does: lift a little, draft a little more.
    public static class FuelPlan
    {
        // Share of the full-throttle burn a car still uses off the throttle.
        public const float IdleShare = 0.25f;

        // Laps one tank has to cover at full throttle.
        public static float StintLaps(int raceLaps, int requiredStops, float spareLaps)
            => Mathf.Max(1f, raceLaps / (float)(Mathf.Max(0, requiredStops) + 1) + Mathf.Max(0f, spareLaps));

        // Litres per metre at full throttle that makes a full tank last StintLaps.
        public static float LitresPerMetreFull(float capacityLitres, int raceLaps, float lapLengthM, int requiredStops, float spareLaps)
        {
            if (capacityLitres <= 0f || raceLaps <= 0 || lapLengthM <= 0f) return 0f;
            return capacityLitres / (StintLaps(raceLaps, requiredStops, spareLaps) * lapLengthM);
        }

        // Share of the full-throttle burn at this throttle.
        public static float BurnShare(float throttle01) => IdleShare + (1f - IdleShare) * Mathf.Clamp01(throttle01);

        // The most throttle a driver can use on average and still make lapsRemaining (+ marginLaps) on what is in the
        // tank: 1 while flat out makes it, less as it falls short, never under minThrottle (a car that slow is no use).
        public static float ThrottleCapToFinish(float litres, float lapsRemaining, float litresPerLapFull,
                                                float marginLaps, float minThrottle = 0.5f)
        {
            if (litresPerLapFull <= 0f || lapsRemaining <= 0f) return 1f;
            float lapsOfFuel = Mathf.Max(0f, litres) / litresPerLapFull;          // at full throttle
            float need = lapsRemaining + Mathf.Max(0f, marginLaps);
            if (lapsOfFuel >= need) return 1f;
            float share = lapsOfFuel / need;                                       // the burn share that just makes it
            float t = (share - IdleShare) / (1f - IdleShare);
            return Mathf.Clamp(t, Mathf.Clamp01(minThrottle), 1f);
        }
    }
}
