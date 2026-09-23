// Infinity Castle: Sunlight and the biwa — the castle gene's always-on parts, proposal for the
// Infinity Castle kit, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is (agreed in outline 2026-09-23; every number is a placeholder and will be an XML field).
// The castle gene is archite (2 archites, complexity 8, metabolism 0; the drawbacks are its price).
//   Sunlight: outdoors in daylight the carrier burns, about 4 burn damage a second, checked once a
//   second, so she is downed in about 20 s. Roofed cells and night are safe. (Chosen over a hard
//   indoor lock, which would need pathing patches and stop her crossing between buildings at night.)
//   Bound biwa: the gene puts a biwa in her hands and she cannot equip any other weapon. It is
//   destroyed when she drops it (downed) and given back when she is up again. Not craftable or
//   tradeable. Weak blunt melee, about 9.
//
// Order, part "sunlight" (times with the default sliders):
//   0.30  she walks out of a roofed room (dark inside) through its doorway into the sun
//   1.70  in the sun: a thin smoke starts; 2.70 first burn check: an orange flash on the body, a
//         burst of embers, ash falls at her feet; from then on thick dark smoke, embers rising, the
//         kimono and skin char darker with each second; one flash every second she stays out
//   4.70  she turns and walks back in; under the roof the burning stops, the smoke thins out over
//         1.5 s; the ash on the ground stays
// Part "biwa comes back":
//   0.00  she lies downed with no biwa (it was destroyed when she went down)
//   0.60  she gets up; 0.90 the biwa appears in her hands with a warm flash and one soft note
//
// Drawing: smoke puffs, embers and ash are sprites, born from time so the clip scrubs; the room is
// a stand-in (in game the roof decides, and RimWorld's own lighting darkens it). Stand-in pawn,
// level shapes, no per-facing method.
import { Color } from '../js/engine.js';
import { P } from './lib/six-paths-impact.js';
import {
  nakime, box, sprite, soft, glow, puff, rand, Strum, Lantern, WallWood, WallTop, WoodFloor, VoidDeep, L, Floor, Lift,
  smooth, clamp, bump, easeOut, thinRing,
} from './lib/infinity-castle.js';

// Decided values and the rule's numbers.
const Walk = 2.5, Tick = 1, Smoke = new Color(.13, .12, .12), Ember = new Color(1, .52, .16), Ash = new Color(.30, .29, .28);
const Inside = { x: .5, z: 5.5 }, Outside = { x: .5, z: 0 }, DoorZ = 2;
const Room = { x0: -3, z0: 3, x1: 4, z1: 8 };            // floor cells; walls around, doorway at x 0 on the south wall

function times(p) {
  if (p.part !== 'sunlight') return { up: .6, biwa: .9, end: 1.3 + p.hold };
  const out0 = .3, walk = Math.hypot(Outside.x - Inside.x, Outside.z - Inside.z) / Walk;
  const sun = out0 + (Inside.z - DoorZ) / Walk, back0 = sun + p.sunTime, shade = back0 + (DoorZ - Outside.z) / Walk;
  return { out0, walk, sun, back0, shade, end: back0 + walk + 1.5 + p.hold };
}

