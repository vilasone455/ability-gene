// Unlimited Void as a pocket map: the pieces shared by "Unlimited Void: open and return" (the home
// map) and "Unlimited Void: inside" (the pocket map). Not a sketch itself, so it is not listed in
// sketches/index.js. The dome and cutscene sketches keep using lib/gojo.js as they were.
//
// Direction (2026-09-23): everyone within the radius is taken into Gojo's domain, a small pocket map
// of its own. From outside only a black ball the size of a basketball shows (manga ch. 227-228).
// Allies are taken too (the Shibuya dilemma) and Gojo spares one by touching it before the domain
// ends. Androids and mechanoids have no living brain, so they are taken but not frozen. The cast,
// Gojo's route and the immune pawns' walk live here so both sketches agree on who comes back how.
//
// Drawing: level circles, quads, strips and flat polygons, so no per-facing method. The space under
// the void is drawn in the BelowTerrain layer under a see-through Terrain layer, as the Infinity
// Castle's depth rooms are: in game the void terrain needs a see-through texture and the mod draws
// the space under it. Textures: SoftDisc, Puff, white, and one lab texture (lab/gojo-splatter, the
// white ink patches) still to be made a PNG.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../../js/engine.js';
import { registerLabTexture, pixels, fbm, hash } from '../../js/standins.js';
import { draw, mesh } from './six-paths-solid.js';
import { Y, Floor, Lift, sprite, glow, soft, rand } from './six-paths-impact.js';
import { stunStars } from './goku.js';
import {
  domeOutline, pawn, ringAt, glint, streak, strip, line, whiteGlow, Ink, Skin, Ally, White, Ice, Blue, Void,
  Violet, Pink, Gold, EyeBlue, pawnLayer, shadowLayer, smooth, clamp,
} from './gojo.js';

const disc = Meshes.disc(40, 'unlimited void pocket disc');
const plane = MeshPool.plane10;
const TAU = Math.PI * 2, Deg = Mathf.Deg2Rad;

// ---- colours ----------------------------------------------------------------------------------
export const Frozen = new Color(.62, .8, 1);                     // tint on a frozen pawn
export const Navy = new Color(.02, .03, .08), Haze = new Color(.16, .55, .62), Indigo = new Color(.26, .22, .72), Dusk = new Color(.45, .28, .72);
export const MechDark = new Color(.17, .18, .21), MechLit = new Color(.36, .38, .43), MechEye = new Color(1, .22, .16);
export const Steel = new Color(.78, .82, .87), Visor = new Color(.4, .92, 1);

// ---- altitudes --------------------------------------------------------------------------------
// The space is under the terrain; the see-through void terrain lies over it; pawns and effects as usual.
const layer = n => AltitudeLayer[n].AltitudeFor();
export const V = { back: layer('BelowTerrain'), deep: layer('BelowTerrain') + .01, fog: layer('Terrain') };

// ---- the white ink patches (anime ep. 7): a ragged blot with droplets thrown round it -----------
registerLabTexture('lab/gojo-splatter', () => pixels(256, (u, v) => {
  const r = Math.hypot(u - .5, v - .5) * 2;
  if (r > .96) return [1, 1, 1, 0];
  const n = fbm(u * 4, v * 4, 313, 4, 4), m = fbm(u * 11, v * 11, 919, 3, 11);
  let a = clamp((.6 - r + (n - .5) * .75 + (m - .5) * .2) * 14);
  for (let i = 0; i < 16; i++) {
    const ang = hash(i, 1, 77) * TAU, d = .3 + .14 * hash(i, 2, 77), rr = .012 + .026 * hash(i, 3, 77);
    a = Math.max(a, clamp((rr - Math.hypot(u - .5 - Math.cos(ang) * d, v - .5 - Math.sin(ang) * d)) / .006));
  }
  return [1, 1, 1, a * clamp((.96 - r) / .06)];
}));
const splat = MaterialPool.MatFrom('lab/gojo-splatter', ShaderDatabase.MoteGlow);

// ---- the rule's numbers (placeholders, XML fields in game) --------------------------------------
export const Hold = 10;                    // seconds the domain lasts
export const ActFrom = .4;                 // Gojo and the immune can act once the white has mostly cleared
export const Reach = .75;                  // how close Gojo stands to touch or strike
export const Walk = { gojo: 4.6, mech: 2.2, android: 3 };      // cells a second
export const Strike = { first: .2, every: 1.7, toDown: 4 };    // Gojo's blows on a frozen raider (a melee cooldown)

// The stand-in cast: offsets in cells from where Gojo stands when he casts.
export const Cast = {
  raiders: [[2.4, 1.2], [-2.0, 2.6], [3.6, -2.2], [-4.4, -1.2], [1.2, 5.2], [-3.2, -4.8]],
  colonists: [[-1.6, -2.4], [6.8, 4.2]],   // one near Gojo, one far
  android: [4.6, -5.0],                    // a colony android: taken, not frozen
  mech: [-6.4, 3.4],                       // a hostile mechanoid: taken, not frozen
};

