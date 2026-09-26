// Nezuko's box: worn — equipment proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed 2026-09-26; the numbers are placeholders and will be XML fields). A
// wooden box worn on the back in a new Back apparel layer. It holds one flesh pawn of body size
// 1.0 or less (every human, any body type; small animals). The wearer moves 15 % x body size
// slower while it is full. Inside, the pawn sleeps: it cannot be hurt, cold, heat and sunlight do
// not reach it, bleeding stops, hunger and recreation pause, rest fills, wounds heal at the no-bed
// rate. It comes out after 12 h at most, or within 1 h of being fully rested.
//
// This sketch only shows how the box sits on a wearer in the four facings, empty (top row) and with
// someone asleep inside (bottom row: the box breathes, the door seam glows pink, z marks rise).
// Left to right the wearers face north, east, south and west.
//
// Drawing: lib/nezuko-box.js draws the box as a true cuboid, so no facing needs its own method.
// Facing south it stands behind the wearer and draws under the pawn layer. The straps show over the
// chest facing south, east and west.
import { Color } from '../js/engine.js';
import { drawBox, figure, Wearer, Ally } from './lib/nezuko-box.js';
import { P } from './lib/six-paths-impact.js';

const Facings = [90, 0, 270, 180];

export default {
  kit: "Nezuko's Box", label: 'Worn (sketch)',
  params: {
    actors: { label: 'Show the wearers', value: true, group: 'Showcase' },
    spacing: P('Spacing (cells)', 1.6, 1, 3, .1, 'Showcase'),
  },
  duration() { return 5.2; },
  phases() { return [{ name: 'Loop', t: 0 }]; },

  draw(s, p, { origin, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    for (let row = 0; row < 2; row++) {
      for (let i = 0; i < 4; i++) {
        const feet = { x: origin.x + (i - 1.5) * p.spacing, z: origin.z + (row === 0 ? 1.1 : -.35) };
        if (p.actors) figure(feet, Wearer, sun, strength);
        drawBox(`worn ${row} ${i}`, feet, Facings[i], { sleeping: row, t: s + i * .4, sun, strength });
      }
    }
  },
};
