// Bank Shot gun kit: the drawing pieces for the charge-mode shot, and for the normal shot's single
// bounce once that is sketched. Not a sketch itself, so it is not listed in sketches/index.js. The rule (walls, bounces, hits) is in
// bank-shot-path.js and has no drawing in it.
//
// The bullet flies level at HandH, so every point of its path is a ground point lifted by
// HandH * Lift, and every shape here is a level line, a quad or a soft sprite: nothing needs a
// per-facing method. Every function takes ages and keeps no state; what lands (chips on a wall,
// blood on the floor) is drawn for every later time so it stays.
//
// Port notes: quads with the white texture, SoftDisc and Puff, the Goku kit's line/streak/glint
// and the Paper Bomb kit's wall stand-ins. The tracer and sparks are additive; the smoke thread
// and the chips are alpha-blended.
import { Color, Mathf, MaterialPool, ShaderDatabase } from '../../js/engine.js';
import { Body, Y, Floor, Lift, sprite, band, soft, rand } from './six-paths-impact.js';
import { draw } from './six-paths-solid.js';
import { disc, rect, shadowLayer, pawnLayer, Skin, IronDark, Iron, IronLit, Pale, Dust, Blood, screen, shadow } from './chain-sickle.js';
import { line, streak, glint, whiteGlow, Flare } from './goku.js';
import { walls, WallTop } from './paper-bomb.js';
import { MaxBounces, along } from './bank-shot-path.js';
export { walls, WallTop };

const clamp = Mathf.Clamp01, lerp = Mathf.Lerp, D2R = Mathf.Deg2Rad, TAU = Math.PI * 2;
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const flat = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
export const Tracer = new Color(1, .93, .62), TracerHot = new Color(1, .58, .18), Core = new Color(1, 1, 1);
export const Smoke = new Color(.42, .40, .37), Chip = new Color(.12, .11, .10), ChipLit = new Color(.62, .58, .54);
export const Blocked = new Color(.75, .16, .10);
export const Enemy = new Color(.55, .38, .27), Holder = new Color(.93, .50, .13);
// Decided looks and the rule's fixed numbers.
export const HandH = .5;                       // the gun and the bullet, cells up
export const GripAlong = .12, MuzzleAlong = .5; // from the caster's feet, along the aim
export const ChestH = .45;
export const BaseDamage = 18, BounceDamage = 6; // shown as the tracer growing wider and hotter per bounce
export const Lead = .35, Tail = .3;
export const bump = x => (x >= 0 && x <= 1) ? Math.sin(x * Math.PI) : 0;
export const heat = n => clamp(n / MaxBounces);
export const tracerColour = n => Color.Lerp(Tracer, TracerHot, heat(n));
export const tracerWidth = n => .07 + .025 * n;
export const lift = q => ({ x: q.x, z: q.z + HandH * Lift });
export const dirOf = deg => ({ x: Math.cos(deg * D2R), z: Math.sin(deg * D2R) });
export const move = (q, dir, k) => ({ x: q.x + dir.x * k, z: q.z + dir.z * k });

// Stand-in pawn on `layer`, so it can sit above the wall faces when it stands south of a wall.
export function figure(pos, colour, sun, strength, layer = pawnLayer + .045) {
  sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, .85, .4, Body.withAlpha(strength), soft, shadowLayer);
  draw(disc, pos.x, layer, pos.z + .18, .22, .32, 0, colour);
  draw(disc, pos.x, layer + .002, pos.z + .58, .16, .17, 0, Skin);
}

// The gun: a heavy pistol lying level at HandH, along `deg` from the hand's ground point. `kick`
// slides it back along the aim (recoil). Drawn above the pawn so it reads as held in front.
export function pistol(key, hand, deg, sun, strength, kick = 0, layer = pawnLayer + .06) {
  const dir = dirOf(deg), base = { ...move(hand, dir, -kick), h: HandH };
  const p = screen(base), sd = shadow(base, sun);
  const off = (q, a, b) => ({ x: q.x + dir.x * a - dir.z * b, z: q.z + dir.z * a + dir.x * b });
  rect(`${key} shadow`, off(sd, .16, 0), .48, .11, deg, Body.withAlpha(strength * .5), shadowLayer);
  rect(`${key} grip`, off(p, -.03, -.07), .15, .075, deg - 70, IronDark, layer);
  rect(`${key} body`, off(p, .14, 0), .36, .09, deg, IronDark, layer + .002);
  rect(`${key} slide`, off(p, .17, .005), .30, .055, deg, Iron, layer + .004);
  rect(`${key} barrel`, off(p, .36, 0), .10, .045, deg, Iron, layer + .004);
  rect(`${key} lit`, off(p, .17, .025), .27, .014, deg, IronLit.withAlpha(.9), layer + .006);
}

