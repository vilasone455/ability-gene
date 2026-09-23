// Planted real weapons — a look trial for the Trace kit (the Origin: Blade rework). Not an ability,
// and nothing in Source/RimArt draws this yet.
//
// The question. The Trace kit plants copies of the weapons a pawn has studied, so it wants each
// weapon's own texture standing in the ground, not one generic sword. RimWorld weapon textures are
// drawn lying flat, seen from above. The early Origin: Blade drew the vanilla longsword texture
// planted and it read as a sword lying on the floor, so that kit switched to two pre-drawn sprites
// (Textures/RimArt/Panoply/Blade.png, BladePlanted.png).
//
// The method. A blade is a flat plane in 3D that carries the weapon's own texture. Its tip, pommel
// and width along the axis come from the texture's alpha, worked out once per weapon. The plane
// gets a lean and a turn, is drawn with the kit's height rule (0.60 cells north per cell up), and
// is cut where it meets the ground, so the buried part is not drawn. The same plane projected
// along the sun is the shadow, so the shadow starts at the hole. Then: a darker copy offset behind
// the face for thickness and two dark bands low on the blade. On the floor: a tight contact
// shadow, cracks running out, a slit as wide as the blade, a thin lip of earth either side (the
// front one drawn over the steel's foot) and a few crumbs. The first version heaped a crescent of
// earth round each blade; the user said it read as a stand, not a blade driven in.
//
// Order, default timings:
//   0.20  six weapons come up, one every 0.22 s.
//         rise:  the cracks run out as the blade comes up hilt first in 0.45 s, crumbs are thrown
//                and land and stay, a puff of dust
//         trace: a wire outline climbs out of the ground, then steel fills it from the ground up
//                behind a bright scan line, then a glint at the top and the wire fades
//   2.00  all six stand: knife, longsword, spear, monosword, two large modded swords
//   2.20  a seventh blade (longsword) flies in at hand height from "Thrown from"
//   2.62  it sticks point first, leaning 28 degrees back toward the thrower, turns its flat side to
//         the camera (in the air it faces up) and quivers for about 0.7 s; the cracks run longer
//         on the far side and the crumbs are thrown that way
// "Look" swaps in the two old answers: the texture flat on the floor (the early attempt) and the
// shipped BladePlanted sprite.
//
// Textures. Textures/RimArt/TraceTrial/ is local only and git-excluded: copies of Melee
// Animation's editor copies of the vanilla knife, longsword (red editor arrow painted out), spear
// and monosword, and two modded swords, with <Name>Outline and <Name>Mask made from their alpha.
// Run make_trace_trial_textures.py to rebuild them; without them the lab shows checkerboards. None
// of it ships: the game would read the weapon's own graphic (material colour included) and make
// the outline and mask at load.
//
// Drawing and port notes:
//   - one custom mesh per blade part: the texture square cut by one or two height planes, at most
//     6 corners, uv carried through, wound clockwise like MeshPool.plane10. Rebuilt only while the
//     blade moves.
//   - facing: a blade whose flat side turns toward north-south collapses into a line (height and
//     north share the screen axis), so the turn stays within the "Face turn" limit. A blade leaning
//     toward the camera gets shorter on screen and at about 60 degrees vanishes into its own cut,
//     so the thrown blade leans 85% less when it comes from due south.
//   - the pawn is a stand-in for scale. Blades draw at the Building layer, under pawns, as a planted
//     blade (a Building) does in game.
import { Color, MaterialPool, Mathf, Meshes, ShaderDatabase } from '../js/engine.js';
import { draw, mesh } from './lib/six-paths-solid.js';
import { P, Y, Floor, Lift, sprite, soft, glow, rand, band } from './lib/six-paths-impact.js';
import { pawn, rock, ringAt, glint, line, shadowLayer, buildingLayer, Dust } from './lib/goku.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, D2R = Math.PI / 180;
const disc = Meshes.disc(28, 'trace trial disc');
const solid = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const oldPlanted = MaterialPool.MatFrom('RimArt/Panoply/BladePlanted', ShaderDatabase.Transparent);
const oldFlying = MaterialPool.MatFrom('RimArt/Panoply/Blade', ShaderDatabase.Transparent);
const Hole = new Color(.05, .04, .03), Black = new Color(0, 0, 0);
const DirtDark = new Color(.2, .14, .09), DirtMid = new Color(.36, .27, .18), DirtLit = new Color(.55, .43, .3);
const Trace = new Color(.35, 1, .82), TraceHot = new Color(.82, 1, .95), Caster = new Color(.55, .3, .24);
const grey = g => new Color(g, g, g);

