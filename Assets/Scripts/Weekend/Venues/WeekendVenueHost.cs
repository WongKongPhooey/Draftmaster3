using System.Collections.Generic;
using Draftmaster.Weekend;
using UnityEngine;

// The person waiting for you at a venue: the crew chief at the pit box, the engineer in the motorhome, the
// official at the top table, the sponsor's rep under the awning, the fan at the fence.
//
// This is where a weekend obligation actually happens now. Walk up, press the action button, and the whole
// thing plays out in the world — their line over their head, your answers in a list, their reply, and the
// meters move when it ends. No panel, no frozen world: the crowd keeps moving, the cars on track are still
// audible, and the player can walk away mid-sentence, which counts as not having done it.
//
// Structurally this is the CareerPathNPC pattern: an NPCInteractable subclass that swaps `lines` per beat
// and hands the input to DialogueChoiceUI in between, keeping IsTalking true so the player stays planted
// and the floating prompt stays hidden.
public class WeekendVenueHost : NPCInteractable
{
    [Tooltip("The venue this host runs. Only bookings kept here will start on them.")]
    public WeekendVenue venue = WeekendVenue.PitBox;

    [Tooltip("What they say when there is nothing booked with them right now.")]
    [TextArea]
    public string[] idleLines = { "Nothing from me right now. Check your schedule." };

    WeekendActivity _activity;
    WeekendConversation _script;
    int _beat;
    bool _choiceOpen;
    int _swallowUntilFrame;
    WeekendOutcome _running;
    int _answered;
    bool _finished;

    // The signing session is worked along a line (SigningQueue): between fans the player is walked to the
    // next place at the rail and turned to face whoever is stood there, and that fan speaks their beat.
    SigningQueue _queue;
    bool _walkNext;           // the lines now playing end with a walk to the next fan
    bool _walking;            // the player is being walked there
    float _walkGiveUpAt;      // somebody in the way: stop trying and sign from where they got to
    OnFootController _walker;

    public override bool IsTalking => base.IsTalking || _choiceOpen || _walking;

    // Whether this host has something to run right now: an appointment, made for this venue, whose window
    // the weekend has reached.
    public bool HasBusiness
    {
        get
        {
            var pending = WeekendAppointment.Pending;
            return pending != null && WeekendVenues.For(pending.kind) == venue;
        }
    }

    public override bool Interact()
    {
        if (Time.frameCount < _swallowUntilFrame) return true;
        if (_choiceOpen || _walking) return true;

        if (!base.IsTalking)
        {
            if (!StartBusiness()) { lines = idleLines; repeatable = true; }
        }

        bool ongoing = base.Interact();
        if (ongoing) return true;

        return BeatFinished();
    }

    // The choice panel can close without answering — its owner being destroyed, or the player walking out
    // of the conversation. Never leave them planted in front of a question that is not there.
    void Update()
    {
        if (_walking) { UpdateWalk(); return; }
        if (!_choiceOpen) return;
        if (DialogueChoiceUI.IsOpen && DialogueChoiceUI.Owner == this) return;

        _choiceOpen = false;
        AbandonConversation();
    }

    // ------------------------------------------------------------------ the obligation

    bool StartBusiness()
    {
        var pending = WeekendAppointment.Pending;
        if (pending == null || WeekendVenues.For(pending.kind) != venue) return false;

        _activity = pending;
        _script = WeekendScripts.For(pending);
        if (_script == null || _script.beats.Count == 0) { _activity = null; return false; }

        _beat = 0;
        _answered = 0;
        _finished = false;
        _running = WeekendOutcome.Nothing;
        _running.score = 0f;
        voice = null;

        // A queue at the fence is walked: the greeting is the liaison's, then the player goes to the first
        // fan in the line. Anywhere else, the greeting runs straight into the first question.
        _queue = venue == WeekendVenue.SigningFence ? SigningQueue.Current : null;
        if (_queue != null && _queue.Count == 0) _queue = null;
        _walkNext = _queue != null;
        _walking = false;

        lines = _walkNext ? Lines(_script.greeting) : Lines(_script.greeting, BeatLines(_script.beats[0]));
        repeatable = false;
        return true;
    }

    // A run of lines finished. Either put the question that follows them up, or close the whole thing out.
    bool BeatFinished()
    {
        if (_activity == null || _script == null) return false;   // idle chat: nothing to continue
        if (_finished) { EndBusiness(); return false; }
        if (_walkNext) { _walkNext = false; WalkToFan(); return true; }

        var beat = _script.beats[Mathf.Clamp(_beat, 0, _script.beats.Count - 1)];
        if (beat.choices == null || beat.choices.Count == 0) { Advance(); return true; }

        var options = new string[beat.choices.Count];
        for (int i = 0; i < beat.choices.Count; i++) options[i] = beat.choices[i].text;

        _choiceOpen = true;
        DialogueChoiceUI.Open(this, beat.Question, options, Picked);
        return true;   // still talking: the panel owns the input now
    }

