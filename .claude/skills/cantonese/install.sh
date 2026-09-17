#!/usr/bin/env bash
# Install the cantonese skill at user level, so it is available in every repo
# rather than only the one it is committed to.
#
#   ./install.sh            copy into ~/.claude/skills/cantonese
#   ./install.sh --link     symlink instead, so git pulls update it in place
#
# The vocabulary log lives at ~/.claude/cantonese-vocab.md and is left alone.

set -euo pipefail

src="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
dest="${HOME}/.claude/skills/cantonese"

mkdir -p "${HOME}/.claude/skills"

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
  echo "Copied skill to $dest"
fi

echo "Start a new Claude session and run /cantonese"
