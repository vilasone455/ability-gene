// Six Paths nullify volley: a proposal, not the game. Nothing in Source/RimArt draws this yet, so
// there is no recording to compare it with.
//
// What it is for (proposed, none of it agreed). The Six Paths ability that uses a single orb.
// Target a point within 15 cells in line of sight. One orb comes up out of the floor beside the
// caster and fires 10 needles over 0.5 s into a 1.3-cell radius around the point. Half the
// needles go to pawns inside the radius, split evenly between them; the rest land on random cells
// in it. Every pawn a needle hits has its active psycast effects ended (hediffs an ability
// applied) and its shield belt set to 0 energy, which starts the belt's normal recharge delay.
// No damage, no stun; allies inside the radius are hit too. 35 s cooldown. A shield belt does
// not stop the needles: they are what cancels it.
//
// Order and default timings, with the target 7 cells away:
//   0.00  the orb comes up out of the floor beside the caster and rises to 1.3 cells (0.30 s)
//   0.30  needle spikes grow out of the side facing the target (0.30 s)
//   0.60  the spikes launch one at a time, 0.05 s apart, at 22 cells/s on a 0.8-cell arc; the orb
//         kicks back with each shot. Needles take about 0.35 s to arrive.
//         a needle that hits a pawn vanishes into it with a flash; the first hit collapses the
//         shield bubble and closes two rings on the floor under the pawn
//         a needle that misses sticks in the floor at the angle it came down at
//   1.25  the orb sinks back into the floor (0.30 s)
//   1.75  the stuck needles sink into the floor (0.30 s)
//
// Depth:
//   - needles are solids (lib/six-paths-solid.js): the Blade form tapered in width and thickness,
//     lit on their edges, pointed along their real 3D flight line
//   - every needle casts its outline along the sun from its height, so the shadow starts far from
//     a needle leaving the orb and meets it where it lands
//   - the orb hovers: its shadow sits off to one side by 1.3 cells of height, and it is shaded as
//     a sphere with a lit side and a highlight
//   - spikes pointing away from the camera draw behind the orb, the rest in front of it
//   - stuck needles north of the target draw behind the target pawn, the rest in front
//
// Lab stand-ins, not part of the effect: the two pawns and the blue shield bubble. In game the
// belt draws its own bubble and stops drawing it at 0 energy; the port draws no bubble.
//
// Port notes for the C# version:
//   - a needle is SixPathsShapes.Blade with taper applied to thickness as well
//   - apply the nullify per pawn when its first needle arrives, not on the cast
//   - stuck needles, holes and dirt need InBounds and Fogged checks per cell
//
// "Copy as C# constants" on the Params panel turns the current values into the lines to paste.

import { AltitudeLayer, Color, Mathf, MaterialPool, Meshes, MeshPool, ShaderDatabase } from '../js/engine.js';
import { hash } from '../js/standins.js';
import {
  Body, Rim, Sheen, Slate, Lift, add, cross, draw, dot, drawSolid, drawSolidShadow, mesh, mul, screenX,
  screenZ, sunLight, unit, Up, v3, View,
} from './lib/six-paths-solid.js';

const Dirt = new Color(0.34, 0.26, 0.19);
const Bubble = new Color(0.36, 0.62, 1.0);
const White = new Color(0.92, 0.86, 1.0);
const TAU = Math.PI * 2;

const additive = MaterialPool.MatFrom('white', ShaderDatabase.MoteGlow);
const soft = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent);
const softGlow = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.MoteGlow);
const pawn = MaterialPool.MatFrom('canvas:pawn', ShaderDatabase.Transparent);

const disc = Meshes.disc(48, 'volley orb');
const rimBand = Meshes.band(1, 1.14, 48, 'volley orb rim');
const thinRing = Meshes.band(0.90, 1, 96, 'volley floor ring');
const bubbleRing = Meshes.band(0.94, 1, 96, 'volley bubble ring');

const rand = (i, k) => hash(i, k, 9119);
const progress = (s, start, span) => Mathf.Clamp01((s - start) / Math.max(span, 1e-5));
const easeOut = (u) => 1 - (1 - Mathf.Clamp01(u)) ** 3;
const easeIn = (u) => Mathf.Clamp01(u) ** 2;
const easeOutBack = (u) => { u = Mathf.Clamp01(u); const c = 1.8; return 1 + (c + 1) * (u - 1) ** 3 + c * (u - 1) ** 2; };

