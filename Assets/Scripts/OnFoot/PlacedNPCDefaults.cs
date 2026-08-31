using UnityEngine;

// The pit-lane opening cast, as PlacedNPC markers: the greeter and the crew chief.
//
// These used to be spawned from fields on PitLaneStart. They're built here instead so there is ONE
// definition of them — the editor menu (Draftmaster > NPCs > Install Default Pit Cast) creates them as real,
// editable scene objects from this file, and a scene that hasn't had that run yet gets the identical set
// created at runtime. Once they exist in the scene the runtime install stays out of the way, so editing them
// in the inspector is the only thing that decides who turns up.
//
// Only beats that work off geometry EVERY track has are installed automatically: the greeter stands in the
// pit lane, the chief stands by the player's car, the liaison catches the driver at the motorhome door.
//
// The RV door used to belong to a race engineer who talked about how the car looked overnight and sent the
// player to their crew chief for a setup call. That beat is retired: the weekend books the driver's day now,
// and the person waiting outside the motorhome is the team liaison telling them what the team needs them at
// next — the strategy meeting, the media session, whatever the sheet says. Two people ambushing somebody on
// their way out of their own motorhome was one too many, and the engineer was the half that no longer knew
// what the day held.
public static class PlacedNPCDefaults
{
    // Create any of the every-track cast the scene doesn't already have. Returns how many were added.
    public static int EnsureCast(Transform parent = null)
    {
        int added = 0;
        if (Find(PlacedNPC.Role.PitGreeter) == null)  { CreateGreeter(parent); added++; }
        if (Find(PlacedNPC.Role.CrewChief) == null)   { CreateChief(parent);   added++; }
        // The liaison turns out whatever is next, including the player's own session: she reads the sheet,
        // and "you're down for cup practice at the car, 10:00" is as much her job as a sponsor appearance.
        // She used to stand down when the next thing was on track, because the race engineer met the player
        // at the door in that case. He is gone, so the door is hers.
        if (Find(PlacedNPC.Role.TeamLiaison) == null) { CreateLiaison(parent); added++; }
        return added;
    }

