using UnityEngine;

namespace Draftmaster.Sim
{
    // The choreography behind the title screen's crash: four cars thrown in from off the right edge, and
    // time easing to a dead stop as they land, leaving the pile frozen where the art slot used to be.
    //
    // Pure maths, no MonoBehaviour state, for the same reason CameraFeel is: the tableau itself can only be
    // judged in Play Mode, but "does every car finish inside the right-hand third", "does the clock actually
    // reach zero" and "is the hero car the front-most one" are all answerable in EditMode.
    //
    // Two clocks. `s` is wall clock, 0..1 across the whole run. `u` is choreography time, also 0..1, and it
    // is what every pose is a function of. Freeze() maps one to the other: u runs at a constant rate for the
    // first stretch, then decelerates to a halt exactly as it reaches 1. Nothing eases its own motion — the
    // cars fly at a constant rate in u — so the slowdown is time slowing, not four objects slowing down.
    //
    // Positions are in the 640x360 reference canvas the Iron Oval screens are laid out on, measured from the
    // bottom-left, so they line up with the menu column (which occupies x < 326) without knowing anything
    // about the screen's real aspect. Rotations are degrees, and start rotations carry whole extra turns so a
    // car spins into its resting angle rather than swinging to it.
    public static class TitleCrash
    {
        // The reference canvas the poses below are authored against (PixelUITheme's ReferenceWidth/Height).
        public const float CanvasWidth = 640f;
        public const float CanvasHeight = 360f;

        // The copy column's right edge. Nothing in the tableau starts left of this, so the crash can never
        // wash over the wordmark or the menu.
        public const float ColumnRightPx = 326f;

        // Which car is the star of the shot: front-most in the pile, biggest on screen, and the one that
        // wears the player's number once they have a team.
        public const int HeroIndex = 3;

        public struct CarPlan
        {
            public Vector2 startPos;        // off-screen entry, reference px
            public Vector2 endPos;          // where it comes to rest
            public float startRotation;     // degrees, including the extra turns it unwinds through
            public float endRotation;
            public float lengthPx;          // drawn length of the car, nose to tail
            public float arcPx;             // height of the hop it takes on the way in
            public float delay;             // choreography time before it enters
            public float travel;            // choreography time it spends in the air
            public int depth;               // 0 = rear-most; sets the draw order within the pile
        }

        public struct CarPose
        {
            public Vector2 position;
            public float rotation;
            public float progress;          // 0 = still off-screen, 1 = landed
        }

        public struct Impact
        {
            public float at;                // choreography time the hit lands
            public int car;                 // which car takes the dent
            public Vector2 pointPx;         // where the flash goes
            public float severity;          // 0..1, straight into VehicleDamage.OnImpact
            public Vector2 spray;           // direction the sparks favour
        }

        // Wall clock -> choreography time. Constant rate over the first `linearFraction` of the run, then a
        // quadratic glide that arrives at exactly 1 with exactly zero speed. The rate is solved rather than
        // guessed so the two halves join smoothly and the whole thing still covers the full sequence:
        // integrating k over [0,a] and k tapering to nothing over [a,1] gives k(1+a)/2 = 1.
        public static float Freeze(float s, float linearFraction)
        {
            s = Mathf.Clamp01(s);
            float a = Mathf.Clamp(linearFraction, 0f, 0.9f);
            float k = 2f / (1f + a);
            if (s <= a) return k * s;

            float d = s - a;
            float span = 1f - a;
            return Mathf.Clamp01(k * (a + d - (d * d) / (2f * span)));
        }

        // How fast choreography time is running, relative to its opening rate. 1 while the crash is at full
        // speed, 0 once it has stopped — which is what the particle systems' simulation speed rides on.
        public static float FreezeRate(float s, float linearFraction)
        {
            s = Mathf.Clamp01(s);
            float a = Mathf.Clamp(linearFraction, 0f, 0.9f);
            if (s <= a) return 1f;
            return Mathf.Clamp01(1f - (s - a) / (1f - a));
        }

