// Trace kit: the horizon of Unlimited Blade Works (2026-09-26), for "Unlimited Blade Works: world v3, horizon
// (pocket)". Not a sketch itself, so it is not listed in sketches/index.js. An experiment, nothing agreed.
//
// The camera looks straight down, so a sky can only show where the ground ends. v2 put a sky strip past a far
// ridge 36 to 41 cells north of the caster, off the top of the screen at the usual zoom, so the gears were
// only seen as their shadows. v3 moves the map's north edge to 13 cells north of the caster and, past it,
// squeezes the ground toward a horizon the way a camera tilted toward it would see it (the SNES airship maps
// of Final Fantasy VI do the same with Mode 7):
//   - past the edge, a point d cells out is drawn s(d) cells past it and pulled toward the camera's x by
//     L(d); its height is drawn 0.6 L(d) north per cell. L(d) = 1 / (1 + q(d / K)), q(x) = x² / (1 + x): at
//     the edge there is no bend and no sudden squeeze (the join does not show), far out the plain shrinks as
//     1 / d like a real horizon. s grows as L² and levels off HT cells past the edge: the horizon (5 by
//     default). As L pulls toward the camera's x, the far plain slides slower than the map when the camera
//     pans; the horizon itself stays fixed to the map in z, so panning north shows more sky;
//   - the map's plates (lib/ubw-terrain.js) run on past the edge through the squeeze, cut at the edge, so
//     their cracks carry across; under them a band shades the plain from the crack floor to the haze;
//   - four ridges stand on the plain 7, 14, 30 and 110 cells out, each hazier, swords along the near three;
//   - swords stand on the plain out to 110 cells, world-fixed, fewer per cell the further out so the screen
//     fills evenly toward a dense line at the horizon: out to 10 cells they are the field's own swords
//     (bladeInto of lib/ubw-pocket.js through the squeeze), past that two strokes each;
//   - above the horizon the sky: a gradient from the glow at the horizon to dusk red, the low sun where the
//     scene's shadows point away from, clouds lit from below near the sun, smog, seven spoked gears standing
//     on and above the horizon, the lower ones partly behind the ridges. A thing of the sky moves by 1 - p of
//     the camera's pan (p 0 for the sun, 0.04 for clouds, 0.2 to 0.5 for gears): parallax;
//   - a haze tied to the camera: none at the bottom of the screen, up to maxHaze at the top, over the ground
//     and the swords, under the pawns;
//   - embers and flakes of ash in front of everything, moving 1.45 times the camera's pan.
// Everything past the edge depends on the camera's x, so it is rebuilt when the camera moves (in game: when
// Find.Camera moves), kept per camera x to 0.05 cells.
import { Color, MaterialPool, Mesh, MeshPool, ShaderDatabase } from '../../js/engine.js';
import { registerLabTexture, pixels, hash, fbm } from '../../js/standins.js';
import { draw, mesh } from './six-paths-solid.js';
import { Lift, soft, glow, Y } from './six-paths-impact.js';
import { buildingLayer, shadowLayer } from './goku.js';
import { solid, Black, D2R, clamp, lerp, Weapons, upright } from './trace.js';
import { BaseLayer, Base, bakeTerrain, terrainAtlas, topUV } from './ubw-terrain.js';
import { bladeInto, AtlasNames, Mix, atlas as swordAtlas, quads, clusterAt, Look } from './ubw-pocket.js';

// Colours: the v2 sky's (lib/ubw-sky.js), a glow at the horizon, the plain's earth and its haze.
const Glow = new Color(1, .89, .67), Horizon = new Color(1, .62, .3), Mid = new Color(.86, .36, .2), High = new Color(.5, .17, .16), Top = new Color(.24, .08, .11);
const SunFace = new Color(1, .96, .88), SunWarm = new Color(1, .75, .45), Silhouette = new Color(.12, .05, .05), Smog = new Color(.27, .09, .09);
const RimLight = new Color(1, .67, .35), CloudDark = new Color(.36, .13, .14), CloudWarm = new Color(.6, .26, .2), CloudLit = new Color(1, .77, .47);
const FarEarth = new Color(.4, .26, .19), RidgeEarth = new Color(.34, .22, .16), HazeFar = new Color(.93, .61, .36), Fog = new Color(.89, .56, .35);
const Steel = new Color(.6, .58, .6), Grip = new Color(.2, .12, .09), Ash = new Color(.27, .21, .2), Spark = new Color(1, .7, .38);
const mixC = (a, b, t) => new Color(lerp(a.r, b.r, t), lerp(a.g, b.g, t), lerp(a.b, b.b, t), 1);

