// Solemn Lament — E.G.O. weapon proposal, not the game. Nothing in Source/RimArt draws this yet.
// Funeral of the Dead Butterflies (Lobotomy Corporation, WAW): the white and black pair of handguns.
// The rules are docs/ego-weapons.md, "Weapon 2: Solemn Lament" (design agreed 2026-09-30, numbers
// are placeholders and become XML fields on CompProperties_EgoWeapon):
//   The pair: one weapon item that draws two guns and alternates its own shots, white first.
//         White shot: no body damage, 2 Butterfly stacks. Black shot: 9 damage and 1 stack.
//         Range 10, 0.25 s between shots.
//   Butterfly: a stacking hediff, -8 % consciousness per stack, cap 10. At the cap the pawn goes
//         down covered in butterflies. -1 stack per 10 s (not shown: the sketch is 3 s long).
//   Ammo: 20 shots, reload 3 s (not shown).
//   Corrosion (shared system): the coffin. A butterfly cloud on the corroded pawn; every pawn within
//         radius 3 takes 1 stack per second, any faction, the wielder excluded. 15 s, interval 1 s.
//         Overclock: the same coffin for 5 s, hostiles only (allies inside are skipped), mood -15.
//   Rule sliders (stacks per shot, cap, interval) are there for the balance pass: with the doc's
//   numbers the 7th shot reaches the cap, 1.5 s after the first.
//
// Look, from the source: the Limbus skill videos of Lobotomy E.G.O::Solemn Lament Yi Sang
// (limbuscompany.wiki.gg, Skill_1/2/3.mp4, looked at 2026-09-30). What the frames show and what the
// sketch does with it:
//   Guns: one white pistol and one black pistol, one in each hand, arms out, firing in turn.
//   White shot: a white crescent swept round the gun hand, a white flash with short spikes at the
//         muzzle, a thin pale smoky line to the target, a white sunburst of thin spikes on the hit.
//   Black shot: a black ink splash of jagged shards off the muzzle, a dark line, a black star of
//         shards on the hit, a red spark and blood.
//   Butterflies: lace butterflies drift off the hit and settle: line art, a white outline and a
//         web of white veins cutting each wing into small irregular cells, see-through between the
//         lines; big rounded forewings, smaller hindwings, scalloped edges. .3 cells across here,
//         about a quarter of the pawn's height.
//         Limbus splits Butterfly into The Living and The Departed; here the white shot's stacks
//         are pale butterflies and the black shot's are dark ones, so the pips and the body show
//         which gun put each one there.
//   The finish (Skill 3): the coffin set down beside the target, a white flash, white streaks
//         rising and butterflies pouring out. Used for the corrosion action: the coffin rises out
//         of the floor behind the wielder (ground, not sky) and opens.
//   Corroded look (doc: the Abnormality's face): a big butterfly over the head, left wings white,
//         right wings black, slowly beating.
//
// Order, "burst" (default sliders):
//   0.00  draw: the guns come up from the hips to the aim over .28 s, white in the right hand
//   0.35  shot 1, white: crescent and muzzle flash, the line reaches the raider 5 cells off in
//         .08 s, white sunburst; 2 pale butterflies loop off the chest and land on the body in .5 s;
//         the white gun kicks up 28 degrees and settles in .3 s
//   0.60  shot 2, black: ink splash, dark line, black star, blood thrown on, a floor spatter that
//         stays, the raider flinches .07 cells; 1 dark butterfly lands
//   ...   one shot every .25 s, alternating. The raider sways more with each stack (consciousness).
//         Ten pips over the head count the stacks, each in its butterfly's colour.
//   1.93  shot 7 hits: stack 10, the cap. 20 more butterflies spiral in from 1-2 cells away over
//         .45 s and cover the body; the raider falls backward (turned 90 degrees) over .35 s and
//         stays down, covered. The guns lower. The result is held 1.2 s.
// Order, "corroded: the coffin" (cloud 4 s shown of the rule's 15):
//   0.00  the coffin rises out of the floor behind the wielder over .5 s, dust at its base; the
//         butterfly face fades in over the head; the guns hang down
//   0.50  the lid swings open over .3 s, the inside lit white, white streaks rise off it
//   0.60  36 butterflies pour out, .03 s apart, and circle the wielder 0.7-2.8 cells out at 0.3-1.4
//         cells up; a pale floor ring at the true radius 3 and a dim floor inside it
//   1.60  and every 1 s: one butterfly leaves the cloud for each pawn inside the ring and lands on
//         it in .5 s (+1 stack); the coffin sends a new one out in its place. The pawn outside the
//         ring gets none.
//   4.60  the cloud flies back into the coffin over .7 s, the lid closes, the coffin sinks back
//         into the floor over .45 s. The landed butterflies stay (the stacks stay).
// "overclock": the same without the face and with the ally skipped (no pips, nothing lands).
// A cloud of 10 s or more reaches the cap and the pawns inside go down as in the burst.
//
// Drawing: the shots, trails, crescents and hits are level shapes at chest height (lib/pawn.js
// chest = .05 north of the cell centre), so they turn with the aim and need no per-facing method.
// Aiming north the guns draw under the pawn layer (held in front of the body, away from the
// viewer). The coffin stands facing the viewer at every aim (a fixed screen orientation, like
// Twin Maw's jaws): a front face .68 wide and 2.0 tall (1.2 on screen, a little over the pawn's
// 1.17, as in the source), a thin top face .26 deep,
// a white edge, a shadow along the sun from its base. It rises by drawing only the part above the
// floor. A butterfly is three flat meshes built once: the wing fill (four fans; ink for the black
// shot's, a faint pale film for the white shot's), a wide outline for the additive glow, and the
// lines (outline strips, 7 + 5 veins per side with two rows of cross veins, antennae), then a
// white-edged body dash. It is scaled across the body for the wing beat (3 beats/s flying, .7 at rest);
// flying ones fly in ground + height and get a small shadow. Resting butterflies sit on fixed
// points of the body (a sunflower spread over the torso ellipse, then the head) and turn with the
// pawn when it falls. Pawns are lib/pawn.js real-size stand-ins (average body).
import { Color, Mathf, Mesh, Meshes, MaterialPool, ShaderDatabase } from '../js/engine.js';
import { P, Y, Floor, Lift, sprite, band, circle, soft, glow, rand } from './lib/six-paths-impact.js';
import { draw } from './lib/six-paths-solid.js';
import { pawn, at, pawnLayer, shadowLayer } from './lib/pawn.js';
import { rect, puff, Blood, Enemy, Holder, Ally, Dust } from './lib/chain-sickle.js';
import { line, strip, streak, whiteGlow } from './lib/goku.js';

