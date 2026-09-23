// The map camera: a centre in cells, a zoom in px per cell, RimWorld's camera shake, and scripted
// camera moves for effects that take the camera over for a moment.
//
// Shake uses the game's own constants (Verse.CameraShaker, RimWorld 1.6): each DoShake adds its
// magnitude, the total is capped at 0.2 and decays by 0.5 per second, and the offset oscillates
// at 24 Hz. The waveform itself is private to the game, so the offset's shape is approximate;
// its size and how long it lasts are not.

const ShakeDecayRate = 0.5, ShakeFrequency = 24, MaxShakeMag = 0.2;

export function shakeAt(events, t) {
  let mag = 0, last = 0;
  for (const e of events) {
    if (e.type !== 'shake' || e.t > t) continue;
    mag = Math.max(0, mag - ShakeDecayRate * (e.t - last));
    mag = Math.min(MaxShakeMag, mag + e.value);
    last = e.t;
  }
  mag = Math.max(0, mag - ShakeDecayRate * (t - last));
  if (mag <= 0) return { x: 0, z: 0, mag: 0 };
  const phase = t * ShakeFrequency * Math.PI * 2;
  return { x: Math.sin(phase) * mag, z: Math.sin(phase * 1.31 + 1.7) * mag, mag };
}

// A scripted camera move, as a cutscene would drive CameraDriver's root position and size:
// { t, type: 'camera', over, zoom, pan, x, z }. From t, eased over `over` seconds, the view goes
// `pan` of the way from where the viewer left it (0) onto the point (x, z) (1), in cells from the
// effect's cell centre, and the viewer's zoom is multiplied by `zoom`. A move starts from wherever
// the one before it had got to, so a push and the return are two events; a field left out keeps
// its value. Returns { pan, zoom, x, z }; no events gives { pan: 0, zoom: 1 }, the viewer's camera.
export function cameraMoveAt(events, t) {
  const moves = events.filter((e) => e.type === 'camera' && e.t <= t).sort((a, b) => a.t - b.t);
  let at = { pan: 0, zoom: 1, x: 0, z: 0 };
  moves.forEach((e, i) => {
    const until = i + 1 < moves.length ? moves[i + 1].t : t;
    const u = Math.min(1, Math.max(0, (until - e.t) / Math.max(1e-6, e.over ?? 0))), k = u * u * (3 - 2 * u);
    const to = { pan: e.pan ?? at.pan, zoom: e.zoom ?? at.zoom, x: e.x ?? at.x, z: e.z ?? at.z };
    at = { pan: at.pan + (to.pan - at.pan) * k, zoom: at.zoom * Math.pow(to.zoom / at.zoom, k), x: at.x + (to.x - at.x) * k, z: at.z + (to.z - at.z) * k };
  });
  return at;
}

/** The viewer's camera with a move applied: centre and zoom, for the effect played on `cell`. */
export function movedView(camera, move, cell) {
  const fx = cell.x + 0.5 + move.x, fz = cell.z + 0.5 + move.z;
  return { cx: camera.cx + (fx - camera.cx) * move.pan, cz: camera.cz + (fz - camera.cz) * move.pan, ppc: camera.ppc * move.zoom };
}

export class Camera {
  constructor() {
    this.cx = 60.5;
    this.cz = 60.5;
    this.ppc = 46;
  }

  zoomAt(factor, worldX, worldZ) {
    const next = Math.min(180, Math.max(6, this.ppc * factor));
    const k = this.ppc / next;
    this.cx = worldX + (this.cx - worldX) * k;
    this.cz = worldZ + (this.cz - worldZ) * k;
    this.ppc = next;
  }

  /** CSS px inside a view rect to map cells. */
  toWorld(px, py, rect) {
    return {
      x: this.cx + (px - rect[0] - rect[2] / 2) / this.ppc,
      z: this.cz - (py - rect[1] - rect[3] / 2) / this.ppc,
    };
  }

  toScreen(x, z, rect) {
    return [rect[0] + rect[2] / 2 + (x - this.cx) * this.ppc, rect[1] + rect[3] / 2 - (z - this.cz) * this.ppc];
  }
}

/**
 * Drag to pan, wheel to zoom about the cursor, click without dragging to pick a cell.
 * `rectAt(px, py)` returns the view rect under the pointer, since compare mode has two.
 */
export function bindCamera(element, camera, { rectAt, onCell, onChange }) {
  let drag = null;
  element.addEventListener('pointerdown', (e) => {
    drag = { x: e.clientX, y: e.clientY, cx: camera.cx, cz: camera.cz, moved: false };
    element.setPointerCapture(e.pointerId);
  });
  element.addEventListener('pointermove', (e) => {
    if (!drag) return;
    const dx = e.clientX - drag.x, dy = e.clientY - drag.y;
    if (!drag.moved && Math.hypot(dx, dy) < 4) return;
    drag.moved = true;
    camera.cx = drag.cx - dx / camera.ppc;
    camera.cz = drag.cz + dy / camera.ppc;
    element.classList.add('panning');
    onChange();
  });
  element.addEventListener('pointerup', (e) => {
    element.classList.remove('panning');
    if (drag && !drag.moved && e.button === 0) {
      const box = element.getBoundingClientRect();
      const px = e.clientX - box.left, py = e.clientY - box.top;
      const rect = rectAt(px, py);
      if (rect) {
        const w = camera.toWorld(px, py, rect);
        onCell(Math.floor(w.x), Math.floor(w.z));
      }
    }
    drag = null;
  });
  element.addEventListener('wheel', (e) => {
    e.preventDefault();
    const box = element.getBoundingClientRect();
    const px = e.clientX - box.left, py = e.clientY - box.top;
    const rect = rectAt(px, py);
    if (!rect) return;
    const w = camera.toWorld(px, py, rect);
    camera.zoomAt(Math.exp(-e.deltaY * 0.0015), w.x, w.z);
    onChange();
  }, { passive: false });
}
