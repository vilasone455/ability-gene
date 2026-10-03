// The sound lab's audio and clip catalog, shared by its own page (lab.js) and the VFX lab's sound
// markers (Tools/VfxLab/web/js/sounds.js). Both servers (Tools/SoundLab/serve.py and
// Tools/VfxLab/lab.py) answer /soundlab/ from Tools/SoundLab/soundlab.py.
//
// Plays clips the way RimWorld's SoundDefs do (Verse.Sound.SubSoundDef): volume is
// volumeRange / 100, pitch is Unity's AudioSource.pitch (speed and pitch together, the same as Web
// Audio's playbackRate), a folder grain plays one random clip from that folder and every folder
// under it, and startDelayRange delays the subSound.

export const CLIP_ROOT = '/Tools/SoundLab/clips/';
/** Sources every player has: the mod requires Core and Biotech and ships its own Sounds/. */
export const SAFE = new Set(['Core', 'Biotech', 'RimArt']);
/** picks.json key for a SoundDef picked as a whole (a VFX lab marker); moments are "<AbilityDef>/<moment>". */
export const soundKey = (name) => `sound:${name}`;

/** What loadSounds() read; one copy per page. */
export const lib = {
  clips: [],            // every clip: {source, folder, clip, url, dur, peakDb, attack, centroid, env}
  byFolder: new Map(),  // lower folder -> clips directly in it
  folders: [],          // [{key, folder, sources:Set, count}]
  vanillaDefs: [], modDefs: [], abilities: [],
  usedBy: new Map(),    // lower folder or clip path -> [{defName, source, volume, pitch}]
  candidates: {}, picks: {},
  index: null,          // clips/index.json from extract.py, or null when it has not been run
  built: null,          // when the server built the catalog
  error: null,          // why there is no catalog
};

const lower = (s) => (s || '').toLowerCase();

// ---------------------------------------------------------------- audio
export const ctx = new AudioContext();
export const master = ctx.createGain();
master.connect(ctx.destination);
const buffers = new Map();
const playing = new Set();
// stopAll() bumps it, so a sound still loading when it was called never starts.
let generation = 0;

// A browser starts no audio before the viewer has clicked or typed on the page. The VFX lab plays
// markers from its frame loop, so a marker before that is skipped rather than queued.
async function ready() {
  if (ctx.state === 'running') return true;
  if (navigator.userActivation && !navigator.userActivation.hasBeenActive) return false;
  await ctx.resume();
  return ctx.state === 'running';
}
for (const type of ['pointerdown', 'keydown']) addEventListener(type, () => { if (ctx.state === 'suspended') ctx.resume(); }, true);

function loadBuffer(url) {
  if (!buffers.has(url)) {
    buffers.set(url, fetch(url).then((r) => {
      if (!r.ok) throw new Error(`${r.status} ${url}`);
      return r.arrayBuffer();
    }).then((b) => ctx.decodeAudioData(b)));
  }
  return buffers.get(url);
}

/** Starts one clip; null if it was stopped while loading or audio is not allowed yet. */
export async function playUrl(url, { pitch = 1, volume = 50, delay = 0, loop = false } = {}) {
  const asked = generation;
  if (!(await ready())) return null;
  const buffer = await loadBuffer(url);
  if (asked !== generation) return null;
  const src = ctx.createBufferSource();
  src.buffer = buffer;
  src.playbackRate.value = pitch;
  src.loop = loop;
  const gain = ctx.createGain();
  gain.gain.value = volume / 100;
  src.connect(gain).connect(master);
  src.start(ctx.currentTime + delay);
  const voice = { src, gain, loop };
  playing.add(voice);
  src.onended = () => playing.delete(voice);
  return voice;
}

function stopVoice(v, fade) {
  const end = ctx.currentTime + fade;
  try {
    if (fade) { v.gain.gain.setValueAtTime(v.gain.gain.value, ctx.currentTime); v.gain.gain.linearRampToValueAtTime(0, end); }
    v.src.stop(end);
  } catch (e) { /* already stopped */ }
  playing.delete(v);
}

