// Satō (Ajin) — Headshot Reset. Satō's hero kit, an active, only while manifested.
//
// Mechanic (agreed 2026-09-26, numbers are XML placeholders): Satō targets himself. Cast 0.6 s:
// he draws a sidearm, puts it to his right temple and fires. The sidearm is drawn by the effect
// only, so it works whatever he holds (rifle, melee weapon, nothing). Cost 0 + the Reset's 20
// charge; the button is greyed out below 20 charge, so it never drops him into the slow 1-day
// reset. He lies in the Reset state for 6 s (the passive's delay is 20 s) and always rises in
// place: a headshot leaves the body his biggest piece. It clears what the Reset clears (injuries,
// missing natural parts, disease, toxic buildup, burning, mental states); he keeps his gear and
// weapon. Anchors stay, so Sever then Headshot gives the limb back and keeps the severed part as
// an anchor. While he lies there raiders treat him as downed and pick other targets (playing
// dead); > 50 % damage during the delay still destroys the body, and he then rises at an anchor.
// Cooldown 60 s.
//
// Source: in the anime's testing-facility scene he is hit by tranquilizer darts, shoots himself
// in the head smiling, plays dead a few seconds and gets up to kill the guards.
//
// Beats: Wounded — he stands hurt while an enemy's shots hit him ("wounded": three wounds;
// "after Sever": his left arm is gone, the stump bleeds, and the arm lies 1.8 cells away inside an
// anchor ring). Draw (0.45 s) — the right arm comes up from the hip with a pistol to the temple
// and holds 0.15 s. Shot — contact flash and smoke at the temple, the head snaps away from the
// gun, a blood spray leaves the far side of the head and lands as stains, small camera shake.
// Fall (0.35 s) — he drops from standing to lying and the pistol falls by his right hand. Play
// dead (6 s, compressed) — blood pools under the head, black matter seeps from both head wounds
// and the body wounds, the timer ring fills, and the enemy's aim line swings to a colonist behind
// sandbags; its shots go there. Rebuild — black matter covers the head first, then the body, and
// the wounds close; after Sever the arm grows back out of the stump as black matter. Rise — he
// stands, the cover flakes off upward and the pistol comes back to his hand; he lowers it and it
// vanishes. The stains, the pool and the anchor arm stay; the enemy's aim swings back to him.
//
// Drawing: the pawn is the lab's two-disc stand-in at real size with drawn arms. Vanilla pawns
// have no arms: in game the gun arm and pistol are ability-drawn quads for the 0.6 s cast (see
// vfx-drawn arms), and the regrown arm shows only in the health tab. Per facing: south — gun hand
// on screen west, spray out east; north — gun hand east, pistol behind the head, spray west; east
// — profile, his right side faces the camera, so hand and pistol sit over the head and the spray
// leaves north (away from the camera, drawn behind the head) and lands north of him; west — the
// east drawing mirrored, as the game mirrors east sprites. The spray is ballistic from head
// height (screen z = floor z + height), so on east/west it rises on screen and lands behind him.
// Lying is the lab's downed layout (head west) for every facing; his right arm is then the south
// one.
import { Color, Meshes, Mathf } from '../js/engine.js';
import { P, glow } from './lib/six-paths-impact.js';
import {
  standInBlend, standIn, coverStandIn, seep, timerRing, edgeFlakes, limbSeg, rifle, bar, Floor, Y, pawnLayer,
  Shirt, Cap, Skin, Blood, Enemy, Slash, Ghost, GhostEdge, Outline, draw, disc, sprite, puff, rand, smooth, clamp, lerp,
} from './lib/ajin.js';

