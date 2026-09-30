// Reversal: Red v2 — Gojo kit, ability sketch, not the game. gojo-red.js (v1) is kept for comparison.
//
// Mechanic (agreed 2026-09-27, docs/hero-echo.md; every number is a placeholder for XML):
//   Target a cell up to 20 away. Red flies there at 20 cells/s at chest height. The first pawn or
//   loose thing on its line is hit; if nothing is, it bursts at the cell. The thing hit is thrown 6
//   cells along Red's direction, 1.5 blunt per cell travelled, +10 if a wall stops it. Everything else
//   within 1.5 cells of the burst is pushed 2 cells straight away from it, 8 blunt. No stun (the user,
//   2026-09-29). Cooldown 10 s, 3 charge. Through an active Blue it becomes Hollow Purple (not here).
//
// Sources (checked 2026-09-29 with YouTube storyboards):
//   anime, 0jRyxkaWuU8   47-51 s finger up, red orb past the fingertip, dark rim, pink halo, lilac
//                        needles, white-hot before it leaves; 24 s red centre in a white ring with thin
//                        red lines crossing (vs Toji); 2-8 s scene washed red; 27-29 s body thrown with
//                        red speed lines; 30-34 s it hits a building in a big dust cloud
//   Cursed Clash, 8Ot4zM2Z-Ow  #46 hold level 1: arm out, a flat red-pink swirl spinning round the hand,
//                        long pale-pink ribbon arcs sweeping round Gojo, red specks; #53 level 3: the
//                        swirl about twice as wide; #47 the hit: a small pale pink burst, target knocked back
//
// v2 against v1: the charge is the game's spinning swirl and ribbon arcs instead of dark smoke; the
// burst is directional (a shock front runs down Red's line, rocks and dust thrown forward) instead of
// target rings; the thrown body flies in an arc lying down and skids; the wall cracks; pushed pawns
// leave a dust trail. The wash, the flash, the orb, the tail and the timing are v1's.
//
// Order (raider into a wall, the default; charge 0.5 s, raider 9 cells out, wall 4 behind him):
//   0.00-0.15   the arm comes up and points at the raider
//   0.10-0.60   charge: a flat red swirl at the fingertip, 4 arms, 0.3 -> 0.75 cells, spinning 540°/s;
//               2 pale-pink ribbon arcs sweep 200° round Gojo at 1.1 and 1.4 cells; red specks drawn in;
//               the orb grows past the fingertip with lilac needles and thin red lines crossing it;
//               a thin white ring round it for the last 0.2 s; the core goes white-hot; Gojo lit red
//   0.60        fire: pink-white flash at the finger, a red ring opens, the arcs fade in 0.15 s
//   0.60-1.01   flight (anime 54-55 s, Phantom Parade #77/#92): a solid red ball 0.21 cells in radius,
//               shaded from the sun side, in a red corona; 2 white-pink crescents (46° each) turn round it
//               at 900°/s; a faint pale pressure ring; the tail is a dark-red layer under the red light so
//               it stays red on grass, with a pink-white core; 8 dark-red ink streaks behind it that
//               jump every 1/24 s like drawn speed lines, 4 red light streaks; red specks shed; the air
//               pushed aside: dust thrown out sideways from its path every 0.6 cells, a V-shaped wake
//   1.01        burst: the ground washed red over 3.5 cells for 0.45 s, a white flash, red sparks
//               thrown forward, a faint ring opening to the 1.5-cell burst radius, the second raider
//               pushed 2 cells with a red edge and a dust trail
//   1.01-1.12   the shock front, a red crescent 1.6 cells across and 110° wide, runs down Red's line at
//               30 cells/s ahead of the body; 6 rocks and dust thrown forward in a ±40° fan
//   1.01-1.17   the raider flies lying down, head first, lifted up to 0.6 cells with his shadow on the
//               floor, red edge for 0.4 s, red and black speed lines behind
//   1.12        the front hits the wall: a red flash on the face
//   1.17        the raider slams into the wall 0.5 cells up: squashes, 5 cracks and a dent stay, a big
//               dust cloud, 12 chunks thrown back; he slides down and stands (no stun)
//   stays       scorch at the burst point, rocks, cracks
//   "group in the open": he flies 4.5 cells, bounces 0.15 cells, skids to 6 cells (drag marks stay)
//   and gets up; two others pushed 2 cells.
//   "empty cell": the target cell is outlined, Red bursts there, the front only runs the 1.5-cell burst
//   radius; one raider 1.2 cells away is pushed, one 2.5 cells away is only lit.
//
// Drawing: flat shapes and level circles only, so it turns with the aim and needs no per-facing
// method. Height is drawn north (0.6 per cell) with shadows left on the floor. Red is light: soft
// additive layers, a Transparent dark-red layer under the wash. Gojo is lib/gojo.js's stand-in with a
// pointing arm drawn here; raiders and wall are stand-ins.
//
// Ported to C# as a picture (Source/RimArt/Gojo/GojoRed*.cs, recorded as "Gojo: red: ..."). "Stand-in
// pawns and props" off hides Gojo's body, the raiders' bodies and shadows and the wall, and shows what
// the port draws.
import { Color, Mathf, Meshes, MeshPool } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import { caster, Uniform } from './lib/gojo.js';
import { pawn, rock, ringAt, line, streak, whiteGlow, wallCell, EnemyColour, Ink, White, Dust, Lift, Chest, Skin, pawnLayer, shadowLayer, buildingLayer, clamp, smooth } from './lib/goku.js';

