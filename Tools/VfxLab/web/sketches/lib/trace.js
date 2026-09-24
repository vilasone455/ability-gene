// Trace kit: the planted-blade drawing shared by the Trace sketches (the Origin: Blade rework). Not
// a sketch itself, so it is not listed in sketches/index.js.
//
// A blade is a weapon's own texture on a flat plane in 3D. Its tip, pommel and width along the
// axis come from the texture's alpha (make_trace_trial_textures.py prints them). The plane gets a
// lean and a turn, is drawn with the kit's height rule (0.60 cells north per cell up) and is cut
// where it meets the ground, so the buried part is not drawn. The same plane cast along the sun is
// the shadow, which therefore starts at the hole. On the floor, plant() draws the mark a blade
// leaves where it goes in. The trace look is blade()'s wire, fill and scan options: an outline
// drawn below a height, steel drawn below a height, and a bright line at a height.
//
// Textures: RimArt/TraceTrial/<Name>, <Name>Outline, <Name>Mask are local reference copies of
// vanilla and modded weapons, git-excluded; make_trace_trial_textures.py rebuilds them. In game the
// kit reads each weapon's own graphic.
//
// Port notes:
//   - one custom mesh per blade part: the texture square cut by one or two height planes, at most
//     6 corners, uv carried through, wound clockwise like MeshPool.plane10.
//   - a blade whose flat side turns toward north-south collapses into a line (height and north
//     share the screen axis); keep the face turn within about 30 degrees of east-west. A blade
//     leaning toward the camera shortens on screen and at about 60 degrees vanishes into its cut.
//   - blades draw at the Building layer, under pawns, as a planted blade (a Building) does in game.
import { Color, MaterialPool, Mathf, ShaderDatabase } from '../../js/engine.js';
import { draw, mesh } from './six-paths-solid.js';
import { Y, Floor, Lift, sprite, soft, glow, rand, band } from './six-paths-impact.js';
import { rock, line, shadowLayer, Dust } from './goku.js';

export const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, D2R = Math.PI / 180;
export const solid = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
export const Hole = new Color(.05, .04, .03), Black = new Color(0, 0, 0);
export const DirtDark = new Color(.2, .14, .09), DirtMid = new Color(.36, .27, .18), DirtLit = new Color(.55, .43, .3);
export const Trace = new Color(.35, 1, .82), TraceHot = new Color(.82, 1, .95);
export const grey = g => new Color(g, g, g);
// A grey, or with tint that grey times the tint: the light of the Unlimited Blade Works world, as its
// baked field is drawn (lib/ubw-pocket.js drawField).
const shade = (g, tint) => tint ? new Color(g * tint.r, g * tint.g, g * tint.b) : grey(g);
const tinted = (c, tint) => tint ? new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a) : c;

