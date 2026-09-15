// The page: effect browser, stage, transport, inspector. It wires sources to the renderer and
// keeps nothing an effect needs; everything about an effect lives in its recording or sketch.

import { Renderer } from './gl.js';
import { Scene } from './scene.js';
import { Camera, bindCamera, shakeAt } from './camera.js';
import { RecordedSource, SketchSource, Clock, RecordedCell } from './player.js';
import { layerOf } from './engine.js';
import sketchFiles from '../sketches/index.js';

const $ = (id) => document.getElementById(id);
const el = (tag, attrs = {}, ...children) => {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (k === 'class') node.className = v;
    else if (k.startsWith('on')) node.addEventListener(k.slice(2), v);
    else if (v !== undefined && v !== null && v !== false) node.setAttribute(k, v === true ? '' : v);
  }
  for (const c of children.flat()) if (c != null) node.append(c);
  return node;
};
const store = {
  get(key, fallback) { try { const v = localStorage.getItem(`vfxlab:${key}`); return v == null ? fallback : JSON.parse(v); } catch { return fallback; } },
  set(key, value) { try { localStorage.setItem(`vfxlab:${key}`, JSON.stringify(value)); } catch { /* private window */ } },
};
const fmt = (s, digits = 3) => (Number.isFinite(s) ? s.toFixed(digits) : '–');
const Speeds = [1, 0.5, 0.25, 0.1];

const state = {
  entries: [],
  sources: new Map(),
  a: store.get('a', null),
  b: store.get('b', null),
  compare: store.get('compare', false),
  cell: store.get('cell', { ...RecordedCell }),
  hidden: new Set(),
  stamp: null,
  status: null,
  frames: [],
  standIns: new Set(),
  drawn: 0,
};

const clock = new Clock();
let renderer, scene, camera;

boot().catch((error) => showNotice(`<p>The lab could not start.</p><pre class="log">${escape(error.stack ?? error)}</pre>`));

async function boot() {
  if (location.protocol === 'file:') {
    showNotice(`<p>Open the lab through its server, not as a file — WebGL cannot read the mod's textures over <code>file://</code>.</p>
      <p>From the repository root run <code>python3 Tools/VfxLab/lab.py</code> and open the address it prints.</p>`);
    return;
  }
  renderer = new Renderer($('gl'));
  scene = new Scene(renderer);
  camera = new Camera();
  Object.assign(scene.sun, store.get('sun', {}));
  Object.assign(scene.show, store.get('show', {}));
  camera.ppc = store.get('ppc', camera.ppc);
  centreCamera();

  for (const file of sketchFiles) {
    const module = (await import(`../sketches/${file}`)).default;
    const source = new SketchSource(module, file);
    const saved = store.get(`params:${file}`, {});
    for (const k of Object.keys(source.values)) if (k in saved) source.values[k] = saved[k];
    state.sources.set(source.id, source);
  }
  await refreshIndex(true);
  bindChrome();

  // ?effect=<label>&b=<label>&t=<seconds>&paused: open an exact frame, for links and for tests.
  const query = new URLSearchParams(location.search);
  const byLabel = (label) => state.entries.find((e) => e.label === label)?.id;
  if (query.has('effect') && byLabel(query.get('effect'))) { state.a = byLabel(query.get('effect')); state.compare = false; }
  if (query.has('b') && byLabel(query.get('b'))) { state.b = byLabel(query.get('b')); state.compare = true; }
  if (!entryFor(state.a)) state.a = (state.entries.find((e) => e.label === 'Six Paths: slam') ?? state.entries[0])?.id ?? null;
  if (state.b && !entryFor(state.b)) state.b = null;
  await ensureLoaded(state.a);
  if (state.compare) await ensureLoaded(state.b);
  selectionChanged(true);
  // p.<param>=<value> sets a sketch param, so a link can carry a whole configuration.
  for (const source of [sourceA(), sourceB()]) {
    if (source?.kind !== 'sketch') continue;
    for (const [k, v] of query) {
      if (!k.startsWith('p.') || !(k.slice(2) in source.values)) continue;
      const current = source.values[k.slice(2)];
      source.values[k.slice(2)] = typeof current === 'number' ? Number(v) : typeof current === 'boolean' ? v === 'true' : v;
    }
    renderParams();
  }
  if (query.has('t')) { clock.duration = activeDuration(); clock.seek(Number(query.get('t'))); }
  if (query.has('paused') || query.has('t')) clock.playing = false;
  if (query.has('cell')) { const [x, z] = query.get('cell').split(',').map(Number); state.cell = { x, z }; centreCamera(); }
  if (query.has('ppc')) camera.ppc = Number(query.get('ppc'));
  state.debug = window.__labDebug = query.has('debug');
  if (state.debug) window.__lab = { state, clock, camera };

  let last = performance.now(), layersAt = 0;
  const frame = (now) => {
    clock.duration = activeDuration();
    clock.tick(Math.min(0.1, (now - last) / 1000));
    last = now;
    drawStage();
    drawTimeline();
    updateTime();
    if (now - layersAt > 250) { layersAt = now; renderLayers(); }
    requestAnimationFrame(frame);
  };
  requestAnimationFrame(frame);
  setInterval(() => refreshIndex(false), 1500);
}

