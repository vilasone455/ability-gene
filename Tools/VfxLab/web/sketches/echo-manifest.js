// Echo — Manifest, the shared transformation picture. Not the game: today EchoUtility.Manifest
// throws one vanilla lightning glow fleck and Revert draws nothing.
//
// What it is for (agreed 2026-09-30: one shared picture for every Echo, tinted per hero, instead
// of fifteen custom ones; custom ones are post-v1 polish). The mechanic is already built and does
// not change: Manifest is instant and free, the costume, hero weapon and abilities swap in the
// same tick, and the pawn is not held. So this is cosmetic, about 1 s, and plays over the swap.
// Revert plays it downward and shorter. Collapse (the charge pool at zero: Revert plus
// AG_EchoCollapse, consciousness max 10 % for 1 h, so the pawn goes down) plays it broken.
// Proposed XML: one `manifestColour` on EchoDef, next to `hairColour`; the defaults per hero
// are the `Heroes` table below.
//
// Order (Manifest, default sliders; heights are the real pawn's from lib/pawn.js):
//   0.00  swap: the costume is on. A tinted flash covers the whole body for 0.12 s so the swap
//         does not pop. Sound marker AG_EchoManifest (not made yet).
//   0.00  a thin tinted ring opens on the floor round the cell centre, 0 -> 0.9 cells in 0.25 s,
//         a soft glow inside it. It holds while the column plays and fades with the column.
//   0.05  column: a soft additive column 0.75 cells wide rises out of the ring. Its top sweeps
//         from the feet to 0.3 cells over the head top in 0.45 s behind a bright scan line. It is
//         translucent and in front of the pawn, so the costume shows through it.
//   0.50  three level rings peel off the top of the column 0.12 s apart: each rises 0.3 cells
//         on screen and widens 0.3 -> 0.55 cells while fading over 0.35 s. Ten sparks spiral up
//         the column from the ring's edge between 0.10 and 0.95.
//   0.50  the column's foot lifts from the feet to the head and its light fades, over 0.5 s.
//   1.00  end: the pawn in costume. Nothing stays on the floor.
// Revert (0.65 s): flash 0.08 s, ring 0.6 cells, the column starts full height and its top drops
//   to the feet in 0.35 s; no peel rings; four sparks fall; the ring closes over 0.25 s; every
//   alpha at 60 %.
// Collapse (0.9 s): as Revert but the floor ring is three broken arcs turning slowly, the column
//   flickers (alpha steps every 3 frames), the scan line jitters, and at 0.55 the pawn goes down
//   and stays down.
//
// Drawing: the floor ring and the peel rings are level circles, and the column is centred on the
// pawn, so every facing draws the same and no per-facing method is needed. The stand-in switches
// shirt colour at 0 under the flash. `lab/echo-soft-ring` is a lab texture: the port needs
// Textures/RimArt/Echo/SoftRing.png from a make_echo_textures.py (the formula is the generator
// below). Everything else uses RimArt/SixPaths/SoftDisc, which the game already ships.
import { Color, MaterialPool, Mathf, ShaderDatabase } from '../js/engine.js';
import { registerLabTexture, pixels } from '../js/standins.js';
import { P, Y, Floor, Lift, sprite, band, circle, glow, rand } from './lib/six-paths-impact.js';
import { draw, mesh } from './lib/six-paths-solid.js';
import { pawn, Bodies, Shirt } from './lib/pawn.js';

// A soft ring: alpha 1 at 0.8 of the image radius, 0 at 0.14 either side.
registerLabTexture('lab/echo-soft-ring', () => pixels(128, (u, v) => {
  const r = Math.hypot(u - 0.5, v - 0.5) * 2;
  return [1, 1, 1, Math.pow(Math.max(0, 1 - Math.abs(r - 0.8) / 0.14), 1.5)];
}));
const ringGlow = MaterialPool.MatFrom('lab/echo-soft-ring', ShaderDatabase.MoteGlow);
const flatGlow = MaterialPool.MatFrom('white', ShaderDatabase.MoteGlow);

