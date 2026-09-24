// Pain kit: the drawing pieces shared by the Banshō Ten'in and Black Receiver sketches. Not a sketch
// itself, so it is not listed in sketches/index.js.
//
// Pain's stand-in (two discs in the black cloak with two red clouds and spiky orange hair), his arm as
// the ability draws it (a sleeve that narrows to a grey cuff and an open hand whose fingers can close),
// and the stand-in pawn standing, lying face-down, and Pain himself downed. Everything is a disc, a
// quad or a strip lying level, so nothing needs a per-facing method; every function takes positions
// and keeps no state. Mesh keys must be unique within a frame: pass a key that names the sketch and
// the part.
import { AltitudeLayer, Color, Mathf, Meshes } from '../../js/engine.js';
import { draw, mesh } from './six-paths-solid.js';
import { sprite, band, soft } from './six-paths-impact.js';
import { line, Skin, Ink } from './goku.js';
import { rect } from './chain-sickle.js';

const lerp = Mathf.Lerp, D2R = Mathf.Deg2Rad;
const disc = Meshes.disc(40, 'pain disc');
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const lyingLayer = AltitudeLayer.LayingPawn.AltitudeFor();

// The kit's colours: Gravity Well's black core and pale blue rim, Shinra Tensei's blue-white.
export const Core = new Color(.015, .015, .035), PaleBlue = new Color(.85, .94, 1), DustC = new Color(.76, .70, .59);
export const Cloak = new Color(.07, .065, .085), Cloud = new Color(.74, .1, .12), CloudEdge = new Color(.95, .93, .9), Hair = new Color(.93, .46, .16);
export const Cuff = new Color(.22, .21, .24);
export const BodyZ = .18;                        // a stand-in pawn's body centre above its ground point on screen
export const ShoulderH = .52, HandH = .56, Reach = .48;   // Pain's arm: cells up, and how far the hand goes out

// A filled polygon, fanned from its first point.
export function poly(key, pts, colour, layer, material) {
  const v = [], tri = [];
  pts.forEach(q => v.push(q.x, q.z));
  for (let i = 1; i < pts.length - 1; i++) tri.push(0, i, i + 1);
  const m = mesh(key); m.setFlat(v, tri);
  draw(m, 0, layer, 0, 1, 1, 0, colour, material);
}

