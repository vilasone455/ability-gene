// Paradise Lost v2 — E.G.O. weapon proposal. The pictures are ported to Source/RimArt/Ego/EgoParadiseLost* (previews
// "E.G.O.: paradise lost: ..."); no weapon, ability or rule draws them yet. v1 is not ported.
// v2 (2026-10-01) is v1 (ego-paradise-lost.js, left as it was) brought closer to the source after a
// side-by-side of the sketch with the Lobotomy and Ruina frames. The rules are unchanged. Changes:
//   Ring: the cross is twice the size (7 cells up, 4.8 each side, twice as wide); 40 spikes along the
//         floor instead of 16, 2.5x as wide, out to 2.4-4 cells; the ring is a filled red disc,
//         brightest at its edge (one soft rim-glow texture, lab/pl2-rim-glow), still the true 6-cell radius;
//         the pink-white flash at 60 % instead of 28 %, with a white core.
//   Thorns: maroon instead of bright red, about 40 % thinner, every thorn with 2-3 branches; a blood
//         spray of 10 drops (4 for the ring's small thorns) that land as spatter.
//   Wings (v2.1, after the user said the v2 wings read as thorn spikes): each of the three wings a
//         side is a notched arm with 5 covert and 6 flight feathers hanging from it like
//         shingles, instead of spokes from one point. Feathers are broad curved blades (a fifth
//         as wide as long) with a centre line, vane strokes and 2-3 blood blotches darker
//         toward the tip, stretched along it, and a dark red tip on about half. 66 feathers in all.
//   Staff: the wing is a fan of scale feathers in three rows, about a third of the staff long; the
//         snake winds the whole shaft in wide loops (3.5 turns).
//
// WhiteNight (Lobotomy Corporation, ALEPH): the staff of the Abnormality that turns twelve employees
// into Apostles. The rules are docs/ego-weapons.md, "Weapon 4: Paradise Lost" (added 2026-10-01).
// Every number is a placeholder and becomes an XML field on CompProperties_EgoWeapon:
//   The room hit: aim at one hostile in range 30 (line of sight to it, like any shot). Every
//         hostile in that pawn's room is hit, seen or not; outdoors, every hostile within 6 cells
//         of it. Damage ignores armor (Pale). 16 to one pawn, 12 each to 2-5, 9 each to 6 or more.
//         One shot every 2 s. Allies are never hit.
//   The slow: every pawn hit moves 60 % slower for 1 s.
//   Sanity: +1 mood per hostile hit, up to +10, 2 h (the SP half of Lobotomy's heal; the HP half
//         is dropped because Samehada and Mimicry already heal on hit).
//   Corrosion (shared system): bands 50 / 100 / 100 %, requirement Shooting 10, 30 s, one action
//         every 6 s, exhaustion 3 h (not drawn). The action: WhiteNight's ring, no target. Every
//         pawn of any faction within 6 cells of the wielder takes 12 armor-ignoring damage. Ring
//         hits never give Sanity.
//   Overclock: 3 rings in 6 s, hostiles only (allies inside are skipped), mood -20 for 1 day.
//
// Look (references in the doc's "Looks" table, looked at 2026-10-01):
//   Staff (Lobotomy weapon sprite): thin white shaft, a snake coiled round it, a red apple at the
//         head, a small gold halo of thorns, a grey feathered wing beside the apple, a gold tip.
//         Held upright here, gold tip on the floor, apple above the head.
//   Normal hit (Lobotomy footage, All Weapons Overview 12:42): red, angular, branching thorns burst
//         round each pawn hit, about 1.5x its height, about .6 s, then gone.
//   Corroded (Library of Ruina footage, All EGO Pages 7:57, and the Realization sprite): a red
//         thorn star (a red circle with long red spikes, compass-like) round the user; white wings
//         streaked with blood, three pairs; the gold thorn halo.
//   Ring (Ruina 8:19, shortened): a white cross of light, red curved slashes (left out here), a
//         burst of thin red spikes outward, an expanding red ring, a pink-white flash. The ring's
//         spiked outer edge is Lobotomy's breach ring.
//
// Order, "room hit" (default sliders): the wielder in the west of a 9 x 7 room, three raiders
// walking in from the east, a fourth raider in the next room east, a colonist in the room.
//   .40   shot 1 at the nearest raider (dashed aim line, lab aid): the apple and the halo flash .25 s
//   .46   maroon thorns rise out of the floor round every raider in the room in .08 s (8 per pawn, the
//         tallest 1.5x the pawn's height, 2-3 branches each), stand .6 s, sink in .3 s; 10 blood drops
//         thrown out that land as spatter; a red
//         flash on each body, a dark burst and holes on the floor that fade over 2.5 s. The raider
//         in the next room and the colonist get nothing. Each raider hit walks 60 % slower for 1 s
//         (a shrinking red ring at its feet, lab aid). Damage bars (lab aid) grow by 12 per raider.
//         Sanity pips under the wielder's feet (lab aid; in game a gizmo): +3.
//   2.40, 4.40  shots 2 and 3, the same. With 4 or more raiders the pips stop at 10.
// "room hit, outdoors": no walls; the hit takes every raider within 6 cells of the aimed one (a
// floor ring at the true radius shows .9 s). A raider behind the wielder, 7+ cells off, is missed.
// Order, "corroded" (the rule's 30 s with a ring every 6 s, cut to 3 rings 2.5 s apart):
//   0     the star grows on the floor round the wielder (ring .55 s, spikes after), the wings (three
//         a side, arms with feathers hanging from them, blood blotches) open
//         from folded along the back over .9 s, the halo lights over the head; a thin red ring at
//         the true radius 6 stays while corroded (lab aid)
//   .90   ring 1: a white cross of light on the wielder (up 7 cells and 4.8 cells each side at
//         chest height, .45 s), 40 red spikes shooting out along the floor to 2.4-4 cells (.45 s),
//         a filled red disc spreading from .4 to 6 cells in .5 s, brightest at its spiked edge,
//         a pink-white flash over the area (.3 s, 60 %). Every pawn inside, any
//         faction, is hit as the front passes: small red thorns, a red flash, a flinch. The
//         colonist 7 cells off is not touched. The Sanity pips do not move.
//   3.40, 5.90  rings 2 and 3
//   7.52  corrosion ends: the wings fold, the star and the halo fade over 1 s
// "overclock (hostiles only)": the same look, 3 rings 2 s apart; the colonist and the dog inside
// the ring are skipped (thin green rings), the raiders inside are hit, the raider outside is not.
//
// Drawing: the staff is upright, so it is a line north on screen for every facing; only its side
// of the body and whether it draws under the pawn change with the facing (facing east or west it
// stands at the front edge of the body, clear of the face). The star, the ring, the
// spikes along the floor and the halo are level circles and flat shapes, so they need no
// per-facing work. The cross is fixed to the screen (a column up, a bar across). The wings need a
// per-facing method: facing south or north both wings spread east and west (behind the pawn facing
// south, over it facing north); facing east or west they sweep back and up, the near wing over the
// pawn and the far one behind it, smaller. The thorns rise from the pawn's ground line (cell
// centre - .42); the ones behind the pawn draw under it. Pawns are lib/pawn.js real-size
// stand-ins (average body); the dog is the Chain Sickle beast stand-in; walls are the Paper Bomb
// kit's stand-ins.
import { AltitudeLayer, Color, Mathf, Meshes, MaterialPool, ShaderDatabase } from '../js/engine.js';
import { P, Y, Floor, Lift, sprite, band, soft, glow, rand } from './lib/six-paths-impact.js';
import { draw } from './lib/six-paths-solid.js';
import { pawn, at, pawnLayer, shadowLayer } from './lib/pawn.js';
import { tube, beast, Enemy, Holder } from './lib/chain-sickle.js';
import { line, streak, whiteGlow, ringAt } from './lib/goku.js';
import { walls } from './lib/paper-bomb.js';
import { registerLabTexture, pixels } from '../js/standins.js';

