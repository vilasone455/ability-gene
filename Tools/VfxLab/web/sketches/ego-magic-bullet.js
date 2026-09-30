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
//   Corrosion (shared system): the "Corroded" checkbox. The gun aims itself at the nearest living
//         pawn, any faction: the colonist stand-in walks up to 2 cells, nearer than the raider, and
//         the shot turns onto them; a violet floor ring and dashed line mark the gun's target. The
//         seventh still goes to the beloved.
//
// Look, from the source. Second pass 2026-09-30 against seven frames of the Limbus "Magic Bullet
// Fire" skill (youtube P3Kq4RHdDE0: aim, bullet at the muzzle, shot 1, hit, a two-circle aim, the
// big shot, its hit). What each frame shows and what the sketch does with it:
//   Aim:   one big translucent blue magic circle (about the pawn's height across) stands about a
//          cell in front of the muzzle: an outer double ring, a ring of rune ticks, a hexagram,
//          an inner ring and a centre sigil, all thin bright blue lines over a soft blue fill.
//          Five or six blue sparks drift up around the muzzle and the shooter's head. Just before
//          the shot the circle slides back to the muzzle and the bullet sits in it.
//   Shot 1-3: a thin dim blue line with a long white-violet lightning bolt running along it that
//          swings up to half a cell to either side and throws short branches; the light sits at
//          the circle, with short rays off it, not at the muzzle.
//          The source frames put tan parchment brush marks around the circle and the target; those
//          were tried and dropped (2026-09-30) as not fitting RimWorld, and so was ground dust
//          (the bullet flies at chest height and nothing touches the ground). The shot's force is
//          in the air instead: a thin pale ring at chest height spreading 1.6 cells from the circle
//          in .3 s, and a puff of gun smoke pushed from the muzzle along the line. The only trace
//          left on the ground is the hit pawns' blood. Short cyan speed lines fly past the circle.
//          The rifle kicks up about 40 degrees, held one-handed, the shooter rocked back.
//   Hit:   the target lit yellow-white, a burst of thin yellow spikes and long rays, an orange
//          lightning bolt arcing past it along the beam, orange sparks flung on forward, orange
//          streak lines, a small burn. The brackets and slab thin to tan oval outlines and stay.
//   Count 4-6: two or more circles stacked along the aim .48 cells apart, each 45 % bigger than the
//          one before (the source's second circle is about 1.6x the first), the beam
//          wider and blue-violet.
//   The seventh: a cyan-white beam several times wider with a wide pale halo carrying long
//          streaks, a cyan burst at the circle, three bolts, and orange streak lines flying on
//          past the target.
//   Rifle: about nine times longer than wide (the icon is 200 x 23): black barrel, a gold filigree
//          band around the chamber, a navy stock.
//   Corroded: not the source's smoke silhouette (it hid the pawn, which RimWorld cannot afford).
//          Made for the game instead: the pawn stays readable with the weapon's magic on him: a dim
//          contract circle on the floor under his feet, dark violet veins from the chest, blue eyes,
//          thin wisps off the shoulders, the barrel lit blue, the counter pips turned violet.
//
// Order (default sliders, shot 1..6):
//   0.00  aim: rifle level at chest height on the aim; seven pips over the head show the count;
//         the circle opens a cell ahead of the muzzle over the first 40 % of the aim time, then
//         slides back to .4 cells ahead; blue sparks drift up; the chamber glows
//   0.45  fire: flash, the shooter rocks back .09 cells, the rifle slides back .16 and its barrel
//         jumps up 40 degrees in .05 s and swings back down, level by .59 s; the bullet flies at
//         120 cells/s out through the circle; the brackets, slab and floor sweep appear in .08 s;
//         the beam is left from the muzzle to the range with forks along it; it crosses the wall
//         (a punched hole and dust that stay) and every pawn on the line (the raider in front of
//         the wall, the colonist behind it): stagger star, orange bolt, fire, blood, a floor
//         spatter that stays; each hit pawn flinches .18 cells; the air ring is
//         gone in .3 s, the gun smoke in .5 s
//   0.80  the bullet has reached the 40-cell range; each 2-cell piece of the beam fades over .6 s
//         from when the bullet passed it; the circle closes .35 s later; the result is held 1.5 s
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
// The air ring and the smoke are a level circle and puffs, so they need no per-facing work. The
// rifle's kick is a real tilt: the muzzle end
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
// white over violet, the seventh cyan-white, the smoke pale, the hit yellow and orange.
const White = new Color(1, 1, 1), Beam = new Color(.55, .80, 1), BeamEdge = new Color(.20, .45, 1);
const Circle = new Color(.35, .62, 1), CircleBright = new Color(.50, .78, 1), CircleDeep = new Color(.12, .22, .80);
const Cyan = new Color(.55, .96, 1), Halo = new Color(.65, .85, 1), Violet = new Color(.72, .62, 1);
const Barrel = new Color(.07, .07, .09), BarrelLit = new Color(.30, .31, .36), Gold = new Color(.86, .68, .26), Navy = new Color(.11, .14, .38);
const BeamViolet = new Color(.55, .45, 1), Pale = new Color(.92, .90, .84);
const Smoke = new Color(.04, .03, .05), Eye = new Color(.45, .75, 1), Fire = new Color(1, .50, .12), FireCore = new Color(1, .90, .50);
const Stagger = new Color(1, .85, .25), Bolt = new Color(1, .62, .18);
const Warn = new Color(.75, .16, .10), Heart = new Color(.95, .45, .55);
const Vein = new Color(.34, .18, .62), Corrupt = new Color(.55, .28, .88);   // the corroded look's veins and its floor marker
// Decided looks and the rule's fixed numbers.
const Speed = 120;                    // cells/s: the shot is near-instant (40 cells in .33 s); the beam is what the player sees
const Chunk = 2;                      // the beam fades in 2-cell pieces from when the bullet passed each
const Swing = .35, Tail = .5;
const Flinch = .18, Settle = .08;
const BeamLife = .6, CircleClose = .25;
const KickTilt = 40 * D2R, KickUp = .05;                     // the barrel jumps up 40 degrees in .05 s ...
const KickDamp = 3.2, KickSwing = 2.9;                       // ... and comes down as a damped swing: 40 x e^(-3.2 t) cos(2.9 t), level again at .59 s
const KickSlide = .16, RockBack = .09;                       // the rifle slides back .16 cells; the shooter rocks back .09
const ShockRadius = 1.6, ShockTime = .3;                     // the air ring from the circle grows to this over this
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
const CorrodedAlly = { along: 1.29, across: 1.53 };   // while corroded the colonist has walked up to 2 cells, nearer than the raider at 2.5
const BelovedDeg = 125, BelovedDist = 5.5, BetweenDist = 2.8;

