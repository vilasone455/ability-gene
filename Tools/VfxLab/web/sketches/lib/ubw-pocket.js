// Trace kit: Unlimited Blade Works as a pocket map (2026-09-24), shared by "Unlimited Blade Works:
// cast (pocket)" and "Unlimited Blade Works: world (pocket)". Not a sketch itself, so it is not
// listed in sketches/index.js. The in-place world of the first two UBW sketches stays in lib/ubw.js.
//
// What is here:
//   circuitPath, chant, squareTraces   the chant on the home map: cyan lines run out along the floor,
//                                      square traces light round the caster (anime 2015, Shirou's chant)
//   upTo, between, pointAt             walking those lines, for the fire that runs along them
//   quads, flames                      a batch of sprites, and of flames: two draws for any number
//   whiteDisc, cover, fireWall         the white that takes everyone; the white round the world and the
//                                      wall of fire at its edge
//   floor, patches, backstop, haze,    the world's ground and air: red-brown cracked earth and dust
//   sunGlow, hill, skyGears, embers    patches, the haze past the map edge, the low sun, the hill of
//                                      swords, the shadows of the gears overhead, drifting embers
//   field, drawField                   the standing swords, laid out once and baked into three meshes
//                                      (shadows, ground marks, blades) from one atlas texture; a
//                                      command's swords can be left out and drawn one by one between
//                                      parts of the blades mesh (the commands sketch)
//   Look, fieldList, standingPose,     the world's decided look, its swords without the bake, one
//   mapEdge                            sword's standing pose, the lab's dashed map edge
//
// Atlas: Textures/RimArt/TraceTrial/Atlas.png, made by make_trace_trial_textures.py from the six
// local reference weapons (git-excluded; it never ships). One texture means every part of every
// sword goes into one mesh in draw order, north first, so the overlaps come out right with three
// draws for a thousand swords. Row r is AtlasNames[r], its colour times the grey of column c: the two
// edge greys, the face lit three ways, the two dark bands low on the blade. Row 6 holds flat swatches
// for the ground marks. The numbers below match the script's. In game the kit builds the same atlas
// at load from the studied weapons' own textures.
import { AltitudeLayer, Color, MaterialPool, Mesh, Meshes, MeshPool, ShaderDatabase } from '../../js/engine.js';
import { registerLabTexture, pixels, fbm, hash } from '../../js/standins.js';
import { draw, mesh } from './six-paths-solid.js';
import { Y, Floor, Lift, sprite, soft, glow, rand } from './six-paths-impact.js';
import { ringAt, line, shadowLayer, buildingLayer } from './goku.js';
import {
  smooth, clamp, D2R, Black, Trace, TraceHot, solid, Weapons, v3, plus, dot3, unit, onScreen, alongSun, clip,
  higherThan, lowerThan, at3, Square, upright, cutOf, partial, blade, pommelOf,
} from './trace.js';
import { light, FireOuter, FireCore, Ember, DuskDark, Sunset, gearMesh } from './ubw.js';

export const Radius = [6, 9, 12], VerseTime = 2, LinesOut = 1.8, Lines = 10;
// The pocket map is 40 x 40 with the caster in the middle. Inside the world the sun is low: shadows
// run twice as long as the scene's.
export const MapHalf = 20, DuskShadow = 2;
// The world as the user passed it (2026-09-24, "otherwise it's perfect"): the world sketch's defaults, and
// the commands sketch's constants.
export const Look = { density: .24, hill: 5.5, beyond: 16, size: 1.3, lean: 22, twilight: .8, gears: .28 };
export const WhiteHot = new Color(1, .97, .9), White = new Color(1, 1, 1);
export const Tint = new Color(1, .86, .76), Twilight = new Color(1, .7, .55), Haze = new Color(.7, .45, .4);
const EarthDark = new Color(.22, .12, .08), EarthLit = new Color(.58, .38, .25), FarEarth = new Color(.46, .3, .25);
const DeepFire = new Color(.8, .22, .07), Soot = new Color(.3, .12, .07);
const terrain = AltitudeLayer.Terrain.AltitudeFor();

// ---- the chant ------------------------------------------------------------------------------------------

// Line i of the chant from the caster's feet out to reach: the in-place sketch's lines exactly, so the
// fire can run along the very lines the chant drew.
export function circuitPath(c, i, reach) {
  const a0 = i / Lines * Math.PI * 2 + rand(i * 3 + 7) * .3, pts = [];
  for (let r = .35; ; r += .5) {
    const rr = Math.min(r, reach), wob = Math.sin(rr * 1.3 + i) * .08;
    pts.push({ x: c.x + Math.cos(a0 + wob) * rr, z: c.z + Math.sin(a0 + wob) * rr });
    if (r >= reach) break;
  }
  return pts;
}
// How far the chant's lines have run at s: verse k carries them from the last verse's radius to its own.
export function chantRadius(s, V) {
  let r = 0;
  for (let k = 1; k <= V; k++) {
    const from = k > 1 ? Radius[k - 2] : 0, start = (k - 1) * VerseTime;
    if (s >= start) r = from + (Radius[k - 1] - from) * Math.min(1, (s - start) / LinesOut);
  }
  return r;
}
// The lines, their halo, the ring at their front, the rings of verses already said, a ring off the pawn
// as each verse starts. alpha fades them all.
export function chant(c, s, V, alpha) {
  if (alpha <= 0) return;
  const reach = chantRadius(s, V);
  for (let i = 0; i < Lines; i++) {
    const pts = circuitPath(c, i, reach);
    if (pts.length < 2) continue;
    line(`ubwp circuit ${i}`, pts, .045, Trace.withAlpha(.75 * alpha), light, Floor + .007, 'end');
    line(`ubwp circuit halo ${i}`, pts, .16, Trace.withAlpha(.16 * alpha), light, Floor + .0065, 'end');
  }
  if (reach > .4) ringAt(c, reach, Trace.withAlpha(.45 * alpha), Floor + .0068);
  for (let k = 1; k < V; k++) if (s >= k * VerseTime - .2) ringAt(c, Radius[k - 1], Trace.withAlpha(.22 * alpha), Floor + .0067);
  for (let k = 1; k <= V; k++) {
    const u = (s - (k - 1) * VerseTime) / .45;
    if (u >= 0 && u < 1) ringAt(c, .3 + 1.2 * u, Trace.withAlpha(.7 * (1 - u) * alpha), Y + .01, false, light);
  }
}

