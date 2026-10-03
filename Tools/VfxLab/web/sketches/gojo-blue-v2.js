// Lapse: Blue v2 — Gojo kit, ability sketch, not the game. gojo-blue.js (v1) is kept for comparison.
//
// Mechanic (agreed 2026-09-27, "a lesser Gravity Well"; numbers are placeholders for XML on
// CompProperties_AbilityGravityWell): Blue opens at a cell up to 15 away and for 3 s pulls pawns and
// loose things within 4 cells (6 cells/s at the core edge, falling to 0 at 4 cells, divided by body size
// over 1). Pawns reaching the 1-cell core are held there, items there are crushed into it, allies are
// pulled too, no bullet bending. Then it implodes: 10-25 blunt by the mass it holds, radius 2. No stun.
// Cooldown 20 s, 3 charge. Not yet agreed (mine): core 1, implosion radius 2, items crushed.
//
// Sources (YouTube storyboards, looked at closer 2026-09-29):
//   anime S2 ep 3, Ybn2csWuIgg  #3 a vortex of dark debris round a bright centre; #9 planks and tiles
//                   flying into a white point with light rays between them; #8, #12 the frame full of blue
//                   fog and debris; #11 debris streaming in through a dark scene
//   qtlY0Vdy_GA     11 s roof tiles and planks the size of a man pulled in; 12-13 s the ball of light over
//                   the courtyard, crushing into the building in dust; 16 s a grey dust cloud after
//   Cursed Clash 8Ot4zM2Z-Ow  #41 hold level 3: a translucent cyan bubble with a bright rim and a dark-blue
//                   inside, in a dark-blue storm with debris; #52 cyan bubble, white centre
//   Phantom Parade YZCxpqUqEUI  #44, #55 cyan arcs round Gojo; #45, #46, #89 blue fog vortex full of slabs
//
// v2 against v1: the ground goes dark blue and a storm of fog turns over the 4-cell pull; slabs, planks
// and chips are torn out of the floor (holes stay), spiral up and orbit the ball before they are packed
// in, passing behind and in front of it; the 1-cell core is a translucent cyan bubble with a dark
// inside (the game's sphere), with a whirlpool and a white point in it (improved 2026-09-29, see sphere()); cracks run out across the floor; light rays;
// cyan arcs round Gojo; a rumble. The implosion throws the debris out and raises a big grey dust cloud.
//
// Order ("group", the default; Blue 8 cells from Gojo, hold 3 s):
//   0.00-0.15   arm up, pointing; blue light at the hand
//   0.10-0.70   2 cyan-white arcs sweep round Gojo (1.1 and 1.4 cells)
//   0.15-0.45   open: a star glint at the cell, the ball grows, the bubble opens to the 1-cell core
//   0.45-3.45   hold: the ground inside 4 cells darkens (60 %, and 21 % over pawns too); 30 fog puffs on
//               3 spiral arms turn round it, faster inside; 14 pale streaks curl in; 14 slabs (0.45-0.85
//               cells), 8 planks (0.65-0.95) and 26 chips are torn up one after another over the first
//               75 % of the hold, each leaving a hole, spiralling up to orbit at
//               1.1-1.6 cells for 0.6-1.4 s, then packed into the bubble as dark shapes; 10 cracks grow
//               out to 1.5-3.6 cells; 8 light rays from the ball after 0.8 s; pawns dragged and held as
//               in v1; small shakes at 1.45 and 2.45 s
//   3.45-3.57   everything rushes in: debris, fog and bubble shrink to the centre
//   3.57        a white flash 3 cells, a blue flash 6 cells, 12 rays, a ring to the 2-cell implosion
//               radius, 16 pieces thrown out 1-3.5 cells (they stay), held pawns flash white and drop
//               to their feet; shake
//   3.57-5.49   a grey dust cloud of 20 puffs (each starts up to 0.12 s late and lasts 1.8 s), 1.3 -> 3.7
//               cells each, rises up to 2 cells and spreads to about 3 cells, then thins
//   to 5.77     crater 1.6 cells across with a torn rim, holes, cracks, debris, drag marks
//
// Drawing: level circles, spirals and quads, nothing per facing. Height is drawn north (0.6 per cell)
// with shadows on the floor; orbiting pieces on the north half of their orbit draw under the bubble and
// on the south half over it. Light is additive; fog and darkening are Transparent. Stand-ins as in v1.
//
// Ported to C# as a picture only (2026-09-30): Source/RimArt/Gojo/GojoBlue*.cs, previews "Gojo: blue: ...",
// with the chosen cell as this sketch's origin. Not ported: Gojo's body and the pulled pawns, rock and
// rifle, with their tints, lift and flash; untick "Stand-in pawns and props" to see what the C# draws.
import { Color, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import { caster, Uniform } from './lib/gojo.js';
import { Blue, Deep, Ice, Void } from './lib/vergil.js';
import { pawn, rock, ringAt, line, glint, whiteGlow, EnemyColour, Ally, Ink, White, Dust, Lift, Chest, Skin, pawnLayer, shadowLayer, clamp, smooth } from './lib/goku.js';

const TAU = Math.PI * 2, D2R = Mathf.Deg2Rad, ChestUp = Chest / Lift;
const disc = Meshes.disc(32, 'blue2 disc');
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent), puffGlow = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.MoteGlow);
// Decided values.
const Raise = .15, Grow = .3, Rush = .12, Tail = 2.2, Reach = .5, HeldLift = .25, Bands = 12, Wisps = 6;
const Streaks = 14, StreakPeriod = .6, Fog = 30, FogArms = 3, Rays = 8, Cracks = 10, Clouds = 20, Thrown = 16, CraterR = .8, Specks = 26;
const Slabs = 14, Planks = 8, Chips = 26, TearShare = .75, Arcs = [[1.1, 30], [1.4, 210]], ArcSweep = 200;
const Royal = new Color(.12, .32, .95), Abyss = new Color(.01, .03, .16), Pale = new Color(.7, .85, 1), Cyan = new Color(.45, .85, 1), Crater = new Color(.14, .12, .1), Torn = new Color(.45, .38, .3);
const Wood = new Color(.4, .28, .16), WoodLit = new Color(.55, .4, .24), Steel = new Color(.45, .46, .5), Cloud = new Color(.62, .62, .64);
const Group = [[60, 2.5, 'raider'], [200, 3.6, 'raider'], [-70, 3.2, 'ally'], [150, 5, 'raider'], [-20, 1.8, 'chunk'], [110, 2.8, 'rifle']];

