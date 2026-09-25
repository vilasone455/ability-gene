// Trace kit: the sky of Unlimited Blade Works (2026-09-25), for "Unlimited Blade Works: world v2, depth
// (pocket)". Not a sketch itself, so it is not listed in sketches/index.js. An experiment, nothing agreed.
//
// The camera looks straight down, so a sky can only be shown the way the kit shows height: what is far up
// is drawn far north (0.60 cells per cell up). The ground with its tiers (lib/ubw-terrain.js) ends at a
// ridge line past the north edge of the map, and above that line on screen is the sky, as the top of an
// oblique view: the twilight gradient (the 2010 movie and the 2015 anime: a red-orange sky over the field,
// dark smog), the low sun on its side of the sky, the gears hanging in it (Type-Moon wiki: black gears
// turning in the distance). Each gear's shadow on the ground is cast from its body along the sun, so the
// shadows that sweep the map come from the gears over the north-east, where the sun is. Only the north has
// a sky: south is toward the camera, east and west are sideways.
//
// Drawing: the sky is a strip from the ridge line up (the highest lifted plate top at each x, so the far
// tiers stand against it), textured with a gradient (lab/ubw-sky), a plane in the top colour beyond it, a
// warm band along the ridge, the sun (a disc and two glows), the gears (spun meshes squashed to about 0.65,
// a standing wheel seen from above and in front), their shadows on the shadow layer, a few smog streaks.
// Level shapes only, no facing. The sky is drawn under the ground (between the crack floor and the plates),
// so the far ridge's plates stand in front of it and three of the gears hang partly behind the ridge. The
// haze that lib/ubw-pocket.js draws as rings would then tint the sky, so the v2 world uses hazeToRidge():
// the same rings, each ray cut off where it meets the ridge.
import { Color, MaterialPool, Mesh, Meshes, MeshPool, ShaderDatabase } from '../../js/engine.js';
import { registerLabTexture, pixels } from '../../js/standins.js';
import { draw, mesh } from './six-paths-solid.js';
import { Lift, sprite, soft, glow, rand } from './six-paths-impact.js';
import { shadowLayer, buildingLayer } from './goku.js';
import { solid, Black, D2R, clamp, lerp } from './trace.js';
import { gearMesh, Sunset } from './ubw.js';
import { WhiteHot, Haze, MapHalf } from './ubw-pocket.js';
import { BaseLayer } from './ubw-terrain.js';

export const SkyLayer = BaseLayer + .001;         // over the crack floor, under the plates (TerrainLayer is + .003 up)
const L = k => SkyLayer + k * .0001;              // the sky's own order, all under the plates
const SkyH = 40, RidgeGlow = 7, SunUp = 5, SunOut = 34, Step = .5;
const Horizon = new Color(1, .62, .3), Mid = new Color(.86, .36, .2), High = new Color(.5, .17, .16), Top = new Color(.28, .1, .13);
const SunFace = new Color(1, .75, .45), Silhouette = new Color(.12, .05, .05), Smog = new Color(.3, .1, .1);
const mix = (a, b, t) => new Color(lerp(a.r, b.r, t), lerp(a.g, b.g, t), lerp(a.b, b.b, t));

registerLabTexture('lab/ubw-sky', () => pixels(64, (u, v) => {
  const h = 1 - v;                                  // 0 at the bottom row (the horizon), 1 at the top
  const c = h < .3 ? mix(Horizon, Mid, h / .3) : h < .65 ? mix(Mid, High, (h - .3) / .35) : mix(High, Top, (h - .65) / .35);
  return [c.r, c.g, c.b, 1];
}));
const skyMat = MaterialPool.MatFrom('lab/ubw-sky', ShaderDatabase.Transparent);
const fadeMat = MaterialPool.MatFrom('lab/ubw-fade', ShaderDatabase.Transparent);   // lib/ubw-pocket.js's gradient
const sunDisc = Meshes.disc(48, 'ubw sky sun');

