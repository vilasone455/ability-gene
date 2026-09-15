// Sources and the clock. A source turns a time into a frame of draw calls; the player does not
// know or care whether those came from the mod's recorded C# or from a sketch.
//
//   frame = { calls: [{ group, mesh, mat, x, y, z, rot, sx, sz, r, g, b, a, age }], index, clock }

import { Graphics, Vector3 } from './engine.js';

/** The cell every recording is taken on (Engine/Verse.cs, UI.MouseCell). */
export const RecordedCell = { x: 60, z: 60 };

const groupOf = (mat) => `${mat.tex} · ${mat.shader}`;

export class RecordedSource {
  static async load(entry) {
    const response = await fetch(`../recordings/${entry.file}`, { cache: 'no-store' });
    if (!response.ok) throw new Error(`${entry.file}: ${response.status}`);
    return new RecordedSource(entry, await response.json());
  }

  constructor(entry, json) {
    this.kind = 'recorded';
    this.id = `recorded:${entry.file}`;
    this.file = entry.file;
    this.label = json.label;
    this.kit = json.kit;
    this.still = json.still;
    this.fps = json.fps;
    this.frames = json.frames;
    this.duration = json.still ? 0 : json.frames.length / json.fps;
    this.phases = json.phases;
    this.materials = json.materials;
    this.meshes = {};
    for (const [key, m] of Object.entries(json.meshes))
      this.meshes[key] = {
        name: m.name, version: 0,
        v: Float32Array.from(m.v), uv: m.uv ? Float32Array.from(m.uv) : null, tri: Uint32Array.from(m.tri),
      };
    this.events = [];
    for (const f of json.frames) for (const e of f.events ?? []) this.events.push({ ...e, t: f.t, clock: f.clock });
  }

  frameAt(t, origin) {
    // Frame i is stamped (i + 1) / fps, the time it shows; before the first frame, show the first.
    const index = this.still ? 0 : Math.max(0, Math.min(this.frames.length - 1, Math.round(t * this.fps) - 1));
    const frame = this.frames[index];
    const dx = origin.x - RecordedCell.x, dz = origin.z - RecordedCell.z;
    const calls = frame.calls.map((c) => {
      const mat = this.materials[c[1]];
      return {
        group: groupOf(mat), mesh: this.meshes[c[0]], mat,
        x: c[2] + dx, y: c[3], z: c[4] + dz, rot: c[5], sx: c[6], sz: c[7],
        r: c[8], g: c[9], b: c[10], a: c[11], age: c[12] ?? 0,
      };
    });
    return { calls, index, clock: frame.clock, frames: this.frames.length };
  }
}

export class SketchSource {
  constructor(module, file) {
    this.kind = 'sketch';
    this.id = `sketch:${file}`;
    this.file = file;
    this.module = module;
    this.label = module.label;
    this.kit = module.kit;
    this.still = false;
    this.values = SketchSource.defaults(module);
  }

  static defaults(module) {
    return Object.fromEntries(Object.entries(module.params).map(([k, p]) => [k, p.value]));
  }

  get duration() { return this.module.duration(this.values); }
  get phases() { return this.module.phases(this.values); }
  get events() { return this.module.events?.(this.values) ?? []; }

  frameAt(t, origin, scene) {
    Graphics.beginFrame();
    this.module.draw(t, this.values, { origin: new Vector3(origin.x + 0.5, 0, origin.z + 0.5), scene });
    const frame = Graphics.endFrame();
    for (const c of frame.calls) c.group ??= groupOf(c.mat);
    return { calls: frame.calls, index: Math.floor(t * 60), clock: t, frames: Math.ceil(this.duration * 60) };
  }
}

export class Clock {
  constructor() {
    this.t = 0;
    this.playing = true;
    this.speed = 1;
    this.loop = true;
    this.duration = 0;
  }

  tick(dt) {
    if (!this.playing || this.duration <= 0) return;
    this.t += dt * this.speed;
    if (this.t > this.duration) {
      if (this.loop) this.t = 0;
      else { this.t = this.duration; this.playing = false; }
    }
  }

  seek(t) { this.t = Math.max(0, Math.min(this.duration, t)); }
  step(frames) { this.playing = false; this.seek(Math.round(this.t * 60 + frames) / 60); }
}
