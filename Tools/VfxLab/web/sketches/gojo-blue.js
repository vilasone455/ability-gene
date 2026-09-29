// Lapse: Blue — Gojo kit, ability sketch, not the game.
//
// Mechanic (agreed 2026-09-27, docs/hero-echo.md: "a lesser Gravity Well"; numbers are placeholders
// for XML on CompProperties_AbilityGravityWell, which Blue reuses):
//   Gojo points at a cell up to 15 away and Blue opens there at chest height. For 3 s it pulls pawns and
//   loose things within 4 cells (fixed radius, it does not grow): 6 cells/s at the core edge, falling to 0
//   at 4 cells, divided by body size over 1. Pawns that reach the 1-cell core are held there; items and
//   chunks that reach it are crushed into the ball. Allies are pulled too. No bullet bending. At the end
//   it implodes: 10-25 blunt by the mass it holds, radius 2. No stun. Cooldown 20 s, 3 charge. Red shot
//   within 1 cell of an active Blue's centre makes Hollow Purple (not in this sketch).
//   Placeholders of mine, not yet agreed: core radius 1, implosion radius 2, items in the core crushed.
//
// Sources (checked 2026-09-29, YouTube storyboards):
//   qtlY0Vdy_GA "Lapse: Blue"  5-6 s blue-white light at Gojo's hand; 11 s roof tiles and wood flying
//                        into blue light; 12-13 s a big blue ball of sparkling light over a courtyard;
//                        17 s afterwards a round crater, torn ground, broken trees
//   Ybn2csWuIgg (S2 ep 3)  4 s white-blue burst at the hand with curved white streaks round it; 6-7 s dark
//                        debris dragged along curved paths, pale curved streaks converging; 13 s the
//                        ball as deep blue light packed with broken wood and debris; 14 s arm out,
//                        debris flying past him into it; 29 s (in the Purple scene) a deep blue sphere
//                        with a pale swirling shell and white specks
//   Pain's Gravity Well (recorded) is a black core with pale orbit loops, a dark ground shade and white
//   and grey rings, so Blue stays bright blue light with no black core and no timer ring.
//
// Order ("group", the default; Blue 8 cells from Gojo, hold 3 s):
//   0.00-0.15   Gojo's arm comes up and points at the cell; blue light at his hand, 3 white arcs curling
//               round it
//   0.15-0.45   open: a white-blue star glint at the cell, then the ball grows to 0.6 cells, a faint blue
//               ring at the 4-cell pull radius, blue light on the floor
//   0.45-3.45   hold: a deep blue ball with a faint pale shell swirling inward (4 arcs), 26 white and cyan
//               specks on it and in a halo round it, a white centre; 14 pale streaks curling in from the pull radius; 24 bits of dirt, leaves and
//               splinters spiral in and rise into it; what arrives is packed into the ball as dark
//               debris (the S2 ep 3 frame). Raiders at 2.5 and 3.6 cells and an ally at 3.2 are dragged in
//               (dust at their feet, drag marks stay), held at the 1-cell core, lifted 0.25 cells, lit blue;
//               a stone chunk and a rifle are pulled in and crushed into the ball. A raider at 5 cells
//               is outside the radius and only lit. The swirl speeds up over the last 0.5 s.
//   3.45-3.57   implode: the ball shrinks to a point, the swirl tightens
//   3.57        a white flash, a blue flash, a ring to the 2-cell implosion radius, the packed debris
//               thrown out and lying 1-2.5 cells round; held pawns flash white and drop to the ground
//               (they stay on their feet); shake
//   stays       a round crater 1.1 cells across with a torn rim and cracks, debris, drag marks
//   "empty ground": no pawns or items, only the dirt and debris.
//
// Drawing: level circles, flat spirals and quads only, so there is nothing per facing; the aim only
// places the ball. Height is drawn north (0.6 per cell) with shadows on the floor. The ball is light
// (additive layers) over a deep-blue body; debris is irregular lumps (rock()). Gojo is lib/gojo.js's
// stand-in with a pointing arm drawn here; pawns, chunk and rifle are stand-ins.
import { Color, Mathf, Meshes, MeshPool } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import { caster, Uniform } from './lib/gojo.js';
import { Blue, Deep, Ice } from './lib/vergil.js';
import { pawn, rock, ringAt, line, streak, glint, whiteGlow, EnemyColour, Ally, Ink, White, Dust, Lift, Chest, Skin, pawnLayer, shadowLayer, clamp, smooth } from './lib/goku.js';

