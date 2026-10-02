// Last Prism — weapon proposal. The pictures are ported to Source/RimArt/LastPrism/LastPrism* (previews
// "Last Prism: ..."); no weapon, ability or rule draws them yet.
// From Terraria's Last Prism (Moon Lord drop): a crystal prism held out in front that splits light
// into six coloured beams, which narrow over a few seconds into one beam.
//
// Rules (agreed in chat 2026-09-30; every number becomes an XML field and is a placeholder until played):
//   Ammo is stored sunlight. The prism holds 12 s of beam and fills whenever it is on an unroofed
//   cell, held or lying on the ground, at 1 s of beam per 20 s at full sun, scaled by the sky glow
//   (0 at night, less in an eclipse). No charging under a roof, on a pocket map, or from sun lamps.
//   A meter on the weapon's button shows the charge. Firing spends 1 s of charge per second.
//   Fire (hold): the wielder targets an enemy and channels. Six beams leave the prism in a level fan,
//   35 degrees to either side of the aim, and sweep across each other; over 3 s the fan narrows and
//   at 3 s the six join into one beam 1 cell wide. The prism turns after its target at up to 45
//   degrees per second. Beams pass through pawns and stop at walls; range 22 cells.
//   Fan: a pawn crossed by any beam takes 3 burn (10 % armour penetration), at most once per 0.5 s,
//   so up to 6 per second and 18 over the fan. Joined: 8 burn (45 %) to each pawn in the lane, at
//   most once per 0.25 s per pawn, 32 per second; the first touch hits at once, so a pawn the beam
//   swings across takes at least one hit. Burns do not bleed: an unarmoured pawn goes into pain
//   shock at about 43 burn (vanilla: pain shock at 80 %, a burn adds 0.01875 pain per point), about
//   1.4 s in the beam.
//   Friend or foe: allies in the fan or the lane are hurt too. No cooldown: the charge is the limit.
//   Stays joined: when the target goes down, the wielder takes the next enemy in range with a clear
//   line (the one needing the smallest turn) and the joined beam swings to it at 45 degrees per
//   second, burning what it passes. It drops back to the fan only after 1 s without firing (order
//   cancelled, no target, charge empty). A full charge (3 s fan + 9 s joined) downs about 4-5 pawns.
//   Sketch rule, not yet agreed: downed pawns are not hit, as bullets fly over them.
//
// Order (defaults, scenario "fires"; times are what this sketch's simulation gives):
//   0.00  the wielder holds the prism at chest height, a pyramid 0.6 long with its tip pointing at the
//         target and its base 0.3 in front of the wielder; it is still, bobs, glints, and casts a
//         small rainbow on the floor where its shadow falls; meter full (12)
//   0.30  fan: the rainbow goes; the prism starts rolling about its long axis (1 turn a second, rising
//         to 2 at the join; its faces slide and shimmer pink and green); six beams (red to violet)
//         leave from its tip and sweep a 35-degree fan every 1.8 s, each
//         lighting the floor under it; opacity .2 to .45 over the first two thirds and to 1 in the
//         last third while the sweep speeds up to one pass per 0.6 s (Terraria's curve). A red ring marks the target, who walks across the aim. Every pawn a beam crosses
//         flickers in its colour and its damage bar fills (3 at a time): the target, the far raider
//         behind him, the raider by the wall and the colonist standing in front. Beams that sweep over
//         the wall stop on it, so the raider behind it takes nothing; the raider outside the fan takes
//         nothing.
//   3.30  join: white flash and ring at the tip, a pulse runs down the beam; one beam 1 cell wide:
//         a thin white core and pale sheath over six colour bands side by side (red on one edge to
//         violet on the other), sparkles and flow lines, rainbow light on the floor. Terraria draws a
//         white core with two drifting colour fringes instead; the bands were kept by choice (2026-10-01).
//         At the join the fan has done 23 to the target, 15 to the far raider, 15 to the colonist and
//         9 to the mech; nothing to the raider by the wall (the fan's edge), behind it or outside it.
//   4.05  the target goes down; the ring jumps to the far raider and the beam swings to him
//   4.88  he goes down; the beam swings to the raider by the wall
//   6.60  he goes down; the beam swings to the raider outside the fan, sweeping over the wall on the
//         way and stopping on it
//   8.22  he goes down; the beam swings the long way round to the mech and crosses the colonist, who
//         takes one hit (8, 23 in all, still standing)
//  12.30  the charge runs out with the mech at 89 of 150 (it feels no pain; 150 downs it in this
//         sketch): the beam flickers out over 0.35 s and the meter flashes red. The raider behind the
//         wall took nothing. The phases list every down.
// "runs dry" starts with 4 s in the meter: the beam joins at 3.3, the target goes down at 4.05, and
//   the beam cuts out at 4.3 while swinging to the far raider.
// "charges in the sun" shows the meter filling from 3 at 20x speed (one segment a second at full
//   sun) with a flash at the prism as each segment fills. "under a roof": the game's roof overlay
//   stand-in over the wielder, no rainbow, no glints, the meter stays at 3.
//
// Drawing: every beam is a level line at chest height (lib/pawn.js chest, .05 north of the cell
// centre), so the fan lies over the ground and turns freely with the aim; no per-facing method.
// Terraria's side-view fan becomes a fan over the floor, which is also the hit area. The beams are
// soft additive strips (outer colour, lighter middle, thin white core) with rounded ends; a beam
// that reaches its range tapers over the last 1.5 cells. The prism, from the Terraria sprite and demo GIF
// (checked 2026-10-01): a pyramid lying level at chest height, tip at the target, base toward the
// wielder, 0.6 long and about 0.6 across (.45 of a pawn's height, as the sprite is to the player). It
// rolls about its long axis while firing, as the sprite's 5 frames do faster and faster. It is drawn
// as a 3D pyramid each frame (corners in east, up, north; up drawn as Lift north), showing only the
// faces turned to the viewer, so it turns with the aim and needs no per-facing method: aiming east or
// west its outline stays a triangle and the faces slide, like the sprite. Faces: pale, lavender and
// slate blue, lit by the scene's sun, a violet base, pale ridges, a navy outline. Every beam starts at
// the tip, so none covers it. Aiming north (sin > .3) beams, glow and prism draw under the pawn layer
// so they pass behind the wielder's head. The meter over the wielder's head stands in for the gizmo, the bars over the others show damage taken against what downs them (a
// sketch aid, not a game UI), and the roof cells stand in for the game's roof overlay. Pawns are
// lib/pawn.js stand-ins (the mech a grey hulk); walls are the Paper Bomb kit's. "Show stand-ins" off hides the
// pawns, walls, roof cells and damage bars, to compare with the C# recording ("Last Prism: ..."). The aim, the targets,
// the walk and every hit are replayed from 0 at 60 steps a second (cached per parameter set), in
// cells relative to the chosen cell, so the drawing and the damage use the same walls and beams.
import { AltitudeLayer, Color, Mathf, MaterialPool, ShaderDatabase } from '../js/engine.js';
import { P, Body, Y, Floor, Lift, sprite, soft, glow, rand } from './lib/six-paths-impact.js';
import { draw, mesh } from './lib/six-paths-solid.js';
import { pawn, at, shadowLayer, Skin } from './lib/pawn.js';
import { Enemy, Ally, rect } from './lib/chain-sickle.js';
import { strip, streak, glint, ringAt, whiteGlow } from './lib/goku.js';
import { walls } from './lib/paper-bomb.js';
import { hue, rayWall, damageBar } from './lib/terraria.js';

