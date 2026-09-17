#!/usr/bin/env bash
# Install the canton-claude skill at user level, so it is available in every
# repo rather than only the one it is committed to.
#
#   ./install.sh            copy into ~/.claude/skills/canton-claude
#   ./install.sh --link     symlink instead, so git pulls update it in place
#
# The vocabulary log lives at ~/.claude/cantonese-vocab.md and is left alone.

set -euo pipefail

src="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
dest="${HOME}/.claude/skills/canton-claude"

mkdir -p "${HOME}/.claude/skills"

# Clear the pre-rename install, if this machine has one.
if [ -e "${HOME}/.claude/skills/cantonese" ] || [ -L "${HOME}/.claude/skills/cantonese" ]; then
  echo "Removing old install at ${HOME}/.claude/skills/cantonese"
  rm -rf "${HOME}/.claude/skills/cantonese"
fi

if [ -e "$dest" ] || [ -L "$dest" ]; then
  echo "Replacing existing install at $dest"
  rm -rf "$dest"
fi

if [ "${1:-}" = "--link" ]; then
  ln -s "$src" "$dest"
  echo "Linked $dest -> $src"
else
  mkdir -p "$dest"
  cp "$src/SKILL.md" "$dest/SKILL.md"
  cp -r "$src/references" "$dest/references"
  cp -r "$src/scripts" "$dest/scripts"
  chmod +x "$dest/scripts/speak.sh"
  echo "Copied skill to $dest"
fi

echo
if "$src/scripts/speak.sh" --list 2>/dev/null; then
  echo "Audio is ready."
else
  echo "Skill installed. Audio needs a Cantonese voice - run"
  echo "  $dest/scripts/speak.sh --list"
  echo "for the setup steps on this machine."
fi

echo
echo "Start a new Claude session and run /canton-claude"
