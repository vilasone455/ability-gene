// Shared drawing for the Obito kit: Kamui: Phase, Kamui: Warp, Kamui: Store, Wood Release.
//
//   picture()   a pawn or an item drawn as its picture on a 20 x 20 grid mesh, so a warp can bend
//               it. In game the picture is the real pawn: PortraitsCache.Get renders it to a
//               RenderTexture, the pawn itself is not drawn while it is bent, and the grid is
//               rebuilt each frame while a warp runs (441 vertices). The lab pictures are stand-ins
//               at the size of the lab's two-disc pawns (lab/obito-pic-*).
//   vortex()    the Kamui warp. Every point within reach turns clockwise round a centre and is
//               pulled into it; nearer points go first, so the subject winds into a spiral that
//               ends in the point. k 0 is untouched, 1 is gone. Run backwards it unwinds a subject
//               out of the point.
//   swirl()     the spiral of space at that centre: dark arms with a pale leading line, a dark core
//               and a faint haze. The colours are the Kamui dimension's void and edge line
//               (lib/kamui.js), so the hole matches the place it leads to.
//   eye()       Obito's right eye, the centre of every Kamui swirl of his (the source's Kamui is
//               the right eye). glint() is the red flash of the Mangekyō in the mask's eye hole.
//   branch()    one Wood Release branch: bark strip with a lit side, grain line, pale pointed tip
//               and a ground shadow.
//
// Level shapes only: a swirl is a flat spiral on the screen and a branch lies at one height, so
// nothing here needs a per-facing draw method. The mask picture has four facings (west is the
// east picture mirrored, as RimWorld draws it).
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, ShaderDatabase } from '../../js/engine.js';
import { registerLabTexture, pixels, hash } from '../../js/standins.js';
import { draw, mesh } from './six-paths-solid.js';
import { Lift, P, sprite, trail, band, circle, soft, glow, rand } from './six-paths-impact.js';

export { Lift, P, draw, mesh, sprite, trail, band, circle, soft, glow, rand, hash };
export const smooth = t => Mathf.Smooth(Math.min(1, Math.max(0, t)));
export const clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
export const disc = Meshes.disc(40, 'obito disc');
const ringMesh = Meshes.band(.84, 1, 48, 'obito ring');

const at = n => AltitudeLayer[n].AltitudeFor();
export const L = { floor: at('Filth') + .01, shadow: at('Shadows'), pawn: at('Pawn'), low: at('MoteOverheadLow'), fx: at('MoteOverhead') };

// ---- colours --------------------------------------------------------------------------------
const rgb = (r, g, b) => new Color(r / 255, g / 255, b / 255, 1);
export const Void = rgb(10, 16, 22), Edge = rgb(196, 222, 244);          // the Kamui dimension's
export const Sharingan = rgb(222, 30, 24), Mask = rgb(236, 128, 36), MaskLine = rgb(140, 60, 14);
export const Cloak = rgb(30, 27, 36), Hair = rgb(26, 22, 24);
export const Zetsu = rgb(232, 229, 218), ZetsuShade = rgb(176, 172, 160);
export const Bark = rgb(92, 64, 40), BarkLit = rgb(146, 108, 68), BarkDark = rgb(50, 34, 22), Heart = rgb(226, 202, 150);
export const Enemy = new Color(.55, .38, .27), Ally = new Color(.42, .62, .40), Skin = new Color(.83, .70, .54);
export const Blood = new Color(.45, .05, .05), Round = new Color(1, .93, .62), Stun = new Color(1, .88, .45);
export const White = new Color(1, 1, 1, 1), Black = new Color(0, 0, 0, 1);

