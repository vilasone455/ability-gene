// Only the Unity vector and scalar maths are stubbed. The block, the projection and the whole
// sequence are production code: SixPathsSlab.cs and SixPathsSlam.cs touch no Verse type, which is
// the point of keeping them apart from the drawing.
namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float f) => new Vector2(a.x * f, a.y * f);
        public float sqrMagnitude => x * x + y * y;
        public float magnitude => Mathf.Sqrt(sqrMagnitude);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float f) => new Vector3(a.x * f, a.y * f, a.z * f);
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public Vector3 normalized
        {
            get
            {
                float length = Mathf.Sqrt(x * x + y * y + z * z);
                return length < 1e-6f ? new Vector3(0f, 0f, 0f) : new Vector3(x / length, y / length, z / length);
            }
        }
    }

    public static class Mathf
    {
        public const float PI = 3.14159265358979f;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public static float Sqrt(float v) => (float)System.Math.Sqrt(v);
        public static float Cos(float v) => (float)System.Math.Cos(v);
        public static float Sin(float v) => (float)System.Math.Sin(v);
        public static float Abs(float v) => System.Math.Abs(v);
        public static float Pow(float v, float e) => (float)System.Math.Pow(v, e);
        public static int FloorToInt(float v) => (int)System.Math.Floor(v);
        public static float Min(float a, float b) => System.Math.Min(a, b);
        public static float Max(float a, float b) => System.Math.Max(a, b);
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
}
