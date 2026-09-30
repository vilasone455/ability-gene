// Last Prism — weapon proposal, not the game. Nothing in Source/RimArt draws this yet.
// From Terraria's Last Prism (Moon Lord drop): a crystal prism held out in front that splits light
// into six coloured beams, which narrow over a few seconds into one beam.
//
// What it is for (ammo rule agreed in chat 2026-09-30; damage numbers are placeholders, not agreed):
//   Ammo is stored sunlight. The prism holds 12 s of beam and fills whenever it is on an unroofed
//   cell, held or lying on the ground, at 1 s of beam per 20 s at full sun, scaled by the sky glow
//   (0 at night, less in an eclipse). No charging under a roof, on a pocket map, or from sun lamps.
//   A meter on the weapon's button shows the charge. Firing spends 1 s of charge per second.
//   Fire (hold): the wielder targets a pawn and channels. Six beams leave the prism in a level fan,
//   35 degrees to either side of the aim, and sweep across each other; over 3 s the fan narrows and
//   the sweep speeds up, and at 3 s the six join into one beam 1 cell wide. The prism turns to follow
//   the target at up to 45 degrees per second. Beams pass through pawns and stop at walls; range 22
//   cells. The channel ends when the target is down, the order is cancelled or the charge is empty;
//   an empty prism sputters out.
//   Damage (placeholders): each fan beam 2 per hit, at most one hit per beam per pawn every 0.25 s;
//   the joined beam 15 every 0.25 s (60 per second) to every pawn in its lane. Whether it hurts
//   friends, and any cooldown past the charge, are open.
//
// Order (defaults, scenario "fires"):
//   0.00  the wielder holds the prism 0.42 cells ahead at chest height; it turns slowly, bobs,
//         glints, and casts a small rainbow on the floor where its shadow falls (it is in sun);
//         the meter over the head is full (12)
//   0.30  channel: the rainbow goes; six beams (red to violet, hues drifting) leave the prism face
//         0.2 cells apart and sweep a 35-degree fan every 1.8 s; each lights the floor under it in
//         its colour; flow sparks run out along them. Beams that sweep over the wall stop on it with
//         sparks, so the raider behind it is never touched. The raider inside the fan flickers in
//         each beam's colour as one crosses him; the one outside the fan is untouched. Opacity
//         goes .2 to .45 over the first two thirds and to 1 in the last third, while the sweep
//         speeds up to one pass per 0.6 s and the prism spins up to 2.5 turns a second (Terraria's
//         curve). The prism follows the target, who is walking across the aim.
//   3.30  join: white flash and ring at the prism, a pulse runs down the beam; one beam 1 cell wide
//         with a thin white core and pale sheath over six coloured bands side by side (red on one
//         edge to violet on the other, white where they overlap), sparkles drifting off, flow lines
//         running out, rainbow light on the floor under it. The target in the lane glows white
//         with sparks.
//   5.30  the target has been in the joined beam 2 s and goes down; the beam lets go over 0.25 s
//   5.55  held 1.4 s: the target down and smoking, the meter at 7 of 12, the rainbow back on the floor
// "runs dry" starts with 4 s in the meter: the beams join at 3.3, the meter empties at 4.3, the
//   beams flicker out over 0.35 s and the meter flashes red; the target survives and walks on.
// "charges in the sun" shows the meter filling from 3 at 20x speed (one segment a second at full
//   sun) with a flash at the prism as each segment fills. "under a roof": the game's roof overlay
//   stand-in over the wielder, no rainbow, no glints, the meter stays at 3.
//
// Drawing: every beam is a level line at chest height (lib/pawn.js chest, .05 north of the cell
// centre), so the fan lies over the ground and turns freely with the aim; no per-facing method.
// Terraria's side-view fan becomes a fan over the floor, which is also the hit area. The beams are
// soft additive strips (outer colour, lighter middle, thin white core) with rounded ends; a beam
// that reaches its range tapers over the last 1.5 cells. Aiming north (sin > .3) the prism and the
// beams draw under the pawn layer so they pass behind the wielder's head. The prism is a flat
// triangle turned in the plane, faces lit by the scene's sun. The meter over the head stands in for
// the gizmo, and the roof cells for the game's roof overlay. Pawns are lib/pawn.js stand-ins; walls
// are the Paper Bomb kit's. The aim, the walk and who goes down are replayed from 0 at 60 steps a
// second each frame (in the aim frame, without walls: the layout keeps walls off the target's line).
import { AltitudeLayer, Color, Mathf, MaterialPool, ShaderDatabase } from '../js/engine.js';
import { P, Body, Y, Floor, sprite, soft, glow, rand } from './lib/six-paths-impact.js';
import { draw, mesh } from './lib/six-paths-solid.js';
import { pawn, at, shadowLayer, pawnLayer, Skin } from './lib/pawn.js';
import { Enemy } from './lib/chain-sickle.js';
import { strip, streak, glint, ringAt, whiteGlow } from './lib/goku.js';
import { walls } from './lib/paper-bomb.js';

