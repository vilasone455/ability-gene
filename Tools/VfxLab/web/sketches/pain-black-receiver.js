// Black Receiver (chakra rods) — technique proposal for the Pain hero kit, not the game. Nothing in
// Source/RimArt draws this yet. The kit's other abilities: Shinra Tensei and Gravity Well (C#, the
// repulsion and attraction eyes) and Banshō Ten'in (sketch, pain-bansho-tenin.js).
//
// What it is for (proposed 2026-09-24, the user said "ok"; every number is a placeholder and will be
// an XML field).
//   Target one hostile pawn or animal up to 12 cells away, line of sight; a berserk colonist counts
//   as hostile. Warm-up 0.3 s: a rod grows out of Pain's palm, then flies at 30 cells/s, and the
//   first pawn in the line takes it (the same rule as Banshō). At 1.5 cells or less Pain stabs
//   instead of throwing, with the same effect. 3 charges, one back every 10 s. It is not a Deva Path
//   technique, so it works during the 5 s gap after Shinra Tensei or Banshō Ten'in.
//   Each rod in a pawn: 5 sharp; the pawn cannot use abilities or psycasts; Moving and Manipulation
//   -25 %. Three rods in one pawn: pinned. It cannot move, shoot or melee, and Shinra Tensei, Banshō
//   Ten'in and Gravity Well do not move it. Each rod lasts 8 s from when it lands. When one breaks the
//   count drops: 3 to 2 ends the pin and the pawn is still slowed. At most 3 rods in one pawn; a 4th
//   replaces the oldest. Every rod breaks at once when Pain is downed, killed or leaves Descent.
//   Heroes are never cast by the AI.
//   Open: big bodies (a thrumbo takes the same 3 rods for now); pinning an ally on purpose (not
//   allowed for now).
//   Overlaps: Chain Sickle Stake ends in the same "cannot move, shoot or melee" (6 s, needs Snag,
//   drops the weapon). This one is ranged, slows at 1-2 rods, blocks abilities, and breaks when Pain
//   goes down. Shikamaru's shadow bind also holds, but costs Shikamaru his own movement.
//
// Source, looked at 2026-09-24 (storyboard frames, 160x90 and 320x180):
//   anime ep 166 (Hinata vs Pain clip, youtube 27GtXUQj_88 at 0:05, 0:15, 0:25): Naruto face-down
//   after Banshō, 5-6 thin dark rods standing almost upright out of his back, mixed heights, blunt
//   ends; from above they show as short dark marks. Storm 4 (Pain moveset, h5i98iv9Ohs at 0:40): Pain
//   throws long thin black rods that fly flat. Narutopedia "Black Receiver": rods come out of the
//   palm; one rod disrupts chakra and movement (ch. 381, 420); several immobilise (ch. 437); they heat
//   up while chakra goes through them (ch. 420); they break up when their maker is killed or
//   incapacitated. Naruto Mobile was not checked.
//
// Scenarios (dropdown), with the default sliders. The lab shows each rod lasting 5 s so the clip stays
// short; the rule is 8 s.
//   Three throws at a raider walking in (8 cells, walking at Pain at 2 cells/s):
//   0.00  rest
//   0.20  throw 1: Pain's arm comes up at the raider, a Rinnegan glint at the eyes, and a rod grows out
//         of the palm in 0.3 s with the fingers round it, lying level at the hand's height
//   0.50  the hand flicks 0.12 cells forward and the rod flies level at 30 cells/s, a faint dark trail
//   0.66  hit 1: it goes into the chest, tip out of the back, knob end toward Pain; a dark ripple and a
//         pale flash; the raider is jolted back and walks on at 1.5 cells/s (-25 %); chakra runs down
//         the rod as a pale spot about once a second
//   0.71-1.15  throw 2, the same, into the hip: 1 cell/s (-50 %)
//   1.21-1.63  throw 3, into the chest: pinned. It falls on its back 0.35 cells further away in 0.18 s,
//         the rods go with it and end almost upright out of its body and into the floor; dust, a shake
//   1.81-5.66  pinned: it shudders, the rods pulse; holes and short cracks in the floor under it
//   5.66  rod 1 breaks: it shrinks from the knob down in 0.35 s and sheds dark flakes; 3 to 2 rods ends
//         the pin, the raider gets up in 0.3 s with two rods back in it and walks at 1 cell/s
//   6.15  rod 2 breaks (1.5 cells/s); 6.63 rod 3 breaks (2 cells/s); the holes stay; ends 7.78
//   Pinned one stays when Pain pushes: a raider already pinned lies 2.2 cells from Pain. Two raiders
//   walk up on Pain's left from 4.2 to 1.7 cells (1.25 s). 1.45: a stand-in push (flash at Pain, a
//   soft glow 4 cells out, dust streaks running outward): both are thrown 5 cells and lie stunned; the
//   pinned one shudders and its rods flare, and it does not move. Ends 3.35.
//   After Banshō, stabbed face-down: the raider lies face-down 1.05 cells in front of Pain with its
//   head at him, stunned, on Banshō's dent and cracks. At 0.20, 0.70 and 1.20 a rod grows in Pain's
//   hand above the back (0.2 s) and is driven 0.35 cells down in 0.08 s; the hand opens and moves to the
//   next place. 1.48: the third is in, pinned. The rods lean toward Pain. Ends 3.68.
//   Pain goes down, every rod breaks: a raider pinned 3 cells away; a raider north-east shoots Pain at
//   0.55, 0.70 and 0.85; 0.95 Pain goes down, falling away from the shots, and every rod breaks at once;
//   1.40-1.70 the raider gets up and walks at Pain; the holes stay. Ends 3.20.
//
// Drawing: a rod is a 3D segment (knob end, tip, both with a height) drawn as a strip: a near-black
// edge, a dark face, a grey stripe on the side that faces the light, a pointed tip, a small round knob,
// and a shadow on the floor from its lowest point along the sun. Heights are drawn north (Lift 0.6).
// In the hand and in flight the rod lies level, so it turns with the aim. Stuck in a standing pawn it
// points back at Pain with its tip out of the pawn's back (that part drawn under the pawn). In a pawn
// on the floor the rods stand almost upright; a body lying north-south spreads them east and west
// (0.05 + 0.12 x |north share of the body axis| cells at the top, alternating sides) so they do not
// fall onto one screen line; stabbed rods lean toward Pain by 0.2 x |east share| only. Chakra going down a rod is a soft pale spot that runs to the body. A
// breaking rod shrinks from the knob down and sheds dark flakes. The holes and cracks where the rods
// went into the floor stay. Pain's arm is lib/pain.js's drawn arm. Pain, the raiders and the push are
// stand-ins; the push is not the Shinra Tensei look.
import { AltitudeLayer, Color, Mathf, Meshes } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, Lift, sprite, band, glow, soft, rand } from './lib/six-paths-impact.js';
import { streak, stunStars, whiteGlow, EnemyColour, Ink } from './lib/goku.js';
import { frame, crack, kick, rect, bump, easeOut } from './lib/chain-sickle.js';
import { eyeStar } from './lib/amenoyodomi.js';
import { Core, PaleBlue, DustC, Reach, ShoulderH, HandH, pain, painDown, standing, lying, arm } from './lib/pain.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, D2R = Mathf.Deg2Rad;
const disc = Meshes.disc(20, 'receiver disc');
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const RodFace = new Color(.1, .1, .12), RodLit = new Color(.46, .46, .51), Tracer = new Color(1, .9, .62);