const clamp = Mathf.Clamp01, smooth = Mathf.Smooth, lerp = Mathf.Lerp, D2R = Mathf.Deg2Rad, TAU = Math.PI * 2;
const disc = Meshes.disc(16, 'solemn lament disc');
const flat = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const bump = x => (x >= 0 && x <= 1) ? Math.sin(x * Math.PI) : 0;
const dirOf = deg => ({ x: Math.cos(deg * D2R), z: Math.sin(deg * D2R) });
const move = (q, d, k) => ({ x: q.x + d.x * k, z: q.z + d.z * k });
const side = d => ({ x: -d.z, z: d.x });
const unit = (a, b) => { const dx = b.x - a.x, dz = b.z - a.z, l = Math.hypot(dx, dz) || 1; return { x: dx / l, z: dz / l }; };

// Palette: monochrome, as the source. White and pale for The Living, ink and soot for The Departed.
const White = new Color(1, 1, 1), Pale = new Color(.90, .90, .93), Ash = new Color(.50, .50, .55);
const Ink = new Color(.05, .05, .06), Soot = new Color(.16, .16, .18), Smoke = new Color(.78, .78, .82);
const Red = new Color(.9, .15, .12), CoffinEdge = new Color(.86, .86, .90), CoffinTop = new Color(.24, .24, .27);
// The rule's fixed numbers here (placeholders from the doc).
const Radius = 3, CoffinTick = 1;
// Decided looks and timings.
const Speed = 60;                     // cells/s, the pistol round: 5 cells in .08 s
const TrailLife = .25, FlyTime = .5, SwarmIn = .45, FallTime = .35, DiveTime = .5;
const KickTilt = 28 * D2R, KickUp = .04, KickDamp = 9, KickSwing = 8, KickSlide = .05;
const GunLen = .30, GunW = .075, HandAcross = .13, HandReach = .22;
const ChestLift = .05;                // lib/pawn.js: the chest is .05 north of the cell centre on screen
const Ground = -.42;                   // a standing pawn's ground contact on screen (feet + .12, as its shadow)
const ChestH = (ChestLift - Ground) / Lift;
const lift = q => ({ x: q.x, z: q.z + ChestLift });
const CoffinH = 2.0, CoffinDepth = .26, CoffinBack = { x: -.18, z: .32 };
const Rise = .5, LidOpen = .3, Open = .6, ReturnTime = .7, LidClose = .2, Sink = .45;
const CloudN = 36, TorsoSlots = 30, HeadSlots = 8;

// ---- Butterflies ------------------------------------------------------------------------------
// Lace butterflies, as the source draws them: a white outline and a web of white veins that splits
// each wing into small irregular cells, see-through between the lines. The forewing is the big
// rounded one, the hindwing smaller; both outer edges are scalloped. Unit size: span about 2
// across, body along +z (north = head). Each wing outline is star-shaped from its root (angles
// checked), so its fill is a fan from the root.
const ForeRaw = [[.05, .06], [.15, .30], [.32, .55], [.52, .74], [.72, .86], [.88, .88], [.97, .78], [.98, .62], [.93, .50], [.90, .38], [.82, .28], [.72, .20], [.52, .12], [.30, .05], [.12, .02]];
const HindRaw = [[.05, -.02], [.25, -.02], [.48, -.08], [.66, -.20], [.74, -.36], [.70, -.52], [.58, -.66], [.42, -.74], [.28, -.70], [.17, -.56], [.10, -.36], [.06, -.16]];
// The scallops: between outline points a..b a midpoint pulled 7 % toward the root.
function scallop(w, a, b) {
  const out = [], r = w[0];
  w.forEach((q, i) => {
    out.push(q);
    if (i >= a && i < b) { const n = w[i + 1], mx = (q[0] + n[0]) / 2, mz = (q[1] + n[1]) / 2; out.push([r[0] + (mx - r[0]) * .93, r[1] + (mz - r[1]) * .93]); }
  });
  return out;
}
const Fore = scallop(ForeRaw, 5, 11), Hind = scallop(HindRaw, 2, 9);
// Veins: straight from the root to these outline points, and two rows of cross veins between each
// neighbouring pair at about 42 % and 72 % of the way (jittered), which makes the cells.
const ForeVeins = [2, 3, 5, 7, 9, 11, 13], HindVeins = [2, 4, 6, 8, 10];
const Outline = .065, Vein = .04, HaloW = .17;   // line widths in unit size (x .15 cells at the default span)

function lineMesh(name, sides, { outline, veins, antennae }) {
  const v = [], tri = [];
  const quad = (a, b, w) => {
    const dx = b[0] - a[0], dz = b[1] - a[1], l = Math.hypot(dx, dz) || 1, nx = -dz / l * w / 2, nz = dx / l * w / 2, n = v.length / 2;
    v.push(a[0] + nx, a[1] + nz, a[0] - nx, a[1] - nz, b[0] - nx, b[1] - nz, b[0] + nx, b[1] + nz);
    tri.push(n, n + 1, n + 2, n, n + 2, n + 3);
  };
  const ring = (pts, w) => {            // a closed outline with mitred corners (mitre capped at 2x)
    const N = pts.length, base = v.length / 2;
    for (let i = 0; i < N; i++) {
      const p = pts[(i - 1 + N) % N], q = pts[i], r = pts[(i + 1) % N];
      const n1 = norm(-(q[1] - p[1]), q[0] - p[0]), n2 = norm(-(r[1] - q[1]), r[0] - q[0]);
      const m = norm(n1[0] + n2[0], n1[1] + n2[1]), k = w / 2 / Math.max(.5, m[0] * n1[0] + m[1] * n1[1]);
      v.push(q[0] + m[0] * k, q[1] + m[1] * k, q[0] - m[0] * k, q[1] - m[1] * k);
    }
    for (let i = 0; i < N; i++) { const a = base + i * 2, b = base + ((i + 1) % N) * 2; tri.push(a, a + 1, b + 1, a, b + 1, b); }
  };
  for (const sgn of sides) {
    const mirror = w => w.map(([x, z]) => [sgn * x, z]);
    ring(mirror(Fore), outline); ring(mirror(Hind), outline);
    if (veins) [[ForeRaw, ForeVeins, 1], [HindRaw, HindVeins, 2]].forEach(([w, ids, seed]) => {
      const root = [sgn * w[0][0], w[0][1]], tip = j => [sgn * w[ids[j]][0], w[ids[j]][1]];
      const along = (j, f) => { const t = tip(j); return [root[0] + (t[0] - root[0]) * f, root[1] + (t[1] - root[1]) * f]; };
      ids.forEach((_, j) => quad(along(j, .15), along(j, .98), veins));
      for (let j = 0; j + 1 < ids.length; j++) for (const [f, row] of [[.42, 0], [.72, 1]]) {
        const ja = (rand(seed * 97 + j * 7 + row) - .5) * .14, jb = (rand(seed * 89 + j * 5 + row + 40) - .5) * .14;
        quad(along(j, f + ja), along(j + 1, f + jb), veins);
      }
    });
    if (antennae) { quad([sgn * .03, .22], [sgn * .16, .5], antennae); quad([sgn * .15, .48], [sgn * .2, .56], antennae * 2.2); }
  }
  const m = new Mesh(name); m.setFlat(v, tri); return m;
}
function norm(x, z) { const l = Math.hypot(x, z) || 1; return [x / l, z / l]; }
function fillMesh(name, sides) {
  const v = [], tri = [];
  for (const sgn of sides) for (const wing of [Fore, Hind]) {
    const base = v.length / 2;
    wing.forEach(([x, z]) => v.push(sgn * x, z));
    for (let i = 1; i < wing.length - 1; i++) tri.push(base, base + i, base + i + 1);
  }
  const m = new Mesh(name); m.setFlat(v, tri); return m;
}
const both = [1, -1];
const Lines = lineMesh('sl lace', both, { outline: Outline, veins: Vein, antennae: Vein }), Halo = lineMesh('sl lace halo', both, { outline: HaloW });
const Fill = fillMesh('sl lace fill', both);
const LinesR = lineMesh('sl lace r', [1], { outline: Outline, veins: Vein, antennae: Vein }), LinesL = lineMesh('sl lace l', [-1], { outline: Outline, veins: Vein, antennae: Vein });
const HaloR = lineMesh('sl lace halo r', [1], { outline: HaloW }), HaloL = lineMesh('sl lace halo l', [-1], { outline: HaloW });
const FillR = fillMesh('sl lace fill r', [1]), FillL = fillMesh('sl lace fill l', [-1]);

