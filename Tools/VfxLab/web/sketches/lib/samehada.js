// Shared drawing for the Samehada kit: the shark-skin sword at any charge, its bandage, the
// scales, the drain that runs from a hit pawn into the blade, the Drained haze on a pawn, and the
// charge tally. Feed and Shark Skin draw the weapon through these so the two stay identical, and a
// C# port makes one class of it.
//
// The blade lies level at hand height and turns with the aim, so it is a flat shape and needs no
// per-facing method. It draws under the pawn layer when it points north and over it when it points
// south (the Vergil rule), or the grip would sit on the holder's head. Height is folded into z by
// Lift; the shadow is the same outline cast along the scene's sun.
//
// The rule the drawing shows (proposed, not agreed; the numbers are placeholders for XML fields):
//   charges 0..5 stored in the blade. Length = 1.0 + 0.15 x charges cells (1.75 at five). Each charge uncovers one
//   more band of scales from the tip back: bandaged share = 1 - 0.6 x charges / 5, so a full blade
//   still has 40 % of its length wrapped at the grip end. Shark Skin tears the rest off.
import { AltitudeLayer, Color, Mathf, Meshes } from '../../js/engine.js';
import { draw, mesh } from './six-paths-solid.js';
import { Body, Y, Floor, Lift, sprite, band, circle, soft, glow, rand } from './six-paths-impact.js';
import { figure, rect, tube, screen, shadow, frame, easeOut, bump, Skin, Pale, Dust, Enemy, Ally, Holder, shadowLayer, pawnLayer, puff } from './chain-sickle.js';
export { figure, rect, tube, screen, shadow, frame, easeOut, bump, Skin, Pale, Dust, Enemy, Ally, Holder, shadowLayer, pawnLayer, puff, rand };

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
export const disc = Meshes.disc(32, 'samehada disc');
// Palette. Hide is the dark blue-grey skin, Flesh the purple that shows between spread scales,
// Chakra the pale blue of what the blade drinks. None of these is Six Paths violet or Vergil blue.
export const Hide = new Color(.12, .14, .22), HideLit = new Color(.24, .28, .40);
export const Scale = new Color(.26, .30, .42), ScaleLit = new Color(.64, .72, .86), ScaleEdge = new Color(.05, .06, .11);
export const Flesh = new Color(.42, .20, .48), FleshLit = new Color(.66, .36, .70);
export const Bandage = new Color(.80, .76, .64), BandageSeam = new Color(.52, .47, .38), BandageLit = new Color(.92, .89, .80);
export const Bone = new Color(.80, .78, .70), Chakra = new Color(.55, .78, 1), ChakraDeep = new Color(.20, .45, .95), Wisp = new Color(.72, .78, .88);
export const Heal = new Color(.60, .95, .70);
// Decided looks and the rule's fixed numbers.
export const HandH = .5;                  // hand height, cells
export const ChestH = .45;                // where the drain leaves a standing pawn
export const MaxCharges = 5;
export const BaseLength = 1.0, LengthPerCharge = .15;
export const GripLength = .30;            // handle + pommel, in front of the hand
export const RowStep = .12;               // one band of scales along the blade
export const BandagedAt = c => 1 - .6 * c / MaxCharges;   // share of the blade still wrapped
export const Lead = .2, Tail = .4;
export const bladeLength = c => BaseLength + LengthPerCharge * c;

// Half-width of the blade at share u of its length: narrow at the grip and widest near the tip,
// as Samehada is (a club more than a sword), with a blunt mouth at the end. `flare` spreads it.
export function halfWidth(u, flare = 0) {
  const base = u < .8 ? lerp(.07, .17, smooth(u / .8)) : lerp(.17, .13, smooth((u - .8) / .2));
  return base * (1 + .55 * flare);
}

// Outline of the blade as two point lists (left, right) from `from` along d for `len`.
function outline(from, d, len, flare, n = 18) {
  const A = [], B = [];
  for (let i = 0; i <= n; i++) {
    const u = i / n, w = i === n ? .05 : halfWidth(u, flare);
    const x = from.x + d.x * len * u, z = from.z + d.z * len * u;
    A.push({ x: x - d.z * w, z: z + d.x * w }); B.push({ x: x + d.z * w, z: z - d.x * w });
  }
  return { A, B };
}

