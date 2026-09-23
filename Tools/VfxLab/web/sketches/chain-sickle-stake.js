// Chain Sickle: Stake — weapon ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (agreed 2026-09-22 in outline; the numbers are placeholders and will be XML
// fields). Stake is only available while a pawn is snagged (the chain is still coiled on it after
// Snag). The holder yanks the chain: the coil tightens around the chest and pins the arms, and the
// weight drives into the floor beside the pawn's feet. The pawn drops whatever it holds, and while
// pinned it cannot move, shoot or melee. The pin lasts 6 s x the same weight ratio Snag uses
// (holder Carrying Capacity / target Mass + gear), capped at 6 s: a 75 kg-carry human pins a 65 kg
// tribal 6 s, a 95 kg raider 4.7 s, a 140 kg muffalo 3.2 s. Below ratio 0.4 (a thrumbo) Stake is
// not allowed and the button greys out "too heavy". The dropped weapon lies on the floor and can
// be picked up again once the pin ends. The pin holds while the holder stays within the chain's
// 7 cells; walking further pulls the stake out. The sickle does 1.5x (18 cut) on a staked pawn.
// Cooldown 30 s.
//
// Order (times with the default sliders):
//   0.00  snagged: the pawn stands 2 cells away, coiled at the chest, chain taut to the hand
//   0.30  yank: the holder pulls, the coil tightens, the weight drops on a tether from the coil
//   0.45  stake: the weight hits the floor beside the feet, dust and a crack that stays; the
//         pawn's rifle falls and lies on the floor; a floor ring shows the pin and shrinks over
//         its computed time (6 s for the tribal); an animal has nothing to drop
//   1.20  tries to run: the pinned pawn strains away and the chain snaps it back (1.2, 2.4 s);
//         the other scenario stands still
//   3.10  step in: the holder walks to 1 cell, the chain goes slack
//   3.40  cut: the sickle swings across the pawn, a large flash for the 1.5x hit and blood drops
//   3.60  result: the pawn stays pinned and unarmed, the crack, rifle and drops stay
//
// Drawing: the weapon is lib/chain-sickle.js (shared with Snag). The stake is the weight drawn
// with its spike buried; the coil is a level spiral split at the pawn so its far half draws under
// the pawn layer; a short tether runs from the coil's end down to the stake. The sickle swing
// pivots the flat sickle about the hand. Nothing needs a per-facing method. The rifle is a
// stand-in.
import { Color, Mathf } from '../js/engine.js';
import { P, Y, Floor, Lift, sprite, circle, band, soft, glow, Body, rand } from './lib/six-paths-impact.js';
import {
  frame, figure, beast, chain, coil, chainPath, coilPath, weight, sickle, crack, rifle, kick, puff, rule as weightRule, Targets,
  Enemy, Holder, Pale, Flash, Blood, Dust, HandH, ChestH, CoilR, CoilTurns, Tail, bump, easeOut,
} from './lib/chain-sickle.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const Yank = .15, Tighten = .2;       // the weight's fall, the coil closing on the chest
const Drop = .3;                      // the rifle's fall
const StepIn = .25, Swing = .12;      // walking to 1 cell, the cut
const Strain = .18;                   // how far a pinned pawn leans away before the chain stops it, cells

function times(p) {
  const yank = p.lead, staked = yank + Yank, step0 = p.swingAt - StepIn, swing0 = p.swingAt, cut = swing0 + Swing * .6;
  return { yank, staked, step0, swing0, cut, end: swing0 + Swing + p.hold + Tail };
}

