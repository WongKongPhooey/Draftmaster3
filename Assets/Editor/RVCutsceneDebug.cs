using System.Text;
using UnityEditor;
using UnityEngine;

// Play-mode debug drivers for the RV exit cutscene. MCP can't move objects or run code while the
// editor is playing, but menu items still execute — so these stand in for walking the player:
// teleport them out the RV door / onto the cutscene trigger, and dump the relevant state to the
// console. Harmless outside play mode (they just log and bail).
public static class RVCutsceneDebug
{
    [MenuItem("Draftmaster/Debug/RV Cutscene/Teleport Player Outside Door")]
    public static void TeleportOutsideDoor() => TeleportFromDoor(1.0f);

    [MenuItem("Draftmaster/Debug/RV Cutscene/Teleport Player Onto Trigger")]
    public static void TeleportOntoTrigger() => TeleportFromDoor(2.6f);

    static void TeleportFromDoor(float metresOut)
    {
        if (!Application.isPlaying) { Debug.LogWarning("RVCutsceneDebug: enter play mode first."); return; }
        var exterior = Object.FindFirstObjectByType<RVExterior>();
        var player = GameObject.Find("OnFootPlayer");
        if (exterior == null || player == null)
        {
            Debug.LogError($"RVCutsceneDebug: missing refs (exterior={(exterior != null)}, player={(player != null)}).");
            return;
        }

        Vector3 target = exterior.DoorWorldPosition + (Vector3)(exterior.DoorWorldDirection * metresOut);
        target.z = player.transform.position.z; // RVInterior owns z; it restores/pulls on the state flip
        var rb = player.GetComponent<Rigidbody2D>();
        if (rb != null) rb.position = target;   // teleport the physics pose, no sweep
        player.transform.position = target;
        Debug.Log($"RVCutsceneDebug: player teleported to {target} ({metresOut}m out the door).");
    }

    [MenuItem("Draftmaster/Debug/RV Cutscene/Start NPC Conversation")]
    public static void StartConversation()
    {
        if (!Application.isPlaying) { Debug.LogWarning("RVCutsceneDebug: enter play mode first."); return; }
        var player = GameObject.Find("OnFootPlayer");
        var ofc = player != null ? player.GetComponent<OnFootController>() : null;
        var npc = EngineerInteractable();
        if (ofc == null || npc == null)
        {
            Debug.LogError($"RVCutsceneDebug: missing refs (player={(ofc != null)}, npc={(npc != null)}).");
            return;
        }
        ofc.BeginConversation(npc);
        Debug.Log("RVCutsceneDebug: conversation started.");
    }

    [MenuItem("Draftmaster/Debug/RV Cutscene/Report State")]
    public static void ReportState()
    {
        if (!Application.isPlaying) { Debug.LogWarning("RVCutsceneDebug: enter play mode first."); return; }
        var sb = new StringBuilder("RVCutsceneDebug state:\n");

        var player = GameObject.Find("OnFootPlayer");
        var ofc = player != null ? player.GetComponent<OnFootController>() : null;
        sb.AppendLine($"  player: pos={(player != null ? player.transform.position.ToString("F2") : "<missing>")} locked={(ofc != null ? ofc.MovementLocked.ToString() : "?")}");

        var rv = Object.FindFirstObjectByType<RVInterior>();
        sb.AppendLine($"  interior: {(rv != null ? $"IsInside={rv.IsInside}" : "<missing>")}");

        var npc = EngineerInteractable();
        sb.AppendLine($"  npc: pos={(npc != null ? npc.transform.position.ToString("F2") : "<missing>")} talking={(npc != null ? npc.IsTalking.ToString() : "?")}");

        var marker = PlacedNPC.Find(PlacedNPC.Role.TeamLiaison);
        var seq = marker != null ? GameObject.Find(marker.name + "_Cutscene") : null;
        sb.AppendLine($"  cutscene object: {(seq != null ? "alive at " + seq.transform.position.ToString("F2") : "destroyed/absent")}");

        // The two things the opening turns on: whether the alarm played, and whether the day is still
        // hers to hand over. An opening that "did nothing" is nearly always one of these two saying no.
        sb.AppendLine($"  wake up: {PitLaneStart.LastWakeDecision}");
        sb.AppendLine($"  objective: waitingToBeTold={WeekendDirector.WaitingToBeTold()} " +
                      $"booked='{WeekendAppointment.PendingId}' " +
                      $"giver={(PlacedNPC.ObjectiveGiver() == null ? "nobody" : PlacedNPC.ObjectiveGiver().name)}");

        Debug.Log(sb.ToString());
    }

    // The engineer is a PlacedNPC marker now, and the body it spawns is named after it — ask the marker
    // rather than guessing at a hard-coded object name.
    static NPCInteractable EngineerInteractable()
    {
        var marker = PlacedNPC.Find(PlacedNPC.Role.TeamLiaison);
        return marker != null ? marker.Interactable : null;
    }
}
