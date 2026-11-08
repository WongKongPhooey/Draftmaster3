"""Push an Android App Bundle to a Google Play Console testing track (internal testing by default).

Talks to the Google Play Developer API (androidpublisher v3) with a service account key. Standard library only,
plus the pure-Python `rsa` package to sign the service account's token request (`pip install rsa` if it is
missing). Nothing here needs the Google client libraries.

    python Tools/play_upload.py check   --package com.x.y --key key.json
    python Tools/play_upload.py maxcode --package com.x.y --key key.json
    python Tools/play_upload.py upload  --package com.x.y --key key.json --aab Builds/Android/x.aab \
                                        --version-name 0.4.2 [--track internal] [--status completed]

  check    signs in, opens an edit, prints the highest version code Play holds and what each track carries,
           then throws the edit away. Changes nothing.
  maxcode  prints only the highest version code Play holds (0 when none), for the Unity build to bump past.
  upload   uploads the bundle, points the track at it, commits. Exits non-zero on any failure.

Unity drives all three from Draftmaster > Build (Assets/Editor/PlayInternalRelease.cs). Progress goes to
--status-file as JSON (state: working / committed / failed) and to stdout. The key and the access token are
never written anywhere.
"""

import argparse
import base64
import datetime
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

API = "https://androidpublisher.googleapis.com/androidpublisher/v3/applications"
UPLOAD_API = "https://androidpublisher.googleapis.com/upload/androidpublisher/v3/applications"
SCOPE = "https://www.googleapis.com/auth/androidpublisher"
NOTES_LIMIT = 500   # Play's cap on release notes per language


class PlayError(Exception):
    pass


# ------------------------------------------------------------------ status

class Status:
    def __init__(self, path):
        self.path = path
        self.data = {"state": "working", "startedAt": _now(), "message": "starting"}

    def update(self, **kw):
        self.data.update(kw)
        print(f"[play_upload] {self.data.get('state')}: {self.data.get('message')}", flush=True)
        if not self.path:
            return
        os.makedirs(os.path.dirname(os.path.abspath(self.path)), exist_ok=True)
        tmp = self.path + ".tmp"
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(self.data, f, indent=2)
        os.replace(tmp, self.path)

    def finish(self, state, message, **kw):
        self.update(state=state, message=message, finishedAt=_now(), **kw)


def _now():
    return datetime.datetime.now().replace(microsecond=0).isoformat()


# ------------------------------------------------------------------ auth

def _b64url(data):
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode("ascii")


def _der_read(buf, pos):
    """One DER TLV at pos -> (tag, value bytes, position after it)."""
    tag = buf[pos]
    pos += 1
    length = buf[pos]
    pos += 1
    if length & 0x80:
        n = length & 0x7F
        length = int.from_bytes(buf[pos:pos + n], "big")
        pos += n
    return tag, buf[pos:pos + length], pos + length


def _pkcs1_from_pkcs8_pem(pem):
    """Service account keys are PKCS#8 ('BEGIN PRIVATE KEY'); the rsa package reads PKCS#1. Unwrap it."""
    body = "".join(line for line in pem.strip().splitlines() if not line.startswith("-----"))
    der = base64.b64decode(body)
    tag, info, _ = _der_read(der, 0)                 # PrivateKeyInfo SEQUENCE
    if tag != 0x30:
        raise PlayError("service account private key is not a DER SEQUENCE")
    _, _, p = _der_read(info, 0)                     # version INTEGER
    _, _, p = _der_read(info, p)                     # algorithm SEQUENCE
    tag, key, _ = _der_read(info, p)                 # privateKey OCTET STRING = RSAPrivateKey
    if tag != 0x04:
        raise PlayError("service account private key has no PKCS#1 payload")
    return key