// ------------------------------------------------------------------ entries and sources

async function refreshIndex(first) {
  let index, status = null;
  try {
    const response = await fetch('../recordings/index.json', { cache: 'no-store' });
    index = response.ok ? await response.json() : null;
  } catch { index = null; }
  try {
    const response = await fetch('../recordings/status.json', { cache: 'no-store' });
    status = response.ok ? await response.json() : null;
  } catch { status = null; }

  const statusKey = status ? `${status.stamp}${status.ok}` : null;
  if (statusKey !== state.statusKey) { state.statusKey = statusKey; state.status = status; }

  const stamp = index?.stamp ?? null;
  if (stamp !== state.stamp) {
    const changed = !first && state.stamp !== null;
    state.stamp = stamp;
    const recorded = (index?.recordings ?? []).map((r) => ({
      id: `recorded:${r.file}`, kind: 'recorded', file: r.file, label: r.label, kit: r.kit, seconds: r.seconds, still: r.still,
    }));
    const sketches = [...state.sources.values()].filter((s) => s.kind === 'sketch')
      .map((s) => ({ id: s.id, kind: 'sketch', file: s.file, label: s.label, kit: s.kit, seconds: s.duration, still: false }));
    state.entries = [...recorded, ...sketches].sort((x, y) => x.kit.localeCompare(y.kit) || (x.kind === y.kind ? 0 : x.kind === 'recorded' ? -1 : 1) || x.label.localeCompare(y.label));
    if (changed) {
      // New recordings from lab.py: swap the loaded ones in place, keeping time, zoom and selection.
      for (const [id, source] of [...state.sources]) if (source.kind === 'recorded') state.sources.delete(id);
      await ensureLoaded(state.a);
      if (state.compare) await ensureLoaded(state.b);
      renderParams();
    }
    renderBrowser();
    renderCompareSelects();
  }
  renderStatus(index);
}

const entryFor = (id) => state.entries.find((e) => e.id === id);

async function ensureLoaded(id) {
  if (!id || state.sources.has(id)) return state.sources.get(id);
  const entry = entryFor(id);
  if (!entry || entry.kind !== 'recorded') return null;
  const source = await RecordedSource.load(entry);
  state.sources.set(id, source);
  return source;
}

const sourceA = () => state.sources.get(state.a);
const sourceB = () => state.sources.get(state.b);
const active = () => (state.compare ? [sourceA(), sourceB()] : [sourceA()]);
const activeDuration = () => Math.max(0, ...active().filter(Boolean).map((s) => s.duration));

async function select(id, side = 'a') {
  await ensureLoaded(id);
  state[side] = id;
  if (side === 'b') state.compare = true;
  selectionChanged(true);
}

function selectionChanged(restart) {
  store.set('a', state.a); store.set('b', state.b); store.set('compare', state.compare);
  if (restart) { clock.t = 0; clock.playing = true; }
  clock.duration = activeDuration();
  $('compare-on').checked = state.compare;
  renderBrowser();
  renderParams();
  renderCompareSelects();
  renderViewLabels();
  const a = sourceA();
  $('now-label').textContent = a ? `${a.label}${state.compare && sourceB() ? `  ·  compared with ${sourceB().label}` : ''}` : '';
  document.title = a ? `${a.label} · RimArt VFX Lab` : 'RimArt VFX Lab';
}