const TAU = Math.PI * 2, D2R = Mathf.Deg2Rad, ChestUp = Chest / Lift;
const disc = Meshes.disc(32, 'blue disc');
// Decided values.
const Raise = .15, Open = .15, Grow = .3, Implode = .12, Tail = 1.5, Reach = .5, HeldLift = .25;
const Streaks = 14, StreakPeriod = .6, Bits = 24, BitGap = .105, ShellArms = 4, Specks = 26, Rush = .5;
const ThrownBits = 8, CraterR = .55, Cracks = 6;
const Sat = new Color(.1, .3, 1), Cyan = new Color(.45, .85, 1), Crater = new Color(.16, .13, .1), Torn = new Color(.45, .38, .3);
const Leaf = new Color(.3, .42, .16), Wood = new Color(.42, .3, .18), Steel = new Color(.45, .46, .5);
// Things round Blue for "group": [angle from Gojo's aim (degrees), cells from Blue's centre, kind].
const Group = [[60, 2.5, 'raider'], [200, 3.6, 'raider'], [-70, 3.2, 'ally'], [150, 5, 'raider'], [-20, 1.8, 'chunk'], [110, 2.8, 'rifle']];

const empty = p => p.scenario === 'empty ground';
function times(p) {
  const open = Raise, full = open + Grow, implode = full + p.hold, burst = implode + Implode;
  return { open, full, implode, burst, end: burst + Tail };
}
// Distance from the centre after being pulled for t seconds from r0: the pull falls linearly from
// pullSpeed at the core edge to 0 at the pull radius, so R - r grows as e^(k t). Stops at the core.
function pulled(r0, t, p) {
  if (r0 >= p.pullR || t <= 0) return r0;
  if (r0 <= p.coreR) return r0;
  const k = p.pullSpeed / (p.pullR - p.coreR), x = (p.pullR - r0) * Math.exp(k * t);
  return Math.max(p.coreR, p.pullR - x);
}
const reachCore = (r0, p) => r0 >= p.pullR ? Infinity : r0 <= p.coreR ? 0 : Math.log((p.pullR - p.coreR) / (p.pullR - r0)) / (p.pullSpeed / (p.pullR - p.coreR));

// Gojo's arm held out at the cell: sleeve, hand, index finger. out 0..1.
function pointingArm(place, deg, out, layer) {
  if (out <= .02) return;
  const reach = Reach * out, sleeve = place(reach * .4, 0, ChestUp), hand = place(reach, 0, ChestUp), tip = place(reach + .16 * out, 0, ChestUp);
  draw(MeshPool.plane10, sleeve.x, layer, sleeve.z, .12, reach * .8, 90 - deg, Uniform);
  draw(disc, hand.x, layer + .001, hand.z, .06, .06, 0, Skin);
  draw(MeshPool.plane10, (hand.x + tip.x) / 2, layer + .0015, (hand.z + tip.z) / 2, .03, .16 * out, 90 - deg, Skin);
}

