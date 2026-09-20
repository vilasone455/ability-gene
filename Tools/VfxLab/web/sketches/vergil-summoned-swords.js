// Summoned Swords — ability proposal for the Vergil kit (a held katana), not the game. Nothing in
// Source/RimArt draws this yet. The kit's ranged support: blades that shoot on their own while the
// carrier keeps fighting with the katana. The source's Spiral Swords ring, fired one blade at a time
// the way its Blistering Swords are.
//
// What it is for (proposed, none of it agreed; every number is a placeholder and will be an XML field).
//   Self cast, warm-up 0.5 s, lasts 20 s, cooldown 45 s. 8 blades of blue light stand in a level ring
//   0.95 cells round the carrier at chest height and circle at 160 degrees per second, points 20 degrees out from the
//   direction of travel, like saw teeth.
//   Every 0.6 s one blade fires by itself at the nearest hostile within 12 cells with line of sight
//   that holds fewer than 4 blades: 9 Stab, 30 % armour penetration. It is not the pawn's attack: no
//   stance, no warm-up, the pawn keeps walking or meleeing. Allies are never picked.
//   The blade stays in the target for 3 s. Each blade in a pawn is -15 % move speed, up to 4 (-60 %).
//   A fired slot is empty for 2.4 s, then a new blade grows in it, so about 4 blades circle at a time.
//   When it ends every blade breaks, the ones stuck in pawns too.
//   It differs from Needle Halo (Six Paths): that is a weapon form, the pawn's own attack in bursts of
//   3 on the player's order, with a Seal active, drawn as needles on a hub above the head. This is a
//   timed buff on a melee pawn, fires without orders, and is drawn as swords round the body.
//
// Order, with the default timings (the sketch shows 6 s of the 20):
//   0.00  the carrier stands. 3 raiders within 12 cells, an ally 2.9 cells away, 1 raider 14.8 cells
//         east. A thin floor ring marks the 12 cell range and moves with the carrier
//   0.30  warm-up 0.5 s: a floor ring grows under the carrier to the blade ring's radius; the 8 blades
//         rise out of it one after another, each from a glint on the floor up to chest height in 0.22 s
//   0.80  the ring circles. "walks east": the carrier walks at 0.8 cells/s and the ring goes with them
//   1.00  first shot, then one every 0.6 s. The blade nearest the target's side leaves its slot, turns
//         point-first in 0.14 s, flies at 45 cells/s with a light trail, and stops in the target's
//         chest: a spark, the pawn flashes white and flinches, the blade stays sticking out toward
//         where it came from, the pawn turns a little blue per blade and keeps a red slit per hit.
//         After 3 s the stuck blade breaks into 7 shards. The empty slot regrows after 2.4 s, from the
//         guard to the tip in 0.22 s. A target holding 4 blades is skipped: with the defaults the
//         10 shots go 1 to the north-west raider, 7 to the east one, 2 to the south one (shots 6 and 10,
//         while the east one holds 4). The far raider takes none: outside 12 cells at first, and never
//         the nearest after the walk brings it inside
//   6.80  it ends: every blade in the ring and every stuck blade breaks into shards, one thin ring
//         front, the floor rings fade over 0.4 s
//   7.80  what stays: the red slits, one per hit; none on the ally or the far raider
//
// Drawing: the ring is a level circle and each blade is a flat shape lying at one height, so nothing
// needs a per-facing method; the ring only shifts north by its height. Blades on the north half draw
// under the pawn layer, the south half over it. A blade is light: a dark blue under-line so it reads
// on pale ground, a soft additive halo (the SoftDisc texture stretched along it), a blue-white body and a white
// core with parallel edges and a point over the last 30 %, plus a cross guard and a hilt. The walk is a slide; pawns are stand-ins.
import { Color, Mathf, MeshPool } from '../js/engine.js';
import { draw, Lift } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import {
  Blue, Deep, Ice, White, Ink, EnemyColour, Ally, pawn, pawnLayer, shadowLayer, carrier, ringAt, strip, glint, aura, streak, whiteGlow, smooth, clamp,
} from './lib/vergil.js';

