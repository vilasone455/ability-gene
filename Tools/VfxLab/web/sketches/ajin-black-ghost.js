// Satō (Ajin) — Black Ghost (IBM). Satō's hero kit, active 4, Descent only.
//
// Mechanic (agreed 2026-09-26, numbers are XML placeholders): summons Satō's Black Ghost next to
// him for 45 s. It is a real pawn (vanilla pathing, targeting and melee) whose look is drawn in
// code: a fast melee fighter with claws that takes 50 % damage. One at a time, cooldown 120 s.
// When the time runs out it dissolves. Order "Ghost Relay": right-click one of Satō's anchors
// (a severed part from Sever); the ghost picks it up, carries it up to 30 cells, sets it down and
// dissolves at once (Relay may be dropped). Order "Tear" (agreed 2026-09-26, replaces the
// rejected "Take my gun": a pawn-sized gun looked like a toy in its hands): the ghost grabs an
// adjacent enemy, holds it 1 s and rips off an arm or a leg (picked like Sever). Arm: the enemy
// drops its weapon and loses manipulation. Leg: it can barely walk; a second leg downs it. Once
// per summon, fleshy targets only (humans, animals; not mechs). The sketch compresses the 45 s
// into a few seconds.
//
// Beats: summon — black matter oozes out of Satō's shoulders and pours sideways; the ghost builds
// up from the feet, the rising edge throwing flakes (1.2 s). Idle and moving — flakes peel off the
// edges and drift up the whole time, more while it walks. Fight — walks to the enemy, two claw
// swipes (left, right) with pale claw streaks, blood and a camera shake; the enemy flinches, then
// goes down. Tear — walks up, grabs the near arm or leg (0.3 s), leans back and pulls while the
// enemy shakes (0.7 s), rips it off with a blood burst and a shake, tosses the limb aside where it
// stays; arm: the enemy staggers back and its rifle drops; leg: it falls; the stump bleeds. The
// enemy stand-in gets drawn arms, legs and a rifle in this scenario so the limb reads.
// Relay — walks to the severed hand, bends and picks it up, carries it, sets it down,
// and dissolves. Dissolve — it breaks into flakes from the feet up (1.0 s). The downed enemy, the
// blood and the dropped anchor stay after the effect.
//
// Look, from the sources: manga vol. 3 cover and a manga panel (funnel head, contour lines,
// long arms), the official anime icon and key visual (dark grey body wrapped in bands with lighter
// band lines, black flakes drifting up), Ajin wiki (triangle head seen from above and flat from
// the side, six fingers per hand, over 2 m tall).
//
// Drawing: see lib/ajin.js. The ghost is drawn like a pawn sprite, upright in the screen plane;
// south = front pose, north = the same pose darker with the claws behind, east = its own profile
// pose (per-facing method for the port), west = east mirrored. Diagonals use the side view, as the
// game picks it for a walking pawn (see facingOf). Stand-in pawns are drawn at the
// real pawn's size (1.3x the lab stand-in, 0.33 lower) so the ghost's 1.25x height reads right.
import { Color, Meshes, Mathf } from '../js/engine.js';
import { P } from './lib/six-paths-impact.js';
import {
  ghost, flakes, edgeFlakes, ooze, standIn, shard, Top, Stand, Lift, Floor, Y, pawnLayer,
  Shirt, Cap, Enemy, Blood, Slash, Skin, Flake, hand, draw, disc, band, sprite, trail, soft, puff, rand, smooth, clamp, lerp, TAU,
} from './lib/ajin.js';

