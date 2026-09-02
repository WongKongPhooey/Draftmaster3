using System.Collections.Generic;
using UnityEngine;

// Which block of the paddock an area lays out.
public enum PaddockLotKind
{
    Motorhomes,   // DriverMotorhomeLot — one RV per driver
    Garages,      // PopupGarageLot — one canopy rig per team entry
}

// The authored footprint of a paddock block.
//
// Both lots are built at play time out of the live field, so until now there was nothing in the editor
// to look at: you set lineDirection/rowGap/gapFromMotorhomes by number, pressed play, and found out.
// Drop one of these in the track package instead (Draftmaster > Tracks > Edit Selected Package In
// Context, then GameObject > Draftmaster > Paddock Lot Area), size its box, and that rectangle IS the
// lot — the rigs are packed inside it and the walkable pocket is cut to match.
//
// AXES. The box's local +X is the direction a line of rigs runs; local +Y is the direction lines stack,
// and it is also the way the bodies point (cabs toward the next line back), so rotating the object
// rotates the whole block. Size the box with the collider's own Edit Collider tool.
//
// WHAT IT OVERRIDES. With an area present the lot ignores its own lineDirection, rowCount, maxPerRow,
// rowGap/lineGap and (for the garages) gapFromMotorhomes — spacing comes from this component and the
// counts are solved from the rectangle. With no area the lots behave exactly as they did: anchored on
// the player's RV and grown by number. One area per kind; the first found wins.
//
// THE PLAYER'S RV never moves. If the scene's RVExterior stands inside the motorhome area, the place
// nearest it is left open and the rest of the field parks around it; if it stands outside, the lot fills
// every place and the player's rig is simply somewhere else.
[RequireComponent(typeof(BoxCollider2D))]
[DisallowMultipleComponent]
public class PaddockLotArea : MonoBehaviour
{
    public static readonly List<PaddockLotArea> Active = new();

    [Tooltip("Which block this rectangle is the footprint of.")]
    public PaddockLotKind kind = PaddockLotKind.Motorhomes;

    [Header("Spacing")]
    [Tooltip("Open ground (m) between one rig and the next along a line. For the garages a rig is its body plus its canopy.")]
    public float gap = 2f;
    [Tooltip("Open ground (m) between one line and the next — the walkway the player comes down.")]
    public float rowGap = 6f;

    [Header("Walkable area")]
    [Tooltip("Cut a PaddockBoundary pocket to this rectangle so the block can be walked into. Only ever " +
             "adds one when the scene already has a boundary — on an unfenced scene it would newly fence the player in.")]
    public bool extendWalkableArea = true;
    [Tooltip("How far (m) the walkable pocket reaches past the rectangle. Big enough to overlap the paddock " +
             "next door, or the player is clamped out of the block they can see.")]
    public float walkPad = 4f;

    [Header("Editor preview")]
    [Tooltip("Rigs the gizmo packs in. 0 = the scene's GridSpawner count, or 43.")]
    public int previewCount = 0;
    [Tooltip("Rig footprint (across a line, along it) the gizmo packs with. Zero = read it off the scene's " +
             "lot components, or the same defaults they ship with.")]
    public Vector2 rigSizeOverride = Vector2.zero;
    public Color gizmoColor = new Color(0.35f, 0.85f, 1f, 0.9f);

    // The lot defaults, so a scene with no lot component in it still previews the real footprint.
    public const float DefaultRvWidth = 3.95f;
    public const float DefaultRvLength = 9.93f;
    public const float DefaultCanopyWidth = 6.5f;

    BoxCollider2D _box;
    BoxCollider2D Box
    {
        get
        {
            if (_box == null) _box = GetComponent<BoxCollider2D>();
            return _box;
        }
    }

    void Awake()
    {
        if (Box != null) Box.isTrigger = true;   // an authoring shape, never a wall
    }

    void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    void OnDisable() { Active.Remove(this); }

