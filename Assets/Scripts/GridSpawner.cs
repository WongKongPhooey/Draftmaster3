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
    [Tooltip("Base car speed in metres per second; driver stats scale this.")]
    public float speed = 35f;
    [Tooltip("Scale to apply to spawned cars.")]
    public Vector2 carScale = new Vector2(6, 6);

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
            splineDriver.startDistance = gridStartDistance - i * spacing;
            splineDriver.lateralOffset = (i % 2 == 0) ? rowStagger * 0.5f : -rowStagger * 0.5f;
            splineDriver.speed = speed;
            splineDriver.spriteFacesUp = false;
            splineDriver.angleOffsetDeg = 180f;

            if (pool.Count > 0)
            {
                var binding = go.GetComponent<AIDriverBinding>();
                if (binding == null) binding = go.AddComponent<AIDriverBinding>();
                binding.driver = pool[i % pool.Count];
                binding.baseSpeed = speed;
                binding.Apply();
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
