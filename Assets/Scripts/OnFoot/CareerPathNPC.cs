using UnityEngine;
using Draftmaster.Progression;

// The paddock old-hand who opens a career. He asks two questions — "do you like racing?", then "what do you
// want to be when you grow up?" — and the second answer sets the career's starting premise: it seeds the
// player's career stats (CareerPath.StartingStats) and becomes the gate other NPCs read when deciding which
// opportunities to offer (AppearanceConditions.careerPaths).
//
// In the demo he's simply stood in the paddock, out of context, so the beat is playable before career mode
// exists (installed by CareerPathNPCSpawner). Once the choice is made he remembers it and just chats.
//
// Structure: the base class walks a flat string[] of lines, so a branching conversation is built by swapping
// `lines` and restarting it per beat. Between beats the choice panel takes over the input (DialogueChoiceUI)
// while IsTalking stays true, which keeps the player planted and the E/A prompt hidden.
public class CareerPathNPC : NPCInteractable
{
    [Header("Beat 1 — do you like racing?")]
    [TextArea]
    [Tooltip("Opening lines. The last one should be the question the first choice answers. A line ending with \"#player\" is spoken by the player.")]
    public string[] introLines =
    {
        "Well now. You've been stood at that fence since the trucks rolled in this morning.",
        "I didn't want to miss anything. #player",
        "Nothing wrong with that. Let me ask you something, then.",
        "Do you like racing?",
    };
    [Tooltip("Question header shown above the first set of answers.")]
    public string likeRacingQuestion = "Do you like racing?";
    [Tooltip("The two answers to the first question. Flavour only — neither one changes your stats.")]
    public string[] likeRacingAnswers =
    {
        "I love it. It's all I've ever thought about",
        "I like the noise. I don't really understand the rest yet",
    };
    [TextArea]
    [Tooltip("His reply if the player says they love it. The last line should lead into the big question.")]
    public string[] lovesItLines =
    {
        "Thought so. You've got that look — same one I had at your age, and it never did wear off.",
        "Then here's the one that matters, and I want a straight answer.",
    };
    [TextArea]
    [Tooltip("His reply if the player is unsure. The last line should lead into the big question.")]
    public string[] unsureLines =
    {
        "Honest answer. Better than honest, actually — most folk out here are still working the rest out too.",
        "So let me put the one that matters to you, and take your time with it.",
    };

    [Header("Beat 2 — what do you want to be?")]
    [Tooltip("Question header shown above the four career answers.")]
    public string ambitionQuestion = "So — what do you want to be when you grow up?";
    [TextArea]
    [Tooltip("Spoken after the choice lands, before the path-specific reply. \"{path}\" is replaced with the chosen role.")]
    public string[] choiceMadeLines =
    {
        "Then that's what we'll make of you.",
    };
    [Tooltip("Say what the choice actually did to the player's starting stats. Off = keep it purely in-fiction.")]
    public bool announceStartingStats = true;

    [Header("Afterwards")]
    [TextArea]
    [Tooltip("Lines used on every later conversation, once a path has been chosen. \"{path}\" is replaced with the chosen role.")]
    public string[] alreadyChosenLines =
    {
        "Still set on the {path} plan, then?",
        "Still set on it. #player",
        "Good. Come find me when the world tries to talk you out of it.",
    };

    // Which beat the conversation is on. The base class owns the lines within a beat; this owns the order
    // the beats run in.
    enum Beat { None, Intro, LikeReply, PathReply, Chat }

    Beat _beat = Beat.None;
    bool _choiceOpen;
    int _swallowUntilFrame;      // interact presses ignored up to this frame (the press that answered a choice)
    CareerPath.Path[] _offered;  // paths in the order they were listed, so the pick index maps back

    // Stay "talking" while the choice panel is up: OnFootController reads this to keep the player planted and
    // to hide the floating E/A prompt.
    public override bool IsTalking => base.IsTalking || _choiceOpen;

