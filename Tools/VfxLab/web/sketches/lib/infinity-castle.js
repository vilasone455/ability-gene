// Shared drawing for the Infinity Castle kit (Nakime's biwa and the pocket-map castle). Every
// Infinity Castle sketch draws through this file, so a C# port can make one class of each part.
//
//   generate()     the room generator. Rooms grow off each other, touch only where they join and
//                  keep 2 cells of void from every other room, so the castle starts as a tree of
//                  rooms hanging in the void. A doorway sits where two rooms' walls touch along 3 or
//                  more cells. local() builds a hand-placed castle with the same rules.
//   drawRooms()    floors, walls, lanterns. In game these are the pocket map's terrain and
//                  buildings and RimWorld draws them; the mod draws a room itself only while it
//                  moves (Shift, Crush), with these same meshes.
//   drawVoid()     the black under everything, drawn-only rooms at two depths below it (dimmer,
//                  smaller, drifting, some turned), and the see-through void terrain over them.
//   floorDoor()    the door that opens in the floor under a pawn to take it in or bring it out.
//   wallDoor()     a fusuma doorway in a wall cell: paper leaves that slide into the wall, and
//                  the seal bar.
//   nakime()       the caster with the biwa and the bachi, seated (castle) or standing (home map);
//   strum()        the ring each strum sends out; roomFlash() marks the room a command lands on.
//
// Cell grid: a castle is built in integer cells. castle.at(x, z) is the world position of the
// lower-left corner of cell (x, z); a room { x, z, w, h } covers cells x..x+w-1, z..z+h-1 with its
// wall ring included (so a 9 x 9 room has a 7 x 7 floor). Everything drawn here is level (floors,
// frames, rings) or a stand-in pawn, so nothing needs a per-facing method.
import { AltitudeLayer, Color, MaterialPool, Mathf, Mesh, MeshPool, Meshes, ShaderDatabase } from '../../js/engine.js';
import { draw } from './six-paths-solid.js';
import { Y, Floor, Lift, sprite, band, circle, soft, glow, rand } from './six-paths-impact.js';

export { Y, Floor, Lift, sprite, band, circle, soft, glow, rand, draw };
export const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
export const easeOut = x => 1 - Math.pow(1 - clamp(x), 3);
export const bump = x => (x > 0 && x < 1) ? Math.sin(x * Math.PI) : 0;
const TAU = Math.PI * 2;

const plane = MeshPool.plane10;
export const disc = Meshes.disc(32, 'castle disc');
export const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const additive = MaterialPool.MatFrom('white', ShaderDatabase.MoteGlow);

// ---- palette --------------------------------------------------------------------------------
export const Void = new Color(.030, .024, .045), VoidDeep = new Color(.010, .008, .016);
export const WoodFloor = new Color(.40, .25, .14), WoodSeam = new Color(.22, .13, .07);
export const Tatami = new Color(.58, .55, .34), TatamiShade = new Color(.52, .49, .29), Heri = new Color(.11, .13, .09);
export const WallWood = new Color(.14, .08, .045), WallTop = new Color(.30, .18, .10), Lacquer = new Color(.52, .12, .07);
export const Paper = new Color(.88, .82, .68), Lattice = new Color(.22, .13, .07), Gold = new Color(.78, .60, .28);
export const Lantern = new Color(1, .60, .24), LanternPaper = new Color(1, .80, .52), LanternCore = new Color(1, .92, .70);
export const Strum = new Color(1, .93, .78);                  // strum rings and command flashes
export const Enemy = new Color(.55, .38, .27), Ally = new Color(.42, .62, .40), Skin = new Color(.83, .70, .54);
export const Kimono = new Color(.57, .48, .67), KimonoDark = new Color(.37, .30, .46), Obi = new Color(.20, .15, .27);
export const Hair = new Color(.05, .04, .06), Eye = new Color(.95, .93, .86);
export const BiwaWood = new Color(.46, .25, .11), BiwaFace = new Color(.66, .44, .23), BiwaDark = new Color(.17, .09, .04), Bachi = new Color(.94, .91, .82);
export const Dust = new Color(.62, .52, .40), Blood = new Color(.50, .07, .06);

// ---- altitudes ------------------------------------------------------------------------------
// The castle stand-ins use the game's layers: the void and the depth rooms under the terrain,
// floors as terrain, doors and walls as buildings, pawns, then effects.
const at = n => AltitudeLayer[n].AltitudeFor();
export const L = {
  back: at('BelowTerrain'), depth: at('BelowTerrain') + .05, fog: at('Terrain'), floor: at('Terrain') + .03,
  door: at('Filth') + .02, shadow: at('Shadows'), wall: at('Building'), pawn: at('Pawn'), fx: Y,
};

// ---- random ---------------------------------------------------------------------------------
// Mulberry32. The port uses Rand.PushState(seed), so its layouts differ from these; the rules
// are the same.
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
// Sizes include the wall ring. The agreed range is 5 x 5 to 17 x 13 (either way round).
export const Rule = { size: 100, margin: 2, gap: 2, minDoor: 3, biwa: 9 };
const Kinds = [
  ['tatami', 34, (R) => [int(R, 5, 11), int(R, 5, 11)]],
  ['corridor', 30, (R, flat) => flat ? [int(R, 11, 17), 5] : [5, int(R, 11, 17)]],
  ['hall', 12, (R, flat) => flat ? [int(R, 13, 17), int(R, 11, 13)] : [int(R, 11, 13), int(R, 13, 17)]],
  ['stair', 24, (R, flat) => flat ? [int(R, 9, 13), int(R, 7, 9)] : [int(R, 7, 9), int(R, 9, 13)]],
];
const kindTotal = Kinds.reduce((s, k) => s + k[1], 0);
function pickKind(R) { let r = R() * kindTotal; for (const k of Kinds) if ((r -= k[1]) < 0) return k; return Kinds[0]; }

// Door-able wall cells exclude the corners. span() is the length of the stretch where two
// touching rooms can share a doorway; 0 when the rooms do not touch.
export function contact(a, b) {
  if (a.x + a.w === b.x || b.x + b.w === a.x) {
    const lo = Math.max(a.z, b.z) + 1, hi = Math.min(a.z + a.h, b.z + b.h) - 1;
    if (hi - lo >= 1) return { axis: 'z', lo, hi, wx: a.x + a.w === b.x ? [a.x + a.w - 1, b.x] : [a.x, b.x + b.w - 1] };
  }
  if (a.z + a.h === b.z || b.z + b.h === a.z) {
    const lo = Math.max(a.x, b.x) + 1, hi = Math.min(a.x + a.w, b.x + b.w) - 1;
    if (hi - lo >= 1) return { axis: 'x', lo, hi, wz: a.z + a.h === b.z ? [a.z + a.h - 1, b.z] : [a.z, b.z + b.h - 1] };
  }
  return null;
}
const span = (a, b) => { const c = contact(a, b); return c ? c.hi - c.lo : 0; };
const near = (a, b, gap) => a.x - gap < b.x + b.w && b.x - gap < a.x + a.w && a.z - gap < b.z + b.h && b.z - gap < a.z + a.h;

// Every doorway: two wall cells side by side, one in each room. `cells` are [x, z] per room.
export function doorsOf(rooms) {
  const out = [];
  for (let i = 0; i < rooms.length; i++) for (let j = i + 1; j < rooms.length; j++) {
    const c = contact(rooms[i], rooms[j]);
    if (!c || c.hi - c.lo < Rule.minDoor) continue;
    const mid = c.lo + Math.floor((c.hi - c.lo - 1) / 2);
    const cells = c.axis === 'z' ? [[c.wx[0], mid], [c.wx[1], mid]] : [[mid, c.wz[0]], [mid, c.wz[1]]];
    out.push({ a: rooms[i].id, b: rooms[j].id, axis: c.axis, cells, key: `${rooms[i].id}-${rooms[j].id}` });
  }
  return out;
}

