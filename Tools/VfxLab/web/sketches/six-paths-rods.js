// Six Paths black rods: a proposal, not the game. Nothing in Source/RimArt draws this yet, so
// there is no recording to compare it with.
//
// A ground ability, start to finish. The six orbs are already on the floor; they sink into it,
// the ground heaves at six points, and black rods stab up out of the holes in a ring around the
// target. They stand, then draw back down and leave the holes and the thrown dirt behind.
//
// Nothing arrives from above at any point. That is the whole difference from the slam, which
// drops a block out of the sky, and from the erasure sketches, which drove a spike down. Every
// vertical move here is upward, out of the floor, and the effect's own shapes -- holes, mounds,
// rods -- all touch the ground.
//
// Two details that say "ground" rather than "air", both worth keeping in the port:
//   - the ring is a true circle. The orb showcase squashes its ring to 0.60 depth because that
//     ring is tilted in the air; a circle drawn flat on the floor is a circle under RimWorld's
//     straight-down camera, so squashing it would put the rods back in the sky.
//   - every rod's shadow starts at its own base and runs along the sun. A shadow that stays
//     attached is what says the thing is standing in the floor rather than floating over it.
//
// Port notes for the C# version:
//   - a rod is SixPathsShapes' staff form (2.05 x 0.20), so SixPathsGraphics.MorphMesh can draw
//     it with its constant-width rim instead of the tapered quad used here
//   - heights are faked as SixPathsHeight.Lift, 0.60 cells north per cell up, as everywhere else
//   - bases, chunks and cracks need InBounds and Fogged checks per cell
//
// "Copy as C# constants" on the Params panel turns the current values into the lines to paste.

import {
  AltitudeLayer, Color, Graphics, MaterialPool, MaterialPropertyBlock, Mathf, Matrix4x4, Mesh,
  Meshes, MeshPool, Quaternion, ShaderDatabase, ShaderPropertyIDs, Vector3,
} from '../js/engine.js';
import { hash } from '../js/standins.js';

const Body = new Color(0.035, 0.028, 0.050);
const Rim = new Color(0.52, 0.36, 0.86);
const Sheen = new Color(0.72, 0.62, 0.95);
const Dirt = new Color(0.34, 0.26, 0.19);
const Lift = 0.60;              // SixPathsHeight.Lift
const Gain = 0.030;             // SixPathsHeight.Gain

const solid = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const soft = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent);
const softGlow = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.MoteGlow);
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const props = new MaterialPropertyBlock();

const disc = Meshes.disc(48, 'rod orb');
const rimBand = Meshes.band(1, 1.14, 48, 'rod orb rim');

// One mesh per thing drawn in a frame, never one reused: Graphics.DrawMesh reads a mesh when the
// frame renders, the same rule SixPathsGraphics.cs keeps.
const scratch = new Map();
const mesh = (key) => scratch.get(key) ?? scratch.set(key, new Mesh(key)).get(key);

const rand = (i, k) => hash(i, k, 6006);
const Y = {
  filth: AltitudeLayer.Filth.AltitudeFor(),
  moteLow: AltitudeLayer.MoteLow.AltitudeFor(),
  shadows: AltitudeLayer.Shadows.AltitudeFor(),
  back: AltitudeLayer.MoteOverheadLow.AltitudeFor(),
  rod: AltitudeLayer.MoteOverhead.AltitudeFor(),
};

function draw(m, x, y, z, sx, sz, rot, colour, material = solid) {
  props.SetColor(ShaderPropertyIDs.Color, colour);
  Graphics.DrawMesh(m, Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0, rot, 0), new Vector3(sx, 1, sz)),
    material, 0, null, 0, props);
}

/** A tapered quad from base to tip, drawn in map space: the rod's whole silhouette. */
function taper(key, x, y, z, base, tip, baseWidth, tipWidth, colour) {
  const dx = tip.x - base.x, dz = tip.z - base.z, len = Math.hypot(dx, dz);
  if (len < 1e-4) return;
  const nx = -dz / len, nz = dx / len;
  const b = baseWidth / 2, t = tipWidth / 2;
  const m = mesh(key);
  m.setFlat([
    base.x + nx * b, base.z + nz * b,
    tip.x + nx * t, tip.z + nz * t,
    tip.x - nx * t, tip.z - nz * t,
    base.x - nx * b, base.z - nz * b,
  ], [0, 1, 2, 0, 2, 3]);
  draw(m, x, y, z, 1, 1, 0, colour);
}

