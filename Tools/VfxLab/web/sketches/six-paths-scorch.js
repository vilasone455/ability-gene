// Truth-Seeking Scorch — reference-inspired piercing line-attack showcase.
// Based on the supplied mobile-game stills (black mass / purple edge), not a
// frame-accurate recreation of unseen motion. One orb swells (0.75 s), extrudes
// an attached lance (0.16 s), holds contact (0.40 s), retracts (0.40 s), settles
// back to its original size (0.45 s). Impact is timed to the tip reaching target.
// Gameplay proposal: committed aim during windup, piercing damage on extension.
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
  const hit = p.charge + p.extend, retract = hit + p.hold, reform = retract + p.retract;
  return { hit, retract, reform, end: reform + p.settle };
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
// A strip, rather than a polygon fan, preserves the concave silhouette of the lance.
function strip(key, points, width, o, p, layer, colour) {
  const v = [], tri = [];
  for (let i = 0; i < points.length; i++) {
    const q = points[i], prev = points[Math.max(0, i - 1)], next = points[Math.min(points.length - 1, i + 1)];
    const dx = next.x - prev.x, dz = next.z - prev.z, len = Math.max(1e-6, Math.hypot(dx, dz));
    const w = width(q.u);
    v.push(q.x - dz / len * w, q.z + dx / len * w, q.x + dz / len * w, q.z - dx / len * w);
    if (i) { const k = i * 2; tri.push(k - 2, k, k - 1, k - 1, k, k + 1); }
  }
  const m = mesh(key); m.setFlat(v, tri);
  paint(m, o, p, 0, 0, layer, 1, 1, colour);
}
function burst(o, p, x, size, alpha) {
  const v = [0, 0], tri = [];
  for (let i = 0; i < 24; i++) {
    const a = i / 24 * Math.PI * 2;
    const r = i % 2 ? 0.27 : 0.7 + 0.3 * Math.sin(i * 7.1) ** 2;
    v.push(Math.cos(a) * r, Math.sin(a) * r);
    tri.push(0, i + 1, (i + 1) % 24 + 1);
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
    extend: P('Lance extends', 0.16, 0.08, 0.5, 0.01, 'Timing (s)'),
    hold: P('Contact hold', 0.40, 0.1, 1.2, 0.05, 'Timing (s)'),
    retract: P('Lance retracts', 0.40, 0.15, 1, 0.05, 'Timing (s)'),
    settle: P('Orb settles', 0.45, 0.2, 1, 0.05, 'Timing (s)'),
    aim: P('Aim direction (degrees)', 180, 0, 360, 5, 'Shape'),
    range: P('Reach from orb (cells)', 5.4, 3.5, 7, 0.1, 'Shape'),
    orb: P('Charged orb radius (cells)', 0.95, 0.6, 1.3, 0.05, 'Shape'),
    width: P('Lance root half-width (cells)', 0.60, 0.3, 0.85, 0.05, 'Shape'),
    ripple: P('Edge undulation (cells)', 0.10, 0, 0.22, 0.01, 'Shape'),
    glow: P('Purple aura strength', 0.70, 0, 1, 0.05, 'Feedback'),
    impact: P('Impact star radius (cells)', 0.75, 0.35, 1.2, 0.05, 'Feedback'),
    shake: P('Contact camera shake', 0.08, 0, 0.2, 0.01, 'Feedback'),
    actors: { label: 'Show caster and target', value: true, group: 'Showcase' },
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Charge / aim', t: 0 }, { name: 'Pierce', t: p.charge },
      { name: 'Contact', t: t.hit }, { name: 'Retract', t: t.retract }, { name: 'Reformed', t: t.reform }];
  },
  events(p) { return p.shake ? [{ t: times(p).hit, type: 'shake', value: p.shake }] : []; },
  draw(s, p, { origin: o }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const alpha = smooth(s / 0.12) * (1 - smooth((s - t.reform) / p.settle));
    const charged = smooth(s / p.charge);
    const recovery = smooth((s - t.retract) / (p.retract + p.settle * 0.6));
    const radius = lerp(0.28, p.orb, charged * (1 - recovery));
    const out = 1 - (1 - clamp((s - p.charge) / p.extend)) ** 3;
    const back = smooth((s - t.retract) / p.retract);
    const length = p.range * out * (1 - back);
    const target = source + p.range;
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
    if (length > 0.01) {
      const points = Array.from({ length: 65 }, (_, i) => {
        const u = i / 64;
        return { u, x: source + length * u,
          z: Math.sin(u * 13 - s * 14) * Math.sin(u * Math.PI) * p.ripple * out };
      });
      const thickness = u => p.width * Math.pow(1 - u, 0.8) * Math.min(1, length / p.orb);
      paint(MeshPool.plane10, o, p, source + length * 0.5, 0, floor + 0.01, length + 1, p.width * 4,
        violet.withAlpha(alpha * p.glow * 0.32), glow);
      strip('lance aura', points, u => thickness(u) + 0.09 * Math.sin(Math.PI * u), o, p, y,
        violet.withAlpha(alpha * p.glow * 0.45));
      strip('lance edge', points, u => thickness(u) + 0.025 * (1 - u), o, p, y + 0.002, rim.withAlpha(alpha));
      strip('lance core', points, thickness, o, p, y + 0.004, ink.withAlpha(alpha));
      // Narrow filaments echo the references without replacing the black silhouette.
      for (let k = 0; k < 4; k++) {
        const sign = k % 2 ? 1 : -1;
        const trail = points.map(q => ({ u: q.u, x: q.x,
          z: q.z + sign * (thickness(q.u) + Math.sin(Math.PI * q.u) * (0.14 + k * 0.05)
            + p.ripple * Math.sin(q.u * 19 + s * 18 + k) * Math.sin(Math.PI * q.u)) }));
        strip(`filament ${k}`, trail, u => 0.013 * Math.sin(Math.PI * u), o, p, y + 0.006,
          (k < 2 ? rim : violet).withAlpha(alpha * p.glow * (k < 2 ? 0.7 : 0.4)));
      }
    }
    // Draw the orb over the lance root to hide the join and retain one continuous mass.
    paint(disc, o, p, source, 0, y + 0.02, radius + 0.035, radius + 0.035, rim.withAlpha(alpha));
    paint(disc, o, p, source, 0, y + 0.022, radius, radius, ink.withAlpha(alpha));
    paint(MeshPool.plane10, o, p, source - radius * 0.3, radius * 0.3, y + 0.024,
      radius * 1.2, radius * 1.2, violet.withAlpha(alpha * p.glow * 0.22), glow);
    const age = s - t.hit;
    if (age >= 0) {
      const contact = 1 - smooth((s - t.retract) / Math.min(0.18, p.retract));
      const pulse = 1 - clamp(age / 0.2);
      paint(MeshPool.plane10, o, p, target, 0, y + 0.07, p.impact * 3, p.impact * 3,
        violet.withAlpha(alpha * contact * p.glow * (0.45 + pulse * 0.4)), glow);
      burst(o, p, target, p.impact * (0.72 + pulse * 0.28), alpha * contact);
      paint(ring, o, p, target, 0, floor + 0.02, 0.3 + age * 2, 0.3 + age * 2,
        rim.withAlpha(alpha * pulse * 0.55));
      paint(MeshPool.plane10, o, p, target, 0, y + 0.09, 0.22, p.impact * 2,
        white.withAlpha(alpha * pulse * p.glow), glow);
    }
  },
};