// Everyone in the scene; taken = within the radius.
export function castList(scenario, radius) {
  const list = Cast.raiders.map((at, i) => ({ id: `r${i}`, kind: 'raider', at }));
  if (scenario === 'mixed') {
    Cast.colonists.forEach((at, i) => list.push({ id: `c${i}`, kind: 'colonist', at }));
    list.push({ id: 'android', kind: 'android', at: Cast.android }, { id: 'mech', kind: 'mech', at: Cast.mech });
  }
  list.forEach(g => { g.taken = Math.hypot(g.at[0], g.at[1]) <= radius; g.immune = g.kind === 'android' || g.kind === 'mech'; });
  return list;
}

// What Gojo does inside, as timed steps from when he can act (u = 0). "touch allies first": every
// caught colonist, nearest first, then blows on the raider nearest him until it goes down. "attack
// first": blows on raider 2 until it goes down, then the colonists. budget = seconds he has. Returns
// the steps, who was spared (id -> time) and who was beaten down (id -> time) within the budget.
export function plan({ scenario, order, radius, speed = Walk.gojo, touch = .3, budget = Hold - ActFrom }) {
  const cast = castList(scenario, radius).filter(g => g.taken);
  const allies = cast.filter(g => g.kind === 'colonist'), raiders = cast.filter(g => g.kind === 'raider');
  const steps = [];
  let t = 0, here = { x: 0, z: 0 };
  const dist = g => Math.hypot(g.at[0] - here.x, g.at[1] - here.z);
  const go = (g, kind) => {
    const dx = g.at[0] - here.x, dz = g.at[1] - here.z, d = Math.hypot(dx, dz) || 1, walk = Math.max(0, d - Reach);
    const to = { x: here.x + dx / d * walk, z: here.z + dz / d * walk };
    if (walk > 0) steps.push({ kind: 'walk', t0: t, t1: t + walk / speed, from: here, to });
    t += walk / speed; here = to;
    const len = kind === 'touch' ? touch : Strike.first + Strike.every * (Strike.toDown - 1) + .3;
    steps.push({ kind, t0: t, t1: t + len, target: g, from: here });
    t += len;
  };
  const touchAll = () => { const left = allies.slice(); while (left.length) { left.sort((a, b) => dist(a) - dist(b)); go(left.shift(), 'touch'); } };
  const strikeOne = () => {
    const r = order === 'attack first' ? (raiders.find(g => g.id === 'r2') ?? raiders[0]) : raiders.slice().sort((a, b) => dist(a) - dist(b))[0];
    if (r) go(r, 'strike');
  };
  if (order === 'attack first') { strikeOne(); touchAll(); } else { touchAll(); strikeOne(); }
  const spared = new Map(), downed = new Map();
  steps.forEach(st => {
    if (st.kind === 'touch' && st.t1 <= budget) spared.set(st.target.id, st.t1);
    if (st.kind === 'strike') { const last = lastBlow(st); if (last <= budget) downed.set(st.target.id, last); }
  });
  return { steps, spared, downed, budget, cast };
}
export const blows = st => Array.from({ length: Strike.toDown }, (_, k) => st.t0 + Strike.first + Strike.every * k);
const lastBlow = st => st.t0 + Strike.first + Strike.every * (Strike.toDown - 1);

// Where Gojo stands at u (offset from his cast cell), and the touch or strike going on, if any.
export function gojoAt(pl, u) {
  u = Math.min(u, pl.budget);
  let pos = { x: 0, z: 0 };
  for (const st of pl.steps) {
    if (u < st.t0) break;
    if (st.kind === 'walk') { const f = clamp((u - st.t0) / (st.t1 - st.t0)); pos = { x: st.from.x + (st.to.x - st.from.x) * f, z: st.from.z + (st.to.z - st.from.z) * f }; }
    else pos = st.from;
  }
  return pos;
}
export function gojoDoing(pl, u) {
  if (u >= pl.budget) return null;
  for (const st of pl.steps) if (st.kind !== 'walk' && u >= st.t0 && u < st.t1) return { st, age: u - st.t0 };
  return null;
}

// The immune pair in "mixed": the mech walks at where Gojo landed; the android cuts it off where
// it can get to first, and they fight there. Offsets from the cast cell; u is clamped to budget.
export function immuneAt(u, budget, mechTaken = true) {
  u = Math.max(0, Math.min(u, budget));
  const M = { x: Cast.mech[0], z: Cast.mech[1] }, N = { x: Cast.android[0], z: Cast.android[1] };
  if (!mechTaken) return { mech: M, android: N, fighting: false, fightAge: 0, mechTo: 0 };
  const dM = Math.hypot(M.x, M.z), ux = -M.x / dM, uz = -M.z / dM, stop = dM - .9;
  let meet = stop;
  for (let x = 0; x <= stop; x += .05) {
    const q = { x: M.x + ux * x, z: M.z + uz * x };
    if ((Math.hypot(q.x - N.x, q.z - N.z) - .7) / Walk.android <= x / Walk.mech) { meet = x; break; }
  }
  const I = { x: M.x + ux * meet, z: M.z + uz * meet };
  const mw = Math.min(u * Walk.mech, meet), mech = { x: M.x + ux * mw, z: M.z + uz * mw };
  const ax = I.x - N.x, az = I.z - N.z, ad = Math.hypot(ax, az) || 1, aw = Math.min(u * Walk.android, Math.max(0, ad - .7));
  const android = { x: N.x + ax / ad * aw, z: N.z + az / ad * aw };
  const fightFrom = Math.max(meet / Walk.mech, (ad - .7) / Walk.android);
  return { mech, android, fighting: u >= fightFrom, fightAge: u - fightFrom };
}