// Screen cells per degree of the sky: across (azimuth) and up (elevation). The sky band is SkyCells tall.
export const KA = .5, KE = .3;
const SkyCells = 40, SunUp = 4.5, FarMax = 110, BladeFar = 10, HazeAt = 420;
// The ridges: d cells past the edge, h cells tall at most, amp of that is wobble, f its wave number.
export const Ridges = [
  { d: 110, h: 110, amp: .6, f: .012, ph: 3, swords: false },
  { d: 30, h: 20, amp: .55, f: .045, ph: 2, swords: true },
  { d: 14, h: 6, amp: .6, f: .09, ph: 4, swords: true },
  { d: 7, h: 2.4, amp: .55, f: .16, ph: 1, swords: true },
];
// The gears of the sky: a azimuth and e elevation of the centre (degrees), r radius (degrees), type 0 a
// six-spoke wheel, 1 a double ring on four spokes, 2 a heavy three-spoke wheel; spin degrees a second,
// p parallax, haze toward the sky's colour.
export const SkyGears = [
  { a: -40, e: 3, r: 26, type: 0, teeth: 18, spin: 2, p: .5, haze: .04 },
  { a: 31, e: 21, r: 15, type: 1, teeth: 16, spin: -3, p: .36, haze: .14 },
  { a: 5, e: 8, r: 9, type: 2, teeth: 12, spin: 4, p: .42, haze: .34 },
  { a: 54, e: 2, r: 12, type: 0, teeth: 14, spin: -3.5, p: .46, haze: .2 },
  { a: -11, e: 27, r: 7, type: 1, teeth: 12, spin: 5, p: .26, haze: .46 },
  { a: -64, e: 23, r: 10, type: 2, teeth: 12, spin: -2.5, p: .3, haze: .36 },
  { a: 15, e: 41, r: 5.5, type: 0, teeth: 10, spin: 6, p: .2, haze: .58 },
];

// Order of the far drawings, all between the crack floor and the map's plates (lib/ubw-terrain.js).
const L = k => BaseLayer + .0002 + k * .00005;

export function skyAt(e) {
  if (e < 6) return mixC(Glow, Horizon, clamp(e / 6));
  if (e < 16) return mixC(Horizon, Mid, (e - 6) / 10);
  if (e < 38) return mixC(Mid, High, (e - 16) / 22);
  return mixC(High, Top, clamp((e - 38) / 40));
}
registerLabTexture('lab/ubw-horizon-sky', () => pixels(128, (u, v) => {
  const e = ((1 - v) * (SkyCells + 1) - 1) / KE, c = skyAt(e);
  return [c.r, c.g, c.b, 1];
}));
// The plain from the edge (bottom row) to the horizon (top row): crack floor, earth, haze, with faint bands
// across it, closer together toward the horizon, so the far plain is not one flat strip.
registerLabTexture('lab/ubw-horizon-ground', () => pixels(256, (u, v) => {
  const t = 1 - v, c = t < .05 ? mixC(Base, FarEarth, t / .05) : mixC(FarEarth, HazeFar, Math.pow((t - .05) / .95, 2.6));
  const band = fbm(u * 3, Math.pow(t, .45) * 40, 83, 3, 64), k = .88 + .24 * band * (1 - .6 * t);
  return [Math.min(1, c.r * k), Math.min(1, c.g * k), Math.min(1, c.b * k), 1];
}));
// The camera's haze: alpha 1 at the top row, 0 at the bottom.
registerLabTexture('lab/ubw-horizon-depth', () => pixels(64, (u, v) => [1, 1, 1, Math.pow(1 - v, 1.7)]));
const skyMat = MaterialPool.MatFrom('lab/ubw-horizon-sky', ShaderDatabase.Transparent);
const groundMat = MaterialPool.MatFrom('lab/ubw-horizon-ground', ShaderDatabase.Transparent);
const depthMat = MaterialPool.MatFrom('lab/ubw-horizon-depth', ShaderDatabase.Transparent);

