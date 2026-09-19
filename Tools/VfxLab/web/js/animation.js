// Animation clips: the json Melee Animation plays (Animations/*.json here, and that mod's own).
//
// A clip is wrapped as a sketch-shaped module -- kit, label, params, duration, phases, events,
// draw -- so the player, timeline, export and shoot.mjs treat it like any sketch. A sketch can also
// draw a clip as its caster with playClip(), so the clip and the sketch's effect share one clock.
//
// The drawing follows Melee Animation's renderer, read from its decompiled zAnimationMod.dll:
//   AnimPartSnapshot   local = TRS(position, Euler(rotation), scale); world = parent.world * local;
//                      a mirrored clip is Scale(-1,1,1) * world * Scale(-1,1,1)
//   AnimRenderer.Draw  one 1 x 1 quad per textured part, drawn with RootTransform * world, so a
//                      part's scale is its sprite size in cells and its world y is its draw depth
//   ConfigureHands     parts named HandA / HandB get Textures/AM/Hand.png tinted with skin colour
//   ItemTweakData      a pawn's melee weapon goes on ItemA / ItemB: the weapon's texture on the
//                      part's quad, placed by WeaponTweakData/<def>_<packageId>.json as one more
//                      transform inside the part -- TRS((OffX, OffY), Rotation, (ScaleX, ScaleY)),
//                      offset and angle negated by the flips; HandsMode hides the off hand or both
//   DrawPawns          the pawn itself is drawn by RimWorld at BodyX's world position and angle,
//                      facing PawnBody.Direction (East becomes West when mirrored)
// and RimArt.MeleeAnimation.ThrowAimWorker, which turns every part under PawnALift about that
// part by the angle between the clip's direction and the target (ThrowAnimation.Aim).
//
// What is a stand-in: the pawn (body, head, a mark for which way it faces), and the melee weapon
// when none is chosen. Weapons come from lab.py's list: ours, and installed mods' that have tweak
// data and a loose PNG; vanilla weapon art is inside asset bundles. Only rotation about y is drawn, which
// is all these clips animate; a rotated part under an unevenly scaled parent would shear and is
// drawn without the shear. The lab engine has no matrices, so the 2D affine maths is in this file.

import {
  AltitudeLayer, Color, Graphics, MaterialPool, MaterialPropertyBlock, Mathf, Matrix4x4, Mesh, Meshes,
  MeshPool, Quaternion, ShaderDatabase, ShaderPropertyIDs, Vector3,
} from './engine.js';

const pawnLayer = AltitudeLayer.Pawn.AltitudeFor(), overlay = AltitudeLayer.MoteOverhead.AltitudeFor();
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor();
const white = MaterialPool.MatFrom('white', ShaderDatabase.Transparent);
const soft = MaterialPool.MatFrom('lab/soft-disc', ShaderDatabase.Transparent);
const disc = Meshes.disc(32, 'clip disc');
const props = new MaterialPropertyBlock();
const Skin = new Color(.83, .70, .54), Hair = new Color(.22, .16, .12), Steel = new Color(.62, .64, .68);
const Shirts = [new Color(.39, .58, .65), new Color(.62, .45, .22), new Color(.45, .55, .35)];
const PathColour = new Color(1, .86, .35), ReleaseColour = new Color(1, .45, .3), PivotColour = new Color(.4, 1, .9);
const AimPivot = 'PawnALift';                  // ThrowAimWorker.PivotPartName
const PathSteps = 48;

// AnimData.GetMesh(flipX, flipY): the same quad with its texture coordinates swapped.
const quads = [0, 1, 2, 3].map((i) => {
  const m = new Mesh(`clip quad ${i}`), fx = i & 1, fy = i & 2;
  m.setFlat([-.5, -.5, -.5, .5, .5, .5, .5, -.5], [0, 1, 2, 0, 2, 3]);
  m.uv = new Float32Array([0, 0, 0, 1, 1, 1, 1, 0].map((v, k) => ((k % 2 ? fy : fx) ? 1 - v : v)));
  return m;
});