const ring = Meshes.band(.93, 1, 48, 'ajin anchor ring');
const Dirs = {
  east: { x: 1, z: 0 }, west: { x: -1, z: 0 }, north: { x: 0, z: 1 }, south: { x: 0, z: -1 },
  northeast: { x: 0.7071, z: 0.7071 }, northwest: { x: -0.7071, z: 0.7071 }, southeast: { x: 0.7071, z: -0.7071 }, southwest: { x: -0.7071, z: -0.7071 },
};
// The game's rule (Pawn_RotationTracker.FaceAdjacentCell): a step with any sideways part faces east
// or west, so every diagonal uses the side view; only straight north or south steps use the back or
// front. RotFromAngleBiased for facing a target agrees for 45 degrees.
// Facing a target (Pawn_RotationTracker.RotFromAngleBiased): north or south only within 30
// degrees of straight up or down, east or west otherwise.
const facingAt = v => { const a = Mathf.Repeat(Math.atan2(v.x, v.z) / Mathf.Deg2Rad, 360); return a < 30 || a >= 330 ? 'north' : a < 150 ? 'east' : a < 210 ? 'south' : 'west'; };
const facingOf = v => Math.abs(v.x) > .2 ? (v.x > 0 ? 'east' : 'west') : (v.z >= 0 ? 'north' : 'south');
const add = (a, b, k = 1) => ({ x: a.x + b.x * k, z: a.z + b.z * k });
const sub = (a, b) => ({ x: a.x - b.x, z: a.z - b.z });
const len = v => Math.hypot(v.x, v.z);
const bump = (x) => (x > 0 && x < 1) ? Math.sin(x * Math.PI) : 0;
const Reach = 1.0;      // melee stop distance, cells
const Pause = .35;      // idle after forming
const Pick = .55, Place = .45, Grab = .3, Pull = .7, Toss = .5;
const chestOf = (feet, sc) => ({ x: feet.x, z: feet.z + 1.2 * Stand * sc });
const unit = v => { const l = len(v) || 1; return { x: v.x / l, z: v.z / l }; };
const Pants = new Color(.30, .25, .20), Boot = new Color(.15, .12, .10), Outline = new Color(.10, .08, .07), Steel = new Color(.17, .17, .18), Stock = new Color(.40, .27, .15);
// Where the tear-scenario enemy's near limbs attach and end, from its feet, x toward the ghost.
const LimbAt = { shoulder: { x: .25, z: .10 }, hand: { x: .34, z: -.20 }, hip: { x: .10, z: -.40 }, foot: { x: .11, z: -.64 } };
// The grip point on the enemy's limb, relative to Satō, for the ghost's reach.
const limbGrip = (L, limb) => { const q = limb === 'arm' ? LimbAt.hand : LimbAt.foot; return { x: L.enemy.x + q.x * L.sx, z: L.enemy.z + q.z }; };
function bar(key, a, b, w, colour, layer) {
  const dx = b.x - a.x, dz = b.z - a.z, l = Math.hypot(dx, dz) || 1, nx = -dz / l * w / 2, nz = dx / l * w / 2;
  band(key, [{ x: a.x + nx, z: a.z + nz }, { x: b.x + nx, z: b.z + nz }], [{ x: a.x - nx, z: a.z - nz }, { x: b.x - nx, z: b.z - nz }], colour, layer);
}
// A limb from `a` (sleeve or trouser colour) to `b` (hand or boot), with a dark outline.
function limbSeg(key, a, b, w, col, tipCol, layer) {
  const m = { x: lerp(a.x, b.x, .62), z: lerp(a.z, b.z, .62) };
  bar(key + ' o', a, b, w + .03, Outline, layer); bar(key + ' a', a, m, w, col, layer); bar(key + ' b', m, b, w * .9, tipCol, layer);
}
// A rifle lying or held at `c`, pointing along screen angle `deg`.
function rifle(c, deg, layer) {
  const r = deg * Mathf.Deg2Rad, dx = Math.cos(r), dz = Math.sin(r);
  const A = { x: c.x - dx * .22, z: c.z - dz * .22 }, B = { x: c.x + dx * .30, z: c.z + dz * .30 }, S0 = { x: c.x - dx * .34, z: c.z - dz * .34 };
  bar('rifle o', S0, B, .1, Outline, layer); bar('rifle s', S0, A, .08, Stock, layer); bar('rifle b', A, B, .055, Steel, layer);
}

