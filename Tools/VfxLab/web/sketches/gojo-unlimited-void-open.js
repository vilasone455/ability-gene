// Unlimited Void: open and return — the pocket-map version of the Gojo kit's ultimate, seen on the
// home map. Not the game; nothing in Source/RimArt draws this yet. The inside is "Unlimited Void:
// inside", whose header has the full proposed mechanic (the ally, touch and immune rules are the
// user's; every number is a placeholder and will be an XML field). In short: everyone within 9
// cells but Gojo is taken into his pocket-map domain for 10 s; the living are frozen there unless
// Gojo touches them; androids and mechanoids are not; whoever is still frozen at the end comes back
// with Void Overload (downed 60 s, then Void-scarred for 2 days). Cooldown 2 days.
//
// Order, with the default sliders (scenario "mixed", plan "touch allies first"):
//   0.00  six raiders and a mech walk in on Gojo; two colonists and an android are inside the
//         radius; one raider outside it walks in from the east the whole time
//   0.30  warm-up 0.6 s: one hand rises in front of the face and the middle finger crosses the
//         index finger (anime ep. 7 and 33); the other hand pulls the blindfold down to the neck;
//         the eyes light blue; a cyan-violet rim light; light gathers at the raised hand
//   0.90  the barrier closes over the 9-cell radius in 0.3 s: a dark sphere (four see-through fills,
//         so the edge is soft) with a faint light rim spreads from Gojo behind a thin white front; everyone under it is taken (camera shake 0.04)
//   1.20  it shrinks in 0.5 s, fast first, and rises into a black ball 0.5 cells across hanging
//         1.2 cells over Gojo's cell (manga ch. 227-228: the barrier the size of a basketball).
//         Everyone inside is gone; a faint ring at the true radius fades over 1.2 s.
//   1.70  the ball hangs (the real domain is 10 s; the sketch shows 2 s). The raider from outside
//         walks on through the empty ground and ignores it.
//   3.70  the ball breaks: cracks of light, then a soft white flash and a thin ring out to the radius
//   3.90  everyone comes back at the matching cell: the raiders stand pale and still for 0.5 s
//         ("unconscious on their feet", anime ep. 33), then fall and lie downed with the violet
//         overload mark; the raider Gojo beat down inside comes back lying; a colonist he touched
//         comes back standing with a blue ring; the android and the mech come back where their
//         fight left them, still fighting; Gojo comes back where he walked to.
//   4.70  to 6.30: the result stays on screen
//
// Drawing: the dark sphere is lib/gojo.js domeOutline (the dome under the 0.6 lift) filled four
// times, see-through, so its edge is soft; the ball, rings and flashes are level circles and quads,
// so no per-facing method. Who comes back how is worked out by lib/unlimited-void.js plan with the
// inside sketch's default speed, touch time and 10 s hold. Stand-ins for every pawn.
import { Mathf } from '../js/engine.js';
import { P, Y, Floor, sprite, glow } from './lib/six-paths-impact.js';
import { caster, pawn, ringAt, glint, streak, whiteGlow, EnemyColour, Ally, White, Ice, Violet, EyeBlue, smooth, clamp } from './lib/gojo.js';
import {
  darkDome, ball, ballBurst, touchPulse, overloadMark, mech, android, brawl,
  castList, plan, gojoAt, immuneAt, Hold, ActFrom, Frozen, Deg,
} from './lib/unlimited-void.js';

// Decided values. The panel keeps only what is still being tuned or shows the rule.
const Lead = .3, Tail = 1.6, Fall = .3, WalkIn = 1.2, Shake = .04, Gather = 8, RingFade = 1.2, Appear = .2;
const Outside = { from: [12, 1.2], speed: 1 };      // a raider past the radius, walking west the whole time

function times(p) {
  const cast = Lead, open = cast + p.warm, full = open + p.close, hang = full + p.shrink, burst = hang + p.hold;
  const back = burst + Appear, fall = back + p.stand, down = fall + Fall;
  return { cast, open, full, hang, burst, back, fall, down, end: down + Tail };
}

