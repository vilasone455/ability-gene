// Tag Line — Paper Bomb weapon proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed, none of it agreed; every number is a placeholder). The pawn holds a
// tag scroll: a roll of explosive tags. Tag Line targets a cell within 8 tiles. Over 2.5 s the
// pawn flicks the roll and a paper strip shoots out along the floor from its feet to that cell,
// one tag per cell, up to 8, after 2.5 cells of plain strip that keep the first burst off the
// pawn. The roll stays in the hand; the strip tears off. The tags stay for
// 1 day. A gizmo on the pawn (a hand seal, 0.35 s) lights the near end. The fuse runs down the
// strip at 0.1 s per tag and each tag bursts as it is reached: 30 Bomb damage, radius 1.1, allies
// included. Costs 1 tag per cell. Cooldown 20 s. Role: a line charge laid along a corridor before
// a raid. Frost Bomb is a circle that slows, makibishi a 3x3 patch that slows, the Toy Car one
// mobile charge; none is a line and none is laid ahead of time.
//
// Drawing: everything lies flat on the floor or is a level circle, so it turns with the aim and
// needs no per-facing method. The strip is one band mesh rebuilt while it moves: a wave, a lift
// and a twist that die out 0.45 s after the leading edge has passed a point. Tag panels are quads
// placed on the strip's own tangent. The tag, the burst (soft additive layers rising from the floor, a
// floor ring at the true radius, rocks and charred scraps that stay) and the roll are in lib/paper-bomb.js. Pawns, walls and the roll in
// the hand are stand-ins. The enemies walking in only show what the blast does.
import { Mathf } from '../js/engine.js';
import { P, Y, Floor, Lift, sprite, glow, rand } from './lib/six-paths-impact.js';
import { strip, streak, whiteGlow, pawn, shadowLayer, Ally, EnemyColour, Ink } from './lib/goku.js';
import { Paper, PaperEdge, Ember, Hot, Seal, Burn, tag, roll, sealFlash, burst, walls } from './lib/paper-bomb.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
// Decided shape and rule numbers. Sliders are only for what is still being tuned.
const Flick = .3;            // wind-up before the strip leaves the hand
const Settle = .45;          // a point on the strip lies flat this long after the leading edge passed it
const Wave = .22, Rise = .3, HandHeight = .38, Step = .2;
const Leader = 2.5, HandOut = .35; // plain strip between the caster and tag 0, so the first burst (radius 1.1) does not reach the caster
const fuseCells = i => Leader - HandOut + i; // strip length the fuse burns before it reaches tag i

function times(p) {
  const n = Math.round(p.cells), fuse = p.lay + p.wait, last = fuse + fuseCells(n - 1) * p.per + Burn;
  return { n, fuse, last, end: last + p.hold };
}

