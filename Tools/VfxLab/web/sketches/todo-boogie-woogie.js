// Boogie Woogie — the picture for Todo's Clap and Double Clap. Ported to C# 2026-09-28
// (Source/RimArt/Todo/BoogieWoogie*.cs, previews "Todo: boogie woogie: ..."), drawn in game by
// MapComponent_ClapTeleports. It replaced the stage-magician picture (anchor-clap-teleport.js:
// card ring, red silk puff, gold star), which is kept for a future magician hero. In game the
// white cover is 1.6 x 2.3 cells, three deep, so it hides a real pawn; the sketch's stand-in needs
// less.
//
// Mechanic (agreed 2026-09-27, built; numbers are XML): Clap, warmup 0.3 s: Todo changes places
// with a living pawn in sight within 15 cells, or with one of his stones anywhere; a stone lands
// where he stood. Double Clap, warmup 0.5 s: two ends change places (pawns in sight within 15
// cells, or stones), Todo stays. Three shared claps, one back every 10 s. A swapped hostile is
// stunned 0.5 s. Tiles never move. The picture does not change any of that.
//
// Source (anime). Stills from the "every time Todo claps" compilation, motion frame by frame from
// the wiki's GIFs (about 15 fps). The clap: short teal dashes with dark outlines burst out round
// his hands (S1 0:27, S2 1:01). The swap (S1, Yuji and Todo on Hanami): 2 white frames (0.13 s)
// with both fighters as black ink figures made of jagged strokes, the first frame in the old
// places, the second in the new, then colour again. The arrival (S2, Todo on Mahito): teal
// crescents curl round whoever arrived for about 2 frames, break up, and the teal flecks drift and
// hang for about 0.6 s. The swap is instant: nothing travels between the ends. The manga shows
// only the clap sound and the new positions.
//
// Order (C = the last palm contact = the warmup's end: Clap 0.3 s, Double Clap 0.5 s; the Double
// Clap's first contact is at 0.25 s). The clip starts late (ClapTeleport.ClipOffset) so the palms
// meet on C, as in game.
//   each contact  "pan": 16 teal dashes (0.2-0.4 cells long) with dark outlines and 6 small teal
//                 drops burst out of the palms in a level ring, 40 % of them a frame (0.065 s)
//                 later. They fly out to about 1 cell in the first 45 % of 0.4 s, turning from
//                 pointing out to pointing round (a swirl), then hang and fade; a faint pale ring
//                 opens to 1.3 cells. The Double Clap's first contact is the same at 0.6 size. The
//                 burst stays where the clap was made.
//   C - C+.13     both ends at once, on white (2.6 cells, soft): 11 jagged black strokes and 5
//                 shards in the shape of whoever stood there, then for the second half of the
//                 time whoever stands there now; a stone is a small ink blot. The white fades by
//                 C+0.19 and the arrival is in colour. Camera shake 0.02.
//   C+.11 - .33   three teal crescents with dark edges swirl round whoever arrived, opening from
//                 0.3 to 0.65 cells.
//   C+.12 - .72   14 teal flecks fly out from them, slow to a stop, turn and hang, then fade.
//   Stone end     no crescents or flecks for a stone arriving; it just lies where Todo stood.
// No line joins the two ends: the range is the whole map, and a joining line is Flying Thunder
// God's picture.
//
// Drawing: the burst, crescents and flecks are level rings at hand or chest height and the ink
// figures stand upright on screen like the pawn they cover, so nothing needs a per-facing method.
// Dashes, strokes and crescents are strips; the white is SoftDisc; the stone is
// Textures/RimArt/Anchor/ClapStone.png, which the mod already ships. The other pawns are two-disc
// stand-ins; Todo is the real clip through playClip().
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, ShaderDatabase } from '../js/engine.js';
import { playClip } from '../js/animation.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
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
const ClapWarmup = .3, DoubleWarmup = .5, Tail = .85;
const Drops = 6, BurstStart = .15, FirstScale = .6, RingReach = 1.3;
const StoneSize = .45;
// Motion. The clap burst comes in two waves a frame apart (SecondWave s), flies out in the first
// 45 % of its life, curling a little, then hangs and fades. The swap is two ink frames (S1): the
// first shows who stood at the end, the second who stands there now, InkStrokes jagged strokes
// each on white, then the white fades over InkFade. The arrival (S2): three teal crescents swirl
// round whoever arrived from SwooshFrom for SwooshLife, then Flecks teal flecks drift out and hang.
const SecondWave = .065, Curl = .25, Turn = .45, InkStrokes = 11, InkShards = 5, InkFade = .06;
const SwooshFrom = .11, SwooshLife = .22, FleckFrom = .12, Flecks = 14;
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
  for (let i = 0; i < p.dashes + Drops; i++) {
    const late = i % 5 < 2 ? SecondWave : 0, a = age - late;
    if (a < 0) continue;
    const u = clamp(a / (p.life - late)), fly = 1 - Math.pow(1 - clamp(u / .45), 3), f = 1 - smooth((u - .55) / .45);
    const k = i < p.dashes ? i : i - p.dashes, n = i < p.dashes ? p.dashes : Drops;
    const ang = (k + rand(i + 3) * .6) / n * Math.PI * 2 + Curl * fly;
    const reach = p.reach * scale * (.6 + .4 * rand(i + 5)), r = lerp(BurstStart * scale, reach, fly) + .08 * scale * u;
    const at = { x: palms.x + Math.cos(ang) * r, z: palms.z + Math.sin(ang) * r };
    if (i >= p.dashes) {
      const size = .065 * scale * (1 - .5 * u);
      draw(disc, at.x, Y + .132, at.z, size, size, 0, Ink.withAlpha(.85 * f));
      draw(disc, at.x, Y + .133, at.z, size * .6, size * .6, 0, Teal.withAlpha(f), whiteGlow);
      continue;
    }
    // Each dash turns from pointing out toward pointing round as it flies, so the burst swirls.
    const dir = ang + Turn * fly, len = (.2 + .2 * rand(i + 7)) * scale * (1 - .5 * smooth(u));
    const b = { x: at.x + Math.cos(dir) * len, z: at.z + Math.sin(dir) * len };
    line(`${key} edge ${i}`, dash(at, b), .14 * scale, Ink.withAlpha(.85 * f), undefined, Y + .13, 'both');
    line(`${key} dash ${i}`, dash(at, b), .075 * scale, Teal.withAlpha(f), whiteGlow, Y + .131, 'both');
  }
  const ring = clamp(age / (p.life * .6));
  if (ring < 1) ringAt(palms, lerp(.2, RingReach, 1 - (1 - ring) * (1 - ring)) * scale, TealPale.withAlpha(.25 * (1 - smooth(ring))), Y + .125, false, whiteGlow);
}

