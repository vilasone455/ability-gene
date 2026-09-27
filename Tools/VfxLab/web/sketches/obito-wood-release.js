// Obito — Wood Release: Cutting Technique. A picture for the agreed mechanic; nothing in
// Source/RimArt draws it yet.
//
// Mechanic (agreed 2026-09-27, numbers are XML placeholders): branches shoot from his right arm
// along a straight line up to 10 cells and skewer every pawn on it, allies included: 15 Stab each,
// 30 % armour penetration. Warm-up 0.5 s, cooldown 10 s, cast 2 Echo charge. No pin (pinning is
// Pain's Black Receiver): the branches crumble at once and the pawns stay free. The hit area is
// the 1-cell line from the next cell out to 10 cells.
//
// Beats (source: Obito's right side is rebuilt from Hashirama cells, so the wood grows out of his
// white right arm; Cutting Technique branches race out and skewer):
//   0.00-0.50  warm-up: his right arm comes up to point down the line; bark creeps over it from the
//              shoulder and small twigs split off the forearm. The line shows on the floor while
//              targeting ("Show hit area").
//   0.50       fire: three strands twisting round each other race out at 40 cells/s. Side twigs
//              split off behind the front; at every pawn on the line two spikes fan out and pass
//              through it, pale points sticking out behind. Each pawn flinches and bleeds as the
//              front passes it (blood stays).
//   full + 0.25  the branches dry to grey and break from the hand outward; the pieces drop to the
//              floor and stay there as splinters. The bark leaves his arm.
//
// Drawing: lib/obito.js branch(). Every branch lies at one height (0.55 up), so it turns freely
// with the aim and needs no per-facing method; aiming north it draws under the pawns, as a blade
// pointing north does in the Vergil sketches. Stand-ins: pawns, blood.
import { Color } from '../js/engine.js';
import {
  P, L, Lift, Edge, Blood, White, smooth, clamp, lerp, rand, figure, obitoKind, facingOf, arm, branch,
  sprite, soft, band,
} from './lib/obito.js';

// Rule numbers (XML later) and decided looks.
const WarmUp = .5, Speed = 40, H = .55, Step = .2, Chunk = .45, Gravity = 6, Break = 30;
const Hand = { along: .48, across: -.13 };
const Strands = [{ w: .2, lag: 0, short: 0, from: 0 }, { w: .09, lag: .12, short: .3, from: .12 }, { w: .09, lag: .2, short: .5, from: .2 }];
const Hurt = new Color(1, .35, .35, 1);

function times(p) {
  const len = p.length + .5, fire = WarmUp, full = fire + (len - Hand.along) / Speed, crumble = full + p.hold;
  const settled = crumble + .12 + (len - Hand.along) / Break + Math.sqrt(H / Gravity);
  return { len, fire, full, crumble, settled, end: settled + .6 };
}
function pawns(p) {
  const on = p.scenario === 'ally on the line';
  return [
    { along: 2.5, across: 0, kind: 'enemy' }, { along: 5.5, across: 0, kind: on ? 'ally' : 'enemy' },
    { along: 8.5, across: 0, kind: 'enemy' }, { along: 4.2, across: 1.4, kind: 'enemy' },
  ].filter(q => q.along <= p.length + 1.5);
}
const onLine = (q, t) => Math.abs(q.across) < .5 && q.along >= .5 && q.along <= t.len;
const hitTime = (q, t) => t.fire + (q.along - Hand.along) / Speed;

