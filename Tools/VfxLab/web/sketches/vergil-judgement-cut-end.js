// Judgement Cut End — ability proposal for the Vergil kit (a held katana), not the game. Nothing in
// Source/RimArt draws this yet. The kit's ultimate.
//
// What it is for (proposed, none of it agreed; every number is a placeholder and will be an XML field).
//   No target: centred on the caster. Warm-up 1.0 s (hand on the hilt). Every hostile pawn within
//   10 cells that has line of sight to the caster is marked. The caster is gone for 1.5 s and cannot
//   be targeted; marked pawns are stunned from that moment until the click. The caster comes back on
//   the cell they left, kneeling, and sheathes for 0.8 s. When the guard meets the scabbard each
//   marked pawn takes 4 hits of 10 Cut. Allies, pawns behind a wall and pawns that walk into the
//   radius after the warm-up are not marked. Cooldown 1 day.
//   Added since the first table: the stun. Without it the pawns keep walking for 2.3 s under cuts
//   that are drawn standing still, and the picture and the rule disagree.
//
// Order, with the default timings:
//   0.00  6 raiders walk in; an ally stands inside the radius; 1 raider is outside it; with the wall
//         on, 1 raider is inside it behind a wall
//   0.50  warm-up 1.0 s: the right hand goes to the hilt, a blue aura stands up round the caster, the
//         floor ring grows out to the true radius, short lights rise out of the floor inside it
//   1.50  gone: the caster thins to a vertical line. The area inside the ring goes dark blue and the
//         marked raiders stop and turn blue. 14 cuts are drawn one after another over 1.25 s, each
//         a straight chord across the whole ring drawn end to end in 0.07 s, with the caster seen
//         for 0.22 s at the far end of each. The first 6 each pass through one marked raider and
//         leave a glint on it. The cuts stay in the air.
//   3.00  back: the caster is on their own cell, kneeling, back turned, and slides the blade into the
//         upright scabbard. The pieces between the cuts show as panes of glass: a faint blue face, a bright
//         edge toward the sun, a dark edge away from it, each pane 0.03 to 0.08 cells off its place.
//   3.80  the click: a glint at the scabbard mouth and a ring front that runs out to the true radius
//         in 0.15 s. The cuts go white and are gone in 0.25 s. The area breaks along them: every
//         pane flashes, slides outward 0.1 to 0.5 cells, turns up to 11 degrees, shrinks to 70 % (it
//         falls away from the camera) and fades over 0.6 s. Each marked raider takes 4 short cuts 0.08 s apart and goes down.
//   4.40  the dark lifts. The raiders stay down, thin scars stay on the floor along the cuts, the
//         caster stands up. The raider who was outside has walked in unhurt; so have the ally and
//         the one behind the wall.
//
// Drawing: chords, level circles and flat polygons about one point, and no aim, so no per-facing
// method. The pieces are the convex polygons left when the ring is split by the cuts
// (lib/vergil.js shatter); their meshes are rebuilt only when the radius, the cut count or the
// sun changes. No distortion shader: RimWorld's is a ripple and cannot shift the picture per pane. One
// built-in distortion wave at the click, caster to the ring in 0.15 s, is an option for the C# port. Cuts are light: a dark slit, a wide additive glow, a thin white core. The kneel is
// one view, a stand-in; in game it needs a pose per facing. Pawns and the wall are stand-ins.
import { Color, Mathf, Meshes } from '../js/engine.js';
import { draw, mesh } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, rand } from './lib/six-paths-impact.js';
import {
  Blue, Deep, Ice, Void, White, EnemyColour, Ally, pawn, carrier, scabbardMouth, cut, afterimage, hitCut, shatter, chord,
  ringAt, glint, aura, streak, whiteGlow, wallCell, smooth, clamp,
} from './lib/vergil.js';