function draw(mesh, x, y, z, sx, sz, rot, colour, material = white) {
  props.SetColor(ShaderPropertyIDs.Color, colour);
  Graphics.DrawMesh(mesh, Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0, rot, 0), new Vector3(sx, 1, sz)), material, 0, null, 0, props);
}

// ------------------------------------------------------------------ curves

/** Unity's AnimationCurve.Evaluate for unweighted keys: cubic Hermite, clamped at both ends. */
function evaluate(curve, t, fallback) {
  const keys = curve?.Keyframes;
  if (!keys?.length) return fallback;
  // The exporter leaves out a time or a value that is zero.
  const time = (k) => k.time ?? 0, val = (k) => k.value ?? 0;
  if (t <= time(keys[0])) return val(keys[0]);
  const last = keys[keys.length - 1];
  if (t >= time(last)) return val(last);
  let i = 1;
  while (time(keys[i]) < t) i++;
  const a = keys[i - 1], b = keys[i], dt = time(b) - time(a), u = (t - time(a)) / dt;
  const m0 = (a.outTangent ?? 0) * dt, m1 = (b.inTangent ?? 0) * dt;
  if (!Number.isFinite(m0) || !Number.isFinite(m1)) return val(a);          // a stepped key
  const u2 = u * u, u3 = u2 * u;
  return (2 * u3 - 3 * u2 + 1) * val(a) + (u3 - 2 * u2 + u) * m0 + (-2 * u3 + 3 * u2) * val(b) + (u3 - u2) * m1;
}

const value = (part, name, t, fallback = 0) =>
  evaluate(part.Curves[name], t, part.DefaultValues?.[name] ?? fallback);

// ------------------------------------------------------------------ 2D affine, on the map plane

// (px, pz) -> (a px + c pz + x, b px + d pz + z). Euler y turns clockwise seen from above, as in Unity.
const trs = (x, z, degrees, sx, sz) => {
  const r = degrees * Mathf.Deg2Rad, cos = Math.cos(r), sin = Math.sin(r);
  return { a: sx * cos, b: -sx * sin, c: sz * sin, d: sz * cos, x, z };
};
const mul = (p, l) => ({
  a: p.a * l.a + p.c * l.b, b: p.b * l.a + p.d * l.b, c: p.a * l.c + p.c * l.d, d: p.b * l.c + p.d * l.d,
  x: p.a * l.x + p.c * l.z + p.x, z: p.b * l.x + p.d * l.z + p.z,
});
const mirrored = (m) => ({ a: m.a, b: -m.b, c: -m.c, d: m.d, x: -m.x, z: m.z });
const turnedAbout = (m, centre, degrees) => {
  const turn = trs(0, 0, -degrees, 1, 1), local = { ...m, x: m.x - centre.x, z: m.z - centre.z }, out = mul(turn, local);
  return { ...out, x: out.x + centre.x, z: out.z + centre.z };
};
/** Position, clockwise angle and sizes for DrawMesh. A negative depth means the quad is flipped. */
const decompose = (m) => {
  const r = Math.atan2(-m.b, m.a), sx = Math.hypot(m.a, m.b), sz = m.c * Math.sin(r) + m.d * Math.cos(r);
  return { rot: r * Mathf.Rad2Deg, sx, sz };
};

// ------------------------------------------------------------------ a clip at a time

