// Samehada: Shark Skin — weapon proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed 2026-09-23, not agreed; the numbers are placeholders and will be XML
// fields). Spends 2 of the blade's charges (see Feed). For 10 s the bandage is gone and the scales
// stand off the flesh: every melee attack hits every hostile pawn in the 3 cells in front of the
// holder (the aimed cell and the two beside it, radius 1.5) instead of one target, and each pawn
// hit is drained as in Feed, so a sweep through three pawns puts three charges back. Needs at least
// 2 charges. Cooldown 600 ticks. When it ends the scales lie flat again; the bandage does not
// return until a charge is gained (Feed re-wraps one row per charge).
//
// Order (times with the default sliders, 3 charges at the start, three targets):
//   0.00  rest: the sword held 30 degrees low across the front, bandaged for 64 % of its length (3 charges)
//   0.20  tear 0.35 s: the bandage unwinds from its loose end back to the grip, 12 strips fly off sideways and land
//         on the floor; the tally loses 2 charges and the blade loses 0.3 cells
//   0.55  flare 0.25 s: the scales stand off and the blade widens 55 %, purple flesh shows; the blade lifts to 80 degrees left
//   0.80  sweep: the blade swings from 80 degrees left of the aim to 80 right in 0.32 s. The three
//         targets are hit as the blade passes each; a floor arc marks the 3 cells covered
//   0.95  drains 0.45 s each, starting as each target is hit; the blade grows one charge per drain
//   1.12  recover 0.3 s: the blade settles back to 30 degrees low
//   1.60  hold: the blade flared and bare, three targets hazed, the tally at 4
//   2.60  end
//
// Drawing: the weapon is lib/samehada.js (shared with Feed). The sweep is a level fan at hand
// height, so it turns with the aim and needs no per-facing method (aiming west it is mirrored). The strips are thrown parcels
// that fall (rise - 0.5 g t^2) and stay where they land. Stand-ins from the Chain Sickle kit.
import { Color, Mathf } from '../js/engine.js';
import { P, Y, Floor, Lift, sprite, circle, soft, glow, band, Body } from './lib/six-paths-impact.js';
import {
  frame, figure, samehada, drain, drained, heal, bite, tally, strip, bladeLength, BandagedAt, GripLength, HandH, ChestH, MaxCharges,
  Enemy, Holder, Pale, Wisp, Bandage, Flesh, FleshLit, Lead, Tail, easeOut, bump, screen, rand,
} from './lib/samehada.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const Cost = 2, ArcR = 1.5, ArcHalf = 60;     // charges spent; the hit area: radius and half-angle in degrees
const SweepFrom = 80, SweepTo = -80, Rest = -30, Recover = .3;   // sweep start and end, degrees left and right of the aim; the rest angle after
const Strips = 12, Rock = .12;
const Spots = { 'three in the arc': [[1.15, 0], [1.05, .95], [1.05, -.95]], 'one ahead': [[1.15, 0]], 'two off-side': [[1.05, .95], [1.05, -.95]] };
function times(p) {
  const tear0 = Lead, flare0 = tear0 + p.tear, sweep0 = flare0 + p.flare, sweepEnd = sweep0 + p.sweep;
  return { tear0, flare0, sweep0, sweepEnd, end: sweepEnd + p.drainT + p.hold + Tail };
}

