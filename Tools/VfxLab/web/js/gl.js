// WebGL2 stand-in for RimWorld's map camera: orthographic, looking straight down, x east and
// z north. Draw calls are sorted by altitude and then by call order -- the order the game's
// transparent queue ends up in for things drawn at distinct altitudes -- and blended the way the
// shaders they name blend:
//
//   Transparent, Mote   texture x colour, alpha blended
//   MoteGlow            texture x colour, additive
//   Cutout              alpha tested at 0.5
//   MoteLargeDistortionWave   screen warp, approximated (the real shader is a vanilla asset)

import { standIn } from './standins.js';

const BASIC_VS = `#version 300 es
in vec2 a_pos; in vec2 a_uv;
uniform vec2 u_translate, u_scale, u_cam, u_ndc;
uniform float u_rot;
out vec2 v_uv;
void main() {
  vec2 s = a_pos * u_scale;
  float c = cos(u_rot), n = sin(u_rot);
  // Quaternion.Euler(0, a, 0): local +x goes to (cos a, -sin a) on the map.
  vec2 w = vec2(s.x * c + s.y * n, -s.x * n + s.y * c) + u_translate;
  gl_Position = vec4((w - u_cam) * u_ndc, 0.0, 1.0);
  v_uv = a_uv;
}`;

const BASIC_FS = `#version 300 es
precision mediump float;
in vec2 v_uv;
uniform sampler2D u_tex;
uniform vec4 u_color;
uniform float u_cutoff;
out vec4 o;
void main() {
  vec4 t = texture(u_tex, v_uv) * u_color;
  if (t.a < u_cutoff) discard;
  o = t;
}`;

const WARP_FS = `#version 300 es
precision mediump float;
in vec2 v_uv;
uniform sampler2D u_tex, u_scene, u_currents, u_noise;
uniform vec4 u_color;
uniform vec2 u_screen, u_cellUv;
uniform float u_age, u_intensity;
out vec4 o;
void main() {
  vec4 mask = texture(u_tex, v_uv);
  vec2 flow = texture(u_currents, v_uv * 1.3 + vec2(u_age * 0.06, u_age * 0.035)).rg - 0.5;
  float grain = texture(u_noise, v_uv * 2.1 - vec2(u_age * 0.1)).r;
  float strength = mask.a * u_color.a * u_intensity;
  // About two cells of shift at intensity 1: 0.12 moves the ground a quarter of a cell.
  vec2 offset = flow * (0.6 + 0.8 * grain) * strength * 4.0 * u_cellUv;
  vec3 warped = texture(u_scene, gl_FragCoord.xy / u_screen + offset).rgb;
  o = vec4(mix(warped, mask.rgb, strength * 0.6), 1.0);
}`;

function compile(gl, vs, fs) {
  const program = gl.createProgram();
  for (const [type, src] of [[gl.VERTEX_SHADER, vs], [gl.FRAGMENT_SHADER, fs]]) {
    const s = gl.createShader(type);
    gl.shaderSource(s, src);
    gl.compileShader(s);
    if (!gl.getShaderParameter(s, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(s));
    gl.attachShader(program, s);
  }
  gl.bindAttribLocation(program, 0, 'a_pos');
  gl.bindAttribLocation(program, 1, 'a_uv');
  gl.linkProgram(program);
  if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program));
  const uniforms = {};
  const count = gl.getProgramParameter(program, gl.ACTIVE_UNIFORMS);
  for (let i = 0; i < count; i++) {
    const { name } = gl.getActiveUniform(program, i);
    uniforms[name] = gl.getUniformLocation(program, name);
  }
  return { program, uniforms };
}

/** The stage's own background, the one colour the lab clears to when it is not exporting. */
const Ground = [0.105, 0.121, 0.133, 1];

