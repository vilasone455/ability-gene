#!/usr/bin/env python3
"""
Writes the mod's own synthesized sounds: the ones the game's clips cannot give (a Chidori's
chirping, a Rasengan's whir, a ki beam's roar, a flock of wings).

    python3 make_sounds.py              every sound
    python3 make_sounds.py Chidori Shun only these

Output: Sounds/AG/<Name>/<Name>_<a|b|c|d>.wav, mono 16-bit 32 kHz. A SoundDef plays a folder
with <clipFolderPath>AG/<Name></clipFolderPath>; the game picks one of the variants at random each
time, as it does for its own folders. Loops (for sustainers) are cross-faded end into start so
they repeat without a click.

Each variant is a different random seed of the same recipe. The recipes are fixed here as
constants; the lab (Tools/SoundLab) is where pitch, volume and layering are tuned.
Needs numpy.
"""
import pathlib, sys, wave, zlib
import numpy as np

ROOT = pathlib.Path(__file__).resolve().parent
OUT = ROOT / "Sounds" / "AG"
SR = 32000
TAU = 2 * np.pi


# ---------------------------------------------------------------------------------------- basics
def n_of(d):
    return int(round(d * SR))


def T(d):
    return np.arange(n_of(d)) / SR


def sine(freq, n=None, phase0=0.0):
    """freq: Hz, a number (needs n) or an array (one value per sample)."""
    f = np.full(n, float(freq)) if np.isscalar(freq) else np.asarray(freq, float)
    return np.sin(phase0 + TAU * np.cumsum(f) / SR)


def saw(freq, n=None, harmonics=30):
    """Band-limited saw by adding harmonics below Nyquist."""
    f = np.full(n, float(freq)) if np.isscalar(freq) else np.asarray(freq, float)
    phase = TAU * np.cumsum(f) / SR
    out = np.zeros(len(f))
    for k in range(1, harmonics + 1):
        mask = (k * f) < SR * 0.45
        out += mask * np.sin(k * phase) / k
    return out * 0.6


def white(n, rng):
    return rng.standard_normal(n)


def brown(n, rng):
    x = np.cumsum(rng.standard_normal(n))
    x = x - np.convolve(x, np.ones(801) / 801, mode="same")  # take out the drift
    return x / (np.abs(x).max() + 1e-9)


def smooth_random(n, rate, rng, lo=0.0, hi=1.0):
    """A random value that wanders at about `rate` changes per second."""
    points = max(4, int(n / SR * rate) + 3)
    y = rng.uniform(lo, hi, points)
    x = np.linspace(0, points - 1, n)
    i = np.floor(x).astype(int).clip(0, points - 2)
    fr = x - i
    fr = fr * fr * (3 - 2 * fr)
    return y[i] * (1 - fr) + y[i + 1] * fr


def ramp(points, n):
    """Piecewise-linear envelope through (time s, value) points."""
    ts, vs = zip(*points)
    return np.interp(np.arange(n) / SR, ts, vs)


def decay(n, attack, tau, start=0.0):
    t = np.arange(n) / SR - start
    a = np.clip(t / max(attack, 1e-4), 0, 1)
    d = np.exp(-np.clip(t - attack, 0, None) / tau)
    return np.where(t < 0, 0, a * d)


def place(out, x, t0):
    i = int(t0 * SR)
    if i >= len(out):
        return
    j = min(len(out), i + len(x))
    out[i:j] += x[: j - i]


# --------------------------------------------------------------------------------------- filters
def spectral(x, gain):
    """Time-varying filter by STFT. gain(freqs, times) -> array broadcast to (frames, bins)."""
    n, hop = 1024, 256
    win = np.hanning(n)
    pad = np.concatenate([np.zeros(n), x, np.zeros(n)])
    frames = 1 + (len(pad) - n) // hop
    idx = np.arange(n)[None, :] + hop * np.arange(frames)[:, None]
    spec = np.fft.rfft(pad[idx] * win, axis=1)
    freqs = np.fft.rfftfreq(n, 1 / SR)[None, :]
    times = ((hop * np.arange(frames) + n / 2 - n) / SR)[:, None]
    y = np.fft.irfft(spec * gain(freqs, times), n=n, axis=1) * win
    out = np.zeros(len(pad))
    norm = np.zeros(len(pad))
    for i in range(frames):
        out[i * hop: i * hop + n] += y[i]
        norm[i * hop: i * hop + n] += win * win
    return (out / np.maximum(norm, 1e-6))[n: n + len(x)]


def _fc(fc, times):
    return fc(times) if callable(fc) else fc


