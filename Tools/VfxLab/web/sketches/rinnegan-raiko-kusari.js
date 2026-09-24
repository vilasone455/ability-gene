// Raikō Kusari — technique proposal for the Rinnegan eye kit, not the game. Nothing in Source/RimArt
// draws this yet.
//
// What it is for (agreed 2026-09-23 in outline: pawns are caught, colonists too, Dark Chidori look;
// every number is a placeholder). A Chidori runs through the weapons Amenoyodomi is holding and
// strings them into a net. No target: it needs 2 or more held weapons (kunai, or the Fūma) within
// 12 cells.
//   Links: in the order the weapons were thrown, one per pair thrown one after the other, up to 6
//   cells each. With 3 or more weapons, if the last is within 6 cells of the first, the chain closes
//   into a ring. A link that stretches past 6 cells (weapons drifting apart) snaps; the net ends
//   when its last link is gone.
//   Warmup 0.6 s: the Chidori charges in the caster's hand, then leaps to the first weapon and runs
//   down the chain.
//   Lasts 8 s, or until Amenoyodomi lets the weapons go.
//   Caught: a pawn on a line when it forms, or that walks into one, stands paralysed until the net
//   ends and takes 3 burn damage per second (Chidori Current, manga ch. 384: whoever it touches goes
//   stiff). The caster is immune; colonists are caught too. Mechanoids stay stunned 3 s after it
//   ends (EMP); a shield belt that touches a line breaks. Bullets pass through: it stops bodies, not
//   shots.
//   Letting go: the net snaps off and frees everyone, and the weapons fly on charged. A kunai stuns
//   whoever it hits for 2 s. The Fūma keeps its own throw (an exact line that cuts every pawn on it,
//   range 12) and every pawn it cuts is stunned 2 s (proposed 2026-09-23, on the user's question).
//   Cooldown 30 s. No eye strain: Chidori is not an eye technique.
// Not a wall: "shocked and thrown back" was dropped because Black rods v2 (Six Paths) already stops
// pawns and lets bullets through. Pairs with Amenotejikara (move a corner, or swap an enemy onto a
// line) and Amaterasu (burning kunai can be corners).
//
// Look. Neither "Raikō Kusari" nor "Amenoyodomi" is a canon name, so the picture is Sasuke's
// Chidori, in the Six Paths "Dark Chidori" colours (the Storm 4 name; the anime frame shows black
// jagged lines crawling over his arm and chest round a white core). From the Chidori Current frame
// (Narutopedia): bolts with a few big kinks and fine crackle on top, a core that swells and
// pinches, white flash frames, the light thrown on everything round it, current running through
// whoever touches it. Colour is a dropdown: Dark Chidori (default, the user's pick), Chidori
// blue-white, Rinnegan violet.
//
// Showcase, default timings:
//   0.00-0.60  charge: a white core beats in the caster's hand, bolts crackle out of it and up the arm
//   0.60       the Chidori leaps to the first weapon (0.05 s, a white flash) and runs down the chain,
//              0.05 s per link; each weapon flashes as it is reached, a ring closes with a larger
//              flash. When the last link closes the whole net flashes white for 0.06 s. Camera
//              shake 0.03.
//   net        each line: a soft halo; 2 bolts, each with 2-4 big kinks and fine crackle, its width
//              swelling and pinching from 0.5 to 1.6 times, a side branch, all redrawn 12 times a
//              second; bolt 0 carries a white thread. Every 0.4-0.7 s (fixed per line) the line
//              flashes for 0.07 s: bolts 1.8 times as wide, the thread wide and white, a third bolt,
//              the halo brighter. Two white-hot pulses run along each line at 7 cells/s, one each
//              way. Under it the floor carries a dark trace (Dark Chidori) or a flickering coloured
//              light, and white light during each flash; the same pools sit under each weapon. 3
//              sparks a second drop from each line to the floor (0.43 s fall) and flash as they land.
//              The weapons hang 0.55 cells up with Amenoyodomi's rings, dots and afterimages.
//   caught     a burst where it touched; the pawn freezes mid-step and shakes 0.02 cells on every
//              redraw, the line bends to run through its chest, bolts crawl over its body, it
//              flashes white about 3 times a second, and a scorch grows under its feet and stays
//   end        the lines break: they vanish in 0.12 s and fall as sparks, 3 per cell of length; a
//              let-go or a snap adds sparks at both ends
// Scenarios:
//   fence         3 kunai across the approach, 6.4 cells end to end, so no ring. A raider, a raider
//                 with a shield belt and a mechanoid run in and are caught; the shield pops. A raider
//                 behind the net shoots at the caster every 0.55 s from 1.0 s: the tracers go
//                 through the lines untouched and wide of the caster. At the end the raiders run on
//                 and the mech stays stunned 3 s more (blue EMP crackle).
//   ring          4 kunai round a group, 3.8 cells apart: the ring closes. A colonist standing on a
//                 line when it forms is caught, a raider walking out is caught at the line, and a
//                 raider who stays inside is not.
//   drifting net  Amenoyodomi at 10 %: the kunai go on at 2.4 cells/s and the lines sweep over three
//                 standing raiders. The kunai fan apart, the links pass 6 cells and snap, and the
//                 net ends.
//   let go        a raider is caught; Amenoyodomi lets go: the net snaps off, the raider runs on, and
//                 the kunai fly on at 24 cells/s: two hit raiders further back (stuck, stunned 2 s),
//                 the third flies on out of view.
//   Fūma corner   kunai, Fūma, kunai. Lines join the Fūma at the blade tip nearest the next weapon,
//                 and arcs run round its rim from blade tip to blade tip as it turns. A raider is
//                 caught on its line. Let go: the Fūma flies its line at 14.4 cells/s at full spin
//                 inside a spinning ring of lightning, cuts three raiders (each stunned 2 s) and flies
//                 out of view, to land 12 cells past where it hung.
//
// Drawing: every line joins two weapons at the same height, so the net is a flat shape at one
// height and turns freely with the direction: no per-facing method. The Fūma's rim arcs and flying
// ring are level circles. Bolts are strips with a width per point, rebuilt on every redraw from a
// hash of the redraw step; the halo and the floor light are soft-disc sprites stretched along the
// line, so the light has no hard edge. Sparks are computed from their birth time (fall under 6
// cells/s² from 0.55 cells), so scrubbing is deterministic. Who is caught, and when, is replayed
// from the cast in 1/60 s steps each frame. Here a pawn touches a line within 0.45 cells of its
// ground track; the port should use the cells the line crosses ("Show the cells that catch" draws
// them). The held weapons are lib/amenoyodomi.js's, the rule of the Amenoyodomi sketch: each was
// thrown at its cell before the clip and hangs there from 0 s, going on along its heading at the
// hold share (1 %: a kunai 0.24 cells/s, the Fūma 0.144; 10 % in the drifting net), and a let-go
// sends it on at full speed up to its range from where it hung (kunai 14.9, Fūma 12). The lib
// draws the weapons, their marks and afterimages, their flight and where they lie; this file adds
// the charge: sparks on the corners, the Fūma's arcs, the lightning trail and ring in flight.
import { AltitudeLayer, Color, Mathf, Meshes, MeshPool } from '../js/engine.js';
import { draw, Lift } from './lib/six-paths-solid.js';
import { P, Y, Floor, at, sprite, circle, glow, soft, rand } from './lib/six-paths-impact.js';
import { figure, strip, whiteGlow, stuckKunai, CasterColour, EnemyColour } from './lib/flying-thunder-god.js';
import { line } from './lib/goku.js';
import { placed, motion, ground, heldU, turnOf, drawWeapon, Hold, Hang, Drift, FumaSize } from './lib/amenoyodomi.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const pawnLayer = AltitudeLayer.Pawn.AltitudeFor(), shadowLayer = AltitudeLayer.Shadows.AltitudeFor();
const flatDisc = Meshes.disc(40, 'raiko disc');

