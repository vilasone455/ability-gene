# RimArt Sound Lab

A browser page for choosing ability sounds without starting RimWorld.

```bash
python3 Tools/SoundLab/extract.py   # once, ~30 s: the game's clips into Tools/SoundLab/clips/
python3 Tools/SoundLab/serve.py     # open http://localhost:8766/
```

Needs Python 3, `pip install --user UnityPy numpy` and ffmpeg. Chrome, Edge or Firefox.

`python3 Tools/VfxLab/lab.py` serves this page too, at `/Tools/SoundLab/web/` on its port, and
plays the same sounds on an effect's timeline: its Sound tab picks a sound for each sound marker
while the effect plays (`Tools/VfxLab/README.md`, "Sound"). `serve.py` is the sound lab alone,
without the VFX recorder.

## Where the sounds come from

The mod ships no audio of the game's. A SoundDef names a clip by path (`clipFolderPath` or
`clipPath`) and the game plays it from its own files, as `AG_Anchor_Sounds.xml` does. The lab
extracts the same clips only so they can be heard; `Tools/SoundLab/clips/` is gitignored.

| Source | Clips | In a SoundDef |
|---|---|---|
| Core | 2,394 | always there |
| Biotech | 1,014 | always there: About.xml requires Biotech |
| Royalty | 135 | only for players who own it: `<li MayRequire="Ludeon.RimWorld.Royalty">` |
| Odyssey | 877 | only for players who own it: `MayRequire="Ludeon.RimWorld.Odyssey"` |

Soundtrack songs are left out. Ideology and Anomaly are extracted too when they are installed.

The mod's own files under `Sounds/` are listed as source RimArt. `make_sounds.py` (repository
root) writes the synthesized ones into `Sounds/AG/<Name>/`: 28 sounds, 74 WAV files, 7 MB, for
what the game's clips cannot give (Chidori chirping, Rasengan whir, ki charge and beam, a flock's
wings, space slices, Kamui swirls, black flame, Nakime's biwa). These ship with the mod, and `deploy.sh` copies
`Sounds/`. A SoundDef names one with `<clipFolderPath>AG/Chidori</clipFolderPath>`.

## The page

- **Abilities**: abilities from `candidates.json`, grouped by hero. Each is split into moments
  (cast, beam, hit, loop), each with about three options. A moment's optional `"defName"` is the
  SoundDef it becomes: the mixer's defName box starts with it, and the VFX lab's Sound tab lists
  that moment's options first for a marker of that name (`"defName": "RimArt_BubblePop"`). ▶ plays one, Mixer loads it for tuning,
  Pick saves it. A moment with "lasts N s in game" is cut there with a 0.2 s fade, because the
  ability's code ends that sound (a sustainer) when the effect ends.
- **Clips**: every clip folder, with each clip's waveform, length, peak and brightness
  (dark < 700 Hz spectral centroid < mid < 2,200 Hz < bright), and the vanilla SoundDefs that use it.
- **SoundDefs**: every vanilla SoundDef, played as the game plays it: random clip from the folder,
  random volume and pitch inside the def's ranges.
- **Mixer**: up to any number of layers; each layer is one `subSound` (pitch, volume, delay,
  loop). The SoundDef XML under it is what gets written into `1.6/Defs/SoundDefs/`. RimWorld
  takes a layer's `startDelayRange` only on a sustainer and logs a config error on a one-shot, so a
  one-shot's delays are written as `RimArt.SoundLayerDelays` instead, and the code plays that sound
  with `SoundLayers.Play` (`Source/RimArt/Shared/SoundLayers.cs`), which starts each layer on its tick.

Playback follows `Verse.Sound.SubSoundDef`: volume is `volumeRange / 100`, pitch is Unity's
`AudioSource.pitch` (speed and pitch together, Web Audio's `playbackRate`), and a folder grain
plays one random clip from the folder and every folder under it. In game the loudness also
depends on the camera distance, so the lab is right about the mix between layers and between
options, not about absolute level.

## Files

| File | Written by | Committed |
|---|---|---|
| `candidates.json` | `make_candidates.py`, per ability moment | yes |
| `picks.json` | Pick on this page (`"<AbilityDef>/<moment>"`) or on the VFX lab's Sound tab (`"sound:<SoundDef>"`) | yes: SoundDefs are written from it |
| `clips/` | `extract.py` | no |
| `soundlab.py` | the `/soundlab/` endpoints (catalog, picks), used by `serve.py` and `Tools/VfxLab/lab.py` | yes |
| `web/sound.js` | audio, the clip catalog and SoundDef XML, used by this page and the VFX lab | yes |

Both pages reload `candidates.json` and `picks.json` every 3 seconds, so new candidates and the
other page's picks show up without a reload.
