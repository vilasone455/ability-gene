// Vacuum: Suck — weapon ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed 2026-09-22; the numbers are placeholders and will be XML fields). The
// pawn carries a conjured vacuum cleaner (Hunter x Hunter, Shizuku's Blinky). It is a normal blunt
// melee weapon. Suck targets a cell up to 12 cells away with line of sight, 0.3 s wind-up. Every
// loose item, chunk, corpse and filth cell within 2 cells of the target flies into the wand's head
// over about 0.6 s and is swallowed. A pawn standing in the radius loses its held weapon into it
// too. Cooldown 8 s, shared with Spit. The canister holds any amount, but only the last thing
// swallowed can come back out (Spit); everything older is gone. That rule is the whole design:
// Suck takes, Spit returns one thing.
//
// The canister is conjured (agreed 2026-09-22). Between casts the pawn carries only the wand, with
// its floor head on the ground and the hose coiled at the hip, a one-cell weapon, and moves
// normally. On cast the canister rises out of the floor on the cell beside the caster, the hose
// unwinds out to it, and after the result it sinks back into the floor.
//
// Order (times with the default sliders):
//   0.00  wand at rest, no canister
//   0.25  wind-up: the canister rises out of the floor with a dust ring, the wand lifts and points,
//         the hose grows to the canister, the mouth opens, the floor ring shows the true radius
//   0.55  pull: the chunk lifts first, then the filth streams up, then the rifle leaves the hand
//   0.97  swallow: each thing enters the head and runs down the hose as a bulge; when a bulge
//         lands the canister swells a step and the eyes blink
//   1.60  result: the ring fades and the mouth closes. The chunk and filth are gone, the pawn
//         stands unarmed, the canister stays larger by the kg it took (chunk 20, rifle 4, filth 0)
//   2.80  sink: the canister goes back into the floor and the hose coils up again
//
// Drawing: the weapon itself is lib/vacuum.js (shared with Spit); see its header. Caster, enemy,
// chunk, filth and rifle are stand-ins. Dust uses the Six Paths Puff texture as a stand-in.
import { Color, Mathf } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Body, Y, Floor, Lift, sprite, band, trail, circle, soft, rand } from './lib/six-paths-impact.js';
import { drawVacuum, frame, figure, chunk, rifle, bump, swellFor, Mass, disc, puff, pawnLayer, Pale, Dust, Blood, HandH, Bulge, Lead, Rise, Sink, Tail } from './lib/vacuum.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
const Stagger = .14;                       // the next thing starts its flight this much later
const Flight = .7;                         // share of the pull each thing spends in the air
const Blink = .16, Marked = 1.0;
const easeIn = x => Math.pow(clamp(x), 1.7);

function itemsFor(p) {
  const list = [
    { kind: 'chunk', x: .9, z: .6, h: 0 },
    { kind: 'filth', x: -.8, z: -.7, h: 0 },
  ];
  if (p.scenario === 'armed pawn') list.push({ kind: 'rifle', x: .80, z: -1.13, h: HandH });
  return list;
}
function times(p) {
  const rise0 = Lead, pull0 = Lead + p.windup, flight = p.pull * Flight, count = itemsFor(p).length;
  const swallow = pull0 + flight, result = pull0 + (count - 1) * Stagger + flight + Bulge, sink0 = result + p.hold;
  return { rise0, pull0, flight, swallow, result, sink0, end: sink0 + Sink + Tail };
}

