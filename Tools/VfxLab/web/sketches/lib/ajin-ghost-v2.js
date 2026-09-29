// Satō's Black Ghost, v2 figure (proposed 2026-09-29, not agreed). The look only: the mechanic is
// the Black Ghost sketch's. What changes from ghost() in lib/ajin.js:
// - Head: Satō's IBM is a reptile (Ajin wiki: a cobra-like head, a triangle seen from above and
//   flat from the side; the only IBM with a working mouth, a long forked tongue and saliva; a fan
//   write-up reads it as an alligator's snout with sharp teeth). Front: a flat hood-shaped triangle
//   pointing down at the snout. Back: the same triangle pointing up (snout away), plain, with a
//   groove. Side: a thin flat wedge with a long snout.
// - Mouth: the lower jaw hinges open (front: a V drops under the snout; side: it swings down up to
//   31 degrees) and shows a dark maroon inside, pale teeth on both jaws and saliva strands past half
//   open. The forked tongue flicks out.
// - Body: a black skeleton in bandages (wiki): darker body, thinner limbs, narrow waist, wraps that
//   spiral at uneven spacing with some broken off so the black shows between them, X straps across
//   the chest, and loose bandage ends that hang, trail behind while it walks and ripple.
// - Stance: hunched, the head low between high shoulders (front) or thrust forward (side), arms to
//   the knees with six long curled claws. Idle: the head sways side to side over 2.6 s, the claws
//   flex, the chest breathes. Walk: a long loping stride with a bob and the arms swinging low.
//   Swipe: the head lunges with the jaw open.
// - Fraying (its time running out): `fray` 0..1 thins the limbs by up to 15 %, adds two more loose
//   ends and lengthens them all by up to 60 % (the wraps unravel). The sketch also raises the
//   flake rate and throws off bigger chunks with chunks() below.
//
// Drawing: the same method as ghost(): a joint at (u, h) is drawn at feet.x + u, feet.z + h * Stand,
// upright in the screen plane like a pawn sprite; west is east mirrored. The head has its own draw
// method per facing (front, back, side), each a local shape turned by the head's tilt about its
// base. The mouth, tongue and teeth colours are guesses: no source frame was checked for them.
import { Color, Mathf } from '../../js/engine.js';
import { Body, band, trail, sprite, soft, rand } from './six-paths-impact.js';
import { pawnLayer, shadowLayer, shard, Flake, Stand, limbBand, polyAt } from './ajin.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
export const Ink = new Color(.10, .10, .12), InkEdge = new Color(.02, .02, .025), InkLit = new Color(.21, .21, .24);
export const Bandage = new Color(.52, .52, .56), LooseBandage = new Color(.33, .33, .37);
export const Maw = new Color(.30, .05, .06), Tongue = new Color(.62, .13, .15), Teeth = new Color(.90, .89, .84), Spit = new Color(.86, .88, .92);
export const Top2 = 1.87;   // head top at scale 1 (front), in cells of height
const TonguePeriod = 2.6, FlickLen = .34, SwayPeriod = 2.6, ClawLen = .34, JawSwing = .55;

// Head shapes in the head's own (u, h), origin at the top of the neck.
// Front: left edge from the blunt snout (over the upper chest) up to the middle of the back edge;
// the right edge mirrors it. The head covers the top of the neck, so it does not sit on it like a
// funnel on a stick (the first try did).
const FrontEdge = [{ u: -.035, h: -.11 }, { u: -.09, h: -.02 }, { u: -.16, h: .08 }, { u: -.24, h: .17 }, { u: -.29, h: .24 }, { u: -.26, h: .30 }, { u: -.16, h: .34 }, { u: 0, h: .355 }];
// Back: from the back of the skull (low, nearest the viewer) up to the snout tip (far).
const BackEdge = [{ u: -.05, h: -.10 }, { u: -.19, h: -.02 }, { u: -.28, h: .07 }, { u: -.25, h: .14 }, { u: -.15, h: .24 }, { u: -.06, h: .32 }, { u: 0, h: .36 }];
// Front mouth: the upper jaw's lower edge, left corner to snout tip to right corner, and how far
// each point of the lower jaw drops when fully open.
const MouthFront = [{ u: -.15, h: .06 }, { u: -.09, h: -.02 }, { u: 0, h: -.115 }, { u: .09, h: -.02 }, { u: .15, h: .06 }], DropFront = [0, .09, .17, .09, 0];
// Side, snout toward +u: the top edge and the mouth line, paired by index; the last pair is the
// blunt end of the snout (a long thin point read as a beak in the first try).
const SideTop = [{ u: -.08, h: .07 }, { u: .00, h: .13 }, { u: .10, h: .125 }, { u: .22, h: .095 }, { u: .32, h: .06 }, { u: .39, h: .04 }, { u: .43, h: .02 }];
const SideMouth = [{ u: -.08, h: -.07 }, { u: .00, h: -.06 }, { u: .10, h: -.05 }, { u: .22, h: -.036 }, { u: .32, h: -.022 }, { u: .39, h: -.014 }, { u: .43, h: -.004 }];
// The lower jaw, hinged at its first point.
const JawTop = [{ u: .00, h: -.06 }, { u: .10, h: -.052 }, { u: .22, h: -.04 }, { u: .32, h: -.028 }, { u: .40, h: -.018 }];
const JawBottom = [{ u: .00, h: -.13 }, { u: .10, h: -.12 }, { u: .22, h: -.095 }, { u: .32, h: -.06 }, { u: .40, h: -.03 }];
const Hinge = JawTop[0];

