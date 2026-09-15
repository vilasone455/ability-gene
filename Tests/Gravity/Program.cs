using System;
using System.Linq;
using HarmonyLib;
using RimArt;
using UnityEngine;
using Verse;

int assertions=0;
void Check(bool condition,string message) { assertions++; if(!condition) throw new Exception(message); }
void Near(float actual,float expected,string message,float epsilon=0.001f) => Check(Math.Abs(actual-expected)<epsilon,$"{message}: {actual} != {expected}");
Map Fresh() { Current.Game=new Game(); Find.TickManager.TicksGame=0; RecursionRegistry.Held.Clear(); MapComponent_RetrievalHooks.Targets.Clear(); return Find.CurrentMap=new Map(); }
T Place<T>(Map map,T thing,int x,int z) where T:Thing { thing.Map=map;thing.Position=new(x,0,z);map.listerThings.AllThings.Add(thing);return thing; }
GravityCast Well(Map map,int x=50,int z=50) {
 var pawn=Place(map,new Pawn(),x-12,z);
 var component=map.GetComponent<MapComponent_Gravity>(); component.Begin(pawn,new(x,0,z));
 var cast=component.Casts.Last();
 for(int i=0;i<30;i++){Find.TickManager.TicksGame++;cast.Tick();}
 return cast;
}
Projectile Shot(Map map,float x,float z,float endX,float endZ,float speed=960f) {
 var p=Place(map,new Projectile(),(int)x,(int)z);p.def.projectile.speed=speed;p.def.category=ThingCategory.Projectile;
 p.StubLaunch(new(x,0,z),new(endX,0,endZ),60);return p;
}

Near(GravityRules.Damage(0),15,"Empty implosion"); Near(GravityRules.Damage(100),30,"Half mass");
Near(GravityRules.Damage(200),45,"Full mass"); Near(GravityRules.Damage(100000),45,"Stockpile cap");
Near(GravityRules.Pull(8,1),0,"Field edge"); Near(GravityRules.Pull(1,1),6,"Core pull cap");
Near(GravityRules.Pull(1,4),1.5f,"Large bodies resist");
var clock=new GravityClock();
for(int i=0;i<29;i++) clock.Tick(); Check(!clock.activated,"Opening has no field");
clock.Tick(); Check(clock.activated&&clock.ticks==0,"Activation boundary");
for(int i=0;i<359;i++) Check(!clock.Tick(),"No early auto-implode");
Check(clock.Tick()&&clock.Finish(true),"Six second auto-implode");
Check(!clock.Finish(true)&&!clock.Tick(),"Burst commits once");

var map=Fresh();var cast=Well(map);var component=map.GetComponent<MapComponent_Gravity>();
var chunks=Place(map,new Thing{Mass=25,stackCount=6},50,50);
var victim=Place(map,new Pawn(),51,50);
Near(component.CoreMass(cast),210,"Prepared mass plus pawn body");
victim.Position=new(54,0,50);Near(component.CoreMass(cast),150,"Escaping pawn stops contributing");
Near(GravityRules.Damage(component.CoreMass(cast)),37.5f,"Live damage follows core mass");
victim.Position=new(51,0,50);MapComponent_RetrievalHooks.Targets.Add(chunks);
Near(component.CoreMass(cast),60,"Exclusive retrieval retains ownership");MapComponent_RetrievalHooks.Targets.Clear();
var building=Place(map,new Thing{Mass=9999},50,50);building.def.category=ThingCategory.Building;
Near(component.CoreMass(cast),210,"Structures contribute no mass");
var second=Well(map);Near(component.CoreMass(second),0,"Stable tie prevents double-counted overlapping mass");
second.Finish(false);
cast.Finish(true);Near(victim.Damage,45,"Implosion damages pawn");
Check(chunks.stackCount==6&&chunks.Spawned&&!chunks.Destroyed,"Implosion preserves loot");
Near(cast.caster.Damage,0,"Caster exempt");Near(GameComponent_Gravity.Instance.Remaining(cast.caster),2400,"Cooldown committed");
cast.Finish(true);Near(victim.Damage,45,"Manual double release does not hit twice");

