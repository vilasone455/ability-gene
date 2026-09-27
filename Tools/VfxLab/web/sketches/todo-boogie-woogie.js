// Boogie Woogie — new picture for Todo's Clap and Double Clap, not the game yet. It replaces the
// stage-magician picture (anchor-clap-teleport.js: card ring, red silk puff, gold star), which is
// kept for a future magician hero. The game still draws the cards (ClapTeleportGraphics.cs).
//
// Mechanic (agreed 2026-09-27, built; numbers are XML): Clap, warmup 0.3 s: Todo changes places
// with a living pawn in sight within 15 cells, or with one of his stones anywhere; a stone lands
// where he stood. Double Clap, warmup 0.5 s: two ends change places (pawns in sight within 15
// cells, or stones), Todo stays. Three shared claps, one back every 10 s. A swapped hostile is
// stunned 0.5 s. Tiles never move. The picture does not change any of that.
//
// Source (anime; frames from the "every time Todo claps" compilation): at the clap short teal
// dashes with dark outlines burst out round his hands (S1 0:27, S2 1:01); the swap itself is
// drawn as a white frame with the fighters as black ink smears (S1 0:05, 0:24, 0:34, 0:38); in S2
// teal flecks show where someone appears (1:11, one frame). The swap is instant: nothing travels
// between the ends. The manga shows only the clap sound and the new positions.
//
// Order (C = the last palm contact = the warmup's end: Clap 0.3 s, Double Clap 0.5 s; the Double
// Clap's first contact is at 0.25 s). The clip starts late (ClapTeleport.ClipOffset) so the palms
// meet on C, as in game.
//   each contact  "pan": 16 teal dashes (0.2-0.4 cells long) with dark outlines and 6 small teal
//                 drops burst out of the palms in a level ring, from 0.15 to about 1 cell, gone in
//                 0.28 s; a faint pale ring opens to 1.3 cells. Drawn over the ink smear. The
//                 Double Clap's first contact is the same at 0.6 size. The burst stays where the
//                 clap was made, so after a Clap it is round whoever now stands in Todo's old cell.
//   C             both ends at once: a soft white flash (2.4 cells across) for 0.12 s, and whoever
//                 left is a black ink silhouette (gone in 0.1 s) with 7 brush streaks smeared 1
//                 cell toward the other end, gone in 0.18 s. Whoever arrived is already standing
//                 under it. 8 teal flecks burst off each arrival and fade by C + 0.3. Shake 0.02.
//   Stone end     the stone is simply gone and Todo stands there; the stone lies where he stood.
// No line joins the two ends: the range is the whole map, and a joining line is Flying Thunder
// God's picture.
//
// Drawing: the burst and the flecks are level rings at hand or chest height and the smears lie
// flat, so they turn with the direction and need no per-facing method. Dashes and streaks are
// strips; the flash is SoftDisc; the stone is Textures/RimArt/Anchor/ClapStone.png, which the mod
// already ships. The other pawns are two-disc stand-ins; Todo is the real clip through playClip().
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, ShaderDatabase } from '../js/engine.js';
import { playClip } from '../js/animation.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, sprite, glow, rand } from './lib/six-paths-impact.js';
import { figure, CasterColour, EnemyColour } from './lib/flying-thunder-god.js';
import { line, ringAt, whiteGlow } from './lib/goku.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const itemLayer = AltitudeLayer.Item.AltitudeFor();
const stoneMat = MaterialPool.MatFrom('RimArt/Anchor/ClapStone', ShaderDatabase.Transparent);
const disc = Meshes.disc(24, 'boogie woogie disc');

// Decided looks.
const Teal = new Color(.32, .95, .8), TealPale = new Color(.78, 1, .95), Ink = new Color(.03, .04, .05), White = new Color(1, 1, 1);
const AllyColour = new Color(.45, .62, .40);
const FirstContact = .5, SecondContact = .75;          // palm contacts in RimArt_Clap / RimArt_ClapTwice
const ClapWarmup = .3, DoubleWarmup = .5, Tail = .7;
const Drops = 6, BurstStart = .15, FirstScale = .6, RingReach = 1.3;
const FlashRadius = .7, FlashLife = .12, Streaks = 7, Flecks = 8, FleckLife = .3, StoneSize = .45;
const Scenarios = ['clap: swap with a pawn', 'clap: swap with a stone', 'double clap: two pawns'];

// A straight dash from a to b as 5 points, so a taper at both ends leaves a body in the middle.
const dash = (a, b) => [0, .25, .5, .75, 1].map(v => ({ x: lerp(a.x, b.x, v), z: lerp(a.z, b.z, v) }));

