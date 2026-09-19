// Rasengan — technique proposal for the kunai belt, not the game. Nothing in Source/RimArt draws
// this yet. The heavy hit of the Flying Thunder God kit. There is no run in it: the ball is formed
// standing still, which is what a RimWorld warmup is, and the teleport closes the distance.
//
// What it is for (proposed, none of it agreed; every number is a placeholder).
//   With a marked enemy: target a pawn that has the caster's kunai in it, range 29.9 cells, no line
//   of sight. Warmup 0.6 s standing still while the ball forms in the hand. Then the caster
//   teleports to the cell behind the target, as the plain jump does, and thrusts at once.
//   Without a mark: a touch-range ability, range 1.9. The pawn walks next to the target as the game
//   already does for any touch-range ability, then the same 0.6 s warmup and thrust. No special
//   movement is drawn.
//   The hit: 30 blunt damage (a kunai is 12), 40% armor penetration. The target is thrown 3 cells
//   away from the caster, turning as it goes, and is stunned 2 s. If something solid stops it
//   sooner it takes +10 damage. Cooldown 30 s. The plain jump keeps its x1.5 slash at 20 s.
//   Because the caster lands behind the target, a teleported Rasengan throws the target back
//   toward where the caster came from.
//
// Order, with the default timings (teleporting):
//   0.00  stand
//   0.40  the ball forms in the hand over 0.6 s, growing to 0.4 cells across and unsteady until it
//         is full size. It is drawn as a sphere: blue shell with a dark rim, 5 white rings that tilt
//         and turn like orbits, 3 arcs inside, a white core that beats. 10 threads of chakra curl
//         into the palm from 1.2 cells. On the floor: blue light under the ball, wind rings that
//         open outward from the caster's feet, dust carried out with them. The floor script and
//         brackets of the jump are written meanwhile.
//   1.00  teleport: sliver, line, star glint at both ends, the ball goes with the caster
//   1.09  thrust: the caster leans in 0.2 cells and the ball reaches the target's body at 1.15
//   1.15  the grind, 0.3 s: the ball swells to 2.2x over the target's body, its rings turn twice as
//         fast, a two-armed spiral turns on it, 12 sparks fly off round it, the target shakes and
//         goes pale, small camera shake
//   1.45  release: the sphere bursts to 1.6 cells, 14 white lines shoot outward for 0.12 s, a
//         two-armed spiral shock opens to 1.5 cells over 0.3 s, camera shake; the target is thrown
//         3 cells in 0.35 s, turning, with a double helix behind it and 5 rings that open across
//         the path as it passes (the vortex), and a groove of dust on the floor; it ends lying
//         down on a scorch. With the wall on, it stops one cell short of the wall at full speed
//         with a second flash and shake.
//
// Drawing: the ball, the spirals and the wind rings are level circles and the throw is a flat line
// turned with the aim, so no per-facing method. Rings and orbits are one unit ring mesh built once
// and scaled into circles and ellipses; threads, sparks, spirals and the helix are strip meshes
// rebuilt while they show. The ball is blue-white, not the kit's gold: it is the source's colour
// and is only ever a ball and one hit. Teleport shapes come from lib/flying-thunder-god.js. Pawns
// and the wall are stand-ins. The caster plays RimArt_RasenganForm and RimArt_RasenganThrust
// (make_rasengan_anim.py) and the ball sits on the clip's hand; the clip's contact (0.06 s) and burst
// (0.36 s) are this file's Reach and Press. With the stand-in caster the ball is drawn at chest
// height (0.3 cells north of the feet).
import { AltitudeLayer, Color, Mathf, Meshes, MeshPool } from '../js/engine.js';
import { playClip } from '../js/animation.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, circle, glow, soft, rand } from './lib/six-paths-impact.js';
import {
  Gold, Ink, Skin, CasterColour, EnemyColour, whiteGlow, Behind, StripBack, StripGlyphs,
  star, figure, downed, sparks, leave, jumpLine, script, brackets, strip, streak, stuckKunai,
} from './lib/flying-thunder-god.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01;
const disc = Meshes.disc(40, 'rasengan disc');
const ring = Meshes.band(.86, 1, 40, 'rasengan ring'), thinRing = Meshes.band(.94, 1, 48, 'rasengan thin ring');
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const buildingLayer = AltitudeLayer.Building.AltitudeFor();
const Blue = new Color(.3, .62, 1), Deep = new Color(.1, .32, .9), Sky = new Color(.5, .78, 1), Ice = new Color(.78, .91, 1), White = new Color(1, 1, 1);
const Dust = new Color(.52, .45, .37), Stone = new Color(.36, .34, .33), StoneTop = new Color(.5, .48, .46);
// Decided values. The panel keeps only what is still being tuned.
const Lead = .4, Chest = .3, Hand = .32, Lean = .2, Reach = .06, Press = .3, Swell = 1.2, Unsteady = .16;
// Orbit rings of the ball: [tilt speed, turn speed (degrees per second), starting angle].
const Orbits = [[7, 90, 0], [-9, -120, 36], [11, 70, 72], [-13, -100, 108], [8, 140, 144]];
const Threads = 10, WindRings = 3, WindEvery = .45, Puffs = 8, GrindSparks = 12;
const BurstTime = .14, BurstRadius = 1.6, BurstLines = 14, ShockTime = .3, ShockRadius = 1.5, ShockTurns = 1.2;
const TrailLength = 1.8, TrailWave = .2, VortexRings = 5, VortexTime = .4, Tumble = 540, Hop = .45, Lift = .6;
const Tail = 1.3, LineTime = .08, FlashTime = .22;

