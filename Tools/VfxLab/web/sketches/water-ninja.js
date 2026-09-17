// Water Ninja: Tidecutter — a new, standalone proposal inspired by water-ninja animation.
// Default sequence: gather 0–0.85 s, hold a water shuriken 0.85–1.30 s, throw
// 1.30–1.72 s, water burst 1.72–2.10 s, splash and settle until 3.35 s.
// Cel-shaped water, tapered foam, ballistic droplets; procedural water texture (lab-only), no game changes.
import { Mathf } from '../js/engine.js';
import { hash } from '../js/standins.js';
import { TAU, clamp, smooth, ink, blue, aqua, foam, disc, altitude, floor, mesh, P, draw, ribbon, stream, arc, droplet } from './lib/water-ninja-water.js';

function shuriken(key, x, z, radius, spin, alpha, p) {
  // Four bent blades with a dark cut edge and a narrow foam lip. Solid geometry,
  // rather than an additive starburst, keeps the silhouette readable on terrain.
  for (let blade = 0; blade < 4; blade++) {
    const a = spin + blade * TAU / 4;
    const shape = [[0, 0], [0.28, 0.27], [0.73, 0.31], [1, 0.08], [0.52, -0.02], [0.24, -0.23]];
    const m = mesh(`${key}-blade-${blade}`);
    m.setFlat(shape.flat(), [0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5]);
    const rotation = -a * Mathf.Rad2Deg;
    draw(m, x, z, radius * 1.07, radius * 1.07, rotation, ink, alpha, altitude + 0.025);
    draw(m, x, z, radius, radius, rotation, blue, alpha, altitude + 0.027);
    const edge = shape.slice(1, 4).map(([u, v]) => [x + (u * Math.cos(a) - v * Math.sin(a)) * radius,
      z + (u * Math.sin(a) + v * Math.cos(a)) * radius]);
    ribbon(`${key}-edge-${blade}`, edge, radius * 0.11, aqua, alpha, altitude + 0.03);
    ribbon(`${key}-lip-${blade}`, edge, radius * 0.027, foam, alpha * p.foam, altitude + 0.032);
  }
  arc(`${key}-hub`, x, z, radius * 0.19, spin, TAU, radius * 0.12, alpha, p.foam, 1, altitude + 0.04);
}

function timing(p) {
  const release = p.gather + p.hold, hit = release + p.travel, finish = hit + p.cut;
  return { release, hit, finish, end: finish + p.settle };
}