const empty = p => p.scenario === 'empty ground';
function times(p) {
  const open = Raise, full = open + Grow, implode = full + p.hold, burst = implode + Rush;
  return { open, full, implode, burst, end: burst + Tail };
}
function pulled(r0, t, p) {
  if (r0 >= p.pullR || t <= 0 || r0 <= p.coreR) return r0;
  const k = p.pullSpeed / (p.pullR - p.coreR);
  return Math.max(p.coreR, p.pullR - (p.pullR - r0) * Math.exp(k * t));
}
const reachCore = (r0, p) => r0 >= p.pullR ? Infinity : r0 <= p.coreR ? 0 : Math.log((p.pullR - p.coreR) / (p.pullR - r0)) / (p.pullSpeed / (p.pullR - p.coreR));

// One piece of torn-up floor. Where it is at time s: on the floor until born, then spiralling up and in
// to its orbit over rise seconds, orbiting until packed, drawn inside the bubble after that. At the
// implosion everything still flying rushes to the centre.
function piece(i, p, t) {
  const n = Slabs + Planks + Chips, kind = i < Slabs ? 'slab' : i < Slabs + Planks ? 'plank' : 'chip';
  const born = t.full + .1 + p.hold * TearShare * (i * 7 % n) / n, rise = .6 + .4 * rand(i + 1100), orbit = .6 + .8 * rand(i + 1110);
  const r0 = p.coreR + .5 + (p.pullR - p.coreR - .7) * rand(i + 1120), a0 = rand(i + 1130) * TAU;
  const orbitR = p.coreR * (1.1 + .5 * rand(i + 1140)), orbitH = ChestUp + (rand(i + 1150) - .5) * .9;
  const size = kind === 'slab' ? .45 + .4 * rand(i + 1160) : kind === 'plank' ? .65 + .3 * rand(i + 1160) : .14 + .1 * rand(i + 1160);
  return { i, kind, born, rise, orbit, r0, a0, orbitR, orbitH, size, packedAt: born + rise + orbit };
}
function pieceAt(g, s, t) {
  if (s < g.born) return { state: 'ground', r: g.r0, q: g.a0, h: 0 };
  let r, q, h;
  const u = Math.min(1, (Math.min(s, t.implode) - g.born) / g.rise), e = smooth(u);
  r = g.r0 + (g.orbitR - g.r0) * e; q = g.a0 - 2.6 * u * u; h = g.orbitH * Math.pow(e, 1.5);
  if (u >= 1) {
    const v = Math.min(s, t.implode) - g.born - g.rise, w = 3.2 * 1.3 / g.orbitR;
    q = g.a0 - 2.6 - w * v; r = g.orbitR * (1 - .25 * clamp(v / g.orbit)); h = g.orbitH + .08 * Math.sin(v * 7 + g.i);
  }
  if (Math.min(s, t.implode) >= g.packedAt) return { state: 'packed', r: 0, q, h: ChestUp };
  if (s >= t.implode) { const k = 1 - smooth((s - t.implode) / Rush); if (k <= 0) return { state: 'packed', r: 0, q, h: ChestUp }; return { state: 'fly', r: r * k, q: q - (1 - k) * 3, h: ChestUp + (h - ChestUp) * k }; }
  return { state: 'fly', r, q, h };
}

