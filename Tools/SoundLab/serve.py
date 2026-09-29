#!/usr/bin/env python3
"""
RimArt Sound Lab: hear the game's clips and the mod's own sounds in a browser, layer them the way
a SoundDef does, and pick one per ability moment.

    python3 Tools/SoundLab/extract.py      once: the game's clips into Tools/SoundLab/clips/
    python3 Tools/SoundLab/serve.py        open http://localhost:8766/Tools/SoundLab/web/

The page is served from the repository root so it can reach the clips and the mod's Sounds/.

- GET  /soundlab/catalog.json  built on each request: vanilla SoundDefs (Core and installed DLC),
  the mod's SoundDefs, its AbilityDefs, and its own sound files under Sounds/.
- POST /soundlab/pick          {"key": "<AbilityDef>/<moment>", "pick": {...} or null} merged into
  Tools/SoundLab/picks.json, which is committed: it is the record of what was chosen.

Candidates for each ability are written by hand in Tools/SoundLab/candidates.json.
"""
import argparse, http.server, json, pathlib, re, socketserver, sys, threading, time
import xml.etree.ElementTree as ET

LAB = pathlib.Path(__file__).resolve().parent
ROOT = LAB.parent.parent
PICKS = LAB / "picks.json"
PORT = 8766
sys.path.insert(0, str(ROOT))
import rimworld_paths  # noqa: E402
sys.path.insert(0, str(LAB))
from extract import features  # noqa: E402

AUDIO_EXT = {".ogg", ".wav", ".mp3"}
_lock = threading.Lock()
_features = {}  # (path, mtime) -> measured length, loudness and envelope of a .wav


def _range(el, name, default):
    text = el.findtext(name)
    if not text:
        return default
    parts = [float(p) for p in text.split("~")]
    return [parts[0], parts[-1]]


def parse_sound_defs(xml_path, source):
    try:
        tree = ET.parse(xml_path)
    except ET.ParseError:
        return []
    out = []
    for d in tree.getroot().iter("SoundDef"):
        name = d.findtext("defName")
        if not name:
            continue
        subs = []
        for li in d.findall("subSounds/li"):
            grains = []
            for g in li.findall("grains/li"):
                cls = g.get("Class", "")
                if g.findtext("clipFolderPath"):
                    grains.append({"folder": g.findtext("clipFolderPath").strip()})
                elif g.findtext("clipPath"):
                    grains.append({"clip": g.findtext("clipPath").strip()})
                elif cls:
                    grains.append({"other": cls})
            subs.append({
                "grains": grains,
                "volume": _range(li, "volumeRange", [50, 50]),
                "pitch": _range(li, "pitchRange", [1, 1]),
                "delay": _range(li, "startDelayRange", [0, 0]),
                "loop": (li.findtext("sustainLoop") or "").strip().lower() == "true",
                "mayRequire": li.get("MayRequire"),
            })
        out.append({
            "defName": name, "source": source,
            "sustain": (d.findtext("sustain") or "").strip().lower() == "true",
            "file": str(xml_path.relative_to(ROOT)) if source == "RimArt" else xml_path.name,
            "subs": subs,
        })
    return out


def vanilla_sound_defs():
    defs = []
    data = pathlib.Path(rimworld_paths.DATA)
    for folder in sorted(p for p in data.iterdir() if (p / "Defs").is_dir()):
        for xml in sorted((folder / "Defs").rglob("*.xml")):
            if "SoundDef" in xml.read_text(errors="ignore"):
                defs += parse_sound_defs(xml, folder.name)
    return defs


def mod_sound_defs():
    defs = []
    for xml in sorted((ROOT / "1.6" / "Defs").rglob("*.xml")):
        if "<SoundDef" in xml.read_text(errors="ignore"):
            defs += parse_sound_defs(xml, "RimArt")
    return defs


def abilities():
    out = []
    for xml in sorted((ROOT / "1.6" / "Defs" / "AbilityDefs").glob("*.xml")):
        kit = re.sub(r"^AG_|_Abilities$|\.xml$", "", xml.name.replace(".xml", ""))
        try:
            root = ET.parse(xml).getroot()
        except ET.ParseError:
            continue
        for d in root.iter("AbilityDef"):
            name = d.findtext("defName")
            if not name or d.get("Abstract") == "True":
                continue
            sounds = sorted({f"{el.tag}: {el.text.strip()}" for el in d.iter()
                             if el.tag.lower().startswith("sound") and el.text and el.text.strip()})
            out.append({
                "defName": name, "kit": kit or "Shared",
                "label": (d.findtext("label") or name).strip(),
                "description": re.sub(r"\s+", " ", (d.findtext("description") or "")).strip(),
                "xmlSounds": sounds,
            })
    return out


def mod_clips():
    """The mod's own sound files, as the game finds them: a path under Sounds/, no extension."""
    base = ROOT / "Sounds"
    out = []
    if base.is_dir():
        for f in sorted(base.rglob("*")):
            if f.suffix.lower() in AUDIO_EXT:
                rel = f.relative_to(base).with_suffix("").as_posix()
                entry = {
                    "source": "RimArt", "folder": rel.rsplit("/", 1)[0] if "/" in rel else "",
                    "clip": f.stem, "url": "/" + f.relative_to(ROOT).as_posix(),
                }
                if f.suffix.lower() == ".wav":
                    key = (str(f), f.stat().st_mtime)
                    if key not in _features:
                        _features[key] = features(f.read_bytes())
                    entry.update({k: v for k, v in _features[key].items() if k != "channels"})
                out.append(entry)
    return out


def catalog():
    return {
        "built": time.strftime("%H:%M:%S"),
        "vanillaDefs": vanilla_sound_defs(),
        "modDefs": mod_sound_defs(),
        "abilities": abilities(),
        "modClips": mod_clips(),
    }


class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *a, **kw):
        super().__init__(*a, directory=str(ROOT), **kw)

    def log_message(self, fmt, *args):
        pass

    def end_headers(self):
        self.send_header("Cache-Control", "no-store" if not self.path.endswith(".mp3") else "max-age=86400")
        super().end_headers()

    def _json(self, obj, code=200):
        body = json.dumps(obj).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        if self.path.split("?")[0] == "/soundlab/catalog.json":
            return self._json(catalog())
        if self.path.split("?")[0] == "/soundlab/picks.json":
            return self._json(json.loads(PICKS.read_text()) if PICKS.exists() else {})
        if self.path == "/":
            self.send_response(302)
            self.send_header("Location", "/Tools/SoundLab/web/")
            self.end_headers()
            return
        return super().do_GET()

    def do_POST(self):
        if self.path != "/soundlab/pick":
            return self._json({"error": "unknown"}, 404)
        body = json.loads(self.rfile.read(int(self.headers.get("Content-Length", 0))) or b"{}")
        key, pick = body.get("key"), body.get("pick")
        if not key or "/" not in key:
            return self._json({"error": "key must be <AbilityDef>/<moment>"}, 400)
        with _lock:
            picks = json.loads(PICKS.read_text()) if PICKS.exists() else {}
            if pick is None:
                picks.pop(key, None)
            else:
                pick["at"] = time.strftime("%Y-%m-%d %H:%M")
                picks[key] = pick
            PICKS.write_text(json.dumps(dict(sorted(picks.items())), indent=2) + "\n")
        return self._json({"ok": True, "picks": picks})


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
