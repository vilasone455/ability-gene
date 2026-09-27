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
// manga. Motion is taken from the wiki's anime GIFs frame by frame (about 15 fps). S2, Todo on
// Mahito: a white bloom at contact, 2 black frames, one small spark ignites and grows over 3
// frames, it bursts into pale shards, then a red speckle hangs in the air. S1, Yuji on Hanami:
// every frame is redrawn whole, black clumps with red rims and thin red tendrils; at the end the
// tendrils stretch into thin red lines. S2 recolours the edges per shot (violet, cyan, red); red
// is the one that repeats, so the edge is red here. Afterwards the user crackles with black
// sparks ("in the zone").
//
// Order (H = the warmup's end, when the fist lands; H = 0.2 s; B = H + 0.07, the burst):
//   0.00      Todo pulls the fist back (0 to 0.6 H), then drives it in, speeding up, to the
//             target's chest. From 0.35 H small black sparks crackle on the fist.
//   H         the whole view goes negative for 0.04 s and back over 0.06 s (pawns and ground, not
//             the effect); the target rocks back 0.2 cells and settles on its cell; stun starts.
//   H - B     the area round the fist goes dark (2.8 cells) and one small spark ignites there:
//             a white core and 12 thin pale rays growing to 0.5 cells.
//   B         camera shake 0.07. Black core (0.5 -> 1 cell), red ring, red glow at the fist.
//             12 pale shards fly out to 1.6 cells in 0.16 s. 3 red streaks shoot along the punch
//             line (1.2 cells behind the fist to 2.2 past it), gone by B+0.3. 10 black sparks fly
//             off and drop. 7 black splats with red glow crowd the fist, each jumping somewhere
//             new 15 times a second, shrinking away by B+0.55.
//   B - B+.07 9 black bolts with red edges run out of the fist at 28 cells/s, a hot pale tip at
//             the front; 5 forward in a 150 deg fan up to 1.8 cells, each forking once the tip
//             has passed; 4 back and to the sides up to 0.75 cells. Lightning zigzags, a sharp
//             corner every 0.2 cells.
//   after     each bolt is redrawn whole every 0.07-0.11 s on its own clock (so they never all
//             change on one frame), turning up to 9 deg either way and 0.85-1.15 times as long, with a pale
//             red flare on the new shape and the old one left 0.035 s as an afterimage; a fine
//             crackle on top changes 15 times a second. A second wave of 4 (0.6 size) at B+0.12.
//   B+.2      the bolts stretch 25 % and thin to 35 % width, the black going before the red, so
//             they end as thin red lines; gone at B+0.45.
//   B+.12     22 red specks drift out and twinkle, gone by B+0.72.
//   H - H+1   three pale red stars circle the target's head for the 1 s stun.
//   B+.35 ... Todo is in the zone: every 0.45 s a small black spark with a red edge flicks on his
//             body for 0.08 s. In game this lasts the hediff's 10 s.
//   Ordinary punch: no crackle, no flash, no bolts; a small white puff, 5 short white impact lines,
//   the target rocks 0.08 cells, shake 0.012, no stun, no zone.
//
// Drawing: everything lies flat at chest height (0.38 cells north of the feet), so it turns with
// the aim and needs no per-facing method. Bolts are strips through jagged points; every shape comes
// from a hash of its strike number or redraw step, so the clip can be scrubbed. Splats are five
// irregular 9-corner meshes built once. The negative flash is InvertShader on MoteOverhead, below
// the effect, so the pawns and ground invert and the bolts stay black; in C# that is
// Hidden/Internal-Colored with _SrcBlend OneMinusDstColor, _DstBlend OneMinusSrcAlpha, then a grey
// Transparent quad on top. The pawns are two-disc stand-ins and the arm is a strip; there is no
// punch clip yet (clips come last).
import { AltitudeLayer, Color, InvertShader, MaterialPool, Mathf, Mesh, Meshes, MeshPool, ShaderDatabase } from '../js/engine.js';
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
const SparkPale = new Color(1, .86, .9), Shard = new Color(1, .8, .85);
const Chest = .38, Reach = 1, StunTime = 1;
const EchoAt = .12, EchoScale = .6, NegHold = .04, NegBack = .06, Grey = .2, DiscRadius = 2.2;
// Motion. A bolt's first strike runs out from the fist at LeaderSpeed cells/s; after that the
// bolt is redrawn whole every StrikeMin-StrikeMax s on its own clock (so the bolts do not all
// change on the same frame), turning up to Sway radians, with the last shape left as an
// afterimage for GhostLife s. At the end it stretches by Stretch and thins to red lines.
const LeaderSpeed = 28, StrikeMin = .07, StrikeMax = .11, GhostLife = .035, Sway = .3, Stretch = .25;
const Clumps = 7, ClumpLife = .55, ShardCount = 12, ShardLife = .16, Specks = 22, SpeckFrom = .12, SpeckLife = .6;
const ForwardShare = .6, ForwardFan = 150, BackFan = 230, BackShare = .42, Jag = .12;
const StreakBack = 1.2, StreakOn = 2.2, StreakLife = .3, SparkCount = 10, SparkLife = .42;
const ZoneStart = .35, ZoneEvery = .45, ZoneFlick = .08;
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
function boltPts(a, b, jag, seed, seg = .2, fine = null) {
  const dx = b.x - a.x, dz = b.z - a.z, len = Math.hypot(dx, dz) || 1e-6;
  const n = Math.max(3, Math.round(len / seg)), nx = -dz / len, nz = dx / len, j = jag * Math.min(1, len / .6);
  const bend = (rand(seed + 1) - .5) * 2.4 * j, first = rand(seed + 2) < .5 ? 1 : -1;
  const pts = [];
  for (let i = 0; i <= n; i++) {
    const u = (i + (i > 0 && i < n ? (rand(seed + i * 5) - .5) * .5 : 0)) / n;
    const zig = i > 0 && i < n ? (i % 2 ? first : -first) * (.35 + .65 * rand(seed + i * 7)) * j : 0;
    const off = zig + bend * Math.sin(u * Math.PI) + (fine !== null && i > 0 && i < n ? (rand(fine + i * 3) - .5) * .4 * j : 0);
    pts.push({ x: a.x + dx * u + nx * off, z: a.z + dz * u + nz * off });
  }
  return pts;
}

