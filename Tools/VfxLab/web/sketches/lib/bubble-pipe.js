// Shared drawing for the Bubble Pipe kit (Utakata's weapon): the bamboo blowpipe, the soap jar at
// the hip with its visible level, a soap bubble at any height, a bubble forming on the pipe's tip,
// and a bubble popping. Every Bubble Pipe sketch draws through these so the C# port is one class.
//
// A bubble is a sphere, so on screen it is a level circle shifted north by h * Lift with a ground
// shadow at its base: it reads the same from every facing and needs no per-facing method. The film
// is a thin rim ring with two thin coloured rings inside it (the iridescence), an almost clear fill,
// a highlight crescent to the upper left and a shade crescent to the lower right. Wobble is a
// stretch of the circle along x or z. The pipe lies along the aim at mouth height.
import { AltitudeLayer, Color, Mathf, Meshes } from '../../js/engine.js';
import { draw } from './six-paths-solid.js';
import { Body, Y, Floor, Lift, sprite, band, circle, soft, rand } from './six-paths-impact.js';
import { frame, tube, rect, figure, disc, shadowLayer, pawnLayer, Skin, Water, WaterLit, WaterDark, Foam } from './water-gun.js';
export { frame, tube, rect, figure, disc, shadowLayer, pawnLayer, Skin, Water, WaterLit, WaterDark, Foam };

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp, TAU = Math.PI * 2;
const ring = Meshes.band(.93, 1, 48, 'bubble ring');
const thin = Meshes.band(.965, 1, 48, 'bubble thin ring');
export const Film = new Color(.90, .96, 1), FilmFill = new Color(.85, .93, 1), Shade = new Color(.35, .45, .70);
export const Iris1 = new Color(1, .70, .85), Iris2 = new Color(.65, .95, .85);   // the two iridescent tints
export const Bamboo = new Color(.74, .64, .36), BambooDark = new Color(.42, .34, .16), BambooLit = new Color(.88, .80, .52);
export const Jar = new Color(.20, .36, .40), JarLit = new Color(.34, .54, .58), JarDark = new Color(.08, .16, .18), Cork = new Color(.60, .46, .28);
export const Soap = new Color(.70, .88, .95, .85);
// Decided looks and the rule's fixed numbers.
export const MouthH = .62;                         // the pipe's height at the mouth, cells
export const PipeLen = .85, PipeW = .055;
export const JarCap = 10;                          // blows the jar holds
export const JarR = .11, JarH = .30, JarBase = .30;
export const JarAlong = -.02, JarAcross = -.30;    // left hip
export const Lead = .2, Raise = .25, Lower = .3, Tail = .2;
export const bump = x => (x >= 0 && x <= 1) ? Math.sin(x * Math.PI) : 0;

