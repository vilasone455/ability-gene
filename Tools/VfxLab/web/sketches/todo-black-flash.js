// Black Flash — picture for Todo's Black Flash (AG_AnchorBlackFlash), not the game yet. The game
// plays a placeholder: vanilla ExplosionFlash, 4 micro sparks, the punch sound and red text
// (CompAbilityEffect_BlackFlash.cs, branch feature/todo-black-flash, 0cb9387).
//
// Mechanic (agreed 2026-09-27, built on the branch; the numbers are XML): a walk-up bare-hand
// punch, warmup 0.2 s, cooldown 6 s, 9 blunt x the melee damage factor. Within 3 s (180 ticks) of
// a Clap or Double Clap that moved someone it is a Black Flash: x2.5 damage, the target is stunned
// 1 s (60 ticks) and Todo is "in the zone" for 10 s (+15 % melee hit chance). One Black Flash per
// clap. Outside the window it is an ordinary punch, and the "ordinary punch" scenario shows that.
//
// Source: the anime (S1 ep 19 Yuji on Hanami, S2 ep 20-21 Yuji and Todo on Mahito) and the
// manga. The fist lands, the frame goes negative for a frame or two, black jagged bolts with red
// edges burst out of the contact point, red streaks run along the punch line and the target is
// rocked. S2 recolours the bolt edges per shot (violet, cyan, red); red is the one that repeats,
// and the manga's bolts are plain black, so the edge is red here. Afterwards the user crackles
// with black sparks ("in the zone").
//
// Order (H = the warmup's end, when the fist lands; H = 0.2 s):
//   0.00      Todo pulls the fist back (0 to 0.6 H), then drives it in, speeding up, to the
//             target's chest. From 0.35 H small black sparks crackle on the fist.
//   H         Black Flash: the whole view goes negative for 0.04 s and back over 0.06 s (the pawns
//             and the ground, not the bolts); camera shake 0.07. A black core (0.5 -> 1 cell) with
//             a red ring and a red glow at the fist.
//   H - H+.05 9 black bolts with red edges grow out of the fist, lightning zigzags with sharp
//             corners every 0.2 cells: 5 forward in a 150 deg fan, up to 1.8 cells, one fork each;
//             4 backward and to the sides, up to 0.75 cells. The zigzag is redrawn 20 times a
//             second. A second, smaller burst of 4 (0.6 size) at H+0.1.
//   H         3 red streaks shoot along the punch line, from 1.2 cells behind the fist to 2.2
//             past it, and fade by H+0.3. 10 black sparks fly off the target and drop.
//   H+.2      the bolts stop changing shape and fade, the red edge first; gone at H+0.45.
//   H - H+1   the target rocks back 0.2 cells and settles (it stays on its cell); three pale red
//             stars circle its head for the 1 s stun.
//   H+.25 ... Todo is in the zone: every 0.45 s a small black spark with a red edge flicks on his
//             body for 0.08 s. In game this lasts the hediff's 10 s.
//   Ordinary punch: no crackle, no flash, no bolts; a small white puff, 5 short white impact lines,
//   the target rocks 0.08 cells, shake 0.012, no stun, no zone.
//
// Drawing: the bolts, streaks, rings and sparks lie flat at chest height (0.38 cells north of the
// feet), so they turn with the aim and need no per-facing method. Bolts are strips through jagged
// points, rebuilt from a hash of the redraw step (same approach as Raiko Kusari). The negative
// flash is InvertShader on MoteOverhead, below the bolts, so the pawns and ground invert and the
// bolts stay black; in C# that is Hidden/Internal-Colored with _SrcBlend OneMinusDstColor,
// _DstBlend OneMinusSrcAlpha, then a grey Transparent quad on top. The pawns are two-disc
// stand-ins and the arm is a strip; there is no punch clip yet (clips come last).
import { AltitudeLayer, Color, InvertShader, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import { pawn, line, streak, stunStars, ringAt, whiteGlow, Skin, EnemyColour } from './lib/goku.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const invertMat = MaterialPool.MatFrom('white', InvertShader);
const greyMat = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const flatDisc = Meshes.disc(48, 'black flash disc');

// Decided looks.
const Ink = new Color(.02, .015, .02), Red = new Color(.95, .08, .1), RedDeep = new Color(.5, .02, .05);
const RedPale = new Color(1, .6, .55), White = new Color(1, 1, 1), TodoColour = new Color(.26, .28, .34);
const Chest = .38, Reach = 1, StunTime = 1;
const Grow = .05, EchoAt = .1, EchoScale = .6, NegHold = .04, NegBack = .06, Grey = .2, DiscRadius = 2.2;
const ForwardShare = .6, ForwardFan = 150, BackFan = 230, BackShare = .42, Jag = .12;
const StreakBack = 1.2, StreakOn = 2.2, StreakLife = .3, SparkCount = 10, SparkLife = .42;
const ZoneStart = .25, ZoneEvery = .45, ZoneFlick = .08;
const Scenarios = ['black flash (clap under 3 s ago)', 'ordinary punch (window shut)'];
const Flashes = ['full screen', 'disc', 'off'];

const lerpP = (a, b, u) => ({ x: lerp(a.x, b.x, u), z: lerp(a.z, b.z, u) });

function times(p) {
  const hit = p.warmup;
  return { hit, pull: hit * .6, crackle: hit * .35, end: hit + 2.4 };
}

// A lightning bolt from a to b as the manga draws it: straight runs that turn sharply, each corner
// thrown to the other side of the line (a zigzag), on top of one slow bend. Zero at both ends;
// seed picks the shape.
function boltPts(a, b, jag, seed, seg = .2) {
  const dx = b.x - a.x, dz = b.z - a.z, len = Math.hypot(dx, dz) || 1e-6;
  const n = Math.max(3, Math.round(len / seg)), nx = -dz / len, nz = dx / len, j = jag * Math.min(1, len / .6);
  const bend = (rand(seed + 1) - .5) * 2.4 * j, first = rand(seed + 2) < .5 ? 1 : -1;
  const pts = [];
  for (let i = 0; i <= n; i++) {
    const u = (i + (i > 0 && i < n ? (rand(seed + i * 5) - .5) * .5 : 0)) / n;
    const zig = i > 0 && i < n ? (i % 2 ? first : -first) * (.35 + .65 * rand(seed + i * 7)) * j : 0;
    const off = zig + bend * Math.sin(u * Math.PI);
    pts.push({ x: a.x + dx * u + nx * off, z: a.z + dz * u + nz * off });
  }
  return pts;
}

// One black bolt with a red edge through pts, tapering to its far end. rim 0..1 scales the edge,
// which goes before the black does.
function bolt(key, pts, width, alpha, layer, rim = 1) {
  if (alpha <= .01 || pts.length < 2) return;
  line(`${key} rim`, pts, width * 2.2, Red.withAlpha(.8 * alpha * rim), whiteGlow, layer, 'end');
  line(key, pts, width, Ink.withAlpha(Math.min(1, alpha)), undefined, layer + .0005, 'end');
}

// A burst of count bolts out of c. age is seconds since this burst began; aim in radians.
function burst(key, c, age, s, p, aim, scale, count, seed) {
  const nF = Math.round(count * ForwardShare), nB = count - nF, step = Math.floor(s * p.boil);
  for (let i = 0; i < count; i++) {
    const a = age - rand(seed + i * 13) * .03;
    if (a < 0 || a > p.life) continue;
    const fwd = i < nF, k = fwd ? i : i - nF, n = fwd ? nF : nB;
    const fan = (fwd ? ForwardFan : BackFan) * Mathf.Deg2Rad, jitter = (rand(seed + i * 17) - .5) * .26;
    const ang = aim + (fwd ? 0 : Math.PI) + ((k + .5) / n - .5) * fan + jitter;
    const len = p.reach * scale * (fwd ? .55 + .45 * rand(seed + i * 19) : BackShare * (.55 + .45 * rand(seed + i * 19)));
    const end = { x: c.x + Math.cos(ang) * len, z: c.z + Math.sin(ang) * len };
    // Once breaking up, the shape stops changing: the edge goes first, then the black.
    const gone = smooth((a - p.life * .45) / (p.life * .55)), frozen = a > p.life * .45 ? Math.floor((s - a + p.life * .45) * p.boil) : step;
    const pts = boltPts(c, end, Jag * scale, seed + i * 101 + frozen * 977);
    const last = pts.length - 1, grown = smooth(a / Grow), to = Math.max(1, Math.ceil(grown * last));
    const alpha = 1 - gone * gone, w = p.width * scale;
    bolt(`${key} ${i}`, pts.slice(0, to + 1), w, alpha, Y + .03 + i * .001, 1 - gone);
    // Forward bolts fork once, 35-65 % along, 25-45 deg off, a third of the bolt long.
    const at = Math.round(last * (.35 + .3 * rand(seed + i * 23)));
    if (!fwd || at >= to) continue;
    const side = rand(seed + i * 29) < .5 ? -1 : 1, fAng = ang + side * (.45 + .35 * rand(seed + i * 31)), fLen = len * (.25 + .2 * rand(seed + i * 37));
    const root = pts[at], tip = { x: root.x + Math.cos(fAng) * fLen, z: root.z + Math.sin(fAng) * fLen };
    const fork = boltPts(root, tip, Jag * scale * .6, seed + i * 211 + frozen * 577, .14);
    bolt(`${key} ${i} fork`, fork.slice(0, Math.max(2, Math.ceil(grown * fork.length))), w * .55, alpha, Y + .03 + i * .001 + .0002, 1 - gone);
  }
}

// Small bolts round a point: the fist before the hit, Todo's body in the zone.
function crackle(key, c, s, p, count, len, width, alpha, seed) {
  const step = Math.floor(s * p.boil);
  for (let i = 0; i < count; i++) {
    const r = seed + i * 43 + step * 311, ang = rand(r) * Math.PI * 2, l = len * (.6 + .4 * rand(r + 1));
    const pts = boltPts(c, { x: c.x + Math.cos(ang) * l, z: c.z + Math.sin(ang) * l }, .06, r + 2, .06);
    bolt(`${key} ${i}`, pts, width, alpha, Y + .05 + i * .001);
  }
}

export default {
  kit: 'Anchor',
  label: 'Black Flash (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    aim: P('Punch direction (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    warmup: P('Warmup, fist lands at', .2, .1, .5, .01, 'Timing (s)'),
    life: P('Bolts last', .45, .2, 1, .05, 'Timing (s)'),
    count: P('Bolts', 9, 5, 16, 1, 'Bolts'),
    reach: P('Forward reach (cells)', 1.8, .8, 3, .1, 'Bolts'),
    width: P('Width at the fist (cells)', .12, .05, .25, .01, 'Bolts'),
    boil: P('Zigzag redraws per second', 20, 6, 40, 1, 'Bolts'),
    flash: { label: 'Negative flash', value: Flashes[0], options: Flashes, group: 'Flash' },
    streaks: { label: 'Red streaks along the punch', value: true, group: 'Flash' },
  },

  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Wind-up', t: 0 }, { name: 'Hit', t: t.hit }, { name: 'Bolts break up', t: t.hit + p.life * .45 },
      { name: 'In the zone', t: t.hit + p.life }, { name: 'Stun ends', t: t.hit + StunTime }];
  },
  events(p) {
    const t = times(p), flash = p.scenario === Scenarios[0];
    return [{ t: t.hit, type: 'shake', value: flash ? .07 : .012 }, { t: t.hit, type: 'sound', def: flash ? 'AG_BlackFlash' : 'Pawn_Melee_Punch_HitPawn' }];
  },

  draw(s, p, { origin, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const flash = p.scenario === Scenarios[0], t = times(p), age = s - t.hit;
    const aim = p.aim * Mathf.Deg2Rad, ca = Math.cos(aim), sa = Math.sin(aim);

    // Todo steps 0.12 cells into the punch; the target rocks back and settles on its cell.
    const lean = s < t.pull ? -.04 * smooth(s / t.pull) : age < 0 ? lerp(-.04, .12, clamp((s - t.pull) / (t.hit - t.pull))) : .12 * (1 - smooth((age - .1) / .4));
    const rockBack = age < 0 ? 0 : (flash ? .2 : .08) * clamp(age / .05) * (1 - smooth((age - .05) / .3));
    const todo = { x: origin.x + ca * lean, z: origin.z + sa * lean };
    const foe = { x: origin.x + ca * (Reach + rockBack), z: origin.z + sa * (Reach + rockBack) };
    const contact = { x: foe.x - ca * .2, z: foe.z - sa * .2 + Chest };

    // The fist: back, then in, speeding up, then home.
    const shoulder = { x: todo.x + ca * .08, z: todo.z + .4 + sa * .05 };
    const rest = { x: shoulder.x + ca * .18, z: shoulder.z + sa * .18 }, back = { x: shoulder.x - ca * .1, z: shoulder.z - sa * .1 };
    const fist = s < t.pull ? lerpP(rest, back, smooth(s / t.pull))
      : age < 0 ? lerpP(back, contact, ((s - t.pull) / (t.hit - t.pull)) ** 2)
      : lerpP(contact, rest, smooth((age - .08) / .25));

    // Pawns, north one first. The target darkens red for 0.25 s when hit.
    const hitTint = age < 0 ? 0 : (flash ? .6 : .25) * (1 - smooth(age / .25));
    const drawTodo = () => {
      pawn(todo, TodoColour, sun, strength, { hair: true });
      line('bf arm', [shoulder, fist], .075, Skin, undefined, pawnLayer + .01, 'none');
      draw(flatDisc, fist.x, pawnLayer + .012, fist.z, .075, .075, 0, Skin);
    };
    const drawFoe = () => pawn(foe, EnemyColour, sun, strength, { tint: flash ? RedDeep : White, tintAmount: hitTint });
    if (foe.z > todo.z) { drawFoe(); drawTodo(); } else { drawTodo(); drawFoe(); }

    if (!flash) {
      if (age < 0 || age > .2) return;
      const u = age / .2, f = (1 - u) * (1 - u);
      sprite(contact, .45 + .3 * u, .45 + .3 * u, White.withAlpha(.75 * f), glow, Y + .02);
      for (let i = 0; i < 5; i++) {
        const ang = aim + (i / 4 - .5) * 1.6 + (rand(i + 3) - .5) * .2, near = .12 + u * .25, far = near + .22;
        streak(`bf plain ${i}`, { x: contact.x + Math.cos(ang) * near, z: contact.z + Math.sin(ang) * near },
          { x: contact.x + Math.cos(ang) * far, z: contact.z + Math.sin(ang) * far }, .04, White.withAlpha(f), whiteGlow, Y + .03, 3);
      }
      return;
    }

    // Before the hit: black sparks crackle on the fist.
    if (s >= t.crackle && age < 0) crackle('bf fist', fist, s, p, 4, .3, .045, clamp((s - t.crackle) / .04), 500);
    if (age < 0) return;

    // Negative flash: pawns and ground, under the bolts.
    if (p.flash !== Flashes[2] && age < NegHold + NegBack) {
      const a = age < NegHold ? 1 : 1 - smooth((age - NegHold) / NegBack);
      const cover = (mat, colour, lift) => {
        if (p.flash === Flashes[0]) draw(MeshPool.plane10, origin.x, Y + lift, origin.z, 400, 400, 0, colour, mat);
        else draw(flatDisc, contact.x, Y + lift, contact.z, DiscRadius, DiscRadius, 0, colour, mat);
      };
      cover(invertMat, new Color(a, a, a, a), 0);
      cover(greyMat, new Color(.5, .5, .5, Grey * a), .001);
    }

    // Red glow, black core, red ring at the fist.
    const f = 1 - smooth(age / .35), core = 1 - smooth(age / .22), ring = smooth(age / .25);
    sprite(contact, 2.4, 2.4, Red.withAlpha(.5 * f), glow, Y + .02);
    const coreSize = .5 + .5 * smooth(age / .15);
    sprite(contact, coreSize, coreSize, Ink.withAlpha(.9 * core), soft, Y + .022);
    ringAt(contact, .3 + .7 * ring, Red.withAlpha(.75 * (1 - ring)), Y + .024, false, whiteGlow);

    // Red streaks along the punch line.
    if (p.streaks && age < StreakLife) {
      [-.22, .04, .26].forEach((across, i) => {
        const bx = contact.x - sa * across, bz = contact.z + ca * across;
        const start = { x: bx - ca * (StreakBack + rand(i + 60) * .3), z: bz - sa * (StreakBack + rand(i + 60) * .3) };
        const end = { x: bx + ca * (StreakOn * (.75 + .25 * rand(i + 61))), z: bz + sa * (StreakOn * (.75 + .25 * rand(i + 61))) };
        const head = lerpP(start, end, smooth(age / .08)), tail = lerpP(start, end, smooth((age - .06) / .24));
        const w = .07 + .05 * rand(i + 62), a = 1 - smooth(age / StreakLife);
        streak(`bf streak ${i}`, tail, head, w, Red.withAlpha(.85 * a), whiteGlow, Y + .026, 6);
        streak(`bf streak core ${i}`, tail, head, w * .3, Ink.withAlpha(.8 * a), undefined, Y + .0265, 6);
      });
    }

    // The bolts: the main burst at the hit, a smaller one 0.1 s later.
    burst('bf burst', contact, age, s, p, aim, 1, p.count, 1000);
    burst('bf echo', contact, age - EchoAt, s, p, aim + .3, EchoScale, Math.max(3, Math.round(p.count * .45)), 3000);

    // Black sparks fly off the target, mostly forward, and drop.
    for (let i = 0; i < SparkCount; i++) {
      const u = age / (SparkLife * (.6 + .4 * rand(i + 70))); if (u > 1) continue;
      const ang = aim + (rand(i + 71) - .5) * 3.2, d = (.35 + .9 * rand(i + 72)) * (1 - (1 - u) * (1 - u)), drop = 1.1 * (u * SparkLife) ** 2;
      const tip = { x: contact.x + Math.cos(ang) * d, z: contact.z + Math.sin(ang) * d - drop };
      const tail = { x: tip.x - Math.cos(ang) * .14, z: tip.z - Math.sin(ang) * .14 + .03 };
      bolt(`bf spark ${i}`, [tail, tip], .035, 1 - u, Y + .045);
    }

    // The target's 1 s stun.
    if (age < StunTime) stunStars('bf stun', foe, s, 1 - smooth((age - (StunTime - .15)) / .15), RedPale);

    // Todo in the zone: a spark on his body every ZoneEvery.
    if (age >= ZoneStart) {
      const k = Math.floor((age - ZoneStart) / ZoneEvery), inPeriod = age - ZoneStart - k * ZoneEvery, start = rand(k * 3 + 400) * (ZoneEvery - ZoneFlick);
      if (inPeriod >= start && inPeriod < start + ZoneFlick) {
        const spot = { x: todo.x + (rand(k * 3 + 401) - .5) * .4, z: todo.z + .12 + rand(k * 3 + 402) * .55 };
        crackle(`bf zone ${k}`, spot, s, p, 3, .35, .045, 1 - (inPeriod - start) / ZoneFlick, 700 + k * 13);
      }
    }
  },
};
