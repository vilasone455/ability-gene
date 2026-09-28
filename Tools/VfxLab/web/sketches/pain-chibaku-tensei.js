// Chibaku Tensei (Planetary Devastation) — proposed replacement for Gravity Well as the Pain hero
// kit's ultimate. Proposed 2026-09-28, NOT agreed; every number is a placeholder and will be an XML
// field. Nothing in Source/RimArt draws this yet.
//
// Why replace Gravity Well: its picture (a black core in a pale blue accretion swirl) is a generic
// black hole and reads as Gojo's Blue, which already reuses its code. Chibaku Tensei is Pain's own
// finishing technique from the Naruto fight: a small black sphere goes up, tears the ground out in
// slabs and packs it, with everyone caught, into a ball of rock. The shape change (floor -> slabs ->
// sphere -> chunks) is the point of it, which is what the mod is built on.
//
// Proposed mechanic (placeholders):
//   Target a cell up to 25 cells away, line of sight. Warm-up 0.8 s: Pain raises his open hand and a
//   black core forms over the palm. The core flies at 14 cells/s to the cell and stops 2.8 cells
//   above it.
//   Pull, 3 s, radius 6: every pawn in the radius (any faction, colonists too; not Pain, not a pawn
//   pinned by 3 Black Receiver rods), and every item, corpse and chunk, is lifted and pulled into the
//   core. Cover does not help: raiders behind sandbags are pulled over them. Buildings are not moved,
//   and the ground under a building or a pinned pawn stays (islands in the crater).
//   Ball: radius 2, holds 12 s. Pawns inside cannot move, act or be targeted, and take 2 blunt per
//   second (24 in total). While it holds, Shinra Tensei and Banshō Ten'in are locked (the Deva Path
//   is holding it); Black Receiver still works. If Pain is downed the ball breaks at once.
//   Release: the ball bursts. The pawns inside fall 2.8 cells: 8 blunt, stunned 2.5 s. Chunks of
//   stone land in the crater and stay (real chunk items, 6-10). The natural ground in the radius
//   becomes gravel; floors are left alone.
//   Cooldown 1 day. Echo charge 30 (Gravity Well's slot was 20). Heroes are never cast by the AI.
//   Differs from Shinra Tensei (push away), Banshō Ten'in (one target, pull and slam) and Black
//   Receiver (one target, pinned). Overlap to watch: Gojo's Unlimited Void also takes a group out of
//   the fight; this one is physical, visible on the map, hurts, catches allies and leaves a crater.
//
// Order, with the default sliders (9 cells, radius 6):
//   0.00  rest
//   0.20  warm-up 0.8 s: Rinnegan glint; Pain's arm comes up toward the cell, hand open; a black core
//         in a soft pale glow grows over the palm (the Banshō palm core)
//   1.00  launch: the core flies to the cell and climbs to 2.8 cells, a dark trail behind it
//   ~1.7  arrival: soft flash, a small shake, a dark see-through circle spreads to the true radius
//   ~1.9  pull, 3 s: cracks run out from the centre to the rim (0.45 s); plates of ground heave and
//         tear free, the inner ones first, and fly up into the core, turning over (grass on top, earth
//         or stone underneath); the pawns in the radius stagger toward the centre and are lifted and
//         pulled in, flailing, before the plates under them go; dark streaks flow into the core; the
//         ball grows from what arrives (radius 0.3 -> 1 x the ball radius, by the cube root of the share
//         arrived); the plates leave a crater of dark earth with ribs between the holes; the sandbags
//         and the ground under them stay; the raider outside the radius leans in and stays
//   ~4.9  formed: shake; the ball turns slowly (14 degrees/s), a few rocks circle it, it squeezes once
//         a second (the crush damage)
//   hold  compressed in the lab to the "Hold shown" slider (12 s in the game)
//   crack 0.4 s: dark seams run round the ball
//   burst the ball breaks outward into chunks that fall and stay in the crater; the pawns inside
//         drop out and lie stunned; a dust cloud; the crater, rim cracks and rocks stay to the end
//
// Drawing: nothing needs a per-facing method; the aim only moves Pain. The floor circle, crater and
// area marker are level circles at the true radius. The ground plates are a fixed Voronoi tiling of
// the disc (jittered hex seeds, edges bent in the middle and shared, so the cracks line up and there
// are no rings in the pattern); each plate is one fan
// polygon with an edge, a south side for its thickness and a face, and it "turns over" by squashing
// along an axis and swapping to its underside colour. The ball is a sphere projected with the kit's
// height rule (screen z = z + 0.6 h), so its outline is an ellipse 1.166 times taller than wide; its
// surface is up to 84 plates on a Fibonacci sphere, drawn back to front on the side that faces the
// camera, lit by the scene sun, over a dark body, with a darker limb. Things in the air are split into
// behind and in front of the ball by their depth along the view direction. Pain, the raiders, the
// colonist, the pinned raider and its rods, and the sandbags are stand-ins.
import { AltitudeLayer, Color, Mathf, Meshes } from '../js/engine.js';
import { draw, mesh, v3, add, mul, dot, unit, cross, Up, View, FillFrom, sunLight } from './lib/six-paths-solid.js';
import { P, Y, Floor, Lift, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import { rock, line, stunStars, streak, Skin, EnemyColour, Ink } from './lib/goku.js';
import { frame, crack, kick, puff, rect, bump, easeOut, Ally } from './lib/chain-sickle.js';
import { eyeStar } from './lib/amenoyodomi.js';
import { Core, PaleBlue, DustC, BodyZ, ShoulderH, HandH, Reach, pain, standing, lying, arm } from './lib/pain.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2, D2R = Mathf.Deg2Rad;
const disc = Meshes.disc(48, 'chibaku disc');
const limb = Meshes.band(.8, 1, 72, 'chibaku limb');
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), buildingLayer = AltitudeLayer.Building.AltitudeFor();
const pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const Squash = Math.sqrt(1 + Lift * Lift);   // a sphere's outline on screen is this much taller than wide

