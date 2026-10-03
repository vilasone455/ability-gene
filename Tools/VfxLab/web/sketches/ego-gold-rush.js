// Gold Rush — E.G.O. weapon proposal (sketch only; nothing of it is in C# yet).
// The King of Greed (Lobotomy Corporation O-01-64, WAW; the weapon itself is rated ALEPH): one golden
// gauntlet far bigger than the hand. Source, checked 2026-10-03 through search summaries (the wikis are
// blocked from this session; the doc's "Source, checked" row should say so):
//   Lobotomy weapon: fist, Red 5-6, Fast (1.5), Very Short (2), Fortitude 5, level 5. Each attack has a
//         10 % chance to add +5 to both ends of its damage for 12 s and cut the wielder's Temperance by
//         50 % for 120 s. "One can release their primal desires and strike enemies with full force;
//         technical skill is unneeded."
//   Ruina page (Floor of Natural Sciences): cost 5, Mass Attack (Individual), one Blunt die 5-15 rolled
//         against every enemy; on hit restore 1 Light and gain 1 Strength next Scene. Greed as taking
//         from everyone and being paid back for each hit.
//   The Abnormality: a crowned golden girl whose breach is a huge-mouthed crawling thing; anything in
//         front of the mouth takes 390-410 Red, which kills outright. The devouring is hers, not the fist's.
// The rules this sketch shows. Every number is a placeholder and becomes an XML field on the weapon's
// CompProperties_EgoWeapon; the picture's own timings are the constants below:
//   The fist: melee, 1 cell, fast: a punch every .5 s (the Lobotomy weapon is the fast one, not a slow
//         heavy hit). Weight goes into the hit, not the swing.
//   The greedy punch (greedChance 10 % per hit, from both games' pay-back): the fist opens into a mouth
//         for one bite at 2x damage (greedDamageFactor). The wielder is paid: +25 % melee damage for 12 s
//         (greedDamage, greedSeconds). The wielder pays: mood -8 for 1 day (greedMood, greedDays), which
//         feeds the corrosion roll. The sketch shows the pay-back as a glow sliding from the fist into
//         the chest, the buff as a gold aura on the fist, the cost as dark flecks off the head.
//   Corrosion (shared system): bands 25 / 75 / 100 %, requirement Melee 6, 30 s, one action every 1.2 s;
//         WAW numbers. The action: the wielder walks to the nearest standing pawn of any faction (downed
//         pawns are skipped, as in the Mimicry sketch) and bites it with the fist, then the chest mouth
//         swallows: the wielder heals a share of the bite (corrodedHeal). Exhaustion 2 h (not drawn).
//   Overclock: 5 bites 1 s apart at hostiles only, holding still, mood -15 for 1 day (not drawn).
//
// Look. Two golds: a dull brushed gold for mass, a hot white-gold for light, black for every strong
// moment (the mouth's inside, the cracks, the eyes). The fist is a flat shape level at hand height that
// turns with the aim (no per-facing method), drawn under the pawn when it points north (the Vergil rule).
// Corroded, the King gilds the wielder from the gauntlet hand up (Magic Bullet draws around the pawn,
// Solemn Lament above it, Mimicry replaces a limb; this one takes the surface): a gold wash climbs the
// body in 2 s with a bright edge, a crown lands on the head, the eyes go black, and the chest becomes
// her mouth, a closed seam between firings that opens for the swallow. A gold stain spreads under the
// wielder and stays where it has stood (the trail says the state is still on). At the end the gold
// cracks and falls off as flakes. Overclock gilds only to the waist: no crown, no eyes, no trail.
//
// Order, "punches" (default sliders): three punches .5 s apart at a raider 1 cell off.
//   .30   punch 1: the fist shoots out .9 cells in .05 s, holds .03 s, comes back in .15 s. Hit at the end
//         of the thrust: a white-gold flash, a gold ring on the ground, five cracks that glow then stay
//         dark, eight gold flakes thrown that land and stay; the raider rocks .07 cells. Shake .02.
//   .80   punch 2.   1.30  punch 3.
// Order, "greedy punch": two punches, then at 1.30 the bite:
//   +0    thrust .05 s; +.05 the knuckles split into two jaws over .08 s (the mouth is .6 cells across,
//         black inside, six gold teeth per jaw); +.13 the jaws snap shut in .07 s on the raider, who goes
//         down: a bigger ring, six coins spinning out, the cracks and flakes. Shake .05.
//   +.20  pay-back: a gold glow slides from the fist into the chest in .35 s and the chest glows; then
//         the fist keeps a gold aura for the buff (shown 1.5 s, the rule says 12 s) and ten dark flecks
//         fall off the head for 1 s (the cost).
// Order, "corroded" (the rule's 30 s cut to about 9 s): an ally next to the wielder, a raider 3 cells off.
//   0     the gold climbs the wielder in 2 s; the stain spreads to 1 cell under the feet
//   2.0   the crown drops onto the head (4 frames, a ring of light); the eyes go black; the chest seam shows
//   2.0   bite 1 at the nearest standing pawn: the ally. +.5 s the chest mouth opens and shuts (the swallow)
//   3.2   bite 2: the ally again, who goes down
//   4.4   nobody standing is adjacent: the wielder walks to the raider (3 cells/s, dust at the feet, the
//         stain trails), bites at arrival
//   ~6.2  bite 4: the raider goes down
//   ~7.0  the state ends: cracks over the gold for .17 s, then the wash falls off as flakes in 1 s, the
//         crown drops and vanishes, the stain fades over the hold
// "overclock (hostiles only)": gold to the waist, no crown; five bites 1 s apart at two raiders next to
// the wielder; the ally standing next to the wielder is never chosen (a thin green ring marks it).
// Lab aids: a dashed gold line to each chosen target. "Stand-in pawns, props and lab aids" off leaves
// what a C# port draws: no pawns, green ring or dashed lines.
//
// Port notes: the wash is drawn here as horizontal slices of the stand-in's ellipses so the climbing
// edge shows; in game it is the pawn's own body quads (PawnBody.Over) tinted gold under a vertical alpha
// mask texture, one draw per part. The stain is a baked strip of discs (VfxDraw.BeginBake). Pawns are
// lib/pawn.js real-size stand-ins (average body).
import { Color, Mathf, Meshes, MaterialPool, MeshPool, ShaderDatabase } from '../js/engine.js';
import { P, Y, Floor, Lift, sprite, band, circle, soft, glow, rand } from './lib/six-paths-impact.js';
import { draw } from './lib/six-paths-solid.js';
import { pawn, at, shape, pawnLayer, shadowLayer } from './lib/pawn.js';
import { tube, puff, Enemy, Holder, Ally, Dust } from './lib/chain-sickle.js';
import { line, streak, whiteGlow } from './lib/goku.js';