// Per texture, from its alpha: tip and pommel in uv (v up), and the blade's extent across its axis
// in 16 steps from the tip (0) to the pommel (1), in uv. image: cells the whole picture spans at
// size 1, picked so the lengths compare the way the weapons do.
export const Weapons = {
  Knife: { image: 1.0, tip: [.8337, .4964], pommel: [.2792, .5031], width: [[-.0251, .0065], [-.0247, .0265], [-.0356, .0464], [-.0586, .0586], [-.0661, .0629], [-.0659, .0633], [-.0652, .0638], [-.0651, .0642], [-.0645, .0646], [-.0835, .0806], [-.0834, .081], [-.083, .0811], [-.0435, .0428], [-.0584, .0549], [-.0583, .0593], [-.054, .0594]] },
  LongSword: { image: 1.2, tip: [.9668, .531], pommel: [.0176, .5312], width: [[-.0451, .0447], [-.0646, .0682], [-.0685, .0682], [-.0685, .0682], [-.0685, .0682], [-.0685, .0682], [-.0685, .0682], [-.0685, .0682], [-.0685, .0683], [-.0685, .0683], [-.0685, .0683], [-.1114, .1113], [-.1114, .1113], [-.0528, .0488], [-.0645, .0683], [-.0645, .0683]] },
  Spear: { image: 1.65, tip: [.9981, .5031], pommel: [.0174, .4988], width: [[-.042, .0439], [-.0618, .0632], [-.0698, .0631], [-.0583, .0551], [-.0431, .047], [-.0434, .0467], [-.0437, .0464], [-.044, .0462], [-.0442, .0459], [-.0445, .0456], [-.0448, .0453], [-.0451, .0451], [-.0453, .0448], [-.0456, .0445], [-.0459, .0442], [-.0461, .044]] },
  MonoSword: { image: 1.18, tip: [.9824, .5931], pommel: [.0303, .3687], width: [[-.1329, -.0084], [-.1135, .0531], [-.088, .0871], [-.0719, .1059], [-.0646, .1041], [-.0599, .1038], [-.0595, .1103], [-.0651, .1118], [-.0718, .1105], [-.0862, .1055], [-.1043, .0939], [-.1265, .0936], [-.1213, .0956], [-.1149, .0623], [-.0536, .0403], [-.0599, .026]] },
  LargeSword: { image: 1.22, tip: [.9169, .9035], pommel: [.0484, .0336], width: [[-.1393, .1396], [-.1394, .1396], [-.1395, .1396], [-.1395, .1395], [-.1396, .1394], [-.1397, .1394], [-.1397, .1393], [-.1398, .1393], [-.1399, .1392], [-.1399, .1391], [-.14, .1391], [-.1649, .1666], [-.0296, .0312], [-.0352, .0367], [-.0353, .0365], [-.0353, .0365]] },
  Wyrmslayer: { image: 1.3, tip: [.9818, .9792], pommel: [.0791, .0534], width: [[-.0746, .0718], [-.1199, .1205], [-.1264, .1251], [-.1301, .1297], [-.1339, .1343], [-.1405, .1389], [-.1443, .1458], [-.1508, .1504], [-.1546, .155], [-.1584, .1596], [-.165, .1642], [-.1687, .1711], [-.1747, .1735], [-.0437, .0982], [-.0503, .041], [-.0589, .0543]] },
};
for (const [name, w] of Object.entries(Weapons)) {
  const du = w.pommel[0] - w.tip[0], dv = w.pommel[1] - w.tip[1], len = Math.hypot(du, dv);
  Object.assign(w, {
    name, length: len, axis: { u: du / len, v: dv / len }, across: { u: -dv / len, v: du / len },
    face: MaterialPool.MatFrom(`RimArt/TraceTrial/${name}`, ShaderDatabase.Transparent),
    wire: MaterialPool.MatFrom(`RimArt/TraceTrial/${name}Outline`, ShaderDatabase.MoteGlow),
    mask: MaterialPool.MatFrom(`RimArt/TraceTrial/${name}Mask`, ShaderDatabase.MoteGlow),
  });
}

// Plain 3D. y is height above the floor.
export const v3 = (x, y, z) => ({ x, y, z });
export const plus = (a, b, f = 1) => v3(a.x + b.x * f, a.y + b.y * f, a.z + b.z * f);
export const dot3 = (a, b) => a.x * b.x + a.y * b.y + a.z * b.z;
export const cross = (a, b) => v3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
export const unit = a => { const l = Math.hypot(a.x, a.y, a.z) || 1; return v3(a.x / l, a.y / l, a.z / l); };
export const Up = v3(0, 1, 0), View = unit(v3(0, 1, -Lift));
export const onScreen = q => ({ x: q.x, z: q.z + q.y * Lift });
export const alongSun = sun => q => ({ x: q.x + sun.x * q.y, z: q.z + sun.z * q.y });