const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });

export default {
  kit: 'Six Paths',
  label: 'Black rods (sketch)',
  params: {
    sink: P('Orbs sink into the floor', 0.35, 0.1, 1.5, 0.05, 'Timing (s)'),
    heave: P('Ground heaves', 0.15, 0, 0.8, 0.01, 'Timing (s)'),
    overlap: P('Ground breaks this early into the sink', 0.80, 0, 0.95, 0.05, 'Timing (s)'),
    stagger: P('Rods start over', 0.30, 0, 1.5, 0.05, 'Timing (s)'),
    punch: P('One rod comes up in', 0.12, 0.04, 0.6, 0.01, 'Timing (s)'),
    stand: P('Rods stand', 1.00, 0, 4, 0.05, 'Timing (s)'),
    retract: P('Draw back down', 0.55, 0.1, 2, 0.05, 'Timing (s)'),
    marksFade: P('Holes and dirt fade', 0.60, 0, 2, 0.05, 'Timing (s)'),

    count: P('Rods', 6, 1, 12, 1, 'Ring'),
    radius: P('Ring radius (cells)', 1.80, 0.5, 5, 0.05, 'Ring'),
    spin: P('Ring angle (degrees)', 0, 0, 60, 1, 'Ring'),
    lean: P('Lean inward (cells at the tip)', 0.25, 0, 1.5, 0.05, 'Ring'),
    leanVary: P('Lean varies by', 0.55, 0, 1, 0.05, 'Ring'),
    leanWander: P('Lean direction wanders (degrees)', 55, 0, 180, 5, 'Ring'),

    tall: P('Rod height', 2.60, 0.5, 5, 0.05, 'Rod (cells)'),
    vary: P('Height varies by', 0.25, 0, 1, 0.01, 'Rod (cells)'),
    baseWidth: P('Width at the base', 0.18, 0.03, 0.4, 0.01, 'Rod (cells)'),
    tipWidth: P('Width at the tip', 0.06, 0.01, 0.3, 0.01, 'Rod (cells)'),
    overshoot: P('Overshoot on arrival', 0.12, 0, 0.5, 0.01, 'Rod (cells)'),
    outline: P('Rim brightness', 0.80, 0, 1, 0.01, 'Rod (cells)'),
    rimWidth: P('Rim width (cells)', 0.02, 0.005, 0.12, 0.005, 'Rod (cells)'),
    sheen: P('Lengthwise sheen', 0.16, 0, 0.6, 0.01, 'Rod (cells)'),
    tipGlow: P('Tip glow', 0.18, 0, 1, 0.01, 'Rod (cells)'),

    hole: P('Hole darkness', 0.55, 0, 1, 0.01, 'Ground'),
    holeSize: P('Hole size (cells)', 0.34, 0.1, 1.2, 0.01, 'Ground'),
    mound: P('Dirt mound', 0.50, 0, 1, 0.01, 'Ground'),
    chunks: P('Chunks thrown up per rod', 4, 0, 12, 1, 'Ground'),
    cracks: P('Cracks per rod', 2, 0, 8, 1, 'Ground'),
    dust: P('Dust per rod', 2, 0, 8, 1, 'Ground'),
    shake: P('Camera shake', 0.06, 0, 0.2, 0.01, 'Ground'),

    sunShadow: P('Sun shadow strength', 0.38, 0, 0.8, 0.01, 'Shadow'),
  },

  duration(p) { return times(p).endAt + p.marksFade; },

  phases(p) {
    const t = times(p);
    return [
      { name: 'Orbs sink', t: 0 }, { name: 'Heave', t: t.heaveAt }, { name: 'Rods up', t: t.punchAt },
      { name: 'Standing', t: t.standAt }, { name: 'Back down', t: t.retractAt },
    ];
  },

  events(p) {
    const t = times(p);
    // One small knock as the first rod breaks the surface. The ground moves; nothing lands.
    return p.shake > 0 ? [{ t: t.punchAt, type: 'shake', value: p.shake }] : [];
  },

  draw(seconds, p, { origin, scene }) {
    const t = times(p);
    const sun = scene.shadowVector;
    drawGround(seconds, p, t, origin);
    drawOrbs(seconds, p, t, origin, sun);
    drawRods(seconds, p, t, origin, sun);
    drawThrown(seconds, p, t, origin, sun);
  },
};

