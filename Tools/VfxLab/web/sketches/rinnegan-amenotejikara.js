// Amenotejikara — technique proposal for a Rinnegan eye kit, not the game. Nothing in Source/RimArt
// draws this yet.
//
// What it is for (proposed, none of it agreed; every number is a placeholder). The caster picks two
// targets and their places are exchanged at once. Targets: the caster, any pawn, an item on the
// ground, or a projectile held by Amenoyodomi. Range 12 cells, line of sight to both, 0 s warmup,
// 3 s cooldown (the eye-strain cost is still open). A pawn cannot be put on a cell it cannot stand
// on; the swap then fails. A projectile keeps its heading and speed, its destination is recomputed
// from the new place. No hand signs, so nothing shows before the swap.
// Differs from the Anchor clap (marks first, whole map, cards and a red puff, no line) and Flying
// Thunder God (one way, a line joining the ends): here nothing joins the two ends either, and the
// sign is the Rinnegan's own ring pattern at each end.
//
// Order, with the default timings:
//   0.00-0.50  before: the three scenarios show who stands where. A held kunai drifts east at 1% of
//              its flight speed (0.24 cells/s) inside a small pulsing ring
//   0.50  cast and swap on one frame: a small violet 4-point star on the caster's eye, 0.1 s. The
//         occupants change places. At both ends the Rinnegan pattern appears on the floor: 4 rings
//         (radius 1.2 cells, so the two inner rings clear the pawn's body), pupil, 6 tomoe (3 on
//         ring 1, 3 on ring 2). Screen negative flash (inverted, pulled 25% toward grey, 0.12 s,
//         then back over 0.1 s): whole screen, or a disc at each end, or off
//   0.50-0.60  a pale ghost of each old occupant crosses to the other end, the two passing mid-way,
//         fading over the second half. This is what says which two things exchanged
//   0.50-0.64  the rings ripple out to 1.7 cells and the tomoe turn 60 degrees
//   0.50-0.70  a faint afterimage of the old occupant at each end
//   0.64-1.14  the pattern fades
// No pinch-to-sliver: that is Flying Thunder God's leaving picture. No camera shake: a swap is not
// an attack.
//
// Drawing: everything is a level circle or a flat quad, so no per-facing method. The pattern is
// drawn around the thing it swaps, on the floor for a pawn and around the kunai for a held kunai
// (a bullet in RimWorld is drawn on the ground at its cell, so there is no height to lift it by).
// Pawns are two-disc stand-ins. The negative flash is InvertShader, which in C# is
// Hidden/Internal-Colored with _SrcBlend OneMinusDstColor, _DstBlend OneMinusSrcAlpha, drawn with
// colour (a, a, a, a) on AltitudeLayer.MetaOverlays, then a mid-grey Transparent quad on top to
// pull the colours toward grey. Full screen is a quad over the camera's view rect.
import { AltitudeLayer, Color, InvertShader, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, at, sprite, glow, trail } from './lib/six-paths-impact.js';
import { figure, kunaiMat, whiteGlow, CasterColour, EnemyColour } from './lib/flying-thunder-god.js';

const smooth = Mathf.Smooth, lerp = Mathf.Lerp;
const pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const topLayer = AltitudeLayer.MetaOverlays.AltitudeFor();
const invertMat = MaterialPool.MatFrom('white', InvertShader);
const greyMat = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const flatDisc = Meshes.disc(48, 'amenotejikara disc');

// Decided looks.
const Lavender = new Color(.80, .74, .98), LavenderDeep = new Color(.46, .36, .78), EyeStar = new Color(.90, .84, 1);
const SecondEnemy = new Color(.62, .30, .30);
const Lead = .5, EyeLife = .1, AfterLife = .2, Stroke = .035, GhostW = .5, GhostH = .95;
const RingFractions = [.28, .52, .76, 1];        // of the pattern radius
const KunaiSpeed = 24 * .01;                      // 40 speed = 24 cells/s; held at 1%
const HeldRing = .32;
const Scenarios = ['caster <-> enemy', 'enemy <-> enemy', 'enemy <-> held kunai'];
const Flashes = ['full screen', 'disc at each end', 'off'];

// One band per ring, stroke Stroke cells at the base radius 1.2.
const ringMesh = RingFractions.map((f, i) => Meshes.band(1 - Stroke / (1.2 * f), 1, 72, `amenotejikara ring ${i}`));

function times(p) {
  const swap = Lead, rippled = swap + p.ripple, end = rippled + p.fade + .4;
  return { cast: swap, swap, rippled, end };
}

