// Unlimited Blade Works: close shot (pocket) — Trace kit experiment (2026-09-26), not agreed, not the game.
// Part 3 of the sky plan: the shot that plays when the world has stood its time (20 to 30 s in game), in place
// of v4's closing white with its wall of fire. Paused while it plays; any key skips it. It changes no
// mechanic of the world (PR #65); what follows is the existing return, played under full white.
//
// Order and timings (the plan's, agreed 2026-09-26; keyframes [t, x, y, z, pitch, fov] from the caster, eased
// between each pair, in lib/ubw-reveal.js), from the start of the shot (Pre, 0.4 s, of v4's standing world
// as the game draws it goes first):
//   0.00-0.85  the black bars come in (0.35 s) and the game view blends back into the perspective camera:
//              the reveal's last pose, 26 cells up, 38 degrees down, craning down toward the caster;
//   0.85-1.70  the camera comes down to 2.4 cells, 5 behind the caster, and tilts up to 20 degrees: the gears;
//   1.75-2.35  the sky burns white: white light added over the sky and its gears, under the ground;
//   2.10-2.60  the whole screen goes white; the camera ends at head height looking 30 degrees up, the pose
//              the reveal opened on. Full white: the existing return plays.
// Drawing: through the lab's 3D camera (camera() below, SKETCHING.md) on the reveal's 3D copy of the v4 world
// (lib/ubw-reveal.js). In game: the reveal's second camera, run backwards into the white. Not ported.
import { Color, Overlay } from '../js/engine.js';
import { P } from './lib/six-paths-impact.js';
import { smooth } from './lib/trace.js';
import { CloseKeys, along, casterOf, world, drawWorld, drawFlat, RidgeHeight } from './lib/ubw-reveal.js';
import { Open } from './trace-ubw-world-v4.js';

// v4's world before the shot; the shot's length (the last keyframe); white held at the end; the black bars
// (36 of 383 pixels, as the reveal's) coming in over 0.35 s.
const Pre = .4, Shot = 2.6, Hold = .3, BarH = 36 / 383, WhiteHot = new Color(1, .98, .94);

export default {
  kit: 'Trace', label: 'Unlimited Blade Works: close shot (pocket) (sketch)', scene: false,
  params: {
    stood: P('World has stood (world time at the start)', 6, 0, 30, .5, 'Timing (s)'),
    unblend: P('Game view blends back over', .85, .3, 1.5, .05, 'Timing (s)'),
    burnFrom: P('Sky starts to burn', 1.75, 1, 2.4, .05, 'Timing (s)'),
    burnOver: P('Sky burns over', .6, .2, 1.2, .05, 'Timing (s)'),
    whiteFrom: P('Screen goes white from (ends 2.6)', 2.1, 1.5, 2.5, .05, 'Timing (s)'),
    ridges: P('Ridge height in 3D (x)', RidgeHeight, .3, 2, .05, 'World'),
    cellsTall: P('Game view height (cells)', 36, 20, 60, 1, 'Game view'),
    gameNorth: P('Game view centre, north of the caster (cells)', 7, 0, 14, .5, 'Game view'),
    bars: { label: 'Black bars', value: true, group: 'Screen' },
  },
  duration() { return Pre + Shot + Hold; },
  phases(p) {
    return [{ name: 'World', t: 0 }, { name: 'Blend back', t: Pre }, { name: 'Tilt up', t: Pre + p.unblend }, { name: 'Sky burns', t: Pre + p.burnFrom },
      { name: 'White', t: Pre + p.whiteFrom }, { name: 'Return', t: Pre + Shot }];
  },

  camera(t, p, { origin }) {
    const c = casterOf(origin), game = { cx: c.x, cz: c.z + p.gameNorth, cellsTall: p.cellsTall }, u = t - Pre;
    if (u < 0) return { flat: true, game };
    const q = along(CloseKeys, Math.min(u, Shot));
    return { x: c.x + q.x, y: q.y, z: c.z + q.z, pitch: q.pitch, yaw: 0, fov: q.fov, near: .3, far: 9000, blend: 1 - smooth(u / p.unblend), game };
  },

  draw(t, p, ctx) {
    const sw = Open + p.stood + t, u = t - Pre;
    if (!ctx.camera || ctx.camera.flat) drawFlat(sw, ctx);
    else drawWorld(world(ctx.origin, ctx.scene, ctx.view), sw, { blend: ctx.camera.blend, ridges: p.ridges, burn: smooth((u - p.burnFrom) / p.burnOver) });
    const white = smooth((u - p.whiteFrom) / (Shot - p.whiteFrom));
    if (white > 0) Overlay.Fill(0, 0, 1, 1, WhiteHot.withAlpha(white));
    const bars = p.bars ? BarH * smooth(u / .35) : 0;
    if (bars > 0) { Overlay.Fill(0, 0, 1, bars, Color.clear.withAlpha(1)); Overlay.Fill(0, 1 - bars, 1, bars, Color.clear.withAlpha(1)); }
  },
};
