// Rainbow Crystal Staff — weapon proposal. Sketch only: no C#.
// From Terraria's Rainbow Crystal Staff (Moon Lord drop, 1 in 5): the staff places a crystal that
// floats in the air and fires spreads of three coloured beams at enemies; each beam leaves a star
// that bursts twice. In RimArt it is the first placed turret: the crystal fights on its own for 45 s.
//
// Rules (proposed 2026-10-02; the user picked this weapon, the numbers are placeholders and each
// becomes an XML field):
//   Weapon: a staff like the Last Prism: no shooting verb, a weak melee bash, one ability button.
//   Place crystal: a cell within 12 cells the wielder can see. The crystal grows out of the floor and
//   rises until its lower tip is 1 cell up. One per staff; placing again breaks the old one.
//   Cooldown 60 s from placement, so a crystal is up 45 of every 60 s.
//   Lasts 45 s. Nothing holds the wielder meanwhile (the Last Prism holds them in a channel). The
//   crystal breaks early if the wielder goes down or is more than 30 cells away. It cannot be hit
//   or targeted.
//   Volley every 2 s at the nearest standing enemy the crystal can see within 20 cells (walls block,
//   as in Terraria): three beams, each leaving a star. The middle star lands on the target, the
//   other two 0.9 cells to either side across the line (cells rather than the 20 degrees first
//   proposed, so the spread does not grow with range). Each star bursts twice, 0.5 s and 1.0 s after
//   the volley (Terraria's timing), for 4 burn (15 % armour penetration) to every enemy within 1
//   cell. Stars do not block each other, so a pawn caught by all three takes 24 per volley. An
//   unarmoured pawn goes into pain shock at about 43 burn (see the Last Prism sketch).
//   Enemies only: allies and the wielder inside a burst take nothing (Terraria sentries never hurt
//   the player). Downed pawns are not targeted or hit.
//   My choice, not agreed: the volley aims where a walking target will be 0.5 s later (RimWorld knows
//   its path), so the first burst catches it; the second burst stays where the star is, so a pawn
//   that keeps walking takes only the first. "Lead" off shows the plain version.
//
// Order (defaults, scenario "raid"; times after 1.2 s come from this sketch's simulation; the
// phases list every down):
//   0.00  the wielder raises the staff; 0.30 a glint at its tip and a ring on the floor at the cell
//   0.30  the crystal grows out of the floor and rises over 0.6 s; the range ring (20 cells, true
//         size) fades in and stays faint (the game would draw it while the crystal is selected, as
//         for turrets)
//   0.90  up: a tall diamond (sprite 22 x 46 px, about pawn height) spinning on its upright axis,
//         faces white, pink, slate blue and lavender, bobbing, a soft glow, its shadow on the floor
//   1.20  volley 1 at the first raider walking in from the east. Every 2 s after: three stars leave
//         the crystal 0.03 s apart and fly straight to their spots in 0.18 s (about 50 cells a second
//         at 9 cells), each a four-point star with a trail up to 4 cells behind it: a white core between
//         two thin strands in the volley's colour, over a soft glow, thinning toward the tail, + shaped
//         sparkles dropping off it (the colour steps round the rainbow each volley, as in the demo GIF).
//         A landed star spins; its trail shrinks into it and fades over 0.35 s. At +0.5 and +1.0 each
//         star bursts: a four-point flash, a faint ring at the true 1-cell radius, + shaped sparkles
//         flying out. A pawn a burst hits flickers in its colour and its damage bar fills.
//         The colonist next to the first raider stands inside bursts and takes nothing. The raider
//         behind the wall is the nearest enemy but is not targeted until he steps out at 9 s. The
//         raider 22 cells out is never targeted.
//   1.20  volley 1 at the first raider while he walks in: the first burst catches him (12), the second
//         lands behind him. 3.20 and 5.20 hit him standing (24, then 8): down at 5.70 (44 burn).
//   7.30  the second raider (hidden by the wall until he came round it): 24 + 20, down at 10.30.
//  11.30  the third raider, who stepped out at 9-10 s: 24 + 20, down at 14.32. Nothing else in sight
//         and range: the crystal hovers and holds fire. 16.3 end.
//   With "Lead" off: volley 1 misses the walking raider; downs at 6.20, 11.80 and 16.32.
// "time runs out" shows the end of the 45 s: the crystal flickers for 2 s, then breaks into shards
// that fall and fade (the same break as when a new crystal replaces it, or the wielder goes down).
//
// Drawing: the crystal is a 3D diamond (four sides) placed in east, up, north and drawn with up as a
// shift north (Lift), showing only the faces turned to the viewer, so it is the same from every
// direction; no per-facing method. The projectile is sprites, not meshes: four white PNGs in
// Textures/RimArt/RainbowCrystal/ made by make_rainbow_crystal_textures.py (Trail 256 px: two feathered strands
// over a glow, brightening toward the head; TrailCore 256 px: the white core line; Star 128 px: the four-point
// star, long ray upright; Plus 64 px: the + sparkle), coloured per draw, additive (MoteGlow). A trail is two quads
// stretched from its tail to its head; a star is a halo plus two star quads. A port uses the same texture paths.
// Meshes stay where the shape is the point: the crystal's faces, its outline, the shards and the floor rings. Pawns are lib/pawn.js stand-ins, the staff in the wielder's hand is a
// stand-in for its texture, walls are the Paper Bomb kit's. "Show stand-ins" off hides pawns, walls,
// the staff, the target ring and the damage bars. The fight is replayed from 0 at 60 steps a second
// (cached per parameter set), so the drawing and the damage use the same positions. In "raid" the crystal
// stands 8 cells behind the chosen cell, so the camera's centre is on the fight.
import { Color, Mathf, MaterialPool, ShaderDatabase } from '../js/engine.js';
import { P, Body, Y, Floor, Lift, sprite, soft, glow, rand, circle } from './lib/six-paths-impact.js';
import { draw, mesh } from './lib/six-paths-solid.js';
import { pawn, at, shadowLayer, Skin } from './lib/pawn.js';
import { Enemy, Ally, rect } from './lib/chain-sickle.js';
import { ringAt, whiteGlow } from './lib/goku.js';
import { walls } from './lib/paper-bomb.js';
import { hue, rayWall, damageBar, PainShock, walk, downSmoke } from './lib/terraria.js';

