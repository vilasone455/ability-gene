// Amenoyodomi — technique proposal for the Rinnegan eye kit, not the game. Nothing in Source/RimArt
// draws this yet.
//
// What it is for (agreed in outline 2026-09-24; every number is a placeholder). A toggle on the
// Rinnegan with three states: off, hang (1 %), drift (10 %). No target, no cooldown, no cost while
// the kit's cost system is undecided.
//   While it is on, throw kunai can target a cell (it cannot while it is off, so the Flying Thunder
//   God kit's throw is unchanged). The kunai flies there at full speed (24 cells/s) and stops 0.55
//   cells up over it. A throw at a pawn stays a normal throw. The Fūma cuts its whole line as usual
//   (range 12) and stops at the end instead of dropping.
//   Most held: 5. A 6th throw is a normal throw.
//   While held it moves on along its heading at the hold share of its speed: hang 0.24 cells/s (the
//   Fūma 0.14, turning 7.2 degrees/s), drift 2.4 cells/s (1.4, 72 degrees/s). Switching the state
//   changes every held weapon at once. It touches nothing: no damage, and pawns walk under it.
//   Let go (a second button, all at once): each weapon flies on at full speed along its heading, up
//   to its range (kunai 14.9, Fūma 12) from where it hung. A kunai hits the first pawn on its line
//   (12 stab, 18 % armour penetration, can stick, allies too). The Fūma cuts every pawn on its line
//   and its damage drop-off carries on from the throw.
//   Drop: it falls to the floor, not launched, after 60 s held, at a wall or the map edge, when the
//   toggle goes off, or if the caster is downed, asleep, dead or leaves the map.
//   Used by Amenotejikara (swap with a held weapon; it keeps its heading), Raikō Kusari (a net through
//   2 or more) and Amaterasu (lights every held weapon).
// Not a defence and not a redirect: Phase Guard holds enemy rounds round its wearer, Vector Edit
// re-aims enemy rounds, UBW Full Open aims gathered swords at one target. This holds only the
// caster's own kunai and Fūma, wherever they were thrown, and never re-aims them.
//
// Look. Not a canon technique, so the picture is the kit's Rinnegan lavender and the word: yodomi is
// still, stagnant water. The weapon eases to a stop and stays in the motion it was stopped in: three
// lavender afterimages behind it along its heading. A ripple spreads on the floor under it, as from
// a drop on still water, and two thin rings with three dots stay there, the dots turning at the hold
// share. A faint ripple leaves it every 2 s; drifting, it leaves a wake of ripples. The eye star
// (Amenotejikara's) flashes on the caster at every Amenoyodomi command: on, drift, let go, off.
// Letting go pulls the rings in with a small flash; no negative flash (Amenotejikara's sign) and no
// camera shake. A drop fades the rings, the weapon falls in 0.25 s and lies in a little dust.
//
// Showcase, default timings. Throws are 0.5 s apart here; the belt's cooldown is 1.5 s in the game.
//   hang and let go  0.00 Amenoyodomi on. 0.30, 0.80, 1.30 three kunai thrown at cells in a fan, each
//                    stopping about 0.2 s later. 1.5 s after the last stops they are let go: two hit
//                    the raiders standing on their lines and stick; the middle one passes 0.75 cells
//                    wide of the third raider and flies out of view. With 6 kunai thrown, kunai 4 and
//                    5 hang too and the 6th, a normal throw, lands on its cell.
//   pawn vs cell     0.30 a throw at a raider hits him as a normal throw; 0.80 a throw at a cell
//                    hangs; 1.5 s later Amenoyodomi is turned off and the kunai drops.
//   drift            three kunai hang; 0.8 s after the last stops the toggle goes to drift. They move
//                    on at 2.4 cells/s leaving ripples; a raider walks under the north one, untouched;
//                    the middle one reaches a wall and drops at its face.
//   Fūma             0.30 the Fūma is thrown at a cell 5 cells out, cuts the raider standing on the
//                    way and hangs over the cell, turning 7.2 degrees/s. 1.5 s later it is let go: full
//                    spin, it cuts the two raiders further on and flies out of view, to land 12 cells
//                    past where it hung.
//   caster downed    three kunai hang; a raider shoots the caster down; every held kunai drops at once.
// "Show release lines and the 60 s timer" draws the rule: a dotted line from each held weapon to where
// a let-go would take it (the first pawn on a kunai's line, or the end of its range), and an arc round
// it that empties over 60 s.
//
// Drawing: lib/amenoyodomi.js, shared with the other Rinnegan sketches. Everything is a level circle
// on the floor or a flat sprite along the heading, so there is no per-facing method. Pawns are the
// two-disc stand-ins. The caster throws from its own cell with no clip: the game launches the
// projectile from the pawn's DrawPos. Raiders stand still except the walker in "drift", and flinch
// when hit.
import { AltitudeLayer, Color, Mathf, MeshPool } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, at, sprite, glow } from './lib/six-paths-impact.js';
import { figure, downed, stuckKunai, knocked, streak, whiteGlow, CasterColour, EnemyColour } from './lib/flying-thunder-god.js';
import { wallCell } from './lib/goku.js';
import {
  weapon, motion, drawWeapon, eyeStar, ground, heldU, creptTime, Drift, MaxHeld, DropTime,
} from './lib/amenoyodomi.js';

