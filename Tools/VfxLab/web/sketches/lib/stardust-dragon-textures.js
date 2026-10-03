// Stardust Dragon Staff: the dragon's textures, generated in the lab (lab/stardust-head, -jaw, -blade, -body1,
// -body2, -tail, -dust). Not a sketch itself, so it is not listed in sketches/index.js; stardust-dragon.js draws them.
// Every shape is written as [x, y] literals in Terraria pixels (x toward the head, y toward the spine) and painted
// from distance functions, so a make_stardust_dragon_textures.py can copy them unchanged and write the same pixels
// as PNGs to Textures/RimArt/StardustDragon/.
import { Mathf } from '../../js/engine.js';
import { registerLabTexture, pixels } from '../../js/standins.js';

const clamp01 = Mathf.Clamp01;
export const ArtOf = { head: 48, jaw: 48, blade: 32, body1: 48, body2: 48, tail: 64 };   // Terraria px across a piece's texture
export const Shift = { head: 0, jaw: 0, blade: 0, body1: 0, body2: 0, tail: -18 };     // px along the line of flight from the piece's centre to its texture's
const Tex = 256;
const hex = h => [1, 3, 5].map(i => parseInt(h.slice(i, i + 2), 16) / 255);
// Terraria's palette (the dragon, the Stardust Cell and Guardian share it).
export const Cream = hex('#fffcc8'), GoldLight = hex('#ffdc7f'), Gold = hex('#ffb400'), GoldDark = hex('#b97d2e'), Outline = hex('#5d3a1e');
export const Ice = hex('#c4f7ff'), BlueLight = hex('#88e2ff'), Cyan = hex('#23c8fe'), BlueMid = hex('#16adfe'), Blue = hex('#0e9ae6'), Deep = hex('#066aff');

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
const BlueLook = [Deep, Blue, BlueMid, Cyan, BlueLight];
const PlateLook = [Deep, Cyan, BlueLight, Ice, Ice];

