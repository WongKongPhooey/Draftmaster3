using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor.Android;
using UnityEngine;

// Keeps Patch And Run a patch. pram (Unity's patch deployer) decides what has changed by each file's size and
// modified time, never its contents — and Unity rewrites gradle.properties, settings.gradle,
// unityLibrary/build.gradle and the manifests on every build with exactly the same bytes but a new time. pram
// then calls the APK out of sync, rebuilds and reinstalls it, and a reinstall throws away every one of the
// ~2,100 Data files already on the phone and pushes them all again, one adb call each over wifi: 18-20 minutes
// a patch (2026-10-22, with or without a version bump).
//
// So straight after Unity writes the Gradle project, and before pram looks at it, every file whose contents
// are the same as last build gets last build's modified time back. Only files that really changed keep a new
// time, so pram only pushes those. Kept per file as (size, hash, time) in Library/PhoneDeploy/gradle-times.txt.
public class PatchTimestampKeeper : IPostGenerateGradleAndroidProject
{
    const string ManifestPath = "Library/PhoneDeploy/gradle-times.txt";

    // Last, after anything else that edits the project.
    public int callbackOrder => int.MaxValue;

    public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
    {
        try
        {
            string root = Path.GetFullPath(Path.Combine(unityLibraryPath, ".."));
            var before = Load();
            var after = new Dictionary<string, Entry>();
            int restored = 0, changed = 0;
            var watch = System.Diagnostics.Stopwatch.StartNew();

            using (var md5 = MD5.Create())
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    string rel = file.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
                    if (Skipped(rel)) continue;

                    var info = new FileInfo(file);
                    string hash;
                    using (var stream = File.OpenRead(file)) hash = Convert.ToBase64String(md5.ComputeHash(stream));

                    if (before.TryGetValue(rel, out var old) && old.size == info.Length && old.hash == hash)
                    {
                        if (info.LastWriteTimeUtc.Ticks != old.ticks)
                        {
                            File.SetLastWriteTimeUtc(file, new DateTime(old.ticks, DateTimeKind.Utc));
                            restored++;
                        }
                        after[rel] = old;
                    }
                    else
                    {
                        after[rel] = new Entry { size = info.Length, hash = hash, ticks = info.LastWriteTimeUtc.Ticks };
                        changed++;
                    }
                }
            }

            Save(after);
            Debug.Log($"PatchTimestampKeeper: {changed} file(s) changed since the last build, {restored} rewritten " +
                      $"unchanged and given their old time back ({watch.Elapsed.TotalSeconds:0.0}s).");
        }
        catch (Exception e)
        {
            // Never fail a build over this: the worst case is the slow full sync it exists to avoid.
            Debug.LogWarning("PatchTimestampKeeper: skipped — " + e.Message);
        }
    }

    // Gradle's own output and caches, and pram's working folder — none of it is deployed from here.
    static bool Skipped(string rel) =>
        rel.StartsWith("build/") || rel.Contains("/build/") || rel.StartsWith(".gradle/") || rel.Contains("/.gradle/") ||
        rel.StartsWith("pram-shadow-files/");

    struct Entry { public long size; public string hash; public long ticks; }

    static Dictionary<string, Entry> Load()
    {
        var map = new Dictionary<string, Entry>();
        if (!File.Exists(ManifestPath)) return map;
        foreach (var line in File.ReadAllLines(ManifestPath))
        {
            var parts = line.Split('\t');
            if (parts.Length == 4 && long.TryParse(parts[1], out long size) && long.TryParse(parts[3], out long ticks))
                map[parts[0]] = new Entry { size = size, hash = parts[2], ticks = ticks };
        }
        return map;
    }

    static void Save(Dictionary<string, Entry> map)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath));
        var lines = new List<string>(map.Count);
        foreach (var kv in map) lines.Add($"{kv.Key}\t{kv.Value.size}\t{kv.Value.hash}\t{kv.Value.ticks}");
        File.WriteAllLines(ManifestPath, lines);
    }
}