const TAU = Math.PI * 2, D2R = Mathf.Deg2Rad, ChestUp = Chest / Lift;
const disc = Meshes.disc(32, 'red2 disc');
// Decided values. Speed is the agreed 20 cells/s.
const Speed = 20, Start = .1, Raise = .15, Flash = .12, ThrowSpeed = 26, PushTime = .28, Tail = 1.6;
const OrbR = .17, Reach = .5, Tip = .8;              // orb radius; hand and orb distance from Gojo's centre
const SwirlFrom = .3, SwirlTo = .75, SwirlSpin = 540, Arcs = [[1.1, 30], [1.4, 210]], ArcSweep = 200;
const WashTime = .45, RingTime = .12, RingHold = .5, Sparks = 16, Squash = .1, Chips = 12, Cracks = 5, Rocks = 6;
const FlightR = .21, Crescents = 2, CrescentSpin = 900, InkStreaks = 8, WakeStep = .6, WakeLife = .45, WakeOut = .9;
const FrontSpeed = 30, FrontHalf = 55, FrontWide = 1.6, FrontFade = .12, Peak = .6, Bounce = .15;
const Red = new Color(1, .1, .14), RedDeep = new Color(.42, 0, .05), HotPink = new Color(1, .78, .82), Lilac = new Color(.98, .74, 1);
const Ink2 = new Color(.3, 0, .04), WakeDust = new Color(.7, .64, .56), Scorch = new Color(.22, .03, .04), WallDust = new Color(.74, .72, .69);
// Other pawns per scenario: [cells past the burst point along Red's line, cells across]. Pushed if
// within the burst radius of it.
const Others = {
  'raider into a wall': [[.3, 1.1]],
  'group in the open': [[-.5, 1], [.6, -.95]],
  'empty cell': [[.4, 1.15], [-1.2, -2.2]],
};

const wall = p => p.scenario === 'raider into a wall', open = p => p.scenario === 'group in the open', empty = p => p.scenario === 'empty cell';
// The body stops with its back 0.15 cells short of the wall face.
const stopAt = p => empty(p) ? 0 : wall(p) ? Math.min(p.throwCells, p.wallBehind - .65) : p.throwCells;
const hitsWall = p => wall(p) && p.throwCells >= p.wallBehind - .65;

// The thrown body leaves at ThrowSpeed and slows evenly to rest at the full throw; a wall stops it early.
function times(p) {
  const fire = Start + p.charge, arrive = fire + Math.max(0, p.dist - Tip) / Speed;
  const full = p.throwCells, dur = 2 * full / ThrowSpeed, stop = stopAt(p);
  const fly = empty(p) ? 0 : (1 - Math.sqrt(Math.max(0, 1 - stop / full))) * dur;
  return { fire, arrive, dur, fly, land: arrive + fly, end: arrive + Math.max(fly, PushTime) + Tail };
}
function thrown(p, age, t) {
  if (age <= 0 || empty(p)) return 0;
  const u = Math.min(age, t.fly) / t.dur;
  return Math.min(stopAt(p), p.throwCells * (1 - (1 - u) ** 2));
}
// Height of the thrown body: one arc up to Peak over the first half of the throw's time (about 75 % of
// its distance), a small bounce, then on the floor.
function lift(age, t) {
  const air = t.dur * .5, hop = t.dur * .2;
  if (age <= 0) return 0;
  if (age < air) return Peak * Math.sin(Math.PI * age / air);
  if (age < air + hop) return Bounce * Math.sin(Math.PI * (age - air) / hop);
  return 0;
}

// The orb: a red halo and a pale pink one, a darker rim, the red body, a core that goes white-hot
// (hot 0..1), and lilac needle flares that flicker (flare 0..1).
function redOrb(key, at, r, s, hot, flare) {
  if (r <= .005) return;
  const beat = 1 + .1 * Math.sin(s * 40);
  sprite(at, r * 9, r * 9, Red.withAlpha(.35), glow, Y + .1);
  sprite(at, r * 4.5, r * 4.5, HotPink.withAlpha(.45), glow, Y + .101);
  if (flare > 0) for (let i = 0; i < 7; i++) {
    const q = (i * 51.4 + rand(i + 180) * 24 + s * 40) * D2R, flick = .45 + .55 * Math.abs(Math.sin(s * 19 + i * 2.7)), len = r * (1.4 + 2.6 * rand(i + 190)) * flick;
    streak(`${key} needle ${i}`, { x: at.x + Math.cos(q) * r * .9, z: at.z + Math.sin(q) * r * .9 }, { x: at.x + Math.cos(q) * (r + len), z: at.z + Math.sin(q) * (r + len) },
      r * .38, Lilac.withAlpha(.8 * flare * flick), whiteGlow, Y + .106, 4);
  }
  draw(disc, at.x, Y + .11, at.z, r * 1.18, r * 1.18, 0, RedDeep);
  draw(disc, at.x, Y + .111, at.z, r, r, 0, Red);
  const core = r * (.35 + .45 * hot) * beat;
  draw(disc, at.x, Y + .112, at.z, core, core, 0, Color.Lerp(HotPink, White, hot));
  sprite(at, core * 3.2, core * 3.2, White.withAlpha(.25 + .6 * hot), glow, Y + .113);
}

