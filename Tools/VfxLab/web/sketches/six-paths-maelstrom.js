// Devouring Maelstrom — VFX-only proposal, separate from Gravity Well.
// Defaults: prepare 0–0.35s, shoot to 1.2s, gather to 2.25s, open to 2.7s,
// devour to 5.1s, sequential arm collapse to 5.85s, dust settles by 6.5s.
// Three orbs become the three arms; none return in this visual proposal.
import { AltitudeLayer, Color, Mathf, Meshes } from '../js/engine.js';
import { draw, Lift } from './lib/six-paths-solid.js';
import { P, Body, Rim, Y, orb, sprite, trail, band, soft, glow, rand } from './lib/six-paths-impact.js';

const smooth=Mathf.Smooth, clamp=Mathf.Clamp01, lerp=Mathf.Lerp, tau=Math.PI*2;
const disc=Meshes.disc(64,'maelstrom throat'), floor=AltitudeLayer.Shadows.AltitudeFor();
const pale=new Color(.88,.77,1), dust=new Color(.48,.41,.34);
const prepare=.35, opening=.45, settle=.65;
function timing(p) {
  const gather=prepare+p.shoot, open=gather+p.gather, hold=open+opening;
  const collapse=hold+p.hold, snap=collapse+p.collapse;
  return {gather,open,hold,collapse,snap,end:snap+settle};
}
// The same rotating phase drives the arriving orbs and the spiral arms.
const spin=(s,t,p)=>p.spin*(s-t.gather);
function pose(s,i,p,t) {
  const a=i*tau/3+spin(s,t,p);
  const ring={x:Math.cos(a)*p.radius,z:Math.sin(a)*p.radius*.55,h:1.6};
  const start={x:-p.reach-.3*Math.cos(i*2),z:(i-1)*.85,h:1.15};
  if(s<prepare)return {...start,h:start.h+.25*smooth(s/prepare)};
  if(s<t.gather) {
    const u=clamp((s-prepare)/p.shoot), v=u*u*(2-u), bend=Math.sin(u*Math.PI);
    const endAngle=i*tau/3;
    return {x:lerp(start.x,Math.cos(endAngle)*p.radius,v),
      z:lerp(start.z,Math.sin(endAngle)*p.radius*.55,v)+(i-1)*bend*1.5,
      h:lerp(1.4,1.6,v)+bend*.7};
  }
  const u=smooth((s-t.gather)/p.gather), r=lerp(1,.32,u);
  return {x:ring.x*r,z:ring.z*r,h:lerp(1.6,.7,u)};
}
export default {
  kit:'Six Paths',label:'Devouring Maelstrom (sketch)',
  params:{
    radius:P('Mouth radius (cells)',2.7,1.7,4,.1,'Shape'),
    reach:P('Launch distance (cells)',6,4,8,.25,'Shape'),
    depth:P('Funnel depth (cells)',2.1,1,3.5,.1,'Shape'),
    spin:P('Rotation (radians/s)',3.2,1.5,5,.1,'Motion'),
    shoot:P('Shoot',.85,.4,1.5,.05,'Timing (s)'),
    gather:P('Gather',1.05,.5,2,.05,'Timing (s)'),
    hold:P('Devour',2.4,1,4,.1,'Timing (s)'),
    collapse:P('Collapse',.75,.45,1.2,.05,'Timing (s)'),
  },
  duration(p){return timing(p).end;},
  phases(p){const t=timing(p);return [
    {name:'Three orbs',t:0},{name:'Shoot',t:prepare},{name:'Gather',t:t.gather},
    {name:'Open throat',t:t.open},{name:'Devour',t:t.hold},
    {name:'Fold inward',t:t.collapse},{name:'Final snap',t:t.snap},
  ];},
  events(p){const t=timing(p);return [
    {t:t.hold,type:'shake',value:.12},{t:t.snap,type:'shake',value:.17},
  ];},
  draw(s,p,{origin:o,scene}) {
    const t=timing(p);
    if(s<0 || s>=t.end)return;
    const sun=scene?.shadowVector??{x:-.45,z:-.32}, strength=scene?.sun?.strength??.32;
    const project=q=>({x:o.x+q.x,z:o.z+q.z+q.h*Lift});
    const cast=q=>({x:o.x+q.x+sun.x*q.h,z:o.z+q.z+sun.z*q.h});
    const point=(x,z,h=0)=>project({x,z,h});
    const opened=smooth((s-t.open)/opening), closed=smooth((s-t.collapse)/p.collapse);
    const alive=1-smooth((s-t.snap+.12)/.12);

    // Three discrete spheres develop long curved blades before joining the throat.
    for(let i=0;i<3;i++) {
      const fade=1-smooth((s-t.open)/(.22+i*.045));
      if(fade<=0)continue;
      const q=pose(Math.min(s,t.open),i,p,t), pts=[];
      const span=lerp(.20,.44,smooth((s-t.gather)/p.gather));
      for(let j=0;j<40;j++)pts.push(project(pose(Math.max(0,Math.min(s,t.open)-(1-j/39)*span),i,p,t)));
      trail('maelstrom launch rim '+i,pts,.20+smooth((s-t.gather)/p.gather)*.26,Rim.withAlpha(fade*.8),Y+.03);
      trail('maelstrom launch mass '+i,pts,.14+smooth((s-t.gather)/p.gather)*.22,Body.withAlpha(fade),Y+.032);
      sprite(cast(q),.8,.48,Body.withAlpha(strength*fade*.7),soft,floor);
      orb(project(q),.27,fade,1,Y+.04);
    }

    if(s>=t.open && s<t.snap) {
      const size=lerp(.32,1,opened)*(1-closed*.97);
      const r=p.radius*size, h=p.depth*opened*(1-closed);
      // An offset throat plus layered conical surfaces makes the near wall overlap
      // the hollow. Surfaces are dark and opaque, not stacked luminous rings.
      const surface=(u,a)=>({x:Math.cos(a)*r*u,
        z:Math.sin(a)*r*u*.55-.42*(1-u)*size,h:.25+h*u});
      const shadowOuter=[],shadowInner=[];
      for(let j=0;j<=80;j++) {
        const a=j/80*tau;
        shadowOuter.push(cast(surface(1,a)));shadowInner.push(cast(surface(.04,a)));
      }
      band('maelstrom bowl shadow',shadowOuter,shadowInner,Body.withAlpha(strength*.48*alive),floor);
      for(let k=0;k<10;k++) {
        const u0=.10+k*.09,u1=u0+.092,outer=[],inner=[];
        for(let j=0;j<=80;j++) {
          const a=j/80*tau;outer.push(project(surface(u1,a)));inner.push(project(surface(u0,a)));
        }
        band('maelstrom bowl '+k,outer,inner,
          new Color(.013+k*.004,.010+k*.003,.021+k*.006,alive),Y+k*.001);
      }
      const throat=project(surface(0,0));
      draw(disc,throat.x,Y+.012,throat.z,r*.22,r*.14,0,new Color(.002,.001,.004,alive));

      // Exactly three broad ribbons, each reaching from the throat to a hooked tip.
      // Stagger their contraction, so the ending folds rather than simply fading.
      for(let i=0;i<3;i++) {
        const fold=smooth((s-t.collapse-i*p.collapse*.16)/(p.collapse*.68));
        const outer=[],inner=[],edge=[],shine=[],shadowA=[],shadowB=[];
        for(let j=0;j<=72;j++) {
          const u=j/72, radial=(.12+.99*u)*(1-fold*.8);
          const a=i*tau/3+spin(s,t,p)-3.8*(1-u)+fold*2;
          const width=Math.sin(Math.PI*u)**.7*(.22+.09*u)*opened;
          const qa=surface(radial,a+width),qb=surface(radial*.96,a-width);
          outer.push(project(qa));inner.push(project(qb));edge.push(project(qa));
          shadowA.push(cast(qa));shadowB.push(cast(qb));
          if(j>25 && j<59)shine.push(project(qa));
        }
        band('maelstrom arm shadow '+i,shadowA,shadowB,Body.withAlpha(strength*.4*alive),floor+.001);
        band('maelstrom arm '+i,outer,inner,new Color(.065+i*.009,.048+i*.006,.088+i*.012,alive),Y+.022+i*.006);
        trail('maelstrom arm edge '+i,edge,.034*size,Rim.withAlpha(alive*.85),Y+.024+i*.006);
        trail('maelstrom arm sheen '+i,shine,.018*size,pale.withAlpha(alive*(.30+.18*Math.sin(s*6+i))),Y+.025+i*.006);
      }
      // Near lip occludes the bottom of the throat: thickness, not a halo.
      const lip=[],under=[];
      for(let j=0;j<=40;j++) {
        const a=Math.PI+j/40*Math.PI, q=surface(.96,a);
        lip.push(project(q));under.push(project({...q,h:q.h-.24*size}));
      }
      band('maelstrom front thickness',lip,under,Body.withAlpha(alive),Y+.048);
      trail('maelstrom front edge',lip,.045*size,Rim.withAlpha(alive*.6),Y+.049);
    }

    // Dust starts on the ground, curls inward, then visibly lifts into the mouth.
    // Each parcel is derived from its emission time, including its sampled trail.
    for(let i=0;i<60;i++) {
      const period=.9+rand(i+40)*.6, start=t.open+rand(i+100)*period;
      const cycle=Math.floor((s-start)/period), emitted=start+cycle*period;
      const u=(s-emitted)/period;
      if(cycle<0 || emitted>t.collapse || u<0 || u>1 || s>=t.snap)continue;
      const path=v=>{
        const a=i*2.399+v*v*3.2, r=lerp(p.radius+1+rand(i+200)*2,.18,v*v);
        return point(Math.cos(a)*r,Math.sin(a)*r*.65,
          Math.sin(v*Math.PI)*p.depth*.85);
      };
      const pts=Array.from({length:14},(_,j)=>path(Math.max(0,u-(1-j/13)*.11)));
      const alpha=Math.sin(u*Math.PI)*alive;
      trail('maelstrom intake '+i,pts,i%4===0?.045:.023,
        (i%4===0?Rim:dust).withAlpha(alpha*.5),Y+.017);
      sprite(path(u),.10+(1-u)*.13,.075+(1-u)*.1,dust.withAlpha(alpha*.65),soft,Y+.018);
    }
    const snap=s-t.snap;
    if(snap>=0) {
      const flash=1-clamp(snap/.14), q=point(0,-.42,.25);
      sprite(q,.20+flash*.25,.4+flash*2.7,pale.withAlpha(flash),glow,Y+.07);
      for(let i=0;i<24;i++) {
        const a=i*tau/24, u=clamp(snap/settle), r=.4+u*(1.6+rand(i+300)*.65);
        sprite(point(Math.cos(a)*r,Math.sin(a)*r*.65),.25+u*.5,.16+u*.3,
          dust.withAlpha(Math.sin(u*Math.PI)*.30),soft,floor+.005);
      }
    }
  },
};