const clamp = Mathf.Clamp01, smooth = Mathf.Smooth, lerp = Mathf.Lerp, D2R = Mathf.Deg2Rad, TAU = Math.PI * 2;
const White = new Color(1, 1, 1), Warn = new Color(.85, .18, .12), Wielder = new Color(.30, .50, .62);
const StaffWood = new Color(.42, .30, .52), StaffDark = new Color(.18, .12, .26);
// The crystal's faces, from the sprite: white, pink, slate blue, lavender; a violet outline.
const Faces = [new Color(.96, .94, 1), new Color(.93, .56, .90), new Color(.45, .57, .82), new Color(.74, .62, .96)];
const FaceDark = new Color(.24, .18, .40), Outline = new Color(.30, .20, .52), Ridge = new Color(.98, .92, 1);
// The rule's numbers (XML fields in a port).
const Life = 45, Pop1 = .5, Pop2 = 1;
// Decided looks and timing of the picture.
const Cast = .3, Rise = .6, FirstLook = .3;           // staff raised; the crystal grows; first volley no sooner than this after it is up
const Flight = .18, Stagger = .03;                    // a star flies from the crystal to its spot; the three leave this far apart
const TrailMax = 4, TrailFade = .35;                  // cells of trail behind a flying star; after it lands the trail shrinks into it
const TrailWide = .6;                                 // cells across the trail sprite (its strands sit up to .13 either side of the middle)
const HueStep = .29;                                  // colour change per volley
const PawnScreen = 1.17;                              // lib/pawn.js: an average pawn's head top to feet, on screen
const Slim = 22 / 46, EqAt = .42;                     // sprite width over height; the widest point, from the top
const Bob = .04, FlickerFor = 2, BreakFor = 1.6, EndIdle = 2, MaxTime = 40, Tail = 2;
// Layout in cells from the crystal, x toward the raid, z across. The colonist stands beside where the
// first raider stops; three wall cells hide the third raider until he steps out.
const Crowd = [
  { path: [[.3, 17, 1.2], [2.9, 9, 1.2]] },                      // walks in and stops
  { path: [[1.5, 19, -7.5], [7.5, 7, -1.5]] },                   // walks in on a slant, behind the wall at first
  { path: [[0, 7.5, -4], [9, 7.5, -4], [10, 9, -1.8]] },         // nearer than the others but behind the wall; steps out at 9 s
  { path: [[0, 22, 3]] },                                        // out of range
  { path: [[0, 8.4, .4]], ally: true },                          // a colonist beside the first raider
];
const WallCells = [[6, -2], [6, -3], [6, -4]], WielderAt = { x: -4, z: -3 };
const Back = 8;                                       // the crystal stands this many cells behind the chosen cell