const Hold = .5;          // wounded, before the cast
const Aim = .15;          // pistol pressed to the temple before the shot
const Fall = .35;
const Tail = 1.1;
const ShotEvery = .5;     // the enemy's rate of fire in the sketch
const Pistol = new Color(.10, .10, .11), Smoke = new Color(.72, .71, .69), Flash = new Color(1, .85, .55);
const Tracer = new Color(1, .88, .55), AimCol = new Color(.95, .25, .20), Ally = new Color(.33, .43, .58), Sandbag = new Color(.58, .50, .36);
const Ground = -.30;      // screen z of the floor under a standing stand-in, from its cell centre
const HeadH = .72;        // the head centre's screen height above that floor
const G = 9;              // gravity for the spray, screen cells / s²
const EnemyAt = { x: 3.4, z: .9 }, AllyAt = { x: -3.2, z: 1.5 }, BagAt = { x: -2.5, z: 1.36 }, AnchorAt = { x: 1.5, z: -1.1 };
const ring = Meshes.band(.93, 1, 48, 'ajin headshot anchor ring');

function plan(p) {
  const tD = Hold, tS = tD + p.draw + Aim, tF = tS + .1, tL = tF + Fall;
  const t1 = tL + p.delay, t2 = t1 + p.rebuild, t3 = t2 + p.rise;
  return { tD, tS, tF, tL, t1, t2, t3, end: t3 + Tail };
}

// How far the stand-in is standing (1) or lying (0) at time t; standInBlend eases it.
function standK(t, L) {
  if (t < L.tF) return 1;
  if (t < L.tL) return 1 - (t - L.tF) / Fall;
  if (t < L.t2) return 0;
  return clamp((t - L.t2) / (L.t3 - L.t2));
}

// Arm joints (shoulder, elbow, hand) from the cell centre. Standing: the gun arm raised by r
// (0 hanging, 1 at the temple), x mirrored by m; lying: the downed layout, head west, gun arm
// flung out south-west.
function standArms(profile, r) {
  const e = smooth(clamp(r)), out = Math.sin(Math.PI * e);
  if (!profile) return {
    gun: [{ x: -.25, z: .12 }, { x: lerp(-.31, -.47, e), z: lerp(-.04, .27, e) }, { x: lerp(-.34, -.33, e) - .10 * out, z: lerp(-.20, .44, e) }],
    other: [{ x: .25, z: .12 }, { x: .31, z: -.04 }, { x: .34, z: -.20 }],
  };
  return {
    gun: [{ x: .02, z: .12 }, { x: lerp(.05, .23, e), z: lerp(-.04, .23, e) }, { x: .07 + .08 * out, z: lerp(-.21, .38, e) }],
    other: [{ x: -.04, z: .10 }, { x: -.03, z: -.05 }, { x: -.01, z: -.20 }],
  };
}
const LieArms = {
  gun: [{ x: -.20, z: -.34 }, { x: -.34, z: -.50 }, { x: -.52, z: -.56 }],
  other: [{ x: -.20, z: -.02 }, { x: -.02, z: .05 }, { x: .16, z: .04 }],
};
const blendArm = (lie, stand, m, e, pos) => lie.map((q, i) => ({ x: pos.x + lerp(q.x, stand[i].x * m, e), z: pos.z + lerp(q.z, stand[i].z, e) }));
// Barrel angle on screen: hanging, at the temple, lying on the floor. Mirrored by m.
const mirrorDeg = (deg, m) => (m > 0 ? deg : 180 - deg);
// Turn from angle a to b the short way round.
const lerpDeg = (a, b, u) => a + (Mathf.Repeat(b - a + 180, 360) - 180) * u;
const Barrel = { hang: -80, temple: 0, templeSide: 145, floor: 200 };
const LieGun = { x: -.62, z: -.63 };

