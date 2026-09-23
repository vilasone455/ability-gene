// Gojo kit: the drawing pieces shared by Unlimited Void and the abilities that come after it
// (Blue, Red, Hollow Purple, Infinity). Not a sketch itself, so it is not listed in sketches/index.js.
//
// Everything here is a level circle, a quad, a line between two points or a flat polygon about one
// point, so nothing needs a per-facing method. Every function takes ages and times and keeps no
// state. Mesh keys must be unique within a frame: pass a key that names the sketch and the part.
//
// Port notes: quads (MeshPool.plane10) with the white texture or SoftDisc, one disc mesh, the Goku
// ring set, and strip meshes rebuilt while they show. The palette is Void (black-blue), Blue and Ice
// from the Vergil kit plus a violet halo, so it is not read as Six Paths violet or Goku ki blue-white.
// pawn, ringAt, glint, streak and strip are the Goku kit's.
import { Color, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../../js/engine.js';
import { draw, mesh } from './six-paths-solid.js';
import { Y, Floor, Lift, sprite, glow, soft, rand } from './six-paths-impact.js';
import { pawn, ringAt, glint, streak, strip, line, whiteGlow, Ink, Skin, EnemyColour, Ally, White, pawnLayer, shadowLayer, smooth, clamp } from './goku.js';
import { Void, Blue, Ice } from './vergil.js';
export { pawn, ringAt, glint, streak, strip, line, whiteGlow, Ink, Skin, EnemyColour, Ally, White, pawnLayer, shadowLayer, smooth, clamp, Void, Blue, Ice, Lift };

const disc = Meshes.disc(32, 'gojo disc');
export const Uniform = new Color(.08, .09, .14), UniformLit = new Color(.17, .19, .28), Hair = new Color(.93, .95, .98);
export const Blindfold = new Color(.05, .05, .07), EyeBlue = new Color(.35, .75, 1), Violet = new Color(.55, .4, .95);
export const Pink = new Color(1, .55, .85), Gold = new Color(1, .92, .75), Teal = new Color(.55, .95, .78);
const puffGlow = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.MoteGlow);

// The kit's carrier as a stand-in: white hair, dark uniform, a blindfold across the eyes.
//   blindfold 0..1  how much of the blindfold is still on; the blue eyes show as it comes off
//   sign 0..1       the hands move from the sides to meet at the chest; past .85 the two index
//                   fingers stand up (the domain hand sign)
//   layer           altitude of the figure (default the pawn layer); the cutscene draws it over its overlay
//   crossed         the source's sign instead (anime ep. 7 and 33, manga ch. 225): one hand rises in
//                   front of the face with the middle finger crossed over the index finger, and the
//                   other hand pulls the blindfold down to the neck (blindfold 1 on the eyes, 0 at
//                   the neck) instead of it fading. The dome and cutscene sketches keep the old sign.
export function caster(pos, sun, strength, { blindfold = 1, sign = 0, alpha = 1, tint = null, tintAmount = 0, rim = 0, layer = pawnLayer, crossed = false } = {}) {
  if (crossed) return casterCrossed(pos, sun, strength, { blindfold, sign, alpha, tint, tintAmount, rim, layer });
  if (alpha <= 0) return;
  const c = q => (tint ? Color.Lerp(q, tint, tintAmount) : q).withAlpha(alpha * q.a), pawnLayer = layer;
  // Rim light (the game's cyan-violet edge on Gojo): a glow behind the figure and a pale edge round the body.
  if (rim > 0) {
    sprite({ x: pos.x, z: pos.z + .3 }, .9, 1.2, EyeBlue.withAlpha(.5 * rim * alpha), glow, pawnLayer - .005);
    draw(disc, pos.x, pawnLayer - .003, pos.z + .18, .26, .36, 0, Violet.withAlpha(.7 * rim * alpha));
    draw(disc, pos.x, pawnLayer - .003, pos.z + .58, .19, .2, 0, Violet.withAlpha(.7 * rim * alpha));
  }
  sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, .85, .4, Ink.withAlpha(strength * alpha), soft, shadowLayer);
  [[0, .18, .22, .32, Uniform], [.04, .22, .08, .24, UniformLit], [0, .58, .16, .17, Skin], [0, .69, .19, .1, Hair]]
    .forEach(([cx, cz, rx, rz, colour], k) => draw(disc, pos.x + cx, pawnLayer + k * .002, pos.z + cz, rx, rz, 0, c(colour)));
  const top = pawnLayer + .012, face = { x: pos.x, z: pos.z + .6 };
  // Eyes under the blindfold: two blue dots that light as it comes off.
  const eyes = clamp(1 - blindfold);
  if (eyes > 0) [-.055, .055].forEach((dx, i) => {
    draw(disc, face.x + dx, top, face.z, .028, .022, 0, EyeBlue.withAlpha(alpha));
    sprite({ x: face.x + dx, z: face.z }, .16, .12, EyeBlue.withAlpha(.6 * eyes * alpha), glow, top + .004 + i * .0005);
  });
  if (blindfold > 0) draw(MeshPool.plane10, face.x, top + .001, face.z, .32, .075, 0, Blindfold.withAlpha(alpha * clamp(blindfold * 1.4)));
  // Hands: at the sides, or meeting at the chest for the sign.
  [-1, 1].forEach(side => {
    const x = pos.x + side * Mathf.Lerp(.22, .045, sign), z = pos.z + Mathf.Lerp(.28, .42, sign);
    draw(MeshPool.plane10, x, top + .002, z, .09, .11, side * 12 * (1 - sign), c(Skin));
  });
  const fingers = clamp((sign - .85) / .15);
  if (fingers > 0) [-1, 1].forEach(side => draw(MeshPool.plane10, pos.x + side * .022, top + .003, pos.z + .53 + .06 * fingers, .034, .16 * fingers, 0, c(Skin)));
}