const clamp = Mathf.Clamp01, smooth = Mathf.Smooth, lerp = Mathf.Lerp, D2R = Mathf.Deg2Rad, TAU = Math.PI * 2;
const flat = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const White = new Color(1, 1, 1), Crystal = new Color(.74, .93, 1), CrystalDeep = new Color(.22, .5, .82), CrystalEdge = new Color(.92, .98, 1);
const Dull = new Color(.45, .5, .56), Sun = new Color(1, .82, .32), SunDim = new Color(.22, .18, .1), Warn = new Color(.85, .18, .12);
const RoofTint = new Color(.55, .64, .82), Wielder = new Color(.30, .50, .62), Smoke = new Color(.32, .32, .34);
// Decided looks and the rule's fixed numbers.
const Beams = 6, Lead = .3, Tail = 1.4, Fade = .25, Sputter = .35, JoinFlash = .3, ShowTime = 6;
const Store = 12, DryStore = 4, StartLevel = 3;       // seconds of beam: full, the "runs dry" start, the charging start
const Turn = 45;                                      // degrees per second the prism turns after its target
const SweepSlow = 1.8, SweepFast = .6;                // seconds per sweep of the fan: at the start, and in the last half
const SpinIdle = .25, SpinFull = 2.5, SpinStop = 3;   // prism turns per second; how fast it winds down after
const StartSide = .2, EndSide = .05;                  // cells across the aim where the beams leave the prism face
const FanHalf = .16, HitReach = .3;                   // half width of a fan beam's outer glow; a pawn this close is crossed
const PrismAhead = .42, PrismR = .2, PrismH = .98;   // cells ahead of the wielder, triangle radius, lab height (chest)
const ChestLift = .05;                                // lib/pawn.js: the chest is .05 north of the cell centre on screen
// Pawns in the aim frame from the wielder: [cells along, cells across]. The target walks from +2.5 to
// -2.5 across over WalkTime; the others stand. The wall is three cells across the fan's right side.
const Cast = [
  { along: 12, from: 2.5, to: -2.5, walks: true },   // the target
  { along: 6, from: 2.4 },                            // inside the fan
  { along: 13.5, from: -5 },                          // behind the wall
  { along: 6, from: -6.5 },                           // outside the fan
];
const WalkTime = 6.3, WallAt = 10, WallAcross = [-3, -4, -5];

const shoots = p => p.scenario === 'fires' || p.scenario === 'runs dry';
const bump = x => (x >= 0 && x <= 1) ? Math.sin(x * Math.PI) : 0;
function hue(h, sat = .72) {
  h = ((h % 1) + 1) % 1;
  const k = n => { const q = (n + h * 6) % 6; return 1 - sat * Math.max(0, Math.min(q, 4 - q, 1)); };
  return new Color(k(5), k(3), k(1));
}
const pale = c => Color.Lerp(c, White, .45);