const clamp = Mathf.Clamp01, lerp = Mathf.Lerp, D2R = Mathf.Deg2Rad, TAU = Math.PI * 2;
const smooth = x => Mathf.Smooth(clamp(x));
const easeOut = x => 1 - Math.pow(1 - clamp(x), 3);
const unEase = u => 1 - Math.cbrt(1 - clamp(u));         // inverse of easeOut
const bump = x => (x >= 0 && x <= 1) ? Math.sin(x * Math.PI) : 0;
const disc = Meshes.disc(28, 'paradise disc');
const flatMat = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const floorLayer = AltitudeLayer.Floor.AltitudeFor();
// v2: the ring's glow, one soft disc that is clear in the middle and brightest just inside the rim
// (alpha rises as ((r - .35) / .62)^2.2 and drops to 0 over the last 3 %). A lab texture; the port draws
// it without a PNG, as a ring mesh whose UVs read the shipped SoftDisc at the radius with this alpha
// (EgoParadiseLostRingGraphics).
registerLabTexture('lab/pl2-rim-glow', () => pixels(256, (u, v) => {
  const r = Math.hypot(u - .5, v - .5) * 2, inner = Math.pow(Math.min(1, Math.max(0, (r - .35) / .62)), 2.2);
  return [1, 1, 1, r <= .97 ? inner : Math.max(0, 1 - (r - .97) / .03)];
}));
const rimGlow = MaterialPool.MatFrom('lab/pl2-rim-glow', ShaderDatabase.MoteGlow);
const dirOf = deg => ({ x: Math.cos(deg * D2R), z: Math.sin(deg * D2R) });
const side = d => ({ x: -d.z, z: d.x });                  // left of d
const add = (a, b, k = 1) => ({ x: a.x + b.x * k, z: a.z + b.z * k });
const mix = (a, b, t) => ({ x: lerp(a.x, b.x, t), z: lerp(a.z, b.z, t) });
const len2 = v => Math.hypot(v.x, v.z);
const dist = (a, b) => Math.hypot(a.x - b.x, a.z - b.z);
const unit2 = v => { const l = len2(v) || 1; return { x: v.x / l, z: v.z / l }; };
const degOf = v => Math.atan2(v.z, v.x) / D2R;

// Palette. Thorns and the star are WhiteNight's red; the staff and the wings white; the halo gold.
const Thorn = new Color(.50, .04, .07), ThornLit = new Color(.82, .18, .18), ThornDark = new Color(.22, .01, .03), Ink = new Color(.07, .02, .03);   // v2: maroon
const Gore = new Color(.42, .03, .05), Clot = new Color(.36, .02, .04);
const Gold = new Color(.96, .76, .26), GoldLit = new Color(1, .93, .62);
const Shaft = new Color(.94, .93, .90), ShaftLine = new Color(.36, .35, .38), Snake = new Color(.80, .80, .78), Eye = new Color(.05, .05, .05);
const Apple = new Color(.80, .07, .09), AppleLit = new Color(1, .48, .42), Stem = new Color(.28, .40, .20);
const Feather = new Color(.97, .96, .95), FeatherLine = new Color(.48, .48, .54), FeatherShade = new Color(.78, .78, .84), Smear = new Color(.66, .04, .06);
const WingBone = new Color(.90, .89, .88), WingGrey = new Color(.62, .62, .66), Star = new Color(.86, .07, .09), StarGlow = new Color(1, .22, .16);
const White = new Color(1, 1, 1), CrossRed = new Color(1, .30, .24), Pink = new Color(1, .78, .86);
const AllyRing = new Color(.45, .85, .45), PipOff = new Color(.22, .18, .10), RoomFloor = new Color(.52, .41, .29), Plank = new Color(.40, .31, .21);
const Colonist = new Color(.36, .50, .62), DogFur = new Color(.55, .42, .30);

// The rule's numbers that are fixed here (placeholders from the doc).
const DamageSingle = 16, DamageFew = 12, DamageMany = 9, FewMax = 5, SlowFactor = .4, SlowSeconds = 1, SanityCap = 10, Shots = 3, Rings = 3;
// Decided looks and timings.
const Lead = .4, HitDelay = .06, Snap = .08, Sink = .3, MarkFade = 2.5, StaffFlash = .25;
const Enter = .9, Exit = 1.0, CrossLife = .45, SpikeStart = .06, SpikeLife = .45, RingDelay = .12, FlashLife = .3;
const WalkSpeed = .8, StopAt = 1.6;                       // showcase walk, cells per second
// Body: lib/pawn.js average. Ground = a standing pawn's ground contact on screen; a point h cells up
// draws at z + Ground + h x Lift. PawnH = the pawn's height in lab cells.
const Ground = -.42, PawnH = (.63 + .54) / Lift, StaffH = 2.2, ChestH = (.05 - Ground) / Lift;
// Room: interior cells x -2..6, z -3..3 from the wielder's cell; walls round it.
const Room = { x0: -2, x1: 6, z0: -3, z1: 3 };
const RaiderSlots = [{ x: 4.2, z: -.6 }, { x: 5.4, z: 1.4 }, { x: 5.6, z: -2.2 }, { x: 3.6, z: 2.4 }, { x: 4.8, z: .6 }, { x: 5.8, z: -1.0 }, { x: 3.9, z: -2.4 }];
// Wings: three pairs, screen angles (degrees from east, up = north) for a wing spreading east,
// share of the wing span. Each pair has five feathers.
// Wings (v2.1): three arms a side at these screen angles (a wing spreading east) and shares of the
// reach; each with 5 coverts and 6 flight feathers. Reach = the wing span slider x 1.25.
const WingArms = [{ angle: 38, len: .5 }, { angle: 8, len: .55 }, { angle: -24, len: .45 }];
const Coverts = 5, Flights = 6, WingReach = 1.25;
// v2 staff wing: three rows of scale feathers at these distances from the root; the snake's loop width.
const WingRows = [.13, .23, .33], SnakeLoop = .075;
// v2 ring: the cross reaches 7 cells up and 4.8 cells each side; 40 spikes.
const CrossUp = 7, CrossSide = 4.8, Spikes = 40;

