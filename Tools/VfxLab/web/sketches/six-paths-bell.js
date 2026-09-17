// Toll of Oblivion — three-orb Six Paths VFX proposal, not gameplay cost/damage logic.
// Three orbs rise, become an anchor slab, hollow bell, and hammer, then strike once.
// The hammer fractures on contact. Bell recoil emits three ground shock fronts;
// the anchor cracks and the bell sheds pieces until the final pulse consumes it.
// No recall or orb regeneration: a game port must spend three orbs independently.
// Default: rise .65, form .65, windup .65, swing .22, pulse interval .55 seconds.
import { Color, Mathf, Meshes, MaterialPool, ShaderDatabase } from '../js/engine.js';
import { draw, mesh } from './lib/six-paths-solid.js';
import { P, Body, Rim, Lift, Y, Floor, at, sprite, orb, band, trail, rand, glow } from './lib/six-paths-impact.js';
const smooth = Mathf.Smooth, lerp = Mathf.Lerp, clamp = Mathf.Clamp01, TAU = Math.PI * 2;
const disc = Meshes.disc(64, 'bell mouth');
const crownRing = Meshes.band(0.65, 1, 48, 'bell crown loop');
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const shade = new Color(0.105, 0.078, 0.15);
function times(p) {
  const form = p.rise, wind = form + p.form, swing = wind + p.wind;
  const hit = swing + p.swing, final = hit + p.interval * 2;
  return { form, wind, swing, hit, final, end: final + Math.max(p.decay + 0.35, p.waveLife) + 0.2 };
}
function polygon(key, points, colour, layer) {
  const m = mesh(key), vertices = points.flatMap(q => [q.x, q.z]), tri = [];
  for (let i = 1; i < points.length - 1; i++) tri.push(0, i, i + 1);
  m.setFlat(vertices, tri); draw(m, 0, layer, 0, 1, 1, 0, colour);
}
function hammerPose(s, p, t, o) {
  const wind = smooth((s - t.wind) / p.wind);
  const swing = clamp((s - t.swing) / p.swing);
  const angle = lerp(1.9, 0.85, wind) + (2.72 - 0.85) * swing ** 2.4;
  const pivot = at(o, 3.8 * p.scale, 0, p.height + 0.1);
  const head = at(pivot, Math.cos(angle) * 3.2 * p.scale, 0, Math.sin(angle) * 3.2 * p.scale);
  return { pivot, head, angle };
}
function hammer(s, p, t, o, formed) {
  if (s >= t.hit || formed <= 0) return;
  const q = hammerPose(s, p, t, o);
  const dx = q.head.x - q.pivot.x, dz = q.head.z - q.pivot.z, len = Math.hypot(dx, dz);
  const ux = dx / len, uz = dz / len, vx = -uz, vz = ux;
  const h = p.scale * formed;
  for (const [key, width, colour, layer] of [['rim', 0.095, Rim, Y + 0.06], ['body', 0.065, Body, Y + 0.061]]) {
    band(`bell hammer handle ${key}`,
      [at(q.pivot, vx * width * h, vz * width * h), at(q.head, vx * width * h, vz * width * h)],
      [at(q.pivot, -vx * width * h, -vz * width * h), at(q.head, -vx * width * h, -vz * width * h)], colour, layer);
  }
  const corner = (u, v) => at(q.head, (ux * u + vx * v) * h, (uz * u + vz * v) * h);
  const outline = [corner(-0.48, -0.68), corner(0.40, -0.68), corner(0.52, -0.48), corner(0.52, 0.48), corner(0.40, 0.68), corner(-0.48, 0.68)];
  polygon('bell hammer head', outline, Body, Y + 0.065);
  polygon('bell hammer bevel', [corner(-0.48, -0.68), corner(0.40, -0.68), corner(0.52, -0.48), corner(-0.35, -0.48)], shade, Y + 0.066);
  trail('bell hammer outline', [...outline, outline[0]], 0.035, Rim, Y + 0.067);
  if (s >= t.swing) {
    const pts = Array.from({ length: 28 }, (_, i) => hammerPose(Math.max(t.swing, s - (1 - i / 27) * p.swing * 0.7), p, t, o).head);
    trail('bell hammer wake', pts, 0.85 * h, Rim.withAlpha(p.trails * 0.22), Y + 0.045);
    trail('bell hammer streak', pts, 0.065 * h, new Color(0.85, 0.74, 1, p.trails), Y + 0.048);
  }
}
function bell(s, p, t, o, formed, echo = 0) {
  const age = Math.max(0, s - echo - t.hit), scale = p.scale * formed;
  const decay = smooth((s - t.final) / p.decay);
  if (scale <= 0 || decay >= 1) return;
  const vibration = s >= t.hit ? Math.sin(age * TAU / p.interval) * Math.exp(-age * 1.2) : 0;
  const swing = vibration * 0.15;
  const base = p.height + 0.35, height = 3.2 * scale;
  const position = (x, h, depth = 0) => at(o,
    x + Math.sin(swing) * (h - height), depth,
    base + height + Math.cos(swing) * (h - height));
  // Broad, flared silhouette with a curved shoulder and an open lower lip.
  const left = [], right = [], spine = [];
  for (let i = 0; i <= 40; i++) {
    const u = i / 40;
    const cap = u > 0.8 ? Math.sqrt(Math.max(0, 1 - ((u - 0.8) / 0.2) ** 2)) : 1;
    const radius = scale * (0.37 + 1.02 * (1 - u) ** 3 + 0.18 * Math.sin(u * Math.PI)) * cap;
    const flex = 1 + vibration * 0.055 * Math.sin(u * Math.PI * 2 + age * 18);
    left.push(position(-radius * flex, u * height));
    right.push(position(radius * flex, u * height));
    spine.push(position(-radius * 0.17, u * height));
  }
  if (echo) {
    band(`bell echo ${echo}`, left, right, Rim.withAlpha(0.10 * p.trails * (1 - decay)), Y + 0.009);
    return;
  }
  const alpha = 1 - decay;
  const crown = position(0, height + 0.1 * scale);
  draw(crownRing, crown.x, Y + 0.024, crown.z, 0.19 * scale, 0.22 * scale, 0, Rim.withAlpha(alpha * 0.7));
  band('bell shell left', left, spine, shade.withAlpha(alpha), Y + 0.025);
  band('bell shell right', spine, right, Body.withAlpha(alpha), Y + 0.026);
  trail('bell left rim', left, 0.035 * scale, Rim.withAlpha(alpha * 0.8), Y + 0.028);
  trail('bell right rim', right, 0.025 * scale, Rim.withAlpha(alpha * 0.55), Y + 0.028);
  const mouth = position(0, 0);
  draw(disc, mouth.x, Y + 0.030, mouth.z, 1.42 * scale, 0.35 * scale, 0, Rim.withAlpha(alpha * 0.8));
  draw(disc, mouth.x, Y + 0.032, mouth.z + 0.015, 1.35 * scale, 0.28 * scale, 0, new Color(0.012, 0.009, 0.018, alpha));
  // Curved etched bands bend with the bell; their brightness follows each ring.
  const pulse = s >= t.hit ? Math.max(0, Math.cos(age * TAU / p.interval)) * Math.exp(-age * 0.5) : 0;
  for (let j = 0; j < 3; j++) {
    const u = 0.16 + j * 0.3, radius = scale * (0.37 + 1.02 * (1 - u) ** 3 + 0.18 * Math.sin(u * Math.PI));
    const pts = Array.from({ length: 32 }, (_, i) => {
      const a = Math.PI + i / 31 * Math.PI;
      return position(Math.cos(a) * radius, height * u, Math.sin(a) * radius * 0.22);
    });
    trail(`bell engraving ${j}`, pts, 0.025 + pulse * 0.025, Rim.withAlpha(alpha * (0.3 + pulse * 0.65)), Y + 0.034);
  }
  if (s >= t.hit) for (let i = 0; i < 5; i++) {
    const pts = Array.from({ length: 8 }, (_, j) => {
      const u = j / 7;
      return position((i - 2) * scale * 0.25 + Math.sin(j * 2.1 + i) * 0.09,
        height * (0.1 + u * 0.7 * clamp(age / (p.interval * 2))));
    });
    trail(`bell fissure ${i}`, pts, 0.018 + decay * 0.025, Rim.withAlpha(alpha * (0.3 + pulse * 0.6)), Y + 0.035);
  }
}
function anchor(s, p, t, o, formed) {
  const gone = smooth((s - t.final) / (p.decay * 0.8));
  const alpha = 1 - gone, size = p.scale * formed;
  if (size <= 0 || alpha <= 0) return;
  const shake = s > t.hit ? Math.sin((s - t.hit) * 55) * 0.045 * Math.exp(-(s - t.hit)) : 0;
  const b = at(o, shake, 0, p.height);
  const corners = [[-2.1, -0.65], [1.75, -0.65], [2.1, 0.55], [-1.75, 0.55]].map(([x,z]) => at(b, x * size, z * size));
  polygon('bell anchor top', corners, shade.withAlpha(alpha), Y);
  polygon('bell anchor thickness', [corners[0], corners[1], at(corners[1], 0, -0.23 * size), at(corners[0], 0, -0.23 * size)], Body.withAlpha(alpha), Y + 0.002);
  trail('bell anchor edge', [...corners, corners[0]], 0.035, Rim.withAlpha(alpha * 0.65), Y + 0.004);
  for (const side of [-1, 1]) {
    const pts = [at(b, side * 1.7 * size, 0), at(b, side * 1.45 * size, 0.24), at(b, side * 1.38 * size, 0.36)];
    trail(`bell anchor clamp ${side}`, pts, 0.18 * size, Body.withAlpha(alpha), Y + 0.038);
  }
  if (s >= t.hit) for (let j = 0; j < 5; j++) {
    const a = j * 2.4, reach = size * clamp((s - t.hit) / p.interval);
    const pts = [b, at(b, Math.cos(a) * reach * 0.6, Math.sin(a) * reach * 0.3),
      at(b, Math.cos(a + 0.2) * reach * 1.7, Math.sin(a + 0.2) * reach * 0.55)];
    trail(`bell anchor crack ${j}`, pts, 0.035, Rim.withAlpha(alpha * 0.8), Y + 0.007);
  }
}
function shockwave(s, p, t, o, pulse) {
  const age = s - t.hit - pulse * p.interval;
  if (age < 0 || age >= p.waveLife) return;
  const u = age / p.waveLife, radius = p.radius * (1 - (1 - u) ** 2);
  const alpha = (1 - u) ** 1.4 * (pulse === 0 ? 1 : 0.7);
  for (let segment = 0; segment < 12; segment++) {
    const outer = [], inner = [];
    for (let i = 0; i <= 16; i++) {
      const a = (segment + i / 16 * 0.88) / 12 * TAU;
      const r = radius * (1 + Math.sin(a * 9 + pulse) * 0.022 + Math.sin(a * 17) * 0.012);
      const width = (0.05 + 0.14 * (1 - u)) * Math.sin(i / 16 * Math.PI);
      outer.push(at(o, Math.cos(a) * r, Math.sin(a) * r));
      inner.push(at(o, Math.cos(a) * Math.max(0, r - width), Math.sin(a) * Math.max(0, r - width)));
    }
    band(`bell wave ${pulse} ${segment}`, outer, inner, Rim.withAlpha(alpha * (0.3 + rand(segment) * 0.4)), Floor + 0.04);
  }
  for (let i = 0; i < 36; i++) {
    const a = i / 36 * TAU + pulse * 0.2;
    const pos = at(o, Math.cos(a) * radius, Math.sin(a) * radius, Math.sin(u * Math.PI) * (0.2 + rand(i) * 0.5));
    sprite(pos, 0.45 + u * 1.5, 0.4 + u, new Color(0.54, 0.48, 0.39, alpha * p.dust * (0.3 + rand(i + 20) * 0.5)), puff, Y - 0.04);
    if (i % 3 === 0) {
      const pts = [at(pos, -Math.cos(a) * 0.4, -Math.sin(a) * 0.4), pos, at(pos, Math.cos(a) * 0.18, Math.sin(a) * 0.18)];
      trail(`bell wave grit ${pulse} ${i}`, pts, 0.065 * (1 - u), Body.withAlpha(alpha), Y - 0.02);
    }
  }
}
function fragments(s, p, t, o, kind) {
  const born = kind === 0 ? t.hit : t.final;
  const age = s - born, life = kind === 0 ? 0.9 : p.decay + 0.35;
  if (age < 0 || age >= life) return;
  const origin = kind === 0 ? hammerPose(t.hit, p, t, o).head : at(o, 0, 0, p.height);
  for (let i = 0; i < 22; i++) {
    const a = i * 2.399 + kind, speed = (0.6 + rand(i + kind * 22) * 2.8) * p.scale;
    const u = age / life, height = Math.max(-p.height * 0.4, speed * age * 0.5 - 2.5 * age * age);
    const shellH = rand(i + 90), shellR = 0.37 + 1.02 * (1 - shellH) ** 3;
    const source = kind === 0 ? at(origin, (rand(i + 70) - 0.5) * 0.6, (rand(i + 80) - 0.5) * 0.9)
      : kind === 1 ? at(origin, Math.cos(a) * shellR * p.scale, 0, 0.35 + shellH * 3.2 * p.scale)
        : at(origin, (rand(i + 70) - 0.5) * 3.8 * p.scale, (rand(i + 80) - 0.5) * p.scale);
    const pos = at(source, Math.cos(a) * speed * age, Math.sin(a) * speed * age * 0.5, height);
    const size = (0.08 + rand(i + 44) * 0.18) * p.scale * (1 - u);
    const angle = a + age * (i % 2 ? 5 : -5);
    const points = [at(pos, Math.cos(angle) * size, Math.sin(angle) * size),
      at(pos, Math.cos(angle + 2.2) * size, Math.sin(angle + 2.2) * size),
      at(pos, Math.cos(angle + 4.5) * size * 1.8, Math.sin(angle + 4.5) * size * 1.8)];
    polygon(`bell fragment ${kind} ${i}`, points, (i % 4 ? Body : Rim).withAlpha(1 - u), Y + 0.08);
  }
}
export default {
  kit: 'Six Paths', label: 'Toll of Oblivion (sketch)',
  params: {
    rise: P('Three orbs rise', 0.65, 0.3, 1.2, 0.05, 'Timing (s)'),
    form: P('Assemble bell, hammer, anchor', 0.65, 0.3, 1.2, 0.05, 'Timing (s)'),
    wind: P('Hammer draws back', 0.65, 0.3, 1.2, 0.05, 'Timing (s)'),
    swing: P('Hammer strike', 0.22, 0.12, 0.45, 0.01, 'Timing (s)'),
    interval: P('Resonance pulse interval', 0.55, 0.3, 0.9, 0.05, 'Timing (s)'),
    decay: P('Bell and anchor fracture', 0.75, 0.4, 1.2, 0.05, 'Timing (s)'),
    waveLife: P('Shock front lifetime', 1.15, 0.6, 1.8, 0.05, 'Timing (s)'),
    scale: P('Structure size', 1, 0.7, 1.3, 0.05, 'Shape'),
    height: P('Anchor height (cells)', 2, 1, 3, 0.1, 'Shape'),
    radius: P('Shockwave reach (cells)', 7, 4, 10, 0.25, 'Impact'),
    trails: P('Hammer and bell trails', 0.8, 0, 1, 0.05, 'Impact'),
    dust: P('Shockwave dust', 0.7, 0, 1, 0.05, 'Impact'),
    shake: P('Strike camera shake', 0.2, 0, 0.2, 0.01, 'Impact'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [{ name: 'Three orbs', t: 0 }, { name: 'Assembly', t: t.form },
    { name: 'Windup', t: t.wind }, { name: 'Swing', t: t.swing }, { name: 'Strike / hammer breaks', t: t.hit },
    { name: 'Second toll', t: t.hit + p.interval }, { name: 'Final toll / fracture', t: t.final },
    { name: 'Consumed', t: t.final + p.decay + 0.35 }]; },
  events(p) { const t = times(p); return [0, 1, 2].map(i => ({ t: t.hit + i * p.interval,
    type: 'shake', value: p.shake * (i === 0 ? 1 : i === 1 ? 0.5 : 0.7) })); },
  draw(s, p, { origin }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const rise = smooth(s / p.rise), formed = smooth((s - t.form) / p.form);
    const remains = 1 - smooth((s - t.final) / p.decay);
    sprite(origin, 5 * p.scale * formed, 3 * p.scale * formed, Body.withAlpha(0.3 * remains), undefined, Floor);
    if (s < t.wind) for (let i = 0; i < 3; i++) {
      const end = i === 0 ? at(origin, 0, 0, p.height) : i === 1 ? at(origin, 0, 0, p.height + 1.7 * p.scale) : hammerPose(0, p, t, origin).head;
      const start = at(origin, (i - 1) * 0.9, -1);
      const pose = u => ({ x: lerp(start.x, end.x, u) + Math.sin(u * Math.PI) * (i - 1) * 0.5, z: lerp(start.z, end.z, u) });
      const pts = Array.from({ length: 20 }, (_, j) => pose(smooth(Math.max(0, s - (1 - j / 19) * 0.18) / p.rise)));
      trail(`bell ascending orb ${i}`, pts, 0.10, Rim.withAlpha(0.45 * (1 - formed)), Y + 0.09);
      orb(pose(rise), 0.34, 1 - formed, 1 + formed * 0.5, Y + 0.095);
    }
    anchor(s, p, t, origin, formed);
    if (s >= t.hit && s < t.final + p.decay) for (const echo of [0.06, 0.03]) bell(s, p, t, origin, formed, echo);
    bell(s, p, t, origin, formed);
    hammer(s, p, t, origin, formed);
    if (s >= t.wind && s < t.hit) for (let i = 0; i < 18; i++) {
      const u = clamp((s - t.wind) / (p.wind + p.swing)), a = i * 2.399;
      const r = lerp(3.5 + rand(i) * 1.5, 1.2, u);
      const pos = at(origin, Math.cos(a) * r, Math.sin(a) * r);
      sprite(pos, 0.35 + u * 0.25, 0.25 + u * 0.2,
        new Color(0.53, 0.47, 0.38, Math.sin(u * Math.PI) * p.dust * 0.28), puff, Floor + 0.015);
    }
    for (let i = 0; i < 3; i++) {
      shockwave(s, p, t, origin, i);
      fragments(s, p, t, origin, i);
    }
    const flash = Math.max(0, 1 - (s - t.hit) / 0.13);
    if (s >= t.hit && flash > 0) {
      const contact = hammerPose(t.hit, p, t, origin).head;
      sprite(contact, 3 * p.scale, 2.5 * p.scale, new Color(0.9, 0.8, 1, flash), glow, Y + 0.14);
    }
  },
};
