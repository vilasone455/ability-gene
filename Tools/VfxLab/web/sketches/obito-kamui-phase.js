// Obito — Kamui: Phase. A picture for the agreed mechanic, ported to
// Source/RimArt/Obito/ObitoFX.cs, the bent body (2026-09-28).
//
// Mechanic (agreed 2026-09-27, numbers are XML placeholders): a toggle. While it is on, Obito is
// intangible and spends his own pool: 30 s, refilling 1 s per 4 s solid (a gizmo bar, not drawn
// here). Every hit on his whole body goes into the Kamui dimension (InvoluteUtility.PassThrough):
// a round is relaunched inside (kamui-dimension.js, "rounds sent in"), other damage lands on a
// random cell there. While phased he cannot attack, carry or cast. Turning solid takes 0.25 s.
// Cast cost 0.
//
// Beats:
//   0.30   toggle on: a twist runs out of the right eye over the body and springs back, and the
//          body turns see-through over 0.3 s. A faint slow swirl stays on the eye while he is
//          phased, so the state reads at a glance.
//   rounds each round stops at the point it hits: a small swirl opens there and the tracer runs
//          into it. Nothing comes out behind him. The body twists a little round the hit point.
//   stabs  a raider's knife cuts through him; the body twists where the blade is and he does not
//          flinch.
//   2.90   toggle off: the eye swirl winds shut and he is solid again in 0.25 s.
//
// Drawing: lib/obito.js. The body is the picture grid (see-through by alpha) with twist() at each
// hit, so the port draws the pawn's own picture the same way. Stand-ins: pawns, tracer, knife.
import { Color } from '../js/engine.js';
import {
  P, L, Edge, Round, smooth, clamp, lerp, hash, figure, obitoKind, eye, facingOf, twist, compose,
  swirl, sprite, glow, rod, trail, draw, disc, Skin,
} from './lib/obito.js';

// Rule numbers (XML later) and decided looks.
const On = .3, Off = 2.9, Solid = .25, FadeIn = .3, End = Off + Solid + .45;
const RoundSpeed = 26, FirstShot = .75, ShotGap = .32, Tracer = .7, Sink = .14;
const Swings = [1.05, 1.75, 2.4], SwingTime = .26;
const Steel = new Color(.84, .86, .9), Flash = new Color(1, .85, .45);

