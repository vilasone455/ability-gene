// Shinra Tensei (Almighty Push) v2: a new picture for the existing ability. Replaced the glass dome
// (ShinraVfxGraphics.cs, removed). Proposed 2026-09-29, the user said "yes build it". Ported the same day
// to Source/RimArt/Shinra/ShinraDome.cs (timing) and ShinraDomeGraphics.cs (drawing); the lab's
// "Shinra Tensei: VFX preview", "VFX 1.5 s hold" and "VFX tap" are its recordings (no pawn, soil).
//
// Two versions (the user, 2026-09-29: "new shinra tensei will have two version, one click version
// and charge version"), picked by the Version dropdown; both draw the same dome.
//   One click (proposed, placeholders): one order, no target; clip RimArt_ShinraTap (0.7 s: the west
//   hand sweeps from the hip out and up to shoulder height, bursts at 0.22 s with a small lean back,
//   holds to 0.4, comes down). 2.5 cells, pushes 4 cells (divided by body size), 10 blunt on a wall
//   hit, shots up to 20 damage turned for 0.45 s (the dome's life), cooldown 8 s shared with the
//   charged version, 3 Echo charge. Picture: a short press under the feet in the last 0.12 s, the
//   dome pops out in the same 0.16 s, lines pour for 0.2 s, no white-out, no floating, a flash at the
//   palm, a 2.5-cell crater. A raider in melee next to Pain shows what it is for.
//   Charged: the rule below, clip RimArt_ShinraCharge. Its hold is no longer a still frame: the arms
//   open and step up with the size (down and out = 2 cells, a level T from 1 s = 3 cells, a Y above
//   the shoulders from 2 s, up on the toes = 4 cells; the anime's Konoha charge and Mobile's spread
//   arms), trembling harder with the charge; each open palm holds a pale-blue light and a chakra glow
//   stands round the body (Storm 4, szCziDnCD-o 1:05), growing and flaring at the steps. A first try
//   pressed the hands together under the chin; the user: "holding position is look too cute", and no
//   source does it. In the clip the hold is a 1 s segment standing for power 0 to 1, with one release
//   segment per size (see make_shinra_anim.py).
//
// The rule, as the game has it today (ShinraCharge.cs, ShinraCombat.cs):
//   Charge up to 3 s (180 ticks, counted from the start of the gesture); power c = held / 3. Release
//   at 0.38 s of the clip. Every living pawn within 4 cells of Pain with a line from him is pushed
//   straight away from him, 3 -> 7 cells by c, divided by max(1, body size); it stops before a solid
//   obstacle and takes 8 -> 20 blunt if it hit one. Pawns pinned by Black Receiver's rods, or held by
//   Banshō, are not moved; their rods flare. Allies are pushed too. Every pushed pawn staggers 30
//   ticks. Direct shots up to 12 -> 60 damage are turned outward for 45 ticks (0.75 s): the dome's
//   life. Cooldown 20 s, 5 Echo charge, 5 s gap with Banshō Ten'in.
//   In game the push sets the pawn's position at once; the short flight drawn here is picture only
//   (the port needs a thrown-pawn drawing, as Banshō has).
//   Proposed change, drawn here (2026-09-29, the user: "let try do", after Naruto Mobile's Shinra was
//   found to have three sizes by how long it is held, its cooldown growing with the size): the area
//   is 2 cells after up to 1 s of charge, 3 cells after 1-2 s, 4 cells after 2-3 s (full keeps
//   today's 4). Cooldown 12 / 16 / 20 s by the size reached (the user, 2026-09-29: "use 12/16/20
//   cooldowns"; follows Mobile; placeholders for XML, not drawn). The one click's 8 s stays.
//   Replaced the same day by one tap/hold button (the user agreed): a hold let go before 1 s gives the
//   one-click version (2.5 cells, 8 s), 1 s gives 3 cells (16 s), 2 s gives 4 cells (20 s); the 2-cell
//   size is gone. In game since then (ShinraTuning, Command_TapHold), and drawn here since the float.
//   Pain floats only for the 4-cell size (the user, 2026-09-29, "yes build it"): the anime's floating
//   charge is the big one over Konoha, and in his ground fights he pushes standing. From 2 s of charge
//   he lifts off to 0.3 cells in 0.15 s (with the arms' step to the Y) and hovers; at the release he goes up to 0.45 (Mobile) and comes
//   down as the dome fades. Tap, quick and 3-cell casts stay on the ground. Picture only: a small lift
//   reads as a hover, not flight, and he can still be hit. RimArt_ShinraCharge carries the same heights.
//
// Order, with the default sliders (full charge, "pawns round Pain"):
//   0.00  rest
//   0.20  the gesture starts (RimArt_ShinraPush: hands come to the chest in 0.27 s)
//   0.47  hands held at the chest while charging. A soft blue ring on the ground shows the size the
//         dome will have if released now: 2 cells, stepping out with a pulse to 3 at 1.2 s and 4 at
//         2.2 s. The ground under his feet presses into a dark circle (0.4 -> 0.9 cells by charge;
//         the anime's Konoha shot), a white dust swirl circles his feet, grit creeps in from 1.5
//         cells, a faint pale-blue glow between the hands, and past half charge cracks run out from
//         the circle's rim
//   3.20  release asked (3 s of charge); the clip moves on from the hold marker
//   3.31  burst (clip 0.38): hands thrown wide, a white flash, camera shake 0.03 -> 0.09 by charge
//   3.31-3.47  the screen whites out for 0.12 s (Mobile, KQQE2-wx_yw 0:03 and 0:17) while the dome
//         pops out to its size in 0.16 s (Mobile: at full size within one 0.19 s storyboard frame).
//         Pain rises 0.45 cells over 0.25 s. The dome starts white, then clears to a see-through
//         half sphere in Naruto Mobile's ice blue, with a bright blue rim, cloudy wind low inside it
//         racing outward along the floor, and a bright ring where it stands on the floor; 12 lines
//         pour over it from the top down to the floor as it fills. Two soft ripples roll from the
//         top down to the floor (starting 0.02 and 0.24 s, 0.4 s each). Faint white wind streaks run
//         ahead of it. Behind it the ground is scoured darker, with short outward scrape marks; a
//         grey-white dust skirt rides its foot. Pawns are thrown as it reaches them: raider, ally, a
//         heavy animal that slides (body size 2.4), a raider stopped by sandbags (hit), a rod-pinned
//         raider that stays (rods flare), a raider at 5.9 cells untouched. Stones fly out and land
//         4.3-6.2 cells out
//   3.61-3.86  the dome overshoots about 0.2 cells and settles at 4. It does not turn (Naruto
//         Mobile, -D6cQGfYKu8 0:38-0:47: the lines flash in new places each frame): each line now
//         flashes along a short path somewhere new, runs about 0.26 s and is gone; half run down the
//         surface, half are swooshes wrapping just outside it (Mobile 0:40); the rim shimmers.
//         Leaves and grass bits fly out low with the stones (Mobile 0:39). A low lip of pushed soil
//         with clods rises at 4 cells
//   3.86-4.06  the dome swells 10 % and fades (Mobile 0:47): gone at 0.75 s, when shots stop being
//         turned; the floor ring outlasts it by about 0.15 s; Pain comes down by 4.4
//   4.1-5.6  the dust skirt drifts out to 4.6 cells and fades
//   after the scoured circle (4 cells), its lip and clods, the scrape marks, the pressed circle and
//         the stones stay. In game they would fade over about 30 s (placeholder); the lab keeps
//         them.
//   "shots during and after": a raider 8.7 cells east fires twice. The round that meets the dome at
//   0.30 s is turned back outward with a spark; the one that arrives at 1.00 s (dome gone) hits
//         Pain.
//
// A first version (same day) had the anime's overhead Konoha shot as its main shape: a tan dust ring
// with 36 pointed spikes. The user: "i feel like it's sand jutsu instead of shinra tensei", and wanted
// "more depth in shinra dome like game or anime". The dome is now the main shape; the dust is a soft
// grey-white skirt. Its lines first turned round it; the user: motion "can better than rotate", the
// sources do not turn. Now they pour, flash and ripple. Then "close to naruto mobile much as
// possible": colours sampled from Mobile's frames (bright 205/227/242, haze 140/158/180), its thick
// rim, the bright wind low inside, the floor ring, swooshes outside the dome, leaves.
//
// Drawing: everything is a level circle, a radial strip, a sprite or a line on the dome's surface,
// so there is no per-facing method; the gesture is the one south-facing clip the game plays (hand
// keyframes copied from Animations/RimArt_ShinraPush.json onto the kit's Pain stand-in).
// The dome is a half sphere in the projection: a point az round and el up is drawn at
// (r cos el cos az, r cos el sin az + r sin el x Lift). Its outline is one quad split at the floor
// line, the south half the floor circle and the north half stretched to sqrt(1 + Lift^2) = 1.166.
// On it: a mottled blue-grey fill (lab/shinra-fill, Transparent; the same texture additive for the
// white burst), a bright blue rim (lab/shinra-shell, additive, two layers), the wind low inside (a
// cloudy ring on the floor, lab/shinra-floor, additive, drawn under the pawns, and 18 wisps that race
// outward along it, picked by hash per generation), a bright ring round the floor circle (to the
// north it sits inside the outline, showing the height), a highlight 0.95 rad up on the sun's side,
// a darker limb on the far side (5 soft sprites), 12 surface lines (a pour from the top to the floor,
// then flashes: head and tail run along a path picked by hash per line and generation, so nothing
// turns) and 2 ripple rings (level circles on the surface, sliding from the top to the floor).
// A line's points that face the viewer
// (sin el - Lift cos el sin az > 0) are drawn bright over the pawns, the far-wall part faint under
// them, so Pain stands inside the dome. The scoured ground is one sprite (lab/shinra-scour). The lab/
// textures go into make_shinra_textures.py for the port. Dust colours follow the ground dropdown; in
// game they would come from the terrain under Pain's cell. The game's screen warp
// (MoteLargeDistortionWave) is not drawn here; the port keeps it, masked to the dome, for the 0.75 s.
// Pain, the pawns, the sandbags, the rods and the shots are stand-ins.
import { AltitudeLayer, Color, Mathf, Mesh, MeshPool, MaterialPool, Overlay, ShaderDatabase } from '../js/engine.js';
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
const White = new Color(1, 1, 1), Mist = new Color(.86, .9, .96), Shade = new Color(.32, .36, .44),
  IceBright = new Color(.8, .89, .95), SkyBlue = new Color(.42, .6, .92), Haze = new Color(.55, .62, .72), Leaf = new Color(.36, .55, .2), LeafDry = new Color(.55, .45, .22), Tracer = new Color(1, .9, .6), Hurt = new Color(.8, .12, .1);
