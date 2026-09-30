// Magic Bullet — E.G.O. weapon proposal, not the game. Nothing in Source/RimArt draws this yet.
// Der Freischütz (Lobotomy Corporation, HE), the rifle whose seven bullets come from the devil.
// The rules are docs/ego-weapons.md, "Weapon 1: Magic Bullet" (design agreed 2026-09-30, numbers
// are placeholders and become XML fields on CompProperties_EgoWeapon):
//   Piercing line: every shot flies a straight line through walls and hits every pawn on it, allies
//         included; never misses, ignores cover. Range 40, 18 damage.
//   The seventh: the count sits on the gun and survives reloads. The seventh shot cannot be aimed:
//         the gun turns itself to the pawn on the map the shooter has the highest opinion of (spouse,
//         lover, friend; a bonded animal; else the shooter) and still pierces everyone between.
//         30 damage. Count resets after. 20 s cooldown before shot one.
//   Gizmo: a counter showing the next shot number; on six it names the seventh's target.
//   Corrosion (shared system): only the look is sketched here (the "Corroded look" checkbox). The
//         corroded targeting rule (nearest pawn, any faction) is not shown.
//
// Look, from the source. Second pass 2026-09-30 against seven frames of the Limbus "Magic Bullet
// Fire" skill (youtube P3Kq4RHdDE0: aim, bullet at the muzzle, shot 1, hit, a two-circle aim, the
// big shot, its hit). What each frame shows and what the sketch does with it:
//   Aim:   one big translucent blue magic circle (about the pawn's height across) stands about a
//          cell in front of the muzzle: an outer double ring, a ring of rune ticks, a hexagram,
//          an inner ring and a centre sigil, all thin bright blue lines over a soft blue fill.
//          Five or six blue sparks drift up around the muzzle and the shooter's head. Just before
//          the shot the circle slides back to the muzzle and the bullet sits in it.
//   Shot 1-3: a thin dim blue line with big white-violet lightning forks branching a cell off it.
//          Two tall tan parchment brackets stand either side of the circle (the left about 1.6
//          pawn heights, the right smaller), a tan slab stands at the target, and a tan brush
//          sweep runs along the floor from the shooter's feet. Short cyan speed lines fly past the
//          circle. The rifle kicks up about 30 degrees, held one-handed.
//   Hit:   a yellow spiked stagger burst at the chest, an orange lightning bolt beside it, orange
//          fire. The brackets and slab thin to tan oval outlines and stay a second.
//   Count 4-6: two or more circles stacked along the aim .48 cells apart, each 45 % bigger than the
//          one before (the source's second circle is about 1.6x the first), the beam
//          wider and blue-violet.
//   The seventh: a cyan-white beam several times wider with a wide pale halo, a cyan burst at the
//          circle, bigger forks, and orange streak lines flying on past the target.
//   Rifle: about nine times longer than wide (the icon is 200 x 23): black barrel, a gold filigree
//          band around the chamber, a navy stock.
//   Corroded: Der Freischütz himself bleeds through: the body a black smoke silhouette with
//          tendrils off the head and the legs trailing into smoke, two glowing blue eyes, a navy
//          cape with gold trim over the shoulders. No hat.
//
// Order (default sliders, shot 1..6):
//   0.00  aim: rifle level at chest height on the aim; seven pips over the head show the count;
//         the circle opens a cell ahead of the muzzle over the first 40 % of the aim time, then
//         slides back to .4 cells ahead; blue sparks drift up; the chamber glows
//   0.45  fire: flash, the rifle kicks up 30 degrees and settles over .45 s; the bullet flies at
//         120 cells/s out through the circle; the brackets, slab and floor sweep appear in .08 s;
//         the beam is left from the muzzle to the range with forks along it; it crosses the wall
//         (a punched hole and dust that stay) and every pawn on the line (the raider in front of
//         the wall, the colonist behind it): stagger star, orange bolt, fire, blood, a floor
//         spatter that stays; each hit pawn flinches .18 cells
//   0.80  the bullet has reached the 40-cell range; each 2-cell piece of the beam fades over .6 s
//         from when the bullet passed it; the circle closes; the parchment thins to outlines over
//         .5 s and fades by 1.4 s; the result is held 1.5 s
// Shot 7: at 0.45 the rifle swings from the aim to the beloved over .35 s (the shooter's arms turn
//   it, not the player), five circles open on the new line, then it fires the wide beam the same
//   way. The raider standing between takes the hit first, then the beloved. After shot 6 (and
//   before 7) a red dashed floor line runs from the shooter to the beloved: the gizmo's warning,
//   drawn on the map so the sketch can show it (UI, not the weapon, so it keeps its red).
//
// Drawing: the bullet flies level at chest height (lib/pawn.js chest = .05 north of the cell centre),
// so the beam is the ground line lifted north by that: every part of it is a level line, quad or
// sprite and needs no per-facing method. The magic circles are vertical gates in the source; a gate
// facing the aim collapses to a line for east and west. The "Circle" dropdown picks the treatment:
// "faces the aim" (default) is the true gate, a circle across the aim and up, its top leaned back
// toward the shooter by an angle solved per aim so the face on screen is always the same size:
// about 2 degrees facing south, 40 facing east or west, 64 facing north; "faces the viewer"
// is a circle in the (east, up) plane at every aim, a 1 : .6 ellipse with a fixed screen
// orientation like Twin Maw's jaws; "lies flat" is a level ring. Up is drawn as .6 north. Standing
// ones get a ground shadow along the sun, a lit top rim and a dark back rim .04 north for thickness.
// The parchment brackets are tall and thin, so they get the projection's free axis: they stand in
// the plane of (u, up) where u is the aim when the aim is more east-west than north-south, else the
// across direction; up is drawn as .6 north. Facing east or west that is the source's side view;
// facing north or south the pair flanks the beam. The rifle's kick is a real tilt: the muzzle end
// goes cos(tilt) along the aim and Lift x sin(tilt) north, so facing east or west the barrel visibly
// swings up, facing north or south it shortens. Pawns are lib/pawn.js real-size stand-ins (average
// body); walls are the Paper Bomb kit's stand-ins. Hits are found every frame from the params: a
// pawn is hit when it stands within .45 cells of the line, a wall cell when the line passes within
// .5 of its centre.
import { Color, Mathf, Meshes, MaterialPool, ShaderDatabase } from '../js/engine.js';
import { P, Body, Y, Floor, Lift, sprite, band, circle, soft, glow, rand } from './lib/six-paths-impact.js';
import { draw } from './lib/six-paths-solid.js';
import { pawn, at, pawnLayer, shadowLayer } from './lib/pawn.js';
import { rect, puff, Blood, Enemy, Holder, Ally, Dust } from './lib/chain-sickle.js';
import { line, streak, glint, whiteGlow } from './lib/goku.js';
import { walls, WallTop } from './lib/paper-bomb.js';

