// Trace kit: the sword crest of Unlimited Blade Works (2026-09-26), for "Unlimited Blade Works: world v4,
// sword crest (pocket)". Not a sketch itself, so it is not listed in sketches/index.js. An experiment, nothing
// agreed.
//
// v3 (lib/ubw-horizon.js) bends the ground past the map edge into a horizon. The 2D games the user pointed at
// (a mountain top over clouds, a SNES cliff over a range, a hilltop against a dusk sky; Final Fantasy VI's
// Solitary Island cliff and the Blackjack's deck) never bend the ground: they cut it. A cliff or a ridge ends
// the top-down ground and behind it hangs a picture drawn from the side, which pans slower than the map. v4
// does that:
//   - the crest: a bank of earth just past the map edge (its foot 0.15 to 0.5 cells past it, so no walkable
//     cell rises), 0.4 to 1.3 times `height` tall with dips every 26 cells or so, its top 0.5 to 1.4 cells north
//     of its foot on the ground (so drawn that plus 0.6 h north of it). The sun is low behind it, so its face
//     is the plates' cracked earth in shade (x 0.62 to 0.4) and a lit rim runs along its top; it throws a
//     shadow onto the map's north cells. Swords, spears and greatswords stand packed on its top against the sky,
//     1.35 times a field sword's size, dark with a lit edge toward the sun, 2 in 5 throwing a long shadow onto
//     the map; a thicket behind the top shows only its upper parts. Mean top 2.05 cells past the edge (H 1.6);
//   - the backdrop behind it, drawn from the side: the sky, sun, clouds, smog and seven gears of v3, three
//     ridges (mountains standing over the horizon, a middle ridge, a low near one with swords on it), and the
//     plain between them with rows of swords shrinking toward the horizon. Each part moves by its share p of
//     the camera's pan across (0 the sun, 0.06 the mountains, 0.45 the nearest row); up and down the horizon
//     moves by 0.25, the nearer parts by more, so panning north opens more plain and sky behind the crest;
//   - motion (the Blackjack's clouds): smoke bands drifting east 0.35 to 1.5 cells over the crest's top at 0.3,
//     0.6 and 1.0 cells a second, embers rising from behind it into the sky, gears turning slower the bigger they are
//     (14 / radius degrees a second: 0.5 for the biggest, 2.5 for the smallest), clouds and smog as v3.
// Drawing: the crest and its swords are fixed to the map and baked once; the ridges and the rows of swords are
// baked once and drawn moved by their parallax; only gears, clouds, smoke and embers are rebuilt per frame. In
// C# that is static meshes and a position per frame from Find.Camera, with no rebuild when the camera moves
// (v3 rebuilds its far ground per 0.05 cells of camera x). Below the map's plates, a cover in the crack floor's
// colour stops the backdrop showing through the cracks south of the crest.
// The gear shape and the sky pieces are copies of lib/ubw-horizon.js (feature/ubw-horizon-sketch has
// uncommitted work that exports them); when the branches meet, import them from there.
import { Color, Mesh, MeshPool, MaterialPool, ShaderDatabase } from '../../js/engine.js';
import { registerLabTexture, pixels, hash, fbm } from '../../js/standins.js';
import { draw, mesh } from './six-paths-solid.js';
import { Lift, soft, glow } from './six-paths-impact.js';
import { shadowLayer } from './goku.js';
import { solid, Black, D2R, clamp, lerp } from './trace.js';
import { BaseLayer, TerrainLayer, Base } from './ubw-terrain.js';
import { quads } from './ubw-pocket.js';
import { KA, KE, SkyGears } from './ubw-horizon.js';

// Colours: v3's sky and plain (lib/ubw-horizon.js), the crest's shaded face.
const Top = new Color(.24, .08, .11), SunFace = new Color(1, .96, .88), SunWarm = new Color(1, .75, .45);
const Silhouette = new Color(.12, .05, .05), Smog = new Color(.27, .09, .09), RimLight = new Color(1, .67, .35);
const CloudDark = new Color(.36, .13, .14), CloudWarm = new Color(.6, .26, .2), CloudLit = new Color(1, .77, .47);
const RidgeEarth = new Color(.34, .22, .16), HazeFar = new Color(.93, .61, .36), Steel = new Color(.6, .58, .6), Spark = new Color(1, .7, .38);
// The face: lib/ubw-terrain.js's earth in shade (x 0.62 at the top, 0.4 at the foot), its cracks the floor's colour.
const EarthDark = new Color(.22, .12, .08), EarthLit = new Color(.58, .38, .25);
// lib/ubw-pocket.js backstop's colour (FarEarth toward Haze by 0.45): the floor cover uses it outside the world.
const Backstop = new Color(.568, .368, .318);
const mixC = (a, b, t) => new Color(lerp(a.r, b.r, t), lerp(a.g, b.g, t), lerp(a.b, b.b, t), 1);