const Bag = new Color(.64, .57, .42), BagDark = new Color(.36, .31, .22);

// The rule's numbers (ShinraCharge / ShinraCombat; placeholders, XML later).
const FullCharge = 3, PushLow = 3, PushHigh = 7;
// The one-click version (proposed 2026-09-29, placeholders): no charge, 2.5 cells, pushes 4 cells
// (divided by body size), 10 blunt on a wall hit, turns shots up to 20 damage for 0.45 s, cooldown 8 s
// shared with the charged version, 3 Echo charge. Drawn with the same dome, smaller and quicker, and
// its own clip (RimArt_ShinraTap).
const Versions = ['charged', 'one click'];
const TapRadius = 2.5, TapPush = 4, TapDefense = .45, TapPour = .2, TapPower = .35;
// Per frame, set at the top of draw() from the version: how long the dome holds (the shots-turned
// window) and how long its lines pour.
let DefenseT = .75, Pour = .3;
// Naruto Mobile's three sizes by how long the button is held, as agreed for the tap/hold button: a hold
// let go before 1 s is the quick version (2.5 cells), 1-2 s gives 3 cells, 2-3 s gives 4 cells.
const Sizes = [TapRadius, 3, 4], SizeStep = 1;
const sizeFor = held => Sizes[Math.min(Sizes.length - 1, Math.floor(held / SizeStep))];
// The area of the cast being drawn: set from the charge at the top of draw(), so every helper below
// reads the same value for that frame.
let Radius = 4;
// Pain lifts off for the 4-cell size only: from TopAt s of charge he rises to HoverH in RiseT and hovers
// with a slow bob; at the release he goes up to FloatH (Mobile 0:43-0:46), reached 0.15 s after the
// burst, and comes down as the dome fades (LandAt s after the burst, over LandT). make_shinra_anim.py
// puts the same heights into RimArt_ShinraCharge.
const FloatH = .45, HoverH = .3, RiseT = .15, HoverBob = .015, TopAt = 2 * SizeStep, LandAt = .6, LandT = .3;
function hoverAt(held) {
  const since = held - TopAt;
  if (since <= 0) return 0;
  const k = clamp(since / RiseT);
  return HoverH * easeOut(k) + HoverBob * Math.sin(since * 5) * k;
}
// His height at scene second s, e seconds after the burst.
function liftAt(s, t, p, e) {
  if (t.tap || p.charge < TopAt) return 0;
  const letGo = Lead + p.charge;
  if (s < letGo) return s > Lead ? hoverAt(s - Lead) : 0;
  const up = lerp(hoverAt(p.charge), FloatH, smooth((s - letGo) / (t.R - letGo + .15)));
  return up * (1 - smooth((e - LandAt) / LandT));
}
// The clip: hands at the chest at 0.27 s (held there while charging), burst 0.38, end 1.35.
const Lead = .2, Hold = .27, Burst = .38, ClipEnd = 1.35;
const HandKeys = [0, .14, .27, .38, .48, .76, 1.05, 1.35];
const HandX = [.22, .15, .11, .34, .38, .36, .27, .22], HandZ = [.06, .12, .15, .16, .16, .15, .11, .06];
const BodyKeyZ = [0, -.02, -.045, .03, .04, .025, .01, 0];
// The charged hold (RimArt_ShinraCharge), from the anime's Konoha charge (arms down and out, then a
// level T, then a raised Y, An1ZrG0mbf4 0:15-0:30) and Mobile's spread arms: the hands open from the
// sides to down-and-out while the gesture starts, and the arms step up with the size, so the pose
// shows the size: down and out, slowly rising (2 cells), a level T from 1 s (3 cells), a Y above the
// shoulders from 2 s, up on the toes (4 cells). Each step takes 0.15 s with a small overshoot; a
// tremble grows with the charge (0.3 -> 2 hundredths of a cell). The release snaps from the pose to
// the T push in 0.11 s. Hand offsets are (out from the middle, up from the chest).
const OpenKeys = [0, .14, .27], OpenX = [.22, .26, .30], OpenZ = [.06, .04, .03], OpenBody = [0, -.01, -.02];
const HoldTiers = [
  { span: [.30, .33], lift: [.03, .09], body: -.02 },   // 2 cells: down and out
  { span: [.40, .40], lift: [.16, .19], body: -.02 },   // 3 cells: level T
  { span: [.36, .37], lift: [.28, .31], body: .03 },    // 4 cells: Y, up on the toes
];
const StepT = .15, StepOver = .03, TrembleLow = .003, TrembleHigh = .02, StepPulse = .25;
function holdPose(held) {
  const i = Math.min(HoldTiers.length - 1, Math.floor(held / SizeStep)), T = HoldTiers[i], w = clamp((held - i * SizeStep) / SizeStep);
  let span = lerp(T.span[0], T.span[1], w), lift = lerp(T.lift[0], T.lift[1], w), body = T.body;
  const since = held - i * SizeStep;
  if (i > 0 && since < StepT + .1) {
    const P = HoldTiers[i - 1], k = smooth(since / StepT), over = StepOver * Math.sin(Math.PI * clamp(since / (StepT + .1)));
    span = lerp(P.span[1], span, k); lift = lerp(P.lift[1], lift, k) + over; body = lerp(P.body, body, k);
  }
  return { span, lift, body };
}
// RimArt_ShinraTap (0.7 s, facing south): the west hand sweeps from the hip out and up to shoulder
// height, palm open, and bursts at 0.22 s with a small lean back; the east hand stays low.
const TapBurst = .22, TapEnd = .7;
const TapKeys = [0, .12, .22, .4, .7];
const TapCastX = [.22, .3, .37, .36, .22], TapCastZ = [.06, .2, .23, .21, .06];
const TapOtherX = [.22, .21, .2, .2, .22], TapOtherZ = [.06, .08, .09, .09, .06];
const TapBodyZ = [0, -.01, .03, .02, 0];
// Decided look.
const FlashT = .12, DustDrift = .6, Streaks = 20, Scrapes = 22;
// The dome's motion: lines pour down for 0.3 s, then flash for about 0.26 s each; two ripples.
const FlashLife = .26, Ripples = [.02, .24], RippleLife = .4, Wisps = 18, WhiteoutT = .12;

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
// The wind low inside the dome, seen from above: a cloudy ring brightest at 0.78 of the radius.
registerLabTexture('lab/shinra-floor', () => pixels(128, (u, v) => {
  const r = Math.hypot(u - .5, v - .5) * 2, n = fbm(u * 7, v * 7, 419, 3, 7);
  if (r >= .99) return [1, 1, 1, 0];
  return [1, 1, 1, (Math.exp(-Math.pow((r - .78) / .22, 2)) * .9 + .12) * (.5 + .5 * n) * clamp((.99 - r) / .1)];
}));
const floorMat = MaterialPool.MatFrom('lab/shinra-floor', ShaderDatabase.MoteGlow);
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
  if (p.version === Versions[1]) { const R = Lead + TapBurst; return { tap: true, hold: 0, power: TapPower, R, end: R + Math.max(2.6, .6 + p.dustFade + .6) }; }
  const hold = Math.max(p.charge, Hold), power = clamp(p.charge / FullCharge);
  const R = Lead + hold + (Burst - Hold);
  return { tap: false, hold, power, R, end: R + Math.max(3.2, .8 + p.dustFade + .9) };
}
function clipTime(s, t) {
  const k = s - Lead;
  if (k <= 0) return 0;
  if (t.tap) return Math.min(TapEnd, k);
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
  const clods = Math.round(64 * Radius / 4);
  for (let i = 0; i < clods; i++) {
    const a = (i + rand(i * 5 + 701)) / clods * TAU, r = Radius + (rand(i * 5 + 702) - .35) * .34, size = (.07 + .13 * rand(i * 5 + 703)) * k;
    rock({ x: o.x + Math.cos(a) * r, z: o.z + Math.sin(a) * r }, size, rand(i * 5 + 704) * 360, k, i * 2 + 1, Floor + .02);
  }
}
// Scrape marks on the scoured floor: short broken grooves pointing outward, at scattered radii (one
// long line per angle from the middle read as the spokes of a wheel), and grit left behind.
function scrapes(o, F, col) {
  for (let i = 0; i < Scrapes; i++) {
    const k = Radius / 4, a = rand(i * 7 + 1) * TAU, r0 = (1.1 + rand(i * 7 + 2) * 2.2) * k, len = (.45 + 1.05 * rand(i * 7 + 3)) * k;
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
    const a = rand(i * 3 + 801) * TAU, r = (1 + 2.8 * Math.sqrt(rand(i * 3 + 802))) * Radius / 4;
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
    const a = (i + rand(i * 9 + 201)) / 48 * TAU, r = rs - .25 * rand(i * 9 + 202) + DustDrift * Radius / 4 * easeOut(late / 2) * (.5 + rand(i * 9 + 206));
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
// Naruto Mobile's dome is full of bright wind low down (60-74 % of its lower half is near white). From
// above that is a cloudy ring on the floor inside the dome, and wisps that race outward along it.
function floorWind(o, e, rs, a) {
  if (a <= 0 || rs <= .1) return;
  sprite(o, rs * 2, rs * 2, SkyBlue.withAlpha(.3 * a), floorMat, pawnLayer - .016);
  for (let k = 0; k < Wisps; k++) {
    const period = .4 * (.8 + .4 * rand(k * 31 + 1)), x = e / period + rand(k * 31 + 2), g = Math.floor(x), u = x - g, seed = k * 71 + g * 17;
    const ang = rand(seed + 1) * TAU + (rand(seed + 2) - .5) * .6 * u;          // a slight curl, never a turn
    const r = rs * lerp(.35 + .3 * rand(seed + 3), .98, easeOut(u)), h = .1 + .35 * rand(seed + 4);
    const len = (.7 + .8 * rand(seed + 5)) * (.6 + .6 * u) * rs / 4, wid = .22 + .15 * rand(seed + 6);
    const dir = ang + Math.PI / 2 * .75 * (rand(seed + 7) < .5 ? 1 : -1);        // along the ring, leaning outward
    sprite({ x: o.x + Math.cos(ang) * r, z: o.z + Math.sin(ang) * r + h * Lift }, len, wid, White.withAlpha(.24 * a * bump(u)), puff, pawnLayer - .015, -dir / Mathf.Deg2Rad);
  }
}
// Where the dome stands on the floor: a bright soft ring round the floor circle (Mobile 0:47). To the
// north it sits inside the outline, and the gap between them is the dome's height. It outlasts the
// dome by about 0.15 s.
function floorRing(o, rs, a) {
  if (a <= 0 || rs <= .1) return;
  const base = [];
  for (let j = 0; j <= 64; j++) { const az = j / 64 * TAU; base.push({ x: o.x + Math.cos(az) * rs, z: o.z + Math.sin(az) * rs }); }
  line('shinra floor ring glow', base, .6, SkyBlue.withAlpha(.26 * a), whiteGlow, Y + .0215, 'none');
  line('shinra floor ring core', base, .12, IceBright.withAlpha(.14 * a), whiteGlow, Y + .0216, 'none');
}
// The dome: a see-through half sphere of grey-white wind (Storm 4's Almighty Push). Milky body with
// a mottled texture, a soft brighter rim, a highlight high on the sun's side, and swirl lines that
// follow the surface: the ones on the far wall are faint and drawn under the pawns, the near ones over
// them, so Pain stands inside it. flash 0..1 whitens the whole dome at the burst.
function dome(o, e, p, rs, alpha, flash, sun) {
  if (alpha <= 0 || rs <= .1) return;
  const a = alpha * p.dome;
  draw(shellMesh, o.x, Y + .02, o.z, rs, rs, 0, Haze.withAlpha(Math.min(1, .14 * a + .35 * flash)), fillMat);
  if (flash > 0) draw(shellMesh, o.x, Y + .0205, o.z, rs, rs, 0, White.withAlpha(.85 * flash), fillGlowMat);
  const shimmer = 1 + .08 * Math.sin(e * 71) + .05 * Math.sin(e * 113 + 1.3);
  draw(shellMesh, o.x, Y + .021, o.z, rs * 1.03, rs * 1.03, 0, SkyBlue.withAlpha(.42 * a * shimmer), shellMat);
  draw(shellMesh, o.x, Y + .0211, o.z, rs * .99, rs * .99, 0, IceBright.withAlpha(.1 * a * shimmer), shellMat);
  // Light: a highlight high on the sun's side, the limb away from the sun a shade darker.
  const lightAz = Math.atan2(-sun.z, -sun.x), hi = domeAt(o, rs, lightAz, .95);
  sprite(hi, rs * .75, rs * .55, White.withAlpha(.4 * a), glow, Y + .022);
  for (let i = -2; i <= 2; i++) {
    const q = domeAt(o, rs * .9, lightAz + Math.PI + i * .42, .3 + .12 * Math.abs(i));
    sprite(q, rs * .55, rs * .38, Shade.withAlpha(.13 * a), soft, Y + .0212);
  }
  // Two pressure ripples roll from the top down to the floor, widening as they go (Naruto Mobile's
  // dome does not turn; its lines flash along the surface in new places).
  Ripples.forEach((start, i) => {
    const u = (e - start) / RippleLife;
    if (u <= 0 || u >= 1) return;
    const el = Math.PI / 2 * (1 - easeOut(u)) + .04, pts = [];
    for (let j = 0; j <= 48; j++) pts.push(domeAt(o, rs, j / 48 * TAU, el));
    surfaceLine(`shinra ripple ${i}`, pts, .5, .12, a * bump(u) * 1.3, false, .15);   // soft band, almost no core
  });
  // Surface lines. For the first 0.3 s they pour from the top down to the floor as the dome fills;
  // after that each one flashes along a short path somewhere new, runs, and is gone.
  const n = Math.round(p.swirls);
  for (let k = 0; k < n; k++) {
    let u, az0, el0, dAz, dEl, out = 1;
    if (e < Pour) {
      u = e / Pour;
      az0 = (k + rand(k * 23 + 1)) / n * TAU; el0 = 1.45; dAz = (rand(k * 23 + 2) - .5) * .9; dEl = -(1.3 + .1 * rand(k * 23 + 3));
    } else {
      const period = FlashLife * (.8 + .4 * rand(k * 23 + 4)), x = (e - Pour) / period + rand(k * 23 + 5), g = Math.floor(x), seed = k * 97 + g * 13;
      u = x - g;
      const down = rand(seed + 1) < .5;
      az0 = rand(seed + 2) * TAU;
      el0 = down ? .75 + .6 * rand(seed + 3) : .15 + .65 * rand(seed + 3);
      dAz = down ? (rand(seed + 4) - .5) * .7 : (rand(seed + 4) < .5 ? -1 : 1) * (.9 + .8 * rand(seed + 5));
      dEl = down ? -(el0 - .05 - .1 * rand(seed + 6)) : (rand(seed + 6) - .5) * .25;
      out = down ? 1 : 1.04 + .08 * rand(seed + 7);      // a swoosh wraps just outside the dome (Mobile 0:40)
    }
    // The head runs out along the path, the tail follows it and they meet at the end.
    const head = easeOut(clamp(u / .55)), tail = smooth(clamp((u - .3) / .7));
    if (head - tail < .03) continue;
    const pts = [];
    for (let j = 0; j <= 16; j++) {
      const w = lerp(tail, head, j / 16);
      pts.push(domeAt(o, rs * out, az0 + dAz * w, Math.max(.03, Math.min(1.52, el0 + dEl * w))));
    }
    surfaceLine(`shinra swirl ${k}`, pts, .16, .045, a * (.55 + .45 * rand(k * 23 + 7)) * clamp(u / .08) * (1 - tail * .6), true);
  }
}
// A line on the dome's surface: the part facing the viewer bright over the pawns, the far-wall part
// faint under them. taper false keeps its width (a closed ripple ring).
function surfaceLine(key, pts, glowW, coreW, f, taper, coreK = 1) {
  if (f <= 0) return;
  const runs = [];
  let run = null;
  for (const q of pts) {
    if (!run || run.front !== q.front) { run = { front: q.front, pts: run ? [run.pts[run.pts.length - 1]] : [] }; runs.push(run); }
    run.pts.push(q);
  }
  runs.forEach((r, i) => {
    if (r.pts.length < 3) return;
    const t = taper ? 'both' : 'none';
    if (r.front) {
      line(`${key} ${i} glow`, r.pts, glowW, White.withAlpha(.1 * f), whiteGlow, Y + .03, t);
      line(`${key} ${i} core`, r.pts, coreW, White.withAlpha(.42 * f * coreK), whiteGlow, Y + .031, t);
    } else {
      line(`${key} ${i} glow`, r.pts, glowW * .65, Mist.withAlpha(.05 * f), whiteGlow, pawnLayer - .012, t);
      line(`${key} ${i} core`, r.pts, coreW * .7, Mist.withAlpha(.2 * f * coreK), whiteGlow, pawnLayer - .011, t);
    }
  });
}

// ---- charge ------------------------------------------------------------------------------------------
// While charging, a soft ring on the ground at the size the dome will have if released now; it steps
// out with a pulse at 1 s and 2 s.
function sizeRing(o, held) {
  const r = sizeFor(held), step = Math.floor(Math.min(held, (Sizes.length - 1) * SizeStep) / SizeStep) * SizeStep;
  const since = held - step, pulse = step > 0 && since < .35 ? 1 - since / .35 : 0, rr = r * (1 + .06 * pulse), pts = [];
  for (let j = 0; j <= 72; j++) { const a = j / 72 * TAU; pts.push({ x: o.x + Math.cos(a) * rr, z: o.z + Math.sin(a) * rr }); }
  const f = clamp(held / .25) * (.2 + .35 * pulse);
  line('shinra size ring glow', pts, .32, SkyBlue.withAlpha(f), whiteGlow, Floor + .007, 'none');
  line('shinra size ring core', pts, .05, IceBright.withAlpha(f * .5), whiteGlow, Floor + .0071, 'none');
  if (pulse > 0) sprite(o, rr * 2.1, rr * 2.1, SkyBlue.withAlpha(.12 * pulse), floorMat, Floor + .0069);
}
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
// The pose at clip time ct: body offset and the two hands, relative to Pain's cell. For the charged
// version the hold is alive: see HoldTiers and holdPose().
function poseAt(ct, s, t, p) {
  if (t.tap) {
    return { bx: 0, bz: key(TapKeys, TapBodyZ, ct), glowAt: -1, aura: 0,
      hands: [{ x: -key(TapKeys, TapCastX, ct), z: key(TapKeys, TapCastZ, ct) }, { x: key(TapKeys, TapOtherX, ct), z: key(TapKeys, TapOtherZ, ct) }] };
  }
  let span = key(HandKeys, HandX, ct), lift = key(HandKeys, HandZ, ct), bz = key(HandKeys, BodyKeyZ, ct), bx = 0, hx = 0, hz = 0, glowK = 0, aura = 0;
  if (ct < Hold) { span = key(OpenKeys, OpenX, ct); lift = key(OpenKeys, OpenZ, ct); bz = key(OpenKeys, OpenBody, ct); }
  else if (ct < Burst && s - Lead > 0) {
    // Held (ct stays at Hold), then the release: from the hold pose to the burst's T push.
    const held = Math.min(s - Lead, p.charge), c = clamp(held / FullCharge), H = holdPose(held);
    const r = smooth((ct - Hold) / (Burst - Hold)), k = 1 - r;
    span = lerp(H.span, HandX[3], r); lift = lerp(H.lift, HandZ[3], r); bz = lerp(H.body, BodyKeyZ[3], r);
    const step = Math.floor(Math.min(held, (Sizes.length - 1) * SizeStep) / SizeStep) * SizeStep, since = held - step;
    const pulse = step > 0 && since < StepPulse ? 1 - since / StepPulse : 0, A = lerp(TrembleLow, TrembleHigh, c) * k;
    hx = Math.sin(s * 44) * A; hz = Math.sin(s * 57 + 1) * A * .7; bx = Math.sin(s * 39 + 2) * A * .4;
    glowK = (.3 + .5 * c + .5 * pulse) * k;
    aura = (c + .6 * pulse) * k;
  }
  return { bx, bz, glowAt: glowK, aura, palms: glowK > 0, hands: [{ x: -span + hx, z: lift + hz }, { x: span - hx, z: lift - hz }] };
}
function painGesture(o, pose, glowAmount, sun, strength, lift = 0, palmFlash = 0) {
  const g = { x: o.x + pose.bx, z: o.z + pose.bz + lift * Lift };
  // His shadow stays on the ground and shrinks a little as he rises.
  const k = 1 - .35 * lift / FloatH;
  sprite({ x: o.x + sun.x * (.45 + lift), z: o.z + sun.z * (.45 + lift) }, .85 * k, .4 * k, Ink.withAlpha(strength * k), soft, shadowLayer);
  pain(g, sun, 0);
  pose.hands.forEach((h, i) => {
    const side = h.x < 0 ? -1 : 1, shoulder = { x: g.x + side * .13, z: g.z + BodyZ + .14 }, hand = { x: o.x + h.x, z: o.z + BodyZ + h.z + lift * Lift };
    line(`shinra sleeve ${i}`, [shoulder, hand], .09, Cloak, undefined, pawnLayer + .01, 'none');
    draw(disc, hand.x, pawnLayer + .011, hand.z, .055, .055, 0, Skin);
    if (i === 0 && palmFlash > 0) sprite(hand, .7, .6, IceBright.withAlpha(.8 * palmFlash), glow, pawnLayer + .013);
  });
  // While charging: a ball of pale-blue light gathers between the palms and a chakra glow stands round
  // the body (Storm 4, szCziDnCD-o 1:05), both growing with the charge and flaring at each size step.
  if (pose.aura > 0) {
    sprite({ x: g.x, z: g.z + .3 }, lerp(.7, 1.5, Math.min(1, pose.aura)), lerp(.9, 1.8, Math.min(1, pose.aura)), SkyBlue.withAlpha(.3 * pose.aura), glow, pawnLayer - .005);
  }
  // While charging each open palm holds a pale-blue light that grows with the charge; at the burst
  // (charged version) a flash between the arms.
  if (pose.palms) {
    const size = lerp(.3, .6, Math.min(1, pose.aura || 0));
    pose.hands.forEach((h, i) => {
      const at = { x: o.x + h.x, z: o.z + BodyZ + h.z + lift * Lift };
      sprite(at, size, size * .85, PaleBlue.withAlpha(.55 * pose.glowAt), glow, pawnLayer + .012 + i * .0002);
      sprite(at, size * .35, size * .3, White.withAlpha(.6 * pose.glowAt), glow, pawnLayer + .0125 + i * .0002);
    });
  }
  if (glowAmount > 0) sprite({ x: o.x, z: o.z + BodyZ + .16 + pose.bz }, .7, .6, PaleBlue.withAlpha(.35 * glowAmount), glow, pawnLayer + .012);
}

// ---- pushed pawns ---------------------------------------------------------------------------------
const Cast = [
  { x: -.9, z: .7, kind: 'raider' },               // in melee, right next to Pain
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
    const want = inside ? (p.version === Versions[1] || p.charge < SizeStep ? TapPush : lerp(PushLow, PushHigh, c)) / Math.max(1, q.body ?? 1) : 0;
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
    const a = rand(i * 11 + 601) * TAU, r0 = (.5 + rand(i * 11 + 602) * 2.8) * Radius / 4, r1 = Math.max(r0 + 1.2, Radius + lerp(.3, 2.2, rand(i * 11 + 603)));
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
  // Leaves and grass bits blown out low, fluttering, and left on the ground (Mobile 0:39).
  for (let i = 0, m = Math.round(8 + 8 * c); i < m; i++) {
    const a = rand(i * 11 + 901) * TAU, r0 = (1 + rand(i * 11 + 902) * 2.6) * Radius / 4, r1 = r0 + 2.4 + 2 * rand(i * 11 + 903);
    const u = (e - reaches(r0, p)) / (.6 + .4 * rand(i * 11 + 904));
    if (u <= 0) continue;
    const k = Math.min(1, u), r = lerp(r0, r1, easeOut(k)), h = (.4 + .6 * rand(i * 11 + 905)) * bump(k), sway = Math.sin(e * 19 + i * 2.3) * .08 * (1 - k);
    const ca = Math.cos(a), sa = Math.sin(a), x = o.x + ca * r - sa * sway, z = o.z + sa * r + ca * sway;
    const deg = u < 1 ? (e * 420 + i * 47) % 360 : rand(i * 11 + 906) * 360;
    draw(disc, x, u < 1 ? Y + .012 : Floor + .021, z + h * Lift, .075, .035, deg, rand(i * 11 + 907) < .6 ? Leaf : LeafDry);
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
    version: { label: 'Version', value: Versions[0], options: Versions, group: 'Mechanic' },
    scenario: { label: 'Scenario', value: Scenarios[0], options: Scenarios, group: 'Mechanic' },
    charge: P('Charge held (s, charged version)', 3, .1, 3, .1, 'Mechanic'),
    terrain: { label: 'Ground', value: 'soil', options: Object.keys(Terrains), group: 'Look' },
    wave: P('Dome forms in (s)', .16, .06, .6, .02, 'Timing (s)'),
    whiteout: { label: 'White-out at the burst (Mobile)', value: true, group: 'Look' },
    dustFade: P('Dust fades over (s)', 1.8, .6, 4, .1, 'Timing (s)'),
    debris: P('Stones at full charge', 18, 0, 40, 1, 'Shape'),
    dome: P('Dome strength', 1, 0, 1.5, .05, 'Look'),
    swirls: P('Dome lines', 12, 0, 32, 1, 'Look'),
    streaks: P('White wind streaks', .7, 0, 1, .05, 'Look'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    if (t.tap) return [{ name: 'Rest', t: 0 }, { name: 'Hand out', t: Lead }, { name: 'Burst', t: t.R }, { name: 'Dome full size', t: t.R + p.wave },
      { name: 'Dome gone (shots turned until)', t: t.R + TapDefense }, { name: 'Dust gone', t: t.R + .5 + p.dustFade }];
    return [
      { name: 'Rest', t: 0 }, { name: 'Hands to chest', t: Lead }, { name: 'Hold (charging)', t: Lead + Hold },
      ...Sizes.slice(1).map((r, i) => ({ name: `Size ${r} cells`, t: Lead + (i + 1) * SizeStep })).filter(q => q.t - Lead <= p.charge),
      { name: 'Burst', t: t.R }, { name: 'Dome full size', t: t.R + p.wave }, { name: 'Dome gone (shots turned until)', t: t.R + DefenseT },
      { name: 'Dust gone', t: t.R + .5 + p.dustFade },
    ];
  },
  events(p) {
    const t = times(p);
    if (t.tap) return [{ t: t.R, type: 'shake', value: .04 }, { t: t.R, type: 'sound', def: 'AG_ShinraRelease' }];
    return [
      { t: Lead, type: 'sound', def: 'AG_ShinraCharge' },
      { t: t.R, type: 'shake', value: lerp(.03, .09, t.power) },
      { t: t.R, type: 'sound', def: 'AG_ShinraRelease' },
    ];
  },

  draw(s, p, { origin, scene }) {
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const t = times(p), c = t.power, e = s - t.R, col = Terrains[p.terrain] ?? Terrains.soil;
    // A hold let go before the first size is the quick version: its size, shots window, pour, no white-out.
    const quick = t.tap || p.charge < SizeStep;
    Radius = t.tap ? TapRadius : sizeFor(p.charge);
    DefenseT = quick ? TapDefense : .75; Pour = quick ? TapPour : .3;
    const o = { x: origin.x, z: origin.z }, ct = clipTime(s, t);
    // Charge counted from the start of the gesture, frozen when the release is asked.
    const cNow = clamp(Math.min(s - Lead, p.charge) / FullCharge);

    if (e < 0) {
      if (t.tap) { const k = clamp((s - (t.R - .12)) / .12); if (k > 0) pressed(o, .45 * k, .35, k, col); }
      else {
        chargePicture(o, s, s > Lead ? Math.max(cNow, .05) : 0, col, sun);
        if (s > Lead) sizeRing(o, Math.min(s - Lead, p.charge));
      }
    } else {
      // The ground: what stays.
      const F = front(e, p);
      sprite(o, F * 2 / .9, F * 2 / .9, col.scour.withAlpha(.55), scourMat, Floor + .001);   // the texture's edge is at 0.9
      scrapes(o, F, col);
      lip(o, smooth((e - p.wave * .7) / .2), col);
      pressed(o, t.tap ? .45 : lerp(.4, .9, c), .3, 1, col);
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
      const x = Math.max(0, e - p.wave), spring = .05 * Math.exp(-7 * x) * Math.sin(14 * x);   // overshoots about 0.2 cells, settles
      const rsD = F * (1 + spring + .1 * fade), domeA = clamp(e / .03) * (1 - fade);
      floorWind(o, e, rsD, domeA * p.dome);
      floorRing(o, rsD, clamp(e / .03) * (1 - smooth((e - (DefenseT - .1)) / .25)) * p.dome);
      dome(o, e, p, rsD, domeA, e < .2 ? (1 - e / .2) ** 2 : 0, sun);
      // Mobile whites out the whole screen for a moment at the burst (0:03, 0:17 of KQQE2-wx_yw).
      if (!quick && p.whiteout && e < WhiteoutT) Overlay.Fill(0, 0, 1, 1, White.withAlpha(.5 * (1 - e / WhiteoutT) ** 2));
    }

    const lift = liftAt(s, t, p, e);
    const flashK = e >= 0 && e < .1 ? 1 - e / .1 : 0;
    painGesture(o, poseAt(ct, s, t, p), t.tap ? 0 : flashK, sun, strength, lift, t.tap ? flashK : 0);
    if (p.scenario === Scenarios[0]) pushed(o, Math.max(-1, e), p, c, sun, strength);
    else if (p.scenario === Scenarios[1]) shots(o, e, p, sun, strength);
  },
};
