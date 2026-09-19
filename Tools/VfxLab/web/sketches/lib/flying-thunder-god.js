// Flying Thunder God: the drawing pieces shared by the single jump and the chain. Not a sketch
// itself, so it is not listed in sketches/index.js.
//
// Everything here is flat on the ground or a line between two points, so it turns with the
// direction it is given and needs no per-facing method. Every function takes ages and times and
// keeps no state. Mesh keys must be unique within a frame: pass a key that names the sketch and
// the hop.
//
// Port notes: quads (MeshPool.plane10) with the white texture or SoftDisc, one ring mesh, and strip
// meshes rebuilt while they show (streak, slash). Yellow-white, so it is not read as Anchor blue
// or Six Paths violet.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../../js/engine.js';
import { draw, mesh } from './six-paths-solid.js';
import { Y, Floor, sprite, circle, glow, soft, rand } from './six-paths-impact.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01;
const disc = Meshes.disc(32, 'thunder god disc');
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
export const whiteGlow = MaterialPool.MatFrom('white', ShaderDatabase.MoteGlow);
export const kunaiMat = MaterialPool.MatFrom('RimArt/Kunai/Kunai', ShaderDatabase.Cutout);
export const Gold = new Color(1, .78, .18), Pale = new Color(1, .95, .68), Ink = new Color(.13, .09, .02);
export const CasterColour = new Color(.39, .58, .65), EnemyColour = new Color(.55, .38, .27), Skin = new Color(.83, .70, .54);

export const SlashTime = .12, SlashFade = .1, SlashRadius = .85, SlashHalfArc = 60;
export const Afterimage = .3, Scorch = 1.2, SparkLife = .28, Knock = .1, Behind = 1;
// Floor script. A strip starts StripBack cells before the kunai and runs into the landing cell.
export const StripBack = .3, StripGlyphs = 10, CrossStart = .15, CrossGlyphs = 4, GlyphPitch = .13, GlyphFlash = .05;
export const Stroke = .03, Bracket = .7, BracketArm = .18;
// Star glint: [angle, half length, width] per ray, as a share of the flash radius.
const StarRays = [[0, 1.25, .1], [90, 1.0, .1], [45, .5, .06], [135, .5, .06]], StarTurn = 12;
export const LeaveStar = .18, LeaveStarScale = .7;

// A strip between two point lists, in the given material.
export function strip(key, a, b, colour, material, layer) {
  const vertices = [], tri = [];
  for (let i = 0; i < a.length; i++) {
    vertices.push(a[i].x, a[i].z, b[i].x, b[i].z);
    if (i) { const n = i * 2; tri.push(n - 2, n, n - 1, n - 1, n, n + 1); }
  }
  const m = mesh(key); m.setFlat(vertices, tri);
  draw(m, 0, layer, 0, 1, 1, 0, colour, material);
}
// A straight line from a to b, widest in the middle.
export function streak(key, a, b, width, colour, material, layer, steps = 8) {
  const dx = b.x - a.x, dz = b.z - a.z, len = Math.hypot(dx, dz) || 1, nx = -dz / len, nz = dx / len;
  const left = [], right = [];
  for (let i = 0; i <= steps; i++) {
    const u = i / steps, w = Math.sin(u * Math.PI) * width / 2, x = a.x + dx * u, z = a.z + dz * u;
    left.push({ x: x + nx * w, z: z + nz * w }); right.push({ x: x - nx * w, z: z - nz * w });
  }
  strip(key, left, right, colour, material, layer);
}