    public override bool Interact()
    {
        // The keypress that answered a choice must not also advance the line it opened.
        if (Time.frameCount < _swallowUntilFrame) return true;
        if (_choiceOpen) return true;                       // the panel owns the input until it's answered

        if (!base.IsTalking) BeginConversation();

        bool ongoing = base.Interact();
        if (ongoing) return true;

        return BeatFinished();
    }

    // The panel can close without answering (it cancels itself if its owner is disabled or destroyed mid
    // question). Never leave the player standing frozen in front of a question that isn't there.
    void Update()
    {
        if (!_choiceOpen) return;
        if (DialogueChoiceUI.IsOpen && DialogueChoiceUI.Owner == this) return;

        _choiceOpen = false;
        _beat = Beat.None;
        EndConversation();
    }

    // Fresh conversation: the opening interview if the player has never been asked, otherwise small talk.
    void BeginConversation()
    {
        if (CareerPath.HasChosen)
        {
            _beat = Beat.Chat;
            lines = Fill(alreadyChosenLines);
            repeatable = true;
        }
        else
        {
            _beat = Beat.Intro;
            lines = introLines;
            repeatable = false;   // the interview runs once through, not on a loop
        }
    }

    // A beat's lines have run out. Either open the next question, start the next beat, or let the
    // conversation end (false = the controller releases the player).
    bool BeatFinished()
    {
        switch (_beat)
        {
            case Beat.Intro:
                AskLikeRacing();
                return true;

            case Beat.LikeReply:
                AskAmbition();
                return true;

            case Beat.PathReply:
                _beat = Beat.Chat;
                return false;

            default:
                _beat = Beat.None;
                return false;
        }
    }

    // ---------------------------------------------------------------- questions

    void AskLikeRacing()
    {
        string[] answers = likeRacingAnswers != null && likeRacingAnswers.Length >= 2
            ? likeRacingAnswers
            : new[] { "Yes", "Not sure yet" };

        OpenChoice(likeRacingQuestion, answers, pick =>
        {
            // Flavour: which reply he gives. The answer itself carries no mechanical weight.
            bool loves = pick == 0;
            var reply = loves ? lovesItLines : unsureLines;
            StartBeat(Beat.LikeReply, Prepend(answers[Mathf.Clamp(pick, 0, answers.Length - 1)] + " #player", reply));
        });
    }

    void AskAmbition()
    {
        _offered = CareerPath.Choices;
        var answers = new string[_offered.Length];
        for (int i = 0; i < _offered.Length; i++) answers[i] = CareerPath.Ambition(_offered[i]);

        OpenChoice(ambitionQuestion, answers, pick =>
        {
            var path = _offered[Mathf.Clamp(pick, 0, _offered.Length - 1)];
            Commit(path);
            StartBeat(Beat.PathReply, BuildPathReply(path, answers[Mathf.Clamp(pick, 0, answers.Length - 1)]));
        });
    }

    void OpenChoice(string question, string[] answers, System.Action<int> picked)
    {
        _choiceOpen = true;
        DialogueChoiceUI.Open(this, question, answers, pick =>
        {
            _choiceOpen = false;
            // The confirm key is very likely E/Space, which the controller reads too — swallow it for a
            // couple of frames so it can't skip the line this callback is about to speak.
            _swallowUntilFrame = Time.frameCount + 2;
            picked(pick);
        });
    }

    // The choice landed: persist it and pay out the starting stats (once per save, enforced by CareerPath).
    void Commit(CareerPath.Path path)
    {
        if (!CareerPath.Choose(path))
        {
            Debug.LogWarning($"CareerPathNPC: career path already set to {CareerPath.Current} — {path} not applied.", this);
            return;
        }

        // Career stats are ordinary ledger counters, so any quest watching one should re-check now.
        QuestManager.ReevaluateStatObjectives();
        Debug.Log($"CareerPathNPC: career path set to {CareerPath.DisplayName(path)}. Starting stats: {StatSummary(path)}.", this);
    }

