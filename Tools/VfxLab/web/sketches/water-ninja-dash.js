// Undertow Dash: gather 0–0.18 s, dash 0.18–0.46 s, arrival splash 0.46–0.64 s,
// then puddles and ripples settle until 1.00 s. A curved water bow shows motion;
// this sketch cannot move the lab's scene pawn. No game ability is replaced.
// Uses layered PNG wake sprites; the arrival uses Tidecutter's lab-only texture.
import { Mathf } from '../js/engine.js';
import { hash } from '../js/standins.js';
import { spriteWake, waterProw, waterSpray } from './lib/water-wake-sprites.js';
import {
  TAU, smooth, foam, P,
  ribbon, stream,
} from './lib/water-ninja-water.js';

const arrival = p => p.gather + p.dash;
const end = p => arrival(p) + p.splashTime + p.settle;

// The moving water proxy is a bow wave; delayed copies show the dash path.
function runner(point, distance, alpha, p) {
  if (alpha > 0.001) waterProw(point, distance, alpha, p);
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
    runner: { label: 'Show forward bow wave', value: true, group: 'Style' },
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
    }

    if (p.afterimages && t > p.gather) {
      for (let i = 2; i >= 1; i--) {
        const old = t - p.dash * i * 0.22;
        if (old <= p.gather || old >= land) continue;
        runner(point, p.reach * smooth((old - p.gather) / p.dash),
          0.34 * (1 - i * 0.18) * fade, p);
      }
    }
    if (p.runner) runner(point, head, intro * fade, p);

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
        const angle = aim + (hash(i, 4, 817) - 0.5) * 3.6;
        const speed = 1.2 + hash(i, 5, 817) * 2.5;
        waterSpray(age, duration, x, z, Math.cos(angle) * speed, Math.sin(angle) * speed * 0.65,
          0.7 + hash(i, 3, 817) * 0.9, 0.035 + hash(i, 6, 817) ** 2 * 0.17, fade, p);
      }
    }
  },
};