// caster() with crossed: the same figure, the source's hand sign and the blindfold pulled down.
function casterCrossed(pos, sun, strength, { blindfold, sign, alpha, tint, tintAmount, rim, layer }) {
  if (alpha <= 0) return;
  const c = q => (tint ? Color.Lerp(q, tint, tintAmount) : q).withAlpha(alpha * q.a), L = layer;
  if (rim > 0) {
    sprite({ x: pos.x, z: pos.z + .3 }, .9, 1.2, EyeBlue.withAlpha(.5 * rim * alpha), glow, L - .005);
    draw(disc, pos.x, L - .003, pos.z + .18, .26, .36, 0, Violet.withAlpha(.7 * rim * alpha));
    draw(disc, pos.x, L - .003, pos.z + .58, .19, .2, 0, Violet.withAlpha(.7 * rim * alpha));
  }
  sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, .85, .4, Ink.withAlpha(strength * alpha), soft, shadowLayer);
  [[0, .18, .22, .32, Uniform], [.04, .22, .08, .24, UniformLit], [0, .58, .16, .17, Skin], [0, .69, .19, .1, Hair]]
    .forEach(([cx, cz, rx, rz, colour], k) => draw(disc, pos.x + cx, L + k * .002, pos.z + cz, rx, rz, 0, c(colour)));
  const top = L + .012, face = { x: pos.x, z: pos.z + .6 };
  const eyes = clamp(1 - blindfold);
  if (eyes > 0) [-.055, .055].forEach((dx, i) => {
    draw(disc, face.x + dx, top, face.z, .028, .022, 0, EyeBlue.withAlpha(alpha));
    sprite({ x: face.x + dx, z: face.z }, .16, .12, EyeBlue.withAlpha(.6 * eyes * alpha), glow, top + .004 + i * .0005);
  });
  // The blindfold slides from the eyes to the neck; it is not taken off.
  const band = face.z - .14 * (1 - blindfold);
  draw(MeshPool.plane10, face.x, top + .001, band, .32, .075, 0, Blindfold.withAlpha(alpha));
  // Left hand: goes up to the band's end while it is pulled down, then back to the side.
  const grab = clamp(sign * 2.5) * clamp(blindfold * 5);
  draw(MeshPool.plane10, pos.x + Mathf.Lerp(-.22, -.15, grab), top + .002, Mathf.Lerp(pos.z + .28, band, grab), .085, .1, -12 * (1 - grab), c(Skin));
  // Right hand: rises in front of the face; the index and middle fingers stand up and cross.
  const hx = pos.x + Mathf.Lerp(.22, .07, sign), hz = pos.z + Mathf.Lerp(.28, .5, sign);
  draw(MeshPool.plane10, hx, top + .004, hz, .085, .1, 12 * (1 - sign), c(Skin));
  const fingers = clamp((sign - .6) / .4);
  if (fingers > 0) [[-1, 16], [1, -16]].forEach(([k, lean], i) =>
    draw(MeshPool.plane10, hx + k * .014, top + .005 + i * .0005, hz + .05 + .045 * fingers, .024, .12 * fingers, lean * fingers, c(Skin)));
}

