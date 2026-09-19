// Guiding Thunder — technique proposal for the kunai belt, not the game. Nothing in Source/RimArt
// draws this yet. The defensive ability of the Flying Thunder God kit.
//
// What it is for (proposed, none of it agreed; every number is a placeholder). The caster picks one
// of their own kunai as the exit: one stuck in a pawn, or one lying on the ground. Range to that
// kunai 29.9 cells, no line of sight. For 6 s a barrier of script stands round the caster, 1.3
// cells out. Every projectile that would hit the caster is taken at the barrier and comes out at
// the kunai instead:
//   Kunai in a pawn: the shot hits that pawn, with its own damage. Shots that pawn fired count too.
//   Kunai on the ground: the shot lands on that cell. It hits whoever stands there, or the floor.
//   Explosive shells go off at the kunai.
// The caster stands still and cannot attack while it holds. It ends early after 12 shots. Melee is
// not stopped. Cooldown 45 s. It differs from the Six Paths Umbrella and Shield, which block: this
// sends the shot somewhere, so where the kunai was put beforehand is the whole skill of it.
//
// Order, with the default timings (the hold is shortened to 3.2 s so the clip stays short):
//   0.00  stand; two enemies are shooting; one has the caster's kunai in it (or it lies near them)
//   0.40  a ring of script is written round the caster over 0.25 s, framed by two thin rings; the
//         seal on the kunai lights and 4 corner brackets mark the cell the shots will come out at
//   0.65  barrier up. Each shot that reaches it: small star glint where it went in, the glyphs near
//         that point go white for 0.25 s, a thin line to the kunai for 0.07 s, a small glint at the
//         kunai, and the hit there (hit spark and knock, or dust on the floor)
//         after 4 such hits the marked enemy goes down and stops shooting
//   3.85  the ring burns away glyph by glyph over 0.5 s; the brackets and the seal go out
//   4.0+  a shot that arrives after that goes through and hits the caster: the rule has ended
//
// Drawing: a level ring on the floor, so it looks the same for every direction the shots come
// from. Glints are at chest height (0.3 cells north of the feet). Shapes come from
// lib/flying-thunder-god.js. Pawns are two-disc stand-ins. The shots are plain tracers in a dull
// colour on purpose, so that only the redirect is gold.
import { AltitudeLayer, Color, Mathf } from '../js/engine.js';
import { P, Y, Floor, sprite, circle, glow, soft } from './lib/six-paths-impact.js';
import {
  Gold, Pale, CasterColour, EnemyColour, kunaiMat, whiteGlow,
  star, figure, downed, streak, scriptRing, brackets, hitSpark, knocked, stung, stuckKunai,
} from './lib/flying-thunder-god.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01;
const itemLayer = AltitudeLayer.Item.AltitudeFor(), projectileLayer = AltitudeLayer.Projectile.AltitudeFor();
const Tracer = new Color(.95, .9, .8), Dust = new Color(.52, .45, .37);
// Decided values. The panel keeps only what is still being tuned.
const Lead = .4, FirstShot = .2, ShotSpeed = 35, TracerLength = .55, Chest = .3;
const RingPitch = .2, RingFrame = .16, GlintTime = .16, LinkTime = .07, LinkWidth = .05, ExitDelay = .03, ExitGlint = .7;
const DownAfter = 4, Tail = 1.1;

function times(p) {
  const cast = Lead, up = cast + p.write, over = up + p.hold;
  return { cast, up, over, gone: over + p.fade, end: over + p.fade + Tail };
}
const marked = p => p.scenario === 'kunai in an enemy';

// Every shot of the clip, replayed from zero each frame so the timeline can be scrubbed.
function shots(p, t, place) {
  const chest = v => ({ x: v.x, z: v.z + Chest }), target = chest(place.caster), list = [];
  let taken = 0, down = null;
  for (let i = 0; ; i++) {
    const fire = t.up + FirstShot + i * p.every;
    if (fire > t.gone + .25) break;
    const shooter = i % 2;                                  // 0 is the unmarked enemy, 1 the marked one
    if (shooter === 1 && down !== null && fire >= down) continue;
    const from = chest(place.shooters[shooter]), dx = target.x - from.x, dz = target.z - from.z, len = Math.hypot(dx, dz);
    const ux = dx / len, uz = dz / len, reach = fire + (len - p.radius) / ShotSpeed, caught = reach >= t.up && reach < t.over;
    const shot = { i, fire, from, ux, uz, caught, deg: Math.atan2(-uz, -ux) * Mathf.Rad2Deg,
      entry: { x: target.x - ux * p.radius, z: target.z - uz * p.radius }, ends: caught ? reach : fire + len / ShotSpeed, target };
    if (caught) { shot.out = reach + ExitDelay; if (marked(p) && ++taken === DownAfter) down = shot.out; }
    list.push(shot);
  }
  return { list, down };
}

