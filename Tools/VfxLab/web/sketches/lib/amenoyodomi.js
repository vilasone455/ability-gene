// Amenoyodomi: the held weapon, shared by the Rinnegan sketches. Not a sketch itself, so it is not
// listed in sketches/index.js. The rule it draws is in the header of rinnegan-amenoyodomi.js.
//
// A held weapon is a belt kunai or the Fūma that Amenoyodomi stopped in the air over the cell it was
// thrown at. `weapon()` makes the record; `motion()` says where it is and what it is doing at any
// clip time; `drawWeapon()` draws it. Nothing is kept between frames, so the timeline can be scrubbed.
//
// A weapon's life along its heading (u = cells from where it left the hand):
//   flying   full speed from the hand (24 cells/s kunai, 14.4 the Fūma), Hold cells up
//   easing   the last Ease seconds before the cell: slows from full speed to the hold share
//   held     over its cell and past it at the hold share of its speed (1 % hang, 10 % drift), which
//            can change while it hangs (w.shares); touches nothing
//   flying   after a let-go: full speed on along the same heading, until w.stop
//   falling  after a drop (toggle off, caster down, a wall, 60 s): straight down in DropTime
//   lying / land / hit / range   on the floor, or stuck in a pawn (the sketch draws that one)
// A normal throw (at a pawn, or the 6th kunai) flies at full speed to w.stop and is never held.
//
// Drawing: the weapon hangs Hold cells up, drawn Hold x Lift north of its ground point, with its
// shadow on the ground. The marks under it (two rings, three dots that turn at the hold share, the
// ripples) are level circles on the floor at its true cell; the afterimages and streaks lie along its
// heading. Nothing here needs a per-facing method.
// Port notes: quads (MeshPool.plane10) with the kunai and Fūma textures, SoftDisc and Puff; two ring
// meshes (Meshes.band) scaled per weapon; strip meshes for the streak and the timer arc.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../../js/engine.js';
import { draw, Lift } from './six-paths-solid.js';
import { Y, Floor, at, sprite, band, glow, soft } from './six-paths-impact.js';
import { whiteGlow, kunaiMat, streak } from './flying-thunder-god.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01;
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), projectileLayer = AltitudeLayer.Projectile.AltitudeFor();

// The game's weapon numbers: AG_KunaiProjectile (speed 40 = 24 cells/s, drawSize 0.75, throw kunai
// range 14.9) and AG_FumaProjectile (14.4 cells/s, drawSize 1.4, range 12). The Fūma's spin is the
// stand-in the Amaterasu and Raikō Kusari sketches use (720 degrees/s, clockwise).
export const KunaiSpeed = 24, KunaiSize = .75, KunaiRange = 14.9;
export const FumaSpeed = 14.4, FumaSize = 1.4, FumaRange = 12, FumaSpin = 720;
// The rule (placeholders): hold shares, most held, how long one hangs.
export const Hang = .01, Drift = .1, MaxHeld = 5, HoldLimit = 60;
export const Hold = .55;                     // cells up while held: chest height
export const Ease = .06, DropTime = .25;     // seconds: slowing into the hold, falling when dropped

export const Lavender = new Color(.80, .74, .98), LavenderDeep = new Color(.46, .36, .78), EyeStar = new Color(.90, .84, 1);
const White = new Color(1, 1, 1), ShadowInk = new Color(.03, .03, .05), Dust = new Color(.52, .47, .40);

// Decided looks.
const Stroke = .025, InnerShare = .6, FumaRing = 2.6;       // ring line width; inner ring; Fūma ring vs kunai ring
const CatchLife = .45, CatchReach = 1, CatchFlash = .12;    // the ripple a weapon makes as it stops
const RippleLife = 1, RippleReach = 2.2;                    // the ripple that leaves a hanging weapon now and then
const WakeGap = .3, WakeLife = .8, WakeFrom = .05;          // ripples left behind while it moves at WakeFrom share or more
const PullIn = .08, LetGoFlash = .1, RingFade = .2, DustLife = .35;
const GhostAlpha = [.5, .32, .2, .12, .07], GhostPerSpeed = .08;
const DotTurn = 360;                                        // degrees the dots turn per cell of the flight speed crept

