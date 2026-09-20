// Shadow imitation — Shadow plexus ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (the user's draft, numbers are placeholders, none of it balanced yet). Target one
// pawn, range up to 19.9 cells times the light level at the carrier's cell, 1 s cast, 20 s
// cooldown. For 15 s the target cannot act. Each step the carrier takes drags the target one cell
// in the same direction. The shadow line from carrier to target breaks when a human-sized pawn
// crosses it, when a cell under it goes dark (under 30 % light) or when smoke covers it.
//
// Order (times with the default sliders):
//   0.00  the carrier's shadow runs out over the ground as one black line with a pointed end
//   1.00  it reaches the target: a pool opens under the target, threads climb to the knees, the
//         target darkens and shudders. No camera shake; this holds, it does not hit
//   1.60  the carrier walks. The target lurches one cell the same way on every step, kicking dust,
//         and the line moves over with them
//   end   daylight scenario: the line runs back into the carrier (the sketch ends the hold early;
//         the rule's 15 s is not played). Cut scenario: a pawn walks over the line, it snaps at
//         that cell and both halves run back. Dark scenario: the target is dragged past the fire's
//         30 % ring, the line dies from that end and the target is free
//
// Drawing: everything is flat on the floor under the pawns except the knee threads, so there is
// no per-facing method. The range ring is the rule's real radius (19.9 x light level: 9.95 in
// daylight). Pawns, sandbags, fire, night and cast shadows are lab stand-ins (lib/shadow-plexus.js).
import { AltitudeLayer, Color, Mathf, Meshes } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P } from './lib/six-paths-impact.js';
import {
  lighting, nightOverlay, fire, pawn, path, shadowLine, brokenLine, pool, grip, shreds, scuff, rangeRing, walked,
  CarrierColour, EnemyColour, AllyColour, DarkBelow, FireRadius, SnapTime,
} from './lib/shadow-plexus.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01;
const bag = Meshes.disc(20, 'imitation sandbag');
const itemLayer = AltitudeLayer.Item.AltitudeFor();
// The rule's numbers and decided looks.
const FullRange = 19.9, Hold = .6, StepPause = .12, Retract = .5, Tail = .9, DragLag = .06, DragSnap = 2.4;
const PoolRadius = .42, KneeThreads = .35, WalkerSpeed = 3, DarkSteps = 6;
const Scenarios = ['daylight: drag out of cover', 'firelight: a pawn cuts the line', 'firelight: dragged into the dark'];

function plan(p, o) {
  const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a), half = p.distance / 2;
  const place = (along, across) => ({ x: o.x + along * ca - across * sa, z: o.z + along * sa + across * ca });
  const mode = Scenarios.indexOf(p.scenario), per = p.stepTime + StepPause, walkStart = p.cast + Hold;
  const fires = mode === 1 ? [{ ...place(0, -2.2), radius: FireRadius, on: 1 }] : mode === 2 ? [{ ...place(-half + 2, -1.5), radius: FireRadius, on: 1 }] : [];
  let steps = mode === 0 ? p.steps : mode === 2 ? DarkSteps : 0, breakAt = Infinity;
  if (mode === 1) breakAt = p.cast + 1;
  if (mode === 2) {
    const L = lighting(true, null, fires);
    for (let t = walkStart; t < walkStart + per * DarkSteps; t += .02)
      if (L.level(place(half, walked(t - walkStart - DragLag, p.stepTime, StepPause, DarkSteps, DragSnap))) < DarkBelow) { breakAt = t; break; }
    steps = Math.ceil((breakAt - walkStart) / per);                 // the carrier finishes the step it is on
  }
  const walkEnd = walkStart + steps * per, release = mode === 0 ? walkEnd + .8 : breakAt;
  return { place, half, mode, fires, steps, walkStart, walkEnd, breakAt, release, end: release + (mode === 0 ? Retract : SnapTime) + Tail };
}

