// Vector shove, reworked. Ported to C# 2026-09-29 (Source/RimArt/Accelerator/VectorShove*.cs, previews
// "Accelerator: vector shove: ..."), played in game by MapComponent_Shoves (Accelerator/Kit/ShoveThrow.cs):
// the thrown pawn flies in a PawnFlyer, the throw starts Touch (0.12 s) after the hand lands, and the
// raider's squash on the wall is left out (a real pawn cannot be squashed). Numbers are AG_VectorShove's XML.
//
// What it is for (agreed 2026-09-24; every number is a placeholder for XML).
//   Touch range, no warmup: Accelerator lays a hand on one adjacent body and reverses its momentum.
//   The player picks the target and then a direction (a second target step). A pawn is thrown 8
//   cells along that line, divided by body size; every pawn standing in the path is knocked 1 cell
//   sideways, takes the slam damage (8) and is stunned 30 ticks, and the thrown pawn keeps going. A wall ends the throw
//   early with the slam. Damage 1.5 blunt per cell travelled, stun 60 ticks. A loose thing (stone
//   chunk, corpse, dropped weapon) is thrown 12 cells as a projectile and hits for blunt damage by
//   its mass, about 20 for a stone chunk, which lands as a chunk again; Uplift leaves chunks, this
//   throws them. Force returned: if the target hit Accelerator in melee within the last second,
//   that hit's damage is added as blunt on the first thing the thrown body strikes. It does not
//   reduce the hit Accelerator took. Strain +0.05, cooldown 20 s.
//
// Order (raider into a wall, the default):
//   0.00  Accelerator stands, a raider adjacent with a mace, a wall 5 cells along the throw
//   0.30  the raider's mace lands on Accelerator: a white flash with a black edge at the chest;
//         a thin white ring closes round the raider over 1 s, the force-returned window
//   0.70  touch: the arm comes out, the hand on the raider, a flash at the contact point; the
//         chosen line is drawn on the floor from the raider to where the throw ends
//   0.82  throw: the raider flies at 20 cells/s with black speed lines behind, dust at the start
//   1.04  slam on the wall (physical, not his light; reworked 2026-09-28): the raider squashes
//         flat on the wall face for 0.1 s and slides down to lie at its foot; the wall cells shake
//         0.05 cells for 0.15 s; a dark dent stays on the face; grey dust bursts out along the face
//         about 1.2 cells each way; 6 stone chips fly back in low arcs, land 0.6-1.5 cells from the
//         wall and stay. Because the window was still open (the mace hit returned): 5 dark cracks
//         on the wall face, 0.4-0.7 cells, that stay; 12 chips; more dust; a harder camera shake.
//   1.04  to 3.04 the raider lies at the wall, stunned
//   "raider through his line": no wall; two of his friends stand in the path at 3 and 5.5 cells.
//         The thrown raider knocks each 1 cell sideways as he passes (4 short black impact lines
//         and a puff of dust on each; 8 longer lines on the first when force is returned) and
//         lands 8 cells out.
//   "chunk at a shooter": a stone chunk beside Accelerator, a shooter 10 cells out. The chunk flies
//         in a low arc and hits the shooter (4 impact lines, dust, 4 chips that stay; he goes down)
//         and lies beside him as a chunk.
//   Slide "Touch after the hit" past 1 s and the window has closed: no cracks, 6 chips.
//
// Palette: monochrome, see lib/accelerator.js. White light with a black edge only on his part (the
// mace-hit flash, the window ring, the touch, the line, the speed lines). The impacts are plain
// physics: dust, stone chips, a dent, cracks, black impact lines; no glow, no rings.
// Drawing: flat shapes and level circles, so it turns with the aim. Pawns, mace, wall and chunk are
// stand-ins. The chunk's arc and the chips' arcs are height drawn as a shift north with a ground
// shadow. The squash is the stand-in pawn's two ellipses, narrowed along the throw.
import { Color, Mathf, Meshes, MeshPool } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import { pawn, rock, ringAt, line, streak, whiteGlow, wallCell, stunStars, EnemyColour, Ink, White, Dust, Lift, Chest, Skin, pawnLayer, shadowLayer, buildingLayer, clamp, smooth } from './lib/goku.js';
import { accelerator, arm, Edge } from './lib/accelerator.js';

