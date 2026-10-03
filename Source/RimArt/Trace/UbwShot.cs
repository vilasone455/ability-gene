using System;
using System.Collections.Generic;
using UnityEngine;

namespace RimArt
{
    /// <summary>
    /// A mesh for a cutscene camera: every vertex has its 3D place (x east, y up, z north, cells) and its game
    /// position, where the game view draws it (x, z on the map). Built once; the cutscene camera draws the 3D
    /// places, and while it blends into the game view each vertex moves on the screen toward its game position.
    /// The lab's Mesh.setXYZ + setGame (Tools/VfxLab/SKETCHING.md, "A 3D camera").
    /// </summary>
    internal sealed class UbwMesh3
    {
        public readonly string Name;
        public Vector3[] Xyz;
        public Vector2[] Game;
        public Vector2[] Uv;
        public int[] Tri;
        /// <summary>Bumped when the arrays are replaced, so a renderer rebuilds what it made from them.</summary>
        public int Version;
        /// <summary>What a renderer made of it (the game's Unity mesh); its own business.</summary>
        public object Made;
        public int MadeVersion = -1;

        public UbwMesh3(string name) { Name = name; }

        public int Count => Xyz?.Length ?? 0;
    }

    /// <summary>Builds a <see cref="UbwMesh3"/>: polygons and quads with a 3D place and a game position per corner.</summary>
    internal sealed class UbwBuilder3
    {
        public readonly List<Vector3> Xyz = new List<Vector3>();
        public readonly List<Vector2> Game = new List<Vector2>();
        public readonly List<Vector2> Uv = new List<Vector2>();
        public readonly List<int> Tri = new List<int>();
        private static readonly Vector2 Flat = new Vector2(0.5f, 0.5f);

        public int Count => Xyz.Count;

        public void Clear()
        {
            Xyz.Clear(); Game.Clear(); Uv.Clear(); Tri.Clear();
        }

        public int Vertex(Vector3 xyz, Vector2 game, Vector2 uv)
        {
            Xyz.Add(xyz);
            Game.Add(game);
            Uv.Add(uv);
            return Xyz.Count - 1;
        }

        /// <summary>
        /// A triangle, turned to run clockwise in its game positions (the screen at the end of the blend, and the
        /// 3D camera's screen too, as it always looks north from the south); where the game positions have no area,
        /// clockwise seen from above.
        /// </summary>
        public void Triangle(int a, int b, int c)
        {
            Vector2 p = Game[a], q = Game[b], r = Game[c];
            float area = (q.x - p.x) * (r.y - p.y) - (r.x - p.x) * (q.y - p.y);
            if (Mathf.Abs(area) < 1e-9f)
            {
                Vector3 P = Xyz[a], Q = Xyz[b], R = Xyz[c];
                area = (Q.x - P.x) * (R.z - P.z) - (R.x - P.x) * (Q.z - P.z);
            }
            if (area > 0f) { int t = b; b = c; c = t; }
            Tri.Add(a); Tri.Add(b); Tri.Add(c);
        }

        /// <summary>A convex polygon as a fan; uvs null for a flat colour.</summary>
        public void Poly(IList<Vector3> xyz, IList<Vector2> game, IList<Vector2> uvs = null)
        {
            if (xyz.Count < 3) return;
            int b = Xyz.Count;
            for (int i = 0; i < xyz.Count; i++) Vertex(xyz[i], game[i], uvs != null ? uvs[i] : Flat);
            for (int i = 2; i < xyz.Count; i++) Triangle(b, b + i - 1, b + i);
        }

        /// <summary>A 2D mesh of the map camera (vertices x, 0, z) lying at height <paramref name="h"/>, moved by <paramref name="by"/>; its game positions are where it is (the height rule taken off by <paramref name="lift"/>: 3D z = z - lift).</summary>
        public void Ground(Mesh flat, Vector2 by, float h, float lift = 0f)
        {
            Vector3[] v = flat.vertices;
            Vector2[] uv = flat.uv;
            int[] tri = flat.triangles;
            int b = Xyz.Count;
            for (int i = 0; i < v.Length; i++)
            {
                float x = v[i].x + by.x, z = v[i].z + by.y;
                Vertex(new Vector3(x, h, z - lift), new Vector2(x, z), uv != null && uv.Length == v.Length ? uv[i] : Flat);
            }
            for (int i = 0; i < tri.Length; i++) Tri.Add(tri[i] + b);
        }

        /// <summary>What a map-camera builder holds (vertices x, 0, z), lying at height <paramref name="h"/>, the height rule taken off by <paramref name="lift"/> (3D z = z - lift); its game positions are where it is.</summary>
        public void Ground(UbwGraphics.Builder flat, float h, float lift = 0f)
        {
            int b = Xyz.Count;
            for (int i = 0; i < flat.Vertices.Count; i++)
            {
                Vector3 v = flat.Vertices[i];
                Vertex(new Vector3(v.x, h, v.z - lift), new Vector2(v.x, v.z), flat.Uvs[i]);
            }
            foreach (int t in flat.Triangles) Tri.Add(t + b);
        }

