// Black Tide — a standalone Six Paths VFX proposal; no gameplay effect is replaced.
// Six orbs gather (0.45 s), flatten into a sheet (0.35 s), surge along the floor
// (1.05 s), curl into teeth (0.30 s), retract (0.75 s), and reform (0.50 s).
// The raised lip is projected north by SixPathsHeight.Lift; its base stays grounded.
// All geometry is sampled from time, including the return, for backwards scrubbing.
import {
  AltitudeLayer, Color, Graphics, MaterialPool, MaterialPropertyBlock, Mathf,
  Matrix4x4, Mesh, Meshes, MeshPool, Quaternion, ShaderDatabase, ShaderPropertyIDs, Vector3,
} from '../js/engine.js';

const ink = new Color(0.025, 0.020, 0.036);
const rim = new Color(0.53, 0.37, 0.79);
const fold = new Color(0.075, 0.058, 0.105);
const solid = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const soft = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent);
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const props = new MaterialPropertyBlock();
const disc = Meshes.disc(48, 'black tide orb');
const meshes = new Map();
const mesh = key => meshes.get(key) ?? meshes.set(key, new Mesh(`black tide ${key}`)).get(key);
const y = AltitudeLayer.MoteOverhead.AltitudeFor();
const ground = AltitudeLayer.Shadows.AltitudeFor();
const smooth = Mathf.Smooth, lerp = Mathf.Lerp;
const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });

function timing(p) {
  const surge = p.gather + p.form, curl = surge + p.surge;
  const retract = curl + p.curl, reform = retract + p.retract;
  return { surge, curl, retract, reform, end: reform + p.reform + 0.3 };
}

function paint(m, pos, layer, width, depth, colour, material = solid, angle = 0) {
  if (width <= 0 || depth <= 0 || colour.a <= 0) return;
  props.SetColor(ShaderPropertyIDs.Color, colour);
  Graphics.DrawMesh(m, Matrix4x4.TRS(new Vector3(pos.x, layer, pos.z),
    Quaternion.Euler(0, angle, 0), new Vector3(width, 1, depth)), material, 0, null, 0, props);
}

function point(origin, angle, side, distance, height = 0) {
  const a = angle * Mathf.Deg2Rad;
  return { x: origin.x + Math.cos(a) * side + Math.sin(a) * distance,
    z: origin.z - Math.sin(a) * side + Math.cos(a) * distance + height * 0.60 };
}

// A strip between two sampled curves preserves the hollow curl and pointed lip.
function strip(key, a, b, layer, colour) {
  const vertices = [], triangles = [];
  for (let i = 0; i < a.length; i++) {
    vertices.push(a[i].x, a[i].z, b[i].x, b[i].z);
    if (i) { const n = i * 2; triangles.push(n - 2, n, n - 1, n - 1, n, n + 1); }
  }
  const m = mesh(key); m.setFlat(vertices, triangles);
  paint(m, { x: 0, z: 0 }, layer, 1, 1, colour);
}

