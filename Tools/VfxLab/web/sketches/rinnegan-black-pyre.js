// Amaterasu: Black Pyre — separate large-area visual trial. Visual direction agreed;
// gameplay numbers remain proposals: target a cell within 12 cells / line of sight, radius 3,
// 4 burn damage/s for 20 s, 60 s cooldown. Caster may release early. Bleeding eye for 60 s
// (Sight -50%); recasting during the cost blinds for 10 s. No orb resource. This deliberately
// overlaps the small Amaterasu ground-fire mechanic, for comparing a large-area presentation.
// The lab demonstrates occupied ground, a pawn walking into it, or an empty area. No AI dodge
// or timed player response is required. The outside pawn never catches. No terrain propagation.
//
// Default showcase: gaze 0–0.55 s; snap to height 0.55–0.70; black/crimson column and curling
// sheets 0.70–1.30; settle 1.30–1.70; low burning field 1.70–4.70; release 4.70–5.20;
// airborne debris finishes by 6.20, while the scorch stays. Nothing falls from the sky.
// Drawing: height projects north by Lift. All flames use the same screen-oriented projection;
// caster aim changes only the caster location. Area roots fill a disc and sort north to south.
// The sustained flames draw behind pawn bodies; short contact flames overlap their feet.
// Pillar, smoke and unfurling sheets obscure the centre only during the brief eruption.
// Ribbons have ordinary UVs on cached meshes, with deformed geometry (no custom shader or UV
// scrolling). lab/pyre-sheet and lab/pyre-scorch are generated white-alpha trial textures and
// need PNG export before a C# port. Pawn figures are stand-ins. No gameplay code is changed.
import { AltitudeLayer, Color, MaterialPool, Mathf, MeshPool, ShaderDatabase } from '../js/engine.js';
import { registerLabTexture, pixels, fbm } from '../js/standins.js';
import { draw, mesh, Lift } from './lib/six-paths-solid.js';
import { P, Y, Floor, at, sprite, trail, glow, soft, rand } from './lib/six-paths-impact.js';
import { figure, CasterColour, EnemyColour, whiteGlow } from './lib/flying-thunder-god.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, TAU = Math.PI * 2;
const Pawn = AltitudeLayer.Pawn.AltitudeFor(), Shadow = AltitudeLayer.Shadows.AltitudeFor();
const Ink = new Color(.012, .007, .010), Soot = new Color(.065, .044, .050);
const Blood = new Color(.29, .012, .022), Coal = new Color(.53, .036, .028);
const Spark = new Color(.94, .19, .10), Ash = new Color(.11, .075, .079);
const Gaze = .55, Steps = 24, ParticleLife = 1.0;
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);

// Elongated turbulence cuts holes through a flame sheet. The texture is fixed; its supporting
// mesh bends and changes width, while separate sheets rise at different speeds and phases.
registerLabTexture('lab/pyre-sheet', () => pixels(256, (u, v) => {
  const y = 1 - v, x = (u - .5) * 2;
  const n = fbm(u * 6, v * 2.6, 83, 4, 8);
  const fine = fbm(u * 15, v * 5, 127, 3, 16);
  const edge = 1 - Math.abs(x) + (n - .5) * .72;
  const fibres = clamp((n * .72 + fine * .28 - .28) * 3.8);
  const fade = smooth(y / .035) * (1 - smooth((y - .83) / .17));
  const border = smooth(Math.min(u, 1 - u, v, 1 - v) / .025);
  return [1, 1, 1, clamp(edge * 4) * fibres * fade * border];
}));
registerLabTexture('lab/pyre-scorch', () => pixels(256, (u, v) => {
  const r = Math.hypot(u - .5, v - .5) * 2;
  const n = fbm(u * 8, v * 8, 91, 3, 8);
  // Alpha reaches zero at the exact requested radius, with an uneven interior feather.
  const a = clamp((1 - r) * 6) * (.72 + .28 * n);
  return [1, 1, 1, a];
}));
const sheetMat = MaterialPool.MatFrom('lab/pyre-sheet', ShaderDatabase.Transparent);
const scorchMat = MaterialPool.MatFrom('lab/pyre-scorch', ShaderDatabase.Transparent);

