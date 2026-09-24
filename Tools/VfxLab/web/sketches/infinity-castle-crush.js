// Infinity Castle: Crush — castle command proposal for the Infinity Castle kit, not the game.
// Nothing in Source/RimArt draws this yet.
//
// What it is for (agreed in outline 2026-09-23; every number is a placeholder and will be an XML
// field). A command the carrier plays from the biwa room while the castle is open; one strum, 1.5 s
// after the last. Crush: pick a room of at least 7 x 7 (not the biwa room). All four walls slam 2
// cells into the room and draw back. Everyone standing in the outer 2 cells of the floor takes
// 15 blunt and is stunned 1 s, colonists included; the middle is safe. It is the only command
// that deals damage. Own cooldown 10 s.
//
// Order (times with the default sliders):
//   0.00  the hall K (13 x 11, floor 11 x 9) holds four raiders and a colonist; three raiders and
//         the colonist stand in the outer 2 cells, one raider in the middle
//   0.20  the bachi lifts; 0.40 strum: rings leave the biwa, K's outline flashes
//   0.45  the outer 2 cells of K's floor light up pale and pulse for 0.3 s (the hit area)
//   0.75  the walls slam 2 cells in, speeding up, in 0.12 s, covering the outer band
//   0.87  impact: dust along the four wall fronts, splinters, camera shake 0.06, a flash on each
//         pawn under the walls
//   1.17  the walls draw back in 0.8 s; the pawns that were under them are squashed, then stand
//         up stunned (stars) until 1.87; the raider in the middle is untouched
//
// Drawing: the walls are drawn as slabs sliding out of the room's wall ring, over the pawns while
// they are in (in game this is all drawn by the mod over the real room; nothing on the map moves).
// Level shapes and stand-in pawns only, no per-facing method.
import { Color } from '../js/engine.js';
import { P } from './lib/six-paths-impact.js';
import {
  local, drawVoid, drawRooms, doorway, figure, carrierPlays, roomFlash, dustLine, stunned, box, glowFlat, sprite, glow, rand,
  centreOf, cellCentre, Enemy, Ally, Strum, WallWood, WallTop, Lacquer, Paper, Lattice, L, Lift, smooth, clamp, bump,
} from './lib/infinity-castle.js';

// Decided values and the rule's numbers.
const Band = 2, Slam = .12, Hold = .3, Back = .8, Stun = 1;
const castle = local([['biwa', -20, -4, 9, 9], ['corridor', -11, -1, 7, 5], ['hall', -4, -6, 13, 11]]);
const K = 2;
// Pawns in K by cell: three raiders and a colonist in the outer band, one raider in the middle.
const Pawns = [[-3, 0, Enemy], [6, -2, Enemy], [2, 3, Enemy], [4, -5, Ally], [1, -1, Enemy]];
const PanelWood = Color.Lerp(WallWood, WallTop, .35), PanelPaper = Color.Lerp(Paper, WallWood, .25);

function times(p) {
  const strumAt = p.lead, mark = strumAt + .05, slam = mark + p.telegraph, hit = slam + Slam, back = hit + Hold, open = back + Back;
  return { strumAt, mark, slam, hit, back, open, end: Math.max(open, hit + Stun) + p.hold };
}