// A blade as a plane: tip point, A from tip to pommel, B across, N out of the face toward the
// camera, scale in cells per uv unit, L its length in cells.
export function pose(w, scale, tip, A, across) {
  const B = unit(plus(across, A, -dot3(across, A)));
  let N = unit(cross(A, B));
  if (dot3(N, View) < 0) N = v3(-N.x, -N.y, -N.z);
  return { w, scale, tip, A, B, N, L: w.length * scale };
}
// The tip of a blade whose axis enters the ground at g with `buried` cells of it underground.
export const tipUnder = (g, A, buried) => v3(g.x - A.x * buried, -A.y * buried, g.z - A.z * buried);
// A blade standing in the ground at g: lean from upright toward dir (degrees, 0 east, 90 north),
// its flat side turned by turn degrees from east-west, sink of its length underground. raise 0 has
// the pommel at the floor, 1 is standing: in between it comes up hilt first.
export function upright(w, size, g, lean, dir, turn, sink, raise = 1) {
  const scale = w.image * size, L = w.length * scale, l = lean * D2R, d = dir * D2R, t = turn * D2R;
  const A = v3(Math.sin(l) * Math.cos(d), Math.cos(l), Math.sin(l) * Math.sin(d));
  return pose(w, scale, tipUnder(g, A, sink * L + (1 - raise) * (1 - sink) * L), A, v3(Math.cos(t), 0, Math.sin(t)));
}
// A blade in flight, point first along dir (a unit direction on the floor), flat side up, tip at
// the 3D point tip; the pommel rides pitch degrees above the tip. This is the texture as the game
// draws an item: seen from above.
export function flying(w, size, tip, dir, pitch = 8) {
  const p = pitch * D2R, A = v3(-dir.x * Math.cos(p), Math.sin(p), -dir.z * Math.cos(p));
  return pose(w, w.image * size, tip, A, v3(-dir.z, 0, dir.x));
}
// A blade stuck in the ground at g, its top leaning lean degrees toward `toward` (a unit direction
// on the floor), flat side turned to the camera, `buried` cells of it underground.
export function stuck(w, size, g, toward, lean, buried) {
  const l = lean * D2R, A = v3(toward.x * Math.sin(l), Math.cos(l), toward.z * Math.sin(l));
  return pose(w, w.image * size, tipUnder(g, A, buried), A, v3(1, 0, 0));
}
// Part way (u 0..1) from pose a to pose b of the same weapon: tip, axis and flat side blended.
export function blend(a, b, u) {
  const mix = (p, q) => v3(lerp(p.x, q.x, u), lerp(p.y, q.y, u), lerp(p.z, q.z, u));
  const B = dot3(a.B, b.B) < 0 ? v3(-b.B.x, -b.B.y, -b.B.z) : b.B;
  return pose(a.w, a.scale, mix(a.tip, b.tip), unit(mix(a.A, b.A)), mix(a.B, B));
}
// Texture point q = { u, v } of blade b, in 3D.
export function at3(b, q) {
  const du = q.u - b.w.tip[0], dv = q.v - b.w.tip[1];
  const s = (du * b.w.axis.u + dv * b.w.axis.v) * b.scale, c = (du * b.w.across.u + dv * b.w.across.v) * b.scale;
  return v3(b.tip.x + b.A.x * s + b.B.x * c, b.tip.y + b.A.y * s + b.B.y * c, b.tip.z + b.A.z * s + b.B.z * c);
}
export const pommelOf = b => at3(b, { u: b.w.pommel[0], v: b.w.pommel[1] });
export const Square = [{ u: 0, v: 0 }, { u: 1, v: 0 }, { u: 1, v: 1 }, { u: 0, v: 1 }];
// The polygon cut to where keep(q) >= 0. keep is linear in (u, v), so one cut is exact.
export function clip(poly, keep) {
  const out = [];
  for (let i = 0; i < poly.length; i++) {
    const a = poly[i], b = poly[(i + 1) % poly.length], fa = keep(a), fb = keep(b);
    if (fa >= 0) out.push(a);
    if ((fa >= 0) !== (fb >= 0)) { const t = fa / (fa - fb); out.push({ u: a.u + (b.u - a.u) * t, v: a.v + (b.v - a.v) * t }); }
  }
  return out;
}
export const higherThan = (b, h) => q => at3(b, q).y - h;
export const lowerThan = (b, h) => q => h - at3(b, q).y;

// Part of blade b's texture (a polygon in uv) drawn with the given projection.
export function texPoly(key, poly, b, project, material, colour, layer, shift = null) {
  if (poly.length < 3 || colour.a <= .002) return;
  const pts = poly.map(q => project(shift ? plus(at3(b, q), shift) : at3(b, q)));
  let area = 0;
  for (let i = 0; i < pts.length; i++) { const p = pts[i], n = pts[(i + 1) % pts.length]; area += p.x * n.z - n.x * p.z; }
  const xz = [], uv = [], tri = [];
  for (let i = 0; i < poly.length; i++) {
    const k = area > 0 ? poly.length - 1 - i : i;          // clockwise on screen, as plane10
    xz.push(pts[k].x, pts[k].z); uv.push(poly[k].u, poly[k].v);
    if (i >= 2) tri.push(0, i - 1, i);
  }
  const m = mesh(key); m.setFlat(xz, tri); m.uv = new Float32Array(uv);
  draw(m, 0, layer, 0, 1, 1, 0, colour, material);
}

