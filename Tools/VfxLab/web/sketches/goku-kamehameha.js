// Kamehameha — technique proposal for the Goku kit, not the game. Nothing in Source/RimArt draws
// this yet. The kit's line nuke.
//
// What it is for (from the user's draft; every number is a placeholder).
//   Target a direction. Channel 2.5 s standing still; a stun or a downing cancels it and the
//   cooldown is still spent. Then a beam 3 cells wide and 30 long from the caster. It lasts 1.2 s
//   and hits 4 times, 20 damage each (80 total, heat-like, 50% armor penetration), on every pawn
//   and building in the lane, friend or foe. It passes through pawns and low cover; a wall takes
//   the damage and stops the beam there unless it breaks. Pawns that are hit are pushed 1.5 cells
//   along the lane. Cooldown 120 s. The lane is drawn on the floor from the start of the channel,
//   at its true size, so the player sees who is in it. Enemy AI does not dodge, and melee that is
//   running at the caster stays in the lane, which is what the channel time is balanced against.
//   Warp Kamehameha (the draft's empty "Combo" line, filled in from the Cell fight): a second
//   gizmo while Instant Transmission is off cooldown. Pick the firing cell and the direction. The
//   caster channels where it stands, out of danger, jumps at the end of the channel and fires at
//   once from the new cell. It spends both cooldowns.
//
// Order, with the default timings (stand and fire):
//   0.00  stand; four enemies in the lane (one walking at the caster), two outside it
//   0.30  channel 2.5 s in four beats, "Ka-me-ha-me": hands cupped at the rear hip, a ball of ki
//         between them that steps up in size on each beat and sends a ring out; rays leak from it;
//         12 threads of light run into the hands; an aura stands round the caster; blue light,
//         wind rings and dust on the floor; 10 pebbles lift off the ground round the caster.
//         The lane shows on the floor and pulses on each beat.
//   2.80  "HA": the hands go forward, a white burst with 14 lines at the muzzle, camera shake. The
//         head of the beam, a bulb 1.25x the beam's width, crosses the 30 cells in 0.25 s.
//   2.80  to 4.00 the beam holds: a wide blue glow, a sky-blue sheath and a white core whose edges
//         ripple toward the far end; rings that run along it; dark flow lines; a burst where it
//         ends; blue light on the floor; dust thrown out to both sides as the head passes. The
//         caster slides back 0.3 cells. Pawns in the lane go white and are carried 1.5 cells.
//   4.00  the beam thins from both edges to a thread over 0.35 s and is gone
//   4.35  a scorched groove the length of the lane, sparkles rising from it, the pawns that were in
//         the lane down, the two outside it untouched.
//
// Drawing: the beam is a flat shape at chest height, so it turns freely with the aim and needs no
// per-facing method. Three strip meshes (glow, sheath, core) rebuilt each frame while it shows, 61
// points each. The rings are one ring mesh scaled into ellipses across the beam. The vanish of the
// warp is Instant Transmission's, from lib/goku.js. Pawns are stand-ins.
import { Mathf, Meshes } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import {
  Ki, KiDeep, KiSky, KiIce, White, Gi, Dust, Ink, EnemyColour, Chest, pawn, rock, sliced, blink, ringAt, kiBall, aura, strip, streak, whiteGlow, smooth, clamp,
} from './lib/goku.js';

const ring = Meshes.band(.84, 1, 40, 'kamehameha ring');
// Decided values. The panel keeps only what is still being tuned.
const Lead = .3, Tail = 1.6, HeadTime = .25, Thin = .35, Beats = 4, Threads = 12, Pebbles = 10, WindRings = 3, Push = 1.5, Recoil = .3;
const Vanish = .12, Gap = .06, Step = .5, BurstLines = 14, FlowLines = 14, BeamRings = 5, SideDust = 26, Sparkles = 22;
// Enemies as [cells along the lane from the caster, cells across it, walks at the caster].
const Enemies = [[5, .6, false], [10, -.9, false], [17, .2, true], [24, -.5, false], [11, 2.3, false], [19, -3.2, false]];
const WalkIn = 1.1, WarpFrom = [9, 6];   // where the warp's caster channels, from the firing cell

const warps = p => p.scenario === 'warps in and fires';
function times(p) {
  const cast = Lead, fire = cast + p.channel, go = fire - Vanish * 2 - Gap, out = fire + HeadTime, thin = fire + p.hold, gone = thin + Thin;
  return { cast, go, fire, out, thin, gone, end: gone + Tail };
}