// Decided looks. Dark Chidori is black bolts round a white thread; the other two are light.
const White = new Color(1, 1, 1), Ink = new Color(.02, .018, .03), ShadowInk = new Color(.03, .03, .05);
const Ally = new Color(.45, .62, .40);
const ShieldBlue = new Color(.45, .72, 1), EmpBlue = new Color(.55, .82, 1);
const MechGrey = new Color(.46, .48, .52), MechDark = new Color(.24, .25, .28), MechEye = new Color(.95, .22, .15);
const Tracer = new Color(1, .9, .6), Gun = new Color(.2, .2, .22);
const Palettes = {
  'Dark Chidori': { dark: true, bolt: Ink, halo: new Color(.72, .68, .88), haloA: .14, floor: Ink, floorA: .24 },
  'Chidori blue-white': { dark: false, bolt: new Color(.82, .93, 1), halo: new Color(.22, .5, 1), haloA: .32, floor: new Color(.22, .5, 1), floorA: .1 },
  'Rinnegan violet': { dark: false, bolt: new Color(.9, .82, 1), halo: new Color(.52, .3, .96), haloA: .32, floor: new Color(.52, .3, .96), floorA: .1 },
};
const EmpLook = { dark: false, bolt: new Color(.82, .94, 1), halo: EmpBlue, haloA: .3 };
const PaletteNames = Object.keys(Palettes);
const Scenarios = ['fence', 'ring', 'drifting net', 'let go', 'Fūma corner'];

// The weapons' numbers and the hold are lib/amenoyodomi.js's. TipR: a Fūma blade tip, in texture widths.
const TipR = .55;
// Timing and the rule.
const Leap = .05, PerLink = .05, FadeOut = .3, CutOut = .12, CornerFlash = .15, CloseFlash = .06, Touch = .45, Step = 1 / 60;
const MechExtra = 3, HitStun = 2, HitReach = .4, CutReach = .55;
const RunSpeed = 3, WalkSpeed = 1.5, MechSpeed = 2.2, ShakeSize = .03;
// The look in motion.
const SurgeTime = .07, PulseSpeed = 7, SparkRate = 3, BreakPerCell = 3, Gravity = 6, ShakeHeld = .02, ShakeHit = .012;
const TracerSpeed = 40, FirstShot = 1, ShotGap = .55;

// The scene, laid out along the aim d from the caster (n is d's left); o is the middle of the net.
// Weapons are listed in the order they were thrown.
function scene(p, o) {
  const a = p.aim * Mathf.Deg2Rad, d = { x: Math.cos(a), z: Math.sin(a) }, n = { x: -d.z, z: d.x };
  const place = (u, v) => ({ x: o.x + d.x * u + n.x * v, z: o.z + d.z * u + n.z * v });
  const caster = place(-p.distance, 0), back = { x: -d.x, z: -d.z };
  // A weapon thrown at the cell u, v before the clip, held there from 0 s at its hold share.
  let thrown = 0;
  const weapon = (kind, u, v, share = Hang) => placed(kind, caster, place(u, v), 0, { shares: [[0, share]], seed: thrown++ });
  // A pawn: what it is, where it starts, which way it moves, how fast, from when, and how far at most.
  const pawn = (kind, u, v, dir = d, speed = 0, t0 = 0, reach = 0) => ({ kind, start: place(u, v), dir, speed, t0, reach });
  const runIn = (kind, u, v, t0, speed = RunSpeed) => pawn(kind, u, v, back, speed, t0, u + 2.6);
  const behind = (w, far) => ({ kind: 'raider', start: { x: w.cell.x + w.dir.x * far, z: w.cell.z + w.dir.z * far }, dir: d, speed: 0, t0: 0, reach: 0 });
  let W, pawns, letsGo = false;
  switch (p.scenario) {
    case 'ring':
      W = [weapon('kunai', 1.9, 1.9), weapon('kunai', 1.9, -1.9), weapon('kunai', -1.9, -1.9), weapon('kunai', -1.9, 1.9)];
      pawns = [pawn('ally', -1.9, .5), pawn('raider', .4, .8, d, WalkSpeed, 1.2, 3), pawn('raider', .1, -.7)];
      break;
    case 'drifting net':
      W = [weapon('kunai', 0, 3.1, Drift), weapon('kunai', .3, 0, Drift), weapon('kunai', 0, -3.1, Drift)];
      pawns = [pawn('raider', 2, 1), pawn('raider', 2.6, -1.4), pawn('raider', 3.3, .4)];
      break;
    case 'let go':
      W = [weapon('kunai', .2, 3.1), weapon('kunai', .5, 0), weapon('kunai', .2, -3.1)];
      pawns = [runIn('raider', 4, 1.2, 0), behind(W[0], 2.2), behind(W[2], 2.2)];
      letsGo = true;
      break;
    case 'Fūma corner':
      W = [weapon('kunai', .2, 3.1), weapon('fuma', .6, 0), weapon('kunai', .2, -3.1)];
      pawns = [runIn('raider', 3.6, 1.5, .3), pawn('raider', 2.2, .15), pawn('raider', 3.4, -.1), pawn('raider', 4.6, .2)];
      letsGo = true;
      break;
    default: // fence
      W = [weapon('kunai', .2, 3.2), weapon('kunai', .5, 0), weapon('kunai', .2, -3.2)];
      pawns = [runIn('raider', 4, 1.6, 0), runIn('shield', 5, -1, .2), runIn('mech', 5.6, -2.3, .3, MechSpeed), pawn('shooter', 4.6, .4)];
  }
  return { d, n, caster, W, pawns, letsGo };
}

// Where a weapon is on the ground at time s (lib/amenoyodomi.js: held at its share from 0 s, then after
// a let-go at full speed along its heading until w.stop), and how far the Fūma has turned.
const along = (w, s) => motion(w, Math.max(0, s))?.u ?? w.dist;
const weaponAt = (w, s) => ground(w, along(w, s));
const turnAt = (w, s) => turnOf(w, along(w, s));
// Blade k of the Fūma at r texture widths from its middle, turned clockwise by turn degrees, as a
// ground offset. Measured off RimArt/Fuma/Unfolded (see the Amaterasu sketch): 22 + 40 r + 90 k.
const bladeAngle = (turn, k, r) => (22 + 40 * r + 90 * k - turn) * Mathf.Deg2Rad;
function blade(c, turn, k, r) {
  const a = bladeAngle(turn, k, r);
  return { x: c.x + Math.cos(a) * r * FumaSize, z: c.z + Math.sin(a) * r * FumaSize };
}
// Where a line meets weapon i at time s, on the ground: a kunai's middle, or the Fūma's blade tip
// nearest weapon j.
function joint(S, T, i, j, s) {
  const w = S.W[i], c = weaponAt(w, s, T);
  if (w.kind !== 'fuma') return c;
  const other = weaponAt(S.W[j], s, T), turn = turnAt(w, s, T);
  let best = c, far = Infinity;
  for (let k = 0; k < 4; k++) {
    const q = blade(c, turn, k, TipR), dd = Math.hypot(q.x - other.x, q.z - other.z);
    if (dd < far) { far = dd; best = q; }
  }
  return best;
}
function linkLength(S, T, L, s) {
  const a = joint(S, T, L.a, L.b, s), b = joint(S, T, L.b, L.a, s);
  return Math.hypot(b.x - a.x, b.z - a.z);
}
function segDist(q, a, b) {
  const dx = b.x - a.x, dz = b.z - a.z, l2 = dx * dx + dz * dz;
  const u = l2 > 0 ? clamp(((q.x - a.x) * dx + (q.z - a.z) * dz) / l2) : 0;
  return Math.hypot(q.x - a.x - dx * u, q.z - a.z - dz * u);
}

