// Stardust Dragon Staff: how the dragon is drawn. Not a sketch itself, so it is not listed in sketches/index.js. The
// movement and the fight are in stardust-dragon-ai.js, the textures in stardust-dragon-textures.js.
//
// The dragon: each body piece and the tail as one texture (piece()); the head in parts (head()): crest blades that
// sway, the mouth between the jaws, the lower jaw on its hinge, the upper head and the eye's glow; whiskers
// (whisker()) and the tail's wake of light (wake()).
//
// Drawing a piece is Terraria's: full bright with the colour's alpha halved, which with premultiplied blending
// is src + 0.5 x dst. Here that is the same texture drawn twice, Transparent at alpha .5 and then MoteGlow at
// alpha .5, the glow 0.0002 higher so Unity keeps the order. A piece flying west is drawn on a quad with its
// u flipped and turned 180 degrees, which mirrors it across its line of flight, so the gold stays north
// (Terraria mirrors by the sign of x speed). In C# the flipped quad is MeshPool.plane10Flip; the mouth and the
// whiskers are meshes rebuilt each frame (Shared/VfxDraw strips).
import { Color, Mathf, Mesh, MeshPool, MaterialPool, ShaderDatabase } from '../../js/engine.js';
import { draw, mesh } from './six-paths-solid.js';
import { Y, sprite, soft, glow } from './six-paths-impact.js';
import { rect } from './chain-sickle.js';
import { strip } from './flying-thunder-god.js';
import { ArtOf, Shift, Kinds, Hinge, JawLip, UpperLip, EyeAt, Cream, GoldLight, Gold, GoldDark, Outline, Ice, BlueLight, Cyan, Blue, Deep } from './stardust-dragon-textures.js';

const clamp01 = Mathf.Clamp01, D2R = Mathf.Deg2Rad;
export const HeadScale = 1.15;
const DrawScale = { head: HeadScale, jaw: HeadScale, blade: HeadScale, body1: 1, body2: 1, tail: 1 };       // the head drawn a little bigger, so it reads at a normal zoom
export const IceColour = new Color(...Ice), CyanColour = new Color(...Cyan), LightColour = new Color(.75, .85, 1);   // Ice Torch light, (0.75, 0.85, 1.4) clamped

const Mats = Object.fromEntries(Kinds.map(kind => [kind, {
  normal: MaterialPool.MatFrom(`lab/stardust-${kind}`, ShaderDatabase.Transparent),
  glow: MaterialPool.MatFrom(`lab/stardust-${kind}`, ShaderDatabase.MoteGlow),
}]));
const dustMat = MaterialPool.MatFrom('lab/stardust-dust', ShaderDatabase.MoteGlow);
const plane10Flip = (() => {
  const m = new Mesh('plane10 flip');
  m.setFlat([-.5, -.5, -.5, .5, .5, .5, .5, -.5], [0, 1, 2, 0, 2, 3]);
  m.uv = new Float32Array([1, 0, 1, 1, 0, 1, 0, 0]);
  return m;
})();
const White = new Color(1, 1, 1);

// One piece at screen point c, flying at rot (radians anticlockwise from east), flip -1 when it is mirrored (flying
// west); since: seconds since it last mirrored, for the roll; cells is the size of one Terraria pixel on screen (px
// x the dragon's scale); alpha 0..1 (the summon and end fades). The roll: a piece that has just mirrored squeezes
// flat across its line of flight and opens out again on its new side over Roll seconds, so a turn past north or
// south rolls down the body instead of snapping piece by piece (Terraria snaps).
export const Roll = .16;
// Which side a piece shows and how wide it is across its line of flight: side flips half way through the roll.
export function rollOf(flip, since) {
  const u = clamp01(since / Roll);
  return { side: u < .5 ? -flip : flip, across: Math.max(.06, Math.abs(Math.cos(Math.PI * u))) };
}
export function piece(kind, c, rot, roll, cells, alpha, layer, scale = 1) {
  if (alpha <= 0) return;
  const deg = rot / D2R, mesh = roll.side < 0 ? plane10Flip : MeshPool.plane10, angle = roll.side < 0 ? 180 - deg : -deg;
  const size = ArtOf[kind] * cells * DrawScale[kind] * scale, shift = Shift[kind] * cells * scale, x = c.x + Math.cos(rot) * shift, z = c.z + Math.sin(rot) * shift;
  draw(mesh, x, layer, z, size, size * roll.across, angle, White.withAlpha(.5 * alpha), Mats[kind].normal);
  draw(mesh, x, layer + .0002, z, size, size * roll.across, angle, White.withAlpha(.5 * alpha), Mats[kind].glow);
}

