// Bubble Pipe: Drifting Burst — weapon ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed 2026-09-22; the numbers are placeholders and will be XML fields). The
// pawn carries a bamboo bubble pipe (Utakata's weapon in Naruto) and a soap jar of 10 blows at the
// hip, refilled at a water cell like the water gun. Drifting Burst is the basic Soap Bubble
// Technique: aim a direction, 0.25 s to raise the pipe, then 6 bubbles are blown one after another
// and drift along the aim at 0.7 cells a second in a 50-degree fan for 8 s, wobbling and bobbing
// at about half a cell up. Any pawn touching a bubble pops it: 5 blunt to everything within 0.8
// cells and a 1 s stagger (the pawn stops, is shoved a little, then walks on slower). Bubbles
// nobody touches pop on their own at 8 s. Costs 2 blows, cooldown 10 s. With less than 2 blows
// the button is greyed "no soap". Held back for the Utakata hero kit: prison, rescue, riding,
// clones, acid. The pipe never lifts, carries or corrodes.
//
// Order (times with the default sliders):
//   0.00  pipe at rest, hanging at the side; the jar shows its soap
//   0.20  raise: the pipe comes up to the mouth along the aim
//   0.45  blow: a film bulges on the tip, rounds off and lets go; six times, one every 0.18 s;
//         the jar's level drops two blows
//   0.65+ drift: the cloud fans out along the aim, each bubble bobbing and wobbling
//   1.50  a pawn walks across the cloud's path (scenario "pawn walks in")
//   ~3.2  the first bubble it touches pops: the film tears and retracts, specks fly, a floor ring at
//         the blast radius, the pawn staggers; it may touch a second one
//   8.65  the bubbles still drifting pop on their own
//   9.00  lower: the pipe goes back to rest
//
// Drawing: pipe, jar, bubble, pop are lib/bubble-pipe.js (no water in the pop: a bubble holds
// almost nothing). Caster and pawn are stand-ins. Pops are replayed from the cast to the current
// time each frame (no stored state), so the timeline scrubs both ways.
import { Color, Mathf } from '../js/engine.js';
import { P, Y, Floor, circle, sprite, soft } from './lib/six-paths-impact.js';
import { drawPipe, bubble, pop, frame, figure, bump, WaterLit, Lead, Raise, Lower, Tail } from './lib/bubble-pipe.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const Cost = 2, Form = .2, Gap = .18, Blast = .8, Stagger = 1.0, Hover = .5, Bob = .12;
const WalkSpeed = 1.6, Slowed = .6;          // the pawn's speed in cells/s, and after the stagger
const Step = 1 / 30;                          // replay step for the pop search

function times(p) {
  const raise0 = Lead, blow = Lead + Raise, last = blow + (p.count - 1) * Gap, expire = blow + p.life;
  const lower0 = expire + .35;
  return { raise0, blow, last, expire, lower0, end: lower0 + Lower + Tail };
}
// Where bubble i is at time s (aim frame: along, across, height), before any pop. Bubbles
// leave the tip one after another and fan out by their index.
function bubbleAt(p, t, i, s, tip) {
  const age = s - (t.blow + i * Gap);
  if (age < 0) return null;
  const fan = (i / (p.count - 1) - .5) * p.spread * Mathf.Deg2Rad, seed = i * 1.7;
  const forming = age < Form, run = Math.max(0, age - Form);
  const dist = run * p.speed, along = tip.along + Math.cos(fan) * dist, across = tip.across + Math.sin(fan) * dist;
  const sway = forming ? 0 : .10 * Math.sin(run * 2.1 + seed) * clamp(run);
  const h = forming ? tip.h : lerp(tip.h, Hover + Bob * Math.sin(run * 1.7 + seed), smooth(run / 1.2));
  const r = forming ? p.radius * smooth(age / Form) : p.radius;
  return { along, across: across + sway * Math.cos(fan), h, r, forming, age, run };
}
// The pawn's place at time s, without staggers: walks across the path at `crossAlong`.
function pawnBase(p, t, s) {
  const t0 = t.blow + p.walkAt, moved = Math.max(0, s - t0);
  return { along: p.crossAlong, across: -2.6 + moved * WalkSpeed, moving: s >= t0 };
}
// Replay from the cast to s: which bubbles popped when, and the pawn's staggers. The pawn stops
// for Stagger seconds on each pop and is shoved 0.2 cells along the bubble's drift, then walks on
// at Slowed for the rest of the stagger.
function replay(p, t, s, tip, walkIn) {
  const pops = new Array(p.count).fill(null), staggers = [];
  if (!walkIn) return { pops, staggers };
  let along = p.crossAlong, across = -2.6, time = t.blow + p.walkAt, haltUntil = -1, slowUntil = -1;
  for (; time <= s; time += Step) {
    const speed = time < haltUntil ? 0 : time < slowUntil ? WalkSpeed * Slowed : WalkSpeed;
    across += speed * Step;
    for (let i = 0; i < p.count; i++) {
      if (pops[i]) continue;
      const b = bubbleAt(p, t, i, time, tip);
      if (!b || b.forming) continue;
      if (Math.hypot(b.along - along, b.across - across) < b.r + .28) {
        pops[i] = { at: time, along: b.along, across: b.across, h: b.h };
        staggers.push({ at: time, along, across, dir: Math.atan2(b.across - tip.across, b.along - tip.along) });
        haltUntil = time + Stagger * .4; slowUntil = time + Stagger;
      }
    }
  }
  return { pops, staggers, pawn: { along, across, moving: s >= t.blow + p.walkAt } };
}