// Red in flight: a solid red ball shaded from the sun side (lit toward light, dark rim away) in a red
// corona over a dark-red shade, a thin pale pressure ring, and white-pink crescents turning round it.
function flightOrb(key, at, r, s, sun) {
  const len = Math.hypot(sun.x, sun.z) || 1, lx = -sun.x / len, lz = -sun.z / len, beat = 1 + .06 * Math.sin(s * 45);
  sprite(at, r * 6, r * 6, RedDeep.withAlpha(.35), soft, Y + .099);
  sprite(at, r * 8 * beat, r * 8 * beat, Red.withAlpha(.45), glow, Y + .1);
  ringAt(at, r * 2.4 + .03 * Math.sin(s * 50), HotPink.withAlpha(.18), Y + .101, false, whiteGlow);
  draw(disc, at.x, Y + .11, at.z, r * 1.1, r * 1.1, 0, Ink2);
  draw(disc, at.x - lx * r * .1, Y + .111, at.z - lz * r * .1, r * .98, r * .98, 0, RedDeep);
  draw(disc, at.x + lx * r * .1, Y + .112, at.z + lz * r * .1, r * .82, r * .82, 0, Red);
  draw(disc, at.x + lx * r * .38, Y + .113, at.z + lz * r * .38, r * .34, r * .3, 0, HotPink.withAlpha(.85));
  draw(disc, at.x + lx * r * .48, Y + .114, at.z + lz * r * .48, r * .13, r * .12, 0, White.withAlpha(.9));
  sprite(at, r * 1.6, r * 1.6, HotPink.withAlpha(.35), glow, Y + .115);
  for (let i = 0; i < Crescents; i++) {
    const a0 = -s * CrescentSpin * D2R + i * TAU / Crescents, R = r * 1.9, pts = [];
    for (let k = 0; k <= 8; k++) { const q = a0 + k / 8 * .8; pts.push({ x: at.x + Math.cos(q) * R, z: at.z + Math.sin(q) * R }); }
    line(`${key} crescent ${i} edge`, pts, .1, Red.withAlpha(.6), whiteGlow, Y + .116, 'both');
    line(`${key} crescent ${i}`, pts, .035, White.withAlpha(.9), whiteGlow, Y + .117, 'both');
  }
}

// The game's hold: a flat swirl of arms curling out from the centre to radius R, turned by spin
// (radians), a red edge and a pink-white core per arm over a red disc of light.
function swirl(key, at, R, spin, alpha, arms = 4) {
  if (R <= .02 || alpha <= 0) return;
  sprite(at, R * 3, R * 3, Red.withAlpha(.35 * alpha), glow, Y + .085);
  for (let i = 0; i < arms; i++) {
    const pts = [], a0 = spin + i * TAU / arms;
    for (let k = 0; k <= 12; k++) { const v = k / 12, rr = R * (.15 + .85 * v), q = a0 + v * 2.6; pts.push({ x: at.x + Math.cos(q) * rr, z: at.z + Math.sin(q) * rr }); }
    line(`${key} arm ${i}`, pts, R * .28, Red.withAlpha(.55 * alpha), whiteGlow, Y + .086, 'both');
    line(`${key} arm ${i} core`, pts, R * .09, HotPink.withAlpha(.85 * alpha), whiteGlow, Y + .087, 'both');
  }
}

// A ribbon arc round a point at radius R: its head at angle head, its tail span radians behind.
function ribbon(key, centre, R, head, span, alpha) {
  if (alpha <= 0) return;
  const pts = [];
  for (let k = 0; k <= 16; k++) { const q = head + span * (1 - k / 16); pts.push({ x: centre.x + Math.cos(q) * R, z: centre.z + Math.sin(q) * R }); }
  line(`${key} edge`, pts, .18, Red.withAlpha(.45 * alpha), whiteGlow, Y + .08, 'both');
  line(`${key} core`, pts, .055, HotPink.withAlpha(.85 * alpha), whiteGlow, Y + .081, 'both');
}

// Thin straight red lines crossing through a point (the Toji frame). half is each line's half length.
function crossLines(key, at, count, half, width, alpha, turn, seed) {
  if (alpha <= 0 || half <= .01) return;
  for (let i = 0; i < count; i++) {
    const q = (i * 180 / count + (rand(i + seed) - .5) * 30) * D2R + turn, l = half * (.7 + .3 * rand(i + seed + 7));
    const dx = Math.cos(q) * l, dz = Math.sin(q) * l;
    streak(`${key} ${i}`, { x: at.x - dx, z: at.z - dz }, { x: at.x + dx, z: at.z + dz }, width, Red.withAlpha(alpha), whiteGlow, Y + .102, 8);
  }
}

// Gojo's arm held out at the target: sleeve, hand, index finger. out 0..1.
function pointingArm(place, deg, out, layer) {
  if (out <= .02) return;
  const reach = Reach * out, sleeve = place(reach * .4, 0, ChestUp), hand = place(reach, 0, ChestUp), tip = place(reach + .16 * out, 0, ChestUp);
  draw(MeshPool.plane10, sleeve.x, layer, sleeve.z, .12, reach * .8, 90 - deg, Uniform);
  draw(disc, hand.x, layer + .001, hand.z, .06, .06, 0, Skin);
  draw(MeshPool.plane10, (hand.x + tip.x) / 2, layer + .0015, (hand.z + tip.z) / 2, .03, .16 * out, 90 - deg, Skin);
}

// A red edge round a standing stand-in pawn (drawn just under it). k 0..1.
function redEdge(pos, k) {
  if (k <= 0) return;
  draw(disc, pos.x, pawnLayer - .003, pos.z + .18, .28, .38, 0, Red.withAlpha(.85 * k));
  draw(disc, pos.x, pawnLayer - .003, pos.z + .58, .22, .23, 0, Red.withAlpha(.85 * k));
  sprite({ x: pos.x, z: pos.z + .35 }, 1.1, 1.3, Red.withAlpha(.35 * k), glow, pawnLayer - .004);
}

