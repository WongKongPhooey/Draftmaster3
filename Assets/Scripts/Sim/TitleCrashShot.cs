using System.Collections.Generic;
using UnityEngine;

namespace Draftmaster.Sim
{
    // One staging of the title screen's crash, composed fresh from a seed.
    //
    // The shot used to be a single hand-solved arrangement: the same four cars, hitting at the same place, at
    // the same speed, every time the game opened. It was solved that way because the constraints are fussy —
    // nothing may cross the copy column, every car has to fly in from off the top edge and land exactly as the
    // clock stops, only the cars in the accident may touch, and the T-bone has to land square in a door rather
    // than clip a corner. Hand-solving one arrangement against all of that is easy. Randomising it is not.
    //
    // So this proposes rather than calculates: Draw() throws a whole shot together out of the seed, IsSound()
    // holds it up against every constraint the tableau has, and Compose() keeps drawing until one passes. A
    // seed that never finds one falls back to the solved shot, which is why that is kept. Everything is driven
    // off System.Random rather than UnityEngine.Random, so a seed is a shot: the same number always composes
    // the same crash, which is what makes any of it testable.
    //
    // What varies: how many cars are in it (two, three or four), which of them the accident is, where on the
    // screen it happens, how fast the cars arrive, how hard they hit — and therefore how far they bury into
    // each other and how deep the bodywork folds — what angle they come in at, and where along the struck
    // car's flank each one lands. Liveries are drawn separately, by TitleCrashScene, from the carset.
    //
    // The shape of an accident is always the same, because it is the shape that shows the damage model off:
    // ONE car that has already lost it and is sliding broadside, presenting a flank, and one to three cars
    // arriving nose-first and square into it at different points along that flank. Every impact is therefore
    // a striker against the same slider, which keeps the bookkeeping honest — and gives the slider two or
    // three completely different dents down one side when the field piles in.
    public struct Shot
    {
        public TitleCrash.CarPlan[] cars;
        public TitleCrash.ImpactPlan[] impacts;
        public bool[] inCrash;        // by car index
        public bool[] isSlider;       // by car index: had already lost it when the shot opened
        public int heroIndex;         // the player's car: front-most, and always the last one to arrive
        public float bitePx;          // how far the cars bury into each other by the freeze — the severity you see

        public int CarCount => cars != null ? cars.Length : 0;
        public bool IsInTheCrash(int index) => inCrash != null && index >= 0 && index < inCrash.Length && inCrash[index];
        public bool IsSlider(int index) => isSlider != null && index >= 0 && index < isSlider.Length && isSlider[index];

        public int CrashCount
        {
            get
            {
                int n = 0;
                if (inCrash != null) for (int i = 0; i < inCrash.Length; i++) if (inCrash[i]) n++;
                return n;
            }
        }

        // How far cars `a` and `b` are allowed to be inside each other at choreography time u.
        //
        // Only a pair the same impact joins gets any allowance at all, and only from the moment that impact
        // lands — so a car still on its way in never sinks into anything, and two cars that are merely racing
        // past each other are held at a hard zero as they always were.
        public float AllowedBite(int a, int b, float u)
        {
            if (impacts == null) return 0f;

            for (int i = 0; i < impacts.Length; i++)
            {
                bool joins = (impacts[i].striker == a && impacts[i].struck == b)
                          || (impacts[i].striker == b && impacts[i].struck == a);
                if (joins) return bitePx * TitleCrash.Crush(impacts[i], u);
            }
            return 0f;
        }
    }

    public static class TitleCrashComposer
    {
        // Attempts before a seed gives up and takes the solved shot. Generous: a draw is cheap and a fallback
        // is a shot somebody has already seen.
        const int Attempts = 32;

        // How coarsely the constraints are walked while composing. The tests walk it far more finely; this
        // only has to be fine enough that nothing sneaks between two samples, and it runs at boot.
        const int SoundnessSteps = 44;

