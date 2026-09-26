"""Where RimWorld is on this machine, for validate.py, make_wiki.py, deploy.sh and the VFX lab.

The Mac's Steam install is used when it exists, otherwise the Windows install as WSL sees it.
Directory.Build.props makes the same choice for the C# projects.

Usage from a shell: python3 rimworld_paths.py MODS   (prints one of the paths below)
"""
import glob
import os
import sys

STEAM_MAC = os.path.expanduser("~/Library/Application Support/Steam/steamapps")
STEAM_WSL = "/mnt/c/Program Files (x86)/Steam/steamapps"

MAC = os.path.isdir(os.path.join(STEAM_MAC, "common/RimWorld/RimWorldMac.app"))
STEAM = STEAM_MAC if MAC else STEAM_WSL
GAME = os.path.join(STEAM, "common/RimWorld")

# The game's own defs (Core and the DLC folders).
DATA = os.path.join(GAME, "RimWorldMac.app/Data") if MAC else os.path.join(GAME, "Data")
# Local mods; deploy.sh copies RimArt here.
MODS = os.path.join(GAME, "RimWorldMac.app/Mods") if MAC else os.path.join(GAME, "Mods")
WORKSHOP = os.path.join(STEAM, "workshop/content/294100")


def _log():
    if MAC:
        return os.path.expanduser("~/Library/Logs/Ludeon Studios/RimWorld by Ludeon Studios/Player.log")
    found = glob.glob("/mnt/c/Users/*/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Player.log")
    return found[0] if found else ""


# The last game session's log, which is the only proof a change loads.
LOG = _log()

if __name__ == "__main__":
    names = {"DATA": DATA, "MODS": MODS, "WORKSHOP": WORKSHOP, "GAME": GAME, "LOG": LOG}
    if len(sys.argv) != 2 or sys.argv[1] not in names:
        sys.exit("usage: python3 rimworld_paths.py " + "|".join(names))
    print(names[sys.argv[1]])
