// Itachi — Susanoo. Itachi's hero kit, ability 4 (agreed 2026-09-27; numbers are XML placeholders).
//
// Mechanic: self cast, warm-up 1 s, lasts 12 s; Itachi walks at half speed inside it.
//   Yata Mirror: every hit from outside the Susanoo is blocked.
//   Totsuka Blade: one stab at a pawn within 4 cells seals it: it leaves the map (counts as a
//   kill), leaves no corpse, and its gear drops where it stood.
//   When it ends: Itachi coughs blood (-30 % consciousness, 10 % blood loss, for 6 h).
//   Cooldown 1 day, 20 charge.
//
// Look: Itachi's complete armoured Susanoo (he never had Perfect), about 3.5 cells tall on
// screen. Sources (links in the session table, 2026-09-27): Shippuden ep 138 (armoured form,
// forming from yellow flame to red, the Totsuka as a liquid flame stream sealing Orochimaru, the
// mirror's rippling rings), the Kabuto fight eps 334-338 (skeletal stage, one yellow eye, Itachi
// visible inside the red), Storm 4's Totsuka Blade ultimate (a straight flame jet, the target
// drawn in), Shinobi Striker (flat orange fill, white contour lines, flame tips on the edges).
// Storm 4's awakening is the winged game-only form and was not used.
//
// Scenarios:
//   raise (built) — 0.25 s Itachi alone; then over the warm-up (1 s, beats as shares of it):
//     0-0.10 Sharingan glint, a red light pool opens on the floor;
//     0.10-0.40 the spine rises out of the floor and the ribs unroll round it, lowest first,
//       flame-yellow;
//     0.35-0.60 skull and the skeletal right arm grow, fingers close on nothing; one yellow eye
//       lights in the skull (the Kabuto fight's look);
//     0.55-1.00 armour, cape and head fill in outward from the ribs, contour lines draw on, the
//       flame edge lights from the shoulders up, colour settles from yellow to red; the gourd
//       forms and pours the Totsuka (0.66-0.96); the mirror's ring draws round (0.70-1.0);
//     0.93-1.00 the yellow eyes in the mouth slot light with a glint.
//     Then 1.8 s idle: flames flicker, the blade flows, the fill churns.
//   block (built) — the Susanoo is up from the start; a floor ring at radius 1.6 marks where
//     "outside" begins.
//     0.35-0.55 a raider 9 cells away (direction slider) aims; the mirror swings to face it: it
//       goes GuardR (1.45) from the chest toward the attacker as seen on screen, as far as the
//       left arm reaches (it may stretch 1.25x);
//     0.50, 0.70, 0.90 three rounds (30 cells/s) stop on the mirror's rim facing the shooter:
//       rings ripple across the face, sparks fly back, the ring flashes;
//     1.20-2.00 a sword raider runs in from the opposite side and stops 1.95 cells out;
//     1.88-2.10 the mirror crosses to meet it, through the lower half (in front of the chest);
//     2.00-2.20 wind-up and strike; 2.20 the blow stops on the mirror (ripple, sparks, shake),
//       the sword bounces back and the raider staggers back 0.35 cells (2.20-2.50).
//     2.70-3.00 the mirror goes back to rest. Itachi is not touched. With two attackers at the same moment in game, the second hit
//     would stop at the outline; the sketch shows them one after the other.
//   seal (built) — a floor ring at the 4-cell seal range; a raider (direction and distance
//     sliders) aims at Itachi. Times with the default Swing and stab 0.45 s, Pull-in 0.6 s:
//     0.50-0.66 the sword arm winds up (hand back and up, blade tilting back);
//     0.66-0.80 it swings: the hand goes 1.42 from the shoulder toward the raider as seen on
//       screen, the elbow straightens;
//     0.80-0.95 the Totsuka shoots out as a thin straight stream from the gourd to the raider's
//       chest; 0.95 it pierces: flash, shake; flames wrap the raider (0.95-1.18); its rifle drops;
//     1.10-1.70 the raider is pulled along the stream into the gourd: a dark silhouette rimmed with
//       flame, head first, stretching along the pull and shrinking, flame licks trailing, the
//       blade shortening behind it; the helmet pops off and falls where it stood;
//     1.70 a seal flash and a closing swirl at the gourd's mouth; 1.80-2.20 the arm goes back
//       to rest and the blade pours out again to its idle length.
//     The rifle and the helmet stay on the floor. No corpse.
//   end (built) — times with the default Break apart 1.0 s:
//     0-0.50 idle (the last moment of the 12 s);
//     0.50-0.80 it dims: fill and lines darker, flames drop to 45 %;
//     0.80-1.80 it breaks apart top first into rising red-orange embers (flecks of light, not
//       debris): head and crown 0.80-1.15, arms, blade and mirror 0.95-1.30, cape, torso and
//       shoulders 1.10-1.50; the bones show brighter as the armour leaves, then sink back into
//       the floor (collarbones and arm 1.35-1.60, spine and ribs 1.40-1.80, the raise in
//       reverse); the base flames and the light pool go out 1.50-1.80;
//     0.80-1.30 the Sharingan fades; 1.40-1.60 Itachi hunches; 1.50 and 1.75 he coughs blood:
//       droplets fly from his mouth and land up to 0.5 cells in front of him;
//     after: the blood stays on the floor and Itachi stays bowed.
//     The -30 % consciousness and 10 % blood loss are rules, not shown.
//
// Flame aura (all scenarios): a red haze round the figure rising a little above the head in
// soft flame tongues (Outer flame slider, 0 = off) (the anime's ep 138 0:22 and Kabuto-fight look), and flame tongues along
// the head, shoulders, cape and floor (Shinobi Striker's licks), both from SusanooFlame.png.
//
// Drawing: see lib/itachi.js. One view (always from the front); only the arms move toward a
// target. The growth is picture only: in game the warm-up is a flat 1 s.
import { Color, Mathf } from '../js/engine.js';
import { P } from './lib/six-paths-impact.js';
import {
  susanoo, growth, Complete, restPose, itachi, glint, raider, round, sparks, floorRing, mirrorAt, mirrorWorld, rimToward, toScreen,
  gourdMouth, handToward, pulled, flameWrap, sealFlash, droppedRifle, droppedHelmet, RestHand, RestBladeDeg,
  breaking, embers, blob, sprite, glow, rand, Y, Blood, Floor,
  DesignH, DesignW, FeetZ, GuardC, RestMirror, Sharingan, EyeHot, FlameMid, smooth, clamp, lerp,
} from './lib/itachi.js';

