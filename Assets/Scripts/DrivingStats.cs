using UnityEngine;

// DrivingStats watches the player's car during a race and turns the act of
// driving into persistent character stats. It accumulates distance, top speed
// and time spent in the draft each frame, then - once the race ends - banks
// those into the database via StatsManager and reports the race to QuestManager
// so driving-based side-quests (e.g. "cover 8km") make progress.
//
// Drop this component on the same object as Movement (the player car) or on a
// race manager object; it finds everything it needs from the Movement statics.
public class DrivingStats : MonoBehaviour
{
    [Header("Stat tuning")]
    [Tooltip("Metres driven needed to earn one point of Endurance.")]
    [SerializeField] private float metresPerEndurancePoint = 1000f;

    [Tooltip("Metres spent in the draft needed to earn one point of Drafting.")]
    [SerializeField] private float metresPerDraftingPoint = 750f;

    [Tooltip("Speedo reading (per the HUD) above which a Pace point is earned.")]
    [SerializeField] private float paceSpeedThreshold = 210f;

    private float metresDriven;
    private float metresDrafted;
    private float topSpeed;
    private bool committed;

    void Start(){
        // Guarantee the stat/quest tables exist even if no DatabaseManager
        // component happens to be in the scene.
        DatabaseManager.EnsureSchema();
        ResetSession();
    }

    private void ResetSession(){
        metresDriven = 0f;
        metresDrafted = 0f;
        topSpeed = 0f;
        committed = false;
    }

    void FixedUpdate(){
        // The race is over - bank the session exactly once.
        if(RaceHUD.raceOver == true){
            if(committed == false){
                CommitSession();
            }
            return;
        }

        // Don't count laps run behind the pace car or while wrecking.
        if(Movement.pacing == true || Movement.isWrecking == true || Movement.wreckOver == true){
            return;
        }

        // playerSpeedMetres is metres/second; integrate it over the frame.
        float metresThisFrame = Movement.playerSpeedMetres * Time.fixedDeltaTime;
        if(metresThisFrame > 0f){
            metresDriven += metresThisFrame;

            // draftPercent is the fraction (0-1) of the slipstream currently
            // being used; weight the distance by it to credit time drafting.
            if(Movement.draftPercent > 0f){
                metresDrafted += metresThisFrame * Mathf.Clamp01(Movement.draftPercent);
            }
        }

        if(Movement.speedoSpeed > topSpeed){
            topSpeed = Movement.speedoSpeed;
        }
    }

    // Converts the session's driving into stat points, banks them and lets the
    // quest system know a race finished. Safe to call only once per race.
    private void CommitSession(){
        committed = true;

        int endurance = Mathf.FloorToInt(metresDriven / metresPerEndurancePoint);
        int drafting  = Mathf.FloorToInt(metresDrafted / metresPerDraftingPoint);
        int pace      = topSpeed >= paceSpeedThreshold ? Mathf.FloorToInt(topSpeed - paceSpeedThreshold) + 1 : 0;

        if(endurance > 0){
            StatsManager.GainStat(StatsManager.Endurance, endurance);
        }
        if(drafting > 0){
            StatsManager.GainStat(StatsManager.Drafting, drafting);
        }
        if(pace > 0){
            StatsManager.GainStat(StatsManager.Pace, pace);
        }

        // Feed driving-based side-quests.
        int metresInt = Mathf.FloorToInt(metresDriven);
        if(metresInt > 0){
            QuestManager.ReportEvent("drive_distance", metresInt);
        }
        QuestManager.ReportEvent("race_finish", 1);

        Debug.Log("[DrivingStats] Banked race: " + metresInt + "m driven, top speed " +
                  topSpeed.ToString("F0") + " (+" + endurance + " Endurance, +" +
                  drafting + " Drafting, +" + pace + " Pace)");
    }
}
