// Judgement Cut — ability proposal for the Vergil kit (a held katana), not the game. Nothing in
// Source/RimArt draws this yet. The kit's everyday ranged attack.
//
// What it is for (agreed as a direction 2026-09-20; every number is a placeholder and will be an XML field).
//   Target a cell within 18 cells with line of sight. Warm-up 0.6 s in a sheathed stance. A sphere of
//   radius 1.9 opens on the cell. Every pawn inside, allies included, takes 5 hits of 7 Cut at 50 %
//   armour penetration over 0.5 s. Nothing flies from the caster to the cell: the cut happens there.
//   Cooldown 12 s.
//   It differs from Judgement Cut End: a small aimed area with many short-lived cuts inside a ball,
//   no panes of glass, no vanish, no stun. Judgement Cut End keeps the long straight chords and the
//   shatter.
//
// Order, with the default timings:
//   0.00  the caster stands with the katana sheathed. At the target: 3 raiders inside the radius, an
//         ally inside it, 1 raider 2.7 cells from its centre
//   0.40  warm-up 0.6 s: the right hand goes to the hilt, a thin aura stands up round the caster. On
//         the target cell a blue floor ring grows to the true radius and 12 short lights rise out of
//         the floor inside it
//   1.00  the draw: a glint at the hilt and one light arc in front of the caster, 0.12 s. At the
//         target the ball opens in 0.08 s: dark inside with a soft edge, a blue shell that brightens
//         toward the rim, a pale patch on the upper left, a thin rim that beats, a shadow on the
//         floor under it. The floor ring hides while the ball is open. 24 cuts appear one after
//         another, each a thin straight chord right across the ball that pokes 0.12 to 0.3 cells out
//         of the rim, drawn in 0.04 s, held 0.07 s, gone in 0.06 s, so about 9 show at once. 5 damage
//         ticks 0.1 s apart: every pawn inside gets a short hot cut across the chest, flashes white,
//         and keeps a red slit per hit
//   1.50  the ball closes in 0.16 s: it breaks along its last 5 cuts into about 14 dark pieces that
//         fall to its centre, turning and shrinking; then a glint, a thin ring front out to 1.3 x
//         the radius, 10 dust puffs off the floor round the true radius. A glint at the caster's
//         hilt: the guard meets the scabbard as the ball closes. The hand leaves the hilt
//   1.60  what stays: the red slits on the 3 raiders and on the ally, none on the raider outside;
//         short dark scars on the floor along the cuts; the floor ring fades over 0.4 s
//
// Drawing: a level disc, level rings and lines inside one area, so no per-facing method. The draw arc
// at the caster lies flat and turns with the aim. Cuts are light: a dark slit, a wide additive glow, a
// thin white core. The ball is two soft round textures (lib/vergil.js sphere), lab textures that still
// have to become PNGs. No distortion shader; the dark inside stands in for the warp the source shows. One
// built-in distortion pulse on the sphere is an option for the C# port. Pawns are stand-ins.
import { Color, Mathf, MeshPool } from '../js/engine.js';
import { draw, mesh } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import {
  Blue, Deep, Ice, Void, White, Dust, EnemyColour, Ally, pawn, pawnLayer, carrier, sphere, cut, arcCut, hitCut, shatter, chord, ringAt, glint, aura, streak, whiteGlow, smooth, clamp,
} from './lib/vergil.js';

// Decided values. The panel keeps only what is still being tuned.
const Lead = .4, Tail = 1.2, Open = .08, Close = .16, Sweep = .04, Hold = .07, Gone = .06, Hits = 5, DrawArc = .12, Motes = 12, RingFade = .4;
const Over = [.12, .3], Breaks = 5, Puffs = 10;   // how far a cut pokes out of the ball; how many cuts the ball breaks along; dust puffs when it closes
const Wound = new Color(.55, .05, .05);
// Pawns at the target as [east, north] of the target cell, in cells.
const Inside = [[.6, .3], [-.9, -.5], [.2, -1.2]], AllyAt = [-.5, 1.1], OutsideAt = [-2.5, -1];

