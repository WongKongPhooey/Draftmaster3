using System.Collections.Generic;

namespace Draftmaster.Sim
{
    // The TV director behind broadcast mode (BroadcastController): which car the camera cuts to next, when a hit is
    // big enough to cut to straight away, and how the ticker along the top reads.
    //
    // Engine-free so the rules are tested without a scene.
    public static class BroadcastShots
    {
        public const int None = -1;

        // The next car to cut to, as an index into the running order (0 = leader), or None for an empty field.
        //
        // `gapAheadSec[i]` is car i's gap to the car in front of it in seconds (anything for the leader),
        // `sinceShownSec[i]` how long ago the camera was last on it (float.PositiveInfinity if never), `current` the
        // car on camera now (never picked again straight away unless it is the only car).
        //
        // A shot is worth: a little for any car, more for the leader, and most for a car within `battleGapSec` of
        // the one ahead — the closer, the more — so the camera goes where the racing is. Every score is scaled by
        // how long it has been since the car was on camera (Freshness: nothing straight after, and still climbing
        // well past `revisitSec`), so a long-running battle is shown again and again, the leader every few shots,
        // and a car the camera has not been to in a long while (or ever) still gets its turn. Ties (cars left off
        // long enough that their freshness has topped out) go to the one off camera longest, then the car further
        // up the order — lowest index alone starved the back of the field once everyone was topped out.
        public static int Pick(IReadOnlyList<float> gapAheadSec, IReadOnlyList<float> sinceShownSec, int current,
                               float battleGapSec = 1f, float revisitSec = 40f)
        {
            int count = gapAheadSec != null ? gapAheadSec.Count : 0;
            if (count <= 0) return None;
            if (count == 1) return 0;

            int best = None;
            float bestScore = -1f, bestSince = 0f;
            for (int i = 0; i < count; i++)
            {
                if (i == current) continue;
                float score = Worth(i, gapAheadSec[i], battleGapSec);
                float since = sinceShownSec != null && i < sinceShownSec.Count ? sinceShownSec[i] : float.PositiveInfinity;
                score *= Freshness(since, revisitSec);
                bool better = score > bestScore + TieEpsilon ||
                              (score > bestScore - TieEpsilon && since > bestSince);
                if (best == None || better) { bestScore = score; bestSince = since; best = i; }
            }
            return best;
        }

        const float TieEpsilon = 1e-4f;

        // What a shot of car i is worth before freshness.
        public static float Worth(int index, float gapAheadSec, float battleGapSec)
        {
            float score = 0.2f;
            // A small edge: in a quiet race the leader comes back every several shots, not every other one, or the
            // rest of the field never gets on TV.
            if (index == 0) score += 0.3f;
            else if (battleGapSec > 0f && gapAheadSec >= 0f && gapAheadSec < battleGapSec)
                score += 0.5f + 2f * (1f - gapAheadSec / battleGapSec);
            return score;
        }

        // 0 straight after a car was on camera, 1 once `revisitSec` has passed, and on up to MaxFreshness for a car
        // left off camera far longer (or never shown) — so being forgotten counts against the leader's edge, and a
        // quiet field is walked through rather than tied with the car shown a few shots ago.
        public const float MaxFreshness = 3f;

        public static float Freshness(float sinceShownSec, float revisitSec)
        {
            if (float.IsPositiveInfinity(sinceShownSec) || revisitSec <= 0f) return MaxFreshness;
            if (sinceShownSec <= 0f) return 0f;
            float f = sinceShownSec / revisitSec;
            return f > MaxFreshness ? MaxFreshness : f;
        }

        // Cut to a hit now? Only a hard one (closing speed at or above `bigHitMps`), never in the first `minShotSec`
        // of the shot already up (a pile-up throws a hit a step for a second or more, and the camera must not ping
        // between the cars in it), and while already showing a hit only for a harder one.
        public static bool CutToHit(float closingMps, float bigHitMps, float shotSec, float minShotSec,
                                    bool showingHit, float showingHitMps)
        {
            if (closingMps < bigHitMps) return false;
            if (shotSec < minShotSec) return false;
            if (showingHit && closingMps <= showingHitMps) return false;
            return true;
        }

        // A car's gap on the ticker: LEADER, +0.512 (seconds behind the leader), or -1 LAP / -3 LAPS down.
        public static string Gap(int position, float gapToLeaderSec, int lapsDown)
        {
            if (position <= 1) return "LEADER";
            if (lapsDown > 0) return "-" + lapsDown + (lapsDown == 1 ? " LAP" : " LAPS");
            if (gapToLeaderSec < 0f) gapToLeaderSec = 0f;
            return "+" + gapToLeaderSec.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);
        }

        // Whole laps a car is down on the leader, from the distance between them along the track.
        public static int LapsDown(float leaderProgress, float carProgress, float lapLength)
        {
            if (lapLength <= 0f) return 0;
            float behind = leaderProgress - carProgress;
            return behind <= 0f ? 0 : (int)(behind / lapLength);
        }

        // The race clock: 7:05, 1:02:09.
        public static string Clock(float seconds)
        {
            if (seconds < 0f || float.IsNaN(seconds)) seconds = 0f;
            int total = (int)seconds;
            int h = total / 3600, m = total / 60 % 60, s = total % 60;
            return h > 0 ? h + ":" + m.ToString("00") + ":" + s.ToString("00") : m + ":" + s.ToString("00");
        }

        // The crawl: offset (px) the strip of entries has scrolled by, wrapping once a whole copy has passed so it
        // loops without a seam. `stripWidth` is one full copy of the entries.
        public static float Scroll(float offset, float pxPerSec, float dt, float stripWidth)
        {
            if (stripWidth <= 0f) return 0f;
            offset += pxPerSec * dt;
            offset %= stripWidth;
            if (offset < 0f) offset += stripWidth;
            return offset;
        }
    }
}
