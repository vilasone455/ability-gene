// Satō (Ajin) — Black Ghost v2: the same ability as "Black Ghost (sketch)" with a new figure
// (proposed 2026-09-29, not agreed; the old sketch stays for comparison).
//
// Mechanic (agreed 2026-09-27, numbers are XML placeholders): 10 charge, summoned next to Satō or
// at any of his anchors within 30 cells; it lasts by the piece it came from (body or leg 45 s, arm
// 35 s, hand 20 s, finger 10 s). A real pawn whose look is drawn in code: a fast claw fighter that
// takes 50 % damage. One at a time, cooldown 120 s. Orders: Tear and Relay (Relay still undecided).
// The sketch compresses its life into a few seconds.
//
// Tear (agreed 2026-09-26, picture redone 2026-09-29): the ghost grabs an adjacent enemy, holds it
// 1 s and rips off an arm or a leg (picked like Sever). Arm: the enemy drops its weapon and loses
// manipulation. Leg: it can barely walk; a second leg downs it. Once per summon, fleshy targets only.
// The real enemy is a pawn sprite with no arms or legs, so the ghost grabs the body and the limb is
// drawn only once it has come off: grab (0.3 s) — in the side views it steps into the target's cell
// (a draw offset; it stays in the adjacent cell in game) and both claws close on the body, the near one where
// the limb joins (shoulder height for an arm, the bottom of the body for a leg); lift (0.3 s) — the
// enemy is lifted 0.35 cells, its shadow stays on the floor and shrinks, the jaw opens; shake
// (0.4 s) — it struggles, the ghost leans back; rip — the near claw rakes down the side (six pale
// streaks), blood bursts from the edge of the body, a camera shake, and the limb (sleeve and hand,
// or trouser leg and boot) comes out from behind the body in the claw; throw (0.5 s) — the claw
// swings up and back and lets go at 0.25 s, the limb lands about a cell behind the ghost and stays;
// in the side views the ghost steps back to its own cell (0.4 s).
// The enemy is dropped (0.2 s). Arm: it lands on its feet, staggers back 0.2 cells, its rifle falls
// and stays. Leg: it lands tilted 20 degrees toward the missing side and limps 0.3 cells in 1.5 s,
// dripping blood. Not downed.
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
// Drawing: the ghost's feet sit on its cell's bottom edge like a pawn's (FeetDrop). The lift is a draw offset on the real pawn in game (Gravity and Obito already patch
// Pawn_DrawTracker.DrawPos), the tilt a draw angle; the thrown limb is drawn by the ability like
// Satō's own severed parts. Per facing, as the old ghost: south front (mouth, tongue), north back (the hood's point
// up, a spine groove), east its own profile (wedge head, hinged jaw), west = east mirrored;
// diagonals use the side view, as the game picks it for a walking pawn. Stand-ins at real pawn size.
import { Color, Mathf, Meshes } from '../js/engine.js';
import { P } from './lib/six-paths-impact.js';
import { flakes, edgeFlakes, ooze, standIn, Floor, pawnLayer, shadowLayer, Shirt, Cap, Enemy, Skin, Blood, Slash, Body, hand, draw, disc, trail, sprite, soft, limbSeg, rifle, Stand, rand, smooth, clamp, lerp, TAU, Y } from './lib/ajin.js';
import { ghost2, chunks, flick, claws2, ClawLen, Top2 } from './lib/ajin-ghost-v2.js';

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
const lerp2 = (a, b, k) => ({ x: lerp(a.x, b.x, k), z: lerp(a.z, b.z, k) });
const bump = (x) => (x > 0 && x < 1) ? Math.sin(x * Math.PI) : 0;
const Reach = 1.0, Pause = .35, Pick = .55, Place = .45;   // melee stop (cells), idle after forming, relay pick-up and set-down (s)
const FrayFrom = .75;                                       // share of its life after which it frays
// The ghost's feet are drawn at the bottom of its cell, where a pawn sprite's feet are (a pawn is
// drawn centred on its cell, feet about 0.5 below the centre). With the feet at the centre the
// ghost looked half a cell higher than a pawn on the same row.
const FeetDrop = -.45;
// Tear: timings (s), the lift (cells), the enemy's clothes for the torn piece.
const Grab = .3, Lift = .3, Hold = 1.0, Toss = .5, Release = .25, Drop = .2, LiftH = .35;
const Pants = new Color(.28, .33, .45), Boot = new Color(.18, .14, .11);   // denim, so a torn leg reads on brown ground
// Points on the enemy from its feet, x toward the torn side: in the side views the far side, in
// front of the ghost as it stands in the target's cell, so its arms wrap forward round the body;
// east when the ghost comes from north or south. Where the torn limb leaves the body, where the
// holding claw grips (the front and back views hold the west side), and the torn piece's size.
const TearAt = { arm: { x: .26, z: .12 }, leg: { x: .13, z: -.44 } }, HoldAt = { arm: { x: .12, z: .16 }, leg: { x: .20, z: .14 } }, HoldNS = { x: -.27, z: .08 };
const Piece = { arm: { len: .40, w: .10 }, leg: { len: .38, w: .12 } };
// A screen offset turned clockwise by `deg` (the way a draw angle turns a sprite).
const rotOff = (o, deg) => { const r = deg * Mathf.Deg2Rad, c = Math.cos(r), s = Math.sin(r); return { x: o.x * c + o.z * s, z: -o.x * s + o.z * c }; };

