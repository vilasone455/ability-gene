// Sound markers. A sketch's events() and a recording's PlayOneShot calls carry
// { t, type: 'sound', def: '<SoundDef>' }; this plays each one as the clock passes it, and the Sound
// tab picks what each SoundDef sounds like. The audio, the clip catalog and picks.json are the sound
// lab's (Tools/SoundLab/web/sound.js), which lab.py serves under /soundlab/.
//
// A marker plays, in order: the mix open in the Sound tab for that name while it differs from what
// is saved, the pick saved for it (picks.json "sound:<SoundDef>"), the mod's or the game's
// SoundDef of that name, or nothing.

import {
  lib, master, loadSounds, pollChanges, statusLine, stopAll, stopLoops, preload, playLayers, playDef,
  soundFor, soundKey, newLayer, cloneLayers, layersOfDef, sourcesOf, dlcNeeds, missingIn, defXml, savePick,
} from '/Tools/SoundLab/web/sound.js';

let ui = null;   // { el, store, clock, source: () => the effect on the left (A), panel }
const S = {
  on: true,
  volume: 1,
  playing: false,  // the clock's state last frame, to tell a pause from a stop at the end
  name: null,      // the SoundDef open in the panel
  mix: [],         // its layers in the mixer
  base: '[]',      // the layers as opened or last saved; a mix that differs plays instead of them
  option: null,    // the candidate option the mix came from, while it is unchanged
  optionBase: null,
  note: '',
  search: '',
};

// Every marker's mix while options are tried, by SoundDef name: { mix, base, option, optionBase, note }.
// Opening another marker keeps the last one's here, so every marker plays what was chosen for it,
// saved or not, and Pick all saves them together. S holds the open marker's copy.
const drafts = new Map();

// An option is heard from this long before its marker's first time; the effect then plays on and loops whole.
const Before = 0.6;

const strip = (layers) => layers.map(({ mute, ...l }) => l);
const unsaved = (d) => JSON.stringify(strip(d.mix)) !== d.base;
const dirty = () => S.name && unsaved(S);

/** The mix a marker plays instead of its saved sound: the open one's, or one tried earlier; null when it matches what is saved. */
function unsavedMix(name) {
  if (name === S.name) return dirty() ? S.mix : null;
  const d = drafts.get(name);
  return d && unsaved(d) ? d.mix : null;
}

/** The open marker's mix, kept for when it is opened again. */
function stash() {
  if (S.name) drafts.set(S.name, { mix: S.mix, base: S.base, option: S.option, optionBase: S.optionBase, note: S.note });
}
const fmt = (t) => t.toFixed(2);

/** Sound events of an effect, in time order. */
const markers = (source) => (source?.events ?? []).filter((e) => e.type === 'sound' && e.def).sort((a, b) => a.t - b.t);

/** One row per SoundDef name: [{ name, times }], by first time. */
function markerNames(source) {
  const byName = new Map();
  for (const e of markers(source)) {
    if (!byName.has(e.def)) byName.set(e.def, []);
    byName.get(e.def).push(e.t);
  }
  return [...byName].map(([name, times]) => ({ name, times }));
}

/** context: { el, store, clock, source, panel }. Loads the catalog in the background. */
export function initSound(context) {
  ui = context;
  S.on = ui.store.get('sound', true);
  S.volume = ui.store.get('soundVolume', 1);
  master.gain.value = S.volume;
  // A hidden page is silent. Some hosts (the Claude desktop app's browser pane) keep drawing a
  // hidden page, so a looping effect would otherwise go on playing its markers out of sight.
  document.addEventListener('visibilitychange', () => { if (document.hidden) stopAll(0.08); });
  return loadSounds().then(() => {
    preloadMarkers();
    renderSoundPanel();
    setInterval(async () => { if (await pollChanges()) { preloadMarkers(); renderSoundPanel(); } }, 3000);
  });
}

function play(name, keep = true) {
  const mix = unsavedMix(name);
  if (mix) return playLayers(mix, { keep });
  const s = soundFor(name);
  if (s.kind === 'pick') return playLayers(s.layers, { keep });
  if (s.kind === 'def') return playDef(s.def, { keep });
  return null;
}