function times(p) {
  // The floor gives way while the orb is still going into it, not after: with the two apart
  // there is a moment with an orb too far gone to see and no hole yet, and the run reads as a
  // cut. The rods still wait for the orbs to be fully in.
  const heaveAt = p.sink * (1 - p.overlap);
  const punchAt = p.sink + p.heave;
  const standAt = punchAt + p.stagger + p.punch;
  const retractAt = standAt + p.stand;
  return { heaveAt, punchAt, standAt, retractAt, endAt: retractAt + p.retract };
}

const progress = (s, start, span) => Mathf.Clamp01((s - start) / span);

/** Where rod i stands, on the floor: a true circle, not a squashed orbit. */
function base(i, p, origin) {
  const angle = (i / p.count) * Math.PI * 2 + p.spin * Mathf.Deg2Rad;
  return { x: origin.x + Math.cos(angle) * p.radius, z: origin.z + Math.sin(angle) * p.radius, angle };
}

/**
 * Which way rod i leans and how far, in cells at the tip. Straight inward is the starting point,
 * then each rod's direction is turned by up to leanWander and its distance scaled by leanVary,
 * so no two come up at the same angle. Fixed per rod: the same rod always leans the same way.
 */
function leanOf(i, p, b) {
  const wander = (rand(i, 5) - 0.5) * 2 * p.leanWander * Mathf.Deg2Rad;
  const amount = p.lean * (1 + (rand(i, 6) - 0.5) * 2 * p.leanVary);
  // b.angle points out from the middle, so the inward direction is the opposite one.
  const heading = b.angle + Math.PI + wander;
  return { x: Math.cos(heading) * amount, z: Math.sin(heading) * amount };
}

/** The moment rod i breaks the surface, spread over the stagger in a fixed scatter. */
const startOf = (i, p, t) => t.punchAt + rand(i, 1) * p.stagger;

/**
 * How far rod i is out of the ground, in cells. Rises fast with a small overshoot, stands, then
 * draws back down. Zero before it starts and after it is gone.
 */
function rodHeight(i, s, p, t) {
  const tall = p.tall * (1 - p.vary * rand(i, 2));
  const up = progress(s, startOf(i, p, t), p.punch);
  if (up <= 0) return 0;
  const back = progress(s, t.retractAt + rand(i, 3) * 0.12, p.retract);
  if (back >= 1) return 0;
  // Overshoot as it arrives: a half sine over the punch, gone by the time it stands.
  const out = Mathf.Smooth(up) * (1 + p.overshoot * Math.sin(Mathf.Clamp01(up) * Math.PI));
  return tall * out * (1 - Mathf.Smooth(back));
}

// ---------------------------------------------------------------- the floor

