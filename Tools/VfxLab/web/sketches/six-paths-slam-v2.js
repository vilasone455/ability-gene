// Six Paths slam, v2: a proposal, not the game.
//
// What is in game is recorded from Source/RimArt/SixPaths (compare against "Six Paths: slam").
// This sketch starts from the same numbers -- SixPathsSlamTiming, SixPathsSlab, SixPathsHeight
// -- and adds what came out of looking at the in-game screenshots:
//
//   sun shadow cast along the scene's sun, so a 6-cell block reads as standing there
//   three face values that stay apart on brown terrain (top 0.35, front 0.20, side 0.08)
//   a bright outline with dimmer inner edges, instead of one uniform violet wireframe
//   a soft contact shadow that darkens and tightens as the block comes down
//   a 0.2-cell sink on impact (translation only; the block never rotates)
//   dust puffs, debris arcs and ground cracks, with dust north of the base drawn behind it
//   four ways to leave: fade, shatter, sink, split back into six orbs
//
// Every number worth arguing about is a param. "Copy as C# constants" on the Params panel
// turns the current values into the lines to paste into SixPathsSlamTiming.

import {
  AltitudeLayer, Color, Graphics, Material, MaterialPool, MaterialPropertyBlock, Mathf, Matrix4x4, Mesh,
  Meshes, Quaternion, ShaderDatabase, ShaderPropertyIDs, Vector3,
} from '../js/engine.js';
import { hash } from '../js/standins.js';

const Body = new Color(0.020, 0.015, 0.032);
const Tint = new Color(0.62, 0.52, 0.95);
const Rim = new Color(0.52, 0.36, 0.86);
const Flash = new Color(0.86, 0.80, 1.0);
const Dirt = new Color(0.36, 0.28, 0.20);
const Gain = 0.030;             // SixPathsHeight.Gain
const OrbitDepth = 0.60;        // SixPathsTiming.OrbitDepth
const Sweep = 210;              // SixPathsSlamTiming.Sweep

// Corner bits: 1 is +x, 2 is up, 4 is +z. Faces +x, -x, +y, -y, +z, -z, as in SixPathsSlab.
const FaceCorners = [[7, 5, 1, 3], [2, 0, 4, 6], [7, 3, 2, 6], [4, 0, 1, 5], [7, 6, 4, 5], [1, 0, 2, 3]];
const Normals = [[1, 0], [-1, 0], null, null, [0, 1], [0, -1]];
const Edges = (() => {
  const list = [];
  for (let c = 0; c < 8; c++)
    for (let bit = 1; bit <= 4; bit <<= 1) {
      if (c & bit) continue;
      const a = c, b = c | bit;
      const faces = FaceCorners.map((f, i) => (f.includes(a) && f.includes(b) ? i : -1)).filter((i) => i >= 0);
      list.push({ a, b, faces });
    }
  return list;
})();

const solid = new Material(ShaderDatabase.Transparent);
const glow = new Material(ShaderDatabase.MoteGlow);
const puff = MaterialPool.MatFrom('lab/puff', ShaderDatabase.Transparent);
const soft = MaterialPool.MatFrom('lab/soft-disc', ShaderDatabase.Transparent);
const softGlow = MaterialPool.MatFrom('lab/soft-disc', ShaderDatabase.MoteGlow);
const props = new MaterialPropertyBlock();
const disc = Meshes.disc(48, 'orb body');
const rimBand = Meshes.band(1, 1.14, 48, 'orb rim');
const ringBand = Meshes.band(0.9, 1, 96, 'impact ring');

// One mesh per role, rebuilt in place. Graphics.DrawMesh reads a mesh when the frame renders,
// so two things drawn in one frame must never share one -- the same rule SixPathsGraphics.cs keeps.
const scratch = new Map();
const mesh = (key) => scratch.get(key) ?? scratch.set(key, new Mesh(key)).get(key);

function draw(m, x, y, z, sx, sz, rot, colour, material = solid) {
  props.SetColor(ShaderPropertyIDs.Color, colour);
  Graphics.DrawMesh(m, Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0, rot, 0), new Vector3(sx, 1, sz)),
    material, 0, null, 0, props);
}