// ------------------------------------------------------------------ stage

function viewRects() {
  const stage = $('stage'), w = stage.clientWidth, h = stage.clientHeight;
  return state.compare ? [[0, 0, w / 2 - 1, h], [w / 2 + 1, 0, w / 2 - 1, h]] : [[0, 0, w, h]];
}

function drawStage() {
  const rects = viewRects(), views = [], frames = [];
  active().forEach((source, i) => {
    const rect = rects[i];
    if (!source || !rect) return;
    const t = Math.min(clock.t, source.duration);
    const frame = source.frameAt(t, state.cell, scene);
    const shake = shakeAt(source.events, clock.t);
    const cells = Math.max(rect[2], rect[3]) / camera.ppc / 2;
    views.push({
      rect,
      camera: { cx: camera.cx + shake.x, cz: camera.cz + shake.z, ppc: camera.ppc },
      calls: scene.calls({ x: camera.cx, z: camera.cz }, cells).concat(frame.calls),
      hidden: state.hidden,
    });
    frames.push({ source, frame, shake, t });
  });
  state.standIns = renderer.render(views);
  state.frames = frames;
  // ?debug: after 3 drawn frames, log what the first view drew and which textures resolved.
  if (state.debug && [3, 60, 200].includes(++state.drawn)) {
    console.log('LAB frame', JSON.stringify(frames[0]?.frame.calls.slice(0, 14).map((c) => ({ g: c.group, y: +c.y.toFixed(3), a: +c.a.toFixed(3), sx: +c.sx.toFixed(2), tri: c.mesh?.tri?.length, uv: c.mesh?.uv?.length, v: c.mesh?.v?.length }))));
    console.log('LAB textures', JSON.stringify([...renderer.textures.values()].map((t) => [t.path, t.ready, t.standIn])));
    console.log('LAB gl error', renderer.gl.getError());
  }
  drawOverlay(rects);
  updateHud();
}

function drawOverlay(rects) {
  const canvas = $('overlay'), dpr = window.devicePixelRatio || 1;
  const w = canvas.clientWidth, h = canvas.clientHeight;
  if (canvas.width !== Math.round(w * dpr) || canvas.height !== Math.round(h * dpr)) { canvas.width = Math.round(w * dpr); canvas.height = Math.round(h * dpr); }
  const ctx = canvas.getContext('2d');
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.clearRect(0, 0, w, h);
  for (const rect of rects.slice(0, state.frames.length || 1)) {
    ctx.save();
    ctx.beginPath(); ctx.rect(...rect); ctx.clip();
    if (scene.show.grid && camera.ppc >= 6) {
      const left = camera.toWorld(rect[0], 0, rect).x, right = camera.toWorld(rect[0] + rect[2], 0, rect).x;
      const top = camera.toWorld(0, rect[1], rect).z, bottom = camera.toWorld(0, rect[1] + rect[3], rect).z;
      for (let x = Math.floor(left); x <= Math.ceil(right); x++) {
        const [sx] = camera.toScreen(x, 0, rect);
        ctx.strokeStyle = x % 10 === 0 ? 'rgba(255,255,255,0.22)' : 'rgba(255,255,255,0.08)';
        ctx.beginPath(); ctx.moveTo(Math.round(sx) + 0.5, rect[1]); ctx.lineTo(Math.round(sx) + 0.5, rect[1] + rect[3]); ctx.stroke();
      }
      for (let z = Math.floor(bottom); z <= Math.ceil(top); z++) {
        const [, sy] = camera.toScreen(0, z, rect);
        ctx.strokeStyle = z % 10 === 0 ? 'rgba(255,255,255,0.22)' : 'rgba(255,255,255,0.08)';
        ctx.beginPath(); ctx.moveTo(rect[0], Math.round(sy) + 0.5); ctx.lineTo(rect[0] + rect[2], Math.round(sy) + 0.5); ctx.stroke();
      }
    }
    // The cell the effect is played on, as the dev tool's cursor would show it.
    const [x0, y0] = camera.toScreen(state.cell.x, state.cell.z + 1, rect);
    ctx.strokeStyle = 'rgba(217,164,65,0.9)';
    ctx.lineWidth = 1.5;
    ctx.strokeRect(x0 + 0.75, y0 + 0.75, camera.ppc - 1.5, camera.ppc - 1.5);
    ctx.restore();
  }
  if (rects.length > 1) { ctx.fillStyle = '#394046'; ctx.fillRect(rects[1][0] - 2, 0, 2, h); }
}