// Layout in cells relative to Satō, and the timeline, from the params.
function plan(p) {
  const d = Dirs[p.dir], straightNS = p.dir === 'north' || p.dir === 'south';
  const side90 = d.x > 0 ? { x: -d.z, z: d.x } : { x: d.z, z: -d.x };   // the perpendicular on Satō's north side
  const perp = straightNS ? { x: 1, z: 0 } : (p.dir === 'east' || p.dir === 'west') ? { x: 0, z: 1 } : side90;
  const sato = { x: 0, z: 0 }, spawn = add(sato, perp, straightNS ? 1.35 : 1.1);
  const S = p.summon, tw = S + Pause, walkT = (a, b) => len(sub(b, a)) / p.speed;
  if (p.scenario === 'tear') {
    const enemy = add(spawn, d, p.distance), stop = add(enemy, d, straightNS ? -.6 : -.85);
    const tG = tw + walkT(spawn, stop), tP = tG + Grab, tR = tP + Pull, tT = tR + Toss, td = tT + .7;
    // The limb on the enemy's side facing the ghost, and where the tossed limb lands.
    const sx = Math.abs(d.x) > .2 ? -Math.sign(d.x) : 1;
    const land = add(add(stop, d, -.7), { x: -d.z, z: d.x }, straightNS ? .9 : -.6);
    return { d, perp, sato, spawn, enemy, stop, sx, land, tw, tG, tP, tR, tT, td, end: td + p.dissolve + 1.0 };
  }
  if (p.scenario === 'fight') {
    const enemy = add(spawn, d, p.distance), stop = add(enemy, d, straightNS ? -.6 : -Reach);
    const ts1 = tw + walkT(spawn, stop), ts2 = ts1 + p.swipe, th = ts2 + p.swipe, td = th + .4;
    return { d, perp, sato, spawn, enemy, stop, tw, ts1, ts2, td, hits: [ts1 + .59 * p.swipe, ts2 + .59 * p.swipe], end: td + p.dissolve + .8 };
  }
  const anchor = add(spawn, d, -1.0), toA = sub(anchor, spawn), dA = { x: toA.x / len(toA), z: toA.z / len(toA) };
  const pickStop = add(anchor, dA, -.42), drop = add(spawn, d, p.distance);
  const toD = sub(drop, pickStop), dD = { x: toD.x / len(toD), z: toD.z / len(toD) }, dropStop = add(drop, dD, -.42);
  const tp = tw + walkT(spawn, pickStop), tw2 = tp + Pick, tpl = tw2 + walkT(pickStop, dropStop), td = tpl + Place;
  return { d, perp, sato, spawn, anchor, pickStop, drop, dropStop, dA, dD, tw, tp, tw2, tpl, td, end: td + p.dissolve + .8 };
}

// Walking from a to b starting at t0: position, steps taken, and how much it is walking.
function walking(t, t0, a, b, speed) {
  const dist = len(sub(b, a)), T = dist / speed, u = clamp((t - t0) / T), e = lerp(u, smooth(u), .35);
  const walk = (t > t0 && t < t0 + T) ? Math.min(1, (t - t0) / .12, (t0 + T - t) / .12) : 0;
  return { pos: { x: lerp(a.x, b.x, e), z: lerp(a.z, b.z, e) }, gait: e * dist / .8, walk, done: t >= t0 + T };
}

