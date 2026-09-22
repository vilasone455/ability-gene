// Amaterasu — technique proposal for the Rinnegan eye kit, not the game. Nothing in Source/RimArt
// draws this yet.
//
// What it is for (proposed, none of it agreed; every number is a placeholder). Black flames light
// where the caster looks. Target: one pawn or one cell, 12 cells, line of sight. 0.5 s warmup,
// 60 s cooldown.
//   On a pawn: a black-flame hediff, 4 burn damage per second for 20 s (80 total). Rain, water,
//   firefoam and beating do not put it out. It ends at 20 s or when the caster presses Release.
//   It spreads to an adjacent pawn at 10% per second, carrying the time that is left.
//   On a cell: a 3x3 black fire for 20 s that ignites any pawn that walks in. It does not spread to
//   buildings or terrain, so it cannot eat a base.
//   Cost: the caster gets Bleeding eye for 60 s (Sight -50%). Casting again while it is on blinds
//   them for 10 s. This is the kit's finisher, not something for every fight.
// Pairs with Amenotejikara: swap a burning pawn into its own line, or a burning kunai into a path.
//
// Showcase: 0.00–0.50 gaze; 0.50 ignition / 0.18 s eruption; 2.00 adjacent pawn
// catches; 4.00 caster releases; 4.30 flames gone, permanent char remains to 4.90.
// Drawing: independent curved tongues, with narrow plum rims and black centres, rise from
// staggered ground-depth rows. Back flames draw behind the pawn, shorter front flames across its
// feet; height projects north by Lift. The outline is screen-oriented for every caster facing.
// Ignition overshoots, spits radial black scraps, then settles into upward travelling curls.
// Detached cinders have analytic birth times, including on release: scrubbing is deterministic.
// Area-fire roots fill a disc and sort north to south, avoiding visible ranks of flames.
// Ground soot and shadows follow the actual footprint / sun, never a floating flattened ring.
// Only the cinders use lab/black-flame (generator below; still needs a PNG before a C# port).
// The gameplay proposal above is unchanged; no orb cost belongs to this eye technique.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../js/engine.js';
import { registerLabTexture, pixels, fbm } from '../js/standins.js';
import { draw, Body, Lift } from './lib/six-paths-solid.js';
import { P, Y, Floor, at, sprite, band, trail, glow, soft, rand } from './lib/six-paths-impact.js';
import { figure, whiteGlow, CasterColour, EnemyColour } from './lib/flying-thunder-god.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01;
const pawnLayer = AltitudeLayer.Pawn.AltitudeFor(), topLayer = AltitudeLayer.MetaOverlays.AltitudeFor();
const flatDisc = Meshes.disc(48, 'amaterasu disc');

// A flame tongue: a teardrop, widest a third of the way up, with a noisy soft edge. White in the
// alpha so the draw colour sets black, crimson or ember.
registerLabTexture('lab/black-flame', () => pixels(128, (u, v) => {
  const y = 1 - v, x = (u - .5) * 2;
  const width = Math.sin(Math.min(1, y / .35) * Math.PI / 2) * Math.pow(Math.max(0, 1 - y), .5) * .9;
  const edge = .18 + .55 * fbm(u * 3, v * 3, 23, 3, 3) - .3;
  const d = Math.abs(x) / Math.max(1e-3, width);
  const a = Math.max(0, 1 - Math.max(0, d - .55 + edge) / .5) * (y < .06 ? y / .06 : 1);
  return [1, 1, 1, Math.min(1, a)];
}));
const flame = MaterialPool.MatFrom('lab/black-flame', ShaderDatabase.Transparent);
const flameGlow = MaterialPool.MatFrom('lab/black-flame', ShaderDatabase.MoteGlow);

// Decided looks.
const Ember = new Color(.55, .06, .10), EmberLit = new Color(.85, .16, .14), Crimson = new Color(.75, .08, .12);
const Scorch = new Color(.05, .03, .04), Ally = new Color(.45, .62, .40);
const Gaze = .5, MarkR = .55, Sink = .3, DimLife = .18, ShakeSize = .075;
const Scenarios = ['pawn', 'pawn, spreads', 'cell'];