// One scale: a small shield shape pointing along d. Cached per key.
function scaleMesh(key) {
  const m = mesh(key);
  m.setFlat([0, .5, .45, .15, .35, -.5, -.35, -.5, -.45, .15], [0, 1, 2, 0, 2, 3, 0, 3, 4]);
  return m;
}
const scaleM = scaleMesh('samehada scale');

// The sword. `hand` is a ground point, HandH up; `deg` the direction the blade points.
//   charges: 0..5, may be fractional while a charge is being gained (the tip grows).
//   flare 0..1: Shark Skin. Scales stand off the flesh and the blade widens.
//   tear 0..1: how much of the bandaged span has been torn off (Shark Skin); 1 = bare.
//   hot 0..1: a pale blue light along the scales, while the blade is drinking.
// Returns { grip, tip, layer } for the drain and the glints.
export function samehada(key, hand, deg, charges, sun, strength, { flare = 0, tear = 0, hot = 0, alpha = 1, layer = null } = {}) {
  const r = deg * Mathf.Deg2Rad, d = { x: Math.cos(r), z: Math.sin(r) };
  if (layer === null) layer = d.z > 0 ? pawnLayer - .03 : Y + .05;
  const len = bladeLength(charges), wrapped = BandagedAt(Math.min(MaxCharges, Math.max(0, charges))) * (1 - tear);
  const p = screen({ ...hand, h: HandH }), sd = shadow({ ...hand, h: HandH }, sun);
  const at = (base, along, side = 0) => ({ x: base.x + d.x * along - d.z * side, z: base.z + d.z * along + d.x * side });

  // Shadow: grip and blade outline in one dark tone on the floor.
  {
    const from = at(sd, GripLength), o = outline(from, d, len, flare);
    rect(`${key} shadow grip`, at(sd, GripLength * .5), GripLength, .11, deg, Body.withAlpha(strength * .6 * alpha), shadowLayer);
    band(`${key} shadow blade`, o.A, o.B, Body.withAlpha(strength * .6 * alpha), shadowLayer);
  }

  // Grip: a dark wrapped handle with a bone pommel at the hand end, and the hand on it.
  rect(`${key} grip`, at(p, GripLength * .55), GripLength * .9, .085, deg, Hide.withAlpha(alpha), layer);
  for (let i = 0; i < 4; i++) rect(`${key} grip wrap ${i}`, at(p, GripLength * (.22 + i * .18)), .09, .028, deg + 62, BandageSeam.withAlpha(alpha), layer + .001);
  draw(disc, p.x - d.x * .02, layer + .002, p.z - d.z * .02, .075, .075, 0, Bone.withAlpha(alpha));
  draw(disc, p.x - d.x * .02 - .012, layer + .003, p.z - d.z * .02 + .015, .028, .028, 0, Hide.withAlpha(alpha));   // eye socket
  sprite(at(p, GripLength * .45), .11, .12, Skin.withAlpha(alpha), soft, layer + .004);

  // Blade body: a dark edge band, the hide face, then the purple flesh where scales are spread.
  const from = at(p, GripLength), o = outline(from, d, len, flare);
  const inset = (pts, other, k) => pts.map((q, i) => ({ x: lerp(q.x, other[i].x, k), z: lerp(q.z, other[i].z, k) }));
  band(`${key} edge`, o.A.map((q, i) => ({ x: q.x + (q.x - o.B[i].x) * .09, z: q.z + (q.z - o.B[i].z) * .09 })),
    o.B.map((q, i) => ({ x: q.x + (q.x - o.A[i].x) * .09, z: q.z + (q.z - o.A[i].z) * .09 })), ScaleEdge.withAlpha(alpha), layer + .005);
  band(`${key} hide`, o.A, o.B, Color.Lerp(Hide, Flesh, flare * .8).withAlpha(alpha), layer + .006);
  band(`${key} hide lit`, inset(o.A, o.B, .5), inset(o.A, o.B, .78), Color.Lerp(HideLit, FleshLit, flare * .8).withAlpha(alpha * .8), layer + .007);

  // Scales: rows every RowStep along the bare span, three across, pointing at the tip. Under the
  // bandage they are not drawn. Flared, each row stands off to its side and lifts a little.
  const rows = Math.floor(len / RowStep), bareFrom = wrapped * len;
  for (let i = 0; i < rows; i++) {
    const along = (i + .6) * RowStep;
    if (along < bareFrom) continue;
    const u = along / len, w = halfWidth(u, flare), sz = Math.min(.13, w * 1.0);
    const reveal = clamp((along - bareFrom) / RowStep);    // the row at the bandage edge is half shown
    for (let k = -1; k <= 1; k++) {
      if (k !== 0 && w < .09) continue;
      const side = k * w * (.55 + .25 * flare), c = at(from, along + (k === 0 ? 0 : RowStep * .5), side);
      const lit = (i + k + 3) % 2 === 0;
      const tone = Color.Lerp(lit ? ScaleLit : Scale, Chakra, hot * .7);
      draw(scaleM, c.x, layer + .008, c.z, sz * (1 + .15 * flare), sz * 1.15, -deg + 90 + k * 14 * flare, ScaleEdge.withAlpha(alpha * reveal));
      draw(scaleM, c.x + d.x * .006, layer + .009, c.z + d.z * .006, sz * .78 * (1 + .15 * flare), sz * .9, -deg + 90 + k * 14 * flare, tone.withAlpha(alpha * reveal));
    }
  }
  if (hot > 0) sprite(at(p, GripLength + len * .55), len * 1.1, .5, Chakra.withAlpha(.45 * hot * alpha), glow, layer + .012, -deg);

  // Bandage over the wrapped span: a cream band, diagonal seams, one lit line along the top.
  if (wrapped > .01) {
    const n = 10, A = [], B = [];
    for (let i = 0; i <= n; i++) {
      const u = i / n * wrapped, w = halfWidth(u, flare) * 1.08 + .01, x = from.x + d.x * len * u, z = from.z + d.z * len * u;
      A.push({ x: x - d.z * w, z: z + d.x * w }); B.push({ x: x + d.z * w, z: z - d.x * w });
    }
    band(`${key} bandage`, A, B, Bandage.withAlpha(alpha), layer + .010);
    band(`${key} bandage lit`, inset(A, B, .55), inset(A, B, .85), BandageLit.withAlpha(alpha * .7), layer + .0105);
    const seams = Math.floor(wrapped * len / .095);
    for (let i = 0; i < seams; i++) {
      const along = (i + .5) * .095, u = along / len, w = halfWidth(u, flare) * 2.3;
      rect(`${key} seam ${i}`, at(from, along), w, .022, deg + 58, BandageSeam.withAlpha(alpha * .9), layer + .011);
    }
    // The frayed end where the wrap stops: two short loose tails.
    const e = wrapped * len, we = halfWidth(e / len, flare);
    rect(`${key} fray a`, at(from, e + .04, we * .6), .09, .03, deg + 30, Bandage.withAlpha(alpha), layer + .0112);
    rect(`${key} fray b`, at(from, e + .03, -we * .7), .07, .03, deg - 40, Bandage.withAlpha(alpha), layer + .0112);
  }

  // The mouth at the tip: two small teeth either side of the point.
  const tip = at(from, len);
  for (const k of [-1, 1]) rect(`${key} tooth ${k}`, at(from, len - .03, k * .07), .09, .028, deg + k * 20, Bone.withAlpha(alpha), layer + .012);
  return { grip: p, tip, layer, len, from, d };
}

