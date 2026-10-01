using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using static RimArt.VfxDraw;
using static RimArt.UbwGraphics;
using C = RimArt.UbwCrest;

namespace RimArt
{
    /// <summary>The world v4 baked for one sun: its plate ground (seed 1, the sketch's) and its crest. The last three are kept.</summary>
    internal sealed class UbwCrestWorld
    {
        public UbwTerrainBake Terrain;
        public UbwCrestBake Crest;
        private static readonly List<(string key, UbwCrestWorld world)> made = new List<(string, UbwCrestWorld)>();

        /// <summary>The sketch's low sun: the scene's shadow direction, <see cref="C.ShadowLength"/> cells of shadow per cell of height.</summary>
        public static Vector2 LowSun(Vector2 shadow)
        {
            float d = shadow.magnitude;
            if (d == 0f) d = 1f;
            return shadow / d * (float)C.ShadowLength;
        }

        public static UbwCrestWorld For(Vector2 sun)
        {
            string key = sun.x.ToString("0.000") + "," + sun.y.ToString("0.000");
            for (int i = 0; i < made.Count; i++) if (made[i].key == key) return made[i].world;
            var world = new UbwCrestWorld
            {
                Terrain = UbwTerrainGraphics.BakeFor(UbwTerrainGraphics.Ground(), sun),
                Crest = UbwCrestBake.Build(C.North, C.Height, C.SwordsPerCell, sun),
            };
            made.Add((key, world));
            if (made.Count > 3) made.RemoveAt(0);
            return world;
        }
    }

    /// <summary>
    /// The world v4's crest baked, relative to the caster: a bank of earth just past the map's north edge,
    /// backlit by the low sun. Its face is the plates' cracked earth in shade (CrestFace.png) with a lit rim
    /// along the top; swords, spears and greatswords stand packed on the top as dark silhouettes with a lit
    /// edge toward the sun, a thicket behind them shows only its upper parts; its shadow and the long
    /// shadows of two in five of its swords fall on the map's north cells. The port of crestOf and
    /// drawCrest in Tools/VfxLab/web/sketches/lib/ubw-crest.js (the 2D bake; the 3D one is the reveal
    /// shot's, not ported). Built once per map edge, height and sun; level shapes and upright silhouettes,
    /// the same from every side.
    /// </summary>
    internal sealed class UbwCrestBake
    {
        public int North;
        public double H;
        /// <summary>The mean top on the screen and the mean ground under the top, in cells past the edge.</summary>
        public float MeanTop, MeanGround;
        public Mesh Face, Rim, Shade, BladeShade;
        /// <summary>The thicket behind the top and the row on it: [0] the rim copy (lit edge), [1] the body.</summary>
        public readonly Mesh[] Back = new Mesh[2], Top = new Mesh[2];
        public int Vertices;

        internal static readonly Material faceMat = MaterialPool.MatFrom("RimArt/Trace/CrestFace", ShaderDatabase.Transparent);
        internal static readonly Color RimLight = new Color(1f, 0.67f, 0.35f), Silhouette = new Color(0.12f, 0.05f, 0.05f), HazeFar = new Color(0.93f, 0.61f, 0.36f);
        internal static readonly Color BackBody = Color.Lerp(Silhouette, HazeFar, 0.14f);
        private static readonly Vector2 Flat = new Vector2(0.5f, 0.5f);

        /// <summary>The top on the screen at x (cells from the caster), in cells north of the caster.</summary>
        public float TopZ(float x) => North + (float)C.TopOf(x, H);

