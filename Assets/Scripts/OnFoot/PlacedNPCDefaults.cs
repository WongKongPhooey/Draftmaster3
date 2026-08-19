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
// pit lane, the chief stands by the player's car. The race engineer is deliberately NOT one of them — his
// beat is the player coming out of their RV, and an RV is track content, so his marker belongs in the track
// package that owns the motorhome (Draftmaster > NPCs > Add RV Engineer To Open Package). Auto-installing
// him meant deleting him from a track put him straight back, which is the opposite of what placing an NPC
// in a package should mean.
public static class PlacedNPCDefaults
{
    // Create any of the every-track cast the scene doesn't already have. Returns how many were added.
    // The race engineer is not included — see the note above; use CreateEngineer inside a track package.
    public static int EnsureCast(Transform parent = null)
    {
        int added = 0;
        if (Find(PlacedNPC.Role.PitGreeter) == null) { CreateGreeter(parent); added++; }
        if (Find(PlacedNPC.Role.CrewChief) == null)  { CreateChief(parent);   added++; }
        return added;
    }

    // Any marker with this role, built or not — unlike PlacedNPC.Find, which only sees ones that appeared.
    public static PlacedNPC Find(PlacedNPC.Role role)
    {
        foreach (var npc in PlacedNPC.All)
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

    public static PlacedNPC CreateEngineer(Transform parent = null)
    {
        var npc = New("NPC_RaceEngineer", parent);
        npc.npcId = "race.engineer";
        npc.speakerName = "Race Engineer";
        npc.role = PlacedNPC.Role.RaceEngineer;
        npc.anchor = PlacedNPC.Anchor.RVDoor;   // falls back to the player's spawn line at a track with no RV
        npc.anchorAlong = 5f;                   // straight out from the door
        npc.anchorLateral = 2.5f;               // along the RV, toward the cab
        npc.interaction = PlacedNPC.Interaction.WalkUpCutscene;
        npc.waitForTrigger = true;
        npc.triggerOffset = new Vector2(2.6f, 0f); // past the interior's exit threshold, so the mask has lifted
        npc.triggerRadius = 1.5f;
        npc.stopDistance = 1.2f;
        npc.objectiveOnFinish = "HEAD TO YOUR CAR";
        // Every practice session, not once per weekend: he's the session's opening beat, and a player who
        // reloads practice should still be met by their engineer. No business turning up at qualifying or
        // the race — he's there to hand the weekend over.
        npc.appear.repeat = AppearanceConditions.Repeat.EveryTime;
        npc.appear.saveKey = "rv.door.intro";
        npc.appear.inPractice = true;
        npc.appear.inQualifying = false;
        npc.appear.inRace = false;
        npc.lines = new[]
        {
            "There you are. Was starting to think you'd sleep through the whole weekend.",
            "The bunk in that thing is better than my bed at home. #player",
            "Well, shake it off. Car's out of the truck and sitting in the box.",
            "How did it look overnight? #player",
            "Solid. We freshened the rubber and dropped a touch of front wing back in.",
            "Anything you want from me? #player",
            "Get in it. Chief's waiting by the car — he'll want your setup call before you roll out.",
            "On my way. #player"
        };
        return npc;
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