const Scenarios = ['raise', 'block', 'seal', 'end'];
const Lead = .25, Idle = 1.8;
const EdgeRadius = 1.6;   // floor ring: where "outside" begins
// Block beats (s) and distances (cells).
const Aim = .35, Face = .2, Shots = [.5, .7, .9], ShooterDist = 9, RoundSpeed = 30;
const Run = 1.2, RunTime = .8, MeleeFrom = 6, MeleeStop = 1.95, WindUp = .12, Strike = .08, Cross = [1.88, 2.1], Stagger = .35;
const Blow = Run + RunTime + WindUp + Strike, Settle = [2.7, 3.0], BlockLength = 3.6;
// Seal beats: the swing starts at SwingAt; Swing and stab and Pull-in come from the params.
const SwingAt = .5, PierceHold = .15, ReturnTime = .4, RegrowTime = .4, SealRange = 4;
// End beats: idle, dim, then the break-apart (its length is the Break apart param).
const EndIdle = .5, DimTime = .3, BreakAt = EndIdle + DimTime, EndTail = 1.0;
const endTimes = p => ({ hunch: BreakAt + .6 * p.breakUp, coughs: [BreakAt + .7 * p.breakUp, BreakAt + .95 * p.breakUp], end: BreakAt + p.breakUp + EndTail });
const sealTimes = p => {
  const pierce = SwingAt + p.stab, pullFrom = pierce + PierceHold, sealed = pullFrom + p.pull;
  return { windEnd: SwingAt + p.stab * .35, strikeEnd: SwingAt + p.stab * .65, pierce, pullFrom, sealed, end: sealed + 1.3 };
};
const deg = (a, b) => Math.atan2(b.z - a.z, b.x - a.x) * 180 / Math.PI;
const unit = (a, b) => { const dx = b.x - a.x, dz = b.z - a.z, L = Math.hypot(dx, dz) || 1; return { x: dx / L, z: dz / L }; };

