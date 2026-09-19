// Devouring Current — VFX-only proposal: prepare 0–0.4s, pull 0.4–1.9s,
// hold visible actors 1.9–2.35s, fire outward 2.35–2.75s, settle by 3.4s.
// One orb survives. Pawn and bullets are scripted stand-ins, not mechanics.
import { AltitudeLayer, Color, Mathf, Meshes } from '../js/engine.js';
import { draw, Lift } from './lib/six-paths-solid.js';
import { P, Body, Rim, Y, orb, sprite, trail, soft, glow, rand } from './lib/six-paths-impact.js';

const clamp=Mathf.Clamp01, smooth=Mathf.Smooth, lerp=Mathf.Lerp;
const pale=new Color(.88,.81,1), dust=new Color(.56,.49,.40);
const disc=Meshes.disc(40,'devouring actor'), shadows=AltitudeLayer.Shadows.AltitudeFor();
const prepare=.4, settle=.65, capture=1.05, hover=1.05;
function timing(p) {
  const hold=prepare+p.pull, release=hold+p.hold, stop=release+p.release;
  return {hold,release,stop,end:stop+settle};
}
// Shared by actors and history-sampled trails; no persistent simulation state.
function along(s,p,t,start,held) {
  if(s<t.hold) return lerp(start,held,clamp((s-prepare)/p.pull)**1.65);
  if(s<t.release) return held;
  return lerp(held,start,1-(1-clamp((s-t.release)/p.release))**2);
}
export default {
  kit:'Six Paths', label:'Devouring Current (sketch)',
  params:{
    facing:{label:'Direction',value:'Right',options:['Right','Left','Up','Down'],group:'Shape'},
    reach:P('Reach (cells)',6,3,9,.25,'Shape'),
    width:P('Funnel mouth width (cells)',2,1.4,3.5,.1,'Shape'),
    pull:P('Pull',1.5,.5,3,.05,'Timing (s)'),
    hold:P('Hold in front',.45,.15,1.5,.05,'Timing (s)'),
    release:P('Outward release',.4,.2,.9,.05,'Timing (s)'),
    wind:P('Wind intensity',.8,0,1,.05,'Look'),
    actors:{label:'Show pawn and bullets',value:true,group:'Showcase'},
  },
  duration(p){return timing(p).end;},
  phases(p){const t=timing(p);return [
    {name:'One orb prepares',t:0},{name:'Pull inward',t:prepare},
    {name:'Hold in front',t:t.hold},{name:'Fire outward',t:t.release},{name:'Settle',t:t.stop},
  ];},
  events(p){return [{t:timing(p).release,type:'shake',value:.065}];},
  draw(s,p,{origin:o,scene}) {
    const t=timing(p);
    if(s<0 || s>t.end)return;
    const [dx,dz]={Right:[1,0],Left:[-1,0],Up:[0,1],Down:[0,-1]}[p.facing];
    const pos=(a,b=0,h=0)=>({x:o.x+dx*a-dz*b,z:o.z+dz*a+dx*b+h*Lift});
    const sun=scene?.shadowVector??{x:-.45,z:-.32}, strength=scene?.sun?.strength??.32;
    const shadow=(a,b,h,w,d,alpha=1)=>{
      const q=pos(a,b);
      sprite({x:q.x+sun.x*h,z:q.z+sun.z*h},w,d,Body.withAlpha(strength*alpha),soft,shadows);
    };
    const age=s-t.release, pressure=smooth((s-prepare)/.18)*(1-smooth(age/.1));
    const tailFade=1-smooth((s-t.stop)/settle);
    const recoil=age>=0?-.19*Math.sin(clamp(age/.3)*Math.PI):0;
    const orbX=-.3*(1-smooth(s/prepare))+recoil;

    // Axial parcels accelerate inward, with a shallow funnel and slight local curl.
    for(let i=0;i<62;i++) {
      const life=.5+rand(i+21)*.28, period=life+.08;
      const clock=s-prepare-rand(i+81)*period, cycle=Math.floor(clock/period);
      const emitted=prepare+rand(i+81)*period+cycle*period, u=(s-emitted)/life;
      if(cycle<0 || s>=t.release || u<0 || u>1)continue;
      const lane=(rand(i+140)-.5)*2;
      const path=v=>{
        const a=lerp(p.reach,.38,clamp(v)**1.65), funnel=.2+.8*smooth(a/p.reach);
        return pos(a,lane*p.width*.5*funnel+Math.sin(v*7+i)*.065*funnel,hover+Math.sin(i*2.4)*.16);
      };
      const pts=Array.from({length:12},(_,j)=>path(Math.max(0,u-(1-j/11)*.19)));
      trail('devouring wind '+i,pts,i%4===0?.065:.026,
        (i%3===0?pale:Rim).withAlpha(Math.sin(u*Math.PI)*p.wind*.48),Y+.018);
    }
    for(let i=0;i<26;i++) {
      const emitted=prepare+i/26*p.pull, u=(s-emitted)/.65;
      if(u<0 || u>1 || s>=t.release)continue;
      const a=lerp(1.8+rand(i+280)*(p.reach-1.8),.5,u*u);
      sprite(pos(a,(rand(i+320)-.5)*p.width*(1-u*.7),.05),.16+u*.22,.12+u*.14,
        dust.withAlpha(Math.sin(u*Math.PI)*p.wind*.23),soft,shadows+.025);
    }

    // A single dark, shaded sphere and narrow forward crescent remain readable.
    shadow(orbX,0,hover,.85,.57,.8);
    const q=pos(orbX,0,hover);
    orb(q,.34,1,1,Y+.04);
    draw(disc,q.x-.075,Y+.045,q.z+.065,.20,.22,0,new Color(.12,.095,.17));
    const crescent=Array.from({length:25},(_,j)=>{
      const a=-1.1+j/24*2.2;return pos(orbX+.31*Math.cos(a),.31*Math.sin(a),hover);
    });
    trail('devouring inlet',crescent,.045,pale.withAlpha(.25+pressure*.55),Y+.05);
    for(let i=0;i<7;i++) {
      if(s<prepare || s>=t.release)continue;
      const u=((s-prepare)*2.2+i/7)%1, side=i%2?1:-1;
      const pts=Array.from({length:12},(_,j)=>{
        const v=clamp(u-(1-j/11)*.2);return pos(lerp(1.55,.4,v),side*lerp(.55,.15,v),hover);
      });
      trail('devouring compression '+i,pts,.032,Rim.withAlpha(pressure*p.wind*.55),Y+.025);
    }
    if(p.actors) {
      const x=along(s,p,t,p.reach-.6,capture);
      const motion=s<t.hold?Math.sin(clamp((s-prepare)/p.pull)*Math.PI):
        age>=0?-Math.sin(clamp(age/p.release)*Math.PI):0;
      shadow(x,0,.55,.8,.42);
      const body=pos(x,0,.58), head=pos(x-motion*.12,0,1.12);
      const limb=(key,pts,w,c)=>trail('devouring pawn '+key,pts,w,c,Y+.062);
      const cloth=new Color(.36,.40,.45),skin=new Color(.82,.68,.52);
      limb('legs',[pos(x-.16,0,.06),body,pos(x+.19,0,.06)],.18,cloth);
      limb('arms',[pos(x-.30,0,.65),pos(x,0,.86),pos(x+.31,0,.72)],.14,skin);
      draw(disc,body.x,Y+.065,body.z,.22,.30,0,new Color(.62,.40,.27));
      draw(disc,head.x,Y+.067,head.z,.16,.18,0,skin);
      for(let i=0;i<6;i++) {
        const start=p.reach-.15-rand(i+410)*1.4, held=.68+(i%3)*.16;
        const a=along(s,p,t,start,held), across=(i%2?1:-1)*(.4+Math.floor(i/2)*.12);
        const alpha=1-smooth((s-t.stop)/.24);
        const pts=Array.from({length:15},(_,j)=>pos(along(Math.max(0,s-(1-j/14)*.075),p,t,start,held),across,hover));
        trail('devouring bullet tail '+i,pts,.048,pale.withAlpha(alpha*.7),Y+.07);
        sprite(pos(a,across,hover),.10,.10,pale.withAlpha(alpha),glow,Y+.075);
      }
      if(age>=0 && s<t.stop+.16)for(let i=0;i<3;i++) {
        const pts=Array.from({length:18},(_,j)=>pos(along(Math.max(t.release,s-(1-j/17)*.12),p,t,p.reach-.6,capture),(i-1)*.2,.6));
        trail('devouring pawn wake '+i,pts,.075,Rim.withAlpha(tailFade*.45),Y+.035);
      }
    }
    if(age>=0) {
      sprite(pos(.4,0,hover),.65,.9,pale.withAlpha((1-clamp(age/.12))*.8),glow,Y+.08);
      const travel=clamp(age/p.release), front=lerp(.5,p.reach,1-(1-travel)**2);
      for(let i=0;i<9;i++) {
        const across=(i-4)/4*p.width*.5;
        const pts=Array.from({length:18},(_,j)=>{
          const v=j/17;return pos(front-(1-v)*1.15-Math.abs(across)*.25,across*(.7+.3*v),hover+Math.sin(i*2.1)*.12);
        });
        trail('devouring outward gust '+i,pts,.055,pale.withAlpha(p.wind*tailFade*.55),Y+.026);
      }
      // Dust remains at sampled release positions, then drifts and disperses.
      for(let i=0;i<16;i++) {
        const emitted=t.release+i/16*p.release,u=(s-emitted)/.5;
        if(u<0 || u>1)continue;
        const a=lerp(.5,p.reach,1-(1-clamp((emitted-t.release)/p.release))**2);
        sprite(pos(a+u*.3,(rand(i+500)-.5)*p.width),.18+u*.4,.14+u*.25,
          dust.withAlpha(Math.sin(u*Math.PI)*p.wind*.25),soft,shadows+.025);
      }
    }
  },
};