// The sprites: white textures with the shape in alpha, coloured per draw, drawn additive. Made by
// make_rainbow_crystal_textures.py (the formulas and what each picture is are in its docstring).
const trailMat = MaterialPool.MatFrom('RimArt/RainbowCrystal/Trail', ShaderDatabase.MoteGlow), coreMat = MaterialPool.MatFrom('RimArt/RainbowCrystal/TrailCore', ShaderDatabase.MoteGlow);
const starMat = MaterialPool.MatFrom('RimArt/RainbowCrystal/Star', ShaderDatabase.MoteGlow), plusMat = MaterialPool.MatFrom('RimArt/RainbowCrystal/Plus', ShaderDatabase.MoteGlow);

const bump = x => (x >= 0 && x <= 1) ? Math.sin(x * Math.PI) : 0;
const add = (a, b) => ({ x: a.x + b.x, z: a.z + b.z });
const pale = c => Color.Lerp(c, White, .45);
const volleyHue = (v, b = 1) => hue(.5 + v * HueStep + (b - 1) * .03, .75);

// The whole fight, replayed from 0 at 60 steps a second in cells relative to the crystal's cell:
// who walks where, every volley, every burst, every hit and who goes down.
let cached = { key: '', value: null };
function replay(p) {
  const key = [p.direction, p.lead, p.every, p.range, p.spread, p.radius, p.burn].join('|');
  if (cached.key === key) return cached.value;
  const dt = 1 / 60, a0 = p.direction * D2R, ca = Math.cos(a0), sa = Math.sin(a0);
  const world = q => ({ x: q.x * ca - q.z * sa, z: q.x * sa + q.z * ca });
  const cells = WallCells.map(([x, z]) => { const q = world({ x, z }); return { x: Math.round(q.x), z: Math.round(q.z) }; });
  const n = Math.ceil(MaxTime / dt);
  const people = Crowd.map(c => ({ ...c, total: 0, down: Infinity, dmg: new Float32Array(n + 1), hits: [] }));
  const pos = (j, s) => world(walk(people[j].path, Math.min(s, people[j].down)));
  const sees = q => { const d = Math.hypot(q.x, q.z); return d <= p.range && rayWall({ x: 0, z: 0 }, Math.atan2(q.z, q.x), cells, d) >= d - .01; };
  // The nearest standing enemy in range with a clear line.
  const pick = s => {
    let best = -1, near = Infinity;
    people.forEach((c, j) => {
      if (c.ally || c.down <= s) return;
      const q = pos(j, s), d = Math.hypot(q.x, q.z);
      if (d < near && sees(q)) { near = d; best = j; }
    });
    return best;
  };
  const volleys = [], pops = new Map(), targets = new Int8Array(n + 1).fill(-1), downs = [];
  let next = Cast + Rise + FirstLook, lastPop = 0;
  for (let k = 0; k <= n; k++) {
    const s = k * dt;
    if (s >= next) {
      const j = pick(s);
      if (j >= 0) {
        const v = volleys.length, aim = pos(j, p.lead ? s + Pop1 : s), d = Math.hypot(aim.x, aim.z) || 1, ux = aim.x / d, uz = aim.z / d;
        const stars = [-1, 0, 1].map((side, b) => {
          const jr = (side ? .15 : .1) * rand(v * 11 + b * 3 + 1), ja = rand(v * 11 + b * 3 + 2) * TAU;
          return { b, x: aim.x - uz * side * p.spread + Math.cos(ja) * jr, z: aim.z + ux * side * p.spread + Math.sin(ja) * jr };
        });
        volleys.push({ v, t: s, target: j, stars });
        [Pop1, Pop2].forEach((delay, w) => { const at = k + Math.round(delay / dt); pops.set(at, [...(pops.get(at) ?? []), { v, w }]); });
        next = s + p.every;
      }
    }
    (pops.get(k) ?? []).forEach(({ v, w }) => {
      lastPop = s;
      volleys[v].stars.forEach(star => people.forEach((c, j) => {
        if (c.ally || c.down <= s) return;
        const q = pos(j, s);
        if (Math.hypot(q.x - star.x, q.z - star.z) > p.radius) return;
        c.total += p.burn; c.hits.push({ t: s, v, b: star.b });
        if (c.total >= PainShock && c.down === Infinity) { c.down = s; downs.push({ t: s, j }); }
      }));
    });
    const live = volleys.length ? volleys[volleys.length - 1] : null;
    targets[k] = live && s < live.t + p.every && people[live.target].down > s ? live.target : -1;
    people.forEach(c => { c.dmg[k] = c.total; });
  }
  const value = { dt, volleys, people, pos, cells, targets, downs, wielder: world(WielderAt), end: Math.min(MaxTime, lastPop + Tail) };
  cached = { key, value };
  return value;
}