function times(p) {
  const double = p.scenario === Scenarios[2], warmup = double ? DoubleWarmup : ClapWarmup;
  const lead = (double ? SecondContact : FirstContact) - warmup;            // the clip starts this far in
  const contacts = double ? [FirstContact - lead, warmup] : [warmup];
  return { double, lead, contacts, swap: warmup, end: warmup + Tail };
}

// "Pan": dashes and drops bursting out of the palms in a level ring. age is seconds since the contact.
function panBurst(key, palms, age, p, scale) {
  if (age < 0 || age >= p.life) return;
  const u = age / p.life, out = 1 - (1 - u) * (1 - u), f = 1 - smooth(u);
  for (let i = 0; i < p.dashes; i++) {
    const ang = (i + rand(i + 3) * .6) / p.dashes * Math.PI * 2, reach = p.reach * scale * (.6 + .4 * rand(i + 5));
    const r = lerp(BurstStart * scale, reach, out), len = (.2 + .2 * rand(i + 7)) * scale * (1 - .4 * u);
    const c = Math.cos(ang), z = Math.sin(ang);
    const a = { x: palms.x + c * r, z: palms.z + z * r }, b = { x: palms.x + c * (r + len), z: palms.z + z * (r + len) };
    line(`${key} edge ${i}`, dash(a, b), .14 * scale, Ink.withAlpha(.85 * f), undefined, Y + .13, 'both');
    line(`${key} dash ${i}`, dash(a, b), .075 * scale, Teal.withAlpha(f), whiteGlow, Y + .131, 'both');
  }
  for (let i = 0; i < Drops; i++) {
    const ang = (i + .5 + rand(i + 20) * .5) / Drops * Math.PI * 2, r = lerp(BurstStart, p.reach * .8, out) * scale * (.7 + .3 * rand(i + 21));
    const at = { x: palms.x + Math.cos(ang) * r, z: palms.z + Math.sin(ang) * r }, size = .065 * scale * (1 - .5 * u);
    draw(disc, at.x, Y + .132, at.z, size, size, 0, Ink.withAlpha(.85 * f));
    draw(disc, at.x, Y + .133, at.z, size * .6, size * .6, 0, Teal.withAlpha(f), whiteGlow);
  }
  ringAt(palms, lerp(.2, RingReach, out) * scale, TealPale.withAlpha(.25 * f), Y + .125, false, whiteGlow);
}

// Whoever left an end: a black ink silhouette smeared toward the other end, with brush streaks.
function inkSmear(key, pos, dir, age, p) {
  if (age < 0 || age >= p.smearLife) return;
  const u = age / p.smearLife, f = 1 - smooth((u - .3) / .7), body = 1 - smooth(age / .1), reach = p.smear * smooth(Math.min(1, age / .06));
  const px = -dir.z, pz = dir.x;
  // The silhouette, drawn three times sliding along the smear, fainter each time.
  for (let k = 0; k < 3; k++) {
    const d = reach * k * .22, a = .9 * body * (1 - k * .3);
    draw(disc, pos.x + dir.x * d, Y + .1 + k * .001, pos.z + .18 + dir.z * d, .24, .33, 0, Ink.withAlpha(a));
    draw(disc, pos.x + dir.x * d, Y + .1 + k * .001, pos.z + .58 + dir.z * d, .17, .18, 0, Ink.withAlpha(a));
  }
  for (let i = 0; i < Streaks; i++) {
    const h = .02 + .72 * (i + rand(i + 40) * .5) / Streaks, side = (rand(i + 41) - .5) * .3, len = reach * (.55 + .45 * rand(i + 42));
    const start = { x: pos.x + px * side, z: pos.z + h + pz * side }, pts = [];
    for (let j = 0; j <= 4; j++) {
      const v = j / 4, wob = (rand(i * 9 + j + 43) - .5) * .08 * v;
      pts.push({ x: start.x + dir.x * len * v + px * wob, z: start.z + dir.z * len * v + pz * wob });
    }
    line(`${key} streak ${i}`, pts, .08 + .08 * rand(i + 44), Ink.withAlpha(f), undefined, Y + .102, 'end');
  }
}

