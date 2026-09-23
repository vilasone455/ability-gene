// Flame Gauntlet: Devour — weapon ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed 2026-09-23, not agreed; the numbers are placeholders and will be XML
// fields). The pawn holds the flame gauntlet as its melee weapon (about 12 blunt, a punch), so it
// costs the weapon slot: no rifle in the other hand. Devour targets a cell up to 10 cells away
// with line of sight, 0.3 s wind-up. Every burning cell within 3 cells of the target is put out
// and its fire streams into the open palm; a pawn on fire in the radius loses its fire too,
// friendlies included. Each cell adds 1 Heat, each pawn 2, up to the meter's 20. At 20 the rest
// is refused ("too hot") and keeps burning. Cooldown 6 s. Heat is what Release spends (see
// lib/flame-gauntlet.js for the whole rule). At 15 or more the wearer is Overheating: -20 % move,
// -20 % manipulation, 1 burn to the gauntlet arm every 5 s. The clip shows the first burn tick
// 0.6 s into the result so it fits; in game it lands 5 s after the state starts.
//
// Order (times with the default sliders, "base fire": nine burning cells, two more outside):
//   0.00  rest: the gauntlet closed at the hip, the fire burning, the meter at the start heat
//   0.20  wind-up 0.3 s: the arm comes forward and the fingers fan open; the palm glows; a red
//         ring on the floor shows the 3-cell radius around the target
//   0.50  pull: the nearest cell's fire lifts off, arches to the palm in 0.45 s and goes in; the
//         next cell follows 0.08 s behind. Each arrival flashes the palm and the plate glows a
//         step hotter (dull red, orange, yellow-white at 20; no fire on the gauntlet). A cell left is black scorch. The two cells outside the ring burn on
//   1.59  result: the hand closes, the plate glows orange with 9 Heat, the meter shows it
//   3.29  end
// Scenario "burning pawn": a pawn on fire at the target with four cells around it; the pawn is
// put out (2 Heat) and stays standing. Scenario "starting hot (14)": nine cells, 14 Heat at the
// start; six go in, the meter hits 20, the gauntlet snaps shut with a grey puff, three cells keep
// burning, and the wearer is Overheating: red pulsing halo on the arm, smoke off the vents, a burn on the arm.
//
// Drawing: the weapon is lib/flame-gauntlet.js (shared with Release). It lies level at hand
// height and turns with the aim, so nothing needs a per-facing method; it draws under the pawn
// layer when it points north. Fire is tongues that rise (drawn north) and sway, the same from
// every facing. Burning cells are world-grid squares; the two outside cells are rounded to the
// grid from the aim frame. Caster and target are the Chain Sickle stand-ins.
import { Color, Mathf } from '../js/engine.js';
import { P, Y, Floor, Lift, sprite, circle, soft, glow } from './lib/six-paths-impact.js';
import {
  frame, figure, gauntlet, fireCell, burningPawn, parcel, tally, overheating, refused, screen,
  MaxHeat, OverheatAt, HeatPerCell, HeatPerPawn, DevourRadius, HandH, Wearer, Enemy, Ember, Flame, Core, Smoke, Lead, Tail, bump, puff, rand,
} from './lib/flame-gauntlet.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const Gap = .08;                 // between one fire leaving and the next
const Close = .3;                // the hand closing at the result
const BurnTick = .6;             // first Overheating burn, into the result (5 s in game)
const Outside = [{ along: 0, across: 4.2 }, { along: 3.5, across: -2.5 }];   // burning cells beyond the radius, kept

