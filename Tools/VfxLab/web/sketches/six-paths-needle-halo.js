// Needle Halo — weapon form proposal, not the game. Nothing in Source/RimArt draws this yet.
// It reimagines the Nullify volley sketch (six-paths-nullify.js), whose picture (an orb throwing
// darts) implied damage while its rule did none.
//
// What it is for (proposed, none of it agreed). The ranged weapon form, made of one orb; the other
// five stay free for techniques. The orb floats above the sage's head as a hub with a level ring
// of 12 needles around it. The ring is the magazine: it regrows one needle every 0.5 s.
//   normal attack  a burst of 3 needles taken from the side nearest the target: 8 Sharp each,
//                  range 24, 0.9 s warm-up, 1.6 s cooldown. Needles steer around allies, so there
//                  is no friendly fire. Each hit adds a stack of "numbed" (about -4% aiming and
//                  -4% move speed for 8 s, up to 5 stacks), shown here as pins left in the target.
//   active, Seal   fires every needle left in the ring at one pawn within 15 cells. Ends its
//                  active psycast effects and zeroes its shield belt, and blocks it from using
//                  any ability for 1 s per needle that lands, so up to 12 s from a full ring and
//                  little right after a long burst. 40 s cooldown. The floor ring under the target
//                  has one segment per needle and loses one per second.
//
// Drawing: a level ring stays a circle under RimWorld's straight-down camera and height only
// shifts it north, so the halo looks the same for every facing. The magazine is simulated from
// the start of the clip each frame, so scrubbing backwards works. Pawns are stand-ins.
import { AltitudeLayer, Color, Mathf, Meshes } from '../js/engine.js';
import { draw, Slate } from './lib/six-paths-solid.js';
import { P, Body, Rim, Y, Floor, Lift, orb, sprite, trail, band, circle, glow, soft } from './lib/six-paths-impact.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const disc = Meshes.disc(32, 'halo disc'), hoop = Meshes.band(.93, 1, 48, 'halo hoop');
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const pale = new Color(.88, .79, 1), shield = new Color(.62, .80, 1);
// The rule's numbers and decided looks, kept out of the panel.
const Slots = 12, Regrow = .5, Burst = 3, ShotGap = .08, SealGap = .04, Speed = 22, Swing = .12, BurstEvery = 2.5;
const Inner = .26, Spin = .5, MaxPins = 5;

function times(p) {
  const form = p.morph, bursts = Array.from({ length: p.bursts }, (_, k) => form + .7 + k * BurstEvery);
  const seal = (bursts.length ? bursts[bursts.length - 1] : form + .2) + p.sealDelay;
  return { form, bursts, seal };
}

// Plays the magazine from the start up to time s. Returns the ring's slots and every needle fired.
function magazine(p, t, s, aimAngle) {
  const slots = Array.from({ length: Slots }, () => ({ born: -1, gone: Infinity })), flights = [], events = [];
  for (const b of t.bursts) for (let j = 0; j < Burst; j++) events.push({ at: b + j * ShotGap, kind: 'shot' });
  events.push({ at: t.seal, kind: 'seal' });
  for (let tick = t.form + Regrow; tick <= s; tick += Regrow) events.push({ at: tick, kind: 'grow' });
  events.sort((a, b) => a.at - b.at);
  const aligned = (at) => slots.map((slot, i) => ({ slot, i, fit: Math.cos(i / Slots * Math.PI * 2 + Spin * at - aimAngle) }))
    .filter(({ slot }) => slot.born <= at && slot.gone === Infinity).sort((a, b) => b.fit - a.fit);
  for (const e of events) {
    if (e.at > s) break;
    if (e.kind === 'grow') {
      const empty = slots.filter(slot => slot.gone <= e.at).sort((a, b) => a.gone - b.gone)[0];
      if (empty) { empty.born = e.at; empty.gone = Infinity; }
    } else aligned(e.at).slice(0, e.kind === 'seal' ? Slots : 1).forEach(({ slot, i }, j) => {
      const fired = e.at + (e.kind === 'seal' ? j * SealGap : 0);
      slot.gone = fired; flights.push({ i, fired, seal: e.kind === 'seal', side: flights.length % 2 ? 1 : -1 });
    });
  }
  return { slots, flights };
}

