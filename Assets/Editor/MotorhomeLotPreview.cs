using System.Collections.Generic;
using Draftmaster.Data;
using UnityEditor;
using UnityEngine;

// Edit-time preview of the drivers' motorhome lot. At play time DriverMotorhomeLot builds the row
// from the live field (which needs the driver database and GridSpawner), so there is no way to see
// where the lot lands while authoring a track. This runs the same layout maths against the code
// roster (CupRoster2026) and drops the bodies into the open scene so the placement can be checked
// against the paddock, the pit garages and the PaddockBoundary.
//
// Preview objects are marked DontSave: they never end up in the scene file, and Clear removes them.
public static class MotorhomeLotPreview
{
    const string RootName = "~MotorhomeLotPreview";

    [MenuItem("Draftmaster/Paddock/Preview Motorhome Lot")]
    static void Build()
    {
        Clear();

        var lot = Object.FindObjectOfType<DriverMotorhomeLot>();
        var playerRv = Object.FindObjectOfType<RVExterior>();
        if (playerRv == null)
        {
            Debug.LogError("Preview Motorhome Lot: no RVExterior in the open scene to anchor the lot to.");
            return;
        }

        // Field defaults come from the scene's own components where they exist, so the preview matches
        // what will actually be built at play time.
        float rvWidth = lot != null ? lot.rvWidth : 3.95f;
        float rvLength = lot != null ? lot.rvLength : 9.93f;
        float sideGap = lot != null ? lot.sideGap : 4f;
        float aisleDepth = lot != null ? lot.aisleDepth : 10f;
        float setback = lot != null ? lot.lotSetback : 25f;
        int perRow = lot != null ? lot.perRow : 8;
        float rvZ = lot != null ? lot.rvZ : -0.5f;

        var grid = Object.FindObjectOfType<GridSpawner>();
        int fieldSize = grid != null ? grid.count : 43;

        var entries = new List<CupRoster2026.Entry>(CupRoster2026.Entries);
        if (entries.Count > fieldSize) entries.RemoveRange(fieldSize, entries.Count - fieldSize);

        Quaternion rot = playerRv.transform.rotation;
        Vector3 sideAxis = -(rot * Vector3.right);
        Vector3 rowAxis = rot * Vector3.up;
        Vector3 origin = playerRv.transform.position + rowAxis * setback;
        origin.z = rvZ;

        float sidePitch = rvWidth + sideGap;
        float rowPitch = rvLength + aisleDepth;

        var root = new GameObject(RootName) { hideFlags = HideFlags.DontSave };
        var sprite = Resources.Load<Sprite>("Environment/motorhome");
        if (sprite == null) Debug.LogWarning("Preview Motorhome Lot: Resources/Environment/motorhome not found — bodies will be invisible.");

        // The scene renders through the 3D URP renderer, where Sprite-Lit-Default has no Light2D and
        // comes out black. Same unlit swap the runtime lot does.
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var unlit = new Material(sh) { hideFlags = HideFlags.DontSave };

        for (int i = 0; i < entries.Count; i++)
        {
            int col = i % Mathf.Max(1, perRow);
            int row = i / Mathf.Max(1, perRow);
            Vector3 pos = origin + sideAxis * (col * sidePitch) + rowAxis * (row * rowPitch);
            pos.z = rvZ;

            var go = new GameObject($"RV_{entries[i].Number}_{entries[i].Last}") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(pos, rot);

            var art = new GameObject("Body") { hideFlags = HideFlags.DontSave };
            art.transform.SetParent(go.transform, false);
            art.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

            var sr = art.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = unlit;
            sr.sortingOrder = lot != null ? lot.sortingOrder : 2;
            var rng = new System.Random(entries[i].Number * 7919 + 17);
            sr.color = Color.HSVToRGB((float)rng.NextDouble(), 0.22f, 0.95f);
            if (sprite != null)
            {
                Vector2 s = sprite.bounds.size;
                art.transform.localScale = new Vector3(rvLength / s.x, rvWidth / s.y, 1f);
            }
        }

        Debug.Log($"Preview Motorhome Lot: {entries.Count} motorhomes in {Mathf.CeilToInt(entries.Count / (float)perRow)} rows, " +
                  $"anchored on '{playerRv.name}' at {playerRv.transform.position}. Clear when done.", root);
        Selection.activeGameObject = root;
    }

    [MenuItem("Draftmaster/Paddock/Clear Motorhome Lot Preview")]
    static void Clear()
    {
        var existing = GameObject.Find(RootName);
        if (existing != null) Object.DestroyImmediate(existing);
    }
}