function times(p) {
  const crest = Gaze + p.rise, settle = crest + p.eruption;
  const field = settle + p.settle, release = field + p.burn;
  return { crest, settle, field, release, end: release + p.dissolve + ParticleLife };
}

// Textured ribbon. Points already include projected height; widths are measured across the
// curve's screen normal. UVs cover one full texture, never repeat or scroll.
function ribbon(key, pts, widths, colour, layer) {
  if (colour.a <= .001) return;
  const v = [], uv = [], tri = [];
  for (let j = 0; j < pts.length; j++) {
    const before = pts[Math.max(0, j - 1)], after = pts[Math.min(pts.length - 1, j + 1)];
    const dx = after.x - before.x, dz = after.z - before.z, len = Math.hypot(dx, dz) || 1;
    const nx = -dz / len * widths[j], nz = dx / len * widths[j], q = pts[j];
    v.push(q.x + nx, q.z + nz, q.x - nx, q.z - nz);
    uv.push(0, j / (pts.length - 1), 1, j / (pts.length - 1));
    if (j) { const n = j * 2; tri.push(n - 2, n, n - 1, n - 1, n, n + 1); }
  }
  const m = mesh(key); m.setFlat(v, tri); m.uv = Float32Array.from(uv);
  draw(m, 0, layer, 0, 1, 1, 0, colour, sheetMat);
}

function flame(key, root, height, width, clock, seed, alpha, layer, heat) {
  if (height < .015 || alpha <= .001) return;
  const pts = [], inner = [], widths = [], phase = rand(seed) * TAU;
  for (let j = 0; j <= Steps; j++) {
    const u = j / Steps, wave = Math.sin(u * 7 - clock * 6 + phase);
    const bend = height * (.085 * wave * u + .065 * Math.sin(u * 4 + phase - clock * 2) * u * u);
    const q = at(root, bend, 0, height * u);
    pts.push(q);
    inner.push(at(q, width * .14 * Math.sin(u * 12 - clock * 8 + phase), 0));
    widths.push(width * (.62 + .52 * Math.sin(u * Math.PI)) * Math.pow(1 - u, .65)
      * (.8 + .2 * Math.sin(u * 19 - clock * 7 + phase)));
  }
  ribbon(key + ' haze', pts, widths.map(w => w * 1.4), Soot.withAlpha(alpha * .24), layer);
  ribbon(key + ' black', pts, widths, Ink.withAlpha(alpha), layer + .0002);
  ribbon(key + ' heat', inner, widths.map(w => w * .36), Blood.withAlpha(alpha * heat), layer + .0004);
}

function floor(key, o, age, radius, active) {
  const grown = smooth(age / .15);
  if (grown <= 0) return;
  sprite(o, radius * 2.45, radius * 2.45, Soot.withAlpha(.32 * grown), soft, Floor + .01);
  sprite(o, radius * 2, radius * 2, Ink.withAlpha(.82 * grown), scorchMat, Floor + .012);
  sprite(o, radius * 1.8, radius * 1.8, Blood.withAlpha(.30 * active), glow, Floor + .014);
  // Short glowing breaks sit on the real AoE boundary, not on a larger shockwave radius.
  for (let i = 0; i < 24; i++) {
    const a = i / 24 * TAU, pts = [];
    for (let j = 0; j < 5; j++) {
      const phi = a + j * .018, r = radius * (1 - .012 * Math.sin(j + i));
      pts.push(at(o, Math.cos(phi) * r, Math.sin(phi) * r));
    }
    trail(key + ' edge ' + i, pts, .025, Coal.withAlpha(active * (.22 + .13 * Math.sin(age * 8 + i))), Floor + .02);
  }
}