// Per texture, from its alpha: tip and pommel in uv (v up), and the blade's extent across its axis
// in 16 steps from the tip (0) to the pommel (1), in uv. image: cells the whole picture spans at
// Size 1, picked so the lengths compare the way the weapons do.
const Weapons = {
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
    length: len, axis: { u: du / len, v: dv / len }, across: { u: -dv / len, v: du / len },
    face: MaterialPool.MatFrom(`RimArt/TraceTrial/${name}`, ShaderDatabase.Transparent),
    wire: MaterialPool.MatFrom(`RimArt/TraceTrial/${name}Outline`, ShaderDatabase.MoteGlow),
    mask: MaterialPool.MatFrom(`RimArt/TraceTrial/${name}Mask`, ShaderDatabase.MoteGlow),
  });
}

// The field. x, z: where the blade enters the ground, cells from the origin. lean and turn are
// fractions of the panel's most; dir is the way the top leans (degrees, 0 east, 90 north).
const Field = [
  { w: 'LongSword', x: -1.3, z: .9, lean: .7, dir: 150, turn: -.5 },
  { w: 'Spear', x: .05, z: 1.35, lean: .5, dir: 70, turn: .7 },
  { w: 'LargeSword', x: 1.5, z: .85, lean: .8, dir: 20, turn: -.9 },
  { w: 'Knife', x: -.5, z: -.3, lean: 1, dir: 200, turn: .3 },
  { w: 'MonoSword', x: .75, z: -.5, lean: .4, dir: 110, turn: 1 },
  { w: 'Wyrmslayer', x: 2.4, z: -.35, lean: .6, dir: 60, turn: -.3 },
];
// The lab camera sits about 2 cells north of the chosen cell; the scene is moved up to meet it.
const SceneNorth = 1.6, PawnAt = { x: -2.7, z: .15 }, Landing = { x: -1.35, z: -1.25 };
const Flight = .42, ThrowDistance = 5, ThrowHeight = .85, FlightPitch = 8, StickLean = 28, QuiverHz = 8, OldLean = 13;

function times(p) {
  const first = .2, appear = p.rise + .25;
  const stood = first + (Field.length - 1) * p.stagger + appear;
  const fly = stood + .2, land = fly + Flight;
  return { first, appear, stood, fly, land, end: land + 1.9 };
}

// Plain 3D. y is height above the floor.
const v3 = (x, y, z) => ({ x, y, z });
const plus = (a, b, f = 1) => v3(a.x + b.x * f, a.y + b.y * f, a.z + b.z * f);
const dot3 = (a, b) => a.x * b.x + a.y * b.y + a.z * b.z;
const cross = (a, b) => v3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
const unit = a => { const l = Math.hypot(a.x, a.y, a.z) || 1; return v3(a.x / l, a.y / l, a.z / l); };
const Up = v3(0, 1, 0), View = unit(v3(0, 1, -Lift));
const onScreen = q => ({ x: q.x, z: q.z + q.y * Lift });
const alongSun = sun => q => ({ x: q.x + sun.x * q.y, z: q.z + sun.z * q.y });

