// Twin Maw — directional projectile parry proposal, not an in-game interception system.
// Two orbs unfold into jaws. Only the open jaws are an active parry window.
// Success: catch at 60% of that window, snap shut, morph to a split barrel, return
// the captured shot, then reform. Miss: window expires, jaws reform, a late shot
// crosses the empty guard position and hits the caster; no return shot is drawn.
// Optional mannequins/projectile demonstrate gameplay intent; replace them with real
// actors and projectile events in a C# port. Uses shipped textures and ordinary meshes.
import {
  AltitudeLayer, Color, Graphics, MaterialPool, MaterialPropertyBlock, Mathf,
  Matrix4x4, Mesh, Meshes, MeshPool, Quaternion, ShaderDatabase, ShaderPropertyIDs, Vector3,
} from '../js/engine.js';

const ink = new Color(0.028, 0.024, 0.040);
const edge = new Color(0.63, 0.54, 0.81);
const pale = new Color(0.91, 0.87, 1);
const amber = new Color(1, 0.57, 0.19);
const solid = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const glow = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.MoteGlow);
const soft = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent);
const disc = Meshes.disc(48, 'maw orb');
const ring = Meshes.band(0.94, 1, 64, 'maw contact ring');
const shapes = Array.from({ length: 2 }, (_, i) => ({ core: new Mesh(`maw plate ${i}`), rim: new Mesh(`maw rim ${i}`) }));
const props = new MaterialPropertyBlock();
const floor = AltitudeLayer.Filth.AltitudeFor();
const y = AltitudeLayer.MoteOverhead.AltitudeFor();
const pawn = AltitudeLayer.Pawn.AltitudeFor();
const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });

function timing(p) {
  const success = p.outcome === 'catch';
  const close = p.open + p.window;
  const contact = success ? p.open + p.window * 0.6 : close + p.reform + 0.15;
  const inbound = Math.min(p.flight, contact - 0.05);
  const sealed = contact + p.snap, fire = sealed + p.barrel;
  const reform = success ? fire + 0.16 : close;
  const hit = success ? fire + p.flight : contact + inbound * 1.65 / p.distance;
  const end = Math.max(reform + p.reform, hit + 0.25) + 0.35;
  return { success, close, contact, inbound, sealed, fire, reform, hit, end };
}

function world(o, p, x, z) {
  const a = p.aim * Mathf.Deg2Rad;
  return new Vector3(o.x + x * Math.cos(a) - z * Math.sin(a), y,
    o.z + x * Math.sin(a) + z * Math.cos(a));
}
function paint(m, o, p, x, z, layer, w, h, c, mat = solid, upright = false) {
  if (c.a <= 0 || w <= 0 || h <= 0) return;
  props.SetColor(ShaderPropertyIDs.Color, c);
  Graphics.DrawMesh(m, Matrix4x4.TRS(world(o, p, x, z).WithY(layer),
    Quaternion.Euler(0, upright ? 0 : -p.aim, 0), new Vector3(w, 1, h)), mat, 0, null, 0, props);
}

// Same vertices morph from circle -> curved jaw -> barrel half -> circle.
// Each outline has its own mesh; the renderer reads it after draw() returns.
function plate(i, opened, shut, barrel, recall, p, o, alpha) {
  const sign = i ? 1 : -1;
  const vertices = [], outer = [], tri = [];
  const jawCenterZ = sign * (p.gap * (1 - shut) + p.thickness * 0.43);
  const cx = lerp(lerp(-1.2, 0, opened), -1.2, recall);
  const cz = lerp(lerp(sign * 0.58, jawCenterZ, opened), sign * 0.58, recall);
  vertices.push(cx, cz); outer.push(cx, cz);
  for (let j = 0; j < 64; j++) {
    const a = j / 64 * Math.PI * 2, c = Math.cos(a), s = Math.sin(a);
    const xJaw = c * p.length * 0.5;
    // The tips flare away from the incoming lane; closure removes the curvature.
    const zJaw = s * p.thickness * 0.5 + sign * (c + 0.3) ** 2 * 0.23 * (1 - shut);
    let x = lerp(c * 0.27, lerp(xJaw, c * p.length * 0.72 + 0.15, barrel), opened);
    let z = lerp(s * 0.27, lerp(zJaw, s * p.thickness * 0.33, barrel), opened);
    x = lerp(x, c * 0.27, recall); z = lerp(z, s * 0.27, recall);
    vertices.push(cx + x, cz + z);
    outer.push(cx + x + c * p.rim, cz + z + s * p.rim);
    tri.push(0, j + 1, (j + 1) % 64 + 1);
  }
  shapes[i].core.setFlat(vertices, tri); shapes[i].rim.setFlat(outer, tri);
  paint(shapes[i].rim, o, p, 0, 0, y + i * 0.01, 1, 1, edge.withAlpha(alpha));
  paint(shapes[i].core, o, p, 0, 0, y + i * 0.01 + 0.002, 1, 1, ink.withAlpha(alpha));
}

