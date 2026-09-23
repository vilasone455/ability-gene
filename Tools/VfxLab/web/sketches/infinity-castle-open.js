// Infinity Castle: Open — ability proposal for the Infinity Castle kit, not the game. Nothing in
// Source/RimArt draws this yet.
//
// What it is for (agreed in outline 2026-09-23; every number is a placeholder and will be an XML
// field). The castle gene's carrier targets a cell within 35 cells; no line of sight needed. Warm-up
// 1 s: a ring shows the 5.9-cell radius around the cell and a pale outline marks the floor under
// each pawn that will be taken. Then one strum of the biwa: a door opens in the floor under every
// hostile pawn within 5.9 cells, nearest first, up to 8 pawns and a total body size of 8, and they
// drop into the castle (see Castle). Then a door opens under the carrier and she follows. Downed
// hostiles, allies, and hostiles past the cap or outside the radius stay. A taken pawn drops
// whatever it carries where it stood (a kidnapped colonist stays behind and can be rescued); its
// own gear goes with it. Its job ends and never resumes. Cooldown 3 days.
// Return, when the castle ends (60 s, Release, or the carrier downed): a door opens at the cell
// each pawn was taken from and it comes back up there; the carrier comes up where she stood.
// Pawns killed inside, and items, come up around the target cell. Returning raiders rejoin their
// raid if it still exists, else they get a new lord that leaves the map.
//
// Order, part "take" (times with the default sliders):
//   0.00  up to 10 raiders stand within 5.9 cells of the target cell; one carries a downed
//         colonist; a downed raider and a colonist are inside the radius, one raider walks in from
//         outside it. The carrier stands 9 cells away with the biwa.
//   0.00  warm-up 1 s: the radius ring and the door outlines fade in under the pawns to be taken
//   1.00  strum: the bachi sweeps the strings, rings leave the biwa; a ring runs out from the target
//         cell to the radius in 0.3 s and each door snaps open as it passes (0.18 s), the pawn sinks
//         into the shaft in 0.4 s, the leaves close, the frame fades. The kidnapper's colonist drops
//         beside its door and stays downed.
//   ~2.0  a door opens under the carrier and she sinks with the biwa
//   ~2.8  result: the downed raider, the colonist, the dropped colonist, the raiders past the cap
//         and the one that walked in are still there
// Part "return":
//   0.30  a door opens at each taken raider's cell, 0.07 s apart, and it rises out with a small hop;
//         the carrier rises where she stood; one raider killed inside comes up as a corpse beside
//         the target cell with a rifle; the raiders walk on toward the colony
//
// Drawing: floor doors and rings are level shapes; the pawns and the carrier are stand-ins; no
// per-facing method. The floor door (lib/infinity-castle.js floorDoor) is the same in the castle.
import { Color } from '../js/engine.js';
import { P } from './lib/six-paths-impact.js';
import {
  floorDoor, doorMark, doorAt, doorEnd, figure, rising, sinking, nakime, strum, thinRing, rifle,
  Enemy, Ally, Strum, Blood, sprite, soft, Floor, L, smooth, clamp, lerp, easeOut, bump,
} from './lib/infinity-castle.js';

// Decided values and the rule's numbers.
const Radius = 5.9, Cap = 8, RingTime = .3, Sink = .4, Rise = .55, Door0 = .09;
const Caster = { x: -9, z: -2.5 };
// Hostiles in range, nearest first (cells from the target cell). The first 8 are taken.
const Hostiles = [[1.1, .5], [-1.4, 1.5], [1.9, -1.6], [.2, -2.9], [-3.2, -.9], [3.3, 2.1], [-1.6, 4.0], [4.4, -1.9], [-4.3, 2.9], [3.0, 4.6]];
const Kidnapper = 2, KilledInside = 4;
const Downed = { x: -2.6, z: -3.3 }, Colonist = { x: -.5, z: 2.4 }, Walker = { x: 7.6, z: -.6 }, WalkSpeed = .8;
const Corpse = new Color(.38, .27, .21);
const dist = ([x, z]) => Math.hypot(x, z);