map=Fresh();component=map.GetComponent<MapComponent_Gravity>();
var openingPawn=Place(map,new Pawn(),40,50);component.Begin(openingPawn,new(50,0,50));
component.For(openingPawn).Finish(false);Check(GameComponent_Gravity.Instance.Remaining(openingPawn)==0,"Opening cancel is free");
foreach(string interruption in new[]{"move","stun","down","death","eye","sight","mental"}) {
 map=Fresh();cast=Well(map);victim=Place(map,new Pawn(),50,50);
 if(interruption=="move") cast.caster.Position=new(37,0,50);
 if(interruption=="stun") cast.caster.stances.stunner.Stunned=true;
 if(interruption=="down") cast.caster.Downed=true;
 if(interruption=="death") cast.caster.Dead=true;
 if(interruption=="eye") cast.caster.HasEye=false;
 if(interruption=="sight") map.Blocked.Add((45,50));
 if(interruption=="mental") cast.caster.InMentalState=true;
 cast.Tick();Check(!cast.Active&&!cast.clock.imploded&&victim.Damage==0,"Harmless interruption: "+interruption);
 Check(GameComponent_Gravity.Instance.Remaining(cast.caster)==2400,"Interruption cooldown: "+interruption);
}

map=Fresh();cast=Well(map);component=map.GetComponent<MapComponent_Gravity>();
victim=Place(map,new Pawn(),57,50);var motion=component.MotionFor(victim);
for(int i=0;i<40;i++) GravityMovement.Step(component,motion,new Vector3(4f/60f,0,0));
Check(motion.position.x>58.5f,"Human can escape outer edge");
victim=Place(map,new Pawn(),52,50);motion=component.MotionFor(victim);
for(int i=0;i<30;i++) GravityMovement.Step(component,motion,new Vector3(4f/60f,0,0));
Check(motion.position.x<52.5f,"Core overcomes ordinary walking");
var job=victim.jobs.def;victim.pather.Moving=true;victim.pather.Destination=new(new(70,0,50));
for(int i=0;i<30;i++) GravityMovement.Step(component,motion,Vector3.zero);
Check(victim.jobs.def==job&&victim.pather.Starts>0,"Dragging preserves job and repaths destination");
Check(motion.position.x>=50.5f,"Pull cannot overshoot center");
var item=Place(map,new Thing{Mass=10,stackCount=7},54,50);var original=item;
motion=component.MotionFor(item);for(int i=0;i<120;i++) GravityMovement.Step(component,motion,Vector3.zero);
Check(ReferenceEquals(item,original)&&item.stackCount==7&&item.Spawned&&item.Position.x<54,"Whole-stack movement preserves identity");
map.Blocked.Add((52,51));Check(!GravityMovement.Clear(map,new(51,0,51),new(52,0,52)),"No diagonal corner squeezing");
map.Doors[new(52,0,50)]=new RimWorld.Building_Door();Check(!GravityMovement.Clear(map,new(51,0,50),new(53,0,50)),"Closed door blocks field");

// Saving the committed phase restores recovery without replaying the implosion.
map=Fresh();cast=Well(map);cast.Finish(true);Scribe.Data.Clear();Scribe.mode=LoadSaveMode.Saving;cast.ExposeData();
var loaded=new GravityCast{caster=cast.caster,map=map};Scribe.mode=LoadSaveMode.LoadingVars;loaded.ExposeData();
Scribe.mode=LoadSaveMode.PostLoadInit;loaded.ExposeData();Scribe.mode=LoadSaveMode.Inactive;
Check(!loaded.Active&&loaded.clock.imploded,"Finished phase persists");
Check(!loaded.clock.Finish(true),"Loaded implosion cannot repeat");

// Exercise production flight integration, native collision dispatch, and both real adapters.
var harmony=new Harmony("RimArt.GravityTests");
harmony.Patch(AccessTools.PropertyGetter(typeof(Projectile),nameof(Projectile.ExactPosition)),postfix:
 new HarmonyMethod(typeof(Patch_GravityProjectilePosition),nameof(Patch_GravityProjectilePosition.Postfix)));
