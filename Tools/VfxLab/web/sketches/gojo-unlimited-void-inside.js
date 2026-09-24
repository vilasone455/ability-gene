// Unlimited Void: inside — the pocket-map version of the Gojo kit's ultimate, not the game. The
// home-map side (cast, the ball, the return) is "Unlimited Void: open and return"; the old dome and
// cutscene sketches stay as they were.
//
// Ported to C# as pictures only, in the Cursed Clash colours (2026-09-24): Source/RimArt/Gojo/
// UnlimitedVoidInside{Timing,Graphics}.cs, VoidSpaceGraphics.cs (space, speed lines, dust, light,
// black hole) and CameraMove.cs (the push), previewed from the RimArts debug window, Gojo,
// "unlimited void: inside". The stand-ins and everything drawn on them (the rules) are not ported:
// untick "Stand-in pawns" to see what the C# draws. The recording carries no camera move, so compare
// it with "Camera push" at 1.
//
// What it is for (proposed 2026-09-23; the ally, touch and immune rules are the user's; every number
// is a placeholder and will be an XML field).
//   No target: Gojo casts it on himself; warm-up 0.6 s (the hand sign). Everyone within 9 cells but
//   Gojo is taken into a small pocket map (about 40 x 40) made for this cast and removed after:
//   hostiles, colonists, allies, neutral visitors, prisoners, animals, downed pawns too; walls do not
//   matter. Each lands at the same offset from Gojo as it stood outside.
//   Everyone taken who has a living brain is frozen for the whole domain. Androids and mechanoids
//   have none: they are taken but not frozen and act normally (an enemy mech fights inside; the
//   colony's androids and mechs can fight beside Gojo).
//   Gojo can walk and act. Touching a frozen pawn (walk next to it, 0.3 s) spares it for the rest of
//   the domain, as Yuji holding Gojo in anime ep. 7. That is the dilemma: 10 s to reach the allies
//   caught with him, or to hit enemies.
//   The domain lasts 10 s, or until Release, or until Gojo is downed. Everyone comes out at the
//   matching home-map cell (nearest free cell if blocked). Everyone still frozen at the end, friend
//   or foe, gets Void Overload: Consciousness capped at 10 % for 60 s (downed), then Void-scarred,
//   Consciousness -15 % and Sight -20 % for 2 days. No damage. Overloading a neutral visitor costs
//   goodwill as a harmful psycast does. Cooldown 2 days.
//
// Order, with the default sliders (scenario "mixed", plan "touch allies first"):
//   0.00  white (the camera switches maps behind it); a white burst and ring on Gojo, the crowd
//         frozen round him (anime ep. 33); the splatter burst
//   0.10  the space fades in over 0.7 s: navy, haze, 180 stars, 6 galaxies, 7 white ink patches
//   0.08  the speed-line tunnel, the opening only (anime ep. 7 22-28 s and Cursed Clash 14-20 s both
//         have it; the lines are gone once the black hole is there): the view fills with streaks
//         rushing out of the point where the black hole will open, 7.5 cells north of Gojo, and the
//         victims fly through it. 360 streaks ("Streaks") at random angles, so they clump and leave
//         gaps, from 0.6 cells out to 60 (in game to the farthest corner of the view, so it fills the
//         screen at any zoom). Each reaches back 0.75 of its distance ("Streak length"; each 0.75-1.25 x
//         that) and stops 1.2 cells short of the point, so the far end of the tunnel is dark; the gap
//         widens with the opening black hole, so the streaks pour out of its rim. Sharp at the head,
//         thin at the point end, wider with distance; heads speed up as they go ("Speed lines: point
//         to edge", 2 s, each 0.8-1.2 x that); they flicker 12 times a second as redrawn anime speed
//         lines do. Two colour sets ("Speed-line colours"): anime ep. 7
//         (purple-black, magenta glow, pink-white cores) and Cursed Clash (indigo, violet glow,
//         lavender-white cores); gojo-unlimited-void-inside-cc.js lists this sketch again with the
//         Cursed Clash set so the Compare tab can show both. Dust streams out with the streaks, slower:
//         16 clouds of glitter (Cursed Clash) or white ink bits (anime). The lines last "Speed lines
//         last" (1.6 s), then fade in 0.45 s.
//   0.08  the fly-in: the stars, galaxies and white patches start 16 x nearer the point and that much
//         smaller ("Fly-in depth") and spread out to where they lie, settling as the lines end (2.05 s);
//         every third star leaves a trail. The pawns fly with the camera, so they stay put.
//   0.10  the camera pushes in ("Camera push", 1.25 x; 1 = no camera move) and centres the point by
//         1.6 s, then goes back to where the player had it over 1 s from 2.05 s. In game this sets
//         CameraDriver's position and size each frame and gives them back after (a setting to skip).
//   1.00  a white light grows at the point (Cursed Clash 19 s), brightest at 1.45 s, gone by 1.9 s
//   1.35  the black hole opens under the light over 0.7 s, as anime ep. 7 draws it (colours sampled from the
//         frame): a black disc of radius 3.2; a light rim of gas hugging it; grey-blue feathery gas
//         out to 6.9 cells, turning slowly, lit upper right and left, dark along the bottom; a thin
//         ring at 7 cells (so it passes just behind Gojo), peach-gold on top, white upper right,
//         blue-white on the left, weak lower right; a soft pale blue-white smoke cloud with cyan
//         sparkles off the ring's east side. No speed lines from here on.
//   0.25  every frozen pawn: pale tint, white edge, specks of light running into the head, the head
//         glowing, eyes wide. The android and the mech: specks glance off them.
//   0.40  Gojo acts (4.6 cells/s): walks to the near colonist and touches it (a blue ring opens,
//         the specks stop, its colour comes back) at 1.2 s; the far one at 3.5 s; then the nearest
//         raider: 4 blows 1.7 s apart, down at 9.6 s. The mech walks at Gojo's landing spot, the android cuts it
//         off and they trade blows. Plan "attack first": the raider first (down at 6.4 s), the near
//         colonist at 7.9 s; the domain ends while Gojo is touching the far one, who stays frozen.
//   10.0  the domain ends: the black hole collapses to a point and white fills the view (the camera
//         switches back behind it)
//
// Drawing: the space is drawn in BelowTerrain under a see-through Terrain layer (lib/unlimited-void.js
// voidFloor), as the Infinity Castle's depth rooms are; in game the void terrain is walkable and
// needs a see-through texture. Level circles, quads and strips only, so no per-facing method.
// Pawns, the android and the mech are stand-ins; the route and the fight are scripted to show the
// rules, not AI. The sketch sets scene: false (no grass or trees).
import { P, Y, sprite, glow } from './lib/six-paths-impact.js';
import { caster, splatter, pawn, ringAt, whiteGlow, EnemyColour, Ally, White, Ice, Pink, Teal, smooth, clamp } from './lib/gojo.js';
import {
  voidFloor, voidHole, tunnel, tunnelDust, tunnelLight, flight, Palettes, infoFlood, deflect, touchPulse, reachOut, blowFlash, overloadMark, mech, android, brawl,
  plan, gojoAt, gojoDoing, immuneAt, blows, ActFrom, Frozen, Deg,
} from './lib/unlimited-void.js';
import { draw } from './lib/six-paths-solid.js';
import { MeshPool } from '../js/engine.js';

