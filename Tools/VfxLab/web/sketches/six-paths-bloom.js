// Obsidian Bloom — standalone VFX proposal, not gameplay damage logic.
// One orb sinks (0.4 s), six petals unfurl (0.65 s), poise (0.3 s), snap shut
// (0.16 s), pulse along their seams (0.45 s), then uncurl (0.65 s) and reform.
// Curved petal strips project real height north by Lift; shadows stay at their roots.
import { Color, Mathf } from '../js/engine.js';
import { P, Body, Rim, Y, Floor, at, sprite, orb, circle, band, trail, impact, target, glow } from './lib/six-paths-impact.js';
const smooth = Mathf.Smooth, lerp = Mathf.Lerp;
function times(p) {
  const poise = p.sink + p.unfold, snap = poise + p.poise, hit = snap + p.snap;
  const open = hit + p.hold, dissolve = open + p.open;
  return { poise, snap, hit, open, dissolve, end: dissolve + p.reform };
}
function petal(s, p, t, o, i, echo = 0) {
  s -= echo;
  const grow = smooth((s - p.sink - i * 0.025) / (p.unfold - 0.125));
  const shut = smooth((s - t.snap) / p.snap) * (1 - smooth((s - t.open) / p.open));
  const dissolve = 1 - smooth((s - t.dissolve) / p.reform);
  if (grow <= 0 || dissolve <= 0) return;
  const angle = i / 6 * Math.PI * 2 + p.spin * Mathf.Deg2Rad;
  const radius = p.radius * grow * dissolve;
  const left = [], right = [], spine = [], seam = [], shadowL = [], shadowR = [];
  for (let j = 0; j <= 32; j++) {
    const u = j / 32;
    // At closure the tip returns over the centre while the belly bulges outward.
    const r = lerp(0.18 + radius * u, 0.28 * (1 - u) + radius * 0.47 * Math.sin(u * Math.PI), shut);
    const h = lerp(Math.sin(u * Math.PI) * 0.25 * grow, p.height * u * grow, shut) * dissolve;
    const w = Math.sin(u * Math.PI) ** 0.85 * radius * 0.40 * (1 - shut * 0.2);
    const x = Math.cos(angle) * r, z = Math.sin(angle) * r;
    left.push(at(o, x - Math.sin(angle) * w, z + Math.cos(angle) * w, h));
    right.push(at(o, x + Math.sin(angle) * w, z - Math.cos(angle) * w, h));
    spine.push(at(o, x, z, h + Math.sin(u * Math.PI) * 0.14));
    seam.push(at(o, x - Math.sin(angle) * w * 0.96, z + Math.cos(angle) * w * 0.96, h));
    shadowL.push(at(o, x - Math.sin(angle) * w, z + Math.cos(angle) * w));
    shadowR.push(at(o, x + Math.sin(angle) * w, z - Math.cos(angle) * w));
  }
  const layer = Y + (1 - Math.sin(angle)) * 0.035;
  const pulse = s >= t.hit && s < t.open ? Math.sin((s - t.hit) / p.hold * Math.PI) : 0;
  if (echo) {
    band(`bloom echo ${i} ${echo}`, left, right, Rim.withAlpha(0.1), layer - 0.005);
    return;
  }
  band(`bloom shadow ${i}`, shadowL, shadowR, Body.withAlpha(0.28 * dissolve), Floor + 0.002);
  band(`bloom left ${i}`, left, spine, Body.withAlpha(dissolve), layer);
  band(`bloom right ${i}`, spine, right, new Color(0.075, 0.055, 0.105, dissolve), layer + 0.001);
  band(`bloom seam ${i}`, left, seam, Rim.withAlpha((0.65 + pulse * 0.35) * dissolve), layer + 0.003);
  trail(`bloom spine ${i}`, spine, 0.025 + pulse * 0.035, Rim.withAlpha((0.25 + pulse * 0.65) * dissolve), layer + 0.004);
}
export default {
  kit: 'Six Paths', label: 'Obsidian Bloom (sketch)',
  params: {
    actors: { label: 'Show target for scale', value: true, group: 'Showcase' },
    sink: P('Orb sinks', 0.4, 0.2, 1, 0.05, 'Timing (s)'),
    unfold: P('Petals unfurl', 0.65, 0.3, 1.2, 0.05, 'Timing (s)'),
    poise: P('Open flower anticipation', 0.3, 0.1, 0.8, 0.05, 'Timing (s)'),
    snap: P('Snap shut', 0.16, 0.08, 0.4, 0.02, 'Timing (s)'),
    hold: P('Closed seam pulse', 0.45, 0.2, 1, 0.05, 'Timing (s)'),
    open: P('Uncurl', 0.65, 0.3, 1.2, 0.05, 'Timing (s)'),
    reform: P('Reform one orb', 0.5, 0.2, 1, 0.05, 'Timing (s)'),
    radius: P('Open petal reach (cells)', 2.8, 1.5, 4, 0.1, 'Shape'),
    height: P('Closed lotus height (cells)', 3.2, 2, 4.5, 0.1, 'Shape'),
    spin: P('Flower angle (degrees)', 15, 0, 60, 5, 'Shape'),
    dust: P('Ground spray', 0.5, 0, 0.8, 0.05, 'Impact'),
    shake: P('Closure shake', 0.14, 0, 0.2, 0.01, 'Impact'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [{ name: 'Sink', t: 0 }, { name: 'Unfurl', t: p.sink },
    { name: 'Poise', t: t.poise }, { name: 'Snap', t: t.snap }, { name: 'Closed lotus', t: t.hit },
    { name: 'Uncurl', t: t.open }, { name: 'Reform', t: t.dissolve }]; },
  events(p) { return [{ t: times(p).hit, type: 'shake', value: p.shake }]; },
  draw(s, p, { origin }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    target(origin, p.actors);
    const sunk = smooth(s / p.sink), reform = smooth((s - t.dissolve) / p.reform);
    const fade = 1 - smooth((s - t.end + 0.15) / 0.15);
    sprite(origin, p.radius * 2 * sunk, p.radius * 2 * sunk, Body.withAlpha(0.3 * (1 - reform)), undefined, Floor);
    circle(origin, p.radius * 0.95, 0.2 * sunk * (1 - smooth((s - t.snap) / p.snap)));
    if (s < p.sink) orb(at(origin, 0, 0, (1 - sunk) * 0.6), 0.38, 1, Math.max(0.01, 1 - sunk));
    // Curl trails follow earlier petal poses, only during the fast closing stroke.
    for (let i = 0; i < 6; i++) {
      if (s >= t.snap && s < t.hit) for (const lag of [0.055, 0.025]) petal(s, p, t, origin, i, lag);
      petal(s, p, t, origin, i);
    }
    impact('bloom close', origin, s - t.hit, 0.85, p.radius * 0.65, p.dust);
    const pulse = Math.sin(Mathf.Clamp01((s - t.hit) / p.hold) * Math.PI);
    sprite(at(origin, 0, 0, p.height), 1.1, 1.1, Rim.withAlpha(pulse * 0.65), glow, Y + 0.12);
    if (reform > 0) orb(at(origin, 0, 0, reform * 0.6), 0.38 * reform, fade);
  },
};
