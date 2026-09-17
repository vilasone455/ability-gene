// Black Tide — a standalone Six Paths VFX proposal; no gameplay effect is replaced.
// Six orbs gather, compress, then launch a compact rolling breaker with streamed
// wakes. Its crest slams into the floor, throwing a shock front and black fragments.
// After a brief impact hold, six pieces whip back along curved, trailing paths.
// The raised lip is projected north by SixPathsHeight.Lift; its base stays grounded.
// All geometry is sampled from time, including the return, for backwards scrubbing.
import {
  AltitudeLayer, Color, Graphics, MaterialPool, MaterialPropertyBlock, Mathf,
  Matrix4x4, Mesh, Meshes, MeshPool, Quaternion, ShaderDatabase, ShaderPropertyIDs, Vector3,
} from '../js/engine.js';

const ink = new Color(0.025, 0.020, 0.036);
const rim = new Color(0.53, 0.37, 0.79);
const fold = new Color(0.075, 0.058, 0.105);
const solid = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const soft = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent);
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const glow = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.MoteGlow);
const props = new MaterialPropertyBlock();
const disc = Meshes.disc(48, 'black tide orb');
const meshes = new Map();
const mesh = key => meshes.get(key) ?? meshes.set(key, new Mesh(`black tide ${key}`)).get(key);
const y = AltitudeLayer.MoteOverhead.AltitudeFor();
const ground = AltitudeLayer.Shadows.AltitudeFor();
const smooth = Mathf.Smooth, lerp = Mathf.Lerp;
const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });

function timing(p) {
  const surge = p.gather + p.form, curl = surge + p.surge;
  const impact = curl + p.curl, retract = impact + p.hold, reform = retract + p.retract;
  return { surge, curl, impact, retract, reform, end: reform + p.reform + 0.3 };
}

function paint(m, pos, layer, width, depth, colour, material = solid, angle = 0) {
  if (width <= 0 || depth <= 0 || colour.a <= 0) return;
  props.SetColor(ShaderPropertyIDs.Color, colour);
  Graphics.DrawMesh(m, Matrix4x4.TRS(new Vector3(pos.x, layer, pos.z),
    Quaternion.Euler(0, angle, 0), new Vector3(width, 1, depth)), material, 0, null, 0, props);
}

function point(origin, angle, side, distance, height = 0) {
  const a = angle * Mathf.Deg2Rad;
  return { x: origin.x + Math.cos(a) * side + Math.sin(a) * distance,
    z: origin.z - Math.sin(a) * side + Math.cos(a) * distance + height * 0.60 };
}

// A strip between two sampled curves preserves the hollow curl and pointed lip.
function strip(key, a, b, layer, colour) {
  const vertices = [], triangles = [];
  for (let i = 0; i < a.length; i++) {
    vertices.push(a[i].x, a[i].z, b[i].x, b[i].z);
    if (i) { const n = i * 2; triangles.push(n - 2, n, n - 1, n - 1, n, n + 1); }
  }
  const m = mesh(key); m.setFlat(vertices, triangles);
  paint(m, { x: 0, z: 0 }, layer, 1, 1, colour);
}

function ribbon(key, points, width, layer, colour) {
  const a = [], b = [];
  for (let i = 0; i < points.length; i++) {
    const prev = points[Math.max(0, i - 1)], next = points[Math.min(points.length - 1, i + 1)];
    const dx = next.x - prev.x, dz = next.z - prev.z, len = Math.hypot(dx, dz) || 1;
    const w = width * Math.sin(i / (points.length - 1) * Math.PI) / 2;
    a.push({ x: points[i].x - dz / len * w, z: points[i].z + dx / len * w });
    b.push({ x: points[i].x + dz / len * w, z: points[i].z - dx / len * w });
  }
  strip(key, a, b, layer, colour);
}

const travel = (s, p, t) => 1.15 + p.reach * Mathf.Clamp01((s - t.surge) / p.surge) ** 1.45;
const noise = i => (Math.sin(i * 127.1 + 31.7) * 43758.5453) % 1 + 1;

