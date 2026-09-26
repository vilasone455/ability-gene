// Shared drawing for the Satō (Ajin) kit: IBM black matter (flakes and ooze strands) and Satō's
// Black Ghost figure. The Reset regrowth sketch will reuse the flakes and the ooze.
//
// Look (sources in the Black Ghost sketch header): a tall lanky humanoid, arms past the knees, dark
// grey body wrapped in bands with a lighter line on each band, six-finger claws, and a head that is
// a flat shield-shaped plate (official figure, manga version); the manga's 3/4 panel shows it as a
// wedge flaring from the neck. Black flakes peel off the edges and
// drift up all the time.
//
// Drawing: the figure is drawn the way RimWorld draws a pawn sprite, upright in the screen plane.
// A joint at (u, h) is drawn at feet.x + u, feet.z + h * Lift. Facing south uses the front pose,
// north the same pose darker with the claws behind the body, east a profile pose with its own draw
// method, west the east pose mirrored. The head is the plate: from the front a dark shield face
// with a pale rim, from the back its plain back, from the side a wedge with its flat top edge
// (wiki: a triangle seen from above, flat seen from the side). The back view adds a spine groove
// and shoulder blades, the front a chest line, so the two are not the same drawing.
//
// Forming and dissolving clamp every joint's height into [lo, hi]: parts above `hi` are squeezed
// down onto the rising edge, parts below `lo` are pushed up onto the falling edge, and a line of
// flakes sits on that edge. So one routine draws the ghost growing from the feet up and breaking
// up from the feet up.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../../js/engine.js';
import { draw, mesh } from './six-paths-solid.js';
import { Body, Y, Floor, Lift, sprite, band, trail, soft, rand } from './six-paths-impact.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
export const disc = Meshes.disc(40, 'ajin disc');
export const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
export const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();

export const Ghost = new Color(.17, .17, .20), GhostEdge = new Color(.03, .03, .04), GhostLit = new Color(.29, .29, .33);
export const PlateFace = new Color(.06, .06, .07);
export const Wrap = new Color(.50, .50, .55), Flake = new Color(.045, .045, .055), Hollow = new Color(.015, .015, .02);
export const Skin = new Color(.83, .70, .54), Shirt = new Color(.86, .86, .83), Cap = new Color(.30, .33, .33);
export const Enemy = new Color(.55, .38, .27), Blood = new Color(.45, .05, .05), Slash = new Color(.90, .90, .95);
export const Top = 2.02;   // head top of the ghost at scale 1, in cells of height
// The ghost stands like a pawn sprite, so its height is drawn at 0.75 per cell, not the effects'
// 0.60 Lift: at 0.60 the legs came out stubby and the figure wider than tall.
export const Stand = .75;

// Irregular shard meshes for flakes, built once.
const shards = [0, 1, 2, 3, 4, 5].map(k => {
  const m = mesh('ajin shard ' + k), n = 3 + (k % 2), v = [];
  for (let i = 0; i < n; i++) {
    const a = i / n * TAU + rand(k * 11 + i) * .9, r = .55 + rand(k * 7 + i + 3) * .45;
    v.push(Math.cos(a) * r, Math.sin(a) * r * (.5 + rand(k + 40) * .5));
  }
  m.setFlat(v, n === 3 ? [0, 1, 2] : [0, 1, 2, 0, 2, 3]);
  return m;
});
export function shard(i, x, layer, z, size, rot, colour) { draw(shards[i % 6], x, layer, z, size, size, rot, colour); }

// A limb through points `pts` ({u, h}) with a width per point, as a band in screen space.
// `lit` draws only the east half (from `from` to `to` of the half width) for the sunlit face.
function limbBand(key, S, pts, widths, colour, layer, { grow = 0, lit = false, from = .15, to = .92 } = {}) {
  const P = pts.map(S), a = [], b = [];
  let sign = 1;
  for (let i = 0; i < P.length; i++) {
    const p0 = P[Math.max(0, i - 1)], p1 = P[Math.min(P.length - 1, i + 1)];
    const dx = p1.x - p0.x, dz = p1.z - p0.z, L = Math.hypot(dx, dz);
    if (L < 1e-5) return false;
    const nx = -dz / L, nz = dx / L, w = (widths[i] + grow) / 2;
    if (lit) {
      if (i === 0) sign = nx >= 0 ? 1 : -1;
      a.push({ x: P[i].x + nx * sign * w * to, z: P[i].z + nz * sign * w * to });
      b.push({ x: P[i].x + nx * sign * w * from, z: P[i].z + nz * sign * w * from });
    } else {
      a.push({ x: P[i].x + nx * w, z: P[i].z + nz * w }); b.push({ x: P[i].x - nx * w, z: P[i].z - nz * w });
    }
  }
  band(key, a, b, colour, layer);
  return true;
}

