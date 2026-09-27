// Obito — Kamui: Warp. A picture for the agreed mechanic; nothing in Source/RimArt draws it yet.
//
// Mechanic (agreed 2026-09-27, numbers are XML placeholders): 1 s warm-up, then Obito is inside
// the Kamui dimension (the pocket map, kamui-dimension.js). From inside he picks any revealed cell
// of the map he left; a warning mark shows there for 0.5 s, then he comes out. Cooldown 15 s after
// the exit, cast 2 Echo charge. He is solid during the warm-up (Phase and Warp are not used at
// once). The sketch compresses his time inside into "Time inside".
//
// Beats (source: Obito's Kamui is his right eye; he winds into a spiral that ends in the eye and
// comes out of one the same way):
//   0.05        the Mangekyō flashes red in the mask's eye hole.
//   0.10        space starts turning round the eye: 4 dark arms with pale leading lines.
//   0.15-1.00   his body winds into the eye, slow at first and fastest at the end, while the swirl
//               widens and spins faster. At 1.00 he is gone and the swirl shuts into the point.
//   exit - 0.5  the warning mark: a swirl opens at head height over the exit cell, with a pale ring
//               on the floor under it.
//   exit        he unwinds out of it over 0.4 s; the swirl slows and fades.
//
// Drawing: lib/obito.js. The whole body is one picture on a grid mesh bent by vortex(); the swirl
// is flat on the screen, so there is no per-facing method. Stand-ins: the pawn pictures.
import { P, L, Edge, smooth, clamp, figure, obitoKind, eye, vortex, swirl, shut, glint, circle } from './lib/obito.js';

// Rule numbers (XML later) and decided looks.
const WarmUp = 1, Mark = .5, Unwind = .4, Tail = .35, Glint = .05, Open = .1, WindFrom = .15, Reach = 1.1, Lead = .5;

function times(p) {
  const gone = WarmUp, markAt = gone + p.inside, out = markAt + Mark, whole = out + Unwind;
  return { gone, markAt, out, whole, end: whole + Tail };
}

export default {
  kit: 'Obito', label: 'Kamui: Warp (sketch)',
  params: {
    facing: { label: 'Obito faces', value: 'south', options: ['south', 'east', 'west', 'north'], group: 'Showcase' },
    aim: P('Exit direction (degrees)', 20, 0, 360, 5, 'Showcase'),
    distance: P('Exit distance (cells)', 7, 2, 20, 1, 'Showcase'),
    raider: { label: 'Raider near the exit', value: true, group: 'Showcase' },
    inside: P('Time inside (compressed)', .6, .2, 3, .1, 'Timing (s)'),
    turns: P('Turns before he is gone', 1.5, .5, 3, .1, 'Shape'),
    size: P('Swirl radius (cells)', .55, .3, 1.2, .05, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [
      { name: 'Warm-up: winds into the eye', t: 0 }, { name: 'Inside the dimension', t: t.gone },
      { name: 'Warning mark', t: t.markAt }, { name: 'Unwinds out', t: t.out },
    ];
  },
  events() { return []; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Math.PI / 180;
    const exit = { x: o.x + Math.round(Math.cos(a) * p.distance), z: o.z + Math.round(Math.sin(a) * p.distance) };
    const kind = obitoKind(p.facing);
    if (p.raider) figure('warp raider', 'enemy', { x: exit.x + 2, z: exit.z - 1 }, sun, strength);

    // Going in: the body winds into the right eye.
    const e0 = eye(o, p.facing);
    if (s < t.gone) {
      const k = Math.pow(clamp((s - WindFrom) / (WarmUp - WindFrom)), 2);
      figure('warp obito in', kind, o, sun, strength, { warp: vortex(e0, k, { turns: p.turns, reach: Reach, lead: Lead }), shadow: 1 - k });
    }
    glint(e0, s - Glint);
    if (s < t.gone + .15) {
      const open = smooth((s - Open) / .35), close = 1 - smooth((s - t.gone + .05) / .2);
      const r = p.size * open * (.35 + .65 * Math.sqrt(clamp((s - WindFrom) / (WarmUp - WindFrom)))) * close;
      swirl('warp in', e0, r, -(s * 4 + s * s * 5), open);
    }
    shut(e0, s - t.gone, p.size);

    // Coming out: the warning mark, then he unwinds out of it.
    if (s < t.markAt) return;
    const e1 = eye(exit, p.facing), m = s - t.markAt;
    const open = smooth(m / .3), fade = 1 - smooth((s - t.out - .1) / (Unwind - .1 + Tail));
    const r = p.size * (.6 * open + .4 * smooth((s - t.out) / Unwind));
    swirl('warp out', e1, r, -m * 5, open * fade, { layer: L.pawn - .02 });   // behind him as he comes out
    circle(exit, .42 + .08 * open, .65 * open * (1 - smooth((s - t.out) / .3)), L.floor, Edge);
    if (s >= t.out) {
      const k = 1 - smooth((s - t.out) / Unwind);
      figure('warp obito out', kind, exit, sun, strength, { warp: vortex(e1, k, { turns: p.turns, reach: Reach, lead: Lead }), shadow: 1 - k });
    }
  },
};
