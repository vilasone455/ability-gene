// Hollow Purple — Gojo kit, combo sketch, not the game.
//
// Mechanic (agreed 2026-09-27, docs/hero-echo.md; numbers are placeholders for XML):
//   A combo, not a counted ability: when Red passes within 1 cell of an active Blue's centre and Purple is
//   ready, both are used up and Purple forms at Blue's centre, travelling in Red's direction: radius 1.5,
//   6 cells/s, 30 cells or the map edge. It erases what it touches: walls, buildings, plants and items are
//   destroyed with no drops; pawns take 60 erasure damage that ignores armour, friend or foe. Its own
//   cooldown is 1 day plus 20 charge; if it is not ready or the pool cannot pay, Red passes through Blue and
//   pushes as normal. The floor is not changed by the mechanic; the trench here is a mark only.
//
// Sources (YouTube storyboards, 2026-09-29):
//   anime S1 ep 20, U2ja8ZLRwrA  17-20 s a blue swirl on one side, a red swirl on the other; 28-30 s they
//                   meet in the middle, a sphere half red half blue; 38 s a purple swirl; 41-44 s purple
//                   lightning, a big purple sphere with a bright edge; 46-53 s it crosses the forest slowly,
//                   everything lit purple, a purple trail; 61-64 s a straight trench with flat walls
//   anime S2 ep 4, Ybn2csWuIgg   #30-32 red and blue rings swirling into each other; #33-36 a white point
//                   at the fingers, purple ripple rings spreading, rays; #38 buildings lit purple; #39-41
//                   purple light rays streaming along the path; #44 white-out
//
// Order ("wall and raiders", the default; Blue 6 cells out, Purple travels 14 cells):
//   0.00-0.65   Blue is already open 6 cells out (its core sphere, fog, streaks, as Blue v2 but lighter);
//               Gojo points, Red charges at his finger (red swirl, orb growing)
//   0.65-0.91   Red flies at 20 cells/s into Blue
//   0.91-1.26   merge: the red orb and the blue sphere spiral round each other and in; 6 red and blue
//               arcs swirl round them; the world split red on one side and blue on the other (8-cell
//               lights and tints), a red and a cyan half ring 2.2 cells out spinning opposite ways;
//               over the last 0.15 s everything within 10 cells darkens (the world holds its breath)
//   1.26        ignition: a white point and flash, a purple wash 18 cells across fading in 0.5 s, 4 purple
//               ripple rings spreading to 3 cells, 12 rays
//   1.26-1.56   Purple grows to 1.5 cells
//   1.56-3.89   it travels 14 cells at 6 cells/s: a deep purple sphere lit from its edge, violet swirl
//               bands, a white core with rays, 5 lightning bolts crackling off it, a ripple ring every
//               0.5 s, light rays along the path, a purple wash on the ground, the trench behind it with
//               glowing edges that fade (a pale scoured strip, half transparent); a small shake every 0.5 s.
//               Travel and erasing (S1 ep 20 #47-48, #61-63): under the sphere the ground glows lilac, a white
//               cut line runs round the front of its footprint and 18 specks of earth lift into it; behind it
//               a band of purple haze rises along the cut and fades over 1.1 s; the cut edges glow white ->
//               lilac -> violet and are dark 1.5 s after it passed; the trench shows its north wall (0.6 cells
//               deep, drawn 0.36 north as earth with a dark foot and a pale lip); dust rises off both lips
//               0.25 s after it passed and fades over 2 s. Whatever it touches dissolves into purple specks drawn into it: a
//               tree at 2.5 cells, a raider at 4 (60 damage: flash, falls), an ally at 6.5 (the same, friend
//               or foe), the 3 middle cells of a 7-cell wall at 9 (the cut faces glow and fade), a crate at
//               12.5. A raider 2.4 cells off the line is only lit.
//   3.89-4.39   it breaks into specks and fades; a last ring
//   stays       the trench, the cut wall ends, the fallen pawns
//   "open field": nothing on the path, to see Purple alone.
//
// Drawing: level circles, spirals, strips and quads; nothing per facing (the path and the wall turn with the
// aim). Blue and Red are compact copies of the Blue v2 and Red v2 looks, not the full effects. Light is
// additive; the sphere's body, the trench and the burns are Transparent. Pawns, tree, wall, crate are stand-ins.
import { Color, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../js/engine.js';
import { draw, mesh } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import { caster, Uniform } from './lib/gojo.js';
import { Blue, Deep, Ice } from './lib/vergil.js';
import { pawn, ringAt, line, strip, glint, whiteGlow, wallCell, EnemyColour, Ally, Ink, White, Lift, Chest, Skin, pawnLayer, buildingLayer, clamp, smooth } from './lib/goku.js';

const TAU = Math.PI * 2, D2R = Mathf.Deg2Rad, ChestUp = Chest / Lift;
const disc = Meshes.disc(40, 'purple disc');
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent), puffGlow = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.MoteGlow);
// Decided values. Red's speed is the agreed 20 cells/s.
const Raise = .15, Start = .15, RedSpeed = 20, Tip = .8, Reach = .5, Grow = .3, Fade = .5, Tail = 1.2;
const BlueR = .9, OrbR = .2, Bands = 12, Bolts = 5, Rays = 10, RingEvery = .5, Specks = 24;
// Travel and erasing (S1 ep 20 #47-48 the purple band behind the head, #61-63 the trench walls and dust).
const TrenchDepth = .6, BandStep = .4, BandLife = 1.1, EdgeStep = .4, EdgeCool = 1.5, Lifted = 18, DustStep = .45, DustLife = 2;
const Red = new Color(1, .1, .14), RedDeep = new Color(.42, 0, .05), HotPink = new Color(1, .78, .82);
const Royal = new Color(.12, .32, .95), Abyss = new Color(.01, .03, .16), Cyan = new Color(.45, .85, 1);
const Violet = new Color(.58, .22, 1), Lilac = new Color(.86, .72, 1), Plum = new Color(.2, .03, .36), Night = new Color(.06, 0, .12), Magenta = new Color(.95, .35, 1);
const Scoured = new Color(.3, .25, .23), Earth = new Color(.38, .3, .24), Lip = new Color(.62, .56, .5), Haze = new Color(.72, .67, .6), Bark = new Color(.35, .24, .14), Leaves = new Color(.25, .45, .2), Crate = new Color(.55, .4, .22);
// Things on the path for "wall and raiders": [cells past Blue's centre along the path, cells across, kind].
const Path = [[2.5, .5, 'tree'], [4, -.3, 'raider'], [6.5, .8, 'ally'], [11, 2.4, 'raider'], [12.5, -.6, 'crate']];
const WallAt = 9, WallCells = [-3, -2, -1, 0, 1, 2, 3];

