// Shared drawing for the Itachi kit: his stand-in and the Susanoo (complete armoured form, not
// Perfect). Used by itachi-susanoo.js; the C# port reads the shapes from here.
//
// Look, from the sources in the sketch header: flat translucent red-orange fill in two tones
// (deep 180,62,44 and lit 217,105,75, sampled from the ep 138 still), pale line art over it
// (contours, spiral swirls, flame curls), flame tips along the top of the head and shoulders, a
// ribcage inside, a cape, a tengu mask with a long nose over a dark mouth slot holding two yellow
// eyes and fangs, the Totsuka Blade as a flowing flame stream out of a gourd in the right hand,
// and the Yata Mirror as a round red shield with spirals in the left.
//
// Drawing: the Susanoo is drawn like a pawn sprite, upright in the screen plane, and always from
// the front whatever way Itachi faces (one view, no per-facing methods). A point (u, v) of the
// design is drawn at feet.x + u * kx, feet.z + v * kz, where the design is 3.5 cells tall and
// 3.2 wide and kx, kz scale it to the params. Screen right (+u) is the Susanoo's left side, so
// the mirror is on +u and the Totsuka on -u.
//
// Layers: everything that covers Itachi (cape, torso fill, back ribs, spine, base flames, the
// moving light in the fill) is drawn at Back, just under the pawn layer, so Itachi is drawn over
// it and stays fully visible; a faint red wash over him (MoteOverheadLow) says he is inside.
// Front ribs, line art, head, arms, gourd, blade, mirror and flame edges are drawn at Front
// (MoteOverhead) and above. Fills use the Transparent shader; lines, flames, eyes and the blade
// use MoteGlow (additive), so nothing is a hard opaque mesh. The only dark shape is the mouth
// slot. Light casts no shadow: the ground gets a warm light pool instead.
//
// Port notes: every fill is one strip mesh between a left and a right edge (fillPairs), every
// line is a strip of constant width along a polyline (stroke, with upTo for drawing on), the
// swirls and the mirror face are textured quads (make_itachi_textures.py). The flame edge is a
// low glowing rim (3 nested additive strips) with flame tongues standing on it: SusanooFlame.png
// quads, mostly upright, swaying, a pale core in each, every fifth a big lick that throws off a
// small tongue. The aura behind the whole figure is 14 hull points each sending up soft red
// tongues (Transparent haze + additive glow) on their own cycles, over a steady red haze; it
// grows from half height with the figure and is flame-yellow while it forms.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../../js/engine.js';
import { draw, mesh } from './six-paths-solid.js';
import { Y, Floor, soft, glow, rand } from './six-paths-impact.js';

export const smooth = t => Mathf.Smooth(Math.min(1, Math.max(0, t)));
export const clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const TAU = Math.PI * 2;
export const disc = Meshes.disc(40, 'itachi disc');
const ringMesh = Meshes.band(.93, 1, 64, 'itachi ring');
const flat = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
export const add = MaterialPool.MatFrom('white', ShaderDatabase.MoteGlow);
const swirlMat = MaterialPool.MatFrom('RimArt/Itachi/SusanooSwirl', ShaderDatabase.MoteGlow);
const mirrorMat = MaterialPool.MatFrom('RimArt/Itachi/SusanooMirror', ShaderDatabase.MoteGlow);
const curlMat = MaterialPool.MatFrom('RimArt/Itachi/SusanooCurl', ShaderDatabase.MoteGlow);
const flameMat = MaterialPool.MatFrom('RimArt/Itachi/SusanooFlame', ShaderDatabase.MoteGlow);
const hazeMat = MaterialPool.MatFrom('RimArt/Itachi/SusanooFlame', ShaderDatabase.Transparent);
export { Y, Floor, soft, glow, rand };

export const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
export const Back = AltitudeLayer.Projectile.AltitudeFor();
export const Wash = AltitudeLayer.MoteOverheadLow.AltitudeFor();
export const Front = Y;

// Colours. Fill and blade values are the sampled ones; Warm is the flame-yellow the anime forms
// it from before it settles to red.
const C = (r, g, b) => new Color(r, g, b, 1);
export const Deep = C(.71, .24, .17), Lit = C(.85, .41, .29), MirrorRed = C(.70, .18, .13), CapeRed = C(.52, .12, .09);
export const Warm = C(.98, .60, .24), WarmLit = C(1, .74, .38);
export const Line = C(1, .80, .66), LineWarm = C(1, .92, .66);
export const Bone = C(1, .64, .47), BoneWarm = C(1, .82, .52);
export const BladeCore = C(.96, .81, .50), BladeBody = C(.90, .66, .42), BladeEdge = C(.74, .40, .25), BladeHot = C(1, .95, .80);
export const Eye = C(.95, .80, .40), EyeHot = C(1, .96, .76), EyeGlow = C(1, .72, .18);
export const Mouth = C(.14, .02, .02), Fang = C(1, .90, .76);
export const FlameDeep = C(1, .32, .10), FlameMid = C(1, .56, .24), FlamePale = C(1, .86, .62);
export const AuraRed = C(.80, .10, .06), FlameBody = C(1, .46, .16);
export const Cloak = C(.09, .08, .10), Cloud = C(.78, .12, .12), CloudEdge = C(.95, .90, .90), Hair = C(.05, .04, .06);
export const Skin = C(.86, .73, .60), Sharingan = C(.95, .06, .05), Blood = C(.50, .03, .03);

// Design, in screen cells from Itachi's feet (u right, v up).
export const DesignH = 3.5, DesignW = 3.2;
export const FeetZ = -.33;              // the real-size stand-in's feet, from the cell centre
export const ArmRoot = { u: 1.0, v: 2.02 };  // shoulder joints at (±u, v)
export const Upper = .78, Fore = .72;
export const RestHand = { u: -1.63, v: 2.13 };  // right hand (screen left): elbow out at the side, forearm up, sword upright
export const RestMirror = { u: 1.52, v: 1.26 }; // left hand (screen right) = the mirror's centre
export const MirrorR = .70;
// Guarding: the mirror goes to the point GuardR from GuardC toward the attacker (as seen on
// screen), as far as the left arm reaches; the arm may stretch to 1.25 times its length, so a
// hit from the far side is met in front of the chest.
export const GuardC = { u: 0, v: 1.2 }, GuardR = 1.45, MaxStretch = 1.25;
export const RestBladeDeg = 100;                 // the Totsuka's idle direction on screen (0 = east)
// Ribs: [centre v, half width, how far the front end drops toward the sternum].
const RibSet = [[1.78, .46, .20], [1.58, .60, .24], [1.38, .70, .28], [1.17, .76, .32], [.96, .78, .34], [.75, .74, .34], [.55, .66, .32]];
const RibB = .10, SternumTop = 1.60, SternumFoot = 1.06, NeckTop = 2.32, RibTop = 1.9;
// Flame lines on the body, [u, v] for the right half (mirrored for the left): long S-curves rising
// from the waist and curling in under the chest.
const curl = (pts, n = 18) => { const e = smoothEdge(pts.map(([u, v]) => [v, u]), Math.ceil(n / (pts.length - 1))); return e.map(([v, u]) => [u, v]); };
const FlameLines = [
  curl([[.72, .55], [.62, 1.0], [.70, 1.45], [.52, 1.78], [.34, 1.74], [.40, 1.6]]),
  curl([[.95, 1.25], [.86, 1.7], [.92, 2.05], [.72, 2.22], [.62, 2.08]]),
];
// Edges are given as a few [v, half width] points and smoothed into curves (Catmull-Rom), so the
// body reads as a rounded mass and not a polygon.
function smoothEdge(edge, per = 4) {
  const out = [], n = edge.length, at = i => edge[Math.max(0, Math.min(n - 1, i))];
  for (let i = 0; i < n - 1; i++) for (let k = 0; k < per; k++) {
    const t = k / per, [p0, p1, p2, p3] = [at(i - 1), at(i), at(i + 1), at(i + 2)];
    const cr = j => .5 * (2 * p1[j] + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t * t + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t * t * t);
    out.push([cr(0), cr(1)]);
  }
  out.push(edge[n - 1]);
  return out;
}
// Torso: narrow waist, a broad chest, round shoulder humps closing into the neck.
const TorsoEdge = smoothEdge([[0, .78], [.2, .74], [.45, .74], [.9, .88], [1.5, 1.02], [1.95, 1.12], [2.15, 1.1], [2.32, .96], [2.45, .72], [2.52, .5]]);
const CapeEdge = smoothEdge([[.15, 1.40], [.6, 1.50], [1.2, 1.46], [1.9, 1.36], [2.2, 1.22], [2.32, 1.0]]);
const ChestEdge = smoothEdge([[1.38, .62], [1.6, .80], [1.9, .90], [2.15, .86], [2.32, .66], [2.42, .48]]);
const CollarEdge = smoothEdge([[2.28, .64], [2.48, .68], [2.72, .60]]);
const HeadEdge = smoothEdge([[2.28, .30], [2.42, .42], [2.66, .48], [2.92, .48], [3.12, .41], [3.26, .27], [3.34, .12], [3.36, 0]]);
const MaskEdge = [[2.64, .41], [2.80, .45], [2.98, .44], [3.10, .37]];
const SlotEdge = [[2.57, .30], [2.62, .41], [2.76, .42], [2.82, .33]];   // dark band at eye level
// The flame edge over the head and shoulders, left to right, so its left normal points out.
const TopPath = [[-1.62, 1.78], [-1.66, 2.05], [-1.5, 2.34], [-1.1, 2.46], [-.75, 2.5], [-.6, 2.72], [-.5, 2.95], [-.4, 3.16],
  [-.24, 3.3], [0, 3.37], [.24, 3.3], [.4, 3.16], [.5, 2.95], [.6, 2.72], [.75, 2.5], [1.1, 2.46], [1.5, 2.34], [1.66, 2.05], [1.62, 1.78]];
const CapeLeftPath = [[-1.42, .3], [-1.5, .7], [-1.47, 1.2], [-1.40, 1.8]];
const CapeRightPath = [[1.40, 1.8], [1.47, 1.2], [1.5, .7], [1.42, .3]];
// The base flames follow the front of the Susanoo's footprint on the floor, lowest in the middle.
const BasePath = Array.from({ length: 15 }, (_, i) => { const u = -1.3 + i / 14 * 2.6; return [u, .08 - .26 * (1 - (u / 1.3) ** 2)]; });

// ---------------------------------------------------------------- low-level drawing

export function strip(key, a, b, colour, material = flat, layer = Y) {
  if (colour.a <= .002 || a.length < 2) return;
  const v = [], t = [];
  for (let i = 0; i < a.length; i++) {
    v.push(a[i].x, a[i].z, b[i].x, b[i].z);
    if (i) { const n = i * 2; t.push(n - 2, n, n - 1, n - 1, n, n + 1); }
  }
  const m = mesh(key); m.setFlat(v, t);
  draw(m, 0, layer, 0, 1, 1, 0, colour, material);
}

export function sprite(pos, w, h, colour, material = glow, layer = Y, angle = 0) {
  if (colour.a <= .002) return;
  draw(MeshPool.plane10, pos.x, layer, pos.z, w, h, angle, colour, material);
}

// One flame tongue (SusanooFlame.png) standing on `base`, w wide and h tall, leaning `deg`
// degrees (positive = the tip to the right, as a clockwise turn on screen).
export function tongue(base, w, h, deg, colour, material = flameMat, layer = Y) {
  if (colour.a <= .002 || h <= .005) return;
  const a = deg * Mathf.Deg2Rad;
  draw(MeshPool.plane10, base.x + Math.sin(a) * h / 2, layer, base.z + Math.cos(a) * h / 2, w, h, deg, colour, material);
}

