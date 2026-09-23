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
// Chidori: a white-hot core in the hand with jagged bolts, redrawn about 12 times a second as the
// anime does, in the Six Paths "Dark Chidori" colours (the Storm 4 name; the anime frame shows black
// jagged lines crawling over his arm and chest round a white core). Colour is a dropdown: Dark
// Chidori (default, the user's pick), Chidori blue-white, Rinnegan violet.
//
// Showcase, default timings:
//   0.00-0.60  charge: a white core beats in the caster's hand, bolts crackle out of it and up the arm
//   0.60       the Chidori leaps to the first weapon (0.05 s) and runs down the chain, 0.05 s per
//              link; each weapon flashes as it is reached, a ring closes with a larger flash. Camera
//              shake 0.03.
//   net        each line: a soft halo, jagged bolts (the first with a white thread) and a side branch
//              per bolt, all redrawn 12 times a second, and a faint strip on the floor under it. The
//              weapons hang 0.55 cells up over a small lavender ring (Amenoyodomi's stand-in mark).
//   caught     a burst where it touched; the pawn freezes mid-step, bolts crawl over its body, it
//              flashes white about 3 times a second, and a scorch grows under its feet and stays
//   end        the lines thin out over 0.3 s; a let-go or a snap cuts them in 0.12 s with sparks
// Scenarios:
//   fence         3 kunai across the approach, 6.4 cells end to end, so no ring. A raider, a raider
//                 with a shield belt and a mechanoid run in and are caught; the shield pops. At the
//                 end the raiders run on and the mech stays stunned 3 s more (blue EMP crackle).
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
//                 and arcs jump between its blade tips as it turns. A raider is caught on its line.
//                 Let go: the Fūma flies its line at 14.4 cells/s at full spin, crackling, cuts three
//                 raiders (each stunned 2 s) and lands 12 cells from the caster.
//
// Drawing: every line joins two weapons at the same height, so the net is a flat shape at one
// height and turns freely with the direction: no per-facing method. Bolts and pawn crackle are
// screen-oriented strips rebuilt on every redraw from a hash of the redraw step, so scrubbing is
// deterministic. Who is caught, and when, is replayed from the cast in 1/60 s steps each frame. Here
// a pawn touches a line within 0.45 cells of its ground track; the port should use the cells the
// line crosses ("Show the cells that catch" draws them). The hold is a stand-in until Amenoyodomi
// has a sketch: a weapon thrown at a cell reached it at 0 s and goes on at the hold speed (1 %: a
// kunai 0.24 cells/s, the Fūma 0.144), and a let-go sends it on at full speed along its heading.
// The weapons use the game's textures and numbers: RimArt/Kunai/Kunai at 0.75 and 24 cells/s,
// range 14.9; RimArt/Fuma/Unfolded at 1.4 and 14.4 cells/s, range 12, spin 720 degrees/s (a
// stand-in, as in the Amaterasu sketch). The halo and floor strip are soft-disc sprites stretched
// along the line, so the light has no hard edge; only the thin bolts are strips.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, at, sprite, circle, glow, soft, rand } from './lib/six-paths-impact.js';
import { figure, whiteGlow, kunaiMat, stuckKunai, CasterColour, EnemyColour } from './lib/flying-thunder-god.js';
import { line } from './lib/goku.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const pawnLayer = AltitudeLayer.Pawn.AltitudeFor(), shadowLayer = AltitudeLayer.Shadows.AltitudeFor();
const projectileLayer = AltitudeLayer.Projectile.AltitudeFor();
const flatDisc = Meshes.disc(40, 'raiko disc');
const HeldRing = .32, heldRing = Meshes.band(1 - .035 / HeldRing, 1, 48, 'raiko held ring');
const fumaMat = MaterialPool.MatFrom('RimArt/Fuma/Unfolded', ShaderDatabase.Cutout);
const fumaGhost = MaterialPool.MatFrom('RimArt/Fuma/Unfolded', ShaderDatabase.Transparent);

