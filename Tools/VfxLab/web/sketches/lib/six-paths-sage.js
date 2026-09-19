// The Six Paths user as a sketch draws it: a stand-in pawn, its six carried orbs on a level ring,
// and the size rule for an orb that leaves the ring. Proposed, none of it agreed.
//
// Ring values are the "level ring" defaults of six-paths-orbs-carried.js. An orb is IdleRadius
// while carried. One that an ability uses leaves its slot, grows to that ability's cast radius
// over the flight out (deployRadius), and shrinks back over the flight home. Its slot stays
// empty while it is away, so the ring is also the orb counter.
//
// Positions are ground positions (the pawn's feet); the pawn sprite is centred Feet north of that.
import { AltitudeLayer, Color, Mathf, Meshes } from '../../js/engine.js';
import { draw } from './six-paths-solid.js';
import { Lift, orb, sprite, soft } from './six-paths-impact.js';

export const Orbs = 6, IdleRadius = .09, RingRadius = .45, RingHeight = .95, RingTurn = 10;
// Cast radius by job: forms held on the pawn, casts out on the field, the heavy single masses
// (0.42 is SixPathsSlam.OrbSize).
export const OnPawn = .20, Field = .30, Heavy = .42;
export const Facings = { East: 0, North: 90, West: 180, South: 270 };

const Feet = .3, Bob = .04, BobHz = .5;
const disc = Meshes.disc(32, 'sage disc');
const pawnLayer = AltitudeLayer.Pawn.AltitudeFor(), shadowLayer = AltitudeLayer.Shadows.AltitudeFor();
const Shirt = new Color(.35, .55, .68), Skin = new Color(.83, .70, .54), Hair = new Color(.22, .15, .10);
const Shade = new Color(.03, .03, .05);
const light = (scene) => ({ sun: scene?.shadowVector ?? { x: -.45, z: -.32 }, strength: scene?.sun?.strength ?? .32 });

/** Radius of an orb `u` of the way (0 to 1) from its ring slot to where it does its work. */
export const deployRadius = (u, target) => Mathf.Lerp(IdleRadius, target, Mathf.Smooth(Mathf.Clamp01(u)));

/** Ring slot `i` at time `s`: where the orb shows on screen, its ground point, height and layer. */
export function slot(ground, i, s) {
  const angle = (s / RingTurn + i / Orbs) * Math.PI * 2;
  const gx = Math.cos(angle) * RingRadius, gz = Math.sin(angle) * RingRadius;
  const h = RingHeight + Math.sin((s * BobHz + i / Orbs) * Math.PI * 2) * Bob;
  return { x: ground.x + gx, z: ground.z + gz + h * Lift, ground: { x: ground.x + gx, z: ground.z + gz }, h,
    layer: gz > 0 ? pawnLayer - .02 : pawnLayer + .02 };
}

/** The stand-in pawn. `facing` is degrees: 0 east, 90 north, 180 west, 270 south. */
export function sage(ground, facing, scene, bob = 0) {
  const { sun, strength } = light(scene), side = facing === 0 ? 1 : facing === 180 ? -1 : 0;
  const x = ground.x, z = ground.z + Feet + bob;
  sprite({ x: x + sun.x * .4, z: ground.z + sun.z * .4 }, .9, .4, Shade.withAlpha(strength), soft, shadowLayer);
  draw(disc, x, pawnLayer, z - .1, .23, .27, 0, Shirt);
  draw(disc, x + side * .03, pawnLayer + .003, z + .3, .17, .17, 0, facing === 90 ? Hair : Skin);
  if (side) draw(disc, x + side * .15, pawnLayer + .004, z + .31, .025, .03, 0, Hair);
  else if (facing === 270) for (const e of [-1, 1]) draw(disc, x + e * .065, pawnLayer + .004, z + .29, .025, .03, 0, Hair);
}

/** The carried orbs, each with its ground shadow. `away(i)` true leaves slot `i` empty. */
export function carried(ground, s, scene, away = () => false) {
  const { sun, strength } = light(scene);
  for (let i = 0; i < Orbs; i++) {
    if (away(i)) continue;
    const q = slot(ground, i, s);
    sprite({ x: q.ground.x + sun.x * q.h, z: q.ground.z + sun.z * q.h }, IdleRadius * 2.4, IdleRadius * 1.3,
      Shade.withAlpha(strength * .8), soft, shadowLayer + .001);
    orb(q, IdleRadius, 1, 1, q.layer);
  }
}
