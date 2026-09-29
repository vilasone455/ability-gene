// Lab — the real-size pawn stand-in (lib/pawn.js) next to the old two-disc one. Not an effect: a
// reference picture for checking the stand-in against the in-game measurements
// (docs/pawn-height-handoff.md, "Measured numbers") and for seeing where a body point lands.
//
// Left to right, 2 cells apart like the measuring test: the old two-disc stand-in most sketches
// before 2026-09-29 draw (switch "Old stand-in"), then average, fat and hulk from lib/pawn.js.
// Each pawn's cell is outlined on the floor with a dot at its centre (= DrawPos).
//
// Marks (switch "Height marks"), a line across each pawn at its own numbers: white head top,
// blue head centre, yellow neck, orange chest, green waist, red feet. The old stand-in gets head
// top, head centre and feet only. Two faint lines run the whole row at the average real pawn's
// head top (+0.63) and feet (-0.54), so the old stand-in's gap shows at a glance.
//
// Check: shoot at 111 px per cell (the in-game measuring scale) and compare each silhouette's
// top, bottom and width with lib/pawn.js's Bodies table.
import { AltitudeLayer, Color, Meshes, MeshPool } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, target } from './lib/six-paths-impact.js';
import { pawn, at, Bodies } from './lib/pawn.js';

const Mark = AltitudeLayer.MoteOverhead.AltitudeFor(), Floor = AltitudeLayer.Filth.AltitudeFor();
const Spacing = 2, Thin = .014;
const Parts = [
  ['headTop', new Color(1, 1, 1, .9)], ['head', new Color(.45, .75, 1, .9)], ['neck', new Color(1, .90, .25, .9)],
  ['chest', new Color(1, .55, .15, .9)], ['waist', new Color(.35, .95, .45, .9)], ['feet', new Color(1, .25, .25, .9)],
];
const Old = { headTop: .75, head: .58, feet: -.14 };
const oldDisc = Meshes.disc(40, 'lab old stand-in');
const line = (x0, x1, z, colour, layer = Mark, thick = Thin) =>
  draw(MeshPool.plane10, (x0 + x1) / 2, layer, z, x1 - x0, thick, 0, colour);

function cell(c) {
  const k = new Color(1, 1, 1, .35);
  line(c.x - .5, c.x + .5, c.z - .5, k, Floor, .02);
  line(c.x - .5, c.x + .5, c.z + .5, k, Floor, .02);
  draw(MeshPool.plane10, c.x - .5, Floor, c.z, .02, 1, 0, k);
  draw(MeshPool.plane10, c.x + .5, Floor, c.z, .02, 1, 0, k);
  draw(oldDisc, c.x, Mark + .01, c.z, .03, .03, 0, new Color(1, 1, 1, .95));
}

export default {
  kit: 'Lab',
  label: 'Pawn stand-in (sketch)',
  params: {
    old: { label: 'Old stand-in', value: true, group: 'Show' },
    marks: { label: 'Height marks', value: true, group: 'Show' },
    downed: { label: 'Downed', value: false, group: 'Pose' },
    turn: P('Downed turn (degrees)', -90, -180, 180, 5, 'Pose'),
  },
  duration() { return 1; },
  phases() { return []; },

  draw(seconds, p, { origin, scene }) {
    const sun = scene?.shadowVector ?? { x: 0, z: 0 };
    const row = ['average', 'fat', 'hulk'].map((body, i) => ({ body, c: { x: origin.x + (i - .5) * Spacing, z: origin.z } }));
    const oldCell = { x: origin.x - 1.5 * Spacing, z: origin.z };
    if (p.marks) {
      const x0 = (p.old ? oldCell.x : row[0].c.x) - .8, x1 = row[2].c.x + .8;
      line(x0, x1, origin.z + Bodies.average.headTop, new Color(1, 1, 1, .25), Mark - .01, .01);
      line(x0, x1, origin.z + Bodies.average.feet, new Color(1, .25, .25, .25), Mark - .01, .01);
    }
    if (p.old) {
      cell(oldCell);
      target(oldCell, true);   // the old two-disc stand-in: body .22 x .32 at +.18, head .16 x .17 at +.58
      if (p.marks) Parts.filter(([k]) => k in Old).forEach(([k, colour]) => line(oldCell.x - .32, oldCell.x + .32, oldCell.z + Old[k], colour));
    }
    for (const { body, c } of row) {
      const who = { body, downed: p.downed, turn: p.turn, sun };
      cell(c);
      pawn(c, who);
      if (!p.marks) continue;
      const half = Bodies[body].width / 2 + .1;
      for (const [k, colour] of Parts) {
        const m = at(c, k, who);
        if (!p.downed) line(m.x - half, m.x + half, m.z, colour);
        else draw(oldDisc, m.x, Mark, m.z, .035, .035, 0, colour);
      }
    }
  },
};