def lowpass(x, fc, order=2):
    return spectral(x, lambda f, t: 1 / np.sqrt(1 + (f / _fc(fc, t)) ** (2 * order)))


def highpass(x, fc, order=2):
    return spectral(x, lambda f, t: 1 / np.sqrt(1 + (_fc(fc, t) / np.maximum(f, 1)) ** (2 * order)))


def bandpass(x, fc, octaves=1.0):
    return spectral(x, lambda f, t: np.exp(-0.5 * (np.log2(np.maximum(f, 1) / _fc(fc, t)) / (octaves / 2)) ** 2))


def reverb(x, wet=0.2, length=0.8, rng=None, bright=4000):
    rng = rng or np.random.default_rng(7)
    n = n_of(length)
    # The recipe's own length cuts whatever is still ringing; fade it over 60 ms so the cut is
    # not a click, and let the reverb carry the tail.
    x = x.copy()
    m = min(len(x), n_of(0.06))
    x[-m:] *= np.cos(np.linspace(0, np.pi / 2, m)) ** 2
    ir = rng.standard_normal(n) * np.exp(-np.arange(n) / SR / (length / 5))
    ir = lowpass(ir, bright)
    ir /= np.sqrt(np.sum(ir * ir)) + 1e-9
    size = 1 << int(np.ceil(np.log2(len(x) + n)))
    y = np.fft.irfft(np.fft.rfft(x, size) * np.fft.rfft(ir, size), size)[: len(x) + n]
    dry = np.concatenate([x, np.zeros(n)])
    return dry * (1 - wet) + y * wet * (np.abs(x).max() / (np.abs(y).max() + 1e-9))


def drive(x, amount):
    return np.tanh(amount * x) / np.tanh(amount)


# ----------------------------------------------------------------------------- shared textures
def chirps(n, rng, per_second, f_lo, f_hi, start=0.0, end=None, density=None):
    """Short bird-like frequency sweeps: the 'thousand birds' of a Chidori."""
    out = np.zeros(n)
    end = end if end is not None else n / SR
    count = int((end - start) * per_second)
    for _ in range(count):
        t0 = rng.uniform(start, end)
        if density is not None and rng.random() > density(t0):
            continue
        length = rng.uniform(0.012, 0.045)
        m = n_of(length)
        f0 = rng.uniform(f_lo, f_hi)
        ratio = rng.uniform(1.2, 2.2) ** (1 if rng.random() < 0.7 else -1)
        f = f0 * ratio ** (np.arange(m) / m)
        e = np.minimum(1, np.arange(m) / (0.002 * SR)) * np.exp(-np.arange(m) / m * 3)
        place(out, sine(f) * e * rng.uniform(0.2, 1.0) ** 2, t0)
    return out


def crackle(n, rng, per_second, start=0.0, end=None, density=None):
    """Electric arcing: tiny noise bursts with heavy-tailed loudness."""
    out = np.zeros(n)
    end = end if end is not None else n / SR
    for _ in range(int((end - start) * per_second)):
        t0 = rng.uniform(start, end)
        if density is not None and rng.random() > density(t0):
            continue
        m = n_of(rng.uniform(0.001, 0.004))
        burst = rng.standard_normal(m) * np.exp(-np.arange(m) / (m / 3))
        place(out, burst * min(1.0, rng.pareto(2.5) * 0.3), t0)
    return out


def bell(n, f, rng, taus=(0.5, 0.25, 0.12, 0.07, 0.04), start=0.0, level=1.0):
    """A struck metal ping: inharmonic partials, each a slightly detuned pair so it shimmers."""
    out = np.zeros(n)
    ratios = (1.0, 2.76, 5.40, 8.93, 13.34)
    amps = (1.0, 0.6, 0.35, 0.2, 0.1)
    t = np.arange(n) / SR - start
    for r, a, tau in zip(ratios, amps, taus):
        if f * r > SR * 0.45:
            continue
        env = np.where(t < 0, 0, np.minimum(1, np.clip(t, 0, None) / 0.001) * np.exp(-np.clip(t, 0, None) / tau))
        ph = rng.uniform(0, TAU)
        out += a * env * 0.5 * (np.sin(TAU * f * r * t + ph) + np.sin(TAU * f * r * 1.003 * t + ph))
    return out * level


