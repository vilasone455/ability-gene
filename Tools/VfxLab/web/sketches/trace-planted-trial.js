// Planted real weapons — a look trial for the Trace kit (the Origin: Blade rework). Not an ability,
// and nothing in Source/RimArt draws this yet.
//
// The question. The Trace kit plants copies of the weapons a pawn has studied, so it wants each
// weapon's own texture standing in the ground, not one generic sword. RimWorld weapon textures are
// drawn lying flat, seen from above. The early Origin: Blade drew the vanilla longsword texture
// planted and it read as a sword lying on the floor, so that kit switched to two pre-drawn sprites
// (Textures/RimArt/Panoply/Blade.png, BladePlanted.png).
//
// The method. A blade is a flat plane in 3D that carries the weapon's own texture. Its tip, pommel
// and width along the axis come from the texture's alpha, worked out once per weapon. The plane
// gets a lean and a turn, is drawn with the kit's height rule (0.60 cells north per cell up), and
// is cut where it meets the ground, so the buried part is not drawn. The same plane projected
// along the sun is the shadow, so the shadow starts at the hole. Then: a darker copy offset behind
// the face for thickness and two dark bands low on the blade. On the floor: a tight contact
// shadow, cracks running out, a slit as wide as the blade, a thin lip of earth either side (the
// front one drawn over the steel's foot) and a few crumbs. The first version heaped a crescent of
// earth round each blade; the user said it read as a stand, not a blade driven in.
//
// Order, default timings:
//   0.20  six weapons come up, one every 0.22 s.
//         rise:  the cracks run out as the blade comes up hilt first in 0.45 s, crumbs are thrown
//                and land and stay, a puff of dust
//         trace: a wire outline climbs out of the ground, then steel fills it from the ground up
//                behind a bright scan line, then a glint at the top and the wire fades
//   2.00  all six stand: knife, longsword, spear, monosword, two large modded swords
//   2.20  a seventh blade (longsword) flies in at hand height from "Thrown from"
//   2.62  it sticks point first, leaning 28 degrees back toward the thrower, turns its flat side to
//         the camera (in the air it faces up) and quivers for about 0.7 s; the cracks run longer
//         on the far side and the crumbs are thrown that way
// "Look" swaps in the two old answers: the texture flat on the floor (the early attempt) and the
// shipped BladePlanted sprite.
//
// Textures. Textures/RimArt/TraceTrial/ is local only and git-excluded: copies of Melee
// Animation's editor copies of the vanilla knife, longsword (red editor arrow painted out), spear
// and monosword, and two modded swords, with <Name>Outline and <Name>Mask made from their alpha.
// Run make_trace_trial_textures.py to rebuild them; without them the lab shows checkerboards. None
// of it ships: the game would read the weapon's own graphic (material colour included) and make
// the outline and mask at load.
//
// Drawing and port notes:
//   - one custom mesh per blade part: the texture square cut by one or two height planes, at most
//     6 corners, uv carried through, wound clockwise like MeshPool.plane10. Rebuilt only while the
//     blade moves.
//   - facing: a blade whose flat side turns toward north-south collapses into a line (height and
//     north share the screen axis), so the turn stays within the "Face turn" limit. A blade leaning
//     toward the camera gets shorter on screen and at about 60 degrees vanishes into its own cut,
//     so the thrown blade leans 85% less when it comes from due south.
//   - the pawn is a stand-in for scale. Blades draw at the Building layer, under pawns, as a planted
//     blade (a Building) does in game.
import { Color, MaterialPool, Meshes, ShaderDatabase } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow } from './lib/six-paths-impact.js';
import { pawn, ringAt, glint, line, shadowLayer, buildingLayer } from './lib/goku.js';
import {
  smooth, clamp, lerp, D2R, solid, Black, Trace, TraceHot, grey, Weapons, v3, onScreen, pose, tipUnder, upright,
  at3, pommelOf, blade, cutOf, plant, breakOut,
} from './lib/trace.js';