// Wrap bands across a limb: thick curved light strokes every ~0.13 cells, the gaps between them
// staying dark, so the limb reads as wrapped at game zoom.
function wraps(key, S, pts, widths, alpha, layer, spacing = .13) {
  const P = pts.map(S);
  let n = 0;
  for (let i = 0; i + 1 < P.length; i++) {
    const A = P[i], B = P[i + 1], dx = B.x - A.x, dz = B.z - A.z, L = Math.hypot(dx, dz);
    if (L < .03) continue;
    const k = Math.max(1, Math.round(L / spacing)), tx = dx / L, tz = dz / L;
    for (let j = 0; j < k; j++) {
      const t = (j + .5) / k, w = lerp(widths[i], widths[i + 1], t) * .46;
      const c = { x: lerp(A.x, B.x, t), z: lerp(A.z, B.z, t) }, bend = .03, tilt = .025;
      const q = [{ x: c.x - tz * w - tx * tilt, z: c.z + tx * w - tz * tilt }, { x: c.x + tx * bend, z: c.z + tz * bend }, { x: c.x + tz * w + tx * tilt, z: c.z - tx * w + tz * tilt }];
      trail(`${key} w${i} ${j}`, q, .036, Wrap.withAlpha(alpha * (.55 + rand(n * 3 + i) * .3)), layer);
      n++;
    }
  }
}

// One body part: dark outline, body, the sunlit east face, then the wraps.
function part(key, S, pts, widths, layer, tone = 1) {
  if (!limbBand(key + ' e', S, pts, widths, GhostEdge, layer, { grow: .045 })) return;
  limbBand(key + ' b', S, pts, widths, Color.Lerp(GhostEdge, Ghost, tone), layer);
  limbBand(key + ' l', S, pts, widths, Color.Lerp(GhostEdge, GhostLit, tone), layer, { lit: true });
  wraps(key, S, pts, widths, tone, layer);
}

// The point at fractional index f along a polyline.
function polyAt(P, f) {
  const i = Math.min(P.length - 2, Math.max(0, Math.floor(f))), t = f - i;
  return { x: lerp(P[i].x, P[i + 1].x, t), z: lerp(P[i].z, P[i + 1].z, t) };
}

// A filled body between a left and a right edge (screen points, bottom to top): outline, body,
// the east face lit, and `nWraps` bowed bands across it.
function slab(key, Lp, Rp, layer, tone, nWraps) {
  const E = Lp.map((p, i) => (p.x > Rp[i].x ? p : Rp[i])), W = Lp.map((p, i) => (p.x > Rp[i].x ? Rp[i] : p));
  band(`${key} e`, W.map(p => ({ x: p.x - .024, z: p.z })), E.map(p => ({ x: p.x + .024, z: p.z })), GhostEdge, layer);
  band(`${key} b`, W, E, Color.Lerp(GhostEdge, Ghost, tone), layer);
  const mix = (k) => W.map((p, i) => ({ x: lerp(p.x, E[i].x, k), z: lerp(p.z, E[i].z, k) }));
  band(`${key} l`, mix(.56), mix(.94), Color.Lerp(GhostEdge, GhostLit, tone), layer);
  const last = Lp.length - 1;
  for (let j = 0; j < nWraps; j++) {
    const f = (j + .5) / nWraps * Math.min(last, 3), A = polyAt(W, f), B = polyAt(E, f);
    trail(`${key} w${j}`, [A, { x: (A.x + B.x) / 2, z: (A.z + B.z) / 2 - .035 }, B], .038, Wrap.withAlpha(.7 * tone), layer);
  }
}

// Six claws fanning from the wrist along the forearm.
function claws(key, S, elbow, wrist, len, layer, tone = 1) {
  const E = S(elbow), W = S(wrist), dx = W.x - E.x, dz = W.z - E.z, base = Math.atan2(dz, dx);
  for (let i = 0; i < 6; i++) {
    const a = base + (i - 2.5) * .17, l = len * (.8 + (i === 0 || i === 5 ? -.15 : rand(i + 3) * .2));
    const tip = { x: W.x + Math.cos(a) * l, z: W.z + Math.sin(a) * l }, mid = { x: W.x + Math.cos(a) * l * .5, z: W.z + Math.sin(a) * l * .5 };
    const n = { x: -Math.sin(a), z: Math.cos(a) }, w0 = .04, w1 = .024;
    band(`${key} c${i}`, [{ x: W.x + n.x * w0, z: W.z + n.z * w0 }, { x: mid.x + n.x * w1, z: mid.z + n.z * w1 }, tip],
      [{ x: W.x - n.x * w0, z: W.z - n.z * w0 }, { x: mid.x - n.x * w1, z: mid.z - n.z * w1 }, tip], Color.Lerp(GhostEdge, Ghost, tone * .7), layer);
  }
}