// A blade standing in (or lying on, or flying over) the ground. fillTo limits the steel to below
// that height; wireTo draws the outline below that height; scan puts the bright line at a height.
// upright false leaves out the thickness and the dark bands low on the blade. tint colours the steel.
export function blade(key, b, sun, strength, layer, { alpha = 1, upright = true, fillTo = Infinity, wireTo = -1, wireAlpha = 0, scan = -1, shadow = .42, thick = .03, tint = null } = {}) {
  const above = clip(Square, higherThan(b, 0));
  const steel = fillTo < Infinity ? clip(above, lowerThan(b, fillTo)) : above;
  texPoly(`${key} shadow`, steel, b, alongSun(sun), b.w.face, Black.withAlpha(shadow * strength / .32 * alpha), shadowLayer + .002);
  if (upright && thick > 0) [1, .5].forEach((f, i) => texPoly(`${key} edge ${i}`, steel, b, onScreen, b.w.face,
    shade(.28 + .1 * i, tint).withAlpha(alpha), layer + .0004 + i * .0001, v3(-b.N.x * thick * f, -b.N.y * thick * f, -b.N.z * thick * f)));
  const light = Math.max(0, dot3(b.N, unit(v3(-sun.x, 1, -sun.z))));
  texPoly(`${key} face`, steel, b, onScreen, b.w.face, shade(.8 + .2 * light, tint).withAlpha(alpha), layer + .001);
  if (upright) {
    texPoly(`${key} low`, clip(steel, lowerThan(b, .2)), b, onScreen, b.w.face, Black.withAlpha(.24 * alpha), layer + .0013);
    texPoly(`${key} lower`, clip(steel, lowerThan(b, .08)), b, onScreen, b.w.face, Black.withAlpha(.24 * alpha), layer + .0015);
  }
  if (wireAlpha > 0) texPoly(`${key} wire`, clip(above, lowerThan(b, wireTo)), b, onScreen, b.w.wire, Trace.withAlpha(wireAlpha), layer + .0018);
  if (scan >= 0) texPoly(`${key} scan`, clip(clip(above, lowerThan(b, scan + .015)), higherThan(b, scan - .07)), b, onScreen, b.w.mask, TraceHot.withAlpha(.85), layer + .002);
}

// The cut across the blade on the floor: centre, direction along it, half length, and the side
// that faces the camera. sink is the share of the blade underground, which picks the width.
export function cutOf(b, sink) {
  let D = cross(b.N, Up); D = unit(v3(D.x, 0, D.z));
  if (dot3(D, b.B) < 0) D = v3(-D.x, 0, -D.z);
  const [lo, hi] = b.w.width[Math.min(15, Math.max(0, Math.floor(sink * 16)))];
  const a = lo * b.scale, c = hi * b.scale, mid = (a + c) / 2;
  const g = { x: b.tip.x + b.A.x * (-b.tip.y / b.A.y), z: b.tip.z + b.A.z * (-b.tip.y / b.A.y) };
  let F = v3(-D.z, 0, D.x);
  if (F.z > 0) F = v3(-F.x, 0, -F.z);
  return { x: g.x + D.x * mid, z: g.z + D.z * mid, D, F, half: (c - a) / 2 };
}

