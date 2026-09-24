// Unlimited Void, cutscene open — a second picture for the same technique as gojo-unlimited-void.js,
// not the game. Same proposed mechanic (see that file's header); only the open changes. Nothing in
// Source/RimArt draws this yet.
//
// Why: the anime and the Cursed Clash game show the domain from inside, through the victim's eyes: a
// vanishing point, light rushing past, a horizon with the black hole on it. A straight-down camera has
// no vanishing point, so the map cannot show that. This version takes the screen over for about 2 s at
// the open and draws the inside view in screen space, then hands back to the top-down dome for the
// hold. The game is not paused. A mod setting can skip it and use the top-down open.
//
// Order, with the default timings (the top-down file's warm-up, hold and close are unchanged):
//   0.00  raiders walk in; ally inside; one raider outside
//   0.30  warm-up 0.6 s on the map: hand sign, blindfold off, rim light (as the top-down file)
//   0.90  camera: slides onto the caster and zooms to about 16 cells wide over 0.25 s. NOT PREVIEWABLE
//         in the lab; the lab shows one fixed zoom, so set the lab to about 60 px per cell.
//   1.15  white: a white disc bursts out from the caster past the screen edges in 0.15 s behind a bright
//         edge (the game's expanding dome, seen up close) and the splatter burst opens behind the caster
//         (the anime's first frame); the top-down dome opens underneath, hidden
//   1.30  lines 0.8 s: the screen is black; 110 dashes of white, pink and violet light rush outward
//         from a point just above the caster to past the screen edges (the game's speed lines: outward
//         from the centre in screen space is the tunnel); a white flare sits at that point; the caster
//         stands in front with the rim light
//   2.10  hole 0.4 s: the lines curl into a spiral around the flare (the game's vortex) while the black
//         hole opens there: black disc to 3.4 cells, gold ring, teal edge, wisps streaming to the right
//   2.50  shrink 0.6 s, fast first and slow at the end: the camera pulls back to where it was during the
//         first half (at a 16-cell zoom the screen corner is only 9 cells out, so without the pull-back
//         the void already looks like the dome and there is nothing to shrink); the black screen then
//         contracts as a disc with a bright edge from past the screen edges to the true radius; the lines are cut
//         off at the edge, the screen-space hole shrinks from 3.4 to the map hole's radius 2 and slides
//         onto its place (0.6 + 3 x 0.6 cells north of the caster). When the disc reaches the radius it
//         IS the dome on the map (same black floor, same ring), and the last 25 % of the shrink fades
//         the overlay so the frozen pawns inside come through. The infinite inside becomes the bubble.
//   3.10  hold in top-down as the other file, then close and results
//
// Drawing: the overlay is a black quad and additive lines, a flare and the black hole, all in SCREEN
// space in game (pixels, centred on the caster's screen position). The sketch draws them in world
// cells centred on the caster, with the frame size as a slider, which is the same picture at one
// zoom. The C# port draws in an OnGUI pass (GL.LoadPixelMatrix + DrawMeshNow, or Graphics.DrawTexture
// for the quads) and moves the camera with CameraDriver. The caster is drawn a second time above the
// overlay; everything else on the map is under it. The top-down file's draw is called underneath with
// its params mapped, so the two files cannot drift apart.
import { Color, Mathf, Meshes, MeshPool } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, sprite, glow, rand } from './lib/six-paths-impact.js';
import { caster, blackHole, splatter, line, streak, ringAt, whiteGlow, White, Ice, Void, Violet, Pink, EyeBlue, smooth, clamp } from './lib/gojo.js';
import topDown from './gojo-unlimited-void.js';

const Lead = .3, Lines = 110, SwirlMax = 28 * Mathf.Deg2Rad, HoleAbove = 1.7, FlashIn = .05, HoleRadius = 3.4;
const disc = Meshes.disc(96, 'void cutscene disc');
const Over = Y + .15;   // the overlay's altitude; everything of the map is under it, the caster is redrawn over it