export default {
  kit: 'Chain Sickle', label: 'Stake (sketch)',
  params: {
    actors: { label: 'Show caster and target', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Target distance (cells)', 2, 1.5, 5, .5, 'Showcase'),
    target: { label: 'Target', value: 'tribal 65 kg', options: Object.keys(Targets).filter(k => weightRule(75, k).pin > 0), group: 'Showcase' },
    scenario: { label: 'Pinned pawn', value: 'tries to run', options: ['tries to run', 'stands still'], group: 'Showcase' },
    lead: P('Snagged before the yank', .3, .1, 1, .05, 'Timing (s)'),
    swingAt: P('Sickle cut at', 3.4, 1.5, 6, .1, 'Timing (s)'),
    hold: P('Show the result', 1.0, .3, 3, .1, 'Timing (s)'),
    carry: P('Holder carrying capacity (kg)', 75, 40, 160, 5, 'Rule'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Snagged', t: 0 }, { name: 'Yank', t: t.yank }, { name: 'Staked', t: t.staked },
    { name: 'Step in', t: t.step0 }, { name: 'Cut', t: t.swing0 }, { name: 'Result', t: t.cut },
  ]; },
  events(p) { const t = times(p); return [
    { t: t.staked, type: 'sound', def: 'RimArt_ChainStake' }, { t: t.staked, type: 'shake', value: .04 },
    { t: t.cut, type: 'sound', def: 'RimArt_SickleCut' }, { t: t.cut, type: 'shake', value: .05 },
  ]; },

  draw(s, p, { origin: centre, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const f = frame(p.aim, sun), { ground } = f;
    const o = ground(centre, p.distance / 2, 0);
    const runs = p.scenario === 'tries to run';
    const R = weightRule(p.carry, p.target), big = Math.sqrt(R.size), animal = R.size > 1, pin = R.pin || 1.5;

    // The holder: at `distance` until step-in, then 1 cell from the target.
    const stepU = s < t.step0 ? 0 : smooth((s - t.step0) / StepIn);
    const caster = ground(centre, lerp(-p.distance / 2, p.distance / 2 - 1.0, stepU), 0);
    const hand = ground(caster, .20, .18);

    // The pinned pawn: strains away and is snapped back (run scenario).
    let strain = 0;
    if (runs) for (const at of [1.2, 2.4]) { const a = s - at; if (a >= 0 && a < .5) strain += Strain * (a < .3 ? smooth(a / .3) : 1 - smooth((a - .3) / .12)); }
    const body = ground(o, strain, 0);

    // Stake geometry: the weight ends beside the far foot, in the floor.
    const stakeAt = ground(o, .22 * big, -.30 * big);
    const yankU = s < t.yank ? 0 : clamp((s - t.yank) / Yank);
    const tightU = s < t.yank ? 0 : smooth((s - t.yank) / Tighten);
    const stakedU = s < t.staked ? 0 : 1;

    // Floor first: the crack, the pin ring counting down, the blood drops.
    const stakeAge = s - t.staked;
    crack('stake', stakeAt, stakeAge < 0 ? 0 : smooth(stakeAge / .12), 3);
    if (stakeAge >= 0) {
      const left = 1 - clamp(stakeAge / pin);
      circle(o, .55 * big, .3, Floor + .01, Pale);
      circle(o, .55 * big * left, .55, Floor + .012, Pale);
    }
    const cutAge = s - t.cut;
    if (cutAge >= 0) for (let i = 0; i < 5; i++) {
      const u = clamp(cutAge / (.25 + rand(i + 70) * .15)), a = (rand(i + 80) - .5) * 2.4 + f.a + Math.PI / 2;
      const d = (.25 + rand(i + 90) * .5) * big * easeOut(u), h = Math.max(0, ChestH * big * (1 - u) * (1 - u) + .3 * u * (1 - u));
      const q = { x: body.x + Math.cos(a) * d, z: body.z + Math.sin(a) * d };
      if (u >= 1) sprite(q, .12 + rand(i) * .08, .09 + rand(i + 5) * .05, Blood.withAlpha(.8), soft, Floor + .02);
      else sprite({ x: q.x, z: q.z + h * Lift }, .08, .1, Blood.withAlpha(.95), soft, Y + .06);
    }

    // The coil: stays at the chest and tightens on the arms.
    const cr = lerp(CoilR, .21, tightU) * big;
    const coilPts = coilPath(body, CoilTurns, cr, (ChestH + .1) * big, .25 * big, p.aim + 180);

    // The weight: rides the coil's end, then drops to the stake point during the yank.
    const coilEnd = coilPts[coilPts.length - 1];
    let w;
    if (yankU <= 0) w = coilEnd;
    else if (yankU < 1) w = { x: lerp(coilEnd.x, stakeAt.x, yankU), z: lerp(coilEnd.z, stakeAt.z, yankU), h: lerp(coilEnd.h, 0, yankU * yankU) };
    else w = { ...stakeAt, h: 0 };

    // The chain from hand to coil: taut while the holder stands back, slack after stepping in.
    // During the strain the chain snaps straight; a short section coil -> stake is always taut.
    const slack = stepU * .3 + (s < t.yank ? .03 : .1 * (1 - stepU)) - (strain > 0 ? .06 : 0);
    const path = chainPath({ ...hand, h: HandH }, coilPts[0], Math.max(.01, slack), 24, strain > 0 ? .01 : 0, s);
    const tether = yankU > 0 ? chainPath(coilEnd, w, .01, 6) : null;

    if (p.actors) {
      figure(caster, Holder, sun, strength);
      if (animal) beast(body, R.size, sun, strength); else figure(body, Enemy, sun, strength);
      if (runs && strain > .05) kick(body, s, .6, 11);
    }
    if (stakeAge >= 0 && stakeAge < .5) {
      // Dust from the stake hitting the floor.
      for (let i = 0; i < 8; i++) {
        const u = clamp(stakeAge / (.3 + rand(i + 50) * .2)); if (u >= 1) continue;
        const a = rand(i + 60) * Math.PI * 2, d = .15 + u * .5;
        sprite({ x: stakeAt.x + Math.cos(a) * d, z: stakeAt.z + Math.sin(a) * d * .7 + Math.sin(u * Math.PI) * .2 * Lift }, .2 + u * .25, .16 + u * .2, Dust.withAlpha(Math.sin(u * Math.PI) * .6), puff, Y + .01);
      }
    }

    // The sickle: held along the aim, then swung 120 degrees across the pawn about the hand.
    let deg = p.aim - 10;
    const swingAge = s - t.swing0;
    if (swingAge >= 0) {
      const u = clamp(swingAge / Swing);
      deg = p.aim - 70 + 120 * easeOut(u);
      if (swingAge < Swing + .2) {
        // The cut's arc: a pale sweep at hand height that fades.
        const fade = 1 - clamp((swingAge - Swing) / .2), a0 = (p.aim - 70) * Mathf.Deg2Rad, a1 = deg * Mathf.Deg2Rad;
        const inner = [], outer = [], n = 12, hz = HandH * Lift;
        for (let i = 0; i <= n; i++) {
          const a = lerp(a0, a1, i / n);
          inner.push({ x: hand.x + Math.cos(a) * .45, z: hand.z + hz + Math.sin(a) * .45 });
          outer.push({ x: hand.x + Math.cos(a) * .72, z: hand.z + hz + Math.sin(a) * .72 });
        }
        band('stake cut arc', inner, outer, Pale.withAlpha(.55 * fade), Y + .045);
      }
    }
    sickle('stake sickle', hand, HandH, deg, sun, strength);
    chain('stake chain', path, sun, strength);
    coil('stake coil', coilPts, body.z + .02, sun, strength);
    if (tether) chain('stake tether', tether, sun, strength, Y + .02);
    weight(w, sun, strength, Y + .03, { staked: stakedU * smooth(stakeAge / .1) });

    // The 1.5x hit: a large flash across the chest plus a red gash.
    if (cutAge >= 0 && cutAge < .25) {
      const fl = 1 - cutAge / .25, chest = { x: body.x, z: body.z + ChestH * big * Lift };
      sprite(chest, 1.4 * fl + .3, 1.0 * fl + .2, Flash.withAlpha(fl * .9), glow, Y + .08);
      sprite(chest, .5, .12, Blood.withAlpha(fl), soft, Y + .085, p.aim + 40);
    }

    // The rifle: held across the chest until the yank, then it falls beside the feet and stays.
    const dropU = s < t.yank ? 0 : clamp((s - t.yank) / Drop);
    if (animal) return;                                   // an animal holds nothing
    const restAt = ground(o, -.15, -.28);
    const held = { x: body.x, z: body.z, h: ChestH };
    const rq = { x: lerp(held.x, restAt.x, easeOut(dropU)), z: lerp(held.z, restAt.z, easeOut(dropU)), h: ChestH * (1 - dropU) * (1 - dropU) + .12 * bump(dropU) };
    rifle('stake rifle', rq, p.aim + 80 + 60 * easeOut(dropU), sun, strength, dropU >= 1 ? Floor + .03 : Y + .06);
  },
};
