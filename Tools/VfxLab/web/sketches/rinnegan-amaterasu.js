// Amaterasu — technique proposal for the Rinnegan eye kit, not the game. Nothing in Source/RimArt
// draws this yet.
//
// What it is for (proposed, none of it agreed; every number is a placeholder). Black flames light
// where the caster looks. Target: one pawn, or the kunai and Fūma shuriken the caster is holding in
// the air with Amenoyodomi. 12 cells, line of sight. 0.5 s warmup, 60 s cooldown.
//   On a pawn: a black-flame hediff, 4 burn damage per second for 20 s (80 total). Rain, water,
//   firefoam and beating do not put it out. It ends at 20 s or when the caster presses Release.
//   It spreads to an adjacent pawn at 10% per second, carrying the time that is left.
//   On held weapons: one cast lights every weapon Amenoyodomi is holding (up to 5), and they burn
//   in the air until it lets them go. A burning kunai that hits a pawn sticks in it (the kunai kit's
//   embedding) and the pawn gets the hediff above. One that misses burns on the cell it lands on for
//   20 s, and a pawn that steps on that cell catches. The Fūma keeps its own throw (an exact line,
//   30 cut damage falling 20% per pawn to 8, range 12) and every pawn it cuts catches, allies
//   included. It lands burning and cannot be picked up until the fire is out (20 s, or Release).
//   The flames never spread to the floor or to buildings, so it cannot eat a base.
//   Cost: the caster gets Bleeding eye for 60 s (Sight -50%). Casting again while it is on blinds
//   them for 10 s. This is the kit's finisher, not something for every fight.
// Pairs with Amenotejikara: swap a burning pawn in among its own side, or a burning kunai into a path.
// The 3x3 ground-cell target was dropped on 2026-09-23: fire on bare ground did not read as
// Amaterasu, and lighting ground cells is Flame Gauntlet Release's job.
//
// Look. The main reference is Storm 4 (YouTube LFZhDUGq6kQ, 1:07-1:09, Sasuke on Killer Bee), chosen
// by the user: black flames lit from inside by thin violet streaks, black brush-stroke wisps flung
// off the sides, a wide dark shadow on the ground, small black flames popping up on the
// floor round the target as it catches, and a fire that balloons out wide first and then stretches
// into a tall column. The rest comes from the anime (Shippuden 137-138, 142-143, Sasuke vs Danzo):
// the flames appear on the target at once with no projectile, cling to the body as ragged blotches,
// tear off at the top as flecks, and the casting eye bleeds. The inner light is a dropdown: violet
// (Storm 4), crimson (Shinobi Striker's jutsu) or none (the anime, pure black).
//
// Showcase, default timings:
//   0.00-0.50  gaze: a red glint on the caster's eye. The Mangekyo mark on the target is an option,
//              off by default, because the sources show the eye close-up and nothing at the target.
//   0.50       ignition: a black mass bursts out of the target's chest (0.2-0.3 s) and throws 18
//              shards up and out (0.32-0.57 s), a soot splash rings its feet, the fire catches from
//              the chest out in 0.12 s. It starts 1.45x as wide and half as tall and stretches into
//              its column by 1.4 s. 6 small ground flames pop up 0.6-1.05 cells round the target at
//              0.02-0.2 s intervals and die down within about a second. Camera shake 0.075, screen dim
//              0.18. Blood runs from the caster's eye from here on, two streaks.
//   burning    26 strands, 5 short base tongues and 3 tall core tongues (behind the pawn, the column's
//              body) per pawn, each on its own 0.4-0.95 s cycle: it grows, sways, tears its top off and
//              regrows. About a third of the tops torn from the flanks are flung outward as black
//              brush-stroke wisps (a blunt ragged head thinning into a tail that curls, 0.35-0.65
//              cells long, a short violet streak on the inside of the curl), 3-6 in the air at once;
//              the rest, and tops from the middle, are small black flecks. Every strand carries two
//              thin violet streaks that creep up it and flicker in steps 12 times a second, and one
//              violet speck. 8 blotches lick up the body; the pawn shows through the gaps. 12 loose
//              flecks rise and vanish. A dark pool and a wide soft shadow darken the floor.
//   2.00       the neighbour catches: 3 flecks jump across in 0.22 s, then blotches and strands creep
//              over it from the touching side in 0.3 s. No burst: it caught, nobody cast it.
//   4.00       the caster releases: strands and blotches sink over 0.3 s, flecks already in the air
//              finish, a puff of grey smoke rises from above the head for 0.8 s. The scorch stays.
// Held kunai scenario: three kunai hang where Amenoyodomi stopped them, 40% of the way to where they
// will land, 0.55 cells up. 0.50-0.60 they catch one after another: a small black burst, then
// flames up to 0.8 cells tall on each, tallest over the middle. 1.70 Amenoyodomi lets go: they fly
// on at 24 cells/s, the flames bent back into a black tail. Two hit and stick in their pawns, which
// catch from the chest out (1.85, 1.86); the third was a miss and lands on the floor 1.9 cells past
// them (1.90), where it keeps burning as a patch about a cell wide. At 2.90 a raider walking in
// steps on it and catches from the feet up.
// Held Fūma scenario: the Fūma hangs 1.6 cells in front of the caster, turning at 1% of its spin.
// 0.50 its hub and blades catch; flames up to 1.15 cells tall stand on the blades. 1.70 it is let
// go: back to full spin, 14.4 cells/s along its line; the blades blur and the fire streams back
// off the whole disc like a comet. The three raiders on the line catch as it cuts them (1.92, 2.02,
// 2.12); it lands 3.2 cells past the middle one (2.23) and lies there with flames on its blades.
//
// Drawing: strands and tongues are band meshes on a wavy spine with a tapered, ragged width, height
// drawn north by Lift. On a pawn, strands rooted on the ground north of its feet draw under the pawn
// layer and the rest over it, so the pawn stands inside the fire. Blotches, flecks and shards are
// sprites of two generated textures, lab/black-blot and lab/black-shred (both need PNGs before a C#
// port). Everything is screen-oriented, so there is no per-facing drawing. All births are analytic
// from the clip time, so scrubbing is deterministic.
// The held weapons use the game's textures and numbers: RimArt/Kunai/Kunai at the projectile's
// drawSize 0.75 and 24 cells/s (AG_KunaiProjectile), RimArt/Fuma/Unfolded at 1.4 and 14.4 cells/s
// (AG_FumaProjectile). The hold is a stand-in, since Amenoyodomi has no sketch yet: the weapons just
// hang still with a shadow under them (at 1% a kunai would creep 0.24 cells/s; not drawn). The
// Fūma's spin (720 degrees/s, clockwise as in the backhand throw sketch) is a stand-in too. Held or
// landed, its flames root on the blades, measured off the Unfolded texture: blade k runs at
// 22 + 40 r + 90 k degrees at r texture widths from the middle. In flight they root round the rim
// instead, and two see-through copies of the blades 22 and 44 degrees behind show the spin (a black
// ring for the spin read as a tyre, and with pointed licks as a gear). Flames on a flying weapon lean back along its path;
// otherwise they stay screen-oriented, so "Throw direction" only turns the layout.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, MeshPool, ShaderDatabase } from '../js/engine.js';
import { registerLabTexture, pixels, fbm } from '../js/standins.js';
import { draw, mesh, Lift } from './lib/six-paths-solid.js';
import { P, Y, Floor, at, sprite, band, trail, glow, soft, rand } from './lib/six-paths-impact.js';
import { figure, whiteGlow, kunaiMat, stuckKunai, CasterColour, EnemyColour } from './lib/flying-thunder-god.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const pawnLayer = AltitudeLayer.Pawn.AltitudeFor(), topLayer = AltitudeLayer.MetaOverlays.AltitudeFor();
const projectileLayer = AltitudeLayer.Projectile.AltitudeFor(), shadowLayer = AltitudeLayer.Shadows.AltitudeFor();
const flatDisc = Meshes.disc(48, 'amaterasu disc');