export function blob(pos, rx, rz, colour, layer = Y, rot = 0, material = flat) {
  if (colour.a <= .002) return;
  draw(disc, pos.x, layer, pos.z, rx, rz, rot, colour, material);
}

// A flat fill that glows: a see-through layer for the colour plus an additive layer, so it reads
// as red light over any ground instead of a painted shape.
export function fillStrip(key, a, b, colour, alpha, layer) {
  if (alpha <= .002) return;
  strip(key, a, b, colour.withAlpha(Math.min(.95, alpha)), flat, layer);
  strip(key + ' glow', a, b, new Color(colour.r * .7, colour.g * .3, colour.b * .26, alpha * .22), add, layer + .0001);
}
export function fillBlob(pos, rx, rz, colour, alpha, layer, rot = 0) {
  if (alpha <= .002) return;
  draw(disc, pos.x, layer, pos.z, rx, rz, rot, colour.withAlpha(Math.min(.95, alpha)), flat);
  draw(disc, pos.x, layer + .0001, pos.z, rx, rz, rot, new Color(colour.r * .7, colour.g * .3, colour.b * .26, alpha * .22), add);
}
// The part of an edge list ([v, half width]) between v0 and v1, ends interpolated.
function edgeBetween(edge, v0, v1) {
  const at = v => { for (let i = 1; i < edge.length; i++) if (edge[i][0] >= v) { const [a, b] = [edge[i - 1], edge[i]], f = (v - a[0]) / (b[0] - a[0] || 1); return [v, a[1] + (b[1] - a[1]) * f]; } return edge[edge.length - 1]; };
  return [at(v0), ...edge.filter(([v]) => v > v0 && v < v1), at(v1)];
}
// A line whose width runs from w0 at its start to w1 at its end.
export function taperLine(key, pts, w0, w1, colour, alpha, { layer = Y, upTo = 1, material = add } = {}) {
  if (upTo <= 0 || alpha <= .002) return;
  const P = cutPath(pts, upTo);
  if (P.length < 2) return;
  const a = [], b = [];
  P.forEach((p, i) => {
    const q0 = P[Math.max(0, i - 1)], q1 = P[Math.min(P.length - 1, i + 1)], dx = q1.x - q0.x, dz = q1.z - q0.z, L = Math.hypot(dx, dz) || 1;
    const w = lerp(w0, w1, i / (P.length - 1)) / 2;
    a.push({ x: p.x - dz / L * w, z: p.z + dx / L * w }); b.push({ x: p.x + dz / L * w, z: p.z - dx / L * w });
  });
  const wide = (q, i) => ({ x: P[i].x + (q.x - P[i].x) * 3.2, z: P[i].z + (q.z - P[i].z) * 3.2 });
  strip(key + ' halo', a.map(wide), b.map(wide), colour.withAlpha(alpha * .2), add, layer);
  strip(key, a, b, colour.withAlpha(alpha), material, layer + .0004);
}

const len = (a, b) => Math.hypot(b.x - a.x, b.z - a.z);

// The first `share` (0..1) of a polyline's length.
export function cutPath(pts, share) {
  if (share >= 1) return pts;
  let total = 0; for (let i = 1; i < pts.length; i++) total += len(pts[i - 1], pts[i]);
  let want = total * Math.max(0, share); const out = [pts[0]];
  for (let i = 1; i < pts.length; i++) {
    const d = len(pts[i - 1], pts[i]);
    if (want <= d) { const f = d > 0 ? want / d : 0; out.push({ x: lerp(pts[i - 1].x, pts[i].x, f), z: lerp(pts[i - 1].z, pts[i].z, f) }); return out; }
    want -= d; out.push(pts[i]);
  }
  return out;
}

// Evenly spaced points along a polyline with unit left normals (left of the walking direction).
export function resample(pts, n) {
  const cum = [0];
  for (let i = 1; i < pts.length; i++) cum.push(cum[i - 1] + len(pts[i - 1], pts[i]));
  const total = cum[cum.length - 1] || 1, out = [];
  let j = 1;
  for (let k = 0; k <= n; k++) {
    const want = k / n * total;
    while (j < pts.length - 1 && cum[j] < want) j++;
    const d = cum[j] - cum[j - 1] || 1, f = clamp((want - cum[j - 1]) / d);
    out.push({ x: lerp(pts[j - 1].x, pts[j].x, f), z: lerp(pts[j - 1].z, pts[j].z, f), t: k / n });
  }
  out.forEach((p, i) => {
    const a = out[Math.max(0, i - 1)], b = out[Math.min(out.length - 1, i + 1)];
    const dx = b.x - a.x, dz = b.z - a.z, L = Math.hypot(dx, dz) || 1;
    p.nx = -dz / L; p.nz = dx / L;
  });
  return out;
}

// A line of constant width along pts (taper 1 = thins to nothing at both ends). upTo draws only
// the first share of its length, for lines that draw themselves on.
export function stroke(key, pts, width, colour, { material = add, layer = Y, upTo = 1, taper = 0 } = {}) {
  if (upTo <= 0 || colour.a <= .002) return;
  const P = cutPath(pts, upTo);
  if (P.length < 2) return;
  const a = [], b = [];
  P.forEach((p, i) => {
    const q0 = P[Math.max(0, i - 1)], q1 = P[Math.min(P.length - 1, i + 1)];
    const dx = q1.x - q0.x, dz = q1.z - q0.z, L = Math.hypot(dx, dz) || 1;
    const f = taper ? lerp(1, Math.sin(i / (P.length - 1) * Math.PI), taper) : 1, w = width / 2 * f;
    a.push({ x: p.x - dz / L * w, z: p.z + dx / L * w }); b.push({ x: p.x + dz / L * w, z: p.z - dx / L * w });
  });
  strip(key, a, b, colour, material, layer);
}

// A glowing line: a soft wide halo under a thin bright core.
export function glowLine(key, pts, width, colour, alpha, opts = {}) {
  stroke(key + ' halo', pts, width * 3.2, colour.withAlpha(alpha * .22), { ...opts, taper: opts.taper ?? 0 });
  stroke(key, pts, width, colour.withAlpha(alpha), { ...opts, layer: (opts.layer ?? Y) + .0004 });
}

// A row of flame tips along a path: 3 nested additive strips (deep outside, pale inside) whose
// outer edge is the tallest tip at each point, each tip rising, flickering and surging on its
// own; a wisp breaks off at the top of each surge. `lit(t)` (0..1 along the path) scales the
// tips, for flames that light from one end. `up` bends them upward, as flames rise.
export function flameEdge(key, pts, s, height, alpha, { count = 30, seed = 0, layer = Y, up = .9, lit = null, inset = .03 } = {}) {
  if (alpha <= .002 || height <= .001) return;
  const tips = [];
  for (let k = 0; k < count; k++) {
    const r = i => rand(seed * 97 + k * 7 + i);
    const phase = s / (.26 + .14 * r(1)) + r(2), u = phase - Math.floor(phase);
    const flicker = .7 + .3 * Math.sin(s * (17 + 8 * r(3)) + k * 2.1);
    const t = (k + .5 + (r(4) - .5) * .8) / count, on = lit ? lit(t) : 1;
    // Every fifth tongue or so is a big lick, 2x taller.
    const big = r(6) > .8 ? 2 : 1;
    tips.push({ t, u, big, r, h: height * (.45 + .55 * r(5)) * flicker * (.75 + .5 * Math.sin(u * Math.PI)) * on });
  }
  const steps = count * 5, P = resample(pts, steps), half = .9 / count;
  let total = 0; for (let i = 1; i < P.length; i++) total += Math.hypot(P[i].x - P[i - 1].x, P[i].z - P[i - 1].z);
  const spacing = total / count;
  const reach = P.map(p => {
    let h = 0;
    tips.forEach(q => { const d = Math.abs(p.t - q.t) / half; if (d < 1) h = Math.max(h, q.h * Math.pow(1 - d, 1.8)); });
    return h;
  });
  const dirOf = p => { let dx = p.nx, dz = p.nz + up; const L = Math.hypot(dx, dz) || 1; return { x: dx / L, z: dz / L }; };
  // The glowing rim the tongues stand on: three nested strips, low.
  [[.55, FlameDeep, .38], [.34, FlameMid, .45], [.16, FlamePale, .5]].forEach(([share, colour, a], j) => {
    const inner = [], outer = [];
    P.forEach((p, i) => {
      const d = dirOf(p), h = .01 + reach[i] * share;
      inner.push({ x: p.x - p.nx * inset, z: p.z - p.nz * inset });
      outer.push({ x: p.x + d.x * h, z: p.z + d.z * h });
    });
    strip(`${key} flame ${j}`, inner, outer, colour.withAlpha(a * alpha), add, layer + j * .0002);
  });
  // Tongues: each rises from the rim, leans with the edge but mostly up (flames rise), sways,
  // with a pale core; big licks on some. A small tongue breaks off the top of each surge.
  tips.forEach((q, k) => {
    if (q.h <= .01) return;
    const p = P[Math.min(steps, Math.round(q.t * steps))], d = dirOf(p);
    // Mostly upright: only 40 % of the edge's lean, so the crown does not fan out like a starburst.
    const lean = Math.max(-25, Math.min(25, Math.atan2(d.x, d.z) * 180 / Math.PI * .4)) + 10 * Math.sin(s * (4 + 3 * q.r(7)) + k * 1.7);
    const h = (q.h * 1.5 + .04) * (q.big > 1 ? 1.6 : 1), w = Math.min(.55, Math.max(.12, spacing * 2.4)) * (q.big > 1 ? 1.25 : 1) * (.8 + .4 * q.r(8));
    const base = { x: p.x - d.x * .02, z: p.z - d.z * .02 };
    tongue(base, w, h, lean, FlameBody.withAlpha(.55 * alpha), flameMat, layer + .0006);
    tongue(base, w * .45, h * .55, lean * .8, FlamePale.withAlpha(.4 * alpha), flameMat, layer + .0007);
    if (q.u > .55 && q.big > 1) {
      const v = (q.u - .55) / .45, a = lean * Mathf.Deg2Rad, rise = h * (1 + .9 * v);
      const at = { x: base.x + Math.sin(a) * rise + .08 * Math.sin(s * 6 + k), z: base.z + Math.cos(a) * rise + .25 * v };
      tongue(at, w * .45 * (1 - .5 * v), h * .35 * (1 - .4 * v), lean, FlameMid.withAlpha(.55 * (1 - v) * alpha), flameMat, layer + .0008);
    }
  });
}

// The aura: a red haze round the whole Susanoo, rising a little above the head in soft flame
// tongues (Outer flame slider: 0 off, 1 = 70 % of the first version's height, 2 = 115 %), as the anime shows it standing inside a column of red chakra. Tongues stand on a
// hull round the figure (design units), each rising and fading on its own cycle, taller at the
// top; a steady haze sits behind the body. Drawn behind everything of the Susanoo. k 0..1.
const AuraHull = [[-1.55, .1], [-1.75, .9], [-1.75, 1.7], [-1.55, 2.25], [-1.0, 2.5], [-.6, 2.85], [-.4, 3.25], [0, 3.42],
  [.4, 3.25], [.6, 2.85], [1.0, 2.5], [1.55, 2.25], [1.95, 1.85], [2.05, 1.2], [1.85, .5], [1.55, .1]];