const scr = q => ({ x: q.x, z: q.z + Ground + q.h * Lift });
const shd = (q, sun) => ({ x: q.x + sun.x * q.h, z: q.z + Ground + sun.z * q.h });
const rot4Of = deg => Math.round((((deg % 360) + 360) % 360) / 90) % 4;   // 0 east, 1 north, 2 west, 3 south
const damageFor = n => n <= 1 ? DamageSingle : n <= FewMax ? DamageFew : DamageMany;
const inRoom = q => q.x > Room.x0 - .5 && q.x < Room.x1 + .5 && q.z > Room.z0 - .5 && q.z < Room.z1 + .5;

// ---- The staff ------------------------------------------------------------------------------------
// Upright in the right hand, gold tip on the floor. pos: the wielder's cell centre; rot4: facing.
// flash 0..1: the apple and the halo light up on a shot.
function staff(key, pos, rot4, flash, sun, strength) {
  const f = [0, 90, 180, 270][rot4], d = dirOf(f), right = { x: -side(d).x, z: -side(d).z };
  const sideways = rot4 === 0 || rot4 === 2;            // facing east or west: at the front edge of the body, clear of the face
  const g = add(add(pos, right, sideways ? .22 : .27), d, sideways ? .2 : .08), behind = g.z > pos.z + .02;
  const L = behind ? pawnLayer - .02 : pawnLayer + .02, out = right.x >= 0 ? 1 : -1;
  const P3 = h => ({ x: g.x, z: g.z, h });
  const base = scr(P3(0)), top = scr(P3(StaffH));
  line(`${key} shadow`, [shd(P3(0), sun), shd(P3(StaffH), sun)], .07, Ink.withAlpha(strength * .5), flatMat, shadowLayer, 'end');
  // Gold tip into the floor, then the shaft with its outline and a lit stripe.
  band(`${key} tip`, [{ x: base.x - .03, z: base.z + .07 }, { x: base.x + .03, z: base.z + .07 }], [{ x: base.x, z: base.z - .05 }, { x: base.x, z: base.z - .05 }], Gold, L);
  tube(`${key} line`, [base, top], () => .042, ShaftLine, L + .0002);
  tube(`${key} shaft`, [base, top], () => .028, Shaft, L + .0004);
  tube(`${key} lit`, [base, top], () => .028, White.withAlpha(.7), L + .0006, .2, .8);
  // The snake (v2): wide loops down the whole shaft, 3.5 turns, its head at the apple.
  const sx = u => g.x + SnakeLoop * Math.sin(u * 3.5 * TAU + .6);
  const coil = Array.from({ length: 57 }, (_, i) => { const u = i / 56; return { x: sx(u), z: g.z + Ground + lerp(.12, 2.0, u) * Lift }; });
  line(`${key} snake line`, coil, .056, ShaftLine, flatMat, L + .0008, 'none');
  line(`${key} snake`, coil, .036, Snake, flatMat, L + .001, 'none');
  const head = { x: sx(1) + .03 * out, z: g.z + Ground + 2.04 * Lift };
  draw(disc, head.x, L + .0012, head.z, .048, .034, 0, ShaftLine);
  draw(disc, head.x, L + .0013, head.z, .039, .026, 0, Snake);
  draw(disc, head.x + .014 * out, L + .0014, head.z + .006, .009, .009, 0, Eye);
  // The wing (v2, the Lobotomy sprite): a fan of scale-like feathers beside the apple on the outer
  // side, about a third of the staff long; three rows, darker toward the root, outer rows drawn first.
  const root = { x: g.x + .03 * out, z: g.z + Ground + 1.98 * Lift };
  [2, 1, 0].forEach(r => {
    const n = 4 + 2 * r, R = WingRows[r], fl = .1 + .025 * r, tone = Color.Lerp(WingGrey, Feather, .25 + .35 * r);
    for (let j = 0; j < n; j++) {
      const a = lerp(-100, 15, n === 1 ? .5 : j / (n - 1)) + 6 * (rand(r * 13 + j + 900) - .5), dd = { x: Math.cos(a * D2R) * out, z: Math.sin(a * D2R) };
      const b = add(root, dd, R - fl * .6), pts = [b, add(b, dd, fl * .5), add(b, dd, fl)];
      tube(`${key} wing line ${r} ${j}`, pts, u => .05 * Math.sin(Math.PI * Math.pow(u, .55)) + .012, ShaftLine, L + .0016 + (2 - r) * .0003 + j * .00002);
      tube(`${key} wing ${r} ${j}`, pts, u => .05 * Math.sin(Math.PI * Math.pow(u, .55)), tone, L + .00165 + (2 - r) * .0003 + j * .00002);
    }
  });
  // The apple and its stem; the halo of thorns round the shaft just above it (a level circle).
  const ap = { x: g.x, z: g.z + Ground + 2.12 * Lift }, hc = { x: g.x, z: g.z + Ground + 2.3 * Lift };
  ringAt(hc, .13, Gold, L + .0024, false, flatMat);
  for (let i = 0; i < 10; i++) {
    const dd = dirOf(i * 36 + 8), n = side(dd), b = add(hc, dd, .12);
    band(`${key} halo spike ${i}`, [add(b, n, .018), add(b, n, -.018)], [add(hc, dd, .2), add(hc, dd, .2)], Gold, L + .0025);
  }
  draw(disc, ap.x, L + .003, ap.z, .088, .082, 0, Ink);
  draw(disc, ap.x, L + .0032, ap.z, .075, .07, 0, Apple);
  draw(disc, ap.x - .025, L + .0034, ap.z + .025, .025, .02, 0, AppleLit);
  line(`${key} stem`, [{ x: ap.x, z: ap.z + .06 }, { x: ap.x + .02, z: ap.z + .11 }], .018, Stem, flatMat, L + .0035, 'none');
  if (flash > 0) {
    sprite(ap, .7 * flash, .7 * flash, StarGlow.withAlpha(.75 * flash), glow, Y + .1);
    sprite(hc, .55 * flash, .45 * flash, GoldLit.withAlpha(.6 * flash), glow, Y + .101);
  }
  return { head: ap };
}

