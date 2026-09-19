// Six Paths blade wave: a proposal, not the game. Nothing in Source/RimArt draws this yet, so
// there is no recording to compare it with.
//
// What it is for. The six orbs gather in front of the caster, weld into one crescent, and the
// crescent is thrown, belly first. It runs out to 10 cells along the aim and cuts everything in
// the 5-cell swath it crosses. Aimed, not radial: the repulsion eye pushes out and the attraction
// eye pulls in, both centred on a point, and neither damages a pawn standing on open ground --
// Shinra Tensei only hurts what it slams into something. This one is pointed somewhere, hits
// everything it passes through, and moves nobody. Proposed at ~18 Cut per pawn, allies included,
// on a 30-second cooldown, between the eyes' 8-20 / 20 s and 15-45 / 40 s. None of it is agreed.
// It is the damage half of the forward-line pair: Black Tide uses the same six orbs and a forward
// line but pushes and debuffs instead of cutting. A lingering effect on the cut (bleed or burn)
// is undecided.
//
// Why a crescent rather than the six separate lanes of the earlier sweep: six blades running
// outward cut six thin lanes and missed most of the ring between them, which looked like an area
// attack and was not one. One continuous edge as wide as the swath has no gaps to explain. The
// swath drawn on the floor is the hit area, at its true 5 cells, so the picture and the rule are
// the same thing.
//
// A ground ability, start to finish. The crescent skims the floor rather than flying over it:
//   - nothing takes a height offset. SixPathsHeight.Lift is what puts a thing in the air in this
//     kit and only the thrown dirt uses it here.
//   - the crescent's shadow sits under it, pushed along the sun by a fixed 0.08 cells and never
//     by a height, so the edge never separates from the ground it is cutting.
//   - the swath is cut before the crescent reaches the far end and stays after it is spent.
//
// Port notes for the C# version:
//   - the crescent is one arc, so it is a strip down a polyline rather than an orb form. The six
//     orbs that made it stay visible as seams along its back, which is what keeps it Six Paths
//   - the gathering orbs are SixPathsShapes.Outline morphing Orb -> Blade through
//     SixPathsShapes.Lerp, the shapes SixPathsGraphics' MorphMesh already builds
//   - rim first, body over it, the order DrawOrb keeps (SixPathsGraphics.cs:112)
//   - the swath, its score lines and the spray need InBounds and Fogged checks per cell, and the
//     crescent should stop at the first wall it meets: it is an edge, not a psychic effect
//
// "Copy as C# constants" on the Params panel turns the current values into the lines to paste.

import {
  AltitudeLayer, Color, Graphics, MaterialPool, MaterialPropertyBlock, Mathf, Matrix4x4, Mesh,
  MeshPool, Quaternion, ShaderDatabase, ShaderPropertyIDs, Vector3,
} from '../js/engine.js';
import { hash } from '../js/standins.js';

const Body = new Color(0.035, 0.028, 0.050);
const Rim = new Color(0.52, 0.36, 0.86);
const Sheen = new Color(0.72, 0.62, 0.95);
const Dirt = new Color(0.34, 0.26, 0.19);
const Cut = new Color(0.09, 0.07, 0.05);
const Lift = 0.60;              // SixPathsHeight.Lift

// The forms the gathering orbs morph between, copied from SixPathsShapes.cs:32-34.
const OrbForm = { halfX: 1.00, halfY: 1.00, corner: 2.0, taper: 0.00 };
const BladeForm = { halfX: 1.70, halfY: 0.52, corner: 2.4, taper: 1.00 };
const Segments = 64;            // SixPathsGraphics' MorphMesh resolution
const Orbs = 6;                 // SixPathsTiming.Orbs

const solid = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const soft = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent);
const softGlow = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.MoteGlow);
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const props = new MaterialPropertyBlock();

// One mesh per thing drawn in a frame, never one reused: Graphics.DrawMesh reads a mesh when the
// frame renders, the same rule SixPathsGraphics.cs keeps.
const scratch = new Map();
const mesh = (key) => scratch.get(key) ?? scratch.set(key, new Mesh(key)).get(key);