// The ghost's feet, facing and pose at time t.
function ghostState(t, p, L) {
  if (p.scenario === 'tear') {
    const face = facingAt(L.d);
    if (t < L.tw) return { feet: L.spawn, face, opts: {} };
    if (t < L.tG) { const w = walking(t, L.tw, L.spawn, L.stop, p.speed); return { feet: w.pos, face: facingOf(L.d), opts: { gait: w.gait, walk: w.walk } }; }
    const gait = len(sub(L.stop, L.spawn)) / .8, grip = limbGrip(L, p.limb);
    const back = t < L.tP ? 0 : t < L.tR ? .16 * smooth((t - L.tP) / Pull) : .16 * (1 - smooth(clamp((t - L.tR) / (Toss * .6))));
    const feet = add(L.stop, L.d, -back);
    // Arm: reach along the line to the hand. Leg: bend down to the foot (the pick-up pose).
    if (t < L.tR) return { feet, face, opts: p.limb === 'leg' ? { gait, reach: smooth(clamp((t - L.tG) / Grab)) } : { gait, aimScreen: unit(sub(grip, chestOf(feet, p.scale))) }, gripping: true };
    // Toss: the arm swings up and back over the shoulder, then drops.
    if (t < L.tT) { const u = (t - L.tR) / Toss; return { feet, face, opts: { gait, aimScreen: unit({ x: L.d.x * (1 - u * 1.6) + (Math.abs(L.d.x) < .2 ? .3 : 0), z: .4 + u * .9 }) }, tossing: u }; }
    return { feet: L.stop, face, opts: { gait } };
  }
  if (p.scenario === 'fight') {
    const face = facingOf(L.d);
    if (t < L.tw) return { feet: L.spawn, face, opts: {} };
    if (t < L.ts1) { const w = walking(t, L.tw, L.spawn, L.stop, p.speed); return { feet: w.pos, face, opts: { gait: w.gait, walk: w.walk } }; }
    const gait = len(sub(L.stop, L.spawn)) / .8;
    // Each swipe lunges .25 cells at the enemy on the strike and steps back on the recover.
    const lunge = w => w < .5 ? 0 : w < .68 ? smooth((w - .5) / .18) : 1 - smooth((w - .68) / .32);
    if (t < L.ts2) { const w = (t - L.ts1) / p.swipe; return { feet: add(L.stop, L.d, .25 * lunge(w)), face, opts: { gait, swipe: w, side: 1 } }; }
    if (t < L.ts2 + p.swipe) { const w = (t - L.ts2) / p.swipe; return { feet: add(L.stop, L.d, .25 * lunge(w)), face, opts: { gait, swipe: w, side: -1 } }; }
    return { feet: L.stop, face, opts: { gait } };
  }
  const f1 = facingOf(L.dA), f2 = facingOf(L.dD);
  if (t < L.tw) return { feet: L.spawn, face: f1, opts: {} };
  if (t < L.tp) { const w = walking(t, L.tw, L.spawn, L.pickStop, p.speed); return { feet: w.pos, face: f1, opts: { gait: w.gait, walk: w.walk } }; }
  if (t < L.tw2) return { feet: L.pickStop, face: f1, opts: { reach: bump((t - L.tp) / Pick) } };
  if (t < L.tpl) { const w = walking(t, L.tw2, L.pickStop, L.dropStop, p.speed); return { feet: w.pos, face: f2, opts: { gait: w.gait, walk: w.walk } }; }
  return { feet: L.dropStop, face: f2, opts: { reach: bump((t - L.tpl) / Place) } };
}