// The rule's fixed numbers (placeholders).
const PerRod = .25, PinAt = 3;            // Moving lost per rod; rods that pin
// Decided timing and shape.
const Lead = .2, Recover = .2, Flick = .08, Fall = .18, GetUp = .3, Crumble = .35, Tail = .8;
const KnockBack = .35;                    // cells the third rod throws the pawn back as it falls
const Front = .62, Through = .25;         // a rod in a standing pawn: cells out the front (to the knob), out the back
const BodySurface = .1;                   // cells up where a rod leaves a pawn lying on the floor
const StabBody = 1.05, StabGap = .5, StabGrow = .2, Thrust = .08, StabLift = .35, Hold = .08;
const PushWalkFrom = 4.2, PushWalkTo = 1.7, PushOut = 5, PushFly = .45, PinnedAt = 2.2, PushFrom = [50, 115];
const ShotsAt = .55, ShotGap = .15, DownAt = .95, FreeDelay = .45, DownPinnedAt = 3;

// Where each rod goes. In a standing pawn: across the body (cells), height, yaw off the line to Pain
// (degrees), how much the knob end rises. In a pawn on the floor: along the body toward the head,
// across, height of the top, which side it leans to.
const InBody = [{ across: .09, h: .58, yaw: 7, rise: .1 }, { across: -.08, h: .32, yaw: -9, rise: .14 }, { across: .02, h: .47, yaw: 3, rise: .08 }];
const OnBack = [{ v: .26, across: .1, vis: .6, side: 1 }, { v: -.14, across: -.1, vis: .5, side: -1 }, { v: .06, across: -.01, vis: .7, side: 1 }];

const Scenarios = ['three throws at a raider walking in', 'pinned one stays when Pain pushes', 'after Banshō: stabbed face-down', 'Pain goes down: every rod breaks'];

// ---- small helpers --------------------------------------------------------------------------------
const mix3 = (a, b, u) => ({ x: lerp(a.x, b.x, u), z: lerp(a.z, b.z, u), h: lerp(a.h, b.h, u) });
const scr = q => ({ x: q.x, z: q.z + q.h * Lift });
const shd = (q, sun) => ({ x: q.x + sun.x * Math.max(0, q.h), z: q.z + sun.z * Math.max(0, q.h) });
const turn = (v, deg) => { const r = deg * D2R, c = Math.cos(r), s = Math.sin(r); return { x: v.x * c - v.z * s, z: v.x * s + v.z * c }; };

// A straight strip from A to B, w wide, shifted `off` cells to its left; tipped narrows the last 12 %
// to a point.
function strip(key, A, B, w, colour, layer, off = 0, tipped = true) {
  const dx = B.x - A.x, dz = B.z - A.z, L = Math.hypot(dx, dz);
  if (L < .005 || w <= 0) return;
  const nx = -dz / L, nz = dx / L, left = [], right = [];
  for (let i = 0; i <= 9; i++) {
    const u = i / 9, k = tipped && u > .88 ? Math.max(0, 1 - (u - .88) / .12) : 1, hw = w / 2 * k;
    const cx = A.x + dx * u + nx * off * k, cz = A.z + dz * u + nz * off * k;
    left.push({ x: cx + nx * hw, z: cz + nz * hw }); right.push({ x: cx - nx * hw, z: cz - nz * hw });
  }
  band(key, left, right, colour, layer);
}

