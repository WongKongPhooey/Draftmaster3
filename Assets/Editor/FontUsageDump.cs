using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Dumps every text object in the open scene with the face it actually renders in, so a readability
// complaint ("that font has to go") can be traced to a specific font asset rather than guessed at.
public static class FontUsageDump
{
    [MenuItem("Draftmaster/Art/Dump Font Usage In Open Scene")]
    public static void Run()
    {
        var lines = new List<string> { $"Font usage: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().path}", "" };

        foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .OrderBy(t => t.transform.position.y * -1))
        {
            string face = t.font != null ? t.font.name : "<none>";
            lines.Add($"TMP  {Path(t.transform)}\n     face={face}  size={t.fontSize}  text=\"{Trim(t.text)}\"");
        }

        foreach (var t in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string face = t.font != null ? t.font.name : "<none>";
            lines.Add($"uGUI {Path(t.transform)}\n     face={face}  size={t.fontSize}  text=\"{Trim(t.text)}\"");
        }

        string report = string.Join("\n", lines);
        Debug.Log(report);
        Directory.CreateDirectory("Docs/Reports");
        File.WriteAllText("Docs/Reports/FontUsage.txt", report);
    }

    static string Trim(string s) => s == null ? "" : s.Replace("\n", "\n").Substring(0, Mathf.Min(s.Length, 60));

    static string Path(Transform t)
    {
        var parts = new List<string>();
        while (t != null) { parts.Insert(0, t.name); t = t.parent; }
        return string.Join("/", parts);
    }
}
