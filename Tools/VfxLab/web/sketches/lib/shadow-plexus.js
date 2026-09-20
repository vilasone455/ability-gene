// Shadow plexus: the drawing pieces shared by the four sketches (Imitation, Seam, Grasp, Double).
// Not a sketch itself, so it is not listed in sketches/index.js.
//
// The kit's picture is a flat black shadow that crawls over the ground from the carrier's feet.
// Everything the ability draws lies on the floor (Filth layer, under items and pawns), so it turns
// freely with the aim and needs no per-facing method. The only things with height are the threads
// that climb a held body and the standing double, and both rise out of the floor.
//
// Stand-ins, lab only, not for the port: the night overlay (the game has its own darkness), the
// campfire and its light pool, and the pawns' cast shadows. They are here because the kit's rules
// are about light: sky light counts half, fire and lamp light counts full, a cell under 30 % is
// dark. lighting() holds that rule; the sketches read levels from it and nowhere else.
//
// Port notes: shadowLine is one strip mesh rebuilt while it shows, plus a wider fringe strip; pool
// is one fan mesh; threads are tapered strips on MoteOverhead. All 'white' texture, Transparent.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../../js/engine.js';
import { draw, mesh } from './six-paths-solid.js';
import { Y, Floor, Lift, sprite, band, trail, circle, glow, soft, rand } from './six-paths-impact.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
const disc = Meshes.disc(32, 'shadow plexus disc');
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const itemLayer = AltitudeLayer.Item.AltitudeFor();
export const NightLayer = AltitudeLayer.LightingOverlay.AltitudeFor(), LightLayer = AltitudeLayer.VisEffects.AltitudeFor();
export const LineLayer = Floor + .02;

export const Shade = new Color(.012, .012, .028), Fringe = new Color(.17, .13, .33), RangeTint = new Color(.74, .70, .92);
export const Ember = new Color(1, .52, .12), Flame = new Color(1, .83, .45), Dust = new Color(.52, .45, .37);
export const CarrierColour = new Color(.39, .58, .65), EnemyColour = new Color(.55, .38, .27), AllyColour = new Color(.42, .62, .40);
export const Skin = new Color(.83, .70, .54), Mech = new Color(.42, .44, .47);
const Night = new Color(.02, .03, .08);

// The light rule. Sky counts SkyShare, a fire counts full, the higher one wins; under DarkBelow a
// cell is dark. A fire's level falls in a straight line to 0 at its radius.
export const SkyShare = .5, DarkBelow = .3, FireRadius = 10;
const NightSteps = 20, NightDepth = .74, LitAt = .5, NightCell = .25;

// fires: [{ x, z, radius, on }] with on 0..1. Returns the level at a point, and how a thing
// standing there throws its shadow: soft and pale along the sun, or hard and black away from a fire.
export function lighting(night, scene, fires = []) {
  const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32, sky = night ? 0 : SkyShare;
  const strongest = (pos) => {
    let level = 0, from = null;
    for (const f of fires) { const v = f.on * clamp(1 - Math.hypot(pos.x - f.x, pos.z - f.z) / f.radius); if (v > level) { level = v; from = f; } }
    return { level, from };
  };
  return {
    night, sun, strength, fires, sky,
    level: (pos) => Math.max(sky, strongest(pos).level),
    shadow(pos) {
      const f = strongest(pos);
      if (f.level > sky) {
        const dx = pos.x - f.from.x, dz = pos.z - f.from.z, d = Math.hypot(dx, dz) || 1;
        return { hard: true, x: dx / d, z: dz / d, length: Math.min(2.6, .9 + d * .3), alpha: .5 + .4 * f.level };
      }
      return night ? null : { hard: false, x: sun.x, z: sun.z, length: 1, alpha: strength };
    },
  };
}

