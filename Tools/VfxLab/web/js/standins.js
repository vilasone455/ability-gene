// Textures the lab cannot read. Vanilla art lives inside Unity asset bundles rather than loose
// PNGs, so anything a kit loads from Core -- distortion maps, skip flashes, skyfaller shadows --
// is drawn from a canvas here instead. Each stand-in is listed on the page while it is on
// screen: the shape and timing are the kit's own, the pixels under a stand-in are not.
//
// Paths the lab itself draws with (scene sprites, sketch puffs) live under "lab/".

const size = 256;

function canvas(n = size) {
  const c = document.createElement('canvas');
  c.width = c.height = n;
  return c;
}

function hash(x, y, seed) {
  let h = (x * 374761393 + y * 668265263 + seed * 144665) | 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177);
  return ((h ^ (h >>> 16)) >>> 0) / 4294967295;
}

/** Tileable value noise, 0..1. */
function noise(x, y, period, seed) {
  const xi = Math.floor(x), yi = Math.floor(y), xf = x - xi, yf = y - yi;
  const s = (t) => t * t * (3 - 2 * t);
  const at = (i, j) => hash(((i % period) + period) % period, ((j % period) + period) % period, seed);
  const a = at(xi, yi), b = at(xi + 1, yi), c = at(xi, yi + 1), d = at(xi + 1, yi + 1);
  return a + (b - a) * s(xf) + (c - a) * s(yf) + (a - b - c + d) * s(xf) * s(yf);
}

function fbm(x, y, seed, octaves = 4, period = 8) {
  let v = 0, amp = 0.5, total = 0;
  for (let o = 0; o < octaves; o++) {
    v += noise(x * (1 << o), y * (1 << o), period * (1 << o), seed + o) * amp;
    total += amp; amp *= 0.5;
  }
  return v / total;
}

function pixels(n, fn) {
  const c = canvas(n), ctx = c.getContext('2d'), img = ctx.createImageData(n, n);
  for (let y = 0; y < n; y++)
    for (let x = 0; x < n; x++) {
      const [r, g, b, a] = fn(x / n, y / n);
      const i = (y * n + x) * 4;
      img.data[i] = r * 255; img.data[i + 1] = g * 255; img.data[i + 2] = b * 255; img.data[i + 3] = a * 255;
    }
  ctx.putImageData(img, 0, 0);
  return c;
}

const radial = (u, v) => Math.hypot(u - 0.5, v - 0.5) * 2;

const makers = {
  white: () => pixels(2, () => [1, 1, 1, 1]),
  'Things/Mote/Black': () => pixels(2, () => [0, 0, 0, 1]),
  'Things/Mote/PsychicDistortionCurrents': () =>
    pixels(size, (u, v) => [fbm(u * 8, v * 8, 11), fbm(u * 8, v * 8, 29), 0.5, 1]),
  'Things/Mote/PsycastNoise': () => pixels(size, (u, v) => { const n = fbm(u * 8, v * 8, 53); return [n, n, n, 1]; }),
  'Things/Mote/PsycastSkipFlash': () => pixels(size, (u, v) => {
    const r = radial(u, v); const a = Math.exp(-((r - 0.62) ** 2) / 0.02) * 0.9 + Math.max(0, 1 - r) * 0.25;
    return [1, 1, 1, Math.min(1, a)];
  }),
  'Things/Mote/SkipInnerDimension': () => pixels(size, (u, v) => {
    const r = radial(u, v), ang = Math.atan2(v - 0.5, u - 0.5);
    const swirl = 0.5 + 0.5 * Math.sin(ang * 3 + r * 12);
    return [1, 1, 1, Math.max(0, 1 - r) * swirl];
  }),
  'Other/ForceField': () => pixels(size, (u, v) => {
    const r = radial(u, v); return [1, 1, 1, r > 1 ? 0 : 0.15 + 0.85 * r ** 6];
  }),
  'Things/Skyfaller/SkyfallerShadowDropPod': () => pixels(size, (u, v) => [0, 0, 0, Math.max(0, 1 - radial(u, v)) ** 1.5]),
  'Things/Skyfaller/SkyfallerShadowCircle': () => pixels(size, (u, v) => [0, 0, 0, Math.max(0, 1 - radial(u, v)) ** 1.5]),
  'lab/soft-disc': () => pixels(128, (u, v) => [1, 1, 1, Math.max(0, 1 - radial(u, v)) ** 1.8]),
  'lab/puff': () => pixels(128, (u, v) => {
    const r = radial(u, v), n = fbm(u * 4, v * 4, 71, 3, 4);
    return [1, 1, 1, Math.max(0, Math.min(1, (1 - r) * 1.6 - 0.35 + (n - 0.5) * 0.9))];
  }),
};

/** A canvas for a known stand-in path, or a magenta checker that cannot be mistaken for art. */
export function standIn(path) {
  if (makers[path]) return { canvas: makers[path](), known: true };
  return {
    canvas: pixels(32, (u, v) => ((Math.floor(u * 4) + Math.floor(v * 4)) % 2 ? [1, 0, 1, 0.8] : [0.2, 0, 0.2, 0.8])),
    known: false,
  };
}

export function registerLabTexture(path, make) { makers[path] = make; }

export { fbm, hash, pixels, canvas };
