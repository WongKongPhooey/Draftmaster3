using UnityEngine;

// Small shared helpers for the title screen's sound: the logo's crunches (TitleLogoIntro) and the cars going
// past (TitleCrashScene). The logo's crunch is also the sound of a card turning over in a Master Draft pack
// (MasterDraftUI.PackOpening), so its recipe lives here: one clip, pitched down, an octave-down copy under it for
// weight, and a short metallic reverb.
public static class TitleAudio
{
    public const string CrunchClip = "Audio/metal-crunch";
    public const float CrunchPitch = 0.74f;
    public const float CrunchVolume = 0.9f;
    public const float CrunchSubVolume = 0.7f;
    public const float CrunchReverbSeconds = 1.3f;

    // A short, bright, metallic room on every hit (the filter treats every source on `host`): the tail is what
    // makes a slam sound like it happened somewhere big rather than in a box.
    public static AudioReverbFilter AddCrunchReverb(GameObject host, float decaySeconds)
    {
        var verb = host.AddComponent<AudioReverbFilter>();
        verb.reverbPreset = AudioReverbPreset.User;
        verb.dryLevel = 0f;
        verb.room = -400f;
        verb.roomHF = -300f;
        verb.roomLF = 0f;
        verb.decayTime = decaySeconds;
        verb.decayHFRatio = 0.7f;
        verb.reflectionsLevel = -200f;
        verb.reflectionsDelay = 0.012f;
        verb.reverbLevel = 0f;
        verb.reverbDelay = 0.02f;
        verb.diffusion = 100f;
        verb.density = 100f;
        return verb;
    }

    // Nothing is heard without a listener. AudioEars normally provides the game's one (on a DontDestroyOnLoad
    // object, installed at boot); this only steps in if it is missing, so the title is never silent.
    public static void EnsureListener()
    {
        if (Object.FindFirstObjectByType<AudioListener>() != null) return;
        // The title's camera is not tagged MainCamera, so Camera.main is no help; any camera will do, since
        // every title sound is unspatialised, and failing that a listener on its own.
        var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam != null) cam.gameObject.AddComponent<AudioListener>();
        else new GameObject("TitleAudioListener", typeof(AudioListener));
    }

    // A looping, unspatialised voice on `host`, silent until it is driven.
    public static AudioSource Loop(GameObject host, AudioClip clip)
    {
        if (clip == null) return null;
        var a = host.AddComponent<AudioSource>();
        a.clip = clip;
        a.loop = true;
        a.playOnAwake = false;
        a.spatialBlend = 0f;
        a.volume = 0f;
        a.Play();
        return a;
    }
}
