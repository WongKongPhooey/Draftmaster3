using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Google Play internal testing, straight from the editor.
//
//   Build + Upload To Play Internal Testing
//       1. asks Play for the highest version code it already holds, and bumps the version past it (together with
//          any bumps patches counted) — Play refuses a code it has seen;
//       2. switches the build to a release (.aab, Development Build off) App Bundle, signed with the upload keystore;
//       3. builds it to Builds/Android/Draftmaster3-Demo.<version>.aab and puts the build settings straight back to
//          the development APK the phone patches need;
//       4. hands the bundle to Tools/play_upload.py in the background, which uploads it, points the internal track
//          at it and commits. The editor stays usable while it uploads.
//   Upload Last Bundle To Play Internal Testing   step 4 alone, for an upload that failed after a good build.
//   Check Play Upload Setup                       signs in and reports what Play holds; changes nothing.
//
// One-time setup (Docs/Play-Internal-Testing.md):
//   * a Google Cloud service account with the Android Publisher API enabled, invited in Play Console > Users and
//     permissions with release rights on this app; its JSON key saved OUTSIDE the repo, by default at
//     %USERPROFILE%\.draftmaster\play-service-account.json (or wherever DRAFTMASTER_PLAY_KEY or
//     UserSettings/PlayUpload.json points);
//   * Python 3 with the `rsa` package (`python -m pip install rsa`);
//   * the app created in the Play Console, with its first bundle uploaded by hand — the API cannot create an app.
//
// Same rules as the rest of PhoneDeploy: no dialogs (Tokenmaxxer drives these through the MCP), menu items only
// schedule work, and progress goes to Library/PhoneDeploy/status.json (request "play-internal"). The uploader's
// own progress is in Library/PlayUpload/status.json.
public static partial class PhoneDeploy
{
    const string UploadDir = "Library/PlayUpload";
    const string UploadStatusFile = UploadDir + "/status.json";
    const string UploadConfigFile = "UserSettings/PlayUpload.json";   // per machine, never committed
    const string UploaderScript = "Tools/play_upload.py";

    // Per-machine settings. Written with defaults on first use so there is a file to edit.
    [Serializable]
    class UploadConfig
    {
        public string serviceAccountKey = "";      // empty: DRAFTMASTER_PLAY_KEY, then the default path
        public string track = "internal";
        public string releaseStatus = "completed"; // "draft" holds the release for a click in the Console
        public string python = "python";
        public string lastBundle = "";             // the last bundle built for upload, for a retry
    }

    [Serializable]
    class UploadStatus
    {
        public string state;   // working / committed / checked / failed
        public string message;
        public int versionCode;
        public string report;
    }

    // ------------------------------------------------------------------ menu

    [MenuItem("Draftmaster/Build/Build + Upload To Play Internal Testing", priority = 221)]
    static void BuildAndUploadInternal()
    {
        var s = new Status { request = "play-internal", bump = true, state = "working", startedAt = Now(), message = "queued" };
        Write(s);
        RunProgress("checking setup");
        EditorApplication.delayCall += () => RunRelease(s, upload: true);
    }

    [MenuItem("Draftmaster/Build/Upload Last Bundle To Play Internal Testing", priority = 222)]
    static void UploadLastBundle()
    {
        var cfg = LoadConfig();
        var s = new Status
        {
            request = "play-upload", bump = false, state = "working", startedAt = Now(), mode = "release",
            version = PlayerSettings.bundleVersion, versionCode = PlayerSettings.Android.bundleVersionCode,
        };
        if (string.IsNullOrEmpty(cfg.lastBundle) || !File.Exists(cfg.lastBundle))
        {
            Finish(s, "skipped", "", "No bundle to upload: run Build + Upload To Play Internal Testing.");
            return;
        }
        LaunchUploader(s, cfg, cfg.lastBundle);
    }