CombatExtendedRounds.Install(harmony);GravityProjectiles.Install(harmony);
map=Fresh();cast=Well(map);
var round=Shot(map,40,50.5f,70,50.5f);int sweeps=0;
round.Collision=(a,b)=>{sweeps++;return false;};
Check(!GravityProjectiles.BeforeTick(round,1)&&round.Destroyed,"Fast shot cannot skip absorbing core");
Check(sweeps>0,"Absorption tests native collisions first");
round=Shot(map,40,50.5f,70,50.5f);round.Collision=(a,b)=>{if(b.x<44)return false;round.Destroy();return true;};
GravityProjectiles.BeforeTick(round,1);Check(round.Destroyed&&round.Position.x<=44,"Cover hits before core");
round=Shot(map,40,54.5f,75,54.5f,speed:120);var shooter=new Pawn();
Rounds.Vanilla.Redirect(round,new(40,0,54.5f),new(75,0,54.5f),18,1f,shooter);
float lastRemaining=float.MaxValue;bool bent=false;
for(int i=0;i<30&&!round.Destroyed;i++) {
 GravityProjectiles.BeforeTick(round,1);var flight=GameComponent_GravityFlights.Instance.Get(round);
 // Initial flight is outside the influence; advance its stub native boundary to entry.
 if(flight==null&&!round.Destroyed){round.ExactPosition+=new Vector3(2,0,0);continue;}
 if(flight==null)break;
 Check(flight.remaining<lastRemaining,"Every bent tick spends range");lastRemaining=flight.remaining;
 bent|=flight.heading.z<0;
 Check(round.DamageAmount==12&&round.Launcher==shooter,"Bending preserves damage and shooter");
}
Check(bent,"Grazing bullet curves inward");
foreach(string excluded in new[]{"rocket","overhead","arc","captured"}) {
 round=Shot(map,40,50.5f,70,50.5f);
 if(excluded=="rocket")round.def.projectile.explosionRadius=1;
 if(excluded=="overhead")round.def.projectile.flyOverhead=true;
 if(excluded=="arc")round.def.projectile.arcHeightFactor=1;
 if(excluded=="captured")RecursionRegistry.Held.Add(round);
 Check(GravityProjectiles.BeforeTick(round,1)&&!round.Destroyed,"Excluded: "+excluded);
}
var ce=Place(map,new CombatExtended.ProjectileCE{exactPosition=new(40,0.8f,54.5f),origin=new(20,54.5f),
 Destination=new(80,54.5f),shotSpeed=240f,initialSpeed=480f,intTicksToImpact=30,damageAmount=80f,
 velocity=new(4,0,0),equipmentDef=new ThingDef()},40,54);
float damage=Rounds.Foreign.DirectDamage(ce);
for(int i=0;i<10;i++) {
 Rounds.Foreign.Bend(ce,new(45+i,0.8f,54.5f),new(0,0,1),20-i,4);
 Near(Rounds.Foreign.DirectDamage(ce),damage,"Repeated CE bending preserves kinetic damage");
 Near(Rounds.Foreign.CurrentSpeedPerTick(ce),4,"CE speed preserved");
 Near(ce.ExactPosition.y,0.8f,"CE height preserved");
 Near(ce.startingTicksToImpact,(20-i)/4f,"CE fractional remaining flight");
}
ce.ExactPosition=new(40,0.8f,50.5f);ce.Destination=new(80,50.5f);ce.shotSpeed=960;ce.velocity=new(16,0,0);ce.intTicksToImpact=10;
ce.Collision=(a,b)=>false;GravityProjectiles.BeforeTick(ce,1);
Check(ce.Destroyed,"CE fast bullet absorbed using native sweep");
Console.WriteLine($"Gravity Well: {assertions} assertions passed (lifecycle, interruption, mass, movement, persistence, native collision routing, vanilla/CE flight).");
