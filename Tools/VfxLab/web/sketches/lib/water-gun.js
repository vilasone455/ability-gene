// Shared drawing for the Water Gun kit: the gun, the water bag worn on the back with its visible
// fill level, the hose between them, water streams, splashes, drips and puddles. Stream Shot and
// Hydro Pump both draw the weapon through drawWaterGun() so the two stay identical, and a C# port
// makes one class of it.
//
// The bag is a level cylinder on the pawn's back (its top is the same circle shifted north by
// BagH * Lift), so its fill level reads the same from every facing: the water is a shorter side
// band from the base up, and a lit surface disc. The gun lies along the aim and turns with it.
// Nothing here needs a per-facing method. When the back is on the far (north) side of the pawn
// the bag draws under the pawn layer so the pawn stands in front of it.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, ShaderDatabase } from '../../js/engine.js';
import { draw } from './six-paths-solid.js';
import { Body, Y, Floor, Lift, sprite, band, circle, soft, glow, rand } from './six-paths-impact.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
export const disc = Meshes.disc(40, 'water gun disc');
export const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
export const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
export const Skin = new Color(.83, .70, .54), Pale = new Color(1, .96, .85), Dust = new Color(.80, .74, .63);
export const Water = new Color(.24, .55, .88), WaterLit = new Color(.62, .86, 1), WaterDark = new Color(.10, .28, .56), Foam = new Color(.92, .97, 1);
export const Bag = new Color(.40, .62, .80, .45), BagRim = new Color(.55, .78, .95), BagDark = new Color(.16, .30, .46, .55);
export const Gun = new Color(.15, .17, .21), GunLit = new Color(.30, .33, .40), GunDark = new Color(.05, .05, .07), Accent = new Color(.95, .60, .16);
export const Steel = new Color(.60, .63, .68), Flame = new Color(1, .55, .12), FlameCore = new Color(1, .90, .55), Steam = new Color(.95, .97, 1);
// Decided looks and the rule's fixed numbers.
export const HandH = .5;                           // gun height when aimed, cells
export const BagCap = 30;                          // units the bag holds
export const BagR = .20, BagH = .55, BagBase = .28; // bag radius, height, and its bottom above the floor
export const BagAlong = -.30, BagAcross = .04;     // where the bag hangs, from the caster's feet (behind the aim)
export const GripAlong = .12, MuzzleAlong = .72;   // the aimed gun along the aim from the caster's feet
export const HoseW = .045;
export const Lead = .2, Raise = .25, Lower = .3, Tail = .2;   // rest, raising the gun, lowering it, rest again
export const bump = x => (x >= 0 && x <= 1) ? Math.sin(x * Math.PI) : 0;

