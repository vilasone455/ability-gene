// Shinra Tensei (Almighty Push) v2: a new picture for the existing ability. Replaces the glass dome
// drawn by Source/RimArt/Shinra/ShinraVfxGraphics.cs ("Shinra Tensei: VFX preview" in the lab).
// Proposed 2026-09-29, the user said "yes build it"; not ported.
//
// The rule, as the game has it today (ShinraCharge.cs, ShinraCombat.cs; no change proposed here):
//   Charge up to 3 s (180 ticks, counted from the start of the gesture); power c = held / 3. Release
//   at 0.38 s of the clip. Every living pawn within 4 cells of Pain with a line from him is pushed
//   straight away from him, 3 -> 7 cells by c, divided by max(1, body size); it stops before a solid
//   obstacle and takes 8 -> 20 blunt if it hit one. Pawns pinned by Black Receiver's rods, or held by
//   Banshō, are not moved; their rods flare. Allies are pushed too. Every pushed pawn staggers 30 ticks.
//   Direct shots up to 12 -> 60 damage are turned outward for 45 ticks (0.75 s): the dome's life. Cooldown 20 s,
//   5 Echo charge, 5 s gap with Banshō Ten'in.
//   In game the push sets the pawn's position at once; the short flight drawn here is picture only
//   (the port needs a thrown-pawn drawing, as Banshō has).
//
// Order, with the default sliders (full charge, "pawns round Pain"):
//   0.00  rest
//   0.20  the gesture starts (RimArt_ShinraPush: hands come to the chest in 0.27 s)
//   0.47  hands held at the chest while charging. The ground under his feet presses into a dark
//         circle (0.4 -> 0.9 cells by charge; the anime's Konoha shot), a white dust swirl circles his
//         feet, grit creeps in from 1.5 cells, a faint pale-blue glow between the hands, and past half
//         charge cracks run out from the circle's rim
//   3.20  release asked (3 s of charge); the clip moves on from the hold marker
//   3.31  burst (clip 0.38): hands thrown wide, a white flash, camera shake 0.03 -> 0.09 by charge
//   3.31-3.61  the dome grows from Pain to exactly 4 cells in 0.30 s, easing out. It starts white
//         (Storm 4's first frame), then clears to a see-through half sphere of grey-white wind with
//         swirl lines over its surface. Faint white wind streaks run ahead of it. Behind it the
//         ground is scoured darker, with short outward scrape marks; a grey-white dust skirt rides
//         its foot. Pawns are thrown as it reaches them: raider, ally, a heavy animal that slides
//         (body size 2.4), a raider stopped by sandbags (hit), a rod-pinned raider that stays (rods
//         flare), a raider at 5.9 cells untouched. Stones fly out and land 4.3-6.2 cells out
//   3.61-3.86  the dome holds at 4 cells, its swirl lines turning and drifting up; a low lip of
//         pushed soil with clods rises at 4 cells
//   3.86-4.06  the dome swells 8 % and fades: gone at 0.75 s, when shots stop being turned
//   4.1-5.6  the dust skirt drifts out to 4.6 cells and fades
//   after the scoured circle (4 cells), its lip and clods, the scrape marks, the pressed circle and
//         the stones stay. In game they would fade over about 30 s (placeholder); the lab keeps them.
//   "shots during and after": a raider 8.7 cells east fires twice. The round that meets the dome at
//   0.30 s is turned back outward with a spark; the one that arrives at 1.00 s (dome gone) hits Pain.
//
// A first version (same day) had the anime's overhead Konoha shot as its main shape: a tan dust ring
// with 36 pointed spikes. The user: "i feel like it's sand jutsu instead of shinra tensei", and wanted
// "more depth in shinra dome like game or anime". The dome is now the main shape; the dust is a soft
// grey-white skirt.
//
// Drawing: everything is a level circle, a radial strip, a sprite or a line on the dome's surface,
// so there is no per-facing method; the gesture is the one south-facing clip the game plays (hand
// keyframes copied from Animations/RimArt_ShinraPush.json onto the kit's Pain stand-in).
// The dome is a half sphere in the projection: a point az round and el up is drawn at
// (r cos el cos az, r cos el sin az + r sin el x Lift). Its outline is one quad split at the floor
// line, the south half the floor circle and the north half stretched to sqrt(1 + Lift^2) = 1.166.
// On it: a mottled milky fill (lab/shinra-fill, Transparent; the same texture additive for the white
// burst), a soft brighter rim (lab/shinra-shell, additive), a highlight 0.95 rad up on the sun's side,
// a darker limb on the far side (5 soft sprites), a soft line round the floor circle (to the north it
// sits inside the outline, showing the height), and 16 swirl lines: even ones round at one height,
// odd ones curling from near the top down to the floor. A line's points that face the viewer
// (sin el - Lift cos el sin az > 0) are drawn bright over the pawns, the far-wall part faint under
// them, so Pain stands inside the dome. The scoured ground is one sprite (lab/shinra-scour). The lab/
// textures go into make_shinra_textures.py for the port. Dust colours follow the ground dropdown; in
// game they would come from the terrain under Pain's cell. The game's screen warp
// (MoteLargeDistortionWave) is not drawn here; the port keeps it, masked to the dome, for the 0.75 s.
// Pain, the pawns, the sandbags, the rods and the shots are stand-ins.
import { AltitudeLayer, Color, Mathf, Mesh, MeshPool, MaterialPool, ShaderDatabase } from '../js/engine.js';
import { registerLabTexture, pixels, fbm } from '../js/standins.js';
import { draw, mesh } from './lib/six-paths-solid.js';
import { P, Y, Floor, Lift, sprite, band, glow, soft, rand } from './lib/six-paths-impact.js';
import { rock, line, streak, whiteGlow, glint, EnemyColour, Ally, Ink, Skin } from './lib/goku.js';
import { beast, kick, puff, scuff, easeOut, bump } from './lib/chain-sickle.js';
import { Core, PaleBlue, Cloak, BodyZ, pain, standing, lying } from './lib/pain.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const buildingLayer = AltitudeLayer.Building.AltitudeFor();
const DustLayer = pawnLayer - .03;           // the dust ring lies under standing pawns
const White = new Color(1, 1, 1), Mist = new Color(.86, .9, .96), Shade = new Color(.32, .36, .44), Tracer = new Color(1, .9, .6), Hurt = new Color(.8, .12, .1);
const Bag = new Color(.64, .57, .42), BagDark = new Color(.36, .31, .22);