    // The area for a block, if one was authored. Falls back to a scan so it works however early it is asked
    // (the lots build a frame or two into the scene, but a disabled object never registered).
    public static PaddockLotArea Find(PaddockLotKind kind)
    {
        for (int i = 0; i < Active.Count; i++)
            if (Active[i] != null && Active[i].kind == kind) return Active[i];

        var all = FindObjectsByType<PaddockLotArea>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i] != null && all[i].kind == kind) return all[i];

        return null;
    }

    // The rectangle in world terms: centre, the frame its axes run in, and its size along those axes.
    public void GetRect(out Vector3 centre, out Quaternion rotation, out float width, out float depth)
    {
        Vector2 size = Box != null ? Box.size : new Vector2(60f, 40f);
        Vector2 offset = Box != null ? Box.offset : Vector2.zero;
        Vector3 scale = transform.lossyScale;

        width = Mathf.Max(0.01f, Mathf.Abs(size.x * scale.x));
        depth = Mathf.Max(0.01f, Mathf.Abs(size.y * scale.y));
        centre = transform.TransformPoint(offset);
        rotation = transform.rotation;
    }

    // Local-space containment rather than Collider2D.OverlapPoint: this is asked while the scene is still
    // assembling itself, and it has to give the same answer in the editor as in play mode.
    public bool Contains(Vector3 worldPos)
    {
        Vector2 size = Box != null ? Box.size : new Vector2(60f, 40f);
        Vector2 offset = Box != null ? Box.offset : Vector2.zero;
        Vector3 local = transform.InverseTransformPoint(new Vector3(worldPos.x, worldPos.y, transform.position.z));
        return Mathf.Abs(local.x - offset.x) <= size.x * 0.5f
            && Mathf.Abs(local.y - offset.y) <= size.y * 0.5f;
    }

    // Pack `count` rigs of the given footprint into the rectangle.
    //
    // `across` is a rig's extent along a line, `depth` its extent across one. The block is centred in the
    // rectangle at the authored spacing; when the field does not fit, the lines are squeezed along their
    // own length first (a lot is easier to read too tight sideways than stacked past its own back edge),
    // and `tight` says the bodies now overlap so the gizmo can say so out loud.
    public bool Solve(int count, float across, float depth, float z,
                      out DriverMotorhomeLot.LineLayout line, out int rows, out bool tight)
    {
        line = default;
        rows = 0;
        tight = false;
        if (count <= 0) return false;

        GetRect(out Vector3 centre, out Quaternion rot, out float width, out float depthAvail);
        across = Mathf.Max(0.1f, across);
        depth = Mathf.Max(0.1f, depth);

        float g = Mathf.Max(0f, gap);
        float rg = Mathf.Max(0f, rowGap);

        int perRowCap = Mathf.Max(1, Mathf.FloorToInt((width - across) / (across + g)) + 1);
        int rowCap = Mathf.Max(1, Mathf.FloorToInt((depthAvail - depth) / (depth + rg)) + 1);

        int perRow = Mathf.Min(count, perRowCap);
        rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)perRow));
        float pitch = across + g;
        float rowPitch = depth + rg;

        // More rigs than the rectangle holds at the authored spacing: squeeze along the lines rather than
        // stack past the back edge, so the block stays inside the footprint that was drawn for it.
        if (rows > rowCap)
        {
            rows = rowCap;
            perRow = Mathf.Max(1, Mathf.CeilToInt(count / (float)rows));
            pitch = perRow > 1 ? (width - across) / (perRow - 1) : 0f;
            tight = pitch < across;
        }

        Vector3 axis = rot * Vector3.right;
        Vector3 front = rot * Vector3.up;

        Vector3 origin = centre
                       - axis * (pitch * (perRow - 1) * 0.5f)
                       - front * (rowPitch * (rows - 1) * 0.5f);
        origin.z = z;

        line = new DriverMotorhomeLot.LineLayout
        {
            origin = origin,
            axis = axis,
            front = front,
            rotation = rot,
            pitch = pitch,
            rowPitch = rowPitch,
            depth = depth,
            perRow = perRow,
        };
        return true;
    }

    // Which place a rig already standing in the lot occupies — the player's RV keeps its authored spot, so
    // the field has to park around whichever place that is.
    public static int NearestPlace(DriverMotorhomeLot.LineLayout line, int places, Vector3 worldPos)
    {
        int best = -1;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < places; i++)
        {
            Vector3 p = line.PlaceAt(i);
            float d = ((Vector2)(p - worldPos)).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = i; }
        }
        return best;
    }

    // The walkable pocket for this block: the rectangle, grown by walkPad so it overlaps the paddock it is
    // reached from. Same disjoint-pocket rule the rest of the paddock uses — inside ANY boundary counts.
    public void InstallWalkablePocket(Transform parent)
    {
        if (!extendWalkableArea || !PaddockBoundary.AnyActive) return;

        GetRect(out Vector3 centre, out Quaternion rot, out float width, out float depth);
        float hw = width * 0.5f + Mathf.Max(0f, walkPad);
        float hd = depth * 0.5f + Mathf.Max(0f, walkPad);
        Vector3 axis = rot * Vector3.right;
        Vector3 front = rot * Vector3.up;

        var go = new GameObject(kind + "LotBoundary");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);   // local space == world space

        var poly = go.AddComponent<PolygonCollider2D>();
        poly.points = new[]
        {
            (Vector2)(centre - axis * hw - front * hd),
            (Vector2)(centre + axis * hw - front * hd),
            (Vector2)(centre + axis * hw + front * hd),
            (Vector2)(centre - axis * hw + front * hd),
        };
        go.AddComponent<PaddockBoundary>();
    }

