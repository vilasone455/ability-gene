// Chain Sickle: Snag — weapon ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (agreed 2026-09-22 in outline; the numbers are placeholders and will be XML
// fields). The pawn carries a kusarigama: a hand sickle on a 7-cell chain with an iron weight on
// the far end. It is a normal 1-cell melee weapon (12 cut). Snag targets a pawn up to 7 cells away
// with line of sight, 0.5 s to swing the weight up to speed, then the weight flies to the target
// and the chain wraps its body. The holder then reels the target toward them. How far and how
// fast depends on weight: ratio = holder Carrying Capacity (kg) / target Mass + carried gear (kg).
//   pull = 5 cells x ratio, capped at 5;  reel time = 1 s / ratio, capped at 2 s;
//   ratio below 0.4: the target does not move, the holder is dragged 2 cells toward it instead.
// A 75 kg-carry human pulls a 65 kg tribal 5 cells in 1 s, a 95 kg armed raider 4 cells in 1.3 s,
// a 140 kg muffalo 2.7 cells in 1.9 s, and is dragged by a 240 kg thrumbo. The target is dragged
// over open cells; a wall or another pawn stops the drag early. Hostile pawns and animals only:
// pulling a downed ally is left to belt items, so this weapon does not overlap them. The target
// takes 4 blunt from the weight. The chain stays on after the reel, which is the state Stake
// starts from. Cooldown 20 s.
//
// Order (times with the default sliders):
//   0.00  rest: sickle in the right hand, weight hanging at the hip on a slack chain
//   0.20  spin: the weight circles overhead on a taut chain, 2 turns, a level circle so it reads
//         the same from every facing
//   0.70  throw: the weight flies to the target's chest over 0.35 s, the chain pays out behind it
//   1.05  wrap: the chain coils twice around the target from chest to waist over 0.2 s
//   1.40  reel: the target slides toward the holder (5 cells in 1 s for the default tribal); the
//         chain hums taut, the holder leans back, the feet scuff the floor and kick dust. Thrumbo
//         scenario: the holder's feet scuff instead as they are dragged 2 cells
//   2.40  result: the target stands 2 cells from the holder, still coiled; the scuff stays
//
// Drawing: the weapon is lib/chain-sickle.js (shared with Stake). The chain is a path of ground
// points with height, drawn as alternating flat and edge-on links over a dark core, with one
// shadow on the floor. The coil is a level spiral, its far half drawn under the pawn layer so it
// reads as wrapped. Nothing needs a per-facing method. Caster and target are stand-ins; the
// animals are the same two discs scaled by body size.
import { Color, Mathf } from '../js/engine.js';
import { P, Y, Floor, Lift, sprite, circle, soft, Body } from './lib/six-paths-impact.js';
import {
  frame, figure, beast, chain, coil, rule as weightRule, Targets, DragBack, chainPath, coilPath, weight, sickle, scuff, kick, rangeRing, pawnLayer,
  Enemy, Holder, Pale, HandH, ChestH, Range, CoilR, CoilTurns, Lead, Tail, easeOut,
} from './lib/chain-sickle.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
const SpinR = .75, SpinH = .85;       // the overhead circle
const Settle = .15;                   // pause between the wrap and the reel
const Lean = .10;                     // how far the holder leans back while reeling, cells
const rule = p => weightRule(p.carry, p.target);
function times(p) {
  const spin0 = Lead, throw0 = spin0 + p.spin, hit = throw0 + p.flight, wrapped = hit + p.wrap;
  const reel0 = wrapped + Settle, reelEnd = reel0 + rule(p).reel;
  return { spin0, throw0, hit, wrapped, reel0, reelEnd, end: reelEnd + p.hold + Tail };
}