export default {
  kit: 'Gojo', label: 'Unlimited Void: open and return (sketch)',
  params: {
    scenario: { label: 'Who is caught', value: 'mixed', options: ['raiders only', 'mixed'], group: 'Showcase' },
    order: { label: "Gojo's plan inside", value: 'touch allies first', options: ['touch allies first', 'attack first'], group: 'Showcase' },
    walker: { label: 'A raider outside walks in', value: true, group: 'Showcase' },
    radius: P('Radius (cells)', 9, 5, 14, .5, 'Rule'),
    ballSize: P('Ball across (cells)', .5, .2, 1.2, .05, 'Shape'),
    ballHeight: P('Ball height (cells)', 1.2, .5, 3, .1, 'Shape'),
    warm: P('Warm-up (hand sign)', .6, .2, 1.5, .05, 'Timing (s)'),
    close: P('Barrier closes', .3, .1, 1, .05, 'Timing (s)'),
    shrink: P('Shrinks to the ball', .5, .2, 1.5, .05, 'Timing (s)'),
    hold: P('Ball hangs (real: 10 s)', 2, .5, 10, .25, 'Timing (s)'),
    stand: P('Out cold on their feet', .5, 0, 1.5, .05, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Walk in', t: 0 }, { name: 'Hand sign', t: t.cast }, { name: 'Barrier closes', t: t.open }, { name: 'Shrinks', t: t.full },
      { name: 'Ball hangs', t: t.hang }, { name: 'Ball breaks', t: t.burst }, { name: 'Back', t: t.back }, { name: 'Downed', t: t.fall }];
  },
  events(p) {
    const t = times(p);
    return [{ t: t.open, type: 'shake', value: Shake }, { t: t.open, type: 'sound', def: 'AG_Gojo_DomainOpen' },
      { t: t.burst, type: 'sound', def: 'AG_Gojo_DomainClose' }, { t: t.burst + .08, type: 'shake', value: .02 }];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const R = p.radius, budget = Hold - ActFrom;
    const pl = plan({ scenario: p.scenario, order: p.order, radius: R, budget });
    const cast = castList(p.scenario, R);
    const imm = immuneAt(budget, budget, cast.some(g => g.id === 'mech' && g.taken));
    const at = ([x, z]) => ({ x: o.x + x, z: o.z + z }), off = q => ({ x: o.x + q.x, z: o.z + q.z });
    const inside = s >= t.full && s < t.back;          // the taken are in the pocket map
    const returned = s >= t.back;

    // --- the barrier, the shrink, the ball, the break ------------------------------------------------------
    if (s >= t.open && s < t.full) {
      const f = smooth((s - t.open) / p.close), r = R * f;
      darkDome('uv open dome', o, r, 1);
      ringAt(o, r, White.withAlpha(.55), Y + .1, false, whiteGlow);
      sprite({ x: o.x, z: o.z + .5 }, 1.5 + 3 * f, 1.5 + 3 * f, Ice.withAlpha(.5 * (1 - f)), glow, Y + .101);
    }
    if (s >= t.full && s < t.hang) {
      const f = 1 - Math.pow(1 - (s - t.full) / p.shrink, 3);          // fast first, slow at the end
      const r = Mathf.Lerp(R, p.ballSize / 2, f), h = p.ballHeight * f;
      darkDome('uv open dome', { x: o.x, z: o.z + h * .6 }, r, 1);
    }
    if (s >= t.full && s < t.full + RingFade) ringAt(o, R, Ice.withAlpha(.5 * (1 - (s - t.full) / RingFade)), Floor + .02, false, whiteGlow);
    if (s >= t.hang && s < t.burst + .1) ball('uv open ball', o, p.ballHeight, p.ballSize, s, 1 - clamp((s - t.burst) / .1), sun, strength);
    ballBurst('uv open burst', o, p.ballHeight, p.ballSize, s - t.burst, R);

    // --- figures -------------------------------------------------------------------------------------------
    const figs = [];
    // Gojo: at his cell until he is taken; back where he walked to inside.
    if (!inside) figs.push({ kind: 'gojo', pos: returned ? off(gojoAt(pl, budget)) : o });
    cast.forEach(g => {
      const dir = Math.hypot(g.at[0], g.at[1]) || 1;
      if (g.taken && inside) return;
      if (g.taken && returned) {
        const pos = g.kind === 'mech' ? off(imm.mech) : g.kind === 'android' ? off(imm.android) : at(g.at);
        figs.push({ kind: g.kind, g, pos, back: true });
        return;
      }
      // Before the barrier (or never taken): raiders and the mech walk in until the barrier opens.
      const walking = g.kind === 'raider' || g.kind === 'mech';
      const before = walking ? Math.max(0, t.open - s) * WalkIn : 0;
      figs.push({ kind: g.kind, g, pos: { x: o.x + g.at[0] * (1 + before / dir), z: o.z + g.at[1] * (1 + before / dir) } });
    });
    if (p.walker) figs.push({ kind: 'walker', pos: { x: o.x + Outside.from[0] - s * Outside.speed, z: o.z + Outside.from[1] } });

    const backAge = s - t.back;
    figs.sort((a, b) => b.pos.z - a.pos.z).forEach(f => {
      const pop = f.back || (f.kind === 'gojo' && returned) ? clamp(backAge / .12) : 1;   // everyone comes back inside the flash
      if (f.kind === 'gojo') {
        const sign = returned ? 0 : smooth((s - t.cast) / p.warm), pull = smooth((s - t.cast) / (p.warm * .6));
        const rim = returned ? 1 - smooth(backAge / 1.2) : smooth((s - t.cast) / (p.warm * .5));
        caster(f.pos, sun, strength, { blindfold: 1 - pull, sign, rim, crossed: true, alpha: pop });
        return;
      }
      if (f.kind === 'walker') { pawn(f.pos, EnemyColour, sun, strength); return; }
      if (f.kind === 'mech') { mech(f.pos, sun, strength, pop); return; }
      if (f.kind === 'android') { android(f.pos, sun, strength, pop); return; }
      const colour = f.kind === 'colonist' ? Ally : EnemyColour;
      if (!f.back) { pawn(f.pos, colour, sun, strength); return; }
      // Back from the void.
      if (pl.spared.has(f.g.id)) { pawn(f.pos, colour, sun, strength, { alpha: pop }); touchPulse(`uv open spared ${f.g.id}`, f.pos, .6 + backAge); return; }
      const beaten = pl.downed.has(f.g.id);
      const lie = beaten ? 1 : smooth(clamp((s - t.fall) / Fall));
      const sway = beaten ? 0 : .03 * Math.sin(backAge * 7 + f.g.at[0]) * (1 - lie);
      if (lie < 1) pawn({ x: f.pos.x + sway, z: f.pos.z }, colour, sun, strength, { alpha: pop * (1 - lie), tint: Frozen, tintAmount: .45, outline: .35 });
      if (lie > 0) { pawn(f.pos, colour, sun, strength, { lie: true, alpha: pop * lie, tint: Frozen, tintAmount: .25 }); overloadMark(`uv open mark ${f.g.id}`, f.pos, s, pop * lie); }
    });
    if (returned && imm.fighting) brawl('uv open brawl', off(imm.mech), off(imm.android), backAge, 1);

    // --- warm-up: light gathers at the raised hand -------------------------------------------------------------
    if (s >= t.cast && s < t.open) {
      const w = (s - t.cast) / p.warm, hand = { x: o.x + .07, z: o.z + .6 };
      sprite(hand, .35 + .6 * w, .35 + .6 * w, EyeBlue.withAlpha(.55 * w), glow, Y + .05);
      glint('uv open gather glint', hand, .08 + .2 * w, w, Ice);
      for (let i = 0; i < Gather; i++) {
        const v = (w * 1.5 + i / Gather) % 1, ang = (i * 45 + 20) * Deg, r0 = .9 * (1 - v) + .1, r1 = r0 + .25 * (1 - v);
        streak(`uv open gather ${i}`, { x: hand.x + Math.cos(ang) * r0, z: hand.z + Math.sin(ang) * r0 }, { x: hand.x + Math.cos(ang) * r1, z: hand.z + Math.sin(ang) * r1 },
          .035, Violet.withAlpha(.9 * Math.sin(v * Math.PI)), whiteGlow, Y + .051, 2);
      }
    }
  },
};
