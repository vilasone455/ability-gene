// Stardust Dragon Staff: how Stardust Flame is drawn. Not a sketch itself, so it is not listed in sketches/index.js.
// The rules are in stardust-dragon-ai.js (breathe()); stardust-dragon.js calls these.
//
// The breath is puffs of starfire: each leaves the mouth where the mouth was when it was born, so a turning head
// bends the flame; it spreads over the cone, slows and grows as it cools from white through ice and cyan to deep
// blue, and a puff that reaches a wall stops there and spreads. Last Breath's flame is bigger and whiter with each
// piece it spends. In C# the puffs are motes or one Shared/VfxDraw batch a frame; the fire is Core's own Fire.
import { Color, Mathf, MaterialPool, ShaderDatabase } from '../../js/engine.js';
import { Floor, sprite, soft, glow, rand } from './six-paths-impact.js';
import { ringAt } from './goku.js';
import { rayWall } from './terraria.js';
import { dust, IceColour, CyanColour } from './stardust-dragon.js';
import { Deep } from './stardust-dragon-textures.js';

const D2R = Mathf.Deg2Rad, TAU = Math.PI * 2;
const flat = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const White = new Color(1, 1, 1), DeepColour = new Color(...Deep), Warn = new Color(.85, .18, .12);
const FireOuter = new Color(1, .45, .1), FireInner = new Color(1, .84, .38), Smoke = new Color(.3, .3, .32);
const Frame = new Color(.08, .08, .1), Face = new Color(.2, .22, .28), FillBlue = new Color(.55, .8, 1);   // Command_TapHold's fill colour

const Emit = 1 / 150, Life = .45;         // seconds between puffs; seconds a puff lives
const PulseRun = .35;                     // seconds a pulse of light takes from the back of the body to the mouth

// The breath at time s. r: the simulation; oc: the wielder's cell where the dragon is drawn (its chest lift included);
// walls: wall cell centres in the same frame.
export function flame(r, s, oc, walls, layer) {
  for (let i = Math.ceil((s - Life) / Emit); i <= Math.floor(s / Emit); i++) {
    const tb = i * Emit, kb = r.step(tb), f = r.flame[kb], u = (s - tb) / Life;
    if (f <= 0 || u < 0 || u >= 1) continue;
    const m = { x: oc.x + r.mouthX[kb], z: oc.z + r.mouthZ[kb] }, heat = r.heat[kb], reach = r.reach[kb];
    const a = r.mouthR[kb] + (rand(i * 3.1) - .5) * 1.8 * r.spread[kb], far = reach * (.82 + .25 * rand(i * 5.7));
    const wall = walls.length ? rayWall(m, a, walls, far + 1) : Infinity;
    let d = far * Math.pow(u, .8), splash = 0;
    if (d > wall) { splash = Math.min(1, (d - wall) / .6); d = wall; }
    const side = Math.sin(u * 7 + i) * .1 * u;
    const pos = { x: m.x + Math.cos(a) * d - Math.sin(a) * side, z: m.z + Math.sin(a) * d + Math.cos(a) * side };
    const size = (.28 + 1.2 * u) * Math.sqrt(reach / 5) * (1 + .5 * heat) * (1 + .8 * splash);
    const cool = u < .2 ? Color.Lerp(White, IceColour, u / .2) : u < .55 ? Color.Lerp(IceColour, CyanColour, (u - .2) / .35) : Color.Lerp(CyanColour, DeepColour, (u - .55) / .45);
    const alpha = f * .65 * Math.pow(1 - u, 1.1) * (1 - .6 * splash);
    sprite(pos, size * 1.4, size, Color.Lerp(cool, White, .35 * heat).withAlpha(alpha), glow, layer + (i % 64) * .00002, -a / D2R);
    if (u < .4) sprite(pos, size * .8, size * .45, White.withAlpha(alpha * (1 - u / .4)), glow, layer + .0015 + (i % 64) * .00001, -a / D2R);   // the white core
    if (i % 4 === 0) dust(pos, u * Life, Life, size * .5, false, layer + .002);
  }
}

