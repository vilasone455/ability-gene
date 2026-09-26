// Nezuko's box: Come out — equipment proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed 2026-09-26; the numbers are placeholders and will be XML fields). An
// ability of the box. Target a cell within 4. The pawn inside bursts out of the top and lands
// there (the game's pawn flyer, which draws any pawn as it is). If an enemy stands next to the
// landing cell, the pawn makes one melee attack with its own weapon or natural attack that cannot
// miss, and the enemy is stunned 1.5 s. A downed pawn is set down gently on a cell next to the box
// instead. Usable while the wearer is downed. Pawns not of the colony come out plain (no leap, no
// strike). At the 12 h limit, or when rested, the pawn steps out plainly by itself. Guard (a
// toggle on the box) does the strike version by itself when an enemy hits the wearer in melee or
// the wearer goes down, once.
//
// Order, "strike" (times with the default sliders, 3 cells):
//   0.00  the wearer with the box, someone asleep inside; the landing cell marked on the floor
//   0.20  rumble 0.25 s: the box shakes, the lid rattles, pink light at the top
//   0.45  burst: the top lid flips up in 0.1 s (hinged on the wearer's side), pink light, wood dust
//   0.50  leap 0.45 s: the pawn shoots up out of the top (from 0.7 to full size in 0.12 s) and
//         arcs to the landing cell, a short pink streak behind it
//   0.95  landing: dust ring on the cell, a small shake
//   1.00  strike: flash and three slash streaks on the enemy, it is knocked back 0.12 cells and
//         dazed for 1.5 s
//   1.40  the lid drops shut over 0.2 s
// The burst uses the top lid because the top shows in every facing (the door faces away from the
// camera when the wearer faces south) and the leap goes up first; the anime box has only the door.
// "downed ally": no rumble, the door opens over 0.3 s and the pawn is laid on the cell next to the
// door under a soft puff. "time up": the door opens gently and the pawn steps out to that cell.
//
// Drawing: the box is lib/nezuko-box.js; the leaping pawn, the wearer and the enemy are stand-ins.
// In game the leap is a PawnFlyer and the strike is the pawn's own melee verb, so nothing here is
// drawn per pawn type.
import { Color } from '../js/engine.js';
import { drawBox, figure, woodDust, dazeMarks, sprite, trail, glow, puff, smooth, clamp, lerp, bump, Wearer, Ally, Enemy, Pink, Pale, Dust, Back, HalfDepth, H0, H1, Floor, Y, Lift, TAU, rand } from './lib/nezuko-box.js';
import { P, circle } from './lib/six-paths-impact.js';

const Rumble = .25, Burst = .1, Emerge = .12, Strike = .05, Dazed = 1.5, Shut = .2;

function times(p) {
  if (p.scenario !== 'strike') {
    const open0 = .2, open = .3, out0 = open0 + open, out = p.scenario === 'time up' ? .45 : .3, shut0 = out0 + out + .3;
    return { open0, open, out0, out, shut0, end: shut0 + Shut + p.hold };
  }
  const rumble0 = .2, burst = rumble0 + Rumble, leap0 = burst + .05, land = leap0 + p.flight, strike = land + Strike;
  return { rumble0, burst, leap0, land, strike, shut0: strike + .4, end: strike + Math.max(Dazed + .3, p.hold) };
}

