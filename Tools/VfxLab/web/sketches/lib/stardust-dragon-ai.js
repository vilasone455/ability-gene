// Stardust Dragon Staff: the dragon's movement and the fight, with no drawing in it, so it runs in Node for
// checks and ports to C# as one class. The movement is Terraria 1.4.0.5's AI_121_StardustDragon (decompiled,
// read 2026-10-03), kept in Terraria's units: pixels and ticks, 60 ticks a second as in RimWorld. A step is
// one real tick and runs `pace` Terraria ticks of movement, so a slower pace keeps the same path shape.
// Lifetimes, fades, dust and the hit timer run in real time. Positions come out in cells (`px` cells per
// Terraria pixel), x east, z north; Terraria's "up" is north.
import { hash } from '../../js/standins.js';

export const Tick = 1 / 60;
// Head, attacking: speed added along the line to the target, px/tick², by distance; braking while it moves
// away; it stops adding speed within Coast x the target's hit box diagonal, so it flies through and overshoots.
const AttackAccel = [.6, .9, 1.2], AttackNear = [600, 300], AttackTop = 30, Brake = .8, BrakeDot = .25, Coast = .75;
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

const clamp01 = x => Math.max(0, Math.min(1, x));
const wrap = a => a - 2 * Math.PI * Math.round(a / (2 * Math.PI));
// The kind of piece id i: 0 head, 1 and 2 the first body pair, 3 the tail, then pairs from 4 on.
export const kindOf = i => i === 0 ? 'head' : i === 3 ? 'tail' : (i === 1 || (i >= 4 && i % 2 === 0)) ? 'body1' : 'body2';

// setup: { px, pace, t0, t1,
//   wielder: s => {x, z} (cells), wielderDown (s or Infinity),
//   casts: [{ t, at: {x, z} }]: the first summons at `at`, later ones add a pair while the dragon is out,
//   people: [{ at: s => {x, z}, ally, tough }], rules: { life, range, damage, perPiece, hitEvery, maxCasts } }
export function simulate(setup) {
  const { px, pace, t0, t1, rules } = setup, dt = Tick, n = Math.ceil((t1 - t0) / dt);
  const MaxIds = 4 + 2 * (rules.maxCasts - 1);   // ids per step: the first four, then a pair per recast
  const toPx = q => ({ x: q.x / px, z: q.z / px });
  const W = n + 1, X = new Float32Array(W * MaxIds), Z = new Float32Array(W * MaxIds), R = new Float32Array(W * MaxIds);
  const A = new Float32Array(W * MaxIds), F = new Int8Array(W * MaxIds), order = new Int8Array(W * MaxIds), count = new Uint8Array(W);
  const scaleAt = new Float32Array(W), targets = new Int8Array(W).fill(-1), live = new Uint8Array(W);
  const people = setup.people.map(c => ({ ...c, total: 0, last: -Infinity, down: Infinity, dmg: new Float32Array(W), hits: [] }));
  const where = (j, s) => people[j].at(Math.min(s, people[j].down));
  const pieces = [];                      // by id: { x, z, rot, flip, alpha, px, pz }
  let chain = [], out = false, ending = false, lifeEnd = Infinity, casts = 0, endAt = Infinity, goneAt = Infinity;
  let vx = 0, vz = 0, target = -1;
  const dust = [], hits = [], downs = [], events = [];
  const turn = 1 - Math.pow(1 - Follow, pace);

  const addDust = (s, x, z, k, id, faint) => {
    const r = (q) => hash(k, id * 13 + q, 4407);
    dust.push({ t: s, x: x * px, z: z * px, vx: (r(1) * 2 - 1) * DustSpeed, vz: (r(2) * 2 - 1) * DustSpeed, faint, seed: k * 131 + id * 7 + dust.length });
  };
  const spawn = (id, at) => { pieces[id] = { x: at.x, z: at.z, rot: 0, flip: 1, alpha: 0, fading: 1, px: at.x, pz: at.z }; };

  for (let k = 0; k <= n; k++) {
    const s = t0 + k * dt, w = toPx(setup.wielder(s));
    // Casts: the summon, then each recast adds a body pair in front of the tail at the wielder.
    for (const c of setup.casts) {
      if (c.t < s - dt / 2 || c.t >= s + dt / 2) continue;
      if (!out && !ending && casts === 0) {
        const at = toPx(c.at);
        [0, 1, 2, 3].forEach(id => spawn(id, at));
        chain = [0, 1, 2, 3]; out = true; casts = 1; lifeEnd = s + rules.life; vx = vz = 0;
        events.push({ t: s, kind: 'summon', pieces: 4 });
      } else if (out && !ending && casts < rules.maxCasts) {
        const id = 4 + 2 * (casts - 1);
        spawn(id, w); spawn(id + 1, w);
        chain.splice(chain.length - 1, 0, id, id + 1); casts++; lifeEnd = s + rules.life;
        events.push({ t: s, kind: 'grow', pieces: chain.length });
      }
    }
    if (out && !ending && (s >= lifeEnd || s >= setup.wielderDown)) {
      ending = true; endAt = s; chain.forEach(id => { pieces[id].fading = -1; });
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
          if (c.ally || c.down <= s || !inRange(j)) return;
          const q = where(j, s), d = Math.hypot(q.x - w.x * px, q.z - w.z * px);
          if (d < near) { near = d; target = j; }
        });
      }
      // The head.
      const h = pieces[chain[0]];
      if (target >= 0) {
        const q = toPx(where(target, s)), gx = q.x - h.x, gz = q.z - h.z, d = Math.hypot(gx, gz);
        const a = d < AttackNear[1] ? AttackAccel[2] : d < AttackNear[0] ? AttackAccel[1] : AttackAccel[0];
        if (d > TargetDiag * Coast) {
          vx += gx / d * a * pace; vz += gz / d * a * pace;
          if (vx * gx + vz * gz < BrakeDot) { const f = Math.pow(Brake, pace); vx *= f; vz *= f; }
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
      if (vx || vz) { h.rot = Math.atan2(vz, vx); h.flip = vx < 0 ? -1 : 1; }
      // The body: each piece turns toward its parent's angle by a share of the gap, then sits Gap behind it.
      for (let i = 1; i < chain.length; i++) {
        const P = pieces[chain[i - 1]], M = pieces[chain[i]];
        let ux = P.x - M.x, uz = P.z - M.z;
        if (P.rot !== M.rot) {
          const b = wrap(P.rot - M.rot) * turn, c = Math.cos(b), sn = Math.sin(b);
          [ux, uz] = [ux * c - uz * sn, ux * sn + uz * c];
        }
        const L = Math.hypot(ux, uz);
        if (L > 1e-6) { M.rot = Math.atan2(uz, ux); M.x = P.x - ux / L * Gap * scale; M.z = P.z - uz / L * Gap * scale; M.flip = ux < 0 ? -1 : 1; }
      }
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
        if (c.ally || c.down <= s || s - c.last < rules.hitEvery) return;
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
      X[o] = m.x * px; Z[o] = m.z * px; R[o] = m.rot; A[o] = m.alpha; F[o] = m.flip; order[k * MaxIds + i] = id;
    });
    people.forEach(c => { c.dmg[k] = c.total; });
  }
  const step = s => Math.max(0, Math.min(n, Math.round((s - t0) / dt)));
  return { dt, t0, t1, n, step, ids: MaxIds, X, Z, R, A, F, order, count, scale: scaleAt, targets, live, people, where, dust, hits, downs, events, endAt, goneAt, px };
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