export default {
  kit: 'Bubble Pipe', label: 'Drifting Burst (sketch)',
  params: {
    actors: { label: 'Show caster and pawn', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    scenario: { label: 'Scenario', value: 'pawn walks in', options: ['pawn walks in', 'empty'], group: 'Showcase' },
    blows: P('Soap in the jar (blows)', 10, 2, 10, 1, 'Showcase'),
    walkAt: P('Pawn starts walking after the blow', 1.5, 0, 6, .1, 'Timing (s)'),
    crossAlong: P('Pawn crosses at (cells from the target cell)', .4, -1.5, 5, .1, 'Showcase'),
    count: P('Bubbles', 6, 3, 8, 1, 'Shape'),
    spread: P('Fan (degrees)', 50, 10, 90, 5, 'Shape'),
    speed: P('Drift speed (cells/s)', .7, .3, 1.5, .05, 'Shape'),
    radius: P('Bubble radius (cells)', .28, .15, .45, .01, 'Shape'),
    life: P('Bubble life', 8, 3, 12, .5, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Rest', t: 0 }, { name: 'Raise', t: t.raise0 }, { name: 'Blow', t: t.blow }, { name: 'Drift', t: t.last + Form }, { name: 'Expire', t: t.expire }, { name: 'Lower', t: t.lower0 },
  ]; },
  events(p) { const t = times(p); return [{ t: t.blow, type: 'sound', def: 'RimArt_BubbleBlow' }]; },

  draw(s, p, { origin: centre, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const f = frame(p.aim, sun), { place } = f;
    const caster = place(centre, -2.5, 0);
    const walkIn = p.scenario === 'pawn walks in';

    const raise = s < t.raise0 ? 0 : s < t.lower0 ? smooth((s - t.raise0) / Raise) : 1 - smooth((s - t.lower0) / Lower);
    const blows = p.blows - Cost * clamp((s - t.blow) / (p.count * Gap));
    // The bubble forming on the tip right now, if any.
    let forming = 0;
    for (let i = 0; i < p.count; i++) {
      const age = s - (t.blow + i * Gap);
      if (age >= 0 && age < Form) forming = Math.max(forming, p.radius * smooth(age / Form));
    }
    if (p.actors) figure(caster, new Color(.93, .50, .13), sun, strength);
    const pipe = drawPipe(f, caster, s, { raise, blows, forming, actors: p.actors, sun, strength });
    const tip = { along: pipe.tipAlong - 2.5, across: pipe.tipAcross, h: pipe.tipH };   // in centre's frame
    const R = replay(p, t, s, tip, walkIn && p.actors);

    // The pawn: walks across the path, staggers on each pop.
    if (walkIn && p.actors && R.pawn) {
      const pos = place(centre, R.pawn.along, R.pawn.across);
      const last = R.staggers[R.staggers.length - 1], shove = last ? .2 * smooth((s - last.at) / .25) : 0;
      const lean = last ? bump((s - last.at) / Stagger) : 0;
      const q = last ? place(centre, R.pawn.along + Math.cos(last.dir) * shove, R.pawn.across + Math.sin(last.dir) * shove) : pos;
      figure(q, new Color(.55, .38, .27), sun, strength);
      if (lean > 0) for (let k = 0; k < 3; k++) {   // dazed stars over the head
        const turn = s * 5 + k * 2.094;
        sprite({ x: q.x + Math.cos(turn) * .2, z: q.z + .86 + Math.sin(turn) * .06 }, .08, .08, WaterLit.withAlpha(.9 * lean), soft, Y + .06);
      }
    }

    // The bubbles: drifting, popped by the pawn, or expired.
    for (let i = 0; i < p.count; i++) {
      const b = bubbleAt(p, t, i, s, tip);
      if (!b || b.forming) continue;
      const popped = R.pops[i], expired = s >= t.expire;
      if (popped) {
        const g = place(centre, popped.along, popped.across);
        pop(`bubble pipe pop ${i}`, g, popped.h, p.radius, s - popped.at, { sun, strength, seed: i });
        const age = s - popped.at;
        if (age < .6) circle(g, Blast * smooth(age / .15), (1 - age / .6) * .7, Floor + .05, WaterLit);
        continue;
      }
      if (expired) {
        const g = place(centre, b.along, b.across);
        pop(`bubble pipe expire ${i}`, g, b.h, p.radius, s - t.expire, { sun, strength, seed: i + 20 });
        continue;
      }
      bubble(place(centre, b.along, b.across), b.h, b.r, s, { id: i, wobble: .07, phase: i * 1.3, sun, strength });
    }
  },
};
