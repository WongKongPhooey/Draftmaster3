using UnityEngine;

// Global modifiers applied to every car's lateral grip and pace. Singleton-style static fields.
// Wire weather/temp/rubber-buildup into these from a race manager later.
public static class TrackConditions
{
    // Baked baseline so the GripMultiplier slider at neutral (1.0) feels like 1.2x of the raw tuning.
    // Grip consumers use Effective, not GripMultiplier directly.
    public const float BaseGrip = 1.2f;

    // Baked baseline so the PowerMultiplier slider at neutral (1.0) feels like 1.5x of the raw accel.
    // Power consumers use EffectivePower, not PowerMultiplier directly.
    public const float BasePower = 1.5f;

    [Tooltip("Driver-facing grip slider. Default 1.2 (= 1.2 baked × 1.2 = 1.44x effective), <1 damp/wet, >1 extra bite. Multiplied by BaseGrip.")]
    public static float GripMultiplier = 1.2f;

    [Tooltip("Driver-facing power slider. Default 0.8 (= 1.5 baked × 0.8 = 1.2x effective), <1 hot air / thin atmosphere. Multiplied by BasePower.")]
    public static float PowerMultiplier = 0.8f;

    // Effective grip the dynamics actually consume: baked baseline × driver slider × the track's own grip × the car
    // package raced here.
    public static float Effective => BaseGrip * GripMultiplier * TrackGripScale * PackageGripScale;

    // The car package's grip (TrackTuning.packageGrip): 1 on a road course, under 1 on an unplated oval, where the
    // road-course grip took every turn at or near the top speed. Every car - player and AI, physics and speed plans -
    // reads it through Effective. Set when the track loads.
    public static float PackageGripScale = 1f;

    // The car package's yaw inertia (TrackTuning.packageYawInertia) for the player's car: >1 on ovals, a heavier car
    // slower to take a set and to snap back. PlayerVehicleController applies it to the human car only. Set when the
    // track loads.
    public static float PackageYawInertiaScale = 1f;

    [Tooltip("The car package's engine at this track (TrackTuning.packageSpeed): a multiplier on every car's engine pull and top speed - the player's and the AI's physics and the AI's speed plan. Set per track when it loads.")]
    public static float PackageSpeedScale = 1f;

    [Tooltip("The AI's engine power here, apart from its pace (TrackTuning.aiPower). <= 0 = the same as AiPaceMultiplier, as everywhere that has not set it. Set per track when it loads.")]
    public static float AiPowerMultiplier = 0f;

    [Tooltip("On this oval the AI's ideal (single-file) line is the road's minimum-curvature line, not its trained one (TrackTuning.aiOptimisedLine). Set per track when it loads.")]
    public static bool AiOptimisedOvalLine;

    [Tooltip("How far apart the AI drivers' corner commitment spreads here (TrackTuning.aiSpread; AIRatings.CornerCommitment). 1 = the full rating range. Set per track when it loads.")]
    public static float AiSpreadScale = 1f;

    // Grip the track itself adds - 1 everywhere but a plated superspeedway, where the turns are given exactly
    // what they need to be taken flat out at the plate speed with a full tow (RestrictorPlate). Set when it loads.
    public static float TrackGripScale = 1f;

    // Effective power the dynamics actually consume: baked baseline × driver slider.
    public static float EffectivePower => BasePower * PowerMultiplier;

    // 0.02 is calibrated against a hot lap: one flat-out lap costs the loaded (outside) tyres ~7% and the
    // unloaded side ~3%, so a stint fades over many laps rather than falling off in a handful.
    [Tooltip("Scales tyre wear accrual on every car. Default 0.02 (gentle). 0 = no wear, 1 = raw model rate, >1 abrasive track.")]
    public static float TireWearMultiplier = 0.02f;

    [Tooltip("Scales fuel burn on every car. 1 nominal, 0 = no burn, 2 = double consumption.")]
    public static float FuelUseMultiplier = 1f;

    [Tooltip("Scales crash damage accrual on every car. 1 nominal, 0 = invulnerable bodywork, 2 = fragile.")]
    public static float DamageMultiplier = 1f;

    // The AI strength a track runs at when AIPaceCalibration has no entry for it.
    public const float DefaultAiPace = 1.2f;
    public const float DefaultAiGrip = 1.2f;

    [Tooltip("Scales every racing AI's pace (their target speeds AND engine power under the shared dynamic model). 1 nominal, <1 slower field, >1 faster field. Does not touch formation/safety-car pacing. Set per track by AIPaceCalibration.")]
    public static float AiPaceMultiplier = DefaultAiPace;

