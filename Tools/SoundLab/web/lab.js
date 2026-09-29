// RimArt Sound Lab. Plays clips the way RimWorld's SoundDefs do (Verse.Sound.SubSoundDef):
// volume is volumeRange / 100, pitch is Unity's AudioSource.pitch (speed and pitch together, the
// same as Web Audio's playbackRate), a folder grain plays one random clip from that folder and
// every folder under it, and startDelayRange delays the subSound.
"use strict";

const $ = (s, el = document) => el.querySelector(s);
const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
const CLIP_ROOT = "/Tools/SoundLab/clips/";
const SAFE = new Set(["Core", "Biotech", "RimArt"]);

const state = {
  tab: "abilities",
  search: "",
  sources: new Set(["Core", "Biotech", "Royalty", "Odyssey", "RimArt"]),
  onlyCandidates: true,
  clips: [],            // every clip: {source, folder, clip, url, dur, peakDb, attack, centroid, env}
  byFolder: new Map(),  // lower folder -> clips directly in it
  folders: [],          // [{key, folder, sources:Set, count}]
  vanillaDefs: [], modDefs: [], abilities: [],
  usedBy: new Map(),    // lower folder or clip path -> [{defName, volume, pitch}]
  candidates: {}, picks: {},
  selected: null,       // {kind:'ability'|'folder'|'def', id}
  target: null,         // {ability, moment} the Pick button writes to
  layers: [],
};

// ---------------------------------------------------------------- audio
const ctx = new AudioContext();
const master = ctx.createGain();
master.connect(ctx.destination);
const buffers = new Map();
const playing = new Set();

function loadBuffer(url) {
  if (!buffers.has(url)) {
    buffers.set(url, fetch(url).then((r) => {
      if (!r.ok) throw new Error(`${r.status} ${url}`);
      return r.arrayBuffer();
    }).then((b) => ctx.decodeAudioData(b)));
  }
  return buffers.get(url);
}

async function playUrl(url, { pitch = 1, volume = 50, delay = 0, loop = false } = {}) {
  if (ctx.state === "suspended") await ctx.resume();
  const buffer = await loadBuffer(url);
  const src = ctx.createBufferSource();
  src.buffer = buffer;
  src.playbackRate.value = pitch;
  src.loop = loop;
  const gain = ctx.createGain();
  gain.gain.value = volume / 100;
  src.connect(gain).connect(master);
  src.start(ctx.currentTime + delay);
  const voice = { src, gain };
  playing.add(voice);
  src.onended = () => playing.delete(voice);
  return voice;
}

// fade > 0 ramps down first, the way a sustainer's sustainFadeoutTime ends it in game.
function stopAll(fade = 0) {
  const end = ctx.currentTime + fade;
  for (const v of playing) {
    try {
      if (fade) { v.gain.gain.setValueAtTime(v.gain.gain.value, ctx.currentTime); v.gain.gain.linearRampToValueAtTime(0, end); }
      v.src.stop(end);
    } catch (e) { /* already stopped */ }
  }
  playing.clear();
}

const lower = (s) => (s || "").toLowerCase();

function clipsUnder(folder) {
  const f = lower(folder);
  const out = [];
  for (const [key, list] of state.byFolder) {
    if (key === f || key.startsWith(f + "/")) out.push(...list);
  }
  return out;
}

function clipByPath(path) {
  const p = lower(path);
  const cut = p.lastIndexOf("/");
  const list = state.byFolder.get(p.slice(0, cut)) || [];
  return list.find((c) => lower(c.clip) === p.slice(cut + 1)) || null;
}

// A layer or grain is {folder} or {clip: "Folder/Name"}. Returns the clips the game would choose from.
function resolve(grain) {
  if (grain.clip) { const c = clipByPath(grain.clip); return c ? [c] : []; }
  if (grain.folder) return clipsUnder(grain.folder);
  return [];
}

const pickOne = (list) => list[Math.floor(Math.random() * list.length)];
const inRange = (r) => r[0] + Math.random() * (r[1] - r[0]);

let cutTimer = null;