export default {
  kit: 'Infinity Castle', label: 'Crush (sketch)', scene: false,
  params: {
    depth: { label: 'Rooms at other depths', value: true, group: 'Showcase' },
    lead: P('Before the strum', .4, .2, 1, .05, 'Timing (s)'),
    telegraph: P('Hit area shows for', .3, 0, .8, .05, 'Timing (s)'),
    hold: P('Show the result', 1, .3, 3, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Room at rest', t: 0 }, { name: 'Strum', t: t.strumAt }, { name: 'Hit area', t: t.mark }, { name: 'Slam', t: t.slam },
    { name: 'Impact', t: t.hit }, { name: 'Walls back', t: t.back }, { name: 'Result', t: t.open },
  ]; },
  events(p) { const t = times(p); return [
    { t: t.strumAt, type: 'sound', def: 'RimArt_BiwaStrum' }, { t: t.hit, type: 'shake', value: .06 }, { t: t.hit, type: 'sound', def: 'RimArt_CastleCrush' },
  ]; },

  draw(s, p, { origin, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = (scene?.sun?.strength ?? .32) * .6;
    const base = { x: origin.x - .5 + 4, z: origin.z - .5 };
    drawVoid({ x: origin.x, z: origin.z }, s, { seed: 12, depth: p.depth, reach: 50 });
    drawRooms(castle, base, s);
    castle.doors.forEach(d => doorway(`crush door ${d.key}`, d, base, 1));
    carrierPlays('crush carrier', castle, base, s, [t.strumAt], sun, strength);
    const room = castle.rooms[K];
    roomFlash(room, centreOf(base, room), s - t.strumAt - .05);

    // The floor inside the walls, in world cells.
    const f = { x0: base.x + room.x + 1, z0: base.z + room.z + 1, x1: base.x + room.x + room.w - 1, z1: base.z + room.z + room.h - 1 };
    const bands = d => [[f.x0, f.z1 - d, f.x1, f.z1, 'n'], [f.x0, f.z0, f.x1, f.z0 + d, 's'], [f.x0, f.z0 + d, f.x0 + d, f.z1 - d, 'w'], [f.x1 - d, f.z0 + d, f.x1, f.z1 - d, 'e']];

    // The hit area: the outer 2 cells light up and pulse until the walls come in.
    const markA = s >= t.mark && s < t.hit ? smooth((s - t.mark) / .1) * (.75 + .25 * Math.sin((s - t.mark) * 30)) : 0;
    if (markA > 0) bands(Band).forEach(([x0, z0, x1, z1]) => box(x0, z0, x1, z1, Strum.withAlpha(.16 * markA), L.floor + .03, glowFlat));

    // How far the walls are in: speeding up into the hit, a short hold, then easing back.
    const inU = s < t.slam ? 0 : s < t.hit ? Math.pow((s - t.slam) / Slam, 2) : s < t.back ? 1 : 1 - smooth((s - t.back) / Back);
    const d = Band * inU;

    // Pawns: those in the band are covered while the walls are in, then squashed and stunned.
    Pawns.forEach(([x, z, colour], i) => {
      const pos = cellCentre(base, x, z), hitBand = x - room.x - 1 < Band || room.x + room.w - 2 - x < Band || z - room.z - 1 < Band || room.z + room.h - 2 - z < Band;
      if (!hitBand) { figure(pos, colour, sun, strength); return; }
      const after = s - t.back;
      figure(pos, colour, sun, strength, { squash: s < t.hit ? 0 : 1 - smooth(after / .45) });
      stunned(`crush stun ${i}`, pos, s - t.back, Stun - Hold);
      const fl = s - t.hit;
      if (fl >= 0 && fl < .18) sprite({ x: pos.x, z: pos.z + .45 }, 1.1, 1.1, Strum.withAlpha(.7 * (1 - fl / .18)), glow, L.fx + .08);
    });

    // The walls: slabs out of the wall ring, drawn over the pawns while they are in.
    if (d > .01) {
      const y = L.fx - .1;
      bands(d).forEach(([x0, z0, x1, z1, side]) => {
        box(x0, z0, x1, z1, WallWood, y);
        const alongX = side === 'n' || side === 's', inset = Math.min(.3, d * .2);
        // A shoji strip down the middle of each slab, lattice every half cell.
        if (alongX) { box(x0 + .2, z0 + inset, x1 - .2, z1 - inset, PanelPaper, y + .002); for (let x = x0 + .5; x < x1 - .3; x += .5) box(x - .02, z0 + inset, x + .02, z1 - inset, Lattice, y + .003); }
        else { box(x0 + inset, z0 + .2, x1 - inset, z1 - .2, PanelPaper, y + .002); for (let z = z0 + .5; z < z1 - .3; z += .5) box(x0 + inset, z - .02, x1 - inset, z + .02, Lattice, y + .003); }
        // The front edge: red lacquer and a lit lip, where the slab meets the room.
        const e = .09;
        if (side === 'n') { box(x0, z0, x1, z0 + e, Lacquer, y + .004); box(x0, z0 + e, x1, z0 + e + .05, PanelWood, y + .004); }
        if (side === 's') { box(x0, z1 - e, x1, z1, Lacquer, y + .004); box(x0, z1 - e - .05, x1, z1 - e, PanelWood, y + .004); }
        if (side === 'w') { box(x1 - e, z0, x1, z1, Lacquer, y + .004); box(x1 - e - .05, z0, x1 - e, z1, PanelWood, y + .004); }
        if (side === 'e') { box(x0, z0, x0 + e, z1, Lacquer, y + .004); box(x0 + e, z0, x0 + e + .05, z1, PanelWood, y + .004); }
      });
    }

    // Impact: dust along the four wall fronts, splinters thrown into the room.
    const age = s - t.hit;
    const fronts = [[{ x: f.x0 + Band, z: f.z1 - Band }, { x: f.x1 - Band, z: f.z1 - Band }], [{ x: f.x0 + Band, z: f.z0 + Band }, { x: f.x1 - Band, z: f.z0 + Band }],
      [{ x: f.x0 + Band, z: f.z0 + Band }, { x: f.x0 + Band, z: f.z1 - Band }], [{ x: f.x1 - Band, z: f.z0 + Band }, { x: f.x1 - Band, z: f.z1 - Band }]];
    fronts.forEach(([a, b], k) => dustLine(`crush dust ${k}`, a, b, age, { count: 9, alpha: .6, seed: k * 17, spread: .5, life: .9 }));
    if (age >= 0 && age < .8) for (let i = 0; i < 24; i++) {
      const [a, b] = fronts[i % 4], u = rand(i + 300), at = { x: a.x + (b.x - a.x) * u, z: a.z + (b.z - a.z) * u };
      const cx = (f.x0 + f.x1) / 2 - at.x, cz = (f.z0 + f.z1) / 2 - at.z, L0 = Math.hypot(cx, cz) || 1;
      const life = .45 + rand(i + 310) * .35, k = clamp(age / life);
      if (k >= 1) continue;
      const reach = .6 + rand(i + 320) * 1.2, h = 1.4 * k * (1 - k) * (.6 + rand(i + 330));
      const q = { x: at.x + cx / L0 * reach * k, z: at.z + cz / L0 * reach * k + h * Lift };
      box(q.x - .06, q.z - .02, q.x + .06, q.z + .02, WallTop.withAlpha(1 - k * k), L.fx + .02);
    }
  },
};