// True when q is within reach of the line from a along ang, between 0 and len cells out.
function onLine(q, a, ang, len, reach) {
  const dx = Math.cos(ang), dz = Math.sin(ang), rx = q.x - a.x, rz = q.z - a.z, along = rx * dx + rz * dz;
  return along > 0 && along < len && Math.abs(rz * dx - rx * dz) <= reach;
}
// Cells from a along ang to the first wall cell, or max.
function rayWall(a, ang, cells, max) {
  const d = [Math.cos(ang), Math.sin(ang)], o = [a.x, a.z];
  let best = max;
  for (const c of cells) {
    const lo = [c.x - .5, c.z - .5], hi = [c.x + .5, c.z + .5];
    let t0 = -Infinity, t1 = Infinity;
    for (let k = 0; k < 2; k++) {
      if (Math.abs(d[k]) < 1e-9) { if (o[k] < lo[k] || o[k] > hi[k]) t0 = Infinity; continue; }
      let ta = (lo[k] - o[k]) / d[k], tb = (hi[k] - o[k]) / d[k];
      if (ta > tb) [ta, tb] = [tb, ta];
      t0 = Math.max(t0, ta); t1 = Math.min(t1, tb);
    }
    if (t0 <= t1 && t0 > 0 && t0 < best) best = t0;
  }
  return best;
}

// The aim, the walk and who goes down, replayed from 0 in the aim frame (wielder at 0,0, aim east).
// The channel ends when the target goes down or the charge runs out.
let cached = { key: '', value: null };
function replay(p) {
  const key = [p.scenario, p.join, p.lasts, p.width, p.walker, p.range].join('|');
  if (cached.key === key) return cached.value;
  const dt = 1 / 60, lead = Lead, join = lead + p.join, dry = lead + (p.scenario === 'runs dry' ? DryStore : Store);
  const down = Cast.map(() => Infinity), exposure = Cast.map(() => 0);
  const local = (j, s) => {
    const c = Cast[j];
    if (!c.walks) return { x: c.along, z: c.from };
    if (!p.walker) return { x: c.along, z: 0 };
    return { x: c.along, z: lerp(c.from, c.to, clamp(Math.min(s, down[j]) / WalkTime)) };
  };
  let release = dry, aim = Math.atan2(local(0, 0).z, local(0, 0).x);
  const n = Math.ceil((dry + Sputter + Tail + .5) / dt), aims = new Float32Array(n + 1);
  for (let k = 0; k <= n; k++) {
    const s = k * dt, tgt = local(0, s), want = Math.atan2(tgt.z, tgt.x), step = Turn * D2R * dt;
    aim += Math.max(-step, Math.min(step, want - aim));
    aims[k] = aim;
    if (s < join || s >= release) continue;
    const prism = { x: Math.cos(aim) * PrismAhead, z: Math.sin(aim) * PrismAhead };
    for (let j = 0; j < Cast.length; j++) {
      if (down[j] < Infinity || !onLine(local(j, s), prism, aim, p.range, p.width / 2)) continue;
      exposure[j] += dt;
      if (exposure[j] >= p.lasts) { down[j] = s; if (j === 0) release = s; }
    }
  }
  const value = { aims, dt, lead, join, release, dried: release >= dry, down, local };
  cached = { key, value };
  return value;
}

function times(p) {
  if (!shoots(p)) return { end: ShowTime };
  const r = replay(p);
  return { lead: r.lead, join: r.join, release: r.release, dried: r.dried, joins: r.join < r.release, end: r.release + (r.dried ? Sputter : Fade) + Tail };
}

// Fan sweep phase (radians): one pass per SweepSlow, speeding up to one per SweepFast over the second
// half of the charge, as Terraria's spin rate drops from 16 to 6 frames.
function sweep(s, t, p) {
  const w0 = TAU / SweepSlow, w1 = TAU / SweepFast, a = t.lead + p.join * .5, L = p.join * .5, r = clamp((s - a) / L);
  const g = s <= a + L ? L / 3 * r * r * r : L / 3 + (s - a - L);
  return w0 * (s - t.lead) + (w1 - w0) * g;
}
// Prism turns so far: idle, spinning up with the charge squared, full while joined, winding down after.
function spin(s, t, p) {
  if (!t.lead) return SpinIdle * s;
  const J = p.join, u = clamp((s - t.lead) / J), held = Math.min(s, t.release);
  let g = J / 3 * u * u * u + Math.max(0, held - t.lead - J);
  if (s > t.release) g += (1 - Math.exp(-SpinStop * (s - t.release))) / SpinStop;
  return SpinIdle * s + (SpinFull - SpinIdle) * g;
}

