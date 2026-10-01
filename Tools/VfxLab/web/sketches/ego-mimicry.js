// Mimicry — E.G.O. weapon proposal, not the game. Nothing in Source/RimArt draws this yet.
// Nothing There (Lobotomy Corporation, ALEPH): the Red Mist's sword. The rules are
// docs/ego-weapons.md, "Weapon 3: Mimicry" (design agreed 2026-09-30), with one change agreed
// 2026-10-01: corrosion grows the arm, not the blade, because Samehada already lengthens its blade
// per hit. Every number is a placeholder and becomes an XML field on CompProperties_EgoWeapon:
//   The sword: melee, 1 cell. Each hit heals the wielder 10 % of the damage dealt (healFraction).
//   The grown swing: 10 % per swing (growChance, or every 8th hit: growEveryHits). The blade swells
//         to 2x for one heavy downswing at 3.5x damage (growDamageFactor), then shrinks back. Its
//         length does not stay: that is Samehada's picture.
//   Corrosion (shared system): bands 50 / 100 / 100 %, requirement Melee 8, 40 s, one action every
//         1.5 s. The action: a swing at the nearest standing pawn of any faction (downed pawns are
//         skipped, so it does not finish them; a choice made for the sketch, not in the doc); a
//         2-cell lunge first if nobody is adjacent. Exhaustion 3 h afterwards (not drawn).
//   The arm (agreed 2026-10-01, from Limbus's Corroded Inquisitors and their Instincts): the flesh
//         takes the sword arm in 4 stages, hand, forearm, arm and shoulder, face. Corrosion starts
//         at stage 1; each hit dealt adds a stage (cap 4), each hit taken removes one. Each stage
//         +15 % melee damage (stageDamage). All stages go when the state ends.
//   Overclock: the same action 5 times at hostiles only (allies next to the wielder are skipped),
//         then mood -20 for 1 day (not drawn).
//
// Look, from the source (research 2026-10-01, the links are in the session report and the kit memory):
//   Sword (Lobotomy weapon sprite): single-edged, the blade about 4x the grip. A dark steel edge
//         curving up into the point; red muscle over the back half, thickest at the hilt, where it
//         wraps the whole blade in a bulb; fibres along it; a big green-iris eye near the hilt and a
//         small blue-iris eye mid-blade; three bone spikes on the back and two small ones under the
//         flesh; a thin black grip with a red ring at the guard and a black end cap; black outlines.
//   Swing trail (Ruina battle frames): a wide crescent of red flesh with white eyes in it and a
//         white streak on its leading edge.
//   Grown swing (Lobotomy text "enlarge blade and swing downwards"; Ruina card art): the blade
//         swells, rises overhead and comes down; on the hit a yellow-white split line on the floor,
//         dark red streaks along it, flesh with eyes bursting out of the blade, a crack that stays.
//   The arm (Limbus Proceeding Inquisitor sprite): a pinker muscle bundle wider than the body, an
//         eye, a mouth with a row of white teeth, two long curved bone blades rising from it, dark
//         tendrils; then flesh over the face with one round eye and bared teeth.
//
// Order, "swings" (default sliders): three swings .9 s apart at a raider 1 cell off.
//   .30   wind-up .18 s: the blade turns edge-forward (wrist, .08 s) and goes back to 110 degrees
//         behind the aim on the hand side
//   .48   swing .12 s to 45 degrees past the aim; the flesh crescent (.42 cells thick at the blade,
//         75 % opaque) follows the blade, its tail .06 s behind, and fades .22 s after; the hit when the blade crosses the target (.04 s in)
//   hit   a white slash across the chest (.12 s), a red cut (.6 s), six blood drops that land and
//         stay; the raider rocks .07 cells. The heal: the blood on the steel slides along the flesh
//         into the green eye in .35 s, the eye glows, a faint red glow on the wielder's chest.
// Order, "grown swing": one normal swing, then at 1.20:
//   +0    swell .3 s: the blade grows to 2x, the flesh bulges and wobbles, the eyes open wide
//   +.30  raise .3 s: the hands go overhead (1.5 cells up), the blade points up and back over the
//         hand-side shoulder (leaned back 35 degrees from upright so it shows about 70 % of its
//         length on screen); the blade turns edge-forward
//   +.60  slam .1 s, accelerating, down to just under level along the aim
//   +.70  impact: the split light along the blade's footprint (.45 s), the red streaks (.3 s), seven flesh
//         lumps with eyes out of the blade (out .08 s, gone by .55 s), dust, ten rocks thrown, the
//         crack, the raider down with a pool of blood; shake .07
//   +.95  shrink .45 s back to size 1 and back to rest
// Order, "corroded" (the rule's 40 s cut to 8.7 s): an ally next to the wielder, a raider 3 cells
// off, a colonist with a rifle behind.
//   0     flesh creeps over the hand and grip in .9 s (stage 1); the blade's eyes open wide; a dim red
//         floor glow under the wielder
//   .9    swing 1 at the nearest standing pawn: the ally. Stage 2 (forearm, an eye opens on it).
//   2.4   swing 2: the ally again, who goes down. Stage 3 (whole arm swells, shoulder bulge, mouth,
//         two bone blades rise, tendrils)
//   3.9   nobody standing is adjacent: a 2-cell lunge (.2 s, two afterimages) at the raider, swing 3.
//         Stage 4 (flesh over the neck and face: one eye, teeth)
//   5.1   the colonist shoots the wielder: a chunk of flesh is torn off and lands, stage back to 3
//   5.4   swing 4 at the raider, who goes down; stage 4 again
//   6.5   corrosion ends: the arm recedes in 1 s
// "overclock (hostiles only)": the same arm, five swings 1 s apart at two raiders (the second after a
// lunge); the ally standing next to the wielder is never chosen (a thin green ring marks it).
// Lab aids: four stage pips under the wielder; a dashed red line to each chosen target.
//
// Drawing: the sword is Lobotomy's side view laid flat and turned to where the blade points, as
// RimWorld draws equipment; mirrored when aiming west so the flesh stays on top. During a swing it
// lies level at hand height, so it turns freely with the aim (no per-facing method); the crescent
// is a level arc at hand height. The overhead swing is the one 3D motion: the blade is a 3D
// direction projected with Lift, and the top of the raise leans toward the hand side by
// .2 + .8 x |sin(aim)| so aiming north or south it is a diagonal cut instead of a line along the
// screen's vertical axis (projection.md). Aiming north the sword and the arm draw under the pawn.
// The arm is drawn over the pawn as quads (the Vergil pose trick); it is placed on the south-facing
// stand-in for every facing, with the shoulder's across offset on the screen's x only (facing east or
// west it sits on the upper chest); at stage 3 it is .5 cells across at mid-arm, about the body's width; aiming north the face gets the eye but no teeth (the back of the head).
// Pawns are lib/pawn.js real-size stand-ins (average body).
import { Color, Mathf, Meshes, MaterialPool, ShaderDatabase } from '../js/engine.js';
import { P, Y, Floor, Lift, sprite, band, circle, soft, glow, rand } from './lib/six-paths-impact.js';
import { draw } from './lib/six-paths-solid.js';
import { pawn, at, pawnLayer, shadowLayer } from './lib/pawn.js';
import { tube, rifle, puff, Blood, Enemy, Holder, Ally, Dust, Flash } from './lib/chain-sickle.js';
import { line, strip, streak, whiteGlow, rock } from './lib/goku.js';

const clamp = Mathf.Clamp01, lerp = Mathf.Lerp, D2R = Mathf.Deg2Rad, TAU = Math.PI * 2;
const smooth = x => Mathf.Smooth(clamp(x));
const easeOut = x => 1 - Math.pow(1 - clamp(x), 3);
const bump = x => (x >= 0 && x <= 1) ? Math.sin(x * Math.PI) : 0;
const disc = Meshes.disc(24, 'mimicry disc');
const flatMat = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const dirOf = deg => ({ x: Math.cos(deg * D2R), z: Math.sin(deg * D2R) });
const side = d => ({ x: -d.z, z: d.x });                 // left of d
const add = (a, b, k = 1) => ({ x: a.x + b.x * k, z: a.z + b.z * k });
const mix = (a, b, t) => ({ x: lerp(a.x, b.x, t), z: lerp(a.z, b.z, t) });
const len2 = v => Math.hypot(v.x, v.z);
const unit2 = v => { const l = len2(v) || 1; return { x: v.x / l, z: v.z / l }; };
const degOf = v => Math.atan2(v.z, v.x) / D2R;
// 3D directions: x, z on the ground, h up.
const n3 = v => { const l = Math.hypot(v.x, v.z, v.h) || 1; return { x: v.x / l, z: v.z / l, h: v.h / l }; };
const lerp3 = (a, b, t) => n3({ x: lerp(a.x, b.x, t), z: lerp(a.z, b.z, t), h: lerp(a.h, b.h, t) });
const level = deg => ({ ...dirOf(deg), h: 0 });