// Tongue flick envelope 0..1: out in 0.12 s, held 0.1 s, back in 0.12 s, every 2.6 s from `start`.
export function flick(t, start = 1.6) {
  if (t < start) return 0;
  const ph = Mathf.Repeat(t - start, TonguePeriod);
  if (ph > FlickLen) return 0;
  return ph < .12 ? smooth(ph / .12) : ph < .22 ? 1 : 1 - smooth((ph - .22) / .12);
}

// A strip through screen points whose width runs from w0 to w1.
function taper(key, pts, w0, w1, colour, layer) {
  const a = [], b = [], n = pts.length - 1;
  pts.forEach((p, i) => {
    const q = pts[Math.min(n, i + 1)], r = pts[Math.max(0, i - 1)], dx = q.x - r.x, dz = q.z - r.z, L = Math.hypot(dx, dz) || 1, w = lerp(w0, w1, i / n) / 2;
    a.push({ x: p.x - dz / L * w, z: p.z + dx / L * w }); b.push({ x: p.x + dz / L * w, z: p.z - dx / L * w });
  });
  band(key, a, b, colour, layer);
}

// Bandage wraps along a limb: bands at uneven spacing (0.085-0.205 cells), most tilted one way so
// they spiral, a few the other way so they cross, about half of them broken off short of an edge.
function wraps2(key, S, pts, widths, tone, layer, seed) {
  const P = pts.map(S);
  let n = 0;
  for (let i = 0; i + 1 < P.length; i++) {
    const A = P[i], B = P[i + 1], dx = B.x - A.x, dz = B.z - A.z, L = Math.hypot(dx, dz);
    if (L < .03) continue;
    const tx = dx / L, tz = dz / L, nx = -tz, nz = tx;
    for (let s = rand(seed + i * 7) * .08; s < L; s += .085 + rand(seed + n * 13 + 5) * .12, n++) {
      const r1 = rand(seed + n * 13 + 1), r2 = rand(seed + n * 13 + 2), r3 = rand(seed + n * 13 + 3), r4 = rand(seed + n * 13 + 4);
      const f = s / L, w = lerp(widths[i], widths[Math.min(i + 1, widths.length - 1)], f) * .5, c = { x: A.x + dx * f, z: A.z + dz * f };
      const tilt = (r1 - .25) * w * 1.3, a0 = r2 < .25 ? r2 * 1.6 : 0, a1 = r3 < .25 ? .6 + r3 * 1.6 : 1;
      const at = k => { const x = lerp(-1, 1, k); return { x: c.x + nx * w * .95 * x + tx * tilt * x, z: c.z + nz * w * .95 * x + tz * tilt * x }; };
      const m = at((a0 + a1) / 2);
      trail(`${key} w${n}`, [at(a0), { x: m.x - tx * .02, z: m.z - tz * .02 }, at(a1)], .026 + r4 * .02, Bandage.withAlpha(tone * (.45 + r4 * .35)), layer);
    }
  }
}

// One limb: dark outline, body, the sunlit east face, then the wraps.
function part2(key, S, pts, widths, layer, tone, seed) {
  if (!limbBand(key + ' e', S, pts, widths, InkEdge, layer, { grow: .04 })) return;
  limbBand(key + ' b', S, pts, widths, Color.Lerp(InkEdge, Ink, tone), layer);
  limbBand(key + ' l', S, pts, widths, Color.Lerp(InkEdge, InkLit, tone), layer, { lit: true, from: .2, to: .85 });
  wraps2(key, S, pts, widths, tone, layer, seed);
}

