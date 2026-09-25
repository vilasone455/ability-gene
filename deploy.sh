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
# restart, no reload of the save. Every clip generator runs here, so adding one means adding it
# to this branch too.
set -euo pipefail
SRC="$(cd "$(dirname "$0")" && pwd)"
# The Mac's RimWorldMac.app/Mods when that install exists, otherwise the Windows Mods folder via WSL.
DEST="$(python3 "$SRC/rimworld_paths.py" MODS)/RimArt"

if [ "${1:-}" = "anims" ]; then
  python3 "$SRC/make_throw_anim.py"
  python3 "$SRC/make_gravity_anim.py"
  python3 "$SRC/make_clap_anim.py"
  python3 "$SRC/make_shinra_anim.py"
  python3 "$SRC/make_mark_anim.py"
  python3 "$SRC/make_power_pole_anim.py"
  python3 "$SRC/make_paper_bomb_anim.py"
  mkdir -p "$DEST/Animations"
  cp "$SRC/Animations/"*.json "$DEST/Animations/"
  echo "Clips updated in: $DEST/Animations"
  echo "In game: dev mode -> Melee Animation -> Reload all animations"
  exit 0
fi

rm -rf "$DEST"
mkdir -p "$DEST"

# Patch_* is a glob rather than a list of names on purpose. These folders hold the defs and
# patches that load only alongside some other mod, and one of them going missing from a deploy
# is close to undetectable: the game reads the folder only when that mod is active, so the
# build is fine, the validator is fine, and the feature is simply absent for the one person
# testing the combination. Adding a folder should not also mean remembering to edit this line.
cd "$SRC"
for item in About 1.6 Textures Languages Animations loadFolders.xml Patch_*; do
  [ -e "$item" ] && cp -r "$item" "$DEST/"
done
echo "Deployed to: $DEST"
echo "Conditional folders: $(ls -d Patch_* 2>/dev/null | tr '\n' ' ')"