const field = p => p.scenario === 'open field';
function times(p) {
  const fire = Start + p.charge, contact = fire + Math.max(0, p.blueDist - Tip) / RedSpeed, ignite = contact + p.merge;
  const move = ignite + Grow, stop = move + p.travel / p.speed;
  return { fire, contact, ignite, move, stop, gone: stop + Fade, end: stop + Fade + Tail };
}

function pointingArm(place, deg, out, layer) {
  if (out <= .02) return;
  const reach = Reach * out, sleeve = place(reach * .4, 0, ChestUp), hand = place(reach, 0, ChestUp), tip = place(reach + .16 * out, 0, ChestUp);
  draw(MeshPool.plane10, sleeve.x, layer, sleeve.z, .12, reach * .8, 90 - deg, Uniform);
  draw(disc, hand.x, layer + .001, hand.z, .06, .06, 0, Skin);
  draw(MeshPool.plane10, (hand.x + tip.x) / 2, layer + .0015, (hand.z + tip.z) / 2, .03, .16 * out, 90 - deg, Skin);
}

// Red: a shaded red ball in a red corona with a white-hot glint (Red v2's flight look, compact).
function redBall(at, r, alpha = 1) {
  if (r <= .01 || alpha <= 0) return;
  sprite(at, r * 7, r * 7, Red.withAlpha(.45 * alpha), glow, Y + .12);
  draw(disc, at.x, Y + .121, at.z, r * 1.1, r * 1.1, 0, RedDeep.withAlpha(alpha));
  draw(disc, at.x + r * .08, Y + .122, at.z + r * .08, r * .85, r * .85, 0, Red.withAlpha(alpha));
  draw(disc, at.x + r * .35, Y + .123, at.z + r * .38, r * .3, r * .26, 0, HotPink.withAlpha(.85 * alpha));
  sprite(at, r * 1.4, r * 1.4, HotPink.withAlpha(.35 * alpha), glow, Y + .124);
}

// Blue's core sphere, compact (Blue v2): royal body darkening inward, lit edge, whirlpool bands, the eye.
function blueSphere(key, at, R, s, alpha = 1) {
  if (R <= .01 || alpha <= 0) return;
  sprite(at, R * 4.4, R * 4.4, Blue.withAlpha(.4 * alpha), glow, Y + .1);
  draw(disc, at.x, Y + .101, at.z, R, R, 0, Royal.withAlpha(.92 * alpha));
  sprite(at, R * 1.9, R * 1.9, Abyss.withAlpha(.9 * alpha), soft, Y + .1015);
  [[.985, .38], [.95, .28], [.91, .19], [.87, .12]].forEach(([f, a], j) => ringAt(at, R * f, Cyan.withAlpha(a * alpha), Y + .102 + j * .0001, false, whiteGlow));
  for (let k = 0; k < 8; k++) {
    const v = k / 7, rr = R * (.3 + .62 * v), a0 = rand(k + 1250) * TAU - s * 3.2 * Math.pow(R / rr, 1.1), pts = [];
    for (let m = 0; m <= 8; m++) { const q = a0 + 1.2 * m / 8; pts.push({ x: at.x + Math.cos(q) * rr, z: at.z + Math.sin(q) * rr }); }
    line(`${key} band ${k}`, pts, R * .07, Color.Lerp(Cyan, Ice, v).withAlpha(.42 * alpha), whiteGlow, Y + .103 + k * .0002, 'both');
  }
  sprite(at, R * .22, R * .22, White.withAlpha(alpha), glow, Y + .105);
}