// One rod, knob end to tip (3D points), or the part of it from `from` to `to` (shares of its length).
function rod(key, knob, tip, w, sun, strength, layer, { alpha = 1, from = 0, to = 1, tipped = true, shadow = true } = {}) {
  if (alpha <= 0 || to - from <= .001) return;
  const A3 = mix3(knob, tip, from), B3 = mix3(knob, tip, to), A = scr(A3), B = scr(B3);
  if (shadow) strip(`${key} shadow`, shd(A3, sun), shd(B3, sun), w * 1.1, Ink.withAlpha(strength * .8 * alpha), shadowLayer, 0, tipped);
  const dx = B.x - A.x, dz = B.z - A.z, L = Math.hypot(dx, dz) || 1;
  const lit = (-dz / L) * -sun.x + (dx / L) * -sun.z >= 0 ? 1 : -1;   // the side that faces the light
  strip(`${key} edge`, A, B, w + .02, Core.withAlpha(alpha), layer, 0, tipped);
  strip(`${key} face`, A, B, w, RodFace.withAlpha(alpha), layer + .0002, 0, tipped);
  strip(`${key} lit`, A, B, w * .32, RodLit.withAlpha(.9 * alpha), layer + .0004, lit * w * .22, tipped);
  if (from <= .001) {
    draw(disc, A.x, layer + .0006, A.z, w * .85, w * .85, 0, Core.withAlpha(alpha));
    draw(disc, A.x - w * .15, layer + .0008, A.z + w * .2, w * .35, w * .35, 0, RodLit.withAlpha(.8 * alpha));
  }
}
// Chakra going down a rod: a soft pale spot at share u of the way from the knob (A3) to the body (B3).
function flow(A3, B3, u, alpha, layer) {
  if (alpha <= 0 || u < 0 || u > 1) return;
  const q = scr(mix3(A3, B3, u));
  sprite(q, .22, .22, PaleBlue.withAlpha(.75 * alpha), glow, layer);
  sprite(q, .09, .09, PaleBlue.withAlpha(alpha), glow, layer + .0002);
}
// Where a rod lands: a dark ripple over the body and a pale flash, 0.25 s.
function landing(at, age, layer) {
  if (age < 0 || age > .25) return;
  const u = age / .25, e = easeOut(u);
  sprite(at, .15 + .6 * e, .12 + .45 * e, Core.withAlpha(.35 * (1 - u)), soft, layer);
  sprite(at, .4, .4, PaleBlue.withAlpha(.65 * (1 - u)), glow, layer + .0003);
}
// A breaking rod sheds flakes from where the break has got to (knob A3 toward the body B3).
function flakes(key, A3, B3, age, seed) {
  for (let i = 0; i < 7; i++) {
    const u0 = rand(seed * 17 + i), a = age - u0 * Crumble;
    if (a < 0 || a > .45) continue;
    const q = mix3(A3, B3, u0), d = turn({ x: 1, z: 0 }, rand(seed * 17 + i + 50) * 360);
    const pt = scr({ x: q.x + d.x * a * .5, z: q.z + d.z * a * .5, h: q.h + a * .55 });
    rect(`${key} flake ${i}`, pt, .055, .032, rand(seed * 17 + i + 90) * 180 + a * 500, Core.withAlpha(.85 * (1 - a / .45)), Y + .03);
  }
}
// Where a rod went into the floor: a small dark hole with a little dirt thrown up round it and short
// cracks. They stay.
function hole(key, g, seed) {
  sprite(g, .2, .14, DustC.withAlpha(.3), soft, Floor + .019);
  crack(key, g, .25, seed);
  draw(disc, g.x, Floor + .022, g.z, .045, .032, 0, Core.withAlpha(.85));
}

// ---- where the rods sit -----------------------------------------------------------------------------
// In a standing pawn at ground point g: in at the chest or hip, out the back; the knob end sticks out
// toward Pain. toward: unit vector from the pawn to Pain. entry: share of the rod in front of the body.
function inBody(k, g, toward) {
  const b = InBody[k], u = turn(toward, b.yaw), px = -toward.z, pz = toward.x;
  const E = { x: g.x + px * b.across, z: g.z + pz * b.across, h: b.h };
  return {
    knob: { x: E.x + u.x * Front, z: E.z + u.z * Front, h: E.h + b.rise },
    tip: { x: E.x - u.x * Through, z: E.z - u.z * Through, h: E.h - .04 },
    entry: Front / (Front + Through), lying: false,
  };
}
// In a pawn lying at g with its head toward `head`: almost upright out of the back, the rest through the
// body into the floor. leanTo tips the tops that way (a stab leans toward Pain), but only by the body's
// east-west share: on a body lying north-south that lean would cancel the height on screen and leave
// stubs. ground: where it went into the floor.
function onBack(k, g, head, leanTo = null) {
  const b = OnBack[k], px = -head.z, pz = head.x;
  const base = { x: g.x + head.x * b.v + px * b.across, z: g.z + head.z * b.v + pz * b.across, h: BodySurface };
  const spread = b.side * (.05 + .12 * Math.abs(head.z));
  let lx = px * spread + head.x * .03, lz = pz * spread + head.z * .03;
  if (leanTo) { const k = .2 * Math.abs(head.x); lx += leanTo.x * k; lz += leanTo.z * k; }
  return { knob: { x: base.x + lx, z: base.z + lz, h: BodySurface + b.vis }, tip: base, entry: 1, lying: true, ground: { x: base.x, z: base.z } };
}
function blend(a, b, u) {
  return { knob: mix3(a.knob, b.knob, u), tip: mix3(a.tip, b.tip, u), entry: lerp(a.entry, b.entry, u), lying: u > .5 ? b.lying : a.lying };
}