const clamp = Mathf.Clamp01, lerp = Mathf.Lerp, D2R = Mathf.Deg2Rad, TAU = Math.PI * 2;
const smooth = x => Mathf.Smooth(clamp(x));
const easeOut = x => 1 - Math.pow(1 - clamp(x), 3);
const bump = x => (x >= 0 && x <= 1) ? Math.sin(x * Math.PI) : 0;
const disc = Meshes.disc(28, 'gold rush disc');
const flatMat = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const dirOf = deg => ({ x: Math.cos(deg * D2R), z: Math.sin(deg * D2R) });
const side = d => ({ x: -d.z, z: d.x });                 // left of d
const add = (a, b, k = 1) => ({ x: a.x + b.x * k, z: a.z + b.z * k });
const mix = (a, b, t) => ({ x: lerp(a.x, b.x, t), z: lerp(a.z, b.z, t) });
const len2 = v => Math.hypot(v.x, v.z);
const unit2 = v => { const l = len2(v) || 1; return { x: v.x / l, z: v.z / l }; };
const degOf = v => Math.atan2(v.z, v.x) / D2R;

// Palette. Dull gold for mass, white-gold for light, black for the mouth, the cracks and the eyes.
const Gold = new Color(.80, .60, .16), GoldDark = new Color(.52, .36, .08), GoldLit = new Color(.98, .84, .42);
const GoldEdge = new Color(1, .93, .62), GoldGlow = new Color(1, .78, .30), Ink = new Color(.07, .05, .02);
const Mouth = new Color(.05, .02, .01), Teeth = new Color(1, .95, .72), Flake = new Color(.95, .78, .30);
const Fleck = new Color(.22, .17, .10), Crown = new Color(.95, .78, .28), White = new Color(1, 1, 1);
const AllyRing = new Color(.45, .85, .45), Stain = new Color(.85, .66, .20);

// The rule's fixed numbers here (placeholders for the doc).
const Adjacent = 1.6, BuffShow = 1.5;
// Decided looks and timings.
const Lead = .3, FallTime = .35, Exit = 1.0, CrackTime = .17, CrownDrop = .067, SwallowAt = .5, SwallowOpen = .05, SwallowShut = .07;
const PunchOut = .05, PunchHold = .03, PunchBack = .15, BiteOpen = .08, BiteShut = .07, PaybackTime = .35, CostTime = 1.0;
const WaistGild = .5;
// Body: lib/pawn.js average. Ground = a standing pawn's ground contact on screen (feet + .12, as its
// shadow); a point h cells up draws at z + Ground + h x Lift.
const Ground = -.42, HandScreen = -.05, HandH = (HandScreen - Ground) / Lift, ChestH = (.05 - Ground) / Lift;
const HandOut = .14, HandAcross = .22;
const scr = q => ({ x: q.x, z: q.z + Ground + q.h * Lift });
const shd = (q, sun) => ({ x: q.x + sun.x * q.h, z: q.z + Ground + sun.z * q.h });
const signOf = deg => Math.cos(deg * D2R) < -1e-6 ? -1 : 1;

