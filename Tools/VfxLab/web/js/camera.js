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

// ---- a 3D camera, for sketches that export camera() (SKETCHING.md) ----------------------------------------
// x east, y up, z north, in cells. pitch: degrees above the horizontal; yaw: degrees clockwise from north;
// fov: the vertical field of view in degrees.

/** The camera's right, up and forward directions. */
export function basis(cam) {
  const p = (cam.pitch ?? 0) * Math.PI / 180, y = (cam.yaw ?? 0) * Math.PI / 180;
  const cp = Math.cos(p), sp = Math.sin(p), cy = Math.cos(y), sy = Math.sin(y);
  return {
    right: { x: cy, y: 0, z: -sy },
    up: { x: -sy * sp, y: cp, z: -cy * sp },
    forward: { x: sy * cp, y: sp, z: cy * cp },
  };
}

/** Projection times view, column-major, for WebGL: a point ahead of the camera has w = its distance along forward. */
export function viewProjection(cam, aspect) {
  const { right: r, up: u, forward: f } = basis(cam), n = cam.near ?? .1, far = cam.far ?? 6000;
  const k = 1 / Math.tan((cam.fov ?? 60) * Math.PI / 360), c = { x: cam.x, y: cam.y, z: cam.z };
  const d = (a) => a.x * c.x + a.y * c.y + a.z * c.z;
  const A = (far + n) / (far - n), B = -2 * far * n / (far - n);
  const row = [
    [r.x * k / aspect, r.y * k / aspect, r.z * k / aspect, -d(r) * k / aspect],
    [u.x * k, u.y * k, u.z * k, -d(u) * k],
    [f.x * A, f.y * A, f.z * A, -d(f) * A + B],
    [f.x, f.y, f.z, -d(f)],
  ];
  const m = new Float32Array(16);
  for (let i = 0; i < 4; i++) for (let j = 0; j < 4; j++) m[j * 4 + i] = row[i][j];
  return m;
}

/** A draw call's matrix as Unity builds it: translate, then Euler rotation (z, then x, then y), then scale. */
export function modelMatrix(call) {
  const R = (deg) => deg * Math.PI / 180, a = R(call.rot ?? 0), b = R(call.rx ?? 0), g = R(call.rz ?? 0);
  const ca = Math.cos(a), sa = Math.sin(a), cb = Math.cos(b), sb = Math.sin(b), cg = Math.cos(g), sg = Math.sin(g);
  const mul = (P, Q) => P.map((row) => [0, 1, 2].map((j) => row[0] * Q[0][j] + row[1] * Q[1][j] + row[2] * Q[2][j]));
  const Ry = [[ca, 0, sa], [0, 1, 0], [-sa, 0, ca]], Rx = [[1, 0, 0], [0, cb, -sb], [0, sb, cb]], Rz = [[cg, -sg, 0], [sg, cg, 0], [0, 0, 1]];
  const M = mul(mul(Ry, Rx), Rz), S = [call.sx ?? 1, call.sy ?? 1, call.sz ?? 1], T = [call.x, call.y, call.z];
  const m = new Float32Array(16);
  for (let i = 0; i < 3; i++) { for (let j = 0; j < 3; j++) m[j * 4 + i] = M[i][j] * S[j]; m[12 + i] = T[i]; }
  m[15] = 1;
  return m;
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
 * `rectAt(px, py)` returns the view rect under the pointer, since compare mode has two, or null
 * where the map camera is not what is shown (a sketch's own camera): nothing happens there.
 */
export function bindCamera(element, camera, { rectAt, onCell, onChange }) {
  let drag = null;
  element.addEventListener('pointerdown', (e) => {
    const box = element.getBoundingClientRect();
    if (!rectAt(e.clientX - box.left, e.clientY - box.top)) return;
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