function prepare(json) {
  const byId = new Map(json.Parts.map((p) => [p.ID, p]));
  for (const part of json.Parts) {
    part.name = part.Path.split('/').pop();
    part.parent = byId.get(part.ParentID ?? 0) ?? null;
  }
  // Parents before children, so one pass fills the world matrices.
  const depth = (p) => (p.parent ? depth(p.parent) + 1 : 0);
  json.ordered = [...json.Parts].sort((x, y) => depth(x) - depth(y));
  const under = (p, name) => { for (let q = p.parent; q; q = q.parent) if (q.name === name) return true; return false; };
  for (const part of json.Parts) part.aimed = under(part, AimPivot);
  json.aimPivot = json.Parts.find((p) => p.name === AimPivot) ?? null;
  // The release: the first time a textured part that started active is switched off.
  let release = null;
  for (const part of json.Parts) {
    const keys = part.Curves['GameObject.m_IsActive']?.Keyframes;
    if (!keys || !part.TexturePath || (keys[0].value ?? 0) < .5) continue;
    const off = keys.find((k) => (k.value ?? 0) < .5);
    if (off && (release === null || (off.time ?? 0) < release)) release = off.time ?? 0;
  }
  json.release = release;
  return json;
}

/** World state of every part at `t`: { m, y, active, flipX, flipY, tint, direction }. */
function pose(clip, t, mirror, aimDegrees) {
  const out = new Map();
  for (const part of clip.ordered) {
    const local = trs(value(part, 'Transform.m_LocalPosition.x', t), value(part, 'Transform.m_LocalPosition.z', t),
      value(part, 'Transform.localEulerAnglesRaw.y', t),
      value(part, 'Transform.m_LocalScale.x', t, 1), value(part, 'Transform.m_LocalScale.z', t, 1));
    const up = part.parent ? out.get(part.parent) : null;
    out.set(part, {
      raw: up ? mul(up.raw, local) : local,
      y: (up?.y ?? 0) + value(part, 'Transform.m_LocalPosition.y', t),
      active: (up?.active ?? true) && value(part, 'GameObject.m_IsActive', t, 1) >= .5,
      flipX: value(part, 'AnimatedPart.FlipX', t) >= .5, flipY: value(part, 'AnimatedPart.FlipY', t) >= .5,
      tint: new Color(value(part, 'AnimatedPart.Tint.r', t, 1), value(part, 'AnimatedPart.Tint.g', t, 1),
        value(part, 'AnimatedPart.Tint.b', t, 1), value(part, 'AnimatedPart.Tint.a', t, 1)),
      direction: Math.round(value(part, 'PawnBody.Direction', t)),
    });
  }
  for (const state of out.values()) state.m = mirror ? mirrored(state.raw) : state.raw;
  if (aimDegrees && clip.aimPivot) {
    const centre = out.get(clip.aimPivot).m;
    for (const [part, state] of out) if (part.aimed) state.m = turnedAbout(state.m, centre, aimDegrees);
  }
  return out;
}

