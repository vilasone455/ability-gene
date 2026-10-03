// Stardust Dragon Staff: how Stardust Flame is drawn. Not a sketch itself, so it is not listed in sketches/index.js.
// The rules are in stardust-dragon-ai.js (breathe()); stardust-dragon.js calls these.
//
// The breath is a stream of night sky, not fire or frost (user's note, 2026-10-03: white to ice to cyan read as a
// frost flame). Three layers: wisps of nebula drawn opaque, deep blue turning indigo and violet as they go, so the
// stream is darker than the ground; cyan light round each wisp; and stars, gold, white and a few cyan, that twinkle,
// ride out with the flame and hang in the air after it has passed. Only the first third, by the mouth, is bright
// cyan. Each wisp and star leaves the mouth where the mouth was when it was born, so a turning head bends the
// stream; it swirls about its line, and stops and spreads at a wall. Last Breath's stream is bigger, brighter and
// fuller of gold stars with each piece it spends. Palette: the dragon's own (Terraria's stardust: cyan and gold on
// deep blue). In C# the wisps and stars are motes or one Shared/VfxDraw batch a frame; the fire is Core's own Fire.
import { Color, Mathf, MaterialPool, ShaderDatabase } from '../../js/engine.js';
import { Floor, sprite, soft, glow, rand } from './six-paths-impact.js';
import { ringAt } from './goku.js';
import { rayWall } from './terraria.js';
import { CyanColour } from './stardust-dragon.js';
import { Deep, GoldLight } from './stardust-dragon-textures.js';

const D2R = Mathf.Deg2Rad, TAU = Math.PI * 2;
const flat = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const nebula = MaterialPool.MatFrom('lab/stardust-nebula', ShaderDatabase.Transparent), nebulaGlow = MaterialPool.MatFrom('lab/stardust-nebula', ShaderDatabase.MoteGlow);
const starMat = MaterialPool.MatFrom('lab/stardust-star', ShaderDatabase.MoteGlow);
const White = new Color(1, 1, 1), DeepColour = new Color(...Deep), Gold = new Color(...GoldLight), Warn = new Color(.85, .18, .12);
const Indigo = new Color(.14, .1, .52), Violet = new Color(.36, .16, .62);
export const Starlit = new Color(.55, .5, 1);   // a pawn the flame is burning is tinted toward this
const FireOuter = new Color(1, .45, .1), FireInner = new Color(1, .84, .38), Smoke = new Color(.3, .3, .32);
const Frame = new Color(.08, .08, .1), Face = new Color(.2, .22, .28), FillBlue = new Color(.55, .8, 1);   // Command_TapHold's fill colour

const Emit = 1 / 150, Life = .5;          // seconds between wisps; seconds a wisp lives
const StarEmit = 1 / 110, StarLife = 1.1, StarOut = .45;   // stars: born this often, live this long, reach their spot in this share of it
const Swirl = 1.3;                        // turns a wisp swirls about the flame's line over its life
const PulseRun = .35;                     // seconds a pulse of light takes from the back of the body to the mouth

// Where something born at step kb, `d` cells out on a line `a` and `twist` cells off it, is: from the mouth then.
function along(r, kb, oc, a, d, twist) {
  return { x: oc.x + r.mouthX[kb] + Math.cos(a) * d - Math.sin(a) * twist, z: oc.z + r.mouthZ[kb] + Math.sin(a) * d + Math.cos(a) * twist };
}
// Cells from the mouth at step kb along `a` to the first wall, or Infinity.
function wallOn(r, kb, oc, a, walls, max) {
  return walls.length ? rayWall({ x: oc.x + r.mouthX[kb], z: oc.z + r.mouthZ[kb] }, a, walls, max) : Infinity;
}
// A star at pos: size in cells, twinkling (0..1 of its size), turned deg.
function star(pos, size, colour, alpha, layer, deg = 0) {
  sprite(pos, size, size, colour.withAlpha(alpha), starMat, layer, deg);
}
const starColour = (q, gold) => q < gold ? Gold : q < .8 + .2 * gold ? White : CyanColour;