// The Rinnegan pattern: a level circle centred on c. radius is the outer ring, turn the tomoe
// angle in radians.
function pattern(key, c, radius, turn, alpha, layer) {
  if (alpha <= 0) return;
  sprite(c, radius * 2.6, radius * 2.6, LavenderDeep.withAlpha(.35 * alpha), glow, layer);
  RingFractions.forEach((f, i) => draw(ringMesh[i], c.x, layer + .002, c.z, radius * f, radius * f, 0, Lavender.withAlpha(alpha)));
  draw(flatDisc, c.x, layer + .003, c.z, radius * .09, radius * .09, 0, Lavender.withAlpha(alpha));
  // Tomoe: a round head on the ring with a tail running back along it.
  for (let i = 0; i < 6; i++) {
    const ringR = radius * RingFractions[i < 3 ? 0 : 1], a0 = turn + i * (Math.PI * 2 / 3) + (i < 3 ? 0 : Math.PI / 3);
    const head = { x: c.x + Math.cos(a0) * ringR, z: c.z + Math.sin(a0) * ringR };
    draw(flatDisc, head.x, layer + .004, head.z, radius * .075, radius * .075, 0, Lavender.withAlpha(alpha));
    const pts = [];
    for (let k = 0; k <= 6; k++) {
      const a = a0 - k / 6 * .75;
      pts.push({ x: c.x + Math.cos(a) * ringR, z: c.z + Math.sin(a) * ringR });
    }
    trail(`${key} tomoe ${i}`, pts, radius * .1, Lavender.withAlpha(alpha), layer + .004);
  }
}

// A 4-point star: two crossed additive slivers over a soft spot.
function star(pos, size, alpha) {
  if (alpha <= 0) return;
  sprite(pos, size * 1.2, size * 1.2, LavenderDeep.withAlpha(alpha * .7), glow, Y + .2);
  draw(MeshPool.plane10, pos.x, Y + .21, pos.z, size * 2, size * .12, 0, EyeStar.withAlpha(alpha), whiteGlow);
  draw(MeshPool.plane10, pos.x, Y + .21, pos.z, size * .12, size * 2, 0, EyeStar.withAlpha(alpha), whiteGlow);
}

// The held kunai: on its cell, pointing along its heading (degrees, 0 east), inside Amenoyodomi's
// small ring.
function heldKunai(pos, deg, s, alpha, ring) {
  if (alpha <= 0) return;
  if (ring) {
    const pulse = .5 + .5 * Math.sin(s * 5);
    draw(ringMesh[3], pos.x, Floor + .02, pos.z, HeldRing * (.9 + .1 * pulse), HeldRing * (.9 + .1 * pulse), 0, Lavender.withAlpha(.35 * alpha));
  }
  sprite(pos, .5, .5, Color.white.withAlpha(alpha), kunaiMat, pawnLayer + .01, 90 - deg);
}

