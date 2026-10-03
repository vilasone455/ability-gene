// Judgement Cut End: what its sketches (vergil-judgement-cut-end.js and -v2.js) share. Not a sketch
// itself, so it is not listed in sketches/index.js.
//
// The showcase (who stands where, who is marked, how the marked ones take the hits) and the panes of
// glass the ring breaks into along the cuts. Every function takes times and keeps no state.
import { CustomShader, Material, Mathf } from '../../js/engine.js';
import { mesh } from './six-paths-solid.js';
import { Y, rand } from './six-paths-impact.js';
import { Deep, Ice, White, EnemyColour, Ally, pawn, wallCell, shatter, streak, whiteGlow, smooth, clamp } from './vergil.js';

// Raiders as [direction from the caster (degrees), distance when the caster vanishes (cells)].
export const Raiders = [[25, 3.2], [80, 5.6], [-35, 6.4], [140, 4.3], [-110, 7.4], [172, 8.4]];
export const OutsideAt = [58, 2.5], AllyAt = [-72, 2.6], WallAt = [212, 4.6];   // outside: degrees, cells past the radius
export const Hits = 4, HitGap = .08, WalkIn = 1.1;

/**
 * RimArt/CutGlass, a shader of the mod's own (engine.js CustomShader): a pane of cut space. It shows
 * the picture behind it moved off its place, with red and blue pulled apart along the move, and
 * drained of colour toward a pale tint. Per call: _Color rgb is the tint and a how much of all of it
 * is on (0 leaves the picture as it is); _Shift xy is the move in cells and z the colour split in cells.
 * On the material: _Grey, how much colour is drained at full strength, and _Pale, how far the grey is
 * pulled to the tint. Not in the game yet: it needs the ShaderLab version and an asset bundle per OS.
 */
export const CutGlass = CustomShader('RimArt/CutGlass', `#version 300 es
precision mediump float;
uniform sampler2D u_scene;
uniform vec4 u_color, _Shift;
uniform vec2 u_screen, u_cellUv;
uniform float _Grey, _Pale;
out vec4 o;
void main() {
  vec2 uv = gl_FragCoord.xy / u_screen, move = _Shift.xy * u_cellUv * u_color.a;
  vec2 split = normalize(_Shift.xy + vec2(1e-5)) * _Shift.z * u_cellUv * u_color.a;
  vec3 c = vec3(texture(u_scene, uv + move + split).r, texture(u_scene, uv + move).g, texture(u_scene, uv + move - split).b);
  float light = dot(c, vec3(0.299, 0.587, 0.114));
  o = vec4(mix(c, mix(vec3(light), u_color.rgb, _Pale), _Grey * u_color.a), 1.0);
}`);
export const cutGlass = new Material(CutGlass);
cutGlass.SetFloat('_Grey', .92); cutGlass.SetFloat('_Pale', .3);

export const polar = (deg, d) => ({ x: Math.cos(deg * Mathf.Deg2Rad) * d, z: Math.sin(deg * Mathf.Deg2Rad) * d });
/** Where the marked raiders stand when the caster vanishes, relative to the caster. */
export const markedIn = radius => Raiders.filter(([, d]) => d <= radius).map(([deg, d]) => polar(deg, d));
/** When marked raider i takes its first hit. */
export const firstHit = (click, i) => click + .04 + i * .025;

/** The three wall cells one raider stands behind. */
export function wall(o) {
  for (let k = -1; k <= 1; k++) {
    const c = polar(WallAt[0], WallAt[1]), r = WallAt[0] * Mathf.Deg2Rad;
    wallCell({ x: o.x + c.x - Math.sin(r) * k, z: o.z + c.z + Math.cos(r) * k }, WallAt[0]);
  }
}

/**
 * The pawns, north first: the marked raiders walk in, stop when the caster vanishes (tinted by `dark`
 * 0..1), shake under their hits after the click and go down; one raider arrives from outside the
 * radius, an ally walks round inside it and, with the wall on, one raider paces behind it.
 */