// The teleport flash as the Naruto games draw it: a star glint. A bright core, a thin ring that
// opens round it, 2 long rays (east-west, north-south) and 2 short diagonal rays, all through the
// centre. It is a light at chest height for a fraction of a second, not a mark on the floor.
export function star(key, at, age, life, size) {
  if (age < 0 || age >= life) return;
  const u = age / life, open = smooth(u / .25), f = (1 - u) * (1 - u), turn = StarTurn * u;
  sprite(at, size * 1.5, size * 1.5, Gold.withAlpha(.7 * f), glow, Y + .04);
  sprite(at, size * .55, size * .55, Pale.withAlpha(f), glow, Y + .05);
  circle(at, size * (.22 + .5 * smooth(u)), .9 * (1 - u), Y + .05, Pale);
  circle(at, size * (.2 + .46 * smooth(u)), .5 * (1 - u), Y + .05, Gold);
  StarRays.forEach(([deg, reach, width], i) => {
    const r = (deg + turn) * Mathf.Deg2Rad, l = size * reach * open * (1 - .35 * u), dx = Math.cos(r) * l, dz = Math.sin(r) * l;
    const from = { x: at.x - dx, z: at.z - dz }, to = { x: at.x + dx, z: at.z + dz }, w = size * width * (1 - .6 * u);
    streak(`${key} glow ${i}`, from, to, w * 2.6, Gold.withAlpha(.6 * f), whiteGlow, Y + .055);
    streak(`${key} ray ${i}`, from, to, w, Pale.withAlpha(Math.min(1, f * 1.4)), undefined, Y + .06);
  });
}

// A two-disc stand-in pawn. thin 0..1 narrows it to a pale vertical sliver.
export function figure(pos, colour, alpha, thin, sun, strength) {
  const w = 1 - thin * .92, h = 1 + thin * .8, body = Color.Lerp(colour, Pale, thin).withAlpha(alpha);
  sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, .85 * w, .4, Ink.withAlpha(strength * alpha * (1 - thin)), soft, shadowLayer);
  draw(disc, pos.x, pawnLayer, pos.z + .18 * h, .22 * w, .32 * h, 0, body);
  draw(disc, pos.x, pawnLayer + .002, pos.z + .58 * h, .16 * w, .17 * h, 0, Color.Lerp(Skin, Pale, thin).withAlpha(alpha));
}

// The same stand-in lying down: the body turned sideways with the head beside it.
export function downed(pos, colour, sun, strength) {
  sprite({ x: pos.x + sun.x * .2, z: pos.z + sun.z * .2 }, .95, .4, Ink.withAlpha(strength), soft, shadowLayer);
  draw(disc, pos.x, pawnLayer, pos.z + .12, .32, .2, 0, colour);
  draw(disc, pos.x + .42, pawnLayer + .002, pos.z + .14, .16, .17, 0, Skin);
}

export function sparks(key, centre, age, count) {
  if (age < 0 || age > SparkLife) return;
  for (let i = 0; i < count; i++) {
    const u = age / (SparkLife * (.6 + .4 * rand(i + 9))); if (u > 1) continue;
    const ang = i * 2.399 + rand(i) * .8, d = .15 + u * (.5 + rand(i + 3) * .7), c = Math.cos(ang), z = Math.sin(ang);
    const tip = { x: centre.x + c * d, z: centre.z + .3 + z * d * .8 }, tail = { x: tip.x - c * .22, z: tip.z - z * .18 };
    streak(`${key} spark ${i}`, tail, tip, .05, Pale.withAlpha(1 - u), whiteGlow, Y + .06, 2);
  }
}

// What stays where the caster left from. gone is seconds since they began to narrow. The scorch is
// optional: the chain leaves one only at the start.
export function leave(key, from, gone, squeeze, starSize, scorch = true) {
  if (gone < 0) return;
  if (scorch) sprite(from, .95, .55, Ink.withAlpha(.42 * (1 - smooth(gone / Scorch))), soft, Floor + .01);
  circle(from, .3 + gone * .5, .7 * (1 - clamp(gone / .5)), Floor + .02, Gold);
  if (gone >= squeeze && gone < squeeze + Afterimage) {
    const fade = 1 - (gone - squeeze) / Afterimage;
    draw(disc, from.x, Y, from.z + .18, .22, .32, 0, Pale.withAlpha(.5 * fade));
    draw(disc, from.x, Y + .002, from.z + .58, .16, .17, 0, Pale.withAlpha(.5 * fade));
    sprite({ x: from.x, z: from.z + .35 }, 1.1, 1.4, Gold.withAlpha(.45 * fade * fade), glow, Y + .004);
  }
  star(`${key} star`, { x: from.x, z: from.z + .3 }, gone, LeaveStar, starSize * LeaveStarScale);
  sparks(key, from, gone, 8);
}