const rand = (i, k) => hash(i, k, 4242);
const Y = {
  filth: AltitudeLayer.Filth.AltitudeFor(),
  moteLow: AltitudeLayer.MoteLow.AltitudeFor(),
  shadows: AltitudeLayer.Shadows.AltitudeFor(),
  back: AltitudeLayer.MoteOverheadLow.AltitudeFor(),
  block: AltitudeLayer.MoteOverhead.AltitudeFor(),
};

const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });

export default {
  kit: 'Six Paths',
  label: 'Slam v2 (sketch)',
  compareWith: 'Six Paths: slam',
  params: {
    gather: P('Gather', 0.95, 0.3, 2, 0.01, 'Timing (s)'),
    fuse: P('Fuse', 0.18, 0.05, 0.6, 0.01, 'Timing (s)'),
    hang: P('Hang', 0.55, 0, 2, 0.01, 'Timing (s)'),
    fall: P('Fall', 0.22, 0.08, 1, 0.01, 'Timing (s)'),
    hold: P('Stand after landing', 0.9, 0, 3, 0.05, 'Timing (s)'),
    exitSeconds: P('Exit', 1.2, 0.3, 3, 0.05, 'Timing (s)'),

    width: P('Width', 2, 1, 4, 0.1, 'Block (cells)'),
    depth: P('Depth', 2, 1, 4, 0.1, 'Block (cells)'),
    height: P('Height', 6, 2, 10, 0.1, 'Block (cells)'),
    apex: P('Apex (centre height)', 9, 5, 16, 0.5, 'Block (cells)'),
    yaw: P('Yaw (degrees)', 25, 0, 45, 1, 'Block (cells)'),
    lift: P('Lift (north per cell up)', 0.6, 0.3, 1, 0.01, 'Block (cells)'),

    top: P('Top face', 0.35, 0, 1, 0.01, 'Faces'),
    front: P('Front face', 0.20, 0, 1, 0.01, 'Faces'),
    side: P('Side face', 0.08, 0, 1, 0.01, 'Faces'),
    seams: { label: 'Six seams (one per orb)', value: false, group: 'Faces' },

    outline: P('Outline brightness', 0.95, 0, 1, 0.01, 'Edges'),
    inner: P('Inner edge (x outline)', 0.4, 0, 1, 0.01, 'Edges'),
    edgeWidth: P('Edge width', 0.05, 0.01, 0.15, 0.005, 'Edges'),
    glowWidth: P('Glow width', 0.22, 0, 0.6, 0.01, 'Edges'),
    glowAlpha: P('Glow strength', 0.28, 0, 1, 0.01, 'Edges'),

    sunShadow: P('Sun shadow strength', 0.38, 0, 0.8, 0.01, 'Shadow'),
    contact: P('Contact shadow strength', 0.5, 0, 1, 0.01, 'Shadow'),
    softness: P('Soft edge (cells)', 0.14, 0, 0.5, 0.01, 'Shadow'),

    sink: P('Sink on impact (cells)', 0.2, 0, 0.6, 0.01, 'Impact'),
    sinkTime: P('Sink time (s)', 0.06, 0.02, 0.3, 0.01, 'Impact'),
    shake: P('Camera shake', 0.18, 0, 0.2, 0.01, 'Impact'),
    flash: P('Flash', 0.3, 0, 1, 0.01, 'Impact'),
    ring: P('Shock ring', 0.6, 0, 1, 0.01, 'Impact'),
    puffs: P('Dust puffs', 18, 0, 40, 1, 'Impact'),
    debris: P('Debris chunks', 14, 0, 40, 1, 'Impact'),
    cracks: P('Ground cracks', 9, 0, 20, 1, 'Impact'),

    exit: { label: 'Exit', value: 'shatter', options: ['fade', 'shatter', 'sink', 'orbs'], group: 'Exit' },
  },

  duration(p) { return times(p).exitAt + p.exitSeconds + 0.6; },

  phases(p) {
    const t = times(p);
    return [
      { name: 'Gather', t: 0 }, { name: 'Fuse', t: t.fuseAt }, { name: 'Hang', t: t.hangAt },
      { name: 'Fall', t: t.fallAt }, { name: 'Land', t: t.landAt }, { name: `Exit: ${p.exit}`, t: t.exitAt },
    ];
  },

  events(p) {
    const t = times(p), list = [{ t: t.landAt, type: 'shake', value: p.shake }];
    if (p.exit === 'shatter') list.push({ t: t.exitAt, type: 'shake', value: p.shake * 0.4 });
    return list;
  },

  draw(seconds, p, { origin, scene }) {
    const t = times(p);
    const sun = scene.shadowVector;
    drawGather(seconds, p, t, origin, sun);
    const pose = blockPose(seconds, p, t);
    if (pose) drawBlockAndShadows(seconds, p, t, origin, sun, pose);
    drawImpact(seconds, p, t, origin, sun);
    drawExit(seconds, p, t, origin, sun);
  },
};