// The crest: foot CrestFoot to CrestFoot + CrestWobble past the edge, top 0.5 to 1.4 cells further north on
// the ground (depthOf); RimW the lit rim; the bake runs Span cells either side of the caster in Step pieces, the face's
// texture repeating every Chunk cells. Dips: one in DipEvery cells on average, 60 % of them there.
export const CrestFoot = .15, CrestWobble = .35;
// SwordScale: the crest's swords against a field sword's size (they are the silhouette the sky is seen through).
const RimW = .07, SwordScale = 1.35, Span = 88, Step = .25, Chunk = 8, DipEvery = 26;
// The framing v3 and the reveal shot end on (7 cells north of the caster), where `horizon` is measured; the
// horizon's share of the camera's pan up and down; the backdrop's lowest line, past the map edge.
export const CamRef = 7, HorizonMove = .25, CoverAt = .45;
const SkyCells = 40, SunUp = 4.5;

// Voronoi cracks on the face, repeating across u: CrackCols x CrackRows jittered seeds per chunk, rows squeezed
// as the height rule squeezes a slope. Distance to the second-nearest seed minus the nearest.
const CrackCols = 3, CrackRows = 2;
function faceCrack(u, v) {
  let d1 = 9, d2 = 9;
  for (let j = 0; j < CrackRows; j++) for (let i = -1; i <= CrackCols; i++) {
    const w = ((i % CrackCols) + CrackCols) % CrackCols, sx = (i + .15 + .7 * hash(w, j, 145)) / CrackCols, sy = (j + .15 + .7 * hash(w, j, 146)) / CrackRows;
    const d = Math.hypot((sx - u) * CrackCols, (sy - v) * CrackRows * 1.6);
    if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
  }
  return d2 - d1;
}
registerLabTexture('lab/ubw-crest-face', () => pixels(256, (u, v) => {
  // v 0 is the top of the bank, 1 its foot; repeats across u, so the chunks join.
  const n = fbm(u * 8, v * 3, 141, 4, 8), grain = fbm(u * 48, v * 24, 143, 2, 48), gully = fbm(u * 16, v * .8, 147, 2, 16);
  const earth = mixC(EarthDark, EarthLit, .2 + .6 * n), shade = lerp(.62, .4, Math.pow(v, .9)) * (.9 + .2 * gully) * (.94 + .12 * grain);
  const crack = faceCrack(u, v), line = clamp(1 - crack / .07), lip = clamp(1 - Math.abs(crack - .1) / .04) * .25;
  let r = earth.r * shade * (1 + lip), g = earth.g * shade * (1 + lip), b = earth.b * shade * (1 + lip);
  r = lerp(r, Base.r, line); g = lerp(g, Base.g, line); b = lerp(b, Base.b, line);
  const rim = Math.pow(clamp(1 - v / .12), 2) * .55;
  return [lerp(r, RimLight.r * .75, rim), lerp(g, RimLight.g * .75, rim), lerp(b, RimLight.b * .75, rim), 1];
}));
const faceMat = MaterialPool.MatFrom('lab/ubw-crest-face', ShaderDatabase.Transparent);
// lib/ubw-horizon.js registers these two when it loads: the sky's gradient, the plain from its foot to the haze.
const skyMat = MaterialPool.MatFrom('lab/ubw-horizon-sky', ShaderDatabase.Transparent);
const groundMat = MaterialPool.MatFrom('lab/ubw-horizon-ground', ShaderDatabase.Transparent);