// Square traces round the caster's feet, as the circuits in Shirou's chant (anime 2015, 21-24 s): each
// runs out from a small ring in straight steps that turn at right angles, and ends in a node.
const Squares = Array.from({ length: 8 }, (_, i) => {
  const a = (i + .5) / 8 * Math.PI * 2 + (rand(i * 5 + 1) - .5) * .3, sx = Math.sign(Math.cos(a)) || 1, sz = Math.sign(Math.sin(a)) || 1;
  let x = Math.cos(a) * .42, z = Math.sin(a) * .42, across = Math.abs(Math.cos(a)) > Math.abs(Math.sin(a));
  const pts = [[x, z]];
  for (let k = 0; k < 4; k++) {
    const len = .16 + rand(i * 17 + k) * .28;
    if (across) x += sx * len; else z += sz * len;
    pts.push([x, z]);
    across = !across;
  }
  const length = pts.reduce((sum, q, k) => k ? sum + Math.hypot(q[0] - pts[k - 1][0], q[1] - pts[k - 1][1]) : 0, 0);
  return { pts, length, delay: rand(i * 23 + 2) * .3 };
});
// s: seconds since the chant began. They light at 2.4 cells a second and pulse while they hold.
export function squareTraces(c, s, alpha) {
  if (alpha <= 0 || s < 0) return;
  const pulse = .8 + .2 * Math.sin(s * 6);
  Squares.forEach((sq, i) => {
    const lit = (s - sq.delay) * 2.4;
    if (lit <= 0) return;
    const pts = partial(sq.pts, Math.min(lit, sq.length)).map(([x, z]) => ({ x: c.x + x, z: c.z + z }));
    if (pts.length < 2) return;
    line(`ubwp square ${i}`, pts, .035, Trace.withAlpha(.85 * alpha * pulse), light, Floor + .0071, 'none');
    line(`ubwp square halo ${i}`, pts, .12, Trace.withAlpha(.14 * alpha), light, Floor + .007, 'none');
    if (lit >= sq.length) sprite(pts[pts.length - 1], .12, .12, TraceHot.withAlpha(.9 * alpha * pulse), glow, Floor + .0073);
  });
}

// ---- walking a line ------------------------------------------------------------------------------------

function lengths(pts) {
  const L = [0];
  for (let i = 1; i < pts.length; i++) L.push(L[i - 1] + Math.hypot(pts[i].x - pts[i - 1].x, pts[i].z - pts[i - 1].z));
  return L;
}
export const lengthOf = pts => lengths(pts)[pts.length - 1];
// The point d cells along the line, and the line's direction there.
export function pointAt(pts, d) {
  const L = lengths(pts);
  for (let i = 1; i < pts.length; i++) {
    if (d > L[i] && i < pts.length - 1) continue;
    const f = clamp((d - L[i - 1]) / ((L[i] - L[i - 1]) || 1)), a = pts[i - 1], b = pts[i], len = (L[i] - L[i - 1]) || 1;
    return { x: a.x + (b.x - a.x) * f, z: a.z + (b.z - a.z) * f, dx: (b.x - a.x) / len, dz: (b.z - a.z) / len };
  }
  return { x: pts[0].x, z: pts[0].z, dx: 1, dz: 0 };
}
// The part of the line from d0 to d1 cells along it.
export function between(pts, d0, d1) {
  const L = lengths(pts), out = [];
  if (d1 <= d0) return out;
  out.push(pointAt(pts, d0));
  for (let i = 1; i < pts.length - 1; i++) if (L[i] > d0 && L[i] < d1) out.push(pts[i]);
  out.push(pointAt(pts, d1));
  return out;
}
export const upTo = (pts, d) => between(pts, 0, d);

// ---- batches ---------------------------------------------------------------------------------------------

