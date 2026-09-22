// Shared drawing for the Chain Sickle kit: the sickle, the chain with its links, the weight, the
// coil that wraps a pawn, the floor stake, drag scuffs and a stand-in bullet. Snag and Stake both
// draw the weapon through these so the two stay identical, and a C# port makes one class of it.
//
// Everything here lies at one height or is a level circle (the spin overhead, the coil around a
// pawn), so it turns with the aim and needs no per-facing method. Height is folded into z by
// Lift; every ground point carries an h and gets a shadow cast along the scene's sun.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, ShaderDatabase } from '../../js/engine.js';
import { draw } from './six-paths-solid.js';
import { Body, Y, Floor, Lift, sprite, band, circle, soft, glow, rand } from './six-paths-impact.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
export const disc = Meshes.disc(40, 'chain sickle disc');
export const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
export const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
export const Skin = new Color(.83, .70, .54), Pale = new Color(1, .96, .85), Dust = new Color(.80, .74, .63);
export const Enemy = new Color(.55, .38, .27), Ally = new Color(.42, .62, .40), Holder = new Color(.93, .50, .13);
export const Iron = new Color(.30, .31, .34), IronLit = new Color(.52, .54, .58), IronDark = new Color(.09, .09, .11);
export const Steel = new Color(.66, .69, .74), SteelLit = new Color(.90, .92, .95), Wood = new Color(.36, .22, .11), WoodDark = new Color(.16, .09, .04);
export const Blood = new Color(.50, .07, .06), Flash = new Color(1, .93, .70);
// Decided looks and the rule's fixed numbers.
export const HandH = .5;                       // the hand that holds the chain, cells up
export const ChestH = .45;                     // where the weight hits a standing pawn
export const Range = 7;                        // Snag range, cells
export const LinkLen = .11, LinkW = .055;      // one chain link on screen
export const CoilR = .26, CoilTurns = 2;       // the coil around a standing pawn
export const Lead = .2, Tail = .4;
// The weight rule, shared by Snag and Stake: ratio = holder Carrying Capacity / target Mass + gear.
export const FullPull = 5, MaxReel = 2, DragRatio = .4, DragBack = 2, DragTime = 1, MaxPin = 6;
// Stand-in targets: mass in kg (body plus carried gear) and body size for the drawing.
export const Targets = {
  'tribal 65 kg': { mass: 65, size: 1 }, 'raider 95 kg': { mass: 95, size: 1 },
  'muffalo 140 kg': { mass: 140, size: 2.4 }, 'thrumbo 240 kg': { mass: 240, size: 4 },
};
export function rule(carry, targetKey) {
  const tg = Targets[targetKey] ?? Targets['tribal 65 kg'], ratio = (carry || 75) / tg.mass, dragged = ratio < DragRatio;
  const pull = dragged ? 0 : Math.min(FullPull, FullPull * ratio);
  const reel = dragged ? DragTime : Math.min(MaxReel, 1 / ratio);
  const pin = dragged ? 0 : Math.min(MaxPin, MaxPin * ratio);      // 0 = Stake not allowed
  return { ...tg, ratio, dragged, pull, reel, pin };
}
export const bump = x => (x >= 0 && x <= 1) ? Math.sin(x * Math.PI) : 0;
export const easeOut = x => 1 - Math.pow(1 - clamp(x), 3);

// The aim frame: along the cast direction, across it, h cells up. cast() is the same point's shadow.
export function frame(aimDeg, sun) {
  const a = aimDeg * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a);
  return {
    ca, sa, a,
    ground: (base, along, across) => ({ x: base.x + along * ca - across * sa, z: base.z + along * sa + across * ca }),
    place: (base, along, across, h = 0) => ({ x: base.x + along * ca - across * sa, z: base.z + along * sa + across * ca + h * Lift }),
    cast: (base, along, across, h = 0) => ({ x: base.x + along * ca - across * sa + sun.x * h, z: base.z + along * sa + across * ca + sun.z * h }),
  };
}
// A ground point with height -> its screen point, and its shadow point.
export const screen = q => ({ x: q.x, z: q.z + (q.h ?? 0) * Lift });
export const shadow = (q, sun) => ({ x: q.x + sun.x * (q.h ?? 0), z: q.z + sun.z * (q.h ?? 0) });