// A handgun gripped at `hand`, barrel along screen angle `deg`: outline, grip, slide. The grip
// hangs off the barrel's lower side.
function pistol(key, hand, deg, layer, alpha = 1) {
  const r = deg * Mathf.Deg2Rad, dx = Math.cos(r), dz = Math.sin(r);
  let nx = dz, nz = -dx;
  if (nz > 0 || (Math.abs(nz) < 1e-3 && nx > 0)) { nx = -nx; nz = -nz; }
  const back = { x: hand.x - dx * .03, z: hand.z - dz * .03 }, muzzle = { x: hand.x + dx * .12, z: hand.z + dz * .12 };
  const g0 = { x: hand.x - dx * .01, z: hand.z - dz * .01 }, g1 = { x: g0.x + nx * .075 - dx * .02, z: g0.z + nz * .075 - dz * .02 };
  bar(key + ' so', back, muzzle, .065, Outline.withAlpha(alpha), layer);
  bar(key + ' go', g0, g1, .06, Outline.withAlpha(alpha), layer + .0002);
  bar(key + ' g', g0, g1, .038, Pistol.withAlpha(alpha), layer + .0004);
  bar(key + ' s', back, muzzle, .042, Pistol.withAlpha(alpha), layer + .0006);
  return muzzle;
}

// Wound spots on the body in its own frame: a along the body (1 = toward the head), b across.
const Wounds = [{ a: .30, b: -.45 }, { a: -.25, b: .40 }, { a: .02, b: .05 }];
function bodyPt(w, B, e, m) {
  const lie = { x: B.x - w.a * B.rx * .7, z: B.z + w.b * B.rz * .5 }, up = { x: B.x + w.b * B.rx * .5 * m, z: B.z + w.a * B.rz * .7 };
  return { x: lerp(lie.x, up.x, e), z: lerp(lie.z, up.z, e) };
}