// One black bolt with a red edge through pts, tapering to its far end. core scales the black and
// rim the red, so a dying bolt can thin to a red line. flare 0..1 is a new strike: a wider edge
// and a pale red thread down the middle.
function bolt(key, pts, width, core, rim, layer, flare = 0) {
  if (pts.length < 2 || (core <= .01 && rim <= .01)) return;
  line(`${key} rim`, pts, width * (2.2 + .8 * flare), Red.withAlpha(Math.min(1, .8 * rim * (1 + .4 * flare))), whiteGlow, layer, 'end');
  if (core > .01) line(key, pts, width, Ink.withAlpha(Math.min(1, core)), undefined, layer + .0005, 'end');
  if (flare > .01) line(`${key} hot`, pts, width * .3, RedPale.withAlpha(flare * rim), whiteGlow, layer + .0007, 'end');
}

// A burst of count bolts out of c. age is seconds since this burst began; aim in radians.
function burst(key, c, age, s, p, aim, scale, count, seed) {
  const nF = Math.round(count * ForwardShare), nB = count - nF;
  for (let i = 0; i < count; i++) {
    const a = age - rand(seed + i * 13) * .03;
    if (a < 0 || a > p.life) continue;
    const fwd = i < nF, k = fwd ? i : i - nF, n = fwd ? nF : nB;
    const fan = (fwd ? ForwardFan : BackFan) * Mathf.Deg2Rad, jitter = (rand(seed + i * 17) - .5) * .26;
    const ang0 = aim + (fwd ? 0 : Math.PI) + ((k + .5) / n - .5) * fan + jitter;
    const len0 = p.reach * scale * (fwd ? .55 + .45 * rand(seed + i * 19) : BackShare * (.55 + .45 * rand(seed + i * 19)));
    const dur = StrikeMin + (StrikeMax - StrikeMin) * rand(seed + i * 47), strike = Math.floor(a / dur), inStrike = a - strike * dur;
    const gone = smooth((a - p.life * .45) / (p.life * .55));
    // Fine crackle on top of the strike's shape, redrawn p.boil times a second on this bolt's own clock.
    const fine = seed + i * 7 + Math.floor(s * p.boil + rand(seed + i * 61)) * 131;
    const shape = st => {
      const ang = ang0 + (st ? (rand(seed + i * 53 + st * 7) - .5) * Sway : 0);
      const len = len0 * (1 + Stretch * gone) * (st ? .85 + .3 * rand(seed + i * 59 + st * 11) : 1);
      const end = { x: c.x + Math.cos(ang) * len, z: c.z + Math.sin(ang) * len };
      return { ang, len, pts: boltPts(c, end, Jag * scale, seed + i * 101 + st * 977, .2, fine) };
    };
    const cur = shape(strike), last = cur.pts.length - 1, layer = Y + .03 + i * .001;
    const lead = strike === 0 ? clamp(a / (cur.len / LeaderSpeed)) : 1, to = Math.max(1, Math.ceil(lead * last));
    const flare = inStrike < .025 ? 1 - inStrike / .025 : 0;
    const w = p.width * scale * (1 - .65 * gone), core = 1 - gone, rim = 1 - gone * gone * gone;
    if (strike > 0 && inStrike < GhostLife) {
      const g = .4 * (1 - inStrike / GhostLife);
      bolt(`${key} ${i} ghost`, shape(strike - 1).pts, w * .8, g * core, g * rim, layer - .0004);
    }
    const pts = cur.pts.slice(0, to + 1);
    bolt(`${key} ${i}`, pts, w, core, rim, layer, flare);
    if (lead < 1) sprite(pts[pts.length - 1], .28 * scale, .28 * scale, RedPale.withAlpha(.9), glow, layer + .0008);
    // Forward bolts fork once, 35-65 % along, 25-45 deg off, a third of the bolt long; the fork
    // grows once the leader has passed its root.
    const at = Math.round(last * (.35 + .3 * rand(seed + i * 23)));
    if (!fwd || at >= to) continue;
    const side = rand(seed + i * 29 + strike) < .5 ? -1 : 1, fAng = cur.ang + side * (.45 + .35 * rand(seed + i * 31 + strike));
    const fLen = cur.len * (.25 + .2 * rand(seed + i * 37 + strike));
    const root = cur.pts[at], tip = { x: root.x + Math.cos(fAng) * fLen, z: root.z + Math.sin(fAng) * fLen };
    const fork = boltPts(root, tip, Jag * scale * .6, seed + i * 211 + strike * 577, .14, fine + 17);
    const forkLead = strike === 0 ? clamp((lead * last - at) / Math.max(1, last - at) * 1.6) : 1;
    bolt(`${key} ${i} fork`, fork.slice(0, Math.max(2, Math.ceil(forkLead * fork.length))), w * .55, core, rim, layer + .0002, flare);
  }
}

