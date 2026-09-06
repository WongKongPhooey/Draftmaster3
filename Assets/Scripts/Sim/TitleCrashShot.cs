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
    // What varies: how many cars go past before any of it happens and in which lanes, how many cars are in
    // the accident (two, three or four), which of them it is, where on the screen it happens, how fast the
    // cars arrive, how hard they hit — and therefore how far they bury into each other and how deep the
    // bodywork folds — which way the wrecked car is spinning and how far past square it lies when everything
    // stops, what angle the strikers come in at, and where along the struck car's flank each one lands: the
    // door, a quarter panel, or right up by the wheel. Liveries are drawn separately, by TitleCrashScene.
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

        // The field going past before any of it happens. Not part of the tableau: these cross the frame on
        // the lead-in beat's own clock and are gone before the first car of the accident is due, so nothing
        // here is settled against anything, dented, or in the frozen picture at the end.
        public TitleCrash.PassPlan[] traffic;

        public int CarCount => cars != null ? cars.Length : 0;
        public int TrafficCount => traffic != null ? traffic.Length : 0;
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
            float drift = Range(rng, -0.22f, 0.22f);                 // sideways per unit of downward travel
            Vector2 travel = new Vector2(drift, -1f).normalized;

            // Broadside is the body lying square across its own line of travel. The turn is hung around it
            // rather than ending on it, so the car is still coming round as it slides and is never pointing
            // anywhere near where it is going.
            float broadside = Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg + 90f;
            if (rng.Next(2) == 0) broadside += 180f;                 // which end is the nose

            // Which WAY it is spinning, and how far past square it has got by the time everything stops.
            //
            // Both of these used to be constants — every slider turned the same way and came to rest the
            // same fraction of the same sweep past broadside — so every shot froze with the wrecked car
            // lying at very nearly the same angle whatever else the seed changed. The two halves of the
            // turn are drawn separately now: `restOff` is how far round it still is at the freeze, `entryOff`
            // is how far the other side of broadside it arrived at, and the turn it makes on the way in is
            // whatever joins them. The floor on entryOff keeps that turn past the point where a slider reads
            // as parked at an angle rather than as a car that has lost it.
            float spin = rng.Next(2) == 0 ? 1f : -1f;
            float restOff = Range(rng, 4f, 24f);
            float entryOff = Range(rng, Mathf.Max(16f, 33f - restOff), 33f);

            float restRotation = broadside + spin * restOff;
            float startRotation = broadside - spin * entryOff;

            float rad = restRotation * Mathf.Deg2Rad;
            Vector2 along = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            if (along.x < 0f) along = -along;                        // reads left to right across the slot
            Vector2 across = new Vector2(-along.y, along.x);
            if (across.y < 0f) across = -across;                     // the flank facing up the screen

            Vector2 pile = new Vector2(Range(rng, sliders > 1 ? 498f : 474f, sliders > 1 ? 534f : 552f),
                                       Range(rng, 128f, 190f));
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
                    startRotation = startRotation,
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
                // A lone striker used to land within six pixels of the middle of a hundred-and-fifty pixel
                // car, every time, which is the same hit at the same place in every shot the composer could
                // ever draw. It picks a spot down the whole flank now — door, quarter panel, or right up by
                // the wheel — and a draw that ends up clipping the corner rather than biting into the body
                // is thrown out by IsSound rather than prevented from ever happening.
                float centred = perSlider > 1 ? (nth - (perSlider - 1) * 0.5f) : 0f;
                float at = perSlider > 1
                    ? centred * spread + Range(rng, -8f, 8f)
                    : Range(rng, -42f, 42f);
                Vector2 point = sliderEnds[target] + along * at + across * (TitleCrash.CarWidthPx * 0.5f);

                // Coming in square to the flank, give or take — that squareness is what makes it a T-bone
                // rather than a sideswipe, and the jitter is what stops a pair of them looking like a comb.
                float lean = Range(rng, -12f, 12f) + centred * Range(rng, 0f, 6f);
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
                traffic = DrawTraffic(rng),
            };

            return shot;
        }

        // ------------------------------------------------------------------ the field going past first

        // The cars that are only passing through: six to ten of them, three abreast, crossing the slot at
        // racing speed and clear of it before the first car of the accident is due.
        //
        // They are drawn against the lead-in beat's own 0..1 clock rather than choreography time, because
        // they are the one beat that is NOT slowed down — the whole reason they are there is to be the speed
        // the crash is about to be slow against. Lanes are dealt round-robin, so two cars in the same lane
        // are always three stagger steps apart and a pack can never run into its own back marker; cars in
        // different lanes are free to go past side by side, which is what makes it a pack.
        //
        // It is ONE pack, not a queue. The stagger used to be spread across the whole beat, which put a
        // single car in the slot at a time and read as light traffic rather than as a field — so the gap is
        // authored in PIXELS now: `PackGapPx` is how far apart two cars in the same lane are nose to tail,
        // and the stagger is whatever fraction of the beat covers that distance at the pace the pack is
        // running. Everything runs at one pace for the same reason: a lone car six per cent quicker than the
        // one in front of it closes the whole gap in half a screen, and nothing here is allowed to touch.
        //
        // The pack is hung off the END of the beat rather than the start, so the last car of it leaves the
        // bottom of the frame at the same moment it always did and the crash still follows straight on its
        // heels. What used to be a car every third of a beat is now an empty slot, then the field.
        static TitleCrash.PassPlan[] DrawTraffic(System.Random rng)
        {
            int n = 6 + rng.Next(5);
            const int Lanes = 3;

            // How long one car takes to cross, as a fraction of the beat — one pace for the whole pack.
            float span = Range(rng, 0.20f, 0.28f);

            // Nose-to-tail distance between two cars in the same lane, and the stagger that produces it. A
            // car crosses `Run` px in `span` of the beat, so `Lanes` stagger steps have to cover the gap.
            const float Run = TitleCrash.CanvasHeight + TitleCrash.PassMarginPx * 2f;
            float gapPx = Range(rng, 205f, 245f);
            float step = gapPx * span / (Run * Lanes);

            // How ragged the ranks are. A quarter of a step either way is enough that the pack isn't drawn
            // on graph paper and small enough that it can never close a same-lane gap to a contact.
            float wobble = step * 0.25f;

            float laneLeft = TitleCrash.ColumnRightPx + TitleCrash.CarWidthPx * 0.5f + 20f;
            float laneRight = TitleCrash.CanvasWidth - TitleCrash.CarWidthPx * 0.5f - 20f;

            // Not on rails: the pack moves across the slot on the way down, which is what stops a field going
            // past reading as columns scrolling. One weave for all of them — packed this tightly, cars in
            // neighbouring lanes are alongside each other, and weaving individually would put them into each
            // other's doors.
            float weave = Range(rng, -10f, 10f);

            // The back marker sets off here; everything else is dealt forwards from it.
            float tail = 1f - span - wobble;

            var passes = new TitleCrash.PassPlan[n];
            for (int i = 0; i < n; i++)
            {
                int lane = i % Lanes;
                float x = Mathf.Lerp(laneLeft, laneRight, lane / (float)(Lanes - 1)) + Range(rng, -3f, 3f);

                var start = new Vector2(x, TitleCrash.CanvasHeight + TitleCrash.PassMarginPx);
                var end = new Vector2(x + weave, -TitleCrash.PassMarginPx);

                passes[i] = new TitleCrash.PassPlan
                {
                    startPos = start,
                    endPos = end,
                    rotation = SpriteAngle((end - start).normalized),
                    atLead = Mathf.Clamp(tail - (n - 1 - i) * step + Range(rng, -wobble, wobble), 0f, 1f - span),
                    lead = span,
                    depth = i,
                };
            }
            return passes;
        }

        // One hand-placed pass, for the solved shot: in at the top of the slot, out at the bottom.
        static TitleCrash.PassPlan Pass(float fromX, float toX, float atLead, float lead, int depth)
        {
            var start = new Vector2(fromX, TitleCrash.CanvasHeight + TitleCrash.PassMarginPx);
            var end = new Vector2(toX, -TitleCrash.PassMarginPx);
            return new TitleCrash.PassPlan
            {
                startPos = start,
                endPos = end,
                rotation = SpriteAngle((end - start).normalized),
                atLead = atLead, lead = lead, depth = depth,
            };
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
                    // Broadside enough that there is a flank to hit, and no more than that. The rest
                    // limit is what decides how much the wrecked car's angle is allowed to vary between
                    // shots, so it is as loose as the T-bone will bear: 0.5 is thirty degrees off square,
                    // and a striker still arrives inside twenty degrees of the flank's normal at that.
                    if (Mathf.Abs(entry) >= 0.6f || Mathf.Abs(rest) >= 0.5f) return No(out why, $"slider {i} not broadside");
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

            // The field going past first: fully out of frame at both ends of its run, never over the copy
            // column or off the right edge, gone before the accident is due — and never into each other,
            // because they are racing rather than crashing.
            if (shot.traffic == null || shot.traffic.Length < 2) return No(out why, "nothing goes past before the crash");

            for (int i = 0; i < shot.traffic.Length; i++)
            {
                var pass = shot.traffic[i];
                float halfW = TitleCrash.HalfSpan(pass.rotation, horizontal: true);
                float halfH = TitleCrash.HalfSpan(pass.rotation, horizontal: false);

                if (pass.lead <= 0f) return No(out why, $"pass {i} crosses in no time at all");
                if (pass.atLead < 0f || pass.atLead + pass.lead > 1f + 1e-4f)
                    return No(out why, $"pass {i} is still in frame when the crash starts");
                if (pass.startPos.y - halfH <= TitleCrash.CanvasHeight) return No(out why, $"pass {i} starts on screen");
                if (pass.endPos.y + halfH >= 0f) return No(out why, $"pass {i} stops on screen");
                if (Mathf.Abs(pass.endPos.y - pass.startPos.y) <= Mathf.Abs(pass.endPos.x - pass.startPos.x))
                    return No(out why, $"pass {i} more sideways than down");
                if (Vector2.Dot(TitleCrash.Heading(pass.rotation), (pass.endPos - pass.startPos).normalized) <= 0.9f)
                    return No(out why, $"pass {i} goes past backwards");
                if (Mathf.Min(pass.startPos.x, pass.endPos.x) - halfW < TitleCrash.ColumnRightPx)
                    return No(out why, $"pass {i} crosses the copy column");
                if (Mathf.Max(pass.startPos.x, pass.endPos.x) + halfW > TitleCrash.CanvasWidth)
                    return No(out why, $"pass {i} runs off the right edge");
            }

            for (int step = 0; step <= SoundnessSteps; step++)
            {
                float lead = step / (float)SoundnessSteps;
                for (int a = 0; a < shot.traffic.Length; a++)
                {
                    var poseA = TitleCrash.PassAt(shot.traffic[a], lead);
                    if (!poseA.inFlight) continue;

                    for (int b = a + 1; b < shot.traffic.Length; b++)
                    {
                        var poseB = TitleCrash.PassAt(shot.traffic[b], lead);
                        if (!poseB.inFlight) continue;

                        if (TitleCrash.Gap(AsCar(poseA), AsCar(poseB)) < 10f)
                            return No(out why, $"passing cars {a},{b} are on top of each other at lead={lead:0.00}");
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

        // A passing car measured as a body, so the same separating-axis maths the pile is held to can say
        // whether two of them are running into each other.
        static TitleCrash.CarPose AsCar(TitleCrash.PassPose pose)
        {
            return new TitleCrash.CarPose { position = pose.position, rotation = pose.rotation, progress = 1f };
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
                // Eight cars, three abreast, the same tight pack DrawTraffic deals: 0.03 of the beat between
                // one car and the next, so the three in a lane are 225px nose to tail, and the back marker
                // still leaves the frame as the beat ends.
                traffic = new[]
                {
                    Pass(384f, 392f, 0.55f, 0.24f, 0),
                    Pass(483f, 491f, 0.58f, 0.24f, 1),
                    Pass(582f, 590f, 0.61f, 0.24f, 2),
                    Pass(384f, 392f, 0.64f, 0.24f, 3),
                    Pass(483f, 491f, 0.67f, 0.24f, 4),
                    Pass(582f, 590f, 0.70f, 0.24f, 5),
                    Pass(384f, 392f, 0.73f, 0.24f, 6),
                    Pass(483f, 491f, 0.76f, 0.24f, 7),
                },
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
