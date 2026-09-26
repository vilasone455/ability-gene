// Unlimited Blade Works: world v3, horizon (pocket) — Trace kit experiment (2026-09-26), not agreed, not the
// game. Part 2 of the sky plan (the user's challenge: see the sky and its gears in a top-down game, not only
// their shadows): the world while it stands, with a horizon. Part 1 (the reveal shot: a camera looking up at
// the gears, tilting down to this view) and part 3 (the close: tilting back up) are not built; the fire that
// runs out when the world opens moves into part 1, so this sketch starts on the standing world.
//
// The mechanic is the one the first world sketch and the ubw mechanic (PR #65) have; only the picture and the
// pocket map's shape change:
//   - the group lands with the map's north edge North cells (13) north of the caster: room for the verse 3
//     circle (12) plus one. The map stays 40 wide and 20 south of the caster: 40 x 33;
//   - past the north edge the ground squeezes toward a horizon 5 cells past it (lib/ubw-horizon.js), with
//     three ridges on it, swords to the horizon, and above it the sky with the low sun, clouds, smog and seven
//     spoked gears. Nothing past the edge is walkable, so the sky covers no cell a pawn can stand on;
//   - no tiers past the east, west and south edges (they made the map a sunken box): the plain goes on flat;
//   - the depth fixes of 2026-09-26: a haze tied to the camera (0 at the bottom of the screen, 0.3 at the top)
//     over the ground and swords, under the pawns; sword clustering (bare patches and groves, cluster); shadows
//     1.7 times a thing's height (v2 1.1), away from the sun in the sky; embers and ash in front of everything
//     at 1.45 times the camera's pan; the gears' shadows kept but faint (0.06, v2 0.28) and on the map only, since
//     the gears themselves show.
// With the lab camera ~36 cells tall and 7 cells north of the caster (the framing part 1 would end on), the
// sky fills the top fifth of the screen. Pan north for more sky; pan sideways to see the parallax.
//
// Order and timings: the white from the cast fades out 0.00 to 0.50, the world stands for `hold` (20 to 30 s
// in game), the white closes in over `close` with the wall of fire at its edge, as v2.
// Drawing: the lab passes the camera (ctx.view: x, z, zoom, view size); what is past the edge is rebuilt when
// the camera's x moves. Without it, a 36-cell view 7 north of the caster is assumed.
import { Color, MeshPool } from '../js/engine.js';
import { pawn, EnemyColour, Ally } from './lib/goku.js';
import { solid } from './lib/trace.js';
import { draw } from './lib/six-paths-solid.js';
import { Y } from './lib/six-paths-impact.js';
import { SceneNorth } from './lib/ubw.js';
import { MapHalf, White, Tint, Twilight, Look, field, drawField, backstop, sunGlow, hill, embers, cover, fireWall, mapEdge } from './lib/ubw-pocket.js';
import { Ground, terrain, terrainBase, drawTerrain } from './lib/ubw-terrain.js';
import { drawHorizon, mapTerrain, gearShadows, depthHaze, foreground } from './lib/ubw-horizon.js';

const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });
const Caster = new Color(.55, .3, .24);
// Where the other three landed, in cells from the caster: the cast sketch's pawns at verse 1.
const Landed = [{ x: -2.2, z: -1.6, colour: Ally }, { x: 2.8, z: 1.2, colour: EnemyColour }, { x: -1.5, z: 3.2, colour: EnemyColour }];
const Keep = [{ x: 0, z: 0 }, ...Landed.map(q => ({ x: q.x, z: q.z }))];
// The map's plates run NorthReach cells past the edge (drawn squeezed there); the white opens in Open s; the
// closing white and its fire wall start Past cells beyond the drawn field, as v2.
const NorthReach = 14, Open = .5, Past = 14, ClosePow = 1.6, WallHeight = 1.2;
// Plates past the east, west and south edges: near the map's size (v2's 5.5 made the edge show as a change of
// plate size once the tiers were gone).
const OuterPlate = 3.8;

function times(p) {
  const close = Open + p.hold, shut = close + p.close;
  return { close, shut, end: shut + .35, reach: MapHalf + Look.beyond + Past };
}

