using System.Collections.Generic;
using Draftmaster.Data;
using Draftmaster.Sponsors;
using UnityEngine;

// A sponsor's representative, stood in the pit area on a race weekend. Walk up, hear their offer, haggle,
// and sign — or find out you're not a big enough name yet.
//
// Structure copies CareerPathNPC: the base class walks a flat string[] of lines, so a branching conversation
// is built by swapping `lines` and restarting the beat, with DialogueChoiceUI taking the input between beats
// while IsTalking stays true (which keeps the player planted and the E/A prompt hidden).
//
// Signing only puts the deal in the book. It earns nothing until the decal is placed on a panel back at the
// garage — that's the whole point of the feature, so the rep says so on their way out.
public class SponsorRepNPC : NPCInteractable
{
    [Tooltip("The brand this rep works for. Set by SponsorRepSpawner from the Sponsors table.")]
    public Sponsor sponsor;

    enum Beat { None, Pitch, Reply, Signed, Rejected, Done }

    Beat _beat = Beat.None;
    bool _choiceOpen;
    int _swallowUntilFrame;

    SponsorTerms.Offer _offer;
    int _ceiling;
    int _round;                 // pushes made so far this conversation
    bool _dealtWith;            // signed or walked — they only talk terms once per weekend

    public override bool IsTalking => base.IsTalking || _choiceOpen;

    public override bool Interact()
    {
        if (Time.frameCount < _swallowUntilFrame) return true;
        if (_choiceOpen) return true;

        if (!base.IsTalking) BeginConversation();

        bool ongoing = base.Interact();
        if (ongoing) return true;

        return BeatFinished();
    }

    // The choice panel can close without answering (it cancels if its owner is disabled). Never leave the
    // player frozen in front of a question that isn't there.
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
        repeatable = true;

        if (sponsor == null)
        {
            _beat = Beat.Done;
            lines = new[] { "Sorry — wrong paddock. Ignore me." };
            return;
        }

        if (_dealtWith)
        {
            _beat = Beat.Done;
            lines = new[] { $"We're all squared away for this weekend. Good luck out there." };
            return;
        }

        if (SponsorBook.HasSponsor(sponsor.Id))
        {
            _beat = Beat.Done;
            lines = new[] { $"You're already carrying {sponsor.Name}. Go race it." };
            return;
        }

        float standing = SponsorCatalog.PlayerStanding;
        if (!SponsorTerms.CanApproach(standing, sponsor.MinPrestige))
        {
            // Not a big enough name yet — but tell them exactly what the bar is, so it reads as a target
            // rather than a locked door.
            _beat = Beat.Rejected;
            lines = new[]
            {
                $"{sponsor.Name}. And you are... yes. I know who you are.",
                "Are we doing business? #player",
                $"Not this year. We put our name on cars people can pick out of a pack.",
                "What would it take? #player",
                $"Reputation of {sponsor.MinPrestige}. You're sitting on {Mathf.RoundToInt(standing)}. " +
                "Sign a few autographs, finish a few races up front, then come and find me.",
            };
            return;
        }

