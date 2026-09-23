// Unlimited Void — technique proposal for the Gojo kit, not the game. Nothing in Source/RimArt draws
// this yet. The kit's ultimate: the domain expansion.
//
// What it is for (proposed, none of it agreed; every number is a placeholder and will be an XML field).
//   No target: a domain centred on the caster. Warm-up 0.6 s (the hand sign; the blindfold comes off).
//   A dome of radius 9 cells opens in 0.35 s and holds 5 s. While it holds, hostile pawns cannot walk
//   in or out through the rim and projectiles from outside do not enter. Line of sight is not needed:
//   inside is inside, walls included. Every non-allied pawn (humans and animals) inside at the open is
//   frozen for the whole hold; the caster is free to move and act. When the dome closes (0.35 s) each
//   victim gets "Void Overload": Consciousness -70 % for 60 s (below 30 % is downed), then
//   "Void-scarred": Consciousness -15 %, Sight -20 % for 2 days. No damage. Allies untouched.
//   Cooldown 2 days.
//   Role: a non-lethal mass lockdown. Everything hostile in 9 cells goes down for a minute, alive,
//   which pairs with capturing prisoners. Solar Flare (Goku) is a 3.5 s line-of-sight stun; Judgement
//   Cut End (Vergil) is a line-of-sight stun that kills. This needs no line of sight, has a barrier
//   and deals no damage.
//
// Order, with the default timings (the look follows the anime and the Cursed Clash game):
//   0.00  7 raiders walk in on the caster; an ally stands inside the radius; 1 raider is outside and
//         keeps walking in
//   0.30  warm-up 0.6 s: the hands meet at the chest and the index fingers stand up, a blue-white
//         glint at the hands with 8 short violet streaks running in to it; the blindfold fades from
//         the face and drops beside the caster, two blue eyes light; a cyan-violet rim light comes up
//         round the caster; a soft dark disc opens under the caster
//   0.90  open: a WHITE dome of light spreads from the caster to the true radius in 0.35 s behind a
//         bright ring front, with a ragged white splatter burst behind the caster; camera shake 0.06.
//         Every raider inside stops where it stands, goes pale with a white edge.
//   1.25  the white inside falls to black over 0.6 s and becomes the void: 60 stars, 3 nebula patches,
//         a blue horizon ring, and 84 dashes of white, pink and violet light streaming out from the
//         caster to the rim on straight rays (the speed lines). The dome is the true-radius floor ring,
//         3 faint latitude rings (level circles lifted 0.6 per cell of height) and a see-through skin:
//         the hemisphere's outline under the lift (1.17 R north, R south), faint blue with a bright edge.
//   1.25  the black hole rises from the caster's head to 3 cells up (1.8 cells north on screen) in
//         0.4 s: a black disc of radius 2 with a thin gold-white accretion ring, a green-cyan inner
//         edge, a wide halo, and 5 nebula wisps streaming off its east side
//   1.65  hold (to 6.25): every 0.55 s a level ring leaves the black hole at its height and runs out
//         to the rim, fading. From 35 % to 75 % of the hold the rays curl into a spiral (0 to 30
//         degrees of turn per cell) and stay curled. Each frozen raider has a white glow at the head
//         and 5 streaks flickering up from it. The ally stands in its own colour. The outside raider
//         walks to the rim and stops there; each push ripples a violet ring on the dome at the contact.
//   6.25  close 0.35 s: the black hole collapses to a point with a white flash, the dome shrinks back
//         into the caster and the void floor goes with the rim. Each raider falls over 0.3 s and lies
//         downed with a small violet ring over the head (the hediff). The ally still stands. The
//         outside raider walks in.
//   6.60  to 8.10 tail: the results stay on screen.
//
// Drawing: level circles, one fixed-orientation polygon (the dome skin, lib/gojo.js domeSkin, rebuilt
// only when the radius changes), and lines about one point, and no aim, so no per-facing method. The
// black hole and the ripples are level circles at its height, so they only shift north. The rays are
// strip meshes rebuilt every frame (84 of them; a port can keep them as one mesh moved by uv offset
// if that is cheaper). The caster (white hair, dark uniform, blindfold), the raiders and the ally are
// stand-ins. Light, not solid: the dome is additive layers with no opaque mesh. Textures: SoftDisc,
// Puff and white only.
import { Color, Mathf, Meshes, MeshPool } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, Lift, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import { caster, blackHole, splatter, rays, domeSkin, overload, pawn, ringAt, glint, streak, whiteGlow, EnemyColour, Ally, White, Ice, Blue, Void, Violet, EyeBlue, Blindfold, pawnLayer, smooth, clamp } from './lib/gojo.js';

