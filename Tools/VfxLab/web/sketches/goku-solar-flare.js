// Solar Flare (Taiyoken) — technique proposal for the Goku kit, not the game. Nothing in
// Source/RimArt draws this yet. The kit's setup and escape tool.
//
// What it is for (from the user's draft; every number is a placeholder).
//   No target: a burst centred on the caster. Warmup 0.25 s (hands to the face). Every pawn within
//   6.9 cells that has line of sight to the caster, except the caster and the caster's own faction
//   (they know to shut their eyes), is stunned 3.5 s and gets a hediff "Flash-blinded": Sight -80%,
//   wearing off over 20 s. No damage. A pawn behind a wall, or outside the radius, is not affected.
//   Cooldown 45 s. Use: surrounded by melee, cast it, walk away while they stand stunned.
//
// Order, with the default timings:
//   0.00  melee enemies walk in on the caster; one more is outside the radius; with the wall on,
//         one stands behind a wall inside the radius
//   0.30  warmup 0.25 s: both hands go up beside the face, light gathers at the head, 10 short rays
//         run inward to it
//   0.55  the flash: a white core at the head, a ring front that reaches the true radius in 0.12 s,
//         28 needle rays out to about the radius, a 4-point cross flare, two halo rings, a
//         yellow-white fill over the whole radius and a whiteout beyond it, all fading over 0.6 s.
//         Every pawn and the wall throw a long shadow away from the caster while it is bright, which
//         is what shows who had line of sight. Blinded pawns recoil 0.2 cells and go pale.
//   0.55  to 4.05 (stun): blinded pawns sway on the spot, an arm over the eyes, 3 stars circling the
//         head. The floor ring at the true radius stays while the stun lasts. The caster walks away
//         (with "walks away" on). The pawn outside the radius keeps walking in; the one behind the
//         wall stays as it was.
//   4.05  the stun ends: the stars go, a dark bar stays over the eyes (Sight is still reduced), and
//         the blinded pawns walk on slowly.
//
// Drawing: all level circles and lines through one point, so no per-facing method. Rays are strip
// meshes rebuilt while they show (0.6 s). Pawns and the wall are stand-ins. Shared shapes are in
// lib/goku.js.
import { Color, Mathf, Meshes, MeshPool } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import {
  White, Flare, FlareWarm, Gi, Ink, Skin, EnemyColour, pawnLayer, pawn, ringAt, glint, stunStars, streak, strip, whiteGlow, wallCell, smooth, clamp,
} from './lib/goku.js';

const disc = Meshes.disc(48, 'solar flare disc');
// Decided values. The panel keeps only what is still being tuned.
const Lead = .3, Tail = 1.2, Front = .12, Rays = 28, Gather = 10, Recoil = .2, WalkIn = 1.2, WalkOut = 1.7, WalkBlind = .5, ShadowReach = 2.6;
// Enemies as [direction from the caster (degrees), distance at the flash (cells)].
const Melee = [[20, 1.3], [75, 2.2], [-40, 1.8], [130, 3.4], [-95, 4.6], [48, 5.6]];
const OutsideBy = 2.2, WallAt = [-8, 3.2];   // the unaffected ones: outside the radius, and behind a wall

function times(p) {
  const cast = Lead, flash = cast + p.warm, wake = flash + p.stun;
  return { cast, flash, wake, end: wake + Tail };
}

