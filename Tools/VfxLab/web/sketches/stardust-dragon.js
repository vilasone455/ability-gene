// Stardust Dragon Staff — weapon proposal. Sketch only: no C#.
// From Terraria's Stardust Dragon Staff (Lunatic Cultist tier, made from Stardust Fragments): the staff summons a
// dragon that flies through blocks and hurts whatever its body touches; summoning again makes the same dragon
// longer. The look and the movement follow the game: the sprites and the demo GIF on terraria.wiki.gg and the
// decompiled 1.4.0.5 source (Projectile.AI_121_StardustDragon, GetAlpha; the Main.cs projectile draw), read
// 2026-10-03. Movement, chain, dust and the fight are in lib/stardust-dragon-ai.js, the pictures in
// lib/stardust-dragon.js and, for Stardust Flame, lib/stardust-flame.js.
//
// Rules (proposed 2026-10-03; lifetime, growth and art agreed with the user, the numbers are placeholders and
// each becomes an XML field):
//   Weapon: a staff like the Rainbow Crystal Staff: no shooting verb, a weak melee bash, two ability buttons: the
//   summon and Stardust Flame.
//   First cast: target a cell within 12 cells. The dragon appears there: head, four body pieces, tail, each fading
//   in over 7 ticks with 2 dust a tick (Terraria's summon). It lasts 60 s. One dragon per wielder. (Terraria's
//   first summon has two body pieces; four, because a 4-piece dragon looked stubby and fish-like next to a long
//   one: the user's note, 2026-10-03. "Body pairs at the first cast" 1 shows Terraria's.)
//   Recast while it is out: no target. A body pair fades in at the tail and the 60 s start again (Terraria:
//   each extra summon inserts a pair in front of the tail). Cooldown 10 s between casts, while it has fewer than 12
//   pieces, so pieces Stardust Flame burned grow back. Each piece makes the dragon 1 % bigger (Terraria's rule).
//   Target: the nearest standing enemy within 20 cells of the wielder, walls or no walls; kept until it is down
//   or out of range. It flies through walls and pawns. With no target it patrols round the wielder (Terraria's
//   idle movement), so it follows them.
//   Hit: any enemy a piece passes within its hit box of (30 x size Terraria px square, plus half a humanoid) takes
//   4 x (1 + 0.23 x (pieces - 1)) cut, at most once per 0.5 s per pawn (Terraria: damage + 23 % per piece, one
//   shared hit timer). 8.6 at 6 pieces, 14.1 at 12. Allies and the wielder take nothing; downed pawns are ignored.
//   An unarmoured pawn goes into pain shock at 43 (lib/terraria.js PainShock): 5 hits at 6 pieces.
//   Ends: after 60 s, or when the wielder goes down. Every piece fades out over 7 ticks with 2 dust a tick, the
//   summon backwards. (Terraria has no end effect; this is the one addition, so the end can be seen.)
//   Stardust Flame (not Terraria's; agreed with the user 2026-10-03): the second button, a Shared/Command_TapHold,
//   usable while the dragon is out and has a target. The flame costs the dragon its body: while the flame is out the
//   last body piece in front of the tail burns away, 1.5 s of flame a piece, with a pulse of light running up the
//   body to the mouth, and the tail closes up behind it. Fewer pieces also mean less contact damage (23 % a piece) and
//   a shorter flame. Recasting grows them back.
//     Tap: 1 piece. Hold: the button fills one mark per 0.3 s, one mark per piece above 4 (head, a pair, tail);
//     let go to spend that many. A breath stops at 4 pieces, so a tap at 4 does nothing.
//     The breath: the dragon stops its passes, hangs 0.55 x reach from its target circling it slowly and opens its
//     jaw 40 degrees. The flame leaves the middle of the open mouth along the line halfway between the jaws, so the
//     head turns 20 degrees past its target to aim it (user's note, 2026-10-03). The flame comes out once it faces the
//     target within 20 degrees and the target is within reach; pieces burn only while it is out. A cone 15 degrees each side, 4 cells long at 4
//     pieces and 0.25 longer a piece (6 at 12), stopped by walls. Every standing enemy in it takes 3 burn every
//     0.25 s (12 a second); allies take nothing. With no target for 1 s the breath ends and keeps what it has not burned.
//     Last Breath: hold past the top mark. Every piece burns, 0.5 s each, the tail last; per piece spent the flame
//     grows 0.25 cells, 1 degree each side and 10 % burn. Then the head alone dives at its target and bursts: 4 burn
//     per piece the dragon had, to every enemy within 2.5 cells. The summon is over. Only Last Breath sets fires: its
//     flame lights cells under it (6 % a cell every 0.25 s), the burst half the cells it covers, and either sets a
//     pawn it burns alight one time in four. RimWorld's fire then spreads and hurts anyone, colonists too.
//
// Order (defaults: scenario "summon + raid", raid from 0, pace .5; times from the simulation):
//   0.00  the wielder swings the staff overhead (0.6 s, Terraria's useStyle 1), the orb glints; at the same
//         time the dragon fades in on the cast cell, 5 cells ahead, laid out along the line from the wielder and
//         already moving at the staff's 10 px a tick, in a puff of Ice Torch dust. A red ring marks
//         its target: the raider behind the wall, the nearest enemy. It speeds up toward him and flies through
//         the wall and through him, coasts on 1.5 cells, brakes into a loop and comes back: one hit a pass, each
//         with a cyan flash and a white slash on his chest.
//   3.23  he goes down (5 hits of 8.6). The ring moves to the raider who has walked in beside the colonist; the
//         dragon's passes cross the colonist at 4.3, 5.7 and 7.2 s and he takes nothing.
//   7.30  that raider goes down; the ring moves to the third raider, further out.
//  11.88  he goes down. The raider 23 cells out was never in range. No target: the dragon loops back toward the
//         wielder in big lazy loops that close in over about 15 s.
//  15.88  end.
// "recast grows": the dragon has been out 20 s and circles the wielder. At 1 s and 11 s the wielder swings the
//   staff and a body pair fades in at the tail: 8, then 10 pieces.
// "flame: tap, then hold": a 12-piece dragon circles the wielder; four raiders walk in, one stops behind the wall, one
//   comes from further out. The Stardust Flame button stands over the wielder's head.
//   1.40  a tap. The dragon breaks off, hangs in front of the nearest raider and breathes from 2.02 to 3.55: one
//         piece burns away (3.47) and the tail closes up; he goes down at 3.02 (5 burns, 2 hits).
//   3.90  the button is held; let go at 5.20 at the 4th of 7 marks: 4 pieces. From 5.65 the flame finishes the
//         second raider (5.92) and the one coming in (7.22), then turns to the one behind the wall, which shields him
//         until the circling head brings the flame round (7.60); he goes down at 11.10 with 3 pieces burned. No
//         target for 1 s: the breath ends at 12.10 and keeps the fourth, 8 pieces.
//  14.00  a recast grows a pair back: 10 pieces.
// "last breath": a 12-piece dragon; three raiders and a mech run in from out of range. The button is held from 0.5 s
//   past the top mark and let go at 3.5: Last Breath. The flame comes out at 4.25 and grows as 11 pieces burn, one
//   every 0.5 s; the raiders go down at 7.00, 7.77 and 8.78, some of them alight. At 9.87 the head is alone; it dives
//   at the mech and bursts on it at 10.08 (48 burn), downing it. 26 cells burn on; the summon is over.
// "ends (time up)": a 12-piece dragon circling;
//   its 60 s run out at 2 s and it fades away in dust. "ends (wielder downed)": the wielder goes down at 1.5 s.
//   "pieces": the four textures large, plain and mirrored, and the dragon of 4 (Terraria's first summon) and 6
//   pieces laid out straight at 2x size, to compare with the wiki's pictures.
//
// Drawing: each piece is its texture on one quad turned to its line of flight: the body pieces a blue belly with
// scales, a gold spine and three ribs, with a tall spike on body 1 and three small ones on body 2 (the user kept this
// body over a continuous one, 2026-10-03); the tail a gold stub and shaft into three swept crystal blades. The head,
// 15 % bigger and on top, is in parts: the skull with two curved horns; the lower jaw on its hinge, opening a little
// in idle flight, wide (30 degrees) as the head closes on its target and shutting the moment the dragon hits someone,
// with a dark mouth and a glowing throat between the jaws; three crystal blades of the crest behind it that sway and
// swing out in turns; a glowing eye; two whiskers that stream back along the line the head has flown. Everything is
// drawn as Terraria draws the dragon (full bright, alpha halved: once normal at .5 and once additive at .5) and
// mirrored when it flies west, so the gold spine is always on the north side; a piece that mirrors rolls over in
// 0.16 s. Pieces draw neck to tail, then the tail, then the head (Terraria's order), each with a soft shadow on the
// ground and a soft cyan glow under it; the body sways gently from side to side (a picture only); a soft trail of
// light follows the tail for 1.6 cells. The glow, the bigger head, the head's moving parts, the sway, the roll, the
// trail and the hit flash are not Terraria's: they make it read as a dragon of light at a normal RimWorld zoom
// (user's notes, 2026-10-03).
// Ice Torch dust: about 2 a second per piece at a random point of the piece, drifting and slowing, swelling then
// shrinking over 0.7 s, each with a faint pool of its light on the floor. The movement runs `pace` Terraria ticks per
// tick (0.5: half Terraria's speed, so the shape of the path is the same and its speed readable; 1 is the game). Sizes
// are Terraria's pixels at 0.028 cells each, so a lab/pawn.js pawn (1.17 cells) is as tall as a Terraria player (42 px).
// Textures: lab/stardust-head, -jaw, -blade, -body1, -body2, -tail and -dust, made in lib/stardust-dragon-textures.js;
// still to be written out as PNGs by a make_stardust_dragon_textures.py. Pawns are lib/pawn.js stand-ins, the staff is a
// stand-in for its texture, walls are the Paper Bomb kit's. "Show stand-ins" off hides pawns, walls, the staff,
// the range and target rings and the damage bars.
import { Color, Mathf } from '../js/engine.js';
import { P, Body, Y, Floor, Lift, sprite, soft, glow } from './lib/six-paths-impact.js';
import { pawn, at, height, shadowLayer, Skin } from './lib/pawn.js';
import { Enemy, Ally } from './lib/chain-sickle.js';
import { ringAt } from './lib/goku.js';
import { walls } from './lib/paper-bomb.js';
import { damageBar, PainShock, MechDown, MechGrey, MechHead, walk, downSmoke } from './lib/terraria.js';
import { simulate, liveDust, dustAt, kindOf, LeastPieces, Hold, BreathJaw } from './lib/stardust-dragon-ai.js';
import { piece, rollOf, aura, wake, head, whisker, WhiskerRoots, HeadScale, dust, staff, IceColour, CyanColour, LightColour } from './lib/stardust-dragon.js';
import { flame, mouthGlow, burning, pulses, burst, fires, onFire, button } from './lib/stardust-flame.js';