// The rule's numbers (ShinraCharge / ShinraCombat; placeholders, XML later).
const Radius = 4, FullCharge = 3, DefenseT = .75, PushLow = 3, PushHigh = 7;
// The clip: hands at the chest at 0.27 s (held there while charging), burst 0.38, end 1.35.
const Lead = .2, Hold = .27, Burst = .38, ClipEnd = 1.35;
const HandKeys = [0, .14, .27, .38, .48, .76, 1.05, 1.35];
const HandX = [.22, .15, .11, .34, .38, .36, .27, .22], HandZ = [.06, .12, .15, .16, .16, .15, .11, .06];
const BodyKeyZ = [0, -.02, -.045, .03, .04, .025, .01, 0];
// Decided look.
const FlashT = .12, DustDrift = .6, Streaks = 20, Scrapes = 22;

const Scenarios = ['pawns round Pain', 'shots during and after', 'effect only'];
const Terrains = {
  soil: { dust: new Color(.66, .56, .42), lit: new Color(.82, .73, .58), shade: new Color(.47, .38, .28), scour: new Color(.22, .15, .1) },
  sand: { dust: new Color(.86, .78, .6), lit: new Color(.96, .9, .75), shade: new Color(.66, .56, .4), scour: new Color(.5, .4, .27) },
  snow: { dust: new Color(.9, .93, .97), lit: new Color(1, 1, 1), shade: new Color(.68, .74, .83), scour: new Color(.27, .23, .21) },
};

// ---- textures (lab only; port to make_shinra_textures.py) ------------------------------------------
// A soft ring: brightest at 0.84 of the radius, fading inward over about 0.26 and out to nothing by 1.
registerLabTexture('lab/shinra-shell', () => pixels(128, (u, v) => {
  const r = Math.hypot(u - .5, v - .5) * 2;
  if (r >= .99) return [1, 1, 1, 0];
  const peak = Math.exp(-Math.pow((r - .84) / .26, 2)), edge = clamp((.99 - r) / .12);
  return [1, 1, 1, Math.min(1, (peak * .85 + .08 * r) * edge)];
}));
// Scoured ground: a disc broken by noise, with a ragged edge.
registerLabTexture('lab/shinra-scour', () => pixels(256, (u, v) => {
  const r = Math.hypot(u - .5, v - .5) * 2, n = fbm(u * 10, v * 10, 311, 4, 10), m = fbm(u * 5, v * 5, 97, 3, 5);
  const edge = .9 + (m - .5) * .12;
  if (r >= edge) return [1, 1, 1, 0];
  const patch = fbm(u * 3, v * 3, 53, 2, 3);
  return [1, 1, 1, clamp(.2 + .9 * n * n + .35 * (patch - .5)) * clamp((edge - r) / .04)];
}));
// The dome's body: filled to 0.85 of the radius, then soft to nothing, mottled like Storm 4's wind.
registerLabTexture('lab/shinra-fill', () => pixels(128, (u, v) => {
  const r = Math.hypot(u - .5, v - .5) * 2, n = fbm(u * 6, v * 6, 211, 3, 6);
  if (r >= .99) return [1, 1, 1, 0];
  return [1, 1, 1, (.55 + .45 * n) * (.7 + .3 * r * r) * clamp((.99 - r) / .14)];
}));
const shellMat = MaterialPool.MatFrom('lab/shinra-shell', ShaderDatabase.MoteGlow);
const fillMat = MaterialPool.MatFrom('lab/shinra-fill', ShaderDatabase.Transparent);
const fillGlowMat = MaterialPool.MatFrom('lab/shinra-fill', ShaderDatabase.MoteGlow);
const scourMat = MaterialPool.MatFrom('lab/shinra-scour', ShaderDatabase.Transparent);
const disc = new Mesh('shinra disc');
{ const v = [0, 0], tri = []; for (let i = 0; i < 40; i++) { const a = i / 40 * TAU; v.push(Math.cos(a), Math.sin(a)); tri.push(0, 1 + i, 1 + (i + 1) % 40); } disc.setFlat(v, tri); }
// The shell: a unit quad split at the floor line, the north half 1.166 tall so the ring bulges north.
const DomeK = Math.sqrt(1 + Lift * Lift);
const shellMesh = new Mesh('shinra shell');
shellMesh.setFlat([-1, -1, 1, -1, 1, 0, -1, 0, -1, 0, 1, 0, 1, DomeK, -1, DomeK], [0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7]);
shellMesh.uv = new Float32Array([0, 0, 1, 0, 1, .5, 0, .5, 0, .5, 1, .5, 1, 1, 0, 1]);

