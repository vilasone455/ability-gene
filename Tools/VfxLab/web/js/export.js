// Frame export: an effect written out as images, so a run can be flipped through, dropped into a
// document, or used as reference while the C# is written.
//
// Frames are sampled from the effect's own clock rather than captured from playback, so the
// output does not depend on the machine keeping up: ask for 12 frames of a 4.6 s effect and you
// get exactly the poses at 0, 0.383, 0.767 ... seconds. That also means a sketch's frames come
// out identical between runs, because a sketch is a pure function of time and its params.
//
// Four shapes of output:
//   sheet      one PNG, frames in a grid, for looking at a whole run at a glance
//   strip      the same, in a single row
//   overlay    every frame stacked into one frame-sized picture, a strobe photo of the motion
//   sequence   one PNG per frame, numbered, for anything that wants them separately
//
// An overlay only works on transparent frames: a frame drawn on opaque terrain would hide the
// ones under it, so the terrain is drawn once as a backdrop and the effect stacked over it.
//
// A transparent export drops the generated terrain, trees and pawn and clears to nothing, so
// what lands in the file is the effect alone. That is the one to use as reference for art; the
// scene background is the one to use for judging how an effect reads on a map.
//
// The renderer keeps its drawing buffer, so each frame is read back with toDataURL immediately
// after it is drawn. The caller must hold the page's own draw loop still while this runs, or the
// next animation frame overwrites the buffer before it is read.

/** Times the frames are taken at: evenly spaced across the range, the end left open for a loop. */
export function frameTimes(count, from, to) {
  const n = Math.max(1, Math.round(count));
  const span = Math.max(0, to - from);
  if (n === 1 || span === 0) return [from];
  const step = span / n;
  return Array.from({ length: n }, (_, i) => from + i * step);
}

export function sheetColumns(count, columns) {
  if (columns > 0) return Math.min(Math.round(columns), count);
  return Math.min(count, Math.max(1, Math.ceil(Math.sqrt(count))));
}

const slug = (text) => text.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
const pad = (n, width) => String(n).padStart(width, '0');

/**
 * Draws every frame and returns them as PNG data URLs, in order.
 *
 * settings: { frames, from, to, px, cells, north, transparent }
 */
export function renderFrames({ renderer, scene, camera, source, cell, hidden }, settings) {
  const times = frameTimes(settings.frames, settings.from, settings.to);
  const px = Math.max(16, Math.round(settings.px));
  const cells = Math.max(1, settings.cells);
  const ppc = px / cells;
  // Centred on the effect's cell, pushed north because height is drawn as a northward offset:
  // the taller the effect, the further up the screen its top sits.
  const centre = { cx: cell.x + 0.5, cz: cell.z + 0.5 + settings.north };
  const view = Math.max(px, 1) / ppc / 2;

  const shots = [];
  for (const t of times) {
    const frame = source.frameAt(Math.min(t, source.duration || t), cell, scene, { ...centre, ppc, halfW: view, halfH: view });
    const calls = settings.transparent || source.ownMap
      ? frame.calls
      : scene.calls({ x: centre.cx, z: centre.cz }, view).concat(frame.calls);
    renderer.renderExport({ camera: { ...centre, ppc }, calls, hidden }, px, px,
      { transparent: settings.transparent });
    shots.push(renderer.canvas.toDataURL('image/png'));
  }
  return { times, shots, px, ppc };
}

const load = (url) => new Promise((resolve, reject) => {
  const image = new Image();
  image.onload = () => resolve(image);
  image.onerror = () => reject(new Error('A frame could not be read back.'));
  image.src = url;
});

/** The frames tiled into one PNG, returned as a data URL. */
export async function composeSheet(shots, px, columns) {
  const cols = sheetColumns(shots.length, columns);
  const rows = Math.ceil(shots.length / cols);
  const canvas = document.createElement('canvas');
  canvas.width = cols * px;
  canvas.height = rows * px;
  const ctx = canvas.getContext('2d');
  const images = await Promise.all(shots.map(load));
  images.forEach((image, i) => ctx.drawImage(image, (i % cols) * px, Math.floor(i / cols) * px, px, px));
  return { url: canvas.toDataURL('image/png'), cols, rows, width: canvas.width, height: canvas.height };
}

/**
 * The scene alone at the export's framing, with nothing of the effect in it: the bed an overlay
 * is stacked on, so the terrain is drawn once instead of once per frame.
 */
export function renderBackdrop({ renderer, scene, source, cell }, settings) {
  const px = Math.max(16, Math.round(settings.px));
  const ppc = px / Math.max(1, settings.cells);
  const centre = { cx: cell.x + 0.5, cz: cell.z + 0.5 + settings.north };
  const calls = source?.ownMap ? [] : scene.calls({ x: centre.cx, z: centre.cz }, px / ppc / 2);
  renderer.renderExport({ camera: { ...centre, ppc }, calls, hidden: new Set() },
    px, px, { transparent: false });
  return renderer.canvas.toDataURL('image/png');
}

/**
 * Every frame in one picture, the size of a single frame. Older frames are drawn fainter so the
 * motion reads in order rather than as a pile; the newest is at full strength.
 */
export async function composeOverlay(shots, px, { backdrop = null, oldest = 0.28 } = {}) {
  const canvas = document.createElement('canvas');
  canvas.width = canvas.height = px;
  const ctx = canvas.getContext('2d');
  if (backdrop) ctx.drawImage(await load(backdrop), 0, 0, px, px);
  const images = await Promise.all(shots.map(load));
  images.forEach((image, i) => {
    ctx.globalAlpha = images.length > 1 ? oldest + (1 - oldest) * (i / (images.length - 1)) : 1;
    ctx.drawImage(image, 0, 0, px, px);
  });
  ctx.globalAlpha = 1;
  return { url: canvas.toDataURL('image/png'), cols: 1, rows: 1, width: px, height: px };
}

/** A text file as a URL, so presets and manifests download the same way pictures do. */
export function textUrl(text) {
  return `data:application/json;charset=utf-8,${encodeURIComponent(text)}`;
}

export function download(url, name) {
  const link = document.createElement('a');
  link.href = url;
  link.download = name;
  link.rel = 'noopener';
  document.body.append(link);
  link.click();
  link.remove();
}

/** What the frames were, as a file: enough to reproduce the export and to port the numbers. */
export function manifest({ source, settings, times, ppc }) {
  return JSON.stringify({
    lab: 'RimArt VFX Lab frames',
    effect: source.label,
    kind: source.kind,
    duration: Number((source.duration ?? 0).toFixed(4)),
    frames: times.length,
    fps: settings.to > settings.from ? Number((times.length / (settings.to - settings.from)).toFixed(3)) : 0,
    from: settings.from,
    to: settings.to,
    times: times.map((t) => Number(t.toFixed(4))),
    pixels: Math.round(settings.px),
    cellsAcross: settings.cells,
    pixelsPerCell: Number(ppc.toFixed(3)),
    northOffset: settings.north,
    background: settings.transparent ? 'transparent' : 'generated scene',
    layout: settings.layout ?? 'sheet',
    params: source.kind === 'sketch' ? source.values : undefined,
  }, null, 2);
}

export function baseName(source, settings) {
  return `${slug(source.label)}-${Math.round(settings.frames)}f-${Math.round(settings.px)}px`;
}

export function frameName(source, settings, index, count) {
  return `${slug(source.label)}-${pad(index + 1, String(count).length)}.png`;
}
