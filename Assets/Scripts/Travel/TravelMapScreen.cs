using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The between-races road trip: a fullscreen authored Canvas (Resources/UI/TravelMap.prefab) with this
// thin binder on the root. Node layout is editable in Prefab Mode — each node is a TravelNodeMarker
// child whose RectTransform position is the node's map position; highway lines are rebuilt from marker
// positions at runtime (and via the "Rebuild Edges" context menu while editing). TravelGraph supplies
// topology and shop data only. Flow (unchanged from the old IMGUI version):
//   1. Choose the next race venue (any circuit node). Stop budget = direct route + DetourAllowance.
//   2. Drive node to node (click an adjacent node, 1 stop each). Minor locations show a side panel —
//      junkyards sell a weekly random salvage roll, engine shops a fixed catalog. Buying installs.
//   3. At the destination: START RACE WEEKEND resets the weekend and loads the circuit's scene (falls
//      back to reloading the current scene when the circuit isn't in the build).
// Out of stops away from the destination -> tow (costs money). State persists in TravelState, so closing
// the panel or the game mid-trip loses nothing. F9 opens the map any time (dev convenience).
// The prefab is generated once by Draftmaster > Travel Map > Build Prefab (see TravelMapPrefabBuilder).
public class TravelMapScreen : MonoBehaviour
{
    public static TravelMapScreen Instance { get; private set; }
    public static bool IsOpen => Instance != null;

    const int TowCost = 2000;
    const string PrefabResourcePath = "UI/TravelMap";

    static readonly Color CircuitColor = new Color(1f, 0.85f, 0.3f);
    static readonly Color JunkyardColor = new Color(0.75f, 0.55f, 0.35f);
    static readonly Color EngineShopColor = new Color(0.45f, 0.75f, 1f);
    static readonly Color MysteryColor = new Color(0.6f, 0.6f, 0.6f);
    static readonly Color CurrentHalo = new Color(0.4f, 1f, 0.5f, 0.9f);
    static readonly Color DestHalo = new Color(1f, 0.35f, 0.3f, 0.9f);
    static readonly Color ReachableHalo = new Color(1f, 1f, 1f, 0.35f);
    static readonly Color EdgeColor = new Color(1f, 1f, 1f, 0.22f);

    [Header("Header")]
    public Text titleLabel;
    public Text subLabel;
    public Text cashLabel;
    public Button closeButton;

    [Header("Map")]
    public RectTransform nodesRoot;   // TravelNodeMarker children — authored, draggable
    public RectTransform edgesRoot;   // highway lines — regenerated from marker positions

    [Header("Side panel")]
    public Text carRowsLabel;
    public Text locationHeader;
    public Text flavorLabel;
    public Button actionButton;       // context-dependent: START RACE WEEKEND or TOW
    public Button walkButton;         // minor locations: park and walk around (Landmark scene)
    public Text shopHeader;
    public RectTransform shopRowsRoot;
    public GameObject stockRowTemplate; // inactive child of shopRowsRoot, cloned per part

    [Header("Notice")]
    public Text noticeLabel;

    readonly Dictionary<string, TravelNodeMarker> _markers = new();
    float _noticeUntil;

    // ---------------- lifecycle ----------------