// The thrown body: lying along the throw, head leading, at pos (already lifted); shadow at ground.
// wob turns it a little (degrees) so it does not fly rigid. lit tints it red, edge draws a red rim.
// actors false draws the red rim only (what the port draws round a real pawn).
function flung(pos, ground, h, deg, colour, lit, edge, wob, sun, strength, actors = true) {
  const q = deg * D2R, ux = Math.cos(q), uz = Math.sin(q), rot = -deg + wob;
  const head = { x: pos.x + ux * .42, z: pos.z + uz * .42 }, body = { x: pos.x, z: pos.z + .12 };
  if (actors) sprite({ x: ground.x + sun.x * (h + .2), z: ground.z + sun.z * (h + .2) }, .95 * (1 - .25 * h), .4 * (1 - .25 * h), Ink.withAlpha(strength * (1 - .35 * h)), soft, shadowLayer, rot);
  if (edge > 0) {
    draw(disc, body.x, pawnLayer - .003, body.z, .38, .26, rot, Red.withAlpha(.85 * edge));
    draw(disc, head.x, pawnLayer - .003, head.z + .02, .22, .23, rot, Red.withAlpha(.85 * edge));
    sprite(body, 1.4, .9, Red.withAlpha(.35 * edge), glow, pawnLayer - .004, rot);
  }
  if (!actors) return;
  draw(disc, body.x, pawnLayer, body.z, .32, .2, rot, Color.Lerp(colour, Red, lit));
  draw(disc, head.x, pawnLayer + .002, head.z + .02, .16, .17, rot, Skin);
}

// The stand-in pawn squashed against a wall: both ellipses narrowed along the throw by k (0..1);
// drawn at pos (lifted), shadow at ground.
function squashed(pos, ground, colour, k, deg, sun, strength) {
  const c = Math.abs(Math.cos(deg * D2R)), n = Math.abs(Math.sin(deg * D2R));
  const fx = (1 - .45 * k * c) * (1 + .15 * k * n), fz = (1 - .45 * k * n) * (1 + .15 * k * c);
  sprite({ x: ground.x + sun.x * .6, z: ground.z + sun.z * .6 }, .85, .4, Ink.withAlpha(strength), soft, shadowLayer);
  draw(disc, pos.x, pawnLayer, pos.z + .18, .22 * fx, .32 * fz, 0, colour);
  draw(disc, pos.x, pawnLayer + .002, pos.z + .18 + .4 * fz, .16 * fx, .17 * fz, 0, Skin);
}

