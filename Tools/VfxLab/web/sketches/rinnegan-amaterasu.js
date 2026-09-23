// Amaterasu — technique proposal for the Rinnegan eye kit, not the game. Nothing in Source/RimArt
// draws this yet.
//
// What it is for (proposed, none of it agreed; every number is a placeholder). Black flames light
// where the caster looks. Target: one pawn or one cell, 12 cells, line of sight. 0.5 s warmup,
// 60 s cooldown.
//   On a pawn: a black-flame hediff, 4 burn damage per second for 20 s (80 total). Rain, water,
//   firefoam and beating do not put it out. It ends at 20 s or when the caster presses Release.
//   It spreads to an adjacent pawn at 10% per second, carrying the time that is left.
//   On a cell: a 3x3 black fire for 20 s that ignites any pawn that walks in. It does not spread to
//   buildings or terrain, so it cannot eat a base.
//   Cost: the caster gets Bleeding eye for 60 s (Sight -50%). Casting again while it is on blinds
//   them for 10 s. This is the kit's finisher, not something for every fight.
// Pairs with Amenotejikara: swap a burning pawn in among its own side, or a burning kunai into a path.
//
// Look, taken from the anime (Itachi vs Sasuke, Shippuden 137-138; Sasuke vs Killer Bee, 142-143;
// Sasuke vs Danzo; the Kaguya fight): the flames are pure black, with no glow and no coloured rim.
// They appear on the target at once, with no projectile. They cling to the body as ragged blotches,
// rise from it as thin wavy strands whose tops tear off as black flecks, and pool on the ground
// under it. The cost shows on the caster: the casting eye bleeds. The games give the flames a
// purple sheen; the anime does not, so this does not either.
//
// Showcase, default timings:
//   0.00-0.50  gaze: a red glint on the caster's eye. The Mangekyo mark on the target is an option,
//              off by default, because the anime shows the eye close-up and nothing at the target.
//   0.50       ignition: a black mass bursts out of the target's chest (0.2-0.3 s) and throws 30
//              shards up and out (0.32-0.57 s, the upward ones furthest), a soot splash rings its
//              feet, the fire catches from the chest out in 0.12 s and the strands surge to 1.3x,
//              settling by 0.45 s. Camera shake 0.075, screen dim 0.18. Blood runs from the caster's
//              eye from here on, two streaks.
//   burning    26 strands and 5 short base tongues per pawn, each on its own 0.4-0.95 s cycle: it
//              grows, sways, tears its top off as a fleck and regrows. Most strands root on the floor
//              round the feet, 30% on the body. 8 blotches lick up the body; the pawn shows through
//              the gaps. 12 loose flecks rise and vanish. A dark pool stains the floor under the feet.
//   2.00       the neighbour catches: 3 flecks jump across in 0.22 s, then blotches and strands creep
//              over it from the touching side in 0.3 s. No burst: it caught, nobody cast it.
//   4.00       the caster releases: strands and blotches sink over 0.3 s, flecks already in the air
//              finish, a puff of grey smoke rises from above the head for 0.8 s. The scorch stays.
// Cell scenario: the same parts spread over a disc of the fire's radius (44 strands, 12 base
// tongues), 0.7x as tall, standing in a see-through pool so the black flames still read against
// it, with the burst at its centre.
//
// Drawing: strands and tongues are band meshes on a wavy spine with a tapered, ragged width, height
// drawn north by Lift. On a pawn, strands rooted on the ground north of its feet draw under the pawn
// layer and the rest over it, so the pawn stands inside the fire. Blotches, flecks and shards are
// sprites of two generated textures, lab/black-blot and lab/black-shred (both need PNGs before a C#
// port). Everything is screen-oriented, so there is no per-facing drawing. All births are analytic
// from the clip time, so scrubbing is deterministic.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../js/engine.js';
import { registerLabTexture, pixels, fbm } from '../js/standins.js';
import { draw, Lift } from './lib/six-paths-solid.js';
import { P, Y, Floor, at, sprite, band, trail, glow, soft, rand } from './lib/six-paths-impact.js';
import { figure, whiteGlow, CasterColour, EnemyColour } from './lib/flying-thunder-god.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01;
const pawnLayer = AltitudeLayer.Pawn.AltitudeFor(), topLayer = AltitudeLayer.MetaOverlays.AltitudeFor();
const flatDisc = Meshes.disc(48, 'amaterasu disc');