const clamp = Mathf.Clamp01, smooth = Mathf.Smooth, D2R = Mathf.Deg2Rad, TAU = Math.PI * 2;
const disc = Meshes.disc(32, 'magic bullet disc');
const ring = Meshes.band(.965, 1, 64, 'magic bullet ring');   // drawn with sx != sz for the standing ellipse
const flat = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);   // strips have no uv: white texture only
const bump = x => (x >= 0 && x <= 1) ? Math.sin(x * Math.PI) : 0;
const dirOf = deg => ({ x: Math.cos(deg * D2R), z: Math.sin(deg * D2R) });
const move = (q, d, k) => ({ x: q.x + d.x * k, z: q.z + d.z * k });
const side = d => ({ x: -d.z, z: d.x });

// Palette, from the frames: the circle bright blue over a deep blue fill, the beam blue, its forks
// white over violet, the seventh cyan-white, the parchment tan, the hit yellow and orange.
const White = new Color(1, 1, 1), Beam = new Color(.55, .80, 1), BeamEdge = new Color(.20, .45, 1);
const Circle = new Color(.35, .62, 1), CircleBright = new Color(.50, .78, 1), CircleDeep = new Color(.12, .22, .80);
const Cyan = new Color(.55, .96, 1), Halo = new Color(.65, .85, 1), Violet = new Color(.72, .62, 1);
const Barrel = new Color(.07, .07, .09), BarrelLit = new Color(.30, .31, .36), Gold = new Color(.86, .68, .26), Navy = new Color(.11, .14, .38);
const BeamViolet = new Color(.55, .45, 1), Tan = new Color(.82, .74, .56), TanDark = new Color(.62, .54, .38);
const Smoke = new Color(.04, .03, .05), Eye = new Color(.45, .75, 1), Fire = new Color(1, .50, .12), FireCore = new Color(1, .90, .50);
const Stagger = new Color(1, .85, .25), Bolt = new Color(1, .62, .18);
const Warn = new Color(.75, .16, .10), Heart = new Color(.95, .45, .55);
// Decided looks and the rule's fixed numbers.
const Speed = 120;                    // cells/s: the shot is near-instant (40 cells in .33 s); the beam is what the player sees
const Chunk = 2;                      // the beam fades in 2-cell pieces from when the bullet passed each
const Swing = .35, Tail = .5;
const Flinch = .18, Settle = .08;
const BeamLife = .6, CircleClose = .25;
const KickTilt = 30 * D2R, KickUp = .06, KickSettle = .45;   // the barrel swings up 30 degrees in .06 s and comes down over .45 s
const PaperOpen = .08, PaperThin = .5, PaperLife = 1.4;      // the parchment: full in .08 s, thins to an outline over .5 s, gone by 1.4 s
const Shots = 7;
const CirclesFor = [1, 1, 2, 2, 3, 4, 5];   // Limbus: circles per Magic Bullet count
const CircleFar = 1.0, CircleNear = .4;     // the circle opens this far ahead of the muzzle and slides back to this before the shot
const HitWidth = .45;                 // a pawn this close to the line is on it
const RifleLen = 1.4, RifleW = .075;  // 9:1 like the icon; longer than the pawn is tall
const GripAlong = .1, MuzzleAlong = GripAlong + RifleLen * .72;
const ChestLift = .05;                // lib/pawn.js: the chest is .05 north of the cell centre on screen
const lift = q => ({ x: q.x, z: q.z + ChestLift });
// Where everyone stands, in the aim frame (along, across) from the shooter. The beloved stands off
// the aim so the seventh visibly turns away from it; the "between" raider is on that second line.
const Wall = { along: 4, halfWidth: 1 };          // three wall cells across the aim
const Raider = { along: 2.5, across: 0 };
const Colonist = { along: 6.5, across: 0 };
const BelovedDeg = 125, BelovedDist = 5.5, BetweenDist = 2.8;

function times(p) {
  const swing = p.shot >= Shots ? Swing : 0, fire = p.lead + swing;
  return { fire, flight: p.range / Speed, end: fire + p.range / Speed + p.hold + Tail };
}

// The line the shot takes, and everything it crosses, in order of distance from the muzzle.
function shotLine(p, o, seventhDeg) {
  const deg = p.shot >= Shots ? seventhDeg : p.aim, d = dirOf(deg);
  const start = move(o, d, MuzzleAlong);
  return { deg, d, start, end: move(start, d, p.range) };
}
function crossings(lineDef, people, wallCells) {
  const { start, d } = lineDef, out = [];
  const along = q => (q.x - start.x) * d.x + (q.z - start.z) * d.z;
  const across = q => Math.abs(-(q.x - start.x) * d.z + (q.z - start.z) * d.x);
  for (const w of wallCells) { const a = along(w); if (a > 0 && across(w) <= .5) out.push({ kind: 'wall', d: a, at: move(start, d, a), cell: w }); }
  for (const who of people) { const a = along(who.pos); if (a > 0 && across(who.pos) <= HitWidth) out.push({ kind: 'pawn', d: a, at: move(start, d, a), who }); }
  return out.sort((m, n) => m.d - n.d);
}
// The projection's free axis for tall thin things: the aim when it is more east-west than
// north-south, else the across direction. Always points east-ish so a pair reads the same way.
function freeAxis(d) {
  const s = side(d), u = Math.abs(d.x) >= Math.abs(s.x) ? d : s;
  return u.x < 0 ? { x: -u.x, z: -u.z } : u;
}

