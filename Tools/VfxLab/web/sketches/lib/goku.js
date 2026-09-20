// Goku kit: the drawing pieces shared by Solar Flare, Instant Transmission, Kamehameha and Spirit
// Bomb. Not a sketch itself, so it is not listed in sketches/index.js.
//
// Everything here is a level circle, a quad or a strip, so nothing needs a per-facing method. Every
// function takes ages and times and keeps no state. Mesh keys must be unique within a frame: pass a
// key that names the sketch and the part.
//
// Port notes: quads (MeshPool.plane10) with the white texture or SoftDisc, one disc and one ring
// mesh, and strip meshes rebuilt while they show. strip and streak are the Flying Thunder God ones.
// Ki is blue-white and Solar Flare is yellow-white; neither is the Six Paths violet.
import { AltitudeLayer, Color, Mathf, Meshes, MeshPool } from '../../js/engine.js';
import { draw } from './six-paths-solid.js';
import { Y, Floor, sprite, glow, soft, rand } from './six-paths-impact.js';
import { strip, streak, whiteGlow, downed, Skin, EnemyColour, Ink } from './flying-thunder-god.js';
export { strip, streak, whiteGlow, downed, Skin, EnemyColour, Ink };

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01;
const disc = Meshes.disc(32, 'goku disc');
const thinRing = Meshes.band(.93, 1, 48, 'goku thin ring');
// A ring mesh scales its thickness with its radius, so rings are picked from a set by the thickness wanted.
const Bands = [.995, .99, .98, .96, .93, .86, .75].map(inner => ({ inner, mesh: Meshes.band(inner, 1, 72, `goku band ${inner}`) }));
const ThinLine = .07, WideLine = .45;
export const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
export const buildingLayer = AltitudeLayer.Building.AltitudeFor();
export const Ki = new Color(.3, .66, 1), KiDeep = new Color(.08, .3, .95), KiSky = new Color(.55, .82, 1), KiIce = new Color(.8, .93, 1);
export const White = new Color(1, 1, 1), Flare = new Color(1, .96, .72), FlareWarm = new Color(1, .82, .35);
export const Gi = new Color(.95, .45, .1), Hair = new Color(.06, .05, .05), Ally = new Color(.39, .58, .65), Dust = new Color(.52, .45, .37);
export const Stone = new Color(.36, .34, .33), StoneTop = new Color(.5, .48, .46);
export const Chest = .3, Lift = .6;

// The parts of a stand-in pawn as ellipses: [centre x, centre z, radius x, radius z, colour].
const standing = (colour, hair) => [[0, .18, .22, .32, colour], [0, .58, .16, .17, Skin], ...(hair ? [[0, .69, .19, .1, Hair]] : [])];
const lying = colour => [[0, .12, .32, .2, colour], [.42, .14, .16, .17, Skin]];

// A ring round a point, about 0.07 cells thick, or 0.45 with wide. Round on screen whatever the facing.
export function ringAt(pos, radius, colour, layer = Floor + .02, wide = false, material) {
  if (radius <= 0) return;
  const want = 1 - Math.min(.5, (wide ? Math.min(WideLine, radius * .09 + .05) : ThinLine) / radius);   // a wide ring on a small circle stays in proportion
  const pick = Bands.reduce((best, b) => Math.abs(b.inner - want) < Math.abs(best.inner - want) ? b : best);
  draw(pick.mesh, pos.x, layer, pos.z, radius, radius, 0, colour, material);
}

// A stand-in pawn. opts: alpha, hair (the caster), lie (downed), tint + tintAmount (lit by an
// effect), arms 0..2 raised by raise 0..1, outline 0..1 (a white edge, for a pawn that is touched).
export function pawn(pos, colour, sun, strength, { alpha = 1, hair = false, lie = false, tint = null, tintAmount = 0, arms = 0, raise = 0, outline = 0 } = {}) {
  if (alpha <= 0) return;
  const parts = lie ? lying(colour) : standing(colour, hair), reach = lie ? .2 : .45;
  sprite({ x: pos.x + sun.x * reach, z: pos.z + sun.z * reach }, lie ? .95 : .85, .4, Ink.withAlpha(strength * alpha), soft, shadowLayer);
  parts.forEach(([cx, cz, rx, rz, c], k) => {
    if (outline > 0) draw(disc, pos.x + cx, pawnLayer - .002, pos.z + cz, rx + .05, rz + .05, 0, White.withAlpha(.8 * outline * alpha));
    const lit = tint && c !== Hair ? Color.Lerp(c, tint, tintAmount) : c;
    draw(disc, pos.x + cx, pawnLayer + k * .002, pos.z + cz, rx, rz, 0, lit.withAlpha(alpha));
  });
  if (!lie && raise > 0) for (let k = 0; k < arms; k++) {
    const side = k ? -1 : 1, lean = side * (28 - 16 * raise);
    draw(MeshPool.plane10, pos.x + side * (.2 + .03 * raise), pawnLayer + .008, pos.z + .42 + .24 * raise, .07, .1 + .3 * raise, lean, Skin.withAlpha(alpha));
  }
}

