// Shared drawing for Nezuko's box: the wooden box worn on the back, its door, the straps, the
// stand-in pawns, and the small pieces Go in and Come out both use (wood dust, the inner glow,
// the sleeping marks, daze marks).
//
// The box is a true cuboid placed behind the wearer's feet along its facing, projected the way
// the kit projects height (a point h cells up is drawn h * Lift further north). Every side face
// whose outward normal points south is visible and is drawn between its base edge at H0 and its
// top edge at H1; the top face is drawn last. So one routine covers every facing: facing north the
// door face is toward the camera, facing south the box stands behind the pawn and only its top
// and the face against the back show, facing east or west it is a tall narrow slab. No per-facing
// method.
//
// Look from the anime: orange-brown vertical planks, black iron bands across it at three heights,
// iron brackets on the corners, a door on the face away from the back, two shoulder straps. It
// reaches from the waist to the nape. The top is also a lid (not in the anime): the burst exit
// (Come out with a strike, Guard) goes out through it, because the top shows in every facing and
// the leap goes up first; calm exits and Go in use the door.
//
// The pawns are the lab's usual stand-ins (body .22 x .32 at +.18, head .16 x .17 at +.58). A real
// pawn draws about 1.3x larger and 0.33 cells lower (the Samehada port measured it), so the C# port
// fits the box to the real pawn the same way.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, ShaderDatabase } from '../../js/engine.js';
import { draw } from './six-paths-solid.js';
import { Body, Y, Floor, Lift, sprite, band, trail, soft, glow, rand } from './six-paths-impact.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
export const disc = Meshes.disc(40, 'nezuko box disc');
export const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
export const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();

export const Skin = new Color(.83, .70, .54), Pale = new Color(1, .96, .90);
export const Wood = new Color(.70, .37, .17), WoodLit = new Color(.86, .52, .27), WoodDark = new Color(.42, .19, .08);
export const Grain = new Color(.36, .15, .06), Iron = new Color(.09, .09, .10), IronLit = new Color(.30, .30, .33);
export const Strap = new Color(.16, .12, .09), Inside = new Color(.10, .03, .04);
export const Pink = new Color(1, .55, .72), Dust = new Color(.78, .62, .45), Blood = new Color(.44, .06, .06);
export const Wearer = new Color(.18, .52, .36), Ally = new Color(.86, .45, .62), Enemy = new Color(.55, .38, .27);

// Decided shape, in cells. Along is the wearer's facing, across its left.
export const Back = -.20;              // box centre behind the feet
export const HalfDepth = .13, HalfWidth = .25;
export const H0 = .25, H1 = 1.05;      // bottom and top of the box above the floor
export const Door = { u0: .14, u1: .86, v0: .08, v1: .92 };   // the door's share of the outer face
export const bump = x => (x >= 0 && x <= 1) ? Math.sin(x * Math.PI) : 0;

// A screen point: ground point g lifted h cells.
export const up = (g, h) => ({ x: g.x, z: g.z + h * Lift });

// The box's frame for a wearer at `feet` facing `deg` (0 east, 90 north).
export function boxFrame(feet, deg, shakeX = 0) {
  const a = deg * Mathf.Deg2Rad, f = { x: Math.cos(a), z: Math.sin(a) }, n = { x: -f.z, z: f.x };
  const at = (along, across) => ({ x: feet.x + along * f.x + across * n.x + shakeX, z: feet.z + along * f.z + across * n.z });
  const C = at(Back, 0);
  // Base corners counter-clockwise seen from above: back-right, back-left, front-left, front-right,
  // where "back" is the outer face away from the wearer.
  const c = [at(Back - HalfDepth, -HalfWidth), at(Back - HalfDepth, HalfWidth), at(Back + HalfDepth, HalfWidth), at(Back + HalfDepth, -HalfWidth)];
  // Faces as edges i -> i+1; outward normals: 0 outer (-f), 1 left (+n), 2 inner (+f), 3 right (-n).
  const normals = [{ x: -f.x, z: -f.z }, n, f, { x: -n.x, z: -n.z }];
  return { f, n, at, C, c, normals };
}