// ---- pictures -------------------------------------------------------------------------------
// A picture covers Pic.size cells square, centred Pic.mid north of the pawn's cell centre. The
// shapes match the lab's two-disc stand-in: body ellipse .22 x .32 at +.18, head .16 x .17 at +.58.
export const Pic = { size: 1.5, mid: .3, grid: 20 };
const Head = { x: 0, z: .58, rx: .16, rz: .17 }, Torso = { x: 0, z: .18, rx: .22, rz: .32 };
// The right eye in head-local fractions of the head's radii. Facing south his right side is on
// the screen's left; facing east it is toward the camera; west is the east picture mirrored.
const EyeAt = { south: [-.36, .1], east: [.56, .1], west: [-.56, .1], north: [.34, .12] };
export function eye(pos, facing = 'south') {
  const e = EyeAt[facing];
  return { x: pos.x + Head.x + e[0] * Head.rx, z: pos.z + Head.z + e[1] * Head.rz };
}
export const chest = pos => ({ x: pos.x, z: pos.z + .3 });
export function facingOf(deg) {
  const a = ((deg % 360) + 360) % 360;
  return a < 45 || a >= 315 ? 'east' : a < 135 ? 'north' : a < 225 ? 'west' : 'south';
}

const PicN = 128;
const ellipse = (x, z, e, px) => {
  const d = Math.hypot((x - e.x) / e.rx, (z - e.z) / e.rz);
  return { a: clamp((1 - d) * Math.min(e.rx, e.rz) / px + .5), d };
};
// Paint layers back to front; each layer returns [colour, alpha] or null.
function paint(layers) {
  return () => pixels(PicN, (u, v) => {
    const x = (u - .5) * Pic.size, z = Pic.mid + (.5 - v) * Pic.size, px = Pic.size / PicN;
    let r = 0, g = 0, b = 0, a = 0;
    for (const layer of layers) {
      const hit = layer(x, z, px);
      if (!hit || hit[1] <= 0) continue;
      const [c, k] = hit, na = k + a * (1 - k);
      r = (c.r * k + r * a * (1 - k)) / na; g = (c.g * k + g * a * (1 - k)) / na; b = (c.b * k + b * a * (1 - k)) / na; a = na;
    }
    return [r, g, b, a];
  });
}
const shade = (c, d) => Color.Lerp(c, Black, .28 * d * d * d).withAlpha(1);
const torso = colour => (x, z, px) => { const e = ellipse(x, z, Torso, px); return [shade(colour, e.d), e.a]; };
// The cloak's high collar under the mask, a shade lighter so the head stands off the body.
const collar = (x, z, px) => {
  const e = ellipse(x, z, { x: 0, z: .44, rx: .2, rz: .1 }, px);
  return e.a > 0 ? [Color.Lerp(Cloak, White, .12 + .1 * (1 - e.d)).withAlpha(1), e.a * ellipse(x, z, Torso, px).a] : null;
};
function head(kind) {
  return (x, z, px) => {
    const e = ellipse(x, z, Head, px);
    if (e.a <= 0) return null;
    const hx = (x - Head.x) / Head.rx, hz = (z - Head.z) / Head.rz;
    if (kind === 'skin') return [shade(hz > .3 ? Hair : Skin, e.d), e.a];
    if (kind === 'north') return [shade(Hair, e.d), e.a];
    // The spiral mask: lines wind out of the eye hole across the whole mask.
    const facing = kind, ex = EyeAt[facing][0], ez = EyeAt[facing][1];
    if ((facing === 'east' && hx < -.25) || (facing === 'west' && hx > .25)) return [shade(Hair, e.d), e.a];
    const rho = Math.hypot(hx - ex, hz - ez), phi = Math.atan2(hz - ez, hx - ex);
    if (rho < .13) return [Sharingan, e.a];
    if (rho < .24) return [Black, e.a];
    const f = ((phi / TAU + rho * 1.7) % 1 + 1) % 1, line = Math.abs(f - .5) > .4;
    return [shade(line ? MaskLine : Mask, e.d), e.a];
  };
}
const crate = (x, z, px) => {
  const hx = Math.abs(x), hz = Math.abs(z - .1), s = .28;
  if (hx > s || hz > s) return null;
  const a = clamp((s - Math.max(hx, hz)) / px + .5), rim = Math.max(hx, hz) > s - .05;
  const slat = Math.abs(((z - .1 + s) / (2 * s) * 4) % 1 - .5) > .44;
  return [rim ? rgb(92, 62, 34) : slat ? rgb(112, 78, 44) : rgb(160, 118, 70), a];
};
const Pictures = {
  'obito-south': [torso(Cloak), collar, head('south')],
  'obito-east': [torso(Cloak), collar, head('east')],
  'obito-west': [torso(Cloak), collar, head('west')],
  'obito-north': [torso(Cloak), collar, head('north')],
  enemy: [torso(Enemy), head('skin')],
  ally: [torso(Ally), head('skin')],
  crate: [crate],
};
for (const [kind, layers] of Object.entries(Pictures)) registerLabTexture(`lab/obito-pic-${kind}`, paint(layers));
const picMats = {};
const picMat = kind => picMats[kind] ??= MaterialPool.MatFrom(`lab/obito-pic-${kind}`, ShaderDatabase.Transparent);