function updateHud() {
  const f = state.frames[0];
  const rows = [];
  if (f) {
    rows.push(['time', `${fmt(clock.t)} s`]);
    if (f.source.kind === 'recorded' && f.frame.clock != null && Math.abs(f.frame.clock - clock.t) > 0.002)
      rows.push(['effect clock', `${fmt(f.frame.clock)} s`]);
    rows.push(['frame', f.source.still ? 'still' : `${f.frame.index + 1} / ${f.frame.frames}`]);
    rows.push(['draw calls', state.frames.map((x) => x.frame.calls.length).join(' | ')]);
    const shake = Math.max(...state.frames.map((x) => x.shake.mag));
    if (shake > 0) rows.push(['shake', fmt(shake)]);
  }
  rows.push(['zoom', `${fmt(camera.ppc, 1)} px/cell`]);
  rows.push(['cell', `${state.cell.x}, ${state.cell.z}`]);
  const key = rows.map((r) => r.join('=')).join('|');
  if (key === state.hudKey) return;
  state.hudKey = key;
  $('hud').replaceChildren(...rows.flatMap(([k, v]) => [el('dt', {}, k), el('dd', {}, v)]));
}

function renderViewLabels() {
  const box = $('view-labels');
  if (!state.compare) { box.replaceChildren(); return; }
  const rects = viewRects();
  box.replaceChildren(...[sourceA(), sourceB()].map((s, i) => s && el('div', {
    class: 'view-label', style: `left:${rects[i][0] + rects[i][2] - 12}px; transform: translateX(-100%)`,
  }, el('b', {}, i ? 'B' : 'A'), s.label)));
}

// ------------------------------------------------------------------ transport

function drawTimeline() {
  const canvas = $('timeline'), dpr = window.devicePixelRatio || 1;
  const w = canvas.clientWidth, h = canvas.clientHeight;
  if (canvas.width !== Math.round(w * dpr) || canvas.height !== Math.round(h * dpr)) { canvas.width = Math.round(w * dpr); canvas.height = Math.round(h * dpr); }
  const ctx = canvas.getContext('2d');
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.clearRect(0, 0, w, h);
  const duration = clock.duration;
  const x = (t) => (duration > 0 ? (t / duration) * (w - 2) + 1 : 1);
  const lanes = active().filter(Boolean);
  const laneH = lanes.length > 1 ? 20 : 30;

  ctx.font = '11px ui-monospace, Consolas, monospace';
  lanes.forEach((source, lane) => {
    const top = 4 + lane * (laneH + 4);
    ctx.fillStyle = '#1b1f22';
    ctx.fillRect(x(0), top, x(source.duration) - x(0), laneH);
    const phases = source.phases ?? [];
    phases.forEach((p, i) => {
      const x0 = x(p.t), x1 = x(phases[i + 1]?.t ?? source.duration);
      ctx.fillStyle = i % 2 ? '#2c3237' : '#262b30';
      ctx.fillRect(x0, top, Math.max(1, x1 - x0), laneH);
      ctx.fillStyle = '#394046';
      ctx.fillRect(x0, top, 1, laneH);
      ctx.fillStyle = '#8c8a82';
      ctx.save(); ctx.beginPath(); ctx.rect(x0, top, x1 - x0 - 2, laneH); ctx.clip();
      ctx.fillText(p.name, x0 + 5, top + laneH / 2 + 4);
      ctx.restore();
    });
    for (const e of source.events ?? []) {
      ctx.fillStyle = e.type === 'shake' ? '#8f6fd8' : '#6fb3b8';
      ctx.fillRect(x(e.t) - 1, top - 3, 2, laneH + 6);
    }
  });

  const px = x(clock.t);
  ctx.fillStyle = '#d9a441';
  ctx.fillRect(px - 1, 0, 2, h);
  ctx.beginPath(); ctx.moveTo(px - 5, 0); ctx.lineTo(px + 5, 0); ctx.lineTo(px, 6); ctx.fill();
  const bottom = h - 4;
  ctx.fillStyle = '#8c8a82';
  for (let s = 0; s <= duration; s += duration > 20 ? 5 : duration > 6 ? 1 : 0.5) {
    ctx.fillRect(x(s), bottom - 4, 1, 4);
  }
}