// A straight bar of constant width from a to b.
export function seg(key, a, b, w, colour, layer) {
  const dx = b.x - a.x, dz = b.z - a.z, L = Math.hypot(dx, dz) || 1, nx = -dz / L * w / 2, nz = dx / L * w / 2;
  band(key, [{ x: a.x + nx, z: a.z + nz }, { x: b.x + nx, z: b.z + nz }], [{ x: a.x - nx, z: a.z - nz }, { x: b.x - nx, z: b.z - nz }], colour, layer);
}

// A quad on side face i between shares u0..u1 of its width and v0..v1 of its height.
function faceQuad(key, b, i, u0, u1, v0, v1, colour, layer, h0 = H0, h1 = H1) {
  const p = b.c[i], q = b.c[(i + 1) % 4];
  const g0 = { x: lerp(p.x, q.x, u0), z: lerp(p.z, q.z, u0) }, g1 = { x: lerp(p.x, q.x, u1), z: lerp(p.z, q.z, u1) };
  const lo = lerp(h0, h1, v0), hi = lerp(h0, h1, v1);
  band(key, [up(g0, lo), up(g1, lo)], [up(g0, hi), up(g1, hi)], colour, layer);
}
// A quad on the top face between shares of its two edges (u along edge 0, w toward edge 2).
function topQuad(key, b, u0, u1, w0, w1, colour, layer, h = H1) {
  const P = (u, w) => {
    const a = { x: lerp(b.c[0].x, b.c[1].x, u), z: lerp(b.c[0].z, b.c[1].z, u) };
    const d = { x: lerp(b.c[3].x, b.c[2].x, u), z: lerp(b.c[3].z, b.c[2].z, u) };
    return up({ x: lerp(a.x, d.x, w), z: lerp(a.z, d.z, w) }, h);
  };
  band(key, [P(u0, w0), P(u1, w0)], [P(u0, w1), P(u1, w1)], colour, layer);
}

// How lit a face is: the sun comes from the side opposite the shadow.
function shade(normal, sun) {
  const L = Math.hypot(sun.x, sun.z) || 1, lx = -sun.x / L, lz = -sun.z / L;
  return clamp(.5 + .5 * (normal.x * lx + normal.z * lz));
}