// A soft beam layer from a along ang: rounded at the start, tapered over softEnd cells at the end,
// with a ripple running out along it. off(d) shifts it across the line d cells out.
function ray(key, a, ang, len, half, colour, layer, softEnd, i, s, off = null) {
  if (colour.a <= .002 || len <= .05) return;
  const dx = Math.cos(ang), dz = Math.sin(ang), n = Math.max(2, Math.ceil(len / .4)), L = [], R = [];
  for (let k = 0; k <= n; k++) {
    const d = len * k / n, w = half * Math.sqrt(clamp(d / .2) * clamp((len - d) / softEnd)) * (1 + .07 * Math.sin(d * 1.7 - s * 26 + i * 1.3)) + .003;
    const shift = off ? off(d) : 0, x = a.x + dx * d - dz * shift, z = a.z + dz * d + dx * shift;
    L.push({ x: x - dz * w, z: z + dx * w }); R.push({ x: x + dz * w, z: z - dx * w });
  }
  strip(key, L, R, colour, whiteGlow, layer);
}
const around = (c, ang, d) => ({ x: c.x + Math.cos(ang) * d, z: c.z + Math.sin(ang) * d });

// The prism: a flat triangle turned in the plane, a pale edge, three faces lit by the sun.
function prism(c, turns, sunAng, layer, dull) {
  const corner = (k, R) => around(c, turns * TAU + k * TAU / 3, R);
  const tri = (key, v0, v1, v2, colour) => { const m = mesh(key); m.setFlat([v0.x, v0.z, v1.x, v1.z, v2.x, v2.z], [0, 1, 2]); draw(m, 0, layer, 0, 1, 1, 0, colour, flat); };
  tri('last prism edge', corner(0, PrismR * 1.22), corner(1, PrismR * 1.22), corner(2, PrismR * 1.22), (dull ? Dull : CrystalEdge).withAlpha(.95));
  for (let k = 0; k < 3; k++) {
    const face = turns * TAU + (k + .5) * TAU / 3, lit = .5 + .5 * Math.cos(face - sunAng);
    const colour = Color.Lerp(CrystalDeep, Crystal, lit);
    layer += .0005;
    tri(`last prism face ${k}`, c, corner(k, PrismR), corner(k + 1, PrismR), (dull ? Color.Lerp(colour, Dull, .7) : colour).withAlpha(.92));
  }
}
// The spectrum the prism throws on the floor in sun: six stripes red to violet, along the sun.
function rainbow(c, sun, amount, s) {
  if (amount <= 0) return;
  const l = Math.hypot(sun.x, sun.z) || 1, ux = sun.x / l, uz = sun.z / l, deg = Math.atan2(uz, ux) / D2R;
  for (let k = 0; k < Beams; k++) {
    const off = (k - 2.5) * .07 + .012 * Math.sin(s * 2.4 + k);
    sprite({ x: c.x - uz * off + ux * .3, z: c.z + ux * off + uz * .3 }, .9, .11, hue(k / Beams, .9).withAlpha(.5 * amount), glow, Floor + .015 + k * .0005, -deg);
  }
}
// The gizmo's meter, drawn over the head: 12 segments, one per second of beam.
function meter(head, level, warn) {
  const pitch = .095, w = .075, h = .1, x0 = head.x - pitch * (Store - 1) / 2, z = head.z + .28;
  if (warn > 0) sprite({ x: head.x, z }, pitch * Store + .12, h + .12, Warn.withAlpha(.8 * warn), flat, Y + .3);
  sprite({ x: head.x, z }, pitch * Store + .05, h + .05, Body.withAlpha(.8), flat, Y + .301);
  for (let k = 0; k < Store; k++) {
    const x = x0 + k * pitch, fill = clamp(level - k);
    sprite({ x, z }, w, h, SunDim, flat, Y + .302);
    if (fill > 0) sprite({ x: x - w / 2 + w * fill / 2, z }, w * fill, h, Sun, flat, Y + .303);
  }
}

