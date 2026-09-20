// Spirit Bomb (Genki Dama) — technique proposal for the Goku kit, not the game. Nothing in
// Source/RimArt draws this yet. The kit's ultimate: the whole colony stops to feed one attack.
//
// What it is for (from the user's draft; every number is a placeholder).
//   Target an area up to 35 cells away. The caster raises both hands and channels with no upper
//   limit; a "Throw" gizmo ends the channel (minimum 3 s). A stun or a downing ends it with nothing.
//   The bomb has a power number. It starts at 0 and gains 1 per second from the caster alone.
//   While the caster channels, every other colonist on the map has a "Lend energy" gizmo. A lender
//   stops what it is doing, stands still with one hand up, and adds 1 power per second until the
//   throw or until told to stop.
//   Blast radius = 2 + 0.25 x power cells. Damage = 20 + 6 x power to every hostile pawn in the
//   radius, no upper limit. It only hurts evil: colonists, allies, animals, neutral pawns and every
//   structure in the radius take nothing. The blast ring on the floor is drawn at its true radius
//   and grows through the channel, so the player sees what a throw would cover right now.
//   The bomb flies slowly (1.4 s) and does not miss: it is an area order. Cooldown 1 day.
//
// Charge. charge = power at the throw / 30, capped at 1. The default run (6 s, 4 lenders) ends at
// 22.6 power, charge 0.75; 3 s alone is 3 power, charge 0.1. Sizes follow the power (ball, blast,
// dome). The weight of the impact follows the charge, from a small bomb to a full one:
//   grind 0.3 to 0.75 s; white frame 0.03 to 0.09 s; dome holds 0.35 to 1 s; camera shake 0.05 to
//   0.2; cracks 5 to 14; lightning veins 2 to 8; pillars 0 to 10; thrown rocks 5 to 32 and they
//   fly 0.5x to 1.2x as high; dust rings 1 or 2; column 2.5 + 1.2 x radius + up to 5 cells; burn
//   streaks 8 to 26; rings left across the flight path 2 to 7. While it is channelled: above charge
//   0.3 pebbles lift off the ground round the caster (up to 14) and wind rings run out from the
//   caster's feet; above 0.5 the camera trembles every 0.5 s. The numbers in the order below are
//   the default run's.
//
// Order, with the default timings and 4 lenders:
//   0.00  stand; colonists behind the caster; at the target 5 enemies, a colonist in melee with
//         them, an animal and a piece of colony wall, and 1 enemy outside the final radius
//   0.30  channel 6 s (a stand-in for "as long as the player likes"): both arms up. A small white-blue
//         ball forms high over the hands. Wisps of light leave the ground up to 10 cells round the
//         caster, rise and curve into it. The ball climbs as it grows (its centre is 2.2 + 1.6 x
//         radius cells up), so it never covers the caster. Blue light on the floor under it.
//         The ball is never still: a flame edge that licks round its rim, 3 tilting orbit lines that
//         make it read as a turning sphere, 4 arcs and a two-armed spiral turning on its face, short
//         lightning that crawls round the rim and re-rolls every 0.22 s, flashes where wisps land,
//         a halo ring shed every 0.9 s, motes circling it, a core that beats.
//   1.10  the lenders join one by one, 0.7 s apart: a hand goes up, the pawn glows, a ribbon of
//         light with beads running along it goes from the hand to the ball, more wisps rise. Each
//         join makes the ball surge: it swells 14% for 0.45 s, flashes, and throws a ring.
//   6.30  throw: over 0.25 s the ball draws back and up 0.5 cells, the arms come down, the ribbons
//         let go
//   6.55  flight 1.4 s, slow then fast. The ball stretches along its path (up to 16%), spins 3x
//         faster, carries a bow arc in front and a wavy tapered tail behind; 6 rings open across the
//         path where it passed, sparks fall behind it, and over the last 45% dust is blown out from
//         under it along the ground.
//   7.95  the grind, 0.6 s: the ball flattens against the ground and shakes; 16 sparks spray along
//         the floor; 12 jagged cracks of light run out (5 of them fork); 10 chunks of ground lift and
//         tremble; lines of light run inward from the blast edge; the floor inside the radius
//         strobes; camera shake every 0.1 s
//   8.55  detonation: a white frame over the whole radius for 0.07 s, big camera shake. A dome
//         opens to the blast radius in 0.4 s, fast then slow: 7 stacked levels, arcs turning on its
//         surface, 7 lightning veins from the top to the rim re-rolled every 0.1 s, a white front
//         on the true radius. A column of light 9 + radius cells tall stands in the middle with
//         rings climbing it; it tapers to a point. 9 thin pillars. 26 rocks are thrown up and out and land. Two dust
//         rings run out past the radius. Enemies inside go white and break into flecks that rise.
//         The colonist, the animal and the wall inside are outlined in blue and nothing happens
//         to them.
//   9.75  the dome lifts and thins over 1 s, the column narrows to a thread, sparkles rise
//  10.75  a scorch with a burnt edge and 22 radial burn streaks; the cracks still glow and cool
//         over 2 s; the 5 enemies inside are down, the one outside is not, the colonist, the animal
//         and the wall are as they were.
//
// Drawing: the ball is a sphere, the dome is a stack of level circles and the column's rings are
// level circles, so none needs a per-facing method. Height is drawn 0.6 cells north per cell up.
// This kit's rule is that effects rise from the ground; here the energy does (wisps, ribbons,
// cracks, rocks, column), and the ball itself hangs over the caster and is thrown, as in the
// source; it does not drop from the sky onto the target. Wisps, ribbons, arcs, cracks, lightning
// and the tail are strip meshes rebuilt while they show. Pawns, the animal and the wall are
// stand-ins. Shared shapes are in lib/goku.js.
import { Color, Mathf, Meshes, MeshPool } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import {
  Ki, KiDeep, KiSky, KiIce, White, Gi, Ally, Dust, Ink, EnemyColour, Lift, pawnLayer, pawn, ringAt, glint, line, strip, streak, whiteGlow, wallCell, smooth, clamp,
} from './lib/goku.js';

