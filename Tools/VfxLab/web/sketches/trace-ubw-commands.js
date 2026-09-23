// Unlimited Blade Works: commands — Trace kit proposal, not the game. Nothing in Source/RimArt draws
// this yet. What the player does while the world stands; the chant, opening and closing are
// "Unlimited Blade Works: chant and open".
//
// What it is for (proposed, none of it agreed; every number is a placeholder and will be an XML
// field).
//   While the world stands the caster gets these buttons. The swords standing in the ground are the
//   stock, and the button shows how many are left. Every command uses real swords from where they
//   stand, so walls still block them, and each sword used leaves its hole: the field visibly thins
//   where the player spends.
//   Full Open   target a pawn, then Charge and Release buttons, as Shinra Tensei has. While charging,
//               one sword every 0.1 s pulls out of its hole, nearest the target first, and hovers 1
//               cell above the hole aimed at the target, turning to follow it; up to 30 (3 s). The
//               caster stands still while charging. Release fires them all within 0.3 s: each hits as
//               its weapon and sticks in the ground past the target. The player picks how many swords
//               to spend by when they press Release. No cooldown inside.
//   Pin         target a pawn: the 4 swords nearest it fly in low and pin its trouser legs and sleeves
//               to the ground. It falls flat and counts as downed for 12 s (a hediff capping Moving
//               at 0), so it can be captured; 2 Cut per sword. 4 swords.
//   Draw        caster only. Target a sword in the world with a clear line to the caster: it tears
//               out and flies to them spinning flat, 22 cells/s, following them if they move. Each
//               hostile within reach of the spinning blade (half its length from the path, about 0.7
//               cells for a longsword) takes one hit of that sword's weapon, once. The caster catches
//               it as their weapon: a real weapon they held goes to their inventory, a traced copy
//               they held breaks. 1 sword.
//   Arm         any other colonist inside right-clicks a sword: it slides up and arcs to their hand
//               in 0.4 s as their weapon, no damage. It breaks when the world closes. 1 sword.
//   Intercept   a toggle. While on, each enemy shot at a pawn inside is met by the sword nearest its
//               path and stopped; an explosive bursts where it is met. 1 sword per shot.
//   Close       ends the world now (the other sketch).
//
// Order, with the default timings ("Command" picks one per run):
//   full open  a raider walks north, east of the caster. 0.40 a ring under him that follows him; the
//              caster raises an arm. From 0.50 one sword every 0.1 s pulls up out of its hole,
//              nearest him first, turns flat about its middle and hovers 1 cell up aimed at him, a
//              glint at its point when it is aimed; the aimed swords turn as he walks. 0.35 s after
//              the last is aimed ("Swords gathered", 16: 2.80) the arm drops and all fire within
//              0.3 s. They hit him, drop into the ground past him and quiver, leaving a crown of
//              swords leaning back in over him. With 5 or more he goes down (a stand-in rule).
//   pin        a raider walks toward the caster. 0.40 a ring under him. 0.50 the nearest sword pulls
//              out and flies in low; at 0.73 it pins the trouser leg on its side and he falls flat
//              forward in 0.3 s. The next 3 arrive 0.1 s apart once he is down and pin his other leg
//              and both sleeves, each coming in from outside the body. The swords go in deep and
//              lean a little out from him, like tent pegs. He lies pinned, jerking every 0.9 s; the
//              swords wobble and hold.
//   draw       0.40 the order: a line from the caster to a big sword about 5 cells out, and the lane
//              its spinning blade covers. 0.50 it tears out, turns flat and spins toward the caster;
//              the two raiders inside the lane are cut as it passes, the one standing 1.25 cells off
//              it is not. The caster catches it and is pushed back a little; the traced knife they
//              held breaks into light.
//   arm        0.40 the ally's order line to the nearest sword. 0.50 it slides up out of its hole,
//              arcs to the ally's hand in 0.4 s and is held, with a traced glint.
//   intercept  a shooter outside the fire fires at the caster at 0.60 and 1.40 and a rocket at 2.20.
//              Each time, the sword nearest the shot's path rises and meets it inside the edge: a
//              spark and the sword breaks into light; the rocket bursts where it is met.
//
// Drawing: the world is lib/ubw.js and the swords lib/trace.js. A flying or hovering sword is its
// texture seen from above, lying flat, point first; a stuck one turns its flat side to the camera, as
// in the planted-weapons trial. A sword pulled out turns about its middle from upright to flat. The
// Draw sword spins in the flat, so no facing needs its own method; two fainter copies a moment behind
// it are its blur. A held sword is drawn flat at the hand the way the game draws an equipped weapon.
// Pinned swords draw over the pawn (they go through its clothes). Pawns are stand-ins; the pinned
// raider's arms, legs, hands and boots only show where the swords go: in game the downed pawn is the
// game's own lying body and the swords stand at its hands and feet.
import { Color, Meshes } from '../js/engine.js';
import { P, Y, Floor, sprite, soft, glow, rand } from './lib/six-paths-impact.js';
import { draw } from './lib/six-paths-solid.js';
import { pawn, ringAt, line, strip, glint, buildingLayer, pawnLayer, shadowLayer, EnemyColour, Ally, Skin, Ink, White, Dust } from './lib/goku.js';
import {
  smooth, clamp, lerp, D2R, Black, TraceHot, solid, Weapons, v3, plus, dot3, unit, onScreen, pose, cutOf, plant, blade, breakOut, flying, stuck, blend,
  flatAt, heldCopy, shatter,
} from './lib/trace.js';
import { light, FireOuter, FireCore, Radius, SceneNorth, DuskShadow, swords, swordPose, gearShadows, fireRing, wasteland } from './lib/ubw.js';

