// Paper Bomb kit: the drawing pieces shared by Tag Line and Paper Shroud. Not a sketch itself, so
// it is not listed in sketches/index.js.
//
// Everything here is a quad, a level circle or a soft sprite, so nothing needs a per-facing method.
// Every function takes ages and amounts and keeps no state. Mesh keys must be unique within a
// frame: pass a key that names the sketch and the part.
//
// Port notes: quads (MeshPool.plane10) with the white texture, SoftDisc and Puff, one ring mesh,
// and the Goku kit's rock, ring and glint. A burst is additive layers, not a solid shape.
import { Color, Mathf, Meshes, MeshPool, MaterialPool, ShaderDatabase } from '../../js/engine.js';
import { draw } from './six-paths-solid.js';
import { Y, Floor, Lift, sprite, band, glow, soft, rand } from './six-paths-impact.js';
import { streak, whiteGlow, ringAt, rock, glint, shadowLayer, pawnLayer, Ink, Flare, Dust, Stone, StoneTop } from './goku.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01;
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const sealRing = Meshes.band(.78, 1, 24, 'paper bomb seal ring');
export const Paper = new Color(.93, .88, .75), PaperEdge = new Color(.33, .25, .17), InkRed = new Color(.62, .12, .1), Char = new Color(.1, .08, .07);
export const Ember = new Color(1, .5, .12), Hot = new Color(1, .9, .6), Red = new Color(.9, .2, .06), Smoke = new Color(.16, .14, .13), Scorch = new Color(.05, .04, .04);
export const TagLong = .7, TagWide = .26;   // one tag, in cells
export const Seal = .35, Burn = .12;        // hand seal before ignition; a tag curls this long before it bursts

// A quad whose long side points along deg (0 east, 90 north).
export const quad = (pos, along, across, deg, colour, layer, material) => draw(MeshPool.plane10, pos.x, layer, pos.z, along, across, -deg, colour, material);
export const up = (g, h) => ({ x: g.x, z: g.z + h * Lift });

// One explosive tag centred on mid, long side along deg. long and wide scale it (1 = TagLong x TagWide;
// a twisting or flipping sheet passes wide < 1). heat 0..1 lights the ink, curl 0..1 shrinks and chars
// it just before the burst, armed 0..1 is the faint idle pulse.
export function tag(mid, deg, { long = 1, wide = 1, heat = 0, curl = 0, armed = 0, layer = Floor + .014, glowLayer = Y + .02 } = {}) {
  const ls = long * (1 - .6 * curl), r = deg * Mathf.Deg2Rad, tx = Math.cos(r), tz = Math.sin(r);
  const ink = Color.Lerp(Color.Lerp(InkRed, Ember, heat), Char, curl), sheet = Color.Lerp(Color.Lerp(Paper, Hot, heat * .6), Char, curl * .8);
  quad(mid, TagLong * ls, TagWide * wide, deg, ink, layer);
  quad(mid, (TagLong - .08) * ls, (TagWide - .07) * wide, deg, sheet, layer + .001);
  draw(sealRing, mid.x, layer + .002, mid.z, .085 * ls, .085 * wide, -deg, ink);
  quad(mid, .12 * ls, .02 * Math.min(1, long * 1.4), deg, ink, layer + .003); quad(mid, .02 * Math.min(1, long * 1.4), .12 * wide, deg, ink, layer + .003);
  for (const e of [-1, 1]) quad({ x: mid.x + tx * e * .23 * ls, z: mid.z + tz * e * .23 * ls }, .02 * Math.min(1, long * 1.4), .13 * wide, deg, ink, layer + .003);
  if (armed > 0) sprite(mid, .5 * long, .4 * long, Red.withAlpha(.16 * armed), glow, layer + .004);
  if (heat > 0) sprite(mid, .9 * long, .7 * long, Ember.withAlpha(.8 * heat * (1 - curl * .5)), glow, glowLayer);
}

