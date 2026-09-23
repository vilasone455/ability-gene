// Samehada: Fusion — weapon proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed 2026-09-23, not agreed; the numbers are placeholders and will be XML
// fields). Spends all 5 of the blade's charges (see Feed): the holder and the sword merge into a
// shark form for 15 s. While fused: +30 % move, regen 2 HP per second, immune to Drained, and the
// pawn can path through shallow and deep water. No melee weapon is held during it (the blade is
// the body), so the fused pawn fights with its own claws at the blade's base damage. When it ends
// the blade comes back into the hand at zero charges, bandaged. Cooldown 2400 ticks.
//
// Order (times with the default sliders; the fused walk is 3.5 s here to fit the clip, 15 s in game):
//   0.00  rest: the sword held 30 degrees low, full and five rows bare, tally at 5; a normal
//         pawn stands beside the holder, a 2-cell deep-water patch 2.5 cells ahead of both
//   0.20  merge 0.5 s: the blade is drawn back into the hand (its length shrinks to nothing), the
//         tally empties, and the hide spreads over the body; purple flash at the arm
//   0.70  fused: dark scaled body, dorsal fin, tail, gills, a green regen ring every second
//   0.70  walk 3.5 s: both pawns walk along the aim, the holder at 1.3 x the other's speed. The
//         holder crosses the water; the other stops at its near edge and stays there
//   4.20  revert 0.5 s: the hide fades, the blade grows back out of the hand at zero charges
//   4.70  result: the holder on the far side of the water with a bandaged 1.0-cell blade, the
//         other pawn still at the edge, the tally at 0
//   5.70  end
//
// Drawing: the shark form is lib/samehada.js sharkForm and has one draw method per facing (up,
// down, side), the only per-facing piece in this kit. The blade, tally and water are shared with
// the other two sketches. Stand-ins from the Chain Sickle kit; the water is a stand-in for terrain.
import { Color, Mathf } from '../js/engine.js';
import { P, Y, Floor, Lift, sprite, circle, soft, glow, Body } from './lib/six-paths-impact.js';
import {
  frame, figure, samehada, tally, sharkForm, regenPulse, waterPatch, facingOf, MaxCharges, HandH, GripLength,
  Holder, Ally, Pale, FleshLit, Chakra, Lead, Tail, screen,
} from './lib/samehada.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const Rest = -30, Speed = 1.0, Boost = 1.3;        // rest blade angle; walk speed of a normal pawn (cells/s, a display speed) and the fused multiplier
const WaterAt = 2.5, WaterLen = 2.0, WaterWide = 3.2; // the patch: distance ahead of the start, length along the aim, width across
const Beside = 1.0;                                  // the other pawn stands this far across
function times(p) {
  const merge0 = Lead, fused0 = merge0 + p.merge, revert0 = fused0 + p.walk, done = revert0 + p.revert;
  return { merge0, fused0, revert0, done, end: done + p.hold + Tail };
}