    void Picked(int index)
    {
        _choiceOpen = false;
        // The confirm key is E/Space, which the on-foot controller reads too — swallow it for a couple of
        // frames so it cannot also skip the reply this is about to speak.
        _swallowUntilFrame = Time.frameCount + 2;

        var beat = _script.beats[Mathf.Clamp(_beat, 0, _script.beats.Count - 1)];
        var choice = beat.choices[Mathf.Clamp(index, 0, beat.choices.Count - 1)];

        WeekendConversation.Accumulate(ref _running, choice);
        _answered++;

        bool last = _script.Ends(_beat, choice, _running.minutesSpent);
        // An obligation on a clock can run out of window with people still queued up — that is a different
        // goodbye from having got to the end of them, and the content says so in its own words.
        bool timeUp = last && !choice.ends && _beat < _script.beats.Count - 1;
        _beat++;

        // What the player said, then what they said back to it, then either the next question or the
        // goodbye. "#player" is the base class's marker for a line spoken out of the player's own bubble.
        var said = new List<string> { choice.text + " #player" };
        if (!string.IsNullOrEmpty(choice.response)) said.Add(Voiced(choice.response));

        if (last)
        {
            _finished = true;
            var goodbye = timeUp && _script.timeUpFarewell != null && _script.timeUpFarewell.Length > 0
                ? _script.timeUpFarewell
                : _script.farewell;
            said.AddRange(Lines(goodbye));
        }
        else if (_queue != null)
        {
            // Their reply lands, then the player moves up the line to whoever is next.
            _walkNext = true;
        }
        else
        {
            said.AddRange(BeatLines(_script.beats[_beat]));
        }

        lines = said.ToArray();
        RestartLines();
    }

    // ------------------------------------------------------------------ walking the queue

    // Send the player to the rail in front of the fan whose turn it is.
    void WalkToFan()
    {
        _walker = OnFootController.Current;
        Transform fan = _queue != null ? _queue.Fan(_beat) : null;
        if (_walker == null || fan == null || _walker.Ridden != null) { FaceFan(); return; }

        Vector2 stand = _queue.Stand(_beat);
        float distance = Vector2.Distance(_walker.transform.position, stand);
        _walkGiveUpAt = Time.time + distance / Mathf.Max(0.5f, _walker.moveSpeed) * 2f + 1.5f;
        _walker.WalkTo(stand);
        _walking = true;
    }

    void UpdateWalk()
    {
        // The body went away under the walk — into the car, a scene change. Nothing left to sign with.
        if (_walker == null) { AbandonConversation(); return; }
        if (_walker.ScriptedWalking && Time.time < _walkGiveUpAt) return;
        FaceFan();
    }

    // Arrived (or got as close as the paddock let them): turn to the fan, turn the fan to the player, and
    // hand the fan the beat.
    void FaceFan()
    {
        _walking = false;
        if (_walker != null) _walker.StopWalking();

        Transform fan = _queue != null ? _queue.Fan(_beat) : null;
        var player = WeekendVenueAnchor.OnFootPlayer();
        if (fan != null && player != null)
        {
            var body = player.GetComponent<OnFootController>();
            if (body != null) body.FaceToward(fan.position);
            float offset = body != null ? body.spriteFacingOffsetDeg : 90f;
            OnFootController.ApplyFacing(fan, fan.GetComponent<Rigidbody2D>(),
                                         player.position - fan.position, offset);
        }

        voice = fan;
        voiceName = SigningContent.FanSpeaker;
        lines = BeatLines(_script.beats[Mathf.Clamp(_beat, 0, _script.beats.Count - 1)], fan != null);
        RestartLines();
    }

    // A line said by the fan at the front, when there is one; otherwise by the host as before.
    string Voiced(string line) => voice != null ? line + " #voice" : line;

    void Advance()
    {
        _beat++;
        if (_beat >= _script.beats.Count) { _finished = true; lines = Lines(_script.farewell); }
        else lines = BeatLines(_script.beats[_beat]);
        RestartLines();
    }

    // Settle up: the ledger takes the outcome, the schedule's clock moves to the end of the booking, and
    // the appointment is discharged.
    void EndBusiness()
    {
        var a = _activity;
        var script = _script;
        _activity = null;
        _script = null;
        _queue = null;
        voice = null;

        if (a == null || script == null) return;

        WeekendAppointment.Clear();
        WeekendDirector.Finish(a, script.Settle(_running, Mathf.Max(1, _answered)), inWorld: true);
    }

    // Walked away mid-conversation. The booking stays unattended: come back, or miss it like any other.
    void AbandonConversation()
    {
        _activity = null;
        _script = null;
        _finished = false;
        _walkNext = false;
        if (_walking && _walker != null) _walker.StopWalking();
        _walking = false;
        _queue = null;
        EndConversation();
        voice = null;
    }

    // ------------------------------------------------------------------ line plumbing

    // Speak a fresh set of lines mid-conversation, which is how a beat is swapped in. The base class has
    // already closed out the previous run by the time we get here (its Interact returned false, which is
    // what opened the question), so this starts the new lines from the top.
    void RestartLines()
    {
        SetInteractor(WeekendVenueAnchor.OnFootPlayer());
        base.Interact();
    }

    // `voiced`: the beat's line is said by the fan stood in front of the player rather than by the host. The
    // preamble stays the host's — it is the fence talking about the queue, not anybody in it.
    static string[] BeatLines(WeekendBeat beat, bool voiced = false)
    {
        var said = new List<string>();
        if (beat.preamble != null) said.AddRange(beat.preamble);
        if (!string.IsNullOrEmpty(beat.line)) said.Add(voiced ? beat.line + " #voice" : beat.line);
        if (said.Count == 0) said.Add("...");
        return said.ToArray();
    }

    static string[] Lines(params IEnumerable<string>[] sets)
    {
        var all = new List<string>();
        foreach (var set in sets)
            if (set != null) all.AddRange(set);
        if (all.Count == 0) all.Add("...");
        return all.ToArray();
    }
}