// Colours. Plate tops match the lab's grass and soil; undersides are earth or stone.
const pair = (dark, lit) => ({ dark, lit });
const GrassTop = pair(new Color(.24, .28, .12), new Color(.47, .52, .26)), SoilTop = pair(new Color(.27, .2, .13), new Color(.5, .38, .26));
const Earth = pair(new Color(.19, .13, .08), new Color(.46, .34, .22)), Stone = pair(new Color(.22, .21, .2), new Color(.56, .54, .5));
const SideC = new Color(.13, .09, .06), SlabEdge = new Color(.07, .05, .035), BallBody = new Color(.08, .06, .04);
const HoleDeep = new Color(.11, .08, .05), HoleEdge = new Color(.24, .18, .12), Rib = new Color(.31, .24, .16), CrackC = new Color(.06, .04, .03);
const Bag = new Color(.64, .57, .42), BagDark = new Color(.36, .31, .22), RodC = new Color(.05, .05, .07), RodLit = new Color(.42, .42, .48);

// The rule's fixed numbers (placeholders; shown in the header, not all drawn).
const HoldSeconds = 12, StunSeconds = 2.5;
// Decided timing and shape.
const Lead = .2, Pulse = .25, CrackRun = .45, CrackTime = .4, Tail = 2.8;
const RaisedH = .72;              // Pain's hand, raised, cells up
const CoreR = .34, PalmR = .2;    // the core over the ball, and over the palm
const Spin = 14;                  // degrees per second the ball turns
const MaxSlots = 84;              // plates drawn on the ball's surface
const HeaveH = .18, IslandR = .8;  // how far a plate rises before it tears free; ground kept round anything anchored
const G = 12;                     // cells/s², for what falls out of the ball
const SandAlong = -2.3;           // the sandbag wall, cells from the centre toward Pain

const Scenarios = ['raiders behind sandbags', 'a colonist inside the radius too', 'a raider pinned by 3 rods'];

function times(p) {
  const cast = Lead, launch = cast + p.warm;
  const run = Math.hypot(p.distance - (.12 + Reach + .3), ballH(p) - (RaisedH + .12));
  const arrive = launch + run / p.speed, pull = arrive + Pulse, formed = pull + p.pull;
  const crackAt = formed + p.hold, burst = crackAt + CrackTime;
  return { cast, launch, arrive, pull, formed, crack: crackAt, burst, end: burst + Tail };
}
const ballH = p => Math.max(p.ballH, p.ballR + .4);

// ---- small geometry -------------------------------------------------------------------------------
const scr = q => ({ x: q.x, z: q.z + q.y * Lift });
const rotY = (n, a) => { const c = Math.cos(a), s = Math.sin(a); return v3(n.x * c - n.z * s, n.y, n.x * s + n.z * c); };
const polar = (r, a) => ({ x: Math.cos(a) * r, z: Math.sin(a) * r });
// A filled polygon fanned from its centre, so a slightly concave plate still fills correctly.
function polyC(key, c, pts, colour, layer) {
  if (colour.a <= .001) return;
  const v = [c.x, c.z], tri = [], n = pts.length;
  pts.forEach(q => v.push(q.x, q.z));
  for (let i = 0; i < n; i++) tri.push(0, 1 + i, 1 + (i + 1) % n);
  const m = mesh(key); m.setFlat(v, tri);
  draw(m, 0, layer, 0, 1, 1, 0, colour);
}
// Every point moved d cells away from c (in toward it for d < 0).
const growC = (pts, c, d) => pts.map(q => { const dx = q.x - c.x, dz = q.z - c.z, l = Math.hypot(dx, dz) || 1, k = Math.max(.1, 1 + d / l); return { x: c.x + dx * k, z: c.z + dz * k }; });

