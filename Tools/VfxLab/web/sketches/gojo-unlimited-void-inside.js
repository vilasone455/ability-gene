// Unlimited Void: inside — the pocket-map version of the Gojo kit's ultimate, not the game. Nothing
// in Source/RimArt draws this yet. The home-map side (cast, the ball, the return) is "Unlimited Void:
// open and return"; the old dome and cutscene sketches stay as they were.
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
//   0.10  the space fades in over 0.7 s: navy, haze, 180 stars, 6 galaxies, 8 white ink patches;
//         the black hole opens 5 cells north of Gojo, radius 4.5, so its ring passes just behind
//         him (black disc, gold and teal ring, gas curling in close to the ring, wisps streaming
//         east, 70 dashes of light running out nearly straight, 5 degrees of curl per cell)
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
  voidFloor, voidHole, infoFlood, deflect, touchPulse, reachOut, blowFlash, overloadMark, mech, android, brawl,
  plan, gojoAt, gojoDoing, immuneAt, blows, ActFrom, Frozen, Deg,
} from './lib/unlimited-void.js';
import { draw } from './lib/six-paths-solid.js';
import { MeshPool } from '../js/engine.js';

// Decided values. The panel keeps only what is still being tuned or shows the rule.
const Shadow = .08, FloodFrom = .25, Unfreeze = .3, BurstRing = .55, WhiteFade = .25, Fall = .3;

function times(p) {
  return { act: ActFrom, end: p.hold, gone: p.hold + p.collapse, total: p.hold + p.collapse + .15 };
}

export default {
  kit: 'Gojo', label: 'Unlimited Void: inside (sketch)', scene: false,
  params: {
    scenario: { label: 'Who is caught', value: 'mixed', options: ['raiders only', 'mixed'], group: 'Showcase' },
    order: { label: "Gojo's plan", value: 'touch allies first', options: ['touch allies first', 'attack first'], group: 'Showcase' },
    radius: P('Radius (cells)', 9, 5, 14, .5, 'Rule'),
    speed: P('Gojo walks (cells/s)', 4.6, 2, 8, .1, 'Rule'),
    hold: P('Domain lasts', 10, 3, 15, .5, 'Timing (s)'),
    touch: P('A touch takes', .3, .1, 1, .05, 'Timing (s)'),
    arrive: P('White clears', .8, .3, 2, .05, 'Timing (s)'),
    collapse: P('Collapse', .7, .3, 1.5, .05, 'Timing (s)'),
    hole: P('Black hole radius (cells)', 4.5, 1.5, 8, .1, 'Shape'),
    holeNorth: P('Black hole north of Gojo (cells)', 5, -6, 12, .5, 'Shape'),
    swirl: P('Ray spiral (degrees per cell)', 5, 0, 60, 1, 'Shape'),
  },
  duration(p) { return times(p).total; },
  phases(p) {
    const t = times(p);
    return [{ name: 'White (map switch)', t: 0 }, { name: 'Void', t: .1 }, { name: 'Gojo acts', t: t.act },
      { name: 'Domain ends', t: t.end }, { name: 'White (back)', t: t.end + p.collapse * .6 }];
  },
  events(p) {
    return [{ t: 0, type: 'sound', def: 'AG_Gojo_VoidInside' }, { t: p.hold, type: 'sound', def: 'AG_Gojo_DomainClose' }];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.total) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = Shadow;   // space: faint contact shadows only
    const pl = plan({ scenario: p.scenario, order: p.order, radius: p.radius, speed: p.speed, touch: p.touch, budget: p.hold - ActFrom });
    const u = Math.max(0, s - ActFrom), ending = clamp((s - p.hold) / p.collapse);
    const fadeIn = smooth(clamp((s - .1) / (p.arrive * .9)));
    const at = ([x, z]) => ({ x: o.x + x, z: o.z + z }), off = q => ({ x: o.x + q.x, z: o.z + q.z });

    // --- the space and the black hole ------------------------------------------------------------------------
    voidFloor('uv inside floor', o, s, fadeIn);
    const H = { x: o.x, z: o.z + p.holeNorth };
    const open = smooth(clamp((s - .15) / (p.arrive * .8))), shut = smooth(clamp(ending / .6));
    voidHole('uv inside hole', H, p.hole * open * (1 - shut), s * (1 + 3 * ending), open, { swirl: p.swirl * Deg, rays: 1 - ending });

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
    figs.sort((a, b) => b.pos.z - a.pos.z).forEach(f => {
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
    if (doing) {
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
    if (mechTaken && imm.fighting) brawl('uv inside brawl', off(imm.mech), off(imm.android), imm.fightAge, 1 - ending);

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