// A torn black scrap, taller than wide: two lopsided lumps with a notched edge and a hole, so it
// reads as a torn piece of flame rather than a leaf or a teardrop. White in the alpha.
registerLabTexture('lab/black-shred', () => pixels(128, (u, v) => {
  const x = (u - .5) * 2, y = (.5 - v) * 2;
  const low = Math.hypot(x / .5, (y + .2) / .62), high = Math.hypot((x - .14) / .3, (y - .38) / .38);
  const n = fbm(u * 6, v * 6, 41, 3, 6) - .5, notch = fbm(u * 13, v * 13, 7, 2, 13) - .5;
  let a = clamp((1 + n * .7 + notch * .4 - Math.min(low, high)) / .08);
  const hole = fbm(u * 7, v * 7, 97, 2, 7);
  if (hole > .7) a *= clamp((.78 - hole) / .08);
  return [1, 1, 1, a * clamp(Math.min(u, 1 - u, v, 1 - v) / .04)];
}));
// A ragged round blot with a few specks thrown off its edge. White in the alpha.
registerLabTexture('lab/black-blot', () => pixels(128, (u, v) => {
  const r = Math.hypot(u - .5, v - .5) * 2;
  const rag = fbm(u * 4, v * 4, 13, 3, 4);
  let a = clamp((.52 + (rag - .5) * 1.1 - r) / .07);
  const speck = fbm(u * 10, v * 10, 57, 2, 10);
  if (r > .5 && r < .88 && speck > .66) a = Math.max(a, clamp((speck - .66) / .05));
  return [1, 1, 1, a * clamp((.98 - r) / .06)];
}));
const shred = MaterialPool.MatFrom('lab/black-shred', ShaderDatabase.Transparent);
const blot = MaterialPool.MatFrom('lab/black-blot', ShaderDatabase.Transparent);
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const fumaMat = MaterialPool.MatFrom('RimArt/Fuma/Unfolded', ShaderDatabase.Cutout);
const fumaGhost = MaterialPool.MatFrom('RimArt/Fuma/Unfolded', ShaderDatabase.Transparent);

// Decided looks. The flame is one colour, black; blood and the eye glint are the only red.
const Ink = new Color(.010, .008, .014);
const Scorch = new Color(.05, .035, .04), Smoke = new Color(.17, .16, .18), ShadowInk = new Color(.03, .03, .05);
const Crimson = new Color(.75, .08, .12), EmberLit = new Color(.85, .16, .14), Blood = new Color(.52, .03, .05);
const Ally = new Color(.45, .62, .40);
// The light inside the black, per source: violet (Storm 4, 1:07-1:09 of the reference clip), crimson
// (Shinobi Striker's jutsu) or none (the anime). It is additive, so it only shows on the black.
const Lights = { 'violet (Storm 4)': new Color(.50, .24, .95), 'crimson (Shinobi Striker)': new Color(.85, .10, .12), 'none (anime)': null };
const LightNames = Object.keys(Lights);
const Gaze = .5, MarkR = .55, Sink = .3, DimLife = .18, ShakeSize = .075, SmokeLife = .8, JumpTime = .22;
const Strands = 26, BaseTongues = 5, CoreTongues = 3, Blotches = 8, LooseFlecks = 12;
const Scenarios = ['pawn', 'pawn, spreads', 'held kunai', 'held Fūma'];
// Held scenes. Speeds and sizes are the game's (see the header); the hold, its height and the spin
// are stand-ins. HoldShare is how far along its path a kunai was stopped.
const KunaiSpeed = 24, KunaiSize = .75, FumaSpeed = 14.4, FumaSize = 1.4, FumaSpin = 720, HeldSpin = FumaSpin * .01;
const HoldLift = .55, HoldShare = .4, FumaHold = 1.6, HitLift = .35, KunaiStagger = .05;
const TailTime = .09, TailLean = 1.3, FumaLean = 1.2, WalkSpeed = 2.2, StepAfter = 1;
const NoLean = { x: 0, z: 0 };

function times(p) {
  const ignite = Gaze, release = p.release ? ignite + p.releaseAt : Infinity, letGo = ignite + p.letGo;
  const end = p.release ? release + SmokeLife + .2 : ignite + p.burn; // showcase cut, not a gameplay expiry
  return { ignite, release, letGo, end };
}

// The held scenes, laid out along the throw direction d from the caster (n is d's left); o is the
// middle of the targets. Each weapon records where it hangs, where it ends and when it gets there.
function layout(p, o) {
  const a = p.aim * Mathf.Deg2Rad, d = { x: Math.cos(a), z: Math.sin(a) }, n = { x: -d.z, z: d.x };
  const go = (q, u, v = 0) => ({ x: q.x + d.x * u + n.x * v, z: q.z + d.z * u + n.z * v });
  const t = times(p), caster = go(o, -p.distance);
  // Kunai: the first two hit their pawns; the third was thrown at the first and misses wide.
  const pawns = [go(o, 0, .9), go(o, .5, -.8)], miss = go(o, 1.9, .25);
  const kunai = [pawns[0], pawns[1], miss].map((end, i) => {
    const hold = { x: lerp(caster.x, end.x, HoldShare), z: lerp(caster.z, end.z, HoldShare) };
    return {
      hold, end, hit: i < 2, lit: t.ignite + i * KunaiStagger,
      arrive: t.letGo + Math.hypot(end.x - hold.x, end.z - hold.z) / KunaiSpeed,
      deg: Math.atan2(end.z - hold.z, end.x - hold.x) * Mathf.Rad2Deg,
    };
  });
  // Fūma: hangs FumaHold cells out, three raiders stand on its line, it lands on the target cell.
  const fumaHold = go(caster, FumaHold), line = [go(o, -1.2), go(o, .2), go(o, 1.6)], land = go(o, 3.2);
  const reach = q => t.letGo + Math.hypot(q.x - fumaHold.x, q.z - fumaHold.z) / FumaSpeed;
  return { d, go, caster, pawns, miss, kunai, step: kunai[2].arrive + StepAfter, fumaHold, line, land, cuts: line.map(reach), landed: reach(land) };
}

// The Mangekyo mark (optional): a pupil and 3 curved blades, drawn from the centre outward as u goes
// 0..1, turning by spin radians.
function mark(key, c, u, spin, alpha) {
  if (u <= 0 || alpha <= 0) return;
  draw(flatDisc, c.x, Floor + .03, c.z, .1 * u, .1 * u, 0, Crimson.withAlpha(alpha));
  for (let i = 0; i < 3; i++) {
    const a0 = spin + i * Math.PI * 2 / 3, pts = [];
    for (let k = 0; k <= 8; k++) {
      const v = k / 8 * u, r = .12 + MarkR * v, a = a0 + v * 1.4;
      pts.push({ x: c.x + Math.cos(a) * r, z: c.z + Math.sin(a) * r });
    }
    trail(`${key} blade ${i}`, pts, .16, Crimson.withAlpha(alpha), Floor + .031);
  }
}

function redStar(pos, size, alpha) {
  if (alpha <= 0) return;
  sprite(pos, size * 1.2, size * 1.2, Crimson.withAlpha(alpha * .7), glow, Y + .2);
  draw(MeshPool.plane10, pos.x, Y + .21, pos.z, size * 2, size * .12, 0, EmberLit.withAlpha(alpha), whiteGlow);
  draw(MeshPool.plane10, pos.x, Y + .21, pos.z, size * .12, size * 2, 0, EmberLit.withAlpha(alpha), whiteGlow);
}

// Bleeding eye: two streaks of blood run down from the casting eye after the cast. In game this is
// the cost hediff, shown on the pawn.
function bleed(eye, age) {
  const l1 = .05 + .15 * smooth(age / 1.2), l2 = .03 + .08 * smooth((age - .3) / 1.2);
  draw(MeshPool.plane10, eye.x + .025, pawnLayer + .01, eye.z - .04 - l1 / 2, .03, l1, 0, Blood);
  if (age > .3) draw(MeshPool.plane10, eye.x - .02, pawnLayer + .01, eye.z - .04 - l2 / 2, .022, l2, 0, Blood);
}

