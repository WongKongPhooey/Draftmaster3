using UnityEditor;
using UnityEngine;

public static class RacingLineMigrationMenu
{
    [MenuItem("Tools/Track/Normalise Racing Line Sides")]
    public static void NormaliseRacingLineSides()
    {
        var guids = AssetDatabase.FindAssets("t:TrackInfoV2");
        int swapsTotal = 0;
        int fillsTotal = 0;
        int assetsChanged = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var track = AssetDatabase.LoadAssetAtPath<TrackInfoV2>(path);
            if (track == null || track.segments == null) continue;

            int swaps = 0;
            int fills = 0;
            for (int i = 0; i < track.segments.Length; i++)
            {
                var seg = track.segments[i];
                var rl = seg.racingLine;

                NormaliseTriple(ref rl.leftEntry, ref rl.idealEntry, ref rl.rightEntry, ref swaps, ref fills);
                NormaliseTriple(ref rl.leftApex,  ref rl.idealApex,  ref rl.rightApex,  ref swaps, ref fills);
                NormaliseTriple(ref rl.leftExit,  ref rl.idealExit,  ref rl.rightExit,  ref swaps, ref fills);

                seg.racingLine = rl;
                track.segments[i] = seg;
            }

            if (swaps > 0 || fills > 0)
            {
                EditorUtility.SetDirty(track);
                assetsChanged++;
                swapsTotal += swaps;
                fillsTotal += fills;
                Debug.Log($"[RacingLineMigration] {track.name}: {swaps} swap(s), {fills} default-fill(s).");
            }
        }

        if (assetsChanged > 0) AssetDatabase.SaveAssets();
        Debug.Log($"[RacingLineMigration] Done. {assetsChanged} asset(s) changed; {swapsTotal} total swap(s); {fillsTotal} total default-fill(s).");
    }

    static void NormaliseTriple(ref float left, ref float ideal, ref float right, ref int swaps, ref int fills)
    {
        if (Mathf.Approximately(left, 0f) && Mathf.Approximately(ideal, 0f) && Mathf.Approximately(right, 0f))
        {
            left = -3f;
            ideal = 0f;
            right = 3f;
            fills++;
            return;
        }

        if (left > right)
        {
            (left, right) = (right, left);
            swaps++;
        }
    }
}
