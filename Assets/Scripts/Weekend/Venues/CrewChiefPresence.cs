using Draftmaster.Weekend;
using UnityEngine;

// One crew chief, not two.
//
// Two things stand the player's crew chief up. The weekend's pit-box host (WeekendVenueHost, venue PitBox)
// runs the strategy briefing and the rest of his bookings at the team's venue; the core cast's PlacedNPC
// crew chief stands by the car in the player's pit box and briefs the driver as they climb in. Both are
// named after whoever runs this car, so with both up the paddock had the same man in two places at once.
//
// So they take turns being him. While one of the player's own sessions is live — practice, qualifying, the
// race — he is at the pit box with the car, where the car-entry briefing needs him; the rest of the weekend
// he is at his venue, where the bookings are. Whoever is mid-sentence is never taken away, and if the pit
// box one never turned up, the venue one stays put: fewer than one of him is worse than two.
//
// They are also the same person to look at. The venue host is the one the player meets first and most, so
// his look is the man's look, and the pit-box body is dressed as him. If the chief's PlacedNPC marker carries
// an authored wardrobe, that was somebody choosing what the chief looks like, so the host wears it first.
//
// Self-installing from WeekendVenueSites, on its own object rather than either body: switching a body off
// would stop anything living on it.
public class CrewChiefPresence : MonoBehaviour
{
    public float pollSeconds = 0.25f;

    WeekendVenueHost _host;
    NPCInteractable _pitBox;
    bool _dressed;
    float _timer;

    // Which of the two should be standing, given whether the player's session is running. Pure, so the rule
    // is testable without a scene.
    public static bool PitBoxOnDuty(bool sessionLive, bool pitBoxExists) => sessionLive && pitBoxExists;

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = Mathf.Max(0.05f, pollSeconds);
        Apply();
    }

    void Apply()
    {
        if (_host == null) _host = FindPitBoxHost();
        if (_host == null) return;   // no venue host: the pit-box chief is the only one anyway

        var marker = PlacedNPC.Find(PlacedNPC.Role.CrewChief);
        if (_pitBox == null && marker != null) { _pitBox = marker.Interactable; _dressed = false; }

        if (_pitBox != null && !_dressed) _dressed = DressAlike(marker);

        bool atPitBox = PitBoxOnDuty(RaceWeekend.SessionLive, _pitBox != null);

        // Never take somebody away mid-conversation — let them finish, and swap on the next poll after.
        if (_host.gameObject.activeSelf && _host.IsTalking) atPitBox = false;
        else if (_pitBox != null && _pitBox.gameObject.activeSelf && _pitBox.IsTalking) atPitBox = true;

        SetActive(_host.gameObject, !atPitBox);
        if (_pitBox != null) SetActive(_pitBox.gameObject, atPitBox);
    }

    static void SetActive(GameObject go, bool on)
    {
        if (go.activeSelf != on) go.SetActive(on);
    }

    static WeekendVenueHost FindPitBoxHost()
    {
        foreach (var host in FindObjectsByType<WeekendVenueHost>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (host.venue == WeekendVenue.PitBox) return host;
        return null;
    }

    // The venue host's body is a paper doll on its own root (PaddockPerson); copy what it is wearing onto the
    // pit-box body. True once done, or once there is nothing to be done.
    bool DressAlike(PlacedNPC marker)
    {
        var hostLook = _host.GetComponent<NPCLayeredAppearance>();
        if (hostLook == null) return true;   // a blob with no part library: nothing to match

        var wardrobe = marker != null ? marker.GetComponent<NPCLayeredAppearance>() : null;
        if (wardrobe != null && wardrobe.useAuthoredOutfit && wardrobe.authoredOutfit != null &&
            wardrobe.authoredOutfit.Length > 0)
        {
            hostLook.useAuthoredOutfit = true;
            hostLook.authoredOutfit = wardrobe.authoredOutfit;
            hostLook.Build();
        }

        if (!hostLook.Built) return false;
        NPCFactory.DressAs(_pitBox.gameObject, hostLook, marker != null ? marker.dressedHeightM : 0f);
        return true;
    }
}
