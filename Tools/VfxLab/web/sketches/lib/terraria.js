// Terraria weapons: pieces shared by the Last Prism and the Rainbow Crystal Staff sketches. Not a
// sketch itself, so it is not listed in sketches/index.js.
import { Color, MaterialPool, ShaderDatabase } from '../../js/engine.js';
import { Body, Y, sprite } from './six-paths-impact.js';

const flat = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
export const Hurt = new Color(.95, .42, .15);

// A rainbow colour: h 0..1 runs red, yellow, green, cyan, blue, magenta and back; sat 0..1.
export function hue(h, sat = .72) {
  h = ((h % 1) + 1) % 1;
  const k = n => { const q = (n + h * 6) % 6; return 1 - sat * Math.max(0, Math.min(q, 4 - q, 1)); };
  return new Color(k(5), k(3), k(1));
}

// Cells from a along ang to the first wall cell (cells: { x, z } centres), or max.
export function rayWall(a, ang, cells, max) {
  const d = [Math.cos(ang), Math.sin(ang)], o = [a.x, a.z];
  let best = max;
  for (const c of cells) {
    const lo = [c.x - .5, c.z - .5], hi = [c.x + .5, c.z + .5];
    let t0 = -Infinity, t1 = Infinity;
    for (let k = 0; k < 2; k++) {
      if (Math.abs(d[k]) < 1e-9) { if (o[k] < lo[k] || o[k] > hi[k]) t0 = Infinity; continue; }
      let ta = (lo[k] - o[k]) / d[k], tb = (hi[k] - o[k]) / d[k];
      if (ta > tb) [ta, tb] = [tb, ta];
      t0 = Math.max(t0, ta); t1 = Math.min(t1, tb);
    }
    if (t0 <= t1 && t0 > 0 && t0 < best) best = t0;
  }
  return best;
}

// Damage taken against what downs the pawn: a sketch aid over the head, shown once hit.
export function damageBar(head, share) {
  if (share <= 0) return;
  const w = .56, h = .065, z = head.z + .2, fill = Math.min(1, share);
  sprite({ x: head.x, z }, w + .04, h + .04, Body.withAlpha(.75), flat, Y + .29);
  sprite({ x: head.x - w / 2 + w * fill / 2, z }, w * fill, h, Hurt, flat, Y + .291);
}