/** Tip position and heading of a needle f (0..1) of the way along its arc. */
function flightAt(shot, f, p) {
  const tip = add(add(shot.launch, mul(shot.chord, f)), mul(Up, 4 * p.arc * f * (1 - f)));
  return { tip, dir: unit(add(shot.chord, mul(Up, 4 * p.arc * (1 - 2 * f)))) };
}

const Y = {
  filth: AltitudeLayer.Filth.AltitudeFor(),
  shadows: AltitudeLayer.Shadows.AltitudeFor(),
  behind: AltitudeLayer.Building.AltitudeFor(),
  pawn: AltitudeLayer.Pawn.AltitudeFor(),
  front: AltitudeLayer.MoteOverhead.AltitudeFor(),
};

const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });

export default {
  kit: 'Six Paths',
  label: 'Nullify volley (sketch)',
  params: {
    popup: P('Orb comes up in', 0.30, 0.05, 1, 0.01, 'Timing (s)'),
    charge: P('Spikes grow in', 0.30, 0.05, 1, 0.01, 'Timing (s)'),
    interval: P('Time between shots', 0.05, 0.01, 0.3, 0.005, 'Timing (s)'),
    speed: P('Needle speed (cells/s)', 22, 5, 60, 0.5, 'Timing (s)'),
    arc: P('Flight arc height (cells)', 0.8, 0, 3, 0.05, 'Volley'),
    orbHold: P('Orb stays after the last shot', 0.20, 0, 1.5, 0.01, 'Timing (s)'),
    linger: P('Stuck needles stay', 0.45, 0, 3, 0.05, 'Timing (s)'),
    sink: P('Sink into the floor', 0.30, 0.05, 1, 0.01, 'Timing (s)'),
    marksFade: P('Marks fade', 0.50, 0, 2, 0.05, 'Timing (s)'),

    distance: P('Target distance (cells)', 7, 2, 15, 0.5, 'Volley'),
    bearing: P('Caster bearing from target (degrees, 0 = north)', 235, 0, 360, 1, 'Volley'),
    needles: P('Needles', 10, 1, 24, 1, 'Volley'),
    spread: P('Landing radius (cells)', 1.3, 0.2, 3, 0.05, 'Volley'),
    hitShare: P('Share aimed at the pawn (demo)', 0.5, 0, 1, 0.05, 'Volley'),
    orbSide: P('Orb beside the caster (cells)', 0.9, 0, 2, 0.05, 'Volley'),
    hover: P('Orb height (cells)', 1.3, 0.3, 3, 0.05, 'Volley'),
    recoil: P('Kick back per shot (cells)', 0.07, 0, 0.3, 0.01, 'Volley'),

    orbSize: P('Orb radius (cells)', 0.30, 0.1, 0.6, 0.01, 'Needle'),
    length: P('Needle length (cells)', 0.55, 0.15, 1.5, 0.05, 'Needle'),
    width: P('Needle width (cells)', 0.10, 0.02, 0.3, 0.01, 'Needle'),
    fan: P('Spike fan before firing', 0.55, 0, 1.5, 0.05, 'Needle'),
    embed: P('Stuck needle depth in the floor (cells)', 0.15, 0, 0.5, 0.01, 'Needle'),
    trail: P('Trail brightness', 0.6, 0, 1, 0.01, 'Needle'),

    shade: P('Sun shading strength', 0.60, 0, 2, 0.01, 'Look'),
    fill: P('Camera-side fill light', 0.35, 0, 1, 0.01, 'Look'),
    ambient: P('Unlit brightness', 0.06, 0, 0.5, 0.01, 'Look'),
    bevelGlow: P('Needle edge line brightness', 0.8, 0, 1, 0.01, 'Look'),
    rings: P('Closing floor rings on a hit', 0.40, 0, 1, 0.01, 'Look'),
    ringRadius: P('Floor ring start radius (cells)', 1.1, 0.3, 3, 0.05, 'Look'),
    sunShadow: P('Sun shadow strength', 0.38, 0, 0.8, 0.01, 'Look'),
    shake: P('Camera shake on the first hit', 0.02, 0, 0.2, 0.01, 'Look'),

    shieldDemo: { label: 'Target wears a shield belt (lab stand-in bubble)', value: true, group: 'Demo' },
    pawns: { label: 'Draw caster and target stand-ins', value: true, group: 'Demo' },
  },

  duration(p) { return times(p, geometry(p, v3(0, 0, 0))).end + p.marksFade; },

  phases(p) {
    const t = times(p, geometry(p, v3(0, 0, 0)));
    return [
      { name: 'Orb up', t: 0 }, { name: 'Spikes', t: t.chargeAt }, { name: 'Fire', t: t.fireAt },
      { name: 'First hit', t: t.firstHit }, { name: 'Orb sinks', t: t.orbSinkAt }, { name: 'Needles sink', t: t.needleSinkAt },
    ];
  },

  events(p) {
    const t = times(p, geometry(p, v3(0, 0, 0)));
    return p.shake > 0 && isFinite(t.firstHit) ? [{ t: t.firstHit, type: 'shake', value: p.shake }] : [];
  },

  draw(seconds, p, { origin, scene }) {
    const s = seconds, g = geometry(p, v3(origin.x, 0, origin.z)), t = times(p, g);
    const sun = scene.shadowVector, light = sunLight(sun);
    const look = { shade: p.shade, fill: p.fill, ambient: p.ambient, bevel: 0.012, bevelGlow: p.bevelGlow, flash: 0 };

    if (p.pawns) {
      draw(MeshPool.plane10, g.target.x, Y.pawn, g.target.z + 0.15, 1, 1, 0, Color.white, pawn);
      draw(MeshPool.plane10, g.caster.x, Y.pawn, g.caster.z + 0.15, 1, 1, 0, Color.white, pawn);
    }

    drawFloor(s, p, t, g);
    drawBubble(s, p, t, g);

    const orb = orbAt(s, p, t, g);
    const orbAlt = Y.front + 0.08;
    if (orb) drawOrb(orb, orbAlt, p, light, sun);

    for (let i = 0; i < g.shots.length; i++) {
      const needle = needleAt(i, s, p, t, g, orb);
      if (!needle) continue;
      let alt;
      if (needle.state === 'spike') alt = dot(needle.T, View) < 0 ? Y.front + 0.005 + i * 0.003 : Y.front + 0.10 + i * 0.006;
      else if (needle.state === 'flying') alt = Y.front + 0.10 + i * 0.006;
      else alt = (g.shots[i].aim.z > g.target.z ? Y.behind : Y.front) + 0.05 + i * 0.006;
      drawSolidShadow(`needle-${i}`, needle, sun, p.sunShadow * (needle.alpha ?? 1));
      drawSolid(`needle-${i}`, needle, alt, light, look);
      if (needle.state === 'flying') drawTrail(i, needle, alt - 0.001, p);
    }

    drawHits(s, p, t, g);
  },
};