// Any number of sprites in one draw: each { x, z, w, h, angle? } is MeshPool.plane10 at x, z scaled w by
// h and turned angle degrees clockwise, as sprite() draws it.
export function quads(key, items, colour, material, layer) {
  if (!items.length || colour.a <= .002) return;
  const xz = [], uv = [], tri = [];
  items.forEach((q, n) => {
    const t = (q.angle || 0) * D2R, ct = Math.cos(t), st = Math.sin(t), b = n * 4;
    [[-.5, -.5, 0, 0], [-.5, .5, 0, 1], [.5, .5, 1, 1], [.5, -.5, 1, 0]].forEach(([x, z, u, v]) => {
      const px = x * q.w, pz = z * q.h;
      xz.push(q.x + px * ct + pz * st, q.z - px * st + pz * ct);
      uv.push(u, v);
    });
    tri.push(b, b + 1, b + 2, b, b + 2, b + 3);
  });
  const m = mesh(key);
  m.setFlat(xz, tri);
  m.uv = new Float32Array(uv);
  draw(m, 0, layer, 0, 1, 1, 0, colour, material);
}
const flameSolid = MaterialPool.MatFrom('lab/ubw-flame', ShaderDatabase.Transparent);
const flameGlow = MaterialPool.MatFrom('lab/ubw-flame', ShaderDatabase.MoteGlow);
// Flames standing on the floor, each { x, z, w, h } with its foot at x, z: the outer flame and a core
// half as wide and 0.6 as tall, as lib/ubw.js fireRing draws one, in two draws. South ones draw over
// north ones. solid draws the outer flame see-through instead of added light, so it shows in front of white.
export function flames(key, list, alpha, layer, { solid: see = false } = {}) {
  if (!list.length || alpha <= 0) return;
  list.sort((a, b) => b.z - a.z);
  quads(`${key} outer`, list.map(f => ({ x: f.x, z: f.z + f.h * .5, w: f.w, h: f.h })), FireOuter.withAlpha((see ? .85 : .5) * alpha), see ? flameSolid : flameGlow, layer);
  quads(`${key} core`, list.map(f => ({ x: f.x, z: f.z + f.h * .3, w: f.w * .5, h: f.h * .6 })), FireCore.withAlpha(.5 * alpha), flameGlow, layer + .0005);
}
// Flame k of n standing along something: its height at s, flickering, 0.75 to 1.25 of h.
export const flick = (k, s, h) => h * (.55 + .25 * Math.sin(s * 11 + k * 1.7) + .2 * Math.sin(s * 17.3 + k * 2.9)) * (.75 + .5 * rand(k * 13 + 1));
// A ring of flames of radius r round c, only those whose angle lit(angle) allows, into list.
export function ringFlames(list, c, r, s, h, lit = () => true) {
  const n = Math.max(10, Math.ceil(Math.PI * 2 * r / .26));
  for (let i = 0; i < n; i++) {
    const a = (i + rand(i * 7 + 3) * .6) / n * Math.PI * 2;
    if (!lit(a)) continue;
    list.push({ x: c.x + Math.cos(a) * r, z: c.z + Math.sin(a) * r, w: .3 * (.8 + .4 * rand(i * 5 + 2)), h: flick(i, s, h) });
  }
}

// ---- white -----------------------------------------------------------------------------------------------

const disc = Meshes.disc(72, 'ubwp disc');
// The flash that takes everyone inside radius r (or brings them back): a white disc and a glow past it.
export function whiteDisc(c, r, alpha, layer) {
  if (alpha <= 0) return;
  draw(disc, c.x, layer, c.z, r, r, 0, WhiteHot.withAlpha(alpha), solid);
  sprite(c, r * 2.9, r * 2.9, WhiteHot.withAlpha(.4 * alpha), glow, layer + .001);
}
function annulus(key, c, r0, r1, colour, layer, n = 96) {
  const xz = [], tri = [];
  for (let i = 0; i <= n; i++) {
    const a = i / n * Math.PI * 2, ca = Math.cos(a), sa = Math.sin(a);
    xz.push(c.x + ca * r0, c.z + sa * r0, c.x + ca * r1, c.z + sa * r1);
    if (i) { const b = i * 2; tri.push(b - 2, b, b - 1, b - 1, b, b + 1); }
  }
  const m = mesh(key);
  m.setFlat(xz, tri);
  draw(m, 0, layer, 0, 1, 1, 0, colour, solid);
}
// A ring from r0 to r1 whose opacity runs from 0 at r0 to 1 at r1 (out false: from 1 at r0 to 0 at r1),
// through a gradient texture across it: one draw, no steps.
registerLabTexture('lab/ubw-fade', () => pixels(64, u => [1, 1, 1, u * u * (3 - 2 * u)]));
const fade = MaterialPool.MatFrom('lab/ubw-fade', ShaderDatabase.Transparent);
export function fadeRing(key, c, r0, r1, colour, layer, rising = true, n = 96) {
  if (r1 <= r0 || colour.a <= .002) return;
  const xz = [], uv = [], tri = [];
  for (let i = 0; i <= n; i++) {
    const a = i / n * Math.PI * 2, ca = Math.cos(a), sa = Math.sin(a);
    xz.push(c.x + ca * r0, c.z + sa * r0, c.x + ca * r1, c.z + sa * r1);
    uv.push(rising ? .02 : .98, .5, rising ? .98 : .02, .5);
    if (i) { const b = i * 2; tri.push(b - 2, b, b - 1, b - 1, b, b + 1); }
  }
  const m = mesh(key);
  m.setFlat(xz, tri);
  m.uv = new Float32Array(uv);
  draw(m, 0, layer, 0, 1, 1, 0, colour, fade);
}
// Everything further than r from c is white: the world not made yet, or already gone. The inner edge
// fades over 0.7 cells, under the wall of fire.
export function cover(key, c, r, alpha, layer, far = 160) {
  if (alpha <= 0) return;
  annulus(`${key} solid`, c, r, far, WhiteHot.withAlpha(alpha), layer);
  fadeRing(`${key} edge`, c, Math.max(0, r - .7), r, WhiteHot.withAlpha(alpha), layer);
}
// The wall of fire standing at radius r in front of the white: a darker back row, the flames, their
// cores, soot on the white just outside it, a glow on the ground just inside it.
export function fireWall(key, c, r, s, h, layer) {
  if (r <= .3) return;
  const back = [], front = [];
  ringFlames(back, c, r + .14, s * .8 + 3, h * 1.3);
  ringFlames(front, c, r, s, h);
  fadeRing(`${key} soot`, c, r, r + 1, Soot.withAlpha(.55), layer, false);
  back.sort((a, b) => b.z - a.z);
  front.sort((a, b) => b.z - a.z);
  quads(`${key} back`, back.map(f => ({ x: f.x, z: f.z + f.h * .5, w: f.w * 1.3, h: f.h })), DeepFire.withAlpha(.85), flameSolid, layer + .001);
  quads(`${key} front`, front.map(f => ({ x: f.x, z: f.z + f.h * .5, w: f.w, h: f.h })), FireOuter.withAlpha(.9), flameSolid, layer + .002);
  quads(`${key} core`, front.map(f => ({ x: f.x, z: f.z + f.h * .3, w: f.w * .5, h: f.h * .6 })), FireCore.withAlpha(.6), flameGlow, layer + .003);
  ringAt(c, r, FireOuter.withAlpha(.45), Floor + .03, true, light);
}