// --- the head's parts --------------------------------------------------------------------------------------------------
// A point of the head's frame (Terraria px, x toward the snout, y toward the spine) on screen; k: cells per px.
function headPoint(h, k, lx, ly) {
  const c = Math.cos(h.rot), sn = Math.sin(h.rot), sy = ly * h.roll.side * h.roll.across;
  return { x: h.c.x + (c * lx - sn * sy) * k, z: h.c.z + (sn * lx + c * sy) * k };
}
// One of the head's textures at (lx, ly) in the head's frame, turned la radians in it, size times its own size.
function part(kind, h, k, alpha, layer, lx = 0, ly = 0, la = 0, size = 1) {
  if (alpha <= 0) return;
  const at = headPoint(h, k, lx, ly), deg = (h.rot + la * h.roll.side) / D2R, side = h.roll.side;
  const mesh0 = side < 0 ? plane10Flip : MeshPool.plane10, angle = side < 0 ? 180 - deg : -deg, w = ArtOf[kind] * k * size;
  draw(mesh0, at.x, layer, at.z, w, w * h.roll.across, angle, White.withAlpha(.5 * alpha), Mats[kind].normal);
  draw(mesh0, at.x, layer + .0002, at.z, w, w * h.roll.across, angle, White.withAlpha(.5 * alpha), Mats[kind].glow);
}
const turnAbout = ([x, y], [cx, cy], a) => [cx + (x - cx) * Math.cos(a) - (y - cy) * Math.sin(a), cy + (x - cx) * Math.sin(a) + (y - cy) * Math.cos(a)];
// The crest: three crystal blades behind the skull, [root x, root y, angle (radians, 0 toward the snout), length].
const Crest = [[-5, 7.5, 2.85, 1], [-8, 5, 3.0, .85], [-10, 2.5, 3.2, .7]];
const Throat = new Color(.03, .07, .32);
// The head: crest blades, the mouth between the jaws, the jaw (open radians about Hinge), the upper head, the
// eye's glow. h: { c, rot, roll }; sway: radians added to each crest blade (they swing out in turns); s: seconds.
export function head(h, cells, alpha, layer, open, sway, s) {
  if (alpha <= 0) return;
  const k = cells * HeadScale;
  Crest.forEach(([x, y, a, len], i) => {
    const la = a + sway + .12 * Math.sin(s * 3.1 + i * 1.3);
    part('blade', h, k, alpha, layer + i * .0005, x + Math.cos(la) * 14 * len, y + Math.sin(la) * 14 * len, la, len);
  });
  // The mouth: the gap between the upper lip and the jaw's top edge, dark with a glowing throat.
  const lower = JawLip.map(q => turnAbout(q, Hinge, -open)), ring = [Hinge, ...UpperLip.slice().reverse(), ...lower].map(q => headPoint(h, k, q[0], q[1]));
  if (open > .02) {
    const m = mesh('stardust mouth'), xz = ring.flatMap(q => [q.x, q.z]), tri = [];
    for (let i = 1; i < ring.length - 1; i++) tri.push(0, i, i + 1);
    m.setFlat(xz, tri);
    draw(m, 0, layer + .002, 0, 1, 1, 0, Throat.withAlpha(.9 * alpha), flatNormal);
    const throat = headPoint(h, k, 6, -1.2 - 4 * Math.sin(open));
    sprite(throat, 12 * k, 7 * k * h.roll.across, CyanColour.withAlpha(.5 * alpha * Math.min(1, open / .4)), glow, layer + .0025, -h.rot / D2R);
  }
  const [jx, jy] = turnAbout([0, 0], Hinge, -open);
  part('jaw', h, k, alpha, layer + .003, jx, jy, -open);
  part('head', h, k, alpha, layer + .004);
  const eye = headPoint(h, k, EyeAt[0], EyeAt[1]), pulse = .85 + .15 * Math.sin(s * 5);
  sprite(eye, 16 * k, 10 * k, CyanColour.withAlpha(.55 * alpha * pulse), glow, layer + .005, -h.rot / D2R);
  sprite(eye, 5 * k, 3.5 * k, White.withAlpha(.9 * alpha), glow, layer + .0052, -h.rot / D2R);
  sprite(eye, 11 * k * pulse, 11 * k * pulse, White.withAlpha(.7 * alpha), dustMat, layer + .0054, s * 40);
}
// The snout's whisker roots in the head's frame.
export const WhiskerRoots = [[19, 2.2], [16, -.6]];
// A whisker: a thin tapering ribbon along pts (root first, in cells), half: half width at the root in cells.
export function whisker(key, pts, half, alpha, layer) {
  if (pts.length < 2 || alpha <= 0) return;
  [[1, Cyan, 1], [.45, Ice, 1]].forEach(([share, colour, a], w) => {
    const A = [], B = [];
    pts.forEach((q, i) => {
      const p0 = pts[Math.max(0, i - 1)], p1 = pts[Math.min(pts.length - 1, i + 1)], L = Math.hypot(p1.x - p0.x, p1.z - p0.z) || 1;
      const nx = -(p1.z - p0.z) / L, nz = (p1.x - p0.x) / L, hw = half * share * (1 - .85 * i / (pts.length - 1));
      A.push({ x: q.x + nx * hw, z: q.z + nz * hw }); B.push({ x: q.x - nx * hw, z: q.z - nz * hw });
    });
    const c = new Color(...colour);
    strip(`${key} ${w} normal`, A, B, c.withAlpha(.5 * a * alpha), flatNormal, layer + w * .0003);
    strip(`${key} ${w} glow`, A, B, c.withAlpha(.5 * a * alpha), flatGlow, layer + w * .0003 + .0001);
  });
}