function eruption(o, age, p, t, sun, strength) {
  const rise = smooth(age / p.rise), collapse = 1 - smooth((age + Gaze - t.settle) / p.settle);
  const life = rise * collapse;
  if (life <= .001) return;
  const clock = age * p.turbulence;
  const h = p.height * life, r = p.radius;
  // Ground-connected pillar shadow. The midpoint shifts along the sun, not along screen height.
  sprite(at(o, sun.x * h * .5, sun.z * h * .5), r * 1.2, Math.hypot(sun.x, sun.z) * h + r,
    Ink.withAlpha(strength * life * .65), soft, Shadow + .01, -Math.atan2(sun.x, sun.z) * 180 / Math.PI);
  // Tall, ragged sheets with black interruptions and crimson interiors. The highest jets stay
  // near the centre, leaving a shoulder of lower eruption around the base.
  for (let i = 0; i < 11; i++) {
    const x = (i / 10 * 2 - 1), root = at(o, x * r * .36, (rand(i + 81) - .5) * r * .32);
    const tall = h * (1 - .32 * Math.abs(x)) * (.82 + rand(i + 2) * .25);
    flame('pyre pillar ' + i, root, tall, r * (.15 + rand(i + 6) * .07), clock, i * 29 + 4,
      .90 * Math.min(1, life * 3), Y + .05 + i * .001, p.heat * (.7 + rand(i) * .3));
  }
  // Broad sheets unfurl out from the base and curl around the central column.
  for (let i = 0; i < 7; i++) {
    const pts = [], widths = [], angle = i * TAU / 7 + clock * .75;
    for (let j = 0; j <= Steps; j++) {
      const u = j / Steps, turn = angle + u * 1.7 + .2 * Math.sin(clock * 4 + u * 8 + i);
      const reach = r * (.12 + u * 1.1) * rise;
      const lift = h * .48 * Math.sin(u * Math.PI) * (.65 + .35 * Math.sin(angle) ** 2);
      pts.push(at(o, Math.cos(turn) * reach, Math.sin(turn) * reach, lift));
      widths.push(r * .17 * Math.pow(Math.sin(u * Math.PI), .9) * life);
    }
    const layer = Math.sin(angle) > 0 ? Y + .035 : Y + .085;
    ribbon('pyre unfurl ' + i, pts, widths, Ink.withAlpha(.66 * life), layer);
    ribbon('pyre unfurl heat ' + i, pts, widths.map(w => w * .28), Blood.withAlpha(.38 * life * p.heat), layer + .0002);
  }
  // Fine black speed slashes exist only on the initial snap, not throughout the burn.
  const burst = clamp(age / .35);
  if (burst < 1) for (let i = 0; i < 22; i++) {
    const a = i * 2.399, reach = r * (.5 + .9 * burst), q = at(o, Math.cos(a) * reach, Math.sin(a) * reach, burst * .7);
    trail('pyre burst ' + i, [at(q, -Math.cos(a) * r * .35, -Math.sin(a) * r * .35), q,
      at(q, Math.cos(a) * r * .3, Math.sin(a) * r * .3)], .04 * (1 - burst), Ink.withAlpha(1 - burst), Y + .10);
  }
}

function actors(o, p, s, t) {
  if (p.scenario === 'empty ground') return [];
  const positions = [[-.35,.24],[.38,.32],[-.40,-.35],[.35,-.42],[.04,.02],[1.18,-.18]];
  return positions.map(([x,z], i) => {
    let burnAt = Gaze;
    if (p.scenario === 'walks into fire' && i === 5) {
      const start = t.field + .35, walk = smooth((s - start) / 1.4);
      x = 1.3 - .7 * walk;
      // Same analytic walk evaluated at this time decides occupancy; once crossed, it stays in.
      burnAt = Math.hypot(x, z) <= 1 ? s : Infinity;
    } else if (i === 5) burnAt = Infinity;
    return { pos: at(o, x * p.radius, z * p.radius), burning: s >= burnAt && s < t.release, i };
  });
}