// ---- The normal hit -------------------------------------------------------------------------------
// Maroon thorns rise out of the floor round a pawn and sink back, with a blood spray. g: the pawn's
// cell centre; age from the hit; scale: the tallest thorn in pawn heights; life: how long they stand.
function thorns(key, g, age, seed, scale, life, sun, strength, count = 8) {
  if (age < 0) return;
  const foot = { x: g.x, z: g.z + Ground }, mark = 1 - smooth((age - life) / MarkFade);
  if (mark > 0) sprite(foot, 1.0 * scale, .6 * scale, ThornDark.withAlpha(.45 * mark), soft, Floor + .02);
  if (age < .15) sprite({ x: g.x, z: g.z + .05 }, 1.1 * scale, 1.2 * scale, StarGlow.withAlpha(.6 * (1 - age / .15)), glow, Y + .09);
  // The blood spray (v2, Lobotomy's corridor frame): drops thrown out from the chest, landing as
  // spatter that fades with the floor marks.
  const drops = scale >= 1 ? 10 : 4;
  for (let j = 0; j < drops; j++) {
    const a = rand(seed * 5 + j + 700) * TAU, dd = { x: Math.cos(a), z: Math.sin(a) }, v = (.9 + .9 * rand(seed * 5 + j + 710)) * scale;
    const up = 1 + rand(seed * 5 + j + 720), gr = 7, land = (up + Math.sqrt(up * up + 2 * gr * ChestH)) / gr, tt = Math.min(age, land);
    const q = add(g, dd, v * tt), h = Math.max(0, ChestH + up * tt - .5 * gr * tt * tt), sz = .035 + .03 * rand(seed * 5 + j + 730);
    if (age < land) draw(disc, q.x, Y + .085, q.z + Ground + h * Lift, sz, sz, 0, Gore);
    else if (mark > 0) draw(disc, q.x, Floor + .022 + j * .0002, q.z + Ground, sz * 1.8, sz * 1.1, -degOf(dd), Gore.withAlpha(.85 * mark));
  }
  const k = age < Snap ? easeOut(age / Snap) : age < life ? 1 : 1 - smooth((age - life) / Sink);
  for (let i = 0; i < count; i++) {
    const a = (i / count + .07 * rand(seed + i)) * TAU, out = { x: Math.cos(a), z: Math.sin(a) }, n = side(out);
    const base = add(g, out, .14 + .2 * rand(seed + 10 + i));
    if (mark > 0) draw(disc, base.x, Floor + .021 + i * .0002, base.z + Ground, .04, .03, 0, Ink.withAlpha(.6 * mark));
    if (k <= 0) continue;
    const H = PawnH * scale * (i % 3 === 0 ? 1 : .55 + .3 * rand(seed + 20 + i)) * k;
    const lean = (.1 + .16 * rand(seed + 30 + i)) * H, kink = (rand(seed + 40 + i) - .5) * .3 * H;
    const p0 = { x: base.x, z: base.z, h: 0 };
    const p1 = { x: base.x + out.x * lean * .45 + n.x * kink, z: base.z + out.z * lean * .45 + n.z * kink, h: .55 * H };
    const p2 = { x: base.x + out.x * lean, z: base.z + out.z * lean, h: H };
    const w = (.035 + .03 * rand(seed + 50 + i)) * Math.max(.6, scale / 1.5), wf = u => w * Math.pow(1 - u, .9) + .003;
    const L = (base.z > g.z ? pawnLayer - .015 : Y + .03) + i * .0008;
    line(`${key} shadow ${i}`, [p0, p1, p2].map(q => shd(q, sun)), w * 1.6, Ink.withAlpha(strength * .4), flatMat, shadowLayer, 'end');
    const pts = [p0, p1, p2].map(scr);
    tube(`${key} line ${i}`, pts, u => wf(u) + .01, Ink, L);
    tube(`${key} body ${i}`, pts, wf, Thorn.withAlpha(.94), L + .0001);
    tube(`${key} dark ${i}`, pts, wf, ThornDark.withAlpha(.7), L + .0002, -1, -.25);
    tube(`${key} lit ${i}`, pts, wf, ThornLit.withAlpha(.8), L + .0003, .2, .75);
    // Two or three branches up the thorn, on alternate sides, rising outward.
    const nb = 2 + (rand(seed + 60 + i) > .5 ? 1 : 0);
    for (let b = 0; b < nb; b++) {
      const u = .28 + .2 * b + .08 * rand(seed + 70 + i * 3 + b), sg = (b + i) % 2 ? 1 : -1;
      const b0 = u < .55 ? { x: lerp(p0.x, p1.x, u / .55), z: lerp(p0.z, p1.z, u / .55), h: u * H } : { x: lerp(p1.x, p2.x, (u - .55) / .45), z: lerp(p1.z, p2.z, (u - .55) / .45), h: u * H };
      const BL = (.18 + .12 * rand(seed + 80 + i * 3 + b)) * H;
      const b1 = { x: b0.x + out.x * .35 * BL + n.x * sg * .5 * BL, z: b0.z + out.z * .35 * BL + n.z * sg * .5 * BL, h: b0.h + .6 * BL };
      const bp = [b0, b1].map(scr), bw = v => w * .6 * (1 - u * .5) * (1 - v) + .003;
      tube(`${key} branch line ${i} ${b}`, bp, v => bw(v) + .009, Ink, L + .0004 + b * .00005);
      tube(`${key} branch ${i} ${b}`, bp, bw, Thorn.withAlpha(.94), L + .0005 + b * .00005);
      tube(`${key} branch lit ${i} ${b}`, bp, bw, ThornLit.withAlpha(.75), L + .0006 + b * .00005, .2, .8);
    }
  }
}

// ---- The corroded look ----------------------------------------------------------------------------
// The red thorn star flat on the floor round the wielder: a red circle, 4 long spikes on the
// compass points, 4 shorter on the diagonals, 8 small ones between. amount 0..1.
function star(key, c, amount) {
  if (amount <= 0) return;
  const rk = easeOut(amount * 1.6), sk = smooth((amount - .25) / .75), R0 = .78;
  sprite(c, 4.4 * rk, 4.4 * rk, StarGlow.withAlpha(.16 * amount), glow, Floor + .044);
  draw(disc, c.x, Floor + .045, c.z, R0 * rk, R0 * rk, 0, ThornDark.withAlpha(.3 * amount));
  ringAt(c, R0 * rk + .02, Ink.withAlpha(.8 * amount), Floor + .046, true, flatMat);
  ringAt(c, R0 * rk, Star.withAlpha(amount), Floor + .047, true, flatMat);
  if (sk <= 0) return;
  for (let i = 0; i < 16; i++) {
    const cardinal = i % 4 === 0, diagonal = i % 4 === 2, Ls = (cardinal ? 1.15 : diagonal ? .72 : .3) * sk, hw = cardinal ? .12 : diagonal ? .09 : .05;
    const d = dirOf(i * 22.5 + 90), n = side(d), b = add(c, d, R0 * rk - .02), tip = add(c, d, R0 * rk + Ls);
    band(`${key} spike line ${i}`, [add(b, n, hw + .025), add(b, n, -hw - .025)], [add(tip, d, .04), add(tip, d, .04)], Ink.withAlpha(.8 * amount), Floor + .048 + i * .0002);
    band(`${key} spike ${i}`, [add(b, n, hw), add(b, n, -hw)], [tip, tip], Star.withAlpha(amount), Floor + .0481 + i * .0002);
    band(`${key} spike lit ${i}`, [add(b, n, hw * .9), b], [tip, tip], StarGlow.withAlpha(.55 * amount), Floor + .0482 + i * .0002);
  }
}

