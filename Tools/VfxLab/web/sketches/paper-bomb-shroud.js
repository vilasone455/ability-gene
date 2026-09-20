// Paper Shroud — Paper Bomb weapon proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed, none of it agreed; every number is a placeholder). The pawn holds a
// tag scroll. Paper Shroud targets one pawn within 6 tiles. Six tags leave the roll 0.09 s apart,
// curve through the air and stick flat onto the target. From the first tag landing the target
// cannot move for 2 s. Then a hand seal (0.35 s) sets all six off at once: 60 Bomb damage to the
// target, 20 to anything else within 0.9 cells, allies included. Costs 6 tags. Cooldown 45 s.
// Role: single-target burst that also pins. Tag Line is a prepared line charge; this is the
// answer to one dangerous enemy already in the fight.
//
// Drawing: a tag is a flat quad, so it looks the same for every aim and needs no per-facing
// method. In flight it flips (its width closes and opens) and follows its path's tangent; over the
// last 30 % it blends onto a fixed slot on the target's body. The slots are screen offsets on the
// stand-in pawn and must be re-made for the real pawn's draw size. The bystanders only show the
// 0.9 radius: the near one (0.78 cells) is hurt, the far one (1.7 cells) is not. The tag, burst
// and roll are in lib/paper-bomb.js.
import { Mathf } from '../js/engine.js';
import { P, Y, Floor, Lift, sprite, soft, trail } from './lib/six-paths-impact.js';
import { pawn, ringAt, stunStars, shadowLayer, pawnLayer, Ally, EnemyColour, Ink } from './lib/goku.js';
import { Paper, Char, Ember, Hot, Red, Seal, Burn, tag, roll, sealFlash, burst } from './lib/paper-bomb.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
// Decided shape and rule numbers.
const Sheets = 6, Wind = .35, HandOut = .35, HandHeight = .4, OnBody = .48;   // OnBody: tag scale once stuck (0.34 x 0.12 cells)
const Power = 1.5;                                                              // six tags at once: a bigger flash than one Tag Line tag
// Where each tag sticks on the stand-in pawn: [x, z, degrees] from its feet. Head, chest, belly, two sides, legs.
const Slots = [[0, .6, 8], [-.09, .36, -38], [.09, .22, 24], [-.13, .1, 68], [.13, .44, -62], [0, -.02, 4]];
const Bulge = [1.3, -1.1, .7, -1.6, 1.7, -.5];                                  // how far each path swings to the side

function times(p) {
  const firstLand = Wind + p.fly, lastLand = firstLand + (Sheets - 1) * p.stagger, burstAt = firstLand + p.bound;
  return { firstLand, lastLand, seal: burstAt - Burn - Seal, curl: burstAt - Burn, burstAt, end: burstAt + p.hold };
}

