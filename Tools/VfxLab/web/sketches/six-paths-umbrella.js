// Obsidian Umbrella — weapon form proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed, none of it agreed). A weapon form made of two orbs; the other four
// stay free for techniques. It has three states:
//   closed    held like a short spear: 14 Blunt thrust every 2.0 s.
//   straight  a toggle, no cooldown, still two orbs. The umbrella opens toward the pawn's facing
//             and stops shots from the front arc only (about 120 degrees) for the sage and
//             whoever stands directly behind. The sage cannot attack while holding it. It follows
//             the pawn's own facing, so the player steers it by giving the pawn a target. It
//             absorbs about 150 damage in total, then collapses.
//   up        the active, Canopy. The four free orbs join, the umbrella opens overhead into six
//             panels, one per orb, and a thin veil hangs from its rim to the floor. For 12 s (the
//             sketch shows less) it follows the sage and stops incoming projectiles for everyone
//             inside its 1.6-cell radius; shots going out pass. The veil is what stops a shot
//             from the side; the panel above that stretch of veil pays for it. Each panel absorbs
//             about 100 damage, then breaks and leaves a gap. All six orbs in use; 45 s cooldown.
//
// Drawing: one canopy routine serves both open states. It places an apex, a middle ring and a
// rim in 3D around an axis (up, or along the facing), projects height north by Lift, and paints
// the six panels back to front. Up mode looks the same for every facing. Straight mode does
// not: facing south shows the outside of the dome over the pawn's body, facing north shows the
// ribbed inside behind the pawn, and east or west show a side profile. Panels and veil are
// see-through so pawns stay visible. Sage, ally and shooters are stand-ins.
import { AltitudeLayer, Color, Mathf, Meshes } from '../js/engine.js';
import { draw, mesh, Slate } from './lib/six-paths-solid.js';
import { P, Body, Rim, Y, Floor, orb, sprite, trail, band, circle, glow, soft, rand, at } from './lib/six-paths-impact.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const disc = Meshes.disc(32, 'umbrella disc');
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const pale = new Color(.88, .79, 1), amber = new Color(1, .57, .19);
const Facing = { East: [1, 0], West: [-1, 0], North: [0, 1], South: [0, -1] };
// Decided looks, kept out of the panel.
const Dome = .75, Mid = .62, Scallop = .06, ShotGap = .55, ShotFlight = .3, ShotHeight = .9, WalkSpeed = .6;
const GuardRadius = .85, GuardReach = .95, GuardDepth = .55, GuardHeight = 1.0, GuardArc = 120;

function times(p) {
  const thrust = p.morph, open = thrust + (p.thrust ? .8 : 0), up = open + p.open;
  const close = up + p.hold, closed = close + p.close;
  return { thrust, open, up, close, closed, end: closed + .6 };
}