export default {
  kit: 'Goku', label: 'Solar Flare (sketch)',
  params: {
    wall: { label: 'One enemy stands behind a wall (no line of sight)', value: true, group: 'Showcase' },
    walk: { label: 'The caster walks away during the stun', value: true, group: 'Showcase' },
    radius: P('Radius (cells)', 6.9, 3, 10, .1, 'Shape'),
    warm: P('Warmup (hands to the face)', .25, .1, 1, .05, 'Timing (s)'),
    fade: P('Flash fades over', .6, .2, 1.5, .05, 'Timing (s)'),
    stun: P('Stun lasts', 3.5, 1, 6, .25, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [{ name: 'Enemies close in', t: 0 }, { name: 'Hands to the face', t: t.cast }, { name: 'Flash / stunned', t: t.flash }, { name: 'Stun ends, still blind', t: t.wake }]; },
  events() { return []; },   // no camera shake: nothing is hit

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const age = s - t.flash, f = clamp(age / p.fade), bright = age >= 0 ? (1 - f) * (1 - f) : 0;
    const head = { x: o.x, z: o.z + .6 };

    // --- who is where ---------------------------------------------------------------------------------------
    const polar = (deg, d) => ({ x: o.x + Math.cos(deg * Mathf.Deg2Rad) * d, z: o.z + Math.sin(deg * Mathf.Deg2Rad) * d });
    const walked = age > .45 && p.walk ? Math.min(4.5, (age - .45) * WalkOut) : 0;
    const caster = { x: o.x - walked, z: o.z - walked * .25 };
    const figures = [{ pos: caster, caster: true }];
    Melee.forEach(([deg, d], i) => {
      if (d > p.radius) return;
      const before = Math.max(0, t.flash - s) * WalkIn, recoil = Recoil * smooth(age / .1), after = Math.max(0, s - t.wake) * WalkBlind;
      const sway = age >= 0 && s < t.wake ? Math.sin(s * 6 + i * 2) * .04 : 0;
      figures.push({ pos: polar(deg + sway * 20, Math.max(1, d + before + recoil - after)), blind: true, deg, i });
    });
    figures.push({ pos: polar(158, Math.max(1.2, p.radius + OutsideBy - (s - t.flash) * WalkIn)), deg: 158, i: 20 });
    if (p.wall) figures.push({ pos: polar(WallAt[0], WallAt[1] + 1), deg: WallAt[0], i: 21, covered: true });

    // --- the wall, three cells across the line from the caster ------------------------------------------------
    const wallCells = [];
    if (p.wall) for (let k = -1; k <= 1; k++) {
      const c = polar(WallAt[0], WallAt[1]), r = WallAt[0] * Mathf.Deg2Rad;
      wallCells.push({ x: c.x - Math.sin(r) * k, z: c.z + Math.cos(r) * k });
    }
    wallCells.forEach(c => wallCell(c, WallAt[0]));

    // --- the floor: the true radius, the fill, and the shadows the flash throws -----------------------------------
    if (s >= t.cast) {
      const warn = smooth((s - t.cast) / p.warm), left = 1 - smooth((s - t.wake) / .5);
      ringAt(o, p.radius, FlareWarm.withAlpha(.55 * warn * left), Floor + .02);
    }
    if (bright > 0) {
      draw(disc, o.x, Floor + .006, o.z, p.radius, p.radius, 0, FlareWarm.withAlpha(.3 * (1 - f)), whiteGlow);
      // Long shadows away from the light, on top of the fill. The wall's is a wedge out to the radius.
      figures.forEach(g => {
        if (g.caster) return;
        const dx = g.pos.x - o.x, dz = g.pos.z - o.z, d = Math.hypot(dx, dz) || 1, far = ShadowReach * (.6 + .4 * bright);
        streak(`solar flare shadow ${g.i}`, g.pos, { x: g.pos.x + dx / d * far, z: g.pos.z + dz / d * far }, .5, Ink.withAlpha(.5 * bright), undefined, Floor + .03, 6);
      });
      if (p.wall) {
        const r = WallAt[0] * Mathf.Deg2Rad, c = polar(WallAt[0], WallAt[1]), ends = [-1.5, 1.5].map(k => ({ x: c.x - Math.sin(r) * k, z: c.z + Math.cos(r) * k }));
        const out = q => { const dx = q.x - o.x, dz = q.z - o.z, d = Math.hypot(dx, dz); return { x: o.x + dx / d * p.radius, z: o.z + dz / d * p.radius }; };
        strip('solar flare wall shadow', [ends[0], out(ends[0])], [ends[1], out(ends[1])], Ink.withAlpha(.5 * (1 - f)), undefined, Floor + .03);
      }
    }

    // --- pawns, north first -------------------------------------------------------------------------------------------
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      if (g.caster) {
        const up = smooth((s - t.cast) / (p.warm * .7)) * (1 - smooth((age - .25) / .25));
        pawn(g.pos, Gi, sun, strength, { hair: true, arms: 2, raise: up * .55, tint: Flare, tintAmount: .8 * bright });
        return;
      }
      const hit = g.blind && age >= 0, stunned = hit && s < t.wake;
      pawn(g.pos, EnemyColour, sun, strength, { tint: Flare, tintAmount: hit ? .85 * bright : g.covered ? 0 : .35 * bright });
      if (hit) {
        // An arm over the eyes while stunned; after it, a dark bar: Sight is still reduced.
        const face = { x: g.pos.x, z: g.pos.z + .6 };
        if (stunned) draw(MeshPool.plane10, face.x, pawnLayer + .01, face.z, .34, .09, 12, Skin);
        else draw(MeshPool.plane10, face.x, pawnLayer + .01, face.z, .26, .06, 0, Ink.withAlpha(.75));
        stunStars(`solar flare stun ${g.i}`, g.pos, s + g.i, stunned ? clamp(age / .2) * clamp((t.wake - s) / .2) : 0);
      }
    });

    // --- warmup: light gathers at the head ----------------------------------------------------------------------------
    if (s >= t.cast && age < 0) {
      const w = (s - t.cast) / p.warm;
      sprite(head, .5 + w * .9, .5 + w * .9, Flare.withAlpha(.7 * w), glow, Y + .05);
      glint('solar flare gather glint', head, .1 + .25 * w, w);
      for (let i = 0; i < Gather; i++) {
        const v = (w * 1.6 + rand(i)) % 1, ang = (i * 36 + rand(i + 5) * 25) * Mathf.Deg2Rad, r0 = 1.1 * (1 - v) + .12, r1 = r0 + .3 * (1 - v);
        streak(`solar flare gather ${i}`, { x: head.x + Math.cos(ang) * r0, z: head.z + Math.sin(ang) * r0 }, { x: head.x + Math.cos(ang) * r1, z: head.z + Math.sin(ang) * r1 },
          .04, Flare.withAlpha(.9 * Math.sin(v * Math.PI)), whiteGlow, Y + .04, 2);
      }
    }

    // --- the flash ---------------------------------------------------------------------------------------------------
    if (bright > 0) {
      const open = smooth(age / Front);
      sprite(head, p.radius * 5, p.radius * 5, Flare.withAlpha(.8 * bright), glow, Y + .1);            // whiteout, past the radius
      sprite(head, p.radius * 2.3, p.radius * 2.3, White.withAlpha(.95 * bright), glow, Y + .101);
      ringAt(o, p.radius * open, White.withAlpha(.9 * (1 - f)), Y + .102, true, whiteGlow);              // the front, on the true radius
      [.34, .58].forEach((share, k) => ringAt(head, p.radius * share * (.7 + .3 * open), FlareWarm.withAlpha(.5 * bright), Y + .103 + k * .001, false, whiteGlow));
      for (let i = 0; i < Rays; i++) {
        const long = i % 4 === 0, ang = (i * 360 / Rays + rand(i + 11) * 8 + 10 * f) * Mathf.Deg2Rad;
        const reach = p.radius * (long ? 1 + .15 * rand(i + 30) : .4 + .45 * rand(i + 30)) * open * (1 - .25 * f), w = (long ? .24 : .13) * (1 - .5 * f);
        const tip = { x: head.x + Math.cos(ang) * reach, z: head.z + Math.sin(ang) * reach }, root = { x: head.x + Math.cos(ang) * .15, z: head.z + Math.sin(ang) * .15 };
        streak(`solar flare ray glow ${i}`, root, tip, w * 2.6, FlareWarm.withAlpha(.5 * bright), whiteGlow, Y + .104, 6);
        streak(`solar flare ray ${i}`, root, tip, w, White.withAlpha(Math.min(1, bright * 1.5)), whiteGlow, Y + .105, 6);
      }
      // The cross flare of the anime: one long horizontal ray, one shorter vertical one.
      [[0, 1.35, .5], [90, .9, .38]].forEach(([deg, reach, w], i) => {
        const r = deg * Mathf.Deg2Rad, l = p.radius * reach * open, dx = Math.cos(r) * l, dz = Math.sin(r) * l;
        streak(`solar flare cross ${i}`, { x: head.x - dx, z: head.z - dz }, { x: head.x + dx, z: head.z + dz }, w * (1 - .5 * f), White.withAlpha(Math.min(1, bright * 1.6)), whiteGlow, Y + .106, 10);
      });
      draw(disc, head.x, Y + .107, head.z, .5 + 1.2 * open * (1 - f), .5 + 1.2 * open * (1 - f), 0, White.withAlpha(Math.min(1, bright * 2)));
    }
  },
};