export default {
  kit: 'Satō (Ajin)',
  label: 'Headshot Reset (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'wounded', options: ['wounded', 'after Sever'], group: 'Scenario' },
    facing: { label: 'Facing', value: 'south', options: ['south', 'east', 'north', 'west'], group: 'Scenario' },
    enemy: { label: 'Enemy and colonist', value: true, group: 'Scenario' },
    timer: { label: 'Timer ring', value: true, group: 'Scenario' },
    draw: P('Draw (cast 0.6 s = this + 0.15 hold)', .45, .2, 1, .05, 'Timing (s)'),
    delay: P('Play dead (6 s in game)', 1.2, .4, 4, .05, 'Timing (s)'),
    rebuild: P('Rebuild', .8, .3, 3, .05, 'Timing (s)'),
    rise: P('Rise', .7, .3, 2.5, .05, 'Timing (s)'),
    spray: P('Blood spray', 1, 0, 2, .05, 'Look'),
    flakes: P('Flakes', 1, 0, 2.5, .05, 'Look'),
  },

  duration(p) { return plan(p).end; },
  phases(p) {
    const L = plan(p);
    return [{ name: 'Draw', t: L.tD }, { name: 'Shot', t: L.tS }, { name: 'Fall', t: L.tF }, { name: 'Play dead', t: L.tL }, { name: 'Rebuild', t: L.t1 }, { name: 'Rise', t: L.t2 }];
  },
  events(p) {
    const L = plan(p);
    return [{ t: L.tS, type: 'shake', value: .03 }, { t: L.tS, type: 'sound', def: 'AG_SatoHeadshot' }];
  },

  draw(t, p, { origin, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const L = plan(p), O = (q) => ({ x: origin.x + q.x, z: origin.z + q.z });
    const profile = p.facing === 'east' || p.facing === 'west', m = p.facing === 'north' || p.facing === 'west' ? -1 : 1;
    const severed = p.scenario === 'after Sever';
    const kBuild = clamp((t - L.t1) / p.rebuild), kRise = clamp((t - L.t2) / p.rise);
    // Exit direction of the bullet on the floor plane: away from the gun side.
    const exit = profile ? { x: 0, z: 1 } : { x: m, z: 0 };
    const snap = t > L.tS && t < L.tS + .14 ? Math.sin((t - L.tS) / .14 * Math.PI) * .05 : 0;
    const home = O({ x: 0, z: 0 }), pos = { x: home.x + exit.x * snap, z: home.z + exit.z * snap };
    const k = standK(t, L), e = smooth(clamp(k));
    const raise = t < L.tD ? 0 : t < L.tF ? clamp((t - L.tD) / p.draw) : 0;

    // ---- The enemy, its aim and its shots (the playing-dead rule) ----
    const bodyAt = (tt) => standInBlend(home, Shirt, standK(tt, L), sun, strength, { draw: false }).B;
    const satoAim = (tt) => { const B = bodyAt(tt); return { x: B.x, z: B.z + .02 }; };
    const allyAim = { x: O(BagAt).x + .02, z: O(BagAt).z + .12 };
    const aimK = (tt) => {
      const a = clamp((tt - L.tF - .1) / .25), b = clamp((tt - (L.t3 - .25)) / .25);
      return smooth(a) * (1 - smooth(b));
    };
    const enemy = O(EnemyAt), gunAt = { x: enemy.x - .36, z: enemy.z - .18 };
    const aimPt = (tt) => { const s = satoAim(tt), K = aimK(tt); return { x: lerp(s.x, allyAim.x, K), z: lerp(s.z, allyAim.z, K) }; };
    const muzzleOf = (T) => { const dx = T.x - gunAt.x, dz = T.z - gunAt.z, l = Math.hypot(dx, dz) || 1; return { x: gunAt.x + dx / l * .30, z: gunAt.z + dz / l * .30 }; };
    const shots = [];
    for (let i = 0; ; i++) {
      const ts = .12 + i * ShotEvery;
      if (ts > L.t3 - .35) break;
      const onSato = aimK(ts) < .5, T = onSato ? satoAim(ts) : allyAim;
      shots.push({ ts, onSato, T, M: muzzleOf(T), hit: ts + .1 });
    }
    // Wounds 1 and 2 come from the first two shots that hit him (or are there from the start).
    const woundT = [severed ? Infinity : 0, 0, 0];
    const satoHits = shots.filter(s => s.onSato);
    woundT[1] = p.enemy ? (satoHits[0]?.hit ?? Infinity) : 0;
    woundT[2] = p.enemy ? (satoHits[1]?.hit ?? Infinity) : 0;

    const actors = [];
    if (p.enemy) {
      const ally = O(AllyAt), bag = O(BagAt);
      actors.push({ z: ally.z, fn: () => { standIn(ally, Ally, sun, strength, {}); rifle({ x: ally.x + .36, z: ally.z - .18 }, -5, pawnLayer + .003, 'hs ally rifle'); } });
      actors.push({
        z: bag.z, fn: () => {
          draw(disc, bag.x, pawnLayer + .004, bag.z, .36, .20, 0, Outline);
          draw(disc, bag.x, pawnLayer + .005, bag.z, .33, .17, 0, Sandbag);
          bar('hs bag seam', { x: bag.x - .26, z: bag.z + .01 }, { x: bag.x + .26, z: bag.z + .01 }, .02, Outline.withAlpha(.6), pawnLayer + .006);
        },
      });
      actors.push({
        z: enemy.z, fn: () => {
          standIn(enemy, Enemy, sun, strength, {});
          const T = aimPt(t), deg = Math.atan2(T.z - gunAt.z, T.x - gunAt.x) / Mathf.Deg2Rad;
          rifle(gunAt, deg, pawnLayer + .003, 'hs enemy rifle');
        },
      });
      // Aim line from the enemy's muzzle to whoever it is aiming at.
      const T = aimPt(t), M = muzzleOf(T);
      bar('hs aim', M, T, .016, AimCol.withAlpha(.32), Y + .001);
      shots.forEach((s, i) => {
        const age = t - s.ts;
        if (age < 0 || age > .45) return;
        if (age < .05) sprite(s.M, .22, .22, Flash.withAlpha(1 - age / .05), glow, Y + .03);
        const u = age / .1;
        if (u <= 1) {
          const head = { x: lerp(s.M.x, s.T.x, u), z: lerp(s.M.z, s.T.z, u) }, tu = Math.max(0, u - .35);
          bar(`hs tracer ${i}`, { x: lerp(s.M.x, s.T.x, tu), z: lerp(s.M.z, s.T.z, tu) }, head, .028, Tracer.withAlpha(.9), Y + .03);
          sprite(head, .12, .12, Tracer.withAlpha(.8), glow, Y + .031);
          return;
        }
        const v = clamp((age - .1) / .35), dx = s.T.x - s.M.x, dz = s.T.z - s.M.z, l = Math.hypot(dx, dz) || 1;
        for (let j = 0; j < 6; j++) {
          const a = Math.atan2(-dz, -dx) + (rand(i * 13 + j) - .5) * 2.2, r = v * (.12 + rand(i * 7 + j + 3) * .16);
          const c = { x: s.T.x + Math.cos(a) * r, z: s.T.z + Math.sin(a) * r * .7 + .08 * Math.sin(v * Math.PI) };
          if (s.onSato) draw(disc, c.x, Y + .02 + j * .0002, c.z, .028, .02, 0, Blood.withAlpha(1 - v));
          else sprite(c, .10 + v * .12, .08 + v * .1, Sandbag.withAlpha((1 - v) * .7), puff, Y + .02 + j * .0002);
        }
      });
    }

    // ---- The severed arm lying as an anchor (after Sever): stays the whole time ----
    if (severed) {
      const A = O(AnchorAt), a0 = { x: A.x - .17, z: A.z + .05 }, a1 = { x: A.x + .15, z: A.z - .04 };
      draw(disc, A.x - .12, Floor + .01, A.z + .01, .16, .08, 0, Blood.withAlpha(.8));
      limbSeg('hs anchor arm', a0, a1, .085, Shirt, Skin, pawnLayer - .01);
      draw(disc, a0.x, pawnLayer - .009, a0.z, .045, .045, 0, Blood);
      draw(ring, A.x, Floor + .02, A.z, .40, .40, 0, Slash.withAlpha(.45));
    }

    // ---- The head spray: ballistic drops from the exit wound, stains where they land ----
    const spray = [];
    if (t > L.tS && p.spray > 0) {
      const age = t - L.tS, base = Math.atan2(exit.z, exit.x), n = Math.round(26 * p.spray);
      const g0 = { x: home.x + exit.x * .2, z: home.z + Ground + exit.z * .2 };
      for (let i = 0; i < n; i++) {
        // Most drops are slow and land close in a narrow cone; a few fast ones reach 1.5 cells.
        // East/west: the flight north already rises on screen, so no extra upward launch (it
        // read as a fountain) and a wider fan behind the head.
        const a = base + (rand(i + 500) + rand(i + 505) - 1) * (profile ? .95 : .6), v = .7 + Math.pow(rand(i + 510), 1.7) * 2.9;
        const up = profile ? (rand(i + 520) - .6) * .8 : (rand(i + 520) - .25) * 1.4;
        const land = (up + Math.sqrt(up * up + 2 * G * HeadH)) / G, u = Math.min(age, land);
        const at = (uu) => ({ x: g0.x + Math.cos(a) * v * uu, z: g0.z + Math.sin(a) * v * uu + Math.max(0, HeadH + up * uu - .5 * G * uu * uu) });
        spray.push({ i, a, v, age, land, at, u, big: i % 6 === 0 });
      }
    }
    // Stains on the floor (stay), stretched along the way they flew; the fast ones land longer
    // and thinner. Then the drops still in the air and the mist.
    spray.forEach(s => {
      if (s.age < s.land) return;
      const q = s.at(s.land), g = smooth(clamp((s.age - s.land) / .12)), r = (s.big ? .07 : .038) + rand(s.i + 530) * .025, st = 1 + s.v * .35;
      draw(disc, q.x, Floor + .012 + s.i * .0001, q.z, r * g * st, r * g * .7, -s.a / Mathf.Deg2Rad, Blood.withAlpha(.85));
    });
    // The bulk of the blood lands close on the exit side as one patch.
    if (t > L.tS + .3 && p.spray > 0) {
      const g = smooth(clamp((t - L.tS - .3) / .25)), c = { x: home.x + exit.x * .55, z: home.z + Ground + exit.z * .55 };
      const rot = -Math.atan2(exit.z, exit.x) / Mathf.Deg2Rad;
      draw(disc, c.x, Floor + .011, c.z, .24 * g * Math.min(1, p.spray), .12 * g * Math.min(1, p.spray), rot, Blood.withAlpha(.8));
      draw(disc, c.x + exit.x * .2 + exit.z * .06, Floor + .0111, c.z + exit.z * .2 - exit.x * .05, .13 * g, .07 * g, rot, Blood.withAlpha(.8));
    }
    const airLayer = profile ? pawnLayer - .004 : Y + .02;
    spray.forEach(s => {
      if (s.age >= s.land) return;
      const q = s.at(s.u), q0 = s.at(Math.max(0, s.u - .035));
      bar(`hs drop ${s.i}`, q0, q, s.big ? .05 : .03, Blood, airLayer + s.i * .0001);
    });
    if (t > L.tS && t < L.tS + .5 && p.spray > 0) {
      const age = t - L.tS, u = age / .5;
      for (let j = 0; j < 6; j++) {
        const d = u * (.25 + rand(j + 560) * .35), sp = (rand(j + 570) - .5) * .5;
        const c = { x: home.x + exit.x * (.2 + d) - exit.z * sp * d, z: home.z + Ground + HeadH + exit.z * (.2 + d) + exit.x * sp * d - u * u * .15 };
        sprite(c, .12 + u * .3, .10 + u * .24, Blood.withAlpha((1 - u) * .5 * Math.min(1, p.spray)), puff, airLayer - .001 + j * .0001);
      }
    }

    // ---- Satō ----
    actors.push({
      z: home.z, fn: () => {
        // Blood pool under the head once he lies (stays).
        if (t > L.tL - .1) {
          const g = smooth(clamp((t - L.tL + .1) / 1.4));
          draw(disc, home.x - .47, Floor + .006, home.z - .24, .08 + .30 * g, .05 + .15 * g, 0, Blood.withAlpha(.78));
        }
        const st = standArms(profile, raise);
        const gunArm = blendArm(LieArms.gun, st.gun, m, e, pos), otherArm = blendArm(LieArms.other, st.other, m, e, pos);
        // Far arm (profile) behind the body.
        const otherBehind = profile, armW = .085;
        const drawOther = !severed || t >= L.t2;
        if (otherBehind && drawOther) { limbSeg('hs other up', otherArm[0], otherArm[1], armW, Shirt, Shirt, pawnLayer - .002); limbSeg('hs other lo', otherArm[1], otherArm[2], armW, Shirt, Skin, pawnLayer - .002); }
        // The pistol, where it is now: in the hand, falling, on the floor, picked up, then gone.
        let gun = null;
        const handDeg = (r) => mirrorDeg(lerpDeg(Barrel.hang, profile ? Barrel.templeSide : Barrel.temple, smooth(clamp(r))), m);
        const recoil = t > L.tS && t < L.tS + .25 ? 25 * (1 - (t - L.tS) / .25) * (profile ? -m : m) : 0;
        const floorAt = { x: home.x + LieGun.x, z: home.z + LieGun.z }, floorDeg = Barrel.floor;
        if (t >= L.tD && t < L.tF) gun = { at: gunArm[2], deg: handDeg(raise) + recoil, a: clamp((t - L.tD) / .08) };
        else if (t >= L.tF && t < L.tL) { const u = smooth((t - L.tF) / Fall); gun = { at: { x: lerp(gunArm[2].x, floorAt.x, u), z: lerp(gunArm[2].z, floorAt.z, u) }, deg: lerpDeg(handDeg(1), floorDeg, u), a: 1 }; }
        else if (t >= L.tL && t < L.t2) gun = { at: floorAt, deg: floorDeg, a: 1 };
        else if (t >= L.t2) {
          const u = smooth(kRise), gone = clamp((t - L.t3 - .4) / .3);
          if (gone < 1) gun = { at: { x: lerp(floorAt.x, gunArm[2].x, u), z: lerp(floorAt.z, gunArm[2].z, u) }, deg: lerpDeg(floorDeg, mirrorDeg(Barrel.hang, m), u), a: 1 - gone };
        }
        // North: the pistol at the temple sits behind the head.
        const gunBehind = p.facing === 'north' && t < L.tF;
        let muzzle = null;
        if (gun && gunBehind) muzzle = pistol('hs pistol', gun.at, gun.deg, pawnLayer - .003, gun.a);
        if (gun && t >= L.tF && t < L.t2) pistol('hs pistol', gun.at, gun.deg, pawnLayer - .006, gun.a);   // on the floor, under him

        const { B, H } = standInBlend(pos, Shirt, k, sun, strength, { cap: Cap });

        // Wounds on the body: open until Rebuild, closing under the cover.
        const healed = smooth(clamp(kBuild * 1.4));
        Wounds.forEach((w, i) => {
          if (t < woundT[i] || healed >= 1) return;
          const at = bodyPt(w, B, e, m), pop = smooth(clamp((t - woundT[i]) / .1));
          draw(disc, at.x, pawnLayer + .0005, at.z, .06 * pop * (1 - healed * .9), .045 * pop * (1 - healed * .9), 0, Blood.withAlpha(1 - healed));
          if (t > L.tL && t < L.t2) seep(`hs wound ${i}`, at, t, Math.min(1, (t - L.tL) / .3) * (1 - kBuild * .7), p.flakes);
        });
        // Head wounds: the exit wound while he still stands, both temples while he lies.
        if (t > L.tS && healed < 1) {
          const lying = t >= L.tL - .05;
          const spots = lying ? [{ x: H.x, z: H.z + .10 }, { x: H.x, z: H.z - .10 }]
            : [{ x: H.x + exit.x * H.rx * .85, z: H.z + (profile ? H.rz * .8 : .02) }];
          spots.forEach((s, i) => {
            draw(disc, s.x, pawnLayer + .0006, s.z, .05 * (1 - healed * .9), .04 * (1 - healed * .9), 0, Blood.withAlpha(1 - healed));
            if (lying && t < L.t2) seep(`hs head ${i}`, s, t, Math.min(1, (t - L.tL) / .25) * (1 - kBuild * .6) * 1.3, p.flakes);
          });
        }
        // Stump (after Sever) until the arm is back.
        if (severed && t < L.t2) {
          const s = otherArm[0];
          draw(disc, s.x, pawnLayer + .0007, s.z, .055, .05, 0, Blood);
          if (t < L.tS) for (let j = 0; j < 3; j++) {
            const u = ((t * 1.3 + j / 3) % 1);
            draw(disc, s.x + .01, pawnLayer + .0008, s.z - u * .28, .018, .022, 0, Blood.withAlpha(1 - u));
          }
        }

        // Near arms over the body; the gun arm last so the hand sits on the head.
        if (!otherBehind && drawOther) { limbSeg('hs other up', otherArm[0], otherArm[1], armW, Shirt, Shirt, pawnLayer + .001); limbSeg('hs other lo', otherArm[1], otherArm[2], armW, Shirt, Skin, pawnLayer + .001); }
        limbSeg('hs gun up', gunArm[0], gunArm[1], armW, Shirt, Shirt, pawnLayer + .0015);
        limbSeg('hs gun lo', gunArm[1], gunArm[2], armW, Shirt, Skin, pawnLayer + .0016);
        if (gun && !gunBehind && !(t >= L.tF && t < L.t2)) muzzle = pistol('hs pistol', gun.at, gun.deg, pawnLayer + .002, gun.a);

        // After Sever: the arm grows back out of the stump as black matter, then turns into the arm.
        if (severed && t > L.t1) {
          const g = smooth(clamp(kBuild / .8)), fade = 1 - smooth(clamp(kRise * 1.15));
          if (g > .01 && fade > .01) {
            const a = otherArm[0], tip = { x: lerp(a.x, otherArm[2].x, g), z: lerp(a.z, otherArm[2].z, g) }, mid = { x: lerp(a.x, otherArm[1].x, Math.min(1, g * 1.6)), z: lerp(a.z, otherArm[1].z, Math.min(1, g * 1.6)) };
            const lay = otherBehind ? pawnLayer - .0015 : pawnLayer + .0012;
            bar('hs regrow e1', a, mid, armW + .04, GhostEdge.withAlpha(fade), lay); bar('hs regrow e2', mid, tip, armW + .03, GhostEdge.withAlpha(fade), lay);
            bar('hs regrow 1', a, mid, armW, Ghost.withAlpha(fade), lay + .0001); bar('hs regrow 2', mid, tip, armW * .9, Ghost.withAlpha(fade), lay + .0001);
            if (t < L.t2) edgeFlakes('hs regrow flakes', { x: tip.x, z: tip.z - .02 }, 0, .12, t, { amount: .5 * p.flakes, rise: .3 });
          }
        }

        // Black matter over him: the head first, then the body; it flakes off as he stands.
        const off = 1 - smooth(clamp(kRise * 1.15)), grow = .6 + .4 * smooth(kBuild);
        const headCover = smooth(clamp(kBuild * 1.6)) * off, bodyCover = smooth(clamp((kBuild - .3) / .7)) * off;
        coverStandIn('hs', B, H, smooth(kRise), { body: bodyCover, head: headCover, grow, layer: pawnLayer + .0025 });
        if (t > L.t2 && kRise < 1) edgeFlakes('hs peel', { x: B.x, z: B.z - .2 }, .1 + e * .6, .6, t, { amount: 1.6 * p.flakes, rise: .8, alpha: 1 - kRise * .6 });

        // The shot: contact flash and smoke at the muzzle.
        if (muzzle && t > L.tS && t < L.tS + .08) {
          const u = (t - L.tS) / .08;
          sprite(muzzle, .34 * (1 + u * .4), .34 * (1 + u * .4), Flash.withAlpha(1 - u), glow, Y + .04);
          sprite(muzzle, .14, .14, Color.white.withAlpha(1 - u), glow, Y + .041);
        }
        const smokeFrom = muzzle ?? { x: home.x - .2 * m, z: home.z + .44 };
        for (let j = 0; j < 4; j++) {
          const age = t - L.tS - j * .05, u = age / .9;
          if (u <= 0 || u >= 1) continue;
          sprite({ x: smokeFrom.x + (rand(j + 600) - .5) * .12 + u * .08, z: smokeFrom.z + u * .38 }, .10 + u * .26, .09 + u * .22, Smoke.withAlpha((1 - u) * .38), puff, Y + .035 + j * .0002);
        }
      },
    });
    actors.sort((a, b) => b.z - a.z).forEach(a => a.fn());

    if (p.timer && t > L.tL && t < L.t2 + .3) {
      timerRing('hs timer', { x: home.x, z: home.z - .15 }, .70, clamp((t - L.tL) / (p.delay + p.rebuild)), 1 - smooth(clamp((t - L.t2) / .3)));
    }
  },
};