export default {
  kit: 'Obito', label: 'Kamui: Phase (sketch)',
  params: {
    scenario: { label: 'Attack', value: 'shot at', options: ['shot at', 'stabbed', 'both'], group: 'Showcase' },
    aim: P('Shooter direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Shooter distance (cells)', 7, 3, 14, 1, 'Showcase'),
    shots: P('Rounds', 5, 1, 7, 1, 'Showcase'),
    ghost: P('Body opacity while phased', .5, .2, .9, .05, 'Look'),
    eyeSwirl: P('Eye swirl radius (cells)', .3, .15, .6, .05, 'Look'),
  },
  duration() { return End; },
  phases() {
    return [{ name: 'Solid', t: 0 }, { name: 'Phase on', t: On }, { name: 'Hits pass into the dimension', t: FirstShot }, { name: 'Solid in 0.25 s', t: Off }];
  },
  events() { return []; },

  draw(s, p, { origin: o, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const shoot = p.scenario !== 'stabbed', stab = p.scenario !== 'shot at';
    const a = p.aim * Math.PI / 180, dir = { x: Math.cos(a), z: Math.sin(a) };
    const facing = facingOf(p.aim), e = eye(o, facing);

    // Phased amount: 0 solid, 1 fully phased.
    const ph = smooth((s - On) / FadeIn) * (1 - smooth((s - Off) / Solid));
    const warps = [
      twist(e, .18 * Math.sin(Math.PI * clamp((s - On) / .45)), .8),
      twist(e, -.14 * Math.sin(Math.PI * clamp((s - Off) / .3)), .8),
    ];

    // Rounds: each stops at its hit point and runs into a small swirl there.
    if (shoot) {
      const shooter = { x: o.x + dir.x * p.distance, z: o.z + dir.z * p.distance };
      figure('phase shooter', 'enemy', shooter, sun, strength);
      const muzzle = { x: shooter.x - dir.x * .35, z: shooter.z - dir.z * .35 + .3 };
      for (let i = 0; i < p.shots; i++) {
        const age = s - FirstShot - i * ShotGap;
        if (age < 0) continue;
        const hp = { x: o.x + (hash(i, 1, 77) - .5) * .28, z: o.z + .08 + hash(i, 2, 77) * .55 };
        const dx = hp.x - muzzle.x, dz = hp.z - muzzle.z, d = Math.hypot(dx, dz), u = { x: dx / d, z: dz / d }, fly = d / RoundSpeed;
        if (age < .06) sprite(muzzle, .35, .35, Flash.withAlpha(.9 * (1 - age / .06)), glow, L.fx);
        if (age < fly) {
          const head = { x: muzzle.x + u.x * age * RoundSpeed, z: muzzle.z + u.z * age * RoundSpeed }, len = Math.min(Tracer, age * RoundSpeed);
          rod(`phase round ${i}`, { x: head.x - u.x * len, z: head.z - u.z * len }, head, .05, Round, L.fx + .01);
        } else if (age < fly + Sink) {
          const k = (age - fly) / Sink, len = Tracer * (1 - k);
          rod(`phase round ${i}`, { x: hp.x - u.x * len, z: hp.z - u.z * len }, hp, .05 * (1 - .6 * k), Round.withAlpha(1 - .5 * k), L.fx + .01);
        }
        const open = smooth((age - fly + .03) / .06), close = 1 - smooth((age - fly - .18) / .17);
        if (age > fly - .03 && close > 0) swirl(`phase hit ${i}`, hp, .2 * (.6 + .4 * open), -age * 14, open * close * .9, { arms: 3, wind: .7, haze: .6 });
        warps.push(twist(hp, .45 * Math.sin(Math.PI * clamp((age - fly) / .35)), .38));
      }
    }

    // Stabs: a raider next to him cuts through; the knife passes, the body twists where it is.
    if (stab) {
      const sa = a + (shoot ? Math.PI * .85 : 0), f = { x: -Math.cos(sa), z: -Math.sin(sa) }, n = { x: -f.z, z: f.x };
      const raider = { x: o.x - f.x, z: o.z - f.z };
      figure('phase stabber', 'enemy', raider, sun, strength);
      Swings.forEach((w, i) => {
        const u = (s - w) / SwingTime;
        const pose = v => {
          const sv = smooth(v), reach = .25 + .3 * Math.sin(Math.PI * clamp(v)), side = lerp(.3, -.3, sv), turn = lerp(55, -55, sv) * Math.PI / 180;
          const hand = { x: raider.x + f.x * reach + n.x * side, z: raider.z + f.z * reach + n.z * side + .3 };
          const bd = { x: f.x * Math.cos(turn) - n.x * Math.sin(turn), z: f.z * Math.cos(turn) - n.z * Math.sin(turn) };
          return { hand, tip: { x: hand.x + bd.x * .5, z: hand.z + bd.z * .5 } };
        };
        if (u < 0 || u > 1.25) return;
        const { hand, tip } = pose(Math.min(1, u));
        if (u <= 1) {
          rod(`phase knife ${i}`, hand, tip, .045, Steel, L.fx + .012);
          draw(disc, hand.x, L.fx + .013, hand.z, .05, .05, 0, Skin);
        }
        const smear = [];
        for (let j = 0; j <= 8; j++) smear.push(pose(clamp(u - .35 + .35 * j / 8)).tip);
        trail(`phase smear ${i}`, smear, .07, Edge.withAlpha(.45 * (1 - clamp((u - .9) / .35))), L.fx + .011);
        const inside = Math.hypot(tip.x - o.x, tip.z - (o.z + .3)) < .42;
        if (inside && u <= 1) swirl(`phase cut ${i}`, tip, .14, -s * 16, .8, { arms: 3, wind: .7, haze: .5 });
        warps.push(twist(pose(.5).tip, .4 * Math.sin(Math.PI * clamp(u)), .45));
      });
    }

    // Obito himself, see-through while phased, bent by every twist above.
    figure('phase obito', obitoKind(facing), o, sun, strength, { alpha: lerp(1, p.ghost, ph), warp: compose(...warps), shadow: lerp(1, .5, ph) });
    const eyeOn = smooth((s - On) / .15) * (1 - smooth((s - Off) / Solid));
    const burst = 1 + .7 * Math.sin(Math.PI * clamp((s - On) / .45));
    swirl('phase eye', e, p.eyeSwirl * burst * (1 - .6 * smooth((s - Off) / Solid)), -s * 2.2, .55 * eyeOn, { arms: 3, haze: .4 });
  },
};
