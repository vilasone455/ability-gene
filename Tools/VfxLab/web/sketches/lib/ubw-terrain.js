// Trace kit: the ground of Unlimited Blade Works with height (2026-09-25), for "Unlimited Blade Works:
// world v2, depth (pocket)". Not a sketch itself, so it is not listed in sketches/index.js. The flat ground
// of the first pocket world (lib/ubw-pocket.js floor and patches) stays as it is.
//
// The idea comes from Kamui's dimension (lib/kamui.js): what makes that field read as 3D is that every
// block is a pillar with a visible south face, the blocks sit at different levels (drawn 0.60 cells north
// per cell up), and past the map edge the ground is free to rise and fall. The same is done here with
// cracked earth. An experiment, nothing agreed:
//   - plates: Voronoi cells of jittered grids (3.2 cells apart on the map with 12 % of the seeds left out
//     so some plates come out twice the size, 2.2 apart on the hill so its terraces have more steps, 5.5
//     past the map), each shrunk by half the crack width, which varies from edge to edge (0.4 to 1.6 of
//     the width, the same on both sides of an edge). Each plate has one height and one of four earth
//     shades; hairline cracks wander over the plates on the map.
//   - on the map the plates are level, give or take a step of 0.08 cells, and the hill of swords rises in
//     three terraces of 0.18 cells. Small on purpose: pawns are drawn on their cells and cannot be lifted,
//     so the walkable ground cannot really rise (the Kamui rule: one walkable level).
//   - past the map edge the ground is free. North, east and west of the map the plates rise in tiers of
//     1.2 cells (three tiers across the 16 cells the sword field goes on for, the tier line wobbling round
//     the map, a jitter of one tier between neighbours). South of the map they drop the same way, to the
//     south tier count, and never rise, so nothing past the south edge is ever drawn over the map. A plate
//     that overlaps the map edge stays level, so the tiers begin one plate out. Along the north edge of the
//     drawn ground the plates get 0 to 5 cells more on a smooth profile: the far ridge, the silhouette the
//     sky stands behind (lib/ubw-sky.js).
//   - every plate is a pillar: each south-facing edge gets a face down to the bottom of the world, so a
//     plate shows exactly the height it stands above its southern neighbour. Plates draw north first, by
//     their seeds: for Voronoi cells that is precisely the order in which a face is covered by the plate in
//     front of it (the face along a shared edge belongs to the plate whose seed is further north).
//   - a raised edge casts a shadow along the sun, swept by the drop to the neighbour.
//
// Drawing: one mesh from one atlas (lab/ubw-terrain-atlas: the earth tile in four shades, a face gradient,
// swatches) in painter's order; per plate the faces (gradient, then solid), the top (its own window of one
// earth tile, so nothing tiles), the hairline cracks, the lit lip along a crest that stands above its
// neighbour. The shadows are a second mesh on the shadow layer. The swords of the field stand on the
// plates: lib/ubw-pocket.js makeField takes heightAt() and lifts each sword by its plate. Level shapes only,
// no facing. The atlas is a lab texture; a port makes it a PNG from the same formulas.
import { AltitudeLayer, Color, MaterialPool, Mesh, MeshPool, ShaderDatabase } from '../../js/engine.js';
import { registerLabTexture, pixels, fbm, hash } from '../../js/standins.js';
import { draw } from './six-paths-solid.js';
import { Lift } from './six-paths-impact.js';
import { shadowLayer } from './goku.js';
import { solid, Black, clamp, lerp } from './trace.js';
import { MapHalf } from './ubw-pocket.js';

