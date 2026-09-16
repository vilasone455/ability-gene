// Black Serpent — proposed single-target restraint, not an in-game status effect.
// One orb wakes (0.40 s), feeds a ground tether toward a target (0.70 s), coils
// around the legs (0.55 s), holds taut (1.25 s), then unwinds/retracts (0.65 s)
// into the same orb (0.35 s). The catch pulse marks when restraint would begin;
// opening the coil marks when it ends. No damage burst or execution flash.
// The optional mannequin is a lab-only target at origin; caster/orb is to its left.
// Port the sampled strip meshes and shipped SoftDisc texture to C#, replacing the
// mannequin with the targeted pawn, and bind Hold/Release to the actual status.
import {
  AltitudeLayer, Color, Graphics, MaterialPool, MaterialPropertyBlock, Mathf,
  Matrix4x4, Mesh, Meshes, MeshPool, Quaternion, ShaderDatabase, ShaderPropertyIDs, Vector3,
} from '../js/engine.js';

const TAU = 2 * Math.PI;
const ink = new Color(0.025, 0.022, 0.036);
const edge = new Color(0.62, 0.53, 0.79);
const pale = new Color(0.87, 0.83, 0.96);
const solid = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const soft = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent);
const glow = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.MoteGlow);
const props = new MaterialPropertyBlock();
const disc = Meshes.disc(48, 'serpent orb');
const ring = Meshes.band(0.95, 1, 64, 'serpent catch');
const cache = new Map();
const meshFor = key => cache.get(key) ?? cache.set(key, new Mesh(key)).get(key);
const floor = AltitudeLayer.Filth.AltitudeFor();
const back = AltitudeLayer.Pawn.AltitudeFor() - 0.01;
const front = AltitudeLayer.MoteOverhead.AltitudeFor();
const pawn = AltitudeLayer.Pawn.AltitudeFor();
const smooth = Mathf.Smooth;
const clamp = Mathf.Clamp01;
const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });

function times(p) {
  const reach = p.wake + p.travel, catchAt = reach + p.wrap;
  const release = catchAt + p.hold, reform = release + p.return;
  return { reach, catchAt, release, reform, end: reform + p.settle };
}

function paint(m, o, x, z, y, w, h, c, mat = solid) {
  if (c.a <= 0 || w <= 0 || h <= 0) return;
  props.SetColor(ShaderPropertyIDs.Color, c);
  Graphics.DrawMesh(m, Matrix4x4.TRS(new Vector3(o.x + x, y, o.z + z),
    Quaternion.identity, new Vector3(w, 1, h)), mat, 0, null, 0, props);
}

// The tether meets the coil tangentially at its left edge. Wave amplitude falls
// to zero at both endpoints, so tightening never separates the two pieces.
function point(u, s, p, t) {
  const tightened = smooth((s - t.reach) / p.wrap);
  const r = p.radius * (1 - 0.22 * tightened);
  if (u <= 1) {
    const wave = p.wave * (1 - 0.92 * tightened);
    return { x: -p.range + (p.range - r) * u,
      z: Math.sin(u * TAU - (s - p.wake) * 5) * Math.sin(Math.PI * u) ** 2 * wave,
      front: false };
  }
  const v = u - 1, a = Math.PI + v * TAU * p.turns;
  return { x: Math.cos(a) * r, z: Math.sin(a) * r * 0.60 + v * p.rise,
    front: Math.sin(a) < 0 };
}

// Separate rim/core strips are built once per frame; no drawn mesh is mutated.
// Front/back coil triangles are partitioned so the target's legs sit inside it.
function ribbon(points, p, o, alpha) {
  const vertices = [], behind = [], ahead = [];
  for (let i = 0; i < points.length; i++) {
    const prev = points[Math.max(0, i - 1)], next = points[Math.min(points.length - 1, i + 1)];
    const dx = next.x - prev.x, dz = next.z - prev.z;
    const length = Math.max(0.00001, Math.hypot(dx, dz));
    const taper = 0.30 + 0.70 * smooth((points.length - 1 - i) / 7);
    const half = p.width * taper / 2;
    vertices.push(points[i].x - dz / length * half, points[i].z + dx / length * half,
      points[i].x + dz / length * half, points[i].z - dx / length * half);
    if (i) {
      const j = i * 2, tris = points[i].front ? ahead : behind;
      tris.push(j - 2, j, j - 1, j - 1, j, j + 1);
    }
  }
  for (const [key, triangles, layer] of [['back', behind, back], ['front', ahead, front]]) {
    if (!triangles.length) continue;
    const core = meshFor(key), outline = meshFor(key + '-rim');
    core.setFlat(vertices, triangles);
    const outer = vertices.slice();
    for (let i = 0; i < points.length; i++) {
      for (let side = 0; side < 2; side++) {
        const k = i * 4 + side * 2;
        const dx = vertices[k] - points[i].x, dz = vertices[k + 1] - points[i].z;
        const len = Math.max(0.00001, Math.hypot(dx, dz));
        outer[k] += dx / len * p.rim;
        outer[k + 1] += dz / len * p.rim;
      }
    }
    outline.setFlat(outer, triangles);
    paint(outline, o, 0, 0, layer, 1, 1, edge.withAlpha(alpha));
    paint(core, o, 0, 0, layer + 0.002, 1, 1, ink.withAlpha(alpha));
  }
}