// The glare at the mouth and the flame's light on the floor under the cone, at step k; below: how far the floor is
// under the dragon on screen.
export function mouthGlow(r, k, oc, below, layer) {
  const f = r.flame[k];
  if (f <= 0) return;
  const m = { x: oc.x + r.mouthX[k], z: oc.z + r.mouthZ[k] }, rot = r.mouthR[k], g = 1 + r.heat[k], reach = r.reach[k];
  sprite(m, 1.6 * g, 1.2 * g, CyanColour.withAlpha(.3 * f), glow, layer + .0028);
  sprite(m, .9 * g, .6 * g, White.withAlpha(.6 * f), glow, layer + .003, -rot / D2R);
  const mid = { x: m.x + Math.cos(rot) * reach * .5, z: m.z + Math.sin(rot) * reach * .5 - below };
  sprite(mid, reach * 1.2, 2 * reach * Math.tan(r.spread[k]) + .8, CyanColour.withAlpha(.14 * f), glow, Floor + .013, -rot / D2R);
}

// A piece the flame is burning, b 0..1: a flickering white glow over it that grows as it goes. cells: the size of one
// Terraria pixel on screen.
export function burning(c, rot, cells, b, s, layer) {
  if (b <= 0) return;
  const flick = .8 + .2 * Math.sin(s * 30 + c.x * 7);
  sprite(c, 44 * cells * (1 + .4 * b), 30 * cells * (1 + .4 * b), IceColour.withAlpha(.45 * b * flick), glow, layer, -rot / D2R);
  sprite(c, 20 * cells, 20 * cells, White.withAlpha(.6 * b * flick), glow, layer + .0001);
}

// The pulses of light that run up the body from the back to the mouth while a piece burns (bigger when one is
// spent). list: the drawn pieces, head first.
export function pulses(r, s, list, cells, layer) {
  if (list.length < 2) return;
  for (const p of r.pulses) {
    const v = (s - p.t) / PulseRun;
    if (v < 0 || v >= 1) continue;
    const f = (list.length - 1) * (1 - v), i = Math.min(list.length - 2, Math.floor(f)), u = f - i, a = list[i], b = list[i + 1];
    const c = { x: Mathf.Lerp(a.c.x, b.c.x, u), z: Mathf.Lerp(a.c.z, b.c.z, u) }, size = (p.big ? 30 : 18) * cells, al = (p.big ? .8 : .5) * Math.sin(Math.PI * (.15 + .85 * v));
    sprite(c, size * 1.6, size, IceColour.withAlpha(al), glow, layer, -Mathf.Lerp(a.rot, b.rot, u) / D2R);
    sprite(c, size * .6, size * .6, White.withAlpha(al), glow, layer + .0001);
  }
}

// Last Breath's burst, b = { t, x, z, radius } from the wielder: a white flash, a ring out to the radius on the floor,
// shards of starlight flung out to it, and its light on the floor fading over 1.2 s.
export function burst(b, s, oc, below, layer) {
  const age = b ? s - b.t : -1;
  if (age < 0 || age > 1.4) return;
  const c = { x: oc.x + b.x, z: oc.z + b.z }, floor = { x: c.x, z: c.z - below }, e = 1 - Math.pow(1 - Math.min(1, age / .35), 3);
  sprite(c, b.radius * 3.2 * e, b.radius * 3.2 * e, CyanColour.withAlpha(.5 * Math.max(0, 1 - age / .7)), glow, layer);
  sprite(c, b.radius * 2.6 * (.3 + .7 * e), b.radius * 2.6 * (.3 + .7 * e), White.withAlpha(Math.max(0, 1 - age / .3)), glow, layer + .001);
  ringAt(floor, b.radius * e, IceColour.withAlpha(.8 * Math.max(0, 1 - age / .6)), Floor + .03, true);
  sprite(floor, b.radius * 2.5, b.radius * 2, CyanColour.withAlpha(.3 * Math.max(0, 1 - age / 1.2)), glow, Floor + .014);
  for (let q = 0; q < 24; q++) {
    const a = q / 24 * TAU + rand(q) * .3, d = b.radius * (.2 + .9 * e) * (.7 + .4 * rand(q + 40));
    dust({ x: c.x + Math.cos(a) * d, z: c.z + Math.sin(a) * d }, age, 1.2, .5, false, layer + .002);
  }
}