const clamp = Mathf.Clamp01, pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const White = new Color(1, 1, 1), Tracer = new Color(1, .9, .6), Gun = new Color(.2, .2, .22);
const Scenarios = ['hang and let go', 'pawn vs cell', 'drift', 'Fūma', 'caster downed'];

// Showcase layout and the rule's reach. Fan: where the kunai are thrown, in cells along the aim and
// to its left, from the middle of the scene; the raiders stand Behind cells past the first and third.
const Fan = [[0, 1.5], [.3, 0], [0, -1.5], [.2, 2.7], [.2, -2.7], [.5, .8]];
const Behind = 3, OffLine = [4.1, -.75], WallU = 3, WalkU = 1.6, WalkIn = 2.2, WalkOn = 1.6;
const FirstThrow = .3, EyeLife = .1, HitReach = .4, CutReach = .55, WalkSpeed = 1.5, TracerSpeed = 40;
const HitLife = .12, CutLife = .15;

// The scene along the aim d from the middle o (n is d's left). Every weapon, pawn and time is worked
// out here from the params, so a frame depends only on the clip time.
function build(p, o) {
  const a = p.aim * Mathf.Deg2Rad, d = { x: Math.cos(a), z: Math.sin(a) }, n = { x: -d.z, z: d.x };
  const place = (u, v = 0) => ({ x: o.x + d.x * u + n.x * v, z: o.z + d.z * u + n.z * v });
  const caster = place(-p.distance);
  const S = { d, n, caster, weapons: [], pawns: [], walls: [], shot: null, down: Infinity, commands: [0], phases: [{ name: 'On', t: 0 }] };
  const pawn = (kind, pos, extra = {}) => {
    const Pn = { kind, start: pos, dir: d, speed: 0, t0: 0, reach: 0, hits: [], ...extra };
    S.pawns.push(Pn);
    return Pn;
  };
  const throwAt = (kind, cell, t, opts = {}) => {
    const w = weapon(kind, caster, cell, t, { seed: S.weapons.length, ...opts });
    S.weapons.push(w);
    return w;
  };
  const fan = count => {
    for (let i = 0; i < count; i++) throwAt('kunai', place(...Fan[i]), FirstThrow + i * p.gap, { held: i < MaxHeld });
    return S.weapons.filter(w => w.held);
  };
  // A pawn Behind cells past fan cell i, on that kunai's line.
  const behind = i => {
    const c = place(...Fan[i]), dx = c.x - caster.x, dz = c.z - caster.z, l = Math.hypot(dx, dz);
    return { x: c.x + dx / l * Behind, z: c.z + dz / l * Behind };
  };
  const lastCatch = held => Math.max(...held.map(w => w.caught));
  let end = 0;

  switch (p.scenario) {
    case 'pawn vs cell': {
      const raider = pawn('raider', place(2.6, 1.4));
      const w0 = throwAt('kunai', raider.start, FirstThrow, { held: false });
      w0.stop = { t: FirstThrow + (w0.dist - .15) / w0.speed, u: w0.dist - .15, how: 'hit', pawn: 0 };
      raider.hits.push({ t: w0.stop.t, deg: w0.deg, kind: 'kunai' });
      const w1 = throwAt('kunai', place(0, -1.2), FirstThrow + p.gap);
      w1.drop = w1.caught + p.hangFor;
      S.commands.push(w1.drop);
      S.phases.push({ name: 'At a pawn: normal', t: FirstThrow }, { name: 'At a cell: held', t: w1.caught }, { name: 'Off: drops', t: w1.drop });
      end = w1.drop + DropTime + 1;
      break;
    }
    case 'drift': {
      const W = [[0, 1.5], [.3, 0], [0, -1.5]].map((c, i) => throwAt('kunai', place(...c), FirstThrow + i * p.gap));
      const switchAt = lastCatch(W) + p.driftAt;
      W.forEach(w => w.shares.push([switchAt, Drift]));
      for (let v = -1; v <= 1; v++) S.walls.push(place(WallU, v));
      // The middle kunai flies straight down the aim and reaches the wall's face.
      const mid = W[1];
      mid.drop = creptTime(mid, p.distance + WallU - .5 - mid.dist);
      // A raider walks across the north kunai's line as it drifts over, and nothing happens to him.
      const top = W[0], along = top.dir.x * d.x + top.dir.z * d.z, uk = (WalkU + p.distance) / along;
      const across = uk * (top.dir.x * n.x + top.dir.z * n.z), pass = creptTime(top, uk - top.dist);
      pawn('walker', place(WalkU, across + WalkIn), { dir: { x: -n.x, z: -n.z }, speed: WalkSpeed, t0: pass - WalkIn / WalkSpeed, reach: WalkIn + WalkOn });
      S.commands.push(switchAt);
      S.phases.push({ name: 'Hanging', t: lastCatch(W) }, { name: 'Drift', t: switchAt }, { name: 'Walks under', t: pass }, { name: 'Wall: drops', t: mid.drop });
      end = Math.max(mid.drop + DropTime + 1, pass + 1.2);
      break;
    }
    case 'Fūma': {
      pawn('raider', place(-1.6, .15));
      const w = throwAt('fuma', place(1, 0), FirstThrow);
      pawn('raider', place(3.2, -.1));
      pawn('raider', place(5, .2));
      w.letGo = w.caught + p.hangFor;
      S.commands.push(w.letGo);
      S.phases.push({ name: 'Throw', t: FirstThrow }, { name: 'Hanging', t: w.caught }, { name: 'Let go', t: w.letGo });
      end = w.letGo + 1.2;
      break;
    }
    case 'caster downed': {
      const held = fan(3), last = lastCatch(held);
      const shooter = pawn('shooter', place(3.6, -2.4));
      S.shot = { from: shooter.start, t: last + 1 };
      S.down = S.shot.t + Math.hypot(caster.x - shooter.start.x, caster.z + .3 - shooter.start.z - .3) / TracerSpeed;
      held.forEach(w => { w.drop = S.down; });
      S.phases.push({ name: 'Hanging', t: last }, { name: 'Downed: all drop', t: S.down });
      end = S.down + DropTime + 1.2;
      break;
    }
    default: { // hang and let go
      const held = fan(Math.round(p.count)), last = lastCatch(held);
      pawn('raider', behind(0));
      pawn('raider', behind(2));
      pawn('raider', place(...OffLine));
      held.forEach(w => { w.letGo = last + p.hangFor; });
      S.weapons.filter(w => !w.held).forEach(w => { w.stop = { t: w.thrown + w.dist / w.speed, u: w.dist, how: 'land' }; });
      S.commands.push(last + p.hangFor);
      S.phases.push({ name: 'Throws', t: FirstThrow }, { name: 'Hanging', t: last }, { name: 'Let go', t: last + p.hangFor });
      end = last + p.hangFor + 1.1;
    }
  }

  // Cuts on the Fūma's way out: every standing pawn within CutReach of its line up to the cell.
  S.weapons.filter(w => w.kind === 'fuma').forEach(w => {
    S.pawns.forEach(Pn => {
      const { along, off } = lineTo(w.from, w.dir, Pn.start);
      if (!Pn.speed && along > 0 && along < w.dist && off < CutReach) Pn.hits.push({ t: w.thrown + along / w.speed, deg: w.deg, kind: 'fuma' });
    });
  });
  // After a let-go: a kunai stops at the first standing pawn on its line within range; the Fūma cuts
  // every standing pawn on its line and lands at the end of its range.
  S.weapons.filter(w => w.held && isFinite(w.letGo)).forEach(w => {
    const uL = heldU(w, w.letGo), q = ground(w, uL);
    let best = null;
    S.pawns.forEach((Pn, k) => {
      const { along, off } = lineTo(q, w.dir, Pn.start);
      if (Pn.speed || along <= 0 || along >= w.range) return;
      if (w.kind === 'fuma') { if (off < CutReach) Pn.hits.push({ t: w.letGo + along / w.speed, deg: w.deg, kind: 'fuma' }); return; }
      if (off < HitReach && (!best || along < best.along)) best = { k, along };
    });
    if (best) {
      w.stop = { t: w.letGo + best.along / w.speed, u: uL + best.along, how: 'hit', pawn: best.k };
      S.pawns[best.k].hits.push({ t: w.stop.t, deg: w.deg, kind: 'kunai' });
    } else w.stop = { t: w.letGo + w.range / w.speed, u: uL + w.range, how: 'range' };
  });
  S.pawns.forEach(Pn => Pn.hits.forEach(h => {
    if (h.t < end) S.phases.push({ name: h.kind === 'fuma' ? 'Cut' : 'Hit', t: h.t });
  }));
  S.end = end;
  S.phases = S.phases.filter(m => m.t < end).sort((x, y) => x.t - y.t)
    .filter((m, i, all) => i === 0 || m.name !== all[i - 1].name || m.t - all[i - 1].t > .05);
  return S;
}