const rand = (i, k) => hash(i, k, 6008);
const Y = {
  cut: AltitudeLayer.Filth.AltitudeFor(),
  shadows: AltitudeLayer.Shadows.AltitudeFor(),
  spray: AltitudeLayer.MoteOverheadLow.AltitudeFor(),
  edge: AltitudeLayer.MoteOverhead.AltitudeFor(),
};

function draw(m, x, y, z, sx, sz, rot, colour, material = solid) {
  props.SetColor(ShaderPropertyIDs.Color, colour);
  Graphics.DrawMesh(m, Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0, rot, 0), new Vector3(sx, 1, sz)),
    material, 0, null, 0, props);
}

// ---------------------------------------------------------------- the orb outline

/** SixPathsShapes.Lerp: four numbers, not 64 points. */
const lerpForm = (a, b, t) => ({
  halfX: Mathf.Lerp(a.halfX, b.halfX, t), halfY: Mathf.Lerp(a.halfY, b.halfY, t),
  corner: Mathf.Lerp(a.corner, b.corner, t), taper: Mathf.Lerp(a.taper, b.taper, t),
});

/** SixPathsShapes.Outline, point for point. t runs 0..1 counter-clockwise from the +x end. */
function outline(shape, t) {
  const phi = t * Math.PI * 2, c = Math.cos(phi), s = Math.sin(phi);
  const radius = 1 / Math.pow(Math.pow(Math.abs(c), shape.corner) + Math.pow(Math.abs(s), shape.corner),
    1 / shape.corner);
  const width = 1 - shape.taper * 0.5 * (1 + c);
  return { x: shape.halfX * radius * c, y: shape.halfY * radius * s * width };
}

/** A filled silhouette, as a fan around the middle. Unit space: the draw call scales it. */
function formMesh(key, shape) {
  const xz = [0, 0], tri = [];
  for (let i = 0; i < Segments; i++) {
    const pt = outline(shape, i / Segments);
    xz.push(pt.x, pt.y);
    tri.push(0, ((i + 1) % Segments) + 1, i + 1);
  }
  const m = mesh(key); m.setFlat(xz, tri); return m;
}

// ---------------------------------------------------------------- strips along a path

/**
 * A ribbon down a polyline, with its own width at every point. The crescent, its lit rim, the
 * swath and every score line in the swath are all this one shape.
 */
function ribbon(key, pts, widths, y, colour, material = solid) {
  if (pts.length < 2) return;
  const xz = [], tri = [];
  for (let i = 0; i < pts.length; i++) {
    const a = pts[Math.max(0, i - 1)], b = pts[Math.min(pts.length - 1, i + 1)];
    const tx = b.x - a.x, tz = b.z - a.z, len = Math.hypot(tx, tz) || 1;
    const nx = -tz / len, nz = tx / len, w = widths[i] / 2;
    xz.push(pts[i].x + nx * w, pts[i].z + nz * w, pts[i].x - nx * w, pts[i].z - nz * w);
    if (i < pts.length - 1) { const v = i * 2; tri.push(v, v + 2, v + 1, v + 1, v + 2, v + 3); }
  }
  const m = mesh(key); m.setFlat(xz, tri);
  draw(m, 0, y, 0, 1, 1, 0, colour, material);
}

/**
 * The ground a bowed edge swept between two distances: bowed at both ends, straight down the
 * sides. Each point across the span runs back along the aimed direction by its own sagitta, so
 * the trailing edge carries the same curve as the leading one.
 */