// Decided looks. Dark Chidori is black bolts round a white thread; the other two are light.
const White = new Color(1, 1, 1), Ink = new Color(.02, .018, .03), ShadowInk = new Color(.03, .03, .05);
const Lavender = new Color(.80, .74, .98), Ally = new Color(.45, .62, .40);
const ShieldBlue = new Color(.45, .72, 1), EmpBlue = new Color(.55, .82, 1);
const MechGrey = new Color(.46, .48, .52), MechDark = new Color(.24, .25, .28), MechEye = new Color(.95, .22, .15);
const Palettes = {
  'Dark Chidori': { dark: true, bolt: Ink, halo: new Color(.72, .68, .88), haloA: .14, floor: Ink, floorA: .24 },
  'Chidori blue-white': { dark: false, bolt: new Color(.82, .93, 1), halo: new Color(.22, .5, 1), haloA: .32, floor: new Color(.22, .5, 1), floorA: .1 },
  'Rinnegan violet': { dark: false, bolt: new Color(.9, .82, 1), halo: new Color(.52, .3, .96), haloA: .32, floor: new Color(.52, .3, .96), floorA: .1 },
};
const EmpLook = { dark: false, bolt: new Color(.82, .94, 1), halo: EmpBlue, haloA: .3 };
const PaletteNames = Object.keys(Palettes);
const Scenarios = ['fence', 'ring', 'drifting net', 'let go', 'Fūma corner'];

// The game's weapon numbers (AG_KunaiProjectile, AG_FumaProjectile); the hold is a stand-in.
const KunaiSpeed = 24, KunaiSize = .75, KunaiRange = 14.9, FumaSpeed = 14.4, FumaSize = 1.4, FumaRange = 12, FumaSpin = 720;
const Hold = .55, Creep = .01, Drift = .1, TipR = .55;
// Timing and the rule.
const Leap = .05, PerLink = .05, FadeOut = .3, SnapOut = .12, CornerFlash = .15, Touch = .45, Step = 1 / 60;
const MechExtra = 3, HitStun = 2, HitReach = .4, CutReach = .55;
const RunSpeed = 3, WalkSpeed = 1.5, MechSpeed = 2.2, ShakeSize = .03;

// The scene, laid out along the aim d from the caster (n is d's left); o is the middle of the net.
// Weapons are listed in the order they were thrown.
function scene(p, o) {
  const a = p.aim * Mathf.Deg2Rad, d = { x: Math.cos(a), z: Math.sin(a) }, n = { x: -d.z, z: d.x };
  const place = (u, v) => ({ x: o.x + d.x * u + n.x * v, z: o.z + d.z * u + n.z * v });
  const caster = place(-p.distance, 0), back = { x: -d.x, z: -d.z };
  const weapon = (kind, u, v, share = Creep) => {
    const cell = place(u, v), dx = cell.x - caster.x, dz = cell.z - caster.z, l = Math.hypot(dx, dz);
    return { kind, cell, dir: { x: dx / l, z: dz / l }, share, speed: kind === 'fuma' ? FumaSpeed : KunaiSpeed };
  };
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
      pawns = [runIn('raider', 4, 1.6, 0), runIn('shield', 5, -1, .2), runIn('mech', 5.6, -2.3, .3, MechSpeed)];
  }
  return { d, n, caster, W, pawns, letsGo };
}