        public static Shot Compose(int seed)
        {
            var rng = new System.Random(seed);

            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                var shot = Draw(rng);
                if (IsSound(shot)) return shot;
            }
            return Solved();
        }

        // ------------------------------------------------------------------ drawing one

        public static Shot Draw(System.Random rng)
        {
            // Two, three or four cars in it. Whatever is left over is racing past, clean, which is what makes
            // the wrecked ones read as wrecked — so a four-car accident deliberately has nobody watching.
            //
            // How they are arranged is forced by how much room there is. A broadside car presents 150px of
            // flank and a car arriving nose-on is 75px wide, so TWO will fit across it side by side and a
            // third will not: it would have to be drawn through one of the others to reach any metal. So a
            // four-car accident is two cars that have BOTH lost it, lying end to end, with one piling into
            // each; three is one slider with two into it; two is the plain T-bone.
            int crashCount = 2 + rng.Next(3);
            int racing = 4 - crashCount;
            int sliders = crashCount == 4 ? 2 : 1;
            int strikers = crashCount - sliders;

            var cars = new TitleCrash.CarPlan[4];
            var inCrash = new bool[4];
            var isSlider = new bool[4];

            // How hard the whole thing is, and the only number that decides how it LOOKS: the cars bury into
            // each other by this much, and each folds half of it (see BodyDeform.Share), so a bigger bite is
            // a bigger accident in both the overlap and the damage.
            float bitePx = Range(rng, 19f, 34f);

            // --- the cars that are only racing, running down the left of the slot in company. The lane is
            // kept well clear of the copy column on one side and of the accident on the other.
            float lane = Range(rng, 364f, 376f);
            float leadY = Range(rng, 78f, 104f);
            float gap = Range(rng, 150f, 196f);
            for (int i = 0; i < racing; i++)
            {
                float endY = leadY + (racing - 1 - i) * gap;
                cars[i] = new TitleCrash.CarPlan
                {
                    startPos = new Vector2(lane, endY + 630f),
                    endPos = new Vector2(lane, endY),
                    startRotation = 90f,
                    endRotation = 90f,
                    arcPx = 0f, delay = 0f, travel = 1f, depth = i,
                };
            }

            // --- the one or two that lost it. Broadside, slow, and in shot long before anything hits them.
            // A pair lie end to end along the same line, which is what a spun car and the one it collected
            // look like, and is also the only way two of them fit across the slot at once.
            float drift = Range(rng, -0.24f, 0.24f);                 // sideways per unit of downward travel
            Vector2 travel = new Vector2(drift, -1f).normalized;

            // Broadside is the body lying square across its own line of travel. The sweep is hung around it
            // rather than ending on it, so the car is still coming round as it slides and is never pointing
            // anywhere near where it is going.
            float broadside = Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg + 90f;
            if (rng.Next(2) == 0) broadside += 180f;                 // which end is the nose
            float sweep = Range(rng, 32f, 46f);
            float restRotation = broadside + sweep * 0.4f;

            float rad = restRotation * Mathf.Deg2Rad;
            Vector2 along = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            if (along.x < 0f) along = -along;                        // reads left to right across the slot
            Vector2 across = new Vector2(-along.y, along.x);
            if (across.y < 0f) across = -across;                     // the flank facing up the screen

            Vector2 pile = new Vector2(Range(rng, sliders > 1 ? 506f : 498f, sliders > 1 ? 522f : 540f),
                                       Range(rng, 142f, 172f));
            float apart = Range(rng, 158f, 176f);                    // end to end, and clear of each other

            var sliderEnds = new Vector2[sliders];
            for (int k = 0; k < sliders; k++)
            {
                int index = racing + k;
                inCrash[index] = true;
                isSlider[index] = true;

                Vector2 end = pile + along * (sliders > 1 ? (k - (sliders - 1) * 0.5f) * apart : 0f);
                sliderEnds[k] = end;

                cars[index] = new TitleCrash.CarPlan
                {
                    startPos = end - travel * Range(rng, 300f, 380f),   // short run = slow = there to be caught
                    endPos = end,
                    startRotation = broadside - sweep * 0.6f,
                    endRotation = restRotation,
                    arcPx = Range(rng, -16f, 16f),
                    delay = 0f, travel = 1f, depth = index,
                };
            }

            // --- the cars that pile into them, spread along the flank each one is presenting.
            var impacts = new TitleCrash.ImpactPlan[strikers];
            int perSlider = strikers / sliders;
            float spread = perSlider > 1 ? Range(rng, 84f, 94f) : 0f;

            for (int k = 0; k < strikers; k++)
            {
                int index = racing + sliders + k;
                inCrash[index] = true;

                int target = k / perSlider;
                int nth = k % perSlider;

                // Where along the flank this one lands. On its own it goes near the middle of the door; two
                // abreast fan out either side of that, far enough apart not to arrive on top of each other.
                float centred = perSlider > 1 ? (nth - (perSlider - 1) * 0.5f) : 0f;
                float at = centred * spread + Range(rng, -6f, 6f);
                Vector2 point = sliderEnds[target] + along * at + across * (TitleCrash.CarWidthPx * 0.5f);

                // Coming in square to the flank, give or take — that squareness is what makes it a T-bone
                // rather than a sideswipe, and the jitter is what stops a pair of them looking like a comb.
                float lean = Range(rng, -9f, 9f) + centred * Range(rng, 0f, 6f);
                Vector2 heading = Rotate(-across, lean);

                // Authored to finish past the bite allowance, so Settle still has something to push with and
                // the struck car is shunted down the road rather than the pair just parking together.
                float drive = bitePx + Range(rng, 5f, 17f);
                Vector2 end = point + heading * (drive - TitleCrash.CarLengthPx * 0.5f);

                // How far back it starts is SOLVED, not drawn. Every car lands at u = 1, so how far a striker
                // had to come is the only thing deciding when it arrives — and it has to arrive inside the
                // crawl, which is the last six percent of the choreography. Drawing a run and hoping put the
                // contact outside that window on essentially every attempt, so every seed fell back to the
                // hand-solved shot and the title screen was not randomised at all. A moment is drawn instead,
                // and the run that produces it is found by halving.
                float arc = Range(rng, -22f, 22f);
                float wantU = Range(rng, 0.948f, 0.974f);
                float run = SolveRun(cars[racing + target], end, heading, arc, wantU);

                cars[index] = new TitleCrash.CarPlan
                {
                    startPos = end - heading * run,
                    endPos = end,
                    startRotation = SpriteAngle(heading),
                    endRotation = SpriteAngle(heading),
                    arcPx = arc,
                    delay = 0.06f, travel = 0.94f, depth = index,
                };

                impacts[k] = new TitleCrash.ImpactPlan
                {
                    striker = index,
                    struck = racing + target,
                    // striker -> struck, which is the way it is DRIVING: the sparks spray along it and the
                    // press folds along it, so inverting it dents the wrong side of both cars.
                    normal = heading,
                    severity = 1f,
                    atU = FirstTouchU(cars[racing + target], cars[index]),
                    throughU = TitleCrash.CrushEndU,
                };
            }

            var shot = new Shot
            {
                cars = cars,
                impacts = impacts,
                inCrash = inCrash,
                isSlider = isSlider,
                heroIndex = 3,
                bitePx = bitePx,
            };

            return shot;
        }