// The muzzle flash and the smoke that follows it. `m` is the muzzle's ground point.
export function muzzle(key, m, deg, age) {
  if (age < 0) return;
  const q = lift(m), dir = dirOf(deg);
  if (age < .09) {
    const a = 1 - age / .09;
    glint(`${key} flash`, q, .55 * (1 + age * 3), a, Flare, deg);
    streak(`${key} tongue`, q, move(q, dir, .55 + age * 3), .16 * a, Flare.withAlpha(a), whiteGlow, Y + .12, 4);
  }
  for (let i = 0; i < 3; i++) {
    const u = (age - i * .06) / .7; if (u < 0 || u > 1) continue;
    const g = move(q, dir, .25 + u * .8 + i * .12);
    sprite({ x: g.x + (rand(i + 700) - .5) * .2, z: g.z + u * .35 + (rand(i + 710) - .5) * .15 }, .25 + u * .5, .22 + u * .45, Smoke.withAlpha((1 - u) * .32), puff, Y + .02);
  }
}

// The bullet at distance d along the path, with its tracer tail. n is the bounces so far.
export function bullet(key, path, d, n) {
  const here = along(path, d), q = lift(here), dir = { x: here.dx, z: here.dz };
  const tail = Math.min(.5, d), col = tracerColour(n), w = tracerWidth(n);
  streak(`${key} tail`, move(q, dir, -tail), q, w * 1.6, col.withAlpha(.9), whiteGlow, Y + .10, 6);
  sprite(q, .30 + .05 * n, .26 + .05 * n, col.withAlpha(.8), MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.MoteGlow), Y + .11);
  streak(`${key} core`, move(q, dir, -.16), move(q, dir, .04), w * .55, Core.withAlpha(1), whiteGlow, Y + .115, 4);
}

// The flown part of the path: a bright trace that fades `life` seconds after the bullet passed,
// and a thin smoke thread under it that lingers longer. Chunks are cut at every bounce so no
// chunk crosses a corner. dNow is how far the bullet has flown, t0 the moment it left the muzzle.
export function trail(key, path, dNow, s, t0, speed, life) {
  const cuts = [0];
  for (let d = .5; d < dNow; d += .5) cuts.push(d);
  for (const b of path.bounces) if (b.d < dNow) cuts.push(b.d);
  cuts.push(dNow);
  cuts.sort((a, b) => a - b);
  let n = 0;
  for (let i = 0; i + 1 < cuts.length; i++) {
    const d0 = cuts[i], d1 = cuts[i + 1]; if (d1 - d0 < .01) continue;
    const mid = (d0 + d1) / 2, age = s - (t0 + mid / speed); if (age < 0) continue;
    while (n < path.bounces.length && path.bounces[n].d <= d0 + .001) n++;
    const a = lift(along(path, d0)), b = lift(along(path, d1));
    const bright = Math.exp(-age / life);
    line(`${key} trace ${i}`, [a, b], tracerWidth(n) * (.7 + .3 * bright), tracerColour(n).withAlpha(.75 * bright), whiteGlow, Y + .08, 'none');
    line(`${key} smoke ${i}`, [a, b], .05 + .04 * clamp(age / 1.2), Smoke.withAlpha(.30 * Math.exp(-age / 1.8) * clamp(age / .15)), flat, Y + .015, 'none');
  }
}

