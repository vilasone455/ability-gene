// Judgement Cut End v2 — a second picture for the Vergil kit's ultimate. The rule is the first sketch's
// (vergil-judgement-cut-end.js: who is marked, the stun, the shared cuts, the three timings); only the
// picture between the vanish and the click changes.
//
// Why: the first picture draws 14 cuts one after another over 1.25 s, each swept end to end with the
// caster seen at its far end. Devil May Cry 5 (stepped through at 10 % speed, 2026-10-03) and every
// fan version looked at do not: the cuts are whole on their first frame, all of them land inside
// about half a second, and nobody is seen along them.
//
// Checked against the source again 2026-10-03 (this container cannot open the wiki or a video; the
// facts below come from the search snippets of the DMC wiki, the Steam and GameFAQs threads and the
// move's mod pages, and from memory of the Special Edition footage). What the first v2 got wrong:
//   - He is gone only while the cuts land, about half a second, and is kneeling on his cell by the
//     time the last one has. The long part is the sheathe, 1 to 1.5 s, and the frozen pale picture
//     holds while he is seen kneeling in it. The first v2 held the pale picture for 0.9 s with nobody
//     there and then sheathed for 0.8 s, so it looked like two moves.
//   - The cuts are hairlines: a white core thinner than a hand with a faint blue edge, and dozens of
//     them (the wiki: "sliced multiple times in very rapid succession"; the SDT version adds more).
//     The first v2 drew 0.06-cell cores with a 0.5-cell glow, which read as beams, not cuts.
//   - A cut stays a line. The "cracks" (a jagged pale bolt along each cut) read as lightning; the
//     game has none. The picture goes pale and the lines simply stay in it as the edges of the panes.
//   - Every cut is straight (a chord of the sphere), and the red/blue split in the game is a whole-
//     picture effect on the pale frame, not a fringe on a line. The curved cuts are kept behind a
//     slider at 0, and the colour split lives in the cut-glass shader, which pulls red and blue apart
//     per pane.
//   - The darkening and the white flash on the click are real ("before the attack the area around
//     Vergil gets darker"; the click is a full-screen flash) and stay.
//
// Order, with the default timings (times after the vanish are shares of "Gone", so they scale with it):
//   0.00  raiders walk in, as in the first sketch
//   0.50  warm-up 1.0 s: unchanged (hand to the hilt, aura, the ring grows, lights rise)
//   1.50  gone: a horizontal flash through the caster and 22 short rays thrown outward for 0.16 s. The
//         area inside the ring goes almost black in 0.2 s (0.8).
//   1.62  first volley, 0.2 s: 60 % of the cuts. Each is a whole chord on its first frame: a soft band
//         for 0.08 s, then a hairline white core that stays. The first ones each pass through a marked
//         raider's chest.
//   1.86  second volley, 0.2 s: the rest
//   2.05  the pale lifts over 0.2 s: the dark goes to a pale blue-grey, the pieces between the cuts
//         show as panes, each a little off its place, the lines dim to half. Small flecks hang in the
//         air.
//   2.10  back: the caster kneels on their own cell inside the pale picture and sheathes for 1.3 s.
//   3.40  the click: a wide horizontal glint at the scabbard mouth, a white flash over the whole area,
//         the pale lifts at once. The panes flash, then fly outward as dark shards (0.6 cells, turning,
//         shrinking) over 0.7 s. Each marked raider takes its hits and goes down.
//   4.10  thin scars stay on the floor along the cuts; the caster stands up.
//
// Drawing: two ways, picked by "The mod's own shader".
//   On: each pane is drawn with RimArt/CutGlass (lib/vergil-cut-end.js), which shows the picture behind
//   it 0.1 to 0.3 cells off its place, red and blue pulled 0.05 cells apart, and drained of colour to
//   a pale blue-grey. A flying shard takes that picture with it. In game this needs the shader
//   written in ShaderLab and built into an asset bundle per OS with Unity 2022.3.35f1; not done yet.
//   Off: built-in shaders only, which is also what the game draws when the bundle is missing. The
//   hold is a see-through pale wash over a dark disc and the panes are faces and edges only.
// Everything else is built-in either way. Lab pawns, the kneel and the wall are stand-ins, as in the
// first sketch.
import { Color, Graphics, MaterialPropertyBlock, Matrix4x4, Meshes, Quaternion, ShaderPropertyIDs, Vector3 } from '../js/engine.js';
import { draw, mesh } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, rand } from './lib/six-paths-impact.js';
import {
  Blue, Ice, Void, White, carrier, scabbardMouth, hitCut, chord,
  ringAt, glint, aura, streak, line, whiteGlow, smooth, clamp,
} from './lib/vergil.js';
import { Hits, HitGap, polar, markedIn, firstHit, wall, pawns, panes, risingLights, cutGlass } from './lib/vergil-cut-end.js';

