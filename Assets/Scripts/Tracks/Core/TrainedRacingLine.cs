using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Draftmaster.Tracks
{
    // The line RacingLineTrainer settled on, in a form the game can read back.
    //
    // Stored as JSON text rather than a ScriptableObject on purpose: it is generated output, so it wants to
    // diff in a pull request, and it wants to be regenerable without touching a binary asset. One file per
    // track at Assets/Resources/RacingLines/<trackId>.json.
    //
    // The lateral samples are resampled to an even spacing along the lap, so reading one back is an index and
    // a lerp instead of a search — SplineDriver asks for a few thousand of them every time it rebuilds.
    [Serializable]
    public class TrainedRacingLine
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string trackId;
        public string vehicle;
        public string trainedUtc;

        public float trackLength;         // sampled centreline length the line was trained against (m)
        public float spacing;             // metres between stored lateral samples
        public float seedLapTime;         // the min-curvature line it started from (s)
        public float trainedLapTime;      // what it got down to (s)
        public float lateralAccelMps2;    // the grip it was trained against
        public float drivenLength;        // length of the trained line itself (m)
        public int lapsSimulated;

        public float[] lateral;

        public float GainSeconds => seedLapTime - trainedLapTime;

        // Usable against a track of this sampled length? Geometry gets regenerated; a line trained against an
        // older shape of the road is worse than no line at all, so a length that has moved is a hard reject.
        public bool MatchesLength(float sampledLength)
        {
            if (lateral == null || lateral.Length < 4 || trackLength <= 1f || spacing <= 0.01f) return false;
            return Mathf.Abs(sampledLength - trackLength) <= Mathf.Max(2f, trackLength * 0.01f);
        }

        public float LateralAt(float distance)
        {
            if (lateral == null || lateral.Length == 0 || spacing <= 0.01f) return 0f;
            int n = lateral.Length;
            if (trackLength > 0f) distance = ((distance % trackLength) + trackLength) % trackLength;

            float f = distance / spacing;
            int i0 = Mathf.FloorToInt(f);
            float t = f - i0;
            i0 = ((i0 % n) + n) % n;
            int i1 = (i0 + 1) % n;
            return Mathf.Lerp(lateral[i0], lateral[i1], t);
        }

        // Resample an arbitrary (unevenly spaced) trained profile onto the even grid this stores.
        public static float[] Resample(float[] sampleDistance, float[] sampleLateral, float loopLength,
                                       float spacing, out int count)
        {
            count = 0;
            if (sampleDistance == null || sampleLateral == null || sampleDistance.Length < 2 ||
                sampleDistance.Length != sampleLateral.Length || loopLength <= 1f || spacing <= 0.01f)
                return new float[0];

            count = Mathf.Max(4, Mathf.CeilToInt(loopLength / spacing));
            var outLat = new float[count];
            int cursor = 0;
            int n = sampleDistance.Length;
            for (int i = 0; i < count; i++)
            {
                float d = i * spacing;
                while (cursor < n - 2 && sampleDistance[cursor + 1] < d) cursor++;
                int a = cursor, b = Mathf.Min(cursor + 1, n - 1);
                float da = sampleDistance[a], db = sampleDistance[b];
                float t = (db - da) > 1e-4f ? Mathf.Clamp01((d - da) / (db - da)) : 0f;
                outLat[i] = Mathf.Lerp(sampleLateral[a], sampleLateral[b], t);
            }
            return outLat;
        }

        // Generated numbers do not need seven significant figures, and the JSON is checked in.
        public void RoundForStorage()
        {
            if (lateral == null) return;
            for (int i = 0; i < lateral.Length; i++)
            {
                float r = Mathf.Round(lateral[i] * 1000f) / 1000f;
                lateral[i] = Mathf.Abs(r) < 1e-6f ? 0f : r;   // and no "-0"
            }
        }

        // Written by hand rather than through JsonUtility because JsonUtility spells every float out to its
        // full round-trip precision: a millimetre of lateral offset arrives as 0.10999999940395355, and 39
        // tracks of that is a megabyte of noise in the repo. One line per track, three decimals, still plain
        // JSON — TrainedRacingLines reads it straight back with JsonUtility.FromJson.
        public string ToCompactJson()
        {
            var sb = new StringBuilder((lateral != null ? lateral.Length * 7 : 0) + 512);
            sb.Append('{');
            sb.Append("\"version\":").Append(version);
            Field(sb, "trackId", trackId);
            Field(sb, "vehicle", vehicle);
            Field(sb, "trainedUtc", trainedUtc);
            Field(sb, "trackLength", trackLength);
            Field(sb, "spacing", spacing);
            Field(sb, "seedLapTime", seedLapTime);
            Field(sb, "trainedLapTime", trainedLapTime);
            Field(sb, "lateralAccelMps2", lateralAccelMps2);
            Field(sb, "drivenLength", drivenLength);
            sb.Append(",\"lapsSimulated\":").Append(lapsSimulated);
            sb.Append(",\"lateral\":[");
            if (lateral != null)
                for (int i = 0; i < lateral.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(lateral[i].ToString("0.###", CultureInfo.InvariantCulture));
                }
            sb.Append("]}");
            return sb.ToString();
        }

        static void Field(StringBuilder sb, string name, string value)
            => sb.Append(",\"").Append(name).Append("\":\"")
                 .Append((value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');

        static void Field(StringBuilder sb, string name, float value)
            => sb.Append(",\"").Append(name).Append("\":")
                 .Append(value.ToString("0.####", CultureInfo.InvariantCulture));
    }

    // Runtime lookup. Loads once per track id and caches the miss as well as the hit, so a track with no
    // trained line does not hit Resources on every rebuild.
    public static class TrainedRacingLines
    {
        public const string ResourceFolder = "RacingLines";

        static readonly Dictionary<string, TrainedRacingLine> _cache = new Dictionary<string, TrainedRacingLine>();

        public static TrainedRacingLine For(string trackId)
        {
            if (string.IsNullOrEmpty(trackId)) return null;
            if (_cache.TryGetValue(trackId, out var cached)) return cached;

            TrainedRacingLine line = null;
            var text = Resources.Load<TextAsset>(ResourceFolder + "/" + trackId);
            if (text != null && !string.IsNullOrEmpty(text.text))
            {
                try { line = JsonUtility.FromJson<TrainedRacingLine>(text.text); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[TrainedRacingLines] '{trackId}' failed to parse: {e.Message}");
                    line = null;
                }
                if (line != null && (line.version > TrainedRacingLine.CurrentVersion || line.lateral == null || line.lateral.Length < 4))
                    line = null;
            }

            _cache[trackId] = line;
            return line;
        }

        // Editor tooling calls this after writing a line so the next rebuild picks up the new one.
        public static void Invalidate() => _cache.Clear();
    }
}
