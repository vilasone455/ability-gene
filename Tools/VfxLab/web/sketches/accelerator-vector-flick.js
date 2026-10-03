// Vector Flick — the kit's plain damage ability: cheap, quick, on a short cooldown. Ported to C#
// 2026-09-29 (Source/RimArt/Accelerator/VectorFlick*.cs, previews "Accelerator: vector flick: ..."),
// played in game by MapComponent_Flicks (Accelerator/Kit/FlickCast.cs); the warm-up is drawn from his
// aiming stance and the leg is the drawn quad, not a clip. Decided at the port: it always hits the
// target and pawns in between are not hit. Numbers are AG_VectorFlick's XML.
//
// What it is for (agreed 2026-09-27; every number is a placeholder for XML).
//   Target a pawn within 24.9 cells in line of sight. Warm-up 0.3 s: Accelerator scuffs the floor
//   and flicks a pebble up to knee height, then kicks it. The kick rewrites its vector and it
//   leaves at bullet speed (42 cells/s here, a rifle round) and hits the target for 14 blunt, 30 %
//   armour penetration. No stun, no knockback beyond a flinch, no strain, 0 Echo charge, cooldown
//   2 s. No item is used: the pebble comes out of the floor.
//   Not decided: whether it can miss like a bullet or always hits, and whether a pawn standing in
//   the line catches it. The sketch shows a hit on the target.
//   Source: Index season 1 episode 13, Accelerator kicks a small stone at Touma and it wrecks a
//   steel tower. The sketch keeps the kick, the stone and the speed; the hit is sized for 14 blunt,
//   not for the tower, so the picture does not promise more damage than the ability does.
//
// Order, one raider (the default):
//   0.00  stand; a raider 9 cells out along the aim
//   0.30  warm-up: a small white ring with a black edge and dust at his foot; a pebble pops out of
//         the floor and rises to knee height in 0.15 s, turning; a small pit stays where it came
//         out; the leg draws back
//   0.54  a white ring with a black edge closes round the pebble (his grip on its vector)
//   0.60  kick: the leg swings through the pebble. A flash, a ring out, five air streaks fanned
//         forward, floor dust blown out round the foot, a small camera shake
//   0.60  flight at 42 cells/s: the pebble turns white-edged (his colours from here on), a white
//         trail with black edges 3 cells long, two cone lines at the head, a ground shadow, and a
//         faint line left along the whole path
//   0.80  hit on the raider's chest: a flash, a black-edged ring, 6 sparks thrown on, camera shake.
//         The raider goes white for 0.15 s and flinches 0.2 cells back, then stands again. The
//         pebble breaks into 4 bits that fly on and land 0.5-1.3 cells behind him and stay. The
//         path line fades over 0.4 s.
//   "three raiders, 2 s cooldown": the same kick at three raiders, one warm-up plus 2 s apart, a
//   new pebble out of the floor each time.
//
// Palette: monochrome, see lib/accelerator.js. White light with a black edge on what he controls
// (the grip ring, the pebble in flight, its trail); the pebble is plain stone before the kick.
// Drawing: the flight is a line and the rings are level circles, so it turns with the aim and needs
// no per-facing method. The leg is one quad from the hip to a shoe disc, drawn behind the body while
// the shoe is north of the hip; it stands in for a drawn leg or a Melee Animation clip, decided at
// the port.
// The pebble before the kick is drawn behind him too when he kicks north. Pawns are stand-ins.
import { Color, Mathf, MeshPool, Meshes } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import { pawn, rock, ringAt, line, streak, whiteGlow, EnemyColour, Ink, White, Dust, Lift, Chest, pawnLayer, clamp, smooth } from './lib/goku.js';
import { accelerator, Edge } from './lib/accelerator.js';

const D2R = Mathf.Deg2Rad;
const disc = Meshes.disc(24, 'vector flick disc');
// Decided values. The panel keeps only what is still being tuned.
const Lead = .3, Pop = .15, Cooldown = 2, Tail = 1.5, MaxRange = 24.9;
const Foot = .35, Knee = .35, Apex = .05, ChestUp = Chest / Lift, TrailCells = 3, Bits = 4, Pebble = .16;
const Pants = new Color(.34, .34, .37), Shoe = new Color(.07, .07, .08);
// The raiders for the three-kick scenario: [degrees off the aim, share of the distance].
const Spread = [[0, 1], [30, .75], [-24, .8]];

