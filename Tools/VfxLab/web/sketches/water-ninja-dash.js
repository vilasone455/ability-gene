// Undertow Dash: collapse into water, travel as a compact splash, emerge at arrival.
// Default: collapse 0–0.18 s, surge 0.18–0.46 s, emergence 0.46–0.64 s,
// wet ground settles until 1.00 s. A simple preview pawn shows the transformation;
// the lab's scene pawn is not moved. No game ability is replaced.
import { Color, Mathf } from '../js/engine.js';
import { spriteWake, travelingSplash, splashBurst } from './lib/water-wake-sprites.js';
import { smooth, disc, altitude, P, draw } from './lib/water-ninja-water.js';

const arrival = p => p.gather + p.dash;
const end = p => arrival(p) + p.splashTime + p.settle;
const suit = new Color(0.06, 0.16, 0.23), face = new Color(0.68, 0.78, 0.77);

// Neutral, pawn-sized stand-in. Sinks on departure and rises on emergence.
function pawn(point, distance, visibility) {
  if (visibility <= 0.001) return;
  const [x, z] = point(distance), lift = 0.60 * visibility;
  draw(disc, x, z + lift * 0.43, 0.22, 0.30 * visibility, 0, suit, visibility, altitude - 0.09);
  draw(disc, x, z + lift, 0.17, 0.18, 0, face, visibility, altitude - 0.085);
}

export default {
  kit: 'Water Ninja',
  label: 'Undertow Dash (sketch)',
  params: {
    gather: P('Collapse into water', 0.18, 0.08, 0.6, 0.01, 'Timing (s)'),
    dash: P('Traveling splash', 0.28, 0.12, 0.8, 0.01, 'Timing (s)'),
    splashTime: P('Emerge on arrival', 0.18, 0.08, 0.5, 0.01, 'Timing (s)'),
    settle: P('Wet ground settles', 0.36, 0.15, 1.2, 0.01, 'Timing (s)'),
    heading: P('Aim (degrees from east)', 20, -180, 180, 5, 'Shape'),
    reach: P('Dash distance (cells)', 5, 2, 10, 0.25, 'Shape'),
    width: P('Moving splash size (cells)', 0.75, 0.3, 1.5, 0.05, 'Shape'),
    splash: P('Arrival splash size (cells)', 1.25, 0.5, 2.5, 0.05, 'Shape'),
    foam: P('White foam', 0.9, 0, 1, 0.05, 'Style'),
    previewPawn: { label: 'Show transformation pawn', value: true, group: 'Style' },
  },
  duration: end,
  phases(p) {
    return [{ name: 'Collapse', t: 0 }, { name: 'Traveling splash', t: p.gather },
      { name: 'Emerge', t: arrival(p) }, { name: 'Wet ground', t: arrival(p) + p.splashTime }];
  },
  events(p) { return [{ t: arrival(p), type: 'shake', value: 0.035 }]; },
  draw(t, p, { origin }) {
    if (t < 0 || t >= end(p)) return;
    const aim = p.heading * Mathf.Deg2Rad;
    const point = (distance, side = 0) => [origin.x + Math.cos(aim) * distance - Math.sin(aim) * side,
      origin.z + Math.sin(aim) * distance + Math.cos(aim) * side];
    const land = arrival(p), duration = p.splashTime + p.settle;
    const collapse = smooth(t / p.gather);
    const progress = smooth((t - p.gather) / p.dash);
    const fade = 1 - smooth((t - land - p.splashTime) / p.settle);

    if (t < p.gather) {
      if (p.previewPawn) pawn(point, 0, 1 - collapse);
      travelingSplash(point, 0, collapse, p, t);
    } else if (t < land) {
      travelingSplash(point, p.reach * progress, 1, p, t);
    }
    splashBurst(t - p.gather * 0.35, p.gather * 0.65 + Math.min(0.2, duration),
      point, 0, p.width * 0.65, p, 301);
    if (t > p.gather) spriteWake(t, p, point);

    if (t >= land) {
      const age = t - land, emerge = smooth(age / p.splashTime);
      travelingSplash(point, p.reach, 1 - emerge, { ...p, width: p.splash }, t, emerge);
      splashBurst(age, duration, point, p.reach, p.splash, p, 713);
      if (p.previewPawn) pawn(point, p.reach, emerge * fade);
    }
  },
};
