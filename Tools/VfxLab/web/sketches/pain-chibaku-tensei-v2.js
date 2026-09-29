// Chibaku Tensei v2 (Planetary Devastation): the same ability as pain-chibaku-tensei.js, drawn closer to
// the source. Proposed 2026-09-29, NOT agreed. The mechanic, timing and numbers are v1's (see its header):
// warm-up 0.8 s, the core at 14 cells/s to 5 cells over the cell, pull 3 s radius 6, ball radius 2 holds
// 12 s, crack 0.4 s, burst; cooldown 1 day, Echo charge 30. v1 stays for comparison.
//
// Source frames:
//   anime (YouTube mAnSrmI_J40) 0:46-0:48 the core forms as a white glow between cupped hands and one hand
//   throws it straight up; 0:50, 1:00-1:18 a black core in a yellow-white glare with long rays, the rocks
//   rising to it as black shapes against the light; 0:58 (and ciUsoZHSeeo 0:06) Pain holds his palms
//   pressed together at the chest while the ground rises; 1:20-1:28 dark cracks, then slabs tip up; 2:26
//   rocks hang in a tan dust haze; 2:18, 2:56-3:04 the ball is lumpy packed rock with a bright yellow line
//   of light on its edge; 3:14 seen from below, a glowing underside and white cloud swirling round it.
//   Storm Connections (g7tqyejSOHA 0:05, 5:45) a stone ball with rocks trailing under it; Nagato's
//   (9W4amF9IW4k 0:13-0:15) breaks with light pouring from the cracks, then a flash. Naruto Mobile
//   (gxBNX3cgINk 0:45-0:48) a wide white flash at the burst.
//
// What changed from v1, by phase:
//   Pain    warm-up: the hands cup at the chest and the core forms between them in a white glow; the last
//           0.18 s the near hand throws it straight up. From 0.1 s after the launch until the ball has
//           formed both palms are pressed together at the chest, fingers up, trembling more as the pull
//           goes on; then the hands drop (Pain is free at formed + 0.7 s, as in v1).
//   core    a black sun: a black disc with a thin white rim in a glare of 3 additive layers (white, pale
//           yellow, warm) out to about 1.4 cells, and 14 thin rays up to 3 cells that turn slowly and
//           flicker. It climbs straight up from the hand to 2.5 cells over the ball's height and comes down
//           onto its place (same speed). Arrival: a 0.15 s flash. While the core shows, a faint warm
//           light lies on the ground inside the radius.
//   pull    flying plates darken to a silhouette (about 25 % brightness) as they near the core, with a
//           pale-yellow edge on the core side; tan dust rises from the torn plates and drifts in; a tan
//           haze hangs under the ball; small rocks spiral up from the crater into the ball (fewer during
//           the hold). The glare dims once about half the plates have arrived.
//   ball    earth and stone side out (about 15 % grass); 24 boulders set into the surface so the outline
//           is lumpy; warm light shows in the gaps between the plates and brightens on each 1 s squeeze;
//           a pale-yellow rim light, brightest on the lower edge; a ring of pale cloud swirls round the
//           middle at 1.3-1.8 times the radius, 25 degrees/s, the far half behind the ball; a small rock
//           falls off the underside about every 0.45 s and stays on the crater floor.
//   crack   over the last 1.2 s of the hold, 9 dark hairline cracks with side branches run out over the
//           camera side of the ball (straight pieces with kinks, thick where they start, a hairline at
//           the tip: rock cracking, not lightning). In the 0.4 s crack phase they widen and fill with the
//           core's warm light (the dark lip stays wider than the light), every plate pulls apart from its
//           neighbours so light shows along all the seams, the ball shakes, dust spurts from the cracks
//           and 18 chips are spat out; they fall and stay.
//   burst   a 0.12 s flash; the rocks fly out as in v1; a dust ring rolls out along the ground; the cloud
//           ring blows out and fades.
//   crater  lower-contrast ribs between the holes (fewer tile lines), a darker middle, a lighter lip.
//
// Drawing: as v1, no per-facing method (level circles, fan polygons, the ball's outline is the height
// rule's ellipse). Light is additive soft sprites and strips (MoteGlow), never an opaque shape. The light
// between the ball's plates is an additive glow over the dark body and under the plates, so it only shows
// in the gaps. Pain, his hands and the pawns are stand-ins; in game the hands need a clip of their own.
import { AltitudeLayer, Color, Mathf, Mesh, Meshes } from '../js/engine.js';
import { draw, mesh, v3, add, mul, dot, unit, cross, Up, View, FillFrom, sunLight } from './lib/six-paths-solid.js';
import { P, Y, Floor, Lift, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import { rock, line, stunStars, streak, strip, whiteGlow, Skin, EnemyColour, Ink } from './lib/goku.js';
import { frame, crack, kick, puff, rect, bump, easeOut, Ally } from './lib/chain-sickle.js';
import { eyeStar } from './lib/amenoyodomi.js';
import { Core, DustC, BodyZ, ShoulderH, pain, standing, lying, arm } from './lib/pain.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2, D2R = Mathf.Deg2Rad;
const disc = Meshes.disc(48, 'chibaku2 disc');
const limb = Meshes.band(.8, 1, 72, 'chibaku2 limb');
const coreRim = Meshes.band(.84, 1, 48, 'chibaku2 core rim');
const rimBand = Meshes.band(.95, 1, 96, 'chibaku2 rim light');
const lipBand = Meshes.band(.84, 1, 96, 'chibaku2 lip');
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), buildingLayer = AltitudeLayer.Building.AltitudeFor();
const pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const Squash = Math.sqrt(1 + Lift * Lift);   // a sphere's outline on screen is this much taller than wide

// The lower edge of a unit circle, from 200 to 340 degrees (south), thickest in the middle: the rim light,
// a thin bright arc and a wide soft one round it.
const arcMesh = (name, thick) => {
  const m = new Mesh(name), v = [], tri = [], n = 28;
  for (let i = 0; i <= n; i++) {
    const u = i / n, a = (200 + 140 * u) * D2R, w = thick * Math.sin(u * Math.PI);
    v.push(Math.cos(a) * (1 - w), Math.sin(a) * (1 - w), Math.cos(a) * (1 + w * .35), Math.sin(a) * (1 + w * .35));
    if (i) { const k = i * 2; tri.push(k - 2, k, k - 1, k - 1, k, k + 1); }
  }
  m.setFlat(v, tri);
  return m;
};
const lowArc = arcMesh('chibaku2 low arc', .07), lowArcWide = arcMesh('chibaku2 low arc wide', .2);
// Boulders set into the ball: six irregular outlines of 5 to 7 corners, about 1 cell across before scaling
// (rough enough not to read as a regular polygon).
const BoulderShapes = [0, 1, 2, 3, 4, 5].map(v => {
  const m = new Mesh(`chibaku2 boulder ${v}`), n = 5 + v % 3, vertices = [0, 0], tri = [];
  for (let i = 0; i < n; i++) {
    const a = (i + (rand(v * 13 + i + 70) - .5) * .8) / n * TAU, r = .5 * (.55 + .45 * rand(v * 29 + i + 90)) * (i % 2 ? .9 : 1.05);
    vertices.push(Math.cos(a) * r, Math.sin(a) * r);
    tri.push(0, 1 + i, 1 + (i + 1) % n);
  }
  m.setFlat(vertices, tri);
  return m;
});