// The shadow a standing thing throws. Hard: a long body strip with a head at the end. Soft: a blob.
export function castShadow(key, pos, L, alpha = 1, big = 1) {
  const sh = L.shadow(pos);
  if (!sh || alpha <= 0) return;
  if (!sh.hard) { sprite({ x: pos.x + sh.x * .45, z: pos.z + sh.z * .45 }, .85 * big, .4 * big, Shade.withAlpha(sh.alpha * alpha), soft, shadowLayer); return; }
  const nx = -sh.z, nz = sh.x, a = [], b = [];
  for (let i = 0; i <= 6; i++) {
    const u = i / 6, w = lerp(.17, .11, u) * big, x = pos.x + sh.x * sh.length * u, z = pos.z + sh.z * sh.length * u;
    a.push({ x: x + nx * w, z: z + nz * w }); b.push({ x: x - nx * w, z: z - nz * w });
  }
  band(`${key} cast`, a, b, Shade.withAlpha(sh.alpha * alpha), shadowLayer);
  draw(disc, pos.x + sh.x * (sh.length + .1), shadowLayer, pos.z + sh.z * (sh.length + .1), .15 * big, .15 * big, 0, Shade.withAlpha(sh.alpha * alpha));
}

// Two-disc stand-in pawn. tint 0..1 darkens it toward the shadow colour (a held body). shadow 0
// leaves out its cast shadow, which is how a carrier whose shadow is away is drawn.
export function pawn(key, pos, colour, L, { alpha = 1, shadow = 1, tint = 0, big = 1 } = {}) {
  castShadow(key, pos, L, shadow * alpha, big);
  draw(disc, pos.x, pawnLayer, pos.z + .18 * big, .22 * big, .32 * big, 0, Color.Lerp(colour, Shade, tint * .7).withAlpha(alpha));
  draw(disc, pos.x, pawnLayer + .002, pos.z + .58 * big, .16 * big, .17 * big, 0, Color.Lerp(Skin, Shade, tint * .4).withAlpha(alpha));
}
export function downedPawn(key, pos, colour, L, turn = 0) {
  sprite({ x: pos.x, z: pos.z - .05 }, 1, .45, Shade.withAlpha(.3), soft, shadowLayer);
  const r = turn * Mathf.Deg2Rad, c = Math.cos(r), s = Math.sin(r);
  draw(disc, pos.x, pawnLayer, pos.z + .12, .32, .2, -turn, colour);
  draw(disc, pos.x + c * .42, pawnLayer + .002, pos.z + .12 + s * .42, .16, .17, 0, Skin);
}
// A centipede stand-in: three plates in a row along deg.
export function centipede(key, pos, deg, L) {
  const r = deg * Mathf.Deg2Rad, c = Math.cos(r), s = Math.sin(r);
  castShadow(key, pos, L, 1, 2.2);
  for (let i = -1; i <= 1; i++) {
    draw(disc, pos.x + c * i * .62, pawnLayer + i * .001, pos.z + .25 + s * i * .62, .5, .42, 0, Color.Lerp(Mech, Shade, .35));
    draw(disc, pos.x + c * i * .62, pawnLayer + .002 + i * .001, pos.z + .3 + s * i * .62, .42, .34, 0, Mech);
  }
}

