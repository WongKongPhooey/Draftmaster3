using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// How the demo opens: black screen, an alarm clock going off somewhere in the dark, and only then the
// picture — so the first thing the game does is wake you up rather than show you a paddock.
//
// The beat, in order:
//   1. the screen is black before the first frame is drawn (ScreenFade.HoldBlack from PitLaneStart.Start),
//   2. the alarm rings, and the player is lying down and cannot move,
//   3. the picture fades up (or the player slaps the clock — any key — and it fades up early),
//   4. the driver gets up, turned toward the way out, and control is handed over.
//
// Getting up is a turn as well as a stand: a body left in the pose it was instantiated with faces whatever
// the prefab was drawn facing (south, for these rigs), which reads as waking up and staring at the back of
// the motorhome. `Settings.facing` is the world direction they are on their feet facing — north by default,
// up the screen and out of the rig. It is a screen direction, not the RV's own doorway (RVExterior puts that
// wherever the rig was placed and it need not be north); PitLaneStart.wakeFacing retunes it. And it is held
// rather than merely applied, because OnFootController writes the standing facing back into the rig from the
// direction it remembers on every physics step.
//
// Art placeholders: with no lying-down sprite assigned the body is simply laid on its side and stood back
// up by rotation, and with no Animator trigger the same rotation is the "getting up animation". Assign
// `lyingDownSprite` / `getUpTrigger` on PitLaneStart and the real art takes over with nothing else changed.
// The alarm works the same way: hand it a clip, or it synthesises a passable digital one.
//
// Runtime-built by PitLaneStart; nothing to wire in a scene.
public class WakeUpSequence : MonoBehaviour
{
    // Everything the beat is allowed to be tuned by, so the knobs can live on PitLaneStart's inspector
    // (where the rest of the scene open is authored) rather than on an object that only exists at runtime.
    public struct Settings
    {
        public AudioClip alarmClip;         // null = the synthesised placeholder
        public float alarmVolume;
        public float darkSeconds;           // how long the alarm rings before the picture comes up
        public float fadeInSeconds;
        public float getUpSeconds;
        public Sprite lyingDownSprite;      // null = lay the standing sprite on its side
        public string getUpTrigger;         // Animator trigger, if the rig has one
        public float lyingRotationDeg;      // which way the body falls when there is no sprite for it
        public Vector2 facing;              // world direction they stand up facing; zero = keep the spawn pose

        public static Settings Default => new Settings
        {
            alarmVolume = 0.55f,
            // Long enough for three full bursts of the placeholder clock — a dozen beeps — and it runs out
            // in the gap after the third rather than mid-chirp, so the picture comes up out of a silence
            // instead of cutting the buzzer off. Two seconds read as a scene transition with a noise over
            // it; this reads as a clock somebody is ignoring.
            darkSeconds = 3.4f,
            fadeInSeconds = 1.8f,
            getUpSeconds = 0.8f,
            lyingRotationDeg = 90f,
            facing = Vector2.up,            // north: out of the motorhome rather than into the back of it
        };
    }

    // The earliest the player is allowed to hit the clock. Without it, a key still held from the menu that
    // loaded this scene snoozes the alarm before anybody has heard it.
    const float SnoozeArmsAfter = 0.6f;

    public static bool Running { get; private set; }

    Settings _s;
    OnFootController _player;
    AudioSource _alarm;

    public static WakeUpSequence Play(OnFootController player, Settings settings)
    {
        if (player == null) return null;
        // A co-op guest joins a weekend already underway — they do not wake up into the host's morning.
        if (Coop.IsGuest) return null;

        var go = new GameObject("WakeUpSequence");
        var seq = go.AddComponent<WakeUpSequence>();
        seq._player = player;
        seq._s = settings;
        seq.StartCoroutine(seq.Run());
        return seq;
    }