// Palette. The sword is Lobotomy's deep red; the arm is Limbus's pinker muscle.
const Flesh = new Color(.60, .07, .06), FleshLit = new Color(.88, .24, .17), Fibre = new Color(.30, .02, .03);
const Steel = new Color(.23, .21, .23), SteelLit = new Color(.66, .64, .68), Ink = new Color(.06, .02, .02);
const Bone = new Color(.86, .80, .66), BoneLit = new Color(.97, .94, .85);
const GripC = new Color(.08, .07, .08), GripLit = new Color(.30, .29, .31), GripRing = new Color(.78, .12, .10);
const Sclera = new Color(.97, .94, .91), IrisGreen = new Color(.22, .72, .42), IrisBlue = new Color(.24, .40, .85), Pupil = new Color(.03, .02, .02);
const Meat = new Color(.74, .23, .29), MeatLit = new Color(.93, .47, .50), MeatDark = new Color(.34, .05, .10);
const Teeth = new Color(.98, .96, .90), Mouth = new Color(.12, .01, .03), Skin = new Color(.83, .70, .54);
const SplitCore = new Color(1, .97, .82), SplitGlow = new Color(1, .60, .20), Streak = new Color(.86, .11, .08), StreakDark = new Color(.28, .02, .02);
const HealRed = new Color(1, .28, .22), White = new Color(1, 1, 1), AllyRing = new Color(.45, .85, .45);

// The rule's fixed numbers here (placeholders from the doc).
const Stages = 4, LungeCells = 2, Adjacent = 1.6;
// Decided looks and timings.
const Lead = .3, Enter = .9, Exit = 1.0, LungeTime = .2, FallTime = .35;
const Rest = -35, Back = -110, Through = 45;           // swing angles from the aim, on the hand side
const SwingHold = .06, Recover = .3, WristTurn = .08, CrescentLag = .06, CrescentFade = .22, SlamHold = .25;
const HitFrac = 1 - Math.cbrt(1 - (0 - Back) / (Through - Back));   // share of the swing when the blade crosses the aim
const StageGrow = .35, StageShrink = .3;
// Body: lib/pawn.js average. Ground = a standing pawn's ground contact on screen (feet + .12, as
// its shadow); a point h cells up draws at z + Ground + h x Lift.
const Ground = -.42, HandScreen = -.05, HandH = (HandScreen - Ground) / Lift, ChestH = (.05 - Ground) / Lift;
const HandOut = .18, HandAcross = .1;
// The sword at size 1, in cells: a = along from the hand, v = across (+ = the back, the flesh side).
const PommelA = -.06, GuardA = .22;
const EdgeV = [[0, -.12], [.2, -.125], [.5, -.088], [.77, -.04], [.92, .02], [1, .065]];
const BackV = [[0, .02], [.5, .05], [.8, .08], [.93, .08], [1, .065]];
const TopV = [[0, .03], [.04, .1], [.12, .135], [.25, .13], [.42, .118], [.58, .1], [.7, .085], [.78, .075]];
const BulbV = [[0, -.05], [.04, -.14], [.1, -.145], [.2, -.095]];
const FleshEnd = .78;
const BackSpikes = [[.13, .065, .25], [.2, .115, .35], [.27, .07, .3]];   // u, length, lean toward the tip
const BellySpikes = [[.2, .045, .2], [.36, .04, .25]];

const scr = q => ({ x: q.x, z: q.z + Ground + q.h * Lift });
const shd = (q, sun) => ({ x: q.x + sun.x * q.h, z: q.z + Ground + sun.z * q.h });
function pl(pts, u) {
  if (u <= pts[0][0]) return pts[0][1];
  for (let i = 1; i < pts.length; i++) if (u <= pts[i][0]) {
    const [u0, v0] = pts[i - 1], [u1, v1] = pts[i];
    return v0 + (v1 - v0) * (u - u0) / (u1 - u0);
  }
  return pts[pts.length - 1][1];
}
const edgeV = u => pl(EdgeV, u), backV = u => pl(BackV, u), topV = u => pl(TopV, u);
const lowV = u => Math.min(lerp(edgeV(u) + .03, backV(u) - .012, smooth((u - .3) / .35)), u < .2 ? pl(BulbV, u) : 9);

