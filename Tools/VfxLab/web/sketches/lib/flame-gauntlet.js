// Shared drawing for the Flame Gauntlet kit: the gauntlet on the wearer's hand, the heat that
// makes its metal glow (no fire on the gauntlet itself), a burning map cell, a burning pawn, the scorch a fire leaves, the
// overheat state on the wearer, and the heat tally. Devour and Release both draw the weapon
// through these so the two stay identical, and a C# port makes one class of it. The pieces only
// Release uses (the jet out of the fist, the ground wave, its soot, the sheet of flame it
// leaves, the vent exhaust and the knuckle wisp) are at the end.
//
// The gauntlet lies level at hand height and turns with the aim, so it is a flat shape and needs
// no per-facing method. It draws under the pawn layer when it points north and over it when it
// points south (the Vergil rule). Flames rise, and height is drawn north (Lift), so every tongue
// of fire is a strip that leans north from its base; that is the same from every facing.
//
// The rule the drawing shows (proposed 2026-09-23, not agreed; the numbers are placeholders for
// XML fields). The gauntlet is a held melee weapon (about 12 blunt, a punch). One meter, Heat,
// 0..20: Devour adds 1 per burning cell and 2 per burning pawn; chemfuel reload adds 2 per unit;
// each burn hit the wearer's immunity blocks adds 1. Release spends 1 per cone cell, minimum 5.
// Heat decays 1 per 30 s. At 15 or more the wearer is Overheating: -20 % move, -20 %
// manipulation, 1 burn to the gauntlet arm every 5 s, and Devour is refused at 20 ("too hot").
import { AltitudeLayer, Color, Mathf, Meshes } from '../../js/engine.js';
import { draw } from './six-paths-solid.js';
import { Body, Y, Floor, Lift, sprite, band, circle, soft, glow, rand } from './six-paths-impact.js';
import { figure, rect, tube, screen, shadow, frame, easeOut, bump, Skin, Pale, Dust, Enemy, Ally, shadowLayer, pawnLayer, puff } from './chain-sickle.js';
export { figure, rect, tube, screen, shadow, frame, easeOut, bump, Skin, Pale, Dust, Enemy, Ally, shadowLayer, pawnLayer, puff, rand };

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
export const disc = Meshes.disc(32, 'flame gauntlet disc');
// Palette. The gauntlet is blackened iron with brass seams; fire is ember red under orange under a
// yellow-white core. None of it is Six Paths violet.
export const Iron = new Color(.16, .15, .16), IronLit = new Color(.34, .32, .34), IronDark = new Color(.05, .05, .06);
export const Brass = new Color(.62, .48, .22), BrassLit = new Color(.86, .72, .40);
export const Ember = new Color(.86, .22, .05), Flame = new Color(1, .55, .10), Core = new Color(1, .92, .60);
export const Hot = new Color(1, .30, .12), Smoke = new Color(.30, .28, .27), Char = new Color(.09, .07, .06);
export const Soot = new Color(.24, .22, .21), Steam = new Color(.80, .78, .76);
export const Wearer = new Color(.30, .45, .55);
// Decided looks and the rule's fixed numbers.
export const HandH = .5;                     // hand height, cells
export const ChestH = .45;
export const MaxHeat = 20, OverheatAt = 15;
export const HeatPerCell = 1, HeatPerPawn = 2, MinRelease = 5;
export const DevourRange = 10, DevourRadius = 3;
export const ConeLength = 6, ConeWidth = 3;
export const ForearmLen = .42, ForearmW = .15;  // from the elbow to the wrist
export const Lead = .2, Tail = .4;