// ---- timing ---------------------------------------------------------------------------------------
function times(p) {
  const hold = Math.max(p.charge, Hold), power = clamp(p.charge / FullCharge);
  const R = Lead + hold + (Burst - Hold);
  return { hold, power, R, end: R + Math.max(3.2, .8 + p.dustFade + .9) };
}
function clipTime(s, t) {
  const k = s - Lead;
  if (k <= 0) return 0;
  if (k < t.hold) return Math.min(Hold, k);
  return Math.min(ClipEnd, Hold + (k - t.hold));
}
function key(times, values, ct) {
  for (let i = 1; i < times.length; i++) if (ct <= times[i]) return lerp(values[i - 1], values[i], smooth((ct - times[i - 1]) / (times[i] - times[i - 1])));
  return values[values.length - 1];
}
// The front's radius e seconds after the burst, and the inverse: when it reaches d.
const front = (e, p) => e <= 0 ? 0 : Radius * easeOut(e / p.wave);
const reaches = (d, p) => p.wave * (1 - Math.cbrt(Math.max(0, 1 - d / Radius)));

// ---- the ground: pressed circle, scoured disc, scrape lines, lip -----------------------------------
// The ground pressed down under Pain's feet: a dark circle with a ragged rim of pushed-up dust.
const wob = (a, seed) => .5 + .2 * Math.sin(a * 5 + rand(seed) * TAU) + .17 * Math.sin(a * 13 + rand(seed + 1) * TAU) + .13 * Math.sin(a * 29 + rand(seed + 2) * TAU);
function pressed(o, rp, dark, alpha, col) {
  draw(disc, o.x, Floor + .006, o.z, rp, rp, 0, Core.withAlpha(dark * alpha));
  const n = 48, a0 = [], a1 = [];
  for (let i = 0; i <= n; i++) {
    const a = i / n * TAU, c = Math.cos(a), s = Math.sin(a), w = wob(a, 91);
    a0.push({ x: o.x + c * rp * .96, z: o.z + s * rp * .96 }); a1.push({ x: o.x + c * (rp + .03 + .12 * w * w), z: o.z + s * (rp + .03 + .12 * w * w) });
  }
  band('shinra pressed rim', a0, a1, col.lit.withAlpha(.4 * alpha), Floor + .0055);
}
// Cracks that run out from the pressed circle's rim once the charge passes half.
function rimCracks(o, rp, amount, col) {
  if (amount <= 0) return;
  for (let i = 0; i < 7; i++) {
    const a = (i + rand(i * 3 + 901)) / 7 * TAU, len = (.2 + .35 * rand(i * 3 + 902)) * amount, kink = (rand(i * 3 + 903) - .5) * .5;
    const p0 = rp + .02, pts = [0, .5, 1].map(u => { const ang = a + kink * u * u, r = p0 + len * u; return { x: o.x + Math.cos(ang) * r, z: o.z + Math.sin(ang) * r }; });
    line(`shinra rim crack ${i}`, pts, .045, col.scour.withAlpha(.75), undefined, Floor + .0058, 'end');
  }
}
// The edge of the scoured circle at 4 cells: a ragged dark inner slope, a broken low ridge of pushed
// soil, and clods of earth along it. k 0..1 raises it.
function lip(o, k, col) {
  if (k <= 0) return;
  const n = 160, inA = [], inB = [], outA = [], outB = [];
  for (let i = 0; i <= n; i++) {
    const a = i / n * TAU, c = Math.cos(a), s = Math.sin(a);
    const r0 = Radius - (.1 + .22 * wob(a, 51)) * k, r1 = Radius + (.04 * wob(a, 71) - .02) * k, r2 = r1 + (.02 + .2 * Math.pow(wob(a, 61), 2)) * k;
    inA.push({ x: o.x + c * r0, z: o.z + s * r0 }); inB.push({ x: o.x + c * r1, z: o.z + s * r1 });
    outA.push({ x: o.x + c * r1, z: o.z + s * r1 }); outB.push({ x: o.x + c * r2, z: o.z + s * r2 });
  }
  band('shinra lip shade', inA, inB, col.scour.withAlpha(.4 * k), Floor + .004);
  band('shinra lip top', outA, outB, col.lit.withAlpha(.32 * k), Floor + .0042);
  for (let i = 0; i < 64; i++) {
    const a = (i + rand(i * 5 + 701)) / 64 * TAU, r = Radius + (rand(i * 5 + 702) - .35) * .34, size = (.07 + .13 * rand(i * 5 + 703)) * k;
    rock({ x: o.x + Math.cos(a) * r, z: o.z + Math.sin(a) * r }, size, rand(i * 5 + 704) * 360, k, i * 2 + 1, Floor + .02);
  }
}
// Scrape marks on the scoured floor: short broken grooves pointing outward, at scattered radii (one
// long line per angle from the middle read as the spokes of a wheel), and grit left behind.
function scrapes(o, F, col) {
  for (let i = 0; i < Scrapes; i++) {
    const a = rand(i * 7 + 1) * TAU, r0 = 1.1 + rand(i * 7 + 2) * 2.2, len = .45 + 1.05 * rand(i * 7 + 3);
    const c = Math.cos(a), s = Math.sin(a), w = .035 + .045 * rand(i * 7 + 4), bend = (rand(i * 7 + 5) - .5) * .12;
    [[0, .42], [.55, 1]].forEach(([u0, u1], d) => {
      const a0 = r0 + len * u0, a1 = Math.min(Radius - .2, r0 + len * u1, F);
      if (a1 <= a0 + .05) return;
      const pts = [0, .5, 1].map(u => { const r = lerp(a0, a1, u), q = (u0 + (u1 - u0) * u); return { x: o.x + c * r - s * bend * bump(q), z: o.z + s * r + c * bend * bump(q) }; });
      line(`shinra scrape ${i} ${d}`, pts, w, col.scour.withAlpha(.38), undefined, Floor + .003, 'both');
      line(`shinra scrape lit ${i} ${d}`, pts.map(q => ({ x: q.x - s * .045, z: q.z + c * .045 })), w * .6, col.lit.withAlpha(.22), undefined, Floor + .0031, 'both');
    });
  }
  for (let i = 0; i < 46; i++) {
    const a = rand(i * 3 + 801) * TAU, r = 1 + 2.8 * Math.sqrt(rand(i * 3 + 802));
    if (r > F) continue;
    rock({ x: o.x + Math.cos(a) * r, z: o.z + Math.sin(a) * r }, .045 + .05 * rand(i * 3 + 803), rand(i * 3 + 804) * 360, .85, i, Floor + .019);
  }
}

