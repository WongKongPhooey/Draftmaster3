using Draftmaster.Weekend;
using UnityEngine;

// "Optional — Find the team e-scooter": the quick way across the paddock to the Friday briefing.
//
// Offered the moment the player puts the phone away after the crew chief's "where are you?" text
// (ChiefCheckInBeat.PhonePutAway) — the chief has just said the crew is waiting, and the scooter is parked
// beside the RV the player has just walked out of (EScooterSpawner). The briefing stays the objective; this
// is an OptionalObjectives entry, so it gets its own edge-of-screen marker beside the briefing's and a line
// in the phone's Tasks app, and Tab never shows it.
//
// Over when the player steps onto the team scooter, or once the briefing stops being the booking (they
// walked it, or skipped it). Remembered across a scene load while it is open, so a reload on the walk keeps
// it; once per save, like the text that starts it.
public class ScooterErrand : MonoBehaviour
{
    public const string Id = "scooter.find";
    const string OpenKey = "errand.scooter.open";

    static ScooterErrand _instance;

    static readonly OptionalObjectives.Objective Objective = new OptionalObjectives.Objective
    {
        id = Id,
        title = "Find the team e-scooter",
        hint = "It's parked by your RV — quicker than walking to the briefing.",
        target = () => EScooterSpawner.Instance != null ? EScooterSpawner.Instance.transform : null,
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (_instance != null) return;
        var go = new GameObject("ScooterErrand");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ScooterErrand>();
    }

    void OnEnable() => ChiefCheckInBeat.PhonePutAway += Offer;
    void OnDisable() => ChiefCheckInBeat.PhonePutAway -= Offer;
    void OnDestroy() { if (_instance == this) _instance = null; }

    static bool Open
    {
        get => PlayerPrefs.GetInt(OpenKey, 0) == 1;
        set { PlayerPrefs.SetInt(OpenKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    void Offer()
    {
        if (!GameSession.CareerActive || Coop.IsGuest) return;
        if (!BriefingBooked()) return;
        if (EScooter.Ridden != null) return;   // already on one: nothing to find

        Open = true;
        OptionalObjectives.Add(Objective);
        SpawnIntroUI.Banner("OPTIONAL — FIND THE TEAM E-SCOOTER", "Quicker than walking to the briefing");
    }

    void Update()
    {
        bool open = Open;
        bool listed = OptionalObjectives.IsActive(Id);
        if (!open) { if (listed) OptionalObjectives.Remove(Id); return; }

        // Done: on the team scooter (any scooter gets them there faster, but the errand is the team's own).
        var team = EScooterSpawner.Instance;
        if (EScooter.Ridden != null && (team == null || EScooter.Ridden == team)) { Close(); return; }

        // Moot: the briefing is not what they are walking to any more.
        if (!BriefingBooked()) { Close(); return; }

        // Back after a scene load with the errand still open.
        if (!listed) OptionalObjectives.Add(Objective, pulse: false);
    }

    static bool BriefingBooked()
    {
        var booked = WeekendAppointment.Pending;
        return booked != null && booked.kind == ChiefCheckIn.Booking;
    }

    static void Close()
    {
        Open = false;
        OptionalObjectives.Remove(Id);
    }

    // Draftmaster > Demo > Re-arm The Opening, with the text that starts it.
    public static void Rearm() => Close();
}