export function aura(key, S, s, k, layer, grow = 1, warm = 0, size = 1) {
  if (k <= .002 || size <= .002) return;
  // grow 0..1 raises the hull from half height to full with the figure; warm tints it the
  // flame-yellow the Susanoo forms from.
  const red = Color.Lerp(AuraRed, C(.95, .45, .12), warm * .8), vs = lerp(.45, 1, grow);
  // size scales the tongues: 1 = 70 % of their first height and 80 % of its strength, 2 = 115 %.
  const hs = .25 + .45 * size, as = Math.min(1, .35 + .45 * size);
  sprite(S(0, 1.5 * vs), 3.3, 3.1 * vs, red.withAlpha(.12 * k * as), soft, layer - .002);
  sprite(S(0, 2.8 * vs), 1.6, 1.4, red.withAlpha(.1 * k * grow * as), soft, layer - .0019);
  const P = resample(AuraHull.map(([u, v]) => S(u, v * vs)), 36);
  const n = 14;
  for (let i = 0; i < n; i++) {
    const r = j => rand(i * 17 + j + 1200);
    const t = (i + .5 + (r(1) - .5) * .6) / n, p = P[Math.round(t * 36)];
    const top = 1 - Math.abs(2 * t - 1);
    for (let c = 0; c < 2; c++) {
      const period = .9 + .5 * r(2 + c), ph = s / period + r(3 + c) + c * .5, u = ph - Math.floor(ph);
      const a = Math.sin(u * Math.PI) * k;
      const h = (1.0 + 1.2 * top + .4 * r(5)) * (.7 + .3 * u) * hs, w = .8 + .4 * r(6);
      const lean = Math.max(-40, Math.min(40, Math.atan2(p.nx, p.nz + 1.2) * 180 / Math.PI)) * .6 + 10 * Math.sin(s * 2 + i);
      const base = { x: p.x - p.nx * .35 + p.nx * u * .2, z: p.z - p.nz * .35 + u * .3 };
      tongue(base, w, h * lerp(.6, 1, grow), lean, red.withAlpha(.3 * a * as), hazeMat, layer + c * .0001);
      tongue(base, w * .7, h * .8 * lerp(.6, 1, grow), lean, FlameDeep.withAlpha(.14 * a * as), flameMat, layer + .0002 + c * .0001);
    }
  }
}

// ---------------------------------------------------------------- the Susanoo

// Growth for the warm-up, from seconds since the cast and the warm-up length. Each part runs
// 0..1: ribs rise out of the floor, then the skull and the skeletal right arm, then armour, cape
// and face fill in with the flame edge lighting from the shoulders up, the gourd pours the blade,
// the mirror's ring draws round, and the yellow eyes light last.
export function growth(s, warmUp) {
  const g = s / warmUp;
  return {
    started: g >= 0,
    pool: smooth(g / .15),
    glint: g < 0 ? 0 : Math.max(0, 1 - Math.abs(g - .05) / .08),
    ribs: clamp((g - .10) / .30),
    skel: clamp((g - .35) / .25),
    oneEye: clamp((g - .52) / .08),
    armour: clamp((g - .55) / .45),
    blade: clamp((g - .66) / .30),
    mirror: clamp((g - .70) / .30),
    eyes: clamp((g - .93) / .07),
    flash: g < .93 ? 0 : Math.max(0, 1 - (g - .93) / .35),
    alpha: 1, fade: NoFade, dim: 0,
  };
}
const NoFade = { head: 1, arms: 1, body: 1, base: 1 };
export const Complete = { started: true, pool: 1, glint: 0, ribs: 1, skel: 1, oneEye: 1, armour: 1, blade: 1, mirror: 1, eyes: 1, flash: 0, alpha: 1, fade: NoFade, dim: 0 };

// The end, from seconds since it starts breaking (tb), the break-apart length D and how far the
// dimming before it has got (dim 0..1). Parts go top first, as windows of D: head and top flames
// 0-0.35, arms, blade and mirror 0.15-0.5, cape, torso and shoulders 0.3-0.7; the bones are
// revealed as the armour leaves and then sink back into the floor (the raise in reverse):
// collarbones and arm bones 0.55-0.8, spine and ribs 0.6-1.0; base flames and the light pool
// 0.7-1.0.
export function breaking(tb, D, dim) {
  const u = tb / D, out = (a, b) => 1 - smooth((u - a) / (b - a));
  return {
    ...Complete, dim,
    fade: { head: out(0, .35), arms: out(.15, .5), body: out(.3, .7), base: out(.7, 1) },
    skel: out(.55, .8), ribs: out(.6, 1), pool: out(.7, 1), eyes: out(0, .3),
  };
}
// Where the embers of each part start (design units) and when (share of D): the same windows.
const EmberParts = [
  { t0: 0, t1: .35, n: 34, at: r => ({ u: (r(1) - .5) * .95, v: 2.3 + r(2) * 1.1 }) },                       // head and crown
  { t0: .15, t1: .5, n: 30, at: r => r(1) < .5 ? { u: -1.1 - r(2) * .7, v: .9 + r(3) * 1.3 } : { u: 1.0 + r(2) * 1.1, v: .6 + r(3) * 1.4 } }, // arms, blade, mirror
  { t0: .3, t1: .7, n: 56, at: r => ({ u: (r(1) - .5) * 2.4, v: .3 + r(2) * 2.1 }) },                        // cape, torso, shoulders
];
// Embers of the breaking Susanoo: small flecks of red-orange light rising and drifting from
// where each part was, as it goes. tb and D as in breaking().
export function embers(F, look, tb, D) {
  EmberParts.forEach((part, j) => {
    for (let i = 0; i < part.n; i++) {
      const r = k => rand(j * 1000 + i * 13 + k + 800);
      const born = (part.t0 + (part.t1 - part.t0) * r(4)) * D, life = .6 + .5 * r(5), age = tb - born;
      if (age < 0 || age > life) continue;
      const u = age / life, p0 = part.at(r), rise = (.7 + .8 * r(6)) * age + .35 * age * age;
      const sway = Math.sin(age * (3 + 3 * r(7)) + r(8) * 6) * .12 * age + (r(9) - .5) * .5 * age;
      const pos = { x: F.x + p0.u * look.kx + sway, z: F.z + p0.v * look.kz + rise };
      // A fleck: a thin bright streak along its path (up and swaying), flickering, most of them
      // small; a faint glow round the bigger ones only.
      const big = r(10) > .8, size = (big ? .09 : .04 + .035 * r(12)) * (1 - u * .5);
      const flicker = .6 + .4 * Math.sin(age * (22 + 14 * r(13)) + i);
      const a = Math.sin(Math.min(1, u * 5) * Math.PI / 2) * (1 - u) * flicker;
      const vx = Math.cos(age * (3 + 3 * r(7)) + r(8) * 6) * .12 * (3 + 3 * r(7)) * age + (r(9) - .5) * .5, vz = (.7 + .8 * r(6)) + .7 * age;
      const L = Math.hypot(vx, vz) || 1, tail = size * (3 + 3 * r(14));
      const back = { x: pos.x - vx / L * tail, z: pos.z - vz / L * tail };
      if (big) sprite(pos, .3, .3, FlameDeep.withAlpha(.3 * a), glow, Front + .04);
      stroke(`ember ${j} ${i}`, [back, pos], size * 2, (r(11) < .4 ? FlamePale : FlameMid).withAlpha(.95 * a), { taper: .7, layer: Front + .0401 });
    }
  });
}

// 2-bone reach: the elbow for a hand at `hand` from `root`, bent to the outside (side -1 = screen
// left, +1 = screen right), or with down 0..1 moved toward the lower of the two elbows (an arm
// hanging at the side with the forearm out). Points are in design units.
export function elbowFor(root, hand, side, down = 0, L1 = Upper, L2 = Fore) {
  let dx = hand.u - root.u, dv = hand.v - root.v, d = Math.hypot(dx, dv) || 1e-4;
  const ux = dx / d, uv = dv / d;
  d = Math.min(L1 + L2 - 1e-3, Math.max(Math.abs(L1 - L2) + 1e-3, d));
  const a = (L1 * L1 - L2 * L2 + d * d) / (2 * d), h = Math.sqrt(Math.max(0, L1 * L1 - a * a));
  const mx = root.u + ux * a, mv = root.v + uv * a;
  const e1 = { u: mx - uv * h, v: mv + ux * h }, e2 = { u: mx + uv * h, v: mv - ux * h };
  const out = side < 0 ? (e1.u < e2.u ? e1 : e2) : (e1.u > e2.u ? e1 : e2), low = e1.v < e2.v ? e1 : e2;
  const elbow = { u: lerp(out.u, low.u, down), v: lerp(out.v, low.v, down) };
  return { elbow, hand: { u: root.u + ux * d, v: root.v + uv * d } };
}

export const bobAt = s => .018 * Math.sin(s * 2.2);
export const toScreen = (F, look, u, v) => ({ x: F.x + u * look.kx, z: F.z + v * look.kz });

// Where the mirror's centre goes (design units) to guard toward screen angle `deg`.
export function mirrorAt(deg) {
  const a = deg * Mathf.Deg2Rad, want = { u: GuardC.u + Math.cos(a) * GuardR, v: GuardC.v + Math.sin(a) * GuardR };
  const dx = want.u - ArmRoot.u, dv = want.v - ArmRoot.v, d = Math.hypot(dx, dv), reach = (Upper + Fore) * MaxStretch - .01;
  return d <= reach ? want : { u: ArmRoot.u + dx / d * reach, v: ArmRoot.v + dv / d * reach };
}
// The mirror's centre and radii in world cells, for a pose's mirror position at time s.
export function mirrorWorld(F, look, s, pose) {
  const c = toScreen(F, look, pose.mirror.u, pose.mirror.v + bobAt(s));
  return { x: c.x, z: c.z, rx: MirrorR * look.kx, rz: MirrorR * look.kz };
}
// The point on the mirror's rim facing `from` (world cells), and that point as {u, v} on the
// unit face for ripple().
export function rimToward(m, from) {
  const ang = Math.atan2((from.z - m.z) / m.rz, (from.x - m.x) / m.rx);
  return { x: m.x + Math.cos(ang) * m.rx * .96, z: m.z + Math.sin(ang) * m.rz * .96, at: { u: Math.cos(ang) * .9, v: Math.sin(ang) * .9 } };
}

// The Totsuka's gourd mouth in world cells for a pose at time s (where the blade leaves the gourd).
export function gourdMouth(F, look, s, pose) {
  const b = bobAt(s), root = { u: -ArmRoot.u, v: ArmRoot.v + b };
  const { hand } = elbowFor(root, { u: pose.hand.u, v: pose.hand.v + b }, -1, pose.elbowDown ?? 0);
  const a = pose.bladeDeg * Mathf.Deg2Rad;
  return toScreen(F, look, hand.u + Math.cos(a) * .33, hand.v + Math.sin(a) * .33);
}
// The right hand's reach toward a world point: the hand goes `reach` design units from the
// shoulder toward it (as seen on screen).
export function handToward(F, look, target, reach = 1.42) {
  const root = toScreen(F, look, -ArmRoot.u, ArmRoot.v), dx = (target.x - root.x) / look.kx, dv = (target.z - root.z) / look.kz, d = Math.hypot(dx, dv) || 1;
  return { u: -ArmRoot.u + dx / d * reach, v: ArmRoot.v + dv / d * reach };
}

export const restPose = () => ({ hand: RestHand, elbowDown: 1, bladeDeg: RestBladeDeg, bladeTip: null, mirror: RestMirror, bladeLen: null, stab: 0, mirrorHit: [] });