// The pose in (u, h) at scale 1 (head top Top = 1.92). facing: 'south' | 'north' | 'east' (west is
// east mirrored by the caller). gait: steps taken (1 = one full cycle), walk 0..1, swipe 0..1 with
// side +1/-1, reach 0..1. Proportions: shoulders about 0.85 wide, so it stands broader than a pawn.
// aim: a direction in (u, h) (the caller converts it from screen space); when set, both arms
// reach along it to hold a gun (or to take one), overriding the walk swing.
export function pose(facing, { gait = 0, walk = 0, swipe = -1, side = 1, reach = 0, aim = null } = {}) {
  const J = {};
  const ph = gait * TAU;
  // Swipe: raise over 0..0.5, strike 0.5..0.68, recover 0.68..1.
  const up = swipe < 0 ? 0 : swipe < .5 ? smooth(swipe / .5) : swipe < .68 ? 1 : 1 - smooth((swipe - .68) / .32);
  const strike = swipe < .5 ? 0 : swipe < .68 ? smooth((swipe - .5) / .18) : 1 - smooth((swipe - .68) / .32);
  if (facing !== 'east') {
    const d = .10 * reach;
    J.legs = [-1, 1].map(s => {
      const lift = walk * Math.max(0, Math.sin(ph + (s < 0 ? Math.PI : 0))) * .12;
      return [{ u: s * .11, h: 1.02 - d }, { u: s * .14, h: .53 + lift }, { u: s * .15, h: .08 + lift * .45 }, { u: s * .16, h: -.01 + lift * .4 }];
    });
    J.torso = { l: [{ u: -.19, h: .92 - d }, { u: -.15, h: 1.09 - d }, { u: -.31, h: 1.29 - d }, { u: -.42, h: 1.39 - d }, { u: -.15, h: 1.50 - d }] };
    J.torso.r = J.torso.l.map(p => ({ u: -p.u, h: p.h }));
    J.arms = [-1, 1].map(s => {
      const sw = walk * Math.sin(ph + (s < 0 ? 0 : Math.PI)) * .05;
      const sh = { u: s * .37, h: 1.35 - d };
      let el = { u: s * .47, h: 1.02 + sw - d }, wr = { u: s * .49, h: .66 + sw - d };
      if (s === 1 && reach > 0) {
        el = { u: lerp(el.u, .42, reach), h: lerp(el.h, .62, reach) };
        wr = { u: lerp(wr.u, .30, reach), h: lerp(wr.h, .16, reach) };
      }
      if (s === side && swipe >= 0) {
        el = { u: lerp(lerp(el.u, s * .55, up), s * .12, strike), h: lerp(lerp(el.h, 1.62, up), 1.00, strike) };
        wr = { u: lerp(lerp(wr.u, s * .38, up), -s * .30, strike), h: lerp(lerp(wr.h, 2.05, up), facing === 'south' ? .30 : .60, strike) };
      }
      if (aim) {
        // Front/back: arms come forward to a two-handed grip in front of the chest, hands
        // together; the gun then points along the true aim from there.
        el = { u: s * .24, h: 1.13 - d }; wr = { u: s * .05, h: 1.04 - d };
      }
      return [sh, el, wr];
    });
    J.neck = [{ u: 0, h: 1.44 - d }, { u: 0, h: 1.58 - d }];
    // Head: a flat shield-shaped plate facing forward (official figure, manga version): narrow
    // at the neck, widest near the top, rounded over the top. Left edge bottom to top; the right
    // edge mirrors it and both meet at the crown.
    J.plate = [{ u: -.045, h: 1.54 }, { u: -.12, h: 1.62 }, { u: -.19, h: 1.74 }, { u: -.22, h: 1.87 }, { u: -.17, h: 1.97 }, { u: 0, h: 2.02 }].map(q => ({ u: q.u, h: q.h - d }));
    return J;
  }
  // Profile, facing +u: a hunched predator. The spine is an S: pelvis back, narrow waist, ribcage
  // pushed forward, neck thrust forward with the funnel tilted forward on it. Knees bent forward,
  // feet flat with a toe. Far limbs sit a little apart from the near ones so the figure has depth:
  // the far leg a step back, the far arm a little forward of the chest. Near leg / arm are index 1.
  const lean = .06 + .12 * reach + .05 * strike, r = reach;
  J.legs = [-1, 1].map(s => {
    const a = ph + (s < 0 ? Math.PI : 0), fu = walk * .30 * Math.sin(a) + (s < 0 ? -.06 : .03), lift = walk * Math.max(0, Math.cos(a)) * .12;
    return [{ u: -.01, h: 1.00 }, { u: fu * .5 + .10, h: .53 + lift }, { u: fu - .03, h: .07 + lift * .45 }, { u: fu + .13, h: lift * .3 }];
  });
  J.torso = {
    back: [{ u: -.17, h: .92 }, { u: -.13, h: 1.07 }, { u: -.16 + lean * .6, h: 1.24 - r * .05 }, { u: -.05 + lean, h: 1.43 - r * .1 }],
    front: [{ u: .13, h: .92 }, { u: .09, h: 1.05 }, { u: .25 + lean * .6, h: 1.21 - r * .05 }, { u: .22 + lean, h: 1.37 - r * .1 }],
  };
  const sh = { u: .07 + lean, h: 1.36 - r * .1 };
  J.arms = [-1, 1].map(s => {
    const sw = walk * Math.sin(ph + (s < 0 ? 0 : Math.PI)), off = s < 0 ? .07 : 0, S0 = { u: sh.u + off, h: sh.h };
    let el = { u: S0.u - .02 + .12 * sw, h: 1.04 - r * .1 }, wr = { u: S0.u + .11 + .24 * sw, h: .70 - r * .1 };
    if (s === 1 && reach > 0) {
      el = { u: lerp(el.u, sh.u + .33, reach), h: lerp(el.h, .70, reach) };
      wr = { u: lerp(wr.u, sh.u + .55, reach), h: lerp(wr.h, .14, reach) };
    }
    if (s === 1 && swipe >= 0) {
      el = { u: lerp(lerp(el.u, sh.u - .15, up), sh.u + .45, strike), h: lerp(lerp(el.h, 1.65, up), 1.22, strike) };
      wr = { u: lerp(lerp(wr.u, sh.u - .03, up), sh.u + .85, strike), h: lerp(lerp(wr.h, 2.05, up), .84, strike) };
    }
    if (aim) {
      // Profile: both arms straight along the aim from the shoulder, the far one a little shorter.
      const L = Math.hypot(aim.u, aim.h) || 1, du = Math.abs(aim.u) / L, dh = aim.h / L, k = s < 0 ? .9 : 1;
      el = { u: S0.u + du * .30 * k, h: S0.h + dh * .30 * k - .03 };
      wr = { u: S0.u + du * .60 * k, h: S0.h + dh * .60 * k };
    }
    return [S0, el, wr];
  });
  const nb = { u: .10 + lean, h: 1.36 - r * .1 }, nt = { u: .23 + lean * 1.2, h: 1.50 - r * .12 };
  J.neck = [nb, nt];
  const f = nt.u, g = nt.h;
  J.funnel = { l: [{ u: f - .07, h: g - .01 }, { u: f - .08, h: g + .13 }, { u: f - .20, h: g + .30 }], r: [{ u: f + .05, h: g }, { u: f + .14, h: g + .13 }, { u: f + .33, h: g + .33 }] };
  J.top = { a: { u: f - .20, h: g + .30 }, b: { u: f + .33, h: g + .33 }, thick: .11 };
  return J;
}

