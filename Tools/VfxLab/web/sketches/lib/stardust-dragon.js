// Stardust Dragon Staff: the dragon's pictures and how a piece is drawn. Not a sketch itself, so it is not
// listed in sketches/index.js. The movement and the fight are in stardust-dragon-ai.js.
//
// The four pieces are our own drawings in the design of Terraria's sprites (head, two body pieces, tail; seen
// on terraria.wiki.gg 2026-10-03), not traces of them: a side view of a fish-skeleton dragon, the gold spine
// and ribs on top, a glowing blue belly below, a spike on every other body piece, a gold skull with a horn swept
// back, a blue eye and teeth, a blue crystal frill and beard, a tail of blue arrow fletching. Terraria's exact
// palette, in flat bands lit from the spine side, with a brown outline round the gold. Each texture is 256 px
// square and covers ArtOf[kind] Terraria pixels round a point Shift[kind] px along the piece's line of flight from
// its centre (the tail's fletching runs far behind it), the head toward +u (east) and the spine toward the top
// (north). They are painted from distance functions with every shape written as [x, y]
// literals in Terraria pixels (x toward the head, y toward the spine), so make_stardust_dragon_textures.py can
// copy them unchanged. Until that script exists they are lab/ textures that only the lab can load.
//
// Drawing a piece is Terraria's: full bright with the colour's alpha halved, which with premultiplied blending
// is src + 0.5 x dst. Here that is the same texture drawn twice, Transparent at alpha .5 and then MoteGlow at
// alpha .5, the glow 0.0002 higher so Unity keeps the order. A piece flying west is drawn on a quad with its
// u flipped and turned 180 degrees, which mirrors it across its line of flight, so the gold stays north
// (Terraria mirrors by the sign of x speed). In C# the flipped quad is MeshPool.plane10Flip.
import { Color, Mathf, Mesh, MeshPool, MaterialPool, ShaderDatabase } from '../../js/engine.js';
import { registerLabTexture, pixels } from '../../js/standins.js';
import { draw } from './six-paths-solid.js';
import { Y, sprite, soft, glow } from './six-paths-impact.js';
import { rect } from './chain-sickle.js';

const clamp01 = Mathf.Clamp01, D2R = Mathf.Deg2Rad;
export const ArtOf = { head: 48, body1: 48, body2: 48, tail: 64 };   // Terraria px across a piece's texture
export const Shift = { head: 0, body1: 0, body2: 0, tail: -14 };     // px along the line of flight from the piece's centre to its texture's
const Tex = 256;
const hex = h => [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16) / 255);
// Terraria's palette (the dragon, the Stardust Cell and Guardian share it).
const Cream = hex('#fffcc8'), GoldLight = hex('#ffdc7f'), Gold = hex('#ffb400'), GoldDark = hex('#b97d2e'), Outline = hex('#5d3a1e');
const Ice = hex('#c4f7ff'), BlueLight = hex('#88e2ff'), Cyan = hex('#23c8fe'), Blue = hex('#0e9ae6'), Deep = hex('#066aff');
export const IceColour = new Color(...Ice), CyanColour = new Color(...Cyan), LightColour = new Color(.75, .85, 1);   // Ice Torch light, (0.75, 0.85, 1.4) clamped

// --- distance functions, in Terraria px; negative inside ------------------------------------------------------------
const circle = (p, c, r) => Math.hypot(p[0] - c[0], p[1] - c[1]) - r;
function capsule(p, a, b, r) {
  const px = p[0] - a[0], py = p[1] - a[1], bx = b[0] - a[0], by = b[1] - a[1];
  const h = clamp01((px * bx + py * by) / (bx * bx + by * by));
  return Math.hypot(px - bx * h, py - by * h) - r;
}
function box(p, c, half, r) {
  const qx = Math.abs(p[0] - c[0]) - half[0] + r, qy = Math.abs(p[1] - c[1]) - half[1] + r;
  return Math.hypot(Math.max(qx, 0), Math.max(qy, 0)) + Math.min(Math.max(qx, qy), 0) - r;
}
function poly(p, v) {                     // Inigo Quilez's polygon distance
  let d = (p[0] - v[0][0]) ** 2 + (p[1] - v[0][1]) ** 2, s = 1;
  for (let i = 0, j = v.length - 1; i < v.length; j = i, i++) {
    const ex = v[j][0] - v[i][0], ey = v[j][1] - v[i][1], wx = p[0] - v[i][0], wy = p[1] - v[i][1];
    const h = clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey)), bx = wx - ex * h, by = wy - ey * h;
    d = Math.min(d, bx * bx + by * by);
    const c1 = p[1] >= v[i][1], c2 = p[1] < v[j][1], c3 = ex * wy > ey * wx;
    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
  }
  return s * Math.sqrt(d);
}
const mirrorY = (pts, axis) => pts.map(([x, y]) => [x, 2 * axis - y]);
const union = (...fs) => p => Math.min(...fs.map(f => f(p)));