// Pain: the two-disc pawn in the black cloak with two red clouds, spiky orange hair.
export function pain(g, sun, strength) {
  sprite({ x: g.x + sun.x * .45, z: g.z + sun.z * .45 }, .85, .4, Ink.withAlpha(strength), soft, shadowLayer);
  draw(disc, g.x, pawnLayer, g.z + BodyZ, .22, .32, 0, Cloak);
  [[-.08, .1, .07, .045], [.075, .28, .06, .04]].forEach(([dx, dz, rx, rz], i) => {
    draw(disc, g.x + dx, pawnLayer + .0005 + i * .0002, g.z + dz, rx + .016, rz + .016, 0, CloudEdge);
    draw(disc, g.x + dx, pawnLayer + .0006 + i * .0002, g.z + dz, rx, rz, 0, Cloud);
  });
  draw(disc, g.x, pawnLayer + .002, g.z + .58, .16, .17, 0, Skin);
  for (let i = 0; i < 5; i++) {
    const deg = -60 + i * 30, a = deg * D2R;
    draw(disc, g.x + Math.sin(a) * .14, pawnLayer + .003, g.z + .67 + Math.cos(a) * .1, .045, .08, deg, Hair);
  }
  draw(disc, g.x, pawnLayer + .0032, g.z + .69, .16, .085, 0, Hair);
}
// Pain downed: on the floor with his head toward `toward`, the cloak and one cloud showing, hair round the head.
export function painDown(key, g, toward, sun, strength) {
  sprite({ x: g.x + sun.x * .15, z: g.z + sun.z * .15 }, 1, .5, Ink.withAlpha(strength), soft, shadowLayer);
  const c = { x: g.x, z: g.z + .08 }, deg = Math.atan2(toward.x, toward.z) / D2R, px = -toward.z, pz = toward.x;
  line(`${key} arm`, [{ x: c.x + toward.x * .18 + px * .12, z: c.z + toward.z * .18 + pz * .12 }, { x: c.x + toward.x * .05 + px * .3, z: c.z + toward.z * .05 + pz * .3 }],
    .07, Cloak, undefined, lyingLayer - .001, 'none');
  draw(disc, c.x, lyingLayer, c.z, .21, .34, deg, Cloak);
  draw(disc, c.x - toward.x * .06 + px * .05, lyingLayer + .0005, c.z - toward.z * .06 + pz * .05, .07, .05, deg, CloudEdge);
  draw(disc, c.x - toward.x * .06 + px * .05, lyingLayer + .0006, c.z - toward.z * .06 + pz * .05, .055, .038, deg, Cloud);
  const head = { x: c.x + toward.x * .4, z: c.z + toward.z * .4 };
  for (let i = 0; i < 5; i++) {
    const a = Math.atan2(toward.z, toward.x) + (i - 2) * .55;
    draw(disc, head.x + Math.cos(a) * .12, lyingLayer + .0015, head.z + Math.sin(a) * .12, .045, .08, 90 - a / D2R, Hair);
  }
  draw(disc, head.x, lyingLayer + .002, head.z, .15, .16, 0, Skin);
}
export function standing(g, colour, sun, strength, alpha = 1) {
  sprite({ x: g.x + sun.x * .45, z: g.z + sun.z * .45 }, .85, .4, Ink.withAlpha(strength * alpha), soft, shadowLayer);
  draw(disc, g.x, pawnLayer, g.z + BodyZ, .22, .32, 0, colour.withAlpha(alpha));
  draw(disc, g.x, pawnLayer + .002, g.z + .58, .16, .17, 0, Skin.withAlpha(alpha));
}
// Face-down on the floor, head toward `toward`, arms out to the sides.
export function lying(key, g, colour, toward, sun, strength, alpha = 1) {
  sprite({ x: g.x + sun.x * .15, z: g.z + sun.z * .15 }, 1, .5, Ink.withAlpha(strength * alpha), soft, shadowLayer);
  const px = -toward.z, pz = toward.x, c = { x: g.x, z: g.z + .08 };
  for (const side of [-1, 1]) {
    const sh = { x: c.x + toward.x * .2 + px * side * .12, z: c.z + toward.z * .2 + pz * side * .12 };
    line(`${key} arm ${side}`, [sh, { x: sh.x + toward.x * .16 + px * side * .2, z: sh.z + toward.z * .16 + pz * side * .2 }], .07, Skin.withAlpha(alpha), undefined, lyingLayer - .001, 'none');
  }
  draw(disc, c.x, lyingLayer, c.z, .21, .34, Math.atan2(toward.x, toward.z) / D2R, colour.withAlpha(alpha));
  draw(disc, c.x + toward.x * .4, lyingLayer + .002, c.z + toward.z * .4, .15, .16, 0, Skin.withAlpha(alpha));
}
// Pain's arm, drawn by the ability: a sleeve in the cloak's colour that narrows from the shoulder to the
// wrist, a grey cuff, and an open hand, a palm with the thumb and four fingers spread at the target (the
// anime's pose). grip 0..1 closes the fingers (round a head, or round a rod). dir points the way the
// fingers point. The hand lies level at its height, so it turns with the aim.
export function arm(key, shoulder, hand, dir, grip) {
  const dx = hand.x - shoulder.x, dz = hand.z - shoulder.z, L = Math.hypot(dx, dz) || 1, ux = dx / L, uz = dz / L, px = -uz, pz = ux;
  const wrist = { x: hand.x - ux * .045, z: hand.z - uz * .045 }, w0 = .065, w1 = .042;
  band(`${key} sleeve`, [{ x: shoulder.x + px * w0, z: shoulder.z + pz * w0 }, { x: wrist.x + px * w1, z: wrist.z + pz * w1 }],
    [{ x: shoulder.x - px * w0, z: shoulder.z - pz * w0 }, { x: wrist.x - px * w1, z: wrist.z - pz * w1 }], Cloak, pawnLayer + .01);
  rect(`${key} cuff`, wrist, .03, w1 * 2.3, Math.atan2(uz, ux) / D2R, Cuff, pawnLayer + .0102);
  const base = Math.atan2(dir.z, dir.x) / D2R, spread = lerp(64, 30, grip), reach = lerp(1, .6, grip);
  // [angle as a share of the spread, length]: the thumb out to one side, the middle fingers longest.
  [[-1.2, .09], [-.5, .125], [-.17, .14], [.17, .13], [.5, .105]].forEach(([k, len], i) => {
    const ang = (base + k * spread) * D2R, cx = Math.cos(ang), cz = Math.sin(ang), root = { x: hand.x + cx * .04, z: hand.z + cz * .04 };
    line(`${key} finger ${i}`, [root, { x: root.x + cx * len * reach, z: root.z + cz * len * reach }], .034, Skin, undefined, pawnLayer + .0106, 'none');
  });
  draw(disc, hand.x, pawnLayer + .0108, hand.z, .062, .062, 0, Skin);
}
