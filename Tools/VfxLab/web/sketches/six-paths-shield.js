// Six Paths shield wall: a proposal, not the game. Nothing in Source/RimArt draws this yet, so
// there is no recording to compare it with.
//
// What it is for (proposed, none of it agreed). The caster aims a direction. The six orbs raise a
// curved wall of six standing plates on an arc 2.2 cells out, 150 degrees wide (about 5.8 cells
// of wall), 1.4 cells tall. A projectile whose path crosses the arc from outside is stopped on the
// plate it meets; shots from inside pass out. Pawns walk through it. Each plate absorbs 120
// damage and then breaks, leaving a gap; the wall lasts 12 s; 45 s cooldown. Unlike a low-shield
// pack it covers everyone standing behind the arc, not one pawn, and only from one side.
//
// Order and default timings:
//   0.00  six orbs lie on the floor around the caster and spread out along it (0.35 s)
//   0.35  each flattens into a tile lying on the ground outside the arc (0.20 s)
//   0.55  tiles swing up on their base edge, middle pair first, 0.25 s stagger, 0.26 s each,
//         with a small over-swing and a puff of dust at the base as each one lands upright
//   1.06  wall holds (2.60 s here; 12 s in game). Scripted shots hit it to show the rule.
//   3.66  plates sink straight down into the floor, outer ones first (0.45 s), marks fade
// "push" rise instead sinks each orb into a slot and pushes the plate straight up out of it.
//
// Depth. The camera is flat, so every cue is drawn:
//   - each plate is a solid slab, not a sprite: the Shield form's outline (SixPathsShapes.Shield,
//     corner 4.5) with thickness, projected with SixPathsHeight.Lift (0.60 cells north per cell
//     up). Only faces turned toward the camera are drawn, so the top edge and the camera-side
//     flank show as a visible thickness.
//   - faces are shaded by the lab sun (the same direction the shadows use): the lit top edge,
//     darker flanks, and a face tone that changes plate to plate around the curve. One colour per
//     draw call, so the edge strips are grouped into 6 brightness steps, one mesh each.
//   - each plate casts its own outline along the sun, starting at its base.
//   - a hit ripple is a ring drawn in the plate's own plane, so it foreshortens with the plate.
//   - plates north of the caster draw below the Pawn layer, plates south of it above, and within
//     a layer nearer plates draw later.
//
// Port notes for the C# version:
//   - a plate is SixPathsShapes.Outline(Shield) mapped through (T, V, W) plate axes; the same
//     MorphMesh topology with a second ring of vertices for the back face
//   - the altitude split is per caster; another pawn standing next to a plate will sort wrongly
//     against it. Fine for a wall, worth knowing.
//   - slots, dust and shadows need InBounds and Fogged checks per cell
//
// "Copy as C# constants" on the Params panel turns the current values into the lines to paste.

import {
  AltitudeLayer, Color, Mathf, MaterialPool, Meshes, MeshPool, ShaderDatabase,
} from '../js/engine.js';
import { hash } from '../js/standins.js';
import {
  Body, Rim, Sheen, Lift, Segments, add, at, draw, dot, drawSolid, drawSolidShadow, mesh, mul, screenX,
  screenZ, sunLight, unit, Up, v3, View, FillFrom,
} from './lib/six-paths-solid.js';

const Dirt = new Color(0.34, 0.26, 0.19);
const Tracer = new Color(1.0, 0.80, 0.48);
const OrbRadius = 0.42;         // the size SixPathsGraphics draws an orb at
const TAU = Math.PI * 2;

const additive = MaterialPool.MatFrom('white', ShaderDatabase.MoteGlow);
const soft = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent);
const softGlow = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.MoteGlow);
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const pawn = MaterialPool.MatFrom('canvas:pawn', ShaderDatabase.Transparent);

const disc = Meshes.disc(48, 'shield orb');
const rimBand = Meshes.band(1, 1.14, 48, 'shield orb rim');