// ---- fire ----------------------------------------------------------------------------------
// One tongue of flame: a strip that rises `h` cells from `base` (a screen point), sways with the
// clip time, widest a third of the way up and pointed at the tip. Three nested strips, ember over
// orange over the pale core, and an additive glow at the base. `lean` {x,z} tilts the whole
// tongue (toward the palm while it is being pulled).
export function tongue(key, base, h, w, s, seed, alpha = 1, layer = Y + .03, lean = { x: 0, z: 0 }) {
  if (h <= .01 || alpha <= 0) return;
  const n = 7, flick = 1 + .12 * Math.sin(s * 13 + seed * 5.1) + .06 * Math.sin(s * 23 + seed * 2.7);
  const spine = k => {
    const pts = [];
    for (let i = 0; i <= n; i++) {
      const u = i / n, hh = h * k * flick;
      const sway = (Math.sin(s * 7 + seed * 6.3 + u * 2.2) * .10 + Math.sin(s * 11.5 + seed) * .04) * u * u * w * 4;
      pts.push({ x: base.x + sway + lean.x * u * hh, z: base.z + u * hh * Lift + lean.z * u * hh });
    }
    return pts;
  };
  const wf = k => u => w * k * Math.pow(Math.sin(Math.PI * Math.min(1, .12 + .88 * u)), .9) * (1 - u * .45);
  sprite(base, w * 3.2, w * 2.2, Flame.withAlpha(.35 * alpha), glow, layer - .001);
  tube(`${key} ember`, spine(1), wf(1), Ember.withAlpha(.9 * alpha), layer);
  tube(`${key} flame`, spine(.8), wf(.62), Flame.withAlpha(.95 * alpha), layer + .001);
  tube(`${key} core`, spine(.55), wf(.32), Core.withAlpha(.95 * alpha), layer + .002);
}

// A burning map cell: a scorch under it, an additive glow, and five tongues that rise from
// points inside the cell. `amount` 0..1 scales the fire (it shrinks as Devour takes it); the
// scorch stays at full strength once anything has burned there. `flare` makes the tongues that
// much taller and the glow brighter (Release lights a cell with a flare that settles).
export function fireCell(key, c, s, amount = 1, seed = 0, layer = Y + .03, flare = 0) {
  scorch(c, 1);
  if (amount <= 0) return;
  sprite({ x: c.x, z: c.z + .1 }, (1.4 * amount + .3) * (1 + .3 * flare), (1.1 * amount + .3) * (1 + .3 * flare), Flame.withAlpha(.30 * amount * (1 + flare)), glow, Floor + .012);
  for (let i = 0; i < 5; i++) {
    const r1 = rand(seed * 17 + i), r2 = rand(seed * 17 + i + 40);
    const base = { x: c.x + (r1 - .5) * .62, z: c.z + (r2 - .5) * .55 };
    const h = (.32 + rand(seed * 17 + i + 80) * .38) * amount * (1 + flare), w = (.11 + rand(seed * 17 + i + 120) * .06) * (.5 + .5 * amount) * (1 + .25 * flare);
    tongue(`${key} t${i}`, base, h, w, s, seed * 7 + i, 1, layer + i * .0005 - base.z * 1e-4);
  }
  // Embers drifting up out of the fire.
  for (let i = 0; i < 4; i++) {
    const life = .9 + rand(seed + i + 300) * .5, u = ((s * .8 + rand(seed + i + 310) * 3) % life) / life;
    const x = c.x + (rand(seed + i + 320) - .5) * .5 + Math.sin(u * 9 + i) * .06, h = u * (.9 + rand(seed + i + 330) * .4);
    sprite({ x, z: c.z + h * Lift }, .06, .06, Core.withAlpha((1 - u) * .9 * amount), glow, Y + .12);
  }
}

// The black mark a fire leaves on a cell. Stays.
export function scorch(c, alpha = 1) {
  sprite(c, 1.05, .95, Char.withAlpha(.55 * alpha), soft, Floor + .006);
  sprite({ x: c.x + .12, z: c.z - .08 }, .55, .45, Char.withAlpha(.5 * alpha), puff, Floor + .007);
  sprite({ x: c.x - .15, z: c.z + .1 }, .5, .4, Char.withAlpha(.45 * alpha), puff, Floor + .007);
}