// ---- The fist -------------------------------------------------------------------------------------
// hand3: the hand (ground point + h). aim: degrees. look: { g (size), open (0 closed, 1 the mouth),
// glow (the buff aura), layer }. The fist lies level at hand height along the aim: a = along, v = across.
// Returns the knuckle point (where a hit lands) and the fist's centre on screen.
function fist(key, hand3, aim, look, s, sun, strength) {
  const H = scr(hand3), d = dirOf(aim), n = side(d), rot = -aim, g = look.g, L = look.layer, open = look.open;
  const pt = (a, v) => ({ x: H.x + (d.x * a + n.x * v) * g, z: H.z + (d.z * a + n.z * v) * g });
  // Shadow on the ground under it.
  const sh = shd(hand3, sun);
  sprite({ x: sh.x + d.x * .1 * g, z: sh.z + d.z * .1 * g }, .62 * g, .4 * g, Ink.withAlpha(strength * .5), soft, shadowLayer, rot);
  // Cuff: a dark gold block on the wrist with a lit line.
  band(`${key} cuff line`, [pt(-.24, -.17), pt(-.03, -.17)], [pt(-.24, .17), pt(-.03, .17)], Ink, L);
  band(`${key} cuff`, [pt(-.22, -.15), pt(-.05, -.15)], [pt(-.22, .15), pt(-.05, .15)], GoldDark, L + .001);
  band(`${key} cuff lit`, [pt(-.19, -.12), pt(-.08, -.12)], [pt(-.19, -.08), pt(-.08, -.08)], GoldLit.withAlpha(.8), L + .002);
  // The fist body: a round mass, far bigger than the hand.
  const c = pt(.12, 0);
  draw(disc, c.x, L + .003, c.z, .21 * g + .016, .2 * g + .016, rot, Ink);
  draw(disc, c.x, L + .004, c.z, .21 * g, .2 * g, rot, Gold);
  const hl = pt(.07, -.07);
  draw(disc, hl.x, L + .0045, hl.z, .1 * g, .07 * g, rot, GoldLit.withAlpha(.5));
  // Facets: three lit lines across the body.
  for (let i = 0; i < 3; i++) {
    const a = -.02 + i * .1, w = .17 - i * .03;
    line(`${key} facet ${i}`, [pt(a, -w), pt(a + .03, 0), pt(a, w)], .012 * g, GoldLit.withAlpha(.45), flatMat, L + .005, 'both');
  }
  // The mouth's inside, between the jaws, when open.
  if (open > 0) {
    const A = [], B = [], a0 = .06, a1 = .34 + .3 * open;
    for (let i = 0; i <= 10; i++) { const u = i / 10, a = lerp(a0, a1, u), w = (.3 * open) * Math.pow(Math.sin(Math.PI * u), .7) + .005; A.push(pt(a, -w)); B.push(pt(a, w)); }
    band(`${key} mouth`, A, B, Mouth, L + .0055);
  }
  // The jaws: the front half of the fist as two halves that swing apart across the aim.
  for (const sg of [1, -1]) {
    const A = [], B = [], a0 = .06, a1 = .34 + .3 * open, N = 10;
    for (let i = 0; i <= N; i++) {
      const u = i / N, a = lerp(a0, a1, u), lens = Math.pow(Math.sin(Math.PI * Math.min(1, .08 + .92 * u)), .7);
      const vin = sg * (.3 * open * lens + .002), vout = sg * (.3 * open * lens + .005 + .19 * lens * (1 - .25 * open) + .0);
      A.push(pt(a, vin)); B.push(pt(a, vout));
    }
    band(`${key} jaw ${sg} line`, A.map((q, i) => add(q, n, -sg * .012 * g)), B.map(q => add(q, n, sg * .014 * g)), Ink, L + .006);
    band(`${key} jaw ${sg}`, A, B, Gold, L + .0065);
    band(`${key} jaw ${sg} lit`, A.map((q, i) => mix(q, B[i], .45)), A.map((q, i) => mix(q, B[i], .7)), GoldLit.withAlpha(.55), L + .007);
    // Teeth on the jaw's inner edge, pointing into the mouth.
    if (open > .05) for (let i = 0; i < 6; i++) {
      const u = (i + .5) / 6, a = lerp(a0 + .02, a1 - .03, u), lens = Math.pow(Math.sin(Math.PI * Math.min(1, .08 + .92 * u)), .7);
      const vin = sg * (.3 * open * lens + .002), tip = pt(a, vin - sg * .085 * open * lens);
      band(`${key} tooth ${sg} ${i}`, [pt(a - .018, vin), pt(a + .018, vin)], [tip, tip], Teeth, L + .0075);
    }
  }
  // Knuckles along the front, fading as the jaws open.
  if (open < .95) for (let i = 0; i < 4; i++) {
    const k = pt(.31, -.135 + i * .09), r = .058 * g * (1 - open);
    draw(disc, k.x, L + .008, k.z, r + .012, r + .012, rot, Ink.withAlpha(1 - open));
    draw(disc, k.x, L + .0085, k.z, r, r, rot, GoldLit.withAlpha(1 - open));
    draw(disc, k.x - .012 * g, L + .009, k.z + .012 * g, r * .5, r * .4, rot, GoldEdge.withAlpha(.7 * (1 - open)));
  }
  // The light sweep across the facets every few seconds, like light crossing a coin you turn.
  const every = 2.5, u = (s % every) / .3;
  if (u >= 0 && u < 1) {
    const a = lerp(-.2, .36, u);
    line(`${key} sweep`, [pt(a - .03, -.2), pt(a, 0), pt(a - .03, .2)], .025 * g, GoldEdge.withAlpha(.8 * bump(u)), whiteGlow, L + .0095, 'both');
  }
  // The buff aura.
  if (look.glow > 0) sprite(c, .95 * g, .8 * g, GoldGlow.withAlpha(.4 * look.glow), glow, L + .0098);
  return { knuckle: pt(.36 + .3 * open, 0), centre: c, pt };
}

// Three flakes drifting off the held fist, one every 1.6 s each, so the idle has only this and the sweep.
function idleFlakes(key, centre, s) {
  for (let i = 0; i < 3; i++) {
    const period = 1.6, u = ((s + i * .53) % period) / period, x0 = (rand(i + 11) - .5) * .3, drift = (rand(i + 21) - .5) * .12;
    const q = { x: centre.x + x0 + drift * u, z: centre.z - .1 - .35 * u * u }, r = .018 * (1 - u * .5);
    draw(disc, q.x, Y + .11, q.z, r, r * .8, u * 500, Flake.withAlpha(.85 * (1 - u)));
  }
}