function times(p) {
  const fuseAt = p.gather, hangAt = fuseAt + p.fuse, fallAt = hangAt + p.hang, landAt = fallAt + p.fall;
  return { fuseAt, hangAt, fallAt, landAt, exitAt: landAt + p.hold };
}

const progress = (s, start, span) => Mathf.Clamp01((s - start) / span);

// ---------------------------------------------------------------- gather: SixPathsSlamTiming.Orb

function drawGather(s, p, t, origin, sun) {
  if (s < t.fuseAt) {
    const u = progress(s, 0, p.gather), inward = u * u * u;
    for (let i = 0; i < 6; i++) {
      const angle = (i / 6) * Math.PI * 2 + u * Sweep * Mathf.Deg2Rad;
      const radius = Mathf.Lerp(2.8, 0, inward);
      const height = Mathf.Lerp(0.3, p.apex, Mathf.Smooth(u));
      const gx = origin.x + Math.cos(angle) * radius, gz = origin.z + Math.sin(angle) * radius * OrbitDepth;
      drawOrb(gx, gz, height, 0.42 * Mathf.Lerp(1, 0.72, inward), 1, p, sun);
    }
  }
  const flash = t.fuseAt <= s ? Math.sin(progress(s, t.fuseAt, p.fuse * 1.6) * Math.PI) : 0;
  // The fuse happens up at the apex, so the flash is drawn there rather than on the cell below.
  if (flash > 0.001 && s < t.fuseAt + p.fuse * 1.6)
    draw(disc, origin.x, Y.block + 0.05, origin.z + p.apex * p.lift, 3.2 * flash, 3.2 * flash, 0,
      Flash.withAlpha(flash * 0.55), softGlow);
}

function drawOrb(gx, gz, height, size, alpha, p, sun) {
  const grow = 1 + height * Gain;
  // Sun shadow on the ground, pushed along the sun by the orb's height.
  draw(disc, gx + sun.x * height, Y.shadows, gz + sun.z * height, size * 1.1, size * 0.8, 0,
    new Color(0, 0, 0, p.sunShadow * 0.55 * alpha * Mathf.Lerp(1, 0.4, height / 9)), soft);
  const x = gx, z = gz + height * p.lift, r = size * grow;
  draw(rimBand, x, Y.block, z, r, r, 0, Rim.withAlpha(0.9 * alpha));
  draw(disc, x, Y.block + 0.004, z, r, r, 0, Body.withAlpha(alpha));
  draw(disc, x - 0.22 * r, Y.block + 0.008, z + 0.26 * r, r * 0.34, r * 0.26, 0, new Color(0.72, 0.62, 0.95, 0.18 * alpha));
}

// ---------------------------------------------------------------- block

