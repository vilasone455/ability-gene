// Six Paths solids: a flat Six Paths outline given thickness and drawn under RimWorld's
// straight-down camera, so an orb form can stand, tilt and lean instead of lying on the map as a
// sprite. Shared by the shield wall and nullify sketches. Not a sketch itself, so it is not listed
// in sketches/index.js.
//
// A body is one outline (SixPathsShapes.Outline: superellipse plus taper) placed in 3D by three
// axes -- T along u, V along v, W out of the face -- and extruded along W. Height is drawn as
// SixPathsHeight.Lift, 0.60 cells north per cell up. Only surfaces turned toward the camera are
// drawn, and edge strips are grouped into a few brightness steps because a draw call has one
// colour.
//
// Port notes: the outline is SixPathsShapes.Outline; the extrusion is MorphMesh with a second
// ring of vertices; the edge groups are one Mesh each, rebuilt only while the body moves.

import {
  AltitudeLayer, Color, Graphics, MaterialPool, MaterialPropertyBlock, Matrix4x4, Mesh, Quaternion,
  ShaderDatabase, ShaderPropertyIDs, Vector3,
} from '../../js/engine.js';

export const Body = new Color(0.035, 0.028, 0.050);
export const Slate = new Color(0.26, 0.21, 0.38);
export const Rim = new Color(0.52, 0.36, 0.86);
export const Sheen = new Color(0.72, 0.62, 0.95);
export const Lift = 0.60;       // SixPathsHeight.Lift
export const Segments = 48;
const Bins = 6;
const TAU = Math.PI * 2;

const solid = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const props = new MaterialPropertyBlock();
const scratch = new Map();
export const mesh = (key) => scratch.get(key) ?? scratch.set(key, new Mesh(key)).get(key);

export function draw(m, x, y, z, sx, sz, rot, colour, material = solid) {
  if (colour.a <= 0.001) return;
  props.SetColor(ShaderPropertyIDs.Color, colour);
  Graphics.DrawMesh(m, Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0, rot, 0), new Vector3(sx, 1, sz)),
    material, 0, null, 0, props);
}

// Plain 3D vectors. y is height above the floor, not altitude.
export const v3 = (x, y, z) => ({ x, y, z });
export const add = (a, b) => v3(a.x + b.x, a.y + b.y, a.z + b.z);
export const mul = (a, f) => v3(a.x * f, a.y * f, a.z * f);
export const dot = (a, b) => a.x * b.x + a.y * b.y + a.z * b.z;
export const cross = (a, b) => v3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
export const unit = (a) => mul(a, 1 / (Math.hypot(a.x, a.y, a.z) || 1));
export const Up = v3(0, 1, 0);
// Points along this direction land on the same pixel, so a surface is visible when its normal has
// a positive component along it: facing up, or facing south.
export const View = unit(v3(0, 1, -Lift));
// A weak second light from low on the camera side. The lab sun sits behind most things you look
// at, which leaves every visible face in shade and every surface the same black.
export const FillFrom = unit(v3(-0.35, 0.5, -0.8));
export const screenX = (P) => P.x;
export const screenZ = (P) => P.z + P.y * Lift;

/** Direction toward the sun for a scene shadow vector. */
export const sunLight = (sun) => unit(v3(-sun.x, 1, -sun.z));

/** Colour of a surface with outward normal n. look: { shade, fill, ambient }. */
export function tone(n, light, look, alpha, boost = 1) {
  const lit = (Math.max(0, dot(n, light)) * look.shade + Math.max(0, dot(n, FillFrom)) * look.fill) * boost;
  return Color.Lerp(Body, Slate, look.ambient + lit).withAlpha(alpha);
}

/** Half-width factor at outline angle a: 1 everywhere at taper 0, closing on +u at taper 1. */
const narrow = (body, c) => 1 - (body.taper ?? 0) * 0.5 * (1 + c);

/**
 * Point k of a body's outline in its own (u, v), inset toward the middle, clamped to the body's
 * cut: vMin, uMin and uMax are where the floor slices it.
 */
export function outline(body, k, inset = 0) {
  const a = (k / Segments) * TAU, c = Math.cos(a), s = Math.sin(a), e = body.corner;
  const r = 1 / Math.pow(Math.abs(c) ** e + Math.abs(s) ** e, 1 / e);
  const u = Math.max(0, body.hx - inset) * r * c;
  const v = body.vc + Math.max(0, body.hy - inset) * r * s * narrow(body, c);
  return {
    u: Math.min(body.uMax ?? Infinity, Math.max(body.uMin ?? -Infinity, u)),
    v: Math.max(body.vMin ?? -Infinity, v),
    // A tapered needle thins in both directions, not only across its width.
    half: (body.th / 2) * (body.taperThickness ? Math.max(0.12, narrow(body, c)) : 1),
  };
}

/** A body-space point in the world. */
export const at = (body, u, v, w) => add(add(add(body.C, mul(body.T, u)), mul(body.V, v)), mul(body.W, w));