// The aim frame: along the cast direction, across it, h cells up. cast() is the same point's shadow.
export function frame(aimDeg, sun) {
  const a = aimDeg * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a);
  return {
    ca, sa, a,
    place: (base, along, across, h = 0) => ({ x: base.x + along * ca - across * sa, z: base.z + along * sa + across * ca + h * Lift }),
    cast: (base, along, across, h = 0) => ({ x: base.x + along * ca - across * sa + sun.x * h, z: base.z + along * sa + across * ca + sun.z * h }),
  };
}
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
// Stand-in pawn: two discs and a soft shadow. downed > 0 lays it on its side, tint blends the body
// toward the soaked colour.
export function figure(pos, colour, sun, strength, { downed = 0, tint = 0 } = {}) {
  const c = tint > 0 ? Color.Lerp(colour, WaterDark, tint * .45) : colour;
  sprite({ x: pos.x + sun.x * .45 * (1 - downed), z: pos.z + sun.z * .45 * (1 - downed) }, .85, .4, Body.withAlpha(strength), soft, shadowLayer);
  if (downed >= 1) {
    draw(disc, pos.x + .05, pawnLayer, pos.z + .12, .32, .2, 0, c);
    draw(disc, pos.x - .34, pawnLayer + .002, pos.z + .14, .16, .17, 0, Skin);
    return;
  }
  const bw = lerp(.22, .32, downed), bh = lerp(.32, .2, downed), hz = lerp(.58, .14, downed), hx = lerp(0, -.34, downed);
  draw(disc, pos.x, pawnLayer, pos.z + lerp(.18, .12, downed), bw, bh, 0, c);
  draw(disc, pos.x + hx, pawnLayer + .002, pos.z + hz, .16, .17, 0, Skin);
}
// Fire on a stand-in pawn: three flickering tongues with a glow. strength 0..1.
export function flames(pos, s, strength, layer = Y + .03) {
  if (strength <= 0) return;
  sprite({ x: pos.x, z: pos.z + .45 }, 1.0 * strength, .9 * strength, Flame.withAlpha(.35 * strength), glow, layer);
  for (let i = 0; i < 3; i++) {
    const ph = s * (9 + i * 2.3) + i * 2.1, sway = Math.sin(ph) * .06, h = .3 + .18 * (.5 + .5 * Math.sin(ph * 1.3 + i));
    const x = pos.x + (i - 1) * .13 + sway, z = pos.z + .2 + h * Lift;
    sprite({ x, z }, .26 * strength, .42 * strength, Flame.withAlpha(.9 * strength), soft, layer + .002);
    sprite({ x, z: z - .06 }, .12 * strength, .22 * strength, FlameCore.withAlpha(.9 * strength), soft, layer + .004);
  }
}
// Steam rising after fire is put out: puffs that climb and fade over `life` seconds from `age`.
export function steam(pos, age, life = .9, seed = 0) {
  if (age < 0 || age > life) return;
  for (let i = 0; i < 6; i++) {
    const u = clamp((age - i * .06) / (life * .8));
    if (u <= 0 || u >= 1) continue;
    const x = pos.x + (rand(i + seed + 700) - .5) * .5, z = pos.z + .3 + (.2 + u * 1.1) * Lift;
    sprite({ x, z }, .3 + u * .5, .25 + u * .45, Steam.withAlpha(Math.sin(u * Math.PI) * .6), puff, Y + .05);
  }
}
// A puddle on the floor that grows over 0.3 s and stays. size in cells.
export function puddle(pos, age, size = 1, alpha = .5) {
  if (age < 0) return;
  const g = smooth(age / .3);
  sprite(pos, 1.2 * size * g, .8 * size * g, WaterDark.withAlpha(alpha * .5), soft, Floor + .02);
  sprite(pos, .9 * size * g, .55 * size * g, Water.withAlpha(alpha), soft, Floor + .03);
  sprite({ x: pos.x - .1 * size, z: pos.z + .08 * size }, .4 * size * g, .18 * size * g, WaterLit.withAlpha(alpha * .5), soft, Floor + .04);
}
// Drips falling from a soaked pawn: three drops on a loop from body height to the floor.
export function drips(pos, age, fade, seed = 0) {
  if (age < 0 || fade <= 0) return;
  for (let i = 0; i < 3; i++) {
    const u = ((age * 1.8 + rand(i + seed + 800)) % 1), x = pos.x + (rand(i + seed + 810) - .5) * .4, h = .45 * (1 - u * u);
    sprite({ x, z: pos.z + .1 + h * Lift }, .07, .11, WaterLit.withAlpha(.9 * fade * (1 - u * .5)), soft, Y + .03);
  }
}
// Splash at a hit point: flash, ring, droplets thrown up and out, foam. big scales it.
export function splash(pos, age, big = 1, seed = 0) {
  if (age < 0 || age > .6) return;
  sprite({ x: pos.x, z: pos.z + .25 * big }, 1.3 * big, .9 * big, WaterLit.withAlpha(Math.max(0, 1 - age / .1) * .8), glow, Y + .04);
  circle(pos, .2 + age * 2.0 * big, (1 - age / .6) * .5, Floor + .05, WaterLit);
  for (let i = 0; i < 14; i++) {
    const life = .25 + rand(i + seed + 900) * .25, u = age / life;
    if (u > 1) continue;
    const th = rand(i + seed + 910) * TAU, far = u * (.3 + rand(i + seed + 920) * .8) * big, h = Math.sin(u * Math.PI) * (.3 + rand(i + seed + 930) * .5) * big;
    sprite({ x: pos.x + Math.cos(th) * far, z: pos.z + Math.sin(th) * far * .6 + h * Lift }, .08 + .05 * big, .11 + .06 * big,
      (i % 3 ? WaterLit : Foam).withAlpha((1 - u) * .95), soft, Y + .035);
  }
}
// A water stream between two screen points, from u0 to u1 of the way (tail and head), with a
// slight sag. Drawn as water, not as a rod: a chain of soft translucent discs whose width pulses
// along the flow, a thin wavy core, bright glints riding along it, droplets breaking off ahead of
// the head and at the sides, and faint mist. width in cells at the nozzle, s is the clip time.
export function stream(key, a, b, u0, u1, width, s, layer = Y + .02, sag = .05, seed = 0) {
  if (u1 <= u0) return;
  const dx = b.x - a.x, dz = b.z - a.z, L = Math.hypot(dx, dz) || 1, nx = -dz / L, nz = dx / L;
  const at = u => ({ x: lerp(a.x, b.x, u), z: lerp(a.z, b.z, u) - Math.sin(u * Math.PI) * sag * Lift });
  const flow = s * 3.2 + seed * .37;                                     // cells travelled, for moving detail
  const wave = u => 1 + .35 * Math.sin((u * L - flow * 1.0) * 6.0);      // bulges moving with the flow
  const wobble = u => .06 * width / .07 * Math.sin((u * L - flow * 1.4) * 4.0 + seed);   // the core sways
  const span = u1 - u0, tailFade = u => clamp((u - u0) / Math.max(.05, span * .3));     // tapers to nothing at the tail
  const headFade = u => 1 - .5 * clamp((u - (u1 - .15)) / .15);
  const w = u => width * (1 + .5 * u) * wave(u) * tailFade(u) * headFade(u);

  // Body: overlapping soft discs every 0.16 cells. Two passes: a wide faint one and the body.
  const n = Math.max(2, Math.ceil(L * span / .10));
  for (let i = 0; i <= n; i++) {
    const u = lerp(u0, u1, i / n), q = at(u), ww = w(u), off = wobble(u);
    const pos = { x: q.x + nx * off, z: q.z + nz * off };
    sprite(pos, ww * 8.0, ww * 8.0, WaterLit.withAlpha(.12), soft, layer);
    sprite(pos, ww * 5.2, ww * 5.2, Water.withAlpha(.55), soft, layer + .002);
    sprite(pos, ww * 3.0, ww * 3.0, WaterLit.withAlpha(.45), glow, layer + .004);
  }
  // Glints: short bright dashes moving along the core, additive.
  for (let i = 0; i < 9; i++) {
    const u = ((flow * .9 + i / 9 + rand(i + seed + 940) * .05) % 1);
    if (u < u0 + .04 || u > u1 - .02) continue;
    const q = at(u), off = wobble(u) + (rand(i + seed + 945) - .5) * width * .8, ang = Math.atan2(dz, dx) / Mathf.Deg2Rad;
    sprite({ x: q.x + nx * off, z: q.z + nz * off }, .22 + .18 * width / .07, .05, Foam.withAlpha(.85), glow, layer + .006, ang);
  }
  // Head: the front breaks into blobs that run ahead and spread, then droplets.
  const head = at(u1);
  if (u1 < 1) {
    for (let i = 0; i < 5; i++) {
      const lead = .10 + i * .09, u = Math.min(1, u1 + lead / L);
      const q = at(u), off = (rand(i + seed + 950) - .5) * width * (1.5 + i * .8) + wobble(u) * .5;
      const size = width * (3.4 - i * .45);
      sprite({ x: q.x + nx * off, z: q.z + nz * off }, size * 1.8, size * 1.8, Water.withAlpha(.6), soft, layer + .003);
      sprite({ x: q.x + nx * off, z: q.z + nz * off }, size * 1.0, size * 1.0, WaterLit.withAlpha(.8), glow, layer + .005);
    }
  } else {
    sprite(head, width * 4, width * 4, WaterLit.withAlpha(.5), glow, layer + .005);   // pooling at the hit
  }
  // Droplets and mist breaking off the sides, further along the flow more of them.
  for (let i = 0; i < 10; i++) {
    const u = ((flow * .6 + rand(i + seed + 960)) % 1);
    if (u < u0 + .1 || u > u1) continue;
    const side = rand(i + seed + 970) < .5 ? -1 : 1, life = (flow * .6 + rand(i + seed + 960)) % 1;
    const off = side * (width * 1.6 + life * .25 * (1 + u)), q = at(u);
    sprite({ x: q.x + nx * off, z: q.z + nz * off + life * .05 }, .06, .08, WaterLit.withAlpha(.8 * (1 - life)), soft, layer + .004);
    if (i % 3 === 0) sprite({ x: q.x + nx * off * 1.5, z: q.z + nz * off * 1.5 }, .28 + u * .2, .22 + u * .15, Foam.withAlpha(.12 * (1 - life)), puff, layer + .001);
  }
}