// ---- The hit --------------------------------------------------------------------------------------
// foot: the target's ground point (cell centre + Ground). d: the aim. big: 1 for a punch, 2 for the bite.
function strike(key, foot, d, age, seed, big, sun, strength) {
  if (age < 0) return;
  const hit = { x: foot.x, z: foot.z + ChestH * Lift * .8 };
  if (age < .08) sprite(hit, .55 * big, .45 * big, GoldEdge.withAlpha(.9 * (1 - age / .08)), glow, Y + .12);
  if (age < .35) { const u = age / .35; circle(foot, (.15 + .6 * u) * (1 + .4 * (big - 1)), (1 - u) * .7, Floor + .03, Gold); }
  // Cracks: five from the foot, mostly ahead; a gold core for .3 s, then the dark line that stays.
  for (let i = 0; i < 5; i++) {
    const ang = degOf(d) + (i - 2) * 38 + (rand(seed + i) - .5) * 20, dd = dirOf(ang), l = (.22 + .28 * rand(seed + 10 + i)) * Math.sqrt(big) * Math.min(1, age / .05);
    const pts = [foot, add(add(foot, dd, l * .5), side(dd), (rand(seed + 20 + i) - .5) * .08), add(foot, dd, l)];
    line(`${key} crack ${i}`, pts, .04, Ink.withAlpha(.55), flatMat, Floor + .032, 'end');
    if (age < .3) line(`${key} crack glow ${i}`, pts, .07, GoldGlow.withAlpha(.9 * (1 - age / .3)), whiteGlow, Floor + .034, 'end');
  }
  // Flakes thrown along the hit that land and stay.
  for (let i = 0; i < 8; i++) {
    const r = rand(seed * 13 + i), v = add(add({ x: 0, z: 0 }, d, .4 + .6 * r), side(d), (rand(seed * 17 + i) - .5) * 1.1), up = .8 + 1.2 * rand(seed * 19 + i), gr = 7;
    const h0 = ChestH * .8, land = (up + Math.sqrt(up * up + 2 * gr * h0)) / gr, tt = Math.min(age, land);
    const g = add(foot, v, tt * big * .8), h = Math.max(0, h0 + up * tt - .5 * gr * tt * tt), sz = .022 + .02 * r;
    if (age < land) draw(disc, g.x, Y + .08, g.z + h * Lift, sz, sz * .8, tt * 700, Flake);
    else draw(disc, g.x, Floor + .036 + i * .0002, g.z, sz * 1.3, sz, i * 40, Flake.withAlpha(.9));
  }
  // The bite's coins: six discs spinning out and gone by .5 s.
  if (big > 1 && age < .5) for (let i = 0; i < 6; i++) {
    const u = age / .5, ang = i / 6 * TAU + rand(seed + 40 + i), dd = dirOf(ang / D2R), r = .25 + .55 * easeOut(u), h = ChestH * .8 + .9 * u - 1.4 * u * u;
    const q = { x: foot.x + dd.x * r, z: foot.z + dd.z * r * .5 + Math.max(0, h) * Lift }, spin = Math.abs(Math.cos(age * 28 + i));
    draw(disc, q.x, Y + .1, q.z, .055 * spin + .008, .055, 0, Ink.withAlpha(1 - u));
    draw(disc, q.x, Y + .1005, q.z, .045 * spin + .004, .045, 0, Flake.withAlpha(1 - u));
  }
}

// The pay-back after a bite (age from the hit): a glow sliding from the fist into the chest, the chest
// glowing; then the cost, dark flecks off the head for CostTime.
function payback(key, from, chest, head, age) {
  if (age < 0) return;
  if (age < PaybackTime) { const u = smooth(age / PaybackTime), q = mix(from, chest, u); sprite(q, .3, .25, GoldGlow.withAlpha(.9 * (1 - .4 * u)), glow, Y + .13); streak(`${key} slide`, mix(from, chest, Math.max(0, u - .25)), q, .06, GoldEdge.withAlpha(.7), whiteGlow, Y + .13); }
  const cg = bump((age - .25) / .5);
  if (cg > 0) sprite(chest, .75, .8, GoldGlow.withAlpha(.45 * cg), glow, Y + .12);
  const ca = age - PaybackTime;
  if (ca >= 0 && ca < CostTime) for (let i = 0; i < 10; i++) {
    const u0 = ca / CostTime - rand(i + 60) * .3; if (u0 < 0 || u0 > 1) continue;
    const q = { x: head.x + (rand(i + 70) - .5) * .3 + (rand(i + 80) - .5) * .1 * u0, z: head.z + .1 - .5 * u0 * u0 };
    draw(disc, q.x, Y + .11, q.z, .02, .016, u0 * 300, Fleck.withAlpha(.9 * (1 - u0)));
  }
}

