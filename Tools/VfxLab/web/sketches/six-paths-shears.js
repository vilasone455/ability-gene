// Obsidian Shears — delayed single-target burst proposal, not gameplay damage logic.
// Two orbs leave the caster, flank a moving target, and become crescent blades.
// When formation completes the location locks. A short warning precedes a fast
// crossing cut. The blades reform and return; both orbs are occupied until then.
// Hit/dodge modes share exactly the same blade paths and locked location. Dodge
// moves the mannequin out during windup, suppressing hit flash/shake, not the cut.
// Default: deploy .45, form .50, warning .45, crossing .18, recover .15,
// reform .40, return .50, settle .25 seconds. Impact occurs halfway through crossing.
// Mesh strips keep the crescent's hollow inner edge; all motion derives from time.
import {
  AltitudeLayer, Color, Graphics, MaterialPool, MaterialPropertyBlock, Mathf,
  Matrix4x4, Mesh, Meshes, MeshPool, Quaternion, ShaderDatabase, ShaderPropertyIDs, Vector3,
} from '../js/engine.js';

const ink = new Color(0.025, 0.019, 0.037);
const edge = new Color(0.63, 0.42, 0.86);
const pale = new Color(0.92, 0.82, 1);
const solid = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const additive = MaterialPool.MatFrom('white', ShaderDatabase.MoteGlow);
const glow = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.MoteGlow);
const soft = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent);
const disc = Meshes.disc(48, 'shears orb');
const ring = Meshes.band(0.97, 1, 72, 'shears locked area');
const slash = new Mesh('shears crossing cut');
slash.setFlat([-0.5, 0, 0, 0.5, 0.5, 0, 0, -0.5], [0, 1, 2, 0, 2, 3]);
const cache = new Map();
const mesh = key => cache.get(key) ?? cache.set(key, new Mesh(key)).get(key);
const props = new MaterialPropertyBlock();
const y = AltitudeLayer.MoteOverhead.AltitudeFor();
const floor = AltitudeLayer.Filth.AltitudeFor();
const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });

function times(p) {
  const lock = p.deploy + p.form, snap = lock + p.warning;
  const hit = snap + p.snap / 2, crossed = snap + p.snap;
  const reform = crossed + p.recover, recall = reform + p.reform;
  const home = recall + p.return;
  return { lock, snap, hit, crossed, reform, recall, home, end: home + 0.25 };
}
function paint(m, o, x, z, layer, w, h, angle, colour, mat = solid) {
  if (colour.a <= 0 || w <= 0 || h <= 0) return;
  props.SetColor(ShaderPropertyIDs.Color, colour);
  Graphics.DrawMesh(m, Matrix4x4.TRS(new Vector3(o.x + x, layer, o.z + z),
    Quaternion.Euler(0, angle, 0), new Vector3(w, 1, h)), mat, 0, null, 0, props);
}
function pose(s, i, p, t) {
  const sign = i ? 1 : -1;
  const deploy = smooth(s / p.deploy);
  const form = smooth((s - p.deploy) / p.form);
  const warning = smooth((s - t.lock) / p.warning);
  const cross = smooth((s - t.snap) / p.snap);
  const reform = smooth((s - t.reform) / p.reform);
  const recall = smooth((s - t.recall) / p.return);
  const drift = 0.4 * (1 - deploy);
  const gap = p.gap + warning * 0.18;
  let x = lerp(sign * 0.48, sign * gap + drift, deploy);
  x = lerp(x, -sign * gap, cross);
  const z = lerp(-p.distance, 0, deploy);
  return { x: lerp(x, sign * 0.48, recall), z: lerp(z, -p.distance, recall),
    angle: sign * 90 * cross * (1 - reform), morph: form * (1 - reform) };
}

