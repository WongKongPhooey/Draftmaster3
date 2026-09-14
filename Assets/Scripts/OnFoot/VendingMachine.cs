using UnityEngine;
using Draftmaster.Progression;

// The drinks machine stood at the side of the paddock grandstand. Walk up, press the action button, and a
// popup shows what is in it this weekend; pick a can and drink it.
//
// What is in the machine is restocked every race weekend and every can is a trade — it raises one career
// attribute and takes the same amount off another, until the trucks roll out on Sunday night. The rules and
// the expiry live in WeekendDrinks; this is the thing in the paddock that offers them.
//
// Built like CareerPathNPC: an NPCInteractable subclass so the on-foot proximity prompt and action button
// work unchanged, with the choice panel (DialogueChoiceUI) taking the input between lines while IsTalking
// stays true — which keeps the player planted in front of the machine and hides the floating E/A prompt.
public class VendingMachine : NPCInteractable
{
    [Tooltip("Header above the list of cans.")]
    public string question = "PADDOCK REFRESHMENTS - one per weekend";
    [Tooltip("The last row of the popup: walk away with nothing.")]
    public string leaveOption = "Nothing for me";
    [TextArea]
    [Tooltip("Said when the player walks away without taking anything.")]
    public string[] leaveLines = { "The compressor kicks in behind the glass and the machine goes back to humming." };
    [TextArea]
    [Tooltip("Said when the machine has already been used this weekend. \"{effect}\" becomes what the can is " +
             "still doing (\"+2 Driving / -2 Business\"), \"{name}\" the can that was taken.")]
    public string[] alreadyTakenLines =
    {
        "You already had a {name} out of here this weekend. Still tasting it, frankly.",
        "It is worth {effect} until the trucks roll out on Sunday night.",
    };
    [Tooltip("Announce what the can did in plain numbers after drinking it. Off = keep it in-fiction.")]
    public bool announceEffect = true;

    bool _choiceOpen;
    int _swallowUntilFrame;                 // interact presses ignored up to this frame (the press that answered)
    WeekendDrinks.Drink[] _offered;         // this weekend's rack, in the order it was listed

    // Stay "talking" while the panel is up, so OnFootController keeps the player where they are.
    public override bool IsTalking => base.IsTalking || _choiceOpen;

    public override bool Interact()
    {
        // The keypress that answered the popup must not also skip the line this is about to speak.
        if (Time.frameCount < _swallowUntilFrame) return true;
        if (_choiceOpen) return true;                       // the panel owns the input until it is answered

        if (base.IsTalking) return base.Interact();         // mid-line: advance it

        int weekend = RaceWeekend.WeekendId;

        if (WeekendDrinks.TryGetTaken(weekend, out var taken))
            return Speak(Fill(alreadyTakenLines, taken));

        _offered = WeekendDrinks.Selection(weekend);
        if (_offered == null || _offered.Length == 0)
            return Speak(new[] { "Empty. Somebody got here first." });

        var options = new string[_offered.Length + 1];
        for (int i = 0; i < _offered.Length; i++) options[i] = WeekendDrinks.Label(_offered[i]);
        options[_offered.Length] = leaveOption;

        _choiceOpen = true;
        DialogueChoiceUI.Open(this, question, options, Picked);
        return true;
    }

    // The panel can close without answering (it cancels itself if its owner is disabled or destroyed mid
    // question). Never leave the player stood frozen in front of a popup that is not there.
    void Update()
    {
        if (!_choiceOpen) return;
        if (DialogueChoiceUI.IsOpen && DialogueChoiceUI.Owner == this) return;

        _choiceOpen = false;
        EndConversation();
    }

    void Picked(int pick)
    {
        _choiceOpen = false;
        // The confirm key is very likely E/Space, which the controller reads too — swallow it for a couple
        // of frames so it cannot skip the line this is about to speak.
        _swallowUntilFrame = Time.frameCount + 2;

        // The last row, a cancelled panel, or a pick that makes no sense: nothing taken.
        if (_offered == null || pick < 0 || pick >= _offered.Length) { Speak(leaveLines); return; }

        int weekend = RaceWeekend.WeekendId;
        var drink = _offered[pick];

        if (!WeekendDrinks.Take(weekend, pick))
        {
            // Only reachable if a can was taken while the panel was open (a second machine, a co-op push).
            if (WeekendDrinks.TryGetTaken(weekend, out var already)) Speak(Fill(alreadyTakenLines, already));
            return;
        }

        Debug.Log($"VendingMachine: took {drink.name} ({WeekendDrinks.Effect(drink)}) for weekend {weekend}.", this);

        var said = new System.Collections.Generic.List<string> { drink.flavour + " #player" };
        if (announceEffect)
            said.Add($"{WeekendDrinks.Effect(drink)}, until the trucks roll out on Sunday night. #player");
        Speak(said.ToArray());
    }

    // Start a short piece of talking on the machine's own bubble. Returns true so the caller stays engaged.
    bool Speak(string[] said)
    {
        if (said == null || said.Length == 0) return false;
        lines = said;
        repeatable = false;             // each of these is said once, then the conversation ends
        return base.Interact();
    }

    static string[] Fill(string[] source, WeekendDrinks.Drink drink)
    {
        if (source == null) return new string[0];
        var copy = new string[source.Length];
        for (int i = 0; i < source.Length; i++)
            copy[i] = (source[i] ?? "").Replace("{name}", drink.name)
                                      .Replace("{effect}", WeekendDrinks.Effect(drink));
        return copy;
    }
}