const rings = [1, InnerShare].map((f, i) => Meshes.band(1 - Stroke / (.32 * f), 1, 48, `amenoyodomi ring ${i}`));
const dot = Meshes.disc(16, 'amenoyodomi dot');
// Afterimages are light, not steel: the kunai's shape drawn additive in lavender.
const ghostKunai = MaterialPool.MatFrom('RimArt/Kunai/Kunai', ShaderDatabase.MoteGlow);
const ghostFuma = MaterialPool.MatFrom('RimArt/Fuma/Unfolded', ShaderDatabase.MoteGlow);
export const fumaMat = MaterialPool.MatFrom('RimArt/Fuma/Unfolded', ShaderDatabase.Cutout);
export const fumaGhost = MaterialPool.MatFrom('RimArt/Fuma/Unfolded', ShaderDatabase.Transparent);
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);

// kind 'kunai' or 'fuma'. from: the ground point it leaves (the game launches from the pawn's
// DrawPos); cell: the ground point it was thrown at; thrown: seconds it leaves the hand.
// opts.held false: a normal throw. opts.shares: [[time, share], ...], the hold share from each time
// on (the toggle's state). opts.letGo, opts.drop: seconds, or never. The sketch sets w.stop.
export function weapon(kind, from, cell, thrown, opts = {}) {
  const dx = cell.x - from.x, dz = cell.z - from.z, dist = Math.hypot(dx, dz) || 1e-6;
  const speed = kind === 'fuma' ? FumaSpeed : KunaiSpeed;
  const w = {
    kind, from, cell, thrown, dist, speed, dir: { x: dx / dist, z: dz / dist }, held: opts.held ?? true,
    shares: opts.shares ?? [[0, Hang]], letGo: opts.letGo ?? Infinity, drop: opts.drop ?? Infinity, seed: opts.seed ?? 0, stop: null,
  };
  w.deg = Math.atan2(w.dir.z, w.dir.x) * Mathf.Rad2Deg;
  w.range = kind === 'fuma' ? FumaRange : KunaiRange;
  w.easeDist = Math.min(dist, (speed + speed * shareAt(w, thrown + dist / speed)) / 2 * Ease);
  w.easeStart = thrown + (dist - w.easeDist) / speed;
  w.caught = w.held ? w.easeStart + Ease : Infinity;
  return w;
}

// The hold share at time t.
export function shareAt(w, t) {
  let v = w.shares[0][1];
  for (const [t0, s] of w.shares) if (t >= t0) v = s;
  return v;
}
// Cells crept at the hold share between times a and b.
export function crept(w, a, b) {
  let d = 0;
  w.shares.forEach(([t0, share], i) => {
    const lo = Math.max(a, i ? t0 : -Infinity), hi = Math.min(b, i + 1 < w.shares.length ? w.shares[i + 1][0] : Infinity);
    if (hi > lo) d += (hi - lo) * share * w.speed;
  });
  return d;
}
// The time at which a held weapon has crept d cells past its cell, or Infinity.
export function creptTime(w, d) {
  let left = d;
  for (let i = 0; i < w.shares.length; i++) {
    const lo = Math.max(w.caught, w.shares[i][0]), hi = i + 1 < w.shares.length ? w.shares[i + 1][0] : Infinity;
    if (hi <= lo) continue;
    const rate = w.shares[i][1] * w.speed, span = (hi - lo) * rate;
    if (span >= left) return lo + left / rate;
    left -= span;
  }
  return Infinity;
}
// The ground point u cells along its heading.
export const ground = (w, u) => ({ x: w.from.x + w.dir.x * u, z: w.from.z + w.dir.z * u });
// Where it was let go or dropped from: cells along its heading.
export const heldU = (w, t) => w.dist + crept(w, w.caught, Math.min(t, w.letGo, w.drop));
// Clockwise degrees the Fūma has turned: its spin in step with the distance it has travelled, so it
// turns at the hold share while held and stops when it lies.
export const turnOf = (w, u) => FumaSpin * u / w.speed;

