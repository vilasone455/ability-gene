// Writes layouts.json: what the Kamui dimension sketch's generator makes for a fixed set of seeds and
// settings, so Program.cs can check the C# port (Source/RimArt/Involute/Kamui/KamuiLayout.cs) against it
// block by block. Run it again whenever lib/kamui.js changes its generator:
//   node Tests/Kamui/dump-layouts.mjs
import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const lib = await import(pathToFileURL(join(here, '../../Tools/VfxLab/web/sketches/lib/kamui.js')).href);

function layout(seed, size, cover, islands, order) {
  const m = lib.generate(seed, { size, cover, islands });
  return {
    seed, size, cover, islands,
    tops: m.tops.map(t => [t.x, t.z, t.w, t.h, t.group, t.shade]),
    lower: m.lower.map(b => [b.x, b.z, b.w, b.h, b.level]),
    outside: m.outside.map(b => [b.x, b.z, b.w, b.h, b.level]),
    mouth: [m.mouth.x, m.mouth.z],
    landings: m.landings.map(c => [c.x, c.z]),
    walkable: m.stats.walkable, islandCount: m.stats.islands,
    order: order ? lib.paintOrder([...m.tops, ...m.lower, ...m.outside]).map(b => [b.x, b.z, b.level]) : null,
  };
}

const layouts = [];
for (let seed = 1; seed <= 40; seed++) layouts.push(layout(seed, 48, .55, 6, seed <= 3));
for (const size of [32, 40, 56, 64]) for (let seed = 1; seed <= 4; seed++) layouts.push(layout(seed, size, .55, 6, false));
for (const cover of [.35, .45, .65, .7]) for (let seed = 1; seed <= 3; seed++) layouts.push(layout(seed, 48, cover, 6, false));
for (const islands of [2, 10]) for (let seed = 1; seed <= 3; seed++) layouts.push(layout(seed, 48, .55, islands, false));

const out = join(here, 'layouts.json');
writeFileSync(out, JSON.stringify({ layouts }) + '\n');
console.log(`wrote ${layouts.length} layouts to ${out}`);
