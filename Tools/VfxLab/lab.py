#!/usr/bin/env python3
"""
RimArt VFX Lab.

    python3 Tools/VfxLab/lab.py              record, serve on http://localhost:8765, re-record on change
    python3 Tools/VfxLab/lab.py --record-only
    python3 Tools/VfxLab/lab.py --no-watch   record once and serve

Recording runs the mod's own drawing code (Tools/VfxLab/Recorder) and writes
Tools/VfxLab/recordings/. The page is served from the repository root rather than opened as a
file, because WebGL will not read the mod's PNGs over file://.

While serving, every .cs file under Source/RimArt and the recorder is polled once a second.
A change re-records, and the open page picks the new recording up by itself, keeping its time,
zoom and selection. A failed build or self-check leaves the previous recordings in place and
shows the error on the page.

Animation clips (the json Melee Animation plays) are listed in recordings/animations.json: the
mod's own Animations/*.json, and Melee Animation's clips when that mod is installed. Its folder is
read where it is installed and served under /_am/; nothing of theirs is copied into this
repository. Set RIMART_MELEE_ANIMATION to the mod folder if it is not found. The list is rebuilt
when a file in Animations/ changes, and the open page reloads the clips.
"""
import argparse, http.server, json, os, pathlib, re, socketserver, subprocess, sys, threading, time, datetime

LAB = pathlib.Path(__file__).resolve().parent
ROOT = LAB.parent.parent
RECORDER = LAB / "Recorder"
RECORDINGS = LAB / "recordings"
WATCHED = [ROOT / "Source" / "RimArt", RECORDER]
PORT = 8765
ANIMATIONS = ROOT / "Animations"
# Melee Animation, Steam workshop item 2944488802. First folder that exists wins.
MELEE_ANIMATION_DIRS = [
    os.environ.get("RIMART_MELEE_ANIMATION", ""),
    os.path.expanduser("~/Library/Application Support/Steam/steamapps/workshop/content/294100/2944488802"),
    "/mnt/c/Program Files (x86)/Steam/steamapps/workshop/content/294100/2944488802",
    "/mnt/c/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/MeleeAnimation",
    os.path.expanduser("~/.steam/steam/steamapps/workshop/content/294100/2944488802"),
    os.path.expanduser("~/.local/share/Steam/steamapps/workshop/content/294100/2944488802"),
]


def melee_animation_dir():
    for candidate in MELEE_ANIMATION_DIRS:
        if candidate and (pathlib.Path(candidate) / "Animations").is_dir():
            return pathlib.Path(candidate)
    return None


def clip_facts(path):
    """Length in seconds, and whether any part is a held melee weapon (ItemA, ItemB...)."""
    # Their exporter writes a BOM and bare Infinity, which Python's json accepts.
    try:
        clip = json.loads(path.read_text(encoding="utf-8-sig"))
        return float(clip.get("Length", 0)), any(re.fullmatch(r"(.*/)?Item[A-Z]", p.get("Path", "")) for p in clip.get("Parts", []))
    except (OSError, ValueError):
        return 0.0, False


def clip_sets(folder, url, kit, source):
    """One entry per clip. <Name>North.json and <Name>South.json join <Name>.json as its facings."""
    names = {p.stem: p for p in sorted(folder.glob("*.json"))}
    sets = []
    for name, path in names.items():
        if any(name.endswith(s) and name[:-len(s)] in names for s in ("North", "South")):
            continue
        clips = {"East": f"{url}/{name}.json"}
        for facing in ("North", "South"):
            if name + facing in names:
                clips[facing] = f"{url}/{name}{facing}.json"
        length, items = clip_facts(path)
        sets.append({"id": f"{source}-{name}", "name": name, "kit": kit, "source": source,
                     "length": length, "items": items, "clips": clips})
    return sets


THING_DEF = re.compile(r"<ThingDef\b[^>]*>(.*?)</ThingDef>", re.S)


def xml_field(block, tag):
    found = re.search(rf"<{tag}>\s*(.*?)\s*</{tag}>", block, re.S)
    return found.group(1) if found else None


