// Vector manipulation, the Apply moment — VFX proposal for the Accelerator kit, not the game.
// Source/RimArt/Vector has the mechanic (VectorEditSession.Apply); it draws nothing when Apply is
// pressed. This is what would be drawn then.
//
// The mechanic, as built (numbers from VectorEditDefaults; placeholders for XML).
//   Pressing the button pauses the game and catches every round that will pass within 12 cells in
//   the next 0.35 s. The player sorts them into up to 4 groups and gives each a rotation (-180 to
//   180 degrees) and a force (x0.25, x0.5, x1, x2). Apply rewrites every round in a changed group
//   where it was caught: new heading, speed and damage x force, range 20 x force cells. Rounds in
//   no group fly on as they were. Strain +8 / 24 / 48 / 80 % for 1-4 groups changed, cooldown 5 s,
//   and the game resumes.
//
// The Apply VFX (proposed 2026-09-28, white trails agreed):
//   Corner: at each changed round a white stroke with a black edge comes in along the old heading
//     and bends onto the new one, and a white jagged star flashes on the corner (the anime draws
//     its reflections as white jagged bursts). The bend is the rotation.
//   Trail: a white trail with a black edge behind each changed round, 2 x force cells long
//     (0.5 / 1 / 2 / 4), so the force can be read from it. The round itself keeps its own look.
//   Strain: a white ring with a black edge out of his head, larger with the strain spent.
//   Reach: the 12-cell ring flashes once, faint, and fades.
//   Rounds in no group get none of this.
//
// Order, with the defaults (volley from the east, all 4 groups changed):
//   0.00  three shooters 9 cells out fire; 8 rounds come in at 42 cells/s
//   0.50  paused: the panel's lines are drawn as a stand-in for the game's own (group-coloured
//         circle on each grouped round and a line to where it will stop, a grey line for the
//         heading it came in on, the 12-cell ring)
//   1.30  Apply: the panel lines go; corners and stars on the 7 grouped rounds; the strain ring
//         (80 %); the reach ring flashes; a small camera shake. Group 1 (3 rounds) turns 180 and
//         goes back at the middle shooter at x1; group 2 (2 rounds) turns 110 at x0.5 and drops at
//         10 cells; group 3 (1 round) goes back at the south shooter at x2; group 4 (1 round)
//         turns -70 at x0.25 and drops at 5 cells. The eighth round is in no group: it passes
//         beside him with no corner and no trail. Slide "Groups changed" down and the rounds of
//         the groups left out fly on as they were; most of them hit Accelerator.
//   Each trail's tail follows its round in at the same speed after it stops.
//
// Palette: monochrome, see lib/accelerator.js; the group colours stay in the paused panel only.
// Rounds are drawn with a yellow stand-in for the vanilla bullet graphic.
// Drawing: everything flies at chest height as flat lines, and the rings are level circles, so it
// turns with the volley direction and needs no per-facing method. Pawns and rifles are stand-ins.
import { Color, Mathf, MeshPool } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import { pawn, ringAt, line, streak, whiteGlow, EnemyColour, White, Dust, Chest, pawnLayer, clamp, smooth } from './lib/goku.js';
import { accelerator, Edge } from './lib/accelerator.js';

const D2R = Mathf.Deg2Rad;
// Decided values (the mechanic's from VectorEditDefaults). The panel keeps what shows the mechanic.
const Speed = 42, Reach = 12, BaseRange = 20, Freeze = .5, Hold = .8, Apply = Freeze + Hold, Tail = .6;
const OnLine = .45, Strain = [.08, .24, .48, .8], TrailPerForce = 2, UnchangedRange = 20;
const Bullet = new Color(1, .86, .5), Rifle = new Color(.2, .19, .18), Grey = new Color(.6, .6, .6);
const GroupColours = [new Color(.35, .85, 1), new Color(1, .65, .25), new Color(1, .45, .85), new Color(.45, .95, .5)];
// In the volley's frame: x toward the shooters, z across. Shooters sit at x = distance.
const Shooters = [2.4, 0, -2.2];
// Rounds: [shooter, cells before the point it was aimed at when caught, across at Accelerator, group 1-4 or 0].
const Rounds = [[1, 3.2, .1, 1], [1, 4.6, -.15, 1], [1, 6, .2, 1], [0, 2.8, .2, 2], [0, 4.4, -.1, 2], [2, 3.6, .15, 3], [2, 5.5, -.2, 4], [2, 2.2, .9, 0]];
// Groups 2-4 are fixed: [rotation, force]. Group 1 comes from the panel.
const Fixed = [null, [110, .5], [180, 2], [-70, .25]];

