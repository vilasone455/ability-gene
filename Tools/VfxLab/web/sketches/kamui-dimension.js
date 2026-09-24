// Kamui's dimension — the Obito kit's pocket map. Replaces the Fold organ's 32 x 32 cave (gravel, sand,
// pools, glowing plants, wreckage, violet tint).
//
// Ported to C# (2026-09-24), not yet run in game: Source/RimArt/Involute/Kamui/KamuiLayout.cs is this
// generator, exact for a seed, size, cover and island count (Tests/Kamui checks it against a dump of
// this file's generator); KamuiGraphics.cs draws the field with the same meshes and atlas (the lab's
// recording "Kamui dimension: field" matches "Show: empty map" here). The pocket map itself is
// GenStep_InvoluteVolume and MapComponent_KamuiDimension (RimArts debug window, Involute, "go to the
// volume" on a pawn with the Fold organ). The stand-ins, the held ring and the rounds are not ported.
//
// What it is (agreed as placeholders 2026-09-24; every number will be an XML field). The dimension
// Obito warps into, and where absorbed pawns and the rounds that pass through him end up:
//   - 48 x 48 cells. A field of stone blocks hanging in a void, after the anime (Kakashi vs Obito
//     inside the dimension) and Storm 4's Kamui stage.
//   - Main top, 14 x 10, in the middle. Obito and allies arrive at its centre.
//   - 20-30 more tops of 3-9 cells a side, covering about 55 % of the map. A top grown off another
//     touches it along 2+ cells and can be walked onto; the rest sit 1-2 cells apart.
//   - At least 6 islands: tops that no walkable top touches (up to 45 cells each). Each absorbed
//     enemy lands on a different one, away from Obito. Held enemies are stunned while inside.
//   - The void cannot be walked on and does not stop shots, so a round sent in (launched from any
//     map edge cell toward a random walkable cell) flies over it.
//   - Lower blocks, 1-3 cells down, fill about half of the void inside the map. Past the map edge
//     the blocks go on for 20 cells, going dark; they only rise above the walkable level north,
//     east and west of the map, never south of it.
//   - Roofed and lit evenly (invisible cold lights in game), the same at every hour.
//
// Drawing: the field is one mesh baked in painter's order (north first, lower first on a tie),
// textured from one atlas (lib/kamui.js). Per block: the south face (edge colour fading to void
// over 2 cells, then solid void, because every block is a pillar out of the abyss), two corner
// lines on the face, the top, and its four edge lines. Walkable tops are drawn exactly on their
// cells, lighter in the middle with faint darker patches. Blocks never overlap on the ground, so
// that order is the whole cover rule. Colours are measured from the references (see the palettes
// in lib/kamui.js); "still" is the darker Narutopedia picture. Stand-ins: the pawns, the held
// ring, the rounds and their entry ring (the Kamui swirl is not designed yet). The atlas is a lab
// texture for now; the port makes it a PNG from the same formulas. Level shapes only, no facing.
import { Color } from '../js/engine.js';
import { hash } from '../js/standins.js';
import { P } from './lib/six-paths-impact.js';
import {
  generate, bakeField, atlasFor, drawVoid, drawEdgeFade, figure, heldMark, round, draw,
  Palettes, L, Obito, Mask, Ally, Enemy, Skin,
} from './lib/kamui.js';

// Decided values.
const RoundSpeed = 26, FirstRound = .3, RoundGap = .42, Rounds = 6;
const Untinted = new Color(1, 1, 1, 1);                 // the atlas holds the colours

// The field for a set of generator values is built and baked once, so scrubbing does not redo it.
const cache = new Map();
function fieldFor(p) {
  const key = `${p.seed}:${p.size}:${p.cover}:${p.islands}:${p.lower}:${p.outside}`;
  if (!cache.has(key)) {
    const map = generate(p.seed, { size: p.size, cover: p.cover, islands: p.islands });
    const blocks = [...map.tops, ...(p.lower ? map.lower : []), ...(p.outside ? map.outside : [])];
    cache.set(key, { map, mesh: bakeField(blocks, key) });
  }
  return cache.get(key);
}

export default {
  kit: 'Obito', label: 'Kamui dimension (sketch)', scene: false, compareWith: 'Kamui dimension: field',
  params: {
    seed: P('Map seed', 1, 1, 60, 1, 'Map'),
    size: P('Map size (cells)', 48, 32, 64, 8, 'Map'),
    cover: P('Walkable share', .55, .35, .7, .05, 'Map'),
    islands: P('Islands at least', 6, 2, 10, 1, 'Map'),
    lower: { label: 'Lower blocks in the gaps', value: true, group: 'Map' },
    outside: { label: 'Blocks past the edge', value: true, group: 'Map' },
    palette: { label: 'Colours', value: 'fight', options: ['fight', 'still'], group: 'Look' },
    scenario: { label: 'Show', value: 'Obito inside', options: ['Obito inside', 'rounds sent in', 'empty map'], group: 'Showcase' },
    view: { label: 'Centre the view on', value: 'the whole map', options: ['the whole map', 'the main top'], group: 'Showcase' },
  },
  duration() { return FirstRound + (Rounds - 1) * RoundGap + 1.6; },
  phases() { return [{ name: 'Map', t: 0 }, { name: 'Rounds arrive', t: FirstRound }]; },
  events() { return []; },

  draw(s, p, { origin, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = (scene?.sun?.strength ?? .32) * .6;
    const pal = Palettes[p.palette], { map, mesh } = fieldFor(p), N = map.size;
    const cell0 = { x: origin.x - .5, z: origin.z - .5 };
    const corner = p.view === 'the main top'
      ? { x: cell0.x - map.mouth.x, z: cell0.z - map.mouth.z }
      : { x: cell0.x - N / 2, z: cell0.z - N / 2 };
    const at = c => ({ x: corner.x + c.x + .5, z: corner.z + c.z + .5 });

    drawVoid(corner, N, pal);
    draw(mesh, corner.x, L.field, corner.z, 1, 1, 0, Untinted, atlasFor(p.palette));
    if (p.outside) drawEdgeFade(corner, N, pal);

    if (p.scenario === 'empty map') return;
    // Stored pawns: two allies standing by the arrival cell and one downed; a held enemy per island.
    const m = map.mouth;
    figure(at({ x: m.x + 2, z: m.z }), Ally, Skin, sun, strength);
    figure(at({ x: m.x - 2, z: m.z + 1 }), Ally, Skin, sun, strength);
    figure(at({ x: m.x - 1, z: m.z - 2 }), Ally, Skin, sun, strength, { downed: true });
    map.landings.slice(0, 3).forEach(c => { heldMark(at(c), s); figure(at(c), Enemy, Skin, sun, strength); });

    // Obito can only be in here while he is solid, so he and the rounds are separate scenarios:
    // rounds come in while he is intangible outside.
    if (p.scenario === 'Obito inside') { figure(at(m), Obito, Mask, sun, strength); return; }
    for (let i = 0; i < Rounds; i++) {
      const side = Math.floor(hash(i, 1, p.seed) * 4), along = Math.floor(hash(i, 2, p.seed) * N);
      const from = side === 0 ? { x: N - 1, z: along } : side === 1 ? { x: along, z: N - 1 } : side === 2 ? { x: 0, z: along } : { x: along, z: 0 };
      const to = map.walkCells[Math.floor(hash(i, 3, p.seed) * map.walkCells.length)];
      round(at(from), at(to), s - FirstRound - i * RoundGap, RoundSpeed);
    }
  },
};
