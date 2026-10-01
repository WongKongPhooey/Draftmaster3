using System;
using UnityEngine;

namespace Draftmaster.Fans
{
    // Where a pit-lane fan may put their feet: anywhere but the track.
    //
    // Fans used to stand 4 m off the pit centreline, which on a 9-12 m pit lane is ON the lane, and walked
    // straight at the player, so a player who stepped out to their car got followed onto the tarmac. Both
    // halves live here as plain math so the rules are EditMode-testable without a TrackBuilder; the caller
    // supplies the real "is this tarmac?" test (TrackBuilder.IsOnSurface — main track, pit lane, box lane).
    public static class FanFooting
    {
        // Lateral distance (m, from the pit centreline, garage side) a fan stands at: the wanted figure,
        // but never closer than `clearance` beyond the outer edge of the pit lane plus its box lane.
        public static float StandLateral(float pitHalfWidth, float boxLaneWidth, float wanted, float clearance)
            => Mathf.Max(wanted, Mathf.Max(0f, pitHalfWidth) + Mathf.Max(0f, boxLaneWidth) + Mathf.Max(0f, clearance));

        // Headings tried, in order, when the straight line toward the player runs onto the track. The wide
        // ones let a fan slide along the edge of the lane to keep up with a player walking down it.
        static readonly float[] kDeflections = { 0f, 30f, -30f, 60f, -60f, 90f, -90f };

        // One walking step of `distance` along `dir` that never ends on the track. Tries the direct heading,
        // then progressively wider deflections either side; if every one lands on tarmac the fan stays put.
        public static Vector2 Step(Vector2 from, Vector2 dir, float distance, Func<Vector2, bool> onTrack)
        {
            if (distance <= 0f || dir.sqrMagnitude < 1e-8f) return from;
            dir.Normalize();
            for (int i = 0; i < kDeflections.Length; i++)
            {
                Vector2 next = from + Rotate(dir, kDeflections[i]) * distance;
                if (onTrack == null || !onTrack(next)) return next;
            }
            return from;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
