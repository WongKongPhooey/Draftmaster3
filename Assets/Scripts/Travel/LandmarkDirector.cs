using UnityEngine;

// Bootstrap for the shared Landmark scene. Reads TravelState.CurrentNodeId and instantiates that
// location's environment prefab from Resources/Landmarks/<locationId>; falls back to the generic
// prefab for the location's type (_junkyard / _engineshop). To give a location a hand-built home,
// duplicate a generic prefab, dress it, and save it as Resources/Landmarks/<locationId>.prefab —
// no code changes needed.
public class LandmarkDirector : MonoBehaviour
{
    [Tooltip("Parent for the instantiated location prefab. Defaults to this transform.")]
    public Transform contentRoot;
    [Tooltip("World-space sign showing the location name.")]
    public TextMesh signLabel;

    void Start()
    {
        var node = TravelGraph.Get(TravelState.CurrentNodeId);

        if (signLabel != null)
            signLabel.text = (node != null ? node.name : "Nowhere, USA").ToUpperInvariant();

        GameObject prefab = null;
        if (node != null)
        {
            prefab = Resources.Load<GameObject>("Landmarks/" + node.id);
            if (prefab == null && node.locationType != TravelLocationType.None)
                prefab = Resources.Load<GameObject>(node.locationType == TravelLocationType.EngineShop
                    ? "Landmarks/_engineshop" : "Landmarks/_junkyard");
        }

        if (prefab != null) Instantiate(prefab, contentRoot != null ? contentRoot : transform);
        else Debug.LogWarning($"LandmarkDirector: no location prefab for '{TravelState.CurrentNodeId}' — empty lot. " +
                              "Run Draftmaster > Travel Map > Build Location Prefabs.");

        // Quest hook: walking a location is its own stat, distinct from driving through it.
        if (node != null)
        {
            PlayerStatsLedger.Increment("walkabout");
            PlayerStatsLedger.Increment("walkabout." + node.id);
        }
    }
}
