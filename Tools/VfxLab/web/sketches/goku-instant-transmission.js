// Instant Transmission (Shunkan Ido) — technique proposal for the Goku kit, not the game. Nothing
// in Source/RimArt draws this yet. The kit's movement: a jump to anywhere, alone or with one pawn.
//
// What it is for (from the user's draft; every number is a placeholder).
//   Two-step order. First, optionally, pick one pawn on a cell next to the caster: friendly, enemy
//   or downed. Then pick any standable cell on the map, no line of sight, no range limit. Warmup
//   0.5 s standing still (two fingers to the forehead). The caster then appears on the cell and the
//   passenger on the cell next to it, on the same side as before. A hostile passenger arrives
//   stunned 1.5 s. Cooldown 60 s.
//   The kidnap: jump next to the enemy's rocket launcher, touch them, jump both into a room of melee
//   colonists. The rescue: jump next to a downed colonist, touch them, jump to the hospital; the
//   passenger lands on the cell beside the caster, so aim the caster at the cell beside the bed.
//
// Order, with the default timings:
//   0.00  stand
//   0.30  warmup 0.5 s: a glint at the forehead, rings closing on the head (feeling for ki); at the
//         destination the floor ripples outward and the pawns standing there glow faintly. With a
//         passenger: the caster's arm reaches over, the passenger gets a white outline.
//   0.65  the caster (and passenger) flicker
//   0.80  vanish, 0.12 s: the body breaks into horizontal slices that slide left and right, stretch
//         into lines, go white and thin away. Left behind for 0.28 s: 7 horizontal speed lines, a
//         floor ring, dust. There is no line and no light between the two places.
//   0.98  arrive, 0.12 s: the same slices closing, speed lines, floor ring, dust
//   1.10  what it got you: the kidnapped enemy stands stunned among three melee colonists; the
//         downed colonist lies on the hospital bed with the doctor beside it.
//
// Drawing: the slices are quads at pawn height and always slide east-west on screen whatever the
// direction, so no per-facing method. It differs from Flying Thunder God on purpose: no gold, no
// star glint, no floor script, no line between the places. The distance in the lab is a stand-in for
// "anywhere on the map". Pawns, the bed and the rooms are stand-ins. Shared shapes are in lib/goku.js.
import { Color, Mathf, MeshPool } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, rand } from './lib/six-paths-impact.js';
import {
  KiSky, KiIce, White, Gi, Ally, Skin, EnemyColour, pawnLayer, buildingLayer, pawn, sliced, blink, ringAt, glint, stunStars, smooth, clamp,
} from './lib/goku.js';

// Decided values. The panel keeps only what is still being tuned.
const Lead = .3, Tail = 1.6, Flicker = .15, BlinkLife = .28, HostileStun = 1.5;
const Wood = new Color(.45, .31, .19), Sheet = new Color(.8, .83, .86), Pillow = new Color(.93, .94, .95);

const carries = p => p.scenario !== 'alone';
function times(p) {
  const cast = Lead, go = cast + p.warm, gone = go + p.vanish, arrive = gone + p.gap, landed = arrive + p.vanish;
  return { cast, go, gone, arrive, landed, end: landed + Tail };
}

