using System;
using AbilityGenes;
using RimWorld;
using UnityEngine;
using Verse;

class Program
{
 static int checks;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static bool Near(float a,float b)=>Math.Abs(a-b)<.0001;
 static (Map map,Pawn pawn,Corpse corpse,CarrionRun run) Setup(float size=1){
  var map=new Map();var pawn=new Pawn{Map=map};pawn.genes.All.Add(new Gene_Dispersal());
  var corpse=new Corpse{Map=map,DrawPos=new Vector3(5,0,0)};corpse.InnerPawn.BodySize=size;
  return(map,pawn,corpse,new CarrionRun(pawn,corpse));
 }
 static CarrionRun Reload(CarrionRun run){Scribe.mode=LoadSaveMode.Saving;Scribe.Data.Clear();run.ExposeData();var copy=new CarrionRun();Scribe.mode=LoadSaveMode.LoadingVars;copy.ExposeData();Scribe.mode=LoadSaveMode.PostLoadInit;copy.ExposeData();return copy;}
 static void Finish(CarrionRun run,Map map){for(int n=0;n<1000;n++)if(!run.Tick(map))return;throw new Exception("Run never finished");}
 static void Main(){
  var s=Setup(); var wound=new Hediff_Injury{Severity=30,BleedRate=1};
  var scar=new Hediff_Injury{Severity=10,BleedRate=1,Permanent=true};
  var bruise=new Hediff_Injury{Severity=10,BleedRate=0};
  var disease=new Hediff{Severity=.5f};var blood=new Hediff{def=HediffDefOf.BloodLoss,Severity=.3f};
  s.pawn.health.hediffSet.hediffs.AddRange(new Hediff[]{wound,scar,bruise,disease,blood});
  var hemogen=new Gene_Hemogen{Value=.9f};s.pawn.genes.All.Add(hemogen);
  for(int i=0;i<60;i++)s.run.Tick(s.map);
  Check(!s.corpse.Destroyed && Near(wound.Severity,30),"No consumption or healing before feeding completes");
  s.run=Reload(s.run);
  for(int i=0;i<90;i++)s.run.Tick(s.map);
  Check(s.corpse.DestroyCalls==1 && Near(wound.Severity,30),"Consume once, delay healing until return");
  Check(s.run.Corpse==null,"Returning save must not retain a destroyed corpse reference");
  s.run=Reload(s.run);Finish(s.run,s.map);
  Check(Near(wound.Severity,10)&&Near(blood.Severity,.1f)&&Near(hemogen.Value,1),"Budgeted wound, blood and hemogen recovery");
  Check(Near(scar.Severity,10)&&Near(bruise.Severity,10)&&Near(disease.Severity,.5f),"Scars, nonbleeding injuries and disease unchanged");
  Check(!s.run.Tick(s.map)&&Near(wound.Severity,10),"No duplicate payout");
  Check(Near(s.pawn.DrawPos.x,0),"Ability never moves the carrier");
  var tiny=Setup(.1f);var tinyWound=new Hediff_Injury{Severity=30,BleedRate=1};tiny.pawn.health.hediffSet.hediffs.Add(tinyWound);Finish(tiny.run,tiny.map);
  Check(Near(tinyWound.Severity,28),"Small corpses give proportionally smaller recovery");
  var capped=Setup(4);var first=new Hediff_Injury{Severity=12,BleedRate=2};var second=new Hediff_Injury{Severity=30,BleedRate=1};
  var lowBlood=new Hediff{def=HediffDefOf.BloodLoss,Severity=.05f};capped.pawn.health.hediffSet.hediffs.AddRange(new Hediff[]{first,second,lowBlood});
  var inactiveHemogen=new Gene_Hemogen{Active=false,Value=.3f};capped.pawn.genes.All.Add(inactiveHemogen);capped.run=Reload(capped.run);Finish(capped.run,capped.map);
  Check(Near(first.Severity,0)&&Near(second.Severity,22),"Largest corpse stays capped; healing is shared across wounds by bleeding priority");
  Check(Near(lowBlood.Severity,0)&&Near(inactiveHemogen.Value,.3f),"Blood loss cannot go negative and inactive Hemogen is unchanged");
  var gone=Setup();gone.corpse.Destroy(DestroyMode.Vanish);Check(!gone.run.Tick(gone.map),"Missing corpse cancels");
  var moved=Setup();for(int i=0;i<30;i++)moved.run.Tick(moved.map);moved.corpse.DrawPos=new Vector3(6,0,0);
  Check(!moved.run.Tick(moved.map)&&!moved.corpse.Destroyed,"Hauling during feeding cancels without consuming");
  var dead=Setup();dead.pawn.Dead=true;Check(!dead.run.Tick(dead.map)&&!dead.corpse.Destroyed,"Dead recipient cancels");
  var away=Setup();away.pawn.Map=new Map();Check(!away.run.Tick(away.map),"Leaving map cancels");
  var flying=Setup();flying.pawn.Spawned=false;bool waiting=true;for(int i=0;i<200;i++)waiting &= flying.run.Tick(flying.map);Check(waiting,"Concurrent Murder pauses feeding");
  Check(!flying.corpse.Destroyed,"No consumption during temporary flyer hold");flying.pawn.Spawned=true;Finish(flying.run,flying.map);Check(flying.corpse.Destroyed,"Feeding resumes after landing");
  var invalid=Setup();invalid.corpse.Rot=RotStage.Rotting;Check(!CarrionRun.ValidCorpse(invalid.corpse,invalid.map),"Rotten corpse rejected");
  invalid.corpse.Rot=RotStage.Fresh;invalid.corpse.InnerPawn.RaceProps.IsFlesh=false;Check(!CarrionRun.ValidCorpse(invalid.corpse,invalid.map),"Mech corpse rejected");
  var claims=Setup();var component=new MapComponent_Carrion(claims.map);var gene=claims.pawn.genes.GetFirstGeneOfType<Gene_Dispersal>();
  Check(component.Begin(claims.pawn,claims.corpse)&&gene.Charges==2,"Start costs exactly one charge");
  var otherCorpse=new Corpse{Map=claims.map};Check(!component.Begin(claims.pawn,otherCorpse)&&gene.Charges==2,"One flock per carrier");
  var otherPawn=new Pawn{Map=claims.map};otherPawn.genes.All.Add(new Gene_Dispersal());Check(!component.Begin(otherPawn,claims.corpse),"One flock per corpse");
  for(int i=0;i<300;i++)component.MapComponentTick();Check(!component.Running(claims.pawn)&&!component.Claimed(claims.corpse),"Completion releases claim and busy state");
  Console.WriteLine($"PASS: {checks} lifecycle assertions (production code with stubbed game boundaries).");
 }
}