function shot(x, direction, returned, o, p, alpha) {
  for (let i = 5; i >= 1; i--)
    paint(MeshPool.plane10, o, p, x - direction * i * 0.13, 0, y + 0.04,
      0.38, 0.13, (returned ? pale : amber).withAlpha(alpha * (1 - i / 6) * 0.4), glow);
  paint(MeshPool.plane10, o, p, x, 0, y + 0.05, 0.56, 0.4,
    (returned ? pale : amber).withAlpha(alpha * 0.8), glow);
  paint(disc, o, p, x, 0, y + 0.06, 0.10, 0.07, amber.withAlpha(alpha));
  paint(disc, o, p, x, 0, y + 0.061, 0.045, 0.035, pale.withAlpha(alpha));
}

function mannequin(x, colour, o, p, alpha) {
  const pos = world(o, p, x, 0);
  // Mannequins stay upright even when the parry direction changes.
  const ctx = { x: pos.x, z: pos.z }, neutral = { aim: 0 };
  paint(MeshPool.plane10, ctx, neutral, 0, 0, floor, 0.8, 0.45, ink.withAlpha(alpha * 0.4), soft);
  paint(disc, ctx, neutral, 0, 0.18, pawn, 0.23, 0.31, colour.withAlpha(alpha));
  paint(disc, ctx, neutral, 0, 0.56, pawn + 0.002, 0.16, 0.17, new Color(0.80, 0.69, 0.53, alpha));
}

