using Draftmaster.Audio;
using UnityEngine;

// The saved sound levels, as the voices read them. OPTIONS > SOUND writes them; every AudioSource the game
// drives multiplies its own level by its channel's here, every time it sets one, so a change is heard on the
// next frame without anything having to be told.
//
// MASTER is the exception: it is AudioListener.volume, so it covers every voice, including the ones nobody
// has put in a channel. AudioEars ducks the listener across a camera cut and brings it back to Master.
//
// There is no AudioMixer in this project, which is why the levels live in code rather than as mixer groups.
// The rules (steps, labels, keys) are Draftmaster.Audio.SoundMix.
public static class SoundVolumes
{
    static readonly int[] _percent = new int[SoundMix.All.Length];
    static bool _loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        _loaded = false;    // play mode may start without a domain reload; read the prefs fresh
        AudioListener.volume = Get(SoundChannel.Master);
    }

    // 0..1 for a channel.
    public static float Get(SoundChannel channel) => SoundMix.Scale(Percent(channel));

    public static float Engines => Get(SoundChannel.Engines);
    public static float Effects => Get(SoundChannel.Effects);
    public static float Crowd => Get(SoundChannel.Crowd);
    public static float Interface => Get(SoundChannel.Interface);
    public static float Master => Get(SoundChannel.Master);

    public static int Percent(SoundChannel channel)
    {
        Load();
        return _percent[(int)channel];
    }

    public static void SetPercent(SoundChannel channel, int percent)
    {
        Load();
        int v = SoundMix.Clamp(percent);
        _percent[(int)channel] = v;
        PlayerPrefs.SetInt(SoundMix.PrefKey(channel), v);
        PlayerPrefs.Save();
        if (channel == SoundChannel.Master) AudioListener.volume = SoundMix.Scale(v);
    }

    // Forget the cached levels, so the next read comes from the prefs (after a save is wiped or restored).
    public static void Invalidate() => _loaded = false;

    static void Load()
    {
        if (_loaded) return;
        foreach (var c in SoundMix.All)
            _percent[(int)c] = SoundMix.Clamp(PlayerPrefs.GetInt(SoundMix.PrefKey(c), SoundMix.DefaultPercent));
        _loaded = true;
    }
}