// Draw the ghost. facing 'south' | 'north' | 'east' | 'west'. lo/hi clamp heights (forming and
// dissolving); scale multiplies the whole figure. Returns the screen points of both wrists and the
// segments used for flakes.
export function ghost(key, feet, facing, poseOpts, { lo = 0, hi = 9, scale = 1, layer = pawnLayer, sun, strength = .3, alpha = 1 } = {}) {
  const east = facing === 'east' || facing === 'west', m = facing === 'west' ? -1 : 1;
  const m0 = facing === 'west' ? -1 : 1;
  const aimUH = poseOpts.aimScreen ? { u: poseOpts.aimScreen.x * m0, h: poseOpts.aimScreen.z / Stand } : null;
  const J = pose(east ? 'east' : facing, { ...poseOpts, aim: aimUH });
  const S = q => ({ x: feet.x + q.u * scale * m, z: feet.z + Math.min(hi, Math.max(lo, q.h * scale)) * Stand });
  const shown = clamp((Math.min(hi, Top * scale) - lo) / (Top * scale));
  if (shown <= 0) return null;
  // Shadow: a dark contact patch under the feet, and the figure's cast shadow starting at the feet
  // and running along the sun for its full height (scene.shadowVector is per cell of height).
  const Hs = Top * scale * shown, v = { x: sun.x * Hs, z: sun.z * Hs }, vl = Math.hypot(v.x, v.z);
  sprite({ x: feet.x, z: feet.z - .02 }, .66 * scale, .26 * scale, Body.withAlpha(strength * 2.8 * alpha), soft, shadowLayer);
  if (vl > .01) {
    const ang = -Math.atan2(v.z, v.x) / Mathf.Deg2Rad;
    sprite({ x: feet.x + v.x * .5, z: feet.z + v.z * .5 }, vl + .35 * scale, .5 * scale, Body.withAlpha(strength * 2.1 * alpha), soft, shadowLayer, ang);
  }
  const back = facing === 'north' ? .8 : 1;
  const w = { thigh: east ? [.22, .15, .10, .085] : [.23, .16, .115, .15], arm: [.19, .15, .115], neck: [.16, .15] };
  const segs = [];
  const add = (pts, widths) => { for (let i = 0; i + 1 < pts.length; i++) segs.push({ a: pts[i], b: pts[i + 1], w: widths[i] }); };
  const clawLen = poseOpts.aimScreen ? .2 : .26;   // claws partly closed while gripping

  if (!east) {
    const drawArms = () => J.arms.forEach((a, i) => {
      part(`${key} arm${i}`, S, a, w.arm, layer, back);
      if (facing === 'south') claws(`${key} claw${i}`, S, a[1], a[2], clawLen, layer, back);
      add(a, w.arm);
    });
    J.legs.forEach((l, i) => { part(`${key} leg${i}`, S, l, w.thigh, layer, back * .92); add(l, w.thigh); });
    if (facing === 'north') { J.arms.forEach((a, i) => claws(`${key} claw${i}`, S, a[1], a[2], clawLen, layer, back * .8)); drawArms(); }
    const tl = J.torso.l.map(S), tr = J.torso.r.map(S);
    if (tl[tl.length - 1].z - tl[0].z > 1e-4) {
      slab(`${key} torso`, tl, tr, layer, back, 4);
      const P = q => S(q), line = (k, pts, wd, c) => trail(`${key} ${k}`, pts.map(P), wd, c, layer);
      if (facing === 'north') {
        // Back: a groove down the spine and two shoulder-blade curves.
        line('spine', [{ u: 0, h: .95 }, { u: .01, h: 1.12 }, { u: 0, h: 1.30 }, { u: 0, h: 1.47 }], .035, GhostEdge.withAlpha(.9));
        [-1, 1].forEach(sg => line('blade' + sg, [{ u: sg * .06, h: 1.36 }, { u: sg * .17, h: 1.30 }, { u: sg * .20, h: 1.18 }], .03, Wrap.withAlpha(.55)));
      } else {
        // Front: the chest line under each pectoral and a short sternum line.
        [-1, 1].forEach(sg => line('pec' + sg, [{ u: sg * .26, h: 1.31 }, { u: sg * .14, h: 1.23 }, { u: sg * .02, h: 1.24 }], .032, GhostEdge.withAlpha(.85)));
        line('sternum', [{ u: 0, h: 1.23 }, { u: 0, h: 1.33 }, { u: 0, h: 1.44 }], .026, GhostEdge.withAlpha(.7));
      }
    }
    segs.push({ a: J.torso.l[0], b: J.torso.l[3], w: .06 }, { a: J.torso.r[0], b: J.torso.r[3], w: .06 });
    if (facing !== 'north') drawArms();
    part(`${key} neck`, S, J.neck, w.neck, layer, back);
  } else {
    // Far limbs first and darker, then torso, near leg, neck and head, near arm last.
    part(`${key} leg0`, S, J.legs[0], w.thigh, layer, .68); add(J.legs[0], w.thigh);
    part(`${key} arm0`, S, J.arms[0], w.arm, layer, .68); claws(`${key} claw0`, S, J.arms[0][1], J.arms[0][2], clawLen, layer, .68);
    const tb = J.torso.back.map(S), tf = J.torso.front.map(S);
    if (tb[2].z - tb[0].z > 1e-4) slab(`${key} torso`, tb, tf, layer, 1, 4);
    segs.push({ a: J.torso.back[0], b: J.torso.back[2], w: .06 }, { a: J.torso.front[0], b: J.torso.front[2], w: .06 });
    part(`${key} leg1`, S, J.legs[1], w.thigh, layer); add(J.legs[1], w.thigh);
    part(`${key} neck`, S, J.neck, w.neck, layer);
  }
  add(J.neck, w.neck);

  if (J.plate) {
    // Plate head, front or back. Front: pale rim, dark smooth face. Back: plain back of the plate,
    // lit on the east side like the body.
    const L0 = J.plate, R0 = L0.map(q => ({ u: -q.u, h: q.h }));
    const Lp = L0.map(S), Rp = R0.map(S);
    if (Lp[Lp.length - 1].z - Lp[0].z > 1e-4) {
      const c = { x: (Lp[2].x + Rp[2].x) / 2, z: (Lp[2].z + Rp[3].z) / 2 };
      const shrink = (P, k) => P.map(p => ({ x: c.x + (p.x - c.x) * k, z: c.z + (p.z - c.z) * k }));
      const grow = (P, k) => shrink(P, k);
      band(`${key} plate e`, grow(Lp, 1.10), grow(Rp, 1.10), GhostEdge, layer);
      if (facing === 'north') {
        slab(`${key} plate`, Lp, Rp, layer, back, 0);
      } else {
        band(`${key} plate rim`, Lp, Rp, Color.Lerp(Ghost, GhostLit, .8), layer);
        band(`${key} plate face`, shrink(Lp, .84), shrink(Rp, .84), PlateFace, layer);
        band(`${key} plate sheen`, shrink(Lp.slice(2), .62).map(p => ({ x: p.x + .02, z: p.z + .015 })), shrink(Rp.slice(2), .62).map(p => ({ x: p.x + .02, z: p.z + .015 })), Color.Lerp(PlateFace, GhostLit, .22), layer);
      }
    }
    segs.push({ a: J.plate[1], b: J.plate[4], w: .05 }, { a: { u: -J.plate[1].u, h: J.plate[1].h }, b: { u: -J.plate[4].u, h: J.plate[4].h }, w: .05 });
  } else {
  // Side head: the plate seen at an angle, a wedge flaring from the neck with its flat top edge.
  const fl = J.funnel.l.map(S), fr = J.funnel.r.map(S);
  if (fl[2].z - fl[0].z > 1e-4) {
    slab(`${key} funnel`, fl, fr, layer, back, 0);
    for (let j = 0; j < 4; j++) {
      const A = polyAt(fl, (j + .8) / 4.4 * 2), B = polyAt(fr, (j + .8) / 4.4 * 2);
      trail(`${key} fw${j}`, [A, { x: (A.x + B.x) / 2, z: (A.z + B.z) / 2 - .02 - .012 * j }, B], .03, Wrap.withAlpha(.6 * back), layer);
    }
    const a = S(J.top.a), b = S(J.top.b), cx = (a.x + b.x) / 2, cz = (a.z + b.z) / 2, len = Math.hypot(b.x - a.x, b.z - a.z);
    const ang = -Math.atan2(b.z - a.z, b.x - a.x) / Mathf.Deg2Rad, th = J.top.thick * scale * Stand;
    draw(disc, cx, layer, cz, len / 2 + .03, th / 2 + .028, ang, GhostEdge);
    draw(disc, cx, layer, cz, len / 2, th / 2, ang, Color.Lerp(Ghost, GhostLit, .5 * back));
    draw(disc, cx + .012, layer, cz + .004, len / 2 * .8, th / 2 * .5, ang, Color.Lerp(Ghost, GhostLit, .3));
  }
  segs.push({ a: J.funnel.l[0], b: J.funnel.l[2], w: .05 }, { a: J.funnel.r[0], b: J.funnel.r[2], w: .05 }, { a: J.top.a, b: J.top.b, w: .04 });
  }

  if (east) {
    part(`${key} arm1`, S, J.arms[1], w.arm, layer);
    claws(`${key} claw1`, S, J.arms[1][1], J.arms[1][2], clawLen, layer);
    add(J.arms[0], w.arm); add(J.arms[1], w.arm);
  }
  return { wrists: J.arms.map(a => S(a[2])), elbows: J.arms.map(a => S(a[1])), segs, S, shown };
}