// duration: how long the effect lasts in game. The code ends the sound then, so the preview is
// cut there too, with a 0.2 s fade.
async function playLayers(layers, duration = null) {
  stopAll();
  clearTimeout(cutTimer);
  if (duration) cutTimer = setTimeout(() => stopAll(0.2), duration * 1000);
  const names = [];
  await Promise.all(layers.filter((l) => !l.mute).map((l) => {
    const c = pickOne(resolve(l));
    if (!c) return null;
    names.push(c.clip);
    return playUrl(c.url, { pitch: l.pitch, volume: l.volume, delay: l.delay, loop: l.loop });
  }));
  setNow(names.length ? "Playing " + names.join(" + ") + (duration ? ` (cut at ${duration} s)` : "") : "Nothing to play: no clips found");
}

async function playDef(def) {
  stopAll();
  const names = [];
  for (const sub of def.subs) {
    const grains = sub.grains.filter((g) => g.folder || g.clip);
    if (!grains.length) continue;
    const c = pickOne(resolve(pickOne(grains)));
    if (!c) continue;
    names.push(c.clip);
    playUrl(c.url, { pitch: inRange(sub.pitch), volume: inRange(sub.volume), delay: inRange(sub.delay), loop: def.sustain && sub.loop });
  }
  setNow(`${def.defName}: ${names.join(" + ") || "no clips found"}${def.sustain ? " (sustainer, Esc stops)" : ""}`);
}

function setNow(text) { $("#now").textContent = text; }

// ---------------------------------------------------------------- data
async function getJson(url, fallback) {
  try { const r = await fetch(url); return r.ok ? await r.json() : fallback; } catch (e) { return fallback; }
}

async function load() {
  const [index, cat, cands, picks] = await Promise.all([
    getJson(CLIP_ROOT + "index.json", null),
    getJson("/soundlab/catalog.json", null),
    getJson("/Tools/SoundLab/candidates.json", { abilities: {} }),
    getJson("/soundlab/picks.json", {}),
  ]);
  if (!cat) { $("#status").textContent = "serve.py is not running"; return; }
  const clips = (index ? index.clips : []).map((c) => ({ ...c, url: CLIP_ROOT + c.file.split("/").map(encodeURIComponent).join("/") }));
  for (const m of cat.modClips) clips.push({ ...m, env: null });
  state.clips = clips;
  state.byFolder = new Map();
  for (const c of clips) {
    const k = lower(c.folder);
    if (!state.byFolder.has(k)) state.byFolder.set(k, []);
    state.byFolder.get(k).push(c);
  }
  const folders = new Map();
  for (const c of clips) {
    const k = lower(c.folder);
    if (!folders.has(k)) folders.set(k, { key: k, folder: c.folder, sources: new Set(), count: 0 });
    const f = folders.get(k);
    f.sources.add(c.source); f.count++;
  }
  state.folders = [...folders.values()].sort((a, b) => a.key.localeCompare(b.key));
  state.vanillaDefs = cat.vanillaDefs;
  state.modDefs = cat.modDefs;
  state.abilities = cat.abilities;
  state.candidates = cands.abilities || {};
  state.picks = picks || {};
  state.usedBy = new Map();
  for (const d of [...cat.vanillaDefs, ...cat.modDefs]) {
    for (const s of d.subs) for (const g of s.grains) {
      const k = lower(g.folder || g.clip);
      if (!k) continue;
      if (!state.usedBy.has(k)) state.usedBy.set(k, []);
      state.usedBy.get(k).push({ defName: d.defName, source: d.source, volume: s.volume, pitch: s.pitch });
    }
  }
  const counts = index ? Object.entries(index.sources).map(([s, v]) => `${s} ${v.clips}`).join(", ") : "no clips: run extract.py";
  $("#status").textContent = `${clips.length} clips (${counts}) · catalog ${cat.built}`;
  renderChips();
  renderList();
  if (!state.selected) renderEmpty();
}

// ---------------------------------------------------------------- browser
function renderChips() {
  const chips = $("#chips");
  if (state.tab === "abilities") {
    chips.innerHTML = `<button class="chip" data-only aria-pressed="${state.onlyCandidates}">Has candidates</button>`;
  } else {
    chips.innerHTML = ["Core", "Biotech", "Royalty", "Odyssey", "RimArt"].map((s) =>
      `<button class="chip" data-src="${s}" aria-pressed="${state.sources.has(s)}" title="${SAFE.has(s) ? "always there: the mod needs Core and Biotech" : "needs MayRequire: only players with this DLC hear it"}">${s}</button>`).join("");
  }
}