// The torn enemy at time t: how far it has moved along the ghost's direction (off), its lift, draw
// angle and sink, and whether it is held.
function tearEnemy(t, p, L) {
  const arm = p.limb === 'arm', tDrop = L.tR + .05, liftK = smooth(clamp((t - L.tL) / Lift)), fall = clamp((t - tDrop) / Drop);
  const held = t >= L.tG && t < tDrop, shake = held ? (t < L.tS ? .4 * liftK : 1) : 0;
  let lift = t < tDrop ? LiftH * liftK : LiftH * (1 - fall * fall), sink = 0, rot = shake * Math.sin(t * 23) * 6;
  let off = (held ? -.06 * smooth(clamp((t - L.tG) / Grab)) : 0) + shake * Math.sin(t * 70) * .02;   // pulled toward the ghost, struggling
  const after = t - tDrop - Drop;
  if (arm && after > 0) off += .2 * smooth(clamp(after / .35));   // staggers back
  if (!arm && t >= tDrop) {                                        // lands tilted to the missing side, then limps
    const k = smooth(fall); rot = 20 * L.sx * k; sink = .08 * k;
    if (after > 0) { const u = clamp(after / 1.5); off += .3 * u; lift += u < 1 ? .04 * Math.abs(Math.sin(after * 9)) : 0; }
  }
  return { off, lift, rot, sink, held };
}
// A point on the enemy (offset from its feet, already sided) in screen cells.
const onEnemy = (g, E, o) => { const q = rotOff(o, E.rot); return { x: g.x + q.x, z: g.z + q.z + E.lift - E.sink }; };
// Where the tearing claw is after the rip: yanked 0.45 cells back past the ghost in 0.12 s, then
// swung up and over for the throw.
function tearHand(t, L, a0, away) {
  const y = smooth(clamp((t - L.tR) / .12)), v = clamp((t - L.tR - .12) / (Toss - .12));
  return { x: a0.x + away.x * (.45 * y + .15 * v), z: a0.z + away.z * (.45 * y + .15 * v) + .7 * Math.sin(v * Math.PI) };
}