/** Plays the markers the clock passed since the last frame. before: clock.t before this frame's tick. */
export function soundTick(before) {
  if (!ui) return;
  const { clock } = ui, was = S.playing;
  S.playing = clock.playing;
  const ended = was && !clock.playing && clock.t >= clock.duration;
  if (!clock.playing && !ended) { if (was) stopAll(0.08); return; }
  if (!S.on || lib.error || document.hidden) return;
  const events = markers(ui.source());
  const fire = (from, to, inclusive) => { for (const e of events) if ((inclusive ? e.t >= from : e.t > from) && e.t <= to) play(e.def); };
  if (clock.wrapped) { fire(before, clock.duration, before === 0); stopLoops(0.2); fire(0, clock.t, true); }
  else if (clock.t >= before) fire(before, clock.t, before === 0);
  else stopAll(0.08);   // moved back while playing
}

/** Another effect: stop what is playing, keep the open marker only if this effect has it too. */
export function soundSelectionChanged() {
  if (!ui) return;
  stopAll(0.08);
  if (S.name && !markerNames(ui.source()).some((m) => m.name === S.name)) { stash(); S.name = null; }
  preloadMarkers();
  renderSoundPanel();
}

/** Timeline colour of a sound marker: teal when something plays for it, grey when it is silent, gold when open. */
export function soundMarkerColor(e) {
  if (e.def === S.name) return '#d9a441';
  return unsavedMix(e.def) || soundFor(e.def).kind ? '#6fb3b8' : '#555c62';
}

export function toggleSound(on = !S.on) {
  S.on = on;
  ui.store.set('sound', on);
  if (!on) stopAll(0.08);
  const box = document.getElementById('sound-on');
  if (box) box.checked = on;
}

function preloadMarkers() {
  for (const { name } of markerNames(ui?.source())) {
    const s = soundFor(name), mix = unsavedMix(name);
    if (mix) preload(mix);
    else if (s.kind === 'pick') preload(s.layers);
    else if (s.kind === 'def') preload(s.def.subs.flatMap((sub) => sub.grains.filter((g) => g.folder || g.clip)));
  }
}

/** Opens a marker in the panel, with the mix last tried for it if there is one. */
function open(name) {
  stash();
  const d = drafts.get(name);
  if (!d) return reset(name);
  S.name = name;
  Object.assign(S, { mix: d.mix, base: d.base, option: d.option, optionBase: d.optionBase, note: d.note });
  preload(S.mix);
  renderSoundPanel();
}

/** Opens a marker with what is saved for it, dropping what was tried. */
function reset(name) {
  drafts.delete(name);
  S.name = name;
  const s = soundFor(name);
  S.mix = s.kind === 'pick' ? cloneLayers(s.layers) : s.kind === 'def' ? layersOfDef(s.def) : [];
  S.base = JSON.stringify(strip(S.mix));
  S.option = s.kind === 'pick' ? lib.picks[soundKey(name)].option : null;
  S.optionBase = S.option ? S.base : null;
  S.note = lib.picks[soundKey(name)]?.note ?? '';
  preload(S.mix);
  renderSoundPanel();
}

function useLayers(layers, option = null) {
  S.mix = cloneLayers(layers);
  S.option = option;
  S.optionBase = option ? JSON.stringify(strip(S.mix)) : null;
  preload(S.mix);
  renderSoundPanel();
}

/** Where the open marker is heard from: shortly before its first time. */
function watchFrom(name) {
  const first = markerNames(ui.source()).find((m) => m.name === name)?.times[0] ?? 0;
  return Math.max(0, first - Before);
}

/** Plays the effect from shortly before the open marker; it plays on to the end and loops whole, every marker with its own sound. */
function watch() {
  stopAll(0.08);
  ui.clock.seek(watchFrom(S.name));
  ui.clock.playing = true;
}

/** An option from the sound lab, in the mix and heard with the picture. */
function tryOption(o) {
  useLayers(o.layers, o.label);
  watch();
}

const inMix = (o) => S.option === o.label && JSON.stringify(strip(S.mix)) === S.optionBase;

