// Flying Thunder God: Chain — technique proposal for the kunai belt, not the game. Nothing in
// Source/RimArt draws this yet. The multi-target version of kunai-flying-thunder-god.js.
//
// What it is for (proposed, none of it agreed; every number is a placeholder). The caster jumps to
// every pawn that has one of their kunai stuck in it, one after another, and cuts each one.
// Target: no target picked; it takes every marked pawn within 29.9 cells, nearest first, up to 5
// (the belt holds 6 kunai). Needs at least 2 marked pawns. No line of sight, 0 s warmup. One jump
// every 0.24 s. At each pawn the caster appears in the cell behind it, seen from where they came
// from, and makes 1 melee attack at x1.0 damage (the single jump is x1.5). The kunai stay in.
// After the last cut the caster either stays there or jumps back to where they started; the
// switch shows both so one can be chosen. Cooldown 60 s. The cost is the setup: each target needs
// a kunai thrown into it first (1.5 s cooldown per throw, 12 damage each).
//
// Order, with the default timings and 3 targets:
//   0.00  stand; every target already has a kunai in it
//   0.40  every kunai's seal lights; a strip of script is written from each kunai to the cell the
//         caster will land on, with 4 corner brackets round that cell. The strips point along the
//         route, so the route can be read before anything moves. (Returning: a cross of script
//         and brackets under the caster too.)
//   0.60  jump 1: sliver, line for 0.08 s, star glint at both ends, slash, hit, shake
//   0.84  jump 2        1.08  jump 3        (1.32  jump back, no cut)
//         each line leaves a faint gold line for 0.6 s, so the whole route shows at the end
//   then  each strip burns away glyph by glyph once its jump has landed, over 1.0 s
//
// Drawing: all of it comes from lib/flying-thunder-god.js. Flat on the ground or lines between two
// points, so no per-facing method. Pawns are two-disc stand-ins with no facing. The target
// positions are a fixed spread for the demonstration, not a rule.
import { Color, Mathf } from '../js/engine.js';
import { P, Y, Floor, sprite, glow } from './lib/six-paths-impact.js';
import {
  Gold, CasterColour, EnemyColour, Pale, whiteGlow, Behind, StripBack, StripGlyphs, SlashTime,
  star, figure, sparks, leave, jumpLine, script, scriptCross, brackets, slash, hitSpark, knocked, stung, stuckKunai, streak,
} from './lib/flying-thunder-god.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01;
const Lead = .4, StrikeDelay = .04, RouteGlow = .6;
// Where the targets stand in the demonstration: [further along the aim than the first, across it].
const Spots = [[0, 0], [1.8, 2.2], [3.6, -1.4], [5.2, 1.6], [6.8, -.8]];
const returns = p => p.after === 'jumps back to the start';

// Every place and direction. Stops are where the caster stands: the start, then each landing cell.
function route(p, centre) {
  const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a), n = Math.round(p.targets);
  const spots = Spots.slice(0, n).map(([along, across]) => [p.distance + along, across]);
  const far = Math.max(...spots.map(v => v[0])) + Behind, low = Math.min(0, ...spots.map(v => v[1])), high = Math.max(0, ...spots.map(v => v[1]));
  // The chosen cell is the middle of everything, so the whole route is in view.
  const at = (along, across) => ({ x: centre.x + (along - far / 2) * ca - (across - (low + high) / 2) * sa, z: centre.z + (along - far / 2) * sa + (across - (low + high) / 2) * ca });
  const home = at(0, 0), stops = [home], hops = [];
  spots.forEach(([along, across]) => {
    const from = stops[stops.length - 1], target = at(along, across);
    const dx = target.x - from.x, dz = target.z - from.z, len = Math.hypot(dx, dz) || 1, ux = dx / len, uz = dz / len;
    const to = { x: target.x + ux * Behind, z: target.z + uz * Behind };
    hops.push({ from, to, target, ux, uz, deg: Math.atan2(dz, dx) * Mathf.Rad2Deg, thrown: Math.atan2(target.z - home.z, target.x - home.x) * Mathf.Rad2Deg });
    stops.push(to);
  });
  if (returns(p)) {
    const from = stops[stops.length - 1];
    hops.push({ from, to: home, target: null, deg: Math.atan2(home.z - from.z, home.x - from.x) * Mathf.Rad2Deg });
    stops.push(home);
  }
  return { home, stops, hops };
}
function times(p) {
  const count = Math.round(p.targets) + (returns(p) ? 1 : 0), first = Lead + p.seal;
  const go = k => first + k * p.hop, arrive = k => go(k) + p.squeeze, hit = k => arrive(k) + StrikeDelay + SlashTime / 2;
  const settle = arrive(count - 1) + p.flash;
  return { count, cast: Lead, go, arrive, strike: k => arrive(k) + StrikeDelay, hit, settle, end: settle + p.linger };
}