export default {
  kit: 'Rinnegan', label: 'Amenotejikara (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    distance: P('Distance between the ends (cells)', 8, 2, 12, .5, 'Showcase'),
    flash: { label: 'Negative flash', value: Flashes[0], options: Flashes, group: 'Flash' },
    hold: P('Negative held', .12, .04, .3, .01, 'Flash'),
    back: P('Back to normal over', .1, .04, .3, .01, 'Flash'),
    grey: P('Pull toward grey (0-1)', .25, 0, 1, .05, 'Flash'),
    local: P('Disc radius (cells)', 1.5, .8, 3, .1, 'Flash'),
    cross: P('Ghosts cross', .1, .05, .3, .01, 'Timing (s)'),
    ripple: P('Ripple out', .14, .06, .3, .01, 'Timing (s)'),
    fade: P('Pattern fades', .5, .2, 1.2, .05, 'Timing (s)'),
    radius: P('Pattern radius (cells)', 1.2, .6, 1.6, .05, 'Pattern'),
    grow: P('Ripples out to (cells)', 1.7, 1, 2.4, .05, 'Pattern'),
    turn: P('Tomoe turn (degrees)', 60, 0, 180, 5, 'Pattern'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Before', t: 0 }, { name: 'Swap + flash', t: t.swap },
    { name: 'Pattern fades', t: t.rippled },
  ]; },
  events(p) { return [{ t: times(p).swap, type: 'sound', def: 'AG_Amenotejikara' }]; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const half = p.distance / 2, swapped = s >= t.swap, age = s - t.swap;
    const casterEnd = p.scenario === Scenarios[0];
    const casterHome = casterEnd ? { x: o.x - half, z: o.z } : { x: o.x, z: o.z - 1.6 };

    // The kunai drifts all through the clip; the swap moves it, not its heading or speed.
    const drift = KunaiSpeed * s;
    const west = { x: o.x - half, z: o.z }, east = { x: o.x + half, z: o.z };
    const kunaiEast = { x: east.x + drift, z: east.z }, kunaiWest = { x: west.x + drift - (swapped ? KunaiSpeed * t.swap : 0), z: west.z };

    // Ends: what stands there before and after, and where the pattern is centred.
    const ends = casterEnd
      ? [{ pos: west, before: 'caster', after: 'enemy' }, { pos: east, before: 'enemy', after: 'caster' }]
      : p.scenario === Scenarios[1]
        ? [{ pos: west, before: 'enemy', after: 'enemy2' }, { pos: east, before: 'enemy2', after: 'enemy' }]
        // The enemy lands where the kunai was at the swap; the kunai goes on east from the enemy's cell.
        : [{ pos: west, before: 'enemy', after: 'kunai' }, { pos: { x: east.x + KunaiSpeed * t.swap, z: east.z }, before: 'kunai', after: 'enemy' }];
    const colourOf = who => who === 'caster' ? CasterColour : who === 'enemy' ? EnemyColour : SecondEnemy;

    // A caster who is not an end stands to the south and watches.
    if (!casterEnd) figure(casterHome, CasterColour, 1, 0, sun, strength);
    // The eye: a small star at the caster's head, on the cast.
    const eyeAge = s - t.cast;
    if (eyeAge >= 0 && eyeAge < EyeLife) star({ x: casterHome.x + .04, z: casterHome.z + .62 }, .22, 1 - eyeAge / EyeLife);

    ends.forEach((e, n) => {
      const who = swapped ? e.after : e.before, old = e.before;
      const pos = who === 'kunai' ? (swapped ? kunaiWest : kunaiEast) : e.pos;
      if (who === 'kunai') heldKunai(pos, 0, s, 1, true);
      else figure(pos, colourOf(who), 1, 0, sun, strength);

      // Afterimage of who was here, pale and fading.
      if (swapped && age < AfterLife) {
        const a = .35 * (1 - age / AfterLife);
        if (old === 'kunai') heldKunai({ x: e.pos.x, z: e.pos.z }, 0, s, a, false);
        else sprite(at(e.pos, 0, .38), .3, .8, Lavender.withAlpha(a), glow, pawnLayer + .02);
      }

      // The ghost of the old occupant crossing to the other end: a pale stretched copy that
      // fades over the second half of the crossing. A kunai's ghost is the kunai itself, pale.
      if (swapped && age < p.cross) {
        const u = age / p.cross, other = ends[1 - n].pos, a = 1 - Math.max(0, u - .5) * 2;
        const along = v => ({ x: lerp(e.pos.x, other.x, v), z: lerp(e.pos.z, other.z, v) });
        const g = along(u), lift = old === 'kunai' ? 0 : .38;
        // A streak a quarter of the way back along the crossing, so the ghost reads as moving.
        const pts = [];
        for (let k = 0; k <= 6; k++) pts.push(at(along(Math.max(0, u - .25 * (1 - k / 6))), 0, lift));
        trail(`amenotejikara ghost ${n}`, pts, .3, LavenderDeep.withAlpha(.7 * a), Y + .09);
        if (old === 'kunai') heldKunai(g, 0, s, a, false);
        else sprite(at(g, 0, lift), GhostW, GhostH, Lavender.withAlpha(.9 * a), glow, Y + .1);
      }

      // The pattern: on at the swap, ripples out and turns, then fades.
      const outA = swapped ? 1 - smooth((s - t.rippled) / p.fade) : 0;
      const rip = smooth(age / p.ripple);
      const radius = lerp(p.radius, p.grow, rip), turn = p.turn * rip * Mathf.Deg2Rad + n * .4;
      pattern(`amenotejikara ${n}`, who === 'kunai' ? pos : e.pos, radius, turn, outA, Floor + .03);
    });

    // Negative flash.
    if (p.flash !== Flashes[2] && swapped) {
      const a = age < p.hold ? 1 : 1 - smooth((age - p.hold) / p.back);
      if (a > 0) {
        const whole = p.flash === Flashes[0];
        const cover = (mat, colour, lift) => {
          if (whole) draw(MeshPool.plane10, o.x, topLayer + lift, o.z, 400, 400, 0, colour, mat);
          else for (const e of ends) draw(flatDisc, e.pos.x, topLayer + lift, e.pos.z, p.local, p.local, 0, colour, mat);
        };
        cover(invertMat, new Color(a, a, a, a), 0);
        if (p.grey > 0) cover(greyMat, new Color(.5, .5, .5, p.grey * a), .001);
      }
    }
  },
};
