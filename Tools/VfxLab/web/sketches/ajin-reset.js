// Satō (Ajin) — Reset, the passive: rising and regrowth.
//
// Mechanic (agreed 2026-09-26, numbers are XML placeholders): Satō never really dies. A lethal
// hit, being downed for 5 s, Headshot Reset or any damaging explosion puts him in the Reset state:
// he lies still with a timer. In Descent with >= 25 % meter the delay is 20 s and costs 25 %;
// otherwise 1 in-game day, in place, anchors ignored. He rises at his biggest piece: the body
// (100 minus severed parts, 0 once destroyed by an explosion or > 50 % damage during the delay)
// or an anchor (leg 14, arm 12, hand 2, finger 1). The Reset clears injuries, missing natural
// parts, disease, toxic buildup, burning and mental states. Rising at an anchor he is naked and
// unarmed; his gear stays where the body was, and his other anchors crumble. The sketch
// compresses the 20 s into the Delay phase.
//
// Beats, "in place": he lies with three wounds in a blood pool. Delay — black matter seeps out of
// the wounds as flakes and smoke, and a timer ring fills round him. Rebuild — the matter spreads
// over the whole body into a dark banded shell and the wounds close under it. Rise — he gets up
// from lying to standing while the shell flakes off upward. The blood pool stays.
// "At an anchor": the body was blown apart; a scorch mark, blood and his gear (shirt, cap, rifle)
// lie there. His severed hand lies 2.6 cells away and a finger further off. Delay — matter seeps
// from the hand, timer ring round it. Rebuild — a dark shell in his shape builds up from the floor
// over the hand, flakes thrown off its rising edge; the hand is taken in. Rise — the shell peels
// from the head down and he stands there naked (no shirt, no cap); the finger crumbles to dust at
// the same moment. Scorch and gear stay.
//
// Look: the same black matter as the Black Ghost (flakes, smoke, banded shell) — the Ajin wiki
// says IBM oozes out of the body to replace lost tissue. No source frames of the regrowth were
// found, so the beats are this reading, not a copy of a scene.
//
// Drawing: see lib/ajin.js (shell, standInBlend, edgeFlakes, arc, hand). The shell is a band
// between a left and a right edge in screen space, built to the real pawn's outline, so it has no
// per-facing work. Pawns are the lab's two-disc stand-ins at real size.
import { Color, Meshes, Mathf } from '../js/engine.js';
import { P } from './lib/six-paths-impact.js';
import {
  edgeFlakes, standInBlend, shell, Shell, arc, hand, shard, Stand, Floor, Y, pawnLayer,
  Shirt, Cap, Skin, Blood, Flake, Ghost, GhostEdge, Wrap, draw, disc, sprite, trail, puff, rand, smooth, clamp, lerp, TAU,
} from './lib/ajin.js';

const Hold = .35, Tail = .9;
const Scorch = new Color(.09, .07, .06), Cloth = new Color(.80, .80, .77), Steel = new Color(.16, .16, .17);
const TimerCol = new Color(.85, .85, .9);
const ringMesh = Meshes.band(.9, 1, 48, 'ajin reset ring');

function plan(p) {
  const t0 = Hold, t1 = t0 + p.delay, t2 = t1 + p.rebuild, t3 = t2 + p.rise;
  return { t0, t1, t2, t3, end: t3 + Tail };
}

// Wound spots on the lying body, relative to the body's centre.
const Wounds = [{ x: -.12, z: .04 }, { x: .16, z: -.05 }, { x: .30, z: .08 }];

// Black matter seeping from a point: flakes and smoke rising, `k` how strong.
function seep(key, at, t, k, amount) {
  if (k <= 0) return;
  edgeFlakes(key, { x: at.x, z: at.z }, 0, .16, t, { amount: .45 * amount * k, rise: .35 });
  for (let i = 0; i < 3; i++) {
    const u = Mathf.Repeat(t * 1.6 + rand(i + key.length), 1);
    sprite({ x: at.x + (rand(i + 5) - .5) * .12, z: at.z + u * .22 }, .14 * (1 + u), .11 * (1 + u), Flake.withAlpha((1 - u) * .45 * k), puff, Y + .004 + i * .0003);
  }
}