// The mark a blade leaves where it goes in, drawn flat on the floor: a tight contact shadow, cracks
// running out from it, a slit as wide as the blade, a thin lip of earth either side, a few crumbs.
// Shadow, cracks and slit only darken whatever the floor is; the lips and crumbs are soil. The
// front lip is drawn over the blade and reaches a little north over its foot, which is what puts
// the steel into the ground. forward (a direction on the floor) tips the cracks that way. dirt
// scales cracks and earth. tint colours the earth of the lips.
export function plant(key, cut, layer, seed, sun, { dirt = 1, grow = 1, alpha = 1, forward = null, cracks = 6, crumbs = 3, tint = null } = {}) {
  if (grow <= 0 || alpha <= 0) return;
  const { D, F, half } = cut, rot = -Math.atan2(D.z, D.x) / D2R, k = dirt;
  const pt = (along, out) => ({ x: cut.x + D.x * along + F.x * out, z: cut.z + D.z * along + F.z * out });
  sprite(pt(0, .015), (half * 2 + .35) * grow, .2 * grow, Black.withAlpha(.34 * alpha), soft, Floor + .001, rot);
  for (let i = 0; i < cracks; i++) {
    const end = i < 2, side = i % 2 ? 1 : -1;
    const start = end ? pt(side * half * .9, 0) : pt((rand(seed * 7 + i) - .5) * half * 1.4, 0);
    const ang = end ? Math.atan2(D.z * side, D.x * side) + (rand(seed * 3 + i) - .5) * .6
      : Math.atan2(F.z * side, F.x * side) + (rand(seed * 5 + i) - .5) * 1.7;
    let len = (.1 + rand(seed * 11 + i) * .17) * k * grow;
    if (forward && !end) {
      const toward = Math.cos(ang) * forward.x + Math.sin(ang) * forward.z;
      len *= 1 + .8 * Math.max(0, toward);
    }
    const pts = [start];
    for (let j = 1; j <= 4; j++) {
      const a = ang + (rand(seed * 13 + i * 5 + j) - .5) * 1.1, q = pts[j - 1];
      pts.push({ x: q.x + Math.cos(a) * len / 4, z: q.z + Math.sin(a) * len / 4 });
    }
    line(`${key} crack ${i}`, pts, .024, Black.withAlpha(.5 * alpha), solid, Floor + .002, 'end');
  }
  line(`${key} slit`, [pt(-half - .035, 0), pt(0, 0), pt(half + .035, 0)], .055, Hole.withAlpha(.92 * alpha), solid, Floor + .004, 'both');
  lip(`${key} back`, cut, -1, half, .026 * k, grow, alpha, layer - .0006, seed, sun, tint);
  lip(`${key} front`, cut, 1, half, .032 * k, grow, alpha, layer + .0025, seed + 3, sun, tint);
  for (let i = 0; i < crumbs; i++) {
    const side = i % 2 ? 1 : -1;
    rock(pt(side * (half + .07 + rand(seed * 17 + i) * .12), (rand(seed * 19 + i) - .3) * .16),
      (.028 + rand(seed + i * 7) * .022) * k * grow, rand(seed + i * 9) * 360, alpha, 1 + 2 * i, layer + .003);
  }
}
// One lip of earth pushed up along the slit, tapering to nothing at both ends. side 1 is the camera
// side. Its slope toward the sun is lit.
function lip(key, cut, side, half, reach, grow, alpha, layer, seed, sun, tint = null) {
  const { D } = cut, F = { x: cut.F.x * side, z: cut.F.z * side }, n = 9;
  const inner = [], crest = [], outer = [];
  for (let i = 0; i < n; i++) {
    const t = i / (n - 1), along = (-half - .045 + (half * 2 + .09) * t) * grow, bulge = Math.pow(Math.sin(t * Math.PI), .6);
    const out = (.01 + reach * bulge * (.7 + .6 * rand(seed * 11 + i))) * grow, back = (side > 0 ? .024 : .012) * bulge * grow;
    const base = { x: cut.x + D.x * along, z: cut.z + D.z * along };
    inner.push({ x: base.x - F.x * back, z: base.z - F.z * back });
    crest.push({ x: base.x + F.x * out * .3, z: base.z + F.z * out * .3 });
    outer.push({ x: base.x + F.x * out, z: base.z + F.z * out });
  }
  const sunward = -(F.x * sun.x + F.z * sun.z) > 0;
  band(`${key} lip`, inner, outer, tinted(sunward ? DirtMid : DirtDark, tint).withAlpha(alpha), layer);
  band(`${key} lip top`, inner, crest, tinted(sunward ? DirtLit : DirtMid, tint).withAlpha(alpha), layer + .0002);
}

// The floor breaking as a blade comes up or drives in: crumbs thrown that land and stay, and a puff.
// forward, if given, throws them that way.
export function breakOut(key, cut, age, seed, forward = null) {
  if (age < 0) return;
  for (let i = 0; i < 5; i++) {
    const u = age - .02 - i * .025, flight = .34;
    if (u < 0) continue;
    const ang = forward ? Math.atan2(forward.z, forward.x) + (rand(seed * 5 + i) - .5) * 1.8 : rand(seed * 5 + i) * Math.PI * 2;
    const reach = .12 + rand(seed * 9 + i) * .2, k = Math.min(1, u / flight);
    const h = Math.max(0, 1.2 * Math.min(u, flight) - 3.5 * Math.min(u, flight) ** 2);
    const pos = { x: cut.x + Math.cos(ang) * reach * k, z: cut.z + Math.sin(ang) * reach * k * .8 + h * Lift };
    rock(pos, .03 + rand(seed + i * 3) * .025, rand(i + seed) * 360 + u * 400 * (1 - k), 1, 1 + 2 * (i % 3), u < flight ? Y + .01 : Floor + .006);
  }
  const d = age / .55;
  if (d < 1) sprite({ x: cut.x, z: cut.z + .08 + d * .12 }, .35 + d * .7, .26 + d * .45, Dust.withAlpha(.4 * (1 - d) * smooth(age / .08)), soft, Y + .005);
}

