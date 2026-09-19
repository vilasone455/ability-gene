// Clap teleport — new picture for the Anchor organ's Clap and Double Clap, not the game. The game
// still plays vanilla Skip_EntryNoDelay / Skip_ExitNoDelay (Source/RimArt/Anchor/AnchorFX.cs).
//
// What it is for (agreed: the mechanic does not change, only the picture). Stage magician theme.
//   Clap: the carrier and one mark change places, any distance, no line of sight, 0.5 s warmup,
//   1800-tick cooldown. A mark on a pawn is a swap; a mark on a tile moves the carrier there.
//   Double Clap: two marks change places with each other, 0.75 s warmup; the carrier stays.
//   Marks: 3 held, 60000 ticks each, placed at 9.9 cells. Each mark is a playing card of its own
//   suit (spade, heart, club) so two can be told apart when picking for Double Clap.
//
// Order. The clip starts with the warmup, so the last palm contact C is the warmup's end and the
// swap happens on it: C = 0.50 (RimArt_Clap) or 0.75 (RimArt_ClapTwice).
//   0.00      the carrier opens the hands wide; marks show as cards (over a pawn, flat on a tile)
//   C - 0.20  at every end 8 cards come up out of the floor in a level ring (radius 0.55), spinning
//             540 deg/s, rising to 1.1 cells; the mark's own card flips faster
//   C         palms meet: a small gold 4-point star at the palms, camera shake 0.02. At every end a
//             red silk puff (radius 0.7) covers the cell for 0.15 s, gold flash, 6 sparkles; the
//             occupants change under it. Double Clap's first contact (0.50) is the palm star only
//   C + 0.15  the puff thins and the other pawn is there; the cards scatter out to 0.85-1.25 cells,
//             flutter down and lie on the floor
//   C + 1.20  the cards on the floor have faded
//   Tile mark: nothing comes back, so the cell the carrier left gets the puff and one falling
//   card (the spent mark) and no ring.
// No line joins the two ends: the range is the whole map, and a joining line is Flying Thunder
// God's picture.
//
// Drawing: a level ring stays a circle for every facing and only shifts north with height, so
// there is no per-facing method. A standing card is one quad whose width is the cosine of its
// turn; the north half of the ring draws under the pawn layer. The marked pawns are two-disc
// stand-ins; the carrier is the real clip through playClip(). The three suit textures are lab/
// generators and still have to be ported to a make_anchor_textures.py; the puff is the Six Paths
// Puff texture.
import { AltitudeLayer, Color, MaterialPool, Mathf, MeshPool, ShaderDatabase } from '../js/engine.js';
import { registerLabTexture, pixels } from '../js/standins.js';
import { playClip } from '../js/animation.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, Lift, at, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import { figure, CasterColour, EnemyColour } from './lib/flying-thunder-god.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const puffMat = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const whiteGlow = MaterialPool.MatFrom('white', ShaderDatabase.MoteGlow);

// Suit pips: white shape in the alpha, coloured at draw time. x, y run -1.2..1.2 with y up.
const heart = (x, y) => { const X = x * 1.15, Y2 = y * 1.15 + .2; return (X * X + Y2 * Y2 - 1) ** 3 - X * X * Y2 ** 3 < 0; };
const stem = (x, y) => y < -.35 && y > -1.05 && Math.abs(x) < .08 + (-.35 - y) * .38;
const suits = {
  heart,
  spade: (x, y) => heart(x, -y + .25) || stem(x, y),
  club: (x, y) => [[0, .48], [-.47, -.12], [.47, -.12]].some(([cx, cy]) => Math.hypot(x - cx, y - cy) < .43) || stem(x, y) || (Math.abs(x) < .2 && Math.abs(y) < .3),
};
const sub = [[-.25, -.25], [.25, -.25], [-.25, .25], [.25, .25]];
const pip = {};
for (const [name, inside] of Object.entries(suits)) {
  registerLabTexture(`lab/suit-${name}`, () => pixels(64, (u, v) => {
    let a = 0;
    for (const [du, dv] of sub) if (inside(((u + du / 64) - .5) * 2.4, (.5 - (v + dv / 64)) * 2.4)) a += .25;
    return [1, 1, 1, a];
  }));
  pip[name] = MaterialPool.MatFrom(`lab/suit-${name}`, ShaderDatabase.Transparent);
}