// Black matter coming off the figure's edges and drifting up: hard flakes plus soft smoke puffs,
// thick enough to read as the source's particle cloud at game zoom. `segs` from ghost(); heights
// outside [lo, hi] are skipped. Every flake is a function of time: it respawns each `life` seconds
// at a new place picked by hash.
export function flakes(key, feet, segs, t, { amount = 1, lo = 0, hi = 9, scale = 1, mirror = 1, layer = Y + .02, alpha = 1 } = {}) {
  if (!segs || !segs.length) return;
  const N = Math.round(78 * amount);
  for (let i = 0; i < N; i++) {
    const life = .8 + rand(i * 3.1) * .7, ph = t / life + rand(i * 7.3), cyc = Math.floor(ph), u = ph - cyc, sd = i * 131 + cyc * 17;
    const sg = segs[Math.floor(rand(sd) * segs.length) % segs.length], tt = rand(sd + 1), side = rand(sd + 2) < .5 ? -1 : 1;
    const h0 = lerp(sg.a.h, sg.b.h, tt) * scale;
    if (h0 < lo || h0 > hi) continue;
    const du = sg.b.u - sg.a.u, dh = sg.b.h - sg.a.h, L = Math.hypot(du, dh) || 1;
    const u0 = (lerp(sg.a.u, sg.b.u, tt) + (-dh / L) * side * sg.w * .5) * scale;
    const smoke = i % 3 === 0;
    const drift = { u: (-dh / L) * side * .14 * u + (rand(sd + 3) - .5) * .35 * u + .12 * u, h: (.45 + rand(sd + 4) * .5) * u * (smoke ? 1.3 : 1) };
    const x = feet.x + (u0 + drift.u) * mirror, z = feet.z + (h0 + drift.h) * Stand;
    if (smoke) {
      const s = (.16 + .14 * rand(sd + 7)) * (1 + u * .8) * scale;
      sprite({ x, z }, s, s * .8, Flake.withAlpha(Math.sin(Math.min(1, u * 1.4) * Math.PI) * .42 * alpha), puff, layer - .002);
      continue;
    }
    const size = (.045 + .06 * rand(sd + 5)) * (1 - u * .5) * scale;
    shard(i, x, layer + i * .0002, z, size, rand(sd + 6) * 360 + u * 260, Flake.withAlpha((1 - u) * .95 * alpha));
  }
}