// ---- the squeeze -----------------------------------------------------------------------------------------
// s(d) and L(d) from a table: L as above, s its square integrated, K set so that s levels off at ht.
const squeezes = new Map();
export function squeeze(ht) {
  if (squeezes.has(ht)) return squeezes.get(ht);
  const N = 2400, DM = 9000, q = x => x * x / (1 + x), S = new Float64Array(N + 1), Ls = new Float64Array(N + 1);
  const build = K => {
    let s = 0, pd = 0, pf = 1;
    for (let i = 0; i <= N; i++) {
      const d = DM * Math.pow(i / N, 3), l = 1 / (1 + q(d / K)), f = l * l;
      if (i) s += (d - pd) * (f + pf) / 2;
      S[i] = s; Ls[i] = l; pd = d; pf = f;
    }
    return s;
  };
  build(ht / build(1));
  const at = (arr, d) => {
    if (d <= 0) return arr[0];
    const u = N * Math.cbrt(Math.min(d, DM) / DM), i = Math.min(N - 1, Math.floor(u));
    return arr[i] + (arr[i + 1] - arr[i]) * (u - i);
  };
  const sq = { ht, s: d => at(S, d), L: d => at(Ls, d) };
  squeezes.set(ht, sq);
  return sq;
}
// A 3D point (x, z on the map, y its height, all absolute) on the screen: the kit's height rule south of the
// edge (edgeZ, absolute), the squeeze past it.
export const projector = (edgeZ, sq, camX) => p => {
  const d = p.z - edgeZ;
  if (d <= 0) return { x: p.x, z: p.z + p.y * Lift };
  const l = sq.L(d);
  return { x: camX + (p.x - camX) * l, z: edgeZ + sq.s(d) + p.y * Lift * l };
};
// A sword's point on the screen, with the squeeze taken at its foot only: the whole sword is scaled as one
// piece round its foot (by L across, L² along the plain, 0.6 L up). With projector() a sword to the side of
// the screen leans hard toward the middle, since its tip and foot are pulled in by different amounts.
export const swordProjector = (edgeZ, sq, camX, foot) => {
  const d = foot.z - edgeZ, l = sq.L(d), fx = camX + (foot.x - camX) * l, fz = edgeZ + sq.s(d);
  return p => ({ x: fx + (p.x - foot.x) * l, z: fz + (p.z - foot.z) * l * l + p.y * Lift * l });
};
const hazeOf = d => Math.sqrt(clamp(d / HazeAt));

// ---- buffers ---------------------------------------------------------------------------------------------
const buffer = () => ({ xz: [], uv: [], tri: [] });
// A convex polygon of screen points, clockwise on screen as every mesh of the kit.
function polyInto(out, pts, uvs) {
  let area = 0;
  for (let i = 0; i < pts.length; i++) { const a = pts[i], b = pts[(i + 1) % pts.length]; area += a.x * b.z - b.x * a.z; }
  if (Math.abs(area) < 1e-7) return;
  const base = out.xz.length / 2;
  for (let i = 0; i < pts.length; i++) {
    const k = area > 0 ? pts.length - 1 - i : i;
    out.xz.push(pts[k].x, pts[k].z);
    out.uv.push(uvs ? uvs[k][0] : .5, uvs ? uvs[k][1] : .5);
    if (i >= 2) out.tri.push(base, base + i - 1, base + i);
  }
}
// A stroke from a to b (screen points), w wide.
function strokeInto(out, a, b, w) {
  const dx = b.x - a.x, dz = b.z - a.z, len = Math.hypot(dx, dz) || 1, nx = -dz / len * w / 2, nz = dx / len * w / 2;
  polyInto(out, [{ x: a.x + nx, z: a.z + nz }, { x: b.x + nx, z: b.z + nz }, { x: b.x - nx, z: b.z - nz }, { x: a.x - nx, z: a.z - nz }]);
}
// A new mesh per bake (dropped with its bake), as lib/ubw-pocket.js field() does.
function meshOf(name, b) {
  const m = new Mesh(name);
  m.setFlat(b.xz, b.tri);
  m.uv = new Float32Array(b.uv);
  return m;
}
// The part of a polygon ({ x, z }, cells from the caster) north (keepNorth) or south of z0.
function clipZ(poly, z0, keepNorth) {
  const out = [], inside = q => keepNorth ? q.z >= z0 : q.z <= z0;
  for (let i = 0; i < poly.length; i++) {
    const a = poly[i], b = poly[(i + 1) % poly.length];
    if (inside(a)) out.push(a);
    if (inside(a) !== inside(b)) { const t = (z0 - a.z) / (b.z - a.z); out.push({ x: a.x + (b.x - a.x) * t, z: z0, nb: -1 }); }
  }
  return out;
}

