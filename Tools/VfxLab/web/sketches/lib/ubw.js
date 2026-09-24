// Trace kit: Unlimited Blade Works' world, shared by its two sketches (chant and open, commands).
// Not a sketch itself, so it is not listed in sketches/index.js.
//
// The world is a disc: a dusk floor with dry cracks, patches and stones, the shadows of three huge
// gears turning on it (the gears in the sky are shown only by their shadows), a warm light over
// it, and a ring of fire standing on its edge. Its swords are laid out by swords(): verse k's
// swords spread over the ring of the world that verse adds, by a sunflower spiral.
//
// Drawing: everything here is flat on the floor or a level circle, so it looks the same from any
// facing. The flame is a lab texture (lab/ubw-flame), still to be made a PNG.
import { Color, MaterialPool, Mesh, Meshes, ShaderDatabase } from '../../js/engine.js';
import { registerLabTexture, pixels, fbm } from '../../js/standins.js';
import { draw } from './six-paths-solid.js';
import { Y, Floor, sprite, soft, glow, rand } from './six-paths-impact.js';
import { ringAt, line, rock, shadowLayer } from './goku.js';
import { clamp, Black, Weapons, upright } from './trace.js';

registerLabTexture('lab/ubw-flame', () => pixels(128, (u, v) => {
  const y = 1 - v, n = fbm(u * 4, v * 3, 91, 3, 4);
  const hw = .38 * (y < .22 ? Math.sqrt(y / .22) : Math.pow(Math.max(0, 1 - (y - .22) / .78), .9));
  const dx = Math.abs(u - .5 + (n - .5) * .22 * y);
  return [1, 1, 1, clamp((hw - dx) / (.3 * hw + .02)) * Math.min(1, (1 - y) * 5)];
}));
export const flame = MaterialPool.MatFrom('lab/ubw-flame', ShaderDatabase.MoteGlow);
export const light = MaterialPool.MatFrom('white', ShaderDatabase.MoteGlow);
const floorDisc = Meshes.disc(72, 'ubw floor');
export const Dusk = new Color(.5, .27, .14), DuskDark = new Color(.2, .1, .06), Sunset = new Color(1, .55, .25);
export const FireOuter = new Color(1, .42, .12), FireCore = new Color(1, .82, .45), Ember = new Color(1, .62, .28);

// The rule's fixed numbers and decided looks. The lab camera sits about 2 cells north of the
// chosen cell, so the world is centred there.
export const Radius = [6, 9, 12], SceneNorth = 2, DuskShadow = 2;
const Mix = ['LongSword', 'LongSword', 'LongSword', 'Spear', 'Spear', 'MonoSword', 'MonoSword', 'Knife', 'LargeSword', 'Wyrmslayer'];
const Gears = [
  { r: .26, x: -.35, z: .28, spin: 7, teeth: 14 },
  { r: .17, x: .33, z: .3, spin: -11, teeth: 10 },
  { r: .22, x: .12, z: -.36, spin: 9, teeth: 12 },
];

