// Stone throw — the picture for Todo's Mark ("stone" in game). Ported to C# 2026-09-28
// (Source/RimArt/Todo/StoneThrow*.cs, previews "Todo: stone throw: ..."), drawn in game by
// MapComponent_MarkFlicks (throw, skid, take back) and MapComponent_Anchors (the stone at rest);
// the item is hidden while it skids. The hand follows tables read off the two clips. It replaced
// the stage-magician card flick (anchor-mark-flick.js, kept for a future magician hero).
//
// Mechanic (agreed 2026-09-27, built; numbers are XML): Mark throws a stone onto a standable cell
// within 9.9 cells, warmup 0.5 s, cooldown 10 s. Up to 3 stones at once; they lie forbidden and
// fade after about a day. A clap reaches a stone from anywhere on the map. Targeting one of your
// own stones takes it back. The picture does not change any of that.
//
// Source: in S2 ep 21 Todo throws a coat hanger he has put his cursed energy into, then claps to
// swap with it behind the enemy's head (the wiki). Motion frame by frame from the wiki's GIF of it
// (about 15 fps): in his hand the hanger sits in a teal flame outline with a dark edge, the tongues
// flickering (1.47-1.8 s); the release is a wide blade of light along the arm (1.93 s); in flight
// the spin shows as a big glowing ring with a bright rim and a faintly lit inside, shrinking as it
// goes (2.0-2.7 s). The glow there is bluer than the clap's teal; the kit keeps one colour, teal,
// so a stone reads as the clap's.
//
// Order (the clips' own times, MarkFlick.cs: the stone leaves the hand at 0.38 s and lands at the
// warmup's end, 0.5 s):
//   Throw (RimArt_MarkFlick for now; a stone-throw clip comes with the other clips, last):
//   0.10  the stone is in the hand inside a teal flame outline (16 points, the upper tongues long
//         and leaning in, flickering 15 times a second), growing until it leaves
//   0.38  release: a blade of light (dark edge, teal, white core) sweeps 0.95 cells along the
//         throw, gone in 0.1 s. The stone flies spinning, its aura smaller, inside a spin ring
//         (0.28 cells: dark edge, teal rim, pale inner rim, lit inside, two white arcs running
//         round) that opens in the first quarter of the flight; a streak (dark edge, teal, white
//         core) behind it; a low arc (0.35 cells high at 5+ cells) with its shadow below
//   0.50  it touches down 0.18 cells short of the cell and slides in over 0.2 s, its spin dying
//         away, dust kicked up along the skid; the spin ring sinks in 0.15 s while a teal floor
//         ring opens 0.25 -> 0.6 cells and 6 short teal dashes spring out; the aura fades by 0.8
//   after it lies there with a faint teal edge that breathes (every 2.4 s), and a glint every 3 s:
//         the charged stone, drawn as long as it exists
//   Take back (RimArt_MarkCatch): from 0.3 the stone flares inside its aura and its floor ring
//   pulls in; at 0.5 it pops off the cell in a puff of dust and flies back in its spin ring, caught
//   at 0.62: 6 teal dashes close in on the hand over 0.14 s as the aura goes out, then a small flare.
// No camera shake and no sound burst: this is not an attack. Its sound markers are for quiet sounds:
// the release, the touch-down, and for a take-back the stone leaving its cell (the catch is 0.12 s
// into that sound). One other stone lies near Todo the whole time to show the resting look and
// that stones stay.
//
// Drawing: the stone is Textures/RimArt/Anchor/ClapStone.png (shipped); the glow, rings and streak
// are SoftDisc sprites, ring meshes and strips; the aura is a fan mesh rebuilt each frame. The
// source's ring spins upright; here it is level, so it stays a ring for every direction. Everything
// is a point on a path or a level shape, so there is no per-facing method; the facing work is in
// the clips. The hand is the clip's, which in C# is MarkFlick.Hand (a fixed reach and height along
// the aim).
import { AltitudeLayer, Color, MaterialPool, Mathf, ShaderDatabase } from '../js/engine.js';
import { playClip } from '../js/animation.js';
import { draw, mesh } from './lib/six-paths-solid.js';
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
const Charge = .1, HandH = .4, Arc = .35, StoneRest = .45, StoneHand = .3;
const LandLife = .3, Breath = 2.4, GlintEvery = 3, GlintLife = .25;
// Motion. The aura's tongues flicker Flicker times a second. The release blade lasts BladeLife.
// The spin ring is SpinRing cells round the stone. A thrown stone touches down Skid cells short
// of its cell and slides in over SkidTime while its spin dies away (SpinDecay s).
const Flicker = 15, BladeLife = .1, SpinRing = .28, Skid = .18, SkidTime = .2, SpinDecay = .12, Absorb = .14;
const Scenarios = ['throw a stone', 'take a stone back'];

