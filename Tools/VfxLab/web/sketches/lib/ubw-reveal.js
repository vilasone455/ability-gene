// Trace kit: Unlimited Blade Works in 3D (2026-09-26), for the reveal and close shots ("Unlimited Blade Works:
// reveal shot (pocket)", "... close shot (pocket)"). Not a sketch itself, so it is not listed in
// sketches/index.js. An experiment, nothing agreed.
//
// Parts 1 and 3 of the sky plan: a perspective camera (the lab's camera(), SKETCHING.md) looks at a 3D copy of
// the world of v4 ("world v4, sword crest") and blends into the game view. The copy is built from v4's own
// bakes, so that at blend 1 it is v4's picture: every vertex has its 3D place (x east, y up, z north, in cells)
// and its game position, where v4 draws it (the height rule on the map, the side-view picture behind the crest,
// degrees in the sky):
//   - the map's plates: v4's bake with 3D places (lib/ubw-terrain.js bakeTerrain, three), their faces down to
//     the crack floor 1 cell under the level plates;
//   - the crest: a bank standing on the ground past the edge, with a flat top and a back slope that only the
//     3D camera sees, its swords standing on its top (lib/ubw-crest.js crestOf, ring);
//   - behind it, v4's side-view picture stood up (lib/ubw-crest.js backdrop3): an eye BackEye (9) cells over
//     the caster sees each part where v4 draws it, so the near ridge is a wall 81 cells north, the middle one
//     172, the mountains 516 (up to 97 cells tall), and the 14 rows of swords stand on the plain from 27 cells
//     north to 480; the smoke over the crest and the embers rising from behind it stand just behind its top;
//   - the plain: a grid from 70 cells south of the edge to 4000 north of it, its game position v4's band of
//     plain between the crest's cover line and the horizon;
//   - the sky: a dome 5000 cells out with v3's gradient; the sun, clouds and smog as sprites facing the
//     caster's eye; the seven gears as wheels 3000 cells out facing the eye, their lower parts behind the
//     ground. In 3D a thing of the sky is round, r degrees across; in the game view it keeps v4's shape;
//   - the swords of the map, the crest's and the backdrop's in rings RingStep cells wide out from the caster, so
//     that the fire running out reveals them: a ring rises out of the ground over Rise cells once the fire is
//     past it; the ones past the fire's reach rise as it gets there;
//   - the pawns: v4's stand-in discs standing up, facing the camera, 1 / 0.6 as tall as drawn (the height
//     rule backwards), on their plates; their game positions are v4's discs;
//   - the camera's haze and the embers in front: v4's, drawn where the game view has them (Graphics.OnScreen),
//     faded in with the blend.
// Drawn far to near with the lab's depth test; marks on the ground go through Graphics.Flat or a material that
// skips the test. The 3D camera (yaw 0) always looks north, so everything that stands faces south.
import { Color, Graphics, Material, MaterialPropertyBlock, Matrix4x4, Mesh, Quaternion, ShaderDatabase, ShaderPropertyIDs, Texture2D, Vector3 } from '../../js/engine.js';
import { hash } from '../../js/standins.js';
import { mesh } from './six-paths-solid.js';
import { Lift, rand } from './six-paths-impact.js';
import { D2R, clamp, smooth, cutOf } from './trace.js';
import { Skin, Hair, Ink, pawn } from './goku.js';
import { SceneNorth, FireOuter, FireCore, Ember } from './ubw.js';
import { fieldList, standingPose, bladeInto, marksInto, lipInto, AtlasNames, atlas as swordAtlas, Look, White, Tint, Twilight, emberLists, EmberAlpha, sunGlow, hill, flick, fadeRing } from './ubw-pocket.js';
import { Ground, terrain, terrainAtlas, Base, bakeTerrain } from './ubw-terrain.js';
import { SkyGears, KA, KE, SkyCells, SunUp, gearShape, cloudPuffs, smogBands, sunAzimuth, SkyColours as C, mixC, hazeOf, skyAt, gearShadows, depthHaze, foreground } from './ubw-horizon.js';
import {
  CrestFoot, CrestWobble, CamRef, HorizonMove, CoverAt, crestOf, CrestColours, drawCrestShadows, backdrop3, backOffset,
  smokeBands, SmokeColour, SmokeLight, updraft, UpdraftColour,
} from './ubw-crest.js';
import v4, { Caster, Landed, Keep, OuterPlate, Cluster, GearShadows } from '../trace-ubw-world-v4.js';

// The caster's eye height; where the dome, the sky's sprites, the gears and the plain end (cells); the rings
// the swords rise in; the ridges' height in 3D (x what the backdrop's eye gives them).
export const Eye = 1.7;
const DomeR = 5000, SpriteR = 4600, GearR = 3000, PlainFar = 4000, PlainSouth = -70;
export const RingStep = 1.5, Rise = 3, RidgeHeight = 1;
// The crack floor runs FloorPast cells past the edge, under the crest's foot; the smoke bands (far to near)
// stand SmokeBehind cells behind the crest's mean top, the rising embers EmberBehind behind the top above them.
const FloorPast = 2, SmokeBehind = [1.2, .7, .3], EmberBehind = .3;
// v4's settings, from its sliders' defaults: the reveal and close shots draw the world v4 draws by default.
export const V = Object.fromEntries(Object.entries(v4.params).map(([k, q]) => [k, q.value]));