const smooth = Mathf.Smooth, D2R = Mathf.Deg2Rad;
const White = new Color(1, 1, 1), Warn = new Color(.85, .18, .12), Wielder = new Color(.30, .50, .62);
// The rule's numbers that are not params (XML fields in a port).
const PerPiece = .23, HitEvery = .5;                 // the cast range (12 cells) has no picture: the cast cell is fixed here
// Decided looks and timing of the picture.
const Swing = .6;                                      // the staff's overhead swing, Terraria's 36 ticks
const ChestLift = .05;                                 // lib/pawn.js: a pawn's chest is .05 north of its cell centre
const FlyHeight = height('chest');                     // lab height of the chest above the feet, for shadows
const DustLife = .7, DustSize = 15;                    // seconds; Terraria px across at its largest, glow included (scale 2 of a 6 px spot)
const PreRoll = 20, Tail = 4, MaxTime = 30;            // seconds the dragon is out before an "ends"/"recast" scene starts; after the last down
// The body's sway, a picture only (Terraria's body is straight in straight flight): Terraria px across, radians per
// piece along the body, radians a second; the head stays on its line and the sway grows over the first two pieces.
const WaveAmp = 3.5, WaveK = 1, WaveRate = 9;
const HitFlash = .3;                                   // seconds a hit's flash and slash last
// The head's life: the jaw's idle opening, how wide it opens as it closes on its target, how fast it shuts on a hit
// and opens again (degrees, cells, seconds); the crest's swing per radian of turn; the whiskers [root, length in
// cells, how far they spread toward the belly in Terraria px].
const JawIdle = 6, JawWide = 30, JawNear = 2.5, JawShut = .05, JawReopen = .3, CrestSwing = .6;
const Whiskers = [[0, 1.3, 11], [1, 1, 17]];
const WakeLong = 1.6, WakeSize = 22, TipBehind = 40;  // the tail's wake: cells long, Terraria px long at its first spot; the blades' tips, px behind the tail piece
const Back = 7;                                        // in "summon + raid" the wielder stands this far behind the chosen cell
// "summon + raid", in cells from the wielder: x toward the raid, z across.
const CastAt = { x: 5, z: 1 };
const Crowd = [
  { path: [[0, 16, -1], [3, 9, -1]] },                 // walks in and stops beside the colonist
  { path: [[0, 12, 3.5]] },                            // behind the wall: no line of sight from the wielder; the nearest at first
  { path: [[0, 15, -4]] },                             // further out, in range
  { path: [[0, 23, 1]] },                              // out of range
  { path: [[0, 8.5, -1.8]], ally: true },              // a colonist a cell from where the first raider stops
];
const WallCells = [[10, 2], [10, 3], [10, 4]];
// Stardust Flame's rule numbers that are not params (XML fields in a port): seconds between burns, cells of reach per
// piece, the cone's half-angle (degrees), the burst's radius (cells).
const BreathEvery = .25, ReachPer = .25, Half = 15, BurstRadius = 2.5;
const ButtonFade = .4;                                 // seconds the button stand-in stays lit after it is let go
// "flame: tap, then hold": raiders walking in (one stops behind the wall, one comes in later), the button's presses
// [from, to], the recast.
const FlameCrowd = [{ path: [[0, 15, -1.5], [4, 8, -1.2]] }, { path: [[0, 16, .8], [4, 9, .6]] }, { path: [[0, 17.5, 3.4], [3, 12.2, 3.4]] },
  { path: [[0, 24, -3.2], [6, 10.5, -2.6]] }];
