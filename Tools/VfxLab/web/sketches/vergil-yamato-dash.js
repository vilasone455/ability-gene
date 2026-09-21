// Yamato Dash — Vergil's gap closer. Direction agreed; numbers remain proposed placeholders.
// Select a walkable cell up to 8 cells away along an unobstructed straight path. Prepare for
// 0.35 s, dash in 0.15 s, then sheathe over 0.4 s. Each hostile in the 1-cell-wide path takes
// 24 Cut at 40 % armour penetration once, as the carrier passes. Allies are unharmed.
// Cooldown 8 s; no resource cost proposed. This sketch is not implemented in the game.
//
// Drawing: the carrier is the kit's shared stand-in, drawn exactly as Judgement Cut and Summoned
// Swords draw it, wearing its scabbard on the left hip at a fixed angle. Only the drawn katana turns
// with the aim (lib/vergil.js heldKatana): a dark under-edge, a steel blade with a point over the
// last 30 %, a gold guard and the hand on the grip, held at the fixed northward Chest offset. A thin
// aura stands up during the prepare, as in the other two sketches. The dash is one straight cut at
// chest height that grows behind the carrier, six short speed lines flicking past on either side as
// it goes, and three blue silhouettes of the same ellipses the carrier is drawn from, each with one
// tapering line of light. No delayed damage on the sheath click. Wounds remain on three crossed
// hostiles; an ally in the path and a hostile 0.9 cells off its centre stay unhurt. The selected cell
// centres the demonstration path. Everything lies flat and rotates in the map plane, with height
// represented only by a northward offset, so no part needs a per-facing method.
// Existing summoned swords would follow the carrier in game; this clip demonstrates the dash alone.
// Default phases: 0.30 prepare, 0.65 launch, 0.80 arrive, 1.20 sheath click, 2.20 end.
import { Color, Mathf, MeshPool } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import {
  Blue, Ice, White, Dust, EnemyColour, Ally, Chest,
  pawn, pawnLayer, carrier, heldKatana, streak, cut, hitCut, afterimage, aura, glint, whiteGlow, smooth, clamp,
} from './lib/vergil.js';

const Lead = .3, Tail = 1, Width = 1, TrailLife = .34, GhostLife = .34, Lines = 6, LineLife = .3, Ghosts = [.2, .5, .78];
const Wound = new Color(.55, .05, .05);
const times = p => ({ launch: Lead + p.warm, arrive: Lead + p.warm + p.dash,
  click: Lead + p.warm + p.dash + p.sheathe, end: Lead + p.warm + p.dash + p.sheathe + Tail });

function layout(p, o) {
  const a = p.aim * Mathf.Deg2Rad, d = { x: Math.cos(a), z: Math.sin(a) };
  const at = (along, across = 0, lift = 0) => ({
    x: o.x + d.x * (along - p.distance / 2) - d.z * across,
    z: o.z + d.z * (along - p.distance / 2) + d.x * across + lift,
  });
  const figures = [
    { fraction: .24, across: .16 }, { fraction: .5, across: -.18 },
    { fraction: .77, across: .12 }, { fraction: .62, across: 0, ally: true },
    { fraction: .44, across: .9 },
  ].map((g, i) => ({ ...g, i, pos: at(g.fraction * p.distance, g.across),
    hit: !g.ally && Math.abs(g.across) <= Width / 2 }));
  return { d, at, figures };
}