export default {
  kit: 'Six Paths', label: 'Obsidian Umbrella (sketch)',
  params: {
    mode: { label: 'Open state', value: 'up (canopy)', options: ['up (canopy)', 'straight (front guard)'], group: 'Showcase' },
    facing: { label: 'Pawn facing', value: 'East', options: ['East', 'West', 'North', 'South'], group: 'Showcase' },
    actors: { label: 'Show ally and shooters', value: true, group: 'Showcase' },
    thrust: { label: 'Show one normal thrust first', value: true, group: 'Showcase' },
    walk: { label: 'Sage walks while it is open', value: true, group: 'Showcase' },
    breakAt: P('Hits that break it (up: one panel)', 3, 1, 8, 1, 'Showcase'),
    morph: P('Two orbs form the umbrella', .5, .25, 1, .05, 'Timing (s)'),
    open: P('Umbrella opens', .45, .2, 1, .05, 'Timing (s)'),
    hold: P('Open time shown (up is 12 s for real)', 5, 2, 12, .5, 'Timing (s)'),
    close: P('Umbrella folds', .4, .2, 1, .05, 'Timing (s)'),
    radius: P('Canopy radius, up mode (cells)', 1.6, 1, 2.5, .05, 'Shape'),
    height: P('Rim height, up mode (cells)', 2.4, 1.5, 3.5, .05, 'Shape'),
    opacity: P('Panel opacity', .68, .3, 1, .02, 'Shape'),
    veil: P('Veil strength, up mode', .5, 0, 1, .05, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p), up = p.mode.startsWith('up'); return [
    { name: 'Form umbrella', t: 0 }, ...(p.thrust ? [{ name: 'Thrust', t: t.thrust }] : []),
    { name: up ? 'Canopy opens' : 'Guard opens', t: t.open }, { name: up ? 'Moving cover' : 'Front guard', t: t.up },
    { name: 'Folds', t: t.close }, { name: 'Held again', t: t.closed },
  ]; },
  events() { return []; },
  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const upMode = p.mode.startsWith('up'), [fx, fz] = Facing[p.facing], heading = Math.atan2(fz, fx);
    const walked = p.walk ? clamp((s - t.up) / p.hold) * p.hold * WalkSpeed : 0;
    const S = { x: o.x + fx * (walked - 1.5), z: o.z + fz * (walked - 1.5) };
    const shooter = { x: o.x + fx * 7, z: o.z + fz * 7 }, rear = { x: S.x - fx * 6.5, z: S.z - fz * 6.5 };
    // Sage frame: f cells toward the facing, c cells across it, h cells up.
    const rel = (f, c, h = 0) => at(S, fx * f - fz * c, fz * f + fx * c, h);

    // Front shots. Hit n breaks one panel (up) or the whole guard (straight); later ones get through.
    const shots = [];
    for (let k = 0; t.up + .3 + k * ShotGap + ShotFlight < t.close; k++) {
      const fired = t.up + .3 + k * ShotGap;
      shots.push({ fired, lands: fired + ShotFlight, blocked: k < p.breakAt });
    }
    const brokenAt = shots.length >= p.breakAt ? shots[p.breakAt - 1].lands : Infinity;
    let openness = smooth((s - t.open) / p.open) * (1 - smooth((s - t.close) / p.close));
    if (!upMode) openness *= 1 - smooth((s - brokenAt - .05) / .15);
    const formed = smooth(s / p.morph);

    const figure = (pos, colour, bob = 0) => {
      sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, .85, .4, Body.withAlpha(strength), soft, shadowLayer);
      draw(disc, pos.x, pawnLayer, pos.z + .18 + bob, .22, .32, 0, colour);
      draw(disc, pos.x, pawnLayer + .002, pos.z + .58 + bob, .16, .17, 0, new Color(.83, .70, .54));
    };
    const stepping = p.walk && s > t.up && s < t.close ? Math.abs(Math.sin(s * 8)) * .04 : 0;
    figure(S, new Color(.39, .58, .65), stepping);
    if (p.actors) {
      figure(upMode ? rel(-.85, -.45) : rel(-.8, 0), new Color(.45, .55, .38), stepping);
      figure(shooter, new Color(.55, .38, .27));
      if (!upMode) figure(rear, new Color(.55, .38, .27));
    }

    const apexOpen = upMode ? at(S, 0, 0, p.height + Dome) : rel(GuardReach, 0, GuardHeight);
    const baseOpen = upMode ? at(S, 0, 0, .7) : rel(.2, 0, .8);
    // The four free orbs hover at the sage's sides, clear of whoever stands behind; only the
    // canopy calls them in.
    const called = upMode ? smooth((s - t.open) / (p.open * .7)) * (1 - smooth((s - t.close - p.close * .3) / (p.close * .7))) : 0;
    for (let k = 0; k < 4; k++) {
      const a = heading + (k < 2 ? 1 : -1) * (100 + (k % 2) * 32) * Mathf.Deg2Rad + Math.sin(s * 1.3 + k) * .06;
      const home = at(S, Math.cos(a) * 1.0, Math.sin(a) * 1.0, .95 + .08 * Math.sin(s * 2 + k * 1.7));
      const q = { x: lerp(home.x, apexOpen.x, called), z: lerp(home.z, apexOpen.z, called) };
      orb(q, .2 * (1 - called * .95));
      if (called > 0 && called < 1) trail('umbrella orb in ' + k, [home, { x: (home.x + q.x) / 2, z: (home.z + q.z) / 2 + .15 }, q], .06, Rim.withAlpha(.4));
    }
    if (formed < 1) for (const side of [-1, 1]) {
      const r = (1 - formed) * .7, a = s * 9 + side * 1.57, c = rel(.2, 0, .8);
      orb({ x: c.x + Math.cos(a) * r, z: c.z + Math.sin(a) * r * .5 }, .2 * (1 - formed * .8));
    }

    // Closed umbrella in the hand: a tapered spindle. It lunges along the facing for the thrust
    // and swings to the open state's axis as it opens.
    const lunge = p.thrust ? Math.sin(clamp((s - t.thrust - .25) / .3) * Math.PI) : 0;
    const level = p.thrust ? smooth((s - t.thrust) / .25) * (1 - smooth((s - t.thrust - .55) / .25)) : 0;
    const behindPawn = !upMode && p.facing === 'North';
    if (formed > .05 && openness < 1) {
      const hand = rel(.2 + lunge * .45, 0, lerp(.55, .7, level)), restTip = rel(.55, 0, 1.6), flat = rel(1.5 + lunge * .45, 0, .72);
      let tip = { x: lerp(restTip.x, flat.x, level), z: lerp(restTip.z, flat.z, level) };
      tip = { x: lerp(tip.x, apexOpen.x, openness), z: lerp(tip.z, apexOpen.z, openness) };
      const base = { x: lerp(hand.x, baseOpen.x, openness), z: lerp(hand.z, baseOpen.z, openness) };
      const dx = tip.x - base.x, dz = tip.z - base.z, len = Math.hypot(dx, dz) || 1, nx = -dz / len, nz = dx / len;
      const L = [], R = [], EL = [], ER = [], C = [];
      for (let j = 0; j <= 12; j++) {
        const u = j / 12, w = (.02 + .085 * Math.sin(Math.min(1, u * 1.25) * Math.PI) ** .7) * formed * (1 - openness * .7);
        const x = base.x + dx * u * formed, z = base.z + dz * u * formed;
        L.push({ x: x - nx * w, z: z - nz * w }); R.push({ x: x + nx * w, z: z + nz * w }); C.push({ x, z });
        EL.push({ x: x - nx * (w + .02), z: z - nz * (w + .02) }); ER.push({ x: x + nx * (w + .02), z: z + nz * (w + .02) });
      }
      const layer = p.facing === 'North' && openness < .05 ? pawnLayer - .02 : Y + .05;
      band('umbrella closed edge', EL, ER, Rim.withAlpha(.9), layer);
      band('umbrella closed dark', L, C, Body, layer + .001);
      band('umbrella closed lit', C, R, Color.Lerp(Body, Slate, .4), layer + .002);
      sprite(tip, .16, .16, pale.withAlpha(.7 * formed), glow, layer + .003);
      if (lunge > .2) trail('umbrella thrust streak', [rel(.4, 0, .72), rel(1.3, 0, .74), tip], .1, pale.withAlpha(lunge * .6), layer - .001);
    }

    if (openness > .01) {
      // Canopy in 3D: an axis A, and U, V spanning the rim's plane. U points at the facing in up
      // mode so that panel 0 is the one the front shooter hits.
      const R = (upMode ? p.radius : GuardRadius) * openness, fold = 1 - openness;
      const A = upMode ? [0, 1, 0] : [fx, 0, fz], U = upMode ? [fx, 0, fz] : [-fz, 0, fx], V = upMode ? [-fz, 0, fx] : [0, 1, 0];
      const centre = upMode ? [0, p.height, 0] : [fx * (GuardReach - GuardDepth), GuardHeight, fz * (GuardReach - GuardDepth)];
      const depth = upMode ? Dome : GuardDepth;
      const point = (a, r, along) => {
        const c = Math.cos(a) * r, n = Math.sin(a) * r;
        return [centre[0] + U[0] * c + V[0] * n + A[0] * along, Math.max(.02, centre[1] + U[1] * c + V[1] * n + A[1] * along), centre[2] + U[2] * c + V[2] * n + A[2] * along];
      };
      const show = (q) => at(S, q[0], q[2], q[1]);
      const apex3 = point(0, 0, depth), apex = show(apex3);
      const rim3 = (a, k) => point(a, R * (1 - Scallop * Math.cos(3 * (a - k * Math.PI / 3)) ** 2), fold * depth * .6);
      const mid3 = (a) => point(a, R * Mid, depth * .78);

      if (upMode) {
        circle(S, p.radius, .4 * openness);
        sprite({ x: S.x + sun.x * p.height, z: S.z + sun.z * p.height }, R * 2.3, R * 2.3, Body.withAlpha(strength * .55 * openness), soft, shadowLayer);
      } else {
        // The guarded arc drawn on the floor, at its true width.
        const arc = Array.from({ length: 17 }, (_, j) => { const a = heading + (j / 16 - .5) * GuardArc * Mathf.Deg2Rad; return at(S, Math.cos(a) * 1.25, Math.sin(a) * 1.25); });
        trail('umbrella guard arc', arc, .07, Rim.withAlpha(.5 * openness), Floor + .01);
        const g = rel(.6, 0); sprite({ x: g.x + sun.x * .8, z: g.z + sun.z * .8 }, 1.3 * openness, .7 * openness, Body.withAlpha(strength * .5), soft, shadowLayer);
      }
      const bottom = behindPawn ? pawnLayer - .03 : Y + .07;
      trail('umbrella shaft', [baseOpen, { x: (baseOpen.x + apex.x) / 2, z: (baseOpen.z + apex.z) / 2 }, apex], .06, Body, bottom - .002);

      // Panels, painted back to front by how near their middle is to the camera.
      const panels = [];
      for (let k = 0; k < 6; k++) {
        const c = k * Math.PI / 3, m = point(c, R * .7, depth * .6);
        const radial = [U[0] * Math.cos(c) + V[0] * Math.sin(c), U[1] * Math.cos(c) + V[1] * Math.sin(c), U[2] * Math.cos(c) + V[2] * Math.sin(c)];
        const n = [radial[0] * .8 + A[0] * .6, radial[1] * .8 + A[1] * .6, radial[2] * .8 + A[2] * .6];
        panels.push({ k, c, near: m[1] - .6 * m[2], outside: n[1] - .6 * n[2] > 0, lit: Math.max(0, n[1] * .5 - n[2] * .8 - n[0] * .35) });
      }
      panels.sort((a, b) => a.near - b.near);
      panels.forEach(({ k, c, outside, lit }, rank) => {
        if (upMode && k === 0 && s >= brokenAt) return;
        const layer = bottom + rank * .002, rim = [], mid = [], top = [], hemGround = [];
        for (let j = 0; j <= 8; j++) {
          const a = c + (j / 8 - .5) * Math.PI / 3, r3 = rim3(a, k);
          rim.push(show(r3)); mid.push(show(mid3(a))); top.push(apex); hemGround.push(at(S, r3[0], r3[2]));
        }
        if (upMode && p.veil > 0) {
          const drop = smooth((s - t.open - p.open * .5) / (p.open * .5)) * openness, south = hemGround[4].z < S.z + .1;
          const hem = rim.map((q, j) => ({ x: lerp(q.x, hemGround[j].x, drop), z: lerp(q.z, hemGround[j].z, drop) }));
          band('umbrella veil ' + k, rim, hem, Rim.withAlpha(.2 * p.veil), south ? Y + .065 : pawnLayer - .01);
          for (const j of [1, 4, 7]) trail(`umbrella thread ${k} ${j}`, [rim[j], { x: (rim[j].x + hem[j].x) / 2, z: (rim[j].z + hem[j].z) / 2 }, hem[j]],
            .03, Rim.withAlpha(.55 * p.veil), south ? Y + .066 : pawnLayer - .009);
        }
        // The inside of the shell is darker than the outside.
        const shade = outside ? .22 + .4 * lit : .06;
        band('umbrella panel low ' + k, mid, rim, Color.Lerp(Body, Slate, shade).withAlpha(p.opacity), layer);
        band('umbrella panel top ' + k, top, mid, Color.Lerp(Body, Slate, shade + (outside ? .12 : .04)).withAlpha(p.opacity), layer + .0002);
        trail('umbrella hem ' + k, rim, .05, Rim.withAlpha(.9), layer + .0004);
        // A blocked shot lights what paid for it: one panel in up mode, the whole guard in straight.
        if (upMode && k !== 0) return;
        for (const shot of shots) {
          const age = s - shot.lands;
          if (!shot.blocked || age < 0 || age > .3) continue;
          const f = (1 - age / .3) ** 2 * (upMode ? .55 : .4);
          band(`umbrella flash low ${k}`, mid, rim, pale.withAlpha(f), layer + .0006);
          band(`umbrella flash top ${k}`, top, mid, pale.withAlpha(f), layer + .0006);
        }
      });
      // Ribs stay even where a panel is gone, like a broken umbrella.
      for (let k = 0; k < 6; k++) {
        const a = (k + .5) * Math.PI / 3;
        trail('umbrella rib ' + k, [apex, show(mid3(a)), show(rim3(a, k))], .04, Rim.withAlpha(.85), bottom + .013);
      }
      sprite(apex, .22, .22, pale.withAlpha(.8 * openness), glow, bottom + .014);
      // Where a blocked shot struck: on the veil (with a ripple up to the rim) or on the dome.
      for (const shot of shots) {
        const age = s - shot.lands;
        if (!shot.blocked || age < 0 || age > .3) continue;
        const f = (1 - age / .3) ** 2, strike = upMode ? rel(p.radius, 0, ShotHeight) : rel(GuardReach, 0, GuardHeight);
        sprite(strike, .7, .7, pale.withAlpha(f * .9), glow, Y + .12);
        if (upMode) trail('umbrella veil ripple', [strike, rel(p.radius, 0, ShotHeight + (p.height - ShotHeight) * (1 - f)), show(rim3(0, 0))], .07, pale.withAlpha(f * .8), Y + .11);
      }
    }
    // What broke falls away as shards: one panel, or the whole guard.
    const since = s - brokenAt;
    if (since >= 0 && since < .7) for (let i = 0; i < (upMode ? 8 : 12); i++) {
      const u = since / .7, m = mesh('umbrella shard ' + i), w = .13 * (1 - u * .4), spin = since * (4 + i);
      const q = upMode
        ? at(S, Math.cos(heading + rand(i) - .5) * (p.radius * (.5 + rand(i + 4) * .5) + u * .5), Math.sin(heading + rand(i) - .5) * (p.radius * (.5 + rand(i + 4) * .5) + u * .5), (p.height + Dome * .4) * (1 - u * u))
        : rel(GuardReach - .3 + u * .6 * rand(i + 2), (rand(i) - .5) * 1.6 * (1 + u * .5), (GuardHeight + (rand(i + 7) - .5) * 1.4) * (1 - u * u));
      m.setFlat([Math.cos(spin) * w, Math.sin(spin) * w, Math.cos(spin + 2.2) * w, Math.sin(spin + 2.2) * w, Math.cos(spin + 4.1) * w, Math.sin(spin + 4.1) * w], [0, 1, 2]);
      draw(m, q.x, Y + .13, q.z, 1, 1, 0, Color.Lerp(Body, Slate, .3).withAlpha(1 - u));
    }

    // The shots: stopped in front, or through a gap; and one from behind that the guard never covers.
    if (p.actors) {
      const fly = (start, end, fired, hits) => {
        const u = (s - fired) / ShotFlight;
        if (u >= 0 && u < 1) {
          const q = { x: lerp(start.x, end.x, u), z: lerp(start.z, end.z, u) }, tx = (start.x - end.x), tz = (start.z - end.z), len = Math.hypot(tx, tz) || 1;
          trail('umbrella shot ' + fired.toFixed(2), [{ x: q.x + tx / len * .7, z: q.z + tz / len * .7 }, { x: q.x + tx / len * .3, z: q.z + tz / len * .3 }, q], .09, amber.withAlpha(.7), Y + .2);
          sprite(q, .22, .18, amber, glow, Y + .201);
        }
        const hurt = s - fired - ShotFlight;
        if (hits && hurt >= 0 && hurt < .25) sprite(end, .8, .8, amber.withAlpha((1 - hurt / .25) * .9), glow, Y + .2);
      };
      const muzzle = at(shooter, -fx * .4, -fz * .4, ShotHeight), body = rel(.1, 0, .6);
      for (const shot of shots) fly(muzzle, shot.blocked ? (upMode ? rel(p.radius, 0, ShotHeight) : rel(GuardReach, 0, GuardHeight)) : body, shot.fired, !shot.blocked);
      if (!upMode && t.up + 1.2 + ShotFlight < t.close) fly(at(rear, fx * .4, fz * .4, ShotHeight), rel(-.1, 0, .6), t.up + 1.2, true);
    }
  },
};
