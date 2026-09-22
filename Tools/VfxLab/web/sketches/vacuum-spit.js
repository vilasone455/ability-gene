// Vacuum: Spit — weapon ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed 2026-09-22; the numbers are placeholders and will be XML fields). The
// pair to Suck. Target a cell up to 12 cells away with line of sight, 0.3 s wind-up. The last thing
// the vacuum swallowed is fired at the cell: blunt damage from its mass, about 1.5 per kg (a 20 kg
// chunk hits for 30, a 4 kg rifle for 6). A weapon or corpse lands on the cell as itself; this is
// the only way to get one thing back. With nothing swallowed the button is greyed "empty". Cooldown
// 8 s, shared with Suck, so the pair is one rhythm: take, throw, take.
//
// Order (times with the default sliders):
//   0.00  wand at rest, no canister
//   0.25  wind-up: the canister rises, sized by the kg inside, the wand lifts and points, the hose
//         unwinds, the mouth opens wide
//   0.55  heave: the thing runs up the hose from the canister to the head as a bulge; the canister
//         shrinks by the thing's mass when it leaves and the eyes blink
//   0.90  launch: the thing leaves the head with a puff and a small shake; it flies to the cell
//   1.35  impact: chunk scenario, it hits the pawn standing on the cell with a dust burst, the
//         pawn is dazed and the chunk lies on the ground beside it. Rifle scenario, it lands on
//         the cell and stays there as an item
//   2.55  sink: the canister goes back into the floor and the hose coils up again
//
// Drawing: the weapon itself is lib/vacuum.js (shared with Suck). Caster, pawn, chunk and rifle
// are stand-ins. Dust uses the Six Paths Puff texture as a stand-in.
import { Color, Mathf } from '../js/engine.js';
import { P, Body, Y, Floor, Lift, sprite, circle, glow, soft, rand } from './lib/six-paths-impact.js';
import { drawVacuum, frame, figure, chunk, rifle, bump, swellFor, Mass, puff, pawnLayer, Pale, Dust, HandH, Bulge, Lead, Rise, Sink, Tail } from './lib/vacuum.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
const Blink = .16, Dazed = 1.2, Arc = .9;   // flight arc height in cells

function times(p) {
  const rise0 = Lead, heave = Lead + p.windup, launch = heave + Bulge, impact = launch + p.flight, sink0 = impact + p.hold;
  return { rise0, heave, launch, impact, sink0, end: sink0 + Sink + Tail };
}