export default {
  kit: 'Obito', label: 'Wood Release: Cutting Technique (sketch)',
  params: {
    aim: P('Aim (degrees)', 0, 0, 360, 5, 'Showcase'),
    scenario: { label: 'Pawns', value: 'three raiders', options: ['three raiders', 'ally on the line'], group: 'Showcase' },
    area: { label: 'Show hit area', value: true, group: 'Showcase' },
    length: P('Line length (cells)', 10, 4, 14, 1, 'Rule'),
    hold: P('Branches stay before they crumble', .25, 0, 1, .05, 'Timing (s)'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p);
    return [{ name: 'Warm-up: bark on the arm', t: 0 }, { name: 'Branches race out', t: t.fire }, { name: 'Crumble', t: t.crumble }];
  },
  events(p) {
    const t = times(p), first = pawns(p).filter(q => onLine(q, t)).map(q => hitTime(q, t));
    return first.length ? [{ t: Math.min(...first), type: 'shake', value: .05 }] : [];
  },

  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const a = p.aim * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a);
    const place = (along, across, h = 0) => ({ x: o.x + along * ca - across * sa, z: o.z + along * sa + across * ca + h * Lift });
    const layer = sa > .5 ? L.pawn - .03 : L.fx;
    const front = Hand.along + Math.max(0, s - t.fire) * Speed;

    // The line on the floor while targeting.
    if (p.area) {
      const k = .9 * (1 - smooth((s - t.fire) / .2));
      if (k > .01) {
        band('wood area', [place(.5, .5), place(t.len, .5)], [place(.5, -.5), place(t.len, -.5)], White.withAlpha(.1 * k), L.floor);
        band('wood area l', [place(.5, .5), place(t.len, .5)], [place(.5, .46), place(t.len, .46)], Edge.withAlpha(.5 * k), L.floor + .001);
        band('wood area r', [place(.5, -.46), place(t.len, -.46)], [place(.5, -.5), place(t.len, -.5)], Edge.withAlpha(.5 * k), L.floor + .001);
      }
    }

    // Pawns: those on the line flinch and bleed as the front passes them.
    for (const [i, q] of pawns(p).entries()) {
      const hit = onLine(q, t) ? s - hitTime(q, t) : -1;
      const push = hit >= 0 ? .1 * Math.sin(Math.PI * clamp(hit / .2)) : 0;
      const pos = place(q.along + push, q.across);
      const red = hit >= 0 ? 1 - clamp(hit / .3) : 0;
      if (hit >= 0) bleed(`wood ${i}`, pos, hit, a);
      figure(`wood pawn ${i}`, q.kind, pos, sun, strength, { tint: Color.Lerp(White, Hurt, .7 * red) });
    }

    // Obito and his right arm: up to point, bark over it, twigs off the forearm.
    const facing = facingOf(p.aim);
    figure('wood obito', obitoKind(facing), o, sun, strength);
    const raise = smooth(s / .15) * (1 - smooth((s - t.settled) / .3)), bark = smooth((s - .05) / .35) * (1 - smooth((s - t.crumble - .1) / .3));
    const shoulder = place(.05, -.17, .62), hand = place(lerp(.15, Hand.along, raise), lerp(-.2, Hand.across, raise), H);
    arm('wood', shoulder, hand, sa > .5 ? L.pawn - .01 : L.pawn + .01, { wood: bark, alpha: raise });

    // Every piece of wood: strands, side twigs, skewer spikes. Each is a list of (along, across)
    // points on the aim frame, a width, and the time it starts growing.
    const pieces = [];
    const line = (from, to, acrossAt, grow = front) => {
      const pts = [], end = Math.min(to, grow);
      if (end <= from + .02) return pts;
      for (let x = from; x < end; x += Step) pts.push([x, acrossAt(x)]);
      pts.push([end, acrossAt(end)]);
      return pts;
    };
    const settle = x => Math.exp(-(x - Hand.along) / .55);
    const main = x => Hand.across * settle(x) + .04 * Math.sin(x * 1.9 + 1.3) * (1 - settle(x));
    Strands.forEach((st, k) => {
      const across = x => main(x) + (k ? (k === 1 ? 1 : -1) * .085 * Math.sin(x * 2.6 + k * 1.7) * (1 - settle(x) * .7) : 0);
      pieces.push({ key: `strand ${k}`, pts: line(Hand.along + st.from, t.len - st.short, across, front - st.lag), w: st.w, tip: .35 });
    });
    // Twigs splitting off the forearm during the warm-up.
    [[.25, 1, .14], [.32, -1, .12], [.4, 1, .17]].forEach(([at, side, len], j) => {
      const g = smooth((s - .22 - j * .07) / .15), ang = 55 * Math.PI / 180, base = [at + .05, lerp(-.17, Hand.across, at / Hand.along)];
      if (g > 0) pieces.push({ key: `arm twig ${j}`, pts: [base, [base[0] + Math.cos(ang) * len * g, base[1] + side * Math.sin(ang) * len * g]], w: .04, tip: .08 });
    });
    for (let j = 0; ; j++) {
      const b = 1 + j * 1.05 + (rand(j * 3 + 1) - .5) * .35;
      if (b > t.len - .6) break;
      const side = rand(j * 3 + 2) < .5 ? 1 : -1, ang = (22 + rand(j * 3 + 3) * 20) * Math.PI / 180, len = .45 + rand(j * 3 + 4) * .85;
      const g = smooth((s - t.fire - (b - Hand.along) / Speed) / .1);
      if (g <= 0) continue;
      const base = [b, main(b)], dir = [Math.cos(ang), side * Math.sin(ang)], l = len * g;
      pieces.push({ key: `twig ${j}`, pts: [base, [base[0] + dir[0] * l * .5, base[1] + dir[1] * l * .5 - side * .03], [base[0] + dir[0] * l, base[1] + dir[1] * l]], w: .07, tip: .2 });
    }
    pawns(p).filter(q => onLine(q, t)).forEach((q, i) => {
      for (const side of [1, -1]) {
        const start = q.along - .75, across = x => main(x) + side * lerp(0, .2, clamp((x - start) / 1.2));
        const pts = line(start, q.along + .45, across);
        if (pts.length) pieces.push({ key: `spike ${i} ${side}`, pts, w: .075, tip: .25 });
      }
    });

    // Intact until the crumble; then dry, break from the hand outward, drop, and lie as splinters.
    const dry = smooth((s - t.crumble) / .15), broken = s >= t.crumble + .12;
    for (const pc of pieces) {
      if (pc.pts.length < 2) continue;
      if (!broken) {
        branch(`wood ${pc.key}`, pc.pts.map(([x, y]) => place(x, y, H)), pc.w, { h: H, sun, strength, layer, tipLen: pc.tip, dry });
        continue;
      }
      const chunks = pc.key.startsWith('strand') ? split(pc.pts) : [pc.pts];
      chunks.forEach((c, ci) => {
        const mid = c[Math.floor(c.length / 2)], age = s - t.crumble - .12 - (mid[0] - Hand.along) / Break;
        const fall = Math.max(0, age), h = Math.max(0, H - Gravity * fall * fall), down = h === 0;
        const r = rand(ci * 7 + pc.key.length * 13), turn = (r - .5) * .7 * clamp(fall / .3), drift = (rand(ci * 5 + 2) - .5) * .25 * clamp(fall / .3);
        const pts = c.map(([x, y]) => {
          const dx = x - mid[0], dy = y - mid[1];
          return place(mid[0] + dx * Math.cos(turn) - dy * Math.sin(turn), mid[1] + drift + dx * Math.sin(turn) + dy * Math.cos(turn), h);
        });
        branch(`wood ${pc.key} ${ci}`, pts, pc.w * (down ? .8 : 1), {
          h, sun, strength: down ? 0 : strength, layer: down ? L.floor + .002 : layer, tipLen: ci === chunks.length - 1 ? pc.tip : .03, dry: 1, alpha: down ? .85 : 1,
        });
      });
    }
  },
};

