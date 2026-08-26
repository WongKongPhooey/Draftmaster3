using UnityEngine;
using Draftmaster.Chatter;
using Draftmaster.Fans;

// Makes a background NPC mutter a one-liner as the on-foot player walks past. This is the cheap half of a
// living paddock: a crowd that only reacts when you press E reads as a room full of mannequins, so the
// silent majority get an unprompted line instead.
//
// Deliberately restrained, because twenty of these run at once:
//   * one line per approach — the NPC re-arms only after the player leaves its notice radius,
//   * a per-NPC cooldown on top, so pacing back and forth doesn't farm barks,
//   * a scene-wide throttle, so a crowd murmurs one voice at a time rather than all shouting,
//   * total silence while a real conversation (NPCInteractable) is open, so barks never talk over dialogue.
//
// Line selection lives in Draftmaster.Chatter.AmbientChatter (pure, unit-tested); this component only
// decides WHEN to speak.
public class NPCAmbientChatter : MonoBehaviour
{
    [Tooltip("Which line pool this speaker draws from.")]
    public ChatterArea area = ChatterArea.Paddock;
    [Tooltip("The player has to come within this many metres to be noticed.")]
    public float noticeRange = 6.5f;
    [Tooltip("Minimum seconds between this NPC's own barks, on top of re-arming.")]
    public float minRepeatSeconds = 22f;
    [Tooltip("Speaker name shown above the bubble. Empty = an anonymous murmur (the usual case).")]
    public string speakerName = "";
    [Tooltip("Stand still and look at the player while speaking. PaddockWalker honours this.")]
    public bool pauseWhileSpeaking = true;

    // One voice at a time across the whole scene, whoever gets there first.
    [Tooltip("Scene-wide gap (s) between any two ambient barks, so a crowd doesn't speak in chorus.")]
    public float sceneGapSeconds = 4f;
    static float _nextSceneBark;

    // Time.time restarts at 0 with the scene, so a stale throttle carried over from the previous run
    // (domain reload disabled) would silence the whole crowd. Clear it every play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSceneThrottle() => _nextSceneBark = 0f;

    SpeechBubble _bubble;
    string _lastLine;
    float _nextOwnBark;
    float _hideAt;
    bool _armed = true;          // false once we've spoken, until the player walks away again
    bool _speaking;

    // Read by PaddockWalker so a wandering NPC stops and turns while delivering its line.
    public bool IsSpeaking => _speaking;
    public Transform Listener { get; private set; }

    void OnDisable() { Silence(); }
    void OnDestroy() { if (_bubble != null) Destroy(_bubble.gameObject); }

    void Update()
    {
        if (_speaking && Time.time >= _hideAt) Silence();

        var player = AutographFanSpawner.OnFootPlayer;
        if (player == null) { _armed = true; return; }

        float dist = Vector2.Distance(transform.position, player.transform.position);
        float range = Mathf.Max(0.5f, noticeRange);

        // Re-arm once the player is clearly clear of us — hysteresis, so standing on the boundary
        // doesn't trigger a bark every cooldown.
        if (dist > range * 1.4f) { _armed = true; return; }
        if (_speaking || !_armed || dist > range) return;

        if (Time.time < _nextOwnBark || Time.time < _nextSceneBark) return;
        if (NPCInteractable.AnyConversationActive) return;   // never talk over real dialogue

        Speak(player.transform);
    }

    void Speak(Transform listener)
    {
        var mood = AmbientChatter.MoodFor(FanAppeal.Value);
        // Seed off our own identity plus the clock so two NPCs noticing the player in the same second
        // don't pick the same line, and the same NPC doesn't repeat itself.
        int seed = GetInstanceID() ^ Mathf.RoundToInt(Time.time * 977f);
        string line = AmbientChatter.Pick(area, mood, seed, _lastLine);
        if (string.IsNullOrEmpty(line)) return;

        if (_bubble == null) _bubble = SpeechBubble.Attach(transform);

        // Background flavour never talks over anything, and is never queued to be said late — if the
        // screen is busy this bark simply did not happen, and the next one is along in a second.
        if (!_bubble.Speak(line, string.IsNullOrEmpty(speakerName) ? null : speakerName,
                           Draftmaster.Sim.SpeechPriority.Ambient))
            return;

        _lastLine = line;
        _speaking = true;
        Listener = pauseWhileSpeaking ? listener : null;
        _hideAt = Time.time + AmbientChatter.ReadSeconds(line);
        _armed = false;
        _nextOwnBark = Time.time + Mathf.Max(0f, minRepeatSeconds);
        _nextSceneBark = Time.time + Mathf.Max(0f, sceneGapSeconds);
    }

    void Silence()
    {
        _speaking = false;
        Listener = null;
        if (_bubble != null) _bubble.Hide();
    }
}
