// Banshō Ten'in (Universal Pull) — technique proposal for the Pain hero kit, not the game. Nothing in
// Source/RimArt draws this yet. The kit's other two abilities already exist in C#: Shinra Tensei (the
// push, repulsion eye) and Gravity Well (the channelled area pull).
//
// What it is for (proposed 2026-09-24, the user said "let sketch"; every number is a placeholder and
// will be an XML field).
//   Target one hostile pawn or animal up to 12 cells away, line of sight. Warm-up 0.4 s: Pain raises
//   the palm at it. The pull takes hold for 0.15 s (the target slides 0.25 cells toward Pain), then
//   the target is lifted 0.6 cells and flies in a straight line to the cell in front of Pain at 20
//   cells/s (12 cells in 0.6 s). Because it is lifted it passes over low cover (sandbags,
//   barricades). On arrival Pain grabs its head and pushes it down face-first on that cell: 15 blunt,
//   stunned 2 s.
//   A standing pawn that gets into the line stops it: both take 8 blunt and are stunned 1 s, and the
//   target drops where it stopped. A wall cannot be in the line (line of sight).
//   Body size over 2 (thrumbo, centipede): it is not lifted; it is dragged along the floor half the
//   distance at 35 % of the speed, with no slam and no stun.
//   Cooldown 15 s. Shinra Tensei and Banshō Ten'in share the canon 5-second gap (manga ch. 427): after
//   either one, the other waits 5 s. Heroes are never cast by the AI.
//   Not pulled: downed pawns, corpses and items. The Retrieval hook belt already pulls downed
//   colonists and items (Chain Sickle Snag leaves rescue out for the same reason); the proposal in
//   chat had it, it was dropped here.
//   Differs from Chain Sickle Snag (7 cells, a chain, the weight ratio sets distance and time, no
//   slam) and from Gravity Well (an area, channelled, pulls everything, implosion).
//
// Order, with the default sliders (scenario "raider behind sandbags", 8 cells):
//   0.00  rest
//   0.20  warm-up 0.4 s: Pain's arm comes up at the target, the hand open with the fingers spread (the
//         anime's pose), and a Rinnegan glint flashes at the eyes; a black core in a soft pale glow
//         forms just past the fingertips (Naruto Mobile's black core); a see-through dark sphere,
//         darkest at its edge, closes on the target's chest; a dashed line on the floor shows the pull
//   0.60  the pull takes hold, 0.15 s, with a small shake: the target leans toward Pain and slides
//         0.25 cells, its feet scraping two short furrows and kicking dust; the core stretches into a
//         teardrop, 1.6 times as long as it is wide, its point aimed at the target, and its glow
//         brightens; dark streaks start round the target and flow along the line into the point
//   0.75  lift: the target leaves the floor 0.6 cells up, its head snaps back, a puff of dust where the
//         feet were
//   0.75  flight, 0.34 s: back-first at Pain, speeding up, head and arms trailing, two faint
//         afterimages; pale speed lines and a dark smoke trail behind it; dust streaks on the floor
//         slide toward Pain (the anime's speed lines, laid on the ground); it clears the sandbags
//   1.09  catch: it reaches the cell in front of Pain; the core snaps back round with a soft flash
//         and is gone into the hand in 0.1 s; the fingers close on the head and push it down in 0.1 s
//   1.19  slam: shake, pale rays, a dust ring and a cloud that hangs 1.7 s; 7 plates of ground tilt up
//         round it (earth, every third one stone) with a gap on Pain's side (Naruto Mobile's crater),
//         rocks thrown out, a dent with cracks; it lies face-down, head at Pain
//   1.19-3.19  stunned (stars); the plates, dent, cracks, furrows and rocks stay to the end
//
// Looks (dropdown). Storm 4, the default, in the kit's colours (Gravity Well's black and pale blue,
// Shinra Tensei's blue-white): the see-through black sphere with a dark edge on the target (Nagato's
// pull), the dark streaks that close on the catch point, the pale rays at the catch; the palm core is
// Naruto Mobile's (a black hole in a soft glow, no hard line on it). Naruto Mobile: the target turns
// red, a dark dome and a small black core hold it, a thick ink trail with a red line, ink drops that
// stay, a red flash with ink splashes, the palm core's glow warm white. Anime: no visible force at
// all, only the pose, the speed lines, the slam and the dust. No rings in any look: none of the games
// has them on the pull (Storm 4's white rings and swirls belong to Almighty Push). The Rinnegan
// cut-in (Naruto Mobile) is a checkbox, off: it is a screen overlay, drawn here as a 16-cell band
// over the middle of the scene as a stand-in for the screen.
//
// Drawing: Pain's arm is drawn by the ability, not a Melee Animation clip: a sleeve in the cloak's
// colour that narrows to the wrist, a grey cuff, a skin palm and five finger strips that lie level at
// the hand's height and turn with the aim. The pulled pawn is drawn Lift x h north of its ground point
// with its shadow on the floor; its body turns so the head trails. The palm core is one fan mesh round
// its centre, its front half pulled out to the point and its edge wobbling, rebuilt every frame, over
// three soft glow quads stretched with it; the streaks into it are short separate strips, never one
// line (one line would read as the Chain Sickle's chain). The sphere, the dome and the crater are
// level circles; each plate is a flat polygon with its base on the floor and its torn top raised by
// its height, leaning outward, more upright on the south side so it does not fold into a line under
// the projection. The streaks lie flat along the pull line. No per-facing method. Pain, the raiders,
// the thrumbo and the sandbags are stand-ins.
import { AltitudeLayer, Color, Mathf, Meshes, MeshPool } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Y, Floor, Lift, sprite, glow, soft, rand } from './lib/six-paths-impact.js';
import { rock, ringAt, line, stunStars, streak, whiteGlow, Skin, EnemyColour, Ink } from './lib/goku.js';
import { frame, beast, crack, scuff, kick, puff, rect, bump, easeOut } from './lib/chain-sickle.js';
import { eyeStar } from './lib/amenoyodomi.js';
import { Core, PaleBlue, DustC, BodyZ, ShoulderH, HandH, Reach, poly, pain, standing, lying, arm } from './lib/pain.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2, D2R = Mathf.Deg2Rad;
const disc = Meshes.disc(40, 'bansho disc');
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), buildingLayer = AltitudeLayer.Building.AltitudeFor();
const pawnLayer = AltitudeLayer.Pawn.AltitudeFor();