const three = p => p.scenario === 'three raiders, 2 s cooldown';
function times(p) {
  const kicks = (three(p) ? Spread : Spread.slice(0, 1)).map(([off, share], k) => {
    const warm = Lead + k * (p.warm + Cooldown), kick = warm + p.warm, dist = Math.min(MaxRange, Math.max(1.5, p.cells * share));
    return { k, deg: p.aim + off, dist, warm, kick, hit: kick + (dist - Foot) / p.speed };
  });
  return { kicks, end: kicks[kicks.length - 1].hit + Tail };
}

// Piecewise smooth path through keys [t, ...values].
function keyed(keys, t) {
  if (t <= keys[0][0]) return keys[0].slice(1);
  for (let i = 1; i < keys.length; i++) {
    const [t1, ...b] = keys[i], [t0, ...a] = keys[i - 1];
    if (t <= t1) { const u = smooth((t - t0) / (t1 - t0)); return a.map((v, j) => v + (b[j] - v) * u); }
  }
  return keys[keys.length - 1].slice(1);
}

// The kicking leg: how far the shoe is along the aim (negative is behind him) and how high.
function legPose(age, warm) {
  return keyed([[0, 0, 0], [Pop * .5, .3, .03], [Pop, .22, .1], [warm - .04, -.3, .16], [warm, Foot, .36], [warm + .05, .62, .44], [warm + .14, .6, .42], [warm + .34, .05, 0]], age);
}
function leg(pos, deg, reach, lift, alpha, layer) {
  if (alpha <= 0) return;
  const r = deg * D2R, hip = { x: pos.x, z: pos.z + .1 }, foot = { x: pos.x + Math.cos(r) * reach, z: pos.z + Math.sin(r) * reach + lift * Lift };
  const dx = foot.x - hip.x, dz = foot.z - hip.z, len = Math.hypot(dx, dz);
  if (len > .03) draw(MeshPool.plane10, (hip.x + foot.x) / 2, layer, (hip.z + foot.z) / 2, .11, len, 90 - Math.atan2(dz, dx) / D2R, Pants.withAlpha(alpha));
  draw(disc, foot.x, layer + .001, foot.z, .08, .065, 0, Shoe.withAlpha(alpha));
}