// ---- The sword ------------------------------------------------------------------------------------
// hand3: the hand (ground point + h). V: the 3D direction the blade points. look: { blade (cells at
// size 1), g (size), flip (+1 flesh on the left of the screen direction, -1 mirrored, between: turning
// edge-on), swell, open, fuse (corroded flesh over the grip), blood, pulse, pulseU, eyePulse, layer }.
// Returns bp(u, v), a point on the blade, and the screen direction.
function sword(key, hand3, V, look, s, sun, strength) {
  const H = scr(hand3), BL = look.blade * look.g, g = look.g, f = look.flip;
  let dx = V.x, dz = V.z + V.h * Lift, k = Math.hypot(dx, dz);
  if (k < .05) { dx = 0; dz = .05; k = .05; }
  const dir = { x: dx / k, z: dz / k }, nrm = side(dir), rot = -degOf(dir);
  const pt = (a, v) => ({ x: H.x + dir.x * a * k + nrm.x * v * f, z: H.z + dir.z * a * k + nrm.z * v * f });
  const wob = u => look.swell * .016 * Math.sin(u * 26 + s * 24);
  const bp = (u, v) => pt(GuardA + u * BL, v * g);
  const L = look.layer, N = 18;

  // Shadow: the blade's line from the hand to the tip, cast along the sun.
  {
    const tip3 = { x: hand3.x + V.x * (GuardA + BL), z: hand3.z + V.z * (GuardA + BL), h: Math.max(0, hand3.h + V.h * (GuardA + BL)) };
    const a = shd(hand3, sun), b = shd(tip3, sun), m = mix(a, b, .35), n = side(unit2({ x: b.x - a.x, z: b.z - a.z }));
    const w = [.03, .13 * g * Math.max(.25, Math.abs(f)), 0];
    band(`${key} shadow`, [a, m, b].map((q, i) => add(q, n, w[i])), [a, m, b].map((q, i) => add(q, n, -w[i])), Ink.withAlpha(strength * .55), shadowLayer);
  }
  // Grip: black rod, a lit line, the red ring at the guard, the end cap; the hand on it.
  band(`${key} grip`, [pt(PommelA, -.024), pt(GuardA, -.024)], [pt(PommelA, .024), pt(GuardA, .024)], GripC, L - .004);
  band(`${key} grip lit`, [pt(PommelA + .02, .006), pt(GuardA - .03, .006)], [pt(PommelA + .02, .016), pt(GuardA - .03, .016)], GripLit, L - .0038);
  band(`${key} ring`, [pt(GuardA - .035, -.036), pt(GuardA + .005, -.036)], [pt(GuardA - .035, .036), pt(GuardA + .005, .036)], GripRing, L - .0036);
  const cap = pt(PommelA, 0);
  draw(disc, cap.x, L - .0035, cap.z, .038 * Math.max(.4, k), .034, rot, GripC);
  const handAt = pt(0, 0);
  if (look.fuse < .99) draw(disc, handAt.x, L - .003, handAt.z, .055, .05, rot, Skin);

  // Outlines (the source's black lines), then steel, then flesh.
  const us = n => Array.from({ length: n + 1 }, (_, i) => i / n), U = us(N), UF = us(14).map(u => u * FleshEnd);
  band(`${key} steel line`, U.map(u => bp(u, edgeV(u) - .014)), U.map(u => bp(u, backV(u) + .014)), Ink, L);
  band(`${key} flesh line`, UF.map(u => bp(u, lowV(u) - .014 - wob(u) * .5)), UF.map(u => bp(u, topV(u) + .014 + wob(u))), Ink, L + .0005);
  band(`${key} steel`, U.map(u => bp(u, edgeV(u))), U.map(u => bp(u, backV(u))), Steel, L + .001);
  const UL = U.filter(u => u >= .1);
  band(`${key} steel lit`, UL.map(u => bp(u, edgeV(u) + .006)), UL.map(u => bp(u, edgeV(u) + .02)), SteelLit.withAlpha(.85), L + .0015);
  if (look.blood > 0) {
    const UB = us(8).map(u => .5 + u * .42);
    band(`${key} blood`, UB.map(u => bp(u, edgeV(u) + .004)), UB.map(u => bp(u, Math.min(backV(u), edgeV(u) + .06) - .004)), Blood.withAlpha(.95 * look.blood), L + .002);
  }
  band(`${key} flesh`, UF.map(u => bp(u, lowV(u) - wob(u) * .5)), UF.map(u => bp(u, topV(u) + wob(u))), Flesh, L + .003);
  band(`${key} flesh shade`, UF.map(u => bp(u, lowV(u) - wob(u) * .5)), UF.map(u => bp(u, lerp(lowV(u), topV(u), .3))), Fibre.withAlpha(.35), L + .0032);
  const UR = UF.filter(u => u >= .04 && u <= .72);
  band(`${key} flesh lit`, UR.map(u => bp(u, topV(u) - .03 + wob(u))), UR.map(u => bp(u, topV(u) - .01 + wob(u))), FleshLit.withAlpha(.85), L + .0034);
  // Fibres along the muscle, and three strands where the flesh runs out along the back of the steel.
  for (let i = 0; i < 4; i++) {
    const fr = .22 + i * .17, u0 = .03 + .03 * i, u1 = FleshEnd - .04 - .05 * i, pts = [];
    for (let j = 0; j <= 10; j++) { const u = lerp(u0, u1, j / 10); pts.push(bp(u, lerp(lowV(u), topV(u), fr) + .006 * Math.sin(u * 40 + i * 2))); }
    line(`${key} fibre ${i}`, pts, .012 * g, Fibre.withAlpha(.75), flatMat, L + .0036, 'both');
  }
  for (let i = 0; i < 3; i++) {
    const u0 = FleshEnd - .1 + i * .02, u1 = FleshEnd + .05 + .04 * i;
    line(`${key} strand ${i}`, [bp(u0, backV(u0) + .03 - i * .01), bp((u0 + u1) / 2, backV((u0 + u1) / 2) + .018 - i * .006), bp(u1, backV(u1) + .004)], .022 * g, Flesh, flatMat, L + .0031, 'end');
  }
  // Bone spikes: three on the back, two small ones under the flesh near the hilt.
  const spike = (name, u, base, len, lean, down) => {
    const sgn = down ? -1 : 1, du = .022 / look.blade, apexU = u + lean * len / look.blade, apexV = base + sgn * (len * (1 + .25 * look.swell));
    band(`${key} ${name} line`, [bp(u - du * 1.6, base), bp(u + du * 1.6, base)], [bp(apexU, apexV + sgn * .016), bp(apexU, apexV + sgn * .016)], Ink, L + .005);
    band(`${key} ${name}`, [bp(u - du, base), bp(u + du, base)], [bp(apexU, apexV), bp(apexU, apexV)], Bone, L + .0052);
    band(`${key} ${name} lit`, [bp(u - du * .2, base), bp(u + du * .7, base)], [bp(apexU, apexV - sgn * .01), bp(apexU, apexV - sgn * .01)], BoneLit.withAlpha(.8), L + .0054);
  };
  BackSpikes.forEach(([u, l, lean], i) => spike(`spike ${i}`, u, topV(u) - .01, l, lean, false));
  BellySpikes.forEach(([u, l, lean], i) => spike(`belly ${i}`, u, lowV(u) + .01, l, lean, true));
  // Eyes: the big green one near the hilt, the small blue one mid-blade.
  const eye = (u, v, rx, rz, iris, ir, name) => {
    const c = bp(u, v), sx = rx * g * Math.max(.3, k), sz = rz * g * Math.max(.15, Math.abs(f)) * (1 + .3 * look.open);
    draw(disc, c.x, L + .006, c.z, sx + .012, sz + .012, rot, Ink);
    draw(disc, c.x, L + .0062, c.z, sx, sz, rot, Sclera);
    draw(disc, c.x, L + .0064, c.z, ir * g * Math.max(.3, k), ir * g * Math.max(.3, Math.abs(f)), rot, iris);
    const pr = ir * .45 * (1 - .45 * look.open + .5 * look.eyePulse);
    draw(disc, c.x, L + .0066, c.z, pr * g * Math.max(.3, k), pr * g, rot, Pupil);
    return c;
  };
  const big = eye(.22, .02, .06, .045, IrisGreen, .024, 'big');
  eye(.5, .054, .032, .024, IrisBlue, .013, 'small');
  // The heal: a red glow sliding along the flesh into the big eye, then the eye glowing.
  if (look.pulse > 0) sprite(bp(look.pulseU, .05), .24 * g, .17 * g, HealRed.withAlpha(.6 * look.pulse), glow, L + .0072, rot);
  if (look.eyePulse > 0) sprite(big, .3 * g, .26 * g, HealRed.withAlpha(.55 * look.eyePulse), glow, L + .0073);
  if (look.swell > 0) sprite(bp(.4, .02), BL * 1.1, .55 * g, Streak.withAlpha(.25 * look.swell), glow, L + .0074, rot);
  // Corroded: flesh grows over the hand and the grip and joins the bulb.
  if (look.fuse > 0) {
    const fu = look.fuse, A = [], B = [];
    for (let i = 0; i <= 6; i++) {
      const a = lerp(PommelA + (1 - fu) * .2, GuardA + .03, i / 6), w = (.05 + .035 * (i / 6)) * fu + .01 * Math.sin(i * 2 + s * 6) * fu;
      A.push(pt(a, -w)); B.push(pt(a, w));
    }
    band(`${key} fuse line`, A.map((q, i) => add(q, side(dir), -.012 * f)), B.map((q, i) => add(q, side(dir), .012 * f)), Ink, L + .0076);
    band(`${key} fuse`, A, B, Meat, L + .0077);
    band(`${key} fuse lit`, A.map((q, i) => mix(q, B[i], .55)), A.map((q, i) => mix(q, B[i], .8)), MeatLit.withAlpha(.7), L + .0078);
  }
  return { bp, dir, H, big, tip: bp(1, .065) };
}

// ---- The swing ------------------------------------------------------------------------------------
function swingT(p) { return { windup: p.windup, swing: p.swing, hitAt: p.windup + p.swing * HitFrac, len: p.windup + p.swing + SwingHold + Recover }; }
function swingRel(a, T) {
  if (a < 0) return Rest;
  if (a < T.windup) return lerp(Rest, Back, smooth(a / T.windup));
  if (a < T.windup + T.swing) return lerp(Back, Through, easeOut((a - T.windup) / T.swing));
  const r0 = T.windup + T.swing + SwingHold;
  return a < r0 ? Through : lerp(Through, Rest, smooth((a - r0) / Recover));
}
// The wrist: the flesh on top at rest, the edge leading from the wind-up to the end of the swing.
function swingFlip(a, T, sign) {
  const turn = smooth(a / WristTurn) * (1 - smooth((a - T.windup - T.swing - SwingHold) / WristTurn));
  return sign * (1 - 2 * turn);
}
// The hand: out in front of the body, a little to the hand side, turning with the blade.
function handAt(pos, deg, sign, h = HandH) {
  const d = dirOf(deg), q = add(add(pos, d, HandOut), side(d), -HandAcross * sign);
  return { x: q.x, z: q.z, h };
}
const signOf = deg => Math.cos(deg * D2R) < -1e-6 ? -1 : 1;