const disc = Meshes.disc(56, 'spirit bomb disc'), rim = Meshes.band(.95, 1, 56, 'spirit bomb rim'), orbit = Meshes.band(.93, 1, 56, 'spirit bomb orbit');
const lerp = Mathf.Lerp, TAU = Math.PI * 2;
// Decided values. The panel keeps only what is still being tuned.
const Lead = .3, Tail = 2, Swing = .25, Open = .4, Fade = 1, FullPower = 30;
const FirstLender = .8, LenderEvery = .7, BaseRadius = 2, StartSize = .25, Hang = 2.2, Climb = 1.6;
const Wisps = 16, WispsPerLender = 8, WispReach = 10, Levels = 7, Sparkles = 60;
const SurgeTime = .45, SurgeSwell = .14, Stretch = .16, FlightSpin = 3, TailSpan = .3, FallSparks = 16;
const GrindSparks = 16, Chunks = 10, Flecks = 9, MaxPebbles = 14;
// What follows the charge, as [small bomb, full bomb].
const GrindTime = [.3, .75], HoldTime = [.35, 1], WhiteTime = [.03, .09], Shake = [.05, .2], CrackCount = [5, 14], VeinCount = [2, 8], PillarCount = [0, 10];
const RockCount = [5, 32], RockHeight = [.5, 1.2], StreakCount = [8, 26], PathRingCount = [2, 7], ColumnExtra = 5;
const Animal = new Color(.62, .5, .33), Rock = new Color(.2, .17, .14);
// Orbit lines of the ball: [tilt speed, turn speed (degrees per second), starting angle].
const Orbits = [[1.1, 25, 0], [-1.5, -35, 60], [.8, 45, 120]];
// Lenders as [cells behind the caster, cells to the side].
const Lenders = [[2.5, 2], [3, -1.6], [1, -3.3], [1.2, 3.5], [4.6, .4], [4.2, 3.1]];
// At the target, as [cells east, cells north] of its centre.
const Foes = [[-.8, .6], [1.1, -.4], [.3, 1.9], [-1.9, -1.2], [2.4, 1.3]], Friend = [-.1, -.5], Beast = [1.6, -1.9], WallFrom = [-2.6, 1.6];

function times(p) {
  const cast = Lead, release = cast + p.channel, charge = Mathf.Clamp01(powerAt(release, p, { cast, release }) / FullPower), by = ([a, b]) => Mathf.Lerp(a, b, charge);
  const fly = release + Swing, hit = fly + p.fly, dome = hit + by(GrindTime), open = dome + Open, fade = open + by(HoldTime), gone = fade + Fade;
  return { cast, release, fly, hit, dome, open, fade, gone, end: gone + Tail, charge, by, count: range => Math.round(by(range)) };
}
const joins = (p, t, i) => t.cast + FirstLender + i * LenderEvery;
// Power at time s: 1 per second from the caster plus 1 per second from each lender since it joined.
function powerAt(s, p, t) {
  const now = Math.min(s, t.release);
  let power = Math.max(0, now - t.cast);
  for (let i = 0; i < p.lenders; i++) power += Math.max(0, now - joins(p, t, i)) * p.lend;
  return power;
}