// ---------------------------------------------------------------- where everything is

function geometry(p, target) {
  const b = p.bearing * Mathf.Deg2Rad;
  const caster = v3(target.x + Math.sin(b) * p.distance, 0, target.z + Math.cos(b) * p.distance);
  const d = v3(-Math.sin(b), 0, -Math.cos(b));        // caster to target
  const side = v3(-d.z, 0, d.x);
  const spot = add(add(caster, mul(side, p.orbSide)), mul(d, 0.3));   // where the orb comes up
  const centre = add(spot, mul(Up, p.hover));

  const shots = [];
  const hits = Math.round(p.needles * p.hitShare);
  for (let i = 0; i < p.needles; i++) {
    // Hits and misses interleaved, so the volley alternates between the pawn and the floor.
    const hit = p.pawns && Math.floor((i + 1) * hits / p.needles) > Math.floor(i * hits / p.needles);
    let aim;
    if (hit) {
      aim = v3(target.x + (rand(i, 1) - 0.5) * 0.3, 0.45 + rand(i, 2) * 0.25, target.z + (rand(i, 3) - 0.5) * 0.2);
    } else {
      // Keep misses off the pawn's own cell, or they read as hits.
      const a = rand(i, 4) * TAU, r = Mathf.Lerp(0.45, p.spread, Math.sqrt(rand(i, 5)));
      aim = v3(target.x + Math.cos(a) * r, 0, target.z + Math.sin(a) * r);
    }
    // Each needle flies a shallow arc, so it comes down steeply and stands in the floor at an
    // angle instead of lying along it.
    const dir = unit(add(add(aim, mul(centre, -1)), mul(Up, 4 * p.arc)));
    const launch = add(centre, mul(dir, p.orbSize * 0.5 + p.length));   // tip at launch
    const chord = add(aim, mul(launch, -1));
    const span = Math.hypot(Math.hypot(chord.x, chord.z), chord.y) + p.arc;
    shots.push({ hit, aim, dir, launch, chord, flight: span / p.speed, fan: v3(rand(i, 6) - 0.5, rand(i, 7) - 0.5, 0) });
  }
  return { target, caster, d, side, spot, centre, shots };
}