// ---- the map's ground, cut at the edge ---------------------------------------------------------------------
// The plates south of the edge, baked as lib/ubw-terrain.js bakes them. A plate cut by the edge keeps its own
// bounds (so its earth window lines up with the part drawn past the edge) and loses its hairline cracks.
const souths = new Map();
export function mapTerrain(T, north, sun) {
  const key = `${T.key}|${north}|${sun.x.toFixed(3)},${sun.z.toFixed(3)}`;
  if (souths.has(key)) return souths.get(key);
  const plates = T.plates.map(p => {
    if (!p) return null;
    if (p.maxZ <= north) return p;
    if (p.minZ >= north) return null;
    const poly = clipZ(p.poly, north, false);
    if (poly.length < 3) return null;
    let area = 0;
    for (let i = 0; i < poly.length; i++) { const a = poly[i], b = poly[(i + 1) % poly.length]; area += a.x * b.z - b.x * a.z; }
    return { ...p, poly, sgn: area > 0 ? 1 : -1, tier: p.tier || .5 };
  });
  const b = bakeTerrain({ ...T, plates }, sun, `v3 ${key}`);
  souths.set(key, b);
  if (souths.size > 3) souths.delete(souths.keys().next().value);
  return b;
}

// ---- past the edge -----------------------------------------------------------------------------------------
export const ridgeTop = (R, x) => R.h * (1 - R.amp + R.amp * clamp(.5 + .25 * Math.sin(x * R.f + R.ph) + .15 * Math.sin(x * R.f * 2.7 + R.ph * 3) + .1 * Math.sin(x * R.f * 6.1 + R.ph * 5) + .12 * (fbm(x * R.f * 4 + R.ph * 10, R.ph, 87, 2, 64) - .5)));
// Rows of swords on the plain, fixed to the world: row k at d (cells past the edge), swords gx cells apart
// across it. Row and sword spacing on the screen shrink from 1.3 and 1.5 cells at the edge to 0.45 and 0.5 far
// out, so the plain thickens toward the horizon.
const rowsOf = new Map();
function swordRows(sq) {
  if (rowsOf.has(sq.ht)) return rowsOf.get(sq.ht);
  const rows = [];
  for (let d = .7; d < FarMax;) {
    const l = sq.L(d), far = d > BladeFar ? .6 : 1, dz = 1.3 * (.35 + .65 * l) * far, gx = 1.5 * (.33 + .67 * l) * far / l;
    rows.push({ d, step: dz / (l * l), gx });
    d += dz / (l * l);
  }
  rowsOf.set(sq.ht, rows);
  return rows;
}
// Distance groups of the far swords, and where the ridges and the far plates go between them.
const Groups = [[60, FarMax], [30, 60], [14, 30], [10, 14], [7, 10], [3, 7], [0, 3]];
const groupOf = d => Groups.findIndex(([a, b]) => d >= a && d < b);

