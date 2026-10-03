// Stardust Dragon Staff: the dragon's movement and the fight, with no drawing in it, so it runs in Node for
// checks and ports to C# as one class. The movement is Terraria 1.4.0.5's AI_121_StardustDragon (decompiled,
// read 2026-10-03), kept in Terraria's units: pixels and ticks, 60 ticks a second as in RimWorld. A step is
// one real tick and runs `pace` Terraria ticks of movement, so a slower pace keeps the same path shape.
// Lifetimes, fades, dust and the hit timer run in real time. Positions come out in cells (`px` cells per
// Terraria pixel), x east, z north; Terraria's "up" is north.
import { hash } from '../../js/standins.js';
import { rayWall } from './terraria.js';

export const Tick = 1 / 60;
// Head, attacking: speed added along the line to the target, px/tick², by distance; braking while it moves
// away; it stops adding speed within Coast x the target's hit box diagonal, so it flies through and overshoots.
const AttackAccel = [.6, .9, 1.2], AttackNear = [600, 300], AttackTop = 30, Brake = .8, BrakeDot = .25, Coast = .75;
// Not Terraria's: past the target the head coasts on Overshoot px before it brakes, and while it moves away it is
// pulled sideways by LoopPull of its speed-up, toward the side it is already drifting to, so each pass ends in a loop
// clear of the target. Terraria's targets walk and fall, so its passes come back on curves; a pawn standing still
// would make the head reverse on its own line, right at the pawn, and fold the body back on itself. Checked over 16
// layouts (2026-10-03): every pawn still goes down; a pull of .7 or a brake weaker than Terraria's .8 makes the head
// orbit a pawn that stands still without passing through it.
const Overshoot = 54, LoopPull = .55;
// Head, idle: speed added toward the wielder on each axis separately, px/tick², by distance; dead zones
// across and up and down; slows inside Settle; a pull north (Terraria's up) while it is slow north-south.
const IdleAccel = [.2, .12, .06], IdleNear = [200, 140], DeadX = 20, DeadZ = 10, Settle = 100, IdleDamp = .96, DampAbove = 2;
const Lift = .1, LiftBelow = 1, IdleTop = 15;
// Body: each piece turns this share of the gap to its parent's angle per tick, then sits Gap x scale behind it.
const Gap = 16, Follow = .1, Grow = .01, GrowCap = 50;
const HitHalf = 15;                       // a piece's hit box is 30 x scale px square
const TargetDiag = Math.hypot(18, 40);    // a humanoid's hit box in Terraria (a zombie, 18 x 40 px), for the coast rule
const PawnHalf = 9;                       // px: half a humanoid's width, added to a piece's reach
const FadeStep = 42 / 255;                // a summoned piece goes from invisible to full in 7 ticks; ending mirrors it
const DustChance = 1 / 30, FadeDust = 2;  // per piece per tick; dust per tick while a piece fades
const DustSpeed = 2;                      // px/tick: Dust.NewDust gives each dust up to this much drift on each axis
const FlipSlack = Math.sin(10 * Math.PI / 180);   // a piece mirrors once its heading is 10 degrees past north or south (Terraria: at 0)
const ShootSpeed = 10;                    // px/tick: the staff's "velocity 10", the head's speed when it is summoned

// Stardust Flame (not Terraria's; agreed with the user 2026-10-03): the dragon breathes starfire at its target and
// pays with its body, one piece at a time from the back. Burning and the flame run in real time; the head's
// movement while it breathes runs at `pace` like the rest.
export const LeastPieces = 4;             // a breath stops at head, one body pair and tail (Terraria's first summon); Last Breath goes on
export const Hold = { tap: .25, step: .3 };   // Shared/Command_TapHold's tap (s); seconds of hold per mark on the button
const D2R = Math.PI / 180;
// The flame leaves the open mouth along the line halfway between the jaws: the jaw opens BreathJaw toward the belly,
// so that line runs Tilt below the head's, and the head turns Tilt above its target to aim it (user's note, 2026-10-03:
// along the head's line the flame left the upper lip and missed the mouth). MouthAt: where it leaves, px in the
// head's frame (x to the snout, y to the spine; the middle of the mouth open BreathJaw, the head drawn 15 % bigger).
export const BreathJaw = 40;              // degrees
const Tilt = BreathJaw / 2 * D2R, MouthAt = [22, -11];
const FlameOn = 20 * D2R, FlameKeep = 35 * D2R, KeepReach = 1.4;   // the flame starts once the head faces its target within
                                          // FlameOn and the target is within reach; it stays on within FlameKeep and KeepReach x reach