// Where the weapon is at time t and what it is doing: { phase, u (cells along), h (cells up),
// speed (cells/s), since (seconds in this phase) }, or null while it is still in the hand.
export function motion(w, t) {
  const v = w.speed;
  if (t < w.thrown) return null;
  if (!w.held) {
    const stopT = w.stop?.t ?? w.thrown + w.dist / v;
    if (t < stopT) return { phase: 'flying', u: v * (t - w.thrown), h: Hold, speed: v, since: t - w.thrown };
    return { phase: w.stop?.how ?? 'land', u: w.stop?.u ?? w.dist, h: 0, speed: 0, since: t - stopT };
  }
  if (t < w.easeStart) return { phase: 'flying', u: v * (t - w.thrown), h: Hold, speed: v, since: t - w.thrown };
  if (t < w.caught) {
    const tau = t - w.easeStart, sv = v * shareAt(w, w.caught);
    return { phase: 'easing', u: w.dist - w.easeDist + v * tau - (v - sv) * tau * tau / (2 * Ease), h: Hold, speed: v - (v - sv) * tau / Ease, since: tau };
  }
  const u = heldU(w, t);
  if (t < w.letGo && t < w.drop) return { phase: 'held', u, h: Hold, speed: v * shareAt(w, t), since: t - w.caught };
  if (w.drop <= w.letGo) {
    const a = t - w.drop, f = clamp(a / DropTime);
    return a < DropTime ? { phase: 'falling', u, h: Hold * (1 - f * f), speed: 0, since: a } : { phase: 'lying', u, h: 0, speed: 0, since: a - DropTime };
  }
  const stopT = w.stop?.t ?? Infinity;
  if (t < stopT) return { phase: 'flying', u: u + v * (t - w.letGo), h: Hold, speed: v, since: t - w.letGo };
  return { phase: w.stop.how, u: w.stop.u, h: 0, speed: 0, since: t - stopT };
}

// The violet 4-point star on the caster's eye when Amenoyodomi is turned on (Amenotejikara's).
export function eyeStar(pos, size, alpha) {
  if (alpha <= 0) return;
  sprite(pos, size * 1.2, size * 1.2, LavenderDeep.withAlpha(alpha * .7), glow, Y + .2);
  draw(MeshPool.plane10, pos.x, Y + .21, pos.z, size * 2, size * .12, 0, EyeStar.withAlpha(alpha), whiteGlow);
  draw(MeshPool.plane10, pos.x, Y + .21, pos.z, size * .12, size * 2, 0, EyeStar.withAlpha(alpha), whiteGlow);
}

// The two rings under a held weapon at ground point g, radius r, and three dots on the outer ring
// turned by turn degrees.
function marks(g, r, turn, alpha) {
  if (alpha <= 0) return;
  sprite(g, r * 2.6, r * 2.6, LavenderDeep.withAlpha(.22 * alpha), glow, Floor + .017);
  draw(rings[0], g.x, Floor + .02, g.z, r, r, 0, Lavender.withAlpha(.38 * alpha));
  draw(rings[1], g.x, Floor + .021, g.z, r * InnerShare, r * InnerShare, 0, Lavender.withAlpha(.3 * alpha));
  for (let k = 0; k < 3; k++) {
    const a = (turn + k * 120) * Mathf.Deg2Rad;
    draw(dot, g.x + Math.cos(a) * r, Floor + .022, g.z - Math.sin(a) * r, r * .1, r * .1, 0, Lavender.withAlpha(.6 * alpha));
  }
}
// A ring spreading on the floor from r0 to r1 over life seconds.
function ripple(g, age, life, r0, r1, alpha) {
  if (age < 0 || age >= life) return;
  const u = age / life, r = r0 + (r1 - r0) * smooth(u);
  draw(rings[0], g.x, Floor + .019, g.z, r, r, 0, Lavender.withAlpha(alpha * (1 - u) * (1 - u)));
}
function weaponSprite(w, pos, deg, turn, alpha = 1, material) {
  if (w.kind === 'fuma') sprite(pos, FumaSize, FumaSize, White.withAlpha(alpha), material ?? fumaMat, projectileLayer, turn);
  else sprite(pos, KunaiSize, KunaiSize, White.withAlpha(alpha), material ?? kunaiMat, projectileLayer, 90 - deg);
}
function shadowOf(w, g, h, deg, turn, sun, strength) {
  const c = { x: g.x + sun.x * h, z: g.z + sun.z * h };
  if (w.kind === 'fuma') sprite(c, 1, 1, ShadowInk.withAlpha(strength * .7), soft, shadowLayer, turn);
  else sprite(c, .1, .5, ShadowInk.withAlpha(strength * .8), soft, shadowLayer, 90 - deg);
}
function lies(w, g, deg, turn) {
  if (w.kind === 'fuma') sprite(g, FumaSize, FumaSize, White, fumaMat, Floor + .05, turn);
  else sprite(g, .62, .62, White, kunaiMat, Floor + .06, 90 - deg);
}
function dust(g, age, size) {
  if (age < 0 || age >= DustLife) return;
  const u = age / DustLife;
  sprite({ x: g.x, z: g.z + .05 + .1 * u }, size * (.4 + .6 * u), size * (.3 + .4 * u), Dust.withAlpha(.45 * (1 - u)), puff, Y + .005);
}

