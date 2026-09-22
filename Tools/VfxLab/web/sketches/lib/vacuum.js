// Shared drawing for the Vacuum kit: the conjured canister with its face, the hose, the hip coil,
// the wand with its head, and the churn of dirt when the canister rises out of or sinks into the
// floor. Suck and Spit both draw the weapon through drawVacuum() so the two stay identical, and a
// C# port makes one class of it.
//
// The canister is a level cylinder (its top is the same circle shifted north by CanH * Lift), so
// it reads the same from every facing. The hose is a curve at hand height and the wand lies along
// the aim; both turn with the aim, so nothing here needs a per-facing method.
import { AltitudeLayer, Color, MaterialPool, Mathf, Meshes, ShaderDatabase } from '../../js/engine.js';
import { draw } from './six-paths-solid.js';
import { Body, Y, Floor, Lift, sprite, band, circle, soft, rand } from './six-paths-impact.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
export const disc = Meshes.disc(40, 'vacuum disc');
export const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
export const shadowLayer = AltitudeLayer.Shadows.AltitudeFor(), pawnLayer = AltitudeLayer.Pawn.AltitudeFor();
export const Skin = new Color(.83, .70, .54), Pale = new Color(1, .96, .85), Dust = new Color(.80, .74, .63);
export const Can = new Color(.12, .30, .33), CanLit = new Color(.22, .46, .48), CanDark = new Color(.06, .16, .18), CanRim = new Color(.34, .60, .62);
export const Hose = new Color(.26, .26, .30), HoseLit = new Color(.44, .44, .50), HoseDark = new Color(.07, .07, .09);
export const EyeWhite = new Color(.97, .97, .95), Pupil = new Color(.05, .05, .06);
export const MouthIn = new Color(.32, .06, .08), Lip = new Color(.05, .05, .06), Tooth = new Color(.95, .93, .85);
export const Stone = new Color(.40, .39, .41), StoneTop = new Color(.57, .56, .59);
export const Wood = new Color(.38, .23, .12), Steel = new Color(.32, .34, .38), Blood = new Color(.44, .06, .06);
// Decided looks and the rule's fixed numbers.
export const HandH = .5;                          // the wand's height when lifted, cells
export const CanR = .45, CanH = .65;              // canister radius and height, cells
export const CanAlong = -.55, CanAcross = .85;    // where the canister rises, from the caster's feet
export const NozzleBack = .30, NozzleTip = .80;   // the lifted wand along the aim from the caster's feet
export const HoseW = .085;                        // hose half width
export const Bulge = .35;                         // one thing takes this long to run the hose
export const Lead = .25, Rise = .25, Sink = .3, Tail = .25;  // wand alone, canister rising, sinking, wand alone again
export const bump = x => (x >= 0 && x <= 1) ? Math.sin(x * Math.PI) : 0;