// Where a weapon is on the ground: at its cell at 0 s, then on at its hold share of its speed; after a
// let-go at full speed along its heading until it stops (stopT).
function heldAt(w, s) {
  const u = w.speed * w.share * Math.max(0, s);
  return { x: w.cell.x + w.dir.x * u, z: w.cell.z + w.dir.z * u };
}
function weaponAt(w, s, T) {
  if (s <= T.letGo) return heldAt(w, s);
  const q = heldAt(w, T.letGo), u = w.speed * (Math.min(s, w.stopT ?? Infinity) - T.letGo);
  return { x: q.x + w.dir.x * u, z: q.z + w.dir.z * u };
}
// Clockwise degrees the Fūma has turned: its hold share of the spin while held, full spin in flight.
function turnAt(w, s, T) {
  return Math.max(0, Math.min(s, T.letGo)) * FumaSpin * w.share + Math.max(0, Math.min(s, w.stopT ?? Infinity) - T.letGo) * FumaSpin;
}
// Blade k of the Fūma at r texture widths from its middle, turned clockwise by turn degrees, as a
// ground offset. Measured off RimArt/Fuma/Unfolded (see the Amaterasu sketch): 22 + 40 r + 90 k.
function blade(c, turn, k, r) {
  const a = (22 + 40 * r + 90 * k - turn) * Mathf.Deg2Rad;
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
  S.W.forEach(w => { w.stopT = Infinity; w.hit = null; w.cuts = []; });
  if (!isFinite(T.letGo)) return;
  S.W.forEach(w => {
    const q = heldAt(w, T.letGo), range = Math.max(0, (w.kind === 'fuma' ? FumaRange : KunaiRange) - Math.hypot(q.x - S.caster.x, q.z - S.caster.z));
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
    // The first moment it touches a live line, replayed from the cast.
    P.caught = Infinity;
    for (let t = T.reach0; t < T.netEnd && !isFinite(P.caught); t += Step) {
      const q = runAt(P, t);
      for (const L of T.links) {
        if (L.fails || t < L.lit || t >= L.snap) continue;
        if (segDist(q, joint(S, T, L.a, L.b, t), joint(S, T, L.b, L.a, t)) <= Touch) { P.caught = t; break; }
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

// A jagged bolt from a to b: inner points pushed sideways by up to jag (most in the middle, less on
// a short bolt) and jittered along it; seed picks the shape.
function boltPts(a, b, jag, seed, seg = .3) {
  const dx = b.x - a.x, dz = b.z - a.z, len = Math.hypot(dx, dz) || 1e-6;
  const n = Math.max(3, Math.round(len / seg)), nx = -dz / len, nz = dx / len, j = jag * Math.min(1, len / .8), pts = [];
  for (let i = 0; i <= n; i++) {
    const u = i / n, inner = i > 0 && i < n;
    const off = inner ? (rand(seed + i * 7) - .5) * 2 * j * Math.min(1, Math.sin(u * Math.PI) * 1.8) : 0;
    const v = inner ? u + (rand(seed + i * 7 + 3) - .5) * .5 / n : u;
    pts.push({ x: a.x + dx * v + nx * off, z: a.z + dz * v + nz * off });
  }
  return pts;
}
// One bolt through pts. Dark Chidori: black, with a white thread when core > 0. Light looks: an
// additive halo strip, a pale core and the white thread.
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
// A lit line from a to b (screen points): a soft halo of stretched soft discs, then p.bolts jagged
// bolts with one side branch each, all redrawn p.boil times a second. Bolt 0 carries the white thread;
// the others drop out for a redraw now and then.
function lightning(key, a, b, s, p, look, alpha, seed, widthF = 1) {
  const dx = b.x - a.x, dz = b.z - a.z, len = Math.hypot(dx, dz);
  if (len < .03 || alpha <= .01) return;
  const step = Math.floor(s * p.boil), ang = Math.atan2(dz, dx) * Mathf.Rad2Deg, k = Math.max(1, Math.ceil(len / 1.1));
  for (let i = 0; i < k; i++) {
    const u = (i + .5) / k;
    sprite({ x: a.x + dx * u, z: a.z + dz * u }, len / k * 1.9, p.glowW * (.8 + .4 * rand(step * 7 + i + seed)),
      look.halo.withAlpha(look.haloA * alpha), glow, Y + .02, -ang);
  }
  for (let j = 0; j < p.bolts; j++) {
    const sd = seed * 7 + step * 131 + j * 17;
    if (j > 0 && rand(sd + 999) < .15) continue;
    const flick = .65 + .35 * rand(sd + 5), pts = boltPts(a, b, p.jag * (j ? 1.3 : 1), sd);
    boltLine(`${key} bolt ${j}`, pts, (j ? .035 : .05) * widthF, alpha * flick, look, Y + .03 + j * .002, 'none', j ? 0 : .65);
    const bi = 1 + Math.floor(rand(sd + 11) * (pts.length - 2)), q0 = pts[bi], q1 = pts[Math.min(pts.length - 1, bi + 1)];
    const dir = Math.atan2(q1.z - q0.z, q1.x - q0.x) + (rand(sd + 12) < .5 ? -1 : 1) * (.5 + .6 * rand(sd + 13)), bl = .14 + .3 * rand(sd + 14);
    boltLine(`${key} branch ${j}`, boltPts(q0, { x: q0.x + Math.cos(dir) * bl, z: q0.z + Math.sin(dir) * bl }, .04, sd + 21, .07),
      .03 * widthF, alpha * flick * .8, look, Y + .029 + j * .002, 'end');
  }
}
// A bright point: a white core, over a dark smudge (Dark Chidori) or a coloured glow.
function spark(pos, size, look, alpha) {
  if (alpha <= 0) return;
  if (look.dark) sprite(pos, size * 1.6, size * 1.6, Ink.withAlpha(.3 * alpha), soft, Y + .044);
  else sprite(pos, size * 2.4, size * 2.4, look.halo.withAlpha(.4 * alpha), glow, Y + .044);
  sprite(pos, size, size, White.withAlpha(alpha), glow, Y + .046);
}
// A flash with short bolts thrown out of it: a weapon reached, a pawn caught or hit, a line cut.
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
// Soft stretched discs along a line's ground track: the floor under the net.
function floorStrip(A, B, look, alpha) {
  const dx = B.x - A.x, dz = B.z - A.z, len = Math.hypot(dx, dz), ang = Math.atan2(dz, dx) * Mathf.Rad2Deg, k = Math.max(1, Math.ceil(len / 1.2));
  for (let i = 0; i < k; i++) {
    const u = (i + .5) / k;
    sprite({ x: A.x + dx * u, z: A.z + dz * u }, len / k * 1.8, .6, look.floor.withAlpha(look.floorA * alpha), look.dark ? soft : glow, Floor + .014, -ang);
  }
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
// it and two up the arm (the Dark Chidori frame). Gone 0.15 s after the leap.
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
// blades); the blue EMP crackle a mechanoid keeps after the net.
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

    // The caster: the Chidori charges in its hand, then leaps to the first weapon; the bolt lingers.
    figure(S.caster, CasterColour, 1, 0, sun, strength);
    const hand = { x: S.caster.x + S.d.x * .26, z: S.caster.z + .25 + S.d.z * .26 };
    chargeHand(hand, S.caster, s, p, look, T);
    const lu = (s - T.leap) / Leap;
    if (lu >= 0 && s < T.leap + Leap + .12) {
      const first = lift(weaponAt(S.W[0], s, T)), head = lu < 1 ? { x: lerp(hand.x, first.x, lu), z: lerp(hand.z, first.z, lu) } : first;
      lightning('raiko leap', hand, head, s, p, look, lu < 1 ? 1 : 1 - (s - T.leap - Leap) / .12, 11);
      if (lu < 1) spark(head, .3, look, 1);
    }

    // The lines, lit one after another; each weapon flashes as the run reaches it.
    T.links.forEach((L, i) => {
      if (s < L.start) return;
      const key = `raiko link ${i}`, A = joint(S, T, L.a, L.b, s), B = joint(S, T, L.b, L.a, s), a = lift(A), b = lift(B);
      if (L.fails) { fizzle(key, a, b, s - L.start, s, p, look); return; }
      const u = (s - L.start) / PerLink;
      if (u < 1) {
        const head = { x: lerp(a.x, b.x, u), z: lerp(a.z, b.z, u) };
        lightning(key, a, head, s, p, look, 1, 20 + i * 7);
        spark(head, .26, look, 1);
        return;
      }
      const end = Math.min(L.snap, T.netEnd), cut = L.snap <= T.netEnd || S.letsGo, f = 1 - clamp((s - end) / (cut ? SnapOut : FadeOut));
      if (f > 0) {
        lightning(key, a, b, s, p, look, f, 20 + i * 7, .5 + .5 * f);
        floorStrip(A, B, look, f);
        if (p.cells) cells(A, B, o, f);
      }
      if (cut && s >= end) {
        burst(`${key} cut a`, a, s - end, .22, .35, look, 4, 60 + i);
        burst(`${key} cut b`, b, s - end, .22, .35, look, 4, 80 + i);
      }
    });
    const reached = [{ i: 0, t: T.reach0, ring: false }];
    T.links.forEach(L => { if (!L.fails) reached.push({ i: L.b, t: L.lit, ring: !!L.closing }); });
    reached.forEach((R, j) => burst(`raiko reach ${j}`, lift(weaponAt(S.W[R.i], s, T)), s - R.t, CornerFlash * (R.ring ? 1.6 : 1),
      R.ring ? .6 : .38, look, R.ring ? 7 : 5, 140 + j * 9));

    // The weapons: held over Amenoyodomi's mark, then flying on charged after a let-go.
    S.W.forEach((w, i) => {
      const g = weaponAt(w, s, T), deg = Math.atan2(w.dir.z, w.dir.x) * Mathf.Rad2Deg;
      const flying = s >= T.letGo && s < w.stopT, corner = T.links.some(L => (L.a === i || L.b === i) && live(L, s));
      if (s < T.letGo) {
        const pulse = .9 + .1 * Math.sin(s * 5 + i);
        draw(heldRing, g.x, Floor + .02, g.z, HeldRing * pulse, HeldRing * pulse, 0, Lavender.withAlpha(.3));
      }
      if (w.kind === 'fuma') {
        const turn = turnAt(w, s, T);
        if (s >= w.stopT) {
          sprite(g, FumaSize, FumaSize, Color.white, fumaMat, Floor + .05, turn);
          burst('raiko fuma lands', at(g, 0, .1), s - w.stopT, .5, .7, look, 6, 500);
          return;
        }
        const h = flying ? lerp(Hold, 0, clamp((s - T.letGo) / (w.stopT - T.letGo))) : Hold, c = at(g, 0, 0, h);
        sprite({ x: g.x + sun.x * h, z: g.z + sun.z * h }, 1, 1, ShadowInk.withAlpha(strength * .7), soft, shadowLayer);
        sprite(c, FumaSize, FumaSize, Color.white, fumaMat, projectileLayer, turn);
        if (flying) {
          for (let k = 1; k <= 2; k++) sprite(c, FumaSize, FumaSize, Color.white.withAlpha(.45 - .15 * k), fumaGhost, projectileLayer - .001 * k, turn - 22 * k);
          for (let k = 0; k < 5; k++) {
            const sd = step * 41 + k * 7, ang = rand(sd) * Math.PI * 2, r0 = .5 * FumaSize, r1 = r0 + .15 + .25 * rand(sd + 1);
            boltLine(`raiko fuma rim ${k}`, boltPts({ x: c.x + Math.cos(ang) * r0, z: c.z + Math.sin(ang) * r0 }, { x: c.x + Math.cos(ang) * r1, z: c.z + Math.sin(ang) * r1 }, .05, sd + 2, .07),
              .03, .9, look, Y + .031 + k * .0003, 'end');
          }
          boltLine('raiko fuma trail', boltPts(at({ x: g.x - w.dir.x * 1.1, z: g.z - w.dir.z * 1.1 }, 0, 0, h), c, .1, step * 43, .15), .04, .85, look, Y + .03, 'both', .6);
        } else if (corner) {
          // Part of the net: arcs jump between the blade tips as it turns.
          for (let k = 0; k < 4; k++) {
            const A = lift(blade(g, turn, k, TipR)), B = lift(blade(g, turn, (k + 1) % 4, TipR));
            boltLine(`raiko fuma arc ${k}`, boltPts(A, B, .09, step * 37 + k * 5, .16), .03, .8, look, Y + .031 + k * .0003, 'none', k ? 0 : .5);
          }
          T.links.forEach(L => {
            if (!live(L, s) || (L.a !== i && L.b !== i)) return;
            spark(lift(joint(S, T, i, L.a === i ? L.b : L.a, s)), .2 * (.75 + .25 * Math.sin(s * 37 + i * 2)), look, .9);
          });
        }
        return;
      }
      if (s < w.stopT) {
        sprite({ x: g.x + sun.x * Hold, z: g.z + sun.z * Hold }, .1, .5, ShadowInk.withAlpha(strength * .8), soft, shadowLayer, 90 - deg);
        sprite(lift(g), KunaiSize, KunaiSize, Color.white, kunaiMat, projectileLayer, 90 - deg);
        if (corner) spark(lift(g), .2 * (.75 + .25 * Math.sin(s * 37 + i * 2)), look, .9);
        if (flying) {
          const tail = lift({ x: g.x - w.dir.x * .8, z: g.z - w.dir.z * .8 });
          boltLine(`raiko trail ${i}`, boltPts(tail, lift(g), .07, step * 29 + i * 3, .13), .035, .9, look, Y + .03, 'both', .6);
          spark(lift(g), .16, look, .8);
        }
      } else if (w.hit) stuckKunai(pawnAt(S.pawns[w.hit.pawn], s), deg, 0);
      else sprite(g, .62, .62, Color.white, kunaiMat, Floor + .06, 90 - deg);
    });

    // The pawns: caught, held, let go; a scorch stays where each one was held.
    S.pawns.forEach((P, k) => {
      const q = pawnAt(P, s), key = `raiko pawn ${k}`, held = s >= P.caught;
      if (held) {
        const cq = pawnAt(P, P.caught);
        sprite(at(cq, 0, .02), .78, .46, Ink.withAlpha(.34 * smooth((Math.min(s, T.netEnd) - P.caught) / 1.5)), soft, Floor + .012);
      }
      if (P.kind === 'mech') mechFigure(q, sun, strength);
      else figure(q, P.kind === 'ally' ? Ally : EnemyColour, 1, 0, sun, strength);
      if (P.kind === 'shield') { if (!held) bubble(q); else bubbleBreak(q, s - P.caught); }
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
