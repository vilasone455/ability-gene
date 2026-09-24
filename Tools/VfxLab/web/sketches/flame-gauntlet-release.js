// Flame Gauntlet: Release — weapon ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed 2026-09-23, not agreed; the numbers are placeholders and will be XML
// fields). Spends the gauntlet's Heat (see Devour and lib/flame-gauntlet.js): a cone of fire 6
// cells long and 3 wide along the aim, 16 cells, rising out of the ground from the fist outward.
// Every cell it reaches is set on fire, 1 Heat per cell, nearest first, so with less than 16 Heat
// the cone is short: 8 Heat lights the first 8 cells and the wave dies there. Needs at least 5
// Heat or it is refused ("too cold", a pale puff). A pawn in a lit cell catches fire. The fires
// then burn as normal map fire; the wearer is immune to burns, so they can stand in their own
// cone. Cooldown 10 s. The wearer's Overheating state ends as soon as Heat drops under 15.
//
// Order (times with the default sliders, 20 Heat at the start, an enemy 4 cells out):
//   0.00  rest: the gauntlet closed at the hip, its plate glowing yellow-white (no fire on the
//         gauntlet itself; Overheating: red pulsing halo, smoke off the vents), meter at 20
//   0.20  wind-up 0.3 s: the fist comes forward; the glow slides down the arm into the fist,
//         builds at the knuckles and lights the floor under it; a red outline on the floor shows
//         the true cone
//   0.50  release (camera shake): a white flash at the knuckles; five licks of flame fan out of
//         the fist and dive onto the floor 0.8 cells ahead in 0.05 s, where a hot ring runs out;
//         the licks leave the fist and are gone by 0.82. Pale exhaust blows back out of both
//         elbow vents (0.7 s); a thin wisp of smoke curls off the knuckles after
//   0.55  wave: a front of fire runs along the floor at 10 cells/s (6 cells in the "Fire runs the
//         cone" time), as wide as the lit cells where it is: 1 cell at row 1, fanning to 3 by
//         row 2. Two rows of tongues leaning forward along a bowed line (the middle 0.3 ahead),
//         light on the floor round it and a cell behind it, sparks thrown ahead, soot rising
//         behind it. Each cell lights as the front reaches it: a flash, a burst of embers, the
//         fire flaring 1.5x tall and settling over 0.6 s, and a low sheet of flame across the
//         cell that joins it to its neighbours and dies down over 1.4 s. The meter drops one
//         per cell; the plate cools as it goes. The enemy's cell lights and the enemy is on fire
//   1.17  the front reaches the end of the lit cells and sinks in 0.2 s (with less Heat this
//         happens earlier and nearer)
//   1.37  result: 16 cells burning, meter at 4, the plate back to dull red, the red halo gone
//   1.57  the hand goes back to the hip; the sheet has died down to 16 separate cell fires; the
//         soot thins out by about 3.0; the cells and the enemy burn on
//   3.37  end
// Under 5 Heat ("too cold"): the fist comes forward, the knuckles flash dull red and go dark, a
// pale puff coughs out of the fist and three sparks drop to the floor. Nothing reaches the cone.
// (Devour's shared refused() puff is dark grey and hard to see on the iron; this one is pale.)
//
// Drawing: the weapon and every piece of the release are lib/flame-gauntlet.js (jet, wave,
// waveSmoke, sheet, exhaust, wisp). The cone cells are 1-cell squares laid in the aim frame, so
// at a diagonal aim they are not on the map grid; in game the cone is rasterised to map cells
// and the count changes a little. In game the cell fires after the result are vanilla Fire
// things; the ability draws the jet, the wave, the flares, the sheet and the smoke. Nothing
// needs a per-facing method: the wave is floor light plus tongues that rise north, and its
// forward lean is capped so a south aim does not flatten the tongues. The enemy's own cell fire
// draws under the pawn layer so the burning pawn stays visible. Caster and enemy are the Chain
// Sickle stand-ins.
import { Mathf } from '../js/engine.js';
import { P, Y, Floor, Lift, sprite, band, glow } from './lib/six-paths-impact.js';
import {
  frame, figure, gauntlet, fireCell, burningPawn, tally, overheating, jet, wave, waveSmoke, sheet, exhaust, wisp,
  MinRelease, OverheatAt, ConeLength, ConeWidth, HandH, Wearer, Enemy, Ember, Flame, Core, Steam, Lead, Tail, JetLand, bump, easeOut, rand, pawnLayer, puff,
} from './lib/flame-gauntlet.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const Back = 3;                  // wearer to the origin cell, along the aim
const EnemyAt = 4;               // the enemy's cell along the aim, from the wearer
const Start = .8;                // the licks land and the wave starts, cells along from the wearer
const Bow = .3;                  // the middle of the front runs this far ahead of its edges
const Catch = .3;                // a cell lights when the front is this far short of its centre
const Flare = .5, FlareFor = .6; // a lit cell's fire starts 1.5x tall and settles over this long
const Sheet = 1.4;               // the low sheet of flame across a lit cell dies down over this long
const Sink = .2;                 // the front sinking where it stops
const Close = .3;                // the hand back to the hip