const FlameWalls = [[11, 2], [11, 3], [11, 4]];
const TapPress = [1.3, 1.4], HoldPress = [3.9, 5.2], Regrow = 14, FlameEnd = 17;
// "last breath": three raiders and a mech run in from out of range (20 cells) while the button is held past the top
// mark (9 marks of 0.3 s at 12 pieces), so the dragon has not worn them down before Last Breath.
const LastCrowd = [{ path: [[0, 25.2, -.9], [6, 9.2, -.8]] }, { path: [[0, 25.4, .9], [6, 9.4, .8]] }, { path: [[0, 27, -.3], [6, 11, -.2]] },
  { path: [[0, 26.2, .1], [6, 10.2, 0]], mech: true }];
const LastPress = [.5, 3.5], AfterBurst = 4;
const Scenarios = ['summon + raid', 'recast grows', 'flame: tap, then hold', 'last breath', 'ends (time up)', 'ends (wielder downed)', 'pieces'];
const SimKeys = ['scenario', 'direction', 'pace', 'px', 'range', 'damage', 'life', 'cooldown', 'most', 'first', 'breathPer', 'breathDamage', 'breathReach', 'lastPer', 'burstPer'];

const add = (a, b) => ({ x: a.x + b.x, z: a.z + b.z });
const bump = x => (x >= 0 && x <= 1) ? Math.sin(x * Math.PI) : 0;
// Casts that grow the dragon to its most pieces, the last one `before` seconds before 0.
const castsToFull = p => 1 + Math.max(0, Math.floor((p.most - 2 - 2 * p.first) / 2));
const grown = (p, before, at) => { const n = castsToFull(p), first = -before - (n - 1) * p.cooldown; return Array.from({ length: n }, (_, i) => ({ t: first + i * p.cooldown, at })); };

