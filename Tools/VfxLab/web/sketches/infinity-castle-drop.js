// Infinity Castle: Drop and Summon — castle command proposals for the Infinity Castle kit, not the
// game. Nothing in Source/RimArt draws this yet.
//
// What they are for (agreed in outline 2026-09-23; every number is a placeholder and will be an XML
// field). Commands the carrier plays from the biwa room while the castle is open; each is one
// strum, strums are 1.5 s apart.
//   Drop: pick one pawn anywhere in the castle, then a room. A door opens in the floor under the
//   pawn and it drops; a door opens in the floor of the chosen room and it comes up there. No
//   damage. Any pawn, hostile or friendly; not the carrier.
//   Summon: pick a room, then a colonist on the home map from a list. A door opens in that room's
//   floor and the colonist comes up; it goes home with everyone when the castle ends.
//
// Order (times with the default sliders):
//   0.00  a raider walks down the corridor toward the biwa room; tatami room T lies across the
//         void, cut off by an earlier Shift (no doorways)
//   0.20  the bachi lifts; 0.40 strum: rings leave the biwa, the corridor's outline flashes
//   Drop:   0.50 a door snaps open under the raider, it sinks in 0.4 s, the door shuts;
//           1.10 T's outline flashes and a door opens in its floor; the raider rises out with a
//           small hop and is stuck in a room with no way out
//   Summon: 0.50 a door opens in the corridor 2.5 cells in front of the raider and a colonist rises
//           out between it and the carrier; the raider stops in front of it
//
// Drawing: floor doors, rings and flashes are the mod's; rooms stand in for the pocket map. Level
// shapes and stand-in pawns only, no per-facing method.
import { P } from './lib/six-paths-impact.js';
import {
  local, drawVoid, drawRooms, doorway, floorDoor, doorAt, doorEnd, figure, rising, sinking, carrierPlays, roomFlash,
  centreOf, cellCentre, Enemy, Ally, clamp,
} from './lib/infinity-castle.js';

// Decided values.
const Sink = .4, Rise = .55, Door0 = .09, Walk = 1.2;
const castle = local([
  ['biwa', -20, -4, 9, 9], ['corridor', -11, -1, 11, 5], ['stair', 0, -5, 9, 13], ['tatami', 12, 3, 7, 7], ['tatami', 12, -8, 7, 9],
]);
const Corridor = 1, T = 3, RaiderFrom = -2.5, RaiderZ = 1, GuardX = -6;

function times(p) {
  const strumAt = p.lead, doorUnder = strumAt + .1;
  const arrive = doorUnder + Door0 + Sink + .2;
  const last = p.scenario === 'drop a raider' ? arrive + doorEnd(Rise * .75) : doorUnder + doorEnd(Rise * .75);
  return { strumAt, doorUnder, arrive, end: last + p.hold };
}

export default {
  kit: 'Infinity Castle', label: 'Drop and Summon (sketch)', scene: false,
  params: {
    scenario: { label: 'Command', value: 'drop a raider', options: ['drop a raider', 'summon a colonist'], group: 'Showcase' },
    depth: { label: 'Rooms at other depths', value: true, group: 'Showcase' },
    lead: P('Before the strum', .4, .2, 1, .05, 'Timing (s)'),
    hold: P('Show the result', 1.4, .3, 3, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Raider walks in', t: 0 }, { name: 'Strum', t: t.strumAt }, { name: p.scenario === 'drop a raider' ? 'Drops' : 'Colonist comes up', t: t.doorUnder },
    ...(p.scenario === 'drop a raider' ? [{ name: 'Comes up in T', t: t.arrive }] : []), { name: 'Result', t: t.end - p.hold },
  ]; },
  events(p) { const t = times(p); return [{ t: t.strumAt, type: 'sound', def: 'RimArt_BiwaStrum' }, { t: t.doorUnder, type: 'sound', def: 'RimArt_CastleDoor' }]; },

  draw(s, p, { origin, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = (scene?.sun?.strength ?? .32) * .6;
    const base = { x: origin.x - .5 + 1, z: origin.z - .5 };
    drawVoid({ x: origin.x, z: origin.z }, s, { seed: 5, depth: p.depth, reach: 50 });
    drawRooms(castle, base, s);
    castle.doors.forEach(d => doorway(`drop door ${d.key}`, d, base, 1));
    carrierPlays('drop carrier', castle, base, s, [t.strumAt], sun, strength);
    const corridor = castle.rooms[Corridor], room = castle.rooms[T];

    const drop = p.scenario === 'drop a raider';
    // The raider walks west down the corridor; with a colonist in the way it stops 1 cell short.
    const start = cellCentre(base, RaiderFrom, RaiderZ);
    const stopX = drop ? -Infinity : base.x + GuardX + .5 + 1.1;
    const walkUntil = drop ? t.doorUnder : Infinity;
    const raider = { x: Math.max(stopX, start.x - Walk * Math.min(s, walkUntil)), z: start.z };

    if (drop) {
      roomFlash(corridor, centreOf(base, corridor), s - t.strumAt - .05);
      const age = s - t.doorUnder, d = doorAt(age, Sink + .05);
      if (age < doorEnd(Sink + .05)) floorDoor('drop under', raider, d.open, d.alpha, { s });
      if (age < Door0) figure(raider, Enemy, sun, strength);
      else sinking('drop sink', raider, Enemy, clamp((age - Door0) / Sink), sun, strength);
      // Out in T, cut off from everything.
      roomFlash(room, centreOf(base, room), s - t.arrive + .05);
      const landAt = cellCentre(base, room.x + 3, room.z + 3), aAge = s - t.arrive, a = doorAt(aAge, Rise * .75);
      if (aAge < doorEnd(Rise * .75)) floorDoor('drop land', landAt, a.open, a.alpha, { s });
      if (aAge >= Door0) rising('drop rise', landAt, Enemy, clamp((aAge - Door0) / Rise), sun, strength);
      return;
    }
    // Summon: the colonist comes up in the corridor between the raider and the carrier.
    roomFlash(corridor, centreOf(base, corridor), s - t.strumAt - .05);
    const guard = cellCentre(base, GuardX, RaiderZ), age = s - t.doorUnder, d = doorAt(age, Rise * .75);
    if (age < doorEnd(Rise * .75)) floorDoor('summon door', guard, d.open, d.alpha, { s });
    if (age >= Door0) rising('summon rise', guard, Ally, clamp((age - Door0) / Rise), sun, strength);
    figure(raider, Enemy, sun, strength);
  },
};