export class Renderer {
  constructor(canvas, onTexture) {
    this.canvas = canvas;
    // alpha: true so the drawing buffer is RGBA8 like the multisampled one it is blitted from;
    // every pixel is written opaque, so nothing shows through.
    const gl = canvas.getContext('webgl2', { antialias: false, alpha: true, premultipliedAlpha: false, preserveDrawingBuffer: true });
    if (!gl) throw new Error('This browser has no WebGL2.');
    this.gl = gl;
    this.basic = compile(gl, BASIC_VS, BASIC_FS);
    this.warp = compile(gl, BASIC_VS, WARP_FS);
    this.meshes = new WeakMap();
    this.textures = new Map();
    this.onTexture = onTexture;
    this.standIns = new Set();
    this.size = [0, 0];
    gl.disable(gl.CULL_FACE);
    gl.disable(gl.DEPTH_TEST);
  }

  /** Mod PNGs load from the repository's Textures/; anything else becomes a stand-in. */
  texture(path) {
    let entry = this.textures.get(path);
    if (entry) return entry;
    const gl = this.gl;
    entry = { tex: gl.createTexture(), ready: false, standIn: false, path };
    this.textures.set(path, entry);
    const upload = (source, repeat) => {
      gl.bindTexture(gl.TEXTURE_2D, entry.tex);
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, source);
      gl.generateMipmap(gl.TEXTURE_2D);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
      const wrap = repeat ? gl.REPEAT : gl.CLAMP_TO_EDGE;
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, wrap);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, wrap);
      entry.ready = true;
      this.onTexture?.();
    };
    const fallback = () => {
      // Melee Animation's hand: not a wrong path, just a mod that is not installed (or a lab.py from
      // before /_am/ existed). A soft disc takes the skin tint and still lists as a stand-in.
      const s = standIn(path.startsWith('am:AM/Hand') ? 'lab/soft-disc' : path);
      entry.standIn = true;
      entry.known = s.known;
      upload(s.canvas, path.includes('Currents') || path.includes('Noise'));
    };
    if (path === 'white' || path.startsWith('lab/') || path.startsWith('Things/') || path.startsWith('Other/') || path === 'generated') {
      fallback();
      entry.standIn = !(path === 'white' || path.startsWith('lab/'));
    } else if (path.startsWith('canvas:')) {
      // Registered by the scene through setCanvasTexture.
    } else {
      const img = new Image();
      img.onload = () => { upload(img, false); if (window.__labDebug) console.log('LAB loaded', path); };
      img.onerror = () => { if (window.__labDebug) console.log('LAB failed', path); fallback(); };
      // "am:" is Melee Animation's own Textures folder, served by lab.py from where that mod is installed.
      // "mod:<workshop id>/<folder>/<path>" is a weapon PNG of another installed mod, listed by lab.py.
      img.src = path.startsWith('am:') ? `/_am/Textures/${path.slice(3)}.png`
        : path.startsWith('mod:') ? `/_mod/${path.slice(4)}.png` : `/Textures/${path}.png`;
    }
    return entry;
  }

  setCanvasTexture(path, canvasSource, repeat = false) {
    const gl = this.gl;
    let entry = this.textures.get(path);
    if (!entry) { entry = { tex: gl.createTexture(), path }; this.textures.set(path, entry); }
    gl.bindTexture(gl.TEXTURE_2D, entry.tex);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, canvasSource);
    gl.generateMipmap(gl.TEXTURE_2D);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    const wrap = repeat ? gl.REPEAT : gl.CLAMP_TO_EDGE;
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, wrap);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, wrap);
    entry.ready = true;
    entry.standIn = false;
  }

  mesh(data) {
    const gl = this.gl;
    let entry = this.meshes.get(data);
    if (entry && entry.version === (data.version ?? 0)) return entry;
    if (!entry) {
      entry = { vao: gl.createVertexArray(), pos: gl.createBuffer(), uv: gl.createBuffer(), index: gl.createBuffer() };
      this.meshes.set(data, entry);
    }
    gl.bindVertexArray(entry.vao);
    gl.bindBuffer(gl.ARRAY_BUFFER, entry.pos);
    gl.bufferData(gl.ARRAY_BUFFER, data.v, gl.DYNAMIC_DRAW);
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
    gl.bindBuffer(gl.ARRAY_BUFFER, entry.uv);
    gl.bufferData(gl.ARRAY_BUFFER, data.uv && data.uv.length === data.v.length ? data.uv : new Float32Array(data.v.length), gl.DYNAMIC_DRAW);
    gl.enableVertexAttribArray(1);
    gl.vertexAttribPointer(1, 2, gl.FLOAT, false, 0, 0);
    gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, entry.index);
    gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, data.tri instanceof Uint32Array ? data.tri : Uint32Array.from(data.tri), gl.DYNAMIC_DRAW);
    entry.count = data.tri.length;
    entry.version = data.version ?? 0;
    return entry;
  }

  resize() {
    const dpr = window.devicePixelRatio || 1;
    this.allocate(Math.max(1, Math.round(this.canvas.clientWidth * dpr)),
      Math.max(1, Math.round(this.canvas.clientHeight * dpr)));
  }

  /**
   * Point the drawing buffer and its multisampled target at this exact pixel size. resize() calls
   * it with the canvas's own size; the frame exporter calls it with whatever size it is writing.
   */
  allocate(w, h) {
    const gl = this.gl;
    if (w === this.size[0] && h === this.size[1]) return;
    this.size = [w, h];
    this.canvas.width = w; this.canvas.height = h;
    const samples = Math.min(4, gl.getParameter(gl.MAX_SAMPLES));
    this.ms?.forEach((o) => o.delete?.());
    const color = gl.createRenderbuffer();
    gl.bindRenderbuffer(gl.RENDERBUFFER, color);
    gl.renderbufferStorageMultisample(gl.RENDERBUFFER, samples, gl.RGBA8, w, h);
    this.msFbo = gl.createFramebuffer();
    gl.bindFramebuffer(gl.FRAMEBUFFER, this.msFbo);
    gl.framebufferRenderbuffer(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.RENDERBUFFER, color);
    this.copyTex = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, this.copyTex);
    gl.texStorage2D(gl.TEXTURE_2D, 1, gl.RGBA8, w, h);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    this.copyFbo = gl.createFramebuffer();
    gl.bindFramebuffer(gl.FRAMEBUFFER, this.copyFbo);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, this.copyTex, 0);
  }

  /**
   * views: [{ rect: [x, y, w, h] in CSS px from top-left, camera: { cx, cz, ppc }, calls, hidden: Set }]
   * Returns the stand-in texture paths drawn this frame.
   */
  render(views) {
    this.resize();
    return this.draw(views, window.devicePixelRatio || 1, Ground);
  }

  /**
   * One view drawn at an exact pixel size, for the frame exporter. The canvas keeps its drawing
   * buffer, so the caller reads the frame straight back with toDataURL before anything else
   * draws; the next render() puts the buffer back to the canvas's own size.
   *
   * A transparent clear gives back the effect on nothing, which is what a frame sequence for an
   * animation wants. Alpha is straight, not premultiplied, as the context is created.
   */
  renderExport(view, width, height, { transparent = false } = {}) {
    this.allocate(width, height);
    return this.draw([{ ...view, rect: [0, 0, width, height] }], 1, transparent ? [0, 0, 0, 0] : Ground);
  }

  draw(views, dpr, clear) {
    const gl = this.gl, [W, H] = this.size;
    const used = new Set();
    gl.bindFramebuffer(gl.FRAMEBUFFER, this.msFbo);
    gl.viewport(0, 0, W, H);
    gl.disable(gl.SCISSOR_TEST);
    gl.clearColor(...clear);
    gl.clear(gl.COLOR_BUFFER_BIT);
    gl.enable(gl.BLEND);

    for (const view of views) {
      const [x, y, w, h] = view.rect.map((n) => Math.round(n * dpr));
      const vx = x, vy = H - y - h;
      gl.viewport(vx, vy, w, h);
      gl.enable(gl.SCISSOR_TEST);
      gl.scissor(vx, vy, w, h);
      const ppc = view.camera.ppc * dpr;
      const ndc = [(2 * ppc) / w, (2 * ppc) / h];
      const order = view.calls.map((c, i) => [c.y, i]).sort((a, b) => a[0] - b[0] || a[1] - b[1]);

      for (const [, i] of order) {
        const call = view.calls[i];
        if (call.a <= 0 || view.hidden?.has(call.group)) continue;
        const mat = call.mat;
        const tex = this.texture(mat.tex);
        if (tex.standIn) used.add(mat.tex);
        if (!tex.ready) continue;
        const mesh = this.mesh(call.mesh);
        const warp = mat.shader === 'MoteLargeDistortionWave';
        const prog = warp ? this.warp : this.basic;
        gl.useProgram(prog.program);
        const u = prog.uniforms;
        gl.uniform2f(u.u_translate, call.x, call.z);
        gl.uniform2f(u.u_scale, call.sx, call.sz);
        gl.uniform1f(u.u_rot, call.rot * Math.PI / 180);
        gl.uniform2f(u.u_cam, view.camera.cx, view.camera.cz);
        gl.uniform2f(u.u_ndc, ndc[0], ndc[1]);
        gl.uniform4f(u.u_color, call.r, call.g, call.b, call.a);
        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, tex.tex);
        gl.uniform1i(u.u_tex, 0);

        if (warp) {
          // Resolve what is drawn so far, then draw the warp reading from that copy.
          gl.disable(gl.SCISSOR_TEST);
          gl.bindFramebuffer(gl.READ_FRAMEBUFFER, this.msFbo);
          gl.bindFramebuffer(gl.DRAW_FRAMEBUFFER, this.copyFbo);
          gl.blitFramebuffer(0, 0, W, H, 0, 0, W, H, gl.COLOR_BUFFER_BIT, gl.NEAREST);
          gl.bindFramebuffer(gl.FRAMEBUFFER, this.msFbo);
          gl.enable(gl.SCISSOR_TEST);
          gl.viewport(vx, vy, w, h);
          const currents = this.texture(mat.textures?._DistortionTex ?? 'Things/Mote/PsychicDistortionCurrents');
          const noise = this.texture(mat.textures?._NoiseTex ?? 'Things/Mote/PsycastNoise');
          used.add('MoteLargeDistortionWave (shader)');
          gl.activeTexture(gl.TEXTURE1); gl.bindTexture(gl.TEXTURE_2D, this.copyTex); gl.uniform1i(u.u_scene, 1);
          gl.activeTexture(gl.TEXTURE2); gl.bindTexture(gl.TEXTURE_2D, currents.tex); gl.uniform1i(u.u_currents, 2);
          gl.activeTexture(gl.TEXTURE3); gl.bindTexture(gl.TEXTURE_2D, noise.tex); gl.uniform1i(u.u_noise, 3);
          gl.uniform2f(u.u_screen, W, H);
          gl.uniform2f(u.u_cellUv, ppc / W, ppc / H);
          gl.uniform1f(u.u_age, call.age ?? 0);
          gl.uniform1f(u.u_intensity, mat.floats?._distortionIntensity ?? 0.1);
          gl.disable(gl.BLEND);
        } else {
          gl.uniform1f(u.u_cutoff, mat.shader === 'Cutout' ? 0.5 : 0.002);
          gl.enable(gl.BLEND);
          if (mat.shader === 'MoteGlow') gl.blendFuncSeparate(gl.SRC_ALPHA, gl.ONE, gl.ZERO, gl.ONE);
          else gl.blendFuncSeparate(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA, gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
        }
        gl.bindVertexArray(mesh.vao);
        gl.drawElements(gl.TRIANGLES, mesh.count, gl.UNSIGNED_INT, 0);
        gl.activeTexture(gl.TEXTURE0);
      }
    }

    gl.disable(gl.SCISSOR_TEST);
    gl.bindFramebuffer(gl.READ_FRAMEBUFFER, this.msFbo);
    gl.bindFramebuffer(gl.DRAW_FRAMEBUFFER, null);
    gl.blitFramebuffer(0, 0, W, H, 0, 0, W, H, gl.COLOR_BUFFER_BIT, gl.NEAREST);
    return used;
  }
}
