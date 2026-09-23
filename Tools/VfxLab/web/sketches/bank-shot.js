// Bank Shot gun: Bank Shot — weapon ability proposal, not the game. Nothing in Source/RimArt draws
// this yet.
//
// What it is for (chosen 2026-09-23 as the last v1 weapon; two modes agreed 2026-09-23; the
// numbers are placeholders and will be XML fields). The pawn carries a heavy pistol whose every
// bullet mirrors off walls.
//   Normal mode: the gun's own verb, used in combat like any pistol. Fired at a pawn, no wall
//   targeting, no aim line. 12 sharp, range 20, 1.2 s between shots. A bullet that misses and
//   hits a wall bounces once and can still find someone.
//   Charge mode (this sketch): an ability. Targets a wall cell, not a pawn, up to 30 cells of
//   flight away. The pawn charges 1.5 s (a glow builds on the barrel, motes are drawn in), then
//   the bullet flies level to that wall, mirrors off the face it hits, and keeps going: up to 3
//   bounces, then it embeds in the next wall. It stops at the first pawn it crosses, friend or
//   foe. No line of sight to the victim is needed, so it shoots round corners, down bent
//   corridors and into rooms through the door. The game shows the whole bounce path as the aim
//   line before the order is given, so the player picks the wall cell that puts the path
//   through the enemy.
//     damage = 18 + 6 per bounce taken (18 / 24 / 30 / 36), sharp, bullet armour penetration;
//     range 30 cells of flight in total; bullet speed 28 cells/s; cooldown 4 s.
// No mode toggle: normal is what a drafted pawn fires, charge is the ability button. A pawn
// standing in the path takes the hit, so an ally in the corridor ends it early. The bullet is
// stopped by pawns at chest height, by walls and by nothing else (doors count as walls when
// shut). The tracer grows wider and hotter with every bounce so the damage climb is visible.
//
// Order (times with the default sliders, corner scenario):
//   0.00  aim: the bounce path is drawn as pale dashes from the muzzle, with a square on the wall
//         cell targeted and a dot at each contact; red floor dashes show the straight shot the
//         wall blocks
//   0.20  charge, 1.5 s: a glow at the muzzle grows from 0.3 to 1.1 cells and turns from pale
//         to hot, a heat line runs the barrel, 12 motes spiral in from about a cell around into
//         the muzzle; at full charge the glow flickers
//   1.70  fire: muzzle flash, recoil, the tracer leaves; the aim line and the charge fade over 0.1 s
//   1.70+ flight: the bullet walks the path at 28 cells/s; a bright trace fades 0.5 s behind it
//         and a smoke thread lingers about 2 s
//   at each contact: a flash on the wall, 7 + 3n sparks thrown away from the face, dust off the
//         face, a chip that stays; the tracer comes off wider and hotter
//   end:  the enemy is hit (flash, blood thrown on past them, a spatter that stays; they flinch
//         0.18 cells and settle 0.08 back), or the bullet embeds in a wall after its 3rd bounce
//   +1.5  result held, then rest
//
// Drawing: the rule is lib/bank-shot-path.js (runs in Node, ports as one class); the pieces are
// lib/bank-shot.js. The bullet flies at HandH = .5, so its path is ground points lifted north by
// .3, and every shape is a level line, quad or sprite: nothing needs a per-facing method. Walls
// are the Paper Bomb kit's stand-ins on the map grid. Caster and target are stand-ins. Scenario
// picks a layout; the aim slider offsets the layout's aim so a miss can be shown (the path is
// recomputed for every frame from the params, never stored).
import { Mathf } from '../js/engine.js';
import { P } from './lib/six-paths-impact.js';
import { Layouts, cells, wallSet, trace, blocked, along } from './lib/bank-shot-path.js';
import {
  walls, figure, pistol, charge, muzzle, bullet, trail, ricochet, embed, wound, preview, blockedLine,
  Enemy, Holder, HandH, GripAlong, MuzzleAlong, Lead, Tail, bump, dirOf, move,
} from './lib/bank-shot.js';

const clamp = Mathf.Clamp01;
const Fade = .1;                       // the aim line fades over this after the shot
const Flinch = .18, Settle = .08;      // how far the hit pawn is thrown, and where they settle

// The origin is the scene's cell. "Centre on" slides the whole layout so the caster or the target
// sits on it instead, for a close look at the gun or the hit.
function anchor(p, o) {
  const L = Layouts[p.scenario] ?? Layouts.corner, c = p.centre === 'caster' ? L.caster : p.centre === 'target' ? L.enemy : { x: 0, z: 0 };
  return { x: o.x - c.x, z: o.z - c.z };
}
function layout(p, o) {
  const L = Layouts[p.scenario] ?? Layouts.corner;
  const world = cells(L).map(c => ({ x: o.x + c.x, z: o.z + c.z }));
  return { L, world, set: wallSet(cells(L)), caster: { x: o.x + L.caster.x, z: o.z + L.caster.z }, enemy: { x: o.x + L.enemy.x, z: o.z + L.enemy.z }, aim: L.aim + p.aimOffset };
}
// The path in layout cells (relative to the origin), so the trace's wall lookups stay on the grid.
function shot(p) {
  const L = Layouts[p.scenario] ?? Layouts.corner, aim = L.aim + p.aimOffset, dir = dirOf(aim);
  const m = move(L.caster, dir, MuzzleAlong);
  return trace(wallSet(cells(L)), m, aim, L.enemy);
}
function times(p) {
  const path = shot(p), fire = Lead + p.charge, flight = path.length / p.speed, endT = fire + flight;
  return { path, fire, endT, end: endT + p.hold + Tail };
}
const toWorld = (path, o) => ({
  ...path,
  pts: path.pts.map(q => ({ ...q, x: q.x + o.x, z: q.z + o.z })),
  bounces: path.bounces.map(b => ({ ...b, x: b.x + o.x, z: b.z + o.z, cell: { x: b.cell.x + o.x, z: b.cell.z + o.z } })),
  end: { ...path.end, x: path.end.x + o.x, z: path.end.z + o.z, cell: path.end.cell ? { x: path.end.cell.x + o.x, z: path.end.cell.z + o.z } : undefined },
});

