// Kamehameha — technique proposal for the Goku kit, not the game. Nothing in Source/RimArt draws
// this yet. The kit's line nuke.
//
// What it is for (from the user's draft; every number is a placeholder).
//   Target a direction. Channel 2.5 s standing still; a stun or a downing cancels it and the
//   cooldown is still spent. Then a beam 3 cells wide from the caster. It runs 30 cells, or to the
//   first wall if that is nearer. It lasts 1.2 s and hits 4 times, 20 damage each (80 total,
//   heat-like, 50% armor penetration), on every pawn and building in the lane, friend or foe. It
//   passes through pawns and low cover. Pawns that are hit are pushed 1.5 cells along the lane.
//   The end blast (agreed 2026-09-20): when the beam lets go, its rear runs down the lane and the
//   beam explodes where it ended: radius 2.5 cells, 40 damage to everything in it; a wall that
//   stopped the beam takes it first. So where the lane ends matters: put the end on the enemy's
//   cover or their back line. Cooldown 120 s.
//   The lane and the end circle are drawn on the floor from the start of the channel, at their true
//   size, so the player sees who is in them. Enemy AI does not dodge, and melee that is running at
//   the caster stays in the lane, which is what the channel time is balanced against.
//   Warp Kamehameha (the draft's empty "Combo" line, filled in from the Cell fight): a second
//   gizmo while Instant Transmission is off cooldown. Pick the firing cell and the direction. The
//   caster channels where it stands, out of danger, jumps at the end of the channel and fires at
//   once from the new cell. It spends both cooldowns.
//
// Order, with the default timings (stand and fire):
//   0.00  stand; four enemies in the lane (one walking at the caster), one beside the lane's end
//         inside the end circle, two outside everything
//   0.30  channel 2.5 s in four beats, "Ka-me-ha-me": hands cupped at the rear hip, a ball of ki
//         between them that steps up in size on each beat. On each beat: a ring leaves the ball, a
//         ring flashes on the floor, the lit floor round the caster steps out (1, 2, 3, 4 cells), and
//         a bar of light runs down the lane from the caster to its end in 0.45 s, so the direction
//         reads from far away. Rays leak from the ball; 12 threads of light run into the hands; an
//         aura stands round the caster; wind rings and dust on the floor; 10 pebbles hang in the
//         air. On the fourth beat the ball flares 35% bigger, the aura grows and the pebbles rise
//         higher.
//   2.80  "HA": the hands go forward, a white burst with 14 lines at the muzzle, a ring runs out
//         3 cells along the floor from the caster's feet, dust is blown 4 cells out behind the
//         caster, camera shake. The head of the beam crosses the lane in 0.25 s: a bright soft bulb
//         with a bow arc in front of it, no rays.
//   2.80  to 4.00 the beam holds. It is drawn as light: four nested additive layers and soft glow
//         along it, so it has no hard edge, a thin white core, and a rounded front; the edges ripple
//         toward the far end; thin rings and pale flow lines run along it. Each pawn in the lane gets a white burst, a
//         ring and sparks as the head reaches it, goes white and is carried 1.5 cells. Dust and
//         rocks are thrown out to both sides as the head passes. Where the beam ends it presses: a
//         pulsing light, sparks thrown back, the end circle flashing. The caster slides back 0.3.
//   4.00  the beam lets go: its rear leaves the hands and runs the lane in 0.3 s
//   4.30  the end blast: a flash, a low blue dome out to the true radius in 0.25 s, 8 cracks of
//         light, 12 rocks thrown, a dust ring, camera shake. Pawns in the circle go down.
//   4.30  to 6.70 a scorched trench the length of the lane whose edges glow and cool over 2 s,
//         smoke rising from it, the rocks lying where they fell, a burnt circle at the end, the
//         pawns that were in the lane or the circle down, the two outside untouched. With the wall
//         on, the beam and the trench stop at the wall, the blast is there, and the pawn behind the
//         wall is untouched.
//
// Drawing: the beam is a flat shape at chest height, so it turns freely with the aim and needs no
// per-facing method; the end dome is level circles. The beam's layers are strip meshes rebuilt each
// frame while it shows, sampled every 0.25 cell. The rings are one ring mesh scaled into ellipses
// across the beam. The vanish of the warp is Instant Transmission's, from lib/goku.js. Pawns and the
// wall are stand-ins.
import { Color, Mathf, Meshes } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import {
  Ki, KiDeep, KiSky, KiIce, White, Gi, Dust, Ink, EnemyColour, Chest, Lift, pawn, rock, sliced, blink, ringAt, kiBall, aura, line, strip, streak, whiteGlow, wallCell, smooth, clamp,
} from './lib/goku.js';

