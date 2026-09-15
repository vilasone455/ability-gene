// The map camera: a centre in cells, a zoom in px per cell, and RimWorld's camera shake.
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