const disc = Meshes.disc(64, 'judgement cut end v2 disc');
const Key = 'judgement cut end v2';
const Lead = .5, Tail = 1.8, StandUp = .7, Motes = 40;
// The vanish.
const Flash = .16, Rays = 22, DarkIn = .2, DarkPeak = .8;
// The cuts, as shares of "Gone": each volley's start and length, and the share of the cuts in the first.
const VolleyA = [.2, .33], VolleyB = [.6, .33], FirstShare = .6;
// Each cut: the soft band on its first frame and its width, the core's width, how much of the line is left in the pale picture, how long the lines take to go on the click.
const Band = .08, BandWidth = .4, Core = .035, LineHold = .5, CutsGone = .25;
// The pale picture: it starts this long before the caster is back and takes this long to lift.
const HoldLead = .05, HoldIn = .2;
// Curved cuts, when the slider asks for any: the red, green and blue fringe each shows for this long.
const ArcFringe = .15;
const Violet = new Color(.62, .45, 1), Pale = new Color(.6, .68, .8), HoldDark = .42, Wash = .2, Flecks = 30;
// With the mod's own shader: how far a pane shows the picture off its place (cells), the colour split (cells), the dark left under it.
const GlassShift = [.1, .3], GlassSplit = .05, GlassDark = .22, glassProps = new MaterialPropertyBlock();
const EdgeWidth = .04, EdgeFacing = .3, Ajar = [.03, .08], FallTo = .7, Front = .15;

function times(p) {
  const cast = Lead, vanish = cast + p.warm, back = vanish + p.gone, click = back + p.sheathe;
  return { cast, vanish, back, click, end: click + Tail };
}

// The cuts, relative to the caster. Straight ones are whole chords: the first pass through a marked
// raider's chest, the rest through a point within 45 % of the radius of the caster, at any angle, so
// they cross each other near the middle. at is when each lands, as a share of "Gone".
function layout(p) {
  const marked = markedIn(p.radius), count = Math.round(p.cuts), cuts = [];
  for (let k = 0; k < count; k++) {
    const m = marked[k], through = polar(rand(k * 7 + 2) * 360, Math.sqrt(rand(k * 5 + 3)) * .45 * p.radius);
    const q = m ? { x: m.x + (rand(k * 13 + 1) - .5) * .2, z: m.z + .3 } : through;
    const ang = rand(k * 11 + 4) * Math.PI, d = { x: Math.cos(ang), z: Math.sin(ang) }, ends = chord(q, d, p.radius);
    cuts.push({ q, d, a: ends[0], b: ends[1], victim: m ? k : -1, sort: rand(k + 50) });
  }
  const first = Math.round(count * FirstShare);
  [...cuts].sort((m, n) => m.sort - n.sort).forEach((c, i) => {
    const [from, length] = i < first ? VolleyA : VolleyB, n = i < first ? first : count - first, rank = i < first ? i : i - first;
    c.at = from + length * rank / Math.max(1, n - 1);
  });
  // Curved cuts, if any: part of a circle that stays inside the ring.
  const arcs = Array.from({ length: Math.round(p.arcs) }, (_, k) => {
    const centre = polar(rand(k + 70) * 360, .22 * p.radius), r = (.6 + .12 * rand(k + 71)) * p.radius, from = rand(k + 72) * 360, span = 110 + 40 * rand(k + 73);
    return Array.from({ length: 25 }, (_, n) => { const q = polar(from + span * n / 24, r); return { x: centre.x + q.x, z: centre.z + q.z }; });
  });
  return { marked, cuts, arcs };
}