// Decided values. The panel keeps only what is still being tuned or shows the rule.
const Shadow = .08, FloodFrom = .25, Unfreeze = .3, BurstRing = .55, WhiteFade = .25, Fall = .3;

// "opening only" stops once the camera is back (for comparing the speed lines); "whole domain" runs to the white.
function times(p) {
  const whole = p.show !== 'opening only';
  return { act: ActFrom, end: p.hold, gone: p.hold + p.collapse, total: whole ? p.hold + p.collapse + .15 : p.linesFor + 1.55, whole };
}

export default {
  kit: 'Gojo', label: 'Unlimited Void: inside (sketch)', scene: false,
  params: {
    scenario: { label: 'Who is caught', value: 'mixed', options: ['raiders only', 'mixed'], group: 'Showcase' },
    order: { label: "Gojo's plan", value: 'touch allies first', options: ['touch allies first', 'attack first'], group: 'Showcase' },
    actors: { label: 'Stand-in pawns (and what happens to them)', value: true, group: 'Showcase' },
    show: { label: 'Show', value: 'whole domain', options: ['whole domain', 'opening only'], group: 'Showcase' },
    radius: P('Radius (cells)', 9, 5, 14, .5, 'Rule'),
    speed: P('Gojo walks (cells/s)', 4.6, 2, 8, .1, 'Rule'),
    hold: P('Domain lasts', 10, 3, 15, .5, 'Timing (s)'),
    touch: P('A touch takes', .3, .1, 1, .05, 'Timing (s)'),
    arrive: P('White clears', .8, .3, 2, .05, 'Timing (s)'),
    collapse: P('Collapse', .7, .3, 1.5, .05, 'Timing (s)'),
    palette: { label: 'Speed-line colours', value: Palettes[0], options: Palettes, group: 'Speed lines' },
    count: P('Streaks', 360, 60, 800, 10, 'Speed lines'),
    length: P('Streak length (share of its distance)', .75, .2, .95, .01, 'Speed lines'),
    linesFor: P('Speed lines last (s)', 1.6, .3, 4, .05, 'Speed lines'),
    lineTrip: P('Speed lines: point to edge (s)', 2, .4, 6, .1, 'Speed lines'),
    flyIn: P('Fly-in depth (x nearer at the start)', 16, 1, 40, 1, 'Speed lines'),
    push: P('Camera push (x zoom, 1 = off)', 1.25, 1, 2, .05, 'Speed lines'),
    hole: P('Black hole radius (cells)', 3.2, 1.5, 6, .1, 'Shape'),
    holeNorth: P('Black hole north of Gojo (cells)', 7.5, -6, 14, .5, 'Shape'),
  },
  duration(p) { return times(p).total; },
  phases(p) {
    const t = times(p);
    const list = [{ name: 'White (map switch)', t: 0 }, { name: 'Speed lines', t: .1 }, { name: 'Gojo acts', t: t.act },
      { name: 'White light', t: Math.max(.1, p.linesFor - .6) }, { name: 'Black hole', t: Math.max(.1, p.linesFor - .25) }];
    if (t.whole) list.push({ name: 'Domain ends', t: t.end }, { name: 'White (back)', t: t.end + p.collapse * .6 });
    return list.sort((a, b) => a.t - b.t);
  },
  events(p) {
    const list = [{ t: 0, type: 'sound', def: 'AG_Gojo_VoidInside' }];
    if (p.push > 1) list.push({ t: .1, type: 'camera', over: p.linesFor - .1, zoom: p.push, pan: 1, x: 0, z: p.holeNorth },   // push in on the point
      { t: p.linesFor + .45, type: 'camera', over: 1, zoom: 1, pan: 0 });                                                     // and back
    if (times(p).whole) list.push({ t: p.hold, type: 'sound', def: 'AG_Gojo_DomainClose' });
    return list;
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.total) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = Shadow;   // space: faint contact shadows only
    const pl = plan({ scenario: p.scenario, order: p.order, radius: p.radius, speed: p.speed, touch: p.touch, budget: p.hold - ActFrom });
    const u = Math.max(0, s - ActFrom), ending = clamp((s - p.hold) / p.collapse);
    const fadeIn = smooth(clamp((s - .1) / (p.arrive * .9)));
    const at = ([x, z]) => ({ x: o.x + x, z: o.z + z }), off = q => ({ x: o.x + q.x, z: o.z + q.z });

    // --- the space, the speed-line tunnel and the black hole ------------------------------------------------
    // The space flies in from the vanishing point while the lines run and settles as they end; the white
    // light grows at that point and the black hole opens under it as it fades.
    const H = { x: o.x, z: o.z + p.holeNorth }, land = p.linesFor + .45;
    voidFloor('uv inside floor', o, s, fadeIn, { fly: { at: H, g: flight(s, .08, land, p.flyIn), g0: flight(s - .1, .08, land, p.flyIn) } });
    const lines = clamp((s - .08) / .15) * (1 - smooth(clamp((s - p.linesFor) / .45))), look = { palette: p.palette, trip: p.lineTrip };
    const open = smooth(clamp((s - (p.linesFor - .25)) / .7)), shut = smooth(clamp(ending / .6));
    tunnel('uv inside tunnel', H, s, lines, { ...look, count: p.count, length: p.length, hollow: Math.max(1.2, p.hole * open * 1.25) });
    tunnelDust('uv inside dust', H, s, lines, look);
    tunnelLight('uv inside light', H, smooth(clamp((s - (p.linesFor - .6)) / .45)) * (1 - smooth(clamp((s - (p.linesFor - .15)) / .45))), p.palette);
    voidHole('uv inside hole', H, p.hole * open * (1 - shut), s * (1 + 3 * ending), open);

    // --- who stands where --------------------------------------------------------------------------------------
    const figs = [];
    const gojo = off(gojoAt(pl, u));
    figs.push({ kind: 'gojo', pos: gojo });
    const mechTaken = pl.cast.some(g => g.id === 'mech');
    const imm = immuneAt(u, pl.budget, mechTaken);
    pl.cast.forEach(g => {
      if (g.kind === 'mech') figs.push({ kind: 'mech', g, pos: off(imm.mech) });
      else if (g.kind === 'android') figs.push({ kind: 'android', g, pos: off(imm.android) });
      else figs.push({ kind: g.kind, g, pos: at(g.at) });
    });

    // Blows landing: jolt the target a little away from Gojo.
    const doing = gojoDoing(pl, u);
    if (doing && doing.st.kind === 'strike') {
      const hit = blows(doing.st).filter(h => h <= u).pop();
      const f = figs.find(q => q.g === doing.st.target);
      if (f && hit !== undefined && u - hit < .12) {
        const dx = f.pos.x - gojo.x, dz = f.pos.z - gojo.z, d = Math.hypot(dx, dz) || 1, k = .07 * (1 - (u - hit) / .12);
        f.pos = { x: f.pos.x + dx / d * k, z: f.pos.z + dz / d * k };
      }
    }

    // --- pawns, north first ------------------------------------------------------------------------------------
    const floodOn = clamp((s - FloodFrom) / .3) * (1 - ending);
    if (p.actors) figs.sort((a, b) => b.pos.z - a.pos.z).forEach(f => {
      if (f.kind === 'gojo') {
        const lower = 1 - smooth(clamp(u / .5));                  // the sign is held as he lands, then the hand comes down
        caster(f.pos, sun, strength, { blindfold: 0, sign: lower, rim: 1, crossed: true });
        return;
      }
      if (f.kind === 'mech') { mech(f.pos, sun, strength); deflect(`uv inside deflect mech`, f.pos, s, floodOn); return; }
      if (f.kind === 'android') { android(f.pos, sun, strength); deflect(`uv inside deflect android`, f.pos, s + .4, floodOn); return; }
      const colour = f.kind === 'colonist' ? Ally : EnemyColour;
      const spareAt = pl.spared.get(f.g.id), sparedAge = spareAt === undefined ? -1 : u - spareAt;
      const freeze = sparedAge < 0 ? 1 : 1 - smooth(clamp(sparedAge / Unfreeze));
      const downAt = pl.downed.get(f.g.id), lie = downAt === undefined ? 0 : smooth(clamp((u - downAt) / Fall));
      const hide = s < .02 ? 0 : 1;
      if (lie < 1) pawn(f.pos, colour, sun, strength, { alpha: hide * (1 - lie), tint: Frozen, tintAmount: .38 * freeze, outline: .3 * freeze });
      if (lie > 0) { pawn(f.pos, colour, sun, strength, { lie: true, alpha: lie, tint: Frozen, tintAmount: .35 }); overloadMark(`uv inside mark ${f.g.id}`, f.pos, s, lie * (1 - ending)); }
      if (freeze > 0 && lie < 1) infoFlood(`uv inside flood ${f.g.id}`, f.pos, s + f.g.at[0], floodOn * freeze * (1 - lie));
      if (sparedAge >= 0) touchPulse(`uv inside spared ${f.g.id}`, f.pos, sparedAge);
    });

    // --- Gojo's hand: a touch, or blows -----------------------------------------------------------------------
    if (p.actors && doing) {
      const target = figs.find(q => q.g === doing.st.target);
      if (target) {
        const aim = { x: target.pos.x, z: target.pos.z + .3 };
        if (doing.st.kind === 'touch') reachOut(gojo, aim, smooth(clamp(doing.age / (p.touch * .5))));
        else {
          const hits = blows(doing.st), next = hits.find(h => h >= u - .1);
          const jab = next === undefined ? 0 : clamp(1 - Math.abs(u - next) / .12);
          reachOut(gojo, aim, .3 + .7 * jab);
          hits.forEach((h, k) => blowFlash(`uv inside blow ${k}`, { x: target.pos.x, z: target.pos.z + .35 }, Math.atan2(gojo.z - target.pos.z, gojo.x - target.pos.x) / Deg, u - h));
        }
      }
    }
    if (p.actors && mechTaken && imm.fighting) brawl('uv inside brawl', off(imm.mech), off(imm.android), imm.fightAge, 1 - ending);

    // --- the arrival: white, the burst and ring on Gojo (anime ep. 33), the splatter burst -----------------
    const white = 1 - smooth(clamp(s / WhiteFade));
    if (white > 0) draw(MeshPool.plane10, o.x, Y + .25, o.z, 90, 90, 0, White.withAlpha(white));
    const ringAge = s / BurstRing;
    if (ringAge < 1) {
      const r = p.radius * 1.05 * smooth(ringAge), fade = 1 - ringAge;
      ringAt(o, r, White.withAlpha(.9 * fade), Y + .1, true, whiteGlow);
      ringAt(o, r * 1.03, Pink.withAlpha(.35 * fade), Y + .101, false, whiteGlow);
      ringAt(o, r * .97, Teal.withAlpha(.35 * fade), Y + .102, false, whiteGlow);
      sprite({ x: o.x, z: o.z + .5 }, 5 * (1 - ringAge * .5), 5 * (1 - ringAge * .5), White.withAlpha(.7 * fade), glow, Y + .103);
    }
    splatter('uv inside splatter', { x: o.x, z: o.z + .5 }, 4, s, (1 - smooth(clamp((s - .1) / .8))) * clamp(s / .05));

    // --- the end: the black hole collapses to a point and white fills the view ---------------------------------
    if (ending > 0) {
      const point = smooth(clamp(ending / .6)), fill = smooth(clamp((ending - .45) / .55));
      sprite(H, .6 + 3 * point, .6 + 3 * point, White.withAlpha(.9 * point), glow, Y + .2);
      sprite(H, 40 * fill + 1, 40 * fill + 1, White.withAlpha(fill), glow, Y + .21);
      if (fill > 0) draw(MeshPool.plane10, o.x, Y + .25, o.z, 90, 90, 0, White.withAlpha(fill * fill));
      ringAt(H, .5 + 14 * point, Ice.withAlpha(.6 * (1 - point)), Y + .205, true, whiteGlow);
    }
  },
};