function blockPose(s, p, t) {
  const rest = p.height / 2;
  if (s < t.fuseAt) return null;
  if (s < t.hangAt) {
    const u = progress(s, t.fuseAt, p.fuse);
    return { centre: p.apex, scale: Mathf.Lerp(0.42, 1, Mathf.Smooth(u)) * (1 + 0.26 * Math.sin(u * Math.PI)), alpha: Mathf.Smooth(u / 0.5), sunk: 0 };
  }
  if (s < t.fallAt) {
    const u = progress(s, t.hangAt, p.hang);
    return { centre: p.apex + 0.3 * Math.sin(u * Math.PI * 2), scale: 1, alpha: 1, sunk: 0 };
  }
  if (s < t.landAt) {
    const left = 1 - progress(s, t.fallAt, p.fall);
    return { centre: rest + (p.apex - rest) * left * left, scale: 1, alpha: 1, sunk: 0, falling: true };
  }
  if (s >= t.exitAt && p.exit !== 'fade' && p.exit !== 'sink') {
    // Shatter and orbs take the block over; it lingers a moment so the swap has cover.
    const u = progress(s, t.exitAt, p.exit === 'orbs' ? p.exitSeconds * 0.25 : 0.05);
    if (u >= 1) return null;
    return { centre: rest, scale: 1, alpha: 1 - u, sunk: p.sink, exiting: true };
  }
  const sunk = p.sink * Mathf.Smooth(progress(s, t.landAt, p.sinkTime));
  let alpha = 1, extraSink = 0;
  if (s >= t.exitAt && p.exit === 'fade') alpha = 1 - Mathf.Smooth(progress(s, t.exitAt, p.exitSeconds));
  if (s >= t.exitAt && p.exit === 'sink') extraSink = (p.height - p.sink) * Mathf.Smooth(progress(s, t.exitAt, p.exitSeconds));
  if (alpha <= 0.001 || p.sink + extraSink >= p.height - 0.01) return null;
  return { centre: rest, scale: 1, alpha, sunk: sunk + extraSink };
}

/**
 * The eight corners of a box standing on its base. Sinking shortens it from the top rather than
 * moving it, because the part below the ground is not drawn at all.
 */
function corners(w, d, h, base, yaw, scale, lift, centreHeight) {
  const cy = Math.cos(yaw * Mathf.Deg2Rad), sy = Math.sin(yaw * Mathf.Deg2Rad);
  const out = [];
  for (let i = 0; i < 8; i++) {
    const lx = ((i & 1) ? 0.5 : -0.5) * w * scale, lz = ((i & 4) ? 0.5 : -0.5) * d * scale;
    const ly = centreHeight + ((i & 2) ? 0.5 : -0.5) * h * scale;
    const height = Math.max(base, ly);
    const x = lx * cy + lz * sy, z = lz * cy - lx * sy;
    out.push({ x, z: z + height * lift, gx: x, gz: z, height });
  }
  return out;
}

const facing = (c, f) => {
  const idx = FaceCorners[f];
  let area = 0;
  for (let i = 0; i < 4; i++) { const a = c[idx[i]], b = c[idx[(i + 1) & 3]]; area += a.x * b.z - b.x * a.z; }
  return area < 0;
};

function drawBox(key, ox, oz, y, c, p, alpha, yaw, { edges = true, glowEdges = true, seams = 0 } = {}) {
  const visible = FaceCorners.map((_, f) => facing(c, f));
  // Front is whichever visible upright face looks most toward the camera's south; the other is the side.
  const cy = Math.cos(yaw * Mathf.Deg2Rad), sy = Math.sin(yaw * Mathf.Deg2Rad);
  const southness = (f) => { const [nx, nz] = Normals[f]; return -(nz * cy - nx * sy); };
  const upright = [0, 1, 4, 5].filter((f) => visible[f]).sort((a, b) => southness(b) - southness(a));
  const value = (f) => (f === 2 ? p.top : f === upright[0] ? p.front : p.side);

  for (let f = 0; f < 6; f++) {
    if (!visible[f]) continue;
    const m = mesh(`${key}-face-${f}`);
    m.setFlat(FaceCorners[f].flatMap((i) => [c[i].x, c[i].z]), [0, 1, 2, 0, 2, 3]);
    draw(m, ox, y, oz, 1, 1, 0, Color.Lerp(Body, Tint, value(f)).withAlpha(alpha));
  }
  if (!edges) return;

  const outline = [], inner = [], halo = [];
  for (const e of Edges) {
    const shown = e.faces.filter((f) => visible[f]).length;
    if (shown === 0) continue;
    const target = shown === 1 ? outline : inner;
    quadAlong(target, c[e.a], c[e.b], p.edgeWidth);
    if (shown === 1 && p.glowWidth > 0) quadAlong(halo, c[e.a], c[e.b], p.glowWidth);
  }
  if (seams > 0)
    for (let k = 1; k < 6; k++)
      for (const f of upright) {
        const [ia, ib] = FaceCorners[f].filter((i) => !(i & 2));
        const a = c[ia], b = c[ib];
        // The same corner's top minus its bottom is the drawn height of that upright edge.
        const lift = (c[ia | 2].z - a.z) * (k / 6);
        quadAlong(inner, { x: a.x, z: a.z + lift }, { x: b.x, z: b.z + lift }, p.edgeWidth * 0.6, seams);
      }
  if (glowEdges && halo.length) flat(`${key}-halo`, halo, ox, y - 0.002, oz, Rim.withAlpha(p.glowAlpha * alpha), glow);
  flat(`${key}-inner`, inner, ox, y + 0.004, oz, Rim.withAlpha(p.outline * p.inner * alpha));
  flat(`${key}-outline`, outline, ox, y + 0.005, oz, Color.Lerp(Rim, Flash, 0.25).withAlpha(p.outline * alpha));
}