// The flesh crescent behind a swing, level at hand height round the wielder (Ruina): .42 cells thick
// at the blade, thinning to the tail, a white streak on the outer edge of its leading part, four white eyes.
function crescent(key, centre, aim, sign, a, T, reach) {
  const t0 = T.windup;
  if (a < t0 || a > t0 + T.swing + CrescentFade) return;
  const head = lerp(Back, Through, easeOut((a - t0) / T.swing)), tail = lerp(Back, Through, easeOut((a - t0 - CrescentLag) / T.swing));
  const fade = 1 - smooth((a - t0 - T.swing) / CrescentFade);
  if (head - tail < 2 || fade <= 0) return;
  const rOut = reach, Thick = .42, N = 22, inner = [], outer = [], lit = [], litIn = [], edge = [], edgeIn = [];
  const P = (rel, r) => { const d = dirOf(aim + sign * rel); return { x: centre.x + d.x * r, z: centre.z + d.z * r }; };
  for (let i = 0; i <= N; i++) {
    const u = i / N, rel = lerp(tail, head, u), th = Thick * Math.pow(u, .8) * (.55 + .45 * fade);
    outer.push(P(rel, rOut)); inner.push(P(rel, rOut - th));
    lit.push(P(rel, rOut - th * .55)); litIn.push(P(rel, rOut - th * .85));
    const e = clamp((u - .35) / .25);
    edge.push(P(rel, rOut + .01)); edgeIn.push(P(rel, rOut - .05 * e));
  }
  band(`${key} body`, inner, outer, Flesh.withAlpha(.75 * fade), Y + .02);
  band(`${key} dark`, litIn, lit, Fibre.withAlpha(.45 * fade), Y + .0202);
  strip(`${key} edge`, edgeIn, edge, White.withAlpha(.9 * fade), whiteGlow, Y + .0204);
  [.3, .5, .68, .86].forEach((u, i) => {
    const rel = lerp(tail, head, u), th = Thick * Math.pow(u, .8), c = P(rel, rOut - th * .5), r = (.028 + .018 * rand(i + 40)) * Math.min(1, th / .25) * fade;
    if (r < .01) return;
    draw(disc, c.x, Y + .0206, c.z, r + .01, r * .8 + .01, 0, Ink.withAlpha(fade));
    draw(disc, c.x, Y + .0207, c.z, r, r * .8, 0, Sclera.withAlpha(fade));
    draw(disc, c.x + (rand(i + 50) - .5) * r * .6, Y + .0208, c.z, r * .35, r * .35, 0, Pupil.withAlpha(fade));
  });
}

// A normal hit on a pawn: a white slash across the chest, a red cut that stays .6 s, six blood drops
// thrown along the swing that land and stay. d: the aim; t: the swing's direction at contact.
function cut(key, chest, foot, d, t, age, seed) {
  if (age < 0) return;
  if (age < .12) streak(`${key} slash`, add(add(chest, t, -.3), d, .05), add(add(chest, t, .3), d, -.05), .09 * (1 - age / .12) + .02, White, whiteGlow, Y + .09);
  if (age < .6) streak(`${key} cut`, add(chest, t, -.2), add(chest, t, .2), .04, Streak.withAlpha(1 - age / .6), flatMat, pawnLayer + .05);
  for (let i = 0; i < 6; i++) {
    const r = rand(seed * 13 + i), v = add(add({ x: 0, z: 0 }, d, .5 + .6 * r), t, (rand(seed * 17 + i) - .3) * .9), up = 1 + rand(seed * 19 + i), gr = 7;
    const land = (up + Math.sqrt(up * up + 2 * gr * ChestH)) / gr, tt = Math.min(age, land);
    const g = add(foot, v, tt), h = Math.max(0, ChestH + up * tt - .5 * gr * tt * tt), sz = .035 + .03 * r;
    if (age < land) draw(disc, g.x, Y + .08, g.z + Ground + h * Lift, sz, sz, 0, Blood);
    else draw(disc, g.x, Floor + .02 + i * .0002, g.z + Ground, sz * 1.6, sz * 1.1, 0, Blood.withAlpha(.85));
  }
}

// The heal after a hit (age from the hit): the blood on the steel, the glow sliding into the eye.
function feedLook(age) {
  if (age < 0 || age > .8) return { blood: 0, pulse: 0, pulseU: 0, eyePulse: 0 };
  return {
    blood: 1 - smooth(age / .35),
    pulse: bump(clamp((age - .05) / .3)), pulseU: lerp(.78, .22, smooth((age - .05) / .3)),
    eyePulse: bump(clamp((age - .3) / .3)),
  };
}

// ---- The grown swing ------------------------------------------------------------------------------
function grownT(p) {
  const t1 = p.swell, t2 = t1 + p.raise, t3 = t2 + p.slam, t4 = t3 + SlamHold, t5 = t4 + p.shrink;
  return { t1, t2, t3, t4, t5, len: t5 + .15 };
}
// Pose at age a: the hand, the 3D blade direction, the size, the flip, swell and open.
function grownPose(a, G, pos, aim, p) {
  const d = dirOf(aim), sign = signOf(aim), hs = { x: side(d).x * -sign, z: side(d).z * -sign };
  const lean = .2 + .8 * Math.abs(Math.sin(aim * D2R));
  const R = level(aim + sign * Rest), T = n3({ x: hs.x * .45 * lean - d.x * .55, z: hs.z * .45 * lean - d.z * .55, h: .8 }), B = n3({ x: d.x, z: d.z, h: -.18 });
  const hRest = handAt(pos, aim + sign * Rest, sign), hTop = { ...add(add(pos, d, .05), hs, .1), h: 1.5 }, hLow = { ...add(add(pos, d, .35), hs, .05), h: .45 };
  const h3 = (A, Bq, t) => ({ x: lerp(A.x, Bq.x, t), z: lerp(A.z, Bq.z, t), h: lerp(A.h, Bq.h, t) });
  // The edge leads the cut: which side of the raised blade faces the slam's motion on screen.
  const Ts = unit2({ x: T.x, z: T.z + T.h * Lift }), Bs = { x: B.x, z: B.z + B.h * Lift };
  const slamFlip = (side(Ts).x * (Bs.x - Ts.x) + side(Ts).z * (Bs.z - Ts.z)) > 0 ? -1 : 1;
  let V, hand, g;
  if (a < G.t1) { V = R; hand = hRest; }
  else if (a < G.t2) { const e = smooth((a - G.t1) / p.raise); V = lerp3(R, T, e); hand = h3(hRest, hTop, e); }
  else if (a < G.t3) { const e = Math.pow(clamp((a - G.t2) / p.slam), 2); V = lerp3(T, B, e); hand = h3(hTop, hLow, e); }
  else if (a < G.t4) { V = B; hand = hLow; }
  else { const e = smooth((a - G.t4) / p.shrink); V = lerp3(B, R, e); hand = h3(hLow, hRest, e); }
  const swell = a < G.t1 ? smooth(a / G.t1) : a < G.t4 ? 1 : 1 - smooth((a - G.t4) / p.shrink);
  g = 1 + (p.grownScale - 1) * swell;
  const turn = smooth((a - G.t1) / .1) * (1 - smooth((a - G.t4) / .15));
  return { V, hand, g, flip: lerp(sign, slamFlip, turn), swell, B, hLow };
}