// Fire on a standing pawn: tongues from the hips, chest and a shoulder, and a glow. `amount` 0..1.
export function burningPawn(key, pos, s, amount = 1, seed = 5) {
  if (amount <= 0) return;
  sprite({ x: pos.x, z: pos.z + .3 }, .9 * amount + .2, 1.0 * amount + .2, Flame.withAlpha(.30 * amount), glow, pawnLayer + .02);
  const spots = [{ x: -.12, z: .12, h: .45 }, { x: .10, z: .22, h: .55 }, { x: .02, z: .42, h: .40 }, { x: -.06, z: .55, h: .35 }];
  spots.forEach((q, i) => tongue(`${key} t${i}`, { x: pos.x + q.x, z: pos.z + q.z }, q.h * amount, .11 * (.5 + .5 * amount), s, seed * 3 + i, 1, pawnLayer + .03 + i * .001));
}

// A parcel of fire in flight: the flame of one cell travelling from `from` to `to` along an arch
// `u` 0..1 of the way. Drawn as a short comet, head first: a core, then ember tail sprites behind.
export function parcel(key, from, to, u, s, seed = 0, size = 1) {
  const arch = q => ({ x: lerp(from.x, to.x, q), z: lerp(from.z, to.z, q) + Math.sin(q * Math.PI) * .55 * Lift * size });
  const head = arch(u);
  for (let i = 5; i >= 0; i--) {
    const q = clamp(u - i * .045), pt = arch(q), k = 1 - i / 6;
    sprite(pt, .34 * k * size, .30 * k * size, Ember.withAlpha(.55 * k), soft, Y + .14 + i * .0005);
    sprite(pt, .26 * k * size, .22 * k * size, Flame.withAlpha(.9 * k), glow, Y + .141 + i * .0005);
  }
  sprite(head, .18 * size, .16 * size, Core.withAlpha(1), glow, Y + .145);
  sprite(head, .5 * size, .4 * size, Flame.withAlpha(.35), glow, Y + .146);
  return head;
}