export default {
  kit: 'Six Paths', label: 'Needle Halo (sketch)',
  params: {
    bursts: P('Normal bursts shown first (2.5 s apart)', 2, 0, 3, 1, 'Showcase'),
    sealDelay: P('Seal fires this long after the last burst', 1.1, .2, 4, .1, 'Showcase'),
    sealShown: P('Seal countdown shown (real: 1 s per needle)', 3, 1, 12, .5, 'Showcase'),
    actors: { label: 'Show target and ally in the line of fire', value: true, group: 'Showcase' },
    aim: P('Aim direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Target distance (cells)', 7, 4, 15, .5, 'Showcase'),
    morph: P('Orb rises and grows the ring', .5, .25, 1, .05, 'Timing (s)'),
    hub: P('Hub height (cells)', 1.75, 1.3, 2.5, .05, 'Shape'),
    length: P('Needle length (cells)', .46, .3, .7, .02, 'Shape'),
    width: P('Needle width (cells)', .07, .04, .12, .005, 'Shape'),
    steer: P('Steering bulge around the ally (cells)', .7, 0, 1.5, .05, 'Shape'),
    flash: P('Hit flash strength', .7, 0, 1, .05, 'Feedback'),
  },
  duration(p) { const t = times(p); return t.seal + Slots * SealGap + Swing + p.distance / Speed + p.sealShown + .5; },
  phases(p) { const t = times(p); return [
    { name: 'Form halo', t: 0 }, ...t.bursts.map((b, k) => ({ name: 'Burst ' + (k + 1), t: b })),
    { name: 'Seal', t: t.seal }, { name: 'Sealed / ring regrows', t: t.seal + Slots * SealGap + Swing + p.distance / Speed },
  ]; },
  events() { return []; },
  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a0 = p.aim * Mathf.Deg2Rad, ca = Math.cos(a0), sa = Math.sin(a0);
    const S = { x: o.x - ca * p.distance / 2, z: o.z - sa * p.distance / 2 }, T = { x: o.x + ca * p.distance / 2, z: o.z + sa * p.distance / 2 };
    const show = (q) => ({ x: q.x, z: q.z + q.h * Lift }), shade = (q) => ({ x: q.x + sun.x * q.h, z: q.z + sun.z * q.h });
    const flight = p.distance / Speed;

    // A needle between two 3D points: shadow, violet edge, a dark and a lit face.
    const needle = (key, tail, tip, scale = 1, alpha = 1, layer = Y + .1) => {
      const A = show(tail), B = show(tip), dx = B.x - A.x, dz = B.z - A.z, len = Math.hypot(dx, dz) || 1, nx = -dz / len, nz = dx / len;
      const L = [], R = [], C = [], EL = [], ER = [], SL = [], SR = [], a = shade(tail), b = shade(tip);
      for (let j = 0; j <= 6; j++) {
        const u = j / 6, w = p.width / 2 * scale * Math.sin(Math.min(1, u * 1.6 + .08) * Math.PI * .5) * (1 - u) ** .6 * 1.6;
        const x = A.x + dx * u, z = A.z + dz * u;
        L.push({ x: x - nx * w, z: z - nz * w }); R.push({ x: x + nx * w, z: z + nz * w }); C.push({ x, z });
        EL.push({ x: x - nx * (w + .015), z: z - nz * (w + .015) }); ER.push({ x: x + nx * (w + .015), z: z + nz * (w + .015) });
        const sx = lerp(a.x, b.x, u), sz = lerp(a.z, b.z, u); SL.push({ x: sx - w, z: sz }); SR.push({ x: sx + w, z: sz });
      }
      band(key + ' shadow', SL, SR, Body.withAlpha(strength * .6 * alpha), shadowLayer + .001);
      band(key + ' edge', EL, ER, Rim.withAlpha(.9 * alpha), layer);
      band(key + ' dark', L, C, Body.withAlpha(alpha), layer + .0005);
      band(key + ' lit', C, R, Color.Lerp(Body, Slate, .55).withAlpha(alpha), layer + .001);
    };
    const figure = (pos, colour) => {
      sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, .85, .4, Body.withAlpha(strength), soft, shadowLayer);
      draw(disc, pos.x, pawnLayer, pos.z + .18, .22, .32, 0, colour);
      draw(disc, pos.x, pawnLayer + .002, pos.z + .58, .16, .17, 0, new Color(.83, .70, .54));
    };
    const ally = { x: lerp(S.x, T.x, .45), z: lerp(S.z, T.z, .45) };
    figure(S, new Color(.39, .58, .65));
    if (p.actors) { figure(T, new Color(.55, .38, .27)); figure(ally, new Color(.45, .55, .38)); }

    const { slots, flights } = magazine(p, t, s, a0);
    const formed = smooth(s / p.morph), hubH = lerp(.7, p.hub, formed), bobbing = Math.sin(s * 2.2) * .03;
    const hub = { x: S.x, z: S.z, h: hubH + bobbing };
    const slotPoint = (i, at, r) => { const a = i / Slots * Math.PI * 2 + Spin * at; return { x: hub.x + Math.cos(a) * r, z: hub.z + Math.sin(a) * r, h: hub.h }; };

    // Hub orb, its shadow, and a faint hoop that marks the ring even when it is empty.
    sprite(shade(hub), .5, .25, Body.withAlpha(strength * .7), soft, shadowLayer);
    const hubAt = show(hub);
    draw(hoop, hubAt.x, Y + .09, hubAt.z, (Inner + p.length * .5) * formed, (Inner + p.length * .5) * formed, 0, Rim.withAlpha(.28 * formed));
    orb(hubAt, .2, 1, 1, Y + .11);
    sprite({ x: hubAt.x - .05, z: hubAt.z + .06 }, .12, .1, pale.withAlpha(.5), glow, Y + .115);
    slots.forEach((slot, i) => {
      if (slot.gone <= s) return;
      const grown = slot.born < 0 ? formed : smooth((s - slot.born) / .3);
      if (grown <= .01) return;
      needle('halo needle ' + i, slotPoint(i, s, Inner), slotPoint(i, s, Inner + p.length * grown), grown);
      if (slot.born >= 0 && s - slot.born < .3) sprite(show(slotPoint(i, s, Inner + p.length * grown)), .2, .2, pale.withAlpha((1 - grown) * .8), glow, Y + .12);
    });

    // Needles in the air. Each swings to face the target, then flies on a low arc that bows
    // sideways around the ally in the line of fire.
    let pins = 0, sealLanded = 0, lastSealLanding = -1, firstSealLanding = Infinity;
    for (const f of flights) {
      const start = slotPoint(f.i, f.fired, Inner + p.length * .5), lands = f.fired + Swing + flight, u = (s - f.fired - Swing) / flight;
      const end = { x: T.x - ca * .1, z: T.z - sa * .1, h: .5 };
      if (f.seal) { firstSealLanding = Math.min(firstSealLanding, lands); if (s >= lands) { sealLanded++; lastSealLanding = Math.max(lastSealLanding, lands); } }
      else if (s >= lands) pins++;
      if (s < f.fired || u >= 1) { if (u >= 1 && s - lands < .2) sprite(show(end), .55, .55, pale.withAlpha((1 - (s - lands) / .2) * p.flash), glow, Y + .14); continue; }
      const along = (v) => {
        const bulge = Math.sin(v * Math.PI) * p.steer * f.side * (p.actors ? 1 : 0);
        return { x: lerp(start.x, end.x, v) - sa * bulge, z: lerp(start.z, end.z, v) + ca * bulge, h: lerp(start.h, end.h, v) + Math.sin(v * Math.PI) * .35 };
      };
      if (u < 0) {
        // Still on the ring: turn from pointing outward to pointing at the target.
        const turn = smooth((s - f.fired) / Swing), out = slotPoint(f.i, f.fired, Inner + p.length), ahead = along(.06);
        const dir = { x: lerp(out.x - start.x, ahead.x - start.x, turn), z: lerp(out.z - start.z, ahead.z - start.z, turn), h: lerp(0, ahead.h - start.h, turn) };
        const n = Math.hypot(dir.x, dir.z, dir.h) || 1, half = p.length / 2;
        needle('halo flight ' + f.i + f.fired.toFixed(2), { x: start.x - dir.x / n * half, z: start.z - dir.z / n * half, h: start.h - dir.h / n * half },
          { x: start.x + dir.x / n * half, z: start.z + dir.z / n * half, h: start.h + dir.h / n * half });
        continue;
      }
      const head = along(u), tailPoint = along(Math.max(0, u - p.length / p.distance));
      needle('halo flight ' + f.i + f.fired.toFixed(2), tailPoint, head, 1, 1, Y + .16);
      trail('halo streak ' + f.i + f.fired.toFixed(2), [show(along(Math.max(0, u - .16))), show(along(Math.max(0, u - .08))), show(tailPoint)], .04, pale.withAlpha(.5), Y + .15);
    }

    if (p.actors) {
      // The target's shield bubble and psycast glow, until the first Seal needle lands.
      const popped = s - firstSealLanding;
      if (popped < .25) {
        const f = popped < 0 ? 1 : 1 - popped / .25, grow = popped < 0 ? 1 : 1 + popped * 2;
        circle({ x: T.x, z: T.z + .3 }, .62 * grow, .55 * f, Y + .05, shield);
        sprite({ x: T.x, z: T.z + .3 }, 1.3 * grow, 1.3 * grow, shield.withAlpha(.12 * f + (popped >= 0 ? .5 * f : 0)), glow, Y + .049);
      }
      // Numbed: pins left standing in the target, one per stack.
      for (let k = 0; k < Math.min(MaxPins, pins + sealLanded); k++) {
        const a = a0 + Math.PI + (k - 2) * .45, root = { x: T.x + Math.cos(a) * .12, z: T.z + Math.sin(a) * .06, h: .45 + (k % 2) * .15 };
        needle('halo pin ' + k, { x: root.x + Math.cos(a) * .34, z: root.z + Math.sin(a) * .34, h: root.h + .16 }, root, .8, 1, pawnLayer + .01);
      }
      // Sealed: one floor segment per needle that landed, and one goes out each second.
      const left = sealLanded - Math.max(0, Math.floor(s - lastSealLanding));
      for (let k = 0; k < Slots; k++) {
        const c = -Math.PI / 2 - (k + .5) / Slots * Math.PI * 2, lit = k < left && sealLanded > 0;
        if (sealLanded === 0 && s < t.seal) break;
        const arc = Array.from({ length: 5 }, (_, j) => { const a = c + (j / 4 - .5) * (Math.PI * 2 / Slots) * .8; return { x: T.x + Math.cos(a) * .85, z: T.z + Math.sin(a) * .85 }; });
        trail('halo seal segment ' + k, arc, lit ? .12 : .05, (lit ? pale : Rim).withAlpha(lit ? .9 : .2), Floor + .01);
      }
      if (left > 0) sprite({ x: T.x, z: T.z + .3 }, 1.2, 1.2, Rim.withAlpha(.16 + .06 * Math.sin(s * 6)), glow, Y + .048);
    }
  },
};