    // Any marker with this role, built or not — unlike PlacedNPC.Find, which only sees ones that appeared.
    //
    // Scans the scene rather than trusting PlacedNPC.All: that registry is filled by OnEnable, which does
    // not run in the editor, so an edit-time check against it always says "nobody is here" and installing
    // the cast a second time quietly doubles it.
    public static PlacedNPC Find(PlacedNPC.Role role)
    {
        foreach (var npc in PlacedNPC.All)
            if (npc != null && npc.role == role) return npc;

        foreach (var npc in Object.FindObjectsByType<PlacedNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (npc != null && npc.role == role) return npc;

        return null;
    }

    public static PlacedNPC CreateGreeter(Transform parent = null)
    {
        var npc = New("NPC_PitGreeter", parent);
        npc.npcId = "pit.greeter";
        npc.speakerName = "Pit Crew";
        npc.role = PlacedNPC.Role.PitGreeter;
        npc.anchor = PlacedNPC.Anchor.PitLane;
        npc.anchorAlong = -1.5f;    // just behind where the player spawns
        npc.anchorLateral = -5.5f;  // out into the lane, away from the wall
        npc.interaction = PlacedNPC.Interaction.TalkOnInteract;
        npc.lines = new[]
        {
            "Morning! Car's prepped and fuelled, ready when you are.",
            "Thanks. Anything I should know? #player",
            "Track's still cold, so take the first lap easy.",
            "Will do. #player",
            "Right then — hop in whenever you're set. Good luck out there!"
        };
        return npc;
    }

    // The people who are at every circuit, in every series, all weekend: the ones the driver's day is
    // actually built around. Installed as editable markers so a track can move them, redress them or give
    // them their own lines — but every track HAS them, which is what makes the paddock legible.
    //
    // Everybody else in the paddock is crowd: PaddockSpawner scatters walkers and talkers around these.
    public static int EnsureCoreCast(Transform parent = null)
    {
        int added = EnsureCast(parent);
        if (Find(PlacedNPC.Role.ChiefStrategist) == null) { CreateStrategist(parent); added++; }
        if (Find(PlacedNPC.Role.PRManager) == null)       { CreatePR(parent);         added++; }
        return added;
    }

    // The run plan: what the race is going to be, and what the car has to do to get through it. Stood at
    // the pit box, which is where the timing stand and the strategy screens are.
    public static PlacedNPC CreateStrategist(Transform parent = null)
    {
        var npc = New("NPC_ChiefStrategist", parent);
        npc.npcId = "team.strategist";
        npc.speakerName = "Chief Strategist";
        npc.role = PlacedNPC.Role.ChiefStrategist;
        npc.anchor = PlacedNPC.Anchor.PitLane;
        npc.anchorAlong = 3.5f;
        npc.anchorLateral = -3.2f;
        npc.interaction = PlacedNPC.Interaction.TalkOnInteract;
        npc.lines = new[] { "Numbers are still coming in. Give me an hour." };

        // The three days as three conversations: what we are trying to learn, what we learned, and what we
        // are going to do about it on Sunday.
        npc.schedule = new System.Collections.Generic.List<PlacedNPC.ScheduledLines>
        {
            new PlacedNPC.ScheduledLines
            {
                label = "Friday — learning the place",
                fridayAM = true, fridayPM = true,
                lines = new[]
                {
                    "Long run first, short run after. I want to know what the tyre does after fifteen.",
                    "And if it falls off a cliff? #player",
                    "Then we build a car that survives it and we pass people at the end.",
                },
            },
            new PlacedNPC.ScheduledLines
            {
                label = "Saturday — qualifying and the plan",
                saturdayAM = true, saturdayPM = true,
                lines = new[]
                {
                    "Two laps on the sticker set, that's all you get. Don't spend it on the out lap.",
                    "Where do we stand on strategy? #player",
                    "Depends where you put it. Front half, we race it. Back half, we go long and wait for the caution.",
                },
            },
            new PlacedNPC.ScheduledLines
            {
                label = "Sunday — race day",
                sundayAM = true, sundayPM = true,
                lines = new[]
                {
                    "Fuel window opens on lap fifty-two. If a caution falls before that, we stay out.",
                    "And if it doesn't? #player",
                    "Then we're in the middle of a green-flag cycle and you'll be driving to a number. You'll be fine.",
                },
            },
        };
        return npc;
    }

    // The media and sponsor side of the day: who wants you, for how long, and what not to say.
    public static PlacedNPC CreatePR(Transform parent = null)
    {
        var npc = New("NPC_PRManager", parent);
        npc.npcId = "team.pr";
        npc.speakerName = "PR Manager";
        npc.role = PlacedNPC.Role.PRManager;
        npc.anchor = PlacedNPC.Anchor.RVDoor;
        npc.anchorAlong = 6.5f;
        npc.anchorLateral = 3.4f;
        npc.interaction = PlacedNPC.Interaction.TalkOnInteract;
        npc.lines = new[] { "Nothing booked with me right now. Enjoy it while it lasts." };

        npc.schedule = new System.Collections.Generic.List<PlacedNPC.ScheduledLines>
        {
            new PlacedNPC.ScheduledLines
            {
                label = "Friday — setting the week up",
                fridayAM = true, fridayPM = true,
                lines = new[]
                {
                    "Two things today: the fan fence at some point, and don't say anything interesting about the car.",
                    "What if the car IS interesting? #player",
                    "Then say it's a work in progress and smile. That's the job.",
                },
            },
            new PlacedNPC.ScheduledLines
            {
                label = "Saturday — media day",
                saturdayAM = true, saturdayPM = true,
                lines = new[]
                {
                    "The room's fuller than yesterday, so give them one line they can print.",
                    "Any line? #player",
                    "One about the team. Not one about the last restart.",
                },
            },
            new PlacedNPC.ScheduledLines
            {
                label = "Sunday — race day",
                sundayAM = true, sundayPM = true,
                lines = new[]
                {
                    "Intros, then you're mine for ninety seconds with the broadcast, then you're the crew chief's.",
                    "Ninety seconds. #player",
                    "Ninety. And the sponsor's name in the first sentence, please.",
                },
            },
        };
        return npc;
    }

    // The team's liaison: the person who catches you on the way out of the motorhome and tells you where
    // the team needs you to be. She is the front door of the race weekend — the schedule exists whether or
    // not the player ever opens it, and this is how they are told about it in the world.
    //
    // Her lines are built when the cast is stood up, which is after the ledger and timetable are loaded, so
    // she can name the actual next booking rather than talk in general terms.
    public static PlacedNPC CreateLiaison(Transform parent = null)
    {
        var npc = New("NPC_TeamLiaison", parent);
        npc.npcId = "team.liaison";
        npc.speakerName = "Team Liaison";
        npc.role = PlacedNPC.Role.TeamLiaison;
        npc.anchor = PlacedNPC.Anchor.RVDoor;
        npc.anchorAlong = 4f;                      // straight out from the door, in the way on purpose
        npc.anchorLateral = -2.2f;                 // off to one side of the door, in the way on purpose
        npc.interaction = PlacedNPC.Interaction.WalkUpCutscene;
        // She is the day: the driver wakes up with nothing booked and an empty objective strip, and it is
        // this conversation that puts the first obligation on the map. See WeekendBriefing.
        npc.givesTheDaysObjective = true;
        npc.waitForTrigger = true;
        npc.triggerOffset = new Vector2(2.6f, 0f); // past the interior's exit threshold, so the mask has lifted
        npc.triggerRadius = 1.5f;
        npc.stopDistance = 1.3f;

        // Once per half-day: she is telling you today's plan, not every session's.
        npc.appear.repeat = AppearanceConditions.Repeat.EveryTime;
        npc.appear.saveKey = "rv.door.liaison";
        npc.appear.inPractice = true;
        npc.appear.inQualifying = false;
        npc.appear.inRace = false;
        npc.linesFromTheWeekendSheet = true;   // re-read when she appears, so a placed marker never goes stale
        npc.lines = LiaisonLines();
        return npc;
    }

    // What she says, filled in from the sheet. No booking left today and she says so rather than sending
    // the player somewhere that is not on.
    public static string[] LiaisonLines()
    {
        var next = Draftmaster.Weekend.WeekendSchedulePlan.NextWorthDoing();
        string clock = Draftmaster.Weekend.WeekendSlots.Day(Draftmaster.Weekend.WeekendLedger.CurrentSlot) + ", " +
                       Draftmaster.Weekend.WeekendSlots.ClockAmPm(Draftmaster.Weekend.WeekendLedger.ClockMinute);

        if (next == null)
        {
            return new[]
            {
                "Morning. You're clear for now — nothing on the sheet until later.",
                "I'll come and find you when you're needed. #player",
                "That's the idea. Enjoy the quiet, it doesn't last.",
            };
        }

        return new[]
        {
            $"There you are. {clock}, and you're already wanted.",
            $"You're down for {Draftmaster.Weekend.WeekendSchedulePlan.Describe(next)}.",
            "Do I have time for a coffee first? #player",
            "You have time to walk. I've put it on your map — follow the marker and don't be late, they " +
            "start without you.",
        };
    }

    public static PlacedNPC CreateChief(Transform parent = null)
    {
        var npc = New("NPC_CrewChief", parent);
        npc.npcId = "crew.chief";
        npc.speakerName = "Crew Chief";
        npc.role = PlacedNPC.Role.CrewChief;
        npc.anchor = PlacedNPC.Anchor.ParkedCar;
        npc.anchorAlong = -1.5f;    // a short way behind the car
        npc.anchorLateral = -3f;    // out on the side the driver walks in from
        npc.followAnchor = true;    // GridSpawner re-parks the car several frames in; go with it
        npc.interaction = PlacedNPC.Interaction.OnCarEntry;
        npc.repeatable = false;     // the briefing is a one-off, not a chat loop
        npc.appear.repeat = AppearanceConditions.Repeat.OncePerWeekend;
        npc.appear.saveKey = "car.setup.briefing";
        npc.lines = new[]
        {
            "Belts tight? Good. Weather's holding, so it's your call on rubber.",
            "How long are we running? #player",
            "Short one. Take what you need and no more — every litre is weight.",
            "Okay — how do you want the car set up?"
        };

        // The same man, three different conversations: he is briefing you into whatever session is next,
        // and on Sunday that is a race rather than a run plan.
        npc.schedule = new System.Collections.Generic.List<PlacedNPC.ScheduledLines>
        {
            new PlacedNPC.ScheduledLines
            {
                label = "Friday — first run",
                fridayAM = true, fridayPM = true,
                lines = new[]
                {
                    "Belts tight? Good. Nobody's run here yet today, so the track's green and it'll come to you.",
                    "How long are we running? #player",
                    "Short one. Bring it back in one piece and tell me what the middle of the corner does.",
                    "Okay — how do you want the car set up?",
                },
            },
            new PlacedNPC.ScheduledLines
            {
                label = "Saturday — qualifying trim",
                saturdayAM = true, saturdayPM = true,
                lines = new[]
                {
                    "Two laps and it's over, so I've trimmed it out. It'll be nervous. That's on purpose.",
                    "How nervous? #player",
                    "Enough that you'll know about it in turn one. Don't chase it, just drive it.",
                    "Okay — how do you want the car set up?",
                },
            },
            new PlacedNPC.ScheduledLines
            {
                label = "Sunday — race day",
                sundayAM = true, sundayPM = true,
                lines = new[]
                {
                    "Long day. The car you qualified is not the car you're racing — I've put the balance back in it.",
                    "What do you need from me? #player",
                    "First twenty laps, tell me nothing but tyres. After that we'll talk about winning it.",
                    "Okay — how do you want the car set up?",
                },
            },
        };
        return npc;
    }

    // Where scene-level markers live: one empty at the origin called "NPCs". They used to be parented to
    // PitLaneStart, which only describes where the pit lane begins — nothing about the cast belongs to it.
    public const string RootName = "NPCs";

    public static Transform Root()
    {
        var existing = GameObject.Find(RootName);
        if (existing != null) return existing.transform;

        var go = new GameObject(RootName);
        go.transform.position = Vector3.zero;
        return go.transform;
    }

    static PlacedNPC New(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent != null ? parent : Root(), false);
        return go.AddComponent<PlacedNPC>();
    }
}
