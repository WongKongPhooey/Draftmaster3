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
        var exterior = RVExterior.Player;   // not the first found: the masked room carries an RVExterior of its own
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
        sb.AppendLine($"  interior: {(rv != null ? $"IsInside={rv.IsInside} room={rv.transform.position.ToString("F2")}" : "<missing>")}");

        // The room is anchored on the spawn marker; if it is not sat on the shell, stepping out of the door
        // lands wherever the room is, not beside the motorhome the player can see.
        var shell = RVExterior.Player;
        if (shell != null)
        {
            var spawn = shell.transform.Find("SpawnPoint_RV");
            Vector3 spawnAt = spawn != null ? spawn.position : shell.transform.position;
            float gap = rv != null ? Vector2.Distance(rv.transform.position, spawnAt) : -1f;
            sb.AppendLine($"  shell: pos={shell.transform.position.ToString("F2")} marker={spawnAt.ToString("F2")} " +
                          $"door={shell.DoorWorldPosition.ToString("F2")} room-to-marker={gap:F2}m");
        }
        else sb.AppendLine("  shell: <missing>");

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

    // Everything that can stop the player inside the motorhome: solid colliders overlapping the room, and
    // whether each spot across the room counts as walkable to the PaddockBoundary clamp.
    [MenuItem("Draftmaster/Debug/RV Cutscene/Report Colliders Near Player")]
    public static void ReportColliders()
    {
        if (!Application.isPlaying) { Debug.LogWarning("RVCutsceneDebug: enter play mode first."); return; }
        var player = GameObject.Find("OnFootPlayer");
        if (player == null) { Debug.LogError("RVCutsceneDebug: no OnFootPlayer."); return; }

        Vector2 p = player.transform.position;
        var sb = new StringBuilder($"RVCutsceneDebug colliders within 8m of {p.ToString("F2")}:\n");
        foreach (var c in Physics2D.OverlapCircleAll(p, 8f))
        {
            if (c.attachedRigidbody != null && c.attachedRigidbody.gameObject == player) continue;
            sb.AppendLine($"  {Path(c.transform)} [{c.GetType().Name}] trigger={c.isTrigger} " +
                          $"bounds={c.bounds.min.ToString("F2")}..{c.bounds.max.ToString("F2")}");
        }

        sb.AppendLine($"  boundaries active={PaddockBoundary.Active.Count}:");
        foreach (var b in PaddockBoundary.Active)
            if (b != null) sb.AppendLine($"    {Path(b.transform)} contains player={b.Contains(p)}");

        // A walkability grid across the room, 1m apart: '#' = the clamp would push the player off it.
        sb.AppendLine("  walkable grid (rows north to south, x -6..+6 round the player, '#' = clamped):");
        for (int y = 6; y >= -6; y--)
        {
            sb.Append("    ");
            for (int x = -6; x <= 6; x++)
                sb.Append(x == 0 && y == 0 ? '@' : PaddockBoundary.IsInside(p + new Vector2(x, y)) ? '.' : '#');
            sb.AppendLine();
        }
        Debug.Log(sb.ToString());
    }

    static string Path(Transform t)
    {
        string s = t.name;
        for (var up = t.parent; up != null; up = up.parent) s = up.name + "/" + s;
        return s;
    }

    // The engineer is a PlacedNPC marker now, and the body it spawns is named after it — ask the marker
    // rather than guessing at a hard-coded object name.
    static NPCInteractable EngineerInteractable()
    {
        var marker = PlacedNPC.Find(PlacedNPC.Role.TeamLiaison);
        return marker != null ? marker.Interactable : null;
    }
}