// Ink splats: irregular outlines of 9 corners, a few of them spikes, built once.
const BlobShapes = [0, 1, 2, 3, 4].map(v => {
  const m = new Mesh(`black flash blob ${v}`), vertices = [0, 0], tri = [], n = 9;
  for (let i = 0; i < n; i++) {
    const ang = (i + (rand(v * 10 + i + 500) - .5) * .5) / n * Math.PI * 2;
    const r = rand(v * 20 + i + 600) > .72 ? .75 + .2 * rand(v * 30 + i + 700) : .32 + .18 * rand(v * 40 + i + 800);
    vertices.push(Math.cos(ang) * r, Math.sin(ang) * r);
    tri.push(0, 1 + i, 1 + (i + 1) % n);
  }
  m.setFlat(vertices, tri);
  return m;
});

// S1's black clumps: splats with a red glow near the fist, each redrawn somewhere new p.boil times
// a second on its own clock, shrinking over ClumpLife.
function clumps(c, age, s, p, aim) {
  if (age < 0 || age >= ClumpLife) return;
  const left = 1 - smooth(age / ClumpLife), grow = smooth(age / .06);
  for (let j = 0; j < Clumps; j++) {
    const r = Math.floor(s * p.boil + rand(j + 900)) * 37 + j * 11 + 950;
    const ang = aim + (rand(r) - .5) * (j < 5 ? 2.2 : 5.5), d = (.12 + .6 * rand(r + 1)) * p.reach * .55 * (.5 + .5 * grow);
    const at = { x: c.x + Math.cos(ang) * d, z: c.z + Math.sin(ang) * d }, size = (.14 + .14 * rand(r + 2)) * left;
    sprite(at, size * 2.6, size * 2.6, Red.withAlpha(.5 * left), glow, Y + .027);
    draw(BlobShapes[Math.floor(rand(r + 3) * BlobShapes.length)], at.x, Y + .028, at.z, size, size, rand(r + 4) * 360, Ink.withAlpha(.95));
  }
}

