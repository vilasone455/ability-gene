// Stardust Dragon Staff — weapon proposal. Sketch only: no C#.
// From Terraria's Stardust Dragon Staff (Lunatic Cultist tier, made from Stardust Fragments): the staff summons a
// dragon that flies through blocks and hurts whatever its body touches; summoning again makes the same dragon
// longer. The look and the movement follow the game: the sprites and the demo GIF on terraria.wiki.gg and the
// decompiled 1.4.0.5 source (Projectile.AI_121_StardustDragon, GetAlpha; the Main.cs projectile draw), read
// 2026-10-03. Movement, chain, dust and the fight are in lib/stardust-dragon-ai.js, the pictures in
// lib/stardust-dragon.js.
//
// Rules (proposed 2026-10-03; lifetime, growth and art agreed with the user, the numbers are placeholders and
// each becomes an XML field):
//   Weapon: a staff like the Rainbow Crystal Staff: no shooting verb, a weak melee bash, one ability button.
//   First cast: target a cell within 12 cells. The dragon appears there: head, body, body, tail, each fading in
//   over 7 ticks with 2 dust a tick (Terraria's summon). It lasts 60 s. One dragon per wielder.
//   Recast while it is out: no target. A body pair fades in at the tail and the 60 s start again (Terraria:
//   each extra summon inserts a pair in front of the tail). Cooldown 10 s between casts, at most 4 casts: 10
//   pieces. Each piece makes the dragon 1 % bigger (Terraria's rule).
//   Target: the nearest standing enemy within 20 cells of the wielder, walls or no walls; kept until it is down
//   or out of range. It flies through walls and pawns. With no target it patrols round the wielder (Terraria's
//   idle movement), so it follows them.
//   Hit: any enemy a piece passes within its hit box of (30 x size Terraria px square, plus half a humanoid) takes
//   5 x (1 + 0.23 x (pieces - 1)) cut, at most once per 0.5 s per pawn (Terraria: damage + 23 % per piece, one
//   shared hit timer). 8.5 at 4 pieces, 15.4 at 10. Allies and the wielder take nothing; downed pawns are ignored.
//   An unarmoured pawn goes into pain shock at 43 (lib/terraria.js PainShock): 6 hits at 4 pieces.
//   Ends: after 60 s, or when the wielder goes down. Every piece fades out over 7 ticks with 2 dust a tick, the
//   summon backwards. (Terraria has no end effect; this is the one addition, so the end can be seen.)
//
// Order (defaults: scenario "summon + raid", raid from 0, pace .5; times from the simulation):
//   0.00  the wielder swings the staff overhead (0.6 s, Terraria's useStyle 1), the orb glints; at the same
//         time the dragon fades in on the cast cell, 5 cells ahead, in a puff of Ice Torch dust. A red ring marks
//         its target: the raider behind the wall, the nearest enemy. It speeds up toward him and flies through
//         the wall and through him, overshoots, brakes, turns and comes back: one hit a pass.
//   3.37  he goes down (6 hits of 8.5). The ring moves to the raider who has walked in beside the colonist; the
//         dragon's passes cross the colonist five times between 4.5 and 6.0 s and he takes nothing.
//   6.67  that raider goes down; the ring moves to the third raider, further out.
//  10.40  he goes down. The raider 23 cells out was never in range. No target: the dragon loops back toward the
//         wielder in big lazy loops that close in over about 15 s.
//  14.40  end.
// "recast grows": the dragon has been out 20 s and circles the wielder. At 1 s and 11 s the wielder swings the
//   staff and a body pair fades in at the tail: 6, then 8 pieces. "ends (time up)": a 10-piece dragon circling;
//   its 60 s run out at 2 s and it fades away in dust. "ends (wielder downed)": the wielder goes down at 1.5 s.
//   "pieces": the four textures large, plain and mirrored, and the dragon of 4 and 6 pieces laid out straight at
//   2x size, to compare with the wiki's pictures.
//
// Drawing: each piece is its texture on one quad turned to its line of flight, drawn as Terraria draws it (full
// bright, alpha halved: the texture once normal at .5 and once additive at .5) and mirrored when it flies west,
// so the gold spine is always on the north side. Pieces draw neck to tail, then the tail, then the head on top
// (Terraria's order), at a pawn's chest height, each with a soft shadow on the ground. Ice Torch dust: about 2 a
// second per piece at a random point of the piece, drifting and slowing, swelling then shrinking over 0.7 s,
// each with a faint pool of its light on the floor. The movement runs `pace` Terraria ticks per tick (0.5: half
// Terraria's speed, so the shape of the path is the same and its speed readable; 1 is the game). Sizes are
// Terraria's pixels at 0.028 cells each, so a lab/pawn.js pawn (1.17 cells) is as tall as a Terraria player (42 px).
// Textures: lab/stardust-head, -body1, -body2, -tail and -dust, made in lib/stardust-dragon.js; still to be
// written out as PNGs by a make_stardust_dragon_textures.py. Pawns are lib/pawn.js stand-ins, the staff is a
// stand-in for its texture, walls are the Paper Bomb kit's. "Show stand-ins" off hides pawns, walls, the staff,
// the range and target rings and the damage bars.
import { Color, Mathf } from '../js/engine.js';
import { P, Body, Y, Floor, Lift, sprite, soft, glow } from './lib/six-paths-impact.js';
import { pawn, at, height, shadowLayer, Skin } from './lib/pawn.js';
import { Enemy, Ally } from './lib/chain-sickle.js';
import { ringAt } from './lib/goku.js';
import { walls } from './lib/paper-bomb.js';
import { damageBar, PainShock, walk, downSmoke } from './lib/terraria.js';
import { simulate, liveDust, dustAt, kindOf } from './lib/stardust-dragon-ai.js';
import { piece, dust, staff, IceColour, CyanColour, LightColour } from './lib/stardust-dragon.js';

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
const Back = 7;                                        // in "summon + raid" the wielder stands this far behind the chosen cell
// "summon + raid", in cells from the wielder: x toward the raid, z across.
const CastAt = { x: 5, z: 1 };
const Crowd = [
  { path: [[0, 16, -1], [3, 9, -1]] },                 // walks in and stops beside the colonist
  { path: [[0, 12, 3.5]] },                            // behind the wall: no line of sight from the wielder; the nearest at first
  { path: [[0, 15, -4]] },                             // further out, in range
  { path: [[0, 23, 1]] },                              // out of range
  { path: [[0, 8.2, -1.6]], ally: true },              // a colonist a cell from where the first raider stops
];
const WallCells = [[10, 2], [10, 3], [10, 4]];
const Scenarios = ['summon + raid', 'recast grows', 'ends (time up)', 'ends (wielder downed)', 'pieces'];
const SimKeys = ['scenario', 'direction', 'pace', 'px', 'range', 'damage', 'life', 'cooldown', 'casts'];