const clamp = Mathf.Clamp01, smooth = Mathf.Smooth, lerp = Mathf.Lerp, D2R = Mathf.Deg2Rad, TAU = Math.PI * 2;
const flat = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const White = new Color(1, 1, 1), Crystal = new Color(.74, .93, 1), CrystalEdge = new Color(.94, .95, 1);
// The prism's faces, from the sprite: a pale white face, a lavender face, a slate-blue face, a violet base, a navy outline.
const FacePale = new Color(.95, .95, 1), FaceLavender = new Color(.80, .70, .98), FaceSlate = new Color(.46, .56, .80);
const FaceBase = new Color(.45, .32, .72), Navy = new Color(.12, .12, .34), FaceDark = new Color(.20, .18, .40);
const Dull = new Color(.45, .5, .56), Sun = new Color(1, .82, .32), SunDim = new Color(.22, .18, .1), Warn = new Color(.85, .18, .12);
const RoofTint = new Color(.55, .64, .82), Wielder = new Color(.30, .50, .62), Smoke = new Color(.32, .32, .34);
const MechGrey = new Color(.52, .54, .58), MechHead = new Color(.36, .38, .42);
// The rule's numbers (XML fields in the port).
const Store = 12, DryStore = 4, StartLevel = 3;       // seconds of beam: full, the "runs dry" start, the charging start
const Turn = 45;                                      // degrees per second the prism turns after its target
const FanHit = 3, FanEvery = .5, JoinHit = 8, JoinEvery = .25;
const PainShock = 43, MechDown = 150;                 // burn that downs an unarmoured pawn; the mech stand-in
// Decided looks and timing of the picture.
const Beams = 6, Lead = .3, Tail = 1.4, Fade = .25, Sputter = .35, JoinFlash = .3, ShowTime = 6;
const SweepSlow = 1.8, SweepFast = .6;                // seconds per sweep of the fan: at the start, and in the last half
const SpinStart = 1, SpinFull = 2, SpinStop = 3;      // prism turns per second about its long axis: channel start, joined; wind-down rate
const StartSide = .1, EndSide = .03;                  // cells across the aim where the fan beams leave the tip
const FanHalf = .16, HitReach = .3;                   // half width of a fan beam's outer glow; a pawn this close is crossed
// The prism: a pyramid 0.6 long with its base corners .35 from its long axis (about .6 across), the sprite's
// 20 x 21 px against a 45 px player, so about .45 of a pawn's height. Its base is .3 in front of the wielder.
const PrismLen = .6, PrismRad = .35, PrismGap = .3, PrismH = .98;   // PrismH: lab height of the chest, for its shadow
const ChestLift = .05;                                // lib/pawn.js: the chest is .05 north of the cell centre on screen
const Back = 7;                                       // the wielder stands this far behind the chosen cell
// Pawns in the aim frame from the wielder: cells along, cells across. The target walks from +2 to -1
// across over WalkTime while standing; the others stand. The wall is three cells across the fan's right side.
const Cast = [
  { along: 12, from: 2, to: -1, walks: true },        // the first target
  { along: 19, from: -1.5 },                          // a raider further out, just off the first line
  { along: 8, from: -4.3 },                           // a raider in front of the wall
  { along: 13.5, from: -5 },                          // behind the wall: no clear line
  { along: 6, from: -6.5 },                           // outside the fan
  { along: 15, from: 7, mech: true },                 // a mech: no pain
  { along: 5, from: 1.3, ally: true },                // a colonist standing in front
];
const WalkTime = 6.3, WallAt = 10, WallAcross = [-3, -4, -5];