// ---- buffers (as lib/ubw-horizon.js) --------------------------------------------------------------------
const buffer = () => ({ xz: [], uv: [], tri: [] });
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
function strokeInto(out, a, b, w) {
  const dx = b.x - a.x, dz = b.z - a.z, len = Math.hypot(dx, dz) || 1, nx = -dz / len * w / 2, nz = dx / len * w / 2;
  polyInto(out, [{ x: a.x + nx, z: a.z + nz }, { x: b.x + nx, z: b.z + nz }, { x: b.x - nx, z: b.z - nz }, { x: a.x - nx, z: a.z - nz }]);
}
function meshOf(name, b) {
  const m = new Mesh(name);
  m.setFlat(b.xz, b.tri);
  m.uv = new Float32Array(b.uv);
  return m;
}

// A weapon standing on the screen: foot (fx, fz), len screen cells long, leaning lean radians from upright
// (east positive). type 0 a sword and 1 a greatsword, point in the ground (blade, guard, grip, pommel); 2 a
// spear, butt in the ground, head up. w widens every piece (the rim copy).
function weaponInto(out, fx, fz, len, lean, type, w = 0) {
  const dx = Math.sin(lean), dz = Math.cos(lean), ax = dz, az = -dx;
  const P = (d, s = 0) => ({ x: fx + dx * d + ax * s, z: fz + dz * d + az * s });
  if (type === 2) {
    strokeInto(out, P(0), P(len * .86), .045 + w);
    polyInto(out, [P(len * .8, 0), P(len * .88, -.075 - w), P(len + w, 0), P(len * .88, .075 + w)]);
    return;
  }
  const bw = (type ? .15 : .085) + w, gw = (type ? .27 : .2) + w, G = len * (type ? .66 : .7);
  polyInto(out, [P(0, -bw * .35), P(0, bw * .35), P(G, bw / 2), P(G, -bw / 2)]);
  polyInto(out, [P(G - .03 - w, -gw), P(G - .03 - w, gw), P(G + .03 + w, gw), P(G + .03 + w, -gw)]);
  strokeInto(out, P(G), P(len * .93), .05 + w);
  const k = .045 + w;
  polyInto(out, [P(len * .96, -k), P(len * .96 + k, 0), P(len * .96, k), P(len * .96 - k, 0)]);
}
// Kind, real height (cells) and screen lean of weapon j of a row.
function weaponOf(j, seed) {
  const r = hash(j, 3, seed), type = r < .6 ? 0 : r < .8 ? 1 : 2, [lo, hi] = [[1, 1.5], [1.3, 1.9], [2, 2.8]][type];
  const lean = hash(j, 8, seed) < .15 ? 45 : 26;
  return { type, tall: lerp(lo, hi, hash(j, 4, seed)), short: .85 + .15 * hash(j, 5, seed), lean: (hash(j, 6, seed) - .5) * 2 * lean * D2R };
}

