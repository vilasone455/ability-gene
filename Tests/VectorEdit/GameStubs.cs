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
  public float sqrMagnitude=>x*x+y*y+z*z;
  public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
  public Vector3 normalized{get{float m=magnitude;return m<=0f?zero:this/m;}}
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
 }
}

namespace Verse {
 using UnityEngine;
 public enum SimpleColor{White,Red,Green,Blue,Cyan,Magenta,Yellow,Orange}
 public enum LookMode{Value,Reference}
 public enum LoadSaveMode{Inactive,Saving,LoadingVars,ResolvingCrossRefs,PostLoadInit}
 public static class Scribe{public static LoadSaveMode mode=LoadSaveMode.Inactive;}
 public static class Scribe_Collections{
  public static void Look<K,V>(ref Dictionary<K,V> dict,string label,LookMode keys,LookMode values){}
 }
 public struct IntVec3 {
  public int x,y,z; public IntVec3(int x,int y,int z){this.x=x;this.y=y;this.z=z;}
  public override string ToString()=>$"({x}, {y}, {z})";
 }
 public static class VectorStubExtensions {
  public static IntVec3 ToIntVec3(this Vector3 v)=>new IntVec3((int)Math.Floor(v.x),(int)Math.Floor(v.y),(int)Math.Floor(v.z));
 }
 public class Map{public IntVec3 Size=new IntVec3(250,1,250);}
 public struct LocalTargetInfo {
  public IntVec3 Cell; public LocalTargetInfo(IntVec3 cell){Cell=cell;}
 }
 public class ProjectileProperties{public float speed=30f; public float SpeedTilesPerTick=>speed/60f;}
 public class ThingDef{public ProjectileProperties projectile=new();}
 public class Thing{public bool Destroyed,Spawned=true; public Map Map;}
 public class Pawn:Thing{}
 // Field names and types mirror Verse.Projectile, which is what the Harmony field refs bind to.
 public class Projectile:Thing {
  public ThingDef def=new();
  protected Vector3 origin;
  protected Vector3 destination;
  protected int ticksToImpact;
  protected int lifetime;
  protected Thing launcher;
  protected bool landed;
  protected bool preventFriendlyFire;
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
