// Unlimited Blade Works: world v4, sword crest (pocket) — Trace kit experiment (2026-09-26), not agreed, not
// the game. Options 1 + 4 of the sky options (2026-09-26): v3 ("world v3, horizon") bends the ground past the
// map edge into a horizon; the 2D games the user pointed at and Final Fantasy VI cut it instead. A ridge
// ends the top-down ground and behind it hangs a picture drawn from the side that pans slower than the map,
// and that picture keeps moving even when the camera is still (the Blackjack's clouds). lib/ubw-crest.js has
// the numbers; in short:
//   - the crest: a bank just past the north edge (foot 0.15 to 0.5 cells past it), about `crest` cells tall
//     with a wobble and dips, backlit by the low sun: the plates' cracked earth in shade on its face, a lit rim,
//     swords packed on its top as dark silhouettes with a lit edge, a thicket behind it, its shadow and the long
//     shadows of some of its swords on the map's north cells. It is the hill of swords seen from below its top;
//   - behind it, drawn from the side: v3's sky, sun, clouds, smog and seven gears; far mountains, a middle and
//     a near ridge; the plain between with 14 rows of swords shrinking toward the horizon. Every part moves by
//     its own share of the camera's pan (0 the sun to 0.45 the nearest row), the horizon by 0.25 up and down,
//     so panning north opens more plain and sky behind the crest;
//   - moving: smoke bands drifting east over the crest (0.3, 0.6, 1.0 cells a second), embers rising from
//     behind it, gears turning at 14 / radius degrees a second (v3: 2 to 6 whatever the size).
// The mechanic is v3's and PR #65's: the group lands with the map's north edge `north` (13) cells north of the
// caster, the map 40 x 33, nothing past the edge walkable. The crest stands past the edge, so no walkable cell
// rises (pawns cannot be lifted). Kept from v3: the map's plates and swords with clustering, the camera haze
// (up to the crest's top), embers and ash in front, faint gear shadows (0.06), long shadows, the white.
// With the lab camera ~36 cells tall and 7 north of the caster, the horizon sits `horizon` (2.6) cells past the
// edge: 27.6 % of the screen is above the crest (v3: 19.4 % above its horizon), with the mountains and bits of
// the far plain showing between the crest's swords. Pan north to open the backdrop; pan sideways for the parallax.
//
// Order and timings as v3: the white from the cast fades out 0.00 to 0.50, the world stands for `hold`
// (20 to 30 s in game), the white closes in over `close` with the wall of fire at its edge.
// Drawing: the lab passes the camera (ctx.view); without it, a 36-cell view 7 north of the caster is assumed.
import { Color, MeshPool } from '../js/engine.js';
import { pawn, EnemyColour, Ally } from './lib/goku.js';
import { solid } from './lib/trace.js';
import { draw } from './lib/six-paths-solid.js';
import { Y } from './lib/six-paths-impact.js';
import { SceneNorth } from './lib/ubw.js';
import { MapHalf, White, Tint, Twilight, Look, field, drawField, backstop, sunGlow, hill, embers, cover, fireWall, mapEdge } from './lib/ubw-pocket.js';
import { Ground, terrain, terrainBase, baked, drawTerrain } from './lib/ubw-terrain.js';
import { gearShadows, depthHaze, foreground } from './lib/ubw-horizon.js';
import { CrestFoot, CrestWobble, crestOf, drawCrest, drawBackdrop } from './lib/ubw-crest.js';

const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });
const Caster = new Color(.55, .3, .24);
// Where the other three landed, in cells from the caster: the cast sketch's pawns at verse 1.
const Landed = [{ x: -2.2, z: -1.6, colour: Ally }, { x: 2.8, z: 1.2, colour: EnemyColour }, { x: -1.5, z: 3.2, colour: EnemyColour }];
const Keep = [{ x: 0, z: 0 }, ...Landed.map(q => ({ x: q.x, z: q.z }))];
// The white opens in Open s; the closing white and its fire wall start Past cells beyond the drawn field, as
// v3. Plates past the east, west and south edges as v3; the map's sword clustering as v3's default; the
// gears' shadows on the map as v3's default.
const Open = .5, Past = 14, ClosePow = 1.6, WallHeight = 1.2, OuterPlate = 3.8, Cluster = 1, GearShadows = .06;

