// Writes fields.json: what the Unlimited Blade Works sketches' field layout makes for a few settings and
// landing spots (lib/ubw-pocket.js makeField and standingPose, lib/trace.js cutOf and pommelOf), so
// Program.cs can check the C# port (Source/RimArt/Trace/UbwField.cs, UbwBlade.cs) against it sword by
// sword. Run it again whenever those libraries change their layout:
//   node Tests/Ubw/dump-field.mjs
import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const pocket = await import(pathToFileURL(join(here, '../../Tools/VfxLab/web/sketches/lib/ubw-pocket.js')).href);
const trace = await import(pathToFileURL(join(here, '../../Tools/VfxLab/web/sketches/lib/trace.js')).href);

const v = q => [q.x, q.y, q.z];
function field(settings, keep) {
  const list = pocket.makeField(settings, keep);
  return {
    settings, keep,
    swords: list.map(sw => {
      const b = pocket.standingPose(sw, { x: 0, z: 0 }), cut = trace.cutOf(b, sw.sink);
      return {
        seed: sw.seed, x: sw.x, z: sw.z, d: sw.d, far: sw.far, name: sw.w.name, lean: sw.lean, dir: sw.dir, turn: sw.turn, sink: sw.sink, size: sw.size,
        tip: v(b.tip), A: v(b.A), B: v(b.B), N: v(b.N), L: b.L, top: trace.pommelOf(b).y + .05,
        cut: [cut.x, cut.z, cut.half, cut.D.x, cut.D.z, cut.F.x, cut.F.z],
      };
    }),
  };
}

const Look = pocket.Look;
const landed = [{ x: 0, z: 0 }, { x: -2.2, z: -1.6 }, { x: 2.8, z: 1.2 }, { x: -1.5, z: 3.2 }];
const fields = [
  field({ density: Look.density, hill: Look.hill, beyond: Look.beyond, size: Look.size, lean: Look.lean }, landed),
  field({ density: Look.density, hill: Look.hill, beyond: Look.beyond, size: Look.size, lean: Look.lean }, [{ x: 0, z: 0 }]),
  field({ density: .4, hill: 3, beyond: 10, size: 1.1, lean: 30 }, [{ x: 0, z: 0 }, { x: 4, z: -3 }]),
  field({ density: .15, hill: 8, beyond: 22, size: 1.5, lean: 10 }, []),
];
const out = join(here, 'fields.json');
writeFileSync(out, JSON.stringify({ fields }) + '\n');
console.log(`wrote ${fields.length} fields (${fields.map(f => f.swords.length).join(', ')} swords) to ${out}`);
