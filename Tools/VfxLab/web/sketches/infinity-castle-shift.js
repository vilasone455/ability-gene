// Infinity Castle: Shift — castle command proposal for the Infinity Castle kit, not the game.
// Nothing in Source/RimArt draws this yet.
//
// What it is for (agreed in outline 2026-09-23; every number is a placeholder and will be an XML
// field). A command the carrier plays from the biwa room while the castle is open. Every command is
// one strum, and strums are 1.5 s apart. Shift: pick a room (not the biwa room) and one of four
// directions. The room slides straight that way until its wall touches another room, at most 20
// cells. Everything in it rides along: pawns, items, corpses. Its doorways close as the rooms part;
// where it now touches another room along 3 or more cells, a new doorway opens. A room pushed into
// open void has no doorways, so whoever is inside is cut off until another Shift. Void rule: a pawn
// standing in a doorway that breaks is left over the void, drops, and comes up in a random room
// through a floor door, unharmed.
//
// Order (times with the default sliders):
//   0.00  the castle at rest: the biwa room (west) with the carrier on the dais, a corridor, the
//         tatami room A with a raider in it, room N north of A, the hall E east of A across 6 cells
//         of void with a colonist in it, a corridor off E and room D beside that corridor
//   0.20  the bachi lifts; 0.40 strum: rings leave the biwa, and A's outline flashes
//   0.45  A's two doorways (to the corridor and to N) slide shut in 0.15 s
//   0.65  A slides east, speeding up, two afterimages of its outline trailing; the raider rides
//   1.50  A's east wall meets E's west wall: dust along the seam, a small shake, the raider
//         stumbles 0.25 cells; 0.15 s later the new doorway between A and E slides open
//   result: A is joined to E only; the corridor and N keep closed doors facing the void
// Scenarios: "nothing in the way" has no E, so A slides the full 20 cells, eases to a stop and has
// no doorway at all. "raider in the doorway" puts a second raider in A's west doorway: once A has
// moved off, it is over the void, drops, and rises through a floor door in D.
//
// Drawing: the sliding room is drawn with the same meshes as a room at rest, offset (in game the
// real room is hidden under the drawing while it moves and the cells are moved at the stop; riding
// pawns need MimicRender or a fade). Level shapes and stand-in pawns only, no per-facing method.
import { P } from './lib/six-paths-impact.js';
import {
  local, slideDistance, moved, doorChanges, drawVoid, drawRoom, doorway, floorDoor, doorAt, doorEnd, figure, rising, sinking,
  nakime, strum, roomFlash, outline, dustLine, centreOf, cellCentre, seatOf, biwaOf, Enemy, Ally, smooth, clamp, bump, easeOut,
} from './lib/infinity-castle.js';

// Decided values.
const Max = 20, Rise = .55, Door0 = .09;
const Layout = [
  ['biwa', -22, -5, 9, 9], ['corridor', -13, -2, 7, 5], ['tatami', -6, -5, 9, 9], ['tatami', -5, 4, 7, 7],
  ['hall', 9, -8, 11, 11], ['corridor', 12, 3, 5, 9], ['tatami', 5, 4, 7, 7],
];
const A = 2, D = 6, Dir = { x: 1, z: 0 };
const Rider = [-2, 1], Colonist = [13, -3], InDoorway = [-6, 0];

// The castle for a scenario, the slide, and the castle after it; built once each.
const cache = new Map();
function setup(scenario) {
  if (cache.has(scenario)) return cache.get(scenario);
  const specs = scenario === 'nothing in the way' ? Layout.slice(0, 4) : Layout;
  const before = local(specs);
  const { d, blocked } = slideDistance(before.rooms, A, Dir.x, Dir.z, Max);
  const after = moved(before, A, Dir.x * d, Dir.z * d);
  const out = { before, after, d, blocked, changes: doorChanges(before, after) };
  cache.set(scenario, out);
  return out;
}

function times(p) {
  const { d, blocked } = setup(p.scenario);
  const strumAt = p.lead, close0 = strumAt + .05, slide0 = strumAt + .25;
  const slideTime = .25 + d / p.speed, stop = slide0 + slideTime, open = stop + .15;
  // Void rule: the doorway raider drops once A's west wall has moved 0.9 cells off its cell.
  const u = blocked ? Math.pow(.9 / d, 1 / 1.5) : .3;
  const fall = slide0 + u * slideTime, land = fall + .75;
  const last = p.scenario === 'raider in the doorway' ? Math.max(open + .2, land + doorEnd(Rise * .75)) : open + .2;
  return { strumAt, close0, slide0, slideTime, stop, open, fall, land, end: last + p.hold };
}