// A torn black scrap, taller than wide: two lopsided lumps with a notched edge and a hole, so it
// reads as a torn piece of flame rather than a leaf or a teardrop. White in the alpha.
registerLabTexture('lab/black-shred', () => pixels(128, (u, v) => {
  const x = (u - .5) * 2, y = (.5 - v) * 2;
  const low = Math.hypot(x / .5, (y + .2) / .62), high = Math.hypot((x - .14) / .3, (y - .38) / .38);
  const n = fbm(u * 6, v * 6, 41, 3, 6) - .5, notch = fbm(u * 13, v * 13, 7, 2, 13) - .5;
  let a = clamp((1 + n * .7 + notch * .4 - Math.min(low, high)) / .08);
  const hole = fbm(u * 7, v * 7, 97, 2, 7);
  if (hole > .7) a *= clamp((.78 - hole) / .08);
  return [1, 1, 1, a * clamp(Math.min(u, 1 - u, v, 1 - v) / .04)];
}));
// A ragged round blot with a few specks thrown off its edge. White in the alpha.
registerLabTexture('lab/black-blot', () => pixels(128, (u, v) => {
  const r = Math.hypot(u - .5, v - .5) * 2;
  const rag = fbm(u * 4, v * 4, 13, 3, 4);
  let a = clamp((.52 + (rag - .5) * 1.1 - r) / .07);
  const speck = fbm(u * 10, v * 10, 57, 2, 10);
  if (r > .5 && r < .88 && speck > .66) a = Math.max(a, clamp((speck - .66) / .05));
  return [1, 1, 1, a * clamp((.98 - r) / .06)];
}));
const shred = MaterialPool.MatFrom('lab/black-shred', ShaderDatabase.Transparent);
const blot = MaterialPool.MatFrom('lab/black-blot', ShaderDatabase.Transparent);
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);

// Decided looks. The flame is one colour, black; blood and the eye glint are the only red.
const Ink = new Color(.010, .008, .014);
const Scorch = new Color(.05, .035, .04), Smoke = new Color(.17, .16, .18);
const Crimson = new Color(.75, .08, .12), EmberLit = new Color(.85, .16, .14), Blood = new Color(.52, .03, .05);
const Ally = new Color(.45, .62, .40);
const Gaze = .5, MarkR = .55, Sink = .3, DimLife = .18, ShakeSize = .075, SmokeLife = .8, JumpTime = .22;
const Strands = 26, BaseTongues = 5, CellStrands = 44, CellTongues = 12, Blotches = 8, LooseFlecks = 12;
const Scenarios = ['pawn', 'pawn, spreads', 'cell'];

function times(p) {
  const ignite = Gaze, release = p.release ? ignite + p.releaseAt : Infinity;
  const end = p.release ? release + SmokeLife + .2 : ignite + p.burn; // showcase cut, not a gameplay expiry
  return { ignite, release, end };
}

// The Mangekyo mark (optional): a pupil and 3 curved blades, drawn from the centre outward as u goes
// 0..1, turning by spin radians.
function mark(key, c, u, spin, alpha) {
  if (u <= 0 || alpha <= 0) return;
  draw(flatDisc, c.x, Floor + .03, c.z, .1 * u, .1 * u, 0, Crimson.withAlpha(alpha));
  for (let i = 0; i < 3; i++) {
    const a0 = spin + i * Math.PI * 2 / 3, pts = [];
    for (let k = 0; k <= 8; k++) {
      const v = k / 8 * u, r = .12 + MarkR * v, a = a0 + v * 1.4;
      pts.push({ x: c.x + Math.cos(a) * r, z: c.z + Math.sin(a) * r });
    }
    trail(`${key} blade ${i}`, pts, .16, Crimson.withAlpha(alpha), Floor + .031);
  }
}

