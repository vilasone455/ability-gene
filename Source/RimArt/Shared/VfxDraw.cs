using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// The drawing pieces every ported picture uses: the flat white and soft-disc materials, a
    /// ring and disc mesh, the sketches' two altitudes, one mesh draw with a colour, sprites,
    /// circles, ribbons between two lines and straight streaks. Everything is flat on the ground
    /// and keeps no state between frames except the strip pool. It began as the shared half of
    /// ThunderGodGraphics (the port of the lab's lib/flying-thunder-god.js).
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class VfxDraw
    {
        internal static readonly Material solid =
            new Material(ShaderDatabase.Transparent) { mainTexture = BaseContent.WhiteTex };
        internal static readonly Material whiteGlow =
            new Material(ShaderDatabase.MoteGlow) { mainTexture = BaseContent.WhiteTex };
        internal static readonly Material soft = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.Transparent);
        internal static readonly Material glow = MaterialPool.MatFrom("RimArt/SixPaths/SoftDisc", ShaderDatabase.MoteGlow);
        private static readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        internal static readonly Mesh disc = Ring(0f, "RimArt disc");
        private static readonly Mesh ring = Ring(0.965f, "RimArt ring");

        /// <summary>The sketches' dark outline colour.</summary>
        internal static readonly Color Ink = new Color(0.13f, 0.09f, 0.02f);

        /// <summary>The sketches' Y and Floor.</summary>
        internal static readonly float Overhead = AltitudeLayer.MoteOverhead.AltitudeFor();
        internal static readonly float Floor = AltitudeLayer.Filth.AltitudeFor();

        // One mesh per thing drawn in a frame: Graphics.DrawMesh reads a mesh when the frame
        // renders. Strips are handed out from a pool that starts again each frame, so two effects
        // in one frame never share one. The Spirit Bomb dome's flame ring takes 361 points.
        internal const int MostPoints = 361;
        private static readonly List<SixPathsStrip>[] pool = new List<SixPathsStrip>[MostPoints + 1];
        private static readonly int[] taken = new int[MostPoints + 1];
        private static readonly Vector2[][] sideA = new Vector2[MostPoints + 1][], sideB = new Vector2[MostPoints + 1][];
        private static int frame = -1;
        /// <summary>Strip meshes are written relative to this ground point and drawn at it.</summary>
        private static Vector2 anchor;

        /// <summary>Call once at the start of an effect's Draw with the effect's ground point.</summary>
        internal static void Begin(Vector2 ground) => anchor = ground;

        internal static bool Shown(Vector2 at, Map map)
        {
            IntVec3 cell = new Vector3(at.x, 0f, at.y).ToIntVec3();
            return cell.InBounds(map) && !cell.Fogged(map);
        }

        internal static Color Fade(Color colour, float alpha) => new Color(colour.r, colour.g, colour.b, alpha);
        internal static Vector2 Turn(float degrees) =>
            new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        /// <summary>
        /// <paramref name="angle"/> is clockwise seen from above, as Unity turns about the vertical,
        /// so a shape that follows a direction of d degrees (0 east, 90 north) is given -d.
        /// </summary>
        internal static void DrawMesh(Mesh mesh, Vector2 at, float altitude, float width, float depth, float angle,
            Color colour, Material material)
        {
            if (colour.a <= 0.001f) return;
            properties.SetColor(ShaderPropertyIDs.Color, colour);
            Graphics.DrawMesh(mesh, Matrix4x4.TRS(new Vector3(at.x, altitude, at.y), Quaternion.AngleAxis(angle, Vector3.up),
                new Vector3(width, 1f, depth)), material, 0, null, 0, properties);
        }

        internal static void Sprite(Vector2 at, float width, float depth, Color colour, Material material, float altitude, float angle = 0f) =>
            DrawMesh(MeshPool.plane10, at, altitude, width, depth, angle, colour, material);

        internal static void Circle(Vector2 at, float radius, float alpha, float altitude, Color colour)
        {
            if (radius <= 0f) return;
            DrawMesh(ring, at, altitude, radius, radius, 0f, Fade(colour, alpha), solid);
        }

        /// <summary>Two lines of <paramref name="points"/> points to fill in, then hand to <see cref="Strip"/>.</summary>
        internal static void Sides(int points, out Vector2[] a, out Vector2[] b)
        {
            a = sideA[points] ?? (sideA[points] = new Vector2[points]);
            b = sideB[points] ?? (sideB[points] = new Vector2[points]);
        }

        /// <summary>A ribbon between the two lines from <see cref="Sides"/>, in map coordinates.</summary>
        internal static void Strip(Vector2[] a, Vector2[] b, Color colour, Material material, float altitude)
        {
            if (colour.a <= 0.001f) return;
            for (int i = 0; i < a.Length; i++) { a[i] -= anchor; b[i] -= anchor; }
            SixPathsStrip strip = Next(a.Length);
            strip.Between(a, b);
            DrawMesh(strip.mesh, anchor, altitude, 1f, 1f, 0f, colour, material);
        }

        private static SixPathsStrip Next(int points)
        {
            if (Time.frameCount != frame)
            {
                frame = Time.frameCount;
                for (int i = 0; i < taken.Length; i++) taken[i] = 0;
            }
            List<SixPathsStrip> made = pool[points] ?? (pool[points] = new List<SixPathsStrip>());
            if (taken[points] == made.Count) made.Add(new SixPathsStrip("RimArt strip " + points + " " + made.Count, points));
            return made[taken[points]++];
        }

        /// <summary>A straight line from a to b, widest in the middle.</summary>
        internal static void Streak(Vector2 a, Vector2 b, float width, Color colour, Material material, float altitude, int steps = 8)
        {
            if (colour.a <= 0.001f) return;
            Vector2 along = b - a;
            float length = along.magnitude;
            if (length < 1e-5f) length = 1f;
            var normal = new Vector2(-along.y / length, along.x / length);
            Sides(steps + 1, out Vector2[] left, out Vector2[] right);
            for (int i = 0; i <= steps; i++)
            {
                float u = i / (float)steps, half = Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)) * width / 2f;
                left[i] = a + along * u + normal * half;
                right[i] = a + along * u - normal * half;
            }
            Strip(left, right, colour, material, altitude);
        }

        /// <summary>A flat ring of 64 segments from radius <paramref name="inner"/> to 1 in the XZ plane; inner 0 is a disc.</summary>
        internal static Mesh Ring(float inner, string name)
        {
            const int segments = 64;
            var vertices = new Vector3[(segments + 1) * 2];
            var indices = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * inner;
                vertices[i * 2 + 1] = direction;
                if (i == segments) continue;
                int v = i * 2, j = i * 6;
                indices[j] = v; indices[j + 1] = v + 2; indices[j + 2] = v + 1;
                indices[j + 3] = v + 1; indices[j + 4] = v + 2; indices[j + 5] = v + 3;
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = indices };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