// The torso between two screen edges (bottom to top): outline, body, lit east side, two straps
// crossing over the chest and broken bands round the waist and ribs.
function torso2(key, Lp, Rp, layer, tone) {
  const E = Lp.map((p, i) => (p.x > Rp[i].x ? p : Rp[i])), W = Lp.map((p, i) => (p.x > Rp[i].x ? Rp[i] : p));
  if (E[E.length - 1].z - E[0].z < 1e-4) return;
  band(`${key} e`, W.map(p => ({ x: p.x - .024, z: p.z })), E.map(p => ({ x: p.x + .024, z: p.z })), InkEdge, layer);
  band(`${key} b`, W, E, Color.Lerp(InkEdge, Ink, tone), layer);
  const mix = k => W.map((p, i) => ({ x: lerp(p.x, E[i].x, k), z: lerp(p.z, E[i].z, k) }));
  band(`${key} l`, mix(.58), mix(.94), Color.Lerp(InkEdge, InkLit, tone), layer);
  const top = Math.min(W.length - 1, 3) - .15;
  const at = (f, k) => { const A = polyAt(W, f), B = polyAt(E, f); return { x: lerp(A.x, B.x, k), z: lerp(A.z, B.z, k) }; };
  trail(`${key} x1`, [at(top, .08), at(top * .55, .5), at(.35, .9)], .05, Bandage.withAlpha(.72 * tone), layer);
  trail(`${key} x2`, [at(top, .92), at(top * .5, .5), at(.2, .12)], .045, Bandage.withAlpha(.58 * tone), layer);
  [[.25, .05, .8], [.75, .3, 1], [1.35, 0, .55], [1.8, .45, .98]].forEach(([f, a, b], j) => {
    const A = at(f, a), B = at(f, b), M = at(f, (a + b) / 2);
    trail(`${key} w${j}`, [A, { x: M.x, z: M.z - .03 }, B], .034, Bandage.withAlpha((.45 + .12 * (j % 3)) * tone), layer);
  });
}

// Six long claws fanning from the wrist, each curling in toward the middle of the hand. `flex`
// -1..1 opens and closes the fan by 30 %.
function claws2(key, S, elbow, wrist, len, flex, layer, tone = 1) {
  const E = S(elbow), W = S(wrist), base = Math.atan2(W.z - E.z, W.x - E.x), spread = .15 * (1 + .3 * flex);
  for (let i = 0; i < 6; i++) {
    const a = base + (i - 2.5) * spread, l = len * (.82 + (i === 0 || i === 5 ? -.18 : rand(i + 3) * .18));
    const mid = { x: W.x + Math.cos(a) * l * .55, z: W.z + Math.sin(a) * l * .55 }, b = a - Math.sign(i - 2.5) * .35;
    const tip = { x: mid.x + Math.cos(b) * l * .45, z: mid.z + Math.sin(b) * l * .45 };
    taper(`${key} c${i} e`, [W, mid, tip], .068, .012, InkEdge, layer);
    taper(`${key} c${i}`, [W, mid, tip], .046, .004, Color.Lerp(InkEdge, Ink, tone), layer);
  }
}

// Teeth along a jaw edge (screen points from (u, h) `line`): small pale triangles, pointing to the
// side `dir` (+1 left of the line's direction on screen, -1 right).
function teeth(key, S, line, dir, size, alpha, layer) {
  const P = line.map(S);
  let k = 0;
  for (let i = 0; i + 1 < P.length; i++) {
    const A = P[i], B = P[i + 1], dx = B.x - A.x, dz = B.z - A.z, L = Math.hypot(dx, dz);
    if (L < .01) continue;
    const m = Math.max(1, Math.round(L / (size * 1.1))), nx = -dz / L * dir, nz = dx / L * dir;
    for (let j = 0; j < m; j++, k++) {
      const f0 = j / m, f1 = (j + 1) / m, fm = (f0 + f1) / 2, len = size * (.8 + rand(k * 5 + i) * .5);
      const tip = { x: A.x + dx * fm + nx * len, z: A.z + dz * fm + nz * len };
      band(`${key} t${k}`, [{ x: A.x + dx * f0, z: A.z + dz * f0 }, tip], [{ x: A.x + dx * f1, z: A.z + dz * f1 }, tip], Teeth.withAlpha(alpha), layer);
    }
  }
}

// The forked tongue: from `root` along angle `ang` (head-local (u, h)), `len` long, forked at 74 %,
// wriggling. `map` takes a head-local point to the screen.
function tongue(key, map, root, ang, len, t, layer, tone) {
  if (len < .01) return;
  const du = Math.cos(ang), dh = Math.sin(ang);
  const at = (s, off) => map({ u: root.u + du * s - dh * off, h: root.h + dh * s + du * off });
  const fork = len * .74, wig = s => Math.sin(s * 14 - t * 20) * .012 * (s / len);
  const main = [0, 1, 2, 3, 4, 5].map(i => at(fork * i / 5, wig(fork * i / 5)));
  taper(`${key} tongue e`, main, .07, .05, InkEdge.withAlpha(tone), layer);
  taper(`${key} tongue`, main, .05, .03, Tongue.withAlpha(tone), layer);
  [-1, 1].forEach(sg => {
    const pts = [main[5], at((fork + len) / 2, wig(fork) + sg * len * .04), at(len, wig(fork) + sg * len * .1)];
    taper(`${key} tine${sg} e`, pts, .042, .016, InkEdge.withAlpha(tone), layer);
    taper(`${key} tine${sg}`, pts, .026, .006, Tongue.withAlpha(tone), layer);
  });
}