// Decided looks. The panel keeps what is still being tuned.
const Paper = new Color(.96, .94, .88), Ink = new Color(.10, .08, .09), Red = new Color(.69, .125, .18);
const RedLit = new Color(.86, .27, .30), RedDark = new Color(.42, .06, .11), Gold = new Color(.88, .69, .25), GoldPale = new Color(1, .93, .66);
const AllyColour = new Color(.45, .62, .40);
const CardW = .13, CardH = .18, MarkScale = 1.5, Edge = .022;
// MarkHeight is cells above the ground: 1.65 draws the card just clear of the head.
const MarkHeight = 1.65, Flip = 11, PuffIn = .05, PuffFade = .4, FlashLife = .12, SparkleLife = .45, PalmStar = .18;
const FirstContact = .5, SecondContact = .75, Tail = .25;
const Scenarios = ['clap: swap with a pawn', 'clap: move to a tile', 'double clap: two pawns'];
const SuitOf = ['spade', 'heart', 'club'];

function times(p) {
  const double = p.scenario === Scenarios[2], contact = double ? SecondContact : FirstContact;
  return { double, contact, rise: contact - p.rise, clear: contact + p.cover, end: contact + p.fade + Tail };
}

// One card. turn is the angle about its own upright axis (0 face on, pi back on); tilt turns it
// on the screen, for a card lying on the floor.
function card(pos, scale, turn, tilt, suit, alpha, layer) {
  const c = Math.cos(turn), w = Math.max(.012, CardW * scale * Math.abs(c)), h = CardH * scale;
  draw(MeshPool.plane10, pos.x, layer, pos.z, w + Edge, h + Edge, tilt, Ink.withAlpha(alpha * .85));
  if (c >= 0) {
    draw(MeshPool.plane10, pos.x, layer + .001, pos.z, w, h, tilt, Paper.withAlpha(alpha));
    draw(MeshPool.plane10, pos.x, layer + .002, pos.z, w * .62, h * .46, tilt, (suit === 'heart' ? Red : Ink).withAlpha(alpha), pip[suit]);
  } else {
    draw(MeshPool.plane10, pos.x, layer + .001, pos.z, w, h, tilt, Gold.withAlpha(alpha));
    draw(MeshPool.plane10, pos.x, layer + .002, pos.z, w * .78, h * .84, tilt, Red.withAlpha(alpha));
  }
}

// A 4-point sparkle: two crossed additive slivers over a soft spot.
function sparkle(pos, size, alpha, turn = 0) {
  if (alpha <= 0) return;
  sprite(pos, size * 1.3, size * 1.3, Gold.withAlpha(alpha * .55), glow, Y + .2);
  draw(MeshPool.plane10, pos.x, Y + .21, pos.z, size * 2, size * .13, turn, GoldPale.withAlpha(alpha), whiteGlow);
  draw(MeshPool.plane10, pos.x, Y + .21, pos.z, size * .13, size * 2, turn, GoldPale.withAlpha(alpha), whiteGlow);
}