// ---- the dust: a grey-white skirt where the dome meets the floor --------------------------------------
function dustSkirt(o, e, p, c, rs, col) {
  const A = clamp(e / .05) * (1 - smooth((e - .5) / p.dustFade));
  if (A <= 0 || rs <= .1) return;
  const grey = Color.Lerp(col.dust, White, .5), pale = Color.Lerp(col.lit, White, .7), late = Math.max(0, e - p.wave);
  for (let i = 0; i < 48; i++) {
    const a = (i + rand(i * 9 + 201)) / 48 * TAU, r = rs - .25 * rand(i * 9 + 202) + DustDrift * easeOut(late / 2) * (.5 + rand(i * 9 + 206));
    const h = (.1 + .5 * rand(i * 9 + 203)) * clamp(e / .9), size = (.5 + .6 * rand(i * 9 + 204)) * lerp(.7, 1.1, c) * (1 + .6 * clamp(late / 1.5));
    sprite({ x: o.x + Math.cos(a) * r, z: o.z + Math.sin(a) * r + h * Lift }, size, size * .75, (rand(i * 9 + 205) < .5 ? grey : pale).withAlpha(.3 * A), puff, Y + .004 + i * .0001);
  }
}

// ---- air: flash, wind streaks, the dome ---------------------------------------------------------------
function windStreaks(o, e, p, alpha) {
  if (alpha <= 0) return;
  for (let i = 0; i < Streaks; i++) {
    const a = rand(i * 13 + 301) * TAU, len = .7 + 1.1 * rand(i * 13 + 302), h = 1.4 * rand(i * 13 + 303) * rand(i * 13 + 304);
    const late = Math.max(0, e - p.wave), head = (e < p.wave ? front(e, p) : Radius + late * 7) + .3 * rand(i * 13 + 305);
    const tail = Math.max(0, head - len * clamp(e / .08)), f = alpha * (.45 + .55 * rand(i * 13 + 306)) * (1 - smooth(late / .28));
    if (f <= 0 || head - tail < .05) continue;
    const c = Math.cos(a), s = Math.sin(a), lift = h * Lift;
    streak(`shinra wind ${i}`, { x: o.x + c * tail, z: o.z + s * tail + lift }, { x: o.x + c * head, z: o.z + s * head + lift }, .028 + .025 * rand(i * 13 + 307), White.withAlpha(.38 * f), whiteGlow, Y + .03, 6);
  }
}
// A point on the half sphere of radius rs round Pain's cell, az round from east, el up from the floor,
// drawn in the projection (height goes north by Lift). It faces the viewer when its normal points
// against the view direction (0, 1, -Lift): sin el - Lift cos el sin az > 0.
function domeAt(o, rs, az, el) {
  const ce = Math.cos(el);
  return { x: o.x + rs * ce * Math.cos(az), z: o.z + rs * ce * Math.sin(az) + rs * Math.sin(el) * Lift, front: Math.sin(el) - Lift * ce * Math.sin(az) > 0 };
}
// The dome: a see-through half sphere of grey-white wind (Storm 4's Almighty Push). Milky body with
// a mottled texture, a soft brighter rim, a highlight high on the sun's side, and swirl lines that
// follow the surface: the ones on the far wall are faint and drawn under the pawns, the near ones over
// them, so Pain stands inside it. flash 0..1 whitens the whole dome at the burst.
function dome(o, e, p, rs, alpha, flash, sun) {
  if (alpha <= 0 || rs <= .1) return;
  const a = alpha * p.dome;
  draw(shellMesh, o.x, Y + .02, o.z, rs, rs, 0, Mist.withAlpha(Math.min(1, .16 * a + .35 * flash)), fillMat);
  if (flash > 0) draw(shellMesh, o.x, Y + .0205, o.z, rs, rs, 0, White.withAlpha(.55 * flash), fillGlowMat);
  draw(shellMesh, o.x, Y + .021, o.z, rs * 1.03, rs * 1.03, 0, PaleBlue.withAlpha(.32 * a), shellMat);
  // Light: a highlight high on the sun's side, the limb away from the sun a shade darker.
  const lightAz = Math.atan2(-sun.z, -sun.x), hi = domeAt(o, rs, lightAz, .95);
  sprite(hi, rs * .75, rs * .55, White.withAlpha(.4 * a), glow, Y + .022);
  for (let i = -2; i <= 2; i++) {
    const q = domeAt(o, rs * .9, lightAz + Math.PI + i * .42, .3 + .12 * Math.abs(i));
    sprite(q, rs * .55, rs * .38, Shade.withAlpha(.13 * a), soft, Y + .0212);
  }
  // Where the dome stands on the floor: a soft line round the floor circle. To the north it sits
  // inside the outline, and the gap between them is the dome's height.
  const base = [];
  for (let j = 0; j <= 64; j++) { const az = j / 64 * TAU; base.push({ x: o.x + Math.cos(az) * rs, z: o.z + Math.sin(az) * rs }); }
  line('shinra dome base glow', base, .4, White.withAlpha(.14 * a), whiteGlow, Y + .0215, 'none');
  const n = Math.round(p.swirls);
  for (let k = 0; k < n; k++) {
    const spin = (k % 2 ? 1 : -1) * (1.6 + 1.2 * rand(k * 19 + 1)), az0 = rand(k * 19 + 2) * TAU + spin * e;
    // Even strokes swirl round at one height; odd ones curl from near the top down to the floor.
    const sweep = k % 2 === 1, span = sweep ? .45 + .5 * rand(k * 19 + 4) : .7 + .9 * rand(k * 19 + 4);
    const el0 = sweep ? .85 + .1 * rand(k * 19 + 3) : .15 + .9 * rand(k * 19 + 3) + .3 * clamp(e / DefenseT);
    const rise = sweep ? -(1.1 + .3 * rand(k * 19 + 5)) : (rand(k * 19 + 5) - .5) * .7;
    const runs = [];
    let run = null;
    for (let j = 0; j <= 18; j++) {
      const u = j / 18, q = domeAt(o, rs, az0 + span * u, Math.max(.03, Math.min(1.5, el0 + rise * (u - .5))));
      if (!run || run.front !== q.front) { run = { front: q.front, pts: run ? [run.pts[run.pts.length - 1]] : [] }; runs.push(run); }
      run.pts.push(q);
    }
    const f = a * (.55 + .45 * rand(k * 19 + 6));
    runs.forEach((r, i) => {
      if (r.pts.length < 3) return;
      const key = `shinra swirl ${k} ${i}`;
      if (r.front) {
        line(`${key} glow`, r.pts, .16, White.withAlpha(.1 * f), whiteGlow, Y + .03, 'both');
        line(`${key} core`, r.pts, .045, White.withAlpha(.42 * f), whiteGlow, Y + .031, 'both');
      } else {
        line(`${key} glow`, r.pts, .1, Mist.withAlpha(.05 * f), whiteGlow, pawnLayer - .012, 'both');
        line(`${key} core`, r.pts, .03, Mist.withAlpha(.2 * f), whiteGlow, pawnLayer - .011, 'both');
      }
    });
  }
}