export default {
  kit: 'Satō (Ajin)',
  label: 'Reset (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'at anchor', options: ['in place', 'at anchor'], group: 'Scenario' },
    timer: { label: 'Timer ring', value: true, group: 'Scenario' },
    delay: P('Delay (20 s in game)', 1.2, .4, 4, .05, 'Timing (s)'),
    rebuild: P('Rebuild', 1.1, .4, 3, .05, 'Timing (s)'),
    rise: P('Rise', .9, .3, 2.5, .05, 'Timing (s)'),
    flakes: P('Flakes', 1.0, 0, 2.5, .05, 'Look'),
  },

  duration(p) { return plan(p).end; },
  phases(p) { const L = plan(p); return [{ name: 'Delay', t: L.t0 }, { name: 'Rebuild', t: L.t1 }, { name: 'Rise', t: L.t2 }]; },

  draw(t, p, { origin, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const L = plan(p), O = (x, z) => ({ x: origin.x + x, z: origin.z + z });
    const kDelay = clamp((t - L.t0) / p.delay), kBuild = clamp((t - L.t1) / p.rebuild), kRise = clamp((t - L.t2) / p.rise);
    const timerFrac = clamp((t - L.t0) / (p.delay + p.rebuild));

    if (p.scenario === 'in place') {
      const pos = O(0, 0);
      // Blood pool under him (stays).
      draw(disc, pos.x + .05, Floor + .005, pos.z - .20, .55, .28, 0, Blood.withAlpha(.75));
      draw(disc, pos.x - .30, Floor + .006, pos.z - .28, .18, .10, 20, Blood.withAlpha(.7));
      const k = smooth(kRise);
      const { B, H } = standInBlend(pos, Shirt, k, sun, strength, { cap: Cap });
      // Wounds, closing during Rebuild.
      const healed = smooth(clamp(kBuild * 1.4));
      Wounds.forEach((w, i) => {
        const at = { x: B.x + w.x * (1 - k), z: B.z + w.z * (1 - k) };
        draw(disc, at.x, pawnLayer, at.z, .06 * (1 - healed * .9), .045 * (1 - healed * .9), 0, Blood.withAlpha(1 - healed));
        if (t > L.t0 && t < L.t2) seep(`reset wound ${i}`, at, t, Math.min(1, (t - L.t0) / .3) * (1 - kBuild * .7), p.flakes);
      });
      // Black matter covering the body: it grows over the discs during Rebuild and flakes off
      // upward during Rise, following him as he stands.
      const cover = smooth(kBuild) * (1 - smooth(clamp(kRise * 1.15)));
      if (cover > .01) {
        const grow = .6 + .4 * smooth(kBuild);
        draw(disc, B.x, pawnLayer, B.z, (B.rx + .04) * grow, (B.rz + .04) * grow, 0, GhostEdge.withAlpha(cover));
        draw(disc, B.x, pawnLayer, B.z, B.rx * grow, B.rz * grow, 0, Ghost.withAlpha(cover));
        draw(disc, H.x, pawnLayer, H.z, (H.rx + .035) * grow, (H.rz + .035) * grow, 0, GhostEdge.withAlpha(cover));
        draw(disc, H.x, pawnLayer, H.z, H.rx * grow, H.rz * grow, 0, Ghost.withAlpha(cover));
        // Bands across the body's long axis: vertical while lying, horizontal once up.
        for (let j = 0; j < 5; j++) {
          const f = (j + .5) / 5 * 2 - 1, standing = k;
          const cx = B.x + f * B.rx * .75 * (1 - standing), cz = B.z + f * B.rz * .75 * standing;
          const ax = lerp(0, 1, standing), az = 1 - ax, w = (standing ? B.rx : B.rz) * .8 * grow;
          const half = { x: ax * w, z: az * w };
          trail(`reset band ${j}`, [{ x: cx - half.x, z: cz - half.z }, { x: cx + (1 - standing) * .02, z: cz - standing * .02 }, { x: cx + half.x, z: cz + half.z }], .032, Wrap.withAlpha(.55 * cover), pawnLayer);
        }
      }
      if (t > L.t2 && kRise < 1) edgeFlakes('reset peel', { x: B.x, z: B.z - .2 }, .1 + k * .6, .6, t, { amount: 1.6 * p.flakes, rise: .8, alpha: 1 - kRise * .6 });
      if (p.timer && t > L.t0 && t < L.t2 + .3) {
        const fade = 1 - smooth(clamp((t - L.t2) / .3));
        draw(ringMesh, pos.x, Floor + .02, pos.z - .15, .72, .72, 0, TimerCol.withAlpha(.18 * fade));
        arc('reset timer', { x: pos.x, z: pos.z - .15 }, .70, timerFrac, .045, TimerCol.withAlpha(.8 * fade), Floor + .025);
      }
      return;
    }

    // At an anchor.
    const A = O(-1.7, .25), Bp = O(1.0, 0), C = O(1.9, 1.35);
    // Where the body was blown apart: scorch, blood and his gear (all stay).
    for (let i = 0; i < 7; i++) {
      const a = i * 2.39, r = .12 + rand(i + 3) * .35;
      draw(disc, A.x + Math.cos(a) * r, Floor + .004 + i * .0002, A.z + Math.sin(a) * r * .7, .25 + rand(i) * .2, .16 + rand(i + 9) * .12, rand(i + 2) * 180, Scorch.withAlpha(.55));
    }
    for (let i = 0; i < 6; i++) draw(disc, A.x + (rand(i + 20) - .5) * 1.1, Floor + .01, A.z + (rand(i + 21) - .5) * .7, .05, .035, 0, Blood.withAlpha(.8));
    draw(disc, A.x - .1, pawnLayer - .01, A.z, .20, .12, 25, Cloth);                           // shirt
    draw(disc, A.x + .22, pawnLayer - .009, A.z + .10, .09, .07, 0, Cap);                      // cap
    trail('reset rifle', [{ x: A.x - .35, z: A.z - .18 }, { x: A.x, z: A.z - .22 }, { x: A.x + .38, z: A.z - .27 }], .07, Steel, pawnLayer - .008);

    // The finger: crumbles when he rises.
    const crumble = clamp((t - L.t2) / .5);
    if (crumble < 1) {
      draw(disc, C.x, pawnLayer - .01, C.z, .045, .016, 30, Skin.withAlpha(1 - crumble));
      draw(disc, C.x - .04, pawnLayer - .009, C.z - .02, .018, .016, 0, Blood.withAlpha(1 - crumble));
    }
    if (t > L.t2 && crumble < 1) {
      for (let i = 0; i < 6; i++) {
        const u = crumble, a = rand(i + 70) * TAU;
        sprite({ x: C.x + Math.cos(a) * u * .2, z: C.z + Math.sin(a) * u * .12 + u * .1 }, .08 + u * .1, .06 + u * .08, Skin.withAlpha((1 - u) * .45), puff, Y + .01 + i * .0003);
      }
    }
    draw(disc, C.x, Floor + .008, C.z - .01, .06, .03, 0, Blood.withAlpha(.6));

    // The hand: seeping, then taken into the shell.
    const handGone = smooth(clamp(kBuild / .4));
    draw(disc, Bp.x - .05, Floor + .01, Bp.z - .04, .16, .09, 10, Blood.withAlpha(.8));
    if (handGone < 1) hand({ x: Bp.x + Math.sin(t * 40) * .006 * kDelay, z: Bp.z }, 20, pawnLayer - .01, 1 - handGone);
    if (t > L.t0 && t < L.t1 + p.rebuild * .5) seep('reset hand', Bp, t, Math.min(1, (t - L.t0) / .3) * (1 + kBuild), p.flakes);

    // Standing pawn under the shell once the shell is whole: naked (skin body, no cap).
    const full = Shell.hi - Shell.lo;
    const buildHi = Shell.lo + full * smooth(kBuild), peelHi = Shell.hi - full * smooth(kRise);
    if (t >= L.t2) standInBlend(Bp, Skin, 1, sun, strength, {});
    if (t > L.t1) {
      const hi = t < L.t2 ? buildHi : peelHi;
      if (t < L.t2) sprite({ x: Bp.x + sun.x * .5, z: Bp.z - .33 + .05 + sun.z * .5 }, .95, .42, GhostEdge.withAlpha(strength * 1.4 * smooth(kBuild)), puff, Floor + .03);
      shell('reset shell', Bp, hi, pawnLayer + .001);
      if (hi > Shell.lo + .02 && hi < Shell.hi - .01) {
        edgeFlakes(t < L.t2 ? 'reset rise' : 'reset peel', Bp, hi / Stand, .55, t, { amount: 1.2 * p.flakes, rise: t < L.t2 ? .45 : .8 });
      }
    }
    // Ooze strands pouring from the hand into the rising shell.
    if (t > L.t1 && t < L.t2) {
      const k = 1 - smooth(clamp((t - L.t1 - p.rebuild * .6) / (p.rebuild * .4)));
      for (let i = 0; i < 5; i++) {
        const top = Bp.z + buildHi - .02, x0 = Bp.x + (rand(i + 90) - .5) * .12, x1 = Bp.x + (i / 4 - .5) * .45;
        const pts = [0, .25, .5, .75, 1].map(q => ({ x: lerp(x0, x1, q) + Math.sin(q * 6 + t * 8 + i) * .02, z: lerp(Bp.z - .02, top, q) }));
        trail(`reset strand ${i}`, pts, .06, Flake.withAlpha(.85 * k), pawnLayer + .002);
      }
    }
    if (p.timer && t > L.t0 && t < L.t2 + .3) {
      const fade = 1 - smooth(clamp((t - L.t2) / .3));
      draw(ringMesh, Bp.x, Floor + .02, Bp.z - .1, .62, .62, 0, TimerCol.withAlpha(.18 * fade));
      arc('reset timer', { x: Bp.x, z: Bp.z - .1 }, .60, timerFrac, .045, TimerCol.withAlpha(.8 * fade), Floor + .025);
    }
  },
};
