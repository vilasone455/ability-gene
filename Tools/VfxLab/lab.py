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
"""
import argparse, http.server, json, os, pathlib, socketserver, subprocess, sys, threading, time, datetime

LAB = pathlib.Path(__file__).resolve().parent
ROOT = LAB.parent.parent
RECORDER = LAB / "Recorder"
RECORDINGS = LAB / "recordings"
WATCHED = [ROOT / "Source" / "RimArt", RECORDER]
PORT = 8765


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
    while True:
        time.sleep(1.0)
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
