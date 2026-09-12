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
    public static class Find { public static Map CurrentMap; }
}

namespace RimArt
{
    internal static class ShinraVfxGraphics
    {
        public static int Calls;
        public static float Time;
        public static UnityEngine.Vector3 Centre;
        public static void Draw(UnityEngine.Vector3 centre, float time, Verse.Map map)
        { Calls++; Time = time; Centre = centre; }
    }
}