// A dense line of flakes on a horizontal edge at height h (the forming or dissolving edge),
// `width` cells wide, thrown upward.
export function edgeFlakes(key, feet, h, width, t, { amount = 1, rise = .5, layer = Y + .03, alpha = 1 } = {}) {
  const N = Math.round(26 * amount);
  for (let i = 0; i < N; i++) {
    const life = .45 + rand(i * 5.7 + 1) * .35, ph = t / life + rand(i * 2.9 + 4), cyc = Math.floor(ph), u = ph - cyc, sd = i * 97 + cyc * 29 + 5;
    const u0 = (rand(sd) - .5) * width, lift = (rise * (.5 + rand(sd + 1) * .7)) * u, side = (rand(sd + 2) - .5) * .5 * u;
    const x = feet.x + u0 + side, z = feet.z + (h + lift) * Stand;
    const size = (.05 + .06 * rand(sd + 3)) * (1 - u * .5);
    shard(i + 3, x, layer + i * .0002, z, size, rand(sd + 4) * 360 + u * 300, Flake.withAlpha((1 - u) * alpha));
    if (i % 3 === 0) sprite({ x, z }, .26 * (1 - u * .3), .20 * (1 - u * .3), Flake.withAlpha((1 - u) * .38 * alpha), puff, layer - .001);
  }
}