// One strand: a black ribbon on a wavy spine. The wave runs up it, each edge boils upward on its own
// rhythm, and the width tapers from the root to a point. lean and leanZ push the tip east and north
// per cell of height (a flying weapon's flames lean back along its path).
const StrandSteps = 16;
function strand(key, root, h, hw, clock, seed, alpha, layer, lean, rootW = .6, leanZ = 0) {
  if (h < .03 || hw < .004 || alpha <= .003) return;
  const phase = rand(seed) * 6.283, f1 = 5 + rand(seed + 1) * 3, f2 = 2 + rand(seed + 2) * 1.5;
  const amp = .05 + .05 * h, left = [], right = [], spine = [];
  for (let j = 0; j <= StrandSteps; j++) {
    const u = j / StrandSteps;
    const sway = amp * u * (.7 * Math.sin(u * f1 - clock * 7 + phase) + .5 * u * Math.sin(u * f2 - clock * 3.3 + phase * 1.7));
    const cx = root.x + lean * u * h + sway, cz = root.z + h * Lift * u + leanZ * u * h;
    const profile = (rootW + (1 - rootW) * Math.sin(Math.min(1, u / .3) * Math.PI / 2)) * Math.pow(1 - u, .8);
    const ragL = 1 + .38 * Math.sin(u * 17 - clock * 10 + phase) * Math.sin(u * 5.3 + phase * 2);
    const ragR = 1 + .38 * Math.sin(u * 15 - clock * 9 + phase * 1.3) * Math.sin(u * 4.1 + phase * 3);
    left.push({ x: cx - hw * profile * ragL, z: cz }); right.push({ x: cx + hw * profile * ragR, z: cz });
    spine.push({ x: cx, z: cz, hw: hw * profile });
  }
  band(key, left, right, Ink.withAlpha(alpha), layer);
  return spine;
}

// band() and trail() from the impact lib, with a material, for the additive light.
function bandMat(key, a, b, colour, layer, material) {
  const vertices = [], tri = [];
  for (let i = 0; i < a.length; i++) {
    vertices.push(a[i].x, a[i].z, b[i].x, b[i].z);
    if (i) { const n = i * 2; tri.push(n - 2, n, n - 1, n - 1, n, n + 1); }
  }
  const m = mesh(key); m.setFlat(vertices, tri);
  draw(m, 0, layer, 0, 1, 1, 0, colour, material);
}
function trailMat(key, pts, width, colour, layer, material) {
  const a = [], b = [];
  pts.forEach((q, i) => {
    const prev = pts[Math.max(0, i - 1)], next = pts[Math.min(pts.length - 1, i + 1)];
    const dx = next.x - prev.x, dz = next.z - prev.z, len = Math.hypot(dx, dz) || 1;
    const w = Math.sin(i / (pts.length - 1) * Math.PI) * width / 2;
    a.push({ x: q.x - dz / len * w, z: q.z + dx / len * w }); b.push({ x: q.x + dz / len * w, z: q.z - dx / len * w });
  });
  bandMat(key, a, b, colour, layer, material);
}

// The light inside a strand (Storm 4's violet streaks): two thin wavy lines crawl up it and flicker in
// steps, 12 times a second, plus one speck. They stay inside the strand's width, so they only show
// on the black.
function veins(key, spine, clock, seed, alpha, layer, light) {
  if (!spine || !light || alpha <= .01) return;
  const step = Math.floor(clock * 12), n = spine.length - 1;
  const along = (u, off) => {
    const f = clamp(u) * n, i0 = Math.min(n - 1, Math.floor(f)), t = f - i0, p0 = spine[i0], p1 = spine[i0 + 1];
    return { x: p0.x + (p1.x - p0.x) * t + off * (p0.hw + (p1.hw - p0.hw) * t), z: p0.z + (p1.z - p0.z) * t };
  };
  for (let v = 0; v < 2; v++) {
    const k = seed + v * 101, len = .2 + rand(k) * .2;
    const f = (clock * (.45 + rand(k + 1) * .35) + rand(k + 2)) % 1;
    const u0 = .05 + f * (.8 - len), off = (rand(k + 3) - .5) * .55;
    const a = alpha * (.3 + .7 * rand(step * 13 + k)) * Math.sin(f * Math.PI);
    if (a <= .01) continue;
    const pts = [];
    for (let j = 0; j <= 6; j++) {
      const u = u0 + len * j / 6, q = along(u, off);
      pts.push({ x: q.x + Math.sin(u * 23 + clock * 9 + k) * .012, z: q.z });
    }
    trailMat(`${key} ${v}`, pts, .018 + rand(k + 4) * .01, light.withAlpha(a), layer, whiteGlow);
  }
  const q = along(.15 + rand(seed + 7) * .5, (rand(seed + 8) - .5) * .8);
  sprite(q, .05, .05, light.withAlpha(alpha * rand(step * 7 + seed) * .9), glow, layer + .00005);
}

// A black wisp flung off the side of the flame (Storm 4, 1:08-1:09): a brush stroke with a blunt,
// ragged head that leaves the flame outward and upward, thins along its length and curls at the tail.
// The inner light is a short streak along the inside of the curl near the head: not a midline, which
// made it read as a leaf with a vein. dir is in radians, curl in radians per cell (its sign is the
// side it curls to).
function wisp(key, start, dir, len, width, curl, alpha, layer, light, seed) {
  if (alpha <= .01 || len < .02) return;
  const N = 16, ds = len / N, spine = [], left = [], right = [];
  let ang = dir, x = start.x, z = start.z;
  for (let j = 0; j <= N; j++) {
    const t = j / N;
    spine.push({ x, z, ang, h: width * .5 * (t < .1 ? .55 + 4.5 * t : 1) * Math.pow(1 - t, 1.1) });
    ang += curl * 3 * t * t * ds;
    x += Math.cos(ang) * ds; z += Math.sin(ang) * ds;
  }
  spine.forEach(q => {
    const nx = -Math.sin(q.ang), nz = Math.cos(q.ang);
    left.push({ x: q.x + nx * q.h, z: q.z + nz * q.h }); right.push({ x: q.x - nx * q.h, z: q.z - nz * q.h });
  });
  bandMat(key, left, right, Ink.withAlpha(alpha), layer);
  sprite(start, width * 1.25, width * 1.25, Ink.withAlpha(alpha), blot, layer - .00005, rand(seed) * 360);
  if (light) {
    const inside = Math.sign(curl) || 1;
    const pts = spine.slice(1, 8).map(q => ({ x: q.x - Math.sin(q.ang) * q.h * .5 * inside, z: q.z + Math.cos(q.ang) * q.h * .5 * inside }));
    trailMat(key + ' light', pts, width * .2, light.withAlpha(alpha * .6), layer + .0001, whiteGlow);
  }
}

// Storm 4: as the target catches, small black flames pop up on the floor round it and die down within
// about a second. They are splash, not a burning area.
function clumps(key, c, age, clock, seed, height, light) {
  if (age < 0 || age > 1.4) return;
  for (let i = 0; i < 6; i++) {
    const k = seed + 800 + i * 19, t0 = .02 + i * .035, u = (age - t0) / (.7 + rand(k) * .4);
    if (u <= 0 || u >= 1) continue;
    const a = (i * 60 + 25 + rand(k + 1) * 30) * Math.PI / 180, r = .6 + rand(k + 2) * .45;
    const q = at(c, Math.cos(a) * r, Math.sin(a) * r);
    const life = smooth(u / .15) * (1 - smooth((u - .55) / .45));
    const layer = (q.z > c.z + .05 ? pawnLayer - .03 : Y + .025) + i * .001;
    for (let j = 0; j < 3; j++) {
      const kk = k + j * 7;
      const sp = strand(`${key} clump ${i}-${j}`, at(q, (j - 1) * .07, 0), (.3 + rand(kk) * .35) * life * height / 2.4,
        .045 + rand(kk + 1) * .025, clock, kk, Math.min(1, life * 3), layer + j * .0002, (j - 1) * .15);
      veins(`${key} clump vein ${i}-${j}`, sp, clock, kk, life * .8, layer + j * .0002 + .0001, light);
    }
  }
}

