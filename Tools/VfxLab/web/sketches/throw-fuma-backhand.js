// Fuma backhand throw — a trial of a new throw clip on all four facings at once, not a new effect.
//
// What it shows. Four pawns stand around the chosen cell and each throws outward: east, north,
// west, south. Each plays RimArt_ThrowFumaBackhand through playClip() with the game's aim rule, so
// east and west are the East clip and its mirror, and north and south are their own clips. "Clip"
// switches all four to the current RimArt_ThrowFuma for comparison. Both clips are 1.2 s long and
// release at 0.8 s. At release a Fuma projectile leaves at 14.4 cells/s (AG_FumaProjectile
// <speed>24</speed>) and stops at "Throw distance".
//
// The backhand (trial, not agreed). The hand crosses the chest to the off side, holds, then sweeps
// forward and around the front to the throwing side: a half circle seen from above. The ring leaves
// one third of the way in, while the hand still moves toward the target (about 6.5 cells/s, 23
// degrees off forward). The rest of the arc is an empty hand. In the East clip the body shows its
// back (north) sprite from 0.40 s to 0.76 s to stand in for the torso turn.
//
// What to look at. North and south show the half circle at full size. East and west put the far
// side of the sweep and height on the same screen axis; the hand path overlay shows what is left.
//
// Drawing: the pawns are the clip's stand-in pawn. The projectile is the Unfolded texture at the
// projectile's drawSize 1.4; its spin rate (720 degrees/s) is a stand-in, not read from the game.
import { AltitudeLayer, Color, MaterialPool, ShaderDatabase } from '../js/engine.js';
import { playClip, clipHandPath } from '../js/animation.js';
import { P, Floor, sprite, circle, trail } from './lib/six-paths-impact.js';

const fuma = MaterialPool.MatFrom('RimArt/Fuma/Unfolded', ShaderDatabase.Cutout);
const projectileLayer = AltitudeLayer.Projectile.AltitudeFor(), overlay = AltitudeLayer.MetaOverlays.AltitudeFor();
const pathColour = new Color(1, .85, .3, .85), mark = new Color(1, .45, .3);
const Clips = { 'backhand (trial)': 'RimArt_ThrowFumaBackhand', 'current Fuma clip': 'RimArt_ThrowFuma' };
const ClipLength = 1.2, Release = .8;   // both clips; used until the clip has loaded
const ProjectileSpeed = 14.4, ProjectileSize = 1.4, Spin = 720, Lead = .3, Tail = .5;
const Aims = [0, 90, 180, 270];

export default {
  kit: 'Throw', label: 'Fuma backhand, four facings (sketch)',
  params: {
    clip: { label: 'Clip', value: 'backhand (trial)', options: Object.keys(Clips), group: 'Showcase' },
    spread: P('Pawn distance from the centre (cells)', 2.5, 1.5, 5, .25, 'Showcase'),
    distance: P('Throw distance (cells)', 5, 2, 10, .5, 'Showcase'),
    path: { label: 'Hand path', value: true, group: 'Showcase' },
    origin: { label: 'Projectile starts at', value: 'pawn centre (as in game)', options: ['pawn centre (as in game)', 'hand at release'], group: 'Showcase' },
  },
  duration(p) { return Lead + Math.max(ClipLength, Release + p.distance / ProjectileSpeed) + Tail; },
  phases(p) { return [
    { name: 'Stand', t: 0 }, { name: 'Cross over', t: Lead }, { name: 'Loaded', t: Lead + .45 }, { name: 'Sweep', t: Lead + .76 },
    { name: 'Release', t: Lead + Release }, { name: 'Follow through', t: Lead + .84 }, { name: 'Clip ends', t: Lead + ClipLength },
  ]; },
  events() { return []; },
  draw(s, p, { origin: o, scene }) {
    const name = Clips[p.clip];
    Aims.forEach((aim, i) => {
      const a = aim * Math.PI / 180, dir = { x: Math.round(Math.cos(a)), z: Math.round(Math.sin(a)) };
      const thrower = { x: o.x + dir.x * p.spread, z: o.z + dir.z * p.spread };
      const clip = playClip(name, s - Lead, thrower, { aim, scene });
      if (!clip) return;
      if (p.path) {
        const path = clipHandPath(name, thrower, { aim });
        if (path) {
          trail(`fuma backhand path ${i}`, path.points, .03, pathColour, overlay);
          if (path.release) circle(path.release, .05, .95, overlay + .002, mark);
        }
      }
      const age = s - Lead - (clip.release ?? Release);
      if (age < 0) return;
      const start = p.origin === 'hand at release' && clip.itemAtRelease ? clip.itemAtRelease : thrower;
      const end = { x: thrower.x + dir.x * p.distance, z: thrower.z + dir.z * p.distance };
      const length = Math.hypot(end.x - start.x, end.z - start.z), u = Math.min(1, age * ProjectileSpeed / length);
      const at = { x: start.x + (end.x - start.x) * u, z: start.z + (end.z - start.z) * u };
      // A right-hand backhand spins the ring clockwise from above; the mirrored clip the other way.
      const turned = Math.min(age, length / ProjectileSpeed) * Spin * (clip.mirror ? -1 : 1);
      sprite(at, ProjectileSize, ProjectileSize, Color.white, fuma, u < 1 ? projectileLayer : Floor + .05, turned);
    });
  },
};