// The cone cells in order of cost: along the aim first, the centre of each row before its sides.
function coneCells() {
  const out = [];
  for (let a = 1; a <= ConeLength; a++) for (const k of a === 1 ? [0] : [0, -1, 1]) {
    if (Math.abs(k) > (ConeWidth - 1) / 2) continue;
    out.push({ along: a, across: k });
  }
  return out;
}
const Cone = coneCells();

// The across extent of the lit cells in each row, rows[a] = { lo, hi }, for the first `lit` cells.
function litRows(lit) {
  const rows = [];
  for (let i = 0; i < lit; i++) {
    const c = Cone[i], r = rows[c.along] ?? (rows[c.along] = { lo: c.across, hi: c.across });
    r.lo = Math.min(r.lo, c.across); r.hi = Math.max(r.hi, c.across);
  }
  return rows;
}
// The lit width at distance d along the aim, cell edges included. Blends from one row to the
// next so the front fans out, and narrows to 0.5 at the fist before row 1.
function spanAt(rows, d) {
  const last = rows.length - 1, a0 = Math.min(Math.max(1, Math.floor(d)), last), a1 = Math.min(a0 + 1, last);
  const k = d < 1 ? 0 : smooth(clamp(d - a0));
  const lo = lerp(rows[a0].lo, rows[a1].lo, k) - .5, hi = lerp(rows[a0].hi, rows[a1].hi, k) + .5;
  if (d >= 1) return { lo, hi };
  const mid = (lo + hi) / 2, pinch = clamp((d - Start) / (1 - Start));
  return { lo: lerp(mid - .25, lo, pinch), hi: lerp(mid + .25, hi, pinch) };
}

function times(p) {
  const go = Lead + p.windup, lit = p.start >= MinRelease ? Math.min(Cone.length, p.start) : 0;
  const speed = ConeLength / p.spread, reach = lit ? Cone[lit - 1].along + .5 : 0;
  const stop = lit ? go + JetLand + (reach - Start) / speed : go;
  const result = lit ? stop + Sink : go + .3;
  return { go, lit, speed, reach, stop, result, end: result + p.hold + Tail };
}
// When cell i lights: the bowed front comes within Catch of its centre.
const eruptAt = (t, i) => {
  const c = Cone[i];
  return t.go + JetLand + Math.max(0, c.along - Catch + Bow * Math.pow(c.across / ((ConeWidth - 1) / 2 + .5), 2) - Start) / t.speed;
};

