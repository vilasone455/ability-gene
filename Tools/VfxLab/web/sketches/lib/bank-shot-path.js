// Bank Shot gun: the ricochet rule with no drawing in it, so it runs in Node for checks and ports
// to C# as one static class. Cells are integers on the map grid; a wall cell covers
// [x - .5, x + .5] x [z - .5, z + .5]. A bullet is a point that flies level in a straight line,
// mirrors off the first wall face it crosses (the x or the z face, whichever it crosses first),
// and stops at the first pawn it comes within HitR of, at its MaxBounces + 1 wall contact
// (it embeds), or at Range cells of flight.
export const MaxBounces = 3, Range = 30, HitR = .38;
const Step = .01;

// Stand-in layouts. Cells are relative to the sketch origin. Runs are inclusive cell lines.
export const Layouts = {
  // A corner: the enemy stands east of a wall the caster cannot see past. One bounce off the
  // wall to the north puts the bullet behind it.
  corner: {
    caster: { x: -4, z: -2 }, enemy: { x: 3, z: -2 }, aim: 45,
    runs: [{ x0: 0, z0: -6, x1: 0, z1: -1 }, { x0: -6, z0: 2, x1: 4, z1: 2 }],
  },
  // A corridor with two staggered pillars. The enemy waits behind the first pillar. Two bounces,
  // south wall then north wall, bring the bullet round the pillar. Aim window about 3.5 degrees.
  corridor: {
    caster: { x: -5, z: -1 }, enemy: { x: 3, z: 1 }, aim: -27,
    runs: [{ x0: -6, z0: 2, x1: 8, z1: 2 }, { x0: -6, z0: -2, x1: 8, z1: -2 }, { x0: 1, z0: -1, x1: 1, z1: 0 }, { x0: 4, z0: 0, x1: 4, z1: 1 }],
  },
  // A room with one door. The enemy hugs the inside of the door wall where nothing outside can
  // see them. The bullet goes in through the door and walks three walls to reach them. Aim
  // window about 1.75 degrees.
  room: {
    caster: { x: -5, z: -2 }, enemy: { x: 1, z: -1 }, aim: 25.4,
    runs: [{ x0: 0, z0: -3, x1: 6, z1: -3 }, { x0: 0, z0: 3, x1: 6, z1: 3 }, { x0: 0, z0: -2, x1: 0, z1: -1 }, { x0: 0, z0: 1, x1: 0, z1: 2 }, { x0: 6, z0: -2, x1: 6, z1: 2 }],
  },
};

export function cells(layout) {
  const out = [], seen = new Set();
  for (const r of layout.runs) {
    const n = Math.max(Math.abs(r.x1 - r.x0), Math.abs(r.z1 - r.z0));
    for (let i = 0; i <= n; i++) {
      const c = { x: r.x0 + Math.sign(r.x1 - r.x0) * i, z: r.z0 + Math.sign(r.z1 - r.z0) * i }, id = `${c.x},${c.z}`;
      if (!seen.has(id)) { seen.add(id); out.push(c); }
    }
  }
  return out;
}
export const wallSet = list => new Set(list.map(c => `${c.x},${c.z}`));
const inWall = (walls, x, z) => walls.has(`${Math.round(x)},${Math.round(z)}`);

// The flight. Returns the corner points, the bounces (point, wall cell, face normal, distance
// along the flight, index), the end (point, distance, 'hit' | 'embed' | 'range') and the length.
export function trace(walls, start, aimDeg, enemy) {
  let x = start.x, z = start.z, dx = Math.cos(aimDeg * Math.PI / 180), dz = Math.sin(aimDeg * Math.PI / 180), d = 0;
  const pts = [{ x, z, d: 0 }], bounces = [];
  let end = null;
  while (d < Range) {
    const nx = x + dx * Step, nz = z + dz * Step;
    if (enemy && Math.hypot(nx - enemy.x, nz - enemy.z) < HitR) { end = { x: nx, z: nz, d: d + Step, kind: 'hit' }; break; }
    if (inWall(walls, nx, nz)) {
      const cell = { x: Math.round(nx), z: Math.round(nz) };
      // Which face did the ray cross first: the plane x = cell.x -/+ .5 or z = cell.z -/+ .5?
      const px = cell.x - Math.sign(dx) * .5, pz = cell.z - Math.sign(dz) * .5;
      const tx = dx !== 0 && Math.round(x) !== cell.x ? (px - x) / dx : Infinity;
      const tz = dz !== 0 && Math.round(z) !== cell.z ? (pz - z) / dz : Infinity;
      const onX = tx <= tz, t = Math.max(0, Math.min(onX ? tx : tz, Step));
      const bx = x + dx * t, bz = z + dz * t;
      const normal = onX ? { x: -Math.sign(dx), z: 0 } : { x: 0, z: -Math.sign(dz) };
      d += t; x = bx; z = bz;
      if (bounces.length >= MaxBounces) { end = { x, z, d, kind: 'embed', normal, cell }; break; }
      bounces.push({ x, z, d, normal, cell, n: bounces.length + 1 });
      pts.push({ x, z, d });
      if (onX) dx = -dx; else dz = -dz;
      continue;
    }
    x = nx; z = nz; d += Step;
  }
  if (!end) end = { x, z, d, kind: 'range' };
  pts.push({ x: end.x, z: end.z, d: end.d });
  return { pts, bounces, end, length: end.d };
}

// Straight line of sight from a to b: the first wall point on it, or null when it is clear.
export function blocked(walls, a, b) {
  const L = Math.hypot(b.x - a.x, b.z - a.z), n = Math.ceil(L / .05);
  for (let i = 1; i <= n; i++) {
    const u = i / n, x = a.x + (b.x - a.x) * u, z = a.z + (b.z - a.z) * u;
    if (inWall(walls, x, z)) return { x, z, u };
  }
  return null;
}
// A point at distance d along the flight, and the direction there.
export function along(path, d) {
  const pts = path.pts;
  for (let i = 1; i < pts.length; i++) {
    if (d <= pts[i].d || i === pts.length - 1) {
      const a = pts[i - 1], b = pts[i], L = b.d - a.d || 1, u = Math.max(0, Math.min(1, (d - a.d) / L));
      const dx = b.x - a.x, dz = b.z - a.z, len = Math.hypot(dx, dz) || 1;
      return { x: a.x + dx * u, z: a.z + dz * u, dx: dx / len, dz: dz / len, seg: i - 1 };
    }
  }
  return { ...pts[0], dx: 1, dz: 0, seg: 0 };
}
