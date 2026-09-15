// Stand-in for the parts of Verse, RimWorld, Verse.Sound and LudeonTK the recorded kits call.
// Values that change what gets drawn are the game's own, read out of Assembly-CSharp 1.6:
// altitude is layer x 0.36585367 (Verse.Altitudes.LayerSpacing), and the layer numbers below
// are the enum's. Everything else is the least that lets the real preview components run.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Verse
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class StaticConstructorOnStartup : Attribute { }

    public enum AltitudeLayer
    {
        BelowTerrain = 0, TerrainEdges = 1, Terrain = 2, TerrainScatter = 3, Floor = 4, Conduits = 5,
        FloorCoverings = 6, FloorEmplacement = 7, Filth = 8, Zone = 9, SmallWire = 10, LowPlant = 11,
        MoteLow = 12, Shadows = 13, DoorMoveable = 14, Building = 15, BuildingBelowTop = 16,
        BuildingOnTop = 17, Item = 18, ItemImportant = 19, LayingPawn = 20, PawnRope = 21,
        Projectile = 22, Pawn = 23, PawnUnused = 24, PawnState = 25, Blueprint = 26,
        MoteOverheadLow = 27, MoteOverhead = 28, Gas = 29, Skyfaller = 30, Weather = 31,
        LightingOverlay = 32, VisEffects = 33, FogOfWar = 34, Darkness = 35, WorldClipper = 36,
        Silhouettes = 37, MapDataOverlay = 38, MetaOverlays = 39,
    }

    public static class Altitudes
    {
        public const float LayerSpacing = 0.36585367f;
        public static float AltitudeFor(this AltitudeLayer layer) => (int)layer * LayerSpacing;
        public static float AltitudeFor(this AltitudeLayer layer, float offset) =>
            AltitudeFor(layer) + offset * LayerSpacing;
    }

    public struct IntVec3
    {
        public int x, y, z;
        public IntVec3(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
        public Vector3 ToVector3() => new Vector3(x, y, z);
        public Vector3 ToVector3Shifted() => new Vector3(x + 0.5f, y, z + 0.5f);
        public bool InBounds(Map map) => x >= 0 && z >= 0 && x < map.Size && z < map.Size;
        public bool Fogged(Map map) => false;
        public static bool operator ==(IntVec3 a, IntVec3 b) => a.x == b.x && a.y == b.y && a.z == b.z;
        public static bool operator !=(IntVec3 a, IntVec3 b) => !(a == b);
        public override bool Equals(object o) => o is IntVec3 v && v == this;
        public override int GetHashCode() => (x * 397) ^ z;
    }

    public static class Vector3Utility
    {
        public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
        public static IntVec3 ToIntVec3(this Vector3 v) =>
            new IntVec3((int)Math.Floor(v.x), 0, (int)Math.Floor(v.z));
        public static float AngleFlat(this Vector3 v) =>
            v.x == 0f && v.z == 0f ? 0f : ((float)(Math.Atan2(v.x, v.z) * 180.0 / Math.PI) + 360f) % 360f;
    }

    public class Map
    {
        /// <summary>The same 120 x 120 the lab's scene draws, so InBounds culls in the same places.</summary>
        public int Size = 120;
        internal readonly List<MapComponent> components = new();

        public T GetComponent<T>() where T : MapComponent
        {
            foreach (var c in components) if (c is T t) return t;
            var created = (T)Activator.CreateInstance(typeof(T), this);
            components.Add(created);
            return created;
        }
    }

    public abstract class MapComponent
    {
        public Map map;
        protected MapComponent(Map map) { this.map = map; }
        public virtual void MapComponentUpdate() { }
    }

    public class CameraShaker
    {
        public void DoShake(float mag) => RimArt.VfxLab.Tap.Event("shake", mag, null);
        public void DoShake(float mag, int ticks) => RimArt.VfxLab.Tap.Event("shake", mag, null);
    }

    public class CameraDriver { public CameraShaker shaker = new CameraShaker(); }

    public static class Find
    {
        public static Map CurrentMap;
        public static CameraDriver CameraDriver = new CameraDriver();
    }

    public static class UI
    {
        /// <summary>Every recording is taken on the centre cell; the lab moves it wherever you click.</summary>
        public static IntVec3 MouseCell() => new IntVec3(60, 0, 60);
    }

    public static class ShaderPropertyIDs
    {
        public static readonly int Color = 1;
        public static readonly int AgeSecs = 2;
    }

    public static class ShaderDatabase
    {
        public static readonly Shader Transparent = new Shader("Transparent");
        public static readonly Shader TransparentPostLight = new Shader("Transparent");
        public static readonly Shader Mote = new Shader("Mote");
        public static readonly Shader MoteGlow = new Shader("MoteGlow");
        public static readonly Shader Cutout = new Shader("Cutout");
        public static readonly Shader SolidColor = new Shader("Transparent");
    }

    public static class BaseContent
    {
        public static readonly Texture2D WhiteTex = new Texture2D("white");
        public static readonly Texture2D BadTex = new Texture2D("white");
    }

    public static class MeshPool
    {
        public static readonly Mesh plane10 = Plane(1f, "plane10");
        public static readonly Mesh plane20 = Plane(2f, "plane20");

        private static Mesh Plane(float size, string name)
        {
            float h = size * 0.5f;
            return new Mesh
            {
                name = name,
                vertices = new[] { new Vector3(-h, 0f, -h), new Vector3(-h, 0f, h), new Vector3(h, 0f, h), new Vector3(h, 0f, -h) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
        }
    }

    public static class MaterialPool
    {
        private static readonly Dictionary<string, Material> pool = new();
        public static Material MatFrom(string path, Shader shader)
        {
            string key = path + "|" + shader.name;
            if (!pool.TryGetValue(key, out var m))
                pool[key] = m = new Material(shader) { mainTexture = new Texture2D(path) };
            return m;
        }
        public static Material MatFrom(string path, Shader shader, Color color)
        {
            var m = MatFrom(path, shader);
            m.color = color;
            return m;
        }
    }

    public static class SolidColorMaterials
    {
        public static Material SimpleSolidColorMaterial(Color color, bool transparent = false) =>
            new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex, color = color };
        public static Material NewSolidColorMaterial(Color color, Shader shader) =>
            new Material(shader) { mainTexture = BaseContent.WhiteTex, color = color };
    }

    public static class ContentFinder<T> where T : class
    {
        /// <summary>Never null: the browser decides whether the path is a mod PNG or a vanilla stand-in.</summary>
        public static T Get(string path, bool reportFailure = true) => new Texture2D(path) as T;
    }

    public class Def { public string defName = ""; }

    public class ShaderTypeDef : Def
    {
        public Shader Shader;
    }

    public static class DefDatabase<T> where T : Def, new()
    {
        public static T GetNamedSilentFail(string name)
        {
            var def = new T { defName = name };
            if (def is ShaderTypeDef shader) shader.Shader = new Shader(name);
            return def;
        }
        public static T GetNamed(string name, bool errorOnFail = true) => GetNamedSilentFail(name);
    }

    public class SoundDef : Def { }

    public struct TargetInfo
    {
        public IntVec3 Cell;
        public Map Map;
        public TargetInfo(IntVec3 cell, Map map, bool allowNullMap = false) { Cell = cell; Map = map; }
    }

    public static class Log
    {
        public static void Message(string text) => Console.Error.WriteLine("[game log] " + text);
        public static void Warning(string text) => Console.Error.WriteLine("[game warning] " + text);
        public static void Error(string text) => Console.Error.WriteLine("[game error] " + text);
    }
}

namespace Verse.Sound
{
    public struct SoundInfo
    {
        public Verse.TargetInfo target;
        public static implicit operator SoundInfo(Verse.TargetInfo t) => new SoundInfo { target = t };
    }

    public static class SoundStarter
    {
        public static void PlayOneShot(this Verse.SoundDef def, SoundInfo info) =>
            RimArt.VfxLab.Tap.Event("sound", 0f, def?.defName);
        public static void PlayOneShotOnCamera(this Verse.SoundDef def, Verse.Map map = null) =>
            RimArt.VfxLab.Tap.Event("sound", 0f, def?.defName);
    }
}

namespace RimWorld
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DefOf : Attribute { }

    public static class DefOfHelper
    {
        /// <summary>Fills each Def field with a def named after the field, which is what the game's
        /// DefOf binding resolves to when the def exists.</summary>
        public static void EnsureInitializedInCtor(Type type)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public))
                if (typeof(Verse.Def).IsAssignableFrom(field.FieldType) && field.GetValue(null) == null)
                {
                    var def = (Verse.Def)Activator.CreateInstance(field.FieldType);
                    def.defName = field.Name;
                    field.SetValue(null, def);
                }
        }
    }
}

namespace LudeonTK
{
    public enum DebugActionType { Action, ToolMap, ToolMapForPawns, ToolWorld }

    [Flags]
    public enum AllowedGameStates { Invalid = 0, Entry = 1, Playing = 2, WorldRenderedNow = 4, IsCurrentlyOnMap = 8, HasGameCondition = 16, PlayingOnMap = 10, PlayingOnWorld = 6 }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DebugActionAttribute : Attribute
    {
        public string category, name;
        public DebugActionType actionType;
        public AllowedGameStates allowedGameStates;
        public DebugActionAttribute(string category = null, string name = null) { this.category = category; this.name = name; }
    }
}
