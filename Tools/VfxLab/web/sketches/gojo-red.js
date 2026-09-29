// Reversal: Red — Gojo kit, ability sketch, not the game.
//
// Mechanic (agreed 2026-09-27, docs/hero-echo.md; every number is a placeholder for XML):
//   Target a cell up to 20 away. Red flies there at 20 cells/s at chest height. The first pawn or
//   loose thing on its line is hit; if nothing is, it bursts at the cell. The thing hit is thrown 6
//   cells along Red's direction, 1.5 blunt per cell travelled, +10 if a wall stops it. Everything else
//   within 1.5 cells of the burst is pushed 2 cells straight away from it, 8 blunt. Cooldown 10 s,
//   3 charge. Shot through an active Blue it becomes Hollow Purple (not in this sketch).
//
// Source frames (checked 2026-09-29, YouTube 0jRyxkaWuU8 "Everytime Gojo uses Red", storyboards 320x180):
//   47-51 s  finger up, a small saturated-red orb just over the fingertip, darker rim, pale pink halo;
//            pale lilac needle flares round it; dark red smoke ribbons round the hand; the orb goes
//            white-hot just before it leaves
//   24-25 s  (S2, vs Toji) the orb as a target: red centre, white ring, red disc, thin straight red
//            lines crossing the frame through it
//   27-29 s  the hit: red spark streaks round the body, the body thrown with red speed lines
//   2-8 s    the scene washed red, then full-screen red, then a red-lit room with debris flying out
//   30-34 s  the thrown body hits a building: a big dust cloud
//   39 s     (Shibuya) arm straight out at the target, the orb at the hand, Gojo lit red
//
// Order (raider into a wall, the default; charge 0.5 s):
//   0.00        Gojo stands; a raider 9 cells out, a second raider beside him, a wall 4 cells behind
//   0.00-0.15   the arm comes up and points at the raider
//   0.10-0.60   charge: dark red ribbons curl in, red sparks gather, the orb grows past the fingertip,
//               lilac needles flicker, thin red lines cross through it, then a white ring and a red
//               ring round it (the Toji frame); the core goes white-hot at the end; Gojo lit red
//   0.60        fire: a white-pink flash at the finger, a red ring opens, a small shake
//   0.60-1.01   flight at 20 cells/s: the orb with a red tail, red speed lines, red light on the floor
//               under it, sparks shed behind
//   1.01        burst on the raider: the ground round it washed red for 0.45 s, a white core flash,
//               6 thin red lines crossing through the point, a white and a red ring open to the
//               1.5-cell burst radius on the floor and fade there, red sparks thrown forward, dust
//               blown outward; shake
//   1.01-       the raider thrown along Red's line with red speed lines behind him (6 cells, or less
//               if a wall is nearer); the second raider pushed 2 cells straight away from the burst
//   1.17        the raider hits the wall: squashes on its face, a dent, a big dust cloud, chips fly back;
//               he stays on his feet (the agreed mechanic has no stun)
//   stays       a dark red scorch at the burst point; drag marks where a thrown body skids to a stop
//   "group in the open": no wall; the raider lands 6 cells out; two others pushed 2 cells.
//   "empty cell": nothing on the line; the target cell is outlined, Red bursts there; one raider 1.2
//   cells away is pushed, one 2.5 cells away is only lit.
//
// Drawing: flat shapes and level circles only, so it turns with the aim and needs no per-facing
// method. Red travels at chest height (0.5 cells up, drawn 0.3 north) and is a light: it throws a red
// glow on the floor, not a shadow. The wash is a Transparent dark-red layer under an additive red
// layer, so it reads red on any ground. Gojo is lib/gojo.js's stand-in with a pointing arm drawn here;
// the raiders and the wall are stand-ins.
import { Color, Mathf, Meshes, MeshPool } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import { caster, Uniform } from './lib/gojo.js';
import { pawn, rock, ringAt, line, streak, whiteGlow, wallCell, EnemyColour, Ink, White, Dust, Lift, Chest, Skin, pawnLayer, shadowLayer, buildingLayer, clamp, smooth } from './lib/goku.js';