// The breath at time s. r: the simulation; oc: the wielder's cell where the dragon is drawn (its chest lift included);
// walls: wall cell centres in the same frame.
export function flame(r, s, oc, walls, layer) {
  for (let i = Math.ceil((s - Life) / Emit); i <= Math.floor(s / Emit); i++) {
    const tb = i * Emit, kb = r.step(tb), f = r.flame[kb], u = (s - tb) / Life;
    if (f <= 0 || u < 0 || u >= 1) continue;
    const heat = r.heat[kb], reach = r.reach[kb], a = r.mouthR[kb] + (rand(i * 3.1) - .5) * 1.7 * r.spread[kb], far = reach * (.8 + .25 * rand(i * 5.7));
    const wall = wallOn(r, kb, oc, a, walls, far + 1);
    let d = far * Math.pow(u, .8), splash = 0;
    if (d > wall) { splash = Math.min(1, (d - wall) / .6); d = wall; }
    const twist = Math.sin(u * TAU * Swirl + rand(i * 7.3) * TAU) * Math.tan(r.spread[kb]) * d * .6;
    const pos = along(r, kb, oc, a, d, twist), spin = rand(i * 1.9) * 360 + u * 70;
    const size = (.3 + 1.5 * u) * Math.sqrt(reach / 5) * (1 + .4 * heat) * (1 + .8 * splash);
    const out = f * Math.pow(1 - u, 1.1) * (1 - .6 * splash), body = u < .5 ? Color.Lerp(DeepColour, Indigo, u / .5) : Color.Lerp(Indigo, Violet, (u - .5) / .5);
    const at = layer + (i % 64) * .00002;
    sprite(pos, size * 1.3, size, Color.Lerp(body, CyanColour, .25 * heat).withAlpha(.55 * out * Math.min(1, u / .12)), nebula, at, spin);
    sprite(pos, size * 1.5, size * 1.15, CyanColour.withAlpha(.3 * out * (1 - .5 * u) * (1 + .5 * heat)), nebulaGlow, at + .001, spin);
    if (u < .3) sprite(pos, size * .7, size * .4, Color.Lerp(White, CyanColour, u / .3).withAlpha(.7 * out * (1 - u / .3)), glow, at + .0015, -a / D2R);
  }
  // The stars: out with the flame over StarOut of their life, then hanging where they stopped, drifting a little.
  for (let j = Math.ceil((s - StarLife) / StarEmit); j <= Math.floor(s / StarEmit); j++) {
    const tb = j * StarEmit, kb = r.step(tb), f = r.flame[kb], u = (s - tb) / StarLife;
    if (f <= 0 || u < 0 || u >= 1) continue;
    const heat = r.heat[kb], reach = r.reach[kb], a = r.mouthR[kb] + (rand(j * 2.3) - .5) * 1.8 * r.spread[kb], far = reach * (.45 + .6 * rand(j * 4.1));
    const go = 1 - Math.pow(1 - Math.min(1, u / StarOut), 2), d = Math.min(far * go, wallOn(r, kb, oc, a, walls, far + 1));
    const twist = Math.sin(go * TAU * Swirl + rand(j * 6.1) * TAU) * Math.tan(r.spread[kb]) * d * .6, hang = Math.max(0, u - StarOut);
    const pos = along(r, kb, oc, a + (rand(j * 8.9) - .5) * .4 * hang, d + .3 * hang, twist);
    const twinkle = .55 + .45 * Math.sin((s - tb) * 18 + j * 2.1), grow = Math.min(1, u / .08);
    star(pos, (.16 + .22 * rand(j * 5.3)) * (1 + .5 * heat) * twinkle * grow, starColour(rand(j * 9.7), .45 + .3 * heat), f * Math.pow(1 - u, .7), layer + .003 + (j % 64) * .00002, rand(j) * 90);
  }
}

// The glow at the mouth and the flame's light on the floor under the cone, at step k; below: how far the floor is
// under the dragon on screen.
export function mouthGlow(r, k, oc, below, layer) {
  const f = r.flame[k];
  if (f <= 0) return;
  const m = { x: oc.x + r.mouthX[k], z: oc.z + r.mouthZ[k] }, rot = r.mouthR[k], g = 1 + r.heat[k], reach = r.reach[k];
  sprite(m, 1.4 * g, 1.1 * g, CyanColour.withAlpha(.35 * f), glow, layer + .0028);
  sprite(m, .45 * g, .3 * g, Color.Lerp(White, Gold, .3).withAlpha(.7 * f), glow, layer + .003, -rot / D2R);
  const mid = { x: m.x + Math.cos(rot) * reach * .5, z: m.z + Math.sin(rot) * reach * .5 - below }, w = 2 * reach * Math.tan(r.spread[k]) + .8;
  sprite(mid, reach * 1.2, w, DeepColour.withAlpha(.12 * f), glow, Floor + .013, -rot / D2R);
  sprite(mid, reach * .9, w * .6, CyanColour.withAlpha(.08 * f), glow, Floor + .0132, -rot / D2R);
}

// A piece the flame is burning, b 0..1: a flickering cyan glow over it that grows as it goes, and two gold stars.
// cells: the size of one Terraria pixel on screen.
export function burning(c, rot, cells, b, s, layer) {
  if (b <= 0) return;
  const flick = .8 + .2 * Math.sin(s * 30 + c.x * 7);
  sprite(c, 44 * cells * (1 + .4 * b), 30 * cells * (1 + .4 * b), CyanColour.withAlpha(.4 * b * flick), glow, layer, -rot / D2R);
  sprite(c, 20 * cells, 20 * cells, White.withAlpha(.5 * b * flick), glow, layer + .0001);
  for (let q = 0; q < 2; q++) {
    const a = s * 3 + q * Math.PI + c.x, tw = .6 + .4 * Math.sin(s * 17 + q * 2);
    star({ x: c.x + Math.cos(a) * 12 * cells, z: c.z + Math.sin(a) * 12 * cells }, 26 * cells * b * tw, Gold, b, layer + .0002);
  }
}