// Everything for one weapon at time t. look: { sun, strength, ghosts, ghostGap, ringR, rippleEvery,
// releaseTo (ground point a let-go would reach from here, for the rule overlay; null for none) }.
// A weapon stuck in a pawn (phase 'hit') is not drawn here: the sketch draws it on the pawn.
export function drawWeapon(key, w, t, look) {
  const m = motion(w, t);
  if (!m) return;
  const g = ground(w, m.u), deg = w.deg, turn = w.kind === 'fuma' ? turnOf(w, m.u) : 0;
  // The Fūma's rings sit outside its blades; its ripples and flashes are a little larger.
  const r = look.ringR * (w.kind === 'fuma' ? FumaRing : 1), big = w.kind === 'fuma' ? 1.8 : 1;
  const hang = w.held && isFinite(w.caught) ? ground(w, heldU(w, t)) : null;

  // The ripple it makes as it stops, and the flash on the weapon.
  if (hang && t >= w.caught) {
    const age = t - w.caught, c = ground(w, w.dist);
    ripple(c, age, CatchLife, r, CatchReach * big, .7);
    ripple(c, age - .08, CatchLife, r * .8, CatchReach * big * .7, .45);
    if (age < CatchFlash) sprite(at(c, 0, 0, Hold), .5 * big, .5 * big, Lavender.withAlpha(.65 * (1 - age / CatchFlash)), glow, Y + .02);
  }

  if (m.phase === 'flying' || m.phase === 'easing') {
    const pos = at(g, 0, 0, m.h), len = Math.min(.6, m.u, m.speed * .025);
    shadowOf(w, g, m.h, deg, turn, look.sun, look.strength);
    if (len > .03) streak(`${key} streak`, at(ground(w, m.u - len), 0, 0, m.h), pos, w.kind === 'fuma' ? .12 : .05,
      White.withAlpha(.45), whiteGlow, projectileLayer - .003, 6);
    if (w.kind === 'fuma' && m.speed > w.speed * .5) {
      for (let k = 1; k <= 2; k++) sprite(pos, FumaSize, FumaSize, White.withAlpha(.45 - .15 * k), fumaGhost, projectileLayer - .001 * k, turn - 22 * k);
    }
    weaponSprite(w, pos, deg, turn);
  }

  if (m.phase === 'held') {
    const pos = at(g, 0, 0, Hold), fade = smooth(m.since / .1), creptNow = m.u - w.dist;
    shadowOf(w, g, Hold, deg, turn, look.sun, look.strength);
    // The afterimages: the motion it was stopped in, left behind along its heading. They spread out
    // when it drifts faster.
    const gap = look.ghostGap + GhostPerSpeed * m.speed;
    for (let k = 1; k <= look.ghosts; k++) {
      const q = at(ground(w, m.u - gap * k), 0, 0, Hold);
      const tint = Lavender.withAlpha(GhostAlpha[k - 1] * fade);
      if (w.kind === 'fuma') sprite(q, FumaSize, FumaSize, tint, ghostFuma, projectileLayer - .001 * k, turn);
      else sprite(q, KunaiSize, KunaiSize, tint, ghostKunai, projectileLayer - .001 * k, 90 - deg);
    }
    weaponSprite(w, pos, deg, turn);
    // The marks on the floor, the ripple that leaves now and then, and the wake while it drifts.
    marks(g, r, DotTurn * creptNow / w.speed, fade);
    const every = look.rippleEvery, first = w.caught + every * (.5 + ((w.seed * .37) % .5));
    for (let n = Math.max(0, Math.floor((t - first) / every)); n >= 0 && n >= Math.floor((t - first) / every) - 1; n--) {
      const born = first + n * every;
      if (born <= t) ripple(ground(w, heldU(w, born)), t - born, RippleLife, r, r * RippleReach, .3);
    }
    for (let k = Math.floor(creptNow / WakeGap); k >= 1; k--) {
      const born = creptTime(w, k * WakeGap), age = t - born;
      if (age >= WakeLife) break;
      if (shareAt(w, born) < WakeFrom) continue;
      const q = ground(w, w.dist + k * WakeGap), f = age / WakeLife;
      draw(rings[0], q.x, Floor + .018, q.z, r * (.8 + .6 * f), r * (.8 + .6 * f), 0, Lavender.withAlpha(.24 * (1 - f)));
    }
    // Rule overlay: where a let-go would send it, and what is left of its 60 s.
    if (look.releaseTo) {
      const dx = look.releaseTo.x - g.x, dz = look.releaseTo.z - g.z, n = Math.floor(Math.hypot(dx, dz) / .25);
      for (let k = 1; k <= n; k++) draw(dot, g.x + dx * k / n, Floor + .015, g.z + dz * k / n, .035, .035, 0, Lavender.withAlpha(.4));
      const left = 1 - clamp(m.since / HoldLimit), R = r + .1, steps = Math.max(2, Math.ceil(left * 48)), outer = [], inner = [];
      for (let k = 0; k <= steps; k++) {
        const a = (90 - 360 * left * k / steps) * Mathf.Deg2Rad;
        outer.push({ x: g.x + Math.cos(a) * (R + .02), z: g.z + Math.sin(a) * (R + .02) });
        inner.push({ x: g.x + Math.cos(a) * R, z: g.z + Math.sin(a) * R });
      }
      band(`${key} timer`, outer, inner, White.withAlpha(.45), Floor + .023);
    }
  }

  // Let go: the rings pull in to the point it hung over and a small flash, as it leaves at full speed.
  if (hang && isFinite(w.letGo) && w.letGo <= w.drop && t >= w.letGo) {
    const age = t - w.letGo, c = ground(w, heldU(w, w.letGo));
    if (age < PullIn) marks(c, r * (1 - smooth(age / PullIn)), 0, 1 - age / PullIn);
    if (age < LetGoFlash) {
      sprite(at(c, 0, 0, Hold), .7 * big, .7 * big, Lavender.withAlpha(.7 * (1 - age / LetGoFlash)), glow, Y + .03);
      sprite(at(c, 0, 0, Hold), .28 * big, .28 * big, White.withAlpha(.9 * (1 - age / LetGoFlash)), glow, Y + .031);
    }
  }

  // Dropped: the marks fade where it hung, it falls straight down, a little dust, and it lies there.
  if (m.phase === 'falling') {
    if (m.since < RingFade) marks(g, r, 0, 1 - m.since / RingFade);
    shadowOf(w, g, m.h, deg, turn, look.sun, look.strength);
    weaponSprite(w, at(g, 0, 0, m.h), deg, turn);
  }
  if (m.phase === 'lying' || m.phase === 'land' || m.phase === 'range') {
    lies(w, g, deg, turn);
    dust(g, m.since, w.kind === 'fuma' ? 1.3 : .7);
  }
}