// Draw one stuck rod. In a standing pawn the part behind the body goes under the pawn. crumble 0..1
// eats it from the knob down. pulse: chakra running down it. flare: brighter while something tries to
// move the pawn.
function stuck(key, pose, s, p, sun, strength, layer, { crumble = 0, crumbleAge = -1, hitAge = -1, seed = 0, flare = 0 } = {}) {
  const w = p.rodW, from = crumble * pose.entry;
  if (crumble < 1) {
    if (pose.lying || pose.entry >= .999) rod(key, pose.knob, pose.tip, w, sun, strength, layer, { from, tipped: !pose.lying });
    else {
      rod(`${key} front`, pose.knob, pose.tip, w, sun, strength, layer, { from, to: pose.entry, tipped: false });
      if (crumble <= 0) rod(`${key} back`, pose.knob, pose.tip, w, sun, strength, pawnLayer - .004, { from: pose.entry, to: 1, shadow: false });
    }
  }
  const body = mix3(pose.knob, pose.tip, pose.entry);
  if (crumbleAge >= 0) flakes(key, pose.knob, body, crumbleAge, seed);
  if (crumble > 0) return;
  landing(scr(body), hitAge, layer + .004);
  // Chakra runs down every rod about once a second, and fast while it flares.
  const every = flare > 0 ? .25 : .9, ph = (s + rand(seed + 7) * every) % every / every;
  flow(pose.knob, body, ph * 1.4, (hitAge >= 0 && hitAge < .3 ? 1 : .55) * Math.sin(Math.min(1, ph * 1.4) * Math.PI) + .5 * flare, layer + .005);
  if (flare > 0) sprite(scr(mix3(pose.knob, body, .5)), .5, .5, PaleBlue.withAlpha(.35 * flare), glow, layer + .006);
}

// ---- the plan for "three throws" ----------------------------------------------------------------------
// The raider walks at `walk` and loses 25 % of it per rod; the third rod pins it (it falls on its back,
// 0.35 cells further away). Each throw waits for the one before it to land. Seconds and cells from Pain.
function throwPlan(p) {
  const hand = .12 + Reach, vel = n => p.walk * Math.max(0, 1 - PerRod * n);
  const segs = [{ t: 0, a: p.distance, v: p.walk }];
  const walkAt = t => { let q = segs[0]; for (const r of segs) if (r.t <= t) q = r; return q.a - q.v * (t - q.t); };
  const addSeg = (t, v) => segs.push({ t, a: walkAt(t), v });
  const starts = [], releases = [], hits = [];
  let start = Lead;
  for (let k = 0; k < 3; k++) {
    const release = start + p.warm, v = segs[segs.length - 1].v;
    const gap = walkAt(release) - .12 - hand - p.rodLen;
    const hit = release + Math.max(.02, gap / (p.speed + v));
    starts.push(start); releases.push(release); hits.push(hit);
    addSeg(hit, k + 1 >= PinAt ? 0 : vel(k + 1));
    start = Math.max(release + Recover, hit + .05);
  }
  const breaks = hits.map(h => h + p.last), upEnd = breaks[0] + GetUp;
  const count = t => hits.filter((h, k) => h <= t && t < breaks[k]).length;
  [upEnd, breaks[1], breaks[2]].filter(x => x >= upEnd).sort((a, b) => a - b).forEach(x => addSeg(x, vel(count(x))));
  const jolt = s => hits.slice(0, 2).reduce((a, h) => a + .08 * bump((s - h) / .14), 0);
  const alongAt = s => Math.max(1.3, walkAt(s) + (s >= hits[2] ? KnockBack * easeOut((s - hits[2]) / Fall) : 0) + jolt(s));
  return { hand, starts, releases, hits, breaks, landed: hits[2] + Fall, upEnd, count, walkAt, alongAt, end: breaks[2] + Crumble + Tail };
}
function stabPlan() {
  const starts = [0, 1, 2].map(k => Lead + k * StabGap), ins = starts.map(t => t + StabGrow + Thrust);
  return { starts, ins, end: ins[2] + 2.2 };
}
function pushPlan(p) {
  const arrive = (PushWalkFrom - PushWalkTo) / Math.max(.5, p.walk), at = arrive + .2;
  return { arrive, at, end: at + 1.9 };
}
function times(p) {
  const i = Scenarios.indexOf(p.scenario);
  if (i === 1) return { kind: 'push', ...pushPlan(p) };
  if (i === 2) return { kind: 'stab', ...stabPlan() };
  if (i === 3) return { kind: 'down', end: DownAt + FreeDelay + GetUp + 1.5 };
  return { kind: 'throws', ...throwPlan(p) };
}

