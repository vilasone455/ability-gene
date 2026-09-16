// Named parameter sets for a sketch, kept in this browser and movable as files.
//
// A preset is nothing but the sketch's own values, so it is small, readable and safe to paste
// into a commit message or a bug report. They live in localStorage per sketch file, which means
// they survive a reload and a re-record but never leave the machine on their own -- Export is
// how one gets shared, and Import is how it comes back.
//
// Nothing here knows what a parameter means. A preset saved before a slider was added simply
// lacks that key, and loading it leaves the missing slider at its current value.

const KEY = (file) => `vfxlab:presets:${file}`;

function read(file) {
  try {
    const raw = localStorage.getItem(KEY(file));
    const parsed = raw ? JSON.parse(raw) : null;
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

function write(file, presets) {
  try {
    localStorage.setItem(KEY(file), JSON.stringify(presets));
    return true;
  } catch {
    return false; // private window, or the quota is full
  }
}

/** Every preset saved for this sketch, newest name order aside, sorted by name. */
export function listPresets(file) {
  return Object.entries(read(file))
    .map(([name, values]) => ({ name, values }))
    .sort((a, b) => a.name.localeCompare(b.name));
}

export function savePreset(file, name, values) {
  const trimmed = name.trim();
  if (!trimmed) return false;
  const presets = read(file);
  presets[trimmed] = { ...values };
  return write(file, presets);
}

export function deletePreset(file, name) {
  const presets = read(file);
  delete presets[name];
  return write(file, presets);
}

/**
 * Only keys the sketch still has, coerced to the type its default has: a preset written by an
 * older version of a sketch, or edited by hand, cannot put a string where a number belongs.
 */
export function applyPreset(values, saved) {
  const out = { ...values };
  for (const [key, value] of Object.entries(saved ?? {})) {
    if (!(key in out)) continue;
    const current = out[key];
    if (typeof current === 'number') { const n = Number(value); if (Number.isFinite(n)) out[key] = n; }
    else if (typeof current === 'boolean') out[key] = value === true || value === 'true';
    else out[key] = String(value);
  }
  return out;
}

/** The file written by Export: one sketch's presets, or a single named one. */
export function presetFile(file, label, presets) {
  return JSON.stringify({ lab: 'RimArt VFX Lab presets', sketch: file, label, presets }, null, 2);
}

/**
 * Reads a file written by presetFile, or a bare { name: values } object, and merges it in.
 * Returns the names it took. A file for another sketch is refused rather than half-applied.
 */
export function importPresets(file, text) {
  let parsed;
  try { parsed = JSON.parse(text); } catch { throw new Error('That file is not JSON.'); }
  const presets = parsed?.presets ?? parsed;
  if (!presets || typeof presets !== 'object' || Array.isArray(presets)) throw new Error('No presets in that file.');
  if (parsed?.sketch && parsed.sketch !== file) throw new Error(`That file is for ${parsed.sketch}, not this sketch.`);
  const merged = read(file);
  const taken = [];
  for (const [name, values] of Object.entries(presets)) {
    if (!values || typeof values !== 'object' || Array.isArray(values)) continue;
    merged[name] = values;
    taken.push(name);
  }
  if (!taken.length) throw new Error('No presets in that file.');
  write(file, merged);
  return taken;
}