export default {
  kit: 'Water Ninja',
  label: 'Tidecutter showcase (sketch)',
  params: {
    gather: P('Water gathers', 0.85, 0.2, 2, 0.05, 'Timing (s)'),
    hold: P('Shuriken anticipation', 0.45, 0.1, 1.5, 0.05, 'Timing (s)'),
    travel: P('Throw flight', 0.42, 0.15, 1.2, 0.01, 'Timing (s)'),
    cut: P('Water burst', 0.38, 0.2, 0.9, 0.01, 'Timing (s)'),
    settle: P('Splash settles', 1.25, 0.4, 2.5, 0.05, 'Timing (s)'),
    heading: P('Aim (degrees from east)', 25, -180, 180, 5, 'Shape'),
    reach: P('Throw distance (cells)', 5.5, 2, 10, 0.25, 'Shape'),
    size: P('Shuriken radius (cells)', 1.15, 0.4, 2.3, 0.05, 'Shape'),
    splash: P('Splash radius (cells)', 2.1, 0.7, 4, 0.1, 'Shape'),
    spin: P('Spin (turns per second)', 1.7, 0.25, 4, 0.05, 'Style'),
    foam: P('White foam', 0.9, 0, 1, 0.05, 'Style'),
    droplets: P('Splash droplets', 28, 8, 48, 1, 'Style'),
    wake: { label: 'Water trails', value: true, group: 'Style' },
  },
  duration(p) { return timing(p).end; },
  phases(p) {
    const t = timing(p);
    return [{ name: 'Gather', t: 0 }, { name: 'Water shuriken', t: p.gather },
      { name: 'Throw', t: t.release }, { name: 'Water burst', t: t.hit },
      { name: 'Foam / settle', t: t.finish }];
  },
  events(p) { return [{ t: timing(p).hit, type: 'shake', value: 0.09 }]; },
  draw(seconds, p, { origin }) {
    const t = timing(p);
    if (seconds < 0 || seconds >= t.end) return;
    const aim = p.heading * Mathf.Deg2Rad;
    const point = (distance, side = 0) => [origin.x + Math.cos(aim) * distance - Math.sin(aim) * side,
      origin.z + Math.sin(aim) * distance + Math.cos(aim) * side];
    const fade = 1 - smooth((seconds - t.finish) / p.settle);
    const gathered = smooth(seconds / p.gather);

    // Thin pools anchor the water to the floor, with broken elliptical ripples.
    const poolAlpha = gathered * (1 - smooth((seconds - t.release) / 0.8));
    draw(disc, origin.x, origin.z, 1.4, 0.76, 0, ink, poolAlpha * 0.30, floor);
    for (let i = 0; i < 3; i++) {
      arc(`gather-${i}`, origin.x, origin.z + 0.1, (1.7 - gathered * 0.55) + i * 0.13,
        seconds * 3.4 + i * TAU / 3, 1.65, 0.14 + gathered * 0.10,
        poolAlpha, p.foam, 0.62, altitude - 0.12);
    }

    if (p.wake && seconds > t.release && seconds < t.hit + 0.45) {
      const head = Math.min(seconds, t.hit);
      const wakeFade = 1 - smooth((seconds - t.hit) / 0.45);
      for (let j = 0; j < 3; j++) {
        const pts = Array.from({ length: 33 }, (_, i) => {
          const u = i / 32;
          const emitted = Math.max(t.release, head - 0.24 * (1 - u));
          const age = seconds - emitted;
          const distance = 0.65 + (p.reach - 0.65) * clamp((emitted - t.release) / p.travel);
          const side = (j - 1) * 0.23 + Math.sin(emitted * 19 + j * 2) * (0.10 + age * 0.3);
          const q = point(distance, side);
          return [q[0], q[1] + 0.55 - age * age * 2];
        });
        stream(`flight-${j}`, pts, 0.22 + j * 0.055, wakeFade, p.foam, seconds * 12 + j);
      }
      for (let i = 0; i < 16; i++) {
        const emitted = t.release + p.travel * (i / 16);
        const age = seconds - emitted;
        if (age < 0 || age > 0.45) continue;
        const q = point(0.65 + (p.reach - 0.65) * i / 16,
          (hash(i, 7, 419) - 0.5) * (0.35 + age * 2));
        droplet(`wake-drop-${i}`, q[0], q[1] + 0.55 - age * age * 3,
          0.045 + hash(i, 8, 419) * 0.05, -p.heading, (1 - age / 0.45) * wakeFade, p.foam);
      }
    }

    if (seconds < t.hit) {
      const flight = clamp((seconds - t.release) / p.travel);
      const distance = 0.65 + (p.reach - 0.65) * flight;
      const [x, z] = point(distance);
      const radius = p.size * smooth(seconds / p.gather);
      const alpha = smooth(seconds / 0.18);
      shuriken('star', x, z + 0.55, radius, seconds * TAU * p.spin, alpha, p);
      if (seconds > p.gather * 0.45) {
        arc('spin-swish', x, z + 0.55, radius * 1.17, seconds * TAU * p.spin,
          3.7, 0.09, alpha * 0.65, p.foam);
      }
      for (let i = 0; i < 8; i++) {
        const a = i * TAU / 8 + seconds * 3;
        const r = (1 - gathered) * 1.2 + radius * 1.3;
        droplet(`gather-drop-${i}`, x + Math.cos(a) * r, z + 0.55 + Math.sin(a) * r * 0.8,
          0.10, -a * Mathf.Rad2Deg, alpha * 0.8, p.foam);
      }
    }

    if (seconds >= t.hit) {
      const age = seconds - t.hit;
      const [x, z] = point(p.reach);
      const spread = smooth(age / 0.5);
      draw(disc, x, z, p.splash * (0.3 + spread * 0.7), p.splash * 0.52,
        0, blue, fade * 0.19, floor);

      // The blade collapses into a short pressure pulse and rounded water lobes.
      // No crossing blades: translucent streams open out and fall into the existing splash.
      const burst = clamp(age / p.cut);
      const burstAlpha = 1 - smooth(burst);
      for (let j = 0; j < 5; j++) {
        const angle = aim + (j - 2) * 0.8;
        const reach = p.splash * (0.25 + Math.sin(burst * Math.PI * 0.65) * 0.72);
        const pts = Array.from({ length: 33 }, (_, i) => {
          const u = i / 32;
          const r = reach * u;
          return [x + Math.cos(angle) * r,
            z + 0.25 + Math.sin(angle) * r * 0.65 + Math.sin(u * Math.PI) * reach * 0.6];
        });
        stream(`burst-${j}`, pts, p.splash * (0.16 - burst * 0.08),
          burstAlpha, p.foam, j + age * 10, altitude + 0.05);
      }
      // A tight foamy contact, quickly opening into gaps rather than a large flash.
      for (let i = 0; i < 9; i++) {
        const a = i * TAU / 9;
        const r = p.splash * (0.04 + burst * 0.30);
        draw(disc, x + Math.cos(a) * r, z + 0.35 + Math.sin(a) * r * 0.65,
          p.splash * 0.07 * (1 - burst), p.splash * 0.05 * (1 - burst),
          0, foam, burstAlpha * p.foam * 0.85, altitude + 0.07);
      }

      // Broken crown: wide curved water tongues instead of straight explosion rays.
      for (let i = 0; i < 7; i++) {
        const a = i * TAU / 7 + 0.3;
        const r = p.splash * (0.28 + spread * 0.73);
        arc(`crown-${i}`, x, z, r, a, 0.57, p.splash * 0.16 * (1 - spread * 0.6),
          (1 - smooth(age / 0.9)) * 0.85, p.foam, 0.60);
      }
      for (let i = 0; i < p.droplets; i++) {
        const a = hash(i, 1, 731) * TAU;
        const life = 0.45 + hash(i, 2, 731) * 0.65;
        const u = age / life;
        if (u > 1) continue;
        const r = p.splash * (0.25 + hash(i, 3, 731) * 0.95) * u;
        const height = Math.sin(u * Math.PI) * (0.45 + hash(i, 4, 731));
        droplet(`splash-${i}`, x + Math.cos(a) * r, z + Math.sin(a) * r * 0.65 + height,
          (0.09 + hash(i, 5, 731) * 0.13) * (1 - u * 0.55),
          -a * Mathf.Rad2Deg + 90, (1 - smooth(u)) * fade, p.foam);
      }
      for (let i = 0; i < 3; i++) {
        const rippleAge = age - i * 0.16;
        if (rippleAge < 0) continue;
        arc(`settle-${i}`, x, z, 0.35 + rippleAge * (1.2 + i * 0.16),
          i * 2.2, 4.9, 0.045, fade * 0.65 * smooth(rippleAge / 0.12), p.foam, 0.57, floor + 0.025);
      }
    }
  },
};
