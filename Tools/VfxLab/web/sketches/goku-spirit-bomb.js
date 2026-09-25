// Spirit Bomb (Genki Dama) — technique proposal for the Goku kit, not the game. The picture port in
// Source/RimArt/Goku/GokuSpiritBomb*.cs still draws the older detonation (a banded dome that lifts
// and thins, no burst). The kit's ultimate: the whole colony stops to feed one attack.
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
//   grind 0.3 to 0.75 s; white frame 0.03 to 0.09 s; dome holds 0.9 to 2 s before it bursts (the Dome stands slider is the full-charge value);
//   flecks in the burst 120 to 420; camera shake 0.05 to 0.2; cracks 5 to 14; pillars 0 to 10;
//   thrown rocks 5 to 32 and they fly 0.5x to 1.2x as high; dust rings 1 or 2;
//   column 2.5 + 1.9 x radius + up to 5 cells; burn streaks 8 to 26; rings left across the flight
//   path 2 to 7. While it is channelled: above charge
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
//   8.59  detonation: a white frame over the whole radius for 0.07 s, big camera shake. The ball
//         itself grows into the dome, from its size at the throw (2.96 cells) to the blast radius
//         in 0.4 s, fast then slow, and explodes. It keeps the ball's palette and parts (blue glow,
//         flame fringe, sky blue body, pale inside, white-hot middle, lightning), nothing turns and
//         everything pushes outward, and it is drawn as a half sphere with depth: its outline is
//         the floor circle to the south and bulges north where its top is; the white-hot middle
//         sits high; a deep blue limb, darker away from the sun, and a highlight toward the sun
//         near the top shade it; 16 rays of light run down its curved surface from the top to the
//         floor, 0.45 s each; its edge is a ring of flame, a row of 60 pointed flame tips that each
//         rise, flicker and surge out every 0.28 to 0.4 s (taller on the top edge, where flames
//         rise), in three nested layers from faint blue outside to white inside, with wisps that
//         break off the tips; the middle beats 14 times a second. On the floor round it, hidden behind it on the far side:
//         the shock rings of its pulses, 7 low jagged discharges of lightning, 20 sparks. The flame
//         edge starts on the true radius. It hides what is inside. A column of light comes out of its
//         top (the part inside is under the dome's body): 16 overlapping soft sprites that ripple
//         in width and dim with height, so it has no outline and no tip; streaks run up through
//         it, rings climb it, a pool of light at its foot. 8 thin pillars drawn the same way, also
//         hidden inside the dome. 25 rocks (irregular lumps, a few big and many small) are thrown
//         up and out with dust behind them, seen only once they fly out of the dome, land with a
//         puff and stay as rubble. Two dust rings run out past the radius. Under the dome each
//         enemy the front reaches turns white-hot and falls 0.3 s later: the damage lands at the
//         hit, and the body stays.
//   8.99  the dome holds for 1.73 s and beats like a heart: 5 pulses (9.03, 9.49, 9.94, 10.31,
//         10.63), the gaps shrinking toward the burst and the pulses growing from 2.5% to 4% of the
//         radius. Everything the dome does runs on its own clock at the Dome animation speed
//         (default 0.6): the times below for its parts are at speed 1 and take 1.7 times as long at
//         0.6, and the heartbeat's gaps go from 0.3 s toward 0.12 s at speed 1. Each pulse kicks the dome out in 0.05 s and lets it ease back, flashes it white,
//         sends a soft band of light down its surface from the top to the floor in 0.15 s, and
//         when the band lands throws a shock ring out to 1.45 radii, makes the flames flare and
//         shakes the camera. Between pulses two slow bulges travel round its outline. Soft blobs
//         of light boil up its surface (0.6 s each); 4 bolts of lightning crawl over it (0.12 s
//         each); 24 embers rise off its top. Over the last third of the hold it swells 4%,
//         brightens and its flames surge harder. The cracks under it stay hidden until it bursts.
//  10.72  burst: a soft flash and a small shake. The dome swells 5% and is gone in 0.25 s, broken
//         into 346 flecks of light, a little over half from its surface and the rest from inside
//         it, that drift up and a little out, twinkle, and fade over 1.1 to 2 s, over a thin haze
//         that rises and is gone in 1.2 s. The column narrows to a thread over 1 s. The scorch
//         burns in as the light clears, and the colonist, the animal and the wall inside stand in
//         blue shells for 0.5 s: nothing happened to them.
//  12.72  the scorch: a ragged burnt edge of soft blotches with a few gaps, uneven blackening darkest
//         at the middle, and 22 blast streaks of uneven spacing, length and width, some past the
//         edge, with thin smoke rising off it until about 12.7 s; the cracks still glow and cool
//         over 2 s; the 5 enemies inside are down, the one outside is not, the colonist, the animal
//         and the wall are as they were.
//
// Source for the detonation: the anime and the games draw the explosion as the ball itself grown
// big, a white middle inside a blue body with a deeper rim (Frieza fight, Kid Buu, FighterZ);
// Sparking Zero bursts it into a cloud of flecks. The dome is not the ball copied 1:1: the ball's
// turning parts (orbit lines, spiral, circling motes) do not fit an explosion.
//
// Drawing: the ball is a sphere; the dome (blastDome) is a half sphere, a fan mesh of its outline
// built once plus sprites and strips placed on it; the flecks are quads placed on the same half
// sphere and the column's rings are level circles, so none needs a per-facing method. Height is
// drawn 0.6 cells north per cell up; anything inside the dome is drawn under its body.
// This kit's rule is that effects rise from the ground; here the energy does (wisps, ribbons,
// cracks, rocks, column), and the ball itself hangs over the caster and is thrown, as in the
// source; it does not drop from the sky onto the target. Wisps, ribbons, arcs, cracks, lightning
// and the tail are strip meshes rebuilt while they show. Pawns, the animal and the wall are
// stand-ins. Shared shapes are in lib/goku.js.
import { Color, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../js/engine.js';
import { draw, mesh } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import {
  Ki, KiDeep, KiSky, KiIce, White, Gi, Ally, Dust, Ink, Skin, EnemyColour, Lift, pawnLayer, pawn, rock, ringAt, glint, line, strip, streak, whiteGlow, wallCell, smooth, clamp,
} from './lib/goku.js';

const disc = Meshes.disc(56, 'spirit bomb disc'), rim = Meshes.band(.95, 1, 56, 'spirit bomb rim'), orbit = Meshes.band(.93, 1, 56, 'spirit bomb orbit');
const lerp = Mathf.Lerp, TAU = Math.PI * 2;
// The dome's outline at radius 1 round its centre, as drawn: for an outward direction az the edge lies on the level circle
// atan(Lift x sin az) up, and facing south on the floor circle. Built once; the body is a fan of it, churned and scaled.
const OutlineParts = 96;
const Outline = Array.from({ length: OutlineParts }, (_, i) => {
  const az = i / OutlineParts * TAU, el = Math.atan(Lift * Math.max(0, Math.sin(az)));
  return { x: Math.cos(az) * Math.cos(el), z: Math.sin(az) * Math.cos(el) + Lift * Math.sin(el) };
});
// Decided values. The panel keeps only what is still being tuned.
const Lead = .3, Tail = 2, Swing = .25, Open = .4, Fade = 1, FullPower = 30;
const FirstLender = .8, LenderEvery = .7, BaseRadius = 2, StartSize = .25, Hang = 2.2, Climb = 1.6;
const Wisps = 16, WispsPerLender = 8, WispReach = 10;
const SurgeTime = .45, SurgeSwell = .14, Stretch = .16, FlightSpin = 3, TailSpan = .3, FallSparks = 16;
const GrindSparks = 16, Chunks = 10, MaxPebbles = 14;
// The dome is the ball grown to the blast and exploding: its radius as a share of the blast radius (its fringe reaches
// about 1.07 of its radius, so the fringe lands on the true radius), its rays, boiling blobs and flame tips, how long the burst
// takes to clear it and how much it swells, and how long the flecks last at most.
const DomeFill = .93, DomeRays = 16, DomeBoils = 18, FlameTips = 60, Pop = .25, PopSwell = .05, Scatter = 2;
// The dome's heartbeat: the gap between pulses at the start of the hold and just before the burst.
const PulseGap = [.3, .12];
// A hit enemy falls this long after the front passes it.
const Fall = .3;
// What follows the charge, as [small bomb, full bomb].
// How long a small bomb's dome stands, as a share of a full one's (the "Dome stands" slider).
const HoldShare = .45;
const GrindTime = [.3, .75], WhiteTime = [.03, .09], Shake = [.05, .2], CrackCount = [5, 14], PillarCount = [0, 10];
const RockCount = [5, 32], RockHeight = [.5, 1.2], StreakCount = [8, 26], PathRingCount = [2, 7], ColumnExtra = 5, FleckCount = [120, 420];
const Animal = new Color(.62, .5, .33), Smoke = new Color(.28, .28, .3);
// The mod's noisy soft spot, for burnt blotches and smoke.
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
// The scorch: patches of blackening inside it and blotches round its ragged edge.
const ScorchPatches = 16, ScorchEdge = 40;
// Orbit lines of the ball: [tilt speed, turn speed (degrees per second), starting angle].
const Orbits = [[1.1, 25, 0], [-1.5, -35, 60], [.8, 45, 120]];
// Lenders as [cells behind the caster, cells to the side].
const Lenders = [[2.5, 2], [3, -1.6], [1, -3.3], [1.2, 3.5], [4.6, .4], [4.2, 3.1]];
// At the target, as [cells east, cells north] of its centre.
const Foes = [[-.8, .6], [1.1, -.4], [.3, 1.9], [-1.9, -1.2], [2.4, 1.3]], Friend = [-.1, -.5], Beast = [1.6, -1.9], WallFrom = [-2.6, 1.6];

function times(p) {
  const cast = Lead, release = cast + p.channel, charge = Mathf.Clamp01(powerAt(release, p, { cast, release }) / FullPower), by = ([a, b]) => Mathf.Lerp(a, b, charge);
  const fly = release + Swing, hit = fly + p.fly, dome = hit + by(GrindTime), open = dome + Open, burst = open + by([HoldShare * p.hold, p.hold]), gone = burst + Scatter;
  return { cast, release, fly, hit, dome, open, burst, gone, end: gone + Tail, charge, by, count: range => Math.round(by(range)) };
}
const joins = (p, t, i) => t.cast + FirstLender + i * LenderEvery;
// Power at time s: 1 per second from the caster plus 1 per second from each lender since it joined.
function powerAt(s, p, t) {
  const now = Math.min(s, t.release);
  let power = Math.max(0, now - t.cast);
  for (let i = 0; i < p.lenders; i++) power += Math.max(0, now - joins(p, t, i)) * p.lend;
  return power;
}

// The dome's heartbeat: pulses from when it has opened until it bursts, as [{ t, strength, progress }]. The gaps shrink from
// PulseGap[0] to PulseGap[1] toward the burst (divided by the dome's animation speed, pace), each a little uneven, and the pulses get stronger (2.5% to 4% of the radius).
function domePulses(t, pace) {
  const list = [], span = t.burst - t.open;
  for (let at = t.open + .04, n = 0; at < t.burst - .03 && n < 40; n++) {
    const progress = (at - t.open) / span;
    list.push({ t: at, strength: (.025 + .015 * progress) * (.8 + .4 * rand(n + 400)), progress });
    at += Mathf.Lerp(PulseGap[0], PulseGap[1], progress * progress) * (.85 + .3 * rand(n + 410)) / pace;
  }
  return list;
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

// The dome: the ball in its explosion, as a half sphere standing on the floor. The ball's palette and parts (blue glow,
// flame fringe, sky blue body, pale inside, white-hot middle, lightning), with nothing turning and everything pushing
// outward, drawn so it has depth: its outline is the half sphere's (the floor circle to the south, bulging north where its
// top is), it is shaded (a deep blue limb, darker away from the sun, and a highlight toward the sun near the top), rays of
// light run down its curved surface from the top to the floor, and what is on the floor behind it (lightning, shock rings,
// sparks) is drawn under its body so it hides. While it stands it beats like a heart: each pulse in beats ({ age,
// strength }, from domePulses) kicks it out in 0.05 s and lets it ease back, sends a bright band down its surface to the
// floor in 0.15 s, and when the band lands throws a shock ring and makes its flames flare. Between pulses two slow bulges
// travel round its outline, so it is never a perfect circle. build 0..1 is how far the hold has gone (in the last third,
// the rush, it swells, brightens and its flames surge); sun is where shadows fall.
function blastDome(at, r, s, alpha, build, sun, beats, pace) {
  if (r <= .01 || alpha <= 0) return;
  // Everything the dome does runs on its own clock, pace times real time, so one slider sets how fast it all moves.
  s *= pace;
  beats = beats.map(b => ({ age: b.age * pace, strength: b.strength }));
  // A pulse kicks out over 0.05 s and eases back; its band lands on the floor at 0.15 s, and the flare it starts then dies away.
  const kick = age => age < 0 ? 0 : age < .05 ? smooth(age / .05) : Math.exp(-(age - .05) / .12);
  const landed = age => age < .15 ? 0 : Math.exp(-(age - .15) / .12);
  const rush = build * build * build, push = beats.reduce((sum, b) => sum + b.strength * kick(b.age), 0);
  const flash = beats.reduce((most, b) => Math.max(most, kick(b.age)), 0), flare = beats.reduce((sum, b) => sum + landed(b.age), 0);
  r *= 1 + push + .04 * rush;
  // The outline's slow churn: two bulges travelling round it, 1% and 0.7% of the radius.
  const churn = az => 1 + .01 * Math.sin(2 * az - s * 3) + .007 * Math.sin(3 * az + s * 4.3);
  const beat = 1 + (.07 + .05 * build) * Math.sin(s * 14), floor = (ang, rad) => ({ x: at.x + Math.cos(ang) * rad, z: at.z + Math.sin(ang) * rad });
  // A point on the half sphere as drawn: az the direction from the centre, el the angle up from the floor.
  const on = (az, el, rad = r) => ({ x: at.x + Math.cos(az) * rad * Math.cos(el), z: at.z + Math.sin(az) * rad * Math.cos(el) + Lift * rad * Math.sin(el) });
  // The lowest angle up at which the surface faces the viewer: 0 facing south, up to the outline facing north.
  const low = az => Math.atan(Lift * Math.max(0, Math.sin(az))), mid = on(0, Math.PI / 2, r * .42), sunAz = Math.atan2(-sun.z, -sun.x);
  sprite(mid, r * 3.2, r * 3.2, Ki.withAlpha((.5 + .2 * flash + .25 * rush) * alpha), glow, Y + .1385);
  // On the floor, behind the body where the dome covers it: the shock ring each pulse throws when its band lands (out to
  // 1.45 radii in 0.5 s), lightning jumping out from the foot and back (0.16 s each), sparks thrown off (0.5 s each).
  beats.forEach(b => { const u = (b.age - .15) / .5; if (u >= 0 && u < 1) ringAt(at, r * (1.02 + .43 * smooth(u)), KiIce.withAlpha(.75 * (1 - u) * alpha), Y + .139, false, whiteGlow); });
  for (let b = 0; b < 7; b++) {
    const cycle = Math.floor(s / .16 + b * .37), seed = cycle * 31 + b * 7, from = rand(seed) * TAU, span = .12 + rand(seed + 1) * .2, lift = .05 + .12 * rand(seed + 2), pts = [];
    for (let j = 0; j <= 12; j++) { const k = j / 12; pts.push(floor(from + span * k, r * (1 + lift * Math.sin(k * Math.PI) + .06 * (rand(seed + 3 + j) - .5) * Math.sin(k * Math.PI)))); }
    line(`spirit bomb dome bolt ${b}`, pts, .05 + r * .012, White.withAlpha(.95 * alpha), whiteGlow, Y + .1395, 'both');
  }
  for (let i = 0; i < 20; i++) {
    const phase = s / .5 + rand(i + 40), cycle = Math.floor(phase), u = phase - cycle;
    glint(`spirit bomb dome spark ${i}`, floor(rand(cycle * 13 + i) * TAU, r * (.98 + .35 * u)), .1 + .06 * rand(i), .9 * (1 - u) * alpha, KiIce, 45);
  }
  // The flame edge: one ring of flame round the outline, so it never breaks into dots. Its outer edge is a row of pointed
  // flame tips; each tip rises and falls on its own, flickers, surges out once every 0.28 to 0.4 s, and is up to 1.6 times
  // taller on the top edge, where flames rise. It is three nested strips, the widest and faintest blue outside and narrower
  // and whiter inside, over a soft blue glow, so it is light and not a flat band. At the top of each surge a wisp breaks off
  // the tip and drifts out and up. The strips start just inside the edge, under the body, so the flame comes from behind it.
  for (let i = 0; i < OutlineParts; i += 2) { const q = Outline[i], k = r * churn(i / OutlineParts * TAU); sprite({ x: at.x + q.x * k, z: at.z + q.z * k }, r * .24, r * .24, Ki.withAlpha(.24 * alpha), glow, Y + .1404); }
  const tips = [];
  for (let k = 0; k < FlameTips; k++) {
    const phase = s / (.28 + .12 * rand(k + 71)) + rand(k + 72), u = phase - Math.floor(phase), flicker = .7 + .3 * Math.sin(s * (19 + 7 * rand(k + 73)) + k * 2.1);
    const ang = (k + .5 + (rand(k + 70) - .5) * .8) / FlameTips * TAU;
    tips.push({ ang, u, h: (.025 + .05 * rand(k + 74)) * flicker * (1 + .8 * Math.sin(u * Math.PI)) * (1 + .6 * Math.max(0, Math.sin(ang))) * (1 + .9 * flare + .5 * rush) });
  }
  // A point of the churning outline at any angle, as drawn, at k radii.
  const rimAt = (ang, k) => { const el = Math.atan(Lift * Math.max(0, Math.sin(ang))); k *= churn(ang); return { x: at.x + Math.cos(ang) * Math.cos(el) * r * k, z: at.z + (Math.sin(ang) * Math.cos(el) + Lift * Math.sin(el)) * r * k }; };
  // How far the flame reaches past the edge at an angle: the tallest tip there, each a point with hollow sides, so neighbours
  // leave a dip between them.
  const half = TAU / FlameTips * .9, steps = 360, reach = [];
  for (let i = 0; i <= steps; i++) {
    const ang = i / steps * TAU; let h = 0;
    tips.forEach(p => { const d = Math.abs(Math.atan2(Math.sin(ang - p.ang), Math.cos(ang - p.ang))) / half; if (d < 1) h = Math.max(h, p.h * Math.pow(1 - d, 1.8)); });
    reach.push(h);
  }
  [[1, Ki, .35], [.6, KiSky, .45], [.3, White, .5]].forEach(([share, colour, a], j) => {
    const inner = [], outer = [];
    for (let i = 0; i <= steps; i++) { const ang = i / steps * TAU; inner.push(rimAt(ang, .97)); outer.push(rimAt(ang, 1.01 + reach[i] * share)); }
    strip(`spirit bomb dome flame ${j}`, inner, outer, colour.withAlpha(a * alpha), whiteGlow, Y + .1405 + j * .0002);
  });
  tips.forEach((p, k) => {
    if (p.u <= .5) return;
    const v = (p.u - .5) / .5, from = rimAt(p.ang, 1.01 + p.h), to = rimAt(p.ang, 1.01 + p.h * 1.8), size = r * (.07 - .035 * v);
    sprite({ x: lerp(from.x, to.x, v), z: lerp(from.z, to.z, v) + .4 * v }, size, size, KiIce.withAlpha(.6 * (1 - v) * alpha), glow, Y + .1407);
  });
  // The body: the half sphere's churning outline filled, one fan built again each frame, opaque so nothing under it shows
  // through; then the pale inside as a soft gradient round its middle, then the limb: deep blue just inside the outline,
  // darkest on the side away from the sun.
  const body = mesh('spirit bomb dome body'), vertices = [at.x, at.z + Lift * .35 * r], tri = [];
  Outline.forEach((q, i) => { const k = r * churn(i / OutlineParts * TAU); vertices.push(at.x + q.x * k, at.z + q.z * k); tri.push(0, 1 + i, 1 + (i + 1) % OutlineParts); });
  body.setFlat(vertices, tri);
  draw(body, 0, Y + .141, 0, 1, 1, 0, KiSky.withAlpha(alpha));
  sprite(mid, r * 1.8, r * 1.7, KiIce.withAlpha(.8 * alpha), soft, Y + .1415);
  for (let i = 0; i < OutlineParts; i++) {
    const q = Outline[i], shade = .5 - .5 * Math.cos(i / OutlineParts * TAU - sunAz), k = r * .9 * churn(i / OutlineParts * TAU);
    sprite({ x: at.x + q.x * k, z: at.z + q.z * k }, r * .34, r * .34, KiDeep.withAlpha((.05 + .16 * shade) * alpha), soft, Y + .142);
  }
  // Rays of light run down the surface from near the top to the floor, over and over, curving with it.
  for (let i = 0; i < DomeRays; i++) {
    const phase = s / .45 + rand(i + 5), cycle = Math.floor(phase), u = phase - cycle, az = (i + (rand(cycle * 7 + i) - .5) * .6) / DomeRays * TAU;
    const head = Mathf.Lerp(1.45, low(az), u), tail = Math.min(1.45, head + .5), pts = [];
    for (let j = 0; j <= 8; j++) pts.push(on(az, Mathf.Lerp(tail, head, j / 8)));
    line(`spirit bomb dome ray ${i}`, pts, r * .02 + .04, White.withAlpha(.5 * Math.sin(u * Math.PI) * alpha), whiteGlow, Y + .143, 'both');
  }
  // Each pulse sends a bright band down the surface, a level circle from near the top to the floor in 0.15 s, drawn where
  // it faces the viewer: the whole circle up high, only the near side lower down.
  beats.forEach((b, n) => {
    if (b.age > .2) return;
    const el = Mathf.Lerp(1.45, .02, b.age / .15), k = Math.tan(el) / Lift, hw = k >= 1 ? Math.PI : Math.PI / 2 + Math.asin(k), pts = [];
    for (let j = 0; j <= 40; j++) pts.push(on(1.5 * Math.PI - hw + 2 * hw * j / 40, el));
    const fade = (1 - smooth((b.age - .15) / .05)) * alpha, ends = hw >= Math.PI ? 'none' : 'both';
    line(`spirit bomb dome band glow ${n}`, pts, .5 + r * .06, KiIce.withAlpha(.35 * fade), whiteGlow, Y + .1439, ends);   // a wide soft band of light
    line(`spirit bomb dome band ${n}`, pts, .05 + r * .008, White.withAlpha(.6 * fade), whiteGlow, Y + .1439, ends);         // and a thin bright line in it
  });
  // Light boils up the surface: soft blobs rise from low on the side that shows to near the top, growing as they go, each
  // 0.6 s and started again somewhere else.
  for (let i = 0; i < DomeBoils; i++) {
    const phase = s / .6 + rand(i + 80), cycle = Math.floor(phase), u = phase - cycle, seed = cycle * 19 + i * 3, az = rand(seed) * TAU;
    const size = r * (.1 + .12 * u) * (.8 + .4 * rand(seed + 1));
    sprite(on(az, Mathf.Lerp(low(az) + .05, 1.35, u)), size, size * .85, Color.Lerp(KiIce, White, rand(seed + 2)).withAlpha(.35 * Math.sin(u * Math.PI) * alpha), glow, Y + .1435);
  }
  // Lightning crawls over the surface: short bolts that wander across the side that shows, each rolled again every 0.12 s.
  for (let b = 0; b < 4; b++) {
    const cycle = Math.floor(s / .12 + b * .29), seed = cycle * 37 + b * 11, az = rand(seed) * TAU, floorEl = low(az) + .02;
    const el = Mathf.Lerp(low(az) + .1, 1.2, rand(seed + 1)), daz = (rand(seed + 2) - .5) * 1.4, del = (rand(seed + 3) - .5) * .9, pts = [];
    for (let j = 0; j <= 10; j++) {
      const k = j / 10;
      pts.push(on(az + daz * k + (rand(seed + 4 + j) - .5) * .08, Math.max(floorEl, Math.min(1.5, el + del * k + (rand(seed + 20 + j) - .5) * .08))));
    }
    line(`spirit bomb dome crawl glow ${b}`, pts, .3 + r * .03, KiIce.withAlpha(.35 * alpha), whiteGlow, Y + .1437, 'both');
    line(`spirit bomb dome crawl ${b}`, pts, .07 + r * .012, White.withAlpha(.95 * alpha), whiteGlow, Y + .1438, 'both');
  }
  // Embers rise off the top and drift up, each 0.8 s.
  for (let i = 0; i < 24; i++) {
    const phase = s / .8 + rand(i + 90), cycle = Math.floor(phase), u = phase - cycle, seed = cycle * 29 + i * 7;
    const from = on(rand(seed) * TAU, Mathf.Lerp(.7, 1.4, rand(seed + 1))), size = .12 + .1 * rand(seed + 2), at2 = { x: from.x + Math.sin(u * 5 + i) * .2, z: from.z + u * (1.5 + 2 * rand(seed + 3)) };
    if (i % 3 === 0) glint(`spirit bomb dome ember ${i}`, at2, size * 2.2, .9 * (1 - u) * alpha, KiIce, 45);
    else draw(MeshPool.plane10, at2.x, Y + .1448, at2.z, size, size, 45 + u * 90, Color.Lerp(White, KiIce, u).withAlpha(.9 * (1 - u) * alpha), whiteGlow);
  }
  // The white-hot middle, and a highlight toward the sun near the top.
  sprite(mid, r * 1.25 * beat, r * 1.2 * beat, White.withAlpha(.7 * alpha), glow, Y + .144);
  sprite(mid, r * .55 * beat, r * .5 * beat, White.withAlpha(.8 * alpha), glow, Y + .1442);
  sprite(on(sunAz, .8), r * .7, r * .55, White.withAlpha(.4 * alpha), glow, Y + .1445);
  sprite(mid, r * 2, r * 1.9, White.withAlpha((.18 * flash + .2 * rush) * alpha), glow, Y + .1446);   // the flash of each pulse
}

export default {
  kit: 'Goku', label: 'Spirit Bomb (sketch)',
  params: {
    lenders: P('Colonists lending energy', 4, 0, 6, 1, 'Showcase'),
    aim: P('Direction to the target (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Distance to the target (cells)', 16, 8, 30, 1, 'Showcase'),
    channel: P('Channel shown (the real one has no upper limit)', 6, 3, 15, .5, 'Timing (s)'),
    fly: P('Bomb is in the air', 1.4, .5, 3, .1, 'Timing (s)'),
    hold: P('Dome stands, full charge (a small bomb 45% of it)', 2, .5, 4, .1, 'Timing (s)'),
    domePace: P('Dome animation speed (1 = as sketched first)', .6, .3, 1.5, .05, 'Timing (s)'),
    lend: P('Power per second per lender (the caster gives 1)', 1, .25, 3, .25, 'Rule'),
    blastPer: P('Blast radius per power (cells; it starts at 2)', .25, .05, .6, .05, 'Rule'),
    sizePer: P('Ball radius per power (cells)', .12, .04, .25, .01, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Stand', t: 0 }, { name: 'Channel', t: t.cast }, ...(p.lenders > 0 ? [{ name: 'Lenders join', t: joins(p, t, 0) }] : []),
    { name: 'Throw', t: t.release }, { name: 'Grind', t: t.hit }, { name: 'Detonation', t: t.dome }, { name: 'Burst', t: t.burst }, { name: 'Aftermath', t: t.gone },
  ]; },
  events(p) {
    const t = times(p), list = [], big = t.by(Shake);
    // A heavy bomb makes the camera tremble while it is still over the caster's head.
    for (let at = t.cast + .5; at < t.release; at += .5) { const c = powerAt(at, p, t) / FullPower; if (c > .5) list.push({ t: at, type: 'shake', value: .012 + .02 * Math.min(1, c) }); }
    for (let at = t.hit; at < t.dome - .01; at += .1) list.push({ t: at, type: 'shake', value: big * (.15 + .2 * (at - t.hit) / (t.dome - t.hit)) });
    // Each heartbeat of the standing dome shakes the camera, harder toward the burst.
    domePulses(t, p.domePace).forEach(q => list.push({ t: q.t, type: 'shake', value: big * (.15 + .25 * q.progress) }));
    return [...list, { t: t.dome, type: 'shake', value: big }, { t: t.dome + .25, type: 'shake', value: big * .55 }, { t: t.dome + .5, type: 'shake', value: big * .3 }, { t: t.burst, type: 'shake', value: big * .35 }];
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

    const cracks = t.count(CrackCount), pillars = t.count(PillarCount), rocks = t.count(RockCount), burns = t.count(StreakCount), pathRings = t.count(PathRingCount);
    const Grind = t.dome - t.hit, Cool = t.gone - t.open + Tail * .8, WhiteFrame = t.by(WhiteTime), chargeNow = clamp(powerAt(s, p, t) / FullPower);
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
    // The dome is the ball grown from its size at the throw (r, fixed once it is thrown) to the blast radius: its front
    // reaches R, fast then slow.
    const domeAge = s - t.dome, opened = clamp(domeAge / Open), R = lerp(r, blast, 1 - Math.pow(1 - opened, 3));
    // The dome is gone Pop seconds after the burst; the column and the pillars thin out over Fade.
    const burstAge = s - t.burst, popped = smooth(burstAge / Pop), domeAlpha = domeAge < 0 ? 0 : 1 - popped;
    const fading = smooth(burstAge / Fade), lightAlpha = domeAge < 0 ? 0 : 1 - fading;
    // The dome's radius as drawn, and whether it still hides what is inside it (until it is half gone in the burst).
    const domeR = R * DomeFill * (1 + PopSwell * popped), hiding = domeAge >= 0 && domeAlpha > .5;
    // Inside the dome: a point h cells up over a spot d cells from the centre. Drawn under its body so the dome covers it.
    const inDome = (d, h) => hiding && d * d + h * h < domeR * domeR;
    // A point on the dome's surface, as drawn: az is the direction from the centre, el the angle up from the floor.
    const onDome = (az, el, rad) => up({ x: target.x + Math.cos(az) * rad * Math.cos(el), z: target.z + Math.sin(az) * rad * Math.cos(el) }, rad * Math.sin(el));
    // When the front of the dome passes a point d cells from the centre; inside the ball's own radius that is at once.
    const passes = d => t.dome + Open * (1 - Math.cbrt(1 - clamp((d - r) / (blast - r))));
    // The blue shell round a colonist, animal or wall inside. The dome hides them while it stands, so the shell shows as
    // it bursts: the light clears and they are standing in blue, untouched. Gone 0.5 s after the dome.
    const shieldAt = d => domeAge >= 0 && d <= blast ? (1 - domeAlpha) * (1 - smooth((burstAge - Pop) / .5)) : 0;
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
    // The scorch: the burn the blast leaves, which stays. It is burnt ground, not a drawn shape: a ragged edge of soft dark
    // blotches round the blast radius with a few gaps, uneven blackening inside that is darkest at the middle, and blast
    // streaks of uneven spacing, length and width, some running past the edge. It burns in as the light clears, and thin
    // smoke rises off it for about 2 s.
    const burnt = domeAge >= 0 ? smooth(burstAge / .8) * (1 - .35 * smooth((s - t.gone) / Tail)) : 0;
    if (burnt > 0) {
      sprite(target, blast * 1.3, blast * 1.2, Ink.withAlpha(.32 * burnt), soft, Floor + .01);                           // darkest at the middle
      for (let i = 0; i < ScorchPatches; i++) {
        const d = blast * .9 * Math.sqrt(rand(i + 500)), size = blast * (.18 + .22 * rand(i + 502));
        sprite(polar(rand(i + 501) * TAU, d), size, size * (.7 + .3 * rand(i + 503)), Ink.withAlpha((.12 + .14 * rand(i + 504)) * (1 - .5 * d / blast) * burnt), puff, Floor + .0102, rand(i + 505) * 180);
      }
      for (let i = 0; i < ScorchEdge; i++) {
        if (rand(i + 513) < .15) continue;                                                                                // a gap
        const ang = (i + rand(i + 510) * .8) / ScorchEdge * TAU, size = blast * (.1 + .12 * rand(i + 512));
        sprite(polar(ang, blast * (.9 + .14 * rand(i + 511))), size * 1.4, size, Ink.withAlpha((.2 + .2 * rand(i + 514)) * burnt), puff, Floor + .0104, -(ang + Math.PI / 2) / Mathf.Deg2Rad);
      }
      for (let i = 0; i < burns; i++) {
        const ang = (i + (rand(i + 90) - .5) * .9) / burns * TAU, from = blast * (.15 + .25 * rand(i + 91)), to = blast * (.6 + .55 * rand(i + 92));
        streak(`spirit bomb burn ${i}`, polar(ang, from), polar(ang, to), .15 + .35 * rand(i + 93) * rand(i + 94), Ink.withAlpha((.22 + .2 * rand(i + 95)) * burnt), undefined, Floor + .012, 5);
      }
      for (let i = 0; i < 12; i++) {
        const u = (burstAge - rand(i + 520) * 1.2) / (1.6 + .8 * rand(i + 521)); if (u < 0 || u > 1) continue;
        const from = polar(rand(i + 522) * TAU, blast * .7 * Math.sqrt(rand(i + 523))), size = .8 + 1.6 * u;
        sprite({ x: from.x + Math.sin(u * 3 + i) * .3, z: from.z + u * (1.5 + 1.5 * rand(i + 524)) }, size, size * .85, Smoke.withAlpha(.22 * Math.sin(u * Math.PI)), puff, Y + .05, u * 40 + i * 30);
      }
    }
    // Jagged cracks of light. They grow through the grind, are hidden while the dome stands, show again when it bursts, and
    // cool afterwards.
    if (s >= t.hit && !hiding) {
      const grown = domeAge >= 0 ? 1 : smooth(grind) * .75, heat = domeAge < 0 ? 1 : 1 - .85 * smooth((s - t.open) / Cool);
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

    // --- the colony wall inside the blast: it takes nothing, and a blue shell over the light shows it -----------------------------
    // The shell's lines are solid blue, not light, so they still show where the light has gone white.
    for (let k = 0; k < 3; k++) wallCell(near([WallFrom[0] + k, WallFrom[1]]), 0);
    const wallShield = shieldAt(Math.hypot(WallFrom[0] + 1, WallFrom[1]));
    if (wallShield > 0) {
      const edge = [[-.62, -.62], [2.62, -.62], [2.62, .62], [-.62, .62]].map(([x, z]) => near([WallFrom[0] + x, WallFrom[1] + z]));
      edge.forEach((a, k) => streak(`spirit bomb wall shell ${k}`, a, edge[(k + 1) % 4], .08, Ki.withAlpha(.95 * wallShield), undefined, Y + .2, 2));
      for (let k = 0; k < 3; k++) sprite(near([WallFrom[0] + k, WallFrom[1]]), 1.4, 1.4, Ki.withAlpha(.25 * wallShield), glow, Y + .199);
    }
    // A shell over a colonist or an animal: a solid blue ring round it and a soft blue light, with the pawn itself drawn
    // again over the light in its own colours, so it reads as untouched even under the column's foot. parts are
    // [centre x, centre z, radius x, radius z, colour] from the pawn's cell; in a port this is a generic pawn-sized shape.
    const shell = (pos, centre, radius, alpha, parts) => {
      if (alpha <= 0) return;
      const at = { x: pos.x + centre.x, z: pos.z + centre.z };
      sprite(at, radius * 2.4, radius * 2.4, Ki.withAlpha(.3 * alpha), glow, Y + .199);
      ringAt(at, radius + .03 * Math.sin(s * 9), Ki.withAlpha(.95 * alpha), Y + .2);
      parts.forEach(([cx, cz, rx, rz, colour], k) => draw(disc, pos.x + cx, Y + .201 + k * .001, pos.z + cz, rx, rz, 0, colour.withAlpha(alpha)));
    };

    // --- pawns, north first ------------------------------------------------------------------------------------------------------
    const joined = [], figures = [{ pos: caster, caster: true }];
    Lenders.slice(0, p.lenders).forEach(([back, side], i) => {
      const at = behind(back, side), lending = smooth((s - joins(p, t, i)) / .3) * (1 - smooth((s - t.release) / .3));
      if (s >= joins(p, t, i) && s < t.release + .3) joined.push({ at, lending, i });
      figures.push({ pos: at, lender: true, lending });
    });
    Foes.forEach((q, i) => figures.push({ pos: near(q), foe: true, d: Math.hypot(q[0], q[1]), i }));
    const outside = BaseRadius + p.blastPer * powerAt(t.release, p, t) + 2.5;                                  // 2.5 cells past the final blast
    figures.push({ pos: near([Math.cos(-.6) * outside, Math.sin(-.6) * outside]), foe: true, d: outside, i: 9 });
    figures.push({ pos: near(Friend), friend: true, d: Math.hypot(...Friend) });
    figures.push({ pos: near(Beast), beast: true, d: Math.hypot(...Beast) });
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      const shielded = g.friend || g.beast ? shieldAt(g.d) : 0;
      if (g.caster) {
        const raise = smooth((s - t.cast) / .35) * (1 - smooth((s - t.release) / Swing));
        pawn(g.pos, Gi, sun, strength, { hair: true, arms: 2, raise, tint: KiIce, tintAmount: .3 * raise + .3 * surge });
      } else if (g.lender) {
        pawn(g.pos, Ally, sun, strength, { arms: 1, raise: g.lending, tint: KiIce, tintAmount: .45 * g.lending });
        sprite({ x: g.pos.x, z: g.pos.z + .35 }, 1.3, 1.6, KiSky.withAlpha(.4 * g.lending * (.8 + .2 * Math.sin(s * 11))), glow, pawnLayer - .01);
      } else if (g.foe) {
        const since = s - passes(g.d);
        if (!(g.d <= blast) || domeAge < 0 || since < 0) { pawn(g.pos, EnemyColour, sun, strength); return; }
        // The damage lands as the front passes, under the dome: it turns white-hot and falls Fall seconds later. The dome
        // hides this; when it bursts the enemy is lying there. The body stays; this is a hit, not an erasure.
        const falling = smooth((since - Fall) / .12), hot = clamp(since / .08) * (1 - smooth((since - Fall) / 1));
        if (falling < 1) pawn(g.pos, EnemyColour, sun, strength, { tint: White, tintAmount: .55 * hot, outline: 1 - falling, alpha: 1 - falling });
        if (falling > 0) pawn(g.pos, EnemyColour, sun, strength, { lie: true, alpha: falling, tint: White, tintAmount: .55 * hot });
      } else if (g.friend) {
        pawn(g.pos, Ally, sun, strength, { tint: KiIce, tintAmount: .25 * shielded });
        shell(g.pos, { x: 0, z: .36 }, .62, shielded, [[0, .18, .22, .32, Ally], [0, .58, .16, .17, Skin]]);
      } else {
        const b = g.pos;
        sprite({ x: b.x + sun.x * .2, z: b.z + sun.z * .2 }, .8, .35, Ink.withAlpha(strength), soft, Floor + .05);
        draw(disc, b.x, pawnLayer, b.z + .14, .3, .18, 0, Animal);
        draw(disc, b.x + .34, pawnLayer + .002, b.z + .26, .13, .12, 0, Animal);
        shell(b, { x: .17, z: .2 }, .58, shielded, [[0, .14, .3, .18, Animal], [.34, .26, .13, .12, Animal]]);
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
        rock({ x: foot.x, z: foot.z + h * Lift }, size * 1.8, i * 50 + s * 60, show, i, Y + .005);
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
    // Up to the detonation; from then on the ball is drawn as the dome, so bomb() runs once a frame.
    if (s >= t.cast && s < t.dome) {
      const swell = 1 + SurgeSwell * surge + .18 * grind + .05 * Math.sin(s * 60) * grind;
      const look = grind > 0 ? { v: { x: 0, z: -1 }, stretch: -.3 * sink, spin: FlightSpin + 3 * grind } : { v, stretch: Stretch * clamp(flight * 3), spin: 1 + (FlightSpin - 1) * clamp(flight * 3) + 1.5 * surge, surge };
      bomb(ball, r * formed * swell, s, 1, look);
    }

    // --- chunks of ground that lift in the grind, and the rocks the blast throws ------------------------------------------------------------
    if (grind > 0 && domeAge < 0) for (let i = 0; i < Chunks; i++) {
      const ang = i * 2.399, d = r * .9 + rand(i + 44) * blast * .45, foot = polar(ang, d), h = smooth(grind * 1.4 - rand(i + 45) * .3) * (.5 + .7 * rand(i + 46)), size = .16 + .16 * rand(i + 47);
      sprite({ x: foot.x + sun.x * h, z: foot.z + sun.z * h }, size * 2.2, size * 1.2, Ink.withAlpha(.35), soft, Floor + .05);
      rock({ x: foot.x + Math.sin(s * 70 + i) * .02, z: foot.z + h * Lift }, size * 1.7, i * 50 + s * 40, 1, i, Y + .02);
    }
    // Thrown rocks: a few big, many small. Each leaves dust behind it in the air, lands with a puff and stays as rubble.
    if (domeAge >= 0) for (let i = 0; i < rocks; i++) {
      const air = .9 + .9 * rand(i + 51), u = domeAge / air, ang = rand(i + 52) * TAU, big = rand(i + 55), size = .24 + .75 * big * big;
      const from = blast * (.15 + .5 * rand(i + 53)), reach = blast * .75, peak = (3 + 5 * rand(i + 54)) * t.by(RockHeight) * (1.15 - .5 * big);
      const where = w => ({ foot: polar(ang, from + reach * Math.min(1, w)), h: w < 1 ? peak * 4 * w * (1 - w) : 0 });
      const now = where(u), landed = u >= 1, turn = i * 47 + Math.min(u, 1) * air * (300 + 300 * rand(i + 56));
      sprite({ x: now.foot.x + sun.x * now.h, z: now.foot.z + sun.z * now.h }, size * 2.2, size * 1.1, Ink.withAlpha(.32), soft, Floor + .05);
      if (!landed) for (let k = 1; k <= 3; k++) {
        const w = u - k * .05; if (w <= 0) continue;
        const q = where(w);
        sprite(up(q.foot, q.h), size * (1.2 + k * .5), size * (1 + k * .4), Dust.withAlpha(.3 * (1 - k / 4)), soft, inDome(Math.hypot(q.foot.x - target.x, q.foot.z - target.z), q.h) ? Y + .134 : Y + .174);
      }
      rock(up(now.foot, now.h), size, turn, 1, i, landed ? Floor + .06 : inDome(Math.hypot(now.foot.x - target.x, now.foot.z - target.z), now.h) ? Y + .134 : Y + .175);   // inside the dome it is hidden until it flies out
      const since = domeAge - air;
      if (since >= 0 && since < .5) { const v = since / .5; sprite({ x: now.foot.x, z: now.foot.z + v * .25 }, size * (2 + 3 * v), size * (1.5 + 2.2 * v), Dust.withAlpha(.5 * Math.sin(v * Math.PI)), soft, Y + .006); }
    }

    // --- the detonation ------------------------------------------------------------------------------------------------------------------------
    if (domeAge >= 0 && domeAge < WhiteFrame * 2) sprite(target, blast * 3.4, blast * 3.4, White.withAlpha(domeAge < WhiteFrame ? 1 : 1 - (domeAge - WhiteFrame) / WhiteFrame), glow, Y + .19);   // soft-edged, so no hard white circle
    if (domeAge >= 0 && domeAge < .4) sprite(target, blast * 5, blast * 5, KiIce.withAlpha((.3 + .4 * t.charge) * Math.pow(1 - domeAge / .4, 2)), glow, Y + .15);   // whiteout
    // The dome is the ball grown from its size at the throw to the blast radius, in its explosion (blastDome). It hides
    // what is inside; the result shows when it bursts. It builds up through the hold and swells a little as it bursts.
    if (domeAlpha > 0) blastDome(target, domeR, s, domeAlpha, smooth((s - t.open) / (t.burst - t.open)), sun,
      domePulses(t, p.domePace).map(q => ({ age: s - q.t, strength: q.strength })).filter(q => q.age >= 0 && q.age * p.domePace < 1.2), p.domePace);
    // The column of light. It is light, not an object: no mesh with an edge, only soft sprites that overlap up its
    // height, each one rippling in width and dimmer than the one below, so it has no outline and no tip. Streaks
    // run up through it, a pool of light sits at its foot, and rings climb it. It narrows to a thread after the burst.
    if (lightAlpha > 0) {
      const tall = (2.5 + 1.9 * blast + ColumnExtra * t.charge) * smooth(domeAge / .25), girth = 1 - .85 * fading, parts = 16, piece = tall / parts;
      const widthAt = u => blast * (.24 - .13 * Math.pow(u, .7)) * girth * (1 + .16 * Math.sin(u * 11 - s * 15) + .08 * Math.sin(u * 23 - s * 27));
      sprite(target, blast * 1.2 * girth, blast * .7 * girth, KiIce.withAlpha(.4 * lightAlpha), glow, hiding ? Y + .134 : Y + .1695);
      for (let j = 0; j < parts; j++) {
        const u = (j + .5) / parts, c = up(target, tall * u), w = widthAt(u), dim = clamp((1 - u) / .4) * (1 - .3 * u) * lightAlpha * (.85 + .15 * Math.sin(s * 33 + j * 1.7));   // full to 60% of the height, then it thins out to nothing
        const layer = inDome(0, tall * u) ? Y + .134 : Y + .17;   // inside the dome it is under its body: it comes out of the top
        sprite(c, w * 3.2, piece * Lift * 3.2, Ki.withAlpha(.6 * dim), glow, layer);
        sprite(c, w * 1.7, piece * Lift * 2.8, KiIce.withAlpha(.75 * dim), glow, layer + .0005);
        sprite(c, w * .8, piece * Lift * 2.6, White.withAlpha(.85 * dim), glow, layer + .001);
      }
      for (let i = 0; i < 18; i++) {
        const u = (domeAge * (1.4 + rand(i + 61)) + rand(i + 62)) % 1, x = (rand(i + 63) - .5) * widthAt(u) * 1.8, long = tall * (.1 + .12 * rand(i + 64));
        const from = up(target, tall * u), to = up(target, Math.min(tall, tall * u + long));
        streak(`spirit bomb column streak ${i}`, { x: from.x + x, z: from.z }, { x: to.x + x, z: to.z }, .09, White.withAlpha(.8 * Math.sin(u * Math.PI) * (1 - u) * lightAlpha), whiteGlow, inDome(0, tall * u) ? Y + .1345 : Y + .1715, 4);
      }
      for (let n = 0; n < 5; n++) {
        const u = (domeAge * .9 + n / 5) % 1, c = up(target, tall * u);
        ringAt(c, widthAt(u) * 1.4 + .1, KiIce.withAlpha(.4 * Math.sin(u * Math.PI) * (1 - u) * lightAlpha), inDome(0, tall * u) ? Y + .1345 : Y + .172, false, whiteGlow);
      }
      // The thin pillars, the same way: a soft shaft that flickers, brightest at the floor, with a bead running up it.
      for (let i = 0; i < pillars; i++) {
        const ang = i * 2.399, d = blast * (.3 + .6 * rand(i + 60)), foot = polar(ang, d);
        if (R < d) continue;
        const h = (3 + 4 * rand(i + 70)) * smooth((s - passes(d)) / .3), flick = (.7 + .3 * Math.sin(s * 30 + i * 2)) * lightAlpha;
        for (let j = 0; j < 5; j++) {
          const u = (j + .5) / 5, c = up(foot, h * u);
          const layer = inDome(d, h * u) ? Y + .134 : Y + .166;
          sprite(c, .9 - .4 * u, h * Lift * .5, Ki.withAlpha(.4 * (1 - u) * flick), glow, layer);
          sprite(c, .3 - .12 * u, h * Lift * .45, White.withAlpha(.75 * (1 - u) * flick), glow, layer + .001);
        }
        sprite(foot, 1.3, .8, KiIce.withAlpha(.6 * flick), glow, hiding ? Y + .134 : Y + .1665);
        const bead = (s * 1.6 + rand(i + 75)) % 1;
        sprite(up(foot, h * bead), .3, .5, White.withAlpha(.9 * (1 - bead) * flick), glow, inDome(d, h * bead) ? Y + .134 : Y + .168);
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

    // --- the burst: the dome breaks into flecks of light that drift up and a little out, twinkle, and fade ---------------------------------------------
    if (burstAge >= 0 && burstAge < Scatter) {
      const rad = blast * (1 + PopSwell), haze = 1 - smooth(burstAge / 1.2);
      if (burstAge < .15) sprite(up(target, rad * .35), rad * 2.8, rad * 2.8, White.withAlpha(.45 * (1 - burstAge / .15)), glow, Y + .185);   // the flash as it bursts
      for (let k = 0; k < 7; k++) sprite(up(k ? onDome(k * TAU / 6, .45, rad) : up(target, rad * .8), burstAge * .8), rad * .9, rad * .9, KiSky.withAlpha(.2 * haze), glow, Y + .176);   // a thin haze where the dome was, rising
      const flecks = t.count(FleckCount);
      for (let i = 0; i < flecks; i++) {
        const a = burstAge - rand(i + 309) * .12, life = 1.1 + .9 * rand(i + 303), u = a / life; if (u < 0 || u >= 1) continue;
        // Born where the dome was: a little over half on its surface (spread evenly: the sine of the angle up is even), the rest
        // through its inside, so the burst fills the whole blast. Then it drifts out and rises, easing off.
        const az = rand(i + 300) * TAU, el = Math.asin(rand(i + 301)), deep = rand(i + 312) < .55 ? 1 : Math.cbrt(rand(i + 313)), ease = 1 - (1 - u) * (1 - u);
        const d = rad * deep * Math.cos(el) + blast * (.03 + .07 * rand(i + 304)) * ease, h = rad * deep * Math.sin(el) + (1 + 2 * rand(i + 305)) * ease;
        const at = up({ x: target.x + Math.cos(az) * d + Math.sin(a * 3 + i) * .15, z: target.z + Math.sin(az) * d }, h);
        const twinkle = .55 + .45 * Math.sin(a * (9 + 8 * rand(i + 306)) + i * 1.7), alpha = twinkle * Math.pow(1 - u, 1.2) * clamp(a / .06);
        const size = .1 + .14 * rand(i + 307), colour = rand(i + 308) < .25 ? KiSky : Color.Lerp(KiIce, White, rand(i + 310));
        draw(MeshPool.plane10, at.x, Y + .18, at.z, size, size, 45 + a * 120 * (rand(i + 311) - .5), colour.withAlpha(alpha), whiteGlow);
        if (i % 6 === 0) glint(`spirit bomb fleck ${i}`, at, size * 2.4, .85 * alpha, KiIce, 45);
      }
    }
  },
};