function quadAlong(list, a, b, width, weight = 1) {
  const dx = b.x - a.x, dz = b.z - a.z, len = Math.hypot(dx, dz);
  if (len < 1e-5) return;
  const ux = dx / len, uz = dz / len, h = width / 2 * weight;
  const nx = -uz * h, nz = ux * h, ex = ux * width / 2, ez = uz * width / 2;
  list.push([a.x - ex + nx, a.z - ez + nz, b.x + ex + nx, b.z + ez + nz, b.x + ex - nx, b.z + ez - nz, a.x - ex - nx, a.z - ez - nz]);
}

function flat(key, quads, x, y, z, colour, material = solid) {
  const xz = [], tri = [];
  quads.forEach((q, i) => { xz.push(...q); const v = i * 4; tri.push(v, v + 1, v + 2, v, v + 2, v + 3); });
  const m = mesh(key);
  m.setFlat(xz, tri);
  draw(m, x, y, z, 1, 1, 0, colour, material);
}

function hull(points) {
  const pts = points.slice().sort((a, b) => a.x - b.x || a.z - b.z);
  const cross = (o, a, b) => (a.x - o.x) * (b.z - o.z) - (a.z - o.z) * (b.x - o.x);
  const lower = [], upper = [];
  for (const q of pts) { while (lower.length >= 2 && cross(lower.at(-2), lower.at(-1), q) <= 0) lower.pop(); lower.push(q); }
  for (const q of pts.reverse()) { while (upper.length >= 2 && cross(upper.at(-2), upper.at(-1), q) <= 0) upper.pop(); upper.push(q); }
  return lower.slice(0, -1).concat(upper.slice(0, -1));
}

/** A convex shadow drawn as a core plus rings grown outward, which is what softens its edge. */
function softShadow(key, points, ox, oz, strength, softness) {
  const shape = hull(points);
  if (shape.length < 3 || strength <= 0.001) return;
  const cx = shape.reduce((s, q) => s + q.x, 0) / shape.length, cz = shape.reduce((s, q) => s + q.z, 0) / shape.length;
  const layers = softness > 0 ? 3 : 1;
  for (let l = 0; l < layers; l++) {
    const grow = softness * (l / 2 - 0.5);
    const xz = [cx, cz], tri = [];
    shape.forEach((q, i) => {
      const dx = q.x - cx, dz = q.z - cz, len = Math.hypot(dx, dz) || 1;
      xz.push(q.x + (dx / len) * grow, q.z + (dz / len) * grow);
      tri.push(0, i + 1, ((i + 1) % shape.length) + 1);
    });
    const m = mesh(`${key}-${l}`);
    m.setFlat(xz, tri);
    draw(m, ox, Y.shadows + l * 0.001, oz, 1, 1, 0, new Color(0, 0, 0, strength / layers));
  }
}