// ---- the space under the void -----------------------------------------------------------------
// Deep navy, drifting haze, stars, far galaxies and the anime's white ink patches, all under a
// see-through void terrain. fade 0..1 brings everything but the navy in.
export function voidFloor(key, c, s, fade, { reach = 24, patches = 1 } = {}) {
  draw(plane, c.x, V.back, c.z, reach * 5, reach * 5, 0, Navy);
  if (fade <= 0) return;
  for (let i = 0; i < 8; i++) {
    const ang = rand(i + 700) * TAU, d = reach * (.1 + .75 * rand(i + 710)), size = 7 + 10 * rand(i + 720), drift = .7 * Math.sin(s * .07 + i);
    sprite({ x: c.x + Math.cos(ang) * d + drift, z: c.z + Math.sin(ang) * d }, size * 1.4, size, [Haze, Indigo, Dusk][i % 3].withAlpha((.14 + .1 * rand(i + 730)) * fade),
      glow, V.deep + .001 * i, rand(i + 740) * 180);
  }
  for (let i = 0; i < 6; i++) {
    const ang = rand(i + 800) * TAU, d = 7 + (reach - 7) * rand(i + 810);
    galaxy(`${key} galaxy ${i}`, { x: c.x + Math.cos(ang) * d, z: c.z + Math.sin(ang) * d }, .8 + 1.2 * rand(i + 820), .4 + .35 * rand(i + 830),
      rand(i + 840) * 180, s * (5 + 5 * rand(i + 850)) * (i % 2 ? 1 : -1), [Violet, Ice, Gold][i % 3], fade);
  }
  for (let i = 0; i < 180; i++) {
    const x = (rand(i + 900) - .5) * reach * 2, z = (rand(i + 1900) - .5) * reach * 2, size = .035 + .1 * rand(i + 2900) ** 3;
    const tw = .45 + .55 * Math.abs(Math.sin(s * (1 + 2.2 * rand(i + 3900)) + i));
    const at = { x: c.x + x, z: c.z + z };
    sprite(at, size * 2.6, size * 2.6, (i % 9 ? White : Ice).withAlpha(.9 * tw * fade), glow, V.deep + .03);
    if (i % 15 === 0) [0, 90].forEach(deg => sprite(at, .03, size * 9, White.withAlpha(.5 * tw * fade), glow, V.deep + .031, deg + 45 * (i % 2)));
  }
  draw(plane, c.x, V.fog, c.z, reach * 5, reach * 5, 0, Navy.withAlpha(.22 * fade));
  // The white patches: light breaking through, not things lying in the space, so they sit over the
  // void terrain, pure white with a pale halo.
  for (let i = 0; i < 7; i++) {
    const ang = i / 7 * TAU + rand(i + 1000) * .6, d = 11 + 8 * rand(i + 1010), size = 2.5 + 3.5 * rand(i + 1020);
    const breathe = .8 + .2 * Math.sin(s * .8 + i * 2.1), at = { x: c.x + Math.cos(ang) * d, z: c.z + Math.sin(ang) * d };
    sprite(at, size * 1.9, size * 1.6, Ice.withAlpha(.18 * breathe * fade * patches), glow, V.fog + .01);
    sprite(at, size, size * (.75 + .35 * rand(i + 1030)), White.withAlpha(breathe * fade * patches), splat, V.fog + .011 + i * .0005, rand(i + 1040) * 360);
  }
}

// A far galaxy: a soft oval, a bright core and two arms, turning slowly. tilt and turn in degrees.
function galaxy(key, g, size, flat, tilt, turn, colour, fade) {
  sprite(g, size * 1.7, size * 1.7 * flat, colour.withAlpha(.2 * fade), glow, V.deep + .02, tilt);
  sprite(g, size * .45, size * .45 * flat, White.withAlpha(.55 * fade), glow, V.deep + .021, tilt);
  const ct = Math.cos(tilt * Deg), st = Math.sin(tilt * Deg);
  for (let arm = 0; arm < 2; arm++) {
    const pts = [];
    for (let k = 0; k <= 8; k++) {
      const v = k / 8, r = size * (.12 + .8 * v), a = arm * Math.PI + v * 2.6 + turn * Deg;
      const lx = Math.cos(a) * r, lz = Math.sin(a) * r * flat;
      pts.push({ x: g.x + lx * ct + lz * st, z: g.z - lx * st + lz * ct });     // turned clockwise by tilt, as the sprites are
    }
    line(`${key} ${arm}`, pts, size * .18, colour.withAlpha(.32 * fade), whiteGlow, V.deep + .022, 'both');
  }
}