// RimWorld's fire, a stand-in (Core's is an animated texture): flickering tongues of orange and yellow, a glow on the
// floor, a puff of smoke rising. A lit cell burns to the end of the scene; RimWorld would spread it and hurt whoever
// stands in it, which the sketch does not show. list: [{ t, x, z }] cells from the wielder o.
export function fires(list, s, o, layer) {
  list.forEach((f, n) => {
    const age = s - f.t;
    if (age < 0) return;
    const c = { x: o.x + f.x, z: o.z + f.z }, grow = Math.min(1, age / .5);
    sprite(c, 1.3 * grow, 1.1 * grow, FireOuter.withAlpha(.18), glow, Floor + .016);
    tongues(c, s, n, grow, .42, layer);
    const v = ((s + n * .37) % 1.6) / 1.6;
    sprite({ x: c.x + .1 * Math.sin(n + s), z: c.z + .3 + v * .9 }, .35 + v * .4, .3 + v * .3, Smoke.withAlpha(.3 * Math.sin(v * Math.PI) * grow), soft, layer + .001);
  });
}
// A pawn on fire (since: seconds since it caught): smaller tongues over its chest.
export function onFire(c, s, since, layer) {
  tongues({ x: c.x, z: c.z - .1 }, s, 7, Math.min(1, since / .3), .3, layer);
}
function tongues(c, s, n, grow, tall, layer) {
  for (let j = 0; j < 3; j++) {
    const ph = s * (6 + j) + n * 1.7 + j * 2.1, x = c.x + (j - 1) * tall * .4 + .03 * Math.sin(ph), h = tall * (1 + .3 * Math.sin(ph * 1.3)) * grow * (j === 1 ? 1.2 : .85);
    sprite({ x, z: c.z + h * .45 - .15 }, tall * .7 * grow, h, FireOuter.withAlpha(.75), glow, layer + j * .0002);
    sprite({ x, z: c.z + h * .35 - .15 }, tall * .35 * grow, h * .6, FireInner.withAlpha(.85), glow, layer + j * .0002 + .0001);
  }
}

// The Stardust Flame button, a stand-in for Shared/Command_TapHold: it fills from the bottom while held, with a line
// at each of `marks` (one per piece a hold can spend); the share above the top line is Last Breath and fills red (a
// second fill colour Command_TapHold does not have yet). press 0..1: the flash of a press; alpha: how lit.
export function button(c, fill, marks, press, alpha, layer) {
  const w = .6, inner = .52, bottom = c.z - inner / 2, segs = marks + 1, top = marks / segs;
  sprite(c, w, w, Frame.withAlpha(.85 * alpha), flat, layer);
  sprite(c, inner, inner, Face.withAlpha(.9 * alpha), flat, layer + .0001);
  sprite(c, .4, .4, CyanColour.withAlpha(.5 * alpha), glow, layer + .0002);
  sprite(c, .16, .16, White.withAlpha(.8 * alpha), glow, layer + .0003);
  const band = (from, to, colour, a) => { if (to > from) sprite({ x: c.x, z: bottom + inner * (from + to) / 2 }, inner, inner * (to - from), colour.withAlpha(a * alpha), flat, layer + .0004); };
  band(0, Math.min(fill, top), FillBlue, .7);
  band(top, fill, Warn, .9);
  for (let i = 1; i <= marks; i++) sprite({ x: c.x, z: bottom + inner * i / segs }, inner, .01, White.withAlpha(.6 * alpha), flat, layer + .0005);
  if (press > 0) sprite(c, w, w, White.withAlpha(.35 * press * alpha), flat, layer + .0006);
}