// ---- the gauntlet ---------------------------------------------------------------------------
// `hand` is a ground point, HandH up; `deg` the direction the fist points. `heat` 0..20 makes the
// metal glow; `open` 0..1 spreads the fingers into the maw (Devour); `lean` 0..1 slides the
// glow to the fist (Release wind-up). The gauntlet itself never burns. Returns { hand, palm, elbow,
// layer } as screen points.
export function gauntlet(key, hand, deg, heat, s, sun, strength, { open = 0, lean = 0, alpha = 1, layer = null } = {}) {
  const r = deg * Mathf.Deg2Rad, d = { x: Math.cos(r), z: Math.sin(r) };
  if (layer === null) layer = d.z > 0 ? pawnLayer - .03 : Y + .05;
  const p = screen({ ...hand, h: HandH }), sd = shadow({ ...hand, h: HandH }, sun);
  const at = (base, along, side = 0) => ({ x: base.x + d.x * along - d.z * side, z: base.z + d.z * along + d.x * side });
  const elbow = at(p, -ForearmLen), palm = at(p, .14);

  // Shadow: forearm and fist in one dark tone on the floor.
  rect(`${key} shadow arm`, at(sd, -ForearmLen / 2), ForearmLen, ForearmW, deg, Body.withAlpha(strength * .6 * alpha), shadowLayer);
  draw(disc, sd.x + d.x * .06, shadowLayer + .001, sd.z + d.z * .06, .13, .12, 0, Body.withAlpha(strength * .6 * alpha));

  // Forearm: a dark iron plate with a lit top strip, three brass seams, and two vents at the elbow.
  rect(`${key} arm edge`, at(p, -ForearmLen / 2), ForearmLen + .03, ForearmW + .03, deg, IronDark.withAlpha(alpha), layer);
  rect(`${key} arm`, at(p, -ForearmLen / 2), ForearmLen, ForearmW, deg, Iron.withAlpha(alpha), layer + .001);
  rect(`${key} arm lit`, at(p, -ForearmLen / 2, ForearmW * .22), ForearmLen * .92, ForearmW * .32, deg, IronLit.withAlpha(alpha * .85), layer + .002);
  for (let i = 0; i < 3; i++) rect(`${key} seam ${i}`, at(p, -ForearmLen * (.22 + i * .26)), .02, ForearmW + .02, deg, Brass.withAlpha(alpha), layer + .003);
  for (const k of [-1, 1]) rect(`${key} vent ${k}`, at(p, -ForearmLen * .85, k * ForearmW * .28), .07, .03, deg, IronDark.withAlpha(alpha), layer + .003);

  // The hand: a fist (closed) or a spread claw with a glowing palm (open).
  const fist = at(p, .05);
  draw(disc, fist.x, layer + .004, fist.z, .12, .11, 0, IronDark.withAlpha(alpha));
  draw(disc, fist.x, layer + .005, fist.z, .10, .09, 0, Iron.withAlpha(alpha));
  draw(disc, fist.x - .01, layer + .006, fist.z + .015, .06, .05, 0, IronLit.withAlpha(alpha * .8));
  const spread = smooth(open);
  for (let i = 0; i < 4; i++) {
    // Four finger plates: closed they lie together over the knuckles; open they fan out 26 deg apart.
    const side = (i - 1.5) * .045, a = deg + (i - 1.5) * 26 * spread, len = .13 + .05 * spread;
    const from = at(fist, .04, side * (1 - spread * .4));
    const ar = a * Mathf.Deg2Rad, c = { x: from.x + Math.cos(ar) * len / 2, z: from.z + Math.sin(ar) * len / 2 };
    rect(`${key} finger ${i} edge`, c, len + .02, .05, a, IronDark.withAlpha(alpha), layer + .007);
    rect(`${key} finger ${i}`, c, len, .035, a, Iron.withAlpha(alpha), layer + .008);
    rect(`${key} knuckle ${i}`, { x: from.x + Math.cos(ar) * .02, z: from.z + Math.sin(ar) * .02 }, .03, .04, a, Brass.withAlpha(alpha), layer + .009);
  }
  if (spread > 0) {
    // The palm: an ember-red mouth that glows, hotter the more heat the gauntlet holds.
    const k = .3 + .7 * clamp(heat / MaxHeat);
    draw(disc, palm.x, layer + .0095, palm.z, .075 * spread, .065 * spread, 0, Ember.withAlpha(alpha * spread));
    draw(disc, palm.x, layer + .0096, palm.z, .045 * spread, .04 * spread, 0, Core.withAlpha(alpha * spread * (.6 + .4 * k)));
    sprite(palm, .5 * spread, .42 * spread, Flame.withAlpha(.45 * spread * alpha), glow, layer + .0097);
  }

  // Heat: no fire on the gauntlet. The metal glows like a heated plate: the seams first (dull
  // red), then the whole plate through orange to yellow-white at 20, with an additive halo that
  // grows with it. `lean` moves the halo toward the fist (the Release wind-up). Above OverheatAt
  // the halo turns red and pulses, and smoke rises from the elbow vents.
  const k = clamp(heat / MaxHeat), over = clamp((heat - OverheatAt) / (MaxHeat - OverheatAt));
  if (k > 0) {
    const glowCol = k < .5 ? Color.Lerp(Ember, Flame, k * 2) : Color.Lerp(Flame, Core, (k - .5) * 2);
    // The plate itself takes the tint, strongest at the wrist end.
    rect(`${key} arm hot`, at(p, -ForearmLen * .42), ForearmLen * .85, ForearmW * .8, deg, glowCol.withAlpha(alpha * (.15 + .55 * k)), layer + .0035);
    for (let i = 0; i < 3; i++) rect(`${key} seam hot ${i}`, at(p, -ForearmLen * (.22 + i * .26)), .024, ForearmW + .02, deg, glowCol.withAlpha(alpha * clamp(k * 3 - i * .6)), layer + .0036);
    for (const j of [-1, 1]) rect(`${key} vent hot ${j}`, at(p, -ForearmLen * .85, j * ForearmW * .28), .07, .03, deg, glowCol.withAlpha(alpha * clamp(k * 2)), layer + .0037);
    // Halo: along the forearm, sliding to the fist with `lean`, pulsing when overheating.
    const pulse = 1 + .25 * over * Math.sin(s * 9);
    const c = at(p, lerp(-ForearmLen * .45, .05, lean));
    sprite(c, (ForearmLen * 1.5 * (1 - lean * .5)) * k * pulse + .15, .45 * k * pulse + .12, glowCol.withAlpha(alpha * .45 * k), glow, layer + .012, -deg);
    if (over > 0) sprite(c, ForearmLen * 2.2 * pulse, .8 * pulse, Hot.withAlpha(.4 * over * alpha), glow, layer + .0125, -deg);
  }
  if (over > 0) {
    for (let i = 0; i < 3; i++) {
      const life = 1.4, u = ((s * .7 + rand(i + 500) * 2) % life) / life;
      const base = at(p, -ForearmLen * .85 + (rand(i + 510) - .5) * .1, (i - 1) * .06);
      sprite({ x: base.x + Math.sin(u * 5 + i) * .08, z: base.z + u * .7 * Lift }, .18 + u * .3, .16 + u * .25, Smoke.withAlpha(.45 * (1 - u) * over * alpha), puff, Y + .16);
    }
  }
  const vents = [-1, 1].map(j => at(p, -ForearmLen * .85, j * ForearmW * .28));
  return { hand: p, palm, elbow, fist, vents, layer, d };
}

