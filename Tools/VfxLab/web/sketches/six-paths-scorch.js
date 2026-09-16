// Truth-Seeking Scorch — reference-inspired piercing line-attack showcase.
// Based on the supplied mobile-game stills (black mass / purple edge), not a
// frame-accurate recreation of unseen motion. The orb charges, emits a straight
// beam, sustains fire, then cuts emission. The full beam thins/fades in place;
// nothing retracts into the orb. Impact starts when the beam front reaches target.
// Gameplay proposal: committed aim during windup, piercing damage on beam contact.
// Mannequins are optional lab props; no damage/status logic is implemented here.
// Ordinary strip meshes and shipped textures only. All animation is time-derived.
import {
  AltitudeLayer, Color, Graphics, MaterialPool, MaterialPropertyBlock, Mathf,
  Matrix4x4, Mesh, Meshes, MeshPool, Quaternion, ShaderDatabase, ShaderPropertyIDs, Vector3,
} from '../js/engine.js';

const ink = new Color(0.012, 0.009, 0.023);
const violet = new Color(0.56, 0.10, 0.96);
const rim = new Color(0.78, 0.31, 1);
const white = new Color(0.95, 0.76, 1);
const solid = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const edgeGlow = MaterialPool.MatFrom('white', ShaderDatabase.MoteGlow);
const glow = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.MoteGlow);
const soft = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent);
const disc = Meshes.disc(80, 'scorch orb');
const ring = Meshes.band(0.975, 1, 96, 'scorch pressure ring');
const scratch = new Map();
const mesh = key => scratch.get(key) ?? scratch.set(key, new Mesh(key)).get(key);
const props = new MaterialPropertyBlock();
const floor = AltitudeLayer.Filth.AltitudeFor();
const y = AltitudeLayer.MoteOverhead.AltitudeFor();
const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });
const source = -2;
function times(p) {
  const hit = p.charge + p.flight, cutoff = hit + p.hold, reform = cutoff + p.dissipate;
  return { hit, cutoff, reform, end: reform + p.settle };
}
function position(o, p, x, z) {
  const a = p.aim * Mathf.Deg2Rad;
  return new Vector3(o.x + x * Math.cos(a) - z * Math.sin(a), y,
    o.z + x * Math.sin(a) + z * Math.cos(a));
}
function paint(m, o, p, x, z, layer, w, h, c, mat = solid) {
  if (c.a <= 0 || w <= 0 || h <= 0) return;
  props.SetColor(ShaderPropertyIDs.Color, c);
  Graphics.DrawMesh(m, Matrix4x4.TRS(position(o, p, x, z).WithY(layer),
    Quaternion.Euler(0, -p.aim, 0), new Vector3(w, 1, h)), mat, 0, null, 0, props);
}
// Parallel strip edges keep the beam straight and constant in width.
function strip(key, points, width, o, p, layer, colour, material = solid) {
  const v = [], tri = [];
  for (let i = 0; i < points.length; i++) {
    const q = points[i], prev = points[Math.max(0, i - 1)], next = points[Math.min(points.length - 1, i + 1)];
    const dx = next.x - prev.x, dz = next.z - prev.z, len = Math.max(1e-6, Math.hypot(dx, dz));
    const w = width(q.u);
    v.push(q.x - dz / len * w, q.z + dx / len * w, q.x + dz / len * w, q.z - dx / len * w);
    if (i) { const k = i * 2; tri.push(k - 2, k, k - 1, k - 1, k, k + 1); }
  }
  const m = mesh(key); m.setFlat(v, tri);
  paint(m, o, p, 0, 0, layer, 1, 1, colour, material);
}
function burst(o, p, x, size, alpha, age) {
  const v = [0, 0], tri = [];
  for (let i = 0; i < 16; i++) {
    const a = i / 16 * Math.PI * 2 + 0.12 * Math.sin(age * 17);
    const r = i % 2 ? 0.24 : 0.54 + 0.46 * Math.sin(i * 7.1 + age * 24) ** 2;
    v.push(Math.cos(a) * r, Math.sin(a) * r);
    tri.push(0, i + 1, (i + 1) % 16 + 1);
  }
  const m = mesh('contact star'); m.setFlat(v, tri);
  paint(m, o, p, x, 0, y + 0.08, size * 1.09, size * 1.09, rim.withAlpha(alpha));
  paint(m, o, p, x, 0, y + 0.082, size, size, ink.withAlpha(alpha));
}
function actor(x, colour, o, p, alpha) {
  const at = position(o, p, x, 0), neutral = { aim: 0 }, origin = { x: at.x, z: at.z };
  const layer = AltitudeLayer.Pawn.AltitudeFor();
  paint(MeshPool.plane10, origin, neutral, 0, -0.1, floor, 0.85, 0.45, ink.withAlpha(alpha * 0.4), soft);
  paint(disc, origin, neutral, 0, 0.18, layer, 0.24, 0.32, colour.withAlpha(alpha));
  paint(disc, origin, neutral, 0, 0.58, layer + 0.002, 0.16, 0.17, new Color(0.8, 0.7, 0.56, alpha));
}

