using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runtime hierarchy tidier.
//
// A race scene finishes wiring itself up with ~190 root GameObjects: every self-installing HUD, every
// director, and three world-space particle emitters per car (they sit at the root on purpose so a scaled
// car cannot distort them). That is unreadable in the Hierarchy window while debugging.
//
// This files every root object under one of a handful of empty parents at the origin — UI, Particles,
// Environment, Vehicles, Characters, Directors, Cameras, Lighting, Audio, Markers, Misc — so the root
// level reads as a dozen folders instead of a wall of names. The buckets are plain identity Transforms
// at (0,0,0) with scale 1, and reparenting keeps world pose, so nothing an object inherits changes.
//
// Two ways in:
//   * Automatic — RuntimeHierarchyOrganizer installs itself on play and sweeps new roots a few times a
//     second, so anything spawned later gets filed without the spawner knowing about it.
//   * Explicit — a spawner that already knows where its object belongs calls
//     RuntimeHierarchy.Adopt(go, HierarchyGroup.Particles), which files it the instant it is created.
//
// Deliberately skipped: DontDestroyOnLoad objects (they live in their own scene and reparenting would
// drag them into a scene that unloads), Netcode NetworkObjects (runtime reparenting is replicated
// state, not decoration), anything with hideFlags set, and anything carrying a HierarchyIgnore.
public enum HierarchyGroup
{
    UI,
    Particles,
    Environment,
    Vehicles,
    Characters,
    Directors,
    Cameras,
    Lighting,
    Audio,
    Markers,
    Misc
}

// Stick this on a root object that must stay at the root.
[DisallowMultipleComponent]
public class HierarchyIgnore : MonoBehaviour { }

public static class RuntimeHierarchy
{
    // Set false before a scene loads to leave the hierarchy exactly as authored/spawned.
    public static bool Enabled = true;

    static readonly string[] GroupNames =
    {
        "UI", "Particles", "Environment", "Vehicles", "Characters",
        "Directors", "Cameras", "Lighting", "Audio", "Markers", "Misc"
    };

    // One set of buckets per scene, keyed by scene handle — a bucket must live in the same scene as its
    // members or reparenting would migrate objects between scenes (and break additive unloads).
    static readonly Dictionary<int, Transform[]> _buckets = new Dictionary<int, Transform[]>();

    static readonly List<GameObject> _roots = new List<GameObject>(256);

    public static string NameOf(HierarchyGroup group) => GroupNames[(int)group];

    // The parent for a group in the given scene, created on first use.
    public static Transform GetGroup(HierarchyGroup group, Scene scene)
    {
        if (!scene.IsValid()) scene = SceneManager.GetActiveScene();

        if (!_buckets.TryGetValue(scene.handle, out var groups) || groups == null)
        {
            groups = new Transform[GroupNames.Length];
            _buckets[scene.handle] = groups;
        }

        int i = (int)group;
        if (groups[i] == null)
        {
            var go = new GameObject(GroupNames[i]);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            if (scene.IsValid() && scene.isLoaded && go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
            groups[i] = go.transform;
        }
        return groups[i];
    }

    public static Transform GetGroup(HierarchyGroup group) => GetGroup(group, SceneManager.GetActiveScene());

    // File an object under a group, keeping world pose. Only moves roots, so a child of a car stays on
    // the car if this is called on something already parented.
    public static void Adopt(GameObject go, HierarchyGroup group)
    {
        if (!Enabled || go == null) return;
        if (go.transform.parent != null) return;
        if (!CanAdopt(go)) return;
        go.transform.SetParent(GetGroup(group, go.scene), true);
    }

    public static void Adopt(Component c, HierarchyGroup group)
    {
        if (c != null) Adopt(c.gameObject, group);
    }

    // Sweep every root of every loaded scene into its bucket. Cheap enough to run several times a second:
    // objects leave the root list the moment they are filed, so steady state only sees new arrivals.
    public static int Organise()
    {
        if (!Enabled) return 0;
        int moved = 0;
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;
            moved += Organise(scene);
        }
        return moved;
    }

    public static int Organise(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return 0;

        _roots.Clear();
        scene.GetRootGameObjects(_roots);

        int moved = 0;
        for (int i = 0; i < _roots.Count; i++)
        {
            var go = _roots[i];
            if (go == null || !CanAdopt(go)) continue;
            if (IsBucket(go, scene)) continue;

            var parent = GetGroup(Classify(go), scene);
            if (parent == null || parent.gameObject == go) continue;

            go.transform.SetParent(parent, true);
            moved++;
        }
        return moved;
    }

    static bool IsBucket(GameObject go, Scene scene)
    {
        if (!_buckets.TryGetValue(scene.handle, out var groups) || groups == null) return false;
        for (int i = 0; i < groups.Length; i++) if (groups[i] != null && groups[i].gameObject == go) return true;
        return false;
    }

