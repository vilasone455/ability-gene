// Infinity Castle: Seal and Open — castle command proposal for the Infinity Castle kit, not the game.
// Nothing in Source/RimArt draws this yet.
//
// What it is for (agreed in outline 2026-09-23; every number is a placeholder and will be an XML
// field). A command the carrier plays from the biwa room while the castle is open; each is one
// strum, strums are 1.5 s apart. Seal: pick one doorway; its paper leaves slide shut and a lacquer
// bar slides across. Nobody passes a sealed doorway, and castle doors cannot be broken, so a raider
// on the far side waits at it. Open: the same doorway again; the bar slides away and the leaves
// open. The biwa room's own doorways cannot be sealed.
//
// Order (times with the default sliders):
//   0.00  a raider in the tatami room R walks toward the doorway to the corridor that leads to the
//         biwa room
//   0.20  the bachi lifts; 0.40 strum: rings leave the biwa, the doorway's outline flashes
//   0.45  the leaves slide shut in 0.15 s; 0.60 the bar slides in from the south in 0.25 s
//   ~1.5  the raider reaches the sealed doorway and stops there, waiting
//   Scenario "seal, then open": 3.00 a second strum, the bar slides out, the leaves open in 0.2 s,
//   the raider walks on through the corridor toward the biwa room
//
// Drawing: the doorway is lib/infinity-castle.js wallDoor (leaves, bar), drawn in both wall cells;
// in game the door is a building and this is its graphic. Level shapes and stand-in pawns only, no
// per-facing method.
import { P } from './lib/six-paths-impact.js';
import {
  local, drawVoid, drawRooms, doorway, figure, carrierPlays, outline, cellCentre, Enemy, smooth, clamp, easeOut,
} from './lib/infinity-castle.js';

// Decided values.
const Walk = 2;
const castle = local([['biwa', -20, -4, 9, 9], ['corridor', -11, -1, 11, 5], ['tatami', 0, -4, 9, 11], ['stair', 2, 7, 7, 9]]);
const Door = castle.doors.find(d => d.a === 1 && d.b === 2);
const RaiderFrom = [4, 2];

function times(p) {
  const seal = p.lead, bar = seal + .2, open = p.scenario === 'seal, then open' ? p.openAt : null;
  return { seal, bar, open, end: (open ?? 1.9) + 1.4 + p.hold };
}

export default {
  kit: 'Infinity Castle', label: 'Seal and Open (sketch)', scene: false,
  params: {
    scenario: { label: 'Scenario', value: 'seal, then open', options: ['seal', 'seal, then open'], group: 'Showcase' },
    depth: { label: 'Rooms at other depths', value: true, group: 'Showcase' },
    lead: P('Before the strum', .4, .2, 1, .05, 'Timing (s)'),
    openAt: P('Open at', 3, 1.9, 5, .1, 'Timing (s)'),
    hold: P('Show the result', 1, .3, 3, .1, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Raider heads out', t: 0 }, { name: 'Seal', t: t.seal }, { name: 'Waits at the door', t: 1.5 },
    ...(t.open ? [{ name: 'Open', t: t.open }] : []),
  ]; },
  events(p) { const t = times(p); return [
    { t: t.seal, type: 'sound', def: 'RimArt_BiwaStrum' }, { t: t.bar + .2, type: 'sound', def: 'RimArt_CastleBar' },
    ...(t.open ? [{ t: t.open, type: 'sound', def: 'RimArt_BiwaStrum' }] : []),
  ]; },

  draw(s, p, { origin, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = (scene?.sun?.strength ?? .32) * .6;
    const base = { x: origin.x - .5 + 5, z: origin.z - .5 - 2 };
    drawVoid({ x: origin.x, z: origin.z }, s, { seed: 8, depth: p.depth, reach: 50 });
    drawRooms(castle, base, s);
    carrierPlays('seal carrier', castle, base, s, t.open ? [t.seal, t.open] : [t.seal], sun, strength);

    // The sealed doorway: shut and barred, and open again on the second strum.
    const openAge = t.open ? s - t.open - .05 : -1;
    const barIn = smooth((s - t.bar) / .25), barOut = openAge >= 0 ? smooth(openAge / .2) : 0;
    const shut = 1 - smooth((s - t.seal - .05) / .15), reopen = openAge >= 0 ? easeOut((openAge - .15) / .2) : 0;
    castle.doors.forEach(d => { if (d !== Door) doorway(`seal door ${d.key}`, d, base, 1); });
    doorway('seal door', Door, base, Math.max(shut, reopen), { sealed: barIn * (1 - barOut) });
    // The doorway lights up as the command lands on it.
    for (const at of [t.seal, t.open]) {
      if (at === null) continue;
      const age = s - at - .05, cells = Door.cells.map(([x, z]) => ({ x, z }));
      const fake = { w: 2.6, h: 1.6 }, mid = { x: base.x + (cells[0].x + cells[1].x) / 2 + .5, z: base.z + cells[0].z + .5 };
      if (age >= 0 && age < .45) outline(fake, mid, .6 * (1 - age / .45), .12);
    }

    // The raider walks to the doorway, waits while it is sealed, and goes on once it is open.
    const [dx, dz] = Door.cells[1], inside = cellCentre(base, dx + 1, dz), from = cellCentre(base, RaiderFrom[0], RaiderFrom[1]);
    const toDoor = Math.hypot(inside.x - from.x, inside.z - from.z), walked = s * Walk;
    let pos;
    if (walked < toDoor) pos = { x: from.x + (inside.x - from.x) * walked / toDoor, z: from.z + (inside.z - from.z) * walked / toDoor };
    else if (!t.open || s < t.open + .35) pos = inside;
    else pos = { x: inside.x - Walk * (s - t.open - .35), z: inside.z };
    // Waiting at a sealed door: a small shuffle in place.
    const waiting = walked >= toDoor && (!t.open || s < t.open + .35);
    figure({ x: pos.x + (waiting ? Math.sin(s * 5) * .04 : 0), z: pos.z }, Enemy, sun, strength);
  },
};