// A gear's silhouette at radius 1: toothed rim, spokes and hub, one mesh per tooth count.
const gearMeshes = new Map();
export function gearMesh(teeth) {
  if (gearMeshes.has(teeth)) return gearMeshes.get(teeth);
  const m = new Mesh(`ubw gear ${teeth}`), xz = [], tri = [], n = teeth * 4;
  for (let j = 0; j < n; j++) {
    const a = j / n * Math.PI * 2, out = j % 4 < 2 ? 1 : .84;
    xz.push(Math.cos(a) * out, Math.sin(a) * out, Math.cos(a) * .64, Math.sin(a) * .64);
    const o = j * 2, next = ((j + 1) % n) * 2;
    tri.push(o, next, o + 1, o + 1, next, next + 1);
  }
  const addQuad = (pts) => { const b = xz.length / 2; pts.forEach(q => xz.push(q[0], q[1])); tri.push(b, b + 1, b + 2, b, b + 2, b + 3); };
  for (let k = 0; k < 5; k++) {
    const a = k / 5 * Math.PI * 2, c = Math.cos(a), s = Math.sin(a), w = .06;
    addQuad([[c * .15 - s * w, s * .15 + c * w], [c * .66 - s * w, s * .66 + c * w], [c * .66 + s * w, s * .66 - c * w], [c * .15 + s * w, s * .15 - c * w]]);
  }
  const hub = xz.length / 2;
  xz.push(0, 0);
  for (let j = 0; j <= 16; j++) { const a = j / 16 * Math.PI * 2; xz.push(Math.cos(a) * .2, Math.sin(a) * .2); if (j) tri.push(hub, hub + j + 1, hub + j); }
  m.setFlat(xz, tri);
  gearMeshes.set(teeth, m);
  return m;
}
// The three gear shadows of a world of radius R, turning and drifting a little.
export function gearShadows(c, R, s, opacity) {
  if (opacity <= 0) return;
  Gears.forEach((g, i) => {
    const drift = .05 * R * Math.sin(s * .3 + i * 2);
    draw(gearMesh(g.teeth), c.x + g.x * R + drift, shadowLayer + .001, c.z + g.z * R, g.r * R, g.r * R, s * g.spin, Black.withAlpha(opacity));
  });
}

// Every sword of all three verses, relative to the caster. Depends only on the count, so it is
// worked out once per count. sw.d is the distance from the caster, sw.k the verse it belongs to.
const fields = new Map();
export function swords(perVerse) {
  if (fields.has(perVerse)) return fields.get(perVerse);
  const list = [];
  for (let k = 0; k < 3; k++) {
    const r0 = k ? Radius[k - 1] + .3 : 1.3, r1 = Radius[k] - .6;
    for (let i = 0; i < perVerse; i++) {
      const seed = k * 1000 + i + 1;
      const r = Math.sqrt(r0 * r0 + (r1 * r1 - r0 * r0) * (i + .5) / perVerse) + (rand(seed * 3) - .5) * .35;
      const a = i * 2.39996 + k * 1.1 + (rand(seed * 5) - .5) * .25, x = Math.cos(a) * r, z = Math.sin(a) * r;
      list.push({ k: k + 1, seed, x, z, d: Math.hypot(x, z), w: Weapons[Mix[Math.floor(rand(seed * 7) * Mix.length)]],
        lean: rand(seed * 11), dir: rand(seed * 13) * 360, turn: (rand(seed * 17) - .5) * 2, sink: .16 + rand(seed * 19) * .12 });
    }
  }
  fields.set(perVerse, list);
  return list;
}
// A sword of the world standing in its place. leanMax in degrees.
export const swordPose = (sw, c, size, leanMax) => upright(sw.w, size, { x: c.x + sw.x, z: c.z + sw.z }, sw.lean * leanMax, sw.dir, sw.turn * 30, sw.sink);

