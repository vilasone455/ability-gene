// Minimal game boundary for testing the production vector-manipulation arithmetic without
// Unity or RimWorld. The field names on Projectile are load-bearing: CapturedProjectile reaches
// them through Harmony field refs by name, exactly as it does against the real engine.
using System;
using System.Collections.Generic;

namespace UnityEngine {
 public struct Color {
  public float r,g,b,a; public Color(float r,float g,float b,float a=1f){this.r=r;this.g=g;this.b=b;this.a=a;}
 }
 public struct Vector3 {
  public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  public static Vector3 zero=>new(0,0,0);
  public static Vector3 up=>new(0,1,0);
  public static Vector3 forward=>new(0,0,1);
  public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
  public static Vector3 operator /(Vector3 a,float b)=>new(a.x/b,a.y/b,a.z/b);
  public void Normalize() { this = normalized; }
  public static Vector3 operator -(Vector3 a) => zero-a;
  public float sqrMagnitude=>x*x+y*y+z*z;
  public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
  public Vector3 normalized{get{float m=magnitude;return m<=0f?zero:this/m;}}
  public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
  public override string ToString()=>$"({x:0.###}, {y:0.###}, {z:0.###})";
 }
 // Only rotation about +Y is ever asked for, and only of ground-plane headings, so the
 // quaternion is stored as that angle and applied as the matching 2D rotation. Unity's
 // convention: a positive angle about up turns north toward east, which is the direction
 // Verse's AngleFlat counts in.
 public struct Quaternion {
  public float degrees;
  public static Quaternion AngleAxis(float degrees,Vector3 axis)=>new Quaternion{degrees=degrees};
  public static Vector3 operator *(Quaternion q,Vector3 v){
   double r=q.degrees*Math.PI/180.0; float c=(float)Math.Cos(r), s=(float)Math.Sin(r);
   return new Vector3(v.x*c+v.z*s, v.y, -v.x*s+v.z*c);
  }
 }
 public static class Mathf {
  public static float Abs(float v)=>Math.Abs(v);
  public static float Max(float a,float b)=>Math.Max(a,b);
  public static int Max(int a,int b)=>Math.Max(a,b);
  public static float Min(float a,float b)=>Math.Min(a,b);
  public static float Clamp01(float v)=>Math.Clamp(v,0f,1f);
  public static int CeilToInt(float v)=>(int)Math.Ceiling(v);
  public static int RoundToInt(float v)=>(int)Math.Round(v,MidpointRounding.AwayFromZero);
  public static bool Approximately(float a,float b)=>Math.Abs(a-b)<1e-4f;
  public static float Sqrt(float v)=>(float)Math.Sqrt(v);
  public static float Pow(float a,float b)=>(float)Math.Pow(a,b);
 }
}

namespace Verse.Sound {
 // Only ever maintained, and only for a round held by a membrane - which the vector
 // arithmetic under test never is.
 public class Sustainer{public bool Ended; public void Maintain(){}}
}

namespace Verse {
 using UnityEngine;
 using Verse.Sound;
 public static class Log { public static void Error(string s) { throw new System.Exception(s); } public static void Warning(string s) { throw new System.Exception(s); } }
 public enum SimpleColor{White,Red,Green,Blue,Cyan,Magenta,Yellow,Orange}
 public enum LookMode{Value,Reference}
 public enum LoadSaveMode{Inactive,Saving,LoadingVars,ResolvingCrossRefs,PostLoadInit}
 public static class Scribe{public static LoadSaveMode mode=LoadSaveMode.Inactive; public static Dictionary<string,object> Data = new();}
 public static class Scribe_Values {
  public static void Look<T>(ref T v, string key) {
   if(Scribe.mode==LoadSaveMode.Saving) Scribe.Data[key]=v;
   if(Scribe.mode==LoadSaveMode.LoadingVars) v=Scribe.Data.TryGetValue(key,out var o)?(T)o:default;
  }
 }
 public static class Scribe_Collections{
  public static void Look<K,V>(ref Dictionary<K,V> dict,string label,LookMode keys,LookMode values){}
 }
 public struct IntVec3 {
  public Vector3 ToVector3Shifted() => new(x+0.5f,0,z+0.5f);
  public bool InBounds(Map map) => x>=0 && z>=0 && x<map.Size.x && z<map.Size.z;
  public bool Walkable(Map map) => !map.Blocked.Contains((x,z));
  public int x,y,z; public IntVec3(int x,int y,int z){this.x=x;this.y=y;this.z=z;}
  public override string ToString()=>$"({x}, {y}, {z})";
 }
 public static class VectorStubExtensions {
  public static float AngleFlat(this Vector3 v) => (float)(Math.Atan2(v.x,v.z)*180/Math.PI);
  public static Vector3 Yto0(this Vector3 v) => new(v.x,0,v.z);
  public static IntVec3 ToIntVec3(this Vector3 v)=>new IntVec3((int)Math.Floor(v.x),(int)Math.Floor(v.y),(int)Math.Floor(v.z));
 }
 public class Map{
  public IntVec3 Size=new IntVec3(250,1,250);
  public HashSet<(int,int)> Blocked = new();
  public MapPawns mapPawns = new();
  public Lister listerThings = new();
 }
 public class MapPawns { public List<Pawn> AllPawnsSpawned = new(); }
 public class Lister { public List<Thing> AllThings = new(); }
 public struct LocalTargetInfo {
  public IntVec3 Cell; public LocalTargetInfo(IntVec3 cell){Cell=cell;}
 }
 public enum ProjectileHitFlags { All }
 public class ProjectileProperties{public bool flyOverhead; public float arcHeightFactor, explosionRadius; public float speed=30f; public float SpeedTilesPerTick=>speed/60f;}
 public class ThingCategoryDef { public string defName; }
 public class ThingDef{public ProjectileProperties projectile=new(); public List<ThingCategoryDef> thingCategories = new();}
 // def lives on Thing in the engine, and Rounds reads it there.
 public class Thing{public bool Destroyed,Spawned=true; public Map Map; public ThingDef def=new();}
 public class Pawn:Thing {
  public bool Dead; public float BodySize = 1f; public IntVec3 Position;
  public Pather pather = new(); public Stances stances = new(); public float Damage;
  public void Notify_Teleported() {}
  public void TakeDamage(DamageInfo info) { Damage += info.amount; }
 }
 public class Pather { public void StopDead() {} }
 public class Stances { public Stagger stagger = new(); }
 public class Stagger { public int Ticks; public void StaggerFor(int ticks) { Ticks=ticks; } }
 public struct DamageInfo { public float amount; public DamageInfo(object def,float amount,float penetration,float angle,Thing source) {this.amount=amount;} }
 public static class GenSight {
  public static bool LineOfSight(IntVec3 a, IntVec3 b, Map map) {
   Vector3 from = a.ToVector3Shifted(), to = b.ToVector3Shifted();
   for (float t=0; t<=1; t+=0.01f) if (!(from+(to-from)*t).ToIntVec3().Walkable(map)) return false;
   return true;
  }
 }
 // Field names and types mirror Verse.Projectile, which is what the Harmony field refs bind to.
 public class Projectile:Thing {
  public int DamageAmount = 12;
  public virtual int UpdateRateTicks => 15;
  protected void TickInterval(int delta) {}
  public ProjectileHitFlags HitFlags;
  protected Vector3 origin;
  protected Vector3 destination;
  protected int ticksToImpact;
  protected int lifetime;
  protected Thing launcher;
  protected bool landed;
  protected bool preventFriendlyFire;
  protected Sustainer ambientSustainer;
  public Thing Launcher=>launcher;
  public LocalTargetInfo usedTarget;
  public LocalTargetInfo intendedTarget;
  public virtual Vector3 ExactPosition{get;set;}
  public void StubLaunch(Vector3 from,Vector3 to,int ticks){
   origin=from; destination=to; ticksToImpact=ticks; lifetime=ticks; ExactPosition=from;
  }
  public Vector3 StubOrigin=>origin;
  public Vector3 StubDestination=>destination;
  public int StubTicks=>ticksToImpact;
  public int StubLifetime=>lifetime;
  public Thing StubLauncher=>launcher;
  public bool StubPreventFriendlyFire=>preventFriendlyFire;
 }
}