// When things happen. The run lights the links one after another; a link too long when the run
// reaches it never forms; a link that grows past the limit later snaps. The net ends at the let-go,
// at the showcase cut, or when its last link has snapped.
function timeline(S, p) {
  const n = S.W.length, leap = p.charge, reach0 = leap + Leap, T = { leap, reach0, letGo: Infinity };
  const links = [];
  for (let i = 0; i + 1 < n; i++) links.push({ a: i, b: i + 1, start: reach0 + i * PerLink });
  const closeAt = reach0 + (n - 1) * PerLink;
  if (n >= 3 && linkLength(S, T, { a: n - 1, b: 0 }, closeAt) <= p.maxLink) links.push({ a: n - 1, b: 0, start: closeAt, closing: true });
  links.forEach(L => { L.lit = L.start + PerLink; L.fails = linkLength(S, T, L, L.start) > p.maxLink; L.snap = Infinity; });
  const formed = reach0 + links.length * PerLink, planned = S.letsGo ? formed + p.letGo : leap + p.lasts;
  links.forEach(L => {
    if (L.fails) return;
    for (let t = L.lit; t < planned; t += Step) if (linkLength(S, T, L, t) > p.maxLink) { L.snap = t; break; }
  });
  const live = links.filter(L => !L.fails);
  const netEnd = live.length ? Math.min(planned, Math.max(...live.map(L => L.snap))) : reach0;
  return Object.assign(T, { links, formed, netEnd, letGo: S.letsGo ? planned : Infinity });
}

// After a let-go: a kunai flies on until the first standing pawn on its path or the end of its
// range; the Fūma flies to the end of its range and cuts every standing pawn on its line.
function flights(S, T) {
  S.W.forEach(w => { w.stopT = Infinity; w.hit = null; w.cuts = []; w.stop = null; w.letGo = T.letGo; });
  if (!isFinite(T.letGo)) return;
  S.W.forEach(w => {
    const uL = heldU(w, T.letGo), q = ground(w, uL), range = w.range;
    const ahead = pos => {
      const dx = pos.x - q.x, dz = pos.z - q.z;
      return { u: dx * w.dir.x + dz * w.dir.z, off: Math.abs(dx * w.dir.z - dz * w.dir.x) };
    };
    if (w.kind === 'fuma') {
      S.pawns.forEach((P, k) => {
        if (P.speed) return;
        const { u, off } = ahead(P.start);
        if (u > 0 && u < range && off < CutReach) w.cuts.push({ pawn: k, t: T.letGo + u / w.speed });
      });
      w.stopT = T.letGo + range / w.speed;
      w.stop = { t: w.stopT, u: uL + range, how: 'range' };
      return;
    }
    let best = null;
    S.pawns.forEach((P, k) => {
      if (P.speed) return;
      const { u, off } = ahead(P.start);
      if (u > 0 && u < range && off < HitReach && (!best || u < best.u)) best = { pawn: k, u };
    });
    w.hit = best && { pawn: best.pawn, t: T.letGo + best.u / w.speed };
    w.stopT = best ? w.hit.t : T.letGo + range / w.speed;
    w.stop = { t: w.stopT, u: uL + (best ? best.u : range), how: best ? 'hit' : 'range' };
  });
}

// A pawn's position if nothing held it.
function runAt(P, t) {
  const u = Math.min(P.reach, P.speed * Math.max(0, t - P.t0));
  return { x: P.start.x + P.dir.x * u, z: P.start.z + P.dir.z * u };
}
// With the time it spent held taken out.
function pawnAt(P, s) {
  let lost = 0;
  for (const [a, b] of P.freezes) { if (s <= a) break; lost += Math.min(s, b) - a; }
  return runAt(P, s - lost);
}

function build(p, o) {
  const S = scene(p, o), T = timeline(S, p);
  flights(S, T);
  S.pawns.forEach((P, k) => {
    // The first moment it touches a live line, replayed from the cast, and which line it was.
    P.caught = Infinity; P.link = -1;
    for (let t = T.reach0; t < T.netEnd && !isFinite(P.caught); t += Step) {
      const q = runAt(P, t);
      for (let li = 0; li < T.links.length; li++) {
        const L = T.links[li];
        if (L.fails || t < L.lit || t >= L.snap) continue;
        if (segDist(q, joint(S, T, L.a, L.b, t), joint(S, T, L.b, L.a, t)) <= Touch) { P.caught = t; P.link = li; break; }
      }
    }
    P.hits = [];
    S.W.forEach(w => {
      if (w.hit && w.hit.pawn === k) P.hits.push(w.hit.t);
      w.cuts.forEach(c => { if (c.pawn === k) P.hits.push(c.t); });
    });
    P.freezes = P.hits.map(t => [t, t + HitStun]);
    if (isFinite(P.caught)) P.freezes.push([P.caught, T.netEnd + (P.kind === 'mech' ? MechExtra : 0)]);
    P.freezes.sort((a, b) => a[0] - b[0]);
  });
  let end = T.netEnd + 1.1;
  S.pawns.forEach(P => P.freezes.forEach(([, b]) => { end = Math.max(end, b + .5); }));
  S.W.forEach(w => { if (isFinite(w.stopT)) end = Math.max(end, Math.min(w.stopT, T.letGo + .8) + .5); });
  return Object.assign(S, { T, end });
}