// The seven pips over the shooter's head. Filled ones are shots already taken; the next one is lit;
// pip seven is the big one. On six it pulses.
function counter(key, head, shot, s) {
  const pitch = .13, x0 = head.x - pitch * 3, z = head.z + .32;
  for (let i = 0; i < Shots; i++) {
    const n = i + 1, spent = n < shot, next = n === shot, last = n === Shots;
    const warm = next && last ? .5 + .5 * Math.sin(s * 9) : 0, r = last ? .06 : .045;
    draw(disc, x0 + i * pitch, Y + .2, z, r, r, 0, spent ? CircleDeep : CircleDeep.withAlpha(.25));
    if (next) sprite({ x: x0 + i * pitch, z }, .18, .18, Beam.withAlpha(.5 + .3 * warm), glow, Y + .201);
    if (!spent) draw(disc, x0 + i * pitch, Y + .202, z, r + .005, r + .005, 0, Beam.withAlpha(.5));
  }
}

// The rifle: a black barrel about nine times longer than wide, a gold filigree band around the
// chamber, a navy stock. Level at chest height along deg from the hand's ground point; kick slides
// it back, tilt (radians) swings the muzzle up: cos(tilt) along the aim, Lift x sin(tilt) north.
// Drawn above the pawn so it reads as held in front. The shadow stays flat along the aim.
function rifle(key, hand, deg, sun, strength, kick = 0, tilt = 0, layer = pawnLayer + .06) {
  const a = dirOf(deg), base = move(hand, a, -kick), q = lift(base), sd = { x: base.x + sun.x * .9, z: base.z + sun.z * .9 };
  const dv = { x: a.x * Math.cos(tilt), z: a.z * Math.cos(tilt) + Lift * Math.sin(tilt) };
  const len = Math.hypot(dv.x, dv.z), d = { x: dv.x / len, z: dv.z / len }, sdeg = Math.atan2(dv.z, dv.x) / D2R;
  const L = RifleLen * len;
  rect(`${key} shadow`, move(sd, a, RifleLen * .22), RifleLen * Math.cos(tilt), RifleW * 1.3, deg, Body.withAlpha(strength * .6), shadowLayer);
  rect(`${key} stock`, move(q, d, -L * .17), L * .28, RifleW * 1.15, sdeg, Navy, layer);
  rect(`${key} barrel`, move(q, d, L * .28), L * .88, RifleW * .6, sdeg, Barrel, layer + .002);
  rect(`${key} chamber`, move(q, d, L * .02), L * .16, RifleW, sdeg, Barrel, layer + .003);
  rect(`${key} gold`, move(q, d, L * .02), L * .13, RifleW * .55, sdeg, Gold, layer + .004);
  rect(`${key} gold cap`, move(q, d, L * .11), L * .02, RifleW * .95, sdeg, Gold, layer + .004);
  rect(`${key} lit`, { x: q.x + d.x * L * .3, z: q.z + d.z * L * .3 + .012 }, L * .8, .012, sdeg, BarrelLit.withAlpha(.8), layer + .005);
  return { chamber: move(q, d, L * .04), muzzle: move(q, d, L * .72), dir: d };
}

// Before the shot: a blue glow at the chamber (u 0..1) and five blue sparks drifting up around the
// muzzle and the shooter's head, as in the aim frames.
function chamberGlow(key, c, muzzle, head, u, s) {
  if (u <= 0) return;
  sprite(c, .18 + .22 * u, .16 + .2 * u, Circle.withAlpha(.6 * u), glow, Y + .07);
  sprite(c, .08 + .08 * u, .07 + .07 * u, White.withAlpha(.8 * u), glow, Y + .071);
  for (let i = 0; i < 6; i++) {
    const ph = (s * .9 + rand(i + 120)) % 1, home = i < 3 ? muzzle : head;
    const g = { x: home.x + (rand(i + 130) - .5) * 1.0, z: home.z + (rand(i + 140) - .5) * .5 + ph * .5 };
    const a = u * Math.sin(ph * Math.PI);
    sprite(g, .14, .12, Circle.withAlpha(.7 * a), glow, Y + .072);
    sprite(g, .06, .05, White.withAlpha(.9 * a), glow, Y + .073);
  }
}