const rand = (i, k) => hash(i, k, 7117);
const progress = (s, start, span) => Mathf.Clamp01((s - start) / Math.max(span, 1e-5));
const easeOut = (u) => 1 - (1 - Mathf.Clamp01(u)) ** 3;

const Y = {
  filth: AltitudeLayer.Filth.AltitudeFor(),
  shadows: AltitudeLayer.Shadows.AltitudeFor(),
  behind: AltitudeLayer.Building.AltitudeFor(),
  pawn: AltitudeLayer.Pawn.AltitudeFor(),
  front: AltitudeLayer.MoteOverhead.AltitudeFor(),
};

const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });

export default {
  kit: 'Six Paths',
  label: 'Shield wall (sketch)',
  params: {
    gather: P('Orbs spread out', 0.35, 0.05, 1.5, 0.05, 'Timing (s)'),
    form: P('Orbs flatten into tiles', 0.20, 0.05, 1, 0.01, 'Timing (s)'),
    stagger: P('Plates start over', 0.25, 0, 1, 0.01, 'Timing (s)'),
    rise: P('One plate stands up in', 0.26, 0.08, 1, 0.01, 'Timing (s)'),
    hold: P('Wall holds', 2.60, 0.5, 8, 0.05, 'Timing (s)'),
    exitStagger: P('Plates sink over', 0.15, 0, 1, 0.01, 'Timing (s)'),
    sink: P('One plate sinks in', 0.45, 0.1, 1.5, 0.05, 'Timing (s)'),
    marksFade: P('Marks fade', 0.50, 0, 2, 0.05, 'Timing (s)'),

    riseMode: { label: 'How plates come up', value: 'hinge', options: ['hinge', 'push'], group: 'Wall' },
    aim: P('Aim (degrees, 0 = north, 90 = east)', 20, 0, 360, 1, 'Wall'),
    count: P('Plates', 6, 2, 10, 1, 'Wall'),
    radius: P('Arc radius (cells)', 2.2, 1, 5, 0.05, 'Wall'),
    span: P('Arc width (degrees)', 150, 30, 360, 5, 'Wall'),
    gap: P('Gap between plates (cells)', 0.06, 0, 0.5, 0.01, 'Wall'),
    orbStart: P('Orbs start this far out (cells)', 0.9, 0.3, 2, 0.05, 'Wall'),

    height: P('Plate height (cells)', 1.40, 0.4, 3, 0.05, 'Plate'),
    thick: P('Plate thickness (cells)', 0.16, 0.02, 0.5, 0.01, 'Plate'),
    corner: P('Corner squareness (Shield = 4.5)', 4.5, 2, 12, 0.1, 'Plate'),
    lean: P('Lean outward (degrees)', 6, -20, 30, 1, 'Plate'),
    overshoot: P('Over-swing past upright (degrees)', 10, 0, 40, 1, 'Plate'),

    shade: P('Sun shading strength', 0.60, 0, 2, 0.01, 'Look'),
    fill: P('Camera-side fill light', 0.35, 0, 1, 0.01, 'Look'),
    ambient: P('Unlit brightness', 0.06, 0, 0.5, 0.01, 'Look'),
    bevel: P('Face edge line width (cells)', 0.035, 0, 0.15, 0.005, 'Look'),
    bevelGlow: P('Face edge line brightness', 0.75, 0, 1, 0.01, 'Look'),
    sheen: P('Diagonal sheen', 0.10, 0, 0.5, 0.01, 'Look'),
    seams: P('Glow at the joins', 0.35, 0, 1, 0.01, 'Look'),
    sunShadow: P('Sun shadow strength', 0.38, 0, 0.8, 0.01, 'Look'),
    contact: P('Dark line at the base', 0.45, 0, 1, 0.01, 'Look'),

    hits: P('Shots hitting it (demo)', 4, 0, 12, 1, 'Hits'),
    bulletSpeed: P('Shot speed (cells/s)', 30, 5, 80, 1, 'Hits'),
    ripple: P('Ripple brightness', 0.9, 0, 1, 0.01, 'Hits'),
    rippleSize: P('Ripple radius (cells)', 0.45, 0.1, 1, 0.01, 'Hits'),
    sparks: P('Sparks per hit', 5, 0, 12, 1, 'Hits'),

    dust: P('Dust per plate', 3, 0, 8, 1, 'Ground'),
    shake: P('Camera shake', 0.03, 0, 0.2, 0.01, 'Ground'),
    caster: { label: 'Draw a caster stand-in at the centre', value: true, group: 'Ground' },
  },

  duration(p) { return times(p).downAt + p.marksFade; },

  phases(p) {
    const t = times(p);
    return [
      { name: 'Orbs spread', t: 0 }, { name: 'Tiles form', t: p.gather }, { name: 'Stand up', t: t.formEnd },
      { name: 'Holding', t: t.upAt }, { name: 'Sink', t: t.holdEnd },
    ];
  },

  events(p) {
    const t = times(p);
    return p.shake > 0 ? [{ t: t.formEnd + p.rise * landAt(p), type: 'shake', value: p.shake }] : [];
  },

  draw(seconds, p, { origin, scene }) {
    const t = times(p);
    const sun = scene.shadowVector;
    const light = sunLight(sun);
    const layouts = [];
    for (let i = 0; i < p.count; i++) layouts.push(layout(i, p, origin));

    if (p.caster) draw(MeshPool.plane10, origin.x, Y.pawn, origin.z + 0.15, 1, 1, 0, Color.white, pawn);

    drawGround(seconds, p, t, layouts);
    if (p.riseMode === 'push') drawSinkingOrbs(seconds, p, t, origin, layouts, sun);

    // Build every plate first, then draw nearest last.
    const slabs = [];
    for (let i = 0; i < p.count; i++) {
      const slab = slabAt(i, seconds, p, t, layouts[i], origin);
      if (slab) slabs.push(slab);
    }
    slabs.sort((a, b) => a.key - b.key);
    const alts = new Map();
    let back = 0, front = 0;
    for (const slab of slabs) {
      const behind = layouts[slab.i].base.z > origin.z + 0.01;
      const alt = (behind ? Y.behind : Y.front) + 0.03 + 0.03 * (behind ? back++ : front++);
      alts.set(slab.i, alt);
      drawSolidShadow(`plate-${slab.i}`, slab, sun, p.sunShadow);
      drawSlab(slab, alt, light, p, seconds, t, layouts[slab.i]);
    }

    drawSeams(seconds, p, t, layouts, alts, origin);
    drawHits(seconds, p, t, layouts, slabs, alts, origin);
    drawDust(seconds, p, t, layouts, alts);
  },
};