// A saliva strand between screen points A and B, sagging `sag` cells.
function spit(key, A, B, sag, alpha, layer) {
  const pts = [0, .25, .5, .75, 1].map(f => ({ x: lerp(A.x, B.x, f) + Math.sin(f * Math.PI) * sag * .4, z: lerp(A.z, B.z, f) - Math.sin(f * Math.PI) * sag }));
  trail(key, pts, .018, Spit.withAlpha(alpha), layer);
}

// A loose bandage end: from screen point `root` along unit `dir`, `len` long, a wave running down it
// and a slow curl to one side (so it hangs like cloth, not a straight stick).
function looseEnd(key, root, dir, len, w, t, phase, amp, colour, layer) {
  const nx = -dir.z, nz = dir.x, pts = [], curl = .10 * Math.sin(t * .9 + phase * 1.7) * len;
  for (let i = 0; i <= 8; i++) {
    const s = i / 8, off = amp * s * Math.sin(s * 5.5 - t * 7 + phase) + curl * s * s;
    pts.push({ x: root.x + dir.x * len * s + nx * off, z: root.z + dir.z * len * s + nz * off });
  }
  taper(`${key} e`, pts, w + .025, w * .45 + .02, InkEdge.withAlpha(colour.a), layer);
  taper(key, pts, w, w * .45, colour, layer);
}

// The pose in (u, h) at scale 1. facing 'south' | 'north' | 'east' (west = east mirrored by the
// caller). gait: steps taken; walk 0..1; swipe 0..1 with side +1/-1; reach 0..1 bends down to the
// floor with the near (index 1) hand; t: clip time for the idle motion; jaw 0..1 and tongue 0..1
// from the caller (the swipe opens the jaw by itself).
export function pose2(facing, { gait = 0, walk = 0, swipe = -1, side = 1, reach = 0, t = 0, jaw = 0, tongue: tg = 0 } = {}) {
  const J = {}, ph = gait * TAU, idle = 1 - walk, busy = swipe >= 0 ? 1 : 0;
  // Swipe: raise over 0..0.5, strike 0.5..0.68, recover 0.68..1.
  const up = swipe < 0 ? 0 : swipe < .5 ? smooth(swipe / .5) : swipe < .68 ? 1 : 1 - smooth((swipe - .68) / .32);
  const strike = swipe < .5 ? 0 : swipe < .68 ? smooth((swipe - .5) / .18) : 1 - smooth((swipe - .68) / .32);
  const sway = idle * (1 - busy) * Math.sin(t * TAU / SwayPeriod);
  const d = idle * Math.sin(t * 2.3) * .012 + walk * (Math.abs(Math.sin(ph)) - .5) * .05 - .10 * reach;   // breathing, stride bob, bending down
  J.jaw = Math.max(jaw, up * .45 + strike * .55);
  J.tongue = busy ? 0 : tg;
  J.flex = idle * Math.sin(t * 3.1);
  if (facing !== 'east') {
    const hs = walk * Math.sin(ph) * .03;   // weight shifting side to side
    J.legs = [-1, 1].map(s => {
      const lift = walk * Math.max(0, Math.sin(ph + (s < 0 ? Math.PI : 0))) * .14;
      return [{ u: s * .10 + hs * .5, h: .95 + d * .5 }, { u: s * .19, h: .52 + lift }, { u: s * .15, h: .09 + lift * .5 }, { u: s * .20, h: -.01 + lift * .4 }];
    });
    J.torso = { l: [{ u: -.12, h: .91 }, { u: -.10, h: 1.04 }, { u: -.26, h: 1.22 }, { u: -.42, h: 1.36 }, { u: -.17, h: 1.47 }].map(p => ({ u: p.u + hs, h: p.h + d })) };
    J.torso.r = J.torso.l.map(p => ({ u: 2 * hs - p.u, h: p.h }));
    J.arms = [-1, 1].map(s => {
      const sw = walk * Math.sin(ph + (s < 0 ? 0 : Math.PI)) * .07, sh = { u: s * .39 + hs, h: 1.33 + d };
      let el = { u: s * .52 + hs, h: .93 + sw + d }, wr = { u: s * .49 + hs, h: .47 + sw * 1.4 + d };
      if (s === 1 && reach > 0) { el = { u: lerp(el.u, .44, reach), h: lerp(el.h, .62, reach) }; wr = { u: lerp(wr.u, .30, reach), h: lerp(wr.h, .12, reach) }; }
      if (s === side && swipe >= 0) {
        el = { u: lerp(lerp(el.u, s * .58, up), s * .10, strike), h: lerp(lerp(el.h, 1.62, up), 1.00, strike) };
        wr = { u: lerp(lerp(wr.u, s * .40, up), -s * .34, strike), h: lerp(lerp(wr.h, 2.00, up), facing === 'south' ? .34 : .60, strike) };
      }
      return [sh, el, wr];
    });
    J.neck = [{ u: hs, h: 1.40 + d }, { u: hs, h: 1.50 + d }];
    J.head = { u: hs + .05 * sway, h: 1.50 + d - .05 * strike, tilt: -.09 * sway, grow: 1 + .06 * strike };
    return J;
  }
  // Profile, facing +u: pelvis back, ribcage forward, neck thrust forward and the head in front of
  // the chest; knees bent; far limbs a little apart from the near ones (near = index 1).
  const lean = .16 + .06 * strike + .12 * reach;
  J.legs = [-1, 1].map(s => {
    const a = ph + (s < 0 ? Math.PI : 0), fu = walk * .36 * Math.sin(a) + (s < 0 ? -.08 : .04), lift = walk * Math.max(0, Math.cos(a)) * .14;
    return [{ u: -.04, h: .95 + d * .5 }, { u: fu * .5 + .13, h: .52 + lift }, { u: fu - .04, h: .08 + lift * .45 }, { u: fu + .14, h: lift * .3 }];
  });
  J.torso = {
    back: [{ u: -.19, h: .90 }, { u: -.16, h: 1.04 }, { u: -.10 + lean * .5, h: 1.22 }, { u: .02 + lean, h: 1.38 }].map(p => ({ u: p.u, h: p.h + d })),
    front: [{ u: .10, h: .90 }, { u: .07, h: 1.03 }, { u: .24 + lean * .6, h: 1.18 }, { u: .26 + lean, h: 1.32 }].map(p => ({ u: p.u, h: p.h + d })),
  };
  const sh = { u: .10 + lean, h: 1.33 + d };
  J.arms = [-1, 1].map(s => {
    const sw = walk * Math.sin(ph + (s < 0 ? 0 : Math.PI)), S0 = { u: sh.u + (s < 0 ? .07 : 0), h: sh.h };
    let el = { u: S0.u + .02 + .16 * sw, h: .93 + d }, wr = { u: S0.u + .12 + .34 * sw, h: .50 + d + .05 * Math.abs(sw) };
    if (s === 1 && reach > 0) { el = { u: lerp(el.u, sh.u + .33, reach), h: lerp(el.h, .70, reach) }; wr = { u: lerp(wr.u, sh.u + .55, reach), h: lerp(wr.h, .12, reach) }; }
    if (s === 1 && swipe >= 0) {
      el = { u: lerp(lerp(el.u, sh.u - .15, up), sh.u + .45, strike), h: lerp(lerp(el.h, 1.65, up), 1.22, strike) };
      wr = { u: lerp(lerp(wr.u, sh.u - .03, up), sh.u + .85, strike), h: lerp(lerp(wr.h, 2.00, up), .84, strike) };
    }
    return [S0, el, wr];
  });
  const nt = { u: .30 + lean * 1.1, h: 1.44 + d };
  J.neck = [{ u: .14 + lean, h: 1.36 + d }, nt];
  J.head = { u: nt.u + .03 * sway + .10 * strike, h: nt.h - .02 * strike, tilt: -.10 + .05 * sway, grow: 1 };
  return J;
}