function updateTime() {
  const f = state.frames[0];
  const frameText = f ? (f.source.still ? 'still frame' : `frame ${f.frame.index + 1}`) : '';
  const html = `<b>${fmt(clock.t)}</b> / ${fmt(clock.duration)} s · ${frameText}`;
  if (html !== state.timeHtml) { state.timeHtml = html; $('time').innerHTML = html; }
  const playing = clock.playing && clock.duration > 0;
  if (playing !== state.playIcon) {
    state.playIcon = playing;
    $('play').setAttribute('aria-label', playing ? 'Pause' : 'Play');
    $('play').innerHTML = playing
      ? '<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M5 3v10M11 3v10" stroke-width="3"/></svg>'
      : '<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M4 2.5v11l9-5.5z"/></svg>';
  }
}

// ------------------------------------------------------------------ panels

function renderBrowser() {
  const box = $('browser');
  if (!state.entries.length) {
    box.replaceChildren(el('p', { class: 'empty' }, 'No recordings yet. Run python3 Tools/VfxLab/lab.py from the repository root.'));
    return;
  }
  const kits = [...new Set(state.entries.map((e) => e.kit))];
  box.replaceChildren(...kits.flatMap((kit) => [
    el('div', { class: 'kit' }, kit),
    ...state.entries.filter((e) => e.kit === kit).map((e) => {
      const name = e.label.startsWith(`${kit}:`) ? e.label.slice(kit.length + 1).trim() : e.label;
      const side = state.compare ? (e.id === state.a ? 'A' : e.id === state.b ? 'B' : null) : null;
      return el('button', {
        class: 'effect', type: 'button', 'aria-current': e.id === state.a || (state.compare && e.id === state.b) ? 'true' : 'false',
        title: 'Click to play. Shift-click to compare on the right (B).',
        onclick: (ev) => select(e.id, ev.shiftKey ? 'b' : 'a'),
      },
      el('span', { class: 'name' }, name),
      el('span', { class: 'dur' }, e.still ? 'still' : `${fmt(e.seconds, 2)} s`),
      el('span', { class: 'tags' },
        el('span', { class: `tag ${e.kind}` }, e.kind === 'recorded' ? 'recorded' : 'sketch'),
        side && el('span', { class: 'tag side' }, side)));
    }),
  ]));
}