export default {
  kit: 'Kunai belt', label: 'Flying Thunder God: Chain (sketch)',
  params: {
    targets: P('Marked pawns', 3, 2, 5, 1, 'Showcase'),
    after: { label: 'After the last cut the caster', value: 'stays at the last target', options: ['stays at the last target', 'jumps back to the start'], group: 'Showcase' },
    aim: P('Direction to the first target (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Distance to the first target (cells)', 5, 3, 10, .5, 'Showcase'),
    seal: P('Floor script is written before the first jump', .2, .05, .6, .01, 'Timing (s)'),
    hop: P('One jump every', .24, .15, .6, .01, 'Timing (s)'),
    squeeze: P('Caster narrows / widens', .05, .02, .2, .01, 'Timing (s)'),
    line: P('Line between the two places', .08, .02, .3, .01, 'Timing (s)'),
    flash: P('Arrival flash fades', .22, .08, .6, .01, 'Timing (s)'),
    linger: P('Floor script burns away', 1, .3, 3, .05, 'Timing (s)'),
    flashRadius: P('Arrival flash radius (cells)', 1, .5, 2.5, .05, 'Shape'),
    lineWidth: P('Line width (cells)', .12, .04, .4, .01, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p), n = Math.round(p.targets); return [
    { name: 'Stand', t: 0 }, { name: 'Script written', t: t.cast },
    ...Array.from({ length: t.count }, (_, k) => ({ name: k < n ? `Jump ${k + 1}` : 'Jump back', t: t.go(k) })),
    { name: 'Script burns away', t: t.settle },
  ]; },
  events(p) { const t = times(p); return Array.from({ length: Math.round(p.targets) }, (_, k) => ({ t: t.hit(k), type: 'shake', value: .03 })); },

  draw(s, p, { origin: centre, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const { stops, hops } = route(p, centre);
    const written = (s - t.cast) / p.seal;

    // --- script on the floor, one strip per jump, all written together -----------------------------
    if (written > 0) hops.forEach((h, k) => {
      const burn = (s - t.arrive(k) - p.flash) / p.linger;
      if (burn >= 1) return;
      const beat = s < t.arrive(k) ? 1 : .75 + .25 * Math.sin((s - t.arrive(k)) * 9), lit = clamp(written) * (1 - smooth(burn)) * beat;
      if (h.target) {
        const mid = (Behind - StripBack) / 2;
        sprite({ x: h.target.x + h.ux * mid, z: h.target.z + h.uz * mid }, 1.8, .5, Gold.withAlpha(.3 * lit), glow, Floor + .005, -h.deg);
        script(h.target, h.deg, -StripBack, StripGlyphs, s, t.cast, p.seal, burn, k * 4);
      } else {
        sprite(h.to, 1.5, 1.5, Gold.withAlpha(.25 * lit), glow, Floor + .005);
        scriptCross(h.to, h.deg, s, t.cast, p.seal, burn, k * 4);
      }
      brackets(h.to, h.deg, clamp((written - .8) / .2) * (1 - clamp((burn - .85) / .15)));
    });

    // --- pawns, north first; each target has its kunai -------------------------------------------------
    const figures = [];
    hops.forEach((h, k) => {
      if (!h.target) return;
      const age = s - t.hit(k), push = knocked(age);
      h.victim = { x: h.target.x - h.ux * push, z: h.target.z - h.uz * push };
      figures.push({ pos: h.victim, colour: Color.Lerp(EnemyColour, Pale, stung(age) * .7), thin: 0, hop: h, k });
    });
    const stop = hops.filter((_, k) => s >= t.arrive(k)).length;    // how many jumps have landed
    const thin = Math.max(stop > 0 ? 1 - smooth((s - t.arrive(stop - 1)) / p.squeeze) : 0, stop < t.count ? smooth((s - t.go(stop)) / p.squeeze) : 0);
    figures.push({ pos: stops[stop], colour: CasterColour, thin });
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(f => {
      figure(f.pos, f.colour, 1, f.thin, sun, strength);
      if (f.hop) stuckKunai(f.pos, f.hop.thrown, smooth(written) * (1 - .7 * smooth((s - t.arrive(f.k)) / p.flash)) * (1 - smooth((s - t.arrive(f.k) - p.flash) / p.linger)));
    });

    // --- each jump: what is left behind, the line, the star, the cut --------------------------------------
    hops.forEach((h, k) => {
      const gone = s - t.go(k), here = s - t.arrive(k);
      leave(`thunder chain leave ${k}`, h.from, gone, p.squeeze, p.flashRadius, k === 0);
      jumpLine(`thunder chain line ${k}`, h.from, h.to, gone / p.line, p.lineWidth);
      // The route stays as a faint line after the bright one, so the whole path shows at the end.
      const faded = (gone - p.line) / RouteGlow;
      if (faded >= 0 && faded < 1) streak(`thunder chain route ${k}`, { x: h.from.x, z: h.from.z + .3 }, { x: h.to.x, z: h.to.z + .3 },
        p.lineWidth * .7, Gold.withAlpha(.45 * (1 - faded) * (1 - faded)), whiteGlow, Y + .015, 12);
      star(`thunder chain arrive ${k}`, { x: h.to.x, z: h.to.z + .3 }, here, p.flash, p.flashRadius);
      sparks(`thunder chain land ${k}`, h.to, here, 8);
      if (!h.target) return;
      slash(`thunder chain slash ${k}`, h.to, h.deg + 180, s - t.strike(k));
      hitSpark(h.victim, s - t.hit(k));
    });
  },
};