// White wings streaked with blood, built as the Ruina Realization sprite draws them: three wings a
// side, each a curved arm with notches along it, five short covert feathers along the arm and six
// long flight feathers from its outer half, overlapping like shingles. A feather is a broad curved
// blade (a fifth as wide as long, widest at 40 %, a thin quill at the base) with a grey line down the
// middle, two grey strokes on the vane and two or three blood blotches, darker toward the tip.
// open 0..1 (folded down along the back at 0). Facing south or north both sides spread out; facing
// east or west they sweep back and up, the near side over the pawn and the far side behind it.
// mir: +1 for a wing spreading east, -1 west, so the curve and the shaded side mirror with it.
function feather(key, base, ang, Lf, broad, seed, layer, mir = 1) {
  const fd = { x: Math.cos(ang * D2R), z: Math.sin(ang * D2R) }, fn = { x: -fd.z * mir, z: fd.x * mir }, bend = .08 * Lf;
  const spine = u => add(add(base, fd, Lf * u), fn, bend * u * u);
  const W = Lf * broad / 2;
  const wf = u => W * (u < .4 ? Math.pow(Math.sin(Math.PI / 2 * u / .4), .8) : Math.pow((1 - u) / .6, .85)) + .003;
  const pts = Array.from({ length: 9 }, (_, i) => spine(i / 8));
  tube(`${key} line`, pts, u => wf(u) + .01, FeatherLine, layer);
  tube(`${key} fill`, pts, wf, Feather, layer + .00002);
  tube(`${key} shade`, pts, wf, FeatherShade.withAlpha(.55), layer + .00004, mir > 0 ? -1 : .15, mir > 0 ? -.15 : 1);
  line(`${key} rachis`, Array.from({ length: 6 }, (_, i) => spine(.05 + .85 * i / 5)), .014, FeatherLine.withAlpha(.75), flatMat, layer + .00006, 'end');
  for (let h = 0; h < 2; h++) {
    const u = .35 + .25 * h, a = spine(u), b = add(spine(u + .1), fn, -wf(u + .1) * .85);
    line(`${key} hatch ${h}`, [a, b], .01, FeatherLine.withAlpha(.5), flatMat, layer + .00007, 'none');
  }
  // Blood: smears stretched along the feather, each two overlapping ovals so the edge is uneven;
  // on about half the feathers the tip is dipped dark red.
  const blots = 2 + (rand(seed + 3) > .5 ? 1 : 0), rot = -degOf(fd);
  for (let b = 0; b < blots; b++) {
    const u = 1 - .7 * Math.pow(rand(seed + 10 + b), 1.5), c = add(spine(u), fn, (rand(seed + 20 + b) - .5) * .7 * wf(u));
    const rx = W * (.9 + 1.0 * rand(seed + 30 + b)) * (.6 + .6 * u), rz = Math.min(wf(u) * .7, rx * .35);
    const c2 = add(add(c, fd, rx * (.35 + .3 * rand(seed + 40 + b))), fn, (rand(seed + 50 + b) - .5) * rz);
    draw(disc, c.x, layer + .00008 + b * .00001, c.z, rx, rz, rot, (u > .7 ? Clot : Smear).withAlpha(.78));
    draw(disc, c2.x, layer + .000085 + b * .00001, c2.z, rx * .55, rz * .75, rot + 8 * (rand(seed + 60 + b) - .5), (u > .6 ? Clot : Smear).withAlpha(.7));
  }
  if (rand(seed + 7) > .5) {
    const tp = Array.from({ length: 4 }, (_, i) => spine(.8 + .2 * i / 3));
    tube(`${key} tip`, tp, u => wf(.8 + .2 * u) * .95, Clot.withAlpha(.7), layer + .00009);
  }
}
function wings(key, pos, rot4, open, span, s, sun, strength) {
  if (open <= 0) return;
  const sideView = rot4 === 0 || rot4 === 2, back = rot4 === 0 ? -1 : 1, R = span * WingReach;
  const root = { x: pos.x, z: pos.z + .14 };
  sprite({ x: pos.x + sun.x * 1.6, z: pos.z + Ground + sun.z * 1.6 }, (sideView ? 1.8 : 3.2) * span * open, 1.1 * open, Ink.withAlpha(strength * .25 * open), soft, shadowLayer);
  const list = sideView
    ? [{ sx: back, k: .82, layer: pawnLayer - .03, dx: .06 * back, dz: .1 }, { sx: back, k: 1, layer: pawnLayer + .03, dx: 0, dz: 0 }]
    : [-1, 1].map(sx => ({ sx, k: 1, layer: rot4 === 3 ? pawnLayer - .03 : pawnLayer + .03, dx: 0, dz: 0 }));
  list.forEach((W, wi) => {
    const r0 = { x: root.x + W.dx, z: root.z + W.dz }, sx = W.sx;
    // A screen angle for a wing spreading east, mirrored for the west side.
    const screen = deg => ({ x: Math.cos(deg * D2R) * sx, z: Math.sin(deg * D2R) });
    const angOf = deg => degOf(screen(deg));
    [2, 1, 0].forEach(ai => {                          // lower wing first, the upper one on top
      const arm = WingArms[ai], e = smooth(open * 1.4 - ai * .15), k = W.k * (sideView ? .85 : 1) * lerp(.45, 1, e);
      const th = lerp(-100, arm.angle + (sideView ? 20 : 0), e) + 3 * Math.sin(s * 2.4 + ai + wi) * e;
      const d = screen(th), up = { x: -Math.sin(th * D2R) * sx, z: Math.cos(th * D2R) }, AL = R * arm.len * k;
      const armAt = t => add(add(r0, d, AL * t), up, .12 * AL * Math.sin(Math.PI * t));
      const L0 = W.layer + (2 - ai) * .006 + wi * .0001;
      // Flight feathers from the outer half, outer first so the inner ones lie over them.
      for (let j = Flights - 1; j >= 0; j--) {
        const t = .45 + .55 * j / (Flights - 1), rel = lerp(70, 10, j / (Flights - 1)) * e;
        feather(`${key} flight ${wi} ${ai} ${j}`, armAt(t), angOf(th - rel), R * .62 * k * (.8 + .25 * j / (Flights - 1)), .2, ai * 97 + j * 7 + wi * 300, L0 + (Flights - j) * .0002, sx);
      }
      // Coverts along the arm, over the flight feathers.
      for (let j = Coverts - 1; j >= 0; j--) {
        const t = .12 + .7 * j / (Coverts - 1), rel = lerp(62, 30, j / (Coverts - 1)) * e;
        feather(`${key} covert ${wi} ${ai} ${j}`, armAt(t), angOf(th - rel), R * .28 * k * (.85 + .3 * j / (Coverts - 1)), .25, ai * 53 + j * 11 + wi * 500, L0 + .002 + (Coverts - j) * .0002, sx);
      }
      // The arm on top: a pale ridge with notches.
      const ap = Array.from({ length: 9 }, (_, i) => armAt(i / 8));
      tube(`${key} arm line ${wi} ${ai}`, ap, u => .045 * (1 - .5 * u) + .012, FeatherLine, L0 + .0035);
      tube(`${key} arm ${wi} ${ai}`, ap, u => .045 * (1 - .5 * u), WingBone, L0 + .0036);
      for (let n = 0; n < 6; n++) {
        const t = .15 + .13 * n, c = armAt(t), w = .04 * (1 - .5 * t);
        line(`${key} notch ${wi} ${ai} ${n}`, [add(c, up, w), add(c, up, -w)], .012, FeatherLine, flatMat, L0 + .0037, 'none');
      }
    });
  });
}