function momentKey(ability, moment) { return `${ability}/${moment}`; }

function abilityStatus(defName) {
  const c = state.candidates[defName];
  if (!c) return "";
  const moments = c.moments || [];
  const picked = moments.filter((m) => state.picks[momentKey(defName, m.id)]?.layers).length;
  return picked === moments.length && moments.length ? "done" : "waiting";
}

function renderList() {
  const q = lower(state.search);
  const list = $("#list");
  let html = "";
  if (state.tab === "abilities") {
    let kit = null;
    const group = (a) => state.candidates[a.defName]?.hero || a.kit;
    const shown = state.abilities.filter((a) => !(state.onlyCandidates && !state.candidates[a.defName]))
      .filter((a) => !q || lower(a.label + " " + a.defName + " " + a.kit + " " + group(a)).includes(q))
      .sort((a, b) => group(a).localeCompare(group(b)));
    for (const a of shown) {
      if (group(a) !== kit) { kit = group(a); html += `<div class="group">${esc(kit)}</div>`; }
      const c = state.candidates[a.defName];
      const n = c ? c.moments.length : 0;
      const done = c ? c.moments.filter((m) => state.picks[momentKey(a.defName, m.id)]?.layers).length : 0;
      html += `<button class="row" data-kind="ability" data-id="${esc(a.defName)}" ${cur("ability", a.defName)}>
        <span class="dot ${abilityStatus(a.defName)}"></span><span class="name">${esc(a.label)}</span>
        ${n ? `<span class="count">${done}/${n}</span>` : ""}</button>`;
    }
    if (!html) html = `<div class="empty">No abilities with candidates match. Turn off "Has candidates" to see all ${state.abilities.length}.</div>`;
  } else if (state.tab === "folders") {
    let shown = 0;
    for (const f of state.folders) {
      if (![...f.sources].some((s) => state.sources.has(s))) continue;
      if (q && !folderMatches(f, q)) continue;
      shown++;
      html += `<button class="row" data-kind="folder" data-id="${esc(f.key)}" ${cur("folder", f.key)}>
        <span class="name" title="${esc(f.folder)}">${esc(f.folder || "(root)")}</span>
        ${[...f.sources].map((s) => `<span class="src ${s}">${s}</span>`).join("")}
        <span class="count">${f.count}</span></button>`;
      if (shown > 600) { html += `<div class="empty">Showing 600; search to narrow.</div>`; break; }
    }
  } else {
    const defs = [...state.modDefs, ...state.vanillaDefs];
    let shown = 0;
    for (const d of defs) {
      if (!state.sources.has(d.source)) continue;
      if (q && !defMatches(d, q)) continue;
      shown++;
      html += `<button class="row" data-kind="def" data-id="${esc(d.source + ":" + d.defName)}" ${cur("def", d.source + ":" + d.defName)}>
        <span class="name">${esc(d.defName)}</span>${d.sustain ? '<span class="count">loop</span>' : ""}<span class="src ${d.source}">${d.source}</span></button>`;
      if (shown > 600) { html += `<div class="empty">Showing 600; search to narrow.</div>`; break; }
    }
  }
  list.innerHTML = html;
}

function folderMatches(f, q) {
  if (f.key.includes(q)) return true;
  const users = state.usedBy.get(f.key) || [];
  if (users.some((u) => lower(u.defName).includes(q))) return true;
  return (state.byFolder.get(f.key) || []).some((c) => lower(c.clip).includes(q));
}

function defMatches(d, q) {
  return lower(d.defName).includes(q) || d.subs.some((s) => s.grains.some((g) => lower(g.folder || g.clip).includes(q)));
}

function cur(kind, id) {
  return state.selected && state.selected.kind === kind && state.selected.id === id ? 'aria-current="true"' : "";
}