// Draw the v2 ghost. facing 'south' | 'north' | 'east' | 'west'. lo/hi clamp heights (forming and
// dissolving); t is the clip time; fray 0..1 its time running out; travel the walking direction
// on the map times how much it walks (loose ends trail against it). Returns the wrists' screen
// points and the segments for flakes and chunks.
export function ghost2(key, feet, facing, poseOpts, { lo = 0, hi = 9, scale = 1, layer = pawnLayer, sun, strength = .3, alpha = 1, t = 0, fray = 0, travel = { x: 0, z: 0 } } = {}) {
  const east = facing === 'east' || facing === 'west', m = facing === 'west' ? -1 : 1;
  const J = pose2(east ? 'east' : facing, { ...poseOpts, t });
  const S = q => ({ x: feet.x + q.u * scale * m, z: feet.z + Math.min(hi, Math.max(lo, q.h * scale)) * Stand });
  const shown = clamp((Math.min(hi, Top2 * scale) - lo) / (Top2 * scale));
  if (shown <= 0) return null;
  const Hs = Top2 * scale * shown, v = { x: sun.x * Hs, z: sun.z * Hs }, vl = Math.hypot(v.x, v.z);
  sprite({ x: feet.x, z: feet.z - .02 }, .62 * scale, .24 * scale, Body.withAlpha(strength * 2.8 * alpha), soft, shadowLayer);
  if (vl > .01) sprite({ x: feet.x + v.x * .5, z: feet.z + v.z * .5 }, vl + .35 * scale, .46 * scale, Body.withAlpha(strength * 2.1 * alpha), soft, shadowLayer, -Math.atan2(v.z, v.x) / Mathf.Deg2Rad);

  const back = facing === 'north' ? .8 : 1, thin = 1 - .15 * fray;
  const w = { thigh: (east ? [.17, .12, .08, .07] : [.17, .12, .085, .11]).map(x => x * thin), arm: [.15, .11, .085].map(x => x * thin), neck: [.13, .12] };
  const segs = [], add = (pts, widths) => { for (let i = 0; i + 1 < pts.length; i++) segs.push({ a: pts[i], b: pts[i + 1], w: widths[i] }); };
  const hd = J.head, hc = Math.cos(hd.tilt), hsn = Math.sin(hd.tilt);
  const H = q => ({ u: hd.u + (q.u * hc - q.h * hsn) * hd.grow, h: hd.h + (q.u * hsn + q.h * hc) * hd.grow });
  const HS = q => S(H(q)), headShown = hi >= hd.h * scale;
  const grown = (P, k) => { const c = P.reduce((s, p) => ({ x: s.x + p.x / P.length, z: s.z + p.z / P.length }), { x: 0, z: 0 }); return P.map(p => ({ x: c.x + (p.x - c.x) * k, z: c.z + (p.z - c.z) * k })); };

  if (!east) {
    const drawArms = () => J.arms.forEach((a, i) => {
      part2(`${key} arm${i}`, S, a, w.arm, layer, back, 40 + i * 50);
      if (facing === 'south') claws2(`${key} claw${i}`, S, a[1], a[2], ClawLen, J.flex * (i ? 1 : -1), layer, back);
      add(a, w.arm);
    });
    J.legs.forEach((l, i) => { part2(`${key} leg${i}`, S, l, w.thigh, layer, back * .92, 140 + i * 50); add(l, w.thigh); });
    if (facing === 'north') { J.arms.forEach((a, i) => claws2(`${key} claw${i}`, S, a[1], a[2], ClawLen, J.flex * (i ? 1 : -1), layer, back * .8)); drawArms(); }
    torso2(`${key} torso`, J.torso.l.map(S), J.torso.r.map(S), layer, back);
    if (facing === 'north') trail(`${key} spine`, [{ u: 0, h: .95 }, { u: .01, h: 1.12 }, { u: 0, h: 1.30 }, { u: 0, h: 1.45 }].map(S), .035, InkEdge.withAlpha(.9), layer);
    segs.push({ a: J.torso.l[0], b: J.torso.l[3], w: .06 }, { a: J.torso.r[0], b: J.torso.r[3], w: .06 });
    if (facing !== 'north') drawArms();
    part2(`${key} neck`, S, J.neck, w.neck, layer, back, 300);
    if (headShown && facing === 'south') headFront(`${key} head`, HS, J, t, layer, grown);
    if (headShown && facing === 'north') headBack(`${key} head`, HS, layer, back, grown);
    add(J.neck, w.neck);
    segs.push({ a: H(FrontEdge[1]), b: H(FrontEdge[4]), w: .05 }, { a: H({ u: -FrontEdge[1].u, h: FrontEdge[1].h }), b: H({ u: -FrontEdge[4].u, h: FrontEdge[4].h }), w: .05 });
  } else {
    // Far limbs first and darker, then torso, near leg, neck, head, near arm last.
    part2(`${key} leg0`, S, J.legs[0], w.thigh, layer, .68, 140); add(J.legs[0], w.thigh);
    part2(`${key} arm0`, S, J.arms[0], w.arm, layer, .68, 40); claws2(`${key} claw0`, S, J.arms[0][1], J.arms[0][2], ClawLen, -J.flex, layer, .68);
    torso2(`${key} torso`, J.torso.back.map(S), J.torso.front.map(S), layer, 1);
    segs.push({ a: J.torso.back[0], b: J.torso.back[2], w: .06 }, { a: J.torso.front[0], b: J.torso.front[2], w: .06 });
    part2(`${key} leg1`, S, J.legs[1], w.thigh, layer, 1, 190); add(J.legs[1], w.thigh);
    part2(`${key} neck`, S, J.neck, w.neck, layer, 1, 300); add(J.neck, w.neck);
    if (headShown) headSide(`${key} head`, HS, J, t, layer, m, grown);
    segs.push({ a: H(SideTop[0]), b: H(SideTop[5]), w: .06 });
    part2(`${key} arm1`, S, J.arms[1], w.arm, layer, 1, 90);
    claws2(`${key} claw1`, S, J.arms[1][1], J.arms[1][2], ClawLen, J.flex, layer);
    add(J.arms[0], w.arm); add(J.arms[1], w.arm);
  }

  // Loose bandage ends: 3, and 2 more as it frays; they hang, trail against the walk and ripple.
  const A = J.arms, T = J.torso, walk = poseOpts.walk ?? 0;
  const roots = east
    ? [[{ u: lerp(A[1][1].u, A[1][2].u, .45), h: lerp(A[1][1].h, A[1][2].h, .45) }, .42], [T.back[1], .40], [H({ u: -.05, h: .08 }), .34], [J.legs[1][1], .28], [A[0][1], .30]]
    : [[{ u: lerp(A[0][1].u, A[0][2].u, .45), h: lerp(A[0][1].h, A[0][2].h, .45) }, .42], [A[1][1], .30], [T.l[1], .40], [J.legs[1][1], .28], [T.r[3], .34]];
  const dl = Math.hypot(travel.x * 1.1, 1 + travel.z * .6);
  roots.forEach(([q, len], i) => {
    const grow = i < 3 ? 1 : smooth(clamp((fray - (i === 3 ? .3 : .6)) / .25));
    if (grow <= 0 || q.h * scale < lo || q.h * scale > hi) return;
    const sway = .25 * Math.sin(t * 1.7 + i * 2.1) * (1 - walk * .5);
    const dir = { x: (-travel.x * 1.1 + sway) / dl, z: (-1 - travel.z * .6) / dl };
    looseEnd(`${key} loose${i}`, S(q), dir, len * scale * (1 + .6 * fray) * grow, .05 * scale, t, i * 1.9, .04 + .03 * walk + .04 * fray, LooseBandage.withAlpha(.9 * back * alpha), layer);
  });
  return { wrists: J.arms.map(a => S(a[2])), elbows: J.arms.map(a => S(a[1])), segs, S, shown };
}

