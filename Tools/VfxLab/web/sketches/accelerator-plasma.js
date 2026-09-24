// Plasma — ability proposal for the Accelerator kit, not the game. Nothing in Source/RimArt draws
// this yet. The kit's nuke: one large hit on one point, priced in brain strain.
//
// What it is for (proposed 2026-09-24, not agreed; every number is a placeholder).
//   Target a direction. Accelerator channels 3 s standing still: the wind within 8 cells is turned
//   toward one hand (dust, filth, leaves and light items slide in; pawns are not pulled) and the air
//   there is compressed until it is plasma. Every round that enters the 8 cells while the channel
//   runs is bent into the hand and destroyed (the same catch as Vector manipulation, minus mortar
//   shells), so the channel doubles as a 3 s shield against direct fire; it ends at release, and a
//   round arriving after that hits. Then it goes down a lane 1 cell wide and 15 cells long
//   in 0.3 s and stops at the first pawn or wall it meets. Where it stops it bursts: radius 1.5
//   cells, 50 heat damage, 40% armour penetration, and fires start on flammable cells. A wall that
//   stops it takes the damage first. Pawns behind the first thing hit are untouched: it does not
//   clear a lane the way Kamehameha does. Strain +0.45 on release, cooldown 90 s. A stun, a downing
//   or a move during the channel cancels it: the cooldown is spent, the strain is not.
//   The lane is drawn on the floor from the start of the channel at its true width so the player
//   sees who is in it. No end circle: where it bursts depends on what it meets. Enemy AI does not
//   react to the lane.
//
// Order, with the default timings:
//   0.00  stand; one enemy in the lane at cell 9 (the first hit), one 4 cells behind it in the lane
//         (untouched), one beside the impact inside the burst radius, one well outside; two shooters
//         to either side, 1.3 cells outside the pull circle, firing every 0.9 s from 0.5 s to 4.2 s
//         (rounds fly at 10 cells/s here so the bend can be seen; a real round is several times
//         faster)
//   0.30  channel 3 s: the arm comes out toward the aim; a faint ring on the floor at 8 cells marks
//         the pull; 30 wind lines spiral in from that ring to the hand; dust and small debris slide
//         in along the floor and lift into the hand at the end; the ball at the hand starts large
//         and faint (loose air) and shrinks as it brightens (compressed), and in the last 30% it is
//         plasma: white core, violet shell, rays. The lane pulses once a second down its length.
//         Each round that crosses the 8-cell ring turns pale and spirals into the hand over 1.3 s,
//         about 1.25 turns, spinning faster as it gets close, and vanishes there with a flash; the
//         ball swells 25% for 0.2 s.
//   3.30  release: the arm thrusts, a white ring leaves the hand, dust is blown back behind
//         Accelerator, camera shake. The head crosses the lane at 50 cells/s with a violet tail and
//         speed lines; the floor lights under it.
//   3.48  burst where it stops (cell 9 with the defaults): a flash, soft additive light out to the
//         true radius in 0.2 s, a ring, 14 sparks thrown and falling, camera shake. Pawns in the
//         radius go white and then down; the pawn it hit burns. Fires on the cells inside the
//         radius, flaring and settling; a scorched circle that stays; smoke rising for 2 s.
//   3.48  to 6.00 the fires burn, the two pawns lie, the pawn behind and the pawn outside stand.
//         The shooters' last rounds, fired after the release, fly straight and hit Accelerator.
//         With the wall on, the lance stops at the wall 3 cells short, the wall is scorched and
//         burns, and every pawn stands.
//
// Palette: monochrome, see lib/accelerator.js. White light with black edges for what he controls;
// desaturated blue only inside the ball and the burst; no violet.
//
// Drawing: the ball, the lane and the burst are level circles and flat shapes at chest height, so
// they turn with the aim and need no per-facing method. The wind lines and the tail are strip
// meshes rebuilt while they show. Fires are the Flame Gauntlet ones. Pawns and the wall are
// stand-ins.
import { Color, Mathf } from '../js/engine.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import { pawn, rock, ringAt, line, streak, whiteGlow, wallCell, EnemyColour, Ink, White, Dust, Lift, Chest, clamp, smooth } from './lib/goku.js';
import { fireCell, burningPawn } from './lib/flame-gauntlet.js';
import { accelerator, arm, plasmaBall, Air, AirDeep, Plasma, PlasmaDeep, PlasmaHot } from './lib/accelerator.js';