export default {
  kit: "Nezuko's Box", label: 'Come out (sketch)',
  params: {
    actors: { label: 'Show the wearer and the pawns', value: true, group: 'Showcase' },
    facing: P('Wearer faces (degrees)', 270, 0, 270, 90, 'Showcase'),
    aim: P('Landing direction (degrees)', 0, 0, 360, 15, 'Showcase'),
    distance: P('Landing distance (cells)', 3, 1, 4, .5, 'Rule'),
    scenario: { label: 'How it comes out', value: 'strike', options: ['strike', 'downed ally', 'time up'], group: 'Showcase' },
    flight: P('Leap', .45, .25, .9, .05, 'Timing (s)'),
    peak: P('Leap height (cells)', 1.1, .4, 2, .1, 'Shape'),
    hold: P('Show the result', 1.2, .3, 3, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    if (p.scenario !== 'strike') return [{ name: 'Open', t: t.open0 }, { name: 'Out', t: t.out0 }, { name: 'Shut', t: t.shut0 }];
    return [{ name: 'Rumble', t: t.rumble0 }, { name: 'Burst', t: t.burst }, { name: 'Leap', t: t.leap0 }, { name: 'Land', t: t.land }, { name: 'Strike', t: t.strike }, { name: 'Shut', t: t.shut0 }];
  },
  events(p) {
    const t = times(p);
    if (p.scenario !== 'strike') return [{ t: t.open0, type: 'sound', def: 'RimArt_BoxDoorOpen' }];
    return [
      { t: t.burst, type: 'sound', def: 'RimArt_BoxBurst' }, { t: t.burst, type: 'shake', value: .03 },
      { t: t.land, type: 'shake', value: .04 }, { t: t.strike, type: 'shake', value: .06 }, { t: t.strike, type: 'sound', def: 'RimArt_BoxKick' },
    ];
  },

  draw(s, p, { origin, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const feet = { x: origin.x, z: origin.z };
    const strikeMode = p.scenario === 'strike', downed = p.scenario === 'downed ally';
    const fa = p.facing * Math.PI / 180, f = { x: Math.cos(fa), z: Math.sin(fa) };
    const a = p.aim * Math.PI / 180, dir = { x: Math.cos(a), z: Math.sin(a) };

    // Door, glow, sleeping, shake.
    let door = 0, lid = 0, inner, sleeping, shake = 0;
    if (strikeMode) {
      // The burst goes out through the top lid; during the rumble the lid rattles on its hinge.
      lid = s < t.burst ? (s >= t.rumble0 ? .07 * Math.abs(Math.sin((s - t.rumble0) * 70)) * smooth((s - t.rumble0) / Rumble) : 0)
        : s < t.shut0 ? smooth((s - t.burst) / Burst) : 1 - smooth((s - t.shut0) / Shut);
      inner = lid * (1 - smooth((s - t.leap0 - .2) / .4)) + .3 * lid;
      sleeping = s < t.burst ? 1 + .6 * smooth((s - t.rumble0) / Rumble) : 0;
      if (s >= t.rumble0 && s < t.burst) shake = .025 * smooth((s - t.rumble0) / Rumble) * Math.sin((s - t.rumble0) * 150);
    } else {
      door = s < t.open0 ? 0 : s < t.shut0 ? smooth((s - t.open0) / t.open) : 1 - smooth((s - t.shut0) / Shut);
      inner = door * .6;
      sleeping = 1 - smooth((s - t.open0) / .2);
    }

    if (p.actors) figure(feet, Wearer, sun, strength);
    const box = drawBox('come out', feet, p.facing, { door, lid, sleeping: clamp(sleeping), inner, shake, t: s, sun, strength });
    if (sleeping > 1) sprite(box.topMouth.screen, .55, .4, Pink.withAlpha(.5 * (sleeping - 1)), glow, Y + .05);

    // Where it lands: for the strike the chosen cell; otherwise the cell just outside the door.
    const land = strikeMode ? { x: feet.x + dir.x * p.distance, z: feet.z + dir.z * p.distance }
      : { x: feet.x + (Back - HalfDepth - .9) * f.x, z: feet.z + (Back - HalfDepth - .9) * f.z };
    const ringA = strikeMode ? smooth(s / .15) * (1 - smooth((s - t.land) / .3)) : 0;
    if (ringA > 0) circle(land, .45, .6 * ringA, Floor + .01, Pale);

    if (strikeMode) {
      // The enemy next to the landing cell, beyond it along the leap.
      const E0 = { x: land.x + dir.x * .95, z: land.z + dir.z * .95 };
      const knock = s >= t.strike ? .12 * smooth((s - t.strike) / .15) : 0;
      const E = { x: E0.x + dir.x * knock, z: E0.z + dir.z * knock };
      if (p.actors) figure(E, Enemy, sun, strength);
      dazeMarks(E, s, s - t.strike, Dazed);

      // The leap: up out of the top and on an arc to the cell.
      const m = box.topMouth, hDoor = m.h;
      if (s >= t.leap0) {
        const u = clamp((s - t.leap0) / p.flight), e = smooth(u);
        const g = { x: lerp(m.ground.x, land.x, e), z: lerp(m.ground.z, land.z, e) };
        const h = s < t.land ? lerp(hDoor, 0, u) + Math.sin(u * Math.PI) * p.peak : 0;
        const size = lerp(.7, 1, smooth((s - t.leap0) / Emerge));
        // A short pink streak along the path just behind it.
        if (u > .02 && u < 1) {
          const pts = [];
          for (let k = 0; k <= 6; k++) {
            const v = Math.max(0, u - .18 * (1 - k / 6)), ev = smooth(v);
            const gv = { x: lerp(m.ground.x, land.x, ev), z: lerp(m.ground.z, land.z, ev) };
            const hv = lerp(hDoor, 0, v) + Math.sin(v * Math.PI) * p.peak;
            pts.push({ x: gv.x, z: gv.z + (hv + .35) * Lift });
          }
          trail('come out streak', pts, .22, Pink.withAlpha(.55), Y + .05);
        }
        if (p.actors) figure(g, Ally, sun, strength, { scale: size, h, layer: s < t.land ? Y + .06 : undefined });
      }
      // Landing dust ring and puffs.
      const la = s - t.land;
      if (la >= 0 && la < .5) {
        circle(land, .2 + la * 1.6, (1 - la / .5) * .55, Floor + .015, Dust);
        for (let i = 0; i < 7; i++) {
          const u = la / (.3 + rand(i + 70) * .2);
          if (u > 1) continue;
          const th = rand(i + 71) * TAU, far = u * (.35 + rand(i + 72) * .4);
          sprite({ x: land.x + Math.cos(th) * far, z: land.z + Math.sin(th) * far * .7 + Math.sin(u * Math.PI) * .1 }, .22 + u * .3, .18 + u * .25, Dust.withAlpha(Math.sin(u * Math.PI) * .55), puff, Y + .01 + i * .0005);
        }
      }
      // The strike: a flash and three slash streaks across the enemy's body.
      const sa = s - t.strike;
      if (sa >= 0 && sa < .35) {
        const c = { x: E0.x, z: E0.z + .35 };
        sprite(c, .9, .7, Pale.withAlpha(Math.max(0, 1 - sa / .1) * .9), glow, Y + .08);
        for (let i = 0; i < 3; i++) {
          const v = clamp(sa / .08 - i * .25), fade = 1 - clamp((sa - .1) / .25);
          if (v <= 0) continue;
          const off = (i - 1) * .1, ang = a + Math.PI / 2 + .5;
          const p0 = { x: c.x - Math.cos(ang) * .35 + dir.x * off, z: c.z - Math.sin(ang) * .35 + dir.z * off };
          const p1 = { x: c.x + Math.cos(ang) * .35 * (2 * v - 1) + dir.x * off, z: c.z + Math.sin(ang) * .35 * (2 * v - 1) + dir.z * off };
          trail(`come out slash ${i}`, [p0, { x: (p0.x + p1.x) / 2, z: (p0.z + p1.z) / 2 }, p1], .09, Pale.withAlpha(.95 * fade), Y + .085 + i * .001);
        }
      }
      woodDust('come out burst dust', box.topMouth.screen, s - t.burst, 1.1, .55, 90);
    } else {
      // Downed: laid on the cell under a soft puff. Time up: steps out of the doorway to the cell.
      const oa = s - t.out0;
      if (downed) {
        if (oa >= 0 && oa < .6) {
          const u = oa / .6;
          sprite({ x: land.x, z: land.z + .15 }, 1.0 * (.5 + u), .7 * (.5 + u), Pink.withAlpha(.45 * Math.sin(u * Math.PI)), puff, Y + .07);
        }
        if (oa >= t.out * .5 && p.actors) figure(land, Ally, sun, strength, { downed: true, alpha: smooth((oa - t.out * .5) / .2) });
      } else if (oa >= 0 && p.actors) {
        const u = smooth(oa / t.out), m = box.doorMouth.ground;
        figure({ x: lerp(m.x, land.x, u), z: lerp(m.z, land.z, u) }, Ally, sun, strength, { scale: lerp(.7, 1, smooth(oa / .15)), alpha: smooth(oa / .1) });
        if (oa > t.out && oa < t.out + .8) {
          const v = (oa - t.out) / .8;
          sprite({ x: land.x + .12, z: land.z + .75 + v * .25 }, .18 + v * .12, .14 + v * .1, Pale.withAlpha(.6 * Math.sin(v * Math.PI)), puff, Y + .03);
        }
      }
      woodDust('come out soft dust', box.doorMouth.screen, s - t.open0, .5, .5, 120);
    }
  },
};