export default {
  kit: 'Six Paths', label: 'Black Tide (sketch)',
  params: {
    gather: P('Gather six orbs', 0.45, 0.15, 1, 0.05, 'Timing (s)'),
    form: P('Flatten into sheet', 0.35, 0.15, 1, 0.05, 'Timing (s)'),
    surge: P('Surge forward', 1.05, 0.3, 2, 0.05, 'Timing (s)'),
    curl: P('Teeth curl over', 0.30, 0.15, 0.8, 0.05, 'Timing (s)'),
    retract: P('Pull tide back', 0.75, 0.25, 1.5, 0.05, 'Timing (s)'),
    reform: P('Reform into orbs', 0.50, 0.2, 1, 0.05, 'Timing (s)'),
    width: P('Wave width (cells)', 5.4, 3, 8, 0.1, 'Shape'),
    reach: P('Travel distance (cells)', 7, 3, 12, 0.25, 'Shape'),
    height: P('Curl height (cells)', 1.25, 0.4, 2, 0.05, 'Shape'),
    teeth: P('Teeth along lip', 9, 5, 15, 1, 'Shape'),
    tooth: P('Tooth length (cells)', 0.65, 0.2, 1.2, 0.05, 'Shape'),
    angle: P('Aim (degrees)', 35, 0, 360, 5, 'Shape'),
    dust: P('Ground spray', 0.4, 0, 0.8, 0.05, 'Finish'),
    shake: P('Curl camera shake', 0.08, 0, 0.2, 0.01, 'Finish'),
  },
  duration(p) { return timing(p).end; },
  phases(p) {
    const t = timing(p);
    return [{ name: 'Gather', t: 0 }, { name: 'Flatten', t: p.gather },
      { name: 'Surge', t: t.surge }, { name: 'Curl', t: t.curl },
      { name: 'Retract', t: t.retract }, { name: 'Reform', t: t.reform }];
  },
  events(p) { return p.shake ? [{ t: timing(p).curl, type: 'shake', value: p.shake }] : []; },
  draw(s, p, { origin }) {
    if (s < 0) return;
    const t = timing(p);
    const gather = smooth(s / p.gather);
    const form = smooth((s - p.gather) / p.form);
    const surge = smooth((s - t.surge) / p.surge);
    const curl = smooth((s - t.curl) / p.curl);
    const retract = smooth((s - t.retract) / p.retract);
    const reform = smooth((s - t.reform) / p.reform);
    const sheet = form * (1 - reform);
    const fade = 1 - smooth((s - t.end + 0.25) / 0.25);
    const front = 1.15 + p.reach * surge * (1 - retract);

    // Orbs squash into adjoining lobes, then split out of those same lobes on recall.
    for (let i = 0; i < 6; i++) {
      const a = i / 6 * Math.PI * 2;
      const homeSide = Math.cos(a) * 1.15, homeDistance = Math.sin(a) * 0.8;
      const side = lerp(homeSide, (i / 5 - 0.5) * p.width * 0.8, gather * (1 - reform));
      const distance = lerp(homeDistance, 1.15, gather * (1 - reform));
      const pos = point(origin, p.angle, side, distance);
      const alpha = (1 - smooth((sheet - 0.65) / 0.35)) * fade;
      const w = lerp(0.29, p.width / 9, sheet), d = lerp(0.29, 0.10, sheet);
      paint(MeshPool.plane10, pos, ground, w * 3, d * 3, ink.withAlpha(alpha * 0.35), soft, p.angle);
      paint(disc, pos, y + 0.03, w + 0.018, d + 0.018, rim.withAlpha(alpha), solid, p.angle);
      paint(disc, pos, y + 0.034, w, d, ink.withAlpha(alpha), solid, p.angle);
    }
    if (sheet < 0.001 || fade <= 0) return;

    const width = p.width * sheet;
    const raised = p.height * surge * (1 - retract) * (1 - reform);
    const teeth = smooth(surge * 1.5) * (1 - retract);
    const back = [], base = [], crest = [], lip = [], edge = [], shadow = [];
    const count = p.teeth * 8;
    for (let i = 0; i <= count; i++) {
      const u = i / count, k = u * 2 - 1;
      const taper = Math.sqrt(Math.max(0, 1 - k * k));
      const side = k * width / 2;
      const bow = 0.55 * (1 - k * k) * surge;
      const ripple = Math.sin(u * Math.PI * 6 - s * 7) * 0.045 * taper * surge;
      const tip = Math.max(0, 1 - Math.abs((u * p.teeth % 1) * 2 - 1)) ** 2;
      const d = front + bow + ripple;
      const h = raised * taper;
      back.push(point(origin, p.angle, side * 0.70, 0.85 + 0.65 * (1 - taper) * surge));
      base.push(point(origin, p.angle, side, d - 0.3 * surge));
      crest.push(point(origin, p.angle, side, d - 0.3 * surge, h));
      const lipD = d + (0.20 + curl * 0.4) * surge + tip * p.tooth * teeth * taper;
      lip.push(point(origin, p.angle, side, lipD, h * (0.75 - curl * 0.4)));
      edge.push(point(origin, p.angle, side, lipD + 0.028 * sheet, h * (0.75 - curl * 0.4)));
      shadow.push(point(origin, p.angle, side * 1.02, lipD + 0.13, 0));
    }
    strip('contact shadow', back, shadow, ground, ink.withAlpha(0.3 * sheet * fade));
    // Wavy, narrowing sides keep the stretched sheet fluid as the crest runs out.
    for (let row = 0; row < 16; row++) {
      const curves = [row / 16, (row + 1) / 16].map(v => back.map((start, i) => {
        const k = i / count * 2 - 1;
        const flutter = Math.sin(v * 11 - s * 6) * Math.sin(v * Math.PI) * 0.12 * surge;
        const offset = point({ x: 0, z: 0 }, p.angle, flutter * k, 0);
        return { x: lerp(start.x, base[i].x, v) + offset.x,
          z: lerp(start.z, base[i].z, v) + offset.z };
      }));
      strip(`sheet ${row}`, curves[0], curves[1], y, ink.withAlpha(sheet * fade));
    }
    strip('rising face', base, crest, y + 0.004, fold.withAlpha(sheet * fade));
    strip('curl', crest, lip, y + 0.008, ink.withAlpha(sheet * fade));
    strip('cutting rim', lip, edge, y + 0.012, rim.withAlpha(sheet * fade * 0.85));

    // Long, interrupted reflections travel down the sheet towards the rolling lip.
    for (let j = 0; j < 5; j++) {
      const a = [], b = [];
      for (let i = 0; i <= 20; i++) {
        const u = i / 20;
        const side = ((j - 2) / 5 + Math.sin(u * 7 - s * 5 + j) * 0.018) * width;
        const d = lerp(1.12, front - 0.35 * surge, u);
        const w = 0.014 * Math.sin(u * Math.PI) * surge;
        a.push(point(origin, p.angle, side - w, d));
        b.push(point(origin, p.angle, side + w, d));
      }
      strip(`flow ${j}`, a, b, y + 0.002, rim.withAlpha(0.22 * sheet * fade));
    }
    for (let i = 0; i < 12; i++) {
      const age = ((s - t.surge) * 2 + i * 0.381) % 1;
      if (s < t.surge || s >= t.retract || age < 0) continue;
      const side = (i / 11 - 0.5) * width;
      const pos = point(origin, p.angle, side, front + age * 0.5);
      paint(MeshPool.plane10, pos, y + 0.02, 0.25 + age * 0.65, 0.2 + age * 0.4,
        new Color(0.49, 0.44, 0.37, Math.sin(age * Math.PI) * p.dust * surge), puff);
    }
  },
};
