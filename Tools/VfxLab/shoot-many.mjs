#!/usr/bin/env node
// Screenshot many frames of one lab sketch from a single browser, with lab.py already serving.
// Chrome starts once and loads the page once; every frame after that is a seek and a capture,
// so a sheet of 12 frames costs one page load instead of twelve.
//
//   node Tools/VfxLab/shoot-many.mjs jobs.json
//
// jobs.json:
//   { "effect": "Twin Maw (sketch)", "port": 8765, "size": "1500x900", "ppc": 90, "wait": 4000,
//     "frames": [ { "t": 0.3, "out": "/tmp/a.png", "params": { "aim": 90 } }, ... ] }
//
// Prints one line per frame ("<out>  <effect> @ <t>s") and "[page error] ..." for exceptions.
// Needs Node 22+ (built-in WebSocket) and google-chrome on PATH.

import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const jobFile = process.argv[2];
if (!jobFile) { console.error('usage: shoot-many.mjs jobs.json'); process.exit(2); }
const job = JSON.parse(readFileSync(jobFile, 'utf8'));
const [w, h] = (job.size ?? '1500x900').split('x').map(Number);
const port = job.port ?? 8765, wait = job.wait ?? 4000, settle = job.settle ?? 120;

const query = new URLSearchParams({ effect: job.effect, t: '0' });
if (job.ppc) query.set('ppc', String(job.ppc));
const url = `http://localhost:${port}/Tools/VfxLab/web/?${query}&debug`;
const profile = mkdtempSync(join(tmpdir(), 'vfxlab-chrome-'));
const debugPort = 9300 + Math.floor(Math.random() * 500);
const chrome = spawn('google-chrome', [
  '--headless=new', '--no-sandbox', '--use-angle=swiftshader', '--enable-unsafe-swiftshader',
  `--remote-debugging-port=${debugPort}`, `--user-data-dir=${profile}`, `--window-size=${w},${h}`, 'about:blank',
], { stdio: 'ignore' });

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
try {
  let target;
  for (let i = 0; i < 50 && !target; i++) {
    await sleep(200);
    try { target = (await (await fetch(`http://127.0.0.1:${debugPort}/json/list`)).json()).find((x) => x.type === 'page'); } catch { /* not up yet */ }
  }
  if (!target) throw new Error('Chrome did not start');
  const ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((r, j) => { ws.onopen = r; ws.onerror = j; });
  let id = 0;
  const pending = new Map();
  ws.onmessage = (m) => {
    const msg = JSON.parse(m.data);
    if (msg.id && pending.has(msg.id)) { pending.get(msg.id)(msg); pending.delete(msg.id); }
    if (msg.method === 'Runtime.exceptionThrown') console.error('[page error]', msg.params.exceptionDetails.exception?.description);
  };
  const send = (method, params = {}) => new Promise((r) => { const n = ++id; pending.set(n, r); ws.send(JSON.stringify({ id: n, method, params })); });
  const evaluate = async (expression) => {
    const r = await send('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
    if (r.result?.exceptionDetails) console.error('[page error]', r.result.exceptionDetails.exception?.description ?? r.result.exceptionDetails.text);
    return r.result?.result?.value;
  };

  await send('Runtime.enable');
  await send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false });
  await send('Page.navigate', { url });
  // Wait for the lab to finish loading: __lab appears once the page's main() reaches the debug hook.
  for (let i = 0; i < wait / 100; i++) { if (await evaluate('Boolean(window.__lab && window.__lab.sourceA())')) break; await sleep(100); }
  await sleep(settle * 3);   // textures the first frame requested

  const known = await evaluate('JSON.stringify(Object.keys(window.__lab.sourceA().values || {}))');
  const keys = new Set(JSON.parse(known ?? '[]'));
  for (const frame of job.frames) {
    const params = frame.params ?? {};
    for (const k of Object.keys(params)) if (!keys.has(k)) console.error(`[page error] unknown param ${k} for ${job.effect}`);
    const set = Object.entries(params).map(([k, v]) => `values[${JSON.stringify(k)}] = (typeof values[${JSON.stringify(k)}] === 'number') ? Number(${JSON.stringify(String(v))}) : (typeof values[${JSON.stringify(k)}] === 'boolean') ? ${JSON.stringify(String(v))} === 'true' : ${JSON.stringify(String(v))};`).join('\n');
    await evaluate(`(() => { const L = window.__lab, s = L.sourceA(), values = s.values || {}; ${set}
      L.clock.playing = false; L.clock.duration = s.duration; L.clock.seek(${Number(frame.t)}); return L.clock.t; })()`);
    await sleep(settle);
    const shot = await send('Page.captureScreenshot', { format: 'png' });
    writeFileSync(frame.out, Buffer.from(shot.result.data, 'base64'));
    console.log(`${frame.out}  ${job.effect} @ ${frame.t}s${Object.keys(params).length ? '  ' + Object.entries(params).map(([k, v]) => `${k}=${v}`).join(' ') : ''}`);
  }
  ws.close();
} finally {
  chrome.kill();
  await sleep(300);
  rmSync(profile, { recursive: true, force: true });
}
