// Only the map/clock/draw boundary is stubbed; the preview lifecycle is production code.
namespace UnityEngine
{
    public struct Vector3 { public float x, y, z; }
    public static class Mathf { public static float Min(float a, float b) => System.Math.Min(a,b); }
    public static class Time { public static float unscaledDeltaTime; }
}

namespace Verse
{
    public struct IntVec3
    {
        public int x, z;
        public IntVec3(int x, int z) { this.x = x; this.z = z; }
        public static bool operator ==(IntVec3 a, IntVec3 b) => a.x == b.x && a.z == b.z;
        public static bool operator !=(IntVec3 a, IntVec3 b) => !(a == b);
        public override bool Equals(object o) => o is IntVec3 v && this == v;
        public override int GetHashCode() => x * 397 ^ z;
        public bool InBounds(Map map) => x >= 0 && z >= 0 && x < 100 && z < 100;
        public bool Fogged(Map map) => map.Fogged;
        public UnityEngine.Vector3 ToVector3Shifted() => new UnityEngine.Vector3 { x = x + 0.5f, z = z + 0.5f };
    }
    public static class VectorExtensions
    {
        public static IntVec3 ToIntVec3(this UnityEngine.Vector3 v) => new IntVec3((int)v.x, (int)v.z);
    }
    public class Map { public bool Fogged; }
    public class MapComponent
    {
        protected Map map;
        public MapComponent(Map map) { this.map = map; }
        public virtual void MapComponentUpdate() { }
    }
    public class Thing { }
    public class Pawn : Thing
    {
        public bool Spawned = true, Dead, Downed;
        public Map Map;
        public JobDef CurJobDef = new JobDef();
        public Health health = new Health();
        public Stances stances = new Stances();
        public Jobs jobs = new Jobs();
        public IntVec3 Position = new IntVec3(10, 20);
    }
    public class TickManager { public int TicksGame; }
    public static class Find
    {
        public static Map CurrentMap;
        public static Selector Selector = new Selector();
        public static TickManager TickManager = new TickManager();
    }
}

namespace RimArt
{
    public static class ShinraCastAnimation
    {
        public static float Speed = 1f;
        public static bool TryRestore(Verse.Pawn p, out Handle h) { h = new Handle(); return true; }
        public class Handle
        {
            public float Time;
            public bool Finished, Valid = true;
            public void Stop() { Valid = false; }
            public bool Seek(float t) { if (!Valid) return false; Time = t; return true; }
            public bool Read(out float time, out bool finished)
            { time = Time; finished = Finished; return Valid; }
        }
    }
    internal static class ShinraSound
    {
        public static int Releases;
        public static Verse.IntVec3 Cell;
        public static void Charge(Verse.Map map, Verse.IntVec3 cell) { }
        public static void Release(Verse.Map map, Verse.IntVec3 cell) { Releases++; Cell = cell; }
    }
    internal static class ShinraVfxGraphics
    {
        public static int Calls;
        public static float Time;
        public static UnityEngine.Vector3 Centre;
        public static void Draw(UnityEngine.Vector3 centre, float time, Verse.Map map)
        { Calls++; Time = time; Centre = centre; }
    }
}

namespace Verse
{
    public interface IExposable { void ExposeData(); }
    public class Game { public RimArt.GameComponent_Shinra Shinra; public T GetComponent<T>() => (T)(object)Shinra; }
    public static class Current { public static Game Game = new Game(); }
    public class GameComponent
    { public GameComponent() {} public virtual void ExposeData() {} public virtual void GameComponentTick() {} }
    public class JobDef { public string defName = "AM_InAnimation"; }
    public class Jobs { public void EndCurrentJob(Verse.AI.JobCondition c) {} }
    public class Stances { public Stunner stunner = new Stunner(); }
    public class Stunner { public bool Stunned; }
    public class Health { public HediffSet hediffSet = new HediffSet(); }
    public class HediffSet { public System.Collections.Generic.List<Hediff> hediffs = new System.Collections.Generic.List<Hediff> { new Hediff() }; }
    public class Hediff { public Def def = new Def(); }
    public class Def { public string defName = "AG_RepulsionEye"; }
    public class Selector { public bool IsSelected(Pawn p) => true; }
    public static class GenDraw { public static void DrawRadiusRing(IntVec3 cell, float radius) {} }
    public static class PawnsFinder { public static Pawn[] AllMapsWorldAndTemporary_AliveOrDead = new Pawn[0]; }
    public enum LoadSaveMode { Inactive, Saving, LoadingVars, PostLoadInit }
    public enum LookMode { Deep, Reference }
    public static class Scribe { public static LoadSaveMode mode; public static System.Collections.Generic.Dictionary<string, object> Data = new(); }
    public static class Scribe_Values
    {
        public static void Look<T>(ref T v, string key, T defaultValue = default)
        { if (Scribe.mode == LoadSaveMode.Saving) Scribe.Data[key] = v;
          if (Scribe.mode == LoadSaveMode.LoadingVars) v = Scribe.Data.TryGetValue(key, out var o) ? (T)o : defaultValue; }
    }
    public static class Scribe_References { public static void Look<T>(ref T v, string key) => Scribe_Values.Look(ref v, key); }
    public static class Scribe_Collections
    { public static void Look<T>(ref System.Collections.Generic.List<T> v, string key, LookMode mode) => Scribe_Values.Look(ref v, key); }
}
namespace Verse.AI { public enum JobCondition { InterruptForced } }
namespace RimWorld { }
namespace RimArt
{
    public static class ShinraAcquisition { public static void Migrate(Verse.Pawn p) {} }
    public static class ShinraCombat
    { public static int Pushes; public static bool Threat;
      public static void Push(ShinraPawnState s) { Pushes++; }
      public static bool Threatened(ShinraPawnState s, float seconds) => Threat; }
}