// Colours. Plate tops match the lab's grass and soil; undersides are earth or stone.
const pair = (dark, lit) => ({ dark, lit });
const GrassTop = pair(new Color(.24, .28, .12), new Color(.47, .52, .26)), SoilTop = pair(new Color(.27, .2, .13), new Color(.5, .38, .26));
const Earth = pair(new Color(.19, .13, .08), new Color(.46, .34, .22)), Stone = pair(new Color(.22, .21, .2), new Color(.56, .54, .5));
const SideC = new Color(.13, .09, .06), SlabEdge = new Color(.07, .05, .035), BallBody = new Color(.08, .06, .04);
const HoleDeep = new Color(.11, .08, .05), HoleEdge = new Color(.24, .18, .12), Rib = new Color(.31, .24, .16), CrackC = new Color(.06, .04, .03);
const RibSoft = Color.Lerp(HoleEdge, Rib, .45), Lip = new Color(.42, .33, .22);
const Bag = new Color(.64, .57, .42), BagDark = new Color(.36, .31, .22), RodC = new Color(.05, .05, .07), RodLit = new Color(.42, .42, .48);
// The core's light (the anime's yellow-white sky), the rim and seam light, the dust and the cloud.
const SunWhite = new Color(1, 1, 1), SunPale = new Color(1, .94, .72), SunWarm = new Color(1, .76, .4);
const RimLight = new Color(1, .88, .58), Seam = new Color(1, .74, .38), Silhouette = new Color(.07, .055, .045);
const DustTan = new Color(.72, .62, .46), CloudC = new Color(.9, .9, .88);

// The rule's fixed numbers (placeholders; shown in the header, not all drawn).
const HoldSeconds = 12, StunSeconds = 2.5;
// Decided timing and shape.
const Lead = .2, Pulse = .25, CrackRun = .45, CrackTime = .4, Tail = 2.8;
const CoreR = .34, PalmR = .08;   // the core over the ball, and between the hands
const Spin = 14;                  // degrees per second the ball turns
const MaxSlots = 84;              // plates drawn on the ball's surface
const Boulders = 24;              // boulders set into it
const HeaveH = .18, IslandR = .8;  // how far a plate rises before it tears free; ground kept round anything anchored
const G = 12;                     // cells/s², for what falls out of the ball
const SandAlong = -2.3;           // the sandbag wall, cells from the centre toward Pain
// Pain's hands, [cells along the aim, across it, up] from his cell, and their timing.
const Rest = [[.04, .2, .3], [.04, -.2, .3]], Cup = [[.24, .06, .5], [.24, -.06, .5]], Seal = [[.2, .028, .56], [.2, -.028, .56]];
const ThrowH = 1.05, Raised = [.1, .14, ThrowH];
const CupIn = .3, ThrowTime = .18, SealAfter = .1, SealIn = .35, DownAfter = .3, DownTime = .4;
const Climb = 2.5;                // cells the core climbs over the ball's height before it comes down onto it
const CloudTurn = 25;             // degrees per second the cloud ring turns
const DribbleEvery = .45;         // seconds between rocks falling off the ball's underside
const CrackCreep = 1.2;           // seconds at the end of the hold over which hairline cracks run out

const Scenarios = ['raiders behind sandbags', 'a colonist inside the radius too', 'a raider pinned by 3 rods'];

// The core's path in the aim's plane, as [cells along the aim from Pain's cell, height]: straight up out of
// the raised hand, over the top and down onto its place (a quadratic curve through K).
const bez = (A, K, C, u) => [0, 1].map(j => (1 - u) * (1 - u) * A[j] + 2 * u * (1 - u) * K[j] + u * u * C[j]);
function corePath(p) {
  const A = [Raised[0] + .02, ThrowH + .12], C = [p.distance, ballH(p)], K = [A[0], Math.max(C[1], A[1]) + Climb];
  let len = 0, prev = A;
  for (let i = 1; i <= 32; i++) { const q = bez(A, K, C, i / 32); len += Math.hypot(q[0] - prev[0], q[1] - prev[1]); prev = q; }
  return { A, K, C, len };
}
function times(p) {
  const cast = Lead, launch = cast + p.warm, path = corePath(p);
  const arrive = launch + path.len / p.speed, pull = arrive + Pulse, formed = pull + p.pull;
  const crackAt = formed + p.hold, burst = crackAt + CrackTime;
  return { cast, launch, arrive, pull, formed, crack: crackAt, burst, end: burst + Tail, free: formed + DownAfter + DownTime, path };
}
const ballH = p => Math.max(p.ballH, p.ballR + .4);

// ---- small geometry -------------------------------------------------------------------------------
const scr = q => ({ x: q.x, z: q.z + q.y * Lift });
const rotY = (n, a) => { const c = Math.cos(a), s = Math.sin(a); return v3(n.x * c - n.z * s, n.y, n.x * s + n.z * c); };
const polar = (r, a) => ({ x: Math.cos(a) * r, z: Math.sin(a) * r });
const mix = (a, b, k) => a.map((v, j) => lerp(v, b[j], k));
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
// Each straight edge gets a bend in the middle, worked out from the two corners in a fixed order, so both
// plates that share the edge bend it the same way and the cracks still line up.
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
// The ground plates: v1's Voronoi tiling of the disc (jittered hex seeds, clipped to the circle).
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
// Directions on a Fibonacci sphere; the ball's plate slots are filled in a shuffled order.
function fibonacci(M, turn = 0) {
  const dirs = [];
  for (let m = 0; m < M; m++) { const y = 1 - 2 * (m + .5) / M, rr = Math.sqrt(1 - y * y), ph = m * 2.399963 + turn; dirs.push(v3(rr * Math.cos(ph), y, rr * Math.sin(ph))); }
  return dirs;
}
const slotCache = new Map();
function slotSet(M) {
  if (slotCache.has(M)) return slotCache.get(M);
  const dirs = fibonacci(M);
  const out = { dirs, perm: dirs.map((_, m) => m).sort((a, b) => rand(a * 7 + 3) - rand(b * 7 + 3)), spread: 1.3 * Math.sqrt(4 / Math.max(1, M)) };
  slotCache.set(M, out);
  return out;
}
const BoulderDirs = fibonacci(Boulders, 1.1);

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
    rect(`chibaku2 bags shadow ${c}`, { x: g.x + sun.x * .3, z: g.z + sun.z * .3 }, .8, 1, aim, Ink.withAlpha(strength * .9), shadowLayer);
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
// Pain's two hands at time s: where each is ([along, across, up]), where its fingers point ('aim' or
// 'up') and how far they are closed. null before the cast and once his hands are down.
function hands(s, t, p) {
  if (s < t.cast || s >= t.free) return null;
  const cup = smooth(clamp((s - t.cast) / CupIn)), throwAt = t.launch - ThrowTime;
  let h0 = mix(Rest[0], Cup[0], cup), h1 = mix(Rest[1], Cup[1], cup), d0 = 'aim', d1 = 'aim', g0 = .55, g1 = .55;
  if (s >= throwAt) {           // the near hand throws the core straight up, speeding up to the release
    const up = Math.pow(clamp((s - throwAt) / ThrowTime), 2);
    h0 = mix(Cup[0], Raised, up); g0 = lerp(.55, .05, up);
    if (up > .5) d0 = 'up';
  }
  if (s >= t.launch + SealAfter) {   // palms pressed together at the chest, fingers up, trembling with the pull
    const k = smooth(clamp((s - t.launch - SealAfter) / SealIn));
    h0 = mix(Raised, Seal[0], k); h1 = mix(Cup[1], Seal[1], k);
    if (k > .5) { d0 = d1 = 'up'; g0 = g1 = .9; }
    const A = (s < t.pull ? .003 : lerp(.004, .02, clamp((s - t.pull) / p.pull))) * k;
    const j = [Math.sin(s * 47) * A, Math.sin(s * 59 + 1) * A * .6, Math.sin(s * 53 + 2) * A];
    h0 = h0.map((v, i) => v + j[i]); h1 = h1.map((v, i) => v + j[i]);
  }
  if (s >= t.formed + DownAfter) {
    const k = smooth(clamp((s - t.formed - DownAfter) / DownTime));
    h0 = mix(h0, Rest[0], k); h1 = mix(h1, Rest[1], k);
    if (k > .5) { d0 = d1 = 'aim'; g0 = g1 = .5; }
  }
  return [{ at: h0, dir: d0, grip: g0 }, { at: h1, dir: d1, grip: g1 }];
}

