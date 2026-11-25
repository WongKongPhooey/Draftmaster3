using UnityEngine;

namespace Draftmaster.Sim
{
    // Engine temperature: a car tucked onto the bumper of the car ahead has its grille in that car's wake, the
    // radiator stops getting air, and the engine heats. It is what stops a superspeedway pack bump-drafting lap
    // after lap: about two laps nose to tail at pack speed and the engine is overheating and losing power.
    //
    // It is a rate model, not a settling one - the gap decides whether the temperature is going up, holding or
    // coming down:
    //
    //   * Bumper to bumper (a push) heats it at the full rate.
    //   * About three quarters of a car length back (HoldCarLengths) it holds whatever temperature it has.
    //   * Further back it cools, quicker the more clean air there is in front of the grille; an empty lane
    //     (nothing ahead within CleanCarLengths, or the car ahead a lane over) cools it quickest.
    //
    // Both ends scale with speed. The heat goes with the work the engine does, so with v² (SpeedShare), and a
    // slow car is cooled by the engine not working: under caution everything comes down, nose to tail or not.
    //
    // Overheated (past OverheatC) the engine loses power and some top speed, which drops the car off the bumper,
    // which cools it - pushing regulates itself. PlayerVehicleController runs it for every dynamic car, player and
    // AI alike; AIRacingBehaviour opens its pack gap when its engine runs hot.
    //
    // On top of the wake, the engine's own work warms it: an engine idling in the box sits at NormalC, and flat out
    // in clean air it settles up to LoadRiseC above that (Settle), which never reaches HotC - only someone's wake, or
    // a heavily taped grille, takes it into the amber. Pinned at MaxC with the throttle down it is living on borrowed
    // time: BlowSeconds of full throttle there and it lets go (StepStrain / Blown).
    public static class EngineTemp
    {
        public const float CarLength = 4.8f;          // metres, nose to tail (AIRacingBehaviour.packFollowGap's car)
        public const float HoldCarLengths = 0.75f;    // bumper gap at which the temperature holds
        public const float CleanCarLengths = 3f;      // bumper gap at which the grille is in clean air
        public const float HoldGap = CarLength * HoldCarLengths;     // 3.6 m bumper to bumper
        public const float CleanGap = CarLength * CleanCarLengths;   // 14.4 m bumper to bumper
        // Lateral offset from the car ahead's line within which its wake covers the whole grille (a line of cars
        // wanders a little either side of it), and past which it misses it (DraftAero.TowLateralHalfWidth).
        public const float FullCoverLateral = 1f;
        public const float LateralHalfWidth = 2.5f;

        public const float NormalC = 95f;     // running temperature in clean air; never cools below it
        public const float HotC = 118f;       // the gauge goes amber: time to back off
        public const float CooledC = 105f;    // back to normal running after being hot (AI hysteresis)
        public const float OverheatC = 125f;  // power starts going
        public const float MaxC = 135f;       // full overheat loss; the temperature stops here

        // °C/s at full pace: bumper to bumper (the full push) and the empty lane. 30 °C from normal to overheating
        // nose to tail is ~90 s - two laps of Daytona at pack speed; an empty lane takes it back down in a minute.
        public const float HeatRate = 0.33f;
        public const float CoolRate = 0.5f;

        // Speed (m/s, ~168 mph) at which the engine is working flat out for heat; off a plate track. On a plate
        // track it is the plate speed, where the pack races.
        public const float ReferenceMps = 75f;

        // The engine's own work: flat out in clean air it settles LoadRiseC above NormalC, approached with a time
        // constant of SettleSeconds. Load = throttle x SpeedShare, so idling (or creeping down pit road) is NormalC.
        public const float LoadRiseC = 12f;
        public const float SettleSeconds = 25f;

        // Seconds of full throttle at MaxC before the engine blows. Part throttle wears it proportionally; once the
        // temperature is back under OverheatC the strain bleeds away at StrainRecoverRate of real time.
        public const float BlowSeconds = 10f;
        public const float StrainRecoverRate = 0.5f;