function times(p) {
  const ignite = Gaze, release = p.release ? ignite + p.releaseAt : Infinity;
  const end = p.release ? release + Sink + .6 : ignite + p.burn; // showcase cut, not a gameplay expiry
  return { ignite, release, end };
}

// The Mangekyo mark: a pupil and 3 curved blades, drawn from the centre outward as u goes 0..1,
// turning by spin radians.
function mark(key, c, u, spin, alpha) {
  if (u <= 0 || alpha <= 0) return;
  draw(flatDisc, c.x, Floor + .03, c.z, .1 * u, .1 * u, 0, Crimson.withAlpha(alpha));
  for (let i = 0; i < 3; i++) {
    const a0 = spin + i * Math.PI * 2 / 3, pts = [];
    for (let k = 0; k <= 8; k++) {
      const v = k / 8 * u, r = .12 + MarkR * v, a = a0 + v * 1.4;
      pts.push({ x: c.x + Math.cos(a) * r, z: c.z + Math.sin(a) * r });
    }
    trail(`${key} blade ${i}`, pts, .16, Crimson.withAlpha(alpha), Floor + .031);
  }
}

function redStar(pos, size, alpha) {
  if (alpha <= 0) return;
  sprite(pos, size * 1.2, size * 1.2, Crimson.withAlpha(alpha * .7), glow, Y + .2);
  draw(MeshPool.plane10, pos.x, Y + .21, pos.z, size * 2, size * .12, 0, EmberLit.withAlpha(alpha), whiteGlow);
  draw(MeshPool.plane10, pos.x, Y + .21, pos.z, size * .12, size * 2, 0, EmberLit.withAlpha(alpha), whiteGlow);
}

// The black body stays opaque. Only the thin broken edges carry light; a full bright outline
// makes black fire look like neon ropes. Each tongue narrows irregularly and hooks at its tip.
const Ink = new Color(.009, .006, .016), Plum = new Color(.20, .065, .23);
const Hot = new Color(.77, .25, .42);
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor();
const TongueSteps = 24;

function tongue(key, root, h, w, clock, seed, alpha, layer, edge, lean = 0) {
  if (h < .005 || alpha <= 0) return;
  const phase = rand(seed) * Math.PI * 2, left = [], right = [], innerL = [], innerR = [];
  const lip = [], lipInner = [];
  for (let j = 0; j <= TongueSteps; j++) {
    const u = j / TongueSteps;
    // The wave runs from root to tip; frequencies differ per flame so the bed never sways as one.
    const wave = Math.sin(u * 6.2 - clock * (5.2 + rand(seed + 2)) + phase);
    const curl = Math.sin(u * 3.8 - clock * 2.8 + phase);
    const cx = root.x + lean * u + h * (.065 * wave * u + .095 * curl * u * u * u);
    const cz = root.z + h * Lift * u;
    const taper = Math.pow(1 - u, .82) * (.90 + .26 * Math.sin(u * Math.PI));
    const scallop = 1 + .14 * Math.sin(u * 14 - clock * 7 + phase) * Math.sin(u * Math.PI);
    const half = w * taper * scallop;
    const rim = Math.min(half * .22, .014 + .012 * Math.sin(u * Math.PI)) * edge;
    left.push({ x: cx - half, z: cz }); right.push({ x: cx + half, z: cz });
    innerL.push({ x: cx - half + rim, z: cz }); innerR.push({ x: cx + half - rim * .5, z: cz });
    // A warm broken seam on one side. Tapers out at both ends, avoiding a uniform neon contour.
    const seam = .018 * edge * Math.pow(Math.max(0, Math.sin(u * 9 - clock * 5 + phase)), 3) * Math.sin(u * Math.PI);
    lip.push({ x: cx - half + rim, z: cz });
    lipInner.push({ x: cx - half + rim + seam, z: cz });
  }
  band(key + ' edge', left, right, Plum.withAlpha(alpha), layer);
  band(key + ' body', innerL, innerR, Ink.withAlpha(alpha), layer + .0002);
  if (edge > 0) band(key + ' seam', lip, lipInner, Hot.withAlpha(alpha * edge * .8), layer + .0004);
}

