using System;
using RimArt;
using UnityEngine;
using Verse;

static void Check(bool b, string message) { if (!b) throw new Exception(message); }
static bool Near(float a, float b) => Math.Abs(a-b)<0.001f;
var map = new Map();
var caster = new Pawn { Map=map, Position=new IntVec3(50,0,50) };
var state = new ShinraPawnState { pawn=caster, map=map, centre=caster.Position.ToVector3Shifted() };
GameComponent_Shinra.Instance.States.Add(state);
Projectile Shot(float x, float z, float endX, float endZ, int damage=12, float speed=960f)
{
    var p = new Projectile { Map=map, DamageAmount=damage };
    p.def.projectile.speed=speed;
    p.StubLaunch(new Vector3(x,0,z), new Vector3(endX,0,endZ), 30);
    return p;
}
foreach (int ticks in new[]{0,90,180})
{
    state.charge.ticks=ticks;
    var p=Shot(40,50.5f,60,50.5f,(int)state.charge.ProjectileLimit);
    float speed=Rounds.Vanilla.CurrentSpeedPerTick(p), damage=p.DamageAmount;
    Check(!ShinraCombat.BeforeProjectileTick(p,1), "Threshold round crossing whole field must be intercepted before impact");
    Check(p.StubOrigin.x<state.centre.x && p.StubDestination.x<p.StubOrigin.x, "Radial outward redirection");
    Check(Near((p.StubDestination-p.StubOrigin).magnitude,60-p.StubOrigin.x), "Preserve remaining travel distance at entry");
    Check(Near(speed,Rounds.Vanilla.CurrentSpeedPerTick(p)) && p.DamageAmount==damage, "Preserve current speed and damage");
    Check(p.StubLauncher==caster && !p.StubPreventFriendlyFire, "Caster owns friendly-fire-capable reflection");
    p.StubLaunch(new Vector3(40,0,50.5f),new Vector3(60,0,50.5f),30);
    Check(ShinraCombat.BeforeProjectileTick(p,1), "Same burst cannot reflect a projectile twice");
    var stronger=Shot(40,50.5f,60,50.5f,(int)state.charge.ProjectileLimit+1);
    Check(ShinraCombat.BeforeProjectileTick(stronger,1), "Overpowered rounds pass");
    var rocket=Shot(40,50.5f,60,50.5f);
    rocket.def.projectile.explosionRadius=2;
    Check(ShinraCombat.BeforeProjectileTick(rocket,1)==(ticks<180), "Rockets require full charge");
    Check(rocket.def.projectile.explosionRadius==2, "Keep explosive behavior");
}
foreach (string type in new[]{"outgoing","overhead","grenade","expired"})
{
    var p=Shot(40,50.5f,60,50.5f);
    if(type=="outgoing") p.StubLaunch(new Vector3(51,0,50.5f),new Vector3(60,0,50.5f),30);
    if(type=="overhead") p.def.projectile.flyOverhead=true;
    if(type=="grenade") p.def.projectile.arcHeightFactor=1;
    if(type=="expired") state.Protected=false;
    Check(ShinraCombat.BeforeProjectileTick(p,1), "Pass unchanged: "+type);
    state.Protected=true;
}
var second=new ShinraPawnState { pawn=new Pawn(),map=map,centre=new Vector3(55,0,50.5f),charge=new ShinraCharge{ticks=180} };
GameComponent_Shinra.Instance.States.Insert(0,second);
var overlap=Shot(40,50.5f,65,50.5f);
Check(!ShinraCombat.BeforeProjectileTick(overlap,1) && overlap.StubLauncher==caster, "Nearest field wins regardless of list order");
GameComponent_Shinra.Instance.States.Remove(second);
var friendly=Shot(40,50.5f,60,50.5f,speed:60);
map.listerThings.AllThings.Add(friendly);
Check(!ShinraCombat.Threatened(state,0.15f) && ShinraCombat.Threatened(state,0.26f), "Auto-release considers arrival and animation delay");
friendly.StubLaunch(new Vector3(50,0,50.5f),new Vector3(60,0,50.5f),30);
Check(ShinraCombat.Threatened(state,0.26f), "Close shot still triggers even though protection may arrive too late");
friendly.StubLaunch(new Vector3(40,0,55),new Vector3(60,0,55),30);
Check(!ShinraCombat.Threatened(state,1), "Misses do not trigger auto-release");
friendly.StubLaunch(new Vector3(40,0,50.5f),new Vector3(44,0,50.5f),30);
Check(!ShinraCombat.Threatened(state,1), "Shots ending before caster are not threats");