// The drain: chakra pulled out of a pawn at chest height and into the blade's tip. A faint line,
// parcels sliding along it, and a haze that leaves the pawn. `u` 0..1 is progress: the parcels
// start at the pawn and reach the blade at u = 1.
export function drain(key, chest, tip, age, life, alpha = 1) {
  if (age < 0 || age >= life) return;
  const u = age / life, fade = Math.sin(Math.min(1, u * 1.15) * Math.PI) ** .5;
  const pts = [];
  for (let i = 0; i <= 12; i++) {
    const v = i / 12, sagg = Math.sin(v * Math.PI) * .12;
    pts.push({ x: lerp(chest.x, tip.x, v), z: lerp(chest.z, tip.z, v) + sagg });
  }
  tube(`${key} line`, pts, () => .02, Chakra.withAlpha(.35 * fade * alpha), Y + .08);
  tube(`${key} line glow`, pts, () => .07, ChakraDeep.withAlpha(.25 * fade * alpha), Y + .079);
  // Nine parcels leave the pawn one after another and travel the line in 55 % of the life.
  for (let i = 0; i < 9; i++) {
    const t0 = (i / 9) * .45, v = (u - t0) / .55;
    if (v < 0 || v > 1) continue;
    const idx = v * 12, j = Math.min(11, Math.floor(idx)), f = idx - j;
    const q = { x: lerp(pts[j].x, pts[j + 1].x, f), z: lerp(pts[j].z, pts[j + 1].z, f) + (rand(i + 700) - .5) * .06 };
    const sz = .14 + rand(i + 710) * .08;
    sprite(q, sz * 1.8, sz * 1.8, ChakraDeep.withAlpha(.5 * alpha), glow, Y + .081);
    sprite(q, sz, sz, Pale.withAlpha(.9 * alpha), glow, Y + .082);
  }
  // The pawn goes grey where the chakra left: a haze around the chest that thins.
  sprite(chest, .5, .45, Wisp.withAlpha(.35 * fade * alpha), soft, Y + .078);
}