// Ooze: black strands pouring from `from` (screen point) to `to` (screen point) with `n` strands
// spread `spread` cells at the far end. `k` 0..1 is how far the stream has travelled, `thick` the
// strand width.
export function ooze(key, from, to, t, { n = 6, spread = .35, k = 1, thick = .07, arc = .45, alpha = 1, layer = Y + .01 } = {}) {
  for (let i = 0; i < n; i++) {
    const off = (i / (n - 1) - .5) * spread, wig = Math.sin(t * 9 + i * 1.7) * .04;
    const A = { x: from.x + (rand(i + 61) - .5) * .12, z: from.z + (rand(i + 62) - .5) * .08 };
    const B = { x: to.x + off, z: to.z + (rand(i + 63) - .5) * .08 };
    // Bow each strand sideways off the line from A to B, alternating sides, so they read as
    // separate tendrils and not one column.
    const dx = B.x - A.x, dz = B.z - A.z, L = Math.hypot(dx, dz) || 1, side = (i % 2 ? 1 : -1) * (.22 + rand(i + 67) * .38) * Math.min(1, L);
    const C = { x: (A.x + B.x) / 2 - dz / L * side + wig, z: (A.z + B.z) / 2 + dx / L * side + arc * (.2 + rand(i + 64) * .4) };
    const pts = [];
    const steps = 12, end = clamp(k * (1.1 - rand(i + 65) * .2));
    if (end <= .02) continue;
    for (let s = 0; s <= steps; s++) {
      const q = s / steps * end, a = (1 - q) * (1 - q), b = 2 * (1 - q) * q, c = q * q;
      pts.push({ x: a * A.x + b * C.x + c * B.x + Math.sin(q * 12 + t * 7 + i) * .015, z: a * A.z + b * C.z + c * B.z });
    }
    trail(`${key} o${i}`, pts, thick * (.7 + rand(i + 66) * .6), Flake.withAlpha(.9 * alpha), layer + i * .0003);
    const tip = pts[pts.length - 1];
    sprite(tip, .13, .10, Flake.withAlpha(.5 * alpha), puff, layer + .002);
  }
}

// Stand-in pawns at the real pawn's size (1.3x the lab's two-disc stand-in, 0.33 lower).
export function standIn(pos, body, sun, strength, { cap = null, downed = false, alpha = 1, layer = pawnLayer, turn = 0 } = {}) {
  const s = 1.3, o = -.33;
  sprite({ x: pos.x + sun.x * .5, z: pos.z + o + .05 + sun.z * .5 }, .95, .42, Body.withAlpha(strength * alpha * 1.4), soft, shadowLayer);
  if (downed) {
    draw(disc, pos.x + .05 * s, layer, pos.z + o + .15, .32 * s, .20 * s, turn, body.withAlpha(alpha));
    draw(disc, pos.x - .30 * s, layer, pos.z + o + .17, .16 * s, .16 * s, 0, Skin.withAlpha(alpha));
    return;
  }
  draw(disc, pos.x, layer, pos.z + o + .18 * s, .22 * s, .32 * s, 0, body.withAlpha(alpha));
  draw(disc, pos.x, layer, pos.z + o + .58 * s, .16 * s, .17 * s, 0, Skin.withAlpha(alpha));
  if (cap) {
    draw(disc, pos.x, layer, pos.z + o + .66 * s, .165 * s, .10 * s, 0, cap.withAlpha(alpha));
    draw(disc, pos.x, layer, pos.z + o + .60 * s, .17 * s, .035 * s, 0, cap.withAlpha(alpha));
  }
}

// A severed hand lying on the floor or held: palm, four finger nubs and a thumb, a red stump.
export function hand(pos, rot, layer, alpha = 1) {
  const a = rot * Mathf.Deg2Rad, c = Math.cos(a), s = Math.sin(a), at = (x, z) => ({ x: pos.x + x * c - z * s, z: pos.z + x * s + z * c });
  const st = at(-.075, 0);
  draw(disc, st.x, layer, st.z, .04, .045, -rot, Blood.withAlpha(alpha));
  draw(disc, pos.x, layer + .0005, pos.z, .075, .05, -rot, Skin.withAlpha(alpha));
  for (let i = 0; i < 4; i++) { const f = at(.085, (i - 1.5) * .024); draw(disc, f.x, layer + .001, f.z, .032, .011, -rot, Skin.withAlpha(alpha)); }
  const th = at(.02, .058); draw(disc, th.x, layer + .001, th.z, .026, .012, -rot + 40, Skin.withAlpha(alpha));
}