// A strand cut into pieces about Chunk cells long.
function split(pts) {
  const out = [];
  let cur = [pts[0]];
  for (let i = 1; i < pts.length; i++) {
    cur.push(pts[i]);
    if (pts[i][0] - cur[0][0] >= Chunk || i === pts.length - 1) { out.push(cur); cur = [pts[i]]; }
  }
  return out.filter(c => c.length >= 2);
}

// Blood thrown out behind the pawn along the aim; drops land and stay.
function bleed(key, pos, age, aim) {
  const chest = { x: pos.x, z: pos.z + .3 };
  for (let i = 0; i < 7; i++) {
    const ang = aim + (rand(i * 3 + 40) - .5) * 1.2, speed = 1.6 + rand(i * 3 + 41) * 1.8, life = .28 + rand(i * 3 + 42) * .1;
    const u = Math.min(age, life), d = speed * u, h = .45 + 1.1 * u - 5 * u * u;
    const x = chest.x + Math.cos(ang) * d, z = pos.z + Math.sin(ang) * d;
    if (age < life) sprite({ x, z: z + Math.max(0, h) * Lift }, .07, .07, Blood, soft, L.fx + .02);
    else sprite({ x, z }, .13, .09, Blood.withAlpha(.85), soft, L.floor);
  }
  const pool = smooth(age / .4);
  sprite({ x: pos.x + Math.cos(aim) * .2, z: pos.z + Math.sin(aim) * .2 - .05 }, .42 * pool, .26 * pool, Blood.withAlpha(.7), soft, L.floor - .001);
}