// The gold thorn halo over the head: a level circle with twelve spikes, turning slowly.
function halo(key, pos, amount, s, who) {
  if (amount <= 0) return;
  const c = { x: pos.x, z: at(pos, 'headTop', who).z + .16 }, r = .2 * easeOut(amount), L = pawnLayer + .05;
  sprite(c, .9 * amount, .7 * amount, Gold.withAlpha(.35 * amount), glow, L - .001);
  ringAt(c, r, Gold.withAlpha(amount), L, false, flatMat);
  for (let i = 0; i < 12; i++) {
    const d = dirOf(i * 30 + s * 12), n = side(d), b = add(c, d, r * .95);
    band(`${key} spike ${i}`, [add(b, n, .025), add(b, n, -.025)], [add(c, d, r + .09 * amount), add(c, d, r + .09 * amount)], Gold.withAlpha(amount), L + .0002);
  }
}

// ---- The ring -------------------------------------------------------------------------------------
// One firing of the corrosion action at the wielder's cell c, age from the firing: the cross of
// light, red spikes along the floor, the red disc spreading to R (brightest at its edge), the
// pink-white flash. v2: the cross is twice the size, 40 wider spikes, a filled disc, the flash at 60 %.
function ring(key, c, age, R, spread) {
  if (age < 0) return;
  const chest = { x: c.x, z: c.z + .05 };
  if (age < CrossLife) {
    const a = age < .06 ? age / .06 : 1 - smooth((age - .12) / (CrossLife - .12));
    const foot = { x: c.x, z: c.z + Ground }, topP = { x: c.x, z: c.z + Ground + CrossUp * Lift };
    streak(`${key} column glow`, foot, topP, 1.6, CrossRed.withAlpha(.5 * a), whiteGlow, Y + .2);
    streak(`${key} column`, foot, topP, .44, White.withAlpha(a), whiteGlow, Y + .201);
    streak(`${key} bar glow`, add(chest, { x: -CrossSide, z: 0 }), add(chest, { x: CrossSide, z: 0 }), 1.2, CrossRed.withAlpha(.5 * a), whiteGlow, Y + .202);
    streak(`${key} bar`, add(chest, { x: -CrossSide, z: 0 }), add(chest, { x: CrossSide, z: 0 }), .32, White.withAlpha(a), whiteGlow, Y + .203);
    sprite(chest, 2.6 * a, 2.6 * a, White.withAlpha(.8 * a), glow, Y + .204);
  }
  const sa = age - SpikeStart;
  if (sa >= 0 && sa < SpikeLife) {
    const grow = easeOut(sa / .18), fade = 1 - smooth((sa - (SpikeLife - .25)) / .25), k = Math.min(1, R / 6);
    for (let i = 0; i < Spikes; i++) {
      const d = dirOf(i * 360 / Spikes + 4 + 6 * (rand(i + 500) - .5)), Ls = lerp(.9, (2.4 + 1.6 * rand(i + 510)) * k, grow);
      const pts = [add(c, d, .7), add(c, d, (.7 + Ls) / 2), add(c, d, Ls)];
      line(`${key} spike glow ${i}`, pts, .7, StarGlow.withAlpha(.55 * fade), whiteGlow, Floor + .06, 'end');
      line(`${key} spike ${i}`, pts, .3, Star.withAlpha(.95 * fade), flatMat, Floor + .061, 'end');
    }
  }
  const ra = age - RingDelay;
  if (ra >= 0 && ra < spread + .35) {
    const x = ra / spread, r = lerp(.4, R, easeOut(x)), a = x < 1 ? 1 : 1 - (ra - spread) / .35;
    draw(disc, c.x, Floor + .063, c.z, r, r, 0, Star.withAlpha(.24 * a), flatMat);
    sprite(c, 2 * r, 2 * r, StarGlow.withAlpha(.85 * a), rimGlow, Floor + .068);
    ringAt(c, r, StarGlow.withAlpha(.5 * a), Floor + .07, true, whiteGlow);
    ringAt(c, r, White.withAlpha(.85 * a), Floor + .071, false, whiteGlow);
    for (let i = 0; i < 28; i++) {
      const d = dirOf(i * 360 / 28), n = side(d), b = add(c, d, r);
      band(`${key} edge spike ${i}`, [add(b, n, .05), add(b, n, -.05)], [add(c, d, r + .28 * a), add(c, d, r + .28 * a)], Star.withAlpha(.9 * a), Floor + .072);
    }
  }
  const fa = ra - spread;
  if (fa >= 0 && fa < FlashLife) {
    const f = 1 - fa / FlashLife;
    sprite(c, 2.4 * R, 2.4 * R, Pink.withAlpha(.6 * f), glow, Y + .19);
    sprite(c, 1.2 * R, 1.2 * R, White.withAlpha(.4 * f), glow, Y + .191);
  }
}

