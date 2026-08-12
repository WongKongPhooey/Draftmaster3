using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// F8 overlay: one row per car on the formation lap, ordered front to back, showing what the gap law is
// actually doing. Built because the pace-lap pile-ups were being diagnosed by guesswork — this shows which
// car loses its gap first and what state it was in when it did, so the cause is read off the screen instead
// of inferred from the wreckage.
//
// Columns:
//   #     grid slot (P = the pace car is what it's following)
//   GAP   metres to the car it's following. "--" = it sees NOTHING ahead, which on a packed formation lap
//         is itself the bug: a blind car runs at full cruise into whatever is actually there.
//   WANT  gap the follow law is trying to hold. GAP well under WANT = it's being pushed from behind or it
//         merged in too close.
//   CLS   closing speed (mph). Positive and rising while GAP falls is the run-up to a hit.
//   CAP   commanded speed cap after every limit.
//   STATE PIT (in the lane, no gap law at all) / SETTLE (pit-out merge) / AVOID (hard braking) / ok
//
// Rows turn amber inside the panic gap and red under 3m (about a car length — contact).
// Self-installs at load; costs nothing until opened.
public class FormationDiagnostics : MonoBehaviour
{
    public static bool Open;

    const float ContactGapM = 3f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<FormationDiagnostics>() != null) return;
        var go = new GameObject("FormationDiagnostics");
        go.AddComponent<FormationDiagnostics>();
        DontDestroyOnLoad(go);
    }

    readonly List<FormationController> _sorted = new();
    GUIStyle _row, _head;

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.f8Key.wasPressedThisFrame) Open = !Open;
    }

    void OnGUI()
    {
        if (!Open) return;

        if (_row == null)
        {
            _row = new GUIStyle(GUI.skin.label) { fontSize = 11, richText = true };
            _head = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold };
        }

        _sorted.Clear();
        foreach (var fc in FormationController.Active)
            if (fc != null && fc.Spline != null) _sorted.Add(fc);
        // Front of the train first. DistanceOnTrack is a lap coordinate, so this is only exactly right while
        // the field is on one lap — which is the whole formation lap, and the only time this panel is useful.
        _sorted.Sort((a, b) => b.Spline.DistanceOnTrack.CompareTo(a.Spline.DistanceOnTrack));

        float h = Mathf.Min(Screen.height - 40f, 34f + _sorted.Count * 14f);
        GUI.Box(new Rect(8f, 8f, 430f, h), $"FORMATION  ({RaceStart.Current})   F8 closes");

        float y = 28f;
        GUI.Label(new Rect(16f, y, 420f, 14f), "  #    GAP    WANT     CLS     CAP   STATE", _head);
        y += 14f;

        for (int i = 0; i < _sorted.Count; i++)
        {
            var fc = _sorted[i];
            bool blind = fc.DbgGap < 0f;
            bool contact = !blind && fc.DbgGap < ContactGapM;
            bool panic = !blind && fc.DbgGap < fc.DbgPanicGap;

            string colour = contact ? "#ff5555" : panic ? "#ffbb44" : blind ? "#88ccff" : "#dddddd";
            string state = fc.DbgOnPit ? "PIT" : fc.DbgSettling ? "SETTLE" : fc.DbgAvoiding ? "AVOID" : "ok";
            string slot = fc.DbgPaceCarAhead ? $"{fc.Spline.qualifyingPosition}P" : fc.Spline.qualifyingPosition.ToString();
            string gap = blind ? "  --" : $"{fc.DbgGap,6:0.0}";

            GUI.Label(new Rect(16f, y, 420f, 14f),
                $"<color={colour}>{slot,4}  {gap}  {fc.DbgStationGap,6:0.0}  {fc.DbgClosingMph,6:0.0}  {fc.DbgCap,6:0.0}   {state}</color>", _row);
            y += 14f;
        }
    }
}
