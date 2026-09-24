// Shared drawing for Kamui's dimension (Obito kit): the map generator and the block field.
//
//   generate()   the pocket map. One main top in the middle, more tops grown off it (touching,
//                so walkable across) or set 1-2 cells apart (islands nobody can walk to), lower
//                blocks in the void between them, and blocks past the map edge. Everything is an
//                integer cell rectangle, so a C# port can copy it line by line.
//   bakeField()  every block as one mesh in painter's order (north first), textured from one
//                atlas: face, top and edge lines per block, so the whole field is one draw call.
//   atlasFor()   the atlas material for a palette (a PNG from make_kamui_textures.py). The atlas
//                holds the colours, so the mesh is the same for both palettes.
//   figure()     a stand-in pawn; heldMark() the ring under a held enemy; round() one round
//                sent into the dimension.
//
// Heights: every walkable top is at one level, so pawns stand exactly on their cells. Height is
// drawn as a shift north (Lift 0.60 per cell up), so a block only rises above that level where
// nothing walks behind it: past the north, east and west map edges. Lower blocks sit in the gaps
// and past the edges. Blocks never overlap on the ground, so drawing them north first, lower
// first on a tie, gives the right cover everywhere; every block is a pillar out of the abyss, so
// its south face runs down past the bottom of the screen (a 2-cell fade, then solid void).
import { AltitudeLayer, Color, MaterialPool, Mathf, Mesh, MeshPool, Meshes, ShaderDatabase } from '../../js/engine.js';
import { hash } from '../../js/standins.js';
import { draw } from './six-paths-solid.js';
import { Lift, sprite, circle, soft } from './six-paths-impact.js';

export { Lift, draw };
export const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const plane = MeshPool.plane10;
const disc = Meshes.disc(32, 'kamui disc');
const white = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);

// ---- palettes -------------------------------------------------------------------------------
// Measured from the references (browser research 2026-09-24):
//   fight: anime, Kakashi vs Obito inside the dimension, seen from above (Crunchyroll clip
//          r2vDD9FFCVE 0:02-0:06): tops (151,171,188), faces (80,95,105), void (12,20,24).
//   still: Narutopedia "Kamui's dimension" (Kamui_Dimension.png): tops (85,100,122), sides near
//          black, void (7,13,20), thin pale edges (113,131,155).
const rgb = (r, g, b) => new Color(r / 255, g / 255, b / 255);
export const Palettes = {
  fight: { top: rgb(151, 171, 188), edge: rgb(196, 222, 244), face: rgb(80, 95, 105), void: rgb(10, 16, 22) },
  still: { top: rgb(85, 100, 122), edge: rgb(113, 131, 155), face: rgb(24, 32, 40), void: rgb(7, 13, 20) },
};
export const Obito = new Color(.16, .14, .20), Mask = new Color(.93, .50, .14);
export const Ally = new Color(.42, .62, .40), Enemy = new Color(.55, .38, .27), Skin = new Color(.83, .70, .54);
export const Held = new Color(1, .88, .45), Round = new Color(1, .93, .62);

// ---- altitudes ------------------------------------------------------------------------------
// The field replaces what the pocket map's terrain would show; pawns, rounds and marks go on top.
const at = n => AltitudeLayer[n].AltitudeFor();
export const L = {
  back: at('BelowTerrain'), field: at('Terrain') + .02, fade: at('Terrain') + .05, mark: at('Filth') + .01,
  shadow: at('Shadows'), pawn: at('Pawn'), round: at('Projectile'), fx: at('MoteOverhead'),
};