// Keep the part of a convex polygon on the side of the line through m where (q - m).n <= 0.
function clipHalf(poly, m, n) {
  const out = [], side = q => (q.x - m.x) * n.x + (q.z - m.z) * n.z;
  for (let i = 0; i < poly.length; i++) {
    const a = poly[i], b = poly[(i + 1) % poly.length], sa = side(a), sb = side(b);
    if (sa <= 0) out.push(a);
    if ((sa < 0) !== (sb < 0) && sa !== sb) { const k = sa / (sa - sb); out.push({ x: a.x + (b.x - a.x) * k, z: a.z + (b.z - a.z) * k }); }
  }
  return out;
}
// Each straight edge gets a bend in the middle. The bend is worked out from the two corners in a fixed
// order, so both plates that share the edge bend it the same way and the cracks still line up.
function bendEdges(poly, cell) {
  const out = [], id = q => Math.round(q.x * 500) * 7 + Math.round(q.z * 500) * 13;
  for (let i = 0; i < poly.length; i++) {
    const a = poly[i], b = poly[(i + 1) % poly.length], len = Math.hypot(b.x - a.x, b.z - a.z);
    out.push(a);
    if (len < cell * .3) continue;
    const [p, q] = id(a) < id(b) ? [a, b] : [b, a], k = (rand(id(p) * .37 + id(q) * .11) - .5) * .22 * len;
    out.push({ x: (a.x + b.x) / 2 - (q.z - p.z) / len * k, z: (a.z + b.z) / 2 + (q.x - p.x) / len * k });
  }
  return out;
}
// The ground plates: a Voronoi tiling of the disc from a jittered hex grid of seeds, clipped to the
// circle, so the plates are irregular and share their edges (the cracks line up) with no rings in the
// pattern. Built once per radius and plate size.
const geoCache = new Map();
function plateGeo(R, cell) {
  const key = `${R}|${cell}`;
  if (geoCache.has(key)) return geoCache.get(key);
  const list = [], seeds = [], dz = cell * .866, rows = Math.ceil(R / dz) + 1, cols = Math.ceil(R / cell) + 1;
  for (let row = -rows; row <= rows; row++) for (let col = -cols; col <= cols; col++) {
    const h = (row + 97) * 331 + (col + 89) * 17;
    const q = { x: (col + (row & 1) * .5 + (rand(h + .1) - .5) * .7) * cell, z: (row + (rand(h + .6) - .5) * .7) * dz };
    if (Math.hypot(q.x, q.z) < R + cell * .6) seeds.push(q);
  }
  const rim = [];
  for (let k = 0; k < 72; k++) { const a = k / 72 * TAU; rim.push(polar(R, a)); }
  const mk = pts => {
    const c = { x: pts.reduce((s, q) => s + q.x, 0) / pts.length, z: pts.reduce((s, q) => s + q.z, 0) / pts.length }, i = list.length;
    list.push({ i, pts, c, r: Math.hypot(c.x, c.z), loc: pts.map(q => ({ x: q.x - c.x, z: q.z - c.z })),
      top: rand(i * 11 + 1) < .6 ? GrassTop : SoilTop, under: rand(i * 11 + 2) < .25 ? Stone : Earth,
      thick: .12 + .1 * rand(i * 11 + 3), axis: rand(i * 11 + 4) * Math.PI,
      spinRate: (70 + 140 * rand(i * 11 + 5)) * (rand(i * 11 + 6) < .5 ? -1 : 1) * D2R, tumbleRate: 3 + 5 * rand(i * 11 + 7) });
  };
  seeds.forEach(sd => {
    const B = R + 2;
    let cellPoly = [{ x: -B, z: -B }, { x: B, z: -B }, { x: B, z: B }, { x: -B, z: B }];
    seeds.forEach(o => {
      if (o === sd || Math.hypot(o.x - sd.x, o.z - sd.z) > cell * 2.6) return;
      cellPoly = clipHalf(cellPoly, { x: (o.x + sd.x) / 2, z: (o.z + sd.z) / 2 }, { x: o.x - sd.x, z: o.z - sd.z });
    });
    for (let k = 0; k < rim.length && cellPoly.length > 2; k++) {
      const a = rim[k], b = rim[(k + 1) % rim.length];
      cellPoly = clipHalf(cellPoly, a, { x: b.z - a.z, z: a.x - b.x });     // outward normal of a counter-clockwise edge
    }
    let area = 0;
    for (let i = 0; i < cellPoly.length; i++) { const a = cellPoly[i], b = cellPoly[(i + 1) % cellPoly.length]; area += a.x * b.z - b.x * a.z; }
    if (cellPoly.length > 2 && Math.abs(area) / 2 > .012) mk(bendEdges(cellPoly, cell));
  });
  geoCache.set(key, list);
  return list;
}
// When each plate cracks, heaves, tears free and reaches the ball: the inner plates first.
function plateTimes(pl, p, t, R) {
  const u = Math.min(1, pl.r / R), crackAt = t.pull + CrackRun * u;
  const liftAt = t.pull + p.pull * (.06 + .5 * Math.pow(u, 1.1) + .16 * rand(pl.i * 3 + 1));
  const fly = p.pull * (.17 + .1 * rand(pl.i * 3 + 2));
  return { crackAt, heaveAt: Math.max(crackAt + .03, liftAt - .22), liftAt, fly, arriveAt: liftAt + fly };
}
// The ball's surface: slot directions on a Fibonacci sphere, filled in a shuffled order.
const slotCache = new Map();
function slotSet(M) {
  if (slotCache.has(M)) return slotCache.get(M);
  const dirs = [];
  for (let m = 0; m < M; m++) { const y = 1 - 2 * (m + .5) / M, rr = Math.sqrt(1 - y * y), ph = m * 2.399963; dirs.push(v3(rr * Math.cos(ph), y, rr * Math.sin(ph))); }
  const out = { dirs, perm: dirs.map((_, m) => m).sort((a, b) => rand(a * 7 + 3) - rand(b * 7 + 3)), spread: 1.3 * Math.sqrt(4 / Math.max(1, M)) };
  slotCache.set(M, out);
  return out;
}

// ---- stand-ins (Pain, standing and lying pawns and his arm are in lib/pain.js) ---------------------
// A pawn in the air or staggering: body turned so the head points at `ang` (0 north, clockwise), arms
// out and flailing by `flail`. g is its ground point, h its height.
function body(key, g, h, colour, ang, flail, s, sun, strength, layer) {
  const mid = { x: g.x, z: g.z + BodyZ + h * Lift };
  sprite({ x: g.x + sun.x * (.3 + h), z: g.z + sun.z * (.3 + h) }, .8, .4, Ink.withAlpha(strength / (1 + h)), soft, shadowLayer);
  const hx = Math.sin(ang), hz = Math.cos(ang), px = -hz, pz = hx;
  for (const side of [-1, 1]) {
    const sh = { x: mid.x + hx * .16 + px * side * .13, z: mid.z + hz * .16 + pz * side * .13 }, wave = Math.sin(s * 30 + side * 1.3) * .14 * flail;
    line(`${key} arm ${side}`, [sh, { x: sh.x + px * side * .22 + hx * wave, z: sh.z + pz * side * .22 + hz * wave }], .07, Skin, undefined, layer, 'none');
  }
  draw(disc, mid.x, layer, mid.z, .22, .32, ang / D2R, colour);
  draw(disc, mid.x + hx * .4, layer, mid.z + hz * .4, .16, .17, 0, Skin);
}
// Head direction for a pawn leaning toward `to` by `k` (0 upright).
function leanAng(g, to, k) {
  const dx = to.x - g.x, dz = to.z - g.z, l = Math.hypot(dx, dz) || 1;
  return Math.atan2(dx / l * k, 1 + dz / l * k);
}
function sandbags(f, T, sun, strength) {
  const aim = f.a / D2R, turn = -(aim + 90), bags = [];
  for (let c = -1; c <= 1; c++) {
    const g = f.ground(T, SandAlong, c);
    rect(`chibaku bags shadow ${c}`, { x: g.x + sun.x * .3, z: g.z + sun.z * .3 }, .8, 1, aim, Ink.withAlpha(strength * .9), shadowLayer);
    for (const d of [-.17, .17]) for (let b = 0; b < 3; b++) bags.push({ q: f.ground(T, SandAlong + d, c + (b - 1) * .32), h: .1 });
    for (let b = 0; b < 2; b++) bags.push({ q: f.ground(T, SandAlong, c + (b - .5) * .32), h: .24 });
  }
  bags.sort((m, n) => (m.h - n.h) || (n.q.z - m.q.z)).forEach(({ q, h }, i) => {
    const lay = buildingLayer + .01 + i * .0004, z = q.z + h * Lift;
    draw(disc, q.x, lay, z, .19, .14, turn, BagDark);
    draw(disc, q.x - .012, lay + .0002, z + .025, .16, .1, turn, Bag);
  });
}
// The raider pinned by Black Receiver: face-down with three black rods standing out of its back.
function pinned(key, g, sun, strength) {
  lying(key, g, EnemyColour, { x: 0, z: 1 }, sun, strength);
  [[-.07, .02, -8], [.06, .1, 6], [.0, -.06, 2]].forEach(([dx, dz, lean], i) => {
    const base = { x: g.x + dx, z: g.z + .08 + dz }, tip = { x: base.x + lean * .01, z: base.z + .55 * Lift };
    line(`${key} rod ${i}`, [base, tip], .05, RodC, undefined, pawnLayer + .01 + i * .0002, 'none');
    line(`${key} rod lit ${i}`, [{ x: base.x + .012, z: base.z }, { x: tip.x + .012, z: tip.z }], .014, RodLit, undefined, pawnLayer + .0101 + i * .0002, 'none');
  });
}