export default {
  kit: 'Six Paths', label: 'Truth-Seeking Scorch (sketch)',
  params: {
    charge: P('Orb swells / aim windup', 0.75, 0.3, 1.5, 0.05, 'Timing (s)'),
    flight: P('Beam reaches target', 0.09, 0.03, 0.25, 0.01, 'Timing (s)'),
    hold: P('Contact hold', 0.40, 0.1, 1.2, 0.05, 'Timing (s)'),
    dissipate: P('Beam dissipates after cutoff', 0.16, 0.06, 0.4, 0.01, 'Timing (s)'),
    settle: P('Orb settles', 0.45, 0.2, 1, 0.05, 'Timing (s)'),
    aim: P('Aim direction (degrees)', 90, 0, 360, 5, 'Shape'),
    range: P('Reach from orb (cells)', 7, 3.5, 7, 0.1, 'Shape'),
    orb: P('Charged orb radius (cells)', 0.6, 0.6, 1.3, 0.05, 'Shape'),
    beamWidth: P('Beam half-width (cells)', 0.26, 0.10, 0.45, 0.01, 'Shape'),
    glow: P('Purple aura strength', 0.70, 0, 1, 0.05, 'Feedback'),
    impact: P('Impact star radius (cells)', 0.75, 0.35, 1.2, 0.05, 'Feedback'),
    shake: P('Contact camera shake', 0.08, 0, 0.2, 0.01, 'Feedback'),
    actors: { label: 'Show caster and target', value: true, group: 'Showcase' },
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Charge / aim', t: 0 }, { name: 'Fire', t: p.charge },
      { name: 'Contact', t: t.hit }, { name: 'Cut emission', t: t.cutoff }, { name: 'Reformed', t: t.reform }];
  },
  events(p) { return p.shake ? [{ t: times(p).hit, type: 'shake', value: p.shake }] : []; },
  draw(s, p, { origin: o }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const alpha = smooth(s / 0.12) * (1 - smooth((s - t.reform) / p.settle));
    const charged = smooth(s / p.charge);
    const recovery = smooth((s - t.reform) / (p.settle * 0.6));
    const radius = lerp(0.28, p.orb, charged * (1 - recovery));
    const out = clamp((s - p.charge) / p.flight);
    const beamFade = 1 - smooth((s - t.cutoff) / p.dissipate);
    const muzzle = source + p.orb * 0.88;
    const length = (p.range - p.orb * 0.88) * out;
    const target = source + p.range;
    const flicker = 0.92 + 0.05 * Math.sin(s * 83) + 0.03 * Math.sin(s * 137);
    if (p.actors) {
      actor(source - 1.25, new Color(0.40, 0.58, 0.61), o, p, alpha);
      actor(target, new Color(0.67, 0.43, 0.28), o, p, alpha);
    }
    paint(MeshPool.plane10, o, p, source, 0, floor, radius * 3, radius * 1.25,
      ink.withAlpha(alpha * 0.35), soft);
    // Diffuse ground spill and aura stay under the opaque material.
    paint(MeshPool.plane10, o, p, source, 0, y - 0.02, radius * 4.2, radius * 4.2,
      violet.withAlpha(p.glow * alpha * 0.7), glow);
    // Concentric softening bands concentrate the aura at the silhouette, where a
    // soft disc alone would hide most of its light behind the opaque orb.
    for (let i = 0; i < 12; i++) {
      const r = radius * (1.025 + i * 0.025);
      paint(ring, o, p, source, 0, y - 0.018, r, r,
        violet.withAlpha(alpha * p.glow * 0.55 * (1 - i / 12) ** 2), edgeGlow);
    }
    if (s < p.charge) {
      const r = radius + (1 - charged) * 0.7;
      paint(ring, o, p, source, 0, y - 0.01, r, r, rim.withAlpha(charged * alpha * 0.5));
    }
    if (length > 0.001 && beamFade > 0) {
      const points = [{ u: 0, x: muzzle, z: 0 }, { u: 1, x: muzzle + length, z: 0 }];
      const w = p.beamWidth * beamFade;
      const beamAlpha = alpha * beamFade;
      paint(MeshPool.plane10, o, p, muzzle + length / 2, 0, floor + 0.01, length + 0.5, w * 7,
        violet.withAlpha(beamAlpha * p.glow * 0.45), glow);
      // Layered additive falloff outside a crisp black core, not a solid neon border.
      for (let i = 9; i >= 0; i--) {
        const spread = (0.025 + i * 0.025) * beamFade;
        strip(`beam halo ${i}`, points, () => w + spread, o, p, y,
          violet.withAlpha(beamAlpha * p.glow * flicker * 0.085 * (1 - i / 12)), edgeGlow);
      }
      strip('beam edge', points, () => w + 0.012 * beamFade, o, p, y + 0.002,
        rim.withAlpha(beamAlpha * 0.72));
      strip('beam core', points, () => w, o, p, y + 0.004, ink.withAlpha(beamAlpha));
      // Energy streaks travel only toward the target, along rigid parallel edges.
      for (let i = 0; i < 6; i++) {
        const u = Mathf.Repeat((s - p.charge) * 2.8 + i / 6, 1);
        const start = muzzle + length * u;
        const end = Math.min(muzzle + length, start + 0.38);
        const z = (i % 2 ? 1 : -1) * (w + 0.025);
        strip(`streak ${i}`, [{ u: 0, x: start, z }, { u: 1, x: end, z }],
          () => 0.013 * beamFade, o, p, y + 0.006, white.withAlpha(beamAlpha * p.glow * 0.65), edgeGlow);
        paint(MeshPool.plane10, o, p, (start + end) / 2, z, y + 0.007,
          Math.max(0.01, end - start) * 1.6, w * 1.3,
          rim.withAlpha(beamAlpha * p.glow * 0.28), glow);
      }
    }
    // The sphere remains the emitter; its silhouette does not stretch into the beam.
    paint(disc, o, p, source, 0, y + 0.02, radius + 0.035, radius + 0.035, rim.withAlpha(alpha));
    paint(disc, o, p, source, 0, y + 0.022, radius, radius, ink.withAlpha(alpha));
    paint(MeshPool.plane10, o, p, source - radius * 0.3, radius * 0.3, y + 0.024,
      radius * 1.2, radius * 1.2, violet.withAlpha(alpha * p.glow * 0.22), glow);
    // A broad, restrained crescent follows the sphere's curvature; the centre stays black.
    const crescent = Array.from({ length: 33 }, (_, i) => {
      const u = i / 32, a = 1.1 + u * 1.7 + 0.12 * Math.sin(s * 2);
      return { u, x: source + Math.cos(a) * radius * 0.86, z: Math.sin(a) * radius * 0.86 };
    });
    strip('orb crescent', crescent, u => radius * 0.065 * Math.sin(Math.PI * u), o, p, y + 0.026,
      rim.withAlpha(alpha * p.glow * 0.35), edgeGlow);
    if (s >= p.charge && s < t.reform) {
      paint(ring, o, p, muzzle, 0, y + 0.04, 0.11, p.beamWidth * 1.9,
        white.withAlpha(alpha * beamFade * p.glow * flicker), edgeGlow);
      paint(MeshPool.plane10, o, p, muzzle, 0, y + 0.042, 0.42, p.beamWidth * 4,
        white.withAlpha(alpha * beamFade * p.glow * flicker), glow);
      const flareLength = Math.min(length, 0.65);
      if (flareLength > 0.001) {
        const flare = [{ u: 0, x: muzzle, z: 0 }, { u: 1, x: muzzle + flareLength, z: 0 }];
        strip('muzzle flare', flare, u => p.beamWidth * 0.85 * (1 - u) * beamFade,
          o, p, y + 0.044, white.withAlpha(alpha * beamFade * p.glow * flicker * 0.65), edgeGlow);
        paint(MeshPool.plane10, o, p, muzzle + flareLength * 0.28, 0, y + 0.046,
          0.7, p.beamWidth * 2.7, rim.withAlpha(alpha * beamFade * p.glow * 0.5), glow);
      }
    }
    const age = s - t.hit;
    if (age >= 0) {
      const contact = beamFade;
      const pulse = 1 - clamp(age / 0.2);
      paint(MeshPool.plane10, o, p, target, 0, y + 0.07, p.impact * 3, p.impact * 3,
        violet.withAlpha(alpha * contact * p.glow * (0.45 + pulse * 0.4)), glow);
      burst(o, p, target, p.impact * (0.45 + pulse * 0.55), alpha * contact, age);
      paint(MeshPool.plane10, o, p, target, 0, y + 0.084, p.impact * 0.85, p.impact * 0.85,
        white.withAlpha(alpha * contact * p.glow * (0.35 + pulse * 0.5) * flicker), glow);
      // Fixed emission times make these outward sparks identical when scrubbing.
      // New particles stop at cutoff; already-emitted ones finish their short flight.
      for (let i = 0; i < 10; i++) {
        const cycle = 0.23, offset = i * 0.019;
        const birth = Math.floor((Math.min(age, p.hold - 0.00001) - offset) / cycle) * cycle + offset;
        const dt = age - birth;
        if (birth < 0 || dt < 0 || dt >= cycle) continue;
        const angle = -1.4 + (i / 9) * 2.8;
        const speed = 2.8 + (i % 3) * 0.7;
        const travel = speed * dt;
        const dx = Math.cos(angle), dz = Math.sin(angle);
        const x = target + dx * travel, z = dz * travel;
        const fade = (1 - dt / cycle) ** 2 * alpha * p.glow;
        strip(`contact spark ${i}`, [{ u: 0, x: x - dx * 0.13, z: z - dz * 0.13 },
          { u: 1, x, z }], u => 0.016 * (1 - u * 0.7), o, p, y + 0.10,
          white.withAlpha(fade), edgeGlow);
        paint(MeshPool.plane10, o, p, x, z, y + 0.102, 0.18, 0.18,
          rim.withAlpha(fade * 0.5), glow);
      }
      paint(ring, o, p, target, 0, floor + 0.02, 0.3 + age * 2, 0.3 + age * 2,
        rim.withAlpha(alpha * pulse * 0.55));
      paint(MeshPool.plane10, o, p, target, 0, y + 0.09, 0.22, p.impact * 2,
        white.withAlpha(alpha * pulse * p.glow), glow);
    }
  },
};