// ---- the black hole (anime ep. 7, the frame with Jogo in front of it) -------------------------------
// Built from that frame (colours sampled from it on 2026-09-23), disc radius R:
//   - the disc: pure black, crisp edge
//   - the gas: grey-blue (about 120, 128, 150 where lit), a light rim hugging the disc at 1.2 R,
//     then a wide band of feathery streaks leaning into a spiral out to 2.15 R, turning slowly;
//     lit on the upper right and the left, near black along the bottom
//   - the ring: thin, at 2.2 R (a little wider toward the upper left), gold outside, white, blue
//     inside; peach-gold on top, white upper right, blue-white on the left, weak lower right
//   - the smoke: a soft pale blue-white cloud with a cyan tinge off the ring's east side, 2.4 to
//     4.4 R east and a little north, with cyan sparkles in it
// The gas and the ring are lab textures (lab/gojo-hole-gas, lab/gojo-hole-wisps, lab/gojo-hole-ring)
// still to be made PNGs; the ring's colours are baked into its texture because it needs several at
// once. Gas turning = the sprites turning, so in game it is two quads and a rotation.
const ss = (a, b, x) => { const t = clamp((x - a) / (b - a)); return t * t * (3 - 2 * t); };
const polarPixels = (n, f) => pixels(n, (u, v) => {
  const dx = u - .5, dz = .5 - v, r = Math.hypot(dx, dz) * 2;
  if (r >= 1) return [0, 0, 0, 0];
  return f(r, (Math.atan2(dz, dx) / TAU + 1) % 1);
});
// The texture's edge (r = 1) is GasOut x R; the disc edge sits at 1 / GasOut.
const GasOut = 2.15, RingAt = 2.2, RingQuad = 2.6, RingShift = { x: -.05, z: .06 };
registerLabTexture('lab/gojo-hole-gas', () => polarPixels(512, (r, phi) => {
  const n = fbm((phi + r * .55) * 12, r * 7, 404, 4, 12);
  const rim = .85 * Math.exp(-(((r - .56) / .04) ** 2)) * ss(.47, .52, r);        // the light rim at 1.2 R
  const band = ss(.5, .64, r) * (1 - ss(.82, .99, r));
  return [1, 1, 1, clamp(rim + band * (.2 + .8 * n * n))];
}));
registerLabTexture('lab/gojo-hole-wisps', () => polarPixels(512, (r, phi) => {
  const n = fbm((phi + r * .8) * 16, r * 11, 505, 4, 16);
  const band = ss(.53, .66, r) * (1 - ss(.88, .98, r));
  return [1, 1, 1, band * ss(.58, .86, n)];
}));
registerLabTexture('lab/gojo-hole-ring', () => polarPixels(512, (r, phi) => {
  const R0 = RingAt / RingQuad, deg = phi * 360;
  const round = .3 + .7 * (.5 + .5 * Math.cos((deg - 125) * Deg));                // brightest top-left
  const w = .012 + .01 * round, d = (r - R0) / w;                                   // and widest there
  if (Math.abs(d) > 5) return [0, 0, 0, 0];
  const east = 1 - .8 * Math.exp(-((((deg + 180) % 360 - 185) / 38) ** 2));          // faint round 5 degrees (east)
  const t = clamp((d + 1.8) / 3.6), gold = clamp(.2 + .8 * Math.cos((deg - 110) * Deg));
  const warm = [1, .78 + .1 * (1 - gold), .35 + .45 * (1 - gold)];
  const col = t < .5 ? [.45 + .55 * t * 2, .7 + .3 * t * 2, 1] : [1 + (warm[0] - 1) * (t - .5) * 2, 1 + (warm[1] - 1) * (t - .5) * 2, 1 + (warm[2] - 1) * (t - .5) * 2];
  const a = clamp((Math.exp(-d * d * 1.1) + .3 * Math.exp(-d * d * .12)) * round * east);
  return [col[0], col[1], col[2], a];
}));
const holeGas = MaterialPool.MatFrom('lab/gojo-hole-gas', ShaderDatabase.MoteGlow);
const holeWisps = MaterialPool.MatFrom('lab/gojo-hole-wisps', ShaderDatabase.MoteGlow);
const holeRing = MaterialPool.MatFrom('lab/gojo-hole-ring', ShaderDatabase.MoteGlow);
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.MoteGlow);
export const GasBody = new Color(.3, .32, .44), GasLight = new Color(.85, .9, 1), Smoke = new Color(.7, .92, .95);