function ignition(key, c, w, age, seed) {
  if (age < 0 || age > .48) return;
  const flash = Math.pow(1 - clamp(age / .13), 2);
  sprite(at(c, 0, .28), w * 3.8, w * 3.8, Crimson.withAlpha(flash * .65), glow, Y + .16);
  // One brutal horizontal eye-cut, followed by the black eruption. No projectile travels here.
  sprite(at(c, 0, .35), w * 4.8, .075, Hot.withAlpha(flash), whiteGlow, Y + .17);
  for (let i = 0; i < 13; i++) {
    const k = seed + i * 7, a = i * 2.399, u = clamp(age / (.26 + rand(k) * .22));
    if (u >= 1) continue;
    const reach = w * (.45 + rand(k + 1) * 1.4) * Math.sin(u * Math.PI / 2);
    const pts = [];
    for (let j = 0; j <= 10; j++) {
      const v = j / 10, r = reach * (.3 + v * .7);
      pts.push(at(c, Math.cos(a) * r, Math.sin(a) * r,
        Math.sin(v * Math.PI) * .25 + u * (.4 + rand(k + 2))));
    }
    trail(key + ' eruption ' + i, pts, (.04 + rand(k + 3) * .09) * (1 - u), Ink.withAlpha(1 - u), Y + .14);
  }
}