function times(p) {
  if (p.scenario === 'time runs out') return { flicker: EndIdle, breaks: EndIdle + FlickerFor, end: EndIdle + FlickerFor + BreakFor };
  const r = replay(p);
  return { up: Cast + Rise, end: r.end };
}

// The crystal: a four-sided diamond standing on its tip over ground point g, its lower tip hBot cells
// up, tall cells high, rad cells from axis to corner, turned by turns about its upright axis. Corners
// are placed in east, up, north and drawn with up as a shift north. A face shows when its outward
// normal points at the viewer, (0, 1, -Lift); those faces never overlap, so no sorting. Each side
// keeps a sprite colour (white, pink, slate blue, lavender), the lower faces a little bluer, lit by
// the sun, so the colours slide across as it spins. flash 0..1 whitens it when it fires.
function crystal(g, hBot, tall, rad, turns, sun, alpha, flash, s, layer) {
  if (alpha <= 0 || tall <= .01) return;
  const hTop = hBot + tall, hEq = hTop - tall * EqAt;
  const corners = [0, 1, 2, 3].map(k => { const a = turns * TAU + k * TAU / 4; return { x: g.x + rad * Math.cos(a), y: hEq, z: g.z + rad * Math.sin(a) }; });
  const v = [...corners, { x: g.x, y: hTop, z: g.z }, { x: g.x, y: hBot, z: g.z }];   // 4 = top tip, 5 = lower tip
  const screen = q => ({ x: q.x, z: q.z + q.y * Lift });
  const sub = (a, b) => ({ x: a.x - b.x, y: a.y - b.y, z: a.z - b.z });
  const sl = Math.hypot(sun.x, 1, sun.z), toSun = { x: -sun.x / sl, y: 1 / sl, z: -sun.z / sl };
  const faces = [];
  for (let k = 0; k < 4; k++) { faces.push([4, k, (k + 1) % 4, k, false]); faces.push([5, (k + 1) % 4, k, k, true]); }
  const edges = new Map();
  faces.forEach(([i, j, m, side, lower], f) => {
    const e1 = sub(v[j], v[i]), e2 = sub(v[m], v[i]);
    let n = { x: e1.y * e2.z - e1.z * e2.y, y: e1.z * e2.x - e1.x * e2.z, z: e1.x * e2.y - e1.y * e2.x };
    const c = { x: (v[i].x + v[j].x + v[m].x) / 3 - g.x, y: (v[i].y + v[j].y + v[m].y) / 3 - hEq, z: (v[i].z + v[j].z + v[m].z) / 3 - g.z };
    if (n.x * c.x + n.y * c.y + n.z * c.z < 0) n = { x: -n.x, y: -n.y, z: -n.z };
    if (n.y - Lift * n.z <= 0) return;
    const nl = Math.hypot(n.x, n.y, n.z), lit = Math.max(0, (n.x * toSun.x + n.y * toSun.y + n.z * toSun.z) / nl);
    let colour = lower ? Color.Lerp(Faces[side], Faces[2], .3) : Color.Lerp(Faces[side], White, .2);
    colour = Color.Lerp(FaceDark, colour, .75 + .25 * lit);
    colour = Color.Lerp(colour, hue(s * .2 + side / 4, .45), .08);
    if (flash > 0) colour = Color.Lerp(colour, White, .55 * flash);
    const pts = [v[i], v[j], v[m]].map(screen), msh = mesh(`rainbow crystal face ${f}`);
    msh.setFlat(pts.flatMap(q => [q.x, q.z]), [0, 1, 2]);
    draw(msh, 0, layer + f * .0003, 0, 1, 1, 0, colour.withAlpha(.95 * alpha));
    [[i, j], [j, m], [m, i]].forEach(([a, b]) => { const key = a < b ? `${a}${b}` : `${b}${a}`; edges.set(key, (edges.get(key) ?? 0) + 1); });
  });
  edges.forEach((count, key) => {
    const a = screen(v[+key[0]]), b = screen(v[+key[1]]), ridge = count > 1;
    rect(`rainbow crystal edge ${key}`, { x: (a.x + b.x) / 2, z: (a.z + b.z) / 2 }, Math.hypot(b.x - a.x, b.z - a.z) + .015, ridge ? .016 : .028,
      Math.atan2(b.z - a.z, b.x - a.x) / D2R, (ridge ? Ridge : Outline).withAlpha((ridge ? .55 : .9) * alpha), layer + .004 + (ridge ? 0 : .0005));
  });
}

