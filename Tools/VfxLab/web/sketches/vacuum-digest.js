// Vacuum: Digest — weapon ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed 2026-09-22; the numbers are placeholders and will be XML fields). The
// vacuum's fullness is the mass it has swallowed since it last digested (filth 0 kg, a rifle about
// 4, a chunk about 20, a human corpse about 60). The carrier gets a hediff that slows it by 0.4 %
// per kg, so -40 % at the 100 kg capacity, and Suck is refused with "full" when the next thing
// would not fit. Spit removes only the last thing's mass. Digest is a third, maintenance button
// like the retrieval hook's reload: no target, no cooldown. The pawn stands still and the canister
// chews for 0.05 s per kg (5 s when full); everything inside is destroyed, the last thing
// included, and fullness returns to 0. The decision: keep the last thing to Spit it back and carry
// its weight, or digest and move at full speed.
//
// Order (times with the default sliders, 60 kg inside):
//   0.00  wand at rest, no canister
//   0.25  rise: the canister comes up fat with what it holds, the wand lifts, the hose unwinds
//   0.55  chew: the mouth chomps about four times a second, the body squeezes on each bite, the
//         lumps inside shift around on the top, crumbs fly out of the mouth and land on the floor.
//         The canister slims down as it goes
//   3.55  burp: a green puff from the mouth, a squash and a little shake, eyes shut
//   4.15  sink: the canister, back at its base size, goes into the floor; the crumbs stay
//
// Drawing: the weapon itself is lib/vacuum.js (shared with Suck and Spit). Caster is a stand-in.
// Dust uses the Six Paths Puff texture as a stand-in.
import { Color, Mathf } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Body, Y, Floor, Lift, sprite, soft, glow, rand } from './lib/six-paths-impact.js';
import { drawVacuum, frame, figure, bump, swellFor, disc, puff, CanDark, Pale, Dust, Lead, Rise, Sink, Tail } from './lib/vacuum.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
const ChompHz = 4, Burp = .35, Crumbs = 14;
const Burped = new Color(.55, .72, .40), Crumb = new Color(.16, .13, .10);

function times(p) {
  const rise0 = Lead, chew0 = Lead + Rise + .05, chew = p.kg * p.perKg, done = chew0 + chew, sink0 = done + Burp + p.hold;
  return { rise0, chew0, chew, done, sink0, end: sink0 + Sink + Tail };
}

