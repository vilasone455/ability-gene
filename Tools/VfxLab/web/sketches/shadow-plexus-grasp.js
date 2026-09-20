// Shadow grasp — Shadow plexus ability proposal, not the game. Nothing in Source/RimArt draws this yet.
//
// What it is for (the user's draft, numbers are placeholders, none of it balanced yet). Target a
// loose item, a weapon, a live grenade or a downed body, then a second cell; both within 24.9
// cells times the light level. 0.5 s cast, 15 s cooldown. The thing slides over the ground to the
// second cell; it is not teleported. A live explosive keeps ticking on the way. The tendril's path
// is a shadow line: it cannot cross dark cells, and if a pawn stands on the path the thing stops
// in that pawn's cell.
//
// Order (times with the default sliders, slide of 7 cells):
//   0.00  the tendril runs from the carrier to the item
//   0.50  a flat hand of shadow opens on the floor under the item; a thin feeler runs ahead to the
//         chosen cell, which shows the path a pawn can block
//   0.60  the fingers curl up over the item
//   0.72  over the first 0.9 cells of the slide the hand turns toward the chosen cell
//         the hand slides the item at 12 cells per second. The tendril bends at the pick-up cell
//         and follows the hand; the grenade's light blinks faster
//   1.30  the fingers open, the item stays, the tendril runs back along both legs
//   1.65  the grenade goes off (ordinary explosion stand-in, camera shake)
//   Rescue scenario: the thing is a downed ally under fire. The hand is 1.9 times the size, opens
//   under the torso and closes over it, then drags the body 7 cells sideways (110 degrees from the
//   aim) at 6 cells per second, half the item speed, to a medic behind sandbags. Dust along the
//   path, the head trails, no shake and no explosion; the body stays where the hand lets go.
//   Blocked scenario: a raider walks onto the path; the hand stops in his cell, 60 % of the way,
//   and the grenade goes off there instead of in the group.
//
// Drawing: the tendril, hand and feeler are flat on the floor. The hand (lib/shadow-plexus.js) is a wrist, an oval palm,
// four fingers of three joints and a thumb of two, each joint one quad with a disc at its end and
// an indigo edge so the fingers stay apart when they lie over the palm. Curling folds the joints;
// half of a folded joint's height is drawn as a shift north (Arch), less than the kit's 0.60 so a
// hand pointing east does not skew. No per-facing method. Grenade, raiders and explosion are lab
// stand-ins.
import { AltitudeLayer, Color, Mathf, Meshes } from '../js/engine.js';
import { draw } from './lib/six-paths-solid.js';
import { P, sprite, glow } from './lib/six-paths-impact.js';
import {
  lighting, pawn, downedPawn, hand, path, pointOn, shadowLine, pool, shreds, scuff, rangeRing, blast,
  CarrierColour, EnemyColour, AllyColour, Shade, Fringe, LineLayer, LightLayer,
} from './lib/shadow-plexus.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const disc = Meshes.disc(20, 'grasp item');
const itemLayer = AltitudeLayer.Item.AltitudeFor();
// The rule's numbers and decided looks.
const FullRange = 24.9, Open = .1, Curl = .12, LetGo = .12, Back = .4, Fuse = .35, BlastRadius = 1.9, Tail = 1.1;
const BlockedAt = .6, WalkerLead = .2, TurnOver = .9;
const Scenarios = ['slide a live grenade into a group', 'a pawn steps on the path', 'drag a downed ally behind cover'];
// The rescue: the body goes sideways out of the line of fire, at half the item speed, in a hand 1.9 times the size.
const RescueTurn = 110, BodySpeed = .5, BodyHand = 1.9, BodyLies = 210, ScuffEvery = .1;
const Group = [[.7, .5], [.9, -.6], [-.2, .9]];