function redStar(pos, size, alpha) {
  if (alpha <= 0) return;
  sprite(pos, size * 1.2, size * 1.2, Crimson.withAlpha(alpha * .7), glow, Y + .2);
  draw(MeshPool.plane10, pos.x, Y + .21, pos.z, size * 2, size * .12, 0, EmberLit.withAlpha(alpha), whiteGlow);
  draw(MeshPool.plane10, pos.x, Y + .21, pos.z, size * .12, size * 2, 0, EmberLit.withAlpha(alpha), whiteGlow);
}

// Bleeding eye: two streaks of blood run down from the casting eye after the cast. In game this is
// the cost hediff, shown on the pawn.
function bleed(eye, age) {
  const l1 = .05 + .15 * smooth(age / 1.2), l2 = .03 + .08 * smooth((age - .3) / 1.2);
  draw(MeshPool.plane10, eye.x + .025, pawnLayer + .01, eye.z - .04 - l1 / 2, .03, l1, 0, Blood);
  if (age > .3) draw(MeshPool.plane10, eye.x - .02, pawnLayer + .01, eye.z - .04 - l2 / 2, .022, l2, 0, Blood);
}

// One strand: a black ribbon on a wavy spine. The wave runs up it, each edge boils upward on its own
// rhythm, and the width tapers from the root to a point.
const StrandSteps = 16;
function strand(key, root, h, hw, clock, seed, alpha, layer, lean, rootW = .6) {
  if (h < .03 || hw < .004 || alpha <= .003) return;
  const phase = rand(seed) * 6.283, f1 = 5 + rand(seed + 1) * 3, f2 = 2 + rand(seed + 2) * 1.5;
  const amp = .05 + .05 * h, left = [], right = [];
  for (let j = 0; j <= StrandSteps; j++) {
    const u = j / StrandSteps;
    const sway = amp * u * (.7 * Math.sin(u * f1 - clock * 7 + phase) + .5 * u * Math.sin(u * f2 - clock * 3.3 + phase * 1.7));
    const cx = root.x + lean * u * h + sway, cz = root.z + h * Lift * u;
    const profile = (rootW + (1 - rootW) * Math.sin(Math.min(1, u / .3) * Math.PI / 2)) * Math.pow(1 - u, .8);
    const ragL = 1 + .38 * Math.sin(u * 17 - clock * 10 + phase) * Math.sin(u * 5.3 + phase * 2);
    const ragR = 1 + .38 * Math.sin(u * 15 - clock * 9 + phase * 1.3) * Math.sin(u * 4.1 + phase * 3);
    left.push({ x: cx - hw * profile * ragL, z: cz }); right.push({ x: cx + hw * profile * ragR, z: cz });
  }
  band(key, left, right, Ink.withAlpha(alpha), layer);
}

// A strand's life, in clock time: it grows from 35% to full height over the first 45% of its cycle,
// holds, and over the last 20% its top tears off as a fleck while the stem drops back to 35%.
function cycleOf(clock, period, offset) {
  const c = (clock + offset) / period, n = Math.floor(c), u = c - n;
  return { n, k: .35 + .65 * (smooth(u / .45) - smooth((u - .8) / .2)) };
}

