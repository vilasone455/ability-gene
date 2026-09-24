// Writes layouts.json: what the Castle sketch's generator makes for a fixed set of seeds, so
// Program.cs can check the C# port (Source/RimArt/InfinityCastle/CastleLayout.cs) against it
// castle by castle. Run it again whenever lib/infinity-castle.js changes its generator:
//   node Tests/InfinityCastle/dump-layouts.mjs
import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const lib = await import(pathToFileURL(join(here, '../../Tools/VfxLab/web/sketches/lib/infinity-castle.js')).href);

// Infinity (a room no doorway reaches) is not JSON; -1 stands for it.
const finite = d => (d === Infinity ? -1 : d);

function layout(seed, count, withLanterns) {
  const c = lib.generate(seed, count);
  return {
    seed, count,
    rooms: c.rooms.map(r => [r.kind, r.x, r.z, r.w, r.h]),
    doors: c.doors.map(d => [d.a, d.b, d.axis, d.cells[0][0], d.cells[0][1], d.cells[1][0], d.cells[1][1]]),
    dist: c.rooms.map(r => finite(c.dist.get(r.id))),
    arrival6: lib.arrivalRooms(c, 6).map(r => r.id),
    arrival8: lib.arrivalRooms(c, 8).map(r => r.id),
    lanterns: withLanterns ? c.rooms.map(r => lib.lanternsOf(r)) : null,
  };
}

const layouts = [];
for (let seed = 1; seed <= 60; seed++) layouts.push(layout(seed, 38, seed <= 5));
for (let seed = 1; seed <= 10; seed++) layouts.push(layout(seed, 30, false), layout(seed, 45, false));

// The Castle sketch draws its void with reach 90; Shift and Crush use 50 with seeds 3 and 12.
const depth = [[1, 90], [7, 90], [3, 50], [12, 50]].map(([seed, reach]) => ({
  seed, reach,
  items: lib.depthItems(seed, reach).map(it =>
    [it.r.id, it.flight ?? 0, it.r.kind ?? '', it.r.w ?? 0, it.r.h ?? 0, it.level, it.x, it.z, it.rot, it.driftA, it.driftP, it.spin]),
}));

const out = join(here, 'layouts.json');
writeFileSync(out, JSON.stringify({ layouts, depth }) + '\n');
console.log(`wrote ${out}: ${layouts.length} castles, ${depth.length} depth lists`);