// The real-size stand-in between lying (k = 0) and standing (k = 1): body and head discs move
// and turn from the downed layout to the standing one. Returns the body and head ellipses so an
// effect can cover them.
export function standInBlend(pos, body, k, sun, strength, { cap = null, alpha = 1, layer = pawnLayer, draw: doDraw = true } = {}) {
  const s = 1.3, o = -.33, e = smooth(clamp(k));
  const B = { x: pos.x + lerp(.05 * s, 0, e), z: pos.z + o + lerp(.15, .18 * s, e), rx: lerp(.32 * s, .22 * s, e), rz: lerp(.20 * s, .32 * s, e) };
  const H = { x: pos.x + lerp(-.30 * s, 0, e), z: pos.z + o + lerp(.17, .58 * s, e), rx: .16 * s, rz: lerp(.16 * s, .17 * s, e) };
  if (!doDraw) return { B, H };
  sprite({ x: pos.x + sun.x * .5, z: pos.z + o + .05 + sun.z * .5 }, .95, .42, Body.withAlpha(strength * alpha * 1.4), soft, shadowLayer);
  draw(disc, B.x, layer, B.z, B.rx, B.rz, 0, body.withAlpha(alpha));
  draw(disc, H.x, layer, H.z, H.rx, H.rz, 0, Skin.withAlpha(alpha));
  if (cap && e > .6) {
    const a = alpha * clamp((e - .6) / .4);
    draw(disc, H.x, layer, H.z + .08 * s, .165 * s, .10 * s, 0, cap.withAlpha(a));
    draw(disc, H.x, layer, H.z + .02 * s, .17 * s, .035 * s, 0, cap.withAlpha(a));
  }
  return { B, H };
}

// A standing pawn's outline filled with black matter, drawn from its feet up to screen height
// `hi` (cells above pos.z; the full figure spans Shell.lo..Shell.hi). Rows are a band between a
// left and a right edge, so the shell can build up from the feet and peel from the head down.
// Light band lines across it tie it to the ghost's wrapped look.
export const Shell = { lo: -.55, hi: .67 };
export function shell(key, pos, hi, layer, alpha = 1) {
  const top = Math.min(hi, Shell.hi);
  if (top <= Shell.lo + .01) return;
  const width = z => {
    const b = (z + .096) / .45, h = (z - .424) / .25;
    const wb = Math.abs(b) < 1 ? .31 * Math.sqrt(1 - b * b) : 0, wh = Math.abs(h) < 1 ? .235 * Math.sqrt(1 - h * h) : 0;
    return Math.max(wb, wh, .03);
  };
  const L = [], R = [], Le = [], Re = [], n = 22;
  for (let i = 0; i <= n; i++) {
    const z = lerp(Shell.lo, top, i / n), w = width(z);
    L.push({ x: pos.x - w, z: pos.z + z }); R.push({ x: pos.x + w, z: pos.z + z });
    Le.push({ x: pos.x - w - .03, z: pos.z + z }); Re.push({ x: pos.x + w + .03, z: pos.z + z });
  }
  band(`${key} e`, Le, Re, GhostEdge.withAlpha(alpha), layer);
  band(`${key} b`, L, R, Ghost.withAlpha(alpha), layer);
  band(`${key} l`, L.map((p, i) => ({ x: lerp(p.x, R[i].x, .58), z: p.z })), R.map((p, i) => ({ x: lerp(p.x, R[i].x, .06), z: p.z })), GhostLit.withAlpha(alpha * .8), layer);
  for (let z = Shell.lo + .08, j = 0; z < top - .03; z += .11, j++) {
    const w = width(z) * .92;
    trail(`${key} w${j}`, [{ x: pos.x - w, z: pos.z + z }, { x: pos.x, z: pos.z + z - .03 }, { x: pos.x + w, z: pos.z + z }], .034, Wrap.withAlpha(.6 * alpha), layer);
  }
}

// A thin ring filling clockwise from the top: `frac` 0..1.
export function arc(key, c, r, frac, w, colour, layer) {
  if (frac <= .005) return;
  const n = Math.max(2, Math.round(48 * frac)), A = [], B = [];
  for (let i = 0; i <= n; i++) {
    const a = Math.PI / 2 - frac * TAU * i / n;
    A.push({ x: c.x + Math.cos(a) * (r + w / 2), z: c.z + Math.sin(a) * (r + w / 2) });
    B.push({ x: c.x + Math.cos(a) * (r - w / 2), z: c.z + Math.sin(a) * (r - w / 2) });
  }
  band(key, A, B, colour, layer);
}


export { Body, Y, Floor, Lift, sprite, band, trail, soft, rand, smooth, clamp, lerp, TAU, draw };
