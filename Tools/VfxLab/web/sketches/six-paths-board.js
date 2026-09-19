// Obsidian Board — weapon form proposal (a mount, not a weapon), not the game. Nothing in
// Source/RimArt draws this yet.
//
// What it is for (proposed, none of it agreed). A form made of two orbs; the other four stay free
// for techniques.
//   mount   a toggle, no cooldown. Two orbs sink at the sage's feet and a board rises out of the
//           floor under them (0.6 s), then lifts to 0.3 cells.
//   riding  move speed +60% (4.6 -> 7.4 cells/s). Ignores terrain move cost and crosses water; does
//           not set off floor traps. Walls still block it: it hovers at shin height, it does not
//           fly. The sketch puts a walking pawn beside the sage and a strip of shallow water across
//           both paths: the walker slows to about 1.5 cells/s in it, the board does not.
//   active  Send. Target a downed pawn within 15 cells in line of sight. The sage steps off. The
//           board goes out at 6 cells/s, drops to the floor to slide under the pawn, lifts it
//           (0.45 s), brings it back to the cell next to the sage and sets it down (0.45 s). The
//           carried pawn can still be hit. Works on downed enemies too. The sage walks while the
//           board is away. 25 s cooldown. It replaces the separate Black Litter idea.
//   The clip ends with a dismount so the toggle-off is shown: the board sinks into the floor under
//   the patient and the two orbs come back up and rejoin the other four.
//
// Drawing: the board is a flat shape at one height, so it turns freely with the travel direction
// and has no per-facing method. It is drawn under the pawn layer (in game: below Pawn, above
// Shadows) and sits 0.12 cells south of the rider's position so it is under the feet of the sprite.
// The rider and the patient are shifted north by height x Lift; shadows stay on the ground. For the
// trip back the board does not turn round under the patient: its point moves to the other end
// (the material is an orb, it can). Pawns and the water are stand-ins.
import { AltitudeLayer, Color, Mathf, Meshes } from '../js/engine.js';
import { draw, Slate } from './lib/six-paths-solid.js';
import { P, Body, Rim, Y, Floor, Lift, orb, sprite, trail, band, circle, glow, soft, rand } from './lib/six-paths-impact.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const disc = Meshes.disc(32, 'board disc');
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const boardLayer = pawnLayer - .02;
const pale = new Color(.88, .79, 1), dustColour = new Color(.56, .49, .40), ripple = new Color(.72, .84, .92);
const skin = new Color(.83, .70, .54);
// The rule's numbers and decided looks, kept out of the panel.
const WalkSpeed = 4.6, RideBonus = 1.6, SendSpeed = 6, WadeSpeed = 1.5;   // cells/s
const StepOff = .35, Dismount = .7, Low = .03, FeetOffset = -.12, Thickness = .07;
const OrbitRadius = .42, OrbitHeight = .95, OrbSize = .13, WalkerLane = -1.6, SageLane = 1;
const PuffGap = .045, PuffLife = .55;

// Short push-off, steady middle, gentle stop.
const glide = (u) => { u = clamp(u); return .45 * u + .55 * smooth(u); };

function times(p) {
  const rideDur = p.ride / (WalkSpeed * RideBonus) * 1.2, sendDur = p.send / SendSpeed * 1.2;
  const ride0 = p.mount + .15, ride1 = ride0 + rideDur, off = ride1 + StepOff;
  const send0 = off + .1, send1 = send0 + sendDur, loaded = send1 + p.load;
  const back1 = loaded + sendDur, unloaded = back1 + p.load, home = unloaded + Dismount;
  return { rideDur, sendDur, ride0, ride1, off, send0, send1, loaded, back1, unloaded, home, end: home + .8 };
}