    [Tooltip("AI-only grip multiplier layered on top of the global grip. >1 = AI corner faster than the player at equal tuning; player unaffected. Set per track by AIPaceCalibration.")]
    public static float AiGripMultiplier = DefaultAiGrip;

    [Tooltip("How much the tow is worth at this track (TrackTuning.draftScale): multiplies the draft's top-speed gain, its extra acceleration and the AI's tow boost. Set per track when it loads.")]
    public static float DraftScale = 1f;

    [Tooltip("How fast tyres wear at this track (TrackTuning.tyreWearScale): a road course and a bullring chew through them, a superspeedway barely touches them. Multiplies TireWearMultiplier. Set per track when it loads.")]
    public static float TyreWearScale = 1f;

    [Tooltip("How much grip a worn tyre gives up at this track (TrackTuning.tyreGripLossScale): little on a superspeedway's flat-out turns. Set per track when it loads.")]
    public static float TyreGripLossScale = 1f;

    [Tooltip("Dirty air at this track (TrackTuning.dirtyAirLoss): the share of grip a full tow's wake costs at speed, every car (Draftmaster.Sim.DirtyAir). 0 = none. Set per track when it loads.")]
    public static float DirtyAirLoss = 0f;

    [Tooltip("How far back the tow reaches at this track (TrackTuning.draftRangeScale): multiplies VehicleInfo.draftingMaxGap. Set per track when it loads.")]
    public static float DraftRangeScale = 1f;

    [Tooltip("How close the AI follows at this track (TrackTuning.draftFollowScale): multiplies their following headway. Set per track when it loads.")]
    public static float AiFollowScale = 1f;

    [Tooltip("Whether the AI races in lanes at this track (TrackTuning.raceInLanes): ovals yes, road courses no - a road course's width changes corner by corner and its lanes with it, so cars hopped between them. Set per track when it loads.")]
    public static bool AiLanes = true;

    [Tooltip("Restrictor plate: every car's solo top speed (mph) at this track, 0 = none. The draft and the push add to it. Set per track when it loads (RestrictorPlate).")]
    public static float PlateMph = 0f;

    [Tooltip("The AI holds the throttle wide open all lap - no lift for the turns. Superspeedways, where the plate keeps the speed inside what the banking holds.")]
    public static bool AiFlatOut = false;

    [Tooltip("The AI races as a pack: lines nose to tail, pull out only with a run, a line follows its leader, nobody backs out. Superspeedways.")]
    public static bool AiPackRacing = false;
    // The tow is worth more the faster the car goes (DraftAero.SpeedShare). False = the same at any speed over the
    // car's draftingMinSpeed.
    public static bool DraftBySpeed = true;
    // How error-prone racing is here (TrackTuning.cautionProneness): Draftmaster.Sim.AIRatings.MistakeScale.
    public static float AiCautionProneness = 0.5f;

    [Tooltip("How much a line of cars pushes the car at its head (bump drafting). 0 = none.")]
    public static float PushScale = 0f;

    [Tooltip("0-1: how hard the AI steers away from a car alongside. 1 = road-course caution, low = holds its lane three wide.")]
    public static float AiSideAwareness = 1f;

    [Tooltip("Metres centre to centre the AI leave a car alongside at this track (0 = AIRacingBehaviour.alongsideWallGap).")]
    public static float AiAlongsideGap = 0f;

    [Tooltip("Metres between lane centres for the AI at this track (0 = AIRacingBehaviour's own).")]
    public static float AiLaneSpacing = 0f;

    [Tooltip("Pack racing: the number of fixed grooves, a lane spacing apart and centred on the centreline (0 = lanes fit between the AI's bounds wherever the car is).")]
    public static int AiPackLanes = 0;