// heading: degrees the head points (0 east, 90 north). flap: 0..1 of the full span. dark: The
// Departed (black shot), an ink fill; otherwise The Living (white shot), a faint pale film. The
// lines are white on both, with a soft glow round the outline, and the body is a white-edged dash.
function butterfly(q, size, heading, flap, dark, alpha, layer) {
  if (alpha <= .01) return;
  const rot = 90 - heading, sx = size * .5 * flap, sz = size * .5;
  draw(Fill, q.x, layer, q.z, sx, sz, rot, dark ? Ink.withAlpha(.82 * alpha) : Pale.withAlpha(.22 * alpha));
  draw(Halo, q.x, layer + .0002, q.z, sx, sz, rot, White.withAlpha(.14 * alpha), whiteGlow);
  draw(Lines, q.x, layer + .0004, q.z, sx, sz, rot, White.withAlpha(.95 * alpha));
  draw(disc, q.x, layer + .0006, q.z, size * .04, size * .17, rot, White.withAlpha(alpha));
  draw(disc, q.x, layer + .0007, q.z, size * .018, size * .13, rot, Soot.withAlpha(alpha));
}
const flapAt = (s, rate, lo, i) => lo + (1 - lo) * Math.abs(Math.cos(Math.PI * rate * s + i * 1.7));
function butterflyShadow(g, h, size, sun, strength, alpha = 1) {
  sprite({ x: g.x + sun.x * h, z: g.z + sun.z * h }, size * .7, size * .45, Ink.withAlpha(strength * .45 * alpha), soft, shadowLayer);
}

// Where a stack's butterfly rests on the body: a sunflower spread over the torso ellipse from the
// chest outward (so stacks gather from the middle), then eight round the head. Offsets from the cell
// centre (average body), turned clockwise with the pawn when it falls, as lib/pawn.js turns it.
function slotLocal(i) {
  if (i < TorsoSlots) {
    const r = Math.sqrt((i + .5) / TorsoSlots) * .85, t = i * 2.39996 + .7;
    return { u: Math.cos(t) * r * .27, v: -.12 + Math.sin(t) * r * .42, h: 70 + rand(i + 3000) * 40 };
  }
  const j = i - TorsoSlots, t = j / HeadSlots * TAU + .3, r = .5 + .35 * rand(j + 3100);
  return { u: Math.cos(t) * r * .21, v: .41 + Math.sin(t) * r * .22, h: 60 + rand(j + 3200) * 60 };
}
function slotAt(pos, i, turn) {
  const L = slotLocal(i), a = turn * D2R, c = Math.cos(a), s = Math.sin(a);
  return { x: pos.x + L.u * c + L.v * s, z: pos.z - L.u * s + L.v * c, heading: L.h - turn };
}
// A flight from e.from ({ g ground point, h height }) to `to`, eased out, bowed out by e.via (a
// ground offset at mid-flight) and up by e.arc, with a small flutter in height.
function flyAt(e, u, to) {
  const k = 1 - (1 - u) * (1 - u), b = bump(u);
  const g = { x: lerp(e.from.g.x, to.g.x, k) + e.via.x * b, z: lerp(e.from.g.z, to.g.z, k) + e.via.z * b };
  const h = lerp(e.from.h, to.h, k) + e.arc * b + .04 * Math.sin(u * 16 + e.seed);
  return { g, h, screen: { x: g.x, z: g.z + h * Lift } };
}

// ---- A pawn that takes stacks --------------------------------------------------------------------
// M: { pos, colour, cap, seed, events, flinches, downAt, fallTurn }. An event is one butterfly:
// { t: when the stack counts, launch, fly, slot, dark, from, via, arc, seed, swarm }.
// Swarm events are the knockdown's cover, not stacks.
function markedPawn(key, M, s, who, size, sun, strength) {
  const fall = M.downAt == null ? 0 : smooth((s - M.downAt - SwarmIn * .5) / FallTime);
  const turn = M.fallTurn * fall;
  const counted = M.events.filter(e => !e.swarm && e.t <= s);
  const sway = (1 - fall) * .045 * counted.length / M.cap * Math.sin(s * TAU * .8 + M.seed);
  let fx = sway, fz = 0;
  for (const f of M.flinches) {
    const a = s - f.t;
    if (a >= 0 && a < .25) { const k = .07 * bump(a / .25); fx += f.d.x * k; fz += f.d.z * k; }
  }
  const pos = { x: M.pos.x + fx, z: M.pos.z + fz };
  sprite({ x: M.pos.x + sun.x * .5, z: M.pos.z + Ground + sun.z * .5 }, .94, .42, Ink.withAlpha(strength * (1 - fall)), soft, shadowLayer);
  if (fall > 0) sprite({ x: M.pos.x + sun.x * .2, z: M.pos.z + sun.z * .2 }, 1.3, .42, Ink.withAlpha(strength * fall), soft, shadowLayer, turn + 90);
  pawn(pos, { ...who, shirt: M.colour, downed: fall > 0, turn, shadow: 0 });

  M.events.forEach((e, i) => {
    const age = s - e.launch; if (age < 0) return;
    const sl = slotAt(pos, e.slot, turn), gz = lerp(M.pos.z + Ground, sl.z, fall);
    const to = { g: { x: sl.x, z: gz }, h: (sl.z - gz) / Lift }, sz = size * (e.swarm ? .9 : 1);
    const u = age / e.fly;
    if (u < 1) {
      const q = flyAt(e, u, to), q2 = flyAt(e, Math.min(1, u + .03), to);
      const heading = Math.atan2(q2.screen.z - q.screen.z, q2.screen.x - q.screen.x) / D2R;
      const fade = e.swarm ? clamp(u / .25) : 1;   // the swarm fades in where it appears
      butterflyShadow(q.g, Math.max(0, q.h), sz, sun, strength, fade);
      butterfly(q.screen, sz, u > .85 ? lerp(heading, sl.heading, (u - .85) / .15) : heading, flapAt(s, 3, .15, i), e.dark, fade, Y + .12 + i * .0008);
    } else {
      butterfly(sl, sz, sl.heading + 8 * Math.sin(s * 1.3 + i), flapAt(s, .7, .55, i), e.dark, 1, pawnLayer + .02 + i * .0008);
    }
  });
  if (M.events.length && fall < .6) pips(M, counted, at(pos, 'headTop', who), 1 - fall / .6);
}
// The stack count over the head: cap pips, each filled in the colour of the butterfly that made it.
function pips(M, counted, top, alpha) {
  const pitch = Math.min(.1, 1.1 / M.cap), x0 = top.x - pitch * (M.cap - 1) / 2, z = top.z + .2;
  for (let i = 0; i < M.cap; i++) {
    const e = counted[i], x = x0 + i * pitch, r = pitch * .38;
    draw(disc, x, Y + .2, z, r + .012, r + .012, 0, (e ? White : Ink).withAlpha((e ? .9 : .35) * alpha));
    if (e) draw(disc, x, Y + .201, z, r, r, 0, (e.dark ? Ink : Pale).withAlpha(alpha));
  }
}