// The kit's colours are in lib/pain.js; these are this sketch's own.
const Red = new Color(.86, .12, .18), InkBlack = new Color(.03, .02, .03), WarmWhite = new Color(1, .95, .92);
const StreakC = new Color(.92, .88, .78);
const EarthDark = new Color(.2, .15, .1), EarthLit = new Color(.56, .45, .33), StoneDark = new Color(.25, .23, .21), StoneLit = new Color(.6, .57, .52);
const SlabEdge = new Color(.08, .06, .04);
const Bag = new Color(.64, .57, .42), BagDark = new Color(.36, .31, .22), Beast = new Color(.45, .36, .28);
const Lavender = new Color(.74, .66, .95), IrisLine = new Color(.3, .2, .46), CutBack = new Color(.1, .05, .16);

// The rule's fixed numbers (placeholders).
const LandGap = 1;                        // lands on the cell in front of Pain
const HeavyShare = .5, HeavySpeed = .35;  // body size over 2: half the distance, 35 % of the speed
const BlockStun = 1;                      // seconds, both pawns when one steps into the line
// Decided timing and shape.
const Lead = .2, Catch = .1, Drop = .12, Tail = .4;
const Tug = .15, TugSlide = .25, TugLean = .3;     // the pull takes hold: seconds, cells slid, lean toward Pain
const OrbR = .2, BindR = .44;             // palm orb, and the sphere once it has closed on the chest
const OrbGap = .2;                        // cells from the palm to the orb's back edge, past the fingertips

const Scenarios = ['raider behind sandbags', 'another raider steps into the line', 'thrumbo (body size 4)'];
const Looks = { 'Storm 4: black core, pale glow': 'storm', 'Naruto Mobile: red and ink': 'mobile', 'anime: no visible force': 'anime' };

// grip: the pull takes hold; lift: the target leaves the floor (a dragged one never does, and has no tug).
function times(p) {
  const heavy = p.scenario === Scenarios[2], blocked = p.scenario === Scenarios[1];
  const cast = Lead, grip = cast + p.warm, blockAt = p.distance * .5;
  const tug = heavy ? 0 : Tug, slide = heavy ? 0 : TugSlide, lift = grip + tug;
  const path = heavy ? (p.distance - LandGap) * HeavyShare : blocked ? p.distance - slide - blockAt - .5 : p.distance - slide - LandGap;
  const fly = path / (p.speed * (heavy ? HeavySpeed : 1));
  const arrive = lift + fly, down = arrive + (heavy ? 0 : blocked ? Drop : Catch);
  return { heavy, blocked, cast, grip, tug, slide, lift, blockAt, path, fly, arrive, down, end: down + p.hold + Tail };
}
// Cells from Pain along the aim. During the tug the target slides TugSlide cells, speeding up; the
// flight then starts at the speed the slide ended at and keeps speeding up toward the hand. A dragged
// one eases in and out.
function alongAt(s, p, t) {
  if (s < t.grip) return p.distance;
  if (s < t.lift) { const u = (s - t.grip) / t.tug; return p.distance - t.slide * u * u; }
  const u = clamp((s - t.lift) / t.fly);
  if (t.heavy) return p.distance - t.path * smooth(u);
  const k = Math.min(.6, 2 * t.slide / t.tug * t.fly / t.path);
  return p.distance - t.slide - t.path * (k * u + (1 - k) * Math.pow(u, 1.6));
}
function heightAt(s, p, t) {
  if (t.heavy || s < t.lift) return 0;
  const up = p.lift * smooth(clamp((s - t.lift) / .07));
  if (s < t.arrive) return up;
  const d = clamp((s - t.arrive) / (t.down - t.arrive));
  return up * (1 - d * d);
}

// ---- small shapes -------------------------------------------------------------------------------
function grow(pts, d) {
  const cx = pts.reduce((a, q) => a + q.x, 0) / pts.length, cz = pts.reduce((a, q) => a + q.z, 0) / pts.length;
  return pts.map(q => { const dx = q.x - cx, dz = q.z - cz, l = Math.hypot(dx, dz) || 1; return { x: q.x + dx / l * d, z: q.z + dz / l * d }; });
}

