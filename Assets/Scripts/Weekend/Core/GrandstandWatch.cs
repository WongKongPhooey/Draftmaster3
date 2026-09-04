using UnityEngine;

namespace Draftmaster.Weekend
{
    // How long an hour in the stand takes, and where the camera looks while it does.
    //
    // A session on the sheet is an hour of the weekend's clock. Sat in a grandstand watching somebody
    // else's practice, that hour is not an hour of the player's evening: the session is played at speed, so
    // the field is out, circulating and timed for a few minutes rather than sixty. Nothing about the cars
    // changes — they run at racing pace, exactly as they would if the player were in one — it is the
    // session's LENGTH that is compressed, which is the same trick a broadcast highlights package plays.
    //
    // The pure half lives here so the numbers are testable without a scene: GrandstandVisit holds the
    // clock, WeekendMarker holds the authored vantage, and both ask this what the answer is.
    public static class GrandstandWatch
    {
        // Weekend minutes per real minute. Ten means an hour session is six minutes in the seat.
        public const float Speed = 10f;

        // However long the session is on the sheet, the player is never asked to sit for longer than this.
        // A two-hour race compresses harder rather than running to twelve minutes.
        public const float MaxSeconds = 360f;

        // ... and never so short that arriving and the chequered flag are the same beat.
        public const float MinSeconds = 20f;

        // Real seconds a session of `sessionMinutes` takes to watch.
        public static float WatchSeconds(int sessionMinutes)
        {
            float raw = Mathf.Max(0, sessionMinutes) * 60f / Speed;
            return Mathf.Clamp(raw, MinSeconds, MaxSeconds);
        }

        // How far through the session the player has watched, 0..1.
        public static float Progress01(float elapsedSeconds, float watchSeconds) =>
            watchSeconds <= 0f ? 1f : Mathf.Clamp01(elapsedSeconds / watchSeconds);

        // Where the session clock has got to, in weekend minutes from its green flag. What the timing
        // screen puts in its corner, so the compressed hour still reads as an hour.
        public static int SessionMinuteAt(float elapsedSeconds, int sessionMinutes) =>
            Mathf.Clamp(Mathf.FloorToInt(Progress01(elapsedSeconds, WatchSeconds(sessionMinutes)) * sessionMinutes),
                        0, Mathf.Max(0, sessionMinutes));

        // ------------------------------------------------------------------ the shot

        // How far from the seat toward the circuit the camera settles when a track has not authored a
        // vantage of its own. Short of the road itself: the stand wants to stay in the bottom of the frame,
        // or the shot is a piece of tarmac with nothing to say who is watching it.
        public const float DefaultPull01 = 0.55f;

        // The fallback vantage: along the line from the seat to the nearest point on the racing surface.
        // Every venue gets a workable shot out of this without anybody opening its package; a track that
        // wants a better one authors a View child on its Grandstand_Marker and this is never asked.
        public static Vector2 Vantage(Vector2 seat, Vector2 trackPoint, float pull01 = DefaultPull01) =>
            Vector2.Lerp(seat, trackPoint, Mathf.Clamp01(pull01));

        // Orthographic size that frames the road from that distance. Half-height in metres, so the seat
        // stays in shot at one end and there is a length of circuit at the other.
        public static float ZoomFor(float seatToTrackMetres, float min = 14f, float max = 45f) =>
            Mathf.Clamp(Mathf.Abs(seatToTrackMetres) * 1.6f, min, max);
    }
}