// ---- the crest ------------------------------------------------------------------------------------------
// Its profile, x in cells from the caster: the foot (cells past the edge) and the height (cells).
const footOf = x => CrestFoot + CrestWobble * fbm(x * .23 + 11, 3.7, 131, 2, 64);
function dipOf(x) {
  const k = Math.round(x / DipEvery), centre = k * DipEvery + (hash(k, 1, 137) - .5) * 10, w = 2 + 2 * hash(k, 2, 137);
  const d = clamp(1 - Math.abs(x - centre) / w);
  return hash(k, 3, 137) < .6 ? d * d * (3 - 2 * d) : 0;
}
// Height: 0.4 to 1.3 of H on a mix of slow and quick waves, cut down by the dips. Depth (how far north of the
// foot the top is, on the ground): 0.5 to 1.4 cells, so the top line wanders more than the foot.
const heightOf = (x, H) => {
  const n = .5 + .26 * Math.sin(x * .13 + 1.3) + .16 * Math.sin(x * .37 + 4.1) + .1 * Math.sin(x * .9 + .7) + .34 * (fbm(x * .45 + 7, 1.1, 133, 3, 64) - .5);
  return H * (.4 + .9 * clamp(n)) * (1 - .6 * dipOf(x));
};
const depthOf = x => .5 + .9 * fbm(x * .17 + 3, 5.3, 139, 2, 64);
// The crest round c whose map ends `north` cells north of it, H tall, perCell swords a cell along its top,
// under this sun: its profile on the screen and its baked meshes.
const crests = new Map();
export function crestOf(c, north, H, perCell, sun) {
  const key = `${c.x},${c.z}|${north}|${H}|${perCell}|${sun.x.toFixed(3)},${sun.z.toFixed(3)}`;
  if (crests.has(key)) return crests.get(key);
  const E = c.z + north, footZ = x => E + footOf(x), topZ = x => footZ(x) + depthOf(x) + heightOf(x, H) * Lift;
  const face = buffer(), rim = buffer(), shade = buffer(), bladeShade = buffer();
  const rows = { back: [buffer(), buffer()], top: [buffer(), buffer()] };
  const n = Math.round(2 * Span / Step);
  let sum = 0;
  for (let i = 0; i < n; i++) {
    const x0 = -Span + i * Step, x1 = x0 + Step, a = c.x + x0, b = c.x + x1;
    const f0 = footZ(x0), f1 = footZ(x1), t0 = topZ(x0), t1 = topZ(x1), u0 = (i % (Chunk / Step)) * Step / Chunk, u1 = u0 + Step / Chunk;
    sum += t0 - E;
    polyInto(face, [{ x: a, z: f0 }, { x: a, z: t0 }, { x: b, z: t1 }, { x: b, z: f1 }], [[u0, .01], [u0, .99], [u1, .99], [u1, .01]]);
    polyInto(rim, [{ x: a, z: t0 }, { x: b, z: t1 }, { x: b, z: t1 - RimW }, { x: a, z: t0 - RimW }]);
    // The shadow of the top edge (on the ground depthOf north of the foot, h up), kept south of the foot.
    const h0 = heightOf(x0, H), h1 = heightOf(x1, H);
    const s0 = { x: a + sun.x * h0, z: Math.min(f0, f0 + depthOf(x0) + sun.z * h0) }, s1 = { x: b + sun.x * h1, z: Math.min(f1, f1 + depthOf(x1) + sun.z * h1) };
    polyInto(shade, [{ x: a, z: f0 }, { x: b, z: f1 }, s1, s0]);
  }
  // Swords: gap 1 / density, a sixth of the places left empty.
  const place = (density, seed, fn) => {
    const gap = 1 / density;
    for (let j = Math.floor(-Span / gap); j <= Span / gap; j++) if (hash(j, 1, seed) < .85) fn((j + .8 * hash(j, 2, seed)) * gap, j);
  };
  const toSun = Math.sign(-sun.x) || 1;
  const put = (pair, x, z, len, q) => {
    weaponInto(pair[0], c.x + x + toSun * .03, z + .018, len, q.lean, q.type, .035);
    weaponInto(pair[1], c.x + x, z, len, q.lean, q.type);
  };
  // Behind the top: only the upper parts show over it. On the top: against the sky; two in five throw a long
  // shadow onto the map (from where the shadow leaves the bank). Both SwordScale times a field sword's height.
  place(perCell * .8, 151, (x, j) => {
    const q = weaponOf(j, 151);
    put(rows.back, x, topZ(x) - .2 - .6 * hash(j, 7, 151), q.tall * Lift * q.short * SwordScale * 1.1, q);
  });
  place(perCell, 153, (x, j) => {
    const q = weaponOf(j, 153), h = heightOf(x, H), zg = footZ(x) + depthOf(x), tall = q.tall * SwordScale;
    put(rows.top, x, topZ(x) - .05, tall * Lift * q.short, q);
    if (hash(j, 9, 153) > .4) return;
    const from = Math.max(h, depthOf(x) / Math.max(.05, -sun.z)), to = h + tall;
    if (sun.z < 0 && from < to) strokeInto(bladeShade, { x: c.x + x + sun.x * from, z: zg + sun.z * from }, { x: c.x + x + sun.x * to, z: zg + sun.z * to }, .08);
  });
  const k = {
    footZ, topZ, meanTop: sum / n,
    face: meshOf('ubw crest face', face), rim: meshOf('ubw crest rim', rim), shade: meshOf('ubw crest shade', shade), bladeShade: meshOf('ubw crest blade shade', bladeShade),
    back: rows.back.map((b, i) => meshOf(`ubw crest back ${i}`, b)), top: rows.top.map((b, i) => meshOf(`ubw crest top ${i}`, b)),
    vertices: [face, rim, shade, bladeShade, ...rows.back, ...rows.top].reduce((v, b) => v + b.xz.length / 2, 0),
  };
  crests.set(key, k);
  if (crests.size > 3) crests.delete(crests.keys().next().value);
  return k;
}
// The crest over the map's plates: the thicket behind it, the face, the rim, the swords on the top; its
// shadow and its swords' shadows on the map.
export function drawCrest(k, strength, tint) {
  draw(k.shade, 0, shadowLayer + .0012, 0, 1, 1, 0, Black.withAlpha(.4 * strength / .32), solid);
  draw(k.bladeShade, 0, shadowLayer + .0013, 0, 1, 1, 0, Black.withAlpha(.22 * strength / .32), solid);
  draw(k.back[0], 0, TerrainLayer + .0002, 0, 1, 1, 0, RimLight.withAlpha(.35), solid);
  draw(k.back[1], 0, TerrainLayer + .00021, 0, 1, 1, 0, mixC(Silhouette, HazeFar, .14), solid);
  draw(k.face, 0, TerrainLayer + .0004, 0, 1, 1, 0, tint, faceMat);
  draw(k.rim, 0, TerrainLayer + .0005, 0, 1, 1, 0, RimLight.withAlpha(.45), solid);
  draw(k.top[0], 0, TerrainLayer + .0006, 0, 1, 1, 0, RimLight.withAlpha(.6), solid);
  draw(k.top[1], 0, TerrainLayer + .0007, 0, 1, 1, 0, Silhouette, solid);
}

