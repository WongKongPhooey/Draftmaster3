#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Finds prefabs that size a sprite with transform scale instead of its import PPU.
//
// This is the bug class that made the on-foot player 8x too big: TaylorEmerson.prefab carried a root
// scale of 8 to turn an 8px sprite at 100 px/unit into a 0.64m character. Once the sprite was reimported
// at the project standard the 8 double-counted and the player became 5m tall.
//
// A prefab listed here is not necessarily wrong -- some scale is deliberate. But any entry whose sprite
// already resolves to a sensible metre size at scale 1 is compensation that should be removed, because
// it will silently break the next time the import settings are corrected.
public static class SpriteScaleCompensationReport
{
    [MenuItem("Draftmaster/Art/Report Sprite Scale Compensation", priority = 104)]
    public static void Run()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Sprite scale compensation");
        sb.AppendLine();
        sb.AppendLine($"Standard: {PixelArt.PixelsPerMetre} px/m. `at scale 1` is what the sprite would " +
                      "measure with no transform scale; `as authored` is what it measures now.");
        sb.AppendLine();
        sb.AppendLine("| prefab | object | sprite | source px | scale | at scale 1 (m) | as authored (m) |");
        sb.AppendLine("|---|---|---|---|---|---|---|");

        int flagged = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;
            if (path.Contains("/_Recovery/")) continue;

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;

            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.sprite == null) continue;
                var s = sr.transform.lossyScale;
                if (Mathf.Approximately(s.x, 1f) && Mathf.Approximately(s.y, 1f)) continue;

                var rect = sr.sprite.rect;
                float ppu = sr.sprite.pixelsPerUnit;
                float nativeW = rect.width / ppu, nativeH = rect.height / ppu;
                float authoredW = nativeW * Mathf.Abs(s.x), authoredH = nativeH * Mathf.Abs(s.y);

                sb.AppendLine($"| `{path}` | {sr.name} | {sr.sprite.name} | {rect.width}x{rect.height} | " +
                              $"{s.x:0.###},{s.y:0.###} | {nativeW:0.###} x {nativeH:0.###} | " +
                              $"{authoredW:0.###} x {authoredH:0.###} |");
                flagged++;
            }
        }

        sb.AppendLine();
        sb.AppendLine($"{flagged} scaled sprite renderer(s) found.");

        Directory.CreateDirectory("Docs");
        File.WriteAllText("Docs/SpriteScaleCompensation.md", sb.ToString());
        AssetDatabase.Refresh();
        Debug.Log($"[SpriteScaleCompensationReport] {flagged} entries — Docs/SpriteScaleCompensation.md");
    }
}
#endif