export default {
  kit: 'Six Paths', label: 'Twin Maw (sketch)',
  params: {
    outcome: { label: 'Parry result', value: 'catch', options: ['catch', 'miss'], group: 'Showcase' },
    actors: { label: 'Show caster and attacker', value: true, group: 'Showcase' },
    open: P('Orbs unfold', 0.6, 0.25, 1.2, 0.05, 'Timing (s)'),
    window: P('Active parry window', 0.8, 0.3, 1.5, 0.05, 'Timing (s)'),
    snap: P('Jaws snap shut', 0.12, 0.06, 0.3, 0.01, 'Timing (s)'),
    barrel: P('Shape captured shot into barrel', 0.35, 0.15, 0.8, 0.05, 'Timing (s)'),
    flight: P('Projectile flight time', 0.55, 0.25, 1, 0.05, 'Timing (s)'),
    reform: P('Return to two orbs', 0.55, 0.25, 1, 0.05, 'Timing (s)'),
    aim: P('Aim direction (degrees)', 0, 0, 360, 5, 'Shape'),
    distance: P('Attacker distance (cells)', 4.6, 3, 7, 0.1, 'Shape'),
    length: P('Jaw length (cells)', 1.65, 1.2, 2.3, 0.05, 'Shape'),
    gap: P('Open half-gap (cells)', 0.48, 0.3, 0.8, 0.02, 'Shape'),
    thickness: P('Plate thickness (cells)', 0.38, 0.25, 0.55, 0.01, 'Shape'),
    rim: P('Pale edge width (cells)', 0.024, 0.01, 0.05, 0.002, 'Shape'),
    flash: P('Catch and muzzle brightness', 0.65, 0, 1, 0.05, 'Feedback'),
  },
  duration(p) { return timing(p).end; },
  phases(p) {
    const t = timing(p);
    return t.success ? [{ name: 'Unfold', t: 0 }, { name: 'Parry active', t: p.open },
      { name: 'Catch', t: t.contact }, { name: 'Form barrel', t: t.sealed },
      { name: 'Return shot', t: t.fire }, { name: 'Reform', t: t.reform }, { name: 'Attacker hit', t: t.hit }]
      : [{ name: 'Unfold', t: 0 }, { name: 'Parry active', t: p.open },
        { name: 'Window expired', t: t.close }, { name: 'Late shot passes', t: t.contact },
        { name: 'Caster hit', t: t.hit }];
  },
  events() { return []; },
  draw(s, p, { origin: o }) {
    const t = timing(p);
    if (s < 0 || s >= t.end) return;
    const alpha = smooth(s / 0.12) * (1 - smooth((s - (t.end - 0.25)) / 0.25));
    const opened = smooth(s / p.open);
    const shut = t.success ? smooth((s - t.contact) / p.snap) : 0;
    const barrel = t.success ? smooth((s - t.sealed) / p.barrel) : 0;
    const recall = smooth((s - t.reform) / p.reform);
    if (p.actors) {
      mannequin(-1.65, new Color(0.39, 0.58, 0.65), o, p, alpha);
      mannequin(p.distance, new Color(0.65, 0.40, 0.28), o, p, alpha);
    }
    // The narrow directional lane exists only while the parry can accept a shot.
    const activeEnd = t.success ? t.contact : t.close;
    if (s >= p.open && s < activeEnd) {
      paint(MeshPool.plane10, o, p, 0.38, 0, floor + 0.01, 1.8, p.gap * 1.6,
        edge.withAlpha(0.16 * alpha), soft);
      paint(ring, o, p, 0, 0, floor + 0.02, 0.6, p.gap * 1.3, edge.withAlpha(0.23 * alpha));
    }
    for (let i = 0; i < 2; i++) plate(i, opened, shut, barrel, recall, p, o, alpha);
    const incomingStart = t.contact - t.inbound;
    if (s >= incomingStart && s < (t.success ? t.contact : t.hit)) {
      const x = p.distance * (1 - (s - incomingStart) / t.inbound);
      shot(x, -1, false, o, p, alpha);
    }
    if (t.success) {
      if (s >= t.contact && s < t.fire) {
        const capture = smooth((s - t.contact) / p.snap);
        paint(MeshPool.plane10, o, p, 0, 0, y + 0.03, lerp(0.6, 0.24, capture), 0.12,
          amber.withAlpha(0.8 * alpha), glow);
      }
      const catchAge = s - t.contact;
      if (catchAge >= 0 && catchAge < 0.18)
        paint(MeshPool.plane10, o, p, 0, 0, y + 0.07, 0.3, 1.2,
          pale.withAlpha((1 - catchAge / 0.18) * p.flash * alpha), glow);
      if (s >= t.fire && s < t.hit)
        shot(lerp(0, p.distance, (s - t.fire) / p.flight), 1, true, o, p, alpha);
      const fireAge = s - t.fire;
      if (fireAge >= 0 && fireAge < 0.16)
        paint(MeshPool.plane10, o, p, p.length * 0.72, 0, y + 0.07, 0.7, 0.8,
          pale.withAlpha((1 - fireAge / 0.16) * p.flash * alpha), glow);
    }
    const hitAge = s - t.hit;
    if (hitAge >= 0 && hitAge < 0.25) {
      const f = 1 - hitAge / 0.25, x = t.success ? p.distance : -1.65;
      paint(ring, o, p, x, 0, y + 0.08, 0.15 + hitAge * 2, 0.15 + hitAge * 2,
        amber.withAlpha(f * alpha * 0.7));
      paint(MeshPool.plane10, o, p, x, 0, y + 0.09, 0.75, 0.75, amber.withAlpha(f * alpha), glow);
    }
  },
};