    public static void Open()
    {
        if (Instance != null) return;
        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"TravelMap prefab missing at Resources/{PrefabResourcePath} — run Draftmaster > Travel Map > Build Prefab.");
            return;
        }
        Instantiate(prefab); // Awake sets Instance
    }

    public static void Close()
    {
        if (Instance != null) Destroy(Instance.gameObject);
    }

    void Awake()
    {
        Instance = this;

        // uGUI needs an EventSystem; race scenes don't all have one.
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        CacheMarkers();
        BuildEdges();

        closeButton.onClick.AddListener(Close);
        foreach (var m in _markers.Values)
        {
            var marker = m; // capture
            marker.button.onClick.AddListener(() => OnNodeClicked(marker));
        }

        noticeLabel.text = "";
        Refresh();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Update()
    {
        if (noticeLabel.text.Length > 0 && Time.unscaledTime >= _noticeUntil)
            noticeLabel.text = "";
    }

    // --- Dev hotkey: F9 toggles the map in any scene (mirrors QuestHUD's self-bootstrap pattern). ---
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void BootstrapHotkey()
    {
        var go = new GameObject("TravelMapHotkey");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<TravelMapHotkey>();
    }

    class TravelMapHotkey : MonoBehaviour
    {
        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame)
            {
                if (IsOpen) Close(); else Open();
            }
        }
    }

    void Notice(string msg)
    {
        noticeLabel.text = msg;
        _noticeUntil = Time.unscaledTime + 3f;
    }

    // ---------------- map ----------------

    void CacheMarkers()
    {
        _markers.Clear();
        foreach (var m in nodesRoot.GetComponentsInChildren<TravelNodeMarker>(true))
        {
            if (m.Node == null) { Debug.LogWarning($"TravelNodeMarker '{m.name}' has unknown nodeId '{m.nodeId}'"); continue; }
            _markers[m.nodeId] = m;
        }
        foreach (var n in TravelGraph.Nodes)
            if (!_markers.ContainsKey(n.id))
                Debug.LogWarning($"TravelGraph node '{n.id}' has no marker in the prefab — run Draftmaster > Travel Map > Sync Node Markers.");
    }

    // Highway lines from marker positions, so they follow when nodes are dragged in Prefab Mode.
    // Editor preview: right-click the component header -> Rebuild Edges.
    [ContextMenu("Rebuild Edges")]
    public void BuildEdges()
    {
        if (_markers.Count == 0) CacheMarkers();

        for (int i = edgesRoot.childCount - 1; i >= 0; i--)
        {
            var child = edgesRoot.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }

        foreach (var (a, b) in TravelGraph.Edges)
        {
            if (!_markers.TryGetValue(a, out var ma) || !_markers.TryGetValue(b, out var mb)) continue;
            Vector2 pa = ma.transform.localPosition, pb = mb.transform.localPosition;
            Vector2 d = pb - pa;
            float len = d.magnitude;
            if (len < 1f) continue;

            var go = new GameObject($"Edge_{a}_{b}", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(edgesRoot, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.localPosition = pa;
            rt.sizeDelta = new Vector2(len, 2f);
            rt.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var img = go.GetComponent<Image>();
            img.color = EdgeColor;
            img.raycastTarget = false;
        }
    }

    void OnNodeClicked(TravelNodeMarker marker)
    {
        var n = marker.Node;
        var current = TravelGraph.Get(TravelState.CurrentNodeId);
        var dest = TravelGraph.Get(TravelState.DestinationId);
        bool choosing = dest == null;
        bool isCurrent = n == current;
        bool reachable = !choosing && TravelGraph.AreAdjacent(current.id, n.id) && TravelState.StopsLeft > 0;

        if (isCurrent) { Refresh(); return; }
        if (choosing)
        {
            if (n.isCircuit && TravelState.ChooseDestination(n.id))
                Notice($"Destination set: {n.name}. {TravelState.StopsLeft} stops for a {TravelGraph.ShortestHops(TravelState.CurrentNodeId, n.id)}-stop direct run.");
            Refresh();
            return;
        }
        if (reachable && TravelState.MoveTo(n.id))
        {
            if (!n.isCircuit) Notice($"Pulled in at {n.name}.");
            Refresh();
        }
    }

    // ---------------- refresh (restyle everything from state) ----------------

    void Refresh()
    {
        var current = TravelGraph.Get(TravelState.CurrentNodeId);
        var dest = TravelGraph.Get(TravelState.DestinationId);
        if (TravelState.HasDestination && dest == null) TravelState.DestinationId = ""; // stale save vs renamed node
        bool choosing = dest == null;

        titleLabel.text = choosing
            ? "THE ROAD    —    choose your next race (click a circuit)"
            : $"THE ROAD TO {dest.name.ToUpperInvariant()}";
        subLabel.text = choosing
            ? $"Week {TravelState.Week}   ·   You are at {current.name}"
            : $"Week {TravelState.Week}   ·   At {current.name}   ·   STOPS LEFT: {TravelState.StopsLeft}";
        cashLabel.text = PlayerWallet.CashText;

        RefreshMarkers(choosing, current, dest);
        RefreshSidePanel(choosing, current, dest);
    }

    void RefreshMarkers(bool choosing, TravelNode current, TravelNode dest)
    {
        foreach (var marker in _markers.Values)
        {
            var n = marker.Node;
            bool isCurrent = n == current;
            bool isDest = n == dest;
            bool adjacent = TravelGraph.AreAdjacent(current.id, n.id);
            bool reachable = !choosing && adjacent && TravelState.StopsLeft > 0;
            bool clickable = (choosing && n.isCircuit && !isCurrent) || reachable || isCurrent;

            marker.button.interactable = clickable;

            if (isCurrent) { marker.halo.enabled = true; marker.halo.color = CurrentHalo; }
            else if (isDest) { marker.halo.enabled = true; marker.halo.color = DestHalo; }
            else if (reachable || (choosing && n.isCircuit)) { marker.halo.enabled = true; marker.halo.color = ReachableHalo; }
            else marker.halo.enabled = false;

            marker.dot.color = n.isCircuit
                ? CircuitColor
                : (TravelState.IsVisited(n.id)
                    ? (n.locationType == TravelLocationType.Junkyard ? JunkyardColor : EngineShopColor)
                    : MysteryColor);

            if (n.isCircuit) marker.label.text = n.name;
            else if (TravelState.IsVisited(n.id)) marker.label.text = (n.locationType == TravelLocationType.Junkyard ? "[J] " : "[E] ") + n.name;
            else marker.label.text = "?";
        }
    }

    // ---------------- side panel ----------------

    void RefreshSidePanel(bool choosing, TravelNode current, TravelNode dest)
    {
        // Car build summary — what's installed, so shop comparisons are one glance.
        var sb = new StringBuilder();
        foreach (PartSlot slot in System.Enum.GetValues(typeof(PartSlot)))
            sb.AppendLine(PlayerCarBuild.DescribeSlot(slot));
        carRowsLabel.text = sb.ToString().TrimEnd();

        ClearShopRows();
        actionButton.gameObject.SetActive(false);
        walkButton.gameObject.SetActive(false);
        shopHeader.gameObject.SetActive(false);

        if (choosing)
        {
            locationHeader.text = "";
            flavorLabel.text = "Pick the next race on the map. Your stop budget covers the direct route plus a small detour — plan it past somewhere useful.";
            return;
        }

        locationHeader.text = current.name.ToUpperInvariant();
        string flavor;

        if (current.isCircuit)
        {
            if (current == dest)
            {
                flavor = "This is the place. Time to go racing.";
                ShowAction("START RACE WEEKEND", () => StartRaceWeekend(current));
            }
            else flavor = $"A racetrack, but not this week's. {dest.name} is waiting.";
        }
        else
        {
            flavor = current.flavor;
            BuildShopRows(current);

            // Park up and walk the place on foot (shared Landmark scene; costs nothing).
            walkButton.gameObject.SetActive(true);
            walkButton.GetComponentInChildren<Text>().text = "STOP & LOOK AROUND";
            walkButton.onClick.RemoveAllListeners();
            walkButton.onClick.AddListener(() =>
            {
                if (!LandmarkLoader.SceneInBuild)
                {
                    Notice("Landmark scene missing from Build Settings — run Draftmaster > Travel Map > Build Landmark Scene.");
                    return;
                }
                Close();
                LandmarkLoader.Visit(TravelState.CurrentNodeId);
            });
        }

        // Stranded? Tow covers the rest of the way, for a price. Never a softlock.
        if (current != dest && TravelState.StopsLeft <= 0)
        {
            flavor += "\n\nOut of stops. The flatbed knows the way.";
            ShowAction($"TOW TO {dest.name.ToUpperInvariant()} ({PlayerWallet.Format(TowCost)})", () =>
            {
                PlayerWallet.Add(-TowCost); // clamped at $0 — the tow always runs
                TravelState.CurrentNodeId = dest.id;
                Notice($"Towed to {dest.name}. The driver talked the whole way.");
                Refresh();
            });
        }

        flavorLabel.text = flavor;
    }

    void ShowAction(string label, UnityEngine.Events.UnityAction onClick)
    {
        actionButton.gameObject.SetActive(true);
        actionButton.GetComponentInChildren<Text>().text = label;
        actionButton.onClick.RemoveAllListeners();
        actionButton.onClick.AddListener(onClick);
    }

    void ClearShopRows()
    {
        for (int i = shopRowsRoot.childCount - 1; i >= 0; i--)
        {
            var child = shopRowsRoot.GetChild(i).gameObject;
            if (child != stockRowTemplate) Destroy(child);
        }
        shopRowsRoot.sizeDelta = new Vector2(shopRowsRoot.sizeDelta.x, 0f);
    }

    void BuildShopRows(TravelNode loc)
    {
        if (loc.locationType == TravelLocationType.None) return;
        shopHeader.gameObject.SetActive(true);

        int count = 0;
        if (loc.locationType == TravelLocationType.Junkyard)
        {
            shopHeader.text = "SALVAGE THIS WEEK";
            foreach (var (part, price) in PartCatalog.JunkyardStock(loc.id, TravelState.Week))
            {
                if (TravelState.WasBought(loc.id, part.id)) continue;
                AddStockRow(part, price, () => TravelState.MarkBought(loc.id, part.id));
                count++;
            }
        }
        else // EngineShop
        {
            shopHeader.text = "FOR SALE";
            if (loc.shopStock != null)
                foreach (var id in loc.shopStock)
                {
                    var part = PartCatalog.Get(id);
                    if (part == null) continue;
                    AddStockRow(part, part.price, null);
                    count++;
                }
        }

        // Outer layout group reads child heights, so the container must claim its own space.
        float rowH = ((RectTransform)stockRowTemplate.transform).sizeDelta.y + 4f;
        shopRowsRoot.sizeDelta = new Vector2(shopRowsRoot.sizeDelta.x, count * rowH);
    }

    void AddStockRow(PartDef part, int price, System.Action onBought)
    {
        var row = Instantiate(stockRowTemplate, shopRowsRoot);
        row.SetActive(true);

        bool installed = PlayerCarBuild.InstalledId(part.slot) == part.id;
        row.transform.Find("NameLabel").GetComponent<Text>().text = part.name + (installed ? "   [INSTALLED]" : "");
        row.transform.Find("EffectLabel").GetComponent<Text>().text = part.EffectSummary() + "\n" + PlayerCarBuild.DescribeSlot(part.slot);

        var buy = row.transform.Find("BuyButton").GetComponent<Button>();
        if (installed) buy.gameObject.SetActive(false);
        else
        {
            buy.GetComponentInChildren<Text>().text = PlayerWallet.Format(price);
            buy.onClick.AddListener(() =>
            {
                if (PlayerWallet.TrySpend(price))
                {
                    PlayerCarBuild.Install(part);
                    PlayerStatsLedger.Increment("partsbought");
                    onBought?.Invoke();
                    Notice($"{part.name} installed. Old {part.slot.ToString().ToLowerInvariant()} scrapped.");
                }
                else Notice("Not enough cash.");
                Refresh();
            });
        }
    }

    void StartRaceWeekend(TravelNode circuit)
    {
        TravelState.ArriveAndClearDestination();
        RaceWeekend.ResetWeekend();
        // Load the circuit's scene when it's in the build; otherwise re-run the current (dev) scene —
        // the travel position still advances, so the map keeps working before every track is wired up.
        string scene = Application.CanStreamedLevelBeLoaded(circuit.id) ? circuit.id : SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(scene);
    }
}