// The wearer's Overheating state: a red haze at the arm and a burn mark on the gauntlet arm for
// each tick that has landed (a small dark blister on the upper arm that stays). `ticks` is how
// many 5 s ticks have hit; `flash` 0..1 is a fresh one.
export function overheating(key, pos, s, over, ticks = 0, flash = 0) {
  if (over <= 0 && ticks <= 0) return;
  if (over > 0) circle({ x: pos.x, z: pos.z + .1 }, .36 + .03 * Math.sin(s * 6), .35 * over, pawnLayer + .03, Hot);
  for (let i = 0; i < ticks; i++) {
    const c = { x: pos.x + .16 + (rand(i + 700) - .5) * .06, z: pos.z + .36 + (rand(i + 710) - .5) * .06 };
    draw(disc, c.x, pawnLayer + .012, c.z, .045, .04, 0, Char.withAlpha(.9));
    draw(disc, c.x, pawnLayer + .013, c.z, .025, .022, 0, Ember.withAlpha(.9));
  }
  if (flash > 0) sprite({ x: pos.x + .16, z: pos.z + .36 }, .35, .3, Hot.withAlpha(.8 * flash), glow, pawnLayer + .04);
}

// The heat tally: twenty ticks south of the wearer's feet, or above the head when aiming south
// so the row does not sit on the target. Filled ticks are flame orange; the last five, the
// overheat zone, are outlined in red. A lab aid: in game this is a gizmo.
export function tally(key, pos, heat, aimDeg = 0, alpha = 1) {
  const above = Math.sin(aimDeg * Mathf.Deg2Rad) < -.5, z = pos.z + (above ? 1.15 : -.55);
  const step = .085, w = .06, x0 = pos.x - step * (MaxHeat - 1) / 2;
  for (let i = 0; i < MaxHeat; i++) {
    const fill = clamp(heat - i), zone = i >= OverheatAt;
    rect(`${key} tick ${i} edge`, { x: x0 + i * step, z }, w + .015, .13, 90, (zone ? Ember : IronDark).withAlpha(.8 * alpha), Floor + .05);
    rect(`${key} tick ${i}`, { x: x0 + i * step, z }, w, .11, 90, Color.Lerp(Iron, zone ? Hot : Flame, fill).withAlpha(alpha * (.6 + .4 * fill)), Floor + .051);
  }
}