// A strand's life, in clock time: it grows from 35% to full height over the first 45% of its cycle,
// holds, and over the last 20% its top tears off as a fleck while the stem drops back to 35%.
function cycleOf(clock, period, offset) {
  const c = (clock + offset) / period, n = Math.floor(c), u = c - n;
  return { n, k: .35 + .65 * (smooth(u / .45) - smooth((u - .8) / .2)) };
}

// The cast: a black mass bursts out of the focal point and throws shards up and out, the upward
// ones furthest, and a soot splash rings the floor.
function eruption(c, age, seed, scale) {
  if (age < 0 || age > .6) return;
  const splash = age / .35;
  if (splash < 1) {
    const d = (.9 + 2.4 * smooth(splash)) * scale;
    sprite(c, d, d, Ink.withAlpha(.5 * (1 - splash)), puff, Floor + .022);
  }
  const focus = at(c, 0, 0, .55);
  for (let i = 0; i < 5; i++) {
    const k = seed + 950 + i * 3, u = age / (.2 + rand(k) * .1);
    if (u >= 1) continue;
    const size = (.35 + .7 * smooth(u * 2)) * (.7 + .5 * rand(k + 1)) * scale;
    sprite(at(focus, (rand(k + 2) - .5) * .4, (rand(k + 3) - .3) * .5), size, size * 1.2,
      Ink.withAlpha(1 - smooth((u - .4) / .6)), blot, Y + .139 + i * .0002, rand(k + 4) * 360);
  }
  for (let i = 0; i < 18; i++) {
    const k = seed + 900 + i * 7, life = .32 + rand(k) * .25, u = age / life;
    if (u >= 1) continue;
    const a = (15 + rand(k + 1) * 150) * Math.PI / 180; // up and out, none straight down
    const d = (.5 + rand(k + 2) * 1.1) * (.7 + .5 * Math.sin(a)) * scale * (1 - Math.pow(1 - u, 3));
    const len = (.22 + rand(k + 3) * .35) * scale * (1 - .5 * u), wid = len * (.22 + rand(k + 4) * .18);
    sprite({ x: focus.x + Math.cos(a) * d, z: focus.z + Math.sin(a) * d }, wid, len,
      Ink.withAlpha(1 - smooth((u - .45) / .55)), shred, Y + .14 + i * .0003, 90 - a * 180 / Math.PI);
  }
}

// A smaller burst where a held weapon catches or a burning weapon strikes a pawn: a blot swells for
// 0.2 s at pos (already lifted) and count shards fly up and out, none straight down.
function burst(pos, age, seed, scale, count) {
  if (age < 0 || age > .5) return;
  const u = age / .22;
  if (u < 1) {
    const size = (.25 + .55 * smooth(u * 2)) * scale;
    sprite(pos, size, size * 1.15, Ink.withAlpha(1 - smooth((u - .4) / .6)), blot, Y + .139, rand(seed) * 360);
  }
  for (let i = 0; i < count; i++) {
    const k = seed + 900 + i * 7, life = .25 + rand(k) * .2, v = age / life;
    if (v >= 1) continue;
    const a = (20 + rand(k + 1) * 140) * Math.PI / 180, d = (.25 + rand(k + 2) * .55) * scale * (1 - Math.pow(1 - v, 3));
    const len = (.14 + rand(k + 3) * .2) * scale * (1 - .5 * v), wid = len * (.25 + rand(k + 4) * .15);
    sprite({ x: pos.x + Math.cos(a) * d, z: pos.z + Math.sin(a) * d }, wid, len,
      Ink.withAlpha(1 - smooth((v - .45) / .55)), shred, Y + .14 + i * .0003, 90 - a * 180 / Math.PI);
  }
}

// Three flecks jump from the burning pawn to the one beside it just before it catches.
function jump(a, b, s, t0) {
  const age = s - (t0 - JumpTime);
  if (age < 0 || age > JumpTime) return;
  const x0 = a.x + .3, x1 = b.x - .2;
  for (let i = 0; i < 3; i++) {
    const u = clamp((age - i * .03) / (JumpTime - .06));
    if (u <= 0 || u >= 1) continue;
    const h = .75 + .45 * Math.sin(u * Math.PI) + i * .12;
    const pos = { x: Mathf.Lerp(x0, x1, u), z: Mathf.Lerp(a.z, b.z, u) + (i - 1) * .07 + h * Lift };
    const slope = Math.atan2(.45 * Math.PI * Math.cos(u * Math.PI) * Lift, x1 - x0);
    sprite(pos, .07, .2, Ink.withAlpha(Math.sin(u * Math.PI) * 1.5), shred, Y + .135 + i * .0003, 90 - slope * 180 / Math.PI);
  }
}