// The defaults (2026-09-25, an experiment): what the v2 sketch's sliders start at, and what is baked.
export const Ground = {
  plate: 3.2, hillPlate: 2.2, outer: 5.5, drop: .12, gap: .14,
  tierStep: 1.2, tiers: 3, southTiers: 3, jitter: 1, ridgeBand: 6, ridgeMax: 5,
  hillStep: .18, plateStep: .08, hillLevels: 3,
};
const terrainAlt = AltitudeLayer.Terrain.AltitudeFor();
export const TerrainLayer = terrainAlt + .005, BaseLayer = terrainAlt + .002;
// The face gradient runs GradLen screen cells down from the crest, then the face is solid to the bottom.
// LipW is the lit lip along a crest, drawn only where the plate stands LipMin cells or more above the
// plate across that edge (so the level plates on the map show cracks, not tile edges). A plate takes a
// window of Tile cells of an earth tile (or its own size if bigger). The ground is drawn Margin cells
// past the sword field and cut off Edge cells past that: the world's edge, where the sky begins north.
// Box is a seed's Voronoi search box in spacings. Hairlines is the number of thin cracks on a map plate.
const GradLen = 3, LipW = .06, LipMin = .1, Tile = 6, Margin = 2, Edge = 2, Box = 2.5, Hairlines = 2, HairW = .028;
const EarthDark = new Color(.22, .12, .08), EarthLit = new Color(.58, .38, .25);
const Crest = new Color(.32, .18, .12), Foot = new Color(.11, .06, .045), LipLit = new Color(.66, .46, .32);
export const Base = new Color(.07, .045, .035);

// ---- the atlas (1024 px): the earth tile of 384 px in four shades in a 2 x 2 block at the top left, the
// face gradient (crest colour at the top, foot colour at the bottom) in a column right of it, swatches of
// 64 px along the bottom: 0 the lit lip, 1 the crest, 2 the solid foot, 3 the crack floor, 4 a hairline
// crack (the foot colour at 0.55).
const Side = 1024, TilePx = 384, Pad = 4, GradX0 = 800, GradX1 = 832, GradH = 768, SwatchY = 900, SwatchPx = 64;
const Shades = [[1, 1, 1], [.84, .82, .8], [1.12, 1.06, 1], [.95, .9, .86]];
const TileUV = TilePx / Side;
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
const mix = (a, b, t) => [lerp(a.r, b.r, t), lerp(a.g, b.g, t), lerp(a.b, b.b, t)];
// The earth once, at tile size: lib/ubw-pocket.js's dust and fine cracks, without the big cracks (the
// plates are those). The four shades multiply it.
let earthTile = null;
function earthAt(px, py) {
  if (!earthTile) {
    earthTile = new Float32Array(TilePx * TilePx * 3);
    for (let y = 0; y < TilePx; y++) for (let x = 0; x < TilePx; x++) {
      const tu = x / TilePx, tv = y / TilePx;
      const n = fbm(tu * 4, tv * 4, 301, 4, 4), fine = fbm(tu * 16, tv * 16, 305, 2, 16), small = border(tu, tv, 7, 331);
      const k = (1 - .15 * (1 - edge(0, .03, small))) * (.92 + .16 * fine);
      const pebble = hash(Math.floor(tu * 128), Math.floor(tv * 128), 337) > .995 ? .18 : 0;
      const [r, g, b] = mix(EarthDark, EarthLit, .2 + .6 * n), o = (y * TilePx + x) * 3;
      earthTile[o] = r * k + pebble; earthTile[o + 1] = g * k + pebble * .8; earthTile[o + 2] = b * k + pebble * .6;
    }
  }
  const o = (py * TilePx + px) * 3;
  return [earthTile[o], earthTile[o + 1], earthTile[o + 2]];
}
registerLabTexture('lab/ubw-terrain-atlas', () => pixels(Side, (u, v) => {
  const px = Math.floor(u * Side), py = Math.floor(v * Side);
  if (px < 2 * TilePx && py < 2 * TilePx) {
    const k = Math.floor(py / TilePx) * 2 + Math.floor(px / TilePx), [r, g, b] = earthAt(px % TilePx, py % TilePx), sh = Shades[k];
    return [Math.min(1, r * sh[0]), Math.min(1, g * sh[1]), Math.min(1, b * sh[2]), 1];
  }
  if (px >= GradX0 && px < GradX1 && py < GradH) {
    const t = py / GradH, [r, g, b] = mix(Crest, Foot, t * t * (3 - 2 * t));
    return [r, g, b, 1];
  }
  if (py >= SwatchY && py < SwatchY + SwatchPx) {
    const k = Math.min(4, Math.floor(px / SwatchPx)), c = [LipLit, Crest, Foot, Base, Foot][k];
    return [c.r, c.g, c.b, k === 4 ? .55 : 1];
  }
  return [0, 0, 0, 0];
}));
const atlas = MaterialPool.MatFrom('lab/ubw-terrain-atlas', ShaderDatabase.Transparent);
const Grad = { u: (GradX0 + GradX1) / 2 / Side, top: 1 - (Pad + 1) / Side, bot: 1 - (GradH - Pad) / Side };
const swatch = k => [(k + .5) * SwatchPx / Side, 1 - (SwatchY + SwatchPx / 2) / Side];
const SwLip = swatch(0), SwFoot = swatch(2), SwHair = swatch(4);
// Tile k's uv origin (its bottom-left corner, v up) and span.
const tileUV = k => ({ u0: (k % 2) * TileUV, v0: 1 - (Math.floor(k / 2) + 1) * TileUV, span: TileUV });