// The cast: a black mass bursts out of the focal point and throws shards up and out, the upward
// ones furthest, and a soot splash rings the floor.
function eruption(c, age, seed, scale) {
  if (age < 0 || age > .6) return;
  const splash = age / .35;
  if (splash < 1) {
    const d = (.9 + 2.4 * smooth(splash)) * scale;
    sprite(c, d, d, Ink.withAlpha(.5 * (1 - splash)), puff, Floor + .022);
  }
  const focus = at(c, 0, 0, .55);
  for (let i = 0; i < 5; i++) {
    const k = seed + 950 + i * 3, u = age / (.2 + rand(k) * .1);
    if (u >= 1) continue;
    const size = (.35 + .7 * smooth(u * 2)) * (.7 + .5 * rand(k + 1)) * scale;
    sprite(at(focus, (rand(k + 2) - .5) * .4, (rand(k + 3) - .3) * .5), size, size * 1.2,
      Ink.withAlpha(1 - smooth((u - .4) / .6)), blot, Y + .139 + i * .0002, rand(k + 4) * 360);
  }
  for (let i = 0; i < 30; i++) {
    const k = seed + 900 + i * 7, life = .32 + rand(k) * .25, u = age / life;
    if (u >= 1) continue;
    const a = (15 + rand(k + 1) * 150) * Math.PI / 180; // up and out, none straight down
    const d = (.6 + rand(k + 2) * 1.5) * (.7 + .5 * Math.sin(a)) * scale * (1 - Math.pow(1 - u, 3));
    const len = (.22 + rand(k + 3) * .35) * scale * (1 - .5 * u), wid = len * (.22 + rand(k + 4) * .18);
    sprite({ x: focus.x + Math.cos(a) * d, z: focus.z + Math.sin(a) * d }, wid, len,
      Ink.withAlpha(1 - smooth((u - .45) / .55)), shred, Y + .14 + i * .0003, 90 - a * 180 / Math.PI);
  }
}

// Three flecks jump from the burning pawn to the one beside it just before it catches.
function jump(a, b, s, t0) {
  const age = s - (t0 - JumpTime);
  if (age < 0 || age > JumpTime) return;
  const x0 = a.x + .3, x1 = b.x - .2;
  for (let i = 0; i < 3; i++) {
    const u = clamp((age - i * .03) / (JumpTime - .06));
    if (u <= 0 || u >= 1) continue;
    const h = .75 + .45 * Math.sin(u * Math.PI) + i * .12;
    const pos = { x: Mathf.Lerp(x0, x1, u), z: Mathf.Lerp(a.z, b.z, u) + (i - 1) * .07 + h * Lift };
    const slope = Math.atan2(.45 * Math.PI * Math.cos(u * Math.PI) * Lift, x1 - x0);
    sprite(pos, .07, .2, Ink.withAlpha(Math.sin(u * Math.PI) * 1.5), shred, Y + .135 + i * .0003, 90 - slope * 180 / Math.PI);
  }
}