// How far along a line from q in direction dir a point lies, and how far off it.
function lineTo(q, dir, pt) {
  const dx = pt.x - q.x, dz = pt.z - q.z;
  return { along: dx * dir.x + dz * dir.z, off: Math.abs(dx * dir.z - dz * dir.x) };
}
// Where a let-go would take a held weapon from where it is at time t: the first standing pawn on a
// kunai's line, or the end of its range.
function releaseTo(S, w, t) {
  const u = heldU(w, t), q = ground(w, u);
  if (w.kind === 'kunai') {
    let best = null;
    S.pawns.forEach(Pn => {
      const { along, off } = lineTo(q, w.dir, Pn.start);
      if (!Pn.speed && along > 0 && along < w.range && off < HitReach && (!best || along < best.along)) best = { along, at: Pn.start };
    });
    if (best) return best.at;
  }
  return ground(w, u + w.range);
}
function pawnAt(Pn, t) {
  const u = Math.min(Pn.reach, Pn.speed * Math.max(0, t - Pn.t0));
  return { x: Pn.start.x + Pn.dir.x * u, z: Pn.start.z + Pn.dir.z * u };
}

export default {
  kit: 'Rinnegan', label: 'Amenoyodomi (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    aim: P('Direction (degrees, 0 east)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Caster to the held weapons (cells)', 4, 2.5, 8, .5, 'Showcase'),
    count: P('Kunai thrown (hang and let go; the 6th is a normal throw)', 3, 1, 6, 1, 'Showcase'),
    overlay: { label: 'Show release lines and the 60 s timer (rule overlay)', value: false, group: 'Showcase' },
    gap: P('Throw every (showcase; 1.5 in the game)', .5, .2, 1.5, .05, 'Timing (s)'),
    hangFor: P('Let go, or turn off, after the last stops', 1.5, .5, 4, .1, 'Timing (s)'),
    driftAt: P('Switch to drift after the last stops', .8, .2, 3, .1, 'Timing (s)'),
    ghosts: P('Afterimages', 3, 0, 5, 1, 'Look'),
    ghostGap: P('Afterimage spacing while hanging (cells)', .16, .04, .4, .01, 'Look'),
    ringR: P('Ring radius under a kunai (cells)', .32, .2, .6, .01, 'Look'),
    rippleEvery: P('A ripple leaves every', 2, .5, 5, .1, 'Look'),
  },
  duration(p) { return build(p, { x: 0, z: 0 }).end; },
  phases(p) { return build(p, { x: 0, z: 0 }).phases; },
  events(p) {
    const S = build(p, { x: 0, z: 0 });
    return S.commands.map((t, i) => ({ t, type: 'sound', def: i ? 'AG_Amenoyodomi_Command' : 'AG_Amenoyodomi_On' }));
  },

  draw(s, p, { origin: o, scene: sc }) {
    const S = build(p, o);
    if (s < 0 || s >= S.end) return;
    const sun = sc?.shadowVector ?? { x: -.45, z: -.32 }, strength = sc?.sun?.strength ?? .32;

    S.walls.forEach(c => wallCell(c, p.aim));

    // The caster, and the eye star at every Amenoyodomi command.
    if (s >= S.down) downed(S.caster, CasterColour, sun, strength);
    else figure(S.caster, CasterColour, 1, 0, sun, strength);
    S.commands.forEach(t => {
      const age = s - t;
      if (age >= 0 && age < EyeLife && s < S.down) eyeStar({ x: S.caster.x + .04, z: S.caster.z + .62 }, .22, 1 - age / EyeLife);
    });

    // The raiders: they flinch along the weapon's heading when hit; kunai stick in them.
    S.pawns.forEach((Pn, k) => {
      let q = pawnAt(Pn, s);
      Pn.hits.forEach(h => {
        const push = knocked(s - h.t), r = h.deg * Mathf.Deg2Rad;
        q = { x: q.x + Math.cos(r) * push, z: q.z + Math.sin(r) * push };
      });
      figure(q, EnemyColour, 1, 0, sun, strength);
      Pn.hits.forEach((h, j) => {
        const age = s - h.t;
        if (age < 0) return;
        if (h.kind === 'kunai') stuckKunai(q, h.deg, 0);
        const chest = at(q, 0, .38);
        if (h.kind === 'kunai' && age < HitLife) sprite(chest, .45, .45, White.withAlpha(.85 * (1 - age / HitLife)), glow, Y + .05);
        if (h.kind === 'fuma' && age < CutLife) {
          // A cut across the body, square to the Fūma's path.
          const r = (h.deg + 90) * Mathf.Deg2Rad, dx = Math.cos(r) * .32, dz = Math.sin(r) * .32, f = 1 - age / CutLife;
          streak(`amenoyodomi cut ${k} ${j}`, { x: chest.x - dx, z: chest.z - dz }, { x: chest.x + dx, z: chest.z + dz }, .09, White.withAlpha(f), whiteGlow, Y + .05, 6);
          sprite(chest, .4, .4, White.withAlpha(.6 * f), glow, Y + .049);
        }
      });
      if (Pn.kind === 'shooter' && S.shot) {
        const dx = S.caster.x - q.x, dz = S.caster.z - q.z, l = Math.hypot(dx, dz), ux = dx / l, uz = dz / l;
        draw(MeshPool.plane10, q.x + ux * .18, pawnLayer + .003, q.z + .3 + uz * .18, .3, .05, -Math.atan2(uz, ux) * Mathf.Rad2Deg, Gun);
        const age = s - S.shot.t, muzzle = { x: q.x + ux * .32, z: q.z + .3 + uz * .32 }, end = { x: S.caster.x, z: S.caster.z + .3 };
        if (age >= 0 && age < .06) sprite(muzzle, .32, .32, Tracer.withAlpha(1 - age / .06), glow, Y + .04);
        const dl = Math.hypot(end.x - muzzle.x, end.z - muzzle.z), head = age * TracerSpeed;
        if (age >= 0 && head < dl + .6) {
          const pt = f => ({ x: muzzle.x + (end.x - muzzle.x) * clamp(f / dl), z: muzzle.z + (end.z - muzzle.z) * clamp(f / dl) });
          streak('amenoyodomi tracer', pt(head - .6), pt(head), .07, Tracer.withAlpha(.9), whiteGlow, Y + .04, 4);
        }
      }
    });
    if (s >= S.down && s < S.down + HitLife) sprite(at(S.caster, 0, .3), .5, .5, White.withAlpha(.85 * (1 - (s - S.down) / HitLife)), glow, Y + .05);

    // The weapons. A kunai stuck in a pawn is drawn with the pawn above.
    const look = { sun, strength, ghosts: Math.round(p.ghosts), ghostGap: p.ghostGap, ringR: p.ringR, rippleEvery: p.rippleEvery };
    S.weapons.forEach((w, i) => {
      const m = motion(w, s);
      look.releaseTo = p.overlay && m?.phase === 'held' ? releaseTo(S, w, s) : null;
      drawWeapon(`amenoyodomi weapon ${i}`, w, s, look);
    });
  },
};