const FlameRise = .1, FlameFall = .15;    // seconds the flame takes to come up and to die down
const Stand = .55;                        // while it breathes the head hangs this share of the flame's reach from its target,
const Orbit = .008;                       // circling it by this many radians a Terraria tick, so the flame sweeps
const Approach = .06, HoverTop = 12, HoverBlend = .12, FaceTurn = .1;   // its speed: Approach x the gap to its spot, at most
                                          // HoverTop px/tick, HoverBlend of the change taken a Terraria tick; it turns FaceTurn radians a tick
const NoTarget = 1;                       // seconds a breath waits with no target before it ends; the pieces not burned are kept
const Recover = .3;                       // seconds a half-burned piece takes to come back when its breath is cut short
const PulseEvery = .5;                    // seconds between the pulses of light that run up the body while a piece burns
const LastWait = 1;                       // Last Breath: seconds after the order its flame starts even if the head is not facing the target
const DiveSpeed = 24, DiveHit = 20, DiveMax = .8;   // the head alone dives at its target at px/tick; bursts within DiveHit px or after DiveMax s
const LastReach = .25, LastDamage = .1, LastSpread = D2R;   // Last Breath, per piece burned: cells of reach, share of burn, half-angle
const IgniteCone = .06, IgniteBurst = .5, IgnitePawn = .25;   // chances: a cell under Last Breath's flame per burn tick, a cell in the
                                          // burst, a pawn either of them hits

const clamp01 = x => Math.max(0, Math.min(1, x));
const wrap = a => a - 2 * Math.PI * Math.round(a / (2 * Math.PI));
// The kind of piece id i: 0 head, 1 and 2 the first body pair, 3 the tail, then pairs from 4 on.
export const kindOf = i => i === 0 ? 'head' : i === 3 ? 'tail' : (i === 1 || (i >= 4 && i % 2 === 0)) ? 'body1' : 'body2';