const disc = Meshes.disc(28, 'trace trial disc');
const oldPlanted = MaterialPool.MatFrom('RimArt/Panoply/BladePlanted', ShaderDatabase.Transparent);
const oldFlying = MaterialPool.MatFrom('RimArt/Panoply/Blade', ShaderDatabase.Transparent);
const Caster = new Color(.55, .3, .24);
// The panel's look settings, as blade() takes them.
const look = p => ({ shadow: p.shadow, thick: p.thick });

// The field. x, z: where the blade enters the ground, cells from the origin. lean and turn are
// fractions of the panel's most; dir is the way the top leans (degrees, 0 east, 90 north).
const Field = [
  { w: 'LongSword', x: -1.3, z: .9, lean: .7, dir: 150, turn: -.5 },
  { w: 'Spear', x: .05, z: 1.35, lean: .5, dir: 70, turn: .7 },
  { w: 'LargeSword', x: 1.5, z: .85, lean: .8, dir: 20, turn: -.9 },
  { w: 'Knife', x: -.5, z: -.3, lean: 1, dir: 200, turn: .3 },
  { w: 'MonoSword', x: .75, z: -.5, lean: .4, dir: 110, turn: 1 },
  { w: 'Wyrmslayer', x: 2.4, z: -.35, lean: .6, dir: 60, turn: -.3 },
];
// The lab camera sits about 2 cells north of the chosen cell; the scene is moved up to meet it.
const SceneNorth = 1.6, PawnAt = { x: -2.7, z: .15 }, Landing = { x: -1.35, z: -1.25 };
const Flight = .42, ThrowDistance = 5, ThrowHeight = .85, FlightPitch = 8, StickLean = 28, QuiverHz = 8, OldLean = 13;

function times(p) {
  const first = .2, appear = p.rise + .25;
  const stood = first + (Field.length - 1) * p.stagger + appear;
  const fly = stood + .2, land = fly + Flight;
  return { first, appear, stood, fly, land, end: land + 1.9 };
}

// The pose of field blade f at raise r (0: pommel at the floor, 1: standing).
const fieldPose = (f, p, r) => upright(Weapons[f.w], p.size, { x: f.x, z: f.z }, f.lean * p.lean, f.dir, f.turn * p.turn, p.sink, r);
// The early attempt: the same texture lying on the floor, turned to where it would lean.
function flatPose(f, p) {
  const w = Weapons[f.w], scale = w.image * p.size, L = w.length * scale, d = f.dir * D2R;
  const A = v3(Math.cos(d), 0, Math.sin(d));
  return pose(w, scale, v3(f.x - A.x * L / 2, .04, f.z - A.z * L / 2), A, v3(-Math.sin(d), 0, Math.cos(d)));
}

// The thrown longsword: flying, then stuck and quivering. Returns { b, flying, age }.
function thrownPose(s, p, t) {
  const w = Weapons.LongSword, scale = w.image * p.size, L = w.length * scale;
  const back = { x: Math.cos(p.from * D2R), z: Math.sin(p.from * D2R) };      // toward the thrower
  const across = v3(-back.z, 0, back.x);
  if (s < t.land) {
    const u = clamp((s - t.fly) / Flight), pitch = FlightPitch * D2R;
    const A = v3(back.x * Math.cos(pitch), Math.sin(pitch), back.z * Math.cos(pitch));
    const tip = v3(Landing.x + back.x * ThrowDistance * (1 - u), ThrowHeight * Math.pow(1 - u, 1.3) + .02, Landing.z + back.z * ThrowDistance * (1 - u));
    return { b: pose(w, scale, tip, A, across), flying: true, age: s - t.fly };
  }
  const age = s - t.land, settle = smooth(age / .06);
  const stuck = StickLean * (1 - .85 * Math.max(0, -back.z));
  const lean = lerp(90 - FlightPitch, stuck, settle) + p.quiver * Math.exp(-age / .22) * Math.sin(age * QuiverHz * Math.PI * 2) * settle;
  const l = lean * D2R, A = v3(back.x * Math.sin(l), Math.cos(l), back.z * Math.sin(l));
  // In the air its flat side faces up. Stuck, it turns that side to the camera (across east-west),
  // or the texture is seen at a slant and its own shadow shows more sword than the blade does.
  const east = across.x >= 0 ? 1 : -1, face = v3(lerp(across.x, east, settle), 0, lerp(across.z, 0, settle));
  return { b: pose(w, scale, tipUnder(Landing, A, p.sink * L * .85 * settle), A, face), flying: false, age };
}

