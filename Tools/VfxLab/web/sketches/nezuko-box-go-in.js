// Nezuko's box: Go in — equipment proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed 2026-09-26; the numbers are placeholders and will be XML fields). An
// ability of the box (see nezuko-box-worn.js for what inside does). Touch range: a pawn next to the
// wearer goes into the box. Who: colonists, slaves, colony animals up to body size 1.0, and any
// downed pawn (a downed enemy is carried off to be captured later); not a pawn in a mental break
// unless it is downed. A pawn that came out cannot go back in for 2 h.
//
// Order (times with the default sliders):
//   0.00  the wearer stands with the empty box; the other pawn stands (or lies downed) a cell away
//   0.20  approach 0.4 s: it walks to the door (a downed pawn is drawn along the ground to it)
//   0.60  the door swings open over 0.25 s on its hinge; pink light inside
//   0.85  in 0.3 s: the pawn moves into the doorway and is gone under a pink puff and wood dust.
//         In game the pawn is taken off the map at 1.00, under the puff (it is not drawn shrinking)
//   1.15  the door swings shut over 0.2 s; at 1.35 the latch catches, the box jolts
//   1.35  asleep: the box breathes, the seam glows, z marks rise. A downed pawn's blood stays on
//         the floor where it lay
//
// Drawing: the box is lib/nezuko-box.js. The wearer and the other pawn are stand-ins.
import { Color } from '../js/engine.js';
import { drawBox, figure, woodDust, sprite, glow, puff, smooth, clamp, lerp, bump, Wearer, Ally, Pink, Blood, Back, HalfDepth, Floor, Y } from './lib/nezuko-box.js';
import { P } from './lib/six-paths-impact.js';

const Approach = .4, Open = .25, Enter = .3, Shut = .2, Settle = .45;

function times(p) {
  const approach0 = .2, open0 = approach0 + Approach, enter0 = open0 + Open, shut0 = enter0 + Enter, latch = shut0 + Shut;
  return { approach0, open0, enter0, gone: enter0 + Enter * .5, shut0, latch, asleep: latch + Settle, end: latch + p.hold };
}

export default {
  kit: "Nezuko's Box", label: 'Go in (sketch)',
  params: {
    actors: { label: 'Show the wearer and the pawn', value: true, group: 'Showcase' },
    facing: P('Wearer faces (degrees)', 270, 0, 270, 90, 'Showcase'),
    scenario: { label: 'Who goes in', value: 'awake ally', options: ['awake ally', 'downed ally'], group: 'Showcase' },
    hold: P('Show the result', 2, .5, 4, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Approach', t: t.approach0 }, { name: 'Open', t: t.open0 }, { name: 'Enter', t: t.enter0 }, { name: 'Shut', t: t.shut0 }, { name: 'Asleep', t: t.latch },
  ]; },
  events(p) { const t = times(p); return [
    { t: t.open0, type: 'sound', def: 'RimArt_BoxDoorOpen' }, { t: t.gone, type: 'sound', def: 'RimArt_BoxIn' }, { t: t.latch, type: 'sound', def: 'RimArt_BoxLatch' },
  ]; },

  draw(s, p, { origin, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const feet = { x: origin.x, z: origin.z };
    const downed = p.scenario === 'downed ally';

    const door = s < t.open0 ? 0 : s < t.shut0 ? smooth((s - t.open0) / Open) : 1 - smooth((s - t.shut0) / Shut);
    const inner = door * (s < t.shut0 ? 1 : .5);
    const jolt = .03 * bump((s - t.latch) / .18) * Math.sin((s - t.latch) * 60);
    const sleeping = smooth((s - t.latch) / Settle);

    if (p.actors) figure(feet, Wearer, sun, strength);
    const box = drawBox('go in', feet, p.facing, { door, sleeping, shake: jolt, inner, t: s, sun, strength });

    // The pawn: from a cell away to just outside the door, then into the doorway.
    const a = p.facing * Math.PI / 180, f = { x: Math.cos(a), z: Math.sin(a) }, n = { x: -f.z, z: f.x };
    const start = { x: feet.x + (Back - .95) * f.x + .55 * n.x, z: feet.z + (Back - .95) * f.z + .55 * n.z };
    const front = { x: feet.x + (Back - HalfDepth - .32) * f.x, z: feet.z + (Back - HalfDepth - .32) * f.z };
    const mouth = box.doorMouth.ground;
    if (downed) sprite({ x: start.x - .05, z: start.z + .02 }, .7, .45, Blood.withAlpha(.8 * smooth(s / .1)), puff, Floor + .02);
    if (s < t.gone && p.actors) {
      const walk = smooth((s - t.approach0) / Approach), inU = smooth((s - t.enter0) / (Enter * .5));
      const g = { x: lerp(lerp(start.x, front.x, walk), mouth.x, inU * .8), z: lerp(lerp(start.z, front.z, walk), mouth.z, inU * .8) };
      // North of the box the pawn is further from the camera and draws under it; south of it, over.
      const north = g.z > box.frame.C.z;
      figure(g, Ally, sun, strength, { downed, scale: 1 - .35 * inU, alpha: 1 - .6 * inU, layer: north ? box.layer - .02 : Math.max(box.layer, Y) + .06 });
    }
    // The puff that covers the pawn leaving the map, and the wood dust off the doorway.
    const puffAge = s - t.enter0;
    if (puffAge >= 0 && puffAge < .6) {
      const u = puffAge / .6, m = box.doorMouth.screen;
      sprite(m, .9 * (.4 + u), .8 * (.4 + u), Pink.withAlpha(.6 * Math.sin(u * Math.PI)), glow, Y + .07);
      for (let i = 0; i < 5; i++) {
        const th = i * 1.26 + .4, far = u * .35;
        sprite({ x: m.x + Math.cos(th) * far, z: m.z + Math.sin(th) * far * .7 }, .35 * (.5 + u), .3 * (.5 + u), Pink.withAlpha(.35 * (1 - u)), puff, Y + .071 + i * .0005);
      }
    }
    woodDust('go in dust a', box.doorMouth.screen, s - t.enter0, .7, .5, 10);
    woodDust('go in dust b', box.doorMouth.screen, s - t.latch, .5, .4, 30);
  },
};