// ---- The gilding ----------------------------------------------------------------------------------
// gild: how far up the body the gold has climbed, 0 (none) to 1 (over the head). look: { alpha, crack
// (0..1 the crack lines), fall (0..1 the flakes falling off), crown (0..1 the drop onto the head), eyes,
// mouth (0..1 the chest mouth open), seam (the closed mouth shows) }.
function gilding(key, pos, who, gild, look, s, sun, strength) {
  if (gild <= 0 && look.fall <= 0) return;
  const L = pawnLayer + .02, feet = -.54, top = .63, level = feet + gild * (top - feet), alpha = look.alpha;
  // A heavier shadow: a gold statue weighs more than a person.
  const b = { x: pos.x + sun.x * .5, z: pos.z + feet + .12 + sun.z * .5 };
  sprite(b, 1.1, .5, Ink.withAlpha(strength * .35 * gild * alpha), soft, shadowLayer + .001);
  // The wash: the stand-in's ellipses in slices up to the level, with a bright edge at the level.
  const parts = shape(pos, who);
  parts.forEach((e, k) => {
    const z0 = e.z - e.rz, z1 = Math.min(e.z + e.rz, pos.z + level), step = .035;
    for (let v = z0 + step / 2; v < z1; v += step) {
      const w = e.rx * Math.sqrt(Math.max(0, 1 - Math.pow((v - e.z) / e.rz, 2)));
      if (w < .01) continue;
      const shadeT = clamp((v - e.z + e.rz) / (2 * e.rz)), col = Color.Lerp(GoldDark, Gold, .4 + .6 * shadeT);
      draw(MeshPool.plane10, e.x, L + k * .0005, v, w * 2, step + .004, 0, col.withAlpha(alpha), flatMat);
    }
    if (pos.z + level > z0 && pos.z + level < e.z + e.rz) {
      const w = e.rx * Math.sqrt(Math.max(0, 1 - Math.pow((pos.z + level - e.z) / e.rz, 2)));
      draw(MeshPool.plane10, e.x, L + .003, pos.z + level, w * 2 + .04, .03, 0, GoldEdge.withAlpha(.9 * alpha), whiteGlow);
    }
    // A lit streak down the left of each part, the statue's polish.
    if (gild > .2) draw(disc, e.x - e.rx * .45, L + .0025, e.z + e.rz * .1, e.rx * .18, Math.min(e.rz * .55, Math.max(0, (pos.z + level - (e.z - e.rz * .4)) / 2)), 0, GoldLit.withAlpha(.45 * alpha));
  });
  // Cracks before the gold falls off.
  if (look.crack > 0) for (let i = 0; i < 7; i++) {
    const e = parts[i % parts.length], a0 = rand(i + 90) * TAU, q0 = { x: e.x + Math.cos(a0) * e.rx * .8, z: e.z + Math.sin(a0) * e.rz * .8 };
    const q1 = { x: e.x + (rand(i + 100) - .5) * e.rx, z: e.z + (rand(i + 110) - .5) * e.rz }, q2 = { x: e.x + (rand(i + 120) - .5) * e.rx * 1.2, z: e.z + (rand(i + 130) - .5) * e.rz * 1.2 };
    line(`${key} crack ${i}`, [q0, mix(q0, q1, look.crack), mix(q0, q2, look.crack)], .016, Ink.withAlpha(.85 * alpha), flatMat, L + .004, 'end');
  }
  // The flakes falling off, from points on the body, landing on the ground.
  if (look.fall > 0) for (let i = 0; i < 12; i++) {
    const e = parts[i % parts.length], a0 = rand(i + 140) * TAU, u = clamp(look.fall * 1.2 - rand(i + 150) * .2);
    const h0 = (e.z + Math.sin(a0) * e.rz * .7 - (pos.z + Ground)) / Lift, x0 = e.x + Math.cos(a0) * e.rx * .8, fall = 1.1 * u * u * 2.2;
    const h = Math.max(0, h0 - fall), q = { x: x0 + (rand(i + 160) - .5) * .2 * u, z: pos.z + Ground + h * Lift }, sz = .03 + .025 * rand(i + 170);
    draw(disc, q.x, h > 0 ? Y + .09 : Floor + .04 + i * .0002, q.z, sz, sz * .8, u * 400, Flake.withAlpha(h > 0 ? 1 : .85));
  }
  // The crown: a band and five points, dropping onto the head top from half a cell up.
  if (look.crown > 0) {
    const drop = (1 - look.crown) * .5, base = { x: pos.x, z: pos.z + top + .06 + drop }, CL = pawnLayer + .04, sc = .5 + .5 * look.crown;
    band(`${key} crown line`, [{ x: base.x - .17, z: base.z - .035 }, { x: base.x + .17, z: base.z - .035 }], [{ x: base.x - .17, z: base.z + .035 }, { x: base.x + .17, z: base.z + .035 }], Ink, CL);
    band(`${key} crown`, [{ x: base.x - .15, z: base.z - .02 }, { x: base.x + .15, z: base.z - .02 }], [{ x: base.x - .15, z: base.z + .02 }, { x: base.x + .15, z: base.z + .02 }], Crown, CL + .001);
    [-.12, -.06, 0, .06, .12].forEach((x, i) => {
      const hgt = [.08, .11, .15, .11, .08][i], bx = base.x + x;
      band(`${key} point ${i} line`, [{ x: bx - .038, z: base.z + .01 }, { x: bx + .038, z: base.z + .01 }], [{ x: bx, z: base.z + hgt + .02 }, { x: bx, z: base.z + hgt + .02 }], Ink, CL);
      band(`${key} point ${i}`, [{ x: bx - .025, z: base.z + .01 }, { x: bx + .025, z: base.z + .01 }], [{ x: bx, z: base.z + hgt }, { x: bx, z: base.z + hgt }], Crown, CL + .001);
      draw(disc, bx, CL + .002, base.z + hgt - .01, .014, .014, 0, GoldEdge);
    });
    draw(disc, base.x - .06, CL + .0015, base.z, .06, .012, 0, GoldLit.withAlpha(.7));
  }
  // The eyes: two black dots, the only black on the pawn between firings.
  if (look.eyes > 0) for (const sg of [-1, 1]) { const h = at(pos, 'head', who); draw(disc, h.x + sg * .07, pawnLayer + .045, h.z + .02, .034 * look.eyes, .03 * look.eyes, 0, Ink); }
  // The chest mouth: a seam between firings, a black lens with gold teeth when it opens.
  const chest = at(pos, 'chest', who), ML = pawnLayer + .05;
  if (look.seam > 0 && look.mouth < .05) {
    band(`${key} seam`, [{ x: chest.x - .2, z: chest.z - .012 }, { x: chest.x, z: chest.z - .02 }, { x: chest.x + .2, z: chest.z - .012 }], [{ x: chest.x - .2, z: chest.z + .012 }, { x: chest.x, z: chest.z + .02 }, { x: chest.x + .2, z: chest.z + .012 }], Mouth.withAlpha(look.seam * alpha), ML);
    for (let i = 0; i < 5; i++) draw(disc, chest.x - .14 + i * .07, ML + .001, chest.z - .022, .012, .016, 0, Teeth.withAlpha(look.seam * alpha));
  }
  if (look.mouth > 0) {
    const o = look.mouth, mw = .12 + .42 * o, mh = .03 + .2 * o;
    draw(disc, chest.x, ML, chest.z, mw + .014, mh + .014, 0, Ink);
    draw(disc, chest.x, ML + .001, chest.z, mw, mh, 0, Mouth);
    for (let i = 0; i < 8; i++) for (const sg of [1, -1]) {
      const u = (i + .5) / 8 * 2 - 1, e = Math.sqrt(Math.max(0, 1 - u * u)), bx = chest.x + u * mw, bz = chest.z + sg * mh * e, tz = bz - sg * mh * .7 * e;
      band(`${key} mouth tooth ${i} ${sg}`, [{ x: bx - .014, z: bz }, { x: bx + .014, z: bz }], [{ x: bx, z: tz }, { x: bx, z: tz }], Teeth, ML + .002);
    }
    sprite(chest, 1.2 * o, 1.0 * o, GoldGlow.withAlpha(.3 * o), glow, ML + .003);
  }
}