// The box, its straps and its door for one frame.
//   door      0..1 how far the door has swung open (1 = 150 degrees, folded against the side)
//   lid       0..1 how far the top lid has flipped up (1 = 110 degrees, over toward the wearer)
//   sleeping  0..1 someone asleep inside: the box breathes, the door seam glows pink, z marks rise
//   shake     sideways jolt in cells (anticipation before a burst, the latch closing)
//   inner     0..1 light spilling out of the open door or lid
//   t         the clip time, for the breathing and the z marks
// Returns the door's and the top's screen and ground centres, and the layer the box drew on.
export function drawBox(key, feet, deg, { door = 0, lid = 0, sleeping = 0, shake = 0, inner = 0, t = 0, sun, strength }) {
  const b = boxFrame(feet, deg, shake);
  const breathe = 1 + .018 * sleeping * Math.sin(t * TAU / 2.6);
  const h1 = H0 + (H1 - H0) * breathe;
  // Behind the wearer (north of it on screen) the box draws under the pawn so the pawn stands in
  // front; otherwise over it.
  const behind = b.C.z > feet.z + .05;
  const base = behind ? pawnLayer - .06 : Y + .002;
  let k = 0; const L = () => base + (k++) * .0012;

  sprite({ x: b.C.x + sun.x * h1 * .55, z: b.C.z + sun.z * h1 * .55 }, .75, .5, Body.withAlpha(strength), soft, shadowLayer);

  // Side faces, north first so nearer ones cover them.
  const faces = [0, 1, 2, 3].filter(i => b.normals[i].z < -1e-3)
    .sort((i, j) => (b.c[j].z + b.c[(j + 1) % 4].z) - (b.c[i].z + b.c[(i + 1) % 4].z));
  let doorMouth = null;
  for (const i of faces) {
    const lit = shade(b.normals[i], sun), face = Color.Lerp(WoodDark, WoodLit, .25 + .6 * lit);
    faceQuad(`${key} face ${i}`, b, i, 0, 1, 0, 1, face, L(), H0, h1);
    for (let g = 1; g <= 3; g++) faceQuad(`${key} grain ${i} ${g}`, b, i, g / 4 - .008, g / 4 + .008, .03, .97, Grain.withAlpha(.55), L(), H0, h1);
    if (i === 0) {
      // The door: a seam, a latch, and when open the dark inside with light spilling out.
      const { u0, u1, v0, v1 } = Door;
      faceQuad(`${key} door seam`, b, 0, u0 - .02, u1 + .02, v0 - .02, v1 + .02, Grain.withAlpha(.9), L(), H0, h1);
      faceQuad(`${key} door wood`, b, 0, u0, u1, v0, v1, face, L(), H0, h1);
      if (door > .02) {
        faceQuad(`${key} door hole`, b, 0, u0, u1, v0, v1, Inside, L(), H0, h1);
        if (inner > .01) faceQuad(`${key} door light`, b, 0, u0 + .05, u1 - .05, v0 + .05, v1 - .05, Pink.withAlpha(.55 * inner), L(), H0, h1);
      } else if (sleeping > .01) {
        const pulse = .65 + .3 * Math.sin(t * TAU / 2.6);
        faceQuad(`${key} door glow`, b, 0, u0 - .045, u1 + .045, v0 - .04, v1 + .04, Pink.withAlpha(pulse * sleeping), L(), H0, h1);
        faceQuad(`${key} door wood 2`, b, 0, u0, u1, v0, v1, face, L(), H0, h1);
      }
      if (door <= .02) faceQuad(`${key} latch`, b, 0, u1 - .09, u1 - .02, .45, .58, Iron, L(), H0, h1);
    }
    for (const v of [.12, .5, .86]) faceQuad(`${key} band ${i} ${v}`, b, i, 0, 1, v - .035, v + .035, Iron, L(), H0, h1);
    faceQuad(`${key} corner a ${i}`, b, i, 0, .1, .88, 1, Iron, L(), H0, h1);
    faceQuad(`${key} corner b ${i}`, b, i, .9, 1, .88, 1, Iron, L(), H0, h1);
    faceQuad(`${key} corner c ${i}`, b, i, 0, .1, 0, .12, Iron, L(), H0, h1);
    faceQuad(`${key} corner d ${i}`, b, i, .9, 1, 0, .12, Iron, L(), H0, h1);
    if (i === 0) {
      const { u0, u1, v0, v1 } = Door, gu = (u0 + u1) / 2, gv = (v0 + v1) / 2;
      const p = b.c[0], q = b.c[1];
      const g = { x: lerp(p.x, q.x, gu), z: lerp(p.z, q.z, gu) };
      doorMouth = { ground: g, screen: up(g, lerp(H0, h1, gv)) };
    }
  }
  // The top: planks, an iron frame and a band; with the lid up, the dark inside and light
  // spilling out, and the lid standing on its hinge at the inner edge (the wearer's side).
  if (lid <= .02) {
    topQuad(`${key} top`, b, 0, 1, 0, 1, WoodLit, L(), h1);
    for (let g = 1; g <= 3; g++) topQuad(`${key} top grain ${g}`, b, g / 4 - .01, g / 4 + .01, .05, .95, Grain.withAlpha(.5), L(), h1);
    topQuad(`${key} top band`, b, .47, .53, 0, 1, IronLit, L(), h1);
  } else {
    topQuad(`${key} top hole`, b, 0, 1, 0, 1, Inside, L(), h1);
    if (inner > .01) topQuad(`${key} top light`, b, .12, .88, .15, .85, Pink.withAlpha(.6 * inner), L(), h1);
  }
  topQuad(`${key} top rim a`, b, 0, 1, 0, .12, Iron, L(), h1);
  topQuad(`${key} top rim b`, b, 0, 1, .88, 1, Iron, L(), h1);
  topQuad(`${key} top rim c`, b, 0, .06, 0, 1, Iron, L(), h1);
  topQuad(`${key} top rim d`, b, .94, 1, 0, 1, Iron, L(), h1);
  if (lid > .02) {
    // Lid depth is the box's depth; at theta = 0 it lies over the top, opening it lifts the outer
    // edge up and over toward the wearer, to 110 degrees.
    const th = lid * 110 * Mathf.Deg2Rad, D = 2 * HalfDepth, hinge0 = b.c[3], hinge1 = b.c[2];
    const off = { x: -b.f.x * D * Math.cos(th), z: -b.f.z * D * Math.cos(th) }, rise = D * Math.sin(th);
    const edge = (u, w) => {
      const h = { x: lerp(hinge0.x, hinge1.x, u), z: lerp(hinge0.z, hinge1.z, u) };
      return up({ x: h.x + off.x * w, z: h.z + off.z * w }, h1 + rise * w);
    };
    const lidLit = th < Math.PI / 2 ? WoodLit : WoodDark, lidLayer = L() + .004;
    band(`${key} lid`, [edge(0, 0), edge(1, 0)], [edge(0, 1), edge(1, 1)], lidLit, lidLayer);
    for (const w of [.0, .88]) band(`${key} lid rim ${w}`, [edge(0, w), edge(1, w)], [edge(0, w + .12), edge(1, w + .12)], Iron, lidLayer + .0005);
    band(`${key} lid band`, [edge(.47, 0), edge(.53, 0)], [edge(.47, 1), edge(.53, 1)], IronLit, lidLayer + .0006);
  }
  const topMouth = { ground: b.C, screen: up(b.C, h1), h: h1 };

  // The door when it is open: a panel swinging on its hinge at the outer face's u1 edge.
  const outerG = (u) => ({ x: lerp(b.c[0].x, b.c[1].x, u), z: lerp(b.c[0].z, b.c[1].z, u) });
  if (!doorMouth) {
    const gu = (Door.u0 + Door.u1) / 2, g = outerG(gu);
    doorMouth = { ground: g, screen: up(g, lerp(H0, h1, .5)) };
  }
  if (door > .02) {
    const phi = door * 150 * Mathf.Deg2Rad, hinge = outerG(Door.u1), w = (Door.u1 - Door.u0) * 2 * HalfWidth;
    // At phi = 0 the panel runs from the hinge back toward u0 (along -n from the hinge side); it
    // turns outward (-f) as it opens.
    const dx = -b.n.x * Math.cos(phi) - b.f.x * Math.sin(phi), dz = -b.n.z * Math.cos(phi) - b.f.z * Math.sin(phi);
    const free = { x: hinge.x + dx * w, z: hinge.z + dz * w };
    const lo = lerp(H0, h1, Door.v0), hi = lerp(H0, h1, Door.v1);
    const panelLayer = free.z > b.C.z + .02 && behind ? base - .004 : L();
    const nrm = { x: -dz, z: dx }, lit = shade(nrm, sun);
    band(`${key} door panel`, [up(hinge, lo), up(free, lo)], [up(hinge, hi), up(free, hi)], Color.Lerp(WoodDark, Wood, .3 + .6 * lit), panelLayer);
    for (const v of [.2, .8]) {
      const a = lerp(lo, hi, v - .04), c = lerp(lo, hi, v + .04);
      band(`${key} door band ${v}`, [up(hinge, a), up(free, a)], [up(hinge, c), up(free, c)], Iron, panelLayer + .0005);
    }
  }

  // Shoulder straps: from the box's inner face at shoulder height over the shoulders, down the
  // chest to the hip. Shown when the wearer faces south, east or west; facing north the chest is
  // away from the camera.
  const facingNorth = b.f.z > .7;
  if (!facingNorth) {
    for (const sd of [-1, 1]) {
      if (Math.abs(b.f.x) > .7 && sd * b.n.z > 0) continue;   // side view: only the near strap
      const top = b.at(Back + HalfDepth, sd * .15), chest = b.at(.06, sd * .12), hip = b.at(.03, sd * .12);
      seg(`${key} strap a ${sd}`, up(top, .62), up(chest, .45), .055, Strap, Y + .004);
      seg(`${key} strap b ${sd}`, up(chest, .45), up(hip, .28), .055, Strap, Y + .0041);
    }
  }

  // Sleeping marks: small "z" shapes that rise from the top and fade, one every 1.3 s.
  if (sleeping > .01) {
    for (let j = 0; j < 3; j++) {
      const u = ((t + j * 1.3) % 3.9) / 3.9, z = .05 + .04 * u, a = Math.sin(u * Math.PI) * .9 * sleeping;
      const o = up({ x: b.C.x + .12 + u * .25 + Math.sin(u * 6 + j) * .04, z: b.C.z }, h1 + .15 + u * .7);
      const c = Pale.withAlpha(a), l = Y + .03 + j * .002;
      seg(`${key} z top ${j}`, { x: o.x - z, z: o.z + z }, { x: o.x + z, z: o.z + z }, .022, c, l);
      seg(`${key} z mid ${j}`, { x: o.x + z, z: o.z + z }, { x: o.x - z, z: o.z - z }, .022, c, l + .0005);
      seg(`${key} z low ${j}`, { x: o.x - z, z: o.z - z }, { x: o.x + z, z: o.z - z }, .022, c, l + .001);
    }
  }
  return { doorMouth, topMouth, layer: base, behind, frame: b };
}

