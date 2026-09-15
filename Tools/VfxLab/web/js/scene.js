// The ground effects are judged against. It is not RimWorld's art -- that is inside the game's
// asset bundles -- but it is built to RimWorld's measurements: 120 x 120 cells, trees drawn at
// the Building altitude with shadows cast along one sun direction, a pawn one cell tall, and
// soil and grass in the values of a temperate map, so contrast judged here carries over.
//
// A screenshot from the game can replace all of it (Scene panel): the lab then draws effects
// over the real thing, at the px-per-cell you calibrate.

import { AltitudeLayer, MeshPool } from './engine.js';
import { fbm, hash, canvas } from './standins.js';

export const MapSize = 120;
const PxPerCell = 16;

function terrainCanvas() {
  const n = MapSize * PxPerCell, c = canvas(n), ctx = c.getContext('2d');
  const img = ctx.createImageData(n, n), d = img.data;
  const soil = [118, 88, 60], soilDark = [92, 68, 46], grass = [112, 124, 62], grassDark = [80, 94, 48];
  for (let y = 0; y < n; y++)
    for (let x = 0; x < n; x++) {
      const u = x / PxPerCell, v = y / PxPerCell;
      const patch = fbm(u / 9, v / 9, 5, 4, 16);
      const grain = fbm(u * 1.7, v * 1.7, 9, 3, 256);
      const speck = hash(x, y, 3);
      const grassy = Math.min(1, Math.max(0, (patch - 0.5) * 5));
      const base = soil.map((s, i) => s + (soilDark[i] - s) * grain);
      const green = grass.map((g, i) => g + (grassDark[i] - g) * grain);
      const i = (y * n + x) * 4;
      for (let k = 0; k < 3; k++) {
        let value = base[k] + (green[k] - base[k]) * grassy * (0.65 + 0.35 * speck);
        if (speck > 0.985) value *= 0.72;
        d[i + k] = value;
      }
      d[i + 3] = 255;
    }
  ctx.putImageData(img, 0, 0);
  return c;
}

function treeCanvas(seed) {
  const s = 128, c = canvas(s), ctx = c.getContext('2d');
  ctx.fillStyle = '#5a3d22';
  ctx.fillRect(s * 0.46, s * 0.55, s * 0.08, s * 0.36);
  const greens = ['#6f8a36', '#809b3f', '#5f7a2e', '#91a84a'];
  for (let i = 0; i < 9; i++) {
    const a = hash(i, seed, 1) * Math.PI * 2, r = hash(i, seed, 2) * s * 0.16;
    ctx.fillStyle = greens[i % greens.length];
    ctx.beginPath();
    ctx.ellipse(s * 0.5 + Math.cos(a) * r, s * 0.38 + Math.sin(a) * r * 0.8, s * (0.16 + hash(i, seed, 4) * 0.08), s * (0.13 + hash(i, seed, 5) * 0.06), 0, 0, Math.PI * 2);
    ctx.fill();
  }
  return c;
}

function rockCanvas() {
  const s = 64, c = canvas(s), ctx = c.getContext('2d');
  ctx.fillStyle = '#6d6a66';
  ctx.beginPath();
  [[0.2, 0.6], [0.35, 0.3], [0.62, 0.22], [0.82, 0.45], [0.75, 0.75], [0.4, 0.8]].forEach(([x, y], i) =>
    i ? ctx.lineTo(x * s, y * s) : ctx.moveTo(x * s, y * s));
  ctx.closePath(); ctx.fill();
  ctx.fillStyle = '#8b8781';
  ctx.beginPath(); ctx.ellipse(s * 0.5, s * 0.4, s * 0.16, s * 0.1, -0.4, 0, Math.PI * 2); ctx.fill();
  return c;
}