// A short grey puff at a point: "refused".
export function refused(pos, age, life = .5) {
  if (age < 0 || age >= life) return;
  const u = age / life;
  sprite({ x: pos.x, z: pos.z + u * .3 }, .3 + u * .3, .28 + u * .25, Smoke.withAlpha(.6 * (1 - u)), puff, Y + .17);
}

// ---- Release ---------------------------------------------------------------------------------
// The pieces of the Release drawing. `place(along, across)` is a floor point in the aim frame
// measured from the wearer; `dir` {x, z} is the aim on screen. All of it is light, tongues and
// puffs sampled from the clip time; nothing is a solid strip.
export const JetLand = .05, JetLife = .32;

// The blast out of the fist: a flash at the knuckles and five licks of flame that fan out and
// dive from hand height onto the floor, where the ground wave takes over. `root` is the floor
// point under the fist, `land` where the licks touch down, `side` the unit vector across the aim
// on the floor, `age` seconds since the release. The tips land in JetLand; from 0.08 s the roots
// leave the fist and the licks run out along their path, gone at JetLife.
export function jet(key, root, land, side, age, s, spread = .55, layer = Y + .06) {
  if (age < 0 || age >= JetLife) return;
  const tip = clamp(age / JetLand), tail = smooth(clamp((age - .08) / (JetLife - .08)));
  const fade = 1 - smooth(clamp((age - .12) / (JetLife - .12)));
  const at = (u, k) => {
    const h = HandH * (1 - u * u);
    return { x: lerp(root.x, land.x, u) + side.x * k * u, z: lerp(root.z, land.z, u) + side.z * k * u + h * Lift };
  };
  if (age < .12) {
    const f = 1 - age / .12;
    sprite(at(0, 0), .5 + .9 * f, .45 + .75 * f, Core.withAlpha(.95 * f), glow, layer + .004);
  }
  for (let i = 0; i < 5; i++) {
    const u = lerp(tail, tip, (i + .5) / 5);
    sprite(at(u, 0), .45 + u * .7, .4 + u * .55, Flame.withAlpha(.45 * fade), glow, layer - .002);
  }
  // Each lick: a strip narrow at the fist, widest near its end, with a round end.
  const wf = k => u => k * (.35 + .65 * u) * Math.sqrt(Math.max(0, 1 - Math.pow(u, 3)));
  for (let j = 0; j < 5; j++) {
    const k = (j - 2) / 2 * spread + Math.sin(s * 31 + j * 1.7) * .04, reach = tip * (.8 + .2 * rand(j + 900)), size = .8 + .4 * rand(j + 910);
    if (reach - tail < .04) continue;
    const pts = [];
    for (let i = 0; i <= 8; i++) {
      const u = lerp(tail, reach, i / 8), q = at(u, k);
      pts.push({ x: q.x + Math.sin(s * 40 + j + u * 9) * .02, z: q.z + Math.cos(s * 37 + j + u * 7) * .02 });
    }
    tube(`${key} lick ${j} ember`, pts, wf(.13 * size), Ember.withAlpha(.85 * fade), layer);
    tube(`${key} lick ${j} flame`, pts, wf(.085 * size), Flame.withAlpha(.9 * fade), layer + .001);
    tube(`${key} lick ${j} core`, pts, wf(.04 * size), Core.withAlpha(.95 * fade), layer + .002);
  }
  // Where it lands: a hot ring that runs out over the floor.
  if (age >= JetLand && age < JetLand + .25) {
    const u = (age - JetLand) / .25;
    circle(land, .25 + u * 1.1, .55 * (1 - u), Floor + .03, Flame);
  }
}

