using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

// What Tokenmaxxer does after every task it finishes: bump the version, and if the Android Run Device is Josh's
// phone over wireless debugging, put the new build on it.
//
//   1. The version's last number goes up by one (0.3.8 -> 0.3.9), and the Android version code with it, saved
//      straight into ProjectSettings so the title screen (Application.version) shows it.
//   2. The Run Device in the Android build settings is read. Only when it is the HONOR (model FCP_N49) on a
//      wireless adb connection, and adb can actually see it right now, does anything get built. A Run Device
//      left on the default (or anything not wireless) is pointed at the HONOR when adb can see it over
//      wireless debugging, rather than skipping the build.
//   3. Patch And Run when a patch will do; Build And Run when it will not (the caller asks for a full build when
//      the change touched packages, project settings or native plugins; a patch also needs a development APK
//      build, so an App Bundle or a non-development setup always gets the full one). A patch that fails is
//      retried once as a full build.
//
// Driven by Tokenmaxxer through the Unity MCP as menu items, so nothing in here may open a dialog (a modal
// wedges the editor for the rest of the night). The menu item only schedules the work and returns, because a
// build blocks the editor for many minutes and the MCP call would time out; progress goes to a status file
// Tokenmaxxer polls: Library/PhoneDeploy/status.json.
public static class PhoneDeploy
{
    const string PhoneModel = "FCP_N49";
    const string StatusDir = "Library/PhoneDeploy";
    const string StatusFile = StatusDir + "/status.json";

    [Serializable]
    class Status
    {
        public string request;     // "patch" or "full", as asked
        public bool bump = true;   // false: deploy the version as it stands
        public string state;       // "working", "deployed", "skipped", "failed"
        public string mode;        // what was actually built: "patch", "full" or ""
        public string version;
        public int versionCode;
        public string device;
        public string message;
        public string startedAt;
        public string finishedAt;
    }

    [MenuItem("Draftmaster/Build/After Task: Bump Version + Patch And Run On Phone", priority = 200)]
    static void AfterTaskPatch() => Schedule("patch");

    [MenuItem("Draftmaster/Build/After Task: Bump Version + Build And Run On Phone", priority = 201)]
    static void AfterTaskFull() => Schedule("full");

    // For a deploy that has to be retried after the bump already happened (a phone that wasn't the Run Device).
    [MenuItem("Draftmaster/Build/Patch And Run On Phone (No Version Bump)", priority = 202)]
    static void PatchNoBump() => Schedule("patch", bump: false);

    static void Schedule(string request, bool bump = true)
    {
        var s = new Status { request = request, bump = bump, state = "working", startedAt = Now(), message = "queued" };
        Write(s);
        // Out of the menu call, so the MCP request that fired it returns before the build blocks the editor.
        EditorApplication.delayCall += () => Run(s);
    }

