using UnityEngine;

namespace Draftmaster.Fights
{
    // What a fighter can throw. Only Shove has art today (Assets/Animation/OnFoot/Push.anim); the hooks are
    // wired end-to-end but gated behind DriverFight.enableHooks until their clips exist.
    public enum FightMove { Shove, LeftHook, RightHook }

    // Why a fight stopped. Peacemakers are sent in on anything but None — the fight itself never "ends"
    // on a knockout, it ends when the paddock pulls the two of them apart.
    public enum BreakupReason { None, Timeout, Exhausted }

    // The decision maths behind a paddock scrap, kept free of Unity scene types so it can be unit tested
    // in EditMode (the rest of the fight is MonoBehaviours that need Play Mode, which can't be driven while
    // the editor is unfocused).
    //
    // Nothing here is lethal or bloody by design: a hit costs "composure", not health in a medical sense,
    // and the worst outcome is a shove that leaves someone winded while the crews drag them apart. That
    // keeps the mechanic inside a 7+ rating.
    public static class FightRules
    {
        // Composure both fighters start with, and the bar the health bar is drawn against.
        public const float MaxHealth = 100f;

        // Mirror of DriverRelationships.RivalThreshold. Duplicated rather than referenced because that type
        // lives in Assembly-CSharp, which an asmdef cannot see; callers should pass the real value in.
        public const float RivalThreshold = -30f;

        // Base cost of each move before the attacker's aggression and the random roll are applied.
        public const float ShoveDamage = 7f;
        public const float HookDamage = 12f;

        // Below this, a fighter is visibly spent — hunched, arms down — and the crews step in.
        public const float ExhaustedHealth = 20f;

        // Aggression is the roster's 1..20 stat; it scales damage within these bounds.
        public const float MinAggressionScale = 0.8f;
        public const float MaxAggressionScale = 1.2f;

        // Per-swing random spread, as a fraction either side of the scaled base damage.
        public const float DamageSpread = 0.15f;

        public static float BaseDamage(FightMove move) => move == FightMove.Shove ? ShoveDamage : HookDamage;

        // Damage for one connected move. aggression is the roster stat (1..20, clamped); roll is a 0..1
        // random draw, so the same inputs always give the same number — which is what makes it testable.
        public static float Damage(FightMove move, int aggression, float roll)
        {
            float agg01 = Mathf.Clamp01((Mathf.Clamp(aggression, 1, 20) - 1) / 19f);
            float scale = Mathf.Lerp(MinAggressionScale, MaxAggressionScale, agg01);
            float spread = Mathf.Lerp(1f - DamageSpread, 1f + DamageSpread, Mathf.Clamp01(roll));
            return BaseDamage(move) * scale * spread;
        }

        // A swing lands only if the target is within arm's length AND roughly in front of the attacker.
        // facingDot is dot(attacker forward, direction to target); minFacingDot ~0.3 allows a wide arc so
        // the player doesn't have to be pixel-accurate with a top-down character.
        public static bool Connects(float distance, float reach, float facingDot, float minFacingDot)
        {
            if (distance > reach) return false;
            return facingDot >= minFacingDot;
        }

        public static float ApplyDamage(float health, float damage)
        {
            if (damage <= 0f) return Mathf.Clamp(health, 0f, MaxHealth);
            return Mathf.Clamp(health - damage, 0f, MaxHealth);
        }

        // Should the crews step in? Exhaustion is checked first: a fight that has already gone one-sided
        // gets broken up immediately rather than running the clock out.
        public static BreakupReason ShouldBreakUp(float elapsed, float maxSeconds,
                                                  float healthA, float healthB, float exhaustedAt)
        {
            if (healthA <= exhaustedAt || healthB <= exhaustedAt) return BreakupReason.Exhausted;
            if (elapsed >= maxSeconds) return BreakupReason.Timeout;
            return BreakupReason.None;
        }