// The black hole that hangs over the caster (the anime's Unlimited Void horizon): a black disc, a thin
// bright accretion ring (gold-white with a green-cyan inner edge), a wide faint halo, and nebula wisps
// streaming off its east side. live 0..1 fades the whole thing. Drawn under the pawn layer by default,
// so the caster and any pawn stand in front of it as in the anime.
export function blackHole(key, centre, radius, s, live, layer = pawnLayer - .09) {
  if (radius <= .01 || live <= 0) return;
  const L = layer;
  const beat = .5 + .5 * Math.sin(s * 2.4), d = radius * 2;
  sprite(centre, d * 3.6, d * 3.6, Violet.withAlpha(.16 * live), glow, L + .00);
  sprite(centre, d * 2.4, d * 2.4, Ice.withAlpha(.22 * live), glow, L + .01);
  // Wisps: five soft lines leaving the ring on the east side, drifting east and a little north.
  for (let i = 0; i < 5; i++) {
    const a0 = (-28 + i * 14 + 6 * Math.sin(s * .7 + i)) * Mathf.Deg2Rad, len = radius * (1.4 + .9 * rand(i + 40)), pts = [];
    for (let k = 0; k <= 6; k++) {
      const v = k / 6, r = radius * (1 + .05 * k);
      pts.push({ x: centre.x + Math.cos(a0) * r + v * len, z: centre.z + Math.sin(a0) * r + v * v * len * (.25 + .2 * rand(i + 50)) * (i % 2 ? 1 : -.4) + .08 * Math.sin(s * 1.3 + i + v * 5) });
    }
    line(`${key} wisp ${i} wide`, pts, .55 + .25 * rand(i + 60), Ice.withAlpha(.14 * live), whiteGlow, L + .015, 'end');
    line(`${key} wisp ${i}`, pts, .18, White.withAlpha(.3 * live), whiteGlow, L + .016, 'end');
  }
  ringAt(centre, radius * 1.22, Ice.withAlpha(.3 * live), L + .02, true, whiteGlow);
  draw(disc, centre.x, L + .03, centre.z, radius, radius, 0, Void.withAlpha(live));
  ringAt(centre, radius * 1.03, Gold.withAlpha((.85 + .15 * beat) * live), L + .04, false, whiteGlow);
  ringAt(centre, radius * 1.075, Teal.withAlpha(.45 * live), L + .045, false, whiteGlow);
  ringAt(centre, radius * 1.11, Ice.withAlpha(.25 * live), L + .05, false, whiteGlow);
  // The ring is brightest where the light streams off it: a hot spot on the north-west.
  sprite({ x: centre.x - radius * .62, z: centre.z + radius * .68 }, radius * .9, radius * .7, Ice.withAlpha(.45 * live), glow, L + .06);
}

// The white splatter burst behind the caster at the open (the anime's first frame): a rough puff of
// light and ragged rays of uneven length. bright 0..1.
export function splatter(key, at, reach, s, bright) {
  if (bright <= 0) return;
  sprite(at, reach * 1.3, reach * 1.3, White.withAlpha(.9 * bright), puffGlow, Y + .09, 30);
  sprite(at, reach * .9, reach * .9, Ice.withAlpha(.8 * bright), puffGlow, Y + .091, 140);
  for (let i = 0; i < 16; i++) {
    const ang = (i * 22.5 + rand(i + 70) * 14) * Mathf.Deg2Rad, l = reach * (.45 + .75 * rand(i + 80)), w = .14 + .18 * rand(i + 90);
    streak(`${key} ${i}`, { x: at.x + Math.cos(ang) * .2, z: at.z + Math.sin(ang) * .2 }, { x: at.x + Math.cos(ang) * l, z: at.z + Math.sin(ang) * l },
      w, White.withAlpha(bright), whiteGlow, Y + .092, 4);
  }
}