// ---- the force ------------------------------------------------------------------------------------
// The black core as the anime draws it in the sky: a black disc with a thin white rim in a yellow-white
// glare, and thin rays that turn slowly and flicker. `light` scales the glare and rays alone, so the ball
// can cover the core and leave no halo round it.
function blackSun(key, c, r, s, alpha, layer, light = 1) {
  if (r <= .005 || alpha <= 0) return;
  const k = r / CoreR, L = alpha * light;
  if (L > .001) {
    sprite(c, 2.8 * k, 2.8 * k, SunWarm.withAlpha(.24 * L), glow, layer);
    sprite(c, 1.7 * k, 1.7 * k, SunPale.withAlpha(.42 * L), glow, layer + .0002);
    sprite(c, 1.05 * k, 1.05 * k, SunWhite.withAlpha(.6 * L), glow, layer + .0004);
    for (let i = 0; i < 14; i++) {
      const ang = (i / 14 * 360 + s * 6 + (rand(i + 960) - .5) * 14) * D2R, flick = .6 + .4 * Math.sin(s * 17 + i * 2.1);
      const len = (i % 2 ? .8 + .6 * rand(i + 961) : 1.6 + 1.4 * rand(i + 962)) * k * flick, w = (i % 2 ? .05 : .08) * k;
      const a0 = { x: c.x + Math.cos(ang) * r * 1.05, z: c.z + Math.sin(ang) * r * 1.05 };
      streak(`${key} ray ${i}`, a0, { x: a0.x + Math.cos(ang) * len, z: a0.z + Math.sin(ang) * len }, w, SunPale.withAlpha(.5 * L * flick), whiteGlow, layer + .0006, 6);
    }
  }
  draw(coreRim, c.x, layer + .0008, c.z, r * 1.1, r * 1.1, 0, SunWhite.withAlpha(.85 * alpha), whiteGlow);
  const pts = [];
  for (let i = 0; i < 44; i++) {
    const a = i / 44 * TAU, wob = 1 + .03 * Math.sin(2 * a + s * 4) + .02 * Math.sin(3 * a - s * 6);
    pts.push({ x: c.x + Math.cos(a) * r * wob, z: c.z + Math.sin(a) * r * wob });
  }
  polyC(`${key} core`, c, pts, Core.withAlpha(alpha), layer + .001);
}
// A dark edge on the area's circle: darkest at the rim, fading inward.
const EdgeBands = [[.97, .4], [.93, .2], [.89, .13], [.84, .1], [.79, .07], [.73, .05]]
  .map(([inner, a]) => ({ mesh: Meshes.band(inner, 1, 96, `chibaku2 edge ${inner}`), a }));
function darkEdge(c, R, alpha, layer) {
  EdgeBands.forEach((b, k) => draw(b.mesh, c.x, layer + k * .0002, c.z, R, R, 0, Core.withAlpha(b.a * alpha)));
}
// Faint dark streaks from all over the area into the core.
function inflow(key, T, R, core, s, alpha) {
  if (alpha <= 0) return;
  for (let i = 0; i < 22; i++) {
    const a = rand(i + 950) * TAU, r0 = R * (.35 + .65 * rand(i + 951)), from = { x: T.x + Math.cos(a) * r0, z: T.z + Math.sin(a) * r0 };
    const ph = (s * 1.3 + rand(i + 952)) % 1, len = .22, at = u => ({ x: lerp(from.x, core.x, u), z: lerp(from.z, core.z, u) });
    const a0 = at(Math.max(0, ph - len)), a1 = at(ph), fade = alpha * Math.sin(ph * Math.PI);
    streak(`${key} ${i} haze`, a0, a1, .16, Core.withAlpha(.07 * fade), undefined, Y + .12, 4);
    streak(`${key} ${i}`, a0, a1, .04, Core.withAlpha(.25 * fade), undefined, Y + .1203, 4);
  }
}
// Small rocks spiralling up from the crater into the ball's underside. Each makes trips of 1.1-1.6 s;
// a trip starts only between `from` and `to`. Returned as air things for the depth split round the ball.
function stream(key, T, C3, rBall, R, s, from, to, count, seed) {
  const out = [];
  if (s < from) return out;
  for (let i = 0; i < count; i++) {
    const cyc = 1.1 + .5 * rand(seed + i * 3), since = s - from, u = (since / cyc + rand(seed + i * 3 + 1)) % 1, start = s - u * cyc;
    if (start < from || start > to) continue;
    const a = rand(seed + i * 3 + 2) * TAU + u * 2.6, r0 = 1.2 + (R - 1.6) * rand(seed + i * 5 + 7), rr = lerp(r0, rBall * .6, u * u);
    const q = v3(T.x + Math.cos(a) * rr, lerp(.1, C3.y - rBall * .7, Math.pow(u, 1.4)), T.z + Math.sin(a) * rr);
    out.push({ pos: q, draw: layer => rock(scr(q), .08 + .08 * rand(seed + i * 5 + 8), a / D2R * 4, clamp(u * 8), i, layer) });
  }
  return out;
}

