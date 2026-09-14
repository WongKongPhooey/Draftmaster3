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
    [Tooltip("Legacy world-space sign. Switched off at runtime — the location names itself on the title " +
             "card now. Kept so the shared Landmark scene's reference does not dangle.")]
    public TextMesh signLabel;

    void Start()
    {
        var node = TravelGraph.Get(TravelState.CurrentNodeId);

        // Where you are is announced, not lettered: the same card the track name arrives on, and the same
        // one a venue uses when you walk up to it (LocationTitle).
        if (signLabel != null) signLabel.gameObject.SetActive(false);
        SpawnIntroUI.Banner((node != null ? node.name : "Nowhere, USA").ToUpperInvariant(), TypeLabel(node));

        GameObject prefab = null;
        if (node != null)
        {
            prefab = Resources.Load<GameObject>("Landmarks/" + node.id);
            // No hand-built home yet: fall back to the generic lot for the kind of place it is. The team
            // factory borrows the engine-shop lot rather than the junkyard one - it is a working shop.
            if (prefab == null && node.locationType != TravelLocationType.None)
                prefab = Resources.Load<GameObject>(node.locationType == TravelLocationType.Junkyard
                    ? "Landmarks/_junkyard" : "Landmarks/_engineshop");
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

    // The line under the name: what kind of place this is, when it is one.
    static string TypeLabel(TravelNode node) => node == null ? "" : node.locationType switch
    {
        TravelLocationType.EngineShop => "Engine shop",
        TravelLocationType.Junkyard => "Junkyard",
        TravelLocationType.TeamFactory => "Your team's factory",
        _ => "",
    };
}