export default {
  kit: 'Chain Sickle', label: 'Snag (sketch)',
  params: {
    actors: { label: 'Show caster and target', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Target distance (cells)', 7, 3, 7, .5, 'Showcase'),
    target: { label: 'Target', value: 'tribal 65 kg', options: Object.keys(Targets), group: 'Showcase' },
    spin: P('Spin up', .5, .2, 1.2, .05, 'Timing (s)'),
    flight: P('Weight flight', .35, .15, .8, .05, 'Timing (s)'),
    wrap: P('Wrap', .2, .1, .5, .05, 'Timing (s)'),
    hold: P('Show the result', 1.0, .3, 3, .1, 'Timing (s)'),
    carry: P('Holder carrying capacity (kg)', 75, 40, 160, 5, 'Rule'),
    turns: P('Spin turns', 2, 1, 4, .5, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Rest', t: 0 }, { name: 'Spin', t: t.spin0 }, { name: 'Throw', t: t.throw0 }, { name: 'Wrap', t: t.hit },
    { name: 'Reel', t: t.reel0 }, { name: 'Result', t: t.reelEnd },
  ]; },
  events(p) { const t = times(p); return [
    { t: t.throw0, type: 'sound', def: 'RimArt_ChainThrow' }, { t: t.hit, type: 'sound', def: 'RimArt_ChainWrap' },
    { t: t.hit, type: 'shake', value: .02 }, { t: t.reel0, type: 'shake', value: .015 },
  ]; },

  draw(s, p, { origin: centre, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const f = frame(p.aim, sun), { ground } = f;
    const caster0 = ground(centre, -p.distance / 2, 0), start = ground(centre, p.distance / 2, 0);

    // Where the target and the holder are. The target slides `pull` cells toward the holder
    // during the reel, or, when the holder lost the weight contest, the holder slides `DragBack`
    // cells toward the target. A wall is not modelled here, so the full distance is covered.
    const R = rule(p);
    const pull = Math.min(R.pull, p.distance - 1.5), back = R.dragged ? Math.min(DragBack, p.distance - 1.5) : 0;
    const reelU = s < t.reel0 ? 0 : easeOut((s - t.reel0) / R.reel);
    const o = ground(start, -pull * reelU, 0);
    const reeling = s >= t.reel0 && s < t.reelEnd;
    const lean = reeling ? Lean * Math.sin(Math.min(1, (s - t.reel0) / R.reel) * Math.PI) ** .5 : 0;
    const caster = ground(caster0, back * reelU - (R.dragged ? 0 : lean), 0);
    const hand = ground(caster, .20, .18);            // right hand, ground point
    const hip = { ...ground(caster, -.08, .30), h: .32 };

    // Floor first: the scuff behind the dragged pawn, and the range ring at the throw.
    if (s >= t.reel0 && pull > 0) scuff('snag', ground(start, 0, 0), ground(start, -pull, 0), reelU);
    if (s >= t.reel0 && back > 0) scuff('snag holder', caster0, ground(caster0, back, 0), reelU, .22);
    rangeRing(caster0, Range, s - t.throw0);
    if (s >= t.hit) circle(o, .45, .35 * (1 - clamp((s - t.hit) / 1.2)), Floor + .01, Pale);

    // Coil geometry around the target: a level spiral from the chest down to the waist, scaled
    // with the body for the animals.
    const big = Math.sqrt(R.size), coilC = o, cr = CoilR * big, ch0 = (ChestH + .1) * big, ch1 = .25 * big;
    const wrapU = s < t.hit ? 0 : smooth((s - t.hit) / p.wrap);
    const arriveDeg = p.aim + 180;                    // the chain comes from the holder's side
    const coilTurns = CoilTurns * wrapU;
    const coilPts = wrapU > 0 ? coilPath(coilC, coilTurns, cr, ch0, ch1, arriveDeg, Math.max(4, Math.round(36 * wrapU))) : null;

    // The weight's position through the phases.
    let w, path, chainSag = .15;
    if (s < t.spin0) {
      w = hip; path = chainPath({ ...hand, h: HandH }, w, .12);
    } else if (s < t.throw0) {
      const u = (s - t.spin0) / p.spin, ang = f.a + (u * u * .5 + u * .5 - 1) * p.turns * TAU;   // ends pointing along the aim
      w = { x: caster.x + Math.cos(ang) * SpinR, z: caster.z + Math.sin(ang) * SpinR, h: SpinH * smooth(u * 3) + .32 * (1 - smooth(u * 3)) };
      path = chainPath({ ...hand, h: HandH }, w, .03);
      // Motion blur: a faint arc behind the weight.
      const blur = smooth(u * 3) * .7;
      for (let i = 1; i <= 9; i++) {
        const a2 = ang - i * .11, q = { x: caster.x + Math.cos(a2) * SpinR, z: caster.z + Math.sin(a2) * SpinR + w.h * Lift };
        sprite(q, .26, .2, Body.withAlpha(blur * (1 - i / 10)), soft, Y + .025);
      }
    } else if (s < t.hit) {
      const u = (s - t.throw0) / p.flight;
      const from = { x: caster.x + f.ca * SpinR, z: caster.z + f.sa * SpinR, h: SpinH }, to = { x: o.x, z: o.z, h: ChestH * big };
      w = { x: lerp(from.x, to.x, u), z: lerp(from.z, to.z, u), h: lerp(from.h, to.h, u) + .15 * Math.sin(u * Math.PI) };
      path = chainPath({ ...hand, h: HandH }, w, .08 * Math.sin(u * Math.PI) + .02);
    } else {
      // Wrapped: the chain runs hand -> first coil point, the weight rides the coil's end.
      w = coilPts[coilPts.length - 1];
      chainSag = reeling ? .01 : s < t.reel0 ? lerp(.02, .06, smooth((s - t.wrapped) / Settle)) : .05;
      path = chainPath({ ...hand, h: HandH }, coilPts[0], chainSag, 24, reeling ? .012 : 0, s);
    }

    // Draw north-first is not needed here: the chain sits on MoteOverhead, the coil's far half
    // under the pawn, so the pawn stands between the two halves.
    if (p.actors) {
      figure(caster, Holder, sun, strength);
      if (R.size > 1) beast(o, R.size, sun, strength); else figure(o, Enemy, sun, strength);
      if (reeling && pull > 0) kick(o, s - t.reel0, 1 - reelU * .6, 7);
      if (reeling && back > 0) kick(caster, s - t.reel0, 1 - reelU * .6, 9);
    }
    sickle('snag sickle', hand, HandH, p.aim, sun, strength);
    chain('snag chain', path, sun, strength);
    if (coilPts) coil('snag coil', coilPts, coilC.z + .02, sun, strength);
    weight(w, sun, strength);

    // Wrap hit: a short pale flash at the chest.
    const hitAge = s - t.hit;
    if (hitAge >= 0 && hitAge < .15) sprite({ x: o.x, z: o.z + ChestH * big * Lift }, .6, .5, Pale.withAlpha((1 - hitAge / .15) * .7), soft, Y + .06);
  },
};