// ---- the world's ground and air ---------------------------------------------------------------------------

// The earth: a stand-in for the real terrain texture (a PNG from make_trace_textures.py once ported).
// Red-brown dust over two sizes of dry cracks (tileable Voronoi borders), with a few pale pebbles.
function border(u, v, period, seed) {
  const x = u * period, y = v * period, xi = Math.floor(x), yi = Math.floor(y);
  let d1 = 9, d2 = 9;
  for (let j = -1; j <= 1; j++) for (let i = -1; i <= 1; i++) {
    const cx = xi + i, cy = yi + j, wx = ((cx % period) + period) % period, wy = ((cy % period) + period) % period;
    const d = Math.hypot(cx + .15 + .7 * hash(wx, wy, seed) - x, cy + .15 + .7 * hash(wx, wy, seed + 1) - y);
    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
  }
  return d2 - d1;
}
const edge = (lo, hi, x) => { const t = clamp((x - lo) / (hi - lo)); return t * t * (3 - 2 * t); };
registerLabTexture('lab/ubw-earth', () => pixels(256, (u, v) => {
  const n = fbm(u * 4, v * 4, 301, 4, 4), fine = fbm(u * 16, v * 16, 305, 2, 16);
  const big = border(u, v, 3, 311), small = border(u, v, 7, 331);
  const k = (1 - .36 * (1 - edge(0, .045, big))) * (1 - .15 * (1 - edge(0, .03, small))) * (.92 + .16 * fine);
  const pebble = hash(Math.floor(u * 128), Math.floor(v * 128), 337) > .995 ? .18 : 0;
  const t = .2 + .6 * n;
  return [(EarthDark.r + (EarthLit.r - EarthDark.r) * t) * k + pebble, (EarthDark.g + (EarthLit.g - EarthDark.g) * t) * k + pebble * .8,
    (EarthDark.b + (EarthLit.b - EarthDark.b) * t) * k + pebble * .6, 1];
}));
const earth = MaterialPool.MatFrom('lab/ubw-earth', ShaderDatabase.Transparent);
const floors = new Map();
// The ground from half cells west to half east (and south to north) of c, in tiles of `tile` cells
// with the whole texture on each (the lab cannot repeat a texture), one draw.
export function floor(c, half, tile, tint) {
  const id = `${c.x},${c.z},${half},${tile}`;
  let m = floors.get(id);
  if (!m) {
    const xz = [], uv = [], tri = [];
    let n = 0;
    for (let z = -half; z < half - 1e-6; z += tile) for (let x = -half; x < half - 1e-6; x += tile) {
      xz.push(c.x + x, c.z + z, c.x + x, c.z + z + tile, c.x + x + tile, c.z + z + tile, c.x + x + tile, c.z + z);
      uv.push(0, 0, 0, 1, 1, 1, 1, 0);
      tri.push(n, n + 1, n + 2, n, n + 2, n + 3);
      n += 4;
    }
    m = mesh(`ubwp floor ${id}`);
    m.setFlat(xz, tri);
    m.uv = new Float32Array(uv);
    floors.set(id, m);
  }
  draw(m, 0, terrain, 0, 1, 1, 0, tint, earth);
}
// Broad dust patches, darker and warmer, over the tiles so the repeat does not show: count of them in
// a square half cells across.
export function patches(c, half, count) {
  const dark = [], warm = [];
  for (let i = 0; i < count; i++) {
    const x = (rand(i * 17 + 2) - .5) * 2 * half, z = (rand(i * 19 + 4) - .5) * 2 * half, size = 5 + rand(i * 23) * 9;
    (i % 3 ? dark : warm).push({ x: c.x + x, z: c.z + z, w: size, h: size * (.6 + .3 * rand(i * 29)), angle: rand(i * 31) * 180 });
  }
  quads('ubwp patches dark', dark, DuskDark.withAlpha(.18), soft, terrain + .01);
  quads('ubwp patches warm', warm, Sunset.withAlpha(.08), soft, terrain + .011);
}
// What the far zoom-out sees past the drawn ground.
const Far = Color.Lerp(FarEarth, Haze, .45);
export function backstop(c) {
  draw(MeshPool.plane10, c.x, terrain - .01, c.z, 500, 500, 0, Far, solid);
}
const hazeBands = Array.from({ length: 8 }, (_, k) => Meshes.band((MapHalf + 2 + k * 2.4) / 240, 1, 96, `ubwp haze ${k}`));
// Haze past the map edge, thicker the further out, over the swords and under the pawns: the field goes on
// but fades, as the twilight does to the horizon. Above every part of a split field (see drawField).
const HazeLayer = buildingLayer + .6;
export function haze(c, alpha) {
  if (alpha <= 0) return;
  hazeBands.forEach((m, k) => draw(m, c.x, HazeLayer + k * .0001, c.z, 240, 240, 0, Haze.withAlpha(.1 * alpha), solid));
}
// The pocket map's edge, MapHalf cells out each way: a dashed line, lab only.
export function mapEdge(key, c) {
  const dashes = [];
  for (let k = -MapHalf; k < MapHalf; k += 1.6) {
    const m = k + .5;
    dashes.push({ x: c.x + m, z: c.z - MapHalf, w: 1, h: .07 }, { x: c.x + m, z: c.z + MapHalf, w: 1, h: .07 });
    dashes.push({ x: c.x - MapHalf, z: c.z + m, w: .07, h: 1 }, { x: c.x + MapHalf, z: c.z + m, w: .07, h: 1 });
  }
  quads(key, dashes, White.withAlpha(.45), solid, HazeLayer + .01);
}
const toSun = sun => { const d = Math.hypot(sun.x, sun.z) || 1; return { x: -sun.x / d, z: -sun.z / d }; };
// The low sun's side of the sky warms the ground on that side.
export function sunGlow(c, sun, alpha) {
  if (alpha <= 0) return;
  const to = toSun(sun);
  sprite({ x: c.x + to.x * 24, z: c.z + to.z * 24 }, 80, 80, Twilight.withAlpha(.14 * alpha), glow, Floor + .0105);
}
// The hill of swords under the caster, shown by its light: lit toward the sun, shaded away from it (dark
// is the shaded side's opacity; the v2 world asks for more).
export function hill(c, radius, sun, alpha, dark = .36) {
  if (radius <= 0 || alpha <= 0) return;
  const to = toSun(sun);
  sprite({ x: c.x + to.x * radius * .25, z: c.z + to.z * radius * .25 }, radius * 2.3, radius * 2.3, Sunset.withAlpha(.2 * alpha), glow, Floor + .0101);
  sprite({ x: c.x - to.x * radius * .45, z: c.z - to.z * radius * .45 }, radius * 2.1, radius * 1.9, DuskDark.withAlpha(dark * alpha), soft, Floor + .01);
}
// The gears turning in the sky, seen by their shadows: big, slow, stretched along the low sun.
export const SkyGears = [
  { r: 7, x: -12, z: 8, spin: 3.5, teeth: 12 }, { r: 5, x: 10, z: 11, spin: -5, teeth: 10 },
  { r: 8, x: 14, z: -9, spin: 2.5, teeth: 14 }, { r: 4.5, x: -4, z: -12, spin: -6, teeth: 10 },
  { r: 6, x: 2, z: 2, spin: 4, teeth: 12 }, { r: 5.5, x: -18, z: -6, spin: 7, teeth: 10 },
];
function spunGear(key, teeth, turn, along, stretch) {
  const base = gearMesh(teeth), v = base.v, out = new Array(v.length);
  const ct = Math.cos(turn), st = Math.sin(turn), ca = Math.cos(along), sa = Math.sin(along);
  for (let k = 0; k < v.length; k += 2) {
    const x = v[k] * ct - v[k + 1] * st, z = v[k] * st + v[k + 1] * ct;
    const a = (x * ca + z * sa) * stretch, b = -x * sa + z * ca;
    out[k] = a * ca - b * sa;
    out[k + 1] = a * sa + b * ca;
  }
  const m = mesh(key);
  m.setFlat(out, base.tri);
  return m;
}
export function skyGears(key, c, s, sun, opacity) {
  if (opacity <= 0) return;
  const along = Math.atan2(sun.z, sun.x);
  SkyGears.forEach((g, i) => {
    const m = spunGear(`${key} ${i}`, g.teeth, s * g.spin * D2R, along, 1.25);
    const x = c.x + g.x + Math.sin(s * .21 + i * 2) * .8, z = c.z + g.z + Math.cos(s * .17 + i) * .6;
    draw(m, x, shadowLayer + .001 + i * .0002, z, g.r * 1.05, g.r * 1.05, 0, Black.withAlpha(opacity * .35));
    draw(m, x, shadowLayer + .0011 + i * .0002, z, g.r, g.r, 0, Black.withAlpha(opacity));
  });
}
const wrap = (v, span) => ((v + span / 2) % span + span) % span - span / 2;
// Embers drifting up (north) and with the wind (east) over the whole view, looping, three brightnesses.
export function embers(key, c, s, count, alpha) {
  if (alpha <= 0) return;
  const lists = [[], [], []], span = { x: 48, z: 34 };
  for (let i = 0; i < count; i++) {
    const x = wrap((rand(i * 3 + 2) - .5) * span.x + s * (.25 + .3 * rand(i * 11)) + Math.sin(s * 1.3 + i) * .15, span.x);
    const z = wrap((rand(i * 5 + 4) - .5) * span.z + s * (.35 + .5 * rand(i * 7 + 1)), span.z);
    const f = .5 + .5 * Math.sin(s * (3 + rand(i) * 4) + i * 1.7), size = .05 + rand(i * 13) * .06;
    lists[Math.min(2, Math.floor(f * 3))].push({ x: c.x + x, z: c.z + z, w: size, h: size });
  }
  lists.forEach((list, k) => quads(`${key} ${k}`, list, Ember.withAlpha((.3 + .3 * k) * alpha), glow, Y + .03));
}