// ---------------------------------------------------------------- detail views
function renderEmpty() {
  $("#detail").innerHTML = `<div class="empty">
    <p><b>Abilities</b>: each ability is split into moments (cast, travel, hit, loop). Every moment has a few candidates. Press ▶ to hear one, "Mixer" to tune its pitch, volume and timing, then "Pick". Picks are saved to <code>Tools/SoundLab/picks.json</code>, and Claude writes the SoundDefs from that file.</p>
    <p><b>Clips</b>: every clip the game has, by the folder a SoundDef names. A purple tag means a DLC the mod does not require, so only players with that DLC would hear it.</p>
    <p><b>SoundDefs</b>: every vanilla SoundDef, played the way the game plays it, with random pitch and volume inside its ranges.</p>
    <p>Space plays the mixer, Esc stops everything.</p></div>`;
}

function select(kind, id) {
  state.selected = { kind, id };
  renderList();
  if (kind === "ability") renderAbility(id);
  else if (kind === "folder") renderFolder(id);
  else renderDef(id);
}

function layerSummary(layers) {
  return layers.map((l) => `${l.folder || l.clip} ×${(+l.pitch).toFixed(2)} v${Math.round(l.volume)}${l.delay ? ` +${(+l.delay).toFixed(2)}s` : ""}${l.loop ? " loop" : ""}`).join("  |  ");
}

// A layer whose clips all come from a DLC the mod does not require needs MayRequire.
function dlcTags(layers) {
  const needs = new Set();
  for (const l of layers) {
    const sources = new Set(resolve(l).map((c) => c.source));
    if (sources.size && ![...sources].some((s) => SAFE.has(s))) sources.forEach((s) => needs.add(s));
  }
  return [...needs].map((s) => `<span class="src ${s}">${s}</span>`).join(" ");
}

function missingIn(layers) {
  return layers.filter((l) => resolve(l).length === 0).map((l) => l.folder || l.clip);
}

function renderAbility(defName) {
  const a = state.abilities.find((x) => x.defName === defName);
  const c = state.candidates[defName];
  let html = `<h1>${esc(a ? a.label : defName)}</h1>
    <div class="sub">${esc(a ? a.kit : "")} · <code>${esc(defName)}</code>${a && a.xmlSounds.length ? " · in XML now: " + esc(a.xmlSounds.join(", ")) : ""}</div>
    <p class="desc">${esc(a ? a.description.replace(/\\n/g, "\n") : "")}</p>`;
  if (c && c.note) html += `<p class="desc"><b>Plan:</b> ${esc(c.note)}</p>`;
  if (!c) html += `<div class="empty">No candidates written for this ability yet.</div>`;
  for (const m of (c ? c.moments : [])) {
    const key = momentKey(defName, m.id);
    const pick = state.picks[key];
    html += `<section class="card"><div class="card-head"><h3>${esc(m.label)}</h3><span class="when">${esc(m.when || "")}${m.duration ? ` · lasts ${m.duration} s in game` : ""}</span>
      ${pick?.layers ? '<span class="picked-tag">picked</span>' : ""}
      <button class="btn small" data-target="${esc(defName)}|${esc(m.id)}">Pick from mixer</button></div>`;
    m.options.forEach((o, i) => {
      const miss = missingIn(o.layers);
      const isPicked = pick?.layers && pick.option === o.label;
      html += `<div class="option ${isPicked ? "picked" : ""}">
        <button class="icon" title="Play" data-play-option="${esc(defName)}|${esc(m.id)}|${i}">▶</button>
        <div class="label">${esc(o.label)} ${dlcTags(o.layers)}
          <small title="${esc(layerSummary(o.layers))}">${esc(layerSummary(o.layers))}</small>
          ${miss.length ? `<span class="missing">missing: ${esc(miss.join(", "))}</span>` : ""}</div>
        <button class="btn small" data-mix-option="${esc(defName)}|${esc(m.id)}|${i}">Mixer</button>
        <button class="btn small" data-pick-option="${esc(defName)}|${esc(m.id)}|${i}">${isPicked ? "Picked" : "Pick"}</button></div>`;
    });
    if (pick?.layers && !m.options.some((o) => o.label === pick.option)) {
      html += `<div class="option picked"><button class="icon" title="Play" data-play-pick="${esc(key)}">▶</button>
        <div class="label">Your mix<small>${esc(layerSummary(pick.layers))}</small></div>
        <button class="btn small" data-mix-pick="${esc(key)}">Mixer</button>
        <button class="btn small" data-unpick="${esc(key)}">Unpick</button></div>`;
    } else if (pick?.layers) {
      html += `<div class="option"><div class="label"></div><button class="btn small" data-unpick="${esc(key)}">Unpick</button></div>`;
    }
    if (pick?.note) html += `<div class="user-note">Note: ${esc(pick.note)}</div>`;
    html += `</section>`;
  }
  $("#detail").innerHTML = html;
}

