# Google Play internal testing

`Draftmaster > Build > Build + Upload To Play Internal Testing` takes the project to the testers in one click:

1. **Version.** Asks Google Play for the highest version code it already holds (every bundle and every
   track) and bumps `bundleVersion` / `bundleVersionCode` past it, together with any bumps phone patches
   counted (`UserSettings/PhoneDeployPendingBumps.txt`). Play refuses any version code it has already seen. This
   runs *before* the build, so a missing key or no network stops the run straight away instead of after a
   ten-minute build.
2. **Build.** Switches to an App Bundle (`.aab`) with Development Build off, signs it with the upload
   keystore, and writes `Builds/Android/Draftmaster3-Demo.<version>.aab`. Then the settings go straight back
   to a development APK, because that is what the phone's Patch And Run needs.
3. **Upload.** `Tools/play_upload.py` runs in the background (the editor stays usable, and closing Unity
   does not stop it). It uploads the bundle, sets the `internal` track to that release with the last few
   commit subjects as release notes, and commits. Testers get it in a few minutes.

Progress:

- **In the editor:** a spinner in the status bar (bottom right; Window > General > Progress) names the step
  it is on: checking setup, asking Play, building, then the uploader's own signing in / uploading N MB. It ends
  with a toast over the focused window and a Console line: a warning if the run stopped before building (no
  key, no keystore password, Play unreachable), an error if it failed, a plain log line if it reached Play.
  The one-minute Play query shows a progress bar so the editor doesn't look frozen.
- `Library/PhoneDeploy/status.json` — the whole run (request `play-internal`). It is `uploading` while the
  upload is running, then `deployed` or `failed`. Tokenmaxxer polls this file.
- `Library/PlayUpload/status.json` — the uploader's own progress.
- The Console gets one line when the upload finishes.

Nothing in the flow opens a dialog, so Tokenmaxxer can run it through the MCP like the phone deploy.

## One-time setup

1. **Upload keystore.** Player Settings > Publishing Settings > Custom Keystore. You have to type its
   passwords in again after every editor restart, because Unity forgets them. The build stops early with a
   message if they are missing.
2. **App in the Play Console.** The app has to exist, and its first bundle has to be uploaded by hand.
   The API can't create an app.
3. **Service account.**
   - Google Cloud Console: pick (or create) a project, enable **Google Play Android Developer API**, then
     create a service account under IAM & Admin > Service Accounts. Add a key (Keys > Add key > JSON) and
     download it.
   - Play Console > **Users and permissions** > Invite new users: enter the service account's email. Under
     app permissions, add this app with *Release to testing tracks* (and *View app information*).
     Permissions can take a while to take effect the first time.
   - Save the key **outside the repo**, at `%USERPROFILE%\.draftmaster\play-service-account.json`. To keep it
     somewhere else, set `DRAFTMASTER_PLAY_KEY` or `serviceAccountKey` in `UserSettings/PlayUpload.json`.
4. **Python.** Install Python 3 on PATH, plus the `rsa` package: `python -m pip install rsa`. It is the
   only package needed. The uploader uses the standard library for HTTP and doesn't need the Google
   client libraries.
5. Run `Draftmaster > Build > Check Play Upload Setup`. It signs in, lists what each track holds, and
   changes nothing.

## Settings — `UserSettings/PlayUpload.json`

This file is per machine and is never committed. It is written with defaults on first use.

| Field | Default | Meaning |
| --- | --- | --- |
| `serviceAccountKey` | empty | Path to the JSON key. When empty, `DRAFTMASTER_PLAY_KEY` is used, then the default path above |
| `track` | `internal` | Also `alpha` (closed testing), `beta` (open testing) or `production` |
| `releaseStatus` | `completed` | `completed` goes out to testers at once. `draft` waits for a click in the Console |
| `python` | `python` | Interpreter to run. Use a full path if `python` isn't on the editor's PATH |
| `lastBundle` | — | The last bundle built for upload, used by *Upload Last Bundle* |

An app that has never been published can only take **draft** releases. When the commit fails for that
reason, the uploader commits again as a draft by itself. The status then says to roll it out from the
Console.

## By hand

```
python Tools/play_upload.py check  --package com.phooey.draftmaster3 --key <key.json>
python Tools/play_upload.py upload --package com.phooey.draftmaster3 --key <key.json> \
       --aab Builds/Android/Draftmaster3-Demo.0.7.7.aab --version-name 0.7.7 [--notes "..."]
```

## When it fails

| Message | Fix |
| --- | --- |
| `No Google Play service account key at …` | Step 3 above |
| `Python package 'rsa' is missing` | `python -m pip install rsa` |
| `HTTP 401` / `403 … does not have permission` | Invite the service account in Play Console > Users and permissions. A new grant can take hours to apply |
| `HTTP 404 … Package not found` | The app does not exist in the Console yet, or the package name differs from Player Settings |
| `version code N is not above M` | Something uploaded a higher code since the build started. Run it again |
| `Keystore password not set` | Re-enter the passwords in Publishing Settings |
| Upload failed after a good build | `Upload Last Bundle To Play Internal Testing` — no need to rebuild |