const ring = Meshes.band(.9, 1, 40, 'kamehameha ring'), disc = Meshes.disc(48, 'kamehameha disc');
const TAU = Math.PI * 2, lerp = Mathf.Lerp;
// Decided values. The panel keeps only what is still being tuned.
const Lead = .3, Tail = 2.4, HeadTime = .25, Depart = .3, Beats = 4, Threads = 12, Pebbles = 10, WindRings = 3, Push = 1.5, Recoil = .3;
const Vanish = .12, Gap = .06, Step = .25, BurstLines = 14, FlowLines = 16, BeamRings = 5, SideDust = 26, SideRocks = 20, Smoke = 18;
const LanePulse = .45, FinalFlare = .35, BackDust = 14, HitSparks = 6, PressSparks = 12;
const BlastOpen = .25, BlastHold = .25, BlastFade = .5, BlastLevels = 4, BlastCracks = 8, BlastRocks = 12, Cool = 2, WallAt = 20;
const Grey = new Color(.42, .42, .44);
// Enemies as [cells along the lane from the caster, cells across it, walks at the caster].
const Enemies = [[5, .6, false], [10, -.9, false], [17, .2, true], [24, -.5, false], [29, 2.2, false], [11, 2.3, false], [19, -3.4, false]];
const WalkIn = 1.1, WarpFrom = [9, 6];   // where the warp's caster channels, from the firing cell

const warps = p => p.scenario === 'warps in and fires';
function times(p) {
  const cast = Lead, fire = cast + p.channel, go = fire - Vanish * 2 - Gap, out = fire + HeadTime, release = fire + p.hold, blast = release + Depart;
  return { cast, go, fire, out, release, blast, end: blast + Tail };
}