export default {
  kit: 'Samehada', label: 'Fusion (sketch)',
  params: {
    actors: { label: 'Show the other pawn', value: true, group: 'Showcase' },
    showTally: { label: 'Show charge tally', value: true, group: 'Showcase' },
    water: { label: 'Show the water', value: true, group: 'Showcase' },
    aim: P('Walk direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    merge: P('Merge', .5, .2, 1.2, .05, 'Timing (s)'),
    walk: P('Fused walk', 3.5, 1, 8, .1, 'Timing (s)'),
    revert: P('Revert', .5, .2, 1.2, .05, 'Timing (s)'),
    hold: P('Show the result', 1.0, .3, 3, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Rest', t: 0 }, { name: 'Merge', t: t.merge0 }, { name: 'Fused', t: t.fused0 }, { name: 'Revert', t: t.revert0 }, { name: 'Result', t: t.done },
  ]; },
  events(p) { const t = times(p); return [
    { t: t.merge0, type: 'sound', def: 'RimArt_SamehadaMerge' }, { t: t.fused0, type: 'shake', value: .015 },
    { t: t.revert0, type: 'sound', def: 'RimArt_SamehadaRevert' },
  ]; },

  draw(s, p, { origin: centre, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const f = frame(p.aim, sun), { ground } = f;
    const start = ground(centre, -1.5, 0);
    const sign = Math.cos(p.aim * Mathf.Deg2Rad) < -1e-6 ? -1 : 1;

    // Progress of the merge and the revert, and how much shark is showing.
    const merge = smooth(clamp((s - t.merge0) / p.merge));
    const revert = smooth(clamp((s - t.revert0) / p.revert));
    const shark = s < t.revert0 ? merge : 1 - revert;
    const charges = s < t.revert0 ? MaxCharges * (1 - merge) : 0;
    const bladeOut = s < t.revert0 ? 1 - merge : revert;   // share of the blade that is out of the hand

    // Walking: the holder from fused0 at Boost x Speed, the other pawn at Speed until the water.
    const walkAge = Math.min(p.walk, Math.max(0, s - t.fused0));
    const holder = ground(start, walkAge * Speed * Boost, 0);
    const edge = WaterAt - WaterLen / 2 - .35;          // where the other pawn stops
    const other = ground(start, Math.min(edge, walkAge * Speed), Beside);
    const otherStopped = walkAge * Speed >= edge;

    // Floor: the water, the tally, the other pawn's stop mark.
    if (p.water) waterPatch('fusion water', ground(start, WaterAt, Beside / 2), WaterLen, WaterWide, p.aim, s);
    if (p.showTally) tally('fusion', holder, charges, p.aim);
    if (p.actors && otherStopped) circle(other, .3, .3, Floor + .012, Pale);

    // The pawns. The holder's own discs stay under the overlay so the fade reads as skin to hide.
    figure(holder, Holder, sun, strength);
    if (p.actors) figure(other, Ally, sun, strength);

    // Merge flash: purple light at the arm as the blade goes in, and a few chakra motes.
    if (s >= t.merge0 && s < t.fused0 + .2) {
      const u = clamp((s - t.merge0) / (p.merge + .2));
      sprite({ x: holder.x + .2 * sign, z: holder.z + .35 }, .7, .6, FleshLit.withAlpha(.5 * Math.sin(u * Math.PI)), glow, Y + .06);
      for (let i = 0; i < 6; i++) {
        const a = i / 6 * Math.PI * 2 + s * 4, r = .35 * (1 - u);
        sprite({ x: holder.x + Math.cos(a) * r, z: holder.z + .3 + Math.sin(a) * r * .6 }, .1, .1, Chakra.withAlpha(.8 * (1 - u)), glow, Y + .061);
      }
    }
    // Revert flash: the same light as the blade comes back out.
    if (s >= t.revert0 && s < t.done + .2) {
      const u = clamp((s - t.revert0) / (p.revert + .2));
      sprite({ x: holder.x + .2 * sign, z: holder.z + .35 }, .6, .5, FleshLit.withAlpha(.4 * Math.sin(u * Math.PI)), glow, Y + .06);
    }

    // The shark form over the holder.
    sharkForm('fusion shark', holder, p.aim, shark, s);
    if (s >= t.fused0 && s < t.revert0) regenPulse(holder, s - t.fused0);

    // The blade, at rest angle, shrinking into the hand and growing back out. Drawn only while
    // any of it is out: the length is scaled by bladeOut, so at 0 it is inside the hand.
    if (bladeOut > .02) {
      const deg = p.aim + sign * Rest, hr = deg * Mathf.Deg2Rad;
      const hand = { x: holder.x + Math.cos(hr) * .22 - Math.sin(hr) * .12, z: holder.z + Math.sin(hr) * .22 + Math.cos(hr) * .12 };
      // Scale the whole sword toward the hand by drawing it at a charge whose length matches.
      const wantLen = (1.0 + .15 * charges) * bladeOut;
      const pseudo = (wantLen - 1.0) / .15;            // may be negative: the lib clamps the bandage share, the length follows
      samehada('fusion sword', hand, deg, pseudo, sun, strength, { alpha: Math.min(1, bladeOut * 3) });
    }
  },
};