// Wall stand-ins, one cell tall, drawn the way the Power Pole sketches draw theirs: a shadow, the
// south face, and the top shifted north by Lift. Walls sit on the map grid whatever the aim, so
// points are snapped to whole cells from origin o, duplicates dropped, and the north ones drawn first.
// skip(cell) leaves a cell out (a wall that has been blown away).
const StoneFace = new Color(.25, .235, .23);
export const WallTop = pawnLayer + .05;     // a layer above every wall top, for a tag stuck on one
export function walls(key, o, points, sun, strength, skip = () => false) {
  const seen = new Set(), cells = [];
  for (const q of points) { const c = { x: o.x + Math.round(q.x - o.x), z: o.z + Math.round(q.z - o.z) }, id = `${c.x},${c.z}`; if (!seen.has(id) && !skip(c)) { seen.add(id); cells.push(c); } }
  cells.sort((m, n) => n.z - m.z).forEach((w, i) => {
    const step = Math.min(i, 9) * .003;
    sprite({ x: w.x + sun.x * .5, z: w.z + sun.z * .5 }, 1.5, 1.2, Ink.withAlpha(strength), soft, shadowLayer);
    band(`${key} front ${i}`, [{ x: w.x - .5, z: w.z - .5 }, { x: w.x + .5, z: w.z - .5 }], [{ x: w.x - .5, z: w.z - .5 + Lift }, { x: w.x + .5, z: w.z - .5 + Lift }], StoneFace, pawnLayer + .01 + step);
    band(`${key} top ${i}`, [{ x: w.x - .5, z: w.z - .5 + Lift }, { x: w.x + .5, z: w.z - .5 + Lift }], [{ x: w.x - .5, z: w.z + .5 + Lift }, { x: w.x + .5, z: w.z + .5 + Lift }], Stone, pawnLayer + .012 + step);
    quad({ x: w.x, z: w.z + Lift }, .88, .88, 0, StoneTop, pawnLayer + .013 + step);   // each cell is its own block: a lighter top inside a darker edge
  });
}

// The tag scroll in the caster's hand, its axis across deg. A stand-in for the weapon texture.
export function roll(hand, deg, width, spin = 0) {
  quad(hand, .15 + spin, width + .1, deg, PaperEdge, pawnLayer + .02);
  quad(hand, .11 + spin, width + .04, deg, Paper, pawnLayer + .021);
  quad(hand, .11 + spin, .07, deg, InkRed, pawnLayer + .022);
}

// The caster's hand seal: a glint at the chest and a floor ring. amount 0..1.
export function sealFlash(key, caster, amount) {
  if (amount <= 0) return;
  glint(key, { x: caster.x, z: caster.z + .5 }, .3 * amount, amount, Flare, 45);
  ringAt(caster, .3 + .5 * amount, Flare.withAlpha(.6 * amount), Floor + .03);
}