// Steps from the biwa room through doorways (breadth first). Unreachable rooms get Infinity.
export function distances(rooms, doors, from = 0) {
  const d = new Map(rooms.map(r => [r.id, Infinity])), queue = [from];
  d.set(from, 0);
  while (queue.length) {
    const r = queue.shift();
    for (const dr of doors) {
      const o = dr.a === r ? dr.b : dr.b === r ? dr.a : null;
      if (o !== null && d.get(o) === Infinity) { d.set(o, d.get(r) + 1); queue.push(o); }
    }
  }
  return d;
}

export function generate(seed, count = 38) {
  const R = rng(seed), { size, margin: M, gap } = Rule, rooms = [];
  rooms.push({ id: 0, x: M, z: Math.round(size / 2 - Rule.biwa / 2 + (R() - .5) * 30), w: Rule.biwa, h: Rule.biwa, kind: 'biwa' });
  for (let tries = 0; rooms.length < count && tries < 9000; tries++) {
    // Newer rooms are picked a little more often, so the castle grows outward.
    const parent = rooms[Math.min(rooms.length - 1, Math.floor(Math.pow(R(), .75) * rooms.length))];
    const side = int(R, 0, 3), flat = side % 2 === 0;          // 0 east, 1 north, 2 west, 3 south
    const [kind, , dims] = pickKind(R), [w, h] = dims(R, flat);
    let x, z;
    if (flat) { x = side === 0 ? parent.x + parent.w : parent.x - w; z = int(R, parent.z + 5 - h, parent.z + parent.h - 5); }
    else { z = side === 1 ? parent.z + parent.h : parent.z - h; x = int(R, parent.x + 5 - w, parent.x + parent.w - 5); }
    const room = { id: rooms.length, x, z, w, h, kind };
    if (x < M || z < M || x + w > size - M || z + h > size - M) continue;
    if (span(parent, room) < Rule.minDoor) continue;
    if (rooms.some(r => r !== parent && near(r, room, gap))) continue;
    rooms.push(room);
  }
  return build(rooms, { size, seed });
}

// A hand-placed castle for the command sketches: [kind, x, z, w, h] per room, the first is the
// biwa room. Doorways follow the same contact rule.
export function local(specs, seed = 0) {
  return build(specs.map(([kind, x, z, w, h], id) => ({ id, kind, x, z, w, h })), { seed });
}

function build(rooms, { size = 0, seed = 0 }) {
  const doors = doorsOf(rooms), dist = distances(rooms, doors);
  return { rooms, doors, dist, size, seed };
}

// Rooms for n arrivals: each a different room at least `min` doorways from the biwa room,
// spread by a hash so the same seed picks the same rooms. Falls back to closer rooms if short.
export function arrivalRooms(castle, n, min = 3) {
  const order = castle.rooms.filter(r => r.kind !== 'biwa').sort((a, b) => rand(a.id * 7 + castle.seed) - rand(b.id * 7 + castle.seed));
  for (let m = min; m >= 1; m--) {
    const pick = order.filter(r => castle.dist.get(r.id) >= m && castle.dist.get(r.id) < Infinity);
    if (pick.length >= n || m === 1) return pick.slice(0, n);
  }
  return [];
}

// Shift: how far a room slides in steps of one cell (dx, dz one of -1, 0, 1) before its next step
// would overlap another room, up to `max`. Rooms may touch; sliding along a touching room is fine.
export const overlaps = (a, b) => a.x < b.x + b.w && b.x < a.x + a.w && a.z < b.z + b.h && b.z < a.z + a.h;
export function slideDistance(rooms, id, dx, dz, max) {
  const r = rooms.find(q => q.id === id);
  let d = 0;
  while (d < max) {
    const next = { ...r, x: r.x + dx * (d + 1), z: r.z + dz * (d + 1) };
    if (rooms.some(q => q.id !== id && overlaps(next, q))) return { d, blocked: true };
    d++;
  }
  return { d, blocked: false };
}
// The same castle with one room moved (doorways worked out again).
export function moved(castle, id, dx, dz) {
  return build(castle.rooms.map(r => r.id === id ? { ...r, x: r.x + dx, z: r.z + dz } : r), { size: castle.size, seed: castle.seed });
}
// Doorways in `before` that are gone in `after` (the rooms parted, or the stretch moved), and the
// new ones. A doorway is the same when both its rooms and both its cells are the same.
export function doorChanges(before, after) {
  const same = (a, b) => a.key === b.key && a.cells.every((c, i) => c[0] === b.cells[i][0] && c[1] === b.cells[i][1]);
  return {
    kept: before.doors.filter(d => after.doors.some(e => same(d, e))),
    broken: before.doors.filter(d => !after.doors.some(e => same(d, e))),
    made: after.doors.filter(d => !before.doors.some(e => same(d, e))),
  };
}

// ---- geometry helpers -----------------------------------------------------------------------
// World position of a cell corner, a room's centre, and a cell's centre, for a castle whose cell
// (0, 0) has its lower-left corner at `base`.
export const corner = (base, x, z) => ({ x: base.x + x, z: base.z + z });
export const centreOf = (base, r, off = { x: 0, z: 0 }) => ({ x: base.x + r.x + r.w / 2 + off.x, z: base.z + r.z + r.h / 2 + off.z });
export const cellCentre = (base, x, z) => ({ x: base.x + x + .5, z: base.z + z + .5 });
export const inRoom = (r, x, z) => x >= r.x && x < r.x + r.w && z >= r.z && z < r.z + r.h;

// ---- room meshes ----------------------------------------------------------------------------
// Built once per kind and size, in cells about the room's centre, so the same mesh draws a
// room at rest, sliding, or small and turned at depth.
const built = new Map();
function rects(key, fill) {
  let m = built.get(key);
  if (m) return m;
  const v = [], t = [];
  fill((x0, z0, x1, z1) => { const n = v.length / 2; v.push(x0, z0, x0, z1, x1, z1, x1, z0); t.push(n, n + 1, n + 2, n, n + 2, n + 3); });
  m = new Mesh(key); m.setFlat(v, t); built.set(key, m);
  return m;
}
const paperWalls = kind => kind === 'tatami' || kind === 'corridor';