function drawStrip(key, p, from, to, spanFrom, spanTo, origin, y, colour, opts = {}) {
  const { ragged = 0, material = solid, uv = null } = opts;
  const { ux, uz, vx, vz } = axes(p);
  // A grid, not a row of columns across the span. Built the flat way, each long side of the band
  // is one quad edge with a vertex at each end, so jitter there slides the whole side over
  // instead of breaking it up and `ragged` does nothing you can see. Rows along the run give the
  // sides vertices to wander with.
  const cols = 24, rows = 6, xz = [], tri = [], uvs = [];
  for (let r = 0; r <= rows; r++) {
    const a = r / rows;
    const d = Mathf.Lerp(from, to, a), spanHere = Mathf.Lerp(spanFrom, spanTo, a);
    for (let i = 0; i <= cols; i++) {
      const k = (i / cols) * 2 - 1;
      // Fixed per (column, row), so every stacked layer frays along the same line rather than
      // each one fraying its own way into a soft gradient.
      const edge = ragged * (rand(i, 40 + r) - 0.5) * 2 * Math.abs(k);
      const along = d + p.bow * (1 - k * k) + edge * 0.4;
      const side = k * spanHere / 2 + edge;
      xz.push(origin.x + ux * along + vx * side, origin.z + uz * along + vz * side);
      // setFlat leaves uv null, and a textured material on an unwrapped mesh samples one texel
      // and paints a flat slab. 'across' runs the disc's horizontal centreline over the width, so
      // the sides feather and the ends do not; 'full' maps the whole disc onto the band.
      if (uv === 'across') uvs.push((k + 1) / 2, 0.5);
      else if (uv === 'full') uvs.push((k + 1) / 2, a);
    }
  }
  const stride = cols + 1;
  for (let r = 0; r < rows; r++)
    for (let i = 0; i < cols; i++) {
      const v = r * stride + i;
      tri.push(v, v + 1, v + stride, v + 1, v + stride + 1, v + stride);
    }
  const m = mesh(key); m.setFlat(xz, tri);
  m.uv = uv ? Float32Array.from(uvs) : null;
  draw(m, 0, y, 0, 1, 1, 0, colour, material);
}

const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });

export default {
  kit: 'Six Paths',
  label: 'Blade wave (sketch)',
  compareWith: 'Six Paths: orb showcase',
  params: {
    gather: P('Orbs gather in front', 0.35, 0.1, 1.5, 0.05, 'Timing (s)'),
    form: P('They weld into the crescent', 0.20, 0.05, 1, 0.01, 'Timing (s)'),
    travel: P('The crescent runs out', 0.55, 0.1, 2, 0.05, 'Timing (s)'),
    spend: P('It is spent at the far end', 0.25, 0.05, 1.5, 0.05, 'Timing (s)'),
    marksFade: P('Swath and dirt fade', 0.80, 0, 3, 0.05, 'Timing (s)'),

    heading: P('Aimed at (degrees)', 0, -180, 180, 5, 'Aim'),
    ringRadius: P('Orbs start on a ring of (cells)', 1.80, 0.5, 5, 0.05, 'Aim'),
    start: P('Crescent forms at (cells)', 1.60, 0.5, 5, 0.1, 'Aim'),
    reach: P('Runs out to (cells)', 10.0, 2, 20, 0.5, 'Aim'),

    span: P('Crescent span (cells)', 5.00, 1, 12, 0.1, 'Crescent'),
    bow: P('Middle leads the tips by (cells)', 1.20, -3, 3, 0.05, 'Crescent'),
    depth: P('Thickness at the middle (cells)', 0.90, 0.1, 2.5, 0.05, 'Crescent'),
    grow: P('Span grows over the run by', 0.18, 0, 1, 0.01, 'Crescent'),
    outline: P('Rim brightness', 0.85, 0, 1, 0.01, 'Crescent'),
    rimWidth: P('Rim width (cells)', 0.05, 0.005, 0.25, 0.005, 'Crescent'),
    sheen: P('Sheen along the back', 0.22, 0, 0.6, 0.01, 'Crescent'),
    seams: P('Seams where the orbs welded', 0.35, 0, 1, 0.01, 'Crescent'),
    tipGlow: P('Tip glow', 0.30, 0, 1, 0.01, 'Crescent'),

    swath: P('Swath darkness', 0.26, 0, 1, 0.01, 'Ground'),
    tail: P('Oldest end keeps', 0.42, 0, 1, 0.01, 'Ground'),
    bands: P('Layers in the gradient', 10, 1, 20, 1, 'Ground'),
    ragged: P('Edges break up by (cells)', 0.22, 0, 1, 0.01, 'Ground'),
    haze: P('Dust haze over the swath', 0.28, 0, 0.6, 0.01, 'Ground'),
    heat: P('Fresh cut glow behind the edge', 0.20, 0, 0.8, 0.01, 'Ground'),
    heatLen: P('Glow reaches back (cells)', 1.20, 0.1, 4, 0.05, 'Ground'),
    scores: P('Score lines across the swath', 7, 0, 16, 1, 'Ground'),
    scoreDark: P('Score darkness', 0.40, 0, 1, 0.01, 'Ground'),
    scoreWidth: P('Score width (cells)', 0.22, 0.02, 1, 0.01, 'Ground'),
    spray: P('Dirt thrown along the edge', 10, 0, 24, 1, 'Ground'),
    dust: P('Dust behind the edge', 5, 0, 12, 1, 'Ground'),
    shake: P('Camera shake', 0.07, 0, 0.2, 0.01, 'Ground'),

    sunShadow: P('Sun shadow strength', 0.34, 0, 0.8, 0.01, 'Shadow'),
    contact: P('Shadow offset (cells)', 0.08, 0, 0.4, 0.01, 'Shadow'),
  },

  duration(p) { return times(p).endAt + p.marksFade; },

  phases(p) {
    const t = times(p);
    return [
      { name: 'Orbs gather', t: 0 }, { name: 'Weld', t: t.formAt },
      { name: 'Run out', t: t.launchAt }, { name: 'Spent', t: t.spendAt },
    ];
  },

  events(p) {
    const t = times(p);
    // One knock as the crescent is thrown. Nothing lands; an edge leaves.
    return p.shake > 0 ? [{ t: t.launchAt, type: 'shake', value: p.shake }] : [];
  },

  draw(seconds, p, { origin, scene }) {
    const t = times(p);
    const sun = scene.shadowVector;
    drawSwath(seconds, p, t, origin);
    drawSpray(seconds, p, t, origin, sun);
    drawCrescent(seconds, p, t, origin, sun);
    drawOrbs(seconds, p, t, origin, sun);
  },
};

