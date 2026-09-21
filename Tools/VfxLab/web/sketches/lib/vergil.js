// Vergil kit: the drawing pieces shared by the kit's sketches. Not a sketch itself, so it is not
// listed in sketches/index.js.
//
// Everything here is a quad, a level circle, a line between two points or a flat polygon, so nothing
// needs a per-facing method. Every function takes ages and times and keeps no state. Mesh keys must
// be unique within a frame: pass a key that names the sketch and the part.
//
// Port notes: quads (MeshPool.plane10) with the white texture or SoftDisc, and strip meshes rebuilt
// while they show. A cut is light, not a solid: a dark slit on the floor side, a wide additive glow
// and a thin white core. Blue-white with a dark blue slit, so it is not read as Six Paths violet,
// Flying Thunder God yellow or Anchor blue. pawn, ringAt, line, glint and aura are the Goku kit's.
import { Color, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../../js/engine.js';
import { registerLabTexture, pixels } from '../../js/standins.js';
import { draw } from './six-paths-solid.js';
import { Y, sprite, glow, soft, rand } from './six-paths-impact.js';
import { Chest, Dust, pawn, ringAt, line, glint, aura, streak, strip, whiteGlow, wallCell, Ink, Skin, EnemyColour, Ally, White, pawnLayer, shadowLayer, smooth, clamp } from './goku.js';
export { Chest, Dust, pawn, ringAt, line, glint, aura, streak, strip, whiteGlow, wallCell, Ink, Skin, EnemyColour, Ally, White, pawnLayer, shadowLayer, smooth, clamp };

export const Blue = new Color(.25, .5, 1), Deep = new Color(.05, .12, .5), Ice = new Color(.78, .9, 1), Void = new Color(.01, .015, .07);
export const Coat = new Color(.09, .14, .4), CoatLit = new Color(.2, .3, .62), Silver = new Color(.88, .9, .95);
export const Scabbard = new Color(.04, .04, .07), Steel = new Color(.9, .95, 1), Guard = new Color(.85, .7, .3);
const BladeLength = .75, ScabbardLength = .72;

const discMesh = Meshes.disc(32, 'vergil disc');

// The kit's carrier as a stand-in: blue coat, silver hair, a katana in a dark scabbard.
//   pose 'stand'  the scabbard hangs at the left hip; hand 0..1 moves the right hand to the hilt
//   pose 'kneel'  back to the camera, one knee down, the scabbard upright in the left hand; sheathe
//                 0..1 slides the blade into it from above (0 drawn, 1 home)
// The kneel has one view only. In game it needs a pose per facing; this is a stand-in.
export function carrier(pos, sun, strength, { alpha = 1, pose = 'stand', hand = 0, sheathe = 1, tint = null, tintAmount = 0 } = {}) {
  if (alpha <= 0) return;
  const c = q => (tint ? Color.Lerp(q, tint, tintAmount) : q).withAlpha(alpha * q.a);
  const kneel = pose === 'kneel';
  sprite({ x: pos.x + sun.x * (kneel ? .3 : .45), z: pos.z + sun.z * (kneel ? .3 : .45) }, .85, .4, Ink.withAlpha(strength * alpha), soft, shadowLayer);
  const parts = kneel
    ? [[0, -.02, .34, .13, Coat], [0, .14, .23, .23, Coat], [.02, .2, .1, .16, CoatLit], [0, .44, .16, .16, Skin], [0, .47, .175, .15, Silver]]
    : [[0, .18, .22, .32, Coat], [.03, .22, .08, .24, CoatLit], [0, .58, .16, .17, Skin], [0, .69, .19, .1, Silver]];
  parts.forEach(([cx, cz, rx, rz, colour], k) => draw(discMesh, pos.x + cx, pawnLayer + k * .002, pos.z + cz, rx, rz, 0, c(colour)));
  const top = pawnLayer + .012;
  if (!kneel) {
    // Scabbard across the left hip, hilt forward. The right hand crosses to it.
    draw(MeshPool.plane10, pos.x - .2, top, pos.z + .2, .055, ScabbardLength, 38, c(Scabbard));
    draw(MeshPool.plane10, pos.x - .02, top + .001, pos.z + .46, .045, .2, 38, c(Guard));
    draw(MeshPool.plane10, pos.x + .2 - .2 * hand, top + .002, pos.z + .3 + .14 * hand, .08, .1, 20, c(Skin));
    return;
  }
  // Scabbard upright in the left hand; the blade comes down into its mouth.
  const mouth = { x: pos.x - .3, z: pos.z + .46 }, out = BladeLength * (1 - sheathe);
  draw(MeshPool.plane10, mouth.x, top, mouth.z - ScabbardLength / 2, .055, ScabbardLength, 0, c(Scabbard));
  if (out > .005) {
    draw(MeshPool.plane10, mouth.x, top + .001, mouth.z + out / 2, .03, out, 0, c(Steel));
    sprite({ x: mouth.x, z: mouth.z + out / 2 }, .16, out + .2, Ice.withAlpha(.5 * alpha), glow, Y + .02);
  }
  draw(MeshPool.plane10, mouth.x, top + .002, mouth.z + out + .015, .11, .03, 0, c(Guard));
  draw(MeshPool.plane10, mouth.x, top + .002, mouth.z + out + .12, .04, .2, 0, c(Scabbard));
  draw(MeshPool.plane10, mouth.x + .02, top + .003, mouth.z + out + .1, .08, .1, 0, c(Skin));
  return mouth;
}

// Where the kneeling carrier's scabbard mouth is: the click happens here.
export const scabbardMouth = pos => ({ x: pos.x - .3, z: pos.z + .46 });

// A ball of cut space, seen from above: a round shape with a soft edge, so it is drawn with two
// textures instead of a flat disc and a ring. Both are lab textures and still have to be ported to
// Textures/RimArt/Vergil by a make_vergil_textures.py: Ball (opaque, soft over the last 20 % of the
// radius) and Shell (clear in the middle, alpha rising as radius^3.2 toward the rim, soft over the last 7 %).
const ease = (a, b, x) => { const v = Math.min(1, Math.max(0, (x - a) / (b - a))); return v * v * (3 - 2 * v); };
registerLabTexture('lab/vergil-ball', () => pixels(256, (u, v) => { const r = Math.hypot(u - .5, v - .5) * 2; return [1, 1, 1, 1 - ease(.8, 1, r)]; }));
registerLabTexture('lab/vergil-shell', () => pixels(256, (u, v) => { const r = Math.hypot(u - .5, v - .5) * 2; return [1, 1, 1, r >= 1 ? 0 : Math.pow(r, 3.2) * (1 - ease(.93, 1, r))]; }));
const ballMat = MaterialPool.MatFrom('lab/vergil-ball', ShaderDatabase.Transparent), shellMat = MaterialPool.MatFrom('lab/vergil-shell', ShaderDatabase.MoteGlow);
// live 0..1 fades the whole thing; dark is how much the inside hides (0 leaves only the light).
export function sphere(centre, radius, s, live, dark = .45) {
  if (radius <= 0 || live <= 0) return;
  const beat = .5 + .5 * Math.sin(s * 42), d = radius * 2;
  sprite(centre, d * 1.7, d * 1.7, Blue.withAlpha(.28 * live), glow, Y + .015);
  if (dark > 0) {
    sprite(centre, d, d, Void.withAlpha(dark * live), ballMat, Y + .016);
    sprite(centre, d * .7, d * .7, Void.withAlpha(dark * 1.2 * live), soft, Y + .0165);
  }
  sprite(centre, d * 1.02, d * 1.02, Blue.withAlpha(.95 * live), shellMat, Y + .017);
  sprite(centre, d * 1.02, d * 1.02, Ice.withAlpha(.45 * beat * live), shellMat, Y + .0175);
  sprite({ x: centre.x - radius * .4, z: centre.z + radius * .45 }, radius * .9, radius * .7, Ice.withAlpha(.22 * live), glow, Y + .018);   // the side toward the viewer's light
  ringAt(centre, radius, Ice.withAlpha((.45 + .4 * beat) * live), Y + .019, false, whiteGlow);
}

// A cut hanging in the air from a to b, drawn up to share u (0..1) of its length. Three layers: a
// dark slit, a wide glow, a thin white core. hot 0..1 turns the whole thing white (the click).
export function cut(key, a, b, u, alpha, { width = .05, hot = 0, flicker = 1 } = {}) {
  if (u <= 0 || alpha <= 0) return;
  const tip = { x: a.x + (b.x - a.x) * u, z: a.z + (b.z - a.z) * u };
  streak(`${key} slit`, a, tip, width * 2.2, Void.withAlpha(.7 * alpha * (1 - hot)), undefined, Y + .03, 10);
  streak(`${key} glow`, a, tip, width * (7 + 4 * hot), Color.Lerp(Blue, Ice, hot).withAlpha((.34 + .2 * hot) * alpha * flicker), whiteGlow, Y + .031, 10);
  streak(`${key} core`, a, tip, width * (1 + .8 * hot), White.withAlpha(alpha * flicker), whiteGlow, Y + .032, 10);
  return tip;
}

// A cut along a list of points (a curve), drawn up to share u of its length, thin at both ends. Same
// three layers as cut.
export function arcCut(key, pts, u, alpha, { width = .05, hot = 0 } = {}) {
  if (u <= 0 || alpha <= 0 || pts.length < 2) return;
  const upto = u * (pts.length - 1), whole = Math.floor(upto), part = upto - whole, shown = pts.slice(0, whole + 1);
  if (part > .001 && whole + 1 < pts.length) shown.push({ x: pts[whole].x + (pts[whole + 1].x - pts[whole].x) * part, z: pts[whole].z + (pts[whole + 1].z - pts[whole].z) * part });
  if (shown.length < 2) return;
  line(`${key} slit`, shown, width * 2.4, Void.withAlpha(.75 * alpha * (1 - hot)), undefined, Y + .03, 'both');
  line(`${key} glow`, shown, width * (5 + 2 * hot), Color.Lerp(Blue, Ice, hot).withAlpha((.4 + .2 * hot) * alpha), whiteGlow, Y + .031, 'both');
  line(`${key} core`, shown, width * (1.2 + .8 * hot), White.withAlpha(alpha), whiteGlow, Y + .032, 'both');
}

// A blade outline from base along d: parallel edges for the first 70 % of its length, then a point.
// The same shape the summoned swords use, so every blade in the kit is drawn alike.
export function tapered(key, base, d, length, width, colour, material, layer) {
  const a = [], b = [];
  [0, .35, .7, .88, 1].forEach(u => {
    const w = width / 2 * (u <= .7 ? 1 : (1 - u) / .3) + .004, x = base.x + d.x * length * u, z = base.z + d.z * length * u;
    a.push({ x: x - d.z * w, z: z + d.x * w }); b.push({ x: x + d.z * w, z: z - d.x * w });
  });
  strip(key, a, b, colour, material, layer);
}

// The katana out of its scabbard and held in front of the carrier, pointing along deg: a dark
// under-edge so it reads on pale ground, a steel blade, a gold guard, a dark grip and the hand on it.
// out 0..1 is how much blade is clear of the scabbard (the carrier keeps wearing the empty scabbard).
// hot 0..1 lights the edge, for the draw and the cut. Everything lies flat and turns with the aim, so
// there is no per-facing method; the height is the fixed northward Chest offset. A blade pointing
// north draws under the pawn layer and one pointing south over it, the rule the summoned swords use,
// or aiming north puts the guard and the hand on top of the carrier's own head.
// Returns the grip and the tip, for glints.
export function heldKatana(key, pos, deg, out, { alpha = 1, hot = 0, layer = null } = {}) {
  if (alpha <= 0) return null;
  const r = deg * Mathf.Deg2Rad, d = { x: Math.cos(r), z: Math.sin(r) };
  if (layer === null) layer = d.z > 0 ? pawnLayer - .02 : pawnLayer + .02;
  const grip = { x: pos.x + d.x * .22, z: pos.z + Chest + d.z * .22 }, len = BladeLength * out;
  const at = (along, side = 0) => ({ x: grip.x + d.x * along - d.z * side, z: grip.z + d.z * along + d.x * side });
  streak(`${key} hilt`, at(-.26), at(-.02), .07, Scabbard.withAlpha(alpha), undefined, layer, 2);
  if (len > .01) {
    tapered(`${key} edge`, at(.02), d, len, .105, Void.withAlpha(.55 * alpha), undefined, layer + .001);
    tapered(`${key} blade`, at(.02), d, len, .075, Color.Lerp(Steel, White, hot).withAlpha(alpha), undefined, layer + .002);
    if (hot > 0) {
      sprite(at(len * .5), len * 1.3, .4 * hot + .08, Ice.withAlpha(.55 * hot * alpha), glow, layer + .003, -deg);
      tapered(`${key} sharp`, at(.02), d, len, .028, White.withAlpha(hot * alpha), whiteGlow, layer + .004);
    }
  }
  streak(`${key} guard`, at(0, -.11), at(0, .11), .05, Guard.withAlpha(alpha), undefined, layer + .005, 2);
  sprite(grip, .12, .13, Skin.withAlpha(alpha), soft, layer + .006);
  return { grip, tip: at(Math.max(.02, len)) };
}

// The carrier seen for a moment behind a dash: a blue silhouette built from the same ellipses the
// standing carrier is, with one thin line of light tapering back along the dash. It is a light and
// not a solid, so it has no shadow and carries no weapon.
export function afterimage(key, pos, from, age, life = .22) {
  if (age < 0 || age >= life) return;
  const f = 1 - age / life, dx = pos.x - from.x, dz = pos.z - from.z, d = Math.hypot(dx, dz) || 1;
  const head = { x: pos.x, z: pos.z + Chest }, back = { x: head.x - dx / d * .95, z: head.z - dz / d * .95 };
  line(`${key} trail`, [head, back], .18, Blue.withAlpha(.55 * f * f), whiteGlow, Y + .02, 'end');
  [[0, .18, .22, .32, Deep], [.03, .22, .08, .24, Blue], [0, .58, .16, .17, Deep], [0, .69, .19, .1, Ice]].forEach(([cx, cz, rx, rz, colour], k) =>
    draw(discMesh, pos.x + cx, Y + .021 + k * .001, pos.z + cz, rx, rz, 0, Color.Lerp(colour, Ice, .3).withAlpha(.62 * f * f)));
}

// A short cut across a pawn's chest when the damage lands: light, 0.14 s, plus a spark.
export function hitCut(key, victim, deg, age, life = .14) {
  if (age < 0 || age >= life) return;
  const u = age / life, r = deg * Mathf.Deg2Rad, l = .62, mid = { x: victim.x, z: victim.z + .32 };
  const a = { x: mid.x - Math.cos(r) * l, z: mid.z - Math.sin(r) * l }, b = { x: mid.x + Math.cos(r) * l, z: mid.z + Math.sin(r) * l };
  cut(key, a, b, clamp(u * 3), 1 - u * u, { width: .025, hot: .5 * (1 - u) });
  sprite(mid, .5 * (1 - u) + .25, .5 * (1 - u) + .25, Ice.withAlpha(.5 * (1 - u)), glow, Y + .04);
}

// Convex polygons left when a disc of the given radius is cut by straight lines. lines are
// [{ q: {x, z}, d: {x, z} }] (a point and a unit direction), all relative to the disc centre. Returns
// [{ points, centre, area }]; points are relative to centre, counter-clockwise.
export function shatter(radius, lines, sides = 40) {
  let pieces = [Array.from({ length: sides }, (_, i) => ({ x: Math.cos(i / sides * Math.PI * 2) * radius, z: Math.sin(i / sides * Math.PI * 2) * radius }))];
  lines.forEach(({ q, d }) => {
    const next = [];
    pieces.forEach(poly => {
      const side = poly.map(v => (v.x - q.x) * -d.z + (v.z - q.z) * d.x), left = [], right = [];
      poly.forEach((v, i) => {
        const w = poly[(i + 1) % poly.length], sv = side[i], sw = side[(i + 1) % poly.length];
        (sv >= 0 ? left : right).push(v);
        if ((sv >= 0) !== (sw >= 0)) { const k = sv / (sv - sw), m = { x: v.x + (w.x - v.x) * k, z: v.z + (w.z - v.z) * k }; left.push(m); right.push(m); }
      });
      [left, right].forEach(part => { if (part.length >= 3) next.push(part); });
    });
    pieces = next;
  });
  return pieces.map(poly => {
    let area = 0, cx = 0, cz = 0;
    poly.forEach((v, i) => { const w = poly[(i + 1) % poly.length], k = v.x * w.z - w.x * v.z; area += k; cx += (v.x + w.x) * k; cz += (v.z + w.z) * k; });
    area /= 2;
    const centre = Math.abs(area) < 1e-6 ? poly[0] : { x: cx / (6 * area), z: cz / (6 * area) };
    return { centre, area: Math.abs(area), points: poly.map(v => ({ x: v.x - centre.x, z: v.z - centre.z })) };
  }).filter(piece => piece.area > .02);
}

// The two ends of the chord a line makes across a disc of the given radius (centre at 0, 0).
export function chord(q, d, radius) {
  const b = q.x * d.x + q.z * d.z, c = q.x * q.x + q.z * q.z - radius * radius, root = Math.sqrt(Math.max(0, b * b - c));
  return [{ x: q.x + d.x * (-b - root), z: q.z + d.z * (-b - root) }, { x: q.x + d.x * (-b + root), z: q.z + d.z * (-b + root) }];
}
export { rand };
