// Writes paths.json: the flight the Bank Shot sketch's rule (lib/bank-shot-path.js) gives for its three
// layouts at a spread of aims, so Program.cs can check the C# port (Source/RimArt/BankShot/BankShotPath.cs)
// against it corner by corner. Run it again whenever the rule changes:
//   node Tests/BankShot/dump-paths.mjs
import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const lib = await import(pathToFileURL(join(here, '../../Tools/VfxLab/web/sketches/lib/bank-shot-path.js')).href);
const MuzzleAlong = .5, D2R = Math.PI / 180;   // lib/bank-shot.js and the lab's Mathf.Deg2Rad

const paths = [];
for (const scene of Object.keys(lib.Layouts)) {
  const L = lib.Layouts[scene], walls = lib.wallSet(lib.cells(L));
  // The sketch's aim slider runs -12..12; the extra aims send the bullet round every layout.
  for (const offset of [-12, -6, -3, -1.5, -.75, -.25, 0, .25, .5, 1, 1.75, 3, 6, 12, 45, 90, 135, 180, 225, 300]) {
    const aim = L.aim + offset, m = { x: L.caster.x + Math.cos(aim * D2R) * MuzzleAlong, z: L.caster.z + Math.sin(aim * D2R) * MuzzleAlong };
    const p = lib.trace(walls, m, aim, L.enemy);
    paths.push({
      scene, offset,
      pts: p.pts.map(q => [q.x, q.z, q.d]),
      bounces: p.bounces.map(b => [b.x, b.z, b.d, b.cell.x, b.cell.z, b.normal.x, b.normal.z, b.n]),
      end: [p.end.kind, p.end.x, p.end.z, p.end.d],
      length: p.length,
    });
  }
}
const out = join(here, 'paths.json');
writeFileSync(out, JSON.stringify({ maxBounces: lib.MaxBounces, range: lib.Range, paths }) + '\n');
console.log(`wrote ${paths.length} paths to ${out}`);
