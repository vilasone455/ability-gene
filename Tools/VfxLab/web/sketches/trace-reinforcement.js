// Reinforcement — Trace kit proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (agreed 2026-09-23 as the kit's second ability; every number is a placeholder and
// will be an XML field). The kit is three abilities, Shirou's three magics in the order he learns
// them: Reinforcement, Trace On, Unlimited Blade Works.
//   Self only, no target. 0.5 s to cast. For 20 s the pawn moves 30 % faster and its melee attacks
//   deal 40 % more damage. Cooldown 45 s. A pawn that gave up guns and psycasts has to cross open
//   ground under fire and win the fight at the end of it; this is for that run and that fight. It
//   reinforces whatever the pawn holds, usually the copy Trace On made. UBW's commands are separate
//   and nothing here copies one.
//
// Order, with the default timings:
//   0.00  a drafted pawn holds a traced longsword; a raider stands 7 cells away.
//   0.20  cast: a teal ring opens at its feet; circuit lines run out from the chest along the arms
//         and legs; the line reaches the hand and climbs the held weapon's outline from the grip to
//         the point behind a bright line; a glint at the point at 0.70. The body's lines fade by
//         1.10; the weapon's edge keeps a pulsing teal glow for the whole buff.
//   0.80  it runs at the raider at 1.3 × the normal speed, leaving teal footprints that fade and
//         faint streaks at its heels. The grey pawn ("Grey pawn at normal speed") starts from the
//         same place at the normal 4.6 cells/s and arrives 0.31 s later; it is a comparison, not
//         part of the effect.
//   1.97  it strikes twice, 0.9 s apart: a lunge, the weapon swings at him, a teal slash and a
//         spark; the raider flinches and gives a little ground.
//   3.60  the buff ends (20 s in game): the body's lines flash back and run into the chest, and the
//         weapon's glow fades.
//
// Drawing: circuit lines are thin additive lines with a soft halo, square traces along the body's
// edges with small bright nodes, drawn over the pawn; they mirror when it faces west. They show only
// while casting and ending: a pattern left on the body for 20 s read as a skeleton at game zoom, so
// the lasting signs are the weapon's glow (damage) and the footprints (speed). The stand-in looks
// the same from every side and the game's body does not, so the port needs a pattern per facing
// (front and back wide, side narrow). The glowing edge
// is the weapon's own outline texture (lib/trace.js, RimArt/TraceTrial/<Name>Outline), cut along
// the blade's axis for the climb. The held weapon lies flat at the hand at a fixed angle, a stand-in
// for the game's carry pose. Pawns are stand-ins.
import { Color } from '../js/engine.js';
import { P, Floor, sprite, glow } from './lib/six-paths-impact.js';
import { pawn, ringAt, line, glint, whiteGlow, pawnLayer, EnemyColour, White } from './lib/goku.js';
import {
  smooth, clamp, lerp, D2R, Trace, TraceHot, Weapons, plus, onScreen, blade, clip, texPoly, Square, along, partial, Circuit, CircuitTravel, heldCopy,
} from './lib/trace.js';

const Caster = new Color(.55, .3, .24), Grey = new Color(.62, .62, .64), Hurt = new Color(1, .25, .2);

// The rule's fixed numbers and decided looks. Speed is RimWorld's base walking speed in cells/s.
const Speed = 4.6, Boost = 1.3, Reach = .9, CastAt = .2, Step = .16, Print = .45, HitGap = .9, Swing = .14, Idle = .7;
const Headings = { east: { x: 1, z: 0 }, west: { x: -1, z: 0 }, north: { x: 0, z: 1 }, south: { x: 0, z: -1 } };
// Every phase boundary in one place.
function times(p) {
  const run = CastAt + p.cast + .1, path = p.distance - Reach, arrive = run + path / (Speed * Boost);
  const hits = [arrive + .15, arrive + .15 + HitGap], end = Math.max(p.endAt, hits[1] + .4);
  return { run, path, arrive, normal: run + path / Speed, hits, end, over: end + .8,
    lines: .55 * p.cast, climb: CastAt + .45 * p.cast, climbTime: .55 * p.cast, lit: CastAt + p.cast };
}