export default {
  kit: 'Goku', label: 'Instant Transmission (sketch)',
  params: {
    scenario: { label: 'The caster', value: 'kidnaps an enemy', options: ['alone', 'kidnaps an enemy', 'rescues a downed colonist'], group: 'Showcase' },
    aim: P('Direction of the jump (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Distance shown (cells; the real range is the whole map)', 10, 5, 20, .5, 'Showcase'),
    warm: P('Warmup (fingers to the forehead)', .5, .2, 1.5, .05, 'Timing (s)'),
    vanish: P('Body slices away / closes up', .12, .05, .4, .01, 'Timing (s)'),
    gap: P('Nobody is anywhere', .06, 0, .3, .01, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [{ name: 'Stand', t: 0 }, { name: 'Fingers to the forehead', t: t.cast }, { name: 'Vanish', t: t.go }, { name: 'Arrive', t: t.arrive }, { name: 'Result', t: t.landed }]; },
  events() { return []; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a);
    const place = (base, along, across = 0) => ({ x: base.x + along * ca - across * sa, z: base.z + along * sa + across * ca });
    const home = place(o, -p.distance / 2), dest = place(o, p.distance / 2);
    const kidnap = p.scenario === 'kidnaps an enemy', rescue = p.scenario === 'rescues a downed colonist';
    const Side = -1;   // the passenger's cell, across the jump direction
    const sensing = smooth((s - t.cast) / .15) * (1 - smooth((s - t.go) / .1));

    // --- the destination: a hospital bed, or the melee colonists waiting ------------------------------------------
    const waiting = [];
    if (kidnap) [[1, -1], [-1, -1], [0, -2]].forEach(([along, across]) => waiting.push(place(dest, along, across)));
    if (rescue) {
      const bed = place(dest, .5, Side);
      draw(MeshPool.plane10, bed.x, buildingLayer, bed.z, 2, .96, -p.aim, Wood);
      draw(MeshPool.plane10, bed.x, buildingLayer + .002, bed.z, 1.86, .82, -p.aim, Sheet);
      const pillow = place(dest, 1.15, Side);
      draw(MeshPool.plane10, pillow.x, buildingLayer + .004, pillow.z, .4, .6, -p.aim, Pillow);
      waiting.push(place(dest, -1, -2));
    }
    // The floor at the destination answers while the caster feels for it.
    if (sensing > 0) for (let n = 0; n < 3; n++) {
      const v = ((s - t.cast) * 1.6 + n / 3) % 1;
      ringAt(dest, .2 + v * .9, KiIce.withAlpha(.55 * (1 - v) * sensing));
    }

    // --- pawns, north first ---------------------------------------------------------------------------------------------
    const out = clamp((s - t.go) / p.vanish), back = 1 - clamp((s - t.arrive) / p.vanish), there = s >= t.arrive;
    const u = there ? back : out, shown = s < t.gone || there;
    const flick = s >= t.go - Flicker && s < t.go ? .55 + .45 * Math.sin(s * 140) : 1;
    const touch = carries(p) ? smooth((s - t.cast) / .2) * (there ? 0 : 1) : 0;
    const casterAt = there ? dest : home, riderAt = place(casterAt, 0, Side);
    const figures = waiting.map((pos, i) => ({ pos, waiting: true, i }));
    if (shown) figures.push({ pos: casterAt, caster: true });
    if (shown && carries(p)) figures.push({ pos: riderAt, rider: true });
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      if (g.waiting) {
        pawn(g.pos, Ally, sun, strength);
        sprite({ x: g.pos.x, z: g.pos.z + .3 }, 1.1, 1.3, KiSky.withAlpha(.3 * sensing * (.7 + .3 * Math.sin(s * 14 + g.i))), glow, Y + .01);
      } else if (g.caster) {
        if (u > 0) sliced(g.pos, Gi, u, sun, strength, { hair: true });
        else pawn(g.pos, Gi, sun, strength, { hair: true, alpha: flick });
      } else {
        const colour = kidnap ? EnemyColour : Ally;
        if (u > 0) sliced(g.pos, colour, u, sun, strength, { lie: rescue });
        else pawn(g.pos, colour, sun, strength, { lie: rescue, alpha: flick, outline: touch });
      }
    });
    if (kidnap && s >= t.landed) stunStars('instant transmission stun', riderAt, s, clamp((s - t.landed) / .15) * clamp((t.landed + HostileStun - s) / .2));

    // --- the caster's hands: fingers to the forehead, the other arm on the passenger ----------------------------------
    if (s >= t.cast && s < t.go) {
      const w = (s - t.cast) / p.warm, brow = { x: home.x + .07, z: home.z + .62 };
      draw(MeshPool.plane10, home.x + .14, pawnLayer + .01, home.z + .5, .06, .26, -32, Skin);
      glint('instant transmission brow', brow, .1 + .06 * Math.sin(s * 30), .9 * sensing, White, s * 90);
      for (let n = 0; n < 2; n++) {
        const v = (w * 2.5 + n / 2) % 1;
        ringAt({ x: home.x, z: home.z + .6 }, .7 * (1 - v) + .08, KiIce.withAlpha(.7 * v * sensing), Y + .03);
      }
      if (touch > 0) {
        const hand = place(home, 0, Side * .62 * touch);
        draw(MeshPool.plane10, (home.x + hand.x) / 2, pawnLayer + .012, (home.z + hand.z) / 2 + .36, .07, Math.hypot(hand.x - home.x, hand.z - home.z) + .02, -p.aim, Skin);
        sprite({ x: hand.x, z: hand.z + .36 }, .45, .45, KiIce.withAlpha(.7 * touch * (.7 + .3 * Math.sin(s * 25))), glow, Y + .02);
      }
    }

    // --- what is left behind, and what announces the arrival --------------------------------------------------------------
    blink('instant transmission leave', home, s - t.go - p.vanish * .5, BlinkLife);
    blink('instant transmission land', dest, s - t.arrive, BlinkLife);
    if (carries(p)) {
      blink('instant transmission leave rider', place(home, 0, Side), s - t.go - p.vanish * .5, BlinkLife);
      blink('instant transmission land rider', place(dest, 0, Side), s - t.arrive, BlinkLife);
    }
  },
};