function times(p) {
  const cast = Lead, open = cast + p.warm, close = open + p.burst;
  return { cast, open, close, end: close + Close + Tail };
}

// One cut, relative to the centre of the ball: a straight chord right across it that pokes out of
// the rim at both ends. q is a point on it, d its direction, a and b its ends.
function cutLine(k, radius) {
  const turn = (k * 137 + rand(k + 5) * 50) * Mathf.Deg2Rad, off = Math.sqrt(rand(k * 3 + 1)) * radius * .6;
  const q = { x: Math.cos(turn) * off, z: Math.sin(turn) * off }, ang = (k * 67 + rand(k * 7 + 2) * 60) * Mathf.Deg2Rad, d = { x: Math.cos(ang), z: Math.sin(ang) };
  const ends = chord(q, d, radius + Over[0] + (Over[1] - Over[0]) * rand(k * 11 + 3));
  return { q, d, a: ends[k % 2], b: ends[1 - k % 2] };
}

// The pieces the ball breaks into when it closes: the disc split along the last Breaks cuts. Rebuilt,
// with their meshes, only when the radius or the cut count changes.
let built = { key: '', pieces: [] };
function pieces(radius, count) {
  const key = `${radius}|${count}`;
  if (built.key !== key) {
    const list = shatter(radius, Array.from({ length: Math.min(Breaks, count) }, (_, i) => cutLine(count - 1 - i, radius)), 32);
    list.forEach((piece, i) => {
      const vertices = [0, 0], tri = [], n = piece.points.length;
      piece.points.forEach(v => vertices.push(v.x, v.z));
      for (let j = 0; j < n; j++) tri.push(0, 1 + j, 1 + (j + 1) % n);
      mesh(`judgement cut piece ${i}`).setFlat(vertices, tri);
    });
    built = { key, pieces: list };
  }
  return built.pieces;
}