    IEnumerator Run()
    {
        Running = true;

        // The screen is already black (PitLaneStart holds it from Start so the first frame is dark); this
        // is belt and braces for anything else that calls in.
        ScreenFade.HoldBlack();

        _player.MovementLocked = true;
        var body = _player.transform;
        var sprite = _player.GetComponentInChildren<SpriteRenderer>();
        var animator = _player.GetComponent<Animator>();

        Sprite standing = sprite != null ? sprite.sprite : null;
        // Where this ends: on their feet, facing the way out. Solved first because the lying-down
        // placeholder below is that pose tipped on its side, so the body falls the way it will stand.
        Quaternion upright = UprightFacing(_s.facing, _player.spriteFacingOffsetDeg, body.rotation);

        // Down you go. A dedicated sprite if there is one, otherwise the standing one on its side —
        // which is the placeholder, and reads correctly from directly overhead.
        if (sprite != null && _s.lyingDownSprite != null) sprite.sprite = _s.lyingDownSprite;
        else body.rotation = upright * Quaternion.Euler(0f, 0f, _s.lyingRotationDeg);

        StartAlarm();

        // Ring in the dark, until the clock has had its say or the player reaches out and hits it.
        float rang = 0f;
        while (rang < Mathf.Max(0.05f, _s.darkSeconds))
        {
            if (rang >= SnoozeArmsAfter && Snoozed()) break;
            rang += Time.unscaledDeltaTime;
            yield return null;
        }

        StopAlarm();

        // The room comes up.
        bool lit = false;
        ScreenFade.FromBlack(0f, Mathf.Max(0.05f, _s.fadeInSeconds), () => lit = true);
        while (!lit) yield return null;

        // ...and the driver gets up. A rig with a real animation plays it; otherwise the body rotates back
        // upright over the same beat, which is the placeholder for that animation.
        bool played = false;
        if (animator != null && !string.IsNullOrEmpty(_s.getUpTrigger) && HasTrigger(animator, _s.getUpTrigger))
        {
            animator.SetTrigger(_s.getUpTrigger);
            played = true;
        }

        if (sprite != null && _s.lyingDownSprite != null && standing != null) sprite.sprite = standing;

        float getUp = Mathf.Max(0.05f, _s.getUpSeconds);
        Quaternion down = body.rotation;
        for (float t = 0f; t < getUp; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / getUp);
            if (!played) body.rotation = Quaternion.Slerp(down, upright, k);
            yield return null;
        }

        // Up, and turned. FaceToward is what makes the turn survive the hand-over: the
        // controller rewrites the rig's facing from the direction it remembers on every FixedUpdate, and
        // that direction is still whatever the prefab was built with until something tells it otherwise.
        if (_s.facing.sqrMagnitude > 0.0001f)
            _player.FaceToward(body.position + (Vector3)_s.facing.normalized);
        body.rotation = upright;

        _player.MovementLocked = false;
        Running = false;
        Destroy(gameObject);
    }

    // The rotation that stands a body up facing `facing`, a world direction. `spriteFacingOffsetDeg` is the
    // angle between the sprite's drawn facing and +X — the same convention OnFootController turns everybody
    // else by, so waking up and walking off agree about which way "north" points. Asked for no direction at
    // all (a zero vector), the body keeps the pose it was spawned with.
    public static Quaternion UprightFacing(Vector2 facing, float spriteFacingOffsetDeg, Quaternion spawned)
    {
        if (facing.sqrMagnitude < 0.0001f) return spawned;
        float ang = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg + spriteFacingOffsetDeg;
        return Quaternion.Euler(0f, 0f, ang);
    }

    // Any key, any face button: hitting the clock.
    static bool Snoozed()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;

        var pad = Gamepad.current;
        if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
                            pad.startButton.wasPressedThisFrame)) return true;

        return false;
    }

    static bool HasTrigger(Animator animator, string name)
    {
        foreach (var p in animator.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == name) return true;
        return false;
    }

    // ------------------------------------------------------------------ the clock itself

    void StartAlarm()
    {
        var clip = _s.alarmClip != null ? _s.alarmClip : PlaceholderAlarm();
        if (clip == null) return;

        _alarm = gameObject.AddComponent<AudioSource>();
        _alarm.clip = clip;
        _alarm.loop = true;
        _alarm.playOnAwake = false;
        _alarm.spatialBlend = 0f;                       // it is on the nightstand, not in the world
        _alarm.volume = Mathf.Clamp01(_s.alarmVolume);
        _alarm.Play();
    }

    void StopAlarm()
    {
        if (_alarm == null) return;
        _alarm.Stop();
        Destroy(_alarm);
        _alarm = null;
    }

    static AudioClip _placeholder;

    // A bedside digital alarm, generated rather than shipped: four hard square-wave beeps and a gap, on a
    // loop. Square because that is what those clocks are — a cheap piezo buzzer, not a tone generator —
    // and it wants to be annoying enough to wake somebody up without being loud enough to be unpleasant.
    static AudioClip PlaceholderAlarm()
    {
        if (_placeholder != null) return _placeholder;

        const int rate = 44100;
        const float beep = 0.09f;     // one chirp
        const float gap = 0.07f;      // between chirps
        const int beeps = 4;
        const float rest = 0.55f;     // before the next burst
        const float freq = 2600f;     // the shrill end of a piezo

        float length = beeps * (beep + gap) + rest;
        int samples = Mathf.CeilToInt(length * rate);
        var data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            float within = t % (beep + gap);
            int index = Mathf.FloorToInt(t / (beep + gap));
            if (index >= beeps || within > beep) continue;      // between chirps, or resting

            // Square wave, with the edges eased over a millisecond so the speaker doesn't click.
            float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t));
            float edge = Mathf.Clamp01(Mathf.Min(within, beep - within) / 0.004f);
            data[i] = square * edge * 0.35f;
        }

        _placeholder = AudioClip.Create("AlarmClockPlaceholder", samples, 1, rate, false);
        _placeholder.SetData(data, 0);
        return _placeholder;
    }
}