// ---- the backdrop's baked layers -------------------------------------------------------------------------
// Ridges: base and top (screen cells from the horizon line), wave number f, parallax p, haze, and the swords
// along the top (per cell, their height in screen cells). Rows of swords on the plain between them.
export const Ridges = [
  { base: -.3, h: 3.2, f: .05, ph: 3, p: .06, haze: .72, swords: 0 },
  { base: -.9, h: 1.4, f: .12, ph: 2, p: .14, haze: .5, swords: 1.4, tall: .22 },
  { base: -1.9, h: 1.1, f: .2, ph: 4, p: .24, haze: .32, swords: 1.1, tall: .38 },
];
const RidgeDepth = 4, RowCount = 14, RowDeep = 5.5;
const profile = (R, x) => clamp(.5 + .25 * Math.sin(x * R.f + R.ph) + .15 * Math.sin(x * R.f * 2.7 + R.ph * 3) + .1 * Math.sin(x * R.f * 6.1 + R.ph * 5) + .24 * (fbm(x * R.f * 4 + R.ph * 10, R.ph, 87, 2, 64) - .5));
// Row k (0 at the horizon): how far below the horizon it sits at the usual framing, its parallax, spacing,
// sword height and haze.
export const rowOf = k => {
  const n = k / (RowCount - 1);
  return { d: .08 + RowDeep * Math.pow(n, 1.7), p: .08 + .37 * n, gap: .35 + .9 * n, tall: .06 + .5 * Math.pow(n, 1.3), haze: .9 - .6 * n };
};
let layers = null;
function backdropLayers() {
  if (layers) return layers;
  const ridges = Ridges.map((R, r) => {
    const fill = buffer(), rim = buffer(), swords = buffer();
    for (let x = -Span; x < Span; x += .5) {
      const t0 = R.base + R.h * profile(R, x), t1 = R.base + R.h * profile(R, x + .5);
      polyInto(fill, [{ x, z: R.base - RidgeDepth }, { x, z: t0 }, { x: x + .5, z: t1 }, { x: x + .5, z: R.base - RidgeDepth }]);
      polyInto(rim, [{ x, z: t0 }, { x: x + .5, z: t1 }, { x: x + .5, z: t1 - .05 }, { x, z: t0 - .05 }]);
    }
    if (R.swords) for (let j = Math.floor(-Span * R.swords); j < Span * R.swords; j++) {
      if (hash(j, r, 171) < .4) continue;
      const x = (j + .7 * hash(j, r, 172)) / R.swords, top = R.base + R.h * profile(R, x), len = R.tall * (.7 + .6 * hash(j, r, 173)), lean = (hash(j, r, 174) - .5) * .5;
      strokeInto(swords, { x, z: top - .03 }, { x: x + Math.sin(lean) * len, z: top + Math.cos(lean) * len }, Math.max(.03, R.tall * .16));
    }
    return { fill: meshOf(`ubw crest ridge ${r}`, fill), rim: meshOf(`ubw crest ridge rim ${r}`, rim), swords: R.swords ? meshOf(`ubw crest ridge swords ${r}`, swords) : null };
  });
  const rows = Array.from({ length: RowCount }, (_, k) => {
    const R = rowOf(k), out = buffer();
    for (let j = Math.floor(-Span / R.gap); j < Span / R.gap; j++) {
      if (hash(j, k, 175) < .3) continue;
      const x = (j + .8 * hash(j, k, 176)) * R.gap, z = (hash(j, k, 177) - .5) * R.gap * .3, len = R.tall * (.6 + .8 * hash(j, k, 178)), lean = (hash(j, k, 179) - .5) * .6;
      strokeInto(out, { x, z }, { x: x + Math.sin(lean) * len, z: z + Math.cos(lean) * len }, Math.max(.025, R.tall * .14));
    }
    return meshOf(`ubw crest row ${k}`, out);
  });
  layers = { ridges, rows };
  return layers;
}

