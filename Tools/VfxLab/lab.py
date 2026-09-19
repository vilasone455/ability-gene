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
import argparse, http.server, json, os, pathlib, socketserver, subprocess, sys, threading, time, datetime

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


def clip_length(path):
    # Their exporter writes a BOM and bare Infinity, which Python's json accepts.
    try:
        return float(json.loads(path.read_text(encoding="utf-8-sig")).get("Length", 0))
    except (OSError, ValueError):
        return 0.0


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
        sets.append({"id": f"{source}-{name}", "name": name, "kit": kit, "source": source,
                     "length": clip_length(path), "clips": clips})
    return sets


def animation_stamps():
    return {p: p.stat().st_mtime for p in ANIMATIONS.glob("*.json")} if ANIMATIONS.is_dir() else {}


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
    }, indent=1))
    print(f"[lab] {len(sets)} animation clips listed" + ("" if theirs else " (Melee Animation not found)"), flush=True)


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