export default {
  kit: 'Six Paths', label: 'Obsidian Board (sketch)',
  params: {
    actors: { label: 'Show pawns', value: true, group: 'Showcase' },
    walker: { label: 'Show walking pawn and water strip', value: true, group: 'Showcase' },
    aim: P('Travel direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    ride: P('Ride distance (cells)', 5, 3, 9, .5, 'Showcase'),
    send: P('Downed pawn distance (cells)', 5, 3, 9, .5, 'Showcase'),
    mount: P('Orbs sink, board rises', .6, .3, 1.2, .05, 'Timing (s)'),
    load: P('Lift / set down the patient', .45, .2, 1, .05, 'Timing (s)'),
    hover: P('Hover height (cells)', .3, .15, .6, .05, 'Shape'),
    length: P('Board length (cells)', 1.9, 1.2, 2.4, .05, 'Shape'),
    width: P('Board width (cells)', .55, .35, .8, .05, 'Shape'),
    wake: P('Dust and ripple strength', .7, 0, 1, .05, 'Feedback'),
    underglow: P('Glow under the board', .5, 0, 1, .05, 'Feedback'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Mount', t: 0 }, { name: 'Ride', t: t.ride0 }, { name: 'Step off', t: t.ride1 },
    { name: 'Send: board goes out', t: t.send0 }, { name: 'Lift patient', t: t.send1 },
    { name: 'Bring back', t: t.loaded }, { name: 'Set down', t: t.back1 },
    { name: 'Dismount, orbs return', t: t.unloaded }, { name: 'Result', t: t.home },
  ]; },
  events() { return []; },                     // friendly: no shake
  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a0 = p.aim * Mathf.Deg2Rad, ca = Math.cos(a0), sa = Math.sin(a0);
    // Travel frame: a along the travel direction, b across it, h cells up. cast() is the shadow.
    const place = (base, a, b, h = 0) => ({ x: base.x + a * ca - b * sa, z: base.z + a * sa + b * ca + h * Lift });
    const cast = (base, a, b, h = 0) => ({ x: base.x + a * ca - b * sa + sun.x * h, z: base.z + a * sa + b * ca + sun.z * h });
    const water = { from: -p.ride * .72, to: -p.ride * .3 };

    // The board at any time: position along the path, height, how much of it is out of the floor,
    // and which end is pointed. Everything that follows the board (rider, patient, wake) asks this.
    const boardAt = (at) => {
      const form = smooth((at - p.mount * .4) / (p.mount * .35)) * (1 - smooth((at - t.unloaded) / (Dismount * .45)));
      let a = -p.ride;
      if (at >= t.back1) a = 0;
      else if (at >= t.loaded) a = lerp(p.send, 0, glide((at - t.loaded) / t.sendDur));
      else if (at >= t.send1) a = p.send;
      else if (at >= t.send0) a = lerp(0, p.send, glide((at - t.send0) / t.sendDur));
      else if (at >= t.ride0) a = lerp(-p.ride, 0, glide((at - t.ride0) / t.rideDur));
      const lifted = smooth((at - p.mount * .7) / (p.mount * .3));
      const dip = smooth((at - t.send1 + t.sendDur * .3) / (t.sendDur * .3)) * (1 - smooth((at - t.send1) / p.load));
      const down = smooth((at - t.back1) / p.load);
      const up = lifted * (1 - Math.max(dip, down));
      const h = lerp(Low * lifted, p.hover, up) + Math.sin(at * 5.5) * .018 * up;
      return { a, h, form, up, flip: smooth((at - t.send1) / p.load) };
    };
    const B = boardAt(s);

    // --- floor: water strip, seams, wake ------------------------------------------------------
    if (p.walker) {
      const quad = (key, inset, colour, layer) => band(key,
        [place(o, water.from + inset, WalkerLane - 1 + inset), place(o, water.to - inset, WalkerLane - 1 + inset)],
        [place(o, water.from + inset, SageLane + .2 - inset), place(o, water.to - inset, SageLane + .2 - inset)], colour, layer);
      quad('board water', 0, new Color(.17, .29, .38, .85), Floor);
      quad('board water inner', .18, new Color(.22, .36, .46, .8), Floor + .001);
    }
    // The slit the board comes out of and goes back into.
    const seam = (key, a, age, life) => {
      if (age < 0 || age >= life) return;
      const f = Math.sin(age / life * Math.PI), half = p.length * .5 * clamp(age / life * 2.5);
      trail(key, [place(o, a - half, 0), place(o, a, 0), place(o, a + half, 0)].map(q => ({ x: q.x, z: q.z + FeetOffset })),
        .07, pale.withAlpha(f * .8), Floor + .02);
      sprite(place(o, a, 0, 0), p.length * 1.2, .7, Rim.withAlpha(f * .3), glow, Floor + .021, -p.aim);
    };
    seam('board seam up', -p.ride, s - p.mount * .25, p.mount * .75);
    seam('board seam down', 0, s - t.unloaded, Dismount * .7);

    // Wake: puffs left at fixed emission times, so scrubbing gives the same picture.
    if (p.wake > 0) {
      const newest = Math.floor(s / PuffGap);
      for (let j = newest; j > newest - Math.ceil(PuffLife / PuffGap) - 1; j--) {
        const born = j * PuffGap, age = s - born;
        if (born < 0 || age >= PuffLife) continue;
        const then = boardAt(born), before = boardAt(born - .03), v = (then.a - before.a) / .03;
        if (Math.abs(v) < .8 || then.form < .9) continue;
        const tail = then.a - Math.sign(v) * p.length * .42, f = 1 - age / PuffLife;
        const wet = p.walker && tail > water.from && tail < water.to;
        const pos = place(o, tail, (rand(j) - .5) * .3);
        pos.z += FeetOffset;
        const amount = p.wake * Math.min(1, Math.abs(v) / 4) * (.4 + .6 * then.up);
        if (wet) { if (j % 2 === 0) circle(pos, .12 + age * 1.3, f * .6 * amount, Floor + .012, ripple); }
        else sprite(pos, .3 + age * 1.5, .2 + age * .9, dustColour.withAlpha(f * .4 * amount), soft, shadowLayer + .02);
      }
    }

    // --- the board ------------------------------------------------------------------------------
    // Tail is blunt, nose is pointed; flip moves the point to the other end for the trip back.
    const outline = (u) => u < .4 ? lerp(.62, 1, smooth(u / .4)) : Math.max(0, 1 - ((u - .4) / .6) ** 2) ** .55;
    if (B.form > .001) {
      const L = p.length * B.form, W = p.width * (.4 + .6 * B.form), n = 22;
      const strip = (project, h, grow, inner, outer) => {
        const left = [], right = [];
        for (let j = 0; j <= n; j++) {
          const u = j / n, l = (u - .5) * (L + grow * 2), w = W / 2 * lerp(outline(u), outline(1 - u), B.flip) + grow;
          const q = project(o, B.a + l, inner(w), h), r = project(o, B.a + l, outer(w), h);
          left.push({ x: q.x, z: q.z + FeetOffset }); right.push({ x: r.x, z: r.z + FeetOffset });
        }
        return [left, right];
      };
      const whole = [w => -w, w => w];
      band('board shadow', ...strip(cast, B.h, 0, ...whole), Body.withAlpha(strength * .75), shadowLayer + .001);
      if (p.underglow > 0) {
        const under = place(o, B.a, 0);
        sprite({ x: under.x, z: under.z + FeetOffset }, L * 1.25, W * 2.4,
          Rim.withAlpha(p.underglow * .45 * B.up * B.form * (.85 + .15 * Math.sin(s * 9))), glow, Floor + .03, -p.aim);
      }
      band('board underside', ...strip(place, Math.max(0, B.h - Thickness), .028, ...whole), Color.Lerp(Body, Rim, .5), boardLayer);
      band('board edge', ...strip(place, B.h, .028, ...whole), Rim.withAlpha(.95), boardLayer + .002);
      band('board deck', ...strip(place, B.h, 0, ...whole), Body, boardLayer + .003);
      band('board facet', ...strip(place, B.h, 0, w => w * .22, w => w * .78), Color.Lerp(Body, Slate, .55), boardLayer + .004);
      const [spine] = strip(place, B.h, 0, () => .012, () => 0);
      trail('board spine', spine, .035, pale.withAlpha(.3 + .25 * B.up), boardLayer + .005);
      const nose = place(o, B.a + lerp(.5, -.5, B.flip) * L * .9, 0, B.h);
      sprite({ x: nose.x, z: nose.z + FeetOffset }, .3, .3, pale.withAlpha(.5 * B.up * B.form), glow, boardLayer + .006);
    }

    // --- pawns ----------------------------------------------------------------------------------
    const off = smooth((s - t.ride1) / StepOff);
    const riding = s < t.ride1;
    const sage = { a: riding ? B.a : 0, b: SageLane * off, h: riding ? B.h : lerp(p.hover, 0, off) + Math.sin(off * Math.PI) * .2 };
    const carried = s >= t.send1 && s < t.unloaded;
    const patient = { a: s < t.send1 ? p.send : B.a, h: carried ? Math.max(0, B.h - Low) : 0 };

    const shadowOf = (a, b, h, w = .85) => {
      const g = cast(o, a, b, h);
      sprite({ x: g.x + sun.x * .45, z: g.z + sun.z * .45 }, w, .4, Body.withAlpha(strength), soft, shadowLayer + .004);
    };
    const standing = (a, b, h, colour, bob = 0) => {
      const pos = place(o, a, b, h);
      shadowOf(a, b, h);
      draw(disc, pos.x, pawnLayer, pos.z + .18 + bob, .22, .32, 0, colour);
      draw(disc, pos.x, pawnLayer + .002, pos.z + .58 + bob, .16, .17, 0, skin);
    };
    // A downed pawn lies along the travel direction, so it lies along the board.
    const lying = (a, b, h, colour) => {
      const body = place(o, a + .08, b, h), head = place(o, a - .34, b, h);
      shadowOf(a, b, h, 1);
      draw(disc, body.x, pawnLayer, body.z, .34, .2, -p.aim, colour);
      draw(disc, head.x, pawnLayer + .002, head.z, .16, .16, 0, skin);
    };
    if (p.actors) {
      lying(patient.a, 0, patient.h, new Color(.55, .38, .27));
      standing(sage.a, sage.b, sage.h, new Color(.39, .58, .65));
      if (p.walker) {
        // Same start time as the ride. Piecewise constant speed: dry, water, dry.
        const start = -p.ride, end = -.2, legs = [[start, water.from, WalkSpeed], [water.from, water.to, WadeSpeed], [water.to, end, WalkSpeed]];
        let left = Math.max(0, s - t.ride0), a = start, moving = false, wading = false;
        for (const [from, to, speed] of legs) {
          const need = (to - from) / speed;
          if (left < need) { a = from + left * speed; moving = left > 0; wading = speed === WadeSpeed; break; }
          left -= need; a = to;
        }
        standing(a, WalkerLane, 0, new Color(.62, .45, .22), moving ? Math.abs(Math.sin(s * (wading ? 5 : 9))) * .05 : 0);
        if (wading) for (let k = 0; k < 2; k++) {
          const age = (s * 1.4 + k * .5) % 1;
          circle(place(o, a, WalkerLane), .15 + age * .5, (1 - age) * .5 * p.wake, Floor + .012, ripple);
        }
      }
    }

    // --- orbs -----------------------------------------------------------------------------------
    // Six slots on a level circle round the sage's head. Slots 4 and 5 are the board.
    const hub = place(o, sage.a, sage.b, sage.h + OrbitHeight);
    const slot = (i) => { const th = s * 1.1 + i * Math.PI / 3; return { x: hub.x + Math.cos(th) * OrbitRadius, z: hub.z + Math.sin(th) * OrbitRadius }; };
    for (let i = 0; i < 4; i++) orb(slot(i), OrbSize);
    for (const [k, i] of [[0, 4], [1, 5]]) {
      const side = k ? 1 : -1;
      // Out: fly down to the floor in front of and behind the feet, then sink.
      const fly = smooth(s / (p.mount * .35)), sink = smooth((s - p.mount * .35) / (p.mount * .15));
      if (sink < 1) {
        const from = slot(i), to = place(o, -p.ride + side * .5, 0, 0);
        to.z += FeetOffset;
        const arc = Math.sin(fly * Math.PI) * .25;
        orb({ x: lerp(from.x, to.x, fly), z: lerp(from.z, to.z, fly) + arc }, OrbSize * (1 - sink), 1, 1 - sink * .6);
      }
      // Back: come up out of the floor where the board sank and rejoin the circle.
      const rise = smooth((s - t.unloaded - Dismount * .4) / (Dismount * .2)), back = smooth((s - t.unloaded - Dismount * .55) / (Dismount * .45));
      if (rise > 0) {
        const from = place(o, side * .5, 0, 0), to = slot(i);
        from.z += FeetOffset;
        const arc = Math.sin(back * Math.PI) * .3;
        orb({ x: lerp(from.x, to.x, back), z: lerp(from.z, to.z, back) + arc }, OrbSize * rise, 1, .4 + .6 * rise);
      }
    }
  },
};
