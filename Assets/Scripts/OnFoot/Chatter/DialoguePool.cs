using UnityEngine;

namespace Draftmaster.Chatter
{
    // Which random speaker a conversation belongs to. The pools are separate because the voices are:
    // a crew member in the paddock and a fan at the fence don't say interchangeable things.
    public enum ConversationKind
    {
        PaddockCrew,    // PaddockSpawner's talkable paddock NPCs
        AutographFan,   // AutographFanSpawner's fans
        DriverFlavour,  // DriverPresenceDirector's rival drivers (not yet wired — see Docs)
    }

    // An authorable pool of dialogue for the randomly-spawned crowd, per track.
    //
    // Assets live in `Assets/Resources/Dialogue/`. A pool with an empty `trackId` applies everywhere (the
    // house style); a pool naming a track applies only at that track, on top of the global one. So a
    // Daytona pool can add beach-week lines without anybody having to re-type the generic paddock chat.
    //
    // `replaceBuiltIn` is the escape hatch for a track that wants a completely different voice — set it and
    // the code's built-in tables drop out, leaving only what's authored here.
    [CreateAssetMenu(fileName = "DialoguePool", menuName = "Draftmaster/Dialogue Pool")]
    public class DialoguePool : ScriptableObject
    {
        [Tooltip("Track id this pool is for, e.g. 'Daytona'. Leave EMPTY for the global pool that applies at every track.")]
        public string trackId = "";

        [Tooltip("Drop the built-in code tables and use only what's authored here (plus the global pool). Off = these lines are ADDED to the built-ins.")]
        public bool replaceBuiltIn = false;

        [System.Serializable]
        public class ChatterSet
        {
            [Tooltip("Where the speaker is standing — a tyre bay shouldn't talk like a grandstand.")]
            public ChatterArea area = ChatterArea.Paddock;
            [Tooltip("What the crowd makes of the player, from fan appeal. Impressed/Dismissive pools fall back to Neutral when empty.")]
            public ChatterMood mood = ChatterMood.Neutral;
            [TextArea]
            [Tooltip("One-liners muttered as the player walks past. One line per entry — these are barks, not conversations.")]
            public string[] lines;
        }

        [System.Serializable]
        public class Conversation
        {
            public ConversationKind kind = ConversationKind.PaddockCrew;
            [Tooltip("Optional speaker name for this conversation. Empty = picked from the name pool below.")]
            public string speakerName = "";
            [TextArea]
            [Tooltip("The whole conversation, one line per entry. A line ending with \"#player\" is spoken by the driver.")]
            public string[] lines;
        }

        [Header("Ambient barks")]
        public ChatterSet[] chatter;

        [Header("Conversations")]
        public Conversation[] conversations;

        [Header("Names")]
        [Tooltip("Names random talkers can be given. Added to the built-in list unless Replace Built In is on.")]
        public string[] speakerNames;

        public bool IsGlobal => string.IsNullOrEmpty(trackId);

        // Does this pool apply at the given track? The global pool applies everywhere.
        public bool AppliesTo(string track)
            => IsGlobal || (!string.IsNullOrEmpty(track) &&
                            string.Equals(trackId.Trim(), track.Trim(), System.StringComparison.OrdinalIgnoreCase));
    }
}