        _offer = SponsorCatalog.OpeningOffer(sponsor);
        _ceiling = SponsorCatalog.Ceiling(sponsor);
        _round = 0;
        _beat = Beat.Pitch;
        lines = PitchLines();
    }

    string[] PitchLines()
    {
        var beat = new List<string>
        {
            $"{sponsor.Name}. We're looking at a car for the rest of the run.",
            "I'm listening. #player",
            $"${_offer.perRace:N0} a race, {_offer.races} races. {ClauseSentence()}",
        };
        if (SponsorBook.FreeSlots() == 0)
            beat.Add("Mind you, that car of yours hasn't a clean panel left on it. Something would have to come off.");
        return beat.ToArray();
    }

    string ClauseSentence() => _offer.clausePosition <= 0
        ? "No strings — we just want the exposure."
        : (_offer.clausePosition == 1
            ? $"Win the race and there's ${_offer.clauseBonus:N0} on top."
            : $"Finish top {_offer.clausePosition} and there's ${_offer.clauseBonus:N0} on top.");

    bool BeatFinished()
    {
        switch (_beat)
        {
            case Beat.Pitch:
            case Beat.Reply:
                AskResponse();
                return true;

            case Beat.Signed:
            case Beat.Rejected:
                _dealtWith = _beat == Beat.Signed || _dealtWith;
                _beat = Beat.Done;
                return false;

            default:
                _beat = Beat.None;
                return false;
        }
    }

    // ---------------------------------------------------------------- haggling

    static readonly SponsorTerms.Move[] kMoves =
    {
        SponsorTerms.Move.Accept,
        SponsorTerms.Move.PushGentle,
        SponsorTerms.Move.PushHard,
        SponsorTerms.Move.Shorten,
        SponsorTerms.Move.Walk,
    };

    void AskResponse()
    {
        var answers = new string[kMoves.Length];
        for (int i = 0; i < kMoves.Length; i++) answers[i] = AnswerText(kMoves[i]);

        OpenChoice($"${_offer.perRace:N0} a race for {_offer.races} races", answers, pick =>
        {
            var move = kMoves[Mathf.Clamp(pick, 0, kMoves.Length - 1)];
            var response = SponsorTerms.Respond(move, _offer, _ceiling, _round);
            _offer = response.offer;
            if (move == SponsorTerms.Move.PushGentle || move == SponsorTerms.Move.PushHard) _round++;

            if (response.signed) { Sign(response.reply, answers[Mathf.Clamp(pick, 0, answers.Length - 1)]); return; }

            if (response.walked)
            {
                _dealtWith = true;
                StartBeat(Beat.Rejected, new[]
                {
                    answers[Mathf.Clamp(pick, 0, answers.Length - 1)] + " #player",
                    response.reply,
                });
                return;
            }

            // Still talking: they countered, so the terms on the table have moved.
            StartBeat(Beat.Reply, new[]
            {
                answers[Mathf.Clamp(pick, 0, answers.Length - 1)] + " #player",
                response.reply,
            });
        });
    }

    string AnswerText(SponsorTerms.Move move) => move switch
    {
        SponsorTerms.Move.Accept => $"That'll do. ${_offer.perRace:N0} a race, {_offer.races} races",
        SponsorTerms.Move.PushGentle => $"Make it ${Mathf.RoundToInt(_offer.perRace * 1.15f):N0} and it's yours",
        SponsorTerms.Move.PushHard => $"I'd want ${Mathf.RoundToInt(_offer.perRace * 1.35f):N0} a race",
        SponsorTerms.Move.Shorten => "Shorter deal, better rate",
        _ => "Not interested",
    };

    void Sign(string reply, string spokenAnswer)
    {
        var deal = SponsorCatalog.BuildDeal(sponsor, _offer);
        SponsorBook.Sign(deal);
        _dealtWith = true;

        var beat = new List<string>
        {
            spokenAnswer + " #player",
            reply,
            "Decals will be in your garage. They pay nothing sat in a box, mind — " +
            "get them on the car and we'll both get what we're after.",
        };
        if (SponsorBook.FreeSlots() == 0)
            beat.Add("You'll be taking something else off to make room. Your problem, not mine.");

        StartBeat(Beat.Signed, beat.ToArray());
        Debug.Log($"SponsorRepNPC: signed {sponsor.Name} — ${deal.perRace:N0}/race for {deal.racesTotal} races " +
                  $"({deal.ClauseText}). Place it on a panel in the garage to start earning.", this);
    }

    void OpenChoice(string question, string[] answers, System.Action<int> picked)
    {
        _choiceOpen = true;
        DialogueChoiceUI.Open(this, question, answers, pick =>
        {
            _choiceOpen = false;
            _swallowUntilFrame = Time.frameCount + 2;   // the confirm key must not also skip the next line
            picked(pick);
        });
    }

    void StartBeat(Beat beat, string[] beatLines)
    {
        _beat = beat;
        lines = beatLines;
        repeatable = false;
        base.Interact();
    }
}