// Stand-in pawn: two discs and a soft shadow; `scale` and `alpha` for things half inside the box,
// `downed` lies it on its side.
export function figure(pos, colour, sun, strength, { scale = 1, alpha = 1, downed = false, layer = pawnLayer, h = 0 } = {}) {
  const s = scale, a = alpha;
  sprite({ x: pos.x + sun.x * (.45 + h), z: pos.z + sun.z * (.45 + h) }, .85 * s, .4 * s, Body.withAlpha(strength * a), soft, shadowLayer);
  const P = { x: pos.x, z: pos.z + h * Lift };
  if (downed) {
    draw(disc, P.x + .05 * s, layer, P.z + .10 * s, .32 * s, .20 * s, 0, colour.withAlpha(a));
    draw(disc, P.x - .30 * s, layer + .002, P.z + .12 * s, .16 * s, .16 * s, 0, Skin.withAlpha(a));
    return;
  }
  draw(disc, P.x, layer, P.z + .18 * s, .22 * s, .32 * s, 0, colour.withAlpha(a));
  draw(disc, P.x, layer + .002, P.z + .58 * s, .16 * s, .17 * s, 0, Skin.withAlpha(a));
}

// Wood dust thrown off the box: puffs that fly out from `at` over `life` seconds and settle.
export function woodDust(key, at, age, amount = 1, life = .5, seed = 0) {
  if (age < 0 || age > life * 1.6) return;
  for (let i = 0; i < 8; i++) {
    const u = clamp(age / (life * (.7 + rand(seed + i + 10) * .6)));
    if (u >= 1) continue;
    const th = rand(seed + i) * TAU, far = u * (.25 + rand(seed + i + 20) * .45) * amount;
    sprite({ x: at.x + Math.cos(th) * far, z: at.z + Math.sin(th) * far * .7 + u * .15 }, (.18 + u * .3) * amount, (.15 + u * .25) * amount,
      Dust.withAlpha(Math.sin(u * Math.PI) * .6), puff, Y + .02 + i * .0005);
  }
  for (let i = 0; i < 6; i++) {   // splinters: short dark flecks that fall and stay a moment
    const life2 = .35 + rand(seed + i + 40) * .2, u = age / life2;
    if (u > 2.2) continue;
    const th = rand(seed + i + 50) * TAU, far = Math.min(u, 1) * (.3 + rand(seed + i + 60) * .4) * amount;
    const h = u < 1 ? Math.max(0, .35 * Math.sin(u * Math.PI)) : 0;
    const g = { x: at.x + Math.cos(th) * far, z: at.z + Math.sin(th) * far * .7 - .15 * Math.min(u, 1) };
    draw(disc, g.x, u < 1 ? Y + .025 : Floor + .03, g.z + h * Lift, .035, .018, 0, WoodDark.withAlpha(u < 1 ? 1 : 1 - (u - 1) / 1.2));
  }
}

// Three pale marks circling over a pawn's head: stunned or dazed.
export function dazeMarks(pos, s, age, length) {
  if (age < 0 || age > length + .3) return;
  const fade = 1 - smooth((age - length) / .3);
  for (let i = 0; i < 3; i++) {
    const turn = s * 5 + i * 2.094;
    sprite({ x: pos.x + Math.cos(turn) * .2, z: pos.z + .86 + Math.sin(turn) * .06 }, .08, .08, Pale.withAlpha(.9 * fade), soft, Y + .03 + i * .0005);
  }
}

export { Body, Y, Floor, Lift, sprite, band, trail, soft, glow, rand, smooth, clamp, lerp, TAU };
