// Bubble Pipe: Eye Pop — weapon ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed 2026-09-22; the numbers are placeholders and will be XML fields). The
// pair to Drifting Burst on the same bubble pipe. Target a pawn within 8 cells with line of
// sight, 0.25 s to raise the pipe, one bubble is blown and sent at the pawn's face at 5 cells a
// second; it always arrives (the hit is decided at cast, the flight is only the picture). It
// bursts on the face: Soap in the Eyes for 5 s, shooting accuracy at 30 %, melee hit chance
// halved, moves 30 % slower. No damage. Costs 1 blow, cooldown 8 s. With an empty jar the
// button is greyed "no soap". Plain soap, no chakra: the pipe's harassment tool, so the Utakata
// hero kit keeps prison, rescue, riding, clones and acid.
//
// Order (times with the default sliders):
//   0.00  pipe at rest; the jar shows its soap
//   0.20  raise
//   0.45  blow: one bubble forms on the tip; the jar's level drops one blow
//   0.65  launch: the bubble flies at the target's face, wobbling, a little arc
//   1.85  hit: the film tears on the face and specks fly; a soapy film stays over the eyes,
//         the pawn shakes its head and rubs; tiny bubbles pop off the face now and then
//   2.30  the shooter fires back three times over 1.5 s; every shot goes wide and lands
//         1 to 2 cells from the caster (the accuracy rule, made visible)
//   6.85  the film fades: the debuff ends, the pawn stands clear-eyed
//   7.20  lower: the pipe goes back to rest
//
// Drawing: pipe, jar, bubble, pop are lib/bubble-pipe.js; the shooter's rifle is lib/vacuum.js's
// stand-in. Caster and target are stand-ins. Tracers are thin lines; each lands as a hole with scuffed
// dirt and a few clods thrown up. Nothing is stored between frames.
import { Color, Mathf } from '../js/engine.js';
import { P, Y, Floor, Body, sprite, circle, band, soft } from './lib/six-paths-impact.js';
import { draw } from './lib/six-paths-solid.js';
import { drawPipe, bubble, pop, arc, frame, figure, disc, pawnLayer, Film, Iris1, Iris2, Foam, Lead, Raise, Lower, Tail } from './lib/bubble-pipe.js';
import { rifle } from './lib/vacuum.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
const Cost = 1, Form = .2, FaceH = .62, Shots = 3, ShotGap = .5, Rub = 1.2;
const Tracer = new Color(1, .85, .55), Mark = new Color(.30, .22, .14);

function times(p) {
  const raise0 = Lead, blow = Lead + Raise, launch = blow + Form, hit = launch + p.distance / p.speed;
  const fire0 = hit + .45, clear = hit + p.debuff, lower0 = clear + .35;
  return { raise0, blow, launch, hit, fire0, clear, lower0, end: lower0 + Lower + Tail };
}

