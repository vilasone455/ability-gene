// Shadow double — Shadow plexus ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (the user's draft, numbers are placeholders, none of it balanced yet). Target a
// cell within 24.9 cells (fixed, not scaled by light) that is at least 30 % lit. 1 s cast, 60 s
// cooldown, lasts 20 s. The carrier's shadow goes there and stands as a dark figure. While it
// stands, every other ability is cast from its cell and uses its light level. It copies the
// carrier's steps one for one; a wall stops it. It is not a pawn and cannot be hit. It ends when
// the time runs out, when its cell goes dark, or when anyone crosses the line between it and the
// carrier. That one line may cross dark cells, so the carrier can stay in the dark.
//
// Order (times with the default sliders). Night, one campfire, the carrier 9 cells away in the dark:
//   0.00  the carrier's shadow comes off their feet and slides over the ground as a flat figure,
//         leaving a thin line behind it
//   1.00  at the lit cell it stands up out of the floor in 0.35 s: a black figure with an indigo
//         edge, a pool at its feet, wisps coming off it. The carrier has no shadow while it is out
//   1.75  the carrier takes 2 steps; the double takes the same 2 steps
//   3.20  Shadow imitation is cast from the double: the line leaves the double's feet and holds a
//         raider by the fire. The range ring is round the double and uses the double's light level
//   end   Time runs out: the imitation line runs back, the double sinks into the floor and the
//         flat shadow slides home along the thin line.
//         The fire goes out: the raider's cell drops under 30 % first and the imitation line dies,
//         then the double's cell does and the double bursts into shreds; the thin line snaps back.
//
// Drawing: the double is the two-disc stand-in in shadow colour, so it needs no per-facing method
// here; in game it would be the carrier's own silhouette. The pale trace over the thin line is a
// lab aid so the line can be seen under the night overlay. Fire, night, pawns are lab stand-ins.
import { AltitudeLayer, Mathf, Meshes } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Lift, Y, band, trail } from './lib/six-paths-impact.js';
import {
  lighting, nightOverlay, fire, pawn, path, shadowLine, pool, grip, shreds, rangeRing, walked,
  CarrierColour, EnemyColour, Shade, Fringe, RangeTint, DarkBelow, FireRadius, LineLayer, NightLayer,
} from './lib/shadow-plexus.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const disc = Meshes.disc(32, 'double disc');
const pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
// The rule's numbers and decided looks.
const ImitationRange = 19.9, Rise = .35, StepPause = .12, LineOut = .6, HoldFor = 1.4, LineBack = .45, Home = .6, Die = 1.2, Burst = .25, Tail = .6;
const TieWidth = .07, LineWidth = .17, PoolRadius = .42;
const Scenarios = ['time runs out', 'the fire goes out'];

function plan(p, o) {
  const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a), mode = Scenarios.indexOf(p.scenario);
  const place = (along, across) => ({ x: o.x + along * ca - across * sa, z: o.z + along * sa + across * ca });
  const per = p.stepTime + StepPause, stood = p.cast + Rise, walkStart = stood + .4, castStart = walkStart + p.steps * per + .3, heldAt = castStart + LineOut;
  const hearth = place(.6, -1.6), enemy = place(4, 1), stand = place(0, p.steps), dieStart = heldAt + .6;
  const on = (s) => mode === 1 ? clamp(1 - (s - dieStart) / Die) : 1;
  const darkAt = (pos) => { for (let s = dieStart; s < dieStart + Die; s += .01) if (lighting(true, null, [{ ...hearth, radius: FireRadius, on: on(s) }]).level(pos) < DarkBelow) return s; return dieStart + Die; };
  const gone0 = mode === 1 ? darkAt(stand) : 0, release = mode === 0 ? heldAt + HoldFor : Math.min(darkAt(enemy), gone0), gone = mode === 0 ? release + LineBack : gone0;
  return { place, mode, stood, walkStart, castStart, heldAt, hearth, enemy, on, release, gone, end: gone + (mode === 0 ? Rise + Home : Burst + LineBack) + Tail };
}

// The flat shadow that slides over the floor: a body strip and a head, pointing along deg.
function flatFigure(key, pos, deg, scale, alpha) {
  if (scale <= 0 || alpha <= 0) return;
  const r = deg * Mathf.Deg2Rad, ux = Math.cos(r), uz = Math.sin(r), a = [], b = [];
  for (let i = 0; i <= 5; i++) { const u = i / 5, w = lerp(.19, .13, u) * scale, x = pos.x + ux * u * .9 * scale, z = pos.z + uz * u * .9 * scale; a.push({ x: x - uz * w, z: z + ux * w }); b.push({ x: x + uz * w, z: z - ux * w }); }
  band(`${key} flat`, a, b, Shade.withAlpha(.94 * alpha), LineLayer + .006);
  draw(disc, pos.x + ux * 1.02 * scale, LineLayer + .006, pos.z + uz * 1.02 * scale, .16 * scale, .16 * scale, 0, Shade.withAlpha(.94 * alpha));
}
// The standing double. up 0..1 is how far it has risen out of the floor.
function silhouette(key, pos, up, alpha, s) {
  if (up <= 0 || alpha <= 0) return;
  for (const [grow, colour, layer] of [[.03, Fringe, 0], [0, Shade, .002]]) {
    draw(disc, pos.x, pawnLayer + layer, pos.z + .18 * up, .22 + grow, (.32 + grow) * up, 0, colour.withAlpha(.95 * alpha));
    draw(disc, pos.x, pawnLayer + layer + .001, pos.z + .58 * up, (.16 + grow) * lerp(.6, 1, up), (.17 + grow) * up, 0, colour.withAlpha(.95 * alpha));
  }
  for (let i = 0; i < 4; i++) {                                     // wisps: they leave the shoulders, lift 0.5 cells and thin out
    const u = (s * .7 + i / 4) % 1, x = pos.x + (i % 2 ? .2 : -.2) + Math.sin(u * 5 + i) * .08, h = (.55 + u * .5) * up;
    trail(`${key} wisp ${i}`, [{ x, z: pos.z + h * Lift }, { x: x + .04, z: pos.z + (h + .12) * Lift }, { x: x - .02, z: pos.z + (h + .26) * Lift }], .07 * (1 - u), Shade.withAlpha(.8 * (1 - u) * alpha * up), Y + .01);
  }
}