const flatNormal = MaterialPool.MatFrom('white', ShaderDatabase.Transparent), flatGlow = MaterialPool.MatFrom('white', ShaderDatabase.MoteGlow);

// The light the tail leaves: soft glow spots along the tail tip's last positions, smaller and fainter further back
// (not Terraria's, which leaves only its dust). pts: newest first, in cells; size: the first spot's length, cells.
const WakeStep = .12;                     // cells between spots
export function wake(pts, size, alpha, layer) {
  if (pts.length < 2 || alpha <= 0) return;
  const run = [0];
  for (let i = 1; i < pts.length; i++) run.push(run[i - 1] + Math.hypot(pts[i].x - pts[i - 1].x, pts[i].z - pts[i - 1].z));
  const total = run[run.length - 1];
  for (let d = 0, i = 1; d < total; d += WakeStep) {
    while (i < pts.length - 1 && run[i] < d) i++;
    const a = pts[i - 1], b = pts[i], u = (d - run[i - 1]) / ((run[i] - run[i - 1]) || 1), f = 1 - d / total;
    const c = { x: Mathf.Lerp(a.x, b.x, u), z: Mathf.Lerp(a.z, b.z, u) }, deg = -Math.atan2(a.z - b.z, a.x - b.x) / D2R;
    sprite(c, size * (.4 + .6 * f), size * .55 * (.3 + .7 * f), CyanColour.withAlpha(.16 * f * f * alpha), glow, layer, deg);
    sprite(c, size * .5 * (.4 + .6 * f), size * .2 * (.3 + .7 * f), IceColour.withAlpha(.14 * f * f * alpha), glow, layer + .0001, deg);
  }
}
// The soft cyan glow under a piece, so the dragon reads as light at a normal zoom (not Terraria's: its blue glows
// only through the half-additive blend). Drawn below every piece.
export function aura(c, rot, cells, alpha, layer) {
  if (alpha <= 0) return;
  sprite(c, 30 * cells, 20 * cells, CyanColour.withAlpha(.2 * alpha), glow, layer, -rot / D2R);
  sprite(c, 48 * cells, 34 * cells, CyanColour.withAlpha(.07 * alpha), glow, layer - .0002, -rot / D2R);
}

// One Ice Torch dust at age (seconds) of life: it swells over the first 0.1 s and shrinks away; faint is the
// summon's dust (Terraria alpha 100, the rest 25). size: cells across at its largest.
export function dust(pos, age, life, size, faint, layer) {
  const u = age / life;
  if (u < 0 || u >= 1) return 0;
  const grow = age < .1 ? .75 + .25 * age / .1 : 1 - (age - .1) / (life - .1), a = faint ? .6 : .9;
  sprite(pos, size * grow, size * grow, CyanColour.withAlpha(a), dustMat, layer);
  sprite(pos, size * grow * .5, size * grow * .5, IceColour.withAlpha(a * .9), dustMat, layer + .0001);
  return a * grow;
}

// The staff in the wielder's hand, a stand-in for its texture (the sprite: a dark shaft with gold bands, a gold
// claw holding a blue orb, two blue shards floating beside it). deg: the staff's angle on screen; glint 0..1.
const Shaft = new Color(.25, .12, .07), Band = new Color(...Gold), Claw = new Color(...GoldLight), Orb = new Color(.24, .35, .93), OrbDeep = new Color(.09, .05, .90);
export function staff(hand, deg, s, glint, layer = Y + .01) {
  const r = deg * D2R, along = d => ({ x: hand.x + Math.cos(r) * d, z: hand.z + Math.sin(r) * d });
  rect('stardust staff shaft', along(.28), .9, .055, deg, Shaft, layer);
  for (const d of [.1, .3, .5]) rect(`stardust staff band ${d}`, along(d), .045, .07, deg, Band, layer + .001);
  const top = along(.78);
  for (const side of [-1, 1]) rect(`stardust staff claw ${side}`, { x: top.x - Math.sin(r) * .07 * side, z: top.z + Math.cos(r) * .07 * side }, .16, .04, deg + 20 * side, Claw, layer + .002);
  sprite(top, .17, .17, OrbDeep, soft, layer + .003);
  sprite(top, .12, .12, Orb, soft, layer + .004);
  sprite({ x: top.x - .02, z: top.z + .02 }, .05, .05, new Color(...Ice), soft, layer + .005);
  sprite(top, .45 + .5 * glint, .45 + .5 * glint, CyanColour.withAlpha(.25 + .5 * glint), glow, layer + .006);
  for (const k of [0, 1]) {                                  // the two floating shards
    const a = r + (k ? 1.9 : -1.6), d = .17 + .02 * Math.sin(s * 3 + k * 2), bob = .015 * Math.sin(s * 4 + k);
    sprite({ x: top.x + Math.cos(a) * d, z: top.z + Math.sin(a) * d + bob }, .09, .09, CyanColour, dustMat, layer + .007);
  }
  if (glint > 0) sprite(top, .7 * glint, .7 * glint, White.withAlpha(glint), dustMat, layer + .008);
  return top;
}
