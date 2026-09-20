// Power Pole: Extend Thrust — weapon ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (agreed 2026-09-21; the numbers are placeholders and will be XML fields; the full
// kit is in docs/power-pole-kit.md). The pawn carries a 1.2-cell red staff, a normal 1-cell melee
// weapon. Extend Thrust targets a cell up to 12 cells away with line of sight, 0.3 s warm-up. The
// pole extends along that line in 0.15 s and hits the first pawn on it, ally or enemy, for 22 blunt
// at 30% armor penetration. The pole keeps extending and carries that pawn 3 cells further, then
// retracts in 0.3 s. If a wall stops the carry early the pawn takes 10 more blunt. The pawn that was
// hit is staggered. Cooldown 8 s. The reach exists only inside this ability; the weapon's own melee
// verb stays at 1 cell.
//
// Drawing: the pole lies flat at hand height (PoleHeight), so it turns freely with the aim and
// needs no per-facing method. Only the shaft stretches; the two end ferrules are drawn at a fixed
// length so nothing smears. The caster, target, hands and wall are stand-ins. Dust and flash use
// the Six Paths SoftDisc and Puff textures as stand-ins for this kit's own.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, ShaderDatabase } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Body, Y, Floor, Lift, sprite, band, circle, glow, soft, rand } from './lib/six-paths-impact.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const disc = Meshes.disc(32, 'power pole disc');
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const Red = new Color(.80, .13, .10), RedLit = new Color(.98, .46, .36), RedDark = new Color(.25, .04, .04);
const Ferrule = new Color(.46, .07, .06), Skin = new Color(.83, .70, .54), Pale = new Color(1, .95, .8);
const Dust = new Color(.80, .74, .63), Stone = new Color(.34, .33, .35), StoneTop = new Color(.50, .49, .52);
// Decided looks and the rule's fixed numbers.
const PoleHeight = .5;          // cells above the ground, the hands
const RestBack = -.45, RestTip = .75;   // the carried 1.2-cell staff, along the aim from the caster
const PullBack = .35;           // how far the staff slides back in the wind-up
const FerruleLength = .14, Lunge = .25, StaggerShown = 1.2, Tail = .6;
const easeOut = x => 1 - Math.pow(1 - clamp(x), 3);

function times(p) {
  const thrust = p.windup, hit = thrust + p.extend, pushed = hit + p.push, retract = pushed + p.hold;
  const home = retract + p.retract;
  return { thrust, hit, pushed, retract, home, end: home + Tail + StaggerShown * .5 };
}