// The impact (age from the slam's end). P0, P1: the blade's footprint on the floor (ground points).
function slamImpact(key, P0, P1, d, age, seed, sun, strength) {
  if (age < 0) return;
  const f0 = { x: P0.x, z: P0.z + Ground }, f1 = { x: P1.x, z: P1.z + Ground }, len = len2({ x: f1.x - f0.x, z: f1.z - f0.z }), n = side(d);
  // The crack: a jagged dark line along the footprint with branches; it opens in .06 s and stays.
  const open = clamp(age / .06), pts = [];
  for (let i = 0; i <= 12; i++) { const u = i / 12 * open; pts.push(add(mix(f0, f1, u), n, (rand(seed + i * 3) - .5) * .09 * Math.sin(u * Math.PI + .2))); }
  line(`${key} crack`, pts, .07, Ink.withAlpha(.75), flatMat, Floor + .03, 'both');
  for (let i = 0; i < 6; i++) {
    const u = .15 + .7 * rand(seed + 60 + i), b0 = mix(f0, f1, u * open), sgn = i % 2 ? 1 : -1, bl = (.15 + .2 * rand(seed + 70 + i)) * open;
    line(`${key} branch ${i}`, [b0, add(add(b0, n, sgn * bl), d, bl * .5)], .035, Ink.withAlpha(.6), flatMat, Floor + .031, 'end');
  }
  sprite(mix(f0, f1, .5), len + .6, .5, Ink.withAlpha(.25 * open), soft, Floor + .025, -degOf(d));
  // The split light (Ruina card art): a yellow-white core in an orange glow along the footprint.
  if (age < .45) {
    const u = age / .45, k = Math.pow(1 - u, 1.5), a = add(f0, d, -.2), b = add(f1, d, .3);
    line(`${key} split glow`, [a, mix(a, b, .5), b], .55 * (1 + .4 * u), SplitGlow.withAlpha(.75 * k), whiteGlow, Y + .01, 'both');
    line(`${key} split core`, [a, mix(a, b, .5), b], .07, SplitCore.withAlpha(k), whiteGlow, Y + .011, 'both');
    if (age < .12) sprite(mix(f0, f1, .3), 2.2, 1.6, SplitCore.withAlpha(.6 * (1 - age / .12)), glow, Y + .012);
  }
  // Red streaks along the cut (the card art's diagonal strokes), sliding forward.
  if (age < .3) {
    const k = 1 - age / .3;
    for (let i = 0; i < 12; i++) {
      const off = (i % 2 ? 1 : -1) * (.15 + .85 * rand(seed + 100 + i)), sl = .5 + .7 * rand(seed + 110 + i), st = rand(seed + 120 + i) * len;
      const a = add(add(f0, d, st + .6 * age * 4), n, off), b = add(a, d, sl);
      streak(`${key} streak ${i}`, a, b, .05 + .04 * rand(seed + 130 + i), (i % 3 ? Streak : StreakDark).withAlpha(.9 * k), flatMat, Y + .013);
    }
  }
  // Flesh with eyes bursting out of the blade (Ruina): out in .08 s, gone by .55 s.
  const fl = age < .08 ? easeOut(age / .08) : 1 - smooth((age - .25) / .3);
  if (fl > 0) for (let i = 0; i < 7; i++) {
    const u = .12 + i * .13, c = add(add(mix(f0, f1, u), n, (rand(seed + 140 + i) - .5) * .4), { x: 0, z: 1 }, .15 + .25 * rand(seed + 150 + i));
    const r = (.13 + .12 * rand(seed + 160 + i)) * fl, er = r * .4;
    draw(disc, c.x, Y + .03 + i * .001, c.z, r + .02, r * .85 + .02, 0, Ink);
    draw(disc, c.x, Y + .0302 + i * .001, c.z, r, r * .85, 0, Flesh);
    draw(disc, c.x - r * .2, Y + .0304 + i * .001, c.z + r * .25, r * .5, r * .3, 0, FleshLit.withAlpha(.6));
    draw(disc, c.x, Y + .0306 + i * .001, c.z, er, er * .85, 0, Sclera);
    draw(disc, c.x + (rand(seed + 170 + i) - .5) * er * .7, Y + .0308 + i * .001, c.z, er * .45, er * .45, 0, i % 3 ? IrisGreen : IrisBlue);
    draw(disc, c.x + (rand(seed + 170 + i) - .5) * er * .7, Y + .0309 + i * .001, c.z, er * .2, er * .2, 0, Pupil);
  }
  // Dust along the footprint and rocks thrown out that land and stay.
  if (age < .8) for (let i = 0; i < 10; i++) {
    const u = age / .8, c = add(mix(f0, f1, i / 9), n, (rand(seed + 200 + i) - .5) * .6 * (1 + u)), sz = .3 + .6 * u;
    sprite({ x: c.x, z: c.z + .25 * u }, sz, sz * .8, Dust.withAlpha(.5 * Math.sin(u * Math.PI)), puff, Y + .005 + i * .0003);
  }
  for (let i = 0; i < 10; i++) {
    const r = rand(seed + 220 + i), sgn = i % 2 ? 1 : -1, v = add(add({ x: 0, z: 0 }, n, sgn * (.8 + 1.2 * r)), d, (rand(seed + 230 + i) - .3) * .8);
    const up = 1.6 + 1.4 * rand(seed + 240 + i), gr = 9, land = 2 * up / gr, tt = Math.min(age, land), base = mix(P0, P1, rand(seed + 250 + i));
    const g = add(base, v, tt), h = Math.max(0, up * tt - .5 * gr * tt * tt), sz = .07 + .07 * rand(seed + 260 + i);
    if (age < land) sprite(shd({ ...g, h }, sun), sz, sz * .7, Ink.withAlpha(strength * .6), soft, shadowLayer);
    rock({ x: g.x, z: g.z + Ground + h * Lift }, sz, i * 47 + tt * 600, 1, i, age < land ? Y + .04 : Floor + .035);
  }
}