function waveSvg(env) {
  if (!env) return `<svg class="wave"></svg>`;
  const w = 150, h = 22, n = env.length;
  let d = "";
  env.forEach((v, i) => {
    const x = (i + 0.5) * (w / n), y = (v / 99) * (h / 2 - 1);
    d += `M${x.toFixed(1)} ${(h / 2 - y).toFixed(1)}V${(h / 2 + y + 0.5).toFixed(1)}`;
  });
  return `<svg class="wave" viewBox="0 0 ${w} ${h}"><path d="${d}" stroke="currentColor" stroke-width="2" opacity="0.7"/></svg>`;
}

function tone(c) {
  if (!c.centroid) return "";
  return c.centroid < 700 ? "dark" : c.centroid < 2200 ? "mid" : "bright";
}

function usedByLine(key) {
  const users = state.usedBy.get(key) || [];
  if (!users.length) return "";
  const shown = users.slice(0, 8).map((u) => `<code>${esc(u.defName)}</code> v${u.volume[0]}${u.volume[1] !== u.volume[0] ? "~" + u.volume[1] : ""} ×${u.pitch[0]}${u.pitch[1] !== u.pitch[0] ? "~" + u.pitch[1] : ""}`);
  return `<p class="usedby">Used by ${shown.join(", ")}${users.length > 8 ? ` and ${users.length - 8} more` : ""}</p>`;
}

function renderFolder(key) {
  const f = state.folders.find((x) => x.key === key);
  if (!f) return;
  const direct = state.byFolder.get(key) || [];
  const under = clipsUnder(f.folder);
  const needs = [...f.sources].filter((s) => !SAFE.has(s));
  let html = `<h1>${esc(f.folder)}</h1>
    <div class="sub">${[...f.sources].map((s) => `<span class="src ${s}">${s}</span>`).join(" ")} · ${direct.length} clips here${under.length > direct.length ? `, ${under.length} counting subfolders (a folder grain picks from all of them)` : ""}${needs.length ? ` · needs MayRequire="${needs.map((s) => "Ludeon.RimWorld." + s).join(",")}"` : ""}</div>
    ${usedByLine(key)}
    <div class="toolbar">
      <button class="btn" data-play-folder="${esc(f.folder)}">▶ Random clip, as a folder grain</button>
      <button class="btn" data-add-folder="${esc(f.folder)}">+ Folder as a layer</button>
    </div>`;
  for (const c of direct) {
    const path = (c.folder ? c.folder + "/" : "") + c.clip;
    html += `<div class="cliprow">
      <button class="icon" title="Play" data-play-clip="${esc(path)}">▶</button>
      <button class="icon" title="Add this clip as a layer" data-add-clip="${esc(path)}">+</button>
      ${waveSvg(c.env)}
      <span class="cname" title="${esc(c.clip)}">${esc(c.clip)} ${c.source !== "Core" ? `<span class="src ${c.source}">${c.source}</span>` : ""}</span>
      <span class="meta">${c.dur != null ? c.dur.toFixed(2) + " s" : ""} ${c.peakDb != null ? c.peakDb + " dB" : ""} ${tone(c)}</span></div>`;
  }
  $("#detail").innerHTML = html;
}

function findDef(id) {
  const [source, name] = id.split(/:(.*)/s);
  return [...state.modDefs, ...state.vanillaDefs].find((d) => d.source === source && d.defName === name);
}

