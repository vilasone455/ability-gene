// Water Gun: Stream Shot — weapon ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed 2026-09-22; the numbers are placeholders and will be XML fields). The
// pawn carries a water gun fed by a 30-unit water bag worn on the back. Stream Shot is the normal
// fire mode: target a pawn or cell up to 20 cells away with line of sight, 0.25 s to raise the gun,
// one jet of water. It costs 1 unit. On a pawn it does 6 blunt and applies Soaked for 60 s (moves
// 10 % slower, cold hits harder, cannot burn; other weapons in the set react to it). Any fire on
// the target pawn or cell is put out. With an empty bag the button is greyed "no water". The bag
// refills 10 units a second standing next to a water cell, and 1 unit per 5 s outdoors in rain.
// The fill level is drawn on the bag itself so the player reads the ammo on the pawn.
//
// Order (times with the default sliders):
//   0.00  gun at rest, hanging across the body; the bag shows the water it holds
//   0.20  raise: the gun comes up to hand height and points along the aim
//   0.45  fire: the jet leaves the muzzle, the bag's surface drops one unit and sloshes
//   0.70  hit: splash on the target; burning scenario, the flames go out in a puff of steam
//   0.70+ soaked: the pawn is tinted, drips fall from it, a puddle stays on its cell
//   2.20  lower: the gun goes back to rest
//
// Drawing: the weapon itself is lib/water-gun.js (shared with Hydro Pump). Caster and target are
// stand-ins. The jet is an alpha-blended blue body with a rounded front, broken reflections
// and detached droplets; its tangent-based width holds in all facings. Steam uses the Six Paths Puff texture.
import { Color, Mathf } from '../js/engine.js';
import { P, Y, Floor, sprite, circle, glow, soft } from './lib/six-paths-impact.js';
import { drawWaterGun, frame, figure, flames, steam, puddle, drips, splash, stream, bump, WaterLit, Water, HandH, Lead, Raise, Lower, Tail } from './lib/water-gun.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const Jet = .12;            // the jet lasts this long at the muzzle
const Cost = 1;

function times(p) {
  const raise0 = Lead, fire = Lead + Raise, hit = fire + p.flight, jetEnd = hit + Jet, lower0 = hit + p.hold;
  return { raise0, fire, hit, jetEnd, lower0, end: lower0 + Lower + Tail };
}

export default {
  kit: 'Water Gun', label: 'Stream Shot (sketch)',
  params: {
    actors: { label: 'Show caster and target', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Target distance (cells)', 6, 3, 14, .5, 'Showcase'),
    scenario: { label: 'Target', value: 'pawn', options: ['pawn', 'burning pawn'], group: 'Showcase' },
    units: P('Water in the bag (units)', 18, 1, 30, 1, 'Showcase'),
    flight: P('Jet flight', .25, .1, .6, .05, 'Timing (s)'),
    hold: P('Show the result', 1.5, .3, 3, .1, 'Timing (s)'),
    width: P('Jet width (cells)', .07, .03, .15, .01, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Rest', t: 0 }, { name: 'Raise', t: t.raise0 }, { name: 'Fire', t: t.fire }, { name: 'Hit', t: t.hit }, { name: 'Lower', t: t.lower0 },
  ]; },
  events(p) { const t = times(p); return [
    { t: t.fire, type: 'sound', def: 'RimArt_WaterGunShot' }, { t: t.hit, type: 'shake', value: .02 },
  ]; },

  draw(s, p, { origin: centre, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const f = frame(p.aim, sun), { place } = f;
    const o = place(centre, p.distance / 2, 0), caster = place(centre, -p.distance / 2, 0);
    const burning = p.scenario === 'burning pawn';

    const raise = s < t.raise0 ? 0 : s < t.lower0 ? smooth((s - t.raise0) / Raise) : 1 - smooth((s - t.lower0) / Lower);
    const fired = s >= t.fire;
    const units = fired ? p.units - Cost * smooth((s - t.fire) / .3) : p.units;
    const slosh = bump((s - t.fire) / .6) + .3 * bump((s - t.raise0) / Raise);
    const recoil = .04 * bump((s - t.fire) / .2);

    if (p.actors) figure(caster, new Color(.93, .50, .13), sun, strength);
    const g = drawWaterGun(f, caster, s, { raise, units, slosh, squeeze: 0, recoil, actors: p.actors, sun, strength });

    // The target: burning until the hit, then tinted and dripping, with a puddle that stays.
    const hitAge = s - t.hit;
    if (p.actors) {
      const soaked = hitAge >= 0 ? smooth(hitAge / .2) : 0;
      const knock = hitAge >= 0 ? .1 * (1 - smooth(hitAge / .25)) : 0;
      puddle({ x: o.x, z: o.z - .2 }, hitAge, 1.1, .5);   // at the feet, south of the body disc
      figure(place(o, knock, 0), new Color(.55, .38, .27), sun, strength, { tint: soaked });
      if (burning) {
        flames(o, s, hitAge < 0 ? 1 : Math.max(0, 1 - hitAge / .12));
        steam(o, hitAge, 1.0, 3);
      }
      drips(o, hitAge, 1 - smooth((hitAge - p.hold + .4) / .4), 5);
    } else {
      puddle(o, hitAge, .9, .5);
    }

    // The jet: its head flies from the muzzle to the target over `flight`, its tail leaves the
    // muzzle Jet seconds later, so a short mass of water crosses the gap and ends in the splash.
    const target = { x: o.x, z: o.z + .38 };        // the pawn's chest
    const u0 = clamp((s - t.fire - Jet) / p.flight), u1 = clamp((s - t.fire) / p.flight);
    if (fired && u0 < 1) {
      stream('water gun jet', g.muzzleS, target, u0, u1, p.width, s);
      const mz = 1 - clamp((s - t.fire) / .12);
      sprite(g.muzzleS, .26, .22, Water.withAlpha(mz * .6), soft, Y + .03);   // wet muzzle spray
    }
    splash(o, hitAge, .8, 1);
    splash({ x: o.x, z: o.z + .38 }, hitAge - .03, .5, 2);
  },
};
