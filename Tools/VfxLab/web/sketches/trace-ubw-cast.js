// Unlimited Blade Works: cast (pocket) — Trace kit proposal, not the game. Nothing in Source/RimArt
// draws this yet. The home-map side of the pocket-map version of the kit's ultimate: the chant, the fire
// that runs out along the chant's lines, the white that takes everyone standing inside, the ring left
// burning while they are away, the return. The inside is "Unlimited Blade Works: world (pocket)", whose
// header has the whole mechanic. The first two UBW sketches (the world standing in place) stay as they
// were for reference.
//
// The rules this sketch shows (proposed 2026-09-24, none agreed; numbers are placeholders):
//   - taken: every standing pawn inside the radius, allies too (the ally and two raiders);
//   - not taken: a pawn outside (a raider who walks up later) and a downed pawn inside;
//   - the ring left burning at the radius has no heat and blocks nothing (the Type-Moon wiki: the fire
//     gives off no heat): the raider walks through it. Our addition: Fate/Zero shows nothing where a
//     Reality Marble stands; the ring shows the player where everyone will come back;
//   - on return everyone is back at the cell they were taken from: a raider downed inside comes back
//     downed, a raider killed inside comes back as a corpse dropped by the cast point.
//
// Order, with the default timings (released after verse 1):
//   0.00  chant: 10 cyan lines run out along the floor from the pawn to the radius (1.8 s a verse), a
//         ring at their front, square circuit traces light round the pawn's feet, a ring off the pawn as
//         each verse starts (anime 2015, Shirou's chant: lines along the stone, then square traces).
//   2.00  release: the pawn's arm goes up; the lines catch fire from the pawn outward and the fire runs
//         along them to the radius in 1 s, forking into two branches off each line; an ember line stays
//         under it (anime 2015, 28-29 s: the fire runs across the ground as branching lines).
//   3.00  the fronts reach the radius and the ring closes along it in 0.25 s.
//   3.25  white: a white disc over the radius; at full white (3.37) everyone standing inside is gone.
//   3.92  the white is gone: a low ring of flames burns on at the radius; the downed raider lies where he
//         was; the raider outside walks in through the ring.
//   6.92  the world ends: the ring flares, then the fire runs back in along the lines in 0.6 s.
//   7.77  white again; at full white (7.89) everyone is back: the caster, the ally, a raider downed where
//         he was taken, the other raider's corpse by the cast point.
//
// Drawing: floor lines, level rings and flames only, so there is no per-facing method. The flames are
// batched (lib/ubw-pocket.js flames: two draws for any number). The flame is a lab texture
// (lab/ubw-flame). Pawns are stand-ins ("Stand-ins").
import { Color } from '../js/engine.js';
import { P, Y, Floor, sprite, glow, rand } from './lib/six-paths-impact.js';
import { pawn, ringAt, line, EnemyColour, Ally } from './lib/goku.js';
import { smooth, clamp, Trace } from './lib/trace.js';
import { light, FireOuter, SceneNorth } from './lib/ubw.js';
import {
  Radius, VerseTime, Lines, circuitPath, chant, squareTraces, lengthOf, pointAt, between, flames, flick, ringFlames, whiteDisc,
} from './lib/ubw-pocket.js';

const Caster = new Color(.55, .3, .24), Dead = new Color(.42, .42, .42);
const RingClose = .25, FlashUp = .12, FlashHold = .1, FlashDown = .45, Flare = .25, After = 1.4, Walk = .9, FlameEvery = .22;
// The pawns, in cells from the caster at verse 1 (scaled with the radius at verses 2 and 3).
const Friend = { x: -2.2, z: -1.6 }, RaiderA = { x: 2.8, z: 1.2 }, RaiderB = { x: -1.5, z: 3.2 }, Downed = { x: 1.8, z: -2.6 };
const Corpse = { x: .8, z: -.9 };
// The raider outside: from here (cells past the radius, across) to here, walking in through the ring.
const Outside = { from: { out: 1.4, across: -1.2 }, to: { out: -2.4, across: .9 }, angle: -20 };
// The branches off each line: [share of the line where it forks, turn off the line (radians), length as a
// share of the radius].
const Branches = [[.38, .6, .3], [.64, .5, .24]];