// ---- the standing field ------------------------------------------------------------------------------------

export const AtlasNames = ['Knife', 'LongSword', 'Spear', 'MonoSword', 'LargeSword', 'Wyrmslayer'];
const AtlasCell = 128, AtlasPad = 8, AtlasSide = 1024;
const Edge0 = 0, Edge1 = 1, Faces = [[.82, 2], [.91, 3], [1, 4]], LowBand = 5, LowerBand = 6;
const SwCrack = 0, SwHole = 1, SwDirtDark = 2, SwDirtMid = 3, SwDirtLit = 4, SwContact = 5;
export const atlas = MaterialPool.MatFrom('RimArt/TraceTrial/Atlas', ShaderDatabase.Transparent);
// Cell (col, row) of the atlas as a uv rectangle, v up: the picture inside its padding.
function cell(col, row) {
  const size = (AtlasCell - 2 * AtlasPad) / AtlasSide;
  return { u0: (col * AtlasCell + AtlasPad) / AtlasSide, v0: 1 - (row * AtlasCell + AtlasCell - AtlasPad) / AtlasSide, du: size, dv: size };
}
const swatch = col => { const r = cell(col, 6); return { u: r.u0 + r.du / 2, v: r.v0 + r.dv / 2 }; };
const buffer = () => ({ xz: [], uv: [], tri: [] });