// ---- The pair --------------------------------------------------------------------------------------
// A pistol seen from above: grip, slide, a lit top edge, the muzzle. Level at chest height along d
// from the hand's ground point; slide pushes it back, tilt (radians) swings the muzzle up (cos(tilt)
// along d, Lift x sin(tilt) north) or, negative, down. The shadow stays flat along d.
function pistol(key, hand, d, white, tilt, slide, layer, sun, strength) {
  const base = move(hand, d, -slide), q = lift(base);
  const dv = { x: d.x * Math.cos(tilt), z: d.z * Math.cos(tilt) + Lift * Math.sin(tilt) };
  const len = Math.hypot(dv.x, dv.z), u = { x: dv.x / len, z: dv.z / len }, deg = Math.atan2(u.z, u.x) / D2R, L = GunLen * len;
  const sh = { x: base.x + sun.x * ChestH + d.x * GunLen * .5, z: base.z + Ground + sun.z * ChestH + d.z * GunLen * .5 };
  sprite(sh, GunLen * Math.cos(tilt) + .08, GunW * 1.6, Ink.withAlpha(strength * .45), soft, shadowLayer, -Math.atan2(d.z, d.x) / D2R);
  rect(`${key} grip`, move(q, u, L * .12), L * .3, GunW * 1.25, deg, white ? Ash : Ink, layer);
  rect(`${key} body`, move(q, u, L * .55), L * .9, GunW, deg, white ? Pale : Ink, layer + .001);
  rect(`${key} edge`, { x: q.x + u.x * L * .55, z: q.z + u.z * L * .55 + .018 }, L * .85, .014, deg, (white ? White : Ash).withAlpha(.9), layer + .002);
  rect(`${key} tip`, move(q, u, L * .96), L * .08, GunW * .7, deg, white ? Ash : Soot, layer + .003);
  return { muzzle: move(q, u, L), dir: u };
}
// The barrel's kick after a shot: up in .04 s, then a damped swing back to level (about .3 s).
const kickAt = age => age < 0 ? 0 : age < KickUp ? Math.sin(age / KickUp * Math.PI / 2) : Math.max(0, Math.exp(-KickDamp * (age - KickUp)) * Math.cos(KickSwing * (age - KickUp)));