const dash = (a, b) => [0, .25, .5, .75, 1].map(v => ({ x: lerp(a.x, b.x, v), z: lerp(a.z, b.z, v) }));

// A filled fan round c through the points of ring, in one colour and material.
function fan(key, c, ring, colour, material, layer) {
  const vertices = [c.x, c.z], tri = [];
  ring.forEach((q, i) => { vertices.push(q.x, q.z); tri.push(0, 1 + i, 1 + (i + 1) % ring.length); });
  const m = mesh(key); m.setFlat(vertices, tri);
  draw(m, 0, layer, 0, 1, 1, 0, colour, material);
}

// Todo's cursed energy on an object (the S2 hanger in his hand): a teal flame outline with a dark
// edge round it, the tongues longer on the upper side, flickering Flicker times a second.
function aura(key, pos, size, s, alpha, seed, layer = Y + .018) {
  if (alpha <= .01 || size <= .01) return;
  // Round and low on the lower half; on the upper half the tongues grow long and lean inward,
  // so it reads as a flame rising off the object, not as a star.
  const n = 16, step = Math.floor(s * Flicker + rand(seed)), ring = [];
  for (let i = 0; i < n; i++) {
    const ang = i / n * Math.PI * 2, up = Math.max(0, Math.sin(ang)), flick = rand(seed + i + step * 29);
    const r = i % 2 ? (.5 + .3 * flick) * (1 + 1.4 * up * up) : .42 + .1 * up;
    const lean = i % 2 ? -Math.cos(ang) * .35 * up : 0;
    ring.push({ x: pos.x + Math.cos(ang + lean) * size * r, z: pos.z + Math.sin(ang + lean) * size * r });
  }
  fan(`${key} edge`, pos, ring.map(q => ({ x: pos.x + (q.x - pos.x) * 1.18, z: pos.z + (q.z - pos.z) * 1.18 })), Ink.withAlpha(.75 * alpha), undefined, layer);
  fan(`${key} fill`, pos, ring, Teal.withAlpha(.9 * alpha), whiteGlow, layer + .0005);
  sprite(pos, size * 1.1, size * 1.1, TealPale.withAlpha(.7 * alpha), glow, layer + .001);
}

