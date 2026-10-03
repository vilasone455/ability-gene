"""
The sound lab's server side, shared by Tools/SoundLab/serve.py and Tools/VfxLab/lab.py, so either
one serves the sound lab page and the VFX lab can play its sound markers.

- GET  /soundlab/catalog.json  built on each request (about 0.2 s): vanilla SoundDefs (Core and
  installed DLC), the mod's SoundDefs, its AbilityDefs, and its own sound files under Sounds/.
- GET  /soundlab/picks.json    Tools/SoundLab/picks.json.
- POST /soundlab/pick          {"key": ..., "pick": {...} or null} merged into picks.json, which is
  committed: it is the record of what was chosen. A key is "<AbilityDef>/<moment>" (a moment
  picked on the sound lab page) or "sound:<SoundDef>" (a sound marker picked in the VFX lab).
"""
import json, pathlib, re, sys, threading, time
import xml.etree.ElementTree as ET

LAB = pathlib.Path(__file__).resolve().parent
ROOT = LAB.parent.parent
PICKS = LAB / "picks.json"
sys.path.insert(0, str(ROOT))
import rimworld_paths  # noqa: E402
sys.path.insert(0, str(LAB))
from extract import features  # noqa: E402

AUDIO_EXT = {".ogg", ".wav", ".mp3"}
PICK_KEY = re.compile(r"\w+/\w+|sound:\w+")
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
    if not data.is_dir():
        return defs
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


def read_picks():
    return json.loads(PICKS.read_text()) if PICKS.exists() else {}


def save_pick(key, pick):
    """Sets or, with pick None, removes one key in picks.json; returns every pick."""
    with _lock:
        picks = read_picks()
        if pick is None:
            picks.pop(key, None)
        else:
            pick["at"] = time.strftime("%Y-%m-%d %H:%M")
            picks[key] = pick
        PICKS.write_text(json.dumps(dict(sorted(picks.items())), indent=2) + "\n")
    return picks


def _json(handler, obj, code=200):
    body = json.dumps(obj).encode()
    handler.send_response(code)
    handler.send_header("Content-Type", "application/json")
    handler.send_header("Content-Length", str(len(body)))
    handler.end_headers()
    handler.wfile.write(body)


def handle_get(handler):
    """Answers a GET under /soundlab/. False for any other path, which the server serves as a file."""
    path = handler.path.split("?")[0]
    if path == "/soundlab/catalog.json":
        try:
            _json(handler, catalog())
        except Exception as error:  # the page shows it instead of a silent empty catalog
            _json(handler, {"error": f"{type(error).__name__}: {error}"}, 500)
        return True
    if path == "/soundlab/picks.json":
        _json(handler, read_picks())
        return True
    return False


def handle_post(handler):
    """Answers POST /soundlab/pick. False for any other path."""
    if handler.path != "/soundlab/pick":
        return False
    body = json.loads(handler.rfile.read(int(handler.headers.get("Content-Length", 0))) or b"{}")
    key, pick = body.get("key"), body.get("pick")
    if not key or not PICK_KEY.fullmatch(key):
        _json(handler, {"error": "key must be <AbilityDef>/<moment> or sound:<SoundDef>"}, 400)
    else:
        _json(handler, {"ok": True, "picks": save_pick(key, pick)})
    return True
