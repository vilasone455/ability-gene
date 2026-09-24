// Unlimited Blade Works: world (pocket) — Trace kit proposal, not the game. Nothing in Source/RimArt
// draws this yet. The inside of the pocket-map version of the kit's ultimate: what everyone taken sees.
// The home-map side (the chant, the fire along its lines, the white that takes them, the ring left
// burning, the return) is "Unlimited Blade Works: cast (pocket)". The first two UBW sketches, the world
// standing in place on the home map, stay as they were for reference.
//
// What it is for (proposed 2026-09-24 when the user chose the pocket version; none of it agreed yet;
// every number is a placeholder and will be an XML field).
//   Self. The pawn chants up to 3 verses of 2 s. Released after verse 1, 2 or 3 it takes every standing
//   pawn within 6, 9 or 12 cells, allies and enemies alike (downed pawns stay), into a 40 x 40 pocket
//   map made for the cast. Each lands at its offset from the caster, who lands in the middle, on a low
//   hill. The world lasts 20, 25 or 30 s by verse. Every sword a command uses (Full Open, Pin, Draw,
//   Arm, Intercept: the commands sketch) takes 0.5 s off it: the source's rule, the swords already in the
//   world cost nothing, using or breaking them costs the caster. Nobody can leave: the map edge is the
//   world's edge. It ends early if the caster is downed, or on Close. Then every sword breaks and everyone
//   goes back to the cell they were taken from (nearest free cell); items and corpses drop round the
//   cast point. Cooldown 2 days.
//
// Order, with the default timings:
//   0.00  white: the flash that took everyone.
//   0.15  a wall of fire runs out from the caster past the map edge in 1.5 s, slow near the caster and
//         fast far out. Behind it is the world: red-brown cracked earth; swords standing like grave
//         markers at every angle, each its weapon's own size, about 400 on the map, thicker toward the
//         top of the hill round the caster and leaning down its slope, about 500 more past the map edge
//         fading into the haze; the low sun's warm light and long shadows; the shadows of six gears
//         turning overhead. Each sword near the caster flashes its wire outline as the fire passes and a
//         bright line runs up it (the Trace look). The pawns stand where they landed.
//   1.65  the world stands: the gear shadows turn and slide, embers drift up and east (20-30 s in game).
//   5.65  close: the white closes in from the edge in 1.2 s behind a wall of fire; each sword near the
//         caster flashes into light just before it goes; the caster, in the middle, goes last.
//   6.85  white; everyone is back on the home map (the cast sketch).
//
// Sources (researched 2026-09-24): the Type-Moon wiki (a fire with no heat forms the boundary, bright
// light, a barren wasteland, black gears turning in the distance, swords like grave markers, a hill of
// swords, Shirou's world bathed in twilight); anime 2015, Archer vs Berserker and Shirou vs Gilgamesh
// (fire sweeps the ground, then white); FGO and Fate/Extra Record (sepia world, cracked ground, big
// gears overhead); the 2010 movie (white, a screen of fire, then the field of swords).
//
// Drawing: everything is flat on the floor, a level circle or a standing sword (lib/trace.js), so there
// is no per-facing method. The standing field is laid out once and baked into three meshes from one
// atlas (lib/ubw-pocket.js): shadows, ground marks, blades, in draw order, north first. The ground is a
// lab texture (lab/ubw-earth) standing in for the pocket map's terrain; the sky shows only as light: a
// warm tint, a glow on the sun's side, shadows twice the scene's length, the gear shadows. The flame is a
// lab texture (lab/ubw-flame). The pawns and the map-edge line are lab stand-ins ("Stand-ins and map edge").
import { Color } from '../js/engine.js';
import { P, Y, Floor, glow, rand } from './lib/six-paths-impact.js';
import { pawn, EnemyColour, Ally } from './lib/goku.js';
import { smooth, clamp, TraceHot, onScreen, plus } from './lib/trace.js';
import { SceneNorth } from './lib/ubw.js';
import {
  MapHalf, DuskShadow, White, Tint, Twilight, Look, field, drawField, traceOver, floor, patches, backstop, haze, sunGlow, hill, skyGears, embers,
  cover, fireWall, quads, mapEdge,
} from './lib/ubw-pocket.js';