// The fire on one pawn. how says how it caught: 'cast' (the caster lit it: a burst, then it balloons
// into a column), -1 or 1 (from a burning neighbour on that side), 'hit' (a burning kunai or the Fūma
// struck it: from the chest out, no cast burst), 'feet' (it stepped on a burning weapon: from the
// floor up).
function fire(key, c, w, height, t0, s, p, t, seed, how = 'cast') {
  const age = s - t0;
  if (age < 0 || t0 >= t.release) return;
  const rel = 1 - smooth((s - t.release) / Sink);
  const clock = age * p.speed, lastBirth = (t.release - t0) * p.speed;
  const side = how === -1 || how === 1 ? how : 0, cast = how === 'cast';
  // Each part catches after a delay: from the chest outward on a cast or a hit, from the touching side
  // on a spread, from the floor up on a step-in (h is how high on the body the part starts).
  const delayOf = (x, h) => side ? clamp((-side * x / w + 1) / 2) * .3 : how === 'feet' ? clamp(h / .9) * .35 : Math.abs(x) / w * .08;
  const catchAt = (x, a, h = 0) => smooth((a - delayOf(x, h)) / p.rise);
  // Storm 4's growth on a cast: the fire balloons out wide and low, then stretches up into a column
  // over 0.9 s. Any other catch comes in at its full shape.
  const swell = cast ? 1 - smooth(age / .9) : 0, widthF = 1 + .45 * swell, heightF = 1 - .5 * swell;
  const light = Lights[p.light] ?? null;

  // Floor: a wide soft shadow darkens the ground round the fire (Storm 4) and thins out after release.
  // Over it the pool, a dark stain rather than solid black so black flames standing in it still read.
  const stain = smooth(age / .25), poolAlpha = .65 * stain * (.5 + .5 * rel);
  sprite(c, w * 4.6, w * 4.6, Ink.withAlpha(.38 * stain * (.3 + .7 * rel)), soft, Floor + .016);
  sprite(c, w * 2.3, w * 2.3, Scorch.withAlpha(.6 * stain), puff, Floor + .018);
  sprite(at(c, 0, -.05), w * 1.9, w * 1.9, Ink.withAlpha(poolAlpha), blot, Floor + .02, seed % 360);
  if (cast) eruption(c, age, seed, 1);
  if (cast && rel > 0) clumps(key, c, age, clock, seed, height, light);

  if (rel > 0) {
    // The pawn inside: darkened, with blotches clinging to it and licking upward.
    const cover = catchAt(0, age, .3) * rel;
    sprite(at(c, 0, .3), .5, .9, Ink.withAlpha(.3 * cover), soft, pawnLayer + .003);
    for (let i = 0; i < Blotches; i++) {
      const k = seed + 500 + i * 11, head = i >= 6;
      const bx = (rand(k) - .5) * (head ? .2 : .36), bz = head ? .46 + rand(k + 1) * .2 : -.06 + rand(k + 1) * .46;
      const g = catchAt(bx, age, bz) * rel;
      if (g <= 0) continue;
      const period = .7 + rand(k + 2) * .5, u = ((clock + rand(k + 3) * period) / period) % 1;
      const size = (head ? .17 : .21) * (.8 + .4 * rand(k + 4)) * (1 - .35 * u) * g;
      sprite(at(c, bx + Math.sin(clock * 3 + k) * .02, bz + u * .14), size, size * 1.15,
        Ink.withAlpha(Math.min(1, Math.sin(u * Math.PI) * 2.2) * .95), blot, pawnLayer + .004 + i * .0002, (k * 47 + clock * 40) % 360);
    }

    // Strands and broad base tongues, drawn north to south so nearer ones overlap.
    const parts = [], total = Strands + BaseTongues + CoreTongues;
    for (let i = 0; i < total; i++) {
      const k = seed + i * 13, base = i >= Strands;
      const core = i >= Strands + BaseTongues; // tall, broad tongues behind the pawn: the column's body
      let rootH = 0;
      // Base tongues sit at fixed, uneven places across the feet so they never line up into a block.
      const x = core ? [-.3, .04, .34][i - Strands - BaseTongues] * w : base ? [-.62, -.2, .12, .5, -.4][i - Strands] * w : (rand(k) * 2 - 1) * w * .8;
      const dz = core ? [.14, .18, .12][i - Strands - BaseTongues] : base ? [.06, -.08, .1, -.04, -.12][i - Strands] : (rand(k + 1) - .5) * .3;
      if (!base && rand(k + 2) > .7) rootH = .2 + rand(k + 3) * .6; // a few start on the body, not the floor
      parts.push({ i, k, base, core, x, dz, rootH });
    }
    parts.sort((a, b) => b.dz - a.dz);
    parts.forEach((q, order) => {
      const { i, k, base, core, x, dz, rootH } = q;
      const env = Math.sqrt(Math.max(0, 1 - (x / w) ** 2)), tall = rand(k + 4);
      const full = height * (core ? .68 + .2 * tall : base ? .18 + .22 * tall : (.35 + .65 * env) * (.45 + .55 * tall));
      const hw = core ? .15 + .05 * rand(k + 5) : base ? .09 + .06 * rand(k + 5) : .035 + .06 * (1 - tall);
      const period = base ? .4 + rand(k + 6) * .2 : .55 + rand(k + 6) * .4, offset = rand(k + 7) * period;
      const cyc = cycleOf(clock, period, offset);
      const g = catchAt(x, age, rootH);
      const h = full * cyc.k * g * heightF * rel;
      const root = at(c, x * widthF, dz, rootH);
      const lean = x / w * (base ? .25 : .1) + .03 * Math.sin(clock * 1.3 + k);
      const behind = rootH === 0 && dz > .02;
      const layer = (behind ? pawnLayer - .02 : Y + .03) + order * .0004;
      const spine = strand(`${key} strand ${i}`, root, h, hw * (.5 + .5 * rel) * (base ? 1 + .6 * swell : 1), clock, k, Math.min(1, g * 3), layer, lean, rootH > 0 ? .2 : .6);
      veins(`${key} vein ${i}`, spine, clock, k, g * rel * (base ? .8 : 1), layer + .0001, light);

      // The torn-off top of this strand's last two cycles. From the flanks it is a curling wisp that
      // flies outward (Storm 4); from the middle a small fleck that rises. None are born after release.
      if (base || p.particles <= 0) return;
      for (const n of [cyc.n, cyc.n - 1]) {
        const tear = (n + .8) * period - offset, life = .5 + rand(k + n * 31) * .3, fu = (clock - tear) / life;
        if (tear < 0 || tear > lastBirth || fu < 0 || fu >= 1) continue;
        const kk = k + n * 31, top = full * catchAt(x, tear / p.speed, rootH);
        if (Math.abs(x) > w * .45) {
          // About a third of the flank tears throw a wisp: 3-6 in the air at once, as in Storm 4.
          if (rand(kk + 9) > .35) continue;
          const side = x >= 0 ? 1 : -1, out = 1 - (1 - fu) * (1 - fu);
          const start = at(c, x * widthF + lean * top + side * out * (.25 + rand(kk + 1) * .35), dz,
            rootH + top * .75 + out * (.3 + rand(kk + 2) * .45) * height / 2.4);
          const dir = side > 0 ? (20 + rand(kk + 3) * 50) * Math.PI / 180 : Math.PI - (20 + rand(kk + 3) * 50) * Math.PI / 180;
          wisp(`${key} wisp ${i} ${n & 1}`, start, dir, (.35 + rand(kk + 4) * .3) * (1 - .45 * fu) * height / 2.4,
            (.07 + rand(kk + 7) * .05) * (1 - .3 * fu), -side * (2.6 + rand(kk + 6) * 2 + fu * 2.5),
            (1 - smooth((fu - .45) / .55)) * Math.min(1, p.particles), Y + .127 + (i % 20) * .0003, light, kk);
          continue;
        }
        const pos = at(c, x * widthF + lean * top + (rand(kk + 1) - .5) * .25 * fu + Math.sin(fu * 5 + kk) * .05, dz,
          rootH + top * .85 + fu * (.5 + rand(kk + 2) * .6) * height / 2.4);
        // About twice as tall as wide: a torn piece, not a leaf.
        const fw = Math.max(.08, hw * 2) * (.8 + .5 * rand(kk + 3)) * (1 - .45 * fu);
        sprite(pos, fw, fw * (1.5 + .7 * rand(kk + 4)) * (1 - .2 * fu),
          Ink.withAlpha((1 - smooth((fu - .45) / .55)) * Math.min(1, p.particles)), shred, Y + .128 + (i % 20) * .0002,
          (rand(kk + 5) - .5) * 70 + Math.sin(fu * 4 + kk) * 25);
      }
    });
  }

  // Loose flecks rising out of the flames. Births stop at release; flecks in the air finish.
  const loose = Math.round(LooseFlecks * p.particles);
  for (let i = 0; i < loose; i++) {
    const k = seed + 300 + i * 17, period = .6 + rand(k) * .35, offset = rand(k + 1) * period;
    const n = Math.floor((Math.min(clock, lastBirth) - offset) / period);
    if (n < 0) continue;
    const born = offset + n * period, u = (clock - born) / period;
    if (u < 0 || u >= 1) continue;
    const r = k + n * 31, x = (rand(r) * 2 - 1) * w * .8;
    const z0 = (rand(r + 5) - .5) * .2;
    const h0 = height * .5 * (.4 + .6 * rand(r + 2)) * catchAt(x, born / p.speed);
    const size = .05 + rand(r + 4) * .07;
    sprite(at(c, x + Math.sin(u * 5 + r) * .1, z0, h0 + u * (.8 + rand(r + 3) * .8)), size * (1 - .5 * u), size * 2 * (1 - .4 * u),
      Ink.withAlpha(Math.min(1, Math.sin(u * Math.PI) * 1.4)), shred, Y + .13 + i * .0002, Math.sin(u * 5 + r) * 35);
  }

  // After release: a last puff of grey smoke rises and thins out.
  const since = s - t.release;
  if (since >= 0 && since < SmokeLife) {
    for (let i = 0; i < 6; i++) {
      const k = seed + 700 + i * 5, u = clamp((since - i * .04) / (SmokeLife - .2));
      if (u <= 0 || u >= 1) continue;
      const size = .35 + u * .6;
      sprite(at(c, (rand(k) - .5) * w * 1.2, 0, 1.1 + u * (1.2 + rand(k + 1))), size * 1.3, size * 1.3,
        Smoke.withAlpha(.22 * Math.sin(u * Math.PI)), puff, Y + .12 + i * .0002);
    }
  }
}