const TAU = Math.PI * 2, D2R = Mathf.Deg2Rad, ChestUp = Chest / Lift;
const disc = Meshes.disc(32, 'red disc');
// Decided values. Speed is the agreed 20 cells/s.
const Speed = 20, Start = .1, Raise = .15, Flash = .12, ThrowSpeed = 26, PushTime = .28, Tail = 1.6;
const OrbR = .17, Reach = .5, Tip = .8;              // orb radius; hand and orb distance from Gojo's centre
const WashTime = .45, RingTime = .12, RingHold = .5, Lines = 6, Sparks = 16, Squash = .1, Chips = 10;
const Red = new Color(1, .1, .14), RedDeep = new Color(.42, 0, .05), HotPink = new Color(1, .78, .82), Lilac = new Color(.98, .74, 1);
const Smoke = new Color(.28, .02, .05), Scorch = new Color(.22, .03, .04), WallDust = new Color(.74, .72, .69);
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

// Thin straight red lines crossing through a point (the Toji frame). half is each line's half length.
function crossLines(key, at, count, half, width, alpha, turn, seed) {
  if (alpha <= 0 || half <= .01) return;
  for (let i = 0; i < count; i++) {
    const q = (i * 180 / count + (rand(i + seed) - .5) * 30) * D2R + turn, l = half * (.7 + .3 * rand(i + seed + 7));
    const dx = Math.cos(q) * l, dz = Math.sin(q) * l;
    streak(`${key} ${i}`, { x: at.x - dx, z: at.z - dz }, { x: at.x + dx, z: at.z + dz }, width, Red.withAlpha(alpha), whiteGlow, Y + .102, 8);
  }
}

// The white ring and red ring round the orb, over a red disc of light.
function targetRings(at, r, k, layer = Y + .103) {
  if (k <= 0 || r <= 0) return;
  sprite(at, r * 5.4, r * 5.4, Red.withAlpha(.35 * k), glow, layer);
  ringAt(at, r * 2.7, Red.withAlpha(.6 * k), layer + .0005, false, whiteGlow);
  ringAt(at, r * 2, White.withAlpha(.7 * k), layer + .001, false, whiteGlow);
}

// Gojo's arm held out at the target: sleeve, hand, index finger. out 0..1.
function pointingArm(place, deg, out, layer) {
  if (out <= .02) return;
  const reach = Reach * out, sleeve = place(reach * .4, 0, ChestUp), hand = place(reach, 0, ChestUp), tip = place(reach + .16 * out, 0, ChestUp);
  draw(MeshPool.plane10, sleeve.x, layer, sleeve.z, .12, reach * .8, 90 - deg, Uniform);
  draw(disc, hand.x, layer + .001, hand.z, .06, .06, 0, Skin);
  draw(MeshPool.plane10, (hand.x + tip.x) / 2, layer + .0015, (hand.z + tip.z) / 2, .03, .16 * out, 90 - deg, Skin);
}

// The stand-in pawn squashed against a wall: both ellipses narrowed along the throw by k (0..1).
function squashed(pos, colour, k, deg, sun, strength) {
  const c = Math.abs(Math.cos(deg * D2R)), n = Math.abs(Math.sin(deg * D2R));
  const fx = (1 - .45 * k * c) * (1 + .15 * k * n), fz = (1 - .45 * k * n) * (1 + .15 * k * c);
  sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, .85, .4, Ink.withAlpha(strength), soft, shadowLayer);
  draw(disc, pos.x, pawnLayer, pos.z + .18, .22 * fx, .32 * fz, 0, colour);
  draw(disc, pos.x, pawnLayer + .002, pos.z + .18 + .4 * fz, .16 * fx, .17 * fz, 0, Skin);
}