function breaker(s, p, t, origin, key, lag = 0) {
  const clock = s - lag;
  if (clock < p.gather || clock >= t.impact) return;
  const formed = smooth((clock - p.gather) / p.form);
  const run = Mathf.Clamp01((clock - t.surge) / p.surge);
  const crash = smooth((clock - t.curl) / p.curl);
  const front = travel(clock, p, t) + crash * 0.55;
  const width = p.width * formed * (1 + crash * 0.12);
  const height = p.height * formed * (1 - crash * 0.97);
  const alpha = lag ? 0.15 * (1 - lag / 0.3) : 1;
  const rows = 10, cols = p.teeth * 8;
  const curves = [];
  for (let row = 0; row <= rows; row++) {
    const v = row / rows, curve = [];
    for (let i = 0; i <= cols; i++) {
      const u = i / cols, k = u * 2 - 1, taper = Math.sqrt(Math.max(0, 1 - k * k));
      const roll = Math.sin(u * Math.PI * 4 - clock * 13) * 0.13 * Math.sin(v * Math.PI);
      const side = k * width / 2 * (0.72 + 0.28 * Math.sin(v * Math.PI / 2));
      const bow = 0.55 * (1 - k * k);
      const tooth = Math.sin(u * p.teeth * Math.PI) ** 6 * p.tooth * taper * v ** 8;
      const d = front - p.depth * (1 - v) * formed + bow * formed + roll + tooth * (0.35 + run * 0.65);
      const h = height * taper * Math.sin(v * Math.PI * 0.88) * (1 + roll);
      curve.push(point(origin, p.angle, side, d, h));
    }
    curves.push(curve);
  }
  for (let row = 0; row < rows; row++) {
    const colour = lag ? rim.withAlpha(alpha) : Color.Lerp(ink, fold, Math.sin(row / rows * Math.PI) ** 3);
    strip(`${key} body ${row}`, curves[row], curves[row + 1], y + row * 0.002, colour);
  }
  if (!lag) {
    ribbon(`${key} crest`, curves[7], 0.035, y + 0.025, rim.withAlpha(0.6));
    ribbon(`${key} lip`, curves[10], 0.045, y + 0.027, rim);
    const pos = point(origin, p.angle, 0, front - p.depth * 0.3);
    paint(MeshPool.plane10, pos, ground, width * 1.2, p.depth * 1.7,
      ink.withAlpha(0.5), soft, p.angle);
  }
}