def access_token(key_path):
    try:
        import rsa
    except ImportError:
        raise PlayError("Python package 'rsa' is missing: run  python -m pip install rsa")

    if not os.path.isfile(key_path):
        raise PlayError(f"service account key not found at {key_path}")
    with open(key_path, encoding="utf-8") as f:
        key = json.load(f)
    if key.get("type") != "service_account":
        raise PlayError(f"{key_path} is not a service account key (type={key.get('type')})")

    token_uri = key.get("token_uri", "https://oauth2.googleapis.com/token")
    now = int(time.time())
    header = {"alg": "RS256", "typ": "JWT", "kid": key.get("private_key_id", "")}
    claims = {"iss": key["client_email"], "scope": SCOPE, "aud": token_uri, "iat": now, "exp": now + 3600}
    signing_input = (_b64url(json.dumps(header).encode()) + "." + _b64url(json.dumps(claims).encode())).encode()
    private = rsa.PrivateKey.load_pkcs1(_pkcs1_from_pkcs8_pem(key["private_key"]), format="DER")
    signature = rsa.sign(signing_input, private, "SHA-256")
    assertion = signing_input.decode() + "." + _b64url(signature)

    body = urllib.parse.urlencode({
        "grant_type": "urn:ietf:params:oauth:grant-type:jwt-bearer",
        "assertion": assertion,
    }).encode()
    reply = _request("POST", token_uri, None, body, {"Content-Type": "application/x-www-form-urlencoded"})
    if "access_token" not in reply:
        raise PlayError("token endpoint gave no access_token")
    return reply["access_token"], key["client_email"]


# ------------------------------------------------------------------ http