const Caster = new Color(.55, .3, .24), Smoke = new Color(.36, .34, .33), Casing = new Color(.18, .18, .2);
const Hurt = new Color(1, .25, .2), Trousers = new Color(.32, .26, .2), Boot = new Color(.15, .11, .08);
const bodyDisc = Meshes.disc(32, 'ubw cmd body');

// The rule's fixed numbers and decided looks.
const Order = .4, Launch = .5, LiftTime = .18, QuiverHz = 8, HitHeight = .35, Clear = .25, StickLean = 25, FallTime = .3;
// Full Open: a sword lifts every GatherEvery, is pulled clear in PullUp and turned flat in TurnTime,
// then hovers Hover up aimed where the raider was TrackLag ago. Release comes Hold after the last is
// aimed and fires them all within Volley. DownAfter hits put the stand-in raider down.
const GatherEvery = .1, GatherCap = 30, PullUp = .2, TurnTime = .25, Hover = 1, Bob = .04, TrackLag = .15, Hold = .35, Volley = .3;
const DownAfter = 5, Mark = { x: 3.4, z: .1 }, MarkWalk = { x: 0, z: .5 };
// Pin. Pins: where the swords go in the lying body's frame, [along from the feet toward the head,
// across to its left]: the two trouser legs, then the two sleeves. A pin sword is driven in to PinDepth
// of its length and leans PinLean out from the body, like a tent peg: a short blade under a hilt
// covers little of the body, and a steeper lean south would fold the blade to a stub on screen.
const PinCost = 4, PinHeight = .25, PinGap = .1, PinLean = 15, PinDepth = .6, PinFrom = { x: -3.4, z: 2.5 }, PinWalk = .8, Jerk = .9;
const Pins = [[-.05, .15], [-.05, -.15], [.5, .31], [.5, -.31]];
// Draw: the caster pulls a big sword lying about DrawBearing from them. DrawFoes stand by its path:
// [share of the way from the sword, cells off the path].
const DrawSpeed = 22, DrawHeight = .45, DrawSpin = 5, TearTime = .1, TurnFlat = .06, DrawBearing = -25;
const DrawFoes = [[.42, .25], [.68, -.4], [.3, 1.25]], Big = ['LargeSword', 'Wyrmslayer', 'LongSword', 'MonoSword'];
// Arm.
const Helper = { x: -1.9, z: -1.7 }, PullTime = .25, ArcTime = .4, HeldAngle = 55;
// Intercept.
const Shots = [{ t: .6, rocket: false }, { t: 1.4, rocket: false }, { t: 2.2, rocket: true }];
const ShooterAngle = -20, BulletSpeed = 24, RocketSpeed = 11, InterceptLift = .08, InterceptSpeed = 20;

const flat = (a, b) => { const dx = b.x - a.x, dz = b.z - a.z, d = Math.hypot(dx, dz) || 1; return { x: dx / d, z: dz / d, d }; };
const home = (sw, c) => ({ x: c.x + sw.x, z: c.z + sw.z });
const nearest = (list, q, c) => list.slice().sort((a, b) => flat(home(a, c), q).d - flat(home(b, c), q).d);
const mix3 = (a, b, u) => v3(lerp(a.x, b.x, u), lerp(a.y, b.y, u), lerp(a.z, b.z, u));
const middle = b => plus(b.tip, b.A, b.L / 2);
// Pose b moved straight up by h cells.
const raised = (b, h) => pose(b.w, b.scale, v3(b.tip.x, b.tip.y + h, b.tip.z), b.A, b.B);
// Part way (u 0..1) from pose a to pose b of the same weapon, turning about the middle of the blade.
function turn(a, b, u) {
  const A = unit(mix3(a.A, b.A, u)), B = dot3(a.B, b.B) < 0 ? v3(-b.B.x, -b.B.y, -b.B.z) : b.B;
  return pose(a.w, a.scale, plus(mix3(middle(a), middle(b), u), A, -a.L / 2), A, mix3(a.B, B, u));
}
// Full Open's clock for n swords: when the last is aimed and when Release is pressed.
function volleyTimes(n) {
  const aimed = Launch + (n - 1) * GatherEvery + PullUp + TurnTime;
  return { n, aimed, release: aimed + Hold };
}
// The Full Open raider: walking north until the release, then standing.
function markAt(c, s, t) {
  const k = Math.min(Math.max(0, s), t.release);
  return { x: c.x + Mark.x + MarkWalk.x * k, z: c.z + Mark.z + MarkWalk.z * k };
}
// A Full Open sword from the moment it starts to lift: pulled straight up until its point is Clear of
// the floor, turned flat about its middle, then hovering Hover up over its hole, aimed at where the
// raider was TrackLag ago and bobbing. up: still partly in the ground. aimed: seconds since it was.
function gathered(job, s, from, p, c, t) {
  const u = s - job.up, rise = Clear - from.tip.y;
  if (u < PullUp) return { b: raised(from, rise * smooth(u / PullUp)), up: true, aimed: -1 };
  const lifted = raised(from, rise), m = middle(lifted), settle = smooth((u - PullUp) / TurnTime);
  const centre = v3(m.x, lerp(m.y, Hover, settle) + Bob * settle * Math.sin(u * 7 + job.sw.seed), m.z);
  const foe = markAt(c, s - TrackLag, t), d = flat(centre, foe);
  const aim = flatAt(from.w, p.size, centre, d, Math.min(30, Math.atan2(centre.y - HitHeight, d.d) / D2R));
  return { b: settle < 1 ? turn(lifted, aim, settle) : aim, up: false, aimed: u - PullUp - TurnTime };
}
// A Pin sword: pulled out, flying in low to point so that it arrives at `arrive`, driven in there
// leaning toward `toward`.
function lowShot(sw, c, point, arrive, toward, p) {
  const g = home(sw, c), dir = flat(g, point), start = v3(g.x + dir.x * .2, PinHeight, g.z + dir.z * .2);
  const hit = v3(point.x - dir.x * .05, PinHeight, point.z - dir.z * .05), fly = Math.hypot(hit.x - start.x, hit.z - start.z) / p.speed;
  return { sw, kind: 'stick', pin: true, launch: arrive - fly - LiftTime, lift: LiftTime, dir, start, hit, fly, land: point, toward, lean: PinLean, buried: PinDepth };
}

