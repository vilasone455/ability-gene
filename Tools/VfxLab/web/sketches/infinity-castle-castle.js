// Infinity Castle: the castle — pocket-map proposal for the Infinity Castle kit, not the game.
// Ported to C# as pictures and a generated pocket map, with no rules (2026-09-24):
// Source/RimArt/InfinityCastle/ (CastleLayout.cs is this generator, exact for a seed and room count;
// CastleRoomGraphics.cs, CastleEffectGraphics.cs, InfinityCastleInside{Timing,Graphics}.cs), previewed
// from the RimArts debug window, Infinity Castle, "castle" and "castle (biwa room)"; the pocket map
// itself is "castle map: open" (Kit/). The stand-ins are not ported: untick "Stand-in pawns" to see
// what the C# draws.
//
// What it is (agreed in outline 2026-09-23; every number is a placeholder and will be an XML field).
// The carrier's Infinity Castle ability takes up to 8 hostiles (total body size 8) within 5.9 cells
// of a target cell into a new 100 x 100 pocket map, and the carrier with them (see Open). This
// sketch is that map while it is open:
//   - 30 to 45 rooms, 5 x 5 to 17 x 13 cells with their walls, hanging in a void nobody can walk.
//     Rooms grow off each other and keep 2 cells of void from every other room, so the castle
//     starts as a tree: every room reachable, one way in. A doorway sits where two rooms' walls
//     touch along 3 or more cells. Room kinds are looks only: tatami room, corridor, great hall,
//     stair hall. Walls and doors cannot be dug or broken.
//   - The biwa room (9 x 9) is at the west edge. The carrier lands on its dais and plays from
//     there: she cannot walk or attack, and the room cannot be shifted, sealed or crushed, so
//     enemies can reach her and down her. Each enemy lands in a different room, at least 3
//     doorways from the biwa room.
//   - Castle sight: no fog, the carrier sees every room. Inside, enemies go for anyone they can
//     reach (here: the carrier, room by room), else wait at a door.
//   - The castle lasts 60 s, or until Release or the carrier is downed. Then everyone goes back
//     to where they were taken (Open shows that side) and the map is removed. Cooldown 3 days.
//
// Order (times with the default sliders; the real castle holds 60 s, the sketch compresses it):
//   0.00  the empty castle: rooms, lantern light, rooms at other depths drifting below the void
//   0.30  a floor door opens on the dais and the carrier rises out, seated with the biwa
//   0.90  a floor door opens in each enemy's room (0.14 s apart) and the enemy rises out
//   ~1.9  enemies walk toward the carrier, doorway by doorway, at 3 cells a second
//   5.10  Release: the carrier strums, the room outlines flash, a floor door opens under every
//         enemy where it stands and it sinks; the carrier goes last
//   6.70  the castle fades out (the pocket map is removed)
//
// Drawing: rooms, walls, doorways and lanterns stand in for the pocket map's terrain and
// buildings; in game RimWorld draws those. The mod draws: the rooms at other depths (drawn under
// the void terrain, which would need a see-through texture), the floor doors, the strum and the
// flashes. The generator in lib/infinity-castle.js (generate) is the rule set for the C# one; a
// port using Rand.PushState(seed) gives different layouts from the same seed. Level shapes and
// stand-in pawns only, so no per-facing method. The sketch sets scene: false (no grass or trees).
import { P } from './lib/six-paths-impact.js';
import {
  generate, arrivalRooms, drawVoid, drawRooms, doorway, floorDoor, doorAt, doorEnd, figure, rising, sinking, nakime, strum, roomFlash,
  centreOf, cellCentre, seatOf, biwaOf, Enemy, VoidDeep, L, Rule, smooth, clamp, lerp, draw,
} from './lib/infinity-castle.js';
import { MeshPool } from '../js/engine.js';

// Decided values.
const CasterLands = .3, FirstEnemy = .9, EnemyGap = .14, Rise = .55, Sink = .4, Walk = 3;
const ReleaseDoors = .35, CasterLast = .8, FadeAfter = 1.6, Fade = .8;
const Door0 = .09;                     // a pawn starts through a floor door once the leaves are half open

function times(p) {
  const landed = FirstEnemy + (p.enemies - 1) * EnemyGap + Rise;
  const release = landed + p.hold, fade = release + FadeAfter;
  return { landed, release, fade, end: fade + Fade + .2 };
}

// The castle for a seed is built once and kept, so scrubbing does not regenerate it.
const cache = new Map();
function castleFor(seed, rooms) {
  const key = `${seed}:${rooms}`;
  if (!cache.has(key)) cache.set(key, generate(seed, rooms));
  return cache.get(key);
}

// The way from a room to the biwa room: the two wall cells of each doorway on the way, in order.
function wayHome(castle, room) {
  const pts = [], d = castle.dist;
  let r = room.id;
  while (d.get(r) > 0 && d.get(r) < Infinity) {
    const door = castle.doors.find(dr => (dr.a === r && d.get(dr.b) === d.get(r) - 1) || (dr.b === r && d.get(dr.a) === d.get(r) - 1));
    if (!door) break;
    const mine = door.a === r ? 0 : 1;
    pts.push(door.cells[mine], door.cells[1 - mine]);
    r = mine === 0 ? door.b : door.a;
  }
  return pts;
}
// Position along a polyline after walking `dist` cells.
function along(pts, dist) {
  for (let i = 1; i < pts.length; i++) {
    const a = pts[i - 1], b = pts[i], L = Math.hypot(b.x - a.x, b.z - a.z);
    if (dist <= L) return { x: lerp(a.x, b.x, dist / L), z: lerp(a.z, b.z, dist / L) };
    dist -= L;
  }
  return pts[pts.length - 1];
}