// One ricochet at bounce b (ground point, face normal) that happened `age` seconds ago, the n-th
// bounce. The chip on the wall stays; the flash, sparks and dust last under half a second.
export function ricochet(key, b, age, n, sun) {
  if (age < 0) return;
  const q = lift(b), nrm = b.normal, nDeg = Math.atan2(nrm.z, nrm.x) / D2R, k = .8 + .25 * n;
  // Chip: a dark pit with pale broken stone around it, on the wall face or top.
  sprite(q, .16 * k, .13 * k, Chip.withAlpha(.85), soft, WallTop + .01);
  for (let i = 0; i < 4; i++) {
    const a = (nDeg + (rand(i + 20 + n * 9) - .5) * 160) * D2R, r = .06 + rand(i + 30 + n * 9) * .07;
    sprite({ x: q.x + Math.cos(a) * r, z: q.z + Math.sin(a) * r }, .06, .05, ChipLit.withAlpha(.8), soft, WallTop + .011);
  }
  if (age < .12) glint(`${key} flash`, q, (.45 + .12 * n) * (1 + age * 2), 1 - age / .12, Flare, nDeg);
  const sparks = 7 + 3 * n;
  for (let i = 0; i < sparks; i++) {
    const life = .22 + rand(i + n * 31) * .18, u = age / life; if (u > 1) continue;
    const a = (nDeg + (rand(i + 50 + n * 31) - .5) * 150) * D2R, v = (2.5 + rand(i + 60 + n * 31) * 4) * k;
    const x = q.x + Math.cos(a) * v * age, z = q.z + Math.sin(a) * v * age - 5 * age * age;
    const dx = Math.cos(a) * v, dz = Math.sin(a) * v - 10 * age, L = Math.hypot(dx, dz) || 1, len = .06 + .05 * (1 - u);
    streak(`${key} spark ${i}`, { x: x - dx / L * len, z: z - dz / L * len }, { x, z }, .035, Color.Lerp(Flare, TracerHot, u).withAlpha(1 - u * u), whiteGlow, Y + .13, 3);
  }
  for (let i = 0; i < 3; i++) {
    const u = (age - i * .04) / .5; if (u < 0 || u > 1) continue;
    const g = { x: q.x + nrm.x * (.15 + u * .45) + (rand(i + 80 + n) - .5) * .25, z: q.z + nrm.z * (.15 + u * .45) + u * .25 };
    sprite(g, .22 + u * .35, .2 + u * .3, Dust.withAlpha((1 - u) * .35), puff, Y + .03);
  }
}

// The bullet embeds after its last allowed contact: a bigger chip, a puff and a fading glow.
export function embed(key, e, age, sun) {
  if (age < 0) return;
  ricochet(key, e, age, MaxBounces, sun);
  const q = lift(e);
  sprite(q, .22, .18, Chip.withAlpha(.9), soft, WallTop + .012);
  if (age < .5) sprite(q, .3, .25, TracerHot.withAlpha(.6 * (1 - age / .5)), MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.MoteGlow), Y + .05);
}

// The bullet hits a pawn at ground point `o`, travelling along `dir`. A flash at the chest, blood
// thrown on past the pawn that lands and stays.
export function wound(key, o, dir, age, n) {
  if (age < 0) return;
  const chest = { x: o.x, z: o.z + ChestH * Lift }, k = .8 + .2 * n;
  if (age < .1) glint(`${key} flash`, chest, .5 * k, 1 - age / .1, Flare);
  if (age < .12) sprite(chest, .5 * k, .4 * k, Blood.withAlpha(.8 * (1 - age / .12)), soft, Y + .09);
  for (let i = 0; i < 8; i++) {
    const life = .25 + rand(i + 900) * .12, u = age / life; if (u > 1) continue;
    const a = Math.atan2(dir.z, dir.x) + (rand(i + 910) - .5) * 1.1, v = (2 + rand(i + 920) * 3.5) * k;
    const x = chest.x + Math.cos(a) * v * age, z = chest.z + Math.sin(a) * v * age - 6 * age * age;
    sprite({ x, z }, .16 * k, .12 * k, Blood.withAlpha(1 - u * u), soft, Y + .09);
  }
  // Floor spatter beyond the pawn, growing over .3 s and then staying.
  const g = clamp(age / .3), c = move(o, dir, .55);
  sprite(c, 1.0 * g * k, .55 * g * k, Blood.withAlpha(.75 * g), soft, Floor + .02, Math.atan2(dir.z, dir.x) / D2R);
  for (let i = 0; i < 5; i++) {
    const a = Math.atan2(dir.z, dir.x) + (rand(i + 930) - .5) * .9, r = .5 + rand(i + 940) * .9 * k;
    if (g < .5 + i * .1) continue;
    sprite({ x: o.x + Math.cos(a) * r, z: o.z + Math.sin(a) * r }, .14 + rand(i + 950) * .12, .11 + rand(i + 960) * .09, Blood.withAlpha(.8), soft, Floor + .021);
  }
}

