// Undertow Dash: gather 0–0.18 s, dash 0.18–0.46 s, arrival splash 0.46–0.64 s,
// then puddles and ripples settle until 1.00 s. A watery pawn proxy shows motion;
// this sketch cannot move the lab's scene pawn. No game ability is replaced.
// Uses layered PNG wake sprites; the runner and arrival use Tidecutter's lab-only texture.
import { Mathf } from '../js/engine.js';
import { hash } from '../js/standins.js';
import { spriteWake } from './lib/water-wake-sprites.js';
import {
  TAU, smooth, blue, aqua, foam, disc, altitude, floor, P,
  draw, ribbon, stream, droplet,
} from './lib/water-ninja-water.js';

const arrival = p => p.gather + p.dash;
const end = p => arrival(p) + p.splashTime + p.settle;

// A crouched water silhouette, scaled like a pawn, makes the displacement readable.
function runner(key, point, distance, alpha, p, phase) {
  if (alpha <= 0.001) return;
  const [x, z] = point(distance);
  draw(disc, x, z, 0.40, 0.17, -p.heading, blue, alpha * 0.20, floor + 0.02);
  const head = point(distance + 0.18);
  draw(disc, head[0], head[1] + 0.70, 0.19, 0.21, 0, aqua, alpha * 0.64, altitude + 0.02);
  draw(disc, head[0] - 0.055, head[1] + 0.77, 0.08, 0.07, 0,
    foam, alpha * p.foam * 0.65, altitude + 0.025);
  const stroke = (suffix, from, to, width) => {
    const pts = Array.from({ length: 33 }, (_, i) => {
      const u = i / 32;
      const q = point(distance + from[0] + (to[0] - from[0]) * u, (from[2] ?? 0) * (1 - u));
      return [q[0], q[1] + from[1] + (to[1] - from[1]) * u];
    });
    stream(`${key}-${suffix}`, pts, width, alpha, p.foam, phase);
  };
  stroke('body', [-0.22, 0.24], [0.16, 0.65], 0.32);
  stroke('back-leg', [-0.65, 0.04, -0.12], [-0.18, 0.30], 0.16);
  stroke('front-leg', [0.23, 0.04, 0.15], [-0.18, 0.30], 0.17);
  stroke('arm', [-0.42, 0.36, 0.18], [0.08, 0.53], 0.12);
}