export default {
  kit: 'Bank Shot gun', label: 'Bank Shot (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'corner', options: Object.keys(Layouts), group: 'Showcase' },
    centre: { label: 'Centre on', value: 'scene', options: ['scene', 'caster', 'target'], group: 'Showcase' },
    aimOffset: P('Aim offset from the layout (degrees)', 0, -12, 12, .25, 'Showcase'),
    actors: { label: 'Show caster and target', value: true, group: 'Showcase' },
    ui: { label: 'Show the aim line and the blocked shot', value: true, group: 'Showcase' },
    speed: P('Bullet speed (cells/s)', 28, 8, 60, 1, 'Rule'),
    charge: P('Charge', 1.5, .3, 3, .05, 'Timing (s)'),
    trailLife: P('Trace fade', .5, .1, 2, .05, 'Timing (s)'),
    hold: P('Show the result', 1.5, .3, 3, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p), out = [{ name: 'Aim', t: 0 }, { name: 'Charge', t: Lead }, { name: 'Fire', t: t.fire }];
    t.path.bounces.forEach(b => out.push({ name: `Bounce ${b.n}`, t: t.fire + b.d / p.speed }));
    out.push({ name: t.path.end.kind === 'hit' ? 'Hit' : t.path.end.kind === 'embed' ? 'Embed' : 'Spent', t: t.endT });
    return out;
  },
  events(p) {
    const t = times(p), out = [{ t: t.fire, type: 'sound', def: 'RimArt_BankShotFire' }, { t: t.fire, type: 'shake', value: .015 }];
    t.path.bounces.forEach(b => { const at = t.fire + b.d / p.speed; out.push({ t: at, type: 'sound', def: 'RimArt_Ricochet' }, { t: at, type: 'shake', value: .01 }); });
    if (t.path.end.kind !== 'range') out.push({ t: t.endT, type: 'sound', def: t.path.end.kind === 'hit' ? 'RimArt_BulletFlesh' : 'RimArt_BulletWall' }, { t: t.endT, type: 'shake', value: .02 });
    return out;
  },

  draw(s, p, { origin: scene0, scene }) {
    const t = times(p), o = anchor(p, scene0);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const { world, set, caster, enemy, aim } = layout(p, o);
    const path = toWorld(t.path, o), dir = dirOf(aim);
    const hand = move(caster, dir, GripAlong), m = move(caster, dir, MuzzleAlong);
    const fired = s >= t.fire, dNow = fired ? Math.min(path.length, (s - t.fire) * p.speed) : 0;
    const hit = path.end.kind === 'hit', hitAge = s - t.endT;

    // The enemy flinches along the bullet's last direction when hit, and settles a little back.
    const last = along(path, path.length), lastDir = { x: last.dx, z: last.dz };
    const shove = hit && hitAge >= 0 ? Flinch * bump(hitAge / .3) + Settle * clamp(hitAge / .3) : 0;
    const enemyAt = move(enemy, lastDir, shove);

    // Floor: the blocked straight shot before the order fires, and what lands afterwards.
    const uiAlpha = p.ui ? (fired ? 1 - clamp((s - t.fire) / Fade) : 1) : 0;
    if (p.actors) blockedLine('bank blocked', caster, (() => { const b = blocked(set, { x: caster.x - o.x, z: caster.z - o.z }, { x: enemy.x - o.x, z: enemy.z - o.z }); return b && { x: b.x + o.x, z: b.z + o.z }; })(), uiAlpha);
    if (hit) wound('bank wound', enemy, lastDir, hitAge, path.bounces.length);

    walls('bank walls', o, world, sun, strength);
    if (p.actors) { figure(caster, Holder, sun, strength); figure(enemyAt, Enemy, sun, strength); }
    const kick = fired ? .09 * bump((s - t.fire) / .14) : 0;
    pistol('bank gun', hand, aim, sun, strength, kick);

    preview('bank preview', path, uiAlpha);
    const chargeU = clamp((s - Lead) / p.charge) * (fired ? 1 - clamp((s - t.fire) / Fade) : 1);
    charge('bank charge', hand, m, aim, chargeU, s);
    if (!fired) return;
    muzzle('bank muzzle', m, aim, s - t.fire);
    trail('bank trail', path, dNow, s, t.fire, p.speed, p.trailLife);
    path.bounces.forEach(b => ricochet(`bank bounce ${b.n}`, b, s - (t.fire + b.d / p.speed), b.n, sun));
    if (path.end.kind === 'embed') embed('bank embed', path.end, hitAge, sun);
    if (s < t.endT) bullet('bank bullet', path, dNow, path.bounces.filter(b => b.d <= dNow).length);
    else if (path.end.kind === 'range' && hitAge < .2) bullet('bank bullet', path, path.length, path.bounces.length);
  },
};