// The aim preview the player sees before firing: pale dashes along the whole bounce path, a
// square on the wall cell they targeted, and the numbered contacts. alpha fades it.
export function preview(key, path, alpha) {
  if (alpha <= 0) return;
  const dash = .32, gap = .22;
  let i = 0;
  for (let d = 0; d < path.length; d += dash + gap, i++) {
    const a = lift(along(path, d)), b = lift(along(path, Math.min(path.length, d + dash)));
    streak(`${key} dash ${i}`, a, b, .05, Pale.withAlpha(.55 * alpha), whiteGlow, Y + .05, 2);
  }
  const first = path.bounces[0];
  if (first) {
    const c = { x: first.cell.x, z: first.cell.z + Lift }, h = .48;
    band(`${key} tile n`, [{ x: c.x - h, z: c.z + h }, { x: c.x + h, z: c.z + h }], [{ x: c.x - h, z: c.z + h - .05 }, { x: c.x + h, z: c.z + h - .05 }], Pale.withAlpha(.8 * alpha), WallTop + .02);
    band(`${key} tile s`, [{ x: c.x - h, z: c.z - h + .05 }, { x: c.x + h, z: c.z - h + .05 }], [{ x: c.x - h, z: c.z - h }, { x: c.x + h, z: c.z - h }], Pale.withAlpha(.8 * alpha), WallTop + .02);
    band(`${key} tile w`, [{ x: c.x - h, z: c.z - h }, { x: c.x - h, z: c.z + h }], [{ x: c.x - h + .05, z: c.z - h }, { x: c.x - h + .05, z: c.z + h }], Pale.withAlpha(.8 * alpha), WallTop + .02);
    band(`${key} tile e`, [{ x: c.x + h - .05, z: c.z - h }, { x: c.x + h - .05, z: c.z + h }], [{ x: c.x + h, z: c.z - h }, { x: c.x + h, z: c.z + h }], Pale.withAlpha(.8 * alpha), WallTop + .02);
  }
  for (const b of path.bounces) sprite(lift(b), .2, .17, Pale.withAlpha(.7 * alpha), soft, Y + .06);
  const e = path.end;
  if (e.kind === 'embed') sprite(lift(e), .16, .14, Blocked.withAlpha(.7 * alpha), soft, Y + .06);
}

// The straight shot that is not possible: red dashes on the floor from the caster toward the
// enemy, stopping at the wall with a cross.
export function blockedLine(key, a, hit, alpha) {
  if (alpha <= 0 || !hit) return;
  const dx = hit.x - a.x, dz = hit.z - a.z, L = Math.hypot(dx, dz) || 1, ux = dx / L, uz = dz / L;
  let i = 0;
  for (let d = .5; d < L - .05; d += .45, i++) {
    const p0 = { x: a.x + ux * d, z: a.z + uz * d }, p1 = { x: a.x + ux * Math.min(L, d + .25), z: a.z + uz * Math.min(L, d + .25) };
    line(`${key} dash ${i}`, [p0, p1], .06, Blocked.withAlpha(.6 * alpha), flat, Floor + .03, 'none');
  }
  const c = { x: hit.x - ux * .12, z: hit.z - uz * .12 };
  for (const t of [45, -45]) line(`${key} x ${t}`, [{ x: c.x - Math.cos(t * D2R) * .16, z: c.z - Math.sin(t * D2R) * .16 }, { x: c.x + Math.cos(t * D2R) * .16, z: c.z + Math.sin(t * D2R) * .16 }], .06, Blocked.withAlpha(.85 * alpha), flat, Floor + .031, 'none');
}