function times(p, g) {
  const chargeAt = p.popup, fireAt = chargeAt + p.charge;
  const launch = (i) => fireAt + i * p.interval;
  let lastLand = fireAt, firstHit = Infinity;
  g.shots.forEach((shot, i) => {
    const land = launch(i) + shot.flight;
    lastLand = Math.max(lastLand, land);
    if (shot.hit) firstHit = Math.min(firstHit, land);
  });
  const orbSinkAt = launch(Math.max(0, g.shots.length - 1)) + p.orbHold;
  const needleSinkAt = lastLand + p.linger;
  return {
    chargeAt, fireAt, launch, firstHit, orbSinkAt, needleSinkAt,
    end: Math.max(orbSinkAt, needleSinkAt) + p.sink,
  };
}

// ---------------------------------------------------------------- orb

function orbAt(s, p, t, g) {
  const r = p.orbSize;
  if (s >= t.orbSinkAt + p.sink) return null;
  let y = Mathf.Lerp(-r, p.hover, easeOutBack(progress(s, 0, p.popup)));
  y -= (p.hover + r) * easeIn(progress(s, t.orbSinkAt, p.sink));
  // A slow bob while it hangs there, and a kick back along the aim with every shot.
  y += 0.03 * Math.sin(s * 7) * progress(s, p.popup, 0.1);
  let back = 0;
  for (let i = 0; i < g.shots.length; i++) {
    const since = s - t.launch(i);
    if (since >= 0) back += p.recoil * Math.exp(-since * 18);
  }
  const x = g.spot.x - g.d.x * back, z = g.spot.z - g.d.z * back;
  return { centre: v3(x, y, z), r };
}

function drawOrb(orb, alt, p, light, sun) {
  const { centre, r } = orb;
  if (centre.y + r <= 0) return;
  // Below the floor the orb is cut: keep the part above ground, squashed like the rods sketch.
  const top = centre.y + r, bottom = Math.max(centre.y - r, 0);
  const shown = Mathf.Clamp01((top - bottom) / (2 * r));
  const x = centre.x, z = centre.z + ((top + bottom) / 2) * Lift;

  if (p.sunShadow > 0)
    draw(MeshPool.plane10, centre.x + sun.x * Math.max(0, centre.y), Y.shadows, centre.z + sun.z * Math.max(0, centre.y),
      r * 2.3, r * 1.7 * shown, 0, new Color(0, 0, 0, p.sunShadow * Mathf.Clamp01(1.3 - centre.y * 0.25)), soft);

  // Rim first, body over it, the order SixPathsGraphics.DrawOrb keeps.
  draw(rimBand, x, alt, z, r, r * shown, 0, Rim.withAlpha(0.9));
  draw(disc, x, alt + 0.002, z, r, r * shown, 0, Body);
  // Shaded as a sphere: a lit side toward the sun as it lands on screen, a dark core, a highlight.
  const lx = light.x, lz = light.z + light.y * Lift * 0.3, ll = Math.hypot(lx, lz) || 1;
  const ox = lx / ll, oz = lz / ll;
  draw(MeshPool.plane10, x + ox * r * 0.38, alt + 0.003, z + oz * r * 0.38 * shown,
    r * 1.45, r * 1.45 * shown, 0, Slate.withAlpha(Math.min(1, 1.2 * p.shade + 0.2)), soft);
  draw(disc, x - ox * r * 0.18, alt + 0.004, z - oz * r * 0.18 * shown, r * 0.62, r * 0.62 * shown, 0, Body.withAlpha(0.7));
  draw(MeshPool.plane10, x + ox * r * 0.45, alt + 0.005, z + oz * r * 0.45 * shown,
    r * 0.50, r * 0.36 * shown, 0, Sheen.withAlpha(0.55), softGlow);
}

// ---------------------------------------------------------------- needles

/** Needle axes pointing along dir; the wide side stays level. */
function axesAlong(dir) {
  const flat = Math.hypot(dir.x, dir.z) > 1e-3 ? unit(cross(Up, dir)) : v3(1, 0, 0);
  return { T: dir, V: flat, W: unit(cross(dir, flat)) };
}