const teleports = p => p.scenario === 'teleports to a marked enemy';
function times(p) {
  const cast = Lead, formed = cast + p.form, tele = teleports(p);
  const arrive = tele ? formed + p.squeeze : formed, thrust = arrive + (tele ? .04 : .06), hit = thrust + Reach, release = hit + Press;
  const thrown = p.wall ? p.throwDist - 1 : p.throwDist, land = release + p.fly * thrown / p.throwDist;
  return { cast, formed, go: formed, arrive, thrust, hit, release, thrown, land, end: land + Tail };
}

// The ball as a sphere: shell with a dark rim, five rings that tilt and turn like orbits, three arcs
// inside, a core that beats. power 0..1 is the grind: everything turns up to twice as fast.
function ball(at, size, s, alpha, power) {
  if (size <= .01 || alpha <= 0) return;
  const r = size / 2, fast = 1 + power, beat = 1 + .1 * Math.sin(s * 42);
  sprite(at, size * 3.2, size * 3.2, Blue.withAlpha(.6 * alpha), glow, Y + .1);
  draw(disc, at.x, Y + .11, at.z, r, r, 0, Blue.withAlpha(.9 * alpha));
  draw(disc, at.x, Y + .111, at.z, r * .84, r * .84, 0, Sky.withAlpha(.8 * alpha));
  draw(thinRing, at.x, Y + .112, at.z, r, r, 0, Deep.withAlpha(alpha));
  Orbits.forEach(([tilt, turn, start], k) => {
    const q = Math.cos(s * tilt * fast + k * 1.3), flat = Math.max(.1, Math.abs(q));
    draw(ring, at.x, Y + .113 + k * .0004, at.z, r * .95, r * .95 * flat, start + s * turn * fast, White.withAlpha((.45 + .55 * flat) * alpha));
  });
  [[.62, 1, 150], [.44, -1.4, 170], [.27, 1.9, 200]].forEach(([share, speed, span], k) => {
    const outer = [], inner = [], start = s * 720 * speed * fast + k * 120, steps = 10;
    for (let i = 0; i <= steps; i++) {
      const v = i / steps, ang = (start + span * v) * Mathf.Deg2Rad, w = Math.sin(v * Math.PI) * r * .12, d = r * share;
      outer.push({ x: at.x + Math.cos(ang) * (d + w), z: at.z + Math.sin(ang) * (d + w) });
      inner.push({ x: at.x + Math.cos(ang) * (d - w), z: at.z + Math.sin(ang) * (d - w) });
    }
    strip(`rasengan arc ${k}`, outer, inner, White.withAlpha(.9 * alpha), undefined, Y + .116 + k * .001);
  });
  draw(disc, at.x, Y + .12, at.z, r * .26 * beat, r * .26 * beat, 0, White.withAlpha(alpha));
  sprite(at, size * (.7 - .25 * power) * beat, size * (.7 - .25 * power) * beat, White.withAlpha(alpha * (1 - .45 * power)), glow, Y + .121);   // dimmer in the grind, or the big ball washes out to white
}
// A spiral of `arms` arms round c, out to radius, turned by spin degrees. Used on the ball while it
// grinds and for the shock after it.
function spiral(key, c, radius, turns, arms, spin, width, colour, material, layer) {
  for (let arm = 0; arm < arms; arm++) {
    const outer = [], inner = [], steps = 36;
    for (let i = 0; i <= steps; i++) {
      const w = i / steps, ang = (w * turns * 360 + spin + arm * 360 / arms) * Mathf.Deg2Rad, rad = radius * (.2 + .8 * w), half = width * Math.sin(w * Math.PI) + .003;
      outer.push({ x: c.x + Math.cos(ang) * (rad + half), z: c.z + Math.sin(ang) * (rad + half) });
      inner.push({ x: c.x + Math.cos(ang) * (rad - half), z: c.z + Math.sin(ang) * (rad - half) });
    }
    strip(`${key} ${arm}`, outer, inner, colour, material, layer);
  }
}