function field(o, age, p, t, occupants, sun, strength) {
  const life = smooth(age / .18) * (1 - smooth((age + Gaze - t.release) / p.dissolve));
  if (life <= 0) return;
  const roots = Array.from({ length: 48 }, (_, i) => {
    const a = i * 2.399, r = Math.sqrt((i + .5) / 48) * p.radius * .94;
    return { pos: at(o, Math.cos(a) * r, Math.sin(a) * r), seed: i * 37 + 131 };
  }).sort((a,b) => b.pos.z - a.pos.z);
  roots.forEach(({pos, seed}, i) => {
    const nearPawn = occupants.some(q => Math.hypot(q.pos.x - pos.x, q.pos.z - pos.z) < .45);
    const h = p.fieldHeight * (.6 + rand(seed) * .65) * (.84 + .16 * Math.sin(age * 6 + seed)) * life * (nearPawn ? .55 : 1);
    sprite(at(pos, sun.x * h * .4, sun.z * h * .4), .65, .7, Ink.withAlpha(strength * life * .24), soft, Shadow + .004);
    flame('pyre bed ' + i, pos, h, .23 + rand(seed + 1) * .13, age * p.turbulence, seed,
      .85 * Math.min(1, life * 3), Pawn - .06 + i * .0007, p.heat * .75);
  });
  occupants.forEach(q => {
    if (!q.burning) return;
    for (let i = 0; i < 3; i++) flame('pyre contact ' + q.i + '-' + i,
      at(q.pos, (i - 1) * .18, -.1), (.42 + rand(i + q.i * 13) * .35) * life, .14,
      age * p.turbulence, q.i * 71 + i, .88 * life, Y + .015 + i * .001, p.heat);
  });
}

function particles(o, age, p, t) {
  const last = Math.min(age, t.release - Gaze - .001);
  for (let i = 0; i < 42; i++) {
    const seed = i * 41 + 81, period = .62 + rand(seed) * .38, offset = rand(seed + 1) * period;
    const cycle = Math.floor((last - offset) / period);
    if (cycle < 0) continue;
    const born = offset + cycle * period, u = (age - born) / period;
    if (u < 0 || u >= 1) continue;
    const k = seed + cycle * 53, a = rand(k) * TAU, r = Math.sqrt(rand(k + 1)) * p.radius * .85;
    const towerAtBirth = smooth(born / p.rise) * (1 - smooth((born + Gaze - t.settle) / p.settle));
    const h = p.fieldHeight * .5 + towerAtBirth * p.height * rand(k + 2) * .8 + u * (1 + rand(k + 3));
    const q = at(o, Math.cos(a) * r + Math.sin(u * 3 + k) * .12, Math.sin(a) * r, h);
    const fade = Math.sin(u * Math.PI), size = .045 + rand(k + 4) * .065;
    if (i % 3 === 0) {
      sprite(q, .18, .22, Coal.withAlpha(fade * .42), glow, Y + .15);
      trail('pyre ember ' + i, [at(q, -.025, -.09), q, at(q, .01, .025)], size * .3,
        Spark.withAlpha(fade * .8), Y + .151);
    } else {
      const pts = [at(q, -.04, -.10), q, at(q, .03, .13)];
      trail('pyre ash ' + i, pts, size * (1 - u * .7), Ink.withAlpha(fade * .9), Y + .15);
    }
    const smoke = fade * smooth(u / .4) * p.smoke;
    sprite(at(q, u * .07, u * .08), .25 + u * .5, .35 + u * .75, Ash.withAlpha(smoke * .24), puff, Y + .13);
  }
}

