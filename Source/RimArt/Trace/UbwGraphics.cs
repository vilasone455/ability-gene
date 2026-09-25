using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.VfxMath;

namespace RimArt
{
    /// <summary>
    /// The weapons a world is drawn with: the set the mix picks from, the atlas that holds every one in
    /// every shade (the baked field's one texture), and per weapon the outline and the silhouette for the
    /// trace look (the wire and the scan line).
    /// </summary>
    internal sealed class UbwWeaponSet
    {
        public UbwWeapon[] Weapons;
        public Material Atlas;
        public Material[] Wire, Mask;
    }

    /// <summary>
    /// The pieces the two Unlimited Blade Works pictures share: the materials and colours of the sketches,
    /// a mesh built from quads, polygons and strips (one per thing drawn in a frame), the batched flames
    /// (two draws for any number), the white, the wall of fire, the gear shadows, and the atlas layout of
    /// the standing swords. The port of quads, flames, flick, ringFlames, whiteDisc, annulus, fadeRing,
    /// cover, fireWall and skyGears in Tools/VfxLab/web/sketches/lib/ubw-pocket.js and gearMesh in
    /// lib/ubw.js. Every shape is level, so it looks the same from every side.
    ///
    /// Every triangle is written clockwise on screen, whichever way the sketch listed its corners, so it
    /// survives the game's backface culling.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class UbwGraphics
    {
        // ---- materials and colours (the sketches') ----------------------------------------------------
        internal static readonly Material flameSolid = MaterialPool.MatFrom("RimArt/Trace/Flame", ShaderDatabase.Transparent);
        internal static readonly Material flameGlow = MaterialPool.MatFrom("RimArt/Trace/Flame", ShaderDatabase.MoteGlow);
        internal static readonly Material earth = MaterialPool.MatFrom("RimArt/Trace/Earth", ShaderDatabase.Transparent);
        internal static readonly Material fadeMat = MaterialPool.MatFrom("RimArt/Trace/Fade", ShaderDatabase.Transparent);

        internal static readonly Color Trace = new Color(0.35f, 1f, 0.82f), TraceHot = new Color(0.82f, 1f, 0.95f);
        internal static readonly Color FireOuter = new Color(1f, 0.42f, 0.12f), FireCore = new Color(1f, 0.82f, 0.45f), Ember = new Color(1f, 0.62f, 0.28f);
        internal static readonly Color DeepFire = new Color(0.8f, 0.22f, 0.07f), Soot = new Color(0.3f, 0.12f, 0.07f);
        internal static readonly Color WhiteHot = new Color(1f, 0.97f, 0.9f), White = new Color(1f, 1f, 1f), Black = new Color(0f, 0f, 0f);
        internal static readonly Color Tint = new Color(1f, 0.86f, 0.76f), Twilight = new Color(1f, 0.7f, 0.55f), Haze = new Color(0.7f, 0.45f, 0.4f);
        internal static readonly Color DuskDark = new Color(0.2f, 0.1f, 0.06f), Sunset = new Color(1f, 0.55f, 0.25f), FarEarth = new Color(0.46f, 0.3f, 0.25f);

        internal static readonly float Shadows = AltitudeLayer.Shadows.AltitudeFor(), Building = AltitudeLayer.Building.AltitudeFor(),
            Terrain = AltitudeLayer.Terrain.AltitudeFor();
        internal const float Lift = (float)UbwBlade.Lift, D2R = Mathf.Deg2Rad;

        // ---- the weapon set -------------------------------------------------------------------------------
        /// <summary>
        /// Set by the game (Trace/Kit/UbwAtlasBuilder.cs) to the set built from the weapons' own textures.
        /// Without it, the lab's six reference weapons and the local reference atlas
        /// (make_trace_trial_textures.py, git-excluded).
        /// </summary>
        internal static Func<UbwWeaponSet> Provider;
        private static UbwWeaponSet lab;

        internal static UbwWeaponSet Set => (Provider != null ? Provider() : null) ?? LabSet();

        private static UbwWeaponSet LabSet()
        {
            if (lab != null) return lab;
            UbwWeapon[] weapons = UbwWeapons.Lab;
            lab = new UbwWeaponSet
            {
                Weapons = weapons,
                Atlas = MaterialPool.MatFrom("RimArt/TraceTrial/Atlas", ShaderDatabase.Transparent),
                Wire = new Material[weapons.Length],
                Mask = new Material[weapons.Length],
            };
            for (int i = 0; i < weapons.Length; i++)
            {
                lab.Wire[i] = MaterialPool.MatFrom("RimArt/TraceTrial/" + weapons[i].Name + "Outline", ShaderDatabase.MoteGlow);
                lab.Mask[i] = MaterialPool.MatFrom("RimArt/TraceTrial/" + weapons[i].Name + "Mask", ShaderDatabase.MoteGlow);
            }
            return lab;
        }

        // ---- the atlas ------------------------------------------------------------------------------------
        // Cells of AtlasCell pixels on an 8 x 8 square of AtlasSide; each picture fills its cell but for
        // AtlasPad clear pixels round it. Row r is weapon r with its colour times a grey per column: the two
        // edge greys (0, 1), the face lit three ways (2, 3, 4), the two dark bands low on the blade (5, 6).
        // Row 6 holds flat swatches for the ground marks. The numbers are make_trace_trial_textures.py's.
        internal const int AtlasCell = 128, AtlasPad = 8, AtlasSide = 1024, AtlasRows = 6;
        internal const int Edge0 = 0, Edge1 = 1, LowBand = 5, LowerBand = 6, SwatchRow = 6;
        internal static readonly float[] Shades = { 0.28f, 0.38f, 0.82f, 0.91f, 1f, 0.91f * 0.76f, 0.91f * 0.76f * 0.76f };
        internal static readonly float[] FaceLit = { 0.82f, 0.91f, 1f };
        internal static readonly int[] FaceCol = { 2, 3, 4 };
        internal const int SwCrack = 0, SwHole = 1, SwDirtDark = 2, SwDirtMid = 3, SwDirtLit = 4, SwContact = 5, SwWhite = 6;

        /// <summary>Cell (col, row) of the atlas as a uv rectangle, v up: the picture inside its padding.</summary>
        internal readonly struct Cell
        {
            public readonly float U0, V0, Du, Dv;

            public Cell(int col, int row)
            {
                float size = (AtlasCell - 2f * AtlasPad) / AtlasSide;
                U0 = (col * AtlasCell + AtlasPad) / (float)AtlasSide;
                V0 = 1f - (row * AtlasCell + AtlasCell - AtlasPad) / (float)AtlasSide;
                Du = size;
                Dv = size;
            }

            public Vector2 At(double u, double v) => new Vector2(U0 + (float)u * Du, V0 + (float)v * Dv);
        }

        /// <summary>The middle of a swatch of row 6: one flat colour.</summary>
        internal static Vector2 Swatch(int col)
        {
            var r = new Cell(col, SwatchRow);
            return new Vector2(r.U0 + r.Du / 2f, r.V0 + r.Dv / 2f);
        }

        // ---- a mesh from quads, polygons and strips ---------------------------------------------------------

        /// <summary>
        /// Vertices on the floor (x, z), their uv and triangles, made into a mesh: built once for the field,
        /// or again each frame for what moves. Every triangle is written clockwise on screen.
        /// </summary>
        internal sealed class Builder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
            private Mesh mesh;
            private readonly string name;

            public Builder(string name) { this.name = name; }

            public int Count => Vertices.Count;

            public void Clear()
            {
                Vertices.Clear();
                Uvs.Clear();
                Triangles.Clear();
            }

            public int Vertex(float x, float z, Vector2 uv)
            {
                Vertices.Add(new Vector3(x, 0f, z));
                Uvs.Add(uv);
                return Vertices.Count - 1;
            }

            /// <summary>A triangle by vertex index, turned to run clockwise on screen if it does not.</summary>
            public void Tri(int a, int b, int c)
            {
                Vector3 p = Vertices[a], q = Vertices[b], r = Vertices[c];
                float area = (q.x - p.x) * (r.z - p.z) - (r.x - p.x) * (q.z - p.z);
                if (area > 0f) { int t = b; b = c; c = t; }
                Triangles.Add(a); Triangles.Add(b); Triangles.Add(c);
            }

            /// <summary>The lab's quads(): MeshPool.plane10 at x, z scaled w by h and turned angle degrees clockwise, with uv 0..1 (or a cell of the atlas).</summary>
            public void Quad(float x, float z, float w, float h, float angle, Cell? cell = null)
            {
                float t = angle * D2R, ct = Mathf.Cos(t), st = Mathf.Sin(t);
                int b = Vertices.Count;
                for (int k = 0; k < 4; k++)
                {
                    float cx = k < 2 ? -0.5f : 0.5f, cz = k == 1 || k == 2 ? 0.5f : -0.5f, u = k < 2 ? 0f : 1f, v = k == 1 || k == 2 ? 1f : 0f;
                    float px = cx * w, pz = cz * h;
                    Vertex(x + px * ct + pz * st, z - px * st + pz * ct, cell.HasValue ? cell.Value.At(u, v) : new Vector2(u, v));
                }
                Tri(b, b + 1, b + 2);
                Tri(b, b + 2, b + 3);
            }

            /// <summary>A convex polygon of screen points with their uv, as a fan.</summary>
            public void Poly(IList<Vector2> points, IList<Vector2> uvs)
            {
                if (points.Count < 3) return;
                int b = Vertices.Count;
                for (int i = 0; i < points.Count; i++) Vertex(points[i].x, points[i].y, uvs[i]);
                for (int i = 2; i < points.Count; i++) Tri(b, b + i - 1, b + i);
            }

            /// <summary>A ribbon between two lines of points, one uv for both sides (a swatch) or one per point.</summary>
            public void Strip(IList<Vector2> a, IList<Vector2> b, Vector2 uv)
            {
                int start = Vertices.Count;
                for (int i = 0; i < a.Count; i++)
                {
                    Vertex(a[i].x, a[i].y, uv);
                    Vertex(b[i].x, b[i].y, uv);
                    if (i == 0) continue;
                    int n = start + i * 2;
                    Tri(n - 2, n, n - 1);
                    Tri(n - 1, n, n + 1);
                }
            }

            /// <summary>The lab's line(): a line through points, taper 0 keeps the width, 1 thins it to nothing at the far end, 2 at both ends.</summary>
            public void Line(IList<Vector2> pts, float width, Vector2 uv, int taper)
            {
                if (pts.Count < 2) return;
                int last = pts.Count - 1;
                var a = new Vector2[pts.Count];
                var b = new Vector2[pts.Count];
                for (int i = 0; i <= last; i++)
                {
                    Vector2 prev = pts[Mathf.Max(0, i - 1)], next = pts[Mathf.Min(last, i + 1)];
                    float dx = next.x - prev.x, dz = next.y - prev.y, len = Mathf.Sqrt(dx * dx + dz * dz), u = i / (float)last;
                    if (len == 0f) len = 1f;
                    float w = width / 2f * (taper == 2 ? Mathf.Sin(u * Mathf.PI) : taper == 1 ? Mathf.Pow(1f - u, 0.6f) : 1f) + 0.004f;
                    a[i] = new Vector2(pts[i].x - dz / len * w, pts[i].y + dx / len * w);
                    b[i] = new Vector2(pts[i].x + dz / len * w, pts[i].y - dx / len * w);
                }
                Strip(a, b, uv);
            }

            /// <summary>The mesh as built now. Bake once and draw it many times, or Bake again each frame for what moves.</summary>
            public Mesh Bake()
            {
                if (mesh == null) mesh = new Mesh { name = name };
                else mesh.Clear();
                mesh.vertices = Vertices.ToArray();
                mesh.uv = Uvs.ToArray();
                mesh.triangles = Triangles.ToArray();
                mesh.RecalculateBounds();
                return mesh;
            }

            /// <summary>A fresh mesh from what is built, for a bake that keeps several.</summary>
            public Mesh Take(string meshName)
            {
                var made = new Mesh { name = meshName, vertices = Vertices.ToArray(), uv = Uvs.ToArray(), triangles = Triangles.ToArray() };
                made.RecalculateNormals();
                made.RecalculateBounds();
                return made;
            }
        }

