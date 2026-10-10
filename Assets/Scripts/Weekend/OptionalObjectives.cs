using System;
using System.Collections.Generic;
using UnityEngine;

// Side objectives: things worth doing that nobody is waiting on — "find the team e-scooter" on the way to the
// briefing. The weekend's booking stays THE objective: it owns the quest strip, Tab and the main marker.
// An optional one only gets a marker of its own at the edge of the screen, drawn alongside the main one
// rather than competing with it (SpawnIntroUI's companion markers, tinted so the two never read as a guess
// about where the player is due), and a line under OPTIONAL in the phone's Tasks app.
//
// Whoever owns an errand adds it and removes it; this only keeps its marker on whichever SpawnIntroUI the
// scene has, re-hanging it after a scene load and taking it down when the errand ends or its target goes.
public class OptionalObjectives : MonoBehaviour
{
    public class Objective
    {
        public string id;
        public string title;           // "Find the team e-scooter"
        public string hint;            // the line under it in Tasks
        public Func<Transform> target; // asked every frame: the thing may not exist yet, or be rebuilt
    }

    // Light blue: apart from the white of the booking's marker and the co-op partner's tint.
    public static readonly Color Tint = new Color(0.45f, 0.80f, 1f, 1f);

    static readonly List<Objective> _active = new();
    public static IReadOnlyList<Objective> Active => _active;

    static OptionalObjectives _instance;

    // Where each objective's marker is hung right now, so a changed target or a new SpawnIntroUI is noticed.
    readonly Dictionary<string, Transform> _hung = new();
    SpawnIntroUI _intro;

    public static void Add(Objective objective, bool pulse = true)
    {
        if (objective == null || string.IsNullOrEmpty(objective.id)) return;
        _active.RemoveAll(o => o.id == objective.id);
        _active.Add(objective);
        Ensure();
        if (pulse) _instance._pulse.Add(objective.id);
    }

    public static void Remove(string id)
    {
        if (_active.RemoveAll(o => o.id == id) == 0) return;
        if (_instance != null) _instance.Unhang(id);
    }

    public static bool IsActive(string id) => _active.Exists(o => o.id == id);

    readonly HashSet<string> _pulse = new();

    static void Ensure()
    {
        if (_instance != null) return;
        var go = new GameObject("OptionalObjectives");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<OptionalObjectives>();
    }

    void OnDestroy() { if (_instance == this) _instance = null; }

    void LateUpdate()
    {
        var intro = SpawnIntroUI.Instance;
        if (intro != _intro)
        {
            _intro = intro;
            _hung.Clear();   // a new scene's UI starts with no markers of ours on it
        }
        if (intro == null) return;

        for (int i = 0; i < _active.Count; i++)
        {
            var o = _active[i];
            Transform target = null;
            try { target = o.target?.Invoke(); } catch (Exception e) { Debug.LogException(e); }

            _hung.TryGetValue(o.id, out var was);
            if (target == was && target != null)
            {
                if (_pulse.Remove(o.id)) intro.PulseMarker(target);
                continue;
            }

            if (was != null) intro.RemoveMarker(was);
            if (target == null) { _hung.Remove(o.id); continue; }

            var sprite = target.GetComponentInChildren<SpriteRenderer>();
            intro.AddCompanionMarker(target, sprite != null ? sprite.sprite : null, "OPTIONAL · " + o.title, Tint);
            if (_pulse.Remove(o.id)) intro.PulseMarker(target);
            _hung[o.id] = target;
        }
    }

    void Unhang(string id)
    {
        _pulse.Remove(id);
        if (!_hung.TryGetValue(id, out var target)) return;
        _hung.Remove(id);
        if (_intro != null && target != null) _intro.RemoveMarker(target);
    }
}