function times(p) {
  const swing = p.shot >= Shots ? Swing : 0, fire = p.lead + swing;
  return { fire, flight: p.range / Speed, end: fire + p.range / Speed + p.hold + Tail };
}

// The line the shot takes, and everything it crosses, in order of distance from the muzzle.
function shotLine(p, o, seventhDeg, aimDeg) {
  const deg = p.shot >= Shots ? seventhDeg : aimDeg, d = dirOf(deg);
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
// The seven pips over the shooter's head. Filled ones are shots already taken; the next one is lit;
// pip seven is the big one. On six it pulses.
function counter(key, head, shot, s, tint = Beam) {
  const pitch = .13, x0 = head.x - pitch * 3, z = head.z + .32;
  for (let i = 0; i < Shots; i++) {
    const n = i + 1, spent = n < shot, next = n === shot, last = n === Shots;
    const warm = next && last ? .5 + .5 * Math.sin(s * 9) : 0, r = last ? .06 : .045;
    draw(disc, x0 + i * pitch, Y + .2, z, r, r, 0, spent ? CircleDeep : CircleDeep.withAlpha(.25));
    if (next) sprite({ x: x0 + i * pitch, z }, .18, .18, tint.withAlpha(.5 + .3 * warm), glow, Y + .201);
    if (!spent) draw(disc, x0 + i * pitch, Y + .202, z, r + .005, r + .005, 0, tint.withAlpha(.5));
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
function magicCircle(key, c, r, open, s, k, H, V, hTrue, stands, sun, strength, L = Y + .04, dim = 1) {
  if (open <= 0) return;
  const rr = r * open, a = Math.min(1, open * 1.5) * dim, spin = s * (k % 2 ? .9 : -.7) + k, N = 48;
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
    ringAt('back', back, rr, .7 * a, L + .0005, CircleDeep, .04);
    ringAt('back 2', back, rr * .80, .5 * a, L + .0005, CircleDeep);
  }
  sprite(c, gw, gh, CircleDeep.withAlpha(.30 * a), glow, L);
  discAt('fill', c, rr * .98, .28 * a, L + .0001, Circle);                  // the translucent fill
  ringAt('outer', c, rr, 1.0 * a, L + .001, CircleBright, .035);
  ringAt('outer 2', c, rr * .955, .55 * a, L + .001, Circle, .02);
  ringAt('rune', c, rr * .80, .9 * a, L + .001, Circle);
  ringAt('mid', c, rr * .62, .8 * a, L + .001, CircleBright);
  ringAt('inner', c, rr * .36, .8 * a, L + .001, CircleBright);
  ringAt('inner 2', c, rr * .30, .5 * a, L + .001, Circle, .02);
  if (stands) sprite(on(Math.PI / 2, rr * .88), gw * .4, rr * .3, White.withAlpha(.35 * a), glow, L + .0011);   // the lit top rim
  const ticks = 36;                    // the rune ring between .80 and .955, every third tick long
  for (let i = 0; i < ticks; i++) {
    const t = spin + i / ticks * TAU, outer = rr * (i % 3 ? .90 : .955);
    streak(`${key} tick ${i}`, on(t, rr * .80), on(t, outer), .02, CircleBright.withAlpha(.6 * a), whiteGlow, L + .002, 3);
  }
  for (let tri = 0; tri < 2; tri++) {  // the hexagram: two triangles inscribed in the .62 ring
    const pts = [];
    for (let i = 0; i <= 3; i++) pts.push(on(-spin * .5 + tri * Math.PI / 3 + i * TAU / 3 + Math.PI / 2, rr * .62));
    line(`${key} tri ${tri}`, pts, .025, CircleBright.withAlpha(.6 * a), whiteGlow, L + .0015, 'none');
  }
  const sq = [];                       // the centre sigil: a square in the inner ring, a dot
  for (let i = 0; i <= 4; i++) sq.push(on(spin + i * TAU / 4 + Math.PI / 4, rr * .30));
  line(`${key} sigil`, sq, .022, CircleBright.withAlpha(.6 * a), whiteGlow, L + .0015, 'none');
  sprite(c, gw * .1, gh * .1, Circle.withAlpha(.5 * a), glow, L + .0025);
  sprite(c, gw * .035, gh * .035, White.withAlpha(.7 * a), glow, L + .0026);
  for (let i = 0; i < 6; i++) {        // rune dots on the .71 ring, spinning the other way
    const g = on(-spin * .7 + i / 6 * TAU, rr * .71);
    sprite(g, .04, .036, White.withAlpha(.6 * a), glow, L + .0025);
  }
  for (let i = 0; i < 4; i++) {        // white sparks around the rim
    const ph = (s * 1.7 + rand(i + 200 + k)) % 1, t = rand(i + 210 + k) * TAU + s * .8;
    sprite(on(t, rr * (1 + ph * .3)), .07, .06, White.withAlpha(a * (1 - ph)), glow, L + .003);
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

// The shot's pressure, in the air where the bullet is: a thin pale ring at chest height spreading
// from the first circle to ShockRadius in ShockTime and thinning as it goes. A level circle, so
// it looks the same from every aim. No dust: nothing touches the ground.
function shockRing(key, c, age, tier) {
  if (age < 0 || age > ShockTime) return;
  const u = age / ShockTime, r = ShockRadius * (1 + .3 * tier) * (1 - Math.pow(1 - u, 2.5)), a = (1 - u) * .6;
  for (let j = 0; j < 4; j++) circle(c, r * (1 - j * .02), a * (j ? .3 : .6), Y + .035, j ? Halo : Pale);
  sprite(c, r * 2.1, r * 2.1, Halo.withAlpha(.08 * (1 - u)), soft, Y + .034);
}
// Gun smoke: a few thin pale puffs pushed from the muzzle along the line at chest height, out to
// 2.5 cells in .35 s, thinning as they go.
function muzzleSmoke(key, mq, d, age) {
  if (age < 0 || age > .5) return;
  for (let i = 0; i < 6; i++) {
    const u = clamp(age / (.35 + .15 * rand(i + 90))); if (u >= 1) continue;
    const along = .2 + 2.3 * (1 - Math.pow(1 - u, 2)) * (.6 + .4 * rand(i + 100)), acr = (rand(i + 110) - .5) * .35 * (1 + u);
    const q = { x: mq.x + d.x * along - d.z * acr, z: mq.z + d.z * along + d.x * acr + u * .1 }, size = .18 + .3 * u;
    sprite(q, size, size * .85, Pale.withAlpha(.3 * (1 - u) * (1 - u)), puff, Y + .03);
  }
}
// The corroded look, made for the game rather than the source: the pawn stays readable and the
// weapon's magic shows on him. A dim contract circle spins slowly on the floor under his feet
// (the shot circle's own drawing, flat, at a quarter speed), four dark violet veins run from the
// chest over the body and pulse, the eyes glow blue, three thin wisps rise off the shoulders, and
// the barrel is lit blue from chamber to muzzle.
function corrodedLook(pos, who, gun, sun, strength, s) {
  const head = at(pos, 'head', who), neck = at(pos, 'neck', who), chest = at(pos, 'chest', who), pulse = .7 + .3 * Math.sin(s * 3);
  magicCircle('mb contract', pos, .6, 1, s * .25, 9, { x: 1, z: 0 }, { x: 0, z: 1 }, { x: 1, z: 0 }, false, sun, strength, Floor + .05, .55);
  for (let i = 0; i < 4; i++) {                                                             // veins
    const t = i / 4 * TAU + .6, reach = .2 + .1 * rand(i + 400), pts = [chest];
    for (let k = 1; k <= 4; k++) {
      const u = k / 4, off = (rand(i * 7 + k * 3) - .5) * reach * .5;
      pts.push({ x: chest.x + Math.cos(t) * reach * u - Math.sin(t) * off, z: chest.z + Math.sin(t) * reach * u * .8 + Math.cos(t) * off });
    }
    line(`mb vein ${i}`, pts, .022, Vein.withAlpha(.9 * pulse), flat, pawnLayer + .03, 'end');
  }
  for (const dx of [-.07, .07]) {                                                           // eyes
    sprite({ x: head.x + dx, z: head.z - .02 }, .1, .08, Eye.withAlpha(.9 * pulse), glow, pawnLayer + .031);
    draw(disc, head.x + dx, pawnLayer + .032, head.z - .02, .02, .016, 0, White);
  }
  for (let i = 0; i < 3; i++) {                                                             // wisps
    const ph = (s * .5 + rand(i + 300)) % 1, x = neck.x + (i - 1) * .16 + Math.sin(s * 2 + i) * .03;
    sprite({ x, z: neck.z + .05 + ph * .4 }, .12 + ph * .1, .14 + ph * .12, Smoke.withAlpha(.35 * Math.sin(ph * Math.PI)), puff, pawnLayer + .04);
  }
  streak('mb barrel lit', gun.chamber, gun.muzzle, .07, Circle.withAlpha(.35 + .25 * pulse), whiteGlow, pawnLayer + .07, 3);   // the barrel lit
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

// One pawn crossed by the line, as the hit frames show it: the pawn lit yellow-white for .15 s, a
// burst of fourteen thin yellow spikes and four long rays over .35 s, an orange bolt arcing past
// the target along the beam, orange sparks flung on forward, orange streak lines flying past, a
// small burn (fire for .6 s), blood thrown on and a spatter that stays. tier scales it: the
// seventh's is 1.5x.
function wound(key, pos, who, d, age, tier, s) {
  if (age < 0) return;
  const chest = at(pos, 'chest', who), k = 1 + tier * .25, aim = Math.atan2(d.z, d.x), frame = Math.floor(s * 18);
  if (age < .15) {                     // the pawn lit
    const f = 1 - age / .15;
    sprite({ x: chest.x, z: chest.z - .12 }, .7 * k, 1.0 * k, Stagger.withAlpha(.7 * f), glow, Y + .091);
    sprite(chest, .45 * k, .45 * k, White.withAlpha(.8 * f), glow, Y + .0911);
  }
  if (age < .35) {                     // the stagger burst
    const u = age / .35, grow = Math.sqrt(Math.min(1, age / .08)), fade = 1 - u * u;
    sprite(chest, 1.1 * k * grow, 1.0 * k * grow, Stagger.withAlpha(.5 * fade), glow, Y + .093);
    sprite(chest, .5 * k * grow, .45 * k * grow, FireCore.withAlpha(.9 * fade), glow, Y + .0931);
    for (let i = 0; i < 14; i++) {
      const t = i / 14 * TAU + .2 + rand(i + 700) * .3, l = (.35 + .6 * rand(i + 710)) * k * grow;
      streak(`${key} spike ${i}`, chest, { x: chest.x + Math.cos(t) * l, z: chest.z + Math.sin(t) * l * .85 }, (.055 - .025 * u) * k, Stagger.withAlpha(.95 * fade), whiteGlow, Y + .094, 4);
    }
    for (let i = 0; i < 4; i++) {
      const t = i / 4 * TAU + .6, l = (1.1 + .4 * rand(i + 720)) * k * grow;
      streak(`${key} ray ${i}`, chest, { x: chest.x + Math.cos(t) * l, z: chest.z + Math.sin(t) * l * .85 }, .03, FireCore.withAlpha(.8 * fade), whiteGlow, Y + .0941, 3);
    }
  }
  if (age < .4) {                      // the orange bolt past the target, redrawn every 1/18 s
    const f = 1 - age / .4;
    for (let b = 0; b < (tier ? 2 : 1); b++) {
      if (rand(frame * 3 + b) < .25) continue;
      const back = .7 + rand(frame + b * 7) * .5, acr = (rand(frame * 5 + b) - .5) * .9;
      const from = { x: chest.x - d.x * back - d.z * acr, z: chest.z - d.z * back + d.x * acr };
      bolt(`${key} bolt ${b}`, from, aim + (rand(frame * 7 + b * 3) - .5) * .5, (1.5 + rand(frame + b) * .8) * k, 7, frame * 11 + b * 29, .045, Bolt.withAlpha(.95 * f), Fire.withAlpha(.5 * f), Y + .097);
    }
  }
  for (let i = 0; i < 12; i++) {       // sparks flung on forward, as short streaks
    const life = .3 + rand(i + 800) * .2, u = age / life; if (u > 1) continue;
    const a = aim + (rand(i + 810) - .5) * 1.6, v = (2.5 + rand(i + 820) * 4) * k;
    const q = { x: chest.x + Math.cos(a) * v * age, z: chest.z + Math.sin(a) * v * age };
    streak(`${key} spark ${i}`, { x: q.x - Math.cos(a) * v * .03, z: q.z - Math.sin(a) * v * .03 }, q, .035, (i % 3 ? Bolt : FireCore).withAlpha(1 - u), whiteGlow, Y + .0975, 3);
  }
  if (age < .45) {                     // streak lines flying on past the target
    for (let i = 0; i < 3 + tier * 2; i++) {
      const u = age / .45, a0 = rand(i + 600) * 1.5 + u * 3, l = .6 + rand(i + 610) * 1.2, acr = (rand(i + 620) - .5) * 1.2;
      const from = { x: chest.x + d.x * a0 - d.z * acr, z: chest.z + d.z * a0 + d.x * acr };
      streak(`${key} streak ${i}`, from, move(from, d, l), .05, Bolt.withAlpha(.9 * (1 - u)), whiteGlow, Y + .098, 3);
    }
  }
  if (age < .3) {                      // the burn: a small ball of fire that flares and thins
    const u = age / .3, size = (.25 + .35 * Math.sqrt(u)) * k;
    sprite(chest, size, size * .85, Fire.withAlpha(.8 * (1 - u * u)), puff, Y + .092);
  }
  for (let i = 0; i < 5; i++) {        // flames rising off them for .6 s
    const life = .6, u = age / life; if (u > 1) continue;
    const ph = (age * (2.2 + rand(i + 500)) + rand(i + 510)) % 1, x = chest.x + (rand(i + 520) - .5) * .4 * k;
    sprite({ x, z: chest.z - .15 + ph * .5 }, (.14 + .12 * (1 - ph)) * k, (.18 + .14 * (1 - ph)) * k, Fire.withAlpha(.85 * Math.sin(ph * Math.PI) * (1 - u)), puff, Y + .095);
    sprite({ x, z: chest.z - .15 + ph * .5 }, .07 * k, .09 * k, FireCore.withAlpha(.7 * Math.sin(ph * Math.PI) * (1 - u)), glow, Y + .096);
  }
  if (age < .12) sprite(chest, .5 * k, .4 * k, Blood.withAlpha(.8 * (1 - age / .12)), soft, Y + .09);
  for (let i = 0; i < 8; i++) {        // blood thrown on past
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

// The beam left behind the bullet, in 2-cell pieces that fade from when the bullet passed them,
// drawn as the frames show it: a thin sharp core, a faint wide halo with long streaks drifting
// along it, and one or two long jagged bolts that run the beam's length swinging up to Amp cells
// to either side, with short branches, re-drawn every 1/16 s so they flicker. The bolts' bends are
// indexed by position so the pieces join. tier 0 (shots 1-3): thin blue, one bolt. tier 1 (4-6):
// wider, blue-violet, two bolts. tier 2 (the seventh): a cyan-white core in a wide pale halo, three.
const BoltStep = .5;                  // cells between bends
function beam(key, L, flown, age, width, life, s, tier, gate) {
  const sd = side(L.d), frame = Math.floor(s * 16), bolts = 1 + tier, amp = [.4, .55, .7][tier];
  const fadeAt = d => Math.exp(-Math.max(0, age - d / Speed) / life);
  const body = [Beam, BeamViolet, Cyan][tier], halo = [BeamEdge, BeamViolet, Halo][tier];
  const haloW = [2.4, 3.0, 4.5][tier], bodyW = [.8, 1.1, 1.6][tier], coreW = [.35, .4, .5][tier];
  for (let d0 = 0, i = 0; d0 < flown; d0 += Chunk, i++) {
    const d1 = Math.min(flown, d0 + Chunk), fade = fadeAt((d0 + d1) / 2);
    if (fade < .02) continue;
    const from = lift(move(L.start, L.d, d0)), to = lift(move(L.start, L.d, d1));
    line(`${key} halo ${i}`, [from, to], width * haloW * (.6 + .4 * fade), halo.withAlpha(.22 * fade), whiteGlow, Y + .079, 'none');
    line(`${key} body ${i}`, [from, to], width * bodyW, body.withAlpha(.7 * fade), whiteGlow, Y + .081, 'none');
    line(`${key} core ${i}`, [from, to], width * coreW, (tier ? White : CircleBright).withAlpha(.9 * fade), whiteGlow, Y + .082, 'none');
  }
  for (let j = 0; j < 4 + tier * 3; j++) {   // streaks drifting along the halo at 6 cells/s
    const len = 1 + rand(j + 700) * 2.5, d = ((rand(j + 710) * flown + age * 6) % Math.max(1, flown)), off = (rand(j + 720) - .5) * width * haloW;
    if (d + len > flown) continue;
    const f = fadeAt(d), a0 = lift(move(L.start, L.d, d));
    streak(`${key} streak ${j}`, { x: a0.x + sd.x * off, z: a0.z + sd.z * off }, { x: a0.x + sd.x * off + L.d.x * len, z: a0.z + sd.z * off + L.d.z * len }, width * .5, halo.withAlpha(.35 * f), whiteGlow, Y + .080, 3);
  }
  const steps = Math.floor(flown / BoltStep);
  for (let b = 0; b < bolts; b++) {    // the long bolts, in 4-cell pieces so each fades with its part of the beam
    if (rand(b * 3 + frame) < .15) continue;                                   // a bolt drops out now and then
    const at = n => {                                                          // bend n of bolt b this frame; n 0 is on the line at the start
      const big = rand(n * 29 + b * 53 + frame * 3) > .7;                   // most bends small, three in ten a full swing
      const off = n === 0 ? 0 : amp * (rand(n * 13 + b * 101 + frame * 7) - .5) * 2 * (big ? 1 : .3);
      const q = lift(move(L.start, L.d, Math.min(flown, n * BoltStep)));
      return { x: q.x + sd.x * off, z: q.z + sd.z * off };
    };
    for (let n0 = 0, piece = 0; n0 < steps; n0 += 8, piece++) {
      const n1 = Math.min(steps, n0 + 8), f = Math.pow(fadeAt((n0 + n1) / 2 * BoltStep), 2.5);   // the bolts die faster than the beam
      if (f < .2 || n1 - n0 < 2) continue;
      const pts = [];
      for (let n = n0; n <= n1; n++) pts.push(at(n));
      line(`${key} bolt ${b} ${piece}`, pts, .045 + tier * .015, White.withAlpha(.95 * f), whiteGlow, Y + .084, 'none');
      line(`${key} bolt glow ${b} ${piece}`, pts, .14 + tier * .05, Violet.withAlpha(.45 * f), whiteGlow, Y + .083, 'none');
      if (rand(piece * 7 + b + frame) > .4) {                                  // a branch off one bend of this piece
        const n = n0 + 1 + Math.floor(rand(piece * 11 + b * 5 + frame) * (n1 - n0 - 1)), base = at(n);
        const t = Math.atan2(sd.z, sd.x) + (rand(n + frame) > .5 ? 0 : Math.PI) + (rand(n * 3 + frame) - .5) * 1.2;
        bolt(`${key} branch ${b} ${piece}`, base, t, (.4 + .6 * rand(n * 5 + frame)) * (1 + tier * .4), 4, n * 17 + frame, .04, White.withAlpha(.9 * f), Violet.withAlpha(.4 * f), Y + .083);
      }
    }
  }
  // The light sits at the circle, not the muzzle: a glow and short rays radiating from it, while the beam is up.
  const up = 1 - smooth((age - flown / Speed - .15) / (life * .8)), size = [.7, 1.0, 1.7][tier];
  if (up > 0) {
    const pulse = .92 + .08 * Math.sin(s * 30);
    sprite(gate, 2.0 * size * up * pulse, 1.8 * size * up * pulse, halo.withAlpha(.45 * up), glow, Y + .085);
    sprite(gate, 1.0 * size * up, .9 * size * up, body.withAlpha(.85 * up), glow, Y + .086);
    sprite(gate, .4 * size * up, .36 * size * up, White.withAlpha(up), glow, Y + .087);
    for (let i = 0; i < 10; i++) {     // the rays: short, re-drawn every 1/16 s
      const t = i / 10 * TAU + rand(i + frame) * .6, r0 = size * (.3 + .2 * rand(i * 3 + frame)), r1 = r0 + size * (.3 + .5 * rand(i * 5 + frame));
      streak(`${key} ray ${i}`, { x: gate.x + Math.cos(t) * r0, z: gate.z + Math.sin(t) * r0 * .8 }, { x: gate.x + Math.cos(t) * r1, z: gate.z + Math.sin(t) * r1 * .8 }, .035, White.withAlpha(.8 * up), whiteGlow, Y + .088, 3);
    }
  }
}

export default {
  kit: 'E.G.O. weapons', label: 'Magic Bullet (sketch)',
  params: {
    shot: P('Shot number (7 = the seventh)', 1, 1, 7, 1, 'Rule'),
    aim: P('Aim (degrees)', 0, 0, 360, 5, 'Showcase'),
    actors: { label: 'Show the pawns and the wall', value: true, group: 'Showcase' },
    corroded: { label: 'Corroded (the gun fires at the nearest pawn, any faction)', value: false, group: 'Showcase' },
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
    return [{ t: t.fire, type: 'sound', def: p.shot >= Shots ? 'RimArt_MagicBulletSeventh' : 'RimArt_MagicBulletFire' }, { t: t.fire, type: 'shake', value: p.shot >= Shots ? .06 : .035 }];
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
      { pos: p.corroded ? place(CorrodedAlly.along, CorrodedAlly.across) : place(Colonist.along, Colonist.across), colour: Ally, tag: 'colonist' },
      { pos: between, colour: Enemy, tag: 'between' },
      { pos: beloved, colour: Ally, tag: 'beloved' },
    ] : [];
    const wallCells = [];
    if (p.actors) for (let k = -Wall.halfWidth; k <= Wall.halfWidth; k++) { const c = place(Wall.along, k); wallCells.push({ x: o.x + Math.round(c.x - o.x), z: o.z + Math.round(c.z - o.z) }); }

    // The rifle's direction now: the aim, or turning to the beloved on the seventh.
    // Corroded, the gun aims itself at the nearest living pawn, any faction (the colonist stand-in
    // is placed nearer than the raider to show it). The seventh still goes to the beloved.
    const nearest = p.corroded && people.length ? people.reduce((m, q) => Math.hypot(q.pos.x - o.x, q.pos.z - o.z) < Math.hypot(m.pos.x - o.x, m.pos.z - o.z) ? q : m) : null;
    const aimDeg = nearest ? Math.atan2(nearest.pos.z - o.z, nearest.pos.x - o.x) / D2R : p.aim;
    const turn = seventh ? smooth((s - p.lead) / Swing) : 0;
    const gunDeg = seventh ? p.aim + turn * BelovedDeg : aimDeg;
    const fired = s >= t.fire, age = fired ? s - t.fire : -1, flown = fired ? Math.min(p.range, age * Speed) : 0;
    const L = shotLine(p, o, seventhDeg, aimDeg), hits = fired ? crossings(L, people, wallCells) : [];
    const hitAge = h => s - (t.fire + h.d / Speed);
    const tier = seventh ? 2 : p.shot >= 4 ? 1 : 0;

    // Floor first: the warning line to the beloved after shot six, the tan sweep, and the spatters.
    if (p.actors && p.shot >= Shots - 1 && (seventh ? s < t.fire : true)) {
      const dx = beloved.x - o.x, dz = beloved.z - o.z, len = Math.hypot(dx, dz), ux = dx / len, uz = dz / len;
      const alpha = seventh ? 1 - clamp((s - p.lead) / Swing) : clamp((s - t.fire) / .3);
      let i = 0;
      for (let d = .6; d < len - .4; d += .45, i++) line(`mb warn ${i}`, [{ x: o.x + ux * d, z: o.z + uz * d }, { x: o.x + ux * (d + .25), z: o.z + uz * (d + .25) }], .06, Warn.withAlpha(.6 * alpha), flat, Floor + .03, 'none');
      sprite(beloved, .9, .9, Warn.withAlpha(.25 * alpha), soft, Floor + .031);
    }
    if (nearest && !seventh && s < t.fire) {   // corroded: the gun's own target, a violet floor ring on the nearest pawn and a dashed line to it
      const dx = nearest.pos.x - o.x, dz = nearest.pos.z - o.z, len = Math.hypot(dx, dz), ux = dx / len, uz = dz / len, pulse = .6 + .3 * Math.sin(s * 5);
      let i = 0;
      for (let d = .6; d < len - .5; d += .4, i++) line(`mb corrupt ${i}`, [{ x: o.x + ux * d, z: o.z + uz * d }, { x: o.x + ux * (d + .2), z: o.z + uz * (d + .2) }], .05, Corrupt.withAlpha(.6), flat, Floor + .03, 'none');
      circle(nearest.pos, .5, pulse, Floor + .031, Corrupt);
    }
    for (const h of hits) if (h.kind === 'pawn') wound(`mb wound ${h.who.tag}`, h.who.pos, who, L.d, hitAge(h), tier, s);

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
    }
    // The shooter, the counter, and the rifle in front. The kick: the shooter rocks back along the
    // shot line, the rifle slides back in the hands and its barrel jumps up, then swings back down.
    const gd = dirOf(gunDeg);
    const rock = fired ? RockBack * (bump(age / .3) * .7 + .3 * clamp(age / .1) * (1 - smooth((age - .3) / .5))) : 0;
    const stand = move(o, L.d, -rock);
    pawn(stand, { ...who, shirt: Holder });
    counter('mb counter', at(stand, 'headTop', who), p.shot, s, p.corroded ? Corrupt : Beam);
    const kick = fired ? KickSlide * (bump(age / .12) * .75 + .25 * clamp(age / .06) * (1 - smooth((age - .12) / .5))) : 0;
    const tiltU = !fired ? 0 : age < KickUp ? Math.sin(age / KickUp * Math.PI / 2) : Math.max(0, Math.exp(-KickDamp * (age - KickUp)) * Math.cos(KickSwing * (age - KickUp)));
    const hand = move(stand, gd, GripAlong);
    const gun = rifle('mb rifle', hand, gunDeg, sun, strength, kick, KickTilt * tiltU);
    if (p.corroded) corrodedLook(stand, who, gun, sun, strength, s);

    // Before the shot: the chamber glows and the magic circles open ahead of the muzzle, then slide
    // back to it. They follow the muzzle while the rifle turns, and close after the bullet has left.
    const head = at(stand, 'headTop', who);
    const charge = clamp((s - .02) / (t.fire - .02)) * (fired ? 1 - clamp(age / .12) : 1);
    chamberGlow('mb chamber', gun.chamber, gun.muzzle, head, charge, s);
    const openU = seventh ? clamp((s - p.lead - Swing * .5) / (Swing * .5 + .05)) : clamp(s / (p.lead * .4));
    const slideU = seventh ? clamp((s - p.lead - Swing * .8) / .15) : clamp((s - p.lead * .4) / (p.lead * .6));
    const open = smooth(openU) * (fired ? 1 - smooth((age - t.flight - .35) / CircleClose) : 1);   // stays while the beam is up, as in the hit frame
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
    // On the shot: a thin air ring spreads from the first circle, gun smoke is pushed from the
    // muzzle along the line, and cyan speed lines fly past the circle.
    const gate = circles[0].c, rr = circles[0].r;
    shockRing('mb shock', gate, age, tier);
    muzzleSmoke('mb smoke', lift(move(hand, L.d, RifleLen * .72)), L.d, age);
    if (age < .3) for (let i = 0; i < 8; i++) {
      const v = age / .3, along = -.4 + rand(i + 800) * 1.4 + v * 1.6, acr = (rand(i + 810) - .5) * 1.8, len = .25 + rand(i + 820) * .4;
      const from = { x: gate.x + L.d.x * along - L.d.z * acr, z: gate.z + L.d.z * along + L.d.x * acr };
      streak(`mb speed ${i}`, from, move(from, L.d, len), .03, Cyan.withAlpha(.9 * (1 - v)), whiteGlow, Y + .09, 3);
    }

    // A small muzzle flash (the light is at the circle), the beam from the muzzle to the bullet, and the bullet.
    const mq = lift(move(hand, L.d, RifleLen * .72)), w = p.beamWidth * [1, 1.5, 2.2][tier];
    if (age < .07) glint('mb flash', mq, .4, 1 - age / .07, Beam, L.deg);
    beam('mb beam', L, flown, age, w, p.beamFade, s, tier, gate);
    if (flown < p.range) {
      const b = lift(move(L.start, L.d, flown));
      streak('mb bullet tail', move(b, L.d, -.9), b, w * 2.2, Beam.withAlpha(.9), whiteGlow, Y + .10, 6);
      sprite(b, .32, .28, Beam.withAlpha(.9), glow, Y + .11);
      streak('mb bullet core', move(b, L.d, -.2), move(b, L.d, .04), w * .8, White.withAlpha(1), whiteGlow, Y + .115, 4);
      sprite(move(b, L.d, .06), .07, .05, Smoke.withAlpha(.9), soft, Y + .116, L.deg);   // the small dark tip the source bullet has
    }
  },
};