function roomParts(r) {
  const { w, h, kind } = r, k = `castle ${kind} ${w}x${h}`, X0 = -w / 2, Z0 = -h / 2, X1 = w / 2, Z1 = h / 2;
  const fx0 = X0 + 1, fz0 = Z0 + 1, fx1 = X1 - 1, fz1 = Z1 - 1;       // the floor inside the walls
  const parts = {};
  parts.walls = rects(`${k} walls`, q => { q(X0, Z0, X1, Z0 + 1); q(X0, Z1 - 1, X1, Z1); q(X0, Z0 + 1, X0 + 1, Z1 - 1); q(X1 - 1, Z0 + 1, X1, Z1 - 1); });
  parts.lip = rects(`${k} lip`, q => { const e = .09; q(fx0, fz0 - e, fx1, fz0); q(fx0, fz1, fx1, fz1 + e); q(fx0 - e, fz0, fx0, fz1); q(fx1, fz0, fx1 + e, fz1); });
  parts.edge = rects(`${k} edge`, q => { const e = .07; q(X0, Z0, X1, Z0 + e); q(X0, Z1 - e, X1, Z1); q(X0, Z0, X0 + e, Z1); q(X1 - e, Z0, X1, Z1); });
  // Pillars at the corners and about every 4 cells along each wall.
  parts.posts = rects(`${k} posts`, q => {
    const p = .2, post = (x, z) => q(x - p, z - p, x + p, z + p);
    const along = (a0, a1, f) => { const n = Math.max(1, Math.round((a1 - a0) / 4)); for (let i = 0; i <= n; i++) f(lerp(a0, a1, i / n)); };
    along(X0 + .5, X1 - .5, x => { post(x, Z0 + .5); post(x, Z1 - .5); });
    along(Z0 + .5, Z1 - .5, z => { post(X0 + .5, z); post(X1 - .5, z); });
  });
  if (paperWalls(kind)) {
    // Shoji: a pale paper strip along each wall with dark lattice bars every half cell.
    parts.paper = rects(`${k} paper`, q => { const a = .3, b = .7; q(X0 + .5, Z0 + a, X1 - .5, Z0 + b); q(X0 + .5, Z1 - b, X1 - .5, Z1 - a); q(X0 + a, Z0 + .5, X0 + b, Z1 - .5); q(X1 - b, Z0 + .5, X1 - a, Z1 - .5); });
    parts.lattice = rects(`${k} lattice`, q => {
      const t = .025;
      for (let x = X0 + 1; x < X1 - .6; x += .5) { q(x - t, Z0 + .3, x + t, Z0 + .7); q(x - t, Z1 - .7, x + t, Z1 - .3); }
      for (let z = Z0 + 1; z < Z1 - .6; z += .5) { q(X0 + .3, z - t, X0 + .7, z + t); q(X1 - .7, z - t, X1 - .3, z + t); }
      q(X0 + .5, Z0 + .49, X1 - .5, Z0 + .51); q(X0 + .5, Z1 - .51, X1 - .5, Z1 - .49); q(X0 + .49, Z0 + .5, X0 + .51, Z1 - .5); q(X1 - .51, Z0 + .5, X1 - .49, Z1 - .5);
    });
  } else {
    parts.beam = rects(`${k} beam`, q => { const a = .38, b = .62; q(X0 + .5, Z0 + a, X1 - .5, Z0 + b); q(X0 + .5, Z1 - b, X1 - .5, Z1 - a); q(X0 + a, Z0 + .5, X0 + b, Z1 - .5); q(X1 - b, Z0 + .5, X1 - a, Z1 - .5); });
  }
  if (kind === 'tatami') {
    // Mats 2 x 1 in running bond; dark heri cloth on each mat's long sides; every other mat a
    // shade darker, as the weave turns.
    const mats = [];
    for (let row = 0, z = fz0; z < fz1 - .01; z += 1, row++)
      for (let x = fx0 - (row % 2); x < fx1 - .01; x += 2) mats.push([Math.max(x, fx0), z, Math.min(x + 2, fx1), z + 1]);
    parts.shade = rects(`${k} shade`, q => mats.forEach(([a, z, b], i) => { if (i % 2) q(a + .03, z + .06, b - .03, z + .94); }));
    parts.heri = rects(`${k} heri`, q => mats.forEach(([a, z, b]) => { q(a, z, b, z + .06); q(a, z + .94, b, z + 1); q(b - .012, z, b + .012, z + 1); }));
  } else {
    // Wooden boards along the room's long side, seams every half cell, joints staggered.
    const flat = w >= h, L0 = flat ? fx0 : fz0, L1 = flat ? fx1 : fz1, A0 = flat ? fz0 : fx0, A1 = flat ? fz1 : fx1, t = .018;
    parts.boards = rects(`${k} boards`, q => {
      const line = (l0, l1, a0, a1) => flat ? q(l0, a0, l1, a1) : q(a0, l0, a1, l1);
      let row = 0;
      for (let a = A0; a < A1 - .01; a += .5, row++) {
        if (a > A0) line(L0, L1, a - t, a + t);
        for (let j = L0 + .6 + (row * 1.37) % 2.4; j < L1 - .3; j += 2.4) line(j - t, j + t, a, a + .5);
      }
    });
  }
  if (kind === 'hall') {
    const flat = w >= h, half = 1.5;
    parts.runner = rects(`${k} runner`, q => flat ? q(fx0 + .5, -half, fx1 - .5, half) : q(-half, fz0 + .5, half, fz1 - .5));
    parts.gold = rects(`${k} gold`, q => {
      const g = .06, i = .22;
      if (flat) { q(fx0 + .5, -half + i, fx1 - .5, -half + i + g); q(fx0 + .5, half - i - g, fx1 - .5, half - i); }
      else { q(-half + i, fz0 + .5, -half + i + g, fz1 - .5); q(half - i - g, fz0 + .5, half - i, fz1 - .5); }
    });
  }
  if (kind === 'stair') {
    // A flight 3 cells wide down the long side, steps every 0.4 cells; the far end goes dark
    // (it leads down into the castle; look only).
    const flat = w >= h, half = 1.5, L0 = (flat ? fx0 : fz0) + .6, L1 = (flat ? fx1 : fz1) - .6;
    parts.stairBed = rects(`${k} stair bed`, q => flat ? q(L0, -half, L1, half) : q(-half, L0, half, L1));
    parts.treads = rects(`${k} treads`, q => { for (let a = L0; a < L1 - .2; a += .4) flat ? q(a, -half + .15, a + .2, half - .15) : q(-half + .15, a, half - .15, a + .2); });
    parts.stringers = rects(`${k} stringers`, q => flat ? (q(L0, -half, L1, -half + .15), q(L0, half - .15, L1, half)) : (q(-half, L0, -half + .15, L1), q(half - .15, L0, half, L1)));
    parts.stairDark = [0, 1, 2].map(i => rects(`${k} stair dark ${i}`, q => {
      const a = lerp(L0, L1, i / 3), b = L1;
      flat ? q(a, -half, b, half) : q(-half, a, half, b);
    }));
  }
  if (kind === 'biwa') {
    // The dais: a raised tatami platform with a red lacquer border at the north, a gold folding
    // screen along the north wall behind it.
    parts.dais = rects(`${k} dais`, q => q(-2.5, fz1 - 3.2, 2.5, fz1 - .3));
    parts.daisMat = rects(`${k} dais mat`, q => q(-2.3, fz1 - 3.0, 2.3, fz1 - .5));
    parts.daisHeri = rects(`${k} dais heri`, q => { q(-2.3, fz1 - 3.0, 2.3, fz1 - 2.94); q(-2.3, fz1 - 1.8, 2.3, fz1 - 1.74); q(-2.3, fz1 - .56, 2.3, fz1 - .5); q(-.02, fz1 - 3.0, .02, fz1 - .5); });
    parts.screen = rects(`${k} screen`, q => { for (let i = 0; i < 6; i++) q(-3 + i, fz1 - .28, -2.04 + i, fz1 - .02); });
  }
  return parts;
}

// Lanterns inside a room, in cells about its centre: two rows along a corridor, the corners of a
// room, more along a hall.
export function lanternsOf(r) {
  const { w, h, kind } = r, out = [], X = w / 2 - 1.55, Z = h / 2 - 1.55;
  if (kind === 'corridor') {
    const flat = w >= h, len = (flat ? w : h) / 2 - 2;
    for (let a = -len; a <= len + .01; a += 3) out.push(flat ? [a, -Z] : [-X, a], flat ? [a + 1.5, Z] : [X, a + 1.5]);
    return out.filter(([x, z]) => Math.abs(x) <= w / 2 - 1.5 && Math.abs(z) <= h / 2 - 1.5);
  }
  if (w <= 5 || h <= 5) return [[X, Z]];
  out.push([-X, -Z], [X, -Z], [-X, Z], [X, Z]);
  if (kind === 'hall') out.push([0, -Z], [0, Z]);
  if (kind === 'biwa') return [[-X, -Z], [X, -Z], [-3.1, Z - .6], [3.1, Z - .6]];
  return out;
}

