// Writes terrains.json: what the world v2 sketch's ground makes for a few rules and seeds (lib/ubw-terrain.js
// makeTerrain, lib/ubw-sky.js ridgeOf) and the field standing on it (lib/ubw-pocket.js makeField with
// heightAt), so Program.cs can check the C# port (Source/RimArt/Trace/UbwTerrain.cs) plate by plate. Run it
// again whenever those libraries change their layout:
//   node Tests/Ubw/dump-terrain.mjs
import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const lib = p => import(pathToFileURL(join(here, '../../Tools/VfxLab/web/sketches/lib/', p)).href);
const terrain = await lib('ubw-terrain.js');
const sky = await lib('ubw-sky.js');
const pocket = await lib('ubw-pocket.js');

function dump(rules, seed, keep) {
  const T = terrain.makeTerrain(rules, seed), ridge = sky.ridgeOf(T);
  const settings = { density: pocket.Look.density, hill: rules.hill, beyond: rules.beyond, size: pocket.Look.size, lean: pocket.Look.lean };
  const field = pocket.makeField(settings, keep, T.heightAt);
  return {
    rules, seed, keep,
    seeds: T.seeds.map(s => [s.x, s.z, s.sp]),
    plates: T.plates.map(p => p && {
      poly: p.poly.map(q => [q.x, q.z, q.nb, q.g]), sgn: p.sgn, minX: p.minX, maxX: p.maxX, minZ: p.minZ, maxZ: p.maxZ, h: p.h, tier: p.tier, shade: p.shade,
    }),
    bottom: T.bottom, reach: T.reach, edge: T.edge,
    ridge: { x0: ridge.x0, n: ridge.n, zs: Array.from(ridge.zs) },
    heights: Array.from({ length: 41 }, (_, k) => T.heightAt(-40 + k * 2, 7 - k * .35)),
    swords: field.map(sw => [sw.seed, sw.x, sw.z, sw.lift]),
  };
}

const G = terrain.Ground, base = { ...G, hill: pocket.Look.hill, beyond: pocket.Look.beyond };
const landed = [{ x: 0, z: 0 }, { x: -2.2, z: -1.6 }, { x: 2.8, z: 1.2 }, { x: -1.5, z: 3.2 }];
const terrains = [
  dump(base, 1, landed),
  dump(base, 2, [{ x: 0, z: 0 }]),
  dump({ ...base, tiers: 2, southTiers: 1, plate: 4, gap: .2, hill: 3 }, 3, []),
  dump({ ...base, tiers: 5, southTiers: 0, hillStep: .3, beyond: 22 }, 7, [{ x: 0, z: 0 }]),
];
const out = join(here, 'terrains.json');
writeFileSync(out, JSON.stringify({ terrains }) + '\n');
console.log(`wrote ${terrains.length} terrains (${terrains.map(t => t.plates.filter(Boolean).length + ' plates, ' + t.swords.length + ' swords').join('; ')}) to ${out}`);