// Flames on a small thing: a kunai, the Fūma's blades, a weapon on the floor. One strand per root,
// each on its own grow-tear-regrow cycle like the pawn's; about half the torn tops rise as flecks.
// A root is { x, z, h, full, hw, k, vein, base }: x, z on the ground, h its height, full the flame's
// height in cells; a base root is a short broad tongue that joins the strands at the bottom, as on
// a pawn, and never tears. lean { x, z } bends the flames back while the thing flies; a flying thing
// drops its flecks along its path instead (pathFlecks).
function smallFlames(key, roots, clock, g, rel, lean, light, layer, lastBirth, particles) {
  const moving = lean.x !== 0 || lean.z !== 0;
  roots.slice().sort((a, b) => b.z - a.z).forEach((r, i) => {
    const period = r.base ? .4 + rand(r.k + 6) * .2 : .45 + rand(r.k + 6) * .35, offset = rand(r.k + 7) * period;
    const cyc = cycleOf(clock, period, offset), root = at(r, 0, 0, r.h);
    const spine = strand(`${key} ${i}`, root, r.full * cyc.k * g * rel, r.hw * (.5 + .5 * rel), clock, r.k,
      Math.min(1, g * 3), layer + i * .0004, lean.x, .6, lean.z);
    if (r.vein) veins(`${key} vein ${i}`, spine, clock, r.k, g * rel, layer + i * .0004 + .0001, light);
    if (moving || particles <= 0 || r.base) return;
    for (const n of [cyc.n, cyc.n - 1]) {
      const tear = (n + .8) * period - offset, life = .45 + rand(r.k + n * 31) * .25, fu = (clock - tear) / life;
      const kk = r.k + n * 31;
      if (tear < 0 || tear > lastBirth || fu < 0 || fu >= 1 || rand(kk + 9) > .5) continue;
      const fw = Math.max(.05, r.hw * 1.8) * (.8 + .5 * rand(kk + 3)) * (1 - .45 * fu);
      sprite(at(root, (rand(kk + 1) - .5) * .15 * fu + Math.sin(fu * 5 + kk) * .03, 0, r.full * g * .85 + fu * (.3 + rand(kk + 2) * .35)),
        fw, fw * (1.5 + .7 * rand(kk + 4)) * (1 - .2 * fu), Ink.withAlpha((1 - smooth((fu - .45) / .55)) * Math.min(1, particles)),
        shred, Y + .128 + (i % 20) * .0002, (rand(kk + 5) - .5) * 70 + Math.sin(fu * 4 + kk) * 25);
    }
  });
}

// Blotches clinging to a thing, licking upward on their own cycles; the thing shows between them.
// A spot is { x, z, h, k, size }.
function cling(spots, clock, g, rel, layer) {
  spots.forEach((q, i) => {
    const period = .7 + rand(q.k + 2) * .5, u = ((clock + rand(q.k + 3) * period) / period) % 1;
    const size = q.size * (.8 + .4 * rand(q.k + 4)) * (1 - .35 * u) * g * rel;
    if (size <= .01) return;
    sprite(at(q, Math.sin(clock * 3 + q.k) * .015, u * .08, q.h), size, size * 1.15,
      Ink.withAlpha(Math.min(1, Math.sin(u * Math.PI) * 2.2) * .95), blot, layer + i * .0002, (q.k * 47 + clock * 40) % 360);
  });
}

// The black tail a burning weapon drags in flight: a band over the last TailTime of its path, widest
// at the weapon and ragged along both edges. After it arrives the tail runs into it.
function tail(key, posAt, t0, arrive, s, width, clock, seed) {
  const head = Math.min(s, arrive), back = Math.max(t0, s - TailTime);
  if (head <= back + .003) return;
  const a = posAt(head), b = posAt(back), A = at(a, 0, 0, a.h), B = at(b, 0, 0, b.h);
  const dx = A.x - B.x, dz = A.z - B.z, len = Math.hypot(dx, dz);
  if (len < .02) return;
  const nx = -dz / len, nz = dx / len, left = [], right = [], N = 12;
  for (let j = 0; j <= N; j++) {
    const u = j / N, w = width * Math.pow(1 - u, .7), x = lerp(A.x, B.x, u), z = lerp(A.z, B.z, u);
    const wl = w * (1 + .4 * Math.sin(u * 17 + clock * 30 + seed)), wr = w * (1 + .4 * Math.sin(u * 13 - clock * 27 + seed * 1.7));
    left.push({ x: x + nx * wl, z: z + nz * wl }); right.push({ x: x - nx * wr, z: z - nz * wr });
  }
  band(key, left, right, Ink.withAlpha(.8), Y + .125);
}

// Flecks a burning weapon leaves in the air along its path while it flies (t0 to t1, cut short by a
// release): each rises a little and fades within 0.3-0.45 s. posAt(time) is the weapon's { x, z, h }.
function pathFlecks(posAt, t0, t1, s, count, seed, particles) {
  const n = Math.round(count * particles);
  if (t1 <= t0) return;
  for (let i = 0; i < n; i++) {
    const k = seed + 400 + i * 13, born = t0 + (t1 - t0) * (i + rand(k)) / n, life = .3 + rand(k + 1) * .15, u = (s - born) / life;
    if (u < 0 || u >= 1) continue;
    const q = posAt(born), size = .05 + rand(k + 2) * .06;
    sprite(at(q, (rand(k + 3) - .5) * .25, (rand(k + 4) - .5) * .15, q.h + .1 + u * (.25 + rand(k + 5) * .3)), size * (1 - .4 * u),
      size * 2 * (1 - .3 * u), Ink.withAlpha(Math.min(1, Math.sin(u * Math.PI) * 1.6)), shred, Y + .13 + i * .0002, (rand(k + 6) - .5) * 60);
  }
}

// A burning weapon on the floor: its flames (already burning when it lands), a see-through pool and a
// scorch that stays. After release the flames sink and a small puff of smoke rises. lit is when the
// weapon caught, so its flames keep their rhythm through the landing.
function floorFire(key, c, roots, size, landed, lit, s, p, t, seed, light) {
  const age = s - landed;
  if (age < 0 || lit >= t.release) return;
  const rel = 1 - smooth((s - t.release) / Sink), stain = smooth(age / .25);
  sprite(c, size * 3, size * 3, Ink.withAlpha(.32 * stain * (.3 + .7 * rel)), soft, Floor + .016);
  sprite(c, size * 1.8, size * 1.8, Scorch.withAlpha(.6 * stain), puff, Floor + .018);
  sprite(c, size * 1.4, size * 1.4, Ink.withAlpha(.55 * stain * (.5 + .5 * rel)), blot, Floor + .02, seed % 360);
  if (rel > 0) smallFlames(key, roots, (s - lit) * p.speed, 1, rel, NoLean, light, Y + .05, (t.release - lit) * p.speed, p.particles);
  const since = s - t.release;
  if (since < 0 || since >= SmokeLife || t.release < landed) return;
  for (let i = 0; i < 3; i++) {
    const k = seed + 700 + i * 5, u = clamp((since - i * .05) / (SmokeLife - .2));
    if (u <= 0 || u >= 1) continue;
    const puffSize = (.3 + u * .5) * 1.3;
    sprite(at(c, (rand(k) - .5) * size, 0, .3 + u * (.8 + rand(k + 1))), puffSize, puffSize, Smoke.withAlpha(.2 * Math.sin(u * Math.PI)), puff, Y + .12 + i * .0002);
  }
}

// Flame roots for a burning weapon lying on the floor: count strands spread round c, wider east-west
// than north-south so the patch reads as flat, tallest in the middle, and three base tongues.
function floorRoots(c, count, spread, seed, scale) {
  const roots = Array.from({ length: count }, (_, j) => {
    const k = seed + j * 23, a = j * 2.399 + rand(k) * .6, r = Math.sqrt((j + .5) / count) * spread;
    return { x: c.x + Math.cos(a) * r, z: c.z + Math.sin(a) * r * .6, h: 0, k, full: (.5 + .5 * (1 - r / spread)) * (.75 + .35 * rand(k + 3)) * scale,
      hw: .04 + .035 * rand(k + 5), vein: j % 3 === 0 };
  });
  [-.5, .1, .55].forEach((u, j) => roots.push({ x: c.x + u * spread, z: c.z + (j - 1) * .04, h: 0, k: seed + 500 + j * 7, full: .28 * scale, hw: .08, base: true }));
  return roots;
}