// Everything past the edge for this camera x, baked: far plates behind and in front of the near ridge,
// the ridges with their rims and swords, the far swords by group (blades from the sword atlas, strokes).
const fars = new Map();
function farBake(T, c, north, sq, sun, camX, halfW, ridgeScale) {
  const qx = Math.round(camX * 20) / 20;
  const key = `${T.key}|${c.x},${c.z}|${north}|${sq.ht}|${qx}|${Math.round(halfW)}|${ridgeScale}|${sun.x.toFixed(3)},${sun.z.toFixed(3)}`;
  if (fars.has(key)) return fars.get(key);
  const edgeZ = c.z + north, project = projector(edgeZ, sq, qx), camR = qx - c.x;
  const plates = [buffer(), buffer()], ridges = Ridges.map(() => ({ fill: buffer(), rim: buffer(), swords: buffer() }));
  const blades = Groups.map(() => buffer()), steel = Groups.map(() => buffer()), grips = Groups.map(() => buffer());

  // The plates' far parts: each edge cut into pieces under 0.7 cells, so the squeeze bends them.
  T.plates.forEach((p, i) => {
    if (!p || p.maxZ <= north) return;
    const poly = clipZ(p.poly, north, true);
    if (poly.length < 3) return;
    const fine = [];
    poly.forEach((a, k) => {
      const b = poly[(k + 1) % poly.length], n = Math.max(1, Math.ceil(Math.hypot(b.x - a.x, b.z - a.z) / .7));
      for (let j = 0; j < n; j++) fine.push({ x: a.x + (b.x - a.x) * j / n, z: a.z + (b.z - a.z) * j / n });
    });
    const uv = topUV(T, i), pts = fine.map(q => project({ x: c.x + q.x, y: p.h, z: c.z + q.z }));
    polyInto(plates[T.seeds[i].z - north < Ridges[3].d ? 1 : 0], pts, fine.map(uv));
  });

  // The ridges: from the plain up to the wobbling top, a lit rim along it, swords standing on the near two.
  Ridges.forEach((R, r) => {
    const l = sq.L(R.d), span = halfW / l * 1.15 + 6, n = 160, z = c.z + north + R.d, out = ridges[r];
    let prevTop = null, prevFoot = null;
    for (let i = 0; i <= n; i++) {
      const x = camR - span + 2 * span * i / n, h = ridgeTop(R, x) * ridgeScale;
      const top = project({ x: c.x + x, y: h, z }), foot = project({ x: c.x + x, y: 0, z: z - .5 });
      if (prevTop) {
        polyInto(out.fill, [prevFoot, prevTop, top, foot]);
        polyInto(out.rim, [prevTop, top, { x: top.x, z: top.z - .05 }, { x: prevTop.x, z: prevTop.z - .05 }]);
      }
      prevTop = top; prevFoot = foot;
    }
    if (!R.swords) return;
    const gap = 1.3 / l;
    for (let j = Math.floor((camR - span) / gap); j <= (camR + span) / gap; j++) {
      if (hash(j, r, 91) < .45) continue;
      const x = (j + .5 * hash(j, r, 92)) * gap, h = ridgeTop(R, x) * ridgeScale, len = 1.3 + .5 * hash(j, r, 93), lean = (hash(j, r, 94) - .5) * .3;
      const at = swordProjector(c.z + north, sq, qx, { x: c.x + x, z });
      const foot = at({ x: c.x + x, y: h, z }), guard = at({ x: c.x + x + lean * .7, y: h + len * .7, z }), tip = at({ x: c.x + x + lean, y: h + len, z });
      strokeInto(out.swords, foot, guard, Math.max(.04, .1 * l));
      strokeInto(out.swords, guard, tip, Math.max(.035, .08 * l));
    }
  });

  // The far swords, row by row, the part of each row in view.
  swordRows(sq).forEach((row, k) => {
    const l0 = sq.L(row.d), span = halfW / l0 * 1.1 + 4;
    for (let j = Math.floor((camR - span) / row.gx); j <= (camR + span) / row.gx; j++) {
      const x = (j + .15 + .7 * hash(j, k, 71)) * row.gx, d = row.d + (hash(j, k, 72) - .5) * row.step * .8;
      if (d <= .4 || d >= FarMax) continue;
      if (hash(j, k, 73) > .62 * clamp(1 + (clusterAt(x, north + d) - 1) * .8, .15, 2)) continue;
      const w = Weapons[Mix[Math.floor(hash(j, k, 74) * Mix.length)]], size = Look.size * (.9 + .2 * hash(j, k, 75));
      const lean = Look.lean * hash(j, k, 76), dir = hash(j, k, 77) * 360, turn = (hash(j, k, 78) - .5) * 60, sink = .16 + hash(j, k, 79) * .12;
      const g = groupOf(d), foot = { x: c.x + x, z: c.z + north + d }, onScreen = swordProjector(c.z + north, sq, qx, foot);
      if (d < BladeFar) {
        bladeInto(null, blades[g], upright(w, size, foot, lean, dir, turn, sink), sun, AtlasNames.indexOf(w.name), onScreen);
        continue;
      }
      const len = w.length * w.image * size * (1 - sink), la = lean * D2R, da = dir * D2R, l = sq.L(d);
      const at = f => onScreen({ x: foot.x + Math.sin(la) * Math.cos(da) * len * f, y: Math.cos(la) * len * f, z: foot.z + Math.sin(la) * Math.sin(da) * len * f });
      const base = onScreen({ x: foot.x, y: 0, z: foot.z }), guard = at(.72), tip = at(1);
      strokeInto(steel[g], base, guard, Math.max(.045, .1 * l));
      strokeInto(grips[g], guard, tip, Math.max(.04, .08 * l));
    }
  });

  const f = {
    plates: plates.map((b, i) => meshOf(`ubw horizon plates ${i}`, b)),
    ridges: ridges.map((R, i) => ({ fill: meshOf(`ubw horizon ridge ${i}`, R.fill), rim: meshOf(`ubw horizon rim ${i}`, R.rim), swords: meshOf(`ubw horizon ridge swords ${i}`, R.swords) })),
    blades: blades.map((b, i) => b.tri.length ? meshOf(`ubw horizon blades ${i}`, b) : null),
    steel: steel.map((b, i) => b.tri.length ? meshOf(`ubw horizon steel ${i}`, b) : null),
    grips: grips.map((b, i) => b.tri.length ? meshOf(`ubw horizon grips ${i}`, b) : null),
    vertices: [...plates, ...blades, ...steel, ...grips].reduce((n, b) => n + b.xz.length / 2, 0),
  };
  fars.set(key, f);
  if (fars.size > 4) fars.delete(fars.keys().next().value);
  return f;
}

