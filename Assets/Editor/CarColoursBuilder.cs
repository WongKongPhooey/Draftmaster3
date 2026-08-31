using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Draftmaster.Sim;
using UnityEditor;
using UnityEngine;

// Draftmaster > Cars > Build Car Colours From Liveries
//
// Fills Resources/Cars/CarColours from the paint itself: every `<carset>livery<n>` texture in Resources is
// read, its two colours picked (LiveryPalette), and a row written for it. Forty cars, no typing.
//
// Rows ticked `handAuthored` are left exactly as they are — that is the point of the tick. Everything else
// is rewritten each run, so repainting a car and re-running this keeps the table honest.
//
// Reading a sprite's pixels needs the texture importer's Read/Write flag, which the project's liveries do
// not have (it costs memory at runtime for no reason). Rather than ask anyone to toggle forty importers,
// this flips the flag, reads, and puts it back — the asset is left exactly as it was found.
public static class CarColoursBuilder
{
    const string AssetPath = "Assets/Resources/Cars/CarColours.asset";

    // "cup26livery24" -> carset "cup26", number 24. Anything not shaped like that is not a livery.
    static readonly Regex LiveryName = new Regex(@"^(?<carset>[a-zA-Z]+[0-9]*)livery(?<number>\d+)$",
                                                 RegexOptions.Compiled);

    [MenuItem("Draftmaster/Cars/Build Car Colours From Liveries", priority = 200)]
    public static void Build() => Debug.Log(Run());

    public static string Run()
    {
        var table = AssetDatabase.LoadAssetAtPath<CarColours>(AssetPath);
        if (table == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            table = ScriptableObject.CreateInstance<CarColours>();
            AssetDatabase.CreateAsset(table, AssetPath);
        }

        var liveries = FindLiveries();
        if (liveries.Count == 0)
            return "No livery textures found. They are Resources/<carset>livery<number>.png — e.g. cup26livery24.";

        int written = 0, kept = 0, unreadable = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var (path, carset, number) in liveries)
            {
                var entry = table.EntryFor(carset, number);
                if (entry.handAuthored) { kept++; continue; }

                if (!TryReadPixels(path, out Color32[] pixels)) { unreadable++; continue; }

                var pair = LiveryPalette.Extract(pixels);
                entry.primary = pair.primary;
                entry.secondary = pair.secondary;
                written++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        CarColours.Forget();

        string note = kept > 0 ? $", left {kept} hand-authored row(s) alone" : "";
        string bad = unreadable > 0 ? $", {unreadable} texture(s) could not be read" : "";
        return $"Car colours: read {written} livery/liveries into {AssetPath}{note}{bad}. " +
               "Correct any that look wrong in the asset and tick Hand Authored to keep the correction.";
    }

    static List<(string path, string carset, int number)> FindLiveries()
    {
        var found = new List<(string, string, int)>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D livery"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.IndexOf("/Resources/", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

            var match = LiveryName.Match(Path.GetFileNameWithoutExtension(path));
            if (!match.Success) continue;

            // "blank" liveries are the unpainted base the paint booth writes onto; they are not a car's colours.
            if (path.IndexOf("blank", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;

            found.Add((path, match.Groups["carset"].Value, int.Parse(match.Groups["number"].Value)));
        }
        return found;
    }

    // Read a texture's pixels whatever its import settings, and leave those settings as they were.
    static bool TryReadPixels(string path, out Color32[] pixels)
    {
        pixels = null;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return false;

        bool wasReadable = importer.isReadable;
        var wasCompression = importer.textureCompression;
        try
        {
            if (!wasReadable || wasCompression != TextureImporterCompression.Uncompressed)
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) return false;
            pixels = tex.GetPixels32();
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Car colours: could not read {path} — {e.Message}");
            return false;
        }
        finally
        {
            if (!wasReadable || wasCompression != TextureImporterCompression.Uncompressed)
            {
                importer.isReadable = wasReadable;
                importer.textureCompression = wasCompression;
                importer.SaveAndReimport();
            }
        }
    }
}