function pointingArm(place, deg, out, layer) {
  if (out <= .02) return;
  const reach = Reach * out, sleeve = place(reach * .4, 0, ChestUp), hand = place(reach, 0, ChestUp), tip = place(reach + .16 * out, 0, ChestUp);
  draw(MeshPool.plane10, sleeve.x, layer, sleeve.z, .12, reach * .8, 90 - deg, Uniform);
  draw(disc, hand.x, layer + .001, hand.z, .06, .06, 0, Skin);
  draw(MeshPool.plane10, (hand.x + tip.x) / 2, layer + .0015, (hand.z + tip.z) / 2, .03, .16 * out, 90 - deg, Skin);
}
function ribbon(key, centre, R, head, span, alpha) {
  if (alpha <= 0) return;
  const pts = [];
  for (let k = 0; k <= 16; k++) { const q = head + span * (1 - k / 16); pts.push({ x: centre.x + Math.cos(q) * R, z: centre.z + Math.sin(q) * R }); }
  line(`${key} edge`, pts, .18, Cyan.withAlpha(.4 * alpha), whiteGlow, Y + .08, 'both');
  line(`${key} core`, pts, .055, White.withAlpha(.85 * alpha), whiteGlow, Y + .081, 'both');
}
// A piece of debris at a screen point: a slab or chip is a lit lump, a plank a two-tone board.
function debris(g, at, spin, layer, alpha = 1) {
  if (g.kind === 'plank') {
    draw(MeshPool.plane10, at.x, layer, at.z, .17, g.size, spin, Wood.withAlpha(alpha));
    draw(MeshPool.plane10, at.x - .02, layer + .0005, at.z + .02, .08, g.size * .92, spin, WoodLit.withAlpha(alpha));
  } else rock(at, g.size, spin, alpha, g.kind === 'slab' ? 2 * (g.i % 3) : 1 + 2 * (g.i % 3), layer);
}