// setup: { px, pace, t0, t1,
//   wielder: s => {x, z} (cells), wielderDown (s or Infinity),
//   casts: [{ t, at: {x, z} }]: the first summons at `at`, later ones add a pair while the dragon is out,
//   breaths: [{ from, t }]: the Stardust Flame button pressed at `from` and let go at `t`,
//   cells: wall cells {x, z}, which stop the flame (the dragon flies through them),
//   people: [{ at: s => {x, z}, ally, tough, arrive (s, default always there) }], rules: { life, range, damage, perPiece, hitEvery, most, firstPairs,
//   breath: { perPiece, damage, every, reach, reachPer, half, lastPer, burstPer, burstRadius } } }
// firstPairs: body pairs the first cast brings (Terraria: 1, so 4 pieces); every later cast adds one more pair while the
// dragon has fewer than `most` pieces, so pieces the flame burned grow back.
export function simulate(setup) {
  const { px, pace, t0, t1, rules } = setup, dt = Tick, n = Math.ceil((t1 - t0) / dt), B = rules.breath;
  const orders = setup.breaths ?? [], walls = setup.cells ?? [];
  const pairs0 = rules.firstPairs ?? 1, MaxIds = 2 + 2 * (pairs0 + setup.casts.length);   // ids per step: head, tail and every pair a cast can bring
  const toPx = q => ({ x: q.x / px, z: q.z / px });
  const W = n + 1, X = new Float32Array(W * MaxIds), Z = new Float32Array(W * MaxIds), R = new Float32Array(W * MaxIds);
  const A = new Float32Array(W * MaxIds), F = new Int8Array(W * MaxIds), FT = new Float32Array(W * MaxIds), order = new Int8Array(W * MaxIds), count = new Uint8Array(W);
  const Bn = new Float32Array(W * MaxIds);
  const scaleAt = new Float32Array(W), targets = new Int8Array(W).fill(-1), live = new Uint8Array(W);
  // The breath per step: flame 0..1, reach (cells), half-angle, heat (Last Breath's share burned), the mouth (cells, radians).
  const flame = new Float32Array(W), reachAt = new Float32Array(W), spreadAt = new Float32Array(W), heatAt = new Float32Array(W);
  const mouthX = new Float32Array(W), mouthZ = new Float32Array(W), mouthR = new Float32Array(W), breathing = new Uint8Array(W);
  const people = setup.people.map(c => ({ ...c, arrive: c.arrive ?? -Infinity, total: 0, last: -Infinity, lastBurn: -Infinity, down: Infinity, fire: Infinity, dmg: new Float32Array(W), hits: [], burns: [] }));
  const where = (j, s) => people[j].at(Math.min(s, people[j].down));
  const foe = (c, s) => !c.ally && c.down > s && s >= c.arrive;   // a standing enemy that has arrived
  const pieces = [];                      // by id: { x, z, rot, flip, alpha, burn, px, pz }
  let chain = [], out = false, ending = false, lifeEnd = Infinity, casts = 0, pairs = 0, endAt = Infinity, goneAt = Infinity;
  let vx = 0, vz = 0, target = -1, breath = null, burst = null;
  const dust = [], hits = [], downs = [], events = [], pulses = [], fires = [], lit = new Set();
  const turn = 1 - Math.pow(1 - Follow, pace);
  const reachOf = () => breath.last ? breath.reach0 + LastReach * breath.burned : B.reach + B.reachPer * (chain.length - LeastPieces);
  const halfOf = () => B.half + (breath.last ? LastSpread * breath.burned : 0);
  // The mouth and the flame's line for head h: the belly is to the right of its line of flight when it is not mirrored.
  const mouthOf = (h, scale) => {
    const c = Math.cos(h.rot), sn = Math.sin(h.rot), [lx, ly] = [MouthAt[0], MouthAt[1] * h.flip];
    return { x: h.x + (c * lx - sn * ly) * scale, z: h.z + (sn * lx + c * ly) * scale };
  };
  const flameRot = h => h.rot - Tilt * h.flip;
  const wallAt = new Set(walls.map(c => `${c.x},${c.z}`));
  const ignite = (s, x, z) => { const key = `${x},${z}`; if (wallAt.has(key) || lit.has(key)) return; lit.add(key); fires.push({ t: s, x, z }); };
  // Burn on one pawn: down at its tough, set alight by Last Breath (a picture: RimWorld's fire does the rest).
  const scorch = (c, j, s, amount, k, alight) => {
    c.total += amount; c.burns.push({ t: s });
    if (alight && c.fire === Infinity && hash(k, j, 4412) < IgnitePawn) c.fire = s;
    if (c.total >= c.tough && c.down === Infinity) { c.down = s; downs.push({ t: s, j }); }
  };

  const addDust = (s, x, z, k, id, faint) => {
    const r = (q) => hash(k, id * 13 + q, 4407);
    dust.push({ t: s, x: x * px, z: z * px, vx: (r(1) * 2 - 1) * DustSpeed, vz: (r(2) * 2 - 1) * DustSpeed, faint, seed: k * 131 + id * 7 + dust.length });
  };
  const spawn = (id, at, rot) => { pieces[id] = { x: at.x, z: at.z, rot, flip: Math.cos(rot) < 0 ? -1 : 1, flipAt: -99, alpha: 0, fading: 1, burn: 0, px: at.x, pz: at.z }; };
  // Terraria mirrors a piece by the sign of its x heading, so the gold stays north; here with a little slack.
  const mirror = (m, s) => { const c = Math.cos(m.rot); if (m.flip * c < -FlipSlack) { m.flip = -m.flip; m.flipAt = s; } };

  // One step of a breath, after the head and body have moved: the flame on or off, the piece it costs, the burn it
  // deals, and Last Breath's dive and burst.
  const breathe = (s, k, h, scale) => {
    const b = breath, R = reachOf() / px, m = mouthOf(h, scale), half = halfOf();
    let d = Infinity, off = Math.PI;
    const fr = flameRot(h);
    if (target >= 0) { const q = toPx(where(target, s)); d = Math.hypot(q.x - m.x, q.z - m.z); off = Math.abs(wrap(Math.atan2(q.z - m.z, q.x - m.x) - fr)); }
    if (b.last) b.on = b.on || (off < FlameOn && d < R) || s - b.began >= LastWait;
    else b.on = !b.done && (b.on ? off < FlameKeep && d < R * KeepReach : off < FlameOn && d < R);
    b.strength = clamp01(b.strength + (b.on ? dt / FlameRise : -dt / FlameFall));
    if (!b.last) { b.idle = target < 0 ? b.idle + dt : 0; if (b.idle >= NoTarget) b.done = true; }
    // The piece it costs, only while the flame is out: the last body piece, then (Last Breath) the tail.
    if (b.on && b.dive === null) {
      if (!chain.includes(b.piece)) b.piece = chain.length > 2 ? chain[chain.length - 2] : chain.length === 2 ? chain[1] : -1;
      const p = pieces[b.piece];
      if (p) {
        const spot = q => [p.x + (hash(k, b.piece * 9 + q, 4411) - .5) * 2 * HitHalf * scale, p.z + (hash(k, b.piece * 9 + q, 4413) - .5) * 2 * HitHalf * scale];
        p.burn = Math.min(1, p.burn + dt / (b.last ? B.lastPer : B.perPiece));
        if (s >= b.pulse) { pulses.push({ t: s, big: false }); b.pulse = s + PulseEvery; }
        if (hash(k, b.piece, 4410) < .3) addDust(s, ...spot(0), k * 3 + 1, b.piece, false);
        if (p.burn >= 1) {
          for (let q = 1; q <= 8; q++) addDust(s, ...spot(q), k * 3 + q, b.piece, false);
          chain.splice(chain.indexOf(b.piece), 1); p.alpha = 0;
          pulses.push({ t: s, big: true }); b.burned++; b.left--; b.piece = -1;
          events.push({ t: s, kind: 'burn', pieces: chain.length });
          if (!b.last && (b.left <= 0 || chain.length <= LeastPieces)) b.done = true;
          if (b.last && chain.length === 1) b.dive = s;
        }
      }
    }
    // The burn: every standing enemy in the cone, once per `every`, unless a wall is in the way. Last Breath burns
    // harder with each piece it has spent and sets cells under its flame alight.
    if (b.strength > .5) {
      const amount = B.damage * (b.last ? 1 + LastDamage * b.burned : 1), mc = { x: m.x * px, z: m.z * px };
      people.forEach((c, j) => {
        if (!foe(c, s) || s - c.lastBurn < B.every) return;
        const q = toPx(where(j, s)), dx = q.x - m.x, dz = q.z - m.z, dd = Math.hypot(dx, dz), a = Math.atan2(dz, dx);
        if (dd > R || Math.abs(wrap(a - fr)) > half + Math.atan2(PawnHalf, dd)) return;
        if (rayWall(mc, a, walls, dd * px) < dd * px) return;
        c.lastBurn = s; scorch(c, j, s, amount, k, b.last);
      });
      if (b.last && s - b.tick >= B.every) {
        b.tick = s;
        const rc = R * px;
        for (let x = Math.floor(mc.x - rc); x <= Math.ceil(mc.x + rc); x++) for (let z = Math.floor(mc.z - rc); z <= Math.ceil(mc.z + rc); z++) {
          const dx = x - mc.x, dz = z - mc.z, dd = Math.hypot(dx, dz), a = Math.atan2(dz, dx);
          if (dd < .8 || dd > rc || Math.abs(wrap(a - fr)) > half || rayWall(mc, a, walls, dd) < dd - .5) continue;
          if (hash(k, x * 131 + z, 4416) < IgniteCone) ignite(s, x, z);
        }
      }
    }
    // Last Breath's end: the head alone dives at its target, glowing whiter, and bursts on it, or where it is after DiveMax.
    if (b.dive !== null) {
      h.burn = clamp01((s - b.dive) / DiveMax);
      const q = target >= 0 ? toPx(where(target, s)) : null;
      if (s - b.dive >= DiveMax || (q && Math.hypot(q.x - h.x, q.z - h.z) < DiveHit)) {
        const at = { x: h.x * px, z: h.z * px }, radius = B.burstRadius;
        people.forEach((c, j) => {
          const p = where(j, s);
          if (foe(c, s) && Math.hypot(p.x - at.x, p.z - at.z) <= radius) scorch(c, j, s, B.burstPer * b.from, k, true);
        });
        for (let x = Math.floor(at.x - radius); x <= Math.ceil(at.x + radius); x++) for (let z = Math.floor(at.z - radius); z <= Math.ceil(at.z + radius); z++)
          if (Math.hypot(x - at.x, z - at.z) <= radius && hash(k, x * 131 + z, 4417) < IgniteBurst) ignite(s, x, z);
        burst = { t: s, x: at.x, z: at.z, radius };
        events.push({ t: s, kind: 'burst', pieces: b.from });
        h.alpha = 0; chain = []; out = false; goneAt = s; target = -1; breath = null;
        return;
      }
    }
    if (b.done && b.strength <= 0) { breath = null; events.push({ t: s, kind: 'breath end', pieces: chain.length }); }
  };

  for (let k = 0; k <= n; k++) {
    const s = t0 + k * dt, w = toPx(setup.wielder(s));
    // Casts: the summon, then each recast adds a body pair in front of the tail at the wielder.
    for (const c of setup.casts) {
      if (c.t < s - dt / 2 || c.t >= s + dt / 2) continue;
      if (!out && !ending && casts === 0) {
        // The summon: the head leaves the cast cell at the staff's speed, heading on from the wielder, the body laid out
        // behind it. (Terraria puts every piece on the cursor and the chain unrolls in a tick or two; here it starts
        // unrolled, as the demo GIF shows it a tenth of a second in.)
        const at = toPx(c.at), more = Array.from({ length: 2 * (pairs0 - 1) }, (_, i) => 4 + i);
        let dx = at.x - w.x, dz = at.z - w.z; const L = Math.hypot(dx, dz) || 1; dx /= L; dz /= L;
        const rot = Math.atan2(dz, dx), size = 1 + Grow * Math.min(GrowCap, 2 * pairs0 + 1);
        chain = [0, 1, 2, ...more, 3];
        chain.forEach((id, i) => spawn(id, { x: at.x - dx * Gap * size * i, z: at.z - dz * Gap * size * i }, rot));
        out = true; casts = 1; pairs = pairs0; lifeEnd = s + rules.life; vx = dx * ShootSpeed; vz = dz * ShootSpeed;
        events.push({ t: s, kind: 'summon', pieces: chain.length });
      } else if (out && !ending && chain.length + 2 <= rules.most && !breath?.last) {
        // A recast: the new pair takes the tail's place and the next one back along its line, the tail moves two
        // places back. (Terraria spawns the pair on the wielder and it joins the chain the next tick.)
        const id = 2 + 2 * pairs, t = pieces[chain[chain.length - 1]], size = 1 + Grow * Math.min(GrowCap, chain.length + 1);
        const bx = -Math.cos(t.rot) * Gap * size, bz = -Math.sin(t.rot) * Gap * size;
        spawn(id, { x: t.x, z: t.z }, t.rot); spawn(id + 1, { x: t.x + bx, z: t.z + bz }, t.rot);
        t.x += 2 * bx; t.z += 2 * bz; t.px = t.x; t.pz = t.z;
        chain.splice(chain.length - 1, 0, id, id + 1); casts++; pairs++; lifeEnd = s + rules.life;
        events.push({ t: s, kind: 'grow', pieces: chain.length });
      }
    }
    // Breath orders: a tap burns one piece; a hold one per mark it reached (the button has one mark per piece above
    // LeastPieces); held past the top mark, Last Breath. Refused while it breathes or with no target.
    for (const o of orders) {
      if (o.t < s - dt / 2 || o.t >= s + dt / 2) continue;
      const spare = chain.length - LeastPieces, held = o.t - o.from, fill = held < Hold.tap ? 0 : (held - Hold.tap) / (Hold.step * (spare + 1));
      const last = fill >= 1, want = held < Hold.tap ? 1 : Math.max(1, Math.min(spare, Math.ceil(fill * (spare + 1))));
      const why = !out || ending ? 'no dragon' : breath ? 'breathing' : target < 0 ? 'no target' : !last && spare < 1 ? 'too short' : '';
      if (why) { events.push({ t: s, kind: 'refused', why }); continue; }
      breath = { last, left: last ? Infinity : want, from: chain.length, began: s, on: false, done: false, strength: 0, idle: 0, burned: 0,
        piece: -1, pulse: s, phi: null, dir: 1, dive: null, tick: -Infinity, reach0: B.reach + B.reachPer * (chain.length - LeastPieces) };
      events.push({ t: s, kind: last ? 'last breath' : 'breath', pieces: last ? chain.length : want, tap: held < Hold.tap });
    }
    if (out && !ending && (s >= lifeEnd || s >= setup.wielderDown)) {
      ending = true; endAt = s; breath = null; chain.forEach(id => { pieces[id].fading = -1; });
      events.push({ t: s, kind: s >= setup.wielderDown ? 'wielder down' : 'time up', pieces: chain.length });
    }
    const scale = 1 + Grow * Math.min(GrowCap, Math.max(0, chain.length - 1));

    if (out) {
      // The target: kept while it stands and is in range of the wielder, else the nearest standing enemy in range.
      const inRange = j => { const q = where(j, s); return Math.hypot(q.x - w.x * px, q.z - w.z * px) <= rules.range; };
      if (target >= 0 && (people[target].down <= s || !inRange(target))) target = -1;
      if (target < 0 && !ending) {
        let near = Infinity;
        people.forEach((c, j) => {
          if (!foe(c, s) || !inRange(j)) return;
          const q = where(j, s), d = Math.hypot(q.x - w.x * px, q.z - w.z * px);
          if (d < near) { near = d; target = j; }
        });
      }
      // The head. While it breathes it hangs in front of its target, circling it, and turns to face it; Last Breath's
      // head, alone, dives at it.
      const h = pieces[chain[0]];
      let face = null;
      if (breath && target >= 0) {
        const q = toPx(where(target, s));
        if (breath.dive !== null) {
          const gx = q.x - h.x, gz = q.z - h.z, d = Math.hypot(gx, gz) || 1;
          vx = gx / d * DiveSpeed; vz = gz / d * DiveSpeed;
        } else {
          if (breath.phi === null) { breath.phi = Math.atan2(h.z - q.z, h.x - q.x); breath.dir = Math.sign(vx * (q.z - h.z) - vz * (q.x - h.x)) || 1; }
          breath.phi += Orbit * pace * breath.dir;
          const stand = Stand * reachOf() / px, sx = q.x + Math.cos(breath.phi) * stand, sz = q.z + Math.sin(breath.phi) * stand;
          let wx = (sx - h.x) * Approach, wz = (sz - h.z) * Approach;
          const wl = Math.hypot(wx, wz); if (wl > HoverTop) { wx *= HoverTop / wl; wz *= HoverTop / wl; }
          const f = 1 - Math.pow(1 - HoverBlend, pace); vx += (wx - vx) * f; vz += (wz - vz) * f;
        }
        // Aim the flame's line from the mouth at the target. The head mirrors by where the flame points, not by its own
        // line, which is Tilt off it: by its own line it would mirror back and forth with a target to the north or south.
        const m = mouthOf(h, scale), bearing = Math.atan2(q.z - m.z, q.x - m.x);
        if (h.flip * Math.cos(bearing) < -FlipSlack) { h.flip = -h.flip; h.flipAt = s; }
        face = bearing + Tilt * h.flip;
      } else if (breath) {
        const f = Math.pow(IdleDamp, pace); vx *= f; vz *= f;
      } else if (target >= 0) {
        const q = toPx(where(target, s)), gx = q.x - h.x, gz = q.z - h.z, d = Math.hypot(gx, gz);
        const a = d < AttackNear[1] ? AttackAccel[2] : d < AttackNear[0] ? AttackAccel[1] : AttackAccel[0];
        const away = vx * gx + vz * gz < 0;
        if (d > TargetDiag * Coast && !(away && d < Overshoot)) {
          vx += gx / d * a * pace; vz += gz / d * a * pace;
          const along = vx * gx + vz * gz;
          if (along < BrakeDot) {
            const f = Math.pow(Brake, pace), side = Math.sign(vz * gx - vx * gz) || 1;   // which side of the line it drifts to
            vx *= f; vz *= f;
            if (along < 0) { vx += -gz / d * side * a * LoopPull * pace; vz += gx / d * side * a * LoopPull * pace; }
          }
        }
        const v = Math.hypot(vx, vz); if (v > AttackTop) { vx *= AttackTop / v; vz *= AttackTop / v; }
      } else {
        const gx = w.x - h.x, gz = w.z - h.z, d = Math.hypot(gx, gz);
        const a = d < IdleNear[1] ? IdleAccel[2] : d < IdleNear[0] ? IdleAccel[1] : IdleAccel[0];
        if (d > Settle) {
          if (Math.abs(gx) > DeadX) vx += a * Math.sign(gx) * pace;
          if (Math.abs(gz) > DeadZ) vz += a * Math.sign(gz) * pace;
        } else if (Math.hypot(vx, vz) > DampAbove) { const f = Math.pow(IdleDamp, pace); vx *= f; vz *= f; }
        if (Math.abs(vz) < LiftBelow) vz += Lift * pace;
        const v = Math.hypot(vx, vz); if (v > IdleTop) { vx *= IdleTop / v; vz *= IdleTop / v; }
      }
      chain.forEach(id => { pieces[id].px = pieces[id].x; pieces[id].pz = pieces[id].z; });
      h.x += vx * pace; h.z += vz * pace;
      if (breath) { if (face !== null) h.rot += Math.max(-FaceTurn * pace, Math.min(FaceTurn * pace, wrap(face - h.rot))); }
      else if (vx || vz) { h.rot = Math.atan2(vz, vx); mirror(h, s); }
      // The body: each piece turns toward its parent's angle by a share of the gap, then sits Gap behind it. A piece
      // that burns closes up: the gaps either side of it shrink to half, so the tail meets the piece in front as it goes.
      for (let i = 1; i < chain.length; i++) {
        const P = pieces[chain[i - 1]], M = pieces[chain[i]];
        let ux = P.x - M.x, uz = P.z - M.z;
        if (P.rot !== M.rot) {
          const b = wrap(P.rot - M.rot) * turn, c = Math.cos(b), sn = Math.sin(b);
          [ux, uz] = [ux * c - uz * sn, ux * sn + uz * c];
        }
        const L = Math.hypot(ux, uz), gap = Gap * scale * (1 - (M.burn + P.burn) / 2);
        if (L > 1e-6) { M.rot = Math.atan2(uz, ux); M.x = P.x - ux / L * gap; M.z = P.z - uz / L * gap; mirror(M, s); }
      }
      chain.forEach(id => { const m = pieces[id]; if (m.burn > 0 && (!breath || breath.done || id !== breath.piece)) m.burn = Math.max(0, m.burn - dt / Recover); });
      if (breath) breathe(s, k, h, scale);
      // Fades and dust.
      chain.forEach(id => {
        const m = pieces[id];
        if (m.fading) {
          m.alpha = clamp01(m.alpha + m.fading * FadeStep);
          for (let q = 0; q < FadeDust; q++) addDust(s, m.x + (hash(k, id * 5 + q, 4401) - .5) * 2 * HitHalf * scale, m.z + (hash(k, id * 5 + q, 4402) - .5) * 2 * HitHalf * scale, k * 3 + q, id, true);
          if (m.fading > 0 && m.alpha >= 1) m.fading = 0;
        }
        if (m.alpha > 0 && hash(k, id, 4400) < DustChance) addDust(s, m.x + (hash(k, id, 4403) - .5) * 2 * HitHalf * scale, m.z + (hash(k, id, 4404) - .5) * 2 * HitHalf * scale, k * 3 + 2, id, false);
      });
      // Hits: any piece whose move this step passes within reach of a standing enemy, once per hitEvery per pawn.
      const reach = HitHalf * scale + PawnHalf, damage = rules.damage * (1 + rules.perPiece * (chain.length - 1));
      people.forEach((c, j) => {
        if (!foe(c, s) || s - c.last < rules.hitEvery) return;
        const q = toPx(where(j, s));
        for (const id of chain) {
          const m = pieces[id]; if (m.alpha < .5) continue;
          const ex = m.x - m.px, ez = m.z - m.pz, l2 = ex * ex + ez * ez;
          const u = l2 > 0 ? clamp01(((q.x - m.px) * ex + (q.z - m.pz) * ez) / l2) : 0, cx = m.px + ex * u, cz = m.pz + ez * u;
          if (Math.hypot(q.x - cx, q.z - cz) > reach) continue;
          c.last = s; c.total += damage; c.hits.push({ t: s, x: cx * px, z: cz * px, rot: m.rot });
          hits.push({ t: s, j });
          for (let e = 0; e < 3; e++) addDust(s, cx + (hash(k, j * 7 + e, 4405) - .5) * 20, cz + (hash(k, j * 7 + e, 4406) - .5) * 20, k * 3 + e, 90 + j, false);
          if (c.total >= c.tough) { c.down = s; downs.push({ t: s, j }); }
          break;
        }
      });
      if (ending && chain.every(id => pieces[id].alpha <= 0)) { out = false; goneAt = s; chain = []; target = -1; }
    }
    // Record the step.
    count[k] = chain.length; scaleAt[k] = scale; targets[k] = out && !ending ? target : -1; live[k] = out ? 1 : 0;
    chain.forEach((id, i) => {
      const o = k * MaxIds + id, m = pieces[id];
      X[o] = m.x * px; Z[o] = m.z * px; R[o] = m.rot; A[o] = m.alpha; F[o] = m.flip; FT[o] = m.flipAt; Bn[o] = m.burn; order[k * MaxIds + i] = id;
    });
    flame[k] = breath ? breath.strength : 0; breathing[k] = breath ? (breath.last ? 2 : 1) : 0;
    if (breath && chain.length) {
      const h = pieces[chain[0]], m = mouthOf(h, scale);
      mouthX[k] = m.x * px; mouthZ[k] = m.z * px; mouthR[k] = flameRot(h); reachAt[k] = reachOf(); spreadAt[k] = halfOf();
      heatAt[k] = breath.last ? breath.burned / Math.max(1, breath.from - 1) : 0;
    }
    people.forEach(c => { c.dmg[k] = c.total; });
  }
  const step = s => Math.max(0, Math.min(n, Math.round((s - t0) / dt)));
  return { dt, t0, t1, n, step, ids: MaxIds, X, Z, R, A, F, FT, burn: Bn, order, count, scale: scaleAt, targets, live, people, where, dust, hits, downs, events,
    endAt, goneAt, px, flame, reach: reachAt, spread: spreadAt, heat: heatAt, mouthX, mouthZ, mouthR, breathing, pulses, fires, burst };
}

// The live dust at time s: dust is born in time order, so a binary search finds the first one young enough.
export function liveDust(r, s, life) {
  let lo = 0, hi = r.dust.length;
  while (lo < hi) { const m = (lo + hi) >> 1; if (r.dust[m].t < s - life) lo = m + 1; else hi = m; }
  const out = [];
  for (let i = lo; i < r.dust.length && r.dust[i].t <= s; i++) out.push(r.dust[i]);
  return out;
}

// Where a dust has drifted after age seconds: its drift slows by 8 % a tick (Terraria's noGravity dust).
export function dustAt(d, age, px) {
  const ticks = age * 60, travel = (1 - Math.pow(.92, ticks)) / .08;
  return { x: d.x + d.vx * travel * px, z: d.z + d.vz * travel * px };
}