export default {
  kit: 'Paper Bomb', label: 'Tag Line (sketch)',
  params: {
    actors: { label: 'Show caster and enemies', value: true, group: 'Showcase' },
    corridor: { label: 'Show corridor walls', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    cells: P('Tags laid (cells)', 8, 3, 8, 1, 'Rule'),
    radius: P('Blast radius per tag (cells)', 1.1, .5, 2, .1, 'Rule'),
    per: P('Fuse time per tag', .1, .04, .4, .01, 'Timing (s)'),
    lay: P('Strip laid in', 2.5, 1, 4, .1, 'Timing (s)'),
    wait: P('Armed, enemies walk in', 1.8, .6, 4, .1, 'Timing (s)'),
    hold: P('Aftermath held', 2.2, .5, 5, .1, 'Timing (s)'),
    width: P('Strip width (cells)', .34, .2, .6, .02, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Flick', t: 0 }, { name: 'Strip runs out', t: Flick }, { name: 'Armed', t: p.lay },
    { name: 'Hand seal', t: t.fuse - Seal }, { name: 'Fuse and bursts', t: t.fuse }, { name: 'Aftermath', t: t.last },
  ]; },
  events(p) { const t = times(p); return Array.from({ length: t.n }, (_, i) => ({ t: t.fuse + fuseCells(i) * p.per + Burn, type: 'shake', value: .1 })); },

  draw(s, p, { origin: o, scene }) {
    const t = times(p), n = t.n;
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a);
    const place = (along, across, h = 0) => ({ x: o.x + along * ca - across * sa, z: o.z + along * sa + across * ca + h * Lift });
    const cast = (along, across, h = 0) => ({ x: o.x + along * ca - across * sa + sun.x * h, z: o.z + along * sa + across * ca + sun.z * h });
    // Caster to last tag is centred on the chosen cell. cell(i) is tag i along the aim; the caster stands Leader cells before tag 0.
    const cell = i => i - (n - 1) / 2 + (Leader - 1) / 2, casterAlong = cell(0) - Leader, a0 = casterAlong + HandOut, aEnd = cell(n - 1) + .42;
    const burstAt = i => t.fuse + fuseCells(i) * p.per + Burn;

    if (p.corridor) { const pts = []; for (let k = Math.floor(casterAlong) - 1; k <= cell(n - 1) + 5; k += .5) pts.push(place(k, 3), place(k, -3)); walls('tag line wall', o, pts, sun, strength); }

    // Leading edge of the strip. It slows as it runs out, and passed() is the inverse: when the edge reached a.
    const run = p.lay - Flick, x = clamp((s - Flick) / run), front = lerp(a0, aEnd, 1 - (1 - x) * (1 - x));
    const passed = al => Flick + run * (1 - Math.sqrt(1 - clamp((al - a0) / (aEnd - a0))));
    const torn = smooth((s - p.lay - .1) / .2);
    const loose = al => (1 - smooth((s - passed(al)) / Settle)) * smooth((al - a0) / .8);
    const shape = al => { const e = loose(al); return {
      side: Wave * e * Math.sin(5 * al - 16 * s),
      h: Rise * e * (.6 + .4 * Math.sin(7 * al - 20 * s + 1)) + HandHeight * (1 - smooth((al - a0) / .9)) * (1 - torn),
      twist: 1 - .7 * e * Math.abs(Math.sin(3 * al - 11 * s)) }; };
    const point = (al, across = 0) => { const f = shape(al); return place(al, across * f.twist + f.side, f.h); };

    // Where the fuse is. Everything behind it has burned away.
    const fuseAlong = s < t.fuse ? -99 : a0 + (s - t.fuse) / p.per;
    const start = Math.max(a0 + .25 * torn, fuseAlong);

    // The strip: shadow, dark edge, paper.
    if (s >= Flick && start < Math.min(front, aEnd)) {
      const edgeL = [], edgeR = [], left = [], right = [], shL = [], shR = [], half = p.width / 2;
      for (let al = start; ; al = Math.min(front, al + Step)) {
        const f = shape(al), w = half * f.twist;
        left.push(place(al, w + f.side, f.h)); right.push(place(al, -w + f.side, f.h));
        edgeL.push(place(al, w + .03 + f.side, f.h)); edgeR.push(place(al, -w - .03 + f.side, f.h));
        shL.push(cast(al, w + f.side, f.h)); shR.push(cast(al, -w + f.side, f.h));
        if (al >= front) break;
      }
      strip('tag line shadow', shL, shR, Ink.withAlpha(strength * .8), undefined, shadowLayer);
      strip('tag line edge', edgeL, edgeR, PaperEdge, undefined, Floor + .01);
      strip('tag line paper', left, right, Paper, undefined, Floor + .012);
    }

    // The tags printed on the strip, one per cell. heat rises as the fuse nears; curl is the 0.12 s before the burst.
    for (let i = 0; i < n; i++) {
      const c = cell(i), reach = t.fuse + fuseCells(i) * p.per;
      if (front < c + .38 || s >= burstAt(i)) continue;
      const heat = clamp((s - (reach - .15)) / .15), curl = clamp((s - reach) / Burn), armed = s >= p.lay && s < t.fuse ? .5 + .5 * Math.sin(s * 4 - i * .5) : 0;
      const pa = point(c - .3), pb = point(c + .3), mid = point(c), f = shape(c);
      const deg = Math.atan2(pb.z - pa.z, pb.x - pa.x) * 57.29578, long = Math.hypot(pb.x - pa.x, pb.z - pa.z) / .6;
      tag(mid, deg, { long, wide: f.twist, heat, curl, armed });
    }

    // The fuse: an ember on the burning end of the strip.
    if (s >= t.fuse && fuseAlong < aEnd) {
      const e = place(fuseAlong, 0);
      sprite(e, .7, .55, Ember.withAlpha(.9), glow, Y + .03); sprite(e, .25, .2, Hot, glow, Y + .031);
      for (let k = 0; k < 4; k++) { const r = rand(Math.floor(s * 45) * 7 + k), ang = r * 6.283, l = .15 + .3 * rand(Math.floor(s * 45) + k + 3);
        streak(`tag line fuse ${k}`, e, { x: e.x + Math.cos(ang) * l, z: e.z + Math.abs(Math.sin(ang)) * l }, .04, Hot.withAlpha(.9), whiteGlow, Y + .032, 3); }
    }

    // Bursts, far ones first so nearer ones overlap them.
    const order = Array.from({ length: n }, (_, i) => i).sort((i, j) => place(cell(j), 0).z - place(cell(i), 0).z);
    for (const i of order) burst(`tag line burst ${i}`, place(cell(i), 0), s - burstAt(i), p.radius, sun, strength, i * 200 + 7);

    if (!p.actors) return;
    // Caster: one arm up while the strip runs out, both up for the hand seal. The roll stays in the hand.
    const caster = place(casterAlong, 0), sealing = smooth((s - (t.fuse - Seal)) / .12) * (1 - smooth((s - t.fuse - .3) / .3)), laying = smooth(s / Flick) * (1 - smooth((s - p.lay) / .3));
    pawn(caster, Ally, sun, strength, { arms: sealing > 0 ? 2 : 1, raise: Math.max(sealing, laying * .7) });
    const hand = place(a0 - .05, 0, HandHeight), spin = s > Flick && s < p.lay ? Math.sin(s * 40) * .02 : 0;
    roll(hand, p.aim, p.width, spin);
    sealFlash('tag line seal', caster, sealing);

    // Three enemies walk down the corridor onto the line while it is armed, and are downed by the tag under them.
    [.25, .55, .85].map(f => Math.min(n - 1, Math.round(f * (n - 1)))).filter((v, k, all) => all.indexOf(v) === k).forEach((idx, j) => {
      const walk = smooth((s - p.lay - .1) / (p.wait - .4)), along = lerp(cell(idx) + 4.5, cell(idx), walk), age = s - burstAt(idx), side = j % 2 ? 1 : -1;
      if (age < 0) { pawn(place(along, side * .08, 0), EnemyColour, sun, strength); return; }
      pawn(place(cell(idx) + .2 * smooth(age / .3), side * .75 * smooth(age / .3)), EnemyColour, sun, strength, { lie: age > .1, tint: Hot, tintAmount: clamp(1 - age / .25) });
    });
  },
};
