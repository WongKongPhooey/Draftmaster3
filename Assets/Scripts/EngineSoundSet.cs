using UnityEngine;

/// <summary>
/// A reusable bundle of engine clips for <see cref="EngineAudio"/>. Assign the clip→RPM layers once on an asset
/// and point every car (player + AI via GridSpawner) at it, instead of wiring AudioSources per prefab.
/// </summary>
[CreateAssetMenu(fileName = "EngineSoundSet", menuName = "Sounds/Engine Sound Set", order = 2)]
public class EngineSoundSet : ScriptableObject
{
    [Tooltip("On-throttle (under power) RPM-banked loops.")]
    public EngineAudio.EngineLayer[] onLayers;

    [Tooltip("Off-throttle (coasting / engine-braking) RPM-banked loops. Optional — leave empty for an RPM-only engine.")]
    public EngineAudio.EngineLayer[] offLayers;

    [Tooltip("Gear-shift one-shots (one picked at random per shift).")]
    public AudioClip[] shiftClips;

    [Tooltip("Engine start/crank one-shot, played once when the car wakes.")]
    public AudioClip startClip;
}