// S1's swap: two frames of white with the fighters as black ink shards. The first frame shows who
// stood at this end (before), the second who stands there now (after); then the white fades.
function inkFrame(key, pos, age, p, before, after) {
  if (age < 0 || age >= p.ink + InkFade) return;
  const white = age < p.ink ? 1 : 1 - smooth((age - p.ink) / InkFade), c = { x: pos.x, z: pos.z + .3 };
  sprite(c, 3.2, 3.2, White.withAlpha(.5 * white), glow, Y + .079);
  sprite(c, 2.6, 2.6, White.withAlpha(.9 * white), soft, Y + .08);
  sprite(c, 1.5, 1.5, White.withAlpha(white), soft, Y + .081);
  for (let k = 0; k < 2; k++) sprite({ x: pos.x, z: pos.z + .2 }, 1.1, 1.65, White.withAlpha(white), soft, Y + .082 + k * .0005);   // hides the pawn under it
  if (age >= p.ink) return;
  const frame = age < p.ink / 2 ? 0 : 1, who = frame ? after : before;
  if (!who) return;
  const seed = frame * 131 + Math.round(pos.x * 7 + pos.z * 3) * 17;
  const jag = (id, from, to, width) => {
    const pts = [];
    for (let j = 0; j <= 4; j++) {
      const v = j / 4, side = j % 2 ? 1 : -1, off = j > 0 && j < 4 ? side * (.02 + .04 * rand(seed + id * 9 + j)) : 0;
      pts.push({ x: lerp(from.x, to.x, v) + off, z: lerp(from.z, to.z, v) });
    }
    line(`${key} ink ${id}`, pts, width, Ink, undefined, Y + .09 + id * .0002, 'both');
  };
  if (who === 'stone') {
    for (let i = 0; i < 4; i++) {
      const ang = i * .8 + rand(seed + i) * .6, l = .12 + .08 * rand(seed + i + 5);
      jag(i, { x: pos.x - Math.cos(ang) * l, z: pos.z - Math.sin(ang) * l }, { x: pos.x + Math.cos(ang) * l, z: pos.z + Math.sin(ang) * l }, .07);
    }
    return;
  }
  // The body as tall jagged strokes, tallest in the middle where the head is; shards flare off it.
  for (let i = 0; i < InkStrokes; i++) {
    const v = i / (InkStrokes - 1) - .5, x = pos.x + v * .5 + (rand(seed + i) - .5) * .05;
    const top = pos.z + .8 - Math.abs(v) * .6 + (rand(seed + i + 20) - .5) * .1, bottom = pos.z - .08 - rand(seed + i + 40) * .1;
    jag(i, { x, z: bottom }, { x: x + (rand(seed + i + 50) - .5) * .06, z: top }, .06 + .05 * rand(seed + i + 60));
  }
  for (let i = 0; i < InkShards; i++) {
    const side = i % 2 ? 1 : -1, h = .1 + .5 * rand(seed + i + 70), from = { x: pos.x + side * .18, z: pos.z + h };
    jag(InkStrokes + i, from, { x: from.x + side * (.15 + .18 * rand(seed + i + 80)), z: from.z + .08 + .2 * rand(seed + i + 90) }, .05);
  }
}