        // +1 = the player came out on top, -1 = the rival did, 0 = honours even (within a point).
        public static int Winner(float playerHealth, float rivalHealth)
        {
            if (Mathf.Abs(playerHealth - rivalHealth) < 1f) return 0;
            return playerHealth > rivalHealth ? 1 : -1;
        }

        // Seconds an AI fighter waits between swings. A 20-Aggression driver comes forward roughly twice
        // as often as a 1-Aggression one; roll (0..1) jitters it so exchanges don't fall into a metronome.
        public static float AiAttackInterval(int aggression, float roll, float fastest = 1.1f, float slowest = 2.6f)
        {
            float agg01 = Mathf.Clamp01((Mathf.Clamp(aggression, 1, 20) - 1) / 19f);
            float baseInterval = Mathf.Lerp(slowest, fastest, agg01);
            return baseInterval * Mathf.Lerp(0.8f, 1.25f, Mathf.Clamp01(roll));
        }

        // How close an AI fighter tries to stand: just inside its own reach, so it can swing without
        // shoving its sprite through the player's.
        public static float DesiredRange(float reach) => reach * 0.72f;

        // Is this driver angry enough with the player to swing at them? threshold is passed in so the
        // caller can hand over DriverRelationships.RivalThreshold rather than trusting the mirror above.
        public static bool CanChallenge(float relationshipScore, float threshold = RivalThreshold)
            => relationshipScore <= threshold;

        // 0..1 for a health bar fill.
        public static float HealthFraction(float health) => Mathf.Clamp01(health / MaxHealth);

        // How far behind the driver a peacemaker stands while walking them off: their two bodies touching,
        // the peacemaker's front against the driver's back. Half-depths are measured along the way both are
        // facing; gap > 0 leaves daylight, gap < 0 presses them together.
        public static float EscortHoldDistance(float driverHalfDepth, float peacemakerHalfDepth, float gap = 0f)
            => Mathf.Max(0.05f, Mathf.Max(0f, driverHalfDepth) + Mathf.Max(0f, peacemakerHalfDepth) + gap);

        // Directions tried for an escort, as degrees off "straight away from the fight". Nothing past 120 —
        // further round than that walks the driver back through the person they were fighting.
        static readonly float[] EscortFanDeg = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f };

        // Which way to walk a driver out of a fight. outward is straight away from the fight's centre;
        // clearRun(dir) answers how far (m) the pair could walk that way before a wall, a motorhome or the
        // paddock fence; wanted is the full separation. A direction with the whole run clear beats a
        // blocked one, and among equally clear ones the least turned wins, so open ground keeps the old
        // straight-out escort. avoid is the other escort's heading (zero if not chosen yet): the two
        // drivers are steered apart rather than both down the one open alley.
        public static Vector2 ChooseEscortHeading(Vector2 outward, System.Func<Vector2, float> clearRun,
                                                  float wanted, Vector2 avoid)
        {
            if (outward.sqrMagnitude < 1e-6f) outward = Vector2.up;
            outward.Normalize();
            wanted = Mathf.Max(0.01f, wanted);
            bool haveAvoid = avoid.sqrMagnitude > 1e-6f;
            if (haveAvoid) avoid.Normalize();

            Vector2 best = outward;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < EscortFanDeg.Length; i++)
            {
                float deg = EscortFanDeg[i];
                float rad = deg * Mathf.Deg2Rad;
                float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
                Vector2 dir = new Vector2(outward.x * c - outward.y * s, outward.x * s + outward.y * c);

                float run = clearRun != null ? Mathf.Clamp(clearRun(dir), 0f, wanted) : wanted;
                float score = run / wanted - 0.1f * Mathf.Abs(deg) / 120f;
                if (haveAvoid) score -= 0.3f * Mathf.Max(0f, Vector2.Dot(dir, avoid));
                if (score > bestScore) { bestScore = score; best = dir; }
            }
            return best;
        }
    }
}