// One bubble. g is its ground point, h its height, r its radius in cells, wobble 0..1 a stretch
// amount, phase turns the stretch. alpha fades it. film 0..1 thins the rim as it is about to pop.
export function bubble(g, h, r, s, { id = 0, wobble = .06, phase = 0, alpha = 1, sun, strength = .32, layer = Y + .05 } = {}) {
  if (r <= 0 || alpha <= 0) return;
  const c = { x: g.x, z: g.z + h * Lift };
  const sx = r * (1 + wobble * Math.sin(s * 7 + phase)), sz = r * (1 - wobble * Math.sin(s * 7 + phase));
  sprite({ x: g.x + sun.x * h * .5, z: g.z + sun.z * h * .5 }, r * 2.2, r * 1.4, Body.withAlpha(strength * .25 * alpha), soft, shadowLayer);
  draw(disc, c.x, layer, c.z, sx, sz, 0, FilmFill.withAlpha(.10 * alpha));
  draw(ring, c.x, layer + .002, c.z, sx * .97, sz * .97, 0, Iris1.withAlpha(.35 * alpha));
  draw(ring, c.x, layer + .003, c.z, sx * .91, sz * .91, 0, Iris2.withAlpha(.28 * alpha));
  draw(thin, c.x, layer + .004, c.z, sx, sz, 0, Film.withAlpha(.85 * alpha));
  // highlight crescent upper left, shade crescent lower right: short thick arcs
  arc(`bubble hi ${id}`, c, sx * .72, sz * .72, 115, 160, r * .14, Foam.withAlpha(.85 * alpha), layer + .006);
  arc(`bubble sh ${id}`, c, sx * .86, sz * .86, 290, 350, r * .06, Shade.withAlpha(.35 * alpha), layer + .005);
}
// A thick arc from a0 to a1 degrees (0 east, counter-clockwise on screen), width w, on an ellipse.
export function arc(key, c, rx, rz, a0, a1, w, colour, layer) {
  const A = [], B = [], n = 10;
  for (let i = 0; i <= n; i++) {
    const t = (a0 + (a1 - a0) * i / n) * Mathf.Deg2Rad, ww = w * Math.sin(i / n * Math.PI);
    A.push({ x: c.x + Math.cos(t) * (rx - ww / 2), z: c.z + Math.sin(t) * (rz - ww / 2) });
    B.push({ x: c.x + Math.cos(t) * (rx + ww / 2), z: c.z + Math.sin(t) * (rz + ww / 2) });
  }
  band(key, A, B, colour, layer);
}
// A bubble popping at ground point g, height h, radius r. age from the pop. A soap film goes in
// one flash: the rim tears at one point and the film retracts around the circle away from the
// tear over 0.12 s, gathering thicker as it goes; 12 iridescent specks fly off the film and fade
// in 0.3 s, drifting down a little; a thin ring expands and thins for 0.1 s. Nothing is left on the floor. No water: a bubble holds almost nothing.
export function pop(key, g, h, r, age, { sun, strength = .32, layer = Y + .05, seed = 0 } = {}) {
  if (age < 0) return;
  const c = { x: g.x, z: g.z + h * Lift };
  const tear = rand(seed + 1300) * 360;
  if (age < .12) {
    const u = age / .12, gone = u * 180;                     // degrees of film gone on each side of the tear
    if (gone < 178) {
      arc(`${key} film a`, c, r, r, tear + gone, tear + 180, r * (.03 + .08 * u), Film.withAlpha(.9), layer + .004);
      arc(`${key} film b`, c, r, r, tear + 180, tear + 360 - gone, r * (.03 + .08 * u), Film.withAlpha(.9), layer + .004);
      arc(`${key} iris a`, c, r * .94, r * .94, tear + gone, tear + 180, r * .05, Iris1.withAlpha(.4 * (1 - u)), layer + .003);
      arc(`${key} iris b`, c, r * .94, r * .94, tear + 180, tear + 360 - gone, r * .05, Iris2.withAlpha(.35 * (1 - u)), layer + .003);
    }
  }
  if (age < .1) draw(thin, c.x, layer + .002, c.z, r * (1 + age * 4), r * (1 + age * 4), 0, Film.withAlpha(.6 * (1 - age / .1)));
  for (let i = 0; i < 12; i++) {
    const life = .18 + rand(i + seed + 1310) * .14, u = age / life;
    if (u > 1) continue;
    const a = (tear + 30 + i / 12 * 300 + rand(i + seed + 1320) * 20) * Mathf.Deg2Rad, far = r * (1 + u * 1.8 * (.6 + rand(i + seed + 1330)));
    const q = { x: c.x + Math.cos(a) * far, z: c.z + Math.sin(a) * far - u * u * .12 };
    const col = i % 3 === 0 ? Iris1 : i % 3 === 1 ? Iris2 : Foam;
    draw(disc, q.x, layer + .008, q.z, .022 * (1 - u * .5), .03 * (1 - u * .5), 0, col.withAlpha(.9 * (1 - u)));
  }
}