export default {
  kit: 'Goku', label: 'Kamehameha (sketch)',
  params: {
    scenario: { label: 'The caster', value: 'stands and fires', options: ['stands and fires', 'warps in and fires'], group: 'Showcase' },
    aim: P('Direction of the beam (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    length: P('Beam length (cells)', 30, 10, 40, 1, 'Shape'),
    width: P('Beam width, the lane that is hit (cells)', 3, 1, 5, .5, 'Shape'),
    ballSize: P('Ball across when full (cells)', .55, .3, 1, .05, 'Shape'),
    channel: P('Channel', 2.5, 1, 4, .1, 'Timing (s)'),
    hold: P('Beam holds', 1.2, .5, 3, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Stand', t: 0 }, { name: 'Ka-me-ha-me (channel)', t: t.cast }, ...(warps(p) ? [{ name: 'Warp', t: t.go }] : []),
    { name: 'HA', t: t.fire }, { name: 'Beam holds', t: t.out }, { name: 'Beam thins', t: t.thin }, { name: 'Aftermath', t: t.gone },
  ]; },
  events(p) {
    const t = times(p), list = [{ t: t.fire, type: 'shake', value: .1 }];
    for (let at = t.fire + .2; at < t.thin; at += .2) list.push({ t: at, type: 'shake', value: .04 });
    return list;
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a), len = p.length, W = p.width;
    // The chosen cell is the middle of the lane. from is the firing cell; along runs down the lane.
    const from = { x: o.x - ca * len / 2, z: o.z - sa * len / 2 };
    const place = (along, across = 0, up = 0) => ({ x: from.x + along * ca - across * sa, z: from.z + along * sa + across * ca + up });
    const warp = warps(p), home = warp ? place(WarpFrom[0], WarpFrom[1]) : from;
    const fired = s - t.fire, firing = fired >= 0 && s < t.gone;
    const reach = len * clamp(fired / HeadTime), wide = 1 - smooth((s - t.thin) / Thin);

    // --- the floor: the lane, the light under the beam, the groove after it ---------------------------------------
    const charge = clamp((s - t.cast) / p.channel), beat = Math.min(Beats - 1, Math.floor(charge * Beats)), inBeat = charge * Beats - beat;
    if (s >= t.cast && s < t.fire) {
      const show = smooth((s - t.cast) / .3), pulse = .35 + .4 * (1 - clamp(inBeat / .5));
      [-1, 1].forEach(side => streak(`kamehameha lane ${side}`, place(.5, side * W / 2), place(len, side * W / 2), .07, KiSky.withAlpha(pulse * show), undefined, Floor + .02, 2));
      streak('kamehameha lane end', place(len, -W / 2), place(len, W / 2), .07, KiSky.withAlpha(pulse * show), undefined, Floor + .02, 2);
      sprite(place(len / 2), len, W, KiDeep.withAlpha(.1 * show), glow, Floor + .006, -p.aim);
    }
    if (fired >= 0) {
      const mark = place(reach / 2), settle = 1 - .45 * smooth((s - t.gone) / Tail);
      sprite(mark, reach + 1, W * .8, Ink.withAlpha(.42 * settle * clamp(fired / .4)), soft, Floor + .01, -p.aim);
      if (firing) sprite(mark, reach + 3, W * 2.4, KiDeep.withAlpha(.4 * wide), glow, Floor + .012, -p.aim);
    }

    // --- pawns, north first ---------------------------------------------------------------------------------------------
    const passes = d => t.fire + HeadTime * d / len;
    const figures = Enemies.filter(([d]) => d < len).map(([d, across, walks], i) => {
      const inLane = Math.abs(across) <= W / 2, hitAt = passes(d), since = s - hitAt;
      const walked = walks ? WalkIn * Math.min(s, hitAt) : 0, carried = inLane ? Push * smooth(since / .5) : 0;
      return { pos: place(d - walked + carried, across), inLane, since, i };
    });
    const out = warp ? clamp((s - t.go) / Vanish) : 0, there = !warp || s >= t.go + Vanish + Gap, back = warp && there ? 1 - clamp((s - t.go - Vanish - Gap) / Vanish) : 0;
    const slide = firing || s >= t.gone ? Recoil * smooth(fired / p.hold) : 0, lunge = .12 * smooth(fired / .05) * (1 - smooth((fired - .05) / .2));
    const stand = there ? place(-slide + lunge) : home, shown = !warp || s < t.go + Vanish || there;
    if (shown) figures.push({ pos: stand, caster: true });
    const power = s < t.fire ? smooth(charge * 1.5) : wide;
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      if (g.caster) {
        if (s >= t.cast && s < t.gone) aura('kamehameha', g.pos, s, power * (1 - Math.max(out, back)));
        const u = there ? back : out;
        if (u > 0) sliced(g.pos, Gi, u, sun, strength, { hair: true }); else pawn(g.pos, Gi, sun, strength, { hair: true, tint: KiIce, tintAmount: .35 * power });
        return;
      }
      const hit = g.inLane && g.since >= 0;
      if (hit && s >= t.thin) pawn(g.pos, EnemyColour, sun, strength, { lie: true });
      else pawn(g.pos, EnemyColour, sun, strength, { tint: hit ? White : KiIce, tintAmount: hit ? .9 : firing ? .25 * wide : 0 });
    });
    if (warp) {
      blink('kamehameha warp leave', home, s - t.go - Vanish * .5);
      blink('kamehameha warp land', from, s - t.go - Vanish - Gap);
    }

    // --- the channel: the ball at the rear hip and everything round it ---------------------------------------------------
    const seen = 1 - Math.max(out, back);
    if (s >= t.cast && s < t.fire) {
      const grown = (beat + smooth(inBeat / .25)) / Beats, size = p.ballSize * grown * (1 + .05 * Math.sin(s * 47));
      const hands = { x: stand.x - ca * .24 + sa * .13, z: stand.z - sa * .24 - ca * .13 + Chest - .06 };
      sprite(stand, 2.6 * (.5 + .5 * grown), 1.8 * (.5 + .5 * grown), Ki.withAlpha(.3 * grown * seen * (.85 + .15 * Math.sin(s * 31))), glow, Floor + .007);
      for (let n = 0; n < WindRings; n++) {
        const v = ((s - t.cast) / .5 + n / WindRings) % 1;
        ringAt(stand, .3 + v * 1.8, KiIce.withAlpha(.5 * (1 - v) * grown * seen));
      }
      // Each beat sends one ring out from the ball.
      ringAt(hands, size / 2 + inBeat * 1.6, KiIce.withAlpha(.9 * (1 - clamp(inBeat / .45)) * seen), Y + .09, false, whiteGlow);
      for (let i = 0; i < 8; i++) {
        const v = ((s - t.cast) * 1.1 + rand(i + 30)) % 1, rad = .4 + v * 1.6, ang = (i * 45 + rand(i + 60) * 30 + v * 40) * Mathf.Deg2Rad;
        sprite({ x: stand.x + Math.cos(ang) * rad, z: stand.z + Math.sin(ang) * rad * .85 + v * .1 }, .35 + v * .4, .28 + v * .3, Dust.withAlpha(.4 * grown * seen * Math.sin(v * Math.PI)), soft, Floor + .04);
      }
      // Pebbles lift off the ground round the caster and hang there.
      for (let i = 0; i < Pebbles; i++) {
        const v = ((s - t.cast) * (.35 + .25 * rand(i + 2)) + rand(i)) % 1, ang = i * 2.399, rad = .55 + rand(i + 7) * 1.1, size0 = .05 + .05 * rand(i + 9);
        const ground = { x: stand.x + Math.cos(ang) * rad, z: stand.z + Math.sin(ang) * rad * .8 }, h = v * (.7 + .8 * rand(i + 4)) * grown, show = Math.sin(v * Math.PI) * seen * clamp(grown * 3);
        sprite({ x: ground.x + sun.x * h, z: ground.z + sun.z * h }, size0 * 2.4, size0 * 1.4, Ink.withAlpha(.35 * show), soft, Floor + .05);
        rock({ x: ground.x, z: ground.z + h * .6 }, size0 * 2.4, i * 50 + s * 50, show, i, Y + .005);
      }
      for (let i = 0; i < Threads; i++) {
        const v = ((s - t.cast) * 1.5 + rand(i)) % 1, ang = (i * 30 + rand(i + 9) * 40) * Mathf.Deg2Rad, r0 = 1.7 * (1 - v) + size / 2, r1 = r0 + .35 * (1 - v) + .05;
        streak(`kamehameha thread ${i}`, { x: hands.x + Math.cos(ang) * r0, z: hands.z + Math.sin(ang) * r0 }, { x: hands.x + Math.cos(ang) * r1, z: hands.z + Math.sin(ang) * r1 },
          .045, KiIce.withAlpha(.85 * Math.sin(v * Math.PI) * seen), whiteGlow, Y + .09, 2);
      }
      kiBall('kamehameha ball', hands, size, s, seen, .5 + .5 * grown);
    }

    // --- HA: the muzzle burst ------------------------------------------------------------------------------------------------
    const muzzle = place(.45 - slide, 0, Chest);
    if (fired >= 0 && fired < .16) {
      const v = fired / .16, f = 1 - v;
      sprite(muzzle, 5.5, 5.5, Ki.withAlpha(.85 * f * f), glow, Y + .13);
      sprite(muzzle, 2.6, 2.6, White.withAlpha(f), glow, Y + .131);
      ringAt(muzzle, .5 + v * 2.2, White.withAlpha(f), Y + .132, true, whiteGlow);
      for (let i = 0; i < BurstLines; i++) {
        const ang = (i * 360 / BurstLines + rand(i + 70) * 14) * Mathf.Deg2Rad, r0 = .5 + v * 1.4, r1 = r0 + .8 + rand(i + 90) * .8;
        streak(`kamehameha burst ${i}`, { x: muzzle.x + Math.cos(ang) * r0, z: muzzle.z + Math.sin(ang) * r0 }, { x: muzzle.x + Math.cos(ang) * r1, z: muzzle.z + Math.sin(ang) * r1 }, .08, White.withAlpha(f), whiteGlow, Y + .133, 4);
      }
    }

    // --- the beam ------------------------------------------------------------------------------------------------------------------
    if (firing && reach > .6) {
      const start = .45 - slide, count = Math.min(80, Math.max(2, Math.ceil((reach - start) / Step)));
      // Half width at d cells down the lane: narrow at the hands, full by 1.8 cells, rippling toward the far end.
      const half = (d, share) => W / 2 * share * wide * (.22 + .78 * smooth((d - start) / 1.8)) * (1 + .05 * Math.sin(d * 1.7 - s * 38) + .03 * Math.sin(d * 4.1 - s * 61));
      const layer = (key, share, colour, material, y) => {
        const left = [], right = [];
        for (let i = 0; i <= count; i++) {
          const d = start + (reach - start) * i / count, h = half(d, share);
          left.push(place(d, h, Chest)); right.push(place(d, -h, Chest));
        }
        strip(key, left, right, colour, material, y);
      };
      layer('kamehameha beam glow', 1.3, Ki.withAlpha(.55), whiteGlow, Y + .1);
      layer('kamehameha beam sheath', .86, KiSky.withAlpha(.9), undefined, Y + .101);
      layer('kamehameha beam core', .46, White.withAlpha(.96), undefined, Y + .104);
      for (let i = 0; i < FlowLines; i++) {
        const d = start + 1 + ((rand(i) * reach + fired * 46) % Math.max(1, reach - start - 2)), across = (rand(i + 15) - .5) * W * .7 * wide, l = 1.2 + rand(i + 33) * 1.6;
        streak(`kamehameha flow ${i}`, place(d, across, Chest), place(Math.min(reach, d + l), across, Chest), .07 * wide, (Math.abs(across) < W * .2 ? KiSky : KiDeep).withAlpha(.55), undefined, Y + .105, 3);
      }
      for (let n = 0; n < BeamRings; n++) {
        const d = start + 1 + ((fired * 24 + n * len / BeamRings) % Math.max(1, len - 2)); if (d > reach - 1) continue;
        const c = place(d, 0, Chest), grow = 1 + .25 * Math.sin(d);
        draw(ring, c.x, Y + .106, c.z, .16, W * .62 * wide * grow, -p.aim, White.withAlpha(.6));
        draw(ring, c.x, Y + .099, c.z, .3, W * .8 * wide * grow, -p.aim, Ki.withAlpha(.5), whiteGlow);
      }
      // The head: a bulb while it travels, a burst where the lane ends once it is there.
      const tip = place(reach, 0, Chest), arrived = reach >= len, bulb = W / 2 * 1.25 * wide * (arrived ? 1 + .12 * Math.sin(s * 52) : 1);
      sprite(tip, bulb * 4.4, bulb * 4.4, Ki.withAlpha(.65 * wide), glow, Y + .1);
      kiBall('kamehameha head', tip, bulb * 2, s, wide, arrived ? 1 : .3);
      const source = place(start, 0, Chest);
      sprite(source, 2.8 + .3 * Math.sin(s * 44), 2.8 + .3 * Math.sin(s * 44), KiIce.withAlpha(.75 * wide), glow, Y + .107);
      sprite(source, 1.1, 1.1, White.withAlpha(.95 * wide), glow, Y + .108);
    }

    // --- dust thrown to both sides as the head passes, and sparkles off the groove afterwards ----------------------------------
    if (fired >= 0) for (let i = 0; i < SideDust; i++) {
      const d = 1 + (i + .5) / SideDust * (len - 1), age = s - passes(d), life = .7 + rand(i) * .4; if (age < 0 || age > life) continue;
      const v = age / life, side = i % 2 ? 1 : -1, at = place(d, side * (W / 2 + .1 + v * (.8 + rand(i + 5))));
      sprite({ x: at.x, z: at.z + v * .4 }, .6 + v * .9, .45 + v * .7, Dust.withAlpha(.5 * Math.sin(v * Math.PI)), soft, Y + .005);
    }
    if (s >= t.thin) for (let i = 0; i < Sparkles; i++) {
      const born = t.thin + rand(i + 3) * .5, life = .7 + rand(i + 12) * .7, v = (s - born) / life; if (v < 0 || v > 1) continue;
      const at = place(1 + rand(i) * (len - 1), (rand(i + 21) - .5) * W * .7, v * .9);
      sprite(at, .16, .16, KiIce.withAlpha(.9 * Math.sin(v * Math.PI)), glow, Y + .02);
    }
  },
};