// Paints layers bottom up with "over"; coverage is one texture pixel of anti-aliasing at the shape's edge.
function painter(K) {                    // K: texture px per Terraria px
  let r = 0, g = 0, b = 0, a = 0;
  return {
    over(d, c, alpha = 1) {
      const k = clamp01(.5 - d * K) * alpha;
      if (k <= 0) return;
      const na = k + a * (1 - k);
      r = (c[0] * k + r * a * (1 - k)) / na; g = (c[1] * k + g * a * (1 - k)) / na; b = (c[2] * k + b * a * (1 - k)) / na; a = na;
    },
    // A transparent pixel keeps the outline's colour, so mipmaps do not bleed black into the edge.
    result() { return a > 0 ? [r, g, b, a] : [...Outline, 0]; },
  };
}
// A shape in flat bands lit from the spine side: base, a dark band along its lower edge, a light band and a
// pale line along its upper edge, over an outline `edge` px wide.
function banded(P, f, p, look, edge) {
  const [outline, dark, base, light, line] = look, up = q => f([p[0], p[1] + q]);
  if (edge > 0) P.over(f(p) - edge, outline);
  P.over(f(p), base);
  P.over(Math.max(f(p), -f([p[0], p[1] - 1.6])), dark);
  P.over(Math.max(f(p), -up(1.6)), light);
  P.over(Math.max(f(p), -up(.7)), line);
}
const GoldLook = [Outline, GoldDark, Gold, GoldLight, Cream];
const BlueLook = [Deep, Deep, Blue, Cyan, BlueLight];
const PlateLook = [Deep, Cyan, BlueLight, Ice, Ice];