// Decided values. The panel keeps only what is still being tuned.
const Lead = .3, Tail = 1, Rise = .22, Grow = .22, Turn = .14, Speed = 45, Break = .35, RingFade = .4, FirstShot = .2;
const Height = .5, Chest = .3, BladeLength = .62, Tilt = 20, BladeWide = .12, StuckOut = .5, Reach = .15, MaxPins = 4, Shards = 7, WalkSpeed = .8;
const Wound = new Color(.55, .05, .05);
// Pawns as [east, north] of the clicked cell, in cells. The last hostile starts outside the range.
const Hostiles = [[6, 2.5], [-4.5, 4], [3.5, -5.5], [14.5, -3]], AllyAt = [2.5, 1.5];

function times(p) {
  const cast = Lead, formed = cast + p.form, fire = formed + FirstShot, stop = formed + p.lasts;
  return { cast, formed, fire, stop, end: stop + Tail };
}
const turnTo = (a, b, u) => a + ((((b - a) % 360) + 540) % 360 - 180) * u;
const casterAt = (p, t, o, s) => ({ x: o.x + (p.scenario === 'walks east' ? WalkSpeed * Math.max(0, Math.min(s, t.stop) - t.formed) : 0), z: o.z });
const slotAngle = (i, n, p, s) => i / n * 360 + p.spin * s;
const ringPoint = (c, deg, radius, height) => ({ x: c.x + Math.cos(deg * Mathf.Deg2Rad) * radius, z: c.z + height * Lift + Math.sin(deg * Mathf.Deg2Rad) * radius });

// Every shot of the clip, worked out from zero each frame so the timeline can be scrubbed backwards.
function play(p, t, o) {
  const n = Math.round(p.slots), free = Array(n).fill(t.formed), shots = [];
  for (let k = 0; t.fire + k * p.every <= t.stop - .4; k++) {
    const T = t.fire + k * p.every, c = casterAt(p, t, o, T);
    let target = -1, near = p.range;
    Hostiles.forEach((q, j) => {
      const d = Math.hypot(o.x + q[0] - c.x, o.z + q[1] - c.z), held = shots.filter(h => h.target === j && h.fireAt + p.stuck > T).length;
      if (d <= near && held < MaxPins) { target = j; near = d; }
    });
    if (target < 0) continue;
    const chest = { x: o.x + Hostiles[target][0], z: o.z + Hostiles[target][1] + Chest };
    const aim = Math.atan2(chest.z - (c.z + Height * Lift), chest.x - c.x) / Mathf.Deg2Rad;
    let slot = -1, best = 1e9;
    for (let i = 0; i < n; i++) {
      const off = Math.abs(turnTo(0, slotAngle(i, n, p, T) - aim, 1));
      if (free[i] <= T && off < best) { slot = i; best = off; }
    }
    if (slot < 0) continue;
    free[slot] = T + p.regrow + Grow;
    const angle = slotAngle(slot, n, p, T), from = ringPoint(c, angle, p.ring, Height);
    const dist = Math.hypot(chest.x - from.x, chest.z - from.z), deg = Math.atan2(chest.z - from.z, chest.x - from.x) / Mathf.Deg2Rad;
    shots.push({ k, fireAt: T, slot, target, from, chest, deg, tangent: angle + 90 - Tilt, out: angle, dist, hitAt: T + Turn + Math.max(0, dist - Reach) / Speed, regrowAt: T + p.regrow });
  }
  return shots;
}

// A sword blade outline from base along d: parallel edges for the first 70 % of its length, then a point.
function tapered(key, base, d, length, width, colour, material, layer) {
  const a = [], b = [];
  [0, .35, .7, .88, 1].forEach(u => {
    const w = width / 2 * (u <= .7 ? 1 : (1 - u) / .3) + .004, x = base.x + d.x * length * u, z = base.z + d.z * length * u;
    a.push({ x: x - d.z * w, z: z + d.x * w }); b.push({ x: x + d.z * w, z: z - d.x * w });
  });
  strip(key, a, b, colour, material, layer);
}

