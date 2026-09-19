// Obsidian Shears — weapon form proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed, none of it agreed). A weapon form made of two orbs; the other four
// stay free for techniques.
//   held    one pair of shears in the hand. Normal attack: 12 Cut every 1.4 s at 45% armour
//           penetration, and hits go to hands, arms and legs first, so it downs enemies without
//           killing them. The sketch shows an arm cut, then a leg cut that drops the enemy.
//   active  Disarm. Target one pawn within 12 cells in line of sight. The shears leave the hand,
//           fly out closed at hand height, open at the target (0.35 s), follow the pawn while they
//           line up on its weapon hand (0.35 s), and snip shut in 0.10 s. About 22 Cut to the
//           weapon hand or arm at about 50% armour penetration, and the pawn is forced to drop its
//           weapon. No locked location and no dodge: the shears track the pawn. A pawn with no
//           held weapon (animal, mech) takes the limb damage only. The sage has no weapon while
//           the shears are away, about 2.3 s. About 40 s cooldown.
//
// Drawing: one pair of scissors with a shared pivot, handles and finger rings, lying flat at hand
// height with a ground shadow. A flat shape turns freely, so there is no per-facing drawing: the
// same routine draws it held, swung at a melee enemy, in flight and at the target. The dropped
// weapon is thrown clear and stays on the ground so the result is visible. Pawns and weapon are
// stand-ins.
import { AltitudeLayer, Color, Mathf, Meshes } from '../js/engine.js';
import { draw, Slate, Lift } from './lib/six-paths-solid.js';
import { P, Body, Rim, Y, Floor, orb, sprite, trail, band, circle, glow, soft, rand } from './lib/six-paths-impact.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const disc = Meshes.disc(32, 'shears disc');
const loop = Meshes.band(.58, 1, 32, 'shears finger ring');
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor();
const pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const pale = new Color(.92, .82, 1);
const steel = new Color(.20, .20, .23);
// Decided looks and the real attack interval, kept out of the panel.
const AttackGap = 1.4, RestAngle = -.45, LegHeight = .15, MeleeReach = .05;

function times(p) {
  const attack = p.morph + .3, deploy = attack + p.attacks * AttackGap + (p.attacks ? .2 : 0);
  const arrive = deploy + p.deploy, formed = arrive + p.form, snip = formed + p.aimTime, shut = snip + p.snip;
  const recall = shut + p.hold, home = recall + p.return;
  return { attack, deploy, arrive, formed, snip, shut, recall, home, end: home + .6 };
}

