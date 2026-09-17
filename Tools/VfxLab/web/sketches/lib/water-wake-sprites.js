// Layered PNG sprites emitted at fixed distances. Each packet ages independently,
// so arbitrary scrubbing produces the same wake without a mutable particle system.
import { MaterialPool, MeshPool, ShaderDatabase } from '../../js/engine.js';
import { hash } from '../../js/standins.js';
import { draw, aqua, blue, foam, smooth, altitude } from './water-ninja-water.js';

const material = name => MaterialPool.MatFrom(`RimArt/WaterNinja/${name}`, ShaderDatabase.Transparent);
const bodies = [0, 1, 2].map(i => material(`Wake${i}`));
const crests = [0, 1, 2].map(i => material(`Foam${i}`));
const drop = material('Droplet');

export function spriteWake(t, p, point) {
  const count = Math.ceil(p.reach / 0.20);
  const life = Math.min(0.30 + p.settle * 0.45, p.splashTime + p.settle);
  for (let i = 0; i < count; i++) {
    const along = (i + 0.5) / count;
    // Inverse smoothstep: emit when the smoothly moving runner passes this position.
    const emitted = p.gather + p.dash * (0.5 - Math.sin(Math.asin(1 - 2 * along) / 3));
    const age = t - emitted;
    if (age < 0 || age >= life) continue;
    const u = age / life;
    const opacity = smooth(age / 0.025) * (1 - smooth(u));
    const variant = i % 3;
    const drift = (hash(i, 1, 902) - 0.5) * p.width * (0.3 + u * 0.8);
    const q = point(p.reach * along + age * 0.30, drift);
    const rotation = -p.heading + (hash(i, 2, 902) - 0.5) * (14 + u * 18);
    const length = (0.75 + hash(i, 3, 902) * 0.4) * (1 + u * 0.3);
    const width = p.width * (0.85 + hash(i, 4, 902) * 0.3) * (1 + u * 0.6);
    // A deep translucent bed, an offset turquoise sheet, and a separate foam crest.
    draw(MeshPool.plane10, q[0], q[1], length * 1.12, width * 1.1, rotation,
      blue, opacity * 0.34, altitude - 0.04, bodies[variant]);
    draw(MeshPool.plane10, q[0], q[1] + 0.035, length, width, rotation,
      aqua, opacity * 0.62, altitude - 0.035, bodies[variant]);
    draw(MeshPool.plane10, q[0], q[1] + 0.035, length, width, rotation,
      foam, opacity * p.foam * 0.78, altitude - 0.03, crests[variant]);
    // Detached round spray follows a short ballistic arc and spreads to both sides.
    if (i % 2 === 0) {
      const side = (i % 4 ? -1 : 1) * p.width * (0.35 + u * 0.8);
      const d = point(p.reach * along + age * 0.5, side);
      const lift = Math.max(0, age * 1.8 - age * age * 5);
      draw(MeshPool.plane10, d[0], d[1] + lift, 0.10 + u * 0.05, 0.075,
        rotation + 20, foam, opacity * p.foam * 0.80, altitude + 0.06, drop);
    }
  }
}