// ---- charge ------------------------------------------------------------------------------------------
function chargePicture(o, s, cNow, col, sun) {
  if (cNow <= 0) return;
  const rp = lerp(.4, .9, cNow);
  pressed(o, rp, .45, clamp(cNow * 4), col);
  rimCracks(o, rp, clamp((cNow - .5) * 2), col);
  for (let i = 0; i < 10; i++) {
    const a = s * 8.5 + i / 10 * TAU, r = rp + .12 + .15 * rand(i + 501);
    sprite({ x: o.x + Math.cos(a) * r, z: o.z + Math.sin(a) * r * .9 + .08 }, .32, .24, Color.Lerp(col.lit, White, .6).withAlpha(.4 * cNow), puff, Y + .001);
  }
  for (let k = 0; k < 2; k++) {
    const pts = [];
    for (let j = 0; j <= 8; j++) { const a = s * 8.5 + k * Math.PI + j * .11; pts.push({ x: o.x + Math.cos(a) * (rp + .2), z: o.z + Math.sin(a) * (rp + .2) * .9 + .1 }); }
    line(`shinra charge swirl ${k}`, pts, .05, White.withAlpha(.45 * cNow), whiteGlow, Y + .002, 'both');
  }
  for (let i = 0; i < 14; i++) {
    const u = Mathf.Repeat(s * 1.1 + rand(i + 520), 1), a = rand(i + 540) * TAU, r = lerp(1.6, rp, Math.pow(u, 1.5));
    draw(disc, o.x + Math.cos(a) * r, Floor + .008, o.z + Math.sin(a) * r, .03, .03, 0, col.scour.withAlpha(.8 * cNow * bump(u)));
  }
}

