// Flying Thunder God — technique proposal for the kunai belt, not the game. Nothing in Source/RimArt
// draws this yet.
//
// What it is for (proposed, none of it agreed; every number is a placeholder). The caster jumps to
// one of their own thrown kunai. Target: a kunai stuck in a pawn (AG_EmbeddedKunai) or lying on the
// ground. Range 29.9 cells, no line of sight, 0 s warmup, 20 s cooldown.
//   Kunai in a pawn: the caster appears in the free cell behind that pawn and makes 1 melee attack
//   at once at x1.5 damage. The kunai stays in.
//   Kunai on the ground: the caster appears on that cell and the kunai goes back into the belt.
// It differs from the Anchor organ's clap: that one is unarmed, marks by hand at 9.9 cells, swaps
// two places and is drawn skip-blue. This one is armed, one way, marked by a throw, and arrives
// with a hit. Yellow-white, so it is not read as Anchor blue or Six Paths violet.
//
// Order, with the default timings:
//   0.00  stand; the kunai is already in the enemy (or on the ground)
//   0.40  the seal on the kunai lights and script is written on the floor, glyph by glyph: a strip of
//         10 glyphs from 0.3 cells before the kunai into the landing cell, then 4 corner brackets
//         round that cell (kunai on ground: a cross of 4 arms x 4 glyphs round the kunai). No ring,
//         because a ring reads as an area of effect and this has none
//   0.55  the caster narrows to a sliver and is gone; afterimage 0.3 s, floor scorch 1.2 s, sparks
//   0.55  a line joins the two places for 0.08 s
//   0.61  arrival flash, a star glint (core, thin opening ring, 2 long and 2 short rays through the
//         centre); a smaller one at the old place at 0.55; the caster widens out of a sliver
//   0.65  slash arc 0.12 s across the enemy, hit spark, the enemy is knocked 0.1 cells, camera shake
//   0.85  the script burns away glyph by glyph from the kunai end over 1.35 s; the caster stays
//
// Drawing: everything is flat on the ground or a line between two points, so it turns with the aim
// and needs no per-facing method. The shapes are in lib/flying-thunder-god.js, shared with the chain
// version: quads, one ring mesh and strip meshes only. Both pawns are two-disc stand-ins with no facing. The kunai is the mod's own texture (RimArt/Kunai/Kunai).
import { AltitudeLayer, Color, Mathf } from '../js/engine.js';
import { P, Y, Floor, sprite, circle, glow } from './lib/six-paths-impact.js';
import {
  Gold, Pale, CasterColour, EnemyColour, kunaiMat, Behind, StripBack, StripGlyphs, SlashTime,
  star, figure, sparks, leave, jumpLine, script, scriptCross, brackets, slash, hitSpark, knocked, stung, stuckKunai,
} from './lib/flying-thunder-god.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01;
const itemLayer = AltitudeLayer.Item.AltitudeFor();
// Decided values. The panel keeps only what is still being tuned. The shapes are in the library.
const Lead = .4, StrikeDelay = .04;

function times(p) {
  const cast = Lead, go = cast + p.seal, arrive = go + p.squeeze, strike = arrive + StrikeDelay;
  const settle = arrive + p.flash;
  return { cast, go, arrive, strike, hit: strike + SlashTime / 2, settle, end: settle + p.linger };
}
const enemy = p => p.scenario === 'kunai in enemy';

