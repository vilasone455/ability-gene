// Unlimited Blade Works: chant, open and close — Trace kit proposal, not the game. Nothing in
// Source/RimArt draws this yet. The kit's ultimate. The commands used while the world stands are
// the second sketch, "Unlimited Blade Works: commands".
//
// What it is for (proposed, none of it agreed; every number is a placeholder and will be an XML
// field).
//   Self. The pawn stands and chants up to 3 verses of 2 s; with each verse the world it will open
//   grows. Released after verse 1, 2 or 3 it opens at radius 6, 9 or 12 cells with 20, 40 or 60
//   swords ("Swords per verse" starts at 30 in the sketch: 20 looked sparse for a hill of swords). Downed or stunned while chanting: it fails and a quarter of the cooldown is spent.
//   On release, fire runs along the ground from the pawn to the edge in 1 s. Inside, the floor
//   turns to a dusk wasteland and the swords stand in it: copies of the weapons the pawn has
//   studied, mixed. The world stays where it opened for 30 s, or until its swords run out, the
//   caster is downed, or the player closes it. While it stands, nobody walks across the edge in
//   either direction ("lock"); shots cross. The alternative ("burn"): pawns can cross and the fire
//   burns them, about 8 Burn. When it closes, the fire runs back in and every sword still standing
//   breaks into light; nothing is left behind. Cooldown 2 days.
//
// Order, with the default timings (released after verse 1):
//   0.00  chant: teal lines run out along the floor from the pawn; as they pass, wire outlines of
//         swords climb out of the ground. A ring marks how far the world would reach. Each verse
//         starts with a ring off the pawn.
//   2.00  release: a flash at the pawn; fire runs out along the ground to the edge in 1 s. Behind
//         it the floor turns dusk and each wire sword fills with steel from the ground up.
//   3.00  the world stands: a lower fire burns round the edge, embers rise, the shadows of huge
//         gears turn on the floor. A raider walks into the edge: "lock" stops him at the fire,
//         "burn" lets him through burning.
//   6.00  close: the fire runs back in over 1 s; every sword it passes breaks into light.
//
// Drawing: the swords are the Trace planted blades (lib/trace.js), real weapon textures standing in
// the ground. Everything else is flat on the floor or a level circle (fire ring, dusk floor, gear
// shadows), so there is no per-facing method. The sky's gears are shown only as their shadows.
// Inside the world the swords' shadows run twice as long, for a low dusk sun.
// Caster and raider are stand-ins. The flame is a lab texture (lab/ubw-flame) still to be made a
// PNG.
import { Color } from '../js/engine.js';
import { P, Y, Floor, sprite, glow, rand } from './lib/six-paths-impact.js';
import { pawn, ringAt, line, buildingLayer, EnemyColour } from './lib/goku.js';
import { smooth, clamp, D2R, Trace, TraceHot, cutOf, plant, blade, pommelOf, onScreen } from './lib/trace.js';
import {
  flame, light, FireOuter, FireCore, Radius, SceneNorth, DuskShadow, swords, swordPose, gearShadows, fireRing, wasteland,
} from './lib/ubw.js';

const Caster = new Color(.55, .3, .24);
// The chant's fixed numbers.
const VerseTime = 2, LinesOut = 1.8;

function times(p) {
  const V = Math.round(p.verse), open = V * VerseTime, stands = open + p.run, close = stands + p.hold, shut = close + p.close;
  return { V, R: Radius[V - 1], open, stands, close, shut, end: shut + 1.2 };
}
// The fire's radius: out fast and easing into the edge, then back in speeding up.
function fireRadius(s, t) {
  if (s < t.open || s >= t.shut) return 0;
  if (s < t.stands) return t.R * (1 - (1 - (s - t.open) / (t.stands - t.open)) ** 2);
  if (s < t.close) return t.R;
  return t.R * (1 - ((s - t.close) / (t.shut - t.close)) ** 2);
}
const fireReaches = (d, t) => t.open + (t.stands - t.open) * (1 - Math.sqrt(Math.max(0, 1 - d / t.R)));
const fireLeaves = (d, t) => t.close + (t.shut - t.close) * Math.sqrt(Math.max(0, 1 - d / t.R));
// How far the chant's lines have run: verse k carries them from the last verse's radius to its own.
function chantRadius(s, t) {
  let r = 0;
  for (let k = 1; k <= t.V; k++) {
    const from = k > 1 ? Radius[k - 2] : 0, start = (k - 1) * VerseTime;
    if (s >= start) r = from + (Radius[k - 1] - from) * Math.min(1, (s - start) / LinesOut);
  }
  return r;
}
function chantReaches(sw) {
  const from = sw.k > 1 ? Radius[sw.k - 2] : 0;
  return (sw.k - 1) * VerseTime + LinesOut * clamp((sw.d - from) / (Radius[sw.k - 1] - from));
}