// ---- The arm --------------------------------------------------------------------------------------
// The flesh taking the sword arm, drawn over the stand-in. stage 0..4 (fractional while it grows):
// 1 hand (drawn by the sword as fuse), 2 forearm with an eye, 3 the whole arm swollen with a shoulder
// bulge, a toothed mouth, two bone blades and tendrils, 4 neck and face with one eye and teeth.
function arm(key, pos, H, d, hs, stage, s, sun, strength, who) {
  const f2 = clamp(stage - 1), f3 = clamp(stage - 2), f4 = clamp(stage - 3);
  if (f2 <= 0) return;
  const north = d.z > .5, L = north ? pawnLayer - .02 : pawnLayer + .03;
  // The shoulder: the across offset goes on the screen's x only, so facing east or west it sits on the
  // upper chest instead of sliding down to mid-body (height and north share the screen's vertical).
  const S = { x: pos.x + hs.x * .22, z: pos.z + .15 + hs.z * .06 }, E = { x: (S.x + H.x) / 2 + hs.x * .08, z: (S.z + H.z) / 2 + hs.z * .04 - .05 };
  const bez = t => ({ x: (1 - t) * (1 - t) * H.x + 2 * (1 - t) * t * E.x + t * t * S.x, z: (1 - t) * (1 - t) * H.z + 2 * (1 - t) * t * E.z + t * t * S.z });
  const reach = .5 * f2 + .5 * f3, N = 12, pts = Array.from({ length: N + 1 }, (_, i) => bez(i / N * reach));
  const width = u => { const t = u * reach; return (.065 + .025 * (1 - t)) * (.4 + .6 * clamp(f2 * 1.5)) + .2 * f3 * Math.sin(Math.PI * clamp(t * 1.05)) + .015 * Math.sin(t * 18 + s * 5) * f3; };
  // Shoulder bulge first, so the arm runs into it.
  if (f3 > 0) {
    const c = { x: S.x + hs.x * .06, z: S.z + .02 };
    draw(disc, c.x, L, c.z, .26 * f3 + .016, .22 * f3 + .016, 0, Ink);
    draw(disc, c.x, L + .0002, c.z, .26 * f3, .22 * f3, 0, Meat);
    draw(disc, c.x - .05, L + .0004, c.z + .06, .13 * f3, .08 * f3, 0, MeatLit.withAlpha(.6));
  }
  tube(`${key} line`, pts, u => width(u) + .014, Ink, L + .001);
  tube(`${key} meat`, pts, width, Meat, L + .0012);
  tube(`${key} lit`, pts, width, MeatLit.withAlpha(.6), L + .0014, .25, .7);
  for (let i = 0; i < 2; i++) line(`${key} fibre ${i}`, pts.map((q, j) => add(q, side(unit2({ x: S.x - H.x, z: S.z - H.z })), (i ? .4 : -.35) * width(j / N))), .012, MeatDark.withAlpha(.8), flatMat, L + .0016, 'both');
  // The forearm eye.
  if (f2 > .2) {
    const c = bez(Math.min(.22, reach * .7)), o = smooth((f2 - .2) / .5), r = .07 * (1 + .3 * f3);
    draw(disc, c.x, L + .002, c.z, r + .012, r * .8 * o + .012, 0, Ink);
    draw(disc, c.x, L + .0022, c.z, r, r * .8 * o, 0, Sclera);
    draw(disc, c.x, L + .0024, c.z, r * .42, r * .42 * o, 0, IrisGreen);
    draw(disc, c.x, L + .0026, c.z, r * .18, r * .18 * o, 0, Pupil);
  }
  if (f3 > 0) {
    // The mouth on the outer side of the upper arm: a dark lens and two rows of white teeth.
    const t0 = .5 * reach, c = { x: bez(t0).x + hs.x * .1, z: bez(t0).z + hs.z * .05 }, tan = unit2({ x: bez(t0 + .05).x - bez(t0 - .05).x, z: bez(t0 + .05).z - bez(t0 - .05).z }), nn = side(tan);
    const mw = .17 * f3, mh = .07 * f3 * (.7 + .3 * Math.sin(s * 3));
    draw(disc, c.x, L + .003, c.z, mw + .012, mh + .012, -degOf(tan), Ink);
    draw(disc, c.x, L + .0032, c.z, mw, mh, -degOf(tan), Mouth);
    for (let i = 0; i < 7; i++) for (const sg of [1, -1]) {
      const u = (i + .5) / 7 * 2 - 1, e = Math.sqrt(Math.max(0, 1 - u * u)), base = add(add(c, tan, u * mw), nn, sg * mh * e), tip = add(base, nn, -sg * mh * .7 * e);
      band(`${key} tooth ${i} ${sg}`, [add(base, tan, -.018), add(base, tan, .018)], [tip, tip], Teeth, L + .0034);
    }
    // Tendrils hanging from the arm.
    for (let i = 0; i < 5; i++) {
      const q = bez((.25 + i * .14) * reach), l = (.2 + .15 * rand(i + 300)) * f3, sw = .04 * Math.sin(s * 2.6 + i * 1.7);
      line(`${key} tendril ${i}`, [q, { x: q.x + sw * .5, z: q.z - l * .5 }, { x: q.x + sw, z: q.z - l }], .032, MeatDark, flatMat, L - .0005, 'end');
    }
    // Two bone blades rising from the arm and curving out (Limbus's scythe), with their shadows.
    [[.58, .35, .15, 1.4], [.82, .45, .3, 1.0]].forEach(([t, out, fwd, rise], i) => {
      const q = bez(t * reach), baseG = { x: q.x, z: q.z - Ground - .85 * Lift, h: .85 };
      const p3 = u => ({ x: baseG.x + hs.x * out * Math.sin(u * 1.4) * f3 + d.x * fwd * u * u * f3, z: baseG.z + hs.z * out * Math.sin(u * 1.4) * f3 + d.z * fwd * u * u * f3, h: baseG.h + rise * u * f3 });
      const sp = Array.from({ length: 9 }, (_, j) => scr(p3(j / 8))), sh = Array.from({ length: 9 }, (_, j) => shd(p3(j / 8), sun));
      line(`${key} bone shadow ${i}`, sh, .1, Ink.withAlpha(strength * .5), flatMat, shadowLayer, 'end');
      tube(`${key} bone line ${i}`, sp, u => (.07 * (1 - u) + .006) + .014, Ink, L + .004 + i * .0004);
      tube(`${key} bone ${i}`, sp, u => .07 * (1 - u) + .006, Bone, L + .0042 + i * .0004);
      tube(`${key} bone lit ${i}`, sp, u => .07 * (1 - u) + .006, BoneLit.withAlpha(.8), L + .0044 + i * .0004, .1, .7);
    });
  }
  if (f4 > 0) {
    // Neck and face: flesh up the neck and over the head, one round eye, a lipless grin.
    const neck = at(pos, 'neck', who), head = at(pos, 'head', who), FL = pawnLayer + .045;
    tube(`${key} neck line`, [S, mix(S, neck, .6), neck, head].map(q => q), u => .07 * f4 + .014, Ink, FL);
    tube(`${key} neck`, [S, mix(S, neck, .6), neck, head], u => .07 * f4, Meat, FL + .0002);
    const c = add(head, hs, .05 * (1 - f4)), rx = .2 * f4, rz = .2 * f4;
    draw(disc, c.x, FL + .0004, c.z, rx + .014, rz + .014, 0, Ink);
    draw(disc, c.x, FL + .0006, c.z, rx, rz, 0, Meat);
    draw(disc, c.x - .05, FL + .0008, c.z + .07, rx * .45, rz * .3, 0, MeatLit.withAlpha(.6));
    for (let i = 0; i < 3; i++) line(`${key} face fibre ${i}`, [add(c, { x: -.15, z: -.05 + i * .06 }, f4), add(c, { x: 0, z: .02 + i * .05 }, f4), add(c, { x: .15, z: -.04 + i * .06 }, f4)], .012, MeatDark.withAlpha(.7), flatMat, FL + .0009, 'both');
    if (f4 > .4) {
      const o = smooth((f4 - .4) / .4), e = add(add(c, hs, .05), { x: 0, z: 1 }, .03), r = .065;
      draw(disc, e.x, FL + .001, e.z, r + .012, r * o + .012, 0, Ink);
      draw(disc, e.x, FL + .0012, e.z, r, r * o, 0, Sclera);
      draw(disc, e.x, FL + .0014, e.z, r * .3, r * .3 * o, 0, Pupil);
      if (!north) {
        const m = { x: c.x, z: c.z - .09 }, mw = .12 * o, mh = .04 * o;
        draw(disc, m.x, FL + .0016, m.z, mw + .01, mh + .01, 0, Ink);
        draw(disc, m.x, FL + .0017, m.z, mw, mh, 0, Mouth);
        for (let i = 0; i < 7; i++) for (const sg of [1, -1]) {
          const u = (i + .5) / 7 * 2 - 1, ee = Math.sqrt(Math.max(0, 1 - u * u)), bx = m.x + u * mw, bz = m.z + sg * mh * ee;
          band(`${key} face tooth ${i} ${sg}`, [{ x: bx - .01, z: bz }, { x: bx + .01, z: bz }], [{ x: bx, z: bz - sg * mh * .75 * ee }, { x: bx, z: bz - sg * mh * .75 * ee }], Teeth, FL + .0018);
        }
      }
    }
  }
}

// Stage pips under the wielder (above the head when aiming south): a lab aid, in game a gizmo.
function pips(pos, stage, aim, alpha) {
  if (alpha <= 0) return;
  const above = Math.sin(aim * D2R) < -.5;
  for (let i = 0; i < Stages; i++) {
    const c = { x: pos.x - .24 + i * .16, z: pos.z + (above ? .85 : -.7) }, fill = clamp(stage - i);
    draw(disc, c.x, Floor + .05, c.z, .065, .065, 0, Ink.withAlpha(.75 * alpha));
    draw(disc, c.x, Floor + .051, c.z, .05, .05, 0, Color.Lerp(MeatDark, Meat, fill).withAlpha(alpha * (.35 + .65 * fill)));
  }
}

// ---- Timelines ------------------------------------------------------------------------------------
const isSwings = p => p.mode === 'swings', isGrown = p => p.mode === 'grown swing';
const isCorroded = p => p.mode === 'corroded', isOverclock = p => p.mode.startsWith('overclock');