function needleAt(i, s, p, t, g, orb) {
  const shot = g.shots[i];
  const launchAt = t.launch(i);
  const hx = p.length / 2, hy = p.width / 2;
  const since = s - launchAt;
  const body = { hx, hy, vc: 0, corner: 2.4, taper: 1, taperThickness: true, th: p.width };

  if (since < 0) {
    // A spike on the orb: grows out during the charge, fanned out, and swings onto its aim just
    // before it goes.
    if (!orb) return null;
    const grow = easeOut(progress(s, t.chargeAt + rand(i, 8) * p.charge * 0.4, p.charge * 0.6));
    if (grow <= 0.01) return null;
    const aimK = Mathf.Smooth(progress(s, launchAt - 0.10, 0.10));
    const fanned = unit(add(add(shot.dir, mul(g.side, shot.fan.x * p.fan)), mul(Up, shot.fan.y * p.fan)));
    const dir = unit(add(mul(fanned, 1 - aimK), mul(shot.dir, aimK)));
    const tail = add(orb.centre, mul(dir, p.orbSize * 0.5));
    Object.assign(body, axesAlong(dir), { hx: hx * grow, C: add(tail, mul(dir, hx * grow)), state: 'spike' });
    return body;
  }

  if (since < shot.flight) {
    const f = since / shot.flight, { tip, dir } = flightAt(shot, f, p);
    Object.assign(body, axesAlong(dir), { C: add(tip, mul(dir, -hx)), state: 'flying', f, shot });
    return body;
  }

  // Arrived. A hit is gone into the pawn; a miss stays in the floor at its flight angle.
  if (shot.hit) return null;
  const out = easeIn(progress(s, t.needleSinkAt + rand(i, 9) * 0.1, p.sink));
  if (out >= 1) return null;
  const { dir } = flightAt(shot, 1, p);
  const tip = add(shot.aim, mul(dir, p.embed + out * p.length * 1.2));
  Object.assign(body, axesAlong(dir), { C: add(tip, mul(dir, -hx)), state: 'stuck' });
  body.uMax = -body.C.y / Math.min(dir.y, -1e-3);
  if (body.uMax <= -hx) return null;
  return body;
}

/** A thin streak following the arc behind a flying needle, narrowing to nothing. */
function drawTrail(i, needle, alt, p) {
  if (p.trail <= 0) return;
  const pts = [], steps = 6;
  for (let j = 0; j <= steps; j++) {
    const f = needle.f - (j / steps) * Math.min(needle.f, 0.35);
    const { tip, dir } = flightAt(needle.shot, f, p);
    const P3 = add(tip, mul(dir, -p.length));
    pts.push([screenX(P3), screenZ(P3)]);
  }
  const xz = [], tri = [];
  for (let j = 0; j <= steps; j++) {
    const [x, z] = pts[j], [nx0, nz0] = pts[Math.min(j + 1, steps)], [px, pz] = pts[Math.max(j - 1, 0)];
    const dx = nx0 - px, dz = nz0 - pz, len = Math.hypot(dx, dz) || 1;
    const w = p.width * 0.2 * (1 - j / steps);
    xz.push(x - (dz / len) * w, z + (dx / len) * w, x + (dz / len) * w, z - (dx / len) * w);
    if (j < steps) { const v = j * 2; tri.push(v, v + 2, v + 1, v + 1, v + 2, v + 3); }
  }
  const m = mesh(`trail-${i}`);
  m.setFlat(xz, tri);
  draw(m, 0, alt, 0, 1, 1, 0, Rim.withAlpha(p.trail), additive);
}

// ---------------------------------------------------------------- what the needles hit

/** The stand-in shield bubble: flickers at the first hit and shrinks into the pawn. */
function drawBubble(s, p, t, g) {
  if (!p.shieldDemo || !isFinite(t.firstHit)) return;
  const k = progress(s, t.firstHit, 0.35);
  if (k >= 1) return;
  const flicker = k > 0 && k < 0.4 ? 0.5 + 0.5 * Math.sign(Math.sin(s * 90)) : 1;
  const R = 0.85 * (1 - easeIn(progress(k, 0.25, 0.75)));
  const cx = g.target.x, cz = g.target.z + 0.3;
  draw(MeshPool.plane10, cx, Y.front + 0.02, cz, R * 2.2, R * 2.2, 0, Bubble.withAlpha(0.20 * flicker), softGlow);
  draw(bubbleRing, cx, Y.front + 0.021, cz, R, R, 0, Bubble.withAlpha(0.45 * flicker * (1 + k)), additive);
}

