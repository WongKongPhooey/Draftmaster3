using UnityEngine;

// Background atmosphere bed for the on-foot pit scene — a distant crowd that's already there when the demo
// opens, so the paddock doesn't feel empty before a car has turned a wheel.
//
// Ducks AND muffles itself while the player is inside a masked room — the motorhome (RVInterior) or a team's
// popup garage (PopupGarageInterior). The world outside is masked out, so the crowd should sound like it's
// through a wall: a wall eats the top end long before it eats the level, so the volume drop alone read as
// "crowd moved away" rather than "player went inside". An AudioLowPassFilter rolls the high end off with it.
// Spawned by PitLaneStart; no scene wiring beyond the clip.
public class AmbienceLoop : MonoBehaviour
{
    [Tooltip("Looping ambience clip.")]
    public AudioClip clip;
    [Tooltip("Volume out in the paddock, for a full house watching the race. Everything quieter than that is scaled off it.")]
    [Range(0f, 1f)] public float volume = 0.35f;

    [Header("How loud the place is")]
    [Tooltip("Hard ceiling on the crowd bed, whatever the spawner asks for. Sunday afternoon at full song sits here and nothing is ever louder.")]
    [Range(0f, 1f)] public float raceDayCeiling = 0.12f;
    [Tooltip("Scale the bed by the half-day and by what the circuit is doing (CrowdPolicy.NoiseForSession). Off = always race-day loud.")]
    public bool scaleWithWeekend = true;
    [Tooltip("Seconds between re-reads of the weekend clock. The answer changes on the hour, not on the frame.")]
    public float sessionPollSeconds = 1f;
    [Tooltip("Volume multiplier while the player is inside a room — muffled, not silent.")]
    [Range(0f, 1f)] public float indoorScale = 0.15f;
    [Tooltip("Seconds to cross-fade between indoor and outdoor levels.")]
    public float fadeSeconds = 1.2f;

    [Header("Muffling")]
    [Tooltip("Low-pass cutoff (Hz) out in the paddock — above hearing, i.e. the filter is out of the way.")]
    public float outdoorCutoffHz = 22000f;
    [Tooltip("Low-pass cutoff (Hz) inside a room. Around 600-900 is 'through a thin wall'; lower is thicker.")]
    [Range(150f, 5000f)] public float indoorCutoffHz = 700f;
    [Tooltip("Filter resonance. 1 is flat; a little above adds a boxy ring at the cutoff.")]
    [Range(1f, 10f)] public float resonance = 1f;
    [Tooltip("Low-pass cutoff (Hz) when the place is at its quietest. A half-empty Friday is heard across an empty circuit, so it should be duller as well as quieter — not the same roar turned down.")]
    [Range(500f, 22000f)] public float quietCutoffHz = 2600f;

    AudioSource _src;
    AudioLowPassFilter _lowPass;
    float _sessionScale = 1f;   // 0..1, how loud this half-day and this session are
    float _pollIn;
    float _level;      // current volume, faded up from silence at spawn
    float _muffle;     // 0 = outdoors, 1 = fully indoors

    public static AmbienceLoop Play(AudioClip clip, float volume)
    {
        if (clip == null) return null;
        var go = new GameObject("PitAmbience");
        var amb = go.AddComponent<AmbienceLoop>();
        amb.clip = clip;
        amb.volume = volume;
        return amb;
    }

    void Start()
    {
        _src = gameObject.AddComponent<AudioSource>();
        _src.clip = clip;
        _src.loop = true;
        _src.playOnAwake = false;
        _src.spatialBlend = 0f;      // 2D bed, not a point source
        _src.volume = 0f;
        _level = 0f;

        // Added after the source so it sits downstream of it on the same object.
        _lowPass = gameObject.AddComponent<AudioLowPassFilter>();
        _lowPass.cutoffFrequency = outdoorCutoffHz;
        _lowPass.lowpassResonanceQ = resonance;
        _muffle = 0f;

        if (clip != null) _src.Play();
    }

    void Update()
    {
        if (_src == null) return;

        float dt = Time.deltaTime;

        bool indoors = PlayerIsIndoors();

        _pollIn -= dt;
        if (_pollIn <= 0f)
        {
            _pollIn = Mathf.Max(0.1f, sessionPollSeconds);
            _sessionScale = scaleWithWeekend ? SessionNoise() : 1f;
        }

        float peak = Mathf.Min(volume, raceDayCeiling) * _sessionScale;
        float target = peak * (indoors ? indoorScale : 1f);
        // Faded at a rate set by the loudest the bed ever gets, so a change of session arrives at the same
        // pace whether it is going up or down and whatever the crowd happens to be doing.
        _level = Mathf.MoveTowards(_level, target, (raceDayCeiling / Mathf.Max(0.05f, fadeSeconds)) * dt);
        _src.volume = _level;

        // The cutoff is swept in octaves, not in Hz: 22000 -> 700 linearly would spend the first second of
        // the fade somewhere nobody can hear the difference and then slam the last few hundred Hz shut.
        _muffle = Mathf.MoveTowards(_muffle, indoors ? 1f : 0f, dt / Mathf.Max(0.05f, fadeSeconds));
        if (_lowPass != null)
        {
            // Two things close the filter, in order: how quiet the session is (distance across an empty
            // circuit), and then the wall, if the player has stepped inside.
            float quiet = Mathf.Clamp01(1f - _sessionScale);
            float open = Mathf.Lerp(Mathf.Log(Mathf.Max(20f, outdoorCutoffHz)),
                                    Mathf.Log(Mathf.Max(20f, quietCutoffHz)), quiet);
            float lo = Mathf.Log(Mathf.Max(20f, indoorCutoffHz));
            _lowPass.cutoffFrequency = Mathf.Exp(Mathf.Lerp(open, Mathf.Min(open, lo), _muffle));
            _lowPass.lowpassResonanceQ = resonance;
        }
    }

    // How loud this hour of the weekend is, 0..1 against a full house watching the race.
    //
    // A crowd that roars through Friday practice is the single loudest wrong note in the paddock: those
    // grandstands are half empty, the cars on track are somebody's installation laps, and what you should
    // hear is a murmur. The rule itself lives in CrowdPolicy next to the headcount the same half-day
    // spawns, so the place sounds as full as it looks.
    // SessionMood owns the mapping, because the crowd's one-liners are drawn off the same answer and the
    // two must never disagree — a murmur that says Friday practice under people talking about the grid
    // forming up is worse than getting both wrong the same way.
    static float SessionNoise()
        => Draftmaster.Crowd.CrowdPolicy.NoiseForSession(SessionMood.HalfDaySlot(), SessionMood.TrackActivity());

    // Is the player stood in any masked room. Both kinds count — to the player, stepping into the motorhome
    // and stepping into the team's garage are the same move, and both put a wall between them and the crowd.
    //
    // Register reads, not scene walks: outdoors both registers come back empty, so this question is asked
    // and answered with nothing on every frame of the walk.
    static bool PlayerIsIndoors()
    {
        var rooms = RVInterior.All;
        for (int i = 0; i < rooms.Count; i++)
            if (rooms[i] != null && rooms[i].IsInside) return true;

        return PopupGarageInterior.Occupied != null;
    }
}