// The stain: a gold disc under each place the wielder has stood, grown to 1 cell over 2 s, kept.
function stain(key, o, stands, s, alpha) {
  stands.forEach((st, i) => {
    const stood = Math.max(0, Math.min(s, st.t1 ?? s) - st.t0), r = (.25 + .75 * smooth(stood / 2)) * .5, c = add(o, st.at);
    if (s < st.t0 || r <= 0) return;
    sprite(c, r * 2.4, r * 1.9, Stain.withAlpha(.32 * alpha), soft, Floor + .02);
    draw(disc, c.x, Floor + .021, c.z, r, r * .75, 0, Stain.withAlpha(.5 * alpha));
    draw(disc, c.x, Floor + .022, c.z, r * .8, r * .55, 0, GoldLit.withAlpha(.3 * alpha));
  });
}

// ---- Timelines ------------------------------------------------------------------------------------
const isPunches = p => p.mode === 'punches', isGreedy = p => p.mode === 'greedy punch';
const isCorroded = p => p.mode === 'corroded', isOverclock = p => p.mode.startsWith('overclock');

// Who stands where, who is hit when. People: { off (from the origin), colour, hp (hits to go down),
// hostile }. Actions: { t (start), kind 'punch' | 'bite', target, aim, at (the wielder's offset), hit }.
// Stands: where the wielder stood and when, for the stain. Walks: { t0, t1, from, to }.
function plan(p) {
  const d = dirOf(p.aim), acr = side(d), F = (al, ac) => ({ x: d.x * al + acr.x * ac, z: d.z * al + acr.z * ac });
  const out = { people: [], actions: [], stands: [], walks: [], exit: null, end: 0 };
  const person = (off, colour, hp, more = {}) => out.people.push({ off, colour, hp, hits: [], downAt: null, ...more });
  const hitOn = (q, t, dir) => { q.hits.push({ t, d: dir }); if (q.hits.length >= q.hp && q.downAt == null) q.downAt = t; };
  if (isPunches(p) || isGreedy(p)) {
    person(F(1, 0), Enemy, isPunches(p) ? 99 : 3, { hostile: true });
    for (let i = 0; i < 3; i++) {
      const bite = isGreedy(p) && i === 2, t = Lead + i * p.spacing, hit = t + (bite ? PunchOut + BiteOpen + BiteShut : PunchOut);
      out.actions.push({ t, kind: bite ? 'bite' : 'punch', target: 0, aim: p.aim, at: { x: 0, z: 0 }, hit });
      hitOn(out.people[0], hit, d);
    }
    const last = out.actions[2];
    out.end = last.hit + (isGreedy(p) ? PaybackTime + BuffShow : PunchBack) + p.hold;
    out.stands.push({ at: { x: 0, z: 0 }, t0: 0 });
    return out;
  }
  const corroded = isCorroded(p);
  if (corroded) { person(F(0, 1), Ally, 2); person(F(3, .3), Enemy, 2, { hostile: true }); }
  else { person(F(0, 1), Ally, 2, { skipped: true }); person(F(1, -.25), Enemy, 3, { hostile: true }); person(F(.3, -1.05), Enemy, 2, { hostile: true }); }
  const every = corroded ? p.corrodedInterval : p.overclockInterval, n = corroded ? 4 : 5;
  let w = { x: 0, z: 0 }, t = corroded ? p.climb : .5;
  out.stands.push({ at: w, t0: 0 });
  for (let k = 0; k < n; k++) {
    const standing = out.people.map((q, i) => ({ q, i })).filter(({ q }) => (q.downAt == null || q.downAt > t) && (corroded || q.hostile));
    if (!standing.length) break;
    standing.sort((a, b) => len2({ x: a.q.off.x - w.x, z: a.q.off.z - w.z }) - len2({ x: b.q.off.x - w.x, z: b.q.off.z - w.z }));
    const { q, i } = standing[0], v = { x: q.off.x - w.x, z: q.off.z - w.z }, dist = len2(v), u = unit2(v);
    if (corroded && dist > Adjacent) {
      const to = add(w, u, dist - 1), walk = (dist - 1) / p.walk;
      out.stands[out.stands.length - 1].t1 = t;
      out.walks.push({ t0: t, t1: t + walk, from: w, to });
      t += walk; w = to;
      out.stands.push({ at: w, t0: t });
    }
    const hit = t + PunchOut + BiteOpen + BiteShut;
    out.actions.push({ t, kind: 'bite', target: i, aim: degOf(u), at: w, hit });
    hitOn(q, hit, u);
    t += every;
  }
  const last = out.actions[out.actions.length - 1];
  out.exit = last.hit + SwallowAt + SwallowShut + .4;
  out.stands[out.stands.length - 1].t1 = out.exit;
  out.end = out.exit + CrackTime + Exit + p.hold;
  return out;
}
// The wielder's ground offset from the origin at s.
function wielderAt(PL, s) {
  let w = { x: 0, z: 0 };
  for (const wk of PL.walks) if (s >= wk.t0) w = mix(wk.from, wk.to, clamp((s - wk.t0) / (wk.t1 - wk.t0)));
  return w;
}
const walking = (PL, s) => PL.walks.some(wk => s >= wk.t0 && s < wk.t1);

