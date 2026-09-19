// Sixfold Barrage — VFX-only proposal. Exactly three orbs stretch into dumbbells
// and morph into six fists (two per source), joined by thin material strands.
// Alternating, accelerating punches have individual windup, contact, and recoil.
// Six fists converge into a heavy finisher, then settle back into three orbs.
// Resource consumption/damage are not defined by this sketch; bell cost is separate.
import { Color, Mathf, AltitudeLayer } from '../js/engine.js';
import { draw, mesh } from './lib/six-paths-solid.js';
import { P, Body, Rim, Y, at, orb, sprite, trail, impact, target, glow } from './lib/six-paths-impact.js';
const smooth = Mathf.Smooth, lerp = Mathf.Lerp, clamp = Mathf.Clamp01;
const face = new Color(0.09, 0.065, 0.13);
const order = [0, 3, 4, 1, 2, 5];
const outline = [[-.30,-.52],[.28,-.52],[.38,-.25],[.56,-.1],[.60,.18],[.42,.3],
  [.40,.52],[.22,.59],[.10,.53],[-.02,.60],[-.17,.54],[-.30,.57],[-.45,.46],[-.49,.20],[-.43,-.16]];
function times(p) {
  const attack = p.split + p.form, count = p.rounds * 6, cycle = p.interval * 3.8;
  const starts = Array.from({ length: count }, (_, j) => attack + p.interval * (j - 0.18 * j * j / (count - 1)));
  const merge = starts[count - 1] + cycle, poise = merge + p.merge;
  const launch = poise + p.poise, hit = launch + p.finish, recover = hit + 0.25;
  return { attack, count, cycle, starts, merge, poise, launch, hit, recover, end: recover + p.recover + 0.35 };
}
const root = (o, pair, p) => at(o, pair === 0 ? -0.9 : pair === 1 ? 0.9 : 0, -p.distance + (pair === 2 ? 1.0 : -0.65));
const home = (o, i, p) => at(o, (i % 2 ? 1 : -1) * (1.65 + (i >> 1) * 0.27), -p.distance + (i >> 1) * 0.95);
const contact = (o, i, p) => at(o, (i % 2 ? 1 : -1) * 0.24, -p.fist * 0.59 + ((i >> 1) - 1) * 0.1, 0.65);
function pose(s, o, i, p, t) {
  const split = smooth(s / p.split), formed = smooth((s - p.split) / p.form);
  const r = root(o, i >> 1, p), h = home(o, i, p);
  let pos = { x: lerp(r.x, h.x, split), z: lerp(r.z, h.z, split) };
  let punch = -1;
  for (let j = 0; j < t.count; j++) if (order[j % 6] === i && s >= t.starts[j]) punch = j;
  if (punch >= 0 && s < t.merge) {
    const u = clamp((s - t.starts[punch]) / t.cycle), c = contact(o, i, p);
    if (u < 0.22) pos = at(h, (i % 2 ? 1 : -1) * smooth(u / .22) * .18, -smooth(u / .22) * .4);
    else if (u < .46) {
      const v = ((u - .22) / .24) ** 2;
      pos = { x: lerp(h.x + (i % 2 ? .18 : -.18), c.x, v) + Math.sin(v * Math.PI) * (i % 2 ? .28 : -.28),
        z: lerp(h.z - .4, c.z, v) };
    } else {
      const v = smooth((u - .52) / .48);
      pos = { x: lerp(c.x, h.x, v), z: lerp(c.z, h.z, v) };
    }
  }
  if (s >= t.merge) {
    const u = smooth((s - t.merge) / p.merge), hub = at(o, 0, -p.distance * .55);
    pos = { x: lerp(h.x, hub.x, u), z: lerp(h.z, hub.z, u) };
  }
  const angle = Math.atan2(o.x - pos.x, o.z + .39 - pos.z);
  return { ...pos, angle, morph: formed };
}
function fist(key, q, size, morph, alpha, layer = Y) {
  if (size <= 0 || alpha <= 0) return;
  const point = (x,z) => at(q, (x * Math.cos(q.angle) + z * Math.sin(q.angle)) * size,
    (-x * Math.sin(q.angle) + z * Math.cos(q.angle)) * size);
  const pts = outline.map(([x,z]) => {
    const a = Math.atan2(z,x);
    return point(lerp(Math.cos(a) * .36,x,morph),lerp(Math.sin(a) * .36,z,morph));
  });
  const verts = [q.x,q.z], tri = [];
  pts.forEach((v,i)=>{verts.push(v.x,v.z);tri.push(0,i+1,(i+1)%pts.length+1);});
  const m=mesh(key);m.setFlat(verts,tri);draw(m,0,layer,0,1,1,0,Body.withAlpha(alpha));
  trail(key+' rim',[...pts,pts[0]],.035*size,Rim.withAlpha(alpha*.85),layer+.002);
  // Four curled fingers and an opposing thumb, rather than a generic mitten.
  for(let j=0;j<4;j++) {
    const x=-.34+j*.20;
    trail(key+' knuckle '+j,[point(x,.18),point(x-.025,.34),point(x,.48)],.028*size,
      Rim.withAlpha(alpha*morph*.55),layer+.004);
  }
  trail(key+' thumb',[point(.48,.14),point(.30,.07),point(.21,-.14)],.09*size,face.withAlpha(alpha*morph),layer+.005);
  trail(key+' wrist',[point(-.24,-.34),point(0,-.30),point(.25,-.34)],.025*size,Rim.withAlpha(alpha*morph*.45),layer+.004);
}
export default {
  kit:'Six Paths', label:'Sixfold Barrage (sketch)',
  params:{
    actors:{label:'Show caster and target',value:true,group:'Showcase'},
    split:P('Three orbs split into pairs',.55,.25,1,.05,'Timing (s)'),
    form:P('Lobes become fists',.4,.2,.8,.05,'Timing (s)'),
    interval:P('Starting punch interval',.14,.10,.22,.01,'Timing (s)'),
    rounds:P('Barrage rounds',3,1,5,1,'Timing (s)'),
    merge:P('Six fists merge',.4,.2,.8,.05,'Timing (s)'),
    poise:P('Finisher anticipation',.28,.12,.6,.02,'Timing (s)'),
    finish:P('Heavy punch travel',.20,.12,.4,.02,'Timing (s)'),
    recover:P('Reform three orbs',.6,.3,1,.05,'Timing (s)'),
    distance:P('Caster to target (cells)',5,3.5,7,.25,'Shape'),
    fist:P('Fist size (cells)',.95,.65,1.3,.05,'Shape'),
    giant:P('Finisher size multiplier',2.2,1.6,3,.1,'Shape'),
    trails:P('Punch trail strength',.65,0,1,.05,'Impact'),
    dust:P('Finisher dust',.6,0,.8,.05,'Impact'),
    shake:P('Finisher shake',.18,0,.2,.01,'Impact'),
  },
  duration(p){return times(p).end;},
  phases(p){const t=times(p);return [{name:'Three orbs → six lobes',t:0},{name:'Form fists',t:p.split},
    {name:'Alternating barrage',t:t.attack},{name:'Merge',t:t.merge},{name:'Poise',t:t.poise},
    {name:'Heavy strike',t:t.hit},{name:'Three orbs return',t:t.recover}];},
  events(p){const t=times(p);return [...t.starts.map((s,j)=>({t:s+t.cycle*.46,type:'shake',value:p.shake*(j%6===5?.18:.07)})),
    {t:t.hit,type:'shake',value:p.shake}];},
  draw(s,p,{origin:o,scene}){
    const t=times(p);if(s<0||s>=t.end)return;
    const merge=smooth((s-t.merge)/p.merge), recover=smooth((s-t.recover)/p.recover);
    const fade=1-smooth((s-t.end+.25)/.25);
    const sun=scene?.shadowVector??{x:-.45,z:-.32};
    target(at(o,0,-p.distance),p.actors);
    target(o,p.actors);
    // Two end-lobes occupy the same source at time zero; explicitly draw only
    // three original orbs until separation begins, avoiding six stacked spheres.
    for(let pair=0;pair<3;pair++){
      const r=root(o,pair,p), split=smooth(s/p.split);
      if(split<.03)orb(r,.34,1-split/.03);
      if(s<t.poise){
        const a=pose(s,o,pair*2,p,t),b=pose(s,o,pair*2+1,p,t);
        const pts=Array.from({length:25},(_,j)=>{const u=j/24;return {x:lerp(a.x,b.x,u),z:lerp(a.z,b.z,u)-Math.sin(u*Math.PI)*(.15+split*.45)};});
        trail('barrage pair '+pair,pts,lerp(.30,.055,smooth((s-p.split*.45)/p.form))*(1-merge),Body,Y-.015);
        trail('barrage pair rim '+pair,pts,.018*(1-merge),Rim.withAlpha(.5),Y-.012);
      }
      if(recover>0){
        const from=at(o,0,-p.fist*p.giant*.59,.65);
        const pos={x:lerp(from.x,r.x,recover),z:lerp(from.z,r.z,recover)+Math.sin(recover*Math.PI)*.4};
        orb(pos,.34*recover,fade);
      }
    }
    for(let i=0;i<6;i++){
      if(s>=t.poise)break;
      const q=pose(s,o,i,p,t),alpha=smooth(s/p.split/.12)*(1-merge);
      const history=Array.from({length:20},(_,j)=>pose(Math.max(0,s-(1-j/19)*.11),o,i,p,t));
      if(s>=t.attack&&s<t.merge)trail('barrage travel '+i,history,.24*p.fist,Rim.withAlpha(p.trails*.35),Y-.008);
      sprite(at(q,sun.x*.65,sun.z*.65-.39),p.fist*1.4,p.fist,Body.withAlpha(alpha*.25),undefined,AltitudeLayer.Shadows.AltitudeFor());
      fist('barrage hand '+i,q,p.fist,q.morph,alpha,Y+i*.008);
    }
    for(let j=0;j<t.count;j++){
      const hit=t.starts[j]+t.cycle*.46,age=s-hit;
      if(age<0||age>.14)continue;
      const c=contact(o,order[j%6],p),pos=at(c,0,p.fist*.59);
      sprite(pos,.95,.7,new Color(.85,.74,1,(1-age/.14)*.65),glow,Y+.10);
      const pts=[at(pos,-.3,-.2),pos,at(pos,.3,.3)];
      trail('barrage contact '+j,pts,.06*(1-age/.14),Rim,Y+.11);
    }
    if(s>=t.merge){
      const start=at(o,0,-p.distance*.55),end=at(o,0,-p.fist*p.giant*.59,.65);
      const flight=clock=>{const u=clamp((clock-t.launch)/p.finish)**2;
        return {x:o.x,z:lerp(start.z-.35*smooth((clock-t.poise)/p.poise),end.z,u),angle:0};};
      const q=flight(Math.min(s,t.hit));
      const pts=Array.from({length:24},(_,j)=>flight(Math.min(s,t.hit)-(1-j/23)*.12));
      if(s>=t.launch)trail('barrage finisher trail',pts,.65*p.fist,Rim.withAlpha(p.trails*.4*(1-recover)),Y+.065);
      fist('barrage giant',q,p.fist*p.giant*merge,1,(1-recover)*fade,Y+.07);
      impact('barrage finisher',o,s-t.hit,1,1.5,p.dust);
    }
  },
};
