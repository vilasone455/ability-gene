// Unlimited Blade Works: reveal shot (pocket) — Trace kit (2026-09-26). Ported 2026-10-01 (Source/RimArt/Trace/UbwReveal*.cs,
// UbwShot.cs, Kit/UbwShotCamera.cs, Kit/UbwRevealWindow.cs); the recording "Trace: unlimited blade works: reveal" is it.
// Part 1 of the sky plan (the user's challenge: see the sky and its gears, not only their shadows): the shot
// that plays at the take, before the world of v4 ("world v4, sword crest") stands. The game is paused while it
// plays and any key skips it. It changes no mechanic of the world (PR #65); it is only a picture.
//
// Order and timings (the plan's, agreed 2026-09-26, keyframes [t, x, y, z, pitch, fov] from the caster, eased
// between each pair, in lib/ubw-reveal.js):
//   0.00-0.50  the white of the take fades onto the sky: the camera at head height (1.7 cells, 3.6 south of
//              the caster) looking 30 degrees up, north, at the gears;
//   1.25-2.50  the camera tilts down to 6 degrees below the horizon, over the crest to the plain, the ridges
//              and the mountains; from 1.2 the fire runs out from the caster to 130 cells in 1.6 s (radius ~
//              time^1.7) and the swords rise out of the ground behind it, ring by ring (the map's, the
//              crest's, the rows' on the plain; the ones past 130 cells as it gets there);
//   2.50-3.30  the camera cranes up to 13 cells, keeping the horizon in frame;
//   3.15-4.20  the picture blends from the perspective camera into the game view (36 cells tall, centred 7
//              north of the caster): each point moves on the screen from where the camera sees it to where
//              v4 draws it; the camera goes on to 26 cells up, 38 degrees down;
//   4.20       hand-over: the lab draws v4 itself from here (in game: the second camera turns off);
//   4.15-4.55  the black bars leave; the world's timer starts.
// Drawing: through the lab's 3D camera (camera() below, SKETCHING.md): a 3D copy of v4's world
// (lib/ubw-reveal.js). In game: a second Unity camera above the map camera, drawing that copy far to near; the
// blend done on the CPU; pawns as their portraits; UI hidden; paused like the gravship cutscene; a mod setting.
import { Color, Overlay } from '../js/engine.js';
import { P } from './lib/six-paths-impact.js';
import { smooth, clamp } from './lib/trace.js';
import { RevealKeys, along, casterOf, world, drawWorld, drawFlat, RidgeHeight } from './lib/ubw-reveal.js';
import { Open } from './trace-ubw-world-v4.js';

// The camera's last keyframe and the hand-over; the end; the black bars (36 of 383 pixels, as the plan's
// widget), leaving from BarsOff over 0.4 s.
const HandOver = 4.2, End = 4.6, BarH = 36 / 383, BarsOff = 4.15, WhiteHot = new Color(1, .98, .94);

const fireAt = (t, p) => t < p.fireFrom ? -1 : Math.pow(clamp((t - p.fireFrom) / p.fireRun), 1.7) * p.fireTo;
const blendAt = (t, p) => smooth((t - p.blendFrom) / (HandOver - p.blendFrom));

export default {
  kit: 'Trace', label: 'Unlimited Blade Works: reveal shot (pocket) (sketch)', scene: false,
  params: {
    white: P('White of the take fades out', .5, .1, 1.5, .05, 'Timing (s)'),
    fireFrom: P('Fire starts', 1.2, .5, 2.5, .05, 'Timing (s)'),
    fireRun: P('Fire runs out', 1.6, .6, 3, .05, 'Timing (s)'),
    blendFrom: P('Blend into the game view starts (ends 4.2)', 3.15, 2.5, 4, .05, 'Timing (s)'),
    fireTo: P('Fire runs out to (cells)', 130, 40, 200, 5, 'Fire'),
    ridges: P('Ridge height in 3D (x)', RidgeHeight, .3, 2, .05, 'World'),
    cellsTall: P('Game view height (cells)', 36, 20, 60, 1, 'Game view'),
    gameNorth: P('Game view centre, north of the caster (cells)', 7, 0, 14, .5, 'Game view'),
    hand: { label: 'Hand over to the world v4 drawing at 4.2 s', value: true, group: 'Game view' },
    bars: { label: 'Black bars', value: true, group: 'Screen' },
  },
  duration() { return End; },
  phases(p) {
    return [{ name: 'White', t: 0 }, { name: 'Sky and gears', t: p.white }, { name: 'Tilt down, fire', t: 1.25 }, { name: 'Crane up', t: 2.5 },
      { name: 'Blend', t: p.blendFrom }, { name: 'Hand-over', t: HandOver }, { name: 'World', t: BarsOff + .4 }];
  },

  camera(t, p, { origin }) {
    const c = casterOf(origin), game = { cx: c.x, cz: c.z + p.gameNorth, cellsTall: p.cellsTall };
    if (p.hand && t >= HandOver) return { flat: true, game };
    const q = along(RevealKeys, Math.min(t, HandOver));
    return { x: c.x + q.x, y: q.y, z: c.z + q.z, pitch: q.pitch, yaw: 0, fov: q.fov, near: .3, far: 9000, blend: blendAt(t, p), game };
  },

  draw(t, p, ctx) {
    // World time: v4's at the hand-over, so gears, clouds, smoke and embers carry straight on into it.
    const sw = Open + (t - HandOver);
    if (!ctx.camera || ctx.camera.flat) drawFlat(Math.max(Open, sw), ctx);
    else {
      const r = fireAt(t, p), flames = 1 - smooth((r - .7 * p.fireTo) / (.3 * p.fireTo));
      drawWorld(world(ctx.origin, ctx.scene, ctx.view), sw, { fire: r, reach: p.fireTo, flames, blend: ctx.camera.blend, ridges: p.ridges });
    }
    const white = 1 - smooth(t / p.white);
    if (white > 0) Overlay.Fill(0, 0, 1, 1, WhiteHot.withAlpha(white));
    const bars = p.bars ? BarH * (1 - smooth((t - BarsOff) / .4)) : 0;
    if (bars > 0) { Overlay.Fill(0, 0, 1, bars, Color.clear.withAlpha(1)); Overlay.Fill(0, 1 - bars, 1, bars, Color.clear.withAlpha(1)); }
  },
};