export default {
  kit: 'Trace', label: 'Reinforcement (sketch)',
  params: {
    run: { label: 'Runs toward', value: 'east', options: Object.keys(Headings), group: 'Scene' },
    weapon: { label: 'Held copy', value: 'LongSword', options: Object.keys(Weapons), group: 'Scene' },
    compare: { label: 'Grey pawn at normal speed', value: true, group: 'Scene' },
    distance: P('Run (cells)', 7, 3, 12, .5, 'Scene'),
    cast: P('Cast', .5, .2, 1.5, .05, 'Timing (s)'),
    endAt: P('Buff ends at (20 s in game)', 3.6, 2, 8, .1, 'Timing (s)'),
    size: P('Weapon size (x image)', 1.1, .7, 1.8, .05, 'Look'),
    bright: P('Circuit line brightness', .9, .2, 1.5, .05, 'Look'),
    width: P('Circuit line width (cells)', .022, .01, .05, .002, 'Look'),
  },
  duration(p) { return times(p).over; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Cast', t: CastAt }, { name: 'Run', t: t.run }, { name: 'Hits', t: t.hits[0] }, { name: 'Ends', t: t.end }];
  },
  events() { return []; },

  draw(s, p, { origin: cell, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.over) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const dir = Headings[p.run], perp = { x: -dir.z, z: dir.x }, side = p.run === 'west' ? -1 : 1, w = Weapons[p.weapon];
    const c = { x: cell.x, z: cell.z + 2 }, start = { x: c.x - dir.x * p.distance / 2, z: c.z - dir.z * p.distance / 2 };
    const foeAt = { x: start.x + dir.x * p.distance, z: start.z + dir.z * p.distance };
    const walked = (speed, x) => { const d = Math.min(t.path, Math.max(0, x - t.run) * speed); return { x: start.x + dir.x * d, z: start.z + dir.z * d }; };

    // Where the pawn is: running, then lunging at each hit.
    const swingAge = t.hits.map(h => s - h).find(a => a >= 0 && a < Swing) ?? -1, lunge = swingAge >= 0 ? .14 * Math.sin(swingAge / Swing * Math.PI) : 0;
    const base = walked(Speed * Boost, s), me = { x: base.x + dir.x * lunge, z: base.z + dir.z * lunge };
    const on = s >= CastAt && s < t.end + .4;

    // Cast: a ring at the feet and a flash at the chest.
    const castAge = s - CastAt;
    if (castAge >= 0 && castAge < .45) {
      const u = castAge / .45;
      ringAt(me, .25 + .8 * smooth(u), Trace.withAlpha(.7 * (1 - u)), Floor + .01, false, whiteGlow);
      sprite({ x: me.x, z: me.z + .36 }, .6, .6, Trace.withAlpha(.35 * Math.sin(u * Math.PI)), glow, pawnLayer + .005);
    }

    // The circuit lines: they run out from the chest while casting and fade once the weapon is lit;
    // when the buff ends they come back and run into the chest.
    const growing = s >= CastAt && s < t.lit + .4, ending = s >= t.end && s < t.end + .4;
    if (growing || ending) {
      const reach = growing ? CircuitTravel * clamp(castAge / t.lines) : CircuitTravel * (1 - smooth((s - t.end) / .4));
      const level = p.bright * (growing ? 1 - smooth((s - t.lit) / .4) : .8);
      const toWorld = q => ({ x: me.x + q[0] * side, z: me.z + q[1] });
      Circuit.forEach((run, k) => {
        const shown = Math.min(run.length, reach - run.from);
        if (shown <= .004) return;
        const pts = partial(run.pts, shown).map(toWorld);
        line(`reinforce circuit halo ${k}`, pts, p.width * 3.5, Trace.withAlpha(.2 * level), whiteGlow, pawnLayer + .006, 'none');
        line(`reinforce circuit ${k}`, pts, p.width, TraceHot.withAlpha(.9 * level), whiteGlow, pawnLayer + .007, 'none');
        pts.slice(1, shown < run.length ? -1 : undefined).forEach(q => sprite(q, .05, .05, TraceHot.withAlpha(.8 * level), glow, pawnLayer + .008));
        if (shown < run.length) sprite(pts[pts.length - 1], .09, .09, White.withAlpha(.9 * level), glow, pawnLayer + .008);
      });
    }

    // Running reinforced: footprints that glow and fade, and faint streaks at the heels.
    if (s >= t.run) for (let k = 0; t.run + k * Step < Math.min(s, t.arrive); k++) {
      const at = t.run + k * Step, age = s - at;
      if (age >= Print) continue;
      const q = walked(Speed * Boost, at), off = k % 2 ? .07 : -.07;
      sprite({ x: q.x + perp.x * off, z: q.z + perp.z * off }, .2, .12, Trace.withAlpha(.75 * (1 - age / Print)), glow, Floor + .005, -Math.atan2(dir.z, dir.x) / D2R);
    }
    if (s >= t.run && s < t.arrive) [1, -1].forEach(k => line(`reinforce streak ${k}`,
      [{ x: me.x - dir.x * .45 + perp.x * .06 * k, z: me.z - dir.z * .45 + perp.z * .06 * k + .05 }, { x: me.x + perp.x * .06 * k, z: me.z + perp.z * .06 * k + .05 }],
      .03, Trace.withAlpha(.4), whiteGlow, Floor + .006, 'both'));

    // The held copy: it swings at the raider on each hit. Its outline lights from the grip to the
    // point while casting, then keeps a pulsing glow until the buff ends.
    const toFoe = Math.atan2(foeAt.z - me.z, foeAt.x - me.x) / D2R, rest = side > 0 ? 55 : 125;
    const turn = ((toFoe - rest + 540) % 360) - 180, angle = rest + (swingAge >= 0 ? turn * Math.sin(swingAge / Swing * Math.PI) : 0);
    const b = heldCopy(w, p.size, me, angle, side);
    blade('reinforce weapon', b, sun, strength, pawnLayer + .012, { upright: false });
    const climb = clamp((s - t.climb) / t.climbTime), fade = s < t.end ? 1 : 1 - smooth((s - t.end) / .3);
    if (climb > 0 && fade > 0) {
      const edge = w.length * (1 - climb), level = lerp(1, Idle * (.75 + .25 * Math.sin(s * 7.5)), smooth((s - t.lit) / .3)) * fade;
      texPoly('reinforce weapon glow', clip(Square, q => along(w, q) - edge), b, onScreen, w.wire, Trace.withAlpha(.95 * p.bright * level), pawnLayer + .014);
      // a soft light along the lit part of the blade, so the glow still reads at game zoom
      const point = onScreen(b.tip), pommel = onScreen(plus(b.tip, b.A, b.L)), lit = { x: lerp(pommel.x, point.x, climb / 2), z: lerp(pommel.z, point.z, climb / 2) };
      const long = Math.hypot(point.x - pommel.x, point.z - pommel.z) * climb + .2;
      sprite(lit, long, .24, Trace.withAlpha(.3 * p.bright * level), glow, pawnLayer + .0135, -Math.atan2(point.z - pommel.z, point.x - pommel.x) / D2R);
      if (climb < 1) texPoly('reinforce weapon scan', clip(clip(Square, q => along(w, q) - (edge - .012)), q => edge + .02 - along(w, q)), b, onScreen, w.mask, TraceHot.withAlpha(.85), pawnLayer + .015);
    }
    const tipAge = s - t.lit;
    if (tipAge >= 0 && tipAge < .25) glint('reinforce tip', onScreen(b.tip), .3, 1 - tipAge / .25, TraceHot);

    // The hits: a teal slash across the raider and a spark.
    const knocked = t.hits.filter(h => s >= h).length * .08;
    const foe = { x: foeAt.x + dir.x * knocked, z: foeAt.z + dir.z * knocked };
    t.hits.forEach((h, i) => {
      const age = s - h;
      if (age < 0 || age >= .3) return;
      // a diagonal cut across the body, bowed a little, drawn in over the first third of its life;
      // the second hit cuts the other way
      const u = age / .3, k = i % 2 ? -1 : 1, from = { x: foe.x - .26 * k, z: foe.z + .56 }, to = { x: foe.x + .26 * k, z: foe.z + .04 }, arc = [];
      for (let j = 0; j <= 8 && j / 8 <= Math.min(1, u / .35); j++) {
        const v = j / 8, bow = Math.sin(v * Math.PI) * .07;
        arc.push({ x: lerp(from.x, to.x, v) + bow * k, z: lerp(from.z, to.z, v) + bow });
      }
      if (arc.length > 1) {
        line(`reinforce slash halo ${i}`, arc, .16, Trace.withAlpha(.35 * (1 - u)), whiteGlow, pawnLayer + .02, 'both');
        line(`reinforce slash ${i}`, arc, .05, White.withAlpha(1 - u), whiteGlow, pawnLayer + .021, 'both');
      }
      if (age < .15) glint(`reinforce spark ${i}`, { x: foe.x - dir.x * .12, z: foe.z + .32 }, .3, 1 - age / .15, White);
    });

    // Stand-ins: the pawn, the raider, and the grey pawn at the normal speed.
    pawn(me, Caster, sun, strength, { hair: true });
    const flinch = t.hits.some(h => s >= h && s < h + .18);
    pawn(foe, EnemyColour, sun, strength, { tint: Hurt, tintAmount: flinch ? .6 : 0 });
    if (p.compare && s >= t.run - .3) {
      const alpha = .3 * smooth((s - t.run + .3) / .3) * (1 - smooth((s - t.normal - .3) / .3));
      if (alpha > 0) pawn(walked(Speed, s), Grey, sun, strength, { alpha });
    }
  },
};