        // One builder per key, kept: what a frame draws is rebuilt into the same mesh next frame.
        private static readonly Dictionary<string, Builder> builders = new Dictionary<string, Builder>();

        internal static Builder Scratch(string key)
        {
            if (!builders.TryGetValue(key, out Builder b))
            {
                b = new Builder(key);
                builders[key] = b;
            }
            b.Clear();
            return b;
        }

        /// <summary>Draws what a builder holds at <paramref name="at"/>, in one draw.</summary>
        internal static void Draw(Builder b, Vector2 at, float altitude, Color colour, Material material)
        {
            if (b.Count == 0 || colour.a <= 0.002f) return;
            DrawMesh(b.Bake(), at, altitude, 1f, 1f, 0f, colour, material);
        }

        // ---- fixed meshes -------------------------------------------------------------------------------------

        /// <summary>The lab's Meshes.disc: a fan of <paramref name="segments"/> triangles at radius 1.</summary>
        internal static Mesh Disc(int segments, string name)
        {
            var b = new Builder(name);
            b.Vertex(0f, 0f, new Vector2(0.5f, 0.5f));
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                b.Vertex(Mathf.Cos(a), Mathf.Sin(a), new Vector2(0.5f, 0.5f));
            }
            for (int i = 0; i < segments; i++) b.Tri(0, (i + 1) % segments + 1, i + 1);
            return b.Take(name);
        }

        /// <summary>The lab's Meshes.band: a ring from radius <paramref name="inner"/> to <paramref name="outer"/>.</summary>
        internal static Mesh Band(float inner, float outer, int segments, string name)
        {
            var b = new Builder(name);
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f, c = Mathf.Cos(a), s = Mathf.Sin(a);
                b.Vertex(c * inner, s * inner, new Vector2(0.5f, 0.5f));
                b.Vertex(c * outer, s * outer, new Vector2(0.5f, 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                int v = i * 2;
                b.Tri(v, v + 2, v + 1);
                b.Tri(v + 1, v + 2, v + 3);
            }
            return b.Take(name);
        }

        internal static readonly Mesh disc72 = Disc(72, "UBW disc");

        // ---- flames ----------------------------------------------------------------------------------------------

        internal struct Flame
        {
            public float X, Z, W, H;
        }

        /// <summary>Flame k of n standing along something: its height at s, flickering, 0.75 to 1.25 of h.</summary>
        internal static float Flick(int k, float s, float h) =>
            h * (0.55f + 0.25f * Mathf.Sin(s * 11f + k * 1.7f) + 0.2f * Mathf.Sin(s * 17.3f + k * 2.9f)) * (0.75f + 0.5f * Rand(k * 13 + 1));

        /// <summary>A ring of flames of radius r round c, only those whose angle <paramref name="lit"/> allows, into the list.</summary>
        internal static void RingFlames(List<Flame> list, Vector2 c, float r, float s, float h, Func<float, bool> lit = null)
        {
            int n = Mathf.Max(10, Mathf.CeilToInt(Mathf.PI * 2f * r / 0.26f));
            for (int i = 0; i < n; i++)
            {
                float a = (i + Rand(i * 7 + 3) * 0.6f) / n * Mathf.PI * 2f;
                if (lit != null && !lit(a)) continue;
                list.Add(new Flame { X = c.x + Mathf.Cos(a) * r, Z = c.y + Mathf.Sin(a) * r, W = 0.3f * (0.8f + 0.4f * Rand(i * 5 + 2)), H = Flick(i, s, h) });
            }
        }

        private static void SouthLast(List<Flame> list) => list.Sort((p, q) => q.Z.CompareTo(p.Z));

        /// <summary>
        /// Flames standing on the floor, each with its foot at x, z: the outer flame and a core half as wide
        /// and 0.6 as tall, in two draws. South ones draw over north ones. <paramref name="see"/> draws the
        /// outer flame see-through instead of added light, so it shows in front of white.
        /// </summary>
        internal static void Flames(string key, List<Flame> list, float alpha, Vector2 at, float altitude, bool see = false)
        {
            if (list.Count == 0 || alpha <= 0f) return;
            SouthLast(list);
            Builder outer = Scratch(key + " outer"), core = Scratch(key + " core");
            foreach (Flame f in list)
            {
                outer.Quad(f.X - at.x, f.Z - at.y + f.H * 0.5f, f.W, f.H, 0f);
                core.Quad(f.X - at.x, f.Z - at.y + f.H * 0.3f, f.W * 0.5f, f.H * 0.6f, 0f);
            }
            Draw(outer, at, altitude, Fade(FireOuter, (see ? 0.85f : 0.5f) * alpha), see ? flameSolid : flameGlow);
            Draw(core, at, altitude + 0.0005f, Fade(FireCore, 0.5f * alpha), flameGlow);
        }

        // ---- white -----------------------------------------------------------------------------------------------

        /// <summary>The flash that takes everyone inside radius r (or brings them back): a white disc and a glow past it.</summary>
        internal static void WhiteDisc(Vector2 c, float r, float alpha, float altitude)
        {
            if (alpha <= 0f) return;
            DrawMesh(disc72, c, altitude, r, r, 0f, Fade(WhiteHot, alpha), solid);
            Sprite(c, r * 2.9f, r * 2.9f, Fade(WhiteHot, 0.4f * alpha), glow, altitude + 0.001f);
        }

        private static void Ring(Builder b, float r0, float r1, int n, Vector2 uvInner, Vector2 uvOuter)
        {
            int start = b.Count;
            for (int i = 0; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                b.Vertex(ca * r0, sa * r0, uvInner);
                b.Vertex(ca * r1, sa * r1, uvOuter);
                if (i == 0) continue;
                int v = start + i * 2;
                b.Tri(v - 2, v, v - 1);
                b.Tri(v - 1, v, v + 1);
            }
        }

        /// <summary>A flat ring from r0 to r1 round c.</summary>
        internal static void Annulus(string key, Vector2 c, float r0, float r1, Color colour, float altitude, int n = 96)
        {
            if (colour.a <= 0.002f) return;
            Builder b = Scratch(key);
            Ring(b, r0, r1, n, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            Draw(b, c, altitude, colour, solid);
        }

        /// <summary>A ring from r0 to r1 whose opacity runs from 0 at r0 to 1 at r1 (<paramref name="rising"/> false: the other way), through the gradient texture across it.</summary>
        internal static void FadeRing(string key, Vector2 c, float r0, float r1, Color colour, float altitude, bool rising = true, int n = 96)
        {
            if (r1 <= r0 || colour.a <= 0.002f) return;
            Builder b = Scratch(key);
            Ring(b, r0, r1, n, new Vector2(rising ? 0.02f : 0.98f, 0.5f), new Vector2(rising ? 0.98f : 0.02f, 0.5f));
            Draw(b, c, altitude, colour, fadeMat);
        }

        /// <summary>Everything further than r from c is white: the world not made yet, or already gone. The inner edge fades over 0.7 cells, under the wall of fire.</summary>
        internal static void Cover(string key, Vector2 c, float r, float alpha, float altitude, float far = 160f)
        {
            if (alpha <= 0f) return;
            Annulus(key + " solid", c, r, far, Fade(WhiteHot, alpha), altitude);
            FadeRing(key + " edge", c, Mathf.Max(0f, r - 0.7f), r, Fade(WhiteHot, alpha), altitude);
        }

        /// <summary>
        /// The wall of fire standing at radius r in front of the white: a darker back row, the flames, their
        /// cores, soot on the white just outside it, a glow on the ground just inside it.
        /// </summary>
        internal static void FireWall(string key, Vector2 c, float r, float s, float h, float altitude)
        {
            if (r <= 0.3f) return;
            var back = new List<Flame>();
            var front = new List<Flame>();
            RingFlames(back, c, r + 0.14f, s * 0.8f + 3f, h * 1.3f);
            RingFlames(front, c, r, s, h);
            FadeRing(key + " soot", c, r, r + 1f, Fade(Soot, 0.55f), altitude, false);
            SouthLast(back);
            SouthLast(front);
            Builder backQuads = Scratch(key + " back"), frontQuads = Scratch(key + " front"), core = Scratch(key + " core");
            foreach (Flame f in back) backQuads.Quad(f.X - c.x, f.Z - c.y + f.H * 0.5f, f.W * 1.3f, f.H, 0f);
            foreach (Flame f in front)
            {
                frontQuads.Quad(f.X - c.x, f.Z - c.y + f.H * 0.5f, f.W, f.H, 0f);
                core.Quad(f.X - c.x, f.Z - c.y + f.H * 0.3f, f.W * 0.5f, f.H * 0.6f, 0f);
            }
            Draw(backQuads, c, altitude + 0.001f, Fade(DeepFire, 0.85f), flameSolid);
            Draw(frontQuads, c, altitude + 0.002f, Fade(FireOuter, 0.9f), flameSolid);
            Draw(core, c, altitude + 0.003f, Fade(FireCore, 0.6f), flameGlow);
            PaperBombGraphics.RingAt(c, r, Fade(FireOuter, 0.45f), Floor + 0.03f, true, whiteGlow);
        }

        // ---- the gears in the sky, seen by their shadows ---------------------------------------------------------------

        /// <summary>The gears overhead: radius, place from the caster, degrees a second, teeth.</summary>
        internal static readonly float[] GearR = { 7f, 5f, 8f, 4.5f, 6f, 5.5f }, GearX = { -12f, 10f, 14f, -4f, 2f, -18f }, GearZ = { 8f, 11f, -9f, -12f, 2f, -6f },
            GearSpin = { 3.5f, -5f, 2.5f, -6f, 4f, 7f };
        internal static readonly int[] GearTeeth = { 12, 10, 14, 10, 12, 10 };

        private static readonly Dictionary<int, (Vector2[] v, int[] tri)> gearShapes = new Dictionary<int, (Vector2[], int[])>();

        /// <summary>A gear's silhouette at radius 1: toothed rim, five spokes and a hub. The port of gearMesh.</summary>
        internal static (Vector2[] v, int[] tri) GearShape(int teeth)
        {
            if (gearShapes.TryGetValue(teeth, out var shape)) return shape;
            var v = new List<Vector2>();
            var tri = new List<int>();
            int n = teeth * 4;
            for (int j = 0; j < n; j++)
            {
                float a = j / (float)n * Mathf.PI * 2f, o = j % 4 < 2 ? 1f : 0.84f;
                v.Add(new Vector2(Mathf.Cos(a) * o, Mathf.Sin(a) * o));
                v.Add(new Vector2(Mathf.Cos(a) * 0.64f, Mathf.Sin(a) * 0.64f));
                int p = j * 2, next = ((j + 1) % n) * 2;
                tri.Add(p); tri.Add(next); tri.Add(p + 1);
                tri.Add(p + 1); tri.Add(next); tri.Add(next + 1);
            }
            for (int k = 0; k < 5; k++)
            {
                float a = k / 5f * Mathf.PI * 2f, c = Mathf.Cos(a), s = Mathf.Sin(a), w = 0.06f;
                int b = v.Count;
                v.Add(new Vector2(c * 0.15f - s * w, s * 0.15f + c * w));
                v.Add(new Vector2(c * 0.66f - s * w, s * 0.66f + c * w));
                v.Add(new Vector2(c * 0.66f + s * w, s * 0.66f - c * w));
                v.Add(new Vector2(c * 0.15f + s * w, s * 0.15f - c * w));
                tri.Add(b); tri.Add(b + 1); tri.Add(b + 2);
                tri.Add(b); tri.Add(b + 2); tri.Add(b + 3);
            }
            int hub = v.Count;
            v.Add(Vector2.zero);
            for (int j = 0; j <= 16; j++)
            {
                float a = j / 16f * Mathf.PI * 2f;
                v.Add(new Vector2(Mathf.Cos(a) * 0.2f, Mathf.Sin(a) * 0.2f));
                if (j > 0) { tri.Add(hub); tri.Add(hub + j + 1); tri.Add(hub + j); }
            }
            shape = (v.ToArray(), tri.ToArray());
            gearShapes[teeth] = shape;
            return shape;
        }

        /// <summary>The gear turned by <paramref name="turn"/> radians and stretched along <paramref name="along"/> (radians) by <paramref name="stretch"/>: a shadow along the low sun.</summary>
        internal static Builder SpunGear(string key, int teeth, float turn, float along, float stretch)
        {
            var (v, tri) = GearShape(teeth);
            Builder b = Scratch(key);
            float ct = Mathf.Cos(turn), st = Mathf.Sin(turn), ca = Mathf.Cos(along), sa = Mathf.Sin(along);
            for (int k = 0; k < v.Length; k++)
            {
                float x = v[k].x * ct - v[k].y * st, z = v[k].x * st + v[k].y * ct;
                float p = (x * ca + z * sa) * stretch, q = -x * sa + z * ca;
                b.Vertex(p * ca - q * sa, p * sa + q * ca, new Vector2(0.5f, 0.5f));
            }
            for (int k = 0; k < tri.Length; k += 3) b.Tri(tri[k], tri[k + 1], tri[k + 2]);
            return b;
        }

        /// <summary>The gears turning in the sky, seen by their shadows: big, slow, stretched along the low sun. The port of skyGears.</summary>
        internal static void SkyGears(string key, Vector2 c, float s, Vector2 sun, float opacity, float altitude)
        {
            if (opacity <= 0f) return;
            float along = Mathf.Atan2(sun.y, sun.x);
            for (int i = 0; i < GearR.Length; i++)
            {
                Builder m = SpunGear(key + " " + i, GearTeeth[i], s * GearSpin[i] * D2R, along, 1.25f);
                var at = new Vector2(c.x + GearX[i] + Mathf.Sin(s * 0.21f + i * 2f) * 0.8f, c.y + GearZ[i] + Mathf.Cos(s * 0.17f + i) * 0.6f);
                Mesh mesh = m.Bake();
                DrawMesh(mesh, at, altitude + 0.001f + i * 0.0002f, GearR[i] * 1.05f, GearR[i] * 1.05f, 0f, Fade(Black, opacity * 0.35f), solid);
                DrawMesh(mesh, at, altitude + 0.0011f + i * 0.0002f, GearR[i], GearR[i], 0f, Fade(Black, opacity), solid);
            }
        }
    }
}