// Paired edges interpolate between a circular cross-section and a concave crescent.
// A triangle fan would fill the hollow side; this strip preserves it throughout.
function blade(key, q, sign, p, o, alpha, trail = false) {
  const core = [], outline = [], triangles = [];
  for (let j = 0; j <= 64; j++) {
    const v = j / 32 - 1, round = Math.sqrt(Math.max(0, 1 - v * v));
    const centre = sign * (0.7 * round - 0.35) * q.morph;
    const half = lerp(0.28 * round, p.thickness * Math.pow(round, 1.4), q.morph);
    const z = v * lerp(0.28, p.length / 2, q.morph);
    core.push(centre - half, z, centre + half, z);
    outline.push(centre - half - p.rim * round, z, centre + half + p.rim * round, z);
    if (j) { const k = j * 2; triangles.push(k - 2, k, k - 1, k - 1, k, k + 1); }
  }
  const c = mesh(key + ' core'); c.setFlat(core, triangles);
  if (trail) {
    paint(c, o, q.x, q.z, y - 0.02, 1, 1, q.angle, edge.withAlpha(alpha));
    return;
  }
  const r = mesh(key + ' rim'); r.setFlat(outline, triangles);
  paint(r, o, q.x, q.z, y, 1, 1, q.angle, edge.withAlpha(alpha));
  paint(c, o, q.x, q.z, y + 0.002, 1, 1, q.angle, ink.withAlpha(alpha));
  // Short inner sheen follows the bent material rather than covering the dark body.
  paint(MeshPool.plane10, o, q.x, q.z, y + 0.004, 0.32, lerp(0.25, p.length * 0.65, q.morph),
    q.angle, edge.withAlpha(alpha * 0.12), glow);
}
function actor(o, x, z, alpha, caster = false) {
  const layer = AltitudeLayer.Pawn.AltitudeFor();
  paint(MeshPool.plane10, o, x, z - 0.05, floor, 0.8, 0.45, 0, ink.withAlpha(alpha * 0.35), soft);
  paint(disc, o, x, z + 0.17, layer, 0.23, 0.31, 0,
    (caster ? new Color(0.39, 0.57, 0.65) : new Color(0.65, 0.43, 0.30)).withAlpha(alpha));
  paint(disc, o, x, z + 0.55, layer + 0.002, 0.16, 0.17, 0, new Color(0.80, 0.69, 0.55, alpha));
}