// The line between two places. u is 0..1 through its life.
export function jumpLine(key, from, to, u, width) {
  if (u < 0 || u >= 1) return;
  const alpha = 1 - smooth((u - .6) / .4), a = { x: from.x, z: from.z + .3 }, b = { x: to.x, z: to.z + .3 };
  streak(`${key} glow`, a, b, width * 3.2, Gold.withAlpha(.5 * alpha), whiteGlow, Y + .02, 12);
  streak(key, a, b, width, Pale.withAlpha(alpha), undefined, Y + .03, 12);
}

// One glyph at c, in a strip running in direction deg: a stroke across the strip, a short one along
// it, and sometimes a second short one across. n picks which. fresh 0..1 adds the white glow of a
// glyph that has just been written or just been touched.
function glyph(c, deg, n, colour, fresh) {
  const r = deg * Mathf.Deg2Rad, ux = Math.cos(r), uz = Math.sin(r), vx = -uz, vz = ux;
  const len = .15 + rand(n) * .06, side = rand(n + 40) > .5 ? 1 : -1, off = (rand(n + 80) - .5) * len * .8;
  draw(MeshPool.plane10, c.x, Floor + .03, c.z, Stroke, len, -deg, colour);
  draw(MeshPool.plane10, c.x + ux * side * .04 + vx * off, Floor + .03, c.z + uz * side * .04 + vz * off, .075, Stroke, -deg, colour);
  if (rand(n + 120) > .45) draw(MeshPool.plane10, c.x - ux * side * .045 - vx * off * .6, Floor + .03, c.z - uz * side * .045 - vz * off * .6, Stroke, len * .45, -deg, colour);
  if (fresh > 0) sprite(c, .35, .35, Pale.withAlpha(.7 * fresh), glow, Floor + .035);
}
// One arm of floor script from o in direction deg: glyphs written one after another over
// writeTime from writeStart, and burnt away in the same order as burn goes 0..1.
export function script(o, deg, start, count, s, writeStart, writeTime, burn, seed = 0) {
  const r = deg * Mathf.Deg2Rad, ux = Math.cos(r), uz = Math.sin(r);
  for (let j = 0; j < count; j++) {
    const f = j / (count - 1), since = s - (writeStart + writeTime * f * .9);
    if (since < 0) continue;
    const fresh = 1 - clamp(since / GlyphFlash), left = 1 - clamp((burn - f * .85) / .15);
    if (left <= 0) continue;
    const d = start + j * GlyphPitch;
    glyph({ x: o.x + ux * d, z: o.z + uz * d }, deg, seed * 16 + j, Color.Lerp(Gold, Pale, Math.max(fresh, 1 - left)).withAlpha(left), fresh);
  }
}
// Script written round a circle, turned by turn degrees. flashes is [{ deg, age }]: the glyphs
// within FlashArc degrees of deg go white for FlashTime seconds, which is how a barrier shows where
// it was touched.
const FlashArc = 40, FlashTime = .25;
export function scriptRing(o, radius, count, s, writeStart, writeTime, burn, turn, flashes = [], seed = 0) {
  for (let j = 0; j < count; j++) {
    const f = j / count, since = s - (writeStart + writeTime * f * .9);
    if (since < 0) continue;
    const fresh = 1 - clamp(since / GlyphFlash), left = 1 - clamp((burn - f * .85) / .15);
    if (left <= 0) continue;
    const deg = turn + 360 * f, r = deg * Mathf.Deg2Rad;
    let hot = 0;
    for (const flash of flashes) {
      if (flash.age < 0 || flash.age >= FlashTime) continue;
      const apart = Math.abs(((deg - flash.deg) % 360 + 540) % 360 - 180);
      hot = Math.max(hot, (1 - flash.age / FlashTime) * clamp(1 - apart / FlashArc));
    }
    glyph({ x: o.x + Math.cos(r) * radius, z: o.z + Math.sin(r) * radius }, deg + 90, seed * 16 + j,
      Color.Lerp(Gold, Pale, Math.max(fresh, 1 - left, hot)).withAlpha(left), Math.max(fresh, hot * .8));
  }
}
// The cross of four short arms used where the landing cell is the marked cell itself.
export function scriptCross(o, deg, s, writeStart, writeTime, burn, seed = 0) {
  [0, 90, 180, 270].forEach((turn, k) => script(o, deg + turn, CrossStart, CrossGlyphs, s, writeStart, writeTime, burn, seed + k));
}
// Four corner brackets round the cell the caster lands on, turned to deg.
export function brackets(cell, deg, alpha) {
  if (alpha <= 0) return;
  const r = deg * Mathf.Deg2Rad, ca = Math.cos(r), sa = Math.sin(r), half = Bracket / 2, reach = BracketArm / 2;
  const corner = (along, across) => ({ x: cell.x + along * ca - across * sa, z: cell.z + along * sa + across * ca });
  for (const su of [-1, 1]) for (const sv of [-1, 1]) {
    const u = corner(su * (half - reach), sv * half), v = corner(su * half, sv * (half - reach));
    draw(MeshPool.plane10, u.x, Floor + .03, u.z, BracketArm, Stroke, -deg, Gold.withAlpha(alpha));
    draw(MeshPool.plane10, v.x, Floor + .03, v.z, Stroke, BracketArm, -deg, Gold.withAlpha(alpha));
  }
}

