using System.Collections.Generic;
using UnityEngine;

// Walkable-area boundary for the on-foot layer. Draw the polygon with the PolygonCollider2D's
// built-in "Edit Collider" tool (it's kept a trigger — it never physically collides, it's just the
// shape). While at least one boundary is active in the scene, the player (OnFootController) and
// wandering NPCs (PaddockWalker) cannot move outside it: positions are clamped to the nearest
// point inside. Multiple boundaries are allowed — being inside ANY of them counts as inside, so
// disjoint walkable pockets (paddock + a viewing area) work.
//
// No boundary in the scene = no constraint, so existing scenes are unaffected.
[RequireComponent(typeof(PolygonCollider2D))]
public class PaddockBoundary : MonoBehaviour
{
    public static readonly List<PaddockBoundary> Active = new();
    public static bool AnyActive => Active.Count > 0;

    // Raised whenever the walkable area changes shape — a boundary turning up or going away. Anything that
    // parked something inside the paddock needs to hear about it, because the boundaries are generated
    // (the motorhome lot brings its own) and can appear after whatever measured the paddock first.
    public static event System.Action Changed;

    [Tooltip("Editor-only gizmo colour for this boundary's outline.")]
    public Color gizmoColor = new Color(1f, 0.6f, 0.1f, 0.9f);

    PolygonCollider2D _poly;

    void Awake()
    {
        _poly = GetComponent<PolygonCollider2D>();
        _poly.isTrigger = true; // never a physical wall — containment is done by clamping
    }

    void OnEnable()
    {
        if (!Active.Contains(this)) Active.Add(this);
        Changed?.Invoke();
    }

    void OnDisable()
    {
        Active.Remove(this);
        Changed?.Invoke();
    }

    public bool Contains(Vector2 worldPos)
    {
        if (_poly == null) _poly = GetComponent<PolygonCollider2D>();
        return _poly.OverlapPoint(worldPos);
    }

    // Clamp a world position to the walkable area. Inside any active boundary = unchanged;
    // outside = the nearest point on the nearest boundary's edge.
    public static Vector2 Constrain(Vector2 worldPos)
    {
        if (Active.Count == 0) return worldPos;

        Vector2 best = worldPos;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < Active.Count; i++)
        {
            var b = Active[i];
            if (b == null || b._poly == null) continue;
            if (b._poly.OverlapPoint(worldPos)) return worldPos;
            Vector2 p = b._poly.ClosestPoint(worldPos);
            float d = (p - worldPos).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = p; }
        }
        return best;
    }

    // True if worldPos is inside any active boundary (or there are none).
    public static bool IsInside(Vector2 worldPos)
    {
        if (Active.Count == 0) return true;
        for (int i = 0; i < Active.Count; i++)
            if (Active[i] != null && Active[i].Contains(worldPos)) return true;
        return false;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        var poly = GetComponent<PolygonCollider2D>();
        if (poly != null) poly.isTrigger = true;
    }

    void OnDrawGizmos()
    {
        var poly = GetComponent<PolygonCollider2D>();
        if (poly == null) return;

        Gizmos.color = gizmoColor;
        for (int path = 0; path < poly.pathCount; path++)
        {
            var pts = poly.GetPath(path);
            if (pts == null || pts.Length < 2) continue;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector3 a = transform.TransformPoint(pts[i] + poly.offset);
                Vector3 b = transform.TransformPoint(pts[(i + 1) % pts.Length] + poly.offset);
                Gizmos.DrawLine(a, b);
                Gizmos.DrawWireSphere(a, 0.35f);
            }
        }

        UnityEditor.Handles.color = gizmoColor;
        UnityEditor.Handles.Label(transform.position + new Vector3(0.5f, 0.5f, 0f), "Paddock Boundary");
    }

    [UnityEditor.MenuItem("GameObject/Draftmaster/Paddock Boundary", false, 11)]
    static void CreateBoundary(UnityEditor.MenuCommand cmd)
    {
        var go = new GameObject("PaddockBoundary");
        var poly = go.AddComponent<PolygonCollider2D>();
        poly.isTrigger = true;
        go.AddComponent<PaddockBoundary>();
        UnityEditor.GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);

        // Start as a generous rectangle at the scene-view pivot; reshape with Edit Collider.
        var view = UnityEditor.SceneView.lastActiveSceneView;
        Vector3 pivot = view != null ? view.pivot : Vector3.zero;
        if (go.transform.parent == null) go.transform.position = new Vector3(pivot.x, pivot.y, 0f);
        poly.SetPath(0, new[]
        {
            new Vector2(-15f, -10f), new Vector2(15f, -10f),
            new Vector2(15f, 10f), new Vector2(-15f, 10f)
        });

        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Paddock Boundary");
        UnityEditor.Selection.activeObject = go;
    }
#endif
}