// --- the pieces: shapes in Terraria px, x toward the head, y toward the spine; the spine line is y = 3.5 --------------
// What makes it a dragon and not a fish (the user's note on the first version, 2026-10-03): a long snout with jaws
// hanging open and fangs, a slanted eye under a brow, long horns swept back, a spiky crest, whiskers and a beard
// trailing back; a comb of spikes along the back; a slim tail ending in swept blades, not a wide fin.
const Spine = 3.5;
// A body piece's texture (the user preferred this body to a continuous one, 2026-10-03): a blue belly with two
// scales, a gold spine, three ribs a piece (16 px apart, so the rhythm runs on across pieces), and on top a tall
// spike (body 1) or the rib tips standing up as three small spikes (body 2).
const Ribs = [-16 / 3, 0, 16 / 3];
const SpikeShape = [[7.5, 5], [1.5, 6.5], [-8, 20]];
const Comb = Ribs.map(x => [[x + .4, 8], [x + 2.6, 8], [x - .6, 12.8]]);
function body(P, p, spike) {
  const belly = q => box(q, [0, -5.6], [10.5, 5.2], 2);           // 21 px long, so neighbouring bellies overlap into one strip
  banded(P, belly, p, BlueLook, .9);
  for (const x of [-4, 4]) banded(P, q => box(q, [x, -6], [2.9, 3.1], 1), p, PlateLook, 0);
  const ribs = Ribs.map(x => q => capsule(q, [x + 1.4, 9], [x - 1.4, -4.6], 1.25));   // they stop halfway down the belly
  const gold = union(q => capsule(q, [-10, Spine], [10, Spine], 3), ...ribs, ...(spike ? [q => poly(q, SpikeShape)] : Comb.map(c => q => poly(q, c))));
  banded(P, gold, p, GoldLook, 1.1);
  for (const x of Ribs) P.over(capsule(p, [x + 1.3, 8.4], [x - 1.1, -3.8], .4), Cream);
}
const ellipse = (p, c, rx, ry) => (Math.hypot((p[0] - c[0]) / rx, (p[1] - c[1]) / ry) - 1) * Math.min(rx, ry);
// A tapering limb: a polyline of capsules whose radius runs from radii[0] to the last, three steps a segment.
function limb(p, pts, radii) {
  let d = Infinity;
  for (let i = 0; i < pts.length - 1; i++) for (let j = 0; j < 3; j++) {
    const u0 = j / 3, u1 = (j + 1) / 3, a = pts[i], b = pts[i + 1];
    const q0 = [a[0] + (b[0] - a[0]) * u0, a[1] + (b[1] - a[1]) * u0], q1 = [a[0] + (b[0] - a[0]) * u1, a[1] + (b[1] - a[1]) * u1];
    d = Math.min(d, capsule(p, q0, q1, radii[i] + (radii[i + 1] - radii[i]) * (u0 + u1) / 2));
  }
  return d;
}
// The head, in three textures that the sketch moves apart: the upper head (head), the lower jaw (jaw), hinged at
// Hinge, and a crystal blade of the crest (blade), drawn three times behind the skull. A dragon's skull, not a
// fish's (the user's notes, 2026-10-03): a long tapered snout with a nose ridge and a small nose horn, an angled brow
// over the eye, a cheekbone and a jaw hinge, two curved horns swept back and a small spine off the cheek; teeth
// along the upper lip; the jaw with its own fangs and a crystal beard. The mouth between the jaws, the eye's glow and
// the whiskers are drawn by head() and whisker().
export const Hinge = [-3.5, -1.2];
const Skull = [[23.5, 1.4], [23, 3.4], [19.5, 4.4], [16, 4.6], [12, 5.8], [9.5, 8.2], [5, 9.4], [0, 9.6], [-5, 8.6], [-9.5, 5.5], [-10, 1.5], [-6, -1.6], [-3, -1], [8, -.5], [18, -.2], [22.5, .2]];
const NoseRidge = [[22.5, 3.2], [19.5, 4.2], [16, 4.4], [12, 5.6], [9.5, 7.8], [8, 7.6], [11, 4.6], [16, 3.4], [21, 2.6]];
const Brow = [[12, 6.4], [9.5, 8.4], [3, 9.2], [1, 8.2], [4, 7.4], [9, 6.6]];
const Cheek = [[6, 3.5], [1, 4.5], [-5, 3], [-7, -.5], [-3, -.4], [2, 1.2]];
const NoseHorn = [[20.5, 3.8], [17.5, 4.5], [18.5, 8]];
const HornMain = [[[3, 8.8], [-3, 11.8], [-10, 14.6], [-17, 16], [-25, 15.6]], [2.4, 2, 1.5, 1, .35]];
const HornBack = [[[-3, 6.5], [-9, 7.6], [-15, 7.4], [-21, 5.8]], [1.9, 1.5, 1, .3]];
const CheekSpine = [[[-7, .5], [-11, -1.5], [-16, -4]], [1.3, .9, .3]];
export const EyeAt = [7.5, 5.3];
const Eye = [[10.4, 5.6], [8, 6.6], [5, 5.9], [7.4, 4.4]];
function skull(P, p) {
  const horns = [HornMain, HornBack, CheekSpine].map(([pts, radii]) => q => limb(q, pts, radii));
  banded(P, union(q => poly(q, Skull), q => poly(q, NoseHorn), ...horns), p, GoldLook, 1.1);
  P.over(poly(p, NoseRidge), GoldLight);
  P.over(capsule(p, [22, 3.1], [12.5, 5.3], .45), Cream);
  P.over(poly(p, Cheek), GoldDark);
  P.over(capsule(p, [5, 3.6], [-5, 3.1], .45), GoldLight);
  P.over(circle(p, [-4, -.8], 1.6), GoldDark);
  P.over(circle(p, [-4.4, -.3], .5), Cream);
  for (const [pts] of [HornMain, HornBack]) for (let i = 0; i < pts.length - 2; i++) P.over(capsule(p, pts[i], pts[i + 1], .45), Cream);   // the horns' lit upper edge
  for (const t of [.3, .55, .8]) {                                   // ridges across the main horn
    const [pts] = HornMain, i = Math.floor(t * (pts.length - 1)), u = t * (pts.length - 1) - i, x = pts[i][0] + (pts[i + 1][0] - pts[i][0]) * u, y = pts[i][1] + (pts[i + 1][1] - pts[i][1]) * u;
    P.over(capsule(p, [x + .6, y + 1.6], [x - .6, y - 1.6], .35), GoldDark);
  }
  P.over(poly(p, Brow), Cream);
  P.over(capsule(p, [11, 5.9], [4, 6.9], .6), Outline);            // the shadow under the brow
  P.over(poly(p, Eye) - .7, Deep);
  P.over(poly(p, Eye), Cyan);
  P.over(circle(p, [8.3, 5.6], .7), [1, 1, 1]);
  P.over(ellipse(p, [20.3, 3], .9, .5), Outline);
  for (const [x, long] of [[21.5, 1.4], [17.5, 0], [13.5, 0], [9.5, 0], [5.5, 0]]) P.over(poly(p, [[x - 1.1, .1], [x + 1.1, .2], [x - .2, -2.2 - long]]), Cream);
}
const JawShape = [[-6, -1.2], [-3, -1.4], [8, -1], [18, -.8], [21.5, -1.2], [22, -2.6], [19, -3.6], [12, -4.6], [4, -5.4], [-3, -5], [-6.5, -3.5]];
const Beard = [[2, -4.8], [-2, -7.5], [-5, -12.5], [-7, -8], [-11, -11.5], [-9.5, -5.5], [-5, -4.2]];
export const JawLip = [[22, -1.1], [18, -.8], [8, -1], [-3, -1.4]];   // its top edge, front to back: the mouth's lower side
export const UpperLip = [[22.5, .2], [18, -.2], [8, -.5], [-3, -1]];
function jaw(P, p) {
  banded(P, q => poly(q, Beard), p, BlueLook, .9);
  P.over(capsule(p, [-1, -6], [-4.5, -11], .45), Ice);
  banded(P, q => poly(q, JawShape), p, [Outline, Outline, GoldDark, Gold, GoldLight], 1.1);
  for (const [x, long] of [[19.5, 1.2], [15, 0], [11, 0], [7, 0]]) P.over(poly(p, [[x - 1, -1.2], [x + 1, -1.1], [x + .2, .9 + long]]), Cream);
}
function blade(P, p) {
  banded(P, q => poly(q, [[-14, 2.2], [-4, 3.4], [14, 0], [-4, -1.6], [-14, -1.4]]), p, BlueLook, .9);
  P.over(capsule(p, [-12, 1.8], [12, .3], .45), Ice);
}
// The tail's texture: the spine goes on as a gold stub and a thin blue shaft into three crystal blades swept back
// (one long one straight on, two out to the sides), with a small pair of fins where they start.
const TailAxis = Spine;
const BladeMid = [[-12, TailAxis + 1.4], [-12, TailAxis - 1.4], [-46, TailAxis]];
const BladeSide = [[-9, TailAxis + 1.6], [-15, TailAxis + .6], [-38, TailAxis + 12], [-30, TailAxis + 12.5]];
const FinSmall = [[-3, TailAxis + 1.6], [-7, TailAxis + 1.2], [-10, TailAxis + 6.5]];
function tail(P, p) {
  const blades = [BladeMid, BladeSide, mirrorY(BladeSide, TailAxis), FinSmall, mirrorY(FinSmall, TailAxis)];
  banded(P, union(...blades.map(v => q => poly(q, v)), q => capsule(q, [-2, TailAxis], [-14, TailAxis], 1.5)), p, BlueLook, .9);
  P.over(capsule(p, [-3, TailAxis], [-13, TailAxis], .55), Ice);
  P.over(capsule(p, [-12, TailAxis], [-44, TailAxis], .5), Ice);
  P.over(capsule(p, BladeSide[0], BladeSide[2], .45), Ice);
  P.over(capsule(p, mirrorY(BladeSide, TailAxis)[0], mirrorY(BladeSide, TailAxis)[2], .45), Ice);
  const rod = union(q => poly(q, [[12, TailAxis + 2.8], [12, TailAxis - 2.8], [-4, TailAxis - 1.3], [-4, TailAxis + 1.3]]), q => capsule(q, [8, TailAxis], [12, TailAxis], 2.8));
  banded(P, rod, p, GoldLook, 1.1);
}
const Painters = { head: skull, jaw, blade, body1: (P, p) => body(P, p, true), body2: (P, p) => body(P, p, false), tail };
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
export const Kinds = Object.keys(Painters);