export default {
  kit: 'Kunai belt', label: 'Flying Thunder God (sketch)',
  params: {
    scenario: { label: 'The kunai is', value: 'kunai in enemy', options: ['kunai in enemy', 'kunai on ground'], group: 'Showcase' },
    aim: P('Direction to the kunai (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Distance to the kunai (cells)', 7, 3, 14, .5, 'Showcase'),
    seal: P('Floor script is written before the jump', .15, .05, .6, .01, 'Timing (s)'),
    squeeze: P('Caster narrows / widens', .06, .02, .2, .01, 'Timing (s)'),
    line: P('Line between the two places', .08, .02, .3, .01, 'Timing (s)'),
    flash: P('Arrival flash fades', .24, .08, .6, .01, 'Timing (s)'),
    linger: P('Floor script burns away', 1.35, .3, 3, .05, 'Timing (s)'),
    flashRadius: P('Arrival flash radius (cells)', 1.2, .5, 2.5, .05, 'Shape'),
    lineWidth: P('Line width (cells)', .12, .04, .4, .01, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Stand', t: 0 }, { name: 'Script written', t: t.cast }, { name: 'Gone / line', t: t.go },
    { name: 'Arrive', t: t.arrive }, ...(enemy(p) ? [{ name: 'Strike', t: t.strike }] : []), { name: 'Script burns away', t: t.settle },
  ]; },
  events(p) { return enemy(p) ? [{ t: times(p).hit, type: 'shake', value: .04 }] : []; },

  draw(s, p, { origin: centre, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a), inEnemy = enemy(p);
    // The chosen cell is halfway between the two places, so both are in view. o is the kunai's cell.
    const o = { x: centre.x + ca * p.distance / 2, z: centre.z + sa * p.distance / 2 };
    const place = (along, across = 0) => ({ x: o.x + along * ca - across * sa, z: o.z + along * sa + across * ca });
    const home = place(-p.distance), landing = inEnemy ? place(Behind) : o;

    // --- script on the floor: written from the kunai to the landing cell, brackets round that cell ----
    // No ring: a ring reads as an area of effect and this has none. The strip says where the caster lands.
    const written = (s - t.cast) / p.seal, burn = (s - t.settle) / p.linger;
    if (written > 0 && burn < 1) {
      const beat = s < t.arrive ? 1 : .75 + .25 * Math.sin((s - t.arrive) * 9), lit = clamp(written) * (1 - smooth(burn)) * beat;
      if (inEnemy) {
        sprite(place((Behind - StripBack) / 2), 1.8, .5, Gold.withAlpha(.3 * lit), glow, Floor + .005, -p.aim);
        script(o, p.aim, -StripBack, StripGlyphs, s, t.cast, p.seal, burn);
      } else {
        sprite(o, 1.5, 1.5, Gold.withAlpha(.25 * lit), glow, Floor + .005);
        scriptCross(o, p.aim, s, t.cast, p.seal, burn);
      }
      brackets(landing, p.aim, clamp((written - .8) / .2) * (1 - clamp((burn - .85) / .15)));
    }

    // --- pawns, north first, and the kunai with the seal on it ---------------------------------------
    const seal = smooth(written) * (1 - .7 * smooth((s - t.arrive) / p.flash)) * (1 - smooth(burn));
    const push = inEnemy ? knocked(s - t.hit) : 0, victim = place(-push);
    const figures = [];
    if (inEnemy) figures.push({ pos: victim, colour: Color.Lerp(EnemyColour, Pale, stung(s - t.hit) * .7), thin: 0, kunai: true });
    if (s < t.arrive) figures.push({ pos: home, colour: CasterColour, thin: smooth((s - t.go) / p.squeeze) });
    else figures.push({ pos: landing, colour: CasterColour, thin: 1 - smooth((s - t.arrive) / p.squeeze) });
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(f => {
      figure(f.pos, f.colour, 1, f.thin, sun, strength);
      if (f.kunai) stuckKunai(f.pos, p.aim, seal);
    });
    if (!inEnemy && s < t.arrive) {
      sprite(o, .6, .6, Color.white, kunaiMat, itemLayer, 140);
      sprite(o, .55, .55, Gold.withAlpha(.85 * seal), glow, Y + .01);
    } else if (!inEnemy) {
      const picked = s - t.arrive;                    // the ground kunai goes back into the belt
      if (picked < .3) circle(o, .2 + picked * .8, .8 * (1 - picked / .3), Floor + .04, Pale);
    }

    // --- leaving, the line between the two places, arriving, the cut -----------------------------------
    leave('thunder god leave', home, s - t.go, p.squeeze, p.flashRadius);
    jumpLine('thunder god line', home, landing, (s - t.go) / p.line, p.lineWidth);
    star('thunder god arrive star', { x: landing.x, z: landing.z + .3 }, s - t.arrive, p.flash, p.flashRadius);
    sparks('thunder god arrive', landing, s - t.arrive, 10);
    if (inEnemy) {
      slash('thunder god slash', landing, p.aim + 180, s - t.strike);
      hitSpark(victim, s - t.hit);
    }
  },
};
