// Carried orbs — how the sage's six orbs sit on the pawn for each facing and how they follow it.
// A look test, not an ability and not the game. Nothing in Source/RimArt draws this yet.
//
// What it shows (proposed, none of it agreed). Two ways to carry the orbs, switched by "Layout":
//   level ring         six slots on a level circle around the pawn at head height. The ring turns
//                      slowly. It is the same picture for every facing.
//   arc behind back    six slots on an arc centred on the pawn's back, as in the source material.
//                      The arc swings round when the pawn turns (0.35 s), so every facing differs.
// "Orbs free" empties slots from the end: an orb that is in use leaves a gap, so the ring is also
// the orb counter. Two scenes: four pawns standing, one per facing, each looking away from the
// chosen cell; and one pawn walking a square, east, north, west, south, turning at each corner.
// While walking, the orbs are centred on where the pawn was "Follow lag" seconds ago.
//
// Drawing: an orb whose ground position is north of the pawn draws under the pawn layer, the rest
// over it; that is what makes the ring go round the pawn. Each orb has a ground shadow at its true
// position, shifted along the sun by its height. The pawn is a stand-in that slides; RimWorld
// pawns have no walk frames either. Its sprite facing snaps at the corner, as in game.
import { AltitudeLayer, Color, Mathf, Meshes } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Body, Floor, Lift, orb, sprite, soft } from './lib/six-paths-impact.js';

const disc = Meshes.disc(32, 'carried orbs disc');
const pawnLayer = AltitudeLayer.Pawn.AltitudeFor(), shadowLayer = AltitudeLayer.Shadows.AltitudeFor();
const Shirt = new Color(.35, .55, .68), Skin = new Color(.83, .70, .54), Hair = new Color(.22, .15, .10);
const Orbs = 6;
const Feet = .3;                     // the pawn's feet are this far south of its sprite centre; heights start there
const Bob = .04, BobHz = .5;          // height wobble per orb, phase offset by slot
const ArcSpan = 150;                  // degrees the behind-the-back arc covers
const TurnTime = .35;                 // seconds the arc takes to swing round at a corner
const Side = 4;                       // walked square, cells per side
const StandSeconds = 10;
const Facings = [0, 90, 180, 270];    // degrees: east, north, west, south

const smooth = (v) => Mathf.Smooth(Math.max(0, Math.min(1, v)));

/** Stand-in pawn for one of the four sprite facings, sized like the clip stand-in in animation.js. */
function pawn(o, facing, scene) {
  const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
  const side = facing === 0 ? 1 : facing === 180 ? -1 : 0;
  sprite({ x: o.x + sun.x * .4, z: o.z - .3 + sun.z * .4 }, .9, .4, new Color(.03, .03, .05, strength), soft, shadowLayer);
  draw(disc, o.x, pawnLayer, o.z - .1, .23, .27, 0, Shirt);
  draw(disc, o.x + side * .03, pawnLayer + .003, o.z + .3, .17, .17, 0, facing === 90 ? Hair : Skin);
  if (side) draw(disc, o.x + side * .15, pawnLayer + .004, o.z + .31, .025, .03, 0, Hair);
  else if (facing === 270) for (const e of [-1, 1]) draw(disc, o.x + e * .065, pawnLayer + .004, o.z + .29, .025, .03, 0, Hair);
}

/** Where the walking pawn is at `t`: position, sprite facing, and the smoothed back angle. */
function walk(t, speed) {
  const lap = Side * 4, d = ((t * speed) % lap + lap) % lap, leg = Math.floor(d / Side), along = d - leg * Side, h = Side / 2;
  const corner = [{ x: -h, z: -h }, { x: h, z: -h }, { x: h, z: h }, { x: -h, z: h }][leg];
  const a = Facings[leg] * Mathf.Deg2Rad;
  return {
    x: corner.x + Math.cos(a) * along, z: corner.z + Math.sin(a) * along, facing: Facings[leg],
    heading: Facings[leg] - 90 * (1 - smooth(along / speed / TurnTime)),
  };
}

function orbs(centre, heading, s, p, scene) {
  const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
  for (let i = 0; i < Math.round(p.free); i++) {
    const angle = p.layout === 'level ring'
      ? (s / p.turn + i / Orbs) * Math.PI * 2
      : (heading + 180 + (i / (Orbs - 1) - .5) * ArcSpan) * Mathf.Deg2Rad;
    const gx = Math.cos(angle) * p.radius, gz = Math.sin(angle) * p.radius;
    const h = p.height + Math.sin((s * BobHz + i / Orbs) * Math.PI * 2) * Bob;
    sprite({ x: centre.x + gx + sun.x * h, z: centre.z - Feet + gz + sun.z * h }, p.size * 2.4, p.size * 1.3, new Color(.03, .03, .05, strength * .8), soft, shadowLayer + .001);
    orb({ x: centre.x + gx, z: centre.z - Feet + gz + h * Lift }, p.size, 1, 1, gz > 0 ? pawnLayer - .02 : pawnLayer + .02);
  }
}

export default {
  kit: 'Six Paths', label: 'Carried orbs (sketch)',
  params: {
    scene: { label: 'Scene', value: 'four facings, standing', options: ['four facings, standing', 'walking a square'], group: 'Showcase' },
    layout: { label: 'Layout', value: 'level ring', options: ['level ring', 'arc behind back'], group: 'Showcase' },
    free: P('Orbs free', 6, 0, 6, 1, 'Showcase'),
    spread: P('Standing pawns, distance from the centre (cells)', 2, 1, 4, .25, 'Showcase'),
    speed: P('Walk speed (cells/s)', 3, 1, 6, .25, 'Showcase'),
    radius: P('Radius (cells)', .45, .25, 1, .01, 'Shape'),
    height: P('Height (cells)', .95, 0, 1.5, .05, 'Shape'),
    size: P('Orb radius (cells)', .09, .04, .2, .005, 'Shape'),
    turn: P('Level ring, one turn', 10, 2, 20, .5, 'Timing (s)'),
    lag: P('Follow lag', .04, 0, .5, .01, 'Timing (s)'),
  },
  duration(p) { return p.scene === 'walking a square' ? Side * 4 / p.speed : StandSeconds; },
  phases(p) {
    if (p.scene !== 'walking a square') return [{ name: 'Standing', t: 0 }];
    return ['Walk east', 'Walk north', 'Walk west', 'Walk south'].map((name, i) => ({ name, t: i * Side / p.speed }));
  },
  events() { return []; },
  draw(s, p, { origin: o, scene }) {
    if (p.scene === 'walking a square') {
      const now = walk(s, p.speed), then = walk(s - p.lag, p.speed);
      pawn({ x: o.x + now.x, z: o.z + now.z }, now.facing, scene);
      orbs({ x: o.x + then.x, z: o.z + then.z }, now.heading, s, p, scene);
      return;
    }
    for (const facing of Facings) {
      const a = facing * Mathf.Deg2Rad, at = { x: o.x + Math.round(Math.cos(a)) * p.spread, z: o.z + Math.round(Math.sin(a)) * p.spread };
      pawn(at, facing, scene);
      orbs(at, facing, s, p, scene);
    }
  },
};