// The ball. r is its radius in cells. look: v is the unit direction it is stretched along and
// stretch how much (negative flattens it), spin multiplies every turning speed, surge 0..1 is the
// swell of a lender joining.
function bomb(at, r, s, alpha, { v = { x: 1, z: 0 }, stretch = 0, spin = 1, surge = 0 } = {}) {
  if (r <= .01 || alpha <= 0) return;
  const beat = 1 + (.06 + .1 * surge) * Math.sin(s * 9), breathe = 1 + .05 * Math.sin(s * 3.1), turn = -Math.atan2(v.z, v.x) / Mathf.Deg2Rad;
  const sx = 1 + stretch, sz = 1 - .35 * stretch, time = s * spin;
  // A point of the round ball, stretched along v.
  const on = (dx, dz) => { const al = (dx * v.x + dz * v.z) * sx, ac = (-dx * v.z + dz * v.x) * sz; return { x: at.x + al * v.x - ac * v.z, z: at.z + al * v.z + ac * v.x }; };
  const round = (ang, rad) => on(Math.cos(ang) * rad, Math.sin(ang) * rad);

  sprite(at, r * 4.6 * breathe, r * 4.6 * breathe, Ki.withAlpha((.5 + .3 * surge) * alpha), glow, Y + .14);
  // The flame edge: an outline that licks in and out round the rim.
  const inner = [], outer = [], steps = 56;
  for (let i = 0; i <= steps; i++) {
    const ang = i / steps * TAU, lick = 1.07 + .06 * Math.sin(ang * 7 + time * 6) + .045 * Math.sin(ang * 13 - time * 9.5) + .03 * Math.sin(ang * 3 + time * 2.2) + .08 * surge;
    inner.push(round(ang, r * .9)); outer.push(round(ang, r * lick));
  }
  strip('spirit bomb flame', inner, outer, Ki.withAlpha(.75 * alpha), whiteGlow, Y + .1405);
  draw(disc, at.x, Y + .141, at.z, r * sx, r * sz, turn, KiSky.withAlpha(.92 * alpha));
  draw(disc, at.x, Y + .142, at.z, r * .86 * sx, r * .86 * sz, turn, KiIce.withAlpha(.9 * alpha));
  draw(rim, at.x, Y + .143, at.z, r * sx, r * sz, turn, Ki.withAlpha(alpha));
  // Orbit lines that tilt and turn: the ball reads as a sphere that is turning.
  Orbits.forEach(([tilt, speed, start], k) => {
    const q = Math.cos(time * tilt + k * 1.3), flat = Math.max(.08, Math.abs(q));
    draw(orbit, at.x, Y + .1435 + k * .0002, at.z, r * .97, r * .97 * flat, start + time * speed, KiSky.withAlpha((.35 + .4 * flat) * alpha));
  });
  [[.8, 40, 140], [.62, -65, 170], [.45, 90, 200], [.28, -130, 160]].forEach(([share, speed, span], k) => {
    const a0 = [], a1 = [], start = time * speed + k * 120, n = 14;
    for (let i = 0; i <= n; i++) {
      const u = i / n, ang = (start + span * u) * Mathf.Deg2Rad, w = Math.sin(u * Math.PI) * r * .06, d = r * share;
      a0.push(round(ang, d + w)); a1.push(round(ang, d - w));
    }
    strip(`spirit bomb arc ${k}`, a0, a1, White.withAlpha(.7 * alpha), undefined, Y + .144 + k * .001);
  });
  for (let arm = 0; arm < 2; arm++) {
    const pts = [];
    for (let i = 0; i <= 22; i++) { const u = i / 22; pts.push(round((u * 400 + time * 150 + arm * 180) * Mathf.Deg2Rad, r * (.12 + .78 * u))); }
    line(`spirit bomb spiral ${arm}`, pts, r * .09, White.withAlpha(.5 * alpha), whiteGlow, Y + .1485, 'both');
  }
  // Lightning that crawls round the rim. Each bolt lives 0.22 s and is then rolled again somewhere else.
  const bolts = 3 + Math.min(4, Math.floor(r * 2));
  for (let b = 0; b < bolts; b++) {
    const cycle = Math.floor(s / .22 + b * .37), seed = cycle * 31 + b * 7, from = rand(seed) * TAU, span = .6 + rand(seed + 1) * .7, pts = [];
    for (let j = 0; j <= 9; j++) pts.push(round(from + span * j / 9, r * (1.02 + .16 * rand(seed + 3 + j) * Math.sin(j / 9 * Math.PI))));
    line(`spirit bomb bolt ${b}`, pts, .035 + r * .02, White.withAlpha(.95 * alpha), whiteGlow, Y + .1495, 'both');
  }
  // Flashes on the face where energy lands.
  for (let i = 0; i < 6; i++) {
    const cycle = Math.floor(s / .3 + i * .41), u = (s / .3 + i * .41) % 1, seed = cycle * 17 + i * 5;
    sprite(round(rand(seed) * TAU, r * (.35 + .55 * rand(seed + 2))), r * .5 + .15, r * .5 + .15, White.withAlpha(.75 * (1 - u) * alpha), glow, Y + .1498);
  }
  draw(disc, at.x, Y + .15, at.z, r * .34 * beat, r * .34 * beat, 0, White.withAlpha(alpha));
  sprite(at, r * 1.5 * beat, r * 1.5 * beat, White.withAlpha(.85 * alpha), glow, Y + .151);
  // A halo ring leaves the rim every 0.9 s.
  for (let n = 0; n < 2; n++) { const u = (s / .9 + n / 2) % 1; ringAt(at, r * (1.05 + .7 * u), KiIce.withAlpha(.55 * (1 - u) * alpha), Y + .139, false, whiteGlow); }
  const motes = 6 + Math.min(8, Math.floor(r * 3));
  for (let i = 0; i < motes; i++) {
    const ang = time * (.9 + .5 * rand(i)) * (i % 2 ? 1 : -1) + i * 2.399, d = r * (1.2 + .25 * Math.sin(s * 3 + i));
    glint(`spirit bomb mote ${i}`, round(ang, d), .08 + r * .04, .8 * alpha, KiIce, 45);
  }
}