#if UNITY_EDITOR
    // What the gizmo packs with. The lots are self-installing, so on most scenes there is no component to
    // read and the shipped defaults are what will actually be built.
    public Vector2 PreviewRigSize()
    {
        if (rigSizeOverride.x > 0.01f && rigSizeOverride.y > 0.01f) return rigSizeOverride;

        if (kind == PaddockLotKind.Motorhomes)
        {
            var lot = FindObjectOfType<DriverMotorhomeLot>();
            return lot != null ? new Vector2(lot.rvWidth, lot.rvLength)
                               : new Vector2(DefaultRvWidth, DefaultRvLength);
        }

        var garages = FindObjectOfType<PopupGarageLot>();
        return garages != null ? new Vector2(garages.bodyWidth + garages.canopyWidth, garages.bodyLength)
                               : new Vector2(DefaultRvWidth + DefaultCanopyWidth, DefaultRvLength);
    }

    public int PreviewCount()
    {
        if (previewCount > 0) return previewCount;
        var grid = FindObjectOfType<GridSpawner>();
        return grid != null && grid.count > 0 ? grid.count : 43;
    }

    void OnValidate()
    {
        if (Box != null) Box.isTrigger = true;
        gap = Mathf.Max(0f, gap);
        rowGap = Mathf.Max(0f, rowGap);
        walkPad = Mathf.Max(0f, walkPad);
        previewCount = Mathf.Max(0, previewCount);
    }

    void OnDrawGizmos()
    {
        GetRect(out Vector3 centre, out Quaternion rot, out float width, out float depth);

        // The rectangle itself.
        Gizmos.color = gizmoColor;
        Gizmos.matrix = Matrix4x4.TRS(centre, rot, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, depth, 0.05f));
        Gizmos.matrix = Matrix4x4.identity;

        Vector2 rig = PreviewRigSize();
        int count = PreviewCount();
        if (!Solve(count, rig.x, rig.y, transform.position.z, out var line, out int rows, out bool tight))
            return;

        // Every place a rig will stand, so the block can be checked against the pit garages, the tarmac and
        // the boundary without entering play mode.
        Gizmos.color = tight ? new Color(1f, 0.35f, 0.2f, 0.9f)
                             : new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.55f);
        for (int i = 0; i < count; i++)
        {
            Gizmos.matrix = Matrix4x4.TRS(line.PlaceAt(i), line.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(rig.x, rig.y, 0.02f));
        }
        Gizmos.matrix = Matrix4x4.identity;

        // Which way the lines run, and which way they stack.
        Vector3 axis = rot * Vector3.right, front = rot * Vector3.up;
        Gizmos.color = gizmoColor;
        Vector3 tail = centre - axis * (width * 0.5f);
        Gizmos.DrawLine(tail, tail + axis * Mathf.Min(width, 12f));
        Gizmos.DrawLine(tail, tail + front * Mathf.Min(depth, 12f));

        int spare = rows * line.perRow - count;
        string caption = kind + $" lot — {count} rigs, {rows} line(s) of {line.perRow}, {line.pitch:0.0}m apart";
        if (tight) caption += $"  TIGHT: {line.pitch:0.0}m spacing under a {rig.x:0.0}m body — grow the box";
        else if (spare > 0) caption += $" ({spare} place(s) spare)";

        UnityEditor.Handles.color = tight ? Color.red : gizmoColor;
        UnityEditor.Handles.Label(centre + front * (depth * 0.5f + 1.5f), caption);
    }

    [UnityEditor.MenuItem("GameObject/Draftmaster/Paddock Lot Area (Motorhomes)", false, 12)]
    static void CreateMotorhomeArea(UnityEditor.MenuCommand cmd) => Create(cmd, PaddockLotKind.Motorhomes, 80f, 70f);

    [UnityEditor.MenuItem("GameObject/Draftmaster/Paddock Lot Area (Garages)", false, 13)]
    static void CreateGarageArea(UnityEditor.MenuCommand cmd) => Create(cmd, PaddockLotKind.Garages, 120f, 60f);

    static void Create(UnityEditor.MenuCommand cmd, PaddockLotKind kind, float width, float depth)
    {
        var go = new GameObject(kind + "LotArea");

        // Into whatever is open — the scene, or the track package on a prefab stage — and parented so the
        // stage will actually save it. See PaddockAuthoringStage.
        PaddockAuthoringStage.Place(go, cmd);

        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = new Vector2(width, depth);

        var area = go.AddComponent<PaddockLotArea>();
        area.kind = kind;
        if (kind == PaddockLotKind.Garages) { area.gap = 2.5f; area.rowGap = 7f; }

        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Paddock Lot Area");
        UnityEditor.Selection.activeGameObject = go;
        UnityEditor.EditorGUIUtility.PingObject(go);   // scroll the Hierarchy to it
    }
#endif
}