export default {
  kit: 'Infinity Castle', label: 'Shift (sketch)', scene: false,
  params: {
    scenario: { label: 'Scenario', value: 'touches a room', options: ['touches a room', 'nothing in the way', 'raider in the doorway'], group: 'Showcase' },
    depth: { label: 'Rooms at other depths', value: true, group: 'Showcase' },
    lead: P('Before the strum', .4, .2, 1, .05, 'Timing (s)'),
    speed: P('Slide speed (cells a second)', 8, 4, 20, 1, 'Timing (s)'),
    hold: P('Show the result', 1.2, .3, 3, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'At rest', t: 0 }, { name: 'Strum', t: t.strumAt }, { name: 'Slide', t: t.slide0 }, { name: 'Stop', t: t.stop }, { name: 'Result', t: t.open + .2 },
  ]; },
  events(p) {
    const t = times(p), { blocked } = setup(p.scenario);
    const out = [{ t: t.strumAt, type: 'sound', def: 'RimArt_BiwaStrum' }, { t: t.slide0, type: 'sound', def: 'RimArt_CastleSlide' }];
    if (blocked) out.push({ t: t.stop, type: 'shake', value: .03 }, { t: t.stop, type: 'sound', def: 'RimArt_CastleThud' });
    return out;
  },

  draw(s, p, { origin, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = (scene?.sun?.strength ?? .32) * .6;
    const { before, after, d, blocked, changes } = setup(p.scenario);
    const base = { x: origin.x - .5 + 2, z: origin.z - .5 - 1 };
    drawVoid({ x: origin.x, z: origin.z }, s, { seed: 3, depth: p.depth, reach: 50 });

    // How far A has slid: speeds up into a wall it hits, eases to a stop in open void.
    const u = clamp((s - t.slide0) / t.slideTime);
    const slid = s < t.slide0 ? 0 : d * (blocked ? Math.pow(u, 1.5) : smooth(u));
    const offsets = new Map([[A, { x: Dir.x * slid, z: Dir.z * slid }]]);
    const room = before.rooms[A];
    before.rooms.forEach(r => drawRoom(r, centreOf(base, r, offsets.get(r.id)), { s }));

    // Afterimages trail the room while it moves fast.
    if (s > t.slide0 && s < t.stop + .15) for (const [lag, a] of [[.05, .22], [.1, .11]]) {
      const lu = clamp((s - lag - t.slide0) / t.slideTime), back = d * (blocked ? Math.pow(lu, 1.5) : smooth(lu));
      const fade = 1 - clamp((s - t.stop) / .15);
      outline(room, centreOf(base, room, { x: Dir.x * back, z: Dir.z * back }), a * fade * clamp(u * 4));
    }

    // Doorways: kept ones stay open, broken ones shut before the slide, new ones open after the stop.
    const shut = 1 - smooth((s - t.close0) / .15);
    changes.kept.forEach(dr => doorway(`shift kept ${dr.key}`, dr, base, 1, { offsets }));
    changes.broken.forEach(dr => doorway(`shift broken ${dr.key}`, dr, base, shut, { offsets }));
    if (s >= t.open) changes.made.forEach(dr => doorway(`shift made ${dr.key}`, dr, base, easeOut((s - t.open) / .2)));

    // The stop against E: dust along the seam where the walls met.
    if (blocked) {
      const seamX = base.x + room.x + room.w + d, z0 = base.z + Math.max(room.z, after.rooms[4].z), z1 = base.z + Math.min(room.z + room.h, after.rooms[4].z + after.rooms[4].h);
      dustLine('shift seam', { x: seamX, z: z0 + .5 }, { x: seamX, z: z1 - .5 }, s - t.stop, { count: 12, alpha: .55, spread: .6 });
    }

    // The raider in A rides along and stumbles at the stop; the colonist waits in E.
    const stumble = blocked ? .25 * bump(clamp((s - t.stop) / .3)) : 0;
    const rider = cellCentre(base, Rider[0], Rider[1]);
    figure({ x: rider.x + Dir.x * (slid + stumble), z: rider.z + Dir.z * (slid + stumble) }, Enemy, sun, strength);
    if (p.scenario !== 'nothing in the way') figure(cellCentre(base, Colonist[0], Colonist[1]), Ally, sun, strength);

    // Void rule: the raider in A's west doorway is left over the void, drops, comes up in D.
    if (p.scenario === 'raider in the doorway') {
      const at = cellCentre(base, InDoorway[0], InDoorway[1]);
      if (s < t.fall) figure(at, Enemy, sun, strength);
      else {
        const fu = clamp((s - t.fall) / .5);
        if (fu < 1) figure({ x: at.x, z: at.z - .3 * smooth(fu) }, Enemy, sun, strength * (1 - fu), { scale: 1 - .6 * smooth(fu), dark: smooth(fu) * .9, alpha: 1 - smooth((fu - .7) / .3) });
      }
      const dr = before.rooms[D], landAt = cellCentre(base, dr.x + Math.floor(dr.w / 2), dr.z + Math.floor(dr.h / 2));
      const age = s - t.land, st = doorAt(age, Rise * .75);
      if (age < doorEnd(Rise * .75)) floorDoor('shift void land', landAt, st.open, st.alpha, { s });
      if (age >= Door0) rising('shift void rise', landAt, Enemy, clamp((age - Door0) / Rise), sun, strength);
    }

    // The carrier plays; the room she moves flashes.
    const seat = seatOf(base, before.rooms[0]);
    nakime('shift carrier', seat, sun, strength, { strum: s - t.strumAt > -.25 ? s - t.strumAt : null });
    strum('shift strum', biwaOf(seat), s - t.strumAt, { reach: 5, life: .6 });
    roomFlash(room, centreOf(base, room, offsets.get(A)), s - t.strumAt - .05);
  },
};