export default {
  kit: 'Flame Gauntlet', label: 'Release (sketch)',
  params: {
    actors: { label: 'Show wearer', value: true, group: 'Showcase' },
    enemy: { label: 'Enemy in the cone', value: true, group: 'Showcase' },
    showTally: { label: 'Show heat meter', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    start: P('Heat at the start', 20, 0, 20, 1, 'Showcase'),
    windup: P('Wind-up', .3, .1, .8, .05, 'Timing (s)'),
    spread: P('Fire runs the cone', .6, .2, 1.5, .05, 'Timing (s)'),
    hold: P('Show the result', 1.6, .3, 4, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    if (!t.lit) return [{ name: 'Rest', t: 0 }, { name: 'Wind-up', t: Lead }, { name: 'Too cold', t: t.go }, { name: 'Result', t: t.result }];
    return [{ name: 'Rest', t: 0 }, { name: 'Wind-up', t: Lead }, { name: 'Release', t: t.go }, { name: 'Wave', t: t.go + JetLand },
      { name: 'Stops', t: t.stop }, { name: 'Result', t: t.result }];
  },
  events(p) {
    const t = times(p);
    return t.lit ? [{ t: t.go, type: 'shake', value: .05 }, { t: t.go, type: 'sound', def: 'RimArt_FlameRelease' }]
      : [{ t: t.go, type: 'sound', def: 'RimArt_FlameRefused' }];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const f = frame(p.aim, sun), { ground } = f, dir = { x: f.ca, z: f.sa }, side = { x: -f.sa, z: f.ca };
    const caster = ground(o, -Back, 0), place = (a, k) => ground(caster, a, k);
    const cell = c => place(c.along, c.across);
    const rows = litRows(t.lit);

    // Heat now: the start less one per cell that has lit.
    let heat = p.start;
    for (let i = 0; i < t.lit; i++) heat -= clamp((s - eruptAt(t, i)) / .15);
    const over = heat >= OverheatAt ? 1 : 0;

    // The hand: forward from the wind-up, a recoil at the release, back at the result.
    const raise = smooth((s - Lead) / p.windup), closing = smooth((s - t.result) / Close);
    const fwd = raise * (1 - closing), recoil = t.lit ? .08 * bump((s - t.go) / .25) : 0;
    const hand = ground(caster, lerp(.12, .34, fwd) - recoil, lerp(.18, .08, fwd));
    const lean = t.lit ? fwd * (1 - smooth((s - t.go - p.spread) / .3)) : fwd * (1 - smooth((s - t.go) / .2));

    // Floor first: the cone outline while it matters.
    const live = smooth((s - Lead) / .2) * (1 - smooth((s - t.result) / .3));
    if (live > 0) {
      // The true footprint: the single first cell, then the 3-wide block from row 2 to row 6.
      const half = (ConeWidth - 1) / 2 + .5, c = (a, k) => place(a, k);
      const pts = [c(.5, -.5), c(1.5, -.5), c(1.5, -half), c(ConeLength + .5, -half), c(ConeLength + .5, half), c(1.5, half), c(1.5, .5), c(.5, .5), c(.5, -.5)];
      const col = Ember.withAlpha(.45 * live), L = Floor + .03;
      for (let i = 0; i + 1 < pts.length; i++) {
        const a = pts[i], b = pts[i + 1], dx = b.x - a.x, dz = b.z - a.z, len = Math.hypot(dx, dz) || 1, w = .02;
        band(`release outline ${i}`, [{ x: a.x - dz / len * w, z: a.z + dx / len * w }, { x: b.x - dz / len * w, z: b.z + dx / len * w }],
          [{ x: a.x + dz / len * w, z: a.z - dx / len * w }, { x: b.x + dz / len * w, z: b.z - dx / len * w }], col, L);
      }
    }

    // Every lit cell: a flash, a burst of embers, its fire flaring tall and settling, and the low
    // sheet of flame that joins it to its neighbours for a moment. The enemy's cell burns under
    // the pawn layer so the pawn stays in front of its own fire.
    const enemyIdx = Cone.findIndex(c => c.along === EnemyAt && c.across === 0);
    for (let i = 0; i < t.lit; i++) {
      const age = s - eruptAt(t, i);
      if (age < 0) continue;
      const c = cell(Cone[i]), under = p.enemy && i === enemyIdx;
      fireCell(`release cell ${i}`, c, s, clamp(age / .1), i + 1, under ? pawnLayer - .02 : Y + .03, Flare * (1 - smooth(age / FlareFor)));
      if (age < Sheet) sheet(`release sheet ${i}`, c, s, 1 - smooth(age / Sheet), i + 1, under ? pawnLayer - .025 : Y + .025);
      if (age < .25) sprite(c, 1.3, 1.1, Core.withAlpha(.45 * (1 - age / .25)), glow, Floor + .025);
      if (age < .5) for (let k = 0; k < 8; k++) {
        const a = k * .785 + rand(i * 9 + k) * .5, d = age * (1.2 + rand(i * 9 + k + 50)), h = age * 2.4 - age * age * 4.5;
        const pt = { x: c.x + Math.cos(a) * d * .5, z: c.z + Math.sin(a) * d * .35 + Math.max(0, h) * Lift }, fade = 1 - age / .5;
        sprite(pt, .16, .14, Flame.withAlpha(.3 * fade), glow, Y + .119);
        sprite(pt, .08, .08, Core.withAlpha(.95 * fade), glow, Y + .12);
      }
    }
    if (t.lit) waveSmoke('release soot', place, t.go + JetLand, t.speed, Start, t.reach, d => spanAt(rows, d), s);

    // The enemy: standing in the cone, on fire once its cell lights.
    if (p.enemy) {
      const e = place(EnemyAt, 0);
      figure(e, Enemy, sun, strength);
      const lit = enemyIdx < t.lit ? clamp((s - eruptAt(t, enemyIdx)) / .2) : 0;
      burningPawn('release enemy', e, s, lit, 9);
    }

    if (p.showTally) tally('release', caster, heat, p.aim);
    if (p.actors) figure(caster, Wearer, sun, strength);
    overheating('release', caster, s, over, 0, 0);

    const g = gauntlet('release gauntlet', hand, p.aim, heat, s, sun, strength, { open: 0, lean });

    // The wind-up: the glow at the knuckles, and the light it throws on the floor under the fist.
    const charge = raise * (1 - smooth((s - t.go) / (t.lit ? .08 : .2))) * (1 + .12 * Math.sin(s * 41));
    if (charge > 0) {
      sprite(g.fist, .5 * charge + .2, .42 * charge + .18, Flame.withAlpha(.55 * charge), glow, Y + .15);
      sprite(g.fist, .22 * charge, .2 * charge, Core.withAlpha(.9 * charge), glow, Y + .151);
      sprite(hand, 1.1 * charge + .2, .95 * charge + .2, Flame.withAlpha(.25 * charge), glow, Floor + .02);
    }

    if (t.lit) {
      // The release: the licks from the fist, the vents dumping heat, the knuckles smoking after.
      const age = s - t.go;
      jet('release jet', hand, place(Start, 0), side, age, s);
      exhaust('release exhaust', g, age);
      wisp(g.fist, age - .3);
      // The wave: from where the licks land to the end of the lit cells, then it sinks.
      const D = Math.min(t.reach, Start + (s - t.go - JetLand) * t.speed);
      const amount = s < t.go + JetLand ? 0 : 1 - smooth((s - t.stop) / Sink);
      if (amount > 0) wave('release wave', place, D, spanAt(rows, D), dir, s, amount, Start, Bow);
    } else {
      // Too cold: the knuckles flash dull red and go dark, a pale puff coughs out of the fist,
      // and three sparks drop off them to the floor and go out. Nothing reaches the cone.
      const age = s - t.go;
      if (age >= 0 && age < .2) sprite(g.fist, .4, .35, Ember.withAlpha(.6 * (1 - age / .2)), glow, Y + .15);
      for (let k = 0; k < 3; k++) {
        const u = (age - k * .07) / .6;
        if (u <= 0 || u >= 1) continue;
        const q = place(.45 + .35 * easeOut(u), (k - 1) * .08 * u);
        sprite({ x: q.x, z: q.z + (HandH + .25 * u) * Lift }, .18 + u * .35, .16 + u * .3, Steam.withAlpha(.5 * (1 - u)), puff, Y + .17 + k * 1e-4);
      }
      for (let k = 0; k < 3; k++) {
        const u = (age - k * .05) / .35;
        if (u <= 0 || u >= 1) continue;
        const q = place(.4 + .15 * u + k * .04, .06 * (k - 1)), h = HandH * (1 - u * u);
        sprite({ x: q.x, z: q.z + h * Lift }, .16, .14, Flame.withAlpha(.35 * (1 - u)), glow, Y + .119);
        sprite({ x: q.x, z: q.z + h * Lift }, .08, .08, Core.withAlpha(.9 * (1 - u)), glow, Y + .12);
      }
    }
  },
};