// The core sphere, lit from its edge (Cursed Clash #41) with a whirlpool inside (anime #29) and a white
// point at its heart (anime #9). Layers, back to front: a soft blue and cyan halo; a royal-blue body
// darkening smoothly to near-black in the middle; the packed debris sinking in a spiral toward the
// centre, tinted by the body over it; 5 thin cyan rings fading inward from the rim (the lit edge) and a
// faint pale line; 12 swirl bands turning faster the nearer the middle they are, with white foam at
// their heads; glitter on the shell; the eye (dark disc, thin pale ring, a beating white point, 2 short
// flickering rays); 6 wisps flicking off
// the edge the way it turns; a soft highlight and a glint on the upper left. Nothing is a hard ring.
function sphere(key, at, R, s, swirl, packedPieces, alpha) {
  if (R <= .01 || alpha <= 0) return;
  const beat = 1 + .08 * Math.sin(s * 11), spin = swirl * .6;
  sprite(at, R * 4.4, R * 4.4, Blue.withAlpha(.4 * alpha), glow, Y + .1);
  sprite(at, R * 2.7, R * 2.7, Cyan.withAlpha(.28 * alpha), glow, Y + .1005);
  draw(disc, at.x, Y + .101, at.z, R, R, 0, Royal.withAlpha(.92 * alpha));
  sprite(at, R * 1.9, R * 1.9, Abyss.withAlpha(.95 * alpha), soft, Y + .1015);
  // Packed debris: each piece sinks from 0.85 R to 0.2 R over 1.5 s after it arrived, turning faster
  // as it goes in; the body is drawn over it again at 40 % so it sits inside.
  packedPieces.forEach((g, k) => {
    const u = clamp((s - g.packedAt) / 1.5), d = R * (.85 - .65 * u * u), q = rand(g.i + 1200) * TAU + swirl * (.3 + 1.2 * u);
    debris({ ...g, size: g.size * (.75 - .35 * u) }, { x: at.x + Math.cos(q) * d, z: at.z + Math.sin(q) * d }, rand(g.i + 1210) * 360 + swirl * 25, Y + .102 + k * .0001, (.8 - .4 * u) * alpha);
  });
  draw(disc, at.x, Y + .104, at.z, R * .97, R * .97, 0, Royal.withAlpha(.25 * alpha));
  sprite(at, R * 1.4, R * 1.4, Abyss.withAlpha(.7 * alpha), soft, Y + .1042);
  // The lit edge: thin cyan rings fading inward from the rim, so the light falls off like a lit sphere,
  // and a faint pale line on the rim.
  [[.985, .38], [.95, .28], [.91, .19], [.87, .12], [.83, .07]].forEach(([f, a], j) =>
    ringAt(at, R * f, Cyan.withAlpha(a * alpha), Y + .1045 + j * .0001, false, whiteGlow));
  ringAt(at, R, Ice.withAlpha(.25 * alpha), Y + .105, false, whiteGlow);
  // Swirl bands: the whirlpool. Inner bands turn faster.
  for (let k = 0; k < Bands; k++) {
    const v = k / (Bands - 1), rr = R * (.3 + .62 * v), w = 3.2 * Math.pow(R / rr, 1.1), a0 = rand(k + 1250) * TAU + swirl * w * .35;
    const span = .9 + .8 * rand(k + 1260), pts = [];
    for (let m = 0; m <= 8; m++) { const q = a0 + span * m / 8, wob = 1 + .04 * Math.sin(m * 1.7 + s * 6 + k); pts.push({ x: at.x + Math.cos(q) * rr * wob, z: at.z + Math.sin(q) * rr * wob }); }
    const colour = Color.Lerp(Cyan, Pale, v), width = R * (.05 + .05 * rand(k + 1270));
    line(`${key} band ${k}`, pts, width, colour.withAlpha(.42 * alpha), whiteGlow, Y + .106 + k * .0002, 'both');
    sprite(pts[0], width * 2.4, width * 2.4, White.withAlpha(.55 * alpha), glow, Y + .1085);
  }
  for (let i = 0; i < Specks; i++) {
    const d = R * (.55 + .5 * rand(i + 750)), q = rand(i + 730) * TAU + swirl * 3.2 * Math.pow(R / d, 1.1) * .35, tw = .3 + .7 * Math.abs(Math.sin(s * 13 + i * 1.7)), size = .04 + .05 * rand(i + 760);
    sprite({ x: at.x + Math.cos(q) * d, z: at.z + Math.sin(q) * d }, size * 1.6, size * 1.6, (i % 3 ? White : Cyan).withAlpha(tw * alpha), glow, Y + .109);
  }
  // The eye.
  draw(disc, at.x, Y + .11, at.z, R * .17, R * .17, 0, Abyss.withAlpha(.9 * alpha));
  ringAt(at, R * .2, Ice.withAlpha(.55 * alpha), Y + .1105, false, whiteGlow);
  sprite(at, R * .55 * beat, R * .55 * beat, Cyan.withAlpha(.55 * alpha), glow, Y + .111);
  sprite(at, R * .22 * beat, R * .22 * beat, White.withAlpha(alpha), glow, Y + .1112);
  for (let i = 0; i < 2; i++) {
    const q = (i * 90 + 30 * Math.sin(s * 2) + 20) * D2R, len = R * (.12 + .14 * Math.abs(Math.sin(s * 19 + i * 2.3)));
    line(`${key} eye ray ${i}`, [{ x: at.x - Math.cos(q) * len, z: at.z - Math.sin(q) * len }, at, { x: at.x + Math.cos(q) * len, z: at.z + Math.sin(q) * len }], R * .03, White.withAlpha(.5 * alpha), whiteGlow, Y + .1114, 'both');
  }
  // Wisps flicking off the edge the way it turns.
  for (let i = 0; i < Wisps; i++) {
    const a0 = i * TAU / Wisps + spin, life = ((s * 1.6 + rand(i + 1280)) % 1), pts = [];
    for (let m = 0; m <= 6; m++) { const v = m / 6, rr = R * (1 + .45 * v * life + .05), q = a0 - v * .9; pts.push({ x: at.x + Math.cos(q) * rr, z: at.z + Math.sin(q) * rr }); }
    line(`${key} wisp ${i}`, pts, R * .09, Cyan.withAlpha(.5 * alpha * Math.sin(Math.PI * life)), whiteGlow, Y + .1116, 'both');
  }
  // Soft highlight and a glint on the upper left: the glossy marble of the anime's close-ups.
  sprite({ x: at.x - R * .36, z: at.z + R * .42 }, R * .55, R * .3, White.withAlpha(.3 * alpha), glow, Y + .112, 35);
  draw(disc, at.x - R * .44, Y + .1122, at.z + R * .52, R * .06, R * .05, 0, White.withAlpha(.9 * alpha));
}