export default {
  kit: 'Itachi',
  label: 'Susanoo (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'raise', options: Scenarios, group: 'Mechanic' },
    dir: P('Target / attacker direction (degrees, 0 = east)', 30, 0, 359, 1, 'Mechanic'),
    dist: P('Seal target distance (cells)', 3.5, 1.5, 4, .1, 'Mechanic'),
    rings: { label: 'Show rule rings', value: true, group: 'Mechanic' },
    warmUp: P('Warm-up', 1.0, .4, 2, .05, 'Timing (s)'),
    stab: P('Swing and stab', .45, .2, 1, .05, 'Timing (s)'),
    pull: P('Pull-in', .6, .2, 1.5, .05, 'Timing (s)'),
    breakUp: P('Break apart', 1.0, .4, 2, .05, 'Timing (s)'),
    height: P('Height on screen (cells)', 3.5, 2.5, 4.5, .05, 'Shape'),
    width: P('Width (cells)', 3.2, 2.2, 4.2, .05, 'Shape'),
    fill: P('Fill opacity', .6, .2, 1, .01, 'Shape'),
    line: P('Line brightness', 1.0, .3, 1.5, .01, 'Shape'),
    flameH: P('Flame tip height (cells)', .45, .1, 1, .01, 'Shape'),
    aura: P('Outer flame (aura)', 1, 0, 2, .05, 'Shape'),
    bladeLen: P('Blade length at idle (cells)', 2.2, 1.2, 3.2, .05, 'Shape'),
  },

  duration(p) { return { raise: Lead + p.warmUp + Idle, block: BlockLength, seal: sealTimes(p).end, end: endTimes(p).end }[p.scenario]; },
  events(p) {
    if (p.scenario === 'block') return [{ t: Blow, type: 'shake', value: .08 }];
    if (p.scenario === 'seal') return [{ t: sealTimes(p).pierce, type: 'shake', value: .1 }];
    return [];
  },
  phases(p) {
    if (p.scenario === 'seal') {
      const T = sealTimes(p);
      return [
        { name: 'Susanoo up', t: 0 }, { name: 'Wind-up', t: SwingAt }, { name: 'Swing', t: T.windEnd }, { name: 'Blade shoots out', t: T.strikeEnd },
        { name: 'Pierce', t: T.pierce }, { name: 'Pulled into the gourd', t: T.pullFrom }, { name: 'Sealed', t: T.sealed },
      ];
    }
    if (p.scenario === 'block') return [
      { name: 'Susanoo up', t: 0 }, { name: 'Shooter aims, mirror turns', t: Aim }, { name: 'Rounds', t: Shots[0] },
      { name: 'Sword raider runs in', t: Run }, { name: 'Mirror crosses', t: Cross[0] }, { name: 'Blow blocked', t: Blow },
    ];
    if (p.scenario === 'end') {
      const T = endTimes(p);
      return [
        { name: 'Last moment', t: 0 }, { name: 'Dims', t: EndIdle }, { name: 'Head breaks up', t: BreakAt }, { name: 'Arms, mirror, blade', t: BreakAt + .15 * p.breakUp },
        { name: 'Body', t: BreakAt + .3 * p.breakUp }, { name: 'Bones sink', t: BreakAt + .55 * p.breakUp }, { name: 'Coughs blood', t: T.coughs[0] }, { name: 'Gone', t: BreakAt + p.breakUp },
      ];
    }
    const w = p.warmUp;
    return [
      { name: 'Itachi alone', t: 0 }, { name: 'Sharingan', t: Lead }, { name: 'Ribs rise', t: Lead + .10 * w },
      { name: 'Skull + skeletal arm', t: Lead + .35 * w }, { name: 'Armour, cape, face', t: Lead + .55 * w },
      { name: 'Eyes light', t: Lead + .93 * w }, { name: 'Complete (idle)', t: Lead + w },
    ];
  },

  draw(seconds, p, { origin, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const look = { kx: p.width / DesignW, kz: p.height / DesignH, fill: p.fill, line: p.line, flameH: p.flameH, bladeLen: p.bladeLen, aura: p.aura };
    const F = { x: origin.x, z: origin.z + FeetZ };
    let g = Complete, eyes = 1, hunch = 0;
    if (p.scenario === 'raise') {
      g = growth(seconds - Lead, p.warmUp);
      eyes = g.started ? 1 : 0;
    }
    if (p.scenario === 'end') {
      const T = endTimes(p), tb = seconds - BreakAt, dim = smooth((seconds - EndIdle) / DimTime);
      g = seconds < BreakAt ? { ...Complete, dim } : breaking(tb, p.breakUp, 1);
      eyes = 1 - smooth(tb / (.5 * p.breakUp));
      // Hunched from the first cough on, with a jolt forward at each cough.
      hunch = smooth((seconds - T.hunch) / .2) * .8 + T.coughs.reduce((h, c) => h + (seconds >= c ? .35 * Math.exp(-(seconds - c) * 9) : 0), 0);
      if (tb >= 0) embers(F, look, tb, p.breakUp);
    }
    const me = itachi(origin, sun, strength, { eyes, hunch });
    if (p.scenario === 'end') cough(seconds, p, origin, me);
    if (p.scenario === 'raise' && g.glint > 0) for (const side of [-1, 1]) glint({ x: me.head.x + side * .07, z: me.head.z - .01 }, .5, g.glint, Color.Lerp(Sharingan, EyeHot, .35));
    const pose = restPose();
    if (p.scenario === 'block') block(seconds, p, origin, F, look, sun, strength, pose);
    if (p.scenario === 'seal') seal(seconds, p, origin, F, look, sun, strength, pose);
    susanoo('susanoo', F, seconds, look, g, pose);
  },
};