function fire(key, c, w, height, t0, s, p, t, seed, sun, strength, cell = false) {
  const age = s - t0;
  if (age < 0 || t0 >= t.release) return;
  const release = 1 - smooth((s - t.release) / Sink);
  const grow = smooth(age / p.rise), life = grow * release;
  const depth = cell ? w : w * .55;
  const clock = age * p.speed;
  // A rough bed, with little glowing fissures. It remains after the caster releases the fire.
  const stain = smooth(age / .22);
  sprite(c, w * 3.3, depth * 3.3, Scorch.withAlpha(.75 * stain), puff, Floor + .02);
  for (let i = 0; i < 12; i++) {
    const a = i * 2.399, r = .55 + rand(seed + i) * .5;
    const q = at(c, Math.cos(a) * w * r, Math.sin(a) * depth * r);
    sprite(q, w * (.38 + rand(i) * .4), depth * .6, Ink.withAlpha(stain * .6), puff, Floor + .021);
    if (life > 0) {
      const pts = [q, at(q, Math.cos(a + .4) * .13, Math.sin(a + .4) * .13), at(q, Math.cos(a) * .25, Math.sin(a) * .25)];
      trail(key + ' coal ' + i, pts, .025, EmberLit.withAlpha(life * (.3 + .2 * Math.sin(clock * 8 + i))), Floor + .025);
    }
  }
  ignition(key, c, w, age, seed);
  if (life > 0) {
    const surge = 1 + .42 * Math.exp(-Math.pow((age - p.rise) / .15, 2));
    sprite(c, w * 3.4, depth * 3.4, Ember.withAlpha(.44 * life), glow, Floor + .022);
    // Low soot welds the roots together. Separate noisy puffs avoid a ruler-straight base.
    for (let i = 0; i < 7; i++) {
      const x = (i / 6 * 2 - 1) * w * .8;
      const q = at(c, x, -depth * .12 + .06 * Math.sin(clock * 4 + i));
      sprite(q, w * .9, depth * .85, Ink.withAlpha(life * .92), puff, Y + .032);
    }
    // The area fire gets more small tongues, not stretched pawn flames. Overlapping soot
    // patches join their feet across the circular footprint, including the far row.
    if (cell) {
      for (let i = 0; i < 23; i++) {
        const a = i * 2.399, r = Math.sqrt((i + .5) / 23) * w * .88;
        const q = at(c, Math.cos(a) * r, Math.sin(a) * r);
        sprite(q, .95, .95, Ink.withAlpha(life * .85), puff, Floor + .027);
      }
    }
    // Roots at different depths, rather than a single straight lower edge.
    const cellRoots = cell ? Array.from({ length: 40 }, (_, i) => {
      const a = i * 2.399, r = Math.sqrt((i + .5) / 40) * w * .93;
      return at(c, Math.cos(a) * r, Math.sin(a) * r);
    }).sort((a, b) => b.z - a.z) : null;
    const rows = cell ? 4 : 3;
    for (let row = 0; row < rows; row++) {
      const count = cell ? (row === 0 || row === 3 ? 9 : 11) : (row === 1 ? 6 : 5);
      for (let i = 0; i < count; i++) {
        const k = seed + row * 71 + i * 13, x = (i / (count - 1) * 2 - 1);
        const root = cell ? cellRoots[[0, 9, 20, 31][row] + i] :
          at(c, x * w * (row === 1 ? .92 : .76), (1 - row) * depth * .65 + (rand(k + 1) - .5) * depth * .25);
        const envelope = .7 + .3 * Math.sqrt(1 - x * x);
        const rowHeight = cell ? [.90, .82, .65, .52][row] : [1, .78, .43][row];
        const breathing = .80 + .20 * Math.sin(clock * (4 + rand(k + 2) * 2) + rand(k) * 12);
        const h = height * rowHeight * envelope * (.78 + rand(k + 3) * .4) * breathing * life * surge;
        const width = (cell ? Math.min(.8, w) : w) * (.24 + rand(k + 4) * .08) * (.7 + .3 * life);
        const layer = row === 0 ? pawnLayer - .018 + i * .0007 : Y + .04 + row * .014 + i * .0007;
        const lean = x * .18 * w + Math.sin(clock * 2 + k) * .05;
        // A stretched soft shadow runs from the root along the scene sun.
        const mid = at(root, sun.x * h * .5, sun.z * h * .5);
        const shadowLen = Math.hypot(sun.x, sun.z) * h;
        sprite(mid, width * 2.5, shadowLen + width, Ink.withAlpha(strength * life * .35), soft,
          shadowLayer + .003, -Math.atan2(sun.x, sun.z) * 180 / Math.PI);
        tongue(key + ' tongue ' + row + '-' + i, root, h, width, clock, k, Math.min(1, life * 4), layer, p.edge, lean);
        // A smaller fork peels away from a flank. It shares the flame's base but not its rhythm.
        if (i % 3 === 0) tongue(key + ' fork ' + row + '-' + i,
          at(root, (rand(k + 6) - .5) * width, 0), h * .62, width * .50,
          clock * 1.18, k + 33, Math.min(1, life * 4), layer + .0005, p.edge * .6, lean - width * .8);
      }
    }
  }
  // Analytic births stop at release; already airborne scraps finish their own lifetime.
  // No frame-to-frame simulation, and no particle jumps caused by changing random seeds mid-flight.
  for (let i = 0; i < 20; i++) {
    const k = seed + i * 17, period = .55 + rand(k) * .35, offset = rand(k + 1) * period;
    const lastBirth = Math.min(age, t.release - t0 - .001);
    const cycle = Math.floor((lastBirth - offset) / period);
    if (cycle < 0) continue;
    const born = offset + cycle * period, u = (age - born) / period;
    if (u < 0 || u >= 1) continue;
    const r = k + cycle * 31, x = (rand(r + 2) * 2 - 1) * w;
    const h = height * (.4 + .4 * rand(r + 3)) * smooth(born / p.rise) + u * (1.0 + rand(r + 4));
    const pos = at(c, x + Math.sin(u * 4 + r) * .14 + u * .18, (rand(r + 5) - .5) * depth, h);
    const fade = Math.sin(u * Math.PI) * (1 - u), size = .04 + rand(r + 6) * .08;
    sprite(pos, size * 1.5, size * (2.5 + u), Ink.withAlpha(fade * 1.6), flame, Y + .13, Math.sin(u * 5 + k) * 28);
    if (i % 3 === 0) sprite(at(pos, -.02, -.025), size * .34, size * .55, EmberLit.withAlpha(fade), flameGlow, Y + .131);
  }
}

// A pawn in the fire: darkened, so it reads as inside the flame and showing through the gaps.
function charred(pos, life) {
  if (life > 0) sprite(at(pos, 0, .38), .55, .95, Body.withAlpha(.5 * life), soft, pawnLayer + .003);
}