        // The four cars, rear of the pile first. Every one enters from off the right edge (x > CanvasWidth)
        // so nothing ever crosses the copy column, and every one has landed by u = 1.
        public static CarPlan[] Field()
        {
            return new[]
            {
                // Broadside behind the pile, first in and first to stop.
                new CarPlan
                {
                    startPos = new Vector2(930f, 210f), endPos = new Vector2(566f, 118f),
                    startRotation = -108f + 350f,       endRotation = -108f,
                    lengthPx = 135f, arcPx = 35f, delay = 0f,     travel = 0.80f, depth = 0,
                },
                // Spun backwards into the top-right corner, half off the edge of the screen.
                new CarPlan
                {
                    startPos = new Vector2(880f, 430f), endPos = new Vector2(568f, 262f),
                    startRotation = 158f + 300f,        endRotation = 158f,
                    lengthPx = 150f, arcPx = 55f, delay = 0.08f, travel = 0.86f, depth = 1,
                },
                // Sliding in low, tucked under the hero's nose.
                new CarPlan
                {
                    startPos = new Vector2(940f, 60f),  endPos = new Vector2(420f, 105f),
                    startRotation = 34f - 380f,         endRotation = 34f,
                    lengthPx = 145f, arcPx = 25f, delay = 0.05f, travel = 0.84f, depth = 2,
                },
                // The hero: biggest, front-most, and still settling as the clock runs out.
                new CarPlan
                {
                    startPos = new Vector2(905f, 305f), endPos = new Vector2(470f, 190f),
                    startRotation = -28f + 400f,        endRotation = -28f,
                    lengthPx = 190f, arcPx = 40f, delay = 0.10f, travel = 0.88f, depth = 3,
                },
            };
        }

        // Where a car is at choreography time u. Linear in u on purpose (see Freeze), with a sine hop so it
        // arrives off the ground rather than sliding in flat.
        public static CarPose Evaluate(CarPlan plan, float u)
        {
            float p = plan.travel <= 0f ? 1f : Mathf.Clamp01((u - plan.delay) / plan.travel);
            Vector2 pos = Vector2.Lerp(plan.startPos, plan.endPos, p);
            pos.y += plan.arcPx * Mathf.Sin(Mathf.PI * p);

            return new CarPose
            {
                position = pos,
                // Plain Lerp, not LerpAngle: the extra turns baked into startRotation are the point, and
                // LerpAngle would take the short way round and throw them away.
                rotation = Mathf.Lerp(plan.startRotation, plan.endRotation, p),
                progress = p,
            };
        }

        // The four moments the pile actually connects, in order. Each one flashes sparks, coughs smoke and
        // puts a fresh dent in the named car on top of whatever it arrived carrying.
        public static Impact[] Impacts()
        {
            return new[]
            {
                new Impact { at = 0.55f, car = 0, pointPx = new Vector2(600f, 150f), severity = 0.70f,
                             spray = new Vector2(-0.6f, 0.8f) },
                new Impact { at = 0.66f, car = 3, pointPx = new Vector2(540f, 150f), severity = 0.95f,
                             spray = new Vector2(-0.9f, 0.45f) },
                new Impact { at = 0.74f, car = 2, pointPx = new Vector2(455f, 130f), severity = 0.80f,
                             spray = new Vector2(-0.5f, -0.85f) },
                new Impact { at = 0.86f, car = 1, pointPx = new Vector2(505f, 235f), severity = 0.85f,
                             spray = new Vector2(0.3f, 0.95f) },
            };
        }

        // When the pile starts smoking: the first proper contact, so the plume builds through the rest of
        // the sequence and is still hanging over the cars when everything stops.
        public const float PlumeStartsAt = 0.55f;
        public static readonly Vector2 PlumeCentrePx = new Vector2(505f, 150f);
    }
}
