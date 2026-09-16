// Sixfold Execution: a lab proposal, not an in-game ability.
// Six ground-born orbs unfold into blades (0.65 s), cross the target in alternating
// directions (six strikes spaced 0.24 s), recover into a ring, hold (0.30 s), then
// converge (0.18 s). A single bright impact gives way to dust and fading cuts (0.75 s).
// All positions are derived from time: scrubbing and PNG export need no simulation.
// Uses shipped textures and ordinary meshes only; port ground marks with fog/bounds checks.
import {
  AltitudeLayer, Color, Graphics, MaterialPool, MaterialPropertyBlock, Mathf, Matrix4x4,
  Mesh, Meshes, MeshPool, Quaternion, ShaderDatabase, ShaderPropertyIDs, Vector3,
} from '../js/engine.js';

const TAU = Math.PI * 2;
const body = new Color(0.025, 0.020, 0.038);
const rim = new Color(0.64, 0.48, 0.88);
const light = new Color(0.90, 0.84, 1);
const dirt = new Color(0.40, 0.32, 0.24);
const solid = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const glow = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.MoteGlow);
const soft = MaterialPool.MatFrom('RimArt/SixPaths/SoftDisc', ShaderDatabase.Transparent);
const puff = MaterialPool.MatFrom('RimArt/SixPaths/Puff', ShaderDatabase.Transparent);
const props = new MaterialPropertyBlock();
const disc = Meshes.disc(48, 'execution orb');
const ring = Meshes.band(0.97, 1, 96, 'execution shock ring');
const blade = new Mesh('execution blade');
// Asymmetric tip and shoulder give the blade a definite direction of travel (+x).
blade.setFlat([-0.50, 0, -0.28, 0.40, 0.05, 0.30, 0.60, 0, 0.05, -0.30, -0.28, -0.40],
  [0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5]);
const Y = AltitudeLayer.MoteOverhead.AltitudeFor();
const ground = AltitudeLayer.Filth.AltitudeFor();
const shadow = AltitudeLayer.Shadows.AltitudeFor();
const clamp = Mathf.Clamp01;
const smooth = Mathf.Smooth;
const P = (label, value, min, max, step, group) => ({ label, value, min, max, step, group });

function paint(mesh, x, z, y, w, h, angle, color, mat = solid) {
  if (color.a <= 0 || w <= 0 || h <= 0) return;
  props.SetColor(ShaderPropertyIDs.Color, color);
  Graphics.DrawMesh(mesh, Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0, angle, 0),
    new Vector3(w, 1, h)), mat, 0, null, 0, props);
}

function timing(p) {
  const recover = p.form + 5 * p.interval + p.slash + p.recover;
  const converge = recover + p.hold;
  const impact = converge + p.converge;
  return { recover, converge, impact, end: impact + p.fade };
}

// Alternate around the ring so consecutive strikes have distinct crossing directions.
const order = [0, 2, 4, 1, 3, 5];
function pose(s, i, p, t) {
  const start = p.form + i * p.interval;
  const turn = smooth(s / p.form) * p.orbit * Mathf.Deg2Rad;
  const a = order[i] * TAU / 6 + p.angle * Mathf.Deg2Rad + turn;
  let r = p.radius, heading = a + Math.PI;
  if (s >= start && s < start + p.slash) {
    r *= 1 - 2 * smooth((s - start) / p.slash);
  } else if (s >= start + p.slash) {
    // Stay at the opposite side, turn inward as the blade recovers.
    r = -p.radius * (1 + 0.12 * Math.sin(Math.PI * clamp((s - start - p.slash) / p.recover)));
    heading += Math.PI * smooth((s - start - p.slash) / p.recover);
  }
  if (s >= t.converge) r *= 1 - Math.pow(clamp((s - t.converge) / p.converge), 2);
  return { x: Math.cos(a) * r, z: Math.sin(a) * r, angle: -heading * Mathf.Rad2Deg };
}