export default {
  kit: 'Water Ninja',
  label: 'Undertow Dash (sketch)',
  params: {
    gather: P('Water gathers at feet', 0.18, 0.08, 0.6, 0.01, 'Timing (s)'),
    dash: P('Dash travel', 0.28, 0.12, 0.8, 0.01, 'Timing (s)'),
    splashTime: P('Arrival splash', 0.18, 0.08, 0.5, 0.01, 'Timing (s)'),
    settle: P('Wake settles', 0.36, 0.15, 1.2, 0.01, 'Timing (s)'),
    heading: P('Aim (degrees from east)', 20, -180, 180, 5, 'Shape'),
    reach: P('Dash distance (cells)', 5, 2, 10, 0.25, 'Shape'),
    width: P('Wake width (cells)', 0.75, 0.3, 1.5, 0.05, 'Shape'),
    splash: P('Arrival radius (cells)', 1.25, 0.5, 2.5, 0.05, 'Shape'),
    foam: P('White foam', 0.9, 0, 1, 0.05, 'Style'),
    afterimages: { label: 'Two watery afterimages', value: true, group: 'Style' },
    runner: { label: 'Show water pawn proxy', value: true, group: 'Style' },
  },
  duration: end,
  phases(p) {
    return [{ name: 'Gather', t: 0 }, { name: 'Dash', t: p.gather },
      { name: 'Arrival', t: arrival(p) }, { name: 'Ripples', t: arrival(p) + p.splashTime }];
  },
  events(p) { return [{ t: arrival(p), type: 'shake', value: 0.035 }]; },
  draw(t, p, { origin }) {
    if (t < 0 || t >= end(p)) return;
    const aim = p.heading * Mathf.Deg2Rad;
    const point = (distance, side = 0) => [origin.x + Math.cos(aim) * distance - Math.sin(aim) * side,
      origin.z + Math.sin(aim) * distance + Math.cos(aim) * side];
    const land = arrival(p), duration = p.splashTime + p.settle;
    const progress = smooth((t - p.gather) / p.dash);
    const head = p.reach * progress;
    const fade = 1 - smooth((t - land) / duration);
    const intro = smooth(t / p.gather);

    // Broken rings tighten at the feet, peeling away as the runner leaves.
    for (let j = 0; j < 3; j++) {
      const radius = 0.9 - intro * 0.45;
      const pts = Array.from({ length: 33 }, (_, i) => {
        const a = j * TAU / 3 + t * 7 + i / 32 * 1.65;
        return [origin.x + Math.cos(a) * radius, origin.z + Math.sin(a) * radius * 0.55];
      });
      stream(`dash-gather-${j}`, pts, 0.16, intro * (1 - smooth((t - p.gather) / 0.15)), p.foam, t * 10);
    }

    if (t > p.gather) {
      spriteWake(t, p, point);
      // Small, irregular pools remain where the wake has already passed.
      for (let i = 0; i < 11; i++) {
        const along = (i + 0.5) / 11;
        if (along > progress) continue;
        const q = point(p.reach * along, (hash(i, 1, 817) - 0.5) * p.width);
        const size = 0.10 + hash(i, 2, 817) * 0.15;
        draw(disc, q[0], q[1], size, size * 0.43, -p.heading, blue, fade * 0.22, floor);
        droplet(`dash-wake-drop-${i}`, q[0], q[1] + 0.12 + Math.sin(along * Math.PI) * 0.13,
          0.055, -p.heading, fade * 0.65, p.foam);
      }
    }

    if (p.afterimages && t > p.gather) {
      for (let i = 2; i >= 1; i--) {
        const old = t - p.dash * i * 0.22;
        if (old <= p.gather || old >= land) continue;
        runner(`dash-echo-${i}`, point, p.reach * smooth((old - p.gather) / p.dash),
          0.34 * (1 - i * 0.18) * fade, p, t * 12 + i);
      }
    }
    if (p.runner) runner('dash-runner', point, head, intro * fade, p, t * 12);

    if (t >= land) {
      const age = t - land, spread = smooth(age / p.splashTime);
      const [x, z] = point(p.reach);
      // A low bow wave rolls forward and opens, never forming a cutting blade.
      for (let j = 0; j < 3; j++) {
        const pts = Array.from({ length: 33 }, (_, i) => {
          const a = aim - 1.35 + i / 32 * 2.7;
          const r = p.splash * (0.22 + spread * (0.70 + j * 0.14));
          return [x + Math.cos(a) * r, z + Math.sin(a) * r * 0.72 + Math.sin(i / 32 * Math.PI) * 0.12];
        });
        stream(`dash-arrival-${j}`, pts, 0.16 * (1 - spread * 0.4), fade, p.foam, age * 9 + j);
        if (j === 0) ribbon('dash-arrival-foam', pts, 0.026, foam, fade * p.foam * 0.75);
      }
      for (let i = 0; i < 18; i++) {
        const life = duration * (0.45 + hash(i, 3, 817) * 0.5), u = age / life;
        if (u >= 1) continue;
        const a = aim + (hash(i, 4, 817) - 0.5) * 3.6;
        const r = p.splash * (0.3 + u * (0.6 + hash(i, 5, 817)));
        droplet(`dash-arrival-drop-${i}`, x + Math.cos(a) * r,
          z + Math.sin(a) * r * 0.65 + Math.sin(u * Math.PI) * 0.35,
          0.05 + hash(i, 6, 817) * 0.07, -a * Mathf.Rad2Deg + 90, (1 - smooth(u)) * fade, p.foam);
      }
    }
  },
};