// S2's arrival: teal crescents swirl round whoever arrived, then break into flecks that drift out
// and hang.
function arrival(key, pos, age, p) {
  const c = { x: pos.x, z: pos.z + .35 }, sw = age - SwooshFrom;
  if (sw >= 0 && sw < SwooshLife) {
    const u = sw / SwooshLife, f = 1 - smooth((u - .5) / .5), r = .3 + .35 * smooth(u), span = 1.9 * (1 - .4 * u);
    for (let k = 0; k < 3; k++) {
      const start = k * 2.094 + rand(k + 80) * .6 + u * 5.5, pts = [];
      for (let j = 0; j <= 8; j++) { const a = start + span * j / 8; pts.push({ x: c.x + Math.cos(a) * r, z: c.z + Math.sin(a) * r * .85 }); }
      line(`${key} swoosh edge ${k}`, pts, .13, Ink.withAlpha(.8 * f), undefined, Y + .1, 'both');
      line(`${key} swoosh ${k}`, pts, .07, Teal.withAlpha(f), whiteGlow, Y + .101, 'both');
    }
  }
  const fa = age - FleckFrom;
  if (fa < 0 || fa >= p.fleck) return;
  const u = fa / p.fleck, fly = 1 - Math.pow(1 - clamp(u / .35), 3), f = 1 - smooth((u - .5) / .5);
  for (let i = 0; i < Flecks; i++) {
    const ang = (i + rand(i + 60) * .7) / Flecks * Math.PI * 2, r = lerp(.35, .7 + .5 * rand(i + 61), fly) + .1 * u;
    const rot = ang + (rand(i + 63) - .5) * 2 + u * 3 * (i % 2 ? 1 : -1), size = (.08 + .08 * rand(i + 64)) * (1 - .4 * u);
    const q = { x: c.x + Math.cos(ang) * r, z: c.z + Math.sin(ang) * r * .8 + .12 * u };
    const a = { x: q.x - Math.cos(rot) * size / 2, z: q.z - Math.sin(rot) * size / 2 }, b = { x: q.x + Math.cos(rot) * size / 2, z: q.z + Math.sin(rot) * size / 2 };
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
    life: P('Burst lasts', .4, .15, .8, .02, 'Timing (s)'),
    ink: P('Two ink frames last', .13, .06, .3, .01, 'Timing (s)'),
    fleck: P('Flecks hang for', .6, .2, 1.2, .05, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Wind-up', t: 0 }, ...(t.double ? [{ name: 'First clap', t: t.contacts[0] }] : []),
      { name: 'Contact: swap, ink frames', t: t.swap }, { name: 'Colour back', t: t.swap + p.ink },
      { name: 'Burst gone', t: t.swap + p.life }, { name: 'Flecks gone', t: t.swap + FleckFrom + p.fleck }];
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
    const kind = who => who === 'stone' ? 'stone' : who ? 'pawn' : null;
    ends.forEach((e, n) => {
      inkFrame(`bw ink ${n}`, e.pos, age, p, kind(e.before), kind(e.after));
      if (e.after !== 'stone') arrival(`bw arrive ${n}`, e.pos, age, p);
    });
  },
};
