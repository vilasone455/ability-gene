// Obsidian Shears — disarm proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed, none of it agreed). Target one pawn within 12 cells in line of sight.
// Two orbs fly out at hand height, join at a pivot into one pair of shears (0.35 s), follow the
// pawn while they line up on its weapon hand (0.35 s), and snip shut in 0.10 s. About 22 Cut to
// the weapon hand or arm at about 50% armour penetration, and the pawn is forced to drop its
// weapon. No locked location and no dodge: the shears track the pawn. A pawn with no held weapon
// (animal, mech) takes the limb damage only. Costs 2 orbs until they return; cooldown about 40 s.
//
// Drawing: one pair of scissors with a shared pivot, handles and finger rings, lying flat at hand
// height with a ground shadow. A flat shape turns freely with the aim, so there is no per-facing
// drawing. The dropped weapon is thrown clear and stays on the ground so the result is visible.
// Caster, target and weapon are stand-ins.
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

function times(p) {
  const formed = p.deploy + p.form, snip = formed + p.aimTime, shut = snip + p.snip;
  const reform = shut + p.hold, recall = reform + p.reform, home = recall + p.return;
  return { formed, snip, shut, reform, recall, home, end: home + .4 };
}

export default {
  kit: 'Six Paths', label: 'Obsidian Shears (sketch)',
  params: {
    moving: { label: 'Target walks (shears track it)', value: true, group: 'Showcase' },
    actors: { label: 'Show caster, target and weapon', value: true, group: 'Showcase' },
    aim: P('Aim direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    distance: P('Caster distance (cells)', 5, 3, 12, .5, 'Showcase'),
    deploy: P('Orbs reach target', .45, .2, 1, .05, 'Timing (s)'),
    form: P('Form shears', .35, .15, .8, .05, 'Timing (s)'),
    aimTime: P('Line up on weapon hand', .35, .15, .8, .05, 'Timing (s)'),
    snip: P('Snip', .10, .05, .25, .01, 'Timing (s)'),
    hold: P('Hold shut', .2, .05, .5, .05, 'Timing (s)'),
    reform: P('Curl back into orbs', .35, .2, .8, .05, 'Timing (s)'),
    return: P('Orbs return to caster', .5, .2, 1, .05, 'Timing (s)'),
    length: P('Blade length (cells)', 1.25, .9, 1.8, .05, 'Shape'),
    width: P('Blade width (cells)', .2, .12, .3, .01, 'Shape'),
    open: P('Open half-angle (degrees)', 34, 20, 50, 1, 'Shape'),
    hover: P('Hover height (cells)', .45, .2, .9, .05, 'Shape'),
    flash: P('Cut flash strength', .8, 0, 1, .05, 'Feedback'),
    shake: P('Cut camera shake', .07, 0, .2, .01, 'Feedback'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Deploy', t: 0 }, { name: 'Form shears', t: p.deploy }, { name: 'Line up', t: t.formed },
    { name: 'Snip / disarm', t: t.snip }, { name: 'Hold', t: t.shut }, { name: 'Reform', t: t.reform },
    { name: 'Return', t: t.recall }, { name: 'Ready', t: t.home },
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
    // The target walks across the aim until the cut lands; everything else is relative to it.
    const walked = p.moving ? Math.min(s, t.shut) * .8 - 1 : 0;
    const flinch = Math.exp(-Math.max(0, s - t.shut) * 9) * (s >= t.shut ? .1 : 0);
    const target = place(o, flinch, walked);

    const form = smooth((s - p.deploy) / p.form) * (1 - smooth((s - t.reform) / p.reform));
    const lining = smooth((s - t.formed) / p.aimTime);
    const cut = clamp((s - t.snip) / p.snip) ** 3;
    const open = p.open * Mathf.Deg2Rad;
    const theta = lerp(lerp(open * .7, open, lining), .025, cut) * lerp(.35, 1, form) +
      smooth((s - t.shut - p.hold * .5) / p.hold) * .2;
    const lunge = Math.sin(clamp((s - t.snip) / (p.snip + .12)) * Math.PI) * .12;
    const pivotA = lerp(-p.length * .95, -p.length * .8, lining) + lunge;
    const out = smooth(s / p.deploy), back = smooth((s - t.recall) / p.return);
    const pivot = place(target, pivotA, 0);
    const carrier = { x: lerp(lerp(caster.x, pivot.x, out), caster.x, back), z: lerp(lerp(caster.z, pivot.z, out), caster.z, back) };
    const local = (a, b) => ({ a: pivotA + a, b });

    if (p.actors) {
      for (const [pos, colour] of [[caster, new Color(.39, .58, .65)], [target, new Color(.55, .38, .27)]]) {
        const bob = pos === target && p.moving && s < t.shut ? Math.abs(Math.sin(s * 9)) * .05 : 0;
        sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, .85, .4, Body.withAlpha(strength), soft, shadowLayer);
        draw(disc, pos.x, pawnLayer, pos.z + .18 + bob, .22, .32, 0, colour);
        draw(disc, pos.x, pawnLayer + .002, pos.z + .58 + bob, .16, .17, 0, new Color(.83, .70, .54));
      }
      // Weapon: held along the aim, pointing at the caster, then thrown clear and left lying.
      const fly = clamp((s - t.shut + p.snip * .3) / .45), landed = fly >= 1;
      const wa = lerp(-.3, -.55, fly), wb = lerp(0, 1.3, smooth(fly)) + (fly > 0 ? 0 : 0);
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

    // Orbs while travelling and while the blades are still growing out of them.
    for (const side of [-1, 1]) {
      const size = .24 * (1 - form);
      if (size <= .004) continue;
      const swirl = Math.sin((out - back) * Math.PI) * .35 * side;
      const q = { x: carrier.x - sa * (swirl + side * .13), z: carrier.z + ca * (swirl + side * .13) + p.hover * Lift };
      orb(q, size);
      const moving = s < p.deploy || s >= t.recall;
      if (moving) sprite({ x: q.x + sun.x * p.hover, z: q.z - p.hover * Lift + sun.z * p.hover }, .4, .2,
        Body.withAlpha(strength * .7), soft, shadowLayer);
    }

    if (form > .001) {
      const L = p.length * form, n = 20;
      for (const side of [-1, 1]) {
        const dA = Math.cos(theta), dB = side * Math.sin(theta), nA = -Math.sin(theta), nB = side * Math.cos(theta);
        const widthAt = (u) => p.width * form * (1 - u) ** .75 * (.75 + .25 * Math.sin(u * Math.PI));
        const strip = (project, inner, outer) => {
          const A = [], B = [];
          for (let j = 0; j <= n; j++) {
            const u = j / n, w = widthAt(u), i = inner(w), e = outer(w), q = local(dA * u * L, dB * u * L);
            A.push(project(target, q.a + nA * i, q.b + nB * i, p.hover));
            B.push(project(target, q.a + nA * e, q.b + nB * e, p.hover));
          }
          return [A, B];
        };
        const layer = Y + .10 + (side > 0 ? .01 : 0), key = 'shears blade ' + side;
        band(key + ' shadow', ...strip(cast, () => 0, w => w), Body.withAlpha(strength * .65), shadowLayer + .001);
        band(key + ' edge', ...strip(place, () => -.024, w => w + .024), Rim.withAlpha(.9), layer);
        band(key + ' body', ...strip(place, () => 0, w => w), Body, layer + .001);
        band(key + ' facet', ...strip(place, w => w * .42, w => w * .72), Color.Lerp(Body, Slate, .55), layer + .002);
        const [cutting] = strip(place, () => .012, () => 0);
        trail(key + ' cutting edge', cutting, .03, pale.withAlpha(.35 + .45 * lining * (1 - cut)), layer + .003);
        // Handle runs straight through the pivot to the other side, as on real scissors.
        const H = .4 * form, h0 = local(0, 0), h1 = local(-dA * H, -dB * H);
        const hw = .035 * form;
        band(key + ' handle edge',
          [place(target, h0.a + nA * (hw + .02), h0.b + nB * (hw + .02), p.hover), place(target, h1.a + nA * (hw + .02), h1.b + nB * (hw + .02), p.hover)],
          [place(target, h0.a - nA * (hw + .02), h0.b - nB * (hw + .02), p.hover), place(target, h1.a - nA * (hw + .02), h1.b - nB * (hw + .02), p.hover)],
          Rim.withAlpha(.9), layer);
        band(key + ' handle',
          [place(target, h0.a + nA * hw, h0.b + nB * hw, p.hover), place(target, h1.a + nA * hw, h1.b + nB * hw, p.hover)],
          [place(target, h0.a - nA * hw, h0.b - nB * hw, p.hover), place(target, h1.a - nA * hw, h1.b - nB * hw, p.hover)],
          Body, layer + .001);
        const r = local(-dA * (H + .13 * form) - nA * .05, -dB * (H + .13 * form) - nB * .05);
        const ringAt = place(target, r.a, r.b, p.hover), ringShadow = cast(target, r.a, r.b, p.hover);
        draw(loop, ringShadow.x, shadowLayer + .001, ringShadow.z, .17 * form, .17 * form, 0, Body.withAlpha(strength * .65));
        draw(loop, ringAt.x, layer, ringAt.z, .19 * form, .19 * form, 0, Rim.withAlpha(.9));
        draw(loop, ringAt.x, layer + .001, ringAt.z, .165 * form, .165 * form, 0, Body);
        // A glint runs down each cutting edge while the shears line up.
        if (lining > 0 && cut <= 0) {
          const u = 1 - lining, g = local(dA * u * L, dB * u * L);
          sprite(place(target, g.a, g.b, p.hover), .34, .34, pale.withAlpha(Math.sin(lining * Math.PI) * .8), glow, Y + .13);
        }
      }
      const rivet = place(target, pivotA, 0, p.hover);
      draw(disc, rivet.x, Y + .12, rivet.z, .075 * form, .075 * form, 0, Rim);
      draw(disc, rivet.x, Y + .121, rivet.z, .04 * form, .04 * form, 0, pale.withAlpha(.9));
    }

    // The cut: a pale line across the weapon hand, sparks, and a small floor ring.
    const age = s - t.shut;
    if (age >= 0 && age < .25) {
      const f = (1 - age / .25) ** 2, hand = place(target, -.12, 0, p.hover);
      sprite(hand, 1.5 * (.6 + age * 2), .07 * f, pale.withAlpha(f * p.flash), glow, Y + .14, -p.aim + 90);
      sprite(hand, .7, .7, pale.withAlpha(f * p.flash * .8), glow, Y + .14);
      circle(place(target, -.12, 0), .2 + age * 2.2, f * .5);
      for (let i = 0; i < 7; i++) {
        const dir = (rand(i + 3) - .5) * 2.4 + (i % 2 ? Math.PI / 2 : -Math.PI / 2), reach = age * (3 + rand(i) * 3);
        const from = place(target, -.12 + Math.cos(dir) * reach * .6, Math.sin(dir) * reach * .6, p.hover);
        const to = place(target, -.12 + Math.cos(dir) * reach, Math.sin(dir) * reach, p.hover);
        trail('shears spark ' + i, [from, { x: (from.x + to.x) / 2, z: (from.z + to.z) / 2 }, to], .05 * f, pale.withAlpha(f * p.flash), Y + .15);
      }
    }
  },
};