// ---------------------------------------------------------------- timing and layout

/** Fraction of the rise at which a hinged plate reaches upright; the rest is the over-swing. */
const landAt = (p) => (p.riseMode === 'hinge' ? 0.72 : 1);

function times(p) {
  const formEnd = p.gather + p.form;
  const upAt = formEnd + p.stagger + p.rise;
  const holdEnd = upAt + p.hold;
  return { formEnd, upAt, holdEnd, downAt: holdEnd + p.exitStagger + p.sink };
}

/** Where plate i stands: its base centre on the arc, its outward normal and its tangent. */
function layout(i, p, origin) {
  const step = p.span / p.count;
  const beta = (p.aim + (i - (p.count - 1) / 2) * step) * Mathf.Deg2Rad;
  const N = v3(Math.sin(beta), 0, Math.cos(beta));
  // Quaternion.Euler(0, beta) sends local +x here, so a plane10 rotated by beta lies along it.
  const T = v3(Math.cos(beta), 0, -Math.sin(beta));
  const half = Math.max((p.count - 1) / 2, 1);
  return {
    beta, N, T,
    base: v3(origin.x + N.x * p.radius, 0, origin.z + N.z * p.radius),
    width: Math.max(0.1, 2 * p.radius * Math.sin((step * Mathf.Deg2Rad) / 2) - p.gap),
    // 0 for the middle plates, 1 for the ends: the order they come up in.
    order: Math.abs(i - (p.count - 1) / 2) / half,
  };
}