        public const float MaxPowerLoss = 0.15f;     // share of engine pull gone at MaxC
        public const float MaxTopSpeedLoss = 0.03f;  // share of top speed gone at MaxC (~6 mph at Daytona)

        // 0..1: how much of the work an engine at this speed is doing, against the reference speed.
        public static float SpeedShare(float speedMps, float referenceMps)
        {
            if (referenceMps <= 0f || speedMps <= 0f) return 0f;
            float q = speedMps / referenceMps;
            return Mathf.Clamp01(q * q);
        }

        // How much of our grille one car ahead covers (0..1), from the centre-to-centre gap along the track (+ = it
        // is ahead) and the lateral offset between us. 1 = bumper to bumper on its line, 0.5 = the hold distance,
        // 0 = clean air (CleanGap back, a lane over, or behind us).
        public const float HoldCover = 0.5f;
        public static float Cover(float centreGap, float lateralDelta)
        {
            if (centreGap <= 0f) return 0f;
            float lat = 1f - Mathf.Clamp01((Mathf.Abs(lateralDelta) - FullCoverLateral) / (LateralHalfWidth - FullCoverLateral));
            if (lat <= 0f) return 0f;
            float gap = Mathf.Max(0f, centreGap - CarLength);   // bumper to bumper
            float c;
            if (gap <= HoldGap) c = 1f - (1f - HoldCover) * gap / HoldGap;
            else c = HoldCover * (1f - Mathf.Clamp01((gap - HoldGap) / (CleanGap - HoldGap)));
            return c * lat;
        }

        // °C per second for a grille this covered (the most any car ahead covers it) at this speed share, with this
        // much grille tape on (GrilleTape; the default half-taped grille is the model as described above).
        public static float Rate(float cover, float speedShare, float tape = GrilleTape.Default)
        {
            cover = Mathf.Clamp01(cover);
            speedShare = Mathf.Clamp01(speedShare);
            float hold = GrilleTape.HoldCover(tape);
            float heat = 0f, clean = 0f;
            if (cover > hold) heat = (cover - hold) / (1f - hold);
            else clean = (hold - cover) / hold;   // cooling goes with the share of the grille in clean air
            // Heat goes with the work; a slow engine cools whatever is in front of it.
            float cool = Mathf.Clamp01(clean + (1f - speedShare));
            return HeatRate * GrilleTape.HeatScale(tape) * speedShare * heat - CoolRate * cool;
        }

        // Where the engine's own work settles it in clean air: NormalC idling, NormalC + LoadRiseC flat out at speed.
        public static float Settle(float load) => NormalC + LoadRiseC * Mathf.Clamp01(load);

        // One step of the engine's temperature, held between NormalC and MaxC. `load` (throttle x SpeedShare, 0..1)
        // warms it toward Settle(load); the wake heats it past that, and clean air cools it back down - never below
        // what the engine's work holds it at. Load 0 is the wake-only model.
        public static float Step(float tempC, float cover, float speedShare, float dt, float tape = GrilleTape.Default,
                                 float load = 0f)
        {
            float settle = Settle(load);
            float next = tempC + Rate(cover, speedShare, tape) * dt;
            float floor = tempC <= settle ? tempC + (settle - tempC) * Mathf.Clamp01(dt / SettleSeconds) : settle;
            return Mathf.Clamp(Mathf.Max(next, floor), NormalC, MaxC);
        }

        // Wear on an engine pinned at MaxC: grows with the throttle while it is there, bleeds away once the
        // temperature is back under OverheatC, holds in between.
        public static float StepStrain(float strain, float tempC, float throttle, float dt)
        {
            if (tempC >= MaxC - 0.01f) return strain + Mathf.Clamp01(throttle) * dt;
            if (tempC < OverheatC) return Mathf.Max(0f, strain - StrainRecoverRate * dt);
            return strain;
        }