// ---- random ---------------------------------------------------------------------------------
// Mulberry32, the same as the Infinity Castle generator, so a port can copy it exactly.
export function rng(seed) {
  let a = (Math.imul(seed | 0, 2654435761) ^ 0x9e3779b9) >>> 0;
  return () => {
    a = (a + 0x6D2B79F5) >>> 0; let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}
const int = (R, lo, hi) => lo + Math.floor(R() * (hi - lo + 1));

// ---- the generator --------------------------------------------------------------------------
// Numbers agreed as placeholders on 2026-09-24; in game they are fields on the map generator def.
export const Rule = {
  size: 48, margin: 2,                  // map side; cells of void kept inside the map edge
  mainW: 14, mainH: 10,                 // the main top, where Obito and allies arrive
  min: 3, max: 9, islandMax: 6,         // other tops, cells a side (islands stay small)
  touch: .65, gapMax: 2,                // share of new tops placed touching their parent; widest gap
  mainParent: .8,                       // share of new tops grown off the main top's group
  cover: .55,                           // walkable share of the map
  islands: 6, islandsMax: 10, islandCells: 45,
  gapFill: .4, gapDepth: 3,             // lower blocks in the void inside the map, 1-3 cells down
  band: 20, bandFill: .8, tall: .3, tallMax: 8, deepMax: 6,   // blocks past the map edge
};

export function generate(seed, { size = Rule.size, cover = Rule.cover, islands = Rule.islands } = {}) {
  const R = rng(seed), N = size, M = Rule.margin;
  const occ = new Int32Array(N * N).fill(-1);            // top id per cell, -1 = void
  const tops = [], group = [], groupCells = new Map();
  const eachCell = (b, f) => { for (let z = b.z; z < b.z + b.h; z++) for (let x = b.x; x < b.x + b.w; x++) f(x, z); };
  const inside = b => b.x >= M && b.z >= M && b.x + b.w <= N - M && b.z + b.h <= N - M;
  const free = b => { for (let z = b.z; z < b.z + b.h; z++) for (let x = b.x; x < b.x + b.w; x++) if (occ[z * N + x] >= 0) return false; return true; };
  // Groups of the tops in the ring of cells around b, corners included (a corner touch looks
  // joined, and pathing does not cut a corner past void, so it is kept inside one group).
  const ringGroups = b => {
    const s = new Set();
    for (let z = b.z - 1; z <= b.z + b.h; z++) for (let x = b.x - 1; x <= b.x + b.w; x++) {
      if (x < 0 || z < 0 || x >= N || z >= N) continue;
      if (x >= b.x && x < b.x + b.w && z >= b.z && z < b.z + b.h) continue;
      const id = occ[z * N + x];
      if (id >= 0) s.add(group[id]);
    }
    return s;
  };
  const place = (b, g) => {
    b.id = tops.length; b.group = g; b.level = 0;
    b.shade = Math.floor(hash(b.id, 5, seed) * 4);
    tops.push(b); group.push(g);
    eachCell(b, (x, z) => { occ[z * N + x] = b.id; });
    groupCells.set(g, (groupCells.get(g) ?? 0) + b.w * b.h);
  };

  const main = { x: Math.floor((N - Rule.mainW) / 2), z: Math.floor((N - Rule.mainH) / 2), w: Rule.mainW, h: Rule.mainH };
  place(main, 0);
  let walk = main.w * main.h, nextGroup = 1, islandCount = 0;
  const target = cover * N * N;
  for (let tries = 0; tries < 6000 && walk < target; tries++) {
    const fromMain = islandCount === 0 || R() < Rule.mainParent;
    const pool = tops.filter(t => (t.group === 0) === fromMain);
    const parent = pool[Math.floor(R() * pool.length)];
    const side = int(R, 0, 3);                                    // 0 east, 1 north, 2 west, 3 south
    const gap = R() < Rule.touch ? 0 : int(R, 1, Rule.gapMax);
    const hi = gap > 0 ? Rule.islandMax : Rule.max;
    const w = int(R, Rule.min, hi), h = int(R, Rule.min, hi);
    const ov = gap === 0 ? 2 : 1;                                 // a touching top shares 2+ cells of edge
    let x, z;
    if (side % 2 === 0) { x = side === 0 ? parent.x + parent.w + gap : parent.x - gap - w; z = int(R, parent.z - h + ov, parent.z + parent.h - ov); }
    else { z = side === 1 ? parent.z + parent.h + gap : parent.z - gap - h; x = int(R, parent.x - w + ov, parent.x + parent.w - ov); }
    const b = { x, z, w, h };
    if (!inside(b) || !free(b)) continue;
    const ring = ringGroups(b);
    if (gap > 0) {
      // A new island: nothing within one cell of it.
      if (ring.size > 0 || islandCount >= Rule.islandsMax) continue;
      place(b, nextGroup++); islandCount++;
    } else {
      // Grows its parent's group and touches no other group, so islands stay islands.
      if (ring.size !== 1 || !ring.has(parent.group)) continue;
      if (parent.group !== 0 && groupCells.get(parent.group) + w * h > Rule.islandCells) continue;
      place(b, parent.group);
    }
    walk += w * h;
  }
  // Too few islands on a crowded seed: drop small ones into whatever void is left.
  for (let tries = 0; tries < 3000 && islandCount < islands; tries++) {
    const w = int(R, Rule.min, Rule.islandMax - 1), h = int(R, Rule.min, Rule.islandMax - 1);
    const b = { x: int(R, M, N - M - w), z: int(R, M, N - M - h), w, h };
    if (!free(b) || ringGroups(b).size > 0) continue;
    place(b, nextGroup++); islandCount++; walk += w * h;
  }

  // Lower blocks in the void inside the map. Scanned north to south, west to east; each one
  // grows east then south over void that no block covers yet.
  const low = new Uint8Array(N * N), lower = [];
  for (let z = N - 1; z >= 0; z--) for (let x = 0; x < N; x++) {
    const i = z * N + x;
    if (occ[i] >= 0 || low[i] || R() > Rule.gapFill) continue;
    const wMax = int(R, 2, 6), hMax = int(R, 2, 6);
    let w = 0;
    while (w < wMax && x + w < N && occ[z * N + x + w] < 0 && !low[z * N + x + w]) w++;
    let h = 0;
    for (; h < hMax && z - h >= 0; h++) {
      let ok = true;
      for (let k = 0; k < w; k++) { const j = (z - h) * N + x + k; if (occ[j] >= 0 || low[j]) { ok = false; break; } }
      if (!ok) break;
    }
    const b = { x, z: z - h + 1, w, h, level: -int(R, 1, Rule.gapDepth) };
    eachCell(b, (cx, cz) => { low[cz * N + cx] = 1; });
    lower.push(b);
  }

  // Past the map edge. Blocks may rise above the walkable level only where nothing inside the map
  // is behind them: north of the map, or beside it east and west. South of it they only go down.
  const B = Rule.band, S = N + 2 * B, taken = new Uint8Array(S * S), outside = [];
  const inMap = (x, z) => x >= 0 && z >= 0 && x < N && z < N;
  const isTaken = (x, z) => inMap(x, z) || taken[(z + B) * S + x + B];
  for (let z = N + B - 1; z >= -B; z--) for (let x = -B; x < N + B; x++) {
    if (isTaken(x, z) || R() > Rule.bandFill) continue;
    const wMax = int(R, 3, 9), hMax = int(R, 3, 9);
    let w = 0;
    while (w < wMax && x + w < N + B && !isTaken(x + w, z)) w++;
    let h = 0;
    for (; h < hMax && z - h >= -B; h++) {
      let ok = true;
      for (let k = 0; k < w; k++) if (isTaken(x + k, z - h)) { ok = false; break; }
      if (!ok) break;
    }
    const b = { x, z: z - h + 1, w, h };
    const southOfMap = b.x < N && b.x + b.w > 0 && b.z < N;
    b.level = !southOfMap && R() < Rule.tall ? int(R, 1, Rule.tallMax) : -int(R, 1, Rule.deepMax);
    for (let cz = b.z; cz < b.z + b.h; cz++) for (let cx = b.x; cx < b.x + b.w; cx++) taken[(cz + B) * S + cx + B] = 1;
    outside.push(b);
  }

  // Where things arrive: Obito and allies at the middle of the main top, each held enemy on its
  // own island (the middle cell of the island's first top).
  const mouth = { x: main.x + Math.floor(main.w / 2), z: main.z + Math.floor(main.h / 2) };
  const islandTops = [];
  for (let g = 1; g < nextGroup; g++) islandTops.push(tops.find(t => t.group === g));
  const landings = islandTops.map(t => ({ x: t.x + Math.floor(t.w / 2), z: t.z + Math.floor(t.h / 2) }));
  const walkCells = [];
  for (let z = 0; z < N; z++) for (let x = 0; x < N; x++) if (occ[z * N + x] >= 0) walkCells.push({ x, z });

  return {
    seed, size: N, tops, lower, outside, occ, mouth, landings, walkCells,
    stats: { tops: tops.length, walkable: walk, cover: walk / (N * N), islands: islandCount, mainCells: groupCells.get(0), lower: lower.length, outside: outside.length },
  };
}

// ---- the atlas ------------------------------------------------------------------------------
// Textures/RimArt/Kamui/Atlas.png (the "fight" palette) and AtlasStill.png, made by
// make_kamui_textures.py, which holds the colours and formulas (tuned here in the lab first).
// 8 x 8 cells of 64 px. Row 0: walkable tops in 4 shades (cols 0-3), a top above the walkable
// level (col 4). Row 1: tops 1-6 cells down (cols 0-5). Rows 2-4 by level column (col 0 walkable,
// col 1 above, cols 2-7 one to six cells down): 2 south faces (face colour at the top of the cell
// fading to void at the bottom), 3 edge lines (solid), 4 face corner lines (fading like the face).
// Row 5: col 0 void, col 1 the patch tile. The mesh samples each cell inside a 5 px border.
const AtlasN = 8, CellPx = 64, SidePx = AtlasN * CellPx, Pad = 5;
const colOfLevel = level => level === 0 ? 0 : level > 0 ? 1 : 1 + Math.min(6, -level);
const AtlasPaths = { fight: 'RimArt/Kamui/Atlas', still: 'RimArt/Kamui/AtlasStill' };
const atlases = new Map();
export function atlasFor(name) {
  if (!atlases.has(name)) atlases.set(name, MaterialPool.MatFrom(AtlasPaths[name], ShaderDatabase.Transparent));
  return atlases.get(name);
}

// A cell's uv rectangle, v up (v 1 is the top row of the image), inside its padding.
function cellUV(col, row) {
  const u0 = (col * CellPx + Pad) / SidePx, u1 = ((col + 1) * CellPx - Pad) / SidePx;
  const vTop = 1 - (row * CellPx + Pad) / SidePx, vBot = 1 - ((row + 1) * CellPx - Pad) / SidePx;
  return { u0, u1, vTop, vBot, uc: (u0 + u1) / 2, vc: (vTop + vBot) / 2 };
}

// ---- the baked field ------------------------------------------------------------------------
export const FaceFade = 2, FaceTail = 14, EdgeW = .06, PatchTile = 6;

// Painter's order: north first; on a tie, lower first, then west first.
export function paintOrder(blocks) {
  return blocks.slice().sort((a, b) => (b.z - a.z) || (a.level - b.level) || (a.x - b.x));
}

// All blocks as one mesh in map-local cells (0..size); draw it at the map's corner.
export function bakeField(blocks, key) {
  const xz = [], uv = [], tri = [];
  // f: the part of the cell to show, as fractions west-east (u) and south-north (v); full by default.
  const quad = (x0, z0, x1, z1, mode, c, f = { u0: 0, u1: 1, v0: 0, v1: 1 }) => {
    if (x1 <= x0 || z1 <= z0) return;
    const n = xz.length / 2;
    xz.push(x0, z0, x0, z1, x1, z1, x1, z0);                          // SW, NW, NE, SE like plane10
    if (mode === 'solid') uv.push(c.uc, c.vc, c.uc, c.vc, c.uc, c.vc, c.uc, c.vc);
    else if (mode === 'down') uv.push(c.uc, c.vBot, c.uc, c.vTop, c.uc, c.vTop, c.uc, c.vBot);
    else {
      const ua = lerp(c.u0, c.u1, f.u0), ub = lerp(c.u0, c.u1, f.u1), va = lerp(c.vBot, c.vTop, f.v0), vb = lerp(c.vBot, c.vTop, f.v1);
      uv.push(ua, va, ua, vb, ub, vb, ub, va);
    }
    tri.push(n, n + 1, n + 2, n, n + 2, n + 3);
  };
  // Patches on a walkable top: whole tiles from the top's north-west corner; the last row and
  // column are cut to the top and show the matching part of their tile.
  const patches = (b, x0, z1) => {
    for (let tz = 0; tz < b.h; tz += PatchTile) for (let tx = 0; tx < b.w; tx += PatchTile) {
      const w = Math.min(PatchTile, b.w - tx), h = Math.min(PatchTile, b.h - tz);
      quad(x0 + tx, z1 - tz - h, x0 + tx + w, z1 - tz, 'part', cellUV(1, 5),
        { u0: 0, u1: w / PatchTile, v0: 1 - h / PatchTile, v1: 1 });
    }
  };
  const voidCell = cellUV(0, 5);
  for (const b of paintOrder(blocks)) {
    const lc = colOfLevel(b.level), lift = b.level * Lift;
    const x0 = b.x, x1 = b.x + b.w, z0 = b.z + lift, z1 = b.z + b.h + lift;
    const fade = FaceFade + Math.max(0, lift);                         // a tall block's face darkens over its height too
    quad(x0, z0 - fade - FaceTail, x1, z0 - fade, 'solid', voidCell);
    quad(x0, z0 - fade, x1, z0, 'down', cellUV(lc, 2));
    quad(x0, z0 - fade, x0 + EdgeW, z0, 'down', cellUV(lc, 4));
    quad(x1 - EdgeW, z0 - fade, x1, z0, 'down', cellUV(lc, 4));
    const top = b.level === 0 ? cellUV(b.shade ?? 0, 0) : b.level > 0 ? cellUV(4, 0) : cellUV(Math.min(6, -b.level) - 1, 1);
    quad(x0, z0, x1, z1, 'full', top);
    if (b.level === 0) patches(b, x0, z1);
    const edge = cellUV(lc, 3);
    quad(x0, z0, x1, z0 + EdgeW, 'solid', edge);
    quad(x0, z1 - EdgeW, x1, z1, 'solid', edge);
    quad(x0, z0, x0 + EdgeW, z1, 'solid', edge);
    quad(x1 - EdgeW, z0, x1, z1, 'solid', edge);
  }
  const m = new Mesh(`kamui field ${key}`);
  m.setFlat(xz, tri);
  m.uv = new Float32Array(uv);
  m.quads = tri.length / 6;
  return m;
}

// ---- void and the edge of the map -----------------------------------------------------------
const fadeMat = MaterialPool.MatFrom('RimArt/Kamui/Fade', ShaderDatabase.Transparent);   // alpha rising west to east

export function drawVoid(corner, size, pal) {
  const c = { x: corner.x + size / 2, z: corner.z + size / 2 };
  draw(plane, c.x, L.back, c.z, size + 2 * Rule.band + 80, size + 2 * Rule.band + 80, 0, pal.void);
}
// The blocks past the edge go dark toward the band's outer side: on each side a strip whose alpha
// rises away from the map over 90 % of the band, then solid void to 12 cells past the band, which
// also covers tall tops lifted past it (up to 4.8 cells) and low tops dropped past it (up to 3.6).
// East and west strips run the full height and cover the corners.
export function drawEdgeFade(corner, size, pal) {
  const B = Rule.band, N = size, col = pal.void, ramp = B * .9, solid = B - ramp + 12;
  const full = N + 2 * (ramp + solid), mid = { x: corner.x + N / 2, z: corner.z + N / 2 };
  draw(plane, corner.x + N + ramp / 2, L.fade, mid.z, ramp, full, 0, col, fadeMat);
  draw(plane, corner.x - ramp / 2, L.fade, mid.z, ramp, full, 180, col, fadeMat);
  draw(plane, corner.x + N + ramp + solid / 2, L.fade, mid.z, solid, full, 0, col);
  draw(plane, corner.x - ramp - solid / 2, L.fade, mid.z, solid, full, 0, col);
  draw(plane, mid.x, L.fade, corner.z + N + ramp / 2, ramp, N, -90, col, fadeMat);
  draw(plane, mid.x, L.fade, corner.z - ramp / 2, ramp, N, 90, col, fadeMat);
  draw(plane, mid.x, L.fade, corner.z + N + ramp + solid / 2, N, solid, 0, col);
  draw(plane, mid.x, L.fade, corner.z - ramp - solid / 2, N, solid, 0, col);
}

// ---- stand-ins ------------------------------------------------------------------------------
export function figure(pos, body, head, sun, strength, { downed = false } = {}) {
  sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, .85, .4, new Color(0, 0, 0, strength), soft, L.shadow);
  if (downed) {
    draw(disc, pos.x + .05, L.pawn, pos.z + .12, .32, .2, 0, body);
    draw(disc, pos.x - .34, L.pawn + .002, pos.z + .14, .16, .17, 0, head);
    return;
  }
  draw(disc, pos.x, L.pawn, pos.z + .18, .22, .32, 0, body);
  draw(disc, pos.x, L.pawn + .002, pos.z + .58, .16, .17, 0, head);
}
// A held enemy: stunned the whole time it is inside. Stand-in for the game's stun marker.
export function heldMark(pos, s) {
  circle(pos, .46, .55 + .2 * Math.sin(s * 4), L.mark, Held);
}
// One round sent in: it appears on the map edge (stand-in ring for the entry, not designed yet),
// crosses to its point and lands there.
export function round(from, to, age, speed) {
  if (age < 0) return;
  const dx = to.x - from.x, dz = to.z - from.z, dist = Math.hypot(dx, dz), fly = dist / speed;
  if (age < .3) circle(from, .2 + age * 1.2, .8 * (1 - age / .3), L.fx, Round);
  if (age < fly) {
    const u = age / fly, ang = -Math.atan2(dz, dx) * 180 / Math.PI;
    draw(plane, from.x + dx * u, L.round, from.z + dz * u, .7, .08, ang, Round, white);
  } else if (age < fly + .25) {
    const k = (age - fly) / .25;
    sprite(to, .3 + .5 * k, .3 + .5 * k, new Color(.8, .82, .85, .5 * (1 - k)), soft, L.fx);
  }
}
