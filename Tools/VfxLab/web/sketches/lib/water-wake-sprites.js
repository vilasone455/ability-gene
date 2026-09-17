// Deterministic sprite water: smooth sheets, independently advected highlights,
// ballistic spray, and ground contacts. All time is reconstructed when scrubbing.
import { Color, MaterialPool, MeshPool, Meshes, ShaderDatabase } from '../../js/engine.js';
import { hash } from '../../js/standins.js';
import { draw, aqua, blue, foam, smooth, altitude, floor } from './water-ninja-water.js';

const material = name => MaterialPool.MatFrom(`RimArt/WaterNinja/${name}`, ShaderDatabase.Transparent);
const bodies = [0, 1, 2].map(i => material(`Wake${i}`));
const crests = [0, 1, 2].map(i => material(`Foam${i}`));
const drop = material('Droplet');
const soft = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent);
const shadow = new Color(0.015, 0.04, 0.06);
const ring = Meshes.band(0.94, 1, 48, 'water-contact-ring');
const quad = MeshPool.plane10;

// A compact rolling mass: overlapping rounded lobes, not a line connecting endpoints.
export function travelingSplash(point, distance, alpha, p, phase, expansion = 0) {
  const q = point(distance);
  const radius = p.width * (1 + expansion * 0.7);
  draw(quad, q[0], q[1], radius * 2.0, radius * 1.3, -p.heading,
    shadow, alpha * 0.35, floor + 0.005, soft);
  draw(quad, q[0], q[1] + 0.18, radius * 1.2, radius * 1.7, -p.heading,
    blue, alpha * 0.70, altitude - 0.006, bodies[0]);
  for (let j = 0; j < 5; j++) {
    const side = (j - 2) * radius * 0.14;
    const forward = 0.12 - Math.abs(j - 2) * 0.10 + Math.sin(phase * 8 + j * 1.7) * 0.07;
    const pos = point(distance + forward, side);
    const lift = 0.12 + (1 - Math.abs(j - 2) / 3) * 0.28;
    const size = radius * (1.05 + Math.sin(phase * 9 + j) * 0.08);
    draw(quad, pos[0], pos[1] + lift, size, size * 1.10, -p.heading + j * 9 - 18,
      blue, alpha * 0.85, altitude + j * 0.003, bodies[j % 3]);
    draw(quad, pos[0], pos[1] + lift + 0.06, size * 0.94, size,
      -p.heading + j * 9 - 18, aqua, alpha * 0.9, altitude + 0.018 + j * 0.003, bodies[j % 3]);
    draw(quad, pos[0], pos[1] + lift + 0.08, size * 0.9, size * 0.85,
      -p.heading + j * 9 - 18, foam, alpha * p.foam * 0.8, altitude + 0.036 + j * 0.003, crests[j % 3]);
  }
}

export function splashBurst(age, life, point, distance, radius, p, seed) {
  if (age < 0 || age >= life) return;
  const q = point(distance);
  for (let i = 0; i < 24; i++) {
    const a = i / 24 * Math.PI * 2 + hash(i, 1, seed) * 0.3;
    const speed = radius * (1.5 + hash(i, 2, seed) * 2);
    waterSpray(age, life, q[0], q[1], Math.cos(a) * speed, Math.sin(a) * speed * 0.6,
      1 + hash(i, 3, seed) * 1.5, 0.065 + hash(i, 4, seed) * 0.15, 1, p);
  }
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
  // Keep visible water within one cell of the moving front, even at long range.
  const life = Math.min(p.dash * 0.8 / p.reach, 0.10);
  const head = p.reach * smooth((t - p.gather) / p.dash);
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
    if (u < 1 && head - p.reach * along < 0.85) {
      const opacity = smooth(age / Math.min(0.012, life * 0.2)) * (1 - smooth(u));
      const erosion = 1 - smooth((u - 0.35) / 0.65);
      const q = point(p.reach * along + age * 0.65,
        Math.sin(along * 5 + age * 2) * p.width * 0.09);
      const length = (0.36 + hash(i, 3, 902) * 0.20) * (0.45 + erosion * 0.55);
      const width = p.width * (0.5 + erosion * 0.5);
      draw(quad, q[0], q[1] + 0.14, length, width, -p.heading,
        blue, opacity * 0.30, altitude - 0.04, bodies[variant]);
      draw(quad, q[0], q[1] + 0.19, length, width * 0.90, -p.heading,
        aqua, opacity * 0.40, altitude - 0.035, bodies[variant]);
      // Move a broad highlight faster than the body: internal flow without UV offsets.
      const flow = point(p.reach * along + age * 1.35, Math.sin(age * 4 + along * 4) * p.width * 0.10);
      draw(quad, flow[0], flow[1] + 0.20, length * 0.9, width * 0.80, -p.heading,
        foam, opacity * p.foam * 0.45, altitude - 0.03, crests[variant]);
    }
    // A light spray and heavier tail fragment per packet. Both land, then ripple.
    for (let j = 0; j < (i % 2 === 0 ? 2 : 0); j++) {
      const delay = j ? life * 0.40 : 0;
      const a = age - delay;
      const side = (i % 2 ? 1 : -1) * (0.8 + hash(i, j, 917) * 1.5);
      const forward = j ? 0.35 : 1.4;
      const vx = Math.cos(aim) * forward - Math.sin(aim) * side;
      const vz = Math.sin(aim) * forward + Math.cos(aim) * side;
      const size = j ? 0.07 + hash(i, 4, 917) * 0.06 : 0.025 + hash(i, 5, 917) * 0.04;
      waterSpray(a, Math.min(0.38, end - emitted - delay), ground[0], ground[1],
        vx, vz, j ? 0.55 : 0.8, size, groundFade, p);
    }
  }
}