// The black hole under the void, disc radius R at h. live 0..1 fades it; rays 0..1 its light dashes.
export function voidHole(key, h, R, s, live, { swirl = .1, rays = 1, lines = 'speed', trip = Speed.trip } = {}) {
  if (R <= .02 || live <= 0) return;
  const L = V.deep + .08, gas = R * GasOut * 2;
  sprite(h, R * 5.5, R * 5.5, Indigo.withAlpha(.2 * live), glow, L);                                  // the haze round it
  sprite(h, gas, gas, GasBody.withAlpha(.95 * live), holeGas, L + .005, -s * 5);                       // the gas body
  sprite(h, gas, gas, GasLight.withAlpha(.8 * live), holeWisps, L + .01, -s * 9);                      // pale streaks, faster
  // Light and shade that stay put while the gas turns: lit upper right and left, dark bottom.
  sprite({ x: h.x + R * .1, z: h.z - R * 1.55 }, R * 3.6, R * 1.9, Navy.withAlpha(.72 * live), soft, L + .011);
  sprite({ x: h.x + R * 1.3, z: h.z - R * .9 }, R * 1.8, R * 1.8, Navy.withAlpha(.5 * live), soft, L + .0112);
  sprite({ x: h.x - R * .1, z: h.z + R * 1.8 }, R * 1.6, R * .7, Navy.withAlpha(.4 * live), soft, L + .0114);
  sprite({ x: h.x + R * 1.05, z: h.z + R * 1.05 }, R * 1.6, R * 1.0, GasLight.withAlpha(.2 * live), glow, L + .012, -45);
  sprite({ x: h.x - R * 1.65, z: h.z + R * .1 }, R * .9, R * 2.6, GasLight.withAlpha(.2 * live), glow, L + .0122);
  for (let i = 0; i < 18; i++) {                                                                       // a few loose streaks on top, turning faster near the disc
    const r0 = R * (1.1 + .75 * rand(i + 1100)), spin = .5 * Math.pow(R / r0, 1.5), a0 = rand(i + 1105) * TAU + s * spin, span = .35 + .6 * rand(i + 1110), pts = [];
    for (let k = 0; k <= 8; k++) { const v = k / 8, r = r0 * (1 - .06 * v), a = a0 + v * span; pts.push({ x: h.x + Math.cos(a) * r, z: h.z + Math.sin(a) * r }); }
    line(`${key} streak ${i}`, pts, R * (.02 + .025 * rand(i + 1120)), GasLight.withAlpha((.1 + .18 * rand(i + 1130)) * live), whiteGlow, L + .015, 'both');
  }
  ringAt(h, R * 1.2, GasLight.withAlpha(.2 * live), L + .016, true, whiteGlow);                        // the light rim hugging the disc
  ringAt(h, R * 1.13, GasLight.withAlpha(.16 * live), L + .017, true, whiteGlow);
  draw(disc, h.x, L + .02, h.z, R, R, 0, Void.withAlpha(live));
  const q = R * RingQuad * 2;
  sprite({ x: h.x + R * RingShift.x, z: h.z + R * RingShift.z }, q, q, White.withAlpha(live), holeRing, L + .03);
  plume(`${key} plume`, h, R, s, live, L + .04);
  if (rays > 0 && lines === 'speed') speedLines(`${key} speed`, h, s, R * RingAt * 1.06, R * RingAt + 17, live * rays, { swirl, trip });
  else if (rays > 0) voidRays(`${key} rays`, h, s, R * RingAt * 1.04, R * RingAt + 14, swirl, .7 * live * rays, 40);
}

// The smoke streaming off the ring's east side: soft puffs drifting out and growing, and sparkles.
function plume(key, h, R, s, live, L) {
  const path = v => ({ x: h.x + R * (2.25 + 2.2 * v), z: h.z + R * (.05 + .55 * v) });
  for (let i = 0; i < 18; i++) {
    const v = (s * .05 + i / 18) % 1, p = path(v), wob = R * .18 * Math.sin(i * 1.7 + s * .4);
    const size = R * (.45 + 1.25 * v) * (.8 + .4 * rand(i + 1600)), a = live * .3 * ss(0, .12, v) * (1 - ss(.65, 1, v));
    sprite({ x: p.x, z: p.z + wob }, size * 1.35, size, [White, GasLight, Smoke][i % 3].withAlpha(a), puff, L + i * .0004, rand(i + 1610) * 360 + s * 7 * (i % 2 ? 1 : -1));
  }
  for (let i = 0; i < 12; i++) {
    const v = .25 + .7 * rand(i + 1650), p = path(v), tw = .4 + .6 * Math.abs(Math.sin(s * (1.5 + rand(i + 1660) * 2) + i));
    const at = { x: p.x + R * (rand(i + 1670) - .5) * 1.4 * v, z: p.z + R * (rand(i + 1680) - .5) * 1.1 * v };
    sprite(at, .16, .16, Smoke.withAlpha(.9 * tw * live), glow, L + .01);
  }
}

// ---- speed lines (2026-09-23) ------------------------------------------------------------------
// Light rushing out of one vanishing point, as the manga (ch. 225) and the anime draw it round the
// void: every line runs straight out from the black hole's centre, starting past the ring. It is a
// warp tunnel seen from above: a line's head moves out by the same factor every second, so lines
// speed up as they go, and each is stretched to about a third of its distance and thickens (inner
// lines short and thin, outer ones long and wide). They come in 16 bundles with gaps between, like
// manga speed lines, and every Wave seconds a wave of 32 leaves the ring together on top of the
// steady stream. Each line is a white core in a pink, violet or ice glow, tapered at both ends.
// trip: seconds a line takes from the ring to the edge (each line 0.8-1.25 x that); a wave takes 0.55 of it.
export const Speed = { count: 96, bundles: 16, trip: 2, stretch: .34, wave: 1.4, waveLines: 32, waveShare: .55 };
export function speedLines(key, h, s, from, to, alpha, { swirl = 0, count = Speed.count, trip = Speed.trip } = {}) {
  if (alpha <= 0 || to <= from + 1) return;
  const k = Math.log(to / from), glows = [Pink, Violet, Ice];
  const one = (id, ang, phase, bright) => {
    const r1 = from * Math.exp(k * phase), len = Math.min(r1 - from + .2, Speed.stretch * r1 * (.65 + .6 * rand(id + 1940)));
    if (len < .2 || phase <= 0 || phase >= 1) return;
    const r0 = r1 - len, fade = Math.pow(Math.sin(Math.PI * phase), .6) * bright, pts = [];
    for (let j = 0; j <= 4; j++) { const r = r0 + len * j / 4, a = ang + swirl * (r - from); pts.push({ x: h.x + Math.cos(a) * r, z: h.z + Math.sin(a) * r }); }
    const w = .035 + .08 * phase;
    line(`${key} glow ${id}`, pts, w * 3.4, glows[id % 3].withAlpha(.2 * alpha * fade), whiteGlow, V.fog + .02, 'both');
    line(`${key} core ${id}`, pts, w, White.withAlpha(.8 * alpha * fade), whiteGlow, V.fog + .021, 'both');
  };
  for (let i = 0; i < count; i++) {                                                   // the steady stream, in bundles
    const b = i % Speed.bundles, ang = b / Speed.bundles * TAU + rand(b + 1900) * .22 + (rand(i + 1910) - .5) * .1;
    one(i, ang, (s / trip * (.8 + .4 * rand(i + 1920)) + rand(i + 1930)) % 1, .55 + .45 * rand(i + 1950));
  }
  // The waves, all lines of one wave together. A slow trip can have several waves out at once.
  const travel = trip * Speed.waveShare, last = Math.floor(s / Speed.wave);
  for (let wave = last; wave >= 0 && wave >= last - Math.ceil(travel / Speed.wave); wave--) {
    const phase = (s - wave * Speed.wave) / travel;
    if (phase >= 1) continue;
    for (let i = 0; i < Speed.waveLines; i++) {
      const ang = (i + rand(wave * 50 + i + 2000)) / Speed.waveLines * TAU;
      one(500 + (wave % 4) * 100 + i, ang, Math.pow(phase, .85) * (.92 + .08 * rand(wave * 50 + i + 2010)), 1);
    }
  }
}

