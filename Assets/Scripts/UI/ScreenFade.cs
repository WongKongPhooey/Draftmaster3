using System;
using System.Collections;
using UnityEngine;

// A black wipe over everything, for the moments the game moves the player somewhere they did not walk.
//
// Self-installing and scene-independent, so anything can ask for one without wiring a canvas. Drawn in
// IMGUI in front of the rest of the furniture and driven on unscaled time, so it still plays while the
// weekend has the clock stopped.
public class ScreenFade : MonoBehaviour
{
    public static ScreenFade Instance { get; private set; }
    public static bool Busy => Instance != null && Instance._busy;

    float _alpha;
    bool _busy;
    Texture2D _px;

    public static ScreenFade Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("ScreenFade");
        DontDestroyOnLoad(go);
        return go.AddComponent<ScreenFade>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // Fade to black, do the thing while nobody can see it, fade back.
    //
    // Overlapping calls are dropped rather than queued: a second wipe starting mid-first would move the
    // player twice from a position the first one had not finished taking them to.
    public static void Cut(Action atBlack, float outSeconds = 0.22f, float holdSeconds = 0.08f,
                           float inSeconds = 0.34f)
    {
        var fade = Ensure();
        if (fade._busy) return;
        fade.StartCoroutine(fade.Run(atBlack, outSeconds, holdSeconds, inSeconds));
    }

    // Black, now, and stay that way until somebody fades it back in. For a scene that opens with the lights
    // off: called from a Start(), it is up before the first frame is drawn rather than a frame after it.
    public static void HoldBlack()
    {
        var fade = Ensure();
        fade.StopAllCoroutines();
        fade._alpha = 1f;
        fade._busy = true;
    }

    // The other half of HoldBlack: sit in the dark, then come up. `atLight` runs when the screen is clear.
    public static void FromBlack(float holdSeconds, float inSeconds, Action atLight = null)
    {
        var fade = Ensure();
        fade.StopAllCoroutines();
        fade.StartCoroutine(fade.Rise(holdSeconds, inSeconds, atLight));
    }

    IEnumerator Rise(float holdSeconds, float inSeconds, Action atLight)
    {
        _busy = true;
        _alpha = 1f;

        for (float held = 0f; held < holdSeconds; held += Time.unscaledDeltaTime) yield return null;

        for (float t = 0f; t < inSeconds; t += Time.unscaledDeltaTime)
        {
            _alpha = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, inSeconds));
            yield return null;
        }

        _alpha = 0f;
        _busy = false;
        atLight?.Invoke();
    }

    IEnumerator Run(Action atBlack, float outSeconds, float holdSeconds, float inSeconds)
    {
        _busy = true;

        for (float t = 0f; t < outSeconds; t += Time.unscaledDeltaTime)
        {
            _alpha = Mathf.Clamp01(t / Mathf.Max(0.01f, outSeconds));
            yield return null;
        }
        _alpha = 1f;
        yield return null;          // one frame fully black before the world changes under it

        atBlack?.Invoke();

        for (float held = 0f; held < holdSeconds; held += Time.unscaledDeltaTime) yield return null;

        for (float t = 0f; t < inSeconds; t += Time.unscaledDeltaTime)
        {
            _alpha = 1f - Mathf.Clamp01(t / Mathf.Max(0.01f, inSeconds));
            yield return null;
        }
        _alpha = 0f;
        _busy = false;
    }

    void OnGUI()
    {
        if (_alpha <= 0f) return;

        if (_px == null)
        {
            _px = new Texture2D(1, 1);
            _px.SetPixel(0, 0, Color.white);
            _px.Apply();
        }

        GUI.depth = -100;   // lower depth draws in front: the wipe covers the HUD, not the other way round
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, _alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _px);
        GUI.color = prev;
    }
}