// The gears in the sky: x cells from the caster, up cells above the world's edge (negative: the lower part
// hangs behind the ridge), r radius, H cells above the ground (which sets where the shadow falls: body
// minus Lift x H north, plus sun x H), spin degrees a second, squash of the standing wheel, haze 0..1 for
// the far ones. The ones over the north-east (x 17 to 52) cast onto the map.
export const Gears = [
  { x: -36, up: 5, r: 6, H: 30, spin: 3.5, teeth: 12, squash: .62, haze: .3 },
  { x: -15, up: -3, r: 9.5, H: 36, spin: -2.2, teeth: 14, squash: .7, haze: .05 },
  { x: 3, up: 3.5, r: 5, H: 30, spin: 5, teeth: 10, squash: .6, haze: .35 },
  { x: 17, up: -2, r: 7.5, H: 32, spin: -3.5, teeth: 12, squash: .66, haze: .1 },
  { x: 33, up: 2.5, r: 5.5, H: 28, spin: 6, teeth: 10, squash: .58, haze: .25 },
  { x: 48, up: -2.5, r: 8.5, H: 44, spin: 2.5, teeth: 14, squash: .72, haze: .08 },
  { x: 30, up: 13, r: 4.5, H: 46, spin: -6, teeth: 10, squash: .6, haze: .45 },
  { x: 52, up: 9.5, r: 6, H: 50, spin: -3, teeth: 12, squash: .64, haze: .4 },
  { x: -50, up: 11, r: 4, H: 40, spin: 4, teeth: 12, squash: .64, haze: .5 },
];

// ---- the ridge ---------------------------------------------------------------------------------------
// The highest lifted plate top at each x across the drawn width, for the plates along the north edge of
// the drawn ground; the world's edge where there is none. Kept per terrain.
const ridges = new Map();
export function ridgeOf(T) {
  if (ridges.has(T.key)) return ridges.get(T.key);
  const x0 = -T.edge - 10, n = Math.round((2 * T.edge + 20) / Step) + 1, zs = new Float32Array(n).fill(T.edge);
  for (const p of T.plates) {
    if (!p || p.maxZ < T.edge - 8 || p.maxZ + p.h * Lift <= T.edge) continue;
    const lift = p.h * Lift, m = p.poly.length;
    const i0 = Math.max(0, Math.ceil((p.minX - x0) / Step)), i1 = Math.min(n - 1, Math.floor((p.maxX - x0) / Step));
    for (let i = i0; i <= i1; i++) {
      const x = x0 + i * Step;
      let top = -Infinity;
      for (let k = 0; k < m; k++) {
        const a = p.poly[k], b = p.poly[(k + 1) % m];
        if ((a.x < x) === (b.x < x)) continue;
        const z = a.z + (b.z - a.z) * (x - a.x) / (b.x - a.x);
        if (z > top) top = z;
      }
      if (top > -Infinity && top + lift > zs[i]) zs[i] = top + lift;
    }
  }
  const ridge = { x0, n, zs, at: x => { const i = Math.max(0, Math.min(n - 1, Math.round((x - x0) / Step))); return zs[i]; } };
  ridges.set(T.key, ridge);
  if (ridges.size > 3) ridges.delete(ridges.keys().next().value);
  return ridge;
}