// The scene's setup for the simulation, in world cells from the wielder.
function setup(p) {
  const a0 = p.direction * D2R, ca = Math.cos(a0), sa = Math.sin(a0), world = q => ({ x: q.x * ca - q.z * sa, z: q.x * sa + q.z * ca });
  const breath = { perPiece: p.breathPer, damage: p.breathDamage, every: BreathEvery, reach: p.breathReach, reachPer: ReachPer, half: Half * D2R, lastPer: p.lastPer, burstPer: p.burstPer, burstRadius: BurstRadius };
  const rules = { life: p.life, range: p.range, damage: p.damage, perPiece: PerPiece, hitEvery: HitEvery, most: p.most, firstPairs: p.first, breath };
  const base = { px: p.px, pace: p.pace, wielder: () => ({ x: 0, z: 0 }), wielderDown: Infinity, people: [], rules, cells: [], breaths: [], world };
  const near = world({ x: 1.5, z: 0 }), cells = list => list.map(([x, z]) => { const q = world({ x, z }); return { x: Math.round(q.x), z: Math.round(q.z) }; });
  const raiders = (list, arrive) => list.map(c => ({ at: s => world(walk(c.path, s)), ally: !!c.ally, mech: !!c.mech, tough: c.mech ? MechDown : PainShock, arrive }));
  if (p.scenario === 'summon + raid')
    return { ...base, t0: 0, t1: MaxTime, casts: [{ t: 0, at: world(CastAt) }], cells: cells(WallCells),
      people: raiders(Crowd), end: r => Math.min(MaxTime, (r.downs.length ? r.downs[r.downs.length - 1].t : 6) + Tail) };
  if (p.scenario === 'recast grows')
    return { ...base, t0: -PreRoll, t1: 4 + p.cooldown, casts: [{ t: -PreRoll, at: near }, { t: 1, at: near }, { t: 1 + p.cooldown, at: near }], end: () => 4 + p.cooldown };
  if (p.scenario === 'flame: tap, then hold') {
    const casts = [...grown(p, PreRoll, near), { t: Regrow, at: near }];
    return { ...base, t0: casts[0].t, t1: FlameEnd, casts, cells: cells(FlameWalls), people: raiders(FlameCrowd, 0),
      breaths: [{ from: TapPress[0], t: TapPress[1] }, { from: HoldPress[0], t: HoldPress[1] }], end: () => FlameEnd };
  }
  if (p.scenario === 'last breath') {
    const casts = grown(p, PreRoll, near);
    return { ...base, t0: casts[0].t, t1: MaxTime, casts, people: raiders(LastCrowd, 0), breaths: [{ from: LastPress[0], t: LastPress[1] }],
      end: r => Math.min(MaxTime, r.burst ? r.burst.t + AfterBurst : MaxTime) };
  }
  if (p.scenario === 'ends (time up)') {
    const casts = grown(p, p.life - 2, near);                       // every cast in turn, the last one 60 s before the end
    return { ...base, t0: casts[0].t, t1: 4, casts, end: () => 4 };
  }
  return { ...base, t0: -PreRoll, t1: 3.5, wielderDown: 1.5, casts: [{ t: -PreRoll, at: near }], end: () => 3.5 };
}
let cached = { key: '', value: null };
function replay(p) {
  const key = SimKeys.map(k => p[k]).join('|');
  if (cached.key === key) return cached.value;
  const set = setup(p), r = simulate(set);
  const value = { ...r, set, end: set.end(r), casts: set.casts.filter(c => c.t >= 0) };
  cached = { key, value };
  return value;
}

