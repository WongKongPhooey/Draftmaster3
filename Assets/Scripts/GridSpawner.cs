using UnityEngine;

public class GridSpawner : MonoBehaviour
{
    public TrackBuilder track;
    public GameObject carPrefab;
    public TrackRacingLine racingLine;
    [Tooltip("Material applied to each spawned car's SpriteRenderer. Use an unlit sprite material so cars render correctly without Light2D coverage.")]
    public Material carMaterial;
    [Tooltip("Sorting order applied to each spawned car's SpriteRenderer. Higher draws on top.")]
    public int carSortingOrder = 5;

    [Header("Field")]
    public int count = 6;
    [Tooltip("Distance between cars on the grid, in metres.")]
    public float spacing = 12f;
    [Tooltip("Lateral offset stagger between odd/even rows (m). 0 = single file.")]
    public float rowStagger = 3.5f;
    [Tooltip("Distance behind the start line where the front of the grid sits.")]
    public float gridStartDistance = -20f;
    [Tooltip("Default car speed in metres per second.")]
    public float speed = 35f;
    [Tooltip("Scale to apply to spawned cars.")]
    public Vector2 carScale = new Vector2(6, 6);

    void Start()
    {
        if (track == null || carPrefab == null) return;
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

            var driver = go.GetComponent<SplineDriver>();
            if (driver == null) driver = go.AddComponent<SplineDriver>();
            driver.track = track;
            driver.racingLine = racingLine;
            driver.startDistance = gridStartDistance - i * spacing;
            driver.lateralOffset = (i % 2 == 0) ? rowStagger * 0.5f : -rowStagger * 0.5f;
            driver.speed = speed * Random.Range(0.92f, 1.0f);
            driver.spriteFacesUp = false;
            driver.angleOffsetDeg = 180f;
        }
    }
}
