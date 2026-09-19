// Power Pole: Sweep — weapon ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (proposed, none of it agreed). Targets a direction. The pole extends to 4 cells
// during a 0.3 s wind-up, swings through the front 180 degrees from the caster's left to its right
// in 0.4 s, and retracts in 0.25 s. Every pawn in that half-circle the caster has line of sight to
// takes 12 blunt and is staggered. A wall shortens the pole while it passes, so a pawn behind the
// wall is not hit. Cooldown 20 s. It is the kit's answer to being surrounded; Extend Thrust is one
// target far away, this is many targets close.
//
// Drawing: the pole lies flat at hand height, so it turns freely and needs no per-facing method.
// The floor outline is the true hit area, with the notch a wall cuts into it. The pole's length at
// each angle is the distance to the first wall on that line, capped at the reach; in C# that is one
// table per cast. Only the shaft stretches; the ferrules keep a fixed length. The caster, enemies,
// hands and wall are stand-ins. Dust and flashes use the Six Paths SoftDisc and Puff textures.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, ShaderDatabase } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, Body, Y, Floor, Lift, sprite, band, glow, soft, rand } from './lib/six-paths-impact.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, Rad = Mathf.Deg2Rad;
const disc = Meshes.disc(32, 'power pole sweep disc');
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
const Red = new Color(.80, .13, .10), RedLit = new Color(.98, .46, .36), RedDark = new Color(.25, .04, .04);
const Ferrule = new Color(.46, .07, .06), Skin = new Color(.83, .70, .54), Pale = new Color(1, .95, .8);
const Dust = new Color(.80, .74, .63), Stone = new Color(.34, .33, .35), StoneTop = new Color(.50, .49, .52);
// Decided looks and the rule's fixed numbers.
const PoleHeight = .5, RestBack = -.45, RestTip = .75, FerruleLength = .14;
const Hold = .1, Tail = 1, StaggerShown = 1.2, Shove = .25;
// Stand-ins as [cells from the caster, degrees from the aim, + is the caster's left].
const Enemies = [[2, 60], [3.5, 10], [3.4, -45], [5, -20], [2.5, 170]];   // the last two are out of reach and behind
const WallAt = [2.2, -45];      // in front of the third enemy

function times(p) {
  const swing = p.windup, swung = swing + p.sweep, retract = swung + Hold, home = retract + p.retract;
  return { swing, swung, retract, home, end: home + Tail };
}

// Everything that depends only on the params: the pole's length per angle, its angle per time, and
// when each enemy is hit. draw() and events() both read this.
function rule(p) {
  const t = times(p), half = p.arc / 2;
  const wall = p.scenario === 'wall'
    ? { x: WallAt[0] * Math.cos((p.aim + WallAt[1]) * Rad), z: WallAt[0] * Math.sin((p.aim + WallAt[1]) * Rad) } : null;
  const lengthAt = (phi) => {
    if (!wall) return p.reach;
    const dx = Math.cos((p.aim + phi) * Rad), dz = Math.sin((p.aim + phi) * Rad);
    let enter = -Infinity, exit = Infinity;
    for (const [d, c] of [[dx, wall.x], [dz, wall.z]]) {
      if (Math.abs(d) < 1e-6) { if (Math.abs(c) > .5) return p.reach; continue; }
      const one = (c - .5) / d, two = (c + .5) / d;
      enter = Math.max(enter, Math.min(one, two)); exit = Math.min(exit, Math.max(one, two));
    }
    return enter <= exit && enter > 0 ? Math.min(p.reach, enter - .08) : p.reach;
  };
  const angleAt = (time) => {
    if (time < t.swing) return half * smooth(time / p.windup);
    if (time < t.swung) return half - p.arc * smooth((time - t.swing) / p.sweep);
    if (time < t.retract) return -half;
    return -half * (1 - smooth((time - t.retract) / p.retract));
  };
  const passes = (phi) => {
    if (Math.abs(phi) > half) return null;
    for (let time = t.swing; time <= t.swung; time += .004) if (angleAt(time) <= phi) return time;
    return t.swung;
  };
  const enemies = Enemies.map(([r, phi]) => ({ r, phi, hit: r <= lengthAt(phi) + .25 ? passes(phi) : null }));
  return { t, half, wall, lengthAt, angleAt, passes, enemies };
}