// Drained: a stacking slow on a pawn. Drawn as a dim grey-blue haze around the feet and thin rings
// that rise slowly and thin, one ring per stack. Stays as long as the hediff would.
export function drained(key, pos, stacks, s, alpha = 1) {
  if (stacks <= 0) return;
  const k = Math.min(1, stacks / MaxCharges);
  sprite({ x: pos.x, z: pos.z + .05 }, .9, .55, Wisp.withAlpha(.18 * k * alpha), soft, Floor + .02);
  for (let i = 0; i < stacks; i++) {
    const u = ((s * .35 + i / stacks) % 1), h = u * .8;
    circle({ x: pos.x, z: pos.z + .1 + h * Lift }, .28 + u * .05, .35 * (1 - u) * alpha, pawnLayer + .03, Wisp);
  }
}

// Healing on the holder: soft green light rising up the body, then gone.
export function heal(pos, age, life, alpha = 1) {
  if (age < 0 || age >= life) return;
  const u = age / life;
  sprite({ x: pos.x, z: pos.z + .3 + u * .3 }, .7, .9, Heal.withAlpha(.32 * Math.sin(u * Math.PI) * alpha), glow, Y + .07);
  for (let i = 0; i < 5; i++) {
    const v = (u * 1.3 + rand(i + 900) * .6) % 1, x = pos.x + (rand(i + 910) - .5) * .5;
    sprite({ x, z: pos.z + .1 + v * .9 }, .1, .1, Heal.withAlpha((1 - v) * .8 * alpha), glow, Y + .071);
  }
}

// Hit: a short pale blue flash at the point of contact and a bite mark of three scratches.
export function bite(key, pos, deg, age, alpha = 1) {
  if (age < 0) return;
  if (age < .14) sprite(pos, .55, .45, Pale.withAlpha((1 - age / .14) * .8 * alpha), glow, Y + .09);
  const fade = 1 - clamp((age - .6) / 1.4);
  if (fade <= 0) return;
  for (let i = -1; i <= 1; i++) {
    const c = { x: pos.x + Math.cos((deg + 90) * Mathf.Deg2Rad) * i * .08, z: pos.z + Math.sin((deg + 90) * Mathf.Deg2Rad) * i * .08 };
    rect(`${key} scratch ${i}`, c, .22, .022, deg + 20, ChakraDeep.withAlpha(.6 * fade * alpha), pawnLayer + .04);
  }
}

// The charge tally: five slots south of the holder's feet, or above the head when the holder aims
// south so the row does not sit on the target. Filled slots are lit scales, empty ones are dark
// outlines. `charges` may be fractional while one fills. A lab aid: in game this is a gizmo.
export function tally(key, pos, charges, aimDeg = 0, alpha = 1) {
  const above = Math.sin(aimDeg * Mathf.Deg2Rad) < -.5;
  for (let i = 0; i < MaxCharges; i++) {
    const c = { x: pos.x - .28 + i * .14, z: pos.z + (above ? 1.15 : -.55) }, fill = clamp(charges - i);
    draw(scaleM, c.x, Floor + .05, c.z, .11, .12, 0, ScaleEdge.withAlpha(.7 * alpha));
    draw(scaleM, c.x, Floor + .051, c.z, .085, .095, 0, Color.Lerp(Hide, ScaleLit, fill).withAlpha(alpha * (.5 + .5 * fill)));
  }
}

// A torn bandage strip that flies out from `start` (a screen point with height h) with sideways
// speed vx, vz and rises `rise`, then lands and stays. Time-only.
export function strip(key, start, vx, vz, rise, age, deg, alpha = 1) {
  if (age < 0) return;
  const g = 3.2, tLand = (rise + Math.sqrt(rise * rise + 2 * g * start.h)) / g;
  const t = Math.min(age, tLand), h = Math.max(0, start.h + rise * t - .5 * g * t * t);
  const gx = start.x + vx * t, gz = start.z + vz * t, landed = age >= tLand;
  const turn = deg + t * 300;
  if (!landed) rect(`${key} shadow`, { x: gx, z: gz }, .16, .05, turn, Body.withAlpha(.3 * alpha), shadowLayer);
  rect(key, { x: gx, z: gz + h * Lift }, .16, .045, turn, Bandage.withAlpha(alpha), landed ? Floor + .03 : Y + .06);
}