def whoosh(n, rng, f_from, f_to, start, length, level=1.0, octaves=1.0):
    """Air moving past: band-passed noise whose centre slides from f_from to f_to."""
    def fc(t):
        k = np.clip((t - start) / length, 0, 1)
        return f_from * (f_to / f_from) ** k
    body = bandpass(white(n, rng), fc, octaves)
    t = np.arange(n) / SR
    k = np.clip((t - start) / length, 0, 1)
    env = np.sin(np.pi * k) ** 1.5 * ((t >= start) & (t <= start + length))
    return body * env * level


# --------------------------------------------------------------------------------------- sounds
# Each recipe: (rng) -> mono float array. LOOP marks sustainer loops.
SOUNDS = {}


def sound(name, variants=3, loop=False, crossfade=0.15):
    def reg(fn):
        SOUNDS[name] = (fn, variants, loop, crossfade)
        return fn
    return reg


@sound("Chidori", variants=2, loop=True)
def chidori(rng, d=2.0):
    """Sasuke's Chidori: dense bright chirps over arcing crackle and a buzz."""
    n = n_of(d)
    birds = chirps(n, rng, 90, 2200, 5200)
    arcs = highpass(crackle(n, rng, 160), 1500)
    flutter = smooth_random(n, 30, rng, 0.3, 1.0)
    hiss = bandpass(white(n, rng), 5000, 1.2) * flutter * 0.25
    buzz = saw(120, n) * 0.12 * flutter
    return highpass(drive(birds * 0.9 + arcs * 0.6 + hiss + buzz, 1.5), 150)


@sound("ChidoriCrack")
def chidori_crack(rng, d=1.0):
    """The Chidori catching: one crack, then chirps that thin out."""
    n = n_of(d)
    fade = lambda t: np.exp(-t / 0.25)
    crack = highpass(white(n, rng) * decay(n, 0.001, 0.012), 800)
    birds = chirps(n, rng, 160, 2000, 5500, end=0.7, density=fade)
    arcs = highpass(crackle(n, rng, 300, end=0.7, density=fade), 1200)
    thump = sine(80 * (0.5 ** (T(d) / 0.15)), n) * decay(n, 0.002, 0.08)
    return reverb(drive(crack * 1.2 + birds + arcs * 0.7 + thump * 0.6, 1.3), 0.15, 0.6, rng)


def _rasengan(rng, n, spin, tone_hz, swirl_hz, level):
    t = np.arange(n) / SR
    spin_phase = TAU * np.cumsum(spin) / SR
    vib = 1 + 0.01 * np.sin(TAU * 5.5 * t)
    tone = lowpass(saw(tone_hz * vib, harmonics=25), 1500) * (0.6 + 0.4 * np.sin(spin_phase))
    swirl = bandpass(white(n, rng), lambda tt: np.interp(tt, t, swirl_hz) * 2 ** (0.8 * np.sin(TAU * 5 * tt)), 0.7)
    swirl *= 0.5 + 0.5 * np.sin(spin_phase + np.pi / 2)
    whistle = sine(2300 + 40 * np.sin(TAU * 6 * t)) * 0.08
    rumble = lowpass(brown(n, rng), 300) * 0.3
    return (tone * 0.6 + swirl * 0.9 + whistle + rumble) * level


@sound("Rasengan", variants=2, loop=True)
def rasengan(rng, d=1.6):
    """Minato's Rasengan held: a whirring spin at about 25 turns a second."""
    n = n_of(d)
    return drive(_rasengan(rng, n, np.full(n, rng.uniform(22, 28)), np.full(n, rng.uniform(150, 170)),
                           np.full(n, 1400.0), 1.0), 1.2)


@sound("RasenganForm")
def rasengan_form(rng, d=0.9):
    """The ball forming over 0.6 s: the spin winds up from nothing."""
    n = n_of(d)
    k = np.clip(T(d) / 0.6, 0, 1)
    body = _rasengan(rng, n, 8 + 17 * k, 90 + 70 * k, 700 + 700 * k, 1.0)
    return drive(body * ramp([(0, 0), (0.6, 1), (d - 0.05, 1), (d, 0)], n), 1.2)


@sound("RasenganHit")
def rasengan_hit(rng, d=0.8):
    """Driven into the target: a burst, the whir grinding down, a thump."""
    n = n_of(d)
    t = T(d)
    burst = lowpass(white(n, rng), 3000) * decay(n, 0.002, 0.15)
    grind = _rasengan(rng, n, 25 * 0.4 ** (t / 0.5), 160 * 0.4 ** (t / 0.5), np.full(n, 1400.0), 1.0) * decay(n, 0.005, 0.25)
    thump = sine(70 * 0.5 ** (t / 0.2), n) * decay(n, 0.002, 0.2)
    return reverb(drive(burst + grind * 0.8 + thump, 1.6), 0.15, 0.7, rng)


