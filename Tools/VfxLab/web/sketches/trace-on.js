// Trace On — Trace kit proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (agreed 2026-09-23 as one of the kit's three abilities: Reinforcement, Trace On,
// Unlimited Blade Works; every number is a placeholder and will be an XML field).
//   Self. The button lists the blades the pawn has studied (its trace library); the player picks
//   one. 0.6 s to cast, standing. A copy appears in the pawn's hand as its weapon: the studied
//   original's material and melee stats, one quality level below the best of that type it studied.
//   A real weapon it was holding goes to its inventory; a copy it was holding breaks. A copy breaks
//   the moment it leaves the hand (dropped, disarmed, downed, another weapon equipped), so copies
//   never lie on the ground and cannot be hauled, sold or given away. Cooldown 5 s.
//   Studying has to stay open after the trait is gained: today the study gizmo, job and float menu
//   hide once it is, and the record keeps only the weapon type, not its material or quality.
//
// Order, with the default timings ("Scenario" picks one):
//   0.20  a circuit line runs from the pawn's chest to its hand; a small flash there at 0.35.
//   0.35  a wire outline of the weapon draws out of the hand from the grip to the point, with the
//         blade's centre line and four cross lines inside it, like a drawing of its structure.
//   0.53  steel fills the outline from the grip to the point behind a bright line.
//   0.80  a glint at the point; the wire fades by 1.10 and a plain weapon is left in the hand.
//   swap copy    at 1.60 another studied blade ("Second weapon") is picked: the held copy breaks
//                into light, and the new one traces in the same way from 1.70.
//   real weapon  the pawn starts holding a real "Second weapon": from 0.20 it slides to the hip and
//                is gone (into the inventory), and the trace starts at 0.45.
//   downed       at 1.60 the pawn goes down: the copy slips out of the hand, turns in the air and
//                breaks into light before it reaches the ground; nothing is left there.
//
// Drawing: the copy is the weapon's own texture on a plane at hand height (lib/trace.js), cut along
// the blade's axis for the wire and the fill (its outline, face and mask textures). The structure
// lines run along the middle of the texture's own width table and cross it at four places, so they
// fit each weapon, curved ones too. The circuit line is Reinforcement's arm run (lib/trace.js
// Circuit). The held weapon lies flat at a fixed angle, a stand-in for the game's carry pose; facing
// west holds it in the west hand. Pawns are stand-ins.
import { Color } from '../js/engine.js';
import { P, Y, sprite, glow, soft } from './lib/six-paths-impact.js';
import { pawn, line, glint, whiteGlow, pawnLayer, shadowLayer, White, Dust } from './lib/goku.js';
import {
  smooth, clamp, D2R, Black, Trace, TraceHot, grey, Weapons, v3, dot3, unit, onScreen, alongSun, at3, clip, texPoly, Square, blade, blend, flying,
  along, partial, Circuit, heldCopy, flatAt, shatter,
} from './lib/trace.js';

const Caster = new Color(.55, .3, .24);
// Decided looks. A trace takes `cast` seconds: the arm line in its first quarter, the wire from 25 %
// to 65 %, the steel from 55 % to the end, the glint at the end; the wire fades over the next 0.3 s.
// Crosses: the width-table steps (of 16, point to pommel) the cross lines sit on.
const CastAt = .2, SwapAt = 1.6, StowTime = .25, Break = .35, Crosses = [2, 5, 8, 11];
const Rest = { east: 55, west: 125, north: 55, south: 55 };

// Every phase boundary in one place.
function times(p) {
  const trace = at => ({ at, wire: at + .25 * p.cast, wired: at + .65 * p.cast, fill: at + .55 * p.cast, lit: at + p.cast });
  if (p.scenario === 'real weapon') { const first = trace(CastAt + StowTime); return { first, end: first.lit + 1.2 }; }
  if (p.scenario === 'swap copy') { const first = trace(CastAt), second = trace(SwapAt + .1); return { first, second, end: second.lit + 1.2 }; }
  if (p.scenario === 'downed') return { first: trace(CastAt), end: SwapAt + 1.2 };
  const first = trace(CastAt);
  return { first, end: first.lit + 1.2 };
}