// Side-to-side shares of two edges paired by index: the points k of the way from L to R.
const across = (L, R, k) => L.map((p, i) => ({ x: lerp(p.x, R[i].x, k), z: lerp(p.z, R[i].z, k) }));

// Front head: first the parts under the hood (the mouth inside, the lower jaw, lower teeth, saliva,
// the tongue), then the hood over them with its lit east side, a ridge down the middle and two
// wraps, then the upper teeth along its lower edges (they show a little even with the mouth shut).
function headFront(key, HS, J, t, layer, grown) {
  const jaw = J.jaw, low = (k = 0) => MouthFront.map((q, i) => ({ u: q.u * (1 - .12 * jaw), h: q.h - DropFront[i] * jaw - k }));
  const ta = clamp((jaw - .1) / .3), upS = MouthFront.map(HS);
  if (jaw > .02) {
    const lowS = low().map(HS), rimS = low(.045).map(HS);
    band(`${key} maw`, upS, lowS, Maw, layer);
    band(`${key} jaw e`, lowS.map(p => ({ x: p.x, z: p.z + .01 })), rimS.map(p => ({ x: p.x, z: p.z - .014 })), InkEdge, layer);
    band(`${key} jaw`, lowS, rimS, Ink, layer);
    if (ta > 0) teeth(`${key} lt`, HS, low(), 1, .03, ta, layer);
    const sa = clamp((jaw - .4) / .3) * .5;
    if (sa > 0) [1, 3].forEach(i => spit(`${key} spit${i}`, upS[i], lowS[i], .03, sa, layer));
  }
  if (J.tongue > .02) tongue(`${key}`, HS, { u: 0, h: -.115 - .07 * jaw }, -Math.PI / 2 + .12 * Math.sin(t * 11), .34 * J.tongue, t, layer, 1);
  const Lp = FrontEdge.map(HS), Rp = FrontEdge.map(q => HS({ u: -q.u, h: q.h }));
  band(`${key} hood e`, grown(Lp, 1.09), grown(Rp, 1.09), InkEdge, layer);
  band(`${key} hood`, Lp, Rp, Ink, layer);
  band(`${key} hood l`, across(Lp, Rp, .55), across(Lp, Rp, .93), InkLit, layer);
  trail(`${key} ridge`, [HS({ u: 0, h: .33 }), HS({ u: .005, h: .14 }), HS({ u: 0, h: -.06 })], .03, InkLit.withAlpha(.7), layer);
  trail(`${key} hw1`, [HS({ u: -.24, h: .22 }), HS({ u: -.05, h: .28 }), HS({ u: .16, h: .31 })], .03, Bandage.withAlpha(.55), layer);
  trail(`${key} hw2`, [HS({ u: -.12, h: .04 }), HS({ u: .03, h: .11 }), HS({ u: .19, h: .17 })], .026, Bandage.withAlpha(.45), layer);
  teeth(`${key} ut`, HS, MouthFront, -1, lerp(.024, .034, ta), lerp(.8, 1, ta), layer);
}