const TAU = Math.PI * 2, D2R = Mathf.Deg2Rad, ChestUp = Chest / Lift;
const disc = Meshes.disc(32, 'shove disc');
// Decided values. The panel keeps only what is still being tuned.
const Lead = .3, Touch = .12, Window = 1, Tail = 2, KnockTime = .2, Speed = 20, Knock = 1, Cracks = 5;
const Chips = 6, BonusChips = 12, Squash = .1, Drop = .15, WallShake = .05;
const Mace = new Color(.22, .2, .19), Steel = new Color(.5, .5, .52), WallDust = new Color(.74, .72, .69);
// Pawns in the path for the line scenario: [cells from the raider's start, across, knocked to side].
const Liners = [[3, .2, 1], [5.5, -.25, -1]], ShooterAt = 10;

const wall = p => p.scenario === 'raider into a wall', lineUp = p => p.scenario === 'raider through his line', chunk = p => p.scenario === 'chunk at a shooter';
const stopAt = p => chunk(p) ? Math.min(p.chunkCells, ShooterAt - .6) : wall(p) ? Math.min(p.cells, p.wallAt - .65) : p.cells;
// The throw reaches the wall: the body stops with its back 0.15 cells short of the face.
const hitsWall = p => wall(p) && p.cells >= p.wallAt - .65;

// The stand-in pawn squashed against a wall: both ellipses narrowed along the throw by k (0..1).
function squashed(pos, colour, k, deg, sun, strength) {
  const c = Math.abs(Math.cos(deg * D2R)), n = Math.abs(Math.sin(deg * D2R));
  const fx = (1 - .45 * k * c) * (1 + .15 * k * n), fz = (1 - .45 * k * n) * (1 + .15 * k * c);
  sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, .85, .4, Ink.withAlpha(strength), soft, shadowLayer);
  draw(disc, pos.x, pawnLayer, pos.z + .18, .22 * fx, .32 * fz, 0, colour);
  draw(disc, pos.x, pawnLayer + .002, pos.z + .18 + .4 * fz, .16 * fx, .17 * fz, 0, Skin);
}
function times(p) {
  const hit = Lead, touch = hit + (chunk(p) ? .2 : p.react), fly = touch + Touch, arrive = fly + stopAt(p) / Speed;
  return { hit, touch, fly, arrive, end: arrive + Tail };
}