// ---- gears ---------------------------------------------------------------------------------------------
// A gear of radius 1 as rings and spokes (no holes needed): the toothed rim, and per type its spokes, inner
// ring and hub. Kept per type and tooth count.
const shapes = new Map();
function gearShape(type, teeth) {
  const id = `${type} ${teeth}`;
  if (shapes.has(id)) return shapes.get(id);
  const xz = [], tri = [];
  const ring = (r0, r1, n, toothed) => {
    const base = xz.length / 2;
    for (let j = 0; j < n; j++) {
      const a = j / n * Math.PI * 2, k = j % 4, out = toothed ? (k === 1 || k === 2 ? 1 : .88) : r1;
      xz.push(Math.cos(a) * out, Math.sin(a) * out, Math.cos(a) * r0, Math.sin(a) * r0);
      const o = base + j * 2, next = base + ((j + 1) % n) * 2;
      tri.push(o, next, o + 1, o + 1, next, next + 1);
    }
  };
  const spokes = (count, r0, r1, w, turn = 0) => {
    for (let k = 0; k < count; k++) {
      const a = k / count * Math.PI * 2 + turn, cs = Math.cos(a), sn = Math.sin(a), b = xz.length / 2;
      xz.push(cs * r0 - sn * w, sn * r0 + cs * w, cs * r1 - sn * w, sn * r1 + cs * w, cs * r1 + sn * w, sn * r1 - cs * w, cs * r0 + sn * w, sn * r0 - cs * w);
      tri.push(b, b + 1, b + 2, b, b + 2, b + 3);
    }
  };
  if (type === 0) { ring(.74, 1, teeth * 4, true); spokes(6, .2, .76, .045); ring(.1, .26, 48, false); }
  else if (type === 1) { ring(.8, 1, teeth * 4, true); spokes(4, .66, .82, .05, Math.PI / 4); ring(.6, .68, 64, false); spokes(4, .3, .62, .06); ring(.14, .34, 48, false); }
  else { ring(.7, 1, teeth * 4, true); spokes(3, .16, .72, .1); ring(.08, .22, 40, false); }
  const s = { xz, tri };
  shapes.set(id, s);
  return s;
}
// The shape turned by turn, then squashed on z (the wheel standing in the sky seen from the tilted view) or,
// for a shadow, stretched by `stretch` along the direction `along` (radians).
function gearMeshAt(key, type, teeth, turn, squash, along = 0, stretch = 1) {
  const s = gearShape(type, teeth), out = new Array(s.xz.length), ct = Math.cos(turn), st = Math.sin(turn), ca = Math.cos(along), sa = Math.sin(along);
  for (let k = 0; k < s.xz.length; k += 2) {
    let x = s.xz[k] * ct - s.xz[k + 1] * st, z = s.xz[k] * st + s.xz[k + 1] * ct;
    if (stretch !== 1) { const a = (x * ca + z * sa) * stretch, b = -x * sa + z * ca; x = a * ca - b * sa; z = a * sa + b * ca; }
    out[k] = x; out[k + 1] = z * squash;
  }
  const m = mesh(key);
  m.setFlat(out, s.tri);
  return m;
}