const Caster = new Color(.55, .3, .24);
// Where the other three landed, in cells from the caster: the cast sketch's pawns at verse 1.
const Landed = [{ x: -2.2, z: -1.6, colour: Ally }, { x: 2.8, z: 1.2, colour: EnemyColour }, { x: -1.5, z: 3.2, colour: EnemyColour }];
const Keep = [{ x: 0, z: 0 }, ...Landed.map(q => ({ x: q.x, z: q.z }))];
// The fire starts at Start and runs Past cells beyond the drawn field so the whole view is done. Its
// radius goes as time^SweepPow (slow near the caster); the white closes as (1 - time)^ClosePow. Swords
// within Near cells get the trace (TraceFor s, the scan line in the first ScanFor s) and break into light
// BreakFor s before the white takes them.
const Start = .15, Past = 14, SweepPow = 1.8, ClosePow = 1.6, Near = 14, TraceFor = .35, ScanFor = .25, BreakFor = .2, WallHeight = 1.2;

function times(p) {
  const reach = MapHalf + p.beyond + Past, swept = Start + p.sweep, close = swept + p.hold, shut = close + p.close;
  return { reach, swept, close, shut, end: shut + .35 };
}
const outAt = (s, p, t) => s < Start ? 0 : t.reach * clamp((s - Start) / p.sweep) ** SweepPow;
const inAt = (s, p, t) => t.reach * (1 - clamp((s - t.close) / p.close)) ** ClosePow;
// When the fire going out passes d cells from the caster, and when the white coming in reaches d.
const passes = (d, p, t) => Start + p.sweep * (d / t.reach) ** (1 / SweepPow);
const covers = (d, p, t) => t.close + p.close * (1 - (d / t.reach) ** (1 / ClosePow));

export default {
  kit: 'Trace', label: 'Unlimited Blade Works: world (pocket) (sketch)', scene: false,
  params: {
    actors: { label: 'Stand-ins and map edge', value: true, group: 'World' },
    density: P('Swords per cell on the map', Look.density, .1, .8, .01, 'World'),
    hill: P('Hill of swords radius (cells)', Look.hill, 0, 10, .5, 'World'),
    beyond: P('Field past the map edge (cells)', Look.beyond, 0, 30, 1, 'World'),
    sweep: P('Fire runs out', 1.5, .6, 3, .05, 'Timing (s)'),
    hold: P('World stands (20-30 s in game)', 4, 1, 10, .5, 'Timing (s)'),
    close: P('White closes in', 1.2, .5, 3, .05, 'Timing (s)'),
    size: P('Sword size (x image)', Look.size, .8, 2.2, .05, 'Look'),
    lean: P('Sword lean, most (degrees)', Look.lean, 0, 40, 1, 'Look'),
    twilight: P('Twilight light', Look.twilight, 0, 1, .05, 'Look'),
    gears: P('Gear shadow opacity', Look.gears, 0, .5, .01, 'Look'),
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
    const sun = { x: base.x * DuskShadow, z: base.z * DuskShadow }, tint = Color.Lerp(White, Tint, p.twilight);
    const f = field({ density: p.density, hill: p.hill, beyond: p.beyond, size: p.size, lean: p.lean }, c, sun, Keep);

    // The ground, the light and the air; the field.
    backstop(c);
    floor(c, MapHalf + p.beyond + 8, 8, tint);
    patches(c, MapHalf + p.beyond, 40);
    sunGlow(c, sun, p.twilight);
    hill(c, p.hill, sun, 1);
    skyGears('ubwp world gears', c, s, sun, p.gears);
    drawField(f, strength, tint);
    haze(c, 1);
    embers('ubwp world embers', c, s, 140, 1);

    // The fire going out traces each sword near the caster as it passes.
    if (s < t.swept) f.list.forEach(sw => {
      if (sw.d > Near) return;
      const age = s - passes(sw.d, p, t);
      if (age >= 0 && age < TraceFor) traceOver(sw, sun, strength, .9 * (1 - smooth(age / TraceFor)), age < ScanFor ? smooth(age / ScanFor) : -1);
    });
    // Closing, each sword near the caster flashes into light just before the white takes it.
    if (s >= t.close) {
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
      quads('ubwp world sparks', sparks, TraceHot.withAlpha(.9), glow, Y + .02);
    }

    // Outside the world: white, with the wall of fire at its edge.
    const r = s < t.swept ? outAt(s, p, t) : s >= t.close ? inAt(s, p, t) : Infinity;
    if (r < Infinity) {
      cover('ubwp world cover', c, r, 1, Y + .06);
      fireWall('ubwp world wall', c, r, s, WallHeight, Y + .065);
    }

    if (!p.actors) return;
    mapEdge('ubwp world edge', c);
    const warm = { tint: Twilight, tintAmount: .3 * p.twilight };
    pawn(c, Caster, sun, strength, { hair: true, ...warm });
    Landed.forEach(q => pawn({ x: c.x + q.x, z: c.z + q.z }, q.colour, sun, strength, warm));
  },
};