// Where the dragon's pieces are at step k, head first: id, kind, screen point, angle, mirror, seconds since it
// mirrored, alpha, how far Stardust Flame has burned it (0..1).
function pieces(r, k, o, s) {
  const out = [];
  for (let i = 0; i < r.count[k]; i++) {
    const id = r.order[k * r.ids + i], at = k * r.ids + id;
    out.push({ id, kind: kindOf(id), c: { x: o.x + r.X[at], z: o.z + r.Z[at] + ChestLift }, rot: r.R[at], flip: r.F[at], since: s - r.FT[at], alpha: r.A[at], burn: r.burn[at] });
  }
  return out;
}

// The dragon: the sway, shadows and glow, then the pieces in Terraria's order (neck to tail, the tail, the head on
// top); the whiskers go under the head. A piece that burns shrinks to half and fades to 60 % under a white glow.
function dragon(list, cells, sun, strength, s, { wave = 1, key = '', open = JawIdle * D2R, whiskers = [], layer = Y + .1 } = {}) {
  const n = list.length, off = list.map((q, i) => wave * WaveAmp * cells * Math.min(1, i / 2) * Math.sin(i * WaveK - s * WaveRate));
  list.forEach((q, i) => {
    q.c = { x: q.c.x - Math.sin(q.rot) * off[i], z: q.c.z + Math.cos(q.rot) * off[i] };
    if (i > 0 && i < n - 1) q.rot += Math.atan((off[i - 1] - off[i + 1]) / (32 * cells));
    q.roll = rollOf(q.flip, q.since);
  });
  list.forEach(q => {
    const g = { x: q.c.x + sun.x * FlyHeight, z: q.c.z - FlyHeight * Lift + sun.z * FlyHeight };
    sprite(g, 24 * cells, 11 * cells, Body.withAlpha(strength * .7 * q.alpha), soft, shadowLayer, -q.rot / D2R);
    aura(q.c, q.rot, cells, q.alpha, layer - .01);
  });
  list.slice(1).forEach((q, i) => {
    const b = q.burn ?? 0;
    piece(q.kind, q.c, q.rot, q.roll, cells, q.alpha * (1 - .4 * b), layer + i * .004, 1 - .5 * b);
    burning(q.c, q.rot, cells, b, s, layer + i * .004 + .003);
  });
  const top = layer + n * .004, h = list[0];
  whiskers.forEach((w, i) => whisker(`stardust whisker${key} ${i}`, w(h), 1.6 * cells * HeadScale, h.alpha, top + .0035 + i * .0001));
  const turn = n > 1 ? Math.atan2(Math.sin(h.rot - list[1].rot), Math.cos(h.rot - list[1].rot)) : 0;
  head(h, cells, h.alpha, top + .004, open, Math.max(-.5, Math.min(.5, -CrestSwing * turn * h.roll.side)), s);
  burning(h.c, h.rot, cells * 1.5, h.burn ?? 0, s, top + .01);       // Last Breath's head, alone, before it bursts
}

// How far the jaw is open at time s (radians): a little in idle flight, wide as the head closes on its target, shut
// the moment the dragon hits someone and opening again over JawReopen.
function jawOpen(r, k, s, o, at) {
  let open = (JawIdle + 3 * Math.sin(s * 2.2)) * D2R;
  const j = r.targets[k];
  if (j >= 0) { const q = add(o, r.where(j, s)), d = Math.hypot(q.x - at.x, q.z - at.z); open = Mathf.Lerp(open, JawWide * D2R, smooth(Mathf.Clamp01((JawNear - d) / 2))); }
  let last = null;
  for (const e of r.hits) { if (e.t > s) break; last = e; }
  if (last) { const b = s - last.t, shut = b < JawShut ? 1 : Math.max(0, 1 - (b - JawShut) / JawReopen); open = Mathf.Lerp(open, D2R, shut); }
  return Mathf.Lerp(open, BreathJaw * D2R, r.flame[k]);                 // wide while the flame is out
}

// Whether the tail is in the chain at step k: Last Breath burns it before the head.
const hasTail = (r, k) => r.count[k] > 1 && r.order[k * r.ids + r.count[k] - 1] === 3;