// ---- drawing -------------------------------------------------------------------------------------------
// The sky, the plain, the ridges and the far swords for the world round c (the caster) whose map ends
// `north` cells north of it, at time s under the dusk sun, seen by `view` (ctx.view: the camera).
//   o.ridges: scale of the ridges' height; o.parallax: false fixes everything of the sky to the map.
export function drawHorizon(T, c, north, ht, s, sun, view, tint, o = {}) {
  const sq = squeeze(ht), edgeZ = c.z + north, horizonZ = edgeZ + ht, camX = view.cx, width = 2 * view.halfW + 10;
  const skyX = (a, p) => c.x + a * KA + (o.parallax === false ? 0 : (1 - p) * (camX - c.x));
  const skyZ = e => horizonZ + e * KE;

  // The sky: the plane above it, the gradient, the sun's glow, the clouds (bodies, then their lit undersides
  // in three groups by how near the sun they are), the sun, the smog.
  draw(MeshPool.plane10, camX, L(0), horizonZ + SkyCells + 200, width + 400, 400, 0, Top, solid);
  draw(MeshPool.plane10, camX, L(1), horizonZ + (SkyCells - 1) / 2, width, SkyCells + 1, 0, new Color(1, 1, 1, 1), skyMat);
  const sunA = clamp(Math.atan2(-sun.x, -sun.z) / D2R, -70, 70), sunPos = { x: skyX(sunA, 0), z: skyZ(SunUp) };
  draw(MeshPool.plane10, sunPos.x, L(2), sunPos.z, 30 * KE * 2, 30 * KE * 2, 0, SunWarm.withAlpha(.38), glow);
  const bodies = [[], [], []], lit = [[], [], []];
  for (let i = 0; i < 20; i++) {
    const a = (hash(i, 1, 51) - .5) * 180 + s * (.35 + .7 * hash(i, 2, 51)), e = 9 + Math.pow(hash(i, 3, 51), .8) * 58;
    const near = clamp(1 - Math.abs(a - sunA) / 80) * clamp(1 - (e - 8) / 50), g = Math.min(2, Math.floor(near * 3));
    const n = 8 + Math.floor(hash(i, 4, 51) * 6);
    for (let k = 0; k < n; k++) {
      const da = (hash(i * 16 + k, 5, 51) - .5) * 18, de = (hash(i * 16 + k, 6, 51) - .35) * 5, r = 3.5 + hash(i * 16 + k, 7, 51) * 5.5;
      const x = skyX(a + da, .04), z = skyZ(e + de), w = r * KE * 2.6, h = r * KE * 2;
      bodies[g].push({ x, z, w, h });
      lit[g].push({ x, z: z - .9 * KE - h * .12, w: w * .8, h: h * .75 });
    }
  }
  bodies.forEach((list, g) => quads(`ubw horizon clouds ${g}`, list, mixC(CloudDark, CloudWarm, g / 2).withAlpha(.5), soft, L(3) + g * .00001));
  lit.forEach((list, g) => quads(`ubw horizon cloud light ${g}`, list, CloudLit.withAlpha(.12 + .19 * g), glow, L(4) + g * .00001));
  draw(MeshPool.plane10, sunPos.x, L(5), sunPos.z, 8 * KE * 2, 8 * KE * 2, 0, SunWarm.withAlpha(.6), glow);
  draw(MeshPool.plane10, sunPos.x, L(6), sunPos.z, 1.6 * KE * 2, 1.6 * KE * 2, 0, SunFace.withAlpha(.97), soft);
  const smog = [];
  for (let i = 0; i < 8; i++) {
    const a = (hash(i, 1, 53) - .5) * 170 + s * (.8 + 1.4 * hash(i, 2, 53)), e = 3 + i * 2.4 + hash(i, 3, 53) * 2;
    smog.push({ x: skyX(a, .06), z: skyZ(e), w: (28 + 34 * hash(i, 4, 53)) * KE * 2, h: (.8 + 1.2 * hash(i, 5, 53)) * KE * 2 });
  }
  quads('ubw horizon smog', smog, Smog.withAlpha(.34), soft, L(7));

  // The gears, the hazier (further) first: a warm rim toward the sun, then the body.
  const toSunX = Math.sign(sunA) || 1;
  SkyGears.slice().sort((a, b) => b.haze - a.haze).forEach((g, i) => {
    const R = g.r * KE, x = skyX(g.a, g.p), z = skyZ(g.e), m = gearMeshAt(`ubw horizon gear ${i}`, g.type, g.teeth, (s * g.spin + g.a * 5) * D2R, .8);
    draw(m, x + toSunX * R * .02, L(8 + i * 2), z + R * .012, R, R, 0, RimLight.withAlpha(.5 * (1 - g.haze)), solid);
    draw(m, x, L(9 + i * 2), z, R, R, 0, mixC(Silhouette, skyAt(g.e + g.r * .3), g.haze).withAlpha(.97), solid);
  });

  // The plain to the horizon, then everything on it, far to near.
  draw(MeshPool.plane10, camX, L(22), edgeZ + ht / 2, width, ht, 0, tint, groundMat);
  const f = farBake(T, c, north, sq, sun, camX, view.halfW, o.ridges ?? 1), hazed = d => mixC(tint, HazeFar, hazeOf(d) * .85);
  const ridge = (r, k) => {
    const R = Ridges[r], hz = hazeOf(R.d) * .55;
    draw(f.ridges[r].fill, 0, L(k), 0, 1, 1, 0, mixC(RidgeEarth, HazeFar, hz), solid);
    draw(f.ridges[r].rim, 0, L(k) + .00001, 0, 1, 1, 0, mixC(mixC(RidgeEarth, HazeFar, hz), RimLight, .45).withAlpha(.4), solid);
    if (R.swords) draw(f.ridges[r].swords, 0, L(k) + .00002, 0, 1, 1, 0, mixC(mixC(Steel, Grip, .5), HazeFar, hz * .9), solid);
  };
  const swords = (g, k) => {
    const [a, b] = Groups[g], d = (a + b) / 2;
    if (f.blades[g]) draw(f.blades[g], 0, L(k), 0, 1, 1, 0, hazed(d), swordAtlas);
    if (f.steel[g]) draw(f.steel[g], 0, L(k) + .00001, 0, 1, 1, 0, mixC(Steel, HazeFar, hazeOf(d) * .85), solid);
    if (f.grips[g]) draw(f.grips[g], 0, L(k) + .00002, 0, 1, 1, 0, mixC(Grip, HazeFar, hazeOf(d) * .85), solid);
  };
  ridge(0, 23); swords(0, 24); swords(1, 25);
  ridge(1, 26); swords(2, 27);
  ridge(2, 28); draw(f.plates[0], 0, L(29), 0, 1, 1, 0, hazed(10), terrainAtlas); swords(3, 30); swords(4, 31);
  ridge(3, 32); draw(f.plates[1], 0, L(33), 0, 1, 1, 0, hazed(3), terrainAtlas); swords(5, 34); swords(6, 35);
  return f;
}