function pawnCanvas() {
  const s = 64, c = canvas(s), ctx = c.getContext('2d');
  ctx.fillStyle = '#8fc0d8';
  ctx.beginPath(); ctx.ellipse(s * 0.5, s * 0.62, s * 0.22, s * 0.28, 0, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = '#e2b48e';
  ctx.beginPath(); ctx.arc(s * 0.5, s * 0.3, s * 0.17, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = '#3b2c22';
  ctx.beginPath(); ctx.arc(s * 0.5, s * 0.24, s * 0.16, Math.PI, 0); ctx.fill();
  return c;
}

function shadowCanvas() {
  const s = 64, c = canvas(s), ctx = c.getContext('2d');
  const g = ctx.createRadialGradient(s / 2, s / 2, 0, s / 2, s / 2, s / 2);
  g.addColorStop(0, 'rgba(0,0,0,1)'); g.addColorStop(0.6, 'rgba(0,0,0,0.75)'); g.addColorStop(1, 'rgba(0,0,0,0)');
  ctx.fillStyle = g; ctx.fillRect(0, 0, s, s);
  return c;
}

const mat = (tex, shader = 'Transparent') => ({ tex, shader, textures: {}, floats: {} });
const quad = (group, tex, x, y, z, sx, sz, rot = 0, a = 1, shader) =>
  ({ group, mesh: MeshPool.plane10, mat: mat(tex, shader), x, y, z, rot, sx, sz, r: 1, g: 1, b: 1, a, age: 0 });

export class Scene {
  constructor(renderer) {
    this.renderer = renderer;
    this.sun = { angle: 235, length: 0.55, strength: 0.32 };
    this.show = { terrain: true, props: true, grid: false };
    this.backdrop = null; // { path, width, height, ppc }
    renderer.setCanvasTexture('canvas:terrain', terrainCanvas());
    for (let i = 0; i < 4; i++) renderer.setCanvasTexture(`canvas:tree${i}`, treeCanvas(i));
    renderer.setCanvasTexture('canvas:rock', rockCanvas());
    renderer.setCanvasTexture('canvas:pawn', pawnCanvas());
    renderer.setCanvasTexture('canvas:shadow', shadowCanvas());
    this.props = this.placeProps();
  }

  placeProps() {
    const props = [];
    const centre = MapSize / 2;
    for (let i = 0; i < 520; i++) {
      const x = Math.floor(hash(i, 1, 77) * MapSize) + 0.5, z = Math.floor(hash(i, 2, 77) * MapSize) + 0.5;
      // A clearing around the cell recordings are taken on, so the effect is not judged through leaves.
      if (Math.hypot(x - centre, z - centre) < 4) continue;
      props.push({ kind: hash(i, 3, 77) < 0.72 ? 'tree' : 'rock', x, z, variant: i % 4, size: 1.1 + hash(i, 4, 77) * 0.7 });
    }
    props.push({ kind: 'pawn', x: centre + 3.5, z: centre - 3.5, size: 1 });
    return props;
  }

  /** Map-space direction shadows fall in, one cell of shadow per cell of height times length. */
  get shadowVector() {
    const a = this.sun.angle * Math.PI / 180;
    return { x: Math.sin(a) * this.sun.length, z: Math.cos(a) * this.sun.length };
  }

  calls(viewCentre, viewCells) {
    const out = [];
    const terrainY = AltitudeLayer.Terrain.AltitudeFor();
    if (this.backdrop) {
      const b = this.backdrop;
      out.push(quad('scene · backdrop', `canvas:backdrop`, b.cx, terrainY, b.cz, b.width / b.ppc, b.height / b.ppc));
      return out;
    }
    if (this.show.terrain)
      out.push(quad('scene · terrain', 'canvas:terrain', MapSize / 2, terrainY, MapSize / 2, MapSize, MapSize));
    if (!this.show.props) return out;
    const sv = this.shadowVector, reach = viewCells + 4;
    for (const p of this.props) {
      if (Math.abs(p.x - viewCentre.x) > reach || Math.abs(p.z - viewCentre.z) > reach) continue;
      const height = p.kind === 'tree' ? 2.4 * p.size : p.kind === 'pawn' ? 1.2 : 0.4;
      const len = Math.hypot(sv.x, sv.z) * height;
      const rot = Math.atan2(sv.x, sv.z) * 180 / Math.PI + 180;
      out.push({
        ...quad('scene · shadows', 'canvas:shadow', p.x + sv.x * height * 0.5, AltitudeLayer.Shadows.AltitudeFor(), p.z + sv.z * height * 0.5,
          p.size * 0.55, Math.max(p.size * 0.5, len), rot, this.sun.strength),
      });
      if (p.kind === 'tree')
        out.push(quad('scene · trees', `canvas:tree${p.variant}`, p.x, AltitudeLayer.Building.AltitudeFor(), p.z + p.size * 0.35, p.size * 1.5, p.size * 1.5));
      else if (p.kind === 'rock')
        out.push(quad('scene · rocks', 'canvas:rock', p.x, AltitudeLayer.Item.AltitudeFor(), p.z, 0.8, 0.8));
      else
        out.push(quad('scene · pawn (1 cell)', 'canvas:pawn', p.x, AltitudeLayer.Pawn.AltitudeFor(), p.z + 0.15, 1, 1));
    }
    return out;
  }

  setBackdrop(image, ppc, centre) {
    this.renderer.setCanvasTexture('canvas:backdrop', image);
    this.backdrop = { width: image.width, height: image.height, ppc, cx: centre.x, cz: centre.z };
  }
}