// One room. `c` is its world centre. Options: k scale and rot turn (depth rooms), dim 0..1 toward
// the void, layer base, far for a depth room (floor, walls, runner and two lantern glows only, 4
// to 6 draws), s for the lantern flicker, lamps 0..1, alpha.
export function drawRoom(r, c, { k = 1, rot = 0, dim = 0, layer = L.floor, wallLayer = L.wall, far = false, s = 0, lamps = 1, alpha = 1, id = r.id } = {}) {
  const parts = roomParts(r), tone = (col, extra = 0) => Color.Lerp(col, Void, Math.min(1, dim + extra)).withAlpha(alpha);
  const d = (m, y, col) => draw(m, c.x, y, c.z, k, k, rot, col);
  const tatami = r.kind === 'tatami';
  // Floor.
  draw(plane, c.x, layer, c.z, (r.w - 2) * k, (r.h - 2) * k, rot, tone(tatami ? Tatami : WoodFloor, r.kind === 'biwa' ? .12 : 0));
  if (far) {
    if (parts.runner) d(parts.runner, layer + .006, tone(Lacquer, .1));
    if (parts.stairBed) d(parts.stairDark[1], layer + .006, VoidDeep.withAlpha(alpha * .5));
    d(parts.walls, wallLayer, tone(WallWood));
    if (parts.paper) d(parts.paper, wallLayer + .006, tone(Paper, .1));
    const cr = Math.cos(-rot * Mathf.Deg2Rad), sr = Math.sin(-rot * Mathf.Deg2Rad);
    lanternsOf(r).slice(0, 2).forEach(([lx, lz], i) => {
      const x = c.x + (lx * cr - lz * sr) * k, z = c.z + (lx * sr + lz * cr) * k;
      const fl = .85 + .15 * Math.sin(s * (1.3 + rand(id * 13 + i)) + i * 1.7 + id);
      sprite({ x, z }, 2.6 * k, 2.6 * k, Lantern.withAlpha(.2 * fl * lamps * alpha), glow, wallLayer + .01);
      sprite({ x, z }, .5 * k, .5 * k, LanternCore.withAlpha(.6 * fl * lamps * alpha), glow, wallLayer + .012);
    });
    return;
  }
  if (parts.shade) d(parts.shade, layer + .002, tone(TatamiShade));
  if (parts.heri) d(parts.heri, layer + .004, tone(Heri));
  if (parts.boards) d(parts.boards, layer + .002, tone(WoodSeam));
  if (parts.runner) { d(parts.runner, layer + .006, tone(Lacquer, .2)); d(parts.gold, layer + .008, tone(Gold, .1)); }
  if (parts.stairBed) {
    d(parts.stairBed, layer + .006, tone(WallTop, .05));
    d(parts.treads, layer + .008, tone(WoodFloor, .05));
    d(parts.stringers, layer + .01, tone(WallWood));
    parts.stairDark.forEach((m, i) => d(m, layer + .012 + i * .001, VoidDeep.withAlpha(alpha * .3)));
  }
  if (parts.dais) {
    d(parts.dais, layer + .006, tone(Lacquer, .1));
    d(parts.daisMat, layer + .008, tone(Tatami));
    d(parts.daisHeri, layer + .01, tone(Heri));
    d(parts.screen, wallLayer + .012, tone(Gold));
  }
  // Walls.
  d(parts.walls, wallLayer, tone(WallWood));
  d(parts.edge, wallLayer + .002, tone(Lacquer, .15));
  d(parts.lip, wallLayer + .004, tone(WallTop));
  if (parts.paper) { d(parts.paper, wallLayer + .006, tone(Paper, .08)); d(parts.lattice, wallLayer + .008, tone(Lattice)); }
  if (parts.beam) d(parts.beam, wallLayer + .006, tone(WallTop, .05));
  d(parts.posts, wallLayer + .01, tone(WallWood, -.05));
  // Lanterns: a warm pool on the floor, the paper body, a bright core. They flicker a little.
  if (lamps > 0) {
    const cr = Math.cos(-rot * Mathf.Deg2Rad), sr = Math.sin(-rot * Mathf.Deg2Rad);
    lanternsOf(r).forEach(([lx, lz], i) => {
      const x = c.x + (lx * cr - lz * sr) * k, z = c.z + (lx * sr + lz * cr) * k;
      const fl = .9 + .1 * Math.sin(s * (5 + rand(id * 13 + i) * 4) + i * 1.7) * Math.sin(s * 1.9 + id + i);
      const lit = lamps * (1 - dim * .7) * alpha;
      sprite({ x, z }, 3.6 * k, 3.6 * k, Lantern.withAlpha(.13 * fl * lit), glow, layer + .014);
      draw(disc, x, wallLayer + .02, z - .03 * k, .19 * k, .17 * k, 0, tone(WallWood));
      draw(disc, x, wallLayer + .021, z, .15 * k, .15 * k, 0, Color.Lerp(LanternPaper, VoidDeep, dim * .8).withAlpha(alpha));
      sprite({ x, z }, .55 * k, .55 * k, LanternCore.withAlpha(.55 * fl * lit), glow, wallLayer + .022);
    });
  }
}

// ---- the void -------------------------------------------------------------------------------
// Black under everything, drawn-only rooms at two depths, then the void terrain over them (see-
// through, so the depth rooms show only in the gaps; real floors cover them). In game the void
// terrain would need a transparent texture for this, with the depth rooms drawn at BelowTerrain.
const DepthRooms = 70, Flights = 12;
// A flight of stairs with no room around it: 3 cells wide, `len` long, steps every 0.4 cells,
// dark side boards, the far end fading into the dark it leads down to.
function flight(len, c, { k = 1, rot = 0, dim = 0, layer = L.depth, alpha = 1 } = {}) {
  const key = `castle flight ${len}`, half = 1.5, L0 = -len / 2, L1 = len / 2;
  const treads = rects(`${key} treads`, q => { for (let a = L0; a < L1 - .2; a += .4) q(a, -half + .12, a + .2, half - .12); });
  const sides = rects(`${key} sides`, q => { q(L0, -half, L1, -half + .12); q(L0, half - .12, L1, half); });
  const tone = col => Color.Lerp(col, Void, dim).withAlpha(alpha);
  draw(plane, c.x, layer, c.z, len * k, 3 * k, rot, tone(WallTop));
  draw(treads, c.x, layer + .002, c.z, k, k, rot, tone(WoodFloor));
  draw(sides, c.x, layer + .004, c.z, k, k, rot, tone(WallWood));
  const r = -rot * Mathf.Deg2Rad, ux = Math.cos(r), uz = Math.sin(r);
  for (const [from, a] of [[.35, .35], [.7, .5]]) {
    const mid = (L0 + len * from + L1) / 2, span = L1 - (L0 + len * from);
    draw(plane, c.x + ux * mid * k, layer + .006, c.z + uz * mid * k, span * k, 3 * k, rot, VoidDeep.withAlpha(alpha * a));
  }
}
export function drawVoid(centre, s, { seed = 1, reach = 90, depth = true, alpha = 1 } = {}) {
  draw(plane, centre.x, L.back, centre.z, reach * 3, reach * 3, 0, VoidDeep.withAlpha(alpha));
  if (depth) {
    const R = rng(seed * 31 + 7);
    const items = [];
    for (let i = 0; i < DepthRooms; i++) {
      const [kind, , dims] = pickKind(R), flat = R() < .5, [w, h] = dims(R, flat);
      const level = R() < .45 ? 2 : 1;                         // 2 = further down
      const x = (R() - .5) * reach * 2, z = (R() - .5) * reach * 2;
      const turned = R() < .3, rot = turned ? (R() < .5 ? 90 : (R() - .5) * 30) : 0;
      const driftA = (R() - .5) * 1.4, driftP = R() * TAU, spin = turned && R() < .4 ? (R() - .5) * 6 : 0;
      items.push({ r: { id: 1000 + i, kind, w, h }, level, x, z, rot, driftA, driftP, spin });
    }
    // Stair flights hanging on their own between levels, some turned: the castle's look of stairs
    // running every way. Drawn-only, like the rooms.
    for (let i = 0; i < Flights; i++) {
      const len = int(R, 8, 16), level = R() < .5 ? 2 : 1, rot = R() < .6 ? (R() < .5 ? 0 : 90) + (R() - .5) * 8 : (R() - .5) * 70;
      items.push({ flight: len, level, x: (R() - .5) * reach * 2, z: (R() - .5) * reach * 2, rot, driftA: (R() - .5), driftP: R() * TAU, spin: 0, r: { id: 2000 + i } });
    }
    items.sort((a, b) => b.level - a.level);
    items.forEach((it, i) => {
      const k = it.level === 2 ? .5 : .72, dim = it.level === 2 ? .72 : .52;
      const drift = Math.sin(s * .18 + it.driftP) * it.driftA * (it.level === 2 ? .6 : 1);
      const c = { x: centre.x + it.x + drift, z: centre.z + it.z + drift * .4 };
      const y = L.depth + (it.level === 2 ? 0 : .12) + (i % 20) * .004;
      if (it.flight) flight(it.flight, c, { k, rot: it.rot, dim, layer: y, alpha });
      else drawRoom(it.r, c, { k, rot: it.rot + it.spin * s, dim, layer: y, wallLayer: y + .03, far: true, s, lamps: it.level === 2 ? .7 : 1, alpha, id: it.r.id });
    });
  }
  // The void terrain: dark and see-through, so what lies below reads as far away.
  draw(plane, centre.x, L.fog, centre.z, reach * 3, reach * 3, 0, Void.withAlpha(.38 * alpha));
}