export default {
  kit: 'Accelerator', label: 'Vector shove (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'raider into a wall', options: ['raider into a wall', 'raider through his line', 'chunk at a shooter'], group: 'Showcase' },
    aim: P('Throw direction (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    react: P('Touch after the hit (s); over 1 s the window has closed', .4, .1, 1.5, .05, 'Showcase'),
    cells: P('Pawn thrown (cells)', 8, 3, 12, 1, 'Shape'),
    chunkCells: P('Thing thrown (cells)', 12, 5, 16, 1, 'Shape'),
    wallAt: P('Wall (cells from the raider)', 5, 2, 8, 1, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return chunk(p)
    ? [{ name: 'Stand', t: 0 }, { name: 'Touch', t: t.touch }, { name: 'Throw', t: t.fly }, { name: 'Hit', t: t.arrive }]
    : [{ name: 'Stand', t: 0 }, { name: 'Mace hits', t: t.hit }, { name: 'Touch', t: t.touch }, { name: 'Throw', t: t.fly }, { name: wall(p) ? 'Slam' : 'Lands', t: t.arrive }]; },
  events(p) { const t = times(p), bonus = !chunk(p) && p.react <= Window; return [{ t: t.hit, type: 'shake', value: chunk(p) ? 0 : .03 }, { t: t.fly, type: 'shake', value: .03 }, { t: t.arrive, type: 'shake', value: hitsWall(p) ? (bonus ? .14 : .1) : .06 }]; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a);
    // The chosen cell is the middle of the throw. A is Accelerator; along runs down the throw from the target's start cell.
    const Half = chunk(p) ? 6 : 4, A = { x: o.x - ca * Half, z: o.z - sa * Half };
    const place = (along, across = 0, up = 0) => ({ x: A.x + (1 + along) * ca - across * sa, z: A.z + (1 + along) * sa + across * ca + up * Lift });
    const stop = stopAt(p), bonus = !chunk(p) && p.react <= Window, arrived = s >= t.arrive;
    const flown = s >= t.fly ? Math.min(stop, (s - t.fly) * Speed) : 0, since = s - t.arrive;

    // --- floor: the chosen line, the scuff on the wall, the landing dust -------------------------------------------
    if (s >= t.touch - .2 && s < t.fly + .25) {
      const show = smooth((s - t.touch + .2) / .12) * (1 - smooth((s - t.fly - .05) / .2)), tip = place(stop), tipBack = place(stop - .5);
      line('shove line edge', [place(.3), tip], .09, Edge.withAlpha(.6 * show), undefined, Floor + .02, 'none');
      line('shove line', [place(.3), tip], .05, White.withAlpha(.9 * show), undefined, Floor + .021, 'none');
      [1, -1].forEach(side => streak(`shove arrow ${side}`, place(stop - .45, side * .35), tip, .06, White.withAlpha(.9 * show), undefined, Floor + .021, 2));
    }
    // The wall: it shakes on the slam, a dent stays on its face, and cracks when the force is returned.
    const slamAge = hitsWall(p) && arrived ? since : -1, face = p.wallAt - .5;
    if (wall(p)) {
      const shake = slamAge >= 0 && slamAge < .15 ? WallShake * Math.sin(slamAge * 95) * (1 - slamAge / .15) : 0;
      for (let k = -1; k <= 1; k++) wallCell(place(p.wallAt + shake, k), p.aim);
      if (slamAge >= 0) {
        const grow = smooth(slamAge / .06), D = place(face + .14, 0, ChestUp);   // where the chest struck, drawn at its height
        sprite(D, .34, .55, Ink.withAlpha(.5 * grow), soft, buildingLayer + .004, -p.aim);
        sprite(D, .16, .28, Ink.withAlpha(.6 * grow), soft, buildingLayer + .0045, -p.aim);
        if (bonus) for (let i = 0; i < Cracks; i++) {
          const th = (-75 + i * 37.5 + (rand(i + 3) - .5) * 20) * D2R, len = (.4 + .3 * rand(i + 8)) * smooth(slamAge / .08), pts = [];
          for (let k = 0; k <= 7; k++) { const d = len * k / 7, off = k ? (rand(i * 11 + k) - .5) * .14 : 0; pts.push(place(face + .02 + Math.cos(th) * d - Math.sin(th) * off, Math.sin(th) * d + Math.cos(th) * off, ChestUp)); }
          line(`shove crack ${i}`, pts, .04, Edge.withAlpha(.75), undefined, buildingLayer + .005);
        }
      }
    }
    if (arrived && !wall(p) && !chunk(p)) sprite(place(stop), 1.2, .8, Ink.withAlpha(.35 * smooth(since / .3)), soft, Floor + .01);

    // --- the pawns, north first ------------------------------------------------------------------------------------
    const figures = [];
    // Accelerator: hit at t.hit, arm out for the touch.
    const struck = !chunk(p) && s >= t.hit ? 1 - clamp((s - t.hit) / .3) : 0;
    const armOut = s >= t.touch - .1 && s < t.fly + .3 ? .5 * smooth((s - t.touch + .1) / .1) * (1 - smooth((s - t.fly - .1) / .2)) : 0;
    figures.push({ pos: A, kind: 'accelerator' });
    if (chunk(p)) {
      figures.push({ pos: place(ShooterAt), kind: 'shooter', down: arrived && since >= .15, lit: arrived && since < .12 });
    } else {
      // The raider: adjacent until the throw, then flying, then lying.
      const flying = s >= t.fly && !arrived, lying = arrived;
      figures.push({ pos: place(flown), kind: 'raider', flying, lying, swing: s < t.hit + .15 });
      if (lineUp(p)) Liners.forEach(([d, across, side], i) => {
        const passed = s >= t.fly && flown >= d, at = passed ? t.fly + d / Speed : 0, k = passed ? smooth((s - at) / KnockTime) : 0;
        figures.push({ pos: place(d, across + side * Knock * k), kind: 'liner', hitAt: at, passed, i, first: i === 0 });
      });
    }
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      if (g.kind === 'accelerator') {
        arm(g.pos, p.aim, armOut);
        accelerator(g.pos, sun, strength, { tint: White, tintAmount: .9 * struck, outline: struck });
        return;
      }
      if (g.kind === 'shooter') { pawn(g.pos, EnemyColour, sun, strength, { lie: g.down, tint: White, tintAmount: g.lit ? .5 : 0 }); return; }
      if (g.kind === 'liner') {
        const flash = g.passed ? 1 - clamp((s - g.hitAt) / .12) : 0;
        pawn(g.pos, EnemyColour, sun, strength, { tint: White, tintAmount: .5 * flash });                 // knocked aside, stunned 30 ticks, still on his feet
        if (g.passed && s - g.hitAt > .2) stunStars(`shove liner ${g.i}`, g.pos, s, .8 * (1 - smooth((s - g.hitAt - .3) / .5)), White);
        return;
      }
      // The raider. At a wall: squashed flat on the face, then slid down to lie at its foot.
      if (g.lying && hitsWall(p)) {
        if (since < Squash) squashed(place(stop + .06 * smooth(since / .05)), EnemyColour, smooth(since / .05), p.aim, sun, strength);
        else { const u = smooth((since - Squash) / Drop); pawn(place(stop - .25 * u, 0, .3 * (1 - u)), EnemyColour, sun, strength, { lie: true }); }
        if (since > Squash + Drop) stunStars('shove raider', place(stop - .25), s, .9 * (1 - smooth((since - .3) / Tail)), White);
        return;
      }
      if (g.lying) {
        pawn(g.pos, EnemyColour, sun, strength, { lie: true });
        stunStars('shove raider', g.pos, s, .9 * (1 - smooth((since - .3) / Tail)), White);
        return;
      }
      pawn(g.pos, EnemyColour, sun, strength, { tint: White, tintAmount: g.flying ? .5 : 0 });
      // The mace: a short handle from the raider toward Accelerator, swinging down onto him before the hit.
      if (g.swing) {
        const u = clamp((s - (t.hit - .15)) / .15), ang = p.aim + 180 + (1 - u) * 70, r = ang * Mathf.Deg2Rad;
        draw(MeshPool.plane10, g.pos.x + Math.cos(r) * .3, pawnLayer + .012, g.pos.z + Math.sin(r) * .3 + Chest, .07, .5, 90 - ang, Mace);
        draw(MeshPool.plane10, g.pos.x + Math.cos(r) * .55, pawnLayer + .013, g.pos.z + Math.sin(r) * .55 + Chest, .2, .2, 90 - ang, Steel);
      }
    });

    // --- the mace hit and the force-returned window ------------------------------------------------------------------
    if (!chunk(p) && s >= t.hit && s < t.hit + .25) {
      const u = (s - t.hit) / .25;
      sprite({ x: A.x + ca * .3, z: A.z + sa * .3 + Chest }, .6 * (1 - u) + .2, .6 * (1 - u) + .2, White.withAlpha(.9 * (1 - u)), glow, Y + .2);
      ringAt({ x: A.x + ca * .3, z: A.z + sa * .3 + Chest }, .15 + u * .5, Edge.withAlpha(.8 * (1 - u)), Y + .19);
    }
    if (!chunk(p) && s >= t.hit && s < Math.min(t.fly, t.hit + Window)) {
      const w = (s - t.hit) / Window, R = place(0);
      ringAt({ x: R.x, z: R.z + .3 }, .75 - .35 * w, White.withAlpha(.85 * (1 - w * .5)), Y + .05);
      ringAt({ x: R.x, z: R.z + .3 }, .79 - .35 * w, Edge.withAlpha(.5 * (1 - w * .5)), Y + .049);
    }

    // --- the touch --------------------------------------------------------------------------------------------------
    if (s >= t.touch && s < t.touch + .3) {
      const u = (s - t.touch) / .3, T = { x: A.x + ca * .75, z: A.z + sa * .75 + Chest };
      sprite(T, .5 * (1 - u) + .3, .5 * (1 - u) + .3, White.withAlpha(.95 * (1 - u)), glow, Y + .2);
      ringAt(T, .2 + u * .8, Edge.withAlpha(.8 * (1 - u)), Y + .19);
      for (let i = 0; i < 5; i++) { const ang = a + Math.PI + (i - 2) * .35, d = .3 + u * .6; streak(`shove touch ${i}`, { x: T.x + Math.cos(ang) * .2, z: T.z + Math.sin(ang) * .2 }, { x: T.x + Math.cos(ang) * d, z: T.z + Math.sin(ang) * d }, .05, White.withAlpha(.9 * (1 - u)), whiteGlow, Y + .2, 3); }
    }

    // --- the flight: speed lines behind the body, dust at the start, the chunk in its arc ---------------------------------
    if (s >= t.fly && !arrived) {
      for (let i = 0; i < 5; i++) {
        const across = (i - 2) * .22 + (rand(i + 9) - .5) * .1, back = 1 + rand(i + 3) * 1.5, black = i % 2 === 0;
        streak(`shove speed ${i}`, place(Math.max(.2, flown - back), across, Chest / Lift), place(Math.max(.2, flown - .3), across, Chest / Lift), black ? .06 : .04, (black ? Ink : White).withAlpha(.7), black ? undefined : whiteGlow, Y + .09, 3);
      }
    }
    if (s >= t.fly && s < t.fly + .6) for (let i = 0; i < 8; i++) {
      const u = clamp((s - t.fly - rand(i) * .1) / .5), d = .2 + u * (.8 + rand(i + 4)), across = (rand(i + 8) - .5) * 1.2;
      sprite(place(-d * .3 + .1, across), .3 + u * .4, .25 + u * .3, Dust.withAlpha(.45 * Math.sin(u * Math.PI)), soft, Floor + .04);
    }
    if (chunk(p)) {
      const u = s < t.fly ? 0 : clamp(flown / stop), h = arrived ? 0 : 1.1 * Math.sin(u * Math.PI), at = arrived ? place(stop + .6, .4) : place(flown, 0, h), ground = arrived ? at : place(flown);
      if (!arrived) sprite({ x: ground.x + sun.x * h, z: ground.z + sun.z * h }, .7, .4, Ink.withAlpha(.35), soft, Floor + .05);
      rock(at, .8, s < t.fly ? 20 : arrived ? 140 : 20 + flown * 90, 1, 2, arrived ? Floor + .06 : Y + .01);
    }

    // --- the strikes: plain physics, not his light ------------------------------------------------------------------
    // Short black impact lines round a contact point, gone in 0.22 s.
    const impactLines = (key, at, age, count, big) => {
      if (age < 0 || age > .22) return;
      const u = age / .22;
      for (let i = 0; i < count; i++) {
        const ang = i * TAU / count + (rand(i + 5) - .5) * .5, inner = .2 + u * .25, outer = inner + (.25 + .2 * rand(i + 15)) * (big ? 1.5 : 1) * (1 - u * .5);
        streak(`${key} ${i}`, { x: at.x + Math.cos(ang) * inner, z: at.z + Math.sin(ang) * inner }, { x: at.x + Math.cos(ang) * outer, z: at.z + Math.sin(ang) * outer }, big ? .075 : .055, Edge.withAlpha(.9 * (1 - u)), undefined, Y + .2, 3);
      }
    };
    // Dust thrown up round a point on the floor.
    const puff = (at, age, count, reach) => {
      if (age < 0 || age > .6) return;
      for (let i = 0; i < count; i++) {
        const u = clamp((age - rand(i + 7) * .05) / .55), ang = i * TAU / count + rand(i + 3), d = .1 + u * reach;
        sprite({ x: at.x + Math.cos(ang) * d, z: at.z + Math.sin(ang) * d * .7 + u * .1 }, .25 + u * .35, .2 + u * .28, Dust.withAlpha(.45 * Math.sin(u * Math.PI)), soft, Floor + .04);
      }
    };
    // Stone chips from chest height at (along0, across0) to where they land; they stay.
    const chips = (key, count, age, along0, across0, land) => {
      if (age < 0) return;
      for (let i = 0; i < count; i++) {
        const T = .3 + .15 * rand(i + 60), u = clamp(age / T), [la, lx] = land(i);
        const along = along0 + (la - along0) * u, across = across0 * (1 - u) + lx * u, h = ChestUp * (1 - u) + .35 * Math.sin(u * Math.PI), ground = place(along, across);
        if (u < 1) sprite({ x: ground.x + sun.x * h, z: ground.z + sun.z * h }, .14, .09, Ink.withAlpha(.35), soft, Floor + .05);
        rock(place(along, across, h), .12 + .07 * rand(i + 100), rand(i + 110) * 360 + age * 700 * (1 - u), 1, 2 * (i % 3), u < 1 ? Y + .05 : Floor + .06);
      }
    };

    // The wall slam: grey dust out along the face both ways, chips back toward Accelerator.
    if (slamAge >= 0) {
      const dusts = bonus ? 16 : 10, spread = bonus ? 1.5 : 1.2, big = bonus ? 1.3 : 1;
      for (let i = 0; i < dusts; i++) {
        const side = i % 2 ? 1 : -1, u = clamp((slamAge - rand(i + 30) * .06) / .6);
        if (u <= 0 || u >= 1) continue;
        const across = side * (.15 + spread * rand(i + 40) * smooth(u)), along = face - .25 - .35 * rand(i + 50) - .15 * u, size = (.3 + .55 * u) * big;
        sprite(place(along, across, .15 * u), size, size * .8, WallDust.withAlpha(.55 * Math.sin(u * Math.PI)), soft, Y + .01);
      }
      chips('shove wall chip', bonus ? BonusChips : Chips, slamAge, face - .1, 0, i => [face - .6 - .9 * rand(i + 80), (rand(i + 90) - .5) * 1.8]);
    }
    // Bodies bowled aside: impact lines and dust on each, more lines on the first when force is returned.
    if (lineUp(p)) Liners.forEach(([d, across], i) => {
      if (s < t.fly || flown < d) return;
      const age = s - (t.fly + d / Speed), big = i === 0 && bonus;
      impactLines(`shove liner ${i}`, place(d, across, ChestUp), age, big ? 8 : 4, big);
      puff(place(d, across), age, big ? 7 : 5, big ? .6 : .4);
    });
    // The chunk on the shooter: impact lines, dust, chips thrown on past him.
    if (chunk(p) && arrived) {
      impactLines('shove chunk', place(ShooterAt, 0, ChestUp), since, 4, false);
      puff(place(ShooterAt), since, 6, .5);
      chips('shove chunk chip', 4, since, ShooterAt, 0, i => [ShooterAt + .4 + .7 * rand(i + 120), (rand(i + 130) - .5) * 1.4]);
    }
  },
};
