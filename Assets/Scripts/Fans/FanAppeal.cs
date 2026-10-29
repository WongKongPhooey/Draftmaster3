using UnityEngine;

namespace Draftmaster.Fans
{
    // The player's "fan appeal": a persistent 0..100 rating that drives how many autograph-seekers turn up
    // in the pit lane. PlayerPrefs-backed, following the project's persistence convention (same as
    // PlayerStatsLedger / track records). Signing an autograph nudges it up; a fan you ignore until they
    // give up nudges it down. Kept dependency-free and in its own assembly so the decision math is
    // EditMode-testable (Assembly-CSharp can't be referenced by test assemblies).
    public static class FanAppeal
    {
        const string Key = "fan.appeal";

        public const float Min = 0f;
        public const float Max = 100f;
        // Where a fresh save sits before anything moves it. High for the demo: the player is Ace Emerson, a
        // Cup veteran the crowd already knows (18/20 Fan Appeal on the laptop), so the paddock is busy with
        // autograph-seekers from the first morning. Drop it when a career starts from nobody.
        public const float Default = 90f;

        public static float Value
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(Key, Default), Min, Max);
            set
            {
                PlayerPrefs.SetFloat(Key, Mathf.Clamp(value, Min, Max));
                PlayerPrefs.Save();
            }
        }

        // Adds delta (negative to reduce), clamps to [Min, Max], persists, and returns the new value.
        public static float Add(float delta)
        {
            float next = Mathf.Clamp(Value + delta, Min, Max);
            Value = next;
            return next;
        }

        // Appeal expressed as 0..1.
        public static float Normalised => Mathf.InverseLerp(Min, Max, Value);

        // How many autograph-seekers to place for a given appeal: scales linearly from minFans (at 0 appeal)
        // to maxFans (at 100 appeal), rounded to a whole fan. Pure so it can be unit-tested directly.
        public static int FanCountForAppeal(float appeal, int minFans, int maxFans)
        {
            int lo = Mathf.Min(minFans, maxFans);
            int hi = Mathf.Max(minFans, maxFans);
            float t = Mathf.InverseLerp(Min, Max, Mathf.Clamp(appeal, Min, Max));
            return Mathf.RoundToInt(Mathf.Lerp(lo, hi, t));
        }
    }
}
