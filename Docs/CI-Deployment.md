# Shipping builds — the CI pipeline

`.github/workflows/build.yml` turns a push to `master` into a **Windows standalone** and an
**Android package**, both stamped with a version that goes up by one every time, and publishes
them to a tagged GitHub Release.

Nothing here changes how you build locally. `File > Build Settings` in the editor still works
exactly as before; CI is a second, unattended way to produce the same players.

---

## What a run does

```
push to master
      │
      ├── Version ──────── reads VERSION, adds the run number, checks the licence secrets
      │        │
      │        ├── Build Windows x64  (GameCI ubuntu windows-mono image) ──┐
      │        └── Build Android      (GameCI ubuntu android image)      ──┤
      │                                                                   ▼
      │                                                          Release  v3.0.<run>
      │                                                          (tag + attached binaries)
      │
      └── EditMode tests ── reported in the run summary, never blocks a build
```

Both builds run on `ubuntu-latest`. GameCI's `windows-mono` image cross-compiles the Windows
player from Linux, so neither platform needs a Windows runner (which bills at 2× the minutes).

**The tests do not gate the builds.** A red suite is reported in the run summary and its results
uploaded as an artifact, but `master` still produces binaries. To make it a hard gate instead,
set `continue-on-error: false` on the `tests` job and add `tests` to the `build` job's `needs:`.

---

## One-time setup

Nothing builds until Unity can activate a licence in CI. The pipeline checks for this first and
fails with a readable message in the run summary rather than an obscure editor error.

Everything below lives under **Settings → Secrets and variables → Actions**.

### Required secrets

| Secret | Where it comes from |
|---|---|
| `UNITY_EMAIL` | The email for your Unity account |
| `UNITY_PASSWORD` | That account's password |
| `UNITY_LICENSE` | **Personal licence only** — the full contents of the `.ulf` file (see below) |
| `UNITY_SERIAL` | **Pro/Plus only** — your serial, instead of `UNITY_LICENSE` |

Set `UNITY_LICENSE` *or* `UNITY_SERIAL`, not both.

**Getting the `.ulf` for a Personal licence.** GameCI has a request/activate dance because
Personal licences cannot be activated headlessly from a serial:

1. Run the [GameCI activation workflow](https://game.ci/docs/github/activation) once — it produces
   an `Unity_v6000.x.alf` file as a build artifact.
2. Upload that `.alf` at <https://license.unity3d.com/manual>, and download the `.ulf` it returns.
3. Paste the **entire** contents of the `.ulf` (it is XML, several lines) into the `UNITY_LICENSE`
   secret.

A Personal licence is bound to a machine seat. If the same account is signed in on your desktop
you may need to return the seat, or use a separate Unity account for CI.

### Optional secrets — Android signing

Leave these unset and the Android build still succeeds: GameCI turns off the custom keystore and
the output is debug-signed. That is fine for sideloading onto a test device, and **rejected by the
Play Store**.

| Secret | Notes |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | The keystore file, base64-encoded (see below) |
| `ANDROID_KEYSTORE_PASS` | Keystore password |
| `ANDROID_KEYALIAS_NAME` | Key alias inside the keystore |
| `ANDROID_KEYALIAS_PASS` | Password for that alias |

```bash
base64 -w0 dm2rt.keystore    # macOS: base64 -i dm2rt.keystore
```

Paste that single line as `ANDROID_KEYSTORE_BASE64`.

> The project currently keeps `dm2rt.keystore` and `admin.keystore` committed at the repo root.
> The pipeline deliberately does **not** use those — it writes the keystore from the secret into
> the container and throws it away with the runner. Committed keystores are readable by anyone with
> repo access; worth moving out of git when you get a chance, though nothing here depends on it.

### Optional variables

Variables, not secrets — same settings page, "Variables" tab.

| Variable | Default | What it does |
|---|---|---|
| `ANDROID_EXPORT_TYPE` | `androidAppBundle` | `androidPackage` to produce a sideloadable `.apk` instead of a Play Store `.aab` |
| `ANDROID_VERSION_CODE_OFFSET` | `0` | Added to every `versionCode` — see below |
| `CACHE_UNITY_LIBRARY` | unset | `true` to cache the `Library/` folder between runs |

---

## Versioning

`ProjectSettings.asset` in this project is **binary-serialised**, so the version cannot be patched
as text by a script. The pipeline sets it through the Unity API instead — GameCI's
`versioning: Custom` assigns `PlayerSettings.bundleVersion` and `bundleVersionCode` inside the
editor, which works whatever the serialisation mode.

```
version name   =  <VERSION file>  .  <workflow run number>      e.g.  3.0.42
versionCode    =  <workflow run number>  +  ANDROID_VERSION_CODE_OFFSET
```

* **`VERSION`** at the repo root holds the hand-managed `MAJOR.MINOR` — currently `3.0`. Edit it
  and commit when you want a feature bump; the pipeline validates the format and fails loudly if
  it is not exactly `MAJOR.MINOR`.
* **The patch is the run number**, which GitHub increments on every run of this workflow. So every
  build gets a strictly higher version than the last, with no state to keep and nothing committed
  back to the repo.
* The version is applied **inside the CI container only**. Your working copy is never modified, and
  there are no bot commits on `master`.

### The two things that can break versionCode

Google Play refuses an upload whose `versionCode` is not higher than the last one you published.

1. **Renaming or replacing `build.yml` resets the run number to 1.** If that happens, set
   `ANDROID_VERSION_CODE_OFFSET` to something above your highest published code and carry on.
2. **If the app was already published** under this bundle id at, say, versionCode 40, set the offset
   to `40` before the first CI upload so codes start at 41.

The pipeline refuses to build if a computed `versionCode` would reach Play's 2100000000 ceiling.

---

## Getting the builds

**From the run** — the Actions tab, any run, "Artifacts" at the bottom. Kept 90 days.

**From Releases** — every `master` push tags the commit `v<version>` and publishes a release with
the binaries attached, so builds outlive the artifact expiry:

| File | What it is |
|---|---|
| `Draftmaster3-<version>-Windows-x64.zip` | The whole standalone player folder — unzip and run the `.exe` |
| `Draftmaster3-<version>-Android.aab` | Play Store bundle (or `.apk` if you switched the export type) |

Re-running a workflow that already released reuses the existing tag and replaces its assets rather
than failing.

---

## Running it by hand

**Actions → Build & Release → Run workflow.** Two inputs:

* **Android package format** — build an `.apk` for one run without changing the repo variable.
* **Attach the builds to a tagged GitHub Release** — off by default for manual runs, so you can
  test the pipeline without cutting a release. Pushes to `master` always release.

Manual runs still consume a run number, so versions stay unique.

---

## Before your first Play Store upload

Two Android settings the pipeline does not change, worth checking in
`Edit > Project Settings > Player > Android` and committing once:

* **Scripting backend must be IL2CPP** with **ARM64** ticked in Target Architectures. Play has
  required 64-bit since 2019 and will reject a Mono/ARMv7-only bundle. GameCI's android image
  carries the NDK, so IL2CPP builds fine in CI — it is just slower.
* **The application id** is currently a Draftmaster 2 identifier
  (`com.DuffetyWong.Draftmaster2RollingThunder`). Set the id you actually intend to publish
  Draftmaster 3 under before the first upload — it cannot be changed afterwards.

---

## Troubleshooting

**"No space left on device"** — the Unity editor images are around 7 GB. Each build job already
clears the runner's preinstalled .NET, Android SDK, GHC and Boost first. If a build outgrows even
that, move that platform to a larger runner.

**A build takes 30–60 minutes cold.** That is normal for a first Unity CI build: pulling the image,
then importing every asset from scratch. Set the `CACHE_UNITY_LIBRARY` variable to `true` to reuse
the imported `Library/` between runs — much faster when it hits, but `Library/` for a project this
size can approach GitHub's 10 GB per-repository cache budget, and uploading it every run can cost
more time than it saves. Try it; turn it off if the cache step is slow or keeps missing.

**Windows builds use the Mono backend**, because that is what cross-compiling from Linux supports.
If you need IL2CPP for the Windows player, that matrix entry has to move to a `windows-2022`
runner (billed at 2× minutes) with `unityci/editor:windows-...-windows-il2cpp` — the rest of the
workflow is unchanged.

**Activation fails with "License is not active".** Personal licences hold a machine seat. Return
the seat from your desktop editor, or give CI its own Unity account.

**Adding another platform** — one entry in the `build` job's matrix:

```yaml
- targetPlatform: StandaloneLinux64
  label: Linux x64
  slug: Linux-x64
```

`StandaloneOSX` needs `mac-mono`, which GameCI supports from Linux; iOS produces an Xcode project
that still has to be built and signed on a Mac.