const disc = Meshes.disc(48, 'unlimited void disc');
// Decided values. The panel keeps only what is still being tuned.
const Lead = .3, Tail = 1.5, Stars = 60, Nebulae = 3, Streaks = 5, Rays = 84, Fall = .3, Shake = .06, EyeRise = .4, RippleLife = 1.3, WhiteFade = .6;
const SwirlFrom = .35, SwirlTo = .75;   // shares of the hold over which the rays curl
const WalkIn = 1.2, OutsideBy = 2.5, RimStop = .45, PushEvery = .6, Gather = 8;
const Latitudes = [.3, .6, .85];
// Raiders as [direction from the caster (degrees), distance at the open (cells)].
const Raiders = [[20, 2.2], [75, 3.6], [-40, 2.8], [130, 5.2], [-95, 6.4], [48, 7.6], [-150, 4.4]];
const AllyAt = [200, 3.0], OutsideDeg = 158;
const PaleBlue = new Color(.6, .78, 1);

function times(p) {
  const cast = Lead, open = cast + p.warm, full = open + p.open, close = full + p.hold, closed = close + p.close;
  return { cast, open, full, close, closed, end: closed + Tail };
}

export default {
  kit: 'Gojo', label: 'Unlimited Void (sketch)',
  params: {
    ally: { label: 'An ally stands inside the radius', value: true, group: 'Showcase' },
    walker: { label: 'A raider outside walks into the barrier', value: true, group: 'Showcase' },
    radius: P('Radius (cells)', 9, 5, 14, .5, 'Shape'),
    eyeHeight: P('Black hole height (cells)', 3, 1, 5, .1, 'Shape'),
    eye: P('Black hole radius (cells)', 2, .6, 3.5, .1, 'Shape'),
    swirl: P('Ray spiral (degrees per cell)', 30, 0, 60, 1, 'Shape'),
    warm: P('Warm-up (hand sign)', .6, .2, 1.5, .05, 'Timing (s)'),
    open: P('Dome opens', .35, .1, 1, .05, 'Timing (s)'),
    hold: P('Dome holds', 5, 1, 8, .25, 'Timing (s)'),
    close: P('Dome closes', .35, .1, 1, .05, 'Timing (s)'),
    ripple: P('Ripple every', .55, .2, 1.5, .05, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Raiders close in', t: 0 }, { name: 'Hand sign', t: t.cast }, { name: 'Domain opens', t: t.open },
      { name: 'Hold', t: t.full }, { name: 'Domain closes', t: t.close }, { name: 'Downed', t: t.closed }];
  },
  events(p) {
    const t = times(p);
    return [{ t: t.open, type: 'shake', value: Shake }, { t: t.open, type: 'sound', def: 'AG_Gojo_DomainOpen' }, { t: t.close, type: 'sound', def: 'AG_Gojo_DomainClose' }];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const R = p.radius, polar = (deg, d) => ({ x: o.x + Math.cos(deg * Mathf.Deg2Rad) * d, z: o.z + Math.sin(deg * Mathf.Deg2Rad) * d });
    // How far the dome reaches right now: 0, growing, R, shrinking.
    const reach = s < t.open ? 0 : s < t.full ? R * smooth((s - t.open) / p.open) : s < t.close ? R : R * (1 - smooth((s - t.close) / p.close));
    const up = reach > 0, hold = s >= t.full && s < t.close;
    const fell = clamp((s - t.close) / Fall);                      // 0 standing, 1 lying

    // --- who is where ---------------------------------------------------------------------------------------
    const figures = [{ pos: o, caster: true }];
    Raiders.forEach(([deg, d], i) => {
      if (d > R - .6) return;
      const before = Math.max(0, t.open - s) * WalkIn;             // walking in until the dome opens, then frozen
      figures.push({ pos: polar(deg, Math.max(1.2, d + before)), victim: true, i });
    });
    if (p.ally) figures.push({ pos: polar(AllyAt[0], Math.min(AllyAt[1], R - 1)), ally: true, i: 30 });
    // The raider outside: walks in, is held at the rim while the dome is up, then walks on.
    let blockedSince = -1, walkerPos = null;
    if (p.walker) {
      const rim = R + RimStop, start = R + OutsideBy + t.open * WalkIn, raw = q => start - q * WalkIn;
      let d;
      if (s <= t.closed) d = Math.max(rim, raw(s)); else d = Math.max(rim, raw(t.closed)) - (s - t.closed) * WalkIn;
      const hits = (start - rim) / WalkIn;                          // when it first meets the rim
      if (up && s >= hits && s <= t.closed) blockedSince = hits;
      walkerPos = polar(OutsideDeg, Math.max(1.5, d));
      figures.push({ pos: walkerPos, i: 20 });
    }

    // --- the floor: the void, its stars, the horizon --------------------------------------------------------
    if (s >= t.cast && s < t.open) {
      const w = smooth((s - t.cast) / p.warm);
      sprite(o, 1.3 * w, 1.3 * w, Void.withAlpha(.7 * w), soft, Floor + .004);
    }
    if (up) {
      draw(disc, o.x, Floor + .005, o.z, reach, reach, 0, Void.withAlpha(.94));
      // The open is white: the light falls to black over WhiteFade after the dome is full.
      const white = s < t.full ? 1 : 1 - smooth((s - t.full) / WhiteFade);
      if (white > 0) draw(disc, o.x, Floor + .0055, o.z, reach, reach, 0, Ice.withAlpha(.92 * white), whiteGlow);
      const holdShare = clamp((s - t.full) / p.hold), swirl = p.swirl * Mathf.Deg2Rad * smooth((holdShare - SwirlFrom) / (SwirlTo - SwirlFrom));
      rays('unlimited void ray', o, s - t.full, reach, R, swirl, .85 * (1 - white * .7), Rays);
      for (let i = 0; i < Stars; i++) {
        const r = R * Math.sqrt(rand(i + 100)), ang = rand(i + 200) * Math.PI * 2;
        if (r > reach - .15) continue;
        const twinkle = .45 + .55 * Math.abs(Math.sin(s * 2.6 + i * 1.9)), size = .05 + .09 * rand(i + 300);
        sprite({ x: o.x + Math.cos(ang) * r, z: o.z + Math.sin(ang) * r }, size * 2.2, size * 2.2, White.withAlpha(.9 * twinkle), glow, Floor + .011);
      }
      for (let i = 0; i < Nebulae; i++) {
        const r = R * (.25 + .45 * rand(i + 400)), ang = rand(i + 500) * Math.PI * 2, size = R * (.3 + .2 * rand(i + 600)) * clamp(reach / R);
        sprite({ x: o.x + Math.cos(ang) * r, z: o.z + Math.sin(ang) * r }, size, size * .7, (i % 2 ? Violet : Blue).withAlpha(.16), glow, Floor + .01, i * 50);
      }
      ringAt(o, reach * .96, Blue.withAlpha(.35), Floor + .012, true, whiteGlow);
    }

    // --- the dome: ring, latitude rings, skin; the front while it moves ------------------------------------------
    if (up) {
      const moving = s < t.full || s >= t.close, skin = clamp(reach / R);
      ringAt(o, reach, Ice.withAlpha(.85), Y + .02, false, whiteGlow);
      Latitudes.forEach((share, k) => {
        const h = reach * share, r = Math.sqrt(Math.max(0, reach * reach - h * h));
        ringAt({ x: o.x, z: o.z + h * Lift }, r, Blue.withAlpha(.16 + .06 * Math.sin(s * 3 + k)), Y + .03 + k * .001, false, whiteGlow);
      });
      domeSkin('unlimited void dome', o, reach, skin);
      if (moving) {
        const f = s < t.full ? smooth((s - t.open) / p.open) : 1 - smooth((s - t.close) / p.close);
        ringAt(o, reach, White.withAlpha(.9), Y + .1, true, whiteGlow);
        sprite(o, 2 + 2 * f, 2 + 2 * f, Ice.withAlpha(.6 * (1 - f)), glow, Y + .101);
      }
      const burst = clamp((s - t.open) / .12) * (1 - smooth((s - t.open - .25) / .7));
      splatter('unlimited void splatter', { x: o.x, z: o.z + .5 }, Math.min(R, 3 + R * .25), s, burst * burst);
    }

    // --- the barrier stops the walker ----------------------------------------------------------------------------
    if (blockedSince >= 0 && hold) {
      const age = (s - blockedSince) % PushEvery, cp = polar(OutsideDeg, R);
      const fade = 1 - age / PushEvery;
      ringAt(cp, .2 + age * 1.4, Violet.withAlpha(.95 * fade), Y + .045, true, whiteGlow);
      ringAt(cp, .1 + age * .9, Ice.withAlpha(.8 * fade), Y + .0455, false, whiteGlow);
      sprite(cp, 1.4, 1.4, Violet.withAlpha(.6 * fade), glow, Y + .046);
    }

    // --- pawns, north first ----------------------------------------------------------------------------------------
    figures.sort((m, n) => n.pos.z - m.pos.z).forEach(g => {
      if (g.caster) {
        const off = smooth((s - t.cast) / (p.warm * .5)), sign = smooth((s - t.cast) / p.warm) * (1 - smooth((s - t.closed) / .4));
        caster(g.pos, sun, strength, { blindfold: 1 - off, sign, rim: off * (1 - smooth((s - t.closed) / .6)) });
        if (off > 0) draw(MeshPool.plane10, g.pos.x + .38, Floor + .02, g.pos.z - .12, .42, .07, 20, Blindfold.withAlpha(.9 * off));
        return;
      }
      if (g.victim) {
        const hit = s >= t.open, tintAmount = hit ? .45 * (1 - fell * .5) : 0;
        if (fell < 1) pawn(g.pos, EnemyColour, sun, strength, { alpha: 1 - fell, tint: PaleBlue, tintAmount, outline: hit ? .45 * (1 - fell) : 0 });
        if (fell > 0) {
          pawn(g.pos, EnemyColour, sun, strength, { lie: true, alpha: fell, tint: PaleBlue, tintAmount: .25 });
          ringAt({ x: g.pos.x + .42, z: g.pos.z + .14 }, .15, Violet.withAlpha(.75 * fell), Y + .03, false, whiteGlow);
        }
        if (hit && fell < 1) overload(`unlimited void overload ${g.i}`, g.pos, s + g.i, clamp((s - t.open) / .15) * (1 - fell), Streaks);
        return;
      }
      pawn(g.pos, g.ally ? Ally : EnemyColour, sun, strength);
    });

    // --- warm-up: the sign gathers light at the hands -------------------------------------------------------------
    if (s >= t.cast && s < t.open) {
      const w = (s - t.cast) / p.warm, hands = { x: o.x, z: o.z + .44 };
      sprite(hands, .4 + .7 * w, .4 + .7 * w, EyeBlue.withAlpha(.6 * w), glow, Y + .05);
      glint('unlimited void gather glint', hands, .08 + .22 * w, w, Ice);
      for (let i = 0; i < Gather; i++) {
        const v = (w * 1.5 + rand(i)) % 1, ang = (i * 45 + rand(i + 5) * 30) * Mathf.Deg2Rad, r0 = .9 * (1 - v) + .1, r1 = r0 + .25 * (1 - v);
        streak(`unlimited void gather ${i}`, { x: hands.x + Math.cos(ang) * r0, z: hands.z + Math.sin(ang) * r0 }, { x: hands.x + Math.cos(ang) * r1, z: hands.z + Math.sin(ang) * r1 },
          .035, Violet.withAlpha(.9 * Math.sin(v * Math.PI)), whiteGlow, Y + .051, 2);
      }
    }

    // --- the eye and its ripples -----------------------------------------------------------------------------------
    if (s >= t.full && s < t.closed) {
      const u = s < t.close ? smooth((s - t.full) / EyeRise) : 1 - smooth((s - t.close) / p.close);
      const centre = { x: o.x, z: o.z + .6 + u * p.eyeHeight * Lift };
      blackHole('unlimited void hole', centre, p.eye * u, s, u);
      if (s >= t.close) { const f = smooth((s - t.close) / p.close); sprite(centre, 1.5 + 4 * f, 1.5 + 4 * f, White.withAlpha(.9 * (1 - f)), glow, Y + .07); }
      const rippleStart = t.full + EyeRise, top = Math.sqrt(Math.max(0, R * R - p.eyeHeight * p.eyeHeight));
      if (s >= rippleStart && s < t.close) for (let k = 0; ; k++) {
        const age = s - rippleStart - k * p.ripple;
        if (age < 0) break;
        if (age > RippleLife) continue;
        const v = age / RippleLife, r = p.eye + (top - p.eye) * v;
        ringAt(centre, r, Blue.withAlpha(.4 * (1 - v) * (1 - v)), Y + .05 + k * .0002, false, whiteGlow);
      }
    }
  },
};
