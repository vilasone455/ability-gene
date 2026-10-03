// WebGL2 stand-in for RimWorld's map camera: orthographic, looking straight down, x east and
// z north. Draw calls are sorted by altitude and then by call order -- the order the game's
// transparent queue ends up in for things drawn at distinct altitudes -- and blended the way the
// shaders they name blend:
//
//   Transparent, Mote   texture x colour, alpha blended
//   MoteGlow            texture x colour, additive
//   Cutout              alpha tested at 0.5
//   Invert              1 - what is under it, by alpha (Hidden/Internal-Colored, see engine.js)
//   MoteLargeDistortionWave   screen warp, approximated (the real shader is a vanilla asset)
//   a CustomShader      its own GLSL, reading the picture drawn so far (engine.js CustomShader)
//
// A view can instead be drawn through a perspective camera (view.three, from a sketch's camera()): calls
// in the order they were made, with a depth test, each vertex placed by the camera and moved by `blend`
// toward where the game view draws it. See draw3d.

import { standIn } from './standins.js';
import { viewProjection, modelMatrix } from './camera.js';

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

// The perspective camera's program. Each vertex is projected by the camera and, by u_blend (0 to 1), moved
// in screen space toward its game-view position: a_game through the call's map transform when the mesh has
// one (Mesh.setGame), otherwise the height rule on its world position, (x, z + lift * y). Depth stays the
// perspective camera's; w is blended with the position so textures stay perspective-correct at blend 0.
// Once blending, a vertex behind the camera (or far off the screen) is put 3 screen half-widths out in its
// direction, so a mesh that runs behind the camera does not streak: big surfaces should be grids of a few
// cells, so that no triangle joins a vertex on the screen to one behind the camera.
const THREE_VS = `#version 300 es
in vec3 a_pos; in vec2 a_uv; in vec2 a_game;
uniform mat4 u_model, u_viewProj;
uniform vec2 u_translate, u_scale, u_cam, u_ndc;
uniform float u_rot, u_blend, u_lift, u_hasGame;
out vec2 v_uv;
void main() {
  vec4 world = u_model * vec4(a_pos, 1.0);
  vec4 clip = u_viewProj * world;
  v_uv = a_uv;
  if (u_blend <= 0.0) { gl_Position = clip; return; }
  vec2 g;
  if (u_hasGame > 0.5) {
    vec2 s = a_game * u_scale;
    float c = cos(u_rot), n = sin(u_rot);
    g = vec2(s.x * c + s.y * n, -s.x * n + s.y * c) + u_translate;
  } else {
    g = vec2(world.x, world.z + u_lift * world.y);
  }
  vec2 ndcG = (g - u_cam) * u_ndc, ndcP;
  float w = clip.w, depth;
  if (w < 0.05) {
    ndcP = normalize(clip.xy + vec2(0.0, 1e-4)) * 3.0;
    depth = 1.0;
  } else {
    ndcP = clip.xy / w;
    float l = length(ndcP);
    if (l > 3.0) ndcP *= 3.0 / l;
    depth = clamp(clip.z / w, -1.0, 1.0);
  }
  float wm = mix(max(w, 0.05), 1.0, u_blend);
  gl_Position = vec4(mix(ndcP, ndcG, u_blend) * wm, depth * wm, wm);
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
  gl.bindAttribLocation(program, 2, 'a_game');
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
    this.three = compile(gl, THREE_VS, BASIC_FS);
    this.meshes = new WeakMap();
    this.meshes3 = new WeakMap();
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

  /** The same mesh for the perspective program: x, y, z (y 0 for a flat mesh), uv, and its game positions. */
  mesh3(data) {
    const gl = this.gl;
    let entry = this.meshes3.get(data);
    if (entry && entry.version === (data.version ?? 0)) return entry;
    if (!entry) {
      entry = { vao: gl.createVertexArray(), pos: gl.createBuffer(), uv: gl.createBuffer(), game: gl.createBuffer(), index: gl.createBuffer() };
      this.meshes3.set(data, entry);
    }
    const n = data.v.length / 2;
    let xyz = data.v3;
    if (!xyz || xyz.length !== n * 3) {
      xyz = new Float32Array(n * 3);
      for (let i = 0; i < n; i++) { xyz[i * 3] = data.v[i * 2]; xyz[i * 3 + 2] = data.v[i * 2 + 1]; }
    }
    entry.hasGame = !!(data.game && data.game.length === n * 2);
    gl.bindVertexArray(entry.vao);
    gl.bindBuffer(gl.ARRAY_BUFFER, entry.pos);
    gl.bufferData(gl.ARRAY_BUFFER, xyz, gl.DYNAMIC_DRAW);
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 3, gl.FLOAT, false, 0, 0);
    gl.bindBuffer(gl.ARRAY_BUFFER, entry.uv);
    gl.bufferData(gl.ARRAY_BUFFER, data.uv && data.uv.length === n * 2 ? data.uv : new Float32Array(n * 2), gl.DYNAMIC_DRAW);
    gl.enableVertexAttribArray(1);
    gl.vertexAttribPointer(1, 2, gl.FLOAT, false, 0, 0);
    gl.bindBuffer(gl.ARRAY_BUFFER, entry.game);
    gl.bufferData(gl.ARRAY_BUFFER, entry.hasGame ? data.game : new Float32Array(n * 2), gl.DYNAMIC_DRAW);
    gl.enableVertexAttribArray(2);
    gl.vertexAttribPointer(2, 2, gl.FLOAT, false, 0, 0);
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
    // Depth, for views drawn through a 3D camera; the map views never test it.
    const depth = gl.createRenderbuffer();
    gl.bindRenderbuffer(gl.RENDERBUFFER, depth);
    gl.renderbufferStorageMultisample(gl.RENDERBUFFER, samples, gl.DEPTH_COMPONENT24, w, h);
    this.msFbo = gl.createFramebuffer();
    gl.bindFramebuffer(gl.FRAMEBUFFER, this.msFbo);
    gl.framebufferRenderbuffer(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.RENDERBUFFER, color);
    gl.framebufferRenderbuffer(gl.FRAMEBUFFER, gl.DEPTH_ATTACHMENT, gl.RENDERBUFFER, depth);
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
      if (view.three) { this.draw3d(view, w, h, dpr, used); this.drawOverlays(view.overlays); continue; }
      const ppc = view.camera.ppc * dpr;
      const ndc = [(2 * ppc) / w, (2 * ppc) / h];
      const order = view.calls.map((c, i) => [c.y, i]).sort((a, b) => a[0] - b[0] || a[1] - b[1]);
      let grabbedFor = null;

      for (const [, i] of order) {
        const call = view.calls[i];
        if (call.a <= 0 || view.hidden?.has(call.group)) continue;
        const mat = call.mat;
        const tex = this.texture(mat.tex);
        if (tex.standIn) used.add(mat.tex);
        if (!tex.ready) continue;
        const mesh = this.mesh(call.mesh);
        const warp = mat.shader === 'MoteLargeDistortionWave', custom = !!mat.fragment;
        const prog = custom ? this.custom(mat) : warp ? this.warp : this.basic;
        if (!custom) grabbedFor = null;
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

        if (custom) {
          // As a named GrabPass does: the picture so far is copied once for a run of calls on one shader.
          if (grabbedFor !== mat.shader) {
            gl.disable(gl.SCISSOR_TEST);
            gl.bindFramebuffer(gl.READ_FRAMEBUFFER, this.msFbo);
            gl.bindFramebuffer(gl.DRAW_FRAMEBUFFER, this.copyFbo);
            gl.blitFramebuffer(0, 0, W, H, 0, 0, W, H, gl.COLOR_BUFFER_BIT, gl.NEAREST);
            gl.bindFramebuffer(gl.FRAMEBUFFER, this.msFbo);
            gl.enable(gl.SCISSOR_TEST);
            gl.viewport(vx, vy, w, h);
            grabbedFor = mat.shader;
          }
          used.add(`${mat.shader} (the mod's own shader; the lab draws its GLSL stand-in)`);
          gl.activeTexture(gl.TEXTURE1); gl.bindTexture(gl.TEXTURE_2D, this.copyTex); gl.uniform1i(u.u_scene, 1);
          gl.uniform2f(u.u_screen, W, H);
          gl.uniform2f(u.u_cellUv, ppc / W, ppc / H);
          gl.uniform1f(u.u_age, call.age ?? 0);
          for (const [name, v] of Object.entries(mat.floats ?? {})) if (u[name]) gl.uniform1f(u[name], v);
          for (const [name, v] of Object.entries(call.values ?? {})) {
            if (!u[name]) continue;
            if (Array.isArray(v)) gl.uniform4f(u[name], v[0], v[1], v[2], v[3]); else gl.uniform1f(u[name], v);
          }
          gl.disable(gl.BLEND);
        } else if (warp) {
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
          else if (mat.shader === 'Invert') gl.blendFuncSeparate(gl.ONE_MINUS_DST_COLOR, gl.ONE_MINUS_SRC_ALPHA, gl.ZERO, gl.ONE);
          else gl.blendFuncSeparate(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA, gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
        }
        gl.bindVertexArray(mesh.vao);
        gl.drawElements(gl.TRIANGLES, mesh.count, gl.UNSIGNED_INT, 0);
        gl.activeTexture(gl.TEXTURE0);
      }
      this.drawOverlays(view.overlays);
    }

    gl.disable(gl.SCISSOR_TEST);
    gl.bindFramebuffer(gl.READ_FRAMEBUFFER, this.msFbo);
    gl.bindFramebuffer(gl.DRAW_FRAMEBUFFER, null);
    gl.blitFramebuffer(0, 0, W, H, 0, 0, W, H, gl.COLOR_BUFFER_BIT, gl.NEAREST);
    return used;
  }

  /** The program for a CustomShader material, compiled once per shader name. */
  custom(mat) {
    this.customs ??= new Map();
    if (!this.customs.has(mat.shader)) this.customs.set(mat.shader, compile(this.gl, BASIC_VS, mat.fragment));
    return this.customs.get(mat.shader);
  }

  setBlend(shader) {
    const gl = this.gl;
    gl.enable(gl.BLEND);
    if (shader === 'MoteGlow') gl.blendFuncSeparate(gl.SRC_ALPHA, gl.ONE, gl.ZERO, gl.ONE);
    else if (shader === 'Invert') gl.blendFuncSeparate(gl.ONE_MINUS_DST_COLOR, gl.ONE_MINUS_SRC_ALPHA, gl.ZERO, gl.ONE);
    else gl.blendFuncSeparate(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA, gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
  }

  /**
   * A view through a sketch's 3D camera (view.three = what camera() returned). Calls draw in the order
   * they were made, depth tested. A normal-blended call (Transparent, Mote, Cutout) also writes depth where
   * its alpha is 0.5 or more, unless its material sets _ZWrite to 0; MoteGlow and Invert never do. So a
   * sketch draws far to near and see-through things last, and the depth test sorts what painter's order
   * cannot (a ground mesh under many swords). A material with _ZTest 8 (Unity's CompareFunction.Always)
   * skips the test: for marks lying on a surface, drawn just after it; so does a call made inside
   * Graphics.Flat, which also lies at the given height, or inside Graphics.OnScreen, which is drawn where
   * the game view draws it. The distortion shader is not drawn.
   * view.camera is the game view the blend moves toward: { cx, cz, ppc }.
   */
  draw3d(view, w, h, dpr, used) {
    const gl = this.gl, cam = view.three, prog = this.three, u = prog.uniforms;
    const blend = Math.min(1, Math.max(0, cam.blend ?? 0)), ppc = view.camera.ppc * dpr;
    gl.clearDepth(1);
    gl.depthMask(true);
    gl.clear(gl.DEPTH_BUFFER_BIT);
    gl.enable(gl.DEPTH_TEST);
    gl.depthFunc(gl.LEQUAL);
    gl.useProgram(prog.program);
    gl.uniformMatrix4fv(u.u_viewProj, false, viewProjection(cam, w / h));
    gl.uniform2f(u.u_cam, view.camera.cx, view.camera.cz);
    gl.uniform2f(u.u_ndc, (2 * ppc) / w, (2 * ppc) / h);
    gl.uniform1f(u.u_blend, blend);
    gl.uniform1f(u.u_lift, cam.lift ?? .6);
    gl.uniform1i(u.u_tex, 0);
    gl.activeTexture(gl.TEXTURE0);
    for (const call of view.calls) {
      if (call.a <= 0 || view.hidden?.has(call.group)) continue;
      const mat = call.mat;
      if (mat.shader === 'MoteLargeDistortionWave') { used.add('MoteLargeDistortionWave (not drawn by a 3D camera)'); continue; }
      if (mat.fragment) { used.add(`${mat.shader} (not drawn by a 3D camera)`); continue; }
      const tex = this.texture(mat.tex);
      if (tex.standIn) used.add(mat.tex);
      if (!tex.ready) continue;
      const mesh = this.mesh3(call.mesh), flat = call.flat != null || call.screen;
      gl.uniformMatrix4fv(u.u_model, false, modelMatrix(flat ? { ...call, y: call.screen ? 0 : call.flat, rx: 0, rz: 0 } : call));
      gl.uniform1f(u.u_blend, call.screen ? 1 : blend);
      gl.uniform2f(u.u_translate, call.x, call.z);
      gl.uniform2f(u.u_scale, call.sx, call.sz);
      gl.uniform1f(u.u_rot, call.rot * Math.PI / 180);
      gl.uniform1f(u.u_hasGame, mesh.hasGame ? 1 : 0);
      gl.uniform4f(u.u_color, call.r, call.g, call.b, call.a);
      gl.bindTexture(gl.TEXTURE_2D, tex.tex);
      gl.bindVertexArray(mesh.vao);
      const cutout = mat.shader === 'Cutout', normal = mat.shader !== 'MoteGlow' && mat.shader !== 'Invert';
      const writes = normal && !flat && !call.noWrite && (mat.floats?._ZWrite ?? 1) !== 0;
      gl.depthFunc(flat || mat.floats?._ZTest === 8 ? gl.ALWAYS : gl.LEQUAL);
      // the colour, blended, tested against what is nearer
      gl.depthMask(false);
      gl.uniform1f(u.u_cutoff, cutout ? .5 : .002);
      this.setBlend(mat.shader);
      gl.drawElements(gl.TRIANGLES, mesh.count, gl.UNSIGNED_INT, 0);
      if (!writes) continue;
      // then its depth where it is at least half opaque
      gl.colorMask(false, false, false, false);
      gl.depthMask(true);
      gl.uniform1f(u.u_cutoff, .5);
      gl.drawElements(gl.TRIANGLES, mesh.count, gl.UNSIGNED_INT, 0);
      gl.colorMask(true, true, true, true);
    }
    gl.depthMask(true);
    gl.disable(gl.DEPTH_TEST);
  }

  /** Overlay.Fill boxes over the view: x, y, w, h are fractions of it, y from the top. */
  drawOverlays(overlays) {
    if (!overlays?.length) return;
    const gl = this.gl, prog = this.basic, u = prog.uniforms, white = this.texture('white'), quad = this.mesh(this.overlayQuad ??= {
      v: new Float32Array([-.5, -.5, -.5, .5, .5, .5, .5, -.5]), uv: new Float32Array([0, 0, 0, 1, 1, 1, 1, 0]), tri: new Uint32Array([0, 1, 2, 0, 2, 3]), version: 0,
    });
    if (!white.ready) return;
    gl.useProgram(prog.program);
    gl.uniform2f(u.u_cam, .5, .5);
    gl.uniform2f(u.u_ndc, 2, 2);
    gl.uniform1f(u.u_rot, 0);
    gl.uniform1f(u.u_cutoff, .002);
    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, white.tex);
    gl.uniform1i(u.u_tex, 0);
    this.setBlend('Transparent');
    gl.bindVertexArray(quad.vao);
    for (const o of overlays) {
      gl.uniform2f(u.u_translate, o.x + o.w / 2, 1 - o.y - o.h / 2);
      gl.uniform2f(u.u_scale, o.w, o.h);
      gl.uniform4f(u.u_color, o.r, o.g, o.b, o.a);
      gl.drawElements(gl.TRIANGLES, quad.count, gl.UNSIGNED_INT, 0);
    }
  }
}