// Back head: the hood from behind, its point (the snout) away from the viewer, and a groove.
function headBack(key, HS, layer, tone, grown) {
  const Lp = BackEdge.map(HS), Rp = BackEdge.map(q => HS({ u: -q.u, h: q.h }));
  band(`${key} e`, grown(Lp, 1.09), grown(Rp, 1.09), InkEdge, layer);
  band(`${key} b`, Lp, Rp, Color.Lerp(InkEdge, Ink, tone), layer);
  band(`${key} l`, across(Lp, Rp, .6), across(Lp, Rp, .93), Color.Lerp(InkEdge, InkLit, tone), layer);
  trail(`${key} groove`, [HS({ u: 0, h: .32 }), HS({ u: 0, h: .12 }), HS({ u: 0, h: -.08 })], .03, InkEdge.withAlpha(.9), layer);
  trail(`${key} w`, [HS({ u: -.26, h: .05 }), HS({ u: 0, h: .10 }), HS({ u: .24, h: .16 })], .03, Bandage.withAlpha(.5 * tone), layer);
}

// Side head: the lower jaw swinging on its hinge, the mouth inside and the tongue, then the wedge of
// the upper head over them, then the teeth (the upper row shows even with the mouth shut, like an
// alligator's) and saliva. m = -1 facing west (the teeth's side flips on screen).
function headSide(key, HS, J, t, layer, m, grown) {
  const jaw = J.jaw, a = -jaw * JawSwing, c = Math.cos(a), s = Math.sin(a);
  const rot = q => { const du = q.u - Hinge.u, dh = q.h - Hinge.h; return { u: Hinge.u + du * c - dh * s, h: Hinge.h + du * s + dh * c }; };
  const jt = JawTop.map(rot), jb = JawBottom.map(rot), upLine = SideMouth.slice(1, 6);
  const jtS = jt.map(HS), jbS = jb.map(HS);
  if (jaw > .02) band(`${key} maw`, upLine.map(HS), jtS, Maw, layer);
  band(`${key} jaw e`, grown(jtS, 1.08), grown(jbS, 1.08), InkEdge, layer);
  band(`${key} jaw`, jtS, jbS, Ink, layer);
  if (J.tongue > .02 || jaw > .3) tongue(`${key}`, HS, { u: .06, h: -.06 }, a * .5 + .05 * Math.sin(t * 9), .12 + .42 * J.tongue, t, layer, 1);
  const top = SideTop.map(HS), mouth = SideMouth.map(HS);
  band(`${key} e`, grown(mouth, 1.08), grown(top, 1.08), InkEdge, layer);
  band(`${key} b`, mouth, top, Ink, layer);
  band(`${key} l`, across(mouth, top, .6), across(mouth, top, .92), InkLit, layer);
  [2, 4].forEach((i, j) => trail(`${key} w${j}`, [mouth[i], { x: (mouth[i].x + top[i].x) / 2 - .015 * m, z: (mouth[i].z + top[i].z) / 2 }, top[i]], .028, Bandage.withAlpha(.5), layer));
  const ta = clamp((jaw - .1) / .3);
  teeth(`${key} ut`, HS, upLine, -m, lerp(.024, .034, ta), lerp(.8, 1, ta), layer);
  if (ta > 0) teeth(`${key} lt`, HS, jt, m, .03, ta, layer);
  const sa = clamp((jaw - .4) / .3) * .5;
  if (sa > 0) [1, 2].forEach(i => spit(`${key} spit${i}`, HS(upLine[i]), jtS[i], .025, sa, layer));
}