// ---- stand-ins (Pain, standing and lying pawns and Pain's arm are in lib/pain.js) -----------------
// A pawn pulled back-first: the body turns so the head trails away from Pain (tilt 0 is upright, 1
// lies along the pull line; below 0 it leans toward Pain, as in the tug) and the arms trail behind the
// shoulders. g is its ground point, h its height, back the unit vector away from Pain. strength 0
// draws no shadow (the afterimages).
function flying(key, g, h, colour, back, tilt, s, sun, strength, alpha = 1, layer = Y + .02) {
  const mid = { x: g.x, z: g.z + BodyZ + h * Lift };
  sprite({ x: g.x + sun.x * (.3 + h), z: g.z + sun.z * (.3 + h) }, .8, .4, Ink.withAlpha(strength * alpha / (1 + h)), soft, shadowLayer);
  let hx = back.x * tilt, hz = 1 - Math.abs(tilt) + back.z * tilt;
  const hl = Math.hypot(hx, hz) || 1; hx /= hl; hz /= hl;
  const px = -hz, pz = hx;
  for (const side of [-1, 1]) {
    const sh = { x: mid.x + hx * .16 + px * side * .13, z: mid.z + hz * .16 + pz * side * .13 };
    const wave = Math.sin(s * 34 + side) * .05, end = { x: sh.x + back.x * .3 + px * side * (.1 + wave), z: sh.z + back.z * .3 + pz * side * (.1 + wave) };
    line(`${key} arm ${side}`, [sh, end], .07, Skin.withAlpha(alpha), undefined, layer - .001, 'none');
  }
  draw(disc, mid.x, layer, mid.z, .22, .32, Math.atan2(hx, hz) / D2R, colour.withAlpha(alpha));
  draw(disc, mid.x + hx * .4, layer + .002, mid.z + hz * .4, .16, .17, 0, Skin.withAlpha(alpha));
}
// A wall of three sandbag cells across the line: a bottom course two bags deep and three across, a
// top course of two, 0.24 cells up. Bags are drawn north first so the nearer ones overlap.
function sandbags(f, P0, along, aim, sun, strength) {
  const turn = -(aim + 90), bags = [];
  for (let c = -1; c <= 1; c++) {
    const g = f.ground(P0, along, c);
    rect(`bansho bags shadow ${c}`, { x: g.x + sun.x * .3, z: g.z + sun.z * .3 }, .8, 1, aim, Ink.withAlpha(strength * .9), shadowLayer);
    for (const d of [-.17, .17]) for (let b = 0; b < 3; b++) bags.push({ q: f.ground(P0, along + d, c + (b - 1) * .32), h: .1 });
    for (let b = 0; b < 2; b++) bags.push({ q: f.ground(P0, along, c + (b - .5) * .32), h: .24 });
  }
  bags.sort((m, n) => (m.h - n.h) || (n.q.z - m.q.z)).forEach(({ q, h }, i) => {
    const lay = buildingLayer + i * .0004, z = q.z + h * Lift;
    draw(disc, q.x, lay, z, .19, .14, turn, BagDark);
    draw(disc, q.x - .012, lay + .0002, z + .025, .16, .1, turn, Bag);
  });
}
// ---- the force ----------------------------------------------------------------------------------
// The black core in front of the palm, as Naruto Mobile draws it: a black hole sitting in a soft pale
// glow that fades outward, with no hard line on it anywhere. Its edge wobbles slowly, so it reads as a
// hole in the air rather than a ball. While it pulls it stretches into a teardrop whose point aims at
// the target (stretch 1: 1.6 times as long as it is wide; the round back stays where it was), the glow
// brightens and stretches with it, and it snaps back round at the catch. Returns the point, where the
// streaks flow in.
function palmOrb(key, c, r, dir, stretch, s, alpha, look) {
  const tip = r * (1 + 1.2 * stretch), point = { x: c.x + dir.x * tip, z: c.z + dir.z * tip };
  if (r <= .005 || alpha <= 0) return point;
  const light = look === 'mobile' ? WarmWhite : PaleBlue, deg = -Math.atan2(dir.z, dir.x) / D2R, pull = .7 + .3 * stretch;
  const mid = { x: c.x + dir.x * (tip - r) * .5, z: c.z + dir.z * (tip - r) * .5 };
  sprite(mid, r * 5.6 + tip - r, r * 5.6, light.withAlpha(.35 * alpha * pull), glow, Y + .1, deg);              // wide faint glow
  sprite(mid, r * 3.4 + (tip - r) * 1.1, r * 3.4, light.withAlpha(.8 * alpha * pull), glow, Y + .1005, deg);   // the glow it sits in
  sprite(mid, r * 2.5 + (tip - r) * 1.05, r * 2.5, light.withAlpha(.9 * alpha * pull), glow, Y + .1008, deg);  // bright soft edge
  const pts = [c];
  for (let i = 0; i <= 44; i++) {
    const a = i / 44 * TAU, front = Math.max(0, Math.cos(a)), wob = 1 + .03 * Math.sin(2 * a + s * 4) + .02 * Math.sin(3 * a - s * 6);
    const x = r * wob * Math.cos(a) + (tip - r) * Math.pow(front, 1.5), y = r * wob * Math.sin(a) * (1 - .25 * front * stretch);
    pts.push({ x: c.x + dir.x * x - dir.z * y, z: c.z + dir.z * x + dir.x * y });
  }
  poly(`${key} core`, pts, Core.withAlpha(alpha), Y + .102);
  return point;
}
// Dark streaks in the air between the target and the orb, flowing into the orb's point (Storm 4's dark
// streaks that close on the catch point). They start spread round the target and converge. Short
// separate strips, so it never reads as one line like the Chain Sickle's chain.
function inflow(key, from, to, s, alpha, colour) {
  const dx = to.x - from.x, dz = to.z - from.z, D = Math.hypot(dx, dz);
  if (D < .35 || alpha <= 0) return;
  const px = -dz / D, pz = dx / D;
  // Each is a thin core in a wider faint haze, so it reads as dark air, not a hard black needle.
  for (let i = 0; i < 10; i++) {
    const ph = (s * 2.4 + rand(i + 900)) % 1, off = (rand(i + 901) - .5) * 1.2, len = Math.min(.55 * D, .6 + rand(i + 902) * .6) / D;
    const at = u => { const k = off * Math.pow(1 - u, 1.3); return { x: from.x + dx * u + px * k, z: from.z + dz * u + pz * k }; };
    const a0 = at(Math.max(0, ph - len)), a1 = at(ph), fade = alpha * Math.sin(ph * Math.PI);
    streak(`${key} ${i} haze`, a0, a1, .09, colour.withAlpha(.12 * fade), undefined, Y + .029, 4);
    streak(`${key} ${i}`, a0, a1, .026, colour.withAlpha(.42 * fade), undefined, Y + .03, 4);
  }
}
// What holds the target. Storm 4 (Nagato's pull): a see-through black sphere round the chest, darkest at
// its edge. Naruto Mobile: a dark dome round the pawn, darker at its edge (dome = its own fade, gone
// early in the flight), and a small black core at the chest that rides with it. No pale line on either:
// neither game has one.
function hold(c, R, alpha, look, dome = 1) {
  if (alpha <= 0 || look === 'anime') return;
  if (look === 'storm') {
    draw(disc, c.x, Y + .04, c.z, R, R, 0, Core.withAlpha(.34 * alpha));
    darkEdge(c, R, alpha, Y + .041);
    return;
  }
  draw(disc, c.x, Y + .035, c.z, R * 2.4, R * 2.4, 0, Core.withAlpha(.2 * alpha * dome));
  darkEdge(c, R * 2.4, .7 * alpha * dome, Y + .036);
  draw(disc, c.x, Y + .046, c.z, .1, .1, 0, Core.withAlpha(alpha));
}
// A dark edge on a see-through circle: darkest at the rim, fading inward over a quarter of the radius.
// Six level band meshes of the kit's black, stacked; small steps, so on a big sphere it reads as a
// fade, not as rings.
const EdgeBands = [[.97, .4], [.93, .2], [.89, .13], [.84, .1], [.79, .07], [.73, .05]]
  .map(([inner, a]) => ({ mesh: Meshes.band(inner, 1, 64, `bansho edge ${inner}`), a }));