/** Saves one marker's mix as its pick; returns the new base. */
async function save(name, d) {
  const layers = strip(d.mix);
  const option = d.option && JSON.stringify(layers) === d.optionBase ? d.option : null;
  await savePick(soundKey(name), { option, layers, note: d.note.trim() });
  return JSON.stringify(layers);
}

async function pick() {
  S.base = await save(S.name, S);
  renderSoundPanel();
}

/** Saves every marker's tried mix, the open one too. */
async function pickAll() {
  stash();
  for (const [name, d] of drafts) if (d.mix.length && unsaved(d)) d.base = await save(name, d);
  if (S.name) S.base = drafts.get(S.name).base;
  renderSoundPanel();
}

async function unpick() {
  await savePick(soundKey(S.name), null);
  reset(S.name);
}

// ------------------------------------------------------------------ the Sound tab

/** Candidate moments from the sound lab for this marker: named for it ("defName"), or of the hero the effect's kit starts with. */
function optionsFor(name, kit) {
  const out = [];
  for (const [ability, c] of Object.entries(lib.candidates)) {
    const sameKit = kit && c.hero && kit.toLowerCase().startsWith(c.hero.toLowerCase());
    for (const m of c.moments ?? []) {
      if (m.defName === name || sameKit) out.push({ ability, label: lib.abilities.find((a) => a.defName === ability)?.label ?? ability, moment: m, linked: m.defName === name });
    }
  }
  return out.sort((a, b) => b.linked - a.linked);
}

