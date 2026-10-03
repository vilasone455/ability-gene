#!/usr/bin/env python3
"""
RimArt Sound Lab: hear the game's clips and the mod's own sounds in a browser, layer them the way
a SoundDef does, and pick one per ability moment.

    python3 Tools/SoundLab/extract.py      once: the game's clips into Tools/SoundLab/clips/
    python3 Tools/SoundLab/serve.py        open http://localhost:8766/Tools/SoundLab/web/

The page is served from the repository root so it can reach the clips and the mod's Sounds/.
Tools/VfxLab/lab.py serves the same page and endpoints (soundlab.py) next to the VFX lab, which
plays its sound markers with them; this server is for the sound lab alone, without the recorder.

Candidates for each ability are written by hand in Tools/SoundLab/candidates.json.
"""
import argparse, http.server, pathlib, socketserver, sys

LAB = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(LAB))
import soundlab  # noqa: E402

PORT = 8766


class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *a, **kw):
        super().__init__(*a, directory=str(soundlab.ROOT), **kw)

    def log_message(self, fmt, *args):
        pass

    def end_headers(self):
        self.send_header("Cache-Control", "no-store" if not self.path.endswith(".mp3") else "max-age=86400")
        super().end_headers()

    def do_GET(self):
        if soundlab.handle_get(self):
            return
        if self.path == "/":
            self.send_response(302)
            self.send_header("Location", "/Tools/SoundLab/web/")
            self.end_headers()
            return
        return super().do_GET()

    def do_POST(self):
        if not soundlab.handle_post(self):
            self.send_error(404)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--port", type=int, default=PORT)
    args = ap.parse_args()
    if not (LAB / "clips" / "index.json").exists():
        print("No clips yet: run python3 Tools/SoundLab/extract.py first. Serving anyway.")
    socketserver.ThreadingTCPServer.allow_reuse_address = True
    with socketserver.ThreadingTCPServer(("127.0.0.1", args.port), Handler) as server:
        print(f"Sound lab on http://localhost:{args.port}/Tools/SoundLab/web/")
        server.serve_forever()


if __name__ == "__main__":
    main()