/** Draws every part of a posed clip at `o`. `look` is { hand, material(path, transparent, asIs), weapon, tweak, shirts }. */
function drawParts(clip, parts, o, scene, mirror, look) {
  const sun = scene?.shadowVector ?? { x: -.45, z: -.32 }, strength = scene?.sun?.strength ?? .32;
  const { weapon = null, tweak = null } = look;
  let pawns = 0;
  for (const part of clip.ordered) {
    const s = parts.get(part);
    if (!s.active || s.tint.a <= 0) continue;
    // HandsMode: 1 hides the off hand (HandB), 2 hides both.
    if (tweak?.HandsMode && /^Hand[A-Z]\d*$/.test(part.name) && (tweak.HandsMode === 2 || part.name[4] !== 'A')) continue;
    if (weapon && /^Item[A-Z]$/.test(part.name)) {
      const fx = s.flipX !== tweak.FlipX, fy = s.flipY !== tweak.FlipY;
      const inner = trs(fx ? -tweak.OffX : tweak.OffX, fy ? -tweak.OffY : tweak.OffY, fx !== fy ? -tweak.Rotation : tweak.Rotation, tweak.ScaleX, tweak.ScaleY);
      const placed = mul(s.raw, inner), m = mirror ? mirrored(placed) : placed, d = decompose(m);
      draw(quads[(fx !== mirror ? 1 : 0) + ((fy !== d.sz < 0) ? 2 : 0)], o.x + m.x, pawnLayer + s.y, o.z + m.z, d.sx, Math.abs(d.sz), d.rot, s.tint, look.material(weapon.texture, false, true));
      continue;
    }
    const { rot, sx, sz } = decompose(s.m), x = o.x + s.m.x, z = o.z + s.m.z, y = pawnLayer + s.y;

    if (/^Body[A-Z]$/.test(part.name)) {
      // Stand-in pawn, sized after a RimWorld human: the position is the middle of the sprite.
      const west = s.direction === 3 || (s.direction === 1 && mirror), east = s.direction === 1 && !mirror;
      const local = (lx, lz) => { const q = mul(trs(s.m.x, s.m.z, rot, 1, 1), { a: 1, b: 0, c: 0, d: 1, x: lx, z: lz }); return { x: o.x + q.x, z: o.z + q.z }; };
      const shirt = (look.shirts ?? Shirts)[pawns++ % (look.shirts ?? Shirts).length], side = east ? 1 : west ? -1 : 0;
      draw(MeshPool.plane10, x + sun.x * .4, shadowLayer, z - .3 + sun.z * .4, .9, .4, 0, new Color(.03, .03, .05, strength), soft);
      const body = local(0, -.1), head = local(side * .03, .3);
      draw(disc, body.x, y, body.z, .23, .27, rot, shirt);
      draw(disc, head.x, y + .003, head.z, .17, .17, 0, s.direction === 0 ? Hair : Skin);
      if (side) { const eye = local(side * .12, .31); draw(disc, eye.x, y + .004, eye.z, .025, .03, 0, Hair); }
      else if (s.direction === 2) for (const e of [-1, 1]) { const eye = local(e * .065, .29); draw(disc, eye.x, y + .004, eye.z, .025, .03, 0, Hair); }
      continue;
    }

    const flipX = s.flipX !== mirror, quad = quads[(flipX ? 1 : 0) + ((s.flipY !== sz < 0) ? 2 : 0)];
    if (/^Hand[A-Z]\d*$/.test(part.name)) draw(quad, x, y, z, sx, Math.abs(sz), rot, new Color(Skin.r * s.tint.r, Skin.g * s.tint.g, Skin.b * s.tint.b, s.tint.a), look.hand);
    else if (part.TexturePath) draw(quad, x, y, z, sx, Math.abs(sz), rot, s.tint, look.material(part.TexturePath, part.TransparentByDefault || s.tint.a < 1));
    else if (/^Item[A-Z]$/.test(part.name)) {
      // Stand-in melee weapon along the part's +x (its -x when the part is flipped): grip, guard, blade.
      const along = (lx, w, h, colour, lift) => { const q = mul(s.m, { a: 1, b: 0, c: 0, d: 1, x: s.flipX ? -lx : lx, z: 0 }); draw(MeshPool.plane10, o.x + q.x, y + lift, o.z + q.z, w * sx, h * Math.abs(sz), rot, colour); };
      along(.2, .75, .06, Steel, 0); along(-.17, .04, .2, Hair, .001); along(-.27, .2, .045, Hair, .001);
    }
  }
}

// ------------------------------------------------------------------ loading

const cache = new Map();
let onLoaded = () => {};
/** ui.js sets this so the phase list can be rebuilt once a clip's release time is known. */
export function whenClipLoads(callback) { onLoaded = callback; }
/** Called when lab.py reports changed clips; the next frame fetches them again. */
export function forgetClips() { cache.clear(); }

function clipAt(url) {
  let entry = cache.get(url);
  if (entry) return entry.json;
  entry = { json: null };
  cache.set(url, entry);
  fetch(url, { cache: 'no-store' }).then((r) => (r.ok ? r.text() : Promise.reject(new Error(`${url}: ${r.status}`))))
    // Unity's exporter writes a BOM and bare Infinity for stepped tangents; JSON.parse takes neither.
    .then((text) => { entry.json = prepare(JSON.parse(text.replace(/^﻿/, '').replace(/(-?)Infinity/g, '$11e999'))); onLoaded(); })
    .catch((error) => console.warn('LAB clip failed', error));
  return null;
}