    static void Run(Status s)
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Finish(s, "skipped", "", "Editor is in Play Mode; nothing bumped or built.");
                return;
            }

            if (s.bump) BumpVersion(s);
            else
            {
                s.version = PlayerSettings.bundleVersion;
                s.versionCode = PlayerSettings.Android.bundleVersionCode;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Finish(s, "skipped", "", $"Version bumped. Active platform is {EditorUserBuildSettings.activeBuildTarget}, " +
                                         "not Android, so no phone build (switching would reimport the project).");
                return;
            }

            string device = RunDevice();
            if (string.IsNullOrEmpty(device) || !IsWireless(device))
            {
                string phone = WirelessPhone();
                if (phone != null)
                {
                    Debug.Log($"PhoneDeploy: Run Device was '{device}'; using the {PhoneModel} on {phone}.");
                    SetRunDevice(phone);
                    device = phone;
                }
            }
            s.device = device;
            if (string.IsNullOrEmpty(device) || !IsWireless(device))
            {
                Finish(s, "skipped", "", $"Version bumped. Run Device is '{device}', not a wireless debugging connection.");
                return;
            }

            string model = AdbModel(device, out string adbWhy);
            if (model == null)
            {
                Finish(s, "skipped", "", $"Version bumped. Run Device {device} is not connected right now ({adbWhy}).");
                return;
            }
            if (!string.Equals(model, PhoneModel, StringComparison.OrdinalIgnoreCase))
            {
                Finish(s, "skipped", "", $"Version bumped. Run Device is a {model}, not the HONOR {PhoneModel}.");
                return;
            }

            bool patch = s.request == "patch";
            string whyFull = null;
            if (patch && EditorUserBuildSettings.buildAppBundle) whyFull = "the build is set to App Bundle (patches are APK only)";
            else if (patch && !EditorUserBuildSettings.development) whyFull = "the build is not a Development Build (patches need one)";
            if (whyFull != null) patch = false;

            var report = Build(s, patch);
            if (patch && report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogWarning($"PhoneDeploy: patch failed ({report.summary.result}); retrying as a full Build And Run.");
                whyFull = "the patch build failed";
                report = Build(s, patch: false);
                patch = false;
            }

            if (report.summary.result == BuildResult.Succeeded)
                Finish(s, "deployed", patch ? "patch" : "full",
                       $"{(patch ? "Patch And Run" : "Build And Run")} to {device} succeeded in " +
                       $"{report.summary.totalTime.TotalMinutes:0.0} min" + (whyFull != null ? $" (full build because {whyFull})." : "."));
            else
                Finish(s, "failed", patch ? "patch" : "full",
                       $"Build {report.summary.result} with {report.summary.totalErrors} error(s). See Editor.log.");
        }
        catch (Exception e)
        {
            Finish(s, "failed", s.mode ?? "", "Exception: " + e.Message);
            Debug.LogException(e);
        }
    }

    // 0.3.8 -> 0.3.9: the last number only. A version with no dot-separated number at the end is left alone.
    static void BumpVersion(Status s)
    {
        string v = PlayerSettings.bundleVersion ?? "";
        var parts = v.Split('.');
        if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int last))
        {
            parts[parts.Length - 1] = (last + 1).ToString();
            PlayerSettings.bundleVersion = string.Join(".", parts);
        }
        else
        {
            Debug.LogWarning($"PhoneDeploy: version '{v}' has no number to bump; left as it is.");
        }
        PlayerSettings.Android.bundleVersionCode = PlayerSettings.Android.bundleVersionCode + 1;
        AssetDatabase.SaveAssets();

        s.version = PlayerSettings.bundleVersion;
        s.versionCode = PlayerSettings.Android.bundleVersionCode;
        s.message = $"version {s.version} (code {s.versionCode})";
        Write(s);
        Debug.Log($"PhoneDeploy: version bumped {v} -> {s.version}, version code {s.versionCode}.");
    }

    static BuildReport Build(Status s, bool patch)
    {
        s.mode = patch ? "patch" : "full";
        s.message = $"building ({s.mode}) {s.version}";
        Write(s);

        var options = BuildOptions.AutoRunPlayer;
        if (EditorUserBuildSettings.development || patch) options |= BuildOptions.Development;
        if (patch) options |= BuildOptions.PatchPackage;
        if (EditorUserBuildSettings.connectProfiler) options |= BuildOptions.ConnectWithProfiler;
        if (EditorUserBuildSettings.allowDebugging) options |= BuildOptions.AllowDebugging;

        var bpo = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray(),
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            locationPathName = OutputPath(s.version, patch),
            options = options,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(bpo.locationPathName));
        return BuildPipeline.BuildPlayer(bpo);
    }

    // Next to the last build, named for the version: Builds/Android/Draftmaster3-Demo.0.3.9.apk (or .aab).
    static string OutputPath(string version, bool patch)
    {
        string last = EditorUserBuildSettings.GetBuildLocation(BuildTarget.Android);
        string dir = !string.IsNullOrEmpty(last) ? Path.GetDirectoryName(last) : "Builds/Android";
        string ext = !patch && EditorUserBuildSettings.buildAppBundle ? ".aab" : ".apk";
        return Path.Combine(dir, $"Draftmaster3-Demo.{version}{ext}").Replace('\\', '/');
    }

    // The Run Device dropdown in the Android build settings. Not public API, hence the reflection; the setting
    // lives in Library/EditorUserBuildSettings.asset as m_AndroidCurrentDeploymentTargetId.
    static string RunDevice()
    {
        var prop = typeof(EditorUserBuildSettings).GetProperty("androidCurrentDeploymentTargetId",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return prop != null ? prop.GetValue(null) as string : null;
    }

    // Point the Run Device dropdown at a device. Best effort: if this Unity has no setter, the build's own
    // AutoRunPlayer still goes to the first connected device, which is the phone when it is the only one.
    static void SetRunDevice(string id)
    {
        try
        {
            var prop = typeof(EditorUserBuildSettings).GetProperty("androidCurrentDeploymentTargetId",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.CanWrite) prop.SetValue(null, id);
        }
        catch (Exception e) { Debug.LogWarning("PhoneDeploy: could not set the Run Device: " + e.Message); }
    }

    // The HONOR as adb sees it over wireless debugging, or null.
    static string WirelessPhone()
    {
        string output;
        try
        {
            var psi = new ProcessStartInfo(AdbPath(), "devices -l")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (var p = Process.Start(psi))
            {
                output = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(20000)) return null;
            }
        }
        catch { return null; }

        foreach (var line in output.Split('\n'))
        {
            var cols = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (cols.Length < 2 || cols[1] != "device" || !IsWireless(cols[0])) continue;
            if (cols.Any(c => string.Equals(c, "model:" + PhoneModel, StringComparison.OrdinalIgnoreCase)))
                return cols[0];
        }
        return null;
    }

    // Wireless debugging shows up either as an mDNS name or as a raw ip:port.
    static bool IsWireless(string id) =>
        id.Contains("_adb-tls-connect") || System.Text.RegularExpressions.Regex.IsMatch(id, @"^\d+\.\d+\.\d+\.\d+:\d+$");

    // The connected device's model as adb reports it, or null if adb cannot see it.
    static string AdbModel(string id, out string why)
    {
        why = null;
        string output;
        try
        {
            var psi = new ProcessStartInfo(AdbPath(), "devices -l")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (var p = Process.Start(psi))
            {
                output = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(20000)) { why = "adb timed out"; return null; }
            }
        }
        catch (Exception e) { why = "adb not runnable: " + e.Message; return null; }

        foreach (var line in output.Split('\n'))
        {
            var cols = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (cols.Length < 2 || cols[0] != id) continue;
            if (cols[1] != "device") { why = "adb state " + cols[1]; return null; }
            var m = cols.FirstOrDefault(c => c.StartsWith("model:"));
            return m != null ? m.Substring(6) : "";
        }
        why = "not in adb devices";
        return null;
    }

    // Unity's own SDK first (the one Build And Run uses), then whatever adb is on PATH.
    static string AdbPath()
    {
        try
        {
            var t = Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions");
            var sdk = t?.GetProperty("sdkRootPath", BindingFlags.Static | BindingFlags.Public)?.GetValue(null) as string;
            if (!string.IsNullOrEmpty(sdk))
            {
                string adb = Path.Combine(sdk, "platform-tools", Application.platform == RuntimePlatform.WindowsEditor ? "adb.exe" : "adb");
                if (File.Exists(adb)) return adb;
            }
        }
        catch { }
        return "adb";
    }

    static void Finish(Status s, string state, string mode, string message)
    {
        s.state = state;
        s.mode = mode;
        s.message = message;
        s.finishedAt = Now();
        Write(s);
        Debug.Log($"PhoneDeploy: {state} — {message}");
    }

    static void Write(Status s)
    {
        Directory.CreateDirectory(StatusDir);
        File.WriteAllText(StatusFile, JsonUtility.ToJson(s, true));
    }

    static string Now() => DateTime.Now.ToString("s");
}