// ---- Lab aids -------------------------------------------------------------------------------------
// Sanity pips: ten gold pips in two rows under the wielder's feet (above the head they would sit on
// the halo).
function pips(pos, count, alpha) {
  if (alpha <= 0) return;
  for (let i = 0; i < SanityCap; i++) {
    const c = { x: pos.x - .32 + (i % 5) * .16, z: pos.z - .72 - Math.floor(i / 5) * .15 }, fill = clamp(count - i);
    draw(disc, c.x, Floor + .05, c.z, .06, .06, 0, Ink.withAlpha(.75 * alpha));
    draw(disc, c.x, Floor + .051, c.z, .047, .047, 0, Color.Lerp(PipOff, Gold, fill).withAlpha(alpha * (.4 + .6 * fill)));
  }
}
// Damage bar under a pawn: one red segment per hit, 0.022 cells per point of damage.
function damageBar(key, pos, hits, s) {
  const done = hits.filter(h => h.t <= s);
  if (!done.length) return;
  let x = pos.x - .4;
  const z = pos.z - .74;
  done.forEach((h, i) => {
    const w = h.dmg * .022;
    band(`${key} ${i}`, [{ x, z: z - .03 }, { x: x + w - .02, z: z - .03 }], [{ x, z: z + .03 }, { x: x + w - .02, z: z + .03 }], Thorn, Floor + .052);
    x += w;
  });
}

// ---- Timelines ------------------------------------------------------------------------------------
const isRoom = p => p.mode.startsWith('room'), isOutdoors = p => p.mode === 'room hit, outdoors';
const isCorroded = p => p.mode === 'corroded', isOverclock = p => p.mode.startsWith('overclock');

// Seconds of walking a pawn has done by s, its slow windows taken off.
function walked(q, s) {
  let slow = 0;
  for (const h of q.hits) slow += Math.max(0, Math.min(s, h.t + SlowSeconds) - Math.max(0, h.t));
  return Math.max(0, s - (1 - SlowFactor) * slow);
}
function posAt(q, s) {
  if (!q.walks) return q.start;
  const d0 = len2(q.start), u = unit2({ x: -q.start.x, z: -q.start.z });
  return add(q.start, u, Math.min(WalkSpeed * walked(q, s), Math.max(0, d0 - StopAt)));
}

// Who stands where and who is hit when. People: { start, kind, hostile, walks, skipped, hits }.
function plan(p) {
  const out = { people: [], shots: [], rings: [], enter: null, exit: null, end: 0 };
  const person = (start, kind, more = {}) => out.people.push({ start, kind, hits: [], walks: false, hostile: false, skipped: false, ...more });
  if (isRoom(p)) {
    for (let i = 0; i < p.raiders; i++) person(RaiderSlots[i], 'raider', { hostile: true, walks: true });
    person(isOutdoors(p) ? { x: -4.6, z: 3.0 } : { x: 8.6, z: .4 }, 'raider', { hostile: true, away: true });
    person({ x: .8, z: 2.0 }, 'colonist', { skipped: true });
    for (let k = 0; k < Shots; k++) {
      const t = Lead + k * p.shotInterval, hitT = t + HitDelay;
      const seen = out.people.filter(q => q.hostile && (isOutdoors(p) || inRoom(posAt(q, t))));
      seen.sort((a, b) => len2(posAt(a, t)) - len2(posAt(b, t)));
      const aimed = seen[0], centre = posAt(aimed, t);
      const hit = out.people.filter(q => q.hostile && (isOutdoors(p) ? dist(posAt(q, t), centre) <= p.outdoorRadius : inRoom(posAt(q, t))));
      const dmg = damageFor(hit.length);
      for (const q of hit) q.hits.push({ t: hitT, dmg });
      out.shots.push({ t, hitT, aimed: out.people.indexOf(aimed), centre, n: hit.length });
    }
    const last = out.shots[Shots - 1];
    out.end = last.hitT + p.thornLife + Sink + p.hold;
    return out;
  }
  const corroded = isCorroded(p);
  if (corroded) {
    person({ x: 1.3, z: .7 }, 'colonist'); person({ x: -2.6, z: -1.5 }, 'raider', { hostile: true });
    person({ x: 3.4, z: -2.4 }, 'dog'); person({ x: -4.2, z: 3.6 }, 'colonist'); person({ x: 6.6, z: 2.3 }, 'colonist');
  } else {
    person({ x: 1.3, z: .7 }, 'colonist', { skipped: true }); person({ x: -2.6, z: -1.5 }, 'raider', { hostile: true });
    person({ x: 3.2, z: 2.0 }, 'raider', { hostile: true }); person({ x: 3.4, z: -2.4 }, 'dog', { skipped: true });
    person({ x: -6.4, z: 2.6 }, 'raider', { hostile: true });
  }
  out.enter = 0;
  const every = corroded ? p.ringInterval : p.overclockInterval;
  for (let k = 0; k < Rings; k++) {
    const t = Enter + k * every;
    out.rings.push(t);
    for (const q of out.people) {
      const d = len2(q.start);
      if (d > p.ringRadius || (!corroded && !q.hostile)) continue;
      q.hits.push({ t: t + RingDelay + p.spread * unEase((d - .4) / (p.ringRadius - .4)), dmg: 12 });
    }
  }
  out.exit = out.rings[Rings - 1] + RingDelay + p.spread + 1.0;
  out.end = out.exit + Exit + p.hold;
  return out;
}