def _request(method, url, token, body=None, headers=None, timeout=120):
    headers = dict(headers or {})
    if token:
        headers["Authorization"] = "Bearer " + token
    if body is not None and not isinstance(body, (bytes, bytearray)) and not hasattr(body, "read"):
        body = json.dumps(body).encode()
        headers.setdefault("Content-Type", "application/json")
    req = urllib.request.Request(url, data=body, method=method, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            raw = resp.read()
    except urllib.error.HTTPError as e:
        detail = e.read().decode("utf-8", "replace")
        try:
            detail = json.loads(detail).get("error", {}).get("message", detail)
        except ValueError:
            pass
        raise PlayError(f"{method} {url.split('?')[0]} -> HTTP {e.code}: {detail}")
    except urllib.error.URLError as e:
        raise PlayError(f"{method} {url.split('?')[0]} failed: {e.reason}")
    return json.loads(raw) if raw else {}


class Edit:
    """One Play Console edit: a draft of changes that only goes live on commit."""

    def __init__(self, token, package):
        self.token = token
        self.package = package
        self.id = _request("POST", f"{API}/{package}/edits", token, {})["id"]
        self.base = f"{API}/{package}/edits/{self.id}"

    def bundles(self):
        return _request("GET", f"{self.base}/bundles", self.token).get("bundles", [])

    def tracks(self):
        return _request("GET", f"{self.base}/tracks", self.token).get("tracks", [])

    def max_version_code(self):
        codes = [int(b["versionCode"]) for b in self.bundles() if "versionCode" in b]
        for t in self.tracks():
            for r in t.get("releases", []):
                codes += [int(c) for c in r.get("versionCodes", [])]
        return max(codes) if codes else 0

    def upload_bundle(self, path):
        size = os.path.getsize(path)
        url = (f"{UPLOAD_API}/{self.package}/edits/{self.id}/bundles"
               "?uploadType=media&ackBundleInstallationWarning=true")
        with open(path, "rb") as f:
            # urllib streams a file object when Content-Length is given. A big bundle on a slow line takes a
            # while, so the timeout is per socket operation and generous.
            return _request("POST", url, self.token, f,
                            {"Content-Type": "application/octet-stream", "Content-Length": str(size)},
                            timeout=600)

    def set_track(self, track, version_code, name, status, notes):
        release = {"name": name, "versionCodes": [str(version_code)], "status": status}
        if notes:
            release["releaseNotes"] = [{"language": "en-US", "text": notes}]
        return _request("PUT", f"{self.base}/tracks/{track}", self.token, {"track": track, "releases": [release]})

    def commit(self):
        return _request("POST", f"{self.base}:commit", self.token, {})

    def discard(self):
        try:
            _request("DELETE", self.base, self.token)
        except PlayError:
            pass   # an edit nobody commits expires on its own


# ------------------------------------------------------------------ release notes

def default_notes(version_name, count=8):
    """The last few commit subjects, trimmed to Play's 500 characters. Empty when git is unavailable."""
    try:
        out = subprocess.run(["git", "log", f"-{count}", "--format=- %s"], capture_output=True, text=True,
                             timeout=20, check=True).stdout.strip()
    except (OSError, subprocess.SubprocessError):
        return f"Build {version_name}"
    text = f"Build {version_name}\n{out}" if out else f"Build {version_name}"
    if len(text) > NOTES_LIMIT:
        text = text[:NOTES_LIMIT - 1].rsplit("\n", 1)[0]
    return text


# ------------------------------------------------------------------ commands

def cmd_check(args, status):
    token, who = access_token(args.key)
    edit = Edit(token, args.package)
    try:
        top = edit.max_version_code()
        lines = [f"signed in as {who}", f"package {args.package}", f"highest version code on Play: {top}"]
        for t in edit.tracks():
            for r in t.get("releases", []):
                lines.append(f"  {t['track']}: {r.get('name', '?')} codes {','.join(r.get('versionCodes', []))} "
                             f"({r.get('status', '?')})")
    finally:
        edit.discard()
    status.finish("checked", "; ".join(lines[:3]), maxVersionCode=top, report="\n".join(lines))
    print("\n".join(lines))


def cmd_maxcode(args, status):
    token, _ = access_token(args.key)
    edit = Edit(token, args.package)
    try:
        top = edit.max_version_code()
    finally:
        edit.discard()
    print(top)


def cmd_upload(args, status):
    if not args.aab or not os.path.isfile(args.aab):
        raise PlayError(f"bundle not found: {args.aab}")
    mb = os.path.getsize(args.aab) / (1024 * 1024)
    status.update(message="signing in", aab=args.aab, track=args.track, package=args.package)
    token, who = access_token(args.key)

    edit = Edit(token, args.package)
    committed = False
    try:
        status.update(message=f"edit {edit.id} open as {who}", editId=edit.id)
        top = edit.max_version_code()
        if args.version_code and args.version_code <= top:
            raise PlayError(f"version code {args.version_code} is not above {top}, the highest Play already holds. "
                            "Bump the version and rebuild.")

        status.update(message=f"uploading {os.path.basename(args.aab)} ({mb:.0f} MB)")
        t0 = time.time()
        bundle = edit.upload_bundle(args.aab)
        code = int(bundle["versionCode"])
        status.update(message=f"uploaded in {time.time() - t0:.0f} s: version code {code}", versionCode=code)

        notes = args.notes if args.notes is not None else default_notes(args.version_name or str(code))
        name = args.version_name or str(code)
        edit.set_track(args.track, code, name, args.status, notes)
        try:
            edit.commit()
            used = args.status
        except PlayError as e:
            # An app that has never been published can only take draft releases. Say so and go again as a draft:
            # the build is still on the track, and promoting it is one click in the Console.
            if args.status != "draft" and "draft" in str(e).lower():
                status.update(message="app is still a draft on Play; committing the release as a draft")
                edit.set_track(args.track, code, name, "draft", notes)
                edit.commit()
                used = "draft"
            else:
                raise
        committed = True
        extra = " Draft app: roll it out from the Play Console." if used == "draft" else ""
        status.finish("committed", f"{name} (code {code}) is on the {args.track} track as {used}.{extra}",
                      versionCode=code, releaseStatus=used)
    finally:
        if not committed:
            edit.discard()


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("command", choices=["check", "maxcode", "upload"])
    p.add_argument("--package", required=True, help="application id, e.g. com.Phooey.Draftmaster3")
    p.add_argument("--key", default=os.environ.get("DRAFTMASTER_PLAY_KEY"),
                   help="service account JSON key (default: $DRAFTMASTER_PLAY_KEY)")
    p.add_argument("--aab", help="bundle to upload")
    p.add_argument("--version-name", help="release name shown in the Console (Unity's bundleVersion)")
    p.add_argument("--version-code", type=int, default=0, help="expected version code, checked before uploading")
    p.add_argument("--track", default="internal", help="internal (default), alpha, beta or production")
    p.add_argument("--status", default="completed", choices=["completed", "draft"],
                   help="completed = live to testers at once; draft = waits for a click in the Console")
    p.add_argument("--notes", help="release notes (default: the last few commit subjects)")
    p.add_argument("--status-file", help="JSON progress file for Unity to poll")
    args = p.parse_args()

    status = Status(args.status_file)
    if not args.key:
        status.finish("failed", "no service account key: pass --key or set DRAFTMASTER_PLAY_KEY")
        return 2
    try:
        {"check": cmd_check, "maxcode": cmd_maxcode, "upload": cmd_upload}[args.command](args, status)
        return 0
    except PlayError as e:
        status.finish("failed", str(e))
        return 1
    except Exception as e:  # noqa: BLE001 — anything else still has to reach the status file
        status.finish("failed", f"{type(e).__name__}: {e}")
        raise


if __name__ == "__main__":
    sys.exit(main())
