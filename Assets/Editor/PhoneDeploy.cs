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
//      straight into ProjectSettings so the title screen (Application.version) shows it. A patch only COUNTS the
//      bump (UserSettings/PhoneDeployPendingBumps.txt) and leaves the version alone: a new version code changes
//      the APK, and pram answers a changed APK by reinstalling it and re-pushing every one of the ~2,100 Data
//      files one adb call at a time over wifi (~16 min, 2026-10-22) instead of the few that changed. The next
//      full build or release bundle applies every counted bump at once, so the numbers come out the same.
//   2. The Run Device in the Android build settings is read. Only when it is the HONOR (model FCP_N49) on a
//      wireless adb connection, and adb can actually see it right now, does anything get built. A Run Device
//      left on the default (or anything not wireless) is pointed at the HONOR when adb can see it over
//      wireless debugging, rather than skipping the build.
//   3. Patch And Run when a patch will do; Build And Run when it will not (the caller asks for a full build when
//      the change touched packages, project settings or native plugins). The build settings are forced to a
//      development APK first, which a patch needs. A patch that fails is retried once as a full build, after
//      the domain reload the patch left pending.
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
        public int pendingBumps;   // bumps counted by patches and not yet applied to the version
        public string device;
        public string message;
        public string startedAt;
        public string finishedAt;
    }

    // The bump is counted, not applied — see the top of the file. The menu path stays as Tokenmaxxer calls it.
    [MenuItem("Draftmaster/Build/After Task: Bump Version + Patch And Run On Phone", priority = 200)]
    static void AfterTaskPatch() => Schedule("patch");

    [MenuItem("Draftmaster/Build/After Task: Bump Version + Build And Run On Phone", priority = 201)]
    static void AfterTaskFull() => Schedule("full");

    // For a deploy that has to be retried after the bump already happened (a phone that wasn't the Run Device).
    [MenuItem("Draftmaster/Build/Patch And Run On Phone (No Version Bump)", priority = 202)]
    static void PatchNoBump() => Schedule("patch", bump: false);

    // A Google Play upload: release (not Development) App Bundle at the current version, then the build settings go
    // straight back to a development APK so the next After Task patch still works. Same status file, request
    // "release"; no bump of its own, but any bumps patches counted are applied first (After Task bumps keep the
    // version code ahead of the last upload), and no Run.
    [MenuItem("Draftmaster/Build/Build Play Store Bundle (Release .aab)", priority = 220)]
    static void ReleaseBundle()
    {
        var s = new Status { request = "release", bump = false, state = "working", startedAt = Now(), message = "queued" };
        Write(s);
        EditorApplication.delayCall += () => RunRelease(s);
    }

    static void RunRelease(Status s)
    {
        bool wasBundle = EditorUserBuildSettings.buildAppBundle;
        bool wasDev = EditorUserBuildSettings.development;
        try
        {
            s.version = PlayerSettings.bundleVersion;
            s.versionCode = PlayerSettings.Android.bundleVersionCode;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Finish(s, "skipped", "", "Editor is in Play Mode; no release build.");
                return;
            }
            // Patches only counted their bumps; an upload has to carry them, or its code could trail the last one.
            if (PendingBumps > 0) BumpVersion(s, PendingBumps);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Finish(s, "skipped", "", $"Active platform is {EditorUserBuildSettings.activeBuildTarget}, not Android.");
                return;
            }
            // Play rejects a debug-signed bundle, so the upload keystore is a hard requirement here.
            if (!PlayerSettings.Android.useCustomKeystore)
            {
                Finish(s, "skipped", "", "No custom keystore set; Google Play will not take a debug-signed bundle.");
                return;
            }
            string unsigned = MissingKeystorePassword();
            if (unsigned != null)
            {
                Finish(s, "skipped", "", $"Keystore password not set ({unsigned}); re-enter it in " +
                                         "Player Settings > Publishing Settings. Unity forgets it on restart.");
                return;
            }

            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.development = false;
            s.mode = "release";
            s.message = $"building release bundle {s.version} (code {s.versionCode})";
            Write(s);
            var bpo = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray(),
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                locationPathName = OutputPath(s.version, patch: false),
                options = BuildOptions.None,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(bpo.locationPathName));
            var report = BuildPipeline.BuildPlayer(bpo);
            if (report.summary.result == BuildResult.Succeeded)
                Finish(s, "deployed", "release",
                       $"Release bundle {bpo.locationPathName} ({report.summary.totalSize / (1024 * 1024)} MB) built in " +
                       $"{report.summary.totalTime.TotalMinutes:0.0} min. Build settings back to development APK.");
            else
                Finish(s, "failed", "release",
                       $"Release build {report.summary.result} with {report.summary.totalErrors} error(s). See Editor.log.");
        }
        catch (Exception e)
        {
            Finish(s, "failed", "release", "Exception: " + e.Message);
            Debug.LogException(e);
        }
        finally
        {
            // Back to the everyday setup whatever happened: development APK, which After Task patches need.
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.development = true;
            if (wasBundle || !wasDev)
                Debug.Log($"PhoneDeploy: build settings were bundle={wasBundle} development={wasDev}; now development APK.");
        }
    }

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

            if (s.bump && s.request == "patch") CountBump(s);
            else if (s.bump) BumpVersion(s, 1 + PendingBumps);
            else
            {
                s.version = PlayerSettings.bundleVersion;
                s.versionCode = PlayerSettings.Android.bundleVersionCode;
                s.pendingBumps = PendingBumps;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Finish(s, "skipped", "", $"{BumpNote(s)}Active platform is {EditorUserBuildSettings.activeBuildTarget}, " +
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
                Finish(s, "skipped", "", $"{BumpNote(s)}Run Device is '{device}', not a wireless debugging connection.");
                return;
            }

            string model = AdbModel(device, out string adbWhy);
            if (model == null)
            {
                Finish(s, "skipped", "", $"{BumpNote(s)}Run Device {device} is not connected right now ({adbWhy}).");
                return;
            }
            if (!string.Equals(model, PhoneModel, StringComparison.OrdinalIgnoreCase))
            {
                Finish(s, "skipped", "", $"{BumpNote(s)}Run Device is a {model}, not the HONOR {PhoneModel}.");
                return;
            }

            // Unity forgets keystore passwords on every editor restart, and a build signed without them stops on a
            // password prompt that nobody answers overnight (2026-10-20: 7 hours, MCP dead the whole time).
            string unsigned = MissingKeystorePassword();
            if (unsigned != null)
            {
                Finish(s, "skipped", "", $"{BumpNote(s)}Keystore password not set ({unsigned}); re-enter it in " +
                                         "Player Settings > Publishing Settings. Unity forgets it on restart.");
                return;
            }

            // A phone build is always a development APK: patches need both, and an App Bundle can't be installed by
            // Run anyway. A release bundle puts these back in its finally, but a hand toggle in Build Profiles
            // would otherwise quietly turn every patch into a full build.
            if (EditorUserBuildSettings.buildAppBundle || !EditorUserBuildSettings.development)
            {
                Debug.Log($"PhoneDeploy: build settings were bundle={EditorUserBuildSettings.buildAppBundle} " +
                          $"development={EditorUserBuildSettings.development}; set to development APK.");
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.development = true;
            }

            bool patch = s.request == "patch";
            var report = Build(s, patch);
            if (patch && report.summary.result != BuildResult.Succeeded)
            {
                // A patch build leaves a domain reload pending, and BuildPlayer refuses to start until it has run
                // ("A domain reload is pending", result Unknown). So the full build waits for the reload: the
                // status rides SessionState across it and RetryAfterReload picks it up.
                Debug.LogWarning($"PhoneDeploy: patch failed ({report.summary.result}); full Build And Run after the domain reload.");
                s.message = "patch failed; full Build And Run after the domain reload";
                Write(s);
                SessionState.SetString(RetryKey, JsonUtility.ToJson(s));
                EditorUtility.RequestScriptReload();
                return;
            }
            FinishBuild(s, report, patch, null);
        }
        catch (Exception e)
        {
            Finish(s, "failed", s.mode ?? "", "Exception: " + e.Message);
            Debug.LogException(e);
        }
    }

    const string RetryKey = "PhoneDeploy.RetryFull";

    [InitializeOnLoadMethod]
    static void RetryAfterReload()
    {
        string json = SessionState.GetString(RetryKey, "");
        if (json.Length == 0) return;
        SessionState.EraseString(RetryKey);
        var s = JsonUtility.FromJson<Status>(json);
        EditorApplication.delayCall += () =>
        {
            try
            {
                // A full install replaces the APK anyway, so the bumps the patches counted cost nothing here.
                if (s.bump && PendingBumps > 0) BumpVersion(s, PendingBumps);
                FinishBuild(s, Build(s, patch: false), false, "the patch build failed");
            }
            catch (Exception e)
            {
                Finish(s, "failed", "full", "Exception: " + e.Message);
                Debug.LogException(e);
            }
        };
    }

    static void FinishBuild(Status s, BuildReport report, bool patch, string whyFull)
    {
        if (report.summary.result == BuildResult.Succeeded)
            Finish(s, "deployed", patch ? "patch" : "full",
                   $"{(patch ? "Patch And Run" : "Build And Run")} to {s.device} succeeded in " +
                   $"{report.summary.totalTime.TotalMinutes:0.0} min" + (whyFull != null ? $" (full build because {whyFull})." : "."));
        else
            Finish(s, "failed", patch ? "patch" : "full",
                   $"{(patch ? "Patch" : "Build")} {report.summary.result} with {report.summary.totalErrors} error(s)" +
                   (whyFull != null ? $" (full build because {whyFull})" : "") + ". See Editor.log.");
    }

    // 0.3.8 -> 0.3.9: the last number only, `by` steps at once (a full build applying the bumps patches counted).
    // A version with no dot-separated number at the end is left alone. Clears the pending count.
    static void BumpVersion(Status s, int by)
    {
        string v = PlayerSettings.bundleVersion ?? "";
        var parts = v.Split('.');
        if (parts.Length > 0 && int.TryParse(parts[parts.Length - 1], out int last))
        {
            parts[parts.Length - 1] = (last + by).ToString();
            PlayerSettings.bundleVersion = string.Join(".", parts);
        }
        else
        {
            Debug.LogWarning($"PhoneDeploy: version '{v}' has no number to bump; left as it is.");
        }
        PlayerSettings.Android.bundleVersionCode = PlayerSettings.Android.bundleVersionCode + by;
        AssetDatabase.SaveAssets();
        PendingBumps = 0;
        WriteDisplayVersion(PlayerSettings.bundleVersion);

        s.version = PlayerSettings.bundleVersion;
        s.versionCode = PlayerSettings.Android.bundleVersionCode;
        s.pendingBumps = 0;
        s.message = $"version {s.version} (code {s.versionCode})";
        Write(s);
        Debug.Log($"PhoneDeploy: version bumped {v} -> {s.version} ({by} step(s)), version code {s.versionCode}.");
    }

    // 0.7.9 stepped by 3 -> 0.7.12; a version with no number at the end comes back as it is.
    static string Stepped(string v, int by)
    {
        var parts = (v ?? "").Split('.');
        if (parts.Length == 0 || !int.TryParse(parts[parts.Length - 1], out int last)) return v;
        parts[parts.Length - 1] = (last + by).ToString();
        return string.Join(".", parts);
    }

    // The version the title screen shows (BuildVersion.Display): the counted one, so a patch carries it in its
    // data while the APK's version name stays put.
    const string DisplayVersionFile = "Assets/Resources/BuildVersion.txt";

    static void WriteDisplayVersion(string v)
    {
        File.WriteAllText(DisplayVersionFile, v);
        AssetDatabase.ImportAsset(DisplayVersionFile, ImportAssetOptions.ForceSynchronousImport);
    }

    // How a skipped deploy reports what happened to the version before it stopped.
    static string BumpNote(Status s) =>
        !s.bump ? "" : s.request == "patch" ? $"Version bump counted ({s.pendingBumps} pending). " : "Version bumped. ";

    // A patch's bump: counted for the next full build, the version left as it is so the APK stays in sync.
    static void CountBump(Status s)
    {
        PendingBumps = PendingBumps + 1;
        WriteDisplayVersion(Stepped(PlayerSettings.bundleVersion, PendingBumps));
        s.version = PlayerSettings.bundleVersion;
        s.versionCode = PlayerSettings.Android.bundleVersionCode;
        s.pendingBumps = PendingBumps;
        s.message = $"version {s.version} (code {s.versionCode}), {s.pendingBumps} bump(s) pending";
        Write(s);
        Debug.Log($"PhoneDeploy: version bump counted, not applied — {s.pendingBumps} pending for the next full build.");
    }

    // Outside Library so a Library wipe can't lose it, and not in ProjectSettings so it isn't committed: it
    // counts this machine's patches.
    const string PendingFile = "UserSettings/PhoneDeployPendingBumps.txt";

    static int PendingBumps
    {
        get
        {
            try { return File.Exists(PendingFile) && int.TryParse(File.ReadAllText(PendingFile).Trim(), out int n) ? Math.Max(0, n) : 0; }
            catch { return 0; }
        }
        set
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PendingFile));
            File.WriteAllText(PendingFile, Math.Max(0, value).ToString());
        }
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

    // Which password a custom-keystore build would have to ask for, or null when signing can go ahead unattended.
    static string MissingKeystorePassword()
    {
        if (!PlayerSettings.Android.useCustomKeystore) return null;
        if (string.IsNullOrEmpty(PlayerSettings.Android.keystorePass)) return "keystore";
        if (string.IsNullOrEmpty(PlayerSettings.Android.keyaliasPass)) return "key alias";
        return null;
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