// The fire on one pawn, or with cell on the ground over a disc of radius w. from is -1 or 1 when it
// caught from a neighbour on that side, 0 when the caster lit it.
function fire(key, c, w, height, t0, s, p, t, seed, from = 0, cell = false) {
  const age = s - t0;
  if (age < 0 || t0 >= t.release) return;
  const rel = 1 - smooth((s - t.release) / Sink);
  const clock = age * p.speed, lastBirth = (t.release - t0) * p.speed;
  // Each part catches after a delay: from the chest outward on a cast, from the touching side on a spread.
  const delayOf = x => from ? clamp((-from * x / w + 1) / 2) * .3 : Math.abs(x) / w * .08;
  const catchAt = (x, a) => smooth((a - delayOf(x)) / p.rise);
  const surge = from ? 1 : 1 + .3 * Math.exp(-Math.pow((age - p.rise) / .12, 2));

  // Floor: the pool under the fire. It keeps a third of its black after release, over the scorch.
  // The pool is a dark stain, not solid black, so black flames standing in it still read.
  const stain = smooth(age / .25), poolAlpha = (cell ? .5 : .65) * stain * (.5 + .5 * rel);
  if (cell) {
    sprite(c, w * 2.4, w * 2.4, Scorch.withAlpha(.6 * stain), puff, Floor + .018);
    sprite(c, w * 1.6, w * 1.6, Ink.withAlpha(poolAlpha), blot, Floor + .02, seed % 360);
    for (let i = 0; i < 10; i++) {
      const a = i * 2.399, q = at(c, Math.cos(a) * w * .62, Math.sin(a) * w * .62);
      sprite(q, w * .95, w * .95, Ink.withAlpha(poolAlpha), blot, Floor + .0202 + i * .0001, (seed + i * 71) % 360);
    }
  } else {
    sprite(c, w * 2.3, w * 2.3, Scorch.withAlpha(.6 * stain), puff, Floor + .018);
    sprite(at(c, 0, -.05), w * 1.9, w * 1.9, Ink.withAlpha(poolAlpha), blot, Floor + .02, seed % 360);
  }
  if (!from) eruption(c, age, seed, cell ? 1.3 : 1);

  if (rel > 0) {
    // The pawn inside: darkened, with blotches clinging to it and licking upward.
    if (!cell) {
      const cover = catchAt(0, age) * rel;
      sprite(at(c, 0, .3), .5, .9, Ink.withAlpha(.3 * cover), soft, pawnLayer + .003);
      for (let i = 0; i < Blotches; i++) {
        const k = seed + 500 + i * 11, head = i >= 6;
        const bx = (rand(k) - .5) * (head ? .2 : .36), bz = head ? .46 + rand(k + 1) * .2 : -.06 + rand(k + 1) * .46;
        const g = catchAt(bx, age) * rel;
        if (g <= 0) continue;
        const period = .7 + rand(k + 2) * .5, u = ((clock + rand(k + 3) * period) / period) % 1;
        const size = (head ? .17 : .21) * (.8 + .4 * rand(k + 4)) * (1 - .35 * u) * g;
        sprite(at(c, bx + Math.sin(clock * 3 + k) * .02, bz + u * .14), size, size * 1.15,
          Ink.withAlpha(Math.min(1, Math.sin(u * Math.PI) * 2.2) * .95), blot, pawnLayer + .004 + i * .0002, (k * 47 + clock * 40) % 360);
      }
    }

    // Strands and broad base tongues, drawn north to south so nearer ones overlap.
    const parts = [], total = cell ? CellStrands + CellTongues : Strands + BaseTongues;
    for (let i = 0; i < total; i++) {
      const k = seed + i * 13, base = i >= (cell ? CellStrands : Strands);
      let x, dz, rootH = 0;
      if (cell) {
        const n = base ? i - CellStrands : i, count = base ? CellTongues : CellStrands;
        const a = n * 2.399 + (base ? 1.1 : 0), r = Math.sqrt((n + .5) / count) * w * (base ? .7 : .92);
        x = Math.cos(a) * r; dz = Math.sin(a) * r;
      } else {
        // Base tongues sit at fixed, uneven places across the feet so they never line up into a block.
        x = base ? [-.62, -.2, .12, .5, -.4][i - Strands] * w : (rand(k) * 2 - 1) * w * .8;
        dz = base ? [.06, -.08, .1, -.04, -.12][i - Strands] : (rand(k + 1) - .5) * .3;
        if (!base && rand(k + 2) > .7) rootH = .2 + rand(k + 3) * .6; // a few start on the body, not the floor
      }
      parts.push({ i, k, base, x, dz, rootH });
    }
    parts.sort((a, b) => b.dz - a.dz);
    parts.forEach((q, order) => {
      const { i, k, base, x, dz, rootH } = q;
      const env = Math.sqrt(Math.max(0, 1 - (x / w) ** 2)), tall = rand(k + 4);
      const full = height * (cell ? .7 : 1) * (base ? .18 + .22 * tall : (.35 + .65 * env) * (.45 + .55 * tall));
      const hw = base ? .09 + .06 * rand(k + 5) : .035 + .06 * (1 - tall);
      const period = base ? .4 + rand(k + 6) * .2 : .55 + rand(k + 6) * .4, offset = rand(k + 7) * period;
      const cyc = cycleOf(clock, period, offset);
      const g = catchAt(x, age);
      const h = full * cyc.k * g * surge * rel;
      const root = at(c, x, dz, rootH);
      const lean = x / w * (base ? .25 : .1) + .03 * Math.sin(clock * 1.3 + k);
      const behind = !cell && rootH === 0 && dz > .02;
      const layer = (behind ? pawnLayer - .02 : Y + .03) + order * .0004;
      strand(`${key} strand ${i}`, root, h, hw * (.5 + .5 * rel), clock, k, Math.min(1, g * 3), layer, lean, rootH > 0 ? .2 : .6);

      // The torn-off top of this strand's last two cycles, rising as a fleck. None are born after release.
      if (base || p.particles <= 0) return;
      for (const n of [cyc.n, cyc.n - 1]) {
        const tear = (n + .8) * period - offset, life = .5 + rand(k + n * 31) * .3, fu = (clock - tear) / life;
        if (tear < 0 || tear > lastBirth || fu < 0 || fu >= 1) continue;
        const kk = k + n * 31, top = full * catchAt(x, tear / p.speed);
        const pos = at(c, x + lean * top + (rand(kk + 1) - .5) * .25 * fu + Math.sin(fu * 5 + kk) * .05, dz,
          rootH + top * .85 + fu * (.5 + rand(kk + 2) * .6) * height / 2.4);
        // About twice as tall as wide: a torn piece, not a leaf.
        const fw = Math.max(.08, hw * 2) * (.8 + .5 * rand(kk + 3)) * (1 - .45 * fu);
        sprite(pos, fw, fw * (1.5 + .7 * rand(kk + 4)) * (1 - .2 * fu),
          Ink.withAlpha((1 - smooth((fu - .45) / .55)) * Math.min(1, p.particles)), shred, Y + .128 + (i % 20) * .0002,
          (rand(kk + 5) - .5) * 70 + Math.sin(fu * 4 + kk) * 25);
      }
    });
  }

  // Loose flecks rising out of the flames. Births stop at release; flecks in the air finish.
  const loose = Math.round(LooseFlecks * p.particles * (cell ? 1.6 : 1));
  for (let i = 0; i < loose; i++) {
    const k = seed + 300 + i * 17, period = .6 + rand(k) * .35, offset = rand(k + 1) * period;
    const n = Math.floor((Math.min(clock, lastBirth) - offset) / period);
    if (n < 0) continue;
    const born = offset + n * period, u = (clock - born) / period;
    if (u < 0 || u >= 1) continue;
    const r = k + n * 31, x = (rand(r) * 2 - 1) * w * .8;
    const z0 = cell ? (rand(r + 5) - .5) * w * 1.6 : (rand(r + 5) - .5) * .2;
    const h0 = height * (cell ? .3 : .5) * (.4 + .6 * rand(r + 2)) * catchAt(x, born / p.speed);
    const size = .05 + rand(r + 4) * .07;
    sprite(at(c, x + Math.sin(u * 5 + r) * .1, z0, h0 + u * (.8 + rand(r + 3) * .8)), size * (1 - .5 * u), size * 2 * (1 - .4 * u),
      Ink.withAlpha(Math.min(1, Math.sin(u * Math.PI) * 1.4)), shred, Y + .13 + i * .0002, Math.sin(u * 5 + r) * 35);
  }

  // After release: a last puff of grey smoke rises and thins out.
  const since = s - t.release;
  if (since >= 0 && since < SmokeLife) {
    for (let i = 0; i < 6; i++) {
      const k = seed + 700 + i * 5, u = clamp((since - i * .04) / (SmokeLife - .2));
      if (u <= 0 || u >= 1) continue;
      const spread = cell ? w * 1.4 : w * 1.2, size = (.35 + u * .6) * (cell ? 1.4 : 1);
      sprite(at(c, (rand(k) - .5) * spread, cell ? (rand(k + 2) - .5) * spread : 0, (cell ? .4 : 1.1) + u * (1.2 + rand(k + 1))), size * 1.3, size * 1.3,
        Smoke.withAlpha(.22 * Math.sin(u * Math.PI)), puff, Y + .12 + i * .0002);
    }
  }
}

