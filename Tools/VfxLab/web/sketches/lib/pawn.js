// The real-size pawn stand-in, for sketches written from 2026-09-29 on. The lab cannot load the
// game's pawn art, so a sketch draws a stand-in and places its effects on it, and a C# port copies
// those positions. This stand-in has the height and width of a real pawn, measured in game on
// 2026-09-29: five naked, bald colonists facing south, 111 px per cell (docs/pawn-height-handoff.md,
// "Measured numbers"). So a port uses the numbers as they are, DrawPos + the offset, with no
// PawnFit and no feet shift.
//
// Sketches written before this keep their own two-disc stand-in (body .22 x .32 at +.18, head
// .16 x .17 at +.58), which is 0.89 tall with its feet 0.4 above a real pawn's. Do not switch them:
// their C# ports are fitted to the real pawn instead (Shared/PawnFit.cs).
//
// Every height here is on screen, in cells from the pawn's cell centre (= DrawPos), + is north.
// Floor things at a pawn's cell (rings, lanes, dust) stay centred on the cell centre, not the feet.
//
//   pawn(pos, who)          draws the stand-in; who = { body, downed, turn, shirt, skin, ... }
//   at(pos, part, who, dx)  a point on the body: 'headTop', 'head', 'neck', 'chest', 'waist', 'feet'
//   shape(pos, who)         the ellipses drawn, for effects that cover or outline the pawn
//   height(part, who)       lab height of a body point above the feet (screen offset / Lift)
//
// Pass the same `who` to all four so the effect follows the body type and pose.
//
// Limits: the outline is the south-facing one for every facing (only south was measured). Downed
// is the standing picture turned about the cell centre, as the game turns a lying pawn about its
// DrawPos; the lying layout was not measured.
import { AltitudeLayer, Color, Meshes } from '../../js/engine.js';
import { draw, Lift } from './six-paths-solid.js';
import { Body, sprite, soft } from './six-paths-impact.js';

// Heights and width per body type. "average" is the mean of thin, male and female; its chest and
// waist are the male pawn's. Fat and hulk had head top, feet and width measured; their head
// centre and neck take the average head size, and chest and waist sit at the male pawn's share of
// the neck-to-feet span (worked out, not measured).
export const Bodies = {
  average: { headTop: .63, head: .41, neck: .20, chest: .05, waist: -.20, feet: -.54, width: .54 },
  fat:     { headTop: .64, head: .42, neck: .21, chest: .05, waist: -.22, feet: -.58, width: .94 },
  hulk:    { headTop: .63, head: .41, neck: .20, chest: .04, waist: -.24, feet: -.60, width: .89 },
};

// The drawn ellipses, standing, as offsets from the cell centre (radii rx across, rz up).
// Average: one body ellipse from the feet to under the head. Fat: a rounder one, widest at the
// belly. Hulk: a wide shoulder ellipse over narrower hips, widest at the shoulders.
const Head = { rx: .21, rz: .22 };
const Torso = {
  average: [{ z: -.12, rx: .27, rz: .42 }],
  fat: [{ z: -.16, rx: .47, rz: .42 }],
  hulk: [{ z: -.27, rx: .29, rz: .33 }, { z: .02, rx: .445, rz: .22 }],
};

export const Shirt = new Color(.67, .43, .30), Skin = new Color(.83, .70, .54);
export const pawnLayer = AltitudeLayer.Pawn.AltitudeFor(), shadowLayer = AltitudeLayer.Shadows.AltitudeFor();
const disc = Meshes.disc(40, 'pawn stand-in');

const kind = who => Bodies[who?.body] ? who.body : 'average';
const angle = who => who?.downed ? (who.turn ?? -90) : 0;

// Offset (u east, v north) turned clockwise on screen by deg about pos, as draw() turns a mesh.
function place(pos, u, v, deg) {
  if (!deg) return { x: pos.x + u, z: pos.z + v };
  const a = deg * Math.PI / 180, c = Math.cos(a), s = Math.sin(a);
  return { x: pos.x + u * c + v * s, z: pos.z - u * s + v * c };
}

export function at(pos, part, who = {}, dx = 0) {
  return place(pos, dx, Bodies[kind(who)][part], angle(who));
}

export function height(part, who = {}) {
  const b = Bodies[kind(who)];
  return (b[part] - b.feet) / Lift;
}

export function shape(pos, who = {}) {
  const b = kind(who), deg = angle(who);
  const out = Torso[b].map(e => ({ ...place(pos, 0, e.z, deg), rx: e.rx, rz: e.rz, rot: deg, part: 'body' }));
  out.push({ ...place(pos, 0, Bodies[b].head, deg), rx: Head.rx, rz: Head.rz, rot: deg, part: 'head' });
  return out;
}

// who: body 'average' | 'fat' | 'hulk'; downed, turn (degrees, default -90 = head to the west);
// shirt, skin, alpha; layer (default the pawn layer); sun = scene.shadowVector, shadow = its
// strength (0 for none).
export function pawn(pos, who = {}) {
  const { shirt = Shirt, skin = Skin, alpha = 1, layer = pawnLayer, sun = { x: 0, z: 0 }, shadow = .3 } = who;
  const b = Bodies[kind(who)], deg = angle(who);
  if (shadow > 0) {
    const foot = who.downed ? pos : { x: pos.x, z: pos.z + b.feet + .12 };
    sprite({ x: foot.x + sun.x * .5, z: foot.z + sun.z * .5 }, who.downed ? 1.3 : b.width + .4, .42,
      Body.withAlpha(shadow * alpha), soft, shadowLayer, deg + (who.downed ? 90 : 0));
  }
  shape(pos, who).forEach((e, i) =>
    draw(disc, e.x, layer + i * .001, e.z, e.rx, e.rz, e.rot, (e.part === 'head' ? skin : shirt).withAlpha(alpha)));
}