const riseStart = (L, p, t) => t.formEnd + L.order * p.stagger;
const sinkStart = (L, p, t) => t.holdEnd + (1 - L.order) * p.exitStagger;

/**
 * Plate axes for a tilt: theta 0 lies flat on the floor pointing outward, pi/2 stands upright.
 * T runs along the arc, V from the base edge to the top, W out of the outer face.
 */
function axes(L, theta) {
  const s = Math.sin(theta), c = Math.cos(theta);
  return { T: L.T, V: add(mul(Up, s), mul(L.N, c)), W: add(mul(L.N, s), mul(Up, -c)) };
}

/** Plate i at this moment, or null when there is nothing of it to draw. */
function slabAt(i, s, p, t, L, origin) {
  const H = p.height;
  const upright = (90 - p.lean) * Mathf.Deg2Rad;
  const start = riseStart(L, p, t);
  let theta = upright, pushed = 1;
  let hx = L.width / 2, hy = H / 2, corner = p.corner, th = p.thick;
  let C = L.base;
  let alpha = 1;

  if (p.riseMode === 'hinge') {
    if (s < start) {
      // Still on the floor: the orb slides out, then spreads into a tile lying outside the arc.
      theta = 0;
      const k = Mathf.Smooth(progress(s, p.gather, p.form));
      hx = Mathf.Lerp(OrbRadius, L.width / 2, k);
      hy = Mathf.Lerp(OrbRadius, H / 2, k);
      corner = Mathf.Lerp(2, p.corner, k);
      th = Mathf.Lerp(0.05, p.thick, k);
      const out = Mathf.Lerp(p.orbStart, p.radius + H / 2, Mathf.Smooth(progress(s, 0, p.gather)));
      C = v3(origin.x + L.N.x * out - L.N.x * hy, 0, origin.z + L.N.z * out - L.N.z * hy);
    } else {
      const u = progress(s, start, p.rise), land = landAt(p);
      if (u < land) {
        // Accelerates the whole way up, so it arrives with a knock rather than easing in.
        const a = u / land;
        theta = upright * a * a;
      } else {
        const b = (u - land) / (1 - land);
        theta = upright + p.overshoot * Mathf.Deg2Rad * Math.sin(b * Math.PI) * (1 - b);
      }
    }
  } else {
    if (s < start) return null;
    pushed = easeOut(progress(s, start, p.rise));
  }

  const sunk = Math.max(1 - pushed, Mathf.Smooth(progress(s, sinkStart(L, p, t), p.sink)));
  if (sunk >= 0.999) return null;

  const F = axes(L, theta);
  // Lying tiles sit on the floor rather than half in it.
  C = add(add(C, mul(Up, (th / 2) * Math.cos(theta))), mul(F.V, -sunk * H));
  const vMin = C.y < 0 && F.V.y > 0.05 ? -C.y / F.V.y : -Infinity;
  const vc = hy;
  const centre = add(C, mul(F.V, Math.max(vc, vMin)));
  return { i, C, T: F.T, V: F.V, W: F.W, hx, hy, vc, vMin, corner, th, alpha, theta, key: dot(centre, View) };
}

// ---------------------------------------------------------------- the plate as a solid