// Who stands where, who swings at whom and when. People: { off (from the origin), colour, hp (hits
// to go down), hostile, shooter }. Actions: { t, start (the swing), kind, target, aim, from, to (the
// wielder before and after a lunge), hit }.
function plan(p) {
  const d = dirOf(p.aim), acr = side(d), F = (al, ac) => ({ x: d.x * al + acr.x * ac, z: d.z * al + acr.z * ac });
  const T = swingT(p), G = grownT(p), out = { people: [], actions: [], stages: [], shots: [], enter: null, exit: null, end: 0, T, G };
  const person = (off, colour, hp, more = {}) => out.people.push({ off, colour, hp, hits: [], downAt: null, ...more });
  const hitOn = (q, t, dir) => { q.hits.push({ t, d: dir }); if (q.hits.length >= q.hp && q.downAt == null) q.downAt = t; };
  if (isSwings(p) || isGrown(p)) {
    person(F(1, 0), Enemy, isSwings(p) ? 99 : 2, { hostile: true });
    const n = isSwings(p) ? 3 : 2;
    for (let i = 0; i < n; i++) {
      const grown = isGrown(p) && i === 1, t = Lead + i * p.spacing, hit = t + (grown ? G.t3 : T.hitAt);
      out.actions.push({ t, start: t, kind: grown ? 'grown' : 'swing', target: 0, aim: p.aim, from: { x: 0, z: 0 }, to: { x: 0, z: 0 }, hit });
      hitOn(out.people[0], hit, d);
    }
    const last = out.actions[n - 1];
    out.end = last.start + (last.kind === 'grown' ? G.len : T.len) + p.hold;
    return out;
  }
  const corroded = isCorroded(p);
  if (corroded) {
    person(F(0, 1), Ally, 2); person(F(3, .3), Enemy, 2, { hostile: true }); person(F(-3, -1.6), Ally, 99, { shooter: true });
  } else {
    person(F(0, 1), Ally, 2, { skipped: true }); person(F(1, -.25), Enemy, 3, { hostile: true }); person(F(3.1, -.6), Enemy, 2, { hostile: true });
  }
  out.enter = 0;
  const every = corroded ? p.corrodedInterval : p.overclockInterval, n = corroded ? 4 : 5;
  let w = { x: 0, z: 0 }, stage = 1;
  out.stages.push({ t: 0, to: 1, dur: Enter });
  for (let k = 0; k < n; k++) {
    const t = Enter + k * every;
    const standing = out.people.map((q, i) => ({ q, i })).filter(({ q }) => (q.downAt == null || q.downAt > t) && (corroded || q.hostile));
    standing.sort((a, b) => len2({ x: a.q.off.x - w.x, z: a.q.off.z - w.z }) - len2({ x: b.q.off.x - w.x, z: b.q.off.z - w.z }));
    const { q, i } = standing[0], v = { x: q.off.x - w.x, z: q.off.z - w.z }, dist = len2(v), u = unit2(v);
    const from = w, lunge = dist > Adjacent ? Math.min(LungeCells, dist - 1) : 0, to = add(w, u, lunge);
    const start = t + (lunge > 0 ? LungeTime : 0), hit = start + T.hitAt;
    out.actions.push({ t, start, kind: 'swing', target: i, aim: degOf(u), from, to, hit, lunge: lunge > 0 });
    hitOn(q, hit, u);
    stage = Math.min(Stages, stage + 1); out.stages.push({ t: hit, to: stage, dur: StageGrow });
    w = to;
    if (corroded && k === 2) {
      const shooter = out.people.find(q2 => q2.shooter), st = hit + .5 * every;
      out.shots.push({ t: st, hitT: st + .05, from: shooter.off, at: w });
      stage = Math.max(0, stage - 1); out.stages.push({ t: st + .05, to: stage, dur: StageShrink });
    }
  }
  const last = out.actions[n - 1];
  out.exit = last.hit + .6 * every;
  out.stages.push({ t: out.exit, to: 0, dur: Exit });
  out.end = out.exit + Exit + p.hold;
  return out;
}
function stageAt(PL, s) {
  let from = 0, cur = 0;
  for (const e of PL.stages) {
    if (s < e.t) break;
    cur = lerp(from, e.to, smooth((s - e.t) / e.dur));
    from = e.to;
  }
  return cur;
}
// The wielder's ground offset from the origin at s (lunges are .2 s, eased).
function wielderAt(PL, s) {
  let w = { x: 0, z: 0 };
  for (const a of PL.actions) if (a.lunge && s >= a.t) w = mix(a.from, a.to, smooth((s - a.t) / LungeTime));
  return w;
}

// ---- Drawing --------------------------------------------------------------------------------------
function drawFrame(s, p, o, sun, strength) {
  const PL = plan(p), T = PL.T, G = PL.G, who = { body: 'average', sun, shadow: strength };
  const armed = isCorroded(p) || isOverclock(p), stage = armed ? stageAt(PL, s) : 0;
  const fuse = clamp(stage);

  // The people: flinch at each hit, fall when downed, blood at each hit.
  PL.people.forEach((q, i) => {
    const base = add(o, q.off), fall = q.downAt == null ? 0 : smooth((s - q.downAt) / FallTime);
    let off = { x: 0, z: 0 };
    for (const h of q.hits) { const a = s - h.t; if (a >= 0 && a < .25) off = add(off, h.d, .07 * bump(a / .25)); }
    const pos = add(base, off), turn = (q.hits.length ? (q.hits[q.hits.length - 1].d.x >= 0 ? 90 : -90) : -90) * fall;
    if (q.downAt != null && s >= q.downAt) sprite({ x: base.x + .05, z: base.z - .05 }, 1.0 * smooth((s - q.downAt) / 1.2), .6 * smooth((s - q.downAt) / 1.2), Blood.withAlpha(.75), soft, Floor + .015);
    if (q.skipped) circle(base, .42, .5, Floor + .04, AllyRing);
    pawn(pos, { ...who, shirt: q.colour, downed: fall > 0, turn });
    const chest = at(pos, 'chest', who);
    q.hits.forEach((h, j) => {
      const grown = PL.actions.some(a => a.kind === 'grown' && Math.abs(a.hit - h.t) < 1e-6);
      if (!grown) cut(`mim cut ${i} ${j}`, chest, base, h.d, side(h.d), s - h.t, i * 7 + j);
    });
    if (q.shooter) {
      const w = add(o, wielderAt(PL, s)), dd = unit2({ x: w.x - base.x, z: w.z - base.z });
      rifle(`mim rifle ${i}`, { x: base.x + dd.x * .25, z: base.z + dd.z * .25 + Ground, h: HandH }, degOf(dd), sun, strength, dd.z > .35 ? pawnLayer - .01 : Y + .06);
    }
  });

  // The wielder: where, facing which way, doing what.
  const wOff = wielderAt(PL, s), pos = add(o, wOff);
  const done = PL.actions.filter(a => a.t <= s), act = done[done.length - 1];
  const aim = act ? act.aim : p.aim, sign = signOf(aim), d = dirOf(aim), hs = { x: side(d).x * -sign, z: side(d).z * -sign };
  if (armed) sprite(pos, 1.5, 1.1, Streak.withAlpha(.16 * clamp(stage)), glow, Floor + .01);
  // Lunge: dust where it leaves and lands, two afterimages.
  for (const a of PL.actions) if (a.lunge) {
    const age = s - a.t;
    if (age >= 0 && age < LungeTime + .12) for (const [lag, al] of [[.06, .3], [.12, .15]]) {
      const ga = add(o, mix(a.from, a.to, smooth((age - lag) / LungeTime)));
      if (age - lag > 0 && age - lag < LungeTime) pawn(ga, { ...who, shirt: Holder, alpha: al, shadow: 0 });
    }
    for (const [at0, u0] of [[a.from, age], [a.to, age - LungeTime]]) if (u0 >= 0 && u0 < .5) for (let i = 0; i < 5; i++) {
      const u = u0 / .5, c = add(o, at0), ang = i / 5 * TAU + rand(i + 330), r = .2 + .35 * u;
      sprite({ x: c.x + Math.cos(ang) * r, z: c.z + Ground + .1 + Math.sin(ang) * r * .4 + .1 * u }, .25 + .3 * u, .2 + .25 * u, Dust.withAlpha(.45 * Math.sin(u * Math.PI)), puff, Y + .008);
    }
  }
  pawn(pos, { ...who, shirt: Holder });
  if (armed) pips(pos, stage, aim, 1 - smooth((s - PL.exit - Exit) / .3));

  // Targeting lines (lab aid): a dashed red line from the wielder to each chosen target.
  if (armed) for (const a of PL.actions) {
    const age = s - a.t; if (age < 0 || age > .45) continue;
    const from = at(add(o, a.from), 'chest', who), to = at(add(o, PL.people[a.target].off), 'chest', who), k = bump(age / .45);
    for (let j = 0; j < 8; j++) line(`mim aim ${a.t} ${j}`, [mix(from, to, j / 8 + .02), mix(from, to, j / 8 + .08)], .03, Streak.withAlpha(.7 * k), flatMat, Y + .1, 'none');
  }

  // The sword: the current action's pose, else rest.
  const restLook = { blade: p.blade, g: 1, flip: sign, swell: 0, open: armed ? smooth(s / .6) * (1 - smooth((s - PL.exit) / Exit)) : 0, fuse, blood: 0, pulse: 0, pulseU: 0, eyePulse: 0 };
  let hand3 = handAt(pos, aim + sign * Rest, sign), V = level(aim + sign * Rest), look = restLook;
  const lastHit = PL.actions.filter(a => a.hit <= s).pop();
  const feed = lastHit ? feedLook(s - lastHit.hit) : feedLook(-1);
  if (act && s >= act.start) {
    const age = s - act.start;
    if (act.kind === 'swing' && age < T.len) {
      const deg = aim + sign * swingRel(age, T);
      hand3 = handAt(pos, deg, sign); V = level(deg);
      look = { ...restLook, flip: swingFlip(age, T, sign) };
      crescent(`mim crescent ${act.t}`, { x: pos.x, z: pos.z + HandScreen }, aim, sign, age, T, HandOut + GuardA + p.blade + .05);
    } else if (act.kind === 'grown' && age < G.len) {
      const g = grownPose(age, G, pos, aim, p);
      hand3 = g.hand; V = g.V;
      look = { ...restLook, g: g.g, flip: g.flip, swell: g.swell, open: g.swell };
      const BL = GuardA + p.blade * p.grownScale, P0 = add(g.hLow, g.B, GuardA + .1), P1 = add(g.hLow, g.B, BL);
      slamImpact(`mim slam ${act.t}`, P0, P1, d, age - G.t3, 900, sun, strength);
    }
  }
  // The grown hit's marks stay after the swing is over.
  for (const a of PL.actions) if (a.kind === 'grown' && s - a.start >= G.len) {
    const g = grownPose(G.t3, G, add(o, a.from), a.aim, p), BL = GuardA + p.blade * p.grownScale;
    slamImpact(`mim slam ${a.t}`, add(g.hLow, g.B, GuardA + .1), add(g.hLow, g.B, BL), dirOf(a.aim), s - a.start - G.t3, 900, sun, strength);
  }
  look = { ...look, blood: feed.blood, pulse: feed.pulse, pulseU: feed.pulseU, eyePulse: feed.eyePulse };
  const pointsNorth = (V.z + V.h * Lift) > .35 * Math.hypot(V.x, V.z + V.h * Lift);
  look.layer = pointsNorth ? pawnLayer - .03 : Y + .05;
  const sw = sword('mim sword', hand3, V, look, s, sun, strength);
  if (feed.eyePulse > 0) sprite(at(pos, 'chest', who), .7, .8, HealRed.withAlpha(.3 * feed.eyePulse), glow, Y + .07);
  if (armed) arm('mim arm', pos, sw.H, d, hs, stage, s, sun, strength, who);

  // The shot that takes a stage back: muzzle flash, tracer, a chunk of flesh torn off that lands
  // and stays, blood.
  for (const sh of PL.shots) {
    const age = s - sh.t; if (age < 0) continue;
    const sp = add(o, sh.from), wp = add(o, sh.at), dd = unit2({ x: wp.x - sp.x, z: wp.z - sp.z });
    const muzzle = add({ x: sp.x, z: sp.z + HandScreen }, dd, .62), hitP = { x: wp.x + hs.x * .2, z: wp.z + hs.z * .2 + .12 };
    if (age < .06) {
      sprite(muzzle, .35, .3, Flash.withAlpha(1 - age / .06), glow, Y + .09);
      streak(`mim tracer`, muzzle, mix(muzzle, hitP, clamp(age / .05)), .05, Flash.withAlpha(.95), whiteGlow, Y + .09);
    }
    const ha = s - sh.hitT; if (ha < 0) continue;
    if (ha < .1) sprite(hitP, .45, .4, White.withAlpha(.8 * (1 - ha / .1)), glow, Y + .1);
    const up = 1.4, gr = 7, h0 = .9, land = (up + Math.sqrt(up * up + 2 * gr * h0)) / gr, tt = Math.min(ha, land);
    const g0 = { x: wp.x + hs.x * .25, z: wp.z + hs.z * .25 }, g = add(add(g0, dd, 1.1 * tt), side(dd), .3 * tt), h = Math.max(0, h0 + up * tt - .5 * gr * tt * tt);
    if (ha < land) sprite(shd({ ...g, h }, sun), .14, .09, Ink.withAlpha(strength * .6), soft, shadowLayer);
    const c = { x: g.x, z: g.z + Ground + h * Lift }, Lc = ha < land ? Y + .06 : Floor + .04;
    if (ha >= land) draw(disc, c.x, Floor + .035, c.z, .26, .16, 0, Blood.withAlpha(.8));
    draw(disc, c.x, Lc, c.z, .17, .13, ha * 400, Ink);
    draw(disc, c.x, Lc + .0005, c.z, .15, .11, ha * 400, Meat);
    draw(disc, c.x - .03, Lc + .001, c.z + .025, .07, .04, ha * 400, MeatLit.withAlpha(.7));
  }
}

