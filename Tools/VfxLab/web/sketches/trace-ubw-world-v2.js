// Unlimited Blade Works: world v2, depth (pocket) — Trace kit experiment (2026-09-25), not agreed, not the
// game. The pocket world of "Unlimited Blade Works: world (pocket)" with the ground given height, after what
// Kamui's dimension showed: the same mechanic (that sketch's header has it), the same order and timings, the
// same swords; only the ground changes. lib/ubw-terrain.js has the rules and the drawing. The first world
// sketch stays as it was.
//
// What changes on screen:
//   - the earth is plates with cracks between them, in four shades, each plate its own window of the earth
//     so nothing tiles; the cracks vary in width, some plates are twice the size, hairline cracks wander
//     over them;
//   - on the map the plates step up and down by 0.08 cells and the hill rises in three terraces of 0.18,
//     on smaller plates so the rings have more steps, shown by their faces and the lit lips along their
//     crests, and the hill's shaded side is darker. Small on purpose: pawns are drawn on their cells and
//     cannot be lifted, so the walkable ground cannot really rise (the Kamui rule);
//   - past the map edge the ground rises north, east and west in three tiers of 1.2 cells and drops south
//     the same way (its own tier count, a slider), with faces, lips and cast shadows; the swords of the
//     field stand on the tiers; along the north edge of the drawn ground the plates get up to 5 cells more:
//     the far ridge;
//   - north of the ridge the sky begins (lib/ubw-sky.js): the twilight gradient, the low sun in the
//     north-east, nine near-black gears hanging in it, three of them partly behind the ridge, and smog. The
//     gears' shadows on the map are cast from those bodies along the sun (before, the shadows stood for
//     gears never drawn). Only the north can have a sky: height is drawn as a shift north, so the top of
//     the screen is the far side of an oblique view; south is toward the camera, east and west are
//     sideways. The haze past the map is cut off at the ridge so the sky keeps its colour;
//   - the fire, the white, the embers, the pawns: as before.
// Order and timings: as the first world sketch (white 0.00, the fire runs out 0.15 to 1.65, the world stands
// to 5.65, the white closes in to 6.85). "Show: the ground only" leaves the swords out, to judge the ground.
//
// Drawing: see lib/ubw-terrain.js and lib/ubw-sky.js. The sword size, lean, twilight and gear shadow
// opacity are the passed world's (Look in lib/ubw-pocket.js), no longer sliders here; the plate step and
// the hill's plate size are baked (Ground in lib/ubw-terrain.js).
import { Color } from '../js/engine.js';
import { P, Y, glow, rand } from './lib/six-paths-impact.js';
import { pawn, EnemyColour, Ally } from './lib/goku.js';
import { smooth, clamp, TraceHot, onScreen, plus } from './lib/trace.js';
import { SceneNorth } from './lib/ubw.js';
import {
  MapHalf, DuskShadow, White, Tint, Twilight, Look, field, drawField, traceOver, patches, backstop, haze, sunGlow, hill, embers,
  cover, fireWall, quads, mapEdge,
} from './lib/ubw-pocket.js';
import { Ground, terrain, baked, terrainBase, drawTerrain } from './lib/ubw-terrain.js';
import { sky, hazeToRidge } from './lib/ubw-sky.js';

const Caster = new Color(.55, .3, .24);
// Where the other three landed, in cells from the caster: the cast sketch's pawns at verse 1.
const Landed = [{ x: -2.2, z: -1.6, colour: Ally }, { x: 2.8, z: 1.2, colour: EnemyColour }, { x: -1.5, z: 3.2, colour: EnemyColour }];
const Keep = [{ x: 0, z: 0 }, ...Landed.map(q => ({ x: q.x, z: q.z }))];
// As the first world sketch: the fire starts at Start and runs Past cells beyond the drawn field; its radius
// goes as time^SweepPow; the white closes as (1 - time)^ClosePow. Swords within Near cells get the trace
// (TraceFor s, the scan line in the first ScanFor s) and break into light BreakFor s before the white.
const Start = .15, Past = 14, SweepPow = 1.8, ClosePow = 1.6, Near = 14, TraceFor = .35, ScanFor = .25, BreakFor = .2, WallHeight = 1.2;

function times(p) {
  const reach = MapHalf + p.beyond + Past, swept = Start + p.sweep, close = swept + p.hold, shut = close + p.close;
  return { reach, swept, close, shut, end: shut + .35 };
}
const outAt = (s, p, t) => s < Start ? 0 : t.reach * clamp((s - Start) / p.sweep) ** SweepPow;
const inAt = (s, p, t) => t.reach * (1 - clamp((s - t.close) / p.close)) ** ClosePow;
const passes = (d, p, t) => Start + p.sweep * (d / t.reach) ** (1 / SweepPow);
const covers = (d, p, t) => t.close + p.close * (1 - (d / t.reach) ** (1 / ClosePow));