// ---- the plates ----------------------------------------------------------------------------------------
// A convex polygon is a list of { x, z, nb, g }: nb is the seed whose bisector made the edge that starts at
// this vertex (-1 for the search box), g is how far that edge moves inward for the crack. Keep the part of
// poly nearer seed s than the seed 2 x half cells away along (nx, nz).
function clipBy(poly, s, nx, nz, half, nb) {
  const out = [];
  for (let i = 0; i < poly.length; i++) {
    const a = poly[i], b = poly[(i + 1) % poly.length];
    const fa = half - ((a.x - s.x) * nx + (a.z - s.z) * nz), fb = half - ((b.x - s.x) * nx + (b.z - s.z) * nz);
    if (fa >= 0) out.push(a);
    if ((fa >= 0) !== (fb >= 0)) {
      const t = fa / (fa - fb);
      out.push({ x: a.x + (b.x - a.x) * t, z: a.z + (b.z - a.z) * t, nb: fa >= 0 ? nb : a.nb });
    }
  }
  return out;
}
const area2 = pts => { let a = 0; for (let i = 0; i < pts.length; i++) { const p = pts[i], n = pts[(i + 1) % pts.length]; a += p.x * n.z - n.x * p.z; } return a; };
// One try at moving every edge its g cells inward (scaled by k): the polygon, or the vertex that stops it
// (two edges that run parallel there, or an edge too short to survive the shrink).
function insetOnce(poly, k) {
  const n = poly.length, sgn = area2(poly) > 0 ? 1 : -1;
  const lines = poly.map((a, i) => {
    const b = poly[(i + 1) % n], dx = b.x - a.x, dz = b.z - a.z, L = Math.hypot(dx, dz) || 1, g = a.g * k;
    return { px: a.x - dz / L * sgn * g, pz: a.z + dx / L * sgn * g, dx: dx / L, dz: dz / L, nb: a.nb, g: a.g };
  });
  const out = [];
  for (let i = 0; i < n; i++) {
    const p = lines[(i - 1 + n) % n], q = lines[i], det = p.dx * q.dz - p.dz * q.dx;
    if (Math.abs(det) < 1e-6) return { bad: i };
    const t = ((q.px - p.px) * q.dz - (q.pz - p.pz) * q.dx) / det;
    out.push({ x: p.px + p.dx * t, z: p.pz + p.dz * t, nb: q.nb, g: q.g });
  }
  for (let i = 0; i < n; i++) {
    const a = out[i], b = out[(i + 1) % n];
    if ((b.x - a.x) * lines[i].dx + (b.z - a.z) * lines[i].dz <= .03) return { bad: (i + 1) % n };
  }
  return { out };
}
// The polygon with every edge moved inward by its g. A Voronoi cell often has an edge shorter than the
// shrink; that edge is merged away (its far vertex dropped) and the shrink tried again, so no plate is
// lost to a sliver. A cell too small for its gaps gets half of them, and so on down; only a cell of under
// three corners is given up (null), which leaves a hole.
function inset(poly, k = 1) {
  let pts = poly.slice();
  for (let pass = 0; pass < 10 && pts.length >= 3; pass++) {
    const r = insetOnce(pts, k);
    if (r.out) return r.out;
    pts.splice(r.bad, 1);
  }
  return k > .05 ? inset(poly, k / 2) : null;
}