// One magic circle, as the frames draw it: a soft blue fill and glow, an outer double ring, a ring
// of rune ticks, a hexagram, an inner ring and a centre sigil, all thin bright lines, spinning.
// Every point of it is c + H * r cos(t) + V * r sin(t): H and V are the circle's two axes on
// screen, so the same drawing serves a flat ring (H east, V north), a ring standing up and facing
// the viewer (V = north * Lift) and a gate facing the aim (H across the aim, V up leaned back
// toward the shooter for east and west, see gateAxes). Rings and the fill are strips built from
// those points. A standing one gets a ground shadow along the sun (hTrue is the real across
// direction), a dark back rim .04 north and a lit top rim.
function magicCircle(key, c, r, open, s, k, H, V, hTrue, stands, sun, strength) {
  if (open <= 0) return;
  const rr = r * open, a = Math.min(1, open * 1.5), spin = s * (k % 2 ? .9 : -.7) + k, N = 48;
  const on = (t, rad, q = c, h = H, v = V) => ({ x: q.x + h.x * rad * Math.cos(t) + v.x * rad * Math.sin(t), z: q.z + h.z * rad * Math.cos(t) + v.z * rad * Math.sin(t) });
  const ringAt = (name, q, rad, alpha, layer, colour, thick = .03) => {
    const inner = [], outer = [];
    for (let i = 0; i <= N; i++) { const t = i / N * TAU; inner.push(on(t, rad - thick, q)); outer.push(on(t, rad, q)); }
    band(`${key} ${name}`, inner, outer, colour.withAlpha(alpha), layer);
  };
  const discAt = (name, q, rad, alpha, layer, colour, h = H, v = V) => {
    const inner = [], outer = [];
    for (let i = 0; i <= N; i++) { const t = i / N * TAU; inner.push(q); outer.push(on(t, rad, q, h, v)); }
    band(`${key} ${name}`, inner, outer, colour.withAlpha(alpha), layer);
  };
  const gw = 3 * rr * Math.hypot(H.x, V.x), gh = 3 * rr * Math.hypot(H.z, V.z);   // the glow sprite's box around the ellipse
  if (stands) {
    discAt('shadow', { x: c.x + sun.x * .5, z: c.z - ChestLift + sun.z * .5 }, rr, .9 * strength * a, shadowLayer, Body, hTrue, { x: 0, z: .25 });
    const back = { x: c.x, z: c.z + .04 };
    ringAt('back', back, rr, .7 * a, Y + .0405, CircleDeep, .04);
    ringAt('back 2', back, rr * .80, .5 * a, Y + .0405, CircleDeep);
  }
  sprite(c, gw, gh, CircleDeep.withAlpha(.30 * a), glow, Y + .04);
  discAt('fill', c, rr * .98, .28 * a, Y + .0401, Circle);                  // the translucent fill
  ringAt('outer', c, rr, 1.0 * a, Y + .041, CircleBright, .035);
  ringAt('outer 2', c, rr * .955, .55 * a, Y + .041, Circle, .02);
  ringAt('rune', c, rr * .80, .9 * a, Y + .041, Circle);
  ringAt('mid', c, rr * .62, .8 * a, Y + .041, CircleBright);
  ringAt('inner', c, rr * .36, .8 * a, Y + .041, CircleBright);
  ringAt('inner 2', c, rr * .30, .5 * a, Y + .041, Circle, .02);
  if (stands) sprite(on(Math.PI / 2, rr * .88), gw * .4, rr * .3, White.withAlpha(.35 * a), glow, Y + .0411);   // the lit top rim
  const ticks = 36;                    // the rune ring between .80 and .955, every third tick long
  for (let i = 0; i < ticks; i++) {
    const t = spin + i / ticks * TAU, outer = rr * (i % 3 ? .90 : .955);
    streak(`${key} tick ${i}`, on(t, rr * .80), on(t, outer), .02, CircleBright.withAlpha(.6 * a), whiteGlow, Y + .042, 3);
  }
  for (let tri = 0; tri < 2; tri++) {  // the hexagram: two triangles inscribed in the .62 ring
    const pts = [];
    for (let i = 0; i <= 3; i++) pts.push(on(-spin * .5 + tri * Math.PI / 3 + i * TAU / 3 + Math.PI / 2, rr * .62));
    line(`${key} tri ${tri}`, pts, .025, CircleBright.withAlpha(.6 * a), whiteGlow, Y + .0415, 'none');
  }
  const sq = [];                       // the centre sigil: a square in the inner ring, a dot
  for (let i = 0; i <= 4; i++) sq.push(on(spin + i * TAU / 4 + Math.PI / 4, rr * .30));
  line(`${key} sigil`, sq, .022, CircleBright.withAlpha(.6 * a), whiteGlow, Y + .0415, 'none');
  sprite(c, gw * .1, gh * .1, Circle.withAlpha(.5 * a), glow, Y + .0425);
  sprite(c, gw * .035, gh * .035, White.withAlpha(.7 * a), glow, Y + .0426);
  for (let i = 0; i < 6; i++) {        // rune dots on the .71 ring, spinning the other way
    const g = on(-spin * .7 + i / 6 * TAU, rr * .71);
    sprite(g, .04, .036, White.withAlpha(.6 * a), glow, Y + .0425);
  }
  for (let i = 0; i < 4; i++) {        // white sparks around the rim
    const ph = (s * 1.7 + rand(i + 200 + k)) % 1, t = rand(i + 210 + k) * TAU + s * .8;
    sprite(on(t, rr * (1 + ph * .3)), .07, .06, White.withAlpha(a * (1 - ph)), glow, Y + .043);
  }
}
// The circle's screen axes for the chosen mode. A gate facing the aim has H across the aim and V
// up. For east and west H is pure north, which the projection would collapse, so the gate leans
// back: its top tilts toward the shooter. The lean angle is solved per aim so the ellipse's face
// on screen (|H x V|) is always GateFace, the value a 40-degree lean gives facing east: about 2
// degrees facing south, 40 facing east or west, 64 facing north (where leaning toward the shooter
// works against the lift, so it leans further). One formula for every aim, so nothing flips.
const GateFace = Math.sin(40 * D2R);
function gateAxes(mode, dir) {
  const h = side(dir);
  if (mode === 'lies flat') return { H: { x: 1, z: 0 }, V: { x: 0, z: 1 }, hTrue: { x: 1, z: 0 }, stands: false };
  if (mode === 'faces the viewer') return { H: { x: 1, z: 0 }, V: { x: 0, z: Lift }, hTrue: { x: 1, z: 0 }, stands: true };
  const k = Lift * dir.z;              // sin(lean) - k cos(lean) = GateFace, so lean = asin(GateFace / sqrt(1 + k^2)) + atan(k)
  const lean = Math.min(80 * D2R, Math.max(0, Math.asin(Math.min(1, GateFace / Math.hypot(1, k))) + Math.atan(k)));
  const cl = Math.cos(lean), sl = Math.sin(lean);
  return { H: h, V: { x: -sl * dir.x, z: Lift * cl - sl * dir.z }, hTrue: h, stands: true };
}

// A parchment stroke: a tall arc standing in the plane (u, up) with its middle gap cells from c
// on side sgn (+1 or -1), width W, half-height H (screen cells: up is already scaled), thick at
// the middle and pointed at the ends. Drawn as one strip. thin 0..1 hollows it to an outline.
function parchment(key, c, u, sgn, gap, W, H, thick, alpha, thin) {
  if (alpha <= 0) return;
  const inner = [], outer = [], n = 18;
  for (let i = 0; i <= n; i++) {
    const t = (-80 + 160 * i / n) * D2R, ct = Math.cos(t), st = Math.sin(t);
    const uo = sgn * (gap + W * ct), z = H * st, tk = Math.max(.02, thick * ct * ct * (1 - thin * .85));
    inner.push({ x: c.x + u.x * uo, z: c.z + u.z * uo + z });
    outer.push({ x: c.x + u.x * (uo + sgn * tk), z: c.z + u.z * (uo + sgn * tk) + z });
  }
  band(`${key} face`, inner, outer, Tan.withAlpha(alpha), Y + .030);
  band(`${key} edge`, outer.map((q, i) => ({ x: q.x + (q.x - inner[i].x) * .3, z: q.z + (q.z - inner[i].z) * .3 })), outer, TanDark.withAlpha(alpha * .8), Y + .0299);
}
// Its life: full in PaperOpen, thinning to an outline over PaperThin, gone by PaperLife.
function paperLife(age) {
  if (age < 0 || age > PaperLife) return null;
  return { open: smooth(age / PaperOpen), thin: smooth((age - PaperOpen - .15) / PaperThin), alpha: 1 - smooth((age - PaperLife + .4) / .4) };
}

