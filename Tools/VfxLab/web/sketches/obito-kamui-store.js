// Obito — Kamui: Store (absorb and release are one ability). A picture for the agreed mechanic;
// nothing in Source/RimArt draws it yet.
//
// Mechanic (agreed 2026-09-27, numbers are XML placeholders):
//   Absorb: touch range, a pawn up to body size 1.2 or one item stack, warm-up 0.4 s. Instant (no
//     warm-up) on an enemy whose attack passed through him in the last 5 s. Cooldown 15 s, cast 1
//     Echo charge. Held enemies are stunned inside the dimension the whole time (no capture);
//     allies stay awake, downed allies keep bleeding.
//   Release: pick a stored thing, then a cell within 6 cells in sight. Enemies come out stunned
//     2 s. Cooldown 5 s.
//   On his death or loss of the kit everything stored comes out at his cell.
//
// Beats:
//   absorb       0.10 the Mangekyō flashes and his right hand reaches the target (0.25 s); a swirl
//                opens on his eye. 0.50 the target winds into his eye over 0.6 s: nearer parts
//                first, so it streams across the gap in a spiral. 1.10 the swirl shuts.
//   counter      he is phased; the raider's knife passes through him (0.15) and the raider gets a
//                small swirl mark over its head: "its attack passed through, absorb is instant"
//                (proposed tell, 5 s). 0.55 he turns solid (0.25 s); 0.80 the absorb fires with no
//                warm-up and the raider is gone by 1.35.
//   item         the same as absorb, on one stack (a crate stand-in).
//   release      0.10 the eye flashes and a small swirl opens on it; a swirl opens at the chosen
//                cell and the stored pawn unwinds out of it (0.30-0.75). An enemy stands there
//                stunned for 2 s; an ally is free at once. The 6-cell range shows while targeting.
//
// Drawing: lib/obito.js. Absorb bends the target's picture with vortex() centred on Obito's eye,
// so the same code draws a pawn one cell away streaming into it. The swirls are flat on the
// screen; the arm is a level strip. Stand-ins: pawns, crate, knife, the stun marker.
import { Color } from '../js/engine.js';
import {
  P, L, Edge, Skin, smooth, clamp, lerp, figure, obitoKind, eye, facingOf, vortex, twist, compose,
  swirl, shut, glint, circle, arm, rod, draw, disc, stunMark,
} from './lib/obito.js';

// Rule numbers (XML later) and decided looks.
const Range = 6, WarmUp = .4, Stunned = 2, Reach = 1.7, Lead = 1.2;
const Sides = { east: 0, north: 90, west: 180, south: 270 };
const Steel = new Color(.84, .86, .9);

// Every phase boundary per scenario.
function times(p) {
  switch (p.scenario) {
    case 'counter': return { stab: .15, solid: .55, cast: .8, pull: .85, gone: 1.35, end: 1.9 };
    case 'release raider': return { cast: .1, open: .2, out: .3, landed: .75, end: .75 + Stunned + .2 };
    case 'release ally': return { cast: .1, open: .2, out: .3, landed: .75, end: 1.5 };
    default: return { cast: .1, pull: .1 + WarmUp, gone: .1 + WarmUp + .6, end: 1.5 };
  }
}