// Instant Transmission's vanish, as the anime draws it: the body breaks into horizontal slices
// that slide left and right alternately, stretch into lines, go white and thin away. u is 0
// (whole) to 1 (gone); run it backwards for the arrival.
export function sliced(pos, colour, u, sun, strength, { hair = false, lie = false } = {}) {
  if (u >= 1) return;
  if (u <= 0) { pawn(pos, colour, sun, strength, { hair, lie }); return; }
  const parts = lie ? lying(colour) : standing(colour, hair), left = 1 - u;
  const low = Math.min(...parts.map(q => q[1] - q[3])), high = Math.max(...parts.map(q => q[1] + q[3]));
  const count = Math.max(4, Math.round((high - low) / .085)), step = (high - low) / count;
  sprite({ x: pos.x + sun.x * .4, z: pos.z + sun.z * .4 }, .85, .4, Ink.withAlpha(strength * left * left), soft, shadowLayer);
  for (let i = 0; i < count; i++) {
    const zc = low + (i + .5) * step, side = i % 2 ? 1 : -1;
    const shift = side * (u * u * .85 + Math.sin(u * 50 + i * 2.1) * .035 * Math.sin(u * Math.PI));
    parts.forEach(([cx, cz, rx, rz, c], k) => {
      const q = 1 - ((zc - cz) / rz) ** 2; if (q <= 0) return;
      const half = rx * Math.sqrt(q) * (1 + 2.2 * u);
      draw(MeshPool.plane10, pos.x + cx + shift, pawnLayer + k * .002, pos.z + zc, half * 2, step * (1.02 - .82 * u), 0, Color.Lerp(c, KiIce, Math.min(1, u * 1.3)).withAlpha(left));
    });
  }
  sprite({ x: pos.x, z: pos.z + .3 }, 1.2 + u, 1.1, KiSky.withAlpha(.5 * Math.sin(u * Math.PI)), glow, Y + .01);
}

// What is left in the air and on the floor where a pawn vanished or appeared: horizontal speed
// lines at body height that shoot sideways, a thin ring on the floor, a little dust.
export function blink(key, pos, age, life = .28) {
  if (age < 0 || age >= life) return;
  const u = age / life, f = (1 - u) * (1 - u);
  for (let i = 0; i < 7; i++) {
    const side = i % 2 ? 1 : -1, z = pos.z - .05 + i * .115, near = .1 + u * 1.2, far = near + .5 + rand(i + 3) * .9 * (1 - u * .5);
    streak(`${key} line ${i}`, { x: pos.x + side * near, z }, { x: pos.x + side * far, z }, .045, KiIce.withAlpha(f), whiteGlow, Y + .02, 3);
  }
  ringAt(pos, .25 + smooth(u) * .9, KiIce.withAlpha(.6 * (1 - u)));
  for (let i = 0; i < 6; i++) {
    const ang = i * 1.05 + rand(i), d = .2 + u * (.5 + rand(i + 8) * .5);
    sprite({ x: pos.x + Math.cos(ang) * d, z: pos.z + Math.sin(ang) * d * .7 + u * .15 }, .3 + u * .4, .24 + u * .3, Dust.withAlpha(.4 * Math.sin(u * Math.PI)), soft, Y + .005);
  }
}

// A line through points, in any material. taper 'both' thins it to nothing at both ends, 'end' only at
// the far end, 'none' keeps the width. Used for cracks, lightning and tails.
export function line(key, pts, width, colour, material, layer, taper = 'end') {
  if (pts.length < 2) return;
  const a = [], b = [], last = pts.length - 1;
  pts.forEach((q, i) => {
    const prev = pts[Math.max(0, i - 1)], next = pts[Math.min(last, i + 1)], dx = next.x - prev.x, dz = next.z - prev.z, len = Math.hypot(dx, dz) || 1, u = i / last;
    const w = width / 2 * (taper === 'both' ? Math.sin(u * Math.PI) : taper === 'end' ? Math.pow(1 - u, .6) : 1) + .004;
    a.push({ x: q.x - dz / len * w, z: q.z + dx / len * w }); b.push({ x: q.x + dz / len * w, z: q.z - dx / len * w });
  });
  strip(key, a, b, colour, material, layer);
}