// A jagged bolt from a to b: 2-4 big kinks it meanders through, with fine crackle on top, both zero
// at the ends and smaller on a short bolt; seed picks the shape.
function boltPts(a, b, jag, seed, seg = .15) {
  const dx = b.x - a.x, dz = b.z - a.z, len = Math.hypot(dx, dz) || 1e-6;
  const n = Math.max(4, Math.round(len / seg)), nx = -dz / len, nz = dx / len, j = jag * Math.min(1, len / .8);
  const kinks = 2 + Math.floor(rand(seed + 1) * 3), ku = [0], kv = [0];
  for (let k = 1; k <= kinks; k++) { ku.push((k - .5 + (rand(seed + 10 + k) - .5) * .6) / kinks); kv.push((rand(seed + 20 + k) - .5) * 3 * j); }
  ku.push(1); kv.push(0);
  const pts = [];
  for (let i = 0, k = 1; i <= n; i++) {
    const u = i / n;
    while (k < ku.length - 1 && u > ku[k]) k++;
    const meander = kv[k - 1] + (kv[k] - kv[k - 1]) * clamp((u - ku[k - 1]) / Math.max(1e-6, ku[k] - ku[k - 1]));
    const off = i > 0 && i < n ? meander + (rand(seed + i * 7) - .5) * .7 * j : 0;
    pts.push({ x: a.x + dx * u + nx * off, z: a.z + dz * u + nz * off });
  }
  return pts;
}
// Width along a bolt of n segments: swells and pinches between 0.5 and 1.6 times w through three
// random control values, and thins to half at the ends.
function widths(n, w, seed) {
  const c = [.5, .5 + 1.1 * rand(seed + 41), .5 + 1.1 * rand(seed + 42), .5 + 1.1 * rand(seed + 43), .5];
  return Array.from({ length: n + 1 }, (_, i) => {
    const f = i / n * (c.length - 1), k = Math.min(c.length - 2, Math.floor(f)), e = (1 - Math.cos((f - k) * Math.PI)) / 2;
    return w * (c[k] + (c[k + 1] - c[k]) * e);
  });
}
// A strip through pts with a width per point.
function stroke(key, pts, ws, colour, material, layer) {
  const a = [], b = [], last = pts.length - 1;
  pts.forEach((q, i) => {
    const prev = pts[Math.max(0, i - 1)], next = pts[Math.min(last, i + 1)], dx = next.x - prev.x, dz = next.z - prev.z;
    const len = Math.hypot(dx, dz) || 1, w = ws[i] / 2 + .003;
    a.push({ x: q.x - dz / len * w, z: q.z + dx / len * w }); b.push({ x: q.x + dz / len * w, z: q.z - dx / len * w });
  });
  strip(key, a, b, colour, material, layer);
}
// One bolt with a width per point. Dark Chidori: black, with a white thread (core 0..1; at 1 it
// fills most of the bolt, a white flash frame). Light looks: a coloured halo strip, a pale core and
// the white thread.
function boltStroke(key, pts, ws, alpha, look, layer, core) {
  if (alpha <= .01 || pts.length < 2) return;
  if (look.dark) {
    stroke(key, pts, ws, look.bolt.withAlpha(Math.min(1, alpha)), undefined, layer);
    if (core > 0) stroke(`${key} core`, pts, ws.map(w => w * (.3 + .45 * core)), White.withAlpha(Math.min(1, alpha * core)), whiteGlow, layer + .0002);
    return;
  }
  stroke(key, pts, ws.map(w => w * 2), look.halo.withAlpha(alpha * .45), whiteGlow, layer);
  stroke(`${key} core`, pts, ws.map(w => w * .7), look.bolt.withAlpha(Math.min(1, alpha)), whiteGlow, layer + .0002);
  if (core > 0) stroke(`${key} white`, pts, ws.map(w => w * (.3 + .25 * core)), White.withAlpha(Math.min(1, alpha * core)), whiteGlow, layer + .0003);
}
// A small bolt of even width that tapers: hand crackle, branches, bursts, body crawl.
function boltLine(key, pts, width, alpha, look, layer, taper = 'none', core = 0) {
  if (alpha <= .01 || pts.length < 2) return;
  if (look.dark) {
    line(key, pts, width, look.bolt.withAlpha(Math.min(1, alpha)), undefined, layer, taper);
    if (core > 0) line(`${key} core`, pts, width * .34, White.withAlpha(Math.min(1, alpha * core)), whiteGlow, layer + .0002, taper);
    return;
  }
  line(key, pts, width * 2, look.halo.withAlpha(alpha * .45), whiteGlow, layer, taper);
  line(`${key} core`, pts, width * .7, look.bolt.withAlpha(Math.min(1, alpha)), whiteGlow, layer + .0002, taper);
  if (core > 0) line(`${key} white`, pts, width * .3, White.withAlpha(Math.min(1, alpha * core)), whiteGlow, layer + .0003, taper);
}
// A white flash on one line every 0.4-0.7 s (fixed per line), SurgeTime long, 1 falling to 0.
function surgeOf(seed, s) {
  const period = .4 + .3 * rand(seed + 5), age = (Math.max(0, s) + rand(seed + 6) * period) % period;
  return age < SurgeTime ? 1 - age / SurgeTime : 0;
}
// A lit line through ctrl (screen points: its two ends and any chest it runs through): a soft halo,
// p.bolts bolts (one more during a flash) with a side branch each, redrawn p.boil times a second.
// Bolt 0 carries the white thread. surge 0..1 flares the lot. Returns bolt 0's points.
function lightning(key, ctrl, s, p, look, alpha, seed, surge = 0) {
  if (alpha <= .01) return null;
  const step = Math.floor(s * p.boil);
  ctrl.slice(1).forEach((b, m) => {
    const a = ctrl[m], dx = b.x - a.x, dz = b.z - a.z, len = Math.hypot(dx, dz);
    if (len < .03) return;
    const ang = Math.atan2(dz, dx) * Mathf.Rad2Deg, k = Math.max(1, Math.ceil(len / 1.1));
    for (let i = 0; i < k; i++) {
      const u = (i + .5) / k;
      sprite({ x: a.x + dx * u, z: a.z + dz * u }, len / k * 1.9, p.glowW * (1 + .6 * surge) * (.8 + .4 * rand(step * 7 + i + m * 31 + seed)),
        look.halo.withAlpha(Math.min(1, look.haloA * (1 + 1.5 * surge)) * alpha), glow, Y + .02, -ang);
    }
  });
  let first = null;
  const count = p.bolts + (surge > .3 ? 1 : 0);
  for (let j = 0; j < count; j++) {
    const sd = seed * 7 + step * 131 + j * 17;
    if (j > 0 && surge < .3 && rand(sd + 999) < .15) continue;
    const flick = .65 + .35 * rand(sd + 5), jag = p.jag * (j ? 1.3 : 1), pts = [];
    ctrl.slice(1).forEach((b, m) => { const piece = boltPts(ctrl[m], b, jag, sd + m * 101); pts.push(...(m ? piece.slice(1) : piece)); });
    first = first ?? pts;
    boltStroke(`${key} bolt ${j}`, pts, widths(pts.length - 1, (j ? .035 : .05) * (1 + .8 * surge), sd), alpha * Math.max(flick, surge), look,
      Y + .03 + j * .002, j ? .8 * surge : .65 + .35 * surge);
    const bi = 1 + Math.floor(rand(sd + 11) * (pts.length - 2)), q0 = pts[bi], q1 = pts[Math.min(pts.length - 1, bi + 1)];
    const dir = Math.atan2(q1.z - q0.z, q1.x - q0.x) + (rand(sd + 12) < .5 ? -1 : 1) * (.5 + .6 * rand(sd + 13)), bl = .14 + .3 * rand(sd + 14);
    boltLine(`${key} branch ${j}`, boltPts(q0, { x: q0.x + Math.cos(dir) * bl, z: q0.z + Math.sin(dir) * bl }, .04, sd + 21, .07),
      .03, alpha * flick * .8, look, Y + .029 + j * .002, 'end');
  }
  return first;
}
// Two white-hot pulses running along a line at PulseSpeed, one each way, dim at the weapons.
function pulses(pts, s, seed, look, alpha) {
  if (!pts || alpha <= .01) return;
  const L = [0];
  for (let i = 1; i < pts.length; i++) L.push(L[i - 1] + Math.hypot(pts[i].x - pts[i - 1].x, pts[i].z - pts[i - 1].z));
  const total = L[L.length - 1];
  if (total < .3) return;
  for (let k = 0; k < 2; k++) {
    let f = (s * PulseSpeed / total + rand(seed + k * 3)) % 1;
    if (k) f = 1 - f;
    const d = f * total;
    let i = 1;
    while (i < L.length - 1 && L[i] < d) i++;
    const t = (d - L[i - 1]) / Math.max(1e-6, L[i] - L[i - 1]), q = { x: lerp(pts[i - 1].x, pts[i].x, t), z: lerp(pts[i - 1].z, pts[i].z, t) };
    const a = alpha * Math.min(1, Math.sin(f * Math.PI) * 3);
    sprite(q, .42, .42, look.dark ? White.withAlpha(.22 * a) : look.halo.withAlpha(.5 * a), glow, Y + .046);
    sprite(q, .2, .2, White.withAlpha(.95 * a), glow, Y + .047);
  }
}
// The floor under a line: a dark trace (Dark Chidori) or a flickering coloured light, and white light
// thrown on the ground during a flash. The same pool sits under each weapon of the net.
function lightPool(A, B, look, alpha, surge, flick) {
  const dx = B.x - A.x, dz = B.z - A.z, len = Math.hypot(dx, dz), ang = Math.atan2(dz, dx) * Mathf.Rad2Deg, k = Math.max(1, Math.ceil(len / 1.2));
  for (let i = 0; i < k; i++) {
    const c = { x: A.x + dx * (i + .5) / k, z: A.z + dz * (i + .5) / k }, l = len / k * 1.9;
    if (look.dark) sprite(c, l, .6, Ink.withAlpha(look.floorA * alpha), soft, Floor + .014, -ang);
    else sprite(c, l, .95, look.floor.withAlpha((.1 + .08 * flick) * alpha), glow, Floor + .014, -ang);
    if (surge > 0) sprite(c, l, 1.2, (look.dark ? White : look.floor).withAlpha((look.dark ? .16 : .3) * surge * alpha), glow, Floor + .015, -ang);
  }
}
function cornerPool(g, look, surge, flick) {
  if (!look.dark) sprite(g, 1.3, 1.3, look.floor.withAlpha(.1 + .06 * flick + .2 * surge), glow, Floor + .016);
  else if (surge > 0) sprite(g, 1.2, 1.2, White.withAlpha(.14 * surge), glow, Floor + .016);
}
// One spark: falls from the net's height under Gravity with a sideways drift (vx, vz cells/s), a
// short tail behind it, and a small flash where it lands. g0 is where it left, on the ground.
function fallingSpark(key, g0, vx, vz, age, look) {
  const fall = Math.sqrt(2 * Hold / Gravity);
  if (age < 0 || age >= fall + .1) return;
  if (age < fall) {
    const pos = at({ x: g0.x + vx * age, z: g0.z + vz * age }, 0, 0, Hold - .5 * Gravity * age * age);
    const sx = vx, sz = vz - Gravity * age * Lift, sl = Math.hypot(sx, sz) || 1;
    line(key, [{ x: pos.x - sx / sl * .14, z: pos.z - sz / sl * .14 }, pos], .03, look.bolt.withAlpha(.9), look.dark ? undefined : whiteGlow, Y + .043, 'none');
    sprite(pos, .08, .08, White, glow, Y + .045);
    return;
  }
  const u = (age - fall) / .1;
  sprite({ x: g0.x + vx * fall, z: g0.z + vz * fall }, .18 * (1 - u), .12 * (1 - u), White.withAlpha(.8 * (1 - u)), glow, Floor + .02);
}
// Sparks spitting off a live line, SparkRate a second, each from where the line was when it left.
// ends(t) gives the line's two ground ends at time t.
function lineSparks(key, ends, lit, end, s, seed, look) {
  const fall = Math.sqrt(2 * Hold / Gravity);
  const first = Math.max(0, Math.floor((s - fall - .1 - lit) * SparkRate)), last = Math.floor((Math.min(s, end) - lit) * SparkRate);
  for (let k = first; k <= last; k++) {
    const sd = seed * 13 + k * 17, born = lit + (k + rand(sd)) / SparkRate;
    if (born > s || born >= end) continue;
    const [A, B] = ends(born), u = rand(sd + 1);
    fallingSpark(`${key} ${k}`, { x: lerp(A.x, B.x, u), z: lerp(A.z, B.z, u) }, (rand(sd + 2) - .5) * 1.2, (rand(sd + 3) - .5) * 1.2, s - born, look);
  }
}
// When a line ends it breaks and falls as sparks, BreakPerCell per cell of its length.
function breakSparks(key, A, B, end, s, seed, look) {
  const age = s - end;
  if (age < 0 || age > .7) return;
  const n = Math.ceil(Math.hypot(B.x - A.x, B.z - A.z) * BreakPerCell);
  for (let k = 0; k < n; k++) {
    const sd = seed * 29 + 500 + k * 13, u = (k + rand(sd + 1)) / n;
    fallingSpark(`${key} ${k}`, { x: lerp(A.x, B.x, u), z: lerp(A.z, B.z, u) }, (rand(sd + 2) - .5) * 1.6, (rand(sd + 3) - .5) * 1.6, age - rand(sd) * .05, look);
  }
}
// Points round the circle of radius R about c from angle a0 to a1 (radians), jittered in and out,
// lifted by h. A level circle stays round on screen, so the Fūma's arcs need no per-facing work.
function arcPts(c, R, a0, a1, jag, seed, h) {
  const n = Math.max(6, Math.round(Math.abs(a1 - a0) * R / .1)), pts = [];
  for (let i = 0; i <= n; i++) {
    const u = i / n, a = a0 + (a1 - a0) * u, r = R + (i > 0 && i < n ? (rand(seed + i * 7) - .5) * 2 * jag * Math.sin(u * Math.PI) : 0);
    pts.push(at({ x: c.x + Math.cos(a) * r, z: c.z + Math.sin(a) * r }, 0, 0, h));
  }
  return pts;
}
// A flash with short bolts thrown out of it: a weapon reached, a pawn caught or hit, a line cut.
function spark(pos, size, look, alpha) {
  if (alpha <= 0) return;
  if (look.dark) sprite(pos, size * 1.6, size * 1.6, Ink.withAlpha(.3 * alpha), soft, Y + .044);
  else sprite(pos, size * 2.4, size * 2.4, look.halo.withAlpha(.4 * alpha), glow, Y + .044);
  sprite(pos, size, size, White.withAlpha(alpha), glow, Y + .046);
}
function burst(key, pos, age, life, size, look, count, seed) {
  if (age < 0 || age >= life) return;
  const u = age / life, f = 1 - u;
  spark(pos, size * (1 - .5 * u), look, f);
  for (let i = 0; i < count; i++) {
    const sd = seed + i * 13, ang = (i + rand(sd)) / count * Math.PI * 2, len = size * (.6 + .8 * rand(sd + 1)) * (.5 + .5 * smooth(u * 3));
    boltLine(`${key} ${i}`, boltPts(pos, { x: pos.x + Math.cos(ang) * len, z: pos.z + Math.sin(ang) * len }, .05, sd + 3, .07),
      .03, f, look, Y + .05 + i * .0003, 'end');
  }
}
// A link that was too long when the run reached it: the bolt gets a third of the way and dies.
function fizzle(key, a, b, age, s, p, look) {
  if (age < 0 || age > .3) return;
  const u = age / .3, reach = .35 * smooth(u * 2), head = { x: lerp(a.x, b.x, reach), z: lerp(a.z, b.z, reach) };
  boltLine(key, boltPts(a, head, .08, 77 + Math.floor(s * p.boil) * 13), .035, 1 - u, look, Y + .03, 'end', .5);
  burst(`${key} end`, head, age, .3, .25, look, 4, 31);
}
// The rule overlay: the cells a line's ground track crosses.
function cells(A, B, o, alpha) {
  const seen = new Set(), n = Math.max(1, Math.ceil(Math.hypot(B.x - A.x, B.z - A.z) / .1));
  for (let i = 0; i <= n; i++) {
    const x = o.x + Math.round(lerp(A.x, B.x, i / n) - o.x), z = o.z + Math.round(lerp(A.z, B.z, i / n) - o.z), key = `${x},${z}`;
    if (seen.has(key)) continue;
    seen.add(key);
    draw(MeshPool.plane10, x, Floor + .006, z, .94, .94, 0, White.withAlpha(.13 * alpha));
  }
}
// The shake of a body the current runs through: a new offset of up to size on every redraw.
function shakeOf(k, s, p, size) {
  const step = Math.floor(s * p.boil);
  return { x: (rand(step * 13 + k * 71 + 1) - .5) * 2 * size, z: (rand(step * 17 + k * 71 + 2) - .5) * 2 * size };
}
// Bolts crawling over a body at q (a pawn's ground position; the body spans 0 to 0.75 up the screen),
// and a white flash about 3 times a second: the muscles lock.
function crackle(key, q, s, p, look, alpha, count, size, seed) {
  if (alpha <= .01) return;
  const step = Math.floor(s * p.boil);
  for (let i = 0; i < count; i++) {
    const sd = seed + step * 97 + i * 13;
    const a = { x: q.x + (rand(sd) - .5) * .34 * size, z: q.z + (.02 + rand(sd + 1) * .62) * size };
    const b = { x: q.x + (rand(sd + 2) - .5) * .42 * size, z: q.z + (.08 + rand(sd + 3) * .7) * size };
    boltLine(`${key} ${i}`, boltPts(a, b, .06 * size, sd + 5, .07), .032 * size, alpha * (.6 + .4 * rand(sd + 4)), look, Y + .035 + i * .0004, 'both', i ? 0 : .6);
  }
  const ph = (s * 3.3 + seed * .137) % 1;
  if (ph < .2) sprite(at(q, 0, .36), .5, .92, White.withAlpha(.4 * alpha * (1 - ph / .2)), glow, Y + .034);
}