// The ground wave: fire running along the floor, as wide as the lit cells where it is. A wide
// faint light round the front, a hot line at the front and a glow fading a cell behind it; a row
// of tall tongues along a bowed line (the middle runs `bow` ahead of the edges) leaning forward,
// a lower row behind them; sparks thrown ahead. `D` is how far along the front is, `span`
// {lo, hi} the lit width there, `from` where the wave started (no light behind that), `amount`
// 0..1 shrinks it where the Heat runs out.
export function wave(key, place, D, span, dir, s, amount = 1, from = 0, bow = .3, layer = Y + .05) {
  if (amount <= 0) return;
  const mid = (span.lo + span.hi) / 2, half = Math.max(.25, (span.hi - span.lo) / 2);
  const front = k => D - bow * Math.pow((k - mid) / half, 2);
  // Flames rise north; a forward lean must not cancel that when the aim is south.
  const lean = { x: dir.x * .45, z: Math.max(-.2, dir.z * .45) };
  sprite(place(D - .3, mid), half * 2 + 2.2, half * 2 + 2.2, Flame.withAlpha(.13 * amount), glow, Floor + .017);
  const n = Math.max(2, Math.round((span.hi - span.lo) / .3));
  for (let i = 0; i <= n; i++) {
    const k = lerp(span.lo + .15, span.hi - .15, i / n), a = front(k);
    sprite(place(a - .05, k), .6, .55, Core.withAlpha(.2 * amount), glow, Floor + .021);
    if (a - .5 > from) sprite(place(a - .5, k), 1, .9, Flame.withAlpha(.16 * amount), glow, Floor + .02);
    if (a - 1.1 > from) sprite(place(a - 1.1, k), 1.1, 1, Ember.withAlpha(.12 * amount), glow, Floor + .019);
  }
  const m = Math.max(2, Math.round((span.hi - span.lo) / .27));
  for (let row = 1; row >= 0; row--) {
    const count = row ? m - 1 : m;
    for (let i = 0; i < count; i++) {
      const k = span.lo + (i + (row ? 1 : .5)) * (span.hi - span.lo) / m, r = rand(i * 7 + row * 50 + 1);
      const edge = 1 - .45 * Math.pow((k - mid) / (half + .15), 2);
      const h = (row ? .45 + .3 * r : .78 + .42 * r) * amount * edge, w = (row ? .13 : .17) * (.6 + .4 * amount);
      const base = place(front(k) - row * .35 + (rand(i * 7 + row * 50 + 2) - .5) * .12, k);
      const l = row ? { x: lean.x * .6, z: lean.z * .6 } : lean;
      tongue(`${key} ${row} ${i}`, base, h, w, s, 120 + i * 3 + row * 40, 1, layer + (row ? 0 : .004) + i * 1e-4, l);
      // The front row glows along its height, so it stays the brightest fire on screen.
      if (!row) sprite({ x: base.x + l.x * h * .4, z: base.z + h * .4 * Lift + l.z * h * .4 }, .5, .35 + h * .5, Flame.withAlpha(.2 * amount), glow, layer + .0035);
    }
  }
  for (let i = 0; i < 12; i++) {
    const life = .3 + rand(i + 640) * .15, ph = (s * 2.7 + rand(i + 600) * 3) % 1, age = ph * life;
    const k = lerp(span.lo, span.hi, rand(i + 610)), h = age * (1.8 + rand(i + 630)) - age * age * 6;
    if (h < 0) continue;
    const q = place(front(k) + .1 + age * (2.5 + rand(i + 620) * 2.5), k), pt = { x: q.x, z: q.z + h * Lift };
    sprite(pt, .17, .15, Flame.withAlpha((1 - ph) * .35 * amount), glow, Y + .119);
    sprite(pt, .07, .07, Core.withAlpha((1 - ph) * .95 * amount), glow, Y + .12);
  }
}