function darkEdge(c, R, alpha, layer) {
  EdgeBands.forEach((b, k) => draw(b.mesh, c.x, layer + k * .0002, c.z, R, R, 0, Core.withAlpha(b.a * alpha)));
}
// Behind the pulled pawn: dark smoke (Storm 4) or a thick ink streak with a red line in it (Naruto Mobile).
function trail(key, chest, back, gone, look) {
  const len = Math.min(1.8, gone);
  if (len < .05 || look === 'anime') return;
  const pts = [];
  for (let i = 0; i <= 8; i++) {
    const u = i / 8, wob = Math.sin(u * 7 + gone * 3) * .04 * u;
    pts.push({ x: chest.x + back.x * len * u - back.z * wob, z: chest.z + back.z * len * u + back.x * wob });
  }
  if (look === 'storm') { line(`${key} smoke`, pts, .36, Core.withAlpha(.3), undefined, Y + .016, 'end'); return; }
  line(`${key} ink`, pts, .5, InkBlack.withAlpha(.82), undefined, Y + .016, 'end');
  line(`${key} red`, pts, .1, Red.withAlpha(.8), whiteGlow, Y + .0165, 'end');
}
// Pale speed lines behind the pawn, at its height.
function speedLines(key, chest, back, s, alpha) {
  const px = -back.z, pz = back.x;
  for (let i = 0; i < 6; i++) {
    const c = (i - 2.5) * .13 + (rand(i + 500) - .5) * .06, st = .3 + rand(i + 501) * .25, len = .5 + rand(i + 502) * .8;
    const flick = .55 + .45 * Math.sin(s * 47 + i * 2.3), a = { x: chest.x + back.x * st + px * c, z: chest.z + back.z * st + pz * c };
    streak(`${key} ${i}`, a, { x: a.x + back.x * len, z: a.z + back.z * len }, .04, PaleBlue.withAlpha(.6 * flick * alpha), whiteGlow, Y + .018, 3);
  }
}
// Dust streaks on the floor in a narrow fan round the pull line, sliding in toward Pain: the anime's
// speed lines, laid on the ground.
function floorStreaks(key, P0, p, t, s) {
  const span = p.distance + 1.2, v = p.speed * (t.heavy ? HeavySpeed : 1) * .65;
  for (let i = 0; i < 16; i++) {
    const born = t.grip + rand(i + 300) * (t.tug + t.fly) * .9, life = .22 + rand(i + 301) * .14, age = s - born;
    if (age < 0 || age > life) continue;
    const ang = (p.aim + (rand(i + 302) - .5) * 44) * D2R, r0 = .6 + rand(i + 303) * span - v * age;
    if (r0 < .45) continue;
    const r1 = r0 + (.5 + rand(i + 304) * .9) * (1 - .5 * age / life);
    streak(`${key} ${i}`, { x: P0.x + Math.cos(ang) * r0, z: P0.z + Math.sin(ang) * r0 }, { x: P0.x + Math.cos(ang) * r1, z: P0.z + Math.sin(ang) * r1 },
      .06, StreakC.withAlpha(.4 * Math.sin(age / life * Math.PI)), undefined, Floor + .016, 4);
  }
}
// Ink that drips off the pawn while it flies and stays on the floor (Naruto Mobile).
function inkDrops(key, f, P0, p, t, s) {
  for (let j = 0; j < 8; j++) {
    const when = t.lift + t.fly * (j + .5) / 8, age = s - when;
    if (age < 0) continue;
    const g = f.ground(P0, alongAt(when, p, t), (rand(j + 600) - .5) * .5), fall = clamp(age / .16), r = .07 + rand(j + 601) * .07;
    if (fall < 1) draw(disc, g.x, Y + .017, g.z + (.5 + p.lift) * (1 - fall * fall) * Lift, r * .7, r, 0, InkBlack.withAlpha(.85));
    else draw(disc, g.x, Floor + .018, g.z, r * 1.5, r * 1.1, rand(j + 602) * 180, InkBlack.withAlpha(lerp(.7, .35, clamp((age - .16) / 1.5))));
  }
}