export default {
  kit: 'Trace', label: 'Unlimited Blade Works: world v2, depth (pocket) (sketch)', scene: false,
  params: {
    actors: { label: 'Stand-ins and map edge', value: true, group: 'World' },
    density: P('Swords per cell on the map', Look.density, .1, .8, .01, 'World'),
    hill: P('Hill of swords radius (cells)', Look.hill, 0, 10, .5, 'World'),
    beyond: P('Field past the map edge (cells)', Look.beyond, 0, 30, 1, 'World'),
    sweep: P('Fire runs out', 1.5, .6, 3, .05, 'Timing (s)'),
    hold: P('World stands (20-30 s in game)', 4, 1, 10, .5, 'Timing (s)'),
    close: P('White closes in', 1.2, .5, 3, .05, 'Timing (s)'),
    tierStep: P('Tier height past the edge (cells)', Ground.tierStep, 0, 2.5, .1, 'Depth'),
    tiers: P('Tiers past the edge', Ground.tiers, 1, 5, 1, 'Depth'),
    plate: P('Plate size on the map (cells)', Ground.plate, 2, 6, .2, 'Depth'),
    gap: P('Crack width (cells)', Ground.gap, .04, .4, .02, 'Depth'),
    hillStep: P('Hill terrace step (cells)', Ground.hillStep, 0, .3, .02, 'Depth'),
    southTiers: P('Tiers past the south edge', Ground.southTiers, 0, 5, 1, 'Depth'),
    show: { label: 'Show', value: 'the world', options: ['the world', 'the ground only'], group: 'Showcase' },
    skyOn: { label: 'Sky past the north ridge', value: true, group: 'Showcase' },
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'White', t: 0 }, { name: 'Fire runs out', t: Start }, { name: 'World stands', t: t.swept }, { name: 'Close', t: t.close }, { name: 'White', t: t.shut }];
  },
  events() { return [{ t: Start, type: 'shake', value: .02 }]; },

  draw(s, p, { origin: cell, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const c = { x: cell.x, z: cell.z + SceneNorth };
    const base = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const sun = { x: base.x * DuskShadow, z: base.z * DuskShadow }, tint = Color.Lerp(White, Tint, Look.twilight);
    const T = terrain({ ...Ground, plate: p.plate, gap: p.gap, tierStep: p.tierStep, tiers: Math.round(p.tiers), southTiers: Math.round(p.southTiers), hillStep: p.hillStep, hill: p.hill, beyond: p.beyond });
    const b = baked(T, sun);
    const f = field({ density: p.density, hill: p.hill, beyond: p.beyond, size: Look.size, lean: Look.lean }, c, sun, Keep, { ground: T });
    const swords = p.show === 'the world';

    // The ground with its height, the light and the air; the field standing on it.
    backstop(c);
    terrainBase(c, T.edge);
    drawTerrain(b, c, tint, strength);
    patches(c, MapHalf + p.beyond, 40);
    sunGlow(c, sun, Look.twilight);
    hill(c, p.hill, sun, 1, .5);
    if (swords) drawField(f, strength, tint);
    if (p.skyOn) { hazeToRidge(T, c, 1); sky('ubwp v2 sky', T, c, s, sun, Look.gears); } else haze(c, 1);
    embers('ubwp v2 embers', c, s, 140, 1);

    // The fire going out traces each sword near the caster as it passes.
    if (swords && s < t.swept) f.list.forEach(sw => {
      if (sw.d > Near) return;
      const age = s - passes(sw.d, p, t);
      if (age >= 0 && age < TraceFor) traceOver(sw, sun, strength, .9 * (1 - smooth(age / TraceFor)), age < ScanFor ? smooth(age / ScanFor) : -1);
    });
    // Closing, each sword near the caster flashes into light just before the white takes it.
    if (swords && s >= t.close) {
      const sparks = [];
      f.list.forEach(sw => {
        if (sw.d > Near) return;
        const lead = covers(sw.d, p, t) - s;
        if (lead <= 0 || lead > BreakFor) return;
        const u = 1 - lead / BreakFor, mid = onScreen(plus(sw.b.tip, sw.b.A, sw.b.L * .6));
        traceOver(sw, sun, strength, .95 * smooth(u));
        for (let i = 0; i < 3; i++) {
          const a = rand(sw.seed + i * 3) * Math.PI * 2;
          sparks.push({ x: mid.x + Math.cos(a) * u * .45, z: mid.z + Math.sin(a) * u * .35 + u * .15, w: .08, h: .08 });
        }
      });
      quads('ubwp v2 sparks', sparks, TraceHot.withAlpha(.9), glow, Y + .02);
    }

    // Outside the world: white, with the wall of fire at its edge.
    const r = s < t.swept ? outAt(s, p, t) : s >= t.close ? inAt(s, p, t) : Infinity;
    if (r < Infinity) {
      cover('ubwp v2 cover', c, r, 1, Y + .06);
      fireWall('ubwp v2 wall', c, r, s, WallHeight, Y + .065);
    }

    if (!p.actors) return;
    mapEdge('ubwp v2 edge', c);
    const warm = { tint: Twilight, tintAmount: .3 * Look.twilight };
    pawn(c, Caster, sun, strength, { hair: true, ...warm });
    Landed.forEach(q => pawn({ x: c.x + q.x, z: c.z + q.z }, q.colour, sun, strength, warm));
  },
};