export default {
  kit: 'Paper Bomb', label: 'Paper Shroud (sketch)',
  params: {
    actors: { label: 'Show caster and target', value: true, group: 'Showcase' },
    bystanders: { label: 'Show bystanders at 0.78 and 1.7 cells', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Caster distance (cells)', 5, 2, 6, .5, 'Showcase'),
    radius: P('Blast radius (cells)', .9, .5, 2, .1, 'Rule'),
    bound: P('Target held before the burst', 2, 1, 4, .1, 'Rule'),
    fly: P('One tag flies for', .55, .3, 1, .05, 'Timing (s)'),
    stagger: P('Gap between tags', .09, .03, .25, .01, 'Timing (s)'),
    hold: P('Aftermath held', 2.2, .5, 5, .1, 'Timing (s)'),
    arc: P('Flight height (cells)', .9, .2, 1.8, .1, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Wind-up', t: 0 }, { name: 'Tags fly', t: Wind }, { name: 'Held', t: t.firstLand },
    { name: 'Hand seal', t: t.seal }, { name: 'Burst', t: t.burstAt },
  ]; },
  events(p) { return [{ t: times(p).burstAt, type: 'shake', value: .22 }]; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a);
    const place = (along, across, h = 0) => ({ x: o.x + along * ca - across * sa, z: o.z + along * sa + across * ca + h * Lift });
    const cast = (along, across, h = 0) => ({ x: o.x + along * ca - across * sa + sun.x * h, z: o.z + along * sa + across * ca + sun.z * h });
    const caster = place(-p.distance, 0), age = s - t.burstAt, gone = age >= 0;

    // The target and two bystanders walk toward the caster and the target stops when the first tag lands.
    const walk = 1.5 * (1 - smooth(s / t.firstLand)), held = s >= t.firstLand && !gone;
    const shake = held ? Math.sin(s * 43) * .018 : 0, victim = place(walk, 0), body = { x: victim.x + shake, z: victim.z };
    const landed = k => s >= Wind + k * p.stagger + p.fly, covered = Slots.filter((_, k) => landed(k)).length / Sheets;
    const lit = clamp((s - t.lastLand - .15) / Math.max(.2, t.curl - t.lastLand - .4));   // 0..1: how far the seals have lit, tag by tag

    // The rule's radius on the floor while the target is held, brighter as the burst nears.
    if (held) ringAt(o, p.radius, Red.withAlpha(.25 + .45 * lit * (.6 + .4 * Math.sin(s * 9))), Floor + .02);

    burst('shroud burst', o, age, p.radius, sun, strength, 911, Power);

    if (p.actors) {
      const release = smooth(s / Wind) * (1 - smooth((s - Wind - Sheets * p.stagger) / .3)), sealing = smooth((s - t.seal) / .12) * (1 - smooth((s - t.burstAt - .3) / .3));
      pawn(caster, Ally, sun, strength, { arms: sealing > 0 ? 2 : 1, raise: Math.max(sealing, release * .8) });
      roll(place(-p.distance + HandOut - .05, 0, HandHeight), p.aim, .34, s > Wind && s < Wind + Sheets * p.stagger ? Math.sin(s * 40) * .02 : 0);
      sealFlash('shroud seal', caster, sealing);
      if (!gone) pawn(body, EnemyColour, sun, strength, { tint: lit > 0 ? Ember : Paper, tintAmount: lit > 0 ? .25 + .35 * lit : .3 * covered });
      else pawn(place(.25 * smooth(age / .3), 0), EnemyColour, sun, strength, { lie: age > .1, tint: age < .25 ? Hot : Char, tintAmount: age < .25 ? 1 - age / .25 : .25 });
      if (held) stunStars('shroud held', body, s, smooth((s - t.firstLand) / .2));
      if (p.bystanders) {
        const knock = gone ? .35 * smooth(age / .25) : 0;
        pawn(place(walk + .78 + knock, 0), EnemyColour, sun, strength, { tint: Hot, tintAmount: gone ? clamp(1 - age / .4) : 0, outline: gone ? clamp(1 - age / .6) : 0 });
        pawn(place(walk + .4, 1.7), EnemyColour, sun, strength);
      }
    }

    // The six tags: in the air, then on the body, then curling just before the burst.
    if (gone) return;
    for (let k = 0; k < Sheets; k++) {
      const leave = Wind + k * p.stagger, u = clamp((s - leave) / p.fly);
      if (s < leave) continue;
      const [sx, sz, slotDeg] = Slots[k], slot = { x: body.x + sx, z: body.z + sz };
      // Path: from the hand, swinging out to one side and up, then blended onto the slot over the last 30 %.
      const path = v => { const e = 1 - (1 - v) * (1 - v), join = smooth((v - .7) / .3);
        const free = place(lerp(-p.distance + HandOut, walk, e), Bulge[k] * Math.sin(Math.PI * e) * (p.distance / 5), lerp(HandHeight, .3, e) + p.arc * Math.sin(Math.PI * e));
        return { x: lerp(free.x, slot.x, join), z: lerp(free.z, slot.z, join) }; };
      if (u < 1) {
        const at = path(u), ahead = path(Math.min(1, u + .04)), join = smooth((u - .7) / .3), e = 1 - (1 - u) * (1 - u);
        const deg = lerp(Math.atan2(ahead.z - at.z, ahead.x - at.x) * 57.29578, slotDeg, join), flip = 1 - .78 * Math.abs(Math.sin(s * 17 + k * 1.3)) * (1 - join);
        const h = (lerp(HandHeight, .3, e) + p.arc * Math.sin(Math.PI * e)) * (1 - join), ground = cast(lerp(-p.distance + HandOut, walk, e), Bulge[k] * Math.sin(Math.PI * e) * (p.distance / 5), h);
        sprite(ground, .3, .14, Ink.withAlpha(strength * .8), soft, shadowLayer);
        trail(`shroud trail ${k}`, [0, 1, 2, 3, 4].map(j => path(Math.max(0, u - .22 + j * .055))), .1, Paper.withAlpha(.3 * (1 - join)), Y + .04);
        tag(at, deg, { long: lerp(.62, OnBody, join), wide: .62 * flip, layer: Y + .05 });
        continue;
      }
      // Stuck. A slap: the tag lands 25 % large and settles in 0.08 s. Seals light one after another.
      const since = s - leave - p.fly, slap = 1 + .25 * (1 - clamp(since / .08)), heat = clamp(lit * Sheets - k), curl = clamp((s - t.curl) / Burn);
      if (since < .12) ringAt(slot, .08 + since * 1.6, Paper.withAlpha(.7 * (1 - since / .12)), pawnLayer + .03);
      tag(slot, slotDeg, { long: OnBody * slap, wide: .55 * slap, heat, curl, armed: heat > 0 ? 0 : .5 + .5 * Math.sin(s * 5 + k), layer: pawnLayer + .012 + k * .005, glowLayer: Y + .02 });
    }
  },
};
