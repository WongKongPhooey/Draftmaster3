using System.Collections.Generic;
using UnityEngine;

// Flat coloured shapes for the things the weekend needs stood in the paddock — the drivers' room, the fan
// fence, the hospitality tent, the intro stage, the seats inside the room.
//
// The same quad recipe RVInterior builds its furniture from, pulled out so the venue builders share it:
// double-sided (never backface-culled whichever side the camera is on) and drawn at a negative local z so
// it sits in front of the tarmac rather than inside it. Placeholder art on purpose — these are blocked out
// to be walked around now and repainted later, exactly as the RV interior was.
public static class PaddockProps
{
    // Depths, relative to the ground plane. Lower (more negative) is nearer the camera.
    public const float FloorZ = -0.05f;
    public const float PropZ = -0.15f;
    public const float WallZ = -0.25f;

    static readonly List<Material> Owned = new();

    public static Material Unlit(Color c)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var m = new Material(shader) { name = "PaddockProp" };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        Owned.Add(m);
        return m;
    }

    // Axis-aligned quad in the parent's local space. `solid` gives it a collider, which is what makes a
    // wall a wall — the on-foot player is a Rigidbody2D and walks into it.
    public static GameObject Quad(Transform parent, string name, Vector2 centreLocal, Vector2 size,
                                  float localZ, Material mat, bool solid = false, int sortingOrder = 0)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(centreLocal.x, centreLocal.y, localZ);
        if (solid) go.AddComponent<BoxCollider2D>().size = size;

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        // Depth alone does not settle a mesh against a sprite, and the people in this paddock are sprites.
        // Anything meant to be stood ON says so here rather than hoping the z ordering holds.
        mr.sortingOrder = sortingOrder;

        float hx = size.x * 0.5f, hy = size.y * 0.5f;
        var mesh = new Mesh { name = name };
        mesh.SetVertices(new List<Vector3> { new(-hx, -hy, 0f), new(hx, -hy, 0f), new(hx, hy, 0f), new(-hx, hy, 0f) });
        mesh.SetUVs(0, new List<Vector2> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) });
        mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 }, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mf.sharedMesh = mesh;
        return go;
    }

    // Writing on a prop — a board, a hoarding, a number on a bay: signage a real circuit would have.
    //
    // NOT for naming a place. A venue introduces itself on the title card when the player walks up to it
    // (LocationTitle), because a name lettered across the ground reads as a label stuck to the scene.
    public static GameObject Sign(Transform parent, string text, Vector2 centreLocal, float widthM,
                                  Color colour, float localZ = PropZ - 0.05f)
    {
        var go = new GameObject("Sign_" + text);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(centreLocal.x, centreLocal.y, localZ);

        // Signs read the same way up whichever way the building is turned. A prop can be rotated to face
        // pit road; the writing on it is for the player looking down at the paddock, and inheriting the
        // parent's rotation prints it mirrored or upside down at half the tracks.
        go.transform.rotation = Quaternion.identity;

        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = colour;
        tm.fontSize = 64;
        tm.characterSize = 1f;

        // TextMesh authors at 10 units per "character size"; scale the whole thing so the line reads at
        // roughly the width asked for, whatever the string length.
        float perChar = Mathf.Max(1, text.Length) * 0.5f;
        float scale = widthM / Mathf.Max(0.001f, perChar) * 0.1f;
        go.transform.localScale = new Vector3(scale, scale, scale);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sortingOrder = 25;
        }
        return go;
    }

    // A chequered slab: the floor of somewhere you are photographed standing.
    //
    // Two meshes rather than one per square — a nine-by-nine board is eighty-one objects otherwise, and
    // this is scenery. Both are drawn BEHIND everybody (a negative sorting order), because the whole point
    // of the thing is that a person is visible standing on it.
    public static GameObject Chequers(Transform parent, string name, Vector2 centreLocal, float size,
                                      int squares, Material light, Material dark, float localZ = FloorZ)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(centreLocal.x, centreLocal.y, localZ);

        squares = Mathf.Clamp(squares, 2, 24);
        float step = size / squares;
        float half = size * 0.5f;

        for (int pass = 0; pass < 2; pass++)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var uvs = new List<Vector2>();

            for (int row = 0; row < squares; row++)
            for (int col = 0; col < squares; col++)
            {
                if ((row + col) % 2 != pass) continue;

                float x0 = -half + col * step, y0 = -half + row * step;
                int b = verts.Count;
                verts.Add(new Vector3(x0, y0, 0f));
                verts.Add(new Vector3(x0 + step, y0, 0f));
                verts.Add(new Vector3(x0 + step, y0 + step, 0f));
                verts.Add(new Vector3(x0, y0 + step, 0f));
                uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
                tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            if (verts.Count == 0) continue;

            var tile = new GameObject(pass == 0 ? "Light" : "Dark");
            tile.transform.SetParent(go.transform, false);

            var mesh = new Mesh { name = tile.name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            tile.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = tile.AddComponent<MeshRenderer>();
            mr.sharedMaterial = pass == 0 ? light : dark;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sortingOrder = -20;
        }
        return go;
    }

    // An anchor on a prop: where the player has to stand, and where TRAVEL THERE puts them.
    public static WeekendVenueAnchor Anchor(Transform parent, Draftmaster.Weekend.WeekendVenue venue,
                                            Vector3 worldPosition, Vector3 standWorldPosition,
                                            float arriveRange = 3.5f, string label = "")
    {
        var go = new GameObject("Venue_" + venue);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPosition;

        var stand = new GameObject("Stand");
        stand.transform.SetParent(go.transform, false);
        stand.transform.position = standWorldPosition;

        var anchor = go.AddComponent<WeekendVenueAnchor>();
        anchor.venue = venue;
        anchor.arriveRange = arriveRange;
        anchor.standPoint = stand.transform;
        anchor.label = label;
        return anchor;
    }
}
