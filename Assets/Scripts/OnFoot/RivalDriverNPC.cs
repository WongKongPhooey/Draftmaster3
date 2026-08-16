using UnityEngine;
using Draftmaster.Fights;

// A driver you can talk to in the paddock — and, if the two of you have history, square up to.
//
// Whether the option appears is not a scene setting: it's the pair's DriverRelationships score, which is
// driven by what actually happened on track (VehicleCollision reports contact, AI paybacks make it worse,
// clean races heal it). Below DriverRelationships.RivalThreshold this driver greets the player with an
// argument instead of small talk, and the conversation ends on a choice: square up, or let it go.
//
// Structure follows CareerPathNPC: the base class walks a flat string[] per beat, and this owns the order
// the beats run in, handing off to DialogueChoiceUI between them.
public class RivalDriverNPC : NPCInteractable
{
    [Header("Identity")]
    [Tooltip("Name this driver's relationships are stored under (their DriverLabel name on track). Falls back to the speaker name.")]
    public string driverName;
    [Tooltip("Roster Aggression (1-20). Scales how hard and how often they swing.")]
    public int aggression = 10;

    [Header("Fighting")]
    [Tooltip("Offer the fight option at all. Off = a rival will still argue with you, but it never goes further.")]
    public bool allowFights = true;

    [Header("Rival dialogue")]
    [TextArea]
    [Tooltip("Spoken instead of the friendly lines once the pair are rivals. A line ending with \"#player\" is spoken by the player.")]
    public string[] rivalLines =
    {
        "You've got some front, walking over here.",
        "We should talk about the last one. #player",
        "We talked about it on the track. You put me in the wall and drove off.",
        "That was racing. #player",
        "That was you not lifting. Say it again and see what happens.",
    };
    [Tooltip("Header shown above the choice. Empty = no header.")]
    public string challengeQuestion = "They've squared up to you.";
    [Tooltip("The answers. First = start the fight, second = walk away.")]
    public string[] challengeAnswers = { "Square up to them", "Let it go" };
    [TextArea]
    [Tooltip("Spoken as it kicks off. The fight starts when these run out.")]
    public string[] squareUpLines =
    {
        "Say it again then. #player",
        "Gladly.",
    };
    [TextArea]
    [Tooltip("Spoken when the player backs off.")]
    public string[] backDownLines =
    {
        "Not worth it. #player",
        "No. It isn't. Save it for Sunday.",
    };
    [TextArea]
    [Tooltip("Spoken by a rival who's already had this out with you and is still cooling off (the fight cooldown).")]
    public string[] cooledOffLines =
    {
        "We're done for today. Go on.",
    };

    enum Beat { None, Normal, RivalArgument, SquareUp, BackDown, CooledOff }

    Beat _beat = Beat.None;
    bool _choiceOpen;
    int _swallowUntilFrame;
    string[] _friendlyLines;      // whatever `lines` held before a rival beat swapped it out

    // The name this driver's relationship scores are filed under.
    public string Identity => string.IsNullOrEmpty(driverName) ? speakerName : driverName;

    // Current relationship score with the player, -100..100. Keyed on DriverRelationships.PlayerName (the
    // player's racing identity), NOT on the name shown in dialogue — the two differ before the player has
    // a career name, and using the wrong one reads an empty relationship.
    public float RelationshipScore => DriverRelationships.Get(DriverRelationships.PlayerName, Identity);

    // Angry enough to argue — and, if fights are allowed, to swing.
    public bool IsRival => FightRules.CanChallenge(RelationshipScore, DriverRelationships.RivalThreshold);

    // Stay "talking" while the choice panel is up, so the player stays planted and the E prompt stays hidden.
    public override bool IsTalking => base.IsTalking || _choiceOpen;

    void Awake()
    {
        _friendlyLines = lines;
    }

    public override bool Interact()
    {
        if (Time.frameCount < _swallowUntilFrame) return true;
        if (_choiceOpen) return true;
        if (DriverFight.IsActive) return false;      // nobody chats mid-scrap

        if (!base.IsTalking) BeginConversation();

        bool ongoing = base.Interact();
        if (ongoing) return true;

        return BeatFinished();
    }

    // The panel can close without answering (its owner was disabled mid-question) — don't leave the player
    // frozen in front of a question that isn't there.
    void Update()
    {
        if (!_choiceOpen) return;
        if (DialogueChoiceUI.IsOpen && DialogueChoiceUI.Owner == this) return;

        _choiceOpen = false;
        _beat = Beat.None;
        EndConversation();
    }

    void BeginConversation()
    {
        if (IsRival && DriverFight.OnCooldown && cooledOffLines != null && cooledOffLines.Length > 0)
        {
            _beat = Beat.CooledOff;
            lines = cooledOffLines;
            repeatable = true;
            return;
        }

        if (IsRival && rivalLines != null && rivalLines.Length > 0)
        {
            _beat = Beat.RivalArgument;
            lines = rivalLines;
            repeatable = true;   // the argument is still there next time you walk over
            return;
        }

        _beat = Beat.Normal;
        if (_friendlyLines != null && _friendlyLines.Length > 0) lines = _friendlyLines;
        repeatable = true;
    }

    // A beat's lines have run out: open the choice, start the fight, or let the conversation end.
    bool BeatFinished()
    {
        switch (_beat)
        {
            case Beat.RivalArgument:
                if (CanOfferFight())
                {
                    AskChallenge();
                    return true;
                }
                _beat = Beat.None;
                return false;

            case Beat.SquareUp:
                _beat = Beat.None;
                EndConversation();
                StartFight();
                return false;

            default:
                _beat = Beat.None;
                return false;
        }
    }

    bool CanOfferFight()
    {
        if (!allowFights || !IsRival) return false;
        if (DriverFight.IsActive || DriverFight.OnCooldown) return false;
        if (Interactor == null) return false;                       // nobody to fight
        if (challengeAnswers == null || challengeAnswers.Length < 2) return false;
        return true;
    }

    void AskChallenge()
    {
        _choiceOpen = true;
        DialogueChoiceUI.Open(this, challengeQuestion, challengeAnswers, pick =>
        {
            _choiceOpen = false;
            // The key that answered the choice must not also skip the line this is about to speak.
            _swallowUntilFrame = Time.frameCount + 2;
            if (pick == 0) StartBeat(Beat.SquareUp, squareUpLines);
            else StartBeat(Beat.BackDown, backDownLines);
        });
    }

    void StartBeat(Beat beat, string[] beatLines)
    {
        if (beatLines == null || beatLines.Length == 0)
        {
            // Nothing to say for this branch — go straight to what it meant.
            if (beat == Beat.SquareUp) { EndConversation(); StartFight(); }
            else EndConversation();
            _beat = Beat.None;
            return;
        }
        _beat = beat;
        lines = beatLines;
        repeatable = false;
        base.Interact();   // the base class is idle between beats, so this speaks the first line
    }

    void StartFight()
    {
        var player = Interactor;
        if (player == null)
        {
            Debug.LogWarning($"RivalDriverNPC ({Identity}): no interactor — nobody to fight.", this);
            return;
        }
        DriverFight.Begin(player.gameObject, gameObject, Identity, aggression);
    }
}