// Every room of a castle. `offsets` maps room id -> { x, z } (a sliding room); `hide` skips ids.
export function drawRooms(castle, base, s, { offsets = null, hide = null, alpha = 1 } = {}) {
  for (const r of castle.rooms) {
    if (hide?.has(r.id)) continue;
    drawRoom(r, centreOf(base, r, offsets?.get(r.id)), { s, alpha });
  }
}

// ---- doors in walls -------------------------------------------------------------------------
// One wall cell of a doorway (a doorway is two such cells, one per room). axis 'z': the wall runs
// north-south and the leaves slide along z; 'x': east-west. open 0..1 slides the two paper leaves
// into the wall; sealed 0..1 slides the bar in from the side and locks it.
export function wallDoor(key, c, axis, open, { sealed = 0, alpha = 1, layer = L.wall + .03 } = {}) {
  if (alpha <= 0) return;
  const alongZ = axis === 'z', rect = (a, b, la, lb, y, col) => {
    // a..b along the wall, la..lb across it, cell-centred.
    const cx = c.x + (alongZ ? (la + lb) / 2 : (a + b) / 2), cz = c.z + (alongZ ? (a + b) / 2 : (la + lb) / 2);
    const sx = alongZ ? lb - la : b - a, sz = alongZ ? b - a : lb - la;
    if (sx > .001 && sz > .001) draw(plane, cx, y, cz, sx, sz, 0, col.withAlpha(col.a * alpha));
  };
  rect(-.5, .5, -.5, .5, layer, WoodFloor);                        // the threshold: floor through the wall
  rect(-.5, .5, -.5, -.42, layer + .001, WoodSeam); rect(-.5, .5, .42, .5, layer + .001, WoodSeam);
  // Leaves: each half a cell long, 0.24 thick; opening slides them into the wall pockets.
  const len = .5 * (1 - open);
  for (const side of [-1, 1]) {
    if (len < .01) continue;
    const e = side * .5, i = side * (.5 - len);
    const a = Math.min(e, i), b = Math.max(e, i);
    rect(a, b, -.14, .14, layer + .004, Lattice);
    rect(a + .03, b - .03, -.1, .1, layer + .006, Paper);
    for (let u = .125; u < .5; u += .125) { const p = i + side * u; if ((p - a) * (p - b) < 0) rect(p - .012, p + .012, -.1, .1, layer + .008, Lattice); }
    rect(Math.min(i, i + side * .04), Math.max(i, i + side * .04), -.14, .14, layer + .009, Lattice);
  }
  rect(-.5, -.4, -.22, .22, layer + .01, WallWood); rect(.4, .5, -.22, .22, layer + .01, WallWood);  // jambs
  if (sealed > 0) {
    // The bar (kannuki): dark lacquered wood across both leaves, sliding in from the south/east.
    const off = (1 - smooth(sealed)) * 1.2;
    rect(-.55 + off, .55 + off, -.07, .07, layer + .014, Lacquer.withAlpha(Math.min(1, sealed * 3)));
    rect(-.55 + off, .55 + off, .02, .05, layer + .015, new Color(.72, .30, .20, Math.min(1, sealed * 3)));
    rect(-.3 + off, -.22 + off, -.12, .12, layer + .016, new Color(.25, .25, .27, Math.min(1, sealed * 3)));
    rect(.22 + off, .3 + off, -.12, .12, layer + .016, new Color(.25, .25, .27, Math.min(1, sealed * 3)));
  }
}

// Both cells of a doorway, with its rooms' offsets.
export function doorway(key, door, base, open, { offsets = null, sealed = 0, alpha = 1 } = {}) {
  door.cells.forEach(([x, z], i) => {
    const off = offsets?.get(i === 0 ? door.a : door.b) ?? { x: 0, z: 0 };
    wallDoor(`${key} ${i}`, { x: base.x + x + .5 + off.x, z: base.z + z + .5 + off.z }, door.axis, open, { sealed, alpha });
  });
}

// ---- the floor door ---------------------------------------------------------------------------
// A pair of shoji leaves lying in the floor inside a dark wood frame. open 0..1 slides them apart
// east-west; under them is the shaft: black, its far (north) wall faintly lit, a warm glow deep
// down. It is level, so it looks the same from every side.
const FrameWood = new Color(.16, .09, .05), ShaftWall = new Color(.20, .11, .06);
export const DoorSize = 1.3;
export function floorDoor(key, c, open, alpha, { size = DoorSize, layer = L.door, s = 0 } = {}) {
  if (alpha <= 0) return;
  const F = .13, H = size - 2 * F, y = layer, a = alpha;
  draw(plane, c.x, y, c.z, size, size, 0, FrameWood.withAlpha(a));
  draw(plane, c.x, y + .001, c.z, size - .05, size - .05, 0, WallTop.withAlpha(a * .6));
  draw(plane, c.x, y + .002, c.z, H + .02, H + .02, 0, FrameWood.withAlpha(a));
  draw(plane, c.x, y + .003, c.z, H, H, 0, VoidDeep.withAlpha(a));
  if (open > 0) {
    draw(plane, c.x, y + .004, c.z + H / 2 - .15, H, .3, 0, ShaftWall.withAlpha(a * Math.min(1, open * 2)));
    draw(plane, c.x, y + .005, c.z + H / 2 - .32, H, .06, 0, ShaftWall.withAlpha(a * .5 * Math.min(1, open * 2)));
    draw(plane, c.x, y + .006, c.z + H / 2 - .015, H, .03, 0, WallTop.withAlpha(a * open));
    const pulse = .85 + .15 * Math.sin(s * 3.1 + c.x);
    sprite({ x: c.x, z: c.z - .1 }, H * .75, H * .55, Lantern.withAlpha(.3 * a * open * pulse), glow, y + .007);
    sprite({ x: c.x, z: c.z - .12 }, H * .22, H * .16, LanternCore.withAlpha(.35 * a * open * pulse), glow, y + .008);
  }
  const len = H / 2 * (1 - open);
  for (const side of [-1, 1]) {
    if (len < .005) continue;
    const edge = side * H / 2, lead = side * (H / 2 - len), x0 = Math.min(edge, lead), x1 = Math.max(edge, lead);
    draw(plane, c.x + (x0 + x1) / 2, y + .01, c.z, x1 - x0, H, 0, Paper.withAlpha(a));
    // Lattice fixed to the leaf, so it slides with it.
    for (let u = H / 6; u < H / 2; u += H / 6) { const p = lead + side * u; if ((p - x0) * (p - x1) < 0) draw(plane, c.x + p, y + .012, c.z, .022, H, 0, Lattice.withAlpha(a)); }
    for (const zz of [-H / 6, H / 6]) draw(plane, c.x + (x0 + x1) / 2, y + .012, c.z + zz, x1 - x0, .022, 0, Lattice.withAlpha(a));
    draw(plane, c.x + lead + side * .02, y + .013, c.z, .045, H, 0, Lattice.withAlpha(a));
  }
}
// A thin pale outline where a floor door will open: shown while the caster winds up.
export function doorMark(c, alpha, size = DoorSize) {
  if (alpha <= 0) return;
  const t = .045, h = size / 2;
  for (const [x, z, w, d] of [[0, -h, size, t], [0, h, size, t], [-h, 0, t, size], [h, 0, t, size]])
    draw(plane, c.x + x, L.door + .02, c.z + z, w, d, 0, Strum.withAlpha(alpha), additive);
}

