// Samehada: Feed — weapon proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed 2026-09-23, not agreed; the numbers are placeholders and will be XML
// fields). The pawn carries Samehada, a shark-skin greatsword wrapped in bandages. It is a 1-cell
// melee weapon (14 cut, slow). Feed is its passive: every melee hit drinks from the target.
//   target: +1 stack of Drained for 20 s, each stack -10 % consciousness and -10 % move
//   holder: heals 4 HP per hit
//   blade: +1 charge, up to 5. Each charge adds +2 damage, lengthens the blade 0.15 cells (
//          1.0 at zero, 1.75 at five) and uncovers one more band of scales from the tip back.
// Charges are what Shark Skin and Fusion spend. A charge is lost every 60 s without a hit.
//
// Order (times with the default sliders, three hits from zero charges):
//   0.00  rest: the sword held 30 degrees low across the front, wrapped, at the starting charge
//   0.20  hit 1 wind-up: the blade swings back to 70 degrees behind the aim over 0.22 s
//   0.42  swing: 82 degrees forward, ending on the target's chest, in 0.14 s, a blur behind it
//   0.56  bite: pale flash at the chest, three scratches, the target rocks back 0.12 cells
//   0.56  drain 0.45 s: chakra parcels run from the chest to the tip; the tip grows one charge
//         (0.15 cells) and the bandage edge slides back one row of scales; the holder glows green
//   1.10  recover to rest, 0.25 s; hit 2 follows at the same spacing (1.15 s per hit)
//   3.65  result: the sword one row longer per hit, the target hazed with one ring per stack,
//         the tally showing the charges
//
// Drawing: the weapon is lib/samehada.js (shared with Shark Skin). It lies level at hand height
// and turns with the aim, so nothing needs a per-facing method; it draws under the pawn layer when
// pointing north. Aiming west, the swing angles are mirrored so the blade rests on the low side. Caster and target are the Chain Sickle stand-ins.
import { Color, Mathf } from '../js/engine.js';
import { P, Y, Floor, Lift, sprite, circle, soft, glow, Body } from './lib/six-paths-impact.js';
import {
  frame, figure, samehada, drain, drained, heal, bite, tally, bladeLength, GripLength, HandH, ChestH, MaxCharges,
  Enemy, Holder, Pale, Wisp, Lead, Tail, easeOut, bump, screen,
} from './lib/samehada.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const Rest = -30, Back = 70, Through = 12; // blade angle at rest, behind the aim at the top of the swing, past it at the end
const Recover = .25, Rock = .12;          // recovery time; how far the target rocks back at the bite
const Reach = 1.15;                       // holder to target, cells (melee)
function times(p) {
  const per = p.windup + p.swing + p.drainT + Recover, first = Lead;
  return { per, first, end: first + per * p.hits + p.hold + Tail };
}
// Where the swing is at age `a` into one hit cycle: degrees relative to the aim.
function swingAngle(a, p) {
  if (a < p.windup) return lerp(Rest, -Back, smooth(a / p.windup));
  if (a < p.windup + p.swing) return lerp(-Back, Through, easeOut((a - p.windup) / p.swing) ** 1.2);
  const rest0 = p.windup + p.swing + p.drainT;
  if (a < rest0) return Through;
  return lerp(Through, Rest, smooth((a - rest0) / Recover));
}

