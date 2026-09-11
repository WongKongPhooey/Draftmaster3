using UnityEngine;

namespace Draftmaster.Sim
{
    // Tyre temperature maths, lifted out of TireModel so it can be unit-tested in EditMode (the tyres
    // themselves can only be judged by driving them, which isn't always available).
    //
    // Two ideas live here:
    //
    //   * Thermal mass. A tyre is a lump of rubber, not a thermometer — it takes most of a lap to come in and
    //     just as long to go cold again. heatRate and coolRate together decide WHERE a tyre settles for a given
    //     amount of work; inertia divides both, so raising it slows the swing without moving the settling
    //     temperature at all. That split is the whole point: the temperatures a hot lap produces stay where they
    //     were tuned, the tyre just stops snapping to them inside a single corner.
    //
    //   * Scrub heat. Lateral load alone says nothing about steering — a car tracking dead straight and a car
    //     sawing at the wheel down the same straight look identical to it. Steering angle is the missing term,
    //     and the scrubbing lands mostly on the front tyres, which is why weaving warms the fronts first.
    public static class TyreThermal
    {
        // Friction heat scales with how fast the rubber is being dragged over the road. The floor is what a
        // working tyre makes at a crawl; at SpeedHeatRefMps the speed term adds a full 1.0 on top.
        public const float SpeedHeatFloor = 0.25f;
        public const float SpeedHeatRefMps = 45f;

        public static float SpeedHeatFactor(float speedMps)
            => SpeedHeatFloor + Mathf.Max(0f, speedMps) / SpeedHeatRefMps;

        // °C per second going in, for a tyre doing `heatWork` (0..1-ish, past 1 when a loaded tyre is over its
        // share of the axle) at this speed.
        public static float HeatPerSecond(float heatWork, float speedMps, float heatRate, float inertia)
            => (heatRate / Inertia(inertia)) * Mathf.Max(0f, heatWork) * SpeedHeatFactor(speedMps);

        // Cooling is proportional to how far above ambient the tyre is, so this is the per-second coefficient
        // rather than a rate. Airflow over the wheel carries heat away faster the quicker you go.
        public static float CoolCoefficient(float speedMps, float coolRate, float airCool, float inertia)
            => (coolRate / Inertia(inertia)) * (1f + Mathf.Max(0f, speedMps) * airCool);

        // One step of the tyre's temperature. Never drops below ambient — the tyre cannot cool itself past the
        // air it is sitting in.
        public static float Step(float tempC, float ambientC, float heatWork, float speedMps,
                                 float heatRate, float coolRate, float airCool, float inertia, float dt)
        {
            float heat = HeatPerSecond(heatWork, speedMps, heatRate, inertia);
            float cool = CoolCoefficient(speedMps, coolRate, airCool, inertia) * (tempC - ambientC);
            float next = tempC + (heat - cool) * dt;
            return next < ambientC ? ambientC : next;
        }

        // Where a tyre held at this work and speed ends up: heat in equals heat out. Independent of inertia,
        // which is what makes inertia safe to turn.
        public static float EquilibriumC(float ambientC, float heatWork, float speedMps,
                                         float heatRate, float coolRate, float airCool, float inertia)
        {
            float k = CoolCoefficient(speedMps, coolRate, airCool, inertia);
            if (k <= 0f) return float.PositiveInfinity;
            return ambientC + HeatPerSecond(heatWork, speedMps, heatRate, inertia) / k;
        }

        // Seconds to cover 63% of the gap to wherever the tyre is heading — one time constant. This is the
        // number that decides whether tyres come in over a lap or arrive inside one corner.
        public static float ResponseSeconds(float speedMps, float coolRate, float airCool, float inertia)
        {
            float k = CoolCoefficient(speedMps, coolRate, airCool, inertia);
            return k > 0f ? 1f / k : float.PositiveInfinity;
        }

        // Scrub term: 0 with the wheel straight, 1 at fullLockDeg of front-wheel angle and beyond. Normalised
        // against a deliberately small angle — an oval is steered in fractions of a degree, so measuring lock
        // against the car's full 28° would make every amount of steering read as none.
        public static float Scrub01(float steerDeg, float fullLockDeg)
            => Mathf.Clamp01(Mathf.Abs(steerDeg) / Mathf.Max(fullLockDeg, 0.01f));

        static float Inertia(float inertia) => Mathf.Max(inertia, 0.01f);
    }
}
