// Shared drawing pieces for Bloom and Heavenfall. All particles are sampled from
// impact age; mesh keys must include the sketch and strike/petal identity.
import { AltitudeLayer, Color, MaterialPool, Meshes, MeshPool, ShaderDatabase } from '../../js/engine.js';
import { draw, mesh, Body, Rim, Lift } from './six-paths-solid.js';
export { Body, Rim, Lift };
export const Y = AltitudeLayer.MoteOverhead.AltitudeFor();
export const Floor = AltitudeLayer.Filth.AltitudeFor();
export const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });
export const soft = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent);
export const glow = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.MoteGlow);
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const disc = Meshes.disc(48, 'impact orb');
const ring = Meshes.band(0.965, 1, 64, 'impact ring');
export const rand = i => { const n = Math.sin(i * 127.1 + 17) * 43758.5453; return n - Math.floor(n); };
export const at = (o, x, z, h = 0) => ({ x: o.x + x, z: o.z + z + h * Lift });
export function sprite(pos, w, h, colour, material = soft, layer = Y, angle = 0) {
  draw(MeshPool.plane10, pos.x, layer, pos.z, w, h, angle, colour, material);
}
export function orb(pos, size, alpha = 1, stretch = 1, layer = Y) {
  draw(disc, pos.x, layer, pos.z, size + 0.018, size * stretch + 0.018, 0, Rim.withAlpha(alpha));
  draw(disc, pos.x, layer + 0.002, pos.z, size, size * stretch, 0, Body.withAlpha(alpha));
}
export function circle(pos, radius, alpha, layer = Floor, colour = Rim) {
  if (radius <= 0) return;
  draw(ring, pos.x, layer, pos.z, radius, radius, 0, colour.withAlpha(alpha));
}
export function band(key, a, b, colour, layer = Y) {
  const vertices = [], tri = [];
  for (let i = 0; i < a.length; i++) {
    vertices.push(a[i].x, a[i].z, b[i].x, b[i].z);
    if (i) { const n = i * 2; tri.push(n - 2, n, n - 1, n - 1, n, n + 1); }
  }
  const m = mesh(key); m.setFlat(vertices, tri);
  draw(m, 0, layer, 0, 1, 1, 0, colour);
}
export function trail(key, pts, width, colour, layer = Y) {
  const a = [], b = [];
  pts.forEach((p, i) => {
    const prev = pts[Math.max(0, i - 1)], next = pts[Math.min(pts.length - 1, i + 1)];
    const dx = next.x - prev.x, dz = next.z - prev.z, len = Math.hypot(dx, dz) || 1;
    const w = Math.sin(i / (pts.length - 1) * Math.PI) * width / 2;
    a.push({ x: p.x - dz / len * w, z: p.z + dx / len * w });
    b.push({ x: p.x + dz / len * w, z: p.z - dx / len * w });
  });
  band(key, a, b, colour, layer);
}
export function impact(key, origin, age, strength, size, dust = 0.6) {
  if (age < 0 || age > 0.8) return;
  const flash = Math.max(0, 1 - age / 0.13);
  sprite(origin, size * 3, size * 2, new Color(0.87, 0.76, 1, flash * strength * 0.8), glow, Y + 0.15);
  circle(origin, size * (0.3 + age * 3), (1 - age / 0.8) * strength * 0.6);
  for (let i = 0; i < 18; i++) {
    const life = 0.35 + rand(i) * 0.4, u = age / life;
    if (u > 1) continue;
    const a = i * 2.399, d = size * (0.3 + age * (1.2 + rand(i + 20) * 2));
    const x = Math.cos(a) * d, z = Math.sin(a) * d;
    const pos = at(origin, x, z, Math.sin(u * Math.PI) * size * 0.65);
    sprite(pos, (0.35 + u) * size, (0.3 + u * 0.7) * size,
      new Color(0.52, 0.45, 0.37, Math.sin(u * Math.PI) * dust * strength), puff, Y + 0.10);
    const pts = [at(pos, -Math.cos(a) * 0.25, -Math.sin(a) * 0.25), pos,
      at(pos, Math.cos(a) * 0.12, Math.sin(a) * 0.12)];
    trail(`${key} shard ${i}`, pts, 0.12 * size * (1 - u), Body.withAlpha(strength * (1 - u)), Y + 0.12);
  }
}
export function target(origin, visible) {
  if (!visible) return;
  const layer = AltitudeLayer.Pawn.AltitudeFor();
  sprite(origin, 0.9, 0.45, Body.withAlpha(0.3), soft, Floor);
  draw(disc, origin.x, layer, origin.z + 0.18, 0.22, 0.32, 0, new Color(0.67, 0.43, 0.3));
  draw(disc, origin.x, layer + 0.002, origin.z + 0.58, 0.16, 0.17, 0, new Color(0.83, 0.70, 0.54));
}
