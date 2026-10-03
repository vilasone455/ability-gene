// Terraria weapons: pieces shared by the Last Prism, Rainbow Crystal Staff and Stardust Dragon Staff sketches.
// Not a sketch itself, so it is not listed in sketches/index.js.
import { Color, Mathf, MaterialPool, ShaderDatabase } from '../../js/engine.js';
import { Body, Y, sprite, soft, rand } from './six-paths-impact.js';

const flat = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
export const Hurt = new Color(.95, .42, .15);
const Smoke = new Color(.32, .32, .34);
// Burn or cut that puts an unarmoured pawn into pain shock (vanilla: pain shock at 80 %, a burn adds 0.01875 pain per
// point; the Last Prism's measure).
export const PainShock = 43;

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

// Where a stand-in on a walking path [[time, x, z], ...] is at time s: still before the first point, in straight
// lines between points, still after the last.
export function walk(path, s) {
  if (s <= path[0][0]) return { x: path[0][1], z: path[0][2] };
  for (let i = 1; i < path.length; i++) {
    const [t0, x0, z0] = path[i - 1], [t1, x1, z1] = path[i];
    if (s < t1) { const u = (s - t0) / (t1 - t0); return { x: Mathf.Lerp(x0, x1, u), z: Mathf.Lerp(z0, z1, u) }; }
  }
  const l = path[path.length - 1];
  return { x: l[1], z: l[2] };
}

// Five puffs of smoke rising off a stand-in at pos, since seconds after it went down (a sketch aid for "down").
export function downSmoke(pos, since) {
  for (let q = 0; q < 5; q++) {
    const v = (since - q * .25) / 1.2;
    if (v < 0 || v > 1) continue;
    sprite({ x: pos.x + (rand(q + 70) - .5) * .4 + v * .2, z: pos.z + .1 + v * .8 }, .35 + v * .5, .3 + v * .4, Smoke.withAlpha(.35 * Math.sin(v * Math.PI)), soft, Y + .004);
  }
}