function times(p) {
  const close = Open + p.hold, shut = close + p.close;
  return { close, shut, end: shut + .35, reach: MapHalf + Look.beyond + Past };
}

export default {
  kit: 'Trace', label: 'Unlimited Blade Works: world v4, sword crest (pocket) (sketch)', scene: false,
  params: {
    actors: { label: 'Stand-ins and map edge', value: true, group: 'World' },
    north: P('Map edge north of the caster (cells)', 13, 8, 20, 1, 'World'),
    density: P('Swords per cell on the map', Look.density, .1, .8, .01, 'World'),
    crest: P('Crest height (cells)', 1.6, .6, 3, .05, 'Crest'),
    crestSwords: P('Swords per cell along the crest', 2.2, .5, 4, .1, 'Crest'),
    horizon: P('Horizon past the edge at the usual framing (cells)', 2.6, 0, 6, .1, 'Backdrop'),
    parallax: P('Backdrop parallax (x each part\'s share)', 1, 0, 2, .05, 'Backdrop'),
    haze: P('Camera haze at the top of the screen', .3, 0, .6, .01, 'Backdrop'),
    smoke: P('Smoke over the crest (opacity)', .32, 0, .8, .01, 'Motion'),
    updraft: P('Embers rising past the crest', 40, 0, 120, 1, 'Motion'),
    spin: P('Gear spin (x 14 / radius degrees a second)', 1, 0, 3, .05, 'Motion'),
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
    // The map's plates end under the crest's foot.
    const T = terrain({ ...Ground, outer: OuterPlate, tierStep: 0, southTiers: 0, hill: Look.hill, beyond: Look.beyond, northClip: north + CrestFoot + CrestWobble });
    const k = crestOf(c, north, p.crest, p.crestSwords, sun);

    // Behind the crest, then the map's ground, the crest, light and swords, the camera's haze, the air.
    backstop(c);
    terrainBase(c, T.edge);
    drawBackdrop(c, s, sun, v, k, { north, horizon: p.horizon, parallax: p.parallax, spin: p.spin, smoke: p.smoke, updraft: Math.round(p.updraft), tint, edge: T.edge });
    drawTerrain(baked(T, sun), c, tint, strength);
    drawCrest(k, strength, tint);
    gearShadows(c, s, sun, GearShadows, north);
    sunGlow(c, sun, Look.twilight);
    hill(c, Look.hill, sun, 1, .5);
    const f = field({ density: p.density, hill: Look.hill, beyond: Look.beyond, size: Look.size, lean: Look.lean, north, cluster: Cluster }, c, sun, Keep, { ground: T });
    drawField(f, strength, tint);
    depthHaze(c, north, k.meanTop - c.z - north, v, p.haze);
    embers('ubwp v4 embers', c, s, 140, 1);
    foreground(c, s, v, 1);

    // The white from the cast fading out; closing, the white coming in from outside with its wall of fire.
    if (s < Open) draw(MeshPool.plane10, v.cx, Y + .06, v.cz, 4 * v.halfW + 20, 4 * v.halfH + 20, 0, White.withAlpha(1 - s / Open), solid);
    if (s >= t.close) {
      const r = t.reach * (1 - Math.min(1, (s - t.close) / p.close)) ** ClosePow;
      cover('ubwp v4 cover', c, r, 1, Y + .06);
      fireWall('ubwp v4 wall', c, r, s, WallHeight, Y + .065);
    }

    if (!p.actors) return;
    mapEdge('ubwp v4 edge', c, north);
    const warm = { tint: Twilight, tintAmount: .3 * Look.twilight };
    pawn(c, Caster, sun, strength, { hair: true, ...warm });
    Landed.forEach(q => pawn({ x: c.x + q.x, z: c.z + q.z }, q.colour, sun, strength, warm));
  },
};