/** The middle of the body's visible part, for fans and draw order. */
export function hub(body, w = 0) {
  const u = Math.min(body.uMax ?? Infinity, Math.max(body.uMin ?? -Infinity, 0));
  return at(body, u, Math.max(body.vc, body.vMin ?? -Infinity), w);
}

/**
 * Draws the body at altitude alt..alt+0.004. look: { shade, fill, ambient, bevel, bevelGlow,
 * flash }. Returns the visible face's side (+1 or -1 along W) so callers can put marks on it.
 */
export function drawSolid(key, body, alt, light, look) {
  const alpha = body.alpha ?? 1;
  const facing = dot(body.W, View) >= 0 ? 1 : -1;
  const pts = [];
  for (let k = 0; k < Segments; k++) pts.push(outline(body, k));

  // Edge strips turned toward the camera, grouped by how much sun they catch.
  const bins = Array.from({ length: Bins }, () => ({ xz: [], tri: [], normal: v3(0, 0, 0), n: 0 }));
  for (let k = 0; k < Segments; k++) {
    const a = pts[k], b = pts[(k + 1) % Segments];
    const du = b.u - a.u, dv = b.v - a.v, len = Math.hypot(du, dv);
    if (len < 1e-6) continue;
    const n = add(mul(body.T, dv / len), mul(body.V, -du / len));
    if (dot(n, View) <= 1e-4) continue;
    const bin = bins[Math.min(Bins - 1, Math.floor(Math.max(0, dot(n, light)) * Bins))];
    const base = bin.xz.length / 2;
    for (const [q, side] of [[a, 1], [b, 1], [b, -1], [a, -1]]) {
      const P3 = at(body, q.u, q.v, side * q.half);
      bin.xz.push(screenX(P3), screenZ(P3));
    }
    bin.tri.push(base, base + 1, base + 2, base, base + 2, base + 3);
    bin.normal = add(bin.normal, n); bin.n++;
  }
  bins.forEach((bin, b) => {
    if (!bin.n) return;
    const m = mesh(`${key}-edge-${b}`);
    m.setFlat(bin.xz, bin.tri);
    // The edge catches more light than the face: it is a narrow bevel, not a wall.
    draw(m, 0, alt, 0, 1, 1, 0, tone(unit(bin.normal), light, look, alpha, 1.25));
  });

  // The visible face, as a fan.
  const face = mesh(`${key}-face`);
  const h = hub(body, (facing * body.th) / 2 * (body.taperThickness ? Math.max(0.12, narrow(body, 0)) : 1));
  const xz = [screenX(h), screenZ(h)], tri = [];
  for (let k = 0; k < Segments; k++) {
    const P3 = at(body, pts[k].u, pts[k].v, facing * pts[k].half);
    xz.push(screenX(P3), screenZ(P3));
    tri.push(0, k + 1, ((k + 1) % Segments) + 1);
  }
  face.setFlat(xz, tri);
  // The broad face takes less light than the edges, so a body lying flat stays near black like
  // an orb, and the lit edges are what give the thickness away.
  draw(face, 0, alt + 0.002, 0, 1, 1, 0, tone(mul(body.W, facing), light, look, alpha, 0.55));

  // A lit line just inside the face's edge, the solid's version of the orb rim.
  const flash = look.flash ?? 0;
  if (look.bevel > 0 && look.bevelGlow > 0) {
    const band = mesh(`${key}-bevel`);
    const bxz = [], btri = [];
    for (let k = 0; k <= Segments; k++) {
      const o = pts[k % Segments], n = outline(body, k % Segments, look.bevel);
      const Po = at(body, o.u, o.v, facing * o.half), Pn = at(body, n.u, n.v, facing * n.half);
      bxz.push(screenX(Po), screenZ(Po), screenX(Pn), screenZ(Pn));
      if (k < Segments) { const v = k * 2; btri.push(v, v + 2, v + 1, v + 1, v + 2, v + 3); }
    }
    band.setFlat(bxz, btri);
    const edge = Color.Lerp(Rim, new Color(0.92, 0.86, 1), flash * 0.8);
    draw(band, 0, alt + 0.004, 0, 1, 1, 0, edge.withAlpha(Math.min(1, look.bevelGlow * (1 + flash)) * alpha));
  }
  return facing;
}

/** The body's outline thrown along the sun onto the floor. */
export function drawSolidShadow(key, body, sun, strength) {
  if (strength <= 0) return;
  const m = mesh(`${key}-shadow`);
  const cast = (P3) => [P3.x + sun.x * Math.max(0, P3.y), P3.z + sun.z * Math.max(0, P3.y)];
  const xz = [...cast(hub(body))], tri = [];
  for (let k = 0; k < Segments; k++) {
    const o = outline(body, k);
    xz.push(...cast(at(body, o.u, o.v, 0)));
    tri.push(0, k + 1, ((k + 1) % Segments) + 1);
  }
  m.setFlat(xz, tri);
  draw(m, 0, AltitudeLayer.Shadows.AltitudeFor(), 0, 1, 1, 0, new Color(0, 0, 0, strength * (body.alpha ?? 1)));
}