export function rect(key, c, len, wid, deg, colour, layer) {
  const r = deg * Mathf.Deg2Rad, cx = Math.cos(r), sx = Math.sin(r);
  const ax = cx * len / 2, az = sx * len / 2, bx = -sx * wid / 2, bz = cx * wid / 2;
  band(key, [{ x: c.x - ax - bx, z: c.z - az - bz }, { x: c.x + ax - bx, z: c.z + az - bz }],
    [{ x: c.x - ax + bx, z: c.z - az + bz }, { x: c.x + ax + bx, z: c.z + az + bz }], colour, layer);
}
export function tube(key, pts, wf, colour, layer, lo = -1, hi = 1) {
  const A = [], B = [], n = pts.length - 1;
  if (n < 1) return;
  for (let i = 0; i <= n; i++) {
    const pr = pts[Math.max(0, i - 1)], nx = pts[Math.min(n, i + 1)];
    let dx = nx.x - pr.x, dz = nx.z - pr.z; const L = Math.hypot(dx, dz) || 1; dx /= L; dz /= L;
    const w = wf(i / n), q = pts[i];
    A.push({ x: q.x - dz * w * lo, z: q.z + dx * w * lo }); B.push({ x: q.x - dz * w * hi, z: q.z + dx * w * hi });
  }
  band(key, A, B, colour, layer);
}
// Stand-in pawn: two discs and a soft shadow. downed = 1 lays it on its side.
export function figure(pos, colour, sun, strength, { downed = 0 } = {}) {
  sprite({ x: pos.x + sun.x * .45 * (1 - downed), z: pos.z + sun.z * .45 * (1 - downed) }, .85, .4, Body.withAlpha(strength), soft, shadowLayer);
  if (downed >= 1) {
    draw(disc, pos.x + .05, pawnLayer, pos.z + .12, .32, .2, 0, colour);
    draw(disc, pos.x - .34, pawnLayer + .002, pos.z + .14, .16, .17, 0, Skin);
    return;
  }
  draw(disc, pos.x, pawnLayer, pos.z + .18, .22, .32, 0, colour);
  draw(disc, pos.x, pawnLayer + .002, pos.z + .58, .16, .17, 0, Skin);
}

// Stand-in animal: a long body disc and a head at the far end, scaled by body size (1 = human).
export function beast(pos, size, sun, strength, colour = new Color(.45, .36, .28)) {
  const k = Math.sqrt(size);
  sprite({ x: pos.x + sun.x * .5 * k, z: pos.z + sun.z * .5 * k }, 1.3 * k, .6 * k, Body.withAlpha(strength), soft, shadowLayer);
  draw(disc, pos.x, pawnLayer, pos.z + .2 * k, .42 * k, .3 * k, 0, colour);
  draw(disc, pos.x + .34 * k, pawnLayer + .002, pos.z + .3 * k, .17 * k, .15 * k, 0, Color.Lerp(colour, Skin, .3));
}