export default {
  kit: 'Vacuum', label: 'Spit (sketch)',
  params: {
    actors: { label: 'Show caster and pawn', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Target distance (cells)', 5, 3, 12, .5, 'Showcase'),
    scenario: { label: 'Last thing swallowed', value: 'chunk, pawn on the cell', options: ['chunk, pawn on the cell', 'rifle, empty cell'], group: 'Showcase' },
    windup: P('Wind-up', .3, .1, .8, .05, 'Timing (s)'),
    flight: P('Flight', .45, .2, 1, .05, 'Timing (s)'),
    hold: P('Show the result', 1.2, .3, 3, .1, 'Timing (s)'),
    inside: P('Inside before the cast (kg)', 40, 5, 100, 5, 'Rule'),
    slack: P('Hose slack (cells)', .6, 0, 1.5, .05, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Wand', t: 0 }, { name: 'Rise', t: t.rise0 }, { name: 'Heave', t: t.heave }, { name: 'Launch', t: t.launch }, { name: 'Impact', t: t.impact }, { name: 'Sink', t: t.sink0 },
  ]; },
  events(p) { const t = times(p); return [
    { t: t.launch, type: 'shake', value: .05 }, { t: t.launch, type: 'sound', def: 'RimArt_VacuumSpit' },
    ...(p.scenario.startsWith('chunk') ? [{ t: t.impact, type: 'shake', value: .10 }] : []),
  ]; },

  draw(s, p, { origin: centre, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const f = frame(p.aim, sun), { place } = f;
    const o = place(centre, p.distance / 2, 0), caster = place(centre, -p.distance / 2, 0);
    const isChunk = p.scenario.startsWith('chunk');

    const present = s < t.rise0 ? 0 : s < t.sink0 ? smooth((s - t.rise0) / Rise) : 1 - smooth((s - t.sink0) / Sink);
    const churn = Math.max(bump((s - t.rise0) / Rise), bump((s - t.sink0) / Sink));
    const heaveU = (s - t.heave) / Bulge;                 // 0..1 while the thing runs up the hose
    // The canister's size is its fullness in kg; the thing's own mass leaves with it at the heave.
    const kg = p.inside - (s < t.heave ? 0 : Math.min(p.inside, Mass[isChunk ? 'chunk' : 'rifle']));
    const pulse = bump((s - t.heave) / .25) * .6;           // a squeeze as it pushes the thing out
    const blink = bump((s - t.heave) / Blink);
    const open = smooth((s - t.rise0) / p.windup);
    const mouthOpen = .18 + .82 * open * (1 - smooth((s - t.launch - .2) / .4)) * (1 + .3 * pulse);
    const bulges = heaveU > 0 && heaveU < 1 ? [1 - heaveU] : [];

    if (p.actors) figure(caster, new Color(.93, .50, .13), sun, strength);
    const v = drawVacuum(f, caster, o, { present, churn, swell: swellFor(kg), pulse, blink, mouthOpen, bulges, slack: p.slack, actors: p.actors, sun, strength });
    const tipG = v.tipG;

    // The pawn on the cell (chunk scenario): hit at impact, dazed after, standing throughout.
    if (isChunk && p.actors) {
      const hitAge = s - t.impact, knock = hitAge >= 0 ? .18 * (1 - smooth(hitAge / .3)) : 0;
      figure(place(o, knock, 0), new Color(.55, .38, .27), sun, strength);
      if (hitAge >= 0) {
        const fade = 1 - smooth((hitAge - Dazed) / .3);
        for (let i = 0; i < 3; i++) {
          const turn = s * 5 + i * 2.094;
          sprite({ x: o.x + Math.cos(turn) * .2, z: o.z + .86 + Math.sin(turn) * .06 }, .08, .08, Pale.withAlpha(.9 * fade), soft, Y + .03);
        }
      }
    }

    // The thing: in flight from the head to the cell, then resting on the ground.
    const rest = isChunk ? place(o, .35, -.55) : o;         // a chunk that hit a pawn drops beside it
    const drawThing = (g, h, scale, deg, layer) => isChunk ? chunk(g, h, scale, layer, sun, strength) : rifle('vacuum spat rifle', g, h, scale, deg, layer, sun, strength);
    if (s >= t.launch && s < t.impact) {
      const u = (s - t.launch) / p.flight;
      const g = { x: lerp(tipG.x, rest.x, u), z: lerp(tipG.z, rest.z, u) }, h = lerp(HandH, 0, u) + Math.sin(u * Math.PI) * Arc;
      drawThing(g, h, .35 + .65 * smooth(u * 3), 25 + u * 900, Y - .03);
    } else if (s >= t.impact) {
      drawThing(rest, 0, 1, 130, pawnLayer - .004);
    }

    // Launch: a puff at the head, a short pale flash, a few dust motes thrown forward.
    const launchAge = s - t.launch;
    if (launchAge >= 0 && launchAge < .4) {
      const tipS = v.tipS, ang = Math.atan2(o.z - tipS.z, o.x - tipS.x);
      sprite(tipS, .9, .6, Pale.withAlpha(Math.max(0, 1 - launchAge / .1) * .7), glow, Y + .022);
      for (let i = 0; i < 7; i++) {
        const u = launchAge / (.25 + rand(i + 500) * .15);
        if (u > 1) continue;
        const spread = (rand(i + 510) - .5) * .9, far = u * (.5 + rand(i + 520) * .8);
        sprite({ x: tipS.x + Math.cos(ang) * far - Math.sin(ang) * spread * u, z: tipS.z + Math.sin(ang) * far + Math.cos(ang) * spread * u },
          .2 + u * .35, .18 + u * .3, Dust.withAlpha(Math.sin(u * Math.PI) * .55), puff, Y + .021);
      }
    }

    // Impact: flash, floor ring and dust on the cell. Bigger for the chunk, a small thud for the rifle.
    const hitAge = s - t.impact, big = isChunk ? 1 : .45;
    if (hitAge >= 0 && hitAge < .5) {
      sprite({ x: o.x, z: o.z + .3 * big }, 1.6 * big, 1.1 * big, Pale.withAlpha(Math.max(0, 1 - hitAge / .12) * .85 * big), glow, Y + .02);
      circle(o, .25 + hitAge * 2.2 * big, (1 - hitAge / .5) * .6, Floor, Pale);
      for (let i = 0; i < 10; i++) {
        const u = hitAge / (.3 + rand(i + 60) * .2);
        if (u > 1) continue;
        const th = rand(i + 70) * TAU, far = u * (.4 + rand(i + 80) * 1.0) * big;
        sprite({ x: o.x + Math.cos(th) * far, z: o.z + Math.sin(th) * far * .7 + Math.sin(u * Math.PI) * .35 * big * Lift }, .25 + u * .4, .2 + u * .3,
          Dust.withAlpha(Math.sin(u * Math.PI) * .6), puff, Y + .012);
      }
    }
  },
};