export default {
  kit: 'Trace', label: 'Unlimited Blade Works: world v3, horizon (pocket) (sketch)', scene: false,
  params: {
    actors: { label: 'Stand-ins and map edge', value: true, group: 'World' },
    north: P('Map edge north of the caster (cells)', 13, 8, 20, 1, 'World'),
    density: P('Swords per cell on the map', Look.density, .1, .8, .01, 'World'),
    cluster: P('Sword clustering (0 even, 1 groves)', 1, 0, 1, .05, 'World'),
    horizon: P('Horizon past the edge (screen cells)', 5, 2, 10, .5, 'Horizon'),
    ridges: P('Ridge height (x)', 1, 0, 2, .05, 'Horizon'),
    haze: P('Camera haze at the top of the screen', .3, 0, .6, .01, 'Horizon'),
    parallax: { label: 'Sky parallax', value: true, group: 'Horizon' },
    gearShadows: P('Gear shadows on the ground', .06, 0, .4, .01, 'Light'),
    shadow: P('Shadow length (x height)', 1.7, .8, 3, .05, 'Light'),
    hold: P('World stands (20-30 s in game)', 6, 1, 12, .5, 'Timing (s)'),
    close: P('White closes in', 1.2, .5, 3, .05, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'White', t: 0 }, { name: 'World stands', t: Open }, { name: 'Close', t: t.close }, { name: 'White', t: t.shut }];
  },

  draw(s, p, { origin: cell, scene, view }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const c = { x: cell.x, z: cell.z + SceneNorth }, north = Math.round(p.north);
    const v = view ?? { cx: c.x, cz: c.z + 7, ppc: 12, halfW: 32, halfH: 18 };
    const base = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32, len = Math.hypot(base.x, base.z) || 1;
    const sun = { x: base.x / len * p.shadow, z: base.z / len * p.shadow }, tint = Color.Lerp(White, Tint, Look.twilight);
    const T = terrain({ ...Ground, outer: OuterPlate, tierStep: 0, southTiers: 0, hill: Look.hill, beyond: Look.beyond, northClip: north + NorthReach });

    // Past the edge and the sky, then the map's ground, light and swords, the camera's haze, the air.
    backstop(c);
    terrainBase(c, T.edge);
    drawHorizon(T, c, north, p.horizon, s, sun, v, tint, { ridges: p.ridges, parallax: p.parallax });
    drawTerrain(mapTerrain(T, north, sun), c, tint, strength);
    gearShadows(c, s, sun, p.gearShadows, north);
    sunGlow(c, sun, Look.twilight);
    hill(c, Look.hill, sun, 1, .5);
    const f = field({ density: p.density, hill: Look.hill, beyond: Look.beyond, size: Look.size, lean: Look.lean, north, cluster: p.cluster }, c, sun, Keep, { ground: T });
    drawField(f, strength, tint);
    depthHaze(c, north, p.horizon, v, p.haze);
    embers('ubwp v3 embers', c, s, 140, 1);
    foreground(c, s, v, 1);

    // The white from the cast fading out; closing, the white coming in from outside with its wall of fire.
    if (s < Open) draw(MeshPool.plane10, v.cx, Y + .06, v.cz, 4 * v.halfW + 20, 4 * v.halfH + 20, 0, White.withAlpha(1 - s / Open), solid);
    if (s >= t.close) {
      const r = t.reach * (1 - Math.min(1, (s - t.close) / p.close)) ** ClosePow;
      cover('ubwp v3 cover', c, r, 1, Y + .06);
      fireWall('ubwp v3 wall', c, r, s, WallHeight, Y + .065);
    }

    if (!p.actors) return;
    mapEdge('ubwp v3 edge', c, north);
    const warm = { tint: Twilight, tintAmount: .3 * Look.twilight };
    pawn(c, Caster, sun, strength, { hair: true, ...warm });
    Landed.forEach(q => pawn({ x: c.x + q.x, z: c.z + q.z }, q.colour, sun, strength, warm));
  },
};