// ---- Drawing --------------------------------------------------------------------------------------
function drawFrame(s, p, o, sun, strength) {
  const PL = plan(p), who = { body: 'average', sun, shadow: strength }, room = isRoom(p);
  const armed = !room, look = armed ? smooth(s / Enter) * (1 - smooth((s - PL.exit) / Exit)) : 0;

  // The room: a plank floor and stone walls round it (not outdoors).
  if (p.actors && room && !isOutdoors(p)) {
    const a = { x: o.x + Room.x0 - .5, z: o.z + Room.z0 - .5 }, b = { x: o.x + Room.x1 + .5, z: o.z + Room.z1 + .5 };
    band('pl2 floor', [a, { x: b.x, z: a.z }], [{ x: a.x, z: b.z }, b], RoomFloor, floorLayer + .01);
    for (let z = Room.z0; z <= Room.z1 + 1; z++) line(`pl2 plank ${z}`, [{ x: a.x, z: o.z + z - .5 }, { x: b.x, z: o.z + z - .5 }], .03, Plank, flatMat, floorLayer + .011, 'none');
    const cells = [];
    for (let x = Room.x0 - 1; x <= Room.x1 + 1; x++) cells.push({ x: o.x + x, z: o.z + Room.z0 - 1 }, { x: o.x + x, z: o.z + Room.z1 + 1 });
    for (let z = Room.z0; z <= Room.z1; z++) cells.push({ x: o.x + Room.x0 - 1, z: o.z + z }, { x: o.x + Room.x1 + 1, z: o.z + z });
    walls('pl2 walls', o, cells, sun, strength);
  }

  // The wielder and the facing: toward the aimed raider in the room hit, the slider otherwise.
  const pos = { x: o.x, z: o.z };
  let facing = p.facing;
  if (room) {
    const sh = PL.shots.filter(x => x.t <= s).pop() ?? PL.shots[0];
    facing = degOf(posAt(PL.people[sh.aimed], Math.max(0, s)));
  }
  const rot4 = rot4Of(facing);

  // Corroded: the star, the true radius, the rings.
  if (armed) {
    star('pl2 star', pos, look);
    if (p.actors && look > 0) ringAt(pos, p.ringRadius, Star.withAlpha(.45 * look), Floor + .04, false, flatMat);
    PL.rings.forEach((t, k) => ring(`pl2 ring ${k}`, pos, s - t, p.ringRadius, p.spread));
  }

  // The people: walking, slowed, flinching at each hit, thorns at each hit.
  PL.people.forEach((q, i) => {
    const g0 = add(o, posAt(q, s)), away = unit2(posAt(q, s));
    let off = { x: 0, z: 0 };
    for (const h of q.hits) { const a = s - h.t; if (a >= 0 && a < .25) off = add(off, away, .07 * bump(a / .25)); }
    const g = add(g0, off);
    if (p.actors && q.skipped) ringAt(g0, .45, AllyRing.withAlpha(.7), Floor + .04, false, flatMat);
    if (p.actors && q.kind === 'dog') beast(g, .5, sun, strength, DogFur);
    else if (p.actors) pawn(g, { ...who, shirt: q.kind === 'raider' ? Enemy : Colonist });
    q.hits.forEach((h, j) => {
      const age = s - h.t;
      if (room) {
        thorns(`pl2 thorns ${i} ${j}`, g0, age, i * 31 + j * 7, p.thornScale, p.thornLife, sun, strength);
        if (p.actors && age >= 0 && age < SlowSeconds) ringAt({ x: g0.x, z: g0.z + Ground + .05 }, lerp(.42, .28, age / SlowSeconds), StarGlow.withAlpha(.6 * (1 - age / SlowSeconds)), Floor + .055, false, flatMat);
      } else thorns(`pl2 thorns ${i} ${j}`, g0, age, i * 31 + j * 7, .55, .3, sun, strength, 6);
    });
    if (p.actors && room && q.hostile) damageBar(`pl2 dmg ${i}`, g0, q.hits, s);
  });

  // The wielder: wings behind or over, the pawn, the staff, the halo.
  wings('pl2 wings', pos, rot4, look, p.wingSpan, s, sun, strength);
  if (p.actors) pawn(pos, { ...who, shirt: Holder });
  const shot = PL.shots.filter(x => x.t <= s).pop(), flash = shot ? 1 - clamp((s - shot.t) / StaffFlash) : 0;
  const ringFlash = PL.rings.reduce((m, t) => Math.max(m, s >= t && s - t < .35 ? 1 - (s - t) / .35 : 0), 0);
  const st = staff('pl2 staff', pos, rot4, Math.max(flash, ringFlash), sun, strength);
  halo('pl2 halo', pos, look, s, who);

  // Lab aids: the aim line, the outdoor radius, the Sanity pips.
  if (!p.actors) return;
  if (room) {
    for (const sh of PL.shots) {
      const age = s - sh.t;
      if (age < 0 || age > .9) continue;
      if (age < .4) {
        const to = at(add(o, posAt(PL.people[sh.aimed], sh.t)), 'chest', who), k = bump(age / .4);
        for (let j = 0; j < 10; j++) line(`pl2 aim ${sh.t} ${j}`, [mix(st.head, to, j / 10 + .02), mix(st.head, to, j / 10 + .07)], .03, Star.withAlpha(.7 * k), flatMat, Y + .1, 'none');
      }
      if (isOutdoors(p)) ringAt(add(o, sh.centre), p.outdoorRadius, Star.withAlpha(.5 * (1 - age / .9)), Floor + .04, false, flatMat);
    }
    const gained = PL.people.reduce((n, q) => n + (q.hostile ? q.hits.filter(h => h.t <= s).length : 0), 0);
    pips(pos, Math.min(SanityCap, gained), 1);
  } else pips(pos, 4, look);   // corroded: the pips stay where they were
}

export default {
  kit: 'E.G.O. weapons', label: 'Paradise Lost v2 (sketch)',
  params: {
    mode: { label: 'Show', value: 'room hit', options: ['room hit', 'room hit, outdoors', 'corroded', 'overclock (hostiles only)'], group: 'Showcase' },
    // Off: only what the C# port draws (no stand-ins, room or lab aids), for comparing with the recording.
    actors: { label: 'Stand-in pawns, room and lab aids', value: true, group: 'Showcase' },
    facing: P('Corroded: facing (degrees)', 270, 0, 360, 90, 'Showcase'),
    raiders: P('Room hit: raiders in the room', 3, 1, 7, 1, 'Showcase'),
    shotInterval: P('Room hit: a shot every (s)', 2.0, 1, 3, .1, 'Rule'),
    outdoorRadius: P('Outdoors radius (cells)', 6, 3, 9, .5, 'Rule'),
    ringInterval: P('Corroded: a ring every (s, rule 6)', 2.5, 1.5, 6, .1, 'Rule'),
    overclockInterval: P('Overclock: a ring every (s)', 2.0, 1, 3, .1, 'Rule'),
    ringRadius: P('Ring radius (cells)', 6, 3, 9, .5, 'Rule'),
    thornScale: P('Thorns: tallest (x pawn height)', 1.5, .8, 2, .05, 'Shape'),
    wingSpan: P('Wing span, each side (cells)', 1.3, .8, 2, .05, 'Shape'),
    thornLife: P('Thorns stand', .6, .3, 1.2, .05, 'Timing (s)'),
    spread: P('Ring spreads', .5, .25, 1, .05, 'Timing (s)'),
    hold: P('Show the result', 1.2, .3, 3, .1, 'Timing (s)'),
  },
  duration(p) { return plan(p).end; },
  phases(p) {
    const PL = plan(p), out = [];
    if (isRoom(p)) PL.shots.forEach((sh, i) => out.push({ name: `Shot ${i + 1} (${sh.n} hit)`, t: sh.t }));
    else {
      out.push({ name: isCorroded(p) ? 'Corroded' : 'Overclock', t: 0 });
      PL.rings.forEach((t, i) => out.push({ name: `Ring ${i + 1}`, t }));
      out.push({ name: 'Ends', t: PL.exit });
    }
    out.push({ name: 'Result', t: PL.end - p.hold });
    return out;
  },
  events(p) {
    const PL = plan(p), out = [];
    if (isCorroded(p)) out.push({ t: 0, type: 'sound', def: 'RimArt_ParadiseCorrode' });
    for (const sh of PL.shots) out.push({ t: sh.t, type: 'sound', def: 'RimArt_ParadiseHit' }, { t: sh.hitT, type: 'shake', value: .012 });
    for (const t of PL.rings) out.push({ t, type: 'sound', def: 'RimArt_ParadiseRing' }, { t: t + RingDelay, type: 'shake', value: .035 });
    return out;
  },

  draw(s, p, { origin: o, scene }) {
    if (s < 0 || s >= plan(p).end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    drawFrame(s, p, o, sun, strength);
  },
};
