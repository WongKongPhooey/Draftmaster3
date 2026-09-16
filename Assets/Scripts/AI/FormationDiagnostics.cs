using System.Collections.Generic;
using Draftmaster.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

// F8 overlay: one row per car on the formation lap, ordered front to back, showing what the gap law is
// actually doing. Built because the pace-lap pile-ups were being diagnosed by guesswork — this shows which
// car loses its gap first and what state it was in when it did, so the cause is read off the screen instead
// of inferred from the wreckage.
//
// Columns:
//   #     grid slot (P = the pace car is what it's following)
//   GAP   bumper-to-bumper metres to the car it's following in its lane. "--" = nothing in its lane ahead.
//   SAFE  clearance the never-hit safety law needs at this closing speed. GAP under SAFE = it is braking hard.
//   WANT  centre-to-centre gap the station keeping holds.
//   CLS   closing speed (mph). Positive and rising while GAP falls is the run-up to a hit.
//   CAP   commanded speed cap after every limit.
//   STATE PIT (in the lane) / SETTLE (pit-out merge) / FOLLOW / BRAKE (safety law acting) /
//         SWERVE (moving out round a car) / BESIDE (alongside one) / HOLD (wants to move across, lane is busy)
//
// Rows turn amber inside the safety clearance and red under half a metre (contact).
// Self-installs at load; costs nothing until opened.
public class FormationDiagnostics : MonoBehaviour
{
    public static bool Open;

    const float ContactGapM = 0.5f;

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
        GUI.Box(new Rect(8f, 8f, 490f, h), $"FORMATION  ({RaceStart.Current})   F8 closes");

        float y = 28f;
        GUI.Label(new Rect(16f, y, 480f, 14f), "  #    GAP    SAFE    WANT     CLS     CAP   STATE", _head);
        y += 14f;

        for (int i = 0; i < _sorted.Count; i++)
        {
            var fc = _sorted[i];
            bool blind = fc.DbgGap < 0f;
            bool contact = !blind && fc.DbgGap < ContactGapM;
            bool panic = !blind && fc.DbgGap < fc.DbgSafeClearance;

            string colour = contact ? "#ff5555" : panic ? "#ffbb44" : blind ? "#88ccff" : "#dddddd";
            string state = fc.DbgOnPit ? "PIT" : fc.DbgSettling ? "SETTLE" : StateName(fc.DbgMode);
            string slot = fc.DbgPaceCarAhead ? $"{fc.Spline.qualifyingPosition}P" : fc.Spline.qualifyingPosition.ToString();
            string gap = blind ? "  --" : $"{fc.DbgGap,6:0.0}";

            GUI.Label(new Rect(16f, y, 480f, 14f),
                $"<color={colour}>{slot,4}  {gap}  {fc.DbgSafeClearance,6:0.0}  {fc.DbgStationGap,6:0.0}  {fc.DbgClosingMph,6:0.0}  {fc.DbgCap,6:0.0}   {state}</color>", _row);
            y += 14f;
        }
    }

    static string StateName(PackMode mode)
    {
        switch (mode)
        {
            case PackMode.Follow: return "FOLLOW";
            case PackMode.Brake: return "BRAKE";
            case PackMode.Swerve: return "SWERVE";
            case PackMode.Alongside: return "BESIDE";
            case PackMode.Hold: return "HOLD";
            default: return "ok";
        }
    }
}