// The aim frame: along the cast direction, across it, h cells up. cast() is the same point's shadow.
export function frame(aimDeg, sun) {
  const a = aimDeg * Mathf.Deg2Rad, ca = Math.cos(a), sa = Math.sin(a);
  return {
    ca, sa,
    place: (base, along, across, h = 0) => ({ x: base.x + along * ca - across * sa, z: base.z + along * sa + across * ca + h * Lift }),
    cast: (base, along, across, h = 0) => ({ x: base.x + along * ca - across * sa + sun.x * h, z: base.z + along * sa + across * ca + sun.z * h }),
  };
}
// A rect is a band of four corners; a tube is a band along a point list with a half width per point.
export function rect(key, c, len, wid, deg, colour, layer) {
  const r = deg * Mathf.Deg2Rad, cx = Math.cos(r), sx = Math.sin(r);
  const ax = cx * len / 2, az = sx * len / 2, bx = -sx * wid / 2, bz = cx * wid / 2;
  band(key, [{ x: c.x - ax - bx, z: c.z - az - bz }, { x: c.x + ax - bx, z: c.z + az - bz }],
    [{ x: c.x - ax + bx, z: c.z - az + bz }, { x: c.x + ax + bx, z: c.z + az + bz }], colour, layer);
}
export function tube(key, pts, wf, colour, layer, lo = -1, hi = 1) {
  const A = [], B = [], n = pts.length - 1;
  for (let i = 0; i <= n; i++) {
    const pr = pts[Math.max(0, i - 1)], nx = pts[Math.min(n, i + 1)];
    let dx = nx.x - pr.x, dz = nx.z - pr.z; const L = Math.hypot(dx, dz) || 1; dx /= L; dz /= L;
    const w = wf(i / n), q = pts[i];
    A.push({ x: q.x - dz * w * lo, z: q.z + dx * w * lo }); B.push({ x: q.x - dz * w * hi, z: q.z + dx * w * hi });
  }
  band(key, A, B, colour, layer);
}
// Stand-in pawn: two discs and a soft shadow.
export function figure(pos, colour, sun, strength) {
  sprite({ x: pos.x + sun.x * .45, z: pos.z + sun.z * .45 }, .85, .4, Body.withAlpha(strength), soft, shadowLayer);
  draw(disc, pos.x, pawnLayer, pos.z + .18, .22, .32, 0, colour);
  draw(disc, pos.x, pawnLayer + .002, pos.z + .58, .16, .17, 0, Skin);
}
// Stand-in things that get swallowed or spat: a stone chunk and a rifle. g is the ground point,
// h the height, scale shrinks them near the mouth.
export function chunk(g, h, scale, layer, sun, strength) {
  sprite({ x: g.x + sun.x * (h + .15), z: g.z + sun.z * (h + .15) }, .75 * scale, .45 * scale, Body.withAlpha(strength), soft, shadowLayer);
  draw(disc, g.x, layer, g.z + h * Lift, .30 * scale, .25 * scale, 0, Stone);
  draw(disc, g.x, layer + .002, g.z + h * Lift + .11 * scale, .27 * scale, .21 * scale, 0, StoneTop);
}
export function rifle(key, g, h, scale, deg, layer, sun, strength) {
  const c = { x: g.x, z: g.z + h * Lift }, L = .6 * scale, W = .09 * scale;
  sprite({ x: g.x + sun.x * h, z: g.z + sun.z * h }, .6 * scale, .25 * scale, Body.withAlpha(strength * .8), soft, shadowLayer);
  rect(`${key} stock`, c, L, W, deg, Wood, layer + .003);
  const r = deg * Mathf.Deg2Rad;
  rect(`${key} barrel`, { x: c.x + Math.cos(r) * L * .28, z: c.z + Math.sin(r) * L * .28 }, L * .5, W * .55, deg, Steel, layer + .004);
}