// One door's life from its opening moment `age` = 0: the frame fades in over 0.1 s before, the
// leaves snap open in 0.18 s, stay open `hold` s, close in 0.25 s, the frame fades over 0.3 s.
export const Door = { fadeIn: .1, open: .18, close: .25, fadeOut: .3 };
export function doorAt(age, hold) {
  if (age < -Door.fadeIn) return { alpha: 0, open: 0 };
  if (age < 0) return { alpha: 1 + age / Door.fadeIn, open: 0 };
  const shut = Door.open + hold;
  if (age < shut) return { alpha: 1, open: easeOut(age / Door.open) };
  if (age < shut + Door.close) return { alpha: 1, open: 1 - smooth((age - shut) / Door.close) };
  const out = age - shut - Door.close;
  return { alpha: 1 - clamp(out / Door.fadeOut), open: 0 };
}
export const doorEnd = hold => Door.open + hold + Door.close + Door.fadeOut;

// ---- pawns ----------------------------------------------------------------------------------
// Stand-in pawn: two discs and a soft shadow. scale shrinks it about its feet, dark 0..1 takes it
// toward black (down a shaft), h lifts it (a hop), downed lays it on its side, squash (Crush).
export function figure(pos, colour, sun, strength, { alpha = 1, scale = 1, dark = 0, h = 0, downed = 0, squash = 0, layer = L.pawn } = {}) {
  if (alpha <= 0) return;
  const c = q => Color.Lerp(q, VoidDeep, dark).withAlpha(alpha), lift = h * Lift;
  const sw = 1 + squash * .35, sh = 1 - squash * .3;
  sprite({ x: pos.x + sun.x * .45 * scale, z: pos.z + sun.z * .45 * scale }, .85 * scale / (1 + h), .4 * scale / (1 + h), Hair.withAlpha(strength * alpha * (1 - dark)), soft, L.shadow);
  if (downed >= 1) {
    draw(disc, pos.x + .05 * scale, layer, pos.z + .12 * scale + lift, .32 * scale, .2 * scale, 0, c(colour));
    draw(disc, pos.x - .34 * scale, layer + .002, pos.z + .14 * scale + lift, .16 * scale, .17 * scale, 0, c(Skin));
    return;
  }
  draw(disc, pos.x, layer, pos.z + (.18 * sh * scale) + lift, .22 * scale * sw, .32 * scale * sh, 0, c(colour));
  draw(disc, pos.x, layer + .002, pos.z + (.58 * sh * scale) + lift, .16 * scale, .17 * scale, 0, c(Skin));
}

// A pawn going down through an open floor door: u 0..1, standing on the open door to gone. It
// shrinks toward the shaft, darkens, and the shaft's dark covers it; pale streaks run up past it.
export function sinking(key, pos, colour, u, sun, strength, { downed = 0 } = {}) {
  if (u >= 1) return;
  const e = smooth(u);
  figure({ x: pos.x, z: pos.z - .18 * e }, colour, sun, strength, { scale: 1 - .5 * e, dark: e * .85, alpha: 1 - smooth((u - .75) / .25), downed });
  const H = DoorSize - .26;
  draw(plane, pos.x, L.pawn + .01, pos.z, H, H, 0, VoidDeep.withAlpha(.85 * Math.pow(e, 1.4)));
  streaks(key, pos, u, 1);
}
// The reverse: out of the shaft, growing and brightening, then a small hop onto the floor.
export function rising(key, pos, colour, u, sun, strength, { hop = .35, downed = 0 } = {}) {
  if (u <= 0) return;
  const up = smooth(Math.min(1, u / .7)), hopU = clamp((u - .6) / .4);
  figure({ x: pos.x, z: pos.z - .18 * (1 - up) }, colour, sun, strength, { scale: .5 + .5 * up, dark: (1 - up) * .85, h: downed ? 0 : hop * bump(hopU), alpha: Math.min(1, u * 4), downed });
  const H = DoorSize - .26;
  draw(plane, pos.x, L.pawn + .01, pos.z, H, H, 0, VoidDeep.withAlpha(.85 * Math.pow(1 - up, 1.4)));
  streaks(key, pos, u, -1);
}
// Thin pale lines inside the shaft: rising past a falling pawn (dir 1), or ahead of a rising one.
function streaks(key, pos, u, dir) {
  const a = bump(u) * .5;
  if (a <= .01) return;
  for (let i = 0; i < 5; i++) {
    const x = pos.x + (rand(i + 40) - .5) * .8, ph = (u * 2.2 + rand(i + 50)) % 1, z = pos.z - .45 + ph * .9;
    draw(plane, x, L.pawn + .012, z, .025, .22 + rand(i + 60) * .2, 0, Strum.withAlpha(a * bump(ph)), additive);
  }
}