// The raider who walks into the edge while the world stands.
function raider(c, s, t, p, sun, strength) {
  const dir = { x: Math.cos(-35 * D2R), z: Math.sin(-35 * D2R) }, speed = .9, startD = t.R - 1.8;
  let d = startD, flash = 0, burning = 0;
  if (s > t.stands) {
    const walked = (s - t.stands) * speed;
    if (p.edge === 'lock') {
      const stop = t.R - .45, hit = t.stands + (stop - startD) / speed;
      if (s < hit) d = startD + walked;
      else { const u = s - hit; d = stop - .3 * Math.sin(Math.min(1, u / .25) * Math.PI / 2); flash = u < .3 ? 1 - u / .3 : 0; }
    } else {
      d = Math.min(t.R + 1.3, startD + walked);
      const into = t.stands + (t.R - .4 - startD) / speed, out = t.stands + (t.R + .4 - startD) / speed;
      if (s >= into) burning = s < out + .8 ? 1 : Math.max(0, 1 - (s - out - .8) / .5);
      if (s >= into && s < into + .3) flash = 1 - (s - into) / .3;
    }
  }
  const pos = { x: c.x + dir.x * d, z: c.z + dir.z * d };
  pawn(pos, EnemyColour, sun, strength, { tint: FireOuter, tintAmount: .6 * Math.max(flash, burning * .6) });
  if (flash > 0) sprite({ x: pos.x, z: pos.z + .35 }, 1.1, 1.1, FireCore.withAlpha(.6 * flash), glow, Y + .03);
  for (let i = 0; i < 3 && burning > 0; i++) {
    const h = .45 + .15 * Math.sin(s * 13 + i * 2);
    sprite({ x: pos.x + (i - 1) * .14, z: pos.z + .2 + h * .5 }, .22, h, FireOuter.withAlpha(.6 * burning), flame, Y + .031);
  }
}

