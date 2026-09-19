// Kunai throw — a check of the throw clip against its projectile on one clock, not a new effect.
//
// What it shows. The pawn plays RimArt_ThrowKunai through playClip() from web/js/animation.js, the
// same clip and the same aim rule the game uses. At the clip's release time (0.30 s, the moment the
// kunai in the hand is switched off) a kunai projectile leaves for the target at 24 cells/s, which
// is AG_KunaiProjectile's <speed>40</speed> (speed / 100 cells per tick, 60 ticks per second), and
// sticks in the target.
//
// What to look at. PendingThrow launches the projectile from thrower.DrawPos, the middle of the
// pawn, not from the hand. "Projectile starts at" switches between that and the hand so the gap
// between where the kunai disappears and where it reappears can be seen and measured; the floor
// mark shows the hand's position at release.
//
// Drawing: the pawn is the clip's stand-in pawn. The projectile is the kunai texture at the
// projectile's drawSize 0.75, turned to its flight direction. The target is a stand-in.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, ShaderDatabase } from '../js/engine.js';
import { playClip } from '../js/animation.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Floor, sprite, circle, glow, soft } from './lib/six-paths-impact.js';

const disc = Meshes.disc(32, 'kunai demo disc');
const kunai = MaterialPool.MatFrom('RimArt/Kunai/Kunai', ShaderDatabase.Cutout);
const pawnLayer = AltitudeLayer.Pawn.AltitudeFor(), projectileLayer = AltitudeLayer.Projectile.AltitudeFor();
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor();
const pale = new Color(1, .95, .8), mark = new Color(1, .45, .3);
const Clip = 'RimArt_ThrowKunai', ClipLength = .6, Release = .3;   // used until the clip has loaded
const ProjectileSpeed = 24, ProjectileSize = .75, Lead = .4, Tail = .8;

export default {
  kit: 'Throw', label: 'Kunai throw (sketch)',
  params: {
    aim: P('Target direction (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Target distance (cells)', 7, 2, 14, .5, 'Showcase'),
    origin: { label: 'Projectile starts at', value: 'pawn centre (as in game)', options: ['pawn centre (as in game)', 'hand at release'], group: 'Showcase' },
    target: { label: 'Show target pawn', value: true, group: 'Showcase' },
  },
  duration(p) { return Lead + Release + p.distance / ProjectileSpeed + Tail; },
  phases(p) { return [
    { name: 'Stand', t: 0 }, { name: 'Throw clip', t: Lead }, { name: 'Release / projectile', t: Lead + Release },
    { name: 'Hit', t: Lead + Release + p.distance / ProjectileSpeed }, { name: 'Clip ends', t: Lead + ClipLength },
  ].sort((a, b) => a.t - b.t); },
  events() { return []; },
  draw(s, p, { origin: o, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, dir = { x: Math.cos(a), z: Math.sin(a) };
    const thrower = { x: o.x - dir.x * p.distance / 2, z: o.z - dir.z * p.distance / 2 };
    const victim = { x: thrower.x + dir.x * p.distance, z: thrower.z + dir.z * p.distance };

    if (p.target) {
      sprite({ x: victim.x + sun.x * .4, z: victim.z - .3 + sun.z * .4 }, .9, .4, new Color(.03, .03, .05, strength), soft, shadowLayer);
      draw(disc, victim.x, pawnLayer, victim.z - .1, .23, .27, 0, new Color(.62, .45, .22));
      draw(disc, victim.x, pawnLayer + .003, victim.z + .3, .17, .17, 0, new Color(.83, .70, .54));
    }

    const clip = playClip(Clip, s - Lead, thrower, { aim: p.aim, scene });
    if (!clip) return;
    const release = Lead + (clip.release ?? Release), fromHand = p.origin === 'hand at release';
    const start = fromHand && clip.itemAtRelease ? clip.itemAtRelease : thrower;
    const length = Math.hypot(victim.x - start.x, victim.z - start.z), flight = length / ProjectileSpeed, age = s - release;
    // Where the hand let go, and how far that is from where the projectile appears.
    if (clip.itemAtRelease && age >= 0) {
      circle(clip.itemAtRelease, .08, .9, Floor + .02, mark);
      if (!fromHand) circle(thrower, .08, .9, Floor + .02, pale);
    }
    if (age < 0) return;
    const u = Math.min(1, age / flight), at = { x: start.x + (victim.x - start.x) * u, z: start.z + (victim.z - start.z) * u };
    // Projectile textures point north; DrawMesh angles run clockwise.
    const heading = 90 - Math.atan2(victim.z - start.z, victim.x - start.x) * Mathf.Rad2Deg;
    sprite(at, ProjectileSize, ProjectileSize, Color.white, kunai, u < 1 ? projectileLayer : pawnLayer + .01, heading);
    const hit = age - flight;
    if (hit >= 0 && hit < .2) sprite(victim, .5 + hit * 3, .5 + hit * 3, pale.withAlpha((1 - hit / .2) * .8), glow);
  },
};