export default {
  kit: 'Vacuum', label: 'Suck (sketch)',
  params: {
    actors: { label: 'Show caster and pawn', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Target distance (cells)', 5, 3, 12, .5, 'Showcase'),
    scenario: { label: 'In the radius', value: 'armed pawn', options: ['armed pawn', 'loose things only'], group: 'Showcase' },
    windup: P('Wind-up', .3, .1, .8, .05, 'Timing (s)'),
    pull: P('Pull', .6, .3, 1.5, .05, 'Timing (s)'),
    hold: P('Show the result', 1.2, .3, 3, .1, 'Timing (s)'),
    radius: P('Radius (cells)', 2, 1, 4, .5, 'Rule'),
    inside: P('Already inside (kg)', 20, 0, 100, 5, 'Rule'),
    slack: P('Hose slack (cells)', .6, 0, 1.5, .05, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Wand', t: 0 }, { name: 'Rise', t: t.rise0 }, { name: 'Pull', t: t.pull0 }, { name: 'Swallow', t: t.swallow }, { name: 'Result', t: t.result }, { name: 'Sink', t: t.sink0 },
  ]; },
  events(p) { return [{ t: 0, type: 'sound', def: 'RimArt_VacuumSuck' }]; },

  draw(s, p, { origin: centre, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const f = frame(p.aim, sun), { place } = f;
    // The scene is centred on the whole span: the caster at one end, the target cell at the other.
    const o = place(centre, p.distance / 2, 0), caster = place(centre, -p.distance / 2, 0);
    const items = itemsFor(p);
    const start = i => t.pull0 + i * Stagger, arrive = i => start(i) + t.flight, land = i => arrive(i) + Bulge;

    // How far the suction is on: rises in the wind-up, holds through the pull, fades in the result.
    const suck = smooth((s - t.rise0) / p.windup) * (1 - smooth((s - t.result) / .35));
    // The canister's size is its fullness in kg: what was already inside plus each thing as it lands.
    const landed = items.filter((_, i) => s >= land(i)).length;
    const kg = p.inside + items.reduce((m, it, i) => m + (s >= land(i) ? Mass[it.kind] : 0), 0);
    const pulse = items.reduce((m, _, i) => m + bump((s - land(i)) / .25), 0);
    const blink = items.reduce((m, _, i) => Math.max(m, bump((s - land(i)) / Blink)), 0);
    const present = s < t.rise0 ? 0 : s < t.sink0 ? smooth((s - t.rise0) / Rise) : 1 - smooth((s - t.sink0) / Sink);
    const churn = Math.max(bump((s - t.rise0) / Rise), bump((s - t.sink0) / Sink));
    const bulges = items.map((_, i) => (s - arrive(i)) / Bulge).filter(u => u > 0 && u < 1);

    // ---- the weapon and the caster ------------------------------------------------------------
    if (p.actors) figure(caster, new Color(.93, .50, .13), sun, strength);
    const v = drawVacuum(f, caster, o, { present, churn, swell: swellFor(kg), pulse, blink, mouthOpen: .18 + .82 * suck * (1 - .6 * blink), bulges, slack: p.slack, actors: p.actors, sun, strength });
    const tipG = v.tipG, tipS = v.tipS;

    // ---- the target area: true radius on the floor, a faint cone from the head, air streaks -----
    circle(o, p.radius, .55 * suck, Floor, Pale);
    if (suck > .01) {
      const ang = Math.atan2(o.z - tipS.z, o.x - tipS.x), nx = -Math.sin(ang), nz = Math.cos(ang);
      band('vacuum cone', [{ x: tipS.x - nx * .1, z: tipS.z - nz * .1 }, { x: o.x - nx * p.radius, z: o.z - nz * p.radius }],
        [{ x: tipS.x + nx * .1, z: tipS.z + nz * .1 }, { x: o.x + nx * p.radius, z: o.z + nz * p.radius }], Pale.withAlpha(.07 * suck), Y - .06);
      for (let i = 0; i < 18; i++) {
        const th = rand(i) * TAU, d = Math.sqrt(rand(i + 30)) * p.radius;
        const g0 = { x: o.x + Math.cos(th) * d, z: o.z + Math.sin(th) * d };
        const u = ((s - t.pull0) * (1.3 + rand(i + 90) * .6) + rand(i + 60)) % 1;
        const pts = [];
        for (const k of [-.07, -.035, 0, .02]) {
          const vv = clamp(u + k), h = lerp(0, HandH, vv) + Math.sin(vv * Math.PI) * .12;
          pts.push({ x: lerp(g0.x, tipG.x, vv), z: lerp(g0.z, tipG.z, vv) + h * Lift });
        }
        trail(`vacuum streak ${i}`, pts, .06, Pale.withAlpha(.45 * suck * Math.sin(u * Math.PI)), Y - .05);
      }
    }

    // ---- the things in the radius, each at rest, in flight, or gone ---------------------------
    const flightOf = i => {
      const it = items[i], g0 = { x: o.x + it.x, z: o.z + it.z };
      if (s < start(i)) return { g: g0, h: it.h, u: 0, scale: 1 };
      if (s >= arrive(i)) return null;
      const u = (s - start(i)) / t.flight, e = easeIn(u);
      return { g: { x: lerp(g0.x, tipG.x, e), z: lerp(g0.z, tipG.z, e) }, h: lerp(it.h, HandH, e) + Math.sin(u * Math.PI) * .3, u, scale: 1 - .8 * clamp((u - .55) / .45) };
    };
    items.forEach((it, i) => {
      const fl = flightOf(i);
      if (it.kind === 'filth') {
        const drained = clamp((s - start(i)) / t.flight), g0 = { x: o.x + it.x, z: o.z + it.z };
        sprite(g0, 1.0 * (1 - .4 * drained), .75 * (1 - .4 * drained), Blood.withAlpha(.85 * (1 - drained)), soft, Floor + .02);
        sprite({ x: g0.x + .25, z: g0.z + .2 }, .4, .3, Blood.withAlpha(.7 * (1 - drained)), soft, Floor + .02);
        for (let k = 0; k < 8; k++) {
          const vv = clamp((s - start(i) - k * .045) / (t.flight * .8));
          if (vv <= 0 || vv >= 1) continue;
          const e = easeIn(vv), jx = (rand(k + 200) - .5) * .5, jz = (rand(k + 210) - .5) * .4;
          const h = lerp(0, HandH, e) + Math.sin(vv * Math.PI) * .25;
          const q = { x: lerp(g0.x + jx, tipG.x, e), z: lerp(g0.z + jz, tipG.z, e) + h * Lift };
          draw(disc, q.x, Y - .04, q.z, .07 * (1 - .5 * vv), .09 * (1 - .5 * vv), 0, Blood);
        }
        return;
      }
      if (!fl) return;
      const layer = fl.u > 0 ? Y - .03 : pawnLayer - .004;
      if (it.kind === 'chunk') chunk(fl.g, fl.h, fl.scale, layer, sun, strength);
      else if (it.kind === 'rifle') rifle('vacuum rifle', fl.g, fl.h, fl.scale, 25 + fl.u * 520, layer, sun, strength);
    });

    // The pawn that stood in the radius. It keeps standing; after its rifle is gone it is marked for a moment.
    const rifleIdx = items.findIndex(it => it.kind === 'rifle');
    if (p.actors && rifleIdx >= 0) {
      const E = { x: o.x + items[rifleIdx].x - .25, z: o.z + items[rifleIdx].z - .02 };
      figure(E, new Color(.55, .38, .27), sun, strength);
      const dazed = s - start(rifleIdx);
      if (dazed >= 0) {
        const fade = 1 - smooth((dazed - Marked) / .3);
        for (let i = 0; i < 3; i++) {
          const turn = s * 5 + i * 2.094;
          sprite({ x: E.x + Math.cos(turn) * .2, z: E.z + .86 + Math.sin(turn) * .06 }, .08, .08, Pale.withAlpha(.9 * fade), soft, Y + .03);
        }
      }
    }

    // A little dust lifts off the floor inside the radius while the pull is on.
    if (s >= t.pull0 && s < t.result) for (let i = 0; i < 6; i++) {
      const life = .5 + rand(i + 300) * .3, u = ((s - t.pull0) / life + rand(i + 310)) % 1;
      const th = rand(i + 320) * TAU, d = rand(i + 330) * p.radius * .9;
      const g = { x: o.x + Math.cos(th) * d, z: o.z + Math.sin(th) * d };
      sprite({ x: lerp(g.x, tipG.x, u * .25), z: lerp(g.z, tipG.z, u * .25) + u * .35 * Lift }, .3 + u * .3, .25 + u * .25, Dust.withAlpha(Math.sin(u * Math.PI) * .45 * suck), puff, Y - .045);
    }
  },
};