// Draws the Susanoo. F: Itachi's feet on screen. s: seconds, for flicker and flow. look:
// { kx, kz, fill, line, flameH, bladeLen }. g: growth (or Complete). pose: restPose() or a
// changed copy (hand and mirror in design units; bladeTip in world cells when the blade reaches
// a target).
export function susanoo(key, F, s, look, g, pose) {
  const { kx, kz } = look, A = g.alpha;
  if (!g.started || A <= .002) return;
  const S = (u, v) => ({ x: F.x + u * kx, z: F.z + v * kz });
  const pair = (edge, dv = 0) => [edge.map(([v, h]) => S(-h, v + dv)), edge.map(([v, h]) => S(h, v + dv))];
  const bob = .018 * Math.sin(s * 2.2);
  const warmK = 1 - g.armour;
  const deep = Color.Lerp(Deep, Warm, warmK), lit = Color.Lerp(Lit, WarmLit, warmK);
  const line = Color.Lerp(Line, LineWarm, warmK), bone = Color.Lerp(Bone, BoneWarm, warmK);
  const fd = g.fade ?? { head: 1, arms: 1, body: 1, base: 1 }, dim = g.dim ?? 0;
  // Dimming before it breaks: fill and lines darker, flames lower.
  const dimmed = c => Color.Lerp(c, new Color(c.r * .62, c.g * .5, c.b * .5, 1), dim);
  const deepD = dimmed(deep), litD = dimmed(lit), lineD = Color.Lerp(line, Deep, dim * .35);
  look = { ...look, flameH: look.flameH * (1 - .55 * dim) };
  const fillA = look.fill * A, lineA = look.line * A * (1 - .25 * dim);
  const aK = smooth(g.armour);
  const bodyFill = fillA * fd.body, bodyLine = lineA * fd.body;

  // Floor: the warm light pool, and a glow round the whole figure.
  sprite(S(0, .05), 3.9 * kx, 2.2 * kz, FlameDeep.withAlpha(.20 * g.pool * A), glow, Floor + .01);
  sprite(S(0, .05), 2.2 * kx, 1.2 * kz, FlameMid.withAlpha(.16 * g.pool * A), glow, Floor + .011);
  const halo = (.07 + .12 * g.flash) * A * fd.body * (1 - .5 * dim);
  sprite(S(0, 1.4), 4.2 * kx, 4.0 * kz, FlameDeep.withAlpha(halo * Math.max(g.ribs * .6, g.armour)), glow, Back - .02);
  sprite(S(0, 2.8), 2.2 * kx, 1.8 * kz, FlameDeep.withAlpha(halo * g.armour), glow, Back - .019);

  // ---- behind Itachi
  aura(`${key} aura`, S, s, Math.max(g.ribs * .5, aK) * fd.body * (1 - .5 * dim) * A, Back - .03, smooth(g.ribs * .45 + g.skel * .25 + aK * .3), warmK, look.aura ?? 1);
  // Cape: grows out from the shoulders down.
  if (aK > 0) {
    const cape = CapeEdge.map(([v, h]) => [lerp(2.2, v, aK), lerp(.9, h, aK)]);
    [[.15, .6, .35], [.6, 2.32, 1]].forEach(([v0, v1, f], i) => {
      const [cl, cr] = pair(edgeBetween(cape, v0, v1));
      fillStrip(`${key} cape ${i}`, cl, cr, dimmed(CapeRed), .6 * bodyFill * aK * f, Back);
    });
    flameEdge(`${key} cape l`, CapeLeftPath.map(([u, v]) => S(u, v)), s, look.flameH * .55, A * aK * fd.body, { count: 10, seed: 3, layer: Back + .001, up: 1.4, lit: t => clamp(aK * 1.6 - (1 - t)) });
    flameEdge(`${key} cape r`, CapeRightPath.map(([u, v]) => S(u, v)), s, look.flameH * .55, A * aK * fd.body, { count: 10, seed: 4, layer: Back + .001, up: 1.4, lit: t => clamp(aK * 1.6 - t) });
  }
  // Spine: rises out of the floor to the top of the ribcage with the ribs, then on to the neck
  // with the skull. Each rib unrolls round from the spine once the spine top has passed it,
  // lowest first. Once the armour is on, the bones dim to what shows through the body.
  const spineTop = Math.max(RibTop * smooth(g.ribs), lerp(RibTop, NeckTop, smooth(g.skel)) * (g.skel > 0 ? 1 : 0));
  const boneA = lerp(1, .15, aK * fd.body) * A;
  if (g.ribs > 0) {
    taperLine(`${key} spine`, [S(0, 0), S(0, spineTop + bob)], .10 * kx, .06 * kx, bone, .6 * boneA, { layer: Back + .003 });
    for (let k = 0; .12 + k * .15 < spineTop; k++) blob(S(0, .12 + k * .15 + bob), .065 * kx, .04 * kz, bone.withAlpha(.45 * boneA), Back + .004, 0, add);
  }
  const ribGrow = RibSet.map(([vc]) => clamp((spineTop - vc) / .4));
  // Back halves: thin and dim, seen through the body.
  RibSet.forEach(([vc, w], i) => {
    const r = ribGrow[i]; if (r <= 0) return;
    for (const side of [-1, 1]) {
      const back = []; for (let j = 0; j <= 10; j++) { const a = j / 10 * Math.PI / 2; back.push(S(side * Math.sin(a) * w, vc + bob + Math.cos(a) * RibB)); }
      taperLine(`${key} rib back ${i} ${side}`, back, .045 * kx, .035 * kx, bone, .35 * boneA, { layer: Back + .005, upTo: clamp(r * 2) });
    }
  });
  // Torso: fills in from the ribs outward (its edge swells from the ribcage's width); its foot
  // fades into the floor in two steps instead of ending on a line.
  if (aK > 0) {
    const torso = TorsoEdge.map(([v, h]) => [v, lerp(.55, h, aK)]);
    [[0, .22, .25], [.22, .5, .6], [.5, 2.5, 1]].forEach(([v0, v1, f], i) => {
      const [tl, tr] = pair(edgeBetween(torso, v0, v1), bob);
      fillStrip(`${key} torso ${i}`, tl, tr, deepD, bodyFill * aK * f, Back + .006);
    });
    const [pl, pr] = pair(ChestEdge.map(([v, h]) => [v, lerp(.3, h, aK)]), bob);
    fillStrip(`${key} chest`, pl, pr, litD, .45 * bodyFill * aK, Back + .007);
    // Darker blotches drifting slowly in the fill, the mottled flame look of the anime's fill.
    for (let i = 0; i < 8; i++) {
      const u0 = (rand(i + 360) - .5) * 1.3, rise = Mathf.Repeat(s * (.06 + .04 * rand(i + 370)) + rand(i + 380), 1);
      const v = .5 + rise * 1.7, fade = Math.sin(rise * Math.PI) * (.7 + .3 * Math.sin(s * 1.7 + i * 2));
      sprite(S(u0, v), (.45 + .3 * rand(i + 390)) * kx, (.35 + .25 * rand(i + 400)) * kz, C(.36, .05, .04).withAlpha(.28 * fade * aK * A * fd.body), soft, Back + .0071, rand(i + 410) * 180);
    }
    // Moving light inside the fill: soft blobs drifting up, as the anime's fill churns.
    for (let i = 0; i < 9; i++) {
      const u0 = (rand(i + 300) - .5) * 1.5, rise = Mathf.Repeat(s * (.22 + .1 * rand(i + 310)) + rand(i + 320), 1);
      const v = .2 + rise * 2.0, fade = Math.sin(rise * Math.PI);
      sprite(S(u0 + .08 * Math.sin(s * 1.3 + i), v), (.55 + .3 * rand(i + 330)) * kx, (.45 + .3 * rand(i + 340)) * kz, FlameMid.withAlpha(.07 * fade * aK * A * fd.body * (1 - dim)), glow, Back + .0075);
    }
  }
  // Base flames rising out of the floor along the front of the footprint.
  const baseH = look.flameH * .7 * (.35 + .65 * Math.max(g.ribs * .5, aK));
  flameEdge(`${key} base`, BasePath.map(([u, v]) => S(u * lerp(.6, 1, aK), v)), s, baseH, A * Math.max(.6 * g.pool, aK) * fd.base, { count: 18, seed: 5, layer: Back + .008, up: 2 });

  // ---- in front of Itachi
  // A faint red wash over Itachi, so he reads as inside.
  sprite(S(0, .45), .95, 1.25, Deep.withAlpha(.20 * Math.max(g.ribs, aK) * A * Math.max(fd.body, .4 * g.ribs)), soft, Wash);
  // Front halves of the ribs: from the side they drop in a curve toward the sternum, lower ribs
  // more; the upper five reach the sternum, the lower ones turn up to its foot (the costal arch).
  RibSet.forEach(([vc, w, drop], i) => {
    const r = ribGrow[i]; if (r <= 0) return;
    const gap = i < 5 ? .07 : .2;
    for (const side of [-1, 1]) {
      const front = [];
      for (let j = 0; j <= 12; j++) { const f = j / 12; front.push(S(side * lerp(w, gap, f * f * (3 - 2 * f) * .9 + f * .1), vc + bob - drop * Math.sin(f * Math.PI / 2) * (1 - .25 * f))); }
      taperLine(`${key} rib front ${i} ${side}`, front, .08 * kx, .035 * kx, bone, .8 * boneA, { layer: Front + .002, upTo: clamp(r * 2 - 1) });
    }
  });
  const archK = clamp(ribGrow[RibSet.length - 1] * 2 - 1);
  if (archK > 0) for (const side of [-1, 1]) {
    const pts = [S(side * .04, SternumFoot + bob), S(side * .22, .78 + bob), S(side * .38, .58 + bob)];
    taperLine(`${key} arch ${side}`, pts, .04 * kx, .05 * kx, bone, .6 * boneA, { layer: Front + .002, upTo: archK });
  }
  const sternK = clamp(ribGrow[0] * 2 - 1);
  if (sternK > 0) taperLine(`${key} sternum`, [S(0, RibSet[0][0] - RibSet[0][2] + .02 + bob), S(0, SternumFoot + bob)], .12 * kx, .06 * kx, bone, .7 * boneA, { layer: Front + .0021, upTo: sternK });
  // Collarbones from the sternum's top out to the shoulders, with the skeletal arm.
  const clavK = clamp(g.skel * 3);
  if (clavK > 0) for (const side of [-1, 1]) {
    const pts = [S(side * .06, RibTop - .06 + bob), S(side * .45, RibTop + .02 + bob), S(side * .8, RibTop + .12 + bob), S(side * ArmRoot.u, ArmRoot.v + bob)];
    taperLine(`${key} clavicle ${side}`, pts, .07 * kx, .06 * kx, bone, .8 * boneA, { layer: Front + .0022, upTo: clavK });
    blob(S(side * ArmRoot.u, ArmRoot.v + bob), .09 * kx * clavK, .08 * kz * clavK, bone.withAlpha(.5 * boneA), Front + .0023, 0, add);
  }

  // Torso contour (from the waist up, so its foot has no line) and chest lines, drawn on.
  if (aK > 0) {
    const [tl, tr] = pair(edgeBetween(TorsoEdge, .45, 2.5), bob);
    glowLine(`${key} torso l`, tl, .022 * kx, lineD, .8 * bodyLine, { layer: Front + .004, upTo: aK });
    glowLine(`${key} torso r`, tr, .022 * kx, lineD, .8 * bodyLine, { layer: Front + .004, upTo: aK });
    // Flowing flame lines up the body, curling in toward the chest, as the anime draws the fill.
    FlameLines.forEach((pts, i) => {
      for (const side of [-1, 1]) {
        glowLine(`${key} flame line ${i} ${side}`, pts.map(([u, v]) => S(side * u, v + bob)), .02 * kx, lineD, .55 * bodyLine, { layer: Front + .004, upTo: clamp(aK * 1.5 - .3 - i * .1), taper: 1 });
      }
    });
    sprite(S(0, 1.95 + bob), .7 * kx, .55 * kz, lineD.withAlpha(.5 * bodyLine * clamp(aK * 2 - 1)), swirlMat, Front + .005, 0);
  }

  // Shoulder plates.
  const ap = smooth(clamp(aK * 1.3));
  for (const side of [-1, 1]) {
    const c = S(side * 1.22, 2.08 + bob), sc = lerp(.5, 1, ap);
    // Drawn over the upper arms (whose fills and lines sit at Front + .022-.0235) so the plate
    // caps the shoulder; the fist (.0246) and the mirror (.03) still draw over it.
    fillBlob(S(side * 1.34, 1.78 + bob), .42 * kx * sc, .2 * kz * sc, litD, .5 * bodyFill * ap, Front + .0236, side * 12);
    fillBlob(c, .5 * kx * sc, .36 * kz * sc, litD, .62 * bodyFill * ap, Front + .0237, side * 10);
    draw(ringMesh, c.x, Front + .0238, c.z, .5 * kx * sc, .36 * kz * sc, side * 10, lineD.withAlpha(.5 * bodyLine * ap), add);
    sprite(c, .62 * kx * sc, .5 * kz * sc, lineD.withAlpha(.75 * bodyLine * ap), swirlMat, Front + .0239, side > 0 ? 0 : 180);
  }

  head(key, S, s, g, { line: lineD, bone, lit: litD, deep: deepD, fillA: fillA * fd.head, lineA: lineA * fd.head, aK, bob, kx, kz, A: A * fd.head });

  // The flame edge over the head and shoulders, lighting from the shoulders up.
  if (aK > 0) {
    const top = TopPath.map(([u, v]) => S(u, v + bob));
    flameEdge(`${key} top`, top, s, look.flameH, A * clamp(aK * 2) * fd.head, {
      count: 40, seed: 1, layer: Front + .0005, up: .7, lit: t => clamp((aK * 1.6 - (1 - Math.abs(2 * t - 1))) / .5) * (1 + .7 * (1 - Math.abs(2 * t - 1))),
    });
  }

  // ---- arms
  arms(key, S, s, look, { ...g, alpha: A * fd.arms }, pose, { line: lineD, bone, lit: litD, deep: deepD, fillA: fillA * fd.arms, lineA: lineA * fd.arms, aK, bob, boneAlpha: A, boneDim: aK * fd.body });
}