export default {
  kit: 'Bubble Pipe', label: 'Eye Pop (sketch)',
  params: {
    actors: { label: 'Show caster and target', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Target distance (cells)', 6, 2, 8, .5, 'Showcase'),
    blows: P('Soap in the jar (blows)', 10, 1, 10, 1, 'Showcase'),
    speed: P('Bubble speed (cells/s)', 5, 2, 10, .5, 'Shape'),
    radius: P('Bubble radius (cells)', .24, .15, .4, .01, 'Shape'),
    debuff: P('Soap in the eyes', 5, 1, 8, .5, 'Timing (s)'),
    miss: P('Shots land this far off (cells)', 1.6, .5, 3, .1, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Rest', t: 0 }, { name: 'Raise', t: t.raise0 }, { name: 'Blow', t: t.blow }, { name: 'Fly', t: t.launch }, { name: 'Hit', t: t.hit }, { name: 'Fires', t: t.fire0 }, { name: 'Clear', t: t.clear }, { name: 'Lower', t: t.lower0 },
  ]; },
  events(p) { const t = times(p); return [
    { t: t.blow, type: 'sound', def: 'RimArt_BubbleBlow' }, { t: t.hit, type: 'sound', def: 'RimArt_BubblePop' },
    ...Array.from({ length: Shots }, (_, k) => ({ t: t.fire0 + k * ShotGap, type: 'sound', def: 'Shot_AssaultRifle' })),
  ]; },

  draw(s, p, { origin: centre, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const f = frame(p.aim, sun), { place, ca, sa } = f;
    const o = place(centre, p.distance / 2, 0), caster = place(centre, -p.distance / 2, 0);

    const raise = s < t.raise0 ? 0 : s < t.lower0 ? smooth((s - t.raise0) / Raise) : 1 - smooth((s - t.lower0) / Lower);
    const blows = p.blows - Cost * clamp((s - t.blow) / Form);
    const formAge = s - t.blow, forming = formAge >= 0 && formAge < Form ? p.radius * smooth(formAge / Form) : 0;
    if (p.actors) figure(caster, new Color(.93, .50, .13), sun, strength);
    const pipe = drawPipe(f, caster, s, { raise, blows, forming, actors: p.actors, sun, strength });

    // The target: a shooter with a rifle. After the hit it shakes its head, rubs its eyes, and
    // carries a soapy film over them until the debuff ends.
    const hitAge = s - t.hit, soaped = hitAge >= 0 && s < t.clear;
    const shake = hitAge >= 0 && hitAge < Rub ? Math.sin(hitAge * 22) * .035 * (1 - hitAge / Rub) : 0;
    if (p.actors) {
      figure(o, new Color(.40, .45, .30), sun, strength);
      // the head is redrawn shifted so the shake shows; the figure's own head sits under it
      const head = { x: o.x + shake, z: o.z + .58 };
      if (shake) draw(disc, head.x, pawnLayer + .003, head.z, .16, .17, 0, new Color(.83, .70, .54));
      rifle('eye pop rifle', place(o, .18, -.22), .45, .9, p.aim + (hitAge >= 0 ? Math.sin(hitAge * 3) * 25 : 0), Y + .012, sun, strength);
      if (soaped) {
        const fade = 1 - smooth((s - t.clear + .4) / .4);
        // film over the eyes: a translucent disc on the head with an iridescent rim, shimmering
        draw(disc, head.x, pawnLayer + .004, head.z + .02, .15, .12, 0, Film.withAlpha(.35 * fade));
        arc('eye pop film rim', { x: head.x, z: head.z + .02 }, .15, .12, 0, 360, .02, Color.Lerp(Iris1, Iris2, .5 + .5 * Math.sin(s * 5)).withAlpha(.6 * fade), pawnLayer + .005);
        // the rubbing hand over the face for the first Rub seconds
        if (hitAge < Rub) draw(disc, head.x + .08 * Math.sin(hitAge * 11), pawnLayer + .006, head.z - .02, .06, .06, 0, new Color(.83, .70, .54));
        // tiny bubbles come off the face and pop
        for (let k = 0; k < 4; k++) {
          const born = t.hit + .3 + k * (p.debuff - .8) / 4, age = s - born, life = .7;
          if (age < 0 || age > life + .15) continue;
          const g = { x: o.x + (k % 2 ? .12 : -.10), z: o.z + .02 * k }, h = .75 + age * .35, r = .045 + k * .008;
          if (age < life) bubble(g, h, r, s, { id: 40 + k, wobble: .1, phase: k, alpha: fade, sun, strength, layer: Y + .06 });
          else pop(`eye pop tiny ${k}`, g, h, r, age - life, { sun, strength, seed: 60 + k, layer: Y + .06 });
        }
      }
    }

    // The bubble: flies from the tip to the face along a slight arc, wobbling, then pops there.
    const u = clamp((s - t.launch) / (t.hit - t.launch));
    if (s >= t.launch && s < t.hit) {
      const g = { x: lerp(pipe.tipG.x, o.x, u), z: lerp(pipe.tipG.z, o.z, u) };
      const h = lerp(pipe.tipH, FaceH, u) + Math.sin(u * Math.PI) * .25;
      bubble({ x: g.x, z: g.z + .01 * Math.sin(s * 9) }, h, p.radius, s, { id: 1, wobble: .12, phase: 2, sun, strength });
    }
    if (hitAge >= 0) pop('eye pop burst', { x: o.x + shake, z: o.z }, FaceH, p.radius, hitAge, { sun, strength, seed: 7 });

    // The shooter's answer: three tracers from the rifle that land wide of the caster, each
    // kicking up dirt where it hits. The miss points sit at least a cell from the caster, spread
    // around it: short south, long north-east, short north.
    const misses = [[.3, -1], [.9, .6], [-.2, 1]];
    if (p.actors) for (let k = 0; k < Shots; k++) {
      const age = s - (t.fire0 + k * ShotGap);
      if (age < 0) continue;
      const land = place(caster, misses[k][0] * p.miss, misses[k][1] * p.miss);
      const from = place(o, .55, -.22, .45), flight = .12;
      if (age < flight) {
        const w = clamp(age / flight), head = { x: lerp(from.x, land.x, w), z: lerp(from.z, land.z, w) };
        const tail = { x: lerp(from.x, land.x, Math.max(0, w - .35)), z: lerp(from.z, land.z, Math.max(0, w - .35)) };
        band(`eye pop tracer ${k}`, [tail, head], [{ x: tail.x, z: tail.z + .03 }, { x: head.x, z: head.z + .03 }], Tracer.withAlpha(.9), Y + .07);
      }
      const hitAgeK = age - flight;
      if (hitAgeK >= 0) {
        draw(disc, land.x, Floor + .04, land.z, .07, .05, 0, Body.withAlpha(.8));          // the hole
        sprite(land, .36, .24, Mark.withAlpha(.6), soft, Floor + .035);                     // scuffed dirt around it
        if (hitAgeK < .3) circle(land, .1 + hitAgeK * 1.2, (1 - hitAgeK / .3) * .7, Floor + .05, Tracer);
        for (let i = 0; i < 6; i++) {                                                       // dirt thrown up
          const u = hitAgeK / .3;
          if (u > 1) continue;
          const a = i / 6 * TAU + k, far = u * .3, h = Math.sin(u * Math.PI) * .25;
          draw(disc, land.x + Math.cos(a) * far, Y + .07, land.z + Math.sin(a) * far * .6 + h * .6, .03, .025, 0, Mark.withAlpha(1 - u));
        }
      }
    }
  },
};