/** Holes and mounded dirt, from the heave until the marks fade. */
function drawGround(s, p, t, origin) {
  if (s < t.heaveAt) return;
  const fade = 1 - Mathf.Smooth(progress(s, t.endAt, p.marksFade));
  if (fade <= 0.001) return;

  for (let i = 0; i < p.count; i++) {
    const b = base(i, p, origin);
    const open = Mathf.Smooth(progress(s, t.heaveAt + rand(i, 4) * 0.06, Math.max(0.08, p.sink * p.overlap + p.heave * 0.5)));
    if (open <= 0.001) continue;
    const r = p.holeSize * open;

    // Mound first, hole over it: the dirt sits around the hole rather than in it.
    if (p.mound > 0)
      draw(MeshPool.plane10, b.x, Y.filth, b.z, r * 4.2, r * 3.4, b.angle * Mathf.Rad2Deg,
        Dirt.withAlpha(p.mound * fade), soft);
    draw(disc, b.x, Y.filth + 0.004, b.z, r, r * 0.8, 0, new Color(0, 0, 0, p.hole * fade));

    // Cracks out from the rim, drawn once the ground has broken.
    for (let k = 0; k < p.cracks; k++) {
      const angle = b.angle + (rand(i, 10 + k) - 0.5) * 2.2;
      const reach = (0.20 + rand(i, 20 + k) * 0.30) * open;
      const m = mesh(`crack-${i}-${k}`);
      const x0 = b.x + Math.cos(angle) * r * 0.9, z0 = b.z + Math.sin(angle) * r * 0.7;
      const x1 = x0 + Math.cos(angle) * reach, z1 = z0 + Math.sin(angle) * reach * 0.8;
      const nx = -(z1 - z0), nz = x1 - x0, len = Math.hypot(nx, nz) || 1;
      const w = 0.02;
      m.setFlat([
        x0 + (nx / len) * w, z0 + (nz / len) * w, x1, z1,
        x1, z1, x0 - (nx / len) * w, z0 - (nz / len) * w,
      ], [0, 1, 3, 1, 2, 3]);
      draw(m, 0, Y.filth + 0.002, 0, 1, 1, 0, new Color(0.10, 0.07, 0.05, 0.32 * fade));
    }
  }
}

// ---------------------------------------------------------------- the orbs going in

/** The six orbs, already on the floor, sinking into it. Gone once they are all the way in. */
function drawOrbs(s, p, t, origin, sun) {
  // Until the sink ends, not until the ground heaves: the heave starts partway through the sink,
  // so the two overlap and the hole grows under an orb that is still going down.
  if (s >= p.sink) return;
  const u = Mathf.Smooth(progress(s, 0, p.sink));
  for (let i = 0; i < p.count; i++) {
    const b = base(i, p, origin);
    // Sinking, not fading: it keeps its width and its colour and loses its height, so what is
    // left is a shape being swallowed by the floor. Opacity only goes at the very end, when
    // what is left is a sliver, or the orbs read as dissolving in mid-air instead.
    const size = 0.42 * (1 - u * 0.15);
    const left = 1 - u;
    const alpha = 1 - Mathf.Smooth(Mathf.Clamp01((u - 0.8) / 0.2));
    draw(MeshPool.plane10, b.x + sun.x * 0.2, Y.shadows, b.z + sun.z * 0.2,
      size * 2.4, size * 1.7 * Mathf.Lerp(0.5, 1, left), 0, new Color(0, 0, 0, p.sunShadow * alpha), soft);
    draw(rimBand, b.x, Y.rod, b.z, size, size * left, 0, Rim.withAlpha(0.9 * alpha));
    draw(disc, b.x, Y.rod + 0.004, b.z, size, size * left, 0, Body.withAlpha(alpha));
  }
}

// ---------------------------------------------------------------- the rods

function drawRods(s, p, t, origin, sun) {
  for (let i = 0; i < p.count; i++) {
    const height = rodHeight(i, s, p, t);
    if (height <= 0.001) continue;
    const b = base(i, p, origin);

    // The tip leans its own way and takes the height offset north. The lean grows with the rod,
    // so a rod still coming up is straighter than one fully out.
    const out = height / Math.max(p.tall, 1e-5);
    const away = leanOf(i, p, b);
    const tip = {
      x: b.x + away.x * out,
      z: b.z + away.z * out + height * Lift,
    };
    const grow = 1 + height * Gain;
    // Nearer rods cover the ones behind them: south of the ring draws last.
    const y = Y.rod - b.z * 0.0005 + origin.z * 0.0005;

    // Shadow: from this rod's own base, along the sun, as long as the rod is tall.
    const shadow = { x: b.x + sun.x * height, z: b.z + sun.z * height };
    taper(`shadow-${i}`, 0, Y.shadows, 0, b, shadow, p.baseWidth * 1.3, p.tipWidth * 1.3,
      new Color(0, 0, 0, p.sunShadow));

    // Rim as a wider silhouette behind the body, so the lit line sits outside it -- the order
    // SixPathsGraphics draws an orb in.
    if (p.outline > 0)
      taper(`rim-${i}`, 0, y, 0, b, tip, (p.baseWidth + p.rimWidth * 2) * grow,
        (p.tipWidth + p.rimWidth * 2) * grow, Rim.withAlpha(p.outline));
    taper(`body-${i}`, 0, y + 0.004, 0, b, tip, p.baseWidth * grow, p.tipWidth * grow, Body);

    if (p.sheen > 0) {
      // Offset a quarter of the width along the rod's own perpendicular, on the lit side.
      const dx = tip.x - b.x, dz = tip.z - b.z, len = Math.hypot(dx, dz) || 1;
      const nx = (-dz / len) * p.baseWidth * 0.26, nz = (dx / len) * p.baseWidth * 0.26;
      taper(`sheen-${i}`, 0, y + 0.006, 0, { x: b.x + nx, z: b.z + nz }, { x: tip.x + nx, z: tip.z + nz },
        p.baseWidth * 0.28, p.tipWidth * 0.28, Sheen.withAlpha(p.sheen));
    }

    if (p.tipGlow > 0)
      draw(MeshPool.plane10, tip.x, y + 0.008, tip.z, p.baseWidth * 3.4, p.baseWidth * 3.4, 0,
        Sheen.withAlpha(p.tipGlow), softGlow);
  }
}