export default {
  kit: 'Six Paths',
  label: 'Black Serpent (sketch)',
  params: {
    wake: P('Orb prepares', 0.40, 0.15, 1, 0.05, 'Timing (s)'),
    travel: P('Tether reaches target', 0.70, 0.25, 1.5, 0.05, 'Timing (s)'),
    wrap: P('Coil closes', 0.55, 0.2, 1.2, 0.05, 'Timing (s)'),
    hold: P('Restraint duration', 1.25, 0.3, 4, 0.05, 'Timing (s)'),
    return: P('Unwind and retract', 0.65, 0.25, 1.5, 0.05, 'Timing (s)'),
    settle: P('Orb settles', 0.35, 0.15, 1, 0.05, 'Timing (s)'),
    range: P('Target distance (cells)', 4.5, 2.5, 7, 0.1, 'Shape'),
    radius: P('Coil radius (cells)', 0.72, 0.5, 1.1, 0.02, 'Shape'),
    turns: P('Coil turns', 2.25, 1.5, 3, 0.25, 'Shape'),
    rise: P('Coil rise (projected cells)', 0.55, 0.25, 0.8, 0.05, 'Shape'),
    width: P('Ribbon width (cells)', 0.16, 0.08, 0.26, 0.01, 'Shape'),
    rim: P('Pale edge width (cells)', 0.018, 0.008, 0.04, 0.002, 'Shape'),
    wave: P('Travel undulation (cells)', 0.45, 0, 0.8, 0.05, 'Shape'),
    pulse: P('Catch pulse brightness', 0.5, 0, 1, 0.05, 'Feedback'),
    target: { label: 'Show target mannequin', value: true, group: 'Showcase' },
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Prepare', t: 0 }, { name: 'Seek', t: p.wake },
      { name: 'Wrap', t: t.reach }, { name: 'Restrained', t: t.catchAt },
      { name: 'Release', t: t.release }, { name: 'Reformed', t: t.reform }];
  },
  events() { return []; }, // A restraint catches; it does not explode or shake the map.
  draw(s, p, { origin: o }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const stage = smooth(s / 0.15) * (1 - smooth((s - t.reform) / p.settle));
    const opening = clamp((s - t.release) / p.return);
    let extent = s < t.reach ? smooth((s - p.wake) / p.travel)
      : 1 + smooth((s - t.reach) / p.wrap);
    if (s >= t.release) extent = 2 * (1 - smooth(opening));
    const bound = s >= t.catchAt && s < t.release;

    // A simple, optional stand-in makes the restraint's location/function visible.
    // Torso/head remain readable above the bands; the target is never an effect texture.
    if (p.target) {
      const reaction = bound ? Math.sin((s - t.catchAt) * 13) * 0.025 : 0;
      paint(MeshPool.plane10, o, 0, -0.12, floor, 1.15, 0.6, ink.withAlpha(stage * 0.4), soft);
      for (const x of [-0.13, 0.13])
        paint(disc, o, x, 0.08, pawn, 0.105, 0.26, new Color(0.19, 0.21, 0.23, stage));
      paint(disc, o, reaction, 0.43, pawn + 0.002, 0.29, 0.36, new Color(0.60, 0.43, 0.28, stage));
      paint(disc, o, reaction, 0.88, pawn + 0.004, 0.19, 0.20, new Color(0.80, 0.69, 0.53, stage));
    }
    // Orb shrinks as it feeds the ribbon, and gains its mass back during recall.
    const orb = 0.32 * Math.sqrt(Math.max(0.08, 1 - extent / 2));
    paint(MeshPool.plane10, o, -p.range, 0, floor, 0.95, 0.5, ink.withAlpha(stage * 0.4), soft);
    paint(disc, o, -p.range, 0, front + 0.01, orb + p.rim, orb + p.rim, edge.withAlpha(stage));
    paint(disc, o, -p.range, 0, front + 0.012, orb, orb, ink.withAlpha(stage));

    if (extent > 0.001) {
      const n = Math.ceil(160 * extent / 2), points = [];
      for (let i = 0; i <= n; i++) points.push(point(extent * i / n, s, p, t));
      ribbon(points, p, o, stage);
      const tip = points.at(-1);
      // A restrained edge glint is the active-status cue; the body stays black.
      const strength = bound ? 0.20 + 0.07 * Math.sin((s - t.catchAt) * 5) : 0.14;
      paint(MeshPool.plane10, o, tip.x, tip.z, front + 0.02, 0.3, 0.3,
        pale.withAlpha(strength * stage), glow);
    }
    const age = s - t.catchAt;
    if (age >= 0 && age < 0.35) {
      const f = 1 - age / 0.35;
      paint(ring, o, 0, 0.1, floor + 0.01, p.radius + age * 1.5, (p.radius + age * 1.5) * 0.6,
        pale.withAlpha(f * p.pulse));
    }
    // At release the top of the coil peels away first; it is gone before recall ends.
    // Nothing flashes at release, so it cannot be mistaken for a second hit.
  },
};
