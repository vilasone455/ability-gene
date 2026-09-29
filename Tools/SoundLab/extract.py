#!/usr/bin/env python3
"""
Sound lab: take the game's own sound clips out of its asset files so they can be heard in a
browser without starting RimWorld.

    python3 Tools/SoundLab/extract.py            Core and every installed DLC (~4,500 clips)
    python3 Tools/SoundLab/extract.py --force    extract again over what is there

Writes Tools/SoundLab/clips/ (gitignored). Nothing here is shipped: the mod plays these clips
through a SoundDef's clipFolderPath / clipPath, which the game resolves from its own files.

- clips/<Source>/<folder>/<ClipName>.mp3, one per clip. <folder> is the path a SoundDef names,
  lowercased, because the game's asset index stores it lowercased.
- clips/index.json: every clip with its source, the folder a SoundDef would name (original case
  where some vanilla SoundDef spells it out), length, loudness, attack, brightness and a 48-point
  envelope for the page's waveform strips.

Needs UnityPy and numpy (pip install --user UnityPy numpy) and ffmpeg. About 2 minutes on 10
cores; a second run skips clips that are already extracted.
"""
import argparse, io, json, multiprocessing, os, pathlib, re, subprocess, sys, time, wave

LAB = pathlib.Path(__file__).resolve().parent
ROOT = LAB.parent.parent
CLIPS = LAB / "clips"
sys.path.insert(0, str(ROOT))
import rimworld_paths  # noqa: E402

# Core's clips live in the player's resources.assets; each DLC ships one asset bundle.
# "requires" is the packageId a SoundDef using the clip needs in MayRequire, or None when the
# mod already depends on it (About.xml: Core and Biotech).
SOURCES = {
    "Core": {"requires": None},
    "Biotech": {"requires": None},
    "Royalty": {"requires": "Ludeon.RimWorld.Royalty"},
    "Ideology": {"requires": "Ludeon.RimWorld.Ideology"},
    "Anomaly": {"requires": "Ludeon.RimWorld.Anomaly"},
    "Odyssey": {"requires": "Ludeon.RimWorld.Odyssey"},
}
ENVELOPE_POINTS = 48


def core_data_dir():
    mac = os.path.join(rimworld_paths.GAME, "RimWorldMac.app/Contents/Resources/Data")
    return mac if os.path.isdir(mac) else os.path.join(rimworld_paths.GAME, "RimWorldWin64_Data")


def bundle_path(source):
    return os.path.join(rimworld_paths.DATA, source, "AssetBundles", "resources_" + source.lower())


def installed_sources():
    found = []
    for source in SOURCES:
        path = core_data_dir() if source == "Core" else bundle_path(source)
        if os.path.exists(path):
            found.append(source)
    return found


def list_clips(source):
    """(path_id, folder_lower, clip_name_lower) for every AudioClip the game can find by path."""
    import UnityPy
    out = []
    if source == "Core":
        env = UnityPy.load(core_data_dir())
        manager = next(o for o in env.objects if o.type.name == "ResourceManager")
        for path, ptr in manager.read_typetree()["m_Container"]:
            if path.startswith("sounds/"):
                out.append((ptr["m_PathID"], path[len("sounds/"):]))
        audio_ids = {o.path_id for o in env.objects if o.type.name == "AudioClip"}
        out = [(pid, p) for pid, p in out if pid in audio_ids]
    else:
        env = UnityPy.load(bundle_path(source))
        for path, obj in env.container.items():
            if obj.type.name != "AudioClip" or "/sounds/" not in path:
                continue
            rel = path.split("/sounds/", 1)[1]
            out.append((obj.path_id, re.sub(r"\.(wav|ogg|mp3|aif|aiff)$", "", rel)))
    # Songs are the soundtrack, not effects: Royalty alone has 69 MB of them.
    out = [(pid, rel) for pid, rel in out if not rel.startswith("songs/")]
    return [(pid, rel.rsplit("/", 1)[0] if "/" in rel else "", rel.rsplit("/", 1)[-1]) for pid, rel in out]