// Teal flecks bursting off whoever arrived.
function flecks(key, pos, age) {
  if (age < 0 || age >= FleckLife) return;
  const u = age / FleckLife, out = 1 - (1 - u) * (1 - u), f = 1 - smooth(u);
  for (let i = 0; i < Flecks; i++) {
    const ang = (i + rand(i + 60) * .7) / Flecks * Math.PI * 2, r = lerp(.25, .6 + .25 * rand(i + 61), out);
    const c = Math.cos(ang), z = Math.sin(ang), centre = { x: pos.x, z: pos.z + .35 };
    const a = { x: centre.x + c * r, z: centre.z + z * r * .8 }, b = { x: centre.x + c * (r + .1), z: centre.z + z * (r + .1) * .8 };
    line(`${key} edge ${i}`, dash(a, b), .08, Ink.withAlpha(.8 * f), undefined, Y + .07, 'both');
    line(`${key} fleck ${i}`, dash(a, b), .04, Teal.withAlpha(f), whiteGlow, Y + .071, 'both');
  }
}

export default {
  kit: 'Anchor', label: 'Boogie Woogie clap (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    distance: P('Distance between the ends (cells)', 6, 2, 15, 1, 'Showcase'),
    dashes: P('Dashes in the burst', 16, 8, 28, 1, 'Clap burst'),
    reach: P('Burst reach (cells)', 1, .5, 1.8, .05, 'Clap burst'),
    life: P('Burst lasts', .28, .12, .6, .02, 'Timing (s)'),
    smear: P('Ink smear length (cells)', 1, .3, 2, .05, 'Swap'),
    smearLife: P('Ink smear lasts', .18, .08, .4, .02, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Wind-up', t: 0 }, ...(t.double ? [{ name: 'First clap', t: t.contacts[0] }] : []),
      { name: 'Contact: swap', t: t.swap }, { name: 'Burst gone', t: t.swap + p.life }];
  },
  events(p) {
    const t = times(p);
    return [...t.contacts.map(c => ({ t: c, type: 'sound', def: 'AG_Clap' })), { t: t.swap, type: 'shake', value: .02 }];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const half = p.distance / 2, stone = p.scenario === Scenarios[1], swapped = s >= t.swap, age = s - t.swap;
    const west = { x: o.x - half, z: o.z }, east = { x: o.x + half, z: o.z };
    const home = t.double ? { x: o.x, z: o.z + 2.5 } : west;

    // What stands at each end before and after the swap.
    const ends = t.double
      ? [{ pos: west, before: 'enemy', after: 'ally' }, { pos: east, before: 'ally', after: 'enemy' }]
      : stone
        ? [{ pos: west, before: 'todo', after: 'stone' }, { pos: east, before: 'stone', after: 'todo' }]
        : [{ pos: west, before: 'todo', after: 'enemy' }, { pos: east, before: 'enemy', after: 'todo' }];

    let todoAt = home;
    for (const e of ends) {
      const who = swapped ? e.after : e.before;
      if (who === 'todo') todoAt = e.pos;
      else if (who === 'stone') sprite(e.pos, StoneSize, StoneSize, White, stoneMat, itemLayer);
      else if (who) figure(e.pos, who === 'enemy' ? EnemyColour : AllyColour, 1, 0, sun, strength);
    }
    const clip = playClip(t.double ? 'RimArt_ClapTwice' : 'RimArt_Clap', s + t.lead, todoAt, { scene, shirt: CasterColour });

    // The burst at every contact, where the clap was made: the palms are on the body's centre line
    // at the hand's height.
    const clapped = ends.find(e => e.before === 'todo')?.pos ?? home;
    if (clip?.hand) t.contacts.forEach((c, k) => {
      const last = k === t.contacts.length - 1;
      panBurst(`bw pan ${k}`, { x: clapped.x, z: clapped.z + clip.hand.z - todoAt.z }, s - c, p, last ? 1 : FirstScale);
    });

    if (!swapped) return;
    ends.forEach((e, n) => {
      const other = ends[1 - n].pos, dx = other.x - e.pos.x, dz = other.z - e.pos.z, len = Math.hypot(dx, dz) || 1;
      if (age < FlashLife) {
        const w = 1 - age / FlashLife, c = { x: e.pos.x, z: e.pos.z + .35 };
        sprite(c, FlashRadius * 3.4, FlashRadius * 3.4, White.withAlpha(.9 * w), glow, Y + .08);
        sprite(c, FlashRadius * 2, FlashRadius * 2, White.withAlpha(w), glow, Y + .081);
      }
      if (e.before !== 'stone') inkSmear(`bw smear ${n}`, e.pos, { x: dx / len, z: dz / len }, age, p);
      if (e.after !== 'stone') flecks(`bw flecks ${n}`, e.pos, age);
    });
  },
};