@sound("KiCharge", variants=2, loop=True, crossfade=0.3)
def ki_charge(rng, d=2.5):
    """Kamehameha held: a humming energy pad with a shimmer and a few sparks."""
    n = n_of(d)
    t = T(d)
    pad = lowpass(saw(110, n) + saw(110.8, n) + 0.6 * saw(165.3, n), 900) * (0.9 + 0.1 * np.sin(TAU * 0.7 * t))
    shimmer = sum(sine(f + smooth_random(n, 3, rng, -8, 8)) for f in (880, 1320, 1760)) * 0.1 * (0.85 + 0.15 * np.sin(TAU * 11 * t))
    air = bandpass(white(n, rng), 2500, 1.5) * 0.12 * smooth_random(n, 12, rng, 0.5, 1.0)
    sparks = highpass(crackle(n, rng, 25), 2000) * 0.3
    sub = sine(55, n) * 0.2
    return drive(pad * 0.4 + shimmer + air + sparks + sub, 1.2)


@sound("KiChargeStart", variants=2)
def ki_charge_start(rng, d=1.8):
    """The charge starting: a whine rising from 250 to 1,100 Hz over the building hum."""
    n = n_of(d)
    t = T(d)
    k = np.clip(t / 1.6, 0, 1)
    f = 250 * (1100 / 250) ** k * (1 + (0.005 + 0.02 * k) * np.sin(TAU * 6 * t))
    whine = (sine(f) + 0.3 * sine(2 * f)) * ramp([(0, 0), (0.2, 0.5), (1.6, 1), (d, 0.9)], n)
    hum = ki_charge(rng, d) * ramp([(0, 0), (1.2, 1), (d, 1)], n)
    swell = bandpass(white(n, rng), lambda tt: 800 + 2500 * np.clip(tt / 1.6, 0, 1), 1.5) * ramp([(0, 0), (1.6, 0.4), (d, 0.3)], n)
    return drive(whine * 0.35 + hum * 0.8 + swell, 1.2) * ramp([(0, 1), (d - 0.03, 1), (d, 0)], n)


@sound("KiBeam", variants=2, loop=True)
def ki_beam(rng, d=1.5):
    """The beam: a roaring wall of noise over a low buzz."""
    n = n_of(d)
    t = T(d)
    roar = lowpass(white(n, rng), 1800) * smooth_random(n, 20, rng, 0.7, 1.0)
    buzz = lowpass(saw(65 * (1 + 0.01 * np.sin(TAU * 5 * t)), harmonics=40), 2500) * 0.35
    hiss = highpass(white(n, rng), 5000) * 0.15
    sub = sine(45, n) * 0.3
    return drive(roar * 0.7 + buzz + hiss + sub, 2.0)


@sound("KiBeamFire")
def ki_beam_fire(rng, d=0.7):
    """Fire: the whoomp as the beam leaves."""
    n = n_of(d)
    whoomp = sine(180 * (45 / 180) ** np.clip(T(d) / 0.3, 0, 1), n) * decay(n, 0.003, 0.2)
    burst = lowpass(white(n, rng), 2000) * decay(n, 0.005, 0.2)
    return reverb(drive(whoomp + burst * 0.8, 1.5), 0.15, 0.7, rng)


@sound("Shun", variants=4)
def shun(rng, d=0.35):
    """A body vanishing at speed ('shun'): a fast upward sweep with a puff of air."""
    n = n_of(d)
    sweep = rng.uniform(0.06, 0.09)
    f0, f1 = rng.uniform(500, 800), rng.uniform(2000, 3200)
    t = T(d)
    f = f0 * (f1 / f0) ** np.clip(t / sweep, 0, 1)
    tone = (sine(f) + 0.3 * sine(2 * f)) * decay(n, 0.005, 0.04, 0) * np.where(t < sweep, 1, np.exp(-(t - sweep) / 0.03))
    air = bandpass(white(n, rng), lambda tt: f0 * (f1 / f0) ** np.clip(tt / sweep, 0, 1), 1.0) * decay(n, 0.01, 0.06)
    return reverb(tone * 0.7 + air * 0.6, 0.2, 0.3, rng)


@sound("Ting")
def ting(rng, d=1.2):
    """A light metallic ping (Amenotejikara's swap)."""
    n = n_of(d)
    click = highpass(white(n, rng) * decay(n, 0.0005, 0.002), 3000) * 0.5
    return reverb(bell(n, rng.uniform(1900, 2400), rng) + click, 0.25, 1.0, rng)