// The Block scenario: moves the mirror in `pose` and draws the two raiders, the rounds and the hits.
function block(t, p, origin, F, look, sun, strength, pose) {
  if (p.rings) floorRing(origin, EdgeRadius, FlameMid, .35);
  const guardC = toScreen(F, look, GuardC.u, GuardC.v);
  const aDeg = p.dir, a = aDeg * Mathf.Deg2Rad, bDeg = aDeg + 180, b = bDeg * Mathf.Deg2Rad;
  const shooterPos = { x: origin.x + Math.cos(a) * ShooterDist, z: origin.z + Math.sin(a) * ShooterDist };
  const shooterChest = { x: shooterPos.x, z: shooterPos.z + .06 };
  const meleeAt = u => { const d = lerp(MeleeFrom, MeleeStop, u); return { x: origin.x + Math.cos(b) * d, z: origin.z + Math.sin(b) * d }; };
  // Guard angles as seen on screen from the Susanoo's chest.
  const gA = deg(guardC, shooterChest), gB0 = deg(guardC, { x: meleeAt(1).x, z: meleeAt(1).z + .06 });
  // Cross through the lower half: pick the B angle (±360) whose midway angle points down.
  const gB = [gB0, gB0 + 360, gB0 - 360].sort((x, y) => Math.abs(x - gA) - Math.abs(y - gA)).find(v => Math.sin(((gA + v) / 2) * Mathf.Deg2Rad) < 0) ?? gB0;
  const mirrorFor = time => {
    if (time < Aim) return RestMirror;
    if (time < Aim + Face) { const m = mirrorAt(gA), f = smooth((time - Aim) / Face); return { u: lerp(RestMirror.u, m.u, f), v: lerp(RestMirror.v, m.v, f) }; }
    if (time < Cross[0]) return mirrorAt(gA);
    if (time < Cross[1]) return mirrorAt(lerp(gA, gB, smooth((time - Cross[0]) / (Cross[1] - Cross[0]))));
    // Back to rest once the raider has been thrown off, so Itachi is not left behind the shield.
    const m = mirrorAt(gB), f = smooth((time - Settle[0]) / (Settle[1] - Settle[0]));
    return { u: lerp(m.u, RestMirror.u, f), v: lerp(m.v, RestMirror.v, f) };
  };
  pose.mirror = mirrorFor(t);
  pose.mirrorHit = [];
  let flash = 0;

  // The shooter and its three rounds.
  const rifleAim = deg(shooterChest, mirrorWorld(F, look, t, { mirror: mirrorAt(gA) }));
  const shooter = raider(shooterPos, sun, strength, { weapon: 'rifle', aimDeg: t < Aim ? rifleAim - 35 : rifleAim });
  Shots.forEach((t0, k) => {
    const m = mirrorWorld(F, look, t0, { mirror: mirrorAt(gA) }), rim = rimToward(m, shooter.tip);
    const dist = Math.hypot(rim.x - shooter.tip.x, rim.z - shooter.tip.z), hitAt = t0 + dist / RoundSpeed, dir = unit(shooter.tip, rim);
    if (t >= t0 && t < t0 + .06) glint(shooter.tip, .35, 1 - (t - t0) / .06, EyeHot);
    if (t >= t0 && t < hitAt) { const f = (t - t0) / (hitAt - t0); round(`block round ${k}`, { x: lerp(shooter.tip.x, rim.x, f), z: lerp(shooter.tip.z, rim.z, f) }, dir); }
    if (t >= hitAt) {
      const age = t - hitAt;
      pose.mirrorHit.push({ age, at: rim.at });
      sparks(`block sparks ${k}`, rim, { x: -dir.x, z: -dir.z }, age, 8, .35, k);
      flash = Math.max(flash, Math.max(0, 1 - age / .2));
    }
  });

  // The sword raider: runs in, winds up, strikes the mirror, is thrown back.
  const runU = clamp((t - Run) / RunTime), stag = smooth((t - Blow) / .3) * Stagger;
  let pos = meleeAt(1 - Math.pow(1 - runU, 1.6));
  pos = { x: pos.x + Math.cos(b) * stag, z: pos.z + Math.sin(b) * stag };
  const mB = mirrorWorld(F, look, Blow, { mirror: mirrorAt(gB) });
  const handAtBlow = { x: meleeAt(1).x + Math.cos(a) * .156, z: meleeAt(1).z - .33 + .39 + Math.sin(a) * .08 };
  const rimB = rimToward(mB, handAtBlow);
  const toRim = deg(handAtBlow, rimB), reach = Math.min(1.0, Math.max(.45, Math.hypot(rimB.x - handAtBlow.x, rimB.z - handAtBlow.z)));
  // Raised = away from the mirror and up; the strike sweeps from there onto the rim.
  const back = unit(rimB, handAtBlow), raised = Math.atan2(back.z + 1.2, back.x) * 180 / Math.PI;
  const toRimAlt = [toRim, toRim + 360, toRim - 360].sort((x, y) => Math.abs(x - raised) - Math.abs(y - raised))[0];
  let wDeg = aDeg + 60, wLen = .6;
  if (t >= Blow - Strike - WindUp && t < Blow - Strike) { const f = smooth((t - (Blow - Strike - WindUp)) / WindUp); wDeg = lerp(aDeg + 60, raised, f); }
  else if (t >= Blow - Strike && t < Blow) { const f = (t - (Blow - Strike)) / Strike; wDeg = lerp(raised, toRimAlt, f * f); wLen = lerp(.6, reach, f); }
  else if (t >= Blow) {
    // Bounces back off the mirror, then drops to the resting hold.
    const bounce = lerp(toRimAlt, raised, .6 * smooth((t - Blow) / .15));
    wDeg = lerp(bounce, aDeg + 60, smooth((t - Blow - .3) / .3));
    wLen = lerp(reach, .6, clamp((t - Blow) / .3));
  }
  if (t >= Run - .2) raider(pos, sun, strength, { weapon: 'sword', weaponDeg: wDeg, weaponLen: wLen, alpha: clamp((t - Run + .2) / .2), lean: t >= Blow && t < Blow + .4 ? -Math.cos(a) : 0 });
  if (t >= Blow) {
    const age = t - Blow;
    pose.mirrorHit.push({ age, at: rimB.at });
    sparks('block sparks blow', rimB, back, age, 12, .4, 9);
    flash = Math.max(flash, Math.max(0, 1 - age / .25));
  }
  pose.mirrorFlash = flash;
}