        // The run that makes a striker arrive at `wantU`, found by halving.
        //
        // Contact time rises with the run: everything in the field lands at u = 1, so a car with further to
        // come is further away at every moment before that, and therefore later to arrive. Which makes it
        // solvable rather than a thing to draw and hope for. The floor is high enough that a striker is always
        // travelling well over twice the speed of the car it hits, which is where the severity comes from.
        static float SolveRun(in TitleCrash.CarPlan slider, Vector2 end, Vector2 heading, float arc, float wantU)
        {
            float lo = 640f, hi = 1500f;

            for (int step = 0; step < 15; step++)
            {
                float mid = (lo + hi) * 0.5f;
                var probe = new TitleCrash.CarPlan
                {
                    startPos = end - heading * mid,
                    endPos = end,
                    startRotation = SpriteAngle(heading),
                    endRotation = SpriteAngle(heading),
                    arcPx = arc,
                    delay = 0.06f, travel = 0.94f, depth = 3,
                };

                if (FirstTouchU(slider, probe) < wantU) lo = mid; else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        // When two cars first touch, on the raw choreography. No settling: whether and when two undeformed
        // bodies meet is a fact about where they were sent, and nothing to do with how far they are later
        // allowed to sink into each other. Returns 1 for a pair that never meet at all.
        static float FirstTouchU(in TitleCrash.CarPlan a, in TitleCrash.CarPlan b)
        {
            if (TitleCrash.Gap(TitleCrash.Evaluate(a, 1f), TitleCrash.Evaluate(b, 1f)) > 0f) return 1f;

            float lo = 0.4f, hi = 1f;
            for (int step = 0; step < 15; step++)
            {
                float mid = (lo + hi) * 0.5f;
                if (TitleCrash.Gap(TitleCrash.Evaluate(a, mid), TitleCrash.Evaluate(b, mid)) <= 0f) hi = mid;
                else lo = mid;
            }
            return hi;
        }

        // ------------------------------------------------------------------ is it a shot at all

        // Every constraint the tableau has, in one place, so a randomised shot is held to exactly what the
        // hand-solved one was. A draw that fails any of these is thrown away rather than shipped.
        public static bool IsSound(in Shot shot) => IsSound(shot, out _);

        // Same, but says what it objected to. A composer that quietly rejects every draw it makes still
        // produces a title screen — the fallback one, every time — so being able to ask why is the
        // difference between tuning the ranges and guessing at them.
        public static bool IsSound(in Shot shot, out string why)
        {
            why = null;
            if (shot.cars == null || shot.cars.Length != 4 || shot.impacts == null || shot.impacts.Length < 1)
                return No(out why, "malformed");
            if (!shot.IsInTheCrash(shot.heroIndex) || shot.heroIndex != 3) return No(out why, "hero not in the crash");

            for (int i = 0; i < shot.cars.Length; i++)
            {
                var plan = shot.cars[i];

                // Flies in from off the top edge, runs down the screen, and lands exactly as the clock stops.
                if (plan.startPos.y - TitleCrash.HalfSpan(plan.startRotation, horizontal: false) <= TitleCrash.CanvasHeight)
                    return No(out why, $"car {i} starts on screen");
                if (plan.endPos.y >= plan.startPos.y - 100f) return No(out why, $"car {i} barely moves");
                if (Mathf.Abs(plan.endPos.y - plan.startPos.y) <= Mathf.Abs(plan.endPos.x - plan.startPos.x)) return No(out why, $"car {i} more sideways than down");
                if (Mathf.Abs(plan.delay + plan.travel - 1f) > 1e-3f) return No(out why, $"car {i} lands off the clock");
                if (TitleCrash.Evaluate(plan, 1f - TitleCrash.Tempo.Default.Share).progress >= 1f) return No(out why, $"car {i} parks before the crawl");

                // Pointing where it is going, unless it is the one that has already lost it — which has to be
                // properly across its own line, or there is no flank presented and no T-bone to be had.
                Vector2 line = (plan.endPos - plan.startPos).normalized;
                float entry = Vector2.Dot(TitleCrash.Heading(plan.startRotation), line);
                float rest = Vector2.Dot(TitleCrash.Heading(plan.endRotation), line);

                if (shot.IsSlider(i))
                {
                    if (Mathf.Abs(entry) >= 0.6f || Mathf.Abs(rest) >= 0.35f) return No(out why, $"slider {i} not broadside");
                    if (Mathf.Abs(plan.endRotation - plan.startRotation) <= 30f) return No(out why, $"slider {i} barely rotates");
                }
                else if (entry <= 0.9f || rest <= 0.9f) return No(out why, $"car {i} not nose-first");
            }

            // Every impact lands inside the slow-motion beat, square into the flank, on the door rather than
            // a corner — and with the two bodies actually together when it fires.
            float share = TitleCrash.Tempo.Default.Share;
            for (int i = 0; i < shot.impacts.Length; i++)
            {
                var hit = shot.impacts[i];
                if (hit.atU <= 1f - share || hit.atU >= 1f - share * 0.25f) return No(out why, $"impact {i} lands at u={hit.atU:0.000}, outside the crawl");
                if (!shot.IsSlider(hit.struck) || shot.IsSlider(hit.striker)) return No(out why, $"impact {i} is not a striker into a slider");

                var poses = TitleCrash.Tableau(shot, hit.atU);
                if (TitleCrash.Gap(poses[hit.striker], poses[hit.struck]) >= 5f) return No(out why, $"impact {i} fires in mid-air");

                Vector2 heading = TitleCrash.Heading(poses[hit.striker].rotation);
                float rad = poses[hit.struck].rotation * Mathf.Deg2Rad;
                Vector2 flank = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                if (Mathf.Abs(Vector2.Dot(heading, flank)) >= 0.35f) return No(out why, $"impact {i} is a sideswipe, not a T-bone");

                // On the body rather than off the end of it. A car on its own goes into the door; when two
                // arrive abreast the outer one lands on a quarter panel, which is where it should land.
                Vector2 nose = poses[hit.striker].position + heading * (TitleCrash.CarLengthPx * 0.5f);
                if (Mathf.Abs(Vector2.Dot(nose - poses[hit.struck].position, flank)) >= TitleCrash.CarLengthPx * 0.4f)
                    return No(out why, $"impact {i} clips a corner rather than the flank");

                if (Vector2.Dot(hit.normal.normalized,
                                (poses[hit.struck].position - poses[hit.striker].position).normalized) <= 0f)
                    return No(out why, $"impact {i} sprays backwards");
            }

            // And the whole thing walked: nothing over the copy column, nobody inside anybody they are not in
            // an accident with, and nobody buried past what their own impact allows.
            for (int step = 0; step <= SoundnessSteps; step++)
            {
                float u = step / (float)SoundnessSteps;
                var poses = TitleCrash.Tableau(shot, u);

                for (int i = 0; i < poses.Length; i++)
                {
                    if (TitleCrash.OffTheTop(poses[i])) continue;
                    if (poses[i].position.x - TitleCrash.HalfSpan(poses[i].rotation, horizontal: true)
                        < TitleCrash.ColumnRightPx) return No(out why, $"car {i} crosses the copy column at u={u:0.00}");
                }

                for (int a = 0; a < poses.Length; a++)
                {
                    for (int b = a + 1; b < poses.Length; b++)
                    {
                        if (TitleCrash.OffTheTop(poses[a]) || TitleCrash.OffTheTop(poses[b])) continue;

                        bool through = TitleCrash.Overlap(poses[a].position, poses[a].rotation,
                                                          poses[b].position, poses[b].rotation, out _, out float depth);
                        if (!through) continue;
                        if (depth > shot.AllowedBite(a, b, u) + 1f)
                            return No(out why, $"cars {a},{b} {depth:0} inside each other at u={u:0.00}");
                    }
                }
            }

            // Where everything comes to rest.
            var final = TitleCrash.Tableau(shot, 1f);
            for (int i = 0; i < final.Length; i++)
            {
                float halfW = TitleCrash.HalfSpan(final[i].rotation, horizontal: true);
                float halfH = TitleCrash.HalfSpan(final[i].rotation, horizontal: false);

                if (final[i].position.x - halfW < TitleCrash.ColumnRightPx - 0.01f) return No(out why, $"car {i} rests over the column");
                if (final[i].position.x + halfW > TitleCrash.CanvasWidth + 60f) return No(out why, $"car {i} rests off the right edge");
                if (final[i].position.y - halfH < -0.01f) return No(out why, $"car {i} rests below the slot");
                if (final[i].position.y + halfH > TitleCrash.CanvasHeight + 0.01f) return No(out why, $"car {i} rests above the slot");
                if (Vector2.Distance(final[i].position, shot.cars[i].endPos) >= 60f) return No(out why, $"car {i} shoved off its pose");
            }

            // The accident has to end up as an accident, and everybody else has to have stayed out of it.
            for (int i = 0; i < shot.impacts.Length; i++)
                if (TitleCrash.Gap(final[shot.impacts[i].striker], final[shot.impacts[i].struck]) >= 1f)
                    return No(out why, $"impact {i} finishes apart");

            for (int a = 0; a < final.Length; a++)
                for (int b = a + 1; b < final.Length; b++)
                    if (shot.AllowedBite(a, b, 1f) <= 0f && TitleCrash.Gap(final[a], final[b]) <= 5f)
                        return No(out why, $"cars {a},{b} rest too close and are not in an accident together");

            return true;
        }

        static bool No(out string why, string reason)
        {
            why = reason;
            return false;
        }

        // ------------------------------------------------------------------ the one that always works

        // The hand-solved shot the randomiser replaced, kept as the fallback for a seed that never finds a
        // sound draw of its own. Sound by construction — it is the arrangement every constraint above was
        // written against in the first place.
        public static Shot Solved()
        {
            var cars = new[]
            {
                new TitleCrash.CarPlan
                {
                    startPos = new Vector2(372f, 882f), endPos = new Vector2(372f, 252f),
                    startRotation = 90f,                endRotation = 90f,
                    arcPx = 0f, delay = 0f, travel = 1f, depth = 0,
                },
                new TitleCrash.CarPlan
                {
                    startPos = new Vector2(372f, 720f), endPos = new Vector2(372f, 90f),
                    startRotation = 90f,                endRotation = 90f,
                    arcPx = 0f, delay = 0f, travel = 1f, depth = 1,
                },
                new TitleCrash.CarPlan
                {
                    startPos = new Vector2(466f, 470f), endPos = new Vector2(516f, 150f),
                    startRotation = -14f,               endRotation = 18f,
                    arcPx = 14f, delay = 0f, travel = 1f, depth = 2,
                },
                new TitleCrash.CarPlan
                {
                    startPos = new Vector2(496f, 1000f), endPos = new Vector2(500f, 238f),
                    startRotation = 96f,                endRotation = 96f,
                    arcPx = -22f, delay = 0.06f, travel = 0.94f, depth = 3,
                },
            };

            var shot = new Shot
            {
                cars = cars,
                inCrash = new[] { false, false, true, true },
                isSlider = new[] { false, false, true, false },
                heroIndex = 3,
                bitePx = 26f,
                impacts = new[]
                {
                    new TitleCrash.ImpactPlan
                    {
                        striker = 3, struck = 2,
                        atU = TitleCrash.ImpactU, throughU = TitleCrash.CrushEndU,
                        normal = new Vector2(0.171f, -0.985f),
                        severity = 1f,
                    },
                },
            };

            shot.impacts[0].atU = FirstTouchU(cars[2], cars[3]);
            return shot;
        }

        // ------------------------------------------------------------------ small things

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // The sprite angle a car pointing along `heading` is drawn at. Liveries are nose-left, so it is the
        // heading turned through 180 — the inverse of TitleCrash.Heading.
        static float SpriteAngle(Vector2 heading)
        {
            return Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg + 180f;
        }
    }
}