// Soot the wave leaves: puffs born along the front as it passes, rising (drawn north) and
// spreading for about 1.5 s, lit orange from below while they are young. `t0` is when the front
// left `from`, `speed` its cells per second, `to` where it stopped, `spanAt(d)` the lit width.
export function waveSmoke(key, place, t0, speed, from, to, spanAt, s, layer = Y + .09) {
  for (let k = 0; ; k++) {
    const d = from + .3 + k * .45;
    if (d > to) break;
    const sp = spanAt(d), born = t0 + (d - from) / speed;
    for (let j = 0; j < 2; j++) {
      const life = 1.3 + rand(k * 5 + j + 700) * .5, u = (s - born - j * .09) / life;
      if (u <= 0 || u >= 1) continue;
      const across = lerp(sp.lo + .25, sp.hi - .25, rand(k * 5 + j + 710)), g = place(d - .25 + u * .3, across), rise = .5 + u * 1.8;
      const pos = { x: g.x + Math.sin(u * 4 + k) * .1, z: g.z + rise * Lift }, a = Math.min(1, u * 5) * (1 - u);
      sprite(pos, .7 + u * 1.5, .6 + u * 1.2, Soot.withAlpha(.5 * a), puff, layer + k * 1e-4);
      if (u < .35) sprite(pos, .5 + u * .8, .42 + u * .7, Ember.withAlpha(.3 * (1 - u / .35)), glow, layer + k * 1e-4 + 5e-5);
    }
  }
}

// The low sheet of flame the wave leaves on a lit cell for a moment: four short tongues toward
// the cell's corners and a bright floor glow, so the lit cells read as one burning area until it
// dies down to the separate cell fires. `amount` 0..1.
export function sheet(key, c, s, amount, seed = 0, layer = Y + .025) {
  if (amount <= 0) return;
  sprite(c, 1.5, 1.3, Flame.withAlpha(.24 * amount), glow, Floor + .013);
  for (let i = 0; i < 4; i++) {
    const qx = (i % 2 ? .3 : -.3) + (rand(seed * 13 + i) - .5) * .16, qz = (i < 2 ? -.32 : .26) + (rand(seed * 13 + i + 20) - .5) * .14;
    tongue(`${key} ${i}`, { x: c.x + qx, z: c.z + qz }, (.2 + rand(seed * 13 + i + 40) * .2) * amount, .09 + .04 * amount, s, seed * 11 + i + 200, Math.min(1, amount * 1.5), layer - qz * 1e-3);
  }
}

// The gauntlet dumping its heat at the Release: pale smoke blown backward out of both elbow
// vents, spreading and rising, gone in 0.7 s. `g` is what gauntlet() returned.
export function exhaust(key, g, age, amount = 1) {
  if (age < 0 || age > .9 || amount <= 0) return;
  const side = { x: -g.d.z, z: g.d.x };
  g.vents.forEach((v, j) => {
    for (let i = 0; i < 4; i++) {
      const u = (age - i * .05) / .7;
      if (u <= 0 || u >= 1) continue;
      const back = .08 + easeOut(u) * (.5 + .1 * i), out = (j ? 1 : -1) * easeOut(u) * .18, rise = u * .3;
      sprite({ x: v.x - g.d.x * back + side.x * out, z: v.z - g.d.z * back + side.z * out + rise * Lift },
        .13 + u * .48, .11 + u * .4, Steam.withAlpha(.6 * (1 - u) * amount), puff, Y + .165);
    }
  });
}

// A thin wisp of smoke off the knuckles after the Release, four puffs 0.18 s apart.
export function wisp(pos, age, life = 1.2) {
  for (let i = 0; i < 4; i++) {
    const u = (age - i * .18) / life;
    if (u <= 0 || u >= 1) continue;
    sprite({ x: pos.x + Math.sin(u * 7 + i) * .05, z: pos.z + u * .9 * Lift }, .08 + u * .25, .08 + u * .22, Steam.withAlpha(.3 * Math.sin(Math.PI * u)), puff, Y + .166);
  }
}