// The copy's structure lines: along the middle of its width table from the grip up to share f of
// its length, and across it at the Crosses steps once the wire has passed them.
function structure(key, w, b, f, colour, layer) {
  const point = (i, x) => {
    const s = w.length * (i + .5) / 16;
    return onScreen(at3(b, { u: w.tip[0] + w.axis.u * s + w.across.u * x, v: w.tip[1] + w.axis.v * s + w.across.v * x }));
  };
  const spine = [];
  for (let i = 15; i >= 0; i--) if ((i + .5) / 16 >= 1 - f) spine.push(point(i, (w.width[i][0] + w.width[i][1]) / 2));
  if (spine.length > 1) line(`${key} spine`, spine, .014, colour, whiteGlow, layer, 'none');
  Crosses.forEach(i => {
    if ((i + .5) / 16 >= 1 - f) line(`${key} cross ${i}`, [point(i, w.width[i][0] * .85), point(i, w.width[i][1] * .85)], .012, colour, whiteGlow, layer, 'none');
  });
}

// A copy tracing into the hand of the pawn standing at me: the arm line, the wire and its structure
// lines, the steel, the glint; afterwards the plain weapon. T is its trace() times.
function traceIn(key, w, b, s, T, p, sun, strength, me, side) {
  const layer = pawnLayer + .012, fade = 1 - smooth((s - T.lit) / .3);
  if (s < T.at) return;
  const armF = clamp((s - T.at) / (T.wire - T.at));
  if (fade > 0) {
    const run = Circuit[0], pts = partial(run.pts, run.length * armF).map(q => ({ x: me.x + q[0] * side, z: me.z + q[1] }));
    if (pts.length > 1) {
      line(`${key} arm halo`, pts, .08, Trace.withAlpha(.2 * p.bright * fade), whiteGlow, pawnLayer + .006, 'none');
      line(`${key} arm`, pts, .022, TraceHot.withAlpha(.9 * p.bright * fade), whiteGlow, pawnLayer + .007, 'none');
      if (armF < 1) sprite(pts[pts.length - 1], .09, .09, White.withAlpha(.9 * p.bright), glow, pawnLayer + .008);
    }
  }
  if (s < T.wire) return;
  const flash = (s - T.wire) / .2;
  if (flash < 1) sprite(onScreen(v3(me.x + .24 * side, .3, me.z + .02)), .4, .4, Trace.withAlpha(.55 * (1 - flash)), glow, layer + .007);
  if (fade > 0) {
    const wf = clamp((s - T.wire) / (T.wired - T.wire)), edge = w.length * (1 - wf);
    texPoly(`${key} wire`, clip(Square, q => along(w, q) - edge), b, onScreen, w.wire, Trace.withAlpha(.95 * p.bright * fade), layer + .004);
    structure(key, w, b, wf, Trace.withAlpha(.7 * p.bright * fade), layer + .005);
  }
  const ff = clamp((s - T.fill) / (T.lit - T.fill));
  if (ff >= 1) blade(key, b, sun, strength, layer, { upright: false });
  else if (ff > 0) {
    // the steel so far, lit and shadowed as blade() does it, and the bright line at its front
    const front = w.length * (1 - ff), steel = clip(Square, q => along(w, q) - front);
    const lit = Math.max(0, dot3(b.N, unit(v3(-sun.x, 1, -sun.z))));
    texPoly(`${key} fill shadow`, steel, b, alongSun(sun), w.face, Black.withAlpha(.42 * strength / .32), shadowLayer + .002);
    texPoly(`${key} fill`, steel, b, onScreen, w.face, grey(.8 + .2 * lit), layer + .001);
    texPoly(`${key} scan`, clip(clip(Square, q => along(w, q) - (front - .012)), q => front + .02 - along(w, q)), b, onScreen, w.mask, TraceHot.withAlpha(.85), layer + .006);
  }
  const g = s - T.lit;
  if (g >= 0 && g < .25) glint(`${key} glint`, onScreen(b.tip), .3, 1 - g / .25, TraceHot);
}