const disc = Meshes.disc(64, 'judgement cut end disc');
// Decided values. The panel keeps only what is still being tuned.
const Lead = .5, Tail = 1.8, Sweep = .07, FirstCut = .1, LastCut = .15, Dark = .55, Front = .15, CutsGone = .25;
const EdgeWidth = .07, EdgeFacing = .3, Ajar = [.03, .08], FallTo = .7;   // pane edges; how far a pane sits off its place before the click; its size when it is gone
const Hits = 4, HitGap = .08, WalkIn = 1.1, Motes = 40, StandUp = .7;
// Raiders as [direction from the caster (degrees), distance when the caster vanishes (cells)].
const Raiders = [[25, 3.2], [80, 5.6], [-35, 6.4], [140, 4.3], [-110, 7.4], [172, 8.4]];
const OutsideAt = [58, 2.5], AllyAt = [-72, 2.6], WallAt = [212, 4.6];   // outside: degrees, cells past the radius

function times(p) {
  const cast = Lead, vanish = cast + p.warm, back = vanish + p.gone, click = back + p.sheathe;
  return { cast, vanish, back, click, end: click + Tail };
}
const polar = (deg, d) => ({ x: Math.cos(deg * Mathf.Deg2Rad) * d, z: Math.sin(deg * Mathf.Deg2Rad) * d });

// The cuts, relative to the caster: the first ones each pass through a marked raider's chest, the
// rest cross the ring anywhere. order is when each is drawn.
function layout(p) {
  const marked = Raiders.filter(([, d]) => d <= p.radius).map(([deg, d]) => polar(deg, d));
  const count = Math.round(p.cuts), cuts = [];
  for (let k = 0; k < count; k++) {
    // Every cut runs across the line from the caster to its point, within 40 degrees, so none crosses the caster's cell.
    const m = marked[k], far = (.2 + .5 * rand(k * 5 + 3)) * p.radius, turn = (k * 137 + rand(k * 3 + 9) * 60) * Mathf.Deg2Rad;
    const q = m ? { x: m.x + (rand(k * 13 + 1) - .5) * .2, z: m.z + .3 } : { x: Math.cos(turn) * far, z: Math.sin(turn) * far };
    const ang = Math.atan2(q.z, q.x) + Math.PI / 2 + (rand(k * 11 + 4) - .5) * 80 * Mathf.Deg2Rad, d = { x: Math.cos(ang), z: Math.sin(ang) };
    const ends = chord(q, d, p.radius);
    cuts.push({ q, d, a: ends[k % 2], b: ends[1 - k % 2], victim: m ? k : -1, sort: rand(k + 50) });
  }
  [...cuts].sort((m, n) => m.sort - n.sort).forEach((c, i) => { c.order = i; });
  return { marked, cuts };
}

// Pieces between the cuts, as panes of glass. Each has a face mesh and two edge meshes: the edges
// that face the light (drawn bright) and the ones that face away (drawn dark), each an EdgeWidth
// strip just inside the outline. Rebuilt only when the radius, the cut count or the sun changes.
let built = { key: '', pieces: [], cuts: [], marked: [] };
function pieces(p, sun) {
  const sunLength = Math.hypot(sun.x, sun.z) || 1, light = { x: -sun.x / sunLength, z: -sun.z / sunLength };
  const key = `${p.radius}|${Math.round(p.cuts)}|${light.x.toFixed(2)}|${light.z.toFixed(2)}`;
  if (built.key !== key) {
    const { marked, cuts } = layout(p), list = shatter(p.radius, cuts);
    list.forEach((piece, i) => {
      const vertices = [0, 0], tri = [], n = piece.points.length, edges = { lit: [[], []], dim: [[], []] };
      piece.points.forEach(v => vertices.push(v.x, v.z));
      for (let j = 0; j < n; j++) {
        tri.push(0, 1 + j, 1 + (j + 1) % n);
        const v = piece.points[j], w = piece.points[(j + 1) % n], dx = w.x - v.x, dz = w.z - v.z, len = Math.hypot(dx, dz);
        if (len < .05) continue;
        const nx = dz / len, nz = -dx / len, facing = nx * light.x + nz * light.z;   // outward normal of a counter-clockwise outline
        if (Math.abs(facing) < EdgeFacing) continue;
        const [verts, tris] = facing > 0 ? edges.lit : edges.dim, at = verts.length / 2;
        verts.push(v.x, v.z, w.x, w.z, w.x - nx * EdgeWidth, w.z - nz * EdgeWidth, v.x - nx * EdgeWidth, v.z - nz * EdgeWidth);
        tris.push(at, at + 1, at + 2, at, at + 2, at + 3);
      }
      mesh(`judgement cut end piece ${i}`).setFlat(vertices, tri);
      piece.lit = edges.lit[0].length > 0; piece.dim = edges.dim[0].length > 0;
      if (piece.lit) mesh(`judgement cut end piece ${i} lit`).setFlat(...edges.lit);
      if (piece.dim) mesh(`judgement cut end piece ${i} dim`).setFlat(...edges.dim);
    });
    built = { key, pieces: list, cuts, marked };
  }
  return built;
}