// The plan for the chosen command: which swords go, when, and where, and how long the run lasts.
// Only positions relative to the caster matter, so the clock can build it with the caster at 0, 0.
function plan(p, c, all, R) {
  if (p.command === 'full open') {
    const t = volleyTimes(Math.min(Math.round(p.gathered), all.length)), foe = markAt(c, t.release, t);
    const picked = nearest(all, markAt(c, Order, t), c).slice(0, t.n);
    const order = picked.map(sw => sw.seed).sort((a, b) => rand(a * 31) - rand(b * 31));
    const jobs = picked.map((sw, i) => {
      const k = order.indexOf(sw.seed);
      const job = { sw, kind: 'volley', up: Launch + i * GatherEvery, launch: t.release + (t.n > 1 ? Volley * k / (t.n - 1) : 0), lift: .05, lean: StickLean };
      job.hover = gathered(job, job.launch, swordPose(sw, c, p.size, p.lean), p, c, t).b;
      const start = job.hover.tip, dir = flat(start, foe), hit = v3(foe.x - dir.x * .15, HitHeight, foe.z - dir.z * .15);
      const reach = .55 + rand(sw.seed * 29) * .6, side = (rand(sw.seed * 23) - .5) * .8;
      return Object.assign(job, { start, dir, hit, fly: Math.hypot(hit.x - start.x, hit.y - start.y, hit.z - start.z) / p.speed,
        land: { x: foe.x + dir.x * reach - dir.z * side, z: foe.z + dir.z * reach + dir.x * side }, toward: { x: -dir.x, z: -dir.z } });
    });
    const hitsAt = jobs.map(j => j.launch + j.lift + j.fly), last = Math.max(...hitsAt);
    return { t, foe, jobs, first: Math.min(...hitsAt), down: t.n >= DownAfter ? last + .05 : Infinity, end: last + 1.6 };
  }
  if (p.command === 'pin') {
    const f = flat(PinFrom, { x: 0, z: 0 }), side = { x: -f.z, z: f.x };
    const walk = s => { const k = PinWalk * Math.max(0, s); return { x: c.x + PinFrom.x + f.x * k, z: c.z + PinFrom.z + f.z * k }; };
    const spot = (F, [along, across]) => ({ x: F.x + f.x * along + side.x * across, z: F.z + f.z * along + side.z * across });
    // out from the body: a leg's sword leans back past the feet and a little out, a sleeve's straight out
    const outward = ([along, across]) => { const k = Math.sign(across); return along < .2 ? flat({ x: 0, z: 0 }, { x: -f.x + side.x * k * .6, z: -f.z + side.z * k * .6 }) : { x: side.x * k, z: side.z * k }; };
    const picked = nearest(all, walk(Order), c).slice(0, PinCost), g0 = home(picked[0], c), at0 = walk(Launch);
    // the first pins the trouser leg on its own side, where the raider is when it arrives, and he stops there
    const leg = (g0.x - at0.x) * side.x + (g0.z - at0.z) * side.z >= 0 ? Pins[0] : Pins[1];
    let t1 = Launch + LiftTime + flat(g0, at0).d / p.speed, first;
    for (let i = 0; i < 5; i++) { first = lowShot(picked[0], c, spot(walk(t1), leg), t1, outward(leg), p); t1 = Launch + LiftTime + first.fly; }
    const F = walk(t1), rest = Pins.filter(q => q !== leg), jobs = [first];
    // the other three go to the other pins, each coming in from outside the body where it can
    const perms = a => a.length < 2 ? [a] : a.flatMap((x, i) => perms([...a.slice(0, i), ...a.slice(i + 1)]).map(r => [x, ...r]));
    const score = order => order.reduce((sum, q, k) => { const d = flat(home(picked[k + 1], c), spot(F, q)), o = outward(q); return sum - d.x * o.x - d.z * o.z; }, 0);
    perms(rest).reduce((a, b) => score(b) > score(a) ? b : a).forEach((q, k) =>
      jobs.push(lowShot(picked[k + 1], c, spot(F, q), t1 + FallTime + .05 + PinGap * k, outward(q), p)));
    const last = Math.max(...jobs.map(j => j.launch + j.lift + j.fly));
    return { f, F, t1, walk, jobs, last, end: Math.max(4, last + 2.4) };
  }
  if (p.command === 'draw') {
    const b0 = DrawBearing * D2R, off = sw => { const a = Math.atan2(sw.z, sw.x) - b0; return Math.abs(Math.atan2(Math.sin(a), Math.cos(a))); };
    const pool = all.filter(sw => Big.includes(sw.w.name) && sw.d > .55 * R && sw.d < .92 * R);
    const sw = (pool.length ? pool : all).slice().sort((a, b) => off(a) - off(b))[0];
    const g = home(sw, c), start = v3(g.x, DrawHeight, g.z), end = v3(c.x + .24, .3, c.z + .02), to = flat(g, end);
    const fly0 = Launch + TearTime, fly = Math.hypot(end.x - start.x, end.z - start.z) / DrawSpeed, caught = fly0 + fly;
    const reach = swordPose(sw, c, p.size, p.lean).L / 2, across = { x: -to.z, z: to.x };
    const foes = DrawFoes.map(([share, o]) => ({ x: g.x + (end.x - g.x) * share + across.x * o, z: g.z + (end.z - g.z) * share + across.z * o,
      off: o, pass: fly0 + fly * share, hit: Math.abs(o) <= reach }));
    return { g, to, across, reach, foes, caught, jobs: [{ sw, kind: 'caster', launch: Launch, fly0, fly, start, end }], end: caught + 1.3 };
  }
  if (p.command === 'intercept') {
    const a = ShooterAngle * D2R, out = { x: Math.cos(a), z: Math.sin(a) }, shotDir = { x: -out.x, z: -out.z };
    const shooter = { x: c.x + out.x * (R + 2.3), z: c.z + out.z * (R + 2.3) };
    const muzzle = { x: shooter.x + shotDir.x * .45, z: shooter.z + shotDir.z * .45 }, used = new Set(), toCaster = flat(muzzle, c).d;
    const jobs = Shots.map(shot => {
      const speed = shot.rocket ? RocketSpeed : BulletSpeed;
      let best = null;
      all.forEach(sw => {
        if (used.has(sw.seed)) return;
        const g = home(sw, c), along = (g.x - muzzle.x) * shotDir.x + (g.z - muzzle.z) * shotDir.z;
        const foot = { x: muzzle.x + shotDir.x * along, z: muzzle.z + shotDir.z * along };
        // the meeting point lies on the shot's path inside the edge and at least 1.5 cells short of the caster
        if (along > toCaster - 1.5 || along < toCaster - (R - .4)) return;
        // it must reach the path before the shot does
        const perp = flat(g, foot).d, meet = shot.t + along / speed;
        if (meet < shot.t + .02 + InterceptLift + perp / InterceptSpeed + .02) return;
        if (!best || perp < best.perp) best = { sw, foot, perp, meet };
      });
      if (!best) {
        // nothing can make it in time: the sword nearest the path goes anyway and meets it late
        all.filter(sw => !used.has(sw.seed)).forEach(sw => {
          const g = home(sw, c), along = Math.min(toCaster - 1.5, Math.max(toCaster - R + .4, (g.x - muzzle.x) * shotDir.x + (g.z - muzzle.z) * shotDir.z));
          const foot = { x: muzzle.x + shotDir.x * along, z: muzzle.z + shotDir.z * along }, perp = flat(g, foot).d;
          if (!best || perp < best.perp) best = { sw, foot, perp, meet: shot.t + along / speed + .15 };
        });
      }
      used.add(best.sw.seed);
      const g = home(best.sw, c), dir = flat(g, best.foot), launch = shot.t + .02;
      return { sw: best.sw, kind: 'meet', launch, lift: InterceptLift, dir, start: v3(g.x + dir.x * .1, .4, g.z + dir.z * .1),
        hit: v3(best.foot.x, HitHeight, best.foot.z), fly: Math.max(.04, best.meet - launch - InterceptLift), meet: best.meet, shot, speed };
    });
    return { shooter, muzzle, shotDir, jobs, end: 4 };
  }
  const ally = { x: c.x + Helper.x, z: c.z + Helper.z }, sw = nearest(all, ally, c)[0];
  return { ally, jobs: [{ sw, kind: 'arm', launch: Launch }], end: 3 };
}
// The plan with the caster at 0, 0, for the timeline.
function clock(p) {
  const V = Math.round(p.verse);
  return plan(p, { x: 0, z: 0 }, swords(Math.round(p.perVerse)).filter(sw => sw.k <= V), Radius[V - 1]);
}