// ------------------------------------------------------------------ for sketches

let listed = null;
const looks = new Map();
function setNamed(name) {
  if (!listed) {
    listed = { sets: null };
    fetch('../recordings/animations.json', { cache: 'no-store' }).then((r) => r.json())
      .then((index) => { listed.sets = index.sets; }).catch((error) => console.warn('LAB clip list failed', error));
  }
  return listed.sets?.find((set) => set.name === name) ?? null;
}

/**
 * Draws clip `name` (the json file's name, "RimArt_ThrowKunai") at `position` for a sketch, in place
 * of a two-disc stand-in caster, and says where the hand and the thrown item are so the sketch can
 * start its own effect from them on the same clock.
 *
 *   options.aim     degrees to the target, 0 east, 90 north. Picks the facing clip and the mirror as
 *                   ThrowAnimation.Aim does and turns the hand as ThrowAimWorker does. Without it
 *                   the East clip plays, mirrored when options.mirror is set.
 *   options.turn    false leaves the hand on the clip's own direction
 *   options.shirt   a Color for the stand-in body
 *   options.scene   the sketch's ctx.scene, for the shadow
 *
 * Returns null until the clip has loaded (nothing is drawn), then
 *   { length, release, facing, mirror, hand: { x, z }, item: { x, z, rot, held } | null, itemAtRelease }
 * `seconds` is clamped to the clip, so a sketch can hold the first or last pose as long as it likes.
 */
export function playClip(name, seconds, position, options = {}) {
  const set = setNamed(name);
  if (!set) return null;
  const facings = Object.keys(set.clips).length > 1;
  const aim = facings && options.aim != null ? aimFor(options.aim) : { facing: 'East', mirror: !!options.mirror, offset: 0 };
  const clip = clipAt(set.clips[aim.facing]);
  if (!clip) return null;
  const offset = options.turn === false ? 0 : aim.offset, t = Math.max(0, Math.min(clip.Length, seconds));
  if (!looks.has(set.id)) {
    const materials = new Map(), prefix = set.source === 'rimart' ? '' : 'am:';
    looks.set(set.id, { hand: MaterialPool.MatFrom('am:AM/Hand', ShaderDatabase.Cutout), material: (path, transparent, asIs) => {
      const key = `${path}|${transparent}`;
      if (!materials.has(key)) materials.set(key, MaterialPool.MatFrom((asIs ? '' : prefix) + path, transparent ? ShaderDatabase.Transparent : ShaderDatabase.Cutout));
      return materials.get(key);
    } });
  }
  const parts = pose(clip, t, aim.mirror, offset);
  drawParts(clip, parts, position, options.scene, aim.mirror, { ...looks.get(set.id), shirts: options.shirt ? [options.shirt] : null });

  const handPart = clip.ordered.find((q) => q.name === 'HandA'), itemPart = clip.ordered.find((q) => q.TexturePath && q.aimed) ?? null;
  const where = (state) => ({ x: position.x + state.m.x, z: position.z + state.m.z, rot: decompose(state.m).rot, held: state.active });
  return {
    length: clip.Length, release: clip.release, facing: aim.facing, mirror: aim.mirror,
    hand: handPart ? where(parts.get(handPart)) : null,
    item: itemPart ? where(parts.get(itemPart)) : null,
    itemAtRelease: itemPart && clip.release != null ? where(pose(clip, Math.max(0, clip.release - 1 / 60), aim.mirror, offset).get(itemPart)) : null,
  };
}

// ------------------------------------------------------------------ the module