// ---- gears (a copy of lib/ubw-horizon.js gearShape / gearMeshAt) -----------------------------------------
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
function gearMeshAt(key, type, teeth, turn, squash) {
  const s = gearShape(type, teeth), out = new Array(s.xz.length), ct = Math.cos(turn), st = Math.sin(turn);
  for (let k = 0; k < s.xz.length; k += 2) {
    out[k] = s.xz[k] * ct - s.xz[k + 1] * st;
    out[k + 1] = (s.xz[k] * st + s.xz[k + 1] * ct) * squash;
  }
  const m = mesh(key);
  m.setFlat(out, s.tri);
  return m;
}
const skyAt = e => {
  const Glow = new Color(1, .89, .67), Horizon = new Color(1, .62, .3), Mid = new Color(.86, .36, .2), High = new Color(.5, .17, .16);
  if (e < 6) return mixC(Glow, Horizon, clamp(e / 6));
  if (e < 16) return mixC(Horizon, Mid, (e - 6) / 10);
  if (e < 38) return mixC(Mid, High, (e - 16) / 22);
  return mixC(High, Top, clamp((e - 38) / 40));
};

// ---- drawing the backdrop ----------------------------------------------------------------------------------
// Order of the backdrop's drawings, all between the crack floor and the map's plates.
const L = k => BaseLayer + .0002 + k * .00005;
const wrap = (v, n) => ((v % n) + n) % n - n / 2;

