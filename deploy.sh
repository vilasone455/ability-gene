#!/usr/bin/env bash
# Copy this mod into the local RimWorld Mods folder for testing.
set -euo pipefail
DEST="/mnt/c/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/AbilityGenes"
SRC="$(cd "$(dirname "$0")" && pwd)"
rm -rf "$DEST"
mkdir -p "$DEST"
for item in About 1.6 Textures Languages loadFolders.xml; do
  [ -e "$SRC/$item" ] && cp -r "$SRC/$item" "$DEST/"
done
echo "Deployed to: $DEST"