// The melee cut: an arc swept round the caster, centred on direction mid (degrees), age seconds in.
export function slash(key, caster, mid, age) {
  if (age < 0 || age >= SlashTime + SlashFade) return;
  const swept = smooth(age / SlashTime), alpha = 1 - clamp((age - SlashTime) / SlashFade);
  const pivot = { x: caster.x, z: caster.z + .25 }, steps = 14;
  const arc = (width, radius) => {
    const outer = [], inner = [];
    for (let i = 0; i <= steps; i++) {
      const v = i / steps, ang = (mid - SlashHalfArc + 2 * SlashHalfArc * swept * v) * Mathf.Deg2Rad;
      const w = Math.sin(v * Math.PI) * width * (.4 + .6 * v) * clamp(swept * 4);   // no blob before the arc has any length
      outer.push({ x: pivot.x + Math.cos(ang) * (radius + w / 2), z: pivot.z + Math.sin(ang) * (radius + w / 2) });
      inner.push({ x: pivot.x + Math.cos(ang) * (radius - w / 2), z: pivot.z + Math.sin(ang) * (radius - w / 2) });
    }
    return [outer, inner];
  };
  const [go, gi] = arc(.34, SlashRadius), [co, ci] = arc(.13, SlashRadius + .03);
  strip(`${key} glow`, go, gi, Gold.withAlpha(.6 * alpha), whiteGlow, Y + .07);
  strip(key, co, ci, Pale.withAlpha(alpha), undefined, Y + .08);
}
// The spark on the body that was cut. age is seconds since the hit.
export function hitSpark(victim, age) {
  if (age < 0 || age >= .16) return;
  const u = age / .16;
  sprite({ x: victim.x, z: victim.z + .3 }, .5 + u * .9, .5 + u * .9, Pale.withAlpha(.9 * (1 - u)), glow, Y + .09);
}
// How far a cut pawn is pushed, in cells, age seconds after the hit.
export const knocked = age => age < 0 ? 0 : Knock * clamp(age * 40) * (1 - smooth(age / .35));
// How pale a cut pawn is, 0..1.
export const stung = age => age < 0 ? 0 : clamp(1 - age / .12);
// A kunai stuck in a pawn, pointing the way it was thrown (deg), with the seal on it lit 0..1.
export function stuckKunai(victim, deg, lit) {
  const r = deg * Mathf.Deg2Rad, at = { x: victim.x - Math.cos(r) * .1, z: victim.z + .2 - Math.sin(r) * .1 };
  sprite(at, .5, .5, Color.white, kunaiMat, pawnLayer + .01, 90 - deg);   // the texture points north; angles run clockwise
  sprite(at, .55, .55, Gold.withAlpha(.85 * lit), glow, Y + .01);
}