function drawBlockAndShadows(s, p, t, origin, sun, pose) {
  const rest = p.height / 2;
  const clearance = Math.max(0, pose.centre - rest * pose.scale);
  const scale = pose.scale * (1 + clearance * Gain);
  const h = p.height - pose.sunk;
  const centre = pose.sunk > 0 ? h / 2 : pose.centre;
  const c = corners(p.width, p.depth, h, 0, p.yaw, scale, p.lift, centre);

  // Sun shadow: every corner pushed along the sun by its own height, wrapped.
  const cast = c.map((q) => ({ x: q.gx + sun.x * q.height, z: q.gz + sun.z * q.height }));
  softShadow('sun', cast, origin.x, origin.z, p.sunShadow * pose.alpha * Mathf.Lerp(1, 0.35, clearance / 9), p.softness * (1 + clearance * 0.25));
  // Contact shadow straight below: faint and wide high up, dark and tight as it arrives.
  const near = 1 - Mathf.Clamp01(clearance / 9);
  const spread = 1 + (1 - near) * 0.6;
  softShadow('contact', c.map((q) => ({ x: q.gx * spread, z: q.gz * spread })), origin.x, origin.z,
    p.contact * pose.alpha * near * near, p.softness * (1 + (1 - near) * 4));

  if (pose.falling)
    for (let i = 2; i >= 1; i--) {
      const back = blockPose(s - i * 0.045, p, t);
      if (!back || back.centre <= pose.centre) continue;
      const g = corners(p.width, p.depth, p.height, 0, p.yaw, back.scale * (1 + Math.max(0, back.centre - rest) * Gain), p.lift, back.centre);
      drawBox(`ghost${i}`, origin.x, origin.z, Y.block - 0.01 * i, g, p, 0.22 - i * 0.06, p.yaw, { edges: false });
    }

  const seamGlow = p.seams ? 0.35 + 0.65 * Math.max(0, 1 - progress(s, t.fuseAt, 1.2)) : 0;
  drawBox('block', origin.x, origin.z, Y.block, c, p, pose.alpha, p.yaw, { seams: seamGlow });
}

// ---------------------------------------------------------------- impact