// Blade k of the Fūma at r texture widths from its middle, turned clockwise by turn degrees, as a
// ground offset. Measured off RimArt/Fuma/Unfolded: the blades curl, 22 + 40 r + 90 k degrees.
function blade(c, turn, k, r) {
  const a = (22 + 40 * r + 90 * k - turn) * Mathf.Deg2Rad;
  return { x: c.x + Math.cos(a) * r * FumaSize, z: c.z + Math.sin(a) * r * FumaSize };
}
// Four strands along each blade, tallest mid-blade, a short tongue at its tip and a broad base tongue
// that joins them; the second strand carries the light. The flames rise well above the blades,
// because black on the dark blades does not read.
function fumaRoots(c, turn, h, seed, scale) {
  const roots = [];
  for (let k = 0; k < 4; k++) {
    [.16, .27, .38, .47, .55].forEach((r, j) => {
      const kk = seed + k * 41 + j * 7, tip = j === 4;
      roots.push({ ...blade(c, turn, k, r), h, k: kk, full: (tip ? .35 : [.7, 1, .95, .65][j] * (.8 + .35 * rand(kk + 3))) * scale,
        hw: tip ? .035 : .045 + .03 * rand(kk + 5), vein: j === 1 });
    });
    roots.push({ ...blade(c, turn, k, .32), h, k: seed + 300 + k * 13, full: .35 * scale, hw: .085, base: true });
  }
  return roots;
}

// In flight the blades spin too fast to root flames on, so the fire streams back off the whole disc
// like a comet: ten tongues round the rim, bent back along the path (d), longest at the back, and
// three base tongues over the hub. They do not turn with the blades.
function fumaTrailRoots(c, d, h, seed, scale) {
  const back = Math.atan2(-d.z, -d.x), roots = [];
  for (let i = 0; i < 10; i++) {
    const k = seed + 500 + i * 11, a = i * Math.PI / 5 + .2, rear = (1 + Math.cos(a - back)) / 2;
    roots.push({ x: c.x + Math.cos(a) * .42 * FumaSize, z: c.z + Math.sin(a) * .42 * FumaSize, h, k,
      full: (.4 + .8 * rear) * (.8 + .3 * rand(k + 3)) * scale, hw: .07 + .04 * rand(k + 5), vein: i % 2 === 0 });
  }
  [-.18, 0, .18].forEach((u, j) => roots.push({ x: c.x + d.z * u, z: c.z - d.x * u, h, k: seed + 600 + j * 7, full: .4 * scale, hw: .1, base: true }));
  return roots;
}

// The spin blur: two fading copies of the blades where they were a moment ago, under the real one.
function spinBlur(pos, turn, alpha) {
  for (let i = 1; i <= 2; i++) sprite(pos, FumaSize, FumaSize, Color.white.withAlpha(alpha * (.45 - .15 * i)), fumaGhost, projectileLayer - .001 * i, turn - 22 * i);
}

// Held kunai: three kunai hang, catch, fly on when Amenoyodomi lets go. Two hit and their pawns catch;
// the third burns on the floor until a raider walking in steps on it.
function kunaiScene(L, s, p, t, sun, strength, light) {
  L.pawns.forEach(q => figure(q, EnemyColour, 1, 0, sun, strength));
  const walker = s < L.step ? L.go(L.miss, WalkSpeed * (L.step - s)) : L.miss;
  figure(walker, EnemyColour, 1, 0, sun, strength);
  const order = L.kunai.map((_, i) => i).sort((a, b) => L.kunai[b].hold.z - L.kunai[a].hold.z);
  L.kunai.forEach((K, i) => {
    const seed = 1000 + i * 100, layer = Y + .06 + order.indexOf(i) * .006, flight = K.arrive - t.letGo;
    const e = { x: Math.cos(K.deg * Mathf.Deg2Rad), z: Math.sin(K.deg * Mathf.Deg2Rad) }, endH = K.hit ? HitLift : 0;
    const posAt = time => {
      const u = clamp((time - t.letGo) / flight);
      return { x: lerp(K.hold.x, K.end.x, u), z: lerp(K.hold.z, K.end.z, u), h: lerp(HoldLift, endH, u) };
    };
    const age = s - K.lit, lit = age >= 0 && K.lit < t.release, rel = 1 - smooth((s - t.release) / Sink);
    const clock = Math.max(0, age) * p.speed, g = lit ? smooth(age / .15) : 0;
    if (s < K.arrive) {
      // In the air: the kunai, its shadow on the floor under it, its flames and two clinging blotches.
      const q = posAt(s), moving = s > t.letGo;
      sprite({ x: q.x + sun.x * q.h, z: q.z + sun.z * q.h }, .1, .5, ShadowInk.withAlpha(strength * .8), soft, shadowLayer, 90 - K.deg);
      sprite(at(q, 0, 0, q.h), KunaiSize, KunaiSize, Color.white, kunaiMat, projectileLayer, 90 - K.deg);
      if (lit && rel > 0) {
        // Seven strands along the kunai, tallest over its middle, and two broad base tongues that
        // join them at the bottom so it reads as one flame, not a comb.
        const roots = [-.24, -.16, -.08, 0, .08, .16, .24].map((u, j) => {
          const k = seed + j * 17, env = 1 - (u / .3) ** 2;
          return { x: q.x + e.x * u, z: q.z + e.z * u, h: q.h, k, full: (.3 + .45 * env) * (.8 + .3 * rand(k + 3)) * (moving ? .7 : 1) * p.height / 2.4,
            hw: .035 + .025 * rand(k + 5), vein: j === 2 || j === 4 };
        });
        [-.1, .08].forEach((u, j) => roots.push({ x: q.x + e.x * u, z: q.z + e.z * u, h: q.h, k: seed + 90 + j * 7,
          full: .2 * p.height / 2.4, hw: .075, base: true }));
        smallFlames(`amaterasu kunai ${i}`, roots, clock, g, rel, moving ? { x: -e.x * TailLean, z: -e.z * TailLean } : NoLean,
          light, layer, (t.release - K.lit) * p.speed, p.particles);
        cling([-.05, .12].map((u, j) => ({ x: q.x + e.x * u, z: q.z + e.z * u, h: q.h, k: seed + 60 + j * 9, size: .13 })), clock, g, rel, layer - .002);
      }
    } else if (K.hit) {
      stuckKunai(K.end, K.deg, 0);
    } else {
      sprite(K.end, .62, .62, Color.white, kunaiMat, Floor + .06, 90 - K.deg);
      floorFire(`amaterasu floor kunai ${i}`, K.end, floorRoots(K.end, 9, .36, seed + 30, p.height / 2.4), .45, K.arrive, K.lit, s, p, t, seed + 50, light);
    }
    if (lit) burst(at(K.hold, 0, 0, HoldLift), age, seed, .7, 6);
    if (lit && s > t.letGo) {
      tail(`amaterasu kunai tail ${i}`, posAt, t.letGo, K.arrive, s, .07 * rel, clock, seed);
      pathFlecks(posAt, t.letGo, Math.min(K.arrive, t.release), s, 8, seed, p.particles);
    }
    // A burning kunai that hits: a burst at the chest, and the pawn catches from there outward.
    if (K.hit && K.arrive < t.release) {
      burst(at(K.end, 0, 0, .55), s - K.arrive, 1500 + i * 50, .9, 8);
      fire(`amaterasu hit ${i}`, K.end, p.width, p.height, K.arrive, s, p, t, 200 + i * 100, 'hit');
    }
  });
  fire('amaterasu walker', L.miss, p.width, p.height, L.step, s, p, t, 700, 'feet');
}