// ---- the force ------------------------------------------------------------------------------------
// The black core: a slightly wobbling black hole in a soft pale glow, no hard line on it (the Banshō
// palm core). glowA scales the glow alone, so the ball can cover the core without a halo round it.
function blackCore(key, c, r, s, alpha, layer, glowA = 1) {
  if (r <= .005 || alpha <= 0) return;
  sprite(c, r * 5.6, r * 5.6, PaleBlue.withAlpha(.35 * alpha * glowA), glow, layer);
  sprite(c, r * 3.4, r * 3.4, PaleBlue.withAlpha(.8 * alpha * glowA), glow, layer + .0003);
  sprite(c, r * 2.5, r * 2.5, PaleBlue.withAlpha(.9 * alpha * glowA), glow, layer + .0005);
  const pts = [];
  for (let i = 0; i < 44; i++) {
    const a = i / 44 * TAU, wob = 1 + .03 * Math.sin(2 * a + s * 4) + .02 * Math.sin(3 * a - s * 6);
    pts.push({ x: c.x + Math.cos(a) * r * wob, z: c.z + Math.sin(a) * r * wob });
  }
  polyC(`${key} core`, c, pts, Core.withAlpha(alpha), layer + .0007);
}
// A dark edge on a see-through circle: darkest at the rim, fading inward (the Banshō hold).
const EdgeBands = [[.97, .4], [.93, .2], [.89, .13], [.84, .1], [.79, .07], [.73, .05]]
  .map(([inner, a]) => ({ mesh: Meshes.band(inner, 1, 96, `chibaku edge ${inner}`), a }));
function darkEdge(c, R, alpha, layer) {
  EdgeBands.forEach((b, k) => draw(b.mesh, c.x, layer + k * .0002, c.z, R, R, 0, Core.withAlpha(b.a * alpha)));
}
// Dark streaks from all over the area into the core.
function inflow(key, T, R, core, s, alpha) {
  if (alpha <= 0) return;
  for (let i = 0; i < 22; i++) {
    const a = rand(i + 950) * TAU, r0 = R * (.35 + .65 * rand(i + 951)), from = { x: T.x + Math.cos(a) * r0, z: T.z + Math.sin(a) * r0 };
    const ph = (s * 1.3 + rand(i + 952)) % 1, len = .22, at = u => ({ x: lerp(from.x, core.x, u), z: lerp(from.z, core.z, u) });
    const a0 = at(Math.max(0, ph - len)), a1 = at(ph), fade = alpha * Math.sin(ph * Math.PI);
    streak(`${key} ${i} haze`, a0, a1, .16, Core.withAlpha(.1 * fade), undefined, Y + .12, 4);
    streak(`${key} ${i}`, a0, a1, .04, Core.withAlpha(.38 * fade), undefined, Y + .1203, 4);
  }
}