// ---- Pain with the clip's hands -------------------------------------------------------------------
function painGesture(o, ct, glowAmount, sun, strength) {
  const bz = key(HandKeys, BodyKeyZ, ct), g = { x: o.x, z: o.z + bz };
  pain(g, sun, strength);
  const hx = key(HandKeys, HandX, ct), hz = key(HandKeys, HandZ, ct);
  for (const side of [-1, 1]) {
    const shoulder = { x: g.x + side * .13, z: g.z + BodyZ + .14 }, hand = { x: o.x + side * hx, z: o.z + BodyZ + hz };
    line(`shinra sleeve ${side}`, [shoulder, hand], .09, Cloak, undefined, pawnLayer + .01, 'none');
    draw(disc, hand.x, pawnLayer + .011, hand.z, .055, .055, 0, Skin);
  }
  if (glowAmount > 0) sprite({ x: o.x, z: o.z + BodyZ + .16 }, .7, .6, PaleBlue.withAlpha(.35 * glowAmount), glow, pawnLayer + .012);
}

// ---- pushed pawns ---------------------------------------------------------------------------------
const Cast = [
  { x: 1.7, z: 1.1, kind: 'raider' },
  { x: -2.5, z: -1.5, kind: 'raider', wall: 5 },   // sandbags across its line at 5 cells
  { x: -1.1, z: 2.3, kind: 'ally' },
  { x: .7, z: -3.2, kind: 'heavy', body: 2.4 },
  { x: 2.9, z: -2.3, kind: 'pinned' },
  { x: 5.6, z: 1.8, kind: 'raider' },              // outside the 4 cells: not moved
];
function sandbags(o, q) {
  const d = Math.hypot(q.x, q.z), ux = q.x / d, uz = q.z / d, px = -uz, pz = ux;
  for (let k = -1; k <= 1; k++) {
    const c = { x: o.x + ux * (q.wall + .15) + px * k * .62, z: o.z + uz * (q.wall + .2) + pz * k * .62 }, deg = Math.atan2(uz, ux) / Mathf.Deg2Rad;
    draw(disc, c.x + .04, shadowLayer, c.z - .05, .24, .36, -deg, Ink.withAlpha(.35));
    draw(disc, c.x, buildingLayer, c.z, .22, .33, -deg, BagDark);
    draw(disc, c.x - .02, buildingLayer + .001, c.z + .05, .17, .27, -deg, Bag);
  }
}
function rods(key, g, sun, strength, flare) {
  for (let k = 0; k < 3; k++) {
    const base = { x: g.x - .12 + k * .12, z: g.z + .05 + (k % 2) * .06 }, h = .62 + .1 * k, top = { x: base.x + (k - 1) * .05, z: base.z + h * Lift };
    line(`${key} rod shadow ${k}`, [base, { x: base.x + sun.x * h, z: base.z + sun.z * h }], .05, Ink.withAlpha(strength), undefined, shadowLayer, 'end');
    line(`${key} rod ${k}`, [base, top], .05, Core, undefined, pawnLayer + .004, 'none');
    if (flare > 0) glint(`${key} flare ${k}`, top, .35, flare, PaleBlue, 45);
  }
}
function pushed(o, e, p, c, sun, strength) {
  Cast.forEach((q, i) => {
    const d0 = Math.hypot(q.x, q.z), ux = q.x / d0, uz = q.z / d0, start = { x: o.x + q.x, z: o.z + q.z }, k = `shinra pawn ${i}`;
    if (q.wall) sandbags(o, q);
    const hitAt = reaches(d0, p), inside = d0 <= Radius;
    if (q.kind === 'pinned') {
      lying(k, start, EnemyColour, { x: -ux, z: -uz }, sun, strength);
      rods(k, start, sun, strength, inside && e > hitAt ? bump(clamp((e - hitAt) / .35)) : 0);
      return;
    }
    const want = inside ? lerp(PushLow, PushHigh, c) / Math.max(1, q.body ?? 1) : 0;
    const travel = q.wall ? Math.min(want, q.wall - .55 - d0) : want, hit = q.wall && want > q.wall - .55 - d0;
    const fly = .1 + .05 * travel, u = travel > 0 && e > hitAt ? clamp((e - hitAt) / fly) : 0;
    const along = d0 + travel * (1 - (1 - u) * (1 - u)), g = { x: o.x + ux * along, z: o.z + uz * along };
    const colour = q.kind === 'ally' ? Ally : EnemyColour;
    if (q.kind === 'heavy') {
      if (u > 0) scuff(`${k} drag`, start, { x: o.x + ux * (d0 + travel), z: o.z + uz * (d0 + travel) }, 1 - (1 - u) * (1 - u), .45);
      beast(g, q.body, sun, strength);
      if (u > 0 && u < 1) kick(g, e, 1, i * 10);
      return;
    }
    if (u > 0 && u < 1) {
      const h = .3 * Math.min(1, travel / 4) * bump(u);
      lying(k, { x: g.x, z: g.z + h * Lift }, colour, { x: ux, z: uz }, sun, strength);
    } else {
      const since = e - hitAt - fly, sway = u >= 1 && since < .5 ? Math.sin(since * 30) * .05 * (1 - since / .5) : 0;
      standing({ x: g.x + sway, z: g.z }, colour, sun, strength);
      if (u >= 1 && since < .4) kick(g, since, 1 - since / .4, i * 10);
      if (hit && u >= 1 && since < .5) {
        const at = { x: g.x + ux * .35, z: g.z + uz * .35 + .3 };
        sprite(at, .9, .7, Hurt.withAlpha(.5 * (1 - since / .5)), glow, Y + .05);
        glint(`${k} hit`, at, .5, 1 - since / .5, White, 20);
      }
    }
  });
}