// The warp-in as the white clears (the anime's lines rushing out as the domain opens): count dashes
// rushing out from o at 22-38 cells a second, for life seconds from age 0.
export function warpBurst(key, o, age, life = .5, count = 90, reach = 16) {
  if (age < 0 || age > life) return;
  const f = age / life;
  for (let i = 0; i < count; i++) {
    const ang = rand(i + 1700) * TAU, speed = 22 + 16 * rand(i + 1710), len = 1.5 + 2.5 * rand(i + 1720);
    const head = .8 + age * speed + rand(i + 1730) * 3, tail = head - len;
    if (tail > reach) continue;
    const r0 = Math.max(.6, tail), r1 = Math.min(head, reach);
    if (r1 - r0 < .2) continue;
    const pts = [r0, (r0 + r1) / 2, r1].map(r => ({ x: o.x + Math.cos(ang) * r, z: o.z + Math.sin(ang) * r }));
    line(`${key} ${i}`, pts, .06 + .05 * rand(i + 1740), [White, White, Pink, Violet, Ice][i % 5].withAlpha((1 - f) * (.6 + .4 * rand(i + 1750))), whiteGlow, Y + .26, 'both');
  }
}

// The earlier look ("old dashes"): dashes of white, pink and violet light running out from the ring, on
// spirals of swirl radians per cell. From lib/gojo.js rays, starting at the ring instead of the centre.
export function voidRays(key, o, s, from, to, swirl, alpha, count = 70) {
  if (alpha <= 0) return;
  const span = to - from;
  for (let i = 0; i < count; i++) {
    const a0 = rand(i + 1500) * TAU, speed = 6 + 6 * rand(i + 1510), len = 1.4 + 2.4 * rand(i + 1520), cycle = span + len;
    const head = (s * speed + rand(i + 1530) * cycle) % cycle, tail = head - len;
    const r0 = Math.max(0, tail), r1 = Math.min(span, head);
    if (r1 - r0 < .15) continue;
    const pts = [];
    for (let k = 0; k <= 6; k++) {
      const r = from + r0 + (r1 - r0) * k / 6, a = a0 + swirl * (r - from);
      pts.push({ x: o.x + Math.cos(a) * r, z: o.z + Math.sin(a) * r });
    }
    const pick = rand(i + 1540), colour = pick < .6 ? White : pick < .85 ? Pink : Violet, edge = 1 - clamp((r1 - span + 3) / 3);
    line(`${key} ${i}`, pts, .05 + .05 * rand(i + 1550), colour.withAlpha(alpha * edge * (.55 + .45 * rand(i + 1560))), whiteGlow, V.deep + .18, 'both');
  }
}

// ---- pawns inside ----------------------------------------------------------------------------
// A frozen pawn's overload: specks of light from every side run into the head, the head glows and
// the eyes stand wide open. s drives it; alpha fades it.
export function infoFlood(key, pos, s, alpha, streams = 8) {
  if (alpha <= 0) return;
  const head = { x: pos.x, z: pos.z + .58 };
  sprite(head, .5, .5, Ice.withAlpha((.42 + .14 * Math.sin(s * 9 + pos.x)) * alpha), glow, Y + .04);
  for (let i = 0; i < streams; i++) {
    const ang = i / streams * TAU + rand(i + 1200 + Math.round(pos.x * 7)) * .5;
    for (let k = 0; k < 3; k++) {
      const v = (s * (1.2 + .5 * rand(i + 1210)) + k / 3 + rand(i + 1220)) % 1;         // 0 far out, 1 at the head
      const r = .12 + 1.05 * (1 - v), a = ang + .4 * (1 - v);
      const p = { x: head.x + Math.cos(a) * r, z: head.z + Math.sin(a) * r };
      sprite(p, .045, .14, [White, Ice, Pink, EyeBlue][(i + k) % 4].withAlpha(alpha * Math.sin(v * Math.PI)), glow, Y + .041, 90 - a / Deg);
    }
  }
  [-.055, .055].forEach(dx => draw(disc, head.x + dx, pawnLayer + .02, head.z + .02, .032, .032, 0, White.withAlpha(alpha)));
}