// A jagged ink shard: a thin triangle pointing along t.
function shard(key, c, t, len, wid, colour, layer) {
  const dx = Math.cos(t), dz = Math.sin(t), nx = -dz * wid / 2, nz = dx * wid / 2;
  const b = { x: c.x - dx * len * .35, z: c.z - dz * len * .35 }, tip = { x: c.x + dx * len * .65, z: c.z + dz * len * .65 };
  band(key, [{ x: b.x + nx, z: b.z + nz }, tip], [{ x: b.x - nx, z: b.z - nz }, tip], colour, layer);
}
// White shot at the muzzle: a flash with seven short spikes for .08 s, and the crescent: a white arc
// swept round the gun hand on its outer side in .05 s, thick in the middle, gone in .18 s.
function whiteMuzzle(key, m, dir, hand, out, age) {
  if (age < 0 || age > .18) return;
  const f = 1 - age / .18, aim = Math.atan2(dir.z, dir.x);
  if (age < .08) {
    const g = 1 - age / .08;
    sprite(m, .12 + .4 * g, .11 + .36 * g, White.withAlpha(.9 * g), glow, Y + .06);
    for (let i = 0; i < 7; i++) {
      const t = aim + (i - 3) * .17 + (rand(i + 50) - .5) * .1, l = (.25 + .35 * rand(i + 60)) * (.5 + g * .5);
      streak(`${key} spike ${i}`, m, { x: m.x + Math.cos(t) * l, z: m.z + Math.sin(t) * l }, .035, White.withAlpha(.95 * g), whiteGlow, Y + .061, 3);
    }
  }
  const sweep = clamp(age / .05), c = lift(hand), r = .42, a0 = aim + out * 2.6, a1 = aim - out * .25, N = 16, inner = [], outer = [];
  for (let i = 0; i <= N; i++) {
    const u = i / N, t = a0 + (a1 - a0) * u * sweep, w = .09 * Math.sin(u * Math.PI) * f;
    inner.push({ x: c.x + Math.cos(t) * (r - w), z: c.z + Math.sin(t) * (r - w) });
    outer.push({ x: c.x + Math.cos(t) * (r + w * .4), z: c.z + Math.sin(t) * (r + w * .4) });
  }
  strip(`${key} crescent`, inner, outer, White.withAlpha(.9 * f), whiteGlow, Y + .059);
}
// Black shot at the muzzle: a dark blot and ten ink shards sprayed forward in a 130-degree cone,
// gone in .22 s, with a pale core for .06 s so the shot still reads as a flash.
function blackMuzzle(key, m, dir, age) {
  if (age < 0 || age > .22) return;
  const f = 1 - age / .22, aim = Math.atan2(dir.z, dir.x);
  sprite(m, .3 + .25 * (1 - f), .28 + .22 * (1 - f), Ink.withAlpha(.75 * f), soft, Y + .062);
  for (let i = 0; i < 10; i++) {
    const t = aim + (i / 9 - .5) * 2.3 + (rand(i + 70) - .5) * .25, v = (.9 + 1.6 * rand(i + 80)) * (1 - .5 * Math.abs(i / 9 - .5)), l = (.1 + .26 * rand(i + 90)) * (.4 + .6 * f), d0 = .06 + v * age;
    shard(`${key} shard ${i}`, { x: m.x + Math.cos(t) * d0, z: m.z + Math.sin(t) * d0 }, t, l, l * .35, Ink.withAlpha(Math.min(1, f * 1.6)), Y + .063);
  }
  if (age < .06) sprite(m, .22, .2, Pale.withAlpha(.6 * (1 - age / .06)), glow, Y + .064);
}
// The round's line from the muzzle to the target: drawn out at Speed, then faded over TrailLife.
function shotTrail(key, from, to, age, white) {
  const flight = Math.hypot(to.x - from.x, to.z - from.z) / Speed, u = clamp(age / flight), fade = 1 - clamp((age - flight) / TrailLife);
  if (age < 0 || fade <= 0) return;
  const head = { x: lerp(from.x, to.x, u), z: lerp(from.z, to.z, u) }, w = .5 + .5 * fade;
  if (white) {
    line(`${key} halo`, [from, head], .14 * w, Smoke.withAlpha(.22 * fade), whiteGlow, Y + .05, 'none');
    line(`${key} core`, [from, head], .035 * w, White.withAlpha(.85 * fade), whiteGlow, Y + .051, 'none');
  } else {
    line(`${key} halo`, [from, head], .15 * w, Soot.withAlpha(.3 * fade), flat, Y + .05, 'none');
    line(`${key} core`, [from, head], .04 * w, Ink.withAlpha(.85 * fade), flat, Y + .051, 'none');
  }
  if (u < 1) sprite(head, .12, .1, (white ? White : Ink).withAlpha(.95), white ? glow : soft, Y + .052);
}
// White hit: a white sunburst of sixteen thin spikes and a soft flash for .3 s, six sparks. No blood.
function whiteHit(key, c, age) {
  if (age < 0 || age > .3) return;
  const u = age / .3, grow = Math.sqrt(Math.min(1, age / .05)), f = 1 - u * u;
  sprite(c, .8 * grow, .75 * grow, White.withAlpha(.55 * f), glow, Y + .09);
  sprite(c, .3 * grow, .28 * grow, White.withAlpha(.95 * f), glow, Y + .0901);
  for (let i = 0; i < 16; i++) {
    const t = i / 16 * TAU + rand(i + 100) * .3, l = (.25 + .4 * rand(i + 110)) * grow;
    streak(`${key} spike ${i}`, c, { x: c.x + Math.cos(t) * l, z: c.z + Math.sin(t) * l * .9 }, .04 * (1 - u * .5), White.withAlpha(.95 * f), whiteGlow, Y + .091, 3);
  }
  for (let i = 0; i < 6; i++) {
    const t = rand(i + 120) * TAU, v = 1.5 + 2 * rand(i + 130);
    sprite({ x: c.x + Math.cos(t) * v * age, z: c.z + Math.sin(t) * v * age }, .06, .06, White.withAlpha(f), glow, Y + .092);
  }
}
// Black hit: a dark blot and a star of twelve ink shards flung out for .32 s, a red spark, blood
// thrown on along the shot, and a floor spatter behind the pawn that stays.
function blackHit(key, c, pos, dir, age, seed) {
  if (age < 0) return;
  const aim = Math.atan2(dir.z, dir.x);
  if (age < .32) {
    const u = age / .32, grow = Math.sqrt(Math.min(1, age / .05)), f = 1 - u * u;
    sprite(c, .75 * grow, .7 * grow, Ink.withAlpha(.7 * f), soft, Y + .09);
    for (let i = 0; i < 12; i++) {
      const t = i / 12 * TAU + rand(i + 200) * .4, l = (.25 + .38 * rand(i + 210)) * grow, d0 = .08 + age * (.6 + rand(i + 220)) + l * .35;
      shard(`${key} star ${i}`, { x: c.x + Math.cos(t) * d0, z: c.z + Math.sin(t) * d0 * .9 }, t, l, l * (.2 + .15 * rand(i + 230)), Ink.withAlpha(.95 * f), Y + .091);
    }
    if (age < .08) sprite(c, .3, .28, Red.withAlpha(.8 * (1 - age / .08)), glow, Y + .092);
  }
  for (let i = 0; i < 6; i++) {
    const life = .22 + rand(i + 240) * .1, u = age / life; if (u > 1) continue;
    const a = aim + (rand(i + 250) - .5), v = 1.8 + rand(i + 260) * 2.5;
    sprite({ x: c.x + Math.cos(a) * v * age, z: c.z + Math.sin(a) * v * age - 5 * age * age }, .12, .09, Blood.withAlpha(1 - u * u), soft, Y + .089);
  }
  const g = clamp(age / .25), sp = move({ x: pos.x + (rand(seed + 270) - .5) * .3, z: pos.z + Ground }, dir, .3 + rand(seed + 280) * .3);
  sprite(sp, .42 * g, .26 * g, Blood.withAlpha(.7 * g), soft, Floor + .02, aim / D2R + (rand(seed + 290) - .5) * 40);
}