// One blade, from the middle of its length: a dark under-line, a glow, a body and a core that taper
// to the point, a cross guard and a hilt. shown 0..1 grows it from the guard to the tip.
function blade(key, mid, deg, alpha, { layer = Y + .03, hot = 0, length = BladeLength, shown = 1 } = {}) {
  if (alpha <= 0 || shown <= 0) return;
  const r = deg * Mathf.Deg2Rad, d = { x: Math.cos(r), z: Math.sin(r) }, base = { x: mid.x - d.x * length * .4, z: mid.z - d.z * length * .4 }, l = length * shown;
  const mid2 = { x: base.x + d.x * l * .5, z: base.z + d.z * l * .5 };
  tapered(`${key} dark`, base, d, l, BladeWide * 1.5, Deep.withAlpha(.55 * alpha * (1 - hot)), undefined, layer);
  sprite(mid2, l * 1.3, BladeWide * (4.5 + 3 * hot), Blue.withAlpha(.42 * alpha), glow, layer + .001, -deg);   // the halo is a soft texture, not a flat wedge
  tapered(`${key} body`, base, d, l, BladeWide, Color.Lerp(Blue, Ice, .45 + .55 * hot).withAlpha(.85 * alpha), whiteGlow, layer + .002);
  tapered(`${key} core`, base, d, l, BladeWide * .3, White.withAlpha(alpha), whiteGlow, layer + .003);
  streak(`${key} guard`, { x: base.x + d.z * .14, z: base.z - d.x * .14 }, { x: base.x - d.z * .14, z: base.z + d.x * .14 }, .08, Ice.withAlpha(alpha), whiteGlow, layer + .003, 4);
  streak(`${key} hilt`, base, { x: base.x - d.x * length * .24, z: base.z - d.z * length * .24 }, .06, Ice.withAlpha(.8 * alpha), whiteGlow, layer + .002, 4);
}

// A blade breaking like glass: short shards that fly out from along its length and drop, and a glint.
function shards(key, seed, mid, deg, age) {
  if (age < 0 || age >= Break) return;
  const u = age / Break, r = deg * Mathf.Deg2Rad;
  for (let i = 0; i < Shards; i++) {
    const along = (rand(seed * 13 + i) - .5) * BladeLength * .8, out = rand(seed * 17 + i + 40) * Math.PI * 2, far = (.2 + .45 * rand(seed * 19 + i + 80)) * smooth(u);
    const at = { x: mid.x + Math.cos(r) * along + Math.cos(out) * far, z: mid.z + Math.sin(r) * along + Math.sin(out) * far - .3 * u * u };
    const tilt = rand(seed * 23 + i) * Math.PI + u * 3, half = .05 + .06 * rand(seed * 29 + i);
    streak(`${key} shard ${i}`, { x: at.x - Math.cos(tilt) * half, z: at.z - Math.sin(tilt) * half }, { x: at.x + Math.cos(tilt) * half, z: at.z + Math.sin(tilt) * half }, .07, Ice.withAlpha(.9 * (1 - u)), whiteGlow, Y + .035, 3);
  }
  glint(`${key} break`, mid, .28 * (1 - u) + .06, 1 - u, White, 30);
}