export default {
  kit: 'Trace', label: 'Unlimited Blade Works: chant and open (sketch)',
  params: {
    verse: P('Released after verse', 1, 1, 3, 1, 'World'),
    perVerse: P('Swords per verse', 30, 10, 60, 1, 'World'),
    edge: { label: 'Edge', value: 'lock', options: ['lock', 'burn'], group: 'World' },
    actors: { label: 'Stand-ins (caster, raider)', value: true, group: 'World' },
    run: P('Fire runs out', 1, .4, 2.5, .05, 'Timing (s)'),
    hold: P('World stands (30 s in game)', 3, 1, 8, .5, 'Timing (s)'),
    close: P('Fire runs back in', 1, .4, 2.5, .05, 'Timing (s)'),
    size: P('Sword size (x image)', 1.3, .8, 2.2, .05, 'Look'),
    lean: P('Sword lean, most (degrees)', 18, 0, 40, 1, 'Look'),
    flame: P('Flame height (cells)', .7, .2, 1.5, .05, 'Look'),
    dusk: P('Dusk floor strength', .85, 0, 1, .05, 'Look'),
    gears: P('Gear shadow opacity', .24, 0, .5, .01, 'Look'),
  },
  duration(p) { return times(p).end; },
  phases(p) {
    const t = times(p), verses = [];
    for (let k = 1; k <= t.V; k++) verses.push({ name: `Verse ${k}`, t: (k - 1) * VerseTime });
    return [...verses, { name: 'Open', t: t.open }, { name: 'Stands', t: t.stands }, { name: 'Close', t: t.close }, { name: 'Closed', t: t.shut }];
  },
  events(p) { const t = times(p); return [{ t: t.open, type: 'shake', value: .05 }, { t: t.shut, type: 'shake', value: .02 }]; },

  draw(s, p, { origin: cell, scene }) {
    const t = times(p);
    if (s < 0 || s >= t.end) return;
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
    const c = { x: cell.x, z: cell.z + SceneNorth };
    const fire = fireRadius(s, t), world = s >= t.open && s < t.shut ? fire : 0;

    // The chant: lines out along the floor, a ring at their front, a ring off the pawn each verse.
    if (s < t.open + .3) {
      const reach = chantRadius(s, t), fade = s < t.open ? 1 : 1 - (s - t.open) / .3;
      for (let i = 0; i < 10; i++) {
        const a0 = i / 10 * Math.PI * 2 + rand(i * 3 + 7) * .3, pts = [];
        for (let r = .35; ; r += .5) {
          const rr = Math.min(r, reach), wob = Math.sin(rr * 1.3 + i) * .08;
          pts.push({ x: c.x + Math.cos(a0 + wob) * rr, z: c.z + Math.sin(a0 + wob) * rr });
          if (r >= reach) break;
        }
        if (pts.length > 1) {
          line(`ubw circuit ${i}`, pts, .045, Trace.withAlpha(.75 * fade), light, Floor + .007, 'end');
          line(`ubw circuit halo ${i}`, pts, .16, Trace.withAlpha(.16 * fade), light, Floor + .0065, 'end');
        }
      }
      if (reach > .4) ringAt(c, reach, Trace.withAlpha(.45 * fade), Floor + .0068);
      for (let k = 1; k < t.V; k++) if (s >= k * VerseTime - .2) ringAt(c, Radius[k - 1], Trace.withAlpha(.22 * fade), Floor + .0067);
      for (let k = 1; k <= t.V; k++) {
        const u = (s - (k - 1) * VerseTime) / .45;
        if (u >= 0 && u < 1) ringAt(c, .3 + 1.2 * u, Trace.withAlpha(.7 * (1 - u)), Y + .01, false, light);
      }
      if (s < t.open) sprite({ x: c.x, z: c.z + .3 }, 1.6, 1.6, Trace.withAlpha((.22 + .1 * Math.sin(s * 9)) * fade), glow, Y + .005);
    }

    // The world: dusk floor, gear shadows, warm light, fire at its edge.
    if (world > 0) {
      const gone = s < t.close ? 1 : 1 - smooth((s - t.close) / (t.shut - t.close));
      wasteland('ubw', c, world, t.R, 1, p.dusk);
      gearShadows(c, t.R, s, p.gears * smooth((s - t.open - p.run * .6) / .8) * gone);
      const flameHeight = s < t.stands ? p.flame : s < t.close ? p.flame * .6 : p.flame * .9;
      fireRing('ubw', c, world, s, flameHeight, 1);
    }
    if (s >= t.open && s < t.open + .35) {
      const u = (s - t.open) / .35;
      sprite({ x: c.x, z: c.z + .3 }, 3.5 * (1 + u), 2.8 * (1 + u), FireCore.withAlpha((1 - u) ** 2), glow, Y + .05);
    }
    if (s >= t.shut && s < t.shut + .3) {
      const u = (s - t.shut) / .3;
      sprite({ x: c.x, z: c.z + .3 }, 1.6, 1.4, FireCore.withAlpha(.8 * (1 - u)), glow, Y + .05);
    }

    // The swords, north first.
    const all = swords(Math.round(p.perVerse)).filter(sw => sw.k <= t.V);
    const order = all.slice().sort((a, b) => b.z - a.z);
    order.forEach((sw, rank) => {
      const traced = chantReaches(sw);
      if (s < traced) return;
      const g = { x: c.x + sw.x, z: c.z + sw.z }, key = `ubw sword ${sw.seed}`, layer = buildingLayer + rank * .0045;
      const b = swordPose(sw, c, p.size, p.lean), top = pommelOf(b).y + .05;
      const hard = fireReaches(sw.d, t);
      if (s < hard) {
        blade(key, b, sun, strength, layer, { fillTo: -1, wireTo: top * smooth((s - traced) / .35), wireAlpha: .85 });
        sprite(g, .5, .25, Trace.withAlpha(.22 * smooth((s - traced) / .2)), glow, Floor + .005);
        return;
      }
      const age = s - hard, leaves = fireLeaves(sw.d, t);
      // inside the world the light is a low dusk sun: shadows run twice as long
      const inside = s < t.shut && Math.hypot(sw.x, sw.z) < fire, sunHere = inside ? { x: sun.x * DuskShadow, z: sun.z * DuskShadow } : sun;
      const left = s - leaves, fade = left > 0 ? 1 - smooth(left / .3) : 1;
      if (left > 0) for (let i = 0; i < 3 && left < .45; i++) {
        const q = onScreen(pommelOf(b)), u = left / .45;
        sprite({ x: q.x + (i - 1) * .12, z: q.z - .2 * i + u * .5 }, .09, .09, TraceHot.withAlpha(Math.sin(u * Math.PI) * .9), glow, Y + .02);
      }
      if (fade <= 0) return;
      plant(key, cutOf(b, sw.sink), layer, sw.seed, sun, { grow: .3 + .7 * smooth(age / .2), alpha: fade, cracks: 4, crumbs: 1 });
      const flash = left > 0 ? smooth(left / .05) * (1 - smooth(left / .3)) : 0;
      blade(key, b, sunHere, strength, layer, {
        alpha: fade, fillTo: age < .3 ? top * smooth(age / .3) : Infinity,
        wireTo: top, wireAlpha: Math.max(.85 * (1 - smooth((age - .2) / .25)), .9 * flash), scan: age < .3 ? top * smooth(age / .3) : -1,
      });
    });

    if (p.actors) {
      pawn(c, Caster, sun, strength, { hair: true });
      raider(c, s, t, p, sun, strength);
    }
  },
};