// Layout in cells relative to Satō, and the timeline, from the params.
function plan(p) {
  const d = Dirs[p.dir], straightNS = p.dir === 'north' || p.dir === 'south';
  const side90 = d.x > 0 ? { x: -d.z, z: d.x } : { x: d.z, z: -d.x };   // the perpendicular on Satō's north side
  const perp = straightNS ? { x: 1, z: 0 } : (p.dir === 'east' || p.dir === 'west') ? { x: 0, z: 1 } : side90;
  const sato = { x: 0, z: 0 }, spawn = add(sato, perp, straightNS ? 1.35 : 1.1);
  const S = p.summon, tw = S + Pause, walkT = (a, b) => len(sub(b, a)) / p.speed;
  if (p.scenario === 'tear') {
    const enemy = add(spawn, d, p.distance), stop = add(enemy, d, straightNS ? -.6 : -1.0);   // the adjacent cell
    const tG = tw + walkT(spawn, stop), tL = tG + Grab, tS = tL + Lift, tR = tG + Hold, tT = tR + Toss, td = tR + 1.9;
    const sx = Math.abs(d.x) > .2 ? Math.sign(d.x) : 1, side = { x: -d.z, z: d.x };   // the torn side (see TearAt)
    const land = add(add(stop, d, -.9), side, straightNS ? .9 : .5);
    // Side views: over the grab it steps into the target's cell (drawn 0.2 cells short of the
    // target's feet, the enemy in front of its body) and steps back out after the throw. The top
    // and bottom views already overlap at 0.6 cells.
    const inStop = straightNS ? stop : add(enemy, d, -.2);
    return { d, perp, sato, spawn, enemy, stop, inStop, sx, land, NS: straightNS, tw, tG, tL, tS, tR, tT, td, end: td + p.dissolve + .8 };
  }
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
  if (p.scenario === 'tear') {
    const face = facingOf(L.d);
    if (t < L.tw) return { feet: L.spawn, face, opts: {} };
    if (t < L.tG) { const w = walking(t, L.tw, L.spawn, L.stop, p.speed); return { feet: w.pos, face, opts: { gait: w.gait, walk: w.walk }, travel: w.travel }; }
    // Steps into the target's cell over the grab, leans back 0.12 cells while it lifts and shakes,
    // and steps back out to its own cell after the throw.
    const back = t < L.tR ? .12 * smooth(clamp((t - L.tL) / (L.tR - L.tL))) : .12 * (1 - smooth(clamp((t - L.tR) / .4)));
    const into = t < L.tT ? smooth(clamp((t - L.tG) / Grab)) : 1 - smooth(clamp((t - L.tT) / .4));
    const stepping = t < L.tT ? clamp((t - L.tG) / Grab) : clamp((t - L.tT) / .4), stepLen = len(sub(L.inStop, L.stop));
    const gait = (len(sub(L.stop, L.spawn)) + stepLen * (t < L.tT ? into : 2 - into)) / .8;
    return { feet: add(lerp2(L.stop, L.inStop, into), L.d, -back), face, opts: { gait, walk: stepLen > .1 ? bump(stepping) * .8 : 0 } };
  }
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
    scenario: { label: 'Scenario', value: 'idle', options: ['idle', 'fight', 'tear', 'relay'], group: 'Scenario' },
    limb: { label: 'Tear (limb)', value: 'arm', options: ['arm', 'leg'], group: 'Scenario' },
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
    if (p.scenario === 'tear') return [{ name: 'Summon', t: 0 }, { name: 'Walk', t: L.tw }, { name: 'Grab', t: L.tG }, { name: 'Lift', t: L.tL }, { name: 'Rip', t: L.tR }, { name: 'Throw', t: L.tR + .12 }, fray, { name: 'Dissolve', t: L.td }].sort((a, b) => a.t - b.t);
    if (p.scenario === 'fight') return [{ name: 'Summon', t: 0 }, { name: 'Walk', t: L.tw }, { name: 'Swipe', t: L.ts1 }, { name: 'Swipe 2', t: L.ts2 }, fray, { name: 'Dissolve', t: L.td }].sort((a, b) => a.t - b.t);
    return [{ name: 'Summon', t: 0 }, { name: 'To anchor', t: L.tw }, { name: 'Pick up', t: L.tp }, { name: 'Carry', t: L.tw2 }, { name: 'Set down', t: L.tpl }, fray, { name: 'Dissolve', t: L.td }].sort((a, b) => a.t - b.t);
  },
  events(p) {
    const L = plan(p);
    return p.scenario === 'fight' ? L.hits.map(t => ({ t, type: 'shake', value: .05 })) : p.scenario === 'tear' ? [{ t: L.tR, type: 'shake', value: .07 }] : [];
  },

  draw(t, p, { origin, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const L = plan(p), O = v => ({ x: origin.x + v.x, z: origin.z + v.z });
    const sc = p.scale, top = Top2 * sc;
    const G = ghostState(t, p, L), feet = O(add(G.feet, { x: 0, z: FeetDrop }));
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
    if (pour > 0 && pourA > 0) ooze('ghost ooze', { x: sato.x, z: sato.z + .12 }, { x: O(L.spawn).x, z: O(L.spawn).z + FeetDrop + .05 }, t, { n: 6, spread: .45, k: pour, alpha: pourA });

    let ghostR = null;     // the ghost's drawn wrists and elbows this frame (set by the ghost actor)
    if (p.scenario === 'tear') {
      const arm = p.limb === 'arm', sx = L.sx, e0 = O(L.enemy), E = tearEnemy(t, p, L), g = add(e0, L.d, E.off);
      const tearO = { x: TearAt[p.limb].x * sx, z: TearAt[p.limb].z }, holdO = L.NS ? HoldNS : { x: HoldAt[p.limb].x * sx, z: HoldAt[p.limb].z };
      const ER = tearEnemy(L.tR, p, L), a0 = onEnemy(add(e0, L.d, ER.off), ER, tearO);   // where the limb leaves the body at the rip
      const away = unit({ x: -L.d.x + (L.NS ? .3 : 0), z: -L.d.z * .8 + .25 }), ripped = t >= L.tR, tDrop = L.tR + .05;
      // The ghost's claws: both close on the body over the grab; the holding claw lets go at the drop,
      // the tearing claw stays on the limb spot, then yanks and throws.
      const m = G.face === 'west' ? -1 : 1, toUH = q => ({ u: (q.x - feet.x) / (sc * m), h: (q.z - feet.z) / (Stand * sc) });
      if (t >= L.tG) {
        const kIn = smooth(clamp((t - L.tG) / Grab));
        const kh = t < tDrop ? kIn : 1 - smooth(clamp((t - tDrop) / .25));
        const kt = t < L.tT ? kIn : 1 - smooth(clamp((t - L.tT) / .3));
        const tearQ = ripped ? tearHand(t, L, a0, away) : onEnemy(g, E, tearO);
        G.opts.grip = { hold: toUH(onEnemy(g, E, holdO)), tear: toUH(tearQ), kh, kt };
        G.opts.jaw = ripped ? 1 - smooth(clamp((t - L.tR - .35) / .3)) : .45 * smooth(clamp((t - L.tL) / Lift));
        G.opts.tongue = 0;
      }
      // The enemy: its shadow stays on the floor and shrinks while it is lifted; body and head at
      // the lift and draw angle; its rifle on the near side until that arm goes.
      const rifleO = { x: .34 * sx, z: -.18 }, rifleDeg = sx > 0 ? -10 : 190;
      actors.push({
        z: g.z - .01, fn: () => {   // just nearer than the ghost, so it covers the ghost's body and arms
          const lk = E.lift / LiftH, s = 1.3, o = -.33;
          sprite({ x: g.x + sun.x * .5, z: g.z + o + .05 + sun.z * .5 }, .95 * (1 - .25 * lk), .42 * (1 - .25 * lk), Body.withAlpha(strength * 1.4 * (1 - .35 * lk)), soft, shadowLayer);
          const b = onEnemy(g, E, { x: 0, z: o + .18 * s }), h = onEnemy(g, E, { x: 0, z: o + .58 * s });
          draw(disc, b.x, pawnLayer, b.z, .22 * s, .32 * s, E.rot, Enemy);
          draw(disc, h.x, pawnLayer, h.z, .16 * s, .17 * s, E.rot, Skin);
          if (!(arm && ripped)) rifle(onEnemy(g, E, rifleO), rifleDeg - E.rot, pawnLayer + .003);
        },
      });
      if (arm && ripped) {   // the rifle falls from the torn side and stays
        const u = clamp((t - L.tR) / .35), from = onEnemy(add(e0, L.d, ER.off), ER, rifleO), to = add(e0, { x: sx * .6, z: -.55 });
        actors.push({ z: to.z, fn: () => rifle({ x: lerp(from.x, to.x, u), z: lerp(from.z, to.z, u) + .15 * Math.sin(u * Math.PI) }, lerp(rifleDeg, sx > 0 ? 35 : 145, u), u < 1 ? pawnLayer + .003 : pawnLayer - .004) });
      }
      // The torn piece: in the claw, hanging from its torn end; thrown at Release; then lying (stays).
      if (ripped) actors.push({
        z: -1e9, fn: () => {
          const P = Piece[p.limb], col = arm ? Enemy : Pants, tip = arm ? Skin : Boot, rel = L.tR + Release;
          if (t < rel) {
            const c = ghostR ? ghostR.wrists[1] : tearHand(t, L, a0, away), hang = unit({ x: sx * .35, z: -1 });
            limbSeg('tear limb', c, add(c, hang, P.len), P.w, col, tip, Y + .01);
            draw(disc, c.x, Y + .012, c.z, .05, .04, 0, Blood);
            return;
          }
          const from = tearHand(rel, L, a0, away), land = O(L.land), u = clamp((t - rel) / .4);
          const c = { x: lerp(from.x, land.x, u), z: lerp(from.z, land.z, u) + .6 * Math.sin(u * Math.PI) };
          const rd = ((1 - u) * 540 + 20) * Mathf.Deg2Rad, half = { x: Math.cos(rd) * P.len / 2, z: Math.sin(rd) * P.len / 2 * .6 };
          if (u >= 1) draw(disc, land.x, Floor + .01, land.z - .03, .17, .09, 0, Blood.withAlpha(.8));
          limbSeg('tear limb', sub(c, half), add(c, half), P.w, col, tip, u < 1 ? Y + .01 : pawnLayer - .004);
          const te = sub(c, half);
          draw(disc, te.x, u < 1 ? Y + .012 : pawnLayer - .003, te.z, .045, .035, 0, Blood);
        },
      });
      // The claws that grip are drawn again over the enemy (and over the torn piece they hold).
      if (G.opts.grip) actors.push({
        z: -2e9, fn: () => {
          if (!ghostR || lo > 0) return;
          [G.opts.grip.kh, G.opts.grip.kt].forEach((k, i) => { if (k > .5) claws2(`tear grip ${i}`, q => q, ghostR.elbows[i], ghostR.wrists[i], ClawLen * p.scale, -.6, Y + .014); });
        },
      });
      if (ripped) {
        const age = t - L.tR, st = onEnemy(g, E, tearO);
        // Six claw streaks raking down the side at the rip.
        const v = unit({ x: away.x, z: away.z - .9 }), n = { x: -v.z, z: v.x }, grow = clamp((age + .02) / .08), fade = 1 - smooth(clamp((age - .1) / .4));
        if (fade > 0) for (let i = 0; i < 6; i++) {
          const a = add(add(a0, n, (i - 2.5) * .045), v, -.2);
          trail(`tear streak ${i}`, [a, add(a, v, .25 * grow), add(a, v, .5 * grow)], .028, Slash.withAlpha(.95 * fade), Y + .06 + i * .0004);
        }
        // Blood: a burst from where the limb left, the stump spurting for 1 s, a pool that grows where
        // it was torn (stays), and for a leg drops along the limp.
        for (let i = 0; i < 16; i++) {
          const life = .35 + rand(i + 400) * .3, u = age / life;
          if (u > 1) continue;
          const th = Math.atan2(away.z, away.x) + (rand(i + 410) - .5) * 1.8, r = u * (.25 + rand(i + 420) * .5);
          draw(disc, a0.x + Math.cos(th) * r, Y + .05 + i * .0002, a0.z + Math.sin(th) * r * .7 + .3 * Math.sin(u * Math.PI), .035, .025, 0, Blood.withAlpha(1 - u * .4));
        }
        for (let k = 0; k < 8; k++) {
          const u = (age - .1 - k * .13) / .3;
          if (u < 0 || u > 1) continue;
          draw(disc, st.x + sx * u * .16, Y + .045, st.z + .1 * Math.sin(u * Math.PI) - u * .12, .025, .02, 0, Blood.withAlpha(1 - u));
        }
        const pg = smooth(clamp((age - Drop) / 1.5));
        draw(disc, e0.x + sx * .12, Floor + .006, e0.z - .36, .28 * pg + .04, .14 * pg + .03, 0, Blood.withAlpha(.8));
        if (!arm) for (let k = 0; k < 6; k++) {
          const at = add(e0, L.d, .3 * (k + .5) / 6), shown = age - .05 - Drop - 1.5 * (k + .5) / 6;
          if (shown > 0) draw(disc, at.x + sx * .1 + (rand(k + 430) - .5) * .08, Floor + .007, at.z - .38, .05, .03, 0, Blood.withAlpha(.8));
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
      z: O(G.feet).z, fn: () => {   // sorted by its cell, like a pawn, not by the lowered feet
        const r = ghost2('ghost', feet, G.face, G.opts, { lo, hi, scale: sc, sun, strength, t, fray, travel: G.travel ?? { x: 0, z: 0 } });
        if (!r) return;
        const mirror = G.face === 'west' ? -1 : 1, walkAmt = G.opts.walk ?? 0;
        flakes('ghost flakes', feet, r.segs, t, { amount: p.flakes * (1 + walkAmt * .6) * (1 + 2 * fray), lo, hi, scale: sc, mirror });
        if (fray > 0) chunks('ghost chunks', feet, r.segs, t, fray * p.flakes, { lo, hi, scale: sc, mirror, layer: Y + .025 });
        if (G.held) hand(r.wrists[1], mirror > 0 ? -60 : 240, pawnLayer);
        if (p.scenario === 'tear') ghostR = r;
      },
    });
    if (hi < top && hi > 0) edgeFlakes('ghost rise', feet, hi, .8 * sc, t, { amount: p.flakes * 1.1, rise: .45 });
    if (lo > 0 && lo < top) edgeFlakes('ghost fall', feet, lo, .85 * sc, t, { amount: p.flakes * 1.5, rise: .7 });

    actors.sort((a, b) => b.z - a.z).forEach(a => a.fn());
  },
};
