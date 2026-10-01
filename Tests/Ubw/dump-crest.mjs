// Writes crest.json: what the world v4 sketch (trace-ubw-world-v4.js) makes of its ground, its field and its
// crest at the sketch's defaults: the plates ending under the crest (lib/ubw-terrain.js makeTerrain with
// northClip), the swords ending at the map's north edge and clustering (lib/ubw-pocket.js makeField with
// north and cluster), the crest's foot, ground and top along x and its means (lib/ubw-crest.js crestOf), the
// ridges' profiles and the rows (Ridges, rowOf), so Program.cs can check the C# port (Source/RimArt/Trace/
// UbwCrest.cs, UbwTerrain.cs, UbwField.cs) against it. Run it again whenever those libraries change:
//   node Tests/Ubw/dump-crest.mjs
import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const lib = p => import(pathToFileURL(join(here, '../../Tools/VfxLab/web/sketches/lib/', p)).href);
const terrain = await lib('ubw-terrain.js');
const pocket = await lib('ubw-pocket.js');
const crest = await lib('ubw-crest.js');

// The v4 sketch's defaults: map edge 13 north, crest 1.6 tall, 2.2 swords a cell, shadows 1.7 x height along
// the lab scene's shadow vector.
const north = 13, H = 1.6, perCell = 2.2, base = { x: -.45, z: -.32 }, len = Math.hypot(base.x, base.z);
const sun = { x: base.x / len * 1.7, z: base.z / len * 1.7 };
const rules = { ...terrain.Ground, outer: 3.8, tierStep: 0, southTiers: 0, hill: pocket.Look.hill, beyond: pocket.Look.beyond, northClip: north + crest.CrestFoot + crest.CrestWobble };
const keep = [{ x: 0, z: 0 }, { x: -2.2, z: -1.6 }, { x: 2.8, z: 1.2 }, { x: -1.5, z: 3.2 }];
const T = terrain.makeTerrain(rules, 1);
const settings = { density: pocket.Look.density, hill: pocket.Look.hill, beyond: pocket.Look.beyond, size: pocket.Look.size, lean: pocket.Look.lean, north, cluster: 1 };
const field = pocket.makeField(settings, keep, T.heightAt);
const k = crest.crestOf({ x: 0, z: 0 }, north, H, perCell, sun);
const xs = Array.from({ length: 477 }, (_, i) => -88 + i * .37);

const out = {
  north, H, perCell, sun, rules, keep, settings,
  plates: T.plates.map(p => p && [p.minX, p.maxX, p.minZ, p.maxZ, p.h, p.tier, p.poly.length]),
  bottom: T.bottom,
  swords: field.map(sw => [sw.seed, sw.x, sw.z, sw.lift]),
  cluster: xs.map((x, i) => pocket.clusterAt(x, (i % 41) - 20)),
  crest: { xs, foot: xs.map(x => k.footZ(x) - k.edge), ground: xs.map(x => k.groundZ(x) - k.edge), top: xs.map(x => k.topZ(x) - k.edge), meanTop: k.meanTop, meanGround: k.meanGround },
  ridges: crest.Ridges.map(R => [R.base, R.h, R.f, R.ph, R.p, R.haze, R.swords, R.tall ?? 0]),
  rows: Array.from({ length: 14 }, (_, q) => { const r = crest.rowOf(q); return [r.d, r.p, r.gap, r.tall, r.haze]; }),
};
writeFileSync(join(here, 'crest.json'), JSON.stringify(out) + '\n');
console.log(`wrote crest.json: ${T.plates.filter(Boolean).length} plates, ${field.length} swords, ${xs.length} crest samples, mean top ${k.meanTop.toFixed(4)}`);