export default {
  kit: 'Goku', label: 'Kamehameha (sketch)',
  params: {
    scenario: { label: 'The caster', value: 'stands and fires', options: ['stands and fires', 'warps in and fires'], group: 'Showcase' },
    wall: { label: `A wall stands across the lane at cell ${WallAt}`, value: false, group: 'Showcase' },
    aim: P('Direction of the beam (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    length: P('Beam length (cells)', 30, 10, 40, 1, 'Shape'),
    width: P('Beam width, the lane that is hit (cells)', 3, 1, 5, .5, 'Shape'),
    blast: P('End blast radius (cells)', 2.5, 1, 5, .1, 'Shape'),
    ballSize: P('Ball across when full (cells)', .55, .3, 1, .05, 'Shape'),
    channel: P('Channel', 2.5, 1, 4, .1, 'Timing (s)'),
    hold: P('Beam holds', 1.2, .5, 3, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Stand', t: 0 }, { name: 'Ka-me-ha-me (channel)', t: t.cast }, ...(warps(p) ? [{ name: 'Warp', t: t.go }] : []),
    { name: 'HA', t: t.fire }, { name: 'Beam holds', t: t.out }, { name: 'Beam lets go', t: t.release }, { name: 'End blast', t: t.blast },
  ]; },
  events(p) {
    const t = times(p), list = [{ t: t.fire, type: 'shake', value: .1 }];
    for (let at = t.fire + .2; at < t.release; at += .2) list.push({ t: at, type: 'shake', value: .04 });
    return [...list, { t: t.blast, type: 'shake', value: .14 }, { t: t.blast + .2, type: 'shake', value: .06 }];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a), len = p.length, W = p.width, R = p.blast;
    // The chosen cell is the middle of the lane. from is the firing cell; along runs down the lane.
    const from = { x: o.x - ca * len / 2, z: o.z - sa * len / 2 };
    const place = (along, across = 0, up = 0) => ({ x: from.x + along * ca - across * sa, z: from.z + along * sa + across * ca + up });
    const warp = warps(p), home = warp ? place(WarpFrom[0], WarpFrom[1]) : from;
    const walled = p.wall && WallAt < len, stop = walled ? WallAt - .5 : len, end = place(stop);
    const fired = s - t.fire, firing = fired >= 0 && s < t.blast, blasted = s - t.blast;
    const reach = stop * clamp(fired / (HeadTime * stop / len)), rear = stop * clamp((s - t.release) / Depart), arrived = fired >= 0 && reach >= stop;
    const passes = d => t.fire + HeadTime * d / len;
    const around = (c, ang, d) => ({ x: c.x + Math.cos(ang) * d, z: c.z + Math.sin(ang) * d });

    // --- the floor: the lane and the end circle, the light under the beam, the trench after it -------------------------------
    const charge = clamp((s - t.cast) / p.channel), beat = Math.min(Beats - 1, Math.floor(charge * Beats)), inBeat = charge * Beats - beat, beatAge = inBeat * p.channel / Beats;
    const last = beat === Beats - 1 && s < t.fire;
    if (s >= t.cast && s < t.blast) {
      const show = smooth((s - t.cast) / .3), pulse = s < t.fire ? .35 + .4 * (1 - clamp(inBeat / .5)) : .3 + .3 * Math.sin(s * 40);
      if (s < t.fire) {
        [-1, 1].forEach(side => streak(`kamehameha lane ${side}`, place(.5, side * W / 2), place(stop, side * W / 2), .07, KiSky.withAlpha(pulse * show), undefined, Floor + .02, 2));
        sprite(place(stop / 2), stop, W, KiDeep.withAlpha(.1 * show), glow, Floor + .006, -p.aim);
        // A bar of light runs down the lane on each beat.
        const run = beatAge / LanePulse;
        if (run < 1) {
          const d = stop * run, fade = Math.sin(Math.min(1, run * 1.1) * Math.PI);
          streak('kamehameha lane pulse', place(d, -W / 2), place(d, W / 2), .3, KiIce.withAlpha(.9 * fade), whiteGlow, Floor + .025, 4);
          sprite(place(d), 2.4, W * 1.3, Ki.withAlpha(.45 * fade), glow, Floor + .024, -p.aim);
        }
      }
      ringAt(end, R, KiSky.withAlpha(pulse * show), Floor + .02);                                             // the end blast, at its true radius
      draw(disc, end.x, Floor + .006, end.z, R, R, 0, KiDeep.withAlpha((arrived ? .3 : .1) * show), whiteGlow);
    }
    if (fired >= 0) {
      const made = Math.max(0, reach), settle = 1 - .3 * smooth(blasted / Tail), heat = 1 - smooth((s - t.release) / Cool);
      sprite(place(made / 2), made + 1, W * .95, Ink.withAlpha(.6 * settle * clamp(fired / .4)), soft, Floor + .01, -p.aim);
      // The trench's edges glow and cool.
      if (made > 1) [-1, 1].forEach(side => {
        const pts = []; for (let d = .5; d <= made; d += 1) pts.push(place(d, side * (W * .33 + .05 * Math.sin(d * 1.3 + side))));
        line(`kamehameha trench ${side}`, pts, .1, Color.Lerp(KiDeep, KiSky, heat).withAlpha(.15 + .4 * heat), whiteGlow, Floor + .03, 'both');
      });
      if (firing) sprite(place((rear + reach) / 2), reach - rear + 3, W * 2.6, KiDeep.withAlpha(.45), glow, Floor + .012, -p.aim);
    }
    if (blasted >= 0) {
      const burnt = smooth(blasted / .5) * (1 - .3 * smooth(blasted / Tail)), heat = 1 - smooth(blasted / Cool);
      sprite(end, R * 2.3, R * 2.3, Ink.withAlpha(.5 * burnt), soft, Floor + .011);
      ringAt(end, R * .97, Ink.withAlpha(.5 * burnt), Floor + .012, true);
      for (let i = 0; i < BlastCracks; i++) {
        const ang = i * TAU / BlastCracks + rand(i + 3) * .4, reachOut = R * (.55 + .4 * rand(i + 8)), pts = [];
        for (let k = 0; k <= 6; k++) { const q = around(end, ang, reachOut * k / 6), off = k ? (rand(i * 13 + k) - .5) * .4 : 0; pts.push({ x: q.x - Math.sin(ang) * off, z: q.z + Math.cos(ang) * off }); }
        line(`kamehameha crack ${i}`, pts, .12, Color.Lerp(KiDeep, White, heat).withAlpha(.3 + .65 * heat), whiteGlow, Floor + .031);
      }
    }

    // --- the wall ---------------------------------------------------------------------------------------------------------------
    if (walled) for (let k = -2; k <= 2; k++) {
      const c = place(WallAt, k);
      wallCell(c, p.aim);
      if (blasted >= 0 && Math.abs(k) <= 1) sprite(c, 1.3, 1.3, Ink.withAlpha(.55 * smooth(blasted / .3)), soft, Y + .001);   // it took the blast
    }

    // --- pawns, north first ---------------------------------------------------------------------------------------------
    const figures = Enemies.filter(([d]) => d < len).map(([d, across, walks], i) => {
      const inLane = Math.abs(across) <= W / 2 && d < stop, hitAt = passes(d), since = inLane ? s - hitAt : -1;
      const walked = walks ? WalkIn * Math.min(s, hitAt) : 0, carried = inLane ? Math.min(Push * smooth(since / .5), Math.max(0, stop - .6 - d)) : 0;
      const inBlast = !inLane && Math.hypot(d - stop, across) <= R && !(walled && d > WallAt);
      return { pos: place(d - walked + carried, across), inLane, inBlast, since, i };
    });
    const out = warp ? clamp((s - t.go) / Vanish) : 0, there = !warp || s >= t.go + Vanish + Gap, back = warp && there ? 1 - clamp((s - t.go - Vanish - Gap) / Vanish) : 0;
    const slide = fired >= 0 ? Recoil * smooth(fired / p.hold) : 0, lunge = .12 * smooth(fired / .05) * (1 - smooth((fired - .05) / .2));
    const stand = there ? place(-slide + lunge) : home, shown = !warp || s < t.go + Vanish || there;
    if (shown) figures.push({ pos: stand, caster: true });
    const power = s < t.fire ? smooth(charge * 1.5) * (last ? 1 + .6 * smooth(inBeat / .3) : 1) : 1 - smooth((s - t.release) / .4);
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      if (g.caster) {
        if (s >= t.cast && s < t.blast) aura('kamehameha', g.pos, s, power * (1 - Math.max(out, back)));
        const u = there ? back : out;
        if (u > 0) sliced(g.pos, Gi, u, sun, strength, { hair: true }); else pawn(g.pos, Gi, sun, strength, { hair: true, tint: KiIce, tintAmount: .35 * Math.min(1, power) });
        return;
      }
      const hit = g.inLane && g.since >= 0, down = (hit && s >= t.release) || (g.inBlast && blasted >= .15);
      if (down) pawn(g.pos, EnemyColour, sun, strength, { lie: true });
      else pawn(g.pos, EnemyColour, sun, strength, { tint: hit || (g.inBlast && blasted >= 0) ? White : KiIce, tintAmount: hit || (g.inBlast && blasted >= 0) ? .9 : firing ? .25 : 0 });
    });
    if (warp) {
      blink('kamehameha warp leave', home, s - t.go - Vanish * .5);
      blink('kamehameha warp land', from, s - t.go - Vanish - Gap);
    }

    // --- the channel: the ball at the rear hip and everything round it ---------------------------------------------------
    const seen = 1 - Math.max(out, back);
    if (s >= t.cast && s < t.fire) {
      const grown = (beat + smooth(inBeat / .25)) / Beats, flare = last ? 1 + FinalFlare * smooth(inBeat / .3) : 1, size = p.ballSize * grown * flare * (1 + .05 * Math.sin(s * 47));
      const hands = { x: stand.x - ca * .24 + sa * .13, z: stand.z - sa * .24 - ca * .13 + Chest - .06 };
      // The lit floor steps out one cell a beat, and a ring flashes on it as it does.
      const lit = beat + smooth(inBeat / .2);
      sprite(stand, lit * 2.6 + 1, (lit * 2.6 + 1) * .85, Ki.withAlpha((.22 + .06 * beat) * seen * (.85 + .15 * Math.sin(s * 31))), glow, Floor + .007);
      ringAt(stand, beat + 1, KiSky.withAlpha(.8 * (1 - clamp(beatAge / .35)) * seen), Floor + .021, false, whiteGlow);
      for (let n = 0; n < WindRings; n++) {
        const v = ((s - t.cast) / .5 + n / WindRings) % 1;
        ringAt(stand, .3 + v * (1 + lit * .7), KiIce.withAlpha(.5 * (1 - v) * grown * seen));
      }
      // Each beat sends one ring out from the ball.
      ringAt(hands, size / 2 + inBeat * 1.6, KiIce.withAlpha(.9 * (1 - clamp(inBeat / .45)) * seen), Y + .09, false, whiteGlow);
      for (let i = 0; i < 8; i++) {
        const v = ((s - t.cast) * 1.1 + rand(i + 30)) % 1, rad = .4 + v * (1 + lit * .6), ang = (i * 45 + rand(i + 60) * 30 + v * 40) * Mathf.Deg2Rad;
        sprite({ x: stand.x + Math.cos(ang) * rad, z: stand.z + Math.sin(ang) * rad * .85 + v * .1 }, .35 + v * .4, .28 + v * .3, Dust.withAlpha(.4 * grown * seen * Math.sin(v * Math.PI)), soft, Floor + .04);
      }
      // Pebbles lift off the ground round the caster and hang there; higher on the last beat.
      for (let i = 0; i < Pebbles; i++) {
        const v = ((s - t.cast) * (.35 + .25 * rand(i + 2)) + rand(i)) % 1, ang = i * 2.399, rad = .55 + rand(i + 7) * 1.4, size0 = .05 + .05 * rand(i + 9);
        const ground = { x: stand.x + Math.cos(ang) * rad, z: stand.z + Math.sin(ang) * rad * .8 }, h = v * (.7 + .8 * rand(i + 4)) * grown * (last ? 1.6 : 1), show = Math.sin(v * Math.PI) * seen * clamp(grown * 3);
        sprite({ x: ground.x + sun.x * h, z: ground.z + sun.z * h }, size0 * 2.4, size0 * 1.4, Ink.withAlpha(.35 * show), soft, Floor + .05);
        rock({ x: ground.x, z: ground.z + h * Lift }, size0 * 2.4, i * 50 + s * 50, show, i, Y + .005);
      }
      for (let i = 0; i < Threads; i++) {
        const v = ((s - t.cast) * 1.5 + rand(i)) % 1, ang = (i * 30 + rand(i + 9) * 40) * Mathf.Deg2Rad, r0 = (1.2 + lit * .4) * (1 - v) + size / 2, r1 = r0 + .35 * (1 - v) + .05;
        streak(`kamehameha thread ${i}`, around(hands, ang, r0), around(hands, ang, r1), .045, KiIce.withAlpha(.85 * Math.sin(v * Math.PI) * seen), whiteGlow, Y + .09, 2);
      }
      kiBall('kamehameha ball', hands, size, s, seen, (.5 + .5 * grown) * flare);
    }

    // --- HA: the muzzle burst, the ring along the floor, the dust blown out behind -----------------------------------------------
    const muzzle = place(.45 - slide, 0, Chest);
    if (fired >= 0 && fired < .16) {
      const v = fired / .16, f = 1 - v;
      sprite(muzzle, 5.5, 5.5, Ki.withAlpha(.85 * f * f), glow, Y + .13);
      sprite(muzzle, 2.6, 2.6, White.withAlpha(f), glow, Y + .131);
      ringAt(muzzle, .5 + v * 2.2, KiSky.withAlpha(f), Y + .132, false, whiteGlow);
      for (let i = 0; i < BurstLines; i++) {
        const ang = (i * 360 / BurstLines + rand(i + 70) * 14) * Mathf.Deg2Rad, r0 = .5 + v * 1.4, r1 = r0 + .8 + rand(i + 90) * .8;
        streak(`kamehameha burst ${i}`, around(muzzle, ang, r0), around(muzzle, ang, r1), .08, White.withAlpha(f), whiteGlow, Y + .133, 4);
      }
    }
    if (fired >= 0 && fired < .7) {
      const v = fired / .7;
      if (fired < .3) ringAt(from, .4 + 2.6 * smooth(fired / .3), KiSky.withAlpha(.9 * (1 - fired / .3)), Floor + .022, false, whiteGlow);
      for (let i = 0; i < BackDust; i++) {
        const spread = (rand(i + 41) - .5) * 1.2, d = .4 + v * (1.5 + 2.5 * rand(i + 42)), at = place(-Math.cos(spread) * d, Math.sin(spread) * d);
        sprite({ x: at.x, z: at.z + v * .35 }, .6 + v * 1.1, .45 + v * .85, Dust.withAlpha(.55 * Math.sin(v * Math.PI)), soft, Y + .005);
      }
    }

    // --- the beam: light, not an object ---------------------------------------------------------------------------------------------
    const start = Math.max(.45 - slide, rear);
    if (firing && reach - start > .4) {
      const count = Math.min(140, Math.max(2, Math.ceil((reach - start) / Step))), leaving = rear > 0;
      // Half width d cells down the lane: narrow at the hands and full by 1.8 cells (or rounded off at the rear once it
      // has let go), with a slow ripple that runs toward the far end.
      const half = (d, share) => W / 2 * share * Math.sqrt(clamp((reach - d) / 1.3) * .85 + .15) * (leaving ? Math.sqrt(clamp((d - rear) / 1.4)) : .22 + .78 * smooth((d - start) / 1.8))
        * (1 + .06 * Math.sin(d * .9 - s * 30) + .025 * Math.sin(d * 2.3 - s * 47));
      const layer = (key, share, colour, material, y) => {
        const left = [], right = [];
        for (let i = 0; i <= count; i++) {
          const d = start + (reach - start) * i / count, h = half(d, share);
          left.push(place(d, h, Chest)); right.push(place(d, -h, Chest));
        }
        strip(key, left, right, colour, material, y);
      };
      // Four nested additive layers, each narrower and brighter: the edge falls off in steps and none of it is opaque.
      [[1.55, .14, Ki], [1.25, .2, Ki], [1, .26, Ki], [.74, .3, KiSky]].forEach(([share, alpha, colour], k) => layer(`kamehameha beam glow ${k}`, share, colour.withAlpha(alpha), whiteGlow, Y + .1 + k * .0005));
      for (let d = start + 1; d < reach - .5; d += 2) sprite(place(d, 0, Chest), 3.6, W * 2 * (half(d, 1) / (W / 2)), Ki.withAlpha(.14), glow, Y + .0995, -p.aim);
      layer('kamehameha beam pale', .46, KiSky.withAlpha(.45), whiteGlow, Y + .103);
      layer('kamehameha beam core', .24, White.withAlpha(.85), whiteGlow, Y + .104);
      for (let i = 0; i < FlowLines; i++) {
        const d = start + 1 + ((rand(i) * reach + fired * 46) % Math.max(1, reach - start - 2)), across = (rand(i + 15) - .5) * W * .75, l = 1.2 + rand(i + 33) * 1.8;
        if (d + .2 >= reach) continue;
        streak(`kamehameha flow ${i}`, place(d, across, Chest), place(Math.min(reach, d + l), across, Chest), .08, (Math.abs(across) < W * .17 ? KiSky : White).withAlpha(.6), whiteGlow, Y + .105, 3);
      }
      for (let n = 0; n < BeamRings; n++) {
        const d = 1.5 + ((fired * 24 + n * stop / BeamRings) % Math.max(1, stop - 2)); if (d > reach - 1 || d < start + .5) continue;
        const c = place(d, 0, Chest), grow = 1 + .25 * Math.sin(d);
        draw(ring, c.x, Y + .106, c.z, .1, W * .62 * grow, -p.aim, KiIce.withAlpha(.55), whiteGlow);
      }
      // The head: a bright soft bulb with a bow arc in front. Where the lane ends it presses instead: a pulsing light
      // and sparks thrown back.
      const tip = place(reach, 0, Chest), bulb = W / 2 * (arrived ? 1.1 + .15 * Math.sin(s * 52) : 1.25);
      sprite(tip, bulb * 4.6, bulb * 4.6, Ki.withAlpha(.6), glow, Y + .107);
      sprite(tip, bulb * 2.8, bulb * 2.8, KiIce.withAlpha(.8), glow, Y + .108);
      sprite(tip, bulb * 1.6, bulb * 1.6, White.withAlpha(.95), glow, Y + .109);
      const bow = [];
      for (let j = 0; j <= 14; j++) { const ang = a + (j / 14 - .5) * 2.5; bow.push(around(tip, ang, bulb * 1.2)); }
      if (!arrived) line('kamehameha bow', bow, .14 + bulb * .08, White.withAlpha(.85), whiteGlow, Y + .11, 'both');   // only while it travels: at a wall it would sit past the wall
      if (arrived) for (let i = 0; i < PressSparks; i++) {
        const v = (s * 4 + rand(i + 12)) % 1, ang = a + Math.PI + (rand(i + 13) - .5) * 2.6, r0 = bulb * .8 + v * 2, r1 = r0 + .5 + rand(i + 14) * .7;
        streak(`kamehameha press spark ${i}`, around(tip, ang, r0), around(tip, ang, r1), .08, White.withAlpha(1 - v), whiteGlow, Y + .111, 3);
      }
      if (!leaving) {
        const source = place(start, 0, Chest);
        sprite(source, 2.8 + .3 * Math.sin(s * 44), 2.8 + .3 * Math.sin(s * 44), KiIce.withAlpha(.75), glow, Y + .112);
        sprite(source, 1.1, 1.1, White.withAlpha(.95), glow, Y + .113);
      }
    }

    // --- the hit on each pawn in the lane as the head reaches it ------------------------------------------------------------------------
    figures.forEach(g => {
      if (g.caster || !g.inLane || g.since < 0 || g.since >= .22) return;
      const v = g.since / .22, c = { x: g.pos.x, z: g.pos.z + Chest };
      sprite(c, 1.2 + v * 1.4, 1.2 + v * 1.4, White.withAlpha(1 - v), glow, Y + .114);
      ringAt(c, .3 + v * 1.1, White.withAlpha(.9 * (1 - v)), Y + .115, true, whiteGlow);
      for (let k = 0; k < HitSparks; k++) {
        const ang = a + (k / (HitSparks - 1) - .5) * 2.4 + (k % 2 ? .5 : -.5) * Math.PI, r0 = .3 + v * 1.2;
        streak(`kamehameha hit ${g.i} ${k}`, around(c, ang, r0), around(c, ang, r0 + .6), .07, White.withAlpha(1 - v), whiteGlow, Y + .116, 3);
      }
    });

    // --- thrown to both sides as the head passes: dust, and rocks that stay ------------------------------------------------------------------
    if (fired >= 0) for (let i = 0; i < SideDust; i++) {
      const d = 1 + (i + .5) / SideDust * (stop - 1), age = s - passes(d), life = .7 + rand(i) * .4; if (age < 0 || age > life) continue;
      const v = age / life, side = i % 2 ? 1 : -1, at = place(d, side * (W / 2 + .1 + v * (.8 + rand(i + 5))));
      sprite({ x: at.x, z: at.z + v * .4 }, .6 + v * .9, .45 + v * .7, Dust.withAlpha(.5 * Math.sin(v * Math.PI)), soft, Y + .005);
    }
    if (fired >= 0) for (let i = 0; i < SideRocks; i++) {
      const d = 1.5 + rand(i + 80) * (stop - 2.5), age = s - passes(d); if (age < 0) continue;
      const air = .5 + .4 * rand(i + 81), u = Math.min(1, age / air), side = i % 2 ? 1 : -1, size = .14 + .3 * rand(i + 82) * rand(i + 83);
      const foot = place(d + u * (rand(i + 84) - .3) * 1.5, side * (W / 2 + u * (.6 + 1.8 * rand(i + 85)))), h = (.6 + 1.2 * rand(i + 86)) * 4 * u * (1 - u);
      sprite({ x: foot.x + sun.x * h, z: foot.z + sun.z * h }, size * 2.2, size * 1.1, Ink.withAlpha(.3), soft, Floor + .05);
      rock({ x: foot.x, z: foot.z + h * Lift }, size, i * 47 + u * 400, 1, i, u < 1 ? Y + .006 : Floor + .06);
    }

    // --- the end blast -------------------------------------------------------------------------------------------------------------------------
    if (blasted >= 0) {
      const opened = 1 - Math.pow(1 - clamp(blasted / BlastOpen), 3), r = R * opened, fading = smooth((blasted - BlastOpen - BlastHold) / BlastFade), alive = 1 - fading;
      if (alive > 0) {
        const first = clamp(1 - blasted / .3);
        sprite(end, R * 5, R * 5, KiIce.withAlpha(.8 * first * first), glow, Y + .12);
        sprite(end, R * 2.4, R * 2.4, White.withAlpha(first), glow, Y + .121);
        for (let k = 0; k < BlastLevels; k++) {
          const tilt = k / BlastLevels * 80 * Mathf.Deg2Rad, c = { x: end.x, z: end.z + r * .38 * Math.sin(tilt) * (1 + .5 * fading) * Lift / .6 }, rad = r * Math.cos(tilt);
          draw(disc, c.x, Y + .122 + k * .001, c.z, rad, rad, 0, Color.Lerp(KiDeep, KiSky, k / (BlastLevels - 1)).withAlpha(.28 * alive), whiteGlow);
          const arc = [], turn = s * (k % 2 ? 110 : -90) + k * 80;
          for (let j = 0; j <= 12; j++) arc.push(around(c, (turn + 120 * j / 12) * Mathf.Deg2Rad, rad * .96));
          line(`kamehameha blast arc ${k}`, arc, .1, White.withAlpha(.6 * alive), whiteGlow, Y + .127, 'both');
        }
        ringAt(end, r, KiIce.withAlpha(.9 * alive), Y + .128, true, whiteGlow);                              // the front, on the true radius
      }
      for (let i = 0; i < BlastRocks; i++) {
        const air = .6 + .6 * rand(i + 51), u = Math.min(1, blasted / air), ang = rand(i + 52) * TAU, big = rand(i + 55), size = .18 + .45 * big * big;
        const foot = around(end, ang, R * (.2 + .4 * rand(i + 53)) + R * .9 * u), h = (1.5 + 2.5 * rand(i + 54)) * (1.1 - .5 * big) * 4 * u * (1 - u);
        sprite({ x: foot.x + sun.x * h, z: foot.z + sun.z * h }, size * 2.2, size * 1.1, Ink.withAlpha(.3), soft, Floor + .05);
        rock({ x: foot.x, z: foot.z + h * Lift }, size, i * 53 + u * air * 500, 1, i + 2, u < 1 ? Y + .129 : Floor + .06);
      }
      const v = blasted / 1; if (v <= 1) for (let i = 0; i < 20; i++) {
        const at = around(end, i * TAU / 20 + rand(i + 60) * .2, R * (.9 + .8 * smooth(v)));
        sprite({ x: at.x, z: at.z + v * .4 }, .9 + v * 1.3, .7 + v, Dust.withAlpha(.5 * Math.sin(v * Math.PI)), soft, Y + .005);
      }
    }

    // --- smoke off the trench afterwards ----------------------------------------------------------------------------------------------------------
    if (s >= t.release) for (let i = 0; i < Smoke; i++) {
      const born = t.release + rand(i + 3) * 1.2, life = 1.2 + rand(i + 12) * .9, v = (s - born) / life; if (v < 0 || v > 1) continue;
      const at = place(1 + rand(i) * (stop - 1), (rand(i + 21) - .5) * W * .6, v * 1.1);
      sprite({ x: at.x + v * .3, z: at.z }, .8 + v * 1.4, .7 + v * 1.1, Grey.withAlpha(.32 * Math.sin(v * Math.PI)), soft, Y + .004);
    }
  },
};