function status(name) {
  const s = soundFor(name);
  if (s.kind === 'pick') return ['picked', `Picked: ${s.label}`];
  if (s.kind === 'def') return ['def', `The ${s.def.source === 'RimArt' ? "mod's" : `game's (${s.def.source})`} SoundDef${s.def.source === 'RimArt' ? `, ${s.def.file}` : ''}`];
  return ['none', 'Nothing yet: silent in the lab, and no SoundDef of this name exists'];
}

export function renderSoundPanel() {
  if (!ui) return;
  const { el, panel } = ui;
  if (panel.hidden) return;   // the tab renders itself when it is opened
  const source = ui.source(), names = markerNames(source);
  const top = el('div', { class: 'group' },
    el('h3', {}, 'Sound'),
    el('label', { class: 'slider' }, el('span', {}, 'Volume'),
      el('input', { type: 'range', min: 0, max: 2, step: 0.01, value: S.volume, oninput: (e) => {
        S.volume = master.gain.value = Number(e.target.value);
        ui.store.set('soundVolume', S.volume);
        e.target.nextElementSibling.textContent = S.volume.toFixed(2);
      } }), el('output', {}, S.volume.toFixed(2))),
    el('p', { class: 'hint' }, `The "Sound" box under the stage, or M, turns markers on and off. ${statusLine()}`));
  if (lib.error) { panel.replaceChildren(top); return; }

  const trying = names.filter(({ name }) => unsavedMix(name)?.length).length;
  const list = el('div', { class: 'group' }, el('h3', {}, `Markers${source ? ` in ${source.label}` : ''}`),
    ...(names.length ? names.map(({ name, times }) => {
      const [kind] = unsavedMix(name) ? ['trying'] : status(name);
      return el('div', { class: 'marker' },
        el('button', { class: 'mini', type: 'button', title: 'Hear it', onclick: () => play(name, false) }, '▶'),
        el('button', { class: 'phase', type: 'button', 'aria-current': name === S.name ? 'true' : 'false', onclick: () => open(name) },
          el('span', {}, name), el('span', {}, `${times.slice(0, 3).map(fmt).join(', ')}${times.length > 3 ? ` +${times.length - 3}` : ''} s`)),
        el('span', { class: `tag sound-${kind}` }, kind === 'def' ? 'SoundDef' : kind));
    }) : [el('p', { class: 'hint' }, 'No sound markers. A sketch adds one in events(): { t, type: \'sound\', def: \'AG_Name\' }.')]),
    ...(trying ? [el('div', { class: 'row' }, el('button', { type: 'button', class: 'primary', onclick: pickAll }, `Pick all ${trying} tried`))] : []),
    el('p', { class: 'hint' }, 'Every marker plays what you chose for it, "trying" ones too, so ▶ under the stage plays the whole effect with all of them. Click a name to choose its sound.'));

  panel.replaceChildren(top, list, ...(S.name ? editor(source) : []));
}

function editor(source) {
  const { el } = ui, name = S.name;
  const [, line] = status(name);
  const head = el('div', { class: 'group' },
    el('h3', {}, name),
    el('p', { class: 'hint' }, line),
    el('p', { class: 'unsaved', id: 'sound-unsaved', hidden: !dirty() }, 'Your mix is not saved; the timeline plays it.'),
    el('div', { class: 'row' },
      el('button', { type: 'button', onclick: watch }, `▶ Watch from ${fmt(watchFrom(name))} s`),
      el('button', { type: 'button', onclick: () => play(name, false) }, 'Hear it alone')),
    el('p', { class: 'hint' }, 'An option\'s ▶ plays the effect from just before this marker, with the other markers\' sounds too.'));

  const options = optionsFor(name, source?.kit);
  const optionGroup = options.length ? [el('div', { class: 'group' },
    el('h3', {}, 'Options from the sound lab'),
    ...options.map(({ label, moment, linked }) => el('details', { class: 'moment', open: linked },
      el('summary', {}, `${label} · ${moment.label}`),
      ...moment.options.map((o) => el('div', { class: 'result', 'aria-current': inMix(o) ? 'true' : 'false' },
        el('button', { class: 'mini', type: 'button', title: 'Play it with the picture (puts it in the mix)', onclick: () => tryOption(o) }, '▶'),
        el('span', { class: 'what', title: o.layers.map((l) => l.folder || l.clip).join(' + ') }, o.label,
          ...(missingIn(o.layers).length ? [el('span', { class: 'missing' }, ' missing clips')] : [])),
        el('button', { class: 'mini', type: 'button', title: 'Hear it alone', onclick: () => playLayers(o.layers.map((l) => newLayer(l, l))) }, '♪'))))))] : [];

  const layers = S.mix.map((l, i) => el('div', { class: 'mix-layer' },
    el('div', { class: 'mix-head' },
      el('button', { class: 'mini', type: 'button', title: 'Play this layer alone', onclick: () => playLayers([{ ...l, mute: false }]) }, '▶'),
      el('span', { class: 'what', title: l.folder || l.clip }, `${l.folder ? 'folder ' : ''}${l.folder || l.clip}`),
      ...sourcesOf(l).filter((s) => s !== 'Core').map((s) => el('span', { class: 'tag' }, s)),
      el('button', { class: 'mini', type: 'button', title: 'Remove', onclick: () => { S.mix.splice(i, 1); renderSoundPanel(); } }, '×')),
    slider(l, 'pitch', 0.2, 3, 0.01, (v) => v.toFixed(2)),
    slider(l, 'volume', 0, 150, 1, (v) => String(Math.round(v))),
    slider(l, 'delay', 0, 3, 0.01, (v) => `${v.toFixed(2)} s`),
    el('div', { class: 'row' },
      check(l, 'loop', 'loop (ends when the effect loops)'),
      check(l, 'mute', 'mute'))));

  const results = el('div', { class: 'results' });
  const search = el('input', { type: 'search', placeholder: 'Folder, clip or SoundDef name', value: S.search, autocomplete: 'off', spellcheck: 'false',
    oninput: (e) => { S.search = e.target.value; fillResults(results); } });
  fillResults(results);
  const needs = dlcNeeds(S.mix);

  const mix = el('div', { class: 'group' },
    el('h3', {}, 'Mix'),
    ...(layers.length ? layers : [el('p', { class: 'hint' }, 'No layers. Use an option above, or add clips below.')]),
    ...(needs.length ? [el('p', { class: 'hint' }, `Needs MayRequire: ${needs.join(', ')}. Players without it hear nothing from that layer.`)] : []),
    el('label', { class: 'select' }, el('span', {}, 'Add a clip, a folder or a SoundDef'), search),
    results,
    el('label', { class: 'select' }, el('span', {}, 'Note for Claude'),
      el('textarea', { rows: 2, placeholder: 'What is right or wrong with it', oninput: (e) => { S.note = e.target.value; } }, S.note)),
    el('div', { class: 'row' },
      el('button', { type: 'button', class: 'primary', disabled: !S.mix.length, onclick: pick }, 'Pick'),
      ...(lib.picks[soundKey(name)] ? [el('button', { type: 'button', onclick: unpick }, 'Unpick')] : []),
      el('button', { type: 'button', id: 'sound-undo', hidden: !dirty(), onclick: () => reset(name) }, 'Undo changes'),
      copyXml()),
    el('p', { class: 'hint' }, `Pick saves the mix to Tools/SoundLab/picks.json as sound:${name}; Claude writes the SoundDef from it.`));

  return [head, ...optionGroup, mix];
}

function slider(layer, key, min, max, step, show) {
  const { el } = ui;
  const out = el('output', {}, show(Number(layer[key])));
  return el('label', { class: 'mix-slider' }, el('span', {}, key),
    el('input', { type: 'range', min, max, step, value: layer[key], oninput: (e) => { layer[key] = Number(e.target.value); out.textContent = show(layer[key]); markDirty(); } }),
    out);
}

function check(layer, key, label) {
  const { el } = ui;
  return el('label', { class: 'check' }, el('input', { type: 'checkbox', checked: !!layer[key], onchange: (e) => { layer[key] = e.target.checked; renderSoundPanel(); } }), label);
}

// A slider moves without a re-render, which would end the drag; only the "not saved" line and
// the undo button follow it.
function markDirty() {
  for (const id of ['sound-unsaved', 'sound-undo']) { const node = document.getElementById(id); if (node) node.hidden = !dirty(); }
}

function copyXml() {
  const { el } = ui;
  const button = el('button', { type: 'button', onclick: async () => {
    const text = defXml(S.name, S.mix);
    try { await navigator.clipboard.writeText(text); button.textContent = 'Copied'; } catch { console.log(text); button.textContent = 'Clipboard blocked; see console'; }
    setTimeout(() => { button.textContent = 'Copy SoundDef XML'; }, 1600);
  } }, 'Copy SoundDef XML');
  return button;
}

/** Search results: folders and clips by path, SoundDefs by name; ▶ hears it, + or Use puts it in the mix. */
function fillResults(box) {
  const { el } = ui;
  const words = S.search.toLowerCase().split(/\s+/).filter(Boolean);
  if (!words.length) { box.replaceChildren(); return; }
  const has = (text) => words.every((w) => text.toLowerCase().includes(w));
  const folders = lib.folders.filter((f) => has(f.folder)).slice(0, 8);
  const clips = lib.clips.filter((c) => has(`${c.folder}/${c.clip}`)).slice(0, 14);
  const defs = [...lib.modDefs, ...lib.vanillaDefs].filter((d) => has(d.defName)).slice(0, 6);
  const tags = (sources) => [...sources].filter((s) => s !== 'Core').map((s) => el('span', { class: 'tag' }, s));
  const row = (hear, label, extra, add, addLabel = '+') => el('div', { class: 'result' },
    el('button', { class: 'mini', type: 'button', title: 'Hear it', onclick: hear }, '▶'),
    el('span', { class: 'what', title: label }, label, ...extra),
    el('button', { type: 'button', onclick: add }, addLabel));
  const add = (grain) => { S.mix.push(newLayer(grain)); preload(S.mix); renderSoundPanel(); };
  box.replaceChildren(
    ...defs.map((d) => row(() => playDef(d), d.defName, [el('span', { class: 'tag' }, d.source)], () => useLayers(layersOfDef(d)), 'Use')),
    ...folders.map((f) => row(() => playLayers([newLayer({ folder: f.folder })]), `folder ${f.folder} (${f.count})`, tags(f.sources), () => add({ folder: f.folder }))),
    ...clips.map((c) => { const path = c.folder ? `${c.folder}/${c.clip}` : c.clip;
      return row(() => playLayers([newLayer({ clip: path })]), path, [...tags([c.source]), ...(c.dur != null ? [el('span', { class: 'dim' }, ` ${c.dur.toFixed(2)} s`)] : [])], () => add({ clip: path })); }),
    ...(defs.length + folders.length + clips.length ? [] : [el('p', { class: 'hint' }, 'Nothing matches.')]));
}
