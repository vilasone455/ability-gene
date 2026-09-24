// Unlimited Blade Works: commands (pocket) — Trace kit proposal, not the game. Nothing in Source/RimArt
// draws this yet. The commands of the pocket-map version, used inside the world while it stands. The world
// itself (the fire that opens it, the close) is "Unlimited Blade Works: world (pocket)"; the home-map side
// is "Unlimited Blade Works: cast (pocket)". The commands, their order and their timings are those of the
// in-place "Unlimited Blade Works: commands", which stays as it was for reference; this sketch puts them on
// the pocket world's ground and adds the pocket version's time rule.
//
// What it is for (proposed, none of it agreed; every number is a placeholder and will be an XML field).
//   The world lasts 20, 25 or 30 s by verse. Every sword a command uses takes 0.5 s off that as it leaves
//   the ground (the source's rule: the swords standing in the world cost nothing, using them costs the
//   caster). The swords are the world's own, about 400 standing on the 40 x 40 map. A command takes real
//   ones from where they stand and each leaves its hole. Swords past the map edge are out of reach.
//   Full Open   target a pawn, then Charge and Release buttons, as Shinra Tensei has. While charging, one
//               sword every 0.1 s pulls out of its hole, nearest the target first, and hovers 1 cell above
//               the hole aimed at the target, turning to follow it; up to 30 (3 s, and 15 s of the world).
//               The caster stands still while charging. Release fires them all within 0.3 s: each hits as
//               its weapon and sticks in the ground past the target. No cooldown.
//   Pin         target a pawn: the 4 swords nearest it fly in low and pin its trouser legs and sleeves to
//               the ground. It falls flat and counts as downed for 12 s (a hediff capping Moving at 0), so
//               it can be captured; 2 Cut per sword. 2 s of the world.
//   Draw        caster only. Target a sword with a clear line to the caster: it tears out and flies to them
//               spinning flat, 22 cells/s. Each hostile within reach of the spinning blade (half its length
//               from the path, about 0.7 cells for a longsword) takes one hit of that sword's weapon, once.
//               The caster catches it as their weapon: a real weapon they held goes to their inventory, a
//               traced copy they held breaks. 0.5 s.
//   Arm         any other colonist inside right-clicks a sword: it slides up and arcs to their hand in 0.4 s
//               as their weapon, no damage. It breaks when the world closes. 0.5 s.
//   Intercept   a toggle. While on, each enemy shot at a pawn inside is met by the sword nearest its path
//               that can get there first, and stopped; an explosive bursts where it is met. 0.5 s a shot.
//               A shot no sword can reach in time (fired from close by, or across bare ground) is not
//               stopped and costs nothing.
//   Close       ends the world now (the world sketch).
//
// Order, with the default timings ("Command" picks one per run). The bar under the caster is the world's
// time left: 16 of 20 s at the order, running down 1 s a second, and 0.5 s at each sword as it leaves.
//   full open  a raider walks north, east of the caster. 0.40 a ring under him that follows him; the caster
//              raises an arm. From 0.50 one sword every 0.1 s pulls up out of its hole, nearest him first,
//              turns flat about its middle and hovers 1 cell up aimed at him, a glint at its point when it
//              is aimed; the aimed swords turn as he walks. 0.35 s after the last is aimed ("Swords
//              gathered", 16: 2.80) the arm drops and all fire within 0.3 s. First hit 2.99. They hit him,
//              drop into the ground past him and quiver, leaving a crown of swords leaning back in over him.
//              With 5 or more he goes down (3.45; a stand-in rule). 16 holes where the swords stood, up to
//              4.6 cells from him. The bar is down 8 s for the swords: 3 s left when the run ends (5.00).
//   pin        a raider walks toward the caster from the north-west. 0.40 a ring under him. 0.50 the nearest
//              sword pulls out and flies in low; at 0.73 it pins the trouser leg on its side and he falls flat
//              forward in 0.3 s. The next 3 arrive 0.1 s apart once he is down (last 1.28) and pin his other
//              leg and both sleeves, each coming in from outside the body. The swords go in deep and lean a
//              little out from him, like tent pegs. He lies pinned, jerking every 0.9 s. 2 s off the bar.
//   draw       0.40 the order: a line from the caster to a big sword 5 cells out, and the lane its spinning
//              blade covers. 0.50 it tears out, turns flat and spins toward the caster through the field;
//              the two raiders inside the lane are cut as it passes, the one standing 1.25 cells off it is
//              not. 0.82 the caster catches it and is pushed back a little; the traced knife they held
//              breaks into light.
//   arm        0.40 the ally's order line to the nearest sword. 0.50 it slides up out of its hole, arcs to
//              the ally's hand in 0.4 s and is held, with a traced glint.
//   intercept  a raider 9 cells out, inside the world, fires at the caster at 0.60 and 1.40 and a rocket at
//              2.20. Each time, the sword nearest the shot's path that can get there first rises and meets
//              it (0.79, 1.55, 2.51): a spark and the sword breaks into light; the rocket bursts where it is
//              met.
//
// Drawing: the world is lib/ubw-pocket.js (ground, light, gear shadows, haze, embers, the baked field) and
// the swords lib/trace.js. The command's swords are left out of the bake and drawn one by one: standing
// until they leave, then their holes. The bake's blades are split at each of those swords and at each place
// a Full Open sword sticks, so what is drawn one by one goes between the swords north of it and those south
// of it, as the whole field is ordered north first. That split is how the lab scrubs time without a bake
// per frame; in game the field can simply be baked again when swords leave or land. A flying or hovering
// sword is its texture seen from above, lying flat, point first; a stuck one turns its flat side to the
// camera. A sword pulled out turns about its middle from upright to flat. The Draw sword spins in the flat,
// so no facing needs its own method; two fainter copies a moment behind it are its blur. It flies at hip
// height through the field: the standing swords are drawn, not things, and block nothing. A held sword is
// drawn flat at the hand the way the game draws an equipped weapon. Pinned swords draw over the pawn (they
// go through its clothes). Everything drawn one by one takes the world's warm light, as the bake does.
// Each command's stand-ins keep their own spots clear of swords, so the world differs a little from one
// command to the next. Lab stand-ins ("Stand-ins and map edge"): the pawns, the shooter's gun, the dashed
// map edge; the pinned raider's arms, legs, hands and boots only show where the swords go. "World time
// bar" stands in for the time left shown on the caster's gizmo.
import { Color, Meshes } from '../js/engine.js';
import { P, Y, Floor, sprite, soft, glow, rand } from './lib/six-paths-impact.js';
import { draw } from './lib/six-paths-solid.js';
import { pawn, ringAt, line, strip, glint, buildingLayer, pawnLayer, shadowLayer, EnemyColour, Ally, Skin, Ink, White, Dust } from './lib/goku.js';
import {
  smooth, clamp, lerp, D2R, Black, Trace, TraceHot, solid, Weapons, v3, plus, dot3, unit, onScreen, pose, cutOf, plant, blade, breakOut, flying,
  stuck, blend, flatAt, heldCopy, shatter,
} from './lib/trace.js';
import { light, FireOuter, FireCore, SceneNorth } from './lib/ubw.js';
import {
  MapHalf, DuskShadow, Tint, Twilight, Look, fieldList, standingPose, field, drawField, floor, patches, backstop, haze, sunGlow, hill,
  skyGears, embers, mapEdge,
} from './lib/ubw-pocket.js';