const turn = (v, deg) => { const r = deg * D2R, c = Math.cos(r), s = Math.sin(r); return { x: v.x * c - v.z * s, z: v.x * s + v.z * c }; };
const unit = v => { const l = Math.hypot(v.x, v.z) || 1; return { x: v.x / l, z: v.z / l }; };

// Everything about each round, in the volley frame, from the params alone.
function plan(p) {
  const pawns = [{ x: 0, z: 0, who: 'me' }, ...Shooters.map((z, i) => ({ x: p.shooters, z, who: i }))];
  const groups = [[p.rot1, +p.force1], ...Fixed.slice(1)];
  const rounds = Rounds.map(([si, before, across, g], i) => {
    const S = { x: p.shooters, z: Shooters[si] }, aimAt = { x: 0, z: across }, h = unit({ x: aimAt.x - S.x, z: aimAt.z - S.z });
    const C = { x: aimAt.x - h.x * before, z: aimAt.z - h.z * before }, fired = Freeze - Math.hypot(C.x - S.x, C.z - S.z) / Speed;
    const changed = g > 0 && g <= p.groups, [rot, force] = changed ? groups[g - 1] : [0, 1];
    const out = changed ? turn(h, rot) : h, speed = Speed * force, range = changed ? BaseRange * force : UnchangedRange;
    // The first pawn on the new line inside the range, or the end of the range.
    let end = range, hit = null;
    pawns.forEach(q => {
      const along = (q.x - C.x) * out.x + (q.z - C.z) * out.z, off = Math.abs((q.x - C.x) * out.z - (q.z - C.z) * out.x);
      if (along > .3 && along < end && off < OnLine) { end = along; hit = q; }
    });
    return { i, S, h, C, fired, g, changed, rot, force, out, speed, end, hit, lands: Apply + end / speed };
  });
  return { rounds, pawns, end: Math.max(...rounds.map(r => r.lands + TrailPerForce * r.force / r.speed)) + Tail };
}

