// Shadow seam — Shadow plexus ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (the user's draft, numbers are placeholders, none of it balanced yet). Pick any
// two targets (pawns or loose items) within 15.9 cells times the light level; both must stand in
// 30 % light or more. 0.8 s cast, 30 s cooldown. For 20 s the two cannot be more than 4 cells
// apart. Whichever moves faster or is thrown drags the other; when they pull against each other
// the larger body wins. The shadow line runs between the two targets, not to the carrier, and
// breaks like every shadow line (a pawn crossing it, a dark cell, smoke).
//
// Order (times with the default sliders):
//   0.00  one line leaves the carrier toward the point between the two targets
//   0.45  it forks, one branch to each target
//   0.80  sewn: a pool opens under each, three stitches loop over each one's feet
//   0.95  the fork lets go of the carrier's line and straightens into the seam between the two;
//         the carrier's line runs back. Slanted stitch marks appear along the seam from both ends
//   taut  at 4 cells the seam goes straight and thin and twangs. Rusher scenario: the rusher is
//         yanked back and from then on moves at the centipede's speed (small camera shake).
//         Dragged ally scenario: the downed ally slides after the runner, 4 cells behind, with dust
//   end   the stitches pop from both ends and the seam fades (the rule's 20 s is not played)
//
// Drawing: flat on the floor except the stitches over the feet, which are level loops 0.3 cells
// up and look the same for every facing. A slack seam waves, a taut one is straight; the amount is
// 1 - distance / 4. Pawns, centipede and cast shadows are lab stand-ins (lib/shadow-plexus.js).
import { Mathf, MeshPool } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Lift, trail } from './lib/six-paths-impact.js';
import {
  lighting, pawn, downedPawn, centipede, path, pointOn, shadowLine, pool, shreds, scuff, rangeRing,
  CarrierColour, EnemyColour, AllyColour, Shade, LineLayer,
} from './lib/shadow-plexus.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
// The rule's numbers and decided looks.
const FullRange = 15.9, MaxApart = 4, RushSpeed = 4, CrawlSpeed = .5, RunSpeed = 4, RunFrom = .25, RunTo = -8;
const Fork = .55, Straighten = .4, FeederBack = .45, StitchPitch = .45, StitchLength = .34, StitchSlant = 25, Undo = .5, Tail = .7;
const Twang = .18, Recoil = .6, PoolRadius = .36;
const Scenarios = ['rusher sewn to a centipede', 'downed ally dragged to safety'];

function plan(p, o) {
  const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a), mode = Scenarios.indexOf(p.scenario), sewn = p.cast;
  const place = (q) => ({ x: o.x + q.along * ca - q.across * sa, z: o.z + q.along * sa + q.across * ca });
  const freeA = (t) => mode === 0 ? { along: 3.4 - RushSpeed * t, across: 1.2 } : { along: Math.max(RunTo, -RunSpeed * Math.max(0, t - sewn - RunFrom)), across: 1.5 };
  const freeB = (t) => mode === 0 ? { along: 2.5 - CrawlSpeed * t, across: -1 } : { along: 1.5, across: -.6 };
  const apart = (t) => { const A = freeA(t), B = freeB(t); return Math.hypot(A.along - B.along, A.across - B.across); };
  let taut = sewn; while (taut < sewn + 8 && apart(taut) < MaxApart) taut += .01;
  const a0 = freeA(taut), b0 = freeB(taut);
  // Where the two are at time t, in along/across of the aim.
  const both = (t) => {
    if (t < taut) return { A: freeA(t), B: freeB(t) };
    const age = t - taut;
    if (mode === 0) {                                                // the centipede wins: the rusher is held at 4 cells
      const B = freeB(t), back = Recoil * Math.sin(age * 9) * Math.exp(-age * 5);
      B.along -= .08 * Math.exp(-age * 6);
      return { A: { along: B.along + (a0.along - b0.along) + Math.max(0, back), across: B.across + (a0.across - b0.across) }, B };
    }
    const A = freeA(t), side = (b0.across - a0.across) * Math.exp(-(a0.along - A.along) / MaxApart);   // the body swings in behind the runner
    return { A, B: { along: A.along + Math.sqrt(MaxApart * MaxApart - side * side), across: A.across + side } };
  };
  const undo = mode === 0 ? taut + 2.2 : sewn + RunFrom + -RunTo / RunSpeed + .5;
  return { place, mode, sewn, taut, both, undo, end: undo + Undo + Tail, carrier: place({ along: -p.distance, across: 0 }) };
}

// Three level loops over the feet, one after another: the needle going over and under.
function stitchOver(key, pos, amount, big) {
  for (let i = 0; i < 3; i++) {
    const on = clamp(amount * 3 - i); if (on <= 0) continue;
    const z = pos.z + (i - 1) * .11, pts = [];
    for (let j = 0; j <= 8; j++) { const u = j / 8 * on; pts.push({ x: pos.x + lerp(-.3, .3, u) * big, z: z + Math.sin(u * Math.PI) * .3 * Lift * big }); }
    trail(`${key} loop ${i}`, pts, .07, Shade.withAlpha(.95), Y + .012);
  }
}