// The head. Skeletal stage: a skull with one lit eye (the Kabuto fight). Then the helmet fills
// in over it as the source draws it (ep 138 still, manga ch. 393): a hood wrapping a tengu mask
// turned a little to screen left (the user's ep 138 front frame): a glowing orange crystal on the
// forehead above the nose root, one small dark eye with a socket line, a long curved spike nose
// pointing up and to the left out past the hood into the flames, an ear with a ring and a bead
// on the right, and the mask's mouth as a dark trapezoid
// slot holding the Susanoo's own yellow eyes, with red teeth outlined in pale line: a fang down
// between the eyes, a tall tusk up at the right corner, block teeth along the bottom lip.
const HoodEdge = smoothEdge([[2.2, .66], [2.45, .66], [2.75, .62], [3.0, .56], [3.22, .44], [3.4, .26], [3.5, .06]]);
const FaceC = { u: -.05, v: 2.76 }, FaceR = { u: .36, v: .44 };
function head(key, S, s, g, c) {
  const { line, bone, lit, deep, fillA, lineA, aK, bob, kx, kz, A } = c;
  const headK = smooth(clamp((aK - .2) / .8));
  const skullA = g.skel * (1 - headK) * A;
  if (skullA > .002) {
    const skull = [];
    for (let j = 0; j <= 24; j++) { const a = -Math.PI * .15 + j / 24 * Math.PI * 1.3; skull.push(S(Math.cos(a) * .38, 2.86 + bob + Math.sin(a) * .42)); }
    glowLine(`${key} skull`, skull, .05 * kx, bone, .85 * skullA, { layer: Front + .009, upTo: g.skel });
    glowLine(`${key} jaw`, [S(-.3, 2.62 + bob), S(-.2, 2.36 + bob), S(.2, 2.36 + bob), S(.3, 2.62 + bob)], .045 * kx, bone, .7 * skullA, { layer: Front + .009, upTo: g.skel });
    for (const side of [-1, 1]) blob(S(side * .16, 2.78 + bob), .1 * kx, .08 * kz, Mouth.withAlpha(.6 * skullA), Front + .0095);
    const e = S(-.16, 2.78 + bob);
    sprite(e, .3 * g.oneEye, .3 * g.oneEye, Eye.withAlpha(.6 * skullA * g.oneEye), glow, Front + .0097);
    blob(e, .045 * kx, .03 * kz, EyeHot.withAlpha(skullA * g.oneEye), Front + .0098, 0, add);
  }
  if (headK <= 0) return;
  const P = (u, v) => S(u, v + bob), hA = A * headK;
  const tri = (k, a, b, t, colour, layer) => { const m = mesh(k); m.setFlat([a.x, a.z, b.x, b.z, t.x, t.z], [0, 1, 2]); draw(m, 0, layer, 0, 1, 1, 0, colour); };
  // Hood, from the shoulders up round the head, rising to a point into the flames.
  const hood = HoodEdge.map(([v, h]) => [v, h * lerp(.75, 1, headK)]);
  const hl = hood.map(([v, h]) => P(-h, v)), hr = hood.map(([v, h]) => P(h, v));
  fillStrip(`${key} hood`, hl, hr, deep, .9 * fillA * headK, Front + .009);
  glowLine(`${key} hood line`, [...hr, ...hl.slice().reverse()], .024 * kx, line, .8 * lineA, { layer: Front + .0092, upTo: headK });
  for (const side of [-1, 1]) glowLine(`${key} hood fold ${side}`, [P(side * .56, 2.3), P(side * .52, 2.7), P(side * .44, 3.05), P(side * .3, 3.3)], .016 * kx, line, .4 * lineA * headK, { layer: Front + .0092, taper: 1 });
  // The mask face inside the hood.
  const ring = (cu, cv, ru, rv, n = 32) => { const pts = []; for (let j = 0; j <= n; j++) { const a = j / n * TAU; pts.push(P(cu + Math.cos(a) * ru, cv + Math.sin(a) * rv)); } return pts; };
  fillBlob(P(FaceC.u, FaceC.v), FaceR.u * kx, FaceR.v * kz, lit, .85 * fillA * headK, Front + .0102);
  glowLine(`${key} face line`, ring(FaceC.u, FaceC.v, FaceR.u, FaceR.v), .022 * kx, line, .75 * lineA, { layer: Front + .0103, upTo: headK });
  // Ear on the far side (screen right) with a ring earring hanging from it.
  fillBlob(P(.36, 2.86), .07 * kx, .12 * kz, lit, .85 * fillA * headK, Front + .0101);
  glowLine(`${key} ear`, [P(.33, 2.76), P(.40, 2.8), P(.43, 2.9), P(.39, 2.97), P(.34, 2.93), P(.37, 2.86)], .018 * kx, line, .75 * lineA * headK, { layer: Front + .0104 });
  const er = P(.4, 2.64), bead = P(.39, 2.74);
  draw(ringMesh, er.x, Front + .0104, er.z, .07 * kx, .07 * kz, 0, line.withAlpha(.9 * lineA * headK), add);
  draw(ringMesh, bead.x, Front + .0104, bead.z, .025 * kx, .025 * kz, 0, line.withAlpha(.8 * lineA * headK), add);
  // Gem on the forehead, upper left: a glowing orange crystal with facet lines.
  const gc = P(.0, 3.17);
  sprite(gc, .55 * kx, .55 * kz, FlameMid.withAlpha(.6 * hA), glow, Front + .0104);
  const gem = [];
  for (let j = 0; j <= 6; j++) { const a = j / 6 * TAU + Math.PI / 6; gem.push(P(Math.cos(a) * .1, 3.17 + Math.sin(a) * .11)); }
  const gm = mesh(`${key} gem`), gv = [gc.x, gc.z], gt = [];
  gem.slice(0, 6).forEach((q, j) => { gv.push(q.x, q.z); gt.push(0, 1 + j, 1 + (j + 1) % 6); });
  gm.setFlat(gv, gt);
  draw(gm, 0, Front + .0105, 0, 1, 1, 0, C(1, .5, .14).withAlpha(.55 * hA), add);
  sprite(gc, .12 * kx, .12 * kz, C(1, .85, .45).withAlpha(.7 * hA), glow, Front + .01052);
  glowLine(`${key} gem line`, gem, .012 * kx, line, .5 * lineA * headK, { layer: Front + .0106 });
  glowLine(`${key} gem facet`, [gem[1], gc, gem[4]], .009 * kx, line, .35 * lineA * headK, { layer: Front + .0106 });
  // The mask's eye: one small dark oval right of the nose root, a socket line over it.
  blob(P(.07, 2.95), .07 * kx, .05 * kz, Mouth.withAlpha(.95 * hA), Front + .0107);
  glowLine(`${key} socket`, [P(-.04, 2.96), P(.02, 3.02), P(.12, 3.02), P(.19, 2.96)], .016 * kx, line, .65 * lineA * headK, { layer: Front + .0108 });
  // Hood strands: lines sweeping round the head down to the jaw.
  [[[-.34, 3.36], [-.5, 3.1], [-.58, 2.75], [-.5, 2.35]], [[-.12, 3.44], [-.36, 3.25], [-.46, 2.95], [-.44, 2.6]], [[.3, 3.38], [.46, 3.15], [.52, 2.9]], [[.52, 2.6], [.5, 2.38], [.38, 2.25]]].forEach((pts, i) =>
    glowLine(`${key} strand ${i}`, pts.map(([u, v]) => P(u, v)), .015 * kx, line, .45 * lineA * headK, { layer: Front + .0103, taper: 1 }));
  // The mouth: a dark slot, a trapezoid wider at the top, under a red upper lip. Inside, the
  // Susanoo's own yellow eyes. Teeth are the body's red with pale outlines, as the anime draws
  // them: one fang down from the top edge between the eyes, a tall tusk up from the bottom right
  // corner past the top edge, a row of block teeth along the bottom lip.
  const slotT = [P(-.38, 2.64), P(.32, 2.65)], slotB = [P(-.32, 2.43), P(.28, 2.43)];
  fillStrip(`${key} upper lip`, [P(-.4, 2.64), P(.33, 2.64)], [P(-.38, 2.71), P(.3, 2.71)], lit, .9 * fillA * headK, Front + .0109);
  strip(`${key} mouth`, slotT, slotB, Mouth.withAlpha(.95 * hA), flat, Front + .011);
  glowLine(`${key} slot line`, [slotT[0], slotT[1], slotB[1], slotB[0], slotT[0]], .014 * kx, line, .45 * lineA * headK, { layer: Front + .0111 });
  if (g.eyes > 0) {
    [[-.19, 2.54, 10], [.1, 2.54, -10]].forEach(([u, v, rot], i) => {
      const e = P(u, v), pulse = .85 + .15 * Math.sin(s * 5 + i);
      sprite(e, .42 * kx, .24 * kz, EyeGlow.withAlpha(.7 * g.eyes * pulse * A), glow, Front + .0112);
      blob(e, .11 * kx, .045 * kz, Eye.withAlpha(g.eyes * A), Front + .0113, rot, add);
      blob(e, .05 * kx, .02 * kz, EyeHot.withAlpha(.8 * g.eyes * A), Front + .01135, rot, add);
      if (g.flash > 0) { const f = g.flash; sprite(e, 1.1 * f, .07, EyeHot.withAlpha(.9 * f * A), glow, Front + .0135); sprite(e, .07, .8 * f, EyeHot.withAlpha(.7 * f * A), glow, Front + .0135); }
    });
  }
  const redTooth = (k, pts) => {
    const m = mesh(k), v = [], t = [];
    pts.forEach(q => v.push(q.x, q.z)); for (let j = 1; j + 1 < pts.length; j++) t.push(0, j, j + 1);
    m.setFlat(v, t);
    draw(m, 0, Front + .0114, 0, 1, 1, 0, lit.withAlpha(.95 * hA));
    glowLine(k + ' line', [...pts, pts[0]], .012 * kx, line, .6 * lineA * headK, { layer: Front + .0115 });
  };
  redTooth(`${key} fang`, [P(-.1, 2.64), P(.01, 2.64), P(-.045, 2.48)]);
  // Lower lip: a red band under the slot, split into teeth by short lines, its top edge slightly
  // uneven where each tooth rises into the slot.
  const lipTop = [], lipBot = [];
  for (let j = 0; j <= 12; j++) { const f = j / 12, u = lerp(-.32, .28, f); lipTop.push(P(u, 2.43 + (j % 2 ? .025 : 0))); lipBot.push(P(u * .95, 2.33 - .02 * Math.sin(f * Math.PI))); }
  fillStrip(`${key} lower lip`, lipTop, lipBot, lit, .95 * fillA * headK, Front + .01142);
  glowLine(`${key} lower lip line`, lipBot, .014 * kx, line, .55 * lineA * headK, { layer: Front + .0115 });
  for (let j = 1; j < 6; j++) { const u = lerp(-.32, .28, j / 6); glowLine(`${key} tooth split ${j}`, [P(u, 2.35), P(u, 2.45)], .01 * kx, line, .5 * lineA * headK, { layer: Front + .0115 }); }
  redTooth(`${key} tusk`, [P(.17, 2.36), P(.28, 2.36), P(.25, 2.74)]);
  // Jaw line under the mouth.
  glowLine(`${key} jaw line`, [P(-.32, 2.36), P(-.05, 2.3), P(.26, 2.36)], .016 * kx, line, .5 * lineA * headK, { layer: Front + .0111, taper: 1 });
  // The tengu nose: a long curved horn from just above the mouth, left of the eye, pointing up
  // and to the left at about 45 degrees and ending past the hood's upper-left edge in the
  // flames (1.4 cells, about twice the mouth's width), its base 0.36 wide over the upper-left
  // of the face, its lower edge bowed out; a nostril mark at its base.
  const root = { u: -.08, v: 2.8 }, tip = { u: -1.0, v: 3.85 }, n = 16, spine = [];
  const dx = tip.u - root.u, dv = tip.v - root.v, L = Math.hypot(dx, dv), nx = dv / L, nv = -dx / L;   // normal toward lower left
  for (let j = 0; j <= n; j++) { const f = j / n, bow = .07 * Math.sin(f * Math.PI); spine.push({ u: lerp(root.u, tip.u, f) - nx * bow, v: lerp(root.v, tip.v, f) - nv * bow }); }
  // A horn: its base spreads over the upper-left of the face (about half the mouth's width).
  const wAt = j => lerp(.36, .025, Math.pow(j / n, .7));
  const lower = spine.map((q, j) => P(q.u - nx * wAt(j) / 2, q.v - nv * wAt(j) / 2)), upper = spine.map((q, j) => P(q.u + nx * wAt(j) / 2, q.v + nv * wAt(j) / 2));
  fillStrip(`${key} nose`, lower, upper, lit, fillA * headK, Front + .0116);
  glowLine(`${key} nose lower`, lower, .018 * kx, line, .85 * lineA * headK, { layer: Front + .0117 });
  glowLine(`${key} nose upper`, upper, .018 * kx, line, .85 * lineA * headK, { layer: Front + .0117 });
  glowLine(`${key} nostril`, [P(-.1, 2.76), P(-.05, 2.72), P(.01, 2.75)], .014 * kx, line, .65 * lineA * headK, { layer: Front + .0117 });
}