export default {
  kit: 'Gojo', label: 'Reversal: Red v2 (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'raider into a wall', options: ['raider into a wall', 'group in the open', 'empty cell'], group: 'Showcase' },
    actors: { label: 'Stand-in pawns and props', value: true, group: 'Showcase' },
    aim: P('Aim (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    dist: P('First pawn or target cell (cells from Gojo, up to 20)', 9, 3, 20, .5, 'Showcase'),
    wallBehind: P('Wall behind the raider (cells)', 4, 2, 8, 1, 'Showcase'),
    charge: P('Charge at the finger', .5, .2, 1.2, .05, 'Timing (s)'),
    throwCells: P('Thing hit thrown (cells)', 6, 2, 10, .5, 'Mechanic'),
    pushCells: P('Others pushed (cells)', 2, .5, 4, .5, 'Mechanic'),
    burstR: P('Burst radius (cells)', 1.5, .5, 3, .25, 'Mechanic'),
    washR: P('Red wash radius (cells)', 3.5, 1, 7, .25, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Point', t: 0 }, { name: 'Charge', t: Start }, { name: 'Fire', t: t.fire }, { name: 'Burst', t: t.arrive },
      ...(empty(p) ? [] : [{ name: hitsWall(p) ? 'Slam' : 'Lands', t: t.land }])];
  },
  events(p) {
    const t = times(p);
    return [{ t: t.fire, type: 'shake', value: .03 }, { t: t.arrive, type: 'shake', value: .12 }, ...(hitsWall(p) ? [{ t: t.land, type: 'shake', value: .08 }] : [])];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * D2R, ca = Math.cos(a), sa = Math.sin(a);
    // The chosen cell is the middle of the whole event; G is Gojo, along runs down Red's line from him.
    const span = p.dist + (empty(p) ? 1.5 : wall(p) ? p.wallBehind : p.throwCells), G = { x: o.x - ca * span / 2, z: o.z - sa * span / 2 };
    const place = (along, across = 0, up = 0) => ({ x: G.x + along * ca - across * sa, z: G.z + along * sa + across * ca + up * Lift });
    const age = s - t.arrive, burst = age >= 0, B = place(p.dist, 0, ChestUp), Bf = place(p.dist), F = place(Tip, 0, ChestUp);
    const charging = s >= Start && s < t.fire, charge = clamp((s - Start) / p.charge);
    const flying = s >= t.fire && s < t.arrive, d = flying ? Tip + (s - t.fire) * Speed : Tip;
    const stop = stopAt(p), gone = thrown(p, age, t), h = empty(p) ? 0 : lift(Math.min(age, t.fly), t);
    const slamAge = hitsWall(p) && burst && age >= t.fly ? age - t.fly : -1, face = p.dist + p.wallBehind - .5, slamUp = lift(t.fly, t);
    // The shock front: runs from the burst point down Red's line, to the wall face, the full throw, or
    // only the burst radius when nothing was hit.
    const frontLen = empty(p) ? p.burstR : wall(p) ? Math.max(.5, face - .1 - p.dist - .2) : p.throwCells, frontTime = frontLen / FrontSpeed;

    // --- floor: red light under the orb, the target cell, the scorch and drag marks that stay ----------------------
    if (charging) sprite(place(Tip), 1.5 * charge, 1.1 * charge, Red.withAlpha(.3 * charge), glow, Floor + .03);
    if (flying) {
      sprite(place(d), 1.5, 1.1, RedDeep.withAlpha(.25), soft, Floor + .029);
      sprite(place(d), 1.7, 1.2, Red.withAlpha(.3), glow, Floor + .03);
      line('red2 floor path', [place(d), place(Math.max(Tip, d - 3.5))], .5, Red.withAlpha(.14), whiteGlow, Floor + .025, 'end');
    }
    if (empty(p) && s < t.arrive + .1) {
      const k = smooth(s / .2) * (1 - smooth((s - t.arrive) / .1)), c = { x: Math.floor(Bf.x) + .5, z: Math.floor(Bf.z) + .5 };
      [[-1, -1, 1, -1], [1, -1, 1, 1], [1, 1, -1, 1], [-1, 1, -1, -1]].forEach(([x0, z0, x1, z1], i) =>
        line(`red2 cell ${i}`, [{ x: c.x + x0 * .5, z: c.z + z0 * .5 }, { x: c.x + x1 * .5, z: c.z + z1 * .5 }], .04, Red.withAlpha(.6 * k), whiteGlow, Floor + .02, 'none'));
    }
    if (burst) {
      const g = smooth(age / .1);
      sprite(Bf, 1.5 * g, 1.1 * g, Scorch.withAlpha(.4), soft, Floor + .01);
      sprite(Bf, .7 * g, .5 * g, Scorch.withAlpha(.55), soft, Floor + .011);
      // Drag marks from the first touchdown to where he stops skidding.
      const from = p.throwCells * .75;
      if (open(p) && gone > from) [-.12, .12].forEach((x, i) =>
        line(`red2 skid ${i}`, [place(p.dist + from, x), place(p.dist + gone, x)], .05, Ink.withAlpha(.3), undefined, Floor + .012, 'none'));
    }

    // --- the wall: shakes on the slam, a dent and cracks stay on its face ---------------------------------------------------
    if (wall(p)) {
      const shake = slamAge >= 0 && slamAge < .15 ? .05 * Math.sin(slamAge * 95) * (1 - slamAge / .15) : 0;
      if (p.actors) for (let k = -1; k <= 1; k++) wallCell(place(p.dist + p.wallBehind + shake, k), p.aim);
      if (slamAge >= 0) {
        const grow = smooth(slamAge / .06), up = slamUp + ChestUp * .6, D = place(face + .14, 0, up);
        sprite(D, .45, .65, Ink.withAlpha(.5 * grow), soft, buildingLayer + .004, -p.aim);
        sprite(D, .2, .32, Ink.withAlpha(.6 * grow), soft, buildingLayer + .0045, -p.aim);
        for (let i = 0; i < Cracks; i++) {
          const th = (-75 + i * 37.5 + (rand(i + 3) - .5) * 20) * D2R, len = (.4 + .3 * rand(i + 8)) * smooth(slamAge / .08), pts = [];
          for (let k = 0; k <= 7; k++) { const dd = len * k / 7, off = k ? (rand(i * 11 + k) - .5) * .14 : 0; pts.push(place(face + .02 + Math.cos(th) * dd - Math.sin(th) * off, Math.sin(th) * dd + Math.cos(th) * off, up)); }
          line(`red2 crack ${i}`, pts, .045, Ink.withAlpha(.8), undefined, buildingLayer + .005);
        }
      }
    }

    // --- rocks thrown forward by the burst: they land and stay ------------------------------------------------------------
    if (burst) for (let i = 0; i < Rocks; i++) {
      const q = a + (rand(i + 400) - .5) * 80 * D2R, reach = 2 + 2 * rand(i + 410), T = .35 + .15 * rand(i + 420), u = clamp(age / T);
      const r0 = .2 + reach * (1 - (1 - u) ** 2), hh = .6 * (.6 + .4 * rand(i + 430)) * Math.sin(Math.PI * u), ground = { x: Bf.x + Math.cos(q) * r0, z: Bf.z + Math.sin(q) * r0 };
      if (u < 1) sprite({ x: ground.x + sun.x * hh, z: ground.z + sun.z * hh }, .18, .12, Ink.withAlpha(.35), soft, Floor + .05);
      rock({ x: ground.x, z: ground.z + hh * Lift }, .14 + .08 * rand(i + 440), rand(i + 450) * 360 + age * 600 * (1 - u), 1, 2 * (i % 3) + 1, u < 1 ? Y + .05 : Floor + .06);
    }

    // --- the pawns, north first ----------------------------------------------------------------------------------------------
    const figures = [{ kind: 'gojo', pos: G }];
    if (!empty(p)) figures.push({ kind: 'target', pos: place(p.dist + gone) });
    Others[p.scenario].forEach(([along, across], i) => {
      const start = place(p.dist + along, across), dx = start.x - Bf.x, dz = start.z - Bf.z, r = Math.hypot(dx, dz) || 1, inside = r <= p.burstR;
      const k = burst && inside ? 1 - (1 - clamp(age / PushTime)) ** 2 : 0;
      figures.push({ kind: 'other', i, inside, start, k, dir: { x: dx / r, z: dz / r }, pos: { x: start.x + dx / r * p.pushCells * k, z: start.z + dz / r * p.pushCells * k } });
    });
    const litByOrb = pos => flying ? .45 * (1 - clamp(Math.hypot(pos.x - place(d).x, pos.z - place(d).z) / 2.5)) : 0;
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      if (g.kind === 'gojo') {
        const out = smooth(s / Raise) * (1 - smooth((s - t.arrive - .4) / .3)), north = sa > .35;
        const lit = charging ? .4 * charge : s >= t.fire ? .4 * (1 - clamp((s - t.fire) / .4)) : 0;
        if (north) pointingArm(place, p.aim, out, pawnLayer - .004);
        if (p.actors) caster(g.pos, sun, strength, { tint: Red, tintAmount: lit });
        if (!north) pointingArm(place, p.aim, out, pawnLayer + .016);
        return;
      }
      if (g.kind === 'target') {
        const edge = burst ? 1 - clamp(age / .4) : 0, lit = burst ? .75 * (1 - clamp(age / .6)) : litByOrb(g.pos);
        if (slamAge >= 0) {
          // On the wall: squashed at the height he hit, then slides down and stands at its foot.
          if (slamAge < Squash) { const k = smooth(slamAge / .05), at = p.dist + stop + .06 * k; if (p.actors) squashed(place(at, 0, slamUp), place(at), Color.Lerp(EnemyColour, Red, lit), k, p.aim, sun, strength); return; }
          const u = smooth((slamAge - Squash) / .15), at = place(p.dist + stop - .2 * u, 0, slamUp * (1 - u));
          redEdge(at, edge);
          if (p.actors) pawn(at, EnemyColour, sun, strength, { tint: Red, tintAmount: lit });
          return;
        }
        // Standing until hit; lying while he flies and skids; up again 0.15 s after he stops.
        const up = !burst || age > t.fly + .15;
        if (up) { redEdge(g.pos, edge); if (p.actors) pawn(g.pos, EnemyColour, sun, strength, { tint: Red, tintAmount: lit }); return; }
        flung(place(p.dist + gone, 0, h), g.pos, h, p.aim, EnemyColour, lit, edge, 18 * Math.sin(age * 22) * (h > .05 ? 1 : 0), sun, strength, p.actors);
        return;
      }
      const lit = burst ? (g.inside ? .6 : .35) * (1 - clamp(age / .6)) : litByOrb(g.pos);
      if (g.inside && burst) redEdge(g.pos, 1 - clamp(age / .2));
      if (p.actors) pawn(g.pos, EnemyColour, sun, strength, { tint: Red, tintAmount: lit });
    });

    // --- the charge: swirl at the finger, ribbon arcs round Gojo, the orb --------------------------------------------------
    const arcFade = s < t.fire ? smooth(charge / .25) : 1 - clamp((s - t.fire) / .15);
    if (s >= Start && arcFade > 0) {
      const C = place(0, 0, ChestUp), sweep = ArcSweep * D2R * smooth(Math.min(1, charge));
      Arcs.forEach(([R, from], i) => ribbon(`red2 arc ${i}`, C, R, from * D2R - sweep, 2.1, arcFade * (.75 + .25 * Math.sin(s * 17 + i))));
    }
    if (charging) {
      const u = charge, env = smooth(u / .25);
      swirl('red2 swirl', F, SwirlFrom + (SwirlTo - SwirlFrom) * smooth(u), -s * SwirlSpin * D2R, env);
      // Red specks drawn in along short spirals.
      for (let i = 0; i < 10; i++) {
        const ph = (s / .32 + rand(i + 200)) % 1, rr = .15 + (1 - ph) * 1.2, q = rand(i + 210) * TAU + ph * 1.4, q2 = q - .25, rr2 = rr + .16;
        streak(`red2 gather ${i}`, { x: F.x + Math.cos(q) * rr, z: F.z + Math.sin(q) * rr }, { x: F.x + Math.cos(q2) * rr2, z: F.z + Math.sin(q2) * rr2 },
          .035, (i % 2 ? HotPink : Red).withAlpha(env * Math.sin(ph * Math.PI)), whiteGlow, Y + .098, 3);
      }
      const r = OrbR * smooth(clamp(u / .6)), hot = clamp((u - .75) / .25), ringK = clamp((s - (t.fire - .2)) / .08);
      crossLines('red2 charge line', F, 4, .9 + .9 * u, .025, .75 * clamp((u - .3) / .3), s * .5, 300);
      if (ringK > 0) {
        sprite(F, r * 5.4, r * 5.4, Red.withAlpha(.3 * ringK), glow, Y + .103);
        ringAt(F, r * 2.2, White.withAlpha(.6 * ringK), Y + .104, false, whiteGlow);
      }
      redOrb('red2 charge orb', F, r, s, hot, env);
    }

    // --- fire: the flash at the finger ------------------------------------------------------------------------------------
    if (s >= t.fire && s < t.fire + Flash) {
      const f = (s - t.fire) / Flash, size = .4 + 1.1 * (1 - f);
      sprite(F, size, size, HotPink.withAlpha(.9 * (1 - f)), glow, Y + .2);
      ringAt(F, .2 + .9 * smooth(f), Red.withAlpha(.8 * (1 - f)), Y + .19, true, whiteGlow);
    }

    // --- flight: the ball, its tail, ink streaks, the wake of pushed dust, specks shed behind ------------------------------
    if (flying) {
      const at = place(d, 0, ChestUp);
      const tail = L => { const back = Math.max(Tip, d - L); return [at, place((d + back) / 2, 0, ChestUp), place(back, 0, ChestUp)]; };
      line('red2 tail shade', tail(2.4), .6, RedDeep.withAlpha(.45), undefined, Y + .092, 'end');
      line('red2 tail wide', tail(2.2), .7, Red.withAlpha(.25), whiteGlow, Y + .095, 'end');
      line('red2 tail', tail(1.6), .3, Red.withAlpha(.6), whiteGlow, Y + .096, 'end');
      line('red2 tail core', tail(1), .08, HotPink.withAlpha(.9), whiteGlow, Y + .097, 'end');
      // Dark-red ink streaks (the anime's smear), redrawn at new places 24 times a second.
      const step = Math.floor(s * 24);
      for (let i = 0; i < InkStreaks; i++) {
        const k = step * 13 + i, across = (rand(k + 500) - .5) * 1.1, len = .9 + 1.8 * rand(k + 510), lag = .15 + .5 * rand(k + 520);
        const head = d - lag, back = Math.max(Tip, head - len);
        if (head > back + .15) streak(`red2 ink ${i}`, place(back, across, ChestUp), place(head, across, ChestUp), .03 + .03 * rand(k + 530), Ink2.withAlpha(.75), undefined, Y + .093, 3);
      }
      for (let i = 0; i < 4; i++) {
        const across = (i % 2 ? 1 : -1) * (.16 + .3 * rand(i + 230)), len = .8 + 1.4 * rand(i + 240), lag = .2 + .6 * rand(i + 250), flick = .6 + .4 * Math.sin(s * 31 + i * 2.3);
        const head = d - lag, back = Math.max(Tip, head - len);
        if (head > back + .1) streak(`red2 speed ${i}`, place(back, across, ChestUp), place(head, across, ChestUp), .035, Red.withAlpha(.7 * flick), whiteGlow, Y + .094, 3);
      }
      flightOrb('red2 flight', at, FlightR, s, sun);
    }
    // The wake: the air Red pushes aside throws dust out from its path, one pair of puffs and two flicked
    // specks every WakeStep cells, each moving out WakeOut cells over WakeLife.
    for (let i = 0; ; i++) {
      const along = 1.3 + i * WakeStep;
      if (along > p.dist - .2) break;
      const born = t.fire + (along - Tip) / Speed, u = (s - born) / WakeLife;
      if (u < 0 || u >= 1) continue;
      [-1, 1].forEach(side => {
        const out = side * (.2 + WakeOut * (1 - (1 - u) ** 2)), back = along - .25 * u, size = .25 + .35 * u;
        sprite(place(back, out, .05 * u), size, size * .8, WakeDust.withAlpha(.55 * Math.sin(u * Math.PI) * (.7 + .3 * rand(i * 2 + side + 600))), soft, Floor + .04);
        const fl = .15 + WakeOut * 1.3 * (1 - (1 - u) ** 2), hh = .25 * Math.sin(u * Math.PI);
        streak(`red2 wake ${i} ${side}`, place(back + .05, side * (fl - .12), hh), place(back - .05, side * fl, hh), .03, Ink.withAlpha(.6 * (1 - u)), undefined, Floor + .045, 3);
      });
    }
    for (let i = 0; i < 12; i++) {
      const born = t.fire + (i + .5) / 12 * (t.arrive - t.fire), sAge = s - born;
      if (sAge < 0 || sAge > .3) continue;
      const u = sAge / .3, along = Tip + (born - t.fire) * Speed - sAge * 2.5, across = (rand(i + 260) - .5) * (.3 + 2.2 * u), up = ChestUp + (rand(i + 270) - .5) * .5 * u;
      streak(`red2 shed ${i}`, place(along - .18, across * 1.05, up), place(along, across, up), .04, (i % 3 ? Red : HotPink).withAlpha(1 - u), whiteGlow, Y + .093, 3);
    }

    // --- the burst -----------------------------------------------------------------------------------------------------------
    if (burst) {
      // The ground washed red (the anime's red frames).
      if (age < WashTime) {
        const w = (1 - smooth(age / WashTime)) * clamp(.5 + age / .04), size = p.washR * 2;
        sprite(B, size * 1.3, size * 1.3, RedDeep.withAlpha(.55 * w), soft, Y + .001);
        sprite(B, size * .8, size * .8, RedDeep.withAlpha(.35 * w), soft, Y + .0015);
        sprite(B, size * 1.4, size * 1.4, Red.withAlpha(.6 * w), glow, Y + .002);
        sprite(B, size * .7, size * .7, Red.withAlpha(.45 * w), glow, Y + .0025);
      }
      if (age < .16) {
        const f = age / .16, a1 = .35 + .9 * (1 - f), a2 = .8 + 2.2 * (1 - f);
        sprite(B, a2, a2, HotPink.withAlpha(.6 * (1 - f)), glow, Y + .199);
        sprite(B, a1, a1, White.withAlpha(.85 * (1 - f)), glow, Y + .2);
      }
      // The burst radius on the floor, faint: the ring opens to it and fades there.
      const rr = p.burstR * (1 - (1 - clamp(age / RingTime)) ** 2), ra = age < RingTime ? 1 : 1 - smooth((age - RingTime) / RingHold);
      if (ra > 0) ringAt(Bf, rr, Red.withAlpha(.45 * ra), Floor + .03, false, whiteGlow);
      // The shock front: a red crescent across Red's line running ahead of the body, and a fainter echo behind it.
      if (age < frontTime + FrontFade) {
        const u = clamp(age / frontTime), k = age < frontTime ? 1 - .35 * u : .65 * (1 - (age - frontTime) / FrontFade);
        const R = FrontWide / 2 / Math.sin(FrontHalf * D2R) * (.6 + .4 * u), lead = p.dist + .2 + frontLen * u;
        [[0, 1], [.45, .45]].forEach(([back, share], j) => {
          const C = place(lead - back - R, 0, .35), pts = [];
          for (let m = 0; m <= 14; m++) { const q = a + (m / 14 * 2 - 1) * FrontHalf * D2R; pts.push({ x: C.x + Math.cos(q) * R, z: C.z + Math.sin(q) * R }); }
          line(`red2 front ${j} wide`, pts, .5, Red.withAlpha(.3 * k * share), whiteGlow, Y + .14, 'both');
          line(`red2 front ${j}`, pts, .2, Red.withAlpha(.7 * k * share), whiteGlow, Y + .141, 'both');
          line(`red2 front ${j} core`, pts, .06, HotPink.withAlpha(.9 * k * share), whiteGlow, Y + .142, 'both');
        });
      }
      // The front hitting the wall: a red flash on the face.
      if (wall(p) && age >= frontTime && age < frontTime + .15) {
        const f = (age - frontTime) / .15, W = place(face - .1, 0, .35);
        sprite(W, 1.1 + .6 * f, 1.8 + .6 * f, Red.withAlpha(.7 * (1 - f)), glow, Y + .143, -p.aim);
      }
      // Red sparks thrown forward along Red's line.
      if (age < .35) for (let i = 0; i < Sparks; i++) {
        const q = a + (rand(i + 280) - .5) * 100 * D2R, u = smooth(age / .35), r0 = .25 + (2.2 + 1.8 * rand(i + 290)) * u, len = (.3 + .5 * rand(i + 300)) * (1 - .5 * u);
        const x0 = B.x + Math.cos(q) * r0, z0 = B.z + Math.sin(q) * r0;
        streak(`red2 burst spark ${i}`, { x: x0 - Math.cos(q) * len, z: z0 - Math.sin(q) * len }, { x: x0, z: z0 }, .045, (i % 3 ? Red : HotPink).withAlpha(1 - age / .35), whiteGlow, Y + .15, 3);
      }
      // Dust: a small ring out to the burst radius, and a fan thrown forward.
      if (age < .7) {
        for (let i = 0; i < 8; i++) {
          const u = clamp((age - rand(i + 310) * .05) / .55), q = i * TAU / 8 + rand(i + 320), r0 = .3 + p.burstR * .9 * smooth(u);
          sprite({ x: Bf.x + Math.cos(q) * r0, z: Bf.z + Math.sin(q) * r0 * .8 + u * .1 }, .3 + .45 * u, .25 + .35 * u, Dust.withAlpha(.4 * Math.sin(u * Math.PI)), soft, Floor + .04);
        }
        for (let i = 0; i < 12; i++) {
          const u = clamp((age - rand(i + 460) * .08) / .62), q = a + (rand(i + 470) - .5) * 80 * D2R, r0 = .3 + (1.5 + 1.5 * rand(i + 480)) * (1 - (1 - u) ** 2);
          sprite({ x: Bf.x + Math.cos(q) * r0, z: Bf.z + Math.sin(q) * r0 + u * .15 }, .3 + .6 * u, .25 + .5 * u, Dust.withAlpha(.45 * Math.sin(u * Math.PI)), soft, Floor + .041);
        }
      }
      // Speed lines behind the thrown body, red and black, at its height.
      if (!empty(p) && age < t.fly + .05 && gone > .3 && (h > .02 || wall(p))) for (let i = 0; i < 6; i++) {
        const across = (i - 2.5) * .12 + (rand(i + 330) - .5) * .08, back = .8 + rand(i + 340) * 1.4, black = i % 2 === 0, up = h + .2;
        streak(`red2 thrown speed ${i}`, place(p.dist + Math.max(0, gone - back), across, up), place(p.dist + gone - .3, across, up), black ? .05 : .04,
          (black ? Ink : Red).withAlpha(.75), black ? undefined : whiteGlow, Y + .09, 3);
      }
      // Pushed pawns: a dust trail from their feet along the push.
      figures.filter(g => g.kind === 'other' && g.inside).forEach(g => {
        for (let k = 0; k < 6; k++) {
          const at = k / 5, when = PushTime * (1 - Math.sqrt(1 - at)), u = clamp((age - when) / .5);
          if (g.k < at || u >= 1) continue;
          const x = g.start.x + g.dir.x * p.pushCells * at, z = g.start.z + g.dir.z * p.pushCells * at;
          sprite({ x, z: z + u * .1 }, .25 + .3 * u, .2 + .25 * u, Dust.withAlpha(.45 * (1 - u)), soft, Floor + .042);
        }
      });
      // Landing in the open: dust where he first touches down and where he stops skidding.
      if (open(p)) [[t.dur * .5, p.throwCells * .75], [t.dur, p.throwCells]].forEach(([when, at], j) => {
        const u = clamp((age - when) / .6);
        if (u <= 0 || u >= 1) return;
        const c = place(p.dist + at);
        for (let k = 0; k < 6; k++) {
          const q = k * TAU / 6 + rand(k + 360 + j * 10), r0 = .2 + .5 * u;
          sprite({ x: c.x + Math.cos(q) * r0, z: c.z + Math.sin(q) * r0 * .7 }, .3 + .35 * u, .25 + .3 * u, Dust.withAlpha(.4 * Math.sin(u * Math.PI)), soft, Floor + .04);
        }
      });
    }

    // --- the wall slam: a big dust cloud along the face, chunks thrown back ---------------------------------------------------
    if (slamAge >= 0) {
      for (let i = 0; i < 18; i++) {
        const side = i % 2 ? 1 : -1, u = clamp((slamAge - rand(i + 30) * .06) / .9);
        if (u <= 0 || u >= 1) continue;
        const across = side * (.15 + 1.9 * rand(i + 40) * smooth(u)), along = face - .25 - .5 * rand(i + 50) - .3 * u, size = .45 + 1.05 * u;
        sprite(place(along, across, .25 * u), size, size * .8, WallDust.withAlpha(.65 * Math.sin(u * Math.PI)), soft, Y + .01);
      }
      for (let i = 0; i < 4; i++) {
        const u = clamp((slamAge - .05 * i) / 1.4), size = (1.3 + .8 * rand(i + 370)) * (.5 + .7 * smooth(u));
        if (u <= 0 || u >= 1) continue;
        sprite(place(face - .5 - .3 * rand(i + 380), (rand(i + 390) - .5) * 1.4, .3 + .5 * u), size, size * .85, WallDust.withAlpha(.6 * Math.sin(u * Math.PI)), soft, Y + .011);
      }
      for (let i = 0; i < Chips; i++) {
        const T = .3 + .15 * rand(i + 60), u = clamp(slamAge / T), la = face - .6 - 1.3 * rand(i + 80), lx = (rand(i + 90) - .5) * 2.2;
        const along = (face - .1) + (la - face + .1) * u, across = lx * u, hh = (slamUp + ChestUp) * (1 - u) + .35 * Math.sin(u * Math.PI), ground = place(along, across);
        if (u < 1) sprite({ x: ground.x + sun.x * hh, z: ground.z + sun.z * hh }, .18, .12, Ink.withAlpha(.35), soft, Floor + .05);
        rock(place(along, across, hh), .16 + .12 * rand(i + 100), rand(i + 110) * 360 + slamAge * 700 * (1 - u), 1, 2 * (i % 3), u < 1 ? Y + .05 : Floor + .06);
      }
    }
  },
};