// Held Fūma: it hangs and turns slowly, catches, then is let go: full spin along its line, every
// raider on the line catches as it is cut, and it lands and lies burning.
function fumaScene(L, s, p, t, sun, strength, light) {
  const seed = 3000, flight = L.landed - t.letGo, rel = 1 - smooth((s - t.release) / Sink);
  const posAt = time => {
    const u = clamp((time - t.letGo) / flight);
    return { x: lerp(L.fumaHold.x, L.land.x, u), z: lerp(L.fumaHold.z, L.land.z, u), h: lerp(HoldLift, 0, u) };
  };
  // Clockwise degrees turned: 1% of the spin while held, full spin in flight, still once it lands.
  const turnAt = time => Math.min(time, t.letGo) * HeldSpin + Math.max(0, Math.min(time, L.landed) - t.letGo) * FumaSpin;
  const age = s - t.ignite, lit = age >= 0 && t.ignite < t.release, clock = Math.max(0, age) * p.speed, g = lit ? smooth(age / .2) : 0;
  L.line.forEach((r, i) => {
    figure(r, EnemyColour, 1, 0, sun, strength);
    if (L.cuts[i] >= t.release) return;
    burst(at(r, 0, 0, .55), s - L.cuts[i], 1600 + i * 50, .9, 8);
    fire(`amaterasu cut ${i}`, r, p.width, p.height, L.cuts[i], s, p, t, 400 + i * 100, 'hit');
  });
  if (s < L.landed) {
    const q = posAt(s), turn = turnAt(s), flying = s > t.letGo;
    sprite({ x: q.x + sun.x * q.h, z: q.z + sun.z * q.h }, 1, 1, ShadowInk.withAlpha(strength * .7), soft, shadowLayer);
    sprite(at(q, 0, 0, q.h), FumaSize, FumaSize, Color.white, fumaMat, projectileLayer, turn);
    if (flying) spinBlur(at(q, 0, 0, q.h), turn, smooth((s - t.letGo) / .06));
    if (lit && rel > 0) {
      const spots = [{ x: q.x, z: q.z }, ...(flying ? [] : [0, 1, 2, 3].map(k => blade(q, turn, k, .33)))]
        .map((b, j) => ({ ...b, h: q.h, k: seed + 60 + j * 9, size: j ? .15 : .2 }));
      cling(spots, clock, g, rel, Y + .058);
      const roots = flying ? fumaTrailRoots(q, L.d, q.h, seed, p.height / 2.4) : fumaRoots(q, turn, q.h, seed, p.height / 2.4);
      smallFlames('amaterasu fuma', roots, clock, g, rel, flying ? { x: -L.d.x * FumaLean, z: -L.d.z * FumaLean } : NoLean,
        light, Y + .06, (t.release - t.ignite) * p.speed, p.particles);
    }
    if (lit) {
      const c0 = posAt(t.ignite), turn0 = turnAt(t.ignite);
      burst(at(c0, 0, 0, HoldLift), age, seed + 90, .8, 6);
      for (let k = 0; k < 4; k++) burst(at(blade(c0, turn0, k, .38), 0, 0, HoldLift), age - .02 - k * .03, seed + k * 20, .75, 5);
    }
  } else {
    const rest = turnAt(L.landed);
    sprite(L.land, FumaSize, FumaSize, Color.white, fumaMat, Floor + .05, rest);
    floorFire('amaterasu floor fuma', L.land, fumaRoots(L.land, rest, 0, seed + 77, p.height / 2.4), .75, L.landed, t.ignite, s, p, t, seed + 90, light);
  }
  if (lit && s > t.letGo) pathFlecks(posAt, t.letGo, Math.min(L.landed, t.release), s, 14, seed, p.particles);
}

const heldScenario = p => p.scenario === Scenarios[2] || p.scenario === Scenarios[3];

export default {
  kit: 'Rinnegan', label: 'Amaterasu (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[1], options: Scenarios, group: 'Showcase' },
    distance: P('Caster distance (cells)', 6, 3, 12, .5, 'Showcase'),
    aim: P('Throw direction, held scenes (degrees, 0 east)', 0, 0, 355, 5, 'Showcase'),
    release: { label: 'Caster releases it', value: true, group: 'Showcase' },
    gazeMark: { label: 'Mangekyo mark on the target (not in the anime)', value: false, group: 'Showcase' },
    releaseAt: P('Release after ignite', 3.5, 1, 8, .25, 'Timing (s)'),
    letGo: P('Amenoyodomi lets go after ignite', 1.2, .3, 3, .05, 'Timing (s)'),
    burn: P('Burns out after (showcase)', 5, 2, 20, .5, 'Timing (s)'),
    rise: P('Catch time per part', .12, .05, .5, .01, 'Timing (s)'),
    spreadAt: P('Neighbour catches after ignite', 1.5, .5, 4, .25, 'Timing (s)'),
    height: P('Flame height (cells up)', 2.4, 1, 4, .1, 'Flame'),
    width: P('Flame half-width on a pawn (cells)', .55, .35, 1.2, .05, 'Flame'),
    speed: P('Flame speed', 1, .3, 2.5, .1, 'Flame'),
    particles: P('Flecks', 1, 0, 2, .1, 'Flame'),
    dim: P('Screen dim on ignite (0-1)', .18, 0, .7, .05, 'Flame'),
    light: { label: 'Light inside the black', value: LightNames[0], options: LightNames, group: 'Flame' },
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p), L = heldScenario(p) ? layout(p, { x: 0, z: 0 }) : null;
    const marks = [{ name: 'Gaze', t: 0 }, { name: 'Ignite', t: t.ignite }];
    if (p.scenario === Scenarios[1]) marks.push({ name: 'Spreads', t: t.ignite + p.spreadAt });
    if (L) marks.push({ name: 'Let go', t: t.letGo });
    if (p.scenario === Scenarios[2]) marks.push({ name: 'Hits', t: Math.min(L.kunai[0].arrive, L.kunai[1].arrive) }, { name: 'Steps in', t: L.step });
    if (p.scenario === Scenarios[3]) marks.push({ name: 'Cuts', t: L.cuts[0] }, { name: 'Lands', t: L.landed });
    if (p.release) marks.push({ name: 'Release', t: t.release });
    return marks.filter(m => m.t < t.end).sort((a, b) => a.t - b.t);
  },
  events(p) { return [{ t: times(p).ignite, type: 'shake', value: ShakeSize }]; },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const L = heldScenario(p) ? layout(p, o) : null, light = Lights[p.light] ?? null;
    const caster = L ? L.caster : { x: o.x - p.distance, z: o.z };
    const neighbour = { x: o.x + 1, z: o.z }, spreads = p.scenario === Scenarios[1];

    figure(caster, CasterColour, 1, 0, sun, strength);
    if (!L) figure(o, EnemyColour, 1, 0, sun, strength);
    if (spreads) figure(neighbour, Ally, 1, 0, sun, strength);

    // The gaze: a red glint on the caster's eye while it looks; after the cast that eye bleeds.
    const eye = { x: caster.x + .04, z: caster.z + .62 };
    if (s < t.ignite + .08) redStar(eye, .16 + .08 * clamp(s / Gaze), s < t.ignite ? .5 + .5 * s / Gaze : 1 - (s - t.ignite) / .08);
    if (s >= t.ignite) bleed(eye, s - t.ignite);
    if (p.gazeMark && !L) {
      if (s < t.ignite) mark('amaterasu', o, smooth(s / (Gaze * .7)), s * 6, .9);
      else if (s < t.ignite + .1) mark('amaterasu', o, 1 + (s - t.ignite) * 6, t.ignite * 6, 1 - (s - t.ignite) / .1);
    }

    // The fires. A pawn burns on its own cell; held weapons burn in the air and carry it to what they hit.
    if (p.scenario === Scenarios[2]) kunaiScene(L, s, p, t, sun, strength, light);
    else if (p.scenario === Scenarios[3]) fumaScene(L, s, p, t, sun, strength, light);
    else {
      fire('amaterasu pawn', o, p.width, p.height, t.ignite, s, p, t, 100);
      if (spreads) {
        const catches = t.ignite + p.spreadAt;
        if (catches < t.release) jump(o, neighbour, s, catches);
        fire('amaterasu spread', neighbour, p.width * .9, p.height * .9, catches, s, p, t, 300, -1);
      }
    }

    // Screen dim on ignite, so the black reads as darker than the world for a moment.
    const dimAge = s - t.ignite;
    if (p.dim > 0 && dimAge >= 0 && dimAge < DimLife) {
      const a = p.dim * (dimAge < .08 ? dimAge / .08 : 1 - smooth((dimAge - .08) / (DimLife - .08)));
      draw(MeshPool.plane10, o.x, topLayer, o.z, 400, 400, 0, new Color(0, 0, 0, a));
    }
  },
};