// ---- the caster -----------------------------------------------------------------------------
// Nakime stand-in: lilac kimono with wide sleeves, dark obi, long black hair over the face with
// one pale eye showing, the biwa across the lap with its neck up to her left (screen right), the
// bachi in her right hand (screen left). She faces south. `strum` = seconds since the last strum
// started (the bachi's swing), or null for rest. Seated in the castle, standing on the home map.
const BiwaAxis = 58 * Mathf.Deg2Rad;
export function nakime(key, pos, sun, strength, { strum = null, seated = true, alpha = 1, dark = 0, scale = 1, biwa = 1, downed = false, char = 0 } = {}) {
  if (alpha <= 0) return;
  const k = scale, lay = L.pawn, c = q => Color.Lerp(Color.Lerp(q, VoidDeep, dark), new Color(.12, .08, .07), char).withAlpha(alpha * q.a);
  if (downed) {
    // Lying on her side: kimono, the hair fanned out past the head. No biwa (it is gone while
    // she is down).
    sprite({ x: pos.x + sun.x * .2, z: pos.z + sun.z * .2 }, 1.0 * k, .4 * k, Hair.withAlpha(strength * alpha), soft, L.shadow);
    draw(disc, pos.x + .08 * k, lay, pos.z + .1 * k, .36 * k, .2 * k, 0, c(Kimono));
    draw(plane, pos.x + .08 * k, lay + .002, pos.z + .1 * k, .08 * k, .36 * k, 0, c(Obi));
    draw(disc, pos.x - .44 * k, lay + .001, pos.z + .12 * k, .26 * k, .2 * k, 0, c(Hair));
    draw(disc, pos.x - .32 * k, lay + .003, pos.z + .13 * k, .15 * k, .16 * k, 0, c(Skin));
    draw(disc, pos.x - .36 * k, lay + .004, pos.z + .15 * k, .15 * k, .15 * k, 0, c(Hair));
    return;
  }
  const up = seated ? 0 : .14;                          // standing lifts the upper body
  const P = (x, z) => ({ x: pos.x + x * k, z: pos.z + z * k });
  const D = (x, z, rx, rz, col, dy = 0, rot = 0) => draw(disc, pos.x + x * k, lay + dy, pos.z + z * k, rx * k, rz * k, rot, c(col));
  sprite({ x: pos.x + sun.x * .4, z: pos.z + sun.z * .4 }, (seated ? 1.0 : .85) * k, .45 * k, Hair.withAlpha(strength * alpha * (1 - dark)), soft, L.shadow);
  // Hair down the back, under everything.
  D(0, .44 + up, .19, .3, Hair, -.004);
  // Kimono: a wide skirt (seated: folded legs), the body, the sleeves, the obi, the collar.
  if (seated) D(0, .1, .33, .2, KimonoDark);
  else D(0, .14, .22, .24, KimonoDark);
  D(0, .24 + up, .23, .22, Kimono, .002);
  D(-.25, .2 + up, .13, .17, Kimono, .003);
  D(.25, .2 + up, .13, .17, Kimono, .003);
  draw(plane, pos.x, lay + .004, pos.z + (.2 + up) * k, .4 * k, .075 * k, 0, c(Obi));
  draw(plane, pos.x - .045 * k, lay + .005, pos.z + (.36 + up) * k, .05 * k, .12 * k, -30, c(new Color(.93, .90, .84)));
  draw(plane, pos.x + .045 * k, lay + .005, pos.z + (.36 + up) * k, .05 * k, .12 * k, 30, c(new Color(.93, .90, .84)));
  // Head: skin under a curtain of hair, strands down both shoulders; the single eye.
  D(0, .5 + up, .15, .16, Skin, .006);
  D(0, .54 + up, .165, .15, Hair, .008);
  draw(plane, pos.x - .12 * k, lay + .008, pos.z + (.4 + up) * k, .07 * k, .26 * k, 8, c(Hair));
  draw(plane, pos.x + .12 * k, lay + .008, pos.z + (.4 + up) * k, .07 * k, .26 * k, -8, c(Hair));
  D(0, .49 + up, .055, .038, Eye, .01);
  D(0, .49 + up, .022, .03, new Color(.25, .08, .10), .012);
  // The biwa: pear-shaped body on the lap, neck up to the north-east, bent pegbox, four strings.
  const ax = Math.cos(BiwaAxis), az = Math.sin(BiwaAxis), nx = -az, nz = ax;
  const cb = q => Color.Lerp(q, VoidDeep, dark).withAlpha(alpha * q.a * biwa);   // the biwa does not char
  const b0 = P(.02, .1 + up * .6);                          // bottom of the body
  const along = (p, t, w = 0) => ({ x: p.x + (ax * t + nx * w) * k, z: p.z + (az * t + nz * w) * k });
  const outline = side => { const pts = []; for (let i = 0; i <= 12; i++) { const t = i / 12; const hw = .15 * Math.sqrt(Math.max(0, Math.sin(t * Math.PI * .92 + .05))) * (1 - .45 * t); pts.push(along(b0, t * .42, side * hw)); } return pts; };
  const mid = []; for (let i = 0; i <= 12; i++) mid.push(along(b0, i / 12 * .42));
  band(`${key} biwa back`, outline(-1), outline(1), cb(BiwaDark), lay + .014);
  band(`${key} biwa face l`, outline(-1).map((q, i) => ({ x: lerp(q.x, mid[i].x, .12), z: lerp(q.z, mid[i].z, .12) })), mid, cb(BiwaFace), lay + .016);
  band(`${key} biwa face r`, mid, outline(1).map((q, i) => ({ x: lerp(q.x, mid[i].x, .12), z: lerp(q.z, mid[i].z, .12) })), cb(BiwaWood), lay + .016);
  const guard = [along(b0, .19, -.12), along(b0, .19, .12)], guard2 = [along(b0, .26, -.1), along(b0, .26, .1)];
  band(`${key} biwa guard`, guard, guard2, cb(new Color(.13, .09, .08)), lay + .018);
  const bridge = [along(b0, .07, -.07), along(b0, .07, .07)], bridge2 = [along(b0, .095, -.07), along(b0, .095, .07)];
  band(`${key} biwa bridge`, bridge, bridge2, cb(BiwaDark), lay + .018);
  const n0 = along(b0, .4), n1 = along(b0, .66), bent = { x: n1.x + Math.cos(BiwaAxis + 1.1) * .11 * k, z: n1.z + Math.sin(BiwaAxis + 1.1) * .11 * k };
  band(`${key} biwa neck`, [along(b0, .38, -.03), along(b0, .66, -.025)], [along(b0, .38, .03), along(b0, .66, .025)], cb(BiwaDark), lay + .015);
  band(`${key} biwa head`, [{ x: n1.x - nx * .03 * k, z: n1.z - nz * .03 * k }, { x: bent.x - nx * .02 * k, z: bent.z - nz * .02 * k }],
    [{ x: n1.x + nx * .03 * k, z: n1.z + nz * .03 * k }, { x: bent.x + nx * .02 * k, z: bent.z + nz * .02 * k }], cb(BiwaDark), lay + .015);
  // Strings: they flash as the bachi crosses them.
  const flash = strum === null ? 0 : bump(clamp((strum - .02) / .14));
  for (let i = 0; i < 4; i++) {
    const w = (i - 1.5) * .018, from = along(b0, .08, w), to = along(n1, 0, w * .6);
    band(`${key} string ${i}`, [along(from, 0, -.004), along(to, 0, -.004)], [along(from, 0, .004), along(to, 0, .004)],
      cb(Color.Lerp(new Color(.85, .80, .66), Strum, flash)), lay + .02);
  }
  if (flash > .01) sprite(along(b0, .2), .5 * k, .5 * k, Strum.withAlpha(.6 * flash * alpha), glow, lay + .03);
  // Left hand on the neck, the sleeve to it.
  const lh = along(b0, .5, .02);
  band(`${key} sleeve l`, [P(.2, .32 + up), P(.28, .26 + up)], [{ x: lh.x - .02 * k, z: lh.z + .04 * k }, { x: lh.x + .03 * k, z: lh.z - .02 * k }], c(Kimono), lay + .021);
  draw(disc, lh.x, lay + .022, lh.z, .045 * k, .05 * k, 0, c(Skin));
  // Right hand and the bachi: rests across the strings, lifts before a strum, sweeps down across.
  const deg = bachiAngle(strum), r = deg * Mathf.Deg2Rad, hand = P(-.13, .2 + up * .7);
  const dir = { x: Math.cos(r), z: Math.sin(r) }, perp = { x: -dir.z, z: dir.x };
  const bp = (t, w) => ({ x: hand.x + (dir.x * t + perp.x * w) * k, z: hand.z + (dir.z * t + perp.z * w) * k });
  band(`${key} bachi`, [bp(0, -.02), bp(.24, -.1)], [bp(0, .02), bp(.24, .1)], cb(Bachi), lay + .024);
  band(`${key} bachi edge`, [bp(.22, -.1), bp(.245, -.1)], [bp(.22, .1), bp(.245, .1)], cb(new Color(.7, .66, .58)), lay + .025);
  band(`${key} sleeve r`, [P(-.34, .3 + up), P(-.2, .36 + up)], [{ x: hand.x - .08 * k, z: hand.z - .05 * k }, { x: hand.x + .02 * k, z: hand.z + .03 * k }], c(Kimono), lay + .023);
  draw(disc, hand.x, lay + .026, hand.z, .05 * k, .05 * k, 0, c(Skin));
  // The sweep's arc: a pale fan behind the bachi tip for 0.15 s.
  if (strum !== null && strum > 0 && strum < .2) {
    const f = 1 - strum / .2, a0 = bachiAngle(Math.max(0, strum - .06)) * Mathf.Deg2Rad, a1 = r, inner = [], outer = [];
    for (let i = 0; i <= 8; i++) { const a = lerp(a0, a1, i / 8); inner.push({ x: hand.x + Math.cos(a) * .14 * k, z: hand.z + Math.sin(a) * .14 * k }); outer.push({ x: hand.x + Math.cos(a) * .27 * k, z: hand.z + Math.sin(a) * .27 * k }); }
    band(`${key} bachi arc`, inner, outer, Strum.withAlpha(.5 * f * alpha), lay + .027);
  }
}
// The bachi's angle (degrees, 0 = east, 90 = north) by time since a strum began: rest across the
// strings, lift 0.2 s before, sweep down in 0.08 s, settle back over 0.4 s.
export const StrumLead = .2;
export function bachiAngle(age) {
  const Rest = -12, Lift = 48, Down = -52;
  if (age === null || age < -StrumLead || age > .6) return Rest;
  if (age < 0) return lerp(Rest, Lift, smooth((age + StrumLead) / StrumLead));
  if (age < .08) return lerp(Lift, Down, easeOut(age / .08));
  return lerp(Down, Rest, smooth((age - .08) / .45));
}