    [MenuItem("Draftmaster/Build/Check Play Upload Setup", priority = 223)]
    static void CheckPlaySetup()
    {
        var cfg = LoadConfig();
        string problem = SetupProblem(cfg);
        if (problem != null)
        {
            Debug.LogError("PlayUpload: " + problem);
            EndProgress(false, "Play setup not done - see Console");
            return;
        }
        WriteUploadStatus(new UploadStatus { state = "working", message = "checking" });
        if (StartUploader(cfg, $"check {CommonArgs(cfg)}", out string why) == null)
        {
            Debug.LogError("PlayUpload: " + why);
            EndProgress(false, "Play check could not start - see Console");
            return;
        }
        RunProgress("checking Google Play");
        Debug.Log($"PlayUpload: checking {PackageName} on Google Play…");
        WatchUploader(null);
    }

    [MenuItem("Draftmaster/Build/Build + Upload To Play Internal Testing", true)]
    [MenuItem("Draftmaster/Build/Upload Last Bundle To Play Internal Testing", true)]
    static bool ValidateUpload() => !EditorApplication.isPlayingOrWillChangePlaymode && !_watching;

    // ------------------------------------------------------------------ before the build

    // Bump the version past everything Play holds (and apply the bumps patches counted). False = stop, with the
    // status already finished: nothing has been built.
    static bool PrepareUploadVersion(Status s)
    {
        var cfg = LoadConfig();
        string problem = SetupProblem(cfg);
        if (problem != null)
        {
            Finish(s, "skipped", "", problem + " Nothing built.");
            return false;
        }

        s.message = "asking Google Play for its highest version code";
        Write(s);
        RunProgress(s.message);
        bool read;
        int top;
        string why;
        // Synchronous (up to a minute): a progress bar, or the editor just looks frozen. Not a dialog - nothing to click.
        EditorUtility.DisplayProgressBar("Google Play", "Asking Google Play for its highest version code…", 0.2f);
        try { read = QueryMaxVersionCode(cfg, out top, out why); }
        finally { EditorUtility.ClearProgressBar(); }
        if (!read)
        {
            Finish(s, "skipped", "", $"Could not read Google Play ({why}). Nothing built — Check Play Upload Setup.");
            return false;
        }

        int code = PlayerSettings.Android.bundleVersionCode;
        int needed = top + 1 - code;
        int by = Math.Max(PendingBumps, needed);
        if (by > 0) BumpVersion(s, by);
        Debug.Log($"PhoneDeploy: Google Play's highest version code is {top}; uploading {s.version} " +
                  $"(code {PlayerSettings.Android.bundleVersionCode}).");
        RunProgress($"building release bundle {s.version} (code {PlayerSettings.Android.bundleVersionCode})");
        return true;
    }

