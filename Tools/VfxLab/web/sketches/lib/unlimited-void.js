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
// see-through void terrain. fade 0..1 brings everything but the navy in. fly = { at, g, g0 } is the
// fly-in at the opening (see flight): the stars, galaxies and patches sit g of their distance from the
// point `at` and are g of their size, stars dimmer while far; every third star that has moved since
// g0 (a moment before) is drawn as a trail from where it was. The haze stays as it is.
export function voidFloor(key, c, s, fade, { reach = 24, patches = 1, fly = null } = {}) {
  draw(plane, c.x, V.back, c.z, reach * 5, reach * 5, 0, Navy);
  if (fade <= 0) return;
  const g = fly ? fly.g : 1, far = Math.sqrt(g);
  const place = (x, z, k = g) => (fly ? { x: fly.at.x + (x - fly.at.x) * k, z: fly.at.z + (z - fly.at.z) * k } : { x, z });
  for (let i = 0; i < 8; i++) {
    const ang = rand(i + 700) * TAU, d = reach * (.1 + .75 * rand(i + 710)), size = 7 + 10 * rand(i + 720), drift = .7 * Math.sin(s * .07 + i);
    sprite({ x: c.x + Math.cos(ang) * d + drift, z: c.z + Math.sin(ang) * d }, size * 1.4, size, [Haze, Indigo, Dusk][i % 3].withAlpha((.14 + .1 * rand(i + 730)) * fade),
      glow, V.deep + .001 * i, rand(i + 740) * 180);
  }
  for (let i = 0; i < 6; i++) {
    const ang = rand(i + 800) * TAU, d = 7 + (reach - 7) * rand(i + 810);
    galaxy(`${key} galaxy ${i}`, place(c.x + Math.cos(ang) * d, c.z + Math.sin(ang) * d), (.8 + 1.2 * rand(i + 820)) * g, .4 + .35 * rand(i + 830),
      rand(i + 840) * 180, s * (5 + 5 * rand(i + 850)) * (i % 2 ? 1 : -1), [Violet, Ice, Gold][i % 3], fade * far);
  }
  for (let i = 0; i < 180; i++) {
    const x = c.x + (rand(i + 900) - .5) * reach * 2, z = c.z + (rand(i + 1900) - .5) * reach * 2, size = (.035 + .1 * rand(i + 2900) ** 3) * (.4 + .6 * g);
    const tw = .45 + .55 * Math.abs(Math.sin(s * (1 + 2.2 * rand(i + 3900)) + i)), at = place(x, z), a = tw * fade * far;
    if (fly && i % 3 === 0) {
      const was = place(x, z, fly.g0);
      if (Math.hypot(at.x - was.x, at.z - was.z) > size * 3) streak(`${key} trail ${i}`, was, at, size * 1.6, White.withAlpha(.55 * a), whiteGlow, V.deep + .029, 2);
    }
    sprite(at, size * 2.6, size * 2.6, (i % 9 ? White : Ice).withAlpha(.9 * a), glow, V.deep + .03);
    if (i % 15 === 0) [0, 90].forEach(deg => sprite(at, .03, size * 9, White.withAlpha(.5 * a), glow, V.deep + .031, deg + 45 * (i % 2)));
  }
  draw(plane, c.x, V.fog, c.z, reach * 5, reach * 5, 0, Navy.withAlpha(.22 * fade));
  // The white patches: light breaking through, not things lying in the space, so they sit over the
  // void terrain, pure white with a pale halo.
  for (let i = 0; i < 7; i++) {
    const ang = i / 7 * TAU + rand(i + 1000) * .6, d = 11 + 8 * rand(i + 1010), size = (2.5 + 3.5 * rand(i + 1020)) * g;
    const breathe = .8 + .2 * Math.sin(s * .8 + i * 2.1), at = place(c.x + Math.cos(ang) * d, c.z + Math.sin(ang) * d);
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

// The black hole under the void, disc radius R at h. live 0..1 fades it. No speed lines round it:
// in the anime they are gone once the hole appears (see tunnel).
export function voidHole(key, h, R, s, live) {
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

// ---- the speed-line tunnel at the opening (anime ep. 7 and Cursed Clash, checked 2026-09-23) -------------
// Both sources: after the hand sign the victims fly through a dense tunnel of streaks toward one
// vanishing point, and the black hole is then at that point (the anime ep. 7 upload nmvkhLz8t7I at
// 22-28 s; Cursed Clash, mxw3_ujSYDo at 14-20 s). Most streaks run from the frame edge nearly to the
// point; streaks cover 33-63 % of an anime frame and 43-91 % of a game frame. Cursed Clash adds clouds
// of glittering dust between the streaks and a white light that grows at the point just before the
// hole opens in it (tunnelDust, tunnelLight). Here the point is where the black hole will open.
// A streak's head moves out by the same factor each second (a warp tunnel seen from above: streaks
// speed up as they go); the streak reaches back `length` of its distance (each 0.75-1.25 x that), is
// sharp at the head, widest just behind it and thin at the point end, and gets wider with distance.
// Angles are random, so streaks clump and leave gaps, and they flicker 12 times a second as anime
// speed lines are redrawn every few frames. reach: in game the distance from the point to the
// farthest corner of the view (CameraDriver.CurrentViewRect), so the tunnel fills the screen at any
// zoom; the lab uses 60 cells. hollow: the streaks stop this far from the point, so the far end of the
// tunnel is dark as in both sources; the sketch widens it with the opening black hole so the streaks pour
// out of its rim. The streaks are drawn in batches, one mesh per colour and brightness step (addRay).
export const Tunnel = { count: 360, from: .6, reach: 60, trip: 2, length: .75, flicker: 12, hollow: 1.2 };

// Two colour sets from those frames, frame colours (0-255) in the comments. grade is laid over the space
// (normal blend, under the white patches) to bring its colour to the frame's dark; tint is added over the
// whole view while the lines run; bloom sits faintly round the point; glows are the wide dim body of a
// streak and cores its thin bright middle; cored is the share of streaks that have a core; dust,
// dustHalo and bits are the dust clouds (bits: the anime's white ink bits instead of a soft cloud);
// light is the halo of the white light.
export const LinePalettes = {
  'anime ep 7': {                           // purple-black (25, 8, 25), magenta glow (88, 29, 70), pink-white cores (238, 180, 230)
    grade: new Color(.09, .02, .07, .55), tint: new Color(.08, 0, .04), bloom: new Color(.6, .1, .45),
    glows: [new Color(.82, .16, .52), new Color(.62, .14, .66), new Color(.88, .2, .36), new Color(.72, .12, .46)],
    cores: [new Color(1, .7, .92), new Color(1, .82, .95), new Color(1, .6, .84)], glowAlpha: .45, coreAlpha: .8, cored: .45,
    dust: White, dustHalo: new Color(.7, .15, .6), bits: true, light: new Color(1, .72, .95),
  },
  'Cursed Clash': {                         // indigo (15, 5, 46), violet glow (45, 35, 80), lavender-white cores (230, 212, 235)
    grade: new Color(0, 0, 0, 0), tint: new Color(.05, .01, .12), bloom: new Color(.4, .35, .85),
    glows: [new Color(.45, .38, .8), new Color(.36, .36, .9), new Color(.6, .45, .82), new Color(.42, .33, .75)],
    cores: [new Color(.92, .86, .96), White, new Color(.84, .86, 1)], glowAlpha: .32, coreAlpha: .85, cored: .8,
    dust: White, dustHalo: new Color(.55, .5, .95), bits: false, light: new Color(.86, .82, 1),
  },
};
export const Palettes = Object.keys(LinePalettes);
const paletteOf = name => LinePalettes[name] ?? LinePalettes[Palettes[0]];

export function tunnel(key, h, s, alpha, { palette = Palettes[0], count = Tunnel.count, length = Tunnel.length, trip = Tunnel.trip, reach = Tunnel.reach, hollow = Tunnel.hollow } = {}) {
  if (alpha <= 0) return;
  const pal = paletteOf(palette), from = Tunnel.from, k = Math.log(reach / from), frame = Math.floor(s * Tunnel.flicker);
  if (pal.grade.a > 0) draw(plane, h.x, V.fog + .005, h.z, reach * 2.2, reach * 2.2, 0, pal.grade.withAlpha(pal.grade.a * alpha));   // the space to the frame's dark
  draw(plane, h.x, V.fog + .016, h.z, reach * 2.2, reach * 2.2, 0, pal.tint.withAlpha(alpha), whiteGlow);   // the tint over the view
  sprite(h, 14, 14, pal.bloom.withAlpha(.14 * alpha), glow, V.fog + .017);                                  // a faint bloom round the point
  const glows = new Map(), cores = new Map();
  for (let i = 0; i < count; i++) {
    const phase = (s / trip * (.8 + .4 * rand(i + 1920)) + rand(i + 1930)) % 1;
    const r1 = from * Math.exp(k * phase), r0 = Math.max(hollow * (.85 + .3 * rand(i + 1945)), r1 * (1 - length * (.75 + .5 * rand(i + 1940))));
    const a = alpha * smooth(clamp(phase / .12)) * (1 - smooth(clamp((phase - .9) / .1))) * (.75 + .25 * rand(i * 131 + frame * 7919 + 17));
    if (a <= .01 || r1 - r0 < .15) continue;
    const ang = rand(i + 1910) * TAU, w = (.02 + .0065 * r1) * (.6 + .8 * rand(i + 1960)), bright = rand(i + 1950);
    addRay(glows, i % 4, pal.glowAlpha * (.45 + .55 * bright) * a, h, ang, r0, r1, w * 3.2);
    if (rand(i + 1970) < pal.cored) addRay(cores, i % 3, pal.coreAlpha * (.5 + .5 * bright) * a, h, ang, r0, r1, w);
  }
  drawRays(`${key} glow`, glows, pal.glows, V.fog + .02);
  drawRays(`${key} core`, cores, pal.cores, V.fog + .021);
}

// Streaks are batched: one mesh per colour and brightness step (alpha in twelfths), about 50 meshes
// for 360 streaks, as the C# port should draw them. A streak runs straight out from h at angle ang
// (radians) from r0 to r1: sharp at the head, widest just behind it, thinning to nothing at the
// point end.
const RaySteps = 12;
function addRay(batches, colour, alpha, h, ang, r0, r1, w) {
  const step = Math.round(alpha * RaySteps);
  if (step <= 0) return;
  const bucket = `${colour} ${step}`;
  let b = batches.get(bucket);
  if (!b) batches.set(bucket, b = { colour, step, v: [], tri: [] });
  const c = Math.cos(ang), sn = Math.sin(ang), base = b.v.length / 2;
  for (let j = 0; j <= 5; j++) {
    const u = j / 5, r = r1 - (r1 - r0) * u, half = w / 2 * (j ? Math.pow(1 - u, .8) : .25) + .003;
    b.v.push(h.x + c * r - sn * half, h.z + sn * r + c * half, h.x + c * r + sn * half, h.z + sn * r - c * half);
    if (j) { const n = base + j * 2; b.tri.push(n - 2, n, n - 1, n - 1, n, n + 1); }
  }
}
function drawRays(key, batches, colours, layer) {
  for (const [bucket, b] of batches) {
    const m = mesh(`${key} ${bucket}`);
    m.setFlat(b.v, b.tri);
    draw(m, 0, layer, 0, 1, 1, 0, colours[b.colour].withAlpha(b.step / RaySteps), whiteGlow);
  }
}

// The dust between the streaks: Cursed Clash's clouds of glitter, the anime's white bits. Clouds stream
// out from the point more slowly than the streaks and grow as they come; each is a soft cloud stretched
// along its path with glitter twinkling in it, a few specks crossed like stars (anime: a fainter cloud
// and small white ink bits among the glitter).
export const Dust = { clouds: 16, glitter: 12, crossed: 2, bits: 3, from: 1.5, slower: 1.6 };
export function tunnelDust(key, h, s, alpha, { palette = Palettes[0], trip = Tunnel.trip, reach = Tunnel.reach } = {}) {
  if (alpha <= 0) return;
  const pal = paletteOf(palette), k = Math.log(reach / Dust.from);
  for (let c = 0; c < Dust.clouds; c++) {
    const phase = (s / (trip * Dust.slower) + rand(c + 2110)) % 1, ang = rand(c + 2100) * TAU;
    const r = Dust.from * Math.exp(k * phase), size = .7 + .24 * r, grow = .7 + .09 * r;
    const fade = alpha * smooth(clamp(phase / .15)) * (1 - smooth(clamp((phase - .8) / .2)));
    if (fade <= .01) continue;
    const ca = Math.cos(ang), sa = Math.sin(ang);
    const at = (along, across) => ({ x: h.x + ca * (r + along) - sa * across, z: h.z + sa * (r + along) + ca * across });
    sprite(at(0, 0), size * 2.2, size * 1.1, pal.dustHalo.withAlpha((pal.bits ? .16 : .38) * fade), puff, V.fog + .022, -ang / Deg + rand(c + 2120) * 30 - 15);
    for (let g = 0; g < Dust.glitter; g++) {
      const n = c * 31 + g, q = at((rand(n + 2200) - .5) * size * 1.9, (rand(n + 2300) - .5) * size * .9);
      const tw = .35 + .65 * Math.abs(Math.sin(s * (5 + 6 * rand(n + 2400)) + g * 1.7)), gs = (.06 + .1 * rand(n + 2500)) * grow;
      sprite(q, gs * 2.4, gs * 2.4, pal.dust.withAlpha(tw * fade), glow, V.fog + .023);
      if (g < Dust.crossed) [45, 135].forEach(deg => sprite(q, gs * .35, gs * 7, pal.dust.withAlpha(.8 * tw * fade), glow, V.fog + .0232, deg));
    }
    if (pal.bits) for (let b = 0; b < Dust.bits; b++) {
      const n = c * 17 + b, q = at((rand(n + 2600) - .5) * size * 1.6, (rand(n + 2700) - .5) * size * .9), bs = (.12 + .16 * rand(n + 2800)) * grow;
      sprite(q, bs, bs * (.6 + .5 * rand(n + 2900)), White.withAlpha(.9 * fade), splat, V.fog + .0235, rand(n + 3000) * 360);
    }
  }
}

// The white light at the vanishing point (Cursed Clash 19 s): it grows as the lines finish and the
// black hole opens under it as it fades. a 0..1.
export function tunnelLight(key, h, a, palette = Palettes[0]) {
  if (a <= 0) return;
  const pal = paletteOf(palette);
  sprite(h, 22 * a, 22 * a, pal.bloom.withAlpha(.22 * a), glow, V.fog + .03);
  sprite(h, 9 * a, 9 * a, pal.light.withAlpha(.6 * a), glow, V.fog + .031);
  sprite(h, 1.2 + 3.2 * a, 1.2 + 3.2 * a, White.withAlpha(a), glow, V.fog + .032);
  sprite(h, .6 + 1.4 * a, .6 + 1.4 * a, White.withAlpha(a), glow, V.fog + .033);
}

// The fly-in at the opening: the space starts `depth` times nearer the vanishing point (and that much
// smaller) and spreads out to where it lies, fast at first and settled by `land`, as a camera flying
// toward the point and stopping would see it. The pawns fly with the camera, so they stay put. Returns
// the scale about the point at time s (1 = landed); voidFloor's fly option takes it.
export function flight(s, from, land, depth) {
  const u = clamp((s - from) / (land - from));
  return Math.exp(-Math.log(Math.max(1, depth)) * (1 - u) * (1 - u));
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
