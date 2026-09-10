// Minimal game boundary for testing the production Carrion lifecycle without Unity.
using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine {
 public struct Vector3 {
  public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator -(Vector3 a)=>new(-a.x,-a.y,-a.z);
  public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
 }
 public static class Mathf {
  public const float PI=(float)Math.PI;
  public static float Clamp(float n,float a,float b)=>Math.Clamp(n,a,b);
  public static float Min(float a,float b)=>Math.Min(a,b);
  public static float Max(float a,float b)=>Math.Max(a,b);
  public static int Max(int a,int b)=>Math.Max(a,b);
  public static int CeilToInt(float v)=>(int)Math.Ceiling(v);
  public static float Sin(float v)=>(float)Math.Sin(v);
  public static float Cos(float v)=>(float)Math.Cos(v);
 }
}
namespace Verse {
 using UnityEngine;
 public interface IExposable{void ExposeData();}
 public class Map{}
 public class MapComponent {
  protected Map map; public MapComponent(Map map){this.map=map;}
  public virtual void MapComponentTick(){} public virtual void MapComponentUpdate(){} public virtual void ExposeData(){}
 }
 public enum DestroyMode{Vanish} public enum RotStage{Fresh,Rotting,Dessicated}
 public class Pawn {
  public bool Dead,Spawned=true; public Map Map; public Map MapHeld=>Map;
  public Vector3 DrawPos; public RaceProperties RaceProps=new(); public float BodySize=1;
  public Health health=new(); public Genes genes=new();
 }
 public class RaceProperties{public bool IsFlesh=true;}
 public class Genes {
  public List<object> All=new(); public T GetFirstGeneOfType<T>() where T:class=>All.OfType<T>().FirstOrDefault();
 }
 public class Corpse {
  public bool Destroyed,Spawned=true; public Map Map; public Pawn InnerPawn=new();
  public Vector3 DrawPos; public RotStage Rot; public int DestroyCalls;
  public RotStage GetRotStage()=>Rot;
  public void Destroy(DestroyMode mode){Destroyed=true;Spawned=false;DestroyCalls++;}
 }
 public class Health{public HediffSet hediffSet=new();}
 public class HediffSet {
  public List<Hediff> hediffs=new();
  public Hediff GetFirstHediffOfDef(object def)=>hediffs.FirstOrDefault(h=>h.def==def);
 }
 public class Hediff{public float Severity;public object def;}
 public class Hediff_Injury:Hediff {
  public float BleedRate; public bool Permanent; public bool IsPermanent()=>Permanent;
  public void Heal(float n){Severity-=n;}
 }
 public static class Vectors {
  public static float MagnitudeHorizontalSquared(this Vector3 v)=>v.x*v.x+v.z*v.z;
  public static float MagnitudeHorizontal(this Vector3 v)=>(float)Math.Sqrt(v.MagnitudeHorizontalSquared());
  public static float AngleFlat(this Vector3 v)=>0;
 }
 public enum LoadSaveMode{Saving,LoadingVars,PostLoadInit} public enum LookMode{Deep}
 public static class Scribe{public static LoadSaveMode mode;public static Dictionary<string,object> Data=new();}
 public static class Scribe_Values {
  public static void Look<T>(ref T value,string key){if(Scribe.mode==LoadSaveMode.Saving)Scribe.Data[key]=value;else if(Scribe.mode==LoadSaveMode.LoadingVars)value=(T)Scribe.Data[key];}
 }
 public static class Scribe_References{public static void Look<T>(ref T value,string key)=>Scribe_Values.Look(ref value,key);}
 public static class Scribe_Collections{public static void Look<T>(ref List<T> value,string key,LookMode mode)=>Scribe_Values.Look(ref value,key);}
}
namespace RimWorld {
 using Verse;
 public static class HediffDefOf{public static object BloodLoss=new();}
 public class Gene_Hemogen{public bool Active=true;public float Value;}
 public static class GeneUtility {
  public static void OffsetHemogen(Pawn p,float value,bool applyStatFactor=true){var g=p.genes.GetFirstGeneOfType<Gene_Hemogen>();g.Value=Math.Min(1,g.Value+value);}
 }
}
namespace AbilityGenes {
 using Verse; using UnityEngine;
 public class Gene_Dispersal{public bool Active=true; public int Charges=3;public bool HasCharge=>Charges>0;public void Spend(){Charges--;}}
 public class Flock{public Flock(int n,float spread,float lift){}public void Draw(Vector3 a,Vector3 b,float p,int t){}}
 public static class DispersalFX{public static void Arrive(Vector3 p,Map m){}public static void Feed(Vector3 p,Map m){}public static void Travel(Vector3 a,Vector3 b,float p,float l,Map m){}}
 public static class DispersalGraphics{public static void DrawFeedingCrow(Vector3 p,float h,int f,float a){}}
}
