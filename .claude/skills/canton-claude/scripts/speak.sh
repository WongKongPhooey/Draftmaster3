#!/usr/bin/env bash
# Speak Cantonese aloud so the user can hear a word before repeating it back.
#
#   ./speak.sh 你好              speak one word or phrase
#   ./speak.sh 你好 多謝 搞掂     speak several, with a pause between each
#   ./speak.sh --list            report which Cantonese voice was found, say nothing
#
# Pass Chinese characters, NOT Jyutping. Speech engines read 好 correctly and
# read "hou2" as English nonsense, which would teach the opposite of the point.
# The vocabulary log stores characters alongside the Jyutping for this reason.
#
# If no Cantonese voice is installed, this exits 3 and prints how to add one.
# It deliberately does not fall back to a Mandarin or English voice: those read
# the same characters with entirely different sounds, and a confident wrong
# pronunciation is worse than silence.

set -uo pipefail

list_only=0
[ "${1:-}" = "--list" ] && { list_only=1; shift; }

if [ "$list_only" -eq 0 ] && [ "$#" -eq 0 ]; then
  echo "usage: speak.sh [--list] <chinese-text> [more-text ...]" >&2
  exit 2
fi

platform="$(uname -s)"
case "$platform" in
  Darwin) kind=macos ;;
  Linux)  if command -v powershell.exe >/dev/null 2>&1; then kind=windows; else kind=linux; fi ;;
  MINGW*|MSYS*|CYGWIN*) kind=windows ;;
  *)      kind=unknown ;;
esac

no_voice() {
  echo "No Cantonese (zh-HK) voice installed." >&2
  case "$kind" in
    macos)
      echo "Add one: System Settings > Accessibility > Spoken Content >" >&2
      echo "System Voice > Manage Voices, then Chinese (Hong Kong) - Sinji." >&2 ;;
    windows)
      echo "Add one: Settings > Time & language > Language & region >" >&2
      echo "Add a language > Chinese (Traditional, Hong Kong SAR), and tick Speech." >&2 ;;
    linux)
      echo "Install espeak-ng, which carries a 'yue' voice:" >&2
      echo "  sudo apt install espeak-ng   # or the equivalent for your distro" >&2
      echo "Its Cantonese is robotic and its tones are approximate - fine for" >&2
      echo "recognising a syllable, not for copying a tone contour." >&2 ;;
    *)
      echo "Unrecognised platform '$platform' - no speech backend to try." >&2 ;;
  esac
  exit 3
}

speak_macos() {
  local line voice
  line="$(say -v '?' 2>/dev/null | grep -m1 ' zh_HK ')" || true
  [ -n "$line" ] || return 1
  voice="$(printf '%s' "$line" | sed 's/  *zh_HK .*//')"
  [ -n "$voice" ] || return 1
  if [ "$list_only" -eq 1 ]; then echo "macOS voice: $voice (zh_HK)"; return 0; fi
  for text in "$@"; do
    echo "  $text"
    say -v "$voice" -r 140 -- "$text"
    sleep 0.4
  done
}

speak_windows() {
  local ps text_block=""
  ps=$(command -v powershell.exe || command -v powershell) || return 1
  if [ "$list_only" -eq 1 ]; then
    "$ps" -NoProfile -Command '
      Add-Type -AssemblyName System.Speech
      $s = New-Object System.Speech.Synthesis.SpeechSynthesizer
      $v = $s.GetInstalledVoices() | Where-Object { $_.VoiceInfo.Culture.Name -eq "zh-HK" }
      if ($v) { "Windows voice: " + $v[0].VoiceInfo.Name + " (zh-HK)" } else { exit 3 }
    ' || return 1
    return 0
  fi
  for text in "$@"; do
    echo "  $text"
    text_block="${text//\'/\'\'}"
    "$ps" -NoProfile -Command "
      Add-Type -AssemblyName System.Speech
      \$s = New-Object System.Speech.Synthesis.SpeechSynthesizer
      \$v = \$s.GetInstalledVoices() | Where-Object { \$_.VoiceInfo.Culture.Name -eq 'zh-HK' }
      if (-not \$v) { exit 3 }
      \$s.SelectVoice(\$v[0].VoiceInfo.Name)
      \$s.Rate = -2
      \$s.Speak('${text_block}')
    " || return 1
    sleep 0.4
  done
}

speak_linux() {
  command -v espeak-ng >/dev/null 2>&1 || return 1
  espeak-ng --voices 2>/dev/null | grep -q ' yue ' || return 1
  if [ "$list_only" -eq 1 ]; then echo "Linux voice: espeak-ng yue (synthetic, approximate tones)"; return 0; fi
  for text in "$@"; do
    echo "  $text"
    espeak-ng -v yue -s 130 -- "$text"
    sleep 0.4
  done
}

case "$kind" in
  macos)   speak_macos   "$@" || no_voice ;;
  windows) speak_windows "$@" || no_voice ;;
  linux)   speak_linux   "$@" || no_voice ;;
  *)       no_voice ;;
esac