// ---- The coffin -----------------------------------------------------------------------------------
// Half width of the coffin's front at height h: narrow at the foot, widest at the shoulders (.7 up),
// narrower at the head.
const coffinHalf = h => { const k = h / CoffinH; return k < .7 ? lerp(.2, .34, k / .7) : lerp(.34, .24, (k - .7) / .3); };
// base: ground point of the coffin's foot. rise 0..1: how much is above the floor. lid 0..1: open.
// Returns the mouth (the lit inside, where the butterflies come out) as { g, h, screen }.
function coffin(key, base, rise, lid, s, sun, strength) {
  const sunk = (1 - rise) * CoffinH, vis = CoffinH - sunk, L = pawnLayer - .03;
  const mouthH = Math.max(0, CoffinH * .62 - sunk), mouth = { g: base, h: mouthH, screen: { x: base.x, z: base.z + mouthH * Lift } };
  if (rise <= 0) return mouth;
  const levels = [sunk, Math.max(sunk, CoffinH * .7), CoffinH];
  const pt = (h, w) => ({ x: base.x + w, z: base.z + (h - sunk) * Lift });
  const left = (inset = 0) => levels.map(h => pt(h, -coffinHalf(h) + inset)), right = (inset = 0) => levels.map(h => pt(h, coffinHalf(h) - inset));
  const w0 = coffinHalf(sunk);
  band(`${key} shadow`, [{ x: base.x - w0, z: base.z }, { x: base.x - .22 + sun.x * vis, z: base.z + sun.z * vis }], [{ x: base.x + w0, z: base.z }, { x: base.x + .22 + sun.x * vis, z: base.z + sun.z * vis }], Ink.withAlpha(strength * .85), shadowLayer);
  const top = pt(CoffinH, 0);
  rect(`${key} top`, { x: top.x, z: top.z + CoffinDepth / 2 }, .48 + .05, CoffinDepth + .04, 0, CoffinEdge, L);
  rect(`${key} top face`, { x: top.x, z: top.z + CoffinDepth / 2 }, .48, CoffinDepth, 0, CoffinTop, L + .0005);
  band(`${key} edge`, left(-.025), right(-.025), CoffinEdge, L + .001);
  band(`${key} inside`, left(), right(), Soot, L + .002);
  if (lid > 0) {                       // the inside, lit: a pale lining and a white glow
    band(`${key} lining`, left(.05), right(.05), Pale.withAlpha(.9 * lid), L + .003);
    sprite(pt(CoffinH * .55, 0), .9 * lid, 1.1 * lid, White.withAlpha(.55 * lid), glow, L + .004);
  }
  // The lid: the front face, hinged on the right edge. Opening, it narrows toward the hinge and comes
  // .1 toward the viewer. Its inner line and the butterfly emblem go with it.
  const squeeze = 1 - .85 * lid, hinge = q => ({ x: base.x + coffinHalf(CoffinH * .7) + (q.x - base.x - coffinHalf(CoffinH * .7)) * squeeze + lid * .12, z: q.z - lid * .1 });
  band(`${key} lid edge`, left(-.012).map(hinge), right(-.012).map(hinge), CoffinEdge, L + .005);
  band(`${key} lid`, left(.012).map(hinge), right(.012).map(hinge), Ink, L + .006);
  band(`${key} lid line`, left(.05).map(hinge), right(.05).map(hinge), Ash.withAlpha(.8), L + .007);
  band(`${key} lid in`, left(.065).map(hinge), right(.065).map(hinge), Ink, L + .008);
  if (vis > CoffinH * .3) {
    const e = hinge(pt(CoffinH * .66, 0));
    draw(Lines, e.x, L + .009, e.z, .16 * squeeze, .16, 0, White.withAlpha(.85));
    draw(disc, e.x, L + .0095, e.z, .018 * squeeze, .07, 0, White.withAlpha(.85));
  }
  return mouth;
}
// Dust at the coffin's foot while it rises or sinks (u 0..1 over that move).
function footDust(key, base, u) {
  if (u <= 0 || u >= 1) return;
  for (let i = 0; i < 6; i++) {
    const a = (i / 6) * TAU + rand(i + 500), r = .25 + .35 * u * (.6 + .4 * rand(i + 510)), sz = .2 + .3 * u;
    sprite({ x: base.x + Math.cos(a) * r, z: base.z + Math.sin(a) * r * .5 + u * .12 }, sz, sz * .8, Dust.withAlpha(.4 * Math.sin(u * Math.PI)), puff, Y + .01);
  }
}
// Six white streaks rising off the open coffin for .4 s, as the Skill 3 frames show at the opening.
function coffinStreaks(key, mouth, age) {
  if (age < 0 || age > .4) return;
  const u = age / .4;
  for (let i = 0; i < 6; i++) {
    const x = mouth.screen.x + (i - 2.5) * .08 + (rand(i + 520) - .5) * .06, z0 = mouth.screen.z + .15 + u * (.4 + .4 * rand(i + 530)), l = .5 + .7 * rand(i + 540);
    streak(`${key} ${i}`, { x, z: z0 }, { x, z: z0 + l }, .05, White.withAlpha(.9 * (1 - u)), whiteGlow, Y + .08, 4);
  }
}
// The Abnormality's face over the corroded wielder's head: a big butterfly, left wings white, right
// wings black, beating slowly (.6 beats/s), a dim halo behind.
function faceButterfly(head, s, alpha) {
  if (alpha <= 0) return;
  const size = .66, flap = .75 + .25 * Math.abs(Math.cos(Math.PI * .6 * s)), sx = size * .5 * flap, sz = size * .5, L = pawnLayer + .04;
  const q = { x: head.x, z: head.z + .02 * Math.sin(s * 2) };
  sprite(q, .9, .7, Smoke.withAlpha(.25 * alpha), glow, L - .001);
  draw(FillL, q.x, L, q.z, sx, sz, 0, Pale.withAlpha(.55 * alpha));
  draw(FillR, q.x, L, q.z, sx, sz, 0, Ink.withAlpha(.9 * alpha));
  draw(HaloL, q.x, L + .0002, q.z, sx, sz, 0, White.withAlpha(.14 * alpha), whiteGlow);
  draw(HaloR, q.x, L + .0002, q.z, sx, sz, 0, White.withAlpha(.14 * alpha), whiteGlow);
  draw(LinesL, q.x, L + .0004, q.z, sx, sz, 0, White.withAlpha(alpha));
  draw(LinesR, q.x, L + .0004, q.z, sx, sz, 0, White.withAlpha(alpha));
  draw(disc, q.x, L + .0006, q.z, size * .04, size * .17, 0, White.withAlpha(alpha));
  draw(disc, q.x, L + .0007, q.z, size * .018, size * .13, 0, Soot.withAlpha(alpha));
}
// One cloud butterfly's orbit round the wielder: 0.7-2.8 cells out (inside the radius-3 ring),
// 0.3-1.4 up, most turning anticlockwise at .45-1.05 rad/s, bobbing .12.
function orbit(i, t, c) {
  const r = .7 + 2.1 * rand(i + 4000), w = (.45 + .6 * rand(i + 4020)) * (rand(i + 4030) < .8 ? 1 : -1), th = rand(i + 4040) * TAU + w * t;
  const g = { x: c.x + Math.cos(th) * r, z: c.z + Math.sin(th) * r }, h = .3 + 1.1 * rand(i + 4010) + .12 * Math.sin(2.3 * t + i);
  return { g, h, screen: { x: g.x, z: g.z + h * Lift }, heading: (th + Math.sign(w) * Math.PI / 2) / D2R };
}

// ---- Timelines ------------------------------------------------------------------------------------
function burstPlan(p) {
  const shots = []; let stacks = 0, downAt = null;
  for (let k = 0; k < p.shots; k++) {
    const white = k % 2 === 0, t = p.lead + k * p.interval, hit = t + (p.dist - .4) / Speed;
    const add = Math.min(white ? p.whiteStacks : p.blackStacks, p.cap - stacks);
    shots.push({ k, white, t, hit, from: stacks, add });
    stacks += add;
    if (stacks >= p.cap) { downAt = hit; break; }
  }
  const last = downAt ?? shots[shots.length - 1].hit;
  return { shots, downAt, last, end: last + p.hold + (downAt == null ? .3 : .4) };
}
function coffinPlan(p) {
  const tEnd = Open + p.cloud, closeAt = tEnd + .1 + ReturnTime, sinkAt = closeAt + LidClose;
  const ticks = [];
  for (let k = 1; k <= Math.floor(p.cloud + 1e-6); k++) ticks.push(Open + k * CoffinTick);
  return { tEnd, closeAt, sinkAt, ticks, end: sinkAt + Sink + p.hold };
}
const isBurst = p => p.mode === 'burst';
const isOverclock = p => p.mode.startsWith('overclock');