// Draws the pipe and the jar for one frame.
//   raise    0..1, how far the pipe is up at the mouth (0 = rest, hanging at the side)
//   blows    soap left in the jar, 0..JarCap
//   forming  radius of a bubble growing on the tip right now (0 = none)
// Returns the tip point on screen and on the ground, and the aim frame's along/across for it.
export function drawPipe(f, caster, s, { raise, blows, forming = 0, actors, sun, strength }) {
  const { place, cast } = f;
  const lift = smooth(raise);

  // The jar at the left hip: a small cylinder (level circle top) with the soap level inside it,
  // a rim, a cork. Drawn under the pawn when the hip is on the far (north) side.
  const J = place(caster, JarAlong, JarAcross, JarBase), behind = J.z > caster.z + .06;
  const jl = behind ? pawnLayer - .03 : Y + .001;
  sprite(cast(caster, JarAlong, JarAcross, JarBase + JarH * .5), JarR * 2.6, JarR * 1.5, Body.withAlpha(strength * .5), soft, shadowLayer);
  const side = (r, h0, h1, n = 14) => {
    const A = [], C = [];
    for (let i = 0; i <= n; i++) {
      const th = Math.PI + i / n * Math.PI, x = J.x + Math.cos(th) * r, z = J.z + Math.sin(th) * r;
      A.push({ x, z: z + h0 * Lift }); C.push({ x, z: z + h1 * Lift });
    }
    return [A, C];
  };
  const level = clamp(blows / JarCap) * JarH;
  const [SA, SC] = side(JarR, 0, JarH);
  band('bubble pipe jar side', SA, SC, JarDark, jl);
  if (level > 0) {
    const [WA, WC] = side(JarR * .9, .01, level);
    band('bubble pipe jar soap', WA, WC, Soap, jl + .002);
  }
  const [LA, LC] = side(JarR * .55, .02, JarH * .96, 6);
  band('bubble pipe jar lit', LA.map(q => ({ x: q.x - JarR * .3, z: q.z })), LC.map(q => ({ x: q.x - JarR * .3, z: q.z })), JarLit.withAlpha(.5), jl + .003);
  draw(disc, J.x, jl + .004, J.z + JarH * Lift, JarR, JarR, 0, Jar);
  circle({ x: J.x, z: J.z + JarH * Lift }, JarR, .9, jl + .005, JarLit);
  draw(disc, J.x, jl + .006, J.z + JarH * Lift + .01, JarR * .5, JarR * .5, 0, Cork);
  // strap from the jar to the belt
  tube('bubble pipe jar strap', [place(caster, JarAlong, JarAcross, JarBase + JarH), place(caster, .02, -.18, .42)], () => .02, BambooDark, jl + .007);

  // The pipe: at rest it hangs at the right side pointing down-forward; raised it runs from the
  // mouth along the aim, tip slightly up.
  const la = lift, lb = lift;
  const PA = [lerp(.05, .08, la), lerp(.24, .03, la), lerp(.45, MouthH, la)];                 // mouth end
  const PB = [lerp(.55, .08 + PipeLen, lb), lerp(.36, 0, lb), lerp(.05, MouthH + .06, lb)];    // tip
  const pa = place(caster, PA[0], PA[1], PA[2]), pb = place(caster, PB[0], PB[1], PB[2]);
  const sa = cast(caster, PA[0], PA[1], PA[2]), sb = cast(caster, PB[0], PB[1], PB[2]);
  const pl = Y + .014;
  tube('bubble pipe shadow', [sa, sb], () => PipeW * 1.2, Body.withAlpha(strength * .6), shadowLayer);
  tube('bubble pipe outline', [pa, pb], () => PipeW + .02, BambooDark, pl);
  tube('bubble pipe body', [pa, pb], () => PipeW, Bamboo, pl + .002);
  tube('bubble pipe lit', [pa, pb], () => PipeW, BambooLit, pl + .004, .1, .5);
  for (let k = 1; k <= 3; k++) {   // bamboo nodes
    const u = k / 4, c = { x: lerp(pa.x, pb.x, u), z: lerp(pa.z, pb.z, u) }, deg = Math.atan2(pb.z - pa.z, pb.x - pa.x) / Mathf.Deg2Rad + 90;
    rect(`bubble pipe node ${k}`, c, PipeW + .02, .025, deg, BambooDark, pl + .006);
  }
  draw(disc, pb.x, pl + .007, pb.z, PipeW * .55, PipeW * .55, 0, JarDark);   // the bore
  if (actors) {
    const hu = .35, h2 = .62;
    draw(disc, lerp(pa.x, pb.x, hu), pl + .008, lerp(pa.z, pb.z, hu), .07, .07, 0, Skin);
    draw(disc, lerp(pa.x, pb.x, h2), pl + .008, lerp(pa.z, pb.z, h2), .07, .07, 0, Skin);
  }
  // A bubble forming on the tip: a film that bulges out along the aim as it grows.
  if (forming > 0) {
    const g = place(caster, PB[0] + forming * .9, PB[1]), c = { x: g.x, z: g.z + PB[2] * Lift };
    draw(disc, c.x, pl + .010, c.z, forming, forming * .9, 0, FilmFill.withAlpha(.12));
    draw(thin, c.x, pl + .012, c.z, forming, forming * .9, 0, Film.withAlpha(.8));
    draw(ring, c.x, pl + .011, c.z, forming * .95, forming * .85, 0, Iris1.withAlpha(.3));
  }
  return { tipS: pb, tipG: place(caster, PB[0], PB[1]), tipAlong: PB[0], tipAcross: PB[1], tipH: PB[2] };
}