function renderDef(id) {
  const d = findDef(id);
  if (!d) return;
  let html = `<h1>${esc(d.defName)}</h1>
    <div class="sub"><span class="src ${d.source}">${d.source}</span> · ${esc(d.file)}${d.sustain ? " · sustainer (loops until stopped)" : ""}</div>
    <div class="toolbar"><button class="btn primary" data-play-def="${esc(id)}">▶ Play as the game does</button>
    <button class="btn" data-mix-def="${esc(id)}">Load into mixer</button></div>`;
  d.subs.forEach((s, i) => {
    html += `<section class="card"><div class="card-head"><h3>subSound ${i + 1}</h3>
      <span class="when">volume ${s.volume.join("~")} · pitch ${s.pitch.join("~")}${s.delay[1] ? ` · delay ${s.delay.join("~")} s` : ""}${s.loop ? " · loop" : ""}${s.mayRequire ? ` · MayRequire ${esc(s.mayRequire)}` : ""}</span></div>`;
    for (const g of s.grains) {
      const p = g.folder || g.clip;
      const n = p ? resolve(g).length : 0;
      html += `<div class="option"><div class="label">${esc(g.folder ? "folder " + g.folder : g.clip ? "clip " + g.clip : g.other)}
        <small>${p ? n + " clips" : ""}</small></div>
        ${p ? `<button class="btn small" data-open-folder="${esc(lower(g.folder || g.clip.slice(0, g.clip.lastIndexOf("/"))))}">Open</button>` : ""}</div>`;
    }
    html += `</section>`;
  });
  $("#detail").innerHTML = html;
}

// ---------------------------------------------------------------- mixer
function newLayer(grain, extra = {}) {
  return { folder: grain.folder || null, clip: grain.clip || null, pitch: 1, volume: 50, delay: 0, loop: false, mute: false, ...extra };
}

function sliderHtml(i, key, label, min, max, step, value, fmt) {
  return `<label class="slider">${label}<input type="range" data-layer="${i}" data-key="${key}" min="${min}" max="${max}" step="${step}" value="${value}"><output>${fmt(value)}</output></label>`;
}

function renderMixer() {
  $("#layers").innerHTML = state.layers.map((l, i) => {
    const path = l.folder || l.clip;
    const sources = [...new Set(resolve(l).map((c) => c.source))];
    return `<div class="layer">
      <div class="layer-head"><button class="icon" title="Play this layer alone" data-solo="${i}">▶</button>
        <span class="name" title="${esc(path)}">${l.folder ? "📁 " : ""}${esc(path)}</span>
        ${sources.map((s) => `<span class="src ${s}">${s}</span>`).join("")}
        <button class="icon" title="Remove" data-remove="${i}">×</button></div>
      ${sliderHtml(i, "pitch", "pitch", 0.2, 3, 0.01, l.pitch, (v) => (+v).toFixed(2))}
      ${sliderHtml(i, "volume", "volume", 0, 150, 1, l.volume, (v) => Math.round(v))}
      ${sliderHtml(i, "delay", "delay", 0, 3, 0.01, l.delay, (v) => (+v).toFixed(2) + "s")}
      <div class="checks"><label><input type="checkbox" data-layer="${i}" data-key="loop" ${l.loop ? "checked" : ""}> loop (sustainer)</label>
        <label><input type="checkbox" data-layer="${i}" data-key="mute" ${l.mute ? "checked" : ""}> mute</label></div></div>`;
  }).join("");
  renderXml();
}

function fmtRange(v) { const s = (+v).toFixed(2).replace(/\.?0+$/, ""); return `${s}~${s}`; }

function renderXml() {
  const name = $("#defname").value.trim() || "AG_NewSound";
  const layers = state.layers.filter((l) => !l.mute);
  const duration = state.target ? targetDuration(state.target.ability, state.target.moment) : null;
  const sustain = layers.some((l) => l.loop) || !!duration;
  let xml = duration ? `<!-- A sustainer: the ability's code ends it after ${duration} s. -->\n` : "";
  xml += `<SoundDef>\n  <defName>${esc(name)}</defName>\n  <context>MapOnly</context>\n`;
  if (sustain) xml += `  <sustain>true</sustain>\n  <sustainFadeoutTime>0.2</sustainFadeoutTime>\n`;
  xml += `  <maxSimultaneous>4</maxSimultaneous>\n  <subSounds>\n`;
  for (const l of layers) {
    const needs = [...new Set(resolve(l).map((c) => c.source))];
    const may = needs.length && needs.every((s) => !SAFE.has(s)) ? ` MayRequire="${needs.map((s) => "Ludeon.RimWorld." + s).join(",")}"` : "";
    const grain = l.folder
      ? `<li Class="AudioGrain_Folder"><clipFolderPath>${esc(l.folder)}</clipFolderPath></li>`
      : `<li Class="AudioGrain_Clip"><clipPath>${esc(l.clip)}</clipPath></li>`;
    xml += `    <li${may}>\n      <grains>${grain}</grains>\n      <volumeRange>${fmtRange(l.volume)}</volumeRange>\n      <pitchRange>${fmtRange(l.pitch)}</pitchRange>\n`;
    if (+l.delay) xml += `      <startDelayRange>${fmtRange(l.delay)}</startDelayRange>\n`;
    if (l.loop) xml += `      <sustainLoop>true</sustainLoop>\n`;
    xml += `    </li>\n`;
  }
  xml += `  </subSounds>\n</SoundDef>`;
  $("#xml").textContent = xml;
}