const gridUV = (() => {
  const n = Pic.grid, uv = [];
  for (let j = 0; j <= n; j++) for (let i = 0; i <= n; i++) uv.push(i / n, j / n);
  return new Float32Array(uv);
})();
const gridTri = (() => {
  const n = Pic.grid, t = [];
  for (let j = 0; j < n; j++) for (let i = 0; i < n; i++) {
    const a = j * (n + 1) + i, b = a + n + 1;
    t.push(a, b, b + 1, a, b + 1, a + 1);
  }
  return t;
})();

// The subject's picture, bent by warp (a function of a world point) when one is given.
export function picture(key, kind, pos, { alpha = 1, warp = null, layer = L.pawn, tint = White } = {}) {
  if (alpha <= .005) return;
  const n = Pic.grid, xz = [];
  for (let j = 0; j <= n; j++) for (let i = 0; i <= n; i++) {
    const x = pos.x + (i / n - .5) * Pic.size, z = pos.z + Pic.mid + (j / n - .5) * Pic.size;
    if (warp) { const w = warp(x, z); xz.push(w.x, w.z); } else xz.push(x, z);
  }
  const m = mesh(`${key} picture`);
  m.setFlat(xz, gridTri);
  if (m.uv !== gridUV) m.uv = gridUV;
  draw(m, 0, layer, 0, 1, 1, 0, tint.withAlpha(alpha * tint.a), picMat(kind));
}
export function shadow(pos, sun, strength, size = 1) {
  if (size <= .02) return;
  sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, .85 * size, .4 * size, Black.withAlpha(strength), soft, L.shadow);
}
export function figure(key, kind, pos, sun, strength, opts = {}) {
  shadow(pos, sun, strength * (opts.alpha ?? 1), opts.shadow ?? 1);
  picture(key, kind, pos, opts);
}
export const obitoKind = facing => `obito-${facing}`;

// ---- the Kamui warp -------------------------------------------------------------------------
// turns: how far the nearest points turn before they vanish. reach: cells round the centre that
// the warp takes. lead: how much sooner near points go than far ones.
export function vortex(c, k, { turns = 1.5, reach = 1.2, lead = .8 } = {}) {
  if (k <= 0) return null;
  return (x, z) => {
    const dx = x - c.x, dz = z - c.z, r = Math.hypot(dx, dz);
    const rho = Math.min(1, r / reach), q = clamp(k * (1 + lead) - lead * rho);
    if (q <= 0) return { x, z };
    const e = q * q * (3 - 2 * q);
    const a = Math.atan2(dz, dx) - turns * TAU * e * (1.25 - .5 * rho);
    const rr = r * Math.pow(1 - e, 1.4);
    return { x: c.x + Math.cos(a) * rr, z: c.z + Math.sin(a) * rr };
  };
}
// A small twist that does not pull in: for ripples where something passes through.
export function twist(c, amount, reach = .4) {
  if (Math.abs(amount) < .002) return null;
  return (x, z) => {
    const dx = x - c.x, dz = z - c.z, r = Math.hypot(dx, dz);
    if (r >= reach) return { x, z };
    const f = 1 - r / reach, a = Math.atan2(dz, dx) - amount * TAU * f * f, rr = r * (1 - .25 * Math.abs(amount) * f);
    return { x: c.x + Math.cos(a) * rr, z: c.z + Math.sin(a) * rr };
  };
}
export function compose(...fs) {
  const list = fs.filter(Boolean);
  if (!list.length) return null;
  return (x, z) => list.reduce((p, f) => f(p.x, p.z), { x, z });
}