// Night: the map darkened everywhere the light level is under LitAt, in NightSteps stacked layers
// so the edge of a light pool is a falloff and not a line. One mesh per layer, rebuilt only when
// the fires change. Lab stand-in for the game's own darkness.
const nightBuilt = new Map();
export function nightOverlay(key, centre, L, halfX = 34, halfZ = 22) {
  if (!L.night) return;
  const fires = L.fires.map(f => ({ ...f, on: Math.round(f.on * 12) / 12 })), quiet = lighting(true, null, fires);
  const x0 = Math.round(centre.x) - halfX, z0 = Math.round(centre.z) - halfZ, cols = halfX * 2 / NightCell, rows = halfZ * 2 / NightCell;
  const sig = `${x0},${z0}|` + fires.map(f => `${f.x.toFixed(2)},${f.z.toFixed(2)},${f.radius},${f.on}`).join(';');
  if (nightBuilt.get(key) !== sig && fires.length === 1) {       // one fire: round steps, no staircase
    nightBuilt.set(key, sig);
    const f = fires[0], far = (halfX + halfZ) * 2;
    for (let k = 0; k < NightSteps; k++) {
      const inner = f.on > 0 ? Math.max(0, f.radius * (1 - (k + 1) / NightSteps * LitAt / f.on)) : 0, v = [], tr = [];
      for (let i = 0; i <= 96; i++) {
        const a = i / 96 * TAU, c = Math.cos(a), sn = Math.sin(a);
        v.push(f.x + c * inner, f.z + sn * inner, f.x + c * far, f.z + sn * far);
        if (i) { const n = i * 2; tr.push(n - 2, n, n - 1, n - 1, n, n + 1); }
      }
      mesh(`${key} night ${k}`).setFlat(v, tr);
    }
  }
  if (nightBuilt.get(key) !== sig) {
    nightBuilt.set(key, sig);
    const v = Array.from({ length: NightSteps }, () => []), t = Array.from({ length: NightSteps }, () => []), row = new Float32Array(cols);
    for (let j = 0; j < rows; j++) {
      const za = z0 + j * NightCell, zb = za + NightCell;
      for (let i = 0; i < cols; i++) row[i] = quiet.level({ x: x0 + (i + .5) * NightCell, z: za + NightCell / 2 });
      for (let k = 0; k < NightSteps; k++) {
        const under = (k + 1) / NightSteps * LitAt;
        for (let i = 0; i < cols; i++) {
          if (row[i] >= under) continue;
          let e = i; while (e + 1 < cols && row[e + 1] < under) e++;
          const xa = x0 + i * NightCell, xb = x0 + (e + 1) * NightCell, n = v[k].length / 2;
          v[k].push(xa, za, xa, zb, xb, zb, xb, za); t[k].push(n, n + 1, n + 2, n, n + 2, n + 3);
          i = e;
        }
      }
    }
    for (let k = 0; k < NightSteps; k++) mesh(`${key} night ${k}`).setFlat(v[k], t[k]);
  }
  const each = 1 - Math.pow(1 - NightDepth, 1 / NightSteps);
  for (let k = 0; k < NightSteps; k++) draw(mesh(`${key} night ${k}`), 0, NightLayer + k * .001, 0, 1, 1, 0, Night.withAlpha(each));
}

// A campfire stand-in with its light pool and a thin ring where the level crosses DarkBelow.
export function fire(f, s) {
  for (const turn of [28, -34]) draw(disc, f.x, itemLayer, f.z, .34, .07, turn, new Color(.2, .13, .08));
  if (f.on <= 0) return;
  const flick = .85 + .1 * Math.sin(s * 11) + .05 * Math.sin(s * 23.7);
  thinRing(`fire ring ${f.x.toFixed(2)} ${f.z.toFixed(2)}`, f, f.radius * (1 - DarkBelow / f.on), Flame.withAlpha(.4 * f.on), Floor + .01);
  sprite(f, f.radius * 1.7, f.radius * 1.7, Ember.withAlpha(.13 * f.on * flick), glow, LightLayer);
  for (let i = 0; i < 3; i++) {
    const h = (.28 + .16 * i) * f.on * (.85 + .15 * Math.sin(s * (9 + i * 4) + i * 2)), x = (i - 1) * .1 * Math.sin(s * 5 + i);
    sprite({ x: f.x + x, z: f.z + h * Lift + .08 }, .5 - i * .1, .5 + h, (i ? Flame : Ember).withAlpha((.75 - i * .12) * f.on * flick), glow, LightLayer + .001 + i * .001);
  }
}

// Points along the straight line from..to between the shares u0 and u1 of it, with a slow sideways
// wave that is pinned at both ends of the whole line. slack 0 is a taut straight line.
export function path(from, to, u0, u1, s, sway = .1, steps = 28) {
  const dx = to.x - from.x, dz = to.z - from.z, len = Math.hypot(dx, dz) || 1, nx = -dz / len, nz = dx / len, waves = Math.max(1, len / 2.4), pts = [];
  for (let i = 0; i <= steps; i++) {
    const u = lerp(u0, u1, i / steps), off = sway * Math.sin(u * waves * TAU - s * 3) * Math.sin(u * Math.PI);
    pts.push({ x: from.x + dx * u + nx * off, z: from.z + dz * u + nz * off });
  }
  return pts;
}
export const pointOn = (from, to, u) => ({ x: lerp(from.x, to.x, u), z: lerp(from.z, to.z, u) });

