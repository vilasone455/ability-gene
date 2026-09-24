// Accelerator kit: the pieces shared by Plasma and the sketches that follow it (Uplift, the touch
// shove, the manipulation Apply flash). Not a sketch itself, so it is not listed in index.js.
//
// Everything is a level circle, a quad or a strip, so nothing needs a per-facing method. Every
// function takes times and keeps no state.
//
// Port notes: the stand-in is the goku pawn with a white hair disc; the arm is one plane10 quad
// turned to the aim.
//
// Palette (decided 2026-09-24): monochrome. Everything Accelerator controls is white light with a
// black edge: the lane, the pull ring, caught rounds, the Apply stroke. The only hue is inside the
// plasma ball and its burst, a desaturated blue as the anime draws it (Index episode 14: a white
// sphere with blue light), and never violet. No other kit uses white and black.
import { Color, Mathf, Meshes, MeshPool } from '../../js/engine.js';
import { draw } from './six-paths-solid.js';
import { Y, sprite, glow, rand } from './six-paths-impact.js';
import { pawn, strip, streak, whiteGlow, White, Skin, pawnLayer, Chest } from './goku.js';
export { pawn, streak, whiteGlow, White, Skin, pawnLayer, Chest };

const disc = Meshes.disc(32, 'accelerator disc'), thinRing = Meshes.band(.93, 1, 48, 'accelerator thin ring');
const clamp = Mathf.Clamp01, TAU = Math.PI * 2;

export const Shirt = new Color(.13, .13, .15), HairWhite = new Color(.93, .93, .96);
export const Air = new Color(.93, .94, .96), AirDeep = new Color(.72, .78, .86), Edge = new Color(.05, .05, .07);
export const Plasma = new Color(.88, .93, 1), PlasmaDeep = new Color(.55, .7, .95), PlasmaHot = new Color(1, 1, 1);

// Accelerator as a stand-in: the goku pawn in a dark shirt with white hair.
export function accelerator(pos, sun, strength, opts = {}) {
  pawn(pos, Shirt, sun, strength, opts);
  if (!opts.lie) draw(disc, pos.x, pawnLayer + .006, pos.z + .69, .19, .1, 0, HairWhite.withAlpha(opts.alpha ?? 1));
}

// One arm held out from the chest toward deg (0 east, 90 north), reach cells long.
export function arm(pos, deg, reach, alpha = 1) {
  if (reach <= .02 || alpha <= 0) return;
  const r = deg * Mathf.Deg2Rad, cx = pos.x + Math.cos(r) * reach / 2, cz = pos.z + Math.sin(r) * reach / 2 + Chest;
  draw(MeshPool.plane10, cx, pawnLayer + .01, cz, .09, reach, 90 - deg, Skin.withAlpha(alpha));
}

// The ball of air being compressed into plasma. stage 0 is loose air (large, faint, swirling),
// stage 1 is plasma (small, white core, violet shell, rays). size is the ball across at stage 1.
export function plasmaBall(key, at, size, s, alpha, stage) {
  if (size <= .01 || alpha <= 0) return;
  const r = size / 2 * (1.9 - .9 * stage), beat = 1 + .1 * Math.sin(s * 41) * stage, hot = clamp((stage - .6) / .4);
  const shell = Color.Lerp(Air, Plasma, stage), halo = Color.Lerp(AirDeep, PlasmaDeep, stage);
  sprite(at, r * 7, r * 7, halo.withAlpha((.05 + .4 * stage) * alpha), glow, Y + .1);
  draw(disc, at.x, Y + .11, at.z, r, r, 0, shell.withAlpha((.1 + .85 * stage) * alpha));
  draw(thinRing, at.x, Y + .112, at.z, r, r, 0, Edge.withAlpha((.35 + .55 * stage) * alpha));   // the black edge
  // Air spiralling into the ball: three arcs turning round it that tighten as it compresses.
  for (let i = 0; i < 3; i++) {
    const pts = [], a0 = s * (5 + stage * 6) + i * TAU / 3, span = 1.6 - .5 * stage, rr = r * (1.25 - .15 * stage);
    for (let k = 0; k <= 8; k++) { const q = a0 + k / 8 * span; pts.push({ x: at.x + Math.cos(q) * rr, z: at.z + Math.sin(q) * rr }); }
    const a = [], b = [];
    pts.forEach((q, k) => { const w = r * .12 * Math.sin(k / 8 * Math.PI) + .004, qa = a0 + k / 8 * span; a.push({ x: q.x - Math.cos(qa) * w, z: q.z - Math.sin(qa) * w }); b.push({ x: q.x + Math.cos(qa) * w, z: q.z + Math.sin(qa) * w }); });
    stripArc(`${key} arc ${i}`, a, b, Color.Lerp(Air, White, stage).withAlpha((.5 + .4 * stage) * alpha));
  }
  if (hot > 0) for (let i = 0; i < 8; i++) {
    const ang = (i * 45 + s * (i % 2 ? 70 : -50) + rand(i + 20) * 20) * Mathf.Deg2Rad, flick = .55 + .45 * Math.sin(s * 23 + i * 1.9);
    const reach = r * (1.4 + 2.2 * rand(i + 40)) * hot * flick;
    streak(`${key} ray ${i}`, { x: at.x + Math.cos(ang) * r * .3, z: at.z + Math.sin(ang) * r * .3 }, { x: at.x + Math.cos(ang) * (r + reach), z: at.z + Math.sin(ang) * (r + reach) },
      r * .3, PlasmaHot.withAlpha(.8 * alpha * flick * hot), whiteGlow, Y + .113, 4);
  }
  const core = r * (.25 + .3 * stage) * beat;
  draw(disc, at.x, Y + .12, at.z, core, core, 0, White.withAlpha((.5 + .5 * stage) * alpha));
  sprite(at, core * 2.4, core * 2.4, White.withAlpha(.9 * stage * alpha), glow, Y + .121);
}

function stripArc(key, a, b, colour) { strip(key, a, b, colour, whiteGlow, Y + .114); }