// ---- Drawing ----------------------------------------------------------------------------------------
function drawBurst(s, p, o, who, sun, strength) {
  const B = burstPlan(p), d = dirOf(p.aim), acr = side(d);
  const tpos = move(o, d, p.dist), chest = at(tpos, 'chest', who);
  const M = { pos: tpos, colour: Enemy, cap: p.cap, seed: 1.3, events: [], flinches: [], downAt: B.downAt, fallTurn: d.x >= 0 ? 90 : -90 };
  for (const sh of B.shots) {
    for (let j = 0; j < sh.add; j++) {
      const n = sh.k * 3 + j;
      M.events.push({ t: sh.hit, launch: sh.hit + j * .07, fly: FlyTime, slot: sh.from + j, dark: !sh.white, seed: n,
        from: { g: { x: tpos.x, z: tpos.z + Ground }, h: ChestH },
        via: { x: d.x * (.3 + .4 * rand(n + 600)) + acr.x * (rand(n + 610) - .5) * 1.2, z: d.z * (.3 + .4 * rand(n + 600)) + acr.z * (rand(n + 610) - .5) * 1.2 },
        arc: .45 + .3 * rand(n + 620) });
    }
    if (!sh.white) M.flinches.push({ t: sh.hit, d });
  }
  if (B.downAt != null) for (let k = p.cap; k < TorsoSlots + HeadSlots; k++) {
    const a = rand(k + 700) * TAU, r = 1.1 + .7 * rand(k + 710), g = { x: tpos.x + Math.cos(a) * r, z: tpos.z + Ground + Math.sin(a) * r };
    M.events.push({ t: B.downAt, launch: B.downAt + rand(k + 720) * .15, fly: SwarmIn, slot: k, dark: k % 2 === 1, seed: k, swarm: true,
      from: { g, h: .4 + rand(k + 730) }, via: { x: -Math.sin(a) * .6, z: Math.cos(a) * .6 }, arc: .15 });
  }

  // Floor, then the target, then the wielder and the guns, then the shots in the air.
  markedPawn('sl target', M, s, who, p.size, sun, strength);

  const drawU = smooth(clamp(s / (p.lead * .8))), lower = smooth(clamp((s - B.last - .5) / .4));
  const lastOf = white => B.shots.filter(sh => sh.white === white && sh.t <= s).pop();
  const rockBack = B.shots.reduce((m, sh) => m + .025 * bump((s - sh.t) / .2), 0);
  const stand = move(o, d, -rockBack);
  pawn(stand, { ...who, shirt: Holder });
  const reach = .04 + (HandReach - .04) * drawU * (1 - .75 * lower);
  const hands = [move(move(stand, d, reach), acr, -HandAcross), move(move(stand, d, reach), acr, HandAcross)];   // right: white, left: black
  const gunLayer = d.z > .35 ? pawnLayer - .012 : pawnLayer + .06;
  const guns = [true, false].map((white, i) => {
    const sh = lastOf(white), age = sh ? s - sh.t : -1, gd = unit(hands[i], tpos);
    const tilt = KickTilt * kickAt(age) - (1 - drawU) * 50 * D2R - lower * 40 * D2R;
    const slide = age >= 0 ? KickSlide * bump(Math.min(1, age / .12)) : 0;
    return { hand: hands[i], gd, ...pistol(`sl gun ${i}`, hands[i], gd, white, tilt, slide, gunLayer + i * .004, sun, strength) };
  });
  for (const sh of B.shots) {
    const age = s - sh.t; if (age < 0) continue;
    const g = guns[sh.white ? 0 : 1], m0 = lift(move(g.hand, g.gd, GunLen));
    if (sh.white) {
      whiteMuzzle(`sl muzzle ${sh.k}`, m0, g.gd, g.hand, -1, age);
      shotTrail(`sl trail ${sh.k}`, m0, chest, age, true);
      whiteHit(`sl hit ${sh.k}`, chest, s - sh.hit);
    } else {
      blackMuzzle(`sl muzzle ${sh.k}`, m0, g.gd, age);
      shotTrail(`sl trail ${sh.k}`, m0, chest, age, false);
      blackHit(`sl hit ${sh.k}`, chest, tpos, d, s - sh.hit, sh.k);
    }
  }
  if (B.downAt != null) {              // the white flash as the swarm covers the body
    const a = s - B.downAt - SwarmIn * .8;
    if (a >= 0 && a < .25) sprite(chest, 1.1, 1.2, White.withAlpha(.45 * (1 - a / .25)), glow, Y + .13);
  }
}