function times(p) {
  const V = Math.round(p.verse), R = Radius[V - 1], open = V * VerseTime, lit = open + p.run, closed = lit + RingClose;
  const taken = closed + FlashUp, clear = taken + FlashHold + FlashDown, ends = clear + p.hold, inward = ends + Flare;
  const back = inward + p.back, home = back + FlashUp, end = home + FlashHold + FlashDown + After;
  return { V, R, open, lit, closed, taken, clear, ends, inward, back, home, end };
}
// The white of a flash that starts at `at`: up, held, gone.
function flashAlpha(s, at) {
  const u = s - at;
  if (u < 0) return 0;
  if (u < FlashUp) return smooth(u / FlashUp);
  if (u < FlashUp + FlashHold) return 1;
  return 1 - smooth((u - FlashUp - FlashHold) / FlashDown);
}
// Every line the fire can run along: the chant's 10 at the full radius, each with its branches.
const networks = new Map();
function network(c, R, count) {
  const id = `${c.x},${c.z},${R},${count}`;
  if (networks.has(id)) return networks.get(id);
  const list = Array.from({ length: Lines }, (_, i) => {
    const main = circuitPath(c, i, R), L = lengthOf(main), end = main[main.length - 1];
    const branches = Branches.slice(0, count).map(([at, turn, len], k) => {
      const fork = at * L, q = pointAt(main, fork), side = rand(i * 7 + k * 3 + 1) > .5 ? 1 : -1;
      const a = Math.atan2(q.dz, q.dx) + side * (turn + (rand(i * 11 + k) - .5) * .3), pts = [{ x: q.x, z: q.z }];
      for (let j = 1; j <= 6; j++) {
        const d = len * R * j / 6, wob = Math.sin(d * 1.7 + i + k) * .12;
        pts.push({ x: q.x + Math.cos(a + wob) * d, z: q.z + Math.sin(a + wob) * d });
      }
      return { fork, pts, L: lengthOf(pts) };
    });
    return { main, L, end, angle: Math.atan2(end.z - c.z, end.x - c.x), branches };
  });
  networks.set(id, list);
  return list;
}
// Flames every FlameEvery cells along pts from d0 to d1 into list, taller within half a cell of `front`.
function along(list, pts, d0, d1, front, s, h, seed) {
  for (let d = Math.ceil(d0 / FlameEvery) * FlameEvery, k = 0; d <= d1; d += FlameEvery, k++) {
    const q = pointAt(pts, d), near = Math.abs(d - front) < .5 ? 1.35 : 1;
    list.push({ x: q.x, z: q.z, w: .26, h: flick(seed + k, s, h) * near });
  }
}
// The glowing line the fire leaves on the ground.
function ember(key, pts, alpha) {
  if (pts.length < 2 || alpha <= 0) return;
  line(`${key} ember`, pts, .08, FireOuter.withAlpha(.55 * alpha), light, Floor + .006, 'none');
  line(`${key} ember glow`, pts, .32, FireOuter.withAlpha(.14 * alpha), light, Floor + .0055, 'none');
}

