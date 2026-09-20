// Mark card flick — picture for the Anchor organ's Mark. Ported: MarkFlick.cs has these numbers,
// ClapTeleportGraphics.FlyingCard draws the card, MapComponent_MarkFlicks plays real casts, and
// the recorder's "Mark flick: ..." previews (DebugActions_MarkFlick.cs) are the C# to compare with.
//
// What it is for (agreed: the mechanic does not change, only the picture). Stage magician theme,
// same cards as the Clap teleport sketch.
//   Mark: range 9.9 cells, 0.5 s warmup, 600-tick cooldown. Puts a mark on a pawn or a standable
//   tile; 3 held, 60000 ticks each. Marking something already marked lifts that mark instead.
//
// Order. The clip starts with the warmup, so 0.50 is the moment the mark is placed or lifted.
//   Placing (RimArt_MarkFlick, 0.80 s):
//   0.00  the hand comes up and curls across the chest with a card
//   0.38  backhand snap: the card leaves the fingers, spinning flat, with a short pale streak
//   0.50  it arrives whatever the distance (up to 82 cells/s at 9.9 cells). On a pawn it stands up
//         above the head as the mark card, face out, with a small gold sparkle. On a tile it
//         drops flat on the cell inside a gold outline
//   0.80  the hand is back at the hip
//   Lifting (RimArt_MarkCatch, 0.95 s):
//   0.00  the hand goes out open toward the mark
//   0.50  the mark card leaves the target and flies back the same way
//   0.62  it is in the fingers (the clip's card part comes on), then goes in to the chest
// No camera shake and no dust: this is not an attack.
//
// Drawing: the flying card is a flat quad turning on the screen (a level card stays a card for
// every facing), so there is no per-facing method; the facing work is in the three clips. It
// starts at the clip's hand, which in C# is a fixed reach and height along the aim, not read from
// Melee Animation. The target pawn is a two-disc stand-in; the carrier is the real clip through
// playClip(). Card back in the hand is Textures/RimArt/Anchor/CardBack.png
// (make_anchor_textures.py); the flying and the mark card are quads, as in the Clap sketch.
import { AltitudeLayer, Color, MaterialPool, Mathf, MeshPool, ShaderDatabase } from '../js/engine.js';
import { playClip } from '../js/animation.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, Lift, at, sprite, trail, glow, soft } from './lib/six-paths-impact.js';
import { figure, CasterColour, EnemyColour } from './lib/flying-thunder-god.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor();
const whiteGlow = MaterialPool.MatFrom('white', ShaderDatabase.MoteGlow);
const heart = MaterialPool.MatFrom('RimArt/Anchor/SuitHeart', ShaderDatabase.Transparent);

// Decided looks, the Clap teleport sketch's numbers.
const Paper = new Color(.96, .94, .88), Ink = new Color(.10, .08, .09), Red = new Color(.69, .125, .18);
const Gold = new Color(.88, .69, .25), GoldPale = new Color(1, .93, .66);
const CardW = .13, CardH = .18, MarkScale = 1.5, Edge = .022, MarkHeight = 1.65;
// The clips' own times (make_mark_anim.py) and the ability's warmup.
const Release = .38, Place = .5, FlickLength = .8, CatchTime = .62, CatchLength = .95, Tail = .6;
const Settle = .12, SparkleLife = .3;
const Scenarios = ['mark a pawn', 'mark a tile', 'lift a mark from a pawn'];

// One card. turn is the angle about its own upright axis (0 face on, pi back on); tilt turns it
// on the screen.
function card(pos, scale, turn, tilt, alpha, layer) {
  const c = Math.cos(turn), w = Math.max(.012, CardW * scale * Math.abs(c)), h = CardH * scale;
  draw(MeshPool.plane10, pos.x, layer, pos.z, w + Edge, h + Edge, tilt, Ink.withAlpha(alpha * .85));
  if (c >= 0) {
    draw(MeshPool.plane10, pos.x, layer + .001, pos.z, w, h, tilt, Paper.withAlpha(alpha));
    draw(MeshPool.plane10, pos.x, layer + .002, pos.z, w * .62, h * .46, tilt, Red.withAlpha(alpha), heart);
  } else {
    draw(MeshPool.plane10, pos.x, layer + .001, pos.z, w, h, tilt, Gold.withAlpha(alpha));
    draw(MeshPool.plane10, pos.x, layer + .002, pos.z, w * .78, h * .84, tilt, Red.withAlpha(alpha));
  }
}

function sparkle(pos, size, alpha, turn) {
  if (alpha <= 0) return;
  sprite(pos, size * 1.3, size * 1.3, Gold.withAlpha(alpha * .55), glow, Y + .2);
  draw(MeshPool.plane10, pos.x, Y + .21, pos.z, size * 2, size * .13, turn, GoldPale.withAlpha(alpha), whiteGlow);
  draw(MeshPool.plane10, pos.x, Y + .21, pos.z, size * .13, size * 2, turn, GoldPale.withAlpha(alpha), whiteGlow);
}