// ---- plates ---------------------------------------------------------------------------------------
// One plate of ground as a solid: shadow, a south side for its thickness, an edge, a face. It turns
// over by squashing along its own axis; past a quarter turn the underside shows.
function slab(key, pl, pos, spin, tumble, scale, sun, strength, layer) {
  const cs = Math.cos(spin), sn = Math.sin(spin), ca = Math.cos(pl.axis), sa = Math.sin(pl.axis);
  const ct = Math.cos(tumble), sq = Math.max(.2, Math.abs(ct));
  const loc = pl.loc.map(q => {
    const x = (q.x * cs - q.z * sn) * scale, z = (q.x * sn + q.z * cs) * scale;
    const a = (x * ca + z * sa) * sq, b = -x * sa + z * ca;
    return { x: a * ca - b * sa, z: a * sa + b * ca };
  });
  const S = scr(pos), shade = { x: pos.x + sun.x * pos.y, z: pos.z + sun.z * pos.y }, o = { x: 0, z: 0 };
  const at = (c, pts) => pts.map(q => ({ x: c.x + q.x, z: c.z + q.z }));
  const thick = pl.thick * scale * (.35 + .65 * sq), faceSet = ct >= 0 ? pl.top : pl.under;
  polyC(`${key} shadow`, shade, at(shade, loc), Ink.withAlpha(strength / (1 + pos.y * .25)), shadowLayer);
  const low = { x: S.x, z: S.z - thick * Lift }, wide = growC(loc, o, .02);
  polyC(`${key} side`, low, at(low, wide), SideC, layer);
  polyC(`${key} edge`, S, at(S, wide), SlabEdge, layer);
  polyC(`${key} face`, S, at(S, loc), Color.Lerp(faceSet.dark, faceSet.lit, .3 + .55 * sq), layer);
}
// One plate on the ball's surface: six corners round its slot direction, pushed out a little so the
// outline is lumpy, lit by the sun and a weak camera-side fill.
function surfacePlate(key, C3, r, m, n, spread, L, layer) {
  const t1 = unit(cross(n, Math.abs(n.y) > .95 ? v3(1, 0, 0) : Up)), t2 = cross(n, t1), out = r * (1 + .07 * rand(m * 5 + 1)), pts = [];
  for (let k = 0; k < 6; k++) {
    const a = (k + (rand(m * 17 + k) - .5) * .5) / 6 * TAU, ext = spread * (.8 + .45 * rand(m * 23 + k));
    pts.push(scr(add(C3, mul(unit(add(n, add(mul(t1, Math.cos(a) * ext), mul(t2, Math.sin(a) * ext)))), out))));
  }
  const c = scr(add(C3, mul(n, out))), kind = rand(m * 11 + 5), set = kind < .12 ? GrassTop : kind < .35 ? SoilTop : kind < .8 ? Earth : Stone;
  const lit = clamp(Math.max(0, dot(n, L)) * .95 + Math.max(0, dot(n, FillFrom)) * .25 + .02) * (n.y < 0 ? 1 + n.y * .45 : 1);
  polyC(`${key} edge`, c, growC(pts, c, .035), SlabEdge, layer);
  polyC(`${key} face`, c, pts, Color.Lerp(set.dark, set.lit, lit), layer);
}
// The ball: shadow on the floor, the plates just past the limb (so the outline is lumpy), the dark
// body, the plates facing the camera back to front, and a darker limb over them.
function ball(key, C3, r, spin, slots, filled, bodyA, L, sun, strength) {
  const S = scr(C3), back = [], front = [];
  sprite({ x: C3.x + sun.x * C3.y, z: C3.z + sun.z * C3.y }, r * 2.3, r * 1.7, Ink.withAlpha(Math.min(.8, strength * 1.5) * bodyA), soft, shadowLayer);
  filled.forEach(m => {
    const n = rotY(slots.dirs[m], spin), nd = dot(n, View);
    if (nd > -.3) (nd < 0 ? back : front).push({ m, n, nd });
  });
  back.sort((a, b) => a.nd - b.nd).forEach(q => surfacePlate(`${key} ${q.m}`, C3, r, q.m, q.n, slots.spread, L, Y + .05));
  draw(disc, S.x, Y + .051, S.z, r * .98, r * .98 * Squash, 0, BallBody.withAlpha(bodyA));
  front.sort((a, b) => a.nd - b.nd).forEach(q => surfacePlate(`${key} ${q.m}`, C3, r, q.m, q.n, slots.spread, L, Y + .052));
  draw(limb, S.x, Y + .08, S.z, r * 1.02, r * 1.02 * Squash, 0, Core.withAlpha(.32 * bodyA));
}
// Seams that run round the ball before it breaks: great circles, only the part facing the camera.
function seams(key, C3, r, spin, u) {
  if (u <= 0) return;
  for (let i = 0; i < 4; i++) {
    const g = rotY(unit(v3(rand(i * 3 + 1) - .5, rand(i * 3 + 2) - .5, rand(i * 3 + 3) - .5)), spin);
    const e1 = unit(cross(g, Math.abs(g.y) > .9 ? v3(1, 0, 0) : Up)), e2 = cross(g, e1), start = rand(i + 40) * TAU, span = Math.min(1, u * 1.5);
    let run = [], piece = 0;
    const flush = () => { if (run.length > 1) line(`${key} ${i} ${piece++}`, run, .03 + .06 * u, CrackC, undefined, Y + .083, 'none'); run = []; };
    for (let k = 0; k <= 48; k++) {
      const a = start + (k / 48 - .5) * TAU * span, d = unit(add(add(mul(e1, Math.cos(a)), mul(e2, Math.sin(a))), mul(g, (rand(i * 60 + k) - .5) * .09)));
      if (dot(d, View) > .06) run.push(scr(add(C3, mul(d, r * 1.04)))); else flush();
    }
    flush();
  }
}
// The burst: every surface slot becomes a chunk thrown out and down; it lands and stays.
function chunks(key, C3, r, spin, slots, M, age, sun, strength) {
  for (let m = 0; m < M; m += 2) {
    const n = rotY(slots.dirs[m], spin), sp = 1.6 + 1.8 * rand(m * 13 + 1), p0 = add(C3, mul(n, r));
    const vx = n.x * sp, vz = n.z * sp, vy = n.y * sp + 1.4, tl = (vy + Math.sqrt(vy * vy + 2 * G * p0.y)) / G;
    const size = .6 + .45 * rand(m * 13 + 2), turn = rand(m * 13 + 3) * 360, dir = m % 2 ? 1 : -1;
    if (age < tl) {
      const q = v3(p0.x + vx * age, p0.y + vy * age - .5 * G * age * age, p0.z + vz * age);
      sprite({ x: q.x + sun.x * q.y, z: q.z + sun.z * q.y }, size * 1.2, size * .8, Ink.withAlpha(strength * .8 / (1 + q.y * .3)), soft, shadowLayer);
      rock(scr(q), size, turn + age * 300 * dir, 1, m, Y + .13);
    } else {
      const land = { x: p0.x + vx * tl, z: p0.z + vz * tl }, la = age - tl;
      rock(land, size, turn + tl * 300 * dir, 1, m, Floor + .03 + (m % 7) * .0005);
      if (m % 3 === 0 && la < .5) sprite({ x: land.x, z: land.z + la * .3 }, .35 + la, .3 + la * .8, DustC.withAlpha(.45 * (1 - la / .5)), puff, Y + .006);
    }
  }
}