// The light streaming through the void (the anime's speed lines): count dashes that run out from
// the centre to reach, on straight rays when swirl is 0 and on spirals otherwise (swirl is radians
// of turn per cell of radius). White, pink and violet. Positions come from s only.
export function rays(key, o, s, reach, full, swirl, alpha, count = 56) {
  if (reach <= .8 || alpha <= 0) return;
  for (let i = 0; i < count; i++) {
    const a0 = rand(i + 500) * Math.PI * 2, speed = 8 + 6 * rand(i + 510), len = 1.6 + 2.6 * rand(i + 520), cycle = full + len;
    const head = (s * speed + rand(i + 530) * cycle) % cycle, tail = head - len;
    if (head < .7 || tail > reach) continue;
    const r0 = Math.max(.7, tail), r1 = Math.min(reach, head), pts = [];
    if (r1 - r0 < .15) continue;
    for (let k = 0; k <= 6; k++) {
      const r = r0 + (r1 - r0) * k / 6, a = a0 + swirl * r;
      pts.push({ x: o.x + Math.cos(a) * r, z: o.z + Math.sin(a) * r });
    }
    const pick = rand(i + 540), colour = pick < .6 ? White : pick < .85 ? Pink : Violet, edge = 1 - clamp((r1 - full + 1.2) / 1.2) * .6;
    line(`${key} ${i}`, pts, .05 + .05 * rand(i + 550), colour.withAlpha(alpha * edge * (.6 + .4 * rand(i + 560))), whiteGlow, Floor + .016, 'both');
  }
}

// The dome as seen from above under the 0.6 lift: the north half is the projected sphere (an
// ellipse 1.166 R tall), the south half is the floor circle. Returns the outline points, counter-
// clockwise from east. Rebuilt only when the radius changes.
const Tall = Math.sqrt(1 + Lift * Lift), Sides = 64;
let domeBuilt = { radius: -1, points: [] };
export function domeOutline(radius) {
  if (domeBuilt.radius !== radius) {
    const points = [];
    for (let i = 0; i < Sides; i++) {
      const a = i / Sides * Math.PI * 2, north = Math.sin(a) >= 0;
      points.push({ x: Math.cos(a) * radius, z: Math.sin(a) * radius * (north ? Tall : 1) });
    }
    domeBuilt = { radius, points };
  }
  return domeBuilt.points;
}

// The dome skin: a faint filled outline and a brighter edge band, both light. alpha scales both.
export function domeSkin(key, o, radius, alpha, edge = .09) {
  if (radius <= .05 || alpha <= 0) return;
  const pts = domeOutline(radius), vertices = [o.x, o.z], tri = [];
  pts.forEach((q, i) => { vertices.push(o.x + q.x, o.z + q.z); tri.push(0, 1 + i, 1 + (i + 1) % pts.length); });
  const m = mesh(`${key} fill`); m.setFlat(vertices, tri);
  draw(m, 0, Y + .025, 0, 1, 1, 0, Blue.withAlpha(.10 * alpha), glow);
  const outer = pts.map(q => ({ x: o.x + q.x, z: o.z + q.z })), inner = pts.map(q => ({ x: o.x + q.x * (1 - edge / radius), z: o.z + q.z * (1 - edge / radius) }));
  outer.push(outer[0]); inner.push(inner[0]);
  strip(`${key} edge`, outer, inner, Ice.withAlpha(.32 * alpha), whiteGlow, Y + .026);
}

// A frozen victim's overload: a white glow at the head and thin streaks of light flickering up from
// it. s drives the flicker; alpha fades the whole thing.
export function overload(key, pos, s, alpha, count = 5) {
  if (alpha <= 0) return;
  const head = { x: pos.x, z: pos.z + .6 };
  sprite(head, .55, .55, Ice.withAlpha(.55 * alpha), glow, Y + .04);
  for (let i = 0; i < count; i++) {
    const flick = .45 + .55 * Math.abs(Math.sin(s * 23 + i * 7.3)), ang = (62 + i * (56 / Math.max(1, count - 1)) + (rand(i + 3) - .5) * 16) * Mathf.Deg2Rad;
    const r0 = .14, r1 = .32 + .38 * rand(i + 9) * (.7 + .3 * Math.sin(s * 5 + i));
    streak(`${key} ${i}`, { x: head.x + Math.cos(ang) * r0, z: head.z + Math.sin(ang) * r0 }, { x: head.x + Math.cos(ang) * r1, z: head.z + Math.sin(ang) * r1 },
      .035, White.withAlpha(alpha * flick), whiteGlow, Y + .041, 3);
  }
}