// The shadow line: a black strip on the floor with a dim indigo fringe so it still reads on dark
// soil. Bulges travel along it toward the far end. flare widens the first points where it leaves a
// body's shadow; the far end runs to a point unless point is false.
export function shadowLine(key, pts, width, alpha, s, { flare = true, point = true, pointStart = false, layer = LineLayer } = {}) {
  const n = pts.length - 1;
  if (n < 1 || alpha <= 0 || Math.hypot(pts[n].x - pts[0].x, pts[n].z - pts[0].z) < .02) return;
  const sides = [[], [], [], []];
  pts.forEach((p, i) => {
    const prev = pts[Math.max(0, i - 1)], next = pts[Math.min(n, i + 1)], dx = next.x - prev.x, dz = next.z - prev.z, len = Math.hypot(dx, dz) || 1;
    let w = width / 2 * (1 + .22 * Math.sin(i * .9 - s * 7));
    if (flare) w *= 1 + .9 * clamp(1 - i / 3);
    if (point) w *= clamp((n - i) / 4 + .12);
    if (pointStart) w *= clamp(i / 4 + .12);
    const f = w + .035, nx = -dz / len, nz = dx / len;
    sides[0].push({ x: p.x + nx * f, z: p.z + nz * f }); sides[1].push({ x: p.x - nx * f, z: p.z - nz * f });
    sides[2].push({ x: p.x + nx * w, z: p.z + nz * w }); sides[3].push({ x: p.x - nx * w, z: p.z - nz * w });
  });
  band(`${key} fringe`, sides[0], sides[1], Fringe.withAlpha(.5 * alpha), layer);
  band(key, sides[2], sides[3], Shade.withAlpha(.94 * alpha), layer + .002);
}

// A line that has been cut at share cutU of from..to, age seconds ago: the near half runs back to
// its root, the far half runs on into the target, and shreds fly from the cut.
export const SnapTime = .4;
export function brokenLine(key, from, to, cutU, age, width, s, sway) {
  const r = smooth(age / SnapTime);
  if (r < 1) {
    shadowLine(`${key} near`, path(from, to, 0, cutU * (1 - r), s, sway), width, 1, s);
    shadowLine(`${key} far`, path(from, to, cutU + (1 - cutU) * r, 1, s, sway), width, 1, s, { flare: false, point: false, pointStart: true });
  }
  shreds(`${key} cut`, pointOn(from, to, cutU), age, 9);
}

// A pool of shadow under something: an uneven blob, flatter north-south, that keeps moving.
export function pool(key, pos, radius, alpha, s, layer = LineLayer) {
  if (radius <= 0 || alpha <= 0) return;
  for (const [name, grow, colour, up] of [['fringe', .06, Fringe.withAlpha(.5 * alpha), 0], ['core', 0, Shade.withAlpha(.94 * alpha), .002]]) {
    const v = [pos.x, pos.z], t = [], N = 24;
    for (let i = 0; i <= N; i++) {
      const a = i / N * TAU, r = (radius + grow) * (1 + .12 * Math.sin(a * 3 + s * 2) + .07 * Math.sin(a * 5 - s * 3));
      v.push(pos.x + Math.cos(a) * r, pos.z + Math.sin(a) * r * .62);
      if (i) t.push(0, i, i + 1);
    }
    mesh(`${key} ${name}`).setFlat(v, t);
    draw(mesh(`${key} ${name}`), 0, layer + up, 0, 1, 1, 0, colour);
  }
}

// Threads of shadow climbing a held body from the pool at its feet. amount 0..1, reach in cells up.
export function grip(key, pos, amount, s, count = 4, reach = .55, big = 1) {
  if (amount <= 0) return;
  for (let i = 0; i < count; i++) {
    const phase = i * TAU / count + s * .8, pts = [];
    for (let j = 0; j <= 8; j++) { const h = amount * reach * j / 8; pts.push({ x: pos.x + Math.sin(phase + h * 6) * .2 * big * (1 - h * .3), z: pos.z + .03 + h * Lift }); }
    trail(`${key} thread ${i}`, pts, .075 * big, Shade.withAlpha(.92), Y + .01);
  }
}

