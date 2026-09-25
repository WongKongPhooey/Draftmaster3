using System.Collections.Generic;
using Draftmaster.Data;
using Draftmaster.Sponsors;
using UnityEngine;

// The rep who comes and finds YOU.
//
// Every other sponsorship is signed by walking up to somebody stood in the pit lane and haggling
// (SponsorRepNPC). This one is the other way round: they have watched the driver work a room, they walk
// over, they say why, and then they put finished terms on the table. There is nothing to negotiate,
// because the whole pitch is that the number already beats what is on the car — so the conversation ends
// in a term sheet (SponsorOfferPopup) with two answers on it.
//
// Structurally the same shape as SponsorRepNPC: an NPCInteractable subclass that keeps IsTalking true
// while its panel is up, so the player stays planted, the letterbox stays in and the floating E prompt
// stays hidden. SponsorPoachBeat builds one of these and walks it over.
public class SponsorPoacherNPC : NPCInteractable
{
    [Tooltip("The brand they work for. Set by SponsorPoachBeat from the Sponsors table.")]
    public Sponsor sponsor;

    [Tooltip("The best per-race rate already on the car, in money. What their offer had to beat.")]
    public int beatRate;

    // The terms they arrived with. Built once by the beat, so what they SAY and what the paper says can
    // never disagree.
    [System.NonSerialized] public SponsorTerms.Offer offer;

    bool _paperOut;        // the term sheet is on screen
    bool _answered;        // it has been signed or turned down; they only ask once
    int _swallowUntilFrame;

    public override bool IsTalking => base.IsTalking || _paperOut;

    public override bool Interact()
    {
        // The key that answered the popup is read by the on-foot controller too — it must not also skip
        // the line the answer is about to speak.
        if (Time.frameCount < _swallowUntilFrame) return true;
        if (_paperOut) return true;

        bool ongoing = base.Interact();
        if (ongoing) return true;

        // The pitch is over. Out comes the paper — once. After that, this is an ordinary NPC with a
        // parting line.
        if (!_answered && sponsor != null)
        {
            ShowTerms();
            return true;
        }
        return false;
    }

    // The popup closes itself if its owner goes away, and the player can walk out of a conversation.
    // Never leave them frozen in front of a sheet that is not there.
    //
    // (No OnDisable here on purpose: NPCInteractable's own OnDisable is what takes this speaker out of
    // the All list, and a same-named method on a subclass is the one Unity would call instead.)
    void Update()
    {
        if (!_paperOut) return;
        if (SponsorOfferPopup.IsOpen && SponsorOfferPopup.Owner == this) return;
        _paperOut = false;
        EndConversation();
    }

    // ---------------------------------------------------------------- the term sheet

    void ShowTerms()
    {
        _paperOut = true;

        var deal = SponsorPoach.Deal(sponsor.Id, sponsor.Name, SponsorCatalog.LogoKey(sponsor.Name), offer);
        var terms = new List<string>
        {
            $"Term          {deal.racesTotal} races",
            $"Retainer      ${deal.perRace:N0} per race",
            $"Bonus         ${deal.targetBonus:N0} — finish top {deal.targetPosition} " +
            $"at least {(deal.targetCount == 2 ? "twice" : deal.targetCount + " times")} during the contract",
        };
        if (beatRate > 0)
            terms.Add($"Currently     ${beatRate:N0} per race on your best panel");

        SponsorOfferPopup.Show(this, sponsor.Name.ToUpperInvariant(), "Term sheet — offered in the paddock",
                               terms.ToArray(),
                               "Signing is half the job. A sponsor pays nothing until its decal is on a panel.",
                               Answered);
    }

    void Answered(bool signed)
    {
        _paperOut = false;
        _answered = true;
        _swallowUntilFrame = Time.frameCount + 2;

        if (signed)
        {
            var deal = SponsorPoach.Deal(sponsor.Id, sponsor.Name, SponsorCatalog.LogoKey(sponsor.Name), offer);
            SponsorBook.Sign(deal);
            Say(new[]
            {
                "Good. My people will have the decals with your garage by tonight.",
                "Get them on the car, mind — a sponsor on a shelf pays nobody. " +
                $"And I want that top {deal.targetPosition} twice before this runs out.",
            });
        }
        else
        {
            Say(new[]
            {
                "Loyal. I can respect that — for about a season.",
                "The offer is on the table until somebody else takes it. You know where we are.",
            });
        }
    }

    // Speak a fresh run of lines straight after the panel closes, the way the haggling NPC swaps beats.
    void Say(string[] said)
    {
        lines = said;
        repeatable = true;
        SetInteractor(OnFootController.Current != null ? OnFootController.Current.transform : null);
        base.Interact();
    }
}