export default {
  kit: 'Six Paths', label: 'Black Tide (sketch)',
  params: {
    gather: P('Gather six orbs', 0.45, 0.15, 1, 0.05, 'Timing (s)'),
    form: P('Compress and rear up', 0.35, 0.15, 1, 0.05, 'Timing (s)'),
    surge: P('Accelerate forward', 0.75, 0.3, 2, 0.05, 'Timing (s)'),
    curl: P('Crest slams down', 0.18, 0.1, 0.8, 0.02, 'Timing (s)'),
    hold: P('Impact hold', 0.25, 0.1, 0.6, 0.05, 'Timing (s)'),
    retract: P('Pull tide back', 0.75, 0.25, 1.5, 0.05, 'Timing (s)'),
    reform: P('Reform into orbs', 0.50, 0.2, 1, 0.05, 'Timing (s)'),
    width: P('Wave width (cells)', 5.4, 3, 8, 0.1, 'Shape'),
    depth: P('Breaker depth (cells)', 1.8, 1, 3, 0.1, 'Shape'),
    reach: P('Travel distance (cells)', 7, 3, 12, 0.25, 'Shape'),
    height: P('Curl height (cells)', 1.7, 0.4, 2.5, 0.05, 'Shape'),
    teeth: P('Teeth along lip', 5, 3, 15, 1, 'Shape'),
    tooth: P('Tooth length (cells)', 0.65, 0.2, 1.2, 0.05, 'Shape'),
    angle: P('Aim (degrees)', 35, 0, 360, 5, 'Shape'),
    trail: P('Wake length (cells)', 3.8, 1, 6, 0.1, 'Finish'),
    dust: P('Ground spray', 0.6, 0, 0.8, 0.05, 'Finish'),
    impact: P('Impact strength', 1, 0, 1.5, 0.1, 'Finish'),
    shake: P('Impact camera shake', 0.16, 0, 0.2, 0.01, 'Finish'),
  },
  duration(p) { return timing(p).end; },
  phases(p) {
    const t = timing(p);
    return [{ name: 'Gather', t: 0 }, { name: 'Compress', t: p.gather },
      { name: 'Surge', t: t.surge }, { name: 'Crash', t: t.curl }, { name: 'Impact', t: t.impact },
      { name: 'Retract', t: t.retract }, { name: 'Reform', t: t.reform }];
  },
  events(p) { const t = timing(p); return p.shake ? [
    { t: t.surge, type: 'shake', value: p.shake * 0.3 },
    { t: t.impact, type: 'shake', value: p.shake },
  ] : []; },
  draw(s, p, { origin }) {
    const t = timing(p);
    if (s < 0 || s >= t.end) return;
    const fade = 1 - smooth((s - t.end + 0.25) / 0.25);
    const front = travel(s, p, t);
    const running = smooth((s - t.surge) / 0.12) * (1 - smooth((s - t.impact) / 0.25));

    // Wakes sample earlier travel positions, so they lengthen with acceleration.
    for (let j = 0; j < 7; j++) {
      const path = [], bright = [];
      for (let i = 0; i <= 24; i++) {
        const u = i / 24, earlier = s - (1 - u) * 0.32;
        const d = Math.max(0.7, travel(earlier, p, t) - (1 - u) * p.trail);
        const side = (j / 6 - 0.5) * p.width * 0.82
          + Math.sin(u * 8 - s * 17 + j) * 0.14 * (1 - u);
        path.push(point(origin, p.angle, side, d));
        bright.push(point(origin, p.angle, side + 0.04, d, 0.04));
      }
      ribbon(`wake ${j}`, path, 0.20, y - 0.02, ink.withAlpha(running * 0.72));
      ribbon(`wake light ${j}`, bright, 0.045, y - 0.015, rim.withAlpha(running * 0.5));
    }
    for (let i = 3; i >= 1; i--) breaker(s, p, t, origin, `echo ${i}`, i * 0.055);
    breaker(s, p, t, origin, 'wave');

    // Six silhouettes spiral into the launch line and whip home from the impact.
    for (let i = 0; i < 6; i++) {
      const home = i / 6 * Math.PI * 2;
      const returning = s >= t.impact;
      const gather = smooth(s / p.gather);
      const form = smooth((s - p.gather) / p.form);
      const recall = smooth((s - t.retract - i * 0.025) / (p.retract - 0.125));
      if (!returning && s >= t.surge) continue;
      const pose = clock => {
        const q = smooth((clock - t.retract - i * 0.025) / (p.retract - 0.125));
        const spread = (i / 5 - 0.5) * p.width;
        return point(origin, p.angle,
          lerp(spread, Math.cos(home) * 1.15, q) + Math.sin(q * Math.PI) * (i % 2 ? 1 : -1) * 0.8,
          lerp(1.7 + p.reach, Math.sin(home) * 0.8, q),
          Math.sin(q * Math.PI) * 0.7);
      };
      const pos = returning ? pose(s) : point(origin, p.angle,
        lerp(Math.cos(home + gather * 1.5) * 1.15, (i / 5 - 0.5) * p.width * 0.7, gather),
        lerp(Math.sin(home + gather * 1.5) * 0.8, 1.15, gather));
      const alpha = returning ? fade * smooth((s - t.impact) / 0.10) : 1 - form;
      if (returning && recall > 0 && recall < 1) {
        const path = Array.from({ length: 18 }, (_, n) => pose(s - (1 - n / 17) * 0.15));
        ribbon(`return black ${i}`, path, 0.24, y + 0.04, ink.withAlpha(0.8));
        ribbon(`return violet ${i}`, path, 0.055, y + 0.045, rim.withAlpha(0.7));
      }
      const size = returning ? lerp(0.38, 0.29, recall) : 0.29 * (1 + form * 0.5);
      paint(disc, pos, y + 0.05, size + 0.02, size + 0.02, rim.withAlpha(alpha));
      paint(disc, pos, y + 0.054, size, size, ink.withAlpha(alpha));
    }

    // Seeded emission times leave dust and droplets behind the moving crest.
    for (let i = 0; i < 42; i++) {
      const born = t.surge + (i / 42) * p.surge;
      const age = s - born, life = 0.35 + (noise(i) % 1) * 0.25;
      if (age < 0 || age > life) continue;
      const u = age / life, side = ((noise(i + 10) % 1) - 0.5) * p.width;
      const d = travel(born, p, t) - age * 1.5;
      const height = Math.sin(u * Math.PI) * 0.55;
      const pos = point(origin, p.angle, side + Math.sign(side) * age * 0.7, d, height);
      paint(MeshPool.plane10, pos, y + 0.03, 0.25 + u * 0.8, 0.2 + u * 0.5,
        new Color(0.50, 0.44, 0.36, Math.sin(u * Math.PI) * p.dust * 0.55), puff);
      if (i % 2 === 0) {
        paint(disc, pos, y + 0.035, 0.07 * (1 - u), 0.19 * (1 - u),
          ink.withAlpha(1 - u), solid, p.angle);
      }
    }

    const since = s - t.impact;
    if (since < 0) return;
    const centre = point(origin, p.angle, 0, p.reach + 1.7);
    const flash = (1 - smooth(since / 0.14)) * p.impact;
    paint(MeshPool.plane10, centre, y + 0.07, p.width * 1.3, 2.4,
      new Color(0.82, 0.72, 1, flash * 0.8), glow, p.angle);
    // Expanding, broken shock arcs stay on the ground and dissipate in half a second.
    const shock = Mathf.Clamp01(since / 0.48);
    for (let j = 0; j < 3; j++) {
      const arc = [];
      for (let i = 0; i <= 32; i++) {
        const a = (-1.25 + i / 32 * 2.5) + j * Math.PI * 2 / 3;
        arc.push(point(centre, p.angle, Math.sin(a) * (0.5 + shock * 3),
          Math.cos(a) * (0.5 + shock * 2)));
      }
      ribbon(`shock ${j}`, arc, 0.11 * (1 - shock), y + 0.025,
        rim.withAlpha((1 - shock) * p.impact * 0.7));
    }
    for (let i = 0; i < 24; i++) {
      const life = 0.35 + (noise(i + 60) % 1) * 0.4;
      const u = since / life;
      if (u > 1) continue;
      const a = i * 2.4, speed = 1.2 + (noise(i + 90) % 1) * 3;
      const side = (i / 23 - 0.5) * p.width + Math.sin(a) * since * speed;
      const d = p.reach + 1.7 + Math.cos(a) * since * speed;
      const height = Math.sin(u * Math.PI) * (0.5 + noise(i + 30) % 1);
      const pos = point(origin, p.angle, side, d, height);
      paint(MeshPool.plane10, pos, y + 0.06, 0.4 + u, 0.3 + u * 0.7,
        new Color(0.53, 0.46, 0.37, Math.sin(u * Math.PI) * p.dust * p.impact), puff);
      const path = [point(origin, p.angle, side - Math.sin(a) * 0.4, d - Math.cos(a) * 0.4, height),
        pos, point(origin, p.angle, side + Math.sin(a) * 0.10, d + Math.cos(a) * 0.10, height)];
      ribbon(`fragment ${i}`, path, 0.12 * (1 - u), y + 0.065, ink.withAlpha(p.impact * (1 - u)));
    }
  },
};