namespace RimWorld { public static class DamageDefOf { public static object Blunt = new(); } }
namespace RimArt {
 public class ShinraPawnState {
  public Verse.Pawn pawn; public Verse.Map map; public UnityEngine.Vector3 centre;
  public ShinraCharge charge = new(); public bool Protected = true, active;
  public System.Collections.Generic.List<Verse.Thing> redirected = new();
 }
 public class GameComponent_Shinra {
  public static GameComponent_Shinra Instance = new();
  public System.Collections.Generic.List<ShinraPawnState> States = new();
 }
 public class HalvingProjectile {}
 public static class RecursionRegistry { public static int CapturedCount; public static bool TryGetCapture(Verse.Thing t, out HalvingProjectile o) { o=null; return false; } }
}

namespace UnityEngine { public struct Vector2 { public float x,y; public Vector2(float x,float y) {this.x=x;this.y=y;} } }
namespace CombatExtended {
 // Field/property boundary only. The adapter under test is production code; installed
 // CE member signatures are independently checked by Tests/OriginBlade/ApiChecks.
 public class BaseTrajectoryWorker {}
 public class LerpedTrajectoryWorker : BaseTrajectoryWorker {}
 public class ProjectileCE : Verse.Thing {
  public UnityEngine.Vector3 exactPosition, LastPos, velocity;
  public UnityEngine.Vector2 origin, Destination;
  public Verse.IntVec3 OriginIV3;
  public float shotSpeed, initialSpeed, shotAngle, shotRotation, shotHeight, startingTicksToImpact, GravityPerWidth;
  public int intTicksToImpact, FlightTicks, ticksToTruePosition;
  public double gravity;
  public bool landed, lerpPosition;
  public Verse.Thing launcher;
  public Verse.ThingDef equipmentDef;
  public Verse.LocalTargetInfo intendedTarget;
  public Verse.Sound.Sustainer ambientSustainer;
  public BaseTrajectoryWorker forcedTrajectoryWorker;
  public System.Collections.Generic.List<UnityEngine.Vector3> cachedPredictedPositions;
  public float? damageAmount;
  public UnityEngine.Vector3 ExactPosition {get=>exactPosition;set=>exactPosition=value;}
  public float DamageAmount {get=>(damageAmount??12f)*(forcedTrajectoryWorker is LerpedTrajectoryWorker?1f:shotSpeed*shotSpeed/(initialSpeed*initialSpeed));set=>damageAmount=value;}
  public void Tick() {}
  public void ExposeData() {}
  protected UnityEngine.Vector3 MoveForward()=>exactPosition;
 }
}

namespace AM.Patches {
 // The installed AM signature is checked separately by ApiChecks. This boundary reproduces
 // its prefix behavior so the regression exercises Harmony's actual nested patch calls.
 public static class Patch_InvisibilityUtility_IsPsychologicallyInvisible {
  [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
  public static bool Prefix(Verse.Pawn pawn, ref bool __result) { __result=true; return false; }
 }
}
namespace RimWorld {
 public static class InvisibilityUtility {
  public static bool GenuineInvisibility;
  [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
  public static bool IsPsychologicallyInvisible(Verse.Pawn pawn) => GenuineInvisibility;
 }
}