// The gears' shadows sweeping the map: spoked, stretched along the low sun, faint (opacity). They stay south
// of the map edge (north cells north of the caster): past it the ground is squeezed and they would not be.
export function gearShadows(c, s, sun, opacity, north) {
  if (opacity <= 0) return;
  const along = Math.atan2(sun.z, sun.x);
  for (let i = 0; i < 3; i++) {
    const r = 5 + i * 1.2, x = c.x + Math.sin(s * .1 + i * 2.4) * 10 - 4 + i * 12, z = c.z + Math.min(-8 + i * 7, north - r * 1.4);
    const m = gearMeshAt(`ubw horizon gear shadow ${i}`, 0, 14, s * .25 + i, 1, along, 1.3);
    draw(m, x, shadowLayer + .001 + i * .0002, z, r, r, 0, Black.withAlpha(opacity), solid);
  }
}

// The camera's haze: from nothing at the bottom of the screen to maxHaze at the top, up to the horizon, over
// the ground and the swords and under the pawns.
export function depthHaze(c, north, ht, view, maxHaze) {
  if (maxHaze <= 0) return;
  const bottom = view.cz - view.halfH, top = Math.min(view.cz + view.halfH, c.z + north + ht), h = top - bottom;
  if (h <= 0) return;
  draw(MeshPool.plane10, view.cx, buildingLayer + .599, (top + bottom) / 2, 2 * view.halfW + 4, h, 0, Fog.withAlpha(maxHaze * Math.pow(h / (2 * view.halfH), 1.7)), depthMat);
}

// Embers and ash in front of everything: fixed to the screen, moved 1.45 times the camera's pan (so they read
// as nearer the camera than the ground), drifting up.
export function foreground(c, s, view, alpha) {
  if (alpha <= 0) return;
  const W = 2 * view.halfW + 8, H = 2 * view.halfH + 8, wrap = (v, n) => ((v % n) + n) % n - n / 2, embers = [], ash = [];
  for (let i = 0; i < 30; i++) {
    const x = view.cx + wrap(hash(i, 1, 61) * W - 1.45 * (view.cx - c.x) + s * (hash(i, 2, 61) - .3) * 1.2, W);
    const z = view.cz + wrap(hash(i, 3, 61) * H - 1.45 * (view.cz - c.z) + s * (.5 + hash(i, 4, 61) * 1.1), H);
    const r = (.22 + .4 * hash(i, 5, 61)) * (1 + .15 * Math.sin(s * 2 + i));
    (hash(i, 6, 61) < .45 ? ash : embers).push({ x, z, w: r * 2, h: r * 2 });
  }
  quads('ubw horizon ash', ash, Ash.withAlpha(.3 * alpha), soft, Y + .08);
  quads('ubw horizon embers', embers, Spark.withAlpha(.34 * alpha), glow, Y + .081);
}