function arms(key, S, s, look, g, pose, c) {
  const { kx, kz } = look, A = g.alpha, { line, bone, lit, fillA, lineA, aK, bob } = c;
  const rootR = { u: -ArmRoot.u, v: ArmRoot.v + bob }, rootL = { u: ArmRoot.u, v: ArmRoot.v + bob };
  const R = elbowFor(rootR, { u: pose.hand.u, v: pose.hand.v + bob }, -1, pose.elbowDown ?? 0);
  const mirrorReach = Math.hypot(pose.mirror.u - rootL.u, pose.mirror.v + bob - rootL.v), stretch = Math.min(MaxStretch, Math.max(1, mirrorReach / (Upper + Fore - .02)));
  const L = elbowFor(rootL, { u: pose.mirror.u, v: pose.mirror.v + bob }, 1, 0, Upper * stretch, Fore * stretch);
  const P = q => S(q.u, q.v);

  // Skeletal right arm: grows from the shoulder to the elbow, then the forearm's two bones,
  // then the fingers closing round the gourd. It dims as the armour covers it.
  const sk = g.skel, bonesA = lerp(1, .35, c.boneDim ?? aK) * (c.boneAlpha ?? A);
  if (sk > 0) {
    const e = R.elbow, h = R.hand;
    glowLine(`${key} humerus`, [P(rootR), P(e)], .075 * kx, bone, .85 * bonesA, { layer: Front + .02, upTo: clamp(sk * 2.2) });
    for (const off of [-.035, .035]) {
      const dx = h.u - e.u, dv = h.v - e.v, L0 = Math.hypot(dx, dv) || 1, nx = -dv / L0 * off, nv = dx / L0 * off;
      glowLine(`${key} forearm ${off}`, [S(e.u + nx, e.v + nv), S(h.u + nx, h.v + nv)], .05 * kx, bone, .8 * bonesA, { layer: Front + .02, upTo: clamp(sk * 2.2 - 1.1) });
    }
    const fk = clamp(sk * 3 - 2);
    if (fk > 0) for (let i = 0; i < 4; i++) {
      const a0 = (-40 + i * 28) * Mathf.Deg2Rad, pts = [];
      for (let j = 0; j <= 4; j++) { const a = a0 + j / 4 * 1.6 * fk; pts.push(S(h.u + Math.cos(a) * .16 - .05, h.v + Math.sin(a) * .16 * (1 - .1 * i))); }
      glowLine(`${key} finger ${i}`, pts, .035 * kx, bone, .75 * bonesA * fk, { layer: Front + .021 });
    }
  }

  // Armoured arms: the right with the armour, the left with the mirror.
  const armArm = (tag, root, j, k, side, stopAt = null) => {
    if (k <= 0) return;
    const e = j.elbow, h = j.hand;
    let reach = k;
    if (stopAt) {
      // The forearm goes behind the mirror: end it just inside the rim instead of drawing its
      // outline across the face.
      const L1 = Math.hypot(e.u - root.u, e.v - root.v), L2 = Math.hypot(h.u - e.u, h.v - e.v);
      let f = 1; for (let i = 0; i <= 20; i++) { const t = i / 20; if (Math.hypot(lerp(e.u, h.u, t) - h.u, lerp(e.v, h.v, t) - h.v) < stopAt) { f = t; break; } }
      reach = Math.min(k, (L1 + L2 * f) / (L1 + L2));
    }
    // The arm as a tapered shape: upper arm .50 at the shoulder with a bulge, a round elbow,
    // forearm .42 swelling a little then .30 at the wrist.
    const N = 10, cl = [], widths = [];
    // Upper arm: .48 at the shoulder, a biceps bulge, .36 at the elbow. Forearm: swells to about
    // .46 a third of the way down, then narrows to a .24 wrist.
    for (let i = 0; i <= N; i++) { const t = i / N; cl.push({ u: lerp(root.u, e.u, t), v: lerp(root.v, e.v, t) }); widths.push(lerp(.48, .36, t) + .1 * Math.sin(Math.pow(t, .8) * Math.PI)); }
    for (let i = 1; i <= N; i++) { const t = i / N; cl.push({ u: lerp(e.u, h.u, t), v: lerp(e.v, h.v, t) }); widths.push(lerp(.38, .24, t) + .1 * Math.sin(Math.min(1, t * 1.5) * Math.PI) * (1 - t * .5)); }
    const keep = Math.max(1, Math.floor(reach * (cl.length - 1)));
    const pts = cl.slice(0, keep + 1).map(P), n = pts.length;
    const a = [], b = [];
    pts.forEach((p, i) => {
      const q0 = pts[Math.max(0, i - 1)], q1 = pts[Math.min(n - 1, i + 1)], dx = q1.x - q0.x, dz = q1.z - q0.z, Ln = Math.hypot(dx, dz) || 1;
      const w = widths[i] / 2 * kx;
      a.push({ x: p.x - dz / Ln * w, z: p.z + dx / Ln * w }); b.push({ x: p.x + dz / Ln * w, z: p.z - dx / Ln * w });
    });
    fillStrip(`${key} arm ${tag}`, a, b, lit, .8 * fillA * k, Front + .022);
    if (keep > N) fillBlob(P(e), .21 * kx, .19 * kz, lit, .5 * fillA * k, Front + .0221);
    glowLine(`${key} arm ${tag} a`, a, .02 * kx, line, .75 * lineA * k, { layer: Front + .023 });
    glowLine(`${key} arm ${tag} b`, b, .02 * kx, line, .75 * lineA * k, { layer: Front + .023 });
    // Bracer lines across the forearm and a flame curl on the outside of each segment.
    if (k > .7) {
      const q = clamp((k - .7) / .3);
      for (let i = 1; i <= 2; i++) {
        const f = .35 + i * .22, c0 = { u: lerp(e.u, h.u, f), v: lerp(e.v, h.v, f) }, dx = h.u - e.u, dv = h.v - e.v, L0 = Math.hypot(dx, dv) || 1;
        const nx = -dv / L0 * .15, nv = dx / L0 * .15;
        glowLine(`${key} bracer ${tag} ${i}`, [S(c0.u - nx, c0.v - nv), S(c0.u + nx, c0.v + nv)], .018 * kx, line, .5 * lineA * q, { layer: Front + .023 });
      }
      [[root, e], [e, h]].forEach(([p0, p1], i) => {
        const m = { u: (p0.u + p1.u) / 2, v: (p0.v + p1.v) / 2 }, ang = Math.atan2(p1.v - p0.v, p1.u - p0.u) * 180 / Math.PI;
        sprite(S(m.u + side * .12, m.v), .34 * kx, .34 * kz, line.withAlpha(.5 * lineA * q), curlMat, Front + .0235, -ang + 90 + (side < 0 ? 180 : 0));
      });
    }
  };
  armArm('r', rootR, R, clamp(aK * 1.4), -1);
  armArm('l', rootL, L, clamp(g.mirror * 1.6), 1, MirrorR * .8);

  // Gourd in the right hand, pointing along the blade, and the Totsuka pouring out of it.
  const bladeDeg = pose.bladeDeg, dir = { u: Math.cos(bladeDeg * Mathf.Deg2Rad), v: Math.sin(bladeDeg * Mathf.Deg2Rad) };
  const gk = clamp(g.blade * 2.5);
  if (gk > 0) {
    const h = R.hand, gA = gk * A;
    // The gourd as one shape along the blade's direction, held like a hilt: its big bulb in the
    // fist (a rounded bottom shows below it), the waist, a small bulb and the neck above; one
    // fill and one outline.
    const G = (along, across) => S(h.u + dir.u * along - dir.v * across, h.v + dir.v * along + dir.u * across);
    const prof = [[-.2, 0], [-.18, .08], [-.12, .13], [0, .14], [.08, .1], [.12, .07], [.17, .09], [.22, .09], [.27, .06], [.31, .045], [.34, .05]];
    const gl = prof.map(([a, w]) => G(a, w * gk)), gr = prof.map(([a, w]) => G(a, -w * gk));
    fillStrip(`${key} gourd`, gl, gr, lit, .8 * fillA * gk + .1 * gA, Front + .024);
    glowLine(`${key} gourd line`, [...gl, ...gr.slice().reverse(), gl[0]], .016 * kx, line, .75 * lineA * gk, { layer: Front + .0244 });
    glowLine(`${key} gourd cord`, [G(.12, .07), G(.02, .2), G(-.1, .24)], .012 * kx, line, .5 * lineA * gk, { layer: Front + .0248, taper: 1 });
    // The armoured fist round the gourd's waist, wider than the wrist: a rounded block across the
    // grip, four finger lines across the knuckles facing us, the thumb curled over the top.
    const F = (along, across) => G(along, across);
    const fist = [];
    for (let j = 0; j <= 20; j++) { const a0 = j / 20 * TAU; fist.push(F(Math.sin(a0) * .12, Math.cos(a0) * .19)); }
    fillBlob(S(h.u, h.v), .19 * kx * gk, .13 * kz * gk, lit, .95 * fillA * gk, Front + .0246, -(bladeDeg - 90));
    glowLine(`${key} fist line`, fist, .016 * kx, line, .75 * lineA * gk, { layer: Front + .0247 });
    for (let i = 0; i < 3; i++) {
      const w0 = -.19 + (i + 1) * .095;
      glowLine(`${key} finger ${i}`, [F(-.1, w0), F(.06, w0)], .012 * kx, line, .5 * lineA * gk, { layer: Front + .0247 });
    }
    glowLine(`${key} thumb`, [F(.04, .17), F(.12, .1), F(.12, -.02), F(.07, -.08)], .016 * kx, line, .7 * lineA * gk, { layer: Front + .0247 });
  }
  const bk = g.blade;
  if (bk > 0) {
    const h = R.hand, mouth = S(h.u + dir.u * .33, h.v + dir.v * .33);
    let tip;
    if (pose.bladeTip) tip = pose.bladeTip;
    else { const L0 = (pose.bladeLen ?? look.bladeLen) * smooth(bk); tip = { x: mouth.x + dir.u * L0 * kx, z: mouth.z + dir.v * L0 * kz }; }
    totsuka(`${key} blade`, mouth, tip, s, A * clamp(bk * 3), pose.stab ?? 0);
  }

  // The Yata Mirror on the left hand: its ring draws round, then the fill and face come in.
  const mk = g.mirror;
  if (mk > 0) {
    const c0 = S(pose.mirror.u, pose.mirror.v + bob), rx = MirrorR * kx, rz = MirrorR * kz, fk = smooth(clamp(mk * 1.4 - .4));
    const ring = [];
    for (let j = 0; j <= 48; j++) { const a = Math.PI / 2 - j / 48 * TAU; ring.push({ x: c0.x + Math.cos(a) * rx, z: c0.z + Math.sin(a) * rz }); }
    // A darker disc under the face so the forearm behind the shield does not show through it.
    blob(c0, rx * .98, rz * .98, Mouth.withAlpha(.35 * fk * A), Front + .0299);
    fillBlob(c0, rx, rz, MirrorRed, .75 * fillA * fk, Front + .03);
    sprite(c0, rx * 2.5, rz * 2.5, FlameDeep.withAlpha(.14 * fk * A), glow, Front + .0295);
    sprite(c0, rx * 2.3, rz * 2.3, line.withAlpha(.85 * lineA * fk), mirrorMat, Front + .031, 0);
    const hitFlash = pose.mirrorFlash || 0;
    glowLine(`${key} mirror ring`, ring, .04 * kx, Color.Lerp(line, EyeHot, hitFlash), Math.min(1.4, .9 * lineA + .6 * hitFlash), { layer: Front + .032, upTo: clamp(mk * 1.6) });
    if (hitFlash > 0) {
      sprite(c0, rx * 2.7, rz * 2.7, FlameMid.withAlpha(.35 * hitFlash * A), glow, Front + .0335);
      sprite(c0, rx * 2.3, rz * 2.3, LineWarm.withAlpha(.5 * hitFlash * A), mirrorMat, Front + .0336, 0);
    }
    // Hits land on the mirror as rings rippling across its face (the Block scenario's).
    (pose.mirrorHit || []).forEach((hit, i) => ripple(`${key} ripple ${i}`, c0, rx, rz, hit.age, hit.at, A));
  }
}