// Draws the whole weapon for one frame.
//   raise    0..1, how far the gun is up and aimed (0 = rest pose)
//   units    water in the bag, 0..BagCap
//   slosh    0..1, how much the surface bobs
//   squeeze  0..1, the bag pinched in (pump wind-up)
//   recoil   cells the gun is pushed back along the aim
// Returns the muzzle point on screen and on the ground.
export function drawWaterGun(f, caster, s, { raise, units, slosh, squeeze, recoil = 0, actors, sun, strength }) {
  const { ca, sa, place, cast } = f;
  const lift = smooth(raise);
  const level = clamp(units / BagCap);

  // The bag on the back.
  const R = BagR * (1 - .12 * squeeze), H = BagH * (1 + .06 * squeeze);
  const B = place(caster, BagAlong, BagAcross, BagBase);
  const behind = B.z > caster.z + .06;
  const bagLayer = behind ? pawnLayer - .03 : Y + .001;
  sprite(cast(caster, BagAlong, BagAcross, BagBase + H * .5), R * 2.6, R * 1.5, Body.withAlpha(strength * .6), soft, shadowLayer);
  const side = (r, h0, h1, n = 20) => {
    const A = [], C = [];
    for (let i = 0; i <= n; i++) {
      const th = Math.PI + i / n * Math.PI, x = B.x + Math.cos(th) * r, z = B.z + Math.sin(th) * r;
      A.push({ x, z: z + h0 * Lift }); C.push({ x, z: z + h1 * Lift });
    }
    return [A, C];
  };
  const wl = level * H, bob = slosh * .02 * Math.sin(s * 11);
  if (level > 0) {
    const [A, C] = side(R * .93, .02, wl + bob);
    band('water gun bag water', A, C, Water, bagLayer);
    const [A2, C2] = side(R * .93, .02, wl + bob, 8);
    // lit stripe on the south face of the water
    const A3 = [], C3 = [];
    for (let i = 0; i <= 8; i++) {
      const th = Math.PI * 1.5 + (i / 8 - .5) * .8, x = B.x + Math.cos(th) * R * .93, z = B.z + Math.sin(th) * R * .93;
      A3.push({ x, z: z + .04 * Lift }); C3.push({ x, z: z + (wl + bob) * Lift * .96 });
    }
    band('water gun bag water lit', A3, C3, WaterLit.withAlpha(.55), bagLayer + .002);
    draw(disc, B.x, bagLayer + .004, B.z + (wl + bob) * Lift, R * .93, R * .93, 0, WaterLit);
  }
  const [SA, SC] = side(R, 0, H);
  band('water gun bag side', SA, SC, BagDark, bagLayer + .006);
  draw(disc, B.x, bagLayer + .008, B.z + H * Lift, R, R, 0, Bag);
  circle({ x: B.x, z: B.z + H * Lift }, R, .9, bagLayer + .010, BagRim);
  circle({ x: B.x, z: B.z }, R, .5, bagLayer + .010, BagRim);
  // Two straps over the shoulders to the pawn's chest.
  for (const sx of [-1, 1]) {
    const pts = [place(caster, BagAlong + .05, BagAcross + sx * R * .6, BagBase + H * .9), place(caster, -.05, sx * .21, .62), place(caster, .10, sx * .17, .40)];
    tube(`water gun strap ${sx}`, pts, () => .03, GunDark, behind ? Y + .001 : bagLayer + .012);
  }
  // Cap on top with a level mark: a small dark disc.
  draw(disc, B.x + R * .35, bagLayer + .011, B.z + H * Lift + .02, .05, .05, 0, GunDark);

  // The gun: at rest it hangs low across the body; raised it points along the aim at hand height.
  const GA = [lerp(.02, GripAlong, lift) - recoil, lerp(-.22, 0, lift), lerp(.38, HandH, lift)];
  const GB = [lerp(.42, MuzzleAlong, lift) - recoil, lerp(-.34, 0, lift), lerp(.16, HandH, lift)];
  const ga = place(caster, GA[0], GA[1], GA[2]), gb = place(caster, GB[0], GB[1], GB[2]);
  const gunDeg = Math.atan2(gb.z - ga.z, gb.x - ga.x) / Mathf.Deg2Rad;
  const len = Math.hypot(gb.x - ga.x, gb.z - ga.z);
  const mid = u => ({ x: lerp(ga.x, gb.x, u), z: lerp(ga.z, gb.z, u) });
  const gunLayer = Y + .014;
  const shA = cast(caster, GA[0], GA[1], GA[2]), shB = cast(caster, GB[0], GB[1], GB[2]);
  tube('water gun shadow', [shA, shB], () => .09, Body.withAlpha(strength * .7), shadowLayer);
  // body (rear 60 %), pump grip below it, nozzle (front 40 %)
  rect('water gun body outline', mid(.32), len * .62 + .04, .17, gunDeg, GunDark, gunLayer);
  rect('water gun body', mid(.32), len * .62, .13, gunDeg, Gun, gunLayer + .002);
  rect('water gun body lit', { x: mid(.32).x, z: mid(.32).z + .035 }, len * .56, .035, gunDeg, GunLit, gunLayer + .004);
  rect('water gun accent', mid(.30), len * .30, .05, gunDeg, Accent, gunLayer + .005);
  const pumpC = { x: mid(.52).x, z: mid(.52).z - .07 };
  rect('water gun pump outline', pumpC, .16, .09, gunDeg, GunDark, gunLayer - .002);
  rect('water gun pump', pumpC, .13, .06, gunDeg, Accent, gunLayer - .001);
  rect('water gun nozzle outline', mid(.80), len * .40 + .02, .09, gunDeg, GunDark, gunLayer);
  rect('water gun nozzle', mid(.80), len * .40, .06, gunDeg, Steel, gunLayer + .002);
  draw(disc, gb.x, gunLayer + .006, gb.z, .04, .04, 0, WaterDark);   // the bore
  if (actors) {
    draw(disc, ga.x, gunLayer + .008, ga.z, .07, .07, 0, Skin);        // rear hand
    draw(disc, pumpC.x, gunLayer + .008, pumpC.z, .07, .07, 0, Skin);  // pump hand
  }

  // The hose from the bag's top to the gun's grip, over the shoulder.
  const P0 = [BagAlong, BagAcross, BagBase + H], P2 = GA, P1 = [(BagAlong + GA[0]) / 2 - .05, BagAcross * .5 + .12, HandH + .3];
  const hosePts = [];
  for (let i = 0; i <= 18; i++) {
    const u = i / 18, w0 = (1 - u) * (1 - u), w1 = 2 * (1 - u) * u, w2 = u * u;
    hosePts.push(place(caster, w0 * P0[0] + w1 * P1[0] + w2 * P2[0], w0 * P0[1] + w1 * P1[1] + w2 * P2[1], w0 * P0[2] + w1 * P1[2] + w2 * P2[2]));
  }
  tube('water gun hose outline', hosePts, () => HoseW + .02, GunDark, Y + .008);
  tube('water gun hose body', hosePts, () => HoseW, Gun, Y + .010);
  tube('water gun hose lit', hosePts, () => HoseW, GunLit, Y + .012, .1, .55);

  return { muzzleS: gb, muzzleG: place(caster, GB[0] , GB[1]), gunDeg, level };
}
