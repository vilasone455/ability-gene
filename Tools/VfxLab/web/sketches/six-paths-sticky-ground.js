// Sticky Ground — two Six Paths orbs cast forward, squash into uneven pools, and
// merge into a viscous patch. Peripheral splats, crawling edges, and elastic
// strands sell adhesion. The patch contracts back into exactly two returning orbs.
// A visual proposal only: the target's struggle does not implement a movement debuff.
// Default sequence: cast .55 s, splat/spread .55 s, sticky hold 2 s, recall .75 s.
import { Color, Mathf } from '../js/engine.js';
import { P, Body, Rim, Y, Floor, at, sprite, orb, band, trail, target, rand, glow } from './lib/six-paths-impact.js';
const smooth = Mathf.Smooth, lerp = Mathf.Lerp, TAU = Math.PI * 2;
const wet = new Color(0.10, 0.075, 0.145);
function times(p) {
  const settle = p.cast + p.spread + 0.08, recall = settle + p.hold;
  return { settle, recall, end: recall + p.recall + 0.3 };
}
function home(o, i) { return at(o, i ? 0.65 : -0.65, -3); }
function landing(o, i, p) { return at(o, (i ? 1 : -1) * p.size * 0.38, i ? 0.30 : -0.15); }
function flight(s, o, i, p, t) {
  const h = home(o, i), b = landing(o, i, p);
  const returning = s >= t.recall;
  const u = smooth(returning ? (s - t.recall) / p.recall : s / p.cast);
  const from = returning ? b : h, to = returning ? h : b;
  return { x: lerp(from.x, to.x, u) + Math.sin(u * Math.PI) * (i ? 0.35 : -0.35),
    z: lerp(from.z, to.z, u) + Math.sin(u * Math.PI) * 0.6 };
}
// Radial outlines have irregular lobes and notches at several scales. The edge
// creeps slowly, preserving its identity rather than changing random shape each frame.
function puddle(key, o, size, seed, s, alpha, aspect = 1, sheen = false) {
  if (size < 0.001 || alpha <= 0) return;
  const pts = [], inner = [], centre = [];
  for (let i = 0; i <= 120; i++) {
    const a = i / 120 * TAU;
    const r = size * (0.84 + Math.sin(a * 3 + seed) * 0.12
      + Math.cos(a * 7 - seed * 2) * 0.085 + Math.sin(a * 13 + seed) * 0.04
      + Math.sin(a * 5 + s * 2.2 + seed) * 0.025);
    pts.push(at(o, Math.cos(a) * r, Math.sin(a) * r * aspect));
    inner.push(at(o, Math.cos(a) * Math.max(0, r - 0.025), Math.sin(a) * Math.max(0, r - 0.025) * aspect));
    centre.push(o);
  }
  // All pool rims are below every pool body, so the two lobes merge cleanly.
  band(`${key} outline`, pts, centre, Rim.withAlpha(alpha * 0.35), Floor + 0.010);
  band(`${key} body`, inner, centre, (sheen ? wet : Body).withAlpha(alpha), Floor + (sheen ? 0.016 : 0.012));
}
export default {
  kit: 'Six Paths', label: 'Sticky Ground (sketch)',
  params: {
    actors: { label: 'Show target struggling', value: true, group: 'Showcase' },
    cast: P('Two orbs travel', 0.55, 0.25, 1, 0.05, 'Timing (s)'),
    spread: P('Splat and spread', 0.55, 0.25, 1, 0.05, 'Timing (s)'),
    hold: P('Sticky ground holds', 2, 0.8, 4, 0.1, 'Timing (s)'),
    recall: P('Pull back into two orbs', 0.75, 0.4, 1.2, 0.05, 'Timing (s)'),
    size: P('Pool radius (cells)', 2.25, 1.3, 3.5, 0.05, 'Ground'),
    mess: P('Scattered splats', 22, 8, 36, 1, 'Ground'),
    strands: P('Sticky strand length', 0.85, 0.3, 1.4, 0.05, 'Ground'),
    sheen: P('Wet highlights', 0.45, 0, 0.8, 0.05, 'Ground'),
    shake: P('Splat camera shake', 0.065, 0, 0.15, 0.005, 'Impact'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [{ name: 'Cast two orbs', t: 0 }, { name: 'Splat', t: p.cast },
    { name: 'Sticky ground', t: t.settle }, { name: 'Recall', t: t.recall }]; },
  events(p) { return [0, 1].map(i => ({ t: p.cast + i * 0.08, type: 'shake', value: p.shake })); },
  draw(s, p, { origin }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const recall = smooth((s - t.recall) / p.recall);
    const fade = 1 - smooth((s - t.end + 0.2) / 0.2);
    const active = smooth((s - p.cast) / p.spread) * (1 - recall);
    for (let i = 0; i < 2; i++) {
      const born = p.cast + i * 0.08;
      const spread = smooth((s - born) / p.spread);
      const b = landing(origin, i, p);
      const size = p.size * spread * (1 - recall);
      puddle(`sticky pool ${i}`, b, size, i * 2.8 + 1, s, 1, i ? 0.82 : 1);
      const moving = s < born || s >= t.recall;
      if (moving) {
        const clock = s < born ? Math.max(0, s - i * 0.08) : s;
        const pos = flight(clock, origin, i, p, t);
        const alpha = s >= t.recall ? recall : 1;
        const pts = Array.from({ length: 20 }, (_, j) => flight(Math.max(0, clock - (1 - j / 19) * 0.15), origin, i, p, t));
        trail(`sticky orb trail ${i}`, pts, 0.13, Rim.withAlpha(0.45 * alpha * fade), Y);
        orb(pos, 0.32, alpha * fade);
      } else if (s < born + 0.16) {
        const squash = smooth((s - born) / 0.16);
        orb(b, 0.32 + squash * 0.32, 1 - squash, 1 - squash * 0.8);
      }
      const age = s - born;
      if (age >= 0 && age < 0.18) sprite(b, 1.2, 0.75, Rim.withAlpha((1 - age / 0.18) * 0.35), glow);
    }
    // Satellites first fly outward, then remain as flattened, uneven stains.
    for (let i = 0; i < p.mess; i++) {
      const born = p.cast + (i % 2) * 0.08, age = s - born;
      if (age < 0) continue;
      const a = i * 2.399, u = Mathf.Clamp01(age / (0.23 + rand(i) * 0.22));
      const distance = p.size * (0.95 + rand(i + 20) * 0.65) * smooth(u) * (1 - recall);
      const b = at(origin, Math.cos(a) * distance * 1.35, Math.sin(a) * distance * 0.95);
      const size = (0.07 + rand(i + 40) * 0.19) * (1 - recall);
      if (u < 1) {
        const pos = at(b, 0, 0, Math.sin(u * Math.PI) * 0.8);
        orb(pos, size, 1, 1 + Math.sin(u * Math.PI));
      } else puddle(`sticky splat ${i}`, b, size * 1.7, i, s * 0.25, 1, 0.65);
    }
    // Local, curved reflections and threads describe thick material on the floor.
    for (let j = 0; j < 9; j++) {
      const a = j * 2.399, r = p.size * (0.25 + rand(j) * 0.7) * active;
      const b = at(origin, Math.cos(a) * r, Math.sin(a) * r * 0.7);
      const pts = Array.from({ length: 24 }, (_, k) => {
        const u = k / 23;
        return at(b, (u - 0.5) * 0.65 * active, Math.sin(u * Math.PI) * 0.13 * active);
      });
      trail(`sticky sheen ${j}`, pts, 0.045, Rim.withAlpha(p.sheen * active * 0.5), Floor + 0.02);
      const bubble = Math.sin(s * 2.8 + j) ** 8;
      puddle(`sticky blister ${j}`, b, (0.06 + bubble * 0.07) * active, j, s, p.sheen * active, 0.7, true);
    }
    const struggle = Math.sin(Math.max(0, s - t.settle) * 4) * 0.18 * active;
    const actor = at(origin, struggle, 0.15 + Math.abs(struggle) * 0.35);
    target(actor, p.actors);
    if (p.actors && active > 0) for (let i = 0; i < 4; i++) {
      const a = i * Math.PI / 2 + 0.4;
      const root = at(origin, Math.cos(a) * p.strands, Math.sin(a) * p.strands * 0.6);
      const tip = at(actor, (i % 2 ? 1 : -1) * 0.13, 0.08, Math.abs(struggle) * 2);
      const pts = Array.from({ length: 20 }, (_, j) => {
        const u = j / 19;
        return { x: lerp(root.x, tip.x, u), z: lerp(root.z, tip.z, u) - Math.sin(u * Math.PI) * 0.18 };
      });
      trail(`sticky tether ${i}`, pts, 0.09 * active, Body, Y + 0.04);
      trail(`sticky tether rim ${i}`, pts, 0.018 * active, Rim.withAlpha(0.6 * active), Y + 0.041);
    }
  },
};