@sound("CrowFlaps")
def crow_flaps(rng, d=1.4):
    """A flock taking off: 10 to 16 birds flapping at 8 to 11 beats a second, flying away."""
    n = n_of(d)
    out = np.zeros(n)
    for _ in range(rng.integers(10, 17)):
        flaps = np.zeros(n)
        t0 = rng.uniform(0, 0.35)
        rate = rng.uniform(8, 11)
        a = rng.uniform(0.4, 1.0)
        t = t0
        for _ in range(rng.integers(6, 13)):
            m = n_of(rng.uniform(0.045, 0.07))
            k = np.arange(m) / m
            env = np.minimum(1, k / 0.25) * (1 - k) ** 2
            place(flaps, rng.standard_normal(m) * env * a, t)
            t += 1 / rate * rng.uniform(0.9, 1.1)
            a *= 0.9
        out += bandpass(flaps, rng.uniform(500, 1000), 1.2)
    low = lowpass(white(n, rng), 300) * ramp([(0, 0), (0.2, 0.3), (d, 0)], n)
    return reverb(out + low, 0.15, 0.6, rng)


@sound("ShadowRun")
def shadow_run(rng, d=0.9):
    """A shadow running over the ground: a dark slide that opens and closes."""
    n = n_of(d)
    slide = lowpass(brown(n, rng), lambda t: np.interp(t, [0, 0.45, d], [250, 900, 400]), 3)
    drops = lowpass(crackle(n, rng, 40), 1500) * 0.3
    sub = sine(50, n) * 0.15
    return (slide + drops + sub) * ramp([(0, 0), (0.1, 1), (d - 0.25, 1), (d, 0)], n)


@sound("ShadowHold", variants=2, loop=True, crossfade=0.4)
def shadow_hold(rng, d=2.5):
    """A shadow holding: a low drone with a faint whisper on top."""
    n = n_of(d)
    t = T(d)
    body = lowpass(brown(n, rng), lambda tt: 450 + 150 * np.sin(TAU * 0.4 * tt), 3)
    drone = sine(48, n) * (0.8 + 0.2 * np.sin(TAU * 0.3 * t)) * 0.3
    whisper = bandpass(white(n, rng), 3500, 1.0) * 0.03 * smooth_random(n, 4, rng, 0.2, 1.0)
    return body + drone + whisper


@sound("SolarFlare", variants=2)
def solar_flare(rng, d=1.3):
    """Goku's Solar Flare: a bright hiss burst with a high shimmer."""
    n = n_of(d)
    t = T(d)
    pshh = highpass(white(n, rng), 2000) * decay(n, 0.003, 0.35) * 0.8
    shimmer = sum(sine(rng.uniform(3000, 7000), n) * decay(n, 0.005, 0.6) * (0.7 + 0.3 * np.sin(TAU * rng.uniform(18, 30) * t))
                  for _ in range(6)) * 0.12
    ping = sine(4200, n) * decay(n, 0.002, 0.25) * 0.3
    whoomp = sine(120 * (50 / 120) ** np.clip(t / 0.2, 0, 1), n) * decay(n, 0.003, 0.15) * 0.5
    return reverb(pshh + shimmer + ping + whoomp, 0.3, 1.2, rng, bright=8000)


@sound("SpiritGather", variants=1, loop=True, crossfade=0.5)
def spirit_gather(rng, d=4.0):
    """The Spirit Bomb gathering: an airy major chord with breath, wind and sparkles."""
    n = n_of(d)
    t = T(d)
    pad = np.zeros(n)
    for f in (220, 277.2, 329.6, 440, 554.4):
        lfo = 0.6 + 0.4 * np.sin(TAU * rng.uniform(0.15, 0.35) * t + rng.uniform(0, TAU))
        pad += (sine(f - 0.6, n) + sine(f + 0.6, n) + 0.2 * sine(2 * f, n)) * lfo
    breath = bandpass(white(n, rng), 1800, 1.5) * 0.1 * smooth_random(n, 2, rng, 0.4, 1.0)
    wind = lowpass(white(n, rng), 600) * 0.25 * (0.6 + 0.4 * np.sin(TAU * 0.25 * t))
    sparkles = np.zeros(n)
    for _ in range(int(d * 3)):
        sparkles += bell(n, rng.uniform(3000, 5000), rng, taus=(0.2, 0.1, 0.05, 0.03, 0.02), start=rng.uniform(0, d - 0.3), level=0.05)
    # A loop keeps its own length: the reverb tail is dropped, the cross-fade joins the ends.
    return reverb(pad * 0.12 + breath + wind + sparkles, 0.35, 2.0, rng)[:n]