// Shreds of shadow flying off a cut, a released body or a double that ends. They lift a little and fade.
export function shreds(key, pos, age, count = 8, life = .45, spread = .7) {
  if (age < 0 || age > life) return;
  for (let i = 0; i < count; i++) {
    const u = age / (life * (.6 + .4 * rand(i + 5))); if (u > 1) continue;
    const a = i * 2.399 + rand(i) * .9, d = spread * (.15 + u * (.5 + rand(i + 11) * .6)), c = Math.cos(a), z = Math.sin(a) * .7;
    const tip = { x: pos.x + c * d, z: pos.z + z * d + u * .25 }, tail = { x: tip.x - c * .2, z: tip.z - z * .2 - .05 };
    trail(`${key} shred ${i}`, [tail, pointOn(tail, tip, .5), tip], .09 * (1 - u), Shade.withAlpha(.9 * (1 - u)), Y + .02);
  }
}

// Dust kicked up by a body pulled over the ground. age in seconds.
export function scuff(pos, age, size = 1) {
  if (age < 0 || age > .5) return;
  const u = age / .5;
  sprite({ x: pos.x, z: pos.z + .05 + u * .12 }, (.35 + u * .5) * size, (.25 + u * .35) * size, Dust.withAlpha(.5 * Math.sin(u * Math.PI)), puff, Y + .005);
}

// The range ring at the rule's true radius: the ability's full range times the light level.
export function rangeRing(pos, fullRange, level, alpha) {
  thinRing(`range ring ${pos.x.toFixed(2)} ${pos.z.toFixed(2)}`, pos, level < DarkBelow ? 0 : fullRange * level, RangeTint.withAlpha(.6 * alpha), Floor + .012);
}
// A ring of one width whatever its radius (circle() from the impact lib gets wider as it grows).
export function thinRing(key, pos, r, colour, layer, width = .06) {
  if (r <= 0 || colour.a <= 0) return;
  const ring = (w) => { const pts = []; for (let i = 0; i <= 96; i++) { const a = i / 96 * TAU; pts.push({ x: pos.x + Math.cos(a) * (r + w), z: pos.z + Math.sin(a) * (r + w) }); } return pts; };
  band(key, ring(-width / 2), ring(width / 2), colour, layer);
}

// Cells walked t seconds into count steps of stepTime with a pause after each. snap > 1 finishes
// each step early, which is how a dragged body lurches where the carrier walks.
export function walked(t, stepTime, pause, count, snap = 1) {
  if (t <= 0) return 0;
  const per = stepTime + pause, k = Math.floor(t / per);
  return k >= count ? count : k + smooth(clamp((t - k * per) / stepTime * snap));
}

// An ordinary explosion stand-in for a grenade: flash, ring, smoke. Not the kit's effect.
export function blast(pos, age, radius) {
  if (age < 0 || age > .9) return;
  const u = age / .9, flash = Math.max(0, 1 - age / .14);
  sprite(pos, radius * 3.2, radius * 3.2, Flame.withAlpha(flash), glow, LightLayer + .01);
  sprite(pos, radius * 2, radius * 2, Ember.withAlpha(.8 * (1 - smooth(u * 1.6))), glow, LightLayer + .009);
  circle(pos, radius * smooth(age / .18), .7 * (1 - u), Floor + .03, Flame);
  for (let i = 0; i < 12; i++) {
    const a = i * 2.399, d = radius * (.2 + u * (.5 + rand(i) * .7));
    sprite({ x: pos.x + Math.cos(a) * d, z: pos.z + Math.sin(a) * d * .8 + u * .4 }, .7 + u * 1.1, .6 + u * .9, new Color(.2, .18, .17, .6 * Math.sin(u * Math.PI)), puff, Y + .1);
  }
  sprite(pos, radius * 1.6, radius * 1.1, Shade.withAlpha(.4 * smooth(age / .2)), soft, Floor + .005);
}