const shoots = p => p.scenario === 'fires' || p.scenario === 'runs dry';
const bump = x => (x >= 0 && x <= 1) ? Math.sin(x * Math.PI) : 0;
const wrap = x => x - TAU * Math.round(x / TAU);
const pale = c => Color.Lerp(c, White, .45);
const add = (a, b) => ({ x: a.x + b.x, z: a.z + b.z });

// True when q is within reach of the line from a along ang, between 0 and len cells out.
function onLine(q, a, ang, len, reach) {
  const dx = Math.cos(ang), dz = Math.sin(ang), rx = q.x - a.x, rz = q.z - a.z, along = rx * dx + rz * dz;
  return along > 0 && along < len && Math.abs(rz * dx - rx * dz) <= reach;
}

// Fan sweep phase (radians): one pass per SweepSlow, speeding up to one per SweepFast over the second
// half of the charge, as Terraria's spin rate drops from 16 to 6 frames.
function sweep(s, lead, p) {
  const w0 = TAU / SweepSlow, w1 = TAU / SweepFast, a = lead + p.join * .5, L = p.join * .5, r = clamp((s - a) / L);
  const g = s <= a + L ? L / 3 * r * r * r : L / 3 + (s - a - L);
  return w0 * (s - lead) + (w1 - w0) * g;
}
// The six fan beams at time s: each swept by cos(phase + i/6 turn), narrowing with the charge.
function fanBeams(s, p, lead, aim, tip, cells) {
  const u = clamp((s - lead) / p.join), ph = sweep(s, lead, p), spread = p.fan * D2R * (1 - u), side = lerp(StartSide, EndSide, u);
  const dx = Math.cos(aim), dz = Math.sin(aim), out = [];
  for (let i = 0; i < Beams; i++) {
    const c = Math.cos(ph + i * TAU / Beams), ang = aim + spread * c, start = { x: tip.x - dz * side * c, z: tip.z + dx * side * c };
    const len = rayWall(start, ang, cells, p.range);
    out.push({ i, ang, start, len, blocked: len < p.range - 1e-6 });
  }
  return out;
}
// The prism's tip on the ground under it: where every beam starts.
const tipOf = (caster, aim) => { const d = PrismGap + PrismLen; return { x: caster.x + Math.cos(aim) * d, z: caster.z + Math.sin(aim) * d }; };

// The whole fight, replayed from 0 at 60 steps a second in cells relative to the chosen cell: the aim,
// who is the target, every hit and who goes down. The channel ends when the charge runs out or no enemy
// with a clear line is left in range.
let cached = { key: '', value: null };
function replay(p) {
  const key = [p.scenario, p.aim, p.join, p.width, p.walker, p.range, p.fan].join('|');
  if (cached.key === key) return cached.value;
  const dt = 1 / 60, lead = Lead, join = lead + p.join, dry = lead + (p.scenario === 'runs dry' ? DryStore : Store);
  const a0 = p.aim * D2R, ca = Math.cos(a0), sa = Math.sin(a0);
  const caster = { x: -ca * Back, z: -sa * Back }, world = q => ({ x: caster.x + q.x * ca - q.z * sa, z: caster.z + q.x * sa + q.z * ca });
  const cells = WallAcross.map(ac => { const q = world({ x: WallAt, z: ac }); return { x: Math.round(q.x), z: Math.round(q.z) }; });
  const n = Math.ceil((dry + Sputter + Tail + .5) / dt);
  const people = Cast.map(c => ({ ...c, tough: c.mech ? MechDown : PainShock, total: 0, lastFan: -9, lastJoin: -9, down: Infinity, dmg: new Float32Array(n + 1) }));
  const pos = (j, s) => {
    const c = people[j];
    if (!c.walks) return world({ x: c.along, z: c.from });
    return world({ x: c.along, z: p.walker ? lerp(c.from, c.to, clamp(Math.min(s, c.down) / WalkTime)) : .5 });
  };
  const hurt = (j, amount, s) => { const c = people[j]; c.total += amount; if (c.total >= c.tough && c.down === Infinity) c.down = s; };
  // The next target: an enemy standing, in range, with no wall in the way, needing the smallest turn.
  const pick = (s, aim) => {
    const prism = tipOf(caster, aim);
    let best = -1, turn = Infinity;
    people.forEach((c, j) => {
      if (c.ally || c.down <= s) return;
      const q = pos(j, s), d = Math.hypot(q.x - prism.x, q.z - prism.z), ang = Math.atan2(q.z - prism.z, q.x - prism.x);
      if (d > p.range || rayWall(prism, ang, cells, d) < d - .01) return;
      const t = Math.abs(wrap(ang - aim));
      if (t < turn) { turn = t; best = j; }
    });
    return best;
  };
  let release = dry, target = 0, aim = Math.atan2(pos(0, 0).z - caster.z, pos(0, 0).x - caster.x);
  const aims = new Float32Array(n + 1), targets = new Int8Array(n + 1), downs = [];
  for (let k = 0; k <= n; k++) {
    const s = k * dt;
    if (s < release && people[target].down <= s) {
      const next = pick(s, aim);
      if (next < 0) release = s; else { downs.push({ t: s, from: target, to: next }); target = next; }
    }
    const tq = pos(target, s), step = Turn * D2R * dt;
    aim += Math.max(-step, Math.min(step, wrap(Math.atan2(tq.z - caster.z, tq.x - caster.x) - aim)));
    aims[k] = aim; targets[k] = target;
    if (s >= lead && s < release) {
      const prism = tipOf(caster, aim);
      if (s < join) {
        const beams = fanBeams(s, p, lead, aim, prism, cells);
        people.forEach((c, j) => {
          if (c.down <= s || s - c.lastFan < FanEvery) return;
          const q = pos(j, s);
          if (beams.some(b => onLine(q, b.start, b.ang, b.len, HitReach))) { c.lastFan = s; hurt(j, FanHit, s); }
        });
      } else {
        const L = rayWall(prism, aim, cells, p.range);
        people.forEach((c, j) => {
          if (c.down <= s || s - c.lastJoin < JoinEvery || !onLine(pos(j, s), prism, aim, L, p.width / 2)) return;
          c.lastJoin = s; hurt(j, JoinHit, s);
        });
      }
    }
    people.forEach(c => { c.dmg[k] = c.total; });
  }
  const value = { aims, targets, dt, lead, join, release, dry, dried: release >= dry, people, pos, caster, cells, downs: downs.filter(d => d.t < release) };
  cached = { key, value };
  return value;
}