// Purple: a deep plum body lit from its edge in lilac, violet bands turning, a white core with rays,
// lightning crackling off it, specks drawn in. R is the radius; s drives the motion.
function purpleSphere(key, at, R, s, alpha = 1) {
  if (R <= .01 || alpha <= 0) return;
  const beat = 1 + .06 * Math.sin(s * 13);
  sprite(at, R * 4.2 * beat, R * 4.2 * beat, Violet.withAlpha(.45 * alpha), glow, Y + .15);
  sprite(at, R * 2.6, R * 2.6, Magenta.withAlpha(.25 * alpha), glow, Y + .1505);
  draw(disc, at.x, Y + .151, at.z, R, R, 0, Plum.withAlpha(.95 * alpha));
  sprite(at, R * 1.8, R * 1.8, Night.withAlpha(.8 * alpha), soft, Y + .1515);
  [[.985, .5], [.95, .36], [.91, .24], [.87, .15], [.83, .09]].forEach(([f, a], j) => ringAt(at, R * f, Lilac.withAlpha(a * alpha), Y + .152 + j * .0001, false, whiteGlow));
  ringAt(at, R * 1.02, Violet.withAlpha(.4 * alpha), Y + .1526, true, whiteGlow);
  for (let k = 0; k < Bands; k++) {
    const v = k / (Bands - 1), rr = R * (.28 + .64 * v), a0 = rand(k + 2050) * TAU - s * 2.6 * Math.pow(R / rr, 1.1), span = .9 + .8 * rand(k + 2060), pts = [];
    for (let m = 0; m <= 8; m++) { const q = a0 + span * m / 8, wob = 1 + .04 * Math.sin(m * 1.7 + s * 6 + k); pts.push({ x: at.x + Math.cos(q) * rr * wob, z: at.z + Math.sin(q) * rr * wob }); }
    line(`${key} band ${k}`, pts, R * (.04 + .04 * rand(k + 2070)), Color.Lerp(Magenta, Lilac, v).withAlpha(.5 * alpha), whiteGlow, Y + .153 + k * .0002, 'both');
  }
  for (let i = 0; i < Specks; i++) {
    const d = R * (.4 + .6 * rand(i + 2100)), q = rand(i + 2110) * TAU - s * 2.6 * Math.pow(R / d, 1.1), tw = .3 + .7 * Math.abs(Math.sin(s * 13 + i * 1.7));
    sprite({ x: at.x + Math.cos(q) * d, z: at.z + Math.sin(q) * d }, .08, .08, (i % 3 ? White : Lilac).withAlpha(tw * alpha), glow, Y + .156);
  }
  // The core: a white point with short rays, beating.
  sprite(at, R * .9 * beat, R * .9 * beat, Lilac.withAlpha(.6 * alpha), glow, Y + .157);
  sprite(at, R * .38 * beat, R * .38 * beat, White.withAlpha(alpha), glow, Y + .1572);
  for (let i = 0; i < 4; i++) {
    const q = (i * 45 + 20 * Math.sin(s * 3)) * D2R, len = R * (.3 + .25 * Math.abs(Math.sin(s * 17 + i * 2.1)));
    line(`${key} core ray ${i}`, [{ x: at.x - Math.cos(q) * len, z: at.z - Math.sin(q) * len }, at, { x: at.x + Math.cos(q) * len, z: at.z + Math.sin(q) * len }], R * .04, White.withAlpha(.6 * alpha), whiteGlow, Y + .1574, 'both');
  }
  // Lightning off the edge, redrawn 20 times a second.
  const step = Math.floor(s * 20);
  for (let i = 0; i < Bolts; i++) {
    const kk = step * 7 + i, q0 = rand(kk + 2200) * TAU, len = R * (.5 + .7 * rand(kk + 2210)), pts = [];
    for (let m = 0; m <= 6; m++) {
      const v = m / 6, d = R * .9 + len * v, off = m ? (rand(kk * 11 + m + 2220) - .5) * .35 * R * v : 0;
      pts.push({ x: at.x + Math.cos(q0) * d - Math.sin(q0) * off, z: at.z + Math.sin(q0) * d + Math.cos(q0) * off });
    }
    line(`${key} bolt ${i}`, pts, .1, Violet.withAlpha(.7 * alpha), whiteGlow, Y + .158, 'end');
    line(`${key} bolt ${i} core`, pts, .035, White.withAlpha(.9 * alpha), whiteGlow, Y + .1582, 'end');
  }
}

// A thing dissolving: specks of its colour turning purple, drawn toward pull (the sphere's centre) and up.
function dissolve(key, at, size, age, colour, pull, count = 12) {
  if (age < 0 || age > .5) return;
  const u = age / .5;
  for (let i = 0; i < count; i++) {
    const ox = (rand(i + 2300) - .5) * size, oz = (rand(i + 2310) - .5) * size, k = smooth(clamp(u * (1.2 + rand(i + 2320))));
    const x = at.x + ox + (pull.x - at.x - ox) * k * .8, z = at.z + oz + (pull.z - at.z - oz) * k * .8 + k * .3;
    sprite({ x, z }, .12 * (1 - .5 * u), .12 * (1 - .5 * u), Color.Lerp(colour, Lilac, k).withAlpha(1 - u), glow, Y + .14 + i * .0001);
  }
}