// ---- Drawing --------------------------------------------------------------------------------------
function drawFrame(s, p, o, sun, strength) {
  const PL = plan(p), who = { body: 'average', sun, shadow: strength };
  const armed = isCorroded(p) || isOverclock(p), corroded = isCorroded(p);
  // The gilding: how far it has climbed, and the end's cracks and fall.
  let gild = 0, look = { alpha: 1, crack: 0, fall: 0, crown: 0, eyes: 0, mouth: 0, seam: 0 };
  if (armed) {
    const cap = corroded ? 1 : WaistGild, over = s - PL.exit;
    gild = cap * smooth(s / (corroded ? p.climb : .6));
    if (over >= 0) {
      look.crack = clamp(over / CrackTime);
      look.fall = clamp((over - CrackTime) / Exit);
      look.alpha = 1 - look.fall;
    }
    if (corroded) {
      look.crown = over >= 0 ? (over < CrackTime ? 1 : 0) : clamp((s - p.climb) / CrownDrop);
      look.eyes = over >= 0 ? 1 - clamp(over / CrackTime) : smooth((s - p.climb) / .2);
      look.seam = over >= 0 ? 1 - clamp(over / CrackTime) : clamp((gild - .6) / .2);
    }
    const sw = PL.actions.filter(a => s >= a.hit + SwallowAt).pop();
    if (corroded && sw) { const a = s - sw.hit - SwallowAt; look.mouth = a < SwallowOpen ? easeOut(a / SwallowOpen) : 1 - smooth((a - SwallowOpen) / SwallowShut); }
    if (corroded) stain('gr stain', o, PL.stands, s, PL.exit != null && s > PL.exit + CrackTime ? 1 - smooth((s - PL.exit - CrackTime) / (Exit + p.hold)) : 1);
  }

  // The people: flinch at each hit, fall when downed.
  PL.people.forEach((q, i) => {
    const base = add(o, q.off), fall = q.downAt == null ? 0 : smooth((s - q.downAt) / FallTime);
    let off = { x: 0, z: 0 };
    for (const h of q.hits) { const a = s - h.t; if (a >= 0 && a < .25) off = add(off, h.d, .07 * bump(a / .25)); }
    const pos = add(base, off), turn = (q.hits.length ? (q.hits[q.hits.length - 1].d.x >= 0 ? 90 : -90) : -90) * fall;
    if (p.actors && q.skipped) circle(base, .42, .5, Floor + .04, AllyRing);
    if (p.actors) pawn(pos, { ...who, shirt: q.colour, downed: fall > 0, turn });
  });

  // The wielder.
  const wOff = wielderAt(PL, s), pos = add(o, wOff), done = PL.actions.filter(a => a.t <= s), act = done[done.length - 1];
  const aim = act ? act.aim : p.aim, sign = signOf(aim), d = dirOf(aim), hs = { x: side(d).x * -sign, z: side(d).z * -sign };
  if (armed) sprite(pos, 1.5, 1.1, GoldGlow.withAlpha(.14 * gild), glow, Floor + .01);
  if (walking(PL, s)) for (let i = 0; i < 4; i++) {
    const u = ((s * 6 + i * .25) % 1), c = { x: pos.x + (rand(i + 200) - .5) * .3 - wOff.x * 0, z: pos.z + Ground + .05 };
    sprite({ x: c.x, z: c.z + .1 * u }, .2 + .25 * u, .16 + .2 * u, Dust.withAlpha(.6 * Math.sin(u * Math.PI)), puff, Y + .008 + i * .0003);
  }
  if (p.actors) pawn(pos, { ...who, shirt: Holder });
  if (armed) gilding('gr gild', pos, who, gild, look, s, sun, strength);

  // Targeting lines (lab aid).
  if (p.actors && armed) for (const a of PL.actions) {
    const age = s - a.t; if (age < 0 || age > .45) continue;
    const from = at(add(o, a.at), 'chest', who), to = at(add(o, PL.people[a.target].off), 'chest', who), k = bump(age / .45);
    for (let j = 0; j < 8; j++) line(`gr aim ${a.t} ${j}`, [mix(from, to, j / 8 + .02), mix(from, to, j / 8 + .08)], .03, Gold.withAlpha(.7 * k), flatMat, Y + .1, 'none');
  }

  // The fist: the current action's thrust, else at rest by the hip on the hand side.
  const rest = add(add(pos, d, HandOut), hs, HandAcross);
  let hand3 = { x: rest.x, z: rest.z, h: HandH }, open = 0, buff = 0;
  const lastBite = PL.actions.filter(a => a.kind === 'bite' && a.hit <= s).pop();
  if (lastBite && !armed) buff = clamp((s - lastBite.hit - PaybackTime) / .2) * (1 - smooth((s - lastBite.hit - PaybackTime - BuffShow) / .4));
  if (act && s >= act.t) {
    const age = s - act.t, bite = act.kind === 'bite', total = bite ? PunchOut + BiteOpen + BiteShut + PunchBack : PunchOut + PunchHold + PunchBack;
    if (age < total) {
      const outT = PunchOut, holdT = bite ? BiteOpen + BiteShut : PunchHold;
      const ext = age < outT ? easeOut(age / outT) : age < outT + holdT ? 1 : 1 - smooth((age - outT - holdT) / PunchBack);
      const q = add(add(pos, d, HandOut + p.reach * ext), hs, HandAcross * (1 - .7 * ext));
      hand3 = { x: q.x, z: q.z, h: HandH + .1 * ext };
      if (bite) open = age < outT ? 0 : age < outT + BiteOpen ? easeOut((age - outT) / BiteOpen) : age < outT + BiteOpen + BiteShut ? 1 - Math.pow(clamp((age - outT - BiteOpen) / BiteShut), 2) : 0;
    }
  }
  const pointsNorth = d.z > .35;
  const fl = fist('gr fist', hand3, aim, { g: p.size, open, glow: buff, layer: pointsNorth ? pawnLayer - .03 : Y + .05 }, s, sun, strength);
  if (!act || s < act.t) idleFlakes('gr idle', fl.centre, s);

  // Hits, pay-backs.
  for (const a of PL.actions) {
    const q = PL.people[a.target], foot = { x: o.x + q.off.x, z: o.z + q.off.z + Ground }, age = s - a.hit;
    if (age < 0) continue;
    strike(`gr hit ${a.t}`, foot, dirOf(a.aim), age, Math.round(a.t * 100), a.kind === 'bite' ? 2 : 1, sun, strength);
    if (a.kind === 'bite' && !armed) {
      const wp = add(o, a.at), fistAt = scr({ ...add(add(wp, dirOf(a.aim), HandOut + p.reach), { x: side(dirOf(a.aim)).x * -signOf(a.aim), z: side(dirOf(a.aim)).z * -signOf(a.aim) }, HandAcross * .3), h: HandH + .1 });
      payback(`gr pay ${a.t}`, fistAt, at(wp, 'chest', who), at(wp, 'head', who), age);
    }
    if (a.kind === 'bite' && corroded && age >= SwallowAt - .3 && age < SwallowAt) {
      const u = (age - SwallowAt + .3) / .3, wp = add(o, a.at), from = scr({ ...add(wp, dirOf(a.aim), HandOut + p.reach * (1 - smooth(u))), h: HandH }), chest = at(wp, 'chest', who);
      sprite(mix(from, chest, smooth(u)), .28, .22, GoldGlow.withAlpha(.8), glow, Y + .13);
    }
    if (a.kind === 'bite' && corroded) { const sa = age - SwallowAt - SwallowOpen; if (sa >= 0 && sa < .12) sprite(at(add(o, a.at), 'chest', who), .9, .8, GoldEdge.withAlpha(.7 * (1 - sa / .12)), glow, Y + .14); }
  }
  // The crown's landing ring.
  if (corroded && PL.exit != null) { const ca = s - p.climb - CrownDrop; if (ca >= 0 && ca < .25) circle({ x: pos.x, z: pos.z + .63 + .06 }, .15 + .35 * (ca / .25), .8 * (1 - ca / .25), pawnLayer + .046, GoldEdge); }
}