function drawHits(s, p, t, g) {
  g.shots.forEach((shot, i) => {
    const land = t.launch(i) + shot.flight, since = s - land;
    if (since < 0 || since > 0.5) return;
    const x = screenX(shot.aim), z = screenZ(shot.aim);
    if (shot.hit) {
      // Gone into the pawn: a purple flash and a short white ring pulled in on it.
      const q = since / 0.3;
      if (q < 1) {
        draw(MeshPool.plane10, x, Y.front + 0.26, z, 0.7 * (1 - q * 0.4), 0.7 * (1 - q * 0.4), 0, Rim.withAlpha(0.9 * (1 - q) ** 2), softGlow);
        const R = Mathf.Lerp(0.45, 0.08, easeIn(q));
        draw(thinRing, x, Y.front + 0.265, z, R, R, 0, White.withAlpha(0.7 * (1 - q)), additive);
      }
    } else {
      // A little dirt where it went in.
      const q = since / 0.4;
      if (q < 1)
        draw(MeshPool.plane10, x, Y.front + 0.004, z + q * 0.08, Mathf.Lerp(0.15, 0.4, q), Mathf.Lerp(0.12, 0.32, q), rand(i, 20) * 360,
          new Color(0.60, 0.54, 0.46, 0.4 * (1 - q)), soft);
    }
  });

  // Rings on the floor closing in on the pawn from the first hit: true circles, drawn flat.
  if (!isFinite(t.firstHit) || p.rings <= 0) return;
  for (let j = 0; j < 2; j++) {
    const u = progress(s, t.firstHit + j * 0.12, 0.40);
    if (u <= 0 || u >= 1) continue;
    const R = Mathf.Lerp(p.ringRadius, 0.2, easeIn(u)), a = p.rings * Math.sin(u * Math.PI);
    draw(thinRing, g.target.x, Y.filth + 0.01 + j * 0.001, g.target.z, R * 1.04, R * 1.04, 0, new Color(0, 0, 0, 0.45 * a));
    draw(thinRing, g.target.x, Y.filth + 0.012 + j * 0.001, g.target.z, R, R, 0, Rim.withAlpha(a), additive);
  }
}

// ---------------------------------------------------------------- the floor

function drawFloor(s, p, t, g) {
  const fade = 1 - Mathf.Smooth(progress(s, t.end, p.marksFade));
  if (fade <= 0) return;

  // Where the orb came up: a hole, dirt, and a few chunks thrown up.
  draw(MeshPool.plane10, g.spot.x, Y.filth, g.spot.z, 0.9, 0.75, 0, Dirt.withAlpha(0.5 * fade * Math.min(1, s * 8)), soft);
  draw(disc, g.spot.x, Y.filth + 0.002, g.spot.z, 0.16, 0.13, 0, new Color(0, 0, 0, 0.55 * fade * Math.min(1, s * 8)));
  for (let k = 0; k < 6; k++) {
    const angle = rand(k, 30) * TAU, speed = 0.5 + rand(k, 31) * 0.9, up = 2 + rand(k, 32) * 1.8, grav = 18;
    const flight = (2 * up) / grav;
    if (s > flight + 0.3) continue;
    const tau = Math.min(s, flight), h = Math.max(0, up * tau - 0.5 * grav * tau * tau);
    const x = g.spot.x + Math.cos(angle) * speed * tau, z = g.spot.z + Math.sin(angle) * speed * tau * 0.85;
    const size = 0.05 + rand(k, 33) * 0.06;
    draw(MeshPool.plane10, x, Y.front + 0.07 + k * 0.0002, z + h * Lift, size, size * 0.8, rand(k, 34) * 360 + tau * 400,
      Dirt.withAlpha(s > flight ? 1 - (s - flight) / 0.3 : 1));
  }

  // Small holes where missed needles went in, left after they sink.
  g.shots.forEach((shot, i) => {
    if (shot.hit || s < t.launch(i) + shot.flight) return;
    draw(disc, shot.aim.x, Y.filth + 0.003, shot.aim.z, 0.05, 0.04, 0, new Color(0, 0, 0, 0.5 * fade));
  });
}
