// The drawing surface sketches are written against. Names and argument order follow the C# a
// kit uses in game -- Graphics.DrawMesh(mesh, Matrix4x4.TRS(pos, Quaternion.Euler(0, a, 0),
// new Vector3(w, 1, d)), material, 0, null, 0, props) -- so a sketch that looks right ports to
// C# line by line. JavaScript has no operator overloading, so vector arithmetic is methods:
// a + b is a.plus(b), a * f is a.times(f).
//
// A frame drawn here has exactly the shape of a recorded frame (see player.js), which is what
// lets a sketch and a recording be compared side by side on one renderer.

export const LayerSpacing = 0.36585367; // Verse.Altitudes.LayerSpacing, RimWorld 1.6

export class Vector2 {
  constructor(x = 0, y = 0) { this.x = x; this.y = y; }
  plus(o) { return new Vector2(this.x + o.x, this.y + o.y); }
  minus(o) { return new Vector2(this.x - o.x, this.y - o.y); }
  times(f) { return new Vector2(this.x * f, this.y * f); }
  get magnitude() { return Math.hypot(this.x, this.y); }
  get normalized() { const m = this.magnitude; return m > 1e-5 ? this.times(1 / m) : new Vector2(); }
}

export class Vector3 {
  constructor(x = 0, y = 0, z = 0) { this.x = x; this.y = y; this.z = z; }
  plus(o) { return new Vector3(this.x + o.x, this.y + o.y, this.z + o.z); }
  minus(o) { return new Vector3(this.x - o.x, this.y - o.y, this.z - o.z); }
  times(f) { return new Vector3(this.x * f, this.y * f, this.z * f); }
  /** Verse's Vector3.WithY: the same point at another altitude. */
  WithY(y) { return new Vector3(this.x, y, this.z); }
  get magnitude() { return Math.hypot(this.x, this.y, this.z); }
  get normalized() { const m = this.magnitude; return m > 1e-5 ? this.times(1 / m) : new Vector3(); }
  static Dot(a, b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
  static get zero() { return new Vector3(); }
}

export class Color {
  constructor(r, g, b, a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
  withAlpha(a) { return new Color(this.r, this.g, this.b, a); }
  static Lerp(a, b, t) {
    t = Mathf.Clamp01(t);
    return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
  }
  static get white() { return new Color(1, 1, 1, 1); }
  static get clear() { return new Color(0, 0, 0, 0); }
}

export const Mathf = {
  PI: Math.PI,
  Deg2Rad: Math.PI / 180,
  Rad2Deg: 180 / Math.PI,
  Sin: Math.sin, Cos: Math.cos, Sqrt: Math.sqrt, Abs: Math.abs, Pow: Math.pow,
  Min: Math.min, Max: Math.max,
  Clamp: (v, lo, hi) => (v < lo ? lo : v > hi ? hi : v),
  Clamp01: (v) => (v < 0 ? 0 : v > 1 ? 1 : v),
  Lerp: (a, b, t) => a + (b - a) * (t < 0 ? 0 : t > 1 ? 1 : t),
  Repeat: (t, l) => t - Math.floor(t / l) * l,
  FloorToInt: Math.floor,
  /** Hermite 0..1, the Smooth every RimArt timing class defines. */
  Smooth: (t) => { t = t < 0 ? 0 : t > 1 ? 1 : t; return t * t * (3 - 2 * t); },
};

// Only the Y angle is used on the map (the top-down camera). A sketch with a 3D camera (see camera() in
// SKETCHING.md) uses all three, as Unity does: z degrees about z, then x about x, then y about y.
export const Quaternion = {
  Euler: (x, y, z) => ({ eulerX: x, eulerY: y, eulerZ: z }),
  identity: { eulerX: 0, eulerY: 0, eulerZ: 0 },
};

export const Matrix4x4 = {
  TRS: (pos, q, scale) => ({ pos, rot: q.eulerY, rotX: q.eulerX ?? 0, rotZ: q.eulerZ ?? 0, scale }),
};

const LayerNames = [
  'BelowTerrain', 'TerrainEdges', 'Terrain', 'TerrainScatter', 'Floor', 'Conduits', 'FloorCoverings',
  'FloorEmplacement', 'Filth', 'Zone', 'SmallWire', 'LowPlant', 'MoteLow', 'Shadows', 'DoorMoveable',
  'Building', 'BuildingBelowTop', 'BuildingOnTop', 'Item', 'ItemImportant', 'LayingPawn', 'PawnRope',
  'Projectile', 'Pawn', 'PawnUnused', 'PawnState', 'Blueprint', 'MoteOverheadLow', 'MoteOverhead', 'Gas',
  'Skyfaller', 'Weather', 'LightingOverlay', 'VisEffects', 'FogOfWar', 'Darkness', 'WorldClipper',
  'Silhouettes', 'MapDataOverlay', 'MetaOverlays',
];

/** AltitudeLayer.MoteOverhead.AltitudeFor() works as it does in C#. */
export const AltitudeLayer = Object.fromEntries(LayerNames.map((name, i) => [name, {
  name, index: i, AltitudeFor: (offset = 0) => (i + offset) * LayerSpacing,
}]));

/** The layer an altitude falls in, and how far above its base, for the Layers panel. */
export function layerOf(y) {
  const index = Math.max(0, Math.min(LayerNames.length - 1, Math.floor(y / LayerSpacing + 1e-4)));
  return { name: LayerNames[index], offset: y - index * LayerSpacing };
}

export const Shader = (name) => ({ name });
export const ShaderDatabase = {
  Transparent: Shader('Transparent'), Mote: Shader('Mote'), MoteGlow: Shader('MoteGlow'), Cutout: Shader('Cutout'),
};
/**
 * Not a ShaderDatabase shader. Stands in for Unity's built-in Hidden/Internal-Colored with
 * _SrcBlend = OneMinusDstColor and _DstBlend = OneMinusSrcAlpha, which C# builds as its own
 * Material (Shader.Find). Drawn with colour (a, a, a, a) it gives lerp(screen, 1 - screen, a):
 * a negative of everything already drawn under it.
 */
export const InvertShader = Shader('Invert');
export const ShaderPropertyIDs = { Color: 'Color', AgeSecs: 'AgeSecs' };

export class Texture2D { constructor(path) { this.path = path; } }
export const BaseContent = { WhiteTex: new Texture2D('white') };
export const ContentFinder = { Get: (path) => new Texture2D(path) };

let nextMaterial = 1;
export class Material {
  constructor(shader, init = {}) {
    this.id = `sketch-${nextMaterial++}`;
    this.shader = shader;
    this.mainTexture = init.mainTexture ?? BaseContent.WhiteTex;
    this.color = init.color ?? Color.white;
    this.textures = {};
    this.floats = {};
  }
  SetTexture(name, tex) { this.textures[name] = tex.path; }
  SetFloat(name, v) { this.floats[name] = v; }
  /** The renderer reads this shape; recordings produce it from JSON. */
  get data() {
    return this._data ??= { shader: this.shader.name, tex: this.mainTexture.path, textures: this.textures, floats: this.floats };
  }
}

const pool = new Map();
export const MaterialPool = {
  MatFrom(path, shader) {
    const key = `${path}|${shader.name}`;
    if (!pool.has(key)) pool.set(key, new Material(shader, { mainTexture: new Texture2D(path) }));
    return pool.get(key);
  },
};

export class MaterialPropertyBlock {
  SetColor(id, c) { if (id === ShaderPropertyIDs.Color) this.colour = c; }
  SetFloat(id, v) { if (id === ShaderPropertyIDs.AgeSecs) this.age = v; }
}

let nextMesh = 1;
/**
 * Vertices are Vector3 on the map plane (y is ignored, as it is in game). Every write bumps
 * `version`, which is what tells the renderer to re-upload.
 */
export class Mesh {
  constructor(name = 'mesh') { this.id = `sketch-mesh-${nextMesh++}`; this.name = name; this.version = 0; this.v = new Float32Array(0); this.v3 = null; this.game = null; this.uv = null; this.tri = new Uint32Array(0); }
  set vertices(list) {
    const v = new Float32Array(list.length * 2);
    list.forEach((p, i) => { v[i * 2] = p.x; v[i * 2 + 1] = p.z; });
    this.v = v; this.version++;
  }
  set uvs(list) {
    const uv = new Float32Array(list.length * 2);
    list.forEach((p, i) => { uv[i * 2] = p.x; uv[i * 2 + 1] = p.y; });
    this.uv = uv; this.version++;
  }
  set triangles(list) { this.tri = Uint32Array.from(list); this.version++; }
  /** Flat [x, z, x, z...] without allocating Vector3s, for meshes rebuilt every frame. */
  setFlat(xz, tri) { this.v = Float32Array.from(xz); this.v3 = null; this.game = null; if (tri) this.tri = Uint32Array.from(tri); this.version++; }
  /**
   * [x, y, z, ...] with y up, for a sketch with a 3D camera: y is height in cells, not altitude. The map
   * camera still draws the mesh from above (its x, z).
   */
  setXYZ(xyz, tri) {
    this.v3 = Float32Array.from(xyz);
    const v = new Float32Array(this.v3.length / 3 * 2);
    for (let i = 0, j = 0; i < this.v3.length; i += 3, j += 2) { v[j] = this.v3[i]; v[j + 1] = this.v3[i + 2]; }
    this.v = v; this.game = null;
    if (tri) this.tri = Uint32Array.from(tri);
    this.version++;
  }
  /**
   * [x, z, ...], one pair per vertex: where the vertex is drawn in the game view, in map cells with the
   * height rule (and anything else the game drawing does) applied. A 3D camera's blend moves each vertex
   * from its 3D projection to this. Without it the game position is (x, z + 0.6 y) of the vertex.
   */
  setGame(xz) { this.game = Float32Array.from(xz); this.version++; }
}

export const MeshPool = (() => {
  const plane = new Mesh('plane10');
  plane.setFlat([-0.5, -0.5, -0.5, 0.5, 0.5, 0.5, 0.5, -0.5], [0, 1, 2, 0, 2, 3]);
  plane.uv = new Float32Array([0, 0, 0, 1, 1, 1, 1, 0]);
  return { plane10: plane };
})();

/** Frame buffer shared by Graphics, Overlay and Find.CameraDriver while a sketch draws. */
const frame = { calls: [], events: [], overlays: [] };
let flatAt = null, onScreen = false;

export const Graphics = {
  DrawMesh(mesh, matrix, material, layer = 0, camera = null, submesh = 0, props = null) {
    const c = props?.colour ?? material.color;
    frame.calls.push({
      mesh, mat: material.data,
      x: matrix.pos.x, y: matrix.pos.y, z: matrix.pos.z, rot: matrix.rot, rx: matrix.rotX ?? 0, rz: matrix.rotZ ?? 0,
      sx: matrix.scale.x, sy: matrix.scale.y, sz: matrix.scale.z,
      r: c.r, g: c.g, b: c.b, a: c.a, age: props?.age ?? 0, flat: flatAt, screen: onScreen,
    });
  },
  /**
   * For a sketch with a 3D camera: what fn draws with the map's own calls (y an altitude, only the Y angle)
   * lies flat on the ground at `height` instead, drawn in call order over what is there without a depth
   * test: glows, shadows, rings and marks made by the 2D helpers. On the map camera it draws as usual.
   */
  Flat(height, fn) {
    const was = flatAt;
    flatAt = height;
    try { fn(); } finally { flatAt = was; }
  },
  /**
   * For a sketch with a 3D camera: what fn draws with the map's own calls is drawn where the game view
   * would draw it, whatever the 3D camera does, over what is there: things tied to the game camera (a haze
   * across the screen, embers in front of it). On the map camera it draws as usual.
   */
  OnScreen(fn) {
    const was = onScreen;
    onScreen = true;
    try { fn(); } finally { onScreen = was; }
  },
  beginFrame() { frame.calls = []; frame.events = []; frame.overlays = []; },
  endFrame() { return { calls: frame.calls, events: frame.events, overlays: frame.overlays }; },
};

/**
 * Drawn on the screen after everything else, not on the map: a cutscene's fade or its black bars. x, y,
 * w, h are fractions of the view, y from the top. In game: Widgets.DrawBoxSolid over the UI in OnGUI.
 */
export const Overlay = {
  Fill(x, y, w, h, colour) { if (colour.a > 0) frame.overlays.push({ x, y, w, h, r: colour.r, g: colour.g, b: colour.b, a: colour.a }); },
};

export const Find = {
  CameraDriver: { shaker: { DoShake: (value) => frame.events.push({ type: 'shake', value }) } },
};

/** Circle and band builders every kit ends up writing; kept here so sketches don't. */
export const Meshes = {
  disc(segments = 64, name = 'disc') {
    const xz = [0, 0], tri = [];
    for (let i = 0; i < segments; i++) {
      const a = (i / segments) * Math.PI * 2;
      xz.push(Math.cos(a), Math.sin(a));
      tri.push(0, ((i + 1) % segments) + 1, i + 1);
    }
    const m = new Mesh(name); m.setFlat(xz, tri); return m;
  },
  band(inner, outer = 1, segments = 64, name = 'band') {
    const xz = [], tri = [];
    for (let i = 0; i <= segments; i++) {
      const a = (i / segments) * Math.PI * 2, c = Math.cos(a), s = Math.sin(a);
      xz.push(c * inner, s * inner, c * outer, s * outer);
      if (i < segments) { const v = i * 2; tri.push(v, v + 2, v + 1, v + 1, v + 2, v + 3); }
    }
    const m = new Mesh(name); m.setFlat(xz, tri); return m;
  },
};