// Bigger pieces breaking off as its time runs out (k 0..1): each clings to the edge for the first
// 30 % of its life, then peels off and drifts up slowly, tumbling.
export function chunks(key, feet, segs, t, k, { lo = 0, hi = 9, scale = 1, mirror = 1, layer, alpha = 1 } = {}) {
  if (!segs || !segs.length) return;
  const N = Math.round(26 * k);
  for (let i = 0; i < N; i++) {
    const life = 1.3 + rand(i * 4.7 + 9) * .9, ph = t / life + rand(i * 3.3 + 2), cyc = Math.floor(ph), u = ph - cyc, sd = i * 173 + cyc * 23 + 11;
    const sg = segs[Math.floor(rand(sd) * segs.length) % segs.length], tt = rand(sd + 1), side = rand(sd + 2) < .5 ? -1 : 1;
    const h0 = lerp(sg.a.h, sg.b.h, tt) * scale;
    if (h0 < lo || h0 > hi) continue;
    const du = sg.b.u - sg.a.u, dh = sg.b.h - sg.a.h, L = Math.hypot(du, dh) || 1;
    const u0 = (lerp(sg.a.u, sg.b.u, tt) + (-dh / L) * side * sg.w * .45) * scale, go = smooth(clamp((u - .3) / .7));
    const x = feet.x + (u0 + ((-dh / L) * side * .10 + (rand(sd + 3) - .5) * .2) * go) * mirror, z = feet.z + (h0 + .35 * go) * Stand;
    shard(i + 1, x, layer + i * .0002, z, (.07 + .06 * rand(sd + 5)) * (1 - go * .4) * scale, rand(sd + 6) * 360 + go * 200, Flake.withAlpha((1 - go * go) * alpha));
  }
}
