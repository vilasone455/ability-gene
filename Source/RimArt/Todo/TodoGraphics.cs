using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;
using static RimArt.VfxDraw;

namespace RimArt
{
    /// <summary>
    /// The drawing pieces the three Todo pictures share (Boogie Woogie, stone throw, Black Flash):
    /// the kit's teal and dark outline (the sketches' Ink), a line through the first points of an array, the five-point dash,
    /// a filled fan rebuilt each frame (the stone's aura), and the negative cover for Black Flash.
    /// The lab's line and ringAt are GokuGraphics.Line and PaperBombGraphics.RingAt; this line
    /// differs only in taking a point count, so a bolt can be drawn up to its leader without a
    /// copy. Every routine keeps no state between frames except the fan pool.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class TodoGraphics
    {
        internal static readonly Color Teal = new Color(0.32f, 0.95f, 0.8f), TealPale = new Color(0.78f, 1f, 0.95f),
            Outline = new Color(0.03f, 0.04f, 0.05f), White = new Color(1f, 1f, 1f), Dust = new Color(0.52f, 0.45f, 0.37f);

        internal static readonly Material stone = MaterialPool.MatFrom("RimArt/Anchor/ClapStone", ShaderDatabase.Transparent);
        internal static readonly float ItemLayer = AltitudeLayer.Item.AltitudeFor(), ShadowLayer = AltitudeLayer.Shadows.AltitudeFor();

        private static readonly Vector2[] dashPoints = new Vector2[5];

        /// <summary>A straight dash from a to b as 5 points, so a taper at both ends leaves a body in the middle. One shared array: draw it before asking for another.</summary>
        internal static Vector2[] Dash(Vector2 a, Vector2 b)
        {
            for (int i = 0; i < 5; i++) dashPoints[i] = Vector2.Lerp(a, b, i * 0.25f);
            return dashPoints;
        }

        /// <summary>The lab's line through the first <paramref name="count"/> points; see GokuGraphics.Line.</summary>
        internal static void Line(Vector2[] pts, int count, float width, Color colour, Material material, float altitude,
            GokuGraphics.Taper taper = GokuGraphics.Taper.End)
        {
            int last = count - 1;
            if (count < 2 || colour.a <= 0.001f) return;
            Sides(count, out Vector2[] a, out Vector2[] b);
            for (int i = 0; i < count; i++)
            {
                Vector2 d = pts[Mathf.Min(last, i + 1)] - pts[Mathf.Max(0, i - 1)];
                float len = d.magnitude, u = i / (float)last;
                if (len == 0f) len = 1f;
                float w = width / 2f * (taper == GokuGraphics.Taper.Both ? Mathf.Max(0f, Mathf.Sin(u * Mathf.PI))
                    : taper == GokuGraphics.Taper.End ? Mathf.Pow(1f - u, 0.6f) : 1f) + 0.004f;
                a[i] = new Vector2(pts[i].x - d.y / len * w, pts[i].y + d.x / len * w);
                b[i] = new Vector2(pts[i].x + d.y / len * w, pts[i].y - d.x / len * w);
            }
            Strip(a, b, colour, material ?? solid, altitude);
        }

        internal static void Line(Vector2[] pts, float width, Color colour, Material material, float altitude,
            GokuGraphics.Taper taper = GokuGraphics.Taper.End) => Line(pts, pts.Length, width, colour, material, altitude, taper);

        // ---- the fan ----

        // Graphics.DrawMesh reads a mesh when the frame renders, so each fan drawn in a frame is its
        // own mesh, handed out from a pool that starts again every frame.
        private static readonly List<Mesh> fans = new List<Mesh>();
        private static int fanFrame = -1, fansTaken;
        private static readonly Dictionary<int, Vector3[]> fanVertices = new Dictionary<int, Vector3[]>();
        private static readonly Dictionary<int, int[]> fanTriangles = new Dictionary<int, int[]>();

        /// <summary>A filled fan round <paramref name="centre"/> through the first <paramref name="count"/> points of <paramref name="ring"/>.</summary>
        internal static void Fan(Vector2 centre, Vector2[] ring, int count, Color colour, Material material, float altitude)
        {
            if (count < 3 || colour.a <= 0.001f) return;
            if (Time.frameCount != fanFrame)
            {
                fanFrame = Time.frameCount;
                fansTaken = 0;
            }
            if (fansTaken == fans.Count) fans.Add(new Mesh { name = "Todo fan " + fans.Count });
            Mesh mesh = fans[fansTaken++];
            if (!fanVertices.TryGetValue(count, out Vector3[] vertices))
            {
                fanVertices[count] = vertices = new Vector3[count + 1];
                var triangles = new int[count * 3];
                // Clockwise seen from above, so the fan survives backface culling.
                for (int i = 0; i < count; i++)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = 1 + (i + 1) % count;
                    triangles[i * 3 + 2] = 1 + i;
                }
                fanTriangles[count] = triangles;
            }
            vertices[0] = Vector3.zero;
            for (int i = 0; i < count; i++) vertices[i + 1] = new Vector3(ring[i].x - centre.x, 0f, ring[i].y - centre.y);
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = fanTriangles[count];
            mesh.RecalculateBounds();
            DrawMesh(mesh, centre, altitude, 1f, 1f, 0f, colour, material ?? solid);
        }

        // ---- the negative ----

        /// <summary>
        /// Unity's built-in Hidden/Internal-Colored with the blend Rinnegan's NegativeFlash uses: a draw
        /// coloured (a, a, a, a) turns what is under it into its negative by a. Null when the build
        /// lacks the shader; the negative is then skipped.
        /// </summary>
        internal static readonly Material invert = MakeInvert();
        // Internal-Colored multiplies by vertex colour and MeshPool's planes have none.
        private static readonly Mesh coverQuad = MakeQuad();
        private static bool warned;

        /// <summary>
        /// Turns everything drawn under <paramref name="altitude"/> in a square of <paramref name="size"/>
        /// cells round <paramref name="centre"/> into its negative by <paramref name="strength"/>, then
        /// pulls it toward grey by <paramref name="greyShare"/>.
        /// </summary>
        internal static void Negative(Vector2 centre, float size, float strength, float greyShare, float altitude, Mesh shape = null)
        {
            if (strength <= 0f) return;
            if (invert == null)
            {
                if (!warned) Log.Warning("[RimArt] Hidden/Internal-Colored was not found; Black Flash's negative is skipped.");
                warned = true;
                return;
            }
            DrawMesh(shape ?? coverQuad, centre, altitude, size, size, 0f, new Color(strength, strength, strength, strength), invert);
            DrawMesh(shape ?? coverQuad, centre, altitude + 0.001f, size, size, 0f, new Color(0.5f, 0.5f, 0.5f, greyShare * strength), solid);
        }

        private static Material MakeInvert()
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null) return null;
            var mat = new Material(shader) { renderQueue = 3000 };
            mat.SetInt("_SrcBlend", (int)BlendMode.OneMinusDstColor);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_Cull", (int)CullMode.Off);
            mat.SetInt("_ZWrite", 0);
            return mat;
        }

        private static Mesh MakeQuad()
        {
            var vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            var mesh = new Mesh
            {
                name = "Todo negative quad", vertices = vertices,
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A disc of radius 1 with white vertex colours, for a negative limited to a disc.</summary>
        internal static Mesh MakeDisc(int segments, string name)
        {
            var vertices = new Vector3[segments + 1];
            var colors = new Color[segments + 1];
            var triangles = new int[segments * 3];
            colors[0] = Color.white;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                colors[i + 1] = Color.white;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % segments + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = name, vertices = vertices, colors = colors, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