// An additive rectangle: x centre, from z0 to z1, width w. Nested at falling widths it is a soft column.
function rect(key, x, z0, z1, w, colour, layer) {
  const m = mesh(key);
  m.setFlat([x - w / 2, z0, x - w / 2, z1, x + w / 2, z1, x + w / 2, z0], [0, 1, 2, 0, 2, 3]);
  draw(m, 0, layer, 0, 1, 1, 0, colour, flatGlow);
}

const White = new Color(1, 1, 1);
// Tint per hero (the proposed manifestColour default) and the stand-in's costume colour.
const Heroes = {
  Gojo:   { tint: new Color(0.35, 0.78, 1.00), shirt: new Color(0.10, 0.10, 0.14) },   // black uniform
  Vergil: { tint: new Color(0.55, 0.72, 1.00), shirt: new Color(0.16, 0.22, 0.45) },   // blue coat
  Itachi: { tint: new Color(0.95, 0.18, 0.22), shirt: new Color(0.10, 0.08, 0.12) },   // Akatsuki cloak
  Pain:   { tint: new Color(0.60, 0.38, 0.95), shirt: new Color(0.10, 0.08, 0.12) },
  Goku:   { tint: new Color(1.00, 0.72, 0.25), shirt: new Color(0.92, 0.45, 0.12) },   // orange gi
  Sato:   { tint: new Color(0.80, 0.82, 0.90), shirt: new Color(0.22, 0.22, 0.24) },
};

const RingOpen = 0.25;      // s, the floor ring reaching its radius
const RingClose = 0.25;     // s, Revert and Collapse closing the ring
const PeelGap = 0.12;       // s between peel rings
const PeelLife = 0.35;      // s, one peel ring rising and fading
const PeelRise = 0.3;       // cells on screen a peel ring rises
const DownAt = 0.55;        // s, Collapse: the pawn goes down
const SparkLife = 0.5;      // s

function times(p) {
  const m = p.scenario === 'manifest', col = p.scenario === 'collapse';
  const sweep = m ? p.sweep : p.sweep * 0.78, fade = m ? p.fade : p.fade * 0.5;
  const sweepStart = 0.05, sweepEnd = sweepStart + sweep;
  return {
    m, col, k: m ? 1 : 0.6,
    flash: m ? p.flash : p.flash * 0.67,
    ringR: m ? p.ring : p.ring * 0.67,
    sweepStart, sweep, sweepEnd,
    fade, fadeEnd: sweepEnd + fade,
    end: m ? Math.max(sweepEnd + fade, sweepEnd + (p.rings - 1) * PeelGap + PeelLife)
      : col ? Math.max(sweepEnd + RingClose, DownAt + 0.35) : sweepEnd + RingClose + 0.05,
  };
}

// A ring segment from angle a0 to a1 (degrees, 0 east, counter-clockwise), radius r, half width w.
function arc(key, o, r, a0, a1, w, colour, layer) {
  const inner = [], outer = [], n = 24;
  for (let i = 0; i <= n; i++) {
    const a = (a0 + (a1 - a0) * i / n) * Math.PI / 180, c = Math.cos(a), s = Math.sin(a);
    inner.push({ x: o.x + c * (r - w), z: o.z + s * (r - w) });
    outer.push({ x: o.x + c * (r + w), z: o.z + s * (r + w) });
  }
  band(key, inner, outer, colour, layer);
}