// Specks that run at an immune pawn's head and glance off it: the overload finds no brain.
export function deflect(key, pos, s, alpha, count = 5) {
  if (alpha <= 0) return;
  const head = { x: pos.x, z: pos.z + .58 };
  for (let i = 0; i < count; i++) {
    const ang = i / count * TAU + rand(i + 1300) * .8, v = (s * 1.1 + rand(i + 1310)) % 1, inward = v < .6, w = inward ? v / .6 : (v - .6) / .4;
    const r = inward ? 1.1 - .62 * w : .48 + .7 * w, a = inward ? ang : ang + .9 * w;
    sprite({ x: head.x + Math.cos(a) * r, z: head.z + Math.sin(a) * r }, .06, .06, (inward ? Ice : Visor).withAlpha(alpha * .8 * (inward ? w : 1 - w)), glow, Y + .041);
    if (!inward && w < .3) sprite({ x: head.x + Math.cos(ang) * .48, z: head.z + Math.sin(ang) * .48 }, .2, .2, Visor.withAlpha(alpha * .7 * (1 - w / .3)), glow, Y + .042);
  }
}

// A pawn spared by Gojo's touch: a blue ring opens off it and a faint ring stays at its feet.
export function touchPulse(key, pos, age) {
  if (age < 0) return;
  if (age < .5) {
    const f = age / .5;
    ringAt({ x: pos.x, z: pos.z + .35 }, .2 + .8 * f, EyeBlue.withAlpha(.9 * (1 - f)), Y + .05, true, whiteGlow);
    sprite({ x: pos.x, z: pos.z + .4 }, .9, .9, EyeBlue.withAlpha(.5 * (1 - f)), glow, Y + .049);
  }
  ringAt(pos, .38, EyeBlue.withAlpha(.35 * clamp(age / .3)), Floor + .02, false, whiteGlow);
}

// An arm from the shoulder toward a point; amount 0..1 how far it reaches (up to 0.6 cells).
export function reachOut(from, to, amount) {
  if (amount <= 0) return;
  const sh = { x: from.x, z: from.z + .36 }, dx = to.x - sh.x, dz = to.z - sh.z, d = Math.hypot(dx, dz) || 1, len = Math.min(.6, d) * amount;
  draw(plane, sh.x + dx / d * len / 2, pawnLayer + .02, sh.z + dz / d * len / 2, .07, len, 90 - Math.atan2(dz, dx) / Deg, Skin);
  draw(disc, sh.x + dx / d * len, pawnLayer + .021, sh.z + dz / d * len, .05, .05, 0, Skin);
}

// A blow landing: a short white arc facing the striker, and a glint. age from the hit.
export function blowFlash(key, at, fromDeg, age) {
  if (age < 0 || age > .25) return;
  const f = age / .25, pts = [];
  for (let k = 0; k <= 6; k++) { const a = (fromDeg + 180 - 55 + 110 * k / 6) * Deg; pts.push({ x: at.x + Math.cos(a) * .32, z: at.z + Math.sin(a) * .32 }); }
  line(`${key} arc`, pts, .1, White.withAlpha(1 - f), whiteGlow, Y + .06, 'both');
  glint(`${key} glint`, at, .22 * (1 - f * .5), 1 - f, Ice, 20);
}

// The overload once it has taken a pawn down: a violet ring over the head and violet stars.
export function overloadMark(key, pos, s, alpha) {
  if (alpha <= 0) return;
  ringAt({ x: pos.x + .42, z: pos.z + .14 }, .15, Violet.withAlpha(.75 * alpha), Y + .03, false, whiteGlow);
  stunStars(key, { x: pos.x + .42, z: pos.z - .5 }, s, .8 * alpha, Violet);
}

// Stand-ins for the immune: a mechanoid (dark plating, red eye, two blades) and a colony android
// (a colonist body with a steel head and a visor).
export function mech(pos, sun, strength, alpha = 1) {
  if (alpha <= 0) return;
  sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, 1, .45, Ink.withAlpha(strength * alpha), soft, shadowLayer);
  [-1, 1].forEach(k => draw(plane, pos.x + k * .3, pawnLayer - .001, pos.z + .36, .05, .38, k * 28, MechLit.withAlpha(alpha)));
  draw(disc, pos.x, pawnLayer, pos.z + .2, .3, .34, 0, MechDark.withAlpha(alpha));
  draw(disc, pos.x + .05, pawnLayer + .002, pos.z + .26, .17, .2, 0, MechLit.withAlpha(alpha));
  draw(disc, pos.x, pawnLayer + .004, pos.z + .56, .15, .12, 0, MechDark.withAlpha(alpha));
  draw(disc, pos.x, pawnLayer + .006, pos.z + .56, .045, .035, 0, MechEye.withAlpha(alpha));
  sprite({ x: pos.x, z: pos.z + .56 }, .25, .2, MechEye.withAlpha(.6 * alpha), glow, pawnLayer + .007);
}
export function android(pos, sun, strength, alpha = 1) {
  if (alpha <= 0) return;
  pawn(pos, Ally, sun, strength, { alpha });
  draw(disc, pos.x, pawnLayer + .01, pos.z + .58, .16, .17, 0, Steel.withAlpha(alpha));
  draw(plane, pos.x, pawnLayer + .012, pos.z + .6, .2, .035, 0, Visor.withAlpha(alpha));
}

