// Power Pole: Vault Strike — weapon ability proposal, not the game. Nothing in Source/RimArt draws this yet.
// A second take on Pole Vault (power-pole-vault.js), which was a jump with nothing at the end.
//
// What it is for (proposed, none of it agreed). Targets an enemy pawn or a standable cell up to 10
// cells away; no line of sight is needed, the pawn goes over walls. 0.3 s plant, then 0.9 s in the
// air. The pole pushes the pawn up to the peak, the pawn whips the pole over its head, falls with it
// held high, and 0.05 s before landing the pole extends down and slams the target cell: 20 blunt and
// a 1.5 s stun to a pawn on that cell, stagger to every pawn within 1.5 cells of it. The pawn lands
// one cell short of the target. On an empty cell the slam only staggers. Cooldown 18 s. It is the
// kit's engage: over the wall, onto the shooter behind it. Extend Thrust is one target far away
// pushed back, Sweep is many targets close with no stun.
//
// Drawing: the pole is a straight strip between two points (along, height). While it is whipped and
// struck it is a length and an angle from the hands in the along-height plane. North and south aims
// get their own method, as in Pole Vault: height also shifts the drawing east by
// Bow x height x |sin(aim)| cells, so the arc, the whip and the strike open sideways instead of
// collapsing onto one line. Shadows, cracks and floor rings do not bow; they are on the true cells.
// The ferrules keep a fixed length. Pawns, hands and walls are stand-ins. Dust and flashes use the
// Six Paths SoftDisc and Puff textures. In game the flight would be a PawnFlyer.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, ShaderDatabase } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Body, Y, Floor, Lift, sprite, band, trail, circle, glow, soft, rand } from './lib/six-paths-impact.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, Rad = Mathf.Deg2Rad;
const disc = Meshes.disc(32, 'power pole strike disc');
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const Red = new Color(.80, .13, .10), RedLit = new Color(.98, .46, .36), RedDark = new Color(.25, .04, .04);
const Ferrule = new Color(.46, .07, .06), Skin = new Color(.83, .70, .54), Pale = new Color(1, .95, .8);
const Dust = new Color(.80, .74, .63), Stone = new Color(.34, .33, .35), StoneTop = new Color(.50, .49, .52);
const Stun = new Color(1, .85, .2), Crack = new Color(.06, .05, .04);
// Decided looks and the rule's fixed numbers.
const Range = 10, LandShort = 1, StaggerRadius = 1.5;
const CarryHeight = .5, RestBack = -.45, RestTip = .75;   // the carried 1.2-cell staff, same as Extend Thrust
const Foot = .4;                // where the tip is planted, cells ahead of the pawn
const TopBack = -.1, TopHeight = 1.1;   // the pole's hand end while the pawn hangs on it, from the pawn's feet
const HandsHigh = .95, Butt = .3;       // hands over the head in the air; pole left behind the hands
const WhipLong = 3, RaisedAngle = 70;   // pole length and angle (degrees up from the aim) held while falling
const WhipTime = .25, StrikeTime = .15, StrikeEarly = .05, Pinned = .15, Retract = .25, Tail = 1.2;
const Bow = .45;                // north/south aims: cells east per cell of height, see Drawing
const FerruleLength = .14, StunShown = 1.5;
// Stand-ins near the target as [cells past the target, cells across]: one inside the stagger radius, one outside.
const Bystanders = [[.8, 1], [-.5, -2.2]];

function times(p) {
  const launch = p.windup, peak = launch + p.flight / 2, land = launch + p.flight;
  const strikeHit = land - StrikeEarly, strikeStart = strikeHit - StrikeTime, whipEnd = Math.min(peak + WhipTime, strikeStart);
  const pinEnd = land + Pinned, home = pinEnd + Retract;
  return { launch, peak, whipEnd, strikeStart, strikeHit, land, pinEnd, home, end: home + Tail };
}