export default {
  kit: 'Echo',
  label: 'Manifest (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'manifest', options: ['manifest', 'revert', 'collapse'], group: 'Scene' },
    hero: { label: 'Hero (tint)', value: 'Gojo', options: Object.keys(Heroes), group: 'Scene' },
    body: { label: 'Body', value: 'average', options: ['average', 'fat', 'hulk'], group: 'Scene' },
    ring: P('Ring radius (cells)', 0.9, 0.3, 1.5, 0.05, 'Shape'),
    width: P('Column width (cells)', 0.75, 0.3, 1.5, 0.05, 'Shape'),
    rise: P('Column top over the head (cells)', 0.3, 0, 1, 0.05, 'Shape'),
    rings: P('Peel rings', 3, 0, 6, 1, 'Shape'),
    sparks: P('Sparks', 10, 0, 24, 1, 'Shape'),
    flash: P('Swap flash', 0.12, 0, 0.5, 0.01, 'Timing (s)'),
    sweep: P('Column sweep', 0.45, 0.1, 1.5, 0.05, 'Timing (s)'),
    fade: P('Column fade', 0.5, 0.1, 1.5, 0.05, 'Timing (s)'),
  },

  duration(p) { return times(p).end; },
  phases(p) {
    const T = times(p), out = [{ name: 'Swap', t: 0 }, { name: 'Column', t: T.sweepStart }];
    if (T.m) out.push({ name: 'Peel', t: T.sweepEnd }, { name: 'Fade', t: T.sweepEnd + PeelGap * 2 });
    else out.push({ name: 'Close', t: T.sweepEnd });
    if (T.col) out.push({ name: 'Down', t: DownAt });
    return out;
  },
  events(p) {
    const T = times(p);
    const def = T.m ? 'AG_EchoManifest' : T.col ? 'AG_EchoCollapse' : 'AG_EchoRevert';
    const out = [{ t: 0, type: 'sound', def }];
    if (T.col) out.push({ t: DownAt, type: 'shake', value: 0.03 });
    return out;
  },

  draw(s, p, { origin, scene }) {
    const T = times(p), hero = Heroes[p.hero] ?? Heroes.Gojo, tint = hero.tint, k = T.k;
    const sun = scene?.shadowVector ?? { x: -0.45, z: -0.32 };
    const b = Bodies[p.body] ?? Bodies.average;
    const feet = origin.z + b.feet, top = origin.z + b.headTop, peak = top + p.rise;
    const inHero = T.m ? s >= 0 : s < 0;
    const downed = T.col && s >= DownAt;
    const who = { body: p.body, sun, downed, shirt: inHero ? hero.shirt : Shirt };
    const frame = Math.floor(s * 60);
    const flicker = T.col ? (rand(Math.floor(s * 20) + 100) > 0.3 ? 1 : 0.35) : 1;
    const pale = Color.Lerp(tint, White, 0.55);

    // Floor ring: opens, holds, then fades with the column (Manifest) or closes (Revert, Collapse).
    let r = T.ringR * Mathf.Smooth(s / RingOpen), ringA = 1;
    if (T.m) ringA = 1 - Mathf.Smooth((s - T.sweepEnd) / T.fade);
    else if (s > T.sweepEnd) r = T.ringR * (1 - Mathf.Smooth((s - T.sweepEnd) / RingClose));
    if (r > 0.02 && ringA > 0) {
      const a = ringA * k * flicker;
      sprite(origin, r * 2.5, r * 2.5, tint.withAlpha(0.55 * a), ringGlow, Floor + 0.006);
      sprite(origin, r * 1.6, r * 1.6, tint.withAlpha(0.22 * a), glow, Floor + 0.004);
      if (T.col) {
        for (let i = 0; i < 3; i++) {
          const a0 = i * 120 + 10 + s * 30;
          arc(`echo manifest arc ${i}`, origin, r, a0, a0 + 85, 0.03, pale.withAlpha(0.9 * a), Floor + 0.008);
        }
      } else {
        circle(origin, r, 0.9 * a, Floor + 0.008, pale);
        circle(origin, r - 0.03, 0.9 * a, Floor + 0.008, pale);
      }
    }

    pawn(origin, who);

    // Column: Manifest's top sweeps up from the feet, then its foot lifts and it fades.
    // Revert's and Collapse's top drops from the peak to the feet.
    let colTop = null, colBot = feet, colA = 0;
    if (T.m) {
      if (s >= T.sweepStart && s < T.fadeEnd) {
        const up = Mathf.Smooth((s - T.sweepStart) / T.sweep), down = Mathf.Smooth((s - T.sweepEnd) / T.fade);
        colTop = Mathf.Lerp(feet, peak, up);
        colBot = Mathf.Lerp(feet, top, down);
        colA = 1 - down;
      }
    } else if (s >= T.sweepStart && s < T.sweepEnd) {
      const down = Mathf.Smooth((s - T.sweepStart) / T.sweep);
      colTop = Mathf.Lerp(peak, feet, down);
      colA = 1 - down * 0.5;
    }
    if (colTop !== null && colTop - colBot > 0.02) {
      const a = colA * k * flicker, h = colTop - colBot, c = { x: origin.x, z: (colTop + colBot) / 2 };
      // Four nested additive rectangles make the column's soft sides; the discs soften its ends.
      [[1.0, 0.10], [0.72, 0.13], [0.46, 0.16], [0.22, 0.22]].forEach(([wf, af], i) =>
        rect(`echo manifest column ${i}`, origin.x, colBot, colTop, p.width * wf, (i < 3 ? tint : pale).withAlpha(af * a), Y + 0.02 + i * 0.002));
      sprite(c, p.width * 1.6, h + 0.5, tint.withAlpha(0.35 * a), glow, Y + 0.03);
      sprite(c, p.width * 0.6, h + 0.15, pale.withAlpha(0.5 * a), glow, Y + 0.04);
      // Scan line at the moving end, only while the top is sweeping.
      const sweeping = T.m ? s < T.sweepEnd : true;
      if (sweeping) {
        const jx = T.col ? (rand(frame + 50) - 0.5) * 0.1 : 0;
        const line = { x: origin.x + jx, z: colTop };
        sprite(line, p.width * 1.4, 0.14, White.withAlpha(0.9 * a), glow, Y + 0.06);
        sprite(line, p.width * 1.9, 0.30, tint.withAlpha(0.6 * a), glow, Y + 0.05);
      }
    }

    // Swap flash over the whole body, so the costume swap at 0 does not pop.
    if (s >= 0 && s < T.flash) {
      const a = (1 - s / T.flash) * k;
      const c = { x: origin.x, z: (feet + top) / 2 };
      sprite(c, b.width + 0.5, top - feet + 0.5, pale.withAlpha(0.9 * a), glow, Y + 0.08);
      sprite(c, b.width + 1.0, top - feet + 1.0, tint.withAlpha(0.5 * a), glow, Y + 0.07);
    }

    // Peel rings: level circles leaving the top of the column (Manifest only).
    if (T.m) {
      for (let i = 0; i < p.rings; i++) {
        const u = (s - T.sweepEnd - i * PeelGap) / PeelLife;
        if (u <= 0 || u >= 1) continue;
        const pos = { x: origin.x, z: peak - 0.1 + u * PeelRise }, rr = 0.3 + 0.25 * u, a = (1 - u) * 0.8 * k;
        circle(pos, rr, 0.5 * a, Y + 0.05, pale);
        sprite(pos, rr * 2.5, rr * 2.5, tint.withAlpha(0.9 * a), ringGlow, Y + 0.045);
      }
    }

    // Sparks: from the ring's edge, spiralling up the column (Manifest) or falling (Revert, Collapse).
    const n = T.m ? p.sparks : Math.round(p.sparks * 0.4), span = (top - feet) / Lift * 1.2;
    for (let i = 0; i < n; i++) {
      const born = 0.1 + rand(i) * 0.35, u = (s - born) / SparkLife;
      if (u <= 0 || u >= 1) continue;
      const a = rand(i + 30) * Math.PI * 2 + u * 3, rr = T.ringR * (0.9 - 0.5 * u);
      const h = T.m ? u * span : (1 - u) * span * 0.6;
      const pos = { x: origin.x + Math.cos(a) * rr, z: feet + Math.sin(a) * rr + h * Lift };
      const al = Math.sin(u * Math.PI) * k * flicker;
      sprite(pos, 0.14, 0.14, White.withAlpha(0.9 * al), glow, Y + 0.09);
      sprite(pos, 0.30, 0.30, tint.withAlpha(0.5 * al), glow, Y + 0.085);
    }
  },
};