export default {
  kit: 'Pain', label: 'Black Receiver (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    aim: P('Direction to the target (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Target distance, three throws (cells)', 8, 3, 12, .5, 'Showcase'),
    walk: P('Raider walk speed (cells/s)', 2, 0, 4, .1, 'Showcase'),
    warm: P('Warm-up: the rod grows from the palm', .3, .1, 1, .05, 'Timing (s)'),
    speed: P('Rod speed (cells/s)', 30, 10, 40, 1, 'Timing (s)'),
    last: P('Each rod lasts, shown (real: 8 s)', 5, 1.5, 8, .25, 'Timing (s)'),
    rodLen: P('Rod length (cells)', 1.1, .6, 1.6, .05, 'Rod'),
    rodW: P('Rod width (cells)', .05, .02, .1, .005, 'Rod'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    if (t.kind === 'push') return [{ name: 'Pinned', t: 0 }, { name: 'Raiders walk up', t: .2 }, { name: 'Push', t: t.at }, { name: 'Result', t: t.at + PushFly }];
    if (t.kind === 'stab') return [{ name: 'Face-down', t: 0 }, { name: 'Stab 1', t: t.starts[0] }, { name: 'Stab 2', t: t.starts[1] }, { name: 'Stab 3', t: t.starts[2] }, { name: 'Pinned', t: t.ins[2] }];
    if (t.kind === 'down') return [{ name: 'Pinned', t: 0 }, { name: 'Shots', t: ShotsAt }, { name: 'Pain down, rods break', t: DownAt }, { name: 'Free', t: DownAt + FreeDelay + GetUp }];
    return [{ name: 'Rest', t: 0 }, { name: 'Throw 1', t: t.starts[0] }, { name: 'Hit 1', t: t.hits[0] }, { name: 'Hit 2', t: t.hits[1] },
      { name: 'Hit 3: pinned', t: t.hits[2] }, { name: 'Rod 1 breaks', t: t.breaks[0] }, { name: 'All broken', t: t.breaks[2] }];
  },
  events(p) {
    const t = times(p), ev = [];
    if (t.kind === 'push') ev.push({ t: t.at, type: 'sound', def: 'AG_ShinraRelease' }, { t: t.at, type: 'shake', value: .04 });
    else if (t.kind === 'stab') t.ins.forEach((x, k) => ev.push({ t: t.starts[k], type: 'sound', def: 'RimArt_ReceiverGrow' }, { t: x, type: 'sound', def: k === 2 ? 'RimArt_ReceiverPin' : 'RimArt_ReceiverHit' }, { t: x, type: 'shake', value: k === 2 ? .025 : .012 }));
    else if (t.kind === 'down') ev.push({ t: DownAt, type: 'sound', def: 'RimArt_ReceiverBreak' });
    else {
      t.hits.forEach((x, k) => ev.push({ t: t.starts[k], type: 'sound', def: 'RimArt_ReceiverGrow' }, { t: t.releases[k], type: 'sound', def: 'RimArt_ReceiverThrow' },
        { t: x, type: 'sound', def: 'RimArt_ReceiverHit' }, { t: x, type: 'shake', value: .012 }, { t: t.breaks[k], type: 'sound', def: 'RimArt_ReceiverBreak' }));
      ev.push({ t: t.landed, type: 'sound', def: 'RimArt_ReceiverPin' }, { t: t.landed, type: 'shake', value: .03 });
    }
    return ev;
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const f = frame(p.aim, sun), away = { x: f.ca, z: f.sa }, toward = { x: -f.ca, z: -f.sa };
    const P0 = f.ground(o, t.kind === 'throws' ? -p.distance / 2 : -1.5, 0);
    const shoulder = f.place(P0, .05, -.1, ShoulderH);
    if (t.kind === 'throws') drawThrows(s, p, t, f, P0, shoulder, away, toward, sun, strength);
    else if (t.kind === 'push') drawPush(s, p, t, f, P0, away, toward, sun, strength);
    else if (t.kind === 'stab') drawStabs(s, p, t, f, P0, shoulder, toward, sun, strength);
    else drawDown(s, p, f, P0, away, toward, sun, strength);
  },
};

// ---- scenario: three throws -------------------------------------------------------------------------
function drawThrows(s, p, t, f, P0, shoulder, away, toward, sun, strength) {
  const a = t.alongAt(s), walked = p.distance - t.walkAt(s);   // walkAt leaves out the knock-back, so this only grows
  const pinnedNow = s >= t.hits[2] && s < t.breaks[0];
  const shake = pinnedNow && s >= t.landed ? .012 * Math.sin(s * 70) : 0;
  const g = f.ground(P0, a, shake), moving = s < t.hits[2] || s >= t.upEnd;
  const bob = moving && p.walk > 0 ? .02 * Math.abs(Math.sin(walked * 5.2)) : 0;
  const gl = f.ground(P0, t.walkAt(t.hits[2]) + KnockBack, 0);   // where it lies while pinned

  // Pain, and the raider: standing, falling on its back, pinned, getting up, walking again.
  pain(P0, sun, strength);
  const fallU = smooth(clamp((s - t.hits[2]) / Fall)), riseU = smooth(clamp((s - t.breaks[0]) / GetUp));
  const lieA = s < t.hits[2] ? 0 : s < t.breaks[0] ? fallU : 1 - riseU;
  if (lieA > 0) lying('receiver target lying', { x: gl.x + shake, z: gl.z }, EnemyColour, away, sun, strength, lieA);
  if (lieA < 1) standing({ x: g.x, z: g.z + bob }, EnemyColour, sun, strength, 1 - lieA);

  // The floor under the pinned body: the rods went through into it. Holes and cracks stay.
  const pinned = [0, 1, 2].map(k => onBack(k, gl, away));
  if (s >= t.landed) pinned.forEach((q, k) => hole(`receiver hole ${k}`, q.ground, k * 3));
  if (s >= t.landed && s < t.landed + .6) kick(gl, s - t.landed, 1 - clamp((s - t.landed) / .6), 21);

  // Pain's arm: up at the target through the three throws, the fingers round the rod while it grows,
  // a flick forward as it leaves, then down after the last one.
  const armUp = smooth(clamp((s - t.starts[0]) / (p.warm * .6))), armDown = smooth(clamp((s - (t.releases[2] + .25)) / .3));
  const flick = t.releases.reduce((acc, r) => acc + .12 * bump((s - r) / (Flick * 2)), 0);
  const reach = Reach * armUp * (1 - armDown), handAlong = .12 + reach + flick;
  const holding = t.starts.some((st, k) => s >= st && s < t.releases[k]);
  if (armUp > 0 && armDown < 1) arm('receiver arm', shoulder, f.place(P0, handAlong, -.1, HandH), away, holding ? .55 : 0);
  eyeStar({ x: P0.x, z: P0.z + .6 }, .28, bump(clamp((s - t.starts[0]) / .35)));

  const standPose = k => inBody(k, f.ground(P0, a, 0), toward);
  for (let k = 0; k < 3; k++) {
    const key = `receiver rod ${k}`;
    if (s < t.starts[k]) continue;
    if (s < t.releases[k]) {                     // grows out of the palm, level at the hand's height
      const grow = smooth(clamp((s - t.starts[k]) / p.warm)), knob = { ...f.ground(P0, handAlong, -.1), h: HandH };
      const tip = { ...f.ground(P0, handAlong + p.rodLen * grow, -.1), h: HandH };
      rod(key, knob, tip, p.rodW, sun, strength, pawnLayer + .0104);
      sprite(scr(knob), .3 * grow, .3 * grow, PaleBlue.withAlpha(.5 * (1 - grow * .5)), glow, pawnLayer + .0112);
      continue;
    }
    if (s < t.hits[k]) {                         // in flight, level, dropping to the height it hits at
      const u = (s - t.releases[k]) / (t.hits[k] - t.releases[k]), tipA = lerp(t.hand + p.rodLen, a - .12, u);
      const h = lerp(HandH, InBody[k].h, u), across = lerp(-.1, InBody[k].across, u);
      const tip = { ...f.ground(P0, tipA, across), h }, knob = { ...f.ground(P0, tipA - p.rodLen, across), h: h + .02 };
      rod(key, knob, tip, p.rodW, sun, strength, Y + .02);
      streak(`${key} trail`, scr(knob), scr({ ...f.ground(P0, tipA - p.rodLen - .7, across), h }), .07, Core.withAlpha(.3), undefined, Y + .019, 4);
      continue;
    }
    // Stuck: in the standing pawn, carried over on its back while it falls, upright while it is pinned,
    // back in the standing pawn once it is up. A rod breaking while the pawn is down breaks where it stands.
    const broke = s >= t.breaks[k], lyingBreak = t.breaks[k] < t.upEnd;
    let pose;
    if (broke && lyingBreak) pose = pinned[k];
    else if (s < t.hits[2]) pose = standPose(k);
    else if (s < t.landed) pose = blend(inBody(k, f.ground(P0, t.walkAt(t.hits[2]), 0), toward), pinned[k], fallU);
    else if (s < t.breaks[0]) pose = pinned[k];
    else if (s < t.upEnd) pose = blend(pinned[k], standPose(k), riseU);
    else pose = standPose(k);
    if (pose.lying) pose = { ...pose, knob: { ...pose.knob, x: pose.knob.x + shake }, tip: { ...pose.tip, x: pose.tip.x + shake } };
    const crumble = broke ? clamp((s - t.breaks[k]) / Crumble) : 0;
    stuck(key, pose, s, p, sun, strength, pawnLayer + .012 + k * .0008, { crumble, crumbleAge: broke ? s - t.breaks[k] : -1, hitAge: s - t.hits[k], seed: k });
  }
}

// ---- scenario: the pinned one stays when Pain pushes ------------------------------------------------
function drawPush(s, p, t, f, P0, away, toward, sun, strength) {
  const gl = f.ground(P0, PinnedAt, 0), age = s - t.at;
  const jolt = age >= 0 && age < .3 ? .025 * Math.sin(age * 90) * (1 - age / .3) : 0;
  pain(P0, sun, strength);
  const pinned = [0, 1, 2].map(k => onBack(k, gl, away));
  pinned.forEach((q, k) => hole(`receiver push hole ${k}`, q.ground, k * 3));
  lying('receiver push pinned', { x: gl.x + jolt, z: gl.z }, EnemyColour, away, sun, strength);
  const flare = age >= 0 ? clamp(1 - age / .5) : 0;
  pinned.forEach((q, k) => stuck(`receiver push rod ${k}`, { ...q, knob: { ...q.knob, x: q.knob.x + jolt }, tip: { ...q.tip, x: q.tip.x + jolt } },
    s, p, sun, strength, pawnLayer + .012 + k * .0008, { seed: k, flare }));
  if (age >= 0 && age < .5) kick(gl, age, 1 - age / .5, 33);

  // Two raiders walk up on Pain's left (north for the default aim, so the lab's camera shows them) and are
  // thrown 5 cells out; they land and lie stunned.
  for (const side of [-1, 1]) {
    const dir = turn(away, PushFrom[side > 0 ? 1 : 0]), r = age < 0 ? lerp(PushWalkFrom, PushWalkTo, clamp(s / t.arrive)) : PushWalkTo + PushOut * easeOut(age / PushFly);
    const g = { x: P0.x + dir.x * r, z: P0.z + dir.z * r }, key = `receiver push raider ${side}`;
    if (age < 0) standing({ x: g.x, z: g.z + (s < t.arrive ? .02 * Math.abs(Math.sin(s * 10)) : 0) }, EnemyColour, sun, strength);
    else if (age < PushFly) {
      const h = .35 * bump(age / PushFly);
      sprite({ x: g.x + sun.x * (.3 + h), z: g.z + sun.z * (.3 + h) }, .8, .4, Ink.withAlpha(strength / (1 + h)), soft, shadowLayer);
      standing({ x: g.x, z: g.z + h * Lift }, EnemyColour, { x: 0, z: 0 }, 0);
    } else {
      lying(key, g, EnemyColour, dir, sun, strength);
      stunStars(key, { x: g.x + dir.x * .4, z: g.z + dir.z * .4 + .08 - .84 }, s, clamp((age - PushFly) / .15));
      if (age < PushFly + .5) kick(g, age - PushFly, 1 - (age - PushFly) / .5, side > 0 ? 40 : 50);
    }
  }
  // The push itself, a stand-in (not the Shinra Tensei look): a flash at Pain, a soft glow 4 cells out,
  // dust streaks running outward on the floor.
  if (age >= 0 && age < .45) {
    const u = age / .45;
    sprite({ x: P0.x, z: P0.z + .35 }, 2.2, 2.2, PaleBlue.withAlpha(.7 * clamp(1 - age / .15)), glow, Y + .1);
    sprite({ x: P0.x, z: P0.z + .2 }, 8 * easeOut(clamp(age / .2)), 8 * easeOut(clamp(age / .2)), PaleBlue.withAlpha(.22 * (1 - u)), glow, Y + .09);
    for (let i = 0; i < 16; i++) {
      const ang = i / 16 * 360 + rand(i + 300) * 12, d = turn({ x: 1, z: 0 }, ang), r0 = .6 + easeOut(u) * 3.2 * (.7 + rand(i + 301) * .3), r1 = r0 + .5 + rand(i + 302) * .5;
      streak(`receiver push streak ${i}`, { x: P0.x + d.x * r0, z: P0.z + d.z * r0 }, { x: P0.x + d.x * r1, z: P0.z + d.z * r1 }, .06, DustC.withAlpha(.45 * Math.sin(u * Math.PI)), undefined, Floor + .016, 4);
    }
  }
}

// ---- scenario: after Banshō, stabbed face-down --------------------------------------------------------
function drawStabs(s, p, t, f, P0, shoulder, toward, sun, strength) {
  const gl = f.ground(P0, StabBody, 0), pinnedNow = s >= t.ins[2];
  const shake = pinnedNow ? .012 * Math.sin(s * 70) : 0;
  pain(P0, sun, strength);
  // Banshō's slam left a dent and cracks under it.
  sprite(gl, 1.2, .9, Core.withAlpha(.25), soft, Floor + .02);
  crack('receiver stab dent', gl, 1.3, 17);
  lying('receiver stab target', { x: gl.x + shake, z: gl.z }, EnemyColour, toward, sun, strength);
  stunStars('receiver stab stun', { x: gl.x + toward.x * .4, z: gl.z + toward.z * .4 + .08 - .84 }, s, clamp((1.1 - s) / .3));

  const finals = [0, 1, 2].map(k => onBack(k, gl, toward, toward));
  // A rod before and during the thrust: the final pose moved back up its own axis by `lift` cells.
  const raised = (q, lift) => {
    const dx = q.tip.x - q.knob.x, dz = q.tip.z - q.knob.z, dh = q.tip.h - q.knob.h, L = Math.hypot(dx, dz, dh) || 1;
    const m = { x: -dx / L * lift, z: -dz / L * lift, h: -dh / L * lift };
    return { ...q, knob: { x: q.knob.x + m.x, z: q.knob.z + m.z, h: q.knob.h + m.h }, tip: { x: q.tip.x + m.x, z: q.tip.z + m.z, h: q.tip.h + m.h } };
  };
  let hand = null, grip = .3;
  for (let k = 0; k < 3; k++) {
    const st = t.starts[k], thrustAt = st + StabGrow, key = `receiver stab rod ${k}`;
    if (s < st) continue;
    if (s < t.ins[k]) {                            // grows in the hand, then is driven down
      const grow = smooth(clamp((s - st) / StabGrow)), push = clamp((s - thrustAt) / Thrust);
      const q = raised(finals[k], StabLift * (1 - push * push));
      rod(key, q.knob, q.tip, p.rodW, sun, strength, pawnLayer + .0104, { to: grow });
      hand = scr(q.knob); grip = .7;
      continue;
    }
    if (s < t.ins[k] + Hold) { hand = scr(finals[k].knob); grip = lerp(.7, .3, (s - t.ins[k]) / Hold); }
    stuck(key, { ...finals[k], knob: { ...finals[k].knob, x: finals[k].knob.x + shake }, tip: { ...finals[k].tip, x: finals[k].tip.x + shake } },
      s, p, sun, strength, pawnLayer + .012 + k * .0008, { hitAge: s - t.ins[k], seed: k });
    if (k === 2 && s - t.ins[2] < .6) kick(gl, s - t.ins[2], 1 - (s - t.ins[2]) / .6, 60);
  }
  // Outside the stabs the hand travels: up from rest to the first rod, from each rod to the next one's
  // start, and back down after the last.
  if (!hand) {
    const rest = f.place(P0, .12 + Reach * .4, -.1, HandH), legs = [[t.starts[0] - .2, rest, t.starts[0], scr(raised(finals[0], StabLift).knob)]];
    for (let k = 0; k < 2; k++) legs.push([t.ins[k] + Hold, scr(finals[k].knob), t.starts[k + 1], scr(raised(finals[k + 1], StabLift).knob)]);
    legs.push([t.ins[2] + Hold, scr(finals[2].knob), t.ins[2] + Hold + .35, rest]);
    const leg = legs.find(([t0, , t1]) => s >= t0 && s < t1);
    if (leg) { const u = smooth((s - leg[0]) / (leg[2] - leg[0])); hand = { x: lerp(leg[1].x, leg[3].x, u), z: lerp(leg[1].z, leg[3].z, u) }; }
  }
  if (hand) arm('receiver stab arm', shoulder, hand, { x: -toward.x, z: -toward.z }, grip);
  eyeStar({ x: P0.x, z: P0.z + .6 }, .28, bump(clamp((s - t.starts[0]) / .35)));
}

// ---- scenario: Pain goes down, every rod breaks ---------------------------------------------------------
function drawDown(s, p, f, P0, away, toward, sun, strength) {
  const gl = f.ground(P0, DownPinnedAt, 0), shooter = f.ground(P0, 6, 3.4), free = DownAt + FreeDelay;
  const pinnedNow = s < DownAt, shake = pinnedNow ? .012 * Math.sin(s * 70) : 0;
  const pinned = [0, 1, 2].map(k => onBack(k, gl, away));
  pinned.forEach((q, k) => hole(`receiver down hole ${k}`, q.ground, k * 3));

  // Pain is shot three times and goes down; he falls away from the shooter.
  const downU = smooth(clamp((s - DownAt) / .2)), fromShooter = { x: P0.x - shooter.x, z: P0.z - shooter.z }, L = Math.hypot(fromShooter.x, fromShooter.z);
  const fall = { x: fromShooter.x / L, z: fromShooter.z / L };
  if (downU < .5) pain({ x: P0.x + fall.x * .15 * downU, z: P0.z + fall.z * .15 * downU }, sun, strength);
  else painDown('receiver pain down', { x: P0.x + fall.x * .25, z: P0.z + fall.z * .25 }, fall, sun, strength);
  standing(shooter, EnemyColour, sun, strength);
  for (let i = 0; i < 3; i++) {
    const at = ShotsAt + i * ShotGap, age = s - at;
    if (age < 0 || age > .1) continue;
    const from = { x: shooter.x, z: shooter.z + .3 }, to = { x: P0.x + (rand(i + 70) - .5) * .15, z: P0.z + .3 }, fade = 1 - age / .1;
    streak(`receiver down shot ${i}`, from, to, .09, Tracer.withAlpha(fade), whiteGlow, Y + .05, 6);
    sprite(to, .5, .5, Tracer.withAlpha(.9 * fade), glow, Y + .051);
    sprite(from, .3, .3, Tracer.withAlpha(.8 * fade), glow, Y + .051);
  }

  // The pinned raider: pinned until Pain goes down, then every rod breaks at once and it gets up.
  const riseU = smooth(clamp((s - free) / GetUp)), walk = s > free + GetUp ? Math.min(1.2, (s - free - GetUp) * p.walk * .6) : 0;
  if (riseU < 1) lying('receiver down pinned', { x: gl.x + shake, z: gl.z }, EnemyColour, away, sun, strength, 1 - riseU);
  if (riseU > 0) standing(f.ground(P0, DownPinnedAt - walk, 0), EnemyColour, sun, strength, riseU);
  pinned.forEach((q, k) => {
    const brokeAge = s - DownAt - k * .03, crumble = brokeAge >= 0 ? clamp(brokeAge / Crumble) : 0;
    stuck(`receiver down rod ${k}`, { ...q, knob: { ...q.knob, x: q.knob.x + shake }, tip: { ...q.tip, x: q.tip.x + shake } }, s, p, sun, strength,
      pawnLayer + .012 + k * .0008, { crumble, crumbleAge: brokeAge, seed: k });
  });
  if (s >= DownAt && s < DownAt + .3) sprite({ x: gl.x, z: gl.z + .3 }, 1.2, 1, PaleBlue.withAlpha(.35 * (1 - (s - DownAt) / .3)), glow, Y + .04);
}