// The things on fire for a scenario, in world cells around the target `o`.
function fires(p, o, f) {
  const cells = [], pawns = [];
  if (p.scenario === 'burning pawn') {
    pawns.push({ x: o.x, z: o.z });
    for (const [x, z] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) cells.push({ x: o.x + x, z: o.z + z });
  } else {
    for (let x = -1; x <= 1; x++) for (let z = -1; z <= 1; z++) cells.push({ x: o.x + x, z: o.z + z });
  }
  const outside = Outside.map(q => { const g = f.ground(o, q.along, q.across); return { x: o.x + Math.round(g.x - o.x), z: o.z + Math.round(g.z - o.z) }; });
  return { cells, pawns, outside };
}
function startHeat(p) { return p.scenario === 'starting hot (14)' ? Math.max(p.start, 14) : p.start; }

// The eat plan: every fire in the radius sorted by distance from the palm, with the ones the
// meter can take marked and their times. Time-only: the same plan for every frame.
function plan(p, o, f, palm) {
  const { cells, pawns, outside } = fires(p, o, f);
  const items = [
    ...pawns.map(c => ({ c, pawn: true, value: HeatPerPawn })),
    ...cells.map(c => ({ c, pawn: false, value: HeatPerCell })),
  ].map(it => ({ ...it, dist: Math.hypot(it.c.x - palm.x, it.c.z - palm.z) })).sort((a, b) => a.dist - b.dist);
  let heat = startHeat(p), eaten = 0;
  const pull = Lead + p.windup;
  for (const it of items) {
    if (heat + it.value > MaxHeat) break;
    it.depart = pull + eaten * Gap; it.arrive = it.depart + p.travel; heat += it.value; eaten++;
  }
  const refusedAt = eaten < items.length ? pull + eaten * Gap : -1;
  const result = eaten ? items[eaten - 1].arrive + .15 : pull + .3;
  return { items, outside, eaten, final: heat, pull, refusedAt, result, end: result + p.hold + Tail };
}