        /// <summary>The crest whose map ends <paramref name="north"/> cells north of the caster, <paramref name="H"/> cells tall, <paramref name="perCell"/> swords a cell along its top, under this sun (the shadow vector per cell of height).</summary>
        public static UbwCrestBake Build(int north, double H, double perCell, Vector2 sun)
        {
            var k = new UbwCrestBake { North = north, H = H };
            Builder face = new Builder("UBW crest face"), rim = new Builder("UBW crest rim"), shade = new Builder("UBW crest shade"), bladeShade = new Builder("UBW crest blade shade");
            Builder[] back = { new Builder("UBW crest back 0"), new Builder("UBW crest back 1") }, top = { new Builder("UBW crest top 0"), new Builder("UBW crest top 1") };
            double E = north;
            double FootZ(double x) => E + C.FootOf(x);
            double GroundZ(double x) => FootZ(x) + C.DepthOf(x);
            double TopZ(double x) => GroundZ(x) + C.HeightOf(x, H) * C.Lift;

            int n = (int)Math.Round(2 * C.Span / C.Step), chunk = (int)Math.Round(C.Chunk / C.Step);
            double sum = 0, ground = 0;
            for (int i = 0; i < n; i++)
            {
                double x0 = -C.Span + i * C.Step, x1 = x0 + C.Step;
                double f0 = FootZ(x0), f1 = FootZ(x1), t0 = TopZ(x0), t1 = TopZ(x1), g0 = GroundZ(x0), h0 = C.HeightOf(x0, H), h1 = C.HeightOf(x1, H);
                float u0 = (float)(i % chunk * C.Step / C.Chunk), u1 = u0 + (float)(C.Step / C.Chunk);
                sum += t0 - E;
                ground += g0 - E;
                Poly(face, new[] { P(x0, f0), P(x0, t0), P(x1, t1), P(x1, f1) }, new[] { new Vector2(u0, 0.01f), new Vector2(u0, 0.99f), new Vector2(u1, 0.99f), new Vector2(u1, 0.01f) });
                Poly(rim, new[] { P(x0, t0), P(x1, t1), P(x1, t1 - C.RimW), P(x0, t0 - C.RimW) });
                // The shadow of the top edge (on the ground depthOf north of the foot, h up), kept south of the foot.
                Vector2 s0 = P(x0 + sun.x * h0, Math.Min(f0, f0 + C.DepthOf(x0) + sun.y * h0)), s1 = P(x1 + sun.x * h1, Math.Min(f1, f1 + C.DepthOf(x1) + sun.y * h1));
                Poly(shade, new[] { P(x0, f0), P(x1, f1), s1, s0 });
            }

            // Swords: gap 1 / density, a sixth of the places left empty. The rim copy sits a little toward the sun.
            double toSun = Math.Sign(-sun.x);
            if (toSun == 0) toSun = 1;
            void Place(double density, int seed, Action<double, int> fn)
            {
                double gap = 1 / density;
                for (int j = (int)Math.Floor(-C.Span / gap); j <= C.Span / gap; j++)
                    if (UbwTerrain.Hash(j, 1, seed) < .85) fn((j + .8 * UbwTerrain.Hash(j, 2, seed)) * gap, j);
            }
            void Put(Builder[] pair, double x, double z, double len, C.Weapon q)
            {
                WeaponInto(pair[0], x + toSun * .03, z + .018, len, q.Lean, q.Type, .035);
                WeaponInto(pair[1], x, z, len, q.Lean, q.Type);
            }
            // Behind the top: only the upper parts show over it. On the top: against the sky; two in five throw a
            // long shadow onto the map from where it leaves the bank. Both SwordScale times a field sword's height.
            Place(perCell * .8, 151, (x, j) =>
            {
                C.Weapon q = C.WeaponOf(j, 151);
                Put(back, x, TopZ(x) - .2 - .6 * UbwTerrain.Hash(j, 7, 151), q.Tall * C.Lift * q.Short * C.SwordScale * 1.1, q);
            });
            Place(perCell, 153, (x, j) =>
            {
                C.Weapon q = C.WeaponOf(j, 153);
                double h = C.HeightOf(x, H), zg = GroundZ(x), tall = q.Tall * C.SwordScale;
                Put(top, x, TopZ(x) - .05, tall * C.Lift * q.Short, q);
                if (UbwTerrain.Hash(j, 9, 153) > .4) return;
                double from = Math.Max(h, C.DepthOf(x) / Math.Max(.05, -sun.y)), to = h + tall;
                if (sun.y < 0 && from < to) Stroke(bladeShade, P(x + sun.x * from, zg + sun.y * from), P(x + sun.x * to, zg + sun.y * to), .08f);
            });

            k.MeanTop = (float)(sum / n);
            k.MeanGround = (float)(ground / n);
            k.Face = face.Take("UBW crest face");
            k.Rim = rim.Take("UBW crest rim");
            k.Shade = shade.Take("UBW crest shade");
            k.BladeShade = bladeShade.Take("UBW crest blade shade");
            for (int i = 0; i < 2; i++)
            {
                k.Back[i] = back[i].Take("UBW crest back " + i);
                k.Top[i] = top[i].Take("UBW crest top " + i);
            }
            k.Vertices = face.Count + rim.Count + shade.Count + bladeShade.Count + back[0].Count + back[1].Count + top[0].Count + top[1].Count;
            return k;
        }