function renderParams() {
  const panel = $('panel-params'), source = sourceA();
  if (!source) { panel.replaceChildren(el('p', { class: 'hint' }, 'Pick an effect on the left.')); return; }
  const phases = el('div', { class: 'group' }, el('h3', {}, 'Phases'),
    ...(source.phases ?? []).map((p) => el('button', { class: 'phase', type: 'button', onclick: () => { clock.playing = false; clock.seek(p.t + 0.0001); } },
      el('span', {}, p.name), el('span', {}, `${fmt(p.t, 2)} s`))));
  if (source.still) phases.append(el('p', { class: 'hint' }, 'A frozen preview: one frame, no timeline.'));

  if (source.kind === 'recorded') {
    const events = source.events.map((e) => el('button', { class: 'phase', type: 'button', onclick: () => { clock.playing = false; clock.seek(e.t); } },
      el('span', {}, e.type === 'shake' ? `Camera shake ${fmt(e.value, 2)}` : `Sound ${e.def}`), el('span', {}, `${fmt(e.t, 2)} s`)));
    panel.replaceChildren(
      el('div', { class: 'group' },
        el('h3', {}, 'Recorded from the mod'),
        el('dl', { class: 'facts' },
          el('dt', {}, 'Dev action'), el('dd', {}, `RimArts → ${source.label}`),
          el('dt', {}, 'Frames'), el('dd', {}, `${source.frames.length} at 60 fps`),
          el('dt', {}, 'Length'), el('dd', {}, source.still ? 'still' : `${fmt(source.duration, 2)} s`),
          el('dt', {}, 'File'), el('dd', {}, `recordings/${source.file}`)),
        el('p', { class: 'hint' }, 'This is the kit\'s own C# run outside the game. Edit it under Source/RimArt and, while lab.py is running, it is re-recorded and reloaded here within a few seconds. Nothing to tune on this page.')),
      phases,
      ...(events.length ? [el('div', { class: 'group' }, el('h3', {}, 'Events'), ...events)] : []),
    );
    return;
  }

  const groups = new Map();
  for (const [key, p] of Object.entries(source.module.params)) {
    if (!groups.has(p.group)) groups.set(p.group, []);
    groups.get(p.group).push([key, p]);
  }
  const save = () => { store.set(`params:${source.file}`, source.values); clock.duration = activeDuration(); };
  const controls = [...groups].map(([group, items]) => el('div', { class: 'group' }, el('h3', {}, group), ...items.map(([key, p]) => {
    const id = `param-${key}`;
    if (p.options) {
      return el('label', { class: 'select' }, el('span', {}, p.label),
        el('select', { id, onchange: (e) => { source.values[key] = e.target.value; save(); renderParams(); } },
          ...p.options.map((o) => el('option', { value: o, selected: source.values[key] === o }, o))));
    }
    if (typeof p.value === 'boolean') {
      return el('label', { class: 'check' }, el('input', { type: 'checkbox', id, checked: source.values[key], onchange: (e) => { source.values[key] = e.target.checked; save(); } }), p.label);
    }
    const out = el('output', { for: id }, String(source.values[key]));
    return el('label', { class: 'slider' }, el('span', {}, p.label),
      el('input', { type: 'range', id, min: p.min, max: p.max, step: p.step, value: source.values[key],
        oninput: (e) => { source.values[key] = Number(e.target.value); out.textContent = e.target.value; save(); } }),
      out);
  })));

  const copy = el('button', { type: 'button', onclick: async () => {
    const text = Object.entries(source.values).map(([k, v]) => {
      const name = k[0].toUpperCase() + k.slice(1);
      if (typeof v === 'boolean') return `public const bool ${name} = ${v};`;
      if (typeof v === 'string') return `// ${name}: ${v}`;
      return `public const float ${name} = ${Number(v)}f;`;
    }).join('\n');
    try { await navigator.clipboard.writeText(text); copy.textContent = 'Copied'; }
    catch { copy.textContent = 'Clipboard blocked; see console'; console.log(text); }
    setTimeout(() => { copy.textContent = 'Copy as C# constants'; }, 1600);
  } }, 'Copy as C# constants');
  const reset = el('button', { type: 'button', onclick: () => { source.values = SketchSource.defaults(source.module); save(); renderParams(); } }, 'Reset to defaults');

  panel.replaceChildren(
    el('div', { class: 'group' },
      el('h3', {}, 'Sketch'),
      el('p', { class: 'hint' }, `A proposal in JavaScript, not the game. ${source.module.compareWith ? `Compare it with the recorded "${source.module.compareWith}" on the Compare tab.` : ''}`),
      el('div', { class: 'row' }, reset, copy)),
    phases, ...controls);
}

function renderLayers() {
  const panel = $('panel-layers');
  if (panel.hidden) return;
  const rows = new Map();
  for (const f of state.frames) {
    for (const c of f.frame.calls) {
      const row = rows.get(c.group) ?? { count: 0, layers: new Set(), scene: false };
      row.count++;
      row.layers.add(layerOf(c.y).name);
      rows.set(c.group, row);
    }
  }
  for (const g of ['scene · terrain', 'scene · shadows', 'scene · trees', 'scene · rocks', 'scene · pawn (1 cell)', 'scene · backdrop'])
    if (!rows.has(g)) rows.set(g, { count: null, layers: new Set(), scene: true });
  const key = [...rows].map(([g, r]) => `${g}:${r.count}:${[...r.layers]}:${state.hidden.has(g)}`).join('|') + [...state.standIns].join();
  if (key === state.layersKey) return;
  state.layersKey = key;

  const list = [...rows].sort((a, b) => Number(a[1].scene) - Number(b[1].scene) || a[0].localeCompare(b[0]));
  const standIns = [...state.standIns];
  panel.replaceChildren(
    el('p', { class: 'hint' }, `Draw calls on screen this frame${state.compare ? ', both sides' : ''}, grouped by texture and shader. Untick to hide.`),
    ...(standIns.length ? [el('div', { class: 'warn' }, 'Stand-ins on screen — vanilla art the lab cannot read, drawn approximately:', el('ul', {}, ...standIns.map((s) => el('li', {}, s))))] : []),
    ...list.map(([group, row]) => el('label', { class: 'layer' },
      el('input', { type: 'checkbox', checked: !state.hidden.has(group), onchange: (e) => { if (e.target.checked) state.hidden.delete(group); else state.hidden.add(group); state.layersKey = null; } }),
      el('span', { class: 'what' }, group),
      el('span', { class: 'count' }, row.count == null ? '' : `×${row.count}`),
      row.layers.size ? el('span', { class: 'where' }, [...row.layers].join(', ')) : null)),
  );
}