    // Per-track aero, following, plate and racing temperament, from the track-type table. Empty id = the neutral
    // defaults.
    public static void ApplyTrackTuning(string trackId)
    {
        DraftScale = 1f;
        DraftRangeScale = 1f;
        TyreWearScale = 1f;
        TyreGripLossScale = 1f;
        DirtyAirLoss = 0f;
        AiFollowScale = 1f;
        AiLanes = true;
        TrackGripScale = 1f;
        PlateMph = 0f;
        AiFlatOut = false;
        AiPackRacing = false;
        PushScale = 0f;
        AiSideAwareness = 1f;
        AiLaneSpacing = 0f;
        AiAlongsideGap = 0f;
        AiPackLanes = 0;
        AiCautionProneness = 0.5f;
        PackageGripScale = 1f;
        PackageYawInertiaScale = 1f;
        PackageSpeedScale = 1f;
        AiPowerMultiplier = 0f;
        AiOptimisedOvalLine = false;
        AiSpreadScale = 1f;
        if (string.IsNullOrEmpty(trackId)) return;
        var tuning = TrackProfile.ForTrack(trackId);
        if (tuning.draftScale > 0f) DraftScale = tuning.draftScale;
        if (tuning.draftRangeScale > 0f) DraftRangeScale = tuning.draftRangeScale;
        if (tuning.tyreWearScale > 0f) TyreWearScale = tuning.tyreWearScale;
        if (tuning.tyreGripLossScale > 0f) TyreGripLossScale = tuning.tyreGripLossScale;
        if (tuning.draftFollowScale > 0f) AiFollowScale = tuning.draftFollowScale;
        DirtyAirLoss = Mathf.Max(0f, tuning.dirtyAirLoss);
        AiLanes = tuning.raceInLanes;
        AiPackRacing = tuning.packRacing;
        PushScale = Mathf.Max(0f, tuning.pushScale);
        AiSideAwareness = tuning.sideAwareness > 0f ? Mathf.Clamp01(tuning.sideAwareness) : 1f;
        AiLaneSpacing = tuning.laneSpacing;
        AiAlongsideGap = tuning.alongsideGap;
        AiPackLanes = tuning.packLanes;
        AiCautionProneness = tuning.cautionProneness > 0f ? Mathf.Clamp01(tuning.cautionProneness) : 0.5f;
        // A track the catalogue doesn't know falls back to the Speedway row above; it keeps the road-course car
        // rather than being handed the oval package on a guess.
        bool known = TrackCatalog.Row(trackId) != null;
        if (known && tuning.packageGrip > 0f) PackageGripScale = tuning.packageGrip;
        if (known && tuning.packageYawInertia > 0f) PackageYawInertiaScale = tuning.packageYawInertia;
        if (known && tuning.packageSpeed > 0f) PackageSpeedScale = tuning.packageSpeed;
        if (known && tuning.aiPower > 0f) AiPowerMultiplier = tuning.aiPower;
        AiOptimisedOvalLine = known && tuning.aiOptimisedLine;
        if (known && tuning.aiSpread > 0f) AiSpreadScale = tuning.aiSpread;
        float plate = 0f, gripScale = 1f;
        if (tuning.plateMph > 0f && RestrictorPlate.Solve(trackId, tuning.plateMph, out plate, out gripScale))
        {
            PlateMph = plate;
            TrackGripScale = gripScale;
            AiFlatOut = tuning.flatOut;
            // The plate solve owns a superspeedway's grip and speed (it is solved against the un-packaged car).
            PackageGripScale = 1f;
            PackageSpeedScale = 1f;
        }
    }

    // A plated superspeedway is raced on equal cars: the plate gives every car the same top speed and the draft
    // decides the race, so the AI's power and grip bonuses (which elsewhere make up for it not driving as well
    // as a person) are nothing but a straight-line advantage there - they let it pull back up to the plate and
    // into a tow faster than the player's car can. RestrictorPlate already solves the turns' grip for the
    // player's car, so the AI still holds them flat out without its bonus.
    public static bool AiParity => PlateMph > 0f;

    // The AI's engine-power (and envelope-stretch) multiplier: AiPaceMultiplier unless the track sets its own
    // (AiPowerMultiplier), and 1 on a plated track.
    public static float AiPowerScale => AiParity ? 1f : AiPowerMultiplier > 0f ? AiPowerMultiplier : AiPaceMultiplier;

    // Effective grip for AI-driven cars: global effective grip × AI-only bonus (none on a plated track).
    public static float AiEffective => AiParity ? Effective : Effective * AiGripMultiplier;

    public static void Reset()
    {
        GripMultiplier = 1.2f;
        PowerMultiplier = 0.8f;
        TireWearMultiplier = 0.02f;
        FuelUseMultiplier = 1f;
        DamageMultiplier = 1f;
        AiPaceMultiplier = DefaultAiPace;
        AiGripMultiplier = DefaultAiGrip;
        DraftScale = 1f;
        DraftRangeScale = 1f;
        TyreWearScale = 1f;
        TyreGripLossScale = 1f;
        DirtyAirLoss = 0f;
        AiFollowScale = 1f;
        AiLanes = true;
        TrackGripScale = 1f;
        PlateMph = 0f;
        AiFlatOut = false;
        AiPackRacing = false;
        PushScale = 0f;
        AiSideAwareness = 1f;
        AiLaneSpacing = 0f;
        AiAlongsideGap = 0f;
        AiPackLanes = 0;
        AiCautionProneness = 0.5f;
        PackageGripScale = 1f;
        PackageYawInertiaScale = 1f;
        PackageSpeedScale = 1f;
        AiPowerMultiplier = 0f;
        AiOptimisedOvalLine = false;
        AiSpreadScale = 1f;
    }
}