// Draws the whole weapon for one frame.
//   present   0..1, how far the canister is out of the floor (0 = wand alone, coil at the hip)
//   churn     0..1, dirt ring and dust at the canister's base (rise and sink)
//   swell     extra size, 1 per thing held (0.05 R and 0.04 H each)
//   pulse     0..1 momentary swell as a thing lands
//   blink     0..1 eyes shut
//   mouthOpen 0..1
//   bulges    list of u along the hose (0 = wand, 1 = canister) where a thing is
//   slack     hose slack in cells
// Returns the canister's ground centre, radius, height, top and layer, the wand's head point (screen
// and ground) and a hoseAt(u) sampler for things in transit.
export function drawVacuum(f, caster, o, { present, churn, swell, pulse, blink, mouthOpen, bulges, slack, actors, sun, strength }) {
  const { ca, sa, place, cast } = f;
  const R = CanR * (1 + .05 * swell + .10 * pulse), H = CanH * present * (1 + .04 * swell + .08 * pulse);
  const C = place(caster, CanAlong, CanAcross), canLayer = pawnLayer + (C.z < caster.z ? .012 : -.012);
  if (churn > 0) {
    circle(C, CanR + .08, .7 * churn, Floor + .02, HoseDark);
    sprite(C, CanR * 2.6, CanR * 2.0, Body.withAlpha(.35 * churn), soft, Floor + .01);
    for (let k = 0; k < 8; k++) {
      const th = rand(k + 400) * TAU, d = CanR + .1 + churn * .35 * (.5 + rand(k + 410));
      sprite({ x: C.x + Math.cos(th) * d, z: C.z + Math.sin(th) * d * .7 + churn * .1 * Lift }, .28, .22, Dust.withAlpha(.6 * churn), puff, Y - .04);
    }
  }
  if (present > 0) {
    const top = C.z + H * Lift;
    sprite({ x: C.x + sun.x * H * .6, z: C.z + sun.z * H * .6 }, R * 2.8, R * 1.6, Body.withAlpha(strength), soft, shadowLayer);
    const A = [], B = [], A2 = [], B2 = [];
    for (let i = 0; i <= 24; i++) {
      const th = Math.PI + i / 24 * Math.PI, x = C.x + Math.cos(th) * R, z = C.z + Math.sin(th) * R;
      A.push({ x, z }); B.push({ x, z: z + H * Lift });
    }
    band('vacuum can side', A, B, CanDark, canLayer);
    for (let i = 0; i <= 8; i++) {
      const th = Math.PI * 1.5 + (i / 8 - .5) * .9, x = C.x + Math.cos(th) * R, z = C.z + Math.sin(th) * R;
      A2.push({ x, z: z + H * Lift * .06 }); B2.push({ x, z: z + H * Lift * .94 });
    }
    band('vacuum can side lit', A2, B2, Can, canLayer + .002);
    draw(disc, C.x, canLayer + .004, top, R, R, 0, Can);
    draw(disc, C.x, canLayer + .005, top + R * .06, R * .82, R * .82, 0, CanLit);
    circle({ x: C.x, z: top }, R, .9, canLayer + .006, CanRim);
    draw(disc, C.x, canLayer + .007, top, .10, .10, 0, HoseDark);      // hose socket
    // Face on the south side: two eyes that blink, a mouth that opens.
    const eyeZ = C.z - R * .90 + H * Lift * .66, eyeH = .15 * (1 - .9 * blink);
    for (const sx of [-1, 1]) {
      const ex = C.x + sx * R * .42;
      draw(disc, ex, canLayer + .008, eyeZ, .15, eyeH, 0, EyeWhite);
      draw(disc, ex + ca * .03, canLayer + .009, eyeZ + sa * .03 * Lift, .07, Math.min(eyeH, .085), 0, Pupil);
    }
    const mZ = C.z - R + H * Lift * .30, mW = R * 1.15, mH = .045 + .19 * mouthOpen;
    draw(disc, C.x, canLayer + .008, mZ, mW / 2 + .02, mH / 2 + .02, 0, Lip);
    draw(disc, C.x, canLayer + .009, mZ, mW / 2, mH / 2, 0, MouthIn);
    for (let k = 0; k < 4; k++) {
      const tx = C.x + (k - 1.5) * mW * .22, th = Math.min(.05, mH * .45);
      draw(disc, tx, canLayer + .010, mZ + mH / 2 - th / 2 - .005, .035, th / 2, 0, Tooth);
    }
  }

  // The wand (aim frame: along, across, height). At rest it hangs at the pawn's right side with its
  // wide floor head resting on the ground ahead; as the canister rises it lifts to hand height and
  // points along the aim, and the head becomes the mouth. Between the two poses it lerps by present.
  const lift = smooth(present);
  const WA = [lerp(.10, NozzleBack, lift), lerp(-.26, 0, lift), lerp(.55, HandH, lift)];   // the grip
  const WB = [lerp(.62, NozzleTip, lift), lerp(-.42, 0, lift), lerp(.04, HandH, lift)];    // the head
  // The hose: a curve from the grip to the canister's top. u = 0 is the wand end, u = 1 the canister.
  // While the canister rises or sinks only the first `present` share of it exists.
  const P0 = WA, P2 = [CanAlong, CanAcross, H], P1 = [(NozzleBack + CanAlong) / 2 - .1, CanAcross * .55 + slack, HandH + .35 + slack * .3];
  const hoseAim = u => { const w0 = (1 - u) * (1 - u), w1 = 2 * (1 - u) * u, w2 = u * u;
    return [w0 * P0[0] + w1 * P1[0] + w2 * P2[0], w0 * P0[1] + w1 * P1[1] + w2 * P2[1], w0 * P0[2] + w1 * P1[2] + w2 * P2[2]]; };
  const hosePts = [], hoseShadow = [], N = 30, M = Math.max(2, Math.round(N * present));
  if (present > 0) for (let i = 0; i <= M; i++) {
    const q = hoseAim(i / N);
    hosePts.push(place(caster, q[0], q[1], q[2])); hoseShadow.push(cast(caster, q[0], q[1], q[2]));
  }
  const widthAt = u => bulges.reduce((w, ub) => w + HoseW * 1.1 * Math.exp(-Math.pow((u - ub) / .07, 2)), HoseW);
  if (present > 0) {
    tube('vacuum hose shadow', hoseShadow, widthAt, Body.withAlpha(strength * .8), shadowLayer);
    tube('vacuum hose outline', hosePts, u => widthAt(u) + .025, HoseDark, Y);
    tube('vacuum hose body', hosePts, widthAt, Hose, Y + .002);
    tube('vacuum hose lit', hosePts, widthAt, HoseLit, Y + .004, .15, .6);
  }
  // At rest the hose is coiled at the left hip: two loops and a short run to the grip. The loops
  // unwind (shrink) as the canister rises and wind back up as it sinks. When the hip is on the far
  // side of the pawn (north on screen) the coil hangs behind the body.
  if (lift < 1) {
    const coil = [-.10, .42, .20], r0 = .22 * (1 - lift), cw = HoseW * .7;
    const coilC = place(caster, coil[0], coil[1], coil[2]), CL = coilC.z > caster.z + .05 ? pawnLayer - .03 : Y;
    for (let loop = 0; loop < 2; loop++) {
      const pts = [];
      for (let i = 0; i <= 26; i++) {
        const th = i / 26 * TAU, r = r0 * (1 - loop * .30 - .06 * i / 26);
        pts.push(place(caster, coil[0] + Math.cos(th) * r, coil[1] + Math.sin(th) * r * .85, coil[2] + loop * .06));
      }
      tube(`vacuum coil outline ${loop}`, pts, () => cw + .02, HoseDark, CL + loop * .006);
      tube(`vacuum coil body ${loop}`, pts, () => cw, Hose, CL + .002 + loop * .006);
      tube(`vacuum coil lit ${loop}`, pts, () => cw, HoseLit, CL + .004 + loop * .006, .1, .55);
    }
    const run = [WA, [lerp(.02, NozzleBack - .1, lift), lerp(.04, .12, lift), .5], [coil[0] + r0, coil[1] - r0 * .3, coil[2]]].map(q => place(caster, q[0], q[1], q[2]));
    tube('vacuum run outline', run, () => HoseW + .025, HoseDark, CL - .002);
    tube('vacuum run body', run, () => HoseW, Hose, CL);
  }

  // The wand: a tube from the grip to the head, and the head itself, a wide flat bar across the
  // wand with a dark slot. At rest the bar is a floor head; lifted it is the mouth things pass.
  const wa = place(caster, WA[0], WA[1], WA[2]), wb = place(caster, WB[0], WB[1], WB[2]);
  const wandPts = [wa, { x: lerp(wa.x, wb.x, .5), z: lerp(wa.z, wb.z, .5) }, wb];
  const wandShadow = [cast(caster, WA[0], WA[1], WA[2]), cast(caster, WB[0], WB[1], WB[2])];
  const wandW = u => lerp(.065, .09, u);
  tube('vacuum wand shadow', wandShadow, wandW, Body.withAlpha(strength * .7), shadowLayer);
  tube('vacuum wand outline', wandPts, u => wandW(u) + .025, HoseDark, Y + .006);
  tube('vacuum wand body', wandPts, wandW, Hose, Y + .008);
  tube('vacuum wand lit', wandPts, wandW, HoseLit, Y + .010, .15, .6);
  const headDeg = Math.atan2(wb.z - wa.z, wb.x - wa.x) / Mathf.Deg2Rad + 90;
  const headLen = lerp(.58, .30, lift), headWid = lerp(.17, .13, lift);
  sprite({ x: wandShadow[1].x, z: wandShadow[1].z }, headLen * 1.1, headWid * 1.6, Body.withAlpha(strength * .6), soft, shadowLayer);
  rect('vacuum head outline', wb, headLen + .04, headWid + .04, headDeg, HoseDark, Y + .012);
  rect('vacuum head body', wb, headLen, headWid, headDeg, Hose, Y + .014);
  rect('vacuum head lit', { x: wb.x, z: wb.z + headWid * .22 }, headLen * .9, headWid * .22, headDeg, HoseLit, Y + .016);
  const fwd = (headDeg - 90) * Mathf.Deg2Rad;
  rect('vacuum head slot', { x: wb.x + Math.cos(fwd) * headWid * .18, z: wb.z + Math.sin(fwd) * headWid * .18 }, headLen * .82, headWid * .38, headDeg, Lip, Y + .018);
  if (actors) draw(disc, wa.x, Y + .020, wa.z, .075, .075, 0, Skin);

  return {
    can: { C, R, H, top: C.z + H * Lift, layer: canLayer },
    tipG: place(caster, NozzleTip, 0), tipS: place(caster, NozzleTip, 0, HandH),
    hoseAt: u => { const q = hoseAim(u); return { ground: place(caster, q[0], q[1]), screen: place(caster, q[0], q[1], q[2]), h: q[2] }; },
  };
}
