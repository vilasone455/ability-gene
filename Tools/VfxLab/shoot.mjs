#!/usr/bin/env node
// Screenshot one frame of the lab from the command line, with lab.py already serving.
//
//   node Tools/VfxLab/shoot.mjs "Six Paths: slam" 2.0 out.png
//   node Tools/VfxLab/shoot.mjs "Six Paths: slam" 2.0 out.png --b "Slam v2 (sketch)" --size 1500x900 --log
//   node Tools/VfxLab/shoot.mjs "Slam v2 (sketch)" 3.2 out.png p.exit=orbs p.seams=true
//
// Chrome runs headless with SwiftShader, so it works on a machine with no GPU, and in real time
// rather than virtual time: the mod's PNGs have to finish loading before the frame is taken.
// Needs Node 22+ (built-in WebSocket) and google-chrome on PATH.

import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const args = process.argv.slice(2);
const flag = (name, fallback) => { const i = args.indexOf(name); return i >= 0 ? args.splice(i, 2)[1] : fallback; };
const log = args.includes('--log') ? (args.splice(args.indexOf('--log'), 1), true) : false;
const b = flag('--b');
const [w, h] = flag('--size', '1500x900').split('x').map(Number);
const port = Number(flag('--port', '8765'));
const wait = Number(flag('--wait', '2500'));
const [effect, t, out] = args;
if (!effect || t === undefined || !out) {
  console.error('usage: shoot.mjs "<dev action or sketch label>" <seconds> <out.png> [--b "<label>"] [--size WxH] [--wait ms] [--log]');
  process.exit(2);
}

const query = new URLSearchParams({ effect, t });
for (const extra of args.slice(3)) { const [k, v] = extra.split("="); if (k && v !== undefined) query.set(k, v); }
if (b) query.set('b', b);
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
    if (log && msg.method === 'Runtime.consoleAPICalled')
      console.log('[page]', msg.params.args.map((a) => a.value ?? a.description).join(' '));
    if (msg.method === 'Runtime.exceptionThrown') console.error('[page error]', msg.params.exceptionDetails.exception?.description);
  };
  const send = (method, params = {}) => new Promise((r) => { const n = ++id; pending.set(n, r); ws.send(JSON.stringify({ id: n, method, params })); });

  await send('Runtime.enable');
  await send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false });
  await send('Page.navigate', { url });
  await sleep(wait);
  const shot = await send('Page.captureScreenshot', { format: 'png' });
  writeFileSync(out, Buffer.from(shot.result.data, 'base64'));
  console.log(`${out}  ${effect} @ ${t}s${b ? `  vs ${b}` : ''}`);
  ws.close();
} finally {
  chrome.kill();
  await sleep(300);
  rmSync(profile, { recursive: true, force: true });
}