export default {
  kit: 'Infinity Castle', label: 'Castle (sketch)', scene: false,
  params: {
    seed: P('Castle seed', 1, 1, 60, 1, 'Castle'),
    rooms: P('Rooms', 38, 30, 45, 1, 'Castle'),
    depth: { label: 'Rooms at other depths', value: true, group: 'Castle' },
    view: { label: 'Centre the view on', value: 'the castle', options: ['the castle', 'the biwa room'], group: 'Showcase' },
    enemies: P('Enemies taken in', 6, 1, 8, 1, 'Showcase'),
    actors: { label: 'Stand-in pawns', value: true, group: 'Showcase' },
    hold: P('Time before Release', 3.2, 1, 10, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Empty castle', t: 0 }, { name: 'Carrier lands', t: CasterLands }, { name: 'Enemies land', t: FirstEnemy },
    { name: 'Hold', t: t.landed }, { name: 'Release', t: t.release }, { name: 'Castle removed', t: t.fade },
  ]; },
  events(p) { const t = times(p); return [
    { t: CasterLands, type: 'sound', def: 'RimArt_CastleDoor' }, { t: FirstEnemy, type: 'sound', def: 'RimArt_CastleDoor' },
    { t: t.release - .05, type: 'sound', def: 'RimArt_BiwaStrum' },
  ]; },

  draw(s, p, { origin, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = (scene?.sun?.strength ?? .32) * .6;
    const castle = castleFor(p.seed, p.rooms), biwa = castle.rooms[0];
    const cell0 = { x: origin.x - .5, z: origin.z - .5 };
    const base = p.view === 'the biwa room'
      ? { x: cell0.x - (biwa.x + biwa.w / 2 + 5), z: cell0.z - (biwa.z + biwa.h / 2) }
      : { x: cell0.x - Rule.size / 2, z: cell0.z - Rule.size / 2 };
    const mid = { x: base.x + Rule.size / 2, z: base.z + Rule.size / 2 };

    drawVoid(mid, s, { seed: p.seed, depth: p.depth });
    drawRooms(castle, base, s);
    castle.doors.forEach(d => doorway(`castle door ${d.key}`, d, base, 1));

    // Release: the strum, every room answering, then doors under everyone.
    const seat = seatOf(base, biwa);
    const relAge = s - t.release;
    if (relAge >= -.3) castle.rooms.forEach((r, i) => roomFlash(r, centreOf(base, r), relAge - .05 - castle.dist.get(r.id) * .03));

    // Enemies: land, walk home toward the carrier, sink at Release.
    const landing = arrivalRooms(castle, p.enemies);
    landing.forEach((r, i) => {
      const at = cellCentre(base, r.x + Math.floor(r.w / 2), r.z + Math.floor(r.h / 2));
      const land = FirstEnemy + i * EnemyGap, walked = Math.max(0, Math.min(s, t.release + ReleaseDoors - .15) - land - Rise - .1) * Walk;
      const path = [at, ...wayHome(castle, r).map(([x, z]) => cellCentre(base, x, z))];
      const pos = along(path, walked);
      const inAge = s - land, inDoor = doorAt(inAge, Rise * .75);
      if (s < t.release) floorDoor(`castle in ${i}`, at, inDoor.open, inDoor.alpha, { s });
      const outAt = t.release + ReleaseDoors + i * .05, outAge = s - outAt, outDoor = doorAt(outAge, Sink + .05);
      if (outAge >= -.2 && outAge < doorEnd(Sink + .05)) floorDoor(`castle out ${i}`, pos, outDoor.open, outDoor.alpha, { s });
      if (inAge < 0) return;
      if (inAge < Rise + Door0) rising(`castle rise ${i}`, at, Enemy, clamp((inAge - Door0) / Rise), sun, strength, { actors: p.actors });
      else if (outAge < Door0) { if (p.actors) figure(pos, Enemy, sun, strength); }
      else sinking(`castle sink ${i}`, pos, Enemy, clamp((outAge - Door0) / Sink), sun, strength, { actors: p.actors });
    });

    // The carrier: rises onto the dais, plays, strums Release, goes down last.
    const cIn = s - CasterLands, cOut = s - (t.release + CasterLast);
    const cInDoor = doorAt(cIn, Rise * .75), cOutDoor = doorAt(cOut, Sink + .05);
    if (cIn >= -.2 && cIn < doorEnd(Rise * .75)) floorDoor('castle carrier in', seat, cInDoor.open, cInDoor.alpha, { s });
    if (cOut >= -.2) floorDoor('castle carrier out', seat, cOutDoor.open, cOutDoor.alpha, { s });
    const strumAge = relAge;
    if (p.actors && cIn >= Door0) {
      if (cOut < Door0) {
        const up = clamp((cIn - Door0) / Rise);
        nakime('castle carrier', seat, sun, strength, { strum: strumAge > -.25 ? strumAge : null, dark: (1 - smooth(up)) * .85, scale: .5 + .5 * smooth(up), alpha: Math.min(1, up * 4) });
      } else {
        const u = clamp((cOut - Door0) / Sink);
        if (u < 1) nakime('castle carrier', { x: seat.x, z: seat.z - .18 * smooth(u) }, sun, strength, { dark: smooth(u) * .85, scale: 1 - .5 * smooth(u), alpha: 1 - smooth((u - .75) / .25) });
      }
    }
    strum('castle release', biwaOf(seat), relAge, { reach: 14, life: .9 });

    // The map closing: everything goes to black.
    const fade = clamp((s - t.fade) / Fade);
    if (fade > 0) draw(MeshPool.plane10, mid.x, L.fx + .2, mid.z, Rule.size * 3, Rule.size * 3, 0, VoidDeep.withAlpha(fade));
  },
};