function drawCoffin(s, p, o, who, sun, strength) {
  const C = coffinPlan(p), overclock = isOverclock(p), aimR = p.aim;
  const polar = (deg, r) => move(o, dirOf(aimR + deg), r);
  const people = [
    { pos: polar(150, 1.8), colour: Ally, ally: true },
    { pos: polar(0, 2.4), colour: Enemy },
    { pos: polar(-55, 4.2), colour: Enemy },
  ].map((q, i) => ({ ...q, M: { pos: q.pos, colour: q.colour, cap: p.cap, seed: i * 2.1, events: [], flinches: [], downAt: null, fallTurn: q.pos.x >= o.x ? 90 : -90 } }));
  const inside = people.filter(q => Math.hypot(q.pos.x - o.x, q.pos.z - o.z) <= Radius && !(overclock && q.ally));
  // Every tick one butterfly leaves the cloud for each pawn inside: the first slot from
  // (n x 11 + 3) mod 36 on whose butterfly is already circling (out of the coffin .7 s, no dive in
  // the last 1.2 s). The coffin sends that slot a new one .25 s later.
  const dives = [];
  let n = 0;
  const ready = (i, T) => Open + i * .03 + .7 <= T && !dives.some(v => v.i === i && T - v.T < 1.2);
  for (const T of C.ticks) for (const q of inside) {
    const M = q.M, count = M.events.length;
    if (M.downAt != null) continue;
    let i = (n++ * 11 + 3) % CloudN;
    for (let c = 0; c < CloudN && !ready(i, T); c++) i = (i + 5) % CloudN;
    const from = orbit(i, T, o);
    dives.push({ i, T });
    M.events.push({ t: T + DiveTime, launch: T, fly: DiveTime, slot: count, dark: i % 2 === 1, seed: i, from: { g: from.g, h: from.h }, via: { x: 0, z: 0 }, arc: .2 });
    if (count + 1 >= p.cap) {
      M.downAt = T + DiveTime;
      for (let k = p.cap; k < TorsoSlots + HeadSlots; k++) {
        const a = rand(k + 700) * TAU, r = 1.1 + .7 * rand(k + 710), g = { x: M.pos.x + Math.cos(a) * r, z: M.pos.z + Ground + Math.sin(a) * r };
        M.events.push({ t: M.downAt, launch: M.downAt + rand(k + 720) * .15, fly: SwarmIn, slot: k, dark: k % 2 === 1, seed: k, swarm: true, from: { g, h: .4 + rand(k + 730) }, via: { x: -Math.sin(a) * .6, z: Math.cos(a) * .6 }, arc: .15 });
      }
    }
  }

  // The floor: a pale ring at the true radius and a dim floor inside it while the coffin is open.
  const ringA = smooth(clamp((s - Open) / .3)) * (1 - smooth(clamp((s - C.closeAt) / .3)));
  if (ringA > 0) {
    sprite(o, Radius * 2.3, Radius * 2.3, Ink.withAlpha(.16 * ringA), soft, Floor + .02);
    circle(o, Radius * (.9 + .1 * ringA), .75 * ringA, Floor + .03, Pale);
    circle(o, Radius * .985, .3 * ringA, Floor + .03, Pale);
  }
  // The coffin behind the wielder.
  const base = { x: o.x + CoffinBack.x, z: o.z + CoffinBack.z };
  const riseU = clamp(s / Rise), sinkU = clamp((s - C.sinkAt) / Sink);
  const rise = smooth(riseU) * (1 - smooth(sinkU));
  const lid = smooth(clamp((s - Rise) / LidOpen)) * (1 - smooth(clamp((s - C.closeAt) / LidClose)));
  const mouth = coffin('sl coffin', base, rise, lid, s, sun, strength);
  footDust('sl rise dust', base, riseU < 1 ? riseU : sinkU);
  coffinStreaks('sl streaks', mouth, s - Rise - .1);

  // The pawns: the wielder (guns hanging down), the face if corroded, then everyone else.
  for (const q of people) markedPawn(`sl pawn ${q.colour === Ally ? 'ally' : q.pos.x}`, q.M, s, who, p.size, sun, strength);
  pawn(o, { ...who, shirt: Holder });
  const d = dirOf(aimR), acr = side(d);
  [true, false].forEach((white, i) => {
    const hand = move(move(o, d, .04), acr, (i ? 1 : -1) * .2);
    pistol(`sl gun ${i}`, hand, d, white, -55 * D2R, 0, pawnLayer + .06 + i * .004, sun, strength);
  });
  if (!overclock) faceButterfly(at(o, 'head', who), s, smooth(clamp(s / .4)) * (1 - smooth(clamp((s - C.sinkAt) / Sink))));

  // The cloud. Slot i leaves the coffin at Open + .03 i (or .25 s after its last dive), flies to its
  // orbit in .7 s, circles, and at the end flies back into the coffin over .7 s.
  for (let i = 0; i < CloudN; i++) {
    const mine = dives.filter(v => v.i === i && v.T <= s), last = mine.length ? mine[mine.length - 1].T : null;
    const rel = last == null ? Open + i * .03 : last + .25, back = C.tEnd + .1 + rand(i + 800) * .15;
    if (s < rel || s >= back + ReturnTime || rel >= back) continue;
    let q, heading, size = p.size, alpha = 1;
    if (s >= back) {
      const u = (s - back) / ReturnTime, from = orbit(i, back, o), e = { from, via: { x: 0, z: 0 }, arc: .3, seed: i };
      q = flyAt(e, u, mouth); const q2 = flyAt(e, Math.min(1, u + .03), mouth);
      heading = Math.atan2(q2.screen.z - q.screen.z, q2.screen.x - q.screen.x) / D2R;
      size *= 1 - .5 * u; alpha = 1 - smooth(clamp((u - .7) / .3));
    } else if (s < rel + .7) {
      const u = (s - rel) / .7, to = orbit(i, rel + .7, o), e = { from: mouth, via: { x: (rand(i + 810) - .5) * .8, z: (rand(i + 820) - .5) * .8 }, arc: .6, seed: i };
      q = flyAt(e, u, to); const q2 = flyAt(e, Math.min(1, u + .03), to);
      heading = Math.atan2(q2.screen.z - q.screen.z, q2.screen.x - q.screen.x) / D2R;
      size *= .5 + .5 * clamp(u * 2);
    } else {
      q = orbit(i, s, o); heading = q.heading;
    }
    butterflyShadow(q.g, Math.max(0, q.h), size, sun, strength, alpha);
    butterfly(q.screen, size, heading, flapAt(s, 3, .15, i), i % 2 === 1, alpha, Y + .12 + i * .0008);
  }
}

export default {
  kit: 'E.G.O. weapons', label: 'Solemn Lament (sketch)',
  params: {
    mode: { label: 'Show', value: 'burst', options: ['burst', 'corroded: the coffin', 'overclock: the coffin, hostiles only'], group: 'Showcase' },
    aim: P('Aim (degrees)', 0, 0, 360, 5, 'Showcase'),
    dist: P('Target distance (cells)', 5, 2, 10, .5, 'Showcase'),
    shots: P('Shots in the burst (ammo 20)', 8, 1, 20, 1, 'Rule'),
    interval: P('Time between shots (s)', .25, .1, 1, .05, 'Rule'),
    whiteStacks: P('White shot: stacks', 2, 0, 5, 1, 'Rule'),
    blackStacks: P('Black shot: stacks', 1, 0, 5, 1, 'Rule'),
    cap: P('Stack cap (down at)', 10, 2, 20, 1, 'Rule'),
    cloud: P('Coffin: seconds shown (rule 15, overclock 5)', 4, 1, 15, 1, 'Timing (s)'),
    lead: P('Draw and aim', .35, .1, 1, .05, 'Timing (s)'),
    hold: P('Show the result', 1.2, .3, 3, .1, 'Timing (s)'),
    size: P('Butterfly span (cells)', .3, .1, .5, .01, 'Shape'),
  },
  duration(p) { return isBurst(p) ? burstPlan(p).end : coffinPlan(p).end; },
  phases(p) {
    if (isBurst(p)) {
      const B = burstPlan(p), out = [{ name: 'Draw', t: 0 }, { name: 'Shot 1 (white)', t: p.lead }];
      if (B.shots.length > 1) out.push({ name: 'Shot 2 (black)', t: B.shots[1].t });
      if (B.downAt != null) out.push({ name: `Stack ${p.cap}: down`, t: B.downAt });
      out.push({ name: 'Result', t: B.last + .6 });
      return out;
    }
    const C = coffinPlan(p), out = [{ name: 'Coffin rises', t: 0 }, { name: 'Opens', t: Rise }];
    if (C.ticks.length) out.push({ name: 'Stack 1', t: C.ticks[0] });
    out.push({ name: 'Returns', t: C.tEnd + .1 }, { name: 'Result', t: C.sinkAt + Sink });
    return out;
  },
  events(p) {
    if (isBurst(p)) return burstPlan(p).shots.flatMap(sh => [
      { t: sh.t, type: 'sound', def: sh.white ? 'RimArt_SolemnLamentWhite' : 'RimArt_SolemnLamentBlack' },
      { t: sh.t, type: 'shake', value: .012 }]);
    return [{ t: 0, type: 'sound', def: 'RimArt_SolemnLamentCoffin' }, { t: Rise, type: 'shake', value: .03 }];
  },

  draw(s, p, { origin: o, scene }) {
    if (s < 0 || s >= (isBurst(p) ? burstPlan(p).end : coffinPlan(p).end)) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const who = { body: 'average', sun, shadow: strength };
    if (isBurst(p)) drawBurst(s, p, o, who, sun, strength);
    else drawCoffin(s, p, o, who, sun, strength);
  },
};
