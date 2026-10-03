// RimArt Sound Lab: the page. The audio, the clip catalog and the SoundDef XML are sound.js, which
// the VFX lab's sound markers use too; this file is the browser, the detail views and the mixer.
import {
  lib, SAFE, master, loadSounds, pollChanges, statusLine, stopAll, resolve, clipsUnder,
  playLayers as playVoices, playDef as playSoundDef, newLayer, cloneLayers, layersOfDef, layerSummary,
  sourcesOf, dlcNeeds, missingIn, defXml, savePick as writePick,
} from "./sound.js";

const $ = (s, el = document) => el.querySelector(s);
const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));

const state = {
  tab: "abilities",
  search: "",
  sources: new Set(["Core", "Biotech", "Royalty", "Odyssey", "RimArt"]),
  onlyCandidates: true,
  selected: null,       // {kind:'ability'|'folder'|'def', id}
  target: null,         // {ability, moment} the Pick button writes to
  layers: [],
};

// ---------------------------------------------------------------- audio
async function playLayers(layers, duration = null) {
  const names = await playVoices(layers, { duration });
  setNow(names.length ? "Playing " + names.join(" + ") + (duration ? ` (cut at ${duration} s)` : "") : "Nothing to play: no clips found");
}

function playDef(def) {
  const names = playSoundDef(def);
  setNow(`${def.defName}: ${names.join(" + ") || "no clips found"}${def.sustain ? " (sustainer, Esc stops)" : ""}`);
}

function setNow(text) { $("#now").textContent = text; }

const lower = (s) => (s || "").toLowerCase();

// ---------------------------------------------------------------- data
async function load() {
  await loadSounds();
  $("#status").textContent = statusLine();
  if (lib.error) return;
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
  const c = lib.candidates[defName];
  if (!c) return "";
  const moments = c.moments || [];
  const picked = moments.filter((m) => lib.picks[momentKey(defName, m.id)]?.layers).length;
  return picked === moments.length && moments.length ? "done" : "waiting";
}