/** fade > 0 ramps down first, the way a sustainer's sustainFadeoutTime ends it in game. */
export function stopAll(fade = 0) {
  generation++;
  for (const v of [...playing]) stopVoice(v, fade);
}

/** Stops only looping layers: the VFX lab ends them when the effect loops back to its start. */
export function stopLoops(fade = 0) {
  for (const v of [...playing]) if (v.loop) stopVoice(v, fade);
}

/** Loads every clip a list of layers could choose, so a marker plays on time the first time. */
export function preload(layers) {
  for (const l of layers) for (const c of resolve(l)) loadBuffer(c.url).catch(() => {});
}

// ---------------------------------------------------------------- clips
export function clipsUnder(folder) {
  const f = lower(folder);
  const out = [];
  for (const [key, list] of lib.byFolder) {
    if (key === f || key.startsWith(f + '/')) out.push(...list);
  }
  return out;
}

function clipByPath(path) {
  const p = lower(path);
  const cut = p.lastIndexOf('/');
  const list = lib.byFolder.get(p.slice(0, cut)) || [];
  return list.find((c) => lower(c.clip) === p.slice(cut + 1)) || null;
}

/** A layer or grain is {folder} or {clip: "Folder/Name"}. Returns the clips the game would choose from. */
export function resolve(grain) {
  if (grain.clip) { const c = clipByPath(grain.clip); return c ? [c] : []; }
  if (grain.folder) return clipsUnder(grain.folder);
  return [];
}

const pickOne = (list) => list[Math.floor(Math.random() * list.length)];
const inRange = (r) => r[0] + Math.random() * (r[1] - r[0]);

/**
 * Plays mixer layers; returns the clip names chosen. duration: how long the effect lasts in game.
 * The code ends the sound then, so these voices are cut there too, with a 0.2 s fade.
 * keep: leave what is already playing (the VFX lab's markers overlap); otherwise it is stopped.
 */
export async function playLayers(layers, { duration = null, keep = false } = {}) {
  if (!keep) stopAll();
  const names = [];
  const voices = await Promise.all(layers.filter((l) => !l.mute).map((l) => {
    const c = pickOne(resolve(l));
    if (!c) return null;
    names.push(c.clip);
    return playUrl(c.url, { pitch: l.pitch, volume: l.volume, delay: l.delay, loop: l.loop });
  }));
  if (duration) setTimeout(() => voices.forEach((v) => v && playing.has(v) && stopVoice(v, 0.2)), duration * 1000);
  return names;
}

/** Plays a SoundDef the way the game does: per subSound one grain, one clip, volume and pitch inside the ranges. */
export function playDef(def, { keep = false } = {}) {
  if (!keep) stopAll();
  const names = [];
  for (const sub of def.subs) {
    const grains = sub.grains.filter((g) => g.folder || g.clip);
    if (!grains.length) continue;
    const c = pickOne(resolve(pickOne(grains)));
    if (!c) continue;
    names.push(c.clip);
    playUrl(c.url, { pitch: inRange(sub.pitch), volume: inRange(sub.volume), delay: inRange(sub.delay), loop: def.sustain && sub.loop });
  }
  return names;
}

// ---------------------------------------------------------------- SoundDefs and picks
/** The mod's SoundDef of that name, else the game's. */
export function defNamed(name) {
  return lib.modDefs.find((d) => d.defName === name) || lib.vanillaDefs.find((d) => d.defName === name) || null;
}

/**
 * What the lab plays for a SoundDef name: a pick saved for it, else the SoundDef itself, else
 * nothing. {kind: 'pick', layers, label} | {kind: 'def', def} | {kind: null}.
 */
