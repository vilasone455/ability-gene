using System;
using System.Collections.Generic;
using System.Linq;
using RimArt;
using UnityEngine;

namespace UnityEngine {
 public class Material {}
 public partial struct Vector3 {
  public static Vector3 RotateTowards(Vector3 a,Vector3 b,float radians,float maxDelta) {
   float angle=(float)Math.Atan2(a.z,a.x), goal=(float)Math.Atan2(b.z,b.x);
   float d=(float)Math.Atan2(Math.Sin(goal-angle),Math.Cos(goal-angle));
   angle+=Math.Clamp(d,-radians,radians); return new((float)Math.Cos(angle),0,(float)Math.Sin(angle));
  }
 }
 public static partial class Mathf { public const float Deg2Rad=(float)Math.PI/180f; }
}
namespace Verse {
 public interface IExposable { void ExposeData(); }
 public static class Scribe_References { public static void Look<T>(ref T value,string key) {} }
 public static partial class Scribe_Collections {
  public static void Look<T>(ref List<T> value,string key,LookMode mode) {}
  public static void Look<K,V>(ref Dictionary<K,V> value,string key,LookMode km,LookMode vm,ref List<K> keys,ref List<V> values,bool log=true) {}
 }
 public class Game {
  private Dictionary<Type,object> components=new();
  public T GetComponent<T>() { if(!components.TryGetValue(typeof(T),out var c)) components[typeof(T)]=c=Activator.CreateInstance(typeof(T),this); return (T)c; }
 }
 public static class Current { public static Game Game=new(); }
 public class GameComponent { public GameComponent(){} public GameComponent(Game game){} public virtual void ExposeData(){} public virtual void GameComponentTick(){} }
 public class MapComponent { protected Map map; public MapComponent(Map map){this.map=map;} public virtual void ExposeData(){} public virtual void MapComponentTick(){} public virtual void MapComponentUpdate(){} public virtual void MapRemoved(){} }
 public static class Find { public static TickManager TickManager=new(); public static Map CurrentMap; }
 public class TickManager { public int TicksGame; }
 public enum ThingCategory { Item,Pawn,Building,Projectile }
 public enum DrawerType { None,MapMeshOnly,RealtimeOnly,MapMeshAndRealTime }
 public enum AltitudeLayer { MoteOverhead }
 public static class Altitudes { public static float AltitudeFor(this AltitudeLayer layer)=>1f; }
 public class MapDrawer { public void MapMeshDirty(IntVec3 cell,object flag){} }
 public static class MapMeshFlagDefOf { public static object Things=new(); }
 public partial class Map {
  private Dictionary<Type,object> components=new();
  public MapDrawer mapDrawer=new();
  public Dictionary<IntVec3,RimWorld.Building_Door> Doors=new();
  public T GetComponent<T>() { if(!components.TryGetValue(typeof(T),out var c)) components[typeof(T)]=c=Activator.CreateInstance(typeof(T),this); return (T)c; }
 }
 public partial struct IntVec3 {
  public static bool operator ==(IntVec3 a,IntVec3 b)=>a.x==b.x&&a.z==b.z;
  public static bool operator !=(IntVec3 a,IntVec3 b)=>!(a==b);
  public override bool Equals(object o)=>o is IntVec3 c&&this==c;
  public override int GetHashCode()=>HashCode.Combine(x,z);
  public bool Fogged(Map map)=>false;
  public RimWorld.Building_Door GetDoor(Map map)=>map.Doors.TryGetValue(this,out var d)?d:null;
  public List<Thing> GetThingList(Map map) { var cell=this; return map.listerThings.AllThings.Where(t=>t.Spawned&&t.Position==cell).ToList(); }
 }
 public static partial class VectorStubExtensions { public static Vector3 WithY(this Vector3 v,float y)=>new(v.x,y,v.z); }
 public partial class ThingDef {
  public ThingCategory category=ThingCategory.Item; public bool EverHaulable=true; public DrawerType drawerType=DrawerType.MapMeshOnly; public float Altitude;
 }
 public partial class Thing {
  public IntVec3 Position; public object ParentHolder=>Map; public int stackCount=1; public float Mass=1f;
  public float GetStatValue(object stat)=>Mass;
  public virtual Vector3 DrawPos=>Position.ToVector3Shifted();
  public void Destroy(){Destroyed=true;Spawned=false;}
  public void DrawNowAt(Vector3 pos){}
 }
 public partial class Pawn {
  public bool Downed,InMentalState,HasEye=true; public JobTracker jobs=new(); public PawnDrawer Drawer=new();
  public Verse.AI.JobDef CurJobDef=>jobs.def;
 }
 public class Corpse:Thing { public Pawn InnerPawn=new(); }
 public class PawnDrawer { public Tweener tweener=new(); }
 public class Tweener { public void ResetTweenedPosToRoot(){} }
 public class Pawn_DrawTracker { private Pawn pawn; public Vector3 DrawPos=>default; }
 public class SectionLayer_ThingsGeneral { protected void TakePrintFrom(Thing t){} }
 public partial class Stances { public Stunner stunner=new(); }
 public class Stunner { public bool Stunned; }
 public class JobTracker { public Verse.AI.JobDef def=new(){defName="AM_InAnimation"}; public void EndCurrentJob(Verse.AI.JobCondition condition){def=null;} }
 public partial struct LocalTargetInfo { public bool IsValid=>true; public bool ThingDestroyed=>false; }
 public static class GenRadial {
  public static IEnumerable<IntVec3> RadialCellsAround(IntVec3 cell,float radius,bool centre) {
   int r=(int)Math.Ceiling(radius);
   for(int x=-r;x<=r;x++) for(int z=-r;z<=r;z++) if(x*x+z*z<=radius*radius) yield return new(cell.x+x,0,cell.z+z);
  }
 }
 public class TargetInfo { public TargetInfo(IntVec3 cell,Map map){} }
 public class SoundDef {}
 public static class GenDraw { public static void DrawLineBetween(Vector3 a,Vector3 b,Material m,float width){} }
 public partial class Projectile {
  public Func<Vector3,Vector3,bool> Collision; public int Impacts;
  private bool CheckForFreeInterceptBetween(Vector3 a,Vector3 b) => Collision?.Invoke(a,b)??false;
  protected virtual void ImpactSomething(){Impacts++;Destroy();}
 }
}
namespace Verse.AI {
 public enum PathEndMode { OnCell,Touch }
 public enum JobCondition { InterruptForced }
 public class JobDef { public string defName; }
 public class Pawn_PathFollower {
  protected Verse.Pawn pawn;
  private PathEndMode peMode;
  public Verse.IntVec3 nextCell; public float nextCellCostLeft=15f,nextCellCostTotal=15f;
  public bool Moving; public Verse.LocalTargetInfo Destination;
  public int Starts;
  public Pawn_PathFollower(Verse.Pawn pawn){this.pawn=pawn;}
  public void StopDead(){Moving=false;}
  public void StartPath(Verse.LocalTargetInfo dest,PathEndMode mode){Destination=dest;peMode=mode;Moving=true;Starts++;}
  public void PatherTick(){}
  private float CostToPayThisTick()=>1f;
  private void TryEnterNextPathCell(){}
  private bool WillCollideWithPawnAt(Verse.IntVec3 c,bool forceOnlyStanding=false,bool useId=false)=>true;
 }
}
namespace RimWorld { public class Building_Door { public bool Open; } public static class StatDefOf { public static object Mass=new(); } }
namespace Verse.Sound {
 public enum MaintenanceType { PerTick }
 public struct SoundInfo { public float pitchFactor; public static SoundInfo InMap(Verse.TargetInfo target,MaintenanceType type)=>new(); }
 public static class Sounds {
  public static Sustainer TrySpawnSustainer(this Verse.SoundDef def,SoundInfo info)=>new(){info=info};
  public static void PlayOneShot(this Verse.SoundDef def,Verse.TargetInfo target){}
 }
}
namespace RimArt {
 public static class GravityAcquisition { public static bool HasEye(Verse.Pawn pawn)=>pawn.HasEye; }
 public static class GravityCommands { public static bool ValidTarget(Verse.Pawn pawn,Verse.IntVec3 cell)=>true; }
 public static class GravityDefOf { public static Verse.SoundDef AG_GravityHum=new(),AG_GravityImplode=new(); }
 public static class GravityCastAnimation {
  public sealed class Handle { public bool Stopped; public void Stop(){Stopped=true;} public bool Seek(float time)=>!Stopped; }
  public static bool TryStart(Verse.Pawn pawn,out Handle handle){handle=new();return true;}
  public static bool TryRestore(Verse.Pawn pawn,out Handle handle)=>TryStart(pawn,out handle);
 }
 public static class GravityGraphics { public static Material TrailMaterial=new(); public static void Draw(Vector3 p,float time,float mass,float fade,bool implode,Verse.Map map){} }
 public static class MapComponent_RetrievalHooks {
  public static HashSet<Verse.Thing> Targets=new(); public static bool IsTargeted(Verse.Thing t)=>Targets.Contains(t);
 }
 public static class ShinraCombat { public static bool BeforeProjectileTick(Verse.Thing thing,int ticks)=>true; }
}
namespace CombatExtended {
 public partial class ProjectileCE {
  public int ticksToImpact;
  public Func<Vector3,Vector3,bool> Collision; public int Impacts;
  protected virtual bool CheckForCollisionBetween()=>Collision?.Invoke(LastPos,ExactPosition)??false;
  protected virtual void ImpactSomething(){Impacts++;Destroy();}
 }
}