        /// <summary>
        /// The crest over the map's plates round <paramref name="o"/>: its shadow and its swords' shadows on the map
        /// (just over the gear shadows), then over the plates the thicket, the face, the rim, the swords on the top.
        /// </summary>
        public void Draw(Vector2 o, in UbwLayers layers, float strength, Color tint)
        {
            DrawMesh(Shade, o, layers.GearShadow + 0.0012f, 1f, 1f, 0f, Fade(Black, 0.4f * strength / 0.32f), solid);
            DrawMesh(BladeShade, o, layers.GearShadow + 0.0013f, 1f, 1f, 0f, Fade(Black, 0.22f * strength / 0.32f), solid);
            DrawMesh(Back[0], o, layers.Floor + 0.0002f, 1f, 1f, 0f, Fade(RimLight, 0.35f), solid);
            DrawMesh(Back[1], o, layers.Floor + 0.00021f, 1f, 1f, 0f, BackBody, solid);
            DrawMesh(Face, o, layers.Floor + 0.0004f, 1f, 1f, 0f, tint, faceMat);
            DrawMesh(Rim, o, layers.Floor + 0.0005f, 1f, 1f, 0f, Fade(RimLight, 0.45f), solid);
            DrawMesh(Top[0], o, layers.Floor + 0.0006f, 1f, 1f, 0f, Fade(RimLight, 0.6f), solid);
            DrawMesh(Top[1], o, layers.Floor + 0.0007f, 1f, 1f, 0f, Silhouette, solid);
        }

        // ---- shapes ------------------------------------------------------------------------------------------------

        private static Vector2 P(double x, double z) => new Vector2((float)x, (float)z);

        /// <summary>A convex polygon of screen points; a sliver of no area is left out, as the sketch's polyInto does.</summary>
        internal static void Poly(Builder into, IList<Vector2> pts, IList<Vector2> uvs = null)
        {
            double area = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Count];
                area += (double)a.x * b.y - (double)b.x * a.y;
            }
            if (Math.Abs(area) < 1e-7) return;
            if (uvs == null)
            {
                var flat = new Vector2[pts.Count];
                for (int i = 0; i < flat.Length; i++) flat[i] = Flat;
                uvs = flat;
            }
            into.Poly(pts, uvs);
        }

        /// <summary>A stroke from a to b, w wide.</summary>
        internal static void Stroke(Builder into, Vector2 a, Vector2 b, float w)
        {
            float dx = b.x - a.x, dz = b.y - a.y, len = Mathf.Sqrt(dx * dx + dz * dz);
            if (len == 0f) len = 1f;
            float nx = -dz / len * w / 2f, nz = dx / len * w / 2f;
            Poly(into, new[] { new Vector2(a.x + nx, a.y + nz), new Vector2(b.x + nx, b.y + nz), new Vector2(b.x - nx, b.y - nz), new Vector2(a.x - nx, a.y - nz) });
        }

        /// <summary>
        /// A weapon standing on the screen: foot (fx, fz), len screen cells long, leaning <paramref name="lean"/> radians
        /// from upright (east positive). Type 0 a sword and 1 a greatsword, point in the ground (blade, guard, grip,
        /// pommel); 2 a spear, butt in the ground, head up. <paramref name="w"/> widens every piece (the rim copy).
        /// </summary>
        internal static void WeaponInto(Builder into, double fx, double fz, double len, double lean, int type, double w = 0)
        {
            double dx = Math.Sin(lean), dz = Math.Cos(lean), ax = dz, az = -dx;
            Vector2 At(double d, double side = 0) => P(fx + dx * d + ax * side, fz + dz * d + az * side);
            if (type == 2)
            {
                Stroke(into, At(0), At(len * .86), (float)(.045 + w));
                Poly(into, new[] { At(len * .8, 0), At(len * .88, -.075 - w), At(len + w, 0), At(len * .88, .075 + w) });
                return;
            }
            double bw = (type != 0 ? .15 : .085) + w, gw = (type != 0 ? .27 : .2) + w, G = len * (type != 0 ? .66 : .7);
            Poly(into, new[] { At(0, -bw * .35), At(0, bw * .35), At(G, bw / 2), At(G, -bw / 2) });
            Poly(into, new[] { At(G - .03 - w, -gw), At(G - .03 - w, gw), At(G + .03 + w, gw), At(G + .03 + w, -gw) });
            Stroke(into, At(G), At(len * .93), (float)(.05 + w));
            double k = .045 + w;
            Poly(into, new[] { At(len * .96, -k), At(len * .96 + k, 0), At(len * .96, k), At(len * .96 - k, 0) });
        }
    }
}