// A level ring `width` cells wide at any radius (a scaled band mesh would thicken with the radius).
export function thinRing(key, pos, radius, width, colour, layer, material = additive) {
  if (radius <= 0 || colour.a <= .001) return;
  const n = Math.max(24, Math.min(96, Math.round(radius * 10))), v = [], t = [];
  for (let i = 0; i <= n; i++) {
    const a = i / n * TAU, c = Math.cos(a), sn = Math.sin(a), r0 = Math.max(0, radius - width / 2), r1 = radius + width / 2;
    v.push(pos.x + c * r0, pos.z + sn * r0, pos.x + c * r1, pos.z + sn * r1);
    if (i < n) { const q = i * 2; t.push(q, q + 2, q + 1, q + 1, q + 2, q + 3); }
  }
  const m = scratch(key); m.setFlat(v, t);
  draw(m, 0, layer, 0, 1, 1, 0, colour, material);
}
const scratchMeshes = new Map();
const scratch = key => scratchMeshes.get(key) ?? scratchMeshes.set(key, new Mesh(key)).get(key);

// The strum: rings out from the biwa (three, 0.07 s apart), and small sound arcs either side.
export function strum(key, pos, age, { reach = 6, life = .6, alpha = 1, layer = L.fx } = {}) {
  if (age < 0 || age > life + .2) return;
  for (let i = 0; i < 3; i++) {
    const a = age - i * .07;
    if (a < 0 || a > life) continue;
    const u = a / life;
    thinRing(`${key} ring ${i}`, pos, reach * easeOut(u), .09 - i * .02, Strum.withAlpha((1 - u) * (.75 - i * .2) * alpha), layer + i * .002);
  }
  const sa = 1 - clamp(age / .35);
  if (sa > 0) for (const side of [-1, 1]) for (let j = 0; j < 3; j++) {
    const r0 = .32 + j * .14 + age * .8, pts = [], pts2 = [];
    for (let i = 0; i <= 6; i++) {
      const a = (side > 0 ? 0 : Math.PI) + (i / 6 - .5) * 1.1;
      pts.push({ x: pos.x + Math.cos(a) * r0, z: pos.z + Math.sin(a) * r0 * .9 });
      pts2.push({ x: pos.x + Math.cos(a) * (r0 + .035), z: pos.z + Math.sin(a) * (r0 + .035) * .9 });
    }
    band(`${key} sound ${side} ${j}`, pts, pts2, Strum.withAlpha(sa * (.7 - j * .18) * alpha), layer + .01);
  }
}

// A room's outline as light: `alpha` 0..1, `width` in cells. Afterimages of a sliding room.
export function outline(r, c, alpha, width = .16) {
  if (alpha <= .005) return;
  const w = r.w, h = r.h, t = width;
  for (const [x, z, sx, sz] of [[0, -h / 2 + t / 2, w, t], [0, h / 2 - t / 2, w, t], [-w / 2 + t / 2, 0, t, h], [w / 2 - t / 2, 0, t, h]])
    draw(plane, c.x + x, L.wall + .05, c.z + z, sx, sz, 0, Strum.withAlpha(alpha), additive);
}

// Where the carrier sits: on the dais, facing south into the biwa room.
export const seatOf = (base, biwa) => ({ x: base.x + biwa.x + biwa.w / 2, z: base.z + biwa.z + biwa.h - 2.9 });
export const biwaOf = seat => ({ x: seat.x + .05, z: seat.z + .2 });

// The carrier on the dais playing a list of strum times (seconds): the bachi follows the latest
// strum whose 0.2 s lift has begun, and every strum sends its rings.
export function carrierPlays(key, castle, base, s, strums, sun, strength, { reach = 5 } = {}) {
  const seat = seatOf(base, castle.rooms[0]);
  let age = null;
  for (const t of strums) if (s >= t - StrumLead) age = s - t;
  nakime(key, seat, sun, strength, { strum: age });
  strums.forEach((t, i) => strum(`${key} strum ${i}`, biwaOf(seat), s - t, { reach, life: .6 }));
}

// The castle answering a strum: the room's outline lights up warm and fades over 0.45 s.
export function roomFlash(r, c, age, { life = .45, alpha = 1 } = {}) {
  if (age < 0 || age > life) return;
  const f = (1 - age / life) * alpha, w = r.w, h = r.h, t = .22;
  for (const [x, z, sx, sz] of [[0, -h / 2 + t / 2, w, t], [0, h / 2 - t / 2, w, t], [-w / 2 + t / 2, 0, t, h], [w / 2 - t / 2, 0, t, h]])
    draw(plane, c.x + x, L.wall + .05, c.z + z, sx, sz, 0, Strum.withAlpha(.55 * f), additive);
  sprite(c, w * 1.1, h * 1.1, Strum.withAlpha(.1 * f), glow, L.wall + .049);
}

// Dust thrown along a segment (a room meeting another, a wall slamming): puffs that rise and fade.
export function dustLine(key, a, b, age, { life = .7, count = 10, alpha = .6, seed = 0, spread = .5 } = {}) {
  if (age < 0 || age > life) return;
  for (let i = 0; i < count; i++) {
    const t = (i + .5) / count, u = clamp(age / (life * (.6 + rand(i + seed) * .4)));
    if (u >= 1) continue;
    const x = lerp(a.x, b.x, t) + (rand(i + seed + 10) - .5) * spread, z = lerp(a.z, b.z, t) + (rand(i + seed + 20) - .5) * spread;
    const h = u * .5 * (.5 + rand(i + seed + 30));
    sprite({ x, z: z + h * Lift }, .45 + u * .5, .35 + u * .4, Dust.withAlpha(bump(Math.min(1, u * 1.3 + .1)) * alpha), puff, L.fx + .01);
  }
}

// Stun marks over a pawn's head: four small pale stars circling for `life` s.
export function stunned(key, pos, age, life = 1) {
  if (age < 0 || age > life) return;
  const a = Math.min(1, age / .1) * (1 - clamp((age - life + .2) / .2));
  for (let i = 0; i < 4; i++) {
    const t = age * 5 + i * TAU / 4, p = { x: pos.x + Math.cos(t) * .2, z: pos.z + .95 + Math.sin(t) * .08 };
    sprite(p, .11, .11, Strum.withAlpha(.9 * a), glow, L.fx + .03);
  }
}

// A soft ring on the floor at a true radius (the cast range).
export function rangeRing(pos, radius, alpha) { if (alpha > 0) circle(pos, radius, alpha, Floor + .01, Strum); }

// An axis-aligned rectangle x0..x1, z0..z1 in world cells.
export function box(x0, z0, x1, z1, colour, layer, material = undefined) {
  if (x1 - x0 <= .001 || z1 - z0 <= .001) return;
  draw(plane, (x0 + x1) / 2, layer, (z0 + z1) / 2, x1 - x0, z1 - z0, 0, colour, material);
}
export const glowFlat = additive;

// A stand-in rifle lying level: dark body, barrel, wooden stock. Items come out of the castle
// with the corpses, around the target cell.
export function rifle(pos, deg, alpha = 1, layer = Floor + .03) {
  const r = deg * Mathf.Deg2Rad, cx = Math.cos(r), sx = Math.sin(r);
  draw(plane, pos.x, layer, pos.z, .78, .075, -deg, new Color(.10, .10, .12, alpha));
  draw(plane, pos.x + cx * .22, layer + .002, pos.z + sx * .22, .34, .04, -deg, new Color(.30, .31, .34, alpha));
  draw(plane, pos.x - cx * .26, layer + .002, pos.z - sx * .26, .24, .085, -deg, new Color(.36, .22, .11, alpha));
}