function setTarget(ability, moment) {
  state.target = ability ? { ability, moment } : null;
  const t = $("#target");
  if (state.target) {
    const a = state.abilities.find((x) => x.defName === ability);
    const m = state.candidates[ability]?.moments.find((x) => x.id === moment);
    t.textContent = `Picking for: ${a ? a.label : ability} · ${m ? m.label : moment}`;
    t.classList.add("on");
    $("#defname").value = m?.defName || `${ability}_${moment}`.replace(/_(\w)/g, (_, c) => c.toUpperCase()).replace(/^AG(\w)/, "AG_$1");
    $("#note").value = state.picks[momentKey(ability, moment)]?.note || "";
  } else {
    t.textContent = "No ability moment selected";
    t.classList.remove("on");
  }
  $("#pick").disabled = !state.target;
  renderXml();
}

function optionOf(spec) {
  const [ability, moment, i] = spec.split("|");
  const m = state.candidates[ability].moments.find((x) => x.id === moment);
  return { ability, moment, option: m.options[+i], duration: m.duration || null };
}

const cloneLayers = (layers) => layers.map((l) => newLayer(l, { pitch: l.pitch ?? 1, volume: l.volume ?? 50, delay: l.delay ?? 0, loop: !!l.loop }));

async function savePick(ability, moment, layers, option) {
  const key = momentKey(ability, moment);
  const note = state.target && state.target.ability === ability && state.target.moment === moment ? $("#note").value.trim() : (state.picks[key]?.note || "");
  const pick = layers ? { option: option || null, layers: layers.map(({ mute, ...l }) => l), note } : null;
  const r = await fetch("/soundlab/pick", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ key, pick }) });
  const body = await r.json();
  if (body.picks) state.picks = body.picks;
  setNow(pick ? `Saved ${key} to picks.json` : `Removed ${key} from picks.json`);
  renderList();
  if (state.selected?.kind === "ability") renderAbility(state.selected.id);
}