export default {
  kit: 'Vergil', label: 'Yamato Dash (sketch)',
  params: {
    aim: P('Direction (degrees)', 0, 0, 360, 15, 'Scene'),
    distance: P('Dash distance (cells)', 6, 2, 8, .5, 'Scene'),
    guides: { label: 'Show the 1-cell hit path', value: true, group: 'Scene' },
    warm: P('Prepare (s)', .35, .2, .8, .05, 'Timing'),
    dash: P('Dash (s)', .15, .1, .4, .01, 'Timing'),
    sheathe: P('Sheathe (s)', .4, .25, .8, .05, 'Timing'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Ready', t: 0 }, { name: 'Hand to hilt', t: Lead },
    { name: 'Dash and cut', t: t.launch }, { name: 'Sheathe', t: t.arrive },
    { name: 'Click', t: t.click },
  ]; },
  events(p) { const t = times(p); return [
    { t: t.launch, type: 'sound', def: 'AG_Vergil_YamatoDash' },
    { t: t.arrive, type: 'shake', value: .025 },
    { t: t.click, type: 'sound', def: 'AG_Vergil_Sheathe' },
  ]; },
  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const { at, d, figures } = layout(p, o), travel = clamp((s - t.launch) / p.dash);
    const pos = at(p.distance * travel), sun = scene?.shadowVector ?? { x: -.45, z: -.32 };
    const strength = scene?.sun?.strength ?? .32, warm = smooth((s - Lead) / p.warm);
    const finish = clamp((s - t.arrive) / p.sheathe);
    const out = s < t.launch ? 0 : s < t.arrive ? smooth(travel / .18) : 1 - smooth((finish - .2) / .8);

    if (p.guides) {
      const alpha = .22 * (1 - smooth((s - t.arrive) / .3));
      if (alpha > 0) {
        [-Width / 2, Width / 2].forEach((side, i) =>
          streak(`yamato path ${i}`, at(0, side), at(p.distance, side), .025, Blue.withAlpha(alpha), undefined, Floor + .01, 2));
        streak('yamato destination', at(p.distance, -.5), at(p.distance, .5), .04, Ice.withAlpha(alpha * 2), undefined, Floor + .012, 2);
      }
    }

    // Damage is tied to passage time, independently for each target. Scrubbing replays it exactly.
    const all = [...figures, { pos, caster: true }].sort((a, b) => b.pos.z - a.pos.z);
    all.forEach(g => {
      if (g.caster) {
        const crouch = .09 * warm * (1 - smooth(finish));
        const body = { x: pos.x, z: pos.z - crouch }, dashing = s >= t.launch && s < t.arrive;
        // The hilt of the scabbard on the left hip, where the carrier draws from and sheathes to.
        const hilt = { x: body.x - .02, z: body.z + .46 };
        if (s >= Lead && s < t.launch) aura('yamato dash', body, s, .5 * warm, Blue);
        // The hand crosses to the hip for the draw and stays there until the blade is home.
        carrier(body, sun, strength, { hand: smooth(warm * 1.5) * (1 - smooth((s - t.click) / .2)),
          tint: Ice, tintAmount: dashing ? .45 : 0 });
        if (out > .005) heldKatana('yamato', body, p.aim, out, { hot: dashing ? .85 : .25 * (1 - finish) });
        if (s >= Lead && s < t.launch) glint('yamato prepare', hilt, .06 + .12 * warm, warm, Blue, 20);
        if (s >= t.click && s < t.click + .16) { const u = (s - t.click) / .16; glint('yamato click', hilt, .3 * (1 - u) + .06, 1 - u, White, 20); }
        return;
      }
      const age = s - (t.launch + g.fraction * p.dash), hit = g.hit && age >= 0;
      const recoil = hit ? .07 * Math.sin(Math.PI * clamp(age / .2)) : 0;
      const q = { x: g.pos.x + d.x * recoil, z: g.pos.z + d.z * recoil };
      pawn(q, g.ally ? Ally : EnemyColour, sun, strength, { tint: White, tintAmount: hit ? .8 * clamp(1 - age / .1) : 0 });
      if (hit) {
        draw(MeshPool.plane10, q.x, pawnLayer + .02, q.z + .31, .28, .035, -p.aim - 45, Wound);
        hitCut(`yamato hit ${g.i}`, q, p.aim + 45, age);
      }
    });

    if (s >= t.launch) {
      const fade = 1 - smooth((s - t.arrive) / TrailLife);
      // The cut the carrier leaves in the air: one straight line at chest height, growing behind it.
      // White while the blade is still on it, cooling to blue over 0.12 s once the carrier stops.
      cut('yamato crossing', at(0, 0, Chest), at(p.distance, 0, Chest), travel, fade, { width: .022, hot: .5 * (1 - smooth((s - t.arrive) / .12)) });
      // Speed lines: short streaks flicking past on either side, each thrown as the carrier passes it.
      for (let i = 0; i < Lines; i++) {
        const u = (i + .5) / Lines, age = s - (t.launch + u * p.dash);
        if (age < 0 || age >= LineLife) continue;
        const f = 1 - age / LineLife, side = (i % 2 ? 1 : -1) * (.2 + .34 * rand(i)), back = u * p.distance - .35 - 1.1 * (1 - f);
        streak(`yamato line ${i}`, at(back, side, Chest), at(back + .5 + 1.1 * f, side, Chest), .045, Ice.withAlpha(.5 * f * f), whiteGlow, Y + .028, 3);
      }
      Ghosts.forEach((u, i) => afterimage(`yamato ghost ${i}`, at(p.distance * u), at(0), s - (t.launch + u * p.dash), GhostLife));
      // Small ground dust at the departure, under each footfall along the path, and at the braking
      // foot; no expanding impact rings.
      [0, 1].forEach((end, i) => {
        const age = s - (end ? t.arrive : t.launch), u = age / .3;
        if (u < 0 || u > 1) return;
        [-1, 1].forEach(side => {
          const q = at(end * p.distance - .25 * u, side * (.16 + .25 * u));
          sprite(q, .24 + .35 * u, .16 + .2 * u, Dust.withAlpha(.35 * Math.sin(Math.PI * u)), soft, Floor + .025 + i * .001);
        });
      });
      for (let i = 0; i < Lines; i++) {
        const u = (i + .5) / Lines, age = s - (t.launch + u * p.dash), v = age / .45;
        if (v < 0 || v > 1) continue;
        sprite(at(u * p.distance - .3 * v, (rand(i + 20) - .5) * .5), .3 + .5 * v, .2 + .32 * v, Dust.withAlpha(.26 * Math.sin(Math.PI * v)), soft, Floor + .022);
      }
    }
  },
};
