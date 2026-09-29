// Satō (Ajin) — Black Ghost v2: the same ability as "Black Ghost (sketch)" with a new figure
// (proposed 2026-09-29, not agreed; the old sketch stays for comparison).
//
// Mechanic (agreed 2026-09-27, numbers are XML placeholders): 10 charge, summoned next to Satō or
// at any of his anchors within 30 cells; it lasts by the piece it came from (body or leg 45 s, arm
// 35 s, hand 20 s, finger 10 s). A real pawn whose look is drawn in code: a fast claw fighter that
// takes 50 % damage. One at a time, cooldown 120 s. Orders: Tear (once per summon; its picture is
// being redone next and is not in this file yet) and Relay (still undecided). The sketch
// compresses its life into a few seconds.
//
// Look (sources in lib/ajin-ghost-v2.js): a reptile head with a working mouth, a forked tongue and
// saliva; a black skeleton in bandages with loose ends; a hunched predator stance.
//
// Beats: summon — black matter pours out of Satō's shoulders to the spot and the ghost builds up
// from the feet (1.2 s). Idle — the head sways side to side over 2.6 s, the tongue flicks out every
// 2.6 s for 0.34 s (the jaw parts a quarter), the claws flex, the loose ends ripple. Walk — a long
// loping stride, arms low, loose ends trailing behind. Swipe — the head lunges with the jaw open,
// then the claw comes down. Time running out — over the last 25 % of its life the limbs thin by up
// to 15 %, the flakes triple, bigger chunks cling to the edges and peel off, and the loose ends grow
// from 3 to 5 and lengthen by up to 60 % (the wraps unravel). Dissolve — it breaks into flakes
// from the feet up (1.0 s). The downed enemy, the blood and the dropped anchor stay.
//
// Drawing: per facing, as the old ghost: south front (mouth, tongue), north back (the hood's point
// up, a spine groove), east its own profile (wedge head, hinged jaw), west = east mirrored;
// diagonals use the side view, as the game picks it for a walking pawn. Stand-ins at real pawn size.
import { Meshes } from '../js/engine.js';
import { P } from './lib/six-paths-impact.js';
import { flakes, edgeFlakes, ooze, standIn, Floor, pawnLayer, Shirt, Cap, Enemy, Blood, Slash, hand, draw, disc, trail, rand, smooth, clamp, lerp, TAU, Y } from './lib/ajin.js';
import { ghost2, chunks, flick, Top2 } from './lib/ajin-ghost-v2.js';

const ring = Meshes.band(.93, 1, 48, 'ajin v2 anchor ring');
const Dirs = {
  east: { x: 1, z: 0 }, west: { x: -1, z: 0 }, north: { x: 0, z: 1 }, south: { x: 0, z: -1 },
  northeast: { x: 0.7071, z: 0.7071 }, northwest: { x: -0.7071, z: 0.7071 }, southeast: { x: 0.7071, z: -0.7071 }, southwest: { x: -0.7071, z: -0.7071 },
};
// The game's rule (Pawn_RotationTracker.FaceAdjacentCell): a step with any sideways part faces east
// or west, so every diagonal uses the side view; only straight north or south steps use the back or
// front.
const facingOf = v => Math.abs(v.x) > .2 ? (v.x > 0 ? 'east' : 'west') : (v.z >= 0 ? 'north' : 'south');
const add = (a, b, k = 1) => ({ x: a.x + b.x * k, z: a.z + b.z * k });
const sub = (a, b) => ({ x: a.x - b.x, z: a.z - b.z });
const len = v => Math.hypot(v.x, v.z);
const unit = v => { const l = len(v) || 1; return { x: v.x / l, z: v.z / l }; };
const bump = (x) => (x > 0 && x < 1) ? Math.sin(x * Math.PI) : 0;
const Reach = 1.0, Pause = .35, Pick = .55, Place = .45;   // melee stop (cells), idle after forming, relay pick-up and set-down (s)
const FrayFrom = .75;                                       // share of its life after which it frays