function drawSlab(slab, alt, light, p, s, t, L) {
  const look = { shade: p.shade, fill: p.fill, ambient: p.ambient, bevel: p.bevel, bevelGlow: p.bevelGlow,
    flash: hitFlash(slab.i, s, p, t) };
  const facing = drawSolid(`plate-${slab.i}`, slab, alt, light, look);

  // A diagonal sheen across the face, well inside the edge. Standing plates only: on a tile
  // lying flat it reads as a scratch.
  const upright = Math.sin(Mathf.Clamp(slab.theta, 0, Math.PI / 2)) ** 3;
  if (p.sheen > 0 && upright > 0.01) {
    const wf = (facing * slab.th) / 2, w = slab.hx * 2, bottom = slab.vc - slab.hy, h = slab.hy * 2;
    const quad = [[-0.34, 0.22], [-0.20, 0.22], [0.10, 0.84], [-0.04, 0.84]];
    const sxz = [];
    for (const [qu, qv] of quad) {
      const P3 = at(slab, qu * w, Math.max(slab.vMin, bottom + qv * h), wf);
      sxz.push(screenX(P3), screenZ(P3));
    }
    const sm = mesh(`plate-${slab.i}-sheen`);
    sm.setFlat(sxz, [0, 1, 2, 0, 2, 3]);
    const faceNormal = mul(slab.W, facing);
    draw(sm, 0, alt + 0.005, 0, 1, 1, 0,
      Sheen.withAlpha(p.sheen * upright * (0.5 + Math.max(0, dot(faceNormal, FillFrom))) * slab.alpha));
  }
}

// ---------------------------------------------------------------- the floor

/** How far plate i is up, 0..1, for the marks that follow it. */
function standing(i, s, p, t, L) {
  const up = p.riseMode === 'hinge'
    ? progress(s, riseStart(L, p, t) + p.rise * landAt(p) * 0.6, p.rise * 0.4)
    : progress(s, riseStart(L, p, t), p.rise);
  return up * (1 - Mathf.Smooth(progress(s, sinkStart(L, p, t), p.sink)));
}

function drawGround(s, p, t, layouts) {
  const fade = 1 - Mathf.Smooth(progress(s, t.downAt, p.marksFade));
  for (let i = 0; i < layouts.length; i++) {
    const L = layouts[i];
    const rot = L.beta * Mathf.Rad2Deg;
    const up = standing(i, s, p, t, L);
    // Contact shade along the base while the plate stands on it.
    if (p.contact > 0 && up > 0)
      draw(MeshPool.plane10, L.base.x, Y.filth + 0.004, L.base.z, L.width * 1.2, p.thick * 3.2, rot,
        new Color(0, 0, 0, p.contact * up), soft);
    // A slot in the floor for the push rise, open from the orb going in until the marks fade.
    if (p.riseMode === 'push') {
      const open = Mathf.Smooth(progress(s, p.gather + p.form * 0.3, p.form * 0.7 + 0.05)) * fade;
      if (open <= 0) continue;
      draw(MeshPool.plane10, L.base.x, Y.filth, L.base.z, L.width * 1.35 * open, 0.6, rot, Dirt.withAlpha(0.45 * open), soft);
      draw(MeshPool.plane10, L.base.x, Y.filth + 0.002, L.base.z, L.width * open, p.thick * 1.4, rot,
        new Color(0, 0, 0, 0.55 * open));
    }
  }
}

/** Push rise only: the orbs slide to their slots and sink into them, as the rods sketch does. */
function drawSinkingOrbs(s, p, t, origin, layouts, sun) {
  if (s >= t.formEnd) return;
  const g = Mathf.Smooth(progress(s, 0, p.gather));
  const u = Mathf.Smooth(progress(s, p.gather, p.form));
  for (const L of layouts) {
    const r = Mathf.Lerp(p.orbStart, p.radius, g);
    const x = origin.x + L.N.x * r, z = origin.z + L.N.z * r;
    const size = OrbRadius * (1 - u * 0.15), left = 1 - u;
    const alpha = 1 - Mathf.Smooth(Mathf.Clamp01((u - 0.8) / 0.2));
    draw(MeshPool.plane10, x + sun.x * 0.2, Y.shadows, z + sun.z * 0.2, size * 2.4, size * 1.7 * Mathf.Lerp(0.5, 1, left), 0,
      new Color(0, 0, 0, p.sunShadow * alpha), soft);
    draw(rimBand, x, Y.front, z, size, size * left, 0, Rim.withAlpha(0.9 * alpha));
    draw(disc, x, Y.front + 0.004, z, size, size * left, 0, Body.withAlpha(alpha));
  }
}