export default {
  kit: 'Anchor', label: 'Mark card flick (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    aim: P('Target direction (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Target distance (cells)', 6, 1, 9.9, .1, 'Showcase'),
    spin: P('Card spin in flight (degrees per s)', 1440, 0, 2880, 60, 'Card'),
    streak: P('Streak length (share of the path)', .3, 0, .6, .02, 'Card'),
    streakWidth: P('Streak width (cells)', .07, .02, .2, .01, 'Card'),
  },
  duration(p) { return (p.scenario === Scenarios[2] ? CatchLength : FlickLength) + Tail; },
  phases(p) { return p.scenario === Scenarios[2]
    ? [{ name: 'Reach out', t: 0 }, { name: 'Mark lifted: card leaves', t: Place }, { name: 'Caught', t: CatchTime }, { name: 'Clip ends', t: CatchLength }]
    : [{ name: 'Curl', t: 0 }, { name: 'Card leaves the hand', t: Release }, { name: 'Mark placed', t: Place }, { name: 'Clip ends', t: FlickLength }]; },
  events() { return []; },

  draw(s, p, { origin: o, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, dir = { x: Math.cos(a), z: Math.sin(a) };
    const carrier = { x: o.x - dir.x * p.distance / 2, z: o.z - dir.z * p.distance / 2 };
    const target = { x: carrier.x + dir.x * p.distance, z: carrier.z + dir.z * p.distance };
    const lifting = p.scenario === Scenarios[2], tile = p.scenario === Scenarios[1];

    if (!tile) figure(target, EnemyColour, 1, 0, sun, strength);
    const clip = playClip(lifting ? 'RimArt_MarkCatch' : 'RimArt_MarkFlick', s, carrier, { aim: p.aim, scene, shirt: CasterColour });
    if (!clip?.hand) return;

    // Where the mark sits: above a pawn's head, or flat on the tile. h is its height in cells.
    const markH = tile ? 0 : MarkHeight, mark = at(target, 0, 0, markH);
    // The hand is about 0.4 cells up (the clips' lift at the release is 0.22 on screen, 0.22 / Lift).
    // A placed card starts where the clip's card was one frame before it was switched off.
    const handH = .4, from = lifting ? mark : clip.itemAtRelease ?? clip.hand, to = lifting ? clip.hand : mark;
    const start = lifting ? Place : Release, end = lifting ? CatchTime : Place, u = (s - start) / (end - start);
    const marked = lifting ? s < Place : s >= Place;

    if (tile) {
      const show = smooth((s - Place) / Settle) * .5;
      for (const [dx, dz, w, h] of [[0, .5, 1, .03], [0, -.5, 1, .03], [.5, 0, .03, 1], [-.5, 0, .03, 1]])
        draw(MeshPool.plane10, target.x + dx, Floor + .01, target.z + dz, w, h, 0, Gold.withAlpha(show));
    }

    // The mark card at rest. Placing: it stands up out of the flat flying card over Settle, a
    // little oversize at first. A tile's card lies flat, slightly turned.
    if (marked) {
      const age = lifting ? 1 : s - Place, up = smooth(age / Settle), pop = 1 + .3 * (1 - up);
      if (tile) card(mark, MarkScale * pop, 0, -14, 1, Floor + .02);
      else card(at(target, 0, 0, MarkHeight + .05 * Math.sin(s * 3)), MarkScale * pop, lerp(Math.PI / 2, Math.sin(s * 2.2) * .5, up), 0, 1, Y + .05);
      if (!lifting && age < SparkleLife) sparkle(mark, .22 * (.6 + .4 * age / SparkleLife), (1 - age / SparkleLife) ** 2, 45 + 90 * age / SparkleLife);
    }

    // The card in flight. Back up, turning flat on the screen; the shadow runs on the ground
    // under it, from under the hand to under the mark.
    if (u >= 0 && u < 1) {
      const point = (k) => ({ x: lerp(from.x, to.x, k), z: lerp(from.z, to.z, k) });
      const pos = point(u), h = lerp(lifting ? markH : handH, lifting ? handH : markH, u);
      const ground = { x: pos.x, z: pos.z - h * Lift };
      sprite({ x: ground.x + sun.x * h, z: ground.z + sun.z * h }, .2, .1, Ink.withAlpha(strength * .6), soft, shadowLayer);
      if (p.streak > 0) {
        const pts = [];
        for (let i = 0; i <= 6; i++) pts.push(point(Math.max(0, u - p.streak * (1 - i / 6))));
        trail('mark flick streak', pts, p.streakWidth, GoldPale.withAlpha(.55), Y + .02);
      }
      card(pos, lerp(1, MarkScale, lifting ? 1 - u : u), Math.PI, p.spin * (s - start), 1, Y + .04);
    }
    // Caught: a small sparkle at the fingers.
    if (lifting && s >= CatchTime && s < CatchTime + SparkleLife) {
      const k = (s - CatchTime) / SparkleLife;
      sparkle(clip.hand, .16 * (.6 + .4 * k), (1 - k) ** 2, 45 + 90 * k);
    }
  },
};