// Layout in cells relative to Satō, and the timeline, from the params.
function plan(p) {
  const d = Dirs[p.dir], straightNS = p.dir === 'north' || p.dir === 'south';
  const side90 = d.x > 0 ? { x: -d.z, z: d.x } : { x: d.z, z: -d.x };   // the perpendicular on Satō's north side
  const perp = straightNS ? { x: 1, z: 0 } : (p.dir === 'east' || p.dir === 'west') ? { x: 0, z: 1 } : side90;
  const sato = { x: 0, z: 0 }, spawn = add(sato, perp, straightNS ? 1.35 : 1.1);
  const S = p.summon, tw = S + Pause, walkT = (a, b) => len(sub(b, a)) / p.speed;
  if (p.scenario === 'idle') {
    const td = S + p.life;
    return { d, perp, sato, spawn, tw, td, end: td + p.dissolve + .8 };
  }
  if (p.scenario === 'fight') {
    const enemy = add(spawn, d, p.distance), stop = add(enemy, d, straightNS ? -.6 : -Reach);
    const ts1 = tw + walkT(spawn, stop), ts2 = ts1 + p.swipe, th = ts2 + p.swipe, td = th + .4;
    return { d, perp, sato, spawn, enemy, stop, tw, ts1, ts2, td, hits: [ts1 + .59 * p.swipe, ts2 + .59 * p.swipe], end: td + p.dissolve + .8 };
  }
  const anchor = add(spawn, d, -1.0), dA = unit(sub(anchor, spawn));
  const pickStop = add(anchor, dA, -.42), drop = add(spawn, d, p.distance);
  const dD = unit(sub(drop, pickStop)), dropStop = add(drop, dD, -.42);
  const tp = tw + walkT(spawn, pickStop), tw2 = tp + Pick, tpl = tw2 + walkT(pickStop, dropStop), td = tpl + Place;
  return { d, perp, sato, spawn, anchor, pickStop, drop, dropStop, dA, dD, tw, tp, tw2, tpl, td, end: td + p.dissolve + .8 };
}

// Walking from a to b starting at t0: position, steps taken, how much it is walking, and the
// walking direction times that amount (the loose ends trail against it).
function walking(t, t0, a, b, speed) {
  const dist = len(sub(b, a)), T = dist / speed, u = clamp((t - t0) / T), e = lerp(u, smooth(u), .35);
  const walk = (t > t0 && t < t0 + T) ? Math.min(1, (t - t0) / .12, (t0 + T - t) / .12) : 0, dir = unit(sub(b, a));
  return { pos: { x: lerp(a.x, b.x, e), z: lerp(a.z, b.z, e) }, gait: e * dist / .8, walk, travel: { x: dir.x * walk, z: dir.z * walk } };
}

// The ghost's feet, facing, pose and walking direction at time t.
function ghostState(t, p, L) {
  if (p.scenario === 'idle') return { feet: L.spawn, face: facingOf(L.d), opts: {} };
  if (p.scenario === 'fight') {
    const face = facingOf(L.d);
    if (t < L.tw) return { feet: L.spawn, face, opts: {} };
    if (t < L.ts1) { const w = walking(t, L.tw, L.spawn, L.stop, p.speed); return { feet: w.pos, face, opts: { gait: w.gait, walk: w.walk }, travel: w.travel }; }
    const gait = len(sub(L.stop, L.spawn)) / .8;
    // Each swipe lunges .25 cells at the enemy on the strike and steps back on the recover.
    const lunge = w => w < .5 ? 0 : w < .68 ? smooth((w - .5) / .18) : 1 - smooth((w - .68) / .32);
    if (t < L.ts2) { const w = (t - L.ts1) / p.swipe; return { feet: add(L.stop, L.d, .25 * lunge(w)), face, opts: { gait, swipe: w, side: 1 } }; }
    if (t < L.ts2 + p.swipe) { const w = (t - L.ts2) / p.swipe; return { feet: add(L.stop, L.d, .25 * lunge(w)), face, opts: { gait, swipe: w, side: -1 } }; }
    return { feet: L.stop, face, opts: { gait } };
  }
  const f1 = facingOf(L.dA), f2 = facingOf(L.dD);
  if (t < L.tw) return { feet: L.spawn, face: f1, opts: {} };
  if (t < L.tp) { const w = walking(t, L.tw, L.spawn, L.pickStop, p.speed); return { feet: w.pos, face: f1, opts: { gait: w.gait, walk: w.walk }, travel: w.travel }; }
  if (t < L.tw2) return { feet: L.pickStop, face: f1, opts: { reach: bump((t - L.tp) / Pick) } };
  if (t < L.tpl) { const w = walking(t, L.tw2, L.pickStop, L.dropStop, p.speed); return { feet: w.pos, face: f2, opts: { gait: w.gait, walk: w.walk }, travel: w.travel }; }
  return { feet: L.dropStop, face: f2, opts: { reach: bump((t - L.tpl) / Place) } };
}

