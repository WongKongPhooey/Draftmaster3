using System;
using System.Collections.Generic;
using UnityEngine;

// NASCAR Thunder 2004-style driver relationships. Every driver pair carries a score from -100 (hated
// enemy) to +100 (trusted ally), persisted across races in PlayerPrefs. Car-to-car contact drops the
// pair's score (scaled by closing severity, reported by VehicleCollision); races without incident slowly
// heal it (RaceDirector calls RegenTowardNeutral on classification). When a score falls below
// PaybackThreshold, AIRacingBehaviour may deliberately wreck the rival on track.
//
// Identity is the car's DriverLabel name (the player car's label is set by TeamSwitchController /
// falls back to RacePositionTracker.playerName), so scores survive between sessions as long as the
// driver-name pool produces the same names.
public static class DriverRelationships
{
    public const float Min = -100f;
    public const float Max = 100f;

    // Standing thresholds. Crossing these is what the HUD feed announces.
    public const float AllyThreshold = 40f;      // above: helps in the draft (future use), friendly feed lines
    public const float RivalThreshold = -30f;    // below: a declared rivalry
    public const float PaybackThreshold = -60f;  // below: the AI will consider wrecking them on purpose

    // Contact tuning: drop = base + severity-scaled, per genuine impact (severity is VehicleCollision's
    // 0..1 closing-speed severity). A pair can only log one contact per cooldown window, so a grinding
    // side-by-side scrape doesn't nuke the relationship in a single corner.
    const float ContactBaseDrop = 3f;
    const float ContactSeverityDrop = 14f;
    const float PairContactCooldown = 1.5f;

    const string PrefPrefix = "rel.";
    const string IndexKey = "rel.index";

    public enum Standing { Furious, Rival, Neutral, Ally }

    static readonly Dictionary<string, float> _values = new();       // pairKey -> score (cache over PlayerPrefs)
    static readonly Dictionary<string, float> _pairContactTime = new();
    static HashSet<string> _index;                                    // all persisted pair keys

    // (nameA, nameB, newValue, delta) — fired on every score change.
    public static event Action<string, string, float, float> Changed;
    // (striker, victim, severity) — fired once per logged contact, striker = car that drove into the other.
    public static event Action<string, string, float> ContactReported;
    // (attacker, target) — fired when an AI commits to a deliberate payback move.
    public static event Action<string, string> PaybackDeclared;

    public static Standing StandingOf(float value)
    {
        if (value <= PaybackThreshold) return Standing.Furious;
        if (value <= RivalThreshold) return Standing.Rival;
        if (value >= AllyThreshold) return Standing.Ally;
        return Standing.Neutral;
    }

    public static float Get(string a, string b)
    {
        string key = PairKey(a, b);
        if (key == null) return 0f;
        if (_values.TryGetValue(key, out float v)) return v;
        v = PlayerPrefs.GetFloat(PrefPrefix + key, 0f);
        _values[key] = v;
        return v;
    }

    public static void Modify(string a, string b, float delta)
    {
        string key = PairKey(a, b);
        if (key == null || Mathf.Approximately(delta, 0f)) return;
        float v = Get(a, b);
        float nv = Mathf.Clamp(v + delta, Min, Max);
        if (Mathf.Approximately(nv, v)) return;
        Store(key, nv);
        Changed?.Invoke(a, b, nv, nv - v);
        QuestManager.OnRelationshipChanged(a, b, nv);
    }

    // Contact entry point, called by VehicleCollision for genuine impacts (closing speed above the damage
    // threshold). frontHit = the contact point lies ahead of the reporter along its direction of travel,
    // i.e. the reporter drove INTO the other car; when false, blame is swapped to the other car. Both cars'
    // colliders report the same impact — the per-pair cooldown keeps only the first.
    public static void ReportContact(GameObject reporterCar, GameObject otherCar, float severity, bool frontHit)
    {
        if (!RaceStart.IsGreen) return;   // formation shuffling / pit-box nudges don't start feuds
        if (reporterCar == null || otherCar == null) return;
        if (reporterCar.GetComponent<SafetyCar>() != null || otherCar.GetComponent<SafetyCar>() != null) return;

        string a = NameOf(reporterCar);
        string b = NameOf(otherCar);
        string key = PairKey(a, b);
        if (key == null) return;

        float now = Time.time;
        if (_pairContactTime.TryGetValue(key, out float last) && now - last < PairContactCooldown) return;
        _pairContactTime[key] = now;

        if (!frontHit) (a, b) = (b, a);   // the other car came to us — they're the striker

        float drop = ContactBaseDrop + ContactSeverityDrop * Mathf.Clamp01(severity);
        Modify(a, b, -drop);
        ContactReported?.Invoke(a, b, severity);
        RivalryFeed.Ensure();

        bool playerStriker = IsPlayerName(a);
        bool playerVictim = IsPlayerName(b);
        if (playerStriker) PlayerStatsLedger.Increment("contacts.caused");
        if (playerVictim) PlayerStatsLedger.Increment("contacts.received");
        if (playerStriker || playerVictim)
            QuestManager.OnPlayerContact(playerStriker ? b : a, severity, playerStriker);
    }

