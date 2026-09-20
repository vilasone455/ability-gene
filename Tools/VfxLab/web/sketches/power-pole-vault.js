// Power Pole: Pole Vault — weapon ability proposal, not the game. Nothing in Source/RimArt draws this yet.
// Superseded by Vault Strike (power-pole-vault-strike.js): this one is a jump with nothing at the end.
// Kept for comparison; the kit's third ability is Vault Strike.
//
// What it is for (proposed, none of it agreed). Targets a standable cell up to 10 cells away. No
// line of sight is needed: the pawn goes over walls. Neither the start cell nor the landing cell
// may be roofed, because the pawn rises PeakHeight cells. 0.3 s plant, then 0.9 s in the air. No
// damage; the pawn cannot be hit in melee while airborne. Cooldown 15 s. It is the kit's
// gap-closer and escape; Extend Thrust moves the enemy, this moves the pawn.
//
// Order: the pawn plants the staff's front tip in the ground, the pole extends and pushes the pawn
// up a ballistic arc, at the peak the foot leaves the ground and the pole retracts up to the pawn,
// the pawn falls the second half holding the short staff, lands, dust. Retracting at the peak
// keeps the pole from lying across the wall that was just cleared.
//
// Drawing: the pole is a straight strip between two 3D points (along, height), width measured
// across its own screen direction, so one routine serves every aim. Aimed east or west the arc
// shows on screen. Aimed north or south, travel and height share the screen's vertical axis, and
// aimed south they cancel: the pole shrank to a dot. So those aims get their own method: height
// also shifts the drawing east, Bow x height x |sin(aim)| cells, which opens the arc and the pole
// sideways. The amount follows the aim, so the change between directions is gradual. Shadows and
// floor markers do not bow; they stay on the true cells. The ferrules keep a fixed length. Caster,
// hands and walls are stand-ins. Dust uses the Six Paths SoftDisc and Puff textures as stand-ins
// for this kit's own. In game the flight would be a PawnFlyer.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, ShaderDatabase } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Body, Y, Floor, Lift, sprite, band, trail, circle, soft, rand } from './lib/six-paths-impact.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const disc = Meshes.disc(32, 'power pole vault disc');
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const Red = new Color(.80, .13, .10), RedLit = new Color(.98, .46, .36), RedDark = new Color(.25, .04, .04);
const Ferrule = new Color(.46, .07, .06), Skin = new Color(.83, .70, .54), Pale = new Color(1, .95, .8);
const Dust = new Color(.80, .74, .63), Stone = new Color(.34, .33, .35), StoneTop = new Color(.50, .49, .52);
// Decided looks and the rule's fixed numbers.
const Range = 10;
const CarryHeight = .5, RestBack = -.45, RestTip = .75;   // the carried 1.2-cell staff, same as Extend Thrust
const Foot = .4;                // where the tip is planted, cells ahead of the pawn
const TopBack = -.1, TopHeight = 1.1;   // the pole's top end while the pawn hangs on it, from the pawn's feet
const Bow = .45;                // north/south aims: cells east per cell of height, see Drawing
const RetractTime = .22, FerruleLength = .14, Tail = 1;

function times(p) {
  const launch = p.windup, peak = launch + p.flight / 2, land = launch + p.flight;
  return { launch, peak, land, end: land + Tail };
}

