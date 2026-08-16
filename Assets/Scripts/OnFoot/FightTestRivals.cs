using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Test scaffolding for the fight mechanic: makes sure a couple of the drivers you can actually walk up to
// are already rivals, so the "square up" option is reachable without first going out and wrecking someone.
//
// Real rivalries come from the track (VehicleCollision -> DriverRelationships). This only fills in for a
// fresh save: it picks the drivers nearest the player's on-foot spawn and, if the pair are still on neutral
// terms, drops the score past DriverRelationships.RivalThreshold. An existing feud is never overwritten.
//
// Self-installing under the same gate as the rest of the paddock (single player + the on-foot pit flow), so
// there is no scene wiring. Turn it off with Enabled = false, or wipe what it wrote with
// Draftmaster > Fights > Clear Seeded Test Rivalries.
public class FightTestRivals : MonoBehaviour
{
    [Tooltip("How many nearby drivers to make rivals.")]
    public int rivalCount = 2;
    [Tooltip("Relationship scores handed out, worst first. Anything at or below DriverRelationships.RivalThreshold (-30) unlocks the fight option; below PaybackThreshold (-60) they're furious.")]
    public float[] seededScores = { -72f, -46f };
    [Tooltip("Seconds to wait for the paddock to finish spawning its drivers before giving up.")]
    public float waitSeconds = 20f;

    // Master switch, in case a play test wants the paddock exactly as the save left it.
    public static bool Enabled = true;

    // PlayerPrefs key holding the pairs this seeder wrote, so the editor menu can undo exactly those.
    public const string SeededKey = "fight.testrivals";

    static bool _hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        TryInstall();
        if (_hooked) return;
        SceneManager.sceneLoaded += (_, __) => TryInstall();
        _hooked = true;
    }

    static void TryInstall()
    {
        if (!Enabled) return;
        if (FindObjectOfType<FightTestRivals>() != null) return;
        if (!GameSession.IsSinglePlayer) return;
        if (FindObjectOfType<PitLaneStart>() == null) return;      // no on-foot paddock, nobody to fall out with
        new GameObject("FightTestRivals").AddComponent<FightTestRivals>();
    }

    IEnumerator Start()
    {
        // The paddock drivers are spawned several seconds in (the field, then the motorhome row, then the
        // presence director), so wait for talkable drivers to exist before picking any.
        float waited = 0f;
        List<RivalDriverNPC> drivers = null;
        while (waited < waitSeconds)
        {
            drivers = Talkable();
            if (drivers.Count > 0) break;
            waited += Time.deltaTime;
            yield return null;
        }

        if (drivers == null || drivers.Count == 0)
        {
            Debug.Log("FightTestRivals: no talkable drivers turned up — nothing seeded.", this);
            yield break;
        }

        // Nearest to the player first: those are the ones a tester can reach in a few seconds.
        var player = GameObject.Find("OnFootPlayer");
        Vector3 from = player != null ? player.transform.position : transform.position;
        drivers.Sort((a, b) => Vector3.SqrMagnitude(a.transform.position - from)
                              .CompareTo(Vector3.SqrMagnitude(b.transform.position - from)));

        string playerName = DriverRelationships.PlayerName;
        var seeded = new List<string>();
        int wanted = Mathf.Min(rivalCount, drivers.Count);

        for (int i = 0, taken = 0; i < drivers.Count && taken < wanted; i++)
        {
            var npc = drivers[i];
            string name = npc.Identity;
            if (string.IsNullOrEmpty(name)) continue;

            float current = DriverRelationships.Get(playerName, name);
            if (current <= DriverRelationships.RivalThreshold)
            {
                // Already feuding for real — leave it exactly as the save has it.
                taken++;
                continue;
            }

            float target = seededScores != null && seededScores.Length > 0
                ? seededScores[Mathf.Min(taken, seededScores.Length - 1)]
                : DriverRelationships.RivalThreshold - 10f;

            DriverRelationships.Modify(playerName, name, target - current);
            seeded.Add(name);
            taken++;

            float dist = Vector3.Distance(npc.transform.position, from);
            Debug.Log($"FightTestRivals: {name} is now a rival of {playerName} at {target:0} " +
                      $"({dist:0}m from the player). Walk over and talk to them to square up.", npc);
        }

        if (seeded.Count > 0)
        {
            var existing = PlayerPrefs.GetString(SeededKey, "");
            var all = new List<string>(existing.Split('\n'));
            foreach (var n in seeded) if (!all.Contains(n)) all.Add(n);
            all.RemoveAll(string.IsNullOrEmpty);
            PlayerPrefs.SetString(SeededKey, string.Join("\n", all));
            PlayerPrefs.Save();
        }
    }

    static List<RivalDriverNPC> Talkable()
    {
        var list = new List<RivalDriverNPC>();
        for (int i = 0; i < NPCInteractable.All.Count; i++)
        {
            if (NPCInteractable.All[i] is RivalDriverNPC r && r.isActiveAndEnabled) list.Add(r);
        }
        return list;
    }
}