// The spiral of space. spin is the arms' turn in radians (decrease it over time: clockwise).
export function swirl(key, c, radius, spin, alpha, { arms = 4, wind = .85, layer = L.fx, core = .13, haze = 1, lit = 1 } = {}) {
  if (alpha <= .01 || radius <= .01) return;
  if (haze > 0) sprite(c, radius * 2.9, radius * 2.9, Void.withAlpha(.14 * alpha * haze), soft, layer);
  const N = 18;
  for (let i = 0; i < arms; i++) {
    const dark = [], edge = [], phase = spin + i * TAU / arms;
    for (let j = 0; j <= N; j++) {
      const u = j / N, r = radius * (.08 + .92 * Math.pow(u, 1.15)), a = phase + wind * TAU * u;
      dark.push({ x: c.x + Math.cos(a) * r, z: c.z + Math.sin(a) * r });
      const al = a - .2 - .1 * u, rl = r * 1.03;
      edge.push({ x: c.x + Math.cos(al) * rl, z: c.z + Math.sin(al) * rl });
    }
    trail(`${key} swirl dark ${i}`, dark, radius * .16, Void.withAlpha(.42 * alpha), layer + .002);
    if (lit > 0) trail(`${key} swirl edge ${i}`, edge, Math.max(.018, radius * .06), Edge.withAlpha(.72 * alpha * lit), layer + .003);
  }
  const cr = Math.max(.02, radius * core);
  draw(disc, c.x, layer + .004, c.z, cr, cr, 0, Void.withAlpha(Math.min(1, alpha * 1.3)));
  draw(ringMesh, c.x, layer + .005, c.z, cr * 1.3, cr * 1.3, 0, Edge.withAlpha(.55 * alpha * lit));
}
// A pale ring pulled into a point as a swirl shuts (age 0 to .25 s).
export function shut(c, age, radius = .5, layer = L.fx) {
  if (age < 0 || age > .25) return;
  const k = age / .25, r = radius * (1 - smooth(k));
  if (r > .01) draw(ringMesh, c.x, layer + .006, c.z, r, r, 0, Edge.withAlpha(.7 * (1 - k * .5)));
  sprite(c, .4 * (1 - k) + .05, .4 * (1 - k) + .05, Void.withAlpha(.5 * (1 - k)), soft, layer + .001);
}
// The Mangekyō's red flash in the eye hole (age 0 to .4 s).
export function glint(pos, age, size = 1) {
  const Life = .4;
  if (age < 0 || age > Life) return;
  const k = age / Life, a = Math.min(1, age / .05) * (1 - smooth((k - .25) / .75));
  sprite(pos, .5 * size, .5 * size, Sharingan.withAlpha(.5 * a), glow, L.fx + .02);
  sprite(pos, .95 * size * (.55 + .45 * k), .05 * size, Sharingan.withAlpha(.85 * a), glow, L.fx + .021);
  sprite(pos, .05 * size, .55 * size * (.55 + .45 * k), Sharingan.withAlpha(.6 * a), glow, L.fx + .021);
  draw(disc, pos.x, L.fx + .022, pos.z, .03 * size, .03 * size, 0, new Color(1, .8, .75, a));
}