export default {
  kit: 'Power Pole', label: 'Pole Vault (sketch)',
  params: {
    actors: { label: 'Show the pawn', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Landing distance (cells)', 8, 3, Range, .5, 'Showcase'),
    scenario: { label: 'In the way', value: 'wall', options: ['open ground', 'wall'], group: 'Showcase' },
    windup: P('Plant the tip', .3, .1, .8, .05, 'Timing (s)'),
    flight: P('In the air', .9, .4, 2, .05, 'Timing (s)'),
    peak: P('Peak height (cells)', 2.5, 1, 4, .1, 'Shape'),
    width: P('Pole width (cells)', .12, .06, .25, .01, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Plant', t: 0 }, { name: 'Pole pushes up', t: t.launch }, { name: 'Retract and fall', t: t.peak }, { name: 'Landed', t: t.land },
  ]; },
  events(p) { const t = times(p); return [{ t: t.launch, type: 'shake', value: .04 }, { t: t.land, type: 'shake', value: .07 }]; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a), D = p.distance;
    const bow = Bow * Math.abs(sa);
    const place = (base, along, across, h = 0) => ({ x: base.x + along * ca - across * sa + h * bow, z: base.z + along * sa + across * ca + h * Lift });
    const cast = (base, along, across, h = 0) => ({ x: base.x + along * ca - across * sa + sun.x * h, z: base.z + along * sa + across * ca + sun.z * h });
    const feet = place(o, -D / 2, 0);                                // scene centred on the jump; "along" is from the start cell

    // The pawn: on the ground, then a ballistic arc at constant ground speed, then on the ground.
    const pawnAt = (time) => {
      const u = clamp((time - t.launch) / p.flight);
      return { along: D * u, h: p.peak * 4 * u * (1 - u) };
    };
    const pawn = pawnAt(s), airborne = s > t.launch && s < t.land;

    // The pole's two ends as (along, height). A is the tip that gets planted, B is the end in the hands.
    const mix = (from, to, k) => ({ along: lerp(from.along, to.along, k), h: lerp(from.h, to.h, k) });
    const carryA = { along: pawn.along + RestTip, h: pawn.h + CarryHeight }, carryB = { along: pawn.along + RestBack, h: pawn.h + CarryHeight };
    const planted = { along: Foot, h: 0 }, top = { along: pawn.along + TopBack, h: pawn.h + TopHeight };
    let A, B;
    if (s < t.launch) { const k = smooth(s / p.windup); A = mix(carryA, planted, k); B = mix(carryB, top, k); }
    else if (s < t.peak) { A = planted; B = top; }
    else {
      // The planted end comes up to the pawn and becomes the trailing end of the carried staff.
      const k = smooth((s - t.peak) / RetractTime);
      A = mix(planted, { along: pawn.along + RestBack, h: pawn.h + CarryHeight }, k);
      B = mix(top, { along: pawn.along + RestTip, h: pawn.h + CarryHeight }, k);
    }

    // Floor markers at the rule's true places: the range around the start, and the landing cell.
    const markers = 1 - smooth((s - t.land) / .4);
    circle(feet, Range, .22 * markers);
    circle(place(feet, D, 0), .45, .7 * markers, Floor, Pale);

    // Wall stand-in: three cells across the path at the halfway point, one cell tall. North first.
    if (p.scenario === 'wall') {
      const cells = [-1, 0, 1].map(across => place(feet, D / 2, across)).sort((m, n) => n.z - m.z);
      cells.forEach((w, i) => {
        sprite({ x: w.x + sun.x * .5, z: w.z + sun.z * .5 }, 1.5, 1.2, Body.withAlpha(strength), soft, shadowLayer);
        band(`power pole vault wall front ${i}`, [{ x: w.x - .5, z: w.z - .5 }, { x: w.x + .5, z: w.z - .5 }],
          [{ x: w.x - .5, z: w.z - .5 + Lift }, { x: w.x + .5, z: w.z - .5 + Lift }], Stone, pawnLayer + .01 + i * .004);
        band(`power pole vault wall top ${i}`, [{ x: w.x - .5, z: w.z - .5 + Lift }, { x: w.x + .5, z: w.z - .5 + Lift }],
          [{ x: w.x - .5, z: w.z + .5 + Lift }, { x: w.x + .5, z: w.z + .5 + Lift }], StoneTop, pawnLayer + .012 + i * .004);
      });
    }

    // The pawn. In the air it draws above walls; its shadow stays on the ground and drifts along the sun.
    if (p.actors) {
      const pos = place(feet, pawn.along, 0, pawn.h), shade = cast(feet, pawn.along, 0, pawn.h), layer = airborne ? Y + .02 : pawnLayer;
      const spread = 1 + pawn.h * .25;
      sprite({ x: shade.x + sun.x * .45, z: shade.z + sun.z * .45 }, .85 * spread, .4 * spread, Body.withAlpha(strength / spread), soft, shadowLayer);
      draw(disc, pos.x, layer, pos.z + .18, .22, .32, 0, new Color(.93, .50, .13));
      draw(disc, pos.x, layer + .002, pos.z + .58, .16, .17, 0, Skin);
      // A short trail behind the pawn while it moves fast.
      if (airborne) {
        const pts = [0, .04, .08, .12, .16].map(back => { const q = pawnAt(Math.max(t.launch, s - back)); return place(feet, q.along, 0, q.h + .3); }).reverse();
        trail('power pole vault trail', pts, .18, Pale.withAlpha(.25), Y + .015);
      }
    }

    // The pole: a strip between the two ends, width across its own screen direction.
    const a0 = place(feet, A.along, 0, A.h), b0 = place(feet, B.along, 0, B.h);
    const sx = b0.x - a0.x, sz = b0.z - a0.z, screenLength = Math.hypot(sx, sz) || 1;
    let nx = -sz / screenLength, nz = sx / screenLength;
    if (nz < 0 || (nz === 0 && nx > 0)) { nx = -nx; nz = -nz; }     // the normal points north, so the lit strip is the north side
    const length = Math.hypot(B.along - A.along, B.h - A.h) || 1, cap = Math.min(.45, FerruleLength / length);
    const point = (k, side) => ({ x: a0.x + sx * k + nx * side, z: a0.z + sz * k + nz * side });
    const strip = (key, from, to, lo, hi, colour, layer) => band(key, [point(from, lo), point(to, lo)], [point(from, hi), point(to, hi)], colour, layer);
    const w = p.width, grow = .02 / screenLength;
    {
      const sa0 = cast(feet, A.along, 0, A.h), sb0 = cast(feet, B.along, 0, B.h);
      const dx = sb0.x - sa0.x, dz = sb0.z - sa0.z, l = Math.hypot(dx, dz) || 1, mx = -dz / l * w / 2, mz = dx / l * w / 2;
      band('power pole vault shadow', [{ x: sa0.x - mx, z: sa0.z - mz }, { x: sb0.x - mx, z: sb0.z - mz }],
        [{ x: sa0.x + mx, z: sa0.z + mz }, { x: sb0.x + mx, z: sb0.z + mz }], Body.withAlpha(strength), shadowLayer);
    }
    strip('power pole vault outline', -grow, 1 + grow, -w / 2 - .02, w / 2 + .02, RedDark, Y);
    strip('power pole vault body', 0, 1, -w / 2, w / 2, Red, Y + .002);
    strip('power pole vault lit', 0, 1, w * .12, w * .40, RedLit, Y + .004);
    strip('power pole vault ferrule a', 0, cap, -w / 2, w / 2, Ferrule, Y + .006);
    strip('power pole vault ferrule b', 1 - cap, 1, -w / 2, w / 2, Ferrule, Y + .006);
    if (p.actors) for (const grip of [.18, .5]) {
      const k = 1 - Math.min(.9, grip / length), hand = point(k, 0);
      draw(disc, hand.x, Y + .03, hand.z, .075, .075, 0, Skin);
    }

    // Dust: a kick at the planted tip when the pole starts to push, a smaller stream while it pushes,
    // and a ring of puffs where the pawn lands.
    const burst = (key, centre, age, count, reachOut, alpha) => {
      if (age < 0 || age > .6) return;
      for (let i = 0; i < count; i++) {
        const life = .35 + rand(i + key) * .25, u = age / life;
        if (u > 1) continue;
        const turn = i * 2.399 + key, far = reachOut * (.25 + u * (.6 + rand(i + key + 9) * .6));
        sprite({ x: centre.x + Math.cos(turn) * far, z: centre.z + Math.sin(turn) * far * .7 + Math.sin(u * Math.PI) * .2 },
          .3 + u * .5, .24 + u * .4, Dust.withAlpha(Math.sin(u * Math.PI) * alpha), puff, Y - .02);
      }
    };
    const footSpot = place(feet, Foot, 0);
    burst(100, footSpot, s - t.launch, 9, .9, .7);
    if (s > t.launch && s < t.peak) for (let i = 0; i < 4; i++) {
      const u = ((s - t.launch) * 3 + i / 4) % 1;
      sprite({ x: footSpot.x + (rand(i + 30) - .5) * .5, z: footSpot.z + u * .3 }, .2 + u * .3, .16 + u * .24, Dust.withAlpha(Math.sin(u * Math.PI) * .45), puff, Y - .02);
    }
    const landSpot = place(feet, D, 0);
    burst(200, landSpot, s - t.land, 12, 1.1, .75);
    circle(landSpot, .3 + (s - t.land) * 2.4, s >= t.land ? (1 - clamp((s - t.land) / .45)) * .55 : 0, Floor, Pale);
  },
};