    static bool CanAdopt(GameObject go)
    {
        if (go.hideFlags != HideFlags.None) return false;
        // DontDestroyOnLoad lives in its own scene; pulling it into a real one would destroy it on load.
        if (go.scene.buildIndex == -1 && go.scene.name == "DontDestroyOnLoad") return false;
        if (go.GetComponent<HierarchyIgnore>() != null) return false;

        // Netcode: checked by type name so this file carries no dependency on the Netcode package.
        var comps = go.GetComponents<Component>();
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] == null) continue;
            if (comps[i].GetType().Name == "NetworkObject") return false;
        }
        return true;
    }

    // Work out where an object belongs from what it carries, falling back to its name. Order matters:
    // a car has a Renderer and an AudioSource too, so the specific tests run before the generic ones.
    public static HierarchyGroup Classify(GameObject go)
    {
        bool particles = false, canvas = false, camera = false, light = false, audio = false;
        bool vehicle = false, character = false, environment = false, marker = false, ui = false;
        bool director = false, renderer = false, behaviour = false;

        var comps = go.GetComponents<Component>();
        for (int i = 0; i < comps.Length; i++)
        {
            var c = comps[i];
            if (c == null) continue;                       // missing script
            string n = c.GetType().Name;

            switch (n)
            {
                case "Transform": case "RectTransform": continue;
                case "ParticleSystem": case "ParticleSystemRenderer": case "VisualEffect": particles = true; continue;
                case "Canvas": case "CanvasScaler": case "GraphicRaycaster": case "EventSystem": canvas = true; continue;
                case "Camera": case "CinemachineCamera": case "CinemachineBrain": camera = true; continue;
                case "Light": case "Light2D": light = true; continue;
                case "AudioSource": case "AudioListener": case "AmbienceLoop": audio = true; continue;
            }

            if (IsAny(n, "VehicleLogic", "SplineDriver", "PlayerVehicleController", "VehicleCollision",
                         "VehicleDamage", "AIDriverBinding", "EngineGearbox")) { vehicle = true; continue; }

            if (IsAny(n, "MovementOnFoot", "OnFootController", "NPCInteractable", "NPCAppearance",
                         "PlacedNPC", "AutographFan", "PaddockNPC", "CrowdNPC", "QuestGiverNPC")) { character = true; continue; }

            if (IsAny(n, "TrackBuilder", "TrackPackage", "ExtraTrackSpline", "TrackEnvironmentBuilder",
                         "Grandstand", "RVExterior", "RVInterior", "SurfaceField", "SpawnField",
                         "TrackReferenceImage")) { environment = true; continue; }

            if (IsAny(n, "PlayerSpawnPoint", "PitLaneStart", "PlayerPitBoxMarker", "PaddockBoundary",
                         "CutsceneTrigger")) { marker = true; continue; }

            if (c is Renderer) { renderer = true; continue; }

            if (EndsWithAny(n, "UI", "HUD", "Panel", "Menu", "Overlay", "Feed", "Bubble", "Label",
                               "MiniMap", "Screen", "Ticker", "Readout")) { ui = true; continue; }

            if (EndsWithAny(n, "Manager", "Director", "Spawner", "Controller", "Handler", "Service",
                               "Tuner", "Diagnostics", "Lot", "Binding")) { director = true; continue; }

            if (c is Behaviour) behaviour = true;
        }

        if (vehicle) return HierarchyGroup.Vehicles;
        if (character) return HierarchyGroup.Characters;
        if (particles) return HierarchyGroup.Particles;
        if (canvas || ui) return HierarchyGroup.UI;
        if (environment) return HierarchyGroup.Environment;
        if (marker) return HierarchyGroup.Markers;
        if (camera) return HierarchyGroup.Cameras;
        if (light) return HierarchyGroup.Lighting;
        if (director) return HierarchyGroup.Directors;
        if (audio && !renderer) return HierarchyGroup.Audio;

        // Nothing decisive on the object itself — read the name. Containers authored as bare Transforms
        // ("NPCs", "Grandstands", "PlayerSpawnPoints") land here.
        string name = go.name;
        if (ContainsAny(name, "Canvas", "HUD", "UI", "Menu", "Bubble", "Dialogue", "Panel")) return HierarchyGroup.UI;
        if (ContainsAny(name, "Spray", "Spark", "Debris", "Smoke", "Dust", "Explosion", "Particle")) return HierarchyGroup.Particles;
        // Markers first: a container called "PlayerSpawnPoints" is a set of markers, not a person.
        if (ContainsAny(name, "SpawnPoint", "Marker", "Boundary", "Trigger", "Waypoint")) return HierarchyGroup.Markers;
        if (ContainsAny(name, "NPC", "Crew", "Fan", "Driver", "Player")) return HierarchyGroup.Characters;
        if (ContainsAny(name, "Track", "Ground", "Wall", "Grandstand", "Environment", "RV", "Paddock",
                              "Pit", "Road", "Route", "Escape")) return HierarchyGroup.Environment;
        if (ContainsAny(name, "Manager", "Director", "Spawner", "Controller", "System")) return HierarchyGroup.Directors;

        if (renderer) return HierarchyGroup.Environment;
        if (behaviour) return HierarchyGroup.Directors;
        return HierarchyGroup.Misc;
    }

    static bool IsAny(string value, params string[] options)
    {
        for (int i = 0; i < options.Length; i++) if (value == options[i]) return true;
        return false;
    }

    static bool EndsWithAny(string value, params string[] suffixes)
    {
        for (int i = 0; i < suffixes.Length; i++) if (value.EndsWith(suffixes[i], System.StringComparison.Ordinal)) return true;
        return false;
    }

    static bool ContainsAny(string value, params string[] parts)
    {
        for (int i = 0; i < parts.Length; i++) if (value.IndexOf(parts[i], System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    // Buckets are per-scene and die with their scene; drop stale handles so the dictionary cannot grow.
    internal static void ForgetScene(Scene scene) => _buckets.Remove(scene.handle);

    internal static void ForgetAll() => _buckets.Clear();
}