export default {
  kit: 'Vacuum', label: 'Digest (sketch)',
  params: {
    actors: { label: 'Show caster', value: true, group: 'Showcase' },
    aim: P('Facing (degrees)', 0, 0, 360, 5, 'Showcase'),
    kg: P('Inside (kg)', 60, 5, 100, 5, 'Rule'),
    perKg: P('Chew per kg', .05, .02, .1, .005, 'Timing (s)'),
    hold: P('Show the result', .8, .3, 3, .1, 'Timing (s)'),
    slack: P('Hose slack (cells)', .6, 0, 1.5, .05, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Wand', t: 0 }, { name: 'Rise', t: t.rise0 }, { name: 'Chew', t: t.chew0 }, { name: 'Burp', t: t.done }, { name: 'Sink', t: t.sink0 },
  ]; },
  events(p) { const t = times(p); return [
    { t: t.chew0, type: 'sound', def: 'RimArt_VacuumChew' }, { t: t.done, type: 'sound', def: 'RimArt_VacuumBurp' }, { t: t.done, type: 'shake', value: .04 },
  ]; },

  draw(s, p, { origin: centre, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const f = frame(p.aim, sun), { place } = f;
    const caster = centre;

    const present = s < t.rise0 ? 0 : s < t.sink0 ? smooth((s - t.rise0) / Rise) : 1 - smooth((s - t.sink0) / Sink);
    const churn = Math.max(bump((s - t.rise0) / Rise), bump((s - t.sink0) / Sink));
    const digested = clamp((s - t.chew0) / t.chew);                       // share of the mass gone
    const swell = swellFor(p.kg * (1 - digested));
    const chewing = s >= t.chew0 && s < t.done;
    const chomp = chewing ? (Math.sin((s - t.chew0) * ChompHz * TAU) + 1) / 2 : 0;   // 1 = mouth wide, 0 = shut
    const bite = chewing ? Math.pow(1 - chomp, 3) : 0;                    // sharp at the shut moment
    const burpAge = s - t.done;
    const pulse = bite * .35 - bump(burpAge / Burp) * .5;                 // squeeze on a bite, squash on the burp
    const blink = chewing ? (Math.floor((s - t.chew0) * ChompHz) % 4 === 3 ? bite : 0) : bump(burpAge / (Burp * 1.6));
    const opening = smooth((s - t.rise0) / p.windup);
    const mouthOpen = chewing ? .15 + .85 * chomp : burpAge >= 0 && burpAge < Burp ? .9 : .18 + .5 * smooth((s - t.rise0 - Rise) / .2) * (s < t.chew0 ? 1 : 0);

    if (p.actors) figure(caster, new Color(.93, .50, .13), sun, strength);
    const v = drawVacuum(f, caster, caster, { present, churn, swell, pulse, blink, mouthOpen, bulges: [], slack: p.slack, actors: p.actors, sun, strength });
    const { C, R, H, top, layer } = v.can;

    // Lumps shifting inside: darker soft spots wandering over the top face while it chews, fading
    // out as the mass goes.
    if (present > .9 && s < t.done) {
      const alive = (1 - digested) * (chewing ? 1 : .6);
      for (let i = 0; i < 5; i++) {
        const th = rand(i + 700) * TAU + (chewing ? (s - t.chew0) * (.8 + rand(i + 710)) * (i % 2 ? 1 : -1) : 0);
        const d = R * (.25 + rand(i + 720) * .45) * (1 - digested * .5), size = R * (.35 + rand(i + 730) * .25);
        sprite({ x: C.x + Math.cos(th) * d, z: top + Math.sin(th) * d }, size, size * .8, CanDark.withAlpha(.55 * alive), soft, layer + .0055);
      }
    }

    // Crumbs: on each bite a few flecks leave the mouth and land on the floor south of the canister.
    // They stay where they land. Replayed from zero so the timeline can scrub.
    const mouth = { x: C.x, z: C.z - R + H * Lift * .30 };   // the face's mouth, on the south side
    const bites = chewing || s >= t.done ? Math.floor(Math.min(s, t.done) - t.chew0) * ChompHz + Math.floor(((Math.min(s, t.done) - t.chew0) % 1) * ChompHz) : 0;
    for (let i = 0; i < Crumbs; i++) {
      const born = t.chew0 + (i / Crumbs) * t.chew + rand(i + 800) * (1 / ChompHz);
      if (s < born) continue;
      const age = s - born, life = .35 + rand(i + 810) * .15, u = Math.min(1, age / life);
      const dx = (rand(i + 820) - .5) * 1.0, dz = -(.25 + rand(i + 830) * .5);
      const g = { x: mouth.x + dx * u, z: mouth.z + dz * u }, h = Math.sin(u * Math.PI) * .3 * (1 - u * .3);
      const size = .03 + rand(i + 840) * .025;
      draw(disc, g.x, u < 1 ? Y - .03 : Floor + .03, g.z + h * Lift, size, size * .8, 0, Crumb.withAlpha(.9));
    }

    // The burp: a green puff rolling out of the mouth, and a short pale flash inside it.
    if (burpAge >= 0 && burpAge < Burp * 2) {
      const u = burpAge / (Burp * 2);
      for (let i = 0; i < 6; i++) {
        const d = u * (.4 + rand(i + 900) * .6), spread = (rand(i + 910) - .5) * .8 * u;
        sprite({ x: mouth.x + spread, z: mouth.z - d + u * .25 * Lift }, .3 + u * .6, .25 + u * .5, Burped.withAlpha((1 - u) * .55), puff, Y + .03);
      }
      sprite(mouth, .6, .3, Pale.withAlpha(Math.max(0, 1 - burpAge / .1) * .5), glow, layer + .011);
    }
  },
};