export default {
  kit: 'Trace', label: 'Trace On (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'empty hand', options: ['empty hand', 'swap copy', 'real weapon', 'downed'], group: 'Scene' },
    weapon: { label: 'Traced', value: 'LongSword', options: Object.keys(Weapons), group: 'Scene' },
    second: { label: 'Second weapon (swap, real)', value: 'Spear', options: Object.keys(Weapons), group: 'Scene' },
    facing: { label: 'Facing', value: 'east', options: Object.keys(Rest), group: 'Scene' },
    cast: P('Cast', .6, .3, 1.5, .05, 'Timing (s)'),
    size: P('Weapon size (x image)', 1.1, .7, 1.8, .05, 'Look'),
    bright: P('Wire brightness', .9, .2, 1.5, .05, 'Look'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p), T = t.first, list = [{ name: 'Arm line', t: T.at }, { name: 'Wire', t: T.wire }, { name: 'Steel', t: T.fill }, { name: 'Done', t: T.lit }];
    if (p.scenario === 'real weapon') return [{ name: 'Stow', t: CastAt }, ...list];
    if (p.scenario === 'swap copy') return [...list, { name: 'Swap', t: SwapAt }, { name: 'New copy', t: t.second.at }];
    if (p.scenario === 'downed') return [...list, { name: 'Down', t: SwapAt }];
    return list;
  },
  events() { return []; },

  draw(s, p, { origin: cell, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const me = { x: cell.x, z: cell.z + 2 }, side = p.facing === 'west' ? -1 : 1, rest = Rest[p.facing];
    const w = Weapons[p.weapon], other = Weapons[p.second], first = heldCopy(w, p.size, me, rest, side);
    const down = p.scenario === 'downed' && s >= SwapAt;

    if (p.scenario === 'real weapon' && s < CastAt + StowTime) {
      // the real weapon goes to the inventory: it slides in to the hip and is gone
      const held = heldCopy(other, p.size, me, rest, side), u = smooth(clamp((s - CastAt) / StowTime));
      const hip = flying(other, p.size, v3(me.x + .1 * side, .12, me.z - .04), { x: Math.cos((rest + 180) * D2R), z: Math.sin((rest + 180) * D2R) }, 0);
      blade('trace on real', u > 0 ? blend(held, hip, u) : held, sun, strength, pawnLayer + .012, { upright: false, alpha: (1 - u) ** 2 });
    }

    if (p.scenario !== 'downed' || s < SwapAt) {
      if (p.scenario !== 'swap copy' || s < SwapAt) traceIn('trace on a', w, first, s, t.first, p, sun, strength, me, side);
      else {
        shatter('trace on a', first, sun, strength, (s - SwapAt) / Break, 11, pawnLayer + .012);
        traceIn('trace on b', other, heldCopy(other, p.size, me, rest, side), s, t.second, p, sun, strength, me, side);
      }
    } else {
      // downed: the copy slips out of the hand, turns in the air and breaks before it lands
      const age = s - SwapAt, fly = Math.min(age, .28), m0 = at3(first, { u: (w.tip[0] + w.pommel[0]) / 2, v: (w.tip[1] + w.pommel[1]) / 2 });
      const m = v3(m0.x + side * .6 * fly / .28, .3 + 1.4 * fly - 4 * fly * fly, m0.z), a = (rest + side * 500 * fly) * D2R;
      const b = flatAt(w, p.size, m, { x: Math.cos(a), z: Math.sin(a) });
      if (age < .28) blade('trace on falling', b, sun, strength, Y + .01, { upright: false });
      else shatter('trace on a', b, sun, strength, (age - .28) / Break, 11, Y + .01);
      const u = age / .5;
      if (u < 1) sprite({ x: me.x + .2, z: me.z + .12 + u * .1 }, .5 + u * .5, .3 + u * .3, Dust.withAlpha(.4 * (1 - u)), soft, Y + .005);
    }

    const lying = down && s >= SwapAt + .1;
    pawn(me, Caster, sun, strength, { hair: !lying, lie: lying });
  },
};
