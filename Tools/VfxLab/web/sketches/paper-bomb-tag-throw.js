// Tag Throw — Paper Bomb weapon proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed, none of it agreed; every number is a placeholder). The pawn holds a
// tag scroll. Tag Throw targets a cell, a pawn or a wall within 12.9 tiles with line of sight. The
// pawn tears one tag off the roll and throws it on a steel pin. It sticks where it hits, burns
// for 2 s on its own (no hand seal), then bursts: 35 Bomb damage, radius 1.5, allies included.
// Stuck in a pawn it travels with that pawn, so the burst happens where the carrier has walked to.
// Stuck in a wall, that wall takes 4x damage (140). Costs 1 tag. Cooldown 4 s. Role: the kit's
// plain ranged attack and its breaching tool. Tag Line is prepared ground, Paper Shroud pins one
// target; this is the one used every few seconds.
// Port note: if the fuse is a vanilla CompExplosive wick, nearby pawns run from it. The carried
// case only works if the fuse is the ability's own timer.
//
// Drawing: the pin and the tag are flat quads that follow the throw's tangent, so every aim is
// the same method. The tag trails behind the pin and flutters (its width closes and opens). Once
// stuck it lies back toward the thrower and its seal pulses faster until it curls and bursts. The
// floor ring is the true radius and follows a carrier. Three scenarios show the three targets.
// Pawns and walls are stand-ins; the wall is one cell tall and the tag sticks on its top edge. The tag, burst and roll are in lib/paper-bomb.js.
import { Color, Mathf } from '../js/engine.js';
import { P, Y, Floor, Lift, sprite, soft } from './lib/six-paths-impact.js';
import { pawn, ringAt, rock, shadowLayer, pawnLayer, Ally, EnemyColour, Ink } from './lib/goku.js';
import { Hot, Char, Red, Burn, TagLong, WallTop, quad, tag, roll, burst, walls } from './lib/paper-bomb.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
// Decided shape and rule numbers.
const Release = .3, HandOut = .35, HandHeight = .45, Power = 1.2;
const FlyLong = .62, CarriedLong = .5;      // tag scale in the air and on a body
const CarrierWalks = 2.6;                   // cells the carrier covers during the fuse
const Steel = new Color(.2, .2, .24);

function times(p) {
  const land = Release + p.fly, burstAt = land + p.fuse;
  return { land, burstAt, curl: burstAt - Burn, end: burstAt + p.hold };
}

