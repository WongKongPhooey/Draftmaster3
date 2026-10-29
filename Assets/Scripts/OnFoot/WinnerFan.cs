using UnityEngine;
using Draftmaster.Fans;

// One of the race winner's fans (WinnerFanCrowd): after an autograph or a photo with the winner. The crowd walks
// them; this is the conversation. Walk up to any of them and talk (E) — passing the last line signs their
// programme or takes the picture, lifts fan appeal a little, and they go off happy.
//
// No OnEnable/OnDisable here: Unity would call only these, and the base's registry and bubble cleanup with them
// would quietly stop.
public class WinnerFan : NPCInteractable
{
    public bool wantsPhoto;
    [Tooltip("Fan appeal gained for an autograph — the same as a pit-lane fan's.")]
    public float appealForSigning = 1.5f;
    [Tooltip("Fan appeal gained for posing for a photo.")]
    public float appealForPhoto = 1f;

    // Set once the exchange is done; the crowd sends them home.
    public bool Satisfied { get; private set; }
    // The crowd that owns this fan, told when the photo goes off.
    public WinnerFanCrowd crowd;

    void Awake() => repeatable = false;

    public override bool Interact()
    {
        bool wasTalking = IsTalking;
        bool stillTalking = base.Interact();
        if (wasTalking && !stillTalking && !Satisfied) Done();
        return stillTalking;
    }

    void Done()
    {
        Satisfied = true;
        if (wantsPhoto)
        {
            FanAppeal.Add(appealForPhoto);
            if (crowd != null) crowd.PhotoTaken(this);
        }
        else
        {
            FanAppeal.Add(appealForSigning);
            PlayerStatsLedger.Increment("autographs");   // quest thresholds and the phone's SoBuzz feed read this
        }
        // Out of NPCInteractable.All (prompt and bubbles cleared by the base OnDisable): nobody to talk to now.
        enabled = false;
    }
}
