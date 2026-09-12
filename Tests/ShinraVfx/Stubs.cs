// Only the map/clock/draw boundary is stubbed; the preview lifecycle is production code.
namespace UnityEngine
{
    public struct Vector3 { public float x, y, z; }
    public static class Time { public static float unscaledDeltaTime; }
}

namespace Verse
{
    public struct IntVec3
    {
        public int x, z;
        public IntVec3(int x, int z) { this.x = x; this.z = z; }
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
    public class Pawn
    {
        public bool Spawned = true, Dead, Downed;
        public Map Map;
        public IntVec3 Position = new IntVec3(10, 20);
    }
    public class TickManager { public int TicksGame; }
    public static class Find
    {
        public static Map CurrentMap;
        public static TickManager TickManager = new TickManager();
    }
}

namespace RimArt
{
    public static class ShinraCastAnimation
    {
        public class Handle
        {
            public float Time;
            public bool Finished, Valid = true;
            public bool Read(out float time, out bool finished)
            { time = Time; finished = Finished; return Valid; }
        }
    }
    internal static class ShinraSound
    {
        public static int Releases;
        public static Verse.IntVec3 Cell;
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