function times(p) {
  const formAt = p.gather;
  const launchAt = formAt + p.form;
  const spendAt = launchAt + p.travel;
  return { formAt, launchAt, spendAt, endAt: spendAt + p.spend };
}

const progress = (s, start, span) => Mathf.Clamp01((s - start) / span);

/** The aimed direction, and the axis across it. */
function axes(p) {
  const h = p.heading * Mathf.Deg2Rad;
  return { ux: Math.cos(h), uz: Math.sin(h), vx: -Math.sin(h), vz: Math.cos(h) };
}

/**
 * How far out the crescent is, in cells, and how far through its life it is. It leaves fast and
 * eases into the far end rather than stopping dead, so the cut behind it is not evenly spaced.
 */
function flightOf(s, p, t) {
  const u = progress(s, t.launchAt, p.travel);
  const eased = 1 - (1 - u) * (1 - u);
  return { u, at: Mathf.Lerp(p.start, p.reach, eased) };
}

/**
 * The crescent's own line at distance `at`, as a polyline across the aimed direction. `k` runs
 * -1..1 from one tip to the other. The middle leads the tips by `bow`, so the belly arrives
 * first and the horns trail. A thrown blade leads with its cutting edge; horns first reads as a
 * bowl being dragged backwards, and the swath behind it doubles the mistake.
 */
function crescent(p, at, span, origin) {
  const { ux, uz, vx, vz } = axes(p);
  const pts = [], steps = 24;
  for (let i = 0; i <= steps; i++) {
    const k = (i / steps) * 2 - 1;
    const forward = at + p.bow * (1 - k * k);
    const side = k * span / 2;
    pts.push({ x: origin.x + ux * forward + vx * side, z: origin.z + uz * forward + vz * side });
  }
  return pts;
}

/** Thickness at each point of the crescent: full at the middle, nothing at the tips. */
function thickness(p, depth) {
  const widths = [], steps = 24;
  for (let i = 0; i <= steps; i++) {
    const k = (i / steps) * 2 - 1;
    widths.push(depth * Math.pow(Math.max(0, 1 - k * k), 0.55));
  }
  return widths;
}

/** How solid the crescent is: welding in, whole through the run, spent at the end. */
function presence(s, p, t) {
  const weld = Mathf.Smooth(progress(s, t.formAt, p.form));
  const gone = Mathf.Smooth(progress(s, t.spendAt, p.spend));
  return weld * (1 - gone);
}

// ---------------------------------------------------------------- the floor

/**
 * The swath: everything the crescent has crossed, at its true width, because that is the rule the
 * ability is. Score lines run down it for the look of a cut rather than a stain.
 */
