// Black rods v2 — palisade proposal, not the game. Nothing in Source/RimArt draws this yet.
// The first sketch (six-paths-rods.js) raised six rods in a ring with 2-cell gaps: it neither hit
// the target nor held it. v2 keeps its ground work (holes, dirt, attached shadows) as a wall.
//
// What it is for (proposed, none of it agreed). Target a cell within 12 cells. Six orbs fly out
// and sink along a line about 6 cells long, across the cast direction. A crack runs along the
// line, then rods stab up from the middle outward. Each rod cell is impassable for 10 s (the
// sketch shows a shorter stand); bullets pass, and each rod has its own HP. A pawn standing on
// the line when it rises takes about 15 Sharp and is pushed to the nearest free cell. Then the
// rods sink from the ends inward and the orbs return. Six orbs, about 45 s cooldown. It pairs
// with Shield wall: that stops bullets and lets pawns through, this stops pawns and lets bullets
// through.
//
// The orbs and the sage: the caster carries the six orbs on its ring (lib/six-paths-sage.js). All
// six leave their slots at 0.09 radius, grow to the field cast radius 0.30 over the flight and
// sink into the line, so the ring is empty while the wall stands. On the way back each shrinks to
// 0.09 and sits in its slot. The sage faces the nearest of the four directions to the cast.
//
// Drawing: a wall running north-south puts height and the line on the same screen axis, so its
// rods cross alternately east and west like a spiked barricade; an east-west wall stands upright.
// The amount of crossing follows the cast direction, so it changes gradually between the two.
// Caster and attacker are stand-ins.
import { AltitudeLayer, Color, Mathf, Meshes } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Body, Rim, Y, Floor, Lift, orb, sprite, trail, band, glow, soft, rand } from './lib/six-paths-impact.js';
import { sage, carried, slot, deployRadius, Field } from './lib/six-paths-sage.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const disc = Meshes.disc(32, 'rods v2 disc');
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor();
const pale = new Color(.88, .79, 1), Dirt = new Color(.34, .26, .19), Face = new Color(.085, .062, .12);
// Decided looks, kept out of the panel.
const TipWidth = .07, Punch = .12, Overshoot = .12, Cross = .75, Lean = .1, Crack = .25, Orbs = 6;

function times(p) {
  const crack = p.sink, rise = crack + Crack, stand = rise + p.stagger + Punch;
  const retract = stand + p.stand, gone = retract + p.retract + .25;
  return { crack, rise, stand, retract, gone, end: gone + .6 + .3 };
}