// The plates round the caster for these settings (o: Ground plus hill and beyond), every position in cells
// from the caster.
export function makeTerrain(o, seed = 1) {
  const reach = MapHalf + o.beyond + Margin, edgeAt = reach + Edge, inner = MapHalf + o.plate, hillR = o.hill + 1;
  const seeds = [];
  const put = (gx, gz, sp, k) => { const i = seeds.length; seeds.push({ x: gx + (hash(i, 1, seed + k) - .5) * sp * .8, z: gz + (hash(i, 2, seed + k) - .5) * sp * .8, sp }); };
  let gi = 0;
  for (let gz = -inner; gz <= inner + 1e-6; gz += o.plate) for (let gx = -inner; gx <= inner + 1e-6; gx += o.plate) {
    gi++;
    if (Math.hypot(gx, gz) < hillR || hash(gi, 8, seed) < o.drop) continue;
    put(gx, gz, o.plate, 0);
  }
  if (o.hill > 0) for (let gz = -hillR; gz <= hillR + 1e-6; gz += o.hillPlate) for (let gx = -hillR; gx <= hillR + 1e-6; gx += o.hillPlate)
    if (Math.hypot(gx, gz) < hillR) put(gx, gz, o.hillPlate, 3);
  for (let gz = -reach; gz <= reach + 1e-6; gz += o.outer) for (let gx = -reach; gx <= reach + 1e-6; gx += o.outer)
    if (Math.max(Math.abs(gx), Math.abs(gz)) > inner + o.outer * .6) put(gx, gz, o.outer, 7);

  const plates = seeds.map((s, i) => {
    const R = s.sp * Box;
    let poly = [{ x: s.x - R, z: s.z - R, nb: -1 }, { x: s.x - R, z: s.z + R, nb: -1 }, { x: s.x + R, z: s.z + R, nb: -1 }, { x: s.x + R, z: s.z - R, nb: -1 }];
    const near = [];
    seeds.forEach((n, j) => { if (j === i) return; const d = Math.hypot(n.x - s.x, n.z - s.z); if (d < 2 * R) near.push({ j, d }); });
    near.sort((a, b) => a.d - b.d);
    for (const { j, d } of near) {
      const n = seeds[j];
      poly = clipBy(poly, s, (n.x - s.x) / d, (n.z - s.z) / d, d / 2, j);
      if (poly.length < 3) return null;
    }
    // The outermost plates end at the world's edge instead of running on to their search box.
    for (const [nx, nz] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
      poly = clipBy(poly, { x: 0, z: 0 }, nx, nz, edgeAt, -1);
      if (poly.length < 3) return null;
    }
    // Each edge's crack: wider past the map, and varying edge to edge, the same width seen from both plates.
    const far = Math.max(Math.abs(s.x), Math.abs(s.z)) > MapHalf, half = o.gap * (far ? .8 : .5);
    poly.forEach(q => { q.g = q.nb < 0 ? half : half * (.4 + 1.2 * hash(Math.min(i, q.nb), Math.max(i, q.nb), seed + 5)); });
    const shrunk = inset(poly);
    if (!shrunk) return null;
    const xs = shrunk.map(q => q.x), zs = shrunk.map(q => q.z);
    return {
      poly: shrunk, sgn: area2(shrunk) > 0 ? 1 : -1, minX: Math.min(...xs), maxX: Math.max(...xs), minZ: Math.min(...zs), maxZ: Math.max(...zs),
      h: 0, tier: 0, shade: Math.floor(hash(i, 7, seed) * 4),
    };
  });

  // Heights. On the map: level, the hill's terraces, a small random step. Past it: the tiers, and the far
  // ridge along the north edge of the drawn ground.
  const tierW = o.beyond / o.tiers;
  plates.forEach((p, i) => {
    if (!p) return;
    const s = seeds[i];
    const onMap = p.minX < MapHalf && p.maxX > -MapHalf && p.minZ < MapHalf && p.maxZ > -MapHalf;
    if (onMap) {
      const d = Math.hypot(s.x, s.z), level = d < o.hill ? Math.floor(o.hillLevels * (1 - d / o.hill) + .5) : 0;
      p.h = level * o.hillStep + hash(i, 3, seed) * o.plateStep;
      return;
    }
    const ang = Math.atan2(s.z, s.x), beyond = Math.max(Math.abs(s.x), Math.abs(s.z)) - MapHalf;
    const wob = 2.2 * Math.sin(3 * ang + seed * .7) + 1.3 * Math.sin(7 * ang + 2.1);
    let tier = Math.max(1, Math.min(o.tiers, Math.ceil((beyond - 1 + wob) / tierW)));
    const south = p.maxZ < -MapHalf && p.minX < MapHalf && p.maxX > -MapHalf;
    if (south) {
      if (o.southTiers <= 0) return;
      tier = Math.min(tier, o.southTiers);
    }
    let mag = tier * o.tierStep + (hash(i, 4, seed) - .5) * o.tierStep * o.jitter;
    if (!south && s.z > edgeAt - o.ridgeBand) {
      const profile = .5 + .5 * Math.sin(s.x * .13 + seed) + .35 * Math.sin(s.x * .29 + 4.2) + .3 * hash(i, 9, seed);
      mag += clamp(profile / 1.65) * o.ridgeMax * (o.tierStep / 1.2);
    }
    p.h = south ? -mag : mag;
    p.tier = south ? -tier : tier;
  });
  const bottom = -(o.southTiers * o.tierStep * (1 + o.jitter / 2) + 1);

  // The plate under a point: the nearest seed, found through buckets one outer spacing wide.
  const cell = o.outer, buckets = new Map();
  const bk = (x, z) => `${Math.floor((x + reach) / cell)},${Math.floor((z + reach) / cell)}`;
  seeds.forEach((s, i) => { const k = bk(s.x, s.z); if (!buckets.has(k)) buckets.set(k, []); buckets.get(k).push(i); });
  const heightAt = (x, z) => {
    const bx = Math.floor((x + reach) / cell), bz = Math.floor((z + reach) / cell);
    let best = -1, bd = Infinity;
    for (let j = -1; j <= 1; j++) for (let i = -1; i <= 1; i++) {
      const list = buckets.get(`${bx + i},${bz + j}`);
      if (list) for (const k of list) { const s = seeds[k], d = (s.x - x) ** 2 + (s.z - z) ** 2; if (d < bd) { bd = d; best = k; } }
    }
    return best >= 0 && plates[best] ? plates[best].h : 0;
  };
  const key = JSON.stringify([o, seed]);
  return { seeds, plates, bottom, reach, edge: edgeAt, heightAt, key, seed, stats: { plates: plates.filter(Boolean).length, seeds: seeds.length } };
}