// ---- stones -----------------------------------------------------------------------------------------
function stones(o, e, p, c, sun) {
  const n = Math.round(p.debris * lerp(.35, 1, c));
  for (let i = 0; i < n; i++) {
    const a = rand(i * 11 + 601) * TAU, r0 = .5 + rand(i * 11 + 602) * 2.8, r1 = Math.max(r0 + 1.2, lerp(4.3, 6.2, rand(i * 11 + 603)));
    const e0 = reaches(r0, p), dur = .35 + .35 * rand(i * 11 + 604), H = (.3 + .9 * rand(i * 11 + 605)) * lerp(.6, 1.2, c);
    const size = .16 + .18 * rand(i * 11 + 606), u = (e - e0) / dur, ca = Math.cos(a), sa = Math.sin(a);
    if (u <= 0) continue;
    if (u >= 1) {
      rock({ x: o.x + ca * r1, z: o.z + sa * r1 }, size, rand(i * 11 + 607) * 360, 1, i, Floor + .02);
      const since = (u - 1) * dur;
      if (since < .3) sprite({ x: o.x + ca * r1, z: o.z + sa * r1 + .05 }, .4 + since, .3 + since, (i % 2 ? Terrains[p.terrain].lit : Terrains[p.terrain].dust).withAlpha(.5 * (1 - since / .3)), puff, Y + .003);
      continue;
    }
    const r = lerp(r0, r1, u), h = 4 * H * u * (1 - u), x = o.x + ca * r, z = o.z + sa * r;
    sprite({ x: x + sun.x * h, z: z + sun.z * h }, size * 1.2, size * .9, Ink.withAlpha(.35), soft, shadowLayer);
    rock({ x, z: z + h * Lift }, size, rand(i * 11 + 607) * 360 + u * 540 * (i % 2 ? 1 : -1), 1, i, Y + .01);
  }
}

// ---- shots ------------------------------------------------------------------------------------------
const Shooter = { x: 8.7, z: 1.3 }, ShotSpeed = 30;
function shots(o, e, p, sun, strength) {
  standing({ x: o.x + Shooter.x, z: o.z + Shooter.z }, EnemyColour, sun, strength);
  const chest = { x: 0, z: BodyZ + .15 }, dx = chest.x - Shooter.x, dz = chest.z - Shooter.z, L = Math.hypot(dx, dz), ux = dx / L, uz = dz / L;
  // Where the round's line meets the 4-cell circle round Pain's cell.
  const b = Shooter.x * ux + Shooter.z * uz, cc = Shooter.x * Shooter.x + Shooter.z * Shooter.z - Radius * Radius;
  const meet = -b - Math.sqrt(Math.max(0, b * b - cc)), contact = { x: Shooter.x + ux * meet, z: Shooter.z + uz * meet };
  const tracer = (key, pos, dir, alpha) => streak(key, { x: o.x + pos.x - dir.x * .6, z: o.z + pos.z - dir.z * .6 }, { x: o.x + pos.x, z: o.z + pos.z }, .08, Tracer.withAlpha(alpha), whiteGlow, Y + .08, 4);
  [[.3, true], [1.0, false]].forEach(([arrive, turned], i) => {
    const fired = arrive - meet / ShotSpeed, k = `shinra shot ${i}`;
    if (e < fired) return;
    const muzzle = e - fired;
    if (muzzle < .08) glint(`${k} muzzle`, { x: o.x + Shooter.x + ux * .45, z: o.z + Shooter.z + uz * .45 + .3 }, .3, 1 - muzzle / .08, Tracer, 0);
    if (e < arrive) { tracer(k, { x: Shooter.x + ux * muzzle * ShotSpeed, z: Shooter.z + uz * muzzle * ShotSpeed + .3 }, { x: ux, z: uz }, 1); return; }
    const after = e - arrive;
    if (turned) {
      const r = Math.hypot(contact.x, contact.z), ox = contact.x / r, oz = contact.z / r;
      if (after < .25) glint(`${k} spark`, { x: o.x + contact.x, z: o.z + contact.z + .3 }, .55, 1 - after / .25, White, 30);
      if (after < .45) tracer(k, { x: contact.x + ox * after * ShotSpeed, z: contact.z + oz * after * ShotSpeed + .3 }, { x: ox, z: oz }, 1 - after / .45);
    } else {
      const left = (L - meet) / ShotSpeed;
      if (after < left) tracer(k, { x: contact.x + ux * after * ShotSpeed, z: contact.z + uz * after * ShotSpeed + .3 }, { x: ux, z: uz }, 1);
      else if (after - left < .35) {
        const f = 1 - (after - left) / .35, at = { x: o.x + chest.x + .08, z: o.z + chest.z + .2 };
        sprite(at, .5, .4, Hurt.withAlpha(.7 * f), glow, Y + .06);
        glint(`${k} hit`, at, .3, f, Tracer, 10);
      }
    }
  });
}