// The trench mark: one mesh, a half circle of radius R behind 'from' and a straight band to 'to', so
// the rounded start is not drawn twice. dir is the path angle in radians.
function capsule(key, from, to, R, dir, colour, layer) {
  const vertices = [from.x, from.z], tri = [], n = 12, nx = -Math.sin(dir), nz = Math.cos(dir);
  for (let i = 0; i <= n; i++) { const q = dir + Math.PI / 2 + Math.PI * i / n; vertices.push(from.x + Math.cos(q) * R, from.z + Math.sin(q) * R); }
  vertices.push(to.x - nx * R, to.z - nz * R, to.x + nx * R, to.z + nz * R);
  for (let i = 1; i <= n; i++) tri.push(0, i, i + 1);
  tri.push(0, n + 1, n + 2, 0, n + 2, n + 3, 0, n + 3, 1);
  const m = mesh(key); m.setFlat(vertices, tri);
  draw(m, 0, layer, 0, 1, 1, 0, colour);
}

export default {
  kit: 'Gojo', label: 'Hollow Purple (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'wall and raiders', options: ['wall and raiders', 'open field'], group: 'Showcase' },
    aim: P('Aim (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    blueDist: P('Blue from Gojo (cells)', 6, 3, 15, .5, 'Showcase'),
    travel: P('Purple travels (cells, up to 30 or the map edge)', 14, 4, 30, 1, 'Showcase'),
    charge: P('Red charge', .5, .2, 1, .05, 'Timing (s)'),
    merge: P('Red and Blue merge', .35, .15, 1, .05, 'Timing (s)'),
    speed: P('Purple speed (cells/s)', 6, 2, 15, .5, 'Mechanic'),
    radius: P('Purple radius (cells)', 1.5, .75, 3, .25, 'Mechanic'),
    trench: { label: 'Trench mark on the floor', value: true, group: 'Shape' },
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Blue', t: 0 }, { name: 'Red', t: t.fire }, { name: 'Merge', t: t.contact }, { name: 'Ignite', t: t.ignite }, { name: 'Travel', t: t.move }, { name: 'Fade', t: t.stop }];
  },
  events(p) {
    const t = times(p);
    const rumble = [];
    for (let r = t.move + .5; r < t.stop; r += .5) rumble.push({ t: r, type: 'shake', value: .035 });
    return [{ t: t.fire, type: 'shake', value: .03 }, { t: t.contact, type: 'shake', value: .05 }, { t: t.ignite, type: 'shake', value: .16 }, ...rumble];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * D2R, ca = Math.cos(a), sa = Math.sin(a);
    // The chosen cell is the middle of Gojo and the end of Purple's run. G is Gojo; along runs down Red's line.
    const span = p.blueDist + p.travel, G = { x: o.x - ca * span / 2, z: o.z - sa * span / 2 };
    const place = (along, across = 0, up = 0) => ({ x: G.x + along * ca - across * sa, z: G.z + along * sa + across * ca + up * Lift });
    const B = p.blueDist, R = p.radius;
    // Purple's centre, cells past Gojo, and its radius now.
    const grown = smooth((s - t.ignite) / Grow), fading = s >= t.stop ? 1 - smooth((s - t.stop) / Fade) : 1;
    const at = B + Math.min(p.travel, Math.max(0, s - t.move) * p.speed), size = R * grown * (s >= t.stop ? .6 + .4 * fading : 1);
    const purpleOn = s >= t.ignite && s < t.gone, P0 = place(at, 0, ChestUp);
    // When the sphere reaches a thing at (along, across): its centre is within the radius of it.
    const hitAt = (along, across) => Math.abs(across) >= R ? Infinity : t.move + Math.max(0, along - B - Math.sqrt(R * R - across * across)) / p.speed;

    // --- floor: the trench, a cut with a wall you can see; the cut edges cooling; the ground being erased ------------
    const passedAt = x => t.move + Math.max(0, x - B) / p.speed;   // when the sphere's centre passed a point on the path
    const mv = s >= t.move && s < t.gone ? smooth((s - t.move) / .3) * fading : 0;
    if (p.trench && s >= t.move) {
      const from = B, to = at;
      if (to > from + .05) {
        capsule('purple trench', place(from), place(to), R, a, Scoured.withAlpha(.5), Floor + .012);
        capsule('purple trench floor', place(from), place(to), R * .9, a, Night.withAlpha(.12), Floor + .0122);
        [-1, 1].forEach(k => {
          const top = [place(from, k * R), place(to, k * R)], north = k * ca > .05;
          if (north) {
            // The wall below the north lip is seen from above: a band TrenchDepth x 0.6 tall, earth, darker at its foot.
            const drop = TrenchDepth * Lift, foot = top.map(q => ({ x: q.x, z: q.z - drop }));
            strip(`purple trench wall ${k}`, top, foot, Earth.withAlpha(.9), undefined, Floor + .0126);
            line(`purple trench foot ${k}`, foot, .07, Night.withAlpha(.5), undefined, Floor + .0127, 'none');
            line(`purple trench lip ${k}`, top, .05, Lip.withAlpha(.6), undefined, Floor + .0128, 'none');
          } else line(`purple trench lip ${k}`, top, .08, Night.withAlpha(.6), undefined, Floor + .0128, 'none');
          // The cut edges glow where the sphere has just passed: white, then lilac, then violet, gone in 1.5 s.
          for (let x0 = Math.max(from, to - EdgeCool * p.speed - EdgeStep); x0 < to; x0 += EdgeStep) {
            const x1 = Math.min(to, x0 + EdgeStep), age = s - passedAt((x0 + x1) / 2), u = clamp(age / EdgeCool);
            if (u >= 1) continue;
            const colour = age < .3 ? Color.Lerp(White, Lilac, age / .3) : Color.Lerp(Lilac, Violet, clamp((age - .3) / .6));
            line(`purple cut glow ${k} ${Math.round(x0 * 10)}`, [place(x0, k * R * .99, .02), place(x1, k * R * .99, .02)], .15 - .07 * u, colour.withAlpha(.9 * (1 - u)), whiteGlow, Floor + .014, 'none');
          }
        });
      }
    }
    if (purpleOn) {
      sprite(place(at), 11 * grown, 8.5 * grown, Plum.withAlpha(.3 * fading), soft, Floor + .029);
      sprite(place(at), 12 * grown, 9 * grown, Violet.withAlpha(.45 * fading), glow, Floor + .03);
    }
    // The ground under the sphere being erased: a glow, a bright cut line round the front of its footprint,
    // and specks of earth lifting off and drawn into it.
    if (mv > 0) {
      sprite(place(at), R * 2.2, R * 1.6, Lilac.withAlpha(.5 * mv), glow, Floor + .034);
      sprite(place(at + R * .45), R * 1.2, R * .6, White.withAlpha(.45 * mv), glow, Floor + .035, -p.aim);
      const cut = [];
      for (let m = 0; m <= 12; m++) { const q = a + (m / 12 * 2 - 1) * 80 * D2R; cut.push({ x: place(at).x + Math.cos(q) * R, z: place(at).z + Math.sin(q) * R }); }
      line('purple ground cut wide', cut, .3, Lilac.withAlpha(.35 * mv), whiteGlow, Floor + .036, 'both');
      line('purple ground cut', cut, .08, White.withAlpha(.9 * mv), whiteGlow, Floor + .037, 'both');
      for (let i = 0; i < Lifted; i++) {
        const ph = (s / .35 + rand(i + 3200)) % 1, along = (rand(i + 3210) - .2) * R, across = (rand(i + 3220) - .5) * R * 1.8;
        const g0 = place(at + along, across), k = smooth(ph), x = g0.x + (P0.x - g0.x) * k * .7, z = g0.z + (P0.z - g0.z) * k * .7 + k * .2;
        sprite({ x, z }, .09, .09, Color.Lerp(Haze, Lilac, k).withAlpha(Math.sin(Math.PI * ph) * mv), glow, Y + .14 + i * .0001);
      }
    }
    // The purple band left behind (S1 #47-48): haze rising along the cut and fading over 1.1 s after the sphere passes.
    if (s >= t.move) for (let n = 0; ; n++) {
      const x = B + (n + .5) * BandStep;
      if (x >= at) break;
      const age = s - passedAt(x), u = age / BandLife;
      if (u < 0 || u >= 1) continue;
      [-.55, 0, .55].forEach((c, j) => {
        const up = .2 + 1.2 * u, sz = R * 1.7 * (.7 + .5 * u), q = place(x + (rand(n * 3 + j + 3330) - .5) * .3, c * R + (rand(n * 3 + j + 3300) - .5) * .3, up);
        sprite(q, sz, sz * .8, Plum.withAlpha(.28 * (1 - u)), puff, Y + .02 + (n % 30) * .0002, rand(n * 3 + j + 3310) * 360 + u * 70);
        sprite(q, sz * .85, sz * .7, Violet.withAlpha(.5 * (1 - u) ** 1.5), puffGlow, Y + .1 + (n % 30) * .0002, rand(n * 3 + j + 3320) * 360 - u * 70);
      });
    }
    // Dust rising off both lips of the trench once the band has thinned (S1 #61), fading over 2 s.
    if (p.trench && s >= t.move) [-1, 1].forEach(k => {
      for (let n = 0; ; n++) {
        const x = B + (n + .5) * DustStep;
        if (x >= at) break;
        const id = n * 2 + (k > 0 ? 1 : 0), u = (s - passedAt(x) - .25 - .3 * rand(id + 3410)) / (DustLife * (.7 + .5 * rand(id + 3420)));
        if (u <= 0 || u >= 1) continue;
        const q = place(x + (rand(id + 3430) - .5) * .5, k * R * (1 + .1 * rand(id + 3440) + .35 * u), .1 + (.5 + .8 * rand(id + 3450)) * u), sz = (.4 + 1.3 * u) * (.7 + .6 * rand(id + 3460));
        sprite(q, sz, sz * .85, Haze.withAlpha(.28 * Math.sin(Math.PI * u)), puff, Y + .015 + (n % 30) * .0002, rand(n * 2 + (k > 0 ? 1 : 0) + 3400) * 360 + u * 50);
      }
    });

    // --- the wall: 7 cells across the path; the middle 3 are erased; the cut faces glow ------------------------------
    if (!field(p)) WallCells.forEach(k => {
      const hit = hitAt(B + WallAt, k);
      if (s < hit) wallCell(place(B + WallAt, k), p.aim);
      else if (hit === Infinity) wallCell(place(B + WallAt, k), p.aim);
      else dissolve(`purple wall ${k}`, place(B + WallAt, k, .4), 1, s - hit, new Color(.5, .48, .46), P0, 14);
      // The cut face on the side toward the path glows for 1.5 s after the cut.
      const neighbourCut = WallCells.includes(k - Math.sign(k)) && hitAt(B + WallAt, k - Math.sign(k)) !== Infinity;
      if (hit === Infinity && neighbourCut && k !== 0) {
        const cutAt = hitAt(B + WallAt, k - Math.sign(k)), g = s >= cutAt ? 1 - clamp((s - cutAt) / 1.5) : 0;
        if (g > 0) {
          const face = k - Math.sign(k) * .5;
          line(`purple cut ${k}`, [place(B + WallAt - .5, face, .02), place(B + WallAt + .5, face, .02)], .12, Lilac.withAlpha(.8 * g), whiteGlow, buildingLayer + .01, 'none');
        }
      }
    });

    // --- Blue (already open) and Red coming in, then the merge ------------------------------------------------------
    const C = place(B, 0, ChestUp);
    if (s < t.ignite) {
      const k = s < t.contact ? 1 : 1 - smooth((s - t.contact) / p.merge);
      // Fog and streaks of the open Blue, lighter than Blue v2's.
      for (let i = 0; i < 12; i++) {
        const r0 = 1.2 + 2.6 * rand(i + 2400), q = rand(i + 2410) * TAU - s * 3.2 / Math.max(1, r0) - (s >= t.contact ? (s - t.contact) * 12 : 0);
        sprite({ x: place(B).x + Math.cos(q) * r0 * k, z: place(B).z + Math.sin(q) * r0 * k + .12 }, 1.8, 1.4, Deep.withAlpha(.3 * k), soft, Floor + .05 + i * .0003);
      }
      for (let i = 0; i < 10; i++) {
        const ph = (s / .6 + rand(i + 2420)) % 1, a0 = rand(i + 2430) * TAU, pts = [];
        for (let m = 0; m <= 6; m++) { const v = Math.max(0, ph - .22 + .22 * m / 6), rr = 1 + 3 * (1 - v) ** 1.4, q = a0 - 2 * v; pts.push({ x: place(B).x + Math.cos(q) * rr * k, z: place(B).z + Math.sin(q) * rr * k + ChestUp * v * v * Lift }); }
        line(`purple blue streak ${i}`, pts, .045, Ice.withAlpha(.5 * k * Math.sin(Math.PI * ph)), whiteGlow, Y + .09, 'both');
      }
    }
    if (s < t.contact) blueSphere('purple blue', C, BlueR, s);
    // Red at the finger, then in flight.
    if (s >= Start && s < t.fire) {
      const u = clamp((s - Start) / p.charge), F = place(Tip, 0, ChestUp);
      for (let i = 0; i < 3; i++) {
        const pts = [], a0 = -s * 9.4 + i * TAU / 3, Rr = .3 + .35 * u;
        for (let m = 0; m <= 10; m++) { const v = m / 10, rr = Rr * (.15 + .85 * v), q = a0 + v * 2.6; pts.push({ x: F.x + Math.cos(q) * rr, z: F.z + Math.sin(q) * rr }); }
        line(`purple red swirl ${i}`, pts, Rr * .25, Red.withAlpha(.6 * u), whiteGlow, Y + .118, 'both');
      }
      redBall(F, OrbR * smooth(clamp(u / .6)));
    }
    if (s >= t.fire && s < t.contact) {
      const d = Tip + (s - t.fire) * RedSpeed, P = place(d, 0, ChestUp), back = Math.max(Tip, d - 1.8);
      line('purple red tail shade', [P, place(back, 0, ChestUp)], .5, RedDeep.withAlpha(.45), undefined, Y + .115, 'end');
      line('purple red tail', [P, place(back, 0, ChestUp)], .28, Red.withAlpha(.6), whiteGlow, Y + .116, 'end');
      redBall(P, OrbR);
    }
    // Merge: red and blue spiral round each other and in; red and blue arcs swirl; the ground split red / blue.
    if (s >= t.contact && s < t.ignite) {
      const u = (s - t.contact) / p.merge, e = smooth(u), rho = .8 * (1 - e), th = a + Math.PI + e * 4 * TAU / 2;
      const rP = { x: C.x + Math.cos(th) * rho, z: C.z + Math.sin(th) * rho }, bP = { x: C.x - Math.cos(th) * rho * .6, z: C.z - Math.sin(th) * rho * .6 };
      const side = { x: -sa, z: ca };
      // The world split red and blue (S2 ep 4 #30-32): a red half on one side, a blue half on the other,
      // and a red and a cyan half ring 2.2 cells out spinning opposite ways.
      const env = Math.sin(Math.PI * clamp(u * 1.1));
      sprite({ x: place(B).x + side.x * 3, z: place(B).z + side.z * 3 }, 8, 7, Red.withAlpha(.45 * env), glow, Floor + .03);
      sprite({ x: place(B).x - side.x * 3, z: place(B).z - side.z * 3 }, 8, 7, Blue.withAlpha(.5 * env), glow, Floor + .031);
      sprite({ x: C.x + side.x * 2.5, z: C.z + side.z * 2.5 }, 6, 5, RedDeep.withAlpha(.25 * env), soft, Y + .0012);
      sprite({ x: C.x - side.x * 2.5, z: C.z - side.z * 2.5 }, 6, 5, Abyss.withAlpha(.25 * env), soft, Y + .0013);
      [[Red, 1], [Cyan, -1]].forEach(([colour, dirn], j) => {
        const pts = [], q0 = a + Math.PI / 2 * dirn + dirn * (s - t.contact) * 7;
        for (let m = 0; m <= 16; m++) { const q = q0 + Math.PI * m / 16; pts.push({ x: C.x + Math.cos(q) * 2.2 * (1 - .4 * e), z: C.z + Math.sin(q) * 2.2 * (1 - .4 * e) }); }
        line(`purple half ring ${j}`, pts, .3, colour.withAlpha(.6 * env), whiteGlow, Y + .124 + j * .0002, 'both');
        line(`purple half ring ${j} core`, pts, .08, White.withAlpha(.7 * env), whiteGlow, Y + .1242 + j * .0002, 'both');
      });
      // The world holds its breath: it darkens over the last 0.15 s before the flash.
      const hush = smooth((s - (t.ignite - .15)) / .15);
      if (hush > 0) sprite(C, 20, 18, Night.withAlpha(.45 * hush), soft, Y + .0015);
      for (let i = 0; i < 6; i++) {
        const rr = .6 + .2 * i, q0 = th * (1 + .15 * i) + i * 1.3, pts = [];
        for (let m = 0; m <= 10; m++) { const q = q0 + 1.6 * m / 10; pts.push({ x: C.x + Math.cos(q) * rr * (1 - .5 * e), z: C.z + Math.sin(q) * rr * (1 - .5 * e) }); }
        line(`purple merge arc ${i}`, pts, .1, (i % 2 ? Red : Cyan).withAlpha(.7 * Math.sin(Math.PI * clamp(u * 1.2))), whiteGlow, Y + .125 + i * .0002, 'both');
      }
      blueSphere('purple blue merging', bP, BlueR * (1 - .5 * e), s);
      redBall(rP, OrbR * (1 + .6 * e));
      sprite(C, 1.6 * e, 1.6 * e, Magenta.withAlpha(.6 * e), glow, Y + .13);
    }

    // --- ignition: white point, flash, ripple rings, rays ---------------------------------------------------------
    if (s >= t.ignite && s < t.ignite + .7) {
      const age = s - t.ignite;
      if (age < .5) { const f = age / .5; sprite(C, 18, 16, Violet.withAlpha(.5 * (1 - f)), glow, Y + .0016); sprite(C, 16, 14, Plum.withAlpha(.3 * (1 - f)), soft, Y + .0015); }
      if (age < .25) { const f = age / .25; sprite(C, 3.5 * (1 - f) + .5, 3.5 * (1 - f) + .5, White.withAlpha(.95 * (1 - f)), glow, Y + .2); glint('purple ignite', C, 1.6 * (1 - f) + .3, 1 - f, Lilac, 15); }
      for (let i = 0; i < 4; i++) {
        const u = clamp((age - i * .08) / .6);
        if (u > 0 && u < 1) ringAt(C, .2 + 3 * smooth(u), Violet.withAlpha(.7 * (1 - u)), Y + .19 + i * .0002, i % 2 === 0, whiteGlow);
      }
      if (age < .35) for (let i = 0; i < 12; i++) {
        const q = i * TAU / 12 + rand(i + 2500) * .3, len = (2 + 2 * rand(i + 2510)) * smooth(age / .1);
        line(`purple ignite ray ${i}`, [C, { x: C.x + Math.cos(q) * len, z: C.z + Math.sin(q) * len }], .16, Lilac.withAlpha(.7 * (1 - age / .35)), whiteGlow, Y + .195, 'end');
      }
    }

    // --- things on the path: stand, then dissolve or fall -------------------------------------------------------------
    const figures = [{ kind: 'gojo', pos: G }];
    if (!field(p)) Path.forEach(([d, across, kind], i) => figures.push({ kind, i, d, across, hit: hitAt(B + d, across), pos: place(B + d, across) }));
    const purpleLight = pos => purpleOn ? .6 * (1 - clamp(Math.hypot(pos.x - place(at).x, pos.z - place(at).z) / 5)) * grown * fading : 0;
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      if (g.kind === 'gojo') {
        const out = smooth(s / Raise) * (1 - smooth((s - t.contact - .3) / .3)), north = sa > .35, lit = Math.max(.4 * clamp((s - Start) / p.charge) * (s < t.fire + .3 ? 1 : 0), purpleLight(g.pos));
        if (north) pointingArm(place, p.aim, out, pawnLayer - .004);
        caster(g.pos, sun, strength, { tint: s < t.fire + .3 ? Red : Violet, tintAmount: lit });
        if (!north) pointingArm(place, p.aim, out, pawnLayer + .016);
        return;
      }
      const age = s - g.hit;
      if (g.kind === 'tree' || g.kind === 'crate') {
        if (age < 0) {
          if (g.kind === 'tree') { draw(disc, g.pos.x, pawnLayer, g.pos.z + .1, .08, .12, 0, Bark); draw(disc, g.pos.x, pawnLayer + .002, g.pos.z + .55, .42, .38, 0, Color.Lerp(Leaves, Violet, purpleLight(g.pos))); }
          else draw(MeshPool.plane10, g.pos.x, pawnLayer, g.pos.z + .1, .7, .6, -p.aim, Color.Lerp(Crate, Violet, purpleLight(g.pos)));
        } else dissolve(`purple ${g.kind} ${g.i}`, place(B + g.d, g.across, .5), .9, age, g.kind === 'tree' ? Leaves : Crate, P0, 16);
        return;
      }
      const colour = g.kind === 'ally' ? Ally : EnemyColour;
      if (age < 0) { pawn(g.pos, colour, sun, strength, { tint: Violet, tintAmount: purpleLight(g.pos) }); return; }
      // Hit: 60 erasure damage. A white-purple flash, specks off the body, then down with a burn under him.
      const flash = 1 - clamp(age / .2);
      if (age > .15) sprite(g.pos, 1.1, .8, Plum.withAlpha(.55 * clamp((age - .15) / .2)), soft, Floor + .011);
      pawn(g.pos, colour, sun, strength, { lie: age > .15, tint: flash > 0 ? White : Violet, tintAmount: flash > 0 ? .8 * flash : purpleLight(g.pos) });
      dissolve(`purple pawn ${g.i}`, place(B + g.d, g.across, .5), .6, age, colour, P0, 10);
    });

    // --- Purple: the sphere, ripple rings, rays along the path, specks drawn in ------------------------------------------
    if (purpleOn) {
      const Pt = place(at, 0, ChestUp);
      if (s >= t.move) {
        const since = s - t.move;
        for (let n = Math.max(0, Math.floor(since / RingEvery) - 1); n <= Math.floor(since / RingEvery); n++) {
          const u = (since - n * RingEvery) / .6;
          if (u > 0 && u < 1) ringAt(Pt, size + 1.6 * smooth(u), Violet.withAlpha(.45 * (1 - u) * fading), Y + .148, false, whiteGlow);
        }
        for (let i = 0; i < Rays; i++) {
          const dir = i % 2 ? 1 : -1, q = a + (dir < 0 ? Math.PI : 0) + (rand(i + 2600) - .5) * 30 * D2R, len = (3 + 4 * rand(i + 2610)) * (.7 + .3 * Math.sin(s * 11 + i)), off = (rand(i + 2620) - .5) * size;
          const from = { x: Pt.x - Math.sin(q) * off, z: Pt.z + Math.cos(q) * off };
          line(`purple path ray ${i}`, [from, { x: from.x + Math.cos(q) * len, z: from.z + Math.sin(q) * len }], .12, Lilac.withAlpha(.3 * fading * grown), whiteGlow, Y + .145, 'end');
        }
        // Specks swept in from the front, the air it erases.
        for (let i = 0; i < 16; i++) {
          const ph = (s / .5 + rand(i + 2700)) % 1, q = a + (rand(i + 2710) - .5) * 2.4, r0 = size * (1.2 + 1.5 * rand(i + 2720)), rr = r0 * (1 - ph) + size * .3 * ph;
          sprite({ x: Pt.x + Math.cos(q) * rr, z: Pt.z + Math.sin(q) * rr }, .1, .1, (i % 2 ? Lilac : White).withAlpha(Math.sin(Math.PI * ph) * fading), glow, Y + .149);
        }
      }
      purpleSphere('purple', Pt, size, s, s >= t.stop ? .5 + .5 * fading : 1);
    }
    // Break-up at the end: specks flying out, a last ring.
    if (s >= t.stop && s < t.gone + .3) {
      const u = (s - t.stop) / (Fade + .3), Pt = place(at, 0, ChestUp);
      for (let i = 0; i < 30; i++) {
        const q = rand(i + 2800) * TAU, rr = R * (.3 + 1.8 * smooth(u) * (.5 + .5 * rand(i + 2810)));
        sprite({ x: Pt.x + Math.cos(q) * rr, z: Pt.z + Math.sin(q) * rr + u * .4 }, .12, .12, (i % 3 ? Lilac : Violet).withAlpha(1 - u), glow, Y + .16);
      }
      ringAt(Pt, R * (1 + 1.5 * smooth(u)), Lilac.withAlpha(.5 * (1 - u)), Y + .161, false, whiteGlow);
    }
  },
};