        /// <summary>What a map-camera builder holds (screen points x, z) standing up on the plane z = zp where the height rule draws them; its game positions are the points.</summary>
        public void OnPlane(UbwGraphics.Builder flat, float zp)
        {
            int b = Xyz.Count;
            for (int i = 0; i < flat.Vertices.Count; i++)
            {
                Vector3 v = flat.Vertices[i];
                Vertex(new Vector3(v.x, (v.z - zp) / UbwGraphics.Lift, zp), new Vector2(v.x, v.z), flat.Uvs[i]);
            }
            foreach (int t in flat.Triangles) Tri.Add(t + b);
        }

        /// <summary>Screen quads (x, z, w, h) standing up on the plane z = zp where the height rule draws them: the game position of each corner is the quad itself.</summary>
        public void Standing(float x, float z, float w, float h, float zp)
        {
            int b = Xyz.Count;
            for (int k = 0; k < 4; k++)
            {
                float cx = k < 2 ? -0.5f : 0.5f, cz = k == 1 || k == 2 ? 0.5f : -0.5f;
                float gx = x + cx * w, gz = z + cz * h;
                Vertex(new Vector3(gx, (gz - zp) / UbwGraphics.Lift, zp), new Vector2(gx, gz), new Vector2(cx + 0.5f, cz + 0.5f));
            }
            Triangle(b, b + 1, b + 2);
            Triangle(b, b + 2, b + 3);
        }

        public UbwMesh3 Into(UbwMesh3 m)
        {
            m.Xyz = Xyz.ToArray();
            m.Game = Game.ToArray();
            m.Uv = Uv.ToArray();
            m.Tri = Tri.ToArray();
            m.Version++;
            return m;
        }

        public UbwMesh3 Take(string name) => Into(new UbwMesh3(name));
    }

    /// <summary>
    /// How a draw meets the depth test in the lab (its 3D camera tests depth; the game draws far to near and tests
    /// nothing): Write, the lab's default; NoWrite, see-through (its _ZWrite 0); Over, lying on what is there,
    /// never tested (its _ZTest 8 or Graphics.Flat).
    /// </summary>
    internal enum UbwDepth : byte { Write, NoWrite, Over }

    /// <summary>One draw of a cutscene frame: a mesh at a place, its y scaled by <see cref="Sy"/> (a sword rising), in one colour. <see cref="Screen"/>: drawn where the game view draws it, whatever the camera does.</summary>
    internal struct UbwDraw3
    {
        public UbwMesh3 Mesh;
        public Material Material;
        public Color Colour;
        public Vector3 At;
        public float Sy;
        public bool Screen;
        public UbwDepth Depth;
    }

    /// <summary>
    /// One frame of a cutscene: the 3D camera (place, pitch above the horizontal, vertical field of view, clip
    /// distances; it always looks north), how far it has blended into the game view (centre and height in cells),
    /// the draws in order (far to near, nothing depth tested), and boxes over the finished frame (fractions of the
    /// screen, y from the top: a fade, the black bars). <see cref="Flat"/>: the map camera draws as usual.
    /// </summary>
    internal sealed class UbwShot
    {
        public bool Flat;
        public Vector3 Eye;
        public float Pitch, Fov, Near = 0.3f, Far = 9000f, Blend;
        public Vector2 GameCentre;
        public float CellsTall;
        public readonly List<UbwDraw3> Draws = new List<UbwDraw3>();
        public readonly List<(Rect box, Color colour)> Overlays = new List<(Rect, Color)>();

        public void Clear()
        {
            Draws.Clear();
            Overlays.Clear();
        }

        public void Draw(UbwMesh3 mesh, Color colour, Material material, Vector3 at, UbwDepth depth = UbwDepth.Write, float sy = 1f, bool screen = false)
        {
            if (mesh == null || mesh.Count == 0 || colour.a <= 0.002f || sy <= 0f) return;
            Draws.Add(new UbwDraw3 { Mesh = mesh, Colour = colour, Material = material, At = at, Sy = sy, Screen = screen, Depth = screen ? UbwDepth.Over : depth });
        }

        public void Fill(float x, float y, float w, float h, Color colour)
        {
            if (colour.a > 0f) Overlays.Add((new Rect(x, y, w, h), colour));
        }

        /// <summary>
        /// Where the game draws a cutscene frame: set by the game to its cutscene camera, by the lab's recorder to its
        /// tap. Null: nothing is drawn.
        /// </summary>
        internal static Action<UbwShot> Sink;
    }
}