export default {
  kit: 'Obito', label: 'Kamui: Store (sketch)',
  params: {
    scenario: { label: 'Show', value: 'absorb raider', options: ['absorb raider', 'counter', 'absorb item', 'release raider', 'release ally'], group: 'Showcase' },
    side: { label: 'Target side (absorb)', value: 'east', options: ['east', 'north', 'west', 'south'], group: 'Showcase' },
    aim: P('Release direction (degrees)', 30, 0, 360, 5, 'Showcase'),
    distance: P('Release distance (cells)', 4, 1, Range, 1, 'Showcase'),
    turns: P('Turns before it is gone', 1.6, .5, 3, .1, 'Shape'),
    size: P('Swirl radius (cells)', .55, .3, 1, .05, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    if (p.scenario.startsWith('release')) return [{ name: 'Target cell', t: 0 }, { name: 'Unwinds out', t: t.out }, { name: 'Out', t: t.landed }];
    if (p.scenario === 'counter') return [{ name: 'Knife passes through', t: t.stab }, { name: 'Solid', t: t.solid }, { name: 'Instant absorb', t: t.cast }];
    return [{ name: 'Reach (warm-up 0.4 s)', t: t.cast }, { name: 'Winds into the eye', t: t.pull }, { name: 'Shut', t: t.gone }];
  },
  events() { return []; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    if (p.scenario.startsWith('release')) return release(s, p, t, o, sun, strength);

    const deg = Sides[p.side], a = deg * Math.PI / 180, f = { x: Math.round(Math.cos(a)), z: Math.round(Math.sin(a)) };
    const facing = facingOf(deg), e = eye(o, facing), target = { x: o.x + f.x, z: o.z + f.z };
    const kind = p.scenario === 'absorb item' ? 'crate' : 'enemy';
    const counter = p.scenario === 'counter';

    // Obito: phased at the start of the counter, solid for the cast.
    const ph = counter ? 1 - smooth((s - t.solid) / .25) : 0;
    const warps = [];
    if (counter) {
      const u = (s - t.stab) / .26;
      warps.push(twist({ x: o.x, z: o.z + .3 }, .4 * Math.sin(Math.PI * clamp(u)), .45));
      if (u >= 0 && u <= 1) {
        const side = lerp(.3, -.3, smooth(u)), n = { x: -f.z, z: f.x };
        const hand = { x: target.x - f.x * (.25 + .3 * Math.sin(Math.PI * u)) + n.x * side, z: target.z - f.z * (.25 + .3 * Math.sin(Math.PI * u)) + n.z * side + .3 };
        const tip = { x: hand.x - f.x * .5, z: hand.z - f.z * .5 };
        rod('store knife', hand, tip, .045, Steel, L.fx + .012);
        draw(disc, hand.x, L.fx + .013, hand.z, .05, .05, 0, Skin);
        swirl('store cut', { x: lerp(hand.x, tip.x, .8), z: lerp(hand.z, tip.z, .8) }, .14, -s * 16, .8, { arms: 3, wind: .7, haze: .5 });
      }
    }
    figure('store obito', obitoKind(facing), o, sun, strength, { alpha: lerp(1, .5, ph), warp: compose(...warps) });
    if (counter) swirl('store phase eye', e, .28, -s * 2.2, .5 * ph, { arms: 3, haze: .4 });

    // The target winds into Obito's eye.
    const k = Math.pow(clamp((s - t.pull) / (t.gone - t.pull)), 1.15);
    if (k < 1) {
      figure('store target', kind, target, sun, strength, { warp: vortex(e, k, { turns: p.turns, reach: Reach, lead: Lead }), shadow: 1 - k });
      // The counter's tell: this raider's attack passed through him.
      if (counter && s > t.stab + .25) swirl('store mark', { x: target.x, z: target.z + .98 }, .12, -s * 3, .85 * (1 - k * 3), { arms: 3, haze: 0 });
    }

    // His right hand reaches the target, holds while it starts to go, then comes back.
    const right = { x: f.z, z: -f.x }, layer = facing === 'north' ? L.pawn - .01 : L.pawn + .01;
    const shoulder = { x: o.x + right.x * .15 + f.x * .05, z: o.z + right.z * .15 + f.z * .05 + .36 };
    const touch = { x: target.x - f.x * .18, z: target.z - f.z * .18 + .3 };
    const reachTime = counter ? .1 : .25;
    const out = smooth((s - t.cast) / reachTime) * (1 - smooth((s - t.pull - .15) / .2));
    if (out > .02) arm('store', shoulder, { x: lerp(shoulder.x, touch.x, out), z: lerp(shoulder.z, touch.z, out) }, layer);

    // The eye: flash, then the swirl the target goes into.
    glint(e, s - t.cast);
    const open = smooth((s - t.cast - (counter ? 0 : .15)) / (counter ? .1 : .3)), close = 1 - smooth((s - t.gone + .05) / .2);
    swirl('store eye', e, p.size * open * (.5 + .5 * k) * close, -(s * 4 + k * 8), open, {});
    shut(e, s - t.gone, p.size);
  },
};

function release(s, p, t, o, sun, strength) {
  const a = p.aim * Math.PI / 180, facing = facingOf(p.aim), e = eye(o, facing);
  const cell = { x: o.x + Math.round(Math.cos(a) * p.distance), z: o.z + Math.round(Math.sin(a) * p.distance) };
  const centre = { x: cell.x, z: cell.z + .32 }, enemy = p.scenario === 'release raider';

  circle(o, Range, .35 * (1 - smooth((s - .3) / .25)), L.floor, Edge);
  figure('release obito', obitoKind(facing), o, sun, strength);
  glint(e, s - t.cast);
  const eyeOpen = smooth((s - t.cast) / .12) * (1 - smooth((s - t.cast - .25) / .2));
  swirl('release eye', e, .3 * eyeOpen, -s * 6, eyeOpen, { arms: 3 });

  // The swirl at the cell and the stored pawn unwinding out of it.
  // Behind the pawn, so what comes out is in front of the hole; it fades as the pawn comes out.
  const open = smooth((s - t.open) / .15), fade = 1 - smooth((s - t.out - .05) / (t.landed - t.out + .15));
  swirl('release cell', centre, p.size * (.6 + .4 * open) * (.7 + .3 * fade), -(s - t.open) * 6, open * fade, { layer: L.pawn - .02 });
  if (s >= t.out) {
    const k = 1 - smooth((s - t.out) / (t.landed - t.out));
    figure('release pawn', enemy ? 'enemy' : 'ally', cell, sun, strength, { warp: vortex(centre, k, { turns: p.turns, reach: 1, lead: .5 }), shadow: 1 - k });
  }
  if (enemy && s >= t.landed && s < t.landed + Stunned) stunMark('release', cell, s - t.landed, s);
}