// The stone at rest on its cell: the texture, a faint teal edge that breathes, a glint now and
// then. charge 0..1 brightens it and wraps it in the aura (taking it back). spin turns the
// texture (degrees). seed offsets its clock from other stones.
function restingStone(key, pos, s, alpha, charge, seed, spin = 0) {
  if (alpha <= 0) return;
  const breath = .5 + .5 * Math.sin((s / Breath + rand(seed)) * Math.PI * 2);
  sprite(pos, StoneRest * 2.4, StoneRest * 2.4, Teal.withAlpha(alpha * (.3 + .15 * breath + .5 * charge)), glow, Floor + .03);
  sprite(pos, StoneRest, StoneRest, White.withAlpha(alpha), stoneMat, itemLayer, spin);
  if (charge > 0) aura(`${key} aura`, pos, StoneRest * .8, s, charge, seed + 3, itemLayer - .01);
  const g = (s + rand(seed + 1) * GlintEvery) % GlintEvery;
  if (g < GlintLife) {
    const u = g / GlintLife, a = Math.sin(u * Math.PI) * alpha, c = { x: pos.x + .08, z: pos.z + .08 };
    sprite(c, .18, .18, TealPale.withAlpha(.8 * a), glow, Y + .01);
    line(`${key} glint h`, dash({ x: c.x - .12, z: c.z }, { x: c.x + .12, z: c.z }), .03, TealPale.withAlpha(a), whiteGlow, Y + .011, 'both');
    line(`${key} glint v`, dash({ x: c.x, z: c.z - .09 }, { x: c.x, z: c.z + .09 }), .03, TealPale.withAlpha(a), whiteGlow, Y + .011, 'both');
  }
}