function drawImpact(s, p, t, origin, sun) {
  const since = s - t.landAt;
  if (since < 0) return;
  const tail = t.exitAt + p.exitSeconds;
  const marksAlpha = 1 - Mathf.Smooth(progress(s, tail, 0.6));

  // Cracks: jagged lines out from the base, drawn on the ground under everything else.
  if (p.cracks > 0) {
    const grow = Mathf.Smooth(progress(since, 0, 0.1));
    const quads = [];
    for (let i = 0; i < p.cracks; i++) {
      const angle = (i / p.cracks) * Math.PI * 2 + rand(i, 1) * 0.6;
      const reach = (1.2 + rand(i, 2) * 1.6) * grow;
      let x = Math.cos(angle) * p.width * 0.55, z = Math.sin(angle) * p.depth * 0.55;
      const steps = 4;
      for (let k = 0; k < steps; k++) {
        const a2 = angle + (rand(i, 10 + k) - 0.5) * 0.9;
        const nx = x + Math.cos(a2) * reach / steps, nz = z + Math.sin(a2) * reach / steps;
        quadAlong(quads, { x, z }, { x: nx, z: nz }, Mathf.Lerp(0.09, 0.025, k / steps));
        x = nx; z = nz;
      }
    }
    flat('cracks', quads, origin.x, Y.filth, origin.z, new Color(0.12, 0.08, 0.05, 0.6 * marksAlpha));
  }

  const flash = p.flash * Math.max(0, 1 - since / 0.22) ** 2;
  if (flash > 0.001) draw(disc, origin.x, Y.moteLow + 0.01, origin.z, Mathf.Lerp(2.5, 5, since / 0.22), Mathf.Lerp(2.5, 5, since / 0.22) * 0.8, 0, Flash.withAlpha(flash), softGlow);

  const ringU = progress(since, 0, 0.55);
  if (p.ring > 0 && ringU < 1) {
    const r = Mathf.Lerp(p.width * 0.6, 7.5, Mathf.Smooth(ringU));
    draw(ringBand, origin.x, Y.moteLow + 0.02, origin.z, r, r * 0.8, 0, Rim.withAlpha(p.ring * (1 - ringU) ** 2));
  }

  // Dust: rolls out along the ground from the base and rises a little. North of the base it is
  // behind the block, south of it in front -- the one ordering the flat block cannot get wrong.
  for (let i = 0; i < p.puffs; i++) {
    const life = 0.9 + rand(i, 20) * 0.6, u = since / life;
    if (u >= 1) continue;
    const angle = (i / p.puffs) * Math.PI * 2 + rand(i, 21);
    const edge = Math.max(p.width, p.depth) * 0.55;
    const out = edge + (0.6 + rand(i, 22) * 1.8) * Mathf.Smooth(Math.min(1, u * 1.8));
    const gx = Math.cos(angle) * out, gz = Math.sin(angle) * out * 0.8;
    const rise = u * (0.3 + rand(i, 23) * 0.5);
    const size = Mathf.Lerp(0.6, 1.9, Math.sqrt(u)) * (0.8 + rand(i, 24) * 0.5);
    const alpha = 0.55 * Math.sin(Math.min(1, u * 4) * Math.PI / 2) * (1 - u) ** 1.4;
    const behind = gz > 0;
    draw(planeMesh(), origin.x + gx, (behind ? Y.back : Y.block + 0.02) + i * 0.0001,
      origin.z + gz + rise * p.lift, size, size * 0.85, rand(i, 25) * 360 + u * 40,
      new Color(0.66, 0.58, 0.48, alpha), puff);
  }

  // Debris: dirt thrown up and falling back under gravity, each with a shadow while it flies.
  for (let i = 0; i < p.debris; i++) {
    const angle = rand(i, 30) * Math.PI * 2, speed = 1.4 + rand(i, 31) * 2.6, vy = 3 + rand(i, 32) * 4, g = 22;
    const flight = (2 * vy) / g, tau = Math.min(since, flight);
    const height = Math.max(0, vy * tau - 0.5 * g * tau * tau);
    const edge = Math.max(p.width, p.depth) * 0.5;
    const dist = edge + speed * tau;
    const gx = Math.cos(angle) * dist, gz = Math.sin(angle) * dist;
    const size = 0.1 + rand(i, 33) * 0.16;
    const fade = marksAlpha * (since > flight ? 0.85 : 1);
    if (height > 0.02)
      draw(disc, origin.x + gx + sun.x * height, Y.shadows, origin.z + gz + sun.z * height, size * 0.7, size * 0.5, 0, new Color(0, 0, 0, 0.3 * fade), soft);
    draw(planeMesh(), origin.x + gx, (gz > 0 && height < 1 ? Y.back : Y.block + 0.03), origin.z + gz + height * p.lift,
      size, size * 0.8, rand(i, 34) * 360 + tau * 720 * (rand(i, 35) - 0.5), Dirt.withAlpha(fade));
  }
}

let plane;
function planeMesh() {
  if (!plane) { plane = new Mesh('plane'); plane.setFlat([-0.5, -0.5, -0.5, 0.5, 0.5, 0.5, 0.5, -0.5], [0, 1, 2, 0, 2, 3]); plane.uv = new Float32Array([0, 0, 0, 1, 1, 1, 1, 0]); }
  return plane;
}

// ---------------------------------------------------------------- exits