function plan(p, o) {
  const a = p.aim * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a), mode = Scenarios.indexOf(p.scenario);
  const place = (along, across) => ({ x: o.x + along * ca - across * sa, z: o.z + along * sa + across * ca });
  const rescue = mode === 2, turnDeg = rescue ? RescueTurn : p.turn, turn = turnDeg * Mathf.Deg2Rad, share = mode === 1 ? BlockedAt : 1;
  const slideStart = p.cast + Open + Curl, arrive = slideStart + p.slide * share / (p.speed * (rescue ? BodySpeed : 1)), boom = rescue ? Infinity : arrive + Fuse;
  return { place, mode, rescue, turnDeg, share, slideStart, arrive, boom, end: rescue ? arrive + LetGo + Back + Tail : boom + Tail, carrier: place(-p.distance, 0), item: place(0, 0),
    dest: place(p.slide * Math.cos(turn), p.slide * Math.sin(turn)), across: place(-Math.sin(turn), Math.cos(turn)) };
}

export default {
  kit: 'Shadow plexus', label: 'Shadow grasp (sketch)',
  params: {
    scenario: { label: 'Scenario', value: Scenarios[0], options: Scenarios, group: 'Showcase' },
    aim: P('Direction to the item (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Showcase'),
    distance: P('Carrier to item (cells)', 5, 2, 9, .5, 'Showcase'),
    slide: P('Slide length (cells)', 7, 2, 10, .5, 'Showcase'),
    turn: P('Slide direction, from the aim (degrees; the rescue uses 110)', 35, -150, 150, 5, 'Showcase'),
    range: { label: 'Show the range ring', value: true, group: 'Showcase' },
    cast: P('Cast: the tendril runs out', .5, .2, 1.5, .05, 'Timing (s)'),
    speed: P('Slide speed (cells per s)', 12, 4, 24, 1, 'Timing (s)'),
    width: P('Tendril width (cells)', .14, .06, .4, .01, 'Line'),
    sway: P('Tendril sway (cells)', .1, 0, .4, .01, 'Line'),
  },
  duration(p) { return plan(p, { x: 0, z: 0 }).end; },
  phases(p) {
    const t = plan(p, { x: 0, z: 0 });
    return [{ name: 'Tendril runs out', t: 0 }, { name: 'Hand opens and closes', t: p.cast }, { name: 'Slide', t: t.slideStart },
      { name: t.mode === 1 ? 'Stopped by a pawn' : 'Let go', t: t.arrive }, ...(t.rescue ? [] : [{ name: 'Grenade goes off', t: t.boom }])];
  },
  events(p) { const t = plan(p, { x: 0, z: 0 }); return t.rescue ? [] : [{ t: t.boom, type: 'shake', value: .12 }]; },   // a rescue is not an attack: no shake

  draw(s, p, { origin: o, scene }) {
    const t = plan(p, o), { mode, rescue, carrier, item, dest } = t;
    if (s < 0 || s >= t.end) return;
    const L = lighting(false, scene), blown = s >= t.boom;
    const slidAt = (time) => t.share * smooth((time - t.slideStart) / (t.arrive - t.slideStart));
    const slid = slidAt(s), at = pointOn(item, dest, slid), stop = pointOn(item, dest, t.share);
    if (p.range) rangeRing(carrier, FullRange, L.level(carrier), 1 - smooth((s - t.arrive) / .4));

    // The tendril: carrier to the pick-up cell, then on to the hand. It runs back along both legs.
    const leg1 = p.distance, leg2 = p.slide * slid, back = smooth((s - t.arrive - LetGo) / Back);
    const left = (s < p.cast ? 1 - (1 - clamp(s / p.cast)) ** 2 : 1 - back) * (leg1 + (s < p.cast ? 0 : leg2));
    shadowLine('grasp leg 1', path(carrier, item, 0, clamp(left / leg1), s, p.sway), p.width, 1, s, { point: s < p.cast || back > 0 });   // blunt while it ends in the wrist
    if (left > leg1) {
      pool('grasp bend', item, p.width * .8, 1, s);
      shadowLine('grasp leg 2', path(item, at, 0, (left - leg1) / (leg2 || 1), s, .04, 12), p.width, 1, s, { flare: false, point: back > 0 });
    }
    pool('grasp root', carrier, .28 * clamp(left * 3), 1, s);

    // The feeler: the path the item will take. A pawn standing on it stops the item there.
    const feel = smooth((s - p.cast) / Open) * (1 - smooth((s - t.arrive) / .1));
    if (feel > 0) shadowLine('grasp feeler', path(at, mode === 1 ? stop : dest, 0, feel, s, 0, 8), .05, .6, s, { flare: false });

    // The hand, and the grenade in it.
    const open = smooth((s - p.cast) / Open) * (1 - smooth((s - t.arrive - LetGo) / .15));
    const curl = smooth((s - p.cast - Open) / Curl) * (1 - smooth((s - t.arrive) / LetGo));
    const turned = smooth(leg2 / TurnOver);
    hand('grasp hand', at, p.aim + t.turnDeg * turned, open, curl, s, rescue ? BodyHand : 1);   // the wrist stays on the tendril: it turns as the second leg grows
    shreds('grasp let go', at, s - t.arrive - LetGo, 6, .35, .5);
    if (rescue) {                                                    // the body: palm under the torso, fingers over it, head trailing
      downedPawn('grasp body', at, AllyColour, L, p.aim + lerp(BodyLies, t.turnDeg + 180, turned));
      for (let k = 0; t.slideStart + k * ScuffEvery < Math.min(s, t.arrive); k++) scuff(pointOn(item, dest, slidAt(t.slideStart + k * ScuffEvery)), s - (t.slideStart + k * ScuffEvery), 1.3);
    } else if (!blown) {
      const rate = s < t.slideStart ? 4 : 9 + 14 * clamp((s - t.slideStart) / (t.boom - t.slideStart)), lit = Math.sin(s * rate * Math.PI) > 0;
      draw(disc, at.x, itemLayer, at.z + .05, .1, .12, 0, new Color(.2, .27, .17));
      if (lit) sprite({ x: at.x, z: at.z + .12 }, .3, .3, new Color(1, .2, .1, .9), glow, LightLayer);
    }
    blast(stop, s - t.boom, BlastRadius);

    // Carrier, the group at the chosen cell, and in the blocked scenario the raider on the path.
    pawn('grasp carrier', carrier, CarrierColour, L);
    if (rescue) {                                                    // two raiders firing from beyond the body; sandbags and a medic at the chosen cell
      pawn('grasp raider 0', t.place(5.5, -1.2), EnemyColour, L); pawn('grasp raider 1', t.place(6.5, .7), EnemyColour, L);
      const c = Math.cos(t.turnDeg * Mathf.Deg2Rad) * p.slide, d = Math.sin(t.turnDeg * Mathf.Deg2Rad) * p.slide;
      for (let k = -1; k <= 1; k++) {
        const b = t.place(c + 1.1, d + k);
        draw(disc, b.x, itemLayer, b.z, .47, .27, -(p.aim + 90), new Color(.45, .39, .27));
        draw(disc, b.x, itemLayer + .001, b.z + .05, .42, .21, -(p.aim + 90), new Color(.64, .57, .41));
      }
      pawn('grasp medic', t.place(c - .2, d + 1), AllyColour, L);
    } else Group.forEach(([dx, dz], i) => {
      const g = { x: dest.x + dx, z: dest.z + dz };
      if (blown && mode === 0) downedPawn(`grasp group ${i}`, { x: g.x + dx * .5, z: g.z + dz * .5 }, EnemyColour, L, 40 + i * 110);
      else pawn(`grasp group ${i}`, g, EnemyColour, L);
    });
    if (mode === 1) {
      const step = 2.5 * (1 - smooth((s - (t.slideStart + WalkerLead - .8)) / .8)), side = { x: t.across.x - o.x, z: t.across.z - o.z };
      const walker = { x: stop.x + side.x * (step + .12), z: stop.z + side.z * (step + .12) };
      if (blown) downedPawn('grasp walker', { x: walker.x + side.x * .6, z: walker.z + side.z * .6 }, EnemyColour, L, p.aim + p.turn + 90);
      else pawn('grasp walker', walker, EnemyColour, L);
    }
  },
};