function drawSwath(s, p, t, origin) {
  if (s < t.launchAt) return;
  const fade = 1 - Mathf.Smooth(progress(s, t.endAt, p.marksFade));
  if (fade <= 0.001) return;
  const { at } = flightOf(s, p, t);
  if (at <= p.start + 0.01) return;

  const { ux, uz, vx, vz } = axes(p);
  const span = p.span * (1 + p.grow * progress(s, t.launchAt, p.travel));

  const spanAt = (a) => Mathf.Lerp(p.span, span, a);

  // Haze first, wider than the cut and soft-edged, so the swath does not begin at a hard line.
  if (p.haze > 0)
    drawStrip('haze', p, p.start - 0.3, at, p.span * 1.5, span * 1.6, origin, Y.cut - 0.002,
      Dirt.withAlpha(p.haze * fade), { ragged: p.ragged * 1.6, material: soft, uv: 'across' });

  // The cut itself. Built as its own strip rather than through ribbon(): ribbon lays width
  // perpendicular to the polyline, and the crescent's perpendiculars fan apart at the tips, so a
  // ribbon down the crescent pinches in the middle and splays into a bowtie. Every point here
  // instead runs straight back along the aimed direction, which leaves straight sides and gives
  // both ends the same bow the edge that cut them has.
  //
  // Drawn as one flat band it reads as a slab. Instead a base layer covers the whole length and
  // further layers start progressively later and all end at the edge, so their alphas stack into
  // a ramp that is darkest where the blade is now: overlapping layers, not abutting bands, or
  // every join would show as a step.
  drawStrip('swath', p, p.start, at, p.span, span, origin, Y.cut,
    Cut.withAlpha(p.swath * p.tail * fade), { ragged: p.ragged });
  const layers = Math.max(1, Math.round(p.bands));
  const perLayer = (p.swath * (1 - p.tail)) / layers;
  for (let k = 0; k < layers; k++) {
    const a = (k + 1) / (layers + 1);
    drawStrip(`swathband-${k}`, p, Mathf.Lerp(p.start, at, a), at, spanAt(a), span, origin,
      Y.cut + 0.0005 + k * 0.0002, Cut.withAlpha(perLayer * fade), { ragged: p.ragged });
  }

  // Score lines: parallel to the run, spread across the span, each starting where the edge that
  // drew it first bit and tapering out at both ends rather than stopping square.
  for (let k = 0; k < p.scores; k++) {
    const across = (((k + 0.5) / p.scores) * 2 - 1) * (0.92 + 0.08 * rand(k, 3));
    const sag = p.bow * (1 - across * across);
    // Each score covers only part of the run, so they do not all start and stop together.
    const head = at + sag, foot = Mathf.Lerp(p.start + sag, head, 0.10 + 0.35 * rand(k, 2));
    const line = [], lineWidths = [], steps = 10;
    const thick = p.scoreWidth * (0.55 + 0.75 * rand(k, 1));
    for (let i = 0; i <= steps; i++) {
      const a = i / steps;
      const d = Mathf.Lerp(foot, head, a);
      const drift = (rand(k, 10 + i) - 0.5) * 0.10;
      const side = across * spanAt(a) / 2 + drift;
      line.push({ x: origin.x + ux * d + vx * side, z: origin.z + uz * d + vz * side });
      // Nothing at the tail, full in the middle, thinning again where the edge is still cutting.
      lineWidths.push(thick * Math.pow(Math.sin(a * Math.PI), 0.45));
    }
    ribbon(`score-${k}`, line, lineWidths, Y.cut + 0.003,
      new Color(0.05, 0.04, 0.03, p.scoreDark * fade));
  }

  // The ground immediately behind the edge, still open. It is the only warm thing on the floor
  // and it is what gives the trail a direction: the far end is finished, this end is happening.
  if (p.heat > 0) {
    const back = Math.max(p.start, at - p.heatLen);
    drawStrip('heat', p, back, at, spanAt(0.75), span, origin, Y.cut + 0.006,
      Rim.withAlpha(p.heat * fade), { ragged: p.ragged * 0.5, material: softGlow, uv: 'full' });
  }
}