    // Short and synchronous: one sign-in and two reads. A minute is far more than it takes.
    static bool QueryMaxVersionCode(UploadConfig cfg, out int top, out string why)
    {
        top = 0;
        why = null;
        try
        {
            var psi = UploaderStartInfo(cfg, $"maxcode {CommonArgs(cfg)}");
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using (var p = Process.Start(psi))
            {
                var stdout = p.StandardOutput.ReadToEndAsync();
                var stderr = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(60000))
                {
                    try { p.Kill(); } catch { }
                    why = "timed out after 60 s";
                    return false;
                }
                string output = stdout.Result.Trim();
                string lastLine = output.Length == 0 ? "" : output.Substring(output.LastIndexOf('\n') + 1).Trim();
                if (p.ExitCode == 0 && int.TryParse(lastLine, out top)) return true;
                why = !string.IsNullOrEmpty(output) ? lastLine : stderr.Result.Trim();
                if (string.IsNullOrEmpty(why)) why = $"exit code {p.ExitCode}";
                return false;
            }
        }
        catch (Exception e)
        {
            why = $"could not run {cfg.python}: {e.Message}";
            return false;
        }
    }

    // ------------------------------------------------------------------ the upload

    static void StartUpload(Status s, string bundlePath, BuildReport report)
    {
        var cfg = LoadConfig();
        cfg.lastBundle = bundlePath;
        SaveConfig(cfg);
        Debug.Log($"PhoneDeploy: release bundle {bundlePath} ({report.summary.totalSize / (1024 * 1024)} MB) built in " +
                  $"{report.summary.totalTime.TotalMinutes:0.0} min.");
        LaunchUploader(s, cfg, bundlePath);
    }

    static void LaunchUploader(Status s, UploadConfig cfg, string bundlePath)
    {
        string args = $"upload {CommonArgs(cfg)} --aab \"{bundlePath}\" --version-name \"{s.version}\" " +
                      $"--version-code {s.versionCode} --track {cfg.track} --status {cfg.releaseStatus}";
        WriteUploadStatus(new UploadStatus { state = "working", message = "starting" });
        if (StartUploader(cfg, args, out string why) == null)
        {
            Finish(s, "failed", "release", $"Bundle built at {bundlePath}, but the uploader would not start ({why}).");
            return;
        }
        s.state = "uploading";
        s.message = $"uploading {Path.GetFileName(bundlePath)} to the {cfg.track} track";
        Write(s);
        RunProgress(s.message);
        Debug.Log($"PhoneDeploy: {s.message} in the background — Library/PlayUpload/status.json.");
        WatchUploader(s);
    }

    // The uploader runs on its own, outside the editor: a domain reload or even closing Unity does not stop it.
    static Process StartUploader(UploadConfig cfg, string args, out string why)
    {
        why = null;
        try { return Process.Start(UploaderStartInfo(cfg, args)); }
        catch (Exception e) { why = $"could not run {cfg.python}: {e.Message}"; return null; }
    }

    static ProcessStartInfo UploaderStartInfo(UploadConfig cfg, string args) =>
        new ProcessStartInfo(cfg.python, $"\"{UploaderScript}\" {args}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Directory.GetCurrentDirectory(),   // the project root: git log for the release notes
        };

    static string CommonArgs(UploadConfig cfg) =>
        $"--package {PackageName} --key \"{KeyPath(cfg)}\" --status-file \"{UploadStatusFile}\"";

    static string PackageName => PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);

    // ------------------------------------------------------------------ watching it finish

    static bool _watching;
    static Status _watchedFor;
    static double _nextPoll;

    // Polls the uploader's status file and reports once when it settles. `s` is the PhoneDeploy run it belongs to
    // (null for a setup check), whose status is finished from the upload's outcome.
    static void WatchUploader(Status s)
    {
        _watchedFor = s;
        if (_watching) return;
        _watching = true;
        EditorApplication.update += PollUploader;
    }

    static void PollUploader()
    {
        if (EditorApplication.timeSinceStartup < _nextPoll) return;
        _nextPoll = EditorApplication.timeSinceStartup + 2.0;

        var u = ReadUploadStatus();
        if (u == null) return;
        if (u.state == "working")
        {
            // The uploader's own steps (signing in / uploading N MB / uploaded in N s) on the status-bar spinner.
            if (!string.IsNullOrEmpty(u.message)) RunProgress(u.message);
            return;
        }

        EditorApplication.update -= PollUploader;
        _watching = false;
        var s = _watchedFor;
        _watchedFor = null;

        if (u.state == "checked")
        {
            Debug.Log("PlayUpload: setup OK.\n" + u.report);
            EndProgress(true, "Play setup OK");
            return;
        }
        bool ok = u.state == "committed";
        if (s != null) Finish(s, ok ? "deployed" : "failed", "release",
                              (ok ? "Google Play: " : "Google Play upload failed: ") + u.message);
        else
        {
            if (ok) Debug.Log("PlayUpload: " + u.message);
            else Debug.LogError("PlayUpload: " + u.message);
            EndProgress(ok, ok ? "Play check OK" : "Play check failed");
        }
    }

    // ------------------------------------------------------------------ being seen to work

    // A Play run used to report only to two JSON files and one Console line at the end - from the editor a run that
    // stopped at its first check looked exactly like one that never started. Now it has a status-bar spinner
    // (Window > General > Progress) that names the current step, and ends with an overlay toast. None of it is a
    // dialog, so Tokenmaxxer can still drive it through the MCP.
    static int _progressId = -1;

    static void RunProgress(string step)
    {
        if (_progressId < 0 || !Progress.Exists(_progressId))
            _progressId = Progress.Start("Google Play internal testing", step, Progress.Options.Indefinite);
        else
            Progress.SetDescription(_progressId, step);
    }

    static void EndProgress(bool ok, string headline, string detail = null)
    {
        if (_progressId >= 0 && Progress.Exists(_progressId))
        {
            if (!string.IsNullOrEmpty(detail)) Progress.SetDescription(_progressId, detail);
            Progress.Finish(_progressId, ok ? Progress.Status.Succeeded : Progress.Status.Failed);
        }
        _progressId = -1;
        var window = EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow : SceneView.lastActiveSceneView;
        if (window != null) window.ShowNotification(new GUIContent(headline), 8.0);
    }

    // Called by Finish for every PhoneDeploy run; only the Play requests own a progress item.
    static void EndRunProgress(Status s)
    {
        if (s.request != "play-internal" && s.request != "play-upload") return;
        bool ok = s.state == "deployed";
        string headline = ok ? $"On Google Play: {s.version} (code {s.versionCode})"
                        : s.state == "skipped" ? "Play upload stopped before building - see Console"
                        : "Play upload FAILED - see Console";
        EndProgress(ok, headline, s.message);
    }

    // A domain reload (a script change mid-upload) drops the poller; pick it back up from the status files.
    [InitializeOnLoadMethod]
    static void ResumeWatchingUpload()
    {
        var u = ReadUploadStatus();
        if (u == null || u.state != "working") return;
        // An uploader that was killed never settles its file. Past a couple of hours it is not coming back, and
        // watching it would keep the upload menus greyed out for good.
        if ((DateTime.Now - File.GetLastWriteTime(UploadStatusFile)).TotalHours > 2) return;
        Status s = null;
        try
        {
            if (File.Exists(StatusFile)) s = JsonUtility.FromJson<Status>(File.ReadAllText(StatusFile));
        }
        catch { }
        if (s != null && s.state != "uploading") s = null;
        EditorApplication.delayCall += () =>
        {
            RunProgress(string.IsNullOrEmpty(u.message) ? "uploading" : u.message);
            WatchUploader(s);
        };
    }

    static UploadStatus ReadUploadStatus()
    {
        try
        {
            return File.Exists(UploadStatusFile)
                ? JsonUtility.FromJson<UploadStatus>(File.ReadAllText(UploadStatusFile))
                : null;
        }
        catch { return null; }   // mid-write; the next poll reads it
    }

    static void WriteUploadStatus(UploadStatus u)
    {
        Directory.CreateDirectory(UploadDir);
        File.WriteAllText(UploadStatusFile, JsonUtility.ToJson(u, true));
    }

    // ------------------------------------------------------------------ setup

    static string DefaultKeyPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".draftmaster", "play-service-account.json");

    static string KeyPath(UploadConfig cfg)
    {
        if (!string.IsNullOrEmpty(cfg.serviceAccountKey)) return cfg.serviceAccountKey;
        string env = Environment.GetEnvironmentVariable("DRAFTMASTER_PLAY_KEY");
        return !string.IsNullOrEmpty(env) ? env : DefaultKeyPath;
    }

    // What stops an upload before it starts, or null.
    static string SetupProblem(UploadConfig cfg)
    {
        string key = KeyPath(cfg);
        if (!File.Exists(key))
            return $"No Google Play service account key at {key}. See Docs/Play-Internal-Testing.md " +
                   $"(or set serviceAccountKey in {UploadConfigFile}).";
        if (!File.Exists(UploaderScript)) return $"{UploaderScript} is missing.";
        if (string.IsNullOrEmpty(PackageName)) return "No Android application identifier in Player Settings.";
        return null;
    }

    static UploadConfig LoadConfig()
    {
        UploadConfig cfg = null;
        try
        {
            if (File.Exists(UploadConfigFile)) cfg = JsonUtility.FromJson<UploadConfig>(File.ReadAllText(UploadConfigFile));
        }
        catch (Exception e) { Debug.LogWarning($"PlayUpload: {UploadConfigFile} unreadable ({e.Message}); using defaults."); }
        if (cfg == null)
        {
            cfg = new UploadConfig();
            SaveConfig(cfg);
        }
        if (string.IsNullOrEmpty(cfg.track)) cfg.track = "internal";
        if (cfg.releaseStatus != "draft") cfg.releaseStatus = "completed";
        if (string.IsNullOrEmpty(cfg.python)) cfg.python = "python";
        return cfg;
    }

    static void SaveConfig(UploadConfig cfg)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(UploadConfigFile));
        File.WriteAllText(UploadConfigFile, JsonUtility.ToJson(cfg, true));
    }
}