// A ring of flames standing on the floor at radius r, with a glowing band under it and embers.
export function fireRing(key, c, r, s, height, alpha) {
  if (r <= .05 || alpha <= 0) return;
  const n = Math.max(10, Math.ceil(Math.PI * 2 * r / .26));
  for (let i = 0; i < n; i++) {
    const a = (i + rand(i * 7 + 3) * .6) / n * Math.PI * 2, base = { x: c.x + Math.cos(a) * r, z: c.z + Math.sin(a) * r };
    const flick = .55 + .25 * Math.sin(s * 11 + i * 1.7) + .2 * Math.sin(s * 17.3 + i * 2.9);
    const h = height * flick * (.75 + .5 * rand(i * 13 + 1)), w = .3 * (.8 + .4 * rand(i * 5 + 2));
    sprite({ x: base.x, z: base.z + h * .5 }, w, h, FireOuter.withAlpha(.5 * alpha), flame, Y + .02 + (base.z < c.z ? .002 : 0));
    sprite({ x: base.x, z: base.z + h * .3 }, w * .5, h * .6, FireCore.withAlpha(.45 * alpha), flame, Y + .0205 + (base.z < c.z ? .002 : 0));
  }
  ringAt(c, r, FireOuter.withAlpha(.35 * alpha), Floor + .008, true, light);
  for (let j = 0; j < Math.ceil(n / 4); j++) {
    const a = rand(j * 11 + 5) * Math.PI * 2, phase = (s * .8 + rand(j * 3 + 1)) % 1;
    const at = { x: c.x + Math.cos(a) * r + Math.sin(s * 2 + j) * .12, z: c.z + Math.sin(a) * r + phase * height * 1.8 };
    sprite(at, .07, .07, Ember.withAlpha(Math.sin(phase * Math.PI) * .9 * alpha), glow, Y + .025);
  }
}

// The dusk floor out to radius r of a world of radius R, with dry cracks, patches and stones that
// were there all along, firelight inside the edge, and a warm light over everything. dusk 0..1.
export function wasteland(key, c, r, R, alpha, dusk) {
  if (r <= .05 || alpha <= 0) return;
  draw(floorDisc, c.x, Floor + .0003, c.z, r, r, 0, Dusk.withAlpha(.88 * dusk * alpha));
  [[.25, .09], [.7, .045], [1.4, .02]].forEach(([inset, a], i) => {
    if (r > inset + .2) ringAt(c, r - inset, FireOuter.withAlpha(a * dusk * alpha), Floor + .0007 + i * .0001, true, light);
  });
  for (let i = 0; i < Math.round(R * 2); i++) {
    const a = rand(i * 47 + 5) * Math.PI * 2, d = Math.sqrt(rand(i * 53 + 1)) * R * .9;
    if (d < r - .3) rock({ x: c.x + Math.cos(a) * d, z: c.z + Math.sin(a) * d }, .07 + rand(i * 59) * .08, rand(i * 61) * 360, alpha, 2 * (i % 3), Floor + .0009);
  }
  for (let i = 0; i < 10; i++) {
    const a = rand(i * 17 + 2) * Math.PI * 2, d = Math.sqrt(rand(i * 19 + 4)) * R * .8, at = { x: c.x + Math.cos(a) * d, z: c.z + Math.sin(a) * d };
    const size = (1.2 + rand(i * 23) * 1.8) * R / 6;
    if (d + size * .4 < r) sprite(at, size, size * .7, (i % 2 ? DuskDark : Sunset).withAlpha((i % 2 ? .16 : .06) * dusk * alpha), soft, Floor + .0005, rand(i) * 180);
  }
  for (let i = 0; i < Math.round(R * 2.5); i++) {
    const a0 = rand(i * 29 + 7) * Math.PI * 2, d0 = Math.sqrt(rand(i * 31 + 9)) * R * .92;
    if (d0 > r - .2) continue;
    const pts = [{ x: c.x + Math.cos(a0) * d0, z: c.z + Math.sin(a0) * d0 }];
    let ang = rand(i * 37 + 3) * Math.PI * 2;
    for (let j = 1; j <= 5; j++) {
      ang += (rand(i * 41 + j) - .5) * 1.2;
      const q = pts[j - 1], step = .25 + rand(i * 43 + j) * .25, next = { x: q.x + Math.cos(ang) * step, z: q.z + Math.sin(ang) * step };
      if (Math.hypot(next.x - c.x, next.z - c.z) > r - .1) break;
      pts.push(next);
    }
    line(`${key} dry ${i}`, pts, .03, Black.withAlpha(.28 * alpha), undefined, Floor + .0006, 'both');
  }
  sprite(c, r * 2.3, r * 2.3, Sunset.withAlpha(.07 * dusk * alpha), glow, Y + .03);
}
