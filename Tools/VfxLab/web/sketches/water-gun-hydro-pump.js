// Water Gun: Hydro Pump — weapon ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed 2026-09-22; the numbers are placeholders and will be XML fields). The
// pair to Stream Shot on the same water gun. Target a direction; 0.35 s wind-up while the pawn
// pumps the bag up to pressure, then a 0.5 s blast in a cone 5 cells long and 3 cells wide at its
// end. Every pawn in the cone is pushed 3 cells along the aim, knocked down for 2 s, takes 8
// blunt and is Soaked for 60 s. Fire in the cone is put out and filth is washed off. It costs 10
// units of the 30-unit bag, so three pumps empty a full bag. Cooldown 12 s. Below 10 units the
// button is greyed "not enough water". Against Repulse Launch this is a wide sweep that soaks, not
// a single throw.
//
// Order (times with the default sliders):
//   0.00  gun at rest; the bag shows the water it holds
//   0.20  raise: the gun comes up and points along the aim
//   0.45  pump: the bag is squeezed in, the gun is pulled back, the wedge on the floor shows the
//         true cone
//   0.80  blast: a sheet of water crosses the cone in 0.2 s with three wavy cores, mist and droplets; the bag
//         drains 10 units; the pawns in the cone slide 3 cells and fall; the one outside it stands
//   1.30  spray ends; puddles cover the cone and the downed pawns drip
//   3.35  the pawns get up
//   3.85  lower: the gun goes back to rest
//
// Drawing: the weapon itself is lib/water-gun.js (shared with Stream Shot). Caster and pawns are
// stand-ins. Mist and steam use the Six Paths Puff texture as a stand-in.
import { Color, Mathf } from '../js/engine.js';
import { P, Y, Floor, sprite, band, circle, glow, soft, rand } from './lib/six-paths-impact.js';
import { drawWaterGun, MuzzleAlong, frame, figure, flames, steam, puddle, drips, splash, stream, bump, puff, Water, WaterLit, WaterDark, Foam, HandH, Lead, Raise, Lower, Tail } from './lib/water-gun.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
const Cost = 10, Push = 3, Front = .2;     // units, cells, seconds for the blast front to reach the cone's end
const Jets = 3, Slide = .4, Rise = .3;

const pawns = [
  { along: 2.0, across: .45, colour: new Color(.55, .38, .27), inCone: true },
  { along: 3.4, across: -.85, colour: new Color(.40, .45, .30), inCone: true },
  { along: 4.4, across: .35, colour: new Color(.35, .30, .50), inCone: true, burning: true },
  { along: 3.0, across: 1.9, colour: new Color(.50, .50, .55), inCone: false },
];

function times(p) {
  const raise0 = Lead, pump0 = Lead + Raise, blast = pump0 + p.windup, sprayEnd = blast + p.spray, up = blast + Front + Slide + p.down, lower0 = up + Rise + .5;
  return { raise0, pump0, blast, sprayEnd, up, lower0, end: lower0 + Lower + Tail };
}