// The head's path over the last cells of flight, newest first, with the distance flown back to each point.
function headPath(r, k, o, long) {
  const out = [];
  for (let j = k, run = 0; j >= 0 && r.count[j]; j--) {
    const at = j * r.ids, q = { x: o.x + r.X[at], z: o.z + r.Z[at] + ChestLift };
    if (out.length) { const d = Math.hypot(q.x - out[out.length - 1].x, q.z - out[out.length - 1].z); if (d > .6) break; run += d; }
    out.push({ ...q, run });
    if (run > long) break;
  }
  return out;
}
// A whisker: from its root on the snout back along the line the head has flown, spreading toward the belly and
// rippling, so it streams behind and follows the head through a turn.
function whiskerLine(path, cells, [root, long, spread], s) {
  return h => {
    const k = cells * HeadScale, [rx, ry] = WhiskerRoots[root], ahead = rx * k, sign = h.roll.side * h.roll.across, out = [];
    const along = e => {                                   // a point e cells back along the path from the head's centre
      if (e <= 0 || path.length < 2) return { x: h.c.x - Math.cos(h.rot) * e, z: h.c.z - Math.sin(h.rot) * e, rot: h.rot };
      let i = 1; while (i < path.length - 1 && path[i].run < e) i++;
      const a = path[i - 1], b = path[i], u = Math.min(1, (e - a.run) / ((b.run - a.run) || 1));
      return { x: Mathf.Lerp(a.x, b.x, u), z: Mathf.Lerp(a.z, b.z, u), rot: Math.atan2(a.z - b.z, a.x - b.x) };
    };
    for (let d = 0; d <= long + 1e-6; d += .07) {
      const q = along(d - ahead), f = d / long, side = (ry - spread * f + 1.2 * Math.sin(f * 9 - s * 7) * f) * sign * k;
      out.push({ x: q.x - Math.sin(q.rot) * side, z: q.z + Math.cos(q.rot) * side });
    }
    return out;
  };
}

// The tail's wake: where the tips of its blades were over the last WakeLong cells of flight, newest first.
function wakeLine(r, k, o, cells) {
  const out = [];
  let run = 0;
  for (let j = k; j >= 0 && hasTail(r, j); j--) {
    const at = j * r.ids + 3, x = o.x + r.X[at] - Math.cos(r.R[at]) * TipBehind * cells, z = o.z + r.Z[at] - Math.sin(r.R[at]) * TipBehind * cells + ChestLift;
    if (out.length) { const d = Math.hypot(x - out[out.length - 1].x, z - out[out.length - 1].z); if (d > .6) break; run += d; }   // a recast moves the tail back: the wake starts again
    out.push({ x, z });
    if (run > WakeLong) break;
  }
  return out;
}

// The staff's angle on screen at time s: held up and forward, or in its overhead swing from front-up over the head
// to behind, Terraria's useStyle 1; glint: the orb flashing at the top of the swing.
function staffPose(s, casts, facing) {
  let deg = 72, glint = 0;
  for (const c of casts) {
    const u = (s - c.t) / Swing;
    if (u < 0 || u > 1.4) continue;
    deg = u <= 1 ? 45 + 160 * smooth(u) : 205 - 133 * smooth((u - 1) / .4);
    glint = Math.max(glint, bump((u - .15) / .35));
  }
  return { deg: facing > 0 ? deg : 180 - deg, glint };
}

// The Stardust Flame button over the wielder's head at time s: how full, its marks (pieces a hold can spend), a press
// flash, how lit. Shown in the flame scenarios while the dragon is out; it empties over ButtonFade once let go.
function buttonState(r, s, k) {
  for (const b of r.set.breaths) {
    if (s < b.from || s > b.t + ButtonFade) continue;
    const held = Math.min(s, b.t) - b.from, marks = Math.max(0, r.count[r.step(Math.min(s, b.t))] - LeastPieces), after = Math.max(0, s - b.t) / ButtonFade;
    const fill = held < Hold.tap ? 0 : Math.min(1, (held - Hold.tap) / (Hold.step * (marks + 1)));
    return { fill: fill * (1 - after), marks, press: Math.max(0, 1 - (s - b.from) / .15), alpha: 1 - .5 * after };
  }
  return { fill: 0, marks: Math.max(0, r.count[k] - LeastPieces), press: 0, alpha: .5 };
}

// "pieces": the four textures large, plain and mirrored, and 4 and 6 pieces in a row at 2x size.
function sheet(o) {
  const kinds = ['head', 'body1', 'body2', 'tail'];
  kinds.forEach((kind, i) => {
    piece(kind, { x: o.x - 4.5 + i * 3, z: o.z + 4 }, 0, rollOf(1, 99), 2.6 / 48, 1, Y + .1 + i * .004);
    piece(kind, { x: o.x - 4.5 + i * 3, z: o.z + 1.2 }, Math.PI, rollOf(-1, 99), 2.6 / 48, 1, Y + .1 + i * .004);
  });
  [4, 6].forEach((n, row) => {
    const scale = 2 * (1 + .01 * (n - 1)), gap = 16 * .028 * scale, ids = [0, 1, 2, ...Array.from({ length: n - 4 }, (_, i) => 4 + i), 3];
    const list = ids.map((id, i) => ({ id, kind: kindOf(id), c: { x: o.x - 3 + i * gap, z: o.z - 1.8 - row * 2.2 }, rot: Math.PI, flip: -1, since: 99, alpha: 1 }));
    dragon(list, .028 * scale, { x: 0, z: 0 }, 0, 0, { wave: 0, key: ` ${n}`, open: 14 * D2R });
  });
}