// Where a sword of a 'stick', 'volley' or 'meet' job is at time s: turning onto its line, flying
// point first, then either dropping into the ground and quivering, or gone at the meeting. wobble
// adds to the lean of a stuck one.
function flight(job, s, from, p, wobble = 0) {
  const w = from.w, t1 = job.launch + job.lift, t2 = t1 + job.fly;
  if (s < t1) return { b: blend(from, flying(w, p.size, job.start, job.dir), smooth((s - job.launch) / job.lift)), air: true };
  if (s < t2) return { b: flying(w, p.size, mix3(job.start, job.hit, (s - t1) / job.fly), job.dir), air: true };
  const age = s - t2;
  if (job.kind === 'meet') return { b: flying(w, p.size, job.hit, job.dir), air: true, met: age };
  const settle = smooth(age / .1), L = from.L;
  const lean = job.lean + wobble + p.quiver * Math.exp(-age / .22) * Math.sin(age * QuiverHz * Math.PI * 2) * settle;
  const final = stuck(w, p.size, job.land, job.toward, lean, (job.buried ?? .2) * L * settle);
  return { b: settle < 1 ? blend(flying(w, p.size, job.hit, job.dir), final, settle) : final, air: settle < .5, landed: age, hitAt: t2 };
}

// The Caster Draw sword: torn straight up out of its hole, turned flat and spinning along the path to
// the caster's hand, then held by the caster standing at me. spinAt gives the flying pose at any
// time, for the blur copies.
function pulled(job, s, from, p, me) {
  const w = from.w, rise = Clear - from.tip.y;
  const spinAt = x => {
    const a = (DrawSpin * 360 * (x - job.fly0) + job.sw.seed * 37) * D2R;
    return flatAt(w, p.size, mix3(job.start, job.end, clamp((x - job.fly0) / job.fly)), { x: Math.cos(a), z: Math.sin(a) });
  };
  if (s < job.fly0) return { b: raised(from, rise * smooth((s - job.launch) / TearTime)), up: true };
  if (s < job.fly0 + job.fly) {
    const u = (s - job.fly0) / TurnFlat;
    return { b: u < 1 ? turn(raised(from, rise), spinAt(s), smooth(u)) : spinAt(s), up: false, spinAt, blur: u >= 1 };
  }
  return { b: heldCopy(w, p.size * .85, me, HeldAngle), held: true, since: s - job.fly0 - job.fly };
}