export default {
  kit: 'Rinnegan', label: 'Amaterasu: Black Pyre (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'occupied ground', options: ['occupied ground', 'walks into fire', 'empty ground'], group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 15, 'Showcase'),
    distance: P('Caster distance (cells)', 6, 5, 12, .5, 'Showcase'),
    radius: P('Burn radius (cells)', 3, 2, 4, .25, 'Shape'),
    height: P('Eruption height (cells)', 6, 4, 8, .25, 'Shape'),
    fieldHeight: P('Sustained flame height', 1.6, .8, 2.5, .1, 'Shape'),
    rise: P('Ignition rise', .15, .08, .3, .01, 'Timing (s)'),
    eruption: P('Tall eruption holds', .6, .2, 1.2, .05, 'Timing (s)'),
    settle: P('Column settles', .4, .2, .8, .05, 'Timing (s)'),
    burn: P('Burn before release (showcase)', 3, 2, 8, .25, 'Timing (s)'),
    dissolve: P('Release fade', .5, .2, 1, .05, 'Timing (s)'),
    turbulence: P('Flame turbulence', 1, .4, 2, .1, 'Flame'),
    heat: P('Crimson interior', .85, 0, 1, .05, 'Flame'),
    smoke: P('Smoke density', .65, 0, 1, .05, 'Flame'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Gaze', t: 0 }, { name: 'Ignite', t: Gaze }, { name: 'Tower', t: t.crest },
    { name: 'Settle', t: t.settle }, { name: 'Burn', t: t.field }, { name: 'Release', t: t.release },
    { name: 'Scorch', t: t.release + p.dissolve },
  ]; },
  events(p) { return [{ t: Gaze, type: 'shake', value: .12 }, { t: times(p).crest, type: 'shake', value: .06 }]; },
  draw(s, p, {origin: o, scene}) {
    const t = times(p); if (s < 0 || s > t.end) return;
    const age = s - Gaze, sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const aim = p.aim * Math.PI / 180, caster = at(o, -Math.cos(aim) * p.distance, -Math.sin(aim) * p.distance);
    const occupants = actors(o, p, s, t);
    figure(caster, CasterColour, 1, 0, sun, strength);
    occupants.forEach(q => figure(q.pos, q.burning ? Color.Lerp(EnemyColour, Blood, .42) : EnemyColour, 1, 0, sun, strength));
    const eye = at(caster, .04, .62), gaze = s < Gaze ? smooth(s / .2) : 1 - smooth(age / .18);
    sprite(eye, .32, .32, Spark.withAlpha(gaze), glow, Y + .21);
    if (s >= Gaze) draw(MeshPool.plane10, eye.x + .025, Pawn + .01, eye.z - .12, .028, .17, 0, Blood);
    if (age < 0) {
      // Three closing marks at the target centre, rather than a warning to dodge the attack.
      for (let i = 0; i < 3; i++) {
        const a = i * TAU / 3 + s * 3, r = .75 - .35 * smooth(s / Gaze);
        trail('pyre gaze ' + i, [at(o, Math.cos(a) * r, Math.sin(a) * r), o,
          at(o, Math.cos(a + .18) * r, Math.sin(a + .18) * r)], .055, Coal.withAlpha(gaze), Floor + .03);
      }
      return;
    }
    const active = smooth(age / p.rise) * (1 - smooth((s - t.release) / p.dissolve));
    floor('pyre', o, age, p.radius, active);
    field(o, age, p, t, occupants, sun, strength);
    eruption(o, age, p, t, sun, strength);
    particles(o, age, p, t);
    const flash = 1 - clamp(age / .13);
    if (flash > 0) {
      sprite(at(o, 0, .2), p.radius * 2.1, .10, Spark.withAlpha(flash * .8), whiteGlow, Y + .20);
      sprite(o, p.radius * 3, p.radius * 3, Blood.withAlpha(flash * .4), glow, Floor + .028);
    }
  },
};