export default {
  kit: 'Shadow plexus', label: 'Shadow imitation (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    aim: P('Target direction (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Target distance (cells)', 7, 3, 9.5, .5, 'Showcase'),
    steps: P('Steps the carrier takes (daylight scenario)', 3, 1, 5, 1, 'Showcase'),
    range: { label: 'Show the range ring', value: true, group: 'Showcase' },
    cast: P('Cast: the line runs out', 1, .4, 2, .05, 'Timing (s)'),
    stepTime: P('One step', .45, .2, 1, .05, 'Timing (s)'),
    width: P('Line width (cells)', .17, .06, .4, .01, 'Line'),
    sway: P('Line sway (cells)', .1, 0, .4, .01, 'Line'),
  },
  duration(p) { return plan(p, { x: 0, z: 0 }).end; },
  phases(p) {
    const t = plan(p, { x: 0, z: 0 });
    return [{ name: 'Line runs out', t: 0 }, { name: 'Held', t: p.cast }, ...(t.steps ? [{ name: 'Carrier walks', t: t.walkStart }] : []),
      { name: t.mode === 0 ? 'Released' : t.mode === 1 ? 'Line cut' : 'Line goes dark', t: t.release }];
  },
  events() { return []; },

  draw(s, p, { origin: o, scene }) {
    const t = plan(p, o), { place, half, mode } = t;
    if (s < 0 || s >= t.end) return;
    const L = lighting(mode !== 0, scene, t.fires);
    nightOverlay('imitation', o, L);
    t.fires.forEach(f => fire(f, s));

    // Both walk across the aim; the target follows a moment late and finishes each step early.
    const moved = walked(s - t.walkStart, p.stepTime, StepPause, t.steps);
    const pulled = walked(Math.min(s, t.breakAt) - t.walkStart - DragLag, p.stepTime, StepPause, t.steps, DragSnap);
    const carrier = place(-half, moved), held = s >= p.cast && s < t.release, since = s - t.release;
    const shudder = held ? .04 * Math.sin((s - p.cast) * 60) * Math.exp(-(s - p.cast) * 8) : 0;
    const target = place(half + shudder, pulled);

    if (mode === 0) for (let k = -1; k <= 1; k++) {                  // sandbags in front of the target
      const b = place(half - 1, k);
      draw(bag, b.x, itemLayer, b.z, .47, .27, -(p.aim + 90), new Color(.45, .39, .27));
      draw(bag, b.x, itemLayer + .001, b.z + .05, .42, .21, -(p.aim + 90), new Color(.64, .57, .41));
    }
    if (mode === 1) {                                                // the pawn that walks over the line
      const across = Math.min(3.2, Math.max(-1.6, (t.breakAt - s) * WalkerSpeed));
      pawn('imitation walker', place(1.2, across), AllyColour, L);
    }
    if (p.range) rangeRing(carrier, FullRange, L.level(carrier), 1 - smooth(since / .5));

    // The line.
    if (s < t.release) {
      const out = 1 - (1 - clamp(s / p.cast)) ** 2;
      shadowLine('imitation line', path(carrier, target, 0, out, s, p.sway), p.width, 1, s);
    } else if (mode === 0) shadowLine('imitation line', path(carrier, target, 0, 1 - smooth(since / Retract), s, p.sway), p.width, 1, s);
    else if (mode === 1) brokenLine('imitation line', carrier, target, (1.2 + half) / p.distance, since, p.width, s, p.sway);
    else shadowLine('imitation line', path(carrier, target, 0, 1 - smooth(since / SnapTime), s, p.sway), p.width, 1, s);

    // The hold on the target: pool, knee threads, dark tint. All of it lets go at the release.
    const grab = smooth((s - p.cast) / .25) * (1 - smooth(since / .25));
    pool('imitation pool', target, PoolRadius * grab, 1, s);
    pool('imitation root', carrier, .3 * (1 - smooth(since / (mode === 0 ? Retract : SnapTime))), 1, s);
    pawn('imitation carrier', carrier, CarrierColour, L);
    pawn('imitation target', target, EnemyColour, L, { tint: .55 * grab });
    grip('imitation grip', target, grab, s, 4, KneeThreads);
    shreds('imitation free', target, since, 7);
    for (let k = 0; k < t.steps; k++) {
      const from = t.walkStart + k * (p.stepTime + StepPause) + DragLag;
      if (from < t.breakAt) scuff(place(half, k + .5), s - from);
    }
  },
};