export default {
  kit: 'Accelerator', label: 'Vector Flick (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'one raider', options: ['one raider', 'three raiders, 2 s cooldown'], group: 'Showcase' },
    aim: P('Aim (degrees, 0 east, 90 north)', 15, 0, 355, 5, 'Showcase'),
    cells: P('Raider distance (cells; range 24.9)', 9, 2, 24.9, .1, 'Showcase'),
    speed: P('Pebble speed (cells/s; a rifle round is 42)', 42, 10, 90, 1, 'Timing'),
    warm: P('Warm-up (s)', .3, .2, .6, .05, 'Timing'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const { kicks } = times(p), one = kicks.length === 1;
    return [{ name: 'Stand', t: 0 }, ...kicks.flatMap(k => {
      const n = one ? '' : ` ${k.k + 1}`;
      return [{ name: `Warm-up${n}`, t: k.warm }, { name: `Kick${n}`, t: k.kick }, { name: `Hit${n}`, t: k.hit }];
    })];
  },
  events(p) { return times(p).kicks.flatMap(k => [{ t: k.kick, type: 'shake', value: .02 }, { t: k.hit, type: 'shake', value: .03 }]); },

  draw(s, p, { origin: o, scene }) {
    const { kicks, end } = times(p);
    if (s < 0 || s >= end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a0 = p.aim * D2R, A = { x: o.x - Math.cos(a0) * p.cells / 2, z: o.z - Math.sin(a0) * p.cells / 2 };
    // A point d cells from Accelerator along deg, h cells up (drawn as a shift north).
    const from = (deg, d, h = 0, across = 0) => { const r = deg * D2R; return { x: A.x + Math.cos(r) * d - Math.sin(r) * across, z: A.z + Math.sin(r) * d + Math.cos(r) * across + h * Lift }; };
    const current = kicks.filter(k => s >= k.warm).pop();

    // --- floor: the pits the pebbles came out of, the scuff and kick dust, the bits that landed -----------------------
    kicks.forEach(k => {
      if (s < k.warm) return;
      const pit = from(k.deg, Foot), age = s - k.warm;
      sprite(pit, .2, .15, Ink.withAlpha(.5 * smooth(age / .1)), soft, Floor + .01);
      if (age < .25) { const u = age / .25; ringAt(pit, .1 + smooth(u) * .3, Edge.withAlpha(.8 * (1 - u)), Floor + .02); ringAt(pit, .08 + smooth(u) * .3, White.withAlpha(.9 * (1 - u)), Floor + .021); }
      if (age < .4) for (let i = 0; i < 5; i++) {
        const u = clamp((age - rand(i + k.k * 7) * .05) / .35), ang = i * 1.26 + rand(i + 3), d = .08 + u * .25;
        sprite({ x: pit.x + Math.cos(ang) * d, z: pit.z + Math.sin(ang) * d * .7 + u * .06 }, .14 + u * .16, .11 + u * .12, Dust.withAlpha(.45 * Math.sin(u * Math.PI)), soft, Floor + .03);
      }
      const kickAge = s - k.kick;
      if (kickAge >= 0 && kickAge < .6) for (let i = 0; i < 9; i++) {
        const u = clamp((kickAge - rand(i + 20) * .06) / .5), ang = k.deg * D2R + (i - 4) * .45 + (rand(i + 30) - .5) * .3, d = .2 + u * (.5 + rand(i + 40) * .5);
        sprite({ x: pit.x + Math.cos(ang) * d, z: pit.z + Math.sin(ang) * d + u * .08 }, .22 + u * .35, .18 + u * .28, Dust.withAlpha(.4 * Math.sin(u * Math.PI)), soft, Floor + .04);
      }
      const hitAge = s - k.hit;
      if (hitAge >= 0) for (let i = 0; i < Bits; i++) {
        const u = clamp(hitAge / (.22 + rand(i + k.k * 5) * .12)), ang = k.deg * D2R + (rand(i + 50 + k.k) - .5) * 1.4, d = .5 + rand(i + 60 + k.k) * .8;
        const r = k.deg * D2R, cx = Math.cos(r) * k.dist + Math.cos(ang) * d * u, cz = Math.sin(r) * k.dist + Math.sin(ang) * d * u;
        const h = ChestUp * (1 - u) + .25 * Math.sin(u * Math.PI), at = { x: A.x + cx, z: A.z + cz + h * Lift };
        if (u < 1) sprite({ x: A.x + cx + sun.x * h, z: A.z + cz + sun.z * h }, .1, .06, Ink.withAlpha(.35), soft, Floor + .05);
        rock(at, .09, rand(i + 70) * 360 + hitAge * 900 * (1 - u), 1, 2 * (i % 3), u < 1 ? Y + .05 : Floor + .06);
      }
    });

    // --- the pawns, north first -----------------------------------------------------------------------------------
    const figures = [{ pos: A, me: true }];
    kicks.forEach(k => {
      const age = s - k.hit, flinch = age < 0 ? 0 : age < .06 ? smooth(age / .06) : 1 - smooth((age - .06) / .35);
      figures.push({ pos: from(k.deg, k.dist + .2 * flinch), flash: age >= 0 ? 1 - clamp(age / .15) : 0 });
    });
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      if (!g.me) { pawn(g.pos, EnemyColour, sun, strength, { tint: White, tintAmount: .9 * g.flash }); return; }
      const k = current, age = k ? s - k.warm : -1;
      const show = k && age < p.warm + .4 ? 1 - smooth((age - p.warm - .3) / .1) : 0;
      if (show > 0) {
        // Behind the body whenever the shoe is north of the hip: kicking north, or drawn back while kicking south.
        const [reach, lift] = legPose(age, p.warm), behind = Math.sin(k.deg * D2R) * reach > .05;
        if (behind) leg(g.pos, k.deg, reach, lift, show, pawnLayer - .012);
        accelerator(g.pos, sun, strength);
        if (!behind) leg(g.pos, k.deg, reach, lift, show, pawnLayer + .012);
        return;
      }
      accelerator(g.pos, sun, strength);
    });

    // --- each pebble: the pop, the grip, the kick, the flight, the hit ------------------------------------------------
    kicks.forEach(k => {
      if (s < k.warm || s >= k.hit + .6) return;
      const r = k.deg * D2R, behind = Math.sin(r) > .5, age = s - k.warm;
      const startH = Knee + Apex, run = k.dist - Foot, flown = s < k.kick ? 0 : Math.min(run, (s - k.kick) * p.speed);
      const heightAt = d => startH + (ChestUp - startH) * clamp(d / run), key = `vector flick ${k.k}`;

      if (s < k.kick) {
        // Out of the floor to knee height, still rising slowly until the foot meets it. Plain stone.
        const h = Knee * smooth(age / Pop) + Apex * clamp((age - Pop) / (p.warm - Pop)), at = from(k.deg, Foot, h), ground = from(k.deg, Foot);
        sprite({ x: ground.x + sun.x * h, z: ground.z + sun.z * h }, .16, .1, Ink.withAlpha(.4 * clamp(age / .05)), soft, Floor + .05);
        rock(at, Pebble, age * 400, 1, 4, behind ? pawnLayer - .016 : Y + .102);
        // The grip: a ring closing onto the pebble in the last 0.06 s.
        const g = clamp((s - (k.kick - .06)) / .06);
        if (g > 0) { ringAt(at, .5 - .38 * g, Edge.withAlpha(.9 * g), Y + .19); ringAt(at, .47 - .38 * g, White.withAlpha(.95 * g), Y + .191); }
        return;
      }

      // The kick: flash, ring out, air streaks fanned forward from the contact point.
      const K = from(k.deg, Foot, startH), kAge = s - k.kick;
      if (kAge < .3) {
        const u = kAge / .3;
        sprite(K, .7 * (1 - u) + .2, .7 * (1 - u) + .2, White.withAlpha(.95 * (1 - u)), glow, Y + .2);
        ringAt(K, .14 + smooth(u) * .8, Edge.withAlpha(.85 * (1 - u)), Y + .19);
        ringAt(K, .11 + smooth(u) * .8, White.withAlpha(.9 * (1 - u)), Y + .191);
        for (let i = 0; i < 5; i++) {
          const ang = r + (i - 2) * .32, d0 = .15 + u * .3, d1 = d0 + .35 + u * .6;
          streak(`${key} air ${i}`, { x: K.x + Math.cos(ang) * d0, z: K.z + Math.sin(ang) * d0 }, { x: K.x + Math.cos(ang) * d1, z: K.z + Math.sin(ang) * d1 }, .05, White.withAlpha(.9 * (1 - u)), whiteGlow, Y + .2, 3);
        }
      }

      // The path line left in the air: faint, the whole way flown, gone 0.4 s after the hit.
      const pathFade = s < k.hit ? 1 : 1 - clamp((s - k.hit) / .4);
      if (flown > .05 && pathFade > 0) line(`${key} path`, [from(k.deg, Foot + flown, heightAt(flown)), K], .035, White.withAlpha(.35 * pathFade), whiteGlow, Y + .08, 'none');
      if (s >= k.hit) return;

      // In flight: trail, cone, the pebble in his colours, and its shadow.
      const along = Foot + flown, h = heightAt(flown), head = from(k.deg, along, h), back = Math.max(0, flown - TrailCells);
      const trail = [head, from(k.deg, Foot + (flown + back) / 2, heightAt((flown + back) / 2)), from(k.deg, Foot + back, heightAt(back))];
      line(`${key} trail edge`, trail, .11, Edge.withAlpha(.75), undefined, Y + .09);
      line(`${key} trail`, trail, .055, White.withAlpha(.95), whiteGlow, Y + .091);
      const cone = Math.min(.55, flown * .6);
      if (cone > .05) [1, -1].forEach(side => {
        const ang = r + Math.PI + side * .32;
        streak(`${key} cone ${side}`, { x: head.x + Math.cos(ang) * .1, z: head.z + Math.sin(ang) * .1 }, { x: head.x + Math.cos(ang) * (.1 + cone), z: head.z + Math.sin(ang) * (.1 + cone) }, .04, White.withAlpha(.85), whiteGlow, Y + .092, 3);
      });
      const ground = from(k.deg, along);
      sprite({ x: ground.x + sun.x * h, z: ground.z + sun.z * h }, .16, .1, Ink.withAlpha(.35), soft, Floor + .05);
      sprite(head, .45, .45, White.withAlpha(.8), glow, Y + .1);
      ringAt(head, .1, Edge.withAlpha(.9), Y + .101);
      rock(head, Pebble * .9, kAge * 2400, 1, 4, Y + .102);
    });

    // --- the hits ----------------------------------------------------------------------------------------------------
    kicks.forEach(k => {
      const age = s - k.hit;
      if (age < 0 || age > .5) return;
      const u = age / .5, r = k.deg * D2R, C = from(k.deg, k.dist, ChestUp);
      sprite(C, .8 * (1 - u) + .2, .8 * (1 - u) + .2, White.withAlpha(.95 * (1 - u)), glow, Y + .2);
      ringAt(C, .15 + smooth(u) * .6, Edge.withAlpha(.85 * (1 - u)), Y + .19);
      ringAt(C, .12 + smooth(u) * .6, White.withAlpha(.9 * (1 - u)), Y + .191);
      for (let i = 0; i < 6; i++) {
        const ang = r + (rand(i + 80 + k.k) - .5) * 2, d0 = .1 + u * .3, d1 = d0 + (.3 + .5 * rand(i + 90)) * (1 - u * .4);
        streak(`vector flick spark ${k.k} ${i}`, { x: C.x + Math.cos(ang) * d0, z: C.z + Math.sin(ang) * d0 }, { x: C.x + Math.cos(ang) * d1, z: C.z + Math.sin(ang) * d1 }, .045, White.withAlpha(1 - u), whiteGlow, Y + .2, 3);
      }
    });
  },
};