// ---------------------------------------------------------------- the orbs gathering

/** The six orbs sliding off their ring into the line the crescent will weld from. */
function drawOrbs(s, p, t, origin, sun) {
  if (s >= t.launchAt) return;
  const u = Mathf.Smooth(progress(s, 0, p.gather));
  const weld = Mathf.Smooth(progress(s, t.formAt, p.form));
  if (weld >= 1) return;

  const { vx, vz } = axes(p);
  const line = crescent(p, p.start, p.span, origin);

  for (let i = 0; i < Orbs; i++) {
    // From its place on the ring to its place along the crescent, which is where it welds in.
    const angle = (i / Orbs) * Math.PI * 2;
    const fromX = origin.x + Math.cos(angle) * p.ringRadius;
    const fromZ = origin.z + Math.sin(angle) * p.ringRadius;
    const seat = line[Math.round(((i + 0.5) / Orbs) * (line.length - 1))];
    const x = Mathf.Lerp(fromX, seat.x, u), z = Mathf.Lerp(fromZ, seat.z, u);

    // Drawing out into the blade form as it travels, then swallowed by the crescent.
    const shape = lerpForm(OrbForm, BladeForm, u * 0.7);
    const size = 0.42 * (1 - 0.25 * weld);
    const alpha = 1 - weld;
    // Lying across the aimed direction, the way it will lie in the crescent it is joining.
    const facing = -Math.atan2(vz, vx) * Mathf.Rad2Deg;

    draw(formMesh(`orbshadow-${i}`, shape), x + sun.x * p.contact, Y.shadows, z + sun.z * p.contact,
      size, size, facing, new Color(0, 0, 0, p.sunShadow * alpha));
    // Rim first, body over it. Without the lit edge these read as holes in the ground rather
    // than as the orbs the rest of the kit draws.
    if (p.outline > 0) {
      const rim = size + p.rimWidth * 1.6;
      draw(formMesh(`orbrim-${i}`, shape), x, Y.edge, z, rim, rim, facing, Rim.withAlpha(p.outline * alpha));
    }
    draw(formMesh(`orbbody-${i}`, shape), x, Y.edge + 0.004, z, size, size, facing, Body.withAlpha(alpha));
    if (p.tipGlow > 0)
      draw(MeshPool.plane10, x, Y.edge + 0.008, z, size * 1.6, size * 1.6, 0,
        Sheen.withAlpha(p.tipGlow * 0.5 * alpha), softGlow);
  }
}

// ---------------------------------------------------------------- the crescent

function drawCrescent(s, p, t, origin, sun) {
  const solidity = presence(s, p, t);
  if (solidity <= 0.001) return;

  const { at, u } = s >= t.launchAt ? flightOf(s, p, t) : { at: p.start, u: 0 };
  const span = p.span * (1 + p.grow * u);
  const depth = p.depth * solidity;
  const pts = crescent(p, at, span, origin);
  const widths = thickness(p, depth);

  // Shadow: under the edge, pushed a fixed distance along the sun. It never grows, because the
  // crescent never leaves the floor.
  const shadow = pts.map((q) => ({ x: q.x + sun.x * p.contact, z: q.z + sun.z * p.contact }));
  ribbon('waveshadow', shadow, widths, Y.shadows, new Color(0, 0, 0, p.sunShadow));

  // Rim first, body over it, so the lit line sits outside the silhouette (SixPathsGraphics.cs:112).
  if (p.outline > 0)
    ribbon('waverim', pts, widths.map((w) => w + p.rimWidth * 2), Y.edge,
      Rim.withAlpha(p.outline * solidity));
  ribbon('wavebody', pts, widths, Y.edge + 0.004, Body);

  // Six seams down the back, where the orbs welded together. Without them the crescent is just a
  // curved blade and stops being Six Paths.
  if (p.seams > 0)
    for (let i = 1; i < Orbs; i++) {
      const idx = Math.round((i / Orbs) * (pts.length - 1));
      const w = widths[idx];
      if (w <= 0.02) continue;
      draw(MeshPool.plane10, pts[idx].x, Y.edge + 0.006, pts[idx].z, w * 0.55, w * 0.55, 0,
        Rim.withAlpha(p.seams * solidity), softGlow);
    }

  if (p.sheen > 0)
    ribbon('wavesheen', pts, widths.map((w) => w * 0.30), Y.edge + 0.008,
      Sheen.withAlpha(p.sheen * solidity));

  if (p.tipGlow > 0)
    for (const end of [pts[0], pts[pts.length - 1]])
      draw(MeshPool.plane10, end.x, Y.edge + 0.010, end.z, p.depth * 1.6, p.depth * 1.6, 0,
        Sheen.withAlpha(p.tipGlow * solidity), softGlow);
}