// A blade as a plane: tip point, A from tip to pommel, B across, N out of the face toward the
// camera, scale in cells per uv unit.
function pose(w, scale, tip, A, across) {
  const B = unit(plus(across, A, -dot3(across, A)));
  let N = unit(cross(A, B));
  if (dot3(N, View) < 0) N = v3(-N.x, -N.y, -N.z);
  return { w, scale, tip, A, B, N, L: w.length * scale };
}
// The tip of a blade whose axis enters the ground at g with `buried` cells of it underground.
const tipUnder = (g, A, buried) => v3(g.x - A.x * buried, -A.y * buried, g.z - A.z * buried);
// Texture point q = { u, v } of blade b, in 3D.
function at3(b, q) {
  const du = q.u - b.w.tip[0], dv = q.v - b.w.tip[1];
  const s = (du * b.w.axis.u + dv * b.w.axis.v) * b.scale, c = (du * b.w.across.u + dv * b.w.across.v) * b.scale;
  return v3(b.tip.x + b.A.x * s + b.B.x * c, b.tip.y + b.A.y * s + b.B.y * c, b.tip.z + b.A.z * s + b.B.z * c);
}
const pommelOf = b => at3(b, { u: b.w.pommel[0], v: b.w.pommel[1] });
const Square = [{ u: 0, v: 0 }, { u: 1, v: 0 }, { u: 1, v: 1 }, { u: 0, v: 1 }];
// The polygon cut to where keep(q) >= 0. keep is linear in (u, v), so one cut is exact.
function clip(poly, keep) {
  const out = [];
  for (let i = 0; i < poly.length; i++) {
    const a = poly[i], b = poly[(i + 1) % poly.length], fa = keep(a), fb = keep(b);
    if (fa >= 0) out.push(a);
    if ((fa >= 0) !== (fb >= 0)) { const t = fa / (fa - fb); out.push({ u: a.u + (b.u - a.u) * t, v: a.v + (b.v - a.v) * t }); }
  }
  return out;
}
const higherThan = (b, h) => q => at3(b, q).y - h;
const lowerThan = (b, h) => q => h - at3(b, q).y;