// The Seal scenario: swings the sword arm in `pose`, draws the raider, its pull into the gourd,
// the seal and the dropped gear.
function seal(t, p, origin, F, look, sun, strength, pose) {
  const T = sealTimes(p);
  if (p.rings) floorRing(origin, SealRange, FlameMid, .35);
  const a = p.dir * Mathf.Deg2Rad;
  const pos = { x: origin.x + Math.cos(a) * p.dist, z: origin.z + Math.sin(a) * p.dist };
  const chest = { x: pos.x, z: pos.z + .06 };
  const reachHand = handToward(F, look, chest);
  // The wind-up pulls the hand back from the target and up.
  const away = { u: RestHand.u - (reachHand.u - RestHand.u) * .25, v: RestHand.v + .45 };
  const strikeDeg = deg(toScreen(F, look, reachHand.u, reachHand.v), chest);
  const windDeg = RestBladeDeg + (strikeDeg > 90 || strikeDeg < -90 ? -25 : 25);
  let hand = RestHand, bladeDeg = RestBladeDeg, elbowDown = 1;
  const k = x => smooth(x);
  if (t >= SwingAt && t < T.windEnd) {
    const f = k((t - SwingAt) / (T.windEnd - SwingAt));
    hand = { u: lerp(RestHand.u, away.u, f), v: lerp(RestHand.v, away.v, f) }; bladeDeg = lerp(RestBladeDeg, windDeg, f); elbowDown = 1 - f * .5;
  } else if (t >= T.windEnd && t < T.sealed + .1) {
    const f = k((t - T.windEnd) / (T.strikeEnd - T.windEnd));
    hand = { u: lerp(away.u, reachHand.u, f), v: lerp(away.v, reachHand.v, f) };
    const target = t < T.strikeEnd ? lerp(windDeg, strikeDeg, f) : strikeDeg;
    bladeDeg = target; elbowDown = .5 * (1 - f);
  } else if (t >= T.sealed + .1) {
    const f = k((t - T.sealed - .1) / ReturnTime);
    hand = { u: lerp(reachHand.u, RestHand.u, f), v: lerp(reachHand.v, RestHand.v, f) }; bladeDeg = lerp(strikeDeg, RestBladeDeg, f); elbowDown = f;
  }
  Object.assign(pose, { hand, bladeDeg, elbowDown });
  const mouth = gourdMouth(F, look, t, pose);

  // The blade: idle, tilted in the wind-up, then shot out to the chest, then holding the raider
  // while it is pulled in, then gone into the gourd and pouring out again.
  if (t >= T.strikeEnd && t < T.pullFrom) {
    const f = k((t - T.strikeEnd) / (T.pierce - T.strikeEnd));
    const idleTip = { x: mouth.x + Math.cos(strikeDeg * Mathf.Deg2Rad) * look.bladeLen * .5, z: mouth.z + Math.sin(strikeDeg * Mathf.Deg2Rad) * look.bladeLen * .5 };
    pose.bladeTip = { x: lerp(idleTip.x, chest.x, f), z: lerp(idleTip.z, chest.z, f) }; pose.stab = f;
  }
  const pullU = clamp((t - T.pullFrom) / p.pull), pullE = pullU * pullU;
  const pulledAt = { x: lerp(chest.x, mouth.x, pullE), z: lerp(chest.z, mouth.z, pullE) };
  if (t >= T.pullFrom && t < T.sealed) { pose.bladeTip = pulledAt; pose.stab = 1; }
  if (t >= T.sealed) pose.bladeLen = look.bladeLen * clamp((t - T.sealed - .2) / RegrowTime);
  if (t >= T.sealed && t < T.sealed + .2) pose.bladeLen = 0;

  // The raider: aims until pierced, burns, then is pulled in.
  if (t < T.pierce + .05) raider(pos, sun, strength, { weapon: 'rifle', aimDeg: deg(chest, origin) });
  else if (t < T.pullFrom) raider(pos, sun, strength, { weapon: 'none', lean: -Math.cos(a) * .5 });
  if (t >= T.pierce && t < T.pullFrom + .08) flameWrap('seal wrap', pos, t, Math.min(1, (t - T.pierce) / .08) * (1 - clamp((t - T.pullFrom) / .08)));
  if (t >= T.pierce && t < T.pierce + .15) glint(chest, .7, 1 - (t - T.pierce) / .15, EyeHot);
  if (t >= T.pullFrom && t < T.sealed) pulled('seal pulled', pulledAt, deg(chest, mouth), pullU, 1 - pullU * .4, t);
  if (t >= T.sealed) sealFlash(mouth, t - T.sealed);
  // Gear: the rifle drops as the raider is pierced; the helmet pops off as it is pulled away.
  const rifleDeg = deg(chest, origin) + 30;
  if (t >= T.pierce + .05) droppedRifle('seal rifle', { x: pos.x + .15, z: pos.z - .15 }, rifleDeg, clamp((t - T.pierce - .05) / .3));
  if (t >= T.pullFrom) droppedHelmet({ x: pos.x - .25, z: pos.z + .05 }, clamp((t - T.pullFrom) / .35));
}

