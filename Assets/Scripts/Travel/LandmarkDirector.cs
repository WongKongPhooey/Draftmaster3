using UnityEngine;

// Bootstrap for the shared Landmark scene (Draftmaster > Travel Map > Build Roadside Shop Scene). Reads
// TravelState.CurrentNodeId and builds that stop's interior from its layout data (RoadsideShopBuilder): a
// fabricator, engine builder or junkyard, one of several floor plans per kind, picked per visit. A hand-built
// Resources/Landmarks/<locationId>.prefab (or the old generic _junkyard / _engineshop) still wins if present.
public class LandmarkDirector : MonoBehaviour
{
    [Tooltip("Parent for the instantiated location prefab. Defaults to this transform.")]
    public Transform contentRoot;
    [Tooltip("Legacy world-space sign. Switched off at runtime — the location names itself on the title " +
             "card now. Kept so the shared Landmark scene's reference does not dangle.")]
    public TextMesh signLabel;
    [Tooltip("Unlit sprite material for the shop's pieces (GarageUnlit, set by the scene builder). Empty = default.")]
    public Material spriteMaterial;

    void Start()
    {
        var node = TravelGraph.Get(TravelState.CurrentNodeId);

        // Where you are is announced, not lettered: the same card the track name arrives on, and the same
        // one a venue uses when you walk up to it (LocationTitle).
        if (signLabel != null) signLabel.gameObject.SetActive(false);
        SpawnIntroUI.Banner(node != null ? node.name : "Nowhere, USA", TypeLabel(node));

        GameObject prefab = null;
        if (node != null)
        {
            // A hand-built home for this one place, if somebody has made one.
            prefab = Resources.Load<GameObject>("Landmarks/" + node.id);
        }

        if (prefab != null) Instantiate(prefab, contentRoot != null ? contentRoot : transform);
        else
        {
            // The usual case: a roadside shop built from its layout data (RoadsideShopBuilder), one of several
            // floor plans per kind of shop, picked per visit.
            var kind = RoadsideShopBuilder.KindOf(node);
            string id = node != null ? node.id : "";
            var layout = RoadsideShopBuilder.LayoutFor(id, kind, TravelState.Week, out string source);
            RoadsideShopBuilder.Build(layout, contentRoot != null ? contentRoot : transform, new RoadsideShopBuilder.Context
            {
                kind = kind, locationId = id, week = TravelState.Week, material = spriteMaterial,
            });
            var player = FindFirstObjectByType<OnFootController>();
            if (player != null)
            {
                Vector2 spawn = RoadsideShopBuilder.Spawn(layout);
                var origin = (contentRoot != null ? contentRoot : transform).position;
                player.transform.position = new Vector3(origin.x + spawn.x, origin.y + spawn.y, player.transform.position.z);
            }
            Debug.Log($"LandmarkDirector: {kind} at '{id}' from {source}.");
        }

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
        TravelLocationType.EngineShop => "Engine builder",
        TravelLocationType.Fabricator => node.fabricatorTier > 0
            ? $"{Draftmaster.Tracks.FabricatorTiers.Name(node.fabricatorTier)} fabricator" : "Fabricator",
        TravelLocationType.Junkyard => "Junkyard",
        TravelLocationType.TeamFactory => "Your team's factory",
        _ => "",
    };
}