// --- the pieces: shapes in Terraria px, x toward the head, y toward the spine; the spine line is y = 3.5 --------------
const Spine = 3.5;
const Ribs = [-16 / 3, 0, 16 / 3];       // three ribs per 16 px of body, so the rhythm runs on across pieces
const SpikeShape = [[7.5, 5], [1.5, 6.5], [-8, 20]];
function body(P, p, spike) {
  const belly = q => box(q, [0, -5], [10.5, 4.4], 2);             // 21 px long, so neighbouring bellies overlap into one strip
  banded(P, belly, p, BlueLook, .9);
  for (const x of [-4, 4]) banded(P, q => box(q, [x, -5.3], [2.7, 2.7], 1), p, PlateLook, 0);
  const ribs = Ribs.map(x => q => capsule(q, [x + 1.4, 10], [x - .9, -8.6], 1.3));
  const gold = union(q => capsule(q, [-10, Spine], [10, Spine], 3), ...ribs, ...(spike ? [q => poly(q, SpikeShape)] : []));
  banded(P, gold, p, GoldLook, 1.1);
  for (const x of Ribs) P.over(capsule(p, [x + 1.3, 9.3], [x - .7, -7.6], .4), Cream);
}
const Mane = [[-1, 9], [-5, 13.5], [-8, 10.5], [-12, 14], [-14, 9.5], [-18, 10.5], [-17, 5], [-12, 3], [-4, 4]];
const Beard = [[14, -5.5], [6, -10], [2, -8.5], [-3, -12], [-6, -9.5], [-11, -11.5], [-12, -5], [-6, -3]];
const Skull = [[22, 2.5], [19, 6], [11, 8.5], [2, 9.5], [-6, 8.5], [-11, 5], [-11, .5], [-6, -2.5], [2, -1.5], [21, -.5]];
const Jaw = [[18, -3.2], [2, -3.5], [-6, -3], [-4, -6.2], [8, -6.5], [16, -5]];
const Horn = [[15, 7], [10.5, 8.6], [6, 21]];
const Mouth = [[20, -.8], [2, -1.8], [-4, -2.8], [2, -3.6], [18, -3.4]];
function head(P, p) {
  banded(P, q => poly(q, Mane), p, BlueLook, .9);
  banded(P, q => poly(q, Beard), p, BlueLook, .9);
  P.over(capsule(p, [-11, 7.5], [-15, 11.5], .45), Ice);
  P.over(capsule(p, [2, -8], [-2, -10.5], .45), Ice);
  const gold = union(q => poly(q, Skull), q => poly(q, Jaw), q => poly(q, Horn), q => capsule(q, [-13, Spine], [-2, Spine], 2.6));
  banded(P, gold, p, GoldLook, 1.1);
  P.over(poly(p, Jaw) + .3, GoldDark);
  P.over(poly(p, Mouth), Outline);
  for (const x of [5, 9, 13, 17]) P.over(poly(p, [[x - 1.2, -1.4], [x + 1.2, -1.2], [x, -3.2]]), Cream);
  for (const x of [7, 11, 15]) P.over(poly(p, [[x - 1, -3.6], [x + 1, -3.6], [x, -2.2]]), Cream);
  P.over(circle(p, [9, 3.5], 3), Deep);
  P.over(circle(p, [9, 3.5], 2.1), Cyan);
  P.over(circle(p, [9.4, 3.9], 1), [1, 1, 1]);
  P.over(circle(p, [19, 4], .7), Outline);
}
// The tail: the spine goes on as a gold rod into a long arrow of blue fletching, three pairs of vanes swept back
// and a forked end, about 52 px from the rod's front to the fork (Terraria's tail is nearly half a 4-piece dragon).
const VaneSmall = [[-2, 4.5], [-6, 4.5], [-10, 10]];
const VaneBig = [[-9, 4.5], [-16, 4.5], [-30, 14], [-23, 14]];
const VaneBack = [[-22, 4.5], [-27, 4.5], [-38, 11], [-33.5, 12]];
function tail(P, p) {
  const vanes = [VaneSmall, VaneBig, VaneBack].flatMap(v => [v, mirrorY(v, Spine)]);
  const blue = union(...vanes.map(v => q => poly(q, v)), q => capsule(q, [-2, Spine], [-38, Spine], 1.6),
    q => capsule(q, [-38, Spine], [-44, Spine + 3], 1.1), q => capsule(q, [-38, Spine], [-44, Spine - 3], 1.1));
  banded(P, blue, p, BlueLook, .9);
  for (const v of [VaneBig, VaneBack]) P.over(capsule(p, v[1], v[2], .45), Ice);
  P.over(capsule(p, [-3, Spine], [-38, Spine], .55), Ice);
  const rod = union(q => poly(q, [[12, Spine + 2.8], [12, Spine - 2.8], [-4, Spine - 1.3], [-4, Spine + 1.3]]), q => capsule(q, [8, Spine], [12, Spine], 2.8));
  banded(P, rod, p, GoldLook, 1.1);
}
const Painters = { head, body1: (P, p) => body(P, p, true), body2: (P, p) => body(P, p, false), tail };
const border = (u, v, n = Tex) => u < 1 / n || v < 1 / n || u >= 1 - 1 / n || v >= 1 - 1 / n;   // the outermost pixel ring stays clear
for (const [kind, paint] of Object.entries(Painters)) {
  const art = ArtOf[kind], K = Tex / art;
  // The texture's pixel (u, v from 0 at the top-left corner) in Terraria px from the piece's centre.
  const toArt = (u, v) => [(u + .5 / Tex - .5) * art + Shift[kind], (.5 - v - .5 / Tex) * art];
  registerLabTexture(`lab/stardust-${kind}`, () => pixels(Tex, (u, v) => {
    if (border(u, v)) return [...Outline, 0];
    const P = painter(K); paint(P, toArt(u, v)); return P.result();
  }));
}
// Ice Torch dust: a soft spot with a thin cross through it, white, coloured per draw.
registerLabTexture('lab/stardust-dust', () => pixels(64, (u, v) => {
  const du = Math.abs(u + 1 / 128 - .5) * 2, dv = Math.abs(v + 1 / 128 - .5) * 2, r = Math.hypot(du, dv);
  const core = clamp01(1 - r / .5) ** 1.6, arm = (a, b) => Math.exp(-((a / .1) ** 2)) * clamp01(1 - b / .95) ** 1.3;
  return [1, 1, 1, border(u, v, 64) ? 0 : Math.min(1, core + .75 * Math.max(arm(du, dv), arm(dv, du)))];
}));

const Mats = Object.fromEntries(Object.keys(Painters).map(kind => [kind, {
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

// One piece at screen point c, flying at rot (radians anticlockwise from east), flip -1 when flying west; cells is
// the size of one Terraria pixel on screen (px x the dragon's scale); alpha 0..1 (the summon and end fades).
export function piece(kind, c, rot, flip, cells, alpha, layer) {
  if (alpha <= 0) return;
  const deg = rot / D2R, mesh = flip < 0 ? plane10Flip : MeshPool.plane10, angle = flip < 0 ? 180 - deg : -deg;
  const size = ArtOf[kind] * cells, shift = Shift[kind] * cells, x = c.x + Math.cos(rot) * shift, z = c.z + Math.sin(rot) * shift;
  draw(mesh, x, layer, z, size, size, angle, White.withAlpha(.5 * alpha), Mats[kind].normal);
  draw(mesh, x, layer + .0002, z, size, size, angle, White.withAlpha(.5 * alpha), Mats[kind].glow);
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