export default {
  kit: 'E.G.O. weapons', label: 'Mimicry (sketch)',
  params: {
    mode: { label: 'Show', value: 'swings', options: ['swings', 'grown swing', 'corroded', 'overclock (hostiles only)'], group: 'Showcase' },
    aim: P('Aim (degrees)', 0, 0, 360, 5, 'Showcase'),
    spacing: P('Swings: time between (s)', .9, .5, 2, .05, 'Rule'),
    corrodedInterval: P('Corroded: a swing every (s)', 1.5, .8, 3, .1, 'Rule'),
    overclockInterval: P('Overclock: a swing every (s)', 1.0, .5, 2, .1, 'Rule'),
    grownScale: P('Grown blade (x size)', 2.0, 1.5, 3, .1, 'Shape'),
    blade: P('Blade length (cells)', 1.09, .8, 1.4, .01, 'Shape'),
    windup: P('Wind-up', .18, .08, .4, .01, 'Timing (s)'),
    swing: P('Swing', .12, .06, .3, .01, 'Timing (s)'),
    swell: P('Grown: swell', .3, .1, .6, .02, 'Timing (s)'),
    raise: P('Grown: raise', .3, .1, .6, .02, 'Timing (s)'),
    slam: P('Grown: slam', .1, .05, .3, .01, 'Timing (s)'),
    shrink: P('Grown: shrink back', .45, .2, 1, .05, 'Timing (s)'),
    hold: P('Show the result', 1.2, .3, 3, .1, 'Timing (s)'),
  },
  duration(p) { return plan(p).end; },
  phases(p) {
    const PL = plan(p), out = [];
    if (isSwings(p)) PL.actions.forEach((a, i) => out.push({ name: `Swing ${i + 1}`, t: a.t }));
    else if (isGrown(p)) {
      const g = PL.actions[1].start;
      out.push({ name: 'Swing', t: PL.actions[0].t }, { name: 'Swell', t: g }, { name: 'Raise', t: g + PL.G.t1 }, { name: 'Slam', t: g + PL.G.t3 }, { name: 'Shrink', t: g + PL.G.t4 });
    } else {
      out.push({ name: isCorroded(p) ? 'Corroded' : 'Overclock', t: 0 });
      PL.actions.forEach((a, i) => out.push({ name: `${a.lunge ? 'Lunge, swing' : 'Swing'} ${i + 1}`, t: a.t }));
      PL.shots.forEach(sh => out.push({ name: 'Shot taken', t: sh.t }));
      out.push({ name: 'Ends', t: PL.exit });
      out.sort((a, b) => a.t - b.t);
    }
    out.push({ name: 'Result', t: PL.end - p.hold });
    return out;
  },
  events(p) {
    const PL = plan(p), out = [];
    if (isCorroded(p)) out.push({ t: 0, type: 'sound', def: 'RimArt_MimicryCorrode' });
    for (const a of PL.actions) {
      if (a.kind === 'grown') out.push({ t: a.start, type: 'sound', def: 'RimArt_MimicryGrow' }, { t: a.hit, type: 'sound', def: 'RimArt_MimicrySlam' }, { t: a.hit, type: 'shake', value: .07 });
      else out.push({ t: a.start + PL.T.windup, type: 'sound', def: 'RimArt_MimicrySwing' }, { t: a.hit, type: 'shake', value: .012 });
      if (a.lunge) out.push({ t: a.t, type: 'shake', value: .01 });
    }
    for (const sh of PL.shots) out.push({ t: sh.hitT, type: 'shake', value: .015 });
    return out;
  },

  draw(s, p, { origin: o, scene }) {
    if (s < 0 || s >= plan(p).end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    drawFrame(s, p, o, sun, strength);
  },
};