function renderList() {
  const q = lower(state.search);
  const list = $("#list");
  let html = "";
  if (state.tab === "abilities") {
    let kit = null;
    const group = (a) => lib.candidates[a.defName]?.hero || a.kit;
    const shown = lib.abilities.filter((a) => !(state.onlyCandidates && !lib.candidates[a.defName]))
      .filter((a) => !q || lower(a.label + " " + a.defName + " " + a.kit + " " + group(a)).includes(q))
      .sort((a, b) => group(a).localeCompare(group(b)));
    for (const a of shown) {
      if (group(a) !== kit) { kit = group(a); html += `<div class="group">${esc(kit)}</div>`; }
      const c = lib.candidates[a.defName];
      const n = c ? c.moments.length : 0;
      const done = c ? c.moments.filter((m) => lib.picks[momentKey(a.defName, m.id)]?.layers).length : 0;
      html += `<button class="row" data-kind="ability" data-id="${esc(a.defName)}" ${cur("ability", a.defName)}>
        <span class="dot ${abilityStatus(a.defName)}"></span><span class="name">${esc(a.label)}</span>
        ${n ? `<span class="count">${done}/${n}</span>` : ""}</button>`;
    }
    if (!html) html = `<div class="empty">No abilities with candidates match. Turn off "Has candidates" to see all ${lib.abilities.length}.</div>`;
  } else if (state.tab === "folders") {
    let shown = 0;
    for (const f of lib.folders) {
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
    const defs = [...lib.modDefs, ...lib.vanillaDefs];
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
  const users = lib.usedBy.get(f.key) || [];
  if (users.some((u) => lower(u.defName).includes(q))) return true;
  return (lib.byFolder.get(f.key) || []).some((c) => lower(c.clip).includes(q));
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
    <p>The VFX lab (<code>Tools/VfxLab/lab.py</code>) plays these sounds on an effect's timeline: its Sound tab picks a sound for each marker while the effect plays.</p>
    <p>Space plays the mixer, Esc stops everything.</p></div>`;
}

function select(kind, id) {
  state.selected = { kind, id };
  renderList();
  if (kind === "ability") renderAbility(id);
  else if (kind === "folder") renderFolder(id);
  else renderDef(id);
}

const dlcTags = (layers) => dlcNeeds(layers).map((s) => `<span class="src ${s}">${s}</span>`).join(" ");

function renderAbility(defName) {
  const a = lib.abilities.find((x) => x.defName === defName);
  const c = lib.candidates[defName];
  let html = `<h1>${esc(a ? a.label : defName)}</h1>
    <div class="sub">${esc(a ? a.kit : "")} · <code>${esc(defName)}</code>${a && a.xmlSounds.length ? " · in XML now: " + esc(a.xmlSounds.join(", ")) : ""}</div>
    <p class="desc">${esc(a ? a.description.replace(/\\n/g, "\n") : "")}</p>`;
  if (c && c.note) html += `<p class="desc"><b>Plan:</b> ${esc(c.note)}</p>`;
  if (!c) html += `<div class="empty">No candidates written for this ability yet.</div>`;
  for (const m of (c ? c.moments : [])) {
    const key = momentKey(defName, m.id);
    const pick = lib.picks[key];
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
  const users = lib.usedBy.get(key) || [];
  if (!users.length) return "";
  const shown = users.slice(0, 8).map((u) => `<code>${esc(u.defName)}</code> v${u.volume[0]}${u.volume[1] !== u.volume[0] ? "~" + u.volume[1] : ""} ×${u.pitch[0]}${u.pitch[1] !== u.pitch[0] ? "~" + u.pitch[1] : ""}`);
  return `<p class="usedby">Used by ${shown.join(", ")}${users.length > 8 ? ` and ${users.length - 8} more` : ""}</p>`;
}

function renderFolder(key) {
  const f = lib.folders.find((x) => x.key === key);
  if (!f) return;
  const direct = lib.byFolder.get(key) || [];
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
  return [...lib.modDefs, ...lib.vanillaDefs].find((d) => d.source === source && d.defName === name);
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
function sliderHtml(i, key, label, min, max, step, value, fmt) {
  return `<label class="slider">${label}<input type="range" data-layer="${i}" data-key="${key}" min="${min}" max="${max}" step="${step}" value="${value}"><output>${fmt(value)}</output></label>`;
}

function renderMixer() {
  $("#layers").innerHTML = state.layers.map((l, i) => {
    const path = l.folder || l.clip;
    return `<div class="layer">
      <div class="layer-head"><button class="icon" title="Play this layer alone" data-solo="${i}">▶</button>
        <span class="name" title="${esc(path)}">${l.folder ? "📁 " : ""}${esc(path)}</span>
        ${sourcesOf(l).map((s) => `<span class="src ${s}">${s}</span>`).join("")}
        <button class="icon" title="Remove" data-remove="${i}">×</button></div>
      ${sliderHtml(i, "pitch", "pitch", 0.2, 3, 0.01, l.pitch, (v) => (+v).toFixed(2))}
      ${sliderHtml(i, "volume", "volume", 0, 150, 1, l.volume, (v) => Math.round(v))}
      ${sliderHtml(i, "delay", "delay", 0, 3, 0.01, l.delay, (v) => (+v).toFixed(2) + "s")}
      <div class="checks"><label><input type="checkbox" data-layer="${i}" data-key="loop" ${l.loop ? "checked" : ""}> loop (sustainer)</label>
        <label><input type="checkbox" data-layer="${i}" data-key="mute" ${l.mute ? "checked" : ""}> mute</label></div></div>`;
  }).join("");
  renderXml();
}

function renderXml() {
  const name = $("#defname").value.trim() || "AG_NewSound";
  const duration = state.target ? targetDuration(state.target.ability, state.target.moment) : null;
  $("#xml").textContent = defXml(name, state.layers, duration);
}

function setTarget(ability, moment) {
  state.target = ability ? { ability, moment } : null;
  const t = $("#target");
  if (state.target) {
    const a = lib.abilities.find((x) => x.defName === ability);
    const m = lib.candidates[ability]?.moments.find((x) => x.id === moment);
    t.textContent = `Picking for: ${a ? a.label : ability} · ${m ? m.label : moment}`;
    t.classList.add("on");
    $("#defname").value = m?.defName || `${ability}_${moment}`.replace(/_(\w)/g, (_, c) => c.toUpperCase()).replace(/^AG(\w)/, "AG_$1");
    $("#note").value = lib.picks[momentKey(ability, moment)]?.note || "";
  } else {
    t.textContent = "No ability moment selected";
    t.classList.remove("on");
  }
  $("#pick").disabled = !state.target;
  renderXml();
}

function optionOf(spec) {
  const [ability, moment, i] = spec.split("|");
  const m = lib.candidates[ability].moments.find((x) => x.id === moment);
  return { ability, moment, option: m.options[+i], duration: m.duration || null };
}

async function savePick(ability, moment, layers, option) {
  const key = momentKey(ability, moment);
  const note = state.target && state.target.ability === ability && state.target.moment === moment ? $("#note").value.trim() : (lib.picks[key]?.note || "");
  const pick = layers ? { option: option || null, layers: layers.map(({ mute, ...l }) => l), note } : null;
  await writePick(key, pick);
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
    playLayers(cloneLayers(lib.picks[d.playPick].layers), targetDuration(ability, moment));
  } else if (d.mixPick) {
    const [ability, moment] = d.mixPick.split("/");
    state.layers = cloneLayers(lib.picks[d.mixPick].layers); setTarget(ability, moment); renderMixer();
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
    state.layers = layersOfDef(findDef(d.mixDef));
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
  return lib.candidates[ability]?.moments.find((x) => x.id === moment)?.duration || null;
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
// candidates.json and picks.json change while the page is open (Claude writes candidates, the VFX
// lab writes picks); poll them.
setInterval(async () => {
  if (!(await pollChanges())) return;
  renderList();
  if (state.selected?.kind === "ability") renderAbility(state.selected.id);
}, 3000);