export default {
  kit: 'Last Prism', label: 'Last Prism (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'fires', options: ['fires', 'runs dry', 'charges in the sun', 'under a roof'], group: 'Showcase' },
    aim: P('Aim (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    walker: { label: 'The target walks across the aim', value: true, group: 'Showcase' },
    range: P('Range (cells)', 22, 10, 40, 1, 'Shape'),
    fan: P('Fan, each side of the aim (degrees)', 35, 10, 60, 1, 'Shape'),
    width: P('Joined beam width, the lane that is hit (cells)', 1, .5, 2, .1, 'Shape'),
    join: P('Beams join after', 3, 1, 5, .1, 'Timing (s)'),
    lasts: P('Target lasts in the joined beam', 2, .5, 5, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    if (p.scenario === 'charges in the sun') return [{ name: 'In the sun (time x20)', t: 0 }];
    if (p.scenario === 'under a roof') return [{ name: 'Under a roof: no charge', t: 0 }];
    const t = times(p);
    return [{ name: 'Hold', t: 0 }, { name: 'Fan (channel)', t: t.lead }, ...(t.joins ? [{ name: 'Beams join', t: t.join }] : []),
      { name: t.dried ? 'Charge runs out' : 'Target down, beam stops', t: t.release }];
  },
  events(p) {
    const t = times(p);
    return shoots(p) && t.joins ? [{ t: t.join, type: 'shake', value: .05 }] : [];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32, light = clamp(strength / .32);
    const sunAng = Math.atan2(-sun.z, -sun.x), firing = shoots(p), roofed = p.scenario === 'under a roof';
    const a0 = p.aim * D2R, ca = Math.cos(a0), sa = Math.sin(a0);
    const caster = firing ? { x: o.x - ca * 7, z: o.z - sa * 7 } : o;
    const world = q => ({ x: caster.x + q.x * ca - q.z * sa, z: caster.z + q.x * sa + q.z * ca });
    const r = firing ? replay(p) : null;
    const theta = r ? a0 + r.aims[Math.min(r.aims.length - 1, Math.round(s / r.dt))] : a0;
    const dx = Math.cos(theta), dz = Math.sin(theta), north = Math.sin(theta) > .3;
    const bob = .025 * Math.sin(s * 2.4);
    const prismG = { x: caster.x + dx * PrismAhead, z: caster.z + dz * PrismAhead }, prismS = { x: prismG.x, z: prismG.z + ChestLift + bob };
    const beamLayer = north ? AltitudeLayer.Projectile.AltitudeFor() : Y, prismLayer = north ? pawnLayer - .03 : pawnLayer + .06;

    // State of the channel at this time.
    const u = firing ? clamp((s - t.lead) / p.join) : 0, after = firing ? s - t.release : -1, endFade = t.dried ? Sputter : Fade;
    const flicker = t.dried && after > 0 ? (rand(Math.floor(s * 25) + 7) > .4 ? 1 : .15) : 1;
    const live = !firing || s < t.lead ? 0 : (after < 0 ? 1 : Math.max(0, 1 - after / endFade) * flicker) * clamp((s - t.lead) / .15);
    const shrink = after > 0 ? 1 - .5 * clamp(after / endFade) : 1, joined = firing && t.joins && s >= t.join;
    const opacity = u <= .66 ? lerp(.2, .45, u / .66) : lerp(.45, 1, (u - .66) / .34);
    const snap = q => ({ x: o.x + Math.round(q.x - o.x), z: o.z + Math.round(q.z - o.z) });
    const cells = firing ? WallAcross.map(ac => snap(world({ x: WallAt, z: ac }))) : [];

    // The six beams: a level fan swept by cos(phase + i/6 turn), narrowing with the charge.
    const beams = [];
    if (live > 0) {
      const ph = sweep(s, t, p), spread = p.fan * D2R * (1 - u), side = lerp(StartSide, EndSide, u);
      for (let i = 0; i < Beams; i++) {
        const c = Math.cos(ph + i * TAU / Beams), ang = theta + spread * c, start = { x: prismG.x - dz * side * c, z: prismG.z + dx * side * c };
        const len = rayWall(start, ang, cells, p.range);
        beams.push({ i, ang, start, len, blocked: len < p.range - 1e-6, colour: hue(i / Beams + s * .12), ph: ph + i * TAU / Beams });
      }
    }
    const lane = joined ? rayWall(prismG, theta, cells, p.range) : 0, laneBlocked = joined && lane < p.range - 1e-6;

    // --- the floor: roof overlay, the rainbow in sun ------------------------------------------------------------------
    if (roofed) for (let ix = -3; ix <= 3; ix++) for (let iz = -2; iz <= 2; iz++)
      sprite({ x: o.x + ix, z: o.z + iz }, .92, .92, RoofTint.withAlpha(.2), flat, Y + .25);
    sprite({ x: prismG.x + sun.x * PrismH, z: prismG.z + sun.z * PrismH }, .34, .2, Body.withAlpha(strength * .45), soft, shadowLayer);
    if (!roofed && live === 0) rainbow({ x: prismG.x + sun.x * PrismH, z: prismG.z + sun.z * PrismH }, sun, light, s);

    if (firing) walls('last prism wall', o, cells, sun, strength);

    // --- pawns, north first -------------------------------------------------------------------------------------------
    const people = firing ? Cast.map((c, j) => {
      const pos = world(r.local(j, s)), down = s >= r.down[j];
      const crossed = down || joined ? [] : beams.filter(b => onLine(pos, b.start, b.ang, b.len, HitReach));
      const inLane = !down && joined && live > 0 && onLine(pos, prismG, theta, lane, p.width / 2);
      return { pos, down, crossed, inLane, j };
    }) : [];
    const figures = [...people, { pos: caster, caster: true }].sort((m, n) => n.pos.z - m.pos.z);
    figures.forEach(g => {
      if (g.caster) {
        pawn(g.pos, { shirt: Wielder, sun, shadow: strength });
        prism(prismS, spin(s, t, p), sunAng, prismLayer, roofed);
        return;
      }
      const heat = g.inLane ? .75 + .2 * Math.sin(s * 40) : g.crossed.length ? .3 : 0;
      pawn(g.pos, { shirt: Color.Lerp(Enemy, White, heat), skin: Color.Lerp(Skin, White, heat), sun, shadow: strength, downed: g.down });
    });

    // --- the prism: glints in sun, the glow as it charges ---------------------------------------------------------------
    if (!roofed && live === 0) {
      const v = (s * .8) % 1;
      glint('last prism glint', { x: prismS.x + .05, z: prismS.z + .06 }, .2, .9 * bump(v / .25) * light, White, 20);
    }
    if (live > 0) {
      const size = .45 + .6 * u;
      sprite(prismS, size, size, hue(s * .5).withAlpha((.35 + .4 * u) * live), glow, prismLayer + .01);
      sprite(prismS, size * .45, size * .45, White.withAlpha((.4 + .5 * u) * live), glow, prismLayer + .011);
    }

    // --- the fan -------------------------------------------------------------------------------------------------------
    if (!joined) beams.forEach(b => {
      const key = `last prism beam ${b.i}`, al = opacity * live, wf = lerp(.45, 1, u) * shrink, end = b.blocked ? .15 : 1.5;
      const a = { x: b.start.x, z: b.start.z + ChestLift + bob }, bx = Math.cos(b.ang), bz = Math.sin(b.ang);
      const pt = d => ({ x: a.x + bx * d, z: a.z + bz * d });
      sprite({ x: b.start.x + bx * b.len / 2, z: b.start.z + bz * b.len / 2 }, b.len, .6 * wf, b.colour.withAlpha(.08 * al), glow, Floor + .01 + b.i * .0005, -b.ang / D2R);
      ray(`${key} outer`, a, b.ang, b.len, FanHalf * wf, b.colour.withAlpha(.24 * al), beamLayer + .01, end, b.i, s);
      ray(`${key} mid`, a, b.ang, b.len, FanHalf * .5 * wf, pale(b.colour).withAlpha(.45 * al), beamLayer + .011, end, b.i, s);
      ray(`${key} core`, a, b.ang, b.len, FanHalf * .16 * wf, White.withAlpha(.75 * al), beamLayer + .012, end, b.i, s);
      sprite(a, .32, .32, b.colour.withAlpha(.5 * al), glow, beamLayer + .013);
      for (let f = 0; f < 2; f++) {
        const d = (s * 16 + rand(b.i * 7 + f) * b.len) % b.len;
        if (d > .5 && d < b.len - 1.2) streak(`${key} flow ${f}`, pt(d), pt(d + .9), .05 * wf, White.withAlpha(.55 * al), whiteGlow, beamLayer + .013, 3);
      }
      if (b.blocked) {
        const e = pt(b.len);
        sprite(e, .7, .7, b.colour.withAlpha(.6 * al), glow, Y + .014);
        sprite(e, .22, .22, White.withAlpha(.85 * al), glow, Y + .015);
        for (let k = 0; k < 3; k++) {
          const v = (s * 5 + rand(b.i * 5 + k)) % 1, back = b.ang + Math.PI + (rand(b.i * 5 + k + 50) - .5) * 2.2, r0 = .1 + v * .6;
          streak(`${key} spark ${k}`, around(e, back, r0), around(e, back, r0 + .25), .04, pale(b.colour).withAlpha((1 - v) * al), whiteGlow, Y + .016, 3);
        }
      }
    });

    // --- the joined beam ---------------------------------------------------------------------------------------------------
    if (joined && live > 0) {
      const inF = clamp((s - t.join) / .15) * live, W = p.width * shrink, L = lane, end = laneBlocked ? .2 : 1.8, tint = hue(s * .25, .55);
      const a = { x: prismS.x, z: prismS.z }, pt = (d, across = 0) => ({ x: a.x + dx * d - dz * across, z: a.z + dz * d + dx * across });
      const mid = { x: prismG.x + dx * L / 2, z: prismG.z + dz * L / 2 };
      sprite(mid, L, W * 2.4, tint.withAlpha(.12 * inF), glow, Floor + .012, -theta / D2R);
      sprite(mid, L, W * 1.1, White.withAlpha(.06 * inF), glow, Floor + .013, -theta / D2R);
      ray('last prism fused glow', a, theta, L, W / 2 * 1.15, pale(tint).withAlpha(.1 * inF), beamLayer + .02, end, 0, s);
      // The six beams, joined: six bands side by side across the beam, red on one edge to violet on the other,
      // each wobbling a little on its own. They overlap into white in the middle and keep their colour at the edges.
      for (let k = 0; k < Beams; k++) {
        const across = (k - 2.5) * W * .14;
        ray(`last prism band ${k}`, a, theta, L, W * .1, hue(k / Beams + .04 * Math.sin(s * .7), .95).withAlpha(.3 * inF), beamLayer + .021 + k * .0003, end, k, s,
          d => across + .025 * Math.sin(d * .8 - s * 10 + k * 1.7));
      }
      ray('last prism fused sheath', a, theta, L, W / 2 * .32, Crystal.withAlpha(.35 * inF), beamLayer + .024, end, 0, s);
      ray('last prism fused core', a, theta, L, W / 2 * .12, White.withAlpha(.9 * inF), beamLayer + .0245, end, 0, s);
      for (let f = 0; f < 10; f++) {
        const d = (s * 30 + rand(f + 11) * L) % L, across = (rand(f + 20) - .5) * W * .6;
        if (d > .6 && d < L - 1.6) streak(`last prism fused flow ${f}`, pt(d, across), pt(d + 1.4, across), .06, White.withAlpha(.5 * inF), whiteGlow, beamLayer + .027, 3);
      }
      for (let g = 0; g < 12; g++) {
        const v = (s * 1.3 + rand(g + 40)) % 1, d = .8 + rand(g + 41) * (L - 1.6), sideways = (rand(g + 42) > .5 ? 1 : -1) * (W * .3 + v * W * .6);
        glint(`last prism sparkle ${g}`, pt(d, sideways), .06 + .1 * (1 - v), .85 * bump(v) * inF, hue(rand(g) + s * .2, .6), 45 * v);
      }
      const pulse = (s - t.join) / .2;
      if (pulse < 1) sprite(pt(L * pulse), W * 2.5, W * 2.5, White.withAlpha(.8 * (1 - pulse)), glow, beamLayer + .028);
      if (laneBlocked) {
        const e = pt(L);
        sprite(e, W * 1.8, W * 1.8, tint.withAlpha(.6 * inF), glow, Y + .03);
        sprite(e, W * .7, W * .7, White.withAlpha(.85 * inF), glow, Y + .031);
      }
      const flare = 1 + .08 * Math.sin(s * 47);
      sprite(prismS, W * 2.2 * flare, W * 2.2 * flare, tint.withAlpha(.45 * inF), glow, prismLayer + .012);
      sprite(prismS, .6, .6, White.withAlpha(.9 * inF), glow, prismLayer + .013);
      glint('last prism flare', { x: prismS.x, z: prismS.z }, .55, .8 * inF, White, s * 200);
    }
    if (firing && t.joins && s >= t.join && s < t.join + JoinFlash) {
      const v = (s - t.join) / JoinFlash;
      sprite(prismS, 3.2, 3.2, White.withAlpha(.7 * (1 - v) * (1 - v)), glow, Y + .2);
      ringAt(prismS, .25 + 1.8 * smooth(v), White.withAlpha(.8 * (1 - v)), Y + .21, false, whiteGlow);
      ringAt(prismS, .15 + 1.2 * smooth(v), hue(s * .25).withAlpha(.7 * (1 - v)), Y + .211, false, whiteGlow);
    }

    // --- hits on pawns: a flicker in each crossing beam's colour, white and sparks in the joined beam, smoke once down -------
    people.forEach(g => {
      const chest = at(g.pos, 'chest'), f = Math.floor(s * 20);
      g.crossed.forEach((b, k) => {
        sprite(chest, .45, .45, b.colour.withAlpha(.65 * live * (rand(f + b.i * 13) > .3 ? 1 : .4)), glow, Y + .04 + k * .001);
        const ang = rand(f * 3 + b.i) * TAU;
        streak(`last prism graze ${g.j} ${b.i}`, around(chest, ang, .12), around(chest, ang, .38), .04, pale(b.colour).withAlpha(.8 * live), whiteGlow, Y + .045, 3);
      });
      if (g.inLane) {
        sprite(chest, .8, .8, White.withAlpha(.55 * live), glow, Y + .05);
        ringAt(chest, .25 + ((s * 3) % 1) * .5, White.withAlpha(.7 * (1 - (s * 3) % 1) * live), Y + .051, false, whiteGlow);
        for (let k = 0; k < 5; k++) {
          const v = (s * 4 + rand(k + 60)) % 1, ang = theta + (rand(k + 61) - .5) * 1.6 + (k % 2 ? .9 : -.9), r0 = .2 + v * .7;
          streak(`last prism burn ${g.j} ${k}`, around(chest, ang, r0), around(chest, ang, r0 + .3), .05, hue(rand(k) + s * .3, .5).withAlpha((1 - v) * live), whiteGlow, Y + .052, 3);
        }
      }
      if (g.down) for (let k = 0; k < 5; k++) {
        const born = r.down[g.j] + k * .25, v = (s - born) / 1.2;
        if (v < 0 || v > 1) continue;
        sprite({ x: g.pos.x + (rand(k + 70) - .5) * .4 + v * .2, z: g.pos.z + .1 + v * .8 }, .35 + v * .5, .3 + v * .4, Smoke.withAlpha(.35 * bump(v)), soft, Y + .004);
      }
    });

    // --- the meter (the gizmo's stand-in) and the charging flash ------------------------------------------------------------
    let level = StartLevel, warn = 0;
    if (firing) {
      level = Math.max(0, (p.scenario === 'runs dry' ? DryStore : Store) - clamp01span(s - t.lead, t.release - t.lead));
      if (t.dried && after > 0 && after < 1.2) warn = Math.floor(after * 6) % 2 ? 1 : .25;
    } else if (!roofed) {
      level = Math.min(Store, StartLevel + s * light);
      const done = level % 1;
      if (s > .1 && level < Store && done < .15) sprite(prismS, .9, .9, Sun.withAlpha(.55 * (1 - done / .15) * light), glow, prismLayer + .014);
    }
    meter(at(caster, 'headTop'), level, warn);
  },
};

function clamp01span(x, max) { return Math.max(0, Math.min(x, max)); }