// ---- chain -------------------------------------------------------------------------------
// A hanging chain between two ground+height points: straight in plan, drooping by `sag` cells at
// the middle. `wobble` adds a small sideways wave (a chain under load hums).
export function chainPath(a, b, sag, n = 24, wobble = 0, s = 0) {
  const out = [];
  for (let i = 0; i <= n; i++) {
    const u = i / n, droop = sag * 4 * u * (1 - u);
    const dx = b.x - a.x, dz = b.z - a.z, L = Math.hypot(dx, dz) || 1;
    const w = wobble * Math.sin(u * Math.PI * 3 + s * 55) * Math.sin(u * Math.PI);
    out.push({ x: lerp(a.x, b.x, u) - dz / L * w, z: lerp(a.z, b.z, u) + dx / L * w, h: Math.max(.02, lerp(a.h ?? 0, b.h ?? 0, u) - droop) });
  }
  return out;
}
// A level spiral around a pawn: `turns` around `centre`, radius r, height h0 -> h1. The first point
// sits at `startDeg` (the side the chain arrives from).
export function coilPath(centre, turns, r, h0, h1, startDeg, n = 36) {
  const out = [];
  for (let i = 0; i <= n; i++) {
    const u = i / n, a = startDeg * Mathf.Deg2Rad + u * turns * TAU;
    out.push({ x: centre.x + Math.cos(a) * r, z: centre.z + Math.sin(a) * r, h: lerp(h0, h1, u) });
  }
  return out;
}
// Resample a path to points `step` apart along its screen length.
function resample(pts, step) {
  const out = [pts[0]]; let carry = 0;
  for (let i = 1; i < pts.length; i++) {
    const a = pts[i - 1], b = pts[i], L = Math.hypot(b.x - a.x, b.z - a.z);
    let d = step - carry;
    while (d <= L) { const u = d / L; out.push({ x: lerp(a.x, b.x, u), z: lerp(a.z, b.z, u), h: lerp(a.h ?? 0, b.h ?? 0, u) }); d += step; }
    carry = L - (d - step);
  }
  return out;
}
// The chain itself: a dark core tube, then alternating flat (lit, wide) and edge-on (dark, narrow)
// links along it, and a soft shadow on the floor. `alpha` fades the whole chain.
export function chain(key, path, sun, strength, layer = Y + .02, alpha = 1) {
  if (!path || path.length < 2) return;
  const scr = path.map(screen), sh = path.map(q => shadow(q, sun));
  tube(`${key} shadow`, sh, () => .045, Body.withAlpha(strength * .55 * alpha), shadowLayer);
  tube(`${key} core`, scr, () => .022, IronDark.withAlpha(alpha), layer);
  const links = resample(scr, LinkLen);
  for (let i = 0; i + 1 < links.length; i++) {
    const a = links[i], b = links[i + 1], mid = { x: (a.x + b.x) / 2, z: (a.z + b.z) / 2 };
    const deg = Math.atan2(b.z - a.z, b.x - a.x) / Mathf.Deg2Rad, flat = i % 2 === 0;
    rect(`${key} link ${i}`, mid, LinkLen * .92, flat ? LinkW : LinkW * .55, deg, (flat ? IronDark : Iron).withAlpha(alpha), layer + .002);
    if (flat) rect(`${key} link lit ${i}`, mid, LinkLen * .70, LinkW * .45, deg, IronLit.withAlpha(alpha), layer + .004);
  }
}
// A coil around a pawn: the runs north of `splitZ` draw under the pawn layer, the runs south of it
// over it, so the pawn stands between the two halves and the chain reads as wrapped.
export function coil(key, pts, splitZ, sun, strength) {
  let run = [], side = null, n = 0;
  const flush = () => { if (run.length > 1) chain(`${key} ${n++}`, run, sun, strength, side ? pawnLayer - .02 : Y + .02); };
  for (const q of pts) {
    const back = q.z >= splitZ;
    if (side !== null && back !== side) { run.push(q); flush(); run = [q]; }
    else run.push(q);
    side = back;
  }
  flush();
}
// The weight: an iron ball with a lit top and a dark underside. Staked, it sits in the floor with
// its spike buried, so only a flat top shows and a crack ring around it.
export function weight(q, sun, strength, layer = Y + .03, { staked = 0, size = .16 } = {}) {
  const p = screen(q), h = q.h ?? 0;
  const sz = lerp(size, size * .8, staked);
  if (staked < 1) {
    const sd = shadow(q, sun);
    sprite(sd, sz * 2.2 * (1 - staked), sz * 1.4 * (1 - staked), Body.withAlpha(strength * (1 - staked) * (1 / (1 + h))), soft, shadowLayer);
  }
  draw(disc, p.x, layer, p.z, sz, sz, 0, IronDark);
  draw(disc, p.x, layer + .002, p.z + sz * .18, sz * .78, sz * .72, 0, Iron);
  draw(disc, p.x - sz * .18, layer + .004, p.z + sz * .38, sz * .32, sz * .24, 0, IronLit.withAlpha(.9));
}
// Cracked floor around a stake: short dark radial lines that stay.
export function crack(key, pos, amount, seed = 0) {
  if (amount <= 0) return;
  for (let i = 0; i < 6; i++) {
    const a = (i / 6) * TAU + rand(i + seed + 400) * .6, len = (.18 + rand(i + seed + 410) * .2) * amount;
    const p0 = { x: pos.x + Math.cos(a) * .1, z: pos.z + Math.sin(a) * .1 }, p1 = { x: pos.x + Math.cos(a) * (.1 + len), z: pos.z + Math.sin(a) * (.1 + len) };
    tube(`${key} crack ${i}`, [p0, { x: (p0.x + p1.x) / 2 + Math.sin(a) * .03, z: (p0.z + p1.z) / 2 - Math.cos(a) * .03 }, p1], u => .035 * (1 - u * .8), Body.withAlpha(.6 * amount), Floor + .02);
  }
  sprite(pos, .5 * amount, .32 * amount, Body.withAlpha(.35 * amount), soft, Floor + .015);
}
// The sickle: a wooden handle along `deg` from the hand, a curved steel blade at the far end that
// hooks 100 degrees to the left. Lies level at height h, so it is a flat shape that turns freely.
export function sickle(key, hand, h, deg, sun, strength, layer = Y + .05, alpha = 1) {
  const r = deg * Mathf.Deg2Rad, cx = Math.cos(r), sx = Math.sin(r);
  const Handle = .48, R = .21, Sweep = 100 * Mathf.Deg2Rad;
  const parts = (base, lay, tone) => {
    const hc = { x: base.x + cx * Handle * .42, z: base.z + sx * Handle * .42 };
    rect(`${key} handle ${lay}`, hc, Handle, .07, deg, tone ? Body.withAlpha(alpha * tone) : WoodDark.withAlpha(alpha), lay);
    if (!tone) rect(`${key} handle face`, hc, Handle * .96, .045, deg, Wood.withAlpha(alpha), lay + .002);
    // The blade: an arc centred `R` to the left of the handle's tip.
    const tip = { x: base.x + cx * Handle * .92, z: base.z + sx * Handle * .92 };
    const c = { x: tip.x - sx * R, z: tip.z + cx * R };
    const inner = [], outer = [], n = 14;
    for (let i = 0; i <= n; i++) {
      const u = i / n, a = r - Math.PI / 2 + u * Sweep, w = .085 * (1 - u * u);
      inner.push({ x: c.x + Math.cos(a) * (R - w * .1), z: c.z + Math.sin(a) * (R - w * .1) });
      outer.push({ x: c.x + Math.cos(a) * (R + w), z: c.z + Math.sin(a) * (R + w) });
    }
    if (tone) { band(`${key} blade ${lay}`, inner, outer, Body.withAlpha(alpha * tone), lay); return; }
    band(`${key} blade edge`, inner.map((q, i) => ({ x: q.x + (q.x - outer[i].x) * .18, z: q.z + (q.z - outer[i].z) * .18 })),
      outer.map((q, i) => ({ x: q.x + (q.x - inner[i].x) * .18, z: q.z + (q.z - inner[i].z) * .18 })), IronDark.withAlpha(alpha), lay + .001);
    band(`${key} blade face`, inner, outer, Steel.withAlpha(alpha), lay + .003);
    band(`${key} blade lit`, inner.map((q, i) => ({ x: lerp(q.x, outer[i].x, .55), z: lerp(q.z, outer[i].z, .55) })), outer, SteelLit.withAlpha(alpha * .85), lay + .005);
  };
  parts(shadow({ ...hand, h }, sun), shadowLayer, strength * .6);
  parts(screen({ ...hand, h }), layer, 0);
}
// A drag mark on the floor from `from` to `to`, growing with `amount`, that stays: two heel
// furrows either side of the line and a faint dusting between them.
export function scuff(key, from, to, amount, alpha = .4) {
  if (amount <= 0) return;
  const end = { x: lerp(from.x, to.x, amount), z: lerp(from.z, to.z, amount) };
  const dx = to.x - from.x, dz = to.z - from.z, L = Math.hypot(dx, dz) || 1, nx = -dz / L, nz = dx / L;
  sprite({ x: (from.x + end.x) / 2, z: (from.z + end.z) / 2 }, L * amount, .3, Dust.withAlpha(alpha * .35), soft, Floor + .011, Math.atan2(dz, dx) / Mathf.Deg2Rad);
  for (const side of [-1, 1]) {
    const pts = [];
    for (let i = 0; i <= 8; i++) {
      const u = i / 8, wave = Math.sin(u * 9 + side) * .02;
      pts.push({ x: lerp(from.x, end.x, u) + nx * (side * .11 + wave), z: lerp(from.z, end.z, u) + nz * (side * .11 + wave) });
    }
    tube(`${key} furrow ${side}`, pts, u => .035 + .02 * u, Body.withAlpha(alpha), Floor + .013);
  }
}
// Dust kicked up at a dragged pawn's feet.
export function kick(pos, age, strength, seed = 0) {
  for (let i = 0; i < 5; i++) {
    const life = .35 + rand(i + seed) * .3, u = ((age * 1.6 + rand(i + seed + 30)) % 1);
    const x = pos.x + (rand(i + seed + 10) - .5) * .5, h = u * .35;
    sprite({ x, z: pos.z - .05 + h * Lift + (rand(i + seed + 20) - .5) * .2 }, .25 + u * .3, .2 + u * .25, Dust.withAlpha(Math.sin(u * Math.PI) * .5 * strength), puff, Y + .01);
  }
}
// A stand-in rifle: a dark body with a lit top edge and a wooden stock, lying level at height
// q.h and turned by `deg`. On the floor it draws at the Filth layer so it reads as an item.
export function rifle(key, q, deg, sun, strength, layer = Y + .06) {
  const p = screen(q), sd = shadow(q, sun), r = deg * Mathf.Deg2Rad, cx = Math.cos(r), sx = Math.sin(r);
  rect(`${key} shadow`, sd, .78, .1, deg, Body.withAlpha(strength * .6), shadowLayer);
  rect(`${key} body`, p, .78, .075, deg, IronDark, layer);
  rect(`${key} barrel`, { x: p.x + cx * .22, z: p.z + sx * .22 }, .34, .04, deg, Iron, layer + .002);
  rect(`${key} stock`, { x: p.x - cx * .26, z: p.z - sx * .26 }, .24, .085, deg, Wood, layer + .002);
  rect(`${key} lit`, { x: p.x + .01, z: p.z + .02 }, .5, .02, deg, IronLit.withAlpha(.8), layer + .004);
}
// A faint floor ring at the rule's radius that fades over `life`.
export function rangeRing(pos, radius, age, life = .8, alpha = .35) {
  if (age < 0 || age > life) return;
  circle(pos, radius, alpha * (1 - age / life), Floor + .01, Pale);
}