def _slice(rng, n, start, level=1.0):
    t = np.arange(n) / SR - start
    on = t >= 0
    tt = np.clip(t, 0, None)
    snap = highpass(white(n, rng), 4000) * on * np.exp(-tt / 0.004)
    f = 6000 * (1800 / 6000) ** np.clip(tt / 0.12, 0, 1)
    zing = (sine(f) + 0.4 * sine(1.5 * f)) * on * np.exp(-tt / 0.12)
    glass = sum(sine(rng.uniform(2500, 9000), n) * on * np.exp(-tt / rng.uniform(0.05, 0.2)) for _ in range(5)) * 0.15
    tear = whoosh(n, rng, 8000, 1500, start, 0.2, 0.4)
    thump = sine(90 * (40 / 90) ** np.clip(tt / 0.12, 0, 1), n) * on * np.exp(-tt / 0.1) * 0.4
    return (snap + zing * 0.5 + glass + tear + thump) * level


@sound("SpaceSlice", variants=4)
def space_slice(rng, d=0.7):
    """Vergil's Judgement Cut: space splitting, one slice."""
    n = n_of(d)
    return reverb(_slice(rng, n, 0.0), 0.25, 0.8, rng, bright=9000)


@sound("SpaceSliceCluster")
def space_slice_cluster(rng, d=1.1):
    """Five slices over half a second, for the sphere of cuts."""
    n = n_of(d)
    out = np.zeros(n)
    for i in range(5):
        out += _slice(rng, n, 0.1 * i + rng.uniform(0, 0.04), rng.uniform(0.6, 1.0))
    return reverb(out, 0.25, 0.8, rng, bright=9000)


@sound("BlackFlash")
def black_flash(rng, d=1.0):
    """Todo's Black Flash: a heavy impact with a burst of dark sparks."""
    n = n_of(d)
    t = T(d)
    hit = lowpass(white(n, rng), 2500) * decay(n, 0.001, 0.06)
    sub = sine(70 * (32 / 70) ** np.clip(t / 0.3, 0, 1), n) * decay(n, 0.002, 0.35) * 0.9
    fade = lambda tt: np.exp(-tt / 0.2)
    sparks = bandpass(crackle(n, rng, 400, end=0.8, density=fade), 1200, 2.0)
    birds = chirps(n, rng, 60, 900, 1800, end=0.5, density=fade) * 0.5
    return reverb(drive(hit + sub + sparks * 0.8 + birds, 2.5), 0.15, 0.5, rng)


@sound("KamuiIn")
def kamui_in(rng, d=1.1):
    """Obito's Kamui taking something in: a whoosh sucked down into a point, then gone."""
    n = n_of(d)
    t = T(d)
    end = d - 0.2
    k = np.clip(t / end, 0, 1)
    fc = lambda tt: 3500 * (250 / 3500) ** np.clip(tt / end, 0, 1)
    body = bandpass(white(n, rng), fc, 0.8)
    swirl = bandpass(white(n, rng), lambda tt: fc(tt) * 1.5, 0.6) * (0.5 + 0.5 * np.sin(TAU * np.cumsum(7 + 7 * k) / SR))
    tone = sine(180 * (60 / 180) ** k, n) * 0.3
    gate = np.where(t < end, k ** 2, np.exp(-(t - end) / 0.006))
    thup = sine(90 * (40 / 90) ** np.clip((t - end) / 0.05, 0, 1), n) * decay(n, 0.001, 0.05, end) * 0.6
    return reverb((body + swirl * 0.7 + tone) * gate + thup, 0.2, 0.5, rng)


@sound("KamuiOut")
def kamui_out(rng, d=0.9):
    """Coming out of the swirl: a pop, then the whoosh opening up and away."""
    n = n_of(d)
    t = T(d)
    k = np.clip(t / d, 0, 1)
    fc = lambda tt: 250 * (3000 / 250) ** np.clip(tt / d, 0, 1)
    body = bandpass(white(n, rng), fc, 0.8)
    pop = sine(90 * (40 / 90) ** np.clip(t / 0.05, 0, 1), n) * decay(n, 0.001, 0.05) * 0.6
    return reverb(body * (1 - k) ** 1.5 * np.minimum(1, t / 0.02) + pop, 0.2, 0.5, rng)


