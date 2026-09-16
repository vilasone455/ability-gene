// Orb armament proposal: orbit (0.45s), approach (0.55s), continuous rifle morph
// (0.8s), held aim (0.55s), burst or melee attack, recover, collapse and return.
// A lab mannequin demonstrates hand attachment; no equipment, damage or ammo logic.
// Rifle and melee variants share the same orb-to-hand transformation.
// Each connected weapon part grows from the same sphere. Pure time-based geometry,
// shipped SoftDisc plus white meshes only; no new textures or game shaders needed.
import {
  AltitudeLayer, Color, Graphics, MaterialPool, MaterialPropertyBlock, Mathf,
  Matrix4x4, Mesh, Meshes, MeshPool, Quaternion, ShaderDatabase, ShaderPropertyIDs, Vector3,
} from '../js/engine.js';

const ink = new Color(0.025, 0.018, 0.04), rim = new Color(0.58, 0.32, 0.88);
const light = new Color(0.91, 0.76, 1), skin = new Color(0.78, 0.66, 0.51);
const solid = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const glow = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.MoteGlow);
const soft = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent);
const disc = Meshes.disc(48, 'armament orb / hands');
const ring = Meshes.band(0.94, 1, 64, 'armament pulse');
const props = new MaterialPropertyBlock(), meshes = new Map();
const y = AltitudeLayer.MoteOverhead.AltitudeFor();
const smooth = Mathf.Smooth, lerp = Mathf.Lerp;
const P = (label, value, min, max, step, group = 'Timing (s)') => ({ label, value, min, max, step, group });
// Connected, rounded rectangular masses: centre x/z, half extents, corner exponent,
// delay fraction. Barrel comes first; stock and grip follow from the receiver.
const rifleParts = [
  ['barrel', 0.76, 0.06, 0.53, 0.065, 5, 0],
  ['stock', -0.59, -0.025, 0.30, 0.14, 4, 0.18],
  ['grip', -0.12, -0.22, 0.085, 0.23, 4, 0.34],
  ['receiver', 0.10, 0, 0.40, 0.17, 5, 0.08],
  ['sight', 0.12, 0.21, 0.20, 0.045, 4, 0.48],
];
const weapons = {
  Rifle: { parts: rifleParts, hands: [[-0.12, -0.26], [0.43, -0.09]] },
  Sword: { parts: [
    ['blade', 0.65, 0, 0.80, 0.12, 3, 0, 1],
    ['guard', -0.16, 0, 0.055, 0.28, 4, 0.3],
    ['hilt', -0.37, 0, 0.21, 0.055, 4, 0.15],
  ], hands: [[-0.35, 0]], tip: 1.45 },
  Staff: { parts: [
    ['shaft', 0.15, 0, 1.25, 0.065, 5, 0],
    ['rear cap', -1.05, 0, 0.11, 0.095, 4, 0.25],
    ['front cap', 1.35, 0, 0.11, 0.095, 4, 0.25],
  ], hands: [[-0.35, 0], [0.35, 0]], tip: 1.46 },
  Spear: { parts: [
    ['shaft', 0.2, 0, 1.15, 0.045, 5, 0],
    ['point', 1.45, 0, 0.43, 0.18, 1, 0.25],
    ['collar', 1.13, 0, 0.10, 0.075, 4, 0.35],
  ], hands: [[-0.4, 0], [0.35, 0]], tip: 1.88 },
  Hammer: { parts: [
    ['handle', 0.12, 0, 0.72, 0.06, 5, 0],
    ['head', 0.91, 0, 0.24, 0.42, 6, 0.24],
    ['head band', 0.91, 0, 0.28, 0.16, 5, 0.45],
  ], hands: [[-0.35, 0], [0.05, 0]], tip: 1.15 },
};
function meleePose(age, kind, duration) {
  const u = age / duration;
  const wind = smooth(u / 0.32), strike = smooth((u - 0.32) / 0.2);
  const recover = smooth((u - 0.62) / 0.38);
  if (kind === 'Spear') return { angle: 0, push: (-0.25 * wind + 1.05 * strike) * (1 - recover) };
  const start = kind === 'Hammer' ? 85 : kind === 'Staff' ? -75 : -55;
  const finish = kind === 'Hammer' ? -30 : 60;
  return { angle: (start * wind + (finish - start) * strike) * (1 - recover), push: 0 };
}
function times(p) {
  const morph = 0.45 + p.approach, held = morph + p.morph, fire = held + p.aimHold;
  const collapse = fire + (p.weapon === 'Rifle' ? 2 * p.interval + 0.65 : p.attack + 0.3), back = collapse + p.morph;
  return { morph, held, fire, collapse, back, end: back + p.approach + 0.45 };
}
function paint(m, o, p, x, z, w, h, layer, c, mat = solid, rotate = true, turn = 0) {
  if (c.a <= 0 || w <= 0 || h <= 0) return;
  const a = p.aim * Mathf.Deg2Rad;
  props.SetColor(ShaderPropertyIDs.Color, c);
  Graphics.DrawMesh(m, Matrix4x4.TRS(new Vector3(o.x + x * Math.cos(a) - z * Math.sin(a), layer,
    o.z + x * Math.sin(a) + z * Math.cos(a)), Quaternion.Euler(0, rotate ? -p.aim - turn : 0, 0),
    new Vector3(w, 1, h)), mat, 0, null, 0, props);
}
function shape(key, hx, hz, exponent, taper = 0) {
  if (!meshes.has(key)) meshes.set(key, new Mesh(`armament ${key}`));
  const v = [0, 0], tri = [];
  for (let i = 0; i < 48; i++) {
    const a = i / 48 * Math.PI * 2, c = Math.cos(a), s = Math.sin(a);
    const r = (Math.abs(c) ** exponent + Math.abs(s) ** exponent) ** (-1 / exponent);
    v.push(hx * r * c, hz * r * s * (1 - taper * (1 + c) / 2));
    tri.push(0, i + 1, (i + 1) % 48 + 1);
  }
  const m = meshes.get(key);
  m.setFlat(v, tri);
  return m;
}
export default {
  kit: 'Six Paths', label: 'Orb Rifle (sketch)',
  params: {
    weapon: { label: 'Weapon form', value: 'Rifle', options: Object.keys(weapons), group: 'Weapon' },
    attack: P('Melee attack duration', 1.0, 0.6, 1.8, 0.05),
    approach: P('Orb moves to hand', 0.55, 0.2, 1.2, 0.05),
    morph: P('Weapon forms / collapses', 0.8, 0.3, 1.6, 0.05),
    aimHold: P('Hold equipped before attack', 0.55, 0.2, 2, 0.05),
    interval: P('Time between shots', 0.32, 0.2, 0.8, 0.02),
    aim: P('Aim direction (degrees)', 0, 0, 360, 5, 'Shape'),
    size: P('Weapon scale', 1, 0.7, 1.4, 0.05, 'Shape'),
    range: P('Shot travel (cells)', 4.5, 2, 7, 0.25, 'Shape'),
    recoil: P('Recoil (cells)', 0.12, 0, 0.25, 0.01, 'Feedback'),
    glow: P('Violet glow', 0.55, 0, 1, 0.05, 'Feedback'),
    actors: { label: 'Show pawn and hands', value: true, group: 'Showcase' },
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Orbit', t: 0 }, { name: 'To hand', t: 0.45 }, { name: `Form ${p.weapon.toLowerCase()}`, t: t.morph },
      { name: 'Equipped / aim', t: t.held }, { name: p.weapon === 'Rifle' ? 'Fire burst' : 'Melee attack', t: t.fire },
      { name: 'Reform orb', t: t.collapse }, { name: 'Return to orbit', t: t.back }];
  },
  draw(s, p, { origin: o }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const travel = smooth((s - 0.45) / p.approach) * (1 - smooth((s - t.back) / p.approach));
    const form = smooth((s - t.morph) / p.morph) * (1 - smooth((s - t.collapse) / p.morph));
    const orbitAngle = s * 0.7;
    const ox = -1.25 + Math.cos(orbitAngle) * 0.2, oz = 0.9 + Math.sin(orbitAngle) * 0.2;
    const kind = p.weapon, weapon = weapons[kind], melee = kind !== 'Rifle';
    let kick = 0;
    for (let i = 0; !melee && i < 3; i++) {
      const dt = s - t.fire - i * p.interval;
      if (dt >= 0 && dt < 0.2) kick += (1 - smooth(dt / 0.2)) * p.recoil;
    }
    const x = lerp(ox, 0.12, travel) - kick, z = lerp(oz, -0.18, travel);
    const pose = melee ? meleePose(s - t.fire, kind, p.attack) : { angle: 0, push: 0 };
    const angle = pose.angle * Mathf.Deg2Rad;
    const point = (px, pz) => ({ x: x + pose.push + (px * Math.cos(angle) - pz * Math.sin(angle)) * p.size,
      z: z + (px * Math.sin(angle) + pz * Math.cos(angle)) * p.size });
    const heldPaint = (m, px, pz, w, h, layer, c, mat = solid) => {
      const q = point(px, pz);
      paint(m, o, p, q.x, q.z, w, h, layer, c, mat, true, pose.angle);
    };
    const pawnY = AltitudeLayer.Pawn.AltitudeFor();
    if (p.actors) {
      paint(MeshPool.plane10, o, p, -0.48, -0.3, 1.1, 0.6, AltitudeLayer.Shadows.AltitudeFor(), ink.withAlpha(0.4), soft);
      paint(disc, o, p, -0.48, 0, 0.29, 0.34, pawnY, new Color(0.30, 0.43, 0.47), solid, false);
      paint(disc, o, p, -0.48, 0.38, 0.19, 0.20, pawnY + 0.01, skin, solid, false);
    }
    paint(MeshPool.plane10, o, p, x, z, lerp(1.2, 2.5, form) * p.size, 1.0,
      y - 0.02, rim.withAlpha(p.glow * (0.25 + Math.sin(form * Math.PI) * 0.4)), glow);
    // All rims first, then all bodies: overlapping parts join without internal neon seams.
    for (const edge of [true, false]) {
      for (const [key, px, pz, hx, hz, corner, delay, taper = 0] of weapon.parts) {
        const u = smooth((form - delay) / (1 - delay));
        const padding = edge ? 0.018 : 0;
        const m = shape(`${kind}-${key}-${edge}`, lerp(0.30, hx, u) + padding,
          lerp(0.30, hz, u) + padding, lerp(2, corner, u), taper * u);
        heldPaint(m, px * u, pz * u, p.size, p.size,
          y + (edge ? 0 : 0.004), edge ? rim : ink);
      }
    }
    // A restrained receiver line makes the held form readable.
    if (!melee) paint(MeshPool.plane10, o, p, x + 0.12 * p.size, z + 0.08 * p.size,
      0.48 * form * p.size, 0.018, y + 0.008, rim.withAlpha(form * 0.65));
    if (p.actors) {
      const grip = smooth((s - t.held + 0.15) / 0.15) * (1 - smooth((s - t.collapse) / 0.18));
      for (let i = 0; i < 2; i++) {
        const hand = weapon.hands[i], rest = i ? -0.2 : -0.75;
        const q = hand ? point(...hand) : { x: rest, z: -0.28 };
        paint(disc, o, p, lerp(rest, q.x, grip), lerp(-0.28, q.z, grip),
          0.075, 0.085, y + 0.012, skin, solid, false);
      }
    }
    if (melee) {
      const age = s - t.fire, u = age / p.attack;
      // Time-sampled tip trail follows the actual swing; it never advances frame state.
      if (u >= 0.32 && u < 0.72) {
        for (let i = 0; i < 10; i++) {
          const past = age - i * 0.012;
          if (past < p.attack * 0.32) continue;
          const previous = meleePose(past, kind, p.attack), a = previous.angle * Mathf.Deg2Rad;
          const fade = (1 - i / 10) * (1 - smooth((u - 0.52) / 0.2));
          paint(MeshPool.plane10, o, p, x + previous.push + Math.cos(a) * weapon.tip * p.size,
            z + Math.sin(a) * weapon.tip * p.size, 0.22, 0.22, y - 0.01,
            rim.withAlpha(fade * p.glow * 0.45), glow);
        }
      }
      if (kind === 'Hammer' && u >= 0.52 && u < 0.8) {
        const impact = (u - 0.52) / 0.28, a = -30 * Mathf.Deg2Rad;
        paint(ring, o, p, x + Math.cos(a) * weapon.tip * p.size, z + Math.sin(a) * weapon.tip * p.size,
          0.12 + impact * 0.6, 0.08 + impact * 0.35, y - 0.01, rim.withAlpha((1 - impact) * p.glow));
      }
      return;
    }
    for (let i = 0; i < 3; i++) {
      const dt = s - t.fire - i * p.interval;
      if (dt < 0) continue;
      const flash = 1 - smooth(dt / 0.12), muzzle = 0.12 + 1.3 * p.size;
      paint(MeshPool.plane10, o, p, muzzle - kick, -0.18 + 0.06 * p.size,
        0.7, 0.5, y + 0.02, light.withAlpha(flash * p.glow), glow);
      paint(ring, o, p, muzzle - kick, -0.18 + 0.06 * p.size,
        0.06 + dt * 0.5, 0.12 + dt, y + 0.021, rim.withAlpha(flash));
      const flight = 0.22, u = dt / flight;
      if (u <= 1) {
        const bx = muzzle + u * p.range, bz = -0.18 + 0.06 * p.size;
        paint(MeshPool.plane10, o, p, bx, bz, 0.65, 0.3, y + 0.025, rim.withAlpha(p.glow), glow);
        paint(disc, o, p, bx, bz, 0.19, 0.035, y + 0.026, light);
      }
      const impact = 1 - smooth((dt - flight) / 0.2);
      if (dt >= flight && impact > 0) {
        paint(MeshPool.plane10, o, p, muzzle + p.range, -0.18 + 0.06 * p.size,
          0.6, 0.85, y + 0.03, light.withAlpha(impact * p.glow), glow);
      }
    }
  },
};