export default {
  kit: 'E.G.O. weapons', label: 'Gold Rush (sketch)',
  params: {
    mode: { label: 'Show', value: 'punches', options: ['punches', 'greedy punch', 'corroded', 'overclock (hostiles only)'], group: 'Showcase' },
    aim: P('Aim (degrees)', 0, 0, 360, 5, 'Showcase'),
    actors: { label: 'Stand-in pawns, props and lab aids', value: true, group: 'Showcase' },
    spacing: P('Punches: time between (s)', .5, .25, 1.5, .05, 'Rule'),
    corrodedInterval: P('Corroded: a bite every (s)', 1.2, .6, 3, .1, 'Rule'),
    overclockInterval: P('Overclock: a bite every (s)', 1.0, .5, 2, .1, 'Rule'),
    walk: P('Corroded walk (cells/s)', 3, 1, 6, .25, 'Rule'),
    size: P('Fist (x size)', 1.2, .6, 1.8, .05, 'Shape'),
    reach: P('Thrust (cells)', .9, .4, 1.4, .05, 'Shape'),
    climb: P('Corroded: the gold climbs', 2.0, .5, 4, .1, 'Timing (s)'),
    hold: P('Show the result', 1.2, .3, 3, .1, 'Timing (s)'),
  },
  duration(p) { return plan(p).end; },
  phases(p) {
    const PL = plan(p), out = [];
    if (isPunches(p)) PL.actions.forEach((a, i) => out.push({ name: `Punch ${i + 1}`, t: a.t }));
    else if (isGreedy(p)) {
      const b = PL.actions[2];
      out.push({ name: 'Punch 1', t: PL.actions[0].t }, { name: 'Punch 2', t: PL.actions[1].t }, { name: 'Bite', t: b.t }, { name: 'Pay-back', t: b.hit }, { name: 'Buff and cost', t: b.hit + PaybackTime });
    } else {
      out.push({ name: isCorroded(p) ? 'Corroded' : 'Overclock', t: 0 });
      if (isCorroded(p)) out.push({ name: 'Crown', t: p.climb });
      PL.actions.forEach((a, i) => out.push({ name: `Bite ${i + 1}`, t: a.t }));
      PL.walks.forEach(w => out.push({ name: 'Walk', t: w.t0 }));
      out.push({ name: 'Ends', t: PL.exit });
      out.sort((a, b) => a.t - b.t);
    }
    out.push({ name: 'Result', t: PL.end - p.hold });
    return out;
  },
  events(p) {
    const PL = plan(p), out = [];
    if (isCorroded(p)) out.push({ t: 0, type: 'sound', def: 'RimArt_GoldRushCorrode' }, { t: PL.exit, type: 'sound', def: 'RimArt_GoldRushShatter' });
    for (const a of PL.actions) {
      if (a.kind === 'bite') out.push({ t: a.t + PunchOut, type: 'sound', def: 'RimArt_GoldRushBite' }, { t: a.hit, type: 'shake', value: .05 });
      else out.push({ t: a.t, type: 'sound', def: 'RimArt_GoldRushPunch' }, { t: a.hit, type: 'shake', value: .02 });
      if (a.kind === 'bite' && isCorroded(p)) out.push({ t: a.hit + SwallowAt, type: 'sound', def: 'RimArt_GoldRushSwallow' });
    }
    return out;
  },

  draw(s, p, { origin: o, scene }) {
    if (s < 0 || s >= plan(p).end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    drawFrame(s, p, o, sun, strength);
  },
};