@sound("SusanooHum", variants=1, loop=True, crossfade=0.5)
def susanoo_hum(rng, d=3.0):
    """Susanoo standing: a low ominous pad over a fire rumble, with an 'ahh' formant."""
    n = n_of(d)
    t = T(d)
    pad = lowpass(saw(55, n, 40) + saw(55.4, n, 40) + 0.7 * saw(82.4, n, 40), 600) * (0.8 + 0.2 * np.sin(TAU * 0.3 * t))
    fire = lowpass(brown(n, rng), 400) * 0.3 * smooth_random(n, 8, rng, 0.5, 1.0)
    choir = (bandpass(white(n, rng), 500, 0.3) + bandpass(white(n, rng), 1100, 0.3)) * 0.1
    return reverb(pad * 0.35 + fire + choir, 0.3, 1.5, rng)[:n]


def _flame(rng, n):
    body = lowpass(brown(n, rng), 900) * smooth_random(n, 8, rng, 0.5, 1.0)
    pops = bandpass(crackle(n, rng, 30), 2000, 1.5) * 0.6
    roar = lowpass(white(n, rng), 150) * 0.4
    return body + pops + roar


@sound("BlackFlame", variants=2, loop=True, crossfade=0.3)
def black_flame(rng, d=2.0):
    """Amaterasu burning: a low, dark fire with sparse pops."""
    return _flame(rng, n_of(d))


@sound("BlackFlameIgnite")
def black_flame_ignite(rng, d=0.9):
    """Amaterasu catching: a whoomp that opens into fire."""
    n = n_of(d)
    whoomp = lowpass(white(n, rng), lambda t: np.interp(t, [0, 0.03, 0.2, d], [300, 2500, 800, 600]), 2)
    whoomp *= decay(n, 0.03, 0.4)
    sub = sine(60, n) * decay(n, 0.01, 0.25) * 0.5
    fire = _flame(rng, n) * ramp([(0, 0), (0.2, 0.7), (d - 0.1, 0.5), (d, 0)], n)
    return drive(whoomp + sub + fire, 1.3)


@sound("SwordSummon", variants=2)
def sword_summon(rng, d=0.9):
    """Vergil's summoned swords appearing: a rising glassy arpeggio over a whoosh."""
    n = n_of(d)
    out = sum(bell(n, f * rng.uniform(0.99, 1.01), rng, taus=(0.35, 0.15, 0.08, 0.05, 0.03), start=0.04 * i, level=0.5)
              for i, f in enumerate((1568, 1976, 2349, 2637, 3136)))
    return reverb(out + whoosh(n, rng, 800, 4000, 0.0, 0.4, 0.4), 0.35, 1.0, rng, bright=9000)


@sound("SwordFly", variants=4)
def sword_fly(rng, d=0.4):
    """One summoned sword flying past: a whoosh with a falling whistle."""
    n = n_of(d)
    t = T(d)
    air = whoosh(n, rng, 1200, 2800, 0.0, 0.25, 1.0) + whoosh(n, rng, 2800, 1500, 0.12, 0.2, 0.6)
    whistle = sine(3000 * (2200 / 3000) ** np.clip(t / 0.3, 0, 1), n) * np.sin(np.pi * np.clip(t / 0.3, 0, 1)) * 0.2
    return air + whistle


def _biwa_string(n, f, rng, start=0.0, t60=2.2, bright=0.6, buzz=0.5, level=1.0):
    """One biwa string plucked (Karplus-Strong: a noise burst one period long, averaged on every
    pass so the highs die first). t60: seconds to fall 60 dB. buzz: the sawari, the flat bridge the
    string slaps on its way down; the slap is an asymmetric clip, so the buzz is loud while the
    string swings wide and goes quiet as it settles, as on the instrument."""
    out = np.zeros(n)
    i0 = int(start * SR)
    m = n - i0
    if m <= 0:
        return out
    # The loop filter is 3 taps (1/4, 1/2, 1/4), so the period is N + 1 samples and the highs die
    # faster than with the usual 2-tap average: a silk string, not a steel one.
    N = max(2, int(round(SR / f)) - 1)
    damp = 0.001 ** (1 / (t60 * f))
    burst = rng.uniform(-1, 1, N)
    burst -= burst.mean()
    soft = np.convolve(burst, np.ones(9) / 9, mode="same")
    blocks = m // N + 2
    y = np.zeros(blocks * N + 2)
    y[2: N + 2] = bright * burst + (1 - bright) * soft
    for k in range(1, blocks):
        seg = y[(k - 1) * N: k * N + 2]
        y[k * N + 2: (k + 1) * N + 2] = damp * (0.25 * seg[:-2] + 0.5 * seg[1:-1] + 0.25 * seg[2:])
    y = y[2: m + 2]
    th = 0.35 * np.abs(y).max()
    slapped = np.where(y < -th, -th + (y + th) * 0.15, y)
    y = lowpass(y, 1800 + 3000 * bright, 1) + buzz * 3 * bandpass(slapped - y, 2200, 1.5)
    out[i0:] = y * level
    return out