function pushQuad(out, pts, uvs) {
  let area = 0;
  for (let i = 0; i < 4; i++) { const p = pts[i], q = pts[(i + 1) % 4]; area += p.x * q.z - q.x * p.z; }
  if (Math.abs(area) < 1e-6) return;
  const base = out.xz.length / 2;
  for (let i = 0; i < 4; i++) { const k = area > 0 ? 3 - i : i; out.xz.push(pts[k].x, pts[k].z); out.uv.push(uvs[k][0], uvs[k][1]); }
  out.tri.push(base, base + 1, base + 2, base, base + 2, base + 3);
}
// Two strips along the ridge: the sky (gradient from the ridge to SkyH above the world's edge) and the
// warm band just above the ridge (fade: alpha 1 at the ridge, 0 RidgeGlow up). Kept per terrain.
const strips = new Map();
function stripsOf(T) {
  if (strips.has(T.key)) return strips.get(T.key);
  const r = ridgeOf(T), sky = { xz: [], uv: [], tri: [] }, band = { xz: [], uv: [], tri: [] }, top = T.edge + SkyH;
  for (let i = 1; i < r.n; i++) {
    const xa = r.x0 + (i - 1) * Step, xb = r.x0 + i * Step, za = r.zs[i - 1], zb = r.zs[i];
    const va = clamp((za - T.edge) / SkyH), vb = clamp((zb - T.edge) / SkyH);
    pushQuad(sky, [{ x: xa, z: za }, { x: xb, z: zb }, { x: xb, z: top }, { x: xa, z: top }], [[.5, va], [.5, vb], [.5, 1], [.5, 1]]);
    pushQuad(band, [{ x: xa, z: za }, { x: xb, z: zb }, { x: xb, z: zb + RidgeGlow }, { x: xa, z: za + RidgeGlow }], [[.98, .5], [.98, .5], [.02, .5], [.02, .5]]);
  }
  const bake = (name, b) => { const m = new Mesh(name); m.setFlat(b.xz, b.tri); m.uv = new Float32Array(b.uv); return m; };
  const out = { sky: bake(`ubw sky ${T.key}`, sky), band: bake(`ubw sky band ${T.key}`, band) };
  strips.set(T.key, out);
  if (strips.size > 3) strips.delete(strips.keys().next().value);
  return out;
}

// ---- gears ---------------------------------------------------------------------------------------------
// A gear turned by `turn` and squashed on z (its body), or turned and stretched along the sun (its shadow,
// as lib/ubw-pocket.js skyGears draws one).
function bodyMesh(key, teeth, turn, squash) {
  const base = gearMesh(teeth), v = base.v, out = new Array(v.length), ct = Math.cos(turn), st = Math.sin(turn);
  for (let k = 0; k < v.length; k += 2) { out[k] = v[k] * ct - v[k + 1] * st; out[k + 1] = (v[k] * st + v[k + 1] * ct) * squash; }
  const m = mesh(key); m.setFlat(out, base.tri); return m;
}
function shadowMesh(key, teeth, turn, along, stretch) {
  const base = gearMesh(teeth), v = base.v, out = new Array(v.length);
  const ct = Math.cos(turn), st = Math.sin(turn), ca = Math.cos(along), sa = Math.sin(along);
  for (let k = 0; k < v.length; k += 2) {
    const x = v[k] * ct - v[k + 1] * st, z = v[k] * st + v[k + 1] * ct, a = (x * ca + z * sa) * stretch, b = -x * sa + z * ca;
    out[k] = a * ca - b * sa; out[k + 1] = a * sa + b * ca;
  }
  const m = mesh(key); m.setFlat(out, base.tri); return m;
}
const toSun = sun => { const d = Math.hypot(sun.x, sun.z) || 1; return { x: -sun.x / d, z: -sun.z / d }; };

// ---- haze cut at the ridge --------------------------------------------------------------------------------
// lib/ubw-pocket.js haze() as eight rings, each ray ending where it meets the ridge, so the ground north of
// the map is hazed as before and the sky above the ridge is not. Kept per terrain.
const HazeLayer = buildingLayer + .6, HazeFar = 240, HazeRays = 360;
const hazes = new Map();
function hazeMeshes(T) {
  if (hazes.has(T.key)) return hazes.get(T.key);
  const ridge = ridgeOf(T), out = [];
  for (let k = 0; k < 8; k++) {
    const r0 = MapHalf + 2 + k * 2.4, xz = [], tri = [];
    for (let j = 0; j <= HazeRays; j++) {
      const a = j / HazeRays * Math.PI * 2, ca = Math.cos(a), sa = Math.sin(a);
      let t = 1;
      if (sa * HazeFar > T.edge) { t = T.edge / (sa * HazeFar); t = Math.max(t, ridge.at(ca * HazeFar * t) / (sa * HazeFar)); }
      xz.push(ca * r0, sa * r0, ca * HazeFar * t, sa * HazeFar * t);
      if (j) { const b = j * 2; tri.push(b - 2, b, b - 1, b - 1, b, b + 1); }
    }
    const m = new Mesh(`ubw sky haze ${T.key} ${k}`); m.setFlat(xz, tri); out.push(m);
  }
  hazes.set(T.key, out);
  if (hazes.size > 3) hazes.delete(hazes.keys().next().value);
  return out;
}
export function hazeToRidge(T, c, alpha) {
  if (alpha <= 0) return;
  hazeMeshes(T).forEach((m, k) => draw(m, c.x, HazeLayer + k * .0001, c.z, 1, 1, 0, Haze.withAlpha(.1 * alpha), solid));
}

