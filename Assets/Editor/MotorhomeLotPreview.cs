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
        float lineGap = lot != null ? lot.lineGap : 2f;
        float rowGap = lot != null ? lot.rowGap : 4f;
        int rowCount = lot != null ? lot.rowCount : 2;
        bool stackForward = lot == null || lot.stackRowsForward;
        Vector2 lineDir = lot != null ? lot.lineDirection : Vector2.right;
        int playerPlace = lot != null ? lot.playerLineIndex : 0;
        float rvZ = lot != null ? lot.rvZ : -0.5f;
        string numberPrefix = lot != null && !string.IsNullOrEmpty(lot.numberSpritePrefix) ? lot.numberSpritePrefix : "cup20num";
        float numberSize = lot != null ? lot.numberSize : 2.5f;
        float numberOffset = lot != null ? lot.numberOffset : 0f;
        bool showNumbers = lot == null || lot.showCarNumbers;

        var grid = Object.FindObjectOfType<GridSpawner>();
        int fieldSize = grid != null ? grid.count : 43;

        var entries = new List<CupRoster2026.Entry>(CupRoster2026.Entries);
        if (entries.Count > fieldSize) entries.RemoveRange(fieldSize, entries.Count - fieldSize);

        Quaternion rot = playerRv.transform.rotation;
        playerPlace = Mathf.Clamp(playerPlace, 0, Mathf.Max(0, entries.Count - 1));
        // entries excludes nobody: the roster count here already stands in for the whole field, so the
        // preview's places-per-line matches what the live lot will compute.
        var line = DriverMotorhomeLot.ComputeLine(playerRv.transform.position, rot, lineDir,
                                                  rvWidth, rvLength, lineGap, rowGap, rowCount,
                                                  entries.Count, playerPlace, rvZ, stackForward);

        var root = new GameObject(RootName) { hideFlags = HideFlags.DontSave };
        var sprite = Resources.Load<Sprite>("Environment/motorhome");
        if (sprite == null) Debug.LogWarning("Preview Motorhome Lot: Resources/Environment/motorhome not found — bodies will be invisible.");

        // The scene renders through the 3D URP renderer, where Sprite-Lit-Default has no Light2D and
        // comes out black. Same unlit swap the runtime lot does.
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var unlit = new Material(sh) { hideFlags = HideFlags.DontSave };

        // The player's own rig is already in the scene, so the preview skips its place in the line —
        // what you see is where everyone ELSE will park around it.
        int place = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (place == playerPlace) place++;
            Vector3 pos = line.PlaceAt(place);
            place++;

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

            // The car number painted on the roof, same art and sizing the live lot uses.
            if (!showNumbers) continue;
            var numberSprite = Resources.Load<Sprite>($"{numberPrefix}{entries[i].Number}");
            if (numberSprite == null) continue;
            Vector3 numPos = pos + (rot * Vector3.up) * numberOffset;
            var num = new GameObject("Number") { hideFlags = HideFlags.DontSave };
            num.transform.SetParent(go.transform, false);
            num.transform.position = new Vector3(numPos.x, numPos.y, rvZ - 0.1f);
            num.transform.localRotation = Quaternion.identity;
            var nsr = num.AddComponent<SpriteRenderer>();
            nsr.sprite = numberSprite;
            nsr.sharedMaterial = unlit;
            nsr.sortingOrder = (lot != null ? lot.sortingOrder : 2) + 1;
            float nh = numberSprite.bounds.size.y;
            if (nh > 0.0001f)
            {
                float ns = numberSize / nh;
                num.transform.localScale = new Vector3(ns, ns, 1f);
            }
        }

        Debug.Log($"Preview Motorhome Lot: {entries.Count} motorhomes in {Mathf.Max(1, rowCount)} line(s) of " +
                  $"{line.perRow}, {line.pitch:0.0}m apart ({line.perRow * line.pitch:0}m long × " +
                  $"{Mathf.Max(1, rowCount) * line.rowPitch:0}m deep), anchored on '{playerRv.name}' at " +
                  $"{playerRv.transform.position} with the player at place {playerPlace}. Clear when done.", root);
        Selection.activeGameObject = root;
    }

    [MenuItem("Draftmaster/Paddock/Clear Motorhome Lot Preview")]
    static void Clear()
    {
        var existing = GameObject.Find(RootName);
        if (existing != null) Object.DestroyImmediate(existing);
    }
}