export default {
  kit: 'Samehada', label: 'Shark Skin (sketch)',
  params: {
    actors: { label: 'Show holder and targets', value: true, group: 'Showcase' },
    showTally: { label: 'Show charge tally', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    start: P('Charges at the start', 3, 2, 5, 1, 'Showcase'),
    targets: { label: 'Targets', value: 'three in the arc', options: Object.keys(Spots), group: 'Showcase' },
    tear: P('Tear', .35, .15, .8, .05, 'Timing (s)'),
    flare: P('Flare', .25, .1, .6, .05, 'Timing (s)'),
    sweep: P('Sweep', .32, .15, .8, .02, 'Timing (s)'),
    drainT: P('Drain', .45, .2, 1, .05, 'Timing (s)'),
    hold: P('Show the result', 1.0, .3, 3, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Rest', t: 0 }, { name: 'Tear', t: t.tear0 }, { name: 'Flare', t: t.flare0 }, { name: 'Sweep', t: t.sweep0 }, { name: 'Result', t: t.sweepEnd + p.drainT },
  ]; },
  events(p) { const t = times(p); return [
    { t: t.tear0, type: 'sound', def: 'RimArt_SamehadaTear' }, { t: t.flare0, type: 'sound', def: 'RimArt_SamehadaFlare' },
    { t: t.sweep0, type: 'sound', def: 'RimArt_SamehadaSweep' }, { t: t.sweep0 + p.sweep * .5, type: 'shake', value: .02 },
  ]; },

  draw(s, p, { origin: centre, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const f = frame(p.aim, sun), { ground } = f;
    const caster = ground(centre, -.5, 0);
    const spots = Spots[p.targets] ?? Spots['three in the arc'];

    // The sweep angle, and when each target is hit: the blade passes a target's bearing.
    // Facing west the sweep is mirrored (Feed does the same), so the blade rests on the low side.
    const sign = Math.cos(p.aim * Mathf.Deg2Rad) < -1e-6 ? -1 : 1;
    const sweepU = s < t.sweep0 ? 0 : easeOut(clamp((s - t.sweep0) / p.sweep));
    const rel = sign * (s < t.sweep0 ? lerp(Rest, SweepFrom, smooth(clamp((s - t.flare0) / p.flare))) : s < t.sweepEnd ? lerp(SweepFrom, SweepTo, sweepU) : lerp(SweepTo, Rest, smooth((s - t.sweepEnd) / Recover)));
    const deg = p.aim + rel;
    const hitTimes = spots.map(([along, across]) => {
      const bearing = sign * Math.atan2(across, along) / Mathf.Deg2Rad;   // relative to the aim, positive left
      const u = clamp((SweepFrom - bearing) / (SweepFrom - SweepTo));
      return t.sweep0 + p.sweep * (1 - Math.cbrt(1 - u));            // inverse of easeOut
    });

    // Charges: spend 2 over the tear, gain one per drain.
    let charges = p.start - Cost * smooth(clamp((s - t.tear0) / p.tear));
    hitTimes.forEach(h => { if (s >= h) charges += clamp((s - h) / p.drainT); });
    charges = Math.min(MaxCharges, charges);
    const tear = smooth(clamp((s - t.tear0) / p.tear));
    const flare = smooth(clamp((s - t.flare0) / p.flare));

    // Floor: the tally, the hit arc (from the flare on), the Drained hazes.
    if (p.showTally) tally('shark', caster, charges, p.aim);
    if (s >= t.flare0) {
      const a = .45 * Math.min(1, (s - t.flare0) / .2), A = [], B = [];
      for (let i = 0; i <= 20; i++) {
        const ang = (p.aim + lerp(ArcHalf, -ArcHalf, i / 20)) * Mathf.Deg2Rad;
        A.push({ x: caster.x + Math.cos(ang) * (ArcR - .5), z: caster.z + Math.sin(ang) * (ArcR - .5) });
        B.push({ x: caster.x + Math.cos(ang) * ArcR, z: caster.z + Math.sin(ang) * ArcR });
      }
      band('shark arc', A, B, Pale.withAlpha(a * .25), Floor + .02);
      band('shark arc rim', B.map((q, i) => ({ x: lerp(A[i].x, q.x, .94), z: lerp(A[i].z, q.z, .94) })), B, Pale.withAlpha(a), Floor + .021);
    }
    const targets = spots.map(([along, across], i) => {
      const hitAge = s - hitTimes[i], rock = hitAge >= 0 && hitAge < .35 ? Rock * bump(hitAge / .35) : 0;
      const dir = Math.atan2(across, along), pos = ground(caster, along + Math.cos(dir) * rock, across + Math.sin(dir) * rock);
      drained(`shark drained ${i}`, pos, hitAge >= 0 ? 1 : 0, s);
      return { pos, hitAge, chest: { x: pos.x, z: pos.z + ChestH * Lift }, bearing: p.aim + dir / Mathf.Deg2Rad };
    });

    // Torn strips: strip i leaves the bandage edge at tear0 + i/Strips x tear, flies off sideways.
    const hr0 = p.aim * Mathf.Deg2Rad;
    const handRest = { x: caster.x + Math.cos(hr0) * .22 - Math.sin(hr0) * .12, z: caster.z + Math.sin(hr0) * .22 + Math.cos(hr0) * .12 };
    const hr = deg * Mathf.Deg2Rad;
    const hand = { x: caster.x + Math.cos(hr) * .22 - Math.sin(hr) * .12, z: caster.z + Math.sin(hr) * .22 + Math.cos(hr) * .12 };
    if (s >= t.tear0) {
      const len0 = bladeLength(p.start), wrap0 = BandagedAt(p.start);
      for (let i = 0; i < Strips; i++) {
        const t0 = t.tear0 + (i / Strips) * p.tear, age = s - t0;
        if (age < 0) continue;
        const along = GripLength + len0 * wrap0 * (1 - i / Strips), side = (i % 2 ? 1 : -1);
        const start = { x: handRest.x + Math.cos(hr0) * along - Math.sin(hr0) * side * .1, z: handRest.z + Math.sin(hr0) * along + Math.cos(hr0) * side * .1, h: HandH };
        const vx = -Math.sin(hr0) * side * (1.2 + rand(i + 300) * .8) + Math.cos(hr0) * (rand(i + 310) - .5) * .8;
        const vz = Math.cos(hr0) * side * (1.2 + rand(i + 300) * .8) + Math.sin(hr0) * (rand(i + 310) - .5) * .8;
        strip(`shark strip ${i}`, start, vx, vz, .8 + rand(i + 320) * .6, age, p.aim + rand(i + 330) * 180);
      }
    }

    if (p.actors) {
      figure(caster, Holder, sun, strength);
      targets.forEach(q => figure(q.pos, Enemy, sun, strength));
    }

    // The flare: purple light and a few loose scales shaking off when the scales stand up.
    if (s >= t.flare0 && s < t.sweep0 + .1) {
      const u = clamp((s - t.flare0) / (p.flare + .1)), from = screen({ ...hand, h: HandH }), len = bladeLength(charges);
      const c = { x: from.x + Math.cos(hr) * (GripLength + len * .55), z: from.z + Math.sin(hr) * (GripLength + len * .55) };
      sprite(c, len * 1.2, .7, FleshLit.withAlpha(.35 * Math.sin(u * Math.PI)), glow, Y + .04, -deg);
    }

    // Sweep blur: ghosts of the blade behind it.
    if (s >= t.sweep0 && s < t.sweepEnd + .08) {
      const from = screen({ ...hand, h: HandH }), len = bladeLength(charges);
      for (let i = 1; i <= 8; i++) {
        const a2 = (p.aim + lerp(SweepFrom * sign, rel, 1 - i * .1)) * Mathf.Deg2Rad;
        const c = { x: from.x + Math.cos(a2) * (GripLength + len * .55), z: from.z + Math.sin(a2) * (GripLength + len * .55) };
        sprite(c, len * .95, .4, Color.Lerp(Wisp, FleshLit, .4).withAlpha(.22 * (1 - i / 9) * Math.min(1, sweepU * 4)), soft, Y + .04, -(p.aim + lerp(SweepFrom * sign, rel, 1 - i * .1)));
      }
    }

    const drinking = targets.some(q => q.hitAge >= 0 && q.hitAge < p.drainT);
    const hot = drinking ? Math.max(...targets.map(q => q.hitAge >= 0 && q.hitAge < p.drainT ? Math.sin(q.hitAge / p.drainT * Math.PI) : 0)) : 0;
    const sword = samehada('shark sword', hand, deg, charges, sun, strength, { flare, tear, hot });

    let healing = -1;
    targets.forEach((q, i) => {
      if (q.hitAge < 0) return;
      bite(`shark bite ${i}`, q.chest, q.bearing, q.hitAge);
      if (q.hitAge < p.drainT) { drain(`shark drain ${i}`, q.chest, sword.tip, q.hitAge, p.drainT); healing = Math.max(healing, q.hitAge); }
    });
    if (healing >= 0) heal(caster, healing - .1, p.drainT);
  },
};