// Der Freischütz bleeding through the corroded pawn: a black smoke silhouette over the body, smoke
// tendrils off the head and around the feet, two blue eyes, a navy cape with gold trim.
function abnormality(pos, who, sun, strength, s) {
  const head = at(pos, 'head', who), top = at(pos, 'headTop', who), neck = at(pos, 'neck', who), chest = at(pos, 'chest', who), feet = at(pos, 'feet', who);
  draw(disc, chest.x, pawnLayer + .01, chest.z - .17, .30, .45, 0, Smoke);                 // body
  draw(disc, head.x, pawnLayer + .011, head.z, .23, .24, 0, Smoke);                        // head
  for (let i = 0; i < 6; i++) {                                                             // tendrils off the head
    const ph = (s * .6 + rand(i + 300)) % 1, x = top.x + (rand(i + 310) - .5) * .3 + Math.sin(s * 2 + i) * .04 * ph;
    sprite({ x, z: top.z + .02 + ph * .6 }, .18 + ph * .26, .16 + ph * .24, Smoke.withAlpha(.95 * Math.sin(ph * Math.PI)), puff, pawnLayer + .04);
  }
  for (let i = 0; i < 5; i++) {                                                             // legs trailing into smoke
    const ph = (s * .5 + rand(i + 330)) % 1, x = feet.x + (rand(i + 340) - .5) * .5;
    sprite({ x, z: feet.z - .04 + ph * .1 }, .24 + ph * .26, .16 + ph * .12, Smoke.withAlpha(.9 * Math.sin(ph * Math.PI)), puff, pawnLayer + .015);
  }
  draw(disc, neck.x, pawnLayer + .02, neck.z - .09, .37, .21, 0, Gold);                    // cape: gold trim under navy
  draw(disc, neck.x, pawnLayer + .021, neck.z - .09, .34, .185, 0, Navy);
  draw(disc, neck.x, pawnLayer + .022, neck.z - .01, .16, .05, 0, Gold);                   // clasp
  for (const dx of [-.07, .07]) {                                                           // eyes
    sprite({ x: head.x + dx, z: head.z - .02 }, .1, .08, Eye.withAlpha(.9), glow, pawnLayer + .03);
    draw(disc, head.x + dx, pawnLayer + .031, head.z - .02, .022, .018, 0, White);
  }
  sprite({ x: head.x + sun.x * .3, z: head.z - .55 + sun.z * .3 }, .9, .35, Body.withAlpha(strength * .5), soft, shadowLayer);
}

// A zigzag bolt from a point: n bends, reaching `reach` in direction t (radians), jittered by seed.
function bolt(key, from, t, reach, n, seed, width, colour, glowColour, layer) {
  const pts = [from], dx = Math.cos(t), dz = Math.sin(t);
  for (let k = 1; k <= n; k++) {
    const u = k / n, off = (rand(seed + k * 7) - .5) * reach * .45 * (k < n ? 1 : .3);
    pts.push({ x: from.x + dx * reach * u - dz * off, z: from.z + dz * reach * u + dx * off });
  }
  line(`${key} core`, pts, width, colour, whiteGlow, layer + .001, 'end');
  line(`${key} glow`, pts, width * 2.8, glowColour, whiteGlow, layer, 'end');
}

// One pawn crossed by the line: a blue-white flash, a yellow spiked stagger burst, an orange bolt,
// orange fire for .6 s, blood thrown on past, a spatter that stays. heavy = the seventh, which
// also throws orange streak lines on past the target.
function wound(key, pos, who, d, age, heavy, s) {
  if (age < 0) return;
  const chest = at(pos, 'chest', who), k = heavy ? 1.2 : 1, aim = Math.atan2(d.z, d.x);
  if (age < .1) glint(`${key} flash`, chest, .55 * k, 1 - age / .1, Beam);
  if (age < .12) sprite(chest, .5 * k, .4 * k, Blood.withAlpha(.8 * (1 - age / .12)), soft, Y + .09);
  if (age < .3) {                      // the stagger burst: eight yellow spikes and a glow
    const u = age / .3, len = (.35 + .45 * Math.sqrt(u)) * k, fade = 1 - u * u;
    sprite(chest, .8 * k, .7 * k, Stagger.withAlpha(.7 * fade), glow, Y + .093);
    for (let i = 0; i < 8; i++) {
      const t = i / 8 * TAU + .3, l = len * (.6 + .4 * rand(i + 700));
      streak(`${key} spike ${i}`, chest, { x: chest.x + Math.cos(t) * l, z: chest.z + Math.sin(t) * l * .85 }, .07 * k, Stagger.withAlpha(.95 * fade), whiteGlow, Y + .094, 4);
    }
  }
  if (age < .35) {                     // an orange lightning bolt beside the hit, flickering
    const frame = Math.floor(s * 18);
    for (let i = 0; i < 2; i++) {
      if (rand(frame * 3 + i) < .3) continue;
      const t = aim + (i ? 1 : -1) * (1.1 + rand(frame + i) * .6);
      bolt(`${key} bolt ${i}`, chest, t, (.6 + rand(i + frame) * .5) * k, 5, frame * 5 + i * 13, .045, Bolt.withAlpha(.95 * (1 - age / .35)), Fire.withAlpha(.5 * (1 - age / .35)), Y + .097);
    }
  }
  if (age < .3) {                      // the fire burst: a ball of orange fire that flares and thins
    const u = age / .3, size = (.4 + .6 * Math.sqrt(u)) * k;
    sprite(chest, size, size * .85, Fire.withAlpha(.85 * (1 - u * u)), puff, Y + .092);
    sprite(chest, size * .6, size * .5, FireCore.withAlpha(.9 * (1 - u)), glow, Y + .0921);
  }
  for (let i = 0; i < 7; i++) {        // flames rising off them for .6 s
    const life = .6, u = age / life; if (u > 1) continue;
    const ph = (age * (2.2 + rand(i + 500)) + rand(i + 510)) % 1, x = chest.x + (rand(i + 520) - .5) * .5 * k;
    sprite({ x, z: chest.z - .15 + ph * .55 }, (.18 + .14 * (1 - ph)) * k, (.22 + .18 * (1 - ph)) * k, Fire.withAlpha(.85 * Math.sin(ph * Math.PI) * (1 - u)), puff, Y + .095);
    sprite({ x, z: chest.z - .15 + ph * .55 }, .09 * k, .11 * k, FireCore.withAlpha(.7 * Math.sin(ph * Math.PI) * (1 - u)), glow, Y + .096);
  }
  if (heavy && age < .45) {            // orange streak lines flying on past the target
    for (let i = 0; i < 7; i++) {
      const u = age / .45, a0 = rand(i + 600) * 1.5 + u * 3, l = .6 + rand(i + 610) * 1.2, acr = (rand(i + 620) - .5) * 1.2;
      const from = { x: chest.x + d.x * a0 - d.z * acr, z: chest.z + d.z * a0 + d.x * acr };
      streak(`${key} streak ${i}`, from, move(from, d, l), .05, Bolt.withAlpha(.9 * (1 - u)), whiteGlow, Y + .098, 3);
    }
  }
  for (let i = 0; i < 8; i++) {
    const life = .25 + rand(i + 900) * .12, u = age / life; if (u > 1) continue;
    const a = aim + (rand(i + 910) - .5) * 1.1, v = (2 + rand(i + 920) * 3.5) * k;
    const x = chest.x + Math.cos(a) * v * age, z = chest.z + Math.sin(a) * v * age - 6 * age * age;
    sprite({ x, z }, .16 * k, .12 * k, Blood.withAlpha(1 - u * u), soft, Y + .09);
  }
  const g = clamp(age / .3), c = move(pos, d, .55);
  sprite(c, 1.0 * g * k, .55 * g * k, Blood.withAlpha(.75 * g), soft, Floor + .02, aim / D2R);
  for (let i = 0; i < 5; i++) {
    const a = aim + (rand(i + 930) - .5) * .9, r = .5 + rand(i + 940) * .9 * k;
    if (g < .5 + i * .1) continue;
    sprite({ x: pos.x + Math.cos(a) * r, z: pos.z + Math.sin(a) * r }, .14 + rand(i + 950) * .12, .11 + rand(i + 960) * .09, Blood.withAlpha(.8), soft, Floor + .021);
  }
}