export default {
  kit: 'Samehada', label: 'Feed (sketch)',
  params: {
    actors: { label: 'Show holder and target', value: true, group: 'Showcase' },
    showTally: { label: 'Show charge tally', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    start: P('Charges at the start', 0, 0, 4, 1, 'Showcase'),
    hits: P('Hits', 3, 1, 5, 1, 'Showcase'),
    windup: P('Wind-up', .22, .1, .6, .02, 'Timing (s)'),
    swing: P('Swing', .14, .06, .4, .02, 'Timing (s)'),
    drainT: P('Drain', .45, .2, 1, .05, 'Timing (s)'),
    hold: P('Show the result', 1.0, .3, 3, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p), out = [{ name: 'Rest', t: 0 }];
    for (let i = 0; i < p.hits; i++) {
      const b = t.first + i * t.per;
      out.push({ name: `Hit ${i + 1}`, t: b }, { name: 'Bite', t: b + p.windup + p.swing });
    }
    out.push({ name: 'Result', t: t.first + p.hits * t.per });
    return out;
  },
  events(p) {
    const t = times(p), out = [];
    for (let i = 0; i < p.hits; i++) {
      const hit = t.first + i * t.per + p.windup + p.swing;
      out.push({ t: hit, type: 'sound', def: 'RimArt_SamehadaBite' }, { t: hit, type: 'shake', value: .015 });
    }
    return out;
  },

  draw(s, p, { origin: centre, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const f = frame(p.aim, sun), { ground } = f;
    const caster = ground(centre, -Reach / 2, 0), target0 = ground(centre, Reach / 2, 0);

    // Replay the hits up to now: charges, stacks, and the current hit's age.
    const maxHits = Math.min(p.hits, MaxCharges - p.start);
    let charges = p.start, stacks = 0, cur = -1, age = 0;
    for (let i = 0; i < p.hits; i++) {
      const b = t.first + i * t.per, hit = b + p.windup + p.swing;
      if (s < b) break;
      cur = i; age = s - b;
      if (s >= hit) {
        stacks = i + 1;
        if (i < maxHits) charges = p.start + i + clamp((s - hit) / p.drainT);
      }
    }
    const inCycle = cur >= 0 && age < t.per;
    const hitAge = inCycle ? age - p.windup - p.swing : -1;
    // Facing west the swing is mirrored, so the blade always rests on the screen's low side and
    // the cut rises from there. The angle is relative to the aim; only its sign changes.
    const sign = Math.cos(p.aim * Mathf.Deg2Rad) < -1e-6 ? -1 : 1;
    const rel = sign * (inCycle ? swingAngle(age, p) : Rest);
    const deg = p.aim + rel;

    // The target rocks back at the bite.
    const rock = hitAge >= 0 && hitAge < .35 ? Rock * bump(hitAge / .35) : 0;
    const target = ground(target0, rock, 0);
    const chest = { x: target.x, z: target.z + ChestH * Lift };

    // Floor first.
    if (p.showTally) tally('feed', caster, charges, p.aim);
    drained('feed', target, stacks, s);

    // Hand: the right hand, out in front and a little to the side, turning with the swing.
    const hr = deg * Mathf.Deg2Rad;
    const hand = { x: caster.x + Math.cos(hr) * .22 - Math.sin(hr) * .12, z: caster.z + Math.sin(hr) * .22 + Math.cos(hr) * .12 };

    if (p.actors) {
      figure(caster, Holder, sun, strength);
      figure(target, Enemy, sun, strength);
    }

    // Swing blur: a fan of faint blade ghosts behind the blade during the swing.
    if (inCycle && age >= p.windup && age < p.windup + p.swing + .06) {
      const u = clamp((age - p.windup) / p.swing), len = bladeLength(Math.floor(charges)), from = screen({ ...hand, h: HandH });
      for (let i = 1; i <= 6; i++) {
        const a2 = (p.aim + lerp(-Back * sign, rel, 1 - i * .12)) * Mathf.Deg2Rad;
        const c = { x: from.x + Math.cos(a2) * (GripLength + len * .55), z: from.z + Math.sin(a2) * (GripLength + len * .55) };
        sprite(c, len * .9, .32, Wisp.withAlpha(.22 * (1 - i / 7) * Math.min(1, u * 3)), soft, Y + .04, -(p.aim + lerp(-Back * sign, rel, 1 - i * .12)));
      }
    }

    const drinking = hitAge >= 0 && hitAge < p.drainT && cur < maxHits;
    const hot = drinking ? Math.sin(clamp(hitAge / p.drainT) * Math.PI) : 0;
    const sword = samehada('feed sword', hand, deg, charges, sun, strength, { hot });

    if (hitAge >= 0) bite('feed bite', chest, p.aim, hitAge);
    if (drinking) {
      drain('feed drain', chest, sword.tip, hitAge, p.drainT);
      heal(caster, hitAge - .1, p.drainT);
    }
    if (cur >= maxHits && hitAge >= 0 && hitAge < .5) {
      // Full: nothing more to drink into the blade. A short grey puff at the tip says so.
      sprite(sword.tip, .3, .3, Wisp.withAlpha(.5 * (1 - hitAge / .5)), soft, Y + .09);
    }
  },
};