export default {
  kit: 'Vergil', label: 'Judgement Cut End (sketch)',
  params: {
    wall: { label: 'One raider stands behind a wall (no line of sight)', value: true, group: 'Showcase' },
    radius: P('Radius (cells)', 10, 5, 14, .5, 'Shape'),
    cuts: P('Cuts', 14, 8, 22, 1, 'Shape'),
    push: P('Pieces slide outward (cells)', .3, 0, 1, .05, 'Shape'),
    warm: P('Warm-up (hand on the hilt)', 1, .3, 2, .05, 'Timing (s)'),
    gone: P('Gone (the cuts are drawn)', 1.5, .6, 3, .05, 'Timing (s)'),
    sheathe: P('Sheathing', .8, .3, 2, .05, 'Timing (s)'),
    fade: P('Pieces fade over', .6, .2, 1.5, .05, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Raiders close in', t: 0 }, { name: 'Hand on the hilt', t: t.cast }, { name: 'Gone / the cuts', t: t.vanish },
      { name: 'Kneel and sheathe', t: t.back }, { name: 'Click: the cuts land', t: t.click }];
  },
  events(p) {
    const t = times(p);
    return [{ t: t.vanish, type: 'shake', value: .03 }, { t: t.click, type: 'shake', value: .14 },
      { t: t.vanish, type: 'sound', def: 'AG_Vergil_CutEndVanish' }, { t: t.click, type: 'sound', def: 'AG_Vergil_SheathClick' }];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const { pieces: shards, cuts, marked } = pieces(p, sun), R = p.radius;
    const world = q => ({ x: o.x + q.x, z: o.z + q.z });
    const sinceClick = s - t.click, stopped = s >= t.vanish && sinceClick < 0;
    const dark = smooth((s - t.vanish) / .12) * (1 - smooth(sinceClick / .4));
    const startOf = c => t.vanish + FirstCut + c.order / Math.max(1, cuts.length - 1) * (p.gone - FirstCut - LastCut - Sweep);

    // --- the wall ---------------------------------------------------------------------------------------------------
    if (p.wall) for (let k = -1; k <= 1; k++) {
      const c = polar(WallAt[0], WallAt[1]), r = WallAt[0] * Mathf.Deg2Rad;
      wallCell(world({ x: c.x - Math.sin(r) * k, z: c.z + Math.cos(r) * k }), WallAt[0]);
    }

    // --- the floor: the true radius, scars left along the cuts ---------------------------------------------------------
    if (s >= t.cast) {
      const grown = smooth((s - t.cast) / (p.warm * .8)), left = 1 - smooth((sinceClick - .3) / .6);
      ringAt(o, R * grown, Blue.withAlpha(.6 * left), Floor + .02);
    }
    if (sinceClick >= 0) cuts.forEach((c, k) =>
      streak(`judgement cut end scar ${k}`, world(c.a), world(c.b), .05, Void.withAlpha(.5 - .25 * smooth(sinceClick / Tail)), undefined, Floor + .01, 4));

    // --- pawns, north first ---------------------------------------------------------------------------------------------
    const figures = [];
    marked.forEach((m, i) => {
      const [deg, d] = Raiders.filter(([, far]) => far <= R)[i], before = Math.max(0, t.vanish - s) * WalkIn;
      figures.push({ pos: world(polar(deg, d + before)), marked: true, i });
    });
    figures.push({ pos: world(polar(OutsideAt[0], Math.max(1.6, R + OutsideAt[1] + (t.vanish - s) * WalkIn))), i: 20 });
    figures.push({ pos: world(polar(AllyAt[0] + s * 4, AllyAt[1])), ally: true, i: 21 });
    if (p.wall) figures.push({ pos: world(polar(WallAt[0] + Math.sin(s * .9) * 6, WallAt[1] + 1.1)), i: 22 });
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      if (!g.marked) { pawn(g.pos, g.ally ? Ally : EnemyColour, sun, strength, { tint: Deep, tintAmount: .25 * dark }); return; }
      const first = t.click + .04 + g.i * .025, last = first + (Hits - 1) * HitGap, down = s >= last + .1;
      const shake = s >= first && !down ? Math.sin(s * 90 + g.i) * .04 : 0;
      pawn({ x: g.pos.x + shake, z: g.pos.z }, EnemyColour, sun, strength, { lie: down, tint: stopped ? Deep : White, tintAmount: stopped ? .5 * dark : .7 * clamp(1 - (s - last) / .2) * (s >= first ? 1 : 0) });
    });

    // --- the caster -------------------------------------------------------------------------------------------------------
    const w = clamp((s - t.cast) / p.warm);
    if (s < t.vanish) {
      if (s >= t.cast) aura('judgement cut end', o, s, w, Blue);
      carrier(o, sun, strength, { hand: smooth(w * 3) });
    } else if (s < t.back) {
      const gone = clamp((s - t.vanish) / .08);
      carrier(o, sun, strength, { alpha: 1 - gone, tint: Ice, tintAmount: gone });
      streak('judgement cut end leave', { x: o.x, z: o.z - .2 }, { x: o.x, z: o.z + 1.4 + gone }, .22 * (1 - clamp((s - t.vanish) / .2)), White.withAlpha(1 - clamp((s - t.vanish) / .2)), whiteGlow, Y + .05, 6);
    } else {
      const v = clamp((s - t.back) / p.sheathe), home = v < .9 ? .88 * smooth(v / .9) : .88 + .12 * (v - .9) / .1;
      const kneeling = sinceClick < StandUp;
      sprite({ x: o.x, z: o.z + .3 }, 1.8, 1.8, Blue.withAlpha(.35 * dark), glow, Y - .04);
      carrier(o, sun, strength, { pose: kneeling ? 'kneel' : 'stand', sheathe: home, alpha: clamp((s - t.back) / .1), tint: Ice, tintAmount: 1 - clamp((s - t.back) / .2) });
    }

    // --- warm-up: lights rise out of the floor inside the ring ---------------------------------------------------------------
    if (s >= t.cast && s < t.vanish) for (let i = 0; i < Motes; i++) {
      const v = (w * 2.2 + rand(i)) % 1, at = polar(rand(i + 60) * 360, Math.sqrt(rand(i + 120)) * R * smooth(w * 1.25)), base = world(at);
      streak(`judgement cut end mote ${i}`, { x: base.x, z: base.z + v * .7 }, { x: base.x, z: base.z + v * .7 + .3 }, .05, Ice.withAlpha(.8 * w * Math.sin(v * Math.PI)), whiteGlow, Y + .01, 3);
    }

    // --- the dark inside the ring, over the pawns and under the cuts -----------------------------------------------------------
    draw(disc, o.x, Y - .05, o.z, R, R, 0, Void.withAlpha(Dark * dark));

    // --- the pieces between the cuts: panes of glass, ajar while the blade goes home, breaking on the click -------------------
    if (s >= t.back && sinceClick < p.fade) {
      const u = clamp(sinceClick / p.fade), show = smooth((s - t.back) / (p.sheathe * .6)), out = smooth(clamp(u * 1.6)), gone = Math.pow(1 - u, 1.5);
      shards.forEach((piece, i) => {
        const far = Math.hypot(piece.centre.x, piece.centre.z) || 1, slide = sinceClick < 0 ? 0 : p.push * (.35 + 1.3 * rand(i + 31)) * out;
        const ajar = (Ajar[0] + (Ajar[1] - Ajar[0]) * rand(i + 90)) * show, lean = rand(i + 91) * Math.PI * 2;
        const at = world({ x: piece.centre.x * (1 + slide / far) + Math.cos(lean) * ajar, z: piece.centre.z * (1 + slide / far) + Math.sin(lean) * ajar });
        const facet = .05 + .2 * rand(i + 3), size = 1 - (1 - FallTo) * out, turn = (rand(i + 7) - .5) * (3 * show + 22 * out);
        const face = sinceClick < 0 ? facet * .8 * show : (facet * 1.6 + .4 * clamp(1 - u * 7)) * gone, edge = sinceClick < 0 ? show : gone;
        draw(mesh(`judgement cut end piece ${i}`), at.x, Y + .02, at.z, size, size, turn, (sinceClick < 0 ? Blue : Color.Lerp(Ice, Blue, clamp(u * 3))).withAlpha(face), whiteGlow);
        if (piece.dim) draw(mesh(`judgement cut end piece ${i} dim`), at.x, Y + .021, at.z, size, size, turn, Void.withAlpha(.75 * edge));
        if (piece.lit) draw(mesh(`judgement cut end piece ${i} lit`), at.x, Y + .022, at.z, size, size, turn, Ice.withAlpha(.85 * edge), whiteGlow);
      });
    }

    // --- the cuts ---------------------------------------------------------------------------------------------------------------
    if (s >= t.vanish && sinceClick < CutsGone) cuts.forEach((c, k) => {
      const start = startOf(c), age = s - start;
      if (age < 0) return;
      const hot = sinceClick >= 0 ? 1 : clamp(1 - age / .18) * .8, alpha = sinceClick >= 0 ? 1 - sinceClick / CutsGone : 1;
      const tip = cut(`judgement cut end cut ${k}`, world(c.a), world(c.b), clamp(age / Sweep), alpha, { hot, flicker: .85 + .15 * Math.sin(s * 40 + k * 1.7) });
      if (age < Sweep && tip) glint(`judgement cut end tip ${k}`, tip, .35, 1, Ice, 45);
      afterimage(`judgement cut end ghost ${k}`, world(c.b), world(c.a), age - Sweep);
      if (c.victim >= 0) {
        const chest = world({ x: marked[c.victim].x, z: marked[c.victim].z + .3 }), since = age - Sweep * .5;
        if (since >= 0 && sinceClick < 0) glint(`judgement cut end mark ${k}`, chest, .16 + .3 * clamp(1 - since / .15), .9, White, 45 + k * 20);
      }
    });

    // --- the click ------------------------------------------------------------------------------------------------------------------
    if (sinceClick >= 0) {
      const mouth = scabbardMouth(o), f = clamp(sinceClick / .3);
      glint('judgement cut end click', mouth, .2 + .5 * (1 - f), 1 - f, White, 0);
      if (sinceClick < Front * 2) ringAt(o, R * smooth(sinceClick / Front), Ice.withAlpha(.45 * (1 - sinceClick / (Front * 2))), Y + .06, true, whiteGlow);
      sprite(o, R * 2.4, R * 2.4, Ice.withAlpha(.3 * (1 - clamp(sinceClick / .2))), glow, Y + .055);
      marked.forEach((m, i) => {
        const first = t.click + .04 + i * .025;
        for (let h = 0; h < Hits; h++) hitCut(`judgement cut end hit ${i} ${h}`, world(m), (h * 47 + i * 31 + 20) % 180 - (h % 2 ? 0 : 90), s - first - h * HitGap);
      });
    }
  },
};