export default {
  kit: 'Anchor', label: 'Clap teleport (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    distance: P('Distance between the ends (cells)', 6, 2, 14, 2, 'Showcase'),
    rise: P('Cards rise before the contact', .2, .1, .45, .01, 'Timing (s)'),
    cover: P('Puff fully covers the cell', .15, .05, .4, .01, 'Timing (s)'),
    fade: P('Floor cards gone after the contact', 1.2, .6, 3, .05, 'Timing (s)'),
    cards: P('Cards in the ring', 8, 4, 14, 1, 'Ring'),
    radius: P('Ring radius (cells)', .55, .35, .9, .01, 'Ring'),
    height: P('Ring height (cells)', 1.1, .5, 1.8, .05, 'Ring'),
    spin: P('Ring spin (degrees per s)', 540, 0, 1080, 20, 'Ring'),
    scatter: P('Furthest a card lands (cells)', 1.25, .7, 2, .05, 'Ring'),
    puff: P('Puff radius (cells)', .7, .4, 1.2, .05, 'Puff'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Wind-up', t: 0 }, ...(t.double ? [{ name: 'First clap', t: FirstContact }] : []),
    { name: 'Cards rise', t: t.rise }, { name: 'Contact: swap', t: t.contact }, { name: 'Cards fall', t: t.clear },
  ]; },
  events(p) { return [{ t: times(p).contact, type: 'shake', value: .02 }]; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const half = p.distance / 2, tile = p.scenario === Scenarios[1], swapped = s >= t.contact, age = s - t.contact;
    const west = { x: o.x - half, z: o.z }, east = { x: o.x + half, z: o.z };
    const carrierHome = t.double ? { x: o.x, z: o.z + 2.5 } : west;

    // The ends. who: what stands there before and after the contact. ring: false is the cell a
    // tile-move leaves, which gets the puff and one falling card only.
    const ends = t.double
      ? [{ pos: west, before: 'enemy', after: 'ally', suit: SuitOf[0], mark: 'pawn', ring: true },
         { pos: east, before: 'ally', after: 'enemy', suit: SuitOf[1], mark: 'pawn', ring: true }]
      : tile
        ? [{ pos: west, before: 'carrier', after: null, suit: SuitOf[2], mark: null, ring: false },
           { pos: east, before: null, after: 'carrier', suit: SuitOf[2], mark: 'tile', ring: true }]
        : [{ pos: west, before: 'carrier', after: 'enemy', suit: SuitOf[1], mark: null, ring: true },
           { pos: east, before: 'enemy', after: 'carrier', suit: SuitOf[1], mark: 'pawn', ring: true }];

    // Pawns. The carrier is the real clip; it goes on playing at the far end, so the open-hands
    // pose after the clap happens where the carrier arrives.
    let carrierAt = carrierHome;
    for (const e of ends) {
      const who = swapped ? e.after : e.before;
      if (who === 'carrier') carrierAt = e.pos;
      else if (who) figure(e.pos, who === 'enemy' ? EnemyColour : AllyColour, 1, 0, sun, strength);
    }
    const clip = playClip(t.double ? 'RimArt_ClapTwice' : 'RimArt_Clap', s, carrierAt, { scene, shirt: CasterColour });

    // Palm star on every contact. The palms are on the body's centre line at the hand's height.
    // It stays where the clap was made: the carrier has left by the time it fades.
    const clapped = ends.find(e => e.before === 'carrier')?.pos ?? carrierHome;
    if (clip?.hand) for (const c of t.double ? [FirstContact, SecondContact] : [FirstContact]) {
      const u = (s - c) / PalmStar;
      if (u >= 0 && u < 1) sparkle({ x: clapped.x, z: clapped.z + clip.hand.z - carrierAt.z }, .3 * (.6 + .4 * u), (1 - u) * (1 - u), 45 * u);
    }

    ends.forEach((e, n) => {
      const up = smooth((s - t.rise) / p.rise), gone = smooth((age - (p.fade - .4)) / .4);

      // The mark, until the contact spends it. Over a pawn it floats above the head; on a tile it
      // lies on the cell inside a gold outline, then lifts to the middle of the ring.
      if (e.mark && !swapped) {
        const turn = s < t.rise ? Math.sin(s * 2.2 + n) * .5 : (s - t.rise) * Flip * 2;
        if (e.mark === 'pawn') card(at(e.pos, 0, 0, MarkHeight + .05 * Math.sin(s * 3 + n)), MarkScale, turn, 0, e.suit, 1, Y + .05);
        else card(at(e.pos, 0, 0, p.height * .7 * up), MarkScale, up > 0 ? turn : 0, lerp(-14, 0, up), e.suit, 1, up > 0 ? Y + .05 : Floor + .02);
      }
      if (e.mark === 'tile') {
        const a = .5 * (1 - gone);
        for (const [dx, dz, w, h] of [[0, .5, 1, .03], [0, -.5, 1, .03], [.5, 0, .03, 1], [-.5, 0, .03, 1]])
          draw(MeshPool.plane10, e.pos.x + dx, Floor + .01, e.pos.z + dz, w, h, 0, Gold.withAlpha(a));
      }

      // The ring: up out of the floor before the contact, scattering and falling after it. The
      // cell a tile-move leaves has one card instead, dropping from where the carrier's chest was.
      const count = e.ring ? p.cards : 1;
      for (let i = 0; i < count; i++) {
        const k = n * 40 + i, slot = i / count * Math.PI * 2 + n * .7;
        if (!swapped) {
          if (!e.ring || up <= 0) continue;
          const a = slot + p.spin * Mathf.Deg2Rad * (s - t.contact), r = p.radius * (.6 + .4 * up), h = p.height * up;
          const ground = { x: e.pos.x + Math.cos(a) * r, z: e.pos.z + Math.sin(a) * r }, behind = Math.sin(a) > 0;
          sprite({ x: ground.x + sun.x * h, z: ground.z + sun.z * h }, .2, .1, Ink.withAlpha(strength * .6 * up), soft, shadowLayer);
          // Facing outward: the south half shows faces, the north half shows backs.
          card({ x: ground.x, z: ground.z + h * Lift }, 1, Math.acos(clamp(-Math.sin(a) * .5 + .5) * 2 - 1), 0, e.suit, clamp(up * 4), behind ? pawnLayer - .02 : Y + .03);
          continue;
        }
        const fall = .55 + .3 * rand(k), u = clamp(age / fall), out = 1 - (1 - u) * (1 - u);
        const a = slot + (rand(k + 7) - .5) * .5, far = e.ring ? lerp(p.radius + .3, p.scatter, rand(k + 3)) : .25 * rand(k + 3);
        const r = lerp(e.ring ? p.radius : 0, far, out), h = (e.ring ? p.height : .7) * (1 - u) ** 1.5;
        const sway = Math.sin(age * 9 + k) * .09 * (1 - u);
        const ground = { x: e.pos.x + Math.cos(a) * r + sway, z: e.pos.z + Math.sin(a) * r };
        const landed = u >= 1, faceUp = rand(k + 11) > .4, alpha = 1 - gone;
        if (!landed) sprite({ x: ground.x + sun.x * h, z: ground.z + sun.z * h }, .2, .1, Ink.withAlpha(strength * .6 * alpha), soft, shadowLayer);
        card({ x: ground.x, z: ground.z + h * Lift }, e.ring ? 1 : MarkScale, landed ? (faceUp ? 0 : Math.PI) : age * Flip + k,
          landed ? rand(k + 5) * 360 : Math.sin(age * 7 + k) * 25, e.suit, alpha, landed ? Floor + .03 : Y + .03);
      }

      // The cover: red silk puff, opaque from PuffIn before the contact until `cover` after it.
      const puffAge = age + PuffIn;
      if (puffAge >= 0 && age < p.cover + PuffFade) {
        const alpha = clamp(puffAge / PuffIn) * (1 - smooth((age - p.cover) / PuffFade)), grow = smooth(puffAge / .5);
        const blob = (x, z, h, size, colour, layer) => sprite(at(e.pos, x, z, h), size * 1.25, size, colour.withAlpha(alpha), puffMat, layer);
        // Two wide blobs sit on the pawn itself (ground to head is 0 to 0.75 on screen); the
        // Puff texture is thin at its rim, so they are well oversize to hide the body's edges.
        // Each is drawn twice so the alpha stacks to opaque over the body.
        for (let i = 0; i < 2; i++) {
          blob(0, -.05, .2, p.puff * 2.3 * (.85 + .25 * grow), RedDark, Y + .10);
          blob(0, 0, .55, p.puff * 2.0 * (.85 + .25 * grow), Red, Y + .105);
        }
        for (let i = 0; i < 7; i++) {
          const a = i / 7 * Math.PI * 2 + n, d = p.puff * (.35 + .35 * grow);
          blob(Math.cos(a) * d, Math.sin(a) * d * .5, .1 + .8 * rand(n * 9 + i) + .25 * grow, p.puff * (.9 + .4 * rand(n * 9 + i + 3)) * (.8 + .4 * grow), Red, Y + .11);
        }
        for (let i = 0; i < 3; i++) blob(-.18 + i * .16, 0, .7 + .12 * i + .3 * grow, p.puff * .55 * (.8 + .4 * grow), RedLit, Y + .12);
      }
      if (age >= 0 && age < FlashLife) sprite(at(e.pos, 0, 0, .5), 1.8, 1.8, GoldPale.withAlpha(.7 * (1 - age / FlashLife)), glow, Y + .19);
      if (age >= 0 && age < SparkleLife) for (let i = 0; i < 6; i++) {
        const u = age / SparkleLife, a = (i * 60 + 20 + n * 30) * Mathf.Deg2Rad, d = .3 + .75 * (1 - (1 - u) * (1 - u));
        sparkle(at(e.pos, Math.cos(a) * d, Math.sin(a) * d * .6, .5 + .6 * u), .16 * (1 - u * .6), 1 - u, 45 + 90 * u);
      }
    });
  },
};