// ---- Fusion --------------------------------------------------------------------------------
// The shark form: a scaled overlay drawn over the stand-in pawn, with a dorsal fin, a tail and
// gills. `amount` 0..1 fades it in over the body. This is the one piece of the kit with a
// per-facing method, because it wraps a pawn instead of lying flat:
//   'up'   (aim north, back to the camera): the fin runs down the spine as a dark strip, the tail
//          hangs below the body
//   'down' (aim south, face to the camera): the fin rises above the head (height shifts north),
//          gills on both cheeks, the tail shows only as two flukes beside the feet
//   'side' (aim east or west): the fin stands up from the mid-back, the tail trails behind the
//          pawn, away from the aim, with a vertical fluke
// `walk` 0..1 phase drives a small tail sway. In game: three textures or three quad sets.
export function facingOf(aimDeg) {
  const sn = Math.sin(aimDeg * Mathf.Deg2Rad);
  return sn > .5 ? 'up' : sn < -.5 ? 'down' : 'side';
}
function tri(key, a, b, c, colour, layer) {
  band(key, [a, b], [c, c], colour, layer);
}
// The form is laid out on a body 0.89 cells tall (the stand-in's feet to the top of its head) and
// fitted to a real humanlike pawn by SharkFit: in game a pawn's body and head are 1.5-cell meshes, and
// what shows of them runs from 0.51 cells below its position to 0.66 above (1.17 tall, the body about
// 0.5 to 0.6 wide). So every offset and size is scaled by SharkFit.scale and the whole form moves down
// by SharkFit.drop: the body ellipse is centred on the torso (0.10 below the pawn's position) and the
// head covers the real head and hair, not the face alone.
export const SharkFit = { scale: 1.3, drop: -0.33 };
export function sharkForm(key, pos, aimDeg, amount, s, { alpha = 1 } = {}) {
  if (amount <= 0) return;
  const A = alpha * amount, facing = facingOf(aimDeg), west = Math.cos(aimDeg * Mathf.Deg2Rad) < 0;
  const S = SharkFit.scale, P = (dx, dz) => ({ x: pos.x + dx * S, z: pos.z + SharkFit.drop + dz * S });
  const L = pawnLayer + .012, sway = Math.sin(s * 9) * .05;
  const skin = Color.Lerp(Hide, HideLit, .25).withAlpha(A), lit = HideLit.withAlpha(A * .8), edge = ScaleEdge.withAlpha(A);
  // Body: an ellipse over the pawn's torso, dark hide with a lit belly stripe.
  { const c = P(0, .18); draw(disc, c.x, L, c.z, .25 * S, .35 * S, 0, skin); }
  { const c = P(facing === 'side' ? (west ? -.06 : .06) : 0, .16); draw(disc, c.x, L + .001, c.z, .10 * S, .26 * S, 0, lit); }
  // Scales: four rows of three across the body.
  for (let r = 0; r < 4; r++) for (let k = -1; k <= 1; k++) {
    const c = P(k * .13 + (r % 2 ? .05 : 0), .04 + r * .1), litS = (r + k) % 2 === 0;
    draw(scaleM, c.x, L + .002, c.z, .08 * S, .09 * S, 0, edge);
    draw(scaleM, c.x, L + .003, c.z + .005 * S, .06 * S, .07 * S, 0, (litS ? ScaleLit : Scale).withAlpha(A));
  }
  // Head: the hide over the head leaves the eyes; gills as three short lines on each side.
  { const c = P(0, .58); draw(disc, c.x, L + .004, c.z, .18 * S, .19 * S, 0, skin); }
  { const c = P(0, .56); draw(disc, c.x, L + .005, c.z, .12 * S, .10 * S, 0, Color.Lerp(Skin, Hide, .55).withAlpha(A)); }
  for (const k of [-1, 1]) for (let i = 0; i < 3; i++)
    rect(`${key} gill ${k} ${i}`, P(k * (.15 + i * .012), .47 + i * .06), .06 * S, .014 * S, 80 * k, Flesh.withAlpha(A), L + .006);
  // Fin and tail by facing.
  const finC = ScaleEdge.withAlpha(A), finLit = ScaleLit.withAlpha(A * .85);
  if (facing === 'up') {
    // Spine strip down the back, a fin outline either side, tail below the feet.
    rect(`${key} spine`, P(0, .22), .55 * S, .08 * S, 90, finC, L + .007);
    rect(`${key} spine lit`, P(0, .22), .5 * S, .035 * S, 90, finLit, L + .008);
    tri(`${key} tail`, P(-.06 + sway, -.05), P(.06 + sway, -.05), P(sway * 2, -.38), finC, L - .03);
    tri(`${key} fluke`, P(-.16 + sway * 2, -.42), P(.16 + sway * 2, -.42), P(sway * 2, -.3), finC, L - .03);
  } else if (facing === 'down') {
    // The fin rises above the head: a triangle whose base is at the shoulders, apex 0.55 up.
    tri(`${key} fin`, P(-.11, .62), P(.11, .62), P(.04, .62 + .55 * Lift), finC, L - .03);
    tri(`${key} fin lit`, P(-.04, .63), P(.06, .63), P(.03, .62 + .42 * Lift), finLit, L - .029);
    for (const k of [-1, 1]) tri(`${key} fluke ${k}`, P(k * .12, -.02), P(k * .3 + sway, -.1), P(k * .2 + sway, .02), finC, L - .03);
  } else {
    // Profile: the fin stands up from the mid-back, the tail trails behind.
    const back = west ? 1 : -1;       // screen direction away from the aim
    tri(`${key} fin`, P(back * .04, .3), P(-back * .16, .3), P(back * .16, .3 + .6 * Lift), finC, L + .007);
    tri(`${key} fin lit`, P(-back * .02, .31), P(-back * .11, .31), P(back * .09, .31 + .45 * Lift), finLit, L + .008);
    // Tail: a thin tube out of the hip and a crescent fluke (two thin lobes), no arrowhead.
    const tailBase = P(back * .18, .12 + sway), tailTip = P(back * .42, .1 + sway * 2);
    tube(`${key} tail`, [tailBase, tailTip], u => .045 * S * (1 - u * .5), finC, L - .03);
    tube(`${key} fluke up`, [tailTip, { x: tailTip.x + back * .06 * S, z: tailTip.z + .2 * S }], u => .03 * S * (1 - u * .7), finC, L - .03);
    tube(`${key} fluke down`, [tailTip, { x: tailTip.x + back * .05 * S, z: tailTip.z - .14 * S }], u => .03 * S * (1 - u * .7), finC, L - .03);
  }
  // Seams: a little purple flesh shows where the hide meets, as in Shark Skin.
  sprite(P(0, .38), .3 * S, .12 * S, Flesh.withAlpha(.35 * A), soft, L + .009);
}