const Caster = new Color(.55, .3, .24), Smoke = new Color(.36, .34, .33), Casing = new Color(.18, .18, .2);
const Hurt = new Color(1, .25, .2), Trousers = new Color(.32, .26, .2), Boot = new Color(.15, .11, .08), Empty = new Color(.9, .2, .15);
const bodyDisc = Meshes.disc(32, 'ubwc body');
// The world as the world sketch draws it by default, and the warmth its light gives the pawns.
const World = { density: Look.density, hill: Look.hill, beyond: Look.beyond, size: Look.size, lean: Look.lean };
const Warm = { tint: Twilight, tintAmount: .3 * Look.twilight };

// World time: the world lasts Lasts[verse - 1] seconds; each sword a command uses costs SwordCost as it
// leaves the ground.
const Lasts = [20, 25, 30], SwordCost = .5;
// The rule's fixed numbers and decided looks, as in the in-place commands sketch.
const Order = .4, Launch = .5, LiftTime = .18, QuiverHz = 8, Quiver = 9, HitHeight = .35, Clear = .25, StickLean = 25, FallTime = .3;
// Full Open: a sword lifts every GatherEvery, is pulled clear in PullUp and turned flat in TurnTime,
// then hovers Hover up aimed where the raider was TrackLag ago. Release comes Hold after the last is
// aimed and fires them all within Volley. DownAfter hits put the stand-in raider down.
const GatherEvery = .1, GatherCap = 30, PullUp = .2, TurnTime = .25, Hover = 1, Bob = .04, TrackLag = .15, Hold = .35, Volley = .3;
const DownAfter = 5, Mark = { x: 3.4, z: .1 }, MarkWalk = { x: 0, z: .5 };
// Pin. Pins: where the swords go in the lying body's frame, [along from the feet toward the head,
// across to its left]: the two trouser legs, then the two sleeves. A pin sword is driven in to PinDepth
// of its length and leans PinLean out from the body, like a tent peg.
const PinCost = 4, PinHeight = .25, PinGap = .1, PinLean = 15, PinDepth = .6, PinFrom = { x: -3.4, z: 2.5 }, PinWalk = .8, Jerk = .9;
const Pins = [[-.05, .15], [-.05, -.15], [.5, .31], [.5, -.31]];
// Draw: the caster pulls a big sword DrawReach cells out, nearest the bearing DrawBearing. DrawFoes stand
// by its path: [share of the way from the sword, cells off the path].
const DrawSpeed = 22, DrawHeight = .45, DrawSpin = 5, TearTime = .1, TurnFlat = .06, DrawBearing = -25, DrawReach = [4.2, 6];
const DrawFoes = [[.42, .25], [.68, -.4], [.3, 1.25]], Big = ['LargeSword', 'Wyrmslayer', 'LongSword', 'MonoSword'], Hand = { x: .24, z: .02 };
// Arm.
const Helper = { x: -1.9, z: -1.7 }, PullTime = .25, ArcTime = .4, HeldAngle = 55;
// Intercept: the shooter stands ShooterAt cells out, inside the world. A shot is met at least MinAlong
// cells from the muzzle and 1.5 short of the caster, by a sword that gets there first: it leaves 0.02 s
// after the shot, lifts in InterceptLift and flies InterceptSpeed, so a bullet has gone about 3 cells
// before any sword can meet it.
const Shots = [{ t: .6, rocket: false }, { t: 1.4, rocket: false }, { t: 2.2, rocket: true }];
const ShooterAngle = -20, ShooterAt = 9, MinAlong = 1.2, BulletSpeed = 24, RocketSpeed = 11, InterceptLift = .08, InterceptSpeed = 20;