foreach(float size in new[]{0.5f,1f,2f,4f})
{
    map.mapPawns.AllPawnsSpawned.Clear();
    var target=new Pawn{Map=map,Position=new IntVec3(52,0,50),BodySize=size};
    map.mapPawns.AllPawnsSpawned.Add(caster);
    map.mapPawns.AllPawnsSpawned.Add(target);
    ShinraCombat.Push(state);
    Check(Math.Abs(target.Position.x-(52+7/Math.Max(1,size)))<=0.51f, "Heavy body scaling with cell rounding");
    Check(target.Damage==0 && target.stances.stagger.Ticks==30, "Free push staggers without damage");
    Check(caster.Position.x==50, "Never push caster");
}
map.mapPawns.AllPawnsSpawned.Clear();
var victim=new Pawn{Map=map,Position=new IntVec3(52,0,50)};
map.mapPawns.AllPawnsSpawned.Add(victim);
map.Blocked.Add((55,50));
ShinraCombat.Push(state);
Check(victim.Position.x==54 && victim.Damage==20, "Solid obstacle stops push and causes collision damage");
Check(map.Blocked.Contains((55,50)), "Obstacle stays intact");
victim.Position=new IntVec3(56,0,50); victim.Damage=0;
ShinraCombat.Push(state);
Check(victim.Position.x==56 && victim.Damage==0, "Outside radius unchanged");
victim.Position=new IntVec3(53,0,50);map.Blocked.Add((52,50));
ShinraCombat.Push(state);
Check(victim.Position.x==53 && victim.Damage==0, "Wall blocks caster-to-target push");
map.Blocked.Clear();
map.Size=new IntVec3(56,1,56);victim.Position=new IntVec3(53,0,50);
ShinraCombat.Push(state);
Check(victim.Position.x==55 && victim.Damage==0, "Map boundary stops travel without damage");
Console.WriteLine("Shinra combat: scaling, collisions, thresholds, explosives, fast segments, overlapping fields and threat prediction passed.");

CombatExtendedRounds.Install(new HarmonyLib.Harmony("RimArt.ShinraTests"));
var ce = new CombatExtended.ProjectileCE {
    Map=map, exactPosition=new Vector3(40,0.8f,50.5f),
    origin=new Vector2(20,50.5f),Destination=new Vector2(60,50.5f),
    velocity=new Vector3(3.7f,0,0),shotSpeed=222f,initialSpeed=444f,
    intTicksToImpact=20,damageAmount=80f,
    equipmentDef=new ThingDef()
};
var ceBackend=Rounds.Foreign;
Check(ceBackend!=null && Near(ceBackend.DirectDamage(ce),20f), "CE inspects current kinetic damage before armor");
ceBackend.Repel(ce,new Vector3(46.5f,0,50.5f),new Vector3(-1,0,0),caster);
Check(Near(ce.DamageAmount,20f) && Near(ce.shotSpeed,222f), "CE preserves damage independently of speed");
Check(Near(ce.startingTicksToImpact,13.5f/3.7f) && Near(ce.Destination.x,33f), "CE preserves remaining range and fractional flight duration");
Check(Near(ce.ExactPosition.y,0.8f) && ce.launcher==caster, "CE keeps flight height and transfers attribution");
var pastEnd=new Vector3(30,0.8f,50.5f);ce.intTicksToImpact=0;
CombatExtendedRounds.RepelledMoveForward(ce,ref pastEnd);
Check(Near(pastEnd.x,33f), "CE final segment stops exactly at remaining range");
ce.equipmentDef.thingCategories.Add(new ThingCategoryDef {defName="Grenades"});
Check(!ceBackend.DirectFlight(ce), "CE thrown grenades excluded even without vanilla arc metadata");
Scribe.mode=LoadSaveMode.Saving; CombatExtendedRounds.RepelledExposeData(ce);
var loadedCe=new CombatExtended.ProjectileCE();
Scribe.mode=LoadSaveMode.LoadingVars; CombatExtendedRounds.RepelledExposeData(loadedCe);
Scribe.mode=LoadSaveMode.PostLoadInit; CombatExtendedRounds.RepelledExposeData(loadedCe);
Scribe.mode=LoadSaveMode.Inactive;
Check(loadedCe.forcedTrajectoryWorker is CombatExtended.LerpedTrajectoryWorker
    && Near(loadedCe.DamageAmount,20f) && loadedCe.gravity==0 && loadedCe.GravityPerWidth==0,
    "CE saves reflected damage and restores forced trajectory without rescaling");
Console.WriteLine("CE adapter: power, independent speed/damage, range, height, grenade exclusion and persistence passed.");

var targetingHarmony = new HarmonyLib.Harmony("RimArt.ShinraTargetingTests");
// AM may have already patched the game method when our compatibility patch is installed.
targetingHarmony.Patch(HarmonyLib.AccessTools.Method(typeof(RimWorld.InvisibilityUtility), "IsPsychologicallyInvisible"),
    prefix: new HarmonyLib.HarmonyMethod(typeof(AM.Patches.Patch_InvisibilityUtility_IsPsychologicallyInvisible), "Prefix"));
Check(RimWorld.InvisibilityUtility.IsPsychologicallyInvisible(caster), "Regression setup: AM hides animated pawns");
ShinraTargeting.Install(targetingHarmony);
state.active=true;
foreach (bool releasing in new[]{false,true}) {
    state.charge.releasing=releasing;
    Check(!RimWorld.InvisibilityUtility.IsPsychologicallyInvisible(caster), "Shinra remains targetable while charging and releasing");
}
RimWorld.InvisibilityUtility.GenuineInvisibility=true;
Check(RimWorld.InvisibilityUtility.IsPsychologicallyInvisible(caster), "Genuine invisibility still works during Shinra");
RimWorld.InvisibilityUtility.GenuineInvisibility=false;
Check(RimWorld.InvisibilityUtility.IsPsychologicallyInvisible(new Pawn()), "Other animations keep AM targeting behavior");
state.active=false;
Check(RimWorld.InvisibilityUtility.IsPsychologicallyInvisible(caster), "Shinra exception ends with the cast");
Console.WriteLine("Shinra targeting: AM invisibility bypass, recovery, genuine invisibility and other animations passed.");