export default {
  kit: 'Kunai belt', label: 'Guiding Thunder (sketch)',
  params: {
    scenario: { label: 'The exit kunai is', value: 'kunai in an enemy', options: ['kunai in an enemy', 'kunai on the ground'], group: 'Showcase' },
    aim: P('Direction to the shooters (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Distance to the shooters (cells)', 9, 5, 14, .5, 'Showcase'),
    every: P('One shot every', .3, .1, 1, .05, 'Showcase'),
    write: P('Ring of script is written', .25, .05, .8, .01, 'Timing (s)'),
    hold: P('Barrier holds (6 s in the proposal)', 3.2, 1, 6, .1, 'Timing (s)'),
    fade: P('Ring burns away', .5, .1, 1.5, .05, 'Timing (s)'),
    radius: P('Barrier radius (cells)', 1.3, .8, 2.5, .05, 'Shape'),
    spin: P('Ring turns (degrees per second)', 18, 0, 90, 1, 'Shape'),
    glint: P('Glint where a shot goes in (cells)', .5, .2, 1.2, .05, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Stand', t: 0 }, { name: 'Ring written', t: t.cast }, { name: 'Barrier up', t: t.up },
    { name: 'Ring burns away', t: t.over }, { name: 'Shots hit again', t: t.gone },
  ]; },
  events() { return []; },                               // a defence: no camera shake

  draw(s, p, { origin: centre, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a), half = p.distance / 2;
    // The chosen cell is halfway between the caster and the shooters, so both are in view.
    const at = (along, across = 0) => ({ x: centre.x + along * ca - across * sa, z: centre.z + along * sa + across * ca });
    const place = { caster: at(-half), shooters: [at(half, 1.7), at(half - 1, -1.9)], ground: at(half - 2.4, -.5) };
    const inEnemy = marked(p), { list, down } = shots(p, t, place);
    const isDown = down !== null && s >= down, exit = inEnemy ? place.shooters[1] : place.ground;

    // --- the barrier: a turning ring of script on the floor round the caster -----------------------------
    const written = (s - t.cast) / p.write, burn = (s - t.over) / p.fade;
    const live = clamp(written) * (1 - clamp(burn));
    if (written > 0 && burn < 1) {
      const flashes = list.filter(v => v.caught).map(v => ({ deg: v.deg, age: s - v.ends }));
      const count = Math.round(2 * Math.PI * p.radius / RingPitch);
      sprite(place.caster, p.radius * 2.8, p.radius * 2.8, Gold.withAlpha(.13 * live), glow, Floor + .005);
      circle(place.caster, p.radius + RingFrame, .55 * live, Floor + .02, Gold);
      circle(place.caster, p.radius - RingFrame, .4 * live, Floor + .02, Gold);
      scriptRing(place.caster, p.radius, count, s, t.cast, p.write, burn, p.spin * s, flashes);
    }
    // Where the shots will come out.
    const seal = smooth(written) * (1 - smooth(burn));
    brackets(exit, p.aim, clamp((written - .6) / .4) * (1 - clamp(burn)));

    // --- pawns, north first ---------------------------------------------------------------------------------
    const lastOut = Math.max(-1, ...list.filter(v => v.caught && s >= v.out).map(v => v.out));
    const lastIn = Math.max(-1, ...list.filter(v => !v.caught && s >= v.ends).map(v => v.ends));
    const hurt = lastIn < 0 ? -1 : s - lastIn, struck = lastOut < 0 || !inEnemy ? -1 : s - lastOut, push = knocked(struck);
    const victim = { x: place.shooters[1].x - ca * push * .6, z: place.shooters[1].z - sa * push * .6 };
    const figures = [
      { pos: place.caster, colour: Color.Lerp(CasterColour, Pale, stung(hurt) * .7) },
      { pos: place.shooters[0], colour: EnemyColour },
      { pos: victim, colour: Color.Lerp(EnemyColour, Pale, stung(struck) * .7), marked: true },
    ];
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(f => {
      if (f.marked && isDown) downed(f.pos, f.colour, sun, strength); else figure(f.pos, f.colour, 1, 0, sun, strength);
      if (f.marked && inEnemy && !isDown) stuckKunai(f.pos, p.aim, seal);
    });
    if (!inEnemy) {
      sprite(place.ground, .6, .6, Color.white, kunaiMat, itemLayer, 140);
      sprite(place.ground, .55, .55, Gold.withAlpha(.85 * seal), glow, Y + .01);
    }
    // The caster holds the barrier: a steady light at the chest.
    sprite({ x: place.caster.x, z: place.caster.z + Chest }, .7, .7, Gold.withAlpha(.35 * live), glow, Y + .01);

    // --- the shots ------------------------------------------------------------------------------------------------
    const out = { x: exit.x, z: exit.z + (inEnemy ? Chest : 0) };
    list.forEach(v => {
      const flight = s - v.fire;
      if (flight >= 0 && flight < .08) sprite(v.from, .5 - flight * 3, .5 - flight * 3, Tracer.withAlpha(.8 * (1 - flight / .08)), glow, Y + .02);
      if (flight >= 0 && s < v.ends) {
        const d = flight * ShotSpeed, tip = { x: v.from.x + v.ux * d, z: v.from.z + v.uz * d }, back = Math.min(d, TracerLength);
        streak(`guiding shot ${v.i}`, { x: tip.x - v.ux * back, z: tip.z - v.uz * back }, tip, .07, Tracer, undefined, projectileLayer, 2);
      }
      if (!v.caught) { hitSpark(place.caster, s - v.ends); return; }
      // Taken at the barrier, out at the kunai.
      star(`guiding in ${v.i}`, v.entry, s - v.ends, GlintTime, p.glint);
      const link = (s - v.ends) / LinkTime;
      if (link >= 0 && link < 1) streak(`guiding link ${v.i}`, v.entry, out, LinkWidth * 2.4, Gold.withAlpha(.55 * (1 - link)), whiteGlow, Y + .02, 12);
      if (link >= 0 && link < 1) streak(`guiding link core ${v.i}`, v.entry, out, LinkWidth, Pale.withAlpha(1 - link), undefined, Y + .03, 12);
      star(`guiding out ${v.i}`, out, s - v.out, GlintTime, p.glint * ExitGlint);
      const landed = s - v.out;
      if (inEnemy) hitSpark(victim, landed);
      else if (landed >= 0 && landed < .45) {
        const u = landed / .45;
        sprite({ x: out.x, z: out.z + u * .15 }, .35 + u * .6, .3 + u * .45, Dust.withAlpha(.55 * Math.sin(u * Math.PI)), soft, Y + .005);
      }
    });
  },
};