function drawExit(s, p, t, origin, sun) {
  const since = s - t.exitAt;
  if (since < 0) return;
  const u = progress(s, t.exitAt, p.exitSeconds);

  if (p.exit === 'sink' && u < 1) {
    // A skirt of dust where the block meets the ground hides the line it is disappearing through.
    for (let i = 0; i < 10; i++) {
      const a = (i / 10) * Math.PI * 2 + since * 0.8, r = Math.max(p.width, p.depth) * 0.6;
      const gz = Math.sin(a) * r * 0.8;
      draw(planeMesh(), origin.x + Math.cos(a) * r, gz > 0 ? Y.back : Y.block + 0.02, origin.z + gz, 1.2, 1.0,
        a * 57 + since * 30, new Color(0.62, 0.55, 0.46, 0.45 * Math.sin(u * Math.PI)), puff);
    }
  }

  if (p.exit === 'shatter') {
    const w = Math.round(p.width), d = Math.round(p.depth), h = Math.round(p.height - p.sink);
    const cy = Math.cos(p.yaw * Mathf.Deg2Rad), sy = Math.sin(p.yaw * Mathf.Deg2Rad);
    const fade = 1 - Mathf.Smooth(progress(u, 0.55, 0.45));
    if (fade <= 0) return;
    const chunkParams = { ...p, inner: 0, edgeWidth: p.edgeWidth * 0.7, outline: p.outline * 0.8 };
    let n = 0;
    for (let ix = 0; ix < w; ix++) for (let iz = 0; iz < d; iz++) for (let iy = 0; iy < h; iy++, n++) {
      const lx = (ix + 0.5) * (p.width / w) - p.width / 2, lz = (iz + 0.5) * (p.depth / d) - p.depth / 2, ly = (iy + 0.5) * ((p.height - p.sink) / h);
      const x0 = lx * cy + lz * sy, z0 = lz * cy - lx * sy;
      const len = Math.hypot(x0, z0) || 1;
      const speed = 0.8 + rand(n, 40) * 2.2, vy = 1 + (iy / h) * 3 + rand(n, 41) * 1.5, g = 16;
      const piece = 0.5 * 0.92;
      // Lands when its underside reaches the ground, then stays put.
      const land = (vy + Math.sqrt(vy * vy + 2 * g * (ly - piece))) / g;
      const tau = Math.min(since, land);
      const y = Math.max(piece, ly + vy * tau - 0.5 * g * tau * tau);
      const gx = x0 + (x0 / len) * speed * tau, gz = z0 + (z0 / len) * speed * tau;
      const spin = p.yaw + (rand(n, 42) - 0.5) * 240 * tau;
      const c = corners(0.92 * p.width / w, 0.92 * p.depth / d, 0.92 * (p.height - p.sink) / h, 0, spin, 1, p.lift, y);
      const base = y - piece;
      if (base > 0.05)
        draw(disc, origin.x + gx + sun.x * base, Y.shadows, origin.z + gz + sun.z * base, 0.5, 0.4, 0, new Color(0, 0, 0, 0.25 * fade), soft);
      // Northern pieces first, so nearer ones cover them.
      // Outline only: 24 pieces each drawing inner edges read as a tangle of wire, not rubble.
      drawBox(`chunk${n}`, origin.x + gx, origin.z + gz, Y.block - gz * 0.0005 + y * 0.00002, c, chunkParams, fade, spin, { glowEdges: false });
    }
    const pop = Math.max(0, 1 - since / 0.2);
    if (pop > 0) draw(disc, origin.x, Y.block + 0.05, origin.z + p.height * 0.5 * p.lift, 3 * pop + 1, (3 * pop + 1) * 1.3, 0, Flash.withAlpha(pop * 0.5), softGlow);
  }

  if (p.exit === 'orbs') {
    // Six orbs out of six cells of height, each leaving from the cell of block it was.
    const leave = progress(u, 0.15, 0.6), settle = Mathf.Smooth(leave), fade = 1 - Mathf.Smooth(progress(u, 0.8, 0.2));
    const pulse = Math.max(0, 1 - Math.abs(u - 0.12) / 0.12);
    if (pulse > 0) draw(disc, origin.x, Y.block + 0.05, origin.z + p.height * 0.5 * p.lift, 2.4, 3.6, 0, Flash.withAlpha(pulse * 0.45), softGlow);
    if (u < 0.12 || fade <= 0) return;
    for (let i = 0; i < 6; i++) {
      const fromHeight = (i + 0.5) * (p.height - p.sink) / 6;
      const angle = (i / 6) * Math.PI * 2 + settle * 0.9;
      const radius = 2.0 * settle;
      const height = Mathf.Lerp(fromHeight, 1.2, settle);
      const gx = origin.x + Math.cos(angle) * radius, gz = origin.z + Math.sin(angle) * radius * OrbitDepth;
      drawOrb(gx, gz, height, 0.42, fade, p, sun);
    }
  }
}