def texture_folder(mod_root, tex_path):
    """The folder under a mod that holds <tex_path>.png, relative to the mod, or None."""
    for folder in ("Textures", "1.6/Textures", "Common/Textures", "1.5/Textures"):
        if (mod_root / folder / f"{tex_path}.png").is_file():
            return folder
    return None


def read_tweak(path):
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, ValueError):
        return None


def our_package_id():
    about = (ROOT / "About" / "About.xml").read_text(encoding="utf-8-sig")
    return xml_field(about, "packageId") or "unknown"


def our_weapons(theirs):
    """Every ThingDef of ours with <tools> and a single-image texture in Textures/."""
    package, weapons = our_package_id(), []
    for path in sorted((ROOT / "1.6" / "Defs").rglob("*.xml")):
        for block in THING_DEF.findall(path.read_text(encoding="utf-8-sig", errors="ignore")):
            name, tex = xml_field(block, "defName"), xml_field(block, "texPath")
            if not name or not tex or "<tools>" not in block or not (ROOT / "Textures" / f"{tex}.png").is_file():
                continue
            file = f"{name}_{package}.json"
            tweak = read_tweak(ROOT / "WeaponTweakData" / file) or (theirs and read_tweak(theirs / "WeaponTweakData" / file))
            weapons.append({"label": f"{name} (RimArt)", "def": name, "package": package, "texture": tex, "tweak": tweak or None, "tweakFile": f"WeaponTweakData/{file}"})
    return weapons


def installed_weapons(theirs):
    """Weapons of installed workshop mods that Melee Animation ships tweak data for and whose
    texture is a loose PNG. Vanilla and DLC art is inside asset bundles, so those are left out.
    Slow over /mnt/c (about two minutes), so index_animations runs it once on a thread and keeps the
    result in recordings/weapons-cache.json; delete that file to scan again."""
    workshop, mods, names = theirs.parent, {}, {}
    for about in workshop.glob("*/About/About.xml"):
        text = about.read_text(encoding="utf-8-sig", errors="ignore")
        package = xml_field(text, "packageId")
        if package:
            mods[package.lower()] = about.parent.parent
            names[about.parent.parent] = xml_field(text, "name") or about.parent.parent.name
    wanted = {}
    for path in sorted((theirs / "WeaponTweakData").glob("*.json")):
        tweak = read_tweak(path)
        mod = mods.get(str((tweak or {}).get("TextureModID", "")).lower())
        if tweak and mod and mod != theirs:
            wanted.setdefault(mod, {})[tweak["ItemDefName"]] = tweak
    weapons = []
    for mod, tweaks in wanted.items():
        for xml in mod.rglob("*.xml"):
            if "Defs" not in xml.parts or not tweaks:
                continue
            text = xml.read_text(encoding="utf-8-sig", errors="ignore")
            if not any(name in text for name in tweaks):
                continue
            for block in THING_DEF.findall(text):
                name, tex = xml_field(block, "defName"), xml_field(block, "texPath")
                folder = texture_folder(mod, tex) if name in tweaks and tex else None
                if folder:
                    weapons.append({"label": f"{name} ({names[mod]})", "def": name, "package": tweaks[name]["TextureModID"], "texture": f"mod:{mod.name}/{folder}/{tex}",
                                    "tweak": tweaks.pop(name), "tweakFile": None})
    weapons.sort(key=lambda w: w["label"])
    return weapons


WEAPON_CACHE = RECORDINGS / "weapons-cache.json"
weapon_scan_started = False


def scan_installed_weapons(theirs):
    started = time.time()
    WEAPON_CACHE.write_text(json.dumps(installed_weapons(theirs)))
    print(f"[lab] scanned installed mods for weapons in {time.time() - started:.0f}s; reload the page to list them", flush=True)
    index_animations()


def animation_stamps():
    watched = [*ANIMATIONS.glob("*.json"), *(ROOT / "WeaponTweakData").glob("*.json")]
    return {p: p.stat().st_mtime for p in watched}