export function soundFor(name) {
  const pick = lib.picks[soundKey(name)];
  if (pick?.layers?.length) return { kind: 'pick', layers: pick.layers, label: pick.option || 'your mix' };
  const def = defNamed(name);
  return def ? { kind: 'def', def } : { kind: null };
}

export function newLayer(grain, extra = {}) {
  return { folder: grain.folder || null, clip: grain.clip || null, pitch: 1, volume: 50, delay: 0, loop: false, mute: false, ...extra };
}

export const cloneLayers = (layers) => layers.map((l) => newLayer(l, { pitch: l.pitch ?? 1, volume: l.volume ?? 50, delay: l.delay ?? 0, loop: !!l.loop }));

/** A SoundDef as mixer layers: one per subSound, its first grain, the middle of each range. */
export function layersOfDef(def) {
  return def.subs.filter((s) => s.grains.some((g) => g.folder || g.clip)).map((s) => {
    const g = s.grains.find((x) => x.folder || x.clip);
    return newLayer(g, { pitch: (s.pitch[0] + s.pitch[1]) / 2, volume: (s.volume[0] + s.volume[1]) / 2, delay: s.delay[0], loop: def.sustain && s.loop });
  });
}

export function layerSummary(layers) {
  return layers.map((l) => `${l.folder || l.clip} ×${(+l.pitch).toFixed(2)} v${Math.round(l.volume)}${l.delay ? ` +${(+l.delay).toFixed(2)}s` : ''}${l.loop ? ' loop' : ''}`).join('  |  ');
}

/** The sources a layer's clips come from. */
export const sourcesOf = (layer) => [...new Set(resolve(layer).map((c) => c.source))];

/** Sources a layer needs MayRequire for: all its clips come from a DLC the mod does not require. */
export function dlcNeeds(layers) {
  const needs = new Set();
  for (const l of layers) {
    const sources = sourcesOf(l);
    if (sources.length && !sources.some((s) => SAFE.has(s))) sources.forEach((s) => needs.add(s));
  }
  return [...needs];
}

export const missingIn = (layers) => layers.filter((l) => resolve(l).length === 0).map((l) => l.folder || l.clip);

const xmlEsc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const fmtRange = (v) => { const s = (+v).toFixed(2).replace(/\.?0+$/, ''); return `${s}~${s}`; };

/** The SoundDef XML for 1.6/Defs/SoundDefs/. duration: a sustainer the ability's code ends after that long. */
export function defXml(name, layers, duration = null) {
  layers = layers.filter((l) => !l.mute);
  const sustain = layers.some((l) => l.loop) || !!duration;
  let xml = duration ? `<!-- A sustainer: the ability's code ends it after ${duration} s. -->\n` : '';
  xml += `<SoundDef>\n  <defName>${xmlEsc(name)}</defName>\n  <context>MapOnly</context>\n`;
  if (sustain) xml += `  <sustain>true</sustain>\n  <sustainFadeoutTime>0.2</sustainFadeoutTime>\n`;
  xml += `  <maxSimultaneous>4</maxSimultaneous>\n  <subSounds>\n`;
  for (const l of layers) {
    const needs = sourcesOf(l);
    const may = needs.length && needs.every((s) => !SAFE.has(s)) ? ` MayRequire="${needs.map((s) => 'Ludeon.RimWorld.' + s).join(',')}"` : '';
    const grain = l.folder
      ? `<li Class="AudioGrain_Folder"><clipFolderPath>${xmlEsc(l.folder)}</clipFolderPath></li>`
      : `<li Class="AudioGrain_Clip"><clipPath>${xmlEsc(l.clip)}</clipPath></li>`;
    xml += `    <li${may}>\n      <grains>${grain}</grains>\n      <volumeRange>${fmtRange(l.volume)}</volumeRange>\n      <pitchRange>${fmtRange(l.pitch)}</pitchRange>\n`;
    if (+l.delay) xml += `      <startDelayRange>${fmtRange(l.delay)}</startDelayRange>\n`;
    if (l.loop) xml += `      <sustainLoop>true</sustainLoop>\n`;
    xml += `    </li>\n`;
  }
  return xml + `  </subSounds>\n</SoundDef>`;
}