    // His answer to the chosen path, opening with the player's own words so the bubble reads as a reply.
    string[] BuildPathReply(CareerPath.Path path, string spokenAnswer)
    {
        var beat = new System.Collections.Generic.List<string> { spokenAnswer + " #player" };
        beat.AddRange(PathLines(path));
        foreach (var line in Fill(choiceMadeLines)) beat.Add(line);
        if (announceStartingStats) beat.Add($"You'll start out with what that gets you: {StatSummary(path)}.");
        return beat.ToArray();
    }

    // Per-path reaction. Written here rather than as four inspector arrays so a designer only has to edit
    // the shared beats above; each path gets its own three lines with a player reply in the middle.
    static string[] PathLines(CareerPath.Path path)
    {
        switch (path)
        {
            case CareerPath.Path.PitCrew:
                return new[]
                {
                    "Over the wall. Good — that's the half of racing nobody claps for and nobody wins without.",
                    "Is it hard to get on a crew? #player",
                    "Hard as anything. Twelve seconds, four tyres, and a hundred thousand people watching you fumble one.",
                };

            case CareerPath.Path.Driver:
                return new[]
                {
                    "A champion. Course you do. Every kid at that fence says it and about one in ten thousand means it.",
                    "I mean it. #player",
                    "Then say it slower and louder for the rest of your life, and get in anything with a wheel.",
                };

            case CareerPath.Path.TeamOwner:
                return new[]
                {
                    "Your own team. Now that's a rarer answer, and a harder road than driving one.",
                    "Why harder? #player",
                    "Because a driver only has to be fast. You'd have to make payroll on a bad Sunday.",
                };

            case CareerPath.Path.Scout:
                return new[]
                {
                    "You want to find them. Huh. Nobody ever says that one.",
                    "Somebody has to spot them first, don't they? #player",
                    "They do, and the somebodies get forgotten. But you'll see them coming years before the cameras do.",
                };

            default:
                return new[] { "Take your time with it. The answer keeps." };
        }
    }

    // "+9 driving, +2 pit craft, ..." — reads the same table that was actually paid out.
    static string StatSummary(CareerPath.Path path)
    {
        var parts = new System.Collections.Generic.List<string>();
        foreach (var grant in CareerPath.StartingStats(path))
        {
            if (grant.value == 0) continue;
            parts.Add($"+{grant.value} {Pretty(grant.key)}");
        }
        return parts.Count > 0 ? string.Join(", ", parts) : "nothing much yet";
    }

    // "career.pitcraft" -> "pit craft"
    static string Pretty(string statKey)
    {
        if (statKey == CareerPath.StatPitCraft) return "pit craft";
        int dot = statKey.LastIndexOf('.');
        return dot >= 0 && dot < statKey.Length - 1 ? statKey.Substring(dot + 1) : statKey;
    }

    // ---------------------------------------------------------------- beat plumbing

    // Start the next beat immediately: the base class is idle between beats, so handing it a fresh line set
    // and calling Interact() speaks the first line of it.
    void StartBeat(Beat beat, string[] beatLines)
    {
        _beat = beat;
        lines = beatLines;
        repeatable = false;
        base.Interact();
    }

    string[] Fill(string[] source)
    {
        if (source == null) return new string[0];
        string path = CareerPath.DisplayName(CareerPath.Current);
        var copy = new string[source.Length];
        for (int i = 0; i < source.Length; i++)
            copy[i] = source[i] == null ? "" : source[i].Replace("{path}", path);
        return copy;
    }

    static string[] Prepend(string first, string[] rest)
    {
        int n = rest != null ? rest.Length : 0;
        var all = new string[n + 1];
        all[0] = first;
        for (int i = 0; i < n; i++) all[i + 1] = rest[i];
        return all;
    }
}