// The ball: glow, a deep-blue body, packed debris inside, a pale shell swirling inward, white specks,
// a white centre and a pale rim. swirl is the shell's turn (radians); packed how many debris bits it holds.
function blueBall(key, at, r, s, swirl, packed, alpha = 1) {
  if (r <= .01 || alpha <= 0) return;
  const beat = 1 + .05 * Math.sin(s * 9);
  sprite(at, r * 9 * beat, r * 9 * beat, Blue.withAlpha(.45 * alpha), glow, Y + .1);
  sprite(at, r * 4.2, r * 4.2, Cyan.withAlpha(.4 * alpha), glow, Y + .101);
  draw(disc, at.x, Y + .11, at.z, r, r, 0, Deep.withAlpha(alpha));
  draw(disc, at.x, Y + .111, at.z + r * .06, r * .86, r * .86, 0, Sat.withAlpha(alpha));
  // Packed debris, turning slowly with the shell.
  for (let k = 0; k < packed; k++) {
    const q = k * 2.4 + swirl * .25, d = r * (.15 + .6 * rand(k + 700));
    rock({ x: at.x + Math.cos(q) * d, z: at.z + Math.sin(q) * d }, r * (.22 + .14 * rand(k + 710)), rand(k + 720) * 360 + swirl * 20, .85 * alpha, k % 6, Y + .112 + k * .0002);
  }
  draw(disc, at.x, Y + .116, at.z, r * .9, r * .9, 0, Deep.withAlpha(.35 * alpha));
  for (let i = 0; i < ShellArms; i++) {
    const pts = [], a0 = swirl + i * TAU / ShellArms;
    for (let k = 0; k <= 10; k++) { const v = k / 10, rr = r * (1.05 - .75 * v), q = a0 + v * 2.2; pts.push({ x: at.x + Math.cos(q) * rr, z: at.z + Math.sin(q) * rr }); }
    line(`${key} shell ${i}`, pts, r * .2, Cyan.withAlpha(.35 * alpha), whiteGlow, Y + .117, 'both');
    line(`${key} shell ${i} core`, pts, r * .05, White.withAlpha(.45 * alpha), whiteGlow, Y + .118, 'both');
  }
  // Glitter: white and cyan specks turning with the swirl, most on the ball, some in a halo round it.
  for (let i = 0; i < Specks; i++) {
    const q = rand(i + 730) * TAU + swirl * (.8 + .6 * rand(i + 740)), d = r * (.3 + 1.5 * rand(i + 750) ** 1.5), tw = .3 + .7 * Math.abs(Math.sin(s * 13 + i * 1.7)), size = .05 + .06 * rand(i + 760);
    sprite({ x: at.x + Math.cos(q) * d, z: at.z + Math.sin(q) * d }, size * 1.6, size * 1.6, (i % 3 ? White : Cyan).withAlpha(tw * alpha), glow, Y + .119);
  }
  sprite(at, r * .8, r * .8, White.withAlpha(.55 * alpha), glow, Y + .12);
  ringAt(at, r, Ice.withAlpha(.7 * alpha), Y + .121, false, whiteGlow);
}