const PalmLong = .22, PalmWide = .19, PalmBack = -.04, FingerWidth = .05, Arch = .5;
// One finger: [along, across] of its knuckle on the palm, spread when flat (degrees), length, the
// share of the length in each joint, and how far each joint folds at full curl (degrees).
const FingerSet = [[.1, -.145, -26, .32], [.16, -.05, -8, .4], [.16, .05, 8, .37], [.1, .145, 26, .29]].map(f => [...f, [.45, .32, .23], [40, 65, 45]]);
const Thumb = [-.08, .16, 64, .27, [.55, .45], [30, 70]];
// The hand, as the source draws its shadow hands: a flat black hand with a wrist, a palm, four
// jointed fingers and a thumb, not a splash. Everything is laid out along the way the fingers
// point (deg) and across it. open 0..1 grows the fingers out of the palm, spread wide. curl 0..1
// folds each finger at its joints up and back over the item and brings the fingers together; a
// folded joint is shorter on the ground and Arch of its height is drawn as a shift north. mirror
// makes it the other hand. layer puts the whole hand on one layer, for a hand lying on a body.
export function hand(key, c, deg, open, curl, s, size = 1, { mirror = false, layer: onto = null } = {}) {
  if (open <= 0) return;
  open *= size;
  const r = deg * Mathf.Deg2Rad, fx = Math.cos(r), fz = Math.sin(r);
  const side = mirror ? -1 : 1, local = (along, across) => ({ x: c.x + (along * fx - across * side * fz) * open, z: c.z + (along * fz + across * side * fx) * open });
  const floor = onto ?? LineLayer + .006, over = onto != null ? onto + .002 : curl > .15 ? Y + .01 : LineLayer + .008;
  for (const [name, grow, colour, up] of [['fringe', .03, Fringe.withAlpha(.5), 0], ['core', 0, Shade.withAlpha(.95), .001]]) {
    const rim = [], centre = [];
    for (let i = 0; i <= 20; i++) { const a = i / 20 * Math.PI * 2; rim.push(local(PalmBack + (PalmLong + grow) * Math.cos(a), (PalmWide + grow) * Math.sin(a))); centre.push(local(PalmBack, 0)); }
    band(`${key} palm ${name}`, centre, rim, colour, floor + up);
    band(`${key} wrist ${name}`, [local(-.55, .06 + grow), local(-.18, .12 + grow)], [local(-.55, -.06 - grow), local(-.18, -.12 - grow)], colour, floor + up);
  }
  [...FingerSet, Thumb].forEach(([along, across, spread, length, joints, folds], i) => {
    const a = (deg + side * spread * (1 - .75 * curl)) * Mathf.Deg2Rad, dx = Math.cos(a), dz = Math.sin(a), layer = over + i * .0006;
    let ground = local(along, across), bend = 0, h = 0, from = ground;
    draw(disc, from.x, layer + .0003, from.z, FingerWidth * size, FingerWidth * size, 0, Shade.withAlpha(.95));
    joints.forEach((share, j) => {
      bend += folds[j] * curl * Mathf.Deg2Rad;
      const len = length * share * open, w = FingerWidth * size * (1 - j * .12);
      ground = { x: ground.x + dx * len * Math.cos(bend), z: ground.z + dz * len * Math.cos(bend) }; h += len * Math.sin(bend) * Arch;
      const to = { x: ground.x, z: ground.z + h * Lift }, mx = (from.x + to.x) / 2, mz = (from.z + to.z) / 2;
      const long = Math.hypot(to.x - from.x, to.z - from.z), turn = -Math.atan2(to.z - from.z, to.x - from.x) * 180 / Math.PI;
      draw(MeshPool.plane10, mx, layer + j * .0001, mz, long + .05, w * 2 + .05, turn, Fringe.withAlpha(.5));
      draw(disc, to.x, layer + j * .0001, to.z, w + .025, w + .025, 0, Fringe.withAlpha(.5));
      draw(MeshPool.plane10, mx, layer + .0003 + j * .0001, mz, long, w * 2, turn, Shade.withAlpha(.95));
      draw(disc, to.x, layer + .0003 + j * .0001, to.z, w, w, 0, Shade.withAlpha(.95));
      from = to;
    });
  });
}