// ---- drawing -------------------------------------------------------------------------------------------
// The whole sky round c for terrain T at s: strip, top, ridge band, sun, gears with their shadows (opacity
// as the world's gear shadow slider), smog. sun is the world's shadow vector per cell of height.
export function sky(key, T, c, s, sun, gearOpacity) {
  const st = stripsOf(T), edge = T.edge, to = toSun(sun);
  draw(MeshPool.plane10, c.x, L(0), c.z + edge + SkyH + 200, 800, 400, 0, Top, solid);
  draw(st.sky, c.x, L(1), c.z, 1, 1, 0, new Color(1, 1, 1, 1), skyMat);
  draw(st.band, c.x, L(2), c.z, 1, 1, 0, Sunset.withAlpha(.45), fadeMat);
  // The sun, low over the world's edge on its side of the sky.
  const sx = c.x + to.x * SunOut, sz = c.z + edge + SunUp;
  sprite({ x: sx, z: sz }, 70, 70, Sunset.withAlpha(.2), glow, L(3));
  sprite({ x: sx, z: sz }, 24, 24, SunFace.withAlpha(.55), glow, L(4));
  draw(sunDisc, sx, L(5), sz, 2.4, 2.4, 0, WhiteHot.withAlpha(.95), solid);
  // Smog: long thin streaks drifting east, darker with height.
  const smog = [];
  for (let k = 0; k < 7; k++) {
    const x = ((rand(k * 7 + 1) - .5) * 120 + s * (.2 + .2 * rand(k * 3)) + 60) % 120 - 60, up = 6 + k * 3.2 + rand(k * 5) * 2;
    smog.push({ x: c.x + x, z: c.z + edge + up, w: 18 + rand(k * 11) * 18, h: 1.2 + rand(k * 13) * 1.6, angle: (rand(k * 17) - .5) * 6 });
  }
  smog.forEach((q, k) => sprite({ x: q.x, z: q.z }, q.w, q.h, Smog.withAlpha(.22 + .04 * k), soft, L(20 + k), q.angle));
  // The gears: bodies in the sky (the far ones hazed toward it), shadows on the ground.
  const along = Math.atan2(sun.z, sun.x);
  Gears.forEach((g, i) => {
    const turn = s * g.spin * D2R + i * .7, dx = Math.sin(s * .21 + i * 2) * .6, dz = Math.cos(s * .17 + i) * .4;
    const bx = c.x + g.x + dx, bz = c.z + edge + g.up + dz;
    const body = bodyMesh(`${key} body ${i}`, g.teeth, turn, g.squash);
    draw(body, bx, L(8 + i), bz, g.r, g.r, 0, mix(Silhouette, Mid, g.haze * .6).withAlpha(.95));
    if (gearOpacity <= 0) return;
    const shadow = shadowMesh(`${key} shadow ${i}`, g.teeth, turn, along, 1.25);
    const gx = bx + sun.x * g.H, gz = bz - Lift * g.H + sun.z * g.H;
    draw(shadow, gx, shadowLayer + .001 + i * .0002, gz, g.r * 1.05, g.r * 1.05, 0, Black.withAlpha(gearOpacity * .35));
    draw(shadow, gx, shadowLayer + .0011 + i * .0002, gz, g.r, g.r, 0, Black.withAlpha(gearOpacity));
  });
}