export default {
  kit: 'Six Paths', label: 'Obsidian Shears (sketch)',
  params: {
    outcome: { label: 'Target response', value: 'hit', options: ['hit', 'dodge'], group: 'Showcase' },
    actors: { label: 'Show caster and target', value: true, group: 'Showcase' },
    deploy: P('Orbs reach target', 0.45, 0.2, 1, 0.05, 'Timing (s)'),
    form: P('Form crescent blades', 0.50, 0.2, 1, 0.05, 'Timing (s)'),
    warning: P('Warning after position locks', 0.45, 0.2, 1.2, 0.05, 'Timing (s)'),
    snap: P('Blades cross', 0.18, 0.10, 0.4, 0.01, 'Timing (s)'),
    recover: P('Hold follow-through', 0.15, 0.05, 0.4, 0.05, 'Timing (s)'),
    reform: P('Curl back into orbs', 0.40, 0.2, 0.8, 0.05, 'Timing (s)'),
    return: P('Orbs return to caster', 0.50, 0.2, 1, 0.05, 'Timing (s)'),
    distance: P('Caster distance (cells)', 3.4, 2.5, 5, 0.1, 'Shape'),
    gap: P('Flank distance (cells)', 1.3, 1, 2, 0.05, 'Shape'),
    length: P('Blade length (cells)', 3.3, 2.4, 4, 0.1, 'Shape'),
    thickness: P('Blade half-thickness (cells)', 0.30, 0.18, 0.45, 0.01, 'Shape'),
    rim: P('Violet edge (cells)', 0.025, 0.01, 0.05, 0.005, 'Shape'),
    trails: P('Blade trail strength', 0.20, 0, 0.5, 0.01, 'Feedback'),
    flash: P('Hit flash strength', 0.75, 0, 1, 0.05, 'Feedback'),
    shake: P('Hit camera shake', 0.09, 0, 0.2, 0.01, 'Feedback'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Deploy', t: 0 }, { name: 'Form', t: p.deploy },
      { name: 'Location locked', t: t.lock }, { name: 'Snap', t: t.snap },
      { name: p.outcome === 'hit' ? 'Hit' : 'Evaded', t: t.hit },
      { name: 'Reform', t: t.reform }, { name: 'Return', t: t.recall }, { name: 'Ready', t: t.home }];
  },
  events(p) { return p.outcome === 'hit' && p.shake ? [{ t: times(p).hit, type: 'shake', value: p.shake }] : []; },
  draw(s, p, { origin: o }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const alpha = smooth(s / 0.10) * (1 - smooth((s - t.home) / 0.25));
    const dodging = p.outcome === 'dodge';
    const escape = dodging ? smooth((s - t.lock) / (p.warning * 0.85)) : 0;
    const targetX = 0.4 * (1 - smooth(s / p.deploy));
    // Escape travels north, outside every allowed blade length; the blades stay locked.
    const targetZ = escape * (p.length / 2 + 1.8);
    if (p.actors) {
      actor(o, 0, -p.distance - 0.6, alpha, true);
      actor(o, targetX, targetZ, alpha);
    }
    const markerFade = 1 - smooth((s - t.hit) / 0.18);
    const mark = smooth((s - p.deploy) / p.form) * markerFade * alpha;
    paint(ring, o, 0, 0, floor + 0.01, 0.72, 0.72, 0, edge.withAlpha(mark * 0.45));
    if (s >= t.lock && s < t.snap) {
      const warning = clamp((s - t.lock) / p.warning);
      for (const a of [-45, 45])
        paint(slash, o, 0, 0, floor + 0.015, 1.8, 0.018, a, pale.withAlpha((0.15 + warning * 0.25) * alpha));
    }
    for (let i = 0; i < 2; i++) {
      const q = pose(s, i, p, t), sign = i ? 1 : -1;
      if (s >= t.snap && s < t.crossed + 0.08) {
        for (let j = 3; j >= 1; j--) {
          const past = Math.max(t.snap, s - j * 0.018);
          const fade = 1 - smooth((s - t.crossed) / 0.08);
          blade(`trail ${i} ${j}`, pose(past, i, p, t), sign, p, o,
            alpha * p.trails * (1 - j / 4) * fade, true);
        }
      }
      paint(MeshPool.plane10, o, q.x, q.z, floor, 0.8, lerp(0.55, p.length * 0.8, q.morph),
        q.angle, ink.withAlpha(alpha * 0.25), soft);
      blade(`blade ${i}`, q, sign, p, o, alpha);
    }
    const age = s - t.hit;
    if (age >= 0 && age < 0.25) {
      const fade = (1 - age / 0.25) ** 2;
      // Both modes retain the crossing motion. Only contact gets a bright central hit.
      for (const a of [-45, 45]) {
        paint(slash, o, 0, 0, y + 0.03, p.length * 1.15, 0.06 * fade, a,
          pale.withAlpha(alpha * fade * (dodging ? 0.20 : p.flash)), additive);
        if (!dodging)
          paint(MeshPool.plane10, o, 0, 0, y + 0.035, p.length, 0.24, a,
            edge.withAlpha(alpha * fade * p.flash * 0.6), glow);
      }
      if (!dodging) {
        paint(MeshPool.plane10, o, 0, 0, y + 0.04, 1.1, 1.1, 0,
          pale.withAlpha(alpha * fade * p.flash), glow);
        for (let i = 0; i < 8; i++) {
          const a = (45 + (i % 4) * 90 + (i < 4 ? -9 : 9)) * Mathf.Deg2Rad;
          const r = age * (3.5 + (i % 3));
          paint(slash, o, Math.cos(a) * r, Math.sin(a) * r, y + 0.05,
            0.2 * fade, 0.035, -a * Mathf.Rad2Deg, pale.withAlpha(alpha * fade * p.flash), additive);
        }
      }
    }
  },
};
