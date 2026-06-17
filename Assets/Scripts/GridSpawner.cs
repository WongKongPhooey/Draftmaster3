using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Draftmaster.Data;
using UnityEngine;

public class GridSpawner : MonoBehaviour
{
    public TrackBuilder track;
    public GameObject carPrefab;
    [Tooltip("Material applied to each spawned car's SpriteRenderer. Use an unlit sprite material so cars render correctly without Light2D coverage.")]
    public Material carMaterial;
    [Tooltip("Sorting order applied to each spawned car's SpriteRenderer. Higher draws on top.")]
    public int carSortingOrder = 5;

    [Header("Field")]
    public int count = 6;
    [Tooltip("Distance between cars on the grid, in metres.")]
    public float spacing = 6f;
    [Tooltip("Lateral offset stagger between odd/even rows (m). 0 = single file.")]
    public float rowStagger = 3.5f;
    [Tooltip("Distance behind the start line where the front of the grid sits.")]
    public float gridStartDistance = -6f;
    [Tooltip("If true, AI start in their pit boxes. If false, they line up on the grid behind the start line. Currently false for testing.")]
    public bool spawnInPit = false;
    [Tooltip("Fallback car speed (m/s) when no VehicleInfo is assigned. Otherwise speed is driven by the vehicle's accel/decel curves.")]
    public float speed = 35f;
    [Tooltip("VehicleInfo asset applied to every spawned AI car. Defines accel/decel/cornering curves.")]
    public VehicleInfo vehicleInfo;
    [Tooltip("Scale to apply to spawned cars.")]
    public Vector2 carScale = new Vector2(6, 6);

    [Header("Collision")]
    [Tooltip("Add VehicleCollision to each spawned car for barrier + car-car contact.")]
    public bool addCollision = true;
    [Tooltip("Box collider half-extents (m). x = half-width, y = half-length.")]
    public Vector2 collisionHalfExtents = new Vector2(1.0f, 2.4f);
    [Tooltip("Layers cars collide against (barriers + other vehicles).")]
    public LayerMask collisionMask = ~0;

    IEnumerator Start()
    {
        if (track == null || carPrefab == null) yield break;

        if (DatabaseManager.Instance == null)
        {
            Debug.LogWarning("GridSpawner: no DatabaseManager in scene — spawning without driver bindings.");
        }
        else
        {
            while (!DatabaseManager.Instance.IsReady) yield return null;
        }

        var drivers = DatabaseManager.Instance != null
            ? DatabaseManager.Instance.Connection.Table<Driver>().ToList()
            : new List<Driver>();

        var pool = new List<Driver>(drivers);
        Shuffle(pool);

        var parent = new GameObject("AIField").transform;
        parent.SetParent(transform, false);

        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(carPrefab, parent);
            go.name = $"AI_{i + 1:D2}";
            go.transform.localScale = new Vector3(carScale.x, carScale.y, 1f);

            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
            {
                if (carMaterial != null) sr.sharedMaterial = carMaterial;
                sr.sortingOrder = carSortingOrder;
            }

            var splineDriver = go.GetComponent<SplineDriver>();
            if (splineDriver == null) splineDriver = go.AddComponent<SplineDriver>();
            splineDriver.track = track;
            splineDriver.vehicleInfo = vehicleInfo;
            float sfAnchor = (track != null && track.track != null) ? track.track.startFinishDistance : 0f;
            splineDriver.startDistance = sfAnchor + gridStartDistance - i * spacing;
            splineDriver.spawnInPit = spawnInPit;
            splineDriver.qualifyingPosition = i;
            splineDriver.lateralOffset = (i % 2 == 0) ? rowStagger * 0.5f : -rowStagger * 0.5f;
            splineDriver.speed = speed;
            splineDriver.spriteFacesUp = false;
            splineDriver.angleOffsetDeg = 180f;

            var binding = go.GetComponent<AIDriverBinding>();
            if (binding == null) binding = go.AddComponent<AIDriverBinding>();
            binding.vehicleInfo = vehicleInfo;
            if (pool.Count > 0) binding.driver = pool[i % pool.Count];
            binding.Apply();

            if (addCollision)
            {
                var col = go.GetComponent<VehicleCollision>();
                if (col == null) col = go.AddComponent<VehicleCollision>();
                col.halfExtents = collisionHalfExtents;
                col.collisionMask = collisionMask;
                col.ApplyExtents();
            }
        }
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