/** Writes one pick (null removes it) to picks.json through the server; lib.picks follows. */
export async function savePick(key, pick) {
  const r = await fetch('/soundlab/pick', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ key, pick }) });
  const body = await r.json();
  if (body.picks) lib.picks = body.picks;
  return body;
}

// ---------------------------------------------------------------- loading
export async function getJson(url, fallback) {
  try { const r = await fetch(url, { cache: 'no-store' }); return r.ok ? await r.json() : fallback; } catch (e) { return fallback; }
}

export async function loadSounds() {
  const [index, cat, cands, picks] = await Promise.all([
    getJson(CLIP_ROOT + 'index.json', null),
    getJson('/soundlab/catalog.json', null),
    getJson('/Tools/SoundLab/candidates.json', { abilities: {} }),
    getJson('/soundlab/picks.json', {}),
  ]);
  if (!cat || cat.error) {
    lib.error = cat?.error || 'no /soundlab/ on this server: run Tools/VfxLab/lab.py or Tools/SoundLab/serve.py';
    return lib;
  }
  lib.error = null;
  const clips = (index ? index.clips : []).map((c) => ({ ...c, url: CLIP_ROOT + c.file.split('/').map(encodeURIComponent).join('/') }));
  for (const m of cat.modClips) clips.push({ env: null, ...m });
  lib.clips = clips;
  lib.byFolder = new Map();
  const folders = new Map();
  for (const c of clips) {
    const k = lower(c.folder);
    if (!lib.byFolder.has(k)) lib.byFolder.set(k, []);
    lib.byFolder.get(k).push(c);
    if (!folders.has(k)) folders.set(k, { key: k, folder: c.folder, sources: new Set(), count: 0 });
    const f = folders.get(k);
    f.sources.add(c.source); f.count++;
  }
  lib.folders = [...folders.values()].sort((a, b) => a.key.localeCompare(b.key));
  lib.vanillaDefs = cat.vanillaDefs;
  lib.modDefs = cat.modDefs;
  lib.abilities = cat.abilities;
  lib.candidates = cands.abilities || {};
  lib.picks = picks || {};
  lib.index = index;
  lib.built = cat.built;
  lib.usedBy = new Map();
  for (const d of [...cat.vanillaDefs, ...cat.modDefs]) {
    for (const s of d.subs) for (const g of s.grains) {
      const k = lower(g.folder || g.clip);
      if (!k) continue;
      if (!lib.usedBy.has(k)) lib.usedBy.set(k, []);
      lib.usedBy.get(k).push({ defName: d.defName, source: d.source, volume: s.volume, pitch: s.pitch });
    }
  }
  return lib;
}

/** Reads candidates.json and picks.json again (Claude writes candidates; the other page writes picks). True if either changed. */
export async function pollChanges() {
  const [cands, picks] = await Promise.all([getJson('/Tools/SoundLab/candidates.json', null), getJson('/soundlab/picks.json', null)]);
  const changed = (cands && JSON.stringify(cands.abilities) !== JSON.stringify(lib.candidates)) || (picks && JSON.stringify(picks) !== JSON.stringify(lib.picks));
  if (!changed) return false;
  if (cands) lib.candidates = cands.abilities || {};
  if (picks) lib.picks = picks;
  return true;
}

/** "N clips (Core 2394, ...) · catalog hh:mm:ss", or why there is no sound. */
export function statusLine() {
  if (lib.error) return lib.error;
  const counts = lib.index ? Object.entries(lib.index.sources).map(([s, v]) => `${s} ${v.clips}`).join(', ') : 'no game clips: run Tools/SoundLab/extract.py';
  return `${lib.clips.length} clips (${counts}) · catalog ${lib.built}`;
}
