// Twin Maw — ground bear-trap proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed, none of it agreed). Target a tile within 12 cells in line of sight.
// Two orbs sink into the ground left and right of the tile over 0.5 s and leave a toothed seam.
// The trap stays armed for 20 s (the sketch shows a shorter wait). The first hostile pawn on the
// tile triggers it; a hostile already standing there triggers it as soon as it arms. Two jaws
// rise out of the floor in 0.10 s and shut in 0.12 s: about 30 sharp damage over 3 bites 0.3 s
// apart, and the pawn cannot move for 4 s. Then the jaws open, sink, and the two orbs return.
// Cooldown 30 s from snap or expiry. Two of the six orbs are unavailable while it is armed.
//
// The orbs and the sage: the caster carries the six orbs on its ring (lib/six-paths-sage.js).
// Slots 0 and 1 leave the ring at 0.09 radius, grow to the field cast radius 0.30 over the flight
// and sink into the sockets. Both slots stay empty while the trap is armed or shut, so the ring
// shows four orbs left. On the way back each shrinks to 0.09 and sits in its slot.
//
// Drawing: the jaws always close east-west on screen whatever the cast direction, so height and
// span never share the screen axis. Each jaw is a tall back rim behind the pawn and a short front
// rim across its body. Caster and victim are stand-ins.
import { AltitudeLayer, Color, Mathf, Meshes } from '../js/engine.js';
import { draw, mesh, Slate, Lift } from './lib/six-paths-solid.js';
import { P, Body, Rim, Y, Floor, orb, sprite, trail, band, impact, glow, soft } from './lib/six-paths-impact.js';
import { sage, carried, slot, deployRadius, Field } from './lib/six-paths-sage.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const disc = Meshes.disc(32, 'maw disc');
const ring = Meshes.band(0.95, 1, 64, 'maw armed ring');
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor();
const pale = new Color(.88, .79, 1);
const OpenAngle = 70 * Mathf.Deg2Rad, Sweep = 88 * Mathf.Deg2Rad, Bites = 3, BiteGap = .3;

function timing(p) {
  const trigger = p.arm + (p.scenario === 'walks in' ? p.wait : 0);
  const rise = trigger + p.rise, shut = rise + p.snap, release = shut + p.hold;
  const sink = release + .2, gone = sink + .25;
  return { trigger, rise, shut, release, sink, gone, end: gone + p.recall + .3 };
}

function figure(pos, body, layer, alpha = 1) {
  draw(disc, pos.x, layer, pos.z + .18, .22, .32, 0, body.withAlpha(alpha));
  draw(disc, pos.x, layer + .002, pos.z + .58, .16, .17, 0, new Color(.83, .70, .54, alpha));
}