const flat = (a, b) => { const dx = b.x - a.x, dz = b.z - a.z, d = Math.hypot(dx, dz) || 1; return { x: dx / d, z: dz / d, d }; };
const homeOf = (sw, c) => ({ x: c.x + sw.x, z: c.z + sw.z });
const mix3 = (a, b, u) => v3(lerp(a.x, b.x, u), lerp(a.y, b.y, u), lerp(a.z, b.z, u));
const middle = b => plus(b.tip, b.A, b.L / 2);
// Pose b moved straight up by h cells.
const raised = (b, h) => pose(b.w, b.scale, v3(b.tip.x, b.tip.y + h, b.tip.z), b.A, b.B);
// Part way (u 0..1) from pose a to pose b of the same weapon, turning about the middle of the blade.
function turn(a, b, u) {
  const A = unit(mix3(a.A, b.A, u)), B = dot3(a.B, b.B) < 0 ? v3(-b.B.x, -b.B.y, -b.B.z) : b.B;
  return pose(a.w, a.scale, plus(mix3(middle(a), middle(b), u), A, -a.L / 2), A, mix3(a.B, B, u));
}

// Where each command's stand-ins stand, from the caster. The world keeps them clear: no sword is drawn
// over one (makeField's keep), as the pocket map is made after everyone taken is known. A walking raider
// keeps his path clear, and a fallen one the ground he falls on. Each command keeps only its own, so the
// Draw raiders do not clear the Intercept shot's line of swords.
const toward = (a, b, d) => { const f = flat(a, b); return { x: a.x + f.x * d, z: a.z + f.z * d }; };
const Spots = {
  'full open': [0, .7, 1.4, 2.1].flatMap(z => [{ x: Mark.x, z: Mark.z + z }, { x: Mark.x + .5, z: Mark.z + z }]),
  pin: [0, .6, 1.1].map(d => toward(PinFrom, { x: 0, z: 0 }, d)),
  draw: [],
  arm: [Helper],
  intercept: [{ x: Math.cos(ShooterAngle * D2R) * ShooterAt, z: Math.sin(ShooterAngle * D2R) * ShooterAt }],
};
// The Draw sword: a big one DrawReach cells out, nearest the bearing.
function drawTarget(list) {
  const b0 = DrawBearing * D2R, off = sw => { const a = Math.atan2(sw.z, sw.x) - b0; return Math.abs(Math.atan2(Math.sin(a), Math.cos(a))); };
  const pool = list.filter(sw => !sw.far && Big.includes(sw.w.name) && sw.d > DrawReach[0] && sw.d < DrawReach[1]);
  return (pool.length ? pool : list.filter(sw => !sw.far)).slice().sort((a, b) => off(a) - off(b))[0];
}
// A command's stand-ins, worked out once: the spots the world keeps clear, and for Draw the sword, picked
// in the world without the Draw raiders, who then stand by its path and are kept clear as well.
const casts = new Map();
function castFor(command) {
  if (casts.has(command)) return casts.get(command);
  const keep = [{ x: 0, z: 0 }, ...Spots[command]];
  let cast = { keep };
  if (command === 'draw') {
    const target = drawTarget(fieldList(World, keep)), to = flat(target, Hand), across = { x: -to.z, z: to.x };
    const foes = DrawFoes.map(([share, o]) => ({ x: target.x + (Hand.x - target.x) * share + across.x * o, z: target.z + (Hand.z - target.z) * share + across.z * o }));
    cast = { keep: [...keep, ...foes], target: target.seed, foes };
  }
  casts.set(command, cast);
  return cast;
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
function gathered(job, s, from, c, t) {
  const u = s - job.up, rise = Clear - from.tip.y;
  if (u < PullUp) return { b: raised(from, rise * smooth(u / PullUp)), up: true, aimed: -1 };
  const lifted = raised(from, rise), m = middle(lifted), settle = smooth((u - PullUp) / TurnTime);
  const centre = v3(m.x, lerp(m.y, Hover, settle) + Bob * settle * Math.sin(u * 7 + job.sw.seed), m.z);
  const foe = markAt(c, s - TrackLag, t), d = flat(centre, foe);
  const aim = flatAt(from.w, job.sw.size, centre, d, Math.min(30, Math.atan2(centre.y - HitHeight, d.d) / D2R));
  return { b: settle < 1 ? turn(lifted, aim, settle) : aim, up: false, aimed: u - PullUp - TurnTime };
}
// A Pin sword: pulled out, flying in low to point so that it arrives at `arrive`, driven in there
// leaning toward `toward`.
function lowShot(sw, c, point, arrive, toward, p) {
  const g = homeOf(sw, c), dir = flat(g, point), start = v3(g.x + dir.x * .2, PinHeight, g.z + dir.z * .2);
  const hit = v3(point.x - dir.x * .05, PinHeight, point.z - dir.z * .05), fly = Math.hypot(hit.x - start.x, hit.z - start.z) / p.speed;
  return { sw, kind: 'stick', pin: true, launch: arrive - fly - LiftTime, lift: LiftTime, dir, start, hit, fly, land: point, toward, lean: PinLean, buried: PinDepth };
}

// The plan for the chosen command: which swords go, when, and where, how long the run lasts, where the
// field's blades are split (cuts) and when each sword is paid for (spends). Only positions relative to the
// caster matter, so the clock can build it with the caster at 0, 0.
function plan(p, c, list, cast) {
  const all = list.filter(sw => !sw.far), home = sw => homeOf(sw, c);
  const nearest = q => all.slice().sort((a, b) => flat(home(a), q).d - flat(home(b), q).d);
  let pl;
  if (p.command === 'full open') {
    const t = volleyTimes(Math.min(Math.round(p.gathered), all.length)), foe = markAt(c, t.release, t);
    const picked = nearest(markAt(c, Order, t)).slice(0, t.n);
    const order = picked.map(sw => sw.seed).sort((a, b) => rand(a * 31) - rand(b * 31));
    const jobs = picked.map((sw, i) => {
      const k = order.indexOf(sw.seed);
      const job = { sw, kind: 'volley', up: Launch + i * GatherEvery, launch: t.release + (t.n > 1 ? Volley * k / (t.n - 1) : 0), lift: .05, lean: StickLean };
      job.hover = gathered(job, job.launch, standingPose(sw, c), c, t).b;
      const start = job.hover.tip, dir = flat(start, foe), hit = v3(foe.x - dir.x * .15, HitHeight, foe.z - dir.z * .15);
      const reach = .55 + rand(sw.seed * 29) * .6, side = (rand(sw.seed * 23) - .5) * .8;
      return Object.assign(job, { start, dir, hit, fly: Math.hypot(hit.x - start.x, hit.y - start.y, hit.z - start.z) / p.speed,
        land: { x: foe.x + dir.x * reach - dir.z * side, z: foe.z + dir.z * reach + dir.x * side }, toward: { x: -dir.x, z: -dir.z } });
    });
    const hitsAt = jobs.map(j => j.launch + j.lift + j.fly), last = Math.max(...hitsAt);
    pl = { t, foe, jobs, first: Math.min(...hitsAt), down: t.n >= DownAfter ? last + .05 : Infinity, end: last + 1.6 };
  } else if (p.command === 'pin') {
    const f = flat(PinFrom, { x: 0, z: 0 }), side = { x: -f.z, z: f.x };
    const walk = s => { const k = PinWalk * Math.max(0, s); return { x: c.x + PinFrom.x + f.x * k, z: c.z + PinFrom.z + f.z * k }; };
    const spot = (F, [along, across]) => ({ x: F.x + f.x * along + side.x * across, z: F.z + f.z * along + side.z * across });
    // out from the body: a leg's sword leans back past the feet and a little out, a sleeve's straight out
    const outward = ([along, across]) => { const k = Math.sign(across); return along < .2 ? flat({ x: 0, z: 0 }, { x: -f.x + side.x * k * .6, z: -f.z + side.z * k * .6 }) : { x: side.x * k, z: side.z * k }; };
    const picked = nearest(walk(Order)).slice(0, PinCost), g0 = home(picked[0]), at0 = walk(Launch);
    // the first pins the trouser leg on its own side, where the raider is when it arrives, and he stops there
    const leg = (g0.x - at0.x) * side.x + (g0.z - at0.z) * side.z >= 0 ? Pins[0] : Pins[1];
    let t1 = Launch + LiftTime + flat(g0, at0).d / p.speed, first;
    for (let i = 0; i < 5; i++) { first = lowShot(picked[0], c, spot(walk(t1), leg), t1, outward(leg), p); t1 = Launch + LiftTime + first.fly; }
    const F = walk(t1), rest = Pins.filter(q => q !== leg), jobs = [first];
    // the other three go to the other pins, each coming in from outside the body where it can
    const perms = a => a.length < 2 ? [a] : a.flatMap((x, i) => perms([...a.slice(0, i), ...a.slice(i + 1)]).map(r => [x, ...r]));
    const score = order => order.reduce((sum, q, k) => { const d = flat(home(picked[k + 1]), spot(F, q)), o = outward(q); return sum - d.x * o.x - d.z * o.z; }, 0);
    perms(rest).reduce((a, b) => score(b) > score(a) ? b : a).forEach((q, k) =>
      jobs.push(lowShot(picked[k + 1], c, spot(F, q), t1 + FallTime + .05 + PinGap * k, outward(q), p)));
    const last = Math.max(...jobs.map(j => j.launch + j.lift + j.fly));
    pl = { f, F, t1, walk, jobs, last, end: Math.max(4, last + 2.4) };
  } else if (p.command === 'draw') {
    const sw = all.find(q => q.seed === cast.target) ?? drawTarget(all);
    const g = home(sw), start = v3(g.x, DrawHeight, g.z), end = v3(c.x + Hand.x, .3, c.z + Hand.z), to = flat(g, end);
    const fly0 = Launch + TearTime, fly = Math.hypot(end.x - start.x, end.z - start.z) / DrawSpeed, caught = fly0 + fly;
    const reach = standingPose(sw, c).L / 2, across = { x: -to.z, z: to.x };
    // each raider: how far off the lane it stands, and when the spinning blade passes it
    const foes = cast.foes.map(q => {
      const at = { x: c.x + q.x, z: c.z + q.z }, rx = at.x - g.x, rz = at.z - g.z, share = (rx * to.x + rz * to.z) / to.d, off = rx * across.x + rz * across.z;
      return { x: at.x, z: at.z, off, pass: fly0 + fly * clamp(share), hit: Math.abs(off) <= reach && share > 0 && share < 1 };
    });
    pl = { g, to, across, reach, foes, caught, jobs: [{ sw, kind: 'caster', launch: Launch, fly0, fly, start, end }], end: caught + 1.3 };
  } else if (p.command === 'intercept') {
    const a = ShooterAngle * D2R, out = { x: Math.cos(a), z: Math.sin(a) }, shotDir = { x: -out.x, z: -out.z };
    const shooter = { x: c.x + out.x * ShooterAt, z: c.z + out.z * ShooterAt };
    const muzzle = { x: shooter.x + shotDir.x * .45, z: shooter.z + shotDir.z * .45 }, used = new Set(), toCaster = flat(muzzle, c).d;
    // each shot: where and when it ends, and the sword that meets it (none: it reaches the caster)
    const shots = Shots.map(shot => {
      const speed = shot.rocket ? RocketSpeed : BulletSpeed;
      let best = null;
      all.forEach(sw => {
        if (used.has(sw.seed)) return;
        const g = home(sw), along = (g.x - muzzle.x) * shotDir.x + (g.z - muzzle.z) * shotDir.z;
        // the meeting point lies on the shot's path, clear of the gun and at least 1.5 cells short of the caster
        if (along > toCaster - 1.5 || along < MinAlong) return;
        // it must reach the path before the shot does
        const foot = { x: muzzle.x + shotDir.x * along, z: muzzle.z + shotDir.z * along }, perp = flat(g, foot).d, meet = shot.t + along / speed;
        if (meet < shot.t + .02 + InterceptLift + perp / InterceptSpeed + .02) return;
        if (!best || perp < best.perp) best = { sw, foot, perp, meet };
      });
      // no sword can get there first: the shot is not stopped and no sword is spent
      if (!best) return { shot, speed, meet: shot.t + (toCaster - .2) / speed, hit: v3(muzzle.x + shotDir.x * (toCaster - .2), HitHeight, muzzle.z + shotDir.z * (toCaster - .2)), job: null };
      used.add(best.sw.seed);
      const g = home(best.sw), dir = flat(g, best.foot), launch = shot.t + .02, hit = v3(best.foot.x, HitHeight, best.foot.z);
      return { shot, speed, meet: best.meet, hit, job: { sw: best.sw, kind: 'meet', launch, lift: InterceptLift, dir, start: v3(g.x + dir.x * .1, .4, g.z + dir.z * .1),
        hit, fly: Math.max(.04, best.meet - launch - InterceptLift) } };
    });
    pl = { shooter, muzzle, shotDir, shots, jobs: shots.filter(q => q.job).map(q => q.job), end: 4 };
  } else {
    const ally = { x: c.x + Helper.x, z: c.z + Helper.z };
    pl = { ally, jobs: [{ sw: nearest(ally)[0], kind: 'arm', launch: Launch }], end: 3 };
  }
  pl.cuts = pl.jobs.flatMap(j => j.kind === 'volley' ? [c.z + j.sw.z, j.land.z] : [c.z + j.sw.z]);
  pl.spends = pl.jobs.map(j => j.kind === 'volley' ? j.up : j.launch).sort((a, b) => a - b);
  return pl;
}
// The plan with the caster at 0, 0, for the timeline.
const clock = p => { const cast = castFor(p.command); return plan(p, { x: 0, z: 0 }, fieldList(World, cast.keep), cast); };

// Where a sword of a 'stick', 'volley' or 'meet' job is at time s: turning onto its line, flying
// point first, then either dropping into the ground and quivering, or gone at the meeting. wobble
// adds to the lean of a stuck one.
function flight(job, s, from, wobble = 0) {
  const w = from.w, size = job.sw.size, t1 = job.launch + job.lift, t2 = t1 + job.fly;
  if (s < t1) return { b: blend(from, flying(w, size, job.start, job.dir), smooth((s - job.launch) / job.lift)), air: true };
  if (s < t2) return { b: flying(w, size, mix3(job.start, job.hit, (s - t1) / job.fly), job.dir), air: true };
  const age = s - t2;
  if (job.kind === 'meet') return { b: flying(w, size, job.hit, job.dir), air: true, met: age };
  const settle = smooth(age / .1), L = from.L;
  const lean = job.lean + wobble + Quiver * Math.exp(-age / .22) * Math.sin(age * QuiverHz * Math.PI * 2) * settle;
  const final = stuck(w, size, job.land, job.toward, lean, (job.buried ?? .2) * L * settle);
  return { b: settle < 1 ? blend(flying(w, size, job.hit, job.dir), final, settle) : final, air: settle < .5, landed: age, hitAt: t2 };
}

// The Caster Draw sword: torn straight up out of its hole, turned flat and spinning along the path to
// the caster's hand, then held by the caster standing at me. spinAt gives the flying pose at any
// time, for the blur copies.
function pulled(job, s, from, me) {
  const w = from.w, size = job.sw.size, rise = Clear - from.tip.y;
  const spinAt = x => {
    const a = (DrawSpin * 360 * (x - job.fly0) + job.sw.seed * 37) * D2R;
    return flatAt(w, size, mix3(job.start, job.end, clamp((x - job.fly0) / job.fly)), { x: Math.cos(a), z: Math.sin(a) });
  };
  if (s < job.fly0) return { b: raised(from, rise * smooth((s - job.launch) / TearTime)), up: true };
  if (s < job.fly0 + job.fly) {
    const u = (s - job.fly0) / TurnFlat;
    return { b: u < 1 ? turn(raised(from, rise), spinAt(s), smooth(u)) : spinAt(s), up: false, spinAt, blur: u >= 1 };
  }
  return { b: heldCopy(w, size * .85, me, HeldAngle), held: true, since: s - job.fly0 - job.fly };
}

// The Arm sword: slides up out of its hole, arcs to the ally's hand, is held there.
function armed(job, s, from, ally) {
  const w = from.w, age = s - job.launch, up = v3(from.tip.x + from.A.x * (from.L * .3 + .15), from.tip.y + from.A.y * (from.L * .3 + .15), from.tip.z + from.A.z * (from.L * .3 + .15));
  const lifted = pose(w, from.scale, up, from.A, from.B), held = heldCopy(w, job.sw.size * .85, ally, HeldAngle);
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

// The world's time left, under the caster: a lab stand-in for the readout on the caster's gizmo. It
// runs down 1 s a second and SwordCost at each sword the command uses, and the part a sword takes
// flashes as it goes. Marks every 5 s; the frame turns red at zero, where in game the world closes.
function timeBar(c, s, p, spends) {
  const full = Lasts[Math.round(p.verse) - 1], start = Math.min(p.left, full), W = 1.8, H = .09, k = W / full, x0 = c.x - W / 2, z = c.z - .62;
  const at = t => Math.max(0, start - t - spends.filter(q => q <= t).length * SwordCost), left = at(Math.max(0, s));
  const bar = (a, b, colour, layer, h = H) => { if (b > a) sprite({ x: (a + b) / 2, z }, b - a, h, colour, solid, layer); };
  bar(x0 - .025, x0 + W + .025, (left > 0 ? Ink : Empty).withAlpha(.75), Y + .08, H + .05);
  bar(x0, x0 + left * k, Trace.withAlpha(.85), Y + .081);
  spends.forEach(q => {
    const age = s - q, after = at(q);
    if (age >= 0 && age < .4) bar(x0 + after * k, x0 + Math.min(full, after + SwordCost) * k, TraceHot.withAlpha(1 - age / .4), Y + .082, H + .03);
  });
  for (let t = 5; t < full; t += 5) bar(x0 + t * k - .007, x0 + t * k + .007, Ink.withAlpha(.7), Y + .083);
}

export default {
  kit: 'Trace', label: 'Unlimited Blade Works: commands (pocket) (sketch)', scene: false,
  params: {
    command: { label: 'Command', value: 'full open', options: ['full open', 'pin', 'draw', 'arm', 'intercept'], group: 'Command' },
    gathered: P('Swords gathered (Full Open)', 16, 1, GatherCap, 1, 'Command'),
    speed: P('Sword flight, Full Open and Pin (cells/s)', 16, 6, 40, 1, 'Command'),
    verse: P('World from verse (20, 25, 30 s)', 1, 1, 3, 1, 'World time'),
    left: P('Time left at the order (s)', 16, 1, 30, .5, 'World time'),
    bar: { label: 'World time bar', value: true, group: 'World time' },
    actors: { label: 'Stand-ins and map edge', value: true, group: 'World time' },
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
    if (p.command === 'intercept') return [{ t: clock(p).shots.find(q => q.shot.rocket).meet, type: 'shake', value: .04 }];
    if (p.command === 'arm') return [];
    if (p.command === 'draw') return [{ t: Launch, type: 'shake', value: .02 }];
    const pl = clock(p);
    if (p.command === 'pin') return [{ t: pl.t1, type: 'shake', value: .02 }];
    return [{ t: pl.first, type: 'shake', value: .03 }];
  },

  draw(s, p, { origin: cell, scene }) {
    const c = { x: cell.x, z: cell.z + SceneNorth }, cast = castFor(p.command), pl = plan(p, c, fieldList(World, cast.keep), cast);
    if (s < 0 || s >= pl.end) return;
    const base = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const sun = { x: base.x * DuskShadow, z: base.z * DuskShadow }, tint = Color.Lerp(White, Tint, Look.twilight);
    const f = field(World, c, sun, cast.keep, { omit: pl.jobs.map(j => j.sw.seed), cuts: pl.cuts });

    // The world standing: ground, light, the gears' shadows. The field is drawn below, with the command's swords.
    backstop(c);
    floor(c, MapHalf + World.beyond + 8, 8, tint);
    patches(c, MapHalf + World.beyond, 40);
    sunGlow(c, sun, Look.twilight);
    hill(c, World.hill, sun, 1);
    skyGears('ubwc gears', c, s, sun, Look.gears);

    // The caster, pushed back a little by the catch; the pinned raider's struggle.
    const kick = p.command === 'draw' && s > pl.caught ? .1 * Math.min(1, (s - pl.caught) / .05) * Math.exp(-Math.max(0, s - pl.caught - .05) / .12) : 0;
    const me = kick ? { x: c.x + pl.to.x * kick, z: c.z + pl.to.z * kick } : c;
    const jerkAge = p.command === 'pin' && s > pl.last + .4 ? (s - pl.last - .4) % Jerk : -1;
    const jerk = jerkAge >= 0 && jerkAge < .22 ? .035 * Math.sin(jerkAge / .22 * Math.PI * 2) : 0;

    // The command's swords: standing in the field until they leave, then their holes, and the swords in
    // the air, stuck in the ground or held.
    const inserts = [], overhead = [], pinned = [], held = [], hits = [];
    pl.jobs.forEach(job => {
      const sw = job.sw, from = standingPose(sw, c), key = `ubwc ${sw.seed}`, leaves = job.kind === 'volley' ? job.up : job.launch;
      if (s < leaves) {
        inserts.push({ z: c.z + sw.z, fn: layer => {
          plant(key, cutOf(from, sw.sink), layer, sw.seed, sun, { cracks: 4, crumbs: 0, tint });
          blade(key, from, sun, strength, layer, { tint });
        } });
        return;
      }
      const hole = cutOf(from, sw.sink);
      plant(`${key} hole`, hole, buildingLayer - .01, sw.seed, sun, { cracks: 4, crumbs: 0, tint });
      if (job.kind !== 'arm') breakOut(`${key} out`, hole, s - leaves, sw.seed + 3, job.kind === 'caster' ? pl.to : null);
      if (job.kind === 'arm') {
        const st = armed(job, s, from, pl.ally);
        held.push(() => {
          blade(key, st.b, sun, strength, st.held ? pawnLayer + .01 : Y + .01, { upright: !st.held && s < job.launch + PullTime, tint });
          if (st.held && st.since < .6) blade(`${key} traced`, st.b, sun, strength, pawnLayer + .012, { fillTo: -1, wireTo: 9, wireAlpha: .8 * (1 - st.since / .6) });
          if (st.held && st.since < .3) glint(`${key} glint`, onScreen(st.b.tip), .35, Math.sin(st.since / .3 * Math.PI), TraceHot);
        });
        return;
      }
      if (job.kind === 'caster') {
        const st = pulled(job, s, from, me);
        if (st.held) held.push(() => {
          blade(key, st.b, sun, strength, pawnLayer + .01, { upright: false, tint });
          if (st.since < .6) blade(`${key} traced`, st.b, sun, strength, pawnLayer + .012, { fillTo: -1, wireTo: 9, wireAlpha: .8 * (1 - st.since / .6) });
          if (st.since < .12) glint(`${key} catch`, onScreen(v3(me.x + Hand.x, .3, me.z + Hand.z)), .45, 1 - st.since / .12, White);
        });
        else overhead.push({ z: onScreen(middle(st.b)).z, fn: layer => {
          if (st.blur) [[.024, .14], [.012, .32]].forEach(([lag, a]) => blade(`${key} blur ${lag}`, st.spinAt(s - lag), sun, strength, layer, { alpha: a, upright: false, shadow: 0, tint }));
          blade(key, st.b, sun, strength, layer, { upright: st.up, tint });
        } });
        return;
      }
      if (job.kind === 'volley' && s < job.launch) {
        const g = gathered(job, s, from, c, pl.t);
        overhead.push({ z: onScreen(middle(g.b)).z, fn: layer => {
          blade(key, g.b, sun, strength, layer, { upright: g.up, tint });
          if (g.aimed >= 0 && g.aimed < .2) glint(`${key} aimed`, onScreen(g.b.tip), .22, Math.sin(g.aimed / .2 * Math.PI), TraceHot);
        } });
        return;
      }
      const st = flight(job, s, job.kind === 'volley' ? job.hover : from, job.pin ? jerk * 60 : 0);
      if (st.met !== undefined) {
        // met the shot: the sword breaks into light where it stood in the path
        if (st.met < .35) overhead.push({ z: job.hit.z, fn: () => {
          if (st.met < .14) glint(`${key} spark`, onScreen(job.hit), .38, 1 - st.met / .14, White);
          shatter(key, st.b, sun, strength, st.met / .35, sw.seed, Y + .01, onScreen(job.hit));
        } });
        return;
      }
      if (st.air) overhead.push({ z: onScreen(middle(st.b)).z, fn: layer => blade(key, st.b, sun, strength, layer, { upright: false, tint }) });
      else {
        const b = st.b, age = st.landed, stick = layer => {
          const cut = cutOf(b, job.buried ?? .2);
          plant(`${key} stuck`, cut, layer, sw.seed + 500, sun, { grow: smooth(age / .1), forward: { x: job.dir.x, z: job.dir.z }, cracks: 5, crumbs: 2, tint });
          breakOut(`${key} kick`, cut, age, sw.seed, { x: job.dir.x, z: job.dir.z });
          blade(key, b, sun, strength, layer, { tint });
        };
        if (job.pin) pinned.push({ z: job.land.z, fn: stick });
        else inserts.push({ z: job.land.z, fn: stick });
      }
      if (st.hitAt !== undefined) hits.push({ at: job.hit, age: s - st.hitAt, key, pin: job.pin });
    });
    drawField(f, strength, tint, 1, inserts);
    haze(c, 1);
    embers('ubwc embers', c, s, 140, 1);
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
        strip('ubwc lane', edge(1), edge(-1), TraceHot.withAlpha(.07 * fade), light, Floor + .0085);
        [1, -1].forEach(k => line(`ubwc lane ${k}`, edge(k), .03, TraceHot.withAlpha(.35 * fade), light, Floor + .0088, 'none'));
        if (s < Launch + .15) {
          const f2 = s < Launch ? fade : 1 - smooth((s - Launch) / .15);
          line('ubwc order', [{ x: c.x, z: c.z + .25 }, a], .025, White.withAlpha(.55 * f2), solid, Y + .005, 'none');
          ringAt(a, .32, TraceHot.withAlpha(.7 * f2), Floor + .009, false, light);
        }
      }
      pl.foes.forEach((foe, i) => { const age = s - foe.pass; if (foe.hit && age >= 0 && age < .15) glint(`ubwc cut ${i}`, onScreen(v3(foe.x, HitHeight + .1, foe.z)), .34, 1 - age / .15, White); });
      // the traced knife the caster held: it breaks as the drawn sword arrives
      const knife = heldCopy(Weapons.Knife, Look.size * .85, me, HeldAngle), age = s - pl.caught;
      if (age < 0) {
        blade('ubwc knife', knife, sun, strength, pawnLayer + .01, { upright: false, tint });
        blade('ubwc knife wire', knife, sun, strength, pawnLayer + .012, { fillTo: -1, wireTo: 9, wireAlpha: .3 });
      } else shatter('ubwc knife', knife, sun, strength, age / .35, 77, pawnLayer + .012);
    }
    if (p.command === 'arm' && s >= Order && s < Launch + .15) {
      const g = homeOf(pl.jobs[0].sw, c), fade = 1 - smooth((s - Launch) / .15);
      line('ubwc order', [{ x: pl.ally.x, z: pl.ally.z + .25 }, g], .025, White.withAlpha(.55 * fade), solid, Y + .005, 'none');
      ringAt(g, .28, TraceHot.withAlpha(.7 * fade), Floor + .009, false, light);
    }
    if (p.command === 'intercept') pl.shots.forEach((q, k) => {
      const shot = q.shot, age = s - shot.t;
      if (age >= 0 && age < .1) glint(`ubwc muzzle ${k}`, onScreen(v3(pl.muzzle.x, HitHeight, pl.muzzle.z)), .3, 1 - age / .1, FireCore);
      if (age >= 0 && s < q.meet) {
        const d = age * q.speed, at = v3(pl.muzzle.x + pl.shotDir.x * d, HitHeight, pl.muzzle.z + pl.shotDir.z * d), tail = onScreen(v3(at.x - pl.shotDir.x * .45, HitHeight, at.z - pl.shotDir.z * .45));
        if (shot.rocket) {
          for (let i = 0; i < 8; i++) {
            const back = i * .25, pa = age - back / q.speed;
            if (pa < 0 || back > d) continue;
            const puff = onScreen(v3(at.x - pl.shotDir.x * back, HitHeight, at.z - pl.shotDir.z * back));
            sprite({ x: puff.x, z: puff.z + i * .03 }, .18 + i * .06, .16 + i * .05, Smoke.withAlpha(.45 * (1 - i / 8)), soft, Y + .012);
          }
          sprite(onScreen(at), .34, .14, Casing, soft, Y + .016, -Math.atan2(pl.shotDir.z, pl.shotDir.x) / D2R);
          sprite(tail, .3, .3, FireOuter.withAlpha(.8), glow, Y + .015);
        } else line(`ubwc bullet ${k}`, [tail, onScreen(at)], .05, FireCore.withAlpha(.95), light, Y + .015, 'none');
      }
      // a shot no sword could reach hits the caster
      if (!q.job && s >= q.meet && s < q.meet + .15) glint(`ubwc through ${k}`, onScreen(q.hit), .3, 1 - (s - q.meet) / .15, Hurt);
      const u = (s - q.meet) / .45;
      if (u >= 0 && u < 1 && shot.rocket) {
        const at = onScreen(q.hit);
        sprite(at, 2.4 * (.5 + u), 2 * (.5 + u), FireCore.withAlpha((1 - u) ** 2), glow, Y + .03);
        ringAt({ x: q.hit.x, z: q.hit.z }, .3 + 1.4 * smooth(u), FireOuter.withAlpha(.7 * (1 - u)), Floor + .009, true, light);
        for (let i = 0; i < 6; i++) {
          const a = i / 6 * Math.PI * 2;
          sprite({ x: at.x + Math.cos(a) * u * .8, z: at.z + Math.sin(a) * u * .6 + u * .2 }, .5 + u * .6, .4 + u * .5, Dust.withAlpha(.5 * (1 - u)), soft, Y + .025);
        }
      }
      if (shot.rocket && s >= q.meet) sprite({ x: q.hit.x, z: q.hit.z }, 1.3, .8, Black.withAlpha(.3 * smooth((s - q.meet) / .2)), soft, Floor + .0095);
    });

    if (p.bar) timeBar(me, s, p, pl.spends);
    if (!p.actors) return;
    mapEdge('ubwc edge', c);
    const raise = p.command === 'full open' ? smooth(clamp((s - Order) / .3)) * (1 - smooth(clamp((s - pl.t.release) / .12))) : 0;
    const flinch = list => list.some(h => h.age >= 0 && h.age < .18), hurt = (on, amount) => on ? { tint: Hurt, tintAmount: amount } : Warm;
    const shotThrough = p.command === 'intercept' && pl.shots.some(q => !q.job && s >= q.meet && s < q.meet + .18);
    pawn(me, Caster, sun, strength, { hair: true, arms: raise > 0 ? 1 : 0, raise, ...hurt(shotThrough, .6) });
    if (p.command === 'full open') {
      const at = markAt(c, s, pl.t), u = clamp((s - pl.down) / FallTime);
      figure('ubwc mark', at, flat(c, at), u * u, EnemyColour, sun, strength, hurt(flinch(hits), .6));
    }
    if (p.command === 'pin') {
      const u = clamp((s - pl.t1) / FallTime), fall = u * u;
      figure('ubwc pinned', pl.walk(Math.min(s, pl.t1)), pl.f, fall, EnemyColour, sun, strength,
        { ...hurt(flinch(hits), .35), limbs: smooth((fall - .5) / .5), jerk });
    }
    if (p.command === 'draw') pl.foes.forEach(foe => {
      const age = s - foe.pass, struck = foe.hit && age >= 0, push = struck ? .12 * smooth(Math.min(1, age / .12)) : 0, k = Math.sign(foe.off) || 1;
      pawn({ x: foe.x + pl.across.x * k * push, z: foe.z + pl.across.z * k * push }, EnemyColour, sun, strength, hurt(struck && age < .2, .6));
    });
    if (p.command === 'arm') pawn(pl.ally, Ally, sun, strength, Warm);
    if (p.command === 'intercept') {
      pawn(pl.shooter, EnemyColour, sun, strength, Warm);
      line('ubwc gun', [{ x: pl.shooter.x, z: pl.shooter.z + .2 }, { x: pl.muzzle.x, z: pl.muzzle.z + .2 }], .07, Casing, solid, pawnLayer + .01, 'none');
    }
  },
};