def index_animations():
    RECORDINGS.mkdir(parents=True, exist_ok=True)
    sets = clip_sets(ANIMATIONS, "/Animations", "Animations: RimArt", "rimart") if ANIMATIONS.is_dir() else []
    theirs = melee_animation_dir()
    if theirs:
        sets += clip_sets(theirs / "Animations", "/_am/Animations", "Animations: Melee Animation", "am")
    (RECORDINGS / "animations.json").write_text(json.dumps({
        "stamp": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "meleeAnimation": bool(theirs),
        "sets": sets,
        "weapons": our_weapons(theirs) + (json.loads(WEAPON_CACHE.read_text()) if WEAPON_CACHE.is_file() else []),
    }, indent=1))
    print(f"[lab] {len(sets)} animation clips listed" + ("" if theirs else " (Melee Animation not found)"), flush=True)
    global weapon_scan_started
    if theirs and not WEAPON_CACHE.is_file() and not weapon_scan_started:
        weapon_scan_started = True
        threading.Thread(target=scan_installed_weapons, args=(theirs,), daemon=True).start()


def snapshot():
    stamps = {}
    for base in WATCHED:
        for path in base.rglob("*"):
            if path.suffix not in (".cs", ".csproj") or "bin" in path.parts or "obj" in path.parts:
                continue
            try:
                stamps[path] = path.stat().st_mtime
            except FileNotFoundError:
                pass
    return stamps


def write_status(ok, log):
    RECORDINGS.mkdir(parents=True, exist_ok=True)
    (RECORDINGS / "status.json").write_text(json.dumps({
        "ok": ok,
        "stamp": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "log": log[-6000:],
    }, indent=2))


def record():
    started = time.time()
    print(f"[lab] recording...", flush=True)
    result = subprocess.run(
        ["dotnet", "run", "--project", str(RECORDER), "--", "--out", str(RECORDINGS)],
        cwd=ROOT, capture_output=True, text=True)
    log = (result.stdout + result.stderr).strip()
    ok = result.returncode == 0
    write_status(ok, log)
    print(log, flush=True)
    print(f"[lab] {'recorded' if ok else 'FAILED'} in {time.time() - started:.1f}s", flush=True)
    return ok


def watch():
    before = snapshot()
    clips = animation_stamps()
    while True:
        time.sleep(1.0)
        if animation_stamps() != clips:
            time.sleep(0.4)
            clips = animation_stamps()
            index_animations()
        now = snapshot()
        if now != before:
            changed = sorted({p for p in set(now) | set(before) if now.get(p) != before.get(p)})
            for p in changed[:5]:
                print(f"[lab] changed: {p.relative_to(ROOT)}", flush=True)
            # Let an editor finish writing a batch of files before building.
            time.sleep(0.4)
            before = snapshot()
            record()


class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(ROOT), **kwargs)

    def translate_path(self, path):
        # /_am/... is Melee Animation's install folder: its clips and its hand texture.
        if path.startswith("/_am/"):
            theirs = melee_animation_dir()
            if theirs is None:
                return str(ROOT / "__missing__")
            inner = super().translate_path("/" + path[len("/_am/"):])
            return str(theirs / pathlib.Path(inner).relative_to(ROOT))
        # /_mod/<workshop id>/... is another installed mod, for the weapon textures listed above.
        if path.startswith("/_mod/"):
            theirs, (mod, _, rest) = melee_animation_dir(), path[len("/_mod/"):].partition("/")
            if theirs is None or not mod.isdigit():
                return str(ROOT / "__missing__")
            inner = super().translate_path("/" + rest)
            return str(theirs.parent / mod / pathlib.Path(inner).relative_to(ROOT))
        return super().translate_path(path)

    def end_headers(self):
        # Recordings change under the page; never let the browser keep an old one.
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def log_message(self, fmt, *args):
        pass


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--record-only", action="store_true")
    parser.add_argument("--no-watch", action="store_true")
    parser.add_argument("--port", type=int, default=PORT)
    args = parser.parse_args()

    ok = record()
    index_animations()
    if args.record_only:
        sys.exit(0 if ok else 1)

    if not args.no_watch:
        threading.Thread(target=watch, daemon=True).start()

    socketserver.ThreadingTCPServer.allow_reuse_address = True
    with socketserver.ThreadingTCPServer(("127.0.0.1", args.port), Handler) as server:
        print(f"[lab] open http://localhost:{args.port}/Tools/VfxLab/web/", flush=True)
        try:
            server.serve_forever()
        except KeyboardInterrupt:
            pass


if __name__ == "__main__":
    main()