export default {
  kit: 'Flame Gauntlet', label: 'Devour (sketch)',
  params: {
    actors: { label: 'Show wearer and target', value: true, group: 'Showcase' },
    showTally: { label: 'Show heat meter', value: true, group: 'Showcase' },
    scenario: { label: 'Scenario', value: 'base fire', options: ['base fire', 'burning pawn', 'starting hot (14)'], group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Wearer to target (cells)', 4, 2, 10, .5, 'Showcase'),
    start: P('Heat at the start', 0, 0, 19, 1, 'Showcase'),
    windup: P('Wind-up', .3, .1, .8, .05, 'Timing (s)'),
    travel: P('One fire flies in', .45, .2, 1, .05, 'Timing (s)'),
    hold: P('Show the result', 1.3, .3, 3, .1, 'Timing (s)'),
  },
  duration(p) {
    const f = frame(p.aim, { x: -.45, z: -.32 }), o = { x: 0, z: 0 };
    return plan(p, o, f, f.ground(o, -p.distance + .3, .12)).end;
  },
  phases(p) {
    const f = frame(p.aim, { x: -.45, z: -.32 }), o = { x: 0, z: 0 }, t = plan(p, o, f, f.ground(o, -p.distance + .3, .12));
    const out = [{ name: 'Rest', t: 0 }, { name: 'Wind-up', t: Lead }, { name: 'Pull', t: t.pull }];
    if (t.refusedAt >= 0) out.push({ name: 'Too hot', t: t.refusedAt });
    out.push({ name: 'Result', t: t.result });
    return out;
  },
  events(p) {
    const f = frame(p.aim, { x: -.45, z: -.32 }), o = { x: 0, z: 0 }, t = plan(p, o, f, f.ground(o, -p.distance + .3, .12));
    const out = [{ t: t.pull, type: 'sound', def: 'RimArt_FlameDevour' }];
    if (t.refusedAt >= 0) out.push({ t: t.refusedAt, type: 'sound', def: 'RimArt_FlameRefused' });
    return out;
  },

  draw(s, p, { origin: o, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const f = frame(p.aim, sun), { ground } = f;
    const caster = ground(o, -p.distance, 0);
    // The hand: at the hip at rest, forward and open during the cast, back at the result.
    const raise = smooth((s - Lead) / p.windup);
    const t = plan(p, o, f, ground(caster, .3, .12));
    if (s < 0 || s >= t.end) return;
    const closing = smooth((s - t.result) / Close);
    const snap = t.refusedAt >= 0 ? smooth((s - t.refusedAt) / .1) : 0;
    const open = raise * (1 - closing) * (1 - snap);
    const hand = ground(caster, lerp(.12, .30, raise * (1 - closing)), lerp(.18, .12, raise * (1 - closing)));

    // Heat now, and what is left burning.
    let heat = startHeat(p);
    for (let i = 0; i < t.eaten; i++) { const it = t.items[i]; if (s >= it.arrive) heat += it.value * clamp((s - it.arrive) / .15); }
    const over = heat >= OverheatAt ? 1 : 0;

    // Floor first: the radius ring while the pull runs, and every fire or scorch.
    const live = smooth((s - Lead) / .2) * (1 - smooth((s - t.result) / .3));
    circle(o, DevourRadius, .45 * live, Floor + .03, Ember);
    t.items.forEach((it, i) => {
      const taken = i < t.eaten && s >= it.depart, amount = taken ? 1 - clamp((s - it.depart) / .2) : 1;
      if (it.pawn) {
        if (p.actors) figure(it.c, Enemy, sun, strength);
        burningPawn(`devour pawn ${i}`, it.c, s, amount);
        if (taken && amount <= 0) {
          // Put out: smoke off the pawn for a second, and it stays standing.
          const age = s - it.depart - .2;
          for (let k = 0; k < 3; k++) {
            const u = clamp((age - k * .15) / 1);
            if (u <= 0 || u >= 1) continue;
            sprite({ x: it.c.x + (k - 1) * .1 + Math.sin(u * 6 + k) * .06, z: it.c.z + .3 + u * .8 * Lift }, .2 + u * .3, .18 + u * .25, Smoke.withAlpha(.45 * (1 - u)), puff, Y + .16);
          }
        }
      } else fireCell(`devour cell ${i}`, it.c, s, amount, i + 1);
    });
    t.outside.forEach((c, i) => fireCell(`devour out ${i}`, c, s, 1, 30 + i));

    if (p.showTally) tally('devour', caster, heat, p.aim);
    if (p.actors) figure(caster, Wearer, sun, strength);
    overheating('devour', caster, s, over * smooth((s - t.result) / .3),
      over && s >= t.result + BurnTick ? 1 : 0, over ? bump((s - t.result - BurnTick) / .3) : 0);

    const g = gauntlet('devour gauntlet', hand, p.aim, heat, s, sun, strength, { open });

    // The fires in flight, and the flash where each one goes in.
    for (let i = 0; i < t.eaten; i++) {
      const it = t.items[i], u = (s - it.depart) / p.travel;
      if (u < 0 || u > 1) continue;
      const from = { x: it.c.x, z: it.c.z + (it.pawn ? .45 : .3) * Lift };
      parcel(`devour parcel ${i}`, from, g.palm, easeOut2(u), s, i, it.pawn ? 1.4 : 1);
      const near = clamp((u - .7) / .3);
      if (near > 0) sprite(g.palm, .6 * near, .5 * near, Flame.withAlpha(.4 * near), glow, Y + .15);
    }
    for (let i = 0; i < t.eaten; i++) {
      const age = s - t.items[i].arrive;
      if (age >= 0 && age < .15) sprite(g.palm, .7, .6, Core.withAlpha(.9 * (1 - age / .15)), glow, Y + .152);
    }
    // While the pull runs the open palm breathes in: a faint glow that pulses.
    if (open > 0 && s >= t.pull && s < t.result) sprite(g.palm, .9, .75, Ember.withAlpha(.2 * open * (.6 + .4 * Math.sin(s * 14))), glow, Y + .149);
    if (t.refusedAt >= 0) refused(g.palm, s - t.refusedAt);
  },
};
function easeOut2(x) { x = clamp(x); return 1 - (1 - x) * (1 - x); }