def features(wav_bytes):
    import numpy as np
    with wave.open(io.BytesIO(wav_bytes)) as w:
        rate, channels, width, frames = w.getframerate(), w.getnchannels(), w.getsampwidth(), w.getnframes()
        raw = w.readframes(frames)
    dtype = {1: np.uint8, 2: np.int16, 4: np.int32}[width]
    x = np.frombuffer(raw, dtype=dtype).astype(np.float32)
    if width == 1:
        x = x - 128
    x /= float(2 ** (8 * width - 1))
    if channels > 1:
        x = x.reshape(-1, channels).mean(axis=1)
    n = len(x)
    if n == 0:
        return {"dur": 0, "peakDb": -99, "rmsDb": -99, "attack": 0, "centroid": 0, "env": [0] * ENVELOPE_POINTS}
    a = np.abs(x)
    peak = float(a.max())
    rms = float(np.sqrt(np.mean(x * x)))
    # 10 ms windows: attack is the time to the loudest window.
    hop = max(1, rate // 100)
    windows = a[: n - n % hop].reshape(-1, hop).max(axis=1) if n >= hop else a
    attack = float(np.argmax(windows) * hop / rate)
    # Brightness: spectral centroid of the loudest 0.5 s.
    start = max(0, int(np.argmax(windows)) * hop - rate // 10)
    seg = x[start: start + rate // 2]
    spec = np.abs(np.fft.rfft(seg * np.hanning(len(seg)))) if len(seg) > 16 else np.zeros(2)
    freqs = np.fft.rfftfreq(len(seg), 1.0 / rate) if len(seg) > 16 else np.zeros(2)
    centroid = float((spec * freqs).sum() / spec.sum()) if spec.sum() > 0 else 0.0
    edges = np.linspace(0, n, ENVELOPE_POINTS + 1).astype(int)
    env = [float(a[edges[i]: max(edges[i] + 1, edges[i + 1])].max()) for i in range(ENVELOPE_POINTS)]
    top = max(env) or 1.0
    db = lambda v: round(20 * np.log10(max(v, 1e-5)), 1)
    return {
        "dur": round(n / rate, 3), "peakDb": db(peak), "rmsDb": db(rms), "attack": round(attack, 3),
        "centroid": int(centroid), "rate": rate, "channels": channels,
        "env": [int(round(99 * v / top)) for v in env],
    }


_env = None


def _worker_init(source):
    global _env
    import UnityPy
    _env = UnityPy.load(core_data_dir() if source == "Core" else bundle_path(source))
    _env.byid = {o.path_id: o for o in _env.objects if o.type.name == "AudioClip"}


def _extract_one(job):
    pid, out_path, force = job
    out = pathlib.Path(out_path)
    meta_path = out.with_suffix(".json")
    if not force and out.exists() and meta_path.exists():
        return json.loads(meta_path.read_text())
    clip = _env.byid[pid].read()
    samples = clip.samples
    if not samples:
        return None
    wav = next(iter(samples.values()))
    meta = features(wav)
    meta["name"] = clip.m_Name
    out.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        ["ffmpeg", "-v", "error", "-y", "-i", "pipe:0", "-codec:a", "libmp3lame", "-q:a", "4", str(out)],
        input=wav, check=True,
    )
    meta_path.write_text(json.dumps(meta))
    return meta


def original_case_paths():
    """Lowercased folder or clip path -> the spelling vanilla SoundDefs use."""
    spelled = {}
    for xml in pathlib.Path(rimworld_paths.DATA).glob("*/Defs/**/*.xml"):
        for m in re.finditer(r"<clip(?:Folder)?Path>([^<]+)</", xml.read_text(errors="ignore")):
            parts = m.group(1).strip().split("/")
            for i in range(1, len(parts) + 1):
                spelled.setdefault("/".join(parts[:i]).lower(), "/".join(parts[:i]))
    return spelled


def spell(lower, spelled):
    parts = lower.split("/")
    for i in range(len(parts), 0, -1):
        key = "/".join(parts[:i])
        if key in spelled:
            return "/".join([spelled[key]] + parts[i:])
    return lower


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--force", action="store_true", help="extract clips that already exist again")
    ap.add_argument("--only", help="one source, e.g. Core")
    args = ap.parse_args()

    spelled = original_case_paths()
    index = {"sources": {}, "clips": []}
    t0 = time.time()
    for source in installed_sources():
        if args.only and source != args.only:
            continue
        found = list_clips(source)
        jobs = []
        for pid, folder, name in found:
            out = CLIPS / source / folder / (name + ".mp3")
            jobs.append((pid, str(out), args.force))
        with multiprocessing.Pool(os.cpu_count(), initializer=_worker_init, initargs=(source,)) as pool:
            metas = pool.map(_extract_one, jobs, chunksize=8)
        kept = 0
        for (pid, folder, name), meta in zip(found, metas):
            if not meta:
                continue
            kept += 1
            meta.pop("channels", None)
            index["clips"].append({
                "source": source,
                "folder": spell(folder, spelled),
                "clip": spell(folder + "/" + name, spelled).rsplit("/", 1)[-1] if folder else meta["name"],
                "file": f"{source}/{folder}/{name}.mp3",
                **{k: v for k, v in meta.items() if k != "name"},
            })
        index["sources"][source] = {**SOURCES[source], "clips": kept}
        print(f"{source}: {kept} clips ({time.time() - t0:.0f} s)")
    if args.only and (CLIPS / "index.json").exists():
        old = json.loads((CLIPS / "index.json").read_text())
        index["clips"] = [c for c in old["clips"] if c["source"] != args.only] + index["clips"]
        index["sources"] = {**old["sources"], **index["sources"]}
    CLIPS.mkdir(parents=True, exist_ok=True)
    (CLIPS / "index.json").write_text(json.dumps(index, separators=(",", ":")))
    print(f"{len(index['clips'])} clips in {CLIPS / 'index.json'}")


if __name__ == "__main__":
    main()