// One burst at floor point g. The floor ring grows to radius, the rule's true blast radius. power
// scales the flash and fireball only. Every part is sampled from age, and what lands stays.
export function burst(key, g, age, radius, sun, strength, seed, power = 1) {
  if (age < 0) return;
  const big = radius * power;
  sprite(g, big * 1.9, big * 1.5, Scorch.withAlpha(.72 * clamp(age / .06) * (1 - .25 * smooth(age / 2))), soft, Floor + .004);
  if (power > 1) sprite(g, big * 1.1, big * .9, Scorch.withAlpha(Math.min(.7, power - 1) * clamp(age / .06)), soft, Floor + .005);   // a stronger burst leaves a darker core
  sprite(up(g, .1), big * 3.4, big * 2.8, Hot.withAlpha(clamp(1 - age / .12)), glow, Y + .2);
  const fb = clamp(age / .45);
  if (fb < 1) {
    const rise = .1 + fb * .7, size = big * (1 + 1.3 * smooth(fb)), al = (1 - fb) * (1 - fb);
    sprite(up(g, rise), size * 1.5, size * 1.3, Red.withAlpha(.8 * al), glow, Y + .16);
    sprite(up(g, rise), size, size * .9, Ember.withAlpha(.9 * al), glow, Y + .17);
    sprite(up(g, rise * .8), size * .55, size * .5, Hot.withAlpha(al), glow, Y + .18);
  }
  if (age < .4) ringAt(g, radius * smooth(age / .22), Hot.withAlpha(.8 * (1 - age / .4)), Floor + .03, true, whiteGlow);
  for (let k = 0; k < 8; k++) {                                     // sparks
    const u = age / .22; if (u >= 1) break;
    const ang = (k / 8 + rand(seed + k) * .1) * Math.PI * 2, r1 = big * (.2 + u * .9), r2 = r1 + big * .5 * (1 - u);
    streak(`${key} spark ${k}`, { x: g.x + Math.cos(ang) * r1, z: g.z + Math.sin(ang) * r1 * .8 }, { x: g.x + Math.cos(ang) * r2, z: g.z + Math.sin(ang) * r2 * .8 }, .06, Hot.withAlpha(1 - u), whiteGlow, Y + .19, 3);
  }
  for (let k = 0; k < 6; k++) {                                     // dust along the floor, then smoke rising
    const ang = k * 1.05 + rand(seed + k + 30), du = age / .7;
    if (du < 1) sprite({ x: g.x + Math.cos(ang) * radius * 1.2 * smooth(du), z: g.z + Math.sin(ang) * radius * .9 * smooth(du) + du * .1 }, .5 + du * .7, .4 + du * .5, Dust.withAlpha(.45 * Math.sin(du * Math.PI)), puff, Y + .08);
    const life = .9 + rand(seed + k + 40) * .6, u = age / life;
    if (u < 1) { const d = big * (.15 + .45 * rand(seed + k + 50)) * smooth(u * 2);
      sprite(up({ x: g.x + Math.cos(ang) * d, z: g.z + Math.sin(ang) * d * .7 }, .2 + u * 1.1), (.5 + u * .9) * power, (.45 + u * .75) * power, Smoke.withAlpha(.55 * Math.sin(u * Math.PI)), puff, Y + .1 + k * .001); }
  }
  for (let k = 0; k < 5; k++) {                                     // rocks: thrown, land, stay
    const ang = rand(seed + k + 60) * Math.PI * 2, dist = radius * (.5 + .9 * rand(seed + k + 70)), land = .3 + .25 * rand(seed + k + 80), u = clamp(age / land);
    const h = (.5 + .6 * rand(seed + k + 90)) * 4 * u * (1 - u), gx = g.x + Math.cos(ang) * dist * u, gz = g.z + Math.sin(ang) * dist * u * .8, size = .09 + .1 * rand(seed + k + 100);
    if (u < 1) sprite({ x: gx + sun.x * h, z: gz + sun.z * h }, size * 1.6, size, Ink.withAlpha(strength), soft, shadowLayer);
    rock({ x: gx, z: gz + h * Lift }, size, u < 1 ? age * 500 : rand(seed + k) * 360, 1, seed + k, u < 1 ? Y + .05 : Floor + .02);
  }
  for (let k = 0; k < 7; k++) {                                     // paper scraps: thrown up, flutter down, stay charred
    const ang = rand(seed + k + 110) * Math.PI * 2, dist = radius * (.6 + rand(seed + k + 120)), land = 1 + .8 * rand(seed + k + 130), u = clamp(age / land);
    const d = dist * (1 - Math.pow(1 - u, 2.5)), h = (.7 + .9 * rand(seed + k + 140)) * (u < .2 ? smooth(u / .2) : 1 - smooth((u - .2) / .8));
    const sway = Math.sin(age * 9 + k) * .12 * (1 - u), flip = u < 1 ? Math.abs(Math.cos(age * 13 + k * 2)) : .8;
    const at = { x: g.x + Math.cos(ang) * d + sway, z: g.z + Math.sin(ang) * d * .8 + h * Lift };
    quad(at, .11, .065 * flip + .012, ang * 57.3 + age * 220 * (1 - u), Color.Lerp(Paper, Char, clamp(age * 1.6 + rand(seed + k) * .5)).withAlpha(.9), u < 1 ? Y + .06 : Floor + .021);
    if (u < 1) sprite(at, .16, .16, Ember.withAlpha(.8 * (1 - u) * (.5 + .5 * Math.sin(age * 30 + k))), glow, Y + .061);
  }
}