// The Arm sword: slides up out of its hole, arcs to the ally's hand, is held there.
function armed(job, s, from, ally, p) {
  const w = from.w, age = s - job.launch, up = v3(from.tip.x + from.A.x * (from.L * .3 + .15), from.tip.y + from.A.y * (from.L * .3 + .15), from.tip.z + from.A.z * (from.L * .3 + .15));
  const lifted = pose(w, from.scale, up, from.A, from.B), held = heldCopy(w, p.size * .85, ally, HeldAngle);
  if (age < PullTime) return { b: blend(from, lifted, smooth(age / PullTime)), held: false };
  if (age < PullTime + ArcTime) {
    const u = (age - PullTime) / ArcTime, b = blend(lifted, held, smooth(u));
    b.tip = v3(b.tip.x, b.tip.y + Math.sin(u * Math.PI) * .5, b.tip.z);
    return { b, held: false };
  }
  return { b: held, held: true, since: age - PullTime - ArcTime };
}

// A stand-in pawn at F falling flat along f (a unit direction on the floor): fall 0 is the usual
// stand-in, 1 lies along f with its feet at F. limbs 0..1 adds arms and legs spread on the floor with
// hands and boots (the pinned raider). jerk moves the body across, as when it struggles.
function figure(key, F, f, fall, colour, sun, strength, { tint = null, tintAmount = 0, limbs = 0, jerk = 0 } = {}) {
  if (fall <= 0) { pawn(F, colour, sun, strength, { tint, tintAmount }); return; }
  const a = Math.min(1, fall) * Math.PI / 2, sa = Math.sin(a), ca = Math.cos(a), side = { x: -f.z, z: f.x }, along = -Math.atan2(f.z, f.x) / D2R;
  const at = (l, across, north = 0) => ({ x: F.x + f.x * l + side.x * (across + jerk), z: F.z + f.z * l + side.z * (across + jerk) + north });
  const lit = c => tint ? Color.Lerp(c, tint, tintAmount) : c, under = at(.36, 0);
  sprite({ x: F.x + sun.x * .45, z: F.z + sun.z * .45 }, .85, .4, Ink.withAlpha(strength * (1 - sa)), soft, shadowLayer);
  sprite({ x: under.x + sun.x * .15, z: under.z + sun.z * .15 }, 1.05, .45, Ink.withAlpha(strength * sa), soft, shadowLayer, along);
  if (limbs > 0) [1, -1].forEach(k => {
    line(`${key} arm ${k}`, [at(.5, .13 * k, .06), at(.53, .38 * k, .05)], .1, lit(colour).withAlpha(limbs), solid, pawnLayer - .004, 'none');
    line(`${key} leg ${k}`, [at(.12, .07 * k, .05), at(-.1, .16 * k, .04)], .12, Trousers.withAlpha(limbs), solid, pawnLayer - .004, 'none');
    const hand = at(.54, .43 * k, .05), boot = at(-.16, .18 * k, .04);
    draw(bodyDisc, hand.x, pawnLayer - .003, hand.z, .055, .055, 0, lit(Skin).withAlpha(limbs));
    draw(bodyDisc, boot.x, pawnLayer - .003, boot.z, .06, .075, along + 90, Boot.withAlpha(limbs));
  });
  // the body's long axis turns from up the screen (standing) to along f (lying)
  const ax = f.x * .32 * sa, az = f.z * .32 * sa + .32 * ca, body = at(.32 * sa, 0, .18 * ca + .1 * sa), head = at(.74 * sa, 0, .58 * ca + .12 * sa);
  draw(bodyDisc, body.x, pawnLayer, body.z, lerp(.22, .2, sa), Math.max(.2, Math.hypot(ax, az)), Math.atan2(ax, az) / D2R, lit(colour));
  draw(bodyDisc, head.x, pawnLayer + .002, head.z, .16, .17, 0, lit(Skin));
}