export default {
  kit: 'Six Paths', label: 'Black rods v2 (sketch)',
  params: {
    scenario: { label: 'Attacker', value: 'on the line', options: ['on the line', 'walks up'], group: 'Showcase' },
    actors: { label: 'Show caster and attacker', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Caster distance (cells)', 5, 3, 12, .5, 'Showcase'),
    length: P('Wall length (cells)', 6, 3, 9, .5, 'Wall'),
    count: P('Rods', 12, 6, 18, 1, 'Wall'),
    tall: P('Rod height (cells)', 2.2, 1, 3.5, .05, 'Wall'),
    width: P('Rod base width (cells)', .3, .15, .45, .01, 'Wall'),
    sink: P('Orbs fly out and sink', .5, .25, 1, .05, 'Timing (s)'),
    stagger: P('Rise spreads from the middle over', .3, 0, 1, .05, 'Timing (s)'),
    stand: P('Wall stands shown (real: 10 s)', 4, 1, 10, .5, 'Timing (s)'),
    retract: P('Draw back down', .55, .2, 1.5, .05, 'Timing (s)'),
    shake: P('Camera shake', .07, 0, .2, .01, 'Impact'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Orbs sink', t: 0 }, { name: 'Crack', t: t.crack }, { name: 'Rods up', t: t.rise },
    { name: 'Wall stands', t: t.stand }, { name: 'Back down', t: t.retract }, { name: 'Orbs return', t: t.gone },
  ]; },
  events(p) { return p.shake ? [{ t: times(p).rise, type: 'shake', value: p.shake }] : []; },
  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, ax = Math.cos(a), az = Math.sin(a);
    // Floor frame: along the cast direction, across it (the wall runs across).
    const floor = (along, across) => ({ x: o.x + ax * along - az * across, z: o.z + az * along + ax * across });
    const caster = floor(-p.distance, 0), mid = (p.count - 1) / 2;
    const acrossOf = (i) => (p.count > 1 ? i / (p.count - 1) - .5 : 0) * p.length;
    const spread = (i) => Math.abs(i - mid) / Math.max(1, mid);             // 0 in the middle, 1 at the ends
    const startOf = (i) => t.rise + spread(i) * p.stagger;
    const heightOf = (i) => {
      const up = clamp((s - startOf(i)) / Punch), back = clamp((s - t.retract - (1 - spread(i)) * .25) / p.retract);
      if (up <= 0 || back >= 1) return 0;
      return p.tall * (1 - .12 * rand(i + 2)) * smooth(up) * (1 + Overshoot * Math.sin(up * Math.PI)) * (1 - smooth(back));
    };

    // The crack telegraphs the line, glows while the wall stands, and closes after it.
    const reach = smooth((s - t.crack) / Crack) * p.length / 2 + .001, closing = 1 - smooth((s - t.gone + .25) / .5);
    if (s >= t.crack && closing > 0) {
      const pts = [];
      for (let k = -24; k <= 24; k++) {
        const across = k / 24 * reach;
        pts.push(floor((k % 2 ? .05 : -.05) * (Math.abs(k) < 24), across));
      }
      const pulse = .5 + .5 * Math.sin(s * 4), standing = s >= t.stand && s < t.retract;
      trail('rods v2 crack dark', pts, .16, Body.withAlpha(.8 * closing), Floor + .008);
      trail('rods v2 crack edge', pts, .05, Rim.withAlpha((standing ? .4 + .3 * pulse : .7) * closing), Floor + .009);
    }

    // All six orbs leave the ring, drop into the line, and come back out to their slots at the end.
    carried(caster, s, scene, () => true);
    for (let k = 0; k < Orbs; k++) {
      const socket = floor(0, ((k + .5) / Orbs - .5) * p.length), home = slot(caster, k, s);
      const path = (u) => ({ x: lerp(home.x, socket.x, u),
        z: lerp(home.z, socket.z, u) + Math.sin(u * Math.PI) * (.7 + k * .08) * Lift });
      const out = s < p.sink, back = s >= t.gone;
      if (!out && !back) continue;
      const fly = p.sink * .75, u = out ? smooth(s / fly) : 1 - smooth((s - t.gone) / .6);
      const drop = out ? smooth((s - fly) / (p.sink - fly)) : 0;
      // Coming back, the orb rises out of the floor before it sets off.
      const emerge = out ? 1 : Math.min(1, (1 - u) * 4);
      orb(path(u), deployRadius(u, Field) * (1 - drop) * emerge, 1, 1 - drop * .5, u > 0 ? Y : home.layer);
      const from = out ? s : s - t.gone, span = out ? fly : .6;
      trail('rods v2 orb trail ' + k, Array.from({ length: 12 }, (_, j) => {
        const v = smooth(Math.max(0, from - (1 - j / 11) * .12) / span); return path(out ? v : 1 - v);
      }), .07, Rim.withAlpha(.45 * (1 - drop)));
    }

    // Attacker: either standing on the line when it rises, or walking up to it afterwards.
    let pawn = null, hitAge = -1;
    if (p.actors) {
      const onLine = p.scenario === 'on the line', across = .78;
      const nearest = Math.round((across / p.length + .5) * (p.count - 1));
      hitAge = onLine ? s - startOf(nearest) - Punch * .4 : -1;
      let along, hop = 0;
      if (onLine) { const u = clamp(hitAge / .3); along = smooth(u) * 1.15; hop = Math.sin(u * Math.PI) * .3; }
      else {
        const arrive = t.stand + .6, w = clamp(s / arrive), since = s - arrive;
        along = lerp(3.6, .8, w) + (since > 0 && s < t.retract ? Math.max(0, Math.sin(since * 5.2)) ** 6 * .14 : 0);
        hop = w < 1 ? Math.abs(Math.sin(s * 9)) * .05 : 0;
      }
      pawn = floor(along, across);
      sprite({ x: pawn.x + sun.x * .45, z: pawn.z + sun.z * .45 }, .85, .4, Body.withAlpha(strength), soft, shadowLayer);
      sage(caster, Math.round(p.aim / 90) % 4 * 90, scene);
      pawn.hop = hop * Lift;
    }
    const drawPawn = () => {
      draw(disc, pawn.x, pawn.layer, pawn.z + .18 + pawn.hop, .22, .32, 0, new Color(.55, .38, .27));
      draw(disc, pawn.x, pawn.layer + .002, pawn.z + .58 + pawn.hop, .16, .17, 0, new Color(.83, .70, .54));
    };
    // Things further north draw first, so nearer rods and the pawn overlap what is behind them.
    const layerAt = (z) => Y + .06 - (z - o.z) * .006;
    if (pawn) pawn.layer = layerAt(pawn.z) + .0005;

    const order = Array.from({ length: p.count }, (_, i) => i);
    for (const i of order) {
      const b = floor(0, acrossOf(i)), h = heightOf(i), age = s - startOf(i);
      const holed = smooth(age / .1) * closing;
      if (holed > 0) {
        sprite(b, p.width * 2.6, p.width * 1.4, Dirt.withAlpha(.55 * holed), soft, shadowLayer + .003);
        draw(disc, b.x, shadowLayer + .005, b.z, p.width * .75, p.width * .4, 0, new Color(.018, .014, .024, holed));
      }
      // Dirt thrown as the rod breaks the surface: three chunks and one puff each.
      if (age >= 0 && age < .55) {
        const u = age / .55;
        sprite({ x: b.x, z: b.z + u * .25 }, .35 + u * .6, .25 + u * .4, new Color(.56, .49, .40, Math.sin(u * Math.PI) * .35), soft, shadowLayer + .02);
        for (let c = 0; c < 3; c++) {
          const dir = rand(i * 7 + c) * Math.PI * 2, far = (.35 + rand(i + c * 3) * .5) * u, up = Math.sin(u * Math.PI) * (.4 + rand(i + c) * .4);
          draw(disc, b.x + Math.cos(dir) * far, Y + .02, b.z + Math.sin(dir) * far * .6 + up * Lift, .05, .04, 0, Dirt.withAlpha(1 - u));
        }
      }
      if (h <= .001) continue;
      const amount = (i % 2 ? 1 : -1) * (Lean + Cross * Math.abs(ax)), g = h / p.tall;
      const tip = { x: b.x + ax * amount * g, z: b.z + az * amount * g };
      const left = [], right = [], spine = [], edgeL = [], edgeR = [], shL = [], shR = [];
      for (let j = 0; j <= 10; j++) {
        const u = j / 10, w = lerp(p.width, TipWidth, u) / 2 * (j === 10 ? .15 : 1);
        const gx = lerp(b.x, tip.x, u), gz = lerp(b.z, tip.z, u), x = gx, z = gz + h * u * Lift;
        // Width is measured across the rod's own screen axis so a leaning rod keeps its thickness.
        const dx = tip.x - b.x, dz = tip.z - b.z + h * Lift, len = Math.hypot(dx, dz) || 1, nx = dz / len, nz = -dx / len;
        left.push({ x: x - nx * w, z: z - nz * w }); right.push({ x: x + nx * w, z: z + nz * w });
        spine.push({ x: x - nx * w * .15, z: z - nz * w * .15 });
        edgeL.push({ x: x - nx * (w + .022), z: z - nz * (w + .022) }); edgeR.push({ x: x + nx * (w + .022), z: z + nz * (w + .022) });
        const sx = gx + sun.x * h * u, sz = gz + sun.z * h * u;
        shL.push({ x: sx - w, z: sz }); shR.push({ x: sx + w, z: sz });
      }
      const layer = layerAt(b.z), key = 'rods v2 rod ' + i;
      band(key + ' shadow', shL, shR, Body.withAlpha(strength * .7 * closing), shadowLayer + .001);
      band(key + ' edge', edgeL, edgeR, Rim.withAlpha(.85), layer);
      band(key + ' dark face', left, spine, Body, layer + .0001);
      band(key + ' lit face', spine, right, Face, layer + .0002);
      const rising = age < Punch + .25 ? 1 : .25;
      sprite({ x: tip.x, z: tip.z + h * Lift }, .3, .3, pale.withAlpha(.6 * rising), glow, layer + .0003);
    }
    if (pawn) {
      drawPawn();
      if (hitAge >= 0 && hitAge < .2) {
        const f = 1 - hitAge / .2;
        sprite({ x: pawn.x, z: pawn.z + .35 }, 1.1, .9, pale.withAlpha(f * .8), glow, Y + .12);
      }
    }
  },
};