// ---- the bake ------------------------------------------------------------------------------------------
function pushPoly(out, pts, uvs) {
  const area = area2(pts);
  if (Math.abs(area) < 1e-5) return;
  const base = out.xz.length / 2;
  for (let i = 0; i < pts.length; i++) {
    const k = area > 0 ? pts.length - 1 - i : i;              // clockwise on screen, as plane10
    out.xz.push(pts[k].x, pts[k].z);
    out.uv.push(uvs[k][0], uvs[k][1]);
    if (i >= 2) out.tri.push(base, base + i - 1, base + i);
  }
}
// A thin strip along a polyline, in one swatch.
function pushLine(out, pts, width, sw) {
  for (let i = 1; i < pts.length; i++) {
    const a = pts[i - 1], b = pts[i], dx = b.x - a.x, dz = b.z - a.z, L = Math.hypot(dx, dz) || 1, nx = -dz / L * width / 2, nz = dx / L * width / 2;
    pushPoly(out, [{ x: a.x + nx, z: a.z + nz }, { x: b.x + nx, z: b.z + nz }, { x: b.x - nx, z: b.z - nz }, { x: a.x - nx, z: a.z - nz }], [sw, sw, sw, sw]);
  }
}
const bake = (name, b) => { const m = new Mesh(name); m.setFlat(b.xz, b.tri); m.uv = new Float32Array(b.uv); return m; };