/** A puff at each side of the base when a plate lands upright. */
function drawDust(s, p, t, layouts, alts) {
  for (let i = 0; i < layouts.length; i++) {
    const L = layouts[i];
    const since = s - (riseStart(L, p, t) + p.rise * landAt(p));
    if (since < 0 || !alts.has(i)) continue;
    for (let k = 0; k < p.dust; k++) {
      const life = 0.45 + rand(i * 16 + k, 1) * 0.3, u = since / life;
      if (u >= 1) continue;
      const along = (rand(i * 16 + k, 2) - 0.5) * L.width;
      const side = k % 2 ? 1 : -1;
      const out = (0.12 + rand(i * 16 + k, 3) * 0.3) * easeOut(u);
      const x = L.base.x + L.T.x * along + L.N.x * side * out, z = L.base.z + L.T.z * along + L.N.z * side * out;
      // Dust on the camera side of the plate covers it; dust behind it is covered.
      const nearSide = side * -L.N.z > 0;
      const size = Mathf.Lerp(0.3, 0.75, Math.sqrt(u));
      draw(MeshPool.plane10, x, alts.get(i) + (nearSide ? 0.012 : -0.012) + k * 0.0001, z + u * 0.12 * Lift,
        size, size * 0.85, rand(i * 16 + k, 4) * 360,
        new Color(0.60, 0.54, 0.46, 0.32 * Math.sin(Math.min(1, u * 4) * Math.PI / 2) * (1 - u) ** 1.3), puff);
    }
  }
}

/** Soft vertical glow where two standing plates meet: the wall is one barrier, not six. */
function drawSeams(s, p, t, layouts, alts, origin) {
  if (p.seams <= 0) return;
  for (let i = 0; i + 1 < layouts.length; i++) {
    const a = layouts[i], b = layouts[i + 1];
    const up = Math.min(standing(i, s, p, t, a), standing(i + 1, s, p, t, b));
    if (up <= 0 || !alts.has(i) || !alts.has(i + 1)) continue;
    const beta = (a.beta + b.beta) / 2;
    const x = origin.x + Math.sin(beta) * p.radius, z = origin.z + Math.cos(beta) * p.radius;
    const tall = p.height * up * Lift;
    const pulse = 0.75 + 0.25 * Math.sin(s * 5 + i * 1.7);
    draw(MeshPool.plane10, x, Math.max(alts.get(i), alts.get(i + 1)) + 0.007, z + tall / 2,
      0.16, tall * 1.25, 0, Sheen.withAlpha(p.seams * pulse * up), softGlow);
  }
}

// ---------------------------------------------------------------- shots stopped by the wall

/** The demo's shots: fixed per index, spread over the hold. */
function hitList(p, t) {
  const list = [];
  for (let k = 0; k < p.hits; k++) {
    list.push({
      at: t.upAt + p.hold * (0.10 + 0.70 * (k + rand(k, 1) * 0.5) / Math.max(p.hits, 1)),
      plate: Math.floor(rand(k, 2) * p.count) % p.count,
      u: (rand(k, 3) - 0.5) * 0.6,
      v: 0.30 + rand(k, 4) * 0.40,
      spread: (rand(k, 5) - 0.5) * 50 * Mathf.Deg2Rad,
    });
  }
  return list;
}

function hitFlash(i, s, p, t) {
  let f = 0;
  for (const h of hitList(p, t)) if (h.plate === i && s >= h.at) f += Math.exp(-(s - h.at) * 9);
  return Math.min(1, f);
}

