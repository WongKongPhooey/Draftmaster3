using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Sim
{
    // The choreography behind the title screen's crash: four cars coming down from above the top edge, and
    // time easing to a dead stop, leaving the moment frozen where the art slot used to be. Two of them are
    // only racing; the other two are the accident. See Field() for what the shot is and why.
    //
    // Pure maths, no MonoBehaviour state, for the same reason CameraFeel is: the tableau itself can only be
    // judged in Play Mode, but "does every car finish inside the right-hand third", "does the clock actually
    // reach zero" and "is the hero car the front-most one" are all answerable in EditMode.
    //
    // Two clocks. Wall clock is real seconds since the first car was due. `u` is choreography time, 0..1, and
    // it is what every pose is a function of. Tempo maps one to the other. Nothing eases its own motion — the
    // cars fly at a constant rate in u — so the slowdown is time slowing, not four objects slowing down.
    //
    // Positions are in the 640x360 reference canvas the Iron Oval screens are laid out on, measured from the
    // bottom-left, so they line up with the menu column (which occupies x < 326) without knowing anything
    // about the screen's real aspect.
    //
    // Rotations are the SPRITE's z angle, not the car's heading. The carset liveries are drawn nose-left, so
    // a car pointing along heading h is drawn at h + 180 — the same convention SplineDriver and
    // PlayerVehicleController run on (spriteFacesUp = false, angleOffsetDeg = 180). Get this wrong and the
    // whole field drives in backwards, which is exactly what it used to do.
    public static class TitleCrash
    {
        // The reference canvas the poses below are authored against (PixelUITheme's ReferenceWidth/Height).
        public const float CanvasWidth = 640f;
        public const float CanvasHeight = 360f;

        // The copy column's right edge. Nothing in the tableau starts left of this, so the crash can never
        // wash over the wordmark or the menu.
        public const float ColumnRightPx = 326f;

        // Which car is the star of the shot: front-most in the pile, and the one that wears the player's
        // number once they have a team. Every car is the same size, so draw order is what makes it the hero.
        public const int HeroIndex = 3;

        // One body, four times over: the cars are all the same machine, so nothing in the shot reads as a toy
        // parked next to a truck. Carset liveries are 64x32, so the body is half as wide as it is long.
        public const float CarLengthPx = 150f;
        public const float CarWidthPx = CarLengthPx * 0.5f;

        public struct CarPlan
        {
            public Vector2 startPos;        // off-screen entry, reference px
            public Vector2 endPos;          // where it comes to rest
            public float startRotation;     // degrees, including the extra turns it unwinds through
            public float endRotation;
            public float arcPx;             // how far it bows off the straight line on the way in
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

        // Two cars touching. These are found rather than authored: Settle() reports wherever the bodies
        // actually meet, so a flash never goes off in mid-air and never fails to where metal met metal.
        public struct Contact
        {
            public int a, b;                // the two cars, by plan index
            public Vector2 pointPx;         // where they meet
            public Vector2 normal;          // the push, a -> b
            public float depthPx;           // how far through each other they were before it
            public float severity;          // 0..1 from the closing speed, straight into VehicleDamage.OnImpact
        }

        // Wall clock (real seconds) -> choreography time. Three beats, and every one of them is the CLOCK,
        // not the cars: nothing in Field() eases itself, so what the eye reads as four cars braking is time
        // braking underneath them.
        //
        //   SLAM   the cars are thrown into shot far too fast to follow, and time sheds speed from the first
        //          frame. Half a second, and by the end of it the clock is barely moving. Nearly the whole
        //          choreography happens in here.
        //   CRAWL  the last sliver — the hit, the sparks, the last of the slide — played out at a
        //          near-constant creep. Another half second.
        //   STOP   nothing moves. The tableau is the title screen's art from here on.
        //
        // The two moving beats are joined at the RATE rather than at the position, so there is no kink where
        // one hands over to the other: the slam decays down onto exactly the crawl's speed, and the crawl
        // carries that speed to the end. The stop at the end IS abrupt, on purpose — but it cuts from the
        // crawl rate, which is a couple of percent of the entry, so what it reads as is a pause.
        public struct Tempo
        {
            public float slamSeconds;    // beat one: very fast -> very, very slow
            public float crawlSeconds;   // beat two: super slow motion
            public float crawlShare;     // how much of the choreography is saved for the crawl, 0..1
            public float slamDecay;      // how sharply the slam sheds speed. 0 = no brake at all; higher = a slam

            // What the tableau is authored around: half a second of slam, half a second of crawl, and only a
            // few percent of the movement left for the crawl to spend — which is what makes it a crawl.
            public static Tempo Default => new Tempo
            {
                slamSeconds = 0.5f,
                crawlSeconds = 0.5f,
                crawlShare = 0.06f,
                slamDecay = 3f,
            };

            public float Slam => Mathf.Max(1e-4f, slamSeconds);
            public float Crawl => Mathf.Max(0f, crawlSeconds);
            public float Decay => Mathf.Max(0f, slamDecay);

            // Nothing is held back for a crawl that has no time to happen in, or the clock would never reach 1.
            public float Share => Crawl <= 0f ? 0f : Mathf.Clamp(crawlShare, 0f, 0.9f);

            public float RunSeconds => Slam + Crawl;

            // Choreography per second during the crawl.
            public float CrawlRate => Crawl <= 0f ? 0f : Share / Crawl;

            // Choreography per second on the very first frame. Solved rather than authored: it is whatever
            // makes the slam cover everything the crawl isn't spending, given how fast it decays.
            public float EntryRate =>
                CrawlRate + (Decay + 1f) * Mathf.Max(0f, (1f - Share) - CrawlRate * Slam) / Slam;

            // Choreography time at `seconds` of wall clock. The slam is the integral of its own rate curve,
            // so it lands on exactly (1 - Share) at the handover and the crawl walks the rest to 1.
            public float Clock(float seconds)
            {
                if (seconds <= 0f) return 0f;
                if (seconds >= RunSeconds) return 1f;

                if (seconds < Slam)
                {
                    float ramp = (EntryRate - CrawlRate) * Slam / (Decay + 1f);
                    return Mathf.Clamp01(CrawlRate * seconds
                                         + ramp * (1f - Mathf.Pow(1f - seconds / Slam, Decay + 1f)));
                }

                return Mathf.Clamp01((1f - Share) + CrawlRate * (seconds - Slam));
            }

            // How fast choreography time is running at `seconds`, in choreography per second — so 1 means
            // "the whole shot in a second", which is the pace the particle bursts are authored at and what
            // their simulation speed rides on. Zero once the clock has stopped, which is what leaves the
            // sparks hanging mid-flight over a still pile instead of burning out above it.
            public float Rate(float seconds)
            {
                if (seconds < 0f || seconds >= RunSeconds) return 0f;
                if (seconds >= Slam) return CrawlRate;
                return CrawlRate + (EntryRate - CrawlRate) * Mathf.Pow(1f - seconds / Slam, Decay);
            }
        }

        // The two cars that are only racing, and the two that are having the accident. Index order is draw
        // order, rear-most first; the crash pair is 2 (the car already sideways, which gets T-boned) and 3
        // (the hero, which does the T-boning).
        public const int TurnedIndex = 2;
        public const int TurnerIndex = 3;

        public static bool IsInTheCrash(int index) => index == TurnedIndex || index == TurnerIndex;

        // The four cars, rear of the shot first. Every one drops in from above the top edge (y > CanvasHeight,
        // clear of its own rotated height) and travels downwards, so nothing is ever on screen when the
        // sequence opens and nothing crosses the copy column on the way.
        //
        // Every car lands at exactly u = 1 rather than parking early. The clock spends its last half second
        // creeping through the last few percent of u (see Tempo), so a car that finished at u = 0.85 would be
        // standing still through the whole slow-motion beat — the one part of the shot anybody can actually
        // watch. Landing on 1 means all four are still moving, by inches, right up to the pause.
        //
        // Rotations are sprite angles: heading + 180, because the liveries are drawn nose-left. Straight down
        // the screen is a heading of -90, so a car driving down the shot is drawn at 90.
        //
        // THIS IS A TABLEAU, NOT A PILE-UP. It used to be four cars thrown at the same spot and separated by
        // Settle every frame, which is a solver running at speed on four bodies — and it looked like one:
        // cars jittering through each other, sparks going off on frames where nothing had really happened.
        // The shot is now authored so that the ONLY two bodies that ever come near each other are the crash
        // pair, and even they close to within a fraction of a pixel rather than interpenetrating. Settle is
        // still run underneath as a backstop, but on this field it has nothing to push, so there is nothing
        // left to jitter.
        //
        // What the shot shows: two cars racing straight down the left of the slot in close company and never
        // touching, and to the right of them a T-BONE, staged to show the damage model off.
        //
        // A car that lost it somewhere off the top of the screen slides in broadside — travelling down the
        // shot with its body lying across its own line, still slewing round as it goes. The hero arrives from
        // much further back, running square down the road nose-first, and puts its nose into the middle of
        // that car's door at the exact moment the clock drops into slow motion. Then it keeps coming: the
        // last half-second is the two of them locked together, the hero still driving in, and the fold
        // deepening while you watch.
        //
        // That pairing is the point. The same one contact deforms two panels two completely different ways —
        // a flank takes the hero's narrow nose and gets a deep local gouge, while the hero's nose takes the
        // whole length of a flank and gets a wide shallow crease across it. A crater model cannot tell those
        // apart; it puts the same round dish on both. See BodyDeform.
        //
        // The T-bone geometry is authored, not hoped for: at the moment of contact the hero's heading is
        // within about ten degrees of square to the struck car's body, and its nose lands within a few pixels
        // of the middle of that car's side rather than on a corner. Both are asserted in EditMode, so
        // retiming the shot and quietly turning the T-bone into a glancing blow is a test failure.
        public static CarPlan[] Field()
        {
            return new[]
            {
                // Racing, trailing car. Same travel as its team-mate below so the gap between them never
                // changes: they are a pair running in company, and two cars that keep station cannot touch.
                new CarPlan
                {
                    startPos = new Vector2(372f, 882f), endPos = new Vector2(372f, 252f),
                    startRotation = 90f,                endRotation = 90f,
                    arcPx = 0f, delay = 0f, travel = 1f, depth = 0,
                },
                // Racing, leading car. Nose-down and dead straight — no spin, nothing to settle.
                new CarPlan
                {
                    startPos = new Vector2(372f, 720f), endPos = new Vector2(372f, 90f),
                    startRotation = 90f,                endRotation = 90f,
                    arcPx = 0f, delay = 0f, travel = 1f, depth = 1,
                },
                // The one that gets T-boned. It arrives already sideways — it lost it before the shot opened —
                // so it slides down the screen with its body lying ACROSS its line of travel, presenting a
                // flank to anything coming down the road behind it. It is still slewing round as it slides
                // (-14 to 18 degrees), which is what stops it reading as a car parked at an angle; the bow
                // across its travel is the back end coming round. It is the only car in the field that is not
                // pointing where it is going, and that is the whole reason there is something to hit.
                //
                // It is also SLOW: 320px of travel against the hero's 762, so it is barely creeping by the
                // time the hero reaches it. That gap in speed is the whole severity of the hit — everything
                // in the field lands at u = 1, so how hard one car arrives at another is decided entirely by
                // how much further it had to come. It enters frame early (u = 0.20) and slides the length of
                // the shot, which also gives the eye something to follow through the slam.
                new CarPlan
                {
                    startPos = new Vector2(466f, 470f), endPos = new Vector2(516f, 150f),
                    startRotation = -14f,               endRotation = 18f,
                    arcPx = 14f, delay = 0f, travel = 1f, depth = TurnedIndex,
                },
                // The hero, and the one that does it: dead square down the road, nose-first, from far further
                // back than anything else in the shot — it enters frame at u = 0.75, long after the other
                // car, so it is visibly arriving on a wreck already in progress rather than being part of
                // one. Coming from 1000px up means it is travelling roughly two and a half times the slider's
                // speed when it gets there.
                //
                // It is authored to end 30px INSIDE the other car, which is more than the bite allowance, and
                // both halves of that matter. Up to the allowance the two bodies simply sink into each other
                // (which is what makes the contact read as contact); past it, Settle pushes, and the push has
                // nowhere to go but into shoving the struck car down the road. So the slow-motion beat is the
                // hero burying its nose AND driving the car it hit along in front of it.
                new CarPlan
                {
                    startPos = new Vector2(496f, 1000f), endPos = new Vector2(500f, 238f),
                    startRotation = 96f,                endRotation = 96f,
                    arcPx = -22f, delay = 0.06f, travel = 0.94f, depth = TurnerIndex,
                },
            };
        }

        // ------------------------------------------------------------------ the one hit in the shot

        // The contact is authored rather than discovered. Settle can tell you THAT two boxes met, but not
        // which panel of which car — so it dented both cars at the midpoint between their centres, and the
        // damage landed wherever that happened to fall. This says it outright: the hero is damaged across its
        // nose, the car it T-bones is damaged across the door, and it happens over a window rather than in a
        // frame.
        //
        // A window, because a crush is not an event. It used to be one instant: a single OnImpact, one frame,
        // full severity, and the dent simply WAS there on the next frame like a decal. The bodywork model can
        // do better than that — press it a little harder each frame and the fold deepens a little more — so
        // the hit is spread across the whole slow-motion beat and what the shot actually shows is metal
        // giving way over half a second.
        public struct ImpactPlan
        {
            public int striker;        // the car doing the hitting — damaged across its NOSE
            public int struck;         // the car being T-boned — damaged across the DOOR
            public float atU;          // choreography time the two bodies meet and the fold starts
            public float throughU;     // choreography time the fold reaches full depth
            public Vector2 pointPx;    // where the flash and the smoke go
            public Vector2 normal;     // the push, striker -> struck; sparks spray along it
            public float severity;     // 0..1 at full crush, into VehicleDamage.OnImpact
        }

        // Contact lands just inside the crawl, and the crush then runs all the way to the end of the clock.
        // Tempo saves the last `crawlShare` (0.06) of the choreography for the slow half-second, so u = 0.942
        // is about two percent into the slow motion — the earliest the fold can start while still being
        // watchable, and the moment the two bodies actually touch. Everything before that is the slam, where
        // time is a blur and nobody would see a panel move.
        public const float ImpactU = 0.942f;

        // ...and it finishes exactly as time stops, so the last thing the tableau does before freezing is
        // fold. Stop the crush early and the shot has a dead beat at the end of it.
        public const float CrushEndU = 1f;

        // How far the two cars in the accident are allowed to bury into each other, in reference px.
        //
        // Settle used to hold every pair at a hard zero — no two bodies ever sharing a pixel — and the crash
        // pair came out looking like two cars parked next to each other rather than one buried in the other.
        // The liveries are opaque edge to edge, so "touching" drew as two rectangles meeting along a line,
        // which is not what a crash looks like from any angle.
        //
        // Cars in a wreck DO occupy the same space: the metal between them has folded, and the outline the
        // sprite is drawn at is no longer where the bodywork is. So the crash pair get an allowance and
        // everybody else still gets none — the racing pair can no more overlap than they could before.
        //
        // 26px is about a sixth of a car. It is also the ONE number that sets how hard the crash looks: the
        // two cars' fold depths are derived from it (TitleCrashScene sizes dentStrength off this, and the
        // pair split it half each), so the metal that caves in is exactly the metal the two bodies are
        // occupying in common. Raise this and the burial and both folds grow together; there is no way to
        // set it such that the panels retreat past each other and leave a void down the middle of the crash.
        public const float MaxBitePx = 26f;

        // The allowance at choreography time u: nothing before contact, opening on the same eased ramp as the
        // fold itself, so the two cars sink into each other exactly as fast as their panels give way.
        public static float Bite(float u)
        {
            float span = Mathf.Max(1e-4f, CrushEndU - ImpactU);
            float x = Mathf.Clamp01((u - ImpactU) / span);
            return MaxBitePx * (1f - (1f - x) * (1f - x));
        }

        // How far through the fold we are at choreography time u. Eased out, because that is how a crush
        // goes: the panel collapses in the first moments and then resists, so most of the depth is spent
        // early and the last of it creeps in as the clock dies.
        public static float Crush(in ImpactPlan hit, float u)
        {
            float span = Mathf.Max(1e-4f, hit.throughU - hit.atU);
            float x = Mathf.Clamp01((u - hit.atU) / span);
            return 1f - (1f - x) * (1f - x);
        }

        public static ImpactPlan[] Impacts()
        {
            return new[]
            {
                new ImpactPlan
                {
                    striker = TurnerIndex, struck = TurnedIndex,
                    atU = ImpactU, throughU = CrushEndU,
                    // On the hero's nose at the moment the two bodies meet, which at that moment is within a
                    // few pixels of the middle of the other car's side. Struck off the poses at ImpactU
                    // rather than eyeballed, so retiming the shot and leaving the flash behind — or turning
                    // the T-bone into a glancing blow down the side — is a test failure rather than a thing
                    // somebody notices later.
                    pointPx = new Vector2(503f, 211f),
                    normal = new Vector2(0.171f, -0.985f),
                    // Flat out. The cars arrive at each other about two and a half to one and there is no
                    // pre-existing damage on either of them any more, so this one contact is the only thing
                    // the screen has to show — it may as well be everything the bodywork model has.
                    severity = 1f,
                },
            };
        }

        // Where a car is at choreography time u. Linear in u on purpose (see Tempo), with a sine bow across
        // the line of travel — sideways to a car coming down the screen — so it swings into the pile rather
        // than running in on a ruler. The bow is zero at both ends, so start and end poses are exactly as
        // authored; a negative arcPx bows the other way.
        public static CarPose Evaluate(CarPlan plan, float u)
        {
            float p = plan.travel <= 0f ? 1f : Mathf.Clamp01((u - plan.delay) / plan.travel);
            Vector2 pos = Vector2.Lerp(plan.startPos, plan.endPos, p);

            Vector2 run = plan.endPos - plan.startPos;
            if (run.sqrMagnitude > 1e-6f)
            {
                Vector2 across = new Vector2(-run.y, run.x).normalized;
                pos += across * (plan.arcPx * Mathf.Sin(Mathf.PI * p));
            }

            return new CarPose
            {
                position = pos,
                // Plain Lerp, not LerpAngle: the extra turns baked into startRotation are the point, and
                // LerpAngle would take the short way round and throw them away.
                rotation = Mathf.Lerp(plan.startRotation, plan.endRotation, p),
                progress = p,
            };
        }

        // ------------------------------------------------------------------ bodies

        // Half the extent of a car along one screen axis at this rotation, in reference px. There is only one
        // car body in the shot, so this is a function of the angle alone.
        public static float HalfSpan(float rotationDeg, bool horizontal)
        {
            float rad = rotationDeg * Mathf.Deg2Rad;
            float along = CarLengthPx * 0.5f;
            float across = CarWidthPx * 0.5f;
            return horizontal
                ? along * Mathf.Abs(Mathf.Cos(rad)) + across * Mathf.Abs(Mathf.Sin(rad))
                : along * Mathf.Abs(Mathf.Sin(rad)) + across * Mathf.Abs(Mathf.Cos(rad));
        }

        // How fast a car is travelling at choreography time u, in reference px per unit of u — the derivative
        // of Evaluate. Zero before it enters and zero once it has landed, which is what makes a late arrival
        // hit a parked pile hard while the pile itself only ever nudges.
        public static Vector2 Velocity(CarPlan plan, float u)
        {
            if (plan.travel <= 0f) return Vector2.zero;

            float p = (u - plan.delay) / plan.travel;
            if (p <= 0f || p >= 1f) return Vector2.zero;

            Vector2 run = plan.endPos - plan.startPos;
            Vector2 v = run / plan.travel;
            if (run.sqrMagnitude > 1e-6f)
            {
                Vector2 across = new Vector2(-run.y, run.x).normalized;
                v += across * (plan.arcPx * Mathf.PI * Mathf.Cos(Mathf.PI * p) / plan.travel);
            }
            return v;
        }

        // Closing speed a full-tilt head-on hit runs to, in px per unit u. Contact severity is measured
        // against it, so anything arriving at entry speed dents as hard as the damage model allows.
        public const float ReferenceClosingSpeed = 250f;

        // How much a car gives way when something runs into it: a landed one is dead weight and slides, one
        // still coming in is under power and holds its line.
        static float Yield(CarPose pose)
        {
            return pose.progress >= 1f ? 1f : 0.35f;
        }

        // Is this car part of the crash yet? Cars are stacked above the top edge before they set off, so
        // without this they would shove each other around — and throw sparks — off screen before the sequence
        // has even started.
        static bool InPlay(CarPose pose)
        {
            return pose.progress > 0f && pose.position.y - HalfSpan(pose.rotation, horizontal: false) <= CanvasHeight;
        }

        // Projection radius of a car body onto an axis: half its length along the body plus half its width
        // across it, both measured on that axis.
        static float Radius(float rotationDeg, Vector2 axis)
        {
            float rad = rotationDeg * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            var across = new Vector2(-along.y, along.x);
            return CarLengthPx * 0.5f * Mathf.Abs(Vector2.Dot(along, axis))
                 + CarWidthPx * 0.5f * Mathf.Abs(Vector2.Dot(across, axis));
        }

        // Separating-axis test between two car bodies at any rotation. Two boxes are clear if and only if one
        // of their four body axes separates them, so a gap on any axis is a complete miss; when there is no
        // gap anywhere, the shallowest axis is the shortest way to push them off each other.
        public static bool Overlap(Vector2 centreA, float rotationA, Vector2 centreB, float rotationB,
                                   out Vector2 normal, out float depth)
        {
            normal = Vector2.right;
            depth = float.MaxValue;
            Vector2 between = centreB - centreA;

            for (int i = 0; i < 4; i++)
            {
                float rad = (i < 2 ? rotationA : rotationB) * Mathf.Deg2Rad;
                var axis = (i % 2 == 0)
                    ? new Vector2(Mathf.Cos(rad), Mathf.Sin(rad))
                    : new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));

                float reach = Vector2.Dot(between, axis);
                float gap = Radius(rotationA, axis) + Radius(rotationB, axis) - Mathf.Abs(reach);
                if (gap <= 0f) return false;

                if (gap < depth)
                {
                    depth = gap;
                    normal = reach < 0f ? -axis : axis;
                }
            }

            return true;
        }

        // Turn four cars flying through each other into four cars leaning on each other: every overlapping
        // pair is pushed apart along the axis that separates them soonest, several passes over the field so a
        // shove that creates a fresh overlap gets resolved too (a car pinned against the edge of the slot can
        // only be separated by moving the other one, which takes a few), and anything that has entered the
        // frame is kept inside the art slot. Contacts are reported from the first pass, before any pushing, so what
        // comes back is the real interpenetration — which is what the dent severity is measured from.
        //
        // Stateless on purpose: poses come out of Evaluate and are settled from scratch every frame, so the
        // pile stays a pure function of u and an EditMode test can ask what it looks like at any moment.
        public static void Settle(CarPlan[] plans, CarPose[] poses, float u, List<Contact> contacts,
                                  int passes = 10)
        {
            if (contacts != null) contacts.Clear();
            if (plans == null || poses == null) return;

            int n = Mathf.Min(plans.Length, poses.Length);
            passes = Mathf.Max(1, passes);

            for (int pass = 0; pass < passes; pass++)
            {
                for (int a = 0; a < n; a++)
                {
                    if (!InPlay(poses[a])) continue;

                    for (int b = a + 1; b < n; b++)
                    {
                        if (!InPlay(poses[b])) continue;

                        if (!Overlap(poses[a].position, poses[a].rotation,
                                     poses[b].position, poses[b].rotation,
                                     out Vector2 normal, out float depth))
                            continue;

                        if (pass == 0 && contacts != null)
                        {
                            float closing = Mathf.Abs(Vector2.Dot(Velocity(plans[b], u) - Velocity(plans[a], u),
                                                                  normal));
                            contacts.Add(new Contact
                            {
                                a = a, b = b,
                                // The two bodies are identical, so the midpoint of their centres is always
                                // inside the region they share — near enough for a flash and a dent.
                                pointPx = (poses[a].position + poses[b].position) * 0.5f,
                                normal = normal,
                                depthPx = depth,
                                severity = Mathf.Clamp(closing / ReferenceClosingSpeed, 0.25f, 1f),
                            });
                        }

                        // The crash pair are allowed to bury into each other (see MaxBitePx); everyone else
                        // is still held at a hard zero. Only the excess beyond the allowance is pushed out,
                        // so the hero can sink its nose into the door AND still shove the car down the road
                        // once it has sunk as far as it is allowed to.
                        float allowed = (IsInTheCrash(a) && IsInTheCrash(b)) ? Bite(u) : 0f;
                        float push = depth - allowed;
                        if (push <= 0f) continue;

                        float yieldA = Yield(poses[a]);
                        float yieldB = Yield(poses[b]);
                        float total = Mathf.Max(0.001f, yieldA + yieldB);

                        poses[a].position -= normal * (push * yieldA / total);
                        poses[b].position += normal * (push * yieldB / total);
                    }
                }

                for (int i = 0; i < n; i++)
                {
                    float halfW = HalfSpan(poses[i].rotation, horizontal: true);
                    float halfH = HalfSpan(poses[i].rotation, horizontal: false);
                    Vector2 at = poses[i].position;

                    // A car still entirely above the top edge is meant to be off screen; leave it there.
                    if (at.y - halfH > CanvasHeight) continue;

                    at.x = Mathf.Clamp(at.x, ColumnRightPx + halfW, CanvasWidth + 60f - halfW);
                    at.y = Mathf.Max(at.y, halfH);
                    if (poses[i].progress >= 1f) at.y = Mathf.Min(at.y, CanvasHeight - halfH);

                    poses[i].position = at;
                }
            }
        }

        // The whole field at choreography time u, already settled against itself. The one call the runtime
        // and the tests both go through, so what a test measures is what the screen is showing.
        public static CarPose[] Tableau(CarPlan[] plans, float u, List<Contact> contacts = null)
        {
            var poses = new CarPose[plans.Length];
            for (int i = 0; i < plans.Length; i++) poses[i] = Evaluate(plans[i], u);
            Settle(plans, poses, u, contacts);
            return poses;
        }

        // Where the smoke hangs before anything has actually been hit: the middle of the art slot, at about
        // the height the pile settles at.
        public static readonly Vector2 PileCentrePx = new Vector2(505f, 160f);
    }
}