// lib/trace.js texPoly into a buffer: part of blade b's picture, a polygon in its own uv, from cell r.
function polyInto(out, poly, b, project, r, shift = null) {
  if (poly.length < 3) return;
  const pts = poly.map(q => project(shift ? plus(at3(b, q), shift) : at3(b, q)));
  let area = 0;
  for (let i = 0; i < pts.length; i++) { const p = pts[i], n = pts[(i + 1) % pts.length]; area += p.x * n.z - n.x * p.z; }
  const base = out.xz.length / 2;
  for (let i = 0; i < poly.length; i++) {
    const k = area > 0 ? poly.length - 1 - i : i;
    out.xz.push(pts[k].x, pts[k].z);
    out.uv.push(r.u0 + poly[k].u * r.du, r.v0 + poly[k].v * r.dv);
    if (i >= 2) out.tri.push(base, base + i - 1, base + i);
  }
}
// A strip between point lists a and b in one flat swatch (band / strip).
function stripInto(out, a, b, sw) {
  const base = out.xz.length / 2;
  for (let i = 0; i < a.length; i++) {
    out.xz.push(a[i].x, a[i].z, b[i].x, b[i].z);
    out.uv.push(sw.u, sw.v, sw.u, sw.v);
    if (i) { const n = base + i * 2; out.tri.push(n - 2, n, n - 1, n - 1, n, n + 1); }
  }
}
// lib/goku.js line into a buffer.
function lineInto(out, pts, width, sw, taper) {
  const a = [], b = [], last = pts.length - 1;
  pts.forEach((q, i) => {
    const prev = pts[Math.max(0, i - 1)], next = pts[Math.min(last, i + 1)], dx = next.x - prev.x, dz = next.z - prev.z, len = Math.hypot(dx, dz) || 1, u = i / last;
    const w = width / 2 * (taper === 'both' ? Math.sin(u * Math.PI) : taper === 'end' ? Math.pow(1 - u, .6) : 1) + .004;
    a.push({ x: q.x - dz / len * w, z: q.z + dx / len * w });
    b.push({ x: q.x + dz / len * w, z: q.z - dx / len * w });
  });
  stripInto(out, a, b, sw);
}
// sprite() into a buffer, with the whole of cell r across it.
function spriteInto(out, pos, w, h, angle, r) {
  const t = angle * D2R, ct = Math.cos(t), st = Math.sin(t), base = out.xz.length / 2;
  [[-.5, -.5, 0, 0], [-.5, .5, 0, 1], [.5, .5, 1, 1], [.5, -.5, 1, 0]].forEach(([x, z, u, v]) => {
    const px = x * w, pz = z * h;
    out.xz.push(pos.x + px * ct + pz * st, pos.z - px * st + pz * ct);
    out.uv.push(r.u0 + u * r.du, r.v0 + v * r.dv);
  });
  out.tri.push(base, base + 1, base + 2, base, base + 2, base + 3);
}
// blade() of lib/trace.js for a sword standing at rest: its shadow, both edges, the face lit one of three
// ways, the two dark bands low on the blade.
function bladeInto(shadows, blades, b, sun, row) {
  const above = clip(Square, higherThan(b, 0));
  polyInto(shadows, above, b, alongSun(sun), cell(Faces[2][1], row));
  [1, .5].forEach((f, i) => polyInto(blades, above, b, onScreen, cell(i ? Edge1 : Edge0, row), v3(-b.N.x * .03 * f, -b.N.y * .03 * f, -b.N.z * .03 * f)));
  const lit = .8 + .2 * Math.max(0, dot3(b.N, unit(v3(-sun.x, 1, -sun.z))));
  const face = Faces.reduce((best, f) => Math.abs(f[0] - lit) < Math.abs(best[0] - lit) ? f : best);
  polyInto(blades, above, b, onScreen, cell(face[1], row));
  polyInto(blades, clip(above, lowerThan(b, .2)), b, onScreen, cell(LowBand, row));
  polyInto(blades, clip(above, lowerThan(b, .08)), b, onScreen, cell(LowerBand, row));
}
// plant() of lib/trace.js on the floor: the contact shadow, cracks, the slit.
function marksInto(marks, cut, seed, cracks) {
  const { D, F, half } = cut, rot = -Math.atan2(D.z, D.x) / D2R;
  const pt = (along, out) => ({ x: cut.x + D.x * along + F.x * out, z: cut.z + D.z * along + F.z * out });
  spriteInto(marks, pt(0, .015), half * 2 + .35, .2, rot, cell(SwContact, 6));
  for (let i = 0; i < cracks; i++) {
    const end = i < 2, side = i % 2 ? 1 : -1;
    const start = end ? pt(side * half * .9, 0) : pt((rand(seed * 7 + i) - .5) * half * 1.4, 0);
    const ang = end ? Math.atan2(D.z * side, D.x * side) + (rand(seed * 3 + i) - .5) * .6 : Math.atan2(F.z * side, F.x * side) + (rand(seed * 5 + i) - .5) * 1.7;
    const len = .1 + rand(seed * 11 + i) * .17, pts = [start];
    for (let j = 1; j <= 4; j++) {
      const a = ang + (rand(seed * 13 + i * 5 + j) - .5) * 1.1, q = pts[j - 1];
      pts.push({ x: q.x + Math.cos(a) * len / 4, z: q.z + Math.sin(a) * len / 4 });
    }
    lineInto(marks, pts, .024, swatch(SwCrack), 'end');
  }
  lineInto(marks, [pt(-half - .035, 0), pt(0, 0), pt(half + .035, 0)], .055, swatch(SwHole), 'both');
}
// plant()'s lip of earth either side of the slit (side 1 faces the camera), into the blades' buffer so
// the back lip goes under its own blade and the front lip over its foot.
function lipInto(blades, cut, side, reach, seed, sun) {
  const { D, half } = cut, F = { x: cut.F.x * side, z: cut.F.z * side }, n = 9, inner = [], crest = [], outer = [];
  for (let i = 0; i < n; i++) {
    const t = i / (n - 1), along = -half - .045 + (half * 2 + .09) * t, bulge = Math.pow(Math.sin(t * Math.PI), .6);
    const out = .01 + reach * bulge * (.7 + .6 * rand(seed * 11 + i)), back = (side > 0 ? .024 : .012) * bulge;
    const base = { x: cut.x + D.x * along, z: cut.z + D.z * along };
    inner.push({ x: base.x - F.x * back, z: base.z - F.z * back });
    crest.push({ x: base.x + F.x * out * .3, z: base.z + F.z * out * .3 });
    outer.push({ x: base.x + F.x * out, z: base.z + F.z * out });
  }
  const sunward = -(F.x * sun.x + F.z * sun.z) > 0;
  stripInto(blades, inner, outer, swatch(sunward ? SwDirtMid : SwDirtDark));
  stripInto(blades, inner, crest, swatch(sunward ? SwDirtLit : SwDirtMid));
}