export default {
  kit: 'Six Paths', label: 'Twin Maw (sketch)',
  params: {
    scenario: { label: 'Trigger', value: 'walks in', options: ['walks in', 'occupied'], group: 'Showcase' },
    actors: { label: 'Show caster and victim', value: true, group: 'Showcase' },
    distance: P('Caster distance (cells)', 5, 3, 12, .5, 'Showcase'),
    wait: P('Armed wait shown (real: 20 s)', 1.6, .6, 4, .1, 'Showcase'),
    arm: P('Orbs sink and arm', .5, .25, 1, .05, 'Timing (s)'),
    rise: P('Jaws rise', .1, .04, .3, .01, 'Timing (s)'),
    snap: P('Jaws shut', .12, .06, .3, .01, 'Timing (s)'),
    hold: P('Hold', 4, 1, 6, .25, 'Timing (s)'),
    recall: P('Orbs return', .65, .3, 1, .05, 'Timing (s)'),
    size: P('Back jaw height (cells)', 1.55, 1.1, 2, .05, 'Shape'),
    reach: P('Hinge half-spacing (cells)', .95, .7, 1.3, .05, 'Shape'),
    plate: P('Jaw thickness (cells)', .30, .18, .45, .01, 'Shape'),
    teeth: P('Teeth per rim', 6, 3, 9, 1, 'Shape'),
    shake: P('Snap shake', .1, 0, .2, .01, 'Impact'),
  },
  duration(p) { return timing(p).end; },
  phases(p) { const t = timing(p); return [
    { name: 'Orbs sink', t: 0 }, { name: 'Armed', t: p.arm }, { name: 'Jaws rise', t: t.trigger },
    { name: 'Shut / bite', t: t.rise }, { name: 'Hold', t: t.shut }, { name: 'Release', t: t.release },
    { name: 'Orbs return', t: t.gone },
  ]; },
  events(p) { return [{ t: timing(p).shut, type: 'shake', value: p.shake }]; },
  draw(s, p, { origin: o, scene }) {
    const t = timing(p);
    if (s < 0 || s > t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 };
    const strength = scene?.sun?.strength ?? .32;
    const caster = { x: o.x - p.distance, z: o.z };

    // Closure: 0 open, 1 shut. Two re-clenches after the first bite, then a slow breathing grip.
    let shutness = clamp((s - t.rise) / p.snap) ** 2;
    for (let k = 1; k < Bites; k++)
      shutness -= .13 * Math.sin(clamp((s - t.shut - k * BiteGap) / .16) * Math.PI);
    if (s > t.shut && s < t.release) shutness -= .012 * (1 + Math.sin(s * 9));
    shutness *= 1 - smooth((s - t.release) / .2);
    const grow = smooth((s - t.trigger) / p.rise) * (1 - smooth((s - t.sink) / .25));
    const planted = smooth((s - p.arm * .75) / (p.arm * .25)) * (1 - smooth((s - t.gone) / .15));

    // Armed marker: sockets where the orbs went in, and a toothed seam between them.
    if (planted > .001) {
      const armed = planted * (1 - .75 * grow), pulse = .5 + .5 * Math.sin(s * 5);
      draw(ring, o.x, Floor + .008, o.z, p.reach * 1.18, .52, 0, Rim.withAlpha(armed * (.14 + .1 * pulse)));
      const seam = Array.from({ length: 15 }, (_, j) => ({
        x: o.x + (j / 14 * 2 - 1) * p.reach, z: o.z + (j % 2 ? .075 : -.075) * (j > 0 && j < 14),
      }));
      trail('maw seam dark', seam, .11, Body.withAlpha(armed * .8), Floor + .009);
      trail('maw seam edge', seam, .04, Rim.withAlpha(armed * (.35 + .35 * pulse)), Floor + .010);
      for (const side of [-1, 1]) {
        const q = { x: o.x + side * p.reach, z: o.z };
        sprite(q, .7, .36, Body.withAlpha(strength * .85 * planted), soft, shadowLayer + .004);
        draw(disc, q.x, shadowLayer + .005, q.z, .2, .1, 0, new Color(.018, .014, .024, planted));
      }
    }

    // One rim of one jaw. zOff sets depth, tall scales height; returns nothing, draws bands.
    const rimOf = (key, side, zOff, tall, layer, shade) => {
      if (grow <= .001) return;
      const theta = (1 - shutness) * OpenAngle, cs = Math.cos(theta), sn = Math.sin(theta);
      const n = 28, pts = [];
      for (let j = 0; j <= n; j++) {
        const u = j / n * grow, a = u * Sweep;
        const rx = p.reach * Math.cos(a) - p.reach, ry = p.size * tall * Math.sin(a);
        const h = Math.max(0, -rx * sn + ry * cs);
        pts.push({ u, h, gx: o.x + side * (p.reach + rx * cs + ry * sn), gz: o.z + zOff });
      }
      for (const q of pts) { q.x = q.gx; q.z = q.gz + q.h * Lift; }
      const outer = [], inner = [], edgeA = [], edgeB = [], ridge = [], shA = [], shB = [];
      const teeth = [], tri = [];
      pts.forEach((q, j) => {
        const a = pts[Math.max(0, j - 1)], b = pts[Math.min(n, j + 1)];
        const tx = b.x - a.x, tz = b.z - a.z, len = Math.hypot(tx, tz) || 1;
        q.tx = tx / len; q.tz = tz / len; q.nx = -q.tz * side; q.nz = q.tx * side;
        q.w = (p.plate * (1 - q.u) ** .7 + .02) * (tall < 1 ? .85 : 1);
        const half = q.w / 2, e = half + .028;
        outer.push({ x: q.x - q.nx * half, z: q.z - q.nz * half });
        inner.push({ x: q.x + q.nx * half, z: q.z + q.nz * half });
        edgeA.push({ x: q.x - q.nx * e, z: q.z - q.nz * e });
        edgeB.push({ x: q.x + q.nx * e, z: q.z + q.nz * e });
        ridge.push({ x: q.x - q.nx * half * .25, z: q.z - q.nz * half * .25 });
        const sx = q.gx + sun.x * q.h, sz = q.gz + sun.z * q.h;
        shA.push({ x: sx - half, z: sz }); shB.push({ x: sx + half, z: sz });
      });
      // Left and right teeth are staggered half a step so they mesh when shut.
      for (let k = 0; k < p.teeth; k++) {
        const u = .2 + (k + (side > 0 ? .5 : 0)) / p.teeth * .78;
        if (u > grow) continue;
        const q = pts[Math.min(n, Math.round(u / grow * n))];
        const bx = q.x + q.nx * q.w * .4, bz = q.z + q.nz * q.w * .4, len = .26 * (1 - .3 * u), w = .075;
        const i = teeth.length / 2;
        teeth.push(bx - q.tx * w, bz - q.tz * w, bx + q.tx * w, bz + q.tz * w,
          bx + q.nx * len, bz + q.nz * len - .04);
        tri.push(i, i + 1, i + 2);
      }
      band(key + ' shadow', shA, shB, Body.withAlpha(strength * .6), shadowLayer + .001);
      band(key + ' edge', edgeA, edgeB, Rim.withAlpha(.9), layer);
      band(key + ' body', outer, inner, Color.Lerp(Body, Slate, shade), layer + .001);
      band(key + ' ridge', outer, ridge, Color.Lerp(Body, Slate, shade + .45), layer + .002);
      if (teeth.length) {
        const m = mesh(key + ' teeth'); m.setFlat(teeth, tri);
        draw(m, 0, layer + .003, 0, 1, 1, 0, pale.withAlpha(.95));
      }
    };

    // Slots 0 and 1 leave the ring, drop into the sockets, and come back to their slots at the end.
    carried(caster, s, scene, (i) => i < 2);
    for (const side of [-1, 1]) {
      const socket = { x: o.x + side * p.reach, z: o.z }, home = slot(caster, side < 0 ? 0 : 1, s);
      const path = (u) => ({
        x: lerp(home.x, socket.x, u),
        z: lerp(home.z, socket.z, u) + Math.sin(u * Math.PI) * .9 * Lift,
      });
      const out = s < p.arm, u = out ? smooth(s / (p.arm * .75)) : 1 - smooth((s - t.gone) / p.recall);
      if (out || s >= t.gone) {
        const sinking = out ? smooth((s - p.arm * .75) / (p.arm * .25)) : 0;
        // Coming back, the orb rises out of the socket before it sets off.
        const emerge = out ? 1 : Math.min(1, (1 - u) * 4);
        orb(path(u), deployRadius(u, Field) * (1 - sinking) * emerge, 1, 1 - sinking * .5, u > 0 ? Y : home.layer);
        const from = out ? s : s - t.gone, span = out ? p.arm * .75 : p.recall;
        const tail = Array.from({ length: 16 }, (_, j) => {
          const v = smooth(Math.max(0, from - (1 - j / 15) * .14) / span);
          return path(out ? v : 1 - v);
        });
        trail('maw orb trail ' + side, tail, .08, Rim.withAlpha(.5 * (1 - sinking)));
        if (out && sinking > 0)
          sprite(socket, .5 + sinking * .5, .25 + sinking * .25,
            new Color(.56, .49, .40, Math.sin(sinking * Math.PI) * .35), soft, shadowLayer + .02);
      }
      impact('maw burst ' + side, socket, s - t.trigger, .7, .5);
    }

    for (const side of [-1, 1]) rimOf('maw back ' + side, side, .2, 1, Y + .02, .12);

    if (p.actors) {
      sage(caster, 0, scene);
      const walking = p.scenario === 'walks in' && s < t.trigger;
      const w = walking ? clamp((s - p.arm) / p.wait) : 1;
      const held = s > t.rise && s < t.release;
      const x = (1 - w) * 3.6 + (held ? Math.sin(s * 31) * .025 : 0);
      const bob = walking && s > p.arm ? Math.abs(Math.sin(s * 9)) * .05 : 0;
      const pos = { x: o.x + x, z: o.z + bob };
      sprite({ x: pos.x + sun.x * .45, z: o.z + sun.z * .45 }, .85, .4, Body.withAlpha(strength), soft, shadowLayer);
      figure(pos, new Color(.55, .38, .27), Y + .05);
    }

    for (const side of [-1, 1]) rimOf('maw front ' + side, side, -.2, .6, Y + .09, .2);

    for (let k = 0; k < Bites; k++) {
      const age = s - t.shut - k * BiteGap;
      if (age < 0 || age > .16) continue;
      const f = 1 - age / .16;
      sprite({ x: o.x, z: o.z + .42 }, 1.5 - k * .25, .9 - k * .15, pale.withAlpha(f * (k ? .45 : .8)), glow, Y + .12);
    }
  },
};
