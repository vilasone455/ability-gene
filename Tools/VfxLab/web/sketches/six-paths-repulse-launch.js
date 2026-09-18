// Repulse Step — VFX-only proposal: two orbs converge (0–0.55s), flatten into
// an upright push surface (0.55–0.95s), load under the pawn (0.95–1.35s),
// then snap and launch horizontally (1.35–1.80s). The surface separates back
// into two orbs and follows the pawn. No jump arc; no gameplay/cost changes.
import { AltitudeLayer, Color, Mathf, Meshes } from '../js/engine.js';
import { draw, mesh, Lift } from './lib/six-paths-solid.js';
import { P, Body, Rim, Y, orb, sprite, trail, band, glow, soft, rand } from './lib/six-paths-impact.js';

const smooth = Mathf.Smooth, clamp = Mathf.Clamp01, lerp = Mathf.Lerp;
const disc = Meshes.disc(32, 'repulse pawn');
const shadowLayer = AltitudeLayer.Shadows.AltitudeFor();
const pale = new Color(.88, .79, 1);
function timing(p) {
  const form = p.gather, load = form + p.form, launch = load + p.load;
  const stop = launch + p.dash, recall = stop + .18;
  return { form, load, launch, stop, recall, end: recall + p.recall + .4 };
}
// Distance only changes along the launch vector; elevation is always zero.
function travel(s, p, t) {
  if (s < t.launch) return -.18 * smooth((s - t.load) / p.load);
  const u = clamp((s - t.launch) / p.dash);
  // Short acceleration, fast middle, gentle braking. Integral is exactly one.
  const f = u < .2 ? 3.125 * u * u : u < .8 ? 1.25 * u - .125 : 1 - 3.125 * (1-u) ** 2;
  return lerp(-.18, p.distance, f);
}
export default {
  kit: 'Six Paths', label: 'Repulse Step (sketch)',
  params: {
    direction: { label: 'Launch direction', value: 'Forward', options: ['Forward', 'Backward'], group: 'Launch' },
    facing: { label: 'Facing direction', value: 'Right', options: ['Right', 'Left', 'Up', 'Down'], group: 'Launch' },
    distance: P('Launch distance (cells)', 5, 2, 8, .25, 'Launch'),
    actors: { label: 'Show moving pawn', value: true, group: 'Showcase' },
    gather: P('Gather two orbs', .55, .25, 1, .05, 'Timing (s)'),
    form: P('Form push surface', .4, .2, .8, .05, 'Timing (s)'),
    load: P('Brace and compress', .4, .2, .8, .05, 'Timing (s)'),
    dash: P('Straight launch', .45, .2, .8, .05, 'Timing (s)'),
    recall: P('Two orbs follow', .65, .3, 1, .05, 'Timing (s)'),
    size: P('Surface half-height (cells)', .9, .65, 1.2, .05, 'Shape'),
    width: P('Surface width (cells)', 2.6, 1.2, 4.5, .1, 'Shape'),
    yaw: P('Surface opening angle (degrees)', 35, 0, 65, 5, 'Shape'),
    rim: P('Frame thickness (cells)', .12, .06, .24, .01, 'Shape'),
    compression: P('Surface compression (cells)', .38, .15, .6, .05, 'Shape'),
    trails: P('Travel trails', .7, 0, 1, .05, 'Impact'),
    shake: P('Release shake', .09, 0, .2, .01, 'Impact'),
  },
  duration(p) { return timing(p).end; },
  phases(p) { const t = timing(p); return [
    { name: 'Two orbs gather', t: 0 }, { name: 'Upright surface', t: t.form },
    { name: 'Brace / compress', t: t.load }, { name: 'Release / level launch', t: t.launch },
    { name: 'Brake', t: t.stop }, { name: 'Two orbs follow', t: t.recall },
  ]; },
  events(p) { return [{ t: timing(p).launch, type: 'shake', value: p.shake }]; },
  draw(s, p, { origin: o, scene }) {
    const t = timing(p);
    if (s < 0 || s > t.end) return;
    const sign = p.direction === 'Backward' ? -1 : 1;
    const [fx, fz] = { Right: [1, 0], Left: [-1, 0], Up: [0, 1], Down: [0, -1] }[p.facing];
    const dx = fx * sign, dz = fz * sign;
    const pos = (along, across = 0, height = 0) => ({
      x: o.x + dx * along - dz * across,
      z: o.z + dz * along + dx * across + height * Lift,
    });
    const sun = scene?.shadowVector ?? { x: -.45, z: -.32 };
    const strength = scene?.sun?.strength ?? .32;
    const gather = smooth(s / p.gather), form = smooth((s - t.form) / p.form);
    const load = smooth((s - t.load) / p.load), age = s - t.launch;
    const release = smooth(age / .13), recall = smooth((s - t.recall) / p.recall);
    const x = travel(s, p, t), pressure = p.compression * load * (1 - release);
    const panelX = -.85, centerH = Math.max(1.05, p.size + .12);
    // One orb supplies each half; both approach the same center and flatten.
    for (let i = 0; i < 2; i++) {
      const side = i ? 1 : -1;
      const a = lerp(.12, panelX, gather), b = side * lerp(.8, .12, gather);
      const q = pos(a, b, lerp(.8, centerH, gather));
      if (form < 1) orb(q, .28 * (1 - form), 1, 1 + form * 1.5);
      if (s < t.load) {
        const pts = Array.from({ length: 16 }, (_, j) => {
          const u = smooth(Math.max(0, s - (1-j/15)*.16) / p.gather);
          return pos(lerp(.12, panelX, u), side*lerp(.8,.12,u), lerp(.8,centerH,u));
        });
        trail('repulse gather '+i, pts, .09, Rim.withAlpha((1-form)*.4));
      }
    }
    // Rounded rectangular pad, turned to reveal its membrane and frame.
    // Center caves away from the pawn during pressure and snaps toward it on release.
    const alpha = 1 - recall, radius = p.size * form * (1 - recall*.8);
    const rebound = age >= 0 ? Math.sin(clamp(age/.26)*Math.PI)*.16 : 0;
    const yaw = p.yaw * Math.PI / 180;
    const point = (r, a, back = 0) => {
      // A superellipse keeps broad straight sides with soft corners. Morph out of
      // the circular source orbs so the rectangular frame grows continuously.
      const c = Math.cos(a), sn = Math.sin(a), exponent = lerp(2, 5, form);
      const outline = 1 / (Math.abs(c)**exponent + Math.abs(sn)**exponent)**(1/exponent);
      const u = c*outline*p.width*.5*form*(1-recall*.8)*r;
      const across = u*Math.cos(yaw);
      const height = centerH + sn*outline*radius*r;
      const along = panelX + u*Math.sin(yaw) + .16*r*r - pressure*(1-r*r) + rebound*(1-r*r) - back;
      return { ...pos(along, across, height), ground: pos(along, across), height };
    };
    if (radius > .001 && alpha > .001) {
      for (const pass of ['shadow', 'back', 'face']) {
        const verts = [], tri = [], rings = 8, segments = 48;
        for (let r = 0; r <= rings; r++) for (let k = 0; k <= segments; k++) {
          const q = point(r/rings, k/segments*Math.PI*2, pass==='back'?p.rim:0);
          verts.push(pass==='shadow'?q.ground.x + sun.x*q.height:q.x,
            pass==='shadow'?q.ground.z + sun.z*q.height:q.z);
          if (r && k) { const n=r*(segments+1)+k; tri.push(n,n-1,n-segments-1,n-1,n-segments-2,n-segments-1); }
        }
        const m=mesh('repulse surface '+pass); m.setFlat(verts,tri);
        const color=pass==='shadow'?new Color(0,0,0,strength*.65*alpha):
          pass==='back'?new Color(.18,.13,.25,alpha):Body.withAlpha(alpha);
        draw(m,0,pass==='shadow'?shadowLayer:Y+(pass==='back'?0:.002),0,1,1,0,color);
      }
      const loop = (r, back=0) => Array.from({length:65},(_,j)=>point(r,j/64*Math.PI*2,back));
      const edge=loop(1), inner=loop(1-p.rim/p.size);
      // Extruded outer wall and broad dark frame surround a recessed elastic face.
      band('repulse frame wall',loop(1,p.rim),edge,new Color(.16,.12,.23,alpha),Y+.004);
      band('repulse frame face',edge,inner,new Color(.09,.065,.135,alpha),Y+.006);
      trail('repulse edge',edge,.028,Rim.withAlpha(alpha*.8),Y+.008);
      trail('repulse inner rim',inner,.022,pale.withAlpha(alpha*.42),Y+.009);
      // Short tension attachments replace the umbrella-like spokes through the center.
      for (let i=0;i<12;i++) {
        const a=i*Math.PI/6;
        const pts=Array.from({length:9},(_,j)=>point(.72+j/8*.2,a));
        trail('repulse tension '+i,pts,.036,Rim.withAlpha(alpha*(.25+load*.35)),Y+.010);
      }
      for (let i=0;i<2;i++) trail('repulse membrane '+i,loop(.38+i*.24),.012,
        Rim.withAlpha(alpha*(.12+load*.16)),Y+.005);
    }
    const contact = pos(panelX-pressure+rebound, 0, centerH);
    if (age>=0 && age<.16) sprite(contact, .65, 1.6, pale.withAlpha((1-age/.16)*.75), glow,Y+.03);

    // The pawn's planted foot shares the deforming surface center until release.
    if (p.actors) {
      const lean = load*(1-release)*.13 + Math.sin(clamp(age/p.dash)*Math.PI)*.2;
      const limb = (key, points, width, color) => trail('repulse pawn '+key, points, width, color, Y+.08);
      const q=pos(x,0,.65), head=pos(x+lean,0,1.18);
      const ground=pos(x);
      sprite({x:ground.x+sun.x*.45,z:ground.z+sun.z*.45},.85,.4,Body.withAlpha(strength),soft,shadowLayer);
      const foot=age<0?pos(lerp(x-.2,panelX-pressure,load),0,lerp(.08,centerH,load)):
        pos(x-.28*(1-smooth(age/.22)),0,.08);
      limb('push leg',[q,pos(x-.24,0,.45),foot],.19,new Color(.23,.25,.29));
      limb('other leg',[q,pos(x+.12,0,.3),pos(x+.15,0,.06)],.18,new Color(.29,.31,.34));
      draw(disc,q.x,Y+.09,q.z,.23,.31,0,new Color(.55,.38,.27));
      limb('arm',[pos(x+lean,0,.9),pos(x+.27,0,.7),pos(x+.31,0,.82)],.14,new Color(.72,.57,.42));
      draw(disc,head.x,Y+.10,head.z,.16,.18,0,new Color(.83,.70,.54));
      // Facing marker stays fixed when Backward is selected.
      const nose={x:head.x+fx*.14,z:head.z+fz*.14};
      draw(disc,nose.x,Y+.11,nose.z,.065,.06,0,new Color(.88,.75,.59));
    }
    if (age>=0) {
      for (let i=0;i<3;i++) {
        const pts=Array.from({length:24},(_,j)=>pos(travel(Math.max(t.launch,s-(1-j/23)*.19),p,t), (i-1)*.18,.45+i*.22));
        trail('repulse slipstream '+i,pts,.07,pale.withAlpha(p.trails*.38*(1-smooth((s-t.stop)/.22))),Y+.02);
      }
      // Dust stays at sampled ground emission positions instead of riding the pawn.
      for (let i=0;i<22;i++) {
        const emitted=t.launch+i/22*p.dash, u=(s-emitted)/.48;
        if (u<0 || u>1) continue;
        const q=pos(travel(emitted,p,t)-u*.18,(rand(i+44)-.5)*(.45+u*.8));
        sprite(q,.22+u*.45,.16+u*.3,new Color(.56,.49,.40,Math.sin(u*Math.PI)*.26),soft,shadowLayer+.02);
      }
    }
    if (recall>0) for (let i=0;i<2;i++) {
      const side=i?1:-1;
      const q=pos(lerp(panelX,p.distance-.35,recall), side*lerp(.1,.65,recall),centerH);
      orb(q,.28*recall);
      const tail=Array.from({length:16},(_,j)=>{
        const u=smooth((s-(1-j/15)*.12-t.recall)/p.recall);
        return pos(lerp(panelX,p.distance-.35,u),side*lerp(.1,.65,u),centerH);
      });
      trail('repulse recall '+i,tail,.065,Rim.withAlpha((1-recall)*.5));
    }
  },
};