// The seventh blade. In flight it is the texture seen from above (a blade lying flat in the air);
// after it sticks it is drawn like the field.
function drawThrown(key, { b, flying, age }, o, p, sun, strength, layer) {
  const shifted = { ...b, tip: v3(b.tip.x + o.x, b.tip.y, b.tip.z + o.z) };
  if (p.look === 'current sprite') {
    const tipDir = Math.atan2(-b.A.x, -b.A.z) / D2R, g = { x: o.x + Landing.x, z: o.z + Landing.z };
    if (flying) sprite(onScreen(at3(shifted, { u: .5, v: .5 })), 1.05, 1.05, grey(1), oldFlying, layer, tipDir);
    else sprite({ x: g.x, z: g.z + .28 * p.size }, p.size, p.size, grey(1), oldPlanted, layer + .001, 0);
    return;
  }
  if (flying) {
    // a light streak behind it along the flight
    const tail = onScreen(pommelOf(shifted)), len = Math.hypot(b.A.x, b.A.z) || 1;
    line(`${key} streak`, [tail, { x: tail.x + b.A.x / len * 1.4, z: tail.z + b.A.z / len * 1.4 }], .18, grey(1).withAlpha(.25), solid, layer - .001, 'end');
    blade(key, shifted, sun, strength, layer, { ...look(p), upright: false });
    return;
  }
  if (p.look === 'planted') {
    const cut = cutOf(shifted, p.sink);
    const ahead = { x: -Math.cos(p.from * D2R), z: -Math.sin(p.from * D2R) };
    plant(key, cut, layer, 21, sun, { dirt: p.dirt, grow: smooth(age / .1), forward: ahead });
    breakOut(`${key} kick`, cut, age, 21, ahead);
    if (age < .15) glint(`${key} spark`, { x: cut.x, z: cut.z + .08 }, .3, 1 - age / .15);
    blade(key, shifted, sun, strength, layer, look(p));
  } else {
    // the early attempt: it lands and lies there
    const d = Math.atan2(b.A.z, b.A.x), L = b.L;
    const flat = pose(b.w, b.scale, v3(o.x + Landing.x - Math.cos(d) * L * .5, .04, o.z + Landing.z - Math.sin(d) * L * .5),
      v3(Math.cos(d), 0, Math.sin(d)), v3(-Math.sin(d), 0, Math.cos(d)));
    blade(key, flat, sun, strength, layer, { ...look(p), upright: false });
  }
}

