// Stone throw — new picture for Todo's Mark, not the game yet. It replaces the stage-magician card
// flick (anchor-mark-flick.js, kept for a future magician hero). The game still flies a card
// (MarkFlick.cs, ClapTeleportGraphics.FlyingCard) and drops the stone at the end.
//
// Mechanic (agreed 2026-09-27, built; numbers are XML): Mark throws a stone onto a standable cell
// within 9.9 cells, warmup 0.5 s, cooldown 10 s. Up to 3 stones at once; they lie forbidden and
// fade after about a day. A clap reaches a stone from anywhere on the map. Targeting one of your
// own stones takes it back. The picture does not change any of that.
//
// Source: in S2 ep 21 Todo throws a coat hanger he has put his cursed energy into, then claps to
// swap with it behind the enemy's head (the wiki; frames in "Itadori & Aoi vs Mahito Part 1",
// YDThPKXGWI4): 0:56 the hanger on the road, 1:00 a hard side-arm throw with a bright blue streak
// leaving the hand, 1:01 the hanger spinning in flight as a glowing ring, 1:02-1:03 it glows
// behind the head. The glow there is bluer than the clap's teal; the kit keeps one colour, teal,
// so a stone reads as the clap's.
//
// Order (the clips' own times, MarkFlick.cs: the stone leaves the hand at 0.38 s and lands at the
// warmup's end, 0.5 s):
//   Throw (RimArt_MarkFlick for now; a stone-throw clip comes with the other clips, last):
//   0.10  a stone is in the hand; a teal glow grows round it and 4 small teal sparks crawl on it
//   0.38  it leaves the hand, spinning, inside a spinning teal ring (the hanger's spin), with a
//         teal streak behind it; a low arc (0.35 cells high at 5+ cells) with its shadow below
//   0.50  it lands on the cell: 4 dust puffs, a teal ring on the floor opens 0.25 -> 0.6 cells,
//         6 short teal dashes spring out, a small bounce
//   after it lies there with a faint teal edge that breathes (every 2.4 s), and a glint every 3 s:
//         the charged stone, drawn as long as it exists
//   Take back (RimArt_MarkCatch): from 0.3 the stone glows brighter and its floor ring pulls in;
//   at 0.5 it jumps off the cell and flies back, caught at 0.62 with a small teal flare; gone into
//   the hand by 0.9.
// No camera shake and no sound burst: this is not an attack. One other stone lies near Todo the
// whole time to show the resting look and that stones stay.
//
// Drawing: the stone is Textures/RimArt/Anchor/ClapStone.png (shipped); the glow, ring and streak
// are SoftDisc sprites, a ring mesh and strips. Everything is a point on a path or a level ring,
// so there is no per-facing method; the facing work is in the clips. The hand is the clip's, which
// in C# is MarkFlick.Hand (a fixed reach and height along the aim).
import { AltitudeLayer, Color, MaterialPool, Mathf, ShaderDatabase } from '../js/engine.js';
import { playClip } from '../js/animation.js';
import { P, Y, Floor, Lift, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import { CasterColour } from './lib/flying-thunder-god.js';
import { line, ringAt, whiteGlow } from './lib/goku.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), itemLayer = AltitudeLayer.Item.AltitudeFor();
const stoneMat = MaterialPool.MatFrom('RimArt/Anchor/ClapStone', ShaderDatabase.Transparent);

// Decided looks, the Boogie Woogie sketch's teal.
const Teal = new Color(.32, .95, .8), TealPale = new Color(.78, 1, .95), Ink = new Color(.03, .04, .05);
const White = new Color(1, 1, 1), Dust = new Color(.52, .45, .37);
// The clips' own times (MarkFlick.cs) and the ability's warmup.
const Release = .38, Place = .5, FlickLength = .8, CatchTime = .62, CatchLength = .95, Tail = 1.4;
const Charge = .1, HandH = .4, Arc = .35, StoneRest = .45, StoneHand = .3, SpinRing = .2;
const LandLife = .3, Breath = 2.4, GlintEvery = 3, GlintLife = .25;
const Scenarios = ['throw a stone', 'take a stone back'];

const dash = (a, b) => [0, .25, .5, .75, 1].map(v => ({ x: lerp(a.x, b.x, v), z: lerp(a.z, b.z, v) }));

// The stone at rest on its cell: the texture, a faint teal edge that breathes, a glint now and
// then. charge 0..1 brightens it (taking it back). seed offsets its clock from other stones.
function restingStone(key, pos, s, alpha, charge, seed) {
  if (alpha <= 0) return;
  const breath = .5 + .5 * Math.sin((s / Breath + rand(seed)) * Math.PI * 2);
  sprite(pos, StoneRest * 2.4, StoneRest * 2.4, Teal.withAlpha(alpha * (.3 + .15 * breath + .5 * charge)), glow, Floor + .03);
  sprite(pos, StoneRest, StoneRest, White.withAlpha(alpha), stoneMat, itemLayer);
  const g = (s + rand(seed + 1) * GlintEvery) % GlintEvery;
  if (g < GlintLife) {
    const u = g / GlintLife, a = Math.sin(u * Math.PI) * alpha, c = { x: pos.x + .08, z: pos.z + .08 };
    sprite(c, .18, .18, TealPale.withAlpha(.8 * a), glow, Y + .01);
    line(`${key} glint h`, dash({ x: c.x - .12, z: c.z }, { x: c.x + .12, z: c.z }), .03, TealPale.withAlpha(a), whiteGlow, Y + .011, 'both');
    line(`${key} glint v`, dash({ x: c.x, z: c.z - .09 }, { x: c.x, z: c.z + .09 }), .03, TealPale.withAlpha(a), whiteGlow, Y + .011, 'both');
  }
}

// The stone in the air (or in the hand), with its glow; ring > 0 adds the spinning ring.
function flyingStone(key, pos, size, spin, glowAlpha, ring) {
  sprite(pos, size * 3.2, size * 3.2, Teal.withAlpha(.6 * glowAlpha), glow, Y + .02);
  sprite(pos, size, size, White, stoneMat, Y + .021, spin);
  if (ring <= 0) return;
  ringAt(pos, SpinRing, Teal.withAlpha(.35 * ring), Y + .022, false, whiteGlow);
  // Two bright arcs running round the ring.
  for (let k = 0; k < 2; k++) {
    const pts = [];
    for (let i = 0; i <= 6; i++) {
      const a = spin * Mathf.Deg2Rad + k * Math.PI + i / 6 * 1.4;
      pts.push({ x: pos.x + Math.cos(a) * SpinRing, z: pos.z + Math.sin(a) * SpinRing });
    }
    line(`${key} arc ${k}`, pts, .045, TealPale.withAlpha(ring), whiteGlow, Y + .023, 'both');
  }
}

export default {
  kit: 'Anchor', label: 'Stone throw (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    aim: P('Target direction (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Target distance (cells)', 6, 1, 9.9, .1, 'Showcase'),
    spin: P('Stone spin in flight (degrees per s)', 1080, 0, 2880, 60, 'Stone'),
    streak: P('Streak length (share of the path)', .35, 0, .7, .05, 'Stone'),
    land: P('Floor ring on landing (cells)', .6, .3, 1.2, .05, 'Stone'),
  },
  duration(p) { return (p.scenario === Scenarios[1] ? CatchLength : FlickLength) + Tail; },
  phases(p) {
    return p.scenario === Scenarios[1]
      ? [{ name: 'Reach out', t: 0 }, { name: 'Stone glows', t: .3 }, { name: 'Stone leaves the cell', t: Place }, { name: 'Caught', t: CatchTime }]
      : [{ name: 'Charge', t: Charge }, { name: 'Leaves the hand', t: Release }, { name: 'Lands', t: Place }, { name: 'Resting', t: Place + LandLife }];
  },
  events() { return []; },

  draw(s, p, { origin: o, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, dir = { x: Math.cos(a), z: Math.sin(a) };
    const todo = { x: o.x - dir.x * p.distance / 2, z: o.z - dir.z * p.distance / 2 };
    const cell = { x: todo.x + dir.x * p.distance, z: todo.z + dir.z * p.distance };
    const back = p.scenario === Scenarios[1];

    // Another of Todo's stones, thrown earlier, lying 1.5 cells to his south-east.
    restingStone('st other', { x: todo.x + 1.2, z: todo.z - .9 }, s, 1, 0, 7);

    const clip = playClip(back ? 'RimArt_MarkCatch' : 'RimArt_MarkFlick', s, todo, { aim: p.aim, scene, shirt: CasterColour });
    if (!clip?.hand) return;

    // The flight: from the hand to the cell, or back. h is height in cells.
    const start = back ? Place : Release, end = back ? CatchTime : Place, u = (s - start) / (end - start);
    const from = back ? cell : clip.itemAtRelease ?? clip.hand, to = back ? clip.hand : cell;
    const high = Arc * Math.min(1, p.distance / 5);
    const path = k => {
      const h = (back ? lerp(0, HandH, k) : lerp(HandH, 0, k)) + high * Math.sin(Math.PI * k);
      // from/to are screen points; the hand's is already lifted, so take the lift out and put the arc's in.
      const baseH = back ? lerp(0, HandH, k) : lerp(HandH, 0, k);
      return { x: lerp(from.x, to.x, k), z: lerp(from.z, to.z, k) + (h - baseH) * Lift, h };
    };

    if (!back) {
      // In the hand: the stone and its charge, until it leaves.
      if (s >= Charge && s < Release) {
        const c = smooth((s - Charge) / (Release - Charge));
        flyingStone('st hand', clip.hand, StoneHand, 0, c, 0);
        for (let i = 0; i < 4; i++) {
          const ang = s * 9 + i * 1.57 + rand(i) * .5, r = .12 + .06 * Math.sin(s * 23 + i);
          const q = { x: clip.hand.x + Math.cos(ang) * r, z: clip.hand.z + Math.sin(ang) * r };
          line(`st crawl ${i}`, dash(q, { x: q.x + Math.cos(ang + 1.6) * .1, z: q.z + Math.sin(ang + 1.6) * .1 }), .035, Teal.withAlpha(c), whiteGlow, Y + .025, 'both');
        }
      }
      // Landed: bounce, dust, the floor ring and the dashes, then the resting stone.
      if (s >= Place) {
        const age = s - Place, bounce = age < .18 ? .06 * Math.sin(age / .18 * Math.PI) : 0;
        restingStone('st thrown', { x: cell.x, z: cell.z + bounce * Lift }, s, 1, age < LandLife ? 1 - age / LandLife : 0, 11);
        if (age < LandLife) {
          const k = age / LandLife, out = 1 - (1 - k) * (1 - k), f = 1 - smooth(k);
          ringAt(cell, lerp(.25, p.land, out), Teal.withAlpha(.8 * f), Floor + .04, false, whiteGlow);
          for (let i = 0; i < 6; i++) {
            const ang = (i + rand(i + 30) * .6) / 6 * Math.PI * 2, r = lerp(.15, p.land * .9, out);
            const q = { x: cell.x + Math.cos(ang) * r, z: cell.z + Math.sin(ang) * r }, t2 = { x: q.x + Math.cos(ang) * .16, z: q.z + Math.sin(ang) * .16 };
            line(`st land edge ${i}`, dash(q, t2), .09, Ink.withAlpha(.7 * f), undefined, Y + .03, 'both');
            line(`st land dash ${i}`, dash(q, t2), .05, Teal.withAlpha(f), whiteGlow, Y + .031, 'both');
          }
          for (let i = 0; i < 4; i++) {
            const ang = i * 1.57 + .4 + rand(i + 40), d = .1 + .3 * out;
            sprite({ x: cell.x + Math.cos(ang) * d, z: cell.z + Math.sin(ang) * d * .7 + .05 * out }, .22 + .2 * out, .18 + .16 * out, Dust.withAlpha(.45 * f), soft, Y + .005);
          }
        }
      }
    } else {
      // Take back: the stone brightens from 0.3, then leaves its cell at Place.
      if (s < Place) restingStone('st taken', cell, s, 1, smooth((s - .3) / (Place - .3)), 11);
      if (s >= .3 && s < Place) {
        const k = (s - .3) / (Place - .3);
        ringAt(cell, lerp(p.land, .2, smooth(k)), Teal.withAlpha(.7 * k), Floor + .04, false, whiteGlow);
      }
      if (s >= CatchTime && s < CatchTime + .28) {
        const k = (s - CatchTime) / .28, size = StoneHand * (1 - smooth((k - .4) / .6));
        if (size > .01) flyingStone('st caught', clip.hand, size, 0, 1 - k, 0);
        sprite(clip.hand, .5 * (.6 + .4 * k), .5 * (.6 + .4 * k), TealPale.withAlpha(.8 * (1 - k) ** 2), glow, Y + .03);
      }
    }

    // In flight: shadow under it, the streak behind it, the stone and its spinning ring.
    if (u >= 0 && u < 1) {
      const pos = path(u), ground = { x: pos.x, z: pos.z - pos.h * Lift };
      sprite({ x: ground.x + sun.x * pos.h, z: ground.z + sun.z * pos.h }, .22, .12, Ink.withAlpha(strength * .7), soft, shadowLayer);
      if (p.streak > 0) {
        // Stone first, so the 'end' taper thins it toward the tail.
        const pts = [];
        for (let i = 0; i <= 8; i++) pts.push(path(Math.max(0, u - p.streak * i / 8)));
        line('st streak glow', pts, .16, Teal.withAlpha(.45), whiteGlow, Y + .018, 'end');
        line('st streak core', pts, .05, TealPale.withAlpha(.8), whiteGlow, Y + .019, 'end');
      }
      flyingStone('st flying', pos, lerp(StoneHand, StoneRest * .9, back ? 1 - u : u), p.spin * (s - start), 1, 1);
    }
  },
};