export default {
  kit: 'Pain', label: 'Chibaku Tensei (sketch)',
  params: {
    scenario: { label: 'Showcase', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    aim: P('Direction to the cell (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Distance to the cell (cells)', 9, 7, 20, .5, 'Showcase'),
    warm: P('Warm-up (hand up)', .8, .3, 1.5, .05, 'Timing (s)'),
    speed: P('Core flight speed (cells/s)', 14, 6, 30, 1, 'Timing (s)'),
    pull: P('Pull', 3, 1.5, 5, .1, 'Timing (s)'),
    hold: P(`Hold shown (${HoldSeconds} s in the game)`, 2.5, 1, 6, .25, 'Timing (s)'),
    radius: P('Pull radius (cells)', 6, 4, 8, .5, 'Shape'),
    ballR: P('Ball radius (cells)', 2, 1.2, 3, .1, 'Shape'),
    ballH: P('Ball centre height (cells)', 2.8, 2, 5, .1, 'Shape'),
    plate: P('Ground plate size (cells)', 1, .7, 1.4, .05, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Rest', t: 0 }, { name: 'Warm-up', t: t.cast }, { name: 'Launch', t: t.launch }, { name: 'Arrive', t: t.arrive },
      { name: 'Pull', t: t.pull }, { name: 'Hold', t: t.formed }, { name: 'Crack', t: t.crack }, { name: 'Burst', t: t.burst }];
  },
  events(p) {
    const t = times(p), ev = [
      { t: t.cast, type: 'sound', def: 'RimArt_ChibakuCast' }, { t: t.launch, type: 'sound', def: 'RimArt_ChibakuLaunch' },
      { t: t.arrive, type: 'shake', value: .02 }, { t: t.pull, type: 'sound', def: 'RimArt_ChibakuPull' },
    ];
    for (let k = 0; k < 6; k++) ev.push({ t: t.pull + p.pull * (k + .5) / 6, type: 'shake', value: .012 + .004 * k });
    ev.push({ t: t.formed, type: 'shake', value: .05 }, { t: t.formed, type: 'sound', def: 'RimArt_ChibakuFormed' });
    for (let k = 1; k < p.hold; k++) ev.push({ t: t.formed + k, type: 'shake', value: .01 });
    ev.push({ t: t.crack, type: 'sound', def: 'RimArt_ChibakuCrack' }, { t: t.burst, type: 'shake', value: .07 }, { t: t.burst, type: 'sound', def: 'RimArt_ChibakuBurst' });
    return ev;
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32, L = sunLight(sun);
    const f = frame(p.aim, sun), R = p.radius, Rb = p.ballR, H = ballH(p);
    const T = f.ground(o, p.distance * .15, 0), P0 = f.ground(T, -p.distance, 0), C3 = v3(T.x, H, T.z), Cs = scr(C3);

    // Who is where. The raider outside the radius is placed from the radius, so it stays outside.
    const pawns = [];
    const put = (along, across, colour, kind = 'raider') => {
      const g = f.ground(T, along, across), d = Math.hypot(along, across);
      pawns.push({ i: pawns.length, g, d, colour, kind, caught: kind !== 'pinned' && d <= R });
    };
    put(-1.3, -.9, EnemyColour); put(-1.4, .6, EnemyColour); put(-.2, 1.9, EnemyColour); put(1.3, -2.1, EnemyColour); put(2.7, .9, EnemyColour);
    put((R + .8) * Math.cos(-62 * D2R), (R + .8) * Math.sin(-62 * D2R), EnemyColour);
    if (p.scenario === Scenarios[1]) put(-3.4, 2.6, Ally);
    if (p.scenario === Scenarios[2]) put(1, 3, EnemyColour, 'pinned');
    pawns.forEach(q => {
      q.liftAt = t.pull + p.pull * (.05 + .5 * Math.min(1, q.d / R)); q.fly = p.pull * .25; q.arriveAt = q.liftAt + q.fly;
      const a = rand(q.i * 9 + 400) * TAU; q.toward = { x: Math.cos(a), z: Math.sin(a) };
      q.drop = { x: (rand(q.i * 9 + 401) - .5) * 1.1, z: (rand(q.i * 9 + 402) - .5) * .9 };
    });

    // The plates: the ones under a building or a pinned pawn stay; the rest are pulled, in arrival order.
    const anchors = [-1, 0, 1].map(c => f.ground(T, SandAlong, c)).concat(pawns.filter(q => q.kind === 'pinned').map(q => q.g));
    const caught = [], kept = [];
    plateGeo(R, p.plate).forEach(pl => {
      const w = { x: T.x + pl.c.x, z: T.z + pl.c.z }, q = { pl, w, abs: pl.pts.map(v => ({ x: T.x + v.x, z: T.z + v.z })), ...plateTimes(pl, p, t, R) };
      (anchors.some(a => Math.hypot(a.x - w.x, a.z - w.z) < IslandR) ? kept : caught).push(q);
    });
    caught.sort((a, b) => a.arriveAt - b.arriveAt);
    const N = caught.length, M = Math.min(MaxSlots, N), slots = slotSet(M);
    caught.forEach((q, k) => { q.slot = slots.perm[k % M]; });
    let arrived = 0;
    while (arrived < N && caught[arrived].arriveAt <= s) arrived++;
    const frac = N ? arrived / N : 1, spin = Spin * D2R * Math.max(0, s - t.pull);
    const squeeze = s >= t.formed && s < t.crack ? bump(clamp(((s - t.formed) % 1) / .18)) : 0;
    const rBall = s < t.formed ? Rb * (.3 + .7 * Math.cbrt(frac)) : Rb * (1 - .03 * squeeze);

    // --- the floor: area marker, cracks, crater, rim -------------------------------------------------------
    if (s >= t.arrive) {
      const spread = easeOut((s - t.arrive) / .3), fade = 1 - clamp((s - t.formed - .6) / .6);
      draw(disc, T.x, Floor + .008, T.z, R * spread, R * spread, 0, Core.withAlpha(.1 * fade));
      darkEdge(T, R * spread, .6 * fade, Floor + .009);
    }
    const outline = (key, q, a) => line(key, [...q.abs, q.abs[0]], .05, CrackC.withAlpha(.75 * a), undefined, Floor + .02, 'none');
    kept.forEach(q => { if (s >= q.crackAt) outline(`chibaku kept crack ${q.pl.i}`, q, clamp((s - q.crackAt) / .1)); });
    caught.forEach(q => {
      if (s < q.crackAt) return;
      if (s < q.heaveAt) { outline(`chibaku crack ${q.pl.i}`, q, clamp((s - q.crackAt) / .1)); return; }
      polyC(`chibaku rib ${q.pl.i}`, q.w, q.abs, Rib, Floor + .013);
      polyC(`chibaku hole ${q.pl.i}`, q.w, growC(q.abs, q.w, -.045), Color.Lerp(HoleEdge, HoleDeep, 1 - q.pl.r / R), Floor + .014);
      if (s < q.liftAt) {       // heaving: rises a little and rocks before it tears free
        const u = smooth((s - q.heaveAt) / (q.liftAt - q.heaveAt)), jig = Math.sin(s * 55 + q.pl.i) * .02 * u;
        slab(`chibaku slab ${q.pl.i}`, q.pl, v3(q.w.x + jig, HeaveH * u, q.w.z), 0, .45 * u, 1, sun, strength, buildingLayer);
      }
      if (q.pl.i % 2 === 0 && s >= q.liftAt && s < q.liftAt + .8) {
        const u = (s - q.liftAt) / .8;
        sprite({ x: q.w.x, z: q.w.z + u * .45 }, .5 + u * .7, .42 + u * .6, DustC.withAlpha(.42 * (1 - u)), puff, Y + .004);
      }
    });
    // The crater is deepest in the middle: a soft dark spot over the holes once the middle has gone.
    const bowl = smooth(clamp((s - t.pull - p.pull * .2) / (p.pull * .6)));
    if (bowl > 0) sprite(T, R * 1.7, R * 1.7, Core.withAlpha(.3 * bowl), soft, Floor + .0145);
    for (let i = 0; i < 12; i++) {
      const a = (i + rand(i + 300) * .6) / 12 * TAU, at = { x: T.x + Math.cos(a) * (R + .15), z: T.z + Math.sin(a) * (R + .15) };
      crack(`chibaku rim ${i}`, at, smooth(clamp((s - t.pull - CrackRun) / .15)), 60 + i * 7);
      const born = t.pull + p.pull * (.6 + .1 * rand(i + 310)), u = clamp((s - born) / .35);
      if (s >= born) {
        const d = R + .1 + .5 * rand(i + 311) * easeOut(u), hh = .35 * Math.sin(u * Math.PI);
        rock({ x: T.x + Math.cos(a + .1) * d, z: T.z + Math.sin(a + .1) * d + hh * Lift }, .14 + .1 * rand(i + 312), a / D2R + u * 200, 1, i, u < 1 ? Y + .006 : Floor + .03);
      }
    }
    sandbags(f, T, sun, strength);

    // --- on the ground, north first: Pain, standing, staggering, pinned and fallen pawns ------------------
    const ground = [{ z: P0.z, draw: () => pain(P0, sun, strength) }];
    const air = [];     // { pos (3D), draw(layer) }: sorted later into behind and in front of the ball
    const falling = [];
    pawns.forEach(q => {
      const key = `chibaku pawn ${q.i}`;
      if (q.kind === 'pinned') { ground.push({ z: q.g.z, draw: () => pinned(key, q.g, sun, strength) }); return; }
      const pulling = s >= t.pull && s < t.formed;
      if (!q.caught) {
        const lean = pulling ? .3 * smooth(clamp((s - t.pull) / .3)) : 0, g = { x: q.g.x + (pulling ? Math.sin(s * 40 + q.i) * .02 : 0), z: q.g.z };
        ground.push({ z: g.z, draw: () => { lean > 0 ? body(key, g, 0, q.colour, leanAng(g, T, lean), .3, s, sun, strength, pawnLayer) : standing(g, q.colour, sun, strength); if (pulling) kick(g, s - t.pull, .8, 20 + q.i); } });
        return;
      }
      if (s < t.pull) { ground.push({ z: q.g.z, draw: () => standing(q.g, q.colour, sun, strength) }); return; }
      if (s < q.liftAt) {
        const lean = .45 * smooth(clamp((s - t.pull) / .25)), g = { x: q.g.x + Math.sin(s * 47 + q.i) * .03, z: q.g.z };
        ground.push({ z: g.z, draw: () => { body(key, g, 0, q.colour, leanAng(g, T, lean), .6, s, sun, strength, pawnLayer); kick(g, s - t.pull, 1, 30 + q.i); } });
        return;
      }
      if (s < q.arriveAt) {       // pulled in, flailing and turning over
        const u = Math.pow((s - q.liftAt) / q.fly, 1.6), G3 = v3(q.g.x, 0, q.g.z), dir = unit(add(G3, mul(C3, -1)));
        const pos = add(mul(G3, 1 - u), mul(add(C3, mul(dir, rBall * 1.05)), u));
        pos.y += .25 * smooth(clamp((s - q.liftAt) / (.2 * q.fly))) * (1 - u);
        const ang = rand(q.i + 70) * TAU + (s - q.liftAt) * 5.5;
        air.push({ pos, draw: layer => body(key, { x: pos.x, z: pos.z }, pos.y, q.colour, ang, 1, s, sun, strength, layer) });
        return;
      }
      if (s < t.burst) return;      // inside the ball
      const start = add(C3, v3(q.drop.x * .4, -.3, q.drop.z * .4)), tf = Math.sqrt(2 * start.y / G), age = s - t.burst;
      if (age < tf) {
        const k = age / tf, g = { x: start.x + q.drop.x * k, z: start.z + q.drop.z * k }, h = start.y - .5 * G * age * age;
        falling.push(() => body(key, g, h, q.colour, rand(q.i + 71) * TAU + age * 6, 1, s, sun, strength, Y + .13));
      } else {
        const g = { x: start.x + q.drop.x, z: start.z + q.drop.z }, la = age - tf;
        ground.push({ z: g.z, draw: () => {
          lying(key, g, q.colour, q.toward, sun, strength);
          if (la < .45) sprite({ x: g.x, z: g.z + .1 + la * .4 }, .6 + la * 1.4, .5 + la, DustC.withAlpha(.5 * (1 - la / .45)), puff, Y + .007);
          const head = { x: g.x + q.toward.x * .4, z: g.z + .08 + q.toward.z * .4 };
          stunStars(`${key} stun`, { x: head.x, z: head.z + .22 - .84 }, s, clamp(la / .15) * clamp((StunSeconds - la) / .2));
        } });
      }
    });
    ground.sort((m, n) => n.z - m.z).forEach(q => q.draw());

    // --- Pain's arm and the core over his palm; the core's flight to the cell -------------------------------
    const armUp = smooth(clamp((s - t.cast) / (p.warm * .6))), armDown = smooth(clamp((s - t.formed - .3) / .4));
    const reach = Reach * armUp * (1 - armDown), handH = lerp(lerp(HandH, RaisedH, armUp), HandH * .7, armDown);
    const hand = f.place(P0, .12 + reach, -.1, handH);
    if (armUp > 0 && armDown < 1) arm('chibaku arm', f.place(P0, .05, -.1, ShoulderH), hand, { x: f.ca, z: f.sa }, 0);
    const warmU = clamp((s - t.cast) / p.warm);
    if (s >= t.cast && s < t.launch) blackCore('chibaku palm', f.place(P0, .12 + reach + .3, -.1, handH + .12), PalmR * smooth(clamp(warmU * 1.25)), s, smooth(clamp(warmU * 1.4)), Y + .1);
    if (s >= t.launch && s < t.arrive) {
      const g0 = f.ground(P0, .12 + Reach + .3, -.1), A3 = v3(g0.x, RaisedH + .12, g0.z), u = (s - t.launch) / (t.arrive - t.launch);
      const at = k => scr(add(mul(A3, 1 - k), mul(C3, k))), trail = [];
      for (let k = 0; k <= 6; k++) trail.push(at(Math.max(0, u - k * .045)));
      line('chibaku trail', trail, .2, Core.withAlpha(.35), undefined, Y + .098, 'end');
      blackCore('chibaku flying', at(u), PalmR * 1.1, s, 1, Y + .1);
    }

    // --- in the air: flying plates and pawns, split by depth round the ball ---------------------------------
    caught.forEach(q => {
      if (s < q.liftAt || s >= q.arriveAt) return;
      const age = s - q.liftAt, u = Math.pow(age / q.fly, 1.7), G3 = v3(q.w.x, HeaveH, q.w.z);
      const target = add(C3, mul(rotY(slots.dirs[q.slot], spin), rBall * 1.05));
      const pos = add(mul(G3, 1 - u), mul(target, u));
      pos.y += .2 * smooth(clamp(age / (.25 * q.fly))) * (1 - u);
      air.push({ pos, draw: layer => slab(`chibaku slab ${q.pl.i}`, q.pl, pos, age * q.pl.spinRate, .3 + age * q.pl.tumbleRate, lerp(1, .6, u), sun, strength, layer) });
    });
    air.forEach(a => { a.nd = dot(add(a.pos, mul(C3, -1)), View); });
    air.sort((a, b) => a.nd - b.nd);
    air.filter(a => a.nd < 0).forEach(a => a.draw(Y + .02));

    // The core over the cell, and the ball that grows round it.
    if (s >= t.arrive && s < t.formed + .2) {
      const grown = lerp(.65, 1, smooth(clamp((s - t.arrive) / Pulse)));
      blackCore('chibaku core', Cs, CoreR * grown, s, 1, Y + .045, 1 - smooth(frac));
      if (s < t.arrive + .2) { const u = (s - t.arrive) / .2, size = lerp(1, 3.2, easeOut(u)); sprite(Cs, size, size, PaleBlue.withAlpha(.55 * (1 - u)), glow, Y + .046); }
    }
    if (s >= t.pull && s < t.burst) {
      const filled = [];
      for (let k = 0; k < Math.min(arrived, M); k++) filled.push(caught[k].slot);
      ball('chibaku ball', C3, rBall, spin, slots, filled, s < t.formed ? smooth(clamp(frac * 1.6)) : 1, L, sun, strength);
      seams('chibaku seam', C3, rBall, spin, clamp((s - t.crack) / CrackTime));
      if (s >= t.crack) for (let i = 0; i < 8; i++) {      // dust spurting from the seams
        const u = ((s - t.crack) * 2.5 + rand(i + 600)) % 1, a = rand(i + 601) * TAU;
        const at = scr(add(C3, mul(v3(Math.cos(a) * .8, .3 + rand(i + 602) * .5, Math.sin(a) * .8 - .4), rBall)));
        sprite({ x: at.x, z: at.z + u * .3 }, .3 + u * .5, .26 + u * .4, DustC.withAlpha(.5 * Math.sin(u * Math.PI)), puff, Y + .085);
      }
    }
    if (s >= t.formed - .3 && s < t.burst) for (let i = 0; i < 7; i++) {    // a few rocks circling the ball
      const a = s * (.6 + .15 * rand(i + 500)) + i * .9, rr = Rb * (1.12 + .1 * rand(i + 501)), y = Math.sin(i * 2.3) * Rb * .5;
      const q = v3(C3.x + Math.cos(a) * rr, C3.y + y, C3.z + Math.sin(a) * rr), front = dot(add(q, mul(C3, -1)), View) >= 0;
      rock(scr(q), .12 + .08 * rand(i + 502), a / D2R * 3, clamp((s - t.formed + .3) / .3), i, front ? Y + .085 : Y + .044);
    }
    air.filter(a => a.nd >= 0).forEach(a => a.draw(Y + .09));
    if (s >= t.pull && s < t.formed) inflow('chibaku inflow', T, R, Cs, s, clamp((s - t.pull) / .2) * (1 - smooth(clamp((frac - .7) / .3))));

    // --- the burst ------------------------------------------------------------------------------------------
    if (s >= t.burst) {
      const age = s - t.burst;
      chunks('chibaku chunk', C3, Rb, spin, slots, M, age, sun, strength);
      falling.forEach(d => d());
      if (age < .15) sprite(Cs, Rb * 3, Rb * 3 * Squash, PaleBlue.withAlpha(.5 * (1 - age / .15)), glow, Y + .15);
      for (let i = 0; i < 18; i++) {
        const life = 1 + rand(i + 700) * .4, u = age / life;
        if (u >= 1) continue;
        const a = rand(i + 701) * TAU, d = Rb * (.15 + .75 * rand(i + 702)) * (1 + .7 * easeOut(u));
        sprite({ x: Cs.x + Math.cos(a) * d, z: Cs.z + Math.sin(a) * d * Squash - u * .6 }, 1.1 + u * 1.5, .95 + u * 1.3, DustC.withAlpha(.5 * (1 - u) * clamp(u * 12)), puff, Y + .14);
      }
      for (let i = 0; i < 6; i++) {
        const u = clamp((age - .3) / 2.2);
        if (u <= 0 || u >= 1) continue;
        const a = i * 1.05 + .3, d = Rb * (.4 + u * .8);
        sprite({ x: T.x + Math.cos(a) * d, z: T.z + Math.sin(a) * d * .7 + u * .4 }, 1.6 + u * 1.4, 1.2 + u * 1.1, DustC.withAlpha(.32 * Math.sin(u * Math.PI)), puff, Y + .005);
      }
    }

    eyeStar({ x: P0.x, z: P0.z + .6 }, .28, bump(clamp((s - t.cast) / .35)));
  },
};