// The Chidori charging in the caster's hand: a white core that beats and grows, bolts crackling out of
// it and up the arm (the Dark Chidori frame). Gone 0.15 s after the leap.
function chargeHand(hand, caster, s, p, look, T) {
  const u = clamp(s / T.leap), a = Math.min(1, u * 3) * (1 - clamp((s - T.leap) / .15));
  if (a <= 0) return;
  const step = Math.floor(s * p.boil), core = (.2 + .3 * u) * (.85 + .15 * Math.sin(s * 40));
  if (look.dark) sprite(hand, core * 2.4, core * 2.4, Ink.withAlpha(.25 * a), soft, Y + .05);
  else sprite(hand, core * 4, core * 4, look.halo.withAlpha(.45 * a), glow, Y + .05);
  sprite(hand, core * 1.6, core * 1.6, White.withAlpha(.9 * a), glow, Y + .058);
  sprite(hand, core * .7, core * .7, White.withAlpha(a), glow, Y + .059);
  const count = 6 + Math.round(4 * u);
  for (let i = 0; i < count; i++) {
    const sd = step * 53 + i * 11 + 7, ang = rand(sd) * Math.PI * 2, len = (.25 + .5 * rand(sd + 1)) * (.5 + .5 * u);
    const tip = { x: hand.x + Math.cos(ang) * len, z: hand.z + Math.sin(ang) * len * .8 };
    boltLine(`raiko hand ${i}`, boltPts(hand, tip, .07, sd + 3, .07), .04, a * (.6 + .4 * rand(sd + 2)), look, Y + .052 + i * .0005, 'end', i % 3 ? 0 : .7);
  }
  for (let i = 0; i < 3; i++) {
    const sd = step * 71 + i * 19 + 3, tip = { x: caster.x + (rand(sd) - .5) * .3, z: caster.z + .35 + rand(sd + 1) * .35 };
    boltLine(`raiko arm ${i}`, boltPts(hand, tip, .05, sd + 5, .06), .028, a * .8 * u, look, pawnLayer + .006 + i * .0005, 'end');
  }
}