export default {
  kit: 'Rinnegan', label: 'Amaterasu (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[1], options: Scenarios, group: 'Showcase' },
    distance: P('Caster distance (cells)', 6, 3, 12, .5, 'Showcase'),
    release: { label: 'Caster releases it', value: true, group: 'Showcase' },
    releaseAt: P('Release after ignite', 3.5, 1, 8, .25, 'Timing (s)'),
    burn: P('Burns out after (showcase)', 5, 2, 20, .5, 'Timing (s)'),
    rise: P('Tongues rise', .18, .1, .6, .05, 'Timing (s)'),
    spreadAt: P('Neighbour catches after ignite', 1.5, .5, 4, .25, 'Timing (s)'),
    height: P('Flame height (cells)', 2.6, 1, 4, .1, 'Flame'),
    width: P('Flame half-width on a pawn (cells)', .7, .4, 1.5, .05, 'Flame'),
    speed: P('Flame speed', 1, .3, 2.5, .1, 'Flame'),
    edge: P('Flame edge intensity (0-1)', .65, 0, 1, .05, 'Flame'),
    radius: P('Cell fire radius (cells)', 1.5, .8, 2, .1, 'Flame'),
    dim: P('Screen dim on ignite (0-1)', .18, 0, .7, .05, 'Flame'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Gaze', t: 0 }, { name: 'Ignite', t: t.ignite },
    ...(p.scenario === Scenarios[1] ? [{ name: 'Spreads', t: t.ignite + p.spreadAt }] : []),
    ...(p.release ? [{ name: 'Release', t: t.release }] : []),
  ]; },
  events(p) { return [{ t: times(p).ignite, type: 'shake', value: ShakeSize }]; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const caster = { x: o.x - p.distance, z: o.z }, cell = p.scenario === Scenarios[2];
    const neighbour = { x: o.x + 1, z: o.z }, spreads = p.scenario === Scenarios[1];

    figure(caster, CasterColour, 1, 0, sun, strength);
    if (!cell) figure(o, EnemyColour, 1, 0, sun, strength);
    if (spreads) figure(neighbour, Ally, 1, 0, sun, strength);

    // The gaze: eye star on the caster, the mark drawing itself and spinning up at the target.
    const eye = { x: caster.x + .04, z: caster.z + .62 };
    if (s < t.ignite + .08) redStar(eye, .2 + .1 * clamp(s / Gaze), s < t.ignite ? .6 + .4 * s / Gaze : 1 - (s - t.ignite) / .08);
    // Bleeding eye: a drip from the cast on. In game this is the hediff's cost, shown on the pawn.
    if (s >= t.ignite) {
      const len = .06 + .1 * clamp((s - t.ignite) / 1.5);
      draw(MeshPool.plane10, eye.x + .03, pawnLayer + .01, eye.z - .06 - len / 2, .035, len, 0, Crimson.withAlpha(.9));
    }
    if (s < t.ignite) mark('amaterasu', o, smooth(s / (Gaze * .7)), s * 6, .9);
    else if (s < t.ignite + .1) mark('amaterasu', o, 1 + (s - t.ignite) * 6, t.ignite * 6, 1 - (s - t.ignite) / .1);

    // The fires. A pawn burns on its own cell; the cell fire covers 3x3.
    const lifeOf = t0 => s < t0 || t0 >= t.release ? 0 : smooth((s - t0) / p.rise) * (1 - smooth((s - t.release) / Sink));
    if (cell) fire('amaterasu cell', o, p.radius, p.height, t.ignite, s, p, t, 100, sun, strength, true);
    else { fire('amaterasu pawn', { x: o.x, z: o.z - .1 }, p.width, p.height, t.ignite, s, p, t, 100, sun, strength); charred(o, lifeOf(t.ignite)); }
    if (spreads) { fire('amaterasu spread', { x: neighbour.x, z: neighbour.z - .1 }, p.width * .9, p.height * .9, t.ignite + p.spreadAt, s, p, t, 300, sun, strength); charred(neighbour, lifeOf(t.ignite + p.spreadAt)); }

    // Screen dim on ignite, so the black reads as darker than the world for a moment.
    const dimAge = s - t.ignite;
    if (p.dim > 0 && dimAge >= 0 && dimAge < DimLife) {
      const a = p.dim * (dimAge < .08 ? dimAge / .08 : 1 - smooth((dimAge - .08) / (DimLife - .08)));
      draw(MeshPool.plane10, o.x, topLayer, o.z, 400, 400, 0, new Color(0, 0, 0, a));
    }
  },
};