export default {
  kit: 'Water Gun', label: 'Hydro Pump (sketch)',
  params: {
    actors: { label: 'Show caster and pawns', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    units: P('Water in the bag (units)', 30, 10, 30, 1, 'Showcase'),
    length: P('Cone length (cells)', 5, 3, 8, .5, 'Shape'),
    width: P('Cone end width (cells)', 3, 1.5, 5, .5, 'Shape'),
    windup: P('Pump wind-up', .35, .1, .8, .05, 'Timing (s)'),
    spray: P('Spray', .5, .2, 1.2, .05, 'Timing (s)'),
    down: P('Pawns down', 2.0, .5, 4, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Rest', t: 0 }, { name: 'Raise', t: t.raise0 }, { name: 'Pump', t: t.pump0 }, { name: 'Blast', t: t.blast }, { name: 'Spray ends', t: t.sprayEnd }, { name: 'Up', t: t.up }, { name: 'Lower', t: t.lower0 },
  ]; },
  events(p) { const t = times(p); return [
    { t: t.blast, type: 'shake', value: .08 }, { t: t.blast, type: 'sound', def: 'RimArt_WaterGunPump' },
  ]; },

  draw(s, p, { origin: centre, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const f = frame(p.aim, sun), { place } = f;
    const caster = place(centre, -p.length / 2, 0);
    const Apex = MuzzleAlong;                 // the cone starts at the muzzle, not the feet
    const halfW = along => p.width / 2 * clamp((along - Apex) / (p.length - Apex));

    const raise = s < t.raise0 ? 0 : s < t.lower0 ? smooth((s - t.raise0) / Raise) : 1 - smooth((s - t.lower0) / Lower);
    const pumping = s >= t.pump0 && s < t.blast ? smooth((s - t.pump0) / p.windup) : 0;
    const squeeze = s < t.blast ? pumping : Math.max(0, 1 - (s - t.blast) / .15);
    const drained = clamp((s - t.blast) / p.spray);
    const units = p.units - Cost * drained;
    const slosh = bump((s - t.blast) / (p.spray + .4)) + .4 * pumping;
    const recoil = -.06 * pumping + .07 * bump((s - t.blast) / .25) + .03 * (s >= t.blast && s < t.sprayEnd ? Math.sin(s * 60) : 0);

    // The cone on the floor: the true hit area, shown from the wind-up until the spray ends.
    const coneA = s < t.pump0 ? 0 : s < t.sprayEnd + .3 ? smooth((s - t.pump0) / .15) * (1 - smooth((s - t.sprayEnd) / .3)) : 0;
    if (coneA > 0) {
      const A = [], B = [];
      for (let i = 0; i <= 12; i++) {
        const along = lerp(Apex, p.length, i / 12), w = halfW(along);
        A.push(place(caster, along, -w)); B.push(place(caster, along, w));
      }
      band('water gun cone fill', A, B, Water.withAlpha(.14 * coneA), Floor + .06);
      const edge = (side, key) => {
        const pts = []; for (let i = 0; i <= 12; i++) { const along = lerp(Apex, p.length, i / 12); pts.push(place(caster, along, side * halfW(along))); }
        const a = pts.map(q => ({ x: q.x, z: q.z })), b = pts.map(q => ({ x: q.x, z: q.z + .03 }));
        band(key, a, b, WaterLit.withAlpha(.7 * coneA), Floor + .07);
      };
      edge(-1, 'water gun cone edge a'); edge(1, 'water gun cone edge b');
      const arcA = [], arcB = [];
      for (let i = 0; i <= 10; i++) { const w = lerp(-halfW(p.length), halfW(p.length), i / 10); arcA.push(place(caster, p.length, w)); arcB.push(place(caster, p.length + .03, w)); }
      band('water gun cone end', arcA, arcB, WaterLit.withAlpha(.7 * coneA), Floor + .07);
    }

    if (p.actors) figure(caster, new Color(.93, .50, .13), sun, strength);
    const g = drawWaterGun(f, caster, s, { raise, units, slosh, squeeze, recoil, actors: p.actors, sun, strength });

    // Puddles across the cone once the front has passed, staying to the end.
    for (let i = 0; i < 7; i++) {
      const along = .8 + i / 6 * (p.length - 1.2), across = (rand(i + 300) - .5) * 2 * halfW(along) * .8;
      puddle(place(caster, along, across), s - t.blast - Front * along / p.length, .8 + rand(i + 310) * .5, .45);
    }

    // The pawns: three in the cone slide Push cells and fall as the front reaches them, drip while
    // down, and get up together; the one outside is untouched.
    if (p.actors) pawns.forEach((q, i) => {
      const reach = t.blast + Front * q.along / p.length;
      const hitAge = s - reach;
      const affected = q.inCone && hitAge >= 0;
      const slide = affected ? Push * smooth(hitAge / Slide) : 0;
      const downed = !affected ? 0 : s < t.up ? smooth(hitAge / Slide) : 1 - smooth((s - t.up) / Rise);
      const soaked = affected ? smooth(hitAge / .2) : 0;
      const pos = place(caster, q.along + slide, q.across);
      if (affected) puddle(place(caster, q.along + Push, q.across, -.3), hitAge - Slide, 1.1, .5);
      figure(pos, q.colour, sun, strength, { downed, tint: soaked });
      if (q.burning) { flames(pos, s, affected ? Math.max(0, 1 - hitAge / .12) : 1); steam(pos, hitAge, 1.0, i * 7); }
      if (affected) { splash({ x: pos.x, z: pos.z + .3 }, hitAge, .6, i * 11); drips(pos, hitAge, 1 - smooth((s - t.up) / .5), i * 13); }
    });

    // The blast: five jets fan out from the muzzle to the cone's end, heads racing out over Front,
    // tails leaving when the spray ends; mist fills the cone while they run.
    const sprayAge = s - t.blast;
    if (sprayAge >= 0 && sprayAge < p.spray + Front + .1) {
      const u1 = clamp(sprayAge / Front), u0 = clamp((sprayAge - p.spray) / Front);
      // The sheet: soft translucent discs filling the cone between the tail and head fronts, wider
      // and fainter further out, so the ground shows through and the edge fades.
      for (let row = 0; row < 14; row++) {
        const u = row / 13, along = lerp(Apex, p.length, u);
        if (u > u1 || u < u0) continue;
        const hw = halfW(along), count = 1 + Math.round(hw / .28);
        for (let k = 0; k < count; k++) {
          const across = count === 1 ? 0 : lerp(-hw, hw, k / (count - 1)) * .85;
          const pos = place(caster, along, across, .12 + .25 * u), size = .35 + .55 * u + .1 * Math.sin(s * 40 + row * 1.3 + k);
          sprite(pos, size * 1.9, size * 1.6, WaterLit.withAlpha(.10), soft, Y + .012);
          sprite(pos, size * 1.2, size * 1.0, Water.withAlpha(.28 * (1 - u * .4)), soft, Y + .014);
          sprite(pos, size * .8, size * .7, WaterLit.withAlpha(.30 * (1 - u * .5)), glow, Y + .016);
        }
      }
      for (let j = 0; j < Jets; j++) {
        const across = lerp(-1, 1, j / (Jets - 1)) * halfW(p.length) * .7;
        const end = place(caster, p.length, across, .15);
        stream(`water gun pump jet ${j}`, g.muzzleS, end, u0, u1, .07, s + j * .13, Y + .02 + j * .001, .12, j + 1);
      }
      const spraying = sprayAge < p.spray ? 1 : Math.max(0, 1 - (sprayAge - p.spray) / .25);
      sprite(g.muzzleS, .9 * spraying + .2, .7 * spraying + .2, WaterLit.withAlpha(.7 * spraying), glow, Y + .03);
      for (let i = 0; i < 60; i++) {
        const cycle = .35 + rand(i + 400) * .3, u = ((sprayAge + rand(i + 410) * cycle) % cycle) / cycle;
        const along = u * p.length, across = (rand(i + 420) - .5) * 2 * halfW(along);
        if (along / p.length > u1 || along / p.length < u0) continue;
        const h = .3 + Math.sin(u * Math.PI) * .5 * rand(i + 430);
        if (i % 2) sprite(place(caster, along, across, h), .18 + u * .35, .15 + u * .3, (i % 4 ? Foam : WaterLit).withAlpha(.35 * spraying * (1 - u * .5)), puff, Y + .028);
        else sprite(place(caster, along, across, h), .07, .09, Foam.withAlpha(.9 * spraying), soft, Y + .03);
      }
    }
  },
};