export default {
  kit: 'Vergil', label: 'Judgement Cut (sketch)',
  params: {
    aim: P('Aim (degrees, 0 = east)', 20, 0, 360, 5, 'Target'),
    distance: P('Distance (cells)', 8, 3, 18, .5, 'Target'),
    radius: P('Radius (cells)', 1.9, 1, 3.5, .1, 'Shape'),
    cuts: P('Cuts', 24, 10, 40, 1, 'Shape'),
    warm: P('Warm-up (sheathed stance)', .6, .2, 1.5, .05, 'Timing (s)'),
    burst: P('The sphere stays open', .5, .3, 1.5, .05, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Sheathed', t: 0 }, { name: 'Hand on the hilt', t: t.cast }, { name: 'The draw / the sphere', t: t.open }, { name: 'Closes', t: t.close }];
  },
  events(p) {
    const t = times(p);
    return [{ t: t.open, type: 'shake', value: .06 }, { t: t.close, type: 'shake', value: .08 }, { t: t.open, type: 'sound', def: 'AG_Vergil_JudgementCut' }];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const r = p.aim * Mathf.Deg2Rad, R = p.radius, count = Math.round(p.cuts);
    const target = { x: o.x + Math.cos(r) * p.distance, z: o.z + Math.sin(r) * p.distance }, centre = { x: target.x, z: target.z + .3 };
    const at = q => ({ x: target.x + q[0], z: target.z + q[1] });
    const since = s - t.open, sinceClose = s - t.close, tick = p.burst / Hits;
    const size = since < 0 ? 0 : sinceClose < 0 ? smooth(since / Open) * (1 + .08 * Math.sin(clamp(since / (Open * 2)) * Math.PI)) : 1 - smooth(sinceClose / Close);

    // --- the floor: the true radius, scars along the cuts ---------------------------------------------------------------
    if (s >= t.cast) {
      // The ring marks the true radius before and after; while the ball is open the ball itself does, with a shadow under it.
      const grown = smooth((s - t.cast) / (p.warm * .7)), left = 1 - smooth((sinceClose - Close) / RingFade);
      const covered = since < 0 ? 0 : sinceClose < 0 ? smooth(since / Open) : 1 - smooth(sinceClose / Close);
      ringAt(target, R * grown, Blue.withAlpha(.7 * left * (1 - covered)), Floor + .02);
      sprite(target, R * 2.5, R * 1.7, Void.withAlpha(.4 * covered), soft, Floor + .03);
    }
    if (sinceClose >= 0) for (let k = 0; k < count; k += 2) {   // every second cut leaves a scar: the middle half of its chord
      const c = cutLine(k, R), mix = u => ({ x: target.x + c.a.x + (c.b.x - c.a.x) * u, z: target.z + c.a.z + (c.b.z - c.a.z) * u });
      streak(`judgement cut scar ${k}`, mix(.25), mix(.75), .05, Void.withAlpha(.45 - .2 * smooth(sinceClose / Tail)), undefined, Floor + .01, 4);
    }

    // --- pawns, north first ---------------------------------------------------------------------------------------------------
    const figures = [{ pos: o, caster: true }, ...Inside.map((q, i) => ({ pos: at(q), hit: Math.hypot(q[0], q[1]) <= R, i })),
      { pos: at(AllyAt), hit: Math.hypot(...AllyAt) <= R, ally: true, i: 10 }, { pos: at(OutsideAt), hit: Math.hypot(...OutsideAt) <= R, i: 11 }];
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      if (g.caster) {
        const w = clamp((s - t.cast) / p.warm), hand = smooth(w * 3) * (1 - smooth((sinceClose - .1) / .3));
        if (s >= t.cast && sinceClose < 0) aura('judgement cut', o, s, .5 * w * (1 - clamp(since / .3)), Blue);
        carrier(o, sun, strength, { hand });
        return;
      }
      const landed = g.hit ? Math.max(0, Math.min(Hits, Math.floor(since / tick + .5))) : 0, last = t.open + (landed - .5) * tick;
      const flash = landed > 0 ? .7 * clamp(1 - (s - last) / .1) : 0, flinch = g.hit && since >= 0 && sinceClose < .1 ? Math.sin(s * 95 + g.i) * .035 : 0;
      const pos = { x: g.pos.x + flinch, z: g.pos.z };
      pawn(pos, g.ally ? Ally : EnemyColour, sun, strength, { tint: White, tintAmount: flash });
      for (let h = 0; h < landed; h++)
        draw(MeshPool.plane10, pos.x + (rand(g.i * 9 + h) - .5) * .16, pawnLayer + .02 + h * .0005, pos.z + .1 + .1 * h, .24, .035, (h * 53 + g.i * 29) % 140 - 70, Wound.withAlpha(.85));
    });

    // --- warm-up: lights rise out of the floor inside the ring --------------------------------------------------------------------
    if (s >= t.cast && since < 0) {
      const w = (s - t.cast) / p.warm;
      for (let i = 0; i < Motes; i++) {
        const v = (w * 1.8 + rand(i)) % 1, ang = rand(i + 40) * Math.PI * 2, far = Math.sqrt(rand(i + 80)) * R * .9;
        const base = { x: target.x + Math.cos(ang) * far, z: target.z + Math.sin(ang) * far + v * .6 };
        streak(`judgement cut mote ${i}`, base, { x: base.x, z: base.z + .28 }, .05, Ice.withAlpha(.85 * w * Math.sin(v * Math.PI)), whiteGlow, Y + .01, 3);
      }
      sprite(centre, R * 1.2 * w, R * 1.2 * w, Deep.withAlpha(.35 * w), glow, Y + .005);
    }

    // --- the draw, at the caster: a glint at the hilt and one flat light arc toward the target ------------------------------------------
    if (since >= 0 && since < DrawArc) {
      const u = since / DrawArc, pts = [];
      for (let j = 0; j <= 10; j++) { const a = r + (j / 10 - .5) * 110 * Mathf.Deg2Rad; pts.push({ x: o.x + Math.cos(a) * .95, z: o.z + .3 + Math.sin(a) * .95 }); }
      arcCut('judgement cut draw', pts, clamp(u * 2.5), 1 - u * u, { width: .05, hot: 1 });   // hot: light only, no dark slit
      glint('judgement cut hilt', { x: o.x - .02, z: o.z + .46 }, .35 * (1 - u) + .1, 1 - u, White, 20);
    }

    // --- the ball: whole while it is open, in pieces that fall to its centre when it closes ----------------------------------------------
    if (size > 0) sphere(centre, R * size, s, sinceClose < 0 ? 1 : size, sinceClose < 0 ? .45 : 0);
    if (sinceClose >= 0 && sinceClose < Close) {
      const u = sinceClose / Close, pull = smooth(u);
      pieces(R, count).forEach((piece, i) => {
        const x = centre.x + piece.centre.x * (1 - pull), z = centre.z + piece.centre.z * (1 - pull), scale = .92 * (1 - .8 * u), turn = (rand(i + 7) - .5) * 70 * u;
        draw(mesh(`judgement cut piece ${i}`), x, Y + .016, z, scale, scale, turn, Void.withAlpha(.55 * (1 - .4 * u)));
        draw(mesh(`judgement cut piece ${i}`), x, Y + .0165, z, scale, scale, turn, Blue.withAlpha(.3 * (1 - u)), whiteGlow);
      });
    }

    // --- the cuts: thin straight chords across the ball, a few on screen at a time ----------------------------------------------------------
    if (since >= 0 && sinceClose < Close) for (let k = 0; k < count; k++) {
      const age = since - k / count * Math.max(.05, p.burst - Hold - Sweep);
      if (age < 0 || age >= Sweep + Hold + Gone) continue;
      const c = cutLine(k, R), place = q => ({ x: centre.x + q.x, z: centre.z + q.z });
      cut(`judgement cut ${k}`, place(c.a), place(c.b), clamp(age / Sweep), 1 - clamp((age - Sweep - Hold) / Gone), { width: .03 * (.35 + .65 * clamp(age / Sweep)), hot: clamp(1 - age / .06) });   // thin while it is still short, or it shows as a leaf
    }
    if (since >= 0) figures.forEach(g => {
      if (!g.hit || g.caster) return;
      for (let h = 0; h < Hits; h++) hitCut(`judgement cut hit ${g.i} ${h}`, g.pos, (h * 67 + g.i * 41) % 180, since - (h + .5) * tick);
    });

    // --- it closes -----------------------------------------------------------------------------------------------------------------------------
    if (sinceClose >= 0 && sinceClose < .35) {
      const u = sinceClose / .35;
      glint('judgement cut close', centre, .25 + .6 * clamp(sinceClose / Close) * (1 - u), 1 - u, White, 45);
      if (sinceClose >= Close) ringAt(centre, R * (.2 + 1.1 * smooth((sinceClose - Close) / .2)), Ice.withAlpha(.6 * (1 - u)), Y + .03, false, whiteGlow);
      glint('judgement cut sheathe', { x: o.x - .02, z: o.z + .46 }, .3 * (1 - clamp(sinceClose / .15)), 1 - clamp(sinceClose / .15), White, 20);   // the guard meets the scabbard as the ball closes
    }
    // Dust off the floor round the true radius as it closes.
    const dusty = (sinceClose - Close * .5) / .6;
    if (dusty >= 0 && dusty < 1) for (let i = 0; i < Puffs; i++) {
      const ang = (i / Puffs + rand(i + 60) * .08) * Math.PI * 2, far = R * .85 + (.3 + .5 * rand(i + 70)) * dusty;
      sprite({ x: target.x + Math.cos(ang) * far, z: target.z + Math.sin(ang) * far * .9 + dusty * .25 }, .45 + .6 * dusty, .38 + .5 * dusty, Dust.withAlpha(.4 * Math.sin(dusty * Math.PI)), soft, Y + .004);
    }
  },
};