/** ThrowAnimation.Aim with a continuous angle: which clip, mirrored or not, and the turn left over. */
function aimFor(degrees) {
  const r = degrees * Mathf.Deg2Rad, dx = Math.cos(r), dz = Math.sin(r);
  const sideways = Math.abs(dx) >= Math.abs(dz) - 1e-9;
  const facing = sideways ? 'East' : dz < 0 ? 'South' : 'North', mirror = sideways && dx < 0;
  let offset = degrees - (facing === 'North' ? 90 : facing === 'South' ? -90 : mirror ? 180 : 0);
  while (offset > 180) offset -= 360;
  while (offset <= -180) offset += 360;
  return { facing, mirror, offset };
}

const P = (label, v, min, max, step, group) => ({ label, value: v, min, max, step, group });

const Stub = 'Stand-in sword';
const NoTweak = { OffX: 0, OffY: 0, Rotation: 0, ScaleX: 1, ScaleY: 1, FlipX: false, FlipY: false, HandsMode: 0 };

export function clipModule(set, handTexture, weapons = []) {
  const facings = Object.keys(set.clips).length > 1, ours = set.source === 'rimart';
  const hand = MaterialPool.MatFrom(handTexture, ShaderDatabase.Cutout);
  const materials = new Map();
  // A clip's own textures live with the clip's mod; a weapon's path already says where it lives.
  const materialFor = (path, transparent, asIs = false) => {
    const key = `${path}|${transparent}`;
    if (!materials.has(key)) materials.set(key, MaterialPool.MatFrom((ours || asIs ? '' : 'am:') + path, transparent ? ShaderDatabase.Transparent : ShaderDatabase.Cutout));
    return materials.get(key);
  };
  const pathMesh = new Mesh(`clip path ${set.id}`);

  const weaponFor = (p) => weapons.find((w) => w.label === p.weapon) ?? null;
  // The file's values, or the sliders when placement is being edited. Missing fields are Melee
  // Animation's defaults: no offset, no turn, scale 1, both hands.
  const tweakFor = (p) => {
    const weapon = weaponFor(p), file = { ...NoTweak, ...(weapon?.tweak ?? {}) };
    return p.edit ? { ...file, OffX: p.offX, OffY: p.offY, Rotation: p.rotation, ScaleX: p.scale, ScaleY: p.scale, FlipX: p.flipX, FlipY: p.flipY } : file;
  };

  const choose = (p) => {
    const aim = facings ? aimFor(p.aim) : { facing: 'East', mirror: p.mirror, offset: 0 };
    return { ...aim, clip: clipAt(set.clips[aim.facing]), offset: facings && p.turn ? aim.offset : 0 };
  };

  return {
    kit: set.kit, label: `${set.name.replace(/^RimArt_/, '')} (clip)`, tag: 'clip',
    note: `Melee Animation clip ${set.name}.json, ${ours ? 'written by the make_*_anim.py scripts; edit those, not this page' : 'read from the installed mod'}. The pawn and any melee weapon are stand-ins.`,
    params: {
      ...(facings
        ? { aim: P('Target direction (degrees, 0 east, 90 north)', 0, 0, 355, 5, 'Playback'),
          turn: { label: 'Turn the throwing hand to the target (ThrowAimWorker)', value: true, group: 'Playback' } }
        : { mirror: { label: 'Mirrored (facing west)', value: false, group: 'Playback' } }),
      ...(set.items ? {
        weapon: { label: 'Melee weapon on ItemA / ItemB', value: Stub, options: [Stub, ...weapons.map((w) => w.label)], group: 'Weapon' },
        edit: { label: 'Place it with the sliders below, not its tweak file', value: false, group: 'Weapon' },
        offX: P('OffX (cells, along the part)', 0, -1, 1, .005, 'Weapon'),
        offY: P('OffY (cells, across the part)', 0, -1, 1, .005, 'Weapon'),
        rotation: P('Rotation (degrees)', 0, -180, 180, 1, 'Weapon'),
        scale: P('ScaleX and ScaleY', 1, .3, 3, .05, 'Weapon'),
        flipX: { label: 'FlipX', value: false, group: 'Weapon' },
        flipY: { label: 'FlipY', value: false, group: 'Weapon' },
      } : {}),
      path: { label: 'Show the path of the main hand', value: true, group: 'Overlays' },
      pivots: { label: 'Show part pivots', value: false, group: 'Overlays' },
    },
    // The json Melee Animation reads, for WeaponTweakData/<def>_<packageId>.json in the weapon's mod.
    ...(set.items ? { extraCopy: { label: 'Copy tweak JSON', build(p) {
      const weapon = weaponFor(p), t = tweakFor(p);
      if (!weapon) return 'Choose a weapon first: the stand-in sword has no tweak file.';
      const out = { TextureModID: weapon.package, ItemDefName: weapon.def, ItemType: 'ThingDef', ItemTypeNamespace: 'Verse', ...(weapon.tweak ?? {}),
        OffX: t.OffX, OffY: t.OffY, Rotation: t.Rotation, ScaleX: t.ScaleX, ScaleY: t.ScaleY, FlipX: t.FlipX, FlipY: t.FlipY };
      return `${weapon.tweakFile ? `// save as ${weapon.tweakFile}\n` : ''}${JSON.stringify(out, null, 2)}`;
    } } } : {}),
    duration(p) { return choose(p).clip?.Length ?? set.length; },
    phases(p) {
      const clip = choose(p).clip;
      return [{ name: 'Clip', t: 0 }, ...(clip?.release != null ? [{ name: 'Release', t: clip.release }] : [])];
    },
    events(p) {
      return (choose(p).clip?.Events ?? []).map((e) => ({ t: e.Time, type: 'sound', def: String(e.Data).split(';')[0] }));
    },

    draw(seconds, p, { origin: o, scene }) {
      const { clip, mirror, offset } = choose(p);
      if (!clip) return;
      const parts = pose(clip, seconds, mirror, offset);
      drawParts(clip, parts, o, scene, mirror, { hand, material: materialFor, weapon: set.items ? weaponFor(p) : null, tweak: set.items && weaponFor(p) ? tweakFor(p) : null });

      if (p.pivots) for (const part of clip.ordered) {
        const s = parts.get(part);
        draw(disc, o.x + s.m.x, overlay + .02, o.z + s.m.z, .03, .03, 0, PivotColour.withAlpha(s.active ? .9 : .3));
      }

      if (p.path) {
        const handPart = clip.ordered.find((q) => q.name === 'HandA');
        if (!handPart) return;
        const points = [];
        for (let i = 0; i <= PathSteps; i++) { const m = pose(clip, clip.Length * i / PathSteps, mirror, offset).get(handPart).m; points.push({ x: o.x + m.x, z: o.z + m.z }); }
        const xz = [], tri = [], half = .012;
        points.forEach((q, i) => {
          const a = points[Math.max(0, i - 1)], b = points[Math.min(points.length - 1, i + 1)];
          const len = Math.hypot(b.x - a.x, b.z - a.z) || 1, nx = -(b.z - a.z) / len * half, nz = (b.x - a.x) / len * half;
          xz.push(q.x + nx, q.z + nz, q.x - nx, q.z - nz);
          if (i) { const v = (i - 1) * 2; tri.push(v, v + 2, v + 1, v + 1, v + 2, v + 3); }
        });
        pathMesh.setFlat(xz, tri);
        draw(pathMesh, 0, overlay, 0, 1, 1, 0, PathColour.withAlpha(.8));
        const now = parts.get(handPart).m;
        draw(disc, o.x + now.x, overlay + .002, o.z + now.z, .035, .035, 0, PathColour);
        if (clip.release != null) { const m = pose(clip, clip.release, mirror, offset).get(handPart).m; draw(disc, o.x + m.x, overlay + .001, o.z + m.z, .05, .05, 0, ReleaseColour); }
      }
    },
  };
}