function times(p) {
  if (!shoots(p)) return { end: ShowTime };
  const r = replay(p);
  return { lead: r.lead, join: r.join, release: r.release, dried: r.dried, joins: r.join < r.release, end: r.release + (r.dried ? Sputter : Fade) + Tail };
}

// Turns the prism has rolled about its long axis: still until the channel, then SpinStart a second rising
// linearly to SpinFull as the beams narrow (Terraria's sprite frames go from every 4 ticks to every 2),
// SpinFull while joined, winding down after the beam stops.
function roll(s, t, p) {
  if (!t.lead || s < t.lead) return 0;
  const J = p.join, A = SpinStart, B = SpinFull, h = Math.min(s, t.release);
  const until = x => { const u = clamp((x - t.lead) / J); return x <= t.join ? A * (x - t.lead) + (B - A) * J * u * u / 2 : A * J + (B - A) * J / 2 + B * (x - t.join); };
  let turns = until(h);
  if (s > t.release) { const v = A + (B - A) * clamp((t.release - t.lead) / J); turns += v * (1 - Math.exp(-SpinStop * (s - t.release))) / SpinStop; }
  return turns;
}

// A soft beam layer from a along ang: rounded at the start, tapered over softEnd cells at the end,
// with a ripple running out along it. off(d) shifts it across the line d cells out.
function ray(key, a, ang, len, half, colour, layer, softEnd, i, s, off = null) {
  if (colour.a <= .002 || len <= .05) return;
  const dx = Math.cos(ang), dz = Math.sin(ang), n = Math.max(2, Math.ceil(len / .4)), L = [], R = [];
  for (let k = 0; k <= n; k++) {
    const d = len * k / n, w = half * Math.sqrt(clamp(d / .2) * clamp((len - d) / softEnd)) * (1 + .07 * Math.sin(d * 1.7 - s * 26 + i * 1.3)) + .003;
    const shift = off ? off(d) : 0, x = a.x + dx * d - dz * shift, z = a.z + dz * d + dx * shift;
    L.push({ x: x - dz * w, z: z + dx * w }); R.push({ x: x + dz * w, z: z - dx * w });
  }
  strip(key, L, R, colour, whiteGlow, layer);
}
const around = (c, ang, d) => ({ x: c.x + Math.cos(ang) * d, z: c.z + Math.sin(ang) * d });