// A shard of the crystal: a thin triangle, len long and wid across its base, pointing along turn (radians),
// with a darker edge one size up behind it.
function shard(key, pos, len, wid, turn, colour, edge, layer) {
  if (colour.a <= .002) return;
  const ca = Math.cos(turn), sa = Math.sin(turn);
  const tri = (k, grow) => {
    const pts = [[.6, 0], [-.4, .5], [-.4, -.5]].map(([u, w]) => ({ x: pos.x + (u * len * ca - w * wid * sa) * grow, z: pos.z + (u * len * sa + w * wid * ca) * grow }));
    const m = mesh(`${key} ${k}`); m.setFlat(pts.flatMap(q => [q.x, q.z]), [0, 1, 2]);
    return m;
  };
  draw(tri('edge', 1.35), 0, layer, 0, 1, 1, 0, edge);
  draw(tri('face', 1), 0, layer + .0005, 0, 1, 1, 0, colour);
}
// A + shaped sparkle: one sprite.
function sparkle(pos, size, colour, layer = Y + .07) {
  if (colour.a <= .002 || size <= 0) return;
  sprite(pos, size, size, colour, plusMat, layer);
}
// A four-point star, size cells from the middle to the tip of the long ray, turned turn degrees anticlockwise:
// a soft halo in its colour, the star sprite in its colour, a smaller white one over it.
function star(pos, size, colour, alpha, turn = 0, layer = Y + .08) {
  if (alpha <= 0 || size <= 0) return;
  sprite(pos, size * .9, size * .9, colour.withAlpha(.5 * alpha), glow, layer);
  sprite(pos, size * 2.1, size * 2.1, pale(colour).withAlpha(alpha), starMat, layer + .001, -turn);
  sprite(pos, size * 1.2, size * 1.2, White.withAlpha(alpha), starMat, layer + .002, -turn);
}

// The staff in the wielder's hand (a stand-in for its texture): a violet rod with a small crystal on top.
function staff(pos, raise, s) {
  const hand = add(at(pos, 'waist'), { x: .3, z: 0 }), lift = .18 * raise;
  rect('rainbow crystal staff rod', { x: hand.x, z: hand.z + .2 + lift }, .85, .05, 90, StaffWood, Y + .01);
  rect('rainbow crystal staff rod dark', { x: hand.x + .012, z: hand.z + .2 + lift }, .85, .018, 90, StaffDark, Y + .0101);
  const top = { x: hand.x, z: hand.z + .66 + lift };
  sprite(top, .13, .24, Faces[1], soft, Y + .011);
  sprite(top, .06, .14, Faces[0], soft, Y + .012);
  sprite(top, .35, .35, hue(s * .3, .6).withAlpha(.2 + .3 * raise), glow, Y + .013);
  return top;
}