// Concentric rings spreading over the mirror from where a hit lands (`at`, {u, v} on the unit
// face), as the anime's Yata Mirror ripples: four rings, 0.07 s apart, growing to span the whole
// face in 0.5 s; the parts outside the face are left out. A flash marks the point. age in s.
export function ripple(key, c0, rx, rz, age, at, A) {
  if (age < 0 || age > .75) return;
  const p = { x: c0.x + at.u * rx, z: c0.z + at.v * rz };
  const f = Math.max(0, 1 - age / .15);
  sprite(p, .25 + .6 * f, .25 + .6 * f, EyeHot.withAlpha(.95 * f * A), glow, Front + .034);
  for (let k = 0; k < 4; k++) {
    const t = age - k * .07; if (t < 0 || t > .5) continue;
    const grow = 1 - Math.pow(1 - t / .5, 2), r = .08 + grow * 2.0, a = (1 - t / .5) * (1 - k * .15);
    let run = [], part = 0;
    const flush = () => { if (run.length > 1) stroke(`${key} ${k} ${part++}`, run, .05, LineWarm.withAlpha(a * A), { layer: Front + .033 }); run = []; };
    for (let j = 0; j <= 64; j++) {
      const ang = j / 64 * TAU, x = p.x + Math.cos(ang) * r * rx, z = p.z + Math.sin(ang) * r * rz;
      const d = Math.hypot((x - c0.x) / rx, (z - c0.z) / rz);
      if (d < .96) run.push({ x, z }); else flush();
    }
    flush();
  }
}

// The Totsuka Blade: a flowing flame stream from the gourd's mouth to `tip` (world cells), in
// nested additive strips (edge, body, core) with a wave running along it, bright dashes flowing
// out and dark streaks, and sparks off the tip. stab 0..1 thins and straightens it for the thrust.
export function totsuka(key, mouth, tip, s, alpha, stab = 0) {
  if (alpha <= .002) return;
  const dx = tip.x - mouth.x, dz = tip.z - mouth.z, L = Math.hypot(dx, dz);
  if (L < .02) return;
  const ux = dx / L, uz = dz / L, nx = -uz, nz = ux, n = 28;
  const wave = lerp(.07, .025, stab), wide = lerp(1, .7, stab);
  const spine = [];
  for (let i = 0; i <= n; i++) {
    const f = i / n, off = wave * Math.sin(f * 7 - s * 11) * Math.sin(f * Math.PI) + .03 * Math.sin(f * 17 - s * 19) * f;
    spine.push({ x: mouth.x + ux * L * f + nx * off, z: mouth.z + uz * L * f + nz * off, f });
  }
  const widthAt = f => wide * (.1 + .16 * Math.sin(Math.min(1, f * 1.8) * Math.PI / 2)) * (1 - Math.pow(f, 3)) + .015;
  const band = (tag, share, colour, a, layer) => {
    const A = [], B = [];
    spine.forEach((p, i) => {
      const w = widthAt(p.f) * share * (1 + .12 * Math.sin(p.f * 9 - s * 14 + share * 3));
      A.push({ x: p.x + nx * w, z: p.z + nz * w }); B.push({ x: p.x - nx * w, z: p.z - nz * w });
    });
    strip(`${key} ${tag}`, A, B, colour.withAlpha(a * alpha), add, layer);
  };
  sprite({ x: (mouth.x + tip.x) / 2, z: (mouth.z + tip.z) / 2 }, L * 1.1 + .4, .55, FlameDeep.withAlpha(.12 * alpha), glow, Front + .0248, -Math.atan2(dz, dx) * 180 / Math.PI);
  band('edge', 1.25, BladeEdge, .6, Front + .025);
  band('body', .85, BladeBody, .6, Front + .0252);
  band('core', .38, BladeCore, .75, Front + .0254);
  band('hot', .14, BladeHot, .6, Front + .0256);
  // Dark streaks and bright dashes running out along the stream.
  for (let i = 0; i < 7; i++) {
    const f0 = Mathf.Repeat(s * (1.1 + .4 * rand(i + 500)) + rand(i + 510), 1), f1 = Math.min(1, f0 + .12);
    const side = (rand(i + 520) - .5) * 1.2, pts = [];
    for (let j = 0; j <= 4; j++) {
      const f = lerp(f0, f1, j / 4), k = Math.min(n, Math.round(f * n)), p = spine[k], w = widthAt(f) * side;
      pts.push({ x: p.x + nx * w, z: p.z + nz * w });
    }
    const c = i % 3 === 0 ? Mouth : BladeHot, a = i % 3 === 0 ? .25 : .5;
    stroke(`${key} dash ${i}`, pts, .025, c.withAlpha(a * alpha * Math.sin(f0 * Math.PI)), { material: i % 3 === 0 ? flat : add, layer: Front + .0258, taper: 1 });
  }
  // Sparks and flame licks off the tip.
  for (let i = 0; i < 6; i++) {
    const age = Mathf.Repeat(s * 1.8 + rand(i + 530), 1), a0 = Math.atan2(uz, ux) + (rand(i + 540) - .5) * 1.6;
    const d = .08 + age * .35;
    sprite({ x: tip.x + Math.cos(a0) * d, z: tip.z + Math.sin(a0) * d + age * .1 }, .09 * (1 - age) + .02, .09 * (1 - age) + .02, BladeCore.withAlpha(.7 * (1 - age) * alpha), glow, Front + .026);
  }
  sprite(tip, .35, .35, BladeCore.withAlpha(.35 * alpha), glow, Front + .0259);
}

// ---------------------------------------------------------------- Itachi and stand-ins

// Itachi at the real pawn's size: the Akatsuki cloak (black, red clouds with pale edges, high
// collar), dark hair, Sharingan when `eyes` > 0. hunch 0..1 bends him forward (the cough).
export function itachi(pos, sun, strength, { alpha = 1, eyes = 0, hunch = 0 } = {}) {
  const s = 1.3, o = -.33, L = pawnLayer, dz = -hunch * .17;
  sprite({ x: pos.x + sun.x * .5, z: pos.z + o + .05 + sun.z * .5 }, .95, .42, C(.03, .03, .04).withAlpha(strength * alpha * 1.4), soft, shadowLayer);
  blob({ x: pos.x, z: pos.z + o + (.18 - .03 * hunch) * s }, .22 * s, (.32 - .06 * hunch) * s, Cloak.withAlpha(alpha), L);
  // The cloud marks sit on the body and move down with it when he bows.
  [[-.1, .1, .07], [.09, .26, .06], [-.06, .36, .05]].forEach(([x, z, r], i) => {
    const cz = pos.z + o + z * (1 - .22 * hunch) * s;
    blob({ x: pos.x + x * s, z: cz }, (r + .012) * s, (r * .62 + .012) * s, CloudEdge.withAlpha(alpha), L + .001 + i * .0003);
    blob({ x: pos.x + x * s, z: cz }, r * s, r * .62 * s, Cloud.withAlpha(alpha), L + .0011 + i * .0003);
  });
  blob({ x: pos.x, z: pos.z + o + .47 * s + dz }, .2 * s, .07 * s, Cloak.withAlpha(alpha), L + .002);
  const head = { x: pos.x, z: pos.z + o + .58 * s + dz * 1.5 };
  blob(head, .16 * s, .17 * s, Skin.withAlpha(alpha), L + .003);
  blob({ x: head.x, z: head.z + .08 * s }, .165 * s, .10 * s, Hair.withAlpha(alpha), L + .004);
  for (const side of [-1, 1]) blob({ x: head.x + side * .13 * s, z: head.z - .02 * s }, .04 * s, .11 * s, Hair.withAlpha(alpha), L + .004);
  blob({ x: head.x, z: head.z - .16 * s }, .09 * s, .05 * s, Hair.withAlpha(alpha), L + .0045);   // ponytail peeking below
  if (eyes > 0) for (const side of [-1, 1]) {
    const e = { x: head.x + side * .055 * s, z: head.z - .01 * s };
    blob(e, .022 * s, .016 * s, Sharingan.withAlpha(alpha * eyes), L + .005);
    sprite(e, .12 * eyes, .12 * eyes, Sharingan.withAlpha(.5 * eyes * alpha), glow, L + .006);
  }
  return { head, chest: { x: pos.x, z: pos.z + o + .3 * s } };
}