export default {
  kit: 'Satō (Ajin)',
  label: 'Black Ghost v2 (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'idle', options: ['idle', 'fight', 'relay'], group: 'Scenario' },
    dir: { label: 'Direction', value: 'east', options: ['east', 'west', 'south', 'north', 'northeast', 'northwest', 'southeast', 'southwest'], group: 'Scenario' },
    distance: P('Enemy / drop distance (cells)', 2.6, 1.2, 6, .1, 'Scenario'),
    jawOpen: { label: 'Hold jaw open (to inspect)', value: false, group: 'Scenario' },
    life: P('Life shown (idle)', 6, 2, 15, .5, 'Timing (s)'),
    summon: P('Summon', 1.2, .5, 3, .05, 'Timing (s)'),
    swipe: P('Swipe', .5, .25, 1.2, .05, 'Timing (s)'),
    dissolve: P('Dissolve', 1.0, .4, 2.5, .05, 'Timing (s)'),
    speed: P('Walk speed (cells/s)', 3.2, 1, 8, .1, 'Timing (s)'),
    scale: P('Ghost scale', 1.0, .7, 1.4, .02, 'Look'),
    flakes: P('Flakes', 1.0, 0, 2.5, .05, 'Look'),
  },

  duration(p) { return plan(p).end; },
  phases(p) {
    const L = plan(p), fray = { name: 'Fraying', t: p.summon + FrayFrom * (L.td - p.summon) };
    if (p.scenario === 'idle') return [{ name: 'Summon', t: 0 }, { name: 'Idle', t: p.summon }, fray, { name: 'Dissolve', t: L.td }];
    if (p.scenario === 'fight') return [{ name: 'Summon', t: 0 }, { name: 'Walk', t: L.tw }, { name: 'Swipe', t: L.ts1 }, { name: 'Swipe 2', t: L.ts2 }, fray, { name: 'Dissolve', t: L.td }].sort((a, b) => a.t - b.t);
    return [{ name: 'Summon', t: 0 }, { name: 'To anchor', t: L.tw }, { name: 'Pick up', t: L.tp }, { name: 'Carry', t: L.tw2 }, { name: 'Set down', t: L.tpl }, fray, { name: 'Dissolve', t: L.td }].sort((a, b) => a.t - b.t);
  },
  events(p) {
    const L = plan(p);
    return p.scenario === 'fight' ? L.hits.map(t => ({ t, type: 'shake', value: .05 })) : [];
  },

  draw(t, p, { origin, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const L = plan(p), O = v => ({ x: origin.x + v.x, z: origin.z + v.z });
    const sc = p.scale, top = Top2 * sc;
    const G = ghostState(t, p, L), feet = O(G.feet);
    const hi = top * smooth(clamp((t - .25) / Math.max(.1, p.summon - .25)));
    const lo = top * smooth(clamp((t - L.td) / p.dissolve));
    // Its time running out: the share of its life used, and how far into the last 25 % it is.
    const used = clamp((t - p.summon) / Math.max(.1, L.td - p.summon)), fray = smooth(clamp((used - FrayFrom) / (1 - FrayFrom)));
    // Tongue flicks while it stands still and does nothing else; the jaw parts a quarter for them.
    if (p.jawOpen) Object.assign(G.opts, { jaw: 1, tongue: 1 });
    else if (!G.opts.walk && G.opts.swipe === undefined) { const f = flick(t); Object.assign(G.opts, { jaw: .25 * f, tongue: f }); }
    const actors = [];

    // Satō, and the black matter pouring out of his shoulders to where the ghost forms.
    const sato = O(L.sato);
    actors.push({ z: sato.z, fn: () => standIn(sato, Shirt, sun, strength, { cap: Cap }) });
    const pour = smooth(clamp(t / (p.summon * .6))), pourA = 1 - smooth(clamp((t - p.summon * .7) / (p.summon * .5)));
    if (pour > 0 && pourA > 0) ooze('ghost ooze', { x: sato.x, z: sato.z + .12 }, { x: O(L.spawn).x, z: O(L.spawn).z + .05 }, t, { n: 6, spread: .45, k: pour, alpha: pourA });

    if (p.scenario === 'fight') {
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
      // Claw streaks (six fingers: six thin lines) and blood thrown away from the ghost at each hit.
      [h1, h2].forEach((h, j) => {
        const age = t - h;
        if (age < -.05 || age > .6) return;
        const side = j === 0 ? 1 : -1, fc = facingOf(L.d);
        const v = fc === 'east' ? { x: .75, z: -.66 } : fc === 'west' ? { x: -.75, z: -.66 } : { x: -.75 * side, z: -.66 };
        const n = { x: -v.z, z: v.x }, c = { x: e0.x + L.d.x * .05, z: e0.z - .1 }, draw1 = clamp((age + .05) / .1), fade = 1 - smooth(clamp((age - .15) / .45));
        for (let i = 0; i < 6; i++) {
          const a = add(add(c, n, (i - 2.5) * .05), v, -.30);
          trail(`ghost streak ${j} ${i}`, [a, add(a, v, .3 * draw1), add(a, v, .6 * draw1)], .03, Slash.withAlpha(.95 * fade), Y + .06 + i * .0004);
        }
        for (let i = 0; i < 9; i++) {
          const life = .35 + rand(i + j * 30) * .2, u = age / life;
          if (u < 0 || u > 1) continue;
          const th = Math.atan2(L.d.z, L.d.x) + (rand(i + j * 30 + 3) - .5) * 1.6, r = u * (.25 + rand(i + 5) * .35), hgt = .5 * Math.sin(u * Math.PI) * .6;
          draw(disc, c.x + Math.cos(th) * r, Y + .05 + i * .0002, c.z + .1 + Math.sin(th) * r * .7 + hgt * .6, .03, .022, 0, Blood.withAlpha(1 - u * .5));
        }
      });
    } else if (p.scenario === 'relay') {
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

    // The ghost, its flakes (up to 3x as it frays) and the bigger chunks breaking off.
    actors.push({
      z: feet.z, fn: () => {
        const r = ghost2('ghost', feet, G.face, G.opts, { lo, hi, scale: sc, sun, strength, t, fray, travel: G.travel ?? { x: 0, z: 0 } });
        if (!r) return;
        const mirror = G.face === 'west' ? -1 : 1, walkAmt = G.opts.walk ?? 0;
        flakes('ghost flakes', feet, r.segs, t, { amount: p.flakes * (1 + walkAmt * .6) * (1 + 2 * fray), lo, hi, scale: sc, mirror });
        if (fray > 0) chunks('ghost chunks', feet, r.segs, t, fray * p.flakes, { lo, hi, scale: sc, mirror, layer: Y + .025 });
        if (G.held) hand(r.wrists[1], mirror > 0 ? -60 : 240, pawnLayer);
      },
    });
    if (hi < top && hi > 0) edgeFlakes('ghost rise', feet, hi, .8 * sc, t, { amount: p.flakes * 1.1, rise: .45 });
    if (lo > 0 && lo < top) edgeFlakes('ghost fall', feet, lo, .85 * sc, t, { amount: p.flakes * 1.5, rise: .7 });

    actors.sort((a, b) => b.z - a.z).forEach(a => a.fn());
  },
};