export default {
  kit: 'Power Pole', label: 'Extend Thrust (sketch)',
  params: {
    actors: { label: 'Show caster and target', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Target distance (cells)', 5, 2, 12, .5, 'Showcase'),
    scenario: { label: 'Behind the target', value: 'open ground', options: ['open ground', 'wall'], group: 'Showcase' },
    windup: P('Wind-up', .3, .1, .8, .05, 'Timing (s)'),
    extend: P('Extend to the target', .15, .05, .5, .01, 'Timing (s)'),
    push: P('Carry the target', .22, .1, .6, .01, 'Timing (s)'),
    hold: P('Hold at full length', .15, 0, .6, .05, 'Timing (s)'),
    retract: P('Retract', .3, .1, .8, .05, 'Timing (s)'),
    pushCells: P('Push (cells)', 3, 0, 5, .5, 'Rule'),
    width: P('Pole width (cells)', .12, .06, .25, .01, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Wind-up', t: 0 }, { name: 'Extend', t: t.thrust }, { name: 'Hit and carry', t: t.hit },
    { name: 'Hold', t: t.pushed }, { name: 'Retract', t: t.retract }, { name: 'Result', t: t.home },
  ]; },
  events(p) { const t = times(p); return [
    { t: t.hit, type: 'shake', value: .12 },
    ...(p.scenario === 'wall' ? [{ t: t.pushed, type: 'shake', value: .08 }] : []),
  ]; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a);
    const place = (base, along, across, h = 0) => ({ x: base.x + along * ca - across * sa, z: base.z + along * sa + across * ca + h * Lift });
    const cast = (base, along, across, h = 0) => ({ x: base.x + along * ca - across * sa + sun.x * h, z: base.z + along * sa + across * ca + sun.z * h });
    // The lit edge is whichever side of the pole is further north on screen.
    const litSide = ca >= 0 ? 1 : -1;

    // The scene is centred on the whole span, push included. All "along" values below are from the caster's feet.
    const feet = place(o, -(p.distance + p.pushCells) / 2, 0);
    const wallAlong = p.distance + 1.5, room = p.scenario === 'wall' ? Math.min(p.pushCells, wallAlong - .5 - .3 - p.distance) : p.pushCells;
    const contact = p.distance - .25;                               // the tip meets the target's body here

    // Tip and back end of the pole for any time. Everything else reads these.
    const tipAt = (time) => {
      if (time < t.thrust) return RestTip - PullBack * smooth(time / p.windup);
      if (time < t.hit) return lerp(RestTip - PullBack, contact, easeOut((time - t.thrust) / p.extend));
      if (time < t.pushed) return contact + room * easeOut((time - t.hit) / p.push);
      if (time < t.retract) return contact + room;
      return lerp(contact + room, RestTip, smooth((time - t.retract) / p.retract));
    };
    const backAt = (time) => {
      if (time < t.thrust) return RestBack - PullBack * smooth(time / p.windup);
      if (time < t.hit) return lerp(RestBack - PullBack, RestBack + Lunge, easeOut((time - t.thrust) / p.extend));
      if (time < t.retract) return RestBack + Lunge;
      return lerp(RestBack + Lunge, RestBack, smooth((time - t.retract) / p.retract));
    };
    const tip = tipAt(s), back = backAt(s), step = back - RestBack;  // the caster leans with the pole
    const carried = s < t.hit ? 0 : room * easeOut((s - t.hit) / p.push);
    const targetPos = place(feet, p.distance + carried, 0);

    const figure = (pos, colour, layer = pawnLayer) => {
      sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, .85, .4, Body.withAlpha(strength), soft, shadowLayer);
      draw(disc, pos.x, layer, pos.z + .18, .22, .32, 0, colour);
      draw(disc, pos.x, layer + .002, pos.z + .58, .16, .17, 0, Skin);
    };

    // Wall stand-in: one cell, one cell tall. Front face, then the top shifted north by Lift.
    if (p.scenario === 'wall') {
      const w = place(feet, wallAlong, 0), layer = pawnLayer + (w.z > targetPos.z ? -.01 : .01);
      sprite({ x: w.x + sun.x * .5, z: w.z + sun.z * .5 }, 1.5, 1.2, Body.withAlpha(strength), soft, shadowLayer);
      band('power pole wall front', [{ x: w.x - .5, z: w.z - .5 }, { x: w.x + .5, z: w.z - .5 }],
        [{ x: w.x - .5, z: w.z - .5 + Lift }, { x: w.x + .5, z: w.z - .5 + Lift }], Stone, layer);
      band('power pole wall top', [{ x: w.x - .5, z: w.z - .5 + Lift }, { x: w.x + .5, z: w.z - .5 + Lift }],
        [{ x: w.x - .5, z: w.z + .5 + Lift }, { x: w.x + .5, z: w.z + .5 + Lift }], StoneTop, layer + .002);
    }

    if (p.actors) {
      figure(place(feet, step, 0), new Color(.93, .50, .13));
      figure(targetPos, new Color(.55, .38, .27));
    }

    // Dust kicked up on the ground under the tip as it passes, so the line reads when zoomed out.
    const firstTime = (along) => { for (let time = t.thrust; time < t.pushed; time += .01) if (tipAt(time) >= along) return time; return null; };
    const reach = contact + room, puffs = Math.max(4, Math.round(reach * 1.6));
    for (let i = 0; i < puffs; i++) {
      const along = lerp(1.2, reach, (i + .5) / puffs), born = firstTime(along);
      if (born === null) continue;
      const life = .45 + rand(i) * .3, u = (s - born) / life;
      if (u < 0 || u > 1) continue;
      const side = (rand(i + 40) - .5) * .5, size = .4 + u * .7;
      sprite(place(feet, along - u * .3, side, u * .25), size, size * .7, Dust.withAlpha(Math.sin(u * Math.PI) * .7), puff, Y - .02);
    }

    // The pole. Shadow, dark outline, red body, lit strip, then two fixed-length ferrules.
    const w = p.width, h = PoleHeight;
    const strip = (key, from, to, lo, hi, colour, layer) => band(key,
      [place(feet, from, lo, h), place(feet, to, lo, h)], [place(feet, from, hi, h), place(feet, to, hi, h)], colour, layer);
    band('power pole shadow', [cast(feet, back, -w / 2, h), cast(feet, tip, -w / 2, h)],
      [cast(feet, back, w / 2, h), cast(feet, tip, w / 2, h)], Body.withAlpha(strength), shadowLayer);
    strip('power pole outline', back - .02, tip + .02, -w / 2 - .02, w / 2 + .02, RedDark, Y);
    strip('power pole body', back, tip, -w / 2, w / 2, Red, Y + .002);
    strip('power pole lit', back, tip, litSide * w * .12, litSide * w * .40, RedLit, Y + .004);
    strip('power pole ferrule back', back, back + FerruleLength, -w / 2, w / 2, Ferrule, Y + .006);
    strip('power pole ferrule tip', tip - FerruleLength, tip, -w / 2, w / 2, Ferrule, Y + .006);
    if (p.actors) for (const grip of [.05, .42]) {
      const hand = place(feet, step + grip, 0, h);
      draw(disc, hand.x, Y + .008, hand.z, .075, .075, 0, Skin);
    }

    // Speed lines beside the shaft while the tip is moving out.
    const moving = s >= t.thrust && s < t.pushed ? (s < t.hit ? 1 : 1 - easeOut((s - t.hit) / p.push)) : 0;
    if (moving > .02) for (let i = 0; i < 6; i++) {
      const across = (i % 2 ? 1 : -1) * (.14 + rand(i + 7) * .2), length = (.8 + rand(i + 3) * 1.4) * moving;
      const head = tip - rand(i + 11) * .6;
      strip(`power pole speed ${i}`, Math.max(back, head - length), head, across - .012, across + .012, Pale.withAlpha(.55 * moving), Y + .01);
    }

    // The hit: flash and ring on the target, a few dust puffs thrown along the aim.
    const hitAge = s - t.hit;
    if (hitAge >= 0 && hitAge < .5) {
      const spot = place(feet, contact, 0, h);
      sprite(spot, 1.6, 1.1, Pale.withAlpha(Math.max(0, 1 - hitAge / .12) * .85), glow, Y + .02);
      circle(place(feet, contact, 0), .25 + hitAge * 2.2, (1 - hitAge / .5) * .6, Floor, Pale);
      for (let i = 0; i < 8; i++) {
        const u = hitAge / (.3 + rand(i + 60) * .2);
        if (u > 1) continue;
        const spread = (rand(i + 70) - .5) * 1.4, far = u * (.6 + rand(i + 80) * 1.2);
        sprite(place(feet, contact + far, spread * u, Math.sin(u * Math.PI) * .4), .25 + u * .4, .2 + u * .3,
          Dust.withAlpha(Math.sin(u * Math.PI) * .6), puff, Y + .012);
      }
    }
    // The wall stops the carry: a smaller dust burst on the wall face.
    const slamAge = s - t.pushed;
    if (p.scenario === 'wall' && room < p.pushCells && slamAge >= 0 && slamAge < .45) {
      const face = place(feet, wallAlong - .5, 0, .3);
      sprite(face, 1.1, .8, Pale.withAlpha(Math.max(0, 1 - slamAge / .1) * .5), glow, Y + .02);
      for (let i = 0; i < 6; i++) {
        const u = slamAge / .45, spread = (rand(i + 90) - .5) * 1.6;
        sprite(place(feet, wallAlong - .5 - u * .5, spread * (.3 + u), .3 + u * .3), .3 + u * .4, .25 + u * .3,
          Dust.withAlpha(Math.sin(u * Math.PI) * .55), puff, Y + .012);
      }
    }

    // Stagger mark: three pale dots circling the target's head for StaggerShown seconds after the carry.
    const dazed = s - t.hit;
    if (p.actors && dazed >= 0) {
      const fade = 1 - smooth((s - t.pushed - StaggerShown) / .3);
      for (let i = 0; i < 3; i++) {
        const turn = s * 5 + i * 2.094;
        sprite({ x: targetPos.x + Math.cos(turn) * .2, z: targetPos.z + .86 + Math.sin(turn) * .06 }, .08, .08, Pale.withAlpha(.9 * fade), soft, Y + .03);
      }
    }
  },
};