export default {
  kit: 'Accelerator', label: 'Vector manipulation Apply (sketch)',
  params: {
    groups: P('Groups changed (strain 8 / 24 / 48 / 80 %)', 4, 1, 4, 1, 'Showcase'),
    from: P('Volley from (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    shooters: P('Shooters (cells away)', 9, 6, 12, .5, 'Showcase'),
    rot1: P('Group 1 rotation (degrees)', 180, -180, 180, 5, 'Group 1'),
    force1: { label: 'Group 1 force', value: '1', options: ['0.25', '0.5', '1', '2'], group: 'Group 1' },
  },
  duration(p) { return plan(p).end; },
  phases(p) {
    const last = Math.max(...plan(p).rounds.map(r => r.lands));
    return [{ name: 'Volley', t: 0 }, { name: 'Paused, panel open', t: Freeze }, { name: 'Apply', t: Apply }, { name: 'Last round stops', t: last }];
  },
  events() { return [{ t: Apply, type: 'shake', value: .025 }]; },

  draw(s, p, { origin: o, scene }) {
    const { rounds, pawns, end } = plan(p);
    if (s < 0 || s >= end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const r0 = p.from * D2R, fx = { x: Math.cos(r0), z: Math.sin(r0) }, fz = { x: -Math.sin(r0), z: Math.cos(r0) };
    const A = { x: o.x - fx.x * p.shooters / 2, z: o.z - fx.z * p.shooters / 2 };
    // Volley frame to the map; up = true for a point at chest height.
    const map = (v, up = false) => ({ x: A.x + fx.x * v.x + fz.x * v.z, z: A.z + fx.z * v.x + fz.z * v.z + (up ? Chest : 0) });
    const dirOf = v => ({ x: fx.x * v.x + fz.x * v.z, z: fx.z * v.x + fz.z * v.z });
    const at = (P0, d, dir, up = true) => map({ x: P0.x + dir.x * d, z: P0.z + dir.z * d }, up);
    const applied = s >= Apply, age = s - Apply, groupsOn = Math.min(4, Math.max(1, Math.round(p.groups)));

    // Where each round is now, and whether it is still flying.
    const now = rounds.map(r => {
      if (s < r.fired) return null;
      if (s < Freeze) { const d = (s - r.fired) * Speed; return { pos: { x: r.S.x + r.h.x * d, z: r.S.z + r.h.z * d }, dir: r.h, flying: true }; }
      if (!applied) return { pos: r.C, dir: r.h, flying: false };
      const d = Math.min(r.end, age * r.speed);
      return { pos: { x: r.C.x + r.out.x * d, z: r.C.z + r.out.z * d }, dir: r.out, flown: d, flying: d < r.end };
    });

    // --- floor: dust where a round ran out of range --------------------------------------------------------------
    rounds.forEach(r => {
      if (r.hit || !applied || s < r.lands || s > r.lands + .5) return;
      const u = (s - r.lands) / .5, E = at(r.C, r.end, r.out, false);
      for (let i = 0; i < 4; i++) { const ang = i * 1.57 + rand(i + r.i * 3), d = .1 + u * .3; sprite({ x: E.x + Math.cos(ang) * d, z: E.z + Math.sin(ang) * d * .7 + u * .1 }, .2 + u * .25, .16 + u * .2, Dust.withAlpha(.45 * Math.sin(u * Math.PI)), soft, Floor + .03); }
    });

    // --- the pawns, north first: each flinches when a round stops in it --------------------------------------------------
    const hitsOn = q => rounds.filter(r => r.hit === q && s >= r.lands).map(r => ({ age: s - r.lands, dir: dirOf(r.out) }));
    pawns.map(q => {
      const recent = hitsOn(q).filter(h => h.age < .45);
      let dx = 0, dz = 0, flash = 0;
      recent.forEach(h => { const k = h.age < .05 ? smooth(h.age / .05) : 1 - smooth((h.age - .05) / .4); dx += h.dir.x * .15 * k; dz += h.dir.z * .15 * k; flash = Math.max(flash, 1 - clamp(h.age / .15)); });
      const pos = map(q); return { q, pos: { x: pos.x + dx, z: pos.z + dz }, flash };
    }).sort((m, n) => n.pos.z - m.pos.z).forEach(({ q, pos, flash }) => {
      if (q.who === 'me') { accelerator(pos, sun, strength, { tint: White, tintAmount: .8 * flash }); return; }
      pawn(pos, EnemyColour, sun, strength, { tint: White, tintAmount: .9 * flash });
      // A rifle held toward Accelerator, and its muzzle flash on each shot.
      const d = dirOf({ x: -1, z: 0 }), ang = Math.atan2(d.z, d.x) / D2R;
      draw(MeshPool.plane10, pos.x + d.x * .32, pawnLayer + .012, pos.z + d.z * .32 + Chest, .07, .55, 90 - ang, Rifle);
      rounds.filter(r => Rounds[r.i][0] === q.who && s >= r.fired && s < r.fired + .07).forEach(() => {
        sprite({ x: pos.x + d.x * .65, z: pos.z + d.z * .65 + Chest }, .45, .45, Bullet.withAlpha(.9), glow, Y + .05);
      });
    });

    // --- the paused panel's lines: a stand-in for what VectorEditDrawer already draws --------------------------------
    const panel = s < Freeze ? 0 : applied ? 1 - clamp(age / .08) : smooth((s - Freeze) / .1);
    if (panel > 0) {
      ringAt(map({ x: 0, z: 0 }), Reach, White.withAlpha(.3 * panel), Floor + .02);
      rounds.forEach(r => {
        const C = map(r.C, true), grouped = r.g > 0 && r.g <= groupsOn;
        line(`apply panel in ${r.i}`, [at(r.C, -1.4, r.h), C], .03, Grey.withAlpha(.7 * panel), undefined, Y + .03, 'none');
        if (!grouped) { ringAt(C, .28, White.withAlpha(.5 * panel), Y + .031); return; }
        const colour = GroupColours[r.g - 1];
        ringAt(C, .3, colour.withAlpha(.95 * panel), Y + .031);
        line(`apply panel out ${r.i}`, [C, at(r.C, r.end, r.out)], .04, colour.withAlpha(.75 * panel), undefined, Y + .03, 'none');
      });
    }

    // --- Apply: the reach ring, the strain ring, the corners -----------------------------------------------------------
    if (applied && age < .3) {
      const u = age / .3, R = Reach * (1 + .03 * smooth(u));
      ringAt(map({ x: 0, z: 0 }), R, Edge.withAlpha(.35 * (1 - u)), Floor + .022);
      ringAt(map({ x: 0, z: 0 }), R - .06, White.withAlpha(.45 * (1 - u)), Floor + .023);
      const head = { x: A.x, z: A.z + .6 }, size = .4 + 1.6 * Strain[groupsOn - 1];
      ringAt(head, .15 + smooth(u) * size, Edge.withAlpha(.85 * (1 - u)), Y + .19);
      ringAt(head, .12 + smooth(u) * size, White.withAlpha(.9 * (1 - u)), Y + .191);
    }
    rounds.forEach(r => {
      if (!r.changed || !applied || age > .3) return;
      const u = age / .3, C = map(r.C, true), grow = smooth(age / .06);
      // The stroke: in along the old heading, bent onto the new one.
      const stroke = [at(r.C, -.8, r.h), C, at(r.C, .8 * grow + .01, r.out)];
      line(`apply bend edge ${r.i}`, stroke, .12, Edge.withAlpha(.85 * (1 - u)), undefined, Y + .15, 'both');
      line(`apply bend ${r.i}`, stroke, .06, White.withAlpha(.95 * (1 - u)), whiteGlow, Y + .151, 'both');
      // The star: 7 white spikes of uneven length and width at uneven angles, out in 0.05 s, gone by 0.15 s.
      const st = clamp(age / .15), white = 1 - st * st, edge = .7 * (1 - st) * (1 - st);
      if (st < 1) {
        sprite(C, .6 * (1 - st) + .15, .6 * (1 - st) + .15, White.withAlpha(.9 * white), glow, Y + .16);
        for (let i = 0; i < 7; i++) {
          const q = rand(i + r.i * 13), ang = (i * 51 + (q - .5) * 40 + r.i * 23) * D2R, len = (.15 + .65 * rand(i + r.i * 7 + 50) ** 1.5) * smooth(Math.min(1, st * 3));
          const w = .04 + .05 * rand(i + r.i * 5 + 90), a = { x: C.x + Math.cos(ang) * .05, z: C.z + Math.sin(ang) * .05 }, b = { x: C.x + Math.cos(ang) * len, z: C.z + Math.sin(ang) * len };
          streak(`apply star edge ${r.i} ${i}`, a, b, w + .035, Edge.withAlpha(edge), undefined, Y + .161, 3);
          streak(`apply star ${r.i} ${i}`, a, b, w, White.withAlpha(white), whiteGlow, Y + .162, 3);
        }
      }
    });

    // --- the rounds: trails on the changed ones, the bullet itself on all ----------------------------------------------
    rounds.forEach((r, i) => {
      const n = now[i];
      if (!n) return;
      if (applied && r.changed) {
        const len = TrailPerForce * r.force, head = n.flown, tail = Math.min(r.end, Math.max(0, age * r.speed - len));
        if (tail < head) {
          const mid = (head + tail) / 2, pts = [at(r.C, head, r.out), at(r.C, mid, r.out), at(r.C, tail, r.out)];
          line(`apply trail edge ${r.i}`, pts, .11, Edge.withAlpha(.75), undefined, Y + .09);
          line(`apply trail ${r.i}`, pts, .055, White.withAlpha(.95), whiteGlow, Y + .091);
        }
      }
      if (!n.flying && applied) return;
      const pos = map(n.pos, true), d = dirOf(n.dir), ang = Math.atan2(d.z, d.x) / D2R;
      draw(MeshPool.plane10, pos.x - d.x * .12, Y + .1, pos.z - d.z * .12, .06, .32, 90 - ang, Bullet);
      sprite(pos, .22, .22, Bullet.withAlpha(.7), glow, Y + .101);
    });

    // --- where rounds stop in a pawn: plain sparks, not his colours ------------------------------------------------------
    rounds.forEach(r => {
      if (!r.hit || s < r.lands || s > r.lands + .3) return;
      const u = (s - r.lands) / .3, E = map(r.hit, true), back = dirOf(r.out);
      sprite(E, .5 * (1 - u) + .1, .5 * (1 - u) + .1, Bullet.withAlpha(.9 * (1 - u)), glow, Y + .2);
      for (let k = 0; k < 5; k++) {
        const ang = Math.atan2(-back.z, -back.x) + (rand(k + r.i * 9) - .5) * 2.2, d0 = .1 + u * .2, d1 = d0 + .25 * (1 - u * .5);
        streak(`apply spark ${r.i} ${k}`, { x: E.x + Math.cos(ang) * d0, z: E.z + Math.sin(ang) * d0 }, { x: E.x + Math.cos(ang) * d1, z: E.z + Math.sin(ang) * d1 }, .04, Bullet.withAlpha(1 - u), whiteGlow, Y + .2, 3);
      }
    });
  },
};