// Stand-ins. A shield belt's bubble, and how it pops; a mechanoid (a scyther-like body with two
// blades); the blue EMP crackle a mechanoid keeps after the net; the shooter's gun and tracers.
function bubble(q) {
  const c = at(q, 0, .32);
  sprite(c, 1.25, 1.25, ShieldBlue.withAlpha(.14), glow, Y + .01);
  circle(c, .62, .55, Y + .011, ShieldBlue);
}
function bubbleBreak(q, age) {
  if (age < 0 || age >= .35) return;
  const u = age / .35, c = at(q, 0, .32);
  sprite(c, 1.3 + u, 1.3 + u, White.withAlpha(.5 * (1 - u)), glow, Y + .012);
  circle(c, .62 + .5 * smooth(u), .8 * (1 - u), Y + .013, ShieldBlue);
  for (let i = 0; i < 7; i++) {
    const ang = i * .9 + .3, r = .62 + .6 * u;
    sprite({ x: c.x + Math.cos(ang) * r, z: c.z + Math.sin(ang) * r }, .12, .05, ShieldBlue.withAlpha(1 - u), whiteGlow, Y + .014, -ang * Mathf.Rad2Deg + u * 200);
  }
}
function mechFigure(q, sun, strength) {
  sprite({ x: q.x + sun.x * .5, z: q.z + sun.z * .5 }, .95, .45, ShadowInk.withAlpha(strength), soft, shadowLayer);
  draw(MeshPool.plane10, q.x - .27, pawnLayer - .001, q.z + .32, .05, .46, -18, MechDark);
  draw(MeshPool.plane10, q.x + .27, pawnLayer - .001, q.z + .32, .05, .46, 18, MechDark);
  draw(flatDisc, q.x, pawnLayer, q.z + .22, .25, .33, 0, MechGrey);
  draw(flatDisc, q.x, pawnLayer + .002, q.z + .6, .17, .13, 0, MechDark);
  draw(flatDisc, q.x + .05, pawnLayer + .004, q.z + .62, .035, .035, 0, MechEye);
}
function emp(key, q, s, age, p) {
  crackle(key, q, s, p, EmpLook, .7, 2, 1, 900);
  const u = (age * 1.6) % 1;
  circle(at(q, 0, .32), .25 + .35 * u, .6 * (1 - u), Y + .02, EmpBlue);
}
// The raider behind the net shoots at the caster every ShotGap s while it holds. The tracers fly
// through the lines untouched and wide of the caster: the net stops bodies, not bullets.
function shots(shooter, caster, T, s) {
  const dx = caster.x - shooter.x, dz = caster.z - shooter.z, len = Math.hypot(dx, dz), ux = dx / len, uz = dz / len;
  const muzzle = { x: shooter.x + ux * .32, z: shooter.z + .3 + uz * .32 };
  draw(MeshPool.plane10, shooter.x + ux * .18, pawnLayer + .003, shooter.z + .3 + uz * .18, .3, .05, -Math.atan2(dz, dx) * Mathf.Rad2Deg, Gun);
  for (let i = 0, t0 = FirstShot; t0 < T.netEnd && t0 <= s; i++, t0 += ShotGap) {
    const age = s - t0, miss = (rand(i * 31 + 7) < .5 ? -1 : 1) * (.5 + .4 * rand(i * 31 + 8));
    if (age < .06) sprite(muzzle, .32, .32, Tracer.withAlpha(1 - age / .06), glow, Y + .04);
    const end = { x: caster.x - uz * miss + ux * 2, z: caster.z + .3 + ux * miss + uz * 2 }, dl = Math.hypot(end.x - muzzle.x, end.z - muzzle.z);
    const head = age * TracerSpeed;
    if (head > dl + .6) continue;
    const at1 = f => ({ x: lerp(muzzle.x, end.x, clamp(f / dl)), z: lerp(muzzle.z, end.z, clamp(f / dl)) }), h = at1(head), t = at1(head - .6);
    line(`raiko tracer ${i}`, [t, h], .07, Tracer.withAlpha(.9), whiteGlow, Y + .04, 'none');
    line(`raiko tracer ${i} core`, [t, h], .025, White, whiteGlow, Y + .0402, 'none');
  }
}