export function pawns(s, { vanish, click }, radius, o, sun, strength, dark, wallOn) {
  const world = q => ({ x: o.x + q.x, z: o.z + q.z }), stopped = s >= vanish && s < click, figures = [];
  Raiders.filter(([, far]) => far <= radius).forEach(([deg, d], i) =>
    figures.push({ pos: world(polar(deg, d + Math.max(0, vanish - s) * WalkIn)), marked: true, i }));
  figures.push({ pos: world(polar(OutsideAt[0], Math.max(1.6, radius + OutsideAt[1] + (vanish - s) * WalkIn))), i: 20 });
  figures.push({ pos: world(polar(AllyAt[0] + s * 4, AllyAt[1])), ally: true, i: 21 });
  if (wallOn) figures.push({ pos: world(polar(WallAt[0] + Math.sin(s * .9) * 6, WallAt[1] + 1.1)), i: 22 });
  figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
    if (!g.marked) { pawn(g.pos, g.ally ? Ally : EnemyColour, sun, strength, { tint: Deep, tintAmount: .25 * dark }); return; }
    const first = firstHit(click, g.i), last = first + (Hits - 1) * HitGap, down = s >= last + .1;
    const shake = s >= first && !down ? Math.sin(s * 90 + g.i) * .04 : 0;
    pawn({ x: g.pos.x + shake, z: g.pos.z }, EnemyColour, sun, strength, { lie: down, tint: stopped ? Deep : White, tintAmount: stopped ? .5 * dark : .7 * clamp(1 - (s - last) / .2) * (s >= first ? 1 : 0) });
  });
}

/** The warm-up's short lights rising out of the floor inside the ring as it grows; w is the warm-up 0..1. */
export function risingLights(key, o, radius, w, count = 40) {
  for (let i = 0; i < count; i++) {
    const v = (w * 2.2 + rand(i)) % 1, at = polar(rand(i + 60) * 360, Math.sqrt(rand(i + 120)) * radius * smooth(w * 1.25));
    streak(`${key} mote ${i}`, { x: o.x + at.x, z: o.z + at.z + v * .7 }, { x: o.x + at.x, z: o.z + at.z + v * .7 + .3 }, .05, Ice.withAlpha(.8 * w * Math.sin(v * Math.PI)), whiteGlow, Y + .01, 3);
  }
}

/**
 * The pieces between the cuts as panes of glass. cuts are [{ q, d }] relative to the caster, light is
 * the unit direction the sun shines from. Each piece gets a face mesh `<key> piece <i>` and two edge
 * meshes, `... lit` (the edges that face the light) and `... dim` (the ones that face away), each an
 * edgeWidth strip just inside the outline; an edge less than edgeFacing square to the light has neither.
 * Call it only when the radius, the cuts or the sun change: it rebuilds every mesh.
 */
export function panes(key, radius, cuts, light, edgeWidth, edgeFacing) {
  const list = shatter(radius, cuts);
  list.forEach((piece, i) => {
    const vertices = [0, 0], tri = [], n = piece.points.length, edges = { lit: [[], []], dim: [[], []] };
    piece.points.forEach(v => vertices.push(v.x, v.z));
    for (let j = 0; j < n; j++) {
      tri.push(0, 1 + j, 1 + (j + 1) % n);
      const v = piece.points[j], w = piece.points[(j + 1) % n], dx = w.x - v.x, dz = w.z - v.z, len = Math.hypot(dx, dz);
      if (len < .05) continue;
      const nx = dz / len, nz = -dx / len, facing = nx * light.x + nz * light.z;   // outward normal of a counter-clockwise outline
      if (Math.abs(facing) < edgeFacing) continue;
      const [verts, tris] = facing > 0 ? edges.lit : edges.dim, at = verts.length / 2;
      verts.push(v.x, v.z, w.x, w.z, w.x - nx * edgeWidth, w.z - nz * edgeWidth, v.x - nx * edgeWidth, v.z - nz * edgeWidth);
      tris.push(at, at + 1, at + 2, at, at + 2, at + 3);
    }
    mesh(`${key} piece ${i}`).setFlat(vertices, tri);
    piece.lit = edges.lit[0].length > 0; piece.dim = edges.dim[0].length > 0;
    if (piece.lit) mesh(`${key} piece ${i} lit`).setFlat(...edges.lit);
    if (piece.dim) mesh(`${key} piece ${i} dim`).setFlat(...edges.dim);
  });
  return list;
}