// The prism: a pyramid lying level at chest height, base centre at base (a screen point), tip PrismLen along
// the aim, rolled about that long axis by turns. Its five corners are placed in 3D (east, up, north) and
// drawn with up as a shift north. A face shows when its outward normal points at the viewer, (0, 1, -Lift):
// those faces never overlap, so no sorting. The three long faces keep the sprite's colours (pale, lavender,
// slate blue) and are lit by the sun, so as it rolls the colours slide across the top as they do across the
// sprite. While firing (power 0..1) they shimmer pink and green. Ridges between two shown faces are pale,
// the outline is navy.
function prism(base, aim, turns, sun, layer, dull, power, s) {
  const ca = Math.cos(aim), sa = Math.sin(aim);
  const at3 = (along, across, up) => ({ x: base.x + along * ca - across * sa, y: up, z: base.z + along * sa + across * ca });
  const v = [0, 1, 2].map(k => { const a = turns * TAU + k * TAU / 3; return at3(0, PrismRad * Math.cos(a), PrismRad * Math.sin(a)); });
  v.push(at3(PrismLen, 0, 0));
  const mid = { x: (v[0].x + v[1].x + v[2].x + v[3].x) / 4, y: (v[0].y + v[1].y + v[2].y + v[3].y) / 4, z: (v[0].z + v[1].z + v[2].z + v[3].z) / 4 };
  const screen = q => ({ x: q.x, z: q.z + q.y * Lift });
  const sub = (a, b) => ({ x: a.x - b.x, y: a.y - b.y, z: a.z - b.z });
  const sl = Math.hypot(sun.x, 1, sun.z), toSun = { x: -sun.x / sl, y: 1 / sl, z: -sun.z / sl };
  const faces = [[3, 0, 1, FacePale], [3, 1, 2, FaceLavender], [3, 2, 0, FaceSlate], [0, 2, 1, FaceBase]];
  const edges = new Map();
  faces.forEach(([i, j, k, base], f) => {
    const e1 = sub(v[j], v[i]), e2 = sub(v[k], v[i]);
    let n = { x: e1.y * e2.z - e1.z * e2.y, y: e1.z * e2.x - e1.x * e2.z, z: e1.x * e2.y - e1.y * e2.x };
    const c = { x: (v[i].x + v[j].x + v[k].x) / 3 - mid.x, y: (v[i].y + v[j].y + v[k].y) / 3 - mid.y, z: (v[i].z + v[j].z + v[k].z) / 3 - mid.z };
    if (n.x * c.x + n.y * c.y + n.z * c.z < 0) n = { x: -n.x, y: -n.y, z: -n.z };
    if (n.y - Lift * n.z <= 0) return;
    const nl = Math.hypot(n.x, n.y, n.z), lit = Math.max(0, (n.x * toSun.x + n.y * toSun.y + n.z * toSun.z) / nl);
    let colour = Color.Lerp(FaceDark, base, .45 + .55 * lit);
    if (power > 0) colour = Color.Lerp(colour, hue(s * .9 + f / 3, .5), .3 * power);
    if (dull) colour = Color.Lerp(colour, Dull, .7);
    const pts = [v[i], v[j], v[k]].map(screen), m = mesh(`last prism face ${f}`);
    m.setFlat(pts.flatMap(q => [q.x, q.z]), [0, 1, 2]);
    draw(m, 0, layer + f * .0003, 0, 1, 1, 0, colour.withAlpha(.94), flat);
    [[i, j], [j, k], [k, i]].forEach(([a, b]) => { const key = a < b ? `${a}${b}` : `${b}${a}`; edges.set(key, (edges.get(key) ?? 0) + 1); });
  });
  if (!dull) {
    const c = screen(mid);
    sprite(c, .5, .5, hue(s * .3, .6).withAlpha(.1 + .25 * power), glow, layer + .0015);
    sprite(c, .18 + .12 * power, .18 + .12 * power, White.withAlpha(.15 + .35 * power), glow, layer + .0016);
  }
  edges.forEach((count, key) => {
    const a = screen(v[+key[0]]), b = screen(v[+key[1]]), ridge = count > 1;
    rect(`last prism edge ${key}`, { x: (a.x + b.x) / 2, z: (a.z + b.z) / 2 }, Math.hypot(b.x - a.x, b.z - a.z) + .02, ridge ? .02 : .03,
      Math.atan2(b.z - a.z, b.x - a.x) / D2R, (ridge ? (dull ? Dull : CrystalEdge) : Navy).withAlpha(ridge ? .8 : .9), layer + .002 + (ridge ? 0 : .0005));
  });
}
// The spectrum the prism throws on the floor in sun: six stripes red to violet, along the sun.
function rainbow(c, sun, amount, s) {
  if (amount <= 0) return;
  const l = Math.hypot(sun.x, sun.z) || 1, ux = sun.x / l, uz = sun.z / l, deg = Math.atan2(uz, ux) / D2R;
  for (let k = 0; k < Beams; k++) {
    const off = (k - 2.5) * .07 + .012 * Math.sin(s * 2.4 + k);
    sprite({ x: c.x - uz * off + ux * .3, z: c.z + ux * off + uz * .3 }, .9, .11, hue(k / Beams, .9).withAlpha(.5 * amount), glow, Floor + .015 + k * .0005, -deg);
  }
}
// The gizmo's meter, drawn over the head: 12 segments, one per second of beam.
function meter(head, level, warn) {
  const pitch = .095, w = .075, h = .1, x0 = head.x - pitch * (Store - 1) / 2, z = head.z + .28;
  if (warn > 0) sprite({ x: head.x, z }, pitch * Store + .12, h + .12, Warn.withAlpha(.8 * warn), flat, Y + .3);
  sprite({ x: head.x, z }, pitch * Store + .05, h + .05, Body.withAlpha(.8), flat, Y + .301);
  for (let k = 0; k < Store; k++) {
    const x = x0 + k * pitch, fill = clamp(level - k);
    sprite({ x, z }, w, h, SunDim, flat, Y + .302);
    if (fill > 0) sprite({ x: x - w / 2 + w * fill / 2, z }, w * fill, h, Sun, flat, Y + .303);
  }
}