// ---- the slam -----------------------------------------------------------------------------------
function slam(key, L, age, look) {
  if (age < 0) return;
  const fl = clamp(1 - age / .14);
  if (look === 'mobile') {
    sprite({ x: L.x, z: L.z + .3 }, 3.2, 2.6, Red.withAlpha(.55 * fl), glow, Y + .12);
    for (let i = 0; i < 8; i++) {
      const a = rand(i + 720) * TAU, far = .5 + rand(i + 721) * .8, u = clamp(age / .22), d = far * easeOut(u), hh = .35 * Math.sin(u * Math.PI), r = .06 + rand(i + 722) * .08;
      draw(disc, L.x + Math.cos(a) * d, u < 1 ? Y + .05 : Floor + .019, L.z + Math.sin(a) * d + hh * Lift, r * (u < 1 ? 1 : 1.4), r, a / D2R,
        InkBlack.withAlpha(u < 1 ? .9 : lerp(.7, .35, clamp((age - .22) / 1.5))));
    }
  } else sprite({ x: L.x, z: L.z + .25 }, 1.7, 1.3, PaleBlue.withAlpha((look === 'storm' ? .7 : .45) * fl), glow, Y + .12);
  if (look === 'storm' && age < .2) for (let i = 0; i < 8; i++) {
    const a = (i * 45 + 22 + (rand(i + 700) - .5) * 20) * D2R, r0 = .25 + age * 3.2, r1 = r0 + .45 + rand(i + 701) * .45;
    streak(`${key} ray ${i}`, { x: L.x + Math.cos(a) * r0, z: L.z + .2 + Math.sin(a) * r0 }, { x: L.x + Math.cos(a) * r1, z: L.z + .2 + Math.sin(a) * r1 },
      .08, PaleBlue.withAlpha(clamp(1 - age / .2)), whiteGlow, Y + .13, 4);
  }
  // A dust ring that runs out and rises, then a cloud that hangs.
  for (let i = 0; i < 14; i++) {
    const life = .6 + rand(i + 740) * .4, u = age / life;
    if (u > 1) continue;
    const a = i / 14 * TAU + rand(i + 741) * .4, d = .3 + easeOut(u) * (.7 + rand(i + 742) * .7), hh = u * .3;
    sprite({ x: L.x + Math.cos(a) * d, z: L.z + Math.sin(a) * d * .85 + hh * Lift }, .42 + u * .6, .36 + u * .5,
      DustC.withAlpha(.55 * (1 - u) * clamp(u * 10)), puff, Y + .06);
  }
  for (let i = 0; i < 4; i++) {
    const u = clamp((age - .05) / 1.7);
    if (u <= 0 || u >= 1) continue;
    const a = i * 1.7 + .4, d = .25 + u * .4;
    sprite({ x: L.x + Math.cos(a) * d, z: L.z + .15 + Math.sin(a) * d * .6 + u * .5 }, .9 + u * .9, .75 + u * .8, DustC.withAlpha(.35 * Math.sin(u * Math.PI)), puff, Y + .07);
  }
}
// One plate of floor broken up by the slam: base on the floor, the broken top raised and leaning out,
// earth on most, stone on a few. The top edge has four points at different heights so it reads as torn.
function slab(key, L, sl, sun, strength, layer) {
  if (sl.len <= .005) return;
  const rx = Math.cos(sl.th), rz = Math.sin(sl.th), tx = -rz, tz = rx;
  const c = { x: L.x + rx * sl.r0, z: L.z + rz * sl.r0 }, outD = sl.len * Math.cos(sl.tau), up = sl.len * Math.sin(sl.tau);
  const pt = (k, f) => ({ x: c.x + tx * sl.w * k + rx * outD * f, z: c.z + tz * sl.w * k + rz * outD * f + up * f * Lift });
  const sh = (k, f) => ({ x: c.x + tx * sl.w * k + rx * outD * f + sun.x * up * f, z: c.z + tz * sl.w * k + rz * outD * f + sun.z * up * f });
  const top = [[-.85, .95 + .1 * sl.j[0]], [-.3, 1.05 + .15 * sl.j[1]], [.3, .8 + .15 * sl.j[2]], [.8, 1 + .12 * sl.j[3]]];
  const face = [pt(1, 0), pt(-1, 0), ...top.map(([k, f]) => pt(k, f))];
  poly(`${key} shadow`, [face[0], face[1], ...top.map(([k, f]) => sh(k, f))], Ink.withAlpha(strength), shadowLayer);
  // Lit by how far its upper face turns to the light (the light comes from against the shadows).
  const nx = -rx * Math.sin(sl.tau), nz = -rz * Math.sin(sl.tau), ny = Math.cos(sl.tau), lx = -sun.x, lz = -sun.z;
  const lit = clamp((nx * lx + ny + nz * lz) / Math.hypot(lx, 1, lz)), dark = sl.stone ? StoneDark : EarthDark, light = sl.stone ? StoneLit : EarthLit;
  poly(`${key} edge`, grow(face, .025), SlabEdge, layer);
  poly(`${key} face`, face, Color.Lerp(dark, light, .15 + .7 * lit), layer + .0005);
  line(`${key} top`, face.slice(2), .035, light.withAlpha(.3 + .5 * lit), undefined, layer + .001, 'none');
}
// The crater: a dent, cracks, slabs round it with a gap on Pain's side, rocks thrown out. All of it stays.
function crater(key, L, age, count, toward, sun, strength) {
  if (age < 0) return;
  const e = smooth(clamp(age / .09));
  sprite(L, 1.35 * e, 1.0 * e, InkBlack.withAlpha(.3), soft, Floor + .02);
  crack(`${key} crack`, L, 1.7 * e, 17);
  const gap = Math.atan2(toward.z, toward.x), list = [];
  for (let k = 0; k < count; k++) {
    const th = gap + (40 + (k + .5 + (rand(k * 7 + 1) - .5) * .5) / count * 280) * D2R;
    // South plates stand more upright: leaning south moves the top south as fast as height moves it
    // north, so a south plate at the north plates' lean would fold into a line.
    const rise = smooth(clamp((age - rand(k * 7 + 6) * .04) / .08)), south = Math.max(0, -Math.sin(th)), r0 = .5 + rand(k * 7 + 2) * .15;
    list.push({ k, th, r0, w: .15 + rand(k * 7 + 3) * .09, len: (.22 + rand(k * 7 + 4) * .18) * rise,
      tau: (40 + rand(k * 7 + 5) * 15 + 30 * south) * D2R, z: L.z + Math.sin(th) * r0,
      stone: k % 3 === 1, j: [0, 1, 2, 3].map(n => rand(k * 13 + n + 90) - .5) });
  }
  list.sort((m, n) => n.z - m.z).forEach((sl, i) => slab(`${key} slab ${sl.k}`, L, sl, sun, strength, Y + .03 + i * .002));
  for (let i = 0; i < 9; i++) {
    const a = rand(i * 5 + 50) * TAU, far = .6 + rand(i * 5 + 51) * .7, u = clamp(age / (.3 + rand(i * 5 + 52) * .15));
    const d = far * easeOut(u), hh = .4 * Math.sin(u * Math.PI);
    rock({ x: L.x + Math.cos(a) * d, z: L.z + Math.sin(a) * d + hh * Lift }, .09 + rand(i * 5 + 53) * .07, a / D2R + u * 200, 1, i, u < 1 ? Y + .025 : Floor + .03);
  }
}
// Naruto Mobile's cut-in: a dark band with falling streaks and Pain's two Rinnegan eyes, 0.42 s. In the
// game it would be a screen overlay; here a 16-cell band over the scene stands in for the screen.
function cutIn(key, C, u) {
  const a = clamp(u / .12) * clamp((1 - u) / .15);
  if (a <= 0) return;
  draw(MeshPool.plane10, C.x, Y + .25, C.z, 16, 3.6, 0, CutBack.withAlpha(.94 * a));
  for (let i = 0; i < 26; i++) {
    const x = C.x - 8 + (i + rand(i + 800)) * 16 / 26, drop = (u * 6 + rand(i + 802)) % 1;
    streak(`${key} streak ${i}`, { x, z: C.z + 1.8 - drop * .6 }, { x, z: C.z - 1.8 + (1 - drop) * .6 }, .05 + rand(i + 801) * .1,
      new Color(.02, .01, .05).withAlpha(.55 * a), undefined, Y + .251, 3);
  }
  for (const side of [-1, 1]) {
    const E = { x: C.x + side * 2.7, z: C.z };
    sprite(E, 3.2, 3.2, new Color(.95, .45, .7).withAlpha(.35 * a), glow, Y + .252);
    draw(disc, E.x, Y + .253, E.z, 1.25, 1.25, 0, Lavender.withAlpha(a));
    [.3, .52, .74, .96, 1.18].forEach((r, k) => ringAt(E, r, IrisLine.withAlpha(.9 * a), Y + .254 + k * .0005));
    draw(disc, E.x, Y + .257, E.z, .1, .1, 0, IrisLine.withAlpha(a));
    sprite({ x: E.x - .38, z: E.z + .4 }, .35, .3, new Color(1, 1, 1, .7 * a), glow, Y + .258);
  }
}