export default {
  kit: 'Trace', label: 'Planted real weapons (sketch)',
  params: {
    look: { label: 'Look', value: 'planted', options: ['planted', 'flat (old attempt)', 'current sprite'], group: 'Compare' },
    appear: { label: 'Appear', value: 'rise', options: ['rise', 'trace'], group: 'Compare' },
    actors: { label: 'Stand-in pawn', value: true, group: 'Compare' },
    size: P('Size (x image)', 1.3, .8, 2.5, .05, 'Blade'),
    sink: P('Buried (share of length)', .2, .05, .45, .01, 'Blade'),
    lean: P('Lean, most (degrees)', 16, 0, 40, 1, 'Blade'),
    turn: P('Face turn, most (degrees)', 30, 0, 60, 1, 'Blade'),
    thick: P('Thickness (cells)', .03, 0, .08, .005, 'Blade'),
    dirt: P('Cracks and earth (size)', 1, 0, 2, .05, 'Ground'),
    shadow: P('Shadow opacity', .42, 0, .8, .02, 'Ground'),
    from: P('Thrown from (degrees)', 200, 0, 360, 5, 'Thrown blade'),
    quiver: P('Quiver (degrees)', 10, 0, 25, 1, 'Thrown blade'),
    stagger: P('Between blades', .22, .05, .5, .01, 'Timing (s)'),
    rise: P('Rise or trace', .45, .2, 1.2, .05, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Blades appear', t: t.first }, { name: 'Standing', t: t.stood }, { name: 'Thrown', t: t.fly }, { name: 'Sticks', t: t.land }];
  },
  events(p) { return [{ t: times(p).land, type: 'shake', value: .035 }]; },

  draw(s, p, { origin: cell, scene }) {
    const t = times(p), o = { x: cell.x, z: cell.z + SceneNorth };
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const at = (x, z) => ({ x: o.x + x, z: o.z + z });
    if (p.actors) pawn(at(PawnAt.x, PawnAt.z), Caster, sun, strength, { hair: true });

    // Everything standing, sorted north first so nearer blades cover farther ones.
    const standing = [];
    Field.forEach((f, i) => {
      const age = s - t.first - i * p.stagger;
      if (age >= 0) standing.push({ f: { ...f, x: o.x + f.x, z: o.z + f.z }, i, age, z: o.z + f.z });
    });
    const throwIn = s >= t.fly ? thrownPose(s, p, t) : null;
    if (throwIn && !throwIn.flying) standing.push({ thrown: throwIn, i: 9, z: o.z + Landing.z });
    standing.sort((a, b) => b.z - a.z);

    standing.forEach((e, rank) => {
      const layer = buildingLayer + rank * .005, key = `trace trial ${e.i}`;
      if (e.thrown) { drawThrown(key, e.thrown, o, p, sun, strength, layer); return; }
      const f = e.f, age = e.age;
      if (p.look === 'current sprite') {
        const lean = f.lean * OldLean * (f.dir > 90 && f.dir < 270 ? -1 : 1), size = p.size;
        const r = lean * D2R;
        draw(disc, f.x + sun.x * .05, shadowLayer + .002, f.z + sun.z * .05, .16 * size, .05 * size, 0, Black.withAlpha(.3 * clamp(age / p.rise)));
        sprite({ x: f.x + Math.sin(r) * .28 * size, z: f.z + Math.cos(r) * .28 * size }, size, size, grey(1).withAlpha(clamp(age / p.rise)), oldPlanted, layer + .001, lean);
        return;
      }
      if (p.look === 'flat (old attempt)') {
        blade(key, flatPose(f, p), sun, strength, layer, { ...look(p), alpha: clamp(age / p.rise), upright: false });
        return;
      }
      if (p.appear === 'rise') {
        const r = 1 - Math.pow(1 - clamp((age - .06) / p.rise), 3);
        const b = fieldPose(f, p, r), cut = cutOf(fieldPose(f, p, 1), p.sink);
        plant(key, cut, layer, e.i + 1, sun, { dirt: p.dirt, grow: .3 + .7 * smooth(r * 1.3) });
        breakOut(key, cut, age - .04, e.i + 1);
        if (r > 0) blade(key, b, sun, strength, layer, look(p));
      } else {
        const b = fieldPose(f, p, 1), cut = cutOf(b, p.sink), top = pommelOf(b).y + .05;
        const wireTime = .45 * p.rise, fillTime = .55 * p.rise;
        const wire = top * smooth(age / wireTime), fill = top * smooth((age - wireTime) / fillTime);
        const wireAlpha = .95 * (1 - smooth((age - p.rise - .05) / .3));
        plant(key, cut, layer, e.i + 1, sun, { dirt: p.dirt, grow: smooth(age / wireTime) });
        const lit = smooth(age / .12) * (1 - smooth((age - p.rise) / .35));
        sprite({ x: cut.x, z: cut.z }, (cut.half * 2 + .8) * (.6 + .4 * smooth(age / p.rise)), .5, Trace.withAlpha(.3 * lit), glow, Floor + .005);
        ringAt(cut, .2 + .25 * smooth(age / p.rise), Trace.withAlpha(.22 * lit), Floor + .006);
        blade(key, b, sun, strength, layer, { ...look(p),
          fillTo: age < wireTime ? -1 : (fill >= top - .001 ? Infinity : fill),
          wireTo: wire, wireAlpha, scan: age > wireTime && fill < top - .001 ? fill : -1,
        });
        const g = age - p.rise;
        if (g >= 0 && g < .3) glint(`${key} glint`, onScreen(pommelOf(b)), .38, Math.sin(g / .3 * Math.PI), TraceHot, g * 60);
      }
    });

    if (throwIn && throwIn.flying) drawThrown('trace trial thrown', throwIn, o, p, sun, strength, Y + .01);
  },
};