// Blood from Itachi's coughs: droplets leave the mouth in an arc and land in front of him
// (south, toward the camera); each landing leaves a spot that stays. A dark puff at the mouth.
function cough(t, p, origin, me) {
  const T = endTimes(p);
  T.coughs.forEach((c, j) => {
    const age = t - c;
    if (age < 0) return;
    const mouth = { x: me.head.x, z: me.head.z - .1 };
    if (age < .2) sprite(mouth, .2 + age * .8, .16 + age * .5, Blood.withAlpha(.6 * (1 - age / .2)), glow, Y + .07);
    for (let i = 0; i < 9; i++) {
      const r = k => rand(j * 100 + i * 7 + k + 900);
      const land = { x: origin.x + (r(1) - .5) * .5, z: origin.z - .38 - r(2) * .45 }, fly = .22 + .14 * r(3), u = Math.min(1, age / fly);
      const arc = .25 * Math.sin(u * Math.PI);
      const pos = { x: lerp(mouth.x, land.x, u), z: lerp(mouth.z, land.z, u) + arc };
      if (u < 1) blob(pos, .025 + .015 * r(4), .025 + .015 * r(4), Blood, Y + .071);
      else blob(land, (.035 + .035 * r(5)) * Math.min(1, (age - fly) / .1 + .5), (.025 + .025 * r(5)) * Math.min(1, (age - fly) / .1 + .5), Blood.withAlpha(.9), Floor + .004 + i * .0001);
    }
  });
}