export default {
  kit: 'Shadow plexus', label: 'Shadow seam (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    aim: P('Direction to the targets (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Carrier distance (cells)', 5.5, 3, 7.5, .5, 'Showcase'),
    range: { label: 'Show the range ring', value: true, group: 'Showcase' },
    cast: P('Cast: the line runs out and forks', .8, .4, 2, .05, 'Timing (s)'),
    width: P('Line width (cells)', .15, .06, .4, .01, 'Line'),
    sway: P('Slack seam sway (cells)', .35, 0, .8, .01, 'Line'),
  },
  duration(p) { return plan(p, { x: 0, z: 0 }).end; },
  phases(p) {
    const t = plan(p, { x: 0, z: 0 });
    return [{ name: 'Line runs out', t: 0 }, { name: 'Forks', t: p.cast * Fork }, { name: 'Sewn', t: t.sewn }, { name: 'Taut at 4 cells', t: t.taut }, { name: 'Seam undone', t: t.undo }];
  },
  events(p) { const t = plan(p, { x: 0, z: 0 }); return t.mode === 0 ? [{ t: t.taut, type: 'shake', value: .04 }] : []; },

  draw(s, p, { origin: o, scene }) {
    const t = plan(p, o), { place, mode } = t;
    if (s < 0 || s >= t.end) return;
    const L = lighting(false, scene), now = t.both(s), A = place(now.A), B = place(now.B), carrier = t.carrier;
    const mid = pointOn(A, B, .5), forkAt = pointOn(mid, carrier, .8 / (Math.hypot(mid.x - carrier.x, mid.z - carrier.z) || 1));
    if (p.range) rangeRing(carrier, FullRange, L.level(carrier), 1 - smooth((s - t.sewn - FeederBack) / .4));

    // The carrier's line and the fork. After the sewing the fork point moves onto the straight
    // line between the two, and the carrier's line runs back.
    const gone = smooth((s - t.undo) / Undo), apart = Math.hypot(A.x - B.x, A.z - B.z), slack = clamp(1 - apart / MaxApart);
    const joint = pointOn(forkAt, mid, smooth((s - t.sewn - .15) / Straighten));
    const feeder = s < t.sewn + .15 ? 1 - (1 - clamp(s / (p.cast * Fork))) ** 2 : 1 - smooth((s - t.sewn - .15) / FeederBack);
    shadowLine('seam feeder', path(carrier, joint, 0, feeder, s, .08), p.width, 1, s);
    pool('seam root', carrier, .28 * clamp(feeder * 4), 1, s);
    const branch = clamp((s - p.cast * Fork) / (p.cast * (1 - Fork))), twang = 1 + .7 * Math.max(0, 1 - Math.abs(s - t.taut) / Twang);
    const sway = p.sway * slack * (s > t.sewn ? 1 : .3), width = p.width * lerp(.75, 1, slack) * twang;
    if (branch > 0 && gone < 1) {
      const halfA = path(A, joint, 1 - branch, 1, s, sway, 14), halfB = path(B, joint, 1 - branch, 1, s, sway, 14);
      shadowLine('seam half a', halfA, width, 1 - gone, s, { flare: false, point: false });
      shadowLine('seam half b', halfB, width, 1 - gone, s, { flare: false, point: false });
      // Stitch marks: from both ends toward the middle; they pop in the same order at the end.
      const count = Math.max(2, Math.round(apart / 2 / StitchPitch)), deg = Math.atan2(B.z - A.z, B.x - A.x) * 180 / Math.PI;
      for (const [k, from] of [[0, A], [1, B]]) for (let i = 0; i < count; i++) {
        const f = i / count, shown = clamp((s - t.sewn - .15 - Straighten * .6 - f * .35) / .08) * (1 - clamp((s - t.undo - f * Undo * .7) / .08));
        const c = path(from, joint, 0, 1, s, sway, count * 2)[i * 2 + 1];
        draw(MeshPool.plane10, c.x, LineLayer + .004, c.z, .05, StitchLength * shown * lerp(1.25, 1, slack), -(deg + (k ? -StitchSlant : StitchSlant)), Shade.withAlpha(.95));
      }
    }

    // The two targets, their pools and the loops over their feet.
    const held = smooth((s - t.sewn) / .3) * (1 - gone);
    pool('seam pool a', A, PoolRadius * held, 1, s);
    pool('seam pool b', B, PoolRadius * (mode === 0 ? 1.9 : 1.2) * held, 1, s);
    pawn('seam carrier', carrier, CarrierColour, L);
    if (mode === 0) { centipede('seam b', B, p.aim, L); pawn('seam a', A, EnemyColour, L, { tint: .3 * held }); }
    else { downedPawn('seam b', B, AllyColour, L, p.aim); pawn('seam a', A, AllyColour, L); }
    stitchOver('seam a', A, held, 1); stitchOver('seam b', B, held, mode === 0 ? 1.8 : 1.2);
    shreds('seam undone a', A, s - t.undo - Undo * .7, 6); shreds('seam undone b', B, s - t.undo - Undo * .7, 6);

    // Dust: the rusher's feet at the yank, or the dragged body every 0.18 s.
    if (mode === 0) scuff(A, s - t.taut, 1.4);
    else for (let k = 0; t.taut + k * .18 < Math.min(s, t.undo - .5); k++) scuff(place(t.both(t.taut + k * .18).B), s - (t.taut + k * .18));
  },
};