export default {
  kit: 'Power Pole', label: 'Sweep (sketch)',
  params: {
    actors: { label: 'Show caster and enemies', value: true, group: 'Showcase' },
    aim: P('Cast direction (degrees)', 0, 0, 360, 5, 'Showcase'),
    scenario: { label: 'In the arc', value: 'open ground', options: ['open ground', 'wall'], group: 'Showcase' },
    windup: P('Wind-up and extend', .3, .1, .8, .05, 'Timing (s)'),
    sweep: P('Swing', .4, .15, 1, .05, 'Timing (s)'),
    retract: P('Retract', .25, .1, .8, .05, 'Timing (s)'),
    reach: P('Reach (cells)', 4, 2, 6, .5, 'Rule'),
    arc: P('Arc (degrees)', 180, 90, 300, 10, 'Rule'),
    width: P('Pole width (cells)', .12, .06, .25, .01, 'Shape'),
  },
  duration(p) { return times(p).end; },
  phases(p) { const t = times(p); return [
    { name: 'Wind-up', t: 0 }, { name: 'Swing', t: t.swing }, { name: 'Retract', t: t.retract }, { name: 'Result', t: t.home },
  ]; },
  events(p) { return rule(p).enemies.filter(e => e.hit !== null).map(e => ({ t: e.hit, type: 'shake', value: .06 })); },

  draw(s, p, { origin: o, scene }) {
    const { t, half, wall, lengthAt, angleAt, passes, enemies } = rule(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    // A point r cells from the caster at phi degrees from the aim, h cells up; shade() is its shadow.
    const polar = (r, phi, h = 0) => ({ x: o.x + r * Math.cos((p.aim + phi) * Rad), z: o.z + r * Math.sin((p.aim + phi) * Rad) + h * Lift });
    const shade = (r, phi, h = 0) => ({ x: o.x + r * Math.cos((p.aim + phi) * Rad) + sun.x * h, z: o.z + r * Math.sin((p.aim + phi) * Rad) + sun.z * h });
    const swinging = s >= t.swing && s < t.swung;

    // The pole's angle and the far end's distance right now.
    const phi = angleAt(s);
    const tipLength = s < t.swing ? lerp(RestTip, lengthAt(half), smooth(s / p.windup))
      : s < t.retract ? lengthAt(phi) : lerp(lengthAt(-half), RestTip, smooth((s - t.retract) / p.retract));

    // Floor outline of the true hit area, with the wall's notch. Fades once the swing is over.
    const shown = 1 - smooth((s - t.swung) / .4), steps = 72;
    const edge = (grow) => Array.from({ length: steps + 1 }, (_, i) => { const at = lerp(half, -half, i / steps); return polar(Math.max(0, lengthAt(at) + grow), at); });
    const hub = Array.from({ length: steps + 1 }, (_, i) => polar(.35, lerp(half, -half, i / steps)));
    band('power pole sweep area', hub, edge(0), Pale.withAlpha(.07 * shown), Floor);
    band('power pole sweep outline', edge(-.05), edge(0), Pale.withAlpha(.55 * shown), Floor + .002);

    const figure = (pos, colour, layer = pawnLayer) => {
      sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, .85, .4, Body.withAlpha(strength), soft, shadowLayer);
      draw(disc, pos.x, layer, pos.z + .18, .22, .32, 0, colour);
      draw(disc, pos.x, layer + .002, pos.z + .58, .16, .17, 0, Skin);
    };

    // Wall stand-in, one cell, one cell tall, behind or in front of the enemy it covers.
    if (wall) {
      const w = { x: o.x + wall.x, z: o.z + wall.z }, hidden = polar(Enemies[2][0], Enemies[2][1]), layer = pawnLayer + (w.z > hidden.z ? -.01 : .01);
      sprite({ x: w.x + sun.x * .5, z: w.z + sun.z * .5 }, 1.5, 1.2, Body.withAlpha(strength), soft, shadowLayer);
      band('power pole sweep wall front', [{ x: w.x - .5, z: w.z - .5 }, { x: w.x + .5, z: w.z - .5 }],
        [{ x: w.x - .5, z: w.z - .5 + Lift }, { x: w.x + .5, z: w.z - .5 + Lift }], Stone, layer);
      band('power pole sweep wall top', [{ x: w.x - .5, z: w.z - .5 + Lift }, { x: w.x + .5, z: w.z - .5 + Lift }],
        [{ x: w.x - .5, z: w.z + .5 + Lift }, { x: w.x + .5, z: w.z + .5 + Lift }], StoneTop, layer + .002);
    }

    // Enemies. One that is hit is shoved Shove cells the way the pole was moving and stays there.
    if (p.actors) {
      figure(o, new Color(.93, .50, .13));
      enemies.forEach((e, i) => {
        const age = e.hit === null ? -1 : s - e.hit, moved = age < 0 ? 0 : Shove * (1 - Math.pow(1 - clamp(age / .2), 3));
        const base = polar(e.r, e.phi), along = (p.aim + e.phi - 90) * Rad;
        const pos = { x: base.x + Math.cos(along) * moved, z: base.z + Math.sin(along) * moved };
        figure(pos, new Color(.55, .38, .27));
        if (age < 0) return;
        sprite({ x: pos.x, z: pos.z + PoleHeight * Lift }, 1.3, .9, Pale.withAlpha(Math.max(0, 1 - age / .12) * .85), glow, Y + .02);
        const fade = 1 - smooth((age - StaggerShown) / .3);
        for (let k = 0; k < 3; k++) {
          const turn = s * 5 + k * 2.094 + i;
          sprite({ x: pos.x + Math.cos(turn) * .2, z: pos.z + .86 + Math.sin(turn) * .06 }, .08, .08, Pale.withAlpha(.9 * fade), soft, Y + .03);
        }
      });
    }

    // The swept fan behind the pole: three nested slices, so the newest part is the brightest.
    if (swinging || (s >= t.swung && s < t.swung + .12)) for (const [k, span] of [.12, .07, .035].entries()) {
      const from = angleAt(Math.min(s, t.swung)), to = angleAt(Math.max(t.swing, s - span)), fade = s < t.swung ? 1 : 1 - (s - t.swung) / .12;
      const inner = [], outer = [];
      for (let i = 0; i <= 10; i++) { const at = lerp(to, from, i / 10); inner.push(polar(.7, at, PoleHeight)); outer.push(polar(lengthAt(at), at, PoleHeight)); }
      band(`power pole sweep fan ${k}`, inner, outer, Pale.withAlpha(.13 * fade), Y - .01);
    }

    // Dust along the ground under the far end as it passes.
    for (let i = 0; i < 16; i++) {
      const at = lerp(half, -half, (i + .5) / 16), born = passes(at), life = .4 + rand(i) * .25, u = (s - born) / life;
      if (u < 0 || u > 1 || s < t.swing) continue;
      const spot = polar(lengthAt(at) - .15 + u * .5, at - u * 6, u * .25);
      sprite(spot, .35 + u * .6, .28 + u * .45, Dust.withAlpha(Math.sin(u * Math.PI) * .65), puff, Y - .02);
    }

    // The pole: a strip from just behind the hands to the far end, lit on its north side.
    const a0 = polar(RestBack, phi, PoleHeight), b0 = polar(tipLength, phi, PoleHeight);
    const sx = b0.x - a0.x, sz = b0.z - a0.z, screenLength = Math.hypot(sx, sz) || 1;
    let nx = -sz / screenLength, nz = sx / screenLength;
    if (nz < 0 || (nz === 0 && nx > 0)) { nx = -nx; nz = -nz; }
    const point = (k, side) => ({ x: a0.x + sx * k + nx * side, z: a0.z + sz * k + nz * side });
    const strip = (key, from, to, lo, hi, colour, layer) => band(key, [point(from, lo), point(to, lo)], [point(from, hi), point(to, hi)], colour, layer);
    const w = p.width, cap = Math.min(.45, FerruleLength / screenLength), grow = .02 / screenLength;
    {
      const sa0 = shade(RestBack, phi, PoleHeight), sb0 = shade(tipLength, phi, PoleHeight);
      band('power pole sweep shadow', [{ x: sa0.x - nx * w / 2, z: sa0.z - nz * w / 2 }, { x: sb0.x - nx * w / 2, z: sb0.z - nz * w / 2 }],
        [{ x: sa0.x + nx * w / 2, z: sa0.z + nz * w / 2 }, { x: sb0.x + nx * w / 2, z: sb0.z + nz * w / 2 }], Body.withAlpha(strength), shadowLayer);
    }
    strip('power pole sweep pole outline', -grow, 1 + grow, -w / 2 - .02, w / 2 + .02, RedDark, Y);
    strip('power pole sweep pole body', 0, 1, -w / 2, w / 2, Red, Y + .002);
    strip('power pole sweep pole lit', 0, 1, w * .12, w * .40, RedLit, Y + .004);
    strip('power pole sweep ferrule a', 0, cap, -w / 2, w / 2, Ferrule, Y + .006);
    strip('power pole sweep ferrule b', 1 - cap, 1, -w / 2, w / 2, Ferrule, Y + .006);
    if (p.actors) for (const grip of [.05, .42]) {
      const hand = polar(grip, phi, PoleHeight);
      draw(disc, hand.x, Y + .008, hand.z, .075, .075, 0, Skin);
    }
  },
};