export default {
  kit: 'Rinnegan', label: 'Raikō Kusari (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    aim: P('Direction (degrees, 0 east)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Caster to the net (cells)', 5, 3, 9, .5, 'Showcase'),
    cells: { label: 'Show the cells that catch (rule overlay)', value: false, group: 'Showcase' },
    charge: P('Chidori charge (warmup)', .6, .2, 1.5, .05, 'Timing (s)'),
    lasts: P('Net lasts (showcase; 8 in the rule)', 4, 1, 8, .5, 'Timing (s)'),
    letGo: P('Amenoyodomi lets go after the net forms', 2, .5, 5, .1, 'Timing (s)'),
    maxLink: P('Longest link (cells)', 6, 2, 9, .5, 'Rule'),
    colour: { label: 'Colour', value: PaletteNames[0], options: PaletteNames, group: 'Look' },
    bolts: P('Bolts per line', 2, 1, 4, 1, 'Look'),
    boil: P('Redraws per second', 12, 4, 30, 1, 'Look'),
    jag: P('Jag (cells)', .14, .02, .4, .01, 'Look'),
    glowW: P('Halo width (cells)', .35, .1, 1, .05, 'Look'),
  },
  duration(p) { return build(p, { x: 0, z: 0 }).end; },
  phases(p) {
    const S = build(p, { x: 0, z: 0 }), T = S.T, marks = [{ name: 'Charge', t: 0 }, { name: 'Leaps', t: T.leap }];
    if (!T.links.some(L => !L.fails)) marks.push({ name: 'Links too long: no net', t: T.reach0 });
    else {
      marks.push({ name: 'Net', t: T.formed });
      const first = Math.min(...S.pawns.map(P => P.caught));
      if (isFinite(first)) marks.push({ name: 'Caught', t: first });
      if (S.letsGo) marks.push({ name: 'Let go', t: T.letGo });
      else {
        const snaps = T.links.filter(L => isFinite(L.snap)).map(L => L.snap);
        if (snaps.length) marks.push({ name: 'Snaps', t: Math.min(...snaps) });
        if (!snaps.length || T.netEnd > Math.min(...snaps) + .05) marks.push({ name: 'Ends', t: T.netEnd });
      }
      if (S.pawns.some(P => P.kind === 'mech' && isFinite(P.caught))) marks.push({ name: 'Mech free', t: T.netEnd + MechExtra });
    }
    return marks.filter(m => m.t < S.end).sort((a, b) => a.t - b.t);
  },
  events(p) {
    const T = build(p, { x: 0, z: 0 }).T;
    return [{ t: 0, type: 'sound', def: 'AG_RaikoKusari_Charge' }, { t: T.leap, type: 'shake', value: ShakeSize }];
  },

  draw(s, p, { origin: o, scene: sc }) {
    const S = build(p, o), T = S.T;
    if (s < 0 || s >= S.end) return;
    const sun = sc?.shadowVector ?? { x: -.45, z: -.32 }, strength = sc?.sun?.strength ?? .32;
    const look = Palettes[p.colour] ?? Palettes[PaletteNames[0]], step = Math.floor(s * p.boil);
    const lift = g => at(g, 0, 0, Hold);
    const live = (L, t) => !L.fails && t >= L.lit && t < Math.min(L.snap, T.netEnd);

    // Where each pawn is drawn: a body the current runs through shakes on every redraw.
    const bodies = S.pawns.map((P, k) => {
      const q = pawnAt(P, s), held = s >= P.caught && s < T.netEnd;
      const emped = P.kind === 'mech' && isFinite(P.caught) && s >= T.netEnd && s < T.netEnd + MechExtra;
      const size = held ? ShakeHeld : emped || P.hits.some(th => s >= th && s < th + HitStun) ? ShakeHit : 0;
      if (!size) return q;
      const j = shakeOf(k, s, p, size);
      return { x: q.x + j.x, z: q.z + j.z };
    });

    // The caster: the Chidori charges in its hand, then leaps to the first weapon; the bolt lingers.
    figure(S.caster, CasterColour, 1, 0, sun, strength);
    const hand = { x: S.caster.x + S.d.x * .26, z: S.caster.z + .25 + S.d.z * .26 };
    chargeHand(hand, S.caster, s, p, look, T);
    const lu = (s - T.leap) / Leap;
    if (lu >= 0 && s < T.leap + Leap + .12) {
      const first = lift(weaponAt(S.W[0], s, T)), head = lu < 1 ? { x: lerp(hand.x, first.x, lu), z: lerp(hand.z, first.z, lu) } : first;
      lightning('raiko leap', [hand, head], s, p, look, lu < 1 ? 1 : 1 - (s - T.leap - Leap) / .12, 11, lu < 1 ? 1 : 0);
      if (lu < 1) spark(head, .3, look, 1);
    }

    // The lines, lit one after another. Each flashes on its own rhythm, carries two pulses, lights
    // the floor, spits sparks, bends through the chest of everyone it holds, and breaks into sparks.
    const cornerSurge = S.W.map(() => 0);
    T.links.forEach((L, i) => {
      if (s < L.start) return;
      const key = `raiko link ${i}`, seed = 20 + i * 7, A = joint(S, T, L.a, L.b, s), B = joint(S, T, L.b, L.a, s), a = lift(A), b = lift(B);
      if (L.fails) { fizzle(key, a, b, s - L.start, s, p, look); return; }
      const u = (s - L.start) / PerLink;
      if (u < 1) {
        const head = { x: lerp(a.x, b.x, u), z: lerp(a.z, b.z, u) };
        lightning(key, [a, head], s, p, look, 1, seed, 1);
        spark(head, .26, look, 1);
        return;
      }
      const end = Math.min(L.snap, T.netEnd), cut = L.snap <= T.netEnd || S.letsGo, f = 1 - clamp((s - end) / CutOut);
      lineSparks(`${key} spark`, t => [joint(S, T, L.a, L.b, t), joint(S, T, L.b, L.a, t)], L.lit, end, s, seed, look);
      if (f > 0) {
        const surge = Math.max(surgeOf(seed, s), s >= T.formed && s < T.formed + CloseFlash ? 1 : 0) * f;
        const dx = b.x - a.x, dz = b.z - a.z, l2 = dx * dx + dz * dz || 1;
        const chests = S.pawns.map((P, k) => ({ P, k })).filter(({ P }) => P.link === i && s >= P.caught && s < T.netEnd)
          .map(({ k }) => { const c = at(bodies[k], 0, .38); return { x: c.x, z: c.z, u: ((c.x - a.x) * dx + (c.z - a.z) * dz) / l2 }; })
          .filter(c => c.u > .02 && c.u < .98).sort((m, n) => m.u - n.u);
        pulses(lightning(key, [a, ...chests, b], s, p, look, f, seed, surge), s, seed, look, f);
        lightPool(A, B, look, f, surge, .7 + .3 * rand(step * 3 + i));
        if (p.cells) cells(A, B, o, f);
        cornerSurge[L.a] = Math.max(cornerSurge[L.a], surge);
        cornerSurge[L.b] = Math.max(cornerSurge[L.b], surge);
      }
      if (s >= end) {
        breakSparks(`${key} break`, joint(S, T, L.a, L.b, end), joint(S, T, L.b, L.a, end), end, s, seed, look);
        if (cut) {
          burst(`${key} cut a`, a, s - end, .22, .35, look, 4, 60 + i);
          burst(`${key} cut b`, b, s - end, .22, .35, look, 4, 80 + i);
        }
      }
    });
    const reached = [{ i: 0, t: T.reach0, ring: false }];
    T.links.forEach(L => { if (!L.fails) reached.push({ i: L.b, t: L.lit, ring: !!L.closing }); });
    reached.forEach((R, j) => burst(`raiko reach ${j}`, lift(weaponAt(S.W[R.i], s, T)), s - R.t, CornerFlash * (R.ring ? 1.6 : 1),
      R.ring ? .6 : .38, look, R.ring ? 7 : 5, 140 + j * 9));

    // The weapons, drawn by lib/amenoyodomi.js (held with its marks and afterimages, flying, lying), with
    // the charge on top: lit while they are corners, then flying on charged.
    S.W.forEach((w, i) => {
      const g = weaponAt(w, s, T), deg = w.deg, sg = cornerSurge[i];
      const flying = s >= T.letGo && s < w.stopT, corner = T.links.some(L => (L.a === i || L.b === i) && live(L, s));
      drawWeapon(`raiko weapon ${i}`, w, s, { sun, strength, streak: false });
      if (corner) cornerPool(g, look, sg, .7 + .3 * rand(step * 5 + i));
      if (w.kind === 'fuma') {
        const turn = turnAt(w, s, T);
        if (s >= w.stopT) {
          burst('raiko fuma lands', at(g, 0, .1), s - w.stopT, .5, .7, look, 6, 500);
          return;
        }
        const c = at(g, 0, 0, Hold);
        if (flying) {
          // Charged in flight: broken arcs of lightning spinning round it (never a closed ring: a black
          // ring round the Fūma read as a tyre in the Amaterasu sketch), and a jagged tail.
          for (let k = 0; k < 3; k++) {
            const sd = step * 37 + k * 5, a0 = -turn * Mathf.Deg2Rad + k * 2.094 + rand(sd) * .5, span = .9 + .5 * rand(sd + 1);
            const pts = arcPts(g, (.46 + .1 * rand(sd + 2)) * FumaSize, a0, a0 + span, .1, sd + 3, Hold);
            boltStroke(`raiko fuma ring ${k}`, pts, widths(pts.length - 1, .04, sd), .95, look, Y + .031 + k * .0003, k ? 0 : .7);
          }
          boltLine('raiko fuma trail', boltPts(at({ x: g.x - w.dir.x * 1.1, z: g.z - w.dir.z * 1.1 }, 0, 0, Hold), c, .1, step * 43, .15), .04, .85, look, Y + .03, 'both', .6);
        } else if (corner) {
          // Part of the net: arcs jump round the rim from a blade tip toward the next as it turns. Each
          // shows on about 60 % of redraws and covers 55-90 of the 90 degrees, so they never close
          // into a ring (a black ring round the Fūma read as a tyre in the Amaterasu sketch).
          for (let k = 0; k < 4; k++) {
            const sd = step * 37 + k * 5;
            if (rand(sd + 9) > .6 && sg < .3) continue;
            const a0 = bladeAngle(turn, k, TipR), pts = arcPts(g, TipR * FumaSize, a0, a0 + Math.PI / 2 * (.6 + .4 * rand(sd + 1)), .1, sd, Hold);
            boltStroke(`raiko fuma arc ${k}`, pts, widths(pts.length - 1, .032 * (1 + .8 * sg), sd), .85, look, Y + .031 + k * .0003, k ? .8 * sg : .5 + .5 * sg);
          }
          T.links.forEach(L => {
            if (!live(L, s) || (L.a !== i && L.b !== i)) return;
            spark(lift(joint(S, T, i, L.a === i ? L.b : L.a, s)), .2 * (.75 + .25 * Math.sin(s * 37 + i * 2)) * (1 + .8 * sg), look, .9);
          });
        }
        return;
      }
      if (s < w.stopT) {
        if (corner) spark(lift(g), .2 * (.75 + .25 * Math.sin(s * 37 + i * 2)) * (1 + .8 * sg), look, .9);
        if (flying) {
          const tail = lift({ x: g.x - w.dir.x * .8, z: g.z - w.dir.z * .8 });
          boltLine(`raiko trail ${i}`, boltPts(tail, lift(g), .07, step * 29 + i * 3, .13), .035, .9, look, Y + .03, 'both', .6);
          spark(lift(g), .16, look, .8);
        }
      } else if (w.hit) stuckKunai(bodies[w.hit.pawn], deg, 0);
    });

    // The pawns: caught, held, let go; a scorch stays where each one was held.
    S.pawns.forEach((P, k) => {
      const q = bodies[k], key = `raiko pawn ${k}`, held = s >= P.caught;
      if (held) {
        const cq = pawnAt(P, P.caught);
        sprite(at(cq, 0, .02), .78, .46, Ink.withAlpha(.34 * smooth((Math.min(s, T.netEnd) - P.caught) / 1.5)), soft, Floor + .012);
      }
      if (P.kind === 'mech') mechFigure(q, sun, strength);
      else figure(q, P.kind === 'ally' ? Ally : EnemyColour, 1, 0, sun, strength);
      if (P.kind === 'shield') { if (!held) bubble(q); else bubbleBreak(q, s - P.caught); }
      if (P.kind === 'shooter') shots(q, S.caster, T, s);
      if (held) {
        burst(`${key} catch`, at(q, 0, .4), s - P.caught, .16, .5, look, 6, 200 + k * 11);
        crackle(`${key} held`, q, s, p, look, 1 - clamp((s - T.netEnd) / FadeOut), 3, 1, 300 + k * 17);
        if (P.kind === 'mech' && s >= T.netEnd && s < T.netEnd + MechExtra) emp(`${key} emp`, q, s, s - T.netEnd, p);
      }
      // Struck by a charged kunai or cut by the charged Fūma: stunned 2 s.
      P.hits.forEach((th, j) => {
        if (s < th || s >= th + HitStun) return;
        burst(`${key} hit ${j}`, at(q, 0, .4), s - th, .18, .45, look, 5, 400 + k * 13 + j);
        crackle(`${key} stun ${j}`, q, s, p, look, .75 * (1 - clamp((s - th - HitStun + .3) / .3)), 2, .9, 500 + k * 19 + j);
      });
    });
  },
};
