// Shadow neck bind — Shadow plexus ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed, none of it agreed, numbers are placeholders). The source is Kage
// Kubishibari: shadow hands climb the held body to the throat. Target only a pawn the carrier
// already holds with Shadow imitation or has sewn with Shadow seam; it has no range of its own
// because it travels along the line that is already there. The carrier channels for up to 8 s and
// must stand still, so it is drag or choke, not both. Suffocation +12.5 % per second from the
// moment the hands close; at 100 % the target is unconscious, not dead. If the line breaks the
// hands fall off and the suffocation drains at 5 % per second. No effect on mechanoids. 45 s
// cooldown. Role: the kit's only finisher, and a capture tool.
//
// Order (times with the default sliders). The sketch starts with Imitation already holding:
//   0.00  two small hands of shadow come out of the carrier's pool and crawl along the line, one
//         each side of it, fingers working
//   0.60  they reach the pool under the target and climb the body, each on a thin arm, 1.5 s
//   1.85  at the shoulders they turn inward
//   2.10  they close on the neck. The meter starts, the target shudders and darkens as it fills
//   10.1  100 %: the target drops. The hands come apart in shreds and the line runs back
//   Cut scenario: a pawn walks over the line at 50 %. The line snaps, the hands come apart, the
//   target stays standing and the meter drains.
//
// Drawing: the hands are the Grasp hand (lib/shadow-plexus.js) at 0.36 size. On the floor they
// turn with the line. On the body they are flat shapes drawn over the pawn, pointing up the
// screen, which is up the body for every facing, so there is no per-facing method; the right hand
// is the mirrored one so both thumbs are up. In game the neck height comes from the pawn's draw
// size. The meter is a lab aid (the game shows a hediff). Pawns are lab stand-ins.
import { Color, Mathf, MeshPool } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y } from './lib/six-paths-impact.js';
import {
  lighting, pawn, downedPawn, hand, path, pointOn, shadowLine, brokenLine, pool, grip, shreds,
  CarrierColour, EnemyColour, AllyColour, Shade, RangeTint, LineLayer, SnapTime,
} from './lib/shadow-plexus.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
// The rule's numbers and decided looks.
const Drain = .05, CutShare = .5, Crawl = .6, LineBack = .5, Tail = 1.2, WalkerSpeed = 3;
const HandSize = .36, Beside = .11, NeckHeight = .41, NeckApart = .15, TurnIn = 40, ArmWidth = .06, PoolRadius = .42;
const Scenarios = ['choked unconscious', 'a pawn cuts the line at 50 %'];

function times(p) {
  const mode = Scenarios.indexOf(p.scenario), closed = Crawl + p.climb;
  const release = closed + p.choke * (mode ? CutShare : 1);
  return { mode, closed, release, end: release + (mode ? SnapTime + 1.6 : LineBack + Tail) };
}