export default {
  kit: 'Goku', label: 'Spirit Bomb (sketch)',
  params: {
    lenders: P('Colonists lending energy', 4, 0, 6, 1, 'Showcase'),
    aim: P('Direction to the target (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Distance to the target (cells)', 16, 8, 30, 1, 'Showcase'),
    channel: P('Channel shown (the real one has no upper limit)', 6, 3, 15, .5, 'Timing (s)'),
    fly: P('Bomb is in the air', 1.4, .5, 3, .1, 'Timing (s)'),
    lend: P('Power per second per lender (the caster gives 1)', 1, .25, 3, .25, 'Rule'),
    blastPer: P('Blast radius per power (cells; it starts at 2)', .25, .05, .6, .05, 'Rule'),
    sizePer: P('Ball radius per power (cells)', .12, .04, .25, .01, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Stand', t: 0 }, { name: 'Channel', t: t.cast }, ...(p.lenders > 0 ? [{ name: 'Lenders join', t: joins(p, t, 0) }] : []),
    { name: 'Throw', t: t.release }, { name: 'Grind', t: t.hit }, { name: 'Detonation', t: t.dome }, { name: 'Dome lifts', t: t.fade }, { name: 'Aftermath', t: t.gone },
  ]; },
  events(p) {
    const t = times(p), list = [], big = t.by(Shake);
    // A heavy bomb makes the camera tremble while it is still over the caster's head.
    for (let at = t.cast + .5; at < t.release; at += .5) { const c = powerAt(at, p, t) / FullPower; if (c > .5) list.push({ t: at, type: 'shake', value: .012 + .02 * Math.min(1, c) }); }
    for (let at = t.hit; at < t.dome - .01; at += .1) list.push({ t: at, type: 'shake', value: big * (.15 + .2 * (at - t.hit) / (t.dome - t.hit)) });
    return [...list, { t: t.dome, type: 'shake', value: big }, { t: t.dome + .25, type: 'shake', value: big * .55 }, { t: t.dome + .5, type: 'shake', value: big * .3 }];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a);
    const caster = { x: o.x - ca * p.distance / 2, z: o.z - sa * p.distance / 2 }, target = { x: o.x + ca * p.distance / 2, z: o.z + sa * p.distance / 2 };
    const behind = (back, side) => ({ x: caster.x - back * ca - side * sa, z: caster.z - back * sa + side * ca });
    const near = ([x, z]) => ({ x: target.x + x, z: target.z + z });
    const up = (ground, h) => ({ x: ground.x, z: ground.z + h * Lift });
    const polar = (ang, d) => ({ x: target.x + Math.cos(ang) * d, z: target.z + Math.sin(ang) * d });

    const cracks = t.count(CrackCount), veins = t.count(VeinCount), pillars = t.count(PillarCount), rocks = t.count(RockCount), burns = t.count(StreakCount), pathRings = t.count(PathRingCount);
    const Grind = t.dome - t.hit, Hold = t.fade - t.open, WhiteFrame = t.by(WhiteTime), chargeNow = clamp(powerAt(s, p, t) / FullPower);
    const power = powerAt(s, p, t), r = StartSize + p.sizePer * power, blast = BaseRadius + p.blastPer * power;
    const channelling = s >= t.cast && s < t.release, formed = smooth((s - t.cast) / .5);
    // Where the ball is at w of the way through its flight: ground position and height.
    const hang = Hang + Climb * r;
    const path = w => { const e = w * w; return { ground: { x: lerp(caster.x, target.x, e), z: lerp(caster.z, target.z, e) }, h: lerp(hang, r * .5, e) }; };
    const seenAt = w => { const q = path(w); return up(q.ground, q.h); };
    const flight = clamp((s - t.fly) / p.fly), grind = clamp((s - t.hit) / Grind), sink = smooth(grind * 2.5);
    const windup = Math.sin(Math.PI * clamp((s - t.release) / Swing));
    const now = path(flight), ground = { x: now.ground.x - ca * .4 * windup, z: now.ground.z - sa * .4 * windup }, height = (now.h + .5 * windup) * (1 - .7 * sink);
    const shake = grind > 0 && grind < 1 ? { x: Math.sin(s * 97) * .05 * (.4 + grind), z: Math.sin(s * 131) * .03 * (.4 + grind) } : { x: 0, z: 0 };
    const ball = { x: ground.x + shake.x, z: ground.z + height * Lift + shake.z };
    const domeAge = s - t.dome, opened = clamp(domeAge / Open), R = blast * (1 - Math.pow(1 - opened, 3));
    const fading = smooth((s - t.fade) / Fade), domeAlpha = domeAge < 0 ? 0 : 1 - fading;
    // Surge: the swell each time a lender joins.
    let surge = 0;
    for (let i = 0; i < p.lenders; i++) { const age = s - joins(p, t, i); if (age >= 0 && age < SurgeTime && s < t.release) surge = Math.max(surge, Math.sin(Math.PI * age / SurgeTime)); }

    // --- the floor ------------------------------------------------------------------------------------------------------
    if (s >= t.cast && s < t.dome) {
      const pulse = .45 + .2 * Math.sin(s * 5), strobe = grind > 0 ? .5 + .5 * Math.sin(s * (30 + 40 * grind)) : 0;
      ringAt(target, blast, KiSky.withAlpha(Math.min(1, pulse + .4 * grind) * formed), Floor + .02);        // what a throw would cover right now
      draw(disc, target.x, Floor + .006, target.z, blast, blast, 0, KiDeep.withAlpha((.1 + .3 * grind * strobe) * formed), whiteGlow);
      const low = 1 - clamp(height / (hang + .01));
      sprite(ground, (r * 3 + 2) * (1 + low), (r * 3 + 2) * (.8 + low), Ki.withAlpha((.32 + .4 * low) * formed), glow, Floor + .008);   // light under the ball
    }
    // The scorch: a burnt edge, radial burn streaks, a dark middle. It stays.
    if (domeAge >= 0) {
      const burnt = smooth(domeAge / .8) * (1 - .35 * smooth((s - t.gone) / Tail));
      sprite(target, blast * 2.2, blast * 2.2, Ink.withAlpha(.34 * burnt), soft, Floor + .01);
      ringAt(target, blast * .98, Ink.withAlpha(.5 * burnt), Floor + .011, true);
      for (let i = 0; i < burns; i++) {
        const ang = i * TAU / burns + rand(i + 90) * .2, from = blast * (.25 + .2 * rand(i + 91)), to = blast * (.8 + .18 * rand(i + 92));
        streak(`spirit bomb burn ${i}`, polar(ang, from), polar(ang, to), .22 + .2 * rand(i + 93), Ink.withAlpha(.4 * burnt), undefined, Floor + .012, 5);
      }
    }
    // Jagged cracks of light. They grow through the grind, blaze in the blast, and cool afterwards.
    if (s >= t.hit) {
      const grown = domeAge >= 0 ? 1 : smooth(grind) * .75, heat = domeAge < 0 ? 1 : 1 - .85 * smooth((s - t.open) / (Hold + Fade + Tail * .8));
      for (let i = 0; i < cracks; i++) {
        const ang = i * TAU / cracks + rand(i + 3) * .35, reach = blast * (.5 + .45 * rand(i + 8)), nx = -Math.sin(ang), nz = Math.cos(ang), n = 8;
        const at = k => { const d = reach * k / n, off = k ? (rand(i * 13 + k) - .5) * .7 : 0; return { x: target.x + Math.cos(ang) * d + nx * off, z: target.z + Math.sin(ang) * d + nz * off }; };
        const pts = []; for (let k = 0; k <= n; k++) if (k / n <= grown) pts.push(at(k));
        line(`spirit bomb crack glow ${i}`, pts, .5, Ki.withAlpha(.6 * heat), whiteGlow, Floor + .03);
        line(`spirit bomb crack ${i}`, pts, .14, Color.Lerp(KiDeep, White, heat).withAlpha(.35 + .6 * heat), whiteGlow, Floor + .031);
        if (i % 5 < 2 && grown > .6) {   // a fork from the middle
          const root = at(4), turn = ang + (i % 2 ? .7 : -.7), fork = [root];
          for (let k = 1; k <= 4; k++) fork.push({ x: root.x + Math.cos(turn) * reach * .1 * k + (rand(i * 7 + k + 50) - .5) * .4, z: root.z + Math.sin(turn) * reach * .1 * k + (rand(i * 7 + k + 60) - .5) * .4 });
          line(`spirit bomb crack fork ${i}`, fork, .1, Color.Lerp(KiDeep, White, heat).withAlpha(.3 + .6 * heat), whiteGlow, Floor + .031);
        }
      }
    }
    // The grind: lines of light run inward along the floor, sparks spray out from under the ball.
    if (grind > 0 && grind < 1) {
      for (let i = 0; i < 14; i++) {
        const u = ((s - t.hit) * 2.6 + rand(i + 20)) % 1, ang = i * TAU / 14 + rand(i + 25) * .3, d0 = blast * (1 - u * .85), d1 = Math.min(blast, d0 + blast * .18);
        streak(`spirit bomb inward ${i}`, polar(ang, d0), polar(ang, d1), .09, KiIce.withAlpha(.8 * Math.sin(u * Math.PI) * grind), whiteGlow, Floor + .032, 3);
      }
      for (let i = 0; i < GrindSparks; i++) {
        const u = ((s - t.hit) * 5 + rand(i + 12)) % 1, ang = i * TAU / GrindSparks + s * 3 + rand(i) * .4, d0 = r * .7 + u * (1.5 + 2 * rand(i + 30)), d1 = d0 + .5 + rand(i + 31) * .8;
        streak(`spirit bomb grind spark ${i}`, polar(ang, d0), up(polar(ang + .12, d1), u * .5), .09, White.withAlpha(1 - u), whiteGlow, Y + .13, 3);
      }
    }

    // --- the colony wall inside the blast: it takes nothing -----------------------------------------------------------------
    const lit = domeAlpha * opened;
    for (let k = 0; k < 3; k++) {
      const c = near([WallFrom[0] + k, WallFrom[1]]);
      wallCell(c, 0);
      sprite(c, 1.5, 1.5, KiSky.withAlpha(.35 * lit), glow, Y + .01);
    }

    // --- pawns, north first ------------------------------------------------------------------------------------------------------
    const joined = [], figures = [{ pos: caster, caster: true }];
    Lenders.slice(0, p.lenders).forEach(([back, side], i) => {
      const at = behind(back, side), lending = smooth((s - joins(p, t, i)) / .3) * (1 - smooth((s - t.release) / .3));
      if (s >= joins(p, t, i) && s < t.release + .3) joined.push({ at, lending, i });
      figures.push({ pos: at, lender: true, lending });
    });
    // When the front of the dome passes a point d cells from the centre.
    const passes = d => t.dome + Open * (1 - Math.cbrt(Math.max(0, 1 - d / blast)));
    Foes.forEach((q, i) => figures.push({ pos: near(q), foe: true, d: Math.hypot(q[0], q[1]), i }));
    const outside = BaseRadius + p.blastPer * powerAt(t.release, p, t) + 2.5;                                  // 2.5 cells past the final blast
    figures.push({ pos: near([Math.cos(-.6) * outside, Math.sin(-.6) * outside]), foe: true, d: outside, i: 9 });
    figures.push({ pos: near(Friend), friend: true, d: Math.hypot(...Friend) });
    figures.push({ pos: near(Beast), beast: true, d: Math.hypot(...Beast) });
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      const inside = domeAge >= 0 && g.d <= blast && s >= passes(g.d), glowing = inside ? domeAlpha : 0;
      if (g.caster) {
        const raise = smooth((s - t.cast) / .35) * (1 - smooth((s - t.release) / Swing));
        pawn(g.pos, Gi, sun, strength, { hair: true, arms: 2, raise, tint: KiIce, tintAmount: .3 * raise + .3 * surge });
      } else if (g.lender) {
        pawn(g.pos, Ally, sun, strength, { arms: 1, raise: g.lending, tint: KiIce, tintAmount: .45 * g.lending });
        sprite({ x: g.pos.x, z: g.pos.z + .35 }, 1.3, 1.6, KiSky.withAlpha(.4 * g.lending * (.8 + .2 * Math.sin(s * 11))), glow, pawnLayer - .01);
      } else if (g.foe) {
        const since = s - passes(g.d);
        if (!(g.d <= blast) || since < 0) { pawn(g.pos, EnemyColour, sun, strength); return; }
        // It goes white, breaks into flecks that rise, and is lying there when the light has gone.
        pawn(g.pos, EnemyColour, sun, strength, { tint: White, tintAmount: clamp(since / .08), alpha: 1 - smooth((since - .15) / .45) });
        for (let k = 0; k < Flecks; k++) {
          const u = (since - .1 - k * .03) / (.7 + .4 * rand(g.i * 11 + k)); if (u < 0 || u > 1) continue;
          const at = { x: g.pos.x + (rand(g.i * 5 + k) - .5) * .5 + Math.sin(u * 4 + k) * .12, z: g.pos.z + rand(g.i * 3 + k + 40) * .7 + u * 1.6 };
          draw(MeshPool.plane10, at.x, Y + .17, at.z, .09 * (1 - u * .5), .09 * (1 - u * .5), u * 200 + k * 40, White.withAlpha(.95 * (1 - u)));
        }
        if (s >= t.fade) pawn(g.pos, EnemyColour, sun, strength, { lie: true, alpha: smooth((s - t.fade) / (Fade * .6)) });
      } else if (g.friend) {
        pawn(g.pos, Ally, sun, strength, { outline: glowing, tint: KiIce, tintAmount: .25 * glowing });
      } else {
        const b = g.pos;
        sprite({ x: b.x + sun.x * .2, z: b.z + sun.z * .2 }, .8, .35, Ink.withAlpha(strength), soft, Floor + .05);
        if (glowing > 0) { draw(disc, b.x, pawnLayer - .002, b.z + .14, .35, .23, 0, White.withAlpha(.8 * glowing)); draw(disc, b.x + .34, pawnLayer - .002, b.z + .26, .18, .17, 0, White.withAlpha(.8 * glowing)); }
        draw(disc, b.x, pawnLayer, b.z + .14, .3, .18, 0, Animal);
        draw(disc, b.x + .34, pawnLayer + .002, b.z + .26, .13, .12, 0, Animal);
      }
    });

    // --- a heavy bomb works on the ground under the caster: wind rings run out, pebbles lift and hang -----------------------------
    if (channelling && chargeNow > .3) {
      const heavy = clamp((chargeNow - .3) / .4);
      for (let n = 0; n < 3; n++) { const u = ((s - t.cast) / .7 + n / 3) % 1; ringAt(caster, .4 + u * (2 + 3 * heavy), KiIce.withAlpha(.5 * (1 - u) * heavy)); }
      const pebbles = Math.round(MaxPebbles * heavy);
      for (let i = 0; i < pebbles; i++) {
        const u = ((s - t.cast) * (.3 + .2 * rand(i + 2)) + rand(i + 1)) % 1, ang = i * 2.399, d = .7 + rand(i + 7) * 2.6, size = .06 + .07 * rand(i + 9), h = u * (.8 + 1.4 * rand(i + 4)), show = Math.sin(u * Math.PI);
        const foot = { x: caster.x + Math.cos(ang) * d, z: caster.z + Math.sin(ang) * d * .8 };
        sprite({ x: foot.x + sun.x * h, z: foot.z + sun.z * h }, size * 2.4, size * 1.4, Ink.withAlpha(.35 * show), soft, Floor + .05);
        draw(MeshPool.plane10, foot.x, Y + .005, foot.z + h * Lift, size, size * .8, i * 50 + s * 60, Rock.withAlpha(show));
      }
    }

    // --- energy rising into the ball: wisps from the ground, ribbons from the lenders ----------------------------------------------
    if (channelling) {
      const count = Wisps + WispsPerLender * joined.length, centre = up(caster, hang);
      for (let i = 0; i < count; i++) {
        const v = ((s - t.cast) * (.45 + .2 * rand(i + 50)) + rand(i)) % 1, ang = rand(i + 17) * TAU, far = 1.5 + rand(i + 31) * (WispReach - 1.5);
        const root = { x: caster.x + Math.cos(ang) * far, z: caster.z + Math.sin(ang) * far * .8 }, curl = (rand(i + 5) - .5) * 2.5;
        const point = w => { const e = w * w, q = up({ x: lerp(root.x, caster.x, e), z: lerp(root.z, caster.z, e) }, hang * Math.pow(w, 1.4)); return { x: q.x + Math.sin(w * Math.PI) * curl * .4, z: q.z }; };
        const head = point(v), tail = point(Math.max(0, v - .09)), show = Math.sin(v * Math.PI) * formed;
        streak(`spirit bomb wisp ${i}`, tail, head, .07, KiIce.withAlpha(.85 * show), whiteGlow, Y + .12, 3);
        sprite(head, .22, .22, White.withAlpha(.8 * show), glow, Y + .121);
        if (v < .15) sprite(root, .6, .4, KiSky.withAlpha(.5 * (1 - v / .15) * formed), glow, Floor + .03);   // where it left the ground
      }
      joined.forEach(({ at, lending, i }) => {
        const hand = { x: at.x + .22, z: at.z + .92 }, pts = [];
        const point = w => ({ x: lerp(hand.x, centre.x, w) + Math.sin(w * Math.PI) * (i % 2 ? .6 : -.6), z: lerp(hand.z, centre.z, smooth(w) * .6 + w * .4) });
        for (let j = 0; j <= 16; j++) pts.push(point(j / 16));
        line(`spirit bomb ribbon ${i}`, pts, .16, KiSky.withAlpha(.6 * lending), whiteGlow, Y + .11, 'none');
        for (let k = 0; k < 3; k++) sprite(point(((s - t.cast) * 1.1 + k / 3 + i * .17) % 1), .3, .3, White.withAlpha(.9 * lending), glow, Y + .111);
        sprite(hand, .4, .4, KiIce.withAlpha(.8 * lending), glow, Y + .112);
      });
      // Each lender that joins makes the ball throw a ring.
      for (let i = 0; i < p.lenders; i++) {
        const age = s - joins(p, t, i); if (age < 0 || age >= SurgeTime) continue;
        const u = age / SurgeTime;
        ringAt(centre, r * (1 + 1.6 * smooth(u)), White.withAlpha(.9 * (1 - u)), Y + .138, true, whiteGlow);
      }
    }

    // --- the ball in flight: rings left across its path, tail, falling sparks, bow arc, dust under it --------------------------------------
    const flying = flight > 0 && grind <= 0;
    const ahead = seenAt(Math.min(1, flight + .01)), before = seenAt(Math.max(0, flight - .01)), vl = Math.hypot(ahead.x - before.x, ahead.z - before.z) || 1;
    const v = flight > 0 ? { x: (ahead.x - before.x) / vl, z: (ahead.z - before.z) / vl } : { x: 0, z: -1 };
    if (flight > 0 && s < t.dome) {
      for (let n = 0; n < pathRings; n++) {
        const w = (n + .7) / (pathRings + .7), age = s - (t.fly + p.fly * w); if (age < 0 || age >= .55) continue;
        const u = age / .55, c = seenAt(w), e0 = seenAt(w - .01), e1 = seenAt(w + .01), deg = Math.atan2(e1.z - e0.z, e1.x - e0.x) / Mathf.Deg2Rad;
        draw(orbit, c.x, Y + .132, c.z, r * (.18 + .25 * u), r * (1.1 + .9 * u), -deg, KiIce.withAlpha(.8 * (1 - u)));
        draw(orbit, c.x, Y + .131, c.z, r * (.3 + .3 * u), r * (1.25 + 1 * u), -deg, Ki.withAlpha(.5 * (1 - u)), whiteGlow);
      }
      for (let i = 0; i < FallSparks; i++) {
        const w = (i + .5) / FallSparks, age = s - (t.fly + p.fly * w), life = .5 + .4 * rand(i + 70); if (age < 0 || age >= life) continue;
        const u = age / life, c = seenAt(w);
        glint(`spirit bomb fall spark ${i}`, { x: c.x + (rand(i + 71) - .5) * r * 1.6, z: c.z + (rand(i + 72) - .5) * r * 1.6 - u * .8 }, .1 + .08 * rand(i), 1 - u, KiIce, 45);
      }
    }
    if (flying) {
      const speed = clamp(flight * 3), pts = [], n = 16;
      for (let j = 0; j <= n; j++) {
        const w = Math.max(0, flight - TailSpan * flight * j / n), c = seenAt(w), wave = Math.sin(j * 1.1 - s * 28) * r * .12 * j / n;
        pts.push({ x: c.x - v.z * wave, z: c.z + v.x * wave });
      }
      line('spirit bomb tail glow', pts, r * 2.3, Ki.withAlpha(.5 * speed), whiteGlow, Y + .133);
      line('spirit bomb tail', pts, r * 1.2, KiIce.withAlpha(.55 * speed), whiteGlow, Y + .134);
      const bow = [], face = Math.atan2(v.z, v.x);
      for (let j = 0; j <= 12; j++) { const ang = face + (j / 12 - .5) * 1.9; bow.push({ x: ball.x + Math.cos(ang) * r * 1.35, z: ball.z + Math.sin(ang) * r * 1.35 }); }
      line('spirit bomb bow', bow, r * .16 + .04, White.withAlpha(.75 * speed), whiteGlow, Y + .153, 'both');
      // Dust blown out from under it once it is low.
      if (flight > .55) for (let i = 0; i < 14; i++) {
        const u = ((s - t.fly) * 2.2 + rand(i + 33)) % 1, side = i % 2 ? 1 : -1, d = r * .5 + u * (1.5 + rand(i + 34) * 1.5), back = rand(i + 35) * 1.5;
        const at = { x: ground.x - ca * back - sa * side * d, z: ground.z - sa * back + ca * side * d };
        sprite({ x: at.x, z: at.z + u * .3 }, .7 + u, .5 + u * .8, Dust.withAlpha(.45 * Math.sin(u * Math.PI) * clamp((flight - .55) / .2)), soft, Y + .005);
      }
    }
    if (s >= t.cast && s < t.dome + .12) {
      const swell = 1 + SurgeSwell * surge + .18 * grind + .05 * Math.sin(s * 60) * grind;
      const look = grind > 0 ? { v: { x: 0, z: -1 }, stretch: -.3 * sink, spin: FlightSpin + 3 * grind } : { v, stretch: Stretch * clamp(flight * 3), spin: 1 + (FlightSpin - 1) * clamp(flight * 3) + 1.5 * surge, surge };
      bomb(ball, r * formed * swell, s, 1 - clamp(domeAge / .12), look);
    }

    // --- chunks of ground that lift in the grind, and the rocks the blast throws ------------------------------------------------------------
    if (grind > 0 && domeAge < 0) for (let i = 0; i < Chunks; i++) {
      const ang = i * 2.399, d = r * .9 + rand(i + 44) * blast * .45, foot = polar(ang, d), h = smooth(grind * 1.4 - rand(i + 45) * .3) * (.5 + .7 * rand(i + 46)), size = .16 + .16 * rand(i + 47);
      sprite({ x: foot.x + sun.x * h, z: foot.z + sun.z * h }, size * 2.2, size * 1.2, Ink.withAlpha(.35), soft, Floor + .05);
      draw(MeshPool.plane10, foot.x + Math.sin(s * 70 + i) * .02, Y + .02, foot.z + h * Lift, size, size * .8, i * 50 + s * 40, Rock);
    }
    if (domeAge >= 0) for (let i = 0; i < rocks; i++) {
      const air = .9 + .9 * rand(i + 51), u = domeAge / air; if (u > 1.15) continue;
      const ang = rand(i + 52) * TAU, d = blast * (.15 + .5 * rand(i + 53)) + blast * .75 * Math.min(1, u), h = Math.max(0, (3 + 5 * rand(i + 54)) * t.by(RockHeight) * 4 * u * (1 - u)), size = .14 + .2 * rand(i + 55);
      const foot = polar(ang, d), gone = 1 - clamp((u - 1) / .15);
      sprite({ x: foot.x + sun.x * h, z: foot.z + sun.z * h }, size * 2.2, size * 1.2, Ink.withAlpha(.3 * gone), soft, Floor + .05);
      draw(MeshPool.plane10, foot.x, Y + .175, foot.z + h * Lift, size, size * .8, i * 47 + domeAge * (300 + 300 * rand(i + 56)), Rock.withAlpha(gone));
    }

    // --- the detonation ------------------------------------------------------------------------------------------------------------------------
    if (domeAge >= 0 && domeAlpha > 0) {
      const first = clamp(1 - domeAge / .4), rise = 1 + .6 * fading, thin = 1 - .25 * fading;
      if (domeAge < WhiteFrame * 2) sprite(target, blast * 3.4, blast * 3.4, White.withAlpha(domeAge < WhiteFrame ? 1 : 1 - (domeAge - WhiteFrame) / WhiteFrame), glow, Y + .19);   // soft-edged, so no hard white circle
      sprite(target, blast * 5, blast * 5, KiIce.withAlpha((.3 + .4 * t.charge) * first * first), glow, Y + .15);              // whiteout
      // The dome: level circles from the floor to the top, brighter toward the top. It lifts and thins as it fades.
      for (let k = 0; k < Levels; k++) {
        const tilt = k / Levels * 84 * Mathf.Deg2Rad, c = up(target, R * .6 * Math.sin(tilt) * rise), rad = R * Math.cos(tilt) * thin;
        draw(disc, c.x, Y + .151 + k * .001, c.z, rad, rad, 0, Color.Lerp(KiDeep, KiSky, k / (Levels - 1)).withAlpha(.24 * domeAlpha), whiteGlow);
        draw(rim, c.x, Y + .16 + k * .0005, c.z, rad, rad, 0, KiSky.withAlpha(.3 * domeAlpha * (1 - k / Levels)), whiteGlow);
        // An arc turning on each level: the surface is moving.
        const arc = [], start = s * (k % 2 ? 90 : -70) + k * 70;
        for (let j = 0; j <= 14; j++) { const ang = (start + 110 * j / 14) * Mathf.Deg2Rad; arc.push({ x: c.x + Math.cos(ang) * rad * .97, z: c.z + Math.sin(ang) * rad * .97 }); }
        line(`spirit bomb dome arc ${k}`, arc, .12 + R * .03, White.withAlpha(.65 * domeAlpha), whiteGlow, Y + .164, 'both');
      }
      // Lightning veins from the top of the dome down to its rim, rolled again every 0.1 s.
      for (let i = 0; i < veins; i++) {
        const seed = Math.floor(s / .1) * 19 + i * 5, az = i * TAU / veins + rand(seed) * .6 + s * .4, pts = [];
        for (let j = 0; j <= 9; j++) {
          const tilt = 84 * (1 - j / 9) * Mathf.Deg2Rad, wob = az + (rand(seed + j + 1) - .5) * .5, rad = R * Math.cos(tilt) * thin;
          pts.push(up({ x: target.x + Math.cos(wob) * rad, z: target.z + Math.sin(wob) * rad }, R * .6 * Math.sin(tilt) * rise));
        }
        line(`spirit bomb vein ${i}`, pts, .1 + R * .015, White.withAlpha(.9 * domeAlpha), whiteGlow, Y + .166, 'both');
      }
      ringAt(target, R, KiIce.withAlpha(.9 * domeAlpha), Y + .168, true, whiteGlow);                        // the front, on the true radius
      // The column of light in the middle, with rings climbing it.
      const tall = (2.5 + 1.2 * blast + ColumnExtra * t.charge) * smooth(domeAge / .25), girth = (1 - .85 * fading) * (.9 + .1 * Math.sin(s * 40)), spine = [];
      for (let j = 0; j <= 12; j++) spine.push(up(target, tall * j / 12));
      line('spirit bomb column glow', spine, blast * .8 * girth, Ki.withAlpha(.45 * domeAlpha), whiteGlow, Y + .17);
      line('spirit bomb column sheath', spine, blast * .42 * girth, KiIce.withAlpha(.6 * domeAlpha), whiteGlow, Y + .1705);
      line('spirit bomb column', spine, blast * .18 * girth, White.withAlpha(.95 * Math.min(1, domeAlpha * 1.5)), whiteGlow, Y + .171);
      for (let n = 0; n < 5; n++) {
        const u = (domeAge * .9 + n / 5) % 1, c = up(target, tall * u), rad = blast * .34 * Math.pow(1 - u, .6) * girth + .1;
        ringAt(c, rad, KiIce.withAlpha(.85 * Math.sin(u * Math.PI) * domeAlpha), Y + .172, false, whiteGlow);
      }
      for (let i = 0; i < pillars; i++) {
        const ang = i * 2.399, d = blast * (.3 + .6 * rand(i + 60)), foot = polar(ang, d);
        if (R < d) continue;
        const h = (3 + 4 * rand(i + 70)) * smooth((s - passes(d)) / .3) * (.8 + .2 * Math.sin(s * 30 + i));
        streak(`spirit bomb pillar glow ${i}`, foot, up(foot, h), .6, Ki.withAlpha(.4 * domeAlpha), whiteGlow, Y + .166, 8);
        streak(`spirit bomb pillar ${i}`, foot, up(foot, h), .18, White.withAlpha(.9 * domeAlpha), whiteGlow, Y + .167, 8);
      }
    }
    // Two dust rings run out past the radius. They are dust, not light: the light stops on the true radius.
    if (domeAge >= 0) for (let n = 0; n < (t.charge > .4 ? 2 : 1); n++) {
      const u = (domeAge - n * .18) / 1.1; if (u < 0 || u > 1) continue;
      const d = blast * (.9 + .9 * smooth(u));
      for (let i = 0; i < 26; i++) {
        const ang = i * TAU / 26 + rand(i + n * 30) * .2;
        sprite({ x: target.x + Math.cos(ang) * d, z: target.z + Math.sin(ang) * d + u * .5 }, 1.1 + u * 1.6, .85 + u * 1.2, Dust.withAlpha(.5 * Math.sin(u * Math.PI)), soft, Y + .005);
      }
    }

    // --- after: sparkles rise from the whole blast -----------------------------------------------------------------------------------------------
    if (s >= t.fade) for (let i = 0; i < Sparkles; i++) {
      const born = t.fade + rand(i + 3) * (Fade + .6), life = .9 + rand(i + 12) * .9, u = (s - born) / life; if (u < 0 || u > 1) continue;
      const at = up(polar(rand(i + 40) * TAU, blast * Math.sqrt(rand(i + 80))), u * (1.5 + 2.5 * rand(i + 9)));
      glint(`spirit bomb sparkle ${i}`, at, .09 + .06 * rand(i), .9 * Math.sin(u * Math.PI), KiIce, 45);
    }
  },
};