function renderCompareSelects() {
  for (const [id, side] of [['compare-a', 'a'], ['compare-b', 'b']]) {
    const select = $(id);
    select.replaceChildren(el('option', { value: '' }, '—'), ...state.entries.map((e) => el('option', { value: e.id, selected: state[side] === e.id }, `${e.label}${e.kind === 'sketch' ? ' (sketch)' : ''}`)));
  }
}

function renderStatus(index) {
  const button = $('status'), status = state.status;
  if (status && !status.ok) {
    button.dataset.state = 'bad';
    button.textContent = 'Recorder failed — showing last good recordings';
    button.onclick = () => showNotice(`<p>The last recording run failed. The previous recordings are still loaded.</p><pre class="log">${escape(status.log)}</pre><p class="hint">Fix and save; lab.py re-records on its own.</p>`, true);
    return;
  }
  if (!index) {
    button.dataset.state = 'bad';
    button.textContent = 'No recordings';
    button.onclick = () => showNotice('<p>No recordings found. Run <code>python3 Tools/VfxLab/lab.py</code> from the repository root.</p>', true);
    return;
  }
  button.dataset.state = 'ok';
  const at = new Date(index.stamp).toLocaleTimeString();
  button.textContent = `${index.recordings.length} recorded · ${at}`;
  button.onclick = () => showNotice(`<p>Recorded ${index.recordings.length} previews at ${at}.</p><pre class="log">${escape(status?.log ?? '')}</pre>`, true);
}

function showNotice(html, dismissable = false) {
  const box = $('notice');
  box.innerHTML = html + (dismissable ? '<p><button type="button" id="notice-close">Close</button></p>' : '');
  box.hidden = false;
  if (dismissable) $('notice-close').onclick = () => { box.hidden = true; };
}