// A regen pulse every second while fused: one soft green ring rising off the body (fitted as the form is).
export function regenPulse(pos, s, alpha = 1) {
  const u = s % 1, S = SharkFit.scale;
  circle({ x: pos.x, z: pos.z + SharkFit.drop + (.15 + u * .5 * Lift) * S }, (.22 + u * .12) * S, .45 * (1 - u) * alpha, pawnLayer + .03, Heal);
}

// A deep-water patch on the floor: a dark blue body with a paler rim, lying across the walk. The
// holder can cross it fused; a normal pawn stops at its edge. A stand-in for map terrain.
export const Water = new Color(.10, .26, .48), WaterRim = new Color(.45, .70, .90);
export function waterPatch(key, centre, along, across, deg, s) {
  const r = deg * Mathf.Deg2Rad, cx = Math.cos(r), sx = Math.sin(r);
  const c = (a, b) => ({ x: centre.x + cx * a - sx * b, z: centre.z + sx * a + cx * b });
  rect(`${key} rim`, centre, along + .16, across + .16, deg, WaterRim.withAlpha(.35), Floor + .008);
  rect(`${key} body`, centre, along, across, deg, Water.withAlpha(.85), Floor + .009);
  // Slow ripples: three faint bands drifting across.
  for (let i = 0; i < 3; i++) {
    const u = ((s * .12 + i / 3) % 1) - .5;
    rect(`${key} ripple ${i}`, c(u * along * .9, (rand(i + 50) - .5) * across * .6), .04, across * .7, deg, WaterRim.withAlpha(.18), Floor + .010);
  }
}