// The line punches through a wall cell: a dark hole on the face and top that stays, a blue flash,
// dust off both sides for half a second. The bullet does not slow or stop.
function punch(key, at0, d, age) {
  if (age < 0) return;
  const q = lift(at0);
  sprite(q, .2, .16, Smoke.withAlpha(.9), soft, WallTop + .01);
  sprite(q, .3, .24, new Color(.16, .14, .12).withAlpha(.5), soft, WallTop + .009);
  if (age < .1) glint(`${key} flash`, q, .4, 1 - age / .1, Beam, Math.atan2(d.z, d.x) / D2R);
  for (let i = 0; i < 4; i++) {
    const u = (age - i * .04) / .5; if (u < 0 || u > 1) continue;
    const g = move(q, d, (i % 2 ? 1 : -1) * (.3 + u * .5));
    sprite({ x: g.x + (rand(i + 80) - .5) * .25, z: g.z + u * .25 }, .22 + u * .35, .2 + u * .3, Dust.withAlpha((1 - u) * .35), puff, Y + .03);
  }
}

// The beam left behind the bullet, in 2-cell pieces that fade from when the bullet passed them.
// tier 0 (shots 1-3): a thin dim blue line and big white-violet lightning forks a cell off it.
// tier 1 (shots 4-6): wider, blue-violet, with a white core. tier 2 (the seventh): a cyan-white
// core inside a wide pale halo, bigger forks.
function beam(key, L, flown, age, width, life, s, tier) {
  const sd = side(L.d), frame = Math.floor(s * 16);
  for (let d0 = 0, i = 0; d0 < flown; d0 += Chunk, i++) {
    const d1 = Math.min(flown, d0 + Chunk), passed = age - (d0 + d1) / 2 / Speed, fade = Math.exp(-Math.max(0, passed) / life);
    if (fade < .02) continue;
    const from = lift(move(L.start, L.d, d0)), to = lift(move(L.start, L.d, d1));
    if (tier === 0) {
      line(`${key} edge ${i}`, [from, to], width * 2.2 * fade, BeamEdge.withAlpha(.35 * fade), whiteGlow, Y + .08, 'none');
      line(`${key} mid ${i}`, [from, to], width * .8, Beam.withAlpha(.75 * fade), whiteGlow, Y + .081, 'none');
    } else if (tier === 1) {
      line(`${key} edge ${i}`, [from, to], width * 2.6 * fade, BeamViolet.withAlpha(.5 * fade), whiteGlow, Y + .08, 'none');
      line(`${key} mid ${i}`, [from, to], width * 1.2, Beam.withAlpha(.8 * fade), whiteGlow, Y + .081, 'none');
      line(`${key} core ${i}`, [from, to], width * .45, White.withAlpha(.95 * fade), whiteGlow, Y + .082, 'none');
    } else {
      line(`${key} halo ${i}`, [from, to], width * 3.2 * fade, Halo.withAlpha(.45 * fade), whiteGlow, Y + .079, 'none');
      line(`${key} edge ${i}`, [from, to], width * 1.8, Beam.withAlpha(.7 * fade), whiteGlow, Y + .08, 'none');
      line(`${key} mid ${i}`, [from, to], width * 1.0, Cyan.withAlpha(.95 * fade), whiteGlow, Y + .081, 'none');
      line(`${key} core ${i}`, [from, to], width * .45, White.withAlpha(1 * fade), whiteGlow, Y + .082, 'none');
    }
    if (fade < .3) continue;
    const forks = 2 + tier;
    for (let j = 0; j < forks; j++) {  // lightning forks: a 6-bend branch a cell off the line, on for a few frames at a time
      if (rand(i * 7 + j * 3 + frame) < .45) continue;
      const u0 = rand(i * 11 + j + frame), base = lift(move(L.start, L.d, d0 + (d1 - d0) * u0)), dir = rand(i + j * 5 + Math.floor(s * 9)) > .5 ? 1 : -1;
      const reach = (.6 + .8 * rand(i * 3 + j + frame)) * (1 + tier * .4), lean = (rand(i + j * 9 + frame) - .5) * 1.4;
      const t = Math.atan2(sd.z * dir + L.d.z * lean, sd.x * dir + L.d.x * lean);
      bolt(`${key} fork ${i} ${j}`, base, t, reach, 6, i * 31 + j * 7 + frame, .05 * (1 + tier * .3), White.withAlpha(.95 * fade), Violet.withAlpha(.5 * fade), Y + .083);
      if (j === 0) {                   // a branch off the first fork's middle
        const mid = { x: base.x + Math.cos(t) * reach * .45, z: base.z + Math.sin(t) * reach * .45 };
        bolt(`${key} twig ${i} ${j}`, mid, t + (rand(i + frame) - .5) * 1.6, reach * .5, 4, i * 17 + frame, .035, White.withAlpha(.8 * fade), Violet.withAlpha(.4 * fade), Y + .083);
      }
    }
  }
}

