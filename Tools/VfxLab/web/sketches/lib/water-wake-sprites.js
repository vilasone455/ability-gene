// Deterministic sprite water: smooth sheets, independently advected highlights,
// ballistic spray, and ground contacts. All time is reconstructed when scrubbing.
import { Color, MaterialPool, MeshPool, Meshes, ShaderDatabase } from '../../js/engine.js';
import { hash } from '../../js/standins.js';
import { draw, aqua, blue, foam, smooth, altitude, floor } from './water-ninja-water.js';

const material = name => MaterialPool.MatFrom(`RimArt/WaterNinja/${name}`, ShaderDatabase.Transparent);
const bodies = [0, 1, 2].map(i => material(`Wake${i}`));
const crests = [0, 1, 2].map(i => material(`Foam${i}`));
const drop = material('Droplet'), bow = material('Bow'), bowFoam = material('BowFoam');
const soft = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent);
const shadow = new Color(0.015, 0.04, 0.06);
const ring = Meshes.band(0.94, 1, 48, 'water-contact-ring');
const quad = MeshPool.plane10;

export function waterProw(point, distance, alpha, p) {
  const q = point(distance + 0.14);
  draw(quad, q[0], q[1] + 0.20, 0.95, p.width * 1.85, -p.heading,
    blue, alpha * 0.75, altitude + 0.01, bow);
  draw(quad, q[0] + 0.015, q[1] + 0.25, 0.9, p.width * 1.8, -p.heading,
    aqua, alpha * 0.90, altitude + 0.012, bow);
  draw(quad, q[0] + 0.015, q[1] + 0.25, 0.9, p.width * 1.8, -p.heading,
    foam, alpha * p.foam * 0.9, altitude + 0.014, bowFoam);
}

// Position and velocity in screen coordinates. At impact, freeze the ground point
// and replace the airborne sprite with an expanding, fading ring.
export function waterSpray(age, life, x, z, vx, vz, rise, size, alpha, p) {
  if (age < 0 || age >= life) return;
  const gravity = 14;
  const flight = 2 * rise / gravity;
  const travel = Math.min(age, flight);
  const gx = x + vx * travel, gz = z + vz * travel;
  const fade = alpha * (1 - smooth(age / life));
  if (age < flight) {
    const height = rise * age - gravity * age * age / 2;
    const dy = vz + rise - gravity * age;
    const speed = Math.hypot(vx, dy);
    draw(quad, gx, gz, size * 2.4, size * 1.4, 0, shadow, fade * 0.17, floor + 0.01, soft);
    draw(quad, gx, gz + height, size * (1 + Math.min(3, speed * 0.40)), size,
      -Math.atan2(dy, vx) * 180 / Math.PI, aqua, fade * 0.85, altitude + 0.07, drop);
    draw(quad, gx, gz + height + size * 0.09, size * 0.50, size * 0.35,
      0, foam, fade * p.foam * 0.65, altitude + 0.072, soft);
  } else {
    const u = (age - flight) / Math.max(0.01, life - flight);
    const radius = size * 1.5 + u * 0.28;
    draw(ring, gx, gz, radius, radius * 0.55, 0, aqua,
      fade * (1 - u) * 0.42, floor + 0.02);
  }
}

export function spriteWake(t, p, point) {
  const count = Math.ceil(p.reach / 0.27);
  const life = Math.min(0.27 + p.settle * 0.35, (p.splashTime + p.settle) * 0.8);
  const end = p.gather + p.dash + p.splashTime + p.settle;
  const groundFade = 1 - smooth((t - p.gather - p.dash - p.splashTime) / p.settle);
  const aim = p.heading * Math.PI / 180;
  for (let i = 0; i < count; i++) {
    const along = (i + 0.5) / count;
    const emitted = p.gather + p.dash * (0.5 - Math.sin(Math.asin(1 - 2 * along) / 3));
    const age = t - emitted;
    if (age < 0) continue;
    const u = age / life, variant = i % 3;
    const ground = point(p.reach * along);
    // Soft shadow remains directly under the elevated stream; damp soil outlasts it.
    draw(quad, ground[0], ground[1], 1.25, p.width * 1.5, -p.heading,
      shadow, (1 - smooth(u)) * 0.16, floor + 0.005, soft);
    draw(quad, ground[0], ground[1], 0.80, p.width * 0.85, -p.heading,
      shadow, smooth(age / 0.10) * groundFade * 0.16, floor, bodies[variant]);
    if (u < 1) {
      const opacity = smooth(age / 0.02) * (1 - smooth(u));
      const erosion = 1 - smooth((u - 0.35) / 0.65);
      const q = point(p.reach * along + age * 0.65,
        Math.sin(along * 5 + age * 2) * p.width * 0.09);
      const length = (1.15 + hash(i, 3, 902) * 0.22) * (0.45 + erosion * 0.55);
      const width = p.width * (0.5 + erosion * 0.5);
      draw(quad, q[0], q[1] + 0.14, length, width, -p.heading,
        blue, opacity * 0.42, altitude - 0.04, bodies[variant]);
      draw(quad, q[0], q[1] + 0.19, length, width * 0.90, -p.heading,
        aqua, opacity * 0.58, altitude - 0.035, bodies[variant]);
      // Move a broad highlight faster than the body: internal flow without UV offsets.
      const flow = point(p.reach * along + age * 1.35, Math.sin(age * 4 + along * 4) * p.width * 0.10);
      draw(quad, flow[0], flow[1] + 0.20, length * 0.9, width * 0.80, -p.heading,
        foam, opacity * p.foam * 0.45, altitude - 0.03, crests[variant]);
    }
    // A light spray and heavier tail fragment per packet. Both land, then ripple.
    for (let j = 0; j < 2; j++) {
      const delay = j ? life * 0.40 : 0;
      const a = age - delay;
      const side = (i % 2 ? 1 : -1) * (0.8 + hash(i, j, 917) * 1.5);
      const forward = j ? 0.35 : 1.4;
      const vx = Math.cos(aim) * forward - Math.sin(aim) * side;
      const vz = Math.sin(aim) * forward + Math.cos(aim) * side;
      const size = j ? 0.10 + hash(i, 4, 917) * 0.11 : 0.025 + hash(i, 5, 917) * 0.04;
      waterSpray(a, Math.min(life + 0.12, end - emitted - delay), ground[0], ground[1],
        vx, vz, j ? 0.9 : 1.5, size, groundFade, p);
    }
  }
}