const TAU = Math.PI * 2, lerp = Mathf.Lerp;
// Decided values. The panel keeps only what is still being tuned.
const Lead = .3, Tail = 2.5, W = 1, WindLines = 30, WindSpeed = .55, SegLen = .18, Loose = 16, LanePulse = 1;
const HotAt = .7, Sparks = 14, SmokePuffs = 10, BackDust = 10, FlashTime = .15, DomeOpen = .2, Recoil = .25;
const Smoke = new Color(.3, .28, .27), Round = new Color(1, .9, .55), Muzzle = new Color(1, .85, .5);
// Shooters as [cells along the lane from Accelerator, side]; they stand 1.3 cells outside the pull circle.
const Shooters = [[3, 1], [-2, -1]], ShooterGap = 1.3, RoundSpeed = 10, FireEvery = .9, FirstShot = .5, LastShot = 4.2, BendTime = 1.3, Orbits = 1.25, Absorb = .2;
// Enemies as [cells along the lane from the first hit, cells across].
const Others = [[4, 0], [.5, 1.1], [-1, -2.6]];

const wallAt = p => Math.max(2, p.firstAt - 3);
const stopAt = p => Math.min(p.length, p.wall ? wallAt(p) - .5 : p.firstAt - .4);
function times(p) {
  const cast = Lead, fire = cast + p.channel, hit = fire + p.flight * stopAt(p) / p.length;
  return { cast, fire, hit, end: hit + Tail };
}

