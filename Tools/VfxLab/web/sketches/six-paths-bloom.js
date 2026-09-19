// Obsidian Bloom — healing cocoon proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed, none of it agreed). Target the caster or one ally within 9 cells; a
// downed ally is allowed. One orb flies over and sinks under the pawn (0.4 s), six petals unfurl
// (0.65 s), poise (0.3 s) and fold shut over it (0.5 s, no shake: a fast snap reads as an attack).
// The bud stays closed for 6 s. Inside, the pawn cannot act, cannot be targeted and takes no
// damage; bleeding stops and it heals about 2 HP per second, one seam pulse per heal tick. Then
// the bud uncurls (0.65 s) and the orb returns. One orb, about 60 s cooldown. It is the friendly
// counterpart of Twin Maw: that closes on an enemy to hurt it, this closes on a friend to keep it.
//
// Curved petal strips project real height north by Lift; shadows stay at their roots.
// Caster and patient are stand-ins. The patient gets up after the bud opens to show the result.
import { Color, Mathf, Meshes } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Body, Rim, Y, Floor, Lift, at, sprite, orb, circle, band, trail, glow, soft, rand } from './lib/six-paths-impact.js';
const smooth = Mathf.Smooth, lerp = Mathf.Lerp;
const disc = Meshes.disc(32, 'bloom figure');
const pale = new Color(.88, .79, 1);
// Heal tick strength: 1 at each tick, decaying before the next.
const tick = (s, t, p) => s >= t.hit && s < t.open ? Math.exp(-((s - t.hit) % p.interval) / p.interval * 4) : 0;
function times(p) {
  const poise = p.sink + p.unfold, snap = poise + p.poise, hit = snap + p.snap;
  const open = hit + p.hold, dissolve = open + p.open;
  return { poise, snap, hit, open, dissolve, end: dissolve + p.reform };
}
function petal(s, p, t, o, i) {
  const grow = smooth((s - p.sink - i * 0.025) / (p.unfold - 0.125));
  const shut = smooth((s - t.snap) / p.snap) * (1 - smooth((s - t.open) / p.open));
  const dissolve = 1 - smooth((s - t.dissolve) / p.reform);
  if (grow <= 0 || dissolve <= 0) return;
  const angle = i / 6 * Math.PI * 2 + p.spin * Mathf.Deg2Rad;
  const radius = p.radius * grow * dissolve;
  const left = [], right = [], spine = [], seam = [], shadowL = [], shadowR = [];
  for (let j = 0; j <= 32; j++) {
    const u = j / 32;
    // At closure the tip returns over the centre while the belly bulges outward.
    const r = lerp(0.18 + radius * u, 0.28 * (1 - u) + radius * 0.47 * Math.sin(u * Math.PI), shut);
    const h = lerp(Math.sin(u * Math.PI) * 0.25 * grow, p.height * u * grow, shut) * dissolve;
    const w = Math.sin(u * Math.PI) ** 0.85 * radius * 0.40 * (1 - shut * 0.2);
    const x = Math.cos(angle) * r, z = Math.sin(angle) * r;
    left.push(at(o, x - Math.sin(angle) * w, z + Math.cos(angle) * w, h));
    right.push(at(o, x + Math.sin(angle) * w, z - Math.cos(angle) * w, h));
    spine.push(at(o, x, z, h + Math.sin(u * Math.PI) * 0.14));
    seam.push(at(o, x - Math.sin(angle) * w * 0.96, z + Math.cos(angle) * w * 0.96, h));
    shadowL.push(at(o, x - Math.sin(angle) * w, z + Math.cos(angle) * w));
    shadowR.push(at(o, x + Math.sin(angle) * w, z - Math.cos(angle) * w));
  }
  const layer = Y + (1 - Math.sin(angle)) * 0.035;
  const pulse = tick(s, t, p);
  band(`bloom shadow ${i}`, shadowL, shadowR, Body.withAlpha(0.28 * dissolve), Floor + 0.002);
  band(`bloom left ${i}`, left, spine, Body.withAlpha(dissolve), layer);
  band(`bloom right ${i}`, spine, right, new Color(0.075, 0.055, 0.105, dissolve), layer + 0.001);
  band(`bloom seam ${i}`, left, seam, Rim.withAlpha((0.65 + pulse * 0.35) * dissolve), layer + 0.003);
  trail(`bloom spine ${i}`, spine, 0.025 + pulse * 0.035, Rim.withAlpha((0.25 + pulse * 0.65) * dissolve), layer + 0.004);
}
export default {
  kit: 'Six Paths', label: 'Obsidian Bloom (sketch)',
  params: {
    patient: { label: 'Patient', value: 'downed ally', options: ['downed ally', 'standing ally'], group: 'Showcase' },
    actors: { label: 'Show caster and patient', value: true, group: 'Showcase' },
    distance: P('Caster distance (cells)', 4.5, 2, 9, .5, 'Showcase'),
    sink: P('Orb sinks', 0.4, 0.2, 1, 0.05, 'Timing (s)'),
    unfold: P('Petals unfurl', 0.65, 0.3, 1.2, 0.05, 'Timing (s)'),
    poise: P('Open flower anticipation', 0.3, 0.1, 0.8, 0.05, 'Timing (s)'),
    snap: P('Petals fold shut', 0.5, 0.2, 1, 0.05, 'Timing (s)'),
    hold: P('Cocoon held (heal time)', 6, 1, 8, 0.25, 'Timing (s)'),
    interval: P('Heal tick interval', 1, 0.5, 2, 0.25, 'Timing (s)'),
    open: P('Uncurl', 0.65, 0.3, 1.2, 0.05, 'Timing (s)'),
    reform: P('Reform one orb', 0.5, 0.2, 1, 0.05, 'Timing (s)'),
    radius: P('Open petal reach (cells)', 1.4, 1, 4, 0.1, 'Shape'),
    height: P('Closed lotus height (cells)', 2, 1.2, 4.5, 0.1, 'Shape'),
    spin: P('Flower angle (degrees)', 15, 0, 60, 5, 'Shape'),
    motes: P('Rising heal motes', 0.7, 0, 1, 0.05, 'Feedback'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [{ name: 'Sink', t: 0 }, { name: 'Unfurl', t: p.sink },
    { name: 'Poise', t: t.poise }, { name: 'Fold shut', t: t.snap }, { name: 'Cocoon / heal', t: t.hit },
    { name: 'Uncurl', t: t.open }, { name: 'Orb returns', t: t.dissolve }]; },
  events() { return []; },
  draw(s, p, { origin, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const caster = { x: origin.x - p.distance, z: origin.z };
    const sunk = smooth(s / p.sink), reform = smooth((s - t.dissolve) / p.reform);
    const closed = smooth((s - t.snap) / p.snap) * (1 - smooth((s - t.open) / p.open));
    if (p.actors) {
      // The patient lies on the open flower, then sits behind the front petals once they rise.
      const layer = closed > .35 ? Y + .02 : Y + .09, skin = new Color(.83, .70, .54);
      sprite({ x: caster.x + sun.x * .45, z: caster.z + sun.z * .45 }, .85, .4, Body.withAlpha(strength), soft, Floor);
      draw(disc, caster.x, Y - .05, caster.z + .18, .22, .32, 0, new Color(.39, .58, .65));
      draw(disc, caster.x, Y - .048, caster.z + .58, .16, .17, 0, skin);
      // A downed patient lies bleeding until the bud has opened again, then stands.
      const down = p.patient === 'downed ally' && s < t.open + p.open * .6, shirt = new Color(.45, .55, .38);
      sprite({ x: origin.x + sun.x * .3, z: origin.z + sun.z * .3 }, .85, .4, Body.withAlpha(strength), soft, Floor);
      if (down) {
        sprite({ x: origin.x + .3, z: origin.z - .22 }, .95, .5, new Color(.42, .07, .07, .6), soft, Floor + .001);
        draw(disc, origin.x + .05, layer, origin.z + .05, .32, .2, 0, shirt);
        draw(disc, origin.x - .36, layer + .002, origin.z + .08, .17, .16, 0, skin);
      } else {
        draw(disc, origin.x, layer, origin.z + .18, .22, .32, 0, shirt);
        draw(disc, origin.x, layer + .002, origin.z + .58, .16, .17, 0, skin);
      }
    }
    const fade = 1 - smooth((s - t.end + 0.15) / 0.15);
    sprite(origin, p.radius * 2 * sunk, p.radius * 2 * sunk, Body.withAlpha(0.3 * (1 - reform)), undefined, Floor);
    circle(origin, p.radius * 0.95, 0.2 * sunk * (1 - smooth((s - t.snap) / p.snap)));
    // The orb flies from the caster, drops under the patient, and goes back at the end.
    const path = (u) => ({ x: lerp(caster.x, origin.x, u),
      z: lerp(caster.z, origin.z, u) + (.7 * (1 - u * u) + Math.sin(u * Math.PI) * .8) * Lift });
    if (s < p.sink) {
      const u = smooth(s / (p.sink * .7)), drop = smooth((s - p.sink * .7) / (p.sink * .3));
      orb(path(u), 0.34 * (1 - drop * .9), 1, 1 - drop * .5);
      trail('bloom orb out', Array.from({ length: 14 }, (_, j) => path(smooth(Math.max(0, s - (1 - j / 13) * .14) / (p.sink * .7)))),
        .08, Rim.withAlpha(.5 * (1 - drop)));
    }
    for (let i = 0; i < 6; i++) petal(s, p, t, origin, i);
    // Heal ticks: a glow at the tip, a soft floor ring, and motes that rise off the seams.
    const pulse = tick(s, t, p);
    sprite(at(origin, 0, 0, p.height), 0.9 + pulse * .4, 0.9 + pulse * .4, pale.withAlpha((.15 + pulse * .55) * closed), glow, Y + 0.12);
    if (pulse > 0) circle(origin, p.radius * (.55 + (1 - pulse) * .5), pulse * .45, Floor + .003, pale);
    if (closed > .5) for (let i = 0; i < 10; i++) {
      const life = 1.4 + rand(i) * .8, u = ((s - t.hit) / life + rand(i + 9)) % 1, a = rand(i + 30) * Math.PI * 2;
      const r = p.radius * .42 * (1 - u * .5);
      sprite(at(origin, Math.cos(a) * r, Math.sin(a) * r * .6, p.height * (.25 + u * .95)), .12, .12,
        pale.withAlpha(Math.sin(u * Math.PI) * p.motes * closed), glow, Y + 0.13);
    }
    if (reform > 0) {
      orb(path(1 - reform), 0.34 * Math.min(1, reform * 4), fade);
      trail('bloom orb back', Array.from({ length: 14 }, (_, j) =>
        path(1 - smooth(Math.max(0, s - t.dissolve - (1 - j / 13) * .14) / p.reform))), .08, Rim.withAlpha(.5 * fade));
    }
  },
};