// ---- the camera ------------------------------------------------------------------------------------------
// Keyframes [t, x, y, z, pitch, fov] from the caster (the plan's, 2026-09-26), eased in and out between each
// pair, as the plan's widget moves.
export const RevealKeys = [[0, 0, 1.7, -3.6, 30, 64], [1.25, 0, 1.9, -4.4, 25, 62], [2.5, 0, 4.2, -9.5, -6, 56], [3.3, 0, 13, -14, -19, 50], [4.2, 0, 26, -18, -38, 44]];
export const CloseKeys = [[0, 0, 26, -18, -38, 44], [.8, 0, 13, -14, -19, 50], [1.7, 0, 2.4, -5, 20, 62], [2.6, 0, 1.9, -4.4, 30, 64]];
const ease = t => { t = clamp(t); return t < .5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2; };
export function along(keys, t) {
  let i = 0;
  while (i < keys.length - 2 && t > keys[i + 1][0]) i++;
  const a = keys[i], b = keys[i + 1], u = ease((t - a[0]) / (b[0] - a[0])), l = k => a[k] + (b[k] - a[k]) * u;
  return { x: l(1), y: l(2), z: l(3), pitch: l(4), fov: l(5) };
}
// The caster of the lab's cell, as v4 places it.
export const casterOf = origin => ({ x: origin.x, z: origin.z + SceneNorth });

// ---- the world -------------------------------------------------------------------------------------------
// Everything v4 works out before it draws, for the lab's cell, scene and game view.
export function world(origin, scene, view) {
  const c = casterOf(origin), north = Math.round(V.north);
  const v = view ?? { cx: c.x, cz: c.z + 7, ppc: 12, halfW: 32, halfH: 18 };
  const base = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32, len = Math.hypot(base.x, base.z) || 1;
  const sun = { x: base.x / len * V.shadow, z: base.z / len * V.shadow };
  const T = terrain({ ...Ground, outer: OuterPlate, tierStep: 0, southTiers: 0, hill: Look.hill, beyond: Look.beyond, northClip: north + CrestFoot + CrestWobble });
  const edgeZ = c.z + north, dz = v.cz - (c.z + CamRef), share = p => Math.min(.95, p * V.parallax);
  return {
    c, north, view: v, sun, strength, T, edgeZ, dz, share, camX: v.cx,
    // v4's horizon and the line its cover ends at, on the game screen
    horizonZ: edgeZ + V.horizon + (1 - share(HorizonMove)) * dz, cover: edgeZ + CoverAt,
    k: crestOf(c, north, V.crest, V.crestSwords, sun),
    tint: Color.Lerp(White, Tint, Look.twilight), sunA: sunAzimuth(sun), eye: { x: c.x, y: Eye, z: c.z },
    settings: { density: V.density, hill: Look.hill, beyond: Look.beyond, size: Look.size, lean: Look.lean, north, cluster: Cluster },
  };
}
export const ringOf = dist => Math.floor(dist / RingStep);
// How far ring k has risen when the fire has run out to r (Infinity: the fire is done); a ring past the fire's
// reach rises with the last ring it reaches.
const riseOf = (k, r, reach = Infinity) => r === Infinity ? 1 : smooth((r - (Math.min(k, ringOf(reach - Rise) - 1) + 1) * RingStep) / Rise);