export default {
  kit: 'Accelerator', label: 'Plasma (sketch)',
  params: {
    wall: { label: 'A wall stands across the lane 3 cells before the first pawn', value: false, group: 'Showcase' },
    shooters: { label: 'Two shooters fire at Accelerator', value: true, group: 'Showcase' },
    aim: P('Direction (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    firstAt: P('First pawn in the lane (cells from Accelerator)', 9, 4, 14, 1, 'Showcase'),
    length: P('Lane length (cells)', 15, 8, 25, 1, 'Shape'),
    radius: P('Burst radius (cells)', 1.5, .5, 3, .1, 'Shape'),
    pull: P('Wind pulled from (cells)', 8, 3, 12, .5, 'Shape'),
    ballSize: P('Ball across when compressed (cells)', .5, .25, 1, .05, 'Shape'),
    channel: P('Channel', 3, 1, 5, .1, 'Timing (s)'),
    flight: P('Flight over the full lane', .3, .1, 1, .05, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Stand', t: 0 }, { name: 'Channel', t: t.cast }, { name: 'Release', t: t.fire }, { name: 'Burst', t: t.hit },
  ]; },
  events(p) { const t = times(p); return [
    { t: t.fire, type: 'shake', value: .07 }, { t: t.hit, type: 'shake', value: .14 }, { t: t.hit + .2, type: 'shake', value: .05 },
  ]; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a), len = p.length, R = p.radius;
    // The chosen cell is the middle of the lane. from is where Accelerator stands; along runs down the lane.
    const from = { x: o.x - ca * len / 2, z: o.z - sa * len / 2 };
    const place = (along, across = 0, up = 0) => ({ x: from.x + along * ca - across * sa, z: from.z + along * sa + across * ca + up * Lift });
    const stop = stopAt(p), end = place(stop), walled = p.wall && wallAt(p) < len;
    const charge = clamp((s - t.cast) / p.channel), grown = smooth(charge * 2), stage = smooth(charge), hot = clamp((charge - HotAt) / (1 - HotAt));
    const fired = s - t.fire, flying = fired >= 0 && s < t.hit, reach = stop * clamp(fired / (p.flight * stop / len)), age = s - t.hit;
    const channelling = s >= t.cast && s < t.fire;

    // --- floor: the lane, the pull ring, the light under the head, the scorch after ------------------------------
    if (s >= t.cast && s < t.hit + .4) {
      const show = smooth((s - t.cast) / .3) * (1 - clamp((s - t.hit) / .4)), laneEnd = walled ? wallAt(p) - .5 : len;
      const pulse = channelling ? .3 + .35 * (1 - clamp(((s - t.cast) % LanePulse) / .5)) : .6;
      [-1, 1].forEach(side => streak(`plasma lane ${side}`, place(.6, side * W / 2), place(laneEnd, side * W / 2), .06, Air.withAlpha(pulse * show), undefined, Floor + .02, 2));
      sprite(place(laneEnd / 2), laneEnd, W, AirDeep.withAlpha(.08 * show), glow, Floor + .006, -p.aim);
      if (channelling) {
        const run = ((s - t.cast) % LanePulse) / .45;
        if (run < 1) { const d = laneEnd * run, fade = Math.sin(run * Math.PI); streak('plasma lane pulse', place(d, -W / 2), place(d, W / 2), .25, Air.withAlpha(.8 * fade * show), whiteGlow, Floor + .025, 4); }
      }
    }
    if (flying) sprite(place(Math.max(0, reach - 1.5)), 4, W * 2.5, PlasmaDeep.withAlpha(.35), glow, Floor + .012, -p.aim);
    if (age >= 0) {
      const burnt = smooth(age / .3) * (1 - .25 * smooth(age / Tail));
      sprite(end, R * 2.3, R * 2.3, Ink.withAlpha(.55 * burnt), soft, Floor + .011);
      ringAt(end, R * .97, Ink.withAlpha(.45 * burnt), Floor + .012, true);
    }

    // --- the wall -----------------------------------------------------------------------------------------------------
    if (walled) for (let k = -1; k <= 1; k++) {
      const c = place(wallAt(p), k);
      wallCell(c, p.aim);
      if (age >= 0 && k === 0) sprite(c, 1.3, 1.3, Ink.withAlpha(.6 * smooth(age / .3)), soft, Y + .001);
    }

    // --- pawns, north first --------------------------------------------------------------------------------------------
    const first = { pos: place(p.firstAt), d: p.firstAt, across: 0 };
    const figures = [first, ...Others.map(([d, across]) => ({ pos: place(p.firstAt + d, across), d: p.firstAt + d, across }))].map(g => {
      const behindWall = walled && g.d > wallAt(p);
      const dist = Math.hypot(g.d - stop, g.across), inBurst = !behindWall && dist <= R + .3;
      return { ...g, inBurst, direct: inBurst && g === first };
    });
    const lunge = fired >= 0 ? .12 * smooth(fired / .05) * (1 - smooth((fired - .05) / .25)) : 0, slide = fired >= 0 ? Recoil * smooth(fired / .35) : 0;
    const stand = place(lunge - slide);
    // --- the rounds: caught during the channel, bent into the hand and destroyed; otherwise they hit ------------------
    const hand0 = { x: stand.x + ca * .42, z: stand.z + sa * .42 }, hand = { x: hand0.x, z: hand0.z + Chest };
    let absorbing = 0, struck = 0;
    if (p.shooters) Shooters.forEach(([al, side], j) => {
      const S = place(al, side * (p.pull + ShooterGap)), dx = stand.x - S.x, dz = stand.z - S.z, dist = Math.hypot(dx, dz), ux = dx / dist, uz = dz / dist;
      const dEnter = Math.max(0, dist - p.pull);
      for (let n = 0; n < 8; n++) {
        const tf = FirstShot + n * FireEvery + j * FireEvery / 2;
        if (tf > LastShot) break;
        const shot = s - tf;
        if (shot < 0) continue;
        if (shot < .07) sprite({ x: S.x + ux * .45, z: S.z + uz * .45 + Chest }, .4, .4, Muzzle.withAlpha(1 - shot / .07), glow, Y + .2);
        const tEnter = tf + dEnter / RoundSpeed, caught = tEnter >= t.cast && tEnter < t.fire;
        let at, vx, vz, pale = false;
        if (caught && s >= tEnter) {
          const u = (s - tEnter) / BendTime;
          if (u >= 1) { absorbing += Math.max(0, 1 - (s - tEnter - BendTime) / Absorb); continue; }
          // An inward spiral round the hand: the radius shrinks from the ring to nothing while the
          // angle advances, slowly at first and faster near the centre, and the round rises to the hand.
          const E = { x: S.x + ux * dEnter, z: S.z + uz * dEnter }, th0 = Math.atan2(E.z - hand0.z, E.x - hand0.x), r0 = Math.hypot(E.x - hand0.x, E.z - hand0.z);
          const spiral = q => { const r = r0 * Math.pow(1 - q, 1.3), th = th0 + Orbits * TAU * Math.pow(q, 1.6); return { x: hand0.x + Math.cos(th) * r, z: hand0.z + Math.sin(th) * r + Chest * smooth(q) }; };
          at = spiral(u); const before = spiral(Math.max(0, u - .02));
          vx = at.x - before.x; vz = at.z - before.z;
          pale = true;
        } else {
          const d = shot * RoundSpeed;
          if (d >= dist) {                                     // it arrived: a hit on Accelerator
            const since = (d - dist) / RoundSpeed;
            if (since < .3) { const f = 1 - since / .3; struck = Math.max(struck, f); sprite({ x: stand.x, z: stand.z + Chest }, 1.1 * f + .3, 1.1 * f + .3, White.withAlpha(f), glow, Y + .2); sprite({ x: stand.x - ux * .2, z: stand.z - uz * .2 + .25 }, .6 + .4 * (1 - f), .5 + .3 * (1 - f), Ink.withAlpha(.6 * f), soft, Y + .19); }
            continue;
          }
          at = { x: S.x + ux * d, z: S.z + uz * d + Chest }; vx = ux; vz = uz;
        }
        const vl = Math.hypot(vx, vz) || 1, tail = { x: at.x - vx / vl * .55, z: at.z - vz / vl * .55 };
        streak(`plasma round ${j} ${n}`, tail, at, .09, (pale ? Air : Round).withAlpha(.95), whiteGlow, Y + .15, 3);
        sprite(at, .16, .16, White.withAlpha(.9), glow, Y + .151);
      }
    });
    if (absorbing > 0) { sprite(hand, .6 + .5 * absorbing, .6 + .5 * absorbing, White.withAlpha(.8 * absorbing), glow, Y + .125); ringAt(hand, .3 + (1 - absorbing) * .5, Air.withAlpha(.8 * absorbing), Y + .124, false, whiteGlow); }

    figures.push({ pos: stand, caster: true });
    if (p.shooters) Shooters.forEach(([al, side]) => figures.push({ pos: place(al, side * (p.pull + ShooterGap)), shooter: true }));
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      if (g.caster) {
        const armOut = channelling || fired < .5 ? (channelling ? .4 * smooth((s - t.cast) / .3) : .48 * (1 - smooth((fired - .2) / .3))) : 0;
        arm(g.pos, p.aim, armOut);
        accelerator(g.pos, sun, strength, { tint: White, tintAmount: struck > 0 ? .9 * struck : .3 * hot * (channelling ? 1 : 1 - clamp(fired / .3)), outline: struck });
        return;
      }
      if (g.shooter) { pawn(g.pos, EnemyColour, sun, strength); return; }
      const lit = g.inBurst && age >= 0, down = lit && age >= .3;
      if (down) { pawn(g.pos, EnemyColour, sun, strength, { lie: true }); if (g.direct) burningPawn('plasma victim', { x: g.pos.x + .1, z: g.pos.z }, s, .8 * (1 - .4 * smooth(age / Tail))); }
      else pawn(g.pos, EnemyColour, sun, strength, { tint: White, tintAmount: lit ? .95 : 0 });
    });

    // --- the channel: wind into the hand, the ball -----------------------------------------------------------------------
    if (channelling) {
      const since = s - t.cast;
      ringAt(stand, p.pull, Air.withAlpha((.25 + .3 * absorbing) * grown * (.8 + .2 * Math.sin(s * 6))));
      for (let i = 0; i < WindLines; i++) {
        const v = (since * WindSpeed * (.8 + .4 * rand(i + 3)) + rand(i)) % 1, ang0 = rand(i + 11) * TAU, turn = 1 + .6 * rand(i + 17), pts = [];
        for (let k = 0; k <= 9; k++) {
          const u = clamp(v - SegLen + k / 9 * SegLen), r = p.pull * Math.pow(1 - u, 1.15) + .2, ang = ang0 + u * turn, h = Chest * smooth((u - .6) / .4);
          pts.push({ x: hand0.x + Math.cos(ang) * r, z: hand0.z + Math.sin(ang) * r + h });
        }
        line(`plasma wind ${i}`, pts, .05, Air.withAlpha(.55 * grown * (.3 + .7 * v) * Math.pow(Math.sin(Math.min(1, v) * Math.PI), .5)), whiteGlow, Y + .02, 'both');
      }
      // Loose things on the floor slide in and lift into the hand at the end.
      for (let i = 0; i < Loose; i++) {
        const v = (since * .3 * (.8 + .4 * rand(i + 2)) + rand(i)) % 1, r = p.pull * (1 - v) + .3, ang = rand(i + 5) * TAU + v * 1.4, h = Chest * smooth((v - .75) / .25);
        const ground = { x: hand0.x + Math.cos(ang) * r, z: hand0.z + Math.sin(ang) * r }, show = Math.sin(v * Math.PI) * grown;
        if (i % 3 === 0) {
          sprite({ x: ground.x + sun.x * h, z: ground.z + sun.z * h }, .18, .1, Ink.withAlpha(.3 * show), soft, Floor + .05);
          rock({ x: ground.x, z: ground.z + h }, .11, i * 40 + since * 90, show, i, Y + .004);
        } else sprite({ x: ground.x, z: ground.z + h }, .35 + .25 * (1 - v), .28 + .2 * (1 - v), Dust.withAlpha(.35 * show), soft, Floor + .04);
      }
      plasmaBall('plasma ball', hand, p.ballSize * (1 + .25 * absorbing), s, grown, stage);
    }

    // --- release: the head down the lane -----------------------------------------------------------------------------
    if (fired >= 0 && fired < .22) ringAt(hand, .25 + fired * 5, White.withAlpha(.8 * (1 - fired / .22)), Y + .09, false, whiteGlow);
    if (fired >= 0 && fired < .8) for (let i = 0; i < BackDust; i++) {
      const u = clamp((fired - rand(i) * .1) / .7), d = .3 + u * (1.5 + 2 * rand(i + 4)), across = (rand(i + 8) - .5) * 1.4;
      sprite(place(-d + lunge - slide, across), .35 + u * .5, .3 + u * .4, Dust.withAlpha(.45 * Math.sin(u * Math.PI)), soft, Floor + .04);
    }
    if (flying) {
      const head = place(reach, 0, Chest / Lift), back = place(Math.max(.4, reach - 5), 0, Chest / Lift), core = place(Math.max(.4, reach - 2.5), 0, Chest / Lift);
      streak('plasma tail', back, head, .55, PlasmaDeep.withAlpha(.45), whiteGlow, Y + .1, 6);
      streak('plasma tail core', core, head, .2, PlasmaHot.withAlpha(.95), whiteGlow, Y + .101, 6);
      for (let i = 0; i < 6; i++) {                                                     // black speed lines
        const side = (i % 2 ? 1 : -1) * (.35 + .5 * rand(i + 30)), from0 = Math.max(.4, reach - 3 - 2 * rand(i)), to0 = Math.max(.4, reach - .6 - rand(i + 3));
        streak(`plasma speed ${i}`, place(from0, side, Chest / Lift), place(to0, side, Chest / Lift), .06, Ink.withAlpha(.7), undefined, Y + .09, 3);
      }
      plasmaBall('plasma head', head, p.ballSize, s, 1, 1);
    }

    // --- the burst ----------------------------------------------------------------------------------------------------------
    if (age >= 0) {
      const endUp = { x: end.x, z: end.z + Chest };
      if (age < FlashTime) sprite(endUp, 5 * (1 - age / FlashTime) + 1, 5 * (1 - age / FlashTime) + 1, White.withAlpha(.9 * (1 - age / FlashTime)), glow, Y + .13);
      const open = smooth(age / DomeOpen), fade = 1 - smooth((age - .35) / .6);
      if (fade > 0) {
        for (let lvl = 0; lvl < 4; lvl++) {
          const rr = R * open * (1.15 - lvl * .22), c = Color.Lerp(PlasmaDeep, White, lvl / 3);
          sprite(end, rr * 2.4, rr * 2.4, c.withAlpha((.25 + .15 * lvl) * fade), glow, Y + .05 + lvl * .001);
        }
        ringAt(end, R * open, PlasmaHot.withAlpha(.7 * fade), Y + .06, false, whiteGlow);
      }
      if (age < .6) ringAt(end, R * (1 + age * 3), Air.withAlpha(.6 * (1 - age / .6)), Floor + .022);
      for (let i = 0; i < Sparks; i++) {
        const ang = i * TAU / Sparks + rand(i + 3) * .4, speed = 2.5 + 3 * rand(i + 7), rise = 2 + 2.5 * rand(i + 9), h = rise * age - 4.9 * age * age;
        if (h < 0) continue;
        const d = speed * age, at = { x: end.x + Math.cos(ang) * d, z: end.z + Math.sin(ang) * d + h * Lift }, prev = { x: end.x + Math.cos(ang) * (d - .25), z: end.z + Math.sin(ang) * (d - .25) + h * Lift };
        streak(`plasma spark ${i}`, prev, at, .08, White.withAlpha(.9), whiteGlow, Y + .12, 3);
      }
      for (let i = 0; i < SmokePuffs; i++) {
        const v = clamp((age - .3 - i * .08) / 2);
        if (v <= 0 || v >= 1) continue;
        const ang = rand(i + 50) * TAU, d = rand(i + 60) * R * .8, h = v * (1 + .6 * rand(i + 70));
        sprite({ x: end.x + Math.cos(ang) * d + Math.sin(v * 5 + i) * .1, z: end.z + Math.sin(ang) * d + h * Lift }, .6 + v * 1.1, .5 + v * 1, Smoke.withAlpha(.55 * Math.sin(v * Math.PI)), soft, Y + .14);
      }
      // Fires on the cells inside the radius; the wall's own cell burns with the wall on.
      const amount = smooth(age / .25) * (.6 + .4 * (1 - smooth(age / Tail))), flare = 1 - smooth(age / .6);
      for (let dx = -2; dx <= 2; dx++) for (let dz = -2; dz <= 2; dz++) {
        if (Math.hypot(dx, dz) > R) continue;
        const c = { x: end.x + dx, z: end.z + dz };
        if (walled && (dx * ca + dz * sa) > .5) continue;   // no fire behind the wall
        fireCell(`plasma fire ${dx} ${dz}`, c, s, amount * (.7 + .3 * rand(dx * 7 + dz)), dx * 5 + dz + 3, Y + .03, flare);
      }
    }
  },
};