function times(p) {
  const cast = Lead, zoom = cast + p.warm, white = zoom + p.zoom, lines = white + p.white, hole = lines + p.lines, dissolve = hole + p.hole, back = dissolve + p.dissolve;
  return { cast, zoom, white, lines, hole, dissolve, back };
}
// The top-down file's params for the same run: its dome opens when the screen goes white, and its hold
// runs on until this file's hold ends.
function under(p, t) {
  const q = {};
  for (const k in topDown.params) q[k] = topDown.params[k].value;
  q.radius = p.radius; q.warm = t.white - Lead; q.open = .35; q.hold = (t.back - t.white - .35) + p.hold;
  return q;
}

export default {
  kit: 'Gojo', label: 'Unlimited Void cutscene (sketch)',
  params: {
    frame: P('Frame width (cells, the lab stand-in for the screen)', 16, 8, 30, .5, 'Showcase'),
    aspect: P('Frame height / width', 1, .4, 1.4, .05, 'Showcase'),
    radius: P('Radius (cells)', 9, 5, 14, .5, 'Shape'),
    warm: P('Warm-up (hand sign)', .6, .2, 1.5, .05, 'Timing (s)'),
    zoom: P('Camera zoom (not previewable)', .25, .1, .6, .05, 'Timing (s)'),
    white: P('White screen', .15, .05, .5, .05, 'Timing (s)'),
    lines: P('Lines rush past', .8, .3, 2, .05, 'Timing (s)'),
    hole: P('Black hole opens', .4, .2, 1, .05, 'Timing (s)'),
    dissolve: P('Void shrinks to the dome', .6, .2, 1.5, .05, 'Timing (s)'),
    hold: P('Top-down hold after the cutscene', 2, .5, 6, .25, 'Timing (s)'),
  },
  duration(p) { const t = times(p); return topDown.duration(under(p, t)); },
  phases(p) {
    const t = times(p);
    return [{ name: 'Raiders close in', t: 0 }, { name: 'Hand sign', t: t.cast }, { name: 'Camera zooms', t: t.zoom }, { name: 'White', t: t.white },
      { name: 'Lines rush past', t: t.lines }, { name: 'Black hole', t: t.hole }, { name: 'Shrinks to the dome', t: t.dissolve }, { name: 'Top-down hold', t: t.back }];
  },
  events(p) {
    const t = times(p);
    return [{ t: t.white, type: 'shake', value: .06 }, { t: t.white, type: 'sound', def: 'AG_Gojo_DomainOpen' }, { t: t.hole, type: 'sound', def: 'AG_Gojo_VoidHole' }];
  },

  draw(s, p, ctx) {
    const t = times(p), o = ctx.origin;
    // The map, as the top-down file draws it, is always underneath.
    topDown.draw(s, under(p, t), ctx);
    if (s < t.white || s >= t.back) return;

    const W = p.frame, H = p.frame * p.aspect, far = Math.hypot(W, H) / 2 + 1;
    const c = { x: o.x, z: o.z + HoleAbove };                                     // the flare and the hole: just above the caster on screen
    const x = s < t.dissolve ? 0 : clamp((s - t.dissolve) / p.dissolve), drop = 1 - (1 - x) * (1 - x);   // 0 inside, 1 back on the map; fast first, slow at the end
    const fade = 1 - clamp((drop - .75) / .25);                                    // the overlay's opacity: only the last quarter of the shrink fades
    const whiteOut = s < t.lines ? 1 : 1 - smooth((s - t.lines) / .2);           // white first, then black
    const since = s - t.white;
    // The overlay is a disc: it bursts out past the screen edges at the open and shrinks back to the
    // true radius at the end, where it is the dome's own floor.
    const rad = s < t.dissolve ? far * smooth(since / p.white) : far + (p.radius - far) * drop, moving = s < t.lines || s >= t.dissolve;

    // --- the screen: black, with the white flash over it, and a bright edge while it moves ---------------------------
    draw(disc, o.x, Over, o.z, rad, rad, 0, Void.withAlpha(fade));
    if (whiteOut > 0) draw(disc, o.x, Over + .001, o.z, rad, rad, 0, White.withAlpha(clamp(since / FlashIn) * whiteOut * fade));
    if (moving) ringAt(o, rad, White.withAlpha(.9 * fade), Over + .1, true, whiteGlow);
    splatter('void cutscene splatter', c, W * .28, s, clamp(since / .1) * (1 - smooth((since - .2) / .6)) * fade);

    // --- the light rushing past: dashes outward from the flare; they curl once the hole starts to open ----------------
    const swirl = SwirlMax * smooth((s - t.hole) / p.hole), lineAlpha = (1 - whiteOut * .8) * fade;
    for (let i = 0; i < Lines; i++) {
      const a0 = rand(i + 700) * Math.PI * 2, speed = 14 + 12 * rand(i + 710), len = 2 + 4.5 * rand(i + 720), cycle = far + len;
      const head = ((s - t.lines) * speed + rand(i + 730) * cycle) % cycle, tail = head - len;
      const r0 = Math.max(.5, tail), r1 = Math.min(rad - .2, head);   // cut off at the disc's edge
      if (r1 - r0 < .2) continue;
      const pts = [];
      for (let k = 0; k <= 6; k++) { const r = r0 + (r1 - r0) * k / 6, a = a0 + swirl * r; pts.push({ x: c.x + Math.cos(a) * r, z: c.z + Math.sin(a) * r }); }
      const pick = rand(i + 740), colour = pick < .55 ? White : pick < .82 ? Pink : Violet;
      line(`void cutscene line ${i}`, pts, .05 + .08 * rand(i + 750), colour.withAlpha(lineAlpha * (.55 + .45 * rand(i + 760))), whiteGlow, Over + .02, 'both');
    }
    // The flare at the centre, until the hole eats it.
    const flare = (1 - smooth((s - t.hole) / (p.hole * .6))) * fade;
    sprite(c, 3.2, 3.2, Violet.withAlpha(.5 * flare), glow, Over + .03);
    sprite(c, 1.6, 1.6, Ice.withAlpha(.9 * flare), glow, Over + .031);
    // A four-point star on the flare (the goku glint draws under the overlay, so it is drawn here).
    if (flare > 0) [0, 90].forEach((deg, i) => {
      const r = (deg + 45 * s) * Mathf.Deg2Rad, l = (i ? .8 : 1.2) * (1.2 + .3 * Math.sin(s * 30)), dx = Math.cos(r) * l, dz = Math.sin(r) * l;
      streak(`void cutscene flare ${i}`, { x: c.x - dx, z: c.z - dz }, { x: c.x + dx, z: c.z + dz }, .2, White.withAlpha(flare), whiteGlow, Over + .032, 4);
    });

    // --- the black hole in screen space, then the caster in front of it ----------------------------------------------
    if (s >= t.hole) {
      // Opens big with a small overshoot; during the dissolve it shrinks and slides onto the map's own
      // black hole (same centre and radius as the top-down file draws it), so the hand-over is seamless.
      const u = smooth((s - t.hole) / p.hole), q = under(p, t);
      const mapCentre = { x: o.x, z: o.z + .6 + q.eyeHeight * .6 }, big = HoleRadius * (u * (1.15 - .15 * u));
      blackHole('void cutscene hole', { x: c.x, z: c.z + (mapCentre.z - c.z) * drop }, big + (q.eye - big) * drop, s, fade, Over + .04);
    }
    caster(o, { x: 0, z: 0 }, 0, { blindfold: 0, sign: 1, rim: fade, alpha: fade, layer: Over + .12 });
    sprite({ x: o.x, z: o.z + .3 }, 1.2, 1.5, EyeBlue.withAlpha(.35 * fade), glow, Over + .11);
  },
};