const Mix = ['LongSword', 'LongSword', 'LongSword', 'Spear', 'Spear', 'MonoSword', 'MonoSword', 'Knife', 'LargeSword', 'Wyrmslayer'];
// The part of the screen a standing sword covers: from its foot to its pommel drawn with the height
// rule, margin wider either side. A pawn draws over every sword, so a sword whose blade rises through a
// landing spot would look run through the pawn standing there.
function screenBox(sw, margin) {
  const L = sw.w.length * sw.w.image * sw.size * (1 - sw.sink), l = sw.lean * D2R, d = sw.dir * D2R, sz = sw.z + sw.lift;
  const px = sw.x + Math.sin(l) * Math.cos(d) * L, pz = sz + Math.sin(l) * Math.sin(d) * L + Math.cos(l) * L * Lift;
  return { x0: Math.min(sw.x, px) - margin, x1: Math.max(sw.x, px) + margin, z0: Math.min(sz, pz) - .15, z1: Math.max(sz, pz) + .15 };
}
// A stand-in pawn on the screen: its shadow's reach below its feet to the top of its head.
const pawnBox = q => ({ x0: q.x - .32, x1: q.x + .32, z0: q.z - .2, z1: q.z + .9 });
const clearOf = (box, keep) => !keep.some(q => { const b = pawnBox(q); return box.x0 < b.x1 && b.x0 < box.x1 && box.z0 < b.z1 && b.z0 < box.z1; });
// Every sword of the world, relative to the caster, north first. A jittered grid 1.2 cells apart keeps
// the field even; density is swords per cell on the map, doubling toward the top of the hill, 0.6 of
// it past the map edge. On the hill they lean out, down its slope. Every sword is its weapon's own size
// (a copy of a studied weapon), give or take 10 %. keep: the landing spots; no sword is drawn over one
// (in game the pocket map is made after everyone taken is known, so it can do the same). heightAt(x, z),
// if given, is the ground's height under a sword (lib/ubw-terrain.js): the sword stands that much higher,
// drawn Lift cells further north per cell (sw.lift), and the list is ordered by that screen foot.
export function makeField({ density, hill: hillRadius, beyond, size, lean }, keep = [], heightAt = null) {
  const step = 1.2, reach = MapHalf + beyond, list = [];
  let n = 0;
  for (let gz = -reach; gz <= reach + 1e-6; gz += step) for (let gx = -reach; gx <= reach + 1e-6; gx += step) {
    const seed = ++n;
    const x = gx + (rand(seed * 3 + 1) - .5) * step * .9, z = gz + (rand(seed * 5 + 2) - .5) * step * .9;
    const d = Math.hypot(x, z), far = Math.max(Math.abs(x), Math.abs(z)) > MapHalf, onHill = d < hillRadius;
    let want = density * (far ? .6 : 1);
    if (onHill) want *= 1 + 2 * (1 - d / hillRadius);
    if (rand(seed * 7 + 3) > want * step * step || d < 1) continue;
    const sw = {
      seed, x, z, d, far, w: Weapons[Mix[Math.floor(rand(seed * 11 + 4) * Mix.length)]],
      lean: lean * (onHill ? .45 + .55 * rand(seed * 13) : rand(seed * 13)),
      dir: onHill ? Math.atan2(z, x) / D2R + (rand(seed * 17) - .5) * 60 : rand(seed * 17) * 360,
      turn: (rand(seed * 19) - .5) * 60, sink: .16 + rand(seed * 23) * .12, size: size * (.9 + .2 * rand(seed * 29)),
      lift: heightAt ? heightAt(x, z) * Lift : 0,
    };
    if (clearOf(screenBox(sw, .3), keep)) list.push(sw);
  }
  return list.sort((a, b) => (b.z + b.lift) - (a.z + a.lift));
}
// makeField without the bake, for a sketch's plan and clock: the same swords, the same seeds. Kept per
// settings and landing spots; the list is shared, so it is not to be changed.
const lists = new Map();
export function fieldList(settings, keep = [], ground = null) {
  const id = JSON.stringify([settings, keep, ground?.key ?? null]);
  if (!lists.has(id)) {
    lists.set(id, makeField(settings, keep, ground?.heightAt));
    if (lists.size > 8) lists.delete(lists.keys().next().value);
  }
  return lists.get(id);
}
// A sword of the field standing in its place round c (field() bakes this pose as sw.b).
export const standingPose = (sw, c) => upright(sw.w, sw.size, { x: c.x + sw.x, z: c.z + sw.z + (sw.lift || 0) }, sw.lean, sw.dir, sw.turn, sw.sink);

