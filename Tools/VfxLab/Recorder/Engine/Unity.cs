// Stand-in for the parts of UnityEngine the recorded kits call. The drawing code is linked in
// unchanged, so every signature here matches Unity's; the behaviour is only as much as a
// recording needs. Graphics.DrawMesh is the one member that does real work: it is the tap.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float f) => new Vector2(a.x * f, a.y * f);
        public static Vector2 operator *(float f, Vector2 a) => new Vector2(a.x * f, a.y * f);
        public static Vector2 operator /(Vector2 a, float f) => new Vector2(a.x / f, a.y / f);
        public float sqrMagnitude => x * x + y * y;
        public float magnitude => Mathf.Sqrt(sqrMagnitude);
        public Vector2 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : new Vector2(0f, 0f); } }
        public static Vector2 zero => new Vector2(0f, 0f);
        public static Vector2 right => new Vector2(1f, 0f);
        public static Vector2 left => new Vector2(-1f, 0f);
        public static Vector2 up => new Vector2(0f, 1f);
        public static Vector2 down => new Vector2(0f, -1f);
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t); }
        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) => new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float f) => new Vector3(a.x * f, a.y * f, a.z * f);
        public static Vector3 operator *(float f, Vector3 a) => new Vector3(a.x * f, a.y * f, a.z * f);
        public static Vector3 operator /(Vector3 a, float f) => new Vector3(a.x / f, a.y / f, a.z / f);
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => Mathf.Sqrt(sqrMagnitude);
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public override string ToString() => $"({x:0.00}, {y:0.00}, {z:0.00})";
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1f, 1f, 1f, 1f);
        public static Color black => new Color(0f, 0f, 0f, 1f);
        public static Color clear => new Color(0f, 0f, 0f, 0f);
        public static Color Lerp(Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }
        public static Color operator *(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a * f);
    }

    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Pow(float v, float e) => (float)Math.Pow(v, e);
        public static float Exp(float v) => (float)Math.Exp(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Acos(float v) => (float)Math.Acos(v);
        public static float Abs(float v) => Math.Abs(v);
        public static int Abs(int v) => Math.Abs(v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
        public static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Repeat(float t, float length) => Clamp(t - (float)Math.Floor(t / length) * length, 0f, length);
        public static float Floor(float v) => (float)Math.Floor(v);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int RoundToInt(float v) => (int)Math.Round(v);
        public static float Round(float v) => (float)Math.Round(v);
    }

    /// <summary>Only rotations about the vertical exist on a RimWorld map, so only that angle is kept.</summary>
    public struct Quaternion
    {
        public float eulerY;
        public static Quaternion identity => new Quaternion();
        public static Quaternion Euler(float x, float y, float z) => new Quaternion { eulerY = y };
        public static Quaternion AngleAxis(float angle, Vector3 axis) => new Quaternion { eulerY = angle * Math.Sign(axis.y) };
    }

    public struct Matrix4x4
    {
        public Vector3 position, scale;
        public float rotation;
        public static Matrix4x4 TRS(Vector3 pos, Quaternion q, Vector3 s) =>
            new Matrix4x4 { position = pos, rotation = q.eulerY, scale = s };
    }

    public class Object
    {
        public string name = "";
    }

    public class Shader : Object
    {
        public Shader(string name) { this.name = name; }
    }

    public enum TextureFormat { RGBA32, ARGB32 }

    public class Texture : Object { }

    public class Texture2D : Texture
    {
        /// <summary>The content path the texture was asked for, which is what the browser loads.</summary>
        public string path;
        public Texture2D(string path) { this.path = path; name = path; }
        public Texture2D(int width, int height, TextureFormat format = TextureFormat.RGBA32, bool mipChain = false)
        { path = "generated"; }
        public void SetPixel(int x, int y, Color c) { }
        public void Apply() { }
    }

    public class Material : Object
    {
        private static int next;
        public readonly int id = ++next;
        public Shader shader;
        public Texture mainTexture;
        public Color color = Color.white;
        public readonly Dictionary<string, string> textures = new();
        public readonly Dictionary<string, float> floats = new();
        public Material(Shader shader) { this.shader = shader; }
        public void SetTexture(string property, Texture texture) =>
            textures[property] = (texture as Texture2D)?.path ?? "generated";
        public void SetFloat(string property, float value) => floats[property] = value;
        public void SetColor(string property, Color value) { if (property == "_Color") color = value; }
    }

    public class MaterialPropertyBlock
    {
        internal Color? colour;
        internal float? age;
        public void SetColor(int id, Color value) { if (id == Verse.ShaderPropertyIDs.Color) colour = value; }
        public void SetFloat(int id, float value) { if (id == Verse.ShaderPropertyIDs.AgeSecs) age = value; }
        public void Clear() { colour = null; age = null; }
    }

    public class Mesh : Object
    {
        private static int next;
        public readonly int id = ++next;
        private Vector3[] vertexData = Array.Empty<Vector3>();
        private Vector2[] uvData;
        private int[] triangleData = Array.Empty<int>();

        public Vector3[] vertices { get => vertexData; set => vertexData = (Vector3[])value.Clone(); }
        public Vector2[] uv { get => uvData; set => uvData = (Vector2[])value.Clone(); }
        public int[] triangles { get => triangleData; set => triangleData = (int[])value.Clone(); }
        public void RecalculateNormals() { }
        public void RecalculateBounds() { }
    }

    public class Camera : Object { }

    public static class Graphics
    {
        public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer) =>
            RimArt.VfxLab.Tap.Draw(mesh, matrix, material, null);

        public static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer, Camera camera,
            int submeshIndex, MaterialPropertyBlock properties) =>
            RimArt.VfxLab.Tap.Draw(mesh, matrix, material, properties);
    }

    public static class Time
    {
        public static float unscaledDeltaTime = 1f / 60f;
        public static float deltaTime = 1f / 60f;
        public static float realtimeSinceStartup;
        public static int frameCount;
    }
}