// Small bolts round a point: the fist before the hit, Todo's body in the zone.
function crackle(key, c, s, p, count, len, width, alpha, seed) {
  const step = Math.floor(s * p.boil);
  for (let i = 0; i < count; i++) {
    const r = seed + i * 43 + step * 311, ang = rand(r) * Math.PI * 2, l = len * (.6 + .4 * rand(r + 1));
    const pts = boltPts(c, { x: c.x + Math.cos(ang) * l, z: c.z + Math.sin(ang) * l }, .06, r + 2, .06);
    bolt(`${key} ${i}`, pts, width, alpha, alpha, Y + .05 + i * .001);
  }
}

export default {
  kit: 'Anchor',
  label: 'Black Flash (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    aim: P('Punch direction (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    warmup: P('Warmup, fist lands at', .2, .1, .5, .01, 'Timing (s)'),
    spark: P('Dark + spark before the burst', .07, 0, .2, .01, 'Timing (s)'),
    life: P('Bolts last', .45, .2, 1, .05, 'Timing (s)'),
    count: P('Bolts', 9, 5, 16, 1, 'Bolts'),
    reach: P('Forward reach (cells)', 1.8, .8, 3, .1, 'Bolts'),
    width: P('Width at the fist (cells)', .12, .05, .25, .01, 'Bolts'),
    boil: P('Crackle redraws per second', 15, 6, 30, 1, 'Bolts'),
    flash: { label: 'Negative flash', value: Flashes[0], options: Flashes, group: 'Flash' },
    streaks: { label: 'Red streaks along the punch', value: true, group: 'Flash' },
  },

  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    const b = t.hit + p.spark;
    return [{ name: 'Wind-up', t: 0 }, { name: 'Hit: dark + spark', t: t.hit }, { name: 'Burst', t: b },
      { name: 'Bolts thin out', t: b + p.life * .45 }, { name: 'In the zone', t: b + p.life }, { name: 'Stun ends', t: t.hit + StunTime }];
  },
  events(p) {
    const t = times(p), flash = p.scenario === Scenarios[0];
    return [{ t: t.hit + (flash ? p.spark : 0), type: 'shake', value: flash ? .07 : .012 }, { t: t.hit, type: 'sound', def: flash ? 'AG_BlackFlash' : 'Pawn_Melee_Punch_HitPawn' }];
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

    // The spark (S2): the fist's surroundings go dark and one small spark ignites and grows, then
    // the burst. b is the burst's age.
    const b = age - p.spark;
    if (age < p.spark + .08) {
      const dark = Math.min(1, age / .02) * (1 - smooth((age - p.spark) / .08));
      sprite(contact, 2.8, 2.8, Ink.withAlpha(.75 * dark), soft, Y + .012);
      const u = p.spark > 0 ? clamp(age / p.spark) : 1, fade = 1 - smooth((age - p.spark) / .06), step = Math.floor(s * p.boil);
      sprite(contact, .14 + .34 * u, .14 + .34 * u, SparkPale.withAlpha(.9 * fade), glow, Y + .014);
      draw(flatDisc, contact.x, Y + .015, contact.z, .03 + .05 * u, .03 + .05 * u, 0, White.withAlpha(fade), whiteGlow);
      for (let i = 0; i < 12; i++) {
        const ang = (i + rand(i + step * 13 + 300) * .6) / 12 * Math.PI * 2 + age * 2, l = (.08 + .45 * u) * (.5 + .5 * rand(i + step * 17 + 320));
        const from = { x: contact.x + Math.cos(ang) * .03, z: contact.z + Math.sin(ang) * .03 };
        line(`bf spark ray ${i}`, [from, { x: contact.x + Math.cos(ang) * l, z: contact.z + Math.sin(ang) * l }], .02, SparkPale.withAlpha(fade), whiteGlow, Y + .016, 'end');
      }
    }
    if (b < 0) return;

    // Red glow, black core, red ring at the fist.
    const f = 1 - smooth(b / .35), core = 1 - smooth(b / .22), ring = smooth(b / .25);
    sprite(contact, 2.4, 2.4, Red.withAlpha(.5 * f), glow, Y + .02);
    const coreSize = .5 + .5 * smooth(b / .15);
    sprite(contact, coreSize, coreSize, Ink.withAlpha(.9 * core), soft, Y + .022);
    ringAt(contact, .3 + .7 * ring, Red.withAlpha(.75 * (1 - ring)), Y + .024, false, whiteGlow);

    // Pale shards (S2) thrown out by the burst.
    if (b < ShardLife) {
      const u = b / ShardLife, out = 1 - (1 - u) * (1 - u);
      for (let i = 0; i < ShardCount; i++) {
        const ang = aim + (rand(i + 330) - .5) * (i < 8 ? 2.6 : 6.3), r = .1 + (.8 + .8 * rand(i + 331)) * out, l = .3 * (1 - u) + .05;
        streak(`bf shard ${i}`, { x: contact.x + Math.cos(ang) * r, z: contact.z + Math.sin(ang) * r },
          { x: contact.x + Math.cos(ang) * (r + l), z: contact.z + Math.sin(ang) * (r + l) }, .07 * (1 - .5 * u), Shard.withAlpha(1 - u), whiteGlow, Y + .04, 3);
      }
    }

    // Red streaks along the punch line.
    if (p.streaks && b < StreakLife) {
      [-.22, .04, .26].forEach((across, i) => {
        const bx = contact.x - sa * across, bz = contact.z + ca * across;
        const start = { x: bx - ca * (StreakBack + rand(i + 60) * .3), z: bz - sa * (StreakBack + rand(i + 60) * .3) };
        const end = { x: bx + ca * (StreakOn * (.75 + .25 * rand(i + 61))), z: bz + sa * (StreakOn * (.75 + .25 * rand(i + 61))) };
        const head = lerpP(start, end, smooth(b / .08)), tail = lerpP(start, end, smooth((b - .06) / .24));
        const w = .07 + .05 * rand(i + 62), a = 1 - smooth(b / StreakLife);
        streak(`bf streak ${i}`, tail, head, w, Red.withAlpha(.85 * a), whiteGlow, Y + .026, 6);
        streak(`bf streak core ${i}`, tail, head, w * .3, Ink.withAlpha(.8 * a), undefined, Y + .0265, 6);
      });
    }

    // S1's black clumps, then the bolts: the main burst and a smaller second wave.
    clumps(contact, b, s, p, aim);
    burst('bf burst', contact, b, s, p, aim, 1, p.count, 1000);
    burst('bf echo', contact, b - EchoAt, s, p, aim + .3, EchoScale, Math.max(3, Math.round(p.count * .45)), 3000);

    // Black sparks fly off the target, mostly forward, and drop.
    for (let i = 0; i < SparkCount; i++) {
      const u = b / (SparkLife * (.6 + .4 * rand(i + 70))); if (u > 1) continue;
      const ang = aim + (rand(i + 71) - .5) * 3.2, d = (.35 + .9 * rand(i + 72)) * (1 - (1 - u) * (1 - u)), drop = 1.1 * (u * SparkLife) ** 2;
      const tip = { x: contact.x + Math.cos(ang) * d, z: contact.z + Math.sin(ang) * d - drop };
      const tail = { x: tip.x - Math.cos(ang) * .14, z: tip.z - Math.sin(ang) * .14 + .03 };
      bolt(`bf spark ${i}`, [tail, tip], .035, 1 - u, 1 - u, Y + .045);
    }

    // Red specks (S2) drift out and twinkle after the burst.
    const sk = b - SpeckFrom;
    if (sk >= 0 && sk < SpeckLife) {
      const u = sk / SpeckLife;
      for (let i = 0; i < Specks; i++) {
        const ang = aim + (rand(i + 340) - .5) * 5, d = (.2 + 1.3 * rand(i + 341)) * (1 + .3 * u), twinkle = Math.max(0, Math.sin(s * 37 + i * 2.1));
        const size = .035 + .03 * rand(i + 342), c = i % 3 ? Red : RedPale;
        draw(flatDisc, contact.x + Math.cos(ang) * d, Y + .05, contact.z + Math.sin(ang) * d + .1 * u, size, size, 0, c.withAlpha((1 - u) * (.35 + .65 * twinkle)), whiteGlow);
      }
    }

    // The target's 1 s stun.
    if (age < StunTime) stunStars('bf stun', foe, s, 1 - smooth((age - (StunTime - .15)) / .15), RedPale);

    // Todo in the zone: a spark on his body every ZoneEvery, from ZoneStart after the burst.
    if (b >= ZoneStart) {
      const k = Math.floor((b - ZoneStart) / ZoneEvery), inPeriod = b - ZoneStart - k * ZoneEvery, start = rand(k * 3 + 400) * (ZoneEvery - ZoneFlick);
      if (inPeriod >= start && inPeriod < start + ZoneFlick) {
        const spot = { x: todo.x + (rand(k * 3 + 401) - .5) * .4, z: todo.z + .12 + rand(k * 3 + 402) * .55 };
        crackle(`bf zone ${k}`, spot, s, p, 3, .35, .045, 1 - (inPeriod - start) / ZoneFlick, 700 + k * 13);
      }
    }
  },
};