// ---- small pieces ---------------------------------------------------------------------------
export function rod(key, a, b, width, colour, layer) {
  const dx = b.x - a.x, dz = b.z - a.z, len = Math.hypot(dx, dz) || 1, nx = -dz / len * width / 2, nz = dx / len * width / 2;
  band(key, [{ x: a.x + nx, z: a.z + nz }, { x: b.x + nx, z: b.z + nz }], [{ x: a.x - nx, z: a.z - nz }, { x: b.x - nx, z: b.z - nz }], colour, layer);
}
// Obito's right arm (white Zetsu cells in the source) from shoulder to hand; bark covers it from
// the shoulder for `wood` of its length.
export function arm(key, shoulder, hand, layer, { wood = 0, alpha = 1 } = {}) {
  rod(`${key} arm edge`, shoulder, hand, .11, ZetsuShade.withAlpha(alpha), layer);
  rod(`${key} arm`, shoulder, hand, .08, Zetsu.withAlpha(alpha), layer + .001);
  draw(disc, hand.x, layer + .002, hand.z, .055, .055, 0, Zetsu.withAlpha(alpha));
  if (wood > 0) {
    const w = { x: lerp(shoulder.x, hand.x, wood), z: lerp(shoulder.z, hand.z, wood) };
    rod(`${key} arm bark`, shoulder, w, .12, Bark.withAlpha(alpha), layer + .003);
    rod(`${key} arm bark lit`, shoulder, w, .05, BarkLit.withAlpha(alpha), layer + .004);
    if (wood > .95) draw(disc, hand.x, layer + .005, hand.z, .07, .07, 0, Bark.withAlpha(alpha));
  }
}
export function stunMark(key, pos, age, s) {
  if (age < 0) return;
  const c = { x: pos.x, z: pos.z + .95 };
  for (let i = 0; i < 3; i++) {
    const a = s * 5 + i * TAU / 3;
    sprite({ x: c.x + Math.cos(a) * .2, z: c.z + Math.sin(a) * .07 }, .12, .12, Stun.withAlpha(.9), glow, L.fx + .03);
  }
}

// ---- Wood Release ---------------------------------------------------------------------------
// One branch through spine points on the screen (height already folded in). h is its height
// above the floor, for the shadow. Width w0 at the root, 55 % of it at the far end, then a point
// over the last tipLen cells; the last tipLen is pale fresh wood.
export function branch(key, pts, w0, { h = .55, sun = { x: -.45, z: -.32 }, strength = .3, alpha = 1, layer = L.fx, tipLen = .3, dry = 0 } = {}) {
  if (pts.length < 2 || alpha <= .01) return;
  const acc = [0];
  for (let i = 1; i < pts.length; i++) acc.push(acc[i - 1] + Math.hypot(pts[i].x - pts[i - 1].x, pts[i].z - pts[i - 1].z));
  const len = acc[acc.length - 1];
  if (len < .02) return;
  const width = i => w0 * (1 - .45 * acc[i] / len) * Math.pow(clamp((len - acc[i]) / tipLen), .75);
  const side = (f, off = 0) => pts.map((p, i) => {
    const q = pts[Math.min(pts.length - 1, i + 1)], o = pts[Math.max(0, i - 1)];
    const dx = q.x - o.x, dz = q.z - o.z, l = Math.hypot(dx, dz) || 1, w = width(i) * f / 2;
    return { x: p.x - dz / l * w + off * sun.x, z: p.z + dx / l * w + off * (sun.z - Lift) };
  });
  // Shadow: the same outline dropped to the floor and pushed along the sun.
  if (strength > 0) band(`${key} shadow`, side(1, h), side(-1, h), Black.withAlpha(strength * alpha * .8), L.shadow);
  const barkCol = Color.Lerp(Bark, rgb(96, 88, 78), dry), litCol = Color.Lerp(BarkLit, rgb(150, 140, 126), dry);
  band(`${key} edge`, side(1.18), side(-1.18), BarkDark.withAlpha(alpha), layer);
  band(`${key} bark`, side(1), side(-1), barkCol.withAlpha(alpha), layer + .001);
  // Lit half: the side away from the shadow.
  const d = { x: pts[pts.length - 1].x - pts[0].x, z: pts[pts.length - 1].z - pts[0].z };
  const litLeft = (-d.z * -sun.x + d.x * -sun.z) > 0;
  band(`${key} lit`, side(litLeft ? .9 : .05), side(litLeft ? .05 : -.9), litCol.withAlpha(alpha), layer + .002);
  trail(`${key} grain`, side(litLeft ? -.45 : .45), Math.max(.012, w0 * .12), BarkDark.withAlpha(.8 * alpha), layer + .003);
  // Fresh pale wood on the point.
  const cut = pts.findIndex((_, i) => acc[i] >= len - tipLen);
  if (cut >= 0 && cut < pts.length - 1) {
    const from = Math.max(0, cut - 1);
    band(`${key} tip`, side(.8).slice(from), side(-.8).slice(from), Color.Lerp(Heart, litCol, dry).withAlpha(alpha), layer + .004);
  }
}