export default {
  kit: 'Gojo', label: 'Reversal: Red (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'raider into a wall', options: ['raider into a wall', 'group in the open', 'empty cell'], group: 'Showcase' },
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
    const stop = stopAt(p), gone = thrown(p, age, t), slamAge = hitsWall(p) && burst && age >= t.fly ? age - t.fly : -1, face = p.dist + p.wallBehind - .5;

    // --- floor: red light under the orb, the target cell, the scorch and drag marks that stay ----------------------
    if (charging) sprite(place(Tip), 1.5 * charge, 1.1 * charge, Red.withAlpha(.3 * charge), glow, Floor + .03);
    if (flying) {
      sprite(place(d), 1.7, 1.2, Red.withAlpha(.3), glow, Floor + .03);
      line('red floor path', [place(d), place(Math.max(Tip, d - 3.5))], .5, Red.withAlpha(.14), whiteGlow, Floor + .025, 'end');
    }
    if (empty(p) && s < t.arrive + .1) {
      const k = smooth(s / .2) * (1 - smooth((s - t.arrive) / .1)), c = { x: Math.floor(Bf.x) + .5, z: Math.floor(Bf.z) + .5 };
      [[-1, -1, 1, -1], [1, -1, 1, 1], [1, 1, -1, 1], [-1, 1, -1, -1]].forEach(([x0, z0, x1, z1], i) =>
        line(`red cell ${i}`, [{ x: c.x + x0 * .5, z: c.z + z0 * .5 }, { x: c.x + x1 * .5, z: c.z + z1 * .5 }], .04, Red.withAlpha(.6 * k), whiteGlow, Floor + .02, 'none'));
    }
    if (burst) {
      const g = smooth(age / .1);
      sprite(Bf, 1.5 * g, 1.1 * g, Scorch.withAlpha(.4), soft, Floor + .01);
      sprite(Bf, .7 * g, .5 * g, Scorch.withAlpha(.55), soft, Floor + .011);
      const from = p.throwCells * .55;
      if (open(p) && gone > from) [-.12, .12].forEach((x, i) =>
        line(`red skid ${i}`, [place(p.dist + from, x), place(p.dist + gone, x)], .05, Ink.withAlpha(.3), undefined, Floor + .012, 'none'));
    }

    // --- the wall: shakes on the slam, a dent stays on its face ----------------------------------------------------------
    if (wall(p)) {
      const shake = slamAge >= 0 && slamAge < .15 ? .05 * Math.sin(slamAge * 95) * (1 - slamAge / .15) : 0;
      for (let k = -1; k <= 1; k++) wallCell(place(p.dist + p.wallBehind + shake, k), p.aim);
      if (slamAge >= 0) {
        const grow = smooth(slamAge / .06), D = place(face + .14, 0, ChestUp);
        sprite(D, .4, .6, Ink.withAlpha(.5 * grow), soft, buildingLayer + .004, -p.aim);
        sprite(D, .18, .3, Ink.withAlpha(.6 * grow), soft, buildingLayer + .0045, -p.aim);
      }
    }

    // --- the pawns, north first ----------------------------------------------------------------------------------------------
    const figures = [{ kind: 'gojo', pos: G }];
    if (!empty(p)) figures.push({ kind: 'target', pos: place(p.dist + gone) });
    Others[p.scenario].forEach(([along, across], i) => {
      const start = place(p.dist + along, across), dx = start.x - Bf.x, dz = start.z - Bf.z, r = Math.hypot(dx, dz) || 1, inside = r <= p.burstR;
      const k = burst && inside ? 1 - (1 - clamp(age / PushTime)) ** 2 : 0;
      figures.push({ kind: 'other', i, inside, start, pos: { x: start.x + dx / r * p.pushCells * k, z: start.z + dz / r * p.pushCells * k } });
    });
    const litByOrb = pos => flying ? .45 * (1 - clamp(Math.hypot(pos.x - place(d).x, pos.z - place(d).z) / 2.5)) : 0;
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      if (g.kind === 'gojo') {
        const out = smooth(s / Raise) * (1 - smooth((s - t.arrive - .4) / .3)), north = sa > .35;
        const lit = charging ? .4 * charge : s >= t.fire ? .4 * (1 - clamp((s - t.fire) / .4)) : 0;
        if (north) pointingArm(place, p.aim, out, pawnLayer - .004);
        caster(g.pos, sun, strength, { tint: Red, tintAmount: lit });
        if (!north) pointingArm(place, p.aim, out, pawnLayer + .016);
        return;
      }
      if (g.kind === 'target') {
        if (slamAge >= 0 && slamAge < Squash) { squashed(place(p.dist + stop + .06 * smooth(slamAge / .05)), EnemyColour, smooth(slamAge / .05), p.aim, sun, strength); return; }
        const pos = slamAge >= Squash ? place(p.dist + stop - .2 * smooth((slamAge - Squash) / .15)) : g.pos;
        const lit = burst ? .75 * (1 - clamp(age / .6)) : litByOrb(g.pos);
        pawn(pos, EnemyColour, sun, strength, { tint: Red, tintAmount: lit });
        return;
      }
      const lit = burst ? (g.inside ? .6 : .35) * (1 - clamp(age / .6)) : litByOrb(g.pos);
      pawn(g.pos, EnemyColour, sun, strength, { tint: Red, tintAmount: lit });
    });

    // --- the charge at the finger ----------------------------------------------------------------------------------------
    if (charging) {
      const u = charge, env = smooth(u / .25);
      // Dark red smoke ribbons curling in round the hand (normal blend, under the light).
      for (let i = 0; i < 3; i++) {
        const pts = [], a0 = i * TAU / 3 + s * 3.2, R0 = 1.05 - .45 * u;
        for (let k = 0; k <= 10; k++) { const v = k / 10, rr = .14 + (1 - v) * R0, q = a0 + v * 2.4; pts.push({ x: F.x + Math.cos(q) * rr, z: F.z + Math.sin(q) * rr * .85 }); }
        line(`red ribbon ${i}`, pts, .11 - .03 * u, Smoke.withAlpha(.4 * env), undefined, Y + .09, 'both');
      }
      // Red sparks drawn in along short spirals.
      for (let i = 0; i < 10; i++) {
        const ph = (s / .32 + rand(i + 200)) % 1, rr = .15 + (1 - ph) * 1.1, q = rand(i + 210) * TAU + ph * 1.4, q2 = q - .25, rr2 = rr + .16;
        streak(`red gather ${i}`, { x: F.x + Math.cos(q) * rr, z: F.z + Math.sin(q) * rr }, { x: F.x + Math.cos(q2) * rr2, z: F.z + Math.sin(q2) * rr2 },
          .035, (i % 2 ? HotPink : Red).withAlpha(env * Math.sin(ph * Math.PI)), whiteGlow, Y + .098, 3);
      }
      const r = OrbR * smooth(clamp(u / .6)), hot = clamp((u - .75) / .25);
      crossLines('red charge line', F, 4, .9 + .9 * u, .025, .75 * clamp((u - .3) / .3), s * .5, 300);
      targetRings(F, r, clamp((u - .45) / .3));
      redOrb('red charge orb', F, r, s, hot, env);
    }

    // --- fire: the flash at the finger ------------------------------------------------------------------------------------
    if (s >= t.fire && s < t.fire + Flash) {
      const f = (s - t.fire) / Flash, size = .4 + 1.1 * (1 - f);
      sprite(F, size, size, HotPink.withAlpha(.9 * (1 - f)), glow, Y + .2);
      ringAt(F, .2 + .9 * smooth(f), Red.withAlpha(.8 * (1 - f)), Y + .19, true, whiteGlow);
    }

    // --- flight: the orb, its tail, speed lines, sparks shed behind -------------------------------------------------------
    if (flying) {
      const at = place(d, 0, ChestUp);
      const tail = L => { const back = Math.max(Tip, d - L); return [at, place((d + back) / 2, 0, ChestUp), place(back, 0, ChestUp)]; };
      line('red tail wide', tail(2.6), .8, Red.withAlpha(.28), whiteGlow, Y + .095, 'end');
      line('red tail', tail(1.7), .32, Red.withAlpha(.6), whiteGlow, Y + .096, 'end');
      line('red tail core', tail(1), .1, HotPink.withAlpha(.9), whiteGlow, Y + .097, 'end');
      for (let i = 0; i < 6; i++) {
        const across = (i % 2 ? 1 : -1) * (.16 + .3 * rand(i + 230)), len = .8 + 1.4 * rand(i + 240), lag = .2 + .6 * rand(i + 250), flick = .6 + .4 * Math.sin(s * 31 + i * 2.3);
        const head = d - lag, back = Math.max(Tip, head - len);
        if (head > back + .1) streak(`red speed ${i}`, place(back, across, ChestUp), place(head, across, ChestUp), .035, Red.withAlpha(.7 * flick), whiteGlow, Y + .094, 3);
      }
      redOrb('red flight orb', at, OrbR, s, 1, .7);
    }
    for (let i = 0; i < 12; i++) {
      const born = t.fire + (i + .5) / 12 * (t.arrive - t.fire), sAge = s - born;
      if (sAge < 0 || sAge > .3) continue;
      const u = sAge / .3, along = Tip + (born - t.fire) * Speed - sAge * 2.5, across = (rand(i + 260) - .5) * (.3 + 2.2 * u), up = ChestUp + (rand(i + 270) - .5) * .5 * u;
      streak(`red shed ${i}`, place(along - .18, across * 1.05, up), place(along, across, up), .04, (i % 3 ? Red : HotPink).withAlpha(1 - u), whiteGlow, Y + .093, 3);
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
      if (age < .4) crossLines('red burst line', B, Lines, 3 * smooth(age / .06), .05, .9 * (1 - smooth(age / .4)), 0, 330);
      // The burst radius on the floor: a white and a red ring open to it and fade there.
      const rr = p.burstR * (1 - (1 - clamp(age / RingTime)) ** 2), ra = age < RingTime ? 1 : 1 - smooth((age - RingTime) / RingHold);
      if (ra > 0) {
        ringAt(Bf, rr * 1.05, Red.withAlpha(.6 * ra), Floor + .029, true, whiteGlow);
        ringAt(Bf, rr, White.withAlpha(.8 * ra), Floor + .03, false, whiteGlow);
      }
      if (age < .2) { const f = age / .2; targetRings(B, .2 + .4 * f, 1 - f, Y + .15); }
      if (age < .3) ringAt(Bf, .5 + 3 * smooth(age / .3), HotPink.withAlpha(.3 * (1 - age / .3)), Floor + .035, true, whiteGlow);
      // Red sparks thrown forward along Red's line.
      if (age < .35) for (let i = 0; i < Sparks; i++) {
        const q = a + (rand(i + 280) - .5) * 100 * D2R, u = smooth(age / .35), r0 = .25 + (2.2 + 1.8 * rand(i + 290)) * u, len = (.3 + .5 * rand(i + 300)) * (1 - .5 * u);
        const x0 = B.x + Math.cos(q) * r0, z0 = B.z + Math.sin(q) * r0;
        streak(`red burst spark ${i}`, { x: x0 - Math.cos(q) * len, z: z0 - Math.sin(q) * len }, { x: x0, z: z0 }, .045, (i % 3 ? Red : HotPink).withAlpha(1 - age / .35), whiteGlow, Y + .15, 3);
      }
      // Dust blown out along the floor.
      if (age < .6) for (let i = 0; i < 14; i++) {
        const u = clamp((age - rand(i + 310) * .05) / .55), q = i * TAU / 14 + rand(i + 320), r0 = .3 + p.burstR * 1.1 * smooth(u);
        sprite({ x: Bf.x + Math.cos(q) * r0, z: Bf.z + Math.sin(q) * r0 * .8 + u * .1 }, .3 + .5 * u, .25 + .4 * u, Dust.withAlpha(.45 * Math.sin(u * Math.PI)), soft, Floor + .04);
      }
      // Speed lines behind the thrown body, red and black.
      if (!empty(p) && age < t.fly + .05 && gone > .3) for (let i = 0; i < 6; i++) {
        const across = (i - 2.5) * .12 + (rand(i + 330) - .5) * .08, back = .8 + rand(i + 340) * 1.4, black = i % 2 === 0;
        streak(`red thrown speed ${i}`, place(p.dist + Math.max(0, gone - back), across, ChestUp), place(p.dist + gone - .3, across, ChestUp), black ? .05 : .04,
          (black ? Ink : Red).withAlpha(.75), black ? undefined : whiteGlow, Y + .09, 3);
      }
      // Pushed pawns: dust at their feet while they slide.
      Others[p.scenario].forEach((_, i) => {
        const g = figures.find(f => f.kind === 'other' && f.i === i);
        if (!g.inside || age > .45) return;
        for (let k = 0; k < 5; k++) {
          const u = clamp(age / .45), q = rand(i * 10 + k + 350) * TAU, r0 = .15 + .35 * u;
          sprite({ x: g.pos.x + Math.cos(q) * r0, z: g.pos.z + Math.sin(q) * r0 * .7 }, .25 + .3 * u, .2 + .25 * u, Dust.withAlpha(.4 * Math.sin(u * Math.PI)), soft, Floor + .04);
        }
      });
      // Landing in the open: a puff where he comes to rest.
      if (open(p) && age >= t.fly && age < t.fly + .6) {
        const u = (age - t.fly) / .6;
        for (let k = 0; k < 6; k++) {
          const q = k * TAU / 6 + rand(k + 360), r0 = .2 + .5 * u;
          sprite({ x: place(p.dist + stop).x + Math.cos(q) * r0, z: place(p.dist + stop).z + Math.sin(q) * r0 * .7 }, .3 + .35 * u, .25 + .3 * u, Dust.withAlpha(.4 * Math.sin(u * Math.PI)), soft, Floor + .04);
        }
      }
    }

    // --- the wall slam: a big dust cloud along the face, chips thrown back ---------------------------------------------------
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
        const T = .3 + .15 * rand(i + 60), u = clamp(slamAge / T), la = face - .6 - 1.1 * rand(i + 80), lx = (rand(i + 90) - .5) * 2;
        const along = (face - .1) + (la - face + .1) * u, across = lx * u, h = ChestUp * (1 - u) + .35 * Math.sin(u * Math.PI), ground = place(along, across);
        if (u < 1) sprite({ x: ground.x + sun.x * h, z: ground.z + sun.z * h }, .14, .09, Ink.withAlpha(.35), soft, Floor + .05);
        rock(place(along, across, h), .12 + .07 * rand(i + 100), rand(i + 110) * 360 + slamAge * 700 * (1 - u), 1, 2 * (i % 3), u < 1 ? Y + .05 : Floor + .06);
      }
    }
  },
};