// Everything behind the crest k for the world round c (map edge `north` cells north of it) at time s, seen by
// view. o: horizon (cells past the edge at the usual framing), parallax (x every part's share), spin (x the
// gears' turning), smoke (its opacity), updraft (embers), edge (the world square's half size).
export function drawBackdrop(c, s, sun, view, k, o) {
  const m = o.parallax, camX = view.cx, dz = view.cz - (c.z + CamRef), width = 2 * view.halfW + 10;
  const share = p => Math.min(.95, p * m);
  // A part with parallax p: its x offset, and where a line `off` screen cells from the horizon sits in z.
  const xAt = p => (1 - share(p)) * (camX - c.x);
  const lineAt = (off, p) => c.z + o.north + o.horizon + off + (1 - share(p)) * dz;
  const horizonZ = lineAt(0, HorizonMove), pv = f => HorizonMove + (1 - HorizonMove) * f;
  const skyX = (a, p) => c.x + a * KA + xAt(p), skyZ = e => horizonZ + e * KE;
  const cover = c.z + o.north + CoverAt;

  // The sky, as v3: the plane above it, the gradient, the sun's glow, clouds, the sun, the smog.
  draw(MeshPool.plane10, camX, L(0), horizonZ + SkyCells + 200, width + 400, 400, 0, Top, solid);
  draw(MeshPool.plane10, camX, L(1), horizonZ + (SkyCells - 1) / 2, width, SkyCells + 1, 0, new Color(1, 1, 1, 1), skyMat);
  const sunA = clamp(Math.atan2(-sun.x, -sun.z) / D2R, -70, 70), sunPos = { x: skyX(sunA, 0), z: skyZ(SunUp) };
  draw(MeshPool.plane10, sunPos.x, L(2), sunPos.z, 30 * KE * 2, 30 * KE * 2, 0, SunWarm.withAlpha(.38), glow);
  const bodies = [[], [], []], lit = [[], [], []];
  for (let i = 0; i < 20; i++) {
    const a = (hash(i, 1, 51) - .5) * 180 + s * (.35 + .7 * hash(i, 2, 51)), e = 9 + Math.pow(hash(i, 3, 51), .8) * 58;
    const near = clamp(1 - Math.abs(a - sunA) / 80) * clamp(1 - (e - 8) / 50), g = Math.min(2, Math.floor(near * 3)), n = 8 + Math.floor(hash(i, 4, 51) * 6);
    for (let q = 0; q < n; q++) {
      const da = (hash(i * 16 + q, 5, 51) - .5) * 18, de = (hash(i * 16 + q, 6, 51) - .35) * 5, r = 3.5 + hash(i * 16 + q, 7, 51) * 5.5;
      const x = skyX(a + da, .04), z = skyZ(e + de), w = r * KE * 2.6, h = r * KE * 2;
      bodies[g].push({ x, z, w, h });
      lit[g].push({ x, z: z - .9 * KE - h * .12, w: w * .8, h: h * .75 });
    }
  }
  bodies.forEach((list, g) => quads(`ubw crest clouds ${g}`, list, mixC(CloudDark, CloudWarm, g / 2).withAlpha(.5), soft, L(3) + g * .00001));
  lit.forEach((list, g) => quads(`ubw crest cloud light ${g}`, list, CloudLit.withAlpha(.12 + .19 * g), glow, L(4) + g * .00001));
  draw(MeshPool.plane10, sunPos.x, L(5), sunPos.z, 8 * KE * 2, 8 * KE * 2, 0, SunWarm.withAlpha(.6), glow);
  draw(MeshPool.plane10, sunPos.x, L(6), sunPos.z, 1.6 * KE * 2, 1.6 * KE * 2, 0, SunFace.withAlpha(.97), soft);
  const smog = [];
  for (let i = 0; i < 8; i++) {
    const a = (hash(i, 1, 53) - .5) * 170 + s * (.8 + 1.4 * hash(i, 2, 53)), e = 3 + i * 2.4 + hash(i, 3, 53) * 2;
    smog.push({ x: skyX(a, .06), z: skyZ(e), w: (28 + 34 * hash(i, 4, 53)) * KE * 2, h: (.8 + 1.2 * hash(i, 5, 53)) * KE * 2 });
  }
  quads('ubw crest smog', smog, Smog.withAlpha(.34), soft, L(7));

  // The gears, the hazier (further) first: a warm rim toward the sun, then the body. Bigger turns slower.
  const toSunX = Math.sign(sunA) || 1;
  SkyGears.slice().sort((a, b) => b.haze - a.haze).forEach((g, i) => {
    const R = g.r * KE, x = skyX(g.a, g.p), z = skyZ(g.e), spin = Math.sign(g.spin) * o.spin * 14 / g.r;
    const gm = gearMeshAt(`ubw crest gear ${i}`, g.type, g.teeth, (s * spin + g.a * 5) * D2R, .8);
    draw(gm, x + toSunX * R * .02, L(8 + i * 2), z + R * .012, R, R, 0, RimLight.withAlpha(.5 * (1 - g.haze)), solid);
    draw(gm, x, L(9 + i * 2), z, R, R, 0, mixC(Silhouette, skyAt(g.e + g.r * .3), g.haze).withAlpha(.97), solid);
  });

  // The ridges and the plain, far to near: mountains, the plain's colour, rows, the middle ridge, rows, the
  // near ridge, rows. The plain runs from the cover line to the horizon.
  const B = backdropLayers();
  const ridge = (r, at) => {
    const R = Ridges[r], z = lineAt(0, pv(R.p)), x = c.x + xAt(R.p), col = mixC(RidgeEarth, HazeFar, R.haze);
    draw(B.ridges[r].fill, x, L(at), z, 1, 1, 0, col, solid);
    draw(B.ridges[r].rim, x, L(at) + .00001, 0 + z, 1, 1, 0, mixC(col, RimLight, .45).withAlpha(.45), solid);
    if (B.ridges[r].swords) draw(B.ridges[r].swords, x, L(at) + .00002, z, 1, 1, 0, mixC(mixC(Steel, Silhouette, .5), HazeFar, R.haze * .9), solid);
  };
  const rows = (k0, k1, at) => {
    for (let q = k0; q < k1; q++) {
      const R = rowOf(q), z = lineAt(-R.d, pv(R.p));
      if (z + R.tall < cover) continue;
      draw(B.rows[q], c.x + xAt(R.p), L(at) + q * .000002, z, 1, 1, 0, mixC(mixC(Steel, Silhouette, .35), HazeFar, R.haze), solid);
    }
  };
  ridge(0, 22);
  if (horizonZ > cover) draw(MeshPool.plane10, camX, L(24), (horizonZ + cover) / 2, width, horizonZ - cover, 0, o.tint, groundMat);
  rows(0, 5, 25); ridge(1, 26); rows(5, 9, 27); ridge(2, 28); rows(9, RowCount, 29);

  // Motion in the drop behind the crest: smoke bands drifting east (far to near), then embers rising from
  // behind the crest into the sky. Each smoke band sits `up` cells over the crest's mean top at the usual
  // framing and moves up and down by its own share of the pan.
  const crestLine = (up, p) => k.meanTop + c.z + up + (1 - share(p)) * dz;
  if (o.smoke > 0) [[.35, .3, 1.5], [.5, .6, .9], [.7, 1, .35]].forEach(([p, v, up], j) => {
    const W = width + 30, body = [], top = [], z0 = crestLine(up, pv(p));
    for (let i = 0; i < 7; i++) {
      const w = 9 + 14 * hash(i, j, 181), h = .5 + .7 * hash(i, j, 182);
      const x = camX + wrap(hash(i, j, 183) * W + v * s - share(p) * (camX - c.x), W), z = z0 + (hash(i, j, 184) - .5) * .8 + .15 * Math.sin(s * .3 + i + j);
      body.push({ x, z, w, h });
      top.push({ x, z: z + h * .22, w: w * .85, h: h * .45 });
    }
    quads(`ubw crest smoke ${j}`, body, Smog.withAlpha(o.smoke * (.75 + .15 * j)), soft, L(30 + j));
    quads(`ubw crest smoke light ${j}`, top, CloudLit.withAlpha(o.smoke * .22), glow, L(30 + j) + .00001);
  });
  if (o.updraft > 0) {
    const W = width + 8, buckets = [[], [], [], []];
    for (let i = 0; i < o.updraft; i++) {
      const T = 3.5 + 2 * hash(i, 1, 161), t = s + hash(i, 2, 161) * T, cycle = Math.floor(t / T), age = t - cycle * T;
      const x = camX + wrap(hash(i + cycle * 97, 3, 161) * W - .8 * (camX - c.x), W) + .25 * Math.sin(s * .9 + i) + age * (hash(i, 4, 161) - .3) * .5;
      const z = k.topZ(x - c.x) - .35 + age * (.8 + .7 * hash(i, 5, 161)), a = clamp(age / .4) * clamp((1 - age / T) / .45);
      const r = (.08 + .14 * hash(i, 6, 161)) * (1 + .3 * Math.sin(s * 9 + i * 3));
      if (a > .02) buckets[Math.min(3, Math.floor(a * 4))].push({ x, z, w: r * 2, h: r * 2 });
    }
    buckets.forEach((list, b) => quads(`ubw crest updraft ${b}`, list, Spark.withAlpha(.8 * (b + 1) / 4), glow, L(34) + b * .00001));
  }

  // The cover: the crack floor's colour from below the screen to the cover line, over the world's square (the
  // backstop's colour outside it), so the backdrop never shows through the plates' cracks.
  const bottom = Math.min(view.cz - view.halfH - 4, cover - 1), h = cover - bottom, x0 = camX - width / 2, x1 = camX + width / 2;
  const w0 = Math.max(x0, c.x - o.edge), w1 = Math.min(x1, c.x + o.edge);
  if (w1 > w0) draw(MeshPool.plane10, (w0 + w1) / 2, L(40), bottom + h / 2, w1 - w0, h, 0, Base, solid);
  if (w0 > x0) draw(MeshPool.plane10, (x0 + w0) / 2, L(40), bottom + h / 2, w0 - x0, h, 0, Backstop, solid);
  if (x1 > w1) draw(MeshPool.plane10, (w1 + x1) / 2, L(40), bottom + h / 2, x1 - w1, h, 0, Backstop, solid);
  return { horizonZ };
}