export default {
  kit: 'Six Paths', label: 'Obsidian Shears (sketch)',
  params: {
    attacks: P('Normal attacks shown first (1.4 s apart)', 2, 0, 3, 1, 'Showcase'),
    moving: { label: 'Disarm target walks (shears track it)', value: true, group: 'Showcase' },
    actors: { label: 'Show pawns and weapon', value: true, group: 'Showcase' },
    aim: P('Aim direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Disarm target distance (cells)', 5, 3, 12, .5, 'Showcase'),
    morph: P('Two orbs form the shears', .5, .25, 1, .05, 'Timing (s)'),
    deploy: P('Shears fly to the target', .45, .2, 1, .05, 'Timing (s)'),
    form: P('Open at the target', .35, .15, .8, .05, 'Timing (s)'),
    aimTime: P('Line up on weapon hand', .35, .15, .8, .05, 'Timing (s)'),
    snip: P('Snip', .10, .05, .25, .01, 'Timing (s)'),
    hold: P('Hold shut', .2, .05, .5, .05, 'Timing (s)'),
    return: P('Shears fly back to the hand', .5, .2, 1, .05, 'Timing (s)'),
    length: P('Blade length (cells)', 1.25, .9, 1.8, .05, 'Shape'),
    width: P('Blade width (cells)', .2, .12, .3, .01, 'Shape'),
    open: P('Open half-angle (degrees)', 34, 20, 50, 1, 'Shape'),
    hover: P('Hand height (cells)', .45, .2, .9, .05, 'Shape'),
    flash: P('Cut flash strength', .8, 0, 1, .05, 'Feedback'),
    shake: P('Disarm camera shake', .07, 0, .2, .01, 'Feedback'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Form in hand', t: 0 }, ...(p.attacks ? [{ name: 'Normal attacks', t: t.attack }] : []),
    { name: 'Disarm: fly out', t: t.deploy }, { name: 'Open', t: t.arrive }, { name: 'Line up', t: t.formed },
    { name: 'Snip / disarm', t: t.snip }, { name: 'Hold', t: t.shut }, { name: 'Fly back', t: t.recall },
    { name: 'Held again', t: t.home },
  ]; },
  events(p) { return p.shake ? [{ t: times(p).shut, type: 'shake', value: p.shake }] : []; },
  draw(s, p, { origin: o, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 };
    const strength = scene?.sun?.strength ?? .32;
    const a0 = p.aim * Mathf.Deg2Rad, ca = Math.cos(a0), sa = Math.sin(a0);
    // Aim frame: a runs from caster to target, b across it, h is height above the floor.
    const place = (base, a, b, h = 0) => ({ x: base.x + a * ca - b * sa, z: base.z + a * sa + b * ca + h * Lift });
    const cast = (base, a, b, h = 0) => ({ x: base.x + a * ca - b * sa + sun.x * h, z: base.z + a * sa + b * ca + sun.z * h });

    const caster = place(o, -p.distance, 0);
    // The disarm target walks across the aim until the cut lands; the shears work relative to it.
    const walked = p.moving ? Math.max(0, Math.min(s, t.shut) - t.deploy) * .8 - .8 : 0;
    const flinch = Math.exp(-Math.max(0, s - t.shut) * 9) * (s >= t.shut ? .1 : 0);
    const target = place(o, flinch, walked);
    // The hand and the melee enemy, written in the target's frame like everything else.
    const hand = { a: -p.distance - flinch + .3, b: -walked }, foe = { a: -p.distance - flinch + .25, b: -walked - 1.15 };
    const foeAngle = Math.atan2(foe.b - hand.b, foe.a - hand.a);

    // Normal attacks: wind up toward the enemy, snip, recover. Odd attacks go low, for a leg.
    let engage = 0, meleeCut = 0, low = 0, reach = 0;
    for (let i = 0; i < p.attacks; i++) {
      const at = t.attack + .25 + i * AttackGap;
      const e = smooth((s - at + .25) / .2) * (1 - smooth((s - at - .15) / .3));
      if (e > engage) { engage = e; meleeCut = clamp((s - at) / .08) ** 3; low = i % 2; }
      reach = Math.max(reach, Math.sin(clamp((s - at + .1) / .25) * Math.PI));
    }

    const form = smooth(s / p.morph);
    const travel = smooth((s - t.deploy) / p.deploy) * (1 - smooth((s - t.recall) / p.return));
    const turned = smooth((s - t.deploy) / (p.deploy * .4)) * (1 - smooth((s - t.recall - p.return * .6) / (p.return * .4)));
    const opened = smooth((s - t.arrive) / p.form), lining = smooth((s - t.formed) / p.aimTime);
    const cut = clamp((s - t.snip) / p.snip) ** 3;
    const open = p.open * Mathf.Deg2Rad;
    const thetaActive = lerp(.05, lerp(lerp(open * .7, open, lining), .025, cut), opened);
    const thetaHeld = lerp(.06, lerp(open * .8, .03, meleeCut), engage);
    const theta = s >= t.deploy && s < t.home ? thetaActive : thetaHeld;
    const axis = lerp(lerp(RestAngle, foeAngle, engage), 0, turned);
    const hov = lerp(p.hover, LegHeight, engage * low);
    const lunge = Math.sin(clamp((s - t.snip) / (p.snip + .12)) * Math.PI) * .12;
    const pivotA = lerp(-p.length * .95, -p.length * .8, lining) + lunge;
    const piv = { a: lerp(hand.a + Math.cos(foeAngle) * MeleeReach * reach, pivotA, travel), b: lerp(hand.b + Math.sin(foeAngle) * MeleeReach * reach, 0, travel) };
    const local = (a, b) => ({ a: piv.a + a, b: piv.b + b });

    const figure = (pos, colour, bob = 0, down = false) => {
      sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, .85, .4, Body.withAlpha(strength), soft, shadowLayer);
      if (down) {
        draw(disc, pos.x + .05, pawnLayer, pos.z + .05, .32, .2, 0, colour);
        draw(disc, pos.x - .36, pawnLayer + .002, pos.z + .08, .17, .16, 0, new Color(.83, .70, .54));
      } else {
        draw(disc, pos.x, pawnLayer, pos.z + .18 + bob, .22, .32, 0, colour);
        draw(disc, pos.x, pawnLayer + .002, pos.z + .58 + bob, .16, .17, 0, new Color(.83, .70, .54));
      }
    };
    if (p.actors) {
      figure(caster, new Color(.39, .58, .65));
      figure(target, new Color(.55, .38, .27), p.moving && s > t.deploy && s < t.shut ? Math.abs(Math.sin(s * 9)) * .05 : 0);
      if (p.attacks) {
        // The melee enemy flinches on each cut and goes down on a leg cut: limbs first.
        const lastLeg = p.attacks >= 2 ? t.attack + .25 + AttackGap : Infinity;
        let jolt = 0;
        for (let i = 0; i < p.attacks; i++) { const age = s - (t.attack + .25 + i * AttackGap); if (age >= 0) jolt = Math.max(jolt, Math.exp(-age * 9) * .1); }
        figure(place(target, foe.a - jolt * Math.cos(foeAngle), foe.b - jolt * Math.sin(foeAngle)), new Color(.62, .45, .22), 0, s > lastLeg + .2);
      }
      // Weapon: held along the aim, pointing at the caster, then thrown clear and left lying.
      const fly = clamp((s - t.shut + p.snip * .3) / .45), landed = fly >= 1;
      const wa = lerp(-.3, -.55, fly), wb = lerp(0, 1.3, smooth(fly));
      const wh = landed ? 0 : lerp(.42, 0, fly) + Math.sin(fly * Math.PI) * .9;
      const spin = (fly > 0 ? lerp(0, 935, fly) : 0) * Mathf.Deg2Rad;
      const base = fly > 0 ? place(o, 0, walked) : target;
      const quad = (key, centreA, halfLen, halfWid, colour, layer, project) => {
        const c = Math.cos(spin), n = Math.sin(spin), pts = [[-1, -1], [1, -1], [-1, 1], [1, 1]].map(([l, w]) =>
          project(base, wa + centreA * c + l * halfLen * c - w * halfWid * n, wb + centreA * n + l * halfLen * n + w * halfWid * c, wh));
        band(key, [pts[0], pts[1]], [pts[2], pts[3]], colour, layer);
      };
      quad('shears weapon shadow', 0, .36, .05, Body.withAlpha(strength * .7), shadowLayer + .003, cast);
      quad('shears weapon barrel', 0, .36, .04, steel, landed ? Floor + .02 : Y + .04, place);
      quad('shears weapon stock', .24, .13, .075, new Color(.30, .22, .16), landed ? Floor + .021 : Y + .041, place);
      const dust = s - t.shut + p.snip * .3 - .45;
      if (dust >= 0 && dust < .4)
        sprite(place(base, wa, wb), .5 + dust * 1.2, .3 + dust * .7,
          new Color(.56, .49, .40, Math.sin(dust / .4 * Math.PI) * .4), soft, shadowLayer + .02);
    }

    // Two orbs spiral into the hand and become the shears.
    if (form < 1) for (const side of [-1, 1]) {
      const r = (1 - form) * .7, a = s * 9 + side * 1.57, c = place(target, hand.a, hand.b, p.hover);
      orb({ x: c.x + Math.cos(a) * r, z: c.z + Math.sin(a) * r * .5 }, .22 * (1 - form * .85));
    }

    if (form > .001) {
      const L = p.length * form, n = 20;
      for (const side of [-1, 1]) {
        const phi = axis + side * theta, dA = Math.cos(phi), dB = Math.sin(phi), nA = -side * Math.sin(phi), nB = side * Math.cos(phi);
        const widthAt = (u) => p.width * form * (1 - u) ** .75 * (.75 + .25 * Math.sin(u * Math.PI));
        const strip = (project, inner, outer) => {
          const A = [], B = [];
          for (let j = 0; j <= n; j++) {
            const u = j / n, w = widthAt(u), i = inner(w), e = outer(w), q = local(dA * u * L, dB * u * L);
            A.push(project(target, q.a + nA * i, q.b + nB * i, hov));
            B.push(project(target, q.a + nA * e, q.b + nB * e, hov));
          }
          return [A, B];
        };
        const layer = Y + .10 + (side > 0 ? .01 : 0), key = 'shears blade ' + side;
        band(key + ' shadow', ...strip(cast, () => 0, w => w), Body.withAlpha(strength * .65), shadowLayer + .001);
        band(key + ' edge', ...strip(place, () => -.024, w => w + .024), Rim.withAlpha(.9), layer);
        band(key + ' body', ...strip(place, () => 0, w => w), Body, layer + .001);
        band(key + ' facet', ...strip(place, w => w * .42, w => w * .72), Color.Lerp(Body, Slate, .55), layer + .002);
        const [cutting] = strip(place, () => .012, () => 0);
        trail(key + ' cutting edge', cutting, .03, pale.withAlpha(.35 + .45 * Math.max(lining * (1 - cut), engage * (1 - meleeCut))), layer + .003);
        // Handle runs straight through the pivot to the other side, as on real scissors.
        const H = .4 * form, h0 = local(0, 0), h1 = local(-dA * H, -dB * H);
        const hw = .035 * form;
        band(key + ' handle edge',
          [place(target, h0.a + nA * (hw + .02), h0.b + nB * (hw + .02), hov), place(target, h1.a + nA * (hw + .02), h1.b + nB * (hw + .02), hov)],
          [place(target, h0.a - nA * (hw + .02), h0.b - nB * (hw + .02), hov), place(target, h1.a - nA * (hw + .02), h1.b - nB * (hw + .02), hov)],
          Rim.withAlpha(.9), layer);
        band(key + ' handle',
          [place(target, h0.a + nA * hw, h0.b + nB * hw, hov), place(target, h1.a + nA * hw, h1.b + nB * hw, hov)],
          [place(target, h0.a - nA * hw, h0.b - nB * hw, hov), place(target, h1.a - nA * hw, h1.b - nB * hw, hov)],
          Body, layer + .001);
        const r = local(-dA * (H + .13 * form) - nA * .05, -dB * (H + .13 * form) - nB * .05);
        const ringAt = place(target, r.a, r.b, hov), ringShadow = cast(target, r.a, r.b, hov);
        draw(loop, ringShadow.x, shadowLayer + .001, ringShadow.z, .17 * form, .17 * form, 0, Body.withAlpha(strength * .65));
        draw(loop, ringAt.x, layer, ringAt.z, .19 * form, .19 * form, 0, Rim.withAlpha(.9));
        draw(loop, ringAt.x, layer + .001, ringAt.z, .165 * form, .165 * form, 0, Body);
        // A glint runs down each cutting edge while the shears line up for the disarm.
        if (lining > 0 && cut <= 0) {
          const u = 1 - lining, g = local(dA * u * L, dB * u * L);
          sprite(place(target, g.a, g.b, hov), .34, .34, pale.withAlpha(Math.sin(lining * Math.PI) * .8), glow, Y + .13);
        }
      }
      const rivet = place(target, piv.a, piv.b, hov);
      draw(disc, rivet.x, Y + .12, rivet.z, .075 * form, .075 * form, 0, Rim);
      draw(disc, rivet.x, Y + .121, rivet.z, .04 * form, .04 * form, 0, pale.withAlpha(.9));
    }

    // A cut: a pale line across the limb, sparks, and a small floor ring. The disarm is the big one.
    const slash = (key, a, b, h, age, size) => {
      if (age < 0 || age >= .25) return;
      const f = (1 - age / .25) ** 2, pos = place(target, a, b, h);
      sprite(pos, 1.5 * size * (.6 + age * 2), .07 * f, pale.withAlpha(f * p.flash), glow, Y + .14, -p.aim + 90 - (size < 1 ? foeAngle * Mathf.Rad2Deg + 90 : 0));
      sprite(pos, .7 * size, .7 * size, pale.withAlpha(f * p.flash * .8), glow, Y + .14);
      circle(place(target, a, b), (.2 + age * 2.2) * size, f * .5);
      for (let i = 0; i < 7; i++) {
        const dir = (rand(i + 3) - .5) * 2.4 + (i % 2 ? Math.PI / 2 : -Math.PI / 2), far = age * (3 + rand(i) * 3) * size;
        const from = place(target, a + Math.cos(dir) * far * .6, b + Math.sin(dir) * far * .6, h);
        const to = place(target, a + Math.cos(dir) * far, b + Math.sin(dir) * far, h);
        trail(`${key} spark ${i}`, [from, { x: (from.x + to.x) / 2, z: (from.z + to.z) / 2 }, to], .05 * f, pale.withAlpha(f * p.flash), Y + .15);
      }
    };
    slash('shears disarm', -.12, 0, p.hover, s - t.shut, 1);
    for (let i = 0; i < p.attacks; i++)
      slash('shears melee ' + i, foe.a - Math.cos(foeAngle) * .15, foe.b - Math.sin(foeAngle) * .15, i % 2 ? LegHeight : p.hover, s - (t.attack + .25 + i * AttackGap), .6);
  },
};