export default {
  kit: 'Pain',
  label: 'Shinra Tensei (sketch)',
  compareWith: 'Shinra Tensei: VFX preview',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[0], options: Scenarios, group: 'Mechanic' },
    charge: P('Charge held (s)', 3, .1, 3, .1, 'Mechanic'),
    terrain: { label: 'Ground', value: 'soil', options: Object.keys(Terrains), group: 'Look' },
    wave: P('Wave reaches 4 cells (s)', .3, .12, .8, .02, 'Timing (s)'),
    dustFade: P('Dust fades over (s)', 1.8, .6, 4, .1, 'Timing (s)'),
    debris: P('Stones at full charge', 18, 0, 40, 1, 'Shape'),
    dome: P('Dome strength', 1, 0, 1.5, .05, 'Look'),
    swirls: P('Dome swirl lines', 16, 0, 32, 1, 'Look'),
    streaks: P('White wind streaks', .7, 0, 1, .05, 'Look'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [
      { name: 'Rest', t: 0 }, { name: 'Hands to chest', t: Lead }, { name: 'Hold (charging)', t: Lead + Hold },
      { name: 'Burst', t: t.R }, { name: 'Dome at 4 cells', t: t.R + p.wave }, { name: 'Dome gone (shots turned until)', t: t.R + DefenseT },
      { name: 'Dust gone', t: t.R + .5 + p.dustFade },
    ];
  },
  events(p) {
    const t = times(p);
    return [
      { t: Lead, type: 'sound', def: 'AG_ShinraCharge' },
      { t: t.R, type: 'shake', value: lerp(.03, .09, t.power) },
      { t: t.R, type: 'sound', def: 'AG_ShinraRelease' },
    ];
  },

  draw(s, p, { origin, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const t = times(p), c = t.power, e = s - t.R, col = Terrains[p.terrain] ?? Terrains.soil;
    const o = { x: origin.x, z: origin.z }, ct = clipTime(s, t);
    // Charge counted from the start of the gesture, frozen when the release is asked.
    const cNow = clamp(Math.min(s - Lead, p.charge) / FullCharge);

    if (e < 0) {
      chargePicture(o, s, s > Lead ? Math.max(cNow, .05) : 0, col, sun);
    } else {
      // The ground: what stays.
      const F = front(e, p);
      sprite(o, F * 2 / .9, F * 2 / .9, col.scour.withAlpha(.55), scourMat, Floor + .001);   // the texture's edge is at 0.9
      scrapes(o, F, col);
      lip(o, smooth((e - p.wave * .7) / .2), col);
      pressed(o, lerp(.4, .9, c), .3, 1, col);
      rimCracks(o, lerp(.4, .9, c), clamp((c - .5) * 2), col);

      // The dust skirt where the dome meets the floor, blown out and fading.
      dustSkirt(o, e, p, c, F, col);
      stones(o, e, p, c, sun);

      // The air: flash, wind streaks, and the dome, held for the 0.75 s shots are turned, then gone.
      if (e < FlashT) {
        const f = (1 - e / FlashT) ** 2;
        sprite({ x: o.x, z: o.z + .35 }, 1.6 + 2 * c, 1.4 + 1.8 * c, White.withAlpha(.9 * f), glow, Y + .06);
      }
      windStreaks(o, e, p, p.streaks * lerp(.6, 1, c));
      const fade = smooth((e - (DefenseT - .2)) / .2);
      dome(o, e, p, F * (1 + .08 * fade), clamp(e / .03) * (1 - fade), e < .25 ? (1 - e / .25) ** 2 : 0, sun);
    }

    painGesture(o, ct, e < 0 && s > Lead + Hold ? cNow : e >= 0 && e < .1 ? 1 - e / .1 : 0, sun, strength);
    if (p.scenario === Scenarios[0]) pushed(o, Math.max(-1, e), p, c, sun, strength);
    else if (p.scenario === Scenarios[1]) shots(o, e, p, sun, strength);
  },
};