export default {
  kit: 'Power Pole', label: 'Vault Strike (sketch)',
  params: {
    actors: { label: 'Show pawns', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Target distance (cells)', 8, 4, Range, .5, 'Showcase'),
    scenario: { label: 'Target', value: 'enemy behind a wall', options: ['enemy behind a wall', 'enemy in the open', 'empty cell'], group: 'Showcase' },
    windup: P('Plant the tip', .3, .1, .8, .05, 'Timing (s)'),
    flight: P('In the air', .9, .7, 2, .05, 'Timing (s)'),
    peak: P('Peak height (cells)', 2.5, 1, 4, .1, 'Shape'),
    width: P('Pole width (cells)', .12, .06, .25, .01, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Plant', t: 0 }, { name: 'Pole pushes up', t: t.launch }, { name: 'Whip overhead', t: t.peak },
    { name: 'Fall, pole raised', t: t.whipEnd }, { name: 'Strike', t: t.strikeStart }, { name: 'Land and retract', t: t.land }, { name: 'Result', t: t.home },
  ]; },
  events(p) { const t = times(p); return [
    { t: t.launch, type: 'shake', value: .04 }, { t: t.strikeHit, type: 'shake', value: .15 }, { t: t.land, type: 'shake', value: .04 },
  ]; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Rad, ca = Math.cos(a), sa = Math.sin(a), D = p.distance, landAt = D - LandShort;
    const bow = Bow * Math.abs(sa);
    const place = (base, along, across, h = 0) => ({ x: base.x + along * ca - across * sa + h * bow, z: base.z + along * sa + across * ca + h * Lift });
    const cast = (base, along, across, h = 0) => ({ x: base.x + along * ca - across * sa + sun.x * h, z: base.z + along * sa + across * ca + sun.z * h });
    const feet = place(o, -D / 2, 0);                                // scene centred on the cast; "along" is from the start cell

    // The pawn: on the ground, a ballistic arc at constant ground speed, on the ground one cell short of the target.
    const pawnAt = (time) => { const u = clamp((time - t.launch) / p.flight); return { along: landAt * u, h: p.peak * 4 * u * (1 - u) }; };
    // The hands, once they are above the head: they come forward during the whip and drop to carry height at the end.
    const handsAt = (time) => {
      const q = pawnAt(time), forward = lerp(-.3, 0, smooth((time - t.peak) / WhipTime));
      return { along: q.along + forward, h: q.h + lerp(HandsHigh, CarryHeight, smooth((time - t.pinEnd) / Retract)) };
    };
    const planted = { along: Foot, h: 0 }, target = { along: D, h: 0 };
    const mix = (from, to, k) => ({ along: lerp(from.along, to.along, k), h: lerp(from.h, to.h, k) });
    // Angles are in the along-height plane, degrees, 0 = along the aim, 90 = straight up. The whip goes
    // from pointing down-back at the planted tip, over the top, to RaisedAngle, so the angle only decreases.
    const peakHands = handsAt(t.peak), whipFrom = Math.atan2(-peakHands.h, Foot - peakHands.along) / Rad;
    const whipFromLength = Math.hypot(Foot - peakHands.along, peakHands.h), raised = RaisedAngle - 360;
    // The pole's two ends for any time. A is the tip that is planted and later strikes; B is the hand end.
    const poleAt = (time) => {
      const q = pawnAt(time);
      const carryA = { along: q.along + RestTip, h: q.h + CarryHeight }, carryB = { along: q.along + RestBack, h: q.h + CarryHeight };
      if (time < t.launch) { const k = smooth(time / p.windup); return { A: mix(carryA, planted, k), B: mix(carryB, { along: q.along + TopBack, h: q.h + TopHeight }, k) }; }
      if (time < t.peak) return { A: planted, B: { along: q.along + TopBack, h: q.h + TopHeight } };
      const H = handsAt(time), toTarget = Math.atan2(-H.h, D - H.along) / Rad - 360, targetLength = Math.hypot(D - H.along, H.h);
      let angle, length;
      if (time < t.whipEnd) { const k = smooth((time - t.peak) / (t.whipEnd - t.peak)); angle = lerp(whipFrom, raised, k); length = lerp(whipFromLength, WhipLong, k); }
      else if (time < t.strikeStart) { angle = raised; length = WhipLong; }
      else if (time < t.strikeHit) { const k = Math.pow((time - t.strikeStart) / StrikeTime, 2); angle = lerp(raised, toTarget, k); length = lerp(WhipLong, targetLength, k); }
      else { angle = toTarget; length = targetLength; }
      const ux = Math.cos(angle * Rad), uh = Math.sin(angle * Rad);
      const A = { along: H.along + ux * length, h: Math.max(0, H.h + uh * length) }, B = { along: H.along - ux * Butt, h: H.h - uh * Butt };
      if (time < t.pinEnd) return { A, B, H, swung: time < t.strikeHit };
      const k = smooth((time - t.pinEnd) / Retract);
      return { A: mix(target, carryA, k), B: mix(B, carryB, k) };
    };
    const pawn = pawnAt(s), airborne = s > t.launch && s < t.land, { A, B } = poleAt(s);
    const struck = s - t.strikeHit, hasEnemy = p.scenario !== 'empty cell';

    // Floor markers at the rule's true places: range around the start, the stagger radius on the target, the landing cell.
    const markers = 1 - smooth((s - t.strikeHit) / .4), targetSpot = place(feet, D, 0);
    circle(feet, Range, .22 * markers);
    circle(targetSpot, StaggerRadius, .6 * markers, Floor, Pale);
    circle(place(feet, landAt, 0), .45, .45 * markers, Floor, Pale);

    // Cracks in the ground from the strike, out to the stagger radius. They stay.
    if (struck >= 0) for (let i = 0; i < 9; i++) {
      const turn = i / 9 * Math.PI * 2 + rand(i + 300) * .5, reach = StaggerRadius * (.55 + rand(i + 310) * .45) * clamp(struck / .08);
      const pts = [0, .33, .66, 1].map(k => {
        const bend = (rand(i * 7 + k * 10 + 320) - .5) * .35 * k;
        return { x: targetSpot.x + Math.cos(turn + bend) * reach * k, z: targetSpot.z + Math.sin(turn + bend) * reach * k };
      });
      pts.unshift({ x: targetSpot.x - Math.cos(turn) * .05, z: targetSpot.z - Math.sin(turn) * .05 });
      trail(`power pole strike crack ${i}`, pts, .075, Crack.withAlpha(.65), Floor + .004);
    }

    // Wall stand-in: three cells across the path at the halfway point of the flight. North first.
    if (p.scenario === 'enemy behind a wall') {
      const cells = [-1, 0, 1].map(across => place(feet, landAt / 2, across)).sort((m, n) => n.z - m.z);
      cells.forEach((w, i) => {
        sprite({ x: w.x + sun.x * .5, z: w.z + sun.z * .5 }, 1.5, 1.2, Body.withAlpha(strength), soft, shadowLayer);
        band(`power pole strike wall front ${i}`, [{ x: w.x - .5, z: w.z - .5 }, { x: w.x + .5, z: w.z - .5 }],
          [{ x: w.x - .5, z: w.z - .5 + Lift }, { x: w.x + .5, z: w.z - .5 + Lift }], Stone, pawnLayer + .01 + i * .004);
        band(`power pole strike wall top ${i}`, [{ x: w.x - .5, z: w.z - .5 + Lift }, { x: w.x + .5, z: w.z - .5 + Lift }],
          [{ x: w.x - .5, z: w.z + .5 + Lift }, { x: w.x + .5, z: w.z + .5 + Lift }], StoneTop, pawnLayer + .012 + i * .004);
      });
    }

    const figure = (pos, colour, layer = pawnLayer, squash = 1) => {
      draw(disc, pos.x, layer, pos.z + .18 * squash, .22 / Math.sqrt(squash), .32 * squash, 0, colour);
      draw(disc, pos.x, layer + .002, pos.z + .18 * squash + .40 * squash, .16, .17, 0, Skin);
    };
    const dots = (pos, count, colour, size, fade, seed) => { for (let k = 0; k < count; k++) {
      const turn = s * 5 + k * Math.PI * 2 / count + seed;
      sprite({ x: pos.x + Math.cos(turn) * .22, z: pos.z + .86 + Math.sin(turn) * .07 }, size, size, colour.withAlpha(.95 * fade), soft, Y + .03);
    } };
    if (p.actors) {
      // The struck pawn is pressed down for a moment, then stands stunned. Bystanders inside the radius are staggered.
      if (hasEnemy) {
        const squash = struck < 0 ? 1 : lerp(.6, 1, smooth(struck / .35));
        sprite({ x: targetSpot.x + sun.x * .45, z: targetSpot.z + sun.z * .45 }, .85, .4, Body.withAlpha(strength), soft, shadowLayer);
        figure(targetSpot, new Color(.55, .38, .27), pawnLayer, squash);
        if (struck >= 0) dots(targetSpot, 5, Stun, .11, 1 - smooth((struck - StunShown) / .3), 0);
      }
      Bystanders.forEach(([past, across], i) => {
        const base = place(feet, D + past, across), near = Math.hypot(past, across) <= StaggerRadius, age = near ? struck : -1;
        const away = Math.atan2(base.z - targetSpot.z, base.x - targetSpot.x), moved = age < 0 ? 0 : .2 * (1 - Math.pow(1 - clamp(age / .2), 3));
        const pos = { x: base.x + Math.cos(away) * moved, z: base.z + Math.sin(away) * moved };
        sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, .85, .4, Body.withAlpha(strength), soft, shadowLayer);
        figure(pos, new Color(.50, .42, .33));
        if (age >= 0) dots(pos, 3, Pale, .08, 1 - smooth((age - 1.2) / .3), i + 2);
      });

      // The caster. In the air it draws above walls; its shadow stays on the ground and drifts along the sun.
      const pos = place(feet, pawn.along, 0, pawn.h), shade = cast(feet, pawn.along, 0, pawn.h), spread = 1 + pawn.h * .25;
      sprite({ x: shade.x + sun.x * .45, z: shade.z + sun.z * .45 }, .85 * spread, .4 * spread, Body.withAlpha(strength / spread), soft, shadowLayer);
      figure(pos, new Color(.93, .50, .13), airborne ? Y + .02 : pawnLayer);
    }

    // The swept fan behind the pole's tip during the whip and the strike: three nested slices, newest brightest.
    const fanFrom = s < t.strikeStart ? t.peak : t.strikeStart, fanTo = s < t.strikeStart ? t.whipEnd : t.strikeHit;
    if (s >= t.peak && s < fanTo + .1 && !(s >= t.whipEnd + .1 && s < t.strikeStart)) for (const [k, span] of [.12, .07, .035].entries()) {
      const newest = Math.min(s, fanTo), oldest = Math.max(fanFrom, s - span), fade = s < fanTo ? 1 : 1 - (s - fanTo) / .1;
      if (newest <= oldest) continue;
      const inner = [], outer = [];
      for (let i = 0; i <= 10; i++) {
        const pole = poleAt(lerp(oldest, newest, i / 10)), mid = mix(pole.H, pole.A, .25);
        inner.push(place(feet, mid.along, 0, mid.h)); outer.push(place(feet, pole.A.along, 0, pole.A.h));
      }
      band(`power pole strike fan ${k}`, inner, outer, Pale.withAlpha(.16 * fade), Y - .01);
    }

    // The pole: a strip between the two ends, width across its own screen direction, lit on its north side.
    const a0 = place(feet, A.along, 0, A.h), b0 = place(feet, B.along, 0, B.h);
    const sx = b0.x - a0.x, sz = b0.z - a0.z, screenLength = Math.hypot(sx, sz) || 1;
    let nx = -sz / screenLength, nz = sx / screenLength;
    if (nz < 0 || (nz === 0 && nx > 0)) { nx = -nx; nz = -nz; }
    const length = Math.hypot(B.along - A.along, B.h - A.h) || 1, cap = Math.min(.45, FerruleLength / length);
    const point = (k, side) => ({ x: a0.x + sx * k + nx * side, z: a0.z + sz * k + nz * side });
    const strip = (key, from, to, lo, hi, colour, layer) => band(key, [point(from, lo), point(to, lo)], [point(from, hi), point(to, hi)], colour, layer);
    const w = p.width, grow = .02 / screenLength;
    {
      const sa0 = cast(feet, A.along, 0, A.h), sb0 = cast(feet, B.along, 0, B.h);
      const dx = sb0.x - sa0.x, dz = sb0.z - sa0.z, l = Math.hypot(dx, dz) || 1, mx = -dz / l * w / 2, mz = dx / l * w / 2;
      band('power pole strike shadow', [{ x: sa0.x - mx, z: sa0.z - mz }, { x: sb0.x - mx, z: sb0.z - mz }],
        [{ x: sa0.x + mx, z: sa0.z + mz }, { x: sb0.x + mx, z: sb0.z + mz }], Body.withAlpha(strength), shadowLayer);
    }
    strip('power pole strike outline', -grow, 1 + grow, -w / 2 - .02, w / 2 + .02, RedDark, Y + .04);
    strip('power pole strike body', 0, 1, -w / 2, w / 2, Red, Y + .042);
    strip('power pole strike lit', 0, 1, w * .12, w * .40, RedLit, Y + .044);
    strip('power pole strike ferrule a', 0, cap, -w / 2, w / 2, Ferrule, Y + .046);
    strip('power pole strike ferrule b', 1 - cap, 1, -w / 2, w / 2, Ferrule, Y + .046);
    if (p.actors) for (const grip of [.18, .5]) {
      const k = 1 - Math.min(.9, grip / length), hand = point(k, 0);
      draw(disc, hand.x, Y + .05, hand.z, .075, .075, 0, Skin);
    }

    // Dust: a kick at the planted tip, a stream while the pole pushes, a small puff where the pawn lands.
    const burst = (key, centre, age, count, reachOut, alpha, size = 1) => {
      if (age < 0 || age > .7) return;
      for (let i = 0; i < count; i++) {
        const life = .4 + rand(i + key) * .3, u = age / life;
        if (u > 1) continue;
        const turn = i * 2.399 + key, far = reachOut * (.25 + u * (.6 + rand(i + key + 9) * .6));
        sprite({ x: centre.x + Math.cos(turn) * far, z: centre.z + Math.sin(turn) * far * .7 + Math.sin(u * Math.PI) * .2 },
          (.3 + u * .5) * size, (.24 + u * .4) * size, Dust.withAlpha(Math.sin(u * Math.PI) * alpha), puff, Y - .02);
      }
    };
    const footSpot = place(feet, Foot, 0);
    burst(100, footSpot, s - t.launch, 9, .9, .7);
    if (s > t.launch && s < t.peak) for (let i = 0; i < 4; i++) {
      const u = ((s - t.launch) * 3 + i / 4) % 1;
      sprite({ x: footSpot.x + (rand(i + 30) - .5) * .5, z: footSpot.z + u * .3 }, .2 + u * .3, .16 + u * .24, Dust.withAlpha(Math.sin(u * Math.PI) * .45), puff, Y - .02);
    }
    burst(200, place(feet, landAt, 0), s - t.land, 7, .7, .55);

    // The strike: flash, a ring out to the stagger radius, a big dust burst.
    if (struck >= 0 && struck < .7) {
      const flash = Math.max(0, 1 - struck / .16);
      sprite({ x: targetSpot.x, z: targetSpot.z + .15 }, 3.4, 2.4, Pale.withAlpha(flash * .7), glow, Y + .06);
      sprite({ x: targetSpot.x, z: targetSpot.z + .15 }, 1.5, 1.1, Pale.withAlpha(flash), glow, Y + .062);
      circle(targetSpot, StaggerRadius * clamp(.2 + struck / .18), (1 - clamp(struck / .5)) * .8, Floor + .006, Pale);
      burst(400, targetSpot, struck, 16, StaggerRadius, .85, 1.4);
    }
  },
};