export default {
  kit: 'Vergil', label: 'Summoned Swords (sketch)',
  params: {
    scenario: { label: 'The carrier', value: 'walks east', options: ['walks east', 'stands'], group: 'Scene' },
    range: P('Range (cells)', 12, 6, 18, .5, 'Rule'),
    every: P('One shot every (s)', .6, .2, 1.5, .05, 'Rule'),
    regrow: P('A fired slot stays empty (s)', 2.4, .5, 6, .1, 'Rule'),
    stuck: P('A blade stays in the target (s)', 3, 1, 8, .25, 'Rule'),
    slots: P('Blades in the ring', 8, 4, 12, 1, 'Shape'),
    ring: P('Ring radius (cells)', .95, .6, 1.6, .05, 'Shape'),
    spin: P('Spin (degrees per second)', 160, 0, 400, 10, 'Shape'),
    form: P('Warm-up (the blades rise)', .5, .2, 1.5, .05, 'Timing (s)'),
    lasts: P('Shown for (the ability is 20)', 6, 3, 20, .5, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Stands', t: 0 }, { name: 'The blades rise', t: t.cast }, { name: 'Fires on its own', t: t.fire }, { name: 'Every blade breaks', t: t.stop }];
  },
  events(p) {
    const t = times(p);
    return [{ t: t.cast, type: 'sound', def: 'AG_Vergil_SummonedSwords' }, { t: t.stop, type: 'sound', def: 'AG_Vergil_SwordsBreak' }, { t: t.stop, type: 'shake', value: .03 }];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const n = Math.round(p.slots), shots = play(p, t, o), c = casterAt(p, t, o, s), sinceStop = s - t.stop;
    const left = 1 - smooth(sinceStop / RingFade);

    // --- the floor: the range, and the ring's mark under the carrier ----------------------------------------------------------
    ringAt(c, p.range, Blue.withAlpha(.22 * (s < t.cast ? 1 : left)), Floor + .015);
    if (s >= t.cast) ringAt(c, p.ring * smooth((s - t.cast) / (p.form * .6)), Blue.withAlpha((s < t.formed ? .7 : .3) * left), Floor + .02);

    // --- pawns, north first -------------------------------------------------------------------------------------------------------------
    const figures = [{ pos: c, caster: true }, { pos: { x: o.x + AllyAt[0], z: o.z + AllyAt[1] }, ally: true },
      ...Hostiles.map((q, j) => ({ pos: { x: o.x + q[0], z: o.z + q[1] }, j }))];
    figures.sort((a, b) => b.pos.z - a.pos.z).forEach(g => {
      if (g.caster) {
        if (s >= t.cast && s < t.formed + .3) aura('summoned swords', c, s, .5 * clamp((s - t.cast) / p.form) * (1 - clamp((s - t.formed) / .3)), Blue);
        carrier(c, sun, strength);
        return;
      }
      if (g.ally) { pawn(g.pos, Ally, sun, strength); return; }
      const mine = shots.filter(h => h.target === g.j && h.hitAt <= s), last = mine.length ? mine[mine.length - 1].hitAt : -9;
      const held = mine.filter(h => s < Math.min(h.hitAt + p.stuck, t.stop)).length, flash = .7 * clamp(1 - (s - last) / .1);
      const pos = { x: g.pos.x + (s - last < .15 ? Math.sin(s * 95 + g.j) * .035 : 0), z: g.pos.z };
      pawn(pos, Color.Lerp(EnemyColour, Blue, .12 * held), sun, strength, { tint: White, tintAmount: flash });
      mine.forEach((h, i) => draw(MeshPool.plane10, pos.x + (rand(g.j * 9 + i) - .5) * .16, pawnLayer + .02 + i * .0005, pos.z + .06 + .05 * (i % 8), .2, .03, (i * 53 + g.j * 29) % 140 - 70, Wound.withAlpha(.85)));
    });

    // --- the ring: blades rise out of the floor, circle, regrow after a shot, break at the end --------------------------------------------
    for (let i = 0; i < n && s >= t.cast; i++) {
      const fired = shots.filter(h => h.slot === i && h.fireAt <= s).pop(), angle = slotAngle(i, n, p, s), north = Math.sin(angle * Mathf.Deg2Rad) > 0;
      const layer = north ? pawnLayer - .02 : Y + .03, key = `summoned swords slot ${i}`;
      if (sinceStop >= 0) {
        if (!fired || t.stop >= fired.regrowAt + Grow * .5) shards(key, i + 1, ringPoint(casterAt(p, t, o, t.stop), slotAngle(i, n, p, t.stop), p.ring, Height), slotAngle(i, n, p, t.stop) + 90 - Tilt, sinceStop);
        continue;
      }
      if (fired) {
        const u = clamp((s - fired.regrowAt) / Grow);
        if (s < fired.regrowAt) continue;
        const mid = ringPoint(c, angle, p.ring, Height);
        if (u < 1) glint(`${key} regrow`, mid, .22 * (1 - u), 1 - u, White, 30);
        blade(key, mid, angle + 90 - Tilt, u, { layer, shown: smooth(u), hot: 1 - u });
        sprite({ x: mid.x + sun.x * Height, z: mid.z - Height * Lift + sun.z * Height }, .75, .14, Ink.withAlpha(strength * .6 * u), soft, shadowLayer, -(angle + 90 - Tilt));
        continue;
      }
      const appear = t.cast + i / n * Math.max(0, p.form - Rise), u = clamp((s - appear) / Rise);
      if (s < appear) continue;
      const height = Height * smooth(u), mid = ringPoint(c, angle, p.ring, height);
      if (u < 1) glint(`${key} rise`, ringPoint(c, angle, p.ring, 0), .3 * (1 - u), 1 - u, White, 30);
      blade(key, mid, angle + 90 - Tilt, u, { layer, hot: 1 - u });
      sprite({ x: mid.x + sun.x * height, z: mid.z - height * Lift + sun.z * height }, .75, .14, Ink.withAlpha(strength * .6 * u), soft, shadowLayer, -(angle + 90 - Tilt));
    }
    // A thin line of light joins the blades, so the ring reads as one thing.
    if (s >= t.formed - .1 && sinceStop < RingFade) ringAt({ x: c.x, z: c.z + Height * Lift }, p.ring, Blue.withAlpha(.16 * clamp((s - t.formed + .1) / .2) * left), Y + .02, false, whiteGlow);

    // --- the shots: turn, fly, stick, break ---------------------------------------------------------------------------------------------------
    shots.forEach(h => {
      const age = s - h.fireAt, key = `summoned swords shot ${h.k}`, r = h.deg * Mathf.Deg2Rad, d = { x: Math.cos(r), z: Math.sin(r) };
      if (age < 0) return;
      if (age < .12) glint(`${key} fire`, h.from, .3 * (1 - age / .12), 1 - age / .12, White, 30);
      if (s < h.hitAt) {
        const u = clamp(age / Turn), out = h.out * Mathf.Deg2Rad, flown = Math.max(0, age - Turn) * Speed;
        const mid = { x: h.from.x + Math.cos(out) * .2 * smooth(u) * (1 - clamp(flown)) + d.x * flown, z: h.from.z + Math.sin(out) * .2 * smooth(u) * (1 - clamp(flown)) + d.z * flown };
        if (flown > 0) {
          const back = Math.min(flown, 2.4), tail = { x: mid.x - d.x * back, z: mid.z - d.z * back };
          sprite({ x: (mid.x + tail.x) / 2, z: (mid.z + tail.z) / 2 }, back * 1.2, .5, Blue.withAlpha(.4), glow, Y + .024, -h.deg);
          streak(`${key} trail`, tail, mid, .07, Ice.withAlpha(.6), whiteGlow, Y + .025, 6);
        }
        blade(key, mid, turnTo(h.tangent, h.deg, smooth(u)), 1, { hot: u });
        return;
      }
      const breakAt = Math.min(h.hitAt + p.stuck, t.stop), lean = h.deg + (rand(h.k + 3) - .5) * 24, lr = lean * Mathf.Deg2Rad;
      const tip = { x: h.chest.x + (rand(h.k + 9) - .5) * .14, z: h.chest.z + (rand(h.k + 15) - .5) * .14 };
      const mid = { x: tip.x - Math.cos(lr) * StuckOut * .6, z: tip.z - Math.sin(lr) * StuckOut * .6 };
      if (s < breakAt) blade(key, mid, lean, 1, { length: StuckOut, hot: clamp(1 - (s - h.hitAt) / .15) });
      else shards(key, h.k + 20, mid, lean, s - breakAt);
      const hit = (s - h.hitAt) / .16;
      if (hit < 1) {
        sprite(tip, .7 * (1 - hit) + .2, .7 * (1 - hit) + .2, Ice.withAlpha(.6 * (1 - hit)), glow, Y + .04);
        glint(`${key} hit`, tip, .4 * (1 - hit), 1 - hit, White, h.deg);
      }
    });

    // --- it ends: one thin ring front round the carrier -------------------------------------------------------------------------------------------
    if (sinceStop >= 0 && sinceStop < .3) ringAt({ x: c.x, z: c.z + Height * Lift }, p.ring * (1 + .6 * smooth(sinceStop / .3)), Ice.withAlpha(.6 * (1 - sinceStop / .3)), Y + .03, false, whiteGlow);
  },
};