// ---------------------------------------------------------------- what the edge throws out

/**
 * Dirt off the leading edge, and dust settling behind it. The only thing here that leaves the
 * floor, and it leaves it because the floor is being opened.
 */
function drawSpray(s, p, t, origin, sun) {
  if (s < t.launchAt) return;
  const fade = 1 - Mathf.Smooth(progress(s, t.endAt, p.marksFade));
  if (fade <= 0.001) return;
  const { at } = flightOf(s, p, t);
  const { ux, uz, vx, vz } = axes(p);

  for (let k = 0; k < p.spray; k++) {
    // Each clod is thrown from a fixed point down the swath, at the moment the edge passed it.
    const along = (k + 0.5) / Math.max(1, p.spray);
    const d = Mathf.Lerp(p.start, p.reach, along);
    if (d > at) continue;
    // The edge reaches d at the time the eased flight says it does.
    const eased = Mathf.Clamp01((d - p.start) / Math.max(1e-5, p.reach - p.start));
    const when = t.launchAt + (1 - Math.sqrt(Math.max(0, 1 - eased))) * p.travel;
    const since = s - when;
    if (since < 0) continue;

    const across = (rand(k, 2) - 0.5) * p.span;
    const bornX = origin.x + ux * d + vx * across, bornZ = origin.z + uz * d + vz * across;
    const speed = 0.8 + rand(k, 3) * 1.4, rise = 1.5 + rand(k, 4) * 1.8, gravity = 20;
    const flight = (2 * rise) / gravity, tau = Math.min(since, flight);
    const height = Math.max(0, rise * tau - 0.5 * gravity * tau * tau);
    // Thrown forward along the run, the way a cut throws what it lifts.
    const out = speed * tau;
    const x = bornX + ux * out, z = bornZ + uz * out * 0.85;
    const size = 0.06 + rand(k, 5) * 0.09;

    if (height > 0.02)
      draw(MeshPool.plane10, x + sun.x * height, Y.shadows, z + sun.z * height,
        size * 1.4, size, 0, new Color(0, 0, 0, 0.26 * fade), soft);
    draw(MeshPool.plane10, x, (height < 0.6 ? Y.spray : Y.edge + 0.02) + k * 0.0002,
      z + height * Lift, size, size * 0.8, rand(k, 6) * 360 + tau * 520 * (rand(k, 7) - 0.5),
      Dirt.withAlpha(fade * (since > flight ? 0.85 : 1)));
  }

  for (let k = 0; k < p.dust; k++) {
    const along = (k + 0.5) / Math.max(1, p.dust);
    const d = Mathf.Lerp(p.start, p.reach, along);
    if (d > at) continue;
    const eased = Mathf.Clamp01((d - p.start) / Math.max(1e-5, p.reach - p.start));
    const when = t.launchAt + (1 - Math.sqrt(Math.max(0, 1 - eased))) * p.travel;
    const life = 0.5 + rand(k, 8) * 0.4, u = (s - when) / life;
    if (u < 0 || u >= 1) continue;
    const across = (rand(k, 9) - 0.5) * p.span * 0.9;
    const size = Mathf.Lerp(0.45, 1.10, Math.sqrt(u));
    // Settles backward, behind the edge that raised it.
    const back = d - u * 0.5;
    draw(MeshPool.plane10, origin.x + ux * back + vx * across, Y.spray + k * 0.0001,
      origin.z + uz * back + vz * across + u * 0.12 * Lift, size, size * 0.85, rand(k, 10) * 360,
      new Color(0.60, 0.54, 0.46, 0.30 * Math.sin(Math.min(1, u * 4) * Math.PI / 2) * (1 - u) ** 1.3), puff);
  }
}
