using Draftmaster.Sim;
using UnityEngine;

// The saved AI STRENGTH and AI AGGRESSION settings (OPTIONS > AI DRIVERS). What they mean is
// Draftmaster.Sim.AIDifficultyRules; this keeps them in PlayerPrefs and applies them: strength when a track package
// binds (TrackPackage, after the track's calibration), aggression when a driver is bound to a car (AIDriverBinding).
public static class AIDifficulty
{
    const string KeyStrength = "AIStrengthPercent";
    const string KeyAggression = "AIAggressionPercent";

    public static int StrengthPercent
    {
        get => AIDifficultyRules.ClampStrength(PlayerPrefs.GetInt(KeyStrength, AIDifficultyRules.StrengthDefault));
        set { PlayerPrefs.SetInt(KeyStrength, AIDifficultyRules.ClampStrength(value)); PlayerPrefs.Save(); }
    }

    public static int AggressionPercent
    {
        get => AIDifficultyRules.ClampAggression(PlayerPrefs.GetInt(KeyAggression, AIDifficultyRules.AggressionDefault));
        set { PlayerPrefs.SetInt(KeyAggression, AIDifficultyRules.ClampAggression(value)); PlayerPrefs.Save(); }
    }

    // A Master Draft championship round sets the field's strength from the series' AI level
    // (Draftmaster.MasterDraft.SeriesRace.AiStrength), in place of the saved setting. Set by MasterDraftRaceLauncher
    // before the race scene loads; only read in GameSession.Mode.MasterDraft. 0 = none (use the setting).
    public static float MasterDraftStrengthScale;

    // On top of AIPaceCalibration.ApplyFor: the field's grip and power, together.
    public static void ApplyStrength()
    {
        float s = GameSession.IsMasterDraft && MasterDraftStrengthScale > 0f
            ? MasterDraftStrengthScale
            : AIDifficultyRules.StrengthScale(StrengthPercent);
        TrackConditions.AiGripMultiplier *= s;
        TrackConditions.AiPaceMultiplier *= s;
    }
}