export default {
  kit: 'Satō (Ajin)',
  label: 'Black Ghost (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'fight', options: ['fight', 'tear', 'relay'], group: 'Scenario' },
    limb: { label: 'Tear (limb)', value: 'arm', options: ['arm', 'leg'], group: 'Scenario' },
    dir: { label: 'Direction', value: 'east', options: ['east', 'west', 'south', 'north', 'northeast', 'northwest', 'southeast', 'southwest'], group: 'Scenario' },
    distance: P('Enemy / drop distance (cells)', 2.6, 1.2, 6, .1, 'Scenario'),
    summon: P('Summon', 1.2, .5, 3, .05, 'Timing (s)'),
    swipe: P('Swipe', .5, .25, 1.2, .05, 'Timing (s)'),
    dissolve: P('Dissolve', 1.0, .4, 2.5, .05, 'Timing (s)'),
    speed: P('Walk speed (cells/s)', 3.2, 1, 8, .1, 'Timing (s)'),
    scale: P('Ghost scale', 1.0, .7, 1.4, .02, 'Look'),
    flakes: P('Flakes', 1.0, 0, 2.5, .05, 'Look'),
  },

  duration(p) { return plan(p).end; },
  phases(p) {
    const L = plan(p);
    if (p.scenario === 'tear') return [{ name: 'Summon', t: 0 }, { name: 'Walk', t: L.tw }, { name: 'Grab', t: L.tG }, { name: 'Pull', t: L.tP }, { name: 'Rip', t: L.tR }, { name: 'Dissolve', t: L.td }];
    if (p.scenario === 'fight') return [{ name: 'Summon', t: 0 }, { name: 'Walk', t: L.tw }, { name: 'Swipe', t: L.ts1 }, { name: 'Swipe 2', t: L.ts2 }, { name: 'Dissolve', t: L.td }];
    return [{ name: 'Summon', t: 0 }, { name: 'To anchor', t: L.tw }, { name: 'Pick up', t: L.tp }, { name: 'Carry', t: L.tw2 }, { name: 'Set down', t: L.tpl }, { name: 'Dissolve', t: L.td }];
  },
  events(p) {
    const L = plan(p);
    return p.scenario === 'fight' ? L.hits.map(t => ({ t, type: 'shake', value: .05 })) : p.scenario === 'tear' ? [{ t: L.tR, type: 'shake', value: .07 }] : [];
  },

  draw(t, p, { origin, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const L = plan(p), O = v => ({ x: origin.x + v.x, z: origin.z + v.z });
    const sc = p.scale, top = Top * sc;
    const G = ghostState(t, p, L), feet = O(G.feet);
    const hi = top * smooth(clamp((t - .25) / Math.max(.1, p.summon - .25)));
    const lo = top * smooth(clamp((t - L.td) / p.dissolve));
    const actors = [];

    // Satō, and the ooze pouring out of him while the ghost forms.
    const sato = O(L.sato);
    actors.push({ z: sato.z, fn: () => standIn(sato, Shirt, sun, strength, { cap: Cap }) });
    let ghostHand = null;   // set by the ghost actor: the gripping hand this frame (tear)
    // Enemy (fight, tear) or the anchor (relay).
    if (p.scenario === 'tear') {
      const e0 = O(L.enemy), arm = p.limb === 'arm', sx = L.sx;
      const pullK = t < L.tP ? 0 : smooth(clamp((t - L.tP) / Pull)), gripped = t >= L.tG && t < L.tR, ripped = t >= L.tR;
      const shake = gripped ? Math.sin(t * 70) * .018 * (.4 + pullK) : 0;
      const towards = gripped ? -.10 * pullK : 0;                                     // pulled toward the ghost
      const stagger = ripped && arm ? .22 * smooth(clamp((t - L.tR) / .35)) : 0;     // knocked back by the rip
      const e = add(e0, L.d, towards + stagger + shake);
      const downed = ripped && !arm && t > L.tR + .12;
      const a0 = arm ? LimbAt.shoulder : LimbAt.hip, n0rel = arm ? LimbAt.hand : LimbAt.foot;
      const at = q => ({ x: e.x + q.x * sx, z: e.z + q.z });
      actors.push({
        z: e.z, fn: () => {
          if (downed) { standIn(e, Enemy, sun, strength, { downed: true }); draw(disc, e.x + .48, pawnLayer + .002, e.z - .16, .05, .05, 0, Blood); return; }   // lying head west, so the stump is east
          // Legs behind the body, arms over it, the rifle in the near hand until that arm goes.
          [-1, 1].forEach(s => {
            if (s === 1 && !arm && (gripped || ripped)) return;
            limbSeg(`tear leg ${s}`, { x: e.x + s * sx * LimbAt.hip.x, z: e.z + LimbAt.hip.z }, { x: e.x + s * sx * LimbAt.foot.x, z: e.z + LimbAt.foot.z }, .1, Pants, Boot, pawnLayer - .002);
          });
          standIn(e, Enemy, sun, strength, {});
          [-1, 1].forEach(s => {
            if (s === 1 && arm && (gripped || ripped)) return;   // the near arm is drawn by the limb actor
            limbSeg(`tear arm ${s}`, { x: e.x + s * sx * LimbAt.shoulder.x, z: e.z + LimbAt.shoulder.z }, { x: e.x + s * sx * LimbAt.hand.x, z: e.z + LimbAt.hand.z }, .085, Enemy, Skin, pawnLayer + .001);
          });
          if (ripped && arm) draw(disc, at(a0).x, pawnLayer + .002, at(a0).z, .05, .05, 0, Blood);
          if (!(arm && t >= L.tG)) rifle(at({ x: .36, z: -.18 }), sx > 0 ? -10 : 190, pawnLayer + .003);   // grabbed by the arm: the rifle drops
        },
      });
      // The limb: stretched toward the ghost's hand while it pulls, held, thrown, then lying.
      actors.push({
        z: -1e9, fn: () => {
          if (t < L.tG) return;
          const a = at(a0), n0 = at(n0rel), L0 = len(sub(n0, a));
          const col = arm ? Enemy : Pants, tip = arm ? Skin : Boot, w = arm ? .085 : .1;
          if (!ripped) {
            const h = ghostHand ?? n0, k = .5 + .5 * pullK;
            limbSeg('tear limb', a, { x: lerp(n0.x, h.x, k), z: lerp(n0.z, h.z, k) }, w, col, tip, Y + .01);
            return;
          }
          const tl = t - L.tR, launch = Toss * .35;
          if (tl < launch && ghostHand) { limbSeg('tear limb', add(ghostHand, sub(a, n0)), ghostHand, w, col, tip, Y + .01); return; }
          const land = O(L.land), from = ghostHand ?? n0, u = clamp((tl - launch) / (Toss * .65 + .15));
          const c = { x: lerp(from.x, land.x, u), z: lerp(from.z, land.z, u) + .7 * Math.sin(u * Math.PI) * (1 - u * .2) };
          const rd = ((1 - u) * 540 + 20) * Mathf.Deg2Rad, half = { x: Math.cos(rd) * L0 / 2, z: Math.sin(rd) * L0 / 2 * .6 };
          if (u >= 1) draw(disc, land.x, Floor + .01, land.z - .02, .16, .08, 0, Blood.withAlpha(.8));
          limbSeg('tear limb', sub(c, half), add(c, half), w, col, tip, u < 1 ? Y + .01 : pawnLayer - .004);
        },
      });
      // Blood: a burst at the rip, the stump spurting for 1 s, a pool that grows (stays).
      if (ripped) {
        const st = at(a0), age = t - L.tR;
        for (let i = 0; i < 16; i++) {
          const life = .35 + rand(i + 400) * .3, u = age / life;
          if (u > 1) continue;
          const th = Math.atan2(-L.d.z, -L.d.x) + (rand(i + 410) - .5) * 1.8, r = u * (.25 + rand(i + 420) * .5);
          draw(disc, st.x + Math.cos(th) * r, Y + .05 + i * .0002, st.z + Math.sin(th) * r * .7 + .35 * Math.sin(u * Math.PI), .035, .025, 0, Blood.withAlpha(1 - u * .4));
        }
        for (let k = 0; k < 8; k++) {
          const u = (age - .1 - k * .13) / .3;
          if (u < 0 || u > 1) continue;
          draw(disc, st.x + sx * u * .18, Y + .045, st.z + .12 * Math.sin(u * Math.PI) - u * .1, .025, .02, 0, Blood.withAlpha(1 - u));
        }
        const g = smooth(clamp(age / 1.5));
        draw(disc, e.x + sx * .15, Floor + .006, e.z - .35, .3 * g + .05, .15 * g + .03, 0, Blood.withAlpha(.8));
      }
      if (arm && t >= L.tG) {   // grabbed by the arm: the rifle falls and stays
        {
          const u = clamp((t - L.tG) / .35), from = add(e0, { x: sx * .36, z: -.18 }), to = add(e0, { x: sx * .6, z: -.55 });
          actors.push({ z: to.z, fn: () => rifle({ x: lerp(from.x, to.x, u), z: lerp(from.z, to.z, u) + .15 * Math.sin(u * Math.PI) }, lerp(sx > 0 ? -10 : 190, sx > 0 ? 35 : 145, u), u < 1 ? pawnLayer + .003 : pawnLayer - .004) });
        }
      }
    } else if (p.scenario === 'fight') {
      const e0 = O(L.enemy), [h1, h2] = L.hits;
      const flinch = .10 * bump((t - h1) / .25) + .06 * bump((t - h2) / .2), downed = t > h2 + .12;
      const e = add(e0, L.d, flinch);
      actors.push({ z: e.z, fn: () => standIn(e, Enemy, sun, strength, { downed, turn: 0 }) });
      // Blood on the floor: two splats at the hits and a pool once down (stays).
      [h1, h2].forEach((h, j) => {
        if (t < h) return;
        const g = clamp((t - h) / .3);
        for (let i = 0; i < 5; i++) {
          const a = rand(j * 20 + i) * TAU, r = (.12 + rand(j * 20 + i + 7) * .3) * g;
          draw(disc, e0.x + L.d.x * .15 + Math.cos(a) * r, Floor + .01 + i * .0003, e0.z - .3 + Math.sin(a) * r * .7, .05 + rand(i + j) * .04, .035, 0, Blood.withAlpha(.85));
        }
      });
      if (downed) {
        const g = smooth(clamp((t - h2 - .12) / 1.2));
        draw(disc, e0.x + .02, Floor + .005, e0.z - .22, .30 * g, .16 * g, 0, Blood.withAlpha(.8));
      }
      // Claw streaks and blood spray at each hit.
      [h1, h2].forEach((h, j) => {
        const age = t - h;
        if (age < -.05 || age > .6) return;
        const side = j === 0 ? 1 : -1;
        const fc = facingOf(L.d), v = fc === 'east' ? { x: .75, z: -.66 } : fc === 'west' ? { x: -.75, z: -.66 } : { x: -.75 * side, z: -.66 };
        const n = { x: -v.z, z: v.x }, c = { x: e0.x + L.d.x * .05, z: e0.z - .1 }, draw1 = clamp((age + .05) / .1), fade = 1 - smooth(clamp((age - .15) / .45));
        for (let i = 0; i < 3; i++) {
          const off = (i - 1) * .075, a = add(add(c, n, off), v, -.30), b = add(a, v, .6 * draw1);
          const pts = [a, add(a, v, .3 * draw1), b];
          trail(`ghost streak ${j} ${i}`, pts, .045, Slash.withAlpha(.95 * fade), Y + .06 + i * .0004);
        }
        for (let i = 0; i < 9; i++) {   // blood flecks thrown away from the ghost
          const life = .35 + rand(i + j * 30) * .2, u = age / life;
          if (u < 0 || u > 1) continue;
          const th = Math.atan2(L.d.z, L.d.x) + (rand(i + j * 30 + 3) - .5) * 1.6, r = u * (.25 + rand(i + 5) * .35), hgt = .5 * Math.sin(u * Math.PI) * .6;
          draw(disc, c.x + Math.cos(th) * r, Y + .05 + i * .0002, c.z + .1 + Math.sin(th) * r * .7 + hgt * Lift, .03, .022, 0, Blood.withAlpha(1 - u * .5));
        }
      });
    } else {
      const a0 = O(L.anchor), drop = O(L.drop), heldFrom = L.tp + Pick * .5, heldTo = L.tpl + Place * .5;
      draw(disc, a0.x - .05, Floor + .01, a0.z - .03, .16, .09, 10, Blood.withAlpha(.8));   // blood where it was cut (stays)
      if (t < heldFrom) actors.push({ z: a0.z, fn: () => hand(a0, 20, pawnLayer) });
      if (t >= heldTo) {
        actors.push({ z: drop.z, fn: () => hand(drop, -15, pawnLayer) });
        const g = smooth(clamp((t - heldTo) / .4));
        draw(ring, drop.x, Floor + .02, drop.z, .38 * g, .38 * g, 0, Slash.withAlpha(.45 * g));
      }
      G.held = t >= heldFrom && t < heldTo;
    }

    // The ghost and its flakes.
    actors.push({
      z: feet.z, fn: () => {
        const r = ghost('ghost', feet, G.face, G.opts, { lo, hi, scale: sc, sun, strength });
        if (!r) return;
        const mirror = G.face === 'west' ? -1 : 1, walkAmt = G.opts.walk ?? 0;
        flakes('ghost flakes', feet, r.segs, t, { amount: p.flakes * (1 + walkAmt * .6), lo, hi, scale: sc, mirror });
        if (G.held) hand(r.wrists[1], mirror > 0 ? -60 : 240, pawnLayer);
        if (G.gripping || G.tossing !== undefined) {
          const east = G.face === 'east' || G.face === 'west';
          ghostHand = east ? r.wrists[1] : { x: (r.wrists[0].x + r.wrists[1].x) / 2, z: (r.wrists[0].z + r.wrists[1].z) / 2 };
        }
      },
    });
    if (hi < top && hi > 0) edgeFlakes('ghost rise', feet, hi, .8 * sc, t, { amount: p.flakes * 1.1, rise: .45 });
    if (lo > 0 && lo < top) edgeFlakes('ghost fall', feet, lo, .85 * sc, t, { amount: p.flakes * 1.5, rise: .7 });

    actors.sort((a, b) => b.z - a.z).forEach(a => a.fn());
  },
};