// A four-point glint: a core and two crossed rays. Used at the forehead and on sparkles.
export function glint(key, at, size, alpha, colour = White, turn = 0) {
  if (alpha <= 0 || size <= 0) return;
  sprite(at, size * .9, size * .9, colour.withAlpha(alpha), glow, Y + .06);
  [0, 90].forEach((deg, i) => {
    const r = (deg + turn) * Mathf.Deg2Rad, l = size * (i ? .7 : 1), dx = Math.cos(r) * l, dz = Math.sin(r) * l;
    streak(`${key} ${i}`, { x: at.x - dx, z: at.z - dz }, { x: at.x + dx, z: at.z + dz }, size * .16, colour.withAlpha(alpha), whiteGlow, Y + .061, 4);
  });
}

// Three small stars circling a pawn's head: it is stunned.
export function stunStars(key, pos, s, alpha, colour = Flare) {
  if (alpha <= 0) return;
  for (let i = 0; i < 3; i++) {
    const ang = s * 5 + i * 2.094, at = { x: pos.x + Math.cos(ang) * .27, z: pos.z + .84 + Math.sin(ang) * .1 };
    glint(`${key} star ${i}`, at, .1, alpha * (.6 + .4 * Math.sin(ang)), colour, 45);
  }
}

// A ball of ki: glow, blue shell with a dark rim, pale inside, a white core that beats, and rays
// of light that leak out and turn. rays 0..1 scales how far they reach.
export function kiBall(key, at, size, s, alpha, rays = 1) {
  if (size <= .01 || alpha <= 0) return;
  const r = size / 2, beat = 1 + .12 * Math.sin(s * 38);
  sprite(at, size * 3.6, size * 3.6, Ki.withAlpha(.6 * alpha), glow, Y + .1);
  draw(disc, at.x, Y + .11, at.z, r, r, 0, Ki.withAlpha(.9 * alpha));
  draw(disc, at.x, Y + .111, at.z, r * .8, r * .8, 0, KiSky.withAlpha(.85 * alpha));
  draw(thinRing, at.x, Y + .112, at.z, r, r, 0, KiDeep.withAlpha(alpha));
  if (rays > 0) for (let i = 0; i < 8; i++) {
    const ang = (i * 45 + s * (i % 2 ? 60 : -45) + rand(i + 20) * 20) * Mathf.Deg2Rad, flick = .55 + .45 * Math.sin(s * 23 + i * 1.9);
    const reach = r * (1.5 + 2.4 * rand(i + 40)) * rays * flick;
    streak(`${key} ray ${i}`, { x: at.x + Math.cos(ang) * r * .3, z: at.z + Math.sin(ang) * r * .3 }, { x: at.x + Math.cos(ang) * (r + reach), z: at.z + Math.sin(ang) * (r + reach) },
      r * .34, KiIce.withAlpha(.8 * alpha * flick), whiteGlow, Y + .113, 4);
  }
  draw(disc, at.x, Y + .12, at.z, r * .42 * beat, r * .42 * beat, 0, White.withAlpha(alpha));
  sprite(at, size * .9 * beat, size * .9 * beat, White.withAlpha(.9 * alpha), glow, Y + .121);
}

// A flame-shaped aura standing round a pawn, drawn behind it. power 0..1.
export function aura(key, pos, s, power, colour = KiSky) {
  if (power <= 0) return;
  const left = [], right = [], steps = 12;
  for (let j = 0; j <= steps; j++) {
    const v = j / steps, z = pos.z - .12 + v * (1.25 + .35 * power);
    const half = (.46 * Math.sin(Math.PI * Math.pow(v, .62)) * (1 - v * .45) + .03) * (.8 + .2 * power), sway = Math.sin(s * 27 + j * 1.2) * .045 * v;
    left.push({ x: pos.x - half + sway + Math.sin(s * 41 + j * 2.3) * .025, z }); right.push({ x: pos.x + half + sway + Math.sin(s * 37 + j * 1.7) * .025, z });
  }
  strip(`${key} aura`, left, right, colour.withAlpha(.34 * power), whiteGlow, pawnLayer - .02);
  sprite({ x: pos.x, z: pos.z + .35 }, 1.5, 1.9, colour.withAlpha(.3 * power), glow, pawnLayer - .021);
}

// A square of wall on a cell, turned to deg.
export function wallCell(at, deg) {
  draw(MeshPool.plane10, at.x, buildingLayer, at.z, 1, 1, -deg, Stone);
  draw(MeshPool.plane10, at.x, buildingLayer + .002, at.z + .06, .9, .78, -deg, StoneTop);
}
export { clamp, smooth };
