// Devouring Maelstrom — VFX-only proposal, separate from Gravity Well.
// Spinning-top revision: three orbs launch, gather, and lock into hooked blades.
// Defaults: lock and accelerate at 2.25s, grind from 2.9s until 5.3s,
// retract blades and snap shut at 6.05s; dust clears by 6.7s. VFX only.
import { AltitudeLayer, Color, Mathf, Meshes } from '../js/engine.js';
import { draw, Lift } from './lib/six-paths-solid.js';
import { P, Body, Rim, Y, orb, sprite, trail, band, soft, glow, rand } from './lib/six-paths-impact.js';

const smooth=Mathf.Smooth, clamp=Mathf.Clamp01, lerp=Mathf.Lerp, tau=Math.PI*2;
const disc=Meshes.disc(64,'maelstrom throat'), floor=AltitudeLayer.Shadows.AltitudeFor();
const pale=new Color(.88,.77,1), dust=new Color(.48,.41,.34);
const prepare=.35, opening=.65, settle=.65;
function timing(p) {
  const gather=prepare+p.shoot, open=gather+p.gather, hold=open+opening;
  const collapse=hold+p.hold, snap=collapse+p.collapse;
  return {gather,open,hold,collapse,snap,end:snap+settle};
}
// The same rotating phase drives the arriving orbs and the spiral arms.
function spin(s,t,p) {
  if(s<t.open)return 2.4*(s-t.gather);
  const age=s-t.open, ramp=.95, u=Math.min(age,ramp);
  return 2.4*(t.open-t.gather)+.8*age+(p.speed-.8)*(u*u/(2*ramp)+Math.max(0,age-ramp));
}
// A shallow tilted rotor plane; true height also drives geometry shadows.
function rotor(s,t,p) {
  const open=smooth((s-t.open)/opening), close=smooth((s-t.collapse)/p.collapse);
  const tilt=.12+Math.sin(s*5)*p.wobble, lean=Math.cos(s*4.1)*p.wobble;
  const safeTilt=Math.min(1,(p.hover-.24)/(p.radius*Math.hypot(tilt,lean)));
  return {x:.20*Math.sin((s-t.open)*2)*open*(1-close),
    z:.16*Math.sin((s-t.open)*2.7)*open*(1-close),
    h:p.hover, tilt:tilt*safeTilt,
    lean:lean*safeTilt, scale:lerp(.32,1,open)*(1-close*.96)};
}
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
    radius:P('Blade reach (cells)',2.7,1.7,4,.1,'Shape'),
    reach:P('Launch distance (cells)',6,4,8,.25,'Shape'),
    hover:P('Rotor height (cells)',.85,.65,1.2,.05,'Shape'),
    wobble:P('Tilt wobble',.065,0,.10,.005,'Motion'),
    speed:P('Full spin (radians/s)',9,5,14,.25,'Motion'),
    shoot:P('Shoot',.85,.4,1.5,.05,'Timing (s)'),
    gather:P('Gather',1.05,.5,2,.05,'Timing (s)'),
    hold:P('Devour',2.4,1,4,.1,'Timing (s)'),
    collapse:P('Collapse',.75,.45,1.2,.05,'Timing (s)'),
  },
  duration(p){return timing(p).end;},
  phases(p){const t=timing(p);return [
    {name:'Three orbs',t:0},{name:'Shoot',t:prepare},{name:'Gather',t:t.gather},
    {name:'Lock blades',t:t.open},{name:'Spin and grind',t:t.hold},
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

    // Three discrete spheres develop long curved trails before becoming the rotor.
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
      const state=rotor(s,t,p), size=state.scale, r=p.radius*size;
      const surface=(radius,a,lower=0)=>{
        const x=Math.cos(a)*r*radius,z=Math.sin(a)*r*radius;
        return {x:state.x+x,z:state.z+z*.70,
          h:state.h+x*state.tilt+z*state.lean-lower*size};
      };
      // A short pointed underside anchors the rotor above the ground.
      const rim=[],tip=[],hubOuter=[],hubInner=[],hubLow=[];
      for(let j=0;j<=64;j++) {
        const a=j/64*tau;
        rim.push(project(surface(.38,a,.16)));
        tip.push(project({x:state.x,z:state.z,h:.10}));
        hubOuter.push(project(surface(.40,a)));
        hubInner.push(project(surface(.18,a)));
        hubLow.push(project(surface(.18,a,.18)));
      }
      band('maelstrom pointed underside',rim,tip,new Color(.035,.026,.051,alive),Y+.004);
      // Front half of the pointed underside catches a muted violet reflection.
      band('maelstrom tip facet',rim.slice(32),tip.slice(32),new Color(.11,.075,.15,alive),Y+.005);
      band('maelstrom hub',hubOuter,hubInner,new Color(.10,.075,.14,alive),Y+.025);
      band('maelstrom hollow wall',hubInner,hubLow,Body.withAlpha(alive),Y+.026);
      const centre=project({x:state.x,z:state.z,h:state.h-.18*size});
      draw(disc,centre.x,Y+.027,centre.z,r*.178,r*.125,0,new Color(.001,.001,.003,alive));
      trail('maelstrom hollow rim',hubInner,.027*size,Rim.withAlpha(alive*.75),Y+.028);

      // Broad swept blades leave deep gaps: the silhouette stays visibly three-sided.
      for(let i=0;i<3;i++) {
        const fold=smooth((s-t.collapse-i*p.collapse*.14)/(p.collapse*.72));
        const outer=[],inner=[],edge=[],lower=[],shadowA=[],shadowB=[];
        for(let j=0;j<=56;j++) {
          const u=j/56, radial=lerp(.32,1,u)*(1-fold*.65);
          const a=i*tau/3+spin(s,t,p)-1.5*(1-u)+fold*1.4;
          const width=(.10+.37*Math.sin(Math.PI*u)**.65)*(1-u**8);
          const qa=surface(radial,a+width),qb=surface(radial,a-width);
          outer.push(project(qa));inner.push(project(qb));
          edge.push(project(qa));lower.push(project({...qa,h:qa.h-.19*size}));
          shadowA.push(cast(qa));shadowB.push(cast(qb));
        }
        band('maelstrom blade shadow '+i,shadowA,shadowB,Body.withAlpha(strength*.65*alive),floor+.001);
        band('maelstrom blade thickness '+i,outer,lower,new Color(.14,.10,.19,alive),Y+.030+i*.005);
        band('maelstrom blade '+i,outer,inner,new Color(.041+i*.006,.033+i*.004,.061+i*.008,alive),Y+.031+i*.005);
        trail('maelstrom blade bevel '+i,edge,.045*size,Rim.withAlpha(alive*.9),Y+.032+i*.005);
        trail('maelstrom blade inner '+i,inner,.018*size,pale.withAlpha(alive*.28),Y+.033+i*.005);
        // Pose-sampled tip arcs follow acceleration, wobble and drift.
        const wake=[];
        for(let j=0;j<=32;j++) {
          const when=Math.max(t.open,s-(1-j/32)*.105), past=rotor(when,t,p);
          const a=i*tau/3+spin(when,t,p), rr=p.radius*past.scale;
          const x=Math.cos(a)*rr,z=Math.sin(a)*rr;
          wake.push(project({x:past.x+x,z:past.z+z*.7,
            h:past.h+x*past.tilt+z*past.lean}));
        }
        trail('maelstrom blade wake '+i,wake,.095*size,
          Rim.withAlpha(alive*opened*(1-closed)*.36),Y+.019);
      }
      // Scraping dust and short sparks come from the spinning point, then stay
      // at their emission position while drifting outward.
      for(let i=0;i<36;i++) {
        const period=.42, start=t.hold+i/36*period;
        const cycle=Math.floor((s-start)/period), emitted=start+cycle*period;
        const age=s-emitted,u=age/period;
        if(cycle<0 || emitted>t.collapse || u<0 || u>1)continue;
        const old=rotor(emitted,t,p), a=spin(emitted,t,p)+i*2.399;
        const pos=v=>point(old.x+Math.cos(a)*(.15+v*1.5),
          old.z+Math.sin(a)*(.15+v*1.5)*.65,Math.sin(v*Math.PI)*.12);
        sprite(pos(u),.12+u*.45,.10+u*.25,dust.withAlpha(Math.sin(u*Math.PI)*.42),soft,floor+.008);
        if(i%3===0)trail('maelstrom grind spark '+i,
          [pos(Math.max(0,u-.09)),pos(u),pos(Math.min(1,u+.04))],.027,
          pale.withAlpha((1-u)*.75),Y+.002);
      }
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
          p.hover*smooth(v));
      };
      const pts=Array.from({length:14},(_,j)=>path(Math.max(0,u-(1-j/13)*.11)));
      const alpha=Math.sin(u*Math.PI)*alive;
      trail('maelstrom intake '+i,pts,i%4===0?.045:.023,
        (i%4===0?Rim:dust).withAlpha(alpha*.5),Y+.017);
      sprite(path(u),.10+(1-u)*.13,.075+(1-u)*.1,dust.withAlpha(alpha*.65),soft,Y+.018);
    }
    const snap=s-t.snap;
    if(snap>=0) {
      const flash=1-clamp(snap/.14), q=point(0,0,p.hover);
      sprite(q,.20+flash*.25,.4+flash*2.7,pale.withAlpha(flash),glow,Y+.07);
      for(let i=0;i<24;i++) {
        const a=i*tau/24, u=clamp(snap/settle), r=.4+u*(1.6+rand(i+300)*.65);
        sprite(point(Math.cos(a)*r,Math.sin(a)*r*.65),.25+u*.5,.16+u*.3,
          dust.withAlpha(Math.sin(u*Math.PI)*.30),soft,floor+.005);
      }
    }
  },
};