def _bachi(n, rng, start=0.0, level=1.0):
    """The bachi, a wide wooden plectrum, striking the body as it crosses the strings: a short knock."""
    t = np.arange(n) / SR - start
    env = np.where(t < 0, 0, np.exp(-np.clip(t, 0, None) / 0.03))
    knock = sine(np.interp(t, [0, 0.04], [190, 120]), n) * env
    click = lowpass(white(n, rng), 3000) * np.where(t < 0, 0, np.exp(-np.clip(t, 0, None) / 0.012))
    return (knock * 0.8 + click * 0.5) * level


@sound("BiwaStrum")
def biwa_strum(rng, d=2.8):
    """Nakime's biwa: one hard bachi stroke across four strings in about 30 ms, low string first,
    with the body knock and the sawari buzz, in a large wooden hall. The game has no plucked string;
    its Royalty harp is a 14 s song."""
    n = n_of(d)
    root = rng.choice([110.0, 116.5, 123.5])
    sweep = rng.uniform(0.022, 0.04)
    out = _bachi(n, rng, 0.0, 0.9)
    for i, ratio in enumerate((1.0, 1.5, 2.0, 2.245)):
        out += _biwa_string(n, root * ratio * rng.uniform(0.997, 1.003), rng, start=0.004 + sweep * i / 3,
                            t60=rng.uniform(1.8, 2.6) / (1 + 0.25 * i), bright=0.75, buzz=0.6, level=1.0 - 0.15 * i)
    return highpass(reverb(drive(out, 1.2), 0.3, 1.8, rng, bright=5000), 50)


@sound("BiwaNote")
def biwa_note(rng, d=2.4):
    """One biwa string plucked softly, no body knock: the bound biwa coming back to her hands."""
    n = n_of(d)
    f = rng.choice([220.0, 246.9, 329.6])
    out = _biwa_string(n, f, rng, start=0.003, t60=2.0, bright=0.35, buzz=0.3)
    return highpass(reverb(out, 0.3, 1.6, rng, bright=4500), 50)


# ------------------------------------------------------------------------------------------ write
def make_loop(x, crossfade):
    """Cross-fade the tail over the head so the clip repeats without a seam."""
    m = n_of(crossfade)
    body = x[:-m].copy()
    k = np.linspace(0, 1, m)
    body[:m] = body[:m] * np.sin(k * np.pi / 2) + x[-m:] * np.cos(k * np.pi / 2)
    return body


def finish(x, loop, crossfade):
    x = x - np.mean(x)
    if loop:
        x = make_loop(x, crossfade)
    else:
        m = n_of(0.004)
        x[:m] *= np.linspace(0, 1, m)
        tail = n_of(0.02)
        x[-tail:] *= np.linspace(1, 0, tail)
        # Trim silence the reverb left at the end.
        loud = np.nonzero(np.abs(x) > 0.001 * np.abs(x).max())[0]
        if len(loud):
            x = x[: min(len(x), loud[-1] + n_of(0.05))]
    return x / (np.abs(x).max() + 1e-9) * 0.89


def write_wav(path, x):
    path.parent.mkdir(parents=True, exist_ok=True)
    data = (np.clip(x, -1, 1) * 32767).astype("<i2").tobytes()
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data)


def main(names):
    todo = names or list(SOUNDS)
    unknown = [n for n in todo if n not in SOUNDS]
    if unknown:
        sys.exit("unknown: " + ", ".join(unknown) + "\nknown: " + ", ".join(SOUNDS))
    total = 0
    for name in todo:
        fn, variants, loop, crossfade = SOUNDS[name]
        folder = OUT / name
        for old in folder.glob("*.wav"):
            old.unlink()
        for v in range(variants):
            rng = np.random.default_rng(zlib.crc32(name.encode()) * 10 + v)
            x = finish(fn(rng), loop, crossfade)
            write_wav(folder / f"{name}_{'abcd'[v]}.wav", x)
            total += len(x)
        print(f"{name:18} {variants} x {'loop' if loop else 'one-shot'}")
    print(f"{len(todo)} sounds, {total / SR:.1f} s of audio in {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main(sys.argv[1:])
