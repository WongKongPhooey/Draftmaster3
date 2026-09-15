using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// EditMode coverage for the first thing the career does: a black screen, an alarm clock going off, and the
// card that says which venue you have woken up at and what day it is — read OFF that black screen rather
// than after it.
//
// The beat is a coroutine on a spawned player and can only be watched in play mode, so what is pinned here
// is the two things that decide whether it reads right and that a later edit could quietly undo:
//   * how many times the clock actually gets to ring before the picture comes up, counted off the clip the
//     game synthesises rather than off a number restated in a test, and
//   * that the card is drawn IN FRONT of ScreenFade's wipe. Under it, the label is simply not on screen —
//     which is why the card used to be held back until the lights were up.
//
// Assembly-CSharp can't be referenced by an asmdef, so the types are reached by reflection the same way
// WakeUpFacingTests reaches the sequence.
public class WakeUpOpeningTests
{
    static readonly System.Type SequenceType = System.Type.GetType("WakeUpSequence, Assembly-CSharp");
    static readonly System.Type SettingsType = System.Type.GetType("WakeUpSequence+Settings, Assembly-CSharp");
    static readonly System.Type IntroType = System.Type.GetType("SpawnIntroUI, Assembly-CSharp");
    static readonly System.Type FadeType = System.Type.GetType("ScreenFade, Assembly-CSharp");

    // How long the alarm rings in the dark, as shipped.
    static float DarkSeconds
    {
        get
        {
            object defaults = SettingsType
                .GetProperty("Default", BindingFlags.Public | BindingFlags.Static)
                .GetValue(null);
            return (float)SettingsType.GetField("darkSeconds").GetValue(defaults);
        }
    }

    static AudioClip Alarm() => (AudioClip)SequenceType
        .GetMethod("PlaceholderAlarm", BindingFlags.NonPublic | BindingFlags.Static)
        .Invoke(null, null);

    static int Depth(System.Type type, string name) =>
        (int)type.GetField(name, BindingFlags.Public | BindingFlags.Static).GetRawConstantValue();

    // Ring the real clock for `seconds` and report what was heard. The clip is one burst plus its rest and
    // is played on loop, so ringing is that loop over and over; a chirp is a run of non-silence, debounced
    // by less than the gap between chirps and more than any zero crossing inside one.
    static void RingFor(float seconds, out int beeps, out bool cutOffMidBeep)
    {
        var clip = Alarm();
        Assert.IsNotNull(clip, "The placeholder alarm did not build, so the opening rings in silence.");

        var loop = new float[clip.samples * clip.channels];
        clip.GetData(loop, 0);

        int rate = clip.frequency;
        int total = Mathf.CeilToInt(seconds * rate);
        int debounce = Mathf.CeilToInt(0.02f * rate);
        const float Silence = 0.01f;

        beeps = 0;
        int lastSounding = -rate;   // a second before the clock started; MinValue would overflow the gap below
        for (int i = 0; i < total; i++)
        {
            if (Mathf.Abs(loop[i % loop.Length]) <= Silence) continue;
            if (i - lastSounding > debounce) beeps++;
            lastSounding = i;
        }

        // Still mid-chirp when the fade starts = the buzzer gets chopped off rather than running out.
        cutOffMidBeep = total - 1 - lastSounding < Mathf.CeilToInt(0.01f * rate);
    }

    [Test]
    public void TheClockRingsALongEnoughSpellToBeIgnored()
    {
        RingFor(DarkSeconds, out int beeps, out _);

        // Two seconds of buzzer reads as a scene transition with a noise over it. Three bursts of the
        // clock — a dozen chirps — reads as an alarm somebody is refusing to get up for, which is the
        // point of opening the career this way.
        Assert.GreaterOrEqual(beeps, 12,
            $"The alarm only gets {beeps} beeps out in {DarkSeconds:0.##}s of dark before the picture " +
            "comes up. The opening is meant to ring for a spell, not chirp once and brighten.");
    }

    [Test]
    public void ThePictureComesUpOutOfASilenceNotOverAChoppedBeep()
    {
        RingFor(DarkSeconds, out _, out bool cutOffMidBeep);

        Assert.IsFalse(cutOffMidBeep,
            $"{DarkSeconds:0.##}s of dark ends inside a chirp, so StopAlarm cuts the buzzer off mid-note. " +
            "Land the dark in one of the gaps in the clock's rhythm.");
    }

    [Test]
    public void TheArrivalCardIsDrawnInFrontOfTheWipe()
    {
        int wipe = Depth(FadeType, "WipeDepth");
        int front = Depth(IntroType, "FrontOfFadeDepth");
        int normal = Depth(IntroType, "NormalDepth");

        // Lower IMGUI depth draws in front.
        Assert.Less(front, wipe,
            "The over-black card is not in front of ScreenFade's wipe, so the venue and the day are " +
            "underneath a full-alpha black rectangle and the player reads nothing while the alarm rings.");
        Assert.Greater(normal, wipe,
            "The ordinary card is in front of the wipe, so every scene transition in the game now has a " +
            "title card drawn through it.");
    }

    [Test]
    public void TheCardIsUpAndReadableWhileItIsStillDark()
    {
        var go = new GameObject("SpawnIntroUITest") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var ui = go.AddComponent(IntroType);

            Assert.IsFalse((bool)IntroType.GetField("overFade").GetValue(ui),
                "SpawnIntroUI defaults to drawing over the fade, which would put a title card through " +
                "every wipe in the game. Only the career's first morning turns it on.");

            float fadeIn = (float)IntroType.GetField("titleFadeIn").GetValue(ui);
            Assert.Less(fadeIn, DarkSeconds,
                "The card takes longer to fade up than the screen stays black, so where you are and what " +
                "day it is is still arriving as the picture does.");
        }
        finally { Object.DestroyImmediate(go); }
    }
}