const escape = (s) => String(s).replace(/[&<>]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;' }[c]));

// ------------------------------------------------------------------ chrome

function centreCamera() {
  camera.cx = state.cell.x + 0.5;
  camera.cz = state.cell.z + 2.5;
}

function bindChrome() {
  bindCamera($('stage'), camera, {
    rectAt: (px) => viewRects().find((r) => px >= r[0] && px <= r[0] + r[2]),
    onCell: (x, z) => { state.cell = { x, z }; store.set('cell', state.cell); },
    onChange: () => { store.set('ppc', camera.ppc); renderViewLabels(); },
  });
  window.addEventListener('resize', renderViewLabels);

  $('play').onclick = () => { if (clock.t >= clock.duration && !clock.loop) clock.t = 0; clock.playing = !clock.playing; };
  $('step-back').onclick = () => clock.step(-1);
  $('step-forward').onclick = () => clock.step(1);
  $('loop').onchange = (e) => { clock.loop = e.target.checked; };
  const speeds = $('speeds');
  const renderSpeeds = () => speeds.replaceChildren(...Speeds.map((s) => el('button', {
    type: 'button', 'aria-pressed': clock.speed === s ? 'true' : 'false', onclick: () => { clock.speed = s; renderSpeeds(); },
  }, `${s}×`)));
  renderSpeeds();

  const timeline = $('timeline');
  const seek = (e) => { const r = timeline.getBoundingClientRect(); clock.seek(((e.clientX - r.left) / r.width) * clock.duration); };
  timeline.addEventListener('pointerdown', (e) => { clock.playing = false; timeline.setPointerCapture(e.pointerId); seek(e); timeline.onpointermove = seek; });
  timeline.addEventListener('pointerup', () => { timeline.onpointermove = null; });

  for (const tab of document.querySelectorAll('[role="tab"]')) {
    tab.onclick = () => {
      for (const t of document.querySelectorAll('[role="tab"]')) {
        t.setAttribute('aria-selected', String(t === tab));
        $(t.getAttribute('aria-controls')).hidden = t !== tab;
      }
      state.layersKey = null;
      renderLayers();
    };
  }

  const bindToggle = (id, key) => { $(id).checked = scene.show[key]; $(id).onchange = (e) => { scene.show[key] = e.target.checked; store.set('show', scene.show); }; };
  bindToggle('show-terrain', 'terrain');
  bindToggle('show-props', 'props');
  bindToggle('show-grid', 'grid');
  const bindSun = (id, key, digits) => {
    const input = $(id), out = $(`${id}-out`);
    input.value = scene.sun[key]; out.textContent = Number(scene.sun[key]).toFixed(digits);
    input.oninput = () => { scene.sun[key] = Number(input.value); out.textContent = Number(input.value).toFixed(digits); store.set('sun', scene.sun); };
  };
  bindSun('sun-angle', 'angle', 0);
  bindSun('sun-length', 'length', 2);
  bindSun('sun-strength', 'strength', 2);
  $('frame-effect').onclick = centreCamera;
  $('reset-cell').onclick = () => { state.cell = { ...RecordedCell }; store.set('cell', state.cell); centreCamera(); };
  setInterval(() => { $('camera-readout').textContent = `centre ${fmt(camera.cx, 1)}, ${fmt(camera.cz, 1)} · ${fmt(camera.ppc, 1)} px per cell · view ${fmt($('stage').clientHeight / camera.ppc, 1)} cells tall`; }, 300);

  let backdropImage = null;
  $('backdrop-file').onchange = (e) => {
    const file = e.target.files[0];
    if (!file) return;
    const img = new Image();
    img.onload = () => { backdropImage = img; scene.setBackdrop(img, Number($('backdrop-ppc').value), { x: state.cell.x + 0.5, z: state.cell.z + 0.5 }); };
    img.src = URL.createObjectURL(file);
  };
  $('backdrop-ppc').oninput = () => { if (backdropImage) scene.setBackdrop(backdropImage, Number($('backdrop-ppc').value), { x: state.cell.x + 0.5, z: state.cell.z + 0.5 }); };
  $('backdrop-clear').onclick = () => { scene.backdrop = null; backdropImage = null; $('backdrop-file').value = ''; };

  $('compare-on').onchange = async (e) => {
    state.compare = e.target.checked;
    const a = sourceA();
    if (state.compare && a?.kind === 'sketch' && a.module.compareWith) {
      const recorded = state.entries.find((x) => x.kind === 'recorded' && x.label === a.module.compareWith);
      if (recorded) { state.b = a.id; state.a = recorded.id; }
    }
    if (state.compare && !state.b) state.b = state.a;
    await ensureLoaded(state.a); await ensureLoaded(state.b);
    selectionChanged(false);
  };
  $('compare-a').onchange = async (e) => { if (e.target.value) { await ensureLoaded(e.target.value); state.a = e.target.value; selectionChanged(true); } };
  $('compare-b').onchange = async (e) => { if (e.target.value) { await ensureLoaded(e.target.value); state.b = e.target.value; state.compare = true; selectionChanged(true); } };
  $('compare-swap').onclick = () => { [state.a, state.b] = [state.b, state.a]; selectionChanged(false); };

  window.addEventListener('keydown', (e) => {
    if (e.target.closest?.('input, select, textarea')) return;
    const k = e.key.toLowerCase();
    if (k === ' ') { e.preventDefault(); $('play').click(); }
    else if (e.key === 'ArrowLeft') { e.preventDefault(); clock.step(e.shiftKey ? -10 : -1); }
    else if (e.key === 'ArrowRight') { e.preventDefault(); clock.step(e.shiftKey ? 10 : 1); }
    else if (e.key === 'Home') clock.seek(0);
    else if (k === 'g') { $('show-grid').checked = !scene.show.grid; $('show-grid').onchange({ target: $('show-grid') }); }
    else if (k === 'c') { $('compare-on').checked = !state.compare; $('compare-on').onchange({ target: $('compare-on') }); }
    else if (k === 'f') centreCamera();
  });
}
