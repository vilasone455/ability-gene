// Repulse Step — VFX-only rope-sling proposal. Two orbs become padded posts,
// five ropes load against the pawn's back, then snap into a level launch.
// Defaults: gather 0–0.55s, posts/ropes form to 0.95s, brace to 1.35s,
// launch to 1.80s, then the posts reform into two following orbs.
// The orbs and the sage: the launched pawn is the sage and carries the six orbs on its ring
// (lib/six-paths-sage.js); the ring travels with it. Slots 0 and 1 leave the ring at 0.09 radius
// and grow to the field cast radius 0.30 on the way to the post anchors (the posts are planted in
// the ground, not held on the pawn). Both slots stay empty until the posts reform into orbs, which
// fly after the sage, shrink to 0.09 and sit in their slots where it stopped.
// Side facings keep the true north-south span. Height and span share the screen
// axis there, so the ropes fan along the launch axis by height to stay separate.
import { AltitudeLayer, Color, Mathf, Meshes } from '../js/engine.js';
import { draw, Lift } from './lib/six-paths-solid.js';
import { P, Body, Rim, Y, orb, sprite, trail, band, glow, soft, rand } from './lib/six-paths-impact.js';
import { sage, carried, slot, deployRadius, Field, Facings } from './lib/six-paths-sage.js';

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
  const braced=-.85-p.compression+.12;
  if (s < t.launch) return braced * smooth((s - t.load) / p.load);
  const u = clamp((s - t.launch) / p.dash);
  // Short acceleration, fast middle, gentle braking. Integral is exactly one.
  const f = u < .2 ? 3.125 * u * u : u < .8 ? 1.25 * u - .125 : 1 - 3.125 * (1-u) ** 2;
  return lerp(braced, p.distance, f);
}
export default {
  kit: 'Six Paths', label: 'Repulse Step (sketch)',
  params: {
    direction: { label: 'Launch direction', value: 'Forward', options: ['Forward', 'Backward'], group: 'Launch' },
    facing: { label: 'Facing direction', value: 'Right', options: ['Right', 'Left', 'Up', 'Down'], group: 'Launch' },
    distance: P('Launch distance (cells)', 5, 2, 8, .25, 'Launch'),
    actors: { label: 'Show moving pawn', value: true, group: 'Showcase' },
    gather: P('Gather two orbs', .55, .25, 1, .05, 'Timing (s)'),
    form: P('Form posts and ropes', .4, .2, .8, .05, 'Timing (s)'),
    load: P('Brace and compress', .4, .2, .8, .05, 'Timing (s)'),
    dash: P('Straight launch', .45, .2, .8, .05, 'Timing (s)'),
    recall: P('Two orbs follow', .65, .3, 1, .05, 'Timing (s)'),
    size: P('Post half-height (cells)', .9, .65, 1.2, .05, 'Shape'),
    width: P('Post spacing (cells)', 2.6, 1.2, 4.5, .1, 'Shape'),
    rim: P('Pad thickness (cells)', .26, .16, .4, .02, 'Shape'),
    compression: P('Rope stretch (cells)', .38, .15, .6, .05, 'Shape'),
    trails: P('Travel trails', .7, 0, 1, .05, 'Impact'),
    shake: P('Release shake', .09, 0, .2, .01, 'Impact'),
  },
  duration(p) { return timing(p).end; },
  phases(p) { const t = timing(p); return [
    { name: 'Two orbs gather', t: 0 }, { name: 'Posts and ropes', t: t.form },
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
    // Side view: posts stand on their true cells. Ropes fan rearward by height
    // (see fan below) and draw over the posts so all five ends stay visible.
    const sideView=Math.abs(dx), contactH=.65;
    const sling=(along,across=0,height=0)=>{
      const ground=pos(along,across);
      return {x:ground.x,
        z:ground.z+height*Lift,ground,height};
    };
    const shadow=q=>({x:q.ground.x+sun.x*q.height,z:q.ground.z+sun.z*q.height});
    const alpha=1-recall, growth=form*(1-recall);
    const rebound=age>=0?Math.sin(clamp(age/.28)*Math.PI)*.24*Math.exp(-age*4):0;
    const half=p.width*.5, anchorAlong=panelX, postTop=centerH+p.size;
    const anchor=(side,h=centerH)=>sling(anchorAlong,side*half,h);
    // The ring rides with the sage. Slots 0 and 1 are the posts.
    const feet=pos(x);
    carried(feet,s,scene,(i)=>i<2);
    // Each orb travels from its slot to its own anchor, then lengthens into a padded post.
    for(let i=0;i<2;i++) {
      const side=i?1:-1, end=sling(panelX,side*half,centerH);
      const start=slot(feet,i,s);
      const q={x:lerp(start.x,end.x,gather),z:lerp(start.z,end.z,gather)};
      if(form<1)orb(q,deployRadius(gather,Field)*(1-form),1,1+form*2,gather>0?Y:start.layer);
      if(s<t.load) {
        const pts=Array.from({length:18},(_,j)=>{
          const u=smooth(Math.max(0,s-(1-j/17)*.16)/p.gather);
          return {x:lerp(start.x,end.x,u),z:lerp(start.z,end.z,u)};
        });
        trail('repulse gather '+i,pts,.085,Rim.withAlpha((1-form)*.5));
      }
      if(growth>.001) {
        const left=[],right=[],inset=[],sidewall=[],shadowA=[],shadowB=[];
        for(let j=0;j<=24;j++) {
          const u=j/24, h=lerp(centerH,u*postTop,growth);
          const q=anchor(side,h), bulge=p.rim*(.72+.28*Math.sin(u*Math.PI));
          // Pad width is screen-space on purpose; a side view must retain mass.
          const a={x:q.x-bulge*.5,z:q.z},b={x:q.x+bulge*.5,z:q.z};
          left.push(a);right.push(b);inset.push({x:a.x+.035,z:a.z});
          sidewall.push({x:b.x+.075*growth,z:b.z-.045*growth});
          const sh=shadow(q);shadowA.push({x:sh.x-bulge*.5,z:sh.z});shadowB.push({x:sh.x+bulge*.5,z:sh.z});
        }
        band('repulse post shadow '+i,shadowA,shadowB,Body.withAlpha(strength*alpha*.65),shadowLayer);
        band('repulse pad thickness '+i,right,sidewall,new Color(.16,.12,.22,alpha),Y+.018);
        band('repulse pad '+i,left,right,Body.withAlpha(alpha),Y+.020);
        band('repulse pad bevel '+i,left,inset,Rim.withAlpha(alpha*.75),Y+.022);
        // The post reaches a fixed ground socket. A dense contact patch and
        // flared foot join the pad to the terrain, including the cheated side view.
        const planted=smooth((form-.35)/.65)*alpha, base=anchor(side,0);
        // Side view: the far foot lands inside the rope fan, so both feet shrink.
        const foot=sideView?.6:1, flare=p.rim*.80*foot;
        sprite(base,.72*foot,.38*foot,Body.withAlpha(strength*.85*planted),soft,shadowLayer+.004);
        draw(disc,base.x,shadowLayer+.005,base.z,p.rim*1.04*foot,.12*foot,0,
          new Color(.018,.014,.024,planted));
        const footLow=anchor(side,.025),footHigh=anchor(side,.22);
        band('repulse planted foot '+i,
          [{x:footLow.x-flare,z:footLow.z},{x:footHigh.x-p.rim*.43,z:footHigh.z}],
          [{x:footLow.x+flare,z:footLow.z},{x:footHigh.x+p.rim*.43,z:footHigh.z}],
          new Color(.12,.085,.17,planted),Y+.025);
        trail('repulse foot lip '+i,
          [{x:footLow.x-flare,z:footLow.z},{x:footLow.x,z:footLow.z+.025},
            {x:footLow.x+flare,z:footLow.z}],.032,Rim.withAlpha(planted*.6),Y+.026);
        // Small violet bindings make the ends feel padded rather than metallic.
        for(let k=0;k<2;k++) {
          const h=lerp(centerH,postTop*(k?.90:.12),growth),q=anchor(side,h);
          trail('repulse pad binding '+i+' '+k,
            [{x:q.x-p.rim*.48,z:q.z},{x:q.x,z:q.z+.016},{x:q.x+p.rim*.48,z:q.z}],
            .048,Rim.withAlpha(alpha*.5),Y+.024);
        }
      }
    }
    if(growth>.001)for(let i=0;i<5;i++) {
      const pts=[],shadowPts=[],highlight=[];
      // Keep one rope at the pawn's back and fill the formerly empty upper pad.
      const ropeH=i===0?.23:i===1?contactH:lerp(contactH,postTop-.16,(i-1)/3);
      const h=lerp(centerH,ropeH,growth);
      for(let j=0;j<=48;j++) {
        const u=j/48, across=(u*2-1)*half;
        const bow=Math.sin(u*Math.PI)**1.6;
        // Endpoints stay attached; the loaded centre tracks the pawn's back.
        const vibration=age>0?Math.sin(u*Math.PI*3)*Math.sin(age*38-i*.7)*.07*Math.exp(-age*7):0;
        const fan=sideView?(h-contactH)*.30*Math.sin(u*Math.PI):0;
        const along=anchorAlong-fan-(pressure-rebound)*bow+vibration;
        const q=sling(along,across,h-.035*Math.sin(u*Math.PI)*(1-load));
        pts.push(q);shadowPts.push(shadow(q));highlight.push({x:q.x,z:q.z+.020});
      }
      trail('repulse rope shadow '+i,shadowPts,.072,Body.withAlpha(strength*alpha*.6),shadowLayer+.002);
      const ropeY=Y+(sideView?.030:.006)+i*.001;
      trail('repulse rope edge '+i,pts,.085*growth,Rim.withAlpha(alpha*.85),ropeY);
      trail('repulse rope body '+i,pts,.050*growth,Body.withAlpha(alpha),ropeY+.001);
      trail('repulse rope shine '+i,highlight,.015*growth,pale.withAlpha(alpha*(.24+load*.32)),ropeY+.002);
    }
    const contact=sling(panelX-pressure+rebound,0,contactH);
    if(age>=0 && age<.16)sprite(contact,.7,.85,pale.withAlpha((1-age/.16)*.75),glow,Y+.03);

    // The sage stands with its back to the central rope. Its face keeps the facing when the
    // launch is Backward.
    if (p.actors) sage(feet, Facings[{ Right: 'East', Left: 'West', Up: 'North', Down: 'South' }[p.facing]], scene);
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
      const side=i?1:-1, start=sling(panelX,side*half,centerH);
      const end=slot(feet,i,s);
      const q={x:lerp(start.x,end.x,recall),z:lerp(start.z,end.z,recall)};
      orb(q,deployRadius(1-recall,Field)*Math.min(1,recall*4),1,1,recall<1?Y:end.layer);
      const tail=Array.from({length:16},(_,j)=>{
        const u=smooth((s-(1-j/15)*.12-t.recall)/p.recall);
        return {x:lerp(start.x,end.x,u),z:lerp(start.z,end.z,u)};
      });
      trail('repulse recall '+i,tail,.065,Rim.withAlpha((1-recall)*.5));
    }
  },
};
