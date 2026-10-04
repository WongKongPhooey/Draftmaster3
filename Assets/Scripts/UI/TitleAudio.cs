using UnityEngine;

// Small shared helpers for the title screen's sound: the logo's crunches (TitleLogoIntro) and the cars going
// past (TitleCrashScene).
public static class TitleAudio
{
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