// A raider at the real pawn's size: tan body, grey helmet. weapon: 'rifle' held toward aimDeg
// (screen degrees), or 'sword' at weaponDeg with weaponLen cells from the hand. Returns the chest,
// hand and weapon tip in world cells.
export const Enemy = C(.55, .38, .27), Helmet = C(.30, .33, .33), Steel = C(.72, .74, .78), Gun = C(.14, .13, .13);
export function raider(pos, sun, strength, { alpha = 1, weapon = 'rifle', aimDeg = 0, weaponDeg = 90, weaponLen = .6, lean = 0, downed = false } = {}) {
  const s = 1.3, o = -.33, L = pawnLayer;
  sprite({ x: pos.x + sun.x * .5, z: pos.z + o + .05 + sun.z * .5 }, .95, .42, C(.03, .03, .04).withAlpha(strength * alpha * 1.4), soft, shadowLayer);
  const chest = { x: pos.x + lean * .08, z: pos.z + o + .3 * s };
  blob({ x: pos.x + lean * .05, z: pos.z + o + .18 * s }, .22 * s, .32 * s, Enemy.withAlpha(alpha), L);
  const head = { x: pos.x + lean * .1, z: pos.z + o + .58 * s };
  blob(head, .16 * s, .17 * s, Skin.withAlpha(alpha), L + .003);
  blob({ x: head.x, z: head.z + .08 * s }, .17 * s, .1 * s, Helmet.withAlpha(alpha), L + .004);
  blob({ x: head.x, z: head.z + .02 * s }, .175 * s, .035 * s, Helmet.withAlpha(alpha), L + .004);
  const deg = weapon === 'rifle' ? aimDeg : weaponDeg, a = deg * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a);
  const hand = { x: chest.x + ca * .12, z: chest.z + sa * .08 };
  if (weapon === 'rifle') {
    const back = { x: hand.x - ca * .12, z: hand.z - sa * .12 }, muzzle = { x: hand.x + ca * .5, z: hand.z + sa * .5 };
    stroke('raider rifle ' + pos.x.toFixed(2) + pos.z.toFixed(2), [back, muzzle], .065, Gun.withAlpha(alpha), { material: flat, layer: L + .006 });
    return { chest, hand, tip: muzzle, head };
  }
  if (weapon === 'none') return { chest, hand, tip: hand, head };
  const tip = { x: hand.x + ca * weaponLen, z: hand.z + sa * weaponLen };
  stroke('raider sword ' + pos.x.toFixed(2) + pos.z.toFixed(2), [hand, tip], .05, Steel.withAlpha(alpha), { material: flat, layer: L + .006 });
  stroke('raider hilt ' + pos.x.toFixed(2) + pos.z.toFixed(2), [{ x: hand.x - ca * .1, z: hand.z - sa * .1 }, hand], .06, Gun.withAlpha(alpha), { material: flat, layer: L + .0061 });
  blob(hand, .05, .05, Skin.withAlpha(alpha), L + .0062);
  return { chest, hand, tip, head };
}

// A round in flight: a short bright streak ending at `at`, pointing along `dir` (unit, world).
export function round(key, at, dir, alpha = 1) {
  if (alpha <= .002) return;
  stroke(key + ' glow', [{ x: at.x - dir.x * .7, z: at.z - dir.z * .7 }, at], .16, C(1, .7, .3).withAlpha(.35 * alpha), { taper: .6, layer: Y + .0495 });
  stroke(key, [{ x: at.x - dir.x * .7, z: at.z - dir.z * .7 }, at], .06, C(1, .92, .62).withAlpha(alpha), { taper: .6, layer: Y + .05 });
  sprite(at, .28, .28, C(1, .85, .5).withAlpha(.6 * alpha), glow, Y + .051);
}

// Sparks thrown off a blocked hit at `at`, back toward `from` (unit direction, world), age in s.
export function sparks(key, at, back, age, n = 8, life = .35, seed = 0) {
  if (age < 0 || age > life) return;
  for (let i = 0; i < n; i++) {
    const k = seed * 31 + i, spread = (rand(k + 600) - .5) * 2.2, a = Math.atan2(back.z, back.x) + spread;
    const v = 1.8 + 2.2 * rand(k + 610), t = age, u = t / life;
    const p = { x: at.x + Math.cos(a) * v * t, z: at.z + Math.sin(a) * v * t - 1.4 * t * t };
    const q = { x: p.x - Math.cos(a) * .08, z: p.z - Math.sin(a) * .08 + .04 };
    stroke(`${key} ${i}`, [q, p], .03, C(1, .86, .5).withAlpha(1 - u), { taper: .5, layer: Y + .06 });
  }
  sprite(at, .5 * (1 - age / life) + .1, .5 * (1 - age / life) + .1, C(1, .9, .6).withAlpha(Math.max(0, 1 - age / .12)), glow, Y + .061);
}

// The sealed target on its way into the gourd: its silhouette, dark against the bright blade,
// rimmed with flame, stretching along the pull (dirDeg, toward the gourd) and shrinking as it
// goes, with flame licks trailing. stretch 0..1 (0 = where it stood, 1 = at the gourd).
export function pulled(key, pos, dirDeg, stretch, alpha, s) {
  if (alpha <= .002) return;
  const rot = -dirDeg, shrink = 1 - .7 * stretch, len = (.26 + stretch * .5) * shrink + .06, thin = .3 * shrink * (1 - stretch * .35);
  const a = dirDeg * Mathf.Deg2Rad, fwd = { x: Math.cos(a), z: Math.sin(a) };
  sprite(pos, len * 3.2, thin * 3.6, FlameDeep.withAlpha(.55 * alpha), glow, Front + .027, rot);
  draw(disc, pos.x, Front + .0272, pos.z, len, thin, rot, C(.16, .05, .04).withAlpha(.92 * alpha), flat);
  // The head at the front of the shape, going in first.
  const head = { x: pos.x + fwd.x * len * .75, z: pos.z + fwd.z * len * .75 };
  draw(disc, head.x, Front + .0273, head.z, .12 * shrink + .02, .12 * shrink + .02, 0, C(.2, .06, .05).withAlpha(.92 * alpha), flat);
  for (let i = 0; i < 11; i++) {
    const f = Mathf.Repeat(s * 3.5 + i / 11, 1), off = (rand(i + 700) - .5) * thin * 2.2;
    const p0 = { x: pos.x - fwd.x * (len * .7 + f * .45) - fwd.z * off, z: pos.z - fwd.z * (len * .7 + f * .45) + fwd.x * off };
    const p1 = { x: p0.x - fwd.x * .3, z: p0.z - fwd.z * .3 };
    stroke(`${key} lick ${i}`, [p0, p1], .12 * (1 - f) * shrink + .03, (i % 3 ? FlameMid : BladeCore).withAlpha(.85 * (1 - f) * alpha), { taper: 1, layer: Front + .0274 });
  }
}

// Flames wrapping a pawn (the moment the Totsuka pierces it): a ring of flame tips round its
// body, rising, over a red glow. amount 0..1.
export function flameWrap(key, pos, s, amount) {
  if (amount <= .002) return;
  sprite({ x: pos.x, z: pos.z + .1 }, 1.1, 1.3, FlameDeep.withAlpha(.32 * amount), glow, Front + .0266);
  const ring = [];
  for (let j = 0; j <= 28; j++) { const a = -j / 28 * TAU; ring.push({ x: pos.x + Math.cos(a) * .34, z: pos.z + .1 + Math.sin(a) * .48 }); }
  flameEdge(key, ring, s, .5 * amount, 1.3 * amount, { count: 16, seed: 11, layer: Front + .0268, up: 1.4 });
}

// The seal at the gourd's mouth: a flash, a ring closing in and a swirl turning shut. age in s.
export function sealFlash(pos, age, A = 1) {
  if (age < 0 || age > .5) return;
  const u = age / .5, f = Math.max(0, 1 - age / .14);
  sprite(pos, 1.4 * f + .3, 1.4 * f + .3, EyeHot.withAlpha(.95 * f * A), glow, Front + .036);
  const r = .7 * (1 - u) + .08;
  draw(ringMesh, pos.x, Front + .0361, pos.z, r, r, 0, LineWarm.withAlpha((1 - u) * A), add);
  sprite(pos, .9 * (1 - u) + .2, .9 * (1 - u) + .2, LineWarm.withAlpha((1 - u) * A), swirlMat, Front + .0362, age * 900);
}

// Gear dropped where a pawn stood: a rifle lying at `deg`, a helmet. drop 0..1 is the fall
// (from hand or head height to the floor, with a small bounce); they stay afterward.
export function droppedRifle(key, pos, deg, drop, fromH = .45) {
  const a = deg * Mathf.Deg2Rad, h = fromH * (1 - drop) * (1 - drop) + .06 * Math.abs(Math.sin(drop * Math.PI * 2)) * (drop > .5 ? 1 : 0);
  const c = { x: pos.x, z: pos.z + h }, ca = Math.cos(a) * .32, sa = Math.sin(a) * .32;
  if (drop >= 1) sprite({ x: pos.x + .04, z: pos.z - .05 }, .75, .22, C(.03, .03, .04).withAlpha(.3), soft, shadowLayer);
  stroke(key, [{ x: c.x - ca, z: c.z - sa }, { x: c.x + ca, z: c.z + sa }], .07, Gun, { material: flat, layer: AltitudeLayer.Item.AltitudeFor() });
}
export function droppedHelmet(pos, drop, fromH = .75) {
  const h = fromH * (1 - drop) * (1 - drop) + .05 * Math.abs(Math.sin(drop * Math.PI * 2)) * (drop > .5 ? 1 : 0);
  blob({ x: pos.x, z: pos.z + h }, .2, .13, Helmet, AltitudeLayer.Item.AltitudeFor() + .001);
  blob({ x: pos.x, z: pos.z + h + .03 }, .15, .07, C(.42, .45, .45), AltitudeLayer.Item.AltitudeFor() + .002);
}

// A thin ring on the floor at the true radius.
export function floorRing(pos, radius, colour, alpha) {
  if (alpha <= .002) return;
  draw(ringMesh, pos.x, Floor + .02, pos.z, radius, radius, 0, colour.withAlpha(alpha), add);
}

// A star glint (core and four rays).
export function glint(pos, size, alpha, colour = EyeHot, layer = Y + .2) {
  if (alpha <= .002) return;
  sprite(pos, size * .5, size * .5, colour.withAlpha(alpha), glow, layer);
  sprite(pos, size * 1.6, size * .09, colour.withAlpha(alpha * .9), glow, layer + .001);
  sprite(pos, size * .09, size * 1.6, colour.withAlpha(alpha * .9), glow, layer + .001);
}