function times(p) {
  if (p.part === 'take') {
    const strumAt = p.warmup, taken = Math.min(p.count, Cap);
    const doorOf = i => strumAt + .05 + dist(Hostiles[i]) / Radius * RingTime;
    const casterDoor = doorOf(taken - 1) + Door0 + Sink + .25;
    const gone = casterDoor + doorEnd(Sink + .05);
    return { strumAt, taken, doorOf, casterDoor, end: gone + p.hold };
  }
  const first = .3, taken = Math.min(p.count, Cap), doorOf = i => first + i * .07;
  const casterDoor = first + .12, corpseDoor = first + .35;
  const landed = doorOf(taken - 1) + doorEnd(Rise * .75);
  return { first, taken, doorOf, casterDoor, corpseDoor, end: landed + p.hold };
}

export default {
  kit: 'Infinity Castle', label: 'Open (sketch)',
  params: {
    part: { label: 'Part', value: 'take', options: ['take', 'return'], group: 'Showcase' },
    count: P('Hostiles in range (8 taken)', 10, 1, 10, 1, 'Showcase'),
    extras: { label: 'Show the rule stand-ins', value: true, group: 'Showcase' },
    warmup: P('Warm-up', 1, .3, 2, .05, 'Timing (s)'),
    hold: P('Show the result', 1.2, .3, 3, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    if (p.part === 'take') return [
      { name: 'Warm-up', t: 0 }, { name: 'Strum', t: t.strumAt }, { name: 'Carrier follows', t: t.casterDoor }, { name: 'Result', t: t.end - p.hold },
    ];
    return [{ name: 'Castle ends', t: 0 }, { name: 'Doors open', t: t.first }, { name: 'Back', t: t.end - p.hold }];
  },
  events(p) {
    const t = times(p);
    if (p.part === 'take') return [
      { t: t.strumAt, type: 'sound', def: 'RimArt_BiwaStrum' }, { t: t.strumAt + .05, type: 'shake', value: .02 },
      { t: t.doorOf(0), type: 'sound', def: 'RimArt_CastleDoor' }, { t: t.casterDoor, type: 'sound', def: 'RimArt_CastleDoor' },
    ];
    return [{ t: t.first, type: 'sound', def: 'RimArt_CastleDoor' }];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const at = ([x, z]) => ({ x: o.x + x, z: o.z + z });
    const caster = at([Caster.x, Caster.z]), biwaAt = { x: caster.x + .1, z: caster.z + .35 };
    const take = p.part === 'take';

    // Pawns that are never taken: the downed raider, the colonist, the raiders past the cap,
    // and the raider walking in from outside the radius.
    if (p.extras) {
      figure(at([Downed.x, Downed.z]), Enemy, sun, strength, { downed: 1 });
      figure(at([Colonist.x, Colonist.z]), Ally, sun, strength);
      const walked = Math.min(s * WalkSpeed, 3.2);
      figure(at([Walker.x - walked, Walker.z]), Enemy, sun, strength);
    }
    for (let i = Cap; i < p.count; i++) figure(at(Hostiles[i]), Enemy, sun, strength);

    if (take) {
      // Warm-up: the radius and the doors to come.
      const warm = smooth(s / Math.max(.1, t.strumAt)) * (1 - smooth((s - t.strumAt - .3) / .5));
      thinRing('open radius', o, Radius, .07, Strum.withAlpha(.45 * warm), Floor + .01);
      for (let i = 0; i < t.taken; i++) if (s < t.doorOf(i)) doorMark(at(Hostiles[i]), .55 * smooth(s / Math.max(.1, t.strumAt)) * (.8 + .2 * Math.sin(s * 9 + i)));
      if (s < t.casterDoor) doorMark(caster, .3 * warm);
      // The answer: a ring runs from the target cell to the radius as the doors open.
      const ra = s - t.strumAt - .05;
      if (ra >= 0 && ra < RingTime + .4) {
        const u = clamp(ra / RingTime), fade = 1 - clamp((ra - RingTime) / .4);
        thinRing('open answer', o, Radius * easeOut(u), .12, Strum.withAlpha(.7 * fade), L.fx);
        sprite(o, Radius * 2 * easeOut(u), Radius * 2 * easeOut(u), Strum.withAlpha(.06 * fade), soft, Floor + .012);
      }
      // Each taken pawn: the door snaps open, it sinks, the door shuts and fades.
      for (let i = 0; i < t.taken; i++) {
        const pos = at(Hostiles[i]), age = s - t.doorOf(i), d = doorAt(age, Sink + .05);
        if (age < doorEnd(Sink + .05)) floorDoor(`open take ${i}`, pos, d.open, d.alpha, { s });
        if (age < Door0) {
          figure(pos, Enemy, sun, strength);
          if (i === Kidnapper) figure({ x: pos.x + .05, z: pos.z + .1 }, Ally, sun, 0, { downed: 1, h: .55 });
        } else sinking(`open sink ${i}`, pos, Enemy, clamp((age - Door0) / Sink), sun, strength);
        // The carried colonist drops beside the door and stays.
        if (i === Kidnapper && age >= Door0) {
          const u = clamp((age - Door0) / .28), rest = { x: pos.x + .95, z: pos.z - .25 };
          const q = { x: lerp(pos.x + .05, rest.x, easeOut(u)), z: lerp(pos.z + .1, rest.z, easeOut(u)) };
          figure(q, Ally, sun, strength, { downed: 1, h: .55 * (1 - u) * (1 - u) + .12 * bump(clamp((u - .7) / .3)) });
        }
      }
      // The carrier follows through her own door.
      const cAge = s - t.casterDoor, cd = doorAt(cAge, Sink + .05);
      if (cAge >= -.2) floorDoor('open carrier', caster, cd.open, cd.alpha, { s });
      const strumAge = s - t.strumAt;
      if (cAge < Door0) nakime('open carrier', caster, sun, strength, { seated: false, strum: strumAge > -.25 ? strumAge : null });
      else {
        const u = clamp((cAge - Door0) / Sink);
        if (u < 1) nakime('open carrier', { x: caster.x, z: caster.z - .18 * smooth(u) }, sun, strength, { seated: false, dark: smooth(u) * .85, scale: 1 - .5 * smooth(u), alpha: 1 - smooth((u - .75) / .25) });
      }
      strum('open strum', biwaAt, strumAge, { reach: 3, life: .5 });
      return;
    }

    // Return: doors at the taken-from cells, the carrier's cell, and beside the target cell.
    const ring = s - t.first;
    if (ring >= 0 && ring < .7) thinRing('open close', o, Radius * easeOut(ring / .3), .1, Strum.withAlpha(.5 * (1 - ring / .7)), L.fx);
    for (let i = 0; i < t.taken; i++) {
      const dead = i === KilledInside;
      const pos = dead ? at([.9, -.7]) : at(Hostiles[i]), age = s - (dead ? t.corpseDoor : t.doorOf(i)), d = doorAt(age, Rise * .75);
      if (age < doorEnd(Rise * .75)) floorDoor(`open back ${i}`, pos, d.open, d.alpha, { s });
      if (age < Door0) continue;
      const u = clamp((age - Door0) / Rise);
      if (dead) {
        if (u >= 1) sprite({ x: pos.x - .1, z: pos.z + .05 }, .9, .45, Blood.withAlpha(.7), soft, Floor + .02);
        rising('open corpse', pos, Corpse, u, sun, strength, { downed: 1 });
        if (u > .5) rifle({ x: pos.x + .15, z: pos.z - .45 }, 20, smooth((u - .5) / .5));
        continue;
      }
      // Back up, then on toward the colony (the carrier's side) at a walk.
      const walk = Math.max(0, age - Door0 - Rise - .2) * .6, dx = caster.x - pos.x, dz = caster.z - pos.z, L0 = Math.hypot(dx, dz) || 1;
      const q = { x: pos.x + dx / L0 * walk, z: pos.z + dz / L0 * walk };
      if (u < 1) rising(`open rise ${i}`, pos, Enemy, u, sun, strength);
      else figure(q, Enemy, sun, strength);
    }
    const cAge = s - t.casterDoor, cd = doorAt(cAge, Rise * .75);
    if (cAge < doorEnd(Rise * .75)) floorDoor('open carrier back', caster, cd.open, cd.alpha, { s });
    if (cAge >= Door0) {
      const u = clamp((cAge - Door0) / Rise), up = smooth(Math.min(1, u / .7));
      nakime('open carrier', { x: caster.x, z: caster.z - .18 * (1 - up) }, sun, strength, { seated: false, dark: (1 - up) * .85, scale: .5 + .5 * up, alpha: Math.min(1, u * 4) });
    }
  },
};