export default {
  kit: 'Shadow plexus', label: 'Shadow double (sketch)',
  params: {
    scenario: { label: 'How it ends', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    aim: P('Direction to the lit cell (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Carrier to the lit cell (cells)', 9, 4, 14, .5, 'Showcase'),
    steps: P('Steps the carrier takes', 2, 0, 3, 1, 'Showcase'),
    range: { label: 'Show the range ring round the double', value: true, group: 'Showcase' },
    cast: P('Cast: the shadow slides out', 1, .4, 2, .05, 'Timing (s)'),
    stepTime: P('One step', .45, .2, 1, .05, 'Timing (s)'),
  },
  duration(p) { return plan(p, { x: 0, z: 0 }).end; },
  phases(p) {
    const t = plan(p, { x: 0, z: 0 });
    return [{ name: 'Shadow slides out', t: 0 }, { name: 'Stands up', t: p.cast }, { name: 'Copies the steps', t: t.walkStart }, { name: 'Casts from the double', t: t.castStart },
      { name: t.mode ? 'Raider goes dark: line dies' : 'Imitation ends', t: t.release }, { name: t.mode ? 'Double goes dark' : 'Double sinks', t: t.gone }];
  },
  events() { return []; },

  draw(s, p, { origin: o, scene }) {
    const t = plan(p, o), { place, mode } = t;
    if (s < 0 || s >= t.end) return;
    const hearth = { ...t.hearth, radius: FireRadius, on: t.on(s) }, L = lighting(true, scene, [hearth]);
    nightOverlay('double', o, L);
    fire(hearth, s);

    const moved = walked(s - t.walkStart, p.stepTime, StepPause, p.steps);
    const carrier = place(-p.distance, moved), spot = place(0, moved), deg = p.aim, after = s - t.gone;
    // Where the shadow is: sliding out, standing, or (time runs out) sliding home.
    const homeward = mode === 0 ? smooth((after - Rise) / Home) : 0, out = s < p.cast ? smooth(s / p.cast) : 1 - homeward;
    const flatAt = { x: lerp(carrier.x, spot.x, out), z: lerp(carrier.z, spot.z, out) };
    const up = smooth((s - p.cast) / Rise) * (mode === 0 ? 1 - smooth(after / Rise) : 1), seen = mode === 1 ? 1 - smooth(after / Burst) : 1;
    const away = mode === 0 ? homeward < 1 : after < Burst + LineBack;

    // The thin line back to the carrier. It is the only line allowed over dark cells.
    const tie = mode === 1 && after > Burst ? 1 - smooth((after - Burst) / LineBack) : out;
    if (away) {
      const pts = path(carrier, spot, 0, tie, s, .06);
      shadowLine('double tie', pts, TieWidth, 1, s, { flare: false, point: false });
      const n = pts.map((q, i) => { const d = pts[Math.min(pts.length - 1, i + 1)], c = pts[Math.max(0, i - 1)], len = Math.hypot(d.x - c.x, d.z - c.z) || 1; return { x: (d.z - c.z) / len * .02, z: -(d.x - c.x) / len * .02 }; });
      band('double tie trace', pts.map((q, i) => ({ x: q.x + n[i].x, z: q.z + n[i].z })), pts.map((q, i) => ({ x: q.x - n[i].x, z: q.z - n[i].z })), RangeTint.withAlpha(.3), NightLayer + .02);
    }
    flatFigure('double', flatAt, deg, 1 - up, away ? seen : 0);
    pool('double pool', spot, PoolRadius * up * seen, 1, s);

    // Imitation cast from the double.
    const lineOut = 1 - (1 - clamp((s - t.castStart) / LineOut)) ** 2, since = s - t.release, grab = smooth((s - t.heldAt) / .25) * (1 - smooth(since / .25));
    if (s >= t.castStart && since < LineBack) shadowLine('double imitation', path(spot, t.enemy, 0, since < 0 ? lineOut : 1 - smooth(since / LineBack), s, .1), LineWidth, 1, s);
    if (p.range) rangeRing(spot, ImitationRange, L.level(spot), smooth((s - t.castStart + .2) / .2) * (1 - smooth(since / .4)));
    pool('double held', t.enemy, PoolRadius * grab, 1, s);

    pawn('double carrier', carrier, CarrierColour, L, { shadow: away ? 0 : 1 });
    pawn('double enemy', t.enemy, EnemyColour, L, { tint: .55 * grab });
    grip('double grip', t.enemy, grab, s, 4, .35);
    shreds('double freed', t.enemy, since, 7);
    silhouette('double figure', spot, up, seen, s);
    if (mode === 1) shreds('double burst', { x: spot.x, z: spot.z + .3 }, after, 16, .55, 1.1);
  },
};
