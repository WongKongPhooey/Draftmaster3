using UnityEngine;
using UnityEngine.UI;

// One node on the authored travel-map prefab. Drag these around in Prefab Mode to lay out the USA —
// the marker's RectTransform position IS the node's map position (TravelGraph only supplies topology:
// edges, BFS, shop data). nodeId must match a TravelGraph node id (circuits: the scene name).
// Highway lines are rebuilt from marker positions at runtime, so they follow wherever you drag.
public class TravelNodeMarker : MonoBehaviour
{
    public string nodeId;

    [Header("Wired by the prefab builder")]
    public Image halo;   // ring behind the dot: green=you, red=destination, white=reachable
    public Image dot;
    public Text label;
    public Button button;

    public TravelNode Node => TravelGraph.Get(nodeId);
}