export default {
  kit: 'Stardust Dragon Staff', label: 'Stardust Dragon (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'summon + raid', options: Scenarios, group: 'Showcase' },
    direction: P('Raid comes from (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    actors: { label: 'Show stand-ins (pawns, walls, staff, rings, damage bars)', value: true, group: 'Showcase' },
    light: { label: 'Dust lights the floor', value: true, group: 'Showcase' },
    pace: P('Pace (share of Terraria speed)', .5, .2, 1, .05, 'Motion'),
    px: P('Size (cells per Terraria pixel)', .028, .015, .05, .001, 'Motion'),
    range: P('Range from the wielder (cells)', 20, 8, 30, 1, 'Rule'),
    first: P('Body pairs at the first cast (Terraria: 1)', 2, 1, 4, 1, 'Rule'),
    damage: P('Cut per hit at 1 piece', 4, 1, 15, .5, 'Rule'),
    life: P('Lasts after the last cast (s)', 60, 10, 120, 5, 'Rule'),
    cooldown: P('Cooldown between casts (s)', 10, 2, 30, 1, 'Rule'),
    most: P('Most pieces (recasts stop here)', 12, 6, 20, 2, 'Rule'),
    breathPer: P('Seconds of flame per piece', 1.5, .5, 4, .1, 'Stardust Flame'),
    breathDamage: P('Burn per 0.25 s in the cone', 3, 1, 10, .5, 'Stardust Flame'),
    breathReach: P('Reach at 4 pieces (cells; +0.25 a piece)', 4, 2, 8, .5, 'Stardust Flame'),
    lastPer: P('Last Breath: seconds per piece', .5, .2, 1.5, .05, 'Stardust Flame'),
    burstPer: P('Last Breath: burst burn per piece', 4, 1, 10, .5, 'Stardust Flame'),
  },
  duration(p) { return p.scenario === 'pieces' ? 1 : replay(p).end; },
  phases(p) {
    if (p.scenario === 'pieces') return [];
    const r = replay(p), list = [];
    const named = { grow: e => `Recast: ${e.pieces} pieces`, 'time up': () => 'Time up: fades', 'wielder down': () => 'Wielder down: fades',
      breath: e => e.tap ? 'Tap: 1 piece' : `Hold: ${e.pieces} pieces`, 'last breath': e => `Last Breath (${e.pieces} pieces)`,
      'breath end': e => `Breath ends: ${e.pieces} pieces left`, burst: () => 'Burst', refused: e => `Refused (${e.why})` };
    if (p.scenario === 'summon + raid') {
      list.push({ name: 'Cast: the dragon appears', t: 0 });
      r.downs.forEach((d, k) => list.push({ name: `Down ${k + 1}`, t: d.t }));
      if (r.downs.length) list.push({ name: 'No target: back to the wielder', t: r.downs[r.downs.length - 1].t + .02 });
    } else {
      list.push({ name: `Circling (${r.count[r.step(0)]} pieces)`, t: 0 });
      r.events.filter(e => e.t > 0 && named[e.kind]).forEach(e => list.push({ name: named[e.kind](e), t: e.t }));
      if (r.set.breaths.length) r.downs.forEach((d, k) => list.push({ name: `Down ${k + 1}`, t: d.t }));
      list.sort((a, b) => a.t - b.t);
    }
    return list;
  },
  events(p) {
    if (p.scenario === 'pieces') return [];
    const r = replay(p), sounds = { breath: 'AG_StardustDragon_Breath', 'last breath': 'AG_StardustDragon_Breath', burst: 'AG_StardustDragon_Burst' };
    return [...r.casts.map(c => ({ t: c.t, type: 'sound', def: 'AG_StardustDragon_Summon' })),
      ...r.events.filter(e => e.t >= 0 && sounds[e.kind]).map(e => ({ t: e.t, type: 'sound', def: sounds[e.kind] }))];
  },

  draw(s, p, { origin, scene }) {
    if (p.scenario === 'pieces') { sheet(origin); return; }
    const r = replay(p);
    if (s < 0 || s >= r.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const raid = p.scenario === 'summon + raid', a0 = p.direction * D2R;
    // o is the wielder's cell: in "summon + raid" Back whole cells behind the chosen cell, so the fight is centred.
    const o = raid ? add(origin, { x: Math.round(-Back * Math.cos(a0)), z: Math.round(-Back * Math.sin(a0)) }) : origin;
    const k = r.step(s), cells = p.px * r.scale[k];

    // --- the floor: the range ring, the target ring, walls --------------------------------------------------------------
    if (p.actors && r.live[k]) ringAt(o, p.range, Color.Lerp(CyanColour, White, .5).withAlpha(.12), Floor + .01);
    if (p.actors && r.targets[k] >= 0) ringAt(add(o, r.where(r.targets[k], s)), .5, Warn.withAlpha(.6), Floor + .02);
    if (p.actors && r.set.cells.length) walls('stardust dragon wall', o, r.set.cells.map(c => add(o, c)), sun, strength);

    // --- pawns, north first; the wielder with the staff ----------------------------------------------------------------
    const downed = s >= r.set.wielderDown, facing = Math.cos(a0) >= 0 || !raid ? 1 : -1;
    const people = r.people.map((c, j) => {
      const pos = add(o, r.where(j, s)), hit = c.hits.filter(h => s - h.t >= 0 && s - h.t < .15).pop();
      return { c, j, pos, down: s >= c.down, hit, share: c.dmg[k] / c.tough };
    });
    if (p.actors) [...people, { pos: o, wielder: true }].sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      if (g.wielder) {
        pawn(g.pos, { shirt: Wielder, sun, shadow: strength, downed });
        if (!downed) { const pose = staffPose(s, r.casts, facing); staff(add(at(g.pos, 'waist'), { x: .2 * facing, z: .05 }), pose.deg, s, pose.glint); }
        if (!downed && r.set.breaths.length && r.live[k]) { const b = buttonState(r, s, k); button(add(at(g.pos, 'headTop'), { x: 0, z: .5 }), b.fill, b.marks, b.press, b.alpha, Y + .3); }
        return;
      }
      const scorched = g.c.burns.some(e => s - e.t >= 0 && s - e.t < .2) ? .35 : 0;
      const lit = Math.max(scorched, g.hit ? .7 * (1 - (s - g.hit.t) / .15) : 0), body = g.c.mech ? 'hulk' : 'average';
      const shirt = g.c.mech ? MechGrey : g.c.ally ? Ally : Enemy, skin = g.c.mech ? MechHead : Skin;
      pawn(g.pos, { body, shirt: Color.Lerp(shirt, IceColour, lit), skin: Color.Lerp(skin, IceColour, lit), sun, shadow: strength, downed: g.down });
      if (!g.down && !g.c.ally) damageBar(at(g.pos, 'headTop', { body }), g.share);
      if (g.down) downSmoke(g.pos, s - g.c.down);
      if (s >= g.c.fire) onFire(at(g.pos, g.down ? 'waist' : 'chest'), s, s - g.c.fire, Y + .05);
    });

    // --- the dragon ------------------------------------------------------------------------------------------------------
    if (r.count[k]) {
      const list = pieces(r, k, o, s);
      if (hasTail(r, k)) wake(wakeLine(r, k, o, cells), WakeSize * cells, list[list.length - 1].alpha, Y + .095);
      const path = headPath(r, k, o, 2.5);
      dragon(list, cells, sun, strength, s, { open: jawOpen(r, k, s, o, list[0].c), whiskers: Whiskers.map(w => whiskerLine(path, cells, w, s)) });
      pulses(r, s, list, cells, Y + .16);
    }

    // --- Stardust Flame: the breath, its light, Last Breath's burst and the fires it leaves ---------------------------------
    const oc = { x: o.x, z: o.z + ChestLift }, below = FlyHeight * Lift;
    flame(r, s, oc, r.set.cells.map(c => add(oc, c)), Y + .17);
    mouthGlow(r, k, oc, below, Y + .17);
    burst(r.burst, s, oc, below, Y + .19);
    fires(r.fires, s, o, Y + .06);

    // --- hits: a cyan flash on the pawn's chest, a white slash along the dragon's line, a small star ------------------------
    people.forEach(g => {
      const h = g.c.hits.filter(e => s - e.t >= 0 && s - e.t < HitFlash).pop();
      if (!h) return;
      const u = (s - h.t) / HitFlash, f = (1 - u) * (1 - u), chest = at(g.pos, 'chest'), deg = -h.rot / D2R;
      sprite(chest, 1.1, 1.1, CyanColour.withAlpha(.55 * f), glow, Y + .2);
      sprite(chest, 1.5 * (.6 + .4 * u), .14 * (1 - u) + .02, White.withAlpha(.9 * f), glow, Y + .201, deg);
      dust(chest, .1 + u * .5, .7, .55 * (1 - .4 * u), false, Y + .202);
    });

    // --- Ice Torch dust and the light it gives the floor --------------------------------------------------------------------
    liveDust(r, s, DustLife).forEach((d, i) => {
      const age = s - d.t, q = dustAt(d, age, r.px), c = { x: o.x + q.x, z: o.z + q.z + ChestLift };
      const a = dust(c, age, DustLife, DustSize * p.px, d.faint, Y + .18 + (i % 20) * .0003);   // summon dust gives no light (Terraria's noLight)
      if (p.light && a > 0 && !d.faint) sprite({ x: c.x, z: c.z - FlyHeight * Lift }, 1.4, 1, LightColour.withAlpha(.07 * a), glow, Floor + .012);
    });
  },
};