// Rebuilt only when the radius, the cut count or the sun changes.
let built = { key: '' };
function scene(p, sun) {
  const sunLength = Math.hypot(sun.x, sun.z) || 1, light = { x: -sun.x / sunLength, z: -sun.z / sunLength };
  const key = `${p.radius}|${Math.round(p.cuts)}|${Math.round(p.arcs)}|${light.x.toFixed(2)}|${light.z.toFixed(2)}`;
  if (built.key !== key) {
    const made = layout(p);
    built = { key, ...made, pieces: panes(Key, p.radius, made.cuts, light, EdgeWidth, EdgeFacing) };
  }
  return built;
}

export default {
  kit: 'Vergil', label: 'Judgement Cut End v2 (sketch)',
  params: {
    wall: { label: 'One raider stands behind a wall (no line of sight)', value: true, group: 'Showcase' },
    glass: { label: "The mod's own shader: panes drain the colour and shift the picture (needs an asset bundle in game)", value: true, group: 'Showcase' },
    radius: P('Radius (cells)', 10, 5, 14, .5, 'Shape'),
    cuts: P('Straight cuts', 30, 12, 48, 1, 'Shape'),
    arcs: P('Curved cuts (the game has none)', 0, 0, 3, 1, 'Shape'),
    push: P('Pieces fly outward (cells)', .6, 0, 1.5, .05, 'Shape'),
    warm: P('Warm-up (hand on the hilt)', 1, .3, 2, .05, 'Timing (s)'),
    gone: P('Gone (the cuts land)', .6, .3, 2, .05, 'Timing (s)'),
    sheathe: P('Kneel and sheathe (the pale picture holds)', 1.3, .3, 2.5, .05, 'Timing (s)'),
    fade: P('Pieces fade over', .7, .2, 1.5, .05, 'Timing (s)'),
  },
  compareWith: 'Judgement Cut End (sketch)',
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Raiders close in', t: 0 }, { name: 'Hand on the hilt', t: t.cast }, { name: 'Gone / the cuts', t: t.vanish },
      { name: 'Back: kneel and sheathe in the pale picture', t: t.back }, { name: 'Click: it breaks', t: t.click }];
  },
  events(p) {
    const t = times(p);
    return [{ t: t.vanish, type: 'shake', value: .05 }, { t: t.click, type: 'shake', value: .14 },
      { t: t.vanish, type: 'sound', def: 'AG_VergilCutEndVanish' }, { t: t.click, type: 'sound', def: 'AG_VergilSheathe' },
      { t: t.click, type: 'sound', def: 'AG_VergilCutEndCuts' }];
  },

  draw(s, p, { origin: o, scene: lab }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = lab?.shadowVector ?? { x: -.45, z: -.32 }, strength = lab?.sun?.strength ?? .32;
    const { pieces: shards, cuts, arcs, marked } = scene(p, sun), R = p.radius;
    const world = q => ({ x: o.x + q.x, z: o.z + q.z });
    const sinceVanish = s - t.vanish, sinceClick = s - t.click;
    // hold: 0 while the cuts are lines in the dark, 1 once the picture is pale and the caster kneels in it. dark: how much of the area's dark is on.
    const hold = smooth((s - (t.back - HoldLead)) / HoldIn), held = sinceClick < 0 ? 1 : 0;
    const dark = smooth(sinceVanish / DarkIn) * held;

    if (p.wall) wall(o);

    // --- the floor: the true radius, scars left along the cuts ---------------------------------------------------------
    if (s >= t.cast) {
      const grown = smooth((s - t.cast) / (p.warm * .8)), left = 1 - smooth((sinceClick - .3) / .6);
      ringAt(o, R * grown, Blue.withAlpha(.6 * left), Floor + .02);
    }
    if (sinceClick >= 0) cuts.forEach((c, k) =>
      streak(`${Key} scar ${k}`, world(c.a), world(c.b), .05, Void.withAlpha(.5 - .25 * smooth(sinceClick / Tail)), undefined, Floor + .01, 4));

    pawns(s, t, R, o, sun, strength, dark, p.wall);

    // --- the caster -------------------------------------------------------------------------------------------------------
    const w = clamp((s - t.cast) / p.warm);
    if (s < t.vanish) {
      if (s >= t.cast) aura(Key, o, s, w, Blue);
      carrier(o, sun, strength, { hand: smooth(w * 3) });
    } else if (s < t.back) {
      const gone = clamp(sinceVanish / .08);
      carrier(o, sun, strength, { alpha: 1 - gone, tint: Ice, tintAmount: gone });
    } else {
      const v = clamp((s - t.back) / p.sheathe), home = v < .9 ? .88 * smooth(v / .9) : .88 + .12 * (v - .9) / .1;
      sprite({ x: o.x, z: o.z + .3 }, 1.8, 1.8, Blue.withAlpha(.35 * dark), glow, Y - .04);
      carrier(o, sun, strength, { pose: sinceClick < StandUp ? 'kneel' : 'stand', sheathe: home, alpha: clamp((s - t.back) / .1), tint: Ice, tintAmount: 1 - clamp((s - t.back) / .2) });
    }
    if (s >= t.cast && s < t.vanish) risingLights(Key, o, R, w, Motes);

    // --- the dark inside the ring, over the pawns and under the cuts: almost black for the cuts, pale for the hold ---------
    draw(disc, o.x, Y - .05, o.z, R, R, 0, Void.withAlpha((DarkPeak + ((p.glass ? GlassDark : HoldDark) - DarkPeak) * hold) * dark));
    if (!p.glass) draw(disc, o.x, Y - .049, o.z, R, R, 0, Pale.withAlpha(Wash * hold * dark));

    // --- the vanish: a flash through the caster and rays thrown outward ------------------------------------------------------
    if (sinceVanish >= 0 && sinceVanish < Flash) {
      const u = sinceVanish / Flash, f = (1 - u) * (1 - u), chest = { x: o.x, z: o.z + .3 };
      streak(`${Key} flash`, { x: chest.x - 2.2 - 2 * u, z: chest.z }, { x: chest.x + 2.2 + 2 * u, z: chest.z }, .3 * (1 - u), White.withAlpha(f), whiteGlow, Y + .05, 8);
      sprite(chest, 3 + 3 * u, 3 + 3 * u, Ice.withAlpha(.7 * f), glow, Y + .049);
      for (let i = 0; i < Rays; i++) {
        const ang = (i / Rays + rand(i + 200) * .03) * Math.PI * 2, near = .5 + R * .5 * u, far = near + (1.2 + 2.5 * rand(i + 220)) * (1 - u * .4);
        streak(`${Key} ray ${i}`, { x: chest.x + Math.cos(ang) * near, z: chest.z + Math.sin(ang) * near }, { x: chest.x + Math.cos(ang) * far, z: chest.z + Math.sin(ang) * far },
          .09, Ice.withAlpha(.8 * f), whiteGlow, Y + .048, 3);
      }
    }

    // --- the pieces between the cuts: panes of glass, ajar through the hold, dark shards that fly on the click -----------------
    if (hold > 0 && sinceClick < p.fade) {
      const u = clamp(sinceClick / p.fade), out = smooth(clamp(u * 1.6)), gone = Math.pow(1 - u, 1.5);
      shards.forEach((piece, i) => {
        const far = Math.hypot(piece.centre.x, piece.centre.z) || 1, slide = sinceClick < 0 ? 0 : p.push * (.35 + 1.3 * rand(i + 31)) * out;
        const ajar = (Ajar[0] + (Ajar[1] - Ajar[0]) * rand(i + 90)) * hold, lean = rand(i + 91) * Math.PI * 2;
        const at = world({ x: piece.centre.x * (1 + slide / far) + Math.cos(lean) * ajar, z: piece.centre.z * (1 + slide / far) + Math.sin(lean) * ajar });
        const facet = .04 + .12 * rand(i + 3), size = 1 - (1 - FallTo) * out, turn = (rand(i + 7) - .5) * (3 * hold + 26 * out), edge = sinceClick < 0 ? hold : gone;
        if (p.glass) {
          // The pane shows the picture behind it off its place and without its colour; flying, it takes that picture with it.
          const shift = GlassShift[0] + (GlassShift[1] - GlassShift[0]) * rand(i + 92) + slide * .6;
          glassProps.SetColor(ShaderPropertyIDs.Color, Pale.withAlpha(sinceClick < 0 ? hold : gone));
          glassProps.SetVector('_Shift', { x: Math.cos(lean) * shift, y: Math.sin(lean) * shift, z: GlassSplit });
          Graphics.DrawMesh(mesh(`${Key} piece ${i}`), Matrix4x4.TRS(new Vector3(at.x, Y + .018, at.z), Quaternion.Euler(0, turn, 0), new Vector3(size, 1, size)), cutGlass, 0, null, 0, glassProps);
        }
        if (sinceClick < 0) draw(mesh(`${Key} piece ${i}`), at.x, Y + .02, at.z, size, size, turn, Ice.withAlpha(facet * hold), whiteGlow);
        else {
          draw(mesh(`${Key} piece ${i}`), at.x, Y + .019, at.z, size, size, turn, Void.withAlpha((p.glass ? .3 : .6) * gone * (.5 + rand(i + 3))));
          draw(mesh(`${Key} piece ${i}`), at.x, Y + .02, at.z, size, size, turn, Ice.withAlpha(.5 * clamp(1 - u * 6)), whiteGlow);
        }
        // The edges: a shard's edge catches the light once as it turns, then the shard is a dark flake.
        if (piece.dim) draw(mesh(`${Key} piece ${i} dim`), at.x, Y + .021, at.z, size, size, turn, Void.withAlpha((sinceClick < 0 ? .4 : .6) * edge));
        if (piece.lit) draw(mesh(`${Key} piece ${i} lit`), at.x, Y + .022, at.z, size, size, turn, Ice.withAlpha((sinceClick < 0 ? .4 : .45) * edge), whiteGlow);
      });
    }

    // --- the cuts: whole on their first frame, a soft band that is gone in 0.08 s, a hairline core that stays --------------------
    // In the pale picture the dark slit goes and half the light with it: the line is then the edge between two panes.
    const lineAlpha = sinceClick >= 0 ? 1 - sinceClick / CutsGone : 1 - (1 - LineHold) * hold;
    if (sinceVanish >= 0 && sinceClick < CutsGone) {
      arcs.forEach((pts, k) => {
        const age = sinceVanish - (VolleyA[0] + k * .03) * p.gone;
        if (age < 0) return;
        const seen = pts.map(world), fringe = clamp(1 - age / ArcFringe);
        if (fringe > 0) [[new Color(1, .15, .25), -1], [new Color(.2, 1, .4), 0], [new Color(.25, .4, 1), 1]].forEach(([colour, side], j) => {
          const centre = seen[12], shifted = seen.map(q => { const dx = q.x - centre.x, dz = q.z - centre.z, len = Math.hypot(dx, dz) || 1; return { x: q.x + dx / len * side * .14, z: q.z + dz / len * side * .14 }; });
          line(`${Key} arc ${k} fringe ${j}`, shifted, .1 + .08 * fringe, colour.withAlpha(.9 * fringe), whiteGlow, Y + .029, 'both');
        });
        line(`${Key} arc ${k} glow`, seen, .2, Violet.withAlpha(.25 * lineAlpha), whiteGlow, Y + .03, 'both');
        line(`${Key} arc ${k} core`, seen, Core * 1.4, Color.Lerp(Violet, White, .6).withAlpha(lineAlpha), whiteGlow, Y + .031, 'both');
      });
      cuts.forEach((c, k) => {
        const age = sinceVanish - c.at * p.gone;
        if (age < 0) return;
        const a = world(c.a), b = world(c.b), fresh = clamp(1 - age / Band), hot = sinceClick >= 0 ? 1 : 0;
        if (fresh > 0) streak(`${Key} cut ${k} band`, a, b, BandWidth * (.5 + .5 * fresh), Ice.withAlpha(.35 * fresh), whiteGlow, Y + .03, 10);
        streak(`${Key} cut ${k} slit`, a, b, Core * 2.4, Void.withAlpha(.6 * lineAlpha * (1 - hot) * (1 - hold)), undefined, Y + .0305, 10);
        streak(`${Key} cut ${k} glow`, a, b, Core * (4 + 4 * hot), Blue.withAlpha(.25 * lineAlpha * (1 - .6 * hold)), whiteGlow, Y + .031, 10);
        streak(`${Key} cut ${k} core`, a, b, Core * (1 + hot), White.withAlpha(lineAlpha), whiteGlow, Y + .032, 10);
        if (c.victim >= 0 && sinceClick < 0) glint(`${Key} mark ${k}`, world({ x: marked[c.victim].x, z: marked[c.victim].z + .3 }), .16 + .3 * fresh, .9, White, 45 + k * 20);
      });
    }

    // --- the hold: flecks hang in the pale air ------------------------------------------------------------------------------------
    if (hold > 0 && sinceClick < .08) {
      const live = hold * (sinceClick < 0 ? 1 : 1 - sinceClick / .08);
      for (let i = 0; i < Flecks; i++) {
        const at = polar(rand(i + 300) * 360, Math.sqrt(rand(i + 330)) * R * .95), drift = (s - t.vanish) * (.05 + .1 * rand(i + 360)), size = .05 + .07 * rand(i + 390);
        sprite(world({ x: at.x + drift * .3, z: at.z + drift }), size, size, (i % 3 ? Void : Ice).withAlpha((i % 3 ? .6 : .55) * live), i % 3 ? undefined : glow, Y + .035, rand(i) * 90);
      }
    }

    // --- the click ------------------------------------------------------------------------------------------------------------------
    if (sinceClick >= 0) {
      const mouth = scabbardMouth(o), f = clamp(sinceClick / .3);
      streak(`${Key} click line`, { x: mouth.x - 1.8 - f, z: mouth.z }, { x: mouth.x + 1.8 + f, z: mouth.z }, .14 * (1 - f), White.withAlpha(1 - f), whiteGlow, Y + .062, 8);
      glint(`${Key} click`, mouth, .2 + .5 * (1 - f), 1 - f, White, 0);
      if (sinceClick < Front * 2) ringAt(o, R * smooth(sinceClick / Front), Ice.withAlpha(.45 * (1 - sinceClick / (Front * 2))), Y + .06, true, whiteGlow);
      draw(disc, o.x, Y + .055, o.z, R, R, 0, White.withAlpha(.55 * (1 - clamp(sinceClick / .14))), whiteGlow);
      sprite(o, R * 2.4, R * 2.4, Ice.withAlpha(.3 * (1 - clamp(sinceClick / .2))), glow, Y + .056);
      marked.forEach((m, i) => {
        const first = firstHit(t.click, i);
        for (let h = 0; h < Hits; h++) hitCut(`${Key} hit ${i} ${h}`, world(m), (h * 47 + i * 31 + 20) % 180 - (h % 2 ? 0 : 90), s - first - h * HitGap);
      });
    }
  },
};