// Every plate in painter's order (north first) into one mesh: faces, top, hairlines, lip; the shadows into
// another.
export function bakeTerrain(T, sun, key) {
  const ground = { xz: [], uv: [], tri: [] }, shadows = { xz: [], uv: [], tri: [] }, flat = [[.5, .5], [.5, .5], [.5, .5], [.5, .5]];
  const order = T.plates.map((p, i) => p ? i : -1).filter(i => i >= 0).sort((a, b) => T.seeds[b].z - T.seeds[a].z);
  for (const i of order) {
    const p = T.plates[i], lift = p.h * Lift, faceScreen = (p.h - T.bottom) * Lift, G = Math.min(faceScreen, GradLen), n = p.poly.length;
    const edges = p.poly.map((a, k) => {
      const b = p.poly[(k + 1) % n], dx = b.x - a.x, dz = b.z - a.z, L = Math.hypot(dx, dz) || 1;
      return { a, b, ox: dz / L * p.sgn, oz: -dx / L * p.sgn };            // outward normal
    });
    for (const e of edges) {
      if (e.oz < -.02) {
        const za = e.a.z + lift, zb = e.b.z + lift;
        pushPoly(ground, [{ x: e.a.x, z: za }, { x: e.b.x, z: zb }, { x: e.b.x, z: zb - G }, { x: e.a.x, z: za - G }],
          [[Grad.u, Grad.top], [Grad.u, Grad.top], [Grad.u, Grad.bot], [Grad.u, Grad.bot]]);
        if (faceScreen > G) pushPoly(ground, [{ x: e.a.x, z: za - G }, { x: e.b.x, z: zb - G }, { x: e.b.x, z: zb - faceScreen }, { x: e.a.x, z: za - faceScreen }],
          [SwFoot, SwFoot, SwFoot, SwFoot]);
      }
      const nb = e.a.nb >= 0 ? T.plates[e.a.nb] : null;
      if (!nb) continue;
      const hd = p.h - nb.h;
      if (hd > .04 && e.ox * sun.x + e.oz * sun.z > 0) {
        const lz = nb.h * Lift;
        pushPoly(shadows, [{ x: e.a.x, z: e.a.z + lz }, { x: e.b.x, z: e.b.z + lz },
          { x: e.b.x + sun.x * hd, z: e.b.z + lz + sun.z * hd }, { x: e.a.x + sun.x * hd, z: e.a.z + lz + sun.z * hd }], flat);
      }
    }
    // The top: a window of this plate's shade of the earth tile.
    const t = tileUV(p.shade), extent = Math.max(p.maxX - p.minX, p.maxZ - p.minZ), tile = Math.max(Tile, extent), span = extent / tile * t.span;
    const room = Math.max(0, t.span - span - .012);
    const u0 = t.u0 + .006 + hash(i, 5, T.seed) * room, v0 = t.v0 + .006 + hash(i, 6, T.seed) * room;
    pushPoly(ground, p.poly.map(q => ({ x: q.x, z: q.z + lift })), p.poly.map(q => [u0 + (q.x - p.minX) / tile * t.span, v0 + (q.z - p.minZ) / tile * t.span]));
    // Hairline cracks on the map's plates: short wandering lines from near the middle.
    if (p.tier === 0 && Math.max(Math.abs(T.seeds[i].x), Math.abs(T.seeds[i].z)) <= MapHalf + 3) {
      const cx = (p.minX + p.maxX) / 2, cz = (p.minZ + p.maxZ) / 2;
      for (let c = 0; c < Hairlines; c++) {
        const s0 = i * 31 + c * 7;
        let ang = hash(s0, 1, T.seed) * Math.PI * 2;
        const pts = [{ x: cx + (hash(s0, 2, T.seed) - .5) * extent * .3, z: cz + lift + (hash(s0, 3, T.seed) - .5) * extent * .3 }];
        for (let j = 1; j <= 4; j++) {
          ang += (hash(s0, 3 + j, T.seed) - .5) * 1.3;
          const step = .18 + hash(s0, 9 + j, T.seed) * .28, q = pts[j - 1];
          pts.push({ x: q.x + Math.cos(ang) * step, z: q.z + Math.sin(ang) * step });
        }
        pushLine(ground, pts, HairW, SwHair);
      }
    }
    for (const e of edges) {
      const nb = e.a.nb >= 0 ? T.plates[e.a.nb] : null;
      if (e.oz >= -.02 || !nb || p.h - nb.h < LipMin) continue;
      const w = LipW * (p.tier ? 1.5 : 1), ix = -e.ox * w, iz = -e.oz * w;
      pushPoly(ground, [{ x: e.a.x, z: e.a.z + lift }, { x: e.b.x, z: e.b.z + lift }, { x: e.b.x + ix, z: e.b.z + lift + iz }, { x: e.a.x + ix, z: e.a.z + lift + iz }],
        [SwLip, SwLip, SwLip, SwLip]);
    }
  }
  return { ground: bake(`ubw terrain ${key}`, ground), shadows: bake(`ubw terrain shadows ${key}`, shadows), vertices: (ground.xz.length + shadows.xz.length) / 2 };
}