export default {
  kit: 'Trace', label: 'Unlimited Blade Works: cast (pocket) (sketch)',
  params: {
    verse: P('Released after verse', 1, 1, 3, 1, 'Cast'),
    actors: { label: 'Stand-ins', value: true, group: 'Cast' },
    run: P('Fire runs out along the lines', 1, .4, 2.5, .05, 'Timing (s)'),
    hold: P('World stands (20-30 s in game)', 3, 1, 8, .5, 'Timing (s)'),
    back: P('Fire runs back in', .6, .3, 1.5, .05, 'Timing (s)'),
    flame: P('Flame height (cells)', .7, .2, 1.5, .05, 'Look'),
    low: P('Ring while they are away (x flame)', .35, .1, 1, .05, 'Look'),
    branches: P('Branches off each line', 2, 0, 2, 1, 'Look'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p), verses = [];
    for (let k = 1; k <= t.V; k++) verses.push({ name: `Verse ${k}`, t: (k - 1) * VerseTime });
    return [...verses, { name: 'Release', t: t.open }, { name: 'Ring closes', t: t.lit }, { name: 'Taken', t: t.taken }, { name: 'Away', t: t.clear },
      { name: 'World ends', t: t.ends }, { name: 'Back', t: t.home }];
  },
  events(p) { const t = times(p); return [{ t: t.taken, type: 'shake', value: .04 }, { t: t.home, type: 'shake', value: .02 }]; },

  draw(s, p, { origin: cell, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const c = { x: cell.x, z: cell.z + SceneNorth }, R = t.R, k = R / 6, at = q => ({ x: c.x + q.x * k, z: c.z + q.z * k });
    const net = network(c, R, Math.round(p.branches));

    // The chant, fading as the fire takes its lines.
    const chantFade = s < t.open ? 1 : 1 - smooth((s - t.open) / (p.run * .8));
    if (s < t.taken) {
      chant(c, s, t.V, chantFade);
      squareTraces(c, s, s < t.open ? 1 : 1 - smooth((s - t.open) / .5));
      if (s < t.open) sprite({ x: c.x, z: c.z + .3 }, 1.6, 1.6, Trace.withAlpha(.22 + .1 * Math.sin(s * 9)), glow, Y + .005);
    }

    // The fire on the lines: out from the pawn after the release, back in from the ring when the world
    // ends. Hidden by the white each time, so drawn only until the white is full.
    const fire = [];
    if (s >= t.open && s < t.taken + FlashHold) {
      const u = clamp((s - t.open) / p.run), ease = 1 - (1 - u) ** 2;
      net.forEach((ln, i) => {
        const front = ln.L * ease;
        along(fire, ln.main, 0, front, front, s, p.flame, i * 100);
        ember(`ubwp cast ${i}`, between(ln.main, 0, front), 1);
        ln.branches.forEach((br, b) => {
          const d = clamp((front - br.fork) / br.L) * br.L;
          if (d <= 0) return;
          along(fire, br.pts, 0, d, d, s, p.flame * .8, i * 100 + 50 + b * 20);
          ember(`ubwp cast ${i} branch ${b}`, between(br.pts, 0, d), 1);
        });
      });
    }
    if (s >= t.inward && s < t.home + FlashHold) {
      const u = clamp((s - t.inward) / p.back), front = u * u;
      net.forEach((ln, i) => {
        const d0 = ln.L * (1 - front);
        along(fire, ln.main, d0, ln.L, d0, s, p.flame, i * 100);
        ember(`ubwp back ${i}`, between(ln.main, d0, ln.L), 1);
        ln.branches.forEach((br, b) => {
          if (d0 >= br.fork) return;
          along(fire, br.pts, 0, br.L, -1, s, p.flame * .8, i * 100 + 50 + b * 20);
          ember(`ubwp back ${i} branch ${b}`, br.pts, 1);
        });
      });
    }

    // The ring: it closes from the ends of the lines, burns low while everyone is away, flares at the end.
    if (s >= t.lit && s < t.home + FlashHold) {
      const spread = Math.PI / Lines * clamp((s - t.lit) / RingClose);
      const height = s < t.taken ? p.flame : s < t.ends ? p.flame * p.low : p.flame * (p.low + (1 - p.low) * smooth((s - t.ends) / Flare));
      const reached = a => net.some(ln => Math.abs(Math.atan2(Math.sin(a - ln.angle), Math.cos(a - ln.angle))) <= spread);
      ringFlames(fire, c, R, s, height, reached);
      ringAt(c, R, FireOuter.withAlpha(.35 * (spread * Lines / Math.PI)), Floor + .008, true, light);
    }
    flames('ubwp cast fire', fire, 1, Y + .02);

    // The two flashes.
    whiteDisc(c, R * 1.04, flashAlpha(s, t.closed), Y + .06);
    whiteDisc(c, R * 1.04, flashAlpha(s, t.back), Y + .06);

    if (!p.actors) return;
    const gone = s >= t.taken && s < t.home, home = s >= t.home;
    const raise = s >= t.open ? smooth((s - t.open) / .3) : 0;
    if (!gone) pawn(c, Caster, sun, strength, { hair: true, arms: raise > 0 && !home ? 1 : 0, raise });
    if (!gone) pawn(at(Friend), Ally, sun, strength);
    if (!gone) pawn(at(RaiderA), EnemyColour, sun, strength, { lie: home });
    if (!gone && !home) pawn(at(RaiderB), EnemyColour, sun, strength);
    if (home) pawn(at(Corpse), Dead, sun, strength, { lie: true });
    pawn(at(Downed), EnemyColour, sun, strength, { lie: true });
    // The raider outside walks in through the burning ring while everyone is away, and stays.
    const a = Outside.angle * Math.PI / 180, dir = { x: Math.cos(a), z: Math.sin(a) }, side = { x: -dir.z, z: dir.x };
    const spot = q => ({ x: c.x + dir.x * (R + q.out) + side.x * q.across, z: c.z + dir.z * (R + q.out) + side.z * q.across });
    const from = spot(Outside.from), to = spot(Outside.to), dist = Math.hypot(to.x - from.x, to.z - from.z);
    const walked = clamp((s - t.clear - .2) * Walk / dist);
    pawn({ x: from.x + (to.x - from.x) * walked, z: from.z + (to.z - from.z) * walked }, EnemyColour, sun, strength);
  },
};