        public static bool Blown(float strain) => strain >= BlowSeconds;

        // 0 at or under OverheatC, 1 at MaxC.
        public static float Overheat01(float tempC) => Mathf.Clamp01((tempC - OverheatC) / (MaxC - OverheatC));

        public static float PowerFactor(float tempC) => 1f - MaxPowerLoss * Overheat01(tempC);
        public static float TopSpeedFactor(float tempC) => 1f - MaxTopSpeedLoss * Overheat01(tempC);

        // The gauge: GaugeIdle (three of its ten bars) at normal running - an engine idling in the box is already
        // warm - and full at MaxC, the temperature where the clock to a blow-up starts.
        public const float GaugeIdle = 0.3f;
        public static float Gauge01(float tempC) =>
            Mathf.Clamp01(GaugeIdle + (1f - GaugeIdle) * (tempC - NormalC) / (MaxC - NormalC));
    }

    // Grille tape, the setup sheet's Tape slider (CarSetup.tape): how much of the radiator opening is taped over.
    // More tape is less drag and a little more straight-line speed, but less air through the radiator, so the engine
    // heats sooner and quicker in someone's wake (EngineTemp). In clean air the radiator still gets enough.
    //
    //   *   0%  no tape: the engine never overheats, but the open grille costs some straight-line speed.
    //   *  50%  the default, and the car EngineTemp describes (and every AI car runs).
    //   *  80%  a qualifying run: alone on the track, in clean air, so worth the speed.
    //   * 100%  fastest in a straight line, and overheats very quickly anywhere but clean air.
    public static class GrilleTape
    {
        public const float Default = 0.5f;
        public const float Qualifying = 0.8f;

        // Heating rate in a wake against the default tape: (tape / Default)², so 0 with no tape, 1 at half, 4 fully
        // taped (bumper to bumper at pack speed it overheats in ~22 s, not ~90).
        public const float MaxHeatScale = 4f;
        // How much of the grille the car ahead must cover before the engine heats (EngineTemp.HoldCover at the
        // default tape). Fully taped it heats with the car ahead about two car lengths up the road - anything short of
        // clean air.
        public const float FullTapeHoldCover = 0.15f;

        // Top speed against the default tape: an open grille gives up 2% (~4 mph at Daytona), a fully taped one gains 1%.
        public const float NoTapeTopSpeedLoss = 0.02f;
        public const float FullTapeTopSpeedGain = 0.01f;

        public static float Clamp(float tape) => Mathf.Clamp01(tape);

        public static float HeatScale(float tape)
        {
            float q = Clamp(tape) / Default;
            return Mathf.Min(MaxHeatScale, q * q);
        }

        public static float HoldCover(float tape)
        {
            float over = Mathf.Clamp01((Clamp(tape) - Default) / (1f - Default));
            return Mathf.Lerp(EngineTemp.HoldCover, FullTapeHoldCover, over);
        }

        public static float TopSpeedFactor(float tape)
        {
            tape = Clamp(tape);
            if (tape <= Default) return 1f - NoTapeTopSpeedLoss * (Default - tape) / Default;
            return 1f + FullTapeTopSpeedGain * (tape - Default) / (1f - Default);
        }

        public static string Label(float tape) => $"{Mathf.RoundToInt(Clamp(tape) * 100f)}%";

        // One line for the setup sheet under the slider.
        public static string Note(float tape)
        {
            tape = Clamp(tape);
            if (tape < 0.15f) return "No tape: never runs hot, but slow on the straights.";
            if (tape < 0.4f) return "Light tape: runs cool, a touch slower on the straights.";
            if (tape < 0.65f) return "Half taped: race trim, runs warm in a pack.";
            if (tape < 0.9f) return "Heavy tape: qualifying trim.";
            return "Fully taped: single-lap qualifying.";
        }
    }
}