// ---------------------------------------------------------------- events
document.addEventListener("click", async (e) => {
  const el = e.target.closest("button");
  if (!el) return;
  const d = el.dataset;
  if (d.tab) {
    state.tab = d.tab;
    document.querySelectorAll("[role=tab]").forEach((t) => t.setAttribute("aria-selected", t === el));
    renderChips(); renderList();
  } else if (d.src) {
    state.sources.has(d.src) ? state.sources.delete(d.src) : state.sources.add(d.src);
    renderChips(); renderList();
  } else if ("only" in d) {
    state.onlyCandidates = !state.onlyCandidates; renderChips(); renderList();
  } else if (d.kind) {
    select(d.kind, d.id);
  } else if (d.playOption) {
    const { option, duration } = optionOf(d.playOption);
    playLayers(option.layers.map((l) => newLayer(l, l)), duration);
  } else if (d.mixOption) {
    const { ability, moment, option } = optionOf(d.mixOption);
    state.layers = cloneLayers(option.layers); setTarget(ability, moment); renderMixer();
  } else if (d.pickOption) {
    const { ability, moment, option } = optionOf(d.pickOption);
    await savePick(ability, moment, cloneLayers(option.layers), option.label);
  } else if (d.playPick) {
    const [ability, moment] = d.playPick.split("/");
    playLayers(cloneLayers(state.picks[d.playPick].layers), targetDuration(ability, moment));
  } else if (d.mixPick) {
    const [ability, moment] = d.mixPick.split("/");
    state.layers = cloneLayers(state.picks[d.mixPick].layers); setTarget(ability, moment); renderMixer();
  } else if (d.unpick) {
    const [ability, moment] = d.unpick.split("/");
    await savePick(ability, moment, null);
  } else if (d.target) {
    const [ability, moment] = d.target.split("|"); setTarget(ability, moment);
  } else if (d.playFolder) {
    playLayers([newLayer({ folder: d.playFolder })]);
  } else if (d.addFolder) {
    state.layers.push(newLayer({ folder: d.addFolder })); renderMixer();
  } else if (d.playClip) {
    playLayers([newLayer({ clip: d.playClip })]);
  } else if (d.addClip) {
    state.layers.push(newLayer({ clip: d.addClip })); renderMixer();
  } else if (d.playDef) {
    playDef(findDef(d.playDef));
  } else if (d.mixDef) {
    const def = findDef(d.mixDef);
    state.layers = def.subs.filter((s) => s.grains.some((g) => g.folder || g.clip)).map((s) => {
      const g = s.grains.find((x) => x.folder || x.clip);
      return newLayer(g, { pitch: (s.pitch[0] + s.pitch[1]) / 2, volume: (s.volume[0] + s.volume[1]) / 2, delay: s.delay[0], loop: def.sustain && s.loop });
    });
    renderMixer();
  } else if (d.openFolder != null) {
    state.tab = "folders";
    document.querySelectorAll("[role=tab]").forEach((t) => t.setAttribute("aria-selected", t.dataset.tab === "folders"));
    renderChips(); select("folder", d.openFolder);
  } else if (d.solo) {
    playLayers([{ ...state.layers[+d.solo], mute: false }]);
  } else if (d.remove) {
    state.layers.splice(+d.remove, 1); renderMixer();
  }
});

document.addEventListener("input", (e) => {
  const el = e.target;
  if (el.id === "search") { state.search = el.value; renderList(); return; }
  if (el.id === "master") { master.gain.value = +el.value; return; }
  if (el.id === "defname") { renderXml(); return; }
  if (el.dataset.layer == null) return;
  const l = state.layers[+el.dataset.layer];
  if (el.type === "checkbox") l[el.dataset.key] = el.checked;
  else {
    l[el.dataset.key] = +el.value;
    el.nextElementSibling.textContent = el.dataset.key === "pitch" ? (+el.value).toFixed(2) : el.dataset.key === "delay" ? (+el.value).toFixed(2) + "s" : Math.round(el.value);
  }
  renderXml();
});

function targetDuration(ability, moment) {
  return state.candidates[ability]?.moments.find((x) => x.id === moment)?.duration || null;
}
const playMixer = () => playLayers(state.layers, state.target ? targetDuration(state.target.ability, state.target.moment) : null);
$("#play").onclick = playMixer;
$("#clear").onclick = () => { state.layers = []; renderMixer(); };
$("#stop-all").onclick = () => { stopAll(); setNow("Stopped"); };
$("#pick").onclick = () => state.target && savePick(state.target.ability, state.target.moment, state.layers, null);
$("#copy").onclick = async () => { await navigator.clipboard.writeText($("#xml").textContent); setNow("SoundDef XML copied"); };

document.addEventListener("keydown", (e) => {
  if (e.target.matches("input[type=search], input:not([type]), textarea, input[id=defname]")) {
    if (e.key === "Escape") e.target.blur();
    return;
  }
  if (e.code === "Space") { e.preventDefault(); playMixer(); }
  else if (e.key === "Escape") { stopAll(); setNow("Stopped"); }
});

renderMixer();
load();
// candidates.json and picks.json change while the page is open (Claude writes candidates); poll them.
setInterval(async () => {
  const [cands, picks] = await Promise.all([getJson("/Tools/SoundLab/candidates.json", null), getJson("/soundlab/picks.json", null)]);
  const changed = (cands && JSON.stringify(cands.abilities) !== JSON.stringify(state.candidates)) || (picks && JSON.stringify(picks) !== JSON.stringify(state.picks));
  if (!changed) return;
  if (cands) state.candidates = cands.abilities || {};
  if (picks) state.picks = picks;
  renderList();
  if (state.selected?.kind === "ability") renderAbility(state.selected.id);
}, 3000);
