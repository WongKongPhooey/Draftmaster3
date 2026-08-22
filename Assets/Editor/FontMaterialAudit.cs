using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

// Labels can keep a material instance from a face they no longer use — the font reference moves, the
// material does not, and the old atlas stays referenced by the scene. This reports (and optionally
// repairs) any TMP_Text whose material does not belong to its current font asset.
public static class FontMaterialAudit
{
    [MenuItem("Draftmaster/Art/Report Stale Font Materials")]
    public static void Report() => Run(false);

    [MenuItem("Draftmaster/Art/Fix Stale Font Materials In Open Scene")]
    public static void Fix() => Run(true);

    static void Run(bool fix)
    {
        var lines = new List<string>();
        int fixedCount = 0;

        foreach (var label in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (label.font == null) continue;
            var mat = label.fontSharedMaterial;
            string matName = mat != null ? mat.name : "<none>";
            // A label's material is expected to name its own face; anything else is a leftover.
            if (mat != null && matName.StartsWith(label.font.name)) continue;

            lines.Add($"{label.name}: font={label.font.name} material={matName}");
            if (fix && label.font.material != null)
            {
                Undo.RecordObject(label, "Fix Font Material");
                label.fontSharedMaterial = label.font.material;
                EditorUtility.SetDirty(label);
                fixedCount++;
            }
        }

        string report = lines.Count == 0
            ? "No stale font materials."
            : string.Join("\n", lines) + (fix ? $"\nrepaired {fixedCount}" : "");
        Debug.Log("[FontMaterialAudit] " + report);
        Directory.CreateDirectory("Docs/Reports");
        File.WriteAllText("Docs/Reports/FontMaterials.txt", report);
    }
}