// ---- plates ---------------------------------------------------------------------------------------
// One plate of ground as a solid: shadow, a south side for its thickness, an edge, a face. It turns over
// by squashing along its own axis; past a quarter turn the underside shows. look.dark (0..1) darkens it
// toward a silhouette against the core; look.rim (a screen direction) and look.rimA light its core side.
function slab(key, pl, pos, spin, tumble, scale, sun, strength, layer, look = null) {
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
  const dark = look?.dark ?? 0, tint = c => dark > 0 ? Color.Lerp(c, Silhouette, dark) : c;
  polyC(`${key} shadow`, shade, at(shade, loc), Ink.withAlpha(strength / (1 + pos.y * .25)), shadowLayer);
  const low = { x: S.x, z: S.z - thick * Lift }, wide = growC(loc, o, .02);
  if (look && look.rimA > .01) {
    const R0 = { x: S.x + look.rim.x * .07, z: S.z + look.rim.z * .07 };
    polyC(`${key} rim`, R0, at(R0, wide), RimLight.withAlpha(look.rimA), layer - .0002);
  }
  polyC(`${key} side`, low, at(low, wide), tint(SideC), layer);
  polyC(`${key} edge`, S, at(S, wide), SlabEdge, layer);
  polyC(`${key} face`, S, at(S, loc), tint(Color.Lerp(faceSet.dark, faceSet.lit, .3 + .55 * sq)), layer);
}
// How lit a surface facing n is: the scene sun, a weak camera-side fill, darker underneath.
const litOf = (n, L) => clamp(Math.max(0, dot(n, L)) * .95 + Math.max(0, dot(n, FillFrom)) * .25 + .02) * (n.y < 0 ? 1 + n.y * .45 : 1);
// One plate on the ball's surface: six corners round its slot direction, pushed out a little so the
// outline is lumpy. Mostly earth and stone: the plates came in underside out. open (0..1, the crack
// phase) pulls it apart from its neighbours: out from the centre and a little smaller, so the light
// inside shows along every seam.
function surfacePlate(key, C3, r, m, n, spread, L, layer, open = 0) {
  const t1 = unit(cross(n, Math.abs(n.y) > .95 ? v3(1, 0, 0) : Up)), t2 = cross(n, t1), out = r * (1 + .1 * rand(m * 5 + 1) + .05 * open), pts = [];
  const ext0 = spread * (1 - .15 * open);
  for (let k = 0; k < 6; k++) {
    const a = (k + (rand(m * 17 + k) - .5) * .5) / 6 * TAU, ext = ext0 * (.7 + .38 * rand(m * 23 + k));
    pts.push(scr(add(C3, mul(unit(add(n, add(mul(t1, Math.cos(a) * ext), mul(t2, Math.sin(a) * ext)))), out))));
  }
  const c = scr(add(C3, mul(n, out))), kind = rand(m * 11 + 5), set = kind < .15 ? GrassTop : kind < .55 ? Earth : Stone;
  polyC(`${key} edge`, c, growC(pts, c, .02), SlabEdge, layer);
  polyC(`${key} face`, c, pts, Color.Lerp(set.dark, set.lit, litOf(n, L)), layer);
}
// A boulder: a dark outline, the rock lit by `lit`, and a smaller lighter top shifted toward the light.
function boulder(pos, size, angle, lit, variant, layer) {
  if (size <= .01) return;
  const shape = BoulderShapes[variant % BoulderShapes.length], set = variant % 3 === 0 ? Earth : Stone;
  draw(shape, pos.x, layer, pos.z, size * 1.1, size * 1.0, angle, SlabEdge);
  draw(shape, pos.x, layer + .00005, pos.z, size, size * .9, angle, Color.Lerp(set.dark, set.lit, lit * .75));
  // The lit top: a smaller copy pushed toward the light, so it is not a centred copy.
  draw(shape, pos.x - size * .13, layer + .0001, pos.z + size * .14, size * .64, size * .52, angle, Color.Lerp(set.dark, set.lit, Math.min(1, lit * .85 + .2)));
}
// The ball: shadow on the floor, the plates and boulders just past the limb (so the outline is lumpy),
// the dark body, the light inside it (it shows in the gaps), the plates and boulders facing the camera
// back to front, a darker limb, and the rim light on its edge, brightest underneath.
function ball(key, C3, r, spin, slots, filled, bodyA, L, sun, strength, glowK, rimA, boulderFill, open = 0) {
  const S = scr(C3), back = [], front = [], stones = [];
  sprite({ x: C3.x + sun.x * C3.y, z: C3.z + sun.z * C3.y }, r * 2.3, r * 1.7, Ink.withAlpha(Math.min(.8, strength * 1.5) * bodyA), soft, shadowLayer);
  filled.forEach(m => {
    const n = rotY(slots.dirs[m], spin), nd = dot(n, View);
    if (nd > -.3) (nd < 0 ? back : front).push({ m, n, nd });
  });
  BoulderDirs.forEach((d, j) => {
    const grown = clamp(boulderFill - j), n = rotY(d, spin), nd = dot(n, View);
    if (grown > 0 && nd > -.35) stones.push({ j, n, nd, grown });
  });
  const putStone = (b, layer) => {
    const pos = scr(add(C3, mul(b.n, r * (1 + .06 * rand(b.j * 7 + 1) + .06 * open)))), size = r * (.2 + .12 * rand(b.j * 7 + 2)) * b.grown;
    boulder(pos, size, rand(b.j * 7 + 3) * 360, litOf(b.n, L), b.j, layer);
  };
  back.sort((a, b) => a.nd - b.nd).forEach(q => surfacePlate(`${key} ${q.m}`, C3, r, q.m, q.n, slots.spread, L, Y + .05));
  stones.filter(b => b.nd < 0).sort((a, b) => a.nd - b.nd).forEach((b, i) => putStone(b, Y + .0505 + i * .0002));
  draw(disc, S.x, Y + .051, S.z, r * .98, r * .98 * Squash, 0, BallBody.withAlpha(bodyA));
  if (glowK > 0) {
    sprite(S, r * 1.8, r * 1.8 * Squash, Seam.withAlpha(Math.min(1, .8 * glowK) * bodyA), glow, Y + .0511);
    sprite(S, r * 1.15, r * 1.15 * Squash, SunPale.withAlpha(Math.min(1, .5 * glowK) * bodyA), glow, Y + .0512);
  }
  // Cracking open: the light inside is even across the ball, so every opening seam glows, not only the middle.
  if (open > 0) draw(disc, S.x, Y + .0513, S.z, r * .96, r * .96 * Squash, 0, Seam.withAlpha(.55 * open * bodyA), whiteGlow);
  front.sort((a, b) => a.nd - b.nd).forEach(q => surfacePlate(`${key} ${q.m}`, C3, r, q.m, q.n, slots.spread, L, Y + .052, open));
  stones.filter(b => b.nd >= 0).sort((a, b) => a.nd - b.nd).forEach((b, i) => putStone(b, Y + .0525 + i * .0002));
  draw(limb, S.x, Y + .08, S.z, r * 1.02, r * 1.02 * Squash, 0, Core.withAlpha(.32 * bodyA));
  // The rim light lies on the ball's own edge (inside the lumps, not a clean ring round them): a soft wide
  // arc and a thin bright one along the lower edge, where the core's light leaks out under the ball.
  if (rimA > 0) {
    draw(rimBand, S.x, Y + .0802, S.z, r * .97, r * .97 * Squash, 0, RimLight.withAlpha(.07 * rimA * bodyA), whiteGlow);
    draw(lowArcWide, S.x, Y + .0803, S.z, r * .97, r * .97 * Squash, 0, Seam.withAlpha(.18 * rimA * bodyA), whiteGlow);
    draw(lowArc, S.x, Y + .0804, S.z, r * .97, r * .97 * Squash, 0, RimLight.withAlpha(.5 * rimA * bodyA), whiteGlow);
  }
}
// The ring of cloud swirling round the ball's middle: soft puffs and a few swirl streaks. Only the half on
// the given side of the ball (front: toward the camera). blow 0..1 throws it outward as it fades.
function cloudRing(key, C3, r, s, amount, blow, front) {
  if (amount <= .001 || blow >= 1) return;
  const rot = s * CloudTurn * D2R, layer = front ? Y + .096 : Y + .043, a = amount * (1 - blow), out = 1 + 1.6 * easeOut(blow);
  const isFront = q => dot(add(q, mul(C3, -1)), View) >= 0;
  // Puffs in clumps along the ring (not evenly spaced, so it reads as cloud and not as a hoop), each clump
  // a big soft puff with two smaller ones trailing it.
  for (let i = 0; i < 14; i++) {
    const base = i / 14 * TAU + (rand(i + 900) - .5) * .35 + rot * (.9 + .2 * rand(i + 903)), rr0 = r * (1.3 + .5 * rand(i + 901)) * out;
    for (let j = 0; j < 3; j++) {
      const ang = base - j * .16, rr = rr0 * (1 + (rand(i * 3 + j + 950) - .5) * .12);
      const y = (rand(i + 902) - .5) * .35 * r + (rand(i * 3 + j + 960) - .5) * .2 * r;
      const q = v3(C3.x + Math.cos(ang) * rr, C3.y + y, C3.z + Math.sin(ang) * rr);
      if (isFront(q) !== front) continue;
      const size = (1.15 - .3 * j) * (.8 + .5 * rand(i + 904)) * (1 + blow);
      sprite(scr(q), size * 1.5, size, CloudC.withAlpha((.34 - .08 * j) * a), puff, layer + (i * 3 + j) * .00005);
    }
  }
  // A few short, faint, wide streaks of wind through the clumps.
  for (let i = 0; i < 6; i++) {
    const a0 = i / 6 * TAU + rot * 1.15 + rand(i + 930) * .8, span = .35 + .2 * rand(i + 931), rr = r * (1.35 + .35 * rand(i + 932)) * out, yy = (rand(i + 933) - .5) * .3 * r;
    const on = ang => v3(C3.x + Math.cos(ang) * rr, C3.y + yy, C3.z + Math.sin(ang) * rr);
    if (isFront(on(a0 + span / 2)) !== front) continue;
    const pts = [];
    for (let k = 0; k <= 8; k++) pts.push(scr(on(a0 + span * k / 8)));
    line(`${key} swirl ${i}`, pts, .3, CloudC.withAlpha(.13 * a), undefined, layer + .003 + i * .00005, 'both');
  }
}
// Rocks falling off the ball's underside during the hold, one about every DribbleEvery seconds; each
// lands under where it fell and stays on the crater floor.
function dribble(key, C3, r, s, t, sun, strength) {
  for (let k = 0; ; k++) {
    const te = t.formed + .3 + k * DribbleEvery + rand(k + 1300) * .2;
    if (te >= t.burst || s < te) break;
    const a = rand(k + 1301) * TAU, out = .6 * rand(k + 1302), n = unit(v3(Math.cos(a) * out, -1, Math.sin(a) * out));
    const p0 = add(C3, mul(n, r * .95)), tf = Math.sqrt(2 * p0.y / G), age = s - te, size = .09 + .09 * rand(k + 1303);
    if (age < tf) {
      const q = v3(p0.x, p0.y - .5 * G * age * age, p0.z);
      sprite({ x: q.x + sun.x * q.y, z: q.z + sun.z * q.y }, size * 1.6, size, Ink.withAlpha(strength * .6), soft, shadowLayer);
      rock(scr(q), size, age * 220, 1, k, Y + .044);
    } else {
      const la = age - tf;
      rock({ x: p0.x, z: p0.z }, size, tf * 220, 1, k, Floor + .031 + (k % 9) * .0003);
      if (la < .35) sprite({ x: p0.x, z: p0.z + la * .3 }, .25 + la, .2 + la * .7, DustC.withAlpha(.4 * (1 - la / .35)), puff, Y + .006);
    }
  }
}
// ---- the ball breaking ------------------------------------------------------------------------------
// The ball's cracks, in its own frame as it faces the camera when the crack phase starts (they turn
// with it). A crack in rock, not lightning: straight pieces with kinks of up to 32 degrees, side
// branches that split off at the kinks and stop, thick where it started and a hairline at its tip.
// They run out from 3 break points on the camera side, 4 or 5 from each, spread round it like a star
// fracture; the break points start one after another. t0..t1: the share of the growth over which each
// runs out; a branch starts when its crack reaches the kink.
const turnAbout = (d, n, a) => unit(add(mul(d, Math.cos(a)), mul(cross(n, d), Math.sin(a))));
function crackWalk(p, d, segs, seed) {
  const pts = [p], dirs = [d];
  for (let k = 0; k < segs; k++) {
    d = turnAbout(d, p, (rand(seed + k * 7) - .5) * 1.1);
    const step = .15 + .1 * rand(seed + k * 7 + 3), q = unit(add(mul(p, Math.cos(step)), mul(d, Math.sin(step))));
    d = unit(add(mul(d, Math.cos(step)), mul(p, -Math.sin(step))));     // the heading carried along the sphere
    p = q; pts.push(p); dirs.push(d);
  }
  return { pts, dirs };
}
const Fractures = (() => {
  const out = [], centres = [];
  // Three break points on the camera side, not too close together.
  for (let j = 0; centres.length < 3 && j < 400; j++) {
    const p = unit(v3(rand(j * 5 + 1650) - .5, rand(j * 5 + 1651) - .5, rand(j * 5 + 1652) - .5));
    if (dot(p, View) > .55 && centres.every(c => dot(c, p) < .8)) centres.push(p);
  }
  centres.forEach((p, c) => {
    const ref = unit(cross(p, Math.abs(p.y) > .9 ? v3(1, 0, 0) : Up)), count = 4 + (c % 2), start = c * .18;
    for (let e = 0; e < count; e++) {
      const i = c * 10 + e, d0 = turnAbout(ref, p, (e + (rand(i + 1663) - .5) * .5) / count * TAU);
      const segs = 4 + Math.floor(rand(i + 1660) * 3), t0 = start + .08 * rand(i + 1661), t1 = Math.min(1, t0 + .45 + .15 * rand(i + 1662));
      const main = crackWalk(p, d0, segs, 1700 + i * 50);
      out.push({ pts: main.pts, t0, t1, w: 1, main: true });
      for (const k of [2, 4]) {
        if (k >= segs || rand(i * 9 + k + 1800) < .35) continue;
        const side = rand(i * 9 + k + 1801) < .5 ? -1 : 1, bd = turnAbout(main.dirs[k], main.pts[k], side * (.7 + .5 * rand(i * 9 + k + 1802)));
        const b = crackWalk(main.pts[k], bd, 1 + Math.floor(rand(i * 9 + k + 1803) * 2), 2000 + i * 40 + k * 5), bt0 = t0 + (t1 - t0) * k / segs;
        out.push({ pts: b.pts, t0: bt0, t1: Math.min(1, bt0 + .2), w: .55, main: false });
      }
    }
  });
  return out;
})();
// A strip along screen points whose half-width at each point is given (so a crack can taper).
function taperStrip(key, pts, halfW, colour, material, layer) {
  const a = [], b = [], last = pts.length - 1;
  pts.forEach((q, i) => {
    const prev = pts[Math.max(0, i - 1)], next = pts[Math.min(last, i + 1)], dx = next.x - prev.x, dz = next.z - prev.z, len = Math.hypot(dx, dz) || 1, w = halfW[i];
    a.push({ x: q.x - dz / len * w, z: q.z + dx / len * w }); b.push({ x: q.x + dz / len * w, z: q.z - dx / len * w });
  });
  strip(key, a, b, colour, material, layer);
}
// One crack at `grow` (0..1, how far the cracks have run) and `open` (0..1, how wide they have opened):
// a dark lip, and once it opens the core's warm light inside it, narrower than the lip, with a faint glow.
function fracture(key, C3, r, rot, cr, grow, open) {
  const f = clamp((grow - cr.t0) / (cr.t1 - cr.t0));
  if (f <= 0) return;
  const n = cr.pts.length - 1, reach = Math.max(.05, f * n), pts = [];
  for (let k = 0; k <= Math.ceil(reach); k++) {
    let d = cr.pts[Math.min(k, n)];
    if (k > reach) { const u = reach - (k - 1); d = unit(add(mul(cr.pts[k - 1], 1 - u), mul(cr.pts[k], u))); }
    pts.push({ d: rotY(d, rot), share: Math.min(k, reach) / reach });
  }
  let run = [], piece = 0;
  const flush = () => {
    if (run.length > 1) {
      const q = run.map(x => x.q), prof = run.map(x => Math.pow(1 - x.share, .6) * cr.w);
      // The dark lip stays wider than the light at every stage, and the light stays warm (not white), so
      // it reads as an opening in the rock with light behind it.
      taperStrip(`${key} ${piece} lip`, q, prof.map(w => .01 + (.022 + .06 * open) * w), CrackC.withAlpha(.92), undefined, Y + .083);
      if (open > 0) {
        taperStrip(`${key} ${piece} glow`, q, prof.map(w => (.03 + .05 * open) * w), Seam.withAlpha(.14 * open), whiteGlow, Y + .0831);
        taperStrip(`${key} ${piece} light`, q, prof.map(w => (.008 + .03 * open) * w), Color.Lerp(Seam, SunPale, .55 * open).withAlpha(Math.min(1, open * 1.6)), whiteGlow, Y + .0832);
      }
      piece++;
    }
    run = [];
  };
  pts.forEach(x => { if (dot(x.d, View) > .04) run.push({ q: scr(add(C3, mul(x.d, r * 1.02))), share: x.share }); else flush(); });
  flush();
}
// Chips of rock spat out of the main cracks as they open: thrown out, they fall and stay on the crater
// floor. launch 0..1 is when in the crack phase each goes.
function chips(key, C3, r, s, t, sun, strength) {
  const mains = Fractures.filter(cr => cr.main);
  for (let i = 0; i < 18; i++) {
    const cr = mains[i % mains.length], k = 1 + (i * 3) % (cr.pts.length - 1), te = t.crack + CrackTime * (.25 + .7 * rand(i + 1900));
    if (s < te) continue;
    const n = rotY(cr.pts[k], Spin * D2R * (te - t.crack)), p0 = add(C3, mul(n, r * 1.02)), sp = 1.4 + 1.6 * rand(i + 1901);
    const vx = n.x * sp, vz = n.z * sp, vy = n.y * sp + 1.6, tl = (vy + Math.sqrt(vy * vy + 2 * G * p0.y)) / G, age = s - te, size = .07 + .07 * rand(i + 1902);
    if (age < tl) {
      const q = v3(p0.x + vx * age, p0.y + vy * age - .5 * G * age * age, p0.z + vz * age);
      rock(scr(q), size, age * 400, 1, i, dot(n, View) >= 0 ? Y + .0835 : Y + .044);
    } else rock({ x: p0.x + vx * tl, z: p0.z + vz * tl }, size, tl * 400, 1, i, Floor + .0305 + (i % 7) * .0003);
  }
}
// The burst: every other surface slot becomes a chunk thrown out and down; it lands and stays.
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
  kit: 'Pain', label: 'Chibaku Tensei v2 (sketch)',
  params: {
    scenario: { label: 'Showcase', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    aim: P('Direction to the cell (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Distance to the cell (cells)', 9, 7, 20, .5, 'Showcase'),
    warm: P('Warm-up (hands cupped, then the throw)', .8, .4, 1.5, .05, 'Timing (s)'),
    speed: P('Core flight speed (cells/s)', 14, 6, 30, 1, 'Timing (s)'),
    pull: P('Pull', 3, 1.5, 5, .1, 'Timing (s)'),
    hold: P(`Hold shown (${HoldSeconds} s in the game)`, 2.5, 1, 6, .25, 'Timing (s)'),
    radius: P('Pull radius (cells)', 6, 4, 8, .5, 'Shape'),
    ballR: P('Ball radius (cells)', 2, 1.2, 3, .1, 'Shape'),
    ballH: P('Ball centre height (cells)', 5, 2, 7, .1, 'Shape'),
    plate: P('Ground plate size (cells)', 1, .7, 1.4, .05, 'Shape'),
    glare: P('Core glare', 1, 0, 1.5, .05, 'Look'),
    cloud: P('Cloud ring', 1, 0, 1.5, .05, 'Look'),
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
    // The core's glare: full until about half the plates have arrived, then the ball covers it.
    const light = p.glare * (s < t.arrive ? 1 : 1 - .75 * smooth(clamp((frac - .45) / .55)));

    // --- the floor: area marker, the core's light, cracks, crater, lip, rim -------------------------------
    if (s >= t.arrive) {
      const spread = easeOut((s - t.arrive) / .3), fade = 1 - clamp((s - t.formed - .6) / .6);
      darkEdge(T, R * spread, .45 * fade, Floor + .009);
      if (s < t.burst + .3) {
        const pool = spread * (s < t.burst ? 1 : 1 - (s - t.burst) / .3);
        sprite(T, R * 2.1, R * 2.1, SunWarm.withAlpha(.1 * light * pool), glow, Floor + .0148);
      }
    }
    const outline = (key, q, a) => line(key, [...q.abs, q.abs[0]], .05, CrackC.withAlpha(.75 * a), undefined, Floor + .02, 'none');
    kept.forEach(q => { if (s >= q.crackAt) outline(`chibaku2 kept crack ${q.pl.i}`, q, clamp((s - q.crackAt) / .1)); });
    caught.forEach(q => {
      if (s < q.crackAt) return;
      if (s < q.heaveAt) { outline(`chibaku2 crack ${q.pl.i}`, q, clamp((s - q.crackAt) / .1)); return; }
      polyC(`chibaku2 rib ${q.pl.i}`, q.w, q.abs, RibSoft, Floor + .013);
      polyC(`chibaku2 hole ${q.pl.i}`, q.w, growC(q.abs, q.w, -.03), Color.Lerp(HoleEdge, HoleDeep, 1 - q.pl.r / R), Floor + .014);
      if (s < q.liftAt) {       // heaving: rises a little and rocks before it tears free
        const u = smooth((s - q.heaveAt) / (q.liftAt - q.heaveAt)), jig = Math.sin(s * 55 + q.pl.i) * .02 * u;
        slab(`chibaku2 slab ${q.pl.i}`, q.pl, v3(q.w.x + jig, HeaveH * u, q.w.z), 0, .45 * u, 1, sun, strength, buildingLayer);
      }
      if (q.pl.i % 2 === 0 && s >= q.liftAt && s < q.liftAt + .8) {
        const u = (s - q.liftAt) / .8;
        sprite({ x: q.w.x, z: q.w.z + u * .45 }, .5 + u * .7, .42 + u * .6, DustC.withAlpha(.42 * (1 - u)), puff, Y + .004);
      }
    });
    // The crater is deepest in the middle: a soft dark spot over the holes, and a lighter lip at the rim.
    const bowl = smooth(clamp((s - t.pull - p.pull * .2) / (p.pull * .6)));
    if (bowl > 0) {
      sprite(T, R * 1.7, R * 1.7, Core.withAlpha(.42 * bowl), soft, Floor + .0145);
      draw(lipBand, T.x, Floor + .0146, T.z, R * 1.01, R * 1.01, 0, Lip.withAlpha(.35 * bowl));
    }
    for (let i = 0; i < 12; i++) {
      const a = (i + rand(i + 300) * .6) / 12 * TAU, at = { x: T.x + Math.cos(a) * (R + .15), z: T.z + Math.sin(a) * (R + .15) };
      crack(`chibaku2 rim ${i}`, at, smooth(clamp((s - t.pull - CrackRun) / .15)), 60 + i * 7);
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
      const key = `chibaku2 pawn ${q.i}`;
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

    // --- Pain's hands, the core forming between them, the throw and the core's flight -------------------
    // Facing north his back is to the camera: the hands in front of his chest are behind his body.
    const hs = hands(s, t, p), handLayer = f.sa > .707 ? pawnLayer - .01 : pawnLayer + .01;
    if (hs) hs.forEach((h, i) => {
      const [a, c, hh] = h.at, shoulder = f.place(P0, .03, (i === 0 ? 1 : -1) * .13, ShoulderH), hand = f.place(P0, a, c, hh);
      arm(`chibaku2 arm ${i}`, shoulder, hand, h.dir === 'up' ? { x: 0, z: 1 } : { x: f.ca, z: f.sa }, h.grip, handLayer);
    });
    if (hs && s < t.launch) {
      const warmU = clamp((s - t.cast) / p.warm), [h0, h1] = hs.map(h => h.at), throwing = s >= t.launch - ThrowTime;
      const at = throwing ? f.place(P0, h0[0] + .02, h0[1], h0[2] + .12) : f.place(P0, (h0[0] + h1[0]) / 2 + .05, (h0[1] + h1[1]) / 2, (h0[2] + h1[2]) / 2 + .06);
      const wg = smooth(clamp(warmU * 1.6));
      sprite(at, .25 + .45 * wg, .25 + .45 * wg, SunWhite.withAlpha(.7 * wg), glow, Y + .099);
      blackSun('chibaku2 palm', at, PalmR * smooth(clamp((warmU - .35) / .5)), s, 1, Y + .1, .45 * p.glare);
    }
    if (s >= t.launch && s < t.arrive) {
      const u = (s - t.launch) / (t.arrive - t.launch), at = k => {
        const [a, h] = bez(t.path.A, t.path.K, t.path.C, k), g = f.ground(P0, a, Raised[1] * (1 - k));
        return { x: g.x, z: g.z + h * Lift };
      };
      const trail = [];
      for (let k = 0; k <= 6; k++) trail.push(at(Math.max(0, u - k * .035)));
      line('chibaku2 trail', trail, .2, Core.withAlpha(.3), undefined, Y + .098, 'end');
      blackSun('chibaku2 flying', at(u), lerp(PalmR, CoreR * .85, smooth(u)), s, 1, Y + .1, lerp(.5, .85, u) * p.glare);
    }

    // --- in the air: flying plates, pawns and the rock stream, split by depth round the ball ----------------
    caught.forEach(q => {
      if (s < q.liftAt || s >= q.arriveAt) return;
      const age = s - q.liftAt, k = age / q.fly, u = Math.pow(k, 1.7), G3 = v3(q.w.x, HeaveH, q.w.z);
      const target = add(C3, mul(rotY(slots.dirs[q.slot], spin), rBall * 1.05));
      const pos = add(mul(G3, 1 - u), mul(target, u));
      pos.y += .2 * smooth(clamp(age / (.25 * q.fly))) * (1 - u);
      const S = scr(pos), dx = Cs.x - S.x, dz = Cs.z - S.z, dl = Math.hypot(dx, dz) || 1;
      // Dark against the core's light once it is up in the air, not while it is still near the ground.
      const look = { dark: .75 * smooth(clamp((u - .12) / .45)), rim: { x: dx / dl, z: dz / dl }, rimA: .6 * smooth(clamp((u - .2) / .3)) * (.35 + .65 * Math.min(1, light)) };
      air.push({ pos, draw: layer => slab(`chibaku2 slab ${q.pl.i}`, q.pl, pos, age * q.pl.spinRate, .3 + age * q.pl.tumbleRate, lerp(1, .6, u), sun, strength, layer, look) });
    });
    air.push(...stream('chibaku2 stream', T, C3, rBall, R, s, t.pull + .3, t.formed, 26, 1500));
    air.push(...stream('chibaku2 trickle', T, C3, rBall, R, s, t.formed, t.crack - 1, 9, 1700));
    air.forEach(a => { a.nd = dot(add(a.pos, mul(C3, -1)), View); });
    air.sort((a, b) => a.nd - b.nd);

    // Tan dust rising from the torn plates and drifting in, and the haze that hangs under the ball.
    caught.forEach(q => {
      if (q.pl.i % 2 || s < q.liftAt - .05) return;
      const u = (s - q.liftAt + .05) / 1.5;
      if (u >= 1) return;
      const e = easeOut(u), g = { x: lerp(q.w.x, T.x, .35 * e), z: lerp(q.w.z, T.z, .35 * e) }, h = 1.4 * e;
      sprite({ x: g.x, z: g.z + h * Lift }, .55 + 1.1 * u, .45 + .9 * u, DustTan.withAlpha(.34 * Math.sin(u * Math.PI)), puff, Y + .012 + (q.pl.i % 40) * .00005);
    });
    const haze = s < t.pull ? 0 : s < t.formed ? smooth(clamp((s - t.pull) / .8)) : s < t.burst ? lerp(1, .45, smooth(clamp((s - t.formed) / 1.5))) : .45 * (1 - clamp((s - t.burst) / .5));
    if (haze > 0) for (let i = 0; i < 9; i++) {
      const a = rand(i + 820) * TAU + s * .12 * (i % 2 ? 1 : -1), d = 1 + 2.2 * rand(i + 821), h = .4 + (H - Rb) * .9 * rand(i + 822);
      const size = 2.2 + 1.4 * rand(i + 823);
      sprite({ x: T.x + Math.cos(a) * d, z: T.z + Math.sin(a) * d * .7 + h * Lift }, size, size * .8, DustTan.withAlpha(.15 * haze), puff, Y + .011 + i * .0001);
    }

    air.filter(a => a.nd < 0).forEach(a => a.draw(Y + .02));

    // The core over the cell, the back half of the cloud ring, and the ball that grows round the core.
    if (s >= t.arrive && s < t.formed + .2) {
      const grown = lerp(.65, 1, smooth(clamp((s - t.arrive) / Pulse)));
      blackSun('chibaku2 core', Cs, CoreR * grown, s, 1, Y + .045, light);
      if (s < t.arrive + .15) {
        const u = (s - t.arrive) / .15, size = lerp(1, 4, easeOut(u));
        sprite(Cs, size, size, SunPale.withAlpha(.7 * (1 - u)), glow, Y + .046);
        sprite(Cs, size * .45, size * .45, SunWhite.withAlpha(.8 * (1 - u)), glow, Y + .0462);
      }
    }
    const cloudA = p.cloud * smooth(clamp((s - t.formed + .8) / 1.2)), blow = s >= t.burst ? clamp((s - t.burst) / .8) : 0;
    cloudRing('chibaku2 cloud back', C3, Rb, s, cloudA, blow, false);
    if (s >= t.formed && s < t.burst + 1.2) dribble('chibaku2 dribble', C3, Rb, s, t, sun, strength);
    if (s >= t.pull && s < t.burst) {
      const filled = [];
      for (let k = 0; k < Math.min(arrived, M); k++) filled.push(caught[k].slot);
      // Cracking: dark hairlines run out over the last CrackCreep seconds of the hold (and on 0.15 s into
      // the crack phase); in the crack phase they open and fill with light, the plates pull apart along
      // every seam and the ball shakes.
      const creepFrom = Math.max(t.formed + .2, t.crack - CrackCreep), grow = clamp((s - creepFrom) / (t.crack + .15 - creepFrom));
      const crackU = clamp((s - t.crack) / CrackTime), open = Math.pow(crackU, 1.4);
      const Cb = add(C3, v3(Math.sin(s * 71) * .035 * open, 0, Math.cos(s * 89) * .03 * open)), rot = Spin * D2R * (s - t.crack);
      const glowK = s < t.formed ? .35 + .3 * frac : .45 + .5 * squeeze + 1.4 * crackU;
      const rimA = s < t.formed ? smooth(clamp((frac - .3) / .7)) : .85 + .6 * crackU;
      ball('chibaku2 ball', Cb, rBall, spin, slots, filled, s < t.formed ? smooth(clamp(frac * 1.6)) : 1, L, sun, strength, glowK, rimA, frac * Boulders, open);
      if (grow > 0) Fractures.forEach((cr, i) => fracture(`chibaku2 crack ${i}`, Cb, rBall, rot, cr, grow, open));
      if (s >= t.crack) Fractures.filter(cr => cr.main).forEach((cr, i) => {      // dust spurting out of the main cracks
        const d = rotY(cr.pts[1 + i % 3], rot);
        if (dot(d, View) <= .1) return;
        const u = ((s - t.crack) * 2.5 + rand(i + 600)) % 1, at = scr(add(Cb, mul(d, rBall * 1.05)));
        sprite({ x: at.x + d.x * u * .3, z: at.z + d.z * u * .3 + u * .25 }, .3 + u * .5, .26 + u * .4, DustC.withAlpha(.45 * Math.sin(u * Math.PI)), puff, Y + .0836 + i * .00005);
      });
    }
    if (s >= t.crack) chips('chibaku2 chip', C3, Rb, s, t, sun, strength);
    if (s >= t.formed - .3 && s < t.burst) for (let i = 0; i < 7; i++) {    // a few rocks circling the ball
      const a = s * (.6 + .15 * rand(i + 500)) + i * .9, rr = Rb * (1.12 + .1 * rand(i + 501)), y = Math.sin(i * 2.3) * Rb * .5;
      const q = v3(C3.x + Math.cos(a) * rr, C3.y + y, C3.z + Math.sin(a) * rr), front = dot(add(q, mul(C3, -1)), View) >= 0;
      rock(scr(q), .12 + .08 * rand(i + 502), a / D2R * 3, clamp((s - t.formed + .3) / .3), i, front ? Y + .085 : Y + .044);
    }
    air.filter(a => a.nd >= 0).forEach(a => a.draw(Y + .09));
    cloudRing('chibaku2 cloud front', C3, Rb, s, cloudA, blow, true);
    if (s >= t.pull && s < t.formed) inflow('chibaku2 inflow', T, R, Cs, s, clamp((s - t.pull) / .2) * (1 - smooth(clamp((frac - .7) / .3))));

    // --- the burst ------------------------------------------------------------------------------------------
    if (s >= t.burst) {
      const age = s - t.burst;
      chunks('chibaku2 chunk', C3, Rb, spin, slots, M, age, sun, strength);
      falling.forEach(d => d());
      if (age < .12) {
        const k = 1 - age / .12;
        sprite(Cs, Rb * 4.5, Rb * 4.5 * Squash, SunPale.withAlpha(.85 * k), glow, Y + .15);
        sprite(Cs, Rb * 2, Rb * 2 * Squash, SunWhite.withAlpha(.9 * k), glow, Y + .1502);
      }
      if (age < .45) sprite(Cs, Rb * 3, Rb * 3 * Squash, SunWarm.withAlpha(.3 * (1 - age / .45)), glow, Y + .1504);
      for (let i = 0; i < 18; i++) {
        const life = 1 + rand(i + 700) * .4, u = age / life;
        if (u >= 1) continue;
        const a = rand(i + 701) * TAU, d = Rb * (.15 + .75 * rand(i + 702)) * (1 + .7 * easeOut(u));
        sprite({ x: Cs.x + Math.cos(a) * d, z: Cs.z + Math.sin(a) * d * Squash - u * .6 }, 1.1 + u * 1.5, .95 + u * 1.3, DustC.withAlpha(.5 * (1 - u) * clamp(u * 12)), puff, Y + .14);
      }
      // A ring of dust rolling out along the ground from under the ball.
      for (let i = 0; i < 22; i++) {
        const u = clamp((age - .05) / 1.3);
        if (u <= 0 || u >= 1) continue;
        const a = i / 22 * TAU + rand(i + 1400) * .25, d = lerp(.8, R + .6, easeOut(u)), g = { x: T.x + Math.cos(a) * d, z: T.z + Math.sin(a) * d };
        sprite({ x: g.x, z: g.z + u * .25 }, .9 + 1.2 * u, .7 + .9 * u, DustC.withAlpha(.42 * (1 - u) * clamp(u * 10)), puff, Y + .0055 + i * .00005);
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
