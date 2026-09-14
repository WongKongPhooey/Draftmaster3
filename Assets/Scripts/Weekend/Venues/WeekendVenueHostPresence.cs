using Draftmaster.Weekend;
using UnityEngine;

// A venue host who is only in the room when there is something to be in it for.
//
// Every other host stands at their venue all three days and that is right for them: the crew chief is at
// the box whether or not you want him, the official is in the drivers' room, the rep is under the awning.
// They are public places with somebody's job attached, and walking up to find nobody there would read as
// the paddock being broken.
//
// The motorhome is not a public place. It is the driver's own room, where they sleep, open the laptop and
// take an hour off, and an engineer permanently sat across the dinette turns it into an office with a man
// in it. So he keeps to the sheet: the debrief is booked at the motorhome, he is in the chair for it, and
// the rest of the weekend the RV is the player's own.
//
// Lives on the interior root rather than on the host, because the thing it switches off is the host's
// GameObject — the same switch the rest of the project uses to take somebody out of the world, since
// NPCInteractable registers and deregisters with it. The interior root is itself switched off whenever the
// player is outside, so this costs nothing while they are out in the paddock and re-decides on the way in.
public class WeekendVenueHostPresence : MonoBehaviour
{
    [Tooltip("The host whose presence is being decided.")]
    public WeekendVenueHost host;

    [Tooltip("The venue a booking has to be at for them to be here.")]
    public WeekendVenue venue = WeekendVenue.Motorhome;

    [Tooltip("Seconds between re-checks while the player is in the room. The sheet (F10) can be opened from " +
             "anywhere, so a booking can be made with the player already stood inside.")]
    public float pollSeconds = 0.25f;

    float _timer;

    // Once they have had their say they stay for the rest of the visit. Settling an obligation clears the
    // appointment, and vanishing off the chair a quarter-second after the goodbye would be the last thing
    // the player sees of the conversation.
    bool _stayForThisVisit;

    void OnEnable()
    {
        // Walking in is the moment the question is asked: a fresh visit, decided before the first frame of
        // the room is drawn, so nobody is ever glimpsed on the way out.
        _stayForThisVisit = false;
        _timer = 0f;
        Apply();
    }

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = Mathf.Max(0.05f, pollSeconds);
        Apply();
    }

    void Apply()
    {
        if (host == null) return;

        bool wanted = Wanted();
        if (wanted) _stayForThisVisit = true;
        else if (_stayForThisVisit) return;      // they are here for the rest of this visit

        var body = host.gameObject;
        if (body.activeSelf != wanted) body.SetActive(wanted);
    }

    bool Wanted()
    {
        // Never take somebody off the chair mid-sentence.
        if (host.gameObject.activeSelf && host.IsTalking) return true;

        var pending = WeekendAppointment.Pending;
        return pending != null && WeekendVenues.For(pending.kind) == venue;
    }
}
