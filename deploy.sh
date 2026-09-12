#!/usr/bin/env bash
# Copy this mod into the local RimWorld Mods folder for testing.
#
#   ./deploy.sh         the whole mod, from scratch
#   ./deploy.sh anims    regenerate and copy only the animation clips
#
# The second form exists because tuning a throw is a loop you run twenty times, and the full
# deploy deletes and rewrites the mod folder - which RimWorld will not reread without a
# restart anyway. The clips are the one thing it *will* reread on demand: regenerate, copy the
# three json files, then in game run dev mode -> Melee Animation -> Reload all animations. No
# restart, no reload of the save.
set -euo pipefail
DEST="/mnt/c/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/RimArt"
SRC="$(cd "$(dirname "$0")" && pwd)"

if [ "${1:-}" = "anims" ]; then
  python3 "$SRC/make_throw_anim.py"
  mkdir -p "$DEST/Animations"
  cp "$SRC/Animations/"*.json "$DEST/Animations/"
  echo "Clips updated in: $DEST/Animations"
  echo "In game: dev mode -> Melee Animation -> Reload all animations"
  exit 0
fi

rm -rf "$DEST"
mkdir -p "$DEST"
for item in About 1.6 Textures Languages Animations Patch_MeleeAnimation loadFolders.xml; do
  [ -e "$SRC/$item" ] && cp -r "$SRC/$item" "$DEST/"
done
echo "Deployed to: $DEST"