export default {
  kit: 'Shadow plexus', label: 'Shadow neck bind (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    aim: P('Target direction (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Target distance (cells)', 6, 3, 9.5, .5, 'Showcase'),
    meter: { label: 'Show the suffocation meter (lab aid)', value: true, group: 'Showcase' },
    climb: P('Hands climb the body', 1.5, .5, 3, .05, 'Timing (s)'),
    choke: P('Closed hands to unconscious', 8, 2, 12, .5, 'Timing (s)'),
    width: P('Line width (cells)', .17, .06, .4, .01, 'Line'),
    sway: P('Line sway (cells)', .1, 0, .4, .01, 'Line'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Hands crawl along the line', t: 0 }, { name: 'Climb the body', t: Crawl }, { name: 'Closed on the neck', t: t.closed },
      { name: t.mode ? 'Line cut: hands fall off' : 'Unconscious', t: t.release }];
  },
  events() { return []; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p), { mode } = t;
    if (s < 0 || s >= t.end) return;
    const L = lighting(false, scene), a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a), half = p.distance / 2;
    const place = (along, across) => ({ x: o.x + along * ca - across * sa, z: o.z + along * sa + across * ca });
    const carrier = place(-half, 0), since = s - t.release, full = clamp((s - t.closed) / p.choke);
    const level = since < 0 ? full : mode ? Math.max(0, CutShare - Drain * since) : 1, out = mode === 0 && since >= 0;
    const target = place(half + (since < 0 ? .025 * level * Math.sin(s * 40) : 0), 0);

    // The Imitation line that is already there, and how it ends.
    if (since < 0) shadowLine('neck line', path(carrier, target, 0, 1, s, p.sway), p.width, 1, s);
    else if (mode) brokenLine('neck line', carrier, target, (1.2 + half) / p.distance, since, p.width, s, p.sway);
    else shadowLine('neck line', path(carrier, target, 0, 1 - smooth(since / LineBack), s, p.sway), p.width, 1, s);
    const held = 1 - smooth(since / .25);
    pool('neck root', carrier, (.3 + .04 * Math.sin(s * 5)) * (1 - smooth(since / LineBack)), 1, s);
    pool('neck pool', target, PoolRadius * held, 1, s);

    pawn('neck carrier', carrier, CarrierColour, L);
    if (mode) pawn('neck walker', place(1.2, Math.min(3.2, Math.max(-1.6, (t.release - s) * WalkerSpeed))), AllyColour, L);
    if (out) downedPawn('neck target', target, EnemyColour, L, p.aim + 160);
    else pawn('neck target', target, EnemyColour, L, { tint: (.55 + .35 * level) * held });
    grip('neck grip', target, held, s, 4, .35);

    // The two hands. k is -1 for the one on the left of the screen, 1 for the right.
    const gone = smooth(since / .2), lineDeg = p.aim;
    for (const k of [-1, 1]) {
      const work = .2 + .25 * (.5 + .5 * Math.sin(s * 22 + k * 1.5));              // fingers working while it crawls
      if (s < Crawl) {
        const c = pointOn(carrier, target, smooth(s / Crawl));
        hand(`neck hand ${k}`, { x: c.x - sa * k * Beside, z: c.z + ca * k * Beside }, lineDeg, smooth(s / .15), work, s, HandSize, { mirror: k > 0, layer: LineLayer + .006 });
        continue;
      }
      if (gone >= 1) continue;
      const u = smooth((s - Crawl) / p.climb), onBody = (v) => ({ x: target.x + k * lerp(.2, NeckApart, v) + Math.sin(v * 9 + k) * .03, z: target.z + .02 + v * NeckHeight });
      const arm = []; for (let i = 0; i <= 10; i++) arm.push(onBody(u * i / 10));
      shadowLine(`neck arm ${k}`, arm, ArmWidth, 1 - gone, s, { flare: false, point: false, layer: Y + .018 });
      const close = smooth((u - .85) / .15), squeeze = s >= t.closed ? .88 + .08 * Math.sin(s * 6) : lerp(work, .88, close);
      hand(`neck hand ${k}`, onBody(u), 90 + k * TurnIn * smooth((u - .75) / .25), 1 - gone, squeeze, s, HandSize, { mirror: k > 0, layer: Y + .02 });
    }
    shreds('neck hands off', { x: target.x, z: target.z + NeckHeight }, since, 10, .45, .6);

    if (p.meter && s >= t.closed) {                                   // lab aid: the hediff's severity
      const at = { x: target.x, z: target.z + (out ? .75 : 1.05) }, w = .8;
      draw(MeshPool.plane10, at.x, Y + .05, at.z, w + .04, .12, 0, Shade.withAlpha(.7));
      draw(MeshPool.plane10, at.x - w / 2 + w * level / 2, Y + .051, at.z, w * level, .08, 0, level >= 1 ? new Color(.9, .3, .25) : RangeTint);
    }
  },
};