export default {
  kit: 'Pain', label: "Bansho Ten'in (sketch)",
  params: {
    scenario: { label: 'Target', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    look: { label: 'Look', value: Object.keys(Looks)[0], options: Object.keys(Looks), group: 'Showcase' },
    cutin: { label: 'Rinnegan cut-in (Naruto Mobile)', value: false, group: 'Showcase' },
    aim: P('Direction to the target (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Target distance (cells)', 8, 3, 12, .5, 'Showcase'),
    warm: P('Warm-up (palm out)', .4, .2, 1, .05, 'Timing (s)'),
    speed: P('Flight speed (cells/s)', 20, 6, 30, 1, 'Timing (s)'),
    stun: P('Stun after the slam', 2, 0, 4, .25, 'Timing (s)'),
    hold: P('Show the result', 2.5, 1, 5, .25, 'Timing (s)'),
    lift: P('Lift while pulled (cells)', .6, .2, 1.2, .05, 'Shape'),
    slabs: P('Crater slabs', 7, 0, 12, 1, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Rest', t: 0 }, { name: 'Warm-up', t: t.cast }, { name: 'Pull', t: t.grip }, ...(t.heavy ? [] : [{ name: 'Lift', t: t.lift }]),
      { name: t.heavy ? 'Stop' : t.blocked ? 'Hit' : 'Catch', t: t.arrive }, { name: 'Result', t: t.down }];
  },
  events(p) {
    const t = times(p), ev = [
      { t: t.cast, type: 'sound', def: 'RimArt_BanshoCast' }, { t: t.grip, type: 'sound', def: 'RimArt_BanshoPull' },
      { t: t.grip, type: 'shake', value: .012 },
    ];
    if (t.heavy) ev.push({ t: t.arrive, type: 'shake', value: .01 });
    else if (t.blocked) ev.push({ t: t.arrive, type: 'sound', def: 'RimArt_BanshoHit' }, { t: t.arrive, type: 'shake', value: .03 });
    else ev.push({ t: t.down, type: 'sound', def: 'RimArt_BanshoSlam' }, { t: t.down, type: 'shake', value: .06 });
    return ev;
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const look = Looks[p.look] ?? 'storm';
    const f = frame(p.aim, sun), back = { x: f.ca, z: f.sa }, toward = { x: -f.ca, z: -f.sa };
    const P0 = f.ground(o, -p.distance / 2, 0), start = f.ground(P0, p.distance, 0), L = f.ground(P0, LandGap, 0);
    const a = alongAt(s, p, t), h = heightAt(s, p, t), g = f.ground(P0, a, 0);
    const pulling = s >= t.grip && s < t.arrive, normal = !t.heavy && !t.blocked;
    const warmU = clamp((s - t.cast) / p.warm);
    const red = look === 'mobile' ? .65 * clamp((s - t.grip + .1) / .1) * (1 - clamp((s - t.down) / .4)) : 0;
    const tint = Color.Lerp(t.heavy ? Beast : EnemyColour, Red, red);

    // --- on the floor: the target line while it is aimed, streaks, drag marks ----------------------------------
    if (s >= t.cast && s < t.grip + .1) {
      const vis = smooth(clamp(warmU * 3)) * (1 - clamp((s - t.grip) / .1));
      for (let i = 0; i < 12; i++)
        rect(`bansho aim dash ${i}`, f.ground(P0, .7 + (p.distance - 1.2) * i / 12 + .1, 0), .2, .035, p.aim, PaleBlue.withAlpha(.4 * vis), Floor + .012);
    }
    if (s >= t.grip && s < t.arrive + .25) floorStreaks('bansho floor', P0, p, t, s);
    if (t.heavy && s >= t.grip) scuff('bansho drag', start, g, 1, .45);
    // The tug: the feet scrape two short furrows, and a puff of dust where they leave the floor. Both stay.
    const liftAt = f.ground(P0, p.distance - t.slide, 0);
    if (!t.heavy && s >= t.grip) scuff('bansho tug', start, liftAt, clamp((s - t.grip) / t.tug), .4);
    if (!t.heavy && s >= t.lift) for (let i = 0; i < 6; i++) {
      const u = clamp((s - t.lift) / (.35 + rand(i + 780) * .2)), an = i * 1.05 + rand(i + 781) * .5, d = .1 + easeOut(u) * .45;
      if (u < 1) sprite({ x: liftAt.x + Math.cos(an) * d, z: liftAt.z + Math.sin(an) * d * .8 + u * .15 }, .25 + u * .35, .2 + u * .3, DustC.withAlpha(.5 * (1 - u)), puff, Y + .005);
    }
    if (look === 'mobile' && !t.heavy) inkDrops('bansho ink', f, P0, p, t, s);
    if (normal) crater('bansho crater', L, s - t.down, p.slabs, toward, sun, strength);
    if (t.blocked && s >= t.down) crack('bansho drop', g, .9 * smooth(clamp((s - t.down) / .08)), 21);
    if (p.scenario === Scenarios[0]) sandbags(f, P0, p.distance - 1, p.aim, sun, strength);

    // --- pawns on the ground, north first; the pulled pawn in the air is drawn above them ----------------------
    const blockWalk = smooth(clamp((s - (t.grip - .5)) / .55)), knock = t.blocked ? .35 * easeOut(clamp((s - t.arrive) / .2)) : 0;
    const B = f.ground(P0, t.blockAt - knock, 1.8 * (1 - blockWalk));
    const ground = [{ z: P0.z, draw: () => pain(P0, sun, strength) }];
    if (t.heavy) {
      const jitter = s >= t.grip && s < t.arrive ? Math.sin(s * 60) * .03 : 0;
      ground.push({ z: g.z, draw: () => beast(f.ground(P0, a, jitter), 4, sun, strength, tint) });
    } else if (s < t.grip) ground.push({ z: g.z, draw: () => standing(g, tint, sun, strength) });
    else if (s < t.lift) ground.push({ z: g.z, draw: () => flying('bansho target', g, 0, tint, back, -TugLean * smooth((s - t.grip) / t.tug), s, sun, strength, 1, pawnLayer) });
    else if (s >= t.down) ground.push({ z: g.z, draw: () => lying('bansho target', g, tint, toward, sun, strength) });
    if (t.blocked) ground.push({ z: B.z, draw: () => standing({ x: B.x + (s >= t.arrive && s < t.arrive + .2 ? Math.sin(s * 90) * .03 : 0), z: B.z }, EnemyColour, sun, strength) });
    ground.sort((m, n) => n.z - m.z).forEach(q => q.draw());

    // Pain's arm: up at the target during the warm-up with the fingers spread, held through the pull; at
    // the catch the fingers close on the head and the hand pushes it down.
    const armUp = smooth(clamp((s - t.cast) / (p.warm * .6))), armBack = smooth(clamp((s - (normal ? t.down + .45 : t.arrive + .35)) / .3));
    const push = normal ? smooth(clamp((s - t.arrive) / Catch)) : 0;
    const reach = Reach * armUp * (1 - armBack), handH = lerp(lerp(HandH, .32, push), .38, armBack);
    const hand = f.place(P0, .12 + reach, -.1, handH);
    if (armUp > 0 && armBack < 1) arm('bansho arm', f.place(P0, .05, -.1, ShoulderH), hand, back, push);

    // --- the pulled pawn in the air, with two afterimages; the head snaps back as it leaves the floor ----------
    if (!t.heavy && s >= t.lift && s < t.down) {
      const tilt = s < t.arrive ? lerp(-TugLean, .7, smooth(clamp((s - t.lift) / .08))) : lerp(.7, 1, clamp((s - t.arrive) / (t.down - t.arrive)));
      const blur = clamp((s - t.lift) / .08) * (1 - clamp((s - t.arrive) / .04));
      for (let k = 2; k >= 1; k--) flying(`bansho ghost ${k}`, f.ground(P0, a + .32 * k, 0), h, tint, back, tilt, s, sun, 0, (k === 1 ? .3 : .14) * blur, Y + .015 + k * .001);
      flying('bansho target', g, h, tint, back, tilt, s, sun, strength);
    }

    // --- the force: the palm orb, what holds the target, the trail --------------------------------------------
    const chest = t.heavy ? { x: g.x, z: g.z + .45 } : { x: g.x, z: g.z + .3 + h * Lift };
    if (look !== 'anime') {
      // The orb stretches toward the target as the pull takes hold (a dragged beast: as the drag starts),
      // snaps back round at the catch and goes into the grip in 0.1 s; after a drag it relaxes and fades.
      const caught = t.heavy ? 0 : clamp((s - t.arrive) / .1);
      const orbA = smooth(clamp(warmU * 1.4)) * (t.heavy ? 1 - clamp((s - t.arrive - .2) / .15) : 1 - caught);
      const stretch = (t.heavy ? smooth(clamp((s - t.grip) / .2)) * (1 - smooth(clamp((s - t.arrive) / .1)))
        : smooth(clamp((s - t.grip) / t.tug)) * (1 - smooth(clamp((s - t.arrive) / .05)))) * (1 + .06 * Math.sin(s * 40));
      const strain = t.heavy && pulling ? .02 * Math.sin(s * 70) : 0, orbAt = f.place(P0, .12 + reach + OrbGap + OrbR, -.1, handH);
      const point = palmOrb('bansho palm', { x: orbAt.x + strain, z: orbAt.z }, OrbR * smooth(clamp(warmU * 1.25)) * (1 - .6 * smooth(caught)),
        back, stretch, s, orbA, look);
      if (!t.heavy && s >= t.arrive && s < t.arrive + .15) {       // the snap back to round: a soft flash that spreads and fades
        const u = (s - t.arrive) / .15, size = OrbR * lerp(3, 6.5, easeOut(u));
        sprite(orbAt, size, size, (look === 'mobile' ? WarmWhite : PaleBlue).withAlpha(.6 * (1 - u)), glow, Y + .0995);
      }
      inflow('bansho inflow', chest, point, s, clamp((s - t.grip) / .1) * (1 - clamp((s - t.arrive) / .05)), look === 'mobile' ? InkBlack : Core);
      const holdA = s < t.grip ? smooth(clamp(warmU * 2)) : 1 - clamp((s - t.arrive) / .12);
      const big = t.heavy ? 2.1 : 1, R = s < t.grip ? lerp(t.heavy ? 1.6 : 1.15, BindR * big, smooth(warmU)) : BindR * big * (1 + .04 * Math.sin(s * 40));
      hold(chest, R, holdA, look, 1 - clamp((s - t.grip) / .1));
    }
    if (!t.heavy && s >= t.lift && s < t.arrive) {
      trail('bansho trail', chest, back, p.distance - t.slide - a, look);
      speedLines('bansho speed', chest, back, s, clamp((s - t.lift) / .05));
    }
    if (!t.heavy && s >= t.grip && s < t.lift) kick(g, s - t.grip, 1, 13);   // dust at the scraping feet
    if (t.heavy && pulling) kick(g, s - t.grip, 1, 5);

    // --- the landing -------------------------------------------------------------------------------------------
    if (normal) slam('bansho slam', L, s - t.down, look);
    if (t.blocked && s >= t.arrive) {
      const hit = { x: (g.x + B.x) / 2, z: (g.z + B.z) / 2 + .35 }, age = s - t.arrive;
      sprite(hit, .9, .8, (look === 'mobile' ? Red : PaleBlue).withAlpha(.8 * clamp(1 - age / .12)), glow, Y + .12);
      for (let i = 0; i < 6; i++) {
        const u = clamp(age / (.4 + rand(i + 760) * .3)), an = i * 1.05 + rand(i + 761);
        if (u < 1) sprite({ x: g.x + Math.cos(an) * (.2 + u * .6), z: g.z + Math.sin(an) * (.2 + u * .6) * .8 + u * .2 }, .3 + u * .4, .26 + u * .3, DustC.withAlpha(.5 * (1 - u)), puff, Y + .06);
      }
    }
    if (t.heavy && s >= t.arrive) kick(g, s - t.arrive, 1 - clamp((s - t.arrive) / .4), 9);

    // Stunned: stars over the head of whoever was hit.
    const stunFor = normal ? p.stun : BlockStun, sa = clamp((s - t.down) / .15) * clamp((t.down + stunFor - s) / .2);
    if (!t.heavy && s >= t.down && stunFor > 0) {
      const head = { x: g.x + toward.x * .4, z: g.z + .08 + toward.z * .4 };
      stunStars('bansho stun', { x: head.x, z: head.z + .22 - .84 }, s, sa);
      if (t.blocked) stunStars('bansho stun blocker', B, s, sa);
    }

    // The Rinnegan: a glint at the eyes as the warm-up starts, and the cut-in if it is on.
    eyeStar({ x: P0.x, z: P0.z + .6 }, .28, bump(clamp((s - t.cast) / .35)));
    if (p.cutin) cutIn('bansho cut-in', { x: o.x, z: o.z + 2.4 }, (s - t.cast) / .42);
  },
};