export default {
  kit: 'Paper Bomb', label: 'Tag Throw (sketch)',
  params: {
    actors: { label: 'Show caster and enemies', value: true, group: 'Showcase' },
    scenario: { label: 'Target', value: 'floor', options: ['floor', 'carried', 'wall'], group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Caster distance (cells)', 6, 3, 12.9, .5, 'Showcase'),
    radius: P('Blast radius (cells)', 1.5, .5, 3, .1, 'Rule'),
    fuse: P('Fuse', 2, .5, 4, .1, 'Rule'),
    fly: P('Tag flies for', .45, .2, 1, .05, 'Timing (s)'),
    hold: P('Aftermath held', 2.2, .5, 5, .1, 'Timing (s)'),
    arc: P('Flight height (cells)', .8, .2, 2, .1, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Tear off', t: 0 }, { name: 'Flight', t: Release }, { name: 'Fuse', t: t.land }, { name: 'Burst', t: t.burstAt },
  ]; },
  events(p) { return [{ t: times(p).burstAt, type: 'shake', value: .16 }]; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a);
    const place = (along, across, h = 0) => ({ x: o.x + along * ca - across * sa, z: o.z + along * sa + across * ca + h * Lift });
    const cast = (along, across, h = 0) => ({ x: o.x + along * ca - across * sa + sun.x * h, z: o.z + along * sa + across * ca + sun.z * h });
    const carried = p.scenario === 'carried', wall = p.scenario === 'wall';
    const age = s - t.burstAt, gone = age >= 0, burning = s - t.land;

    // Where the tag is stuck. A carrier walks on toward its group during the fuse, and the burst happens there.
    const anchor = carried ? (s < t.land ? lerp(-.6, 0, s / t.land) : CarrierWalks * smooth(burning / (p.fuse - .1))) : 0;
    const stuckHeight = carried ? .5 : wall ? 1 : 0, centre = place(carried ? (gone ? CarrierWalks : anchor) : 0, 0);

    // The wall is one cell tall and on the map grid. The hit cell is gone after the burst.
    if (wall) walls('tag throw wall', o, [-3, -2, -1, 0, 1, 2, 3].map(k => place(0, k)), sun, strength, c => gone && c.x === o.x && c.z === o.z);
    if (!gone && s >= t.land) { const f = burning / p.fuse; ringAt(centre, p.radius, Red.withAlpha(.2 + .5 * f * (.6 + .4 * Math.sin(s * 9))), Floor + .02); }

    burst('tag throw burst', centre, age, p.radius, sun, strength, 77, Power);
    if (wall && gone) for (let k = 0; k < 7; k++) {                 // the wall cell breaks into rubble that stays
      // Thrown out of the wall line to both sides of it, so the neighbouring blocks' tops do not hide the pieces.
      const n = Math.sin(k * 91.7 + 3) * 43758.5, r = n - Math.floor(n), u = clamp(age / .3), h = .5 * 4 * u * (1 - u);
      const g = place((k % 2 ? 1 : -1) * (.55 + .6 * r) * u, (r - .5) * .9 * u);
      rock({ x: g.x, z: g.z + h * Lift }, .22 + .18 * r, u < 1 ? age * 400 : r * 360, 1, k * 2, u < 1 ? Y + .05 : Floor + .025);
    }

    if (p.actors) {
      const throwing = smooth(s / Release) * (1 - smooth((s - Release - .1) / .3)), caster = place(-p.distance, 0);
      pawn(caster, Ally, sun, strength, { arms: 1, raise: throwing });
      roll(place(-p.distance + HandOut - .05, -.12, HandHeight - .1), p.aim, .34);
      const hit = (pos, hurt) => hurt && gone
        ? pawn({ x: pos.x + (pos.x - centre.x) * .3 * smooth(age / .3), z: pos.z + (pos.z - centre.z) * .3 * smooth(age / .3) }, EnemyColour, sun, strength, { lie: age > .1, tint: age < .25 ? Hot : Char, tintAmount: age < .25 ? 1 - age / .25 : .2 })
        : pawn(pos, EnemyColour, sun, strength);
      if (carried) { hit(place(anchor, 0), true); hit(place(CarrierWalks + .4, .75), true); hit(place(CarrierWalks + .3, -.8), true); hit(place(CarrierWalks + 2.4, .2), false); }
      else if (!wall) { hit(place(.9, .3), true); hit(place(-.3, -2.2), false); }
    }

    if (gone || s < Release) return;
    const hitAlong = wall ? -.35 : 0;       // a wall is hit on the near edge of its top
    // Flight: the pin leads and the tag trails behind it, fluttering.
    if (s < t.land) {
      const path = v => place(lerp(-p.distance + HandOut, hitAlong, v), 0, lerp(HandHeight, stuckHeight, v) + p.arc * Math.sin(Math.PI * v));
      const u = (s - Release) / p.fly, at = path(u), next = path(Math.min(1, u + .03)), dx = next.x - at.x, dz = next.z - at.z, len = Math.hypot(dx, dz) || 1, deg = Math.atan2(dz, dx) * 57.29578;
      const h = lerp(HandHeight, stuckHeight, u) + p.arc * Math.sin(Math.PI * u), back = .13 + TagLong * FlyLong / 2;
      sprite(cast(lerp(-p.distance + HandOut, hitAlong, u), 0, h), .4, .16, Ink.withAlpha(strength * .8), soft, shadowLayer);
      tag({ x: at.x - dx / len * back, z: at.z - dz / len * back + Math.sin(s * 40) * .02 }, deg + Math.sin(s * 31) * 9, { long: FlyLong, wide: .7 * (.45 + .55 * Math.abs(Math.cos(s * 23))), layer: Y + .05 });
      quad(at, .28, .04, deg, Steel, Y + .06);
      return;
    }
    // Stuck and burning. The seal pulses faster as the fuse runs down, heats over the last 0.5 s, then curls.
    const f = burning / p.fuse, pulse = .5 + .5 * Math.sin(burning * (5 + 16 * f)), heat = clamp((burning - (p.fuse - .5 - Burn)) / .5), curl = clamp((s - t.curl) / Burn);
    const stick = place(wall ? hitAlong : anchor, 0, stuckHeight), slap = 1 + .25 * (1 - clamp(burning / .08));
    const layer = carried ? pawnLayer + .012 : wall ? WallTop : Floor + .014;
    if (burning < .12) ringAt(stick, .1 + burning * 2, Hot.withAlpha(.7 * (1 - burning / .12)), layer + .02);
    if (carried) { tag(stick, -35, { long: CarriedLong * slap, wide: .6 * slap, heat, curl, armed: pulse, layer }); quad(stick, .14, .035, p.aim, Steel, layer + .006); return; }
    const long = .8 * slap, back = .1 + TagLong * long / 2 * (1 - .6 * curl), sway = Math.sin(burning * 7) * 5 * (1 - curl);
    const r = (p.aim + 180 + sway) * Mathf.Deg2Rad;
    tag({ x: stick.x + Math.cos(r) * back, z: stick.z + Math.sin(r) * back }, p.aim + sway, { long, wide: .85 + .15 * Math.sin(burning * 9), heat, curl, armed: pulse, layer });
    quad(stick, .2, .04, p.aim, Steel, layer + .006);
  },
};