export default {
  kit: 'Rainbow Crystal Staff', label: 'Rainbow Crystal (sketch)',
  params: {
    scenario: { label: 'Scenario', value: 'raid', options: ['raid', 'time runs out'], group: 'Showcase' },
    direction: P('Raid comes from (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    lead: { label: 'Lead: aim where a walking target will be at the first burst', value: true, group: 'Showcase' },
    actors: { label: 'Show stand-ins (pawns, walls, staff, target ring, damage bars)', value: true, group: 'Showcase' },
    every: P('Volley every', 2, .5, 4, .1, 'Timing (s)'),
    range: P('Range (cells)', 20, 8, 30, 1, 'Rule'),
    spread: P('Side stars, across the line (cells)', .9, 0, 2, .05, 'Rule'),
    radius: P('Burst radius (cells)', 1, .5, 2, .05, 'Rule'),
    burn: P('Burn per burst', 4, 1, 12, 1, 'Rule'),
    float: P('Lower tip, cells up', 1, .3, 2, .05, 'Shape'),
    size: P('Crystal height (share of a pawn)', .8, .4, 1.2, .05, 'Shape'),
    spin: P('Spin (turns a second)', .45, 0, 2, .05, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    if (p.scenario === 'time runs out') { const t = times(p); return [{ name: 'Up (41 s of 45)', t: 0 }, { name: 'Last 2 s: flickers', t: t.flicker }, { name: '45 s: breaks', t: t.breaks }]; }
    const r = replay(p);
    return [{ name: 'Place', t: 0 }, { name: 'Crystal up', t: Cast + Rise }, ...(r.volleys.length ? [{ name: 'Volley 1', t: r.volleys[0].t }] : []),
      ...r.downs.map((d, k) => ({ name: `Down ${k + 1}`, t: d.t }))];
  },

  draw(s, p, { origin, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const ending = p.scenario === 'time runs out';
    // o is the crystal's cell: in "raid" Back whole cells behind the chosen cell, so the fight is centred on screen.
    const a0 = p.direction * D2R, o = ending ? origin : add(origin, { x: Math.round(-Back * Math.cos(a0)), z: Math.round(-Back * Math.sin(a0)) });
    const r = ending ? null : replay(p), k = r ? Math.min(r.targets.length - 1, Math.round(s / r.dt)) : 0;
    const wielder = add(o, r ? r.wielder : { x: WielderAt.x, z: WielderAt.z });

    // --- the crystal's state at this time ----------------------------------------------------------------------------
    const grow = ending ? 1 : smooth((s - Cast) / Rise);
    const tall = p.size * PawnScreen / Lift * grow, rad = p.size * PawnScreen * Slim / 2 * grow;
    const hBot = p.float * grow + Bob * Math.sin(s * 2.2) * grow, hMid = hBot + tall * (1 - EqAt);
    const centre = { x: o.x, z: o.z + hMid * Lift };
    let alpha = grow > 0 ? 1 : 0, broken = false;
    if (ending && s >= t.flicker && s < t.breaks) {
      const u = (s - t.flicker) / FlickerFor, rate = lerp(6, 18, u);
      alpha = rand(Math.floor(s * rate) + 3) > .25 + .3 * u ? 1 : .3;
    }
    if (ending && s >= t.breaks) { alpha = 0; broken = true; }
    let flash = 0;
    if (r) r.volleys.forEach(v => { const a = s - v.t; if (a >= 0 && a < .15) flash = Math.max(flash, 1 - a / .15); });

    // --- the floor: range ring, placement ring and sparkles, the target ring ----------------------------------------------
    const ringIn = ending ? 1 : clamp((s - Cast - .3) / .6);
    if (!broken) circle(o, p.range, (ringIn < 1 ? .35 * ringIn : .14) * (ending ? alpha : 1), Floor + .01, pale(hue(.75, .4)));
    if (!ending) {
      const u = (s - Cast) / (Rise + .3);
      if (u >= 0 && u < 1) {
        ringAt(o, .25 + .55 * smooth(u), hue(.85 + u * .4, .6).withAlpha(.7 * (1 - u)), Floor + .02, true, whiteGlow);
        for (let q = 0; q < 8; q++) {
          const a = rand(q + 90) * TAU, d = .2 + .35 * rand(q + 91), h = u * (1 + rand(q + 92));
          sparkle({ x: o.x + Math.cos(a) * d, z: o.z + Math.sin(a) * d * .6 + h * Lift }, .14, hue(q / 8 + u * .5, .6).withAlpha(bump(u)));
        }
      }
    }
    if (r && p.actors && r.targets[k] >= 0) ringAt(add(o, r.pos(r.targets[k], s)), .5, Warn.withAlpha(.6), Floor + .02);
    if (r && p.actors) walls('rainbow crystal wall', o, r.cells.map(c => add(o, c)), sun, strength);

    // --- pawns, north first; the wielder with the staff ---------------------------------------------------------------------
    const people = r ? r.people.map((c, j) => {
      const pos = add(o, r.pos(j, s)), down = s >= c.down;
      const hit = c.hits.filter(h => s - h.t >= 0 && s - h.t < .15).pop();
      return { c, j, pos, down, hit, share: c.dmg[k] / PainShock };
    }) : [];
    const figures = [...people, { pos: wielder, wielder: true }].sort((m, n) => n.pos.z - m.pos.z);
    if (p.actors) figures.forEach(g => {
      if (g.wielder) {
        pawn(g.pos, { shirt: Wielder, sun, shadow: strength });
        const raise = ending ? 0 : bump(clamp(s / (Cast + .4)));
        const top = staff(g.pos, raise, s);
        if (!ending) star(top, .4, hue(.85, .6), bump((s - .15) / .35), 0, Y + .02);
        return;
      }
      const tint = g.hit ? volleyHue(g.hit.v, g.hit.b) : null, amount = g.hit ? .7 : 0;
      const shirt = g.c.ally ? Ally : Enemy;
      pawn(g.pos, { shirt: tint ? Color.Lerp(shirt, tint, amount) : shirt, skin: tint ? Color.Lerp(Skin, tint, amount) : Skin, sun, shadow: strength, downed: g.down });
      if (!g.down && !g.c.ally) damageBar(at(g.pos, 'headTop'), g.share);
      if (g.down) downSmoke(g.pos, s - g.c.down);
    });

    // --- the crystal: shadow, glow, faces --------------------------------------------------------------------------------
    if (alpha > 0) {
      sprite({ x: o.x + sun.x * hMid, z: o.z + sun.z * hMid }, rad * 2.8 + .1, rad * 1.8 + .06, Body.withAlpha(strength * .5 * grow * alpha), soft, shadowLayer);
      sprite({ x: o.x, z: o.z }, 1.1 * grow, .7 * grow, hue(s * .15, .5).withAlpha(.12 * alpha), glow, Floor + .015);
      sprite(centre, tall * Lift * 1.2 + .3, tall * Lift * 1.5 + .3, hue(s * .15, .5).withAlpha((.16 + .3 * flash) * alpha), glow, Y + .03);
      crystal(o, hBot, tall, rad, s * p.spin, sun, alpha, flash, s, Y + .035);
      const g = (s * .7) % 1;
      if (flash <= 0) star({ x: o.x - rad * .3, z: o.z + (hBot + tall * .8) * Lift }, .2, White, .8 * bump(g / .2) * alpha, 0, Y + .045);
      if (flash > 0) star(centre, .3 + .2 * flash, White, flash, 0, Y + .045);
    }

    // --- the break: shards fall and fade, sparkles burst -----------------------------------------------------------------
    if (broken) {
      const a = s - t.breaks, u = a / BreakFor;
      sprite(centre, 2.4 * (1 - u * .5), 2.4 * (1 - u * .5), hue(.8, .5).withAlpha(.6 * Math.max(0, 1 - a / .3)), glow, Y + .05);
      for (let q = 0; q < 10; q++) {
        const ang = q * TAU / 10 + rand(q + 30), sp = .7 + .8 * rand(q + 31), up = .8 + 1.2 * rand(q + 32), h0 = hBot + tall * rand(q + 33);
        const land = (up + Math.sqrt(up * up + 2 * 6 * h0)) / 6, tt = Math.min(a, land), h = Math.max(0, h0 + up * tt - 3 * tt * tt);
        const gx = o.x + Math.cos(ang) * sp * tt, gz = o.z + Math.sin(ang) * sp * tt, fade = a < land ? 1 : Math.max(0, 1 - (a - land) / .6);
        if (a < land) sprite({ x: gx + sun.x * h, z: gz + sun.z * h }, .12, .08, Body.withAlpha(strength * .5), soft, shadowLayer);
        const turn = ang + (a < land ? a : land) * 9 * (q % 2 ? 1 : -1), size = .7 + .6 * rand(q + 34), layer = a < land ? Y + .04 : Floor + .03;
        shard(`rainbow crystal shard ${q}`, { x: gx, z: gz + h * Lift }, .2 * size, .09 * size, turn, Faces[q % 4].withAlpha(fade), Outline.withAlpha(fade), layer);
      }
      for (let q = 0; q < 12; q++) {
        const ang = rand(q + 50) * TAU, d = .2 + u * (1 + rand(q + 51)) * 1.4;
        sparkle({ x: centre.x + Math.cos(ang) * d, z: centre.z + Math.sin(ang) * d }, .16, hue(q / 12, .6).withAlpha(Math.max(0, 1 - u * 1.6)));
      }
    }

    // --- volleys: beams, stars, bursts -----------------------------------------------------------------------------------
    if (r) r.volleys.forEach(v => {
      if (s < v.t || s > v.t + Pop2 + .6) return;
      v.stars.forEach(st => {
        const colour = volleyHue(v.v, st.b);
        const ground = add(o, st), pt = at(ground, 'chest');
        const tb = v.t + st.b * Stagger, age = s - tb;
        // The flight: the star leaves the crystal and flies straight to its spot in Flight seconds, its trail up to
        // TrailMax cells behind it. After it lands the trail shrinks into it and fades over TrailFade.
        const dx = pt.x - centre.x, dz = pt.z - centre.z, L = Math.hypot(dx, dz) || 1, ux = dx / L, uz = dz / L;
        const along = d => ({ x: centre.x + ux * d, z: centre.z + uz * d });
        if (age >= 0 && age < Flight + TrailFade) {
          const flying = age < Flight, head = L * clamp(age / Flight), landTail = Math.max(0, L - TrailMax);
          const tail = flying ? Math.max(0, head - TrailMax) : lerp(landTail, L, smooth((age - Flight) / TrailFade));
          const fade = flying ? 1 : 1 - clamp((age - Flight) / TrailFade), span = head - tail;
          if (span > .05) {
            // One trail sprite in the volley's colour and a white core over it, stretched from tail to head.
            const T = along(tail), H = along(head), mid = { x: (T.x + H.x) / 2, z: (T.z + H.z) / 2 }, deg = -Math.atan2(uz, ux) / D2R;
            sprite(mid, span, TrailWide, colour.withAlpha(.95 * fade), trailMat, Y + .05, deg);
            sprite(mid, span, TrailWide * .8, White.withAlpha(.85 * fade), coreMat, Y + .051, deg);
            for (let q = 0; q < 4; q++) {
              const d = lerp(tail, head, rand(v.v * 13 + st.b * 5 + q)), side = (rand(q + v.v * 3 + 40) - .5) * .5 * (1 + age * 2), c = along(d);
              sparkle({ x: c.x - uz * side, z: c.z + ux * side }, .12, pale(colour).withAlpha(fade));
            }
          }
          if (flying) {
            star(along(head), .3, colour, 1, age * 720, Y + .06);
            if (age < .1) sprite(centre, .45, .45, pale(colour).withAlpha(.6 * (1 - age / .1)), glow, Y + .046);
          }
        }
        // The star after it lands: spins and grows until the first burst, smaller until the second.
        const landed = age - Flight, p1 = s - (v.t + Pop1), p2 = s - (v.t + Pop2);
        if (landed >= 0 && p2 < 0) {
          const grow = p1 < 0 ? .3 + .08 * smooth(landed / (Pop1 - Flight - st.b * Stagger)) : .22 + .03 * Math.sin(s * 30);
          star(pt, grow, colour, .9, s * 360, Y + .06);
          sprite(ground, .9, .6, colour.withAlpha(.18), glow, Floor + .025);
        }
        // Each burst: a flash the size of the hit area, a ring at the true radius, a four-point flash, sparkles flying out.
        [p1, p2].forEach((pa, w) => {
          if (pa < 0 || pa > .5) return;
          const u = pa / .5, f = (1 - u) * (1 - u), big = w ? .8 : 1;
          if (u < .5) sprite(pt, p.radius * 2 * big, p.radius * 2 * big, colour.withAlpha(.35 * f), glow, Y + .065);
          ringAt(ground, p.radius * (.75 + .25 * smooth(u)), colour.withAlpha(.28 * (1 - u)), Floor + .03, false, whiteGlow);
          star(pt, .6 * big * (1 - .5 * u), colour, 1 - u, 0, Y + .09);
          for (let q = 0; q < 10; q++) {
            const ang = rand(v.v * 31 + st.b * 11 + w * 7 + q) * TAU, d = .15 + smooth(u) * (.4 + .8 * rand(q + v.v + 60)) * p.radius;
            sparkle({ x: pt.x + Math.cos(ang) * d, z: pt.z + Math.sin(ang) * d }, .14 * (1 - .4 * u), pale(colour).withAlpha(1 - u), Y + .095);
          }
        });
      });
    });
  },
};