const fields = new Map();
// The field for these settings round c under this sun, baked: each sword's pose b, and the meshes.
// Swords past the map edge get no lips or cracks (they are far off and hazed). The last three bakes are
// kept; their meshes are dropped with them.
//   omit: seeds of swords left out of the bake. A command's swords: the sketch draws them one by one.
//   cuts: map z values where the blades mesh is split, so that what the sketch draws one by one (a
//         command's sword standing at z, or stuck in the ground at z) goes between the swords north of it
//         and those south of it (drawField's inserts). Without cuts the blades are one mesh.
//   ground: the ground with height (lib/ubw-terrain.js terrain()): the swords stand on its plates.
export function field(settings, c, sun, keep = [], { omit = [], cuts = [], ground = null } = {}) {
  const gone = new Set(omit), zs = cuts.slice().sort((a, b) => b - a);
  const id = JSON.stringify([settings, c.x, c.z, sun.x, sun.z, keep, [...gone].sort((a, b) => a - b), zs, ground?.key ?? null]);
  let f = fields.get(id);
  if (f) return f;
  const list = makeField(settings, keep, ground?.heightAt), shadows = buffer(), marks = buffer(), parts = [buffer()];
  list.forEach(sw => {
    // the list is north first (by the screen foot): a sword at or south of the next cut starts the next part
    while (parts.length <= zs.length && c.z + sw.z + sw.lift <= zs[parts.length - 1]) parts.push(buffer());
    sw.b = standingPose(sw, c);
    sw.top = pommelOf(sw.b).y + .05;
    if (gone.has(sw.seed)) return;
    const blades = parts[parts.length - 1], row = AtlasNames.indexOf(sw.w.name), cut = cutOf(sw.b, sw.sink);
    marksInto(marks, cut, sw.seed, sw.far ? 0 : 4);
    if (!sw.far) lipInto(blades, cut, -1, .026, sw.seed, sun);
    bladeInto(shadows, blades, sw.b, sun, row);
    if (!sw.far) lipInto(blades, cut, 1, .032, sw.seed + 3, sun);
  });
  while (parts.length <= zs.length) parts.push(buffer());
  const bake = (name, b) => { const m = new Mesh(`ubwp field ${name}`); m.setFlat(b.xz, b.tri); m.uv = new Float32Array(b.uv); return m; };
  f = {
    list, cuts: zs, shadows: bake('shadows', shadows), marks: bake('marks', marks),
    blades: parts.map((b, k) => b.tri.length ? bake(`blades ${k}`, b) : null),
    vertices: (shadows.xz.length + marks.xz.length + parts.reduce((sum, b) => sum + b.xz.length, 0)) / 2,
  };
  fields.set(id, f);
  if (fields.size > 3) fields.delete(fields.keys().next().value);
  return f;
}
// Part k of a split field's blades draws at buildingLayer + k * CutStep, and what goes in after it at
// + CutStep * 0.375: room for lib/trace.js plant() and blade(), which reach from 0.0006 below their layer
// to 0.003 above it. 60 cuts stay under the haze.
const CutStep = .008;
// The baked field: shadows, ground marks, blades. tint colours the marks and blades. inserts: what the
// sketch draws one by one, each { z, fn(layer) } with z one of the field's cuts; it is drawn after the
// blades north of z and before those south of it.
export function drawField(f, strength, tint, alpha = 1, inserts = []) {
  draw(f.shadows, 0, shadowLayer + .002, 0, 1, 1, 0, Black.withAlpha(.42 * strength / .32 * alpha), atlas);
  draw(f.marks, 0, Floor + .002, 0, 1, 1, 0, tint.withAlpha(alpha), atlas);
  f.blades.forEach((m, k) => { if (m) draw(m, 0, buildingLayer + k * CutStep, 0, 1, 1, 0, tint.withAlpha(alpha), atlas); });
  // after part k: the number of cuts north of z
  inserts.forEach(q => q.fn(buildingLayer + f.cuts.filter(z => z > q.z).length * CutStep + CutStep * .375));
}
// One sword's trace drawn over its baked steel: the wire outline at wireAlpha and, if scan >= 0, the
// bright line that runs up it (a scan of 0..1 of its height).
export function traceOver(sw, sun, strength, wireAlpha, scan = -1) {
  blade(`ubwp over ${sw.seed}`, sw.b, sun, strength, HazeLayer + .05, { fillTo: -1, wireTo: 9, wireAlpha, scan: scan >= 0 ? sw.top * scan : -1 });
}