export default {
  kit: 'Rinnegan', label: 'Amaterasu (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[1], options: Scenarios, group: 'Showcase' },
    distance: P('Caster distance (cells)', 6, 3, 12, .5, 'Showcase'),
    release: { label: 'Caster releases it', value: true, group: 'Showcase' },
    gazeMark: { label: 'Mangekyo mark on the target (not in the anime)', value: false, group: 'Showcase' },
    releaseAt: P('Release after ignite', 3.5, 1, 8, .25, 'Timing (s)'),
    burn: P('Burns out after (showcase)', 5, 2, 20, .5, 'Timing (s)'),
    rise: P('Catch time per part', .12, .05, .5, .01, 'Timing (s)'),
    spreadAt: P('Neighbour catches after ignite', 1.5, .5, 4, .25, 'Timing (s)'),
    height: P('Flame height (cells up)', 2.4, 1, 4, .1, 'Flame'),
    width: P('Flame half-width on a pawn (cells)', .55, .35, 1.2, .05, 'Flame'),
    speed: P('Flame speed', 1, .3, 2.5, .1, 'Flame'),
    particles: P('Flecks', 1, 0, 2, .1, 'Flame'),
    radius: P('Cell fire radius (cells)', 1.5, .8, 2, .1, 'Flame'),
    dim: P('Screen dim on ignite (0-1)', .18, 0, .7, .05, 'Flame'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Gaze', t: 0 }, { name: 'Ignite', t: t.ignite },
    ...(p.scenario === Scenarios[1] ? [{ name: 'Spreads', t: t.ignite + p.spreadAt }] : []),
    ...(p.release ? [{ name: 'Release', t: t.release }] : []),
  ]; },
  events(p) { return [{ t: times(p).ignite, type: 'shake', value: ShakeSize }]; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const caster = { x: o.x - p.distance, z: o.z }, cell = p.scenario === Scenarios[2];
    const neighbour = { x: o.x + 1, z: o.z }, spreads = p.scenario === Scenarios[1];

    figure(caster, CasterColour, 1, 0, sun, strength);
    if (!cell) figure(o, EnemyColour, 1, 0, sun, strength);
    if (spreads) figure(neighbour, Ally, 1, 0, sun, strength);

    // The gaze: a red glint on the caster's eye while it looks; after the cast that eye bleeds.
    const eye = { x: caster.x + .04, z: caster.z + .62 };
    if (s < t.ignite + .08) redStar(eye, .16 + .08 * clamp(s / Gaze), s < t.ignite ? .5 + .5 * s / Gaze : 1 - (s - t.ignite) / .08);
    if (s >= t.ignite) bleed(eye, s - t.ignite);
    if (p.gazeMark) {
      if (s < t.ignite) mark('amaterasu', o, smooth(s / (Gaze * .7)), s * 6, .9);
      else if (s < t.ignite + .1) mark('amaterasu', o, 1 + (s - t.ignite) * 6, t.ignite * 6, 1 - (s - t.ignite) / .1);
    }

    // The fires. A pawn burns on its own cell; the cell fire covers 3x3.
    if (cell) fire('amaterasu cell', o, p.radius, p.height, t.ignite, s, p, t, 100, 0, true);
    else fire('amaterasu pawn', o, p.width, p.height, t.ignite, s, p, t, 100);
    if (spreads) {
      const catches = t.ignite + p.spreadAt;
      if (catches < t.release) jump(o, neighbour, s, catches);
      fire('amaterasu spread', neighbour, p.width * .9, p.height * .9, catches, s, p, t, 300, -1);
    }

    // Screen dim on ignite, so the black reads as darker than the world for a moment.
    const dimAge = s - t.ignite;
    if (p.dim > 0 && dimAge >= 0 && dimAge < DimLife) {
      const a = p.dim * (dimAge < .08 ? dimAge / .08 : 1 - smooth((dimAge - .08) / (DimLife - .08)));
      draw(MeshPool.plane10, o.x, topLayer, o.z, 400, 400, 0, new Color(0, 0, 0, a));
    }
  },
};