// Two immune pawns trading blows: sparks between them every 0.7 s.
export function brawl(key, a, b, age, alpha) {
  if (age < 0 || alpha <= 0) return;
  const k = Math.floor(age / .7), f = (age % .7) / .7, hit = k % 2 ? a : b, from = k % 2 ? b : a;
  if (f < .35) blowFlash(`${key} ${k % 2}`, { x: hit.x, z: hit.z + .35 }, Math.atan2(from.z - hit.z, from.x - hit.x) / Deg, f * .7);
}

// ---- the home map: the barrier and the ball --------------------------------------------------
// From outside, the barrier closing over the radius: a dark sphere (the dome under the 0.6 lift, as
// lib/gojo.js domeOutline draws it) built from three see-through fills so its edge is soft, with a
// thin light rim and a faint lit cap. c is the sphere's ground point (it rises while it shrinks).
export function darkDome(key, c, radius, alpha) {
  if (radius <= .05 || alpha <= 0) return;
  [[1, .22], [.95, .28], [.88, .32], [.8, .3]].forEach(([k, a], j) => {
    const pts = domeOutline(radius * k), vertices = [c.x, c.z], tri = [];
    pts.forEach((q, i) => { vertices.push(c.x + q.x, c.z + q.z); tri.push(0, 1 + i, 1 + (i + 1) % pts.length); });
    const m = mesh(`${key} fill ${j}`); m.setFlat(vertices, tri);
    draw(m, 0, Y + .02 + j * .001, 0, 1, 1, 0, Void.withAlpha(a * alpha));
  });
  const pts = domeOutline(radius), e = Math.min(.08, radius * .2) / radius;
  const outer = pts.map(q => ({ x: c.x + q.x, z: c.z + q.z })), inner = pts.map(q => ({ x: c.x + q.x * (1 - e), z: c.z + q.z * (1 - e) }));
  outer.push(outer[0]); inner.push(inner[0]);
  strip(`${key} rim`, outer, inner, Ice.withAlpha(.3 * alpha), whiteGlow, Y + .024);
  sprite({ x: c.x - radius * .3, z: c.z + radius * .72 }, radius * .9, radius * .45, Blue.withAlpha(.16 * alpha), glow, Y + .025, -20);
}

// The barrier shrunk to a ball (manga ch. 227-228): a black ball hanging height cells over its
// ground point, a thin light rim, a faint blue-violet halo, a slow glint, and its shadow.
export function ball(key, ground, height, size, s, alpha, sun, strength) {
  if (alpha <= 0 || size <= 0) return;
  const c = { x: ground.x, z: ground.z + height * Lift }, r = size / 2;
  sprite({ x: ground.x + sun.x * height, z: ground.z + sun.z * height }, size * 1.3, size * .7, Ink.withAlpha(Math.min(.8, strength * 1.6) * alpha), soft, shadowLayer);
  sprite(c, size * 5, size * 5, Violet.withAlpha(.14 * alpha), glow, Y + .03);
  sprite(c, size * 2.6, size * 2.6, EyeBlue.withAlpha(.22 * alpha), glow, Y + .031);
  draw(disc, c.x, Y + .032, c.z, r, r, 0, Void.withAlpha(alpha));
  ringAt(c, r * 1.02, Ice.withAlpha(.75 * alpha), Y + .033, false, whiteGlow);
  const g = s * 1.3;
  sprite({ x: c.x + Math.cos(g) * r * .55, z: c.z + Math.sin(g) * r * .55 }, r * .5, r * .5, White.withAlpha(.3 * alpha), glow, Y + .034);
  sprite({ x: c.x - r * .35, z: c.z + r * .4 }, r * .45, r * .32, Ice.withAlpha(.5 * alpha), glow, Y + .035);
}

// The ball breaking at the end: cracks of light across it, then a white flash out to the radius.
export function ballBurst(key, ground, height, size, age, R) {
  if (age < 0 || age > 1.2) return;
  const c = { x: ground.x, z: ground.z + height * Lift };
  if (age < .12) for (let i = 0; i < 4; i++) {
    const a = (i * 47 + 20) * Deg, l = size * .75;
    streak(`${key} crack ${i}`, { x: c.x - Math.cos(a) * l * .25, z: c.z - Math.sin(a) * l * .25 }, { x: c.x + Math.cos(a) * l, z: c.z + Math.sin(a) * l },
      .035, White.withAlpha(1 - age / .12), whiteGlow, Y + .04, 3);
  }
  const f = smooth(clamp((age - .08) / .3)), fade = 1 - clamp((age - .2) / .6);
  if (f > 0) {
    sprite(c, R * 2 * f, R * 2 * f, White.withAlpha(.5 * fade), glow, Y + .05);
    ringAt(ground, R * f, White.withAlpha(.6 * fade), Y + .051, false, whiteGlow);
  }
}

export { TAU, Deg };