export default {
  kit: 'Six Paths',
  label: 'Sixfold Execution (sketch)',
  params: {
    form: P('Orbs unfold', 0.65, 0.2, 1.5, 0.05, 'Timing (s)'),
    interval: P('Time between strikes', 0.24, 0.12, 0.6, 0.01, 'Timing (s)'),
    slash: P('Blade crosses target', 0.18, 0.08, 0.4, 0.01, 'Timing (s)'),
    recover: P('Blade recovers', 0.22, 0.08, 0.6, 0.01, 'Timing (s)'),
    hold: P('Pause before final strike', 0.30, 0, 1, 0.05, 'Timing (s)'),
    converge: P('Final convergence', 0.18, 0.08, 0.5, 0.01, 'Timing (s)'),
    fade: P('Aftermath fades', 0.75, 0.3, 1.5, 0.05, 'Timing (s)'),
    radius: P('Orbit radius (cells)', 2.8, 1.5, 5, 0.1, 'Formation'),
    angle: P('Formation angle (degrees)', 15, 0, 360, 5, 'Formation'),
    orbit: P('Gather rotation (degrees)', 35, 0, 120, 5, 'Formation'),
    length: P('Blade length (cells)', 1.55, 0.7, 2.8, 0.05, 'Blades'),
    width: P('Blade width (cells)', 0.42, 0.15, 0.8, 0.01, 'Blades'),
    rim: P('Rim width (cells)', 0.025, 0.01, 0.08, 0.005, 'Blades'),
    trails: P('Trail strength', 0.32, 0, 0.7, 0.01, 'Blades'),
    flash: P('Final flash strength', 0.85, 0, 1, 0.05, 'Impact'),
    dust: P('Dust strength', 0.45, 0, 1, 0.05, 'Impact'),
    shake: P('Final camera shake', 0.15, 0, 0.2, 0.01, 'Impact'),
  },
  duration(p) { return timing(p).end; },
  phases(p) {
    const t = timing(p);
    return [{ name: 'Unfold', t: 0 },
      ...order.map((_, i) => ({ name: `Strike ${i + 1}`, t: p.form + i * p.interval })),
      { name: 'Poise', t: t.recover }, { name: 'Converge', t: t.converge },
      { name: 'Execution', t: t.impact }];
  },
  events(p) {
    if (!p.shake) return [];
    return [...order.map((_, i) => ({ t: p.form + i * p.interval + p.slash / 2,
      type: 'shake', value: p.shake * 0.16 })),
    { t: timing(p).impact, type: 'shake', value: p.shake }];
  },
  draw(s, p, { origin: o }) {
    const t = timing(p);
    if (s < 0 || s >= t.end) return;
    const appear = smooth(s / (p.form * 0.3));
    const unfold = smooth((s / p.form - 0.25) / 0.65);
    const aftermath = clamp((s - t.impact) / p.fade);
    const fade = 1 - smooth(aftermath);

    // Ground contact and a quiet target halo anchor the pattern at map level.
    paint(disc, o.x, o.z, ground, p.radius * 0.43, p.radius * 0.43, 0,
      body.withAlpha(0.17 * appear * fade));
    paint(ring, o.x, o.z, ground + 0.002, p.radius, p.radius, 0,
      rim.withAlpha(0.16 * appear * fade));

    for (let i = 0; i < 6; i++) {
      const hit = p.form + i * p.interval + p.slash / 2;
      const age = s - hit;
      const a = (order[i] * 60 + p.angle + p.orbit) * Mathf.Deg2Rad;
      if (age >= 0) {
        const cut = smooth(age / 0.07) * fade;
        paint(blade, o.x, o.z, ground + 0.004 + i * 0.001,
          p.radius * 1.7, 0.055, -a * Mathf.Rad2Deg, body.withAlpha(0.42 * cut));
        if (age < 0.18) {
          const f = 1 - age / 0.18;
          paint(MeshPool.plane10, o.x, o.z, Y + 0.16, p.radius * 1.5, 0.16,
            -a * Mathf.Rad2Deg, light.withAlpha(f * 0.75), glow);
          paint(MeshPool.plane10, o.x, o.z, Y + 0.17, 0.8, 0.8, 0,
            light.withAlpha(f * 0.5), glow);
        }
      }
      if (s >= t.impact) continue;
      const q = pose(s, i, p, t);
      const w = Mathf.Lerp(0.42, p.length, unfold);
      const h = Mathf.Lerp(0.42, p.width, unfold);
      // The orb contracts as its blade grows out, preserving material continuity.
      if (unfold < 1) {
        paint(disc, o.x + q.x, o.z + q.z, Y + 0.06, 0.23, 0.23, 0,
          rim.withAlpha(appear * (1 - unfold)));
        paint(disc, o.x + q.x, o.z + q.z, Y + 0.061, 0.20, 0.20, 0,
          body.withAlpha(appear * (1 - unfold)));
      }
      const alpha = appear * unfold;
      paint(MeshPool.plane10, o.x + q.x, o.z + q.z, shadow, w * 1.15, h * 1.5,
        q.angle, body.withAlpha(alpha * 0.35), soft);
      const start = p.form + i * p.interval;
      const moving = (s >= start && s < start + p.slash) || s >= t.converge;
      if (moving) {
        for (let j = 5; j >= 1; j--) {
          const old = pose(Math.max(start, s - j * 0.012), i, p, t);
          paint(blade, o.x + old.x, o.z + old.z, Y + 0.01,
            w, h, old.angle, rim.withAlpha(p.trails * (1 - j / 6) * alpha));
        }
      }
      paint(blade, o.x + q.x, o.z + q.z, Y + 0.08, w + p.rim * 2, h + p.rim * 2,
        q.angle, rim.withAlpha(alpha));
      paint(blade, o.x + q.x, o.z + q.z, Y + 0.081, w, h, q.angle, body.withAlpha(alpha));
    }

    if (s < t.impact) return;
    const age = s - t.impact;
    const flash = Math.pow(1 - clamp(age / 0.20), 2) * p.flash;
    const wave = 0.25 + p.radius * 1.25 * Math.sqrt(aftermath);
    paint(ring, o.x, o.z, ground + 0.02, wave, wave, 0, rim.withAlpha(fade * 0.55));
    for (let i = 0; i < 6; i++) {
      const a = (order[i] * 60 + p.angle + p.orbit) * Mathf.Deg2Rad;
      const r = 0.3 + p.radius * 0.7 * Math.sqrt(aftermath);
      const size = 0.4 + aftermath * 1.5;
      paint(MeshPool.plane10, o.x + Math.cos(a) * r, o.z + Math.sin(a) * r,
        Y + 0.10, size, size * 0.7, i * 47, dirt.withAlpha(p.dust * fade), puff);
      paint(blade, o.x + Math.cos(a) * r, o.z + Math.sin(a) * r, Y + 0.12,
        0.22 * fade, 0.12 * fade, i * 73 + age * 150, body.withAlpha(fade));
    }
    paint(MeshPool.plane10, o.x, o.z, Y + 0.18, 3.0, 3.0, 0, light.withAlpha(flash), glow);
    paint(MeshPool.plane10, o.x, o.z, Y + 0.19, p.radius * 2.5, 0.18, 15,
      light.withAlpha(flash), glow);
  },
};