// Part of blade b's texture (a polygon in uv) drawn with the given projection.
function texPoly(key, poly, b, project, material, colour, layer, shift = null) {
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
function blade(key, b, sun, strength, p, layer, { alpha = 1, upright = true, fillTo = Infinity, wireTo = -1, wireAlpha = 0, scan = -1 } = {}) {
  const above = clip(Square, higherThan(b, 0));
  const steel = fillTo < Infinity ? clip(above, lowerThan(b, fillTo)) : above;
  texPoly(`${key} shadow`, steel, b, alongSun(sun), b.w.face, Black.withAlpha(p.shadow * strength / .32 * alpha), shadowLayer + .002);
  if (upright && p.thick > 0) [1, .5].forEach((f, i) => texPoly(`${key} edge ${i}`, steel, b, onScreen, b.w.face,
    grey(.28 + .1 * i).withAlpha(alpha), layer + .0004 + i * .0001, v3(-b.N.x * p.thick * f, -b.N.y * p.thick * f, -b.N.z * p.thick * f)));
  const light = Math.max(0, dot3(b.N, unit(v3(-sun.x, 1, -sun.z))));
  texPoly(`${key} face`, steel, b, onScreen, b.w.face, grey(.8 + .2 * light).withAlpha(alpha), layer + .001);
  if (upright) {
    texPoly(`${key} low`, clip(steel, lowerThan(b, .2)), b, onScreen, b.w.face, Black.withAlpha(.24 * alpha), layer + .0013);
    texPoly(`${key} lower`, clip(steel, lowerThan(b, .08)), b, onScreen, b.w.face, Black.withAlpha(.24 * alpha), layer + .0015);
  }
  if (wireAlpha > 0) texPoly(`${key} wire`, clip(above, lowerThan(b, wireTo)), b, onScreen, b.w.wire, Trace.withAlpha(wireAlpha), layer + .0018);
  if (scan >= 0) texPoly(`${key} scan`, clip(clip(above, lowerThan(b, scan + .015)), higherThan(b, scan - .07)), b, onScreen, b.w.mask, TraceHot.withAlpha(.85), layer + .002);
}

// The cut across the blade on the floor: centre, direction along it, half length, and the side
// that faces the camera.
function cutOf(b, p) {
  let D = cross(b.N, Up); D = unit(v3(D.x, 0, D.z));
  if (dot3(D, b.B) < 0) D = v3(-D.x, 0, -D.z);
  const [lo, hi] = b.w.width[Math.min(15, Math.max(0, Math.floor(p.sink * 16)))];
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
// the steel into the ground. forward (a direction on the floor) tips the cracks that way.
function plant(key, cut, p, layer, seed, sun, grow = 1, alpha = 1, forward = null) {
  if (grow <= 0 || alpha <= 0) return;
  const { D, F, half } = cut, rot = -Math.atan2(D.z, D.x) / D2R, k = p.dirt;
  const pt = (along, out) => ({ x: cut.x + D.x * along + F.x * out, z: cut.z + D.z * along + F.z * out });
  sprite(pt(0, .015), (half * 2 + .35) * grow, .2 * grow, Black.withAlpha(.34 * alpha), soft, Floor + .001, rot);
  for (let i = 0; i < 6; i++) {
    const end = i < 2, side = i % 2 ? 1 : -1;
    const start = end ? pt(side * half * .9, 0) : pt((rand(seed * 7 + i) - .5) * half * 1.4, 0);
    let ang = end ? Math.atan2(D.z * side, D.x * side) + (rand(seed * 3 + i) - .5) * .6
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
  lip(`${key} back`, cut, -1, half, .026 * k, grow, alpha, layer - .0006, seed, sun);
  lip(`${key} front`, cut, 1, half, .032 * k, grow, alpha, layer + .0025, seed + 3, sun);
  for (let i = 0; i < 3; i++) {
    const side = i % 2 ? 1 : -1;
    rock(pt(side * (half + .07 + rand(seed * 17 + i) * .12), (rand(seed * 19 + i) - .3) * .16),
      (.028 + rand(seed + i * 7) * .022) * k * grow, rand(seed + i * 9) * 360, alpha, 1 + 2 * i, layer + .003);
  }
}
// One lip of earth pushed up along the slit, tapering to nothing at both ends. side 1 is the camera
// side. Its slope toward the sun is lit.
function lip(key, cut, side, half, reach, grow, alpha, layer, seed, sun) {
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
  band(`${key} lip`, inner, outer, (sunward ? DirtMid : DirtDark).withAlpha(alpha), layer);
  band(`${key} lip top`, inner, crest, (sunward ? DirtLit : DirtMid).withAlpha(alpha), layer + .0002);
}

// The floor breaking as a blade comes up or drives in: crumbs thrown that land and stay, and a puff.
// forward, if given, throws them that way.
function breakOut(key, cut, age, seed, forward = null) {
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

// The pose of field blade f at raise r (0: pommel at the floor, 1: standing).
function fieldPose(f, p, r) {
  const w = Weapons[f.w], scale = w.image * p.size, L = w.length * scale;
  const l = f.lean * p.lean * D2R, d = f.dir * D2R, t = f.turn * p.turn * D2R;
  const A = v3(Math.sin(l) * Math.cos(d), Math.cos(l), Math.sin(l) * Math.sin(d));
  const buried = p.sink * L + (1 - r) * (1 - p.sink) * L;
  return pose(w, scale, tipUnder({ x: f.x, z: f.z }, A, buried), A, v3(Math.cos(t), 0, Math.sin(t)));
}
// The early attempt: the same texture lying on the floor, turned to where it would lean.
function flatPose(f, p) {
  const w = Weapons[f.w], scale = w.image * p.size, L = w.length * scale, d = f.dir * D2R;
  const A = v3(Math.cos(d), 0, Math.sin(d));
  return pose(w, scale, v3(f.x - A.x * L / 2, .04, f.z - A.z * L / 2), A, v3(-Math.sin(d), 0, Math.cos(d)));
}

// The thrown longsword: flying, then stuck and quivering. Returns { b, flying, age }.
function thrownPose(s, p, t) {
  const w = Weapons.LongSword, scale = w.image * p.size, L = w.length * scale;
  const back = { x: Math.cos(p.from * D2R), z: Math.sin(p.from * D2R) };      // toward the thrower
  const across = v3(-back.z, 0, back.x);
  if (s < t.land) {
    const u = clamp((s - t.fly) / Flight), pitch = FlightPitch * D2R;
    const A = v3(back.x * Math.cos(pitch), Math.sin(pitch), back.z * Math.cos(pitch));
    const tip = v3(Landing.x + back.x * ThrowDistance * (1 - u), ThrowHeight * Math.pow(1 - u, 1.3) + .02, Landing.z + back.z * ThrowDistance * (1 - u));
    return { b: pose(w, scale, tip, A, across), flying: true, age: s - t.fly };
  }
  const age = s - t.land, settle = smooth(age / .06);
  const stuck = StickLean * (1 - .85 * Math.max(0, -back.z));
  const lean = lerp(90 - FlightPitch, stuck, settle) + p.quiver * Math.exp(-age / .22) * Math.sin(age * QuiverHz * Math.PI * 2) * settle;
  const l = lean * D2R, A = v3(back.x * Math.sin(l), Math.cos(l), back.z * Math.sin(l));
  // In the air its flat side faces up. Stuck, it turns that side to the camera (across east-west),
  // or the texture is seen at a slant and its own shadow shows more sword than the blade does.
  const east = across.x >= 0 ? 1 : -1, face = v3(lerp(across.x, east, settle), 0, lerp(across.z, 0, settle));
  return { b: pose(w, scale, tipUnder(Landing, A, p.sink * L * .85 * settle), A, face), flying: false, age };
}

// The seventh blade. In flight it is the texture seen from above (a blade lying flat in the air);
// after it sticks it is drawn like the field.
function drawThrown(key, { b, flying, age }, o, p, sun, strength, layer) {
  const shifted = { ...b, tip: v3(b.tip.x + o.x, b.tip.y, b.tip.z + o.z) };
  if (p.look === 'current sprite') {
    const tipDir = Math.atan2(-b.A.x, -b.A.z) / D2R, g = { x: o.x + Landing.x, z: o.z + Landing.z };
    if (flying) sprite(onScreen(at3(shifted, { u: .5, v: .5 })), 1.05, 1.05, grey(1), oldFlying, layer, tipDir);
    else sprite({ x: g.x, z: g.z + .28 * p.size }, p.size, p.size, grey(1), oldPlanted, layer + .001, 0);
    return;
  }
  if (flying) {
    // a light streak behind it along the flight
    const tail = onScreen(pommelOf(shifted)), len = Math.hypot(b.A.x, b.A.z) || 1;
    line(`${key} streak`, [tail, { x: tail.x + b.A.x / len * 1.4, z: tail.z + b.A.z / len * 1.4 }], .18, grey(1).withAlpha(.25), solid, layer - .001, 'end');
    blade(key, shifted, sun, strength, p, layer, { upright: false });
    return;
  }
  if (p.look === 'planted') {
    const cut = cutOf(shifted, p);
    const ahead = { x: -Math.cos(p.from * D2R), z: -Math.sin(p.from * D2R) };
    plant(key, cut, p, layer, 21, sun, smooth(age / .1), 1, ahead);
    breakOut(`${key} kick`, cut, age, 21, ahead);
    if (age < .15) glint(`${key} spark`, { x: cut.x, z: cut.z + .08 }, .3, 1 - age / .15);
    blade(key, shifted, sun, strength, p, layer);
  } else {
    // the early attempt: it lands and lies there
    const d = Math.atan2(b.A.z, b.A.x), L = b.L;
    const flat = pose(b.w, b.scale, v3(o.x + Landing.x - Math.cos(d) * L * .5, .04, o.z + Landing.z - Math.sin(d) * L * .5),
      v3(Math.cos(d), 0, Math.sin(d)), v3(-Math.sin(d), 0, Math.cos(d)));
    blade(key, flat, sun, strength, p, layer, { upright: false });
  }
}

export default {
  kit: 'Trace', label: 'Planted real weapons (sketch)',
  params: {
    look: { label: 'Look', value: 'planted', options: ['planted', 'flat (old attempt)', 'current sprite'], group: 'Compare' },
    appear: { label: 'Appear', value: 'rise', options: ['rise', 'trace'], group: 'Compare' },
    actors: { label: 'Stand-in pawn', value: true, group: 'Compare' },
    size: P('Size (x image)', 1.3, .8, 2.5, .05, 'Blade'),
    sink: P('Buried (share of length)', .2, .05, .45, .01, 'Blade'),
    lean: P('Lean, most (degrees)', 16, 0, 40, 1, 'Blade'),
    turn: P('Face turn, most (degrees)', 30, 0, 60, 1, 'Blade'),
    thick: P('Thickness (cells)', .03, 0, .08, .005, 'Blade'),
    dirt: P('Cracks and earth (size)', 1, 0, 2, .05, 'Ground'),
    shadow: P('Shadow opacity', .42, 0, .8, .02, 'Ground'),
    from: P('Thrown from (degrees)', 200, 0, 360, 5, 'Thrown blade'),
    quiver: P('Quiver (degrees)', 10, 0, 25, 1, 'Thrown blade'),
    stagger: P('Between blades', .22, .05, .5, .01, 'Timing (s)'),
    rise: P('Rise or trace', .45, .2, 1.2, .05, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Blades appear', t: t.first }, { name: 'Standing', t: t.stood }, { name: 'Thrown', t: t.fly }, { name: 'Sticks', t: t.land }];
  },
  events(p) { return [{ t: times(p).land, type: 'shake', value: .035 }]; },

  draw(s, p, { origin: cell, scene }) {
    const t = times(p), o = { x: cell.x, z: cell.z + SceneNorth };
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const at = (x, z) => ({ x: o.x + x, z: o.z + z });
    if (p.actors) pawn(at(PawnAt.x, PawnAt.z), Caster, sun, strength, { hair: true });

    // Everything standing, sorted north first so nearer blades cover farther ones.
    const standing = [];
    Field.forEach((f, i) => {
      const age = s - t.first - i * p.stagger;
      if (age >= 0) standing.push({ f: { ...f, x: o.x + f.x, z: o.z + f.z }, i, age, z: o.z + f.z });
    });
    const throwIn = s >= t.fly ? thrownPose(s, p, t) : null;
    if (throwIn && !throwIn.flying) standing.push({ thrown: throwIn, i: 9, z: o.z + Landing.z });
    standing.sort((a, b) => b.z - a.z);

    standing.forEach((e, rank) => {
      const layer = buildingLayer + rank * .005, key = `trace trial ${e.i}`;
      if (e.thrown) { drawThrown(key, e.thrown, o, p, sun, strength, layer); return; }
      const f = e.f, age = e.age;
      if (p.look === 'current sprite') {
        const lean = f.lean * OldLean * (f.dir > 90 && f.dir < 270 ? -1 : 1), size = p.size;
        const r = lean * D2R;
        draw(disc, f.x + sun.x * .05, shadowLayer + .002, f.z + sun.z * .05, .16 * size, .05 * size, 0, Black.withAlpha(.3 * clamp(age / p.rise)));
        sprite({ x: f.x + Math.sin(r) * .28 * size, z: f.z + Math.cos(r) * .28 * size }, size, size, grey(1).withAlpha(clamp(age / p.rise)), oldPlanted, layer + .001, lean);
        return;
      }
      if (p.look === 'flat (old attempt)') {
        blade(key, flatPose(f, p), sun, strength, p, layer, { alpha: clamp(age / p.rise), upright: false });
        return;
      }
      if (p.appear === 'rise') {
        const r = 1 - Math.pow(1 - clamp((age - .06) / p.rise), 3);
        const b = fieldPose(f, p, r), cut = cutOf(fieldPose(f, p, 1), p);
        plant(key, cut, p, layer, e.i + 1, sun, .3 + .7 * smooth(r * 1.3));
        breakOut(key, cut, age - .04, e.i + 1);
        if (r > 0) blade(key, b, sun, strength, p, layer);
      } else {
        const b = fieldPose(f, p, 1), cut = cutOf(b, p), top = pommelOf(b).y + .05;
        const wireTime = .45 * p.rise, fillTime = .55 * p.rise;
        const wire = top * smooth(age / wireTime), fill = top * smooth((age - wireTime) / fillTime);
        const wireAlpha = .95 * (1 - smooth((age - p.rise - .05) / .3));
        plant(key, cut, p, layer, e.i + 1, sun, smooth(age / wireTime));
        const lit = smooth(age / .12) * (1 - smooth((age - p.rise) / .35));
        sprite({ x: cut.x, z: cut.z }, (cut.half * 2 + .8) * (.6 + .4 * smooth(age / p.rise)), .5, Trace.withAlpha(.3 * lit), glow, Floor + .005);
        ringAt(cut, .2 + .25 * smooth(age / p.rise), Trace.withAlpha(.22 * lit), Floor + .006);
        blade(key, b, sun, strength, p, layer, {
          fillTo: age < wireTime ? -1 : (fill >= top - .001 ? Infinity : fill),
          wireTo: wire, wireAlpha, scan: age > wireTime && fill < top - .001 ? fill : -1,
        });
        const g = age - p.rise;
        if (g >= 0 && g < .3) glint(`${key} glint`, onScreen(pommelOf(b)), .38, Math.sin(g / .3 * Math.PI), TraceHot, g * 60);
      }
    });

    if (throwIn && throwIn.flying) drawThrown('trace trial thrown', throwIn, o, p, sun, strength, Y + .01);
  },
};