export default {
  kit: 'E.G.O. weapons', label: 'Magic Bullet (sketch)',
  params: {
    shot: P('Shot number (7 = the seventh)', 1, 1, 7, 1, 'Rule'),
    aim: P('Aim (degrees)', 0, 0, 360, 5, 'Showcase'),
    actors: { label: 'Show the pawns and the wall', value: true, group: 'Showcase' },
    corroded: { label: 'Corroded look (Der Freischütz bleeds through)', value: false, group: 'Showcase' },
    range: P('Range (cells)', 40, 5, 40, 1, 'Rule'),
    lead: P('Aim before the shot', .45, .1, 1.5, .05, 'Timing (s)'),
    hold: P('Show the result', 1.5, .3, 3, .1, 'Timing (s)'),
    beamFade: P('Beam fade', BeamLife, .2, 2, .05, 'Timing (s)'),
    beamWidth: P('Beam width (cells)', .07, .03, .3, .01, 'Shape'),
    circleRadius: P('Magic circle radius (cells)', .55, .2, 1, .02, 'Shape'),
    circleMode: { label: 'Circle', value: 'faces the aim', options: ['faces the aim', 'faces the viewer', 'lies flat'], group: 'Shape' },
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p), out = [{ name: 'Aim', t: 0 }];
    if (p.shot >= Shots) out.push({ name: 'Rifle turns', t: p.lead });
    out.push({ name: p.shot >= Shots ? 'The seventh' : `Shot ${p.shot}`, t: t.fire }, { name: 'Result', t: t.fire + t.flight });
    return out;
  },
  events(p) {
    const t = times(p);
    return [{ t: t.fire, type: 'sound', def: p.shot >= Shots ? 'RimArt_MagicBulletSeventh' : 'RimArt_MagicBulletFire' }, { t: t.fire, type: 'shake', value: p.shot >= Shots ? .03 : .015 }];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const seventh = p.shot >= Shots;
    const a = dirOf(p.aim), across = side(a);
    const place = (along, acr) => ({ x: o.x + a.x * along + across.x * acr, z: o.z + a.z * along + across.z * acr });
    const bd = dirOf(p.aim + BelovedDeg), beloved = move(o, bd, BelovedDist), between = move(o, bd, BetweenDist);
    const seventhDeg = p.aim + BelovedDeg;
    const who = { body: 'average', sun, shadow: strength };
    const people = p.actors ? [
      { pos: place(Raider.along, Raider.across), colour: Enemy, tag: 'raider' },
      { pos: place(Colonist.along, Colonist.across), colour: Ally, tag: 'colonist' },
      { pos: between, colour: Enemy, tag: 'between' },
      { pos: beloved, colour: Ally, tag: 'beloved' },
    ] : [];
    const wallCells = [];
    if (p.actors) for (let k = -Wall.halfWidth; k <= Wall.halfWidth; k++) { const c = place(Wall.along, k); wallCells.push({ x: o.x + Math.round(c.x - o.x), z: o.z + Math.round(c.z - o.z) }); }

    // The rifle's direction now: the aim, or turning to the beloved on the seventh.
    const turn = seventh ? smooth((s - p.lead) / Swing) : 0;
    const gunDeg = p.aim + turn * BelovedDeg;
    const fired = s >= t.fire, age = fired ? s - t.fire : -1, flown = fired ? Math.min(p.range, age * Speed) : 0;
    const L = shotLine(p, o, seventhDeg), hits = fired ? crossings(L, people, wallCells) : [];
    const hitAge = h => s - (t.fire + h.d / Speed);
    const tier = seventh ? 2 : p.shot >= 4 ? 1 : 0, u = freeAxis(L.d);

    // Floor first: the warning line to the beloved after shot six, the tan sweep, and the spatters.
    if (p.actors && p.shot >= Shots - 1 && (seventh ? s < t.fire : true)) {
      const dx = beloved.x - o.x, dz = beloved.z - o.z, len = Math.hypot(dx, dz), ux = dx / len, uz = dz / len;
      const alpha = seventh ? 1 - clamp((s - p.lead) / Swing) : clamp((s - t.fire) / .3);
      let i = 0;
      for (let d = .6; d < len - .4; d += .45, i++) line(`mb warn ${i}`, [{ x: o.x + ux * d, z: o.z + uz * d }, { x: o.x + ux * (d + .25), z: o.z + uz * (d + .25) }], .06, Warn.withAlpha(.6 * alpha), flat, Floor + .03, 'none');
      sprite(beloved, .9, .9, Warn.withAlpha(.25 * alpha), soft, Floor + .031);
    }
    if (fired && age < 1.2) {          // the brush sweep along the floor from the shooter's feet
      const g = clamp(age / .1), f = 1 - smooth((age - .5) / .7);
      line('mb sweep', [move(o, L.d, .2), move(o, L.d, .2 + 2.6 * g)], .6, Tan.withAlpha(.55 * f), flat, Floor + .032, 'end');
      line('mb sweep 2', [{ x: o.x - across.x * .25 + L.d.x * .5, z: o.z - across.z * .25 + L.d.z * .5 }, move(o, L.d, .5 + 1.6 * g)], .3, TanDark.withAlpha(.35 * f), flat, Floor + .0321, 'end');
    }
    for (const h of hits) if (h.kind === 'pawn') wound(`mb wound ${h.who.tag}`, h.who.pos, who, L.d, hitAge(h), seventh, s);

    if (p.actors) walls('mb walls', o, wallCells, sun, strength);
    for (const h of hits) if (h.kind === 'wall') punch(`mb punch ${h.cell.x},${h.cell.z}`, h.at, L.d, hitAge(h));

    // Pawns: the hit ones flinch along the line. The beloved wears a heart so the reader knows.
    for (const q of people) {
      const h = hits.find(x => x.kind === 'pawn' && x.who === q), ha = h ? hitAge(h) : -1;
      const shove = ha >= 0 ? Flinch * bump(ha / .3) + Settle * clamp(ha / .3) : 0;
      const pos = move(q.pos, L.d, shove);
      pawn(pos, { ...who, shirt: q.colour });
      if (q.tag === 'beloved') {
        const top = at(pos, 'headTop', who), z = top.z + .2 + .03 * Math.sin(s * 4);
        draw(disc, top.x - .06, Y + .15, z + .03, .08, .08, 0, Heart);
        draw(disc, top.x + .06, Y + .15, z + .03, .08, .08, 0, Heart);
        band('mb heart', [{ x: top.x - .13, z: z + .02 }, { x: top.x, z: z - .14 }], [{ x: top.x + .13, z: z + .02 }, { x: top.x, z: z - .14 }], Heart, Y + .151);
      }
      if (h) {                         // a parchment slab stands at the hit and thins to an oval
        const life = paperLife(ha);
        if (life) parchment(`mb slab ${q.tag}`, at(pos, 'chest', who), u, 1, -.06, .12, .55 * life.open, .18 * life.open, life.alpha * .9, life.thin);
      }
    }
    // The shooter, the counter, and the rifle in front. The kick swings the barrel up.
    pawn(o, { ...who, shirt: Holder });
    if (p.corroded) abnormality(o, who, sun, strength, s);
    counter('mb counter', at(o, 'headTop', who), p.shot, s);
    const gd = dirOf(gunDeg), kick = fired ? .1 * bump(age / .14) : 0;
    const tiltU = !fired ? 0 : age < KickUp ? age / KickUp : 1 - smooth((age - KickUp) / KickSettle);
    const hand = move(o, gd, GripAlong);
    const gun = rifle('mb rifle', hand, gunDeg, sun, strength, kick, KickTilt * tiltU);

    // Before the shot: the chamber glows and the magic circles open ahead of the muzzle, then slide
    // back to it. They follow the muzzle while the rifle turns, and close after the bullet has left.
    const head = at(o, 'headTop', who);
    const charge = clamp((s - .02) / (t.fire - .02)) * (fired ? 1 - clamp(age / .12) : 1);
    chamberGlow('mb chamber', gun.chamber, gun.muzzle, head, charge, s);
    const openU = seventh ? clamp((s - p.lead - Swing * .5) / (Swing * .5 + .05)) : clamp(s / (p.lead * .4));
    const slideU = seventh ? clamp((s - p.lead - Swing * .8) / .15) : clamp((s - p.lead * .4) / (p.lead * .6));
    const open = smooth(openU) * (fired ? 1 - smooth((age - .15) / CircleClose) : 1);
    const ahead = CircleFar + (CircleNear - CircleFar) * smooth(slideU);
    const n = CirclesFor[Math.min(Shots, Math.max(1, Math.round(p.shot))) - 1];
    const gateDir = fired ? L.d : gd;      // once fired the circles stay on the shot line, not the kicked barrel
    const gateAt = move(fired ? move(lift(hand), L.d, RifleLen * .72) : gun.muzzle, gateDir, 0);
    const circles = [], ax = gateAxes(p.circleMode, gateDir);
    for (let i = 0; i < n; i++) {
      const c = move(gateAt, gateDir, ahead + i * .48), r = p.circleRadius * (1 + i * .45);
      circles.push({ c, r });
      magicCircle(`mb circle ${i}`, c, r, open * clamp(1 - i * .1), s, i + 1, ax.H, ax.V, ax.hTrue, ax.stands, sun, strength);
    }
    if (tier === 2 && open > 0) { circle(lift(o), 1.0 * open, .8 * open, Y + .037, Circle); sprite(lift(o), 2.4 * open, 2.4 * open, CircleDeep.withAlpha(.25 * open), glow, Y + .0365); }

    if (!fired) return;
    // On the shot: the parchment brackets either side of the first circle, and cyan speed lines past it.
    const gate = circles[0].c, rr = circles[0].r, life = paperLife(age);
    if (life) {
      parchment('mb bracket left', gate, u, -1, rr * 1.25, rr * .6, rr * 1.6 * life.open, .16 * life.open, life.alpha, life.thin);
      parchment('mb bracket right', gate, u, 1, rr * 1.15, rr * .4, rr * 1.15 * life.open, .13 * life.open, life.alpha, life.thin);
    }
    if (age < .3) for (let i = 0; i < 8; i++) {
      const v = age / .3, along = -.4 + rand(i + 800) * 1.4 + v * 1.6, acr = (rand(i + 810) - .5) * 1.8, len = .25 + rand(i + 820) * .4;
      const from = { x: gate.x + L.d.x * along - L.d.z * acr, z: gate.z + L.d.z * along + L.d.x * acr };
      streak(`mb speed ${i}`, from, move(from, L.d, len), .03, Cyan.withAlpha(.9 * (1 - v)), whiteGlow, Y + .09, 3);
    }

    // Muzzle flash, then the beam from the muzzle to the bullet, and the bullet itself.
    const mq = lift(move(hand, L.d, RifleLen * .72)), w = p.beamWidth * [1, 1.5, 2.2][tier];
    if (age < .09) {
      glint('mb flash', mq, .7 * (1 + age * 3), 1 - age / .09, Beam, L.deg);
      streak('mb tongue', mq, move(mq, L.d, .7 + age * 3), .2 * (1 - age / .09), White.withAlpha(1 - age / .09), whiteGlow, Y + .12, 4);
    }
    if (tier === 2 && age < t.flight + .4) {   // the seventh: a cyan burst that sits at the circle while the beam is up
      const f = 1 - smooth((age - t.flight) / .4), pulse = .9 + .1 * Math.sin(s * 30);
      sprite(gate, 2.2 * f * pulse, 2.0 * f * pulse, Halo.withAlpha(.5 * f), glow, Y + .085);
      sprite(gate, 1.1 * f, 1.0 * f, Cyan.withAlpha(.9 * f), glow, Y + .086);
      sprite(gate, .5 * f, .45 * f, White.withAlpha(1 * f), glow, Y + .087);
    }
    beam('mb beam', L, flown, age, w, p.beamFade, s, tier);
    if (flown < p.range) {
      const b = lift(move(L.start, L.d, flown));
      streak('mb bullet tail', move(b, L.d, -.9), b, w * 2.2, Beam.withAlpha(.9), whiteGlow, Y + .10, 6);
      sprite(b, .32, .28, Beam.withAlpha(.9), glow, Y + .11);
      streak('mb bullet core', move(b, L.d, -.2), move(b, L.d, .04), w * .8, White.withAlpha(1), whiteGlow, Y + .115, 4);
      sprite(move(b, L.d, .06), .07, .05, Smoke.withAlpha(.9), soft, Y + .116, L.deg);   // the small dark tip the source bullet has
    }
  },
};