const add = (a, b) => ({ x: a.x + b.x, z: a.z + b.z });
const bump = x => (x >= 0 && x <= 1) ? Math.sin(x * Math.PI) : 0;

// The scene's setup for the simulation, in world cells from the wielder.
function setup(p) {
  const a0 = p.direction * D2R, ca = Math.cos(a0), sa = Math.sin(a0), world = q => ({ x: q.x * ca - q.z * sa, z: q.x * sa + q.z * ca });
  const rules = { life: p.life, range: p.range, damage: p.damage, perPiece: PerPiece, hitEvery: HitEvery, maxCasts: p.casts };
  const base = { px: p.px, pace: p.pace, wielder: () => ({ x: 0, z: 0 }), wielderDown: Infinity, people: [], rules, cells: [], world };
  const near = world({ x: 1.5, z: 0 });
  if (p.scenario === 'summon + raid')
    return { ...base, t0: 0, t1: MaxTime, casts: [{ t: 0, at: world(CastAt) }], cells: WallCells.map(([x, z]) => { const q = world({ x, z }); return { x: Math.round(q.x), z: Math.round(q.z) }; }),
      people: Crowd.map(c => ({ at: s => world(walk(c.path, s)), ally: !!c.ally, tough: PainShock })), end: r => Math.min(MaxTime, (r.downs.length ? r.downs[r.downs.length - 1].t : 6) + Tail) };
  if (p.scenario === 'recast grows')
    return { ...base, t0: -PreRoll, t1: 4 + p.cooldown, casts: [{ t: -PreRoll, at: near }, { t: 1, at: near }, { t: 1 + p.cooldown, at: near }], end: () => 4 + p.cooldown };
  if (p.scenario === 'ends (time up)') {
    const first = 2 - p.life - (p.casts - 1) * p.cooldown;         // every cast in turn, the last one 60 s before the end
    return { ...base, t0: first, t1: 4, casts: Array.from({ length: p.casts }, (_, i) => ({ t: first + i * p.cooldown, at: near })), end: () => 4 };
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

// Where the dragon's pieces are at step k, head first: id, kind, screen point, angle, mirror, alpha.
function pieces(r, k, o) {
  const out = [];
  for (let i = 0; i < r.count[k]; i++) {
    const id = r.order[k * r.ids + i], at = k * r.ids + id;
    out.push({ id, kind: kindOf(id), c: { x: o.x + r.X[at], z: o.z + r.Z[at] + ChestLift }, rot: r.R[at], flip: r.F[at], alpha: r.A[at] });
  }
  return out;
}

// The dragon: shadows, then the pieces in Terraria's order (neck to tail, the tail, the head on top).
function dragon(list, cells, sun, strength, layer = Y + .1) {
  const drawOrder = [...list.slice(1, -1), list[list.length - 1], list[0]].filter(Boolean);
  list.forEach(q => {
    const g = { x: q.c.x + sun.x * FlyHeight, z: q.c.z - FlyHeight * Lift + sun.z * FlyHeight };
    sprite(g, 24 * cells, 11 * cells, Body.withAlpha(strength * .55 * q.alpha), soft, shadowLayer, -q.rot / D2R);
  });
  drawOrder.forEach((q, i) => piece(q.kind, q.c, q.rot, q.flip, cells, q.alpha, layer + i * .004));
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

// "pieces": the four textures large, plain and mirrored, and 4 and 6 pieces in a row at 2x size.
function sheet(o) {
  const kinds = ['head', 'body1', 'body2', 'tail'];
  kinds.forEach((kind, i) => {
    piece(kind, { x: o.x - 4.5 + i * 3, z: o.z + 4 }, 0, 1, 2.6 / 48, 1, Y + .1 + i * .004);
    piece(kind, { x: o.x - 4.5 + i * 3, z: o.z + 1.2 }, Math.PI, -1, 2.6 / 48, 1, Y + .1 + i * .004);
  });
  [4, 6].forEach((n, row) => {
    const scale = 2 * (1 + .01 * (n - 1)), gap = 16 * .028 * scale, ids = [0, 1, 2, ...Array.from({ length: n - 4 }, (_, i) => 4 + i), 3];
    const list = ids.map((id, i) => ({ id, kind: kindOf(id), c: { x: o.x - 3 + i * gap, z: o.z - 1.8 - row * 2.2 }, rot: Math.PI, flip: -1, alpha: 1 }));
    dragon(list, .028 * scale, { x: 0, z: 0 }, 0);
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
    damage: P('Cut per hit at 1 piece', 5, 1, 15, .5, 'Rule'),
    life: P('Lasts after the last cast (s)', 60, 10, 120, 5, 'Rule'),
    cooldown: P('Cooldown between casts (s)', 10, 2, 30, 1, 'Rule'),
    casts: P('Most casts (pieces = 2 + 2 x casts)', 4, 1, 8, 1, 'Rule'),
  },
  duration(p) { return p.scenario === 'pieces' ? 1 : replay(p).end; },
  phases(p) {
    if (p.scenario === 'pieces') return [];
    const r = replay(p), list = [];
    if (p.scenario === 'summon + raid') {
      list.push({ name: 'Cast: the dragon appears', t: 0 });
      r.downs.forEach((d, k) => list.push({ name: `Down ${k + 1}`, t: d.t }));
      if (r.downs.length) list.push({ name: 'No target: back to the wielder', t: r.downs[r.downs.length - 1].t + .02 });
    } else {
      list.push({ name: `Circling (${r.count[r.step(0)]} pieces)`, t: 0 });
      r.events.filter(e => e.t > 0).forEach(e => list.push({ name: e.kind === 'grow' ? `Recast: ${e.pieces} pieces` : e.kind === 'time up' ? 'Time up: fades' : 'Wielder down: fades', t: e.t }));
    }
    return list;
  },
  events(p) {
    if (p.scenario === 'pieces') return [];
    return replay(p).casts.map(c => ({ t: c.t, type: 'sound', def: 'AG_StardustDragon_Summon' }));
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
        return;
      }
      const lit = g.hit ? .7 * (1 - (s - g.hit.t) / .15) : 0, shirt = g.c.ally ? Ally : Enemy;
      pawn(g.pos, { shirt: Color.Lerp(shirt, IceColour, lit), skin: Color.Lerp(Skin, IceColour, lit), sun, shadow: strength, downed: g.down });
      if (!g.down && !g.c.ally) damageBar(at(g.pos, 'headTop'), g.share);
      if (g.down) downSmoke(g.pos, s - g.c.down);
    });

    // --- the dragon ------------------------------------------------------------------------------------------------------
    if (r.count[k]) dragon(pieces(r, k, o), cells, sun, strength);

    // --- Ice Torch dust and the light it gives the floor --------------------------------------------------------------------
    liveDust(r, s, DustLife).forEach((d, i) => {
      const age = s - d.t, q = dustAt(d, age, r.px), c = { x: o.x + q.x, z: o.z + q.z + ChestLift };
      const a = dust(c, age, DustLife, DustSize * p.px, d.faint, Y + .16 + (i % 20) * .0003);   // summon dust gives no light (Terraria's noLight)
      if (p.light && a > 0 && !d.faint) sprite({ x: c.x, z: c.z - FlyHeight * Lift }, 1.4, 1, LightColour.withAlpha(.07 * a), glow, Floor + .012);
    });
  },
};