// The pulses of light that run up the body from the back to the mouth while a piece burns (bigger when one is
// spent), gold like the stars the flame carries. list: the drawn pieces, head first.
export function pulses(r, s, list, cells, layer) {
  if (list.length < 2) return;
  for (const p of r.pulses) {
    const v = (s - p.t) / PulseRun;
    if (v < 0 || v >= 1) continue;
    const f = (list.length - 1) * (1 - v), i = Math.min(list.length - 2, Math.floor(f)), u = f - i, a = list[i], b = list[i + 1];
    const c = { x: Mathf.Lerp(a.c.x, b.c.x, u), z: Mathf.Lerp(a.c.z, b.c.z, u) }, size = (p.big ? 30 : 18) * cells, al = (p.big ? .8 : .5) * Math.sin(Math.PI * (.15 + .85 * v));
    sprite(c, size * 1.6, size, Gold.withAlpha(al * .7), glow, layer, -Mathf.Lerp(a.rot, b.rot, u) / D2R);
    star(c, size * 1.2, White, al, layer + .0001, v * 90);
  }
}

// A pawn the flame is burning: each burn flings three stars off its chest over 0.5 s.
export function starBurn(chest, s, burns, layer) {
  burns.forEach((e, n) => {
    const age = s - e.t;
    if (age < 0 || age >= .5) return;
    for (let q = 0; q < 3; q++) {
      const a = rand(n * 3 + q) * TAU, d = .4 * (1 - (1 - age / .5) ** 2), tw = .6 + .4 * Math.sin(age * 30 + q);
      star({ x: chest.x + Math.cos(a) * d, z: chest.z + Math.sin(a) * d }, .22 * tw, starColour(rand(n * 5 + q), .5), 1 - age / .5, layer + q * .0001, q * 30);
    }
  });
}

// Last Breath's burst, b = { t, x, z, radius } from the wielder: a white flash in a cyan bloom, a ring out to the radius
// on the floor, gold and white stars flung out to it, and its light on the floor fading over 1.2 s.
export function burst(b, s, oc, below, layer) {
  const age = b ? s - b.t : -1;
  if (age < 0 || age > 1.4) return;
  const c = { x: oc.x + b.x, z: oc.z + b.z }, floor = { x: c.x, z: c.z - below }, e = 1 - Math.pow(1 - Math.min(1, age / .35), 3);
  sprite(c, b.radius * 3.2 * e, b.radius * 3.2 * e, CyanColour.withAlpha(.5 * Math.max(0, 1 - age / .7)), glow, layer);
  sprite(c, b.radius * 2.6 * (.3 + .7 * e), b.radius * 2.6 * (.3 + .7 * e), White.withAlpha(Math.max(0, 1 - age / .3)), glow, layer + .001);
  ringAt(floor, b.radius * e, CyanColour.withAlpha(.8 * Math.max(0, 1 - age / .6)), Floor + .03, true);
  sprite(floor, b.radius * 2.5, b.radius * 2, DeepColour.withAlpha(.3 * Math.max(0, 1 - age / 1.2)), glow, Floor + .014);
  for (let q = 0; q < 30; q++) {
    const a = q / 30 * TAU + rand(q) * .3, d = b.radius * (.2 + .9 * e) * (.7 + .4 * rand(q + 40)), tw = .6 + .4 * Math.sin(age * 16 + q);
    star({ x: c.x + Math.cos(a) * d, z: c.z + Math.sin(a) * d }, .35 * tw * (1 - age / 1.4), starColour(rand(q + 80), .55), 1 - age / 1.4, layer + .002 + q * .00002, q * 17);
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
  star(c, .3, Gold, .9 * alpha, layer + .0003);
  const band = (from, to, colour, a) => { if (to > from) sprite({ x: c.x, z: bottom + inner * (from + to) / 2 }, inner, inner * (to - from), colour.withAlpha(a * alpha), flat, layer + .0004); };
  band(0, Math.min(fill, top), FillBlue, .7);
  band(top, fill, Warn, .9);
  for (let i = 1; i <= marks; i++) sprite({ x: c.x, z: bottom + inner * i / segs }, inner, .01, White.withAlpha(.6 * alpha), flat, layer + .0005);
  if (press > 0) sprite(c, w, w, White.withAlpha(.35 * press * alpha), flat, layer + .0006);
}