export default {
  kit: 'Infinity Castle', label: 'Sunlight and the biwa (sketch)',
  params: {
    part: { label: 'Part', value: 'sunlight', options: ['sunlight', 'biwa comes back'], group: 'Showcase' },
    sunTime: P('Time in the sun', 3, 1, 8, .5, 'Timing (s)'),
    hold: P('Show the result', 1, .3, 3, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    if (p.part !== 'sunlight') return [{ name: 'Downed', t: 0 }, { name: 'Gets up', t: t.up }, { name: 'Biwa back', t: t.biwa }];
    return [{ name: 'Walks out', t: t.out0 }, { name: 'In the sun', t: t.sun }, { name: 'First burn', t: t.sun + Tick }, { name: 'Back in', t: t.back0 }, { name: 'Under the roof', t: t.shade }];
  },
  events(p) {
    const t = times(p);
    if (p.part !== 'sunlight') return [{ t: t.biwa, type: 'sound', def: 'RimArt_BiwaNote' }];
    const out = [];
    for (let k = 1; t.sun + k * Tick <= t.shade; k++) out.push({ t: t.sun + k * Tick, type: 'sound', def: 'RimArt_SunBurn' });
    return out;
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const at = (x, z) => ({ x: o.x + x, z: o.z + z });

    if (p.part !== 'sunlight') {
      const pos = at(0, 0), up = s >= t.up, bi = clamp((s - t.biwa) / .35);
      nakime('sun carrier', pos, sun, strength, { seated: false, downed: !up, biwa: easeOut(bi) });
      const fa = s - t.biwa;
      if (fa >= 0 && fa < .5) {
        sprite({ x: pos.x + .05, z: pos.z + .45 }, 1.2 * (1 - fa), 1.2 * (1 - fa), Lantern.withAlpha(.5 * (1 - fa / .5)), glow, L.fx);
        thinRing('sun note', { x: pos.x + .05, z: pos.z + .35 }, .3 + fa * 2.4, .05, Strum.withAlpha(.6 * (1 - fa / .5)), L.fx + .01);
      }
      return;
    }

    // The roofed room: wooden floor, walls, a doorway on the south side; dark inside.
    box(o.x + Room.x0, o.z + Room.z0, o.x + Room.x1, o.z + Room.z1, WoodFloor, Floor - .05);
    const wall = (x0, z0, x1, z1) => { box(o.x + x0, o.z + z0, o.x + x1, o.z + z1, WallWood, L.wall); box(o.x + x0 + .1, o.z + z0 + .1, o.x + x1 - .1, o.z + z1 - .1, WallTop, L.wall + .002); };
    wall(Room.x0 - 1, Room.z1, Room.x1 + 1, Room.z1 + 1); wall(Room.x0 - 1, Room.z0 - 1, 0, Room.z0); wall(1, Room.z0 - 1, Room.x1 + 1, Room.z0);
    wall(Room.x0 - 1, Room.z0, Room.x0, Room.z1); wall(Room.x1, Room.z0, Room.x1 + 1, Room.z1);

    // Where she is: out through the doorway, a while in the sun, back in.
    let pos;
    if (s < t.out0) pos = at(Inside.x, Inside.z);
    else if (s < t.out0 + t.walk) { const u = (s - t.out0) / t.walk; pos = at(Inside.x, Inside.z + (Outside.z - Inside.z) * u); }
    else if (s < t.back0) pos = at(Outside.x, Outside.z);
    else { const u = clamp((s - t.back0) / t.walk); pos = at(Outside.x, Outside.z + (Inside.z - Outside.z) * u); }

    // Heat: smoulders on entering the sun, full from the first check, gone 1.5 s after the roof.
    const heatAt = x => x < t.sun ? 0 : x < t.shade ? Math.min(1, .25 + .75 * smooth((x - t.sun) / Tick)) : 1 - smooth((x - t.shade) / 1.5);
    const heat = heatAt(s);
    const ticks = [];
    for (let k = 1; t.sun + k * Tick <= t.shade; k++) ticks.push(t.sun + k * Tick);
    const burnt = ticks.filter(x => x <= s).length;

    // Ash that fell at each check stays on the ground.
    ticks.forEach((tk, k) => {
      if (s < tk) return;
      const foot = Outside;
      for (let i = 0; i < 4; i++) {
        const q = at(foot.x + (rand(k * 9 + i) - .5) * .9, foot.z + (rand(k * 9 + i + 50) - .5) * .5 - .1);
        sprite(q, .12 + rand(i + k) * .1, .08 + rand(i + k + 3) * .06, Ash.withAlpha(.75 * smooth((s - tk) / .4)), soft, Floor + .02);
      }
    });

    nakime('sun carrier', pos, sun, strength, { seated: false, char: Math.min(.7, burnt * .2) });
    // Burning patches on the body: small orange spots that flicker while she is hot.
    if (heat > .3) for (let i = 0; i < 6; i++) {
      const q = { x: pos.x + (rand(i + 140) - .5) * .36, z: pos.z + .12 + rand(i + 150) * .5 };
      sprite(q, .09, .09, Ember.withAlpha((heat - .3) * (.5 + .5 * Math.sin(s * 17 + i * 2.1))), glow, L.pawn + .05);
    }

    // Smoke: puffs born every 0.07 s while hot, rising 1.6 cells and drifting with the wind.
    for (let i = 0; i < 24; i++) {
      const period = 1.4, u = ((s / period) + i / 24) % 1, born = s - u * period, h = heatAt(born);
      if (h <= 0.01) continue;
      const from = born < t.back0 ? at(Outside.x, Outside.z) : pos;
      const x = from.x + (rand(i + 20) - .5) * .35 + u * .6 + Math.sin(u * 5 + i) * .1, z = from.z + .35 + u * 2.1 * Lift;
      sprite({ x, z }, .4 + u * .95, .36 + u * .8, Smoke.withAlpha(bump(Math.min(1, u * 1.4 + .05)) * .8 * h), puff, L.fx + .01 + i * .0005);
    }
    // Embers: small orange sparks flickering up.
    for (let i = 0; i < 12; i++) {
      const period = .8 + rand(i + 70) * .4, u = ((s / period) + rand(i + 80)) % 1, born = s - u * period, h = heatAt(born);
      if (h <= .2) continue;
      const x = pos.x + (rand(i + 90) - .5) * .45 + Math.sin(u * 9 + i) * .06, z = pos.z + .25 + u * 1.2 * Lift;
      sprite({ x, z }, .09, .09, Ember.withAlpha((1 - u) * h * (.6 + .4 * Math.sin(s * 30 + i))), glow, L.fx + .02);
    }
    // Each burn check: an orange flash on the body and a burst of embers.
    for (const tk of ticks) {
      const a = s - tk;
      if (a < 0 || a > .4) continue;
      sprite({ x: pos.x, z: pos.z + .35 }, .9, 1.1, Ember.withAlpha(.55 * (1 - a / .4)), glow, L.fx + .03);
      for (let i = 0; i < 8; i++) {
        const ang = rand(i + tk * 7) * Math.PI * 2, r = .2 + a * 2.2, hh = a * 1.2 - a * a * 2;
        sprite({ x: pos.x + Math.cos(ang) * r, z: pos.z + .35 + Math.sin(ang) * r * .6 + hh * Lift }, .08, .08, Ember.withAlpha(1 - a / .4), glow, L.fx + .031);
      }
    }

    // The roof: shade over the room and anyone in it (RimWorld's lighting does this in game).
    box(o.x + Room.x0 - 1, o.z + Room.z0 - 1, o.x + Room.x1 + 1, o.z + Room.z1 + 1, VoidDeep.withAlpha(.42), L.pawn + .1);
  },
};