function drawHits(s, p, t, layouts, slabs, alts, origin) {
  for (const [k, h] of hitList(p, t).entries()) {
    const slab = slabs.find((x) => x.i === h.plate);
    if (!slab || !alts.has(h.plate)) continue;
    const L = layouts[h.plate], alt = alts.get(h.plate);
    const u = h.u * slab.hx * 2, v = slab.vc - slab.hy + h.v * slab.hy * 2;
    if (v < slab.vMin) continue;
    const hit = at(slab, u, v, slab.th / 2);
    const outerShows = dot(slab.W, View) >= 0;
    const over = outerShows ? 0.010 : -0.010;
    // Direction the shot travels back out along, level with the hit.
    const c = Math.cos(h.spread), sn = Math.sin(h.spread);
    const back = v3(L.N.x * c + L.N.z * sn, 0, -L.N.x * sn + L.N.z * c);

    const before = h.at - s;
    if (before > 0 && before < 0.25) {
      const head = add(hit, mul(back, before * p.bulletSpeed));
      const tail = add(head, mul(back, 1.1));
      const hx = screenX(head), hz = screenZ(head), tx = screenX(tail), tz = screenZ(tail);
      const len = Math.hypot(tx - hx, tz - hz) || 1, nx = -(tz - hz) / len * 0.025, nz = (tx - hx) / len * 0.025;
      const m = mesh(`tracer-${k}`);
      m.setFlat([hx + nx, hz + nz, tx, tz, hx - nx, hz - nz], [0, 1, 2]);
      draw(m, 0, alt + over, 0, 1, 1, 0, Tracer.withAlpha(0.95), additive);
    }

    const since = s - h.at;
    if (since < 0 || since > 0.6) continue;
    const q = since / 0.45;
    if (q < 1) {
      // Flash where it struck.
      draw(MeshPool.plane10, screenX(hit), alt + 0.012, screenZ(hit), 0.9 * (1 - q * 0.5), 0.9 * (1 - q * 0.5), 0,
        Sheen.withAlpha(p.ripple * (1 - q) ** 2), softGlow);
      // A ring spreading in the plate's own plane, held inside its edge, on the face you can see.
      const wf = ((outerShows ? 1 : -1) * slab.th) / 2;
      const r = 0.05 + p.rippleSize * easeOut(q), width = 0.05 * (1 - q) + 0.015;
      const um = slab.hx * 0.92, vlo = Math.max(slab.vMin, slab.vc - slab.hy * 0.92), vhi = slab.vc + slab.hy * 0.92;
      const rm = mesh(`ripple-${k}`), rxz = [], rtri = [];
      const ringSeg = 32;
      for (let j = 0; j <= ringSeg; j++) {
        const a = (j / ringSeg) * TAU;
        for (const rr of [r, r + width]) {
          const P3 = at(slab, Mathf.Clamp(u + Math.cos(a) * rr, -um, um), Mathf.Clamp(v + Math.sin(a) * rr, vlo, vhi), wf);
          rxz.push(screenX(P3), screenZ(P3));
        }
        if (j < ringSeg) { const b = j * 2; rtri.push(b, b + 2, b + 1, b + 1, b + 2, b + 3); }
      }
      rm.setFlat(rxz, rtri);
      draw(rm, 0, alt + 0.006, 0, 1, 1, 0, Sheen.withAlpha(p.ripple * (1 - q) ** 1.5), additive);
    }
    // Sparks thrown back off the outer face and falling to the floor.
    for (let j = 0; j < p.sparks; j++) {
      const turn = (rand(k * 13 + j, 6) - 0.5) * 2.2;
      const cs = Math.cos(turn), ss = Math.sin(turn);
      const dir = v3(back.x * cs + back.z * ss, 0, -back.x * ss + back.z * cs);
      const speed = 1.2 + rand(k * 13 + j, 7) * 2.2, rise = 1.0 + rand(k * 13 + j, 8) * 2.0, g = 12;
      const y = hit.y + rise * since - 0.5 * g * since * since;
      if (y <= 0) continue;
      const P3 = v3(hit.x + dir.x * speed * since, y, hit.z + dir.z * speed * since);
      const size = 0.08 * (1 - since / 0.6);
      draw(MeshPool.plane10, screenX(P3), alt + over + 0.002, screenZ(P3), size, size, 0,
        Tracer.withAlpha(1 - since / 0.6), softGlow);
    }
  }
}