// The stone in the air or in the hand, in its aura; ring > 0 adds the spin ring (S2: a big glowing
// ring with a bright rim and a faintly lit inside, two bright arcs running round it). The ring is
// drawn level so it stays a ring for every direction.
function flyingStone(key, pos, size, spin, s, auraAlpha, ring, ringSize = 1) {
  aura(`${key} aura`, pos, size * (ring > 0 ? .9 : 1.25), s, auraAlpha * (ring > 0 ? .7 : 1), 40);
  sprite(pos, size, size, White, stoneMat, Y + .021, spin);
  if (ring <= 0) return;
  const r = SpinRing * ringSize;
  sprite(pos, r * 2.2, r * 2.2, Teal.withAlpha(.18 * ring), glow, Y + .0215);
  ringAt(pos, r * 1.08, Ink.withAlpha(.6 * ring), Y + .0216);
  ringAt(pos, r, Teal.withAlpha(.9 * ring), Y + .0217, false, whiteGlow);
  ringAt(pos, r * .9, TealPale.withAlpha(.5 * ring), Y + .0218, false, whiteGlow);
  for (let k = 0; k < 2; k++) {
    const pts = [];
    for (let i = 0; i <= 6; i++) {
      const a = spin * Mathf.Deg2Rad + k * Math.PI + i / 6 * 1.4;
      pts.push({ x: pos.x + Math.cos(a) * r, z: pos.z + Math.sin(a) * r });
    }
    line(`${key} arc ${k}`, pts, .06, White.withAlpha(ring), whiteGlow, Y + .023, 'both');
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
      ? [{ name: 'Reach out', t: 0 }, { name: 'Stone flares', t: .3 }, { name: 'Stone leaves the cell', t: Place }, { name: 'Caught', t: CatchTime }]
      : [{ name: 'Charge', t: Charge }, { name: 'Release blade', t: Release }, { name: 'Touches down, skids', t: Place }, { name: 'Resting', t: Place + SkidTime }];
  },
  events(p) {
    return p.scenario === Scenarios[1] ? [{ t: Place, type: 'sound', def: 'AG_AnchorStoneBack' }]
      : [{ t: Release, type: 'sound', def: 'AG_AnchorStoneThrow' }, { t: Place, type: 'sound', def: 'AG_AnchorStoneLand' }];
  },

  draw(s, p, { origin: o, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, dir = { x: Math.cos(a), z: Math.sin(a) }, side = { x: -dir.z, z: dir.x };
    const todo = { x: o.x - dir.x * p.distance / 2, z: o.z - dir.z * p.distance / 2 };
    const cell = { x: todo.x + dir.x * p.distance, z: todo.z + dir.z * p.distance };
    const touch = { x: cell.x - dir.x * Skid, z: cell.z - dir.z * Skid };
    const back = p.scenario === Scenarios[1];

    // Another of Todo's stones, thrown earlier, lying 1.5 cells to his south-east.
    restingStone('st other', { x: todo.x + 1.2, z: todo.z - .9 }, s, 1, 0, 7);

    const clip = playClip(back ? 'RimArt_MarkCatch' : 'RimArt_MarkFlick', s, todo, { aim: p.aim, scene, shirt: CasterColour });
    if (!clip?.hand) return;

    // The flight: from the hand to where it touches down, or back from the cell. h is height in cells.
    const start = back ? Place : Release, end = back ? CatchTime : Place, u = (s - start) / (end - start);
    const from = back ? cell : clip.itemAtRelease ?? clip.hand, to = back ? clip.hand : touch;
    const high = Arc * Math.min(1, p.distance / 5);
    const path = k => {
      const baseH = back ? lerp(0, HandH, k) : lerp(HandH, 0, k), h = baseH + high * Math.sin(Math.PI * k);
      // from/to are screen points; the hand's is already lifted, so only the arc's lift is added.
      return { x: lerp(from.x, to.x, k), z: lerp(from.z, to.z, k) + (h - baseH) * Lift, h };
    };
    const spinAt = t => p.spin * (t - start);

    if (!back) {
      // In the hand: the stone wrapped in its aura, growing until it leaves.
      if (s >= Charge && s < Release) {
        const c = smooth((s - Charge) / (Release - Charge));
        flyingStone('st hand', clip.hand, StoneHand, 0, s, c, 0);
      }
      // Release (S2): a blade of light sweeps along the arm into the throw.
      const rb = s - Release;
      if (rb >= 0 && rb < BladeLife) {
        const f = 1 - rb / BladeLife, h = clip.itemAtRelease ?? clip.hand, pts = [];
        for (let i = 0; i <= 8; i++) {
          const v = i / 8, along = lerp(-.35, .6, v), across = .15 * (1 - v) * (1 - v);
          pts.push({ x: h.x + dir.x * along + side.x * across, z: h.z + dir.z * along + side.z * across });
        }
        line('st blade edge', pts, .32 * f, Ink.withAlpha(.7 * f), undefined, Y + .024, 'both');
        line('st blade', pts, .22 * f, Teal.withAlpha(f), whiteGlow, Y + .0245, 'both');
        line('st blade core', pts, .07 * f, White.withAlpha(f), whiteGlow, Y + .025, 'both');
      }
      // Touched down short of the cell: it slides in, spinning down, dust behind it; the spin
      // ring sinks into the floor ring and teal dashes spring out; then it rests.
      if (s >= Place) {
        const age = s - Place, slide = smooth(Math.min(1, age / SkidTime));
        const pos = { x: lerp(touch.x, cell.x, slide), z: lerp(touch.z, cell.z, slide) };
        const turn = spinAt(Place) + p.spin * SpinDecay * (1 - Math.exp(-age / SpinDecay));
        restingStone('st thrown', pos, s, 1, age < LandLife ? 1 - age / LandLife : 0, 11, turn);
        if (age < .15) flyingStone('st landing ring', pos, 0, turn, s, 0, 1 - age / .15, 1 - age / .15);
        if (age < LandLife) {
          const k = age / LandLife, out = 1 - (1 - k) * (1 - k), f = 1 - smooth(k);
          ringAt(cell, lerp(.25, p.land, out), Teal.withAlpha(.8 * f), Floor + .04, false, whiteGlow);
          for (let i = 0; i < 6; i++) {
            const ang = (i + rand(i + 30) * .6) / 6 * Math.PI * 2, r = lerp(.15, p.land * .9, out);
            const q = { x: cell.x + Math.cos(ang) * r, z: cell.z + Math.sin(ang) * r }, t2 = { x: q.x + Math.cos(ang) * .16, z: q.z + Math.sin(ang) * .16 };
            line(`st land edge ${i}`, dash(q, t2), .09, Ink.withAlpha(.7 * f), undefined, Y + .03, 'both');
            line(`st land dash ${i}`, dash(q, t2), .05, Teal.withAlpha(f), whiteGlow, Y + .031, 'both');
          }
          // Dust kicked up along the skid, behind the stone.
          for (let i = 0; i < 5; i++) {
            const v = i / 4 * slide, d = { x: lerp(touch.x, cell.x, v), z: lerp(touch.z, cell.z, v) }, jitter = (rand(i + 40) - .5) * .2;
            sprite({ x: d.x + side.x * jitter, z: d.z + side.z * jitter + .05 * out }, .2 + .2 * out, .16 + .14 * out, Dust.withAlpha(.45 * f), soft, Y + .005);
          }
        }
      }
    } else {
      // Take back: from 0.3 the stone flares in its aura and the floor ring pulls in; at Place it
      // pops up and flies back.
      if (s < Place) restingStone('st taken', cell, s, 1, smooth((s - .3) / (Place - .3)), 11);
      if (s >= .3 && s < Place) {
        const k = (s - .3) / (Place - .3);
        ringAt(cell, lerp(p.land, .2, smooth(k)), Teal.withAlpha(.7 * k), Floor + .04, false, whiteGlow);
      }
      if (s >= Place && s < Place + .15) {
        const k = (s - Place) / .15;
        for (let i = 0; i < 3; i++) sprite({ x: cell.x + (i - 1) * .12, z: cell.z + .03 + .06 * k }, .2 + .15 * k, .15 + .1 * k, Dust.withAlpha(.4 * (1 - k)), soft, Y + .005);
      }
      // Caught: the aura is drawn into the hand, 6 teal dashes closing on it, and goes out.
      const ca = s - CatchTime;
      if (ca >= 0 && ca < Absorb + .14) {
        const k = clamp(ca / Absorb), f = 1 - smooth(k);
        flyingStone('st caught', clip.hand, StoneHand * (1 - .3 * k), 0, s, f, 0);
        for (let i = 0; i < 6; i++) {
          const ang = i / 6 * Math.PI * 2 + .3, r = lerp(.45, .06, k);
          const q = { x: clip.hand.x + Math.cos(ang) * r, z: clip.hand.z + Math.sin(ang) * r }, t2 = { x: q.x + Math.cos(ang) * .12, z: q.z + Math.sin(ang) * .12 };
          line(`st absorb edge ${i}`, dash(q, t2), .08, Ink.withAlpha(.7 * f), undefined, Y + .03, 'both');
          line(`st absorb ${i}`, dash(q, t2), .04, Teal.withAlpha(f), whiteGlow, Y + .031, 'both');
        }
        const g = clamp((ca - Absorb) / .14);
        if (ca >= Absorb) sprite(clip.hand, .45, .45, TealPale.withAlpha(.8 * (1 - g) ** 2), glow, Y + .032);
      }
    }

    // In flight: shadow under it, the streak behind it (dark edge, teal, pale core), the stone in
    // its aura inside the spin ring.
    if (u >= 0 && u < 1) {
      const pos = path(u), ground = { x: pos.x, z: pos.z - pos.h * Lift };
      sprite({ x: ground.x + sun.x * pos.h, z: ground.z + sun.z * pos.h }, .22, .12, Ink.withAlpha(strength * .7), soft, shadowLayer);
      if (p.streak > 0) {
        // Stone first, so the 'end' taper thins it toward the tail.
        const pts = [];
        for (let i = 0; i <= 8; i++) pts.push(path(Math.max(0, u - p.streak * i / 8)));
        line('st streak edge', pts, .24, Ink.withAlpha(.5), undefined, Y + .0175, 'end');
        line('st streak glow', pts, .16, Teal.withAlpha(.7), whiteGlow, Y + .018, 'end');
        line('st streak core', pts, .05, White.withAlpha(.85), whiteGlow, Y + .019, 'end');
      }
      const ringIn = back ? smooth(u / .3) : smooth(u / .25);
      flyingStone('st flying', pos, lerp(StoneHand, StoneRest * .9, back ? 1 - u : u), spinAt(s), s, 1, ringIn, .6 + .4 * ringIn);
    }
  },
};