// The thrown pawn: the two discs turned by angle round the body's middle, h cells off the ground.
function tumbling(pos, colour, angle, h, sun, strength) {
  const r = angle * Mathf.Deg2Rad, mid = { x: pos.x, z: pos.z + .3 + h * Lift };
  sprite({ x: pos.x + sun.x * (.3 + h), z: pos.z + sun.z * (.3 + h) }, .8, .4, Ink.withAlpha(strength), soft, shadowLayer);
  draw(disc, mid.x, pawnLayer, mid.z, .22, .32, -angle, colour);
  draw(disc, mid.x - Math.sin(r) * .38, pawnLayer + .002, mid.z + Math.cos(r) * .38, .16, .17, 0, Skin);
}

export default {
  kit: 'Kunai belt', label: 'Rasengan (sketch)',
  params: {
    scenario: { label: 'The caster', value: 'teleports to a marked enemy', options: ['teleports to a marked enemy', 'is already next to the enemy'], group: 'Showcase' },
    wall: { label: 'A wall stands in the way of the throw', value: false, group: 'Showcase' },
    look: { label: 'The caster is drawn as', value: 'animation clips', options: ['animation clips', 'stand-in'], group: 'Showcase' },
    aim: P('Direction to the enemy (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Distance to the enemy when teleporting (cells)', 7, 4, 14, .5, 'Showcase'),
    form: P('Ball forms (the warmup)', .6, .2, 1.5, .05, 'Timing (s)'),
    squeeze: P('Caster narrows / widens', .05, .02, .2, .01, 'Timing (s)'),
    fly: P('Target is in the air', .35, .15, 1, .05, 'Timing (s)'),
    ballSize: P('Ball across (cells)', .4, .2, .8, .02, 'Shape'),
    swirl: P('Air and dust drawn in from (cells)', 1.2, .5, 2.5, .1, 'Shape'),
    throwDist: P('Target is thrown (cells)', 3, 2, 6, 1, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Stand', t: 0 }, { name: 'Ball forms', t: t.cast }, ...(teleports(p) ? [{ name: 'Teleport', t: t.go }] : []),
    { name: 'Thrust', t: t.thrust }, { name: 'Grind', t: t.hit }, { name: 'Release / thrown', t: t.release }, { name: p.wall ? 'Hits the wall' : 'Lands', t: t.land },
  ]; },
  events(p) { const t = times(p); return [{ t: t.hit, type: 'shake', value: .03 }, { t: t.release, type: 'shake', value: .07 }, ...(p.wall ? [{ t: t.land, type: 'shake', value: .04 }] : [])]; },

  draw(s, p, { origin: centre, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a), tele = teleports(p);
    // dir is the way the thrust and the throw go: back along the aim after a teleport, along it otherwise.
    const dir = tele ? -1 : 1;
    // The chosen cell is the middle of what happens, so all of it is in view. o is the enemy's cell.
    const mid = tele ? (1 - p.distance) / 2 : (p.throwDist - 1) / 2, o = { x: centre.x - ca * mid, z: centre.z - sa * mid };
    const place = (along, across = 0) => ({ x: o.x + along * ca - across * sa, z: o.z + along * sa + across * ca });
    const home = tele ? place(-p.distance) : place(-1), spot = tele ? place(Behind) : home;

    // --- the jump's floor script, written while the ball forms -------------------------------------------
    if (tele) {
      const write = p.form * .4, written = (s - t.cast) / write, burn = (s - t.arrive - FlashTime) / 1;
      if (written > 0 && burn < 1) {
        sprite(place((Behind - StripBack) / 2), 1.8, .5, Gold.withAlpha(.3 * clamp(written) * (1 - smooth(burn))), glow, Floor + .005, -p.aim);
        script(o, p.aim, -StripBack, StripGlyphs, s, t.cast, write, burn);
        brackets(spot, p.aim, clamp((written - .8) / .2) * (1 - clamp((burn - .85) / .15)));
      }
    }

    // --- the wall, and the groove the thrown pawn leaves --------------------------------------------------
    if (p.wall) for (let k = -1; k <= 1; k++) {
      const w = place(dir * p.throwDist, k);
      draw(MeshPool.plane10, w.x, buildingLayer, w.z, 1, 1, -p.aim, Stone);
      draw(MeshPool.plane10, w.x, buildingLayer + .002, w.z + .06, .9, .78, -p.aim, StoneTop);
    }
    const flight = (s - t.release) / (t.land - t.release), u = clamp(flight);
    const eased = p.wall ? u : 1 - (1 - u) * (1 - u), gone = t.thrown * eased;
    if (flight > 0) {
      const settle = 1 - .4 * smooth((s - t.land) / Tail);
      sprite(place(dir * gone / 2), gone + .5, .42, Ink.withAlpha(.3 * settle), soft, Floor + .01, -p.aim);
      for (let i = 0; i < 7; i++) {
        const where = (i + .5) / 7 * t.thrown; if (where > gone) continue;
        const age = s - (t.release + (t.land - t.release) * (p.wall ? where / t.thrown : 1 - Math.sqrt(1 - where / t.thrown))), life = .5 + rand(i) * .3;
        if (age < 0 || age > life) continue;
        const v = age / life, at = place(dir * where, (rand(i + 5) - .5) * .4);
        sprite({ x: at.x, z: at.z + v * .35 }, .4 + v * .5, .32 + v * .4, Dust.withAlpha(.5 * Math.sin(v * Math.PI)), soft, Y + .005);
      }
    }

    // Where the thrown pawn is at a given distance along its path, as a time.
    const passes = where => t.release + (t.land - t.release) * (p.wall ? where / t.thrown : 1 - Math.sqrt(Math.max(0, 1 - where / t.thrown)));

    // --- pawns, north first ----------------------------------------------------------------------------------
    const grinding = s >= t.hit && s < t.release, pressed = grinding ? clamp((s - t.hit) / Press) : 0;
    const lunge = smooth((s - t.thrust) / Reach) * (1 - smooth((s - t.release) / .3));
    const thinOut = tele ? smooth((s - t.go) / p.squeeze) : 0, thinIn = tele ? 1 - smooth((s - t.arrive) / p.squeeze) : 0;
    const landed = s >= t.arrive, stand = landed ? spot : home, face = landed ? dir : 1;
    const caster = { x: stand.x + ca * face * Lean * (landed ? lunge : 0), z: stand.z + sa * face * Lean * (landed ? lunge : 0) };
    const shake = grinding ? Math.sin(s * 95) * .028 : 0, victim = place(dir * (gone + .08 * pressed) + shake, shake * .6);
    const enemyColour = Color.Lerp(EnemyColour, Ice, Math.max(pressed, clamp(1 - (s - t.release) / .15) * (flight > 0 ? 1 : 0)) * .75);
    if (s >= t.land) sprite(victim, 1.3, .8, Ink.withAlpha(.34 * (1 - .4 * smooth((s - t.land) / Tail))), soft, Floor + .012, -p.aim);
    let clipHand = null;
    const figures = [{ pos: caster, caster: true }, { pos: victim }];
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(f => {
      if (f.caster) {
        // The caster plays the two Rasengan clips (make_rasengan_anim.py): Form where the ball is made,
        // Thrust where it lands. A clip cannot be narrowed, so the sliver of the teleport is the stand-in.
        const thin = landed ? thinIn : thinOut, thrusting = landed && s >= t.thrust - (tele ? .04 : 0);
        const clip = p.look === 'animation clips' && thin < .02
          ? (thrusting ? playClip('RimArt_RasenganThrust', s - t.thrust, stand, { aim: (p.aim + (tele ? 180 : 0)) % 360, scene })
            : playClip('RimArt_RasenganForm', s - t.cast, stand, { aim: p.aim, scene }))
          : null;
        if (clip) clipHand = clip.hand; else figure(f.pos, CasterColour, 1, thin, sun, strength);
      }
      else if (s >= t.land) downed(f.pos, EnemyColour, sun, strength);
      else if (flight > 0) tumbling(f.pos, enemyColour, dir * Tumble * u, Hop * Math.sin(u * Math.PI) * (p.wall ? .5 : 1), sun, strength);
      else { figure(f.pos, enemyColour, 1, 0, sun, strength); if (tele) stuckKunai(f.pos, p.aim, smooth((s - t.cast) / (p.form * .4))); }
    });

    // --- the teleport ---------------------------------------------------------------------------------------------
    if (tele) {
      leave('rasengan leave', home, s - t.go, p.squeeze, 1.2);
      jumpLine('rasengan line', home, spot, (s - t.go) / LineTime, .12);
      star('rasengan arrive star', { x: spot.x, z: spot.z + Chest }, s - t.arrive, FlashTime, 1.2);
      sparks('rasengan arrive', spot, s - t.arrive, 10);
    }

    // --- the ball in the hand, and everything round it while it is held ---------------------------------------------
    const grown = smooth((s - t.cast) / p.form), out = Hand + (landed ? lunge * (1 - Lean - Hand - .03) : 0);
    const hand = clipHand ?? { x: caster.x + ca * face * out, z: caster.z + sa * face * out + Chest };
    if (s >= t.cast && s < t.release) {
      const seen = 1 - (landed ? thinIn : thinOut), unsteady = Unsteady * (1 - grown);
      const size = p.ballSize * grown * (1 + Swell * smooth(pressed)) * (1 + unsteady * Math.sin(s * 53));
      const held = { x: hand.x + unsteady * .25 * Math.sin(s * 71), z: hand.z + unsteady * .25 * Math.cos(s * 59) };
      const forming = clamp((s - t.cast) / .12) * (1 - clamp((s - t.formed) / .15));
      // Floor: blue light under the ball, wind rings opening from the caster's feet, dust carried out with them.
      sprite({ x: held.x, z: held.z - Chest }, 2.2 * (.6 + .4 * grown) * (1 + pressed), 1.5 * (.6 + .4 * grown) * (1 + pressed),
        Blue.withAlpha(.26 * grown * seen * (.85 + .15 * Math.sin(s * 31))), glow, Floor + .006);
      for (let n = 0; n < WindRings; n++) {
        const v = ((s - t.cast) / WindEvery + n / WindRings) % 1;
        circle(caster, .3 + v * p.swirl, .55 * (1 - v) * grown * seen, Floor + .02, Ice);
      }
      for (let i = 0; i < Puffs; i++) {
        const v = ((s - t.cast) * 1.1 + rand(i + 30)) % 1, rad = .35 + v * p.swirl, ang = (i * 45 + rand(i + 60) * 30 + v * 40) * Mathf.Deg2Rad;
        sprite({ x: caster.x + Math.cos(ang) * rad, z: caster.z + Math.sin(ang) * rad * .85 + v * .1 }, .35 + v * .4, .28 + v * .3, Dust.withAlpha(.42 * grown * seen * Math.sin(v * Math.PI)), soft, Floor + .04);
      }
      // Threads of chakra curling into the palm while it forms.
      if (forming > 0) for (let i = 0; i < Threads; i++) {
        const v = ((s - t.cast) * 1.7 + rand(i)) % 1, left = [], right = [], steps = 7;
        for (let j = 0; j <= steps; j++) {
          const w = Math.max(0, v - (steps - j) * .03), rad = p.swirl * Math.pow(1 - w, 1.3) + size / 2, ang = (i * 36 + rand(i + 9) * 40 + w * 260) * Mathf.Deg2Rad;
          const c = { x: held.x + Math.cos(ang) * rad, z: held.z + Math.sin(ang) * rad }, half = .028 * (j / steps) + .002;
          left.push({ x: c.x + Math.cos(ang) * half, z: c.z + Math.sin(ang) * half }); right.push({ x: c.x - Math.cos(ang) * half, z: c.z - Math.sin(ang) * half });
        }
        strip(`rasengan thread ${i}`, left, right, Ice.withAlpha(.85 * forming * Math.sin(v * Math.PI)), whiteGlow, Y + .09);
      }
      ball(held, size, s, seen, pressed);
      // The grind: a spiral turning on the ball and sparks thrown off round it.
      if (grinding) {
        spiral('rasengan grind', held, size * .62, .8, 2, -s * 1400, .035 * size / p.ballSize, White.withAlpha(.85), undefined, Y + .122);
        for (let i = 0; i < GrindSparks; i++) {
          const v = ((s - t.hit) * 5 + rand(i + 12)) % 1, ang = i * 30 + s * 700 + v * 50, r0 = size / 2 + v * .8, r1 = r0 + .2;
          const a0 = ang * Mathf.Deg2Rad, a1 = (ang + 14) * Mathf.Deg2Rad;
          streak(`rasengan grind spark ${i}`, { x: held.x + Math.cos(a0) * r0, z: held.z + Math.sin(a0) * r0 }, { x: held.x + Math.cos(a1) * r1, z: held.z + Math.sin(a1) * r1 },
            .05, Ice.withAlpha(1 - v), whiteGlow, Y + .123, 2);
        }
      }
    }

    // --- release: the sphere bursts, lines shoot out, a spiral shock opens ------------------------------------------
    const since = s - t.release, burst = { x: o.x, z: o.z + Chest };
    if (since >= 0 && since < BurstTime) {
      const v = since / BurstTime, f = 1 - v, rad = p.ballSize * (1 + Swell) / 2 + (BurstRadius - p.ballSize) * smooth(v);
      sprite(burst, 3.4, 3.4, Blue.withAlpha(.8 * f * f), glow, Y + .1);
      draw(disc, burst.x, Y + .101, burst.z, rad, rad, 0, Sky.withAlpha(.5 * f));
      draw(ring, burst.x, Y + .102, burst.z, rad, rad, 0, White.withAlpha(f));
      sprite(burst, 1.5, 1.5, White.withAlpha(f), glow, Y + .11);
      for (let i = 0; i < BurstLines; i++) {
        const ang = (i * 360 / BurstLines + rand(i + 70) * 14) * Mathf.Deg2Rad, r0 = .4 + v * 1.1, r1 = r0 + .7 + rand(i + 90) * .6;
        streak(`rasengan burst line ${i}`, { x: burst.x + Math.cos(ang) * r0, z: burst.z + Math.sin(ang) * r0 }, { x: burst.x + Math.cos(ang) * r1, z: burst.z + Math.sin(ang) * r1 },
          .07, White.withAlpha(f), whiteGlow, Y + .105, 4);
      }
    }
    if (since >= 0 && since < ShockTime) {
      const v = since / ShockTime;
      spiral('rasengan shock glow', burst, ShockRadius * smooth(v), ShockTurns, 2, since * 500, .13 * (1 - v), Blue.withAlpha(.7 * (1 - v)), whiteGlow, Y + .08);
      spiral('rasengan shock', burst, ShockRadius * smooth(v), ShockTurns, 2, since * 500, .06 * (1 - v), Ice.withAlpha(1 - v), undefined, Y + .085);
    }

    // --- the vortex behind the thrown pawn: a double helix and rings that open across the path -----------------------
    if (flight > 0 && s < t.land + .25) {
      const fade = 1 - clamp((s - t.land) / .25), tail = Math.max(0, gone - TrailLength), steps = 18;
      [1, -1].forEach(side => {
        const left = [], right = [];
        for (let i = 0; i <= steps; i++) {
          const w = i / steps, d = tail + (gone - tail) * w, wave = side * Math.sin(d * 7 - since * 30) * TrailWave * (.3 + .7 * w), half = .04 * Math.sin(w * Math.PI) + .003;
          const c = place(dir * d, wave);
          left.push({ x: c.x - sa * half, z: c.z + ca * half + Chest }); right.push({ x: c.x + sa * half, z: c.z - ca * half + Chest });
        }
        strip(`rasengan helix glow ${side}`, left, right, Blue.withAlpha(.8 * fade), whiteGlow, Y + .07);
        strip(`rasengan helix ${side}`, left, right, Ice.withAlpha(.9 * fade), undefined, Y + .072);
      });
    }
    for (let n = 0; n < VortexRings; n++) {
      const where = (n + .5) / VortexRings * t.thrown, age = s - passes(where);
      if (flight <= 0 || age < 0 || age >= VortexTime) continue;
      const v = age / VortexTime, c = place(dir * where);
      draw(ring, c.x, Y + .06, c.z + Chest, .07 + .07 * v, .28 + .4 * v, -p.aim, Ice.withAlpha(.85 * (1 - v)));
      draw(ring, c.x, Y + .059, c.z + Chest, .11 + .09 * v, .34 + .44 * v, -p.aim, Blue.withAlpha(.6 * (1 - v)), whiteGlow);
    }

    // --- where it stops: dust, and a second flash if a wall stopped it -------------------------------------------------
    const stopped = s - t.land;
    if (stopped >= 0 && stopped < .6) {
      const v = stopped / .6, at = place(dir * (t.thrown + (p.wall ? .45 : 0)));
      if (p.wall && stopped < .12) sprite({ x: at.x, z: at.z + Chest }, 1.5, 1.5, White.withAlpha(.9 * (1 - stopped / .12)), glow, Y + .1);
      for (let i = 0; i < 10; i++) {
        const ang = i * .63 + rand(i), d = .25 + v * (.6 + rand(i + 4) * .7);
        sprite({ x: at.x + Math.cos(ang) * d, z: at.z + Math.sin(ang) * d * .7 + v * .3 }, .45 + v * .6, .36 + v * .45, Dust.withAlpha(.55 * Math.sin(v * Math.PI)), soft, Y + .005);
      }
    }
  },
};