// Copies in the hand and the body's circuit, shared by Reinforcement, Trace On and the UBW commands.

// Along weapon w's axis in uv, from its point (0) to its pommel (w.length). It is linear in (u, v), so
// clip(poly, q => along(w, q) - a) keeps exactly the part from the pommel down to `a` from the point.
export const along = (w, q) => (q.u - w.tip[0]) * w.axis.u + (q.v - w.tip[1]) * w.axis.v;
// The first `length` cells of a polyline of [x, z] pairs.
export function partial(pts, length) {
  const out = [pts[0]];
  let left = length;
  for (let i = 1; i < pts.length && left > 0; i++) {
    const a = pts[i - 1], b = pts[i], d = Math.hypot(b[0] - a[0], b[1] - a[1]);
    if (d <= left) { out.push(b); left -= d; continue; }
    out.push([a[0] + (b[0] - a[0]) * left / d, a[1] + (b[1] - a[1]) * left / d]);
    left = 0;
  }
  return out;
}
// The magic circuit on a stand-in pawn, in cells from its feet (x toward the weapon hand, z up the
// screen): square traces along the edges of the body, uneven from side to side and with no line down
// the middle, because a spine with limbs off it reads as a skeleton. Run 0 ends at the weapon hand.
// Each run starts `from` cells of travel after the chest lights, so a branch lights when the line
// reaches it; CircuitTravel lights all of it.
export const Circuit = [
  { from: 0, pts: [[0, .34], [.1, .34], [.1, .41], [.18, .41], [.18, .28], [.24, .2]] },
  { from: .38, pts: [[.18, .28], [.18, .1], [.12, .1], [.12, -.1]] },
  { from: 0, pts: [[0, .34], [-.1, .34], [-.1, .22], [-.19, .22], [-.19, .04], [-.1, .04], [-.1, -.1]] },
  { from: .31, pts: [[.18, .35], [.23, .35]] },
  { from: .4, pts: [[-.19, .13], [-.14, .13]] },
  { from: .72, pts: [[.12, 0], [.07, 0]] },
];
Circuit.forEach(run => { run.length = run.pts.reduce((sum, q, i) => i ? sum + Math.hypot(q[0] - run.pts[i - 1][0], q[1] - run.pts[i - 1][1]) : 0, 0); });
export const CircuitTravel = Math.max(...Circuit.map(run => run.from + run.length));
// Weapon w lying flat with its middle at the 3D point m, point along dir (a unit direction on the
// floor), the pommel pitch degrees above the point.
export function flatAt(w, size, m, dir, pitch = 0) { const f = flying(w, size, m, dir, pitch); return pose(w, f.scale, plus(m, f.A, -f.L / 2), f.A, f.B); }
// A copy in the hand of a pawn standing at pos: flat, pointing `angle` degrees, as the game draws a
// carried weapon; side 1 holds it in the east hand, -1 in the west one.
export function heldCopy(w, size, pos, angle, side = 1) {
  const d = angle * D2R, dir = { x: Math.cos(d), z: Math.sin(d) }, reach = w.length * w.image * size * .62;
  return flying(w, size, v3(pos.x + .24 * side + dir.x * reach, .3, pos.z + .02 + dir.z * reach), dir, 0);
}
// A copy breaking into light: its outline flashes and fades, sparks fly out from `at` (a screen
// point, the blade's middle unless given). u 0..1.
export function shatter(key, b, sun, strength, u, seed, layer, at = onScreen(plus(b.tip, b.A, b.L / 2))) {
  if (u < 0 || u >= 1) return;
  blade(`${key} ghost`, b, sun, strength, layer, { fillTo: -1, wireTo: 9, wireAlpha: .9 * (1 - u) });
  for (let i = 0; i < 6; i++) {
    const a = i / 6 * Math.PI * 2 + rand(seed + i) * .6;
    sprite({ x: at.x + Math.cos(a) * u * .7, z: at.z + Math.sin(a) * u * .5 + u * .2 }, .08, .08, TraceHot.withAlpha(1 - u), glow, Y + .02);
  }
}