    // AIRacingBehaviour announces a committed payback move here (for the feed, quests, and the ledger).
    public static void DeclarePayback(string attacker, string target)
    {
        PaybackDeclared?.Invoke(attacker, target);
        RivalryFeed.Ensure();
        if (IsPlayerName(target)) PlayerStatsLedger.Increment("paybacks.against");
    }

    // Time heals: pull every stored pair toward neutral by `amount`. Called once per race classification.
    public static void RegenTowardNeutral(float amount)
    {
        if (amount <= 0f) return;
        foreach (var key in LoadIndex())
        {
            float v = _values.TryGetValue(key, out float c) ? c : PlayerPrefs.GetFloat(PrefPrefix + key, 0f);
            float nv = Mathf.MoveTowards(v, 0f, amount);
            if (!Mathf.Approximately(nv, v)) Store(key, nv);
        }
        PlayerPrefs.Save();
    }

    // Every persisted pair, for standings UI / quest checks. Names are the stored lower-case halves.
    public static IEnumerable<(string a, string b, float value)> AllPairs()
    {
        foreach (var key in LoadIndex())
        {
            int bar = key.IndexOf('|');
            if (bar <= 0) continue;
            float v = _values.TryGetValue(key, out float c) ? c : PlayerPrefs.GetFloat(PrefPrefix + key, 0f);
            yield return (key.Substring(0, bar), key.Substring(bar + 1), v);
        }
    }

    public static void ResetAll()
    {
        foreach (var key in LoadIndex()) PlayerPrefs.DeleteKey(PrefPrefix + key);
        PlayerPrefs.DeleteKey(IndexKey);
        PlayerPrefs.Save();
        _values.Clear();
        _index = null;
    }

    // The display name a car competes under. AI carry a DriverLabel from spawn; the player car falls back
    // to the position tracker's player name so player relationships persist regardless of which car the
    // player currently drives (TeamSwitchController labels swapped cars explicitly).
    public static string NameOf(GameObject car)
    {
        if (car == null) return "";
        var label = car.GetComponent<DriverLabel>();
        if (label != null && !string.IsNullOrEmpty(label.driverName)) return label.driverName;
        if (car.GetComponent<PlayerVehicleController>() != null)
        {
            var rt = RacePositionTracker.Instance;
            return rt != null && !string.IsNullOrEmpty(rt.playerName) ? rt.playerName : "You";
        }
        return car.name;
    }

    public static bool IsPlayerName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        var rt = RacePositionTracker.Instance;
        string player = rt != null && !string.IsNullOrEmpty(rt.playerName) ? rt.playerName : "You";
        return string.Equals(name.Trim(), player.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    // ---- internals ----

    static void Store(string key, float value)
    {
        _values[key] = value;
        PlayerPrefs.SetFloat(PrefPrefix + key, value);
        if (LoadIndex().Add(key))
            PlayerPrefs.SetString(IndexKey, string.Join("\n", _index));
        PlayerPrefs.Save();
    }

    static HashSet<string> LoadIndex()
    {
        if (_index == null)
        {
            _index = new HashSet<string>();
            var raw = PlayerPrefs.GetString(IndexKey, "");
            foreach (var k in raw.Split('\n'))
                if (!string.IsNullOrEmpty(k)) _index.Add(k);
        }
        return _index;
    }

    // Order-independent, case-insensitive pair key: "alowercase|blowercase". Null for degenerate pairs.
    static string PairKey(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return null;
        string la = a.Trim().ToLowerInvariant();
        string lb = b.Trim().ToLowerInvariant();
        if (la.Length == 0 || lb.Length == 0 || la == lb) return null;
        return string.CompareOrdinal(la, lb) <= 0 ? la + "|" + lb : lb + "|" + la;
    }
}