export default {
  kit: 'Gojo', label: 'Lapse: Blue (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'group', options: ['group', 'empty ground'], group: 'Showcase' },
    aim: P('Aim (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    dist: P('Blue from Gojo (cells, up to 15)', 8, 3, 15, .5, 'Showcase'),
    hold: P('Hold', 3, 1, 6, .25, 'Timing (s)'),
    pullR: P('Pull radius (cells)', 4, 2, 6, .25, 'Mechanic'),
    coreR: P('Core radius (cells)', 1, .5, 2, .25, 'Mechanic'),
    pullSpeed: P('Pull at the core edge (cells/s)', 6, 1, 12, .5, 'Mechanic'),
    burstR: P('Implosion radius (cells)', 2, 1, 4, .25, 'Mechanic'),
    ballR: P('Ball radius (cells)', .6, .2, 1, .05, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Point', t: 0 }, { name: 'Open', t: t.open }, { name: 'Hold', t: t.full }, { name: 'Implode', t: t.implode }, { name: 'Burst', t: t.burst }];
  },
  events(p) { const t = times(p); return [{ t: t.open, type: 'shake', value: .02 }, { t: t.burst, type: 'shake', value: .1 }]; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * D2R, ca = Math.cos(a), sa = Math.sin(a);
    // The chosen cell is the middle of Gojo and the far edge of the pull; G is Gojo, Cf Blue's floor point.
    const span = p.dist + p.pullR, G = { x: o.x - ca * span / 2, z: o.z - sa * span / 2 };
    const place = (along, across = 0, up = 0) => ({ x: G.x + along * ca - across * sa, z: G.z + along * sa + across * ca + up * Lift });
    const Cf = place(p.dist), C = place(p.dist, 0, ChestUp);
    const around = (deg, r, up = 0) => { const q = a + deg * D2R; return { x: Cf.x + Math.cos(q) * r, z: Cf.z + Math.sin(q) * r + up * Lift }; };
    const live = s >= t.open && s < t.burst, pullT = Math.max(0, Math.min(s, t.implode) - t.full);
    const grow = smooth((s - t.open) / Grow), shrink = s >= t.implode ? 1 - smooth((s - t.implode) / Implode) : 1;
    const rush = smooth((s - (t.implode - Rush)) / Rush), swirl = -(s * 3.2 + rush * rush * 4) - (s >= t.implode ? (s - t.implode) * 30 : 0);
    const age = s - t.burst, burst = age >= 0;

    // --- floor: blue light, the pull radius, drag marks and the crater that stay --------------------------------------
    if (live) {
      const k = grow * shrink;
      sprite(Cf, 2.6 * k, 2 * k, Blue.withAlpha(.35 * k), glow, Floor + .03);
      ringAt(Cf, p.pullR, Blue.withAlpha(.22 * grow * (s < t.implode ? 1 : shrink)), Floor + .031, false, whiteGlow);
    }
    const things = empty(p) ? [] : Group.map(([deg, r0, kind], i) => {
      const r = pulled(r0, pullT, p), tCore = reachCore(r0, p), inCore = pullT >= tCore;
      return { deg, r0, kind, i, r, inCore, coreAt: t.full + tCore };
    });
    things.filter(g => g.kind !== 'chunk' && g.kind !== 'rifle' && g.r < g.r0 - .05).forEach(g =>
      [-.1, .1].forEach((x, j) => {
        const from = around(g.deg, g.r0 - .1), to = around(g.deg, g.r + .25), q = a + g.deg * D2R, nx = -Math.sin(q) * x, nz = Math.cos(q) * x;
        line(`blue drag ${g.i} ${j}`, [{ x: from.x + nx, z: from.z + nz }, { x: to.x + nx, z: to.z + nz }], .045, Ink.withAlpha(.28), undefined, Floor + .012, 'none');
      }));
    if (burst) {
      const g = smooth(age / .1), cr = CraterR * 2 * g;
      sprite(Cf, cr * 1.5, cr * 1.2, Torn.withAlpha(.55), soft, Floor + .008);
      sprite(Cf, cr, cr * .8, Crater.withAlpha(.85), soft, Floor + .009);
      ringAt(Cf, CraterR * 1.1 * g, Torn.withAlpha(.75), Floor + .0095, true);
      ringAt(Cf, CraterR * 1.3 * g, Crater.withAlpha(.35), Floor + .0093, false);
      for (let i = 0; i < Cracks; i++) {
        const q = i * TAU / Cracks + (rand(i + 800) - .5) * .6, len = (.5 + .5 * rand(i + 810)) * g, pts = [];
        for (let k = 0; k <= 6; k++) { const d = CraterR * .8 + len * k / 6, off = k ? (rand(i * 7 + k + 820) - .5) * .1 : 0; pts.push({ x: Cf.x + Math.cos(q) * d - Math.sin(q) * off, z: Cf.z + Math.sin(q) * d + Math.cos(q) * off }); }
        line(`blue crack ${i}`, pts, .045, Ink.withAlpha(.6), undefined, Floor + .011);
      }
    }

    // --- dirt, leaves and splinters spiralling in; they rise to the ball and are packed into it ----------------------------
    let packed = 0;
    for (let i = 0; i < Bits; i++) {
      const born = t.full - .1 + i * BitGap * (p.hold / 3), T = .9 + .6 * rand(i + 600), u = (Math.min(s, t.implode) - born) / T;
      if (u >= 1) { packed++; continue; }
      if (u < 0 || !live) continue;
      const r0 = 1.8 + (p.pullR - 1.8) * rand(i + 610), a0 = rand(i + 620) * TAU, e = u * u;
      const r = p.ballR * .6 + (r0 - p.ballR * .6) * (1 - e), q = a0 - 2.4 * e, h = ChestUp * e * e;
      const ground = { x: Cf.x + Math.cos(q) * r, z: Cf.z + Math.sin(q) * r }, at = { x: ground.x, z: ground.z + h * Lift };
      if (h > .05) sprite({ x: ground.x + sun.x * h, z: ground.z + sun.z * h }, .12, .08, Ink.withAlpha(.3), soft, Floor + .05);
      const kind = i % 3, spin = rand(i + 630) * 360 + u * 900;
      if (kind === 0) rock(at, .12 + .06 * rand(i + 640), spin, 1, 2 * (i % 3) + 1, Y + .05);
      else draw(MeshPool.plane10, at.x, Y + .05 + i * .0002, at.z, kind === 1 ? .12 : .05, kind === 1 ? .08 : .22, spin, kind === 1 ? Leaf : Wood);
    }
    things.filter(g => (g.kind === 'chunk' || g.kind === 'rifle') && g.inCore && s >= g.coreAt + .2).forEach(() => { packed += 3; });

    // --- pawns and items, north first ---------------------------------------------------------------------------------------
    const lightAt = pos => live ? .55 * (1 - clamp(Math.hypot(pos.x - Cf.x, pos.z - Cf.z) / (p.pullR + 1.5))) * grow * shrink : 0;
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
        caster(g.pos, sun, strength, { tint: Blue, tintAmount: lit });
        if (!north) pointingArm(place, p.aim, out, pawnLayer + .016);
        return;
      }
      if (g.kind === 'chunk') { if (g.gone < 1) rock(g.pos, .45 * (1 - .7 * g.gone), 30 + g.gone * 400, 1, 2, g.up > .02 ? Y + .06 : Floor + .06); return; }
      if (g.kind === 'rifle') { if (g.gone < 1) draw(MeshPool.plane10, g.pos.x, g.up > .02 ? Y + .06 : Floor + .06, g.pos.z, .09 * (1 - .6 * g.gone), .62 * (1 - .6 * g.gone), 40 + g.gone * 500, Steel); return; }
      const flash = burst && g.inCore ? 1 - clamp(age / .25) : 0, colour = g.kind === 'ally' ? Ally : EnemyColour;
      if (g.up > .02) sprite({ x: g.ground.x + sun.x * .45, z: g.ground.z + sun.z * .45 }, .85, .4, Ink.withAlpha(strength), soft, shadowLayer);
      pawn(g.pos, colour, sun, g.up > .02 ? 0 : strength, { tint: flash > 0 ? White : Blue, tintAmount: flash > 0 ? .7 * flash : lightAt(g.pos) });
    });

    // --- Gojo's hand: blue light and white arcs curling round it while Blue opens -----------------------------------------
    const handK = smooth(s / Raise) * (1 - smooth((s - t.full) / .3));
    if (handK > 0) {
      const H = place(Reach + .1, 0, ChestUp);
      sprite(H, .7, .7, Blue.withAlpha(.6 * handK), glow, Y + .1);
      sprite(H, .25, .25, White.withAlpha(.8 * handK), glow, Y + .101);
      for (let i = 0; i < 3; i++) {
        const pts = [], a0 = -s * 12 + i * TAU / 3;
        for (let k = 0; k <= 8; k++) { const q = a0 + k / 8 * 1.6, rr = .18 + .14 * k / 8; pts.push({ x: H.x + Math.cos(q) * rr, z: H.z + Math.sin(q) * rr }); }
        line(`blue hand arc ${i}`, pts, .05, White.withAlpha(.85 * handK), whiteGlow, Y + .102, 'both');
      }
    }

    // --- open: a star glint, then the ball grows ---------------------------------------------------------------------------
    if (s >= t.open && s < t.full + .1) {
      const u = (s - t.open) / (Grow + .1), k = Math.sin(Math.PI * clamp(u));
      glint('blue open', C, .5 + 1.2 * k, .95 * k, Ice, 20);
    }
    // --- hold: streaks curling in, then the ball ------------------------------------------------------------------------------
    if (s >= t.full && s < t.implode + Implode) {
      const k = s < t.implode ? smooth((s - t.full) / .2) : shrink;
      for (let i = 0; i < Streaks; i++) {
        const per = StreakPeriod * (1 - .4 * rush), ph = ((s - t.full) / per + rand(i + 900)) % 1, a0 = rand(i + 910) * TAU, pts = [];
        for (let m = 0; m <= 6; m++) {
          const v = Math.max(0, ph - .22 + .22 * m / 6), rr = p.ballR + (p.pullR - p.ballR) * (1 - v) ** 1.4, q = a0 - 2 * v, hh = ChestUp * v * v;
          pts.push({ x: Cf.x + Math.cos(q) * rr, z: Cf.z + Math.sin(q) * rr + hh * Lift });
        }
        line(`blue streak ${i}`, pts, .045, (i % 3 ? Ice : White).withAlpha(.55 * k * Math.sin(Math.PI * ph)), whiteGlow, Y + .09, 'both');
      }
      // Dust dragged along the floor toward the centre.
      for (let i = 0; i < 10; i++) {
        const ph = ((s - t.full) / 1.1 + rand(i + 950)) % 1, q = rand(i + 960) * TAU - 1.2 * ph, rr = p.coreR * .8 + (p.pullR - p.coreR * .8) * (1 - ph);
        sprite({ x: Cf.x + Math.cos(q) * rr, z: Cf.z + Math.sin(q) * rr }, .5 - .25 * ph, .4 - .2 * ph, Dust.withAlpha(.35 * Math.sin(Math.PI * ph) * k), soft, Floor + .04);
      }
    }
    if (live) blueBall('blue ball', C, p.ballR * grow * shrink * (s >= t.implode ? 1 : 1 + .06 * rush), s, swirl, Math.min(16, packed), s >= t.implode ? .6 + .4 * shrink : 1);
    // Pawns being dragged: dust at their feet.
    things.filter(g => g.kind === 'raider' || g.kind === 'ally').forEach(g => {
      if (g.r >= g.r0 - .02 || g.inCore || !live) return;
      for (let k = 0; k < 3; k++) {
        const ph = (s * 4 + k / 3 + rand(g.i + k + 990)) % 1, at = around(g.deg, g.r + .15 + .5 * ph);
        sprite(at, .25 + .25 * ph, .2 + .2 * ph, Dust.withAlpha(.4 * (1 - ph)), soft, Floor + .042);
      }
    });

    // --- the implosion ---------------------------------------------------------------------------------------------------
    if (burst) {
      if (age < .2) {
        const f = age / .2;
        sprite(C, 2.4 * (1 - f) + .4, 2.4 * (1 - f) + .4, White.withAlpha(.9 * (1 - f)), glow, Y + .2);
        sprite(C, 4.5 * (1 - .5 * f), 4.5 * (1 - .5 * f), Blue.withAlpha(.6 * (1 - f)), glow, Y + .199);
      }
      if (age < .3) {
        const f = age / .3, rr = .3 + (p.burstR - .3) * (1 - (1 - f) ** 2);
        ringAt(Cf, rr, White.withAlpha(.8 * (1 - f)), Floor + .035, false, whiteGlow);
        ringAt(Cf, rr * 1.04, Blue.withAlpha(.6 * (1 - f)), Floor + .034, true, whiteGlow);
      }
      if (age < .6) for (let i = 0; i < 12; i++) {
        const u = clamp((age - rand(i + 1000) * .05) / .55), q = i * TAU / 12 + rand(i + 1010), rr = .3 + p.burstR * .9 * smooth(u);
        sprite({ x: Cf.x + Math.cos(q) * rr, z: Cf.z + Math.sin(q) * rr * .8 + u * .1 }, .35 + .5 * u, .3 + .4 * u, Dust.withAlpha(.45 * Math.sin(u * Math.PI)), soft, Floor + .04);
      }
      // The packed debris thrown out; it lands and stays.
      for (let i = 0; i < ThrownBits; i++) {
        const q = i * TAU / ThrownBits + (rand(i + 1020) - .5) * .7, reach = 1 + 1.5 * rand(i + 1030), T = .3 + .15 * rand(i + 1040), u = clamp(age / T);
        const rr = reach * (1 - (1 - u) ** 2), h = ChestUp * (1 - u) + .5 * Math.sin(Math.PI * u), ground = { x: Cf.x + Math.cos(q) * rr, z: Cf.z + Math.sin(q) * rr };
        if (u < 1) sprite({ x: ground.x + sun.x * h, z: ground.z + sun.z * h }, .16, .1, Ink.withAlpha(.35), soft, Floor + .05);
        rock({ x: ground.x, z: ground.z + h * Lift }, .14 + .1 * rand(i + 1050), rand(i + 1060) * 360 + age * 700 * (1 - u), 1, i % 6, u < 1 ? Y + .05 : Floor + .06);
      }
    }
  },
};