export default {
  kit: 'Trace', label: 'Unlimited Blade Works: commands (sketch)',
  params: {
    command: { label: 'Command', value: 'full open', options: ['full open', 'pin', 'draw', 'arm', 'intercept'], group: 'Command' },
    gathered: P('Swords gathered (Full Open)', 16, 1, GatherCap, 1, 'Command'),
    speed: P('Sword flight, Full Open and Pin (cells/s)', 16, 6, 40, 1, 'Command'),
    quiver: P('Quiver (degrees)', 9, 0, 25, 1, 'Command'),
    verse: P('World from verse', 1, 1, 3, 1, 'World'),
    perVerse: P('Swords per verse', 30, 10, 60, 1, 'World'),
    actors: { label: 'Stand-ins', value: true, group: 'World' },
    size: P('Sword size (x image)', 1.3, .8, 2.2, .05, 'Look'),
    lean: P('Sword lean, most (degrees)', 18, 0, 40, 1, 'Look'),
    flame: P('Flame height (cells)', .45, .2, 1.5, .05, 'Look'),
    dusk: P('Dusk floor strength', .85, 0, 1, .05, 'Look'),
    gears: P('Gear shadow opacity', .24, 0, .5, .01, 'Look'),
  },
  duration(p) { return clock(p).end; },
  phases(p) {
    if (p.command === 'intercept') return [{ name: 'Shot 1', t: Shots[0].t }, { name: 'Shot 2', t: Shots[1].t }, { name: 'Rocket', t: Shots[2].t }];
    if (p.command === 'arm') return [{ name: 'Order', t: Order }, { name: 'Pulled', t: Launch }, { name: 'Held', t: Launch + PullTime + ArcTime }];
    const pl = clock(p);
    if (p.command === 'draw') return [{ name: 'Order', t: Order }, { name: 'Torn out', t: Launch }, { name: 'Caught', t: pl.caught }];
    if (p.command === 'pin') return [{ name: 'Order', t: Order }, { name: 'Swords fly', t: Launch }, { name: 'Leg pinned', t: pl.t1 }, { name: 'Pinned', t: pl.last }];
    return [{ name: 'Order', t: Order }, { name: 'Gather', t: Launch }, { name: 'Release', t: pl.t.release }, { name: 'Hits', t: pl.first },
      ...(pl.down < Infinity ? [{ name: 'Down', t: pl.down }] : [])];
  },
  events(p) {
    if (p.command === 'intercept') return [{ t: 2.4, type: 'shake', value: .04 }];
    if (p.command === 'arm') return [];
    if (p.command === 'draw') return [{ t: Launch, type: 'shake', value: .02 }];
    const pl = clock(p);
    if (p.command === 'pin') return [{ t: pl.t1, type: 'shake', value: .02 }];
    return [{ t: pl.first, type: 'shake', value: .03 }];
  },

  draw(s, p, { origin: cell, scene }) {
    const c = { x: cell.x, z: cell.z + SceneNorth }, V = Math.round(p.verse), R = Radius[V - 1];
    const all = swords(Math.round(p.perVerse)).filter(sw => sw.k <= V), pl = plan(p, c, all, R);
    if (s < 0 || s >= pl.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const dusk = { x: sun.x * DuskShadow, z: sun.z * DuskShadow };
    wasteland('ubw cmd', c, R, R, 1, p.dusk);
    gearShadows(c, R, s, p.gears);
    fireRing('ubw cmd', c, R, s, p.flame, 1);

    // The caster, pushed back a little by the catch; the pinned raider's struggle.
    const kick = p.command === 'draw' && s > pl.caught ? .1 * Math.min(1, (s - pl.caught) / .05) * Math.exp(-Math.max(0, s - pl.caught - .05) / .12) : 0;
    const me = kick ? { x: c.x + pl.to.x * kick, z: c.z + pl.to.z * kick } : c;
    const jerkAge = p.command === 'pin' && s > pl.last + .4 ? (s - pl.last - .4) % Jerk : -1;
    const jerk = jerkAge >= 0 && jerkAge < .22 ? .035 * Math.sin(jerkAge / .22 * Math.PI * 2) : 0;

    const jobs = new Map(pl.jobs.map(j => [j.sw.seed, j]));
    const standing = [], overhead = [], pinned = [], held = [], hits = [];
    all.forEach(sw => {
      const from = swordPose(sw, c, p.size, p.lean), key = `ubw cmd ${sw.seed}`, job = jobs.get(sw.seed);
      const leaves = !job ? Infinity : job.kind === 'volley' ? job.up : job.launch;
      if (s < leaves) {
        standing.push({ z: c.z + sw.z, fn: layer => { plant(key, cutOf(from, sw.sink), layer, sw.seed, sun, { cracks: 4, crumbs: 1 }); blade(key, from, dusk, strength, layer); } });
        return;
      }
      const hole = cutOf(from, sw.sink);
      plant(`${key} hole`, hole, buildingLayer - .01, sw.seed, sun, { cracks: 4, crumbs: 1 });
      if (job.kind !== 'arm') breakOut(`${key} out`, hole, s - leaves, sw.seed + 3, job.kind === 'caster' ? pl.to : null);
      if (job.kind === 'arm') {
        const st = armed(job, s, from, pl.ally, p);
        held.push(() => {
          blade(key, st.b, dusk, strength, st.held ? pawnLayer + .01 : Y + .01, { upright: !st.held && s < job.launch + PullTime });
          if (st.held && st.since < .6) blade(`${key} traced`, st.b, dusk, strength, pawnLayer + .012, { fillTo: -1, wireTo: 9, wireAlpha: .8 * (1 - st.since / .6) });
          if (st.held && st.since < .3) glint(`${key} glint`, onScreen(st.b.tip), .35, Math.sin(st.since / .3 * Math.PI), TraceHot);
        });
        return;
      }
      if (job.kind === 'caster') {
        const st = pulled(job, s, from, p, me);
        if (st.held) held.push(() => {
          blade(key, st.b, dusk, strength, pawnLayer + .01, { upright: false });
          if (st.since < .6) blade(`${key} traced`, st.b, dusk, strength, pawnLayer + .012, { fillTo: -1, wireTo: 9, wireAlpha: .8 * (1 - st.since / .6) });
          if (st.since < .12) glint(`${key} catch`, onScreen(v3(me.x + .24, .3, me.z + .02)), .45, 1 - st.since / .12, White);
        });
        else overhead.push({ z: onScreen(middle(st.b)).z, fn: layer => {
          if (st.blur) [[.024, .14], [.012, .32]].forEach(([lag, a]) => blade(`${key} blur ${lag}`, st.spinAt(s - lag), dusk, strength, layer, { alpha: a, upright: false, shadow: 0 }));
          blade(key, st.b, dusk, strength, layer, { upright: st.up });
        } });
        return;
      }
      if (job.kind === 'volley' && s < job.launch) {
        const g = gathered(job, s, from, p, c, pl.t);
        overhead.push({ z: onScreen(middle(g.b)).z, fn: layer => {
          blade(key, g.b, dusk, strength, layer, { upright: g.up });
          if (g.aimed >= 0 && g.aimed < .2) glint(`${key} aimed`, onScreen(g.b.tip), .22, Math.sin(g.aimed / .2 * Math.PI), TraceHot);
        } });
        return;
      }
      const st = flight(job, s, job.kind === 'volley' ? job.hover : from, p, job.pin ? jerk * 60 : 0);
      if (st.met !== undefined) {
        // met the shot: the sword breaks into light where it stood in the path
        if (st.met < .35) overhead.push({ z: job.hit.z, fn: () => {
          if (st.met < .14) glint(`${key} spark`, onScreen(job.hit), .38, 1 - st.met / .14, White);
          shatter(key, st.b, dusk, strength, st.met / .35, sw.seed, Y + .01, onScreen(job.hit));
        } });
        return;
      }
      if (st.air) overhead.push({ z: onScreen(middle(st.b)).z, fn: layer => blade(key, st.b, dusk, strength, layer, { upright: false }) });
      else {
        const b = st.b, age = st.landed;
        (job.pin ? pinned : standing).push({ z: job.land.z, fn: layer => {
          const cut = cutOf(b, job.buried ?? .2);
          plant(`${key} stuck`, cut, layer, sw.seed + 500, sun, { grow: smooth(age / .1), forward: { x: job.dir.x, z: job.dir.z }, cracks: 5, crumbs: 2 });
          breakOut(`${key} kick`, cut, age, sw.seed, { x: job.dir.x, z: job.dir.z });
          blade(key, b, dusk, strength, layer);
        } });
      }
      if (st.hitAt !== undefined) hits.push({ at: job.hit, age: s - st.hitAt, key, pin: job.pin });
    });
    standing.sort((a, b) => b.z - a.z).forEach((e, rank) => e.fn(buildingLayer + rank * .0045));
    pinned.sort((a, b) => b.z - a.z).forEach((e, rank) => e.fn(pawnLayer + .006 + rank * .0045));
    overhead.sort((a, b) => b.z - a.z).forEach((e, rank) => e.fn(Y + .01 + rank * .0025));
    held.forEach(fn => fn());
    hits.forEach(h => { if (h.age < .15) glint(`${h.key} spark`, onScreen(h.at), h.pin ? .22 : .32, 1 - h.age / .15, White); });

    // The command's own marks: the target rings, the Draw lane, the shots.
    if (p.command === 'full open') {
      const u = smooth(clamp((s - Order) / .3)), gone = 1 - smooth(clamp((s - pl.t.release) / .25));
      if (u > 0 && gone > 0) ringAt(markAt(c, s, pl.t), .42 + .04 * Math.sin(s * 9), TraceHot.withAlpha(.55 * u * gone), Floor + .009, false, light);
    }
    if (p.command === 'pin') {
      const u = (s - Order) / .5;
      if (u >= 0 && u < 1.6) ringAt(pl.walk(Math.min(s, pl.t1)), .55 * (.6 + .4 * smooth(Math.min(1, u))), TraceHot.withAlpha(.6 * (1 - smooth((u - .6) / 1))), Floor + .009, false, light);
    }
    if (p.command === 'draw') {
      if (s >= Order && s < pl.caught + .25) {
        const fade = s < Launch ? smooth((s - Order) / .1) : 1 - smooth((s - pl.caught) / .25), a = pl.g, b = pl.jobs[0].end;
        const n = { x: pl.across.x * pl.reach, z: pl.across.z * pl.reach }, edge = k => [{ x: a.x + n.x * k, z: a.z + n.z * k }, { x: b.x + n.x * k, z: b.z + n.z * k }];
        strip('ubw cmd lane', edge(1), edge(-1), TraceHot.withAlpha(.07 * fade), light, Floor + .0085);
        [1, -1].forEach(k => line(`ubw cmd lane ${k}`, edge(k), .03, TraceHot.withAlpha(.35 * fade), light, Floor + .0088, 'none'));
        if (s < Launch + .15) {
          const f2 = s < Launch ? fade : 1 - smooth((s - Launch) / .15);
          line('ubw cmd order', [{ x: c.x, z: c.z + .25 }, a], .025, White.withAlpha(.55 * f2), solid, Y + .005, 'none');
          ringAt(a, .32, TraceHot.withAlpha(.7 * f2), Floor + .009, false, light);
        }
      }
      pl.foes.forEach((foe, i) => { const age = s - foe.pass; if (foe.hit && age >= 0 && age < .15) glint(`ubw cmd cut ${i}`, onScreen(v3(foe.x, HitHeight + .1, foe.z)), .34, 1 - age / .15, White); });
      // the traced knife the caster held: it breaks as the drawn sword arrives
      const knife = heldCopy(Weapons.Knife, p.size * .85, me, HeldAngle), age = s - pl.caught;
      if (age < 0) {
        blade('ubw cmd knife', knife, dusk, strength, pawnLayer + .01, { upright: false });
        blade('ubw cmd knife wire', knife, dusk, strength, pawnLayer + .012, { fillTo: -1, wireTo: 9, wireAlpha: .3 });
      } else shatter('ubw cmd knife', knife, dusk, strength, age / .35, 77, pawnLayer + .012);
    }
    if (p.command === 'arm' && s >= Order && s < Launch + .15) {
      const g = home(pl.jobs[0].sw, c), fade = 1 - smooth((s - Launch) / .15);
      line('ubw cmd order', [{ x: pl.ally.x, z: pl.ally.z + .25 }, g], .025, White.withAlpha(.55 * fade), solid, Y + .005, 'none');
      ringAt(g, .28, TraceHot.withAlpha(.7 * fade), Floor + .009, false, light);
    }
    if (p.command === 'intercept') pl.jobs.forEach((job, k) => {
      const shot = job.shot, age = s - shot.t;
      if (age >= 0 && age < .1) glint(`ubw cmd muzzle ${k}`, onScreen(v3(pl.muzzle.x, HitHeight, pl.muzzle.z)), .3, 1 - age / .1, FireCore);
      if (age >= 0 && s < job.meet) {
        const d = age * job.speed, at = v3(pl.muzzle.x + pl.shotDir.x * d, HitHeight, pl.muzzle.z + pl.shotDir.z * d), tail = onScreen(v3(at.x - pl.shotDir.x * .45, HitHeight, at.z - pl.shotDir.z * .45));
        if (shot.rocket) {
          for (let i = 0; i < 8; i++) {
            const back = i * .25, pa = age - back / job.speed;
            if (pa < 0 || back > d) continue;
            const q = onScreen(v3(at.x - pl.shotDir.x * back, HitHeight, at.z - pl.shotDir.z * back));
            sprite({ x: q.x, z: q.z + i * .03 }, .18 + i * .06, .16 + i * .05, Smoke.withAlpha(.45 * (1 - i / 8)), soft, Y + .012);
          }
          sprite(onScreen(at), .34, .14, Casing, soft, Y + .016, -Math.atan2(pl.shotDir.z, pl.shotDir.x) / D2R);
          sprite(tail, .3, .3, FireOuter.withAlpha(.8), glow, Y + .015);
        } else line(`ubw cmd bullet ${k}`, [tail, onScreen(at)], .05, FireCore.withAlpha(.95), light, Y + .015, 'none');
      }
      const u = (s - job.meet) / .45;
      if (u >= 0 && u < 1 && shot.rocket) {
        const at = onScreen(job.hit);
        sprite(at, 2.4 * (.5 + u), 2 * (.5 + u), FireCore.withAlpha((1 - u) ** 2), glow, Y + .03);
        ringAt({ x: job.hit.x, z: job.hit.z }, .3 + 1.4 * smooth(u), FireOuter.withAlpha(.7 * (1 - u)), Floor + .009, true, light);
        for (let i = 0; i < 6; i++) {
          const a = i / 6 * Math.PI * 2;
          sprite({ x: at.x + Math.cos(a) * u * .8, z: at.z + Math.sin(a) * u * .6 + u * .2 }, .5 + u * .6, .4 + u * .5, Dust.withAlpha(.5 * (1 - u)), soft, Y + .025);
        }
      }
      if (shot.rocket && s >= job.meet) sprite({ x: job.hit.x, z: job.hit.z }, 1.3, .8, Black.withAlpha(.3 * smooth((s - job.meet) / .2)), soft, Floor + .0095);
    });

    if (!p.actors) return;
    const raise = p.command === 'full open' ? smooth(clamp((s - Order) / .3)) * (1 - smooth(clamp((s - pl.t.release) / .12))) : 0;
    pawn(me, Caster, sun, strength, { hair: true, arms: raise > 0 ? 1 : 0, raise });
    const flinch = list => list.some(h => h.age >= 0 && h.age < .18);
    if (p.command === 'full open') {
      const at = markAt(c, s, pl.t), u = clamp((s - pl.down) / FallTime);
      figure('ubw cmd mark', at, flat(c, at), u * u, EnemyColour, sun, strength, { tint: Hurt, tintAmount: flinch(hits) ? .6 : 0 });
    }
    if (p.command === 'pin') {
      const u = clamp((s - pl.t1) / FallTime), fall = u * u;
      figure('ubw cmd pinned', pl.walk(Math.min(s, pl.t1)), pl.f, fall, EnemyColour, sun, strength,
        { tint: Hurt, tintAmount: flinch(hits) ? .35 : 0, limbs: smooth((fall - .5) / .5), jerk });
    }
    if (p.command === 'draw') pl.foes.forEach(foe => {
      const age = s - foe.pass, struck = foe.hit && age >= 0, push = struck ? .12 * smooth(Math.min(1, age / .12)) : 0, k = Math.sign(foe.off) || 1;
      pawn({ x: foe.x + pl.across.x * k * push, z: foe.z + pl.across.z * k * push }, EnemyColour, sun, strength, { tint: Hurt, tintAmount: struck && age < .2 ? .6 : 0 });
    });
    if (p.command === 'arm') pawn(pl.ally, Ally, sun, strength);
    if (p.command === 'intercept') {
      pawn(pl.shooter, EnemyColour, sun, strength);
      line('ubw cmd gun', [{ x: pl.shooter.x, z: pl.shooter.z + .2 }, { x: pl.muzzle.x, z: pl.muzzle.z + .2 }], .07, Casing, solid, pawnLayer + .01, 'none');
    }
  },
};
