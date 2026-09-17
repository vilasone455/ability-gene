// Heavenfall Spears — standalone overhead VFX proposal explicitly requested by
// the user. Six orbs ascend (0.65 s), stretch (0.4 s), aim (0.35 s), then fall
// at 0.17 s intervals. A pause precedes the heavier central sixth spear.
// Height uses the kit's northward Lift projection; target marks never leave the floor.
// Trails sample prior tip positions. The impact event occurs exactly when the tip lands.
import { Mathf, Color } from '../js/engine.js';
import { P, Body, Rim, Y, Floor, at, sprite, orb, circle, band, trail, impact, target, glow } from './lib/six-paths-impact.js';
const smooth = Mathf.Smooth, lerp = Mathf.Lerp;
function times(p) {
  const form = p.rise, aim = form + p.form, drop = aim + p.aim;
  const last = drop + 5 * p.interval + p.finalPause;
  const recall = last + p.fall + p.stand;
  return { form, aim, drop, last, recall, end: recall + p.recall + 0.3 };
}
const start = (i, p, t) => t.drop + i * p.interval + (i === 5 ? p.finalPause : 0);
function base(o, i, p) {
  const a = i / 5 * Math.PI * 2 + p.spin * Mathf.Deg2Rad;
  return i === 5 ? at(o, 0, 0) : at(o, Math.cos(a) * p.radius, Math.sin(a) * p.radius);
}
function tip(s, p, t, o, i) {
  const b = base(o, i, p), up = smooth(s / p.rise);
  const fall = Mathf.Clamp01((s - start(i, p, t)) / p.fall);
  return at(b, 0, 0, p.altitude * up * (1 - fall * fall));
}
function spear(key, tipPos, length, width, colour, layer) {
  // Diamond shaft: a narrow centre ridge and asymmetric shading preserve solid mass.
  const left = [tipPos, at(tipPos, -width / 2, length * 0.3), at(tipPos, -width * 0.28, length * 0.92), at(tipPos, 0, length)];
  const right = [tipPos, at(tipPos, width / 2, length * 0.3), at(tipPos, width * 0.28, length * 0.92), at(tipPos, 0, length)];
  const ridge = [tipPos, at(tipPos, width * 0.05, length * 0.3), at(tipPos, width * 0.05, length * 0.92), at(tipPos, 0, length)];
  band(`${key} left`, left, ridge, colour, layer);
  band(`${key} right`, ridge, right, Color.Lerp(colour, Rim, 0.12).withAlpha(colour.a), layer + 0.001);
  trail(`${key} rim`, left, 0.025, Rim.withAlpha(colour.a * 0.8), layer + 0.003);
}
export default {
  kit: 'Six Paths', label: 'Heavenfall Spears (sketch)',
  params: {
    actors: { label: 'Show target for scale', value: true, group: 'Showcase' },
    rise: P('Orbs ascend', 0.65, 0.3, 1.2, 0.05, 'Timing (s)'),
    form: P('Stretch into spears', 0.4, 0.2, 0.8, 0.05, 'Timing (s)'),
    aim: P('Hold and aim', 0.35, 0.15, 0.8, 0.05, 'Timing (s)'),
    interval: P('Time between drops', 0.17, 0.1, 0.35, 0.01, 'Timing (s)'),
    finalPause: P('Extra pause before final spear', 0.22, 0, 0.5, 0.02, 'Timing (s)'),
    fall: P('One spear falls in', 0.24, 0.12, 0.5, 0.02, 'Timing (s)'),
    stand: P('Embedded hold', 0.4, 0.1, 0.8, 0.05, 'Timing (s)'),
    recall: P('Reform and return', 0.65, 0.3, 1.2, 0.05, 'Timing (s)'),
    radius: P('Outer landing radius (cells)', 1.8, 1, 3, 0.1, 'Shape'),
    altitude: P('Suspended tip height (cells)', 5, 3, 7, 0.25, 'Shape'),
    length: P('Spear length (cells)', 3.3, 2, 5, 0.1, 'Shape'),
    width: P('Shaft width (cells)', 0.24, 0.15, 0.5, 0.01, 'Shape'),
    spin: P('Landing formation angle', 18, 0, 72, 3, 'Shape'),
    finalScale: P('Final spear size multiplier', 1.5, 1.1, 2, 0.1, 'Impact'),
    trails: P('Descent trail strength', 0.65, 0, 1, 0.05, 'Impact'),
    dust: P('Impact dust', 0.6, 0, 0.8, 0.05, 'Impact'),
    shake: P('Final strike shake', 0.19, 0, 0.2, 0.01, 'Impact'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [{ name: 'Ascend', t: 0 }, { name: 'Form spears', t: t.form },
    { name: 'Aim', t: t.aim }, ...Array.from({ length: 6 }, (_, i) => ({ name: i === 5 ? 'Final impact' : `Impact ${i + 1}`, t: start(i, p, t) + p.fall })),
    { name: 'Recall', t: t.recall }]; },
  events(p) { const t = times(p); return Array.from({ length: 6 }, (_, i) => ({
    t: start(i, p, t) + p.fall, type: 'shake', value: p.shake * (i === 5 ? 1 : 0.3),
  })); },
  draw(s, p, { origin }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    target(origin, p.actors);
    const form = smooth((s - t.form) / p.form), recall = smooth((s - t.recall) / p.recall);
    const fade = 1 - smooth((s - t.end + 0.25) / 0.25);
    for (let i = 0; i < 6; i++) {
      const b = base(origin, i, p), drop = start(i, p, t), hit = drop + p.fall;
      const big = i === 5 ? p.finalScale : 1;
      const landing = smooth((s - drop) / p.fall);
      const warning = smooth(s / p.rise) * (1 - smooth((s - hit) / 0.15));
      // Fixed ground marker plus an increasingly dark contact shadow communicate descent.
      circle(b, 0.45 * big, warning * (0.4 + 0.35 * Math.sin(s * 15) ** 2));
      circle(b, (0.8 - landing * 0.3) * big, warning * 0.25);
      sprite(b, 1.3 * big, 1.1 * big, Body.withAlpha((0.18 + landing * 0.35) * (1 - recall)), undefined, Floor + 0.001);
      const rawTip = tip(s, p, t, origin, i);
      const a = i / 6 * Math.PI * 2;
      const pos = { x: lerp(rawTip.x, origin.x + Math.cos(a) * 1.2, recall),
        z: lerp(rawTip.z, origin.z + Math.sin(a) * 0.8 - 2.3, recall) + Math.sin(recall * Math.PI) * 0.8 };
      const length = p.length * 0.60 * big * form * (1 - recall);
      const layer = Y + (origin.z - b.z + p.radius) * 0.015;
      if (s < t.aim || recall > 0) orb(at(pos, 0, length * 0.4), 0.30 * big,
        (s < t.aim ? 1 - form : recall) * fade, 1 + form * (1 - recall) * 1.3, layer + 0.008);
      if (form > 0 && recall < 1) spear(`heavenfall ${i}`, pos, length, p.width * big * form, Body.withAlpha(fade), layer);
      if (s >= drop && s < hit + 0.12) {
        const pts = Array.from({ length: 20 }, (_, j) => tip(Math.min(s, hit) - (1 - j / 19) * 0.12, p, t, origin, i));
        const alpha = p.trails * (1 - smooth((s - hit) / 0.12));
        trail(`heavenfall wake ${i}`, pts, p.width * big * 1.5, Rim.withAlpha(alpha * 0.4), layer - 0.005);
        trail(`heavenfall streak ${i}`, pts, 0.045 * big, new Color(0.85, 0.75, 1, alpha), layer + 0.005);
      }
      // Ascending wisps make the orbs' upward travel visible before weapon formation.
      if (s < t.form) {
        const pts = Array.from({ length: 16 }, (_, j) => tip(Math.max(0, s - (1 - j / 15) * 0.2), p, t, origin, i));
        trail(`heavenfall rise ${i}`, pts, 0.075, Rim.withAlpha(0.35), layer - 0.005);
      }
      impact(`heavenfall impact ${i}`, b, s - hit, i === 5 ? 1 : 0.55, 0.7 * big, p.dust);
      if (s > hit) circle(b, 0.27 * big, 0.4 * (1 - recall), Floor, Body);
      if (i === 5 && s >= t.drop && s < drop) sprite(at(rawTip, 0, length * 0.5), 1.5, length * 1.5,
        Rim.withAlpha(0.18 + Math.sin(s * 18) ** 2 * 0.12), glow, layer - 0.002);
    }
  },
};