// Kept per settings and per sun, three of each.
const terrains = new Map(), bakes = new Map();
export function terrain(o, seed = 1) {
  const key = JSON.stringify([o, seed]);
  if (!terrains.has(key)) { terrains.set(key, makeTerrain(o, seed)); if (terrains.size > 3) terrains.delete(terrains.keys().next().value); }
  return terrains.get(key);
}
export function baked(T, sun) {
  const key = `${T.key}|${sun.x.toFixed(3)},${sun.z.toFixed(3)}`;
  if (!bakes.has(key)) { bakes.set(key, bakeTerrain(T, sun, key)); if (bakes.size > 3) bakes.delete(bakes.keys().next().value); }
  return bakes.get(key);
}

// ---- drawing -------------------------------------------------------------------------------------------
// The crack floor under the plates, over the world's square only: past its edge the far haze colour
// (lib/ubw-pocket.js backstop) shows, as it does in the first world sketch.
export function terrainBase(c, edge) {
  draw(MeshPool.plane10, c.x, BaseLayer, c.z, 2 * edge, 2 * edge, 0, Base, solid);
}
// The plates round c, in the world's light; their cast shadows.
export function drawTerrain(b, c, tint, strength, alpha = 1) {
  draw(b.ground, c.x, TerrainLayer, c.z, 1, 1, 0, tint.withAlpha(alpha), atlas);
  draw(b.shadows, c.x, shadowLayer + .0015, c.z, 1, 1, 0, Black.withAlpha(.4 * strength / .32 * alpha), solid);
}