export default {
  kit: 'Gojo', label: 'Lapse: Blue v2 (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'group', options: ['group', 'empty ground'], group: 'Showcase' },
    actors: { label: 'Stand-in pawns and props', value: true, group: 'Showcase' },
    aim: P('Aim (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    dist: P('Blue from Gojo (cells, up to 15)', 8, 3, 15, .5, 'Showcase'),
    hold: P('Hold', 3, 1, 6, .25, 'Timing (s)'),
    pullR: P('Pull radius (cells)', 4, 2, 6, .25, 'Mechanic'),
    coreR: P('Core radius (cells)', 1, .5, 2, .25, 'Mechanic'),
    pullSpeed: P('Pull at the core edge (cells/s)', 6, 1, 12, .5, 'Mechanic'),
    burstR: P('Implosion radius (cells)', 2, 1, 4, .25, 'Mechanic'),
    dark: P('Darkening', .6, 0, .9, .05, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Point', t: 0 }, { name: 'Open', t: t.open }, { name: 'Hold', t: t.full }, { name: 'Rush', t: t.implode }, { name: 'Burst', t: t.burst }];
  },
  events(p) {
    const t = times(p);
    return [{ t: t.open, type: 'shake', value: .03 }, { t: t.full + 1, type: 'shake', value: .02 }, { t: t.full + 2, type: 'shake', value: .025 }, { t: t.burst, type: 'shake', value: .15 },
      // The pull is a sustainer in game (Gravity Well's hum), ended when Blue implodes.
      { t: t.open, type: 'sound', def: 'AG_GojoBlueOpen' }, { t: t.full, type: 'sound', def: 'AG_GojoBluePull', lasts: t.burst - t.full },
      { t: t.burst, type: 'sound', def: 'AG_GojoBlueImplode' }];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * D2R, ca = Math.cos(a), sa = Math.sin(a);
    const span = p.dist + p.pullR, G = { x: o.x - ca * span / 2, z: o.z - sa * span / 2 };
    const place = (along, across = 0, up = 0) => ({ x: G.x + along * ca - across * sa, z: G.z + along * sa + across * ca + up * Lift });
    const Cf = place(p.dist), C = place(p.dist, 0, ChestUp);
    const around = (deg, r, up = 0) => { const q = a + deg * D2R; return { x: Cf.x + Math.cos(q) * r, z: Cf.z + Math.sin(q) * r + up * Lift }; };
    const polar = (q, r, up = 0) => ({ x: Cf.x + Math.cos(q) * r, z: Cf.z + Math.sin(q) * r + up * Lift });
    const live = s >= t.open && s < t.burst, pullT = Math.max(0, Math.min(s, t.implode) - t.full);
    const grow = smooth((s - t.open) / Grow), shrink = s >= t.implode ? 1 - smooth((s - t.implode) / Rush) : 1, k = grow * shrink;
    const late = smooth((s - (t.implode - .6)) / .6), swirl = -(s * 3.2 + late * late * 4) - (s >= t.implode ? (s - t.implode) * 30 : 0);
    const age = s - t.burst, burst = age >= 0, held = clamp((s - t.full) / .4);

    // --- floor: darkening, the pull ring, holes, cracks, drag marks, crater ------------------------------------------------
    if (live) {
      sprite(Cf, p.pullR * 2.9, p.pullR * 2.7, Void.withAlpha(p.dark * k), soft, Floor + .02);
      sprite(Cf, p.pullR * 2, p.pullR * 1.85, Void.withAlpha(p.dark * .8 * k), soft, Floor + .021);
      sprite(Cf, p.pullR * 2.6, p.pullR * 2.4, Void.withAlpha(p.dark * .35 * k), soft, Y + .001);
      sprite(Cf, p.pullR * 1.8, p.pullR * 1.6, Blue.withAlpha(.3 * k), glow, Floor + .03);
      ringAt(Cf, p.pullR, Blue.withAlpha(.22 * k), Floor + .031, false, whiteGlow);
    }
    const pieces = [];
    for (let i = 0; i < Slabs + Planks + Chips; i++) pieces.push(piece(i, p, t));
    pieces.forEach(g => {
      if (s < g.born || g.kind === 'chip') return;
      const hole = smooth((s - g.born) / .15), at = polar(g.a0, g.r0);
      sprite(at, g.size * 1.6 * hole, g.size * 1.2 * hole, Crater.withAlpha(.7), soft, Floor + .009);
      sprite({ x: at.x + .04, z: at.z + .04 }, g.size * 1.9 * hole, g.size * 1.5 * hole, Torn.withAlpha(.35), soft, Floor + .0085);
    });
    for (let i = 0; i < Cracks; i++) {
      const g = smooth((s - t.full - .5 - .15 * i * (p.hold / 3)) / (p.hold * .5)), q = i * TAU / Cracks + (rand(i + 1300) - .5) * .5, len = (p.coreR * .6 + (1.5 + 2.1 * rand(i + 1310))) * g, pts = [];
      if (g <= 0) continue;
      for (let m = 0; m <= 9; m++) { const d = p.coreR * .8 + len * m / 9, off = m ? (rand(i * 13 + m + 1320) - .5) * .16 : 0; pts.push({ x: Cf.x + Math.cos(q) * d - Math.sin(q) * off, z: Cf.z + Math.sin(q) * d + Math.cos(q) * off }); }
      line(`blue2 crack ${i}`, pts, .055, Ink.withAlpha(.65), undefined, Floor + .011);
      if (live && s < t.implode) line(`blue2 crack glow ${i}`, pts, .1, Cyan.withAlpha(.25 * k), whiteGlow, Floor + .012);
    }
    const things = empty(p) ? [] : Group.map(([deg, r0, kind], i) => {
      const r = pulled(r0, pullT, p), tCore = reachCore(r0, p);
      return { deg, r0, kind, i, r, inCore: pullT >= tCore, coreAt: t.full + tCore };
    });
    things.filter(g => g.kind === 'raider' || g.kind === 'ally').filter(g => g.r < g.r0 - .05).forEach(g =>
      [-.1, .1].forEach((x, j) => {
        const from = around(g.deg, g.r0 - .1), to = around(g.deg, g.r + .25), q = a + g.deg * D2R, nx = -Math.sin(q) * x, nz = Math.cos(q) * x;
        line(`blue2 drag ${g.i} ${j}`, [{ x: from.x + nx, z: from.z + nz }, { x: to.x + nx, z: to.z + nz }], .045, Ink.withAlpha(.28), undefined, Floor + .012, 'none');
      }));
    if (burst) {
      const g = smooth(age / .1), cr = CraterR * 2 * g;
      sprite(Cf, cr * 1.5, cr * 1.2, Torn.withAlpha(.55), soft, Floor + .0095);
      sprite(Cf, cr, cr * .8, Crater.withAlpha(.9), soft, Floor + .0096);
      ringAt(Cf, CraterR * 1.1 * g, Torn.withAlpha(.75), Floor + .0097, true);
      ringAt(Cf, CraterR * 1.3 * g, Crater.withAlpha(.35), Floor + .0094, false);
    }

    // --- the storm: fog turning over the pull, dark below and faint cyan light above -------------------------------------
    if (live) for (let i = 0; i < Fog; i++) {
      // Puffs along FogArms spiral arms that turn, faster inside, so the fog reads as one vortex.
      const arm = i % FogArms, v = (Math.floor(i / FogArms) + .5) / Math.ceil(Fog / FogArms), r0 = 1 + (p.pullR - .6) * v + (rand(i + 1400) - .5) * .4;
      const rr = r0 * (1 - .15 * held) * (s >= t.implode ? shrink : 1), turn = (s - t.open) * 2.6 * 2 / Math.max(1, r0) + (s >= t.implode ? (s - t.implode) * 25 : 0);
      const q = arm * TAU / FogArms + v * 2.8 - turn, size = (1.8 + 1.4 * v + .5 * rand(i + 1420)) * (s >= t.implode ? .5 + .5 * shrink : 1), at = polar(q, rr, .2);
      sprite(at, size, size * .8, Deep.withAlpha(.42 * k), puff, Floor + .05 + i * .0003, rand(i + 1430) * 360 + s * 40);
      if (i % 2) sprite(at, size * .7, size * .5, Cyan.withAlpha(.13 * k), puffGlow, Y + .02 + i * .0003, rand(i + 1440) * 360 - s * 60);
    }

    // --- pawns and items (north first); debris behind the sphere draws under it, in front over it ---------------------------
    const lightAt = pos => live ? .55 * (1 - clamp(Math.hypot(pos.x - Cf.x, pos.z - Cf.z) / (p.pullR + 1.5))) * k : 0;
    const figures = [{ kind: 'gojo', pos: G }];
    things.forEach(g => {
      let r = g.r, up = 0, gone = 0;
      if (g.kind === 'chunk' || g.kind === 'rifle') {
        if (g.inCore) { const u = clamp((s - g.coreAt) / .2); r = p.coreR * (1 - u); up = ChestUp * u; gone = u; }
      } else if (g.inCore && s < t.burst) up = HeldLift * smooth((s - g.coreAt) / .25);
      else if (burst && g.inCore) up = HeldLift * (1 - smooth(age / .15));
      figures.push({ ...g, pos: around(g.deg, r, up), ground: around(g.deg, r), up, gone });
    });
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      if (g.kind === 'gojo') {
        const out = smooth(s / Raise) * (1 - smooth((s - t.full - .4) / .3)), north = sa > .35, lit = .45 * smooth(s / Raise) * (1 - smooth((s - t.full) / .5));
        if (north) pointingArm(place, p.aim, out, pawnLayer - .004);
        if (p.actors) caster(g.pos, sun, strength, { tint: Blue, tintAmount: lit });
        if (!north) pointingArm(place, p.aim, out, pawnLayer + .016);
        return;
      }
      if (!p.actors) return;
      if (g.kind === 'chunk') { if (g.gone < 1) rock(g.pos, .45 * (1 - .7 * g.gone), 30 + g.gone * 400, 1, 2, g.up > .02 ? Y + .06 : Floor + .06); return; }
      if (g.kind === 'rifle') { if (g.gone < 1) draw(MeshPool.plane10, g.pos.x, g.up > .02 ? Y + .06 : Floor + .06, g.pos.z, .09 * (1 - .6 * g.gone), .62 * (1 - .6 * g.gone), 40 + g.gone * 500, Steel); return; }
      const flash = burst && g.inCore ? 1 - clamp(age / .25) : 0, colour = g.kind === 'ally' ? Ally : EnemyColour;
      if (g.up > .02) sprite({ x: g.ground.x + sun.x * .45, z: g.ground.z + sun.z * .45 }, .85, .4, Ink.withAlpha(strength), soft, shadowLayer);
      pawn(g.pos, colour, sun, g.up > .02 ? 0 : strength, { tint: flash > 0 ? White : Blue, tintAmount: flash > 0 ? .7 * flash : lightAt(g.pos) });
    });
    const packedPieces = [];
    pieces.forEach(g => {
      const st = pieceAt(g, s, t);
      if (st.state === 'packed') { if (live && g.kind !== 'chip') packedPieces.push(g); return; }
      if (st.state === 'ground' || !live) return;
      const ground = polar(st.q, st.r), at = polar(st.q, st.r, st.h), spin = rand(g.i + 1500) * 360 + (s - g.born) * (200 + 300 * rand(g.i + 1510));
      if (st.h > .05) sprite({ x: ground.x + sun.x * st.h, z: ground.z + sun.z * st.h }, g.size * 1.1, g.size * .7, Ink.withAlpha(.3 * clamp(1.4 - st.h)), soft, Floor + .05);
      const front = Math.sin(st.q) < 0 && st.r < p.coreR * 1.8;
      debris(g, at, spin, front ? Y + .135 + g.i * .0002 : Y + .05 + g.i * .0002);
    });

    // --- Gojo: blue at the hand, cyan arcs round him ------------------------------------------------------------------------
    const handK = smooth(s / Raise) * (1 - smooth((s - t.full) / .3));
    if (handK > 0) {
      const H = place(Reach + .1, 0, ChestUp);
      sprite(H, .8, .8, Blue.withAlpha(.6 * handK), glow, Y + .1);
      sprite(H, .28, .28, White.withAlpha(.85 * handK), glow, Y + .101);
    }
    const arcK = s < t.full ? smooth((s - .1) / .15) : 1 - clamp((s - t.full) / .25);
    if (s >= .1 && arcK > 0) {
      const Cg = place(0, 0, ChestUp), sweep = ArcSweep * D2R * smooth(clamp((s - .1) / .6));
      Arcs.forEach(([R, from], i) => ribbon(`blue2 arc ${i}`, Cg, R, from * D2R - sweep, 2.1, arcK));
    }

    // --- open, streaks, rays, the sphere ---------------------------------------------------------------------------------
    if (s >= t.open && s < t.full + .1) { const u = (s - t.open) / (Grow + .1), kk = Math.sin(Math.PI * clamp(u)); glint('blue2 open', C, .6 + 1.4 * kk, .95 * kk, Ice, 20); }
    if (s >= t.full && s < t.burst) {
      for (let i = 0; i < Streaks; i++) {
        const per = StreakPeriod * (1 - .4 * late), ph = ((s - t.full) / per + rand(i + 900)) % 1, a0 = rand(i + 910) * TAU, pts = [];
        for (let m = 0; m <= 6; m++) {
          const v = Math.max(0, ph - .22 + .22 * m / 6), rr = p.coreR + (p.pullR - p.coreR) * (1 - v) ** 1.4, q = a0 - 2 * v, hh = ChestUp * v * v;
          pts.push(polar(q, rr * shrink, hh));
        }
        line(`blue2 streak ${i}`, pts, .045, (i % 3 ? Ice : White).withAlpha(.5 * k * Math.sin(Math.PI * ph)), whiteGlow, Y + .09, 'both');
      }
      const rayK = smooth((s - t.full - .8) / .5) * (.5 + .5 * late) * shrink;
      if (rayK > 0) for (let i = 0; i < Rays; i++) {
        const q = rand(i + 1600) * TAU + s * .4 * (i % 2 ? 1 : -1), len = (1.4 + 1.6 * rand(i + 1610)) * (.7 + .3 * Math.sin(s * 9 + i * 2)), fl = .6 + .4 * Math.abs(Math.sin(s * 17 + i * 3.1));
        line(`blue2 ray ${i}`, [C, { x: C.x + Math.cos(q) * len * .5, z: C.z + Math.sin(q) * len * .5 }, { x: C.x + Math.cos(q) * len, z: C.z + Math.sin(q) * len }], .14, Ice.withAlpha(.35 * rayK * fl), whiteGlow, Y + .095, 'end');
      }
    }
    if (live) sphere('blue2 sphere', C, p.coreR * grow * shrink, s, swirl, packedPieces.slice(-20), s >= t.implode ? .6 + .4 * shrink : 1);

    // --- the implosion -----------------------------------------------------------------------------------------------------
    if (burst) {
      if (age < .22) {
        const f = age / .22;
        sprite(C, 3 * (1 - f) + .5, 3 * (1 - f) + .5, White.withAlpha(.95 * (1 - f)), glow, Y + .2);
        sprite(C, 6 * (1 - .4 * f), 6 * (1 - .4 * f), Blue.withAlpha(.65 * (1 - f)), glow, Y + .199);
        for (let i = 0; i < 12; i++) {
          const q = i * TAU / 12 + rand(i + 1700) * .4, len = (2.5 + 1.5 * rand(i + 1710)) * (.4 + .6 * f);
          line(`blue2 burst ray ${i}`, [C, { x: C.x + Math.cos(q) * len, z: C.z + Math.sin(q) * len }], .18, Ice.withAlpha(.7 * (1 - f)), whiteGlow, Y + .198, 'end');
        }
      }
      if (age < .35) {
        const f = age / .35, rr = .3 + (p.burstR - .3) * (1 - (1 - f) ** 2);
        ringAt(Cf, rr, White.withAlpha(.85 * (1 - f)), Floor + .035, false, whiteGlow);
        ringAt(Cf, rr * 1.04, Blue.withAlpha(.6 * (1 - f)), Floor + .034, true, whiteGlow);
      }
      // The dust cloud: grey puffs rising from the crater and spreading, then thinning.
      for (let i = 0; i < Clouds; i++) {
        const u = clamp((age - rand(i + 1800) * .12) / 1.8);
        if (u <= 0 || u >= 1) continue;
        const q = rand(i + 1810) * TAU, rr = .3 + (1 + 1.8 * rand(i + 1820)) * smooth(u), up = .2 + (.5 + 1.6 * rand(i + 1830)) * smooth(u), size = (1.3 + 2.4 * smooth(u)) * (.8 + .4 * rand(i + 1840));
        sprite(polar(q, rr, up), size, size * .85, Cloud.withAlpha(.75 * (1 - u) ** 1.3 * smooth(u / .06)), puff, Y + .03 + i * .0004, rand(i + 1850) * 360 + u * 60);
      }
      // Pieces thrown out: they land and stay.
      for (let i = 0; i < Thrown; i++) {
        const g = { i: i + 2000, kind: i % 4 === 0 ? 'plank' : 'slab', size: i % 4 === 0 ? .4 : .18 + .2 * rand(i + 1900) };
        const q = i * TAU / Thrown + (rand(i + 1910) - .5) * .5, reach = 1 + 2.5 * rand(i + 1920), T = .3 + .2 * rand(i + 1930), u = clamp(age / T);
        const rr = reach * (1 - (1 - u) ** 2), h = ChestUp * (1 - u) + .7 * Math.sin(Math.PI * u), ground = polar(q, rr);
        if (u < 1) sprite({ x: ground.x + sun.x * h, z: ground.z + sun.z * h }, g.size, g.size * .6, Ink.withAlpha(.35), soft, Floor + .05);
        debris(g, polar(q, rr, h), rand(i + 1940) * 360 + age * 700 * (1 - u), u < 1 ? Y + .05 : Floor + .06);
      }
    }
  },
};