// ---- materials and drawing -------------------------------------------------------------------------------
// Own instances, so the depth settings do not reach v4's pooled materials.
function mat(path, shader, floats = {}) {
  const m = new Material(shader, { mainTexture: new Texture2D(path) });
  for (const [k, v] of Object.entries(floats)) m.SetFloat(k, v);
  return m;
}
const NoWrite = { _ZWrite: 0 }, Over = { _ZWrite: 0, _ZTest: 8 };
const skyMat = mat('lab/ubw-horizon-sky', ShaderDatabase.Transparent, NoWrite);
const plainMat = mat('lab/ubw-horizon-ground', ShaderDatabase.Transparent, NoWrite);
const solid = mat('white', ShaderDatabase.Transparent);
const see = mat('white', ShaderDatabase.Transparent, NoWrite);
const softSky = mat('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent, NoWrite);
const glowSky = mat('RimArt/SixPaths/SoftDisc', ShaderDatabase.MoteGlow);
const onGround = mat('white', ShaderDatabase.Transparent, Over);
const softGround = mat('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent, Over);
const atlasGround = mat('RimArt/TraceTrial/Atlas', ShaderDatabase.Transparent, Over);
const faceMat = mat('lab/ubw-crest-face', ShaderDatabase.Transparent);
const flameMat = mat('lab/ubw-flame', ShaderDatabase.MoteGlow);
const burnMat = mat('white', ShaderDatabase.MoteGlow, { _ZTest: 8 });

const props = new MaterialPropertyBlock();
// One 3D draw: at x, y (height), z, turned rx, ry, rz degrees (Unity's order), scaled sx, sy, sz.
export function d3(m, x, y, z, colour, material, { rx = 0, ry = 0, rz = 0, sx = 1, sy = 1, sz = 1 } = {}) {
  if (colour.a <= .002) return;
  props.SetColor(ShaderPropertyIDs.Color, colour);
  Graphics.DrawMesh(m, Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(rx, ry, rz), new Vector3(sx, sy, sz)), material, 0, null, 0, props);
}
const buf2 = () => ({ xz: [], uv: [], tri: [] }), buf3 = () => ({ xz: [], uv: [], tri: [], xyz: [] });
function meshOf(m, b) {
  m.setXYZ(b.xyz, b.tri);
  m.setGame(b.xz);
  m.uv = new Float32Array(b.uv);
  return m;
}
const baked = (name, b) => meshOf(new Mesh(name), b);
// A 2D buffer of things lying on the ground (v4's screen points: the height rule already in z) into a 3D
// buffer, lying at height h (lift = the 0.6 h in its z); the screen points stay its game positions.
function appendGround(out, b, h, lift) {
  const base = out.xz.length / 2;
  for (let i = 0; i < b.xz.length; i += 2) {
    out.xz.push(b.xz[i], b.xz[i + 1]);
    out.xyz.push(b.xz[i], h, b.xz[i + 1] - lift);
  }
  for (const u of b.uv) out.uv.push(u);
  for (const t of b.tri) out.tri.push(t + base);
}
// Screen quads (lib/ubw-pocket.js quads' items) standing up on the plane z = zp(q) where the height rule draws
// them: the game position of each corner is the quad itself.
function standing(key, items, zp) {
  const b = buf3();
  items.forEach((q, n) => {
    const Z = zp(q);
    [[-.5, -.5, 0, 0], [-.5, .5, 0, 1], [.5, .5, 1, 1], [.5, -.5, 1, 0]].forEach(([x, z, u, v]) => {
      const gx = q.x + x * q.w, gz = q.z + z * q.h;
      b.xyz.push(gx, (gz - Z) / Lift, Z); b.xz.push(gx, gz); b.uv.push(u, v);
    });
    b.tri.push(n * 4, n * 4 + 1, n * 4 + 2, n * 4, n * 4 + 2, n * 4 + 3);
  });
  return meshOf(mesh(key), b);
}

// ---- the sky ---------------------------------------------------------------------------------------------
const dirOf = (a, e) => { const A = a * D2R, E = e * D2R; return { x: Math.sin(A) * Math.cos(E), y: Math.sin(E), z: Math.cos(A) * Math.cos(E) }; };
// Across and up for something of the sky at a, e that faces the eye.
const faceOf = (a, e) => { const A = a * D2R, E = e * D2R; return { r: { x: Math.cos(A), y: 0, z: -Math.sin(A) }, u: { x: -Math.sin(A) * Math.sin(E), y: Math.cos(E), z: -Math.cos(A) * Math.sin(E) } }; };
function skyPoint(W, a, e, dist, dr = 0, du = 0) {
  const d = dirOf(a, e), f = faceOf(a, e);
  return [W.eye.x + d.x * dist + f.r.x * dr + f.u.x * du, W.eye.y + d.y * dist + f.u.y * du, W.eye.z + d.z * dist + f.r.z * dr + f.u.z * du];
}
// v4's sky on the game screen: a thing with a share p of the camera's pan across, e above the horizon.
const skyX = (W, a, p) => W.c.x + a * KA + (1 - W.share(p)) * (W.camX - W.c.x);
const skyZ = (W, e) => W.horizonZ + e * KE;
// Sprites of the sky, each { a, e, w, h } (degrees across and up in 3D) with its game quad { gx, gz, gw, gh }.
function skySprites(W, key, items, dist) {
  const b = buf3();
  items.forEach((q, n) => {
    const hw = dist * Math.tan(q.w / 2 * D2R), hh = dist * Math.tan(q.h / 2 * D2R);
    [[-.5, -.5, 0, 0], [-.5, .5, 0, 1], [.5, .5, 1, 1], [.5, -.5, 1, 0]].forEach(([x, z, u, v]) => {
      b.xyz.push(...skyPoint(W, q.a, q.e, dist, x * 2 * hw, z * 2 * hh));
      b.xz.push(q.gx + x * q.gw, q.gz + z * q.gh);
      b.uv.push(u, v);
    });
    b.tri.push(n * 4, n * 4 + 1, n * 4 + 2, n * 4, n * 4 + 2, n * 4 + 3);
  });
  return meshOf(mesh(key), b);
}
// The dome: v4's gradient by elevation, 120 degrees either side of north, from 8 below the horizon to the top.
const domes = new Map();
function dome(W) {
  const key = `${W.c.x},${W.c.z}|${W.horizonZ}`;
  if (domes.has(key)) return domes.get(key);
  const b = buf3(), E = [-8, -4, -2, -1, 0, 1, 2, 3, 4, 6, 8, 10, 13, 16, 20, 25, 30, 36, 44, 54, 66, 80, 89], cols = 41;
  E.forEach((e, j) => {
    for (let i = 0; i < cols; i++) {
      const a = -120 + 240 * i / (cols - 1);
      b.xyz.push(...skyPoint(W, a, e, DomeR));
      b.xz.push(W.c.x + a * KA, skyZ(W, e));
      b.uv.push(.5, clamp((e * KE + 1) / (SkyCells + 1), .004, .996));
      if (i && j) { const n = j * cols + i; b.tri.push(n - cols - 1, n - 1, n, n - cols - 1, n, n - cols); }
    }
  });
  const m = baked(`ubw reveal dome ${key}`, b);
  domes.set(key, m);
  if (domes.size > 2) domes.delete(domes.keys().next().value);
  return m;
}
// A gear of the sky turned by turn: its 3D wheel (radius R3, facing the eye) and its game drawing (radius R,
// squashed 0.8 as v4 draws it), moved by (dr, du) in 3D and (gx, gz) on the game screen: the rim light.
function gearInto(W, key, g, turn, dr, du, dgx, dgz) {
  const s = gearShape(g.type, g.teeth), b = buf3(), ct = Math.cos(turn), st = Math.sin(turn);
  const R3 = GearR * Math.tan(g.r * D2R), R = g.r * KE, gx = skyX(W, g.a, g.p) + dgx, gz = skyZ(W, g.e) + dgz;
  for (let k = 0; k < s.xz.length; k += 2) {
    const x = s.xz[k] * ct - s.xz[k + 1] * st, z = s.xz[k] * st + s.xz[k + 1] * ct;
    b.xyz.push(...skyPoint(W, g.a, g.e, GearR, x * R3 + dr, z * R3 + du));
    b.xz.push(gx + x * R, gz + z * R * .8);
    b.uv.push(.5, .5);
  }
  b.tri = s.tri;
  return meshOf(mesh(key), b);
}
// The sky at world time s, in v4's order: dome, the sun's wide glow, cloud bodies, their lit undersides, the
// sun's glow and face, the smog, the gears (hazier first; a warm rim toward the sun, then the body; the bigger
// the slower, 14 / radius degrees a second).
export function drawSky(W, s) {
  d3(dome(W), 0, 0, 0, Color.white, skyMat);
  const sunG = { x: skyX(W, W.sunA, 0), z: skyZ(W, SunUp) }, sunAt = (r, w) => ({ a: W.sunA, e: SunUp, w: 2 * r, h: 2 * r, gx: sunG.x, gz: sunG.z, gw: w, gh: w });
  d3(skySprites(W, 'ubw reveal sun glow', [sunAt(30, 30 * KE * 2)], SpriteR), 0, 0, 0, C.SunWarm.withAlpha(.38), glowSky);
  const bodies = [[], [], []], lit = [[], [], []];
  cloudPuffs(s, W.sunA).forEach(q => {
    const gx = skyX(W, q.a, .04), gz = skyZ(W, q.e), gw = q.r * KE * 2.6, gh = q.r * KE * 2;
    bodies[q.g].push({ a: q.a, e: q.e, w: 2 * q.r, h: 2 * q.r, gx, gz, gw, gh });
    lit[q.g].push({ a: q.a, e: q.e - .9 - .24 * q.r, w: 1.6 * q.r, h: 1.5 * q.r, gx, gz: gz - .9 * KE - gh * .12, gw: gw * .8, gh: gh * .75 });
  });
  bodies.forEach((list, g) => d3(skySprites(W, `ubw reveal clouds ${g}`, list, SpriteR - 10), 0, 0, 0, mixC(C.CloudDark, C.CloudWarm, g / 2).withAlpha(.5), softSky));
  lit.forEach((list, g) => d3(skySprites(W, `ubw reveal cloud light ${g}`, list, SpriteR - 20), 0, 0, 0, C.CloudLit.withAlpha(.12 + .19 * g), glowSky));
  d3(skySprites(W, 'ubw reveal sun', [sunAt(8, 8 * KE * 2)], SpriteR - 30), 0, 0, 0, C.SunWarm.withAlpha(.6), glowSky);
  d3(skySprites(W, 'ubw reveal sun face', [sunAt(1.6, 1.6 * KE * 2)], SpriteR - 31), 0, 0, 0, C.SunFace.withAlpha(.97), softSky);
  const smog = smogBands(s).map(q => ({ a: q.a, e: q.e, w: 2 * q.w, h: 2 * q.h, gx: skyX(W, q.a, .06), gz: skyZ(W, q.e), gw: q.w * KE * 2, gh: q.h * KE * 2 }));
  d3(skySprites(W, 'ubw reveal smog', smog, SpriteR - 40), 0, 0, 0, C.Smog.withAlpha(.34), softSky);
  const toSunX = Math.sign(W.sunA) || 1;
  SkyGears.slice().sort((a, b) => b.haze - a.haze).forEach((g, i) => {
    const turn = (s * Math.sign(g.spin) * V.spin * 14 / g.r + g.a * 5) * D2R, R3 = GearR * Math.tan(g.r * D2R), R = g.r * KE;
    d3(gearInto(W, `ubw reveal gear rim ${i}`, g, turn, toSunX * R3 * .02, R3 * .012, toSunX * R * .02, R * .012), 0, 0, 0, C.RimLight.withAlpha(.5 * (1 - g.haze)), see);
    d3(gearInto(W, `ubw reveal gear ${i}`, g, turn, 0, 0, 0, 0), 0, 0, 0, mixC(C.Silhouette, skyAt(g.e + g.r * .3), g.haze).withAlpha(.97), solid);
  });
}

// ---- the ground --------------------------------------------------------------------------------------------
// The plain from PlainSouth to PlainFar cells past the edge, level at 0, in v3's texture of earth going to
// haze. 3D: the row whose colour is the haze at that distance (hazeOf, 0.85 of it), so the plain hazes over
// hundreds of cells. game: v4's band of plain, from the cover line (0 on the texture) to the horizon (1); a
// point is where the backdrop's eye sees it (lib/ubw-crest.js backOffset), no lower than the cover line, so
// the plain up to 72 cells north of the caster is drawn onto that line. The two are drawn one over the other,
// the game one faded in with the blend. It writes no depth: everything else is drawn over it.
const hazeRow = d => Math.min(.05, d * .1) + .95 * Math.pow(hazeOf(d) * .85, 1 / 2.6);
const plains = new Map();
function plain(W, game) {
  const width = 2 * W.view.halfW + 10, key = `${W.c.x},${W.c.z}|${W.north}|${W.horizonZ}|${W.camX}|${width}|${game}`;
  if (plains.has(key)) return plains.get(key);
  const b = buf3(), rows = [PlainSouth, -40, -20, -10, -5, -2, 0];
  for (let d = .25; d < PlainFar; d *= d < 2 ? 2 : 1.32) rows.push(d);
  rows.push(PlainFar);
  const cols = 49;
  rows.forEach((d, j) => {
    // (the far end on the horizon itself, or the sky shows through under it)
    const X = 80 + 1.5 * (W.north + Math.max(0, d) + 25), z = W.edgeZ + d;
    const gz = d <= CoverAt ? z : d >= PlainFar ? W.horizonZ : Math.max(W.cover, W.horizonZ + backOffset(W.north + d));
    for (let i = 0; i < cols; i++) {
      const x = W.c.x - X + 2 * X * i / (cols - 1);
      b.xyz.push(x, 0, z);
      b.xz.push(x, gz);
      b.uv.push(clamp((x - W.camX) / width + .5, .002, .998), d <= 0 ? .002 : clamp(game ? (gz - W.cover) / (W.horizonZ - W.cover) : hazeRow(d), .002, .998));
      if (i && j) { const n = j * cols + i; b.tri.push(n - cols - 1, n - 1, n, n - cols - 1, n, n - cols); }
    }
  });
  const m = baked(`ubw reveal plain ${key}`, b);
  plains.set(key, m);
  if (plains.size > 4) plains.delete(plains.keys().next().value);
  return m;
}
// The crack floor under the map's plates (v4's terrainBase and cover), T.bottom below the level plates, from
// the world's south edge to FloorPast cells past the map's north edge (under the crest's foot). Its game
// position is v4's: flat, with no height rule, and no further north than the cover line.
const floors = new Map();
function crackFloor(W) {
  const e = W.T.edge, key = `${W.c.x},${W.c.z}|${e}|${W.north}|${W.T.bottom}`;
  if (floors.has(key)) return floors.get(key);
  const b = buf3(), n = 20, m = 12, reach = W.north + FloorPast;
  for (let j = 0; j <= m; j++) for (let i = 0; i <= n; i++) {
    const x = W.c.x - e + 2 * e * i / n, z = W.c.z - e + (e + reach) * j / m;
    b.xyz.push(x, W.T.bottom, z);
    b.xz.push(x, Math.min(z, W.cover));
    b.uv.push(.5, .5);
    if (i && j) { const k = j * (n + 1) + i; b.tri.push(k - n - 2, k - 1, k, k - n - 2, k, k - n - 1); }
  }
  const mm = baked(`ubw reveal floor ${key}`, b);
  floors.set(key, mm);
  if (floors.size > 2) floors.delete(floors.keys().next().value);
  return mm;
}
// The map's plates in 3D (v4's bake with 3D places).
const grounds = new Map();
function ground3(W) {
  const key = `${W.T.key}|${W.sun.x.toFixed(3)},${W.sun.z.toFixed(3)}`;
  if (!grounds.has(key)) { grounds.set(key, bakeTerrain(W.T, W.sun, `reveal ${key}`, true)); if (grounds.size > 2) grounds.delete(grounds.keys().next().value); }
  return grounds.get(key);
}

// ---- the swords of the map -------------------------------------------------------------------------------
// v4's field (lib/ubw-pocket.js field(), with the ground's heights) in rings: per ring the blades with their
// lips (3D), and the marks and shadows lying on the plates. A sword on a plate h high stands at h in 3D; v4
// draws it Lift h further north instead, which is its game position.
const fields = new Map();
function field3(W) {
  const key = JSON.stringify([W.settings, W.c.x, W.c.z, W.sun.x, W.sun.z, W.T.key]);
  if (fields.has(key)) return fields.get(key);
  const rings = new Map(), at = k => rings.get(k) ?? rings.set(k, { blades: buf3(), marks: buf3(), shadows: buf3() }).get(k);
  fieldList(W.settings, Keep, W.T).forEach(sw => {
    const b = standingPose(sw, W.c), lift = sw.lift || 0, h = lift / Lift, R = at(ringOf(Math.hypot(sw.x, sw.z)));
    const row = AtlasNames.indexOf(sw.w.name), cut = cutOf(b, sw.sink), marks = buf2(), shadows = buf2();
    marksInto(marks, cut, sw.seed, sw.far ? 0 : 4);
    appendGround(R.marks, marks, h, lift);
    if (!sw.far) { const lip = buf2(); lipInto(lip, cut, -1, .026, sw.seed, W.sun); appendGround(R.blades, lip, h + .01, lift); }
    const from = R.blades.xyz.length;
    bladeInto(shadows, R.blades, b, W.sun, row);
    for (let i = from; i < R.blades.xyz.length; i += 3) { R.blades.xyz[i + 1] += h; R.blades.xyz[i + 2] -= lift; }
    appendGround(R.shadows, shadows, h, lift);
    if (!sw.far) { const lip = buf2(); lipInto(lip, cut, 1, .032, sw.seed + 3, W.sun); appendGround(R.blades, lip, h + .01, lift); }
  });
  const f = [...rings].sort((a, b) => a[0] - b[0]).map(([k, R]) => ({
    ring: k, blades: baked(`ubw reveal blades ${k}`, R.blades), marks: baked(`ubw reveal marks ${k}`, R.marks), shadows: baked(`ubw reveal sword shadows ${k}`, R.shadows),
  }));
  fields.set(key, f);
  if (fields.size > 2) fields.delete(fields.keys().next().value);
  return f;
}

// ---- the stand-ins -----------------------------------------------------------------------------------------
// lib/goku.js pawn()'s discs [x, z, rx, rz, colour] above the feet, standing up (height = z / Lift), each a
// little nearer the camera than the one before; the shadow blob lying on the plate. Game positions: v4's.
const StandingParts = (colour, hair) => [[0, .18, .22, .32, colour], [0, .58, .16, .17, Skin], ...(hair ? [[0, .69, .19, .1, Hair]] : [])];
const pawnMeshes = new Map();
function pawnMesh(key, pos, h, parts, sun) {
  const id = `${key}|${pos.x},${pos.z},${h}|${sun.x},${sun.z}`;
  if (pawnMeshes.has(id)) return pawnMeshes.get(id);
  const n = 40, discs = parts.map(([cx, cz, rx, rz], k) => {
    const b = buf3();
    b.xyz.push(pos.x + cx, h + cz / Lift, pos.z - .01 * (k + 1)); b.xz.push(pos.x + cx, pos.z + cz); b.uv.push(.5, .5);
    for (let i = 0; i < n; i++) {
      const t = i / n * Math.PI * 2, x = Math.cos(t), z = Math.sin(t);
      b.xyz.push(pos.x + cx + rx * x, h + (cz + rz * z) / Lift, pos.z - .01 * (k + 1));
      b.xz.push(pos.x + cx + rx * x, pos.z + cz + rz * z);
      b.uv.push(.5, .5);
      b.tri.push(0, 1 + i, 1 + (i + 1) % n);
    }
    return baked(`ubw reveal pawn ${key} ${k}`, b);
  });
  const s = buf3(), sx = pos.x + sun.x * .45, sz = pos.z + sun.z * .45;
  [[-.5, -.5, 0, 0], [-.5, .5, 0, 1], [.5, .5, 1, 1], [.5, -.5, 1, 0]].forEach(([x, z, u, v]) => {
    s.xyz.push(sx + x * .85, h, sz + z * .4); s.xz.push(sx + x * .85, sz + z * .4); s.uv.push(u, v);
  });
  s.tri.push(0, 1, 2, 0, 2, 3);
  const out = { discs, shadow: baked(`ubw reveal pawn shadow ${key}`, s) };
  pawnMeshes.set(id, out);
  if (pawnMeshes.size > 12) pawnMeshes.delete(pawnMeshes.keys().next().value);
  return out;
}
export function drawPawns(W) {
  const warm = c => Color.Lerp(c, Twilight, .3 * Look.twilight);
  [{ x: 0, z: 0, colour: Caster, hair: true }, ...Landed].forEach((q, i) => {
    const pos = { x: W.c.x + q.x, z: W.c.z + q.z }, parts = StandingParts(q.colour, !!q.hair), m = pawnMesh(`p${i}`, pos, W.T.heightAt(q.x, q.z), parts, W.sun);
    d3(m.shadow, 0, 0, 0, Ink.withAlpha(W.strength), softGround);
    m.discs.forEach((d, k) => d3(d, 0, 0, 0, parts[k][4] === Hair ? Hair : warm(parts[k][4]), solid));
  });
}

// ---- the fire that runs out ---------------------------------------------------------------------------------
// A ring of flames r cells round the caster, standing up and facing the camera (1.2 to 2.9 cells tall), with a
// glow on the ground inside it, at alpha. The glow lies flat over whatever is drawn, so it fades out as the
// ring passes the crest (past the map it would light the crest's face and the backdrop).
export function drawFireRing(W, r, s, alpha = 1) {
  if (r <= .2 || alpha <= 0) return;
  const n = Math.min(1400, Math.max(24, Math.ceil(Math.PI * 2 * r / .45))), outer = buf3(), core = buf3();
  const quad = (b, x, z, w, h, k) => {
    [[-.5, 0, 0, 0], [-.5, 1, 0, 1], [.5, 1, 1, 1], [.5, 0, 1, 0]].forEach(([a, y, u, v]) => {
      b.xyz.push(x + a * w, y * h, z); b.xz.push(x + a * w, z + y * h * Lift); b.uv.push(u, v);
    });
    b.tri.push(k * 4, k * 4 + 1, k * 4 + 2, k * 4, k * 4 + 2, k * 4 + 3);
  };
  for (let i = 0; i < n; i++) {
    const a = (i + rand(i * 7 + 3) * .6) / n * Math.PI * 2, x = W.c.x + Math.cos(a) * r, z = W.c.z + Math.sin(a) * r;
    const w = .6 * (.8 + .4 * rand(i * 5 + 2)), h = flick(i, s, 2.1);
    quad(outer, x, z, w, h, i);
    quad(core, x, z - .02, w * .5, h * .6, i);
  }
  const floor = alpha * (1 - smooth((r - W.north) / 4));
  if (floor > 0) Graphics.Flat(0, () => fadeRing('ubw reveal fire floor', W.c, Math.max(0, r - 2.5), r, FireOuter.withAlpha(.3 * floor), 0));
  d3(meshOf(mesh('ubw reveal fire'), outer), 0, 0, 0, FireOuter.withAlpha(.5 * alpha), flameMat);
  d3(meshOf(mesh('ubw reveal fire core'), core), 0, 0, 0, FireCore.withAlpha(.5 * alpha), flameMat);
}

// ---- the whole world --------------------------------------------------------------------------------------
// The world at world time s through the 3D camera: the fire has run out fire cells (Infinity: all swords
// stand) and goes no further than reach, its flames at alpha flames; blend is the camera's (the screen's haze
// and embers fade in with it); ridges: the ridges' height in 3D (x); burn: white light added over the sky and
// its gears (0 to 1), under the ground.
export function drawWorld(W, s, { fire = Infinity, reach = Infinity, flames = 0, blend = 0, ridges = RidgeHeight, burn = 0 } = {}) {
  const rise = k => riseOf(k, fire, reach);
  drawSky(W, s);
  if (burn > 0) d3(dome(W), 0, 0, 0, White.withAlpha(burn), burnMat);
  d3(plain(W, false), 0, 0, 0, W.tint, plainMat);
  if (blend > 0) d3(plain(W, true), 0, 0, 0, W.tint.withAlpha(blend), plainMat);

  // Behind the crest, in v4's order: the ridges (writing depth, so a ridge's swords rise from behind it) and
  // the rows of swords between them (not writing it, so a ridge drawn later covers them as in v4).
  const back = backdrop3(W.c, W.north, { horizon: V.horizon, parallax: V.parallax, camX: W.camX, dz: W.dz }, ringOf);
  for (const L of back.layers) {
    if (L.fill) {
      d3(L.fill, 0, 0, 0, L.colour, solid, { sy: ridges });
      d3(L.rim, 0, 0, 0, L.rimColour, onGround, { sy: ridges });
      for (const q of L.swords) { const up = rise(q.ring); if (up > 0) d3(q.mesh, 0, 0, 0, L.swordColour, see, { sy: ridges * up }); }
    } else {
      // Seen from the 3D camera a row stands against the low sun: a dark shape going to haze with distance.
      const colour = Color.Lerp(mixC(C.Silhouette, C.HazeFar, hazeOf(L.d) * .85), L.colour, blend);
      for (const q of L.rings) { const up = rise(q.ring); if (up > 0) d3(q.mesh, 0, 0, 0, colour, see, { sy: up }); }
    }
  }
  // The smoke drifting over the crest and the embers rising from behind it, standing just behind its top.
  const k = W.k;
  if (V.smoke > 0) smokeBands(W.c, s, W.view, k, V.parallax).forEach(({ body, top }, j) => {
    const zp = () => k.edge + k.meanGround + SmokeBehind[j];
    d3(standing(`ubw reveal smoke ${j}`, body, zp), 0, 0, 0, SmokeColour(V.smoke, j), softSky);
    d3(standing(`ubw reveal smoke light ${j}`, top, zp), 0, 0, 0, SmokeLight(V.smoke), glowSky);
  });
  if (V.updraft > 0) updraft(W.c, s, W.view, k, Math.round(V.updraft)).forEach((list, b) => {
    d3(standing(`ubw reveal updraft ${b}`, list, q => k.groundZ(q.x - W.c.x) + EmberBehind), 0, 0, 0, UpdraftColour(b), glowSky);
  });

  // The map: crack floor, plates and their shadows.
  d3(crackFloor(W), 0, 0, 0, Base, solid);
  const t = ground3(W);
  d3(t.ground, W.c.x, 0, W.c.z, W.tint, terrainAtlas);
  d3(t.shadows, W.c.x, 0, W.c.z, new Color(0, 0, 0, .4 * W.strength / .32), onGround);

  // The crest, as v4 draws it: the plateau behind the top, the thicket, the face, the rim, the swords on the
  // top (the rim copies write no depth, so the sword drawn over each covers it); its shadows on the map.
  const K = crestOf(W.c, W.north, V.crest, V.crestSwords, W.sun, ringOf), CC = CrestColours;
  d3(K.plateau, 0, 0, 0, CC.plateau, solid);
  for (const q of K.rings) { const up = rise(q.ring); if (up > 0) { d3(q.back[0], 0, 0, 0, CC.backRim, see, { sy: up }); d3(q.back[1], 0, 0, 0, CC.back, solid, { sy: up }); } }
  d3(K.face, 0, 0, 0, W.tint, faceMat);
  d3(K.rim, 0, 0, 0, CC.rim, onGround);
  for (const q of K.rings) { const up = rise(q.ring); if (up > 0) { d3(q.top[0], 0, 0, 0, CC.topRim, see, { sy: up }); d3(q.top[1], 0, 0, 0, CC.top, solid, { sy: up }); } }

  // The light on the ground; the swords' marks and shadows, then the swords.
  Graphics.Flat(0, () => {
    drawCrestShadows(k, W.strength * rise(ringOf(W.north + 1)));
    gearShadows(W.c, s, W.sun, GearShadows, W.north);
    sunGlow(W.c, W.sun, Look.twilight);
    hill(W.c, Look.hill, W.sun, 1, .5);
  });
  const rings = field3(W);
  for (const q of rings) {
    const up = rise(q.ring);
    if (up <= 0) continue;
    d3(q.shadows, 0, 0, 0, new Color(0, 0, 0, .42 * W.strength / .32 * up), atlasGround);
    d3(q.marks, 0, 0, 0, W.tint.withAlpha(up), atlasGround);
  }
  for (const q of rings) {
    const up = rise(q.ring);
    if (up > 0) d3(q.blades, 0, 0, 0, W.tint, swordAtlas, { sy: up });
  }
  if (fire !== Infinity) drawFireRing(W, fire, s, flames);

  // The camera's haze over the ground and swords (up to the crest's mean top), the pawns, the embers, the ash
  // and sparks in front.
  if (blend > 0) Graphics.OnScreen(() => depthHaze(W.c, W.north, k.meanTop, W.view, V.haze * blend));
  drawPawns(W);
  drawEmbers(W, s);
  if (blend > 0) Graphics.OnScreen(() => foreground(W.c, s, W.view, blend));
}

// v4's embers (lib/ubw-pocket.js embers) in the air round the caster: each at its own height 0.4 to 3.6 cells,
// standing where v4 draws it once the height rule is taken off.
function drawEmbers(W, s) {
  emberLists(W.c, s, 140).forEach((list, k) => {
    const b = buf3();
    list.forEach((q, n) => {
      const y = .4 + 3.2 * hash(q.i, 9, 61);
      [[-.5, -.5, 0, 0], [-.5, .5, 0, 1], [.5, .5, 1, 1], [.5, -.5, 1, 0]].forEach(([x, z, u, v]) => {
        b.xyz.push(q.x + x * q.w, y + z * q.h, q.z - y * Lift); b.xz.push(q.x + x * q.w, q.z + z * q.h); b.uv.push(u, v);
      });
      b.tri.push(n * 4, n * 4 + 1, n * 4 + 2, n * 4, n * 4 + 2, n * 4 + 3);
    });
    d3(meshOf(mesh(`ubw reveal embers ${k}`), b), 0, 0, 0, Ember.withAlpha(EmberAlpha(k)), glowSky);
  });
}

// The world through the map camera, as v4 draws it at world time s, with v4's stand-ins but not its map edge,
// and never closing: the close shot takes the place of v4's closing white.
export function drawFlat(s, ctx) {
  v4.draw(s, { ...V, actors: false, hold: 1e6 }, ctx);
  const W = world(ctx.origin, ctx.scene, ctx.view);
  pawnsFlat(W);
}
function pawnsFlat(W) {
  const warm = { tint: Twilight, tintAmount: .3 * Look.twilight };
  pawn(W.c, Caster, W.sun, W.strength, { hair: true, ...warm });
  Landed.forEach(q => pawn({ x: W.c.x + q.x, z: W.c.z + q.z }, q.colour, W.sun, W.strength, warm));
}
