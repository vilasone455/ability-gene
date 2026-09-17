// Shared Water Ninja drawing helpers and procedural water texture.
import {
  AltitudeLayer, Color, Graphics, MaterialPool, MaterialPropertyBlock, Mathf,
  Matrix4x4, Mesh, Meshes, Quaternion, ShaderDatabase, ShaderPropertyIDs, Vector3,
} from '../../js/engine.js';
import { fbm, hash, pixels, registerLabTexture } from '../../js/standins.js';

const TAU = Math.PI * 2;
const clamp = Mathf.Clamp01;
const smooth = Mathf.Smooth;
const ink = new Color(0.025, 0.13, 0.29);
const blue = new Color(0.025, 0.40, 0.73);
const aqua = new Color(0.10, 0.80, 0.91);
const foam = new Color(0.85, 1, 0.98);
const solid = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
// Soft, striated alpha with holes: the mesh supplies motion, not the visible outline.
registerLabTexture('lab/tidecutter-water', () => pixels(256, (u, v) => {
  const n = fbm(u * 6, v * 3, 419, 3);
  const edge = Math.abs(v - 0.5) * 2;
  const feather = smooth((1 - edge - (n - 0.5) * 0.35) / 0.30);
  const ends = smooth(u / 0.12) * smooth((1 - u) / 0.08);
  const strands = 0.45 + 0.55 * Math.sin(v * 35 + n * 8 + u * 5) ** 2;
  const holes = smooth((n - 0.26) / 0.22);
  return [1, 1, 1, feather * ends * strands * holes];
}));
const water = MaterialPool.MatFrom('lab/tidecutter-water', ShaderDatabase.Transparent);
const props = new MaterialPropertyBlock();
const meshes = new Map();
const disc = Meshes.disc(40, 'water-pool');
const altitude = AltitudeLayer.MoteOverhead.AltitudeFor();
const floor = AltitudeLayer.Filth.AltitudeFor();
const mesh = key => meshes.get(key) ?? meshes.set(key, new Mesh(`water-${key}`)).get(key);
const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });

function draw(m, x, z, sx, sz, rotation, color, alpha, y = altitude, material = solid) {
  if (alpha <= 0.001 || sx <= 0 || sz <= 0) return;
  props.SetColor(ShaderPropertyIDs.Color, color.withAlpha(clamp(alpha)));
  Graphics.DrawMesh(m, Matrix4x4.TRS(new Vector3(x, y, z),
    Quaternion.Euler(0, rotation, 0), new Vector3(sx, 1, sz)), material, 0, null, 0, props);
}

// Each ribbon owns its mesh: earlier draws must survive arbitrary timeline scrubbing.
function ribbon(key, points, width, color, alpha, y = altitude) {
  const vertices = [], triangles = [];
  for (let i = 0; i < points.length; i++) {
    const prev = points[Math.max(0, i - 1)], next = points[Math.min(points.length - 1, i + 1)];
    const dx = next[0] - prev[0], dz = next[1] - prev[1];
    const length = Math.hypot(dx, dz) || 1;
    const w = width * Math.pow(Math.sin(Math.PI * i / (points.length - 1)), 0.65) / 2;
    vertices.push(points[i][0] - dz / length * w, points[i][1] + dx / length * w,
      points[i][0] + dz / length * w, points[i][1] - dx / length * w);
    if (i) { const v = i * 2; triangles.push(v - 2, v, v - 1, v - 1, v, v + 1); }
  }
  const m = mesh(key); m.setFlat(vertices, triangles);
  draw(m, 0, 0, 1, 1, 0, color, alpha, y);
}

// Uneven transparent streams; no dark outline or solid central strip.
function stream(key, points, width, alpha, foamStrength, phase, y = altitude) {
  const vertices = [], uv = [], triangles = [];
  for (let i = 0; i < points.length; i++) {
    const u = i / (points.length - 1);
    const prev = points[Math.max(0, i - 1)], next = points[Math.min(points.length - 1, i + 1)];
    const dx = next[0] - prev[0], dz = next[1] - prev[1], length = Math.hypot(dx, dz) || 1;
    const w = width * (0.55 + 0.20 * Math.sin(u * 19 + phase) + 0.13 * Math.sin(u * 37 - phase));
    for (const side of [-1, 1]) {
      vertices.push(points[i][0] - dz / length * w * side, points[i][1] + dx / length * w * side);
      uv.push(u, (side + 1) / 2);
    }
    if (i) { const v = i * 2; triangles.push(v - 2, v, v - 1, v - 1, v, v + 1); }
  }
  const m = mesh(key); m.setFlat(vertices, triangles); m.uv = Float32Array.from(uv);
  draw(m, 0, 0, 1, 1, 0, aqua, alpha * 0.70, y, water);
  // Disconnected surface glints leave gaps and let the terrain show through.
  for (let j = 0; j < 4; j++) {
    const start = 2 + j * 7;
    const glint = points.slice(start, start + 5).map(([x, z], i) =>
      [x, z + width * 0.13 * Math.sin(phase + j + i * 0.5)]);
    ribbon(`${key}-glint-${j}`, glint, width * 0.07, foam, alpha * foamStrength * 0.6, y + 0.002);
  }
}

function waterStroke(key, points, width, alpha, foamStrength, y = altitude) {
  ribbon(`${key}-ink`, points, width * 1.18, ink, alpha * 0.9, y);
  ribbon(`${key}-body`, points, width, blue, alpha, y + 0.002);
  ribbon(`${key}-light`, points.map(([x, z]) => [x, z + width * 0.20]),
    width * 0.46, aqua, alpha, y + 0.004);
  ribbon(`${key}-foam`, points.map(([x, z]) => [x, z + width * 0.39]),
    width * 0.09, foam, alpha * foamStrength, y + 0.006);
}

function arc(key, x, z, radius, start, sweep, width, alpha, foamStrength, flatten = 1, y = altitude) {
  const points = Array.from({ length: 41 }, (_, i) => {
    const a = start + sweep * i / 40;
    return [x + Math.cos(a) * radius, z + Math.sin(a) * radius * flatten];
  });
  waterStroke(key, points, width, alpha, foamStrength, y);
}

function droplet(key, x, z, size, angle, alpha, foamStrength) {
  const m = mesh(key);
  // Rounded shoulder and a pointed tail, deliberately unlike a spark or a diamond.
  m.setFlat([0, -1, -0.38, -0.12, -0.30, 0.35, 0, 0.5, 0.30, 0.35, 0.38, -0.12],
    [0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5]);
  draw(m, x, z, size, size * 1.6, angle, aqua, alpha, altitude + 0.08);
  draw(m, x - size * 0.06, z + size * 0.09, size * 0.40, size * 0.55,
    angle, foam, alpha * foamStrength, altitude + 0.082);
}

export { TAU, clamp, smooth, ink, blue, aqua, foam, disc, altitude, floor, mesh, P, draw, ribbon, stream, arc, droplet };