// ---------------------------------------------------------------- what the rods throw up

/**
 * Dirt knocked loose where a rod breaks through: up, then back down under gravity, landing near
 * its own hole. Nothing is thrown outward far -- the floor is being punctured, not hit.
 */
function drawThrown(s, p, t, origin, sun) {
  const fade = 1 - Mathf.Smooth(progress(s, t.endAt, p.marksFade));
  if (fade <= 0.001) return;

  for (let i = 0; i < p.count; i++) {
    const b = base(i, p, origin);
    const since = s - startOf(i, p, t);
    if (since < 0) continue;

    for (let k = 0; k < p.chunks; k++) {
      const angle = rand(i, 30 + k) * Math.PI * 2;
      const speed = 0.5 + rand(i, 40 + k) * 1.1, rise = 2.2 + rand(i, 50 + k) * 2.2, gravity = 20;
      const flight = (2 * rise) / gravity, tau = Math.min(since, flight);
      const height = Math.max(0, rise * tau - 0.5 * gravity * tau * tau);
      const at = p.holeSize * 0.8 + speed * tau;
      const x = b.x + Math.cos(angle) * at, z = b.z + Math.sin(angle) * at * 0.85;
      const size = 0.07 + rand(i, 60 + k) * 0.10;
      if (height > 0.02)
        draw(MeshPool.plane10, x + sun.x * height, Y.shadows, z + sun.z * height,
          size * 1.4, size, 0, new Color(0, 0, 0, 0.28 * fade), soft);
      draw(MeshPool.plane10, x, (z > b.z && height < 0.8 ? Y.back : Y.rod + 0.02) + k * 0.0002,
        z + height * Lift, size, size * 0.8, rand(i, 70 + k) * 360 + tau * 540 * (rand(i, 80 + k) - 0.5),
        Dirt.withAlpha(fade * (since > flight ? 0.85 : 1)));
    }

    // A little dust at the lip of each hole, low and brief.
    for (let k = 0; k < p.dust; k++) {
      const life = 0.4 + rand(i, 90 + k) * 0.3, u = since / life;
      if (u >= 1) continue;
      const angle = rand(i, 100 + k) * Math.PI * 2;
      const at = p.holeSize + (0.15 + rand(i, 110 + k) * 0.35) * Mathf.Smooth(u);
      const size = Mathf.Lerp(0.35, 0.85, Math.sqrt(u));
      const gz = Math.sin(angle) * at * 0.8;
      draw(MeshPool.plane10, b.x + Math.cos(angle) * at, (gz > 0 ? Y.back : Y.rod + 0.03) + k * 0.0001,
        b.z + gz + u * 0.15 * Lift, size, size * 0.85, rand(i, 120 + k) * 360,
        new Color(0.60, 0.54, 0.46, 0.34 * Math.sin(Math.min(1, u * 4) * Math.PI / 2) * (1 - u) ** 1.3), puff);
    }
  }
}
