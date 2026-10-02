// Solemn Lament — E.G.O. weapon proposal. The pictures are ported to Source/RimArt/Ego/EgoSolemnLament* (previews
// "E.G.O.: solemn lament: ..."); no weapon, ability or rule draws them yet. The corroded walk
// (2026-10-02: the wielder walks, the coffin stays) is ported too: EgoSolemnLamentCoffin takes the
// wielder's point now and the coffin's point apart.
// Funeral of the Dead Butterflies (Lobotomy Corporation, WAW): the white and black pair of handguns.
// The rules are docs/ego-weapons.md, "Weapon 2: Solemn Lament" (design agreed 2026-09-30, numbers
// are placeholders and become XML fields on CompProperties_EgoWeapon):
//   The pair: one weapon item that draws two guns and alternates its own shots, white first.
//         White shot: no body damage, 2 Butterfly stacks. Black shot: 9 damage and 1 stack.
//         Range 10, 0.25 s between shots.
//   Butterfly: a stacking hediff, -7.5 % consciousness per stack, cap 10. At the cap the pawn goes
//         down covered in butterflies (the game downs a pawn under 30 % consciousness). -1 stack
//         per 10 s (not shown: the sketch is 3 s long).
//   Ammo: 20 shots, reload 3 s (not shown).
//   Corrosion (shared system): the coffin. A butterfly cloud on the corroded pawn; every pawn within
//         radius 3 takes 1 stack per second, any faction, the wielder excluded. 30 s, interval 1 s.
//         The wielder walks to the nearest pawn of any faction and stays next to it; the coffin
//         stays where it rose and the cloud and its ring go with the wielder (changed 2026-10-02,
//         PR #153). The funeral: a downed pawn left in the cloud keeps taking stacks and dies at 20
//         (drawn in "corroded: the funeral"; ported: EgoSolemnLamentMark.Darken and DeadAt, the lift in
//         EgoSolemnLamentButterflies.DrawMark, preview "solemn lament: corroded, the funeral").
//         Overclock: the same coffin for 5 s, hostiles only (allies inside are skipped), mood -15.
//         Overclock does not walk.
//   Rule sliders (stacks per shot, cap, interval) are there for the balance pass: with the doc's
//   numbers the 7th shot reaches the cap, 1.5 s after the first.
//
// Look, from the source: the Limbus skill videos of Lobotomy E.G.O::Solemn Lament Yi Sang
// (limbuscompany.wiki.gg, Skill_1/2/3.mp4, looked at 2026-09-30). What the frames show and what the
// sketch does with it:
//   Guns: one white pistol and one black pistol, one in each hand, arms out, firing in turn.
//   White shot: a white crescent swept round the gun hand, a ragged white blast thrown forward off
//         the muzzle, a wide white-grey smoke band to the target that thins into a wispy line and
//         hangs, white dashes flying on along it; on the hit the black hit's shape in white: a
//         solid white splat with grey cracks, breaking into see-through shards that fly out.
//   Black shot: at the muzzle a black ink splat with a white glow behind it and a torn ink smear
//         thrown along the shot (drips, slivers), a dark line; on the hit one solid black ink
//         splat over the torso with a spiky edge, red streaks inside it, black droplets and slivers
//         flung out, then it breaks into chunks that fly off; blood.
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
//   0.35  shot 1, white: crescent and a ragged white blast off the muzzle, the round reaches the
//         raider 5 cells off in .08 s leaving a smoke band that thins and hangs until .8 s, white
//         dashes fly on along it, a white splat on the hit breaking into shards; 2 pale butterflies loop off the chest and land on the body in .5 s;
//         the white gun kicks up 28 degrees and settles in .3 s
//   0.60  shot 2, black: muzzle splat and ink smear, dark line; on the hit a black ink splat over the chest in
//         .06 s with red inside, breaking into flying chunks from .1 s, gone by .4 s; blood thrown
//         on, a floor spatter that stays, the raider flinches .07 cells; 1 dark butterfly lands
//   ...   one shot every .25 s, alternating. The raider sways more with each stack (consciousness).
//         Ten pips over the head count the stacks, each in its butterfly's colour.
//   1.93  shot 7 hits: stack 10, the cap. 20 more butterflies spiral in from 1-2 cells away over
//         .45 s and cover the body; the raider falls backward (turned 90 degrees) over .35 s and
//         stays down, covered. The guns lower. The result is held 1.2 s.
// Order, "corroded: the coffin" (cloud 4 s shown of the rule's 30):
//   0.00  the coffin rises out of the floor behind the wielder over .5 s, dust at its base; the
//         butterfly face fades in over the head; the guns hang down
//   0.50  the lid swings open over .3 s, the inside lit white
//   0.60  the opening (Skill 3): a white flash 3.2 cells wide with twelve rays, a white glow on the
//         floor, twelve white beams shooting up out of the floor and off the coffin, white smoke
//         puffs pushed out; 24 butterflies burst out through it within .12 s, the other 12 follow
//         .05 s apart; they circle the wielder 0.7-2.8 cells out at 0.3-1.4 cells up; a pale floor
//         ring at the true radius 3 and a dim floor inside it
//   0.80  the wielder walks along the aim to the nearest pawn (the ally, "Target distance" off) and
//         stops 1 cell short of it: 4 cells in 1.3 s at the default, eased, peaking at 4.6 cells/s.
//         The coffin stays open where it rose. The cloud, the ring and the dim floor go with the
//         wielder; butterflies still flying out of the coffin bend after it, toward their orbit
//         round where the wielder is at that moment.
//         The ally and the raider beyond it are outside the ring at the start and inside on arrival.
//   1.60  and every 1 s: one butterfly leaves the cloud for each pawn inside the ring round the
//         wielder and lands on it in .5 s (+1 stack); .25 s later the coffin sends a new one out,
//         which flies from the coffin to the cloud: .7 s, or 6 cells/s when the wielder was further
//         than 4.2 cells from the coffin at the dive.
//   4.60  the cloud flies back to the coffin (.7 s, or 6 cells/s from further off), the lid closes,
//         the coffin sinks back into the floor over .45 s. The landed butterflies stay (the stacks
//         stay).
// "overclock": the wielder holds; the same without the face and with the ally skipped (no pips,
// nothing lands); its own layout: an ally and a raider inside the ring, a raider outside. It stops at
// the cap: a downed pawn takes no more.
// Order, "corroded: the funeral" (the corroded layout with a 23 s cloud; the cloud slider is not used).
// Source: Lobotomy's Funeral kills an employee whose sanity it empties: "covered in butterflies, then
// fall to the ground with their eyes closed" (Cogitopedia); its lore says the dead become "beautiful
// beings with small wings". Down is the first half here; the lift is the death.
//   11.10  the ally's 10th stack: the swarm covers it and it falls, as in the burst. The cover is about
//          half white, half dark. The wielder stays beside it (open in the doc: whether the walk goes
//          to downed pawns).
//   12.10  and every 1 s: a dark butterfly dives from the cloud onto a white one of the cover, and with
//          it about a tenth of the cover's white butterflies turn dark over .3 s, so by stack 19 the
//          cover is nearly all dark: the countdown, and the cue to carry the pawn out.
//   21.10  stack 20: dead. A pale flash on the body (.3 s); the whole cover (38 butterflies plus the
//          divers) lifts off within .15 s, rises .6-1.4 cells over .6 s turning white, then flies into
//          the coffin's open mouth at 6 cells/s (never under .7 s), shrinking and fading in. The body is
//          left bare.
//   12.10 / 22.10  the raider goes down and dies the same way, 1 s after the ally.
//   23.70  the cloud flies home, the lid closes on the dead pawns' butterflies, the coffin sinks.
//
// Drawing: the shots, trails, crescents and hits are level shapes at chest height (lib/pawn.js
// chest = .05 north of the cell centre), so they turn with the aim and need no per-facing method.
// The guns are side views laid flat and turned to the aim, grip toward the viewer, mirrored when
// aiming west, as RimWorld draws equipment; aiming north they draw under the pawn layer (held in
// front of the body, away from the viewer). The coffin stands facing the viewer at every aim (a fixed screen orientation, like
// Twin Maw's jaws): a front face .68 wide and 2.0 tall (1.2 on screen, a little over the pawn's
// 1.17, as in the source), a thin top face .26 deep,
// a white edge, a shadow along the sun from its base. It rises by drawing only the part above the
// floor. A butterfly is four flat meshes built once: the wing fill (four fans; ink for the black
// shot's, a faint pale film for the white shot's), a wide outline for the additive glow (white
// shot's only), the veins (7 + 5 per side, three or four uneven cross veins per gap, most bent;
// grey on the dark ones) and the edge (outline strips, antennae), then a white-edged body dash.
// Resting butterflies draw over the hit effects and flying ones over those. It is scaled across the body for the wing beat (3 beats/s flying, .7 at rest);
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
const GunLen = .32, GunW = .075, HandAcross = .13, HandReach = .22;
const ChestLift = .05;                // lib/pawn.js: the chest is .05 north of the cell centre on screen
const Ground = -.42;                   // a standing pawn's ground contact on screen (feet + .12, as its shadow)
const ChestH = (ChestLift - Ground) / Lift;
const lift = q => ({ x: q.x, z: q.z + ChestLift });
const CoffinH = 2.0, CoffinDepth = .26, CoffinBack = { x: -.18, z: .32 };
const Rise = .5, LidOpen = .3, Open = .6, ReturnTime = .7, LidClose = .2, Sink = .45;
const CloudN = 36, CloudBurst = 24, TorsoSlots = 30, HeadSlots = 8;
const cloudOut = i => i < CloudBurst ? Open + rand(i + 830) * .12 : Open + .15 + (i - CloudBurst) * .05;
const cloudFly = i => i < CloudBurst ? .55 : .7;
// The corroded wielder's walk (doc, changed 2026-10-02): once the cloud is out it walks along the aim to
// the nearest pawn and stops Beside cells from it, eased at both ends and peaking at WalkSpeed (a pawn's
// base move speed). The coffin stays where it rose. Overclock never walks. A butterfly flying between
// the coffin and a wielder further off flies at FarSpeed, never in less than the near time (.7 s).
const WalkFrom = Open + .2, WalkSpeed = 4.6, Beside = 1, FarSpeed = 6;
// The funeral (doc, changed 2026-10-02): a downed pawn left in the cloud keeps taking stacks. Each one past
// the cap turns about a tenth of the white butterflies on its body dark over TurnTime s; at the death count
// the whole cover lifts off (LiftTime s, .6-1.4 cells up, turning white) and flies into the coffin's mouth
// at FarSpeed, never in under .7 s. FuneralCloud: the funeral showcase's cloud, long enough for two deaths.
const TurnTime = .3, LiftTime = .6, FuneralCloud = 23;

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
// Veins: straight from the root to these outline points. In each gap between two neighbouring
// veins, three or four cross veins at uneven heights, each end at its own height and most bent
// at a middle point, so the cells come out uneven, like the source's leaf-skeleton lace.
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
    if (outline) { ring(mirror(Fore), outline); ring(mirror(Hind), outline); }
    if (veins) [[ForeRaw, ForeVeins, 1], [HindRaw, HindVeins, 2]].forEach(([w, ids, seed]) => {
      const root = [sgn * w[0][0], w[0][1]], tip = j => [sgn * w[ids[j]][0], w[ids[j]][1]];
      const along = (j, f) => { const t = tip(j); return [root[0] + (t[0] - root[0]) * f, root[1] + (t[1] - root[1]) * f]; };
      ids.forEach((_, j) => quad(along(j, .15), along(j, .98), veins));
      for (let j = 0; j + 1 < ids.length; j++) {
        const n = 3 + (rand(seed * 31 + j) > .5 ? 1 : 0);
        for (let k = 0; k < n; k++) {
          const f = .28 + (k + .5) / n * .66, r1 = rand(seed * 97 + j * 7 + k), r2 = rand(seed * 89 + j * 5 + k + 40), r3 = rand(seed * 83 + j * 3 + k + 80);
          const a = along(j, f + (r1 - .5) * .16), b = along(j + 1, f + (r2 - .5) * .16);
          if (r3 < .35) { quad(a, b, veins); continue; }
          const mx = (a[0] + b[0]) / 2, mz = (a[1] + b[1]) / 2, out = norm(mx - root[0], mz - root[1]), bend = (r3 - .67) * .12;
          const mid = [mx + out[0] * bend, mz + out[1] * bend];
          quad(a, mid, veins); quad(mid, b, veins);
        }
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
const Edge = lineMesh('sl lace edge', both, { outline: Outline, antennae: Vein }), Veins = lineMesh('sl lace veins', both, { veins: Vein });
const LinesR = lineMesh('sl lace r', [1], { outline: Outline, veins: Vein, antennae: Vein }), LinesL = lineMesh('sl lace l', [-1], { outline: Outline, veins: Vein, antennae: Vein });
const HaloR = lineMesh('sl lace halo r', [1], { outline: HaloW }), HaloL = lineMesh('sl lace halo l', [-1], { outline: HaloW });
const FillR = fillMesh('sl lace fill r', [1]), FillL = fillMesh('sl lace fill l', [-1]);

// heading: degrees the head points (0 east, 90 north). flap: 0..1 of the full span.
// The Living (white shot): a faint pale film, white edge and veins, a soft glow round the edge.
// The Departed (dark, black shot): a near-opaque ink fill, a white edge, grey veins and no glow, so
// it stays dark at normal zoom, where the lines would otherwise outweigh a small fill.
// dark is true/false, or 0..1 for one turning (the funeral's cover going dark, the lift going white).
function butterfly(q, size, heading, flap, dark, alpha, layer) {
  if (alpha <= .01) return;
  const k = +dark, rot = 90 - heading, sx = size * .5 * flap, sz = size * .5;
  draw(Fill, q.x, layer, q.z, sx, sz, rot, Color.Lerp(Pale.withAlpha(.22 * alpha), Ink.withAlpha(.94 * alpha), k));
  if (k < 1) draw(Halo, q.x, layer + .0002, q.z, sx, sz, rot, White.withAlpha(.14 * alpha * (1 - k)), whiteGlow);
  draw(Veins, q.x, layer + .0003, q.z, sx, sz, rot, Color.Lerp(White.withAlpha(.95 * alpha), Ash.withAlpha(.75 * alpha), k));
  draw(Edge, q.x, layer + .0004, q.z, sx, sz, rot, White.withAlpha(lerp(.95, .85, k) * alpha));
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
// M: { pos, colour, cap, seed, events, flinches, downAt, fallTurn }, and for the funeral deadAt (when
// it died), turnAt (slot -> when the pale butterfly there turns dark) and mouth (the coffin's mouth the
// cover flies into). An event is one butterfly: { t: when the stack counts, launch, fly, slot, dark,
// from, via, arc, seed, swarm }. Swarm events are the knockdown's cover, not stacks.
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
  if (who.actors !== false) {
    sprite({ x: M.pos.x + sun.x * .5, z: M.pos.z + Ground + sun.z * .5 }, .94, .42, Ink.withAlpha(strength * (1 - fall)), soft, shadowLayer);
    if (fall > 0) sprite({ x: M.pos.x + sun.x * .2, z: M.pos.z + sun.z * .2 }, 1.3, .42, Ink.withAlpha(strength * fall), soft, shadowLayer, turn + 90);
    pawn(pos, { ...who, shirt: M.colour, downed: fall > 0, turn, shadow: 0 });
  }

  M.events.forEach((e, i) => {
    const age = s - e.launch; if (age < 0) return;
    const sl = slotAt(pos, e.slot, turn), gz = lerp(M.pos.z + Ground, sl.z, fall);
    const to = { g: { x: sl.x, z: gz }, h: (sl.z - gz) / Lift }, sz = size * (e.swarm ? .9 : 1);
    const turnAt = M.turnAt?.[e.slot], dk = e.dark ? 1 : turnAt != null ? clamp((s - turnAt) / TurnTime) : 0;
    const lifted = M.deadAt != null ? s - M.deadAt - rand(i + 5100) * .15 : -1;
    if (lifted >= 0) { liftOff(M, i, lifted, to, sz, dk, sun, strength); return; }
    const u = age / e.fly;
    if (u < 1) {
      const q = flyAt(e, u, to), q2 = flyAt(e, Math.min(1, u + .03), to);
      const heading = Math.atan2(q2.screen.z - q.screen.z, q2.screen.x - q.screen.x) / D2R;
      const fade = e.swarm ? clamp(u / .25) : 1;   // the swarm fades in where it appears
      butterflyShadow(q.g, Math.max(0, q.h), sz, sun, strength, fade);
      butterfly(q.screen, sz, u > .85 ? lerp(heading, sl.heading, (u - .85) / .15) : heading, flapAt(s, 3, .15, i), e.dark, fade, Y + .14 + i * .0008);
    } else {
      butterfly(sl, sz, sl.heading + 8 * Math.sin(s * 1.3 + i), flapAt(s, .7, .55, i), dk, 1, Y + .1 + i * .0008);   // over the hits, as the source draws them over the ink
    }
  });
  if (M.events.length && fall < .6) pips(M, counted, at(pos, 'headTop', who), 1 - fall / .6);
  if (M.deadAt != null) {             // the funeral: a pale flash on the body as the cover lifts
    const a = s - M.deadAt;
    if (a >= 0 && a < .3) sprite(slotAt(pos, 0, turn), 1.3, 1.1, Pale.withAlpha(.5 * (1 - a / .3)), glow, Y + .13);
  }
}
// The funeral's lift, a seconds after butterfly i's start: it rises from where it rests (rest) .6-1.4
// cells over LiftTime s, drifting up to .3 cells, turning from its darkness (dark) to white; then it flies
// into the coffin's mouth at FarSpeed (never under .7 s), shrinking to half and fading at the end, as the
// cloud flies home. Gone after that: the coffin keeps it.
function liftOff(M, i, a, rest, size, dark, sun, strength) {
  const top = { g: { x: rest.g.x + (rand(i + 5300) - .5) * .6, z: rest.g.z + (rand(i + 5310) - .5) * .6 }, h: rest.h + .6 + .8 * rand(i + 5200) };
  let q, q2, sz = size, alpha = 1;
  if (a < LiftTime) {
    const e = { from: rest, via: { x: 0, z: 0 }, arc: 0, seed: i + 5400 }, u = a / LiftTime;
    q = flyAt(e, u, top); q2 = flyAt(e, Math.min(1, u + .03), top);
  } else {
    const fly = Math.max(ReturnTime, Math.hypot(M.mouth.g.x - top.g.x, M.mouth.g.z - top.g.z) / FarSpeed), u = (a - LiftTime) / fly;
    if (u >= 1) return;
    const e = { from: top, via: { x: (rand(i + 5500) - .5) * 1.2, z: (rand(i + 5510) - .5) * 1.2 }, arc: .4, seed: i + 5600 };
    q = flyAt(e, u, M.mouth); q2 = flyAt(e, Math.min(1, u + .03), M.mouth);
    sz *= 1 - .5 * u; alpha = 1 - smooth(clamp((u - .7) / .3));
  }
  const heading = Math.atan2(q2.screen.z - q.screen.z, q2.screen.x - q.screen.x) / D2R;
  butterflyShadow(q.g, Math.max(0, q.h), sz, sun, strength, alpha);
  butterfly(q.screen, sz, heading, flapAt(a, 3, .15, i), dark * (1 - smooth(a / LiftTime)), alpha, Y + .14 + i * .0008);
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
// A pistol as RimWorld draws a gun: its side view laid flat and turned to the aim, the grip hanging
// on the side toward the viewer (mirrored when aiming west, as the game flips the sprite). Slide
// .34 long and .065 high with a lit top edge, a barrel tip, a grip .12 long raked back, a trigger
// guard; the black gun gets a grey outline so it reads on a dark coat. Level at chest height from
// the hand's ground point (the top of the grip); slide pushes it back; tilt (radians) swings the
// muzzle up (cos(tilt) along d, Lift x sin(tilt) north) or, negative, down. The shadow stays flat.
const gunUp = u => u.x >= 0 ? { x: -u.z, z: u.x } : { x: u.z, z: -u.x };
const MuzzleUp = .035;
function muzzleAt(hand, d) { const q = lift(hand), up = gunUp(d); return { x: q.x + d.x * GunLen + up.x * MuzzleUp, z: q.z + d.z * GunLen + up.z * MuzzleUp }; }
function pistol(key, hand, d, white, tilt, slide, layer, sun, strength) {
  const base = move(hand, d, -slide), q = lift(base);
  const dv = { x: d.x * Math.cos(tilt), z: d.z * Math.cos(tilt) + Lift * Math.sin(tilt) };
  const len = Math.hypot(dv.x, dv.z), u = { x: dv.x / len, z: dv.z / len }, up = gunUp(u);
  const P = (a, b) => ({ x: q.x + u.x * a * len + up.x * b, z: q.z + u.z * a * len + up.z * b });
  const sh = { x: base.x + sun.x * ChestH + d.x * GunLen * .5, z: base.z + Ground + sun.z * ChestH + d.z * GunLen * .5 };
  sprite(sh, GunLen * Math.cos(tilt) + .08, GunW * 1.6, Ink.withAlpha(strength * .45), soft, shadowLayer, -Math.atan2(d.z, d.x) / D2R);
  // Each part is four corners (along, up): bottom-back, top-back, bottom-front, top-front.
  const parts = [
    ['grip', [[-.075, -.115], [-.04, .005], [0, -.12], [.04, .005]], white ? Ash : Soot],
    ['guard', [[.03, -.045], [.03, 0], [.1, -.045], [.1, 0]], white ? Ash : Soot],
    ['slide', [[-.05, 0], [-.05, .065], [.29, 0], [.29, .065]], white ? Pale : Ink],
    ['tip', [[.29, .012], [.29, .052], [GunLen, .012], [GunLen, .052]], white ? Ash : Soot],
  ];
  if (!white) parts.forEach(([name, c], i) => {    // the black gun's outline, .012 out from each part
    const ca = (c[0][0] + c[3][0]) / 2, cb = (c[0][1] + c[3][1]) / 2, g = ([a, b]) => { const n = norm(a - ca, b - cb); return P(a + n[0] * .012, b + n[1] * .012); };
    band(`${key} ${name} line`, [g(c[0]), g(c[2])], [g(c[1]), g(c[3])], Ash.withAlpha(.8), layer + i * .0002);
  });
  parts.forEach(([name, c, colour], i) => band(`${key} ${name}`, [P(...c[0]), P(...c[2])], [P(...c[1]), P(...c[3])], colour, layer + .001 + i * .0002));
  band(`${key} edge`, [P(-.045, .05), P(.285, .05)], [P(-.045, .064), P(.285, .064)], (white ? White : Ash).withAlpha(.9), layer + .002);
  return { muzzle: P(GunLen, MuzzleUp), dir: u };
}
// The barrel's kick after a shot: up in .04 s, then a damped swing back to level (about .3 s).
const kickAt = age => age < 0 ? 0 : age < KickUp ? Math.sin(age / KickUp * Math.PI / 2) : Math.max(0, Math.exp(-KickDamp * (age - KickUp)) * Math.cos(KickSwing * (age - KickUp)));

// White shot at the muzzle: a flash with seven short spikes for .08 s; the blast, a ragged white
// splat (the ink's shapes, white) thrown forward, centred .3 ahead and stretched 2.6x along the
// shot, out to .22 cells in .03 s and shrinking away by .15 s (Skill 1 2.93 s, Skill 2 3.48 s);
// and the crescent: a white arc swept round the gun hand on its outer side in .05 s, thick in the
// middle, gone in .18 s.
function whiteMuzzle(key, m, dir, hand, out, age, seed) {
  if (age < 0 || age > .18) return;
  const f = 1 - age / .18, aim = Math.atan2(dir.z, dir.x);
  if (age < .15) {
    const R = .22 * (1 - Math.pow(1 - clamp(age / .03), 3)) * (1 - Math.pow(clamp((age - .04) / .11), 1.5));
    if (R > .01) {
      const c = move(m, dir, .3);
      draw(Splats[(seed + 2) % 3], c.x, Y + .0605, c.z, R * 2.6, R * .8, -aim / D2R, White.withAlpha(.8), whiteGlow);
      draw(Splats[seed % 3], c.x, Y + .0606, c.z, R * 1.7, R * .5, -aim / D2R, White.withAlpha(.95));
    }
  }
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
// Black shot at the muzzle, as the Limbus frames draw it (Skill 1: a black splat with a white glow
// behind it; Skill 2: a torn ink smear thrown along the shot):
//   a soft white glow .9 cells wide, .2 ahead of the muzzle, for .12 s, so the dark flash still
//   reads as a flash; a black ink splat (the hit's shapes) centred .22 ahead (its back at the muzzle), stretched 1.6x along the shot, out to
//   .2 cells in .04 s, shrinking away from .06 s, opaque, gone by .2 s;
//   a torn ink smear from the muzzle along the shot, out to .95 cells in .05 s, .15 wide at the
//   muzzle and tapering, ragged on both edges; from .06 s its back end chases the front, gone by .26 s;
//   six drips thrown sideways off the smear and eight pointed slivers flung forward in a 70-degree
//   cone at 2-4 cells/s, all shrinking, gone by .3 s.
function blackMuzzle(key, m, dir, age, seed) {
  if (age < 0 || age > .3) return;
  const aim = Math.atan2(dir.z, dir.x), across = side(dir);
  if (age < .12) { const g = 1 - age / .12; sprite(move(m, dir, .2), .9 * g, .75 * g, White.withAlpha(.8 * g), glow, Y + .061); }
  const grow = 1 - Math.pow(1 - clamp(age / .04), 3), R = .2 * grow * (1 - Math.pow(clamp((age - .06) / .14), 1.5));
  if (R > .01) {
    const c = move(m, dir, .22);
    draw(Splats[(seed + 1) % 3], c.x, Y + .063, c.z, R * 1.6, R * .9, -aim / D2R, Ink.withAlpha(.95));
  }
  const reach = .95 * (1 - Math.pow(1 - clamp(age / .05), 2)), tail = Math.pow(clamp((age - .06) / .2), 1.3) * reach;
  if (reach - tail > .02) {
    const N = 14, a = [], b = [];
    for (let i = 0; i <= N; i++) {
      const u = i / N, d = tail + (reach - tail) * u, w = .075 * Math.pow(1 - u, .6) + .008, q = move(m, dir, d);
      const up = w * (.5 + .9 * rand(seed * 41 + i)), dn = w * (.5 + .9 * rand(seed * 43 + i + 7));
      a.push(move(q, across, up)); b.push(move(q, across, -dn));
    }
    band(`${key} smear`, a, b, Ink.withAlpha(.95), Y + .062);
  }
  for (let i = 0; i < 6; i++) {
    const u = age / (.2 + .1 * rand(seed * 47 + i)); if (u >= 1) continue;
    const q = move(move(m, dir, .15 + .7 * rand(seed * 53 + i)), across, (i % 2 ? 1 : -1) * (.05 + (1 + rand(seed * 59 + i)) * age)), r = .028 * (1 - u * u);
    draw(disc, q.x, Y + .0625, q.z - 1.5 * age * age, r, r, 0, Ink);
  }
  for (let i = 0; i < 8; i++) {
    const u = age / (.22 + .08 * rand(seed * 61 + i)); if (u >= 1) continue;
    const t = aim + (rand(seed * 67 + i) - .5) * 1.2, v = 2 + 2 * rand(seed * 71 + i), d0 = .15 + v * age;
    chunk(`${key} sliver ${i}`, { x: m.x + Math.cos(t) * d0, z: m.z + Math.sin(t) * d0 }, t, .05 * (1 - u), seed * 73 + i, Ink.withAlpha(.95), Y + .064);
  }
}
// The white shot's line, as Skill 1 draws it (2.93-3.12 s): the round's bright core, then a wide
// white-grey smoke band the whole way to the target that thins into a wispy line and hangs in the air,
// with white dashes flying on along it and sparks drifting off.
//   core: a white line .03 wide in a glow .12 wide, drawn out at Speed, gone .1 s after it arrives;
//   smoke band: .18 cells wide (narrower in the first sixth, at the muzzle), ragged edges, thinning
//     to .06 over .45 s after the round arrives and drifting sideways in two uneven waves, held,
//     then gone between .45 and .8 s;
//   dashes: fourteen white dashes .2-.6 long anywhere along it within .25 either side, flying on
//     at 8-12 cells/s for .14-.3 s, starting in the first .12 s;
//   sparks: eight white dots off the line, drifting up for .6 s.
function whiteTrail(key, from, to, age, seed) {
  if (age < 0 || age > .8) return;
  const dist = Math.hypot(to.x - from.x, to.z - from.z), d = unit(from, to), acr = side(d), flight = dist / Speed;
  const reach = clamp(age / flight), thin = clamp((age - flight) / .45), fade = 1 - smooth(clamp((age - .45) / .35));
  const N = 24, a = [], b = [];
  for (let i = 0; i <= N; i++) {
    const u = i / N * reach, q = move(from, d, dist * u), off = .03 * thin * (Math.sin(i * .9 + age * 5 + seed) + .7 * Math.sin(i * 2.3 - age * 3 + seed * 2) + .5 * (rand(seed * 79 + i) - .5));
    const w = (.09 - .06 * thin) * (.35 + .65 * Math.min(1, u * 6)) * (.75 + .25 * Math.sin(i * 1.7 + age * 4 + seed));
    a.push(move(q, acr, off + w)); b.push(move(q, acr, off - w * (.7 + .3 * Math.sin(i * 2.3 + seed))));
  }
  band(`${key} smoke`, a, b, Smoke.withAlpha(.5 * fade * (1 - .35 * thin)), Y + .05);
  const core = 1 - clamp((age - flight) / .1);
  if (core > 0) {
    const head = move(from, d, dist * reach);
    line(`${key} halo`, [from, head], .12, White.withAlpha(.3 * core), whiteGlow, Y + .0505, 'none');
    line(`${key} core`, [from, head], .03, White.withAlpha(.95 * core), whiteGlow, Y + .051, 'none');
    if (reach < 1) sprite(head, .12, .1, White.withAlpha(.95), glow, Y + .052);
  }
  for (let i = 0; i < 14; i++) {
    const a0 = age - rand(seed * 37 + i) * .12, life = .14 + .16 * rand(seed * 41 + i); if (a0 < 0 || a0 > life) continue;
    const along = dist * (.05 + .85 * rand(seed * 43 + i)) + a0 * (8 + 4 * rand(seed * 47 + i)), len = .2 + .4 * rand(seed * 59 + i);
    if (along > dist + .6) continue;
    const p0 = move(move(from, d, along), acr, (rand(seed * 53 + i) - .5) * .5);
    streak(`${key} dash ${i}`, p0, move(p0, d, len), .035, White.withAlpha(.95 * (1 - a0 / life)), whiteGlow, Y + .052, 3);
  }
  for (let i = 0; i < 8; i++) {
    const a0 = age - .05 - rand(seed * 61 + i) * .1; if (a0 < 0 || a0 > .6) continue;
    const q = move(move(from, d, dist * rand(seed * 67 + i)), acr, (rand(seed * 71 + i) - .5) * .6);
    sprite({ x: q.x, z: q.z + .15 * a0 }, .05, .05, White.withAlpha(.9 * (1 - a0 / .6)), glow, Y + .053);
  }
}
// The black shot's line from the muzzle to the target: drawn out at Speed, then faded over TrailLife.
function blackTrail(key, from, to, age) {
  const flight = Math.hypot(to.x - from.x, to.z - from.z) / Speed, u = clamp(age / flight), fade = 1 - clamp((age - flight) / TrailLife);
  if (age < 0 || fade <= 0) return;
  const head = { x: lerp(from.x, to.x, u), z: lerp(from.z, to.z, u) }, w = .5 + .5 * fade;
  line(`${key} halo`, [from, head], .15 * w, Soot.withAlpha(.3 * fade), flat, Y + .05, 'none');
  line(`${key} core`, [from, head], .04 * w, Ink.withAlpha(.85 * fade), flat, Y + .051, 'none');
  if (u < 1) sprite(head, .12, .1, Ink.withAlpha(.95), soft, Y + .052);
}
// White hit, as the Limbus frames draw it (Skill 1 2.95-3.0 s, Skill 2 3.5-3.56 s): the black hit's
// shape in white. A solid white splat over the chest (the ink's shapes, picked and turned per shot),
// out to .38 cells (spikes to .75) in .04 s, a white bloom round it, eight thin grey cracks from its
// middle; from .05 s it shrinks away, opaque, gone by .25 s, and about fifteen see-through grey-white
// shards of uneven width and length (a third of them pointed, some gaps), narrow at the middle and
// wide at the tip, fly out from it to about 1.2 cells and thin out by .35 s; six white sparks. No blood: the white shot does no body damage. It is drawn after the
// black hit's splat so a white hit on fresh ink still shows.
const WhiteHit = .35;
function whiteHit(key, c, age, seed) {
  if (age < 0 || age > WhiteHit) return;
  const grow = 1 - Math.pow(1 - clamp(age / .04), 3), R = .38 * grow * (1 - Math.pow(clamp((age - .05) / .2), 1.5)), turn = rand(seed + 900) * 360;
  if (age < .2) sprite(c, 1.3 * grow, 1.2 * grow, White.withAlpha(.6 * (1 - age / .2)), glow, Y + .093);
  for (let i = 0; i < 18; i++) {
    const u = clamp((age - .03) / (WhiteHit - .03)); if (age < .03 || u >= 1 || rand(seed * 11 + i + 905) > .82) continue;
    const t = i / 18 * TAU + (rand(seed * 13 + i + 910) - .5) * .5, r0 = .12 + .9 * Math.sqrt(u) * (.6 + .4 * rand(seed * 17 + i + 920));
    const len = (.2 + .5 * rand(seed * 19 + i + 930)) * (.6 + .4 * u), wl = .03 + .14 * rand(seed * 23 + i + 940), wr = .03 + .14 * rand(seed * 29 + i + 950);
    const dx = Math.cos(t), dz = Math.sin(t), nx = -dz, nz = dx, pointed = rand(seed * 41 + i + 955) > .65;
    const p = (r, w) => ({ x: c.x + dx * r + nx * w, z: c.z + dz * r + nz * w }), tip = r0 + len * (pointed ? 1.25 : 1);
    band(`${key} shard ${i}`, [p(r0, .02), pointed ? p(tip, 0) : p(r0 + len, wl)], [p(r0, -.02), pointed ? p(tip, 0) : p(r0 + len * (.8 + .3 * rand(seed * 43 + i + 958)), -wr)], Pale.withAlpha(.6 * Math.pow(1 - u, 1.2)), Y + .0935);
  }
  if (R > .01) {
    draw(Splats[(seed + 1) % 3], c.x, Y + .094, c.z, R, R, turn, White);
    for (let i = 0; i < 8; i++) {
      const t = rand(seed * 31 + i + 960) * TAU, l = R * (.4 + .4 * rand(seed * 37 + i + 970));
      streak(`${key} crack ${i}`, c, { x: c.x + Math.cos(t) * l, z: c.z + Math.sin(t) * l }, .02, Ash.withAlpha(.7), flat, Y + .0945, 3);
    }
  }
  for (let i = 0; i < 6; i++) {
    const t = rand(i + 120 + seed) * TAU, v = 1.5 + 2 * rand(i + 130 + seed), f = 1 - age / WhiteHit;
    sprite({ x: c.x + Math.cos(t) * v * age, z: c.z + Math.sin(t) * v * age }, .06, .06, White.withAlpha(f), glow, Y + .0948);
  }
}
// A black ink splat, unit radius: 64 points round the centre, a ragged edge of small teeth and,
// about one point in five, a long sharp spike, so it reads as a splash and not a disc. Three
// variants built once; each shot picks one and turns it.
function splatMesh(name, seed) {
  const N = 64, v = [0, 0], tri = [];
  for (let i = 0; i < N; i++) {
    const t = i / N * TAU;
    let r = (.8 + .2 * rand(seed * 71 + i * 3)) * (i % 2 ? .88 : 1);
    if (rand(seed * 131 + i) > .8) r += .35 + .55 * rand(seed * 53 + i);
    v.push(Math.cos(t) * r, Math.sin(t) * r);
  }
  for (let i = 0; i < N; i++) tri.push(0, 1 + i, 1 + (i + 1) % N);
  const m = new Mesh(name); m.setFlat(v, tri); return m;
}
const Splats = [1, 2, 3].map(k => splatMesh(`sl splat ${k}`, k));
// A jagged ink sliver round q: four corners, long and pointed along rot (radians), narrow across.
const SliverShape = [1.6, .45, .8, .4];
function chunk(key, q, rot, size, seed, colour, layer) {
  const pts = [0, 1, 2, 3].map(k => {
    const t = rot + k * Math.PI / 2 + (rand(seed * 5 + k) - .5) * .5, r = size * SliverShape[k] * (.75 + .5 * rand(seed * 7 + k));
    return { x: q.x + Math.cos(t) * r, z: q.z + Math.sin(t) * r };
  });
  band(key, [pts[0], pts[1]], [pts[3], pts[2]], colour, layer);
}
// Black hit, as the Limbus frames draw it: a solid black ink splat over the chest, out to .36
// cells (spikes to .7) in .06 s, a shade lighter in the middle; red inside it (a glow and seven
// thin streaks) for .16 s; three thin black slashes along the shot through the target for .15 s;
// ten droplets flung out. From .1 s the ink breaks up: the splat shrinks away (it stays opaque,
// it does not fade) and fourteen slivers come off its edge, fly 1.2-3.4 cells/s, turn and shrink,
// all gone by .4 s. Then blood thrown on along the shot, and a floor spatter that stays.
const InkHit = .4;
function blackHit(key, c, pos, dir, age, seed) {
  if (age < 0) return;
  const aim = Math.atan2(dir.z, dir.x);
  if (age < InkHit) {
    const grow = 1 - Math.pow(1 - clamp(age / .06), 3), brk = clamp((age - .1) / (InkHit - .1));
    const R = .36 * grow * (1 - Math.pow(brk, 1.5));
    if (R > .01) {
      draw(Splats[seed % 3], c.x, Y + .09, c.z, R, R, rand(seed + 300) * 360, Ink.withAlpha(.95));
      sprite(c, R * 1.1, R, Soot.withAlpha(.4), soft, Y + .0902);
    }
    if (age < .16) {
      const f = 1 - age / .16;
      sprite(c, .3 * grow, .28 * grow, Red.withAlpha(.6 * f), glow, Y + .0905);
      for (let i = 0; i < 7; i++) {
        const t = rand(seed * 7 + i + 310) * TAU, l = (.1 + .2 * rand(seed * 7 + i + 320)) * grow;
        streak(`${key} red ${i}`, c, { x: c.x + Math.cos(t) * l, z: c.z + Math.sin(t) * l }, .035, Red.withAlpha(.95 * f), flat, Y + .0906, 3);
      }
    }
    for (let i = 0; i < 14; i++) {
      const a = age - .08 - rand(seed * 11 + i + 330) * .06; if (a < 0) continue;
      const u = a / (InkHit - .08); if (u >= 1) continue;
      const t = i / 14 * TAU + rand(seed * 13 + i + 340) * .4, v = 1.2 + 2.2 * rand(seed * 17 + i + 350), d0 = .28 + v * a;
      const q = { x: c.x + Math.cos(t) * d0, z: c.z + Math.sin(t) * d0 * .9 - 1.5 * a * a };
      chunk(`${key} chunk ${i}`, q, t + a * 3 * (rand(seed + i + 370) - .5), (.06 + .08 * rand(seed * 19 + i + 360)) * (1 - u), seed * 23 + i, Ink.withAlpha(.95), Y + .091);
    }
    for (let i = 0; i < 10; i++) {
      const u = age / (.3 + .1 * rand(seed + i + 380)); if (u >= 1) continue;
      const t = rand(seed * 29 + i + 390) * TAU, v = 2.5 + 3 * rand(seed * 31 + i + 400), r = .035 * (1 - u * u);
      draw(disc, c.x + Math.cos(t) * v * age, Y + .0915, c.z + Math.sin(t) * v * age * .9 - 3 * age * age, r, r, 0, Ink);
    }
    if (age < .15) {
      const f = 1 - age / .15;
      for (let i = 0; i < 3; i++) {
        const off = (rand(seed * 37 + i + 410) - .5) * .5, a0 = -.5 + age * 6 + rand(i + 420) * .3, len = .6 + .5 * rand(i + 430);
        const from = { x: c.x + dir.x * a0 - dir.z * off, z: c.z + dir.z * a0 + dir.x * off };
        streak(`${key} slash ${i}`, from, move(from, dir, len), .03, Ink.withAlpha(.9 * f), flat, Y + .0918, 4);
      }
    }
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
// The opening, as Skill 3 draws it (8.8-9.2 s): a white flash, white streaks shooting up, white
// smoke puffs, and the butterflies bursting out through them. age is from Open.
//   flash: a white glow 3.2 cells wide on the coffin's face, gone in .35 s, a bright core, twelve
//     white rays out to 1-2 cells for .2 s, and a white glow on the floor round the foot for .6 s;
//   streaks: from .08 s, twelve white beams 1.8 cells across (wider than the coffin, whose lit
//     inside would hide them) shoot up out of the floor and past its top, each .8-1.6 cells long,
//     rising 2.6 cells in .7 s, a white core .05 wide in a
//     tall soft glow .24 wide;
//   smoke: fourteen pale puffs pushed out from the coffin 0.6-1.6 cells over .8 s, growing .35 to
//     1.0, thinning; four of them roll along the floor from the foot.
function coffinOpening(key, base, mouth, age) {
  if (age < 0 || age > .9) return;
  const face = { x: base.x, z: base.z + CoffinH * .5 * Lift };
  if (age < .35) {
    const u = age / .35, g = Math.pow(1 - u, 1.5);
    sprite(face, 3.2 * (.6 + .4 * u), 3.0 * (.6 + .4 * u), White.withAlpha(.85 * g), glow, Y + .075);
    sprite(face, 1.2, 1.4, White.withAlpha(g), glow, Y + .076);
  }
  if (age < .2) {
    const u = age / .2;
    for (let i = 0; i < 12; i++) {
      const t = i / 12 * TAU + rand(i + 540) * .3, l = (1 + rand(i + 550)) * (.4 + .6 * Math.sqrt(u));
      streak(`${key} ray ${i}`, face, { x: face.x + Math.cos(t) * l, z: face.z + Math.sin(t) * l }, .06, White.withAlpha(.9 * (1 - u)), whiteGlow, Y + .077, 4);
    }
  }
  if (age < .6) sprite(base, 3.0, 3.0, White.withAlpha(.45 * (1 - age / .6)), glow, Floor + .025);
  for (let i = 0; i < 12; i++) {
    const a = age - .08 - rand(i + 560) * .12; if (a < 0 || a > .7) continue;
    const u = a / .7, x = base.x + (i / 11 - .5) * 1.8 + (rand(i + 570) - .5) * .08, len = .8 + .8 * rand(i + 580);
    const bottom = base.z + (-.1 + 2.6 * Math.pow(u, .7)) * Lift, top = bottom + len * Lift * Math.min(1, u * 5), f = 1 - u * u;
    const mid = { x, z: (bottom + top) / 2 };
    sprite(mid, .24, (top - bottom) * 1.3 + .1, White.withAlpha(.45 * f), glow, Y + .078);
    line(`${key} beam ${i}`, [{ x, z: bottom }, { x, z: top }], .05, White.withAlpha(f), flat, Y + .079, 'both');
  }
  for (let i = 0; i < 14; i++) {
    const a = age - rand(i + 590) * .08; if (a < 0 || a > .8) continue;
    const u = a / .8, e = 1 - Math.pow(1 - u, 3), low = i < 4;
    const t = low ? (i < 2 ? Math.PI : 0) + (rand(i + 600) - .5) * .8 : rand(i + 610) * TAU, r = (.6 + rand(i + 620)) * e;
    const from = low ? base : mouth.screen, sz = .35 + .65 * e;
    sprite({ x: from.x + Math.cos(t) * r, z: from.z + Math.sin(t) * r * (low ? .3 : .8) + (low ? 0 : .2 * e) }, sz, sz * .85, Pale.withAlpha(.75 * Math.pow(1 - u, 1.2)), puff, Y + .07 + i * .0005);
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
// walk: cells the wielder walks (0 in Overclock); walkTime: its eased walk, so the speed peaks at
// WalkSpeed; home: the flight back into the coffin from where the wielder stopped when the cloud ended.
function coffinPlan(p) {
  const walk = isOverclock(p) ? 0 : Math.max(0, p.dist - Beside), walkTime = 1.5 * walk / WalkSpeed, d = dirOf(p.aim), tEnd = Open + cloudOf(p);
  const walked = walk > 0 ? walk * smooth(clamp((tEnd + .1 - WalkFrom) / walkTime)) : 0;
  const home = Math.max(ReturnTime, Math.hypot(d.x * walked - CoffinBack.x, d.z * walked - CoffinBack.z) / FarSpeed);
  const closeAt = tEnd + .1 + home, sinkAt = closeAt + LidClose;
  const ticks = [];
  for (let k = 1; k <= Math.floor(cloudOf(p) + 1e-6); k++) ticks.push(Open + k * CoffinTick);
  return { tEnd, closeAt, sinkAt, ticks, walk, walkTime, home, end: sinkAt + Sink + p.hold };
}
// Where the wielder is at s: walking from WalkFrom, stopped where it got to when the cloud ends.
const wielderAt = (C, o, d, s) => C.walk <= 0 ? o : move(o, d, C.walk * smooth(clamp((Math.min(s, C.tEnd + .1) - WalkFrom) / C.walkTime)));
const isBurst = p => p.mode === 'burst';
const isOverclock = p => p.mode.startsWith('overclock');
const isFuneral = p => p.mode === 'corroded: the funeral';
const cloudOf = p => isFuneral(p) ? FuneralCloud : p.cloud;
const deathOf = p => Math.max(p.death, p.cap + 1);

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
  if (who.actors !== false) pawn(stand, { ...who, shirt: Holder });
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
    const g = guns[sh.white ? 0 : 1], m0 = muzzleAt(g.hand, g.gd);
    if (sh.white) {
      whiteMuzzle(`sl muzzle ${sh.k}`, m0, g.gd, g.hand, -1, age, sh.k);
      whiteTrail(`sl trail ${sh.k}`, m0, chest, age, sh.k);
      whiteHit(`sl hit ${sh.k}`, chest, s - sh.hit, sh.k);
    } else {
      blackMuzzle(`sl muzzle ${sh.k}`, m0, g.gd, age, sh.k);
      blackTrail(`sl trail ${sh.k}`, m0, chest, age);
      blackHit(`sl hit ${sh.k}`, chest, tpos, d, s - sh.hit, sh.k);
    }
  }
  if (B.downAt != null) {              // the white flash as the swarm covers the body
    const a = s - B.downAt - SwarmIn * .8;
    if (a >= 0 && a < .25) sprite(chest, 1.1, 1.2, White.withAlpha(.45 * (1 - a / .25)), glow, Y + .13);
  }
}

// The coffin's scene: the plan, the walk, the people and every dive, worked out from the clip's start
// (the drawing and the timeline both read it).
function coffinScene(p, o) {
  const C = coffinPlan(p), overclock = isOverclock(p), aimR = p.aim, d = dirOf(aimR), acr = side(d), death = deathOf(p);
  const W = t => wielderAt(C, o, d, t);
  const polar = (deg, r) => move(o, dirOf(aimR + deg), r);
  // Corroded: the ally is the nearest pawn, "Target distance" off along the aim, and the walk ends
  // beside it; a raider further from the start (so not the nearest) stands 2.6 cells from where the
  // walk ends, so both are outside the ring at the start (for any distance) and inside once it arrives.
  // Overclock holds: an ally inside the ring (skipped), a raider inside and one outside.
  const people = (overclock ? [
    { pos: polar(150, 1.8), colour: Ally, ally: true },
    { pos: polar(0, 2.4), colour: Enemy },
    { pos: polar(-55, 4.2), colour: Enemy },
  ] : [
    { pos: polar(0, p.dist), colour: Ally, ally: true },
    { pos: move(move(o, d, C.walk), dirOf(aimR - 70), 2.6), colour: Enemy },
  ]).map((q, i) => ({ ...q, M: { pos: q.pos, colour: q.colour, cap: p.cap, seed: i * 2.1, events: [], flinches: [], downAt: null, fallTurn: q.pos.x >= o.x ? 90 : -90,
    stacks: 0, deadAt: null, turnAt: null, pale: [] } }));
  // A slot that dove gets a new butterfly .25 s later, flying from the coffin to the cloud round the
  // wielder: .7 s, or at FarSpeed when the wielder was further off at the dive (the game knows only
  // where the wielder is, not where it will be).
  const base = { x: o.x + CoffinBack.x, z: o.z + CoffinBack.z };
  const refill = T => { const w = W(T); return Math.max(.7, Math.hypot(w.x - base.x, w.z - base.z) / FarSpeed); };
  // Every tick one butterfly leaves the cloud for each pawn inside the ring round the wielder at that
  // moment: the first slot from (n x 11 + 3) mod 36 whose butterfly is circling (out of the coffin,
  // its refill landed .25 s ago). The coffin sends that slot a new one .25 s later.
  // Up to the cap each stack lands on the next free slot of the body; at the cap the swarm covers the
  // rest and the pawn goes down. Corroded, a downed pawn keeps taking stacks (the funeral): stack m past
  // the cap is a dark butterfly landing on a white one of the cover, and with it the m-th tenth (of the
  // stacks left to the death count) of the cover's white butterflies, in a fixed shuffled order, turn
  // dark. At the death count the pawn is dead and takes no more. Overclock stops at the cap.
  const dives = [];
  let n = 0;
  const ready = (i, T) => cloudOut(i) + cloudFly(i) <= T && !dives.some(v => v.i === i && T - v.T < .5 + refill(v.T));
  for (const T of C.ticks) for (const q of people) {
    const M = q.M, c = W(T);
    if (M.deadAt != null || (overclock && (q.ally || M.downAt != null)) || Math.hypot(q.pos.x - c.x, q.pos.z - c.z) > Radius) continue;
    let i = (n++ * 11 + 3) % CloudN;
    for (let k = 0; k < CloudN && !ready(i, T); k++) i = (i + 5) % CloudN;
    const from = orbit(i, T, c), tc = T + DiveTime;
    dives.push({ i, T });
    M.stacks++;
    const dive = { t: tc, launch: T, fly: DiveTime, seed: i, from: { g: from.g, h: from.h }, via: { x: 0, z: 0 }, arc: .2 };
    if (M.stacks <= p.cap) {
      M.events.push({ ...dive, slot: M.stacks - 1, dark: i % 2 === 1 });
      if (M.stacks === p.cap) {
        M.downAt = tc;
        for (let k = p.cap; k < TorsoSlots + HeadSlots; k++) {
          const a = rand(k + 700) * TAU, r = 1.1 + .7 * rand(k + 710), g = { x: M.pos.x + Math.cos(a) * r, z: M.pos.z + Ground + Math.sin(a) * r };
          M.events.push({ t: M.downAt, launch: M.downAt + rand(k + 720) * .15, fly: SwarmIn, slot: k, dark: k % 2 === 1, seed: k, swarm: true, from: { g, h: .4 + rand(k + 730) }, via: { x: -Math.sin(a) * .6, z: Math.cos(a) * .6 }, arc: .15 });
        }
        M.pale = [...new Set(M.events.filter(e => !e.dark).map(e => e.slot))].sort((a, b) => rand(a + 5000) - rand(b + 5000));
        M.turnAt = {};
      }
    } else {
      const m = M.stacks - p.cap, N = death - p.cap, P = M.pale.length, r0 = Math.floor((m - 1) * P / N), r1 = Math.floor(m * P / N);
      for (let r = r0; r < r1; r++) M.turnAt[M.pale[r]] = tc;
      M.events.push({ ...dive, slot: P ? M.pale[Math.min(P - 1, r0)] : M.stacks % (TorsoSlots + HeadSlots), dark: true });
      if (M.stacks >= death) M.deadAt = tc;
    }
  }
  return { C, overclock, d, acr, W, people, base, refill, dives };
}

function drawCoffin(s, p, o, who, sun, strength) {
  const { C, overclock, d, acr, W, people, base, refill, dives } = coffinScene(p, o);

  // The floor: a pale ring at the true radius and a dim floor inside it while the coffin is open,
  // round the wielder wherever it walks.
  const w = W(s);
  const ringA = smooth(clamp((s - Open) / .3)) * (1 - smooth(clamp((s - C.closeAt) / .3)));
  if (ringA > 0) {
    sprite(w, Radius * 2.3, Radius * 2.3, Ink.withAlpha(.16 * ringA), soft, Floor + .02);
    circle(w, Radius * (.9 + .1 * ringA), .75 * ringA, Floor + .03, Pale);
    circle(w, Radius * .985, .3 * ringA, Floor + .03, Pale);
  }
  // The coffin where the wielder stood when it rose.
  const riseU = clamp(s / Rise), sinkU = clamp((s - C.sinkAt) / Sink);
  const rise = smooth(riseU) * (1 - smooth(sinkU));
  const lid = smooth(clamp((s - Rise) / LidOpen)) * (1 - smooth(clamp((s - C.closeAt) / LidClose)));
  const mouth = coffin('sl coffin', base, rise, lid, s, sun, strength);
  footDust('sl rise dust', base, riseU < 1 ? riseU : sinkU);
  coffinOpening('sl opening', base, mouth, s - Open);

  // The pawns: the wielder (guns hanging down), the face if corroded, then everyone else.
  for (const q of people) q.M.mouth = mouth;
  for (const q of people) markedPawn(`sl pawn ${q.colour === Ally ? 'ally' : q.pos.x}`, q.M, s, who, p.size, sun, strength);
  if (who.actors !== false) pawn(w, { ...who, shirt: Holder });
  [true, false].forEach((white, i) => {
    const hand = move(move(w, d, .04), acr, (i ? 1 : -1) * .2);
    pistol(`sl gun ${i}`, hand, d, white, -55 * D2R, 0, pawnLayer + .06 + i * .004, sun, strength);
  });
  if (!overclock) faceButterfly(at(w, 'head', who), s, smooth(clamp(s / .4)) * (1 - smooth(clamp((s - C.sinkAt) / Sink))));

  // The cloud, circling the wielder. On the flash 24 butterflies burst out within .12 s and fly to
  // their orbits in .55 s, bowed out through the smoke; the other 12 follow .05 s apart and take .7 s.
  // A slot sends a new one out of the coffin .25 s after its last dive (refill). A flight out of the
  // coffin aims at its orbit round where the wielder is now, so it bends after a walking wielder and
  // lands where the orbit is. At the end all fly back into the coffin, .7 s or longer from further off
  // (C.home).
  for (let i = 0; i < CloudN; i++) {
    const mine = dives.filter(v => v.i === i && v.T <= s), last = mine.length ? mine[mine.length - 1].T : null;
    const rel = last == null ? cloudOut(i) : last + .25, dur = last == null ? cloudFly(i) : refill(last), back = C.tEnd + .1 + rand(i + 800) * .15;
    if (s < rel || s >= back + C.home || rel >= back) continue;
    let q, heading, size = p.size, alpha = 1;
    if (s >= back) {
      const u = (s - back) / C.home, from = orbit(i, back, W(back)), e = { from, via: { x: 0, z: 0 }, arc: .3, seed: i };
      q = flyAt(e, u, mouth); const q2 = flyAt(e, Math.min(1, u + .03), mouth);
      heading = Math.atan2(q2.screen.z - q.screen.z, q2.screen.x - q.screen.x) / D2R;
      size *= 1 - .5 * u; alpha = 1 - smooth(clamp((u - .7) / .3));
    } else if (s < rel + dur) {
      const u = (s - rel) / dur, to = orbit(i, rel + dur, w), out = norm(to.g.x - mouth.g.x, to.g.z - mouth.g.z), wide = last == null && i < CloudBurst ? 1.1 : .4;
      const e = { from: mouth, via: { x: out[0] * wide + (rand(i + 810) - .5) * .8, z: out[1] * wide + (rand(i + 820) - .5) * .8 }, arc: .6, seed: i };
      q = flyAt(e, u, to); const q2 = flyAt(e, Math.min(1, u + .03), to);
      heading = Math.atan2(q2.screen.z - q.screen.z, q2.screen.x - q.screen.x) / D2R;
      size *= .5 + .5 * clamp(u * 2);
    } else {
      q = orbit(i, s, w); heading = q.heading;
    }
    butterflyShadow(q.g, Math.max(0, q.h), size, sun, strength, alpha);
    butterfly(q.screen, size, heading, flapAt(s, 3, .15, i), i % 2 === 1, alpha, Y + .14 + i * .0008);
  }
}

export default {
  kit: 'E.G.O. weapons', label: 'Solemn Lament (sketch)',
  params: {
    mode: { label: 'Show', value: 'burst', options: ['burst', 'corroded: the coffin', 'corroded: the funeral', 'overclock: the coffin, hostiles only'], group: 'Showcase' },
    aim: P('Aim (degrees)', 0, 0, 360, 5, 'Showcase'),
    dist: P('Target distance (cells); corroded: the nearest pawn', 5, 2, 10, .5, 'Showcase'),
    shots: P('Shots in the burst (ammo 20)', 8, 1, 20, 1, 'Rule'),
    interval: P('Time between shots (s)', .25, .1, 1, .05, 'Rule'),
    whiteStacks: P('White shot: stacks', 2, 0, 5, 1, 'Rule'),
    blackStacks: P('Black shot: stacks', 1, 0, 5, 1, 'Rule'),
    cap: P('Stack cap (down at)', 10, 2, 20, 1, 'Rule'),
    death: P('Funeral: dead at (stacks)', 20, 11, 40, 1, 'Rule'),
    cloud: P('Coffin: seconds shown (rule 30, overclock 5; the funeral 23)', 4, 1, 30, 1, 'Timing (s)'),
    lead: P('Draw and aim', .35, .1, 1, .05, 'Timing (s)'),
    hold: P('Show the result', 1.2, .3, 3, .1, 'Timing (s)'),
    size: P('Butterfly span (cells)', .3, .1, .5, .01, 'Shape'),
    actors: { label: 'Stand-in pawns', value: true, group: 'Showcase' },
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
    if (C.walk > 0) out.push({ name: 'Walks', t: WalkFrom });
    if (C.ticks.length) out.push({ name: 'Stack 1', t: C.ticks[0] });
    const S = coffinScene(p, { x: 0, z: 0 }), first = key => S.people.map(q => q.M[key]).filter(t => t != null).sort((a, b) => a - b)[0];
    if (first('downAt') != null) out.push({ name: `Stack ${p.cap}: down`, t: first('downAt') });
    if (first('deadAt') != null) out.push({ name: `Stack ${deathOf(p)}: dead`, t: first('deadAt') });
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
    const who = { body: 'average', sun, shadow: strength, actors: p.actors };
    if (isBurst(p)) drawBurst(s, p, o, who, sun, strength);
    else drawCoffin(s, p, o, who, sun, strength);
  },
};