export default {
  kit: 'Last Prism', label: 'Last Prism (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'fires', options: ['fires', 'runs dry', 'charges in the sun', 'under a roof'], group: 'Showcase' },
    aim: P('Aim (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    walker: { label: 'The first target walks across the aim', value: true, group: 'Showcase' },
    actors: { label: 'Show stand-ins (pawns, walls, roof, damage bars)', value: true, group: 'Showcase' },
    range: P('Range (cells)', 22, 10, 40, 1, 'Shape'),
    fan: P('Fan, each side of the aim (degrees)', 35, 10, 60, 1, 'Shape'),
    width: P('Joined beam width, the lane that is hit (cells)', 1, .5, 2, .1, 'Shape'),
    join: P('Beams join after', 3, 1, 5, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    if (p.scenario === 'charges in the sun') return [{ name: 'In the sun (time x20)', t: 0 }];
    if (p.scenario === 'under a roof') return [{ name: 'Under a roof: no charge', t: 0 }];
    const t = times(p), r = replay(p);
    return [{ name: 'Hold', t: 0 }, { name: 'Fan (channel)', t: t.lead }, ...(t.joins ? [{ name: 'Beams join', t: t.join }] : []),
      ...r.downs.map((d, k) => ({ name: `Down ${k + 1}, beam swings`, t: d.t })),
      { name: t.dried ? 'Charge runs out' : 'No target left', t: t.release }];
  },
  events(p) {
    const t = times(p);
    return shoots(p) && t.joins ? [{ t: t.join, type: 'shake', value: .05 }] : [];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32, light = clamp(strength / .32);
    const firing = shoots(p), roofed = p.scenario === 'under a roof';
    const r = firing ? replay(p) : null, k = r ? Math.min(r.aims.length - 1, Math.round(s / r.dt)) : 0;
    const caster = r ? add(o, r.caster) : o, cells = r ? r.cells.map(c => add(o, c)) : [];
    const theta = r ? r.aims[k] : p.aim * D2R;
    const dx = Math.cos(theta), dz = Math.sin(theta), north = Math.sin(theta) > .3;
    const bob = .025 * Math.sin(s * 2.4);
    const lifted = q => ({ x: q.x, z: q.z + ChestLift + bob });
    const baseG = { x: caster.x + dx * PrismGap, z: caster.z + dz * PrismGap }, midG = { x: baseG.x + dx * PrismLen / 2, z: baseG.z + dz * PrismLen / 2 };
    const tipG = tipOf(caster, theta), tipS = lifted(tipG), midS = lifted(midG);
    // Beams, then the glow round the tip, then the prism, so the beams come out of its tip. Aiming north all three sit
    // under the pawn layer so they pass behind the wielder's head; glints (Y + .06) stay on top.
    const beamLayer = north ? AltitudeLayer.Projectile.AltitudeFor() : Y, glowLayer = beamLayer + .035, prismLayer = beamLayer + .04;

    // State of the channel at this time.
    const u = firing ? clamp((s - t.lead) / p.join) : 0, after = firing ? s - t.release : -1, endFade = t.dried ? Sputter : Fade;
    const flicker = t.dried && after > 0 ? (rand(Math.floor(s * 25) + 7) > .4 ? 1 : .15) : 1;
    const live = !firing || s < t.lead ? 0 : (after < 0 ? 1 : Math.max(0, 1 - after / endFade) * flicker) * clamp((s - t.lead) / .15);
    const shrink = after > 0 ? 1 - .5 * clamp(after / endFade) : 1, joined = firing && t.joins && s >= t.join;
    const opacity = u <= .66 ? lerp(.2, .45, u / .66) : lerp(.45, 1, (u - .66) / .34);
    const beams = live > 0 ? fanBeams(s, p, t.lead, theta, tipG, cells).map(b => ({ ...b, colour: hue(b.i / Beams + s * .12) })) : [];
    const lane = joined ? rayWall(tipG, theta, cells, p.range) : 0, laneBlocked = joined && lane < p.range - 1e-6;

    // --- the floor: roof overlay, the rainbow in sun, the target ring --------------------------------------------------
    if (roofed && p.actors) for (let ix = -3; ix <= 3; ix++) for (let iz = -2; iz <= 2; iz++)
      sprite({ x: o.x + ix, z: o.z + iz }, .92, .92, RoofTint.withAlpha(.2), flat, Y + .25);
    const fall = { x: midG.x + sun.x * PrismH, z: midG.z + sun.z * PrismH };
    sprite(fall, PrismLen * 1.2, PrismRad * 1.5, Body.withAlpha(strength * .35), soft, shadowLayer, -theta / D2R);
    if (!roofed && live === 0) rainbow(fall, sun, light, s);
    if (firing && s >= t.lead && s < t.release) ringAt(add(o, r.pos(r.targets[k], s)), .5, Warn.withAlpha(.7), Floor + .02);

    if (firing && p.actors) walls('last prism wall', o, cells, sun, strength);

    // --- pawns, north first -------------------------------------------------------------------------------------------
    const people = firing ? r.people.map((c, j) => {
      const pos = add(o, r.pos(j, s)), down = s >= c.down;
      const crossed = down || joined ? [] : beams.filter(b => onLine(pos, b.start, b.ang, b.len, HitReach));
      const inLane = !down && joined && live > 0 && onLine(pos, tipG, theta, lane, p.width / 2);
      return { c, pos, down, crossed, inLane, j, share: c.dmg[k] / c.tough };
    }) : [];
    const figures = [...people, { pos: caster, caster: true }].sort((m, n) => n.pos.z - m.pos.z);
    figures.forEach(g => {
      if (g.caster) {
        if (p.actors) pawn(g.pos, { shirt: Wielder, sun, shadow: strength });
        prism(lifted(baseG), theta, roll(s, t, p), sun, prismLayer, roofed, joined ? live : u * live, s);
        return;
      }
      if (!p.actors) return;
      const heat = g.inLane ? .75 + .2 * Math.sin(s * 40) : g.crossed.length ? .3 : 0;
      const shirt = g.c.mech ? MechGrey : g.c.ally ? Ally : Enemy, skin = g.c.mech ? MechHead : Skin;
      pawn(g.pos, { body: g.c.mech ? 'hulk' : 'average', shirt: Color.Lerp(shirt, White, heat), skin: Color.Lerp(skin, White, heat), sun, shadow: strength, downed: g.down });
      if (!g.down) damageBar(at(g.pos, 'headTop', { body: g.c.mech ? 'hulk' : 'average' }), g.share);
    });

    // --- the prism: glints in sun, the glow as it charges ---------------------------------------------------------------
    if (!roofed && live === 0) {
      const v = (s * .8) % 1;
      glint('last prism glint', { x: midS.x, z: midS.z + PrismRad * Lift * .8 }, .22, .9 * bump(v / .25) * light, White, 20);
    }
    if (live > 0) {
      const size = .5 + .9 * u;
      sprite(tipS, size, size, hue(s * .5).withAlpha((.35 + .4 * u) * live), glow, glowLayer);
      sprite(tipS, size * .45, size * .45, White.withAlpha((.4 + .5 * u) * live), glow, glowLayer + .001);
    }

    // --- the fan -------------------------------------------------------------------------------------------------------
    if (!joined) beams.forEach(b => {
      const key = `last prism beam ${b.i}`, al = opacity * live, wf = lerp(.45, 1, u) * shrink, end = b.blocked ? .15 : 1.5;
      const a = { x: b.start.x, z: b.start.z + ChestLift + bob }, bx = Math.cos(b.ang), bz = Math.sin(b.ang);
      const pt = d => ({ x: a.x + bx * d, z: a.z + bz * d });
      sprite({ x: b.start.x + bx * b.len / 2, z: b.start.z + bz * b.len / 2 }, b.len, .6 * wf, b.colour.withAlpha(.08 * al), glow, Floor + .01 + b.i * .0005, -b.ang / D2R);
      ray(`${key} outer`, a, b.ang, b.len, FanHalf * wf, b.colour.withAlpha(.24 * al), beamLayer + .01, end, b.i, s);
      ray(`${key} mid`, a, b.ang, b.len, FanHalf * .5 * wf, pale(b.colour).withAlpha(.45 * al), beamLayer + .011, end, b.i, s);
      ray(`${key} core`, a, b.ang, b.len, FanHalf * .16 * wf, White.withAlpha(.75 * al), beamLayer + .012, end, b.i, s);
      sprite(a, .32, .32, b.colour.withAlpha(.5 * al), glow, beamLayer + .013);
      for (let f = 0; f < 2; f++) {
        const d = (s * 16 + rand(b.i * 7 + f) * b.len) % b.len;
        if (d > .5 && d < b.len - 1.2) streak(`${key} flow ${f}`, pt(d), pt(d + .9), .05 * wf, White.withAlpha(.55 * al), whiteGlow, beamLayer + .013, 3);
      }
      if (b.blocked) {
        const e = pt(b.len);
        sprite(e, .7, .7, b.colour.withAlpha(.6 * al), glow, Y + .014);
        sprite(e, .22, .22, White.withAlpha(.85 * al), glow, Y + .015);
        for (let q = 0; q < 3; q++) {
          const v = (s * 5 + rand(b.i * 5 + q)) % 1, back = b.ang + Math.PI + (rand(b.i * 5 + q + 50) - .5) * 2.2, r0 = .1 + v * .6;
          streak(`${key} spark ${q}`, around(e, back, r0), around(e, back, r0 + .25), .04, pale(b.colour).withAlpha((1 - v) * al), whiteGlow, Y + .016, 3);
        }
      }
    });

    // --- the joined beam ---------------------------------------------------------------------------------------------------
    if (joined && live > 0) {
      const inF = clamp((s - t.join) / .15) * live, W = p.width * shrink, L = lane, end = laneBlocked ? .2 : 1.8, tint = hue(s * .25, .55);
      const a = tipS, pt = (d, across = 0) => ({ x: a.x + dx * d - dz * across, z: a.z + dz * d + dx * across });
      const mid = { x: tipG.x + dx * L / 2, z: tipG.z + dz * L / 2 };
      sprite(mid, L, W * 2.4, tint.withAlpha(.12 * inF), glow, Floor + .012, -theta / D2R);
      sprite(mid, L, W * 1.1, White.withAlpha(.06 * inF), glow, Floor + .013, -theta / D2R);
      ray('last prism fused glow', a, theta, L, W / 2 * 1.15, pale(tint).withAlpha(.1 * inF), beamLayer + .02, end, 0, s);
      // The six beams, joined: six bands side by side across the beam, red on one edge to violet on the other,
      // each wobbling a little on its own. They overlap into white in the middle and keep their colour at the edges.
      // (Terraria's joined beam is a white core with two drifting colour fringes; the user kept these bands, 2026-10-01.)
      for (let q = 0; q < Beams; q++) {
        const across = (q - 2.5) * W * .14;
        ray(`last prism band ${q}`, a, theta, L, W * .1, hue(q / Beams + .04 * Math.sin(s * .7), .95).withAlpha(.3 * inF), beamLayer + .021 + q * .0003, end, q, s,
          d => across + .025 * Math.sin(d * .8 - s * 10 + q * 1.7));
      }
      ray('last prism fused sheath', a, theta, L, W / 2 * .32, Crystal.withAlpha(.35 * inF), beamLayer + .024, end, 0, s);
      ray('last prism fused core', a, theta, L, W / 2 * .12, White.withAlpha(.9 * inF), beamLayer + .0245, end, 0, s);
      for (let f = 0; f < 10; f++) {
        const d = (s * 30 + rand(f + 11) * L) % L, across = (rand(f + 20) - .5) * W * .6;
        if (d > .6 && d < L - 1.6) streak(`last prism fused flow ${f}`, pt(d, across), pt(d + 1.4, across), .06, White.withAlpha(.5 * inF), whiteGlow, beamLayer + .027, 3);
      }
      for (let g = 0; g < 12; g++) {
        const v = (s * 1.3 + rand(g + 40)) % 1, d = .8 + rand(g + 41) * (L - 1.6), sideways = (rand(g + 42) > .5 ? 1 : -1) * (W * .3 + v * W * .6);
        glint(`last prism sparkle ${g}`, pt(d, sideways), .06 + .1 * (1 - v), .85 * bump(v) * inF, hue(rand(g) + s * .2, .6), 45 * v);
      }
      const pulse = (s - t.join) / .2;
      if (pulse < 1) sprite(pt(L * pulse), W * 2.5, W * 2.5, White.withAlpha(.8 * (1 - pulse)), glow, beamLayer + .028);
      if (laneBlocked) {
        const e = pt(L);
        sprite(e, W * 1.8, W * 1.8, tint.withAlpha(.6 * inF), glow, Y + .03);
        sprite(e, W * .7, W * .7, White.withAlpha(.85 * inF), glow, Y + .031);
      }
      const flare = 1 + .08 * Math.sin(s * 47);
      sprite(tipS, W * 1.8 * flare, W * 1.8 * flare, tint.withAlpha(.45 * inF), glow, glowLayer + .002);
      sprite(tipS, W * .8, W * .8, White.withAlpha(.85 * inF), glow, glowLayer + .003);
      glint('last prism flare', tipS, .5, .6 * inF, White, s * 200);
    }
    if (firing && t.joins && s >= t.join && s < t.join + JoinFlash) {
      const v = (s - t.join) / JoinFlash;
      sprite(tipS, 3.2, 3.2, White.withAlpha(.7 * (1 - v) * (1 - v)), glow, Y + .2);
      ringAt(tipS, .25 + 1.8 * smooth(v), White.withAlpha(.8 * (1 - v)), Y + .21, false, whiteGlow);
      ringAt(tipS, .15 + 1.2 * smooth(v), hue(s * .25).withAlpha(.7 * (1 - v)), Y + .211, false, whiteGlow);
    }

    // --- hits on pawns: a flicker in each crossing beam's colour, white and sparks in the joined beam, smoke once down -------
    people.forEach(g => {
      const chest = at(g.pos, 'chest'), f = Math.floor(s * 20);
      g.crossed.forEach((b, q) => {
        sprite(chest, .45, .45, b.colour.withAlpha(.65 * live * (rand(f + b.i * 13) > .3 ? 1 : .4)), glow, Y + .04 + q * .001);
        const ang = rand(f * 3 + b.i) * TAU;
        streak(`last prism graze ${g.j} ${b.i}`, around(chest, ang, .12), around(chest, ang, .38), .04, pale(b.colour).withAlpha(.8 * live), whiteGlow, Y + .045, 3);
      });
      if (g.inLane) {
        sprite(chest, .8, .8, White.withAlpha(.55 * live), glow, Y + .05);
        ringAt(chest, .25 + ((s * 3) % 1) * .5, White.withAlpha(.7 * (1 - (s * 3) % 1) * live), Y + .051, false, whiteGlow);
        for (let q = 0; q < 5; q++) {
          const v = (s * 4 + rand(q + 60)) % 1, ang = theta + (rand(q + 61) - .5) * 1.6 + (q % 2 ? .9 : -.9), r0 = .2 + v * .7;
          streak(`last prism burn ${g.j} ${q}`, around(chest, ang, r0), around(chest, ang, r0 + .3), .05, hue(rand(q) + s * .3, .5).withAlpha((1 - v) * live), whiteGlow, Y + .052, 3);
        }
      }
      if (g.down) for (let q = 0; q < 5; q++) {
        const born = g.c.down + q * .25, v = (s - born) / 1.2;
        if (v < 0 || v > 1) continue;
        sprite({ x: g.pos.x + (rand(q + 70) - .5) * .4 + v * .2, z: g.pos.z + .1 + v * .8 }, .35 + v * .5, .3 + v * .4, Smoke.withAlpha(.35 * bump(v)), soft, Y + .004);
      }
    });

    // --- the meter (the gizmo's stand-in) and the charging flash ------------------------------------------------------------
    let level = StartLevel, warn = 0;
    if (firing) {
      level = Math.max(0, (p.scenario === 'runs dry' ? DryStore : Store) - Math.max(0, Math.min(s - t.lead, t.release - t.lead)));
      if (t.dried && after > 0 && after < 1.2) warn = Math.floor(after * 6) % 2 ? 1 : .25;
    } else if (!roofed) {
      level = Math.min(Store, StartLevel + s * light);
      const done = level % 1;
      if (s > .1 && level < Store && done < .15) sprite(midS, 1.2, 1.2, Sun.withAlpha(.55 * (1 - done / .15) * light), glow, glowLayer + .004);
    }
    meter(at(caster, 'headTop'), level, warn);
  },
};
